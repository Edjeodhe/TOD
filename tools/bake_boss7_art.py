"""23차-boss7: 칠죄종 보스 파츠 굽기  art_src/raw/boss7_<죄종>.png → Assets/sprites/boss7_<죄종>_<파츠>.png

python tools/bake_boss7_art.py [죄종...]

뼈대는 기존 리그(robe = boss, stalker, charger)를 그대로 쓰므로, 새 파츠를 그 뼈대의 원래 파츠 칸에 맞춘다
(bake_22_art.fit_part: 비율이 비슷하면 칸에 꼭 맞추고, 다르면 같은 배율로 줄여 관절 끝을 원래 자리에 두고 캔버스를 넓힌다).
원래 파츠의 피벗은 아래 가운데(0.5, 0)라서, 넓힌 캔버스에서도 원래 칸의 아래 가운데가 피벗이 되게 계산해
art_src/raw/boss7_fit.json 에 적는다 (Unity 가져오기는 MCP execute_code 로 원래 파츠 설정을 베끼고 이 피벗을 넣는다).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from bake_16_art import pieces, tight  # noqa: E402
from bake_22_art import fit_part  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"
FIT = RAW / "boss7_fit.json"

RIG = {"pride": "boss", "envy": "boss", "lust": "boss", "sloth": "boss", "greed": "stalker", "gluttony": "stalker", "wrath": "charger"}
PARTS = {
    "stalker": ["head", "torso", "uarm", "farm", "thigh", "shin"],
    "charger": ["head", "torso", "uarm", "farm", "thigh", "shin", "foot"],
    "boss": ["head", "torso", "skirt", "uarm", "farm"],
}
KEY = {"envy": "magenta"}


def bake(sin, fit):
    src = RAW / f"boss7_{sin}.png"
    if not src.exists():
        print("missing", src.name)
        return
    rig = RIG[sin]
    parts = PARTS[rig]
    for merge in (12, 6, 3):
        found = pieces(src, KEY.get(sin, "green"), len(parts), merge)
        if len(found) == len(parts):
            break
    if len(found) != len(parts):
        print("SKIP (part count)", src.name, len(found), "!=", len(parts))
        return
    notes = []
    for (col, a), part in zip(found, parts):
        base = Image.open(OUT / f"enemy_{rig}_{part}.png").convert("RGBA")
        canvas, pivot, ratio = fit_part(col, a, base, part)
        name = f"boss7_{sin}_{part}"
        canvas.save(OUT / f"{name}.png")
        W, H = base.size
        if pivot is None:
            px, py = .5, 0.0
        else:
            # fit_part 의 pivot = 원래 칸 가운데 (L + W/2, B + H/2) 비율 → 아래 가운데 (L + W/2, B) 로
            cw, ch = canvas.size
            left = pivot[0] * cw - W / 2
            bottom = pivot[1] * ch - H / 2
            px, py = (left + W / 2) / cw, bottom / ch
        fit[name] = {"name": name, "base": f"enemy_{rig}_{part}", "pivot": [round(px, 5), round(py, 5)], "size": list(canvas.size)}
        notes.append(f"{part} {ratio:.2f}{' fit' if pivot else ''}")
    print("wrote", sin, rig, ", ".join(notes))


if __name__ == "__main__":
    fit = {i["name"]: i for i in json.loads(FIT.read_text(encoding="utf-8"))["items"]} if FIT.exists() else {}
    for s in (sys.argv[1:] or list(RIG)):
        bake(s, fit)
    FIT.write_text(json.dumps({"items": sorted(fit.values(), key=lambda i: i["name"])}, indent=1), encoding="utf-8")
