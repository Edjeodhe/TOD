"""15차 아버지의 일기: 잠긴 조각용 '찢긴 빈 쪽지' 굽기.

python tools/bake_diary_paper.py

밝은 양피지 판(Assets/UI/Paper/panel_light.png)을 쪽지 비율로 줄이고, 아래쪽 절반을 들쭉날쭉하게 찢어 낸 뒤
찢긴 가장자리에 잉크 테두리를 두르고 색을 바랜다. → Assets/UI/Paper/note_torn.png
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Assets/UI/Paper/panel_light.png"
OUT = ROOT / "Assets/UI/Paper/note_torn.png"


def main():
    rng = np.random.default_rng(15)
    im = Image.open(SRC).convert("RGBA").resize((360, 300), Image.LANCZOS)
    a = np.asarray(im).astype(np.float32)
    h, w = a.shape[:2]
    # 찢긴 선: 왼쪽 58% 높이에서 오른쪽 46% 높이로 비스듬히, 들쭉날쭉한 톱니 + 잔물결
    xs = np.arange(w)
    base = h * (.58 - .12 * xs / w)
    teeth = np.cumsum(rng.normal(0, 3.2, w))
    teeth -= np.linspace(teeth[0], teeth[-1], w)
    jag = rng.uniform(-6, 6, w // 9 + 2)
    jag = np.interp(xs, np.linspace(0, w, len(jag)), jag)
    line = base + teeth + jag
    yy = np.arange(h)[:, None]
    keep = yy < line[None, :]
    # 찢긴 가장자리 바로 위: 흰 섬유(밝게) + 잉크 테두리
    dist = line[None, :] - yy
    fiber = (dist >= 0) & (dist < 5)
    ink = (dist >= 0) & (dist < 2.2)
    rgb = a[..., :3]
    gray = rgb.mean(-1, keepdims=True)
    rgb = rgb * .55 + gray * .45            # 바랜 색
    rgb = rgb * .78 + np.array([30, 22, 18]) * .22
    rgb[fiber] = rgb[fiber] * .6 + np.array([236, 224, 200]) * .4
    rgb[ink] = np.array([26, 16, 19])
    alpha = a[..., 3] * keep
    out = np.dstack([rgb.clip(0, 255), alpha]).astype(np.uint8)
    img = Image.fromarray(out, "RGBA")
    # 가장자리 알파만 살짝 부드럽게
    soft = img.split()[3].filter(ImageFilter.GaussianBlur(.6))
    img.putalpha(soft)
    img.save(OUT)
    print("saved", OUT)


if __name__ == "__main__":
    main()
