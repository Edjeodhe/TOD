// 16차: 층 암시 문구(라운드 시작마다 화면 가운데)와 부가 세트 충전 게이지. Main.uxml 과 따로 두는 UIDocument(ElementHud) 위에 그린다.
// 게이지 한 줄은 ProcGauge.uxml 템플릿을 복제한다. 문구·시간은 db.elements.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public class ElementHud : MonoBehaviour
    {
        public GameController game;
        public UIDocument document;           // ElementHud.uxml
        public UIDocument mainDocument;       // Main.uxml (hud 가 보일 때만 게이지를 띄운다)
        public VisualTreeAsset gaugeTemplate; // ProcGauge.uxml

        VisualElement hint, gauges, hud;
        Label hintText;
        int seenSerial = -1;
        RunState seenRun;
        float hintStart = -99;
        readonly List<VisualElement> rows = new();

        ElementConfig EC => game.db.elements;

        void Start()
        {
            VisualElement root = document.rootVisualElement;
            hint = root.Q("floor-hint");
            hintText = root.Q<Label>("floor-hint-text");
            gauges = root.Q("proc-gauges");
            hud = mainDocument != null ? mainDocument.rootVisualElement.Q("hud") : null;
        }

        void Update()
        {
            if (hint == null || game == null) return;
            RunState r = game.Run;
            bool inCombat = r != null && (hud == null || !hud.ClassListContains("hidden"));
            UpdateHint(r, inCombat);
            UpdateGauges(r, inCombat);
        }

        void UpdateHint(RunState r, bool inCombat)
        {
            if (r == null || EC == null) { hint.EnableInClassList("hidden", true); seenRun = null; return; }
            if (r != seenRun || r.hintSerial != seenSerial)
            {
                seenRun = r; seenSerial = r.hintSerial;
                ElementDef def = EC.Def(r.floorElement);
                if (def != null && !string.IsNullOrEmpty(def.hint))
                {
                    hintText.text = def.hint;
                    hintText.style.color = def.hintColor.a > 0 ? def.hintColor : new StyleColor(StyleKeyword.Null);
                    hintStart = Time.unscaledTime + EC.hintDelay;
                }
                else hintStart = -99;
            }
            float t = Time.unscaledTime - hintStart;
            float total = EC.hintFadeIn + EC.hintHold + EC.hintFadeOut;
            bool show = inCombat && hintStart > 0 && t >= 0 && t < total;
            hint.EnableInClassList("hidden", !show);
            if (!show) return;
            float a = t < EC.hintFadeIn ? t / Mathf.Max(.01f, EC.hintFadeIn)
                : t < EC.hintFadeIn + EC.hintHold ? 1 : 1 - (t - EC.hintFadeIn - EC.hintHold) / Mathf.Max(.01f, EC.hintFadeOut);
            hint.style.opacity = Mathf.Clamp01(a);
            hint.style.translate = new Translate(0, (1 - Mathf.Clamp01(a)) * 10);
        }

        void UpdateGauges(RunState r, bool inCombat)
        {
            List<ActiveSet> subs = inCombat ? game.Combat.SubSets(r).Where(s => s.def.procCharge > 0).ToList() : new List<ActiveSet>();
            while (rows.Count < subs.Count)
            {
                VisualElement row = gaugeTemplate.Instantiate().Q("gauge");
                gauges.Add(row);
                rows.Add(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                VisualElement row = rows[i];
                bool on = i < subs.Count;
                row.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) continue;
                ActiveSet set = subs[i];
                int need = set.def.procCharge, have = Mathf.Min(need, game.Combat.ProcCharge(r, set));
                bool ready = game.Combat.ProcReady(r, set);
                ElementTone tone = game.db.Tone(set.def.element);
                row.Q("swatch").style.backgroundColor = set.def.color;
                row.Q<Label>("name").text = set.def.name;
                VisualElement fill = row.Q("fill");
                fill.style.width = Length.Percent(100f * have / Mathf.Max(1, need));
                float pulse = ready ? .75f + .25f * Mathf.Sin(Time.unscaledTime * 8) : 1;
                Color c = ready ? Color.Lerp(tone.color, Color.white, .3f) : tone.color;
                fill.style.backgroundColor = new Color(c.r * pulse, c.g * pulse, c.b * pulse, 1);
                row.EnableInClassList("ready", ready);
                row.Q<Label>("count").text = ready ? EC.procReady : string.Format(EC.procCount ?? "{0}/{1}", have, need);
            }
        }
    }
}
