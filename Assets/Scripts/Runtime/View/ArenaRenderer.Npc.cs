// 23차 쉬어가기 NPC 그리기: 씬 Arena/Npcs 의 CutoutRig 4개(NpcKind 순서) 중 지금 NPC 하나만 켜서 r.npc 자리에 발밑을 맞춘다.
// 발밑 그림자, 이름 꼬리표, 말풍선 자리(NpcHead)도 여기서 정한다. 말하기 자세(Talking)는 GameController.Npc.cs 가 켠다.
using UnityEngine;

namespace TOD
{
    public partial class ArenaRenderer
    {
        [Header("23차 쉬어가기 NPC (NpcKind 순서)")]
        public CutoutRig[] npcRigs;
        public int npcSortingOrder = 30000;   // Ground 층 위쪽 (바닥 장식, 계단, 탑보다 앞, 주인공(Actors 층)보다 뒤)

        public Vector2? NpcHead { get; private set; }
        public CutoutRig NpcRig { get; private set; }
        float npcClock = -1;

        void DrawNpc(RunState r, Interactable nearest, float clock)
        {
            NpcHead = null;
            NpcRig = null;
            float dt = npcClock < 0 ? 0 : Mathf.Clamp(clock - npcClock, 0, .1f);
            npcClock = clock;
            NpcConfig cfg = db.npcs;
            NpcDef def = r.npc != null ? cfg?.Get(r.npcKind) : null;
            for (int i = 0; npcRigs != null && i < npcRigs.Length; i++)
                if (npcRigs[i] != null) npcRigs[i].gameObject.SetActive(def != null && i == (int)r.npcKind);
            if (def == null || npcRigs == null || (int)r.npcKind >= npcRigs.Length || npcRigs[(int)r.npcKind] == null) return;
            CutoutRig rig = npcRigs[(int)r.npcKind];
            NpcRig = rig;
            bool near = nearest == Interactable.Npc;
            float s = rig.height > 0 && def.height > 0 ? def.height / rig.height : 1;
            rig.transform.position = Space.Ground(r.npc.x, r.npc.y);
            rig.transform.localScale = new Vector3(s, s, 1);
            if (rig.group != null) { rig.group.sortingLayerName = "Ground"; rig.group.sortingOrder = npcSortingOrder; }
            rig.Animate(dt, near ? (db.hub != null && db.hub.highlight > 0 ? db.hub.highlight : 1.12f) : 1);

            Vector2 at = C(r.npc);
            if (cfg.shadowSize.x > 0) painter.Ellipse(Layer.Ground, at, cfg.shadowSize.x / 2, cfg.shadowSize.y / 2, cfg.shadowColor);
            if (near) painter.Ellipse(Layer.Ground, at, 46, 46 * Space.Proj, Painter.Alpha(Pal.promptActive, .7f), 1.5f);
            float h = def.height;
            string label = string.IsNullOrEmpty(def.prompt) ? def.name : $"{def.prompt}  {def.name}";   // 상인 꼬리표와 같은 꼴 ("E  상인 단추")
            PropLabel(label, at - new Vector2(0, h + 18), near);
            NpcHead = at - new Vector2(0, h + def.bubbleLift);
        }
    }
}
