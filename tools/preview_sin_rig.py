"""22차 죄종 몬스터 미리보기: 원래 파츠와 죄종 파츠를 같은 뼈대(art_src/rig/enemy_rigs.json)에 끼워 나란히 그린다.

python tools/preview_sin_rig.py [죄종 ...]   → Temp/Shots/sin22_rig_<죄종>.png (쉬는 자세 + 걷기 2칸, 몬스터 4종)
                                               Temp/Shots/sin22_rig_all.png (7죄종 쉬는 자세 한 장)
bake_22_art.py 가 캔버스를 넓힌 파츠(art_src/raw/sin_parts_fit.json)는 넓힌 만큼 관절 px 를 옮겨 그린다.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import preview_enemy_rig as R  # noqa: E402

ROOT = R.ROOT
SINS = ["wrath", "sloth", "envy", "greed", "gluttony", "lust", "pride"]
FIT_PATH = ROOT / "art_src/raw/sin_parts_fit.json"
FIT = {i["name"]: i["pivot"] for i in json.loads(FIT_PATH.read_text(encoding="utf-8"))["items"]} if FIT_PATH.exists() else {}

_open = Image.open
SIN = {"id": None}


def open_variant(path, *a, **k):
    p = Path(path)
    if SIN["id"] and p.parent.name == "sprites" and p.stem.startswith("enemy_"):
        v = p.with_name(f"{p.stem}_{SIN['id']}.png")
        if v.exists():
            return _open(v, *a, **k)
    return _open(path, *a, **k)


def shifted_spec(spec, sin):
    """넓힌 캔버스 파츠는 관절 px(pivot)를 넓힌 만큼 옮긴다."""
    spec = json.loads(json.dumps(spec))
    if not sin:
        return spec
    for b in spec["bones"]:
        if not b.get("part"):
            continue
        name = f"enemy_{spec['name']}_{b['part']}"
        fit = FIT.get(f"{name}_{sin}")
        if not fit:
            continue
        W, H = _open(ROOT / f"Assets/sprites/{name}.png").size
        W2, H2 = _open(ROOT / f"Assets/sprites/{name}_{sin}.png").size
        L = fit[0] * W2 - W / 2
        B = fit[1] * H2 - H / 2
        T = H2 - H - B
        b["pivot"] = [b["pivot"][0] + L, b["pivot"][1] + T]
    return spec


def cell(name, sin, motion="rest", k=0, frames=1):
    spec = dict(R.SPEC[name])
    spec["name"] = name
    spec = shifted_spec(spec, sin)
    SIN["id"] = sin
    scale = 150 / max(300, spec.get("height", 300)) * 1.6
    w, h = 300, 330
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pose = {"root": [0, 0], "ang": {}}
    m = spec.get(motion, {})
    if motion in ("idle", "walk"):
        R.loop_pose(m, m["length"] * k / frames, pose)
    R.render(spec, pose, c, (w / 2, h - 30), scale)
    SIN["id"] = None
    return c


def main():
    R.Image.open = open_variant
    sins = sys.argv[1:] or SINS
    names = ["stalker", "charger", "shooter", "boss"]
    for sin in sins:
        sheet = Image.new("RGBA", (300 * 4, 330 * 3), (58, 54, 66, 255))
        d = ImageDraw.Draw(sheet)
        for i, n in enumerate(names):
            sheet.alpha_composite(cell(n, None), (i * 300, 0))
            sheet.alpha_composite(cell(n, sin), (i * 300, 330))
            sheet.alpha_composite(cell(n, sin, "walk", 2, 8), (i * 300, 660))
        for r, label in enumerate(["original", f"{sin} rest", f"{sin} walk"]):
            d.text((6, r * 330 + 6), label, fill=(255, 255, 255, 255))
        out = ROOT / f"Temp/Shots/sin22_rig_{sin}.png"
        sheet.save(out)
        print("wrote", out.name)
    allsheet = Image.new("RGBA", (300 * 4, 330 * len(SINS)), (58, 54, 66, 255))
    for r, sin in enumerate(SINS):
        for i, n in enumerate(names):
            allsheet.alpha_composite(cell(n, sin), (i * 300, r * 330))
    allsheet.save(ROOT / "Temp/Shots/sin22_rig_all.png")


if __name__ == "__main__":
    main()
