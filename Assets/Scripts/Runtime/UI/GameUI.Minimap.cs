// 25차 HUD: 주인공 머리 위 대시 칸, 왼쪽 아래 미니맵 (전투와 재봉실). 값은 db.hud (HudData.cs).
// 미니맵은 주인공이 늘 판 가운데이고 맵이 움직인다. 점: NPC 파랑, 주인공 초록, 적 빨강, 보스 = 빨간 두꺼운 고리.
// 점은 minimap-draw 의 generateVisualContent(Painter2D)로 그린다. 바닥 좌표 y 는 화면처럼 Space.Proj 를 곱한다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        // ---------------------------------------------------------------- 머리 위 대시 칸
        readonly List<VisualElement> dashHeadPips = new();
        readonly List<float> dashUsedAt = new();   // 칸마다 대시를 쓴 시각 (use 프레임), 안 썼으면 -1
        readonly List<Sprite> dashShown = new();
        int lastDashCharges = -1;

        void UpdateDashHead(RunState r)
        {
            VisualElement row = hud.Q("dash-head");
            DashHeadConfig c = db.hud?.dashHead;
            if (row == null || c == null || c.fill == null || c.fill.Length == 0 || root.panel == null) return;
            Player player = r.player;
            int max = Mathf.Max(1, db.feel.dashCharges);
            bool show = r.outcome == Outcome.None && Opened == "";
            row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            while (dashHeadPips.Count < max)
            {
                VisualElement pip = Slot(slots, "dash-head-pip");
                row.Add(pip);
                dashHeadPips.Add(pip);
                dashUsedAt.Add(-1);
                dashShown.Add(null);
            }
            // 칸이 줄면 = 대시를 씀: 그 칸(새 개수 자리)부터 줄어든 만큼 use 프레임
            if (lastDashCharges > player.dashCharges)
                for (int i = player.dashCharges; i < lastDashCharges && i < dashUsedAt.Count; i++) dashUsedAt[i] = Now;
            lastDashCharges = player.dashCharges;
            for (int i = 0; i < dashHeadPips.Count; i++)
            {
                VisualElement pip = dashHeadPips[i];
                pip.style.display = i < max ? DisplayStyle.Flex : DisplayStyle.None;
                pip.style.width = c.iconSize; pip.style.height = c.iconSize;
                pip.style.marginRight = i < max - 1 ? c.gap : 0;
                Sprite frame;
                float used = dashUsedAt[i] >= 0 ? (Now - dashUsedAt[i]) / Mathf.Max(.01f, c.useTime) : 1;
                if (used < 1 && c.use != null && c.use.Length > 0) frame = c.use[Mathf.Min(c.use.Length - 1, (int)(used * c.use.Length))];
                else
                {
                    dashUsedAt[i] = -1;
                    float fill = i < player.dashCharges ? 1 : i == player.dashCharges ? Mathf.Clamp01(player.dashCharge) : 0;
                    // 꽉 찬 프레임은 실제로 찼을 때만 (차오르는 중에는 끝 바로 앞까지)
                    int last = c.fill.Length - 1;
                    frame = c.fill[fill >= 1 ? last : Mathf.Min(last - 1, Mathf.FloorToInt(fill * last))];
                }
                if (dashShown[i] != frame) { dashShown[i] = frame; pip.style.backgroundImage = frame != null ? new StyleBackground(frame) : StyleKeyword.Null; }
            }
            // 발밑에서 lift 위 = 줄 아래 끝 가운데 (USS translate -50% -100%)
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Ground(player.x, player.y, c.lift));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            row.style.left = p.x;
            row.style.top = p.y;
        }

        // 25차 3부: 털실 칸 + 실 가닥 수를 주인공 발밑에 (칸 채우기는 UpdateHud 가 그대로)
        void UpdateYarnFoot(RunState r)
        {
            VisualElement row = hud.Q("yarn-foot");
            if (row == null || db.hud == null || root.panel == null) return;
            bool show = r.outcome == Outcome.None && Opened == "";
            row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            Vector3 screen = worldCamera.WorldToScreenPoint(Space.Ground(r.player.x, r.player.y, -db.hud.yarnFootDrop));
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            row.style.left = p.x;
            row.style.top = p.y;
        }

        // ---------------------------------------------------------------- 미니맵
        public struct MinimapView
        {
            public Vector2 hero;              // 바닥 좌표
            public Rect bounds;               // 걸을 수 있는 곳 (바닥 좌표)
            public float range;               // 판 가로가 보여 주는 바닥 너비
        }

        VisualElement minimap, minimapDraw;
        MinimapView mapView;
        readonly List<Vector2> mapNpcs = new(), mapEnemies = new(), mapBosses = new();
        bool mapShown;

        void InitMinimap()
        {
            minimap = root.Q("minimap");
            minimapDraw = root.Q("minimap-draw");
            if (minimapDraw != null) minimapDraw.generateVisualContent += DrawMinimap;
        }

        // GameController 가 매 프레임: 점 목록을 채운 뒤 부른다
        public List<Vector2> MapNpcs => mapNpcs;
        public List<Vector2> MapEnemies => mapEnemies;
        public List<Vector2> MapBosses => mapBosses;

        public void ShowMinimap(MinimapView view)
        {
            if (minimap == null) InitMinimap();
            MinimapConfig c = db.hud?.minimap;
            if (minimap == null || c == null || c.size.x <= 0) { HideMinimap(); return; }
            mapView = view;
            minimap.style.width = c.size.x;
            minimap.style.height = c.size.y;
            if (!mapShown) { Show(minimap, true); mapShown = true; }
            minimapDraw.MarkDirtyRepaint();
        }

        public void HideMinimap()
        {
            if (minimap == null) InitMinimap();
            if (minimap != null && mapShown) { Show(minimap, false); mapShown = false; }
        }

        void DrawMinimap(MeshGenerationContext ctx)
        {
            MinimapConfig c = db.hud?.minimap;
            if (c == null || mapView.range <= 0) return;
            Rect box = minimapDraw.contentRect;
            if (box.width <= 0) return;
            Vector2 mid = box.center;
            float k = box.width / mapView.range;
            Vector2 To(Vector2 g) => mid + new Vector2((g.x - mapView.hero.x) * k, (g.y - mapView.hero.y) * Space.Proj * k);

            Painter2D p = ctx.painter2D;
            // 맵 밖 → 맵 안 → 경계 선
            p.fillColor = c.outside;
            p.BeginPath(); p.MoveTo(box.min); p.LineTo(new Vector2(box.xMax, box.yMin)); p.LineTo(box.max); p.LineTo(new Vector2(box.xMin, box.yMax)); p.ClosePath(); p.Fill();
            Vector2 a = To(mapView.bounds.min), b = To(mapView.bounds.max);
            p.fillColor = c.background;
            p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y)); p.ClosePath(); p.Fill();
            if (c.boundsWidth > 0)
            {
                p.strokeColor = c.bounds; p.lineWidth = c.boundsWidth;
                p.BeginPath(); p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y)); p.ClosePath(); p.Stroke();
            }
            foreach (Vector2 e in mapEnemies) Dot(p, To(e), c.enemyRadius, c.enemy, c, box);
            foreach (Vector2 n in mapNpcs) Dot(p, To(n), c.npcRadius, c.npc, c, box);
            foreach (Vector2 boss in mapBosses) Ring(p, To(boss), c.bossRadius, c.bossRing, c.boss, c, box);
            Dot(p, mid, c.heroRadius, c.hero, c, box);
        }

        static void Dot(Painter2D p, Vector2 at, float radius, Color color, MinimapConfig c, Rect box)
        {
            if (!Near(at, radius, box)) return;
            if (c.dotOutline > 0)
            {
                p.fillColor = c.dotEdge;
                p.BeginPath(); p.Arc(at, radius + c.dotOutline, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
            }
            p.fillColor = color;
            p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
        }

        static void Ring(Painter2D p, Vector2 at, float radius, float width, Color color, MinimapConfig c, Rect box)
        {
            if (!Near(at, radius + width, box)) return;
            if (c.dotOutline > 0)
            {
                p.strokeColor = c.dotEdge; p.lineWidth = width + c.dotOutline * 2;
                p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
            p.strokeColor = color; p.lineWidth = width;
            p.BeginPath(); p.Arc(at, radius, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
        }

        static bool Near(Vector2 at, float r, Rect box) => at.x > box.xMin - r && at.x < box.xMax + r && at.y > box.yMin - r && at.y < box.yMax + r;
    }
}
