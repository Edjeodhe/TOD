"""종이 UI 키트 굽기: Codex 시트(art_src/raw/ui_kit.png, 초록 키) → Assets/UI/Paper/*.png

python tools/bake_ui_paper.py

시트의 6조각(읽기 순서: 어두운 종이 판, 밝은 양피지 판, 찢어진 버튼 띠, 빈 게이지 틀, 바느질 카드, 밀랍 인장)을
초록 키로 오려 투명 PNG 로 굽는다. 9-slice 테두리는 Unity 쪽 스프라이트 border 로 잡는다(HANDOFF "종이 UI").
덤으로 게이지 채움용 종이 결(grain.png, 밝은 회색 종이 결 → USS 에서 색으로 물들임)을 양피지 가운데서 떠 온다.
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "art_src/raw/ui_kit.png"
OUT = ROOT / "Assets/UI/Paper"


def key_green(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    dom = g - np.maximum(r, b)
    spill = g > np.maximum(r, b)
    g2 = np.where(spill, np.maximum(r, b), g)
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    return np.stack((r, g2, b), -1).clip(0, 255).astype(np.uint8), a


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rgb = np.asarray(Image.open(SRC).convert("RGB"))
    col, a = key_green(rgb)
    mask = a > .16
    lab, _ = ndimage.label(ndimage.binary_dilation(mask, iterations=4))
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        area = int((mask[sl] & (lab[sl] == i + 1)).sum())
        found.append((area, i + 1, sl))
    found = sorted(found, reverse=True)[:6]

    def center(f):
        sl = f[2]
        return (sl[1].start + sl[1].stop) / 2, (sl[0].start + sl[0].stop) / 2

    # 모양으로 이름 붙이기: 가로세로 비율과 위치
    names = {}
    for f in found:
        sl = f[2]
        w, h = sl[1].stop - sl[1].start, sl[0].stop - sl[0].start
        cx, cy = center(f)
        if w / h > 6: names["meter_frame"] = f
        elif w / h > 2.5: names["button"] = f
        elif h > w * 1.2: names["card"] = f
        elif w < 300 and h < 300: names["seal"] = f
        elif cx < rgb.shape[1] / 2: names["panel_dark"] = f
        else: names["panel_light"] = f
    assert len(names) == 6, names.keys()

    for name, (_, li, sl) in names.items():
        y0, y1 = max(0, sl[0].start - 4), sl[0].stop + 4
        x0, x1 = max(0, sl[1].start - 4), sl[1].stop + 4
        sub_a = a[y0:y1, x0:x1].copy()
        sub_a[lab[y0:y1, x0:x1] != li] = 0
        rgba = np.dstack([col[y0:y1, x0:x1], (sub_a * 255).astype(np.uint8)])
        Image.fromarray(rgba, "RGBA").save(OUT / f"{name}.png")
        print(name, rgba.shape[1], "x", rgba.shape[0])

    # 종이 결: 양피지 가운데 조각 → 평균을 밝게 맞춘 회색(색은 USS tint 로)
    _, _, sl = names["panel_light"]
    cy, cx = (sl[0].start + sl[0].stop) // 2, (sl[1].start + sl[1].stop) // 2
    patch = rgb[cy - 64:cy + 64, cx - 128:cx + 128].astype(np.float32)
    lum = patch @ np.array([.3, .59, .11], np.float32)
    lum = 225 + (lum - lum.mean()) * 1.6
    g = lum.clip(0, 255).astype(np.uint8)
    Image.fromarray(np.dstack([g, g, g, np.full_like(g, 255)]), "RGBA").save(OUT / "grain.png")
    print("grain 256 x 128")


if __name__ == "__main__":
    main()
