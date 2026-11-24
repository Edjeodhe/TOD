// 23차: 결박 당기기의 라틴어 죄종 글자 (IWorldText.Inscription). Slots.uxml 의 world-inscription 템플릿을 복제해 쓴다.
// 한 글자 묶음 = 후광(죄종 빛, 굵은 외곽 + 번짐) · 아래 두께(어두운 단, 아래로 밀림) · 금속 면(죄종 색 + 어두운 쇠 테두리 + 빛 훑기) · 양옆 가로줄.
// 글꼴은 Cinzel Black SDF (Assets/Fonts/Cinzel, OFL), 모양은 Main.uss .world-inscription.
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        readonly List<VisualElement> inscriptions = new();
        int inscriptionsUsed;
        readonly StringBuilder sheenText = new();

        void BeginInscriptions() => inscriptionsUsed = 0;

        void EndInscriptions()
        {
            for (int i = inscriptionsUsed; i < inscriptions.Count; i++) inscriptions[i].style.display = DisplayStyle.None;
        }

        public void Inscription(string text, Vector2 canvas, in InscriptionPose pose)
        {
            if (root.panel == null || Opened != "") return;
            if (inscriptionsUsed == inscriptions.Count)
            {
                VisualElement created = Slot(slots, "world-inscription");
                worldText.Add(created);
                inscriptions.Add(created);
            }
            VisualElement box = inscriptions[inscriptionsUsed++];
            box.style.display = DisplayStyle.Flex;
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Canvas(canvas));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, UnityEngine.Screen.height - screen.y));
            float perPx = root.layout.height / (worldCamera.orthographicSize * 2 * Space.Ppu);
            box.style.left = p.x;
            box.style.top = p.y;
            box.style.scale = new Scale(new Vector2(pose.scale, pose.scale));
            box.style.opacity = pose.alpha;

            float size = pose.fontPx * perPx, spacing = pose.spacingPx * perPx;
            Label halo = box.Q<Label>("halo"), shade = box.Q<Label>("shade"), face = box.Q<Label>("face"), hi = box.Q<Label>("face-hi");
            foreach (Label l in new[] { halo, shade, face, hi })
            {
                l.text = l == face ? Sheen(text, pose.face, pose) : l == hi ? Sheen(text, pose.hi, pose) : text;
                l.style.fontSize = size;
                l.style.letterSpacing = spacing;
            }
            halo.style.color = pose.glow;
            halo.style.unityTextOutlineColor = pose.glow;
            halo.style.unityTextOutlineWidth = pose.glowPx * perPx;
            halo.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = pose.glowBlur * perPx, color = pose.glow };
            shade.style.color = pose.shade;
            shade.style.unityTextOutlineColor = pose.edge;
            shade.style.unityTextOutlineWidth = pose.edgePx * perPx;
            shade.style.translate = new Translate(0, pose.shadePx * perPx);
            face.style.color = pose.face;
            face.style.unityTextOutlineColor = pose.edge;
            face.style.unityTextOutlineWidth = pose.edgePx * perPx;
            hi.style.color = pose.hi;   // 위쪽 절반만 보이는 밝은 면 (금속 두 톤, .inscription-upper 가 자름)
            hi.style.unityTextOutlineColor = pose.edge;
            hi.style.unityTextOutlineWidth = pose.edgePx * perPx;
            VisualElement aura = box.Q("aura");
            aura.style.unityBackgroundImageTintColor = pose.glow;
            aura.style.opacity = pose.aura;

            float rule = pose.rulePx * perPx;
            foreach (string side in new[] { "rule-l", "rule-r" })
            {
                VisualElement r = box.Q(side);
                r.style.width = rule;
                r.style.height = Mathf.Max(1, pose.ruleThick * perPx);
                r.style.backgroundColor = pose.face;
                r.style.borderTopColor = r.style.borderBottomColor = r.style.borderLeftColor = r.style.borderRightColor = pose.edge;
                r.style.display = rule > .5f ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // 금속 면의 빛 훑기: 글자마다 하이라이트 세기를 <color> 로 칠한다 (글자 자리는 그대로라 후광 · 두께와 어긋나지 않음).
        string Sheen(string text, Color baseColor, in InscriptionPose pose)
        {
            if (pose.sheen < -1 || pose.sheenPower <= 0 || pose.sheenWidth <= 0) return text;
            sheenText.Clear();
            int n = text.Length;
            for (int i = 0; i < n; i++)
            {
                float x = (i + .5f) / n - pose.sheen;
                float h = pose.sheenPower * Mathf.Exp(-x * x / (2 * pose.sheenWidth * pose.sheenWidth));
                Color c = Color.Lerp(baseColor, Color.white, Mathf.Clamp01(h));
                sheenText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(text[i]).Append("</color>");
            }
            return sheenText.ToString();
        }
    }
}
