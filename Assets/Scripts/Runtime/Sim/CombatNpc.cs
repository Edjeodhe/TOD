// 23차 쉬어가기 NPC 4종 (Combat 의 나머지 부분). 데이터 = db.npcs (NpcData.cs).
//   BeginRound(쉬어가기) → SpawnNpc: chance 확률로 한 명 (weight 가중, 아직 못 만난 NPC 는 unmetWeight 배).
//   E → TalkNpc: 축복/선물/경험치는 바로 보상, 도박은 첫 E 가 제안(체력 hpCost), 두 번째 E 가 받아들임.
//   만난 NPC 는 Profile.npcMet 에 남고, 재봉실에서는 HubGift(등반마다 한 번)가 hubRewards 중 하나를 준다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOD
{
    // 말 한 번의 결과: NPC 대사, 알림, 나쁜 결과인지, 도박 제안인지, 처음 만났는지
    public struct NpcTalkResult
    {
        public string line, notice;
        public bool bad, offer, first, rewarded;
    }

    public partial class Combat
    {
        NpcConfig N => db.npcs;

        void SpawnNpc(RunState r)
        {
            r.npc = null; r.npcStage = 0; r.npcGreeted = false;
            if (N?.npcs == null || N.npcs.Length == 0 || r.random() >= N.chance) return;
            List<int> met = r.profile?.npcMet ?? new List<int>();
            NpcDef pick = Pick(N.npcs.Where(n => n != null && n.weight > 0).ToList(),
                n => n.weight * (met.Contains((int)n.kind) ? 1 : Mathf.Max(1, N.unmetWeight)), r.random);
            if (pick != null) PlaceNpc(r, pick.kind);
        }

        void PlaceNpc(RunState r, NpcKind kind)
        {
            r.npcKind = kind;
            r.npc = Near(r, N.offset);
            r.npcStage = 0;
            r.npcGreeted = false;
        }

        // 개발자 모드: 지금 라운드(어디서든) 플레이어 옆에 그 NPC 를 부른다
        public void DevSummonNpc(RunState r, NpcKind kind)
        {
            if (N?.Get(kind) == null) return;
            PlaceNpc(r, kind);
        }

        static T Pick<T>(IList<T> list, Func<T, float> weight, Func<float> random) where T : class
        {
            float total = list.Sum(x => Mathf.Max(0, weight(x)));
            if (total <= 0) return list.Count > 0 ? list[0] : null;
            float roll = random() * total;
            foreach (T x in list) if ((roll -= Mathf.Max(0, weight(x))) < 0) return x;
            return list[list.Count - 1];
        }

        static string Line(string[] lines, Func<float> random) =>
            lines == null || lines.Length == 0 ? null : lines[Mathf.Min(lines.Length - 1, Mathf.FloorToInt(random() * lines.Length))];

        public NpcDef NpcHere(RunState r) => r.npc != null ? N?.Get(r.npcKind) : null;

        // 가까이 왔을 때 처음 한 번 혼잣말 (null = 없음)
        public string NpcApproach(RunState r)
        {
            NpcDef def = NpcHere(r);
            if (def == null || r.npcGreeted) return null;
            r.npcGreeted = true;
            return Line(r.npcStage >= 2 ? def.doneLines : def.approachLines, r.random);
        }

        public float NpcHpCost(RunState r, NpcDef def) => Mathf.Max(1, Round(r.maxHp * def.hpCost));

        public NpcTalkResult TalkNpc(RunState r)
        {
            var result = new NpcTalkResult();
            NpcDef def = NpcHere(r);
            if (def == null) return result;
            r.profile.npcMet ??= new List<int>();
            if (!r.profile.npcMet.Contains((int)def.kind)) { r.profile.npcMet.Add((int)def.kind); result.first = true; }
            if (r.npcStage >= 2) { result.line = Line(def.doneLines, r.random); return result; }
            if (def.hpCost > 0 && r.npcStage == 0)
            {
                r.npcStage = 1;
                float cost = NpcHpCost(r, def);
                result.offer = true;
                result.line = string.Format(def.offerLine ?? "{0}", cost);
                result.notice = string.IsNullOrEmpty(def.offerNotice) ? null : string.Format(def.offerNotice, cost);
                return result;
            }
            if (def.hpCost > 0)
            {
                float cost = NpcHpCost(r, def);
                r.hp = Mathf.Max(1, r.hp - cost);
                r.hurtFlash = Mathf.Max(r.hurtFlash, db.hero.hurtFlash * .6f);   // 거는 체력은 붉은 번쩍임으로만 (피해 숫자 글꼴에는 '-' 가 없다)
            }
            r.npcStage = 2;
            NpcReward reward = Pick(def.rewards ?? new NpcReward[0], x => x.weight, r.random);
            result.rewarded = true;
            result.line = !string.IsNullOrEmpty(reward?.line) ? reward.line : Line(def.giveLines, r.random);
            if (reward != null)
            {
                result.bad = reward.bad;
                result.notice = GiveRun(r, reward);
            }
            return result;
        }

        // 등반 중 보상 하나를 준다. 반환 = 알림 문구
        string GiveRun(RunState r, NpcReward w)
        {
            string Note(object a, object b = null) => string.IsNullOrEmpty(w.notice) ? null : string.Format(w.notice, a, b);
            switch (w.kind)
            {
                case NpcRewardKind.Effects:
                case NpcRewardKind.Boon:
                    ApplyEffects(r, w.effects);
                    return Note(w.name, EffectText(w.effects));
                case NpcRewardKind.Piece:
                    PieceDef def = Gear.AddItem(db, r.bag, -1, r.random, FateSet(r));   // 24차 예언 세트 가중치
                    if (def == null) { r.gold += WeatherGain(r, Mathf.RoundToInt(w.amount), ref r.goldCarry); return Note(null, w.amount); }
                    return Note(def.name);
                case NpcRewardKind.Xp:
                    int xp = WeatherGain(r, Mathf.Max(1, Mathf.RoundToInt(XpNeeded(r) * w.amount)), ref r.xpCarry);   // 24차 탐욕 날씨
                    GainXp(r, xp);
                    return Note(w.name, xp);
                case NpcRewardKind.Gold:
                    int gold = WeatherGain(r, Mathf.RoundToInt(w.amount), ref r.goldCarry);   // 24차 탐욕 날씨
                    r.gold += gold;
                    return Note(w.name, gold);
                case NpcRewardKind.Shards:
                    r.profile.shards += Mathf.RoundToInt(w.amount);
                    return Note(w.name, Mathf.RoundToInt(w.amount));
                case NpcRewardKind.Heal:
                    float healed = HealPlayer(r, r.maxHp * w.amount);   // 24차 교만 날씨
                    r.player.heal = db.rewards.healTime;
                    return Note(w.name, Mathf.Max(0, healed).ToString("0"));
                case NpcRewardKind.Hurt:
                    float lost = r.hp - Mathf.Max(1, r.hp - Round(r.maxHp * w.amount));
                    r.hp -= lost;
                    r.hurtFlash = Mathf.Max(r.hurtFlash, db.hero.hurtFlash);
                    return Note(w.name, lost.ToString("0"));
                default:
                    return Note(w.name);
            }
        }

        // 증강과 같은 계산으로 능력치를 바꾼다 (등반 동안 영구). augmentQueue 는 건드리지 않는다.
        public void ApplyEffects(RunState r, AugmentEffect[] effects)
        {
            if (effects == null) return;
            foreach (AugmentEffect fx in effects)
            {
                float Apply(float v) => fx.op == StatOp.Multiply ? v * fx.value : v + fx.value;
                switch (fx.stat)
                {
                    case RunStat.FireInterval: r.fireInterval = Mathf.Max(db.augments.minFireInterval, Apply(r.fireInterval)); break;
                    case RunStat.Reach: r.reach = Apply(r.reach); break;
                    case RunStat.NeedleSpeed: r.needleSpeed = Apply(r.needleSpeed); break;
                    case RunStat.Damage: r.damage = Mathf.Max(1, Apply(r.damage)); break;
                    case RunStat.YarnMax:
                        float before = r.yarnBonus;
                        r.yarnBonus = Apply(r.yarnBonus);
                        r.yarn = Mathf.Clamp(r.yarn + (r.yarnBonus - before), 0, YarnMax(r));
                        break;
                    case RunStat.Recharge: r.recharge = Apply(r.recharge); break;
                    case RunStat.Encumbrance: r.encumbrance = Apply(r.encumbrance); break;
                    case RunStat.DashCooldown: r.dashRate = Apply(r.dashRate); break;
                    case RunStat.MoveSpeed: r.moveScale = Mathf.Max(.3f, Apply(r.moveScale)); break;
                    case RunStat.Desire: AdjustDesire(r, fx.op == StatOp.Multiply ? r.desire * (fx.value - 1) : fx.value); break;
                    case RunStat.MaxHealth:
                        float gain = Apply(r.maxHp) - r.maxHp;
                        r.maxHp = Mathf.Max(1, r.maxHp + gain);
                        r.hp = Mathf.Clamp(r.hp + Mathf.Max(0, gain), 1, r.maxHp);
                        break;
                }
            }
        }

        // 알림에 붙이는 수치 ("피해 +2, 최대 체력 +10%")
        public string EffectText(AugmentEffect[] effects)
        {
            if (effects == null || effects.Length == 0) return "";
            string[] names = N?.statNames;
            return string.Join(", ", effects.Select(fx =>
            {
                string name = names != null && (int)fx.stat < names.Length ? names[(int)fx.stat] : fx.stat.ToString();
                string value = fx.op == StatOp.Multiply
                    ? $"{(fx.value >= 1 ? "+" : "")}{Mathf.RoundToInt((fx.value - 1) * 100)}%"
                    : $"{(fx.value >= 0 ? "+" : "")}{fx.value:0.##}";
                return $"{name} {value}";
            }));
        }

        // ---------------------------------------------------------------- 재봉실 (Profile 만, 등반 밖)

        // 그 NPC 가 이번 등반 사이에 선물을 줄 수 있는지
        public static bool NpcCanGift(Profile p, NpcKind kind) => p.npcMet != null && p.npcMet.Contains((int)kind) && (p.npcGifted == null || !p.npcGifted.Contains((int)kind));

        public NpcTalkResult HubGift(Profile p, NpcKind kind, Func<float> random)
        {
            var result = new NpcTalkResult();
            NpcDef def = N?.Get(kind);
            if (def == null) return result;
            if (!NpcCanGift(p, kind)) { result.line = Line(def.hubDoneLines, random); return result; }
            p.npcGifted ??= new List<int>();
            p.npcGifted.Add((int)kind);
            NpcReward w = Pick(def.hubRewards ?? new NpcReward[0], x => x.weight, random);
            result.rewarded = true;
            result.line = !string.IsNullOrEmpty(w?.line) ? w.line : Line(def.hubLines, random);
            if (w == null) return result;
            string Note(object a, object b = null) => string.IsNullOrEmpty(w.notice) ? null : string.Format(w.notice, a, b);
            switch (w.kind)
            {
                case NpcRewardKind.Piece:
                    int pieceKind = Gear.PickWeighted(db, Gear.Unowned(db, p), random);   // 23차: 레어도 가중치 (재봉실 NPC: 예언은 등반 중에만)
                    if (pieceKind < 0) goto case NpcRewardKind.Shards;
                    PieceDef piece = Gear.AddItem(db, p, pieceKind);
                    result.notice = Note(piece.name);
                    break;
                case NpcRewardKind.Shards:
                    int shards = Mathf.Max(1, Mathf.RoundToInt(w.amount));
                    p.shards += shards;
                    result.notice = Note(w.name, shards);
                    break;
                case NpcRewardKind.Effects:
                case NpcRewardKind.Boon:
                    p.npcBoons ??= new List<NpcBoon>();
                    p.npcBoons.Add(new NpcBoon { name = w.name, effects = w.effects });
                    result.notice = Note(w.name, EffectText(w.effects));
                    break;
                default:
                    result.notice = Note(w.name);
                    break;
            }
            return result;
        }

        // 등반 시작: 재봉실 NPC 가 걸어 둔 축복을 건다. 반환 = 건 축복 이름들 (없으면 null)
        public string ApplyBoons(RunState r)
        {
            List<NpcBoon> boons = r.profile.npcBoons;
            if (boons == null || boons.Count == 0) return null;
            foreach (NpcBoon b in boons) ApplyEffects(r, b.effects);
            string names = string.Join(", ", boons.Select(b => b.name).Where(n => !string.IsNullOrEmpty(n)));
            boons.Clear();
            return names;
        }
    }
}
