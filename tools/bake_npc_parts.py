"""23차 쉬어가기 NPC 파츠 굽기: Codex 시트(art_src/raw/npc_<id>_parts.png, 초록 키) → Assets/sprites/npc_<id>_<파츠>.png

python tools/bake_npc_parts.py [id ...]      (없으면 4종 모두: nun rat ghost doll)

시트는 읽기 순서(위 줄 → 아래 줄, 줄 안은 왼쪽부터)로 아래 NPCS 의 이름을 붙인다 (모이라 굽기 tools/bake_moira_parts.py 와 같은 키, 조각 찾기).
관절 분석 (그림 px, 굽힌 뒤 배율 적용):
  limb  : 세로로 곧게 그린 팔 마디. 위쪽 둥근 끝의 중심 = 회전 중심(pivot), 아래쪽 둥근 끝의 중심 = 끝 관절(end).
  hand  : 손이 달린 아래팔. pivot = 위쪽 둥근 끝, end = 손바닥 가운데(손목 아래, 폭이 가장 좁은 곳 아래).
  hang  : 매달린 것(묵주, 꼬리, 너울). pivot = 위쪽 끝 가운데(고리, 꼬리 뿌리), end = 아래 끝.
  bottom: 아래 가운데 (머리 = 목 탭 아래 끝, 불꽃 = 밑동, 서 있는 몸 = 발밑).
  top   : 위 가운데 (매달린 유령 몸 = 목).
  center: 가운데 (주사위, 보따리).
결과 크기와 관절은 art_src/rig/npc_<id>_parts.json 에 쓰고, 첫 리그 값(tools/npc_rig_seed.py)과 리깅 페이지(tools/rig_editor/rig.html)가 읽는다.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from bake_moira_parts import pieces, save_rgba, cap_centers, wrist  # noqa: E402

RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"
RIG = ROOT / "art_src/rig"

# id: (배율, 조각 합치기 거리, [(이름, 관절 규칙)])
NPCS = {
    "nun": (.5, 18, [("head", "bottom"), ("veil", "hang"), ("body", "bottom"),
                     ("arm_upper", "limb"), ("arm_lower", "hand"), ("rosary", "hang")]),
    "rat": (.5, 18, [("head", "bottom"), ("body", "bottom"), ("bundle", "center"),
                     ("tail", "hang"), ("arm_upper", "limb"), ("arm_lower", "hand"), ("arm_piece", "hand")]),
    "ghost": (.5, 18, [("head", "bottom"), ("flame", "bottom"), ("body", "top"),
                       ("arm_upper", "limb"), ("arm_lower", "hand"), ("arm_wisp", "hand")]),
    "doll": (.5, 18, [("head", "bottom"), ("body", "bottom"),
                      ("arm_upper", "limb"), ("arm_lower", "hand"), ("die", "center")]),
}


def joints(alpha, rule):
    m = alpha > 100
    ys, xs = np.nonzero(m)
    y0, y1 = int(ys.min()), int(ys.max())
    if rule == "limb":
        top, bottom, *_ = cap_centers(alpha)
        return {"pivot": top, "end": bottom, "found": [top, bottom]}
    if rule == "hand":
        top, *_ = cap_centers(alpha)
        palm, waist = wrist(alpha)
        return {"pivot": top, "end": palm, "found": [top, palm, [palm[0], waist]]}
    if rule == "hang":
        top = [round(float(xs[ys < y0 + 10].mean())), y0 + 6]
        bottom = [round(float(xs[ys > y1 - 10].mean())), y1 - 4]
        return {"pivot": top, "end": bottom, "found": [top, bottom]}
    if rule == "bottom":
        return {"pivot": [round(float(xs[ys > y1 - 6].mean())), y1]}
    if rule == "top":
        return {"pivot": [round(float(xs[ys < y0 + 6].mean())), y0]}
    return {"pivot": [round(float(xs.mean())), round(float(ys.mean()))]}


def bake(npc):
    scale, merge, names = NPCS[npc]
    parts = pieces(RAW / f"npc_{npc}_parts.png", merge=merge)
    assert len(parts) == len(names), f"{npc}: 파츠 {len(parts)}개 (기대 {len(names)})"
    info = {}
    for (name, rule), (col, a) in zip(names, parts):
        im = save_rgba(col, a, OUT / f"npc_{npc}_{name}.png", scale)
        alpha = np.asarray(im)[..., 3]
        entry = {"file": f"Assets/sprites/npc_{npc}_{name}.png", "w": im.width, "h": im.height}
        entry.update(joints(alpha, rule))
        info[name] = entry
        print(npc, name, im.size, entry.get("pivot"), entry.get("end"))
    RIG.mkdir(parents=True, exist_ok=True)
    (RIG / f"npc_{npc}_parts.json").write_text(json.dumps(info, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


if __name__ == "__main__":
    for npc in sys.argv[1:] or list(NPCS):
        bake(npc)
