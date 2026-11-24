// 23차 포션 칸: 넣기(겹치기), 마시기, 정예 드롭. 값은 db.potions (PotionData.cs).
using System;
using UnityEngine;

namespace TOD
{
    // 포션 칸 하나 (id 가 비면 빈 칸)
    [Serializable] public class PotionSlot { public string id; public int count; public bool Empty => string.IsNullOrEmpty(id) || count <= 0; }

    public static class Potions
    {
        public static PotionDef Def(GameDatabase db, string id) =>
            string.IsNullOrEmpty(id) || db.potions?.potions == null ? null : Array.Find(db.potions.potions, p => p.id == id);

        public static int SlotCount(GameDatabase db) => Mathf.Max(1, db.potions?.slots ?? 5);
        static int Stack(GameDatabase db) => Mathf.Max(1, db.potions?.stack ?? 1);

        public static PotionSlot[] NewSlots(GameDatabase db)
        {
            var slots = new PotionSlot[SlotCount(db)];
            for (int i = 0; i < slots.Length; i++) slots[i] = new PotionSlot();
            if (db.potions?.start != null) foreach (string id in db.potions.start) Add(db, slots, id);
            return slots;
        }

        // 넣을 칸: 같은 포션이 덜 찬 칸 먼저, 없으면 첫 빈 칸. 못 넣으면 -1
        public static int SlotFor(GameDatabase db, PotionSlot[] slots, string id)
        {
            if (slots == null || Def(db, id) == null) return -1;
            for (int i = 0; i < slots.Length; i++) if (!slots[i].Empty && slots[i].id == id && slots[i].count < Stack(db)) return i;
            for (int i = 0; i < slots.Length; i++) if (slots[i].Empty) return i;
            return -1;
        }

        public static bool CanAdd(GameDatabase db, PotionSlot[] slots, string id) => SlotFor(db, slots, id) >= 0;

        // 하나 넣는다. 넣은 칸 번호, 못 넣으면 -1
        public static int Add(GameDatabase db, PotionSlot[] slots, string id)
        {
            int i = SlotFor(db, slots, id);
            if (i < 0) return -1;
            if (slots[i].Empty) { slots[i].id = id; slots[i].count = 0; }
            slots[i].count++;
            return i;
        }

        // 정예 처치 드롭: 확률이 맞고 칸에 들어갈 자리가 있을 때만 (보스는 없음)
        public static string EliteDrop(GameDatabase db, RunState r, Enemy e)
        {
            PotionConfig c = db.potions;
            if (c == null || !e.elite || e.IsBoss || c.eliteDrops == null || c.eliteDrops.Length == 0) return null;
            if (r.random() >= c.eliteChance) return null;
            string id = c.eliteDrops[Mathf.Min(c.eliteDrops.Length - 1, Mathf.FloorToInt(r.random() * c.eliteDrops.Length))];
            return CanAdd(db, r.potions, id) ? id : null;
        }
    }

    public partial class Combat
    {
        // 숫자키 1~5: 그 칸의 포션을 마신다. 소용없으면(체력 가득 등) 마시지 않고 알림만. 마셨으면 true
        public bool UsePotion(RunState r, int slot)
        {
            if (r?.potions == null || slot < 0 || slot >= r.potions.Length || r.outcome != Outcome.None || r.hp <= 0) return false;
            PotionSlot s = r.potions[slot];
            PotionDef def = s.Empty ? null : Potions.Def(db, s.id);
            if (def == null || r.time < r.potionReady) return false;
            float amount = def.amount * (r.desires.Contains("hunger") ? db.desire.hungerPotion : 1);   // 욕망 '허기': 포션 효과 배율
            string number;
            switch (def.effect)
            {
                case PotionEffect.Heal:
                    {
                        float healed = Mathf.Min(r.maxHp, r.hp + amount) - r.hp;
                        if (healed <= 0) { Event(r, "empty", db.potions.noNeed); return false; }
                        r.hp += healed;
                        r.player.heal = db.rewards.healTime;   // 몸에 피가 차오르는 연출 (하트와 같음)
                        number = "+" + Round(healed).ToString("0");
                        break;
                    }
                case PotionEffect.Purify:
                    {
                        float before = r.desire;
                        if (before <= 0) { Event(r, "empty", db.potions.noNeed); return false; }
                        AdjustDesire(r, -amount);
                        number = "-" + Round(before - r.desire).ToString("0");
                        break;
                    }
                default:
                    {
                        int max = YarnMax(r);
                        if (r.yarn >= max) { Event(r, "empty", db.potions.noNeed); return false; }
                        float before = r.yarn;
                        r.yarn = amount > 0 ? Mathf.Min(max, r.yarn + amount) : max;
                        number = "+" + Round(r.yarn - before).ToString("0");
                        break;
                    }
            }
            if (--s.count <= 0) { s.id = null; s.count = 0; }
            r.potionReady = r.time + Mathf.Max(0, db.potions.cooldown);
            Fx fx = Spawn(r, FxKind.Number, r.player.x, r.player.y, X.damageNumber);
            fx.text = number; fx.color = def.color; fx.lift = 44;
            Event(r, string.IsNullOrEmpty(def.sound) ? "heal" : def.sound);
            return true;
        }
    }
}
