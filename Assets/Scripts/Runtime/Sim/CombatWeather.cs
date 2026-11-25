// 24차 층 날씨: 상성 대신, 그 층 죄종이 주인공에게 거는 디버프 (ElementDef.weather).
// 세기 = min(cap, baseValue + perFloor × (층 − 1)) → 7층에서 1층의 2배가 되도록 perFloor = baseValue / 6 (데이터).
using UnityEngine;

namespace TOD
{
    public partial class Combat
    {
        // 24차 예언: 이번 등반 동안 가중치가 오르는 세트의 죄종 (없으면 Plain). Gear.PickWeighted 에 넘긴다.
        public static Element FateSet(RunState r) => r != null && r.fate > 0 ? (Element)(r.fate - 1) : Element.Plain;

        // 지금 층의 날씨 (없거나 None 이면 null)
        public FloorWeather Weather(RunState r)
        {
            FloorWeather w = EC?.Def(r.floorElement)?.weather;
            return w != null && w.kind != WeatherKind.None ? w : null;
        }

        public static float WeatherValue(FloorWeather w, int floor)
        {
            if (w == null || w.kind == WeatherKind.None) return 0;
            float v = w.baseValue + w.perFloor * Mathf.Max(0, floor - 1);
            return w.cap > 0 ? Mathf.Min(w.cap, v) : v;
        }

        // 지금 층 날씨가 kind 이면 그 세기, 아니면 0
        public float WeatherValue(RunState r, WeatherKind kind)
        {
            FloorWeather w = Weather(r);
            return w != null && w.kind == kind ? WeatherValue(w, r.floor) : 0;
        }

        // 날씨 설명 한 줄 (예: "얻는 경험치·골드 -30%"). 없으면 null.
        public string WeatherText(RunState r)
        {
            FloorWeather w = Weather(r);
            if (w == null || string.IsNullOrEmpty(w.text)) return null;
            return string.Format(w.text, Mathf.RoundToInt(WeatherValue(w, r.floor) * (w.kind == WeatherKind.Drain ? 1000 : 100)) / (w.kind == WeatherKind.Drain ? 10f : 1f));
        }

        float Less(RunState r, WeatherKind kind) => 1 - Mathf.Clamp01(WeatherValue(r, kind));

        float WeatherCritScale(RunState r) => Less(r, WeatherKind.CritDown);        // 시기
        float WeatherHealScale(RunState r) => Less(r, WeatherKind.HealDown);        // 교만
        float WeatherMoveScale(RunState r) => Less(r, WeatherKind.MoveDown);        // 나태
        float WeatherTakenScale(RunState r) => 1 + Mathf.Max(0, WeatherValue(r, WeatherKind.DamageTakenUp));   // 분노
        public float WeatherVision(RunState r) => Mathf.Clamp01(WeatherValue(r, WeatherKind.Vision));         // 색욕 (뷰가 그림)

        // 탐욕: 경험치·골드를 깎되 소수는 모아 두었다가 넘긴다 (1짜리 드롭이 영영 0 이 되지 않게)
        int WeatherGain(RunState r, int amount, ref float carry)
        {
            float v = WeatherValue(r, WeatherKind.GainDown);
            if (v <= 0 || amount <= 0) return amount;
            carry += amount * (1 - Mathf.Clamp01(v));
            int whole = Mathf.FloorToInt(carry);
            carry -= whole;
            return whole;
        }

        // 회복 한 번 (교만 날씨가 깎는다). 실제로 오른 양을 돌려준다.
        float HealPlayer(RunState r, float amount)
        {
            if (amount <= 0) return 0;
            float healed = Mathf.Max(0, Mathf.Min(r.maxHp, r.hp + Round(amount * WeatherHealScale(r))) - r.hp);
            r.hp += healed;
            return healed;
        }

        // 식탐 허기: 전투 중(쉬어가기 제외) 초당 최대 체력 비율만큼 줄고, 1 아래로는 안 내려간다. 모아서 숫자 하나로 띄운다.
        void UpdateWeather(RunState r, float dt)
        {
            float drain = WeatherValue(r, WeatherKind.Drain);
            if (drain <= 0 || r.devGod || r.outcome != Outcome.None) return;
            if (r.roundKind == RoundKind.Rest || (r.phase != Phase.Combat)) return;
            r.hungerPending += r.maxHp * drain * dt;
            r.hungerTick += dt;
            if (r.hungerTick < 1) return;
            r.hungerTick = 0;
            float amount = Mathf.Min(Mathf.Floor(r.hungerPending), r.hp - 1);
            r.hungerPending -= Mathf.Floor(r.hungerPending);
            if (amount < 1) return;
            r.hp -= amount;
            Fx number = Spawn(r, FxKind.Number, r.player.x, r.player.y, X.hurtNumber);
            number.text = $"-{amount}"; number.color = db.Tone(Element.Gluttony).color; number.useElement = true; number.element = Element.Gluttony;
        }

        // 식탐 허기: 처치하면 조금 채운다 (extra = 최대 체력 비율)
        void WeatherOnKill(RunState r)
        {
            FloorWeather w = Weather(r);
            if (w == null || w.kind != WeatherKind.Drain || w.extra <= 0) return;
            r.hp = Mathf.Min(r.maxHp, r.hp + Mathf.Max(1, Round(r.maxHp * w.extra)));
        }
    }
}
