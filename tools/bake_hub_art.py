"""15차 재봉실 허브 그림 굽기: Codex 시트(art_src/raw) → Assets/sprites.

python tools/bake_hub_art.py

- hub_facilities.png (초록 키, 2×2): 재봉 상자, 다락 사다리, 탑 문, 일기 게시판 → hub_sewing_box / hub_attic / hub_tower_door / hub_diary_board
  칸마다 그 안의 그림 전부를 한 장으로 (다락에 매달린 장식처럼 떨어진 조각도 함께). 그림 아래 끝 = 스프라이트 아래 끝 (아래 가운데 피벗).
- hub_room.png (불투명): 재봉실 배경 한 장 → hub_room.png (그대로 복사)
- 20차 hub_facilities2.png (초록 키, 3×1, 방 빛에 맞춰 어둡게, 가장자리 비네트): 다락 사다리, 탑 문, 일기 게시판 → 위 같은 이름을 덮어씀
상인 단추는 16차에 뽑은 merchant_idle.png 를 그대로 씁니다.
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"
FACILITIES = ["sewing_box", "attic", "tower_door", "diary_board"]


def key_green(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    dom = g - np.maximum(r, b)
    spill = g > np.maximum(r, b)
    col = np.stack((r, np.where(spill, np.maximum(r, b), g), b), -1).clip(0, 255).astype(np.uint8)
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    return col, a


def grid(path, cols, rows, merge=24):
    """칸(cols×rows)마다 그 칸에 무게중심이 있는 그림 덩어리 전부를 한 장으로.
    (덩어리는 merge px 만큼 붙여서 찾는다. 아래 칸 그림의 윗부분이 위 칸으로 넘어와도 아래 칸으로 간다.)"""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_green(rgb)
    a = np.where(a > .16, a, 0)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    h, w = a.shape
    owner = np.zeros(n + 1, int) - 1
    for i, sl in enumerate(ndimage.find_objects(lab)):
        ys, xs = np.nonzero(mask[sl] & (lab[sl] == i + 1))
        if len(ys) == 0:
            continue
        cy, cx = sl[0].start + ys.mean(), sl[1].start + xs.mean()
        owner[i + 1] = int(cy * rows // h) * cols + int(cx * cols // w)
    out = []
    for cell in range(cols * rows):
        keep = mask & (owner[lab] == cell)
        ys, xs = np.nonzero(keep)
        y0, y1 = max(0, ys.min() - 4), ys.max() + 2
        x0, x1 = max(0, xs.min() - 4), xs.max() + 5
        sub = np.where(keep[y0:y1, x0:x1], a[y0:y1, x0:x1], 0)
        out.append((col[y0:y1, x0:x1].copy(), sub))
    return out


def vignette(col, a, rim=.38, bottom=.45, side=.22):
    """20차: 그림 안쪽 비네트. 실루엣 가장자리 안쪽(rim), 아래쪽 40%(bottom, 바닥에 닿는 곳), 양옆(side)을
    방의 어두운 자두색(#16131B) 쪽으로 눌러 방에 녹아들게 한다."""
    mask = a > .3
    ys, xs = np.nonzero(mask)
    size = max(ys.max() - ys.min(), xs.max() - xs.min()) + 1
    dist = ndimage.distance_transform_edt(mask)
    r = np.clip(1 - dist / (.06 * size), 0, 1) ** 1.5
    h, w = a.shape
    yy, xx = np.mgrid[0:h, 0:w]
    v = np.clip((yy - (ys.min() + .6 * (ys.max() - ys.min()))) / (.4 * (ys.max() - ys.min()) + 1), 0, 1) ** 1.3
    cx, half = (xs.min() + xs.max()) / 2, (xs.max() - xs.min()) / 2 + 1
    sd = np.clip(np.abs(xx - cx) / half, 0, 1) ** 2
    k = 1 - (1 - (1 - rim * r) * (1 - bottom * v) * (1 - side * sd))
    dark = np.array([22, 19, 27], np.float32)
    return (col.astype(np.float32) * k[..., None] + dark * (1 - k[..., None])).clip(0, 255).astype(np.uint8)


def save(col, a, name, max_side):
    im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
    scale = min(1, max_side / max(im.size))
    if scale < 1:
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
    im.save(OUT / f"{name}.png")
    print("wrote", name, im.size)


if __name__ == "__main__":   # 24차: 다른 스크립트(bake_hub_corners.py)가 함수만 가져다 쓸 수 있게
    if (RAW / "hub_facilities.png").exists():
        for (col, a), name in zip(grid(RAW / "hub_facilities.png", 2, 2), FACILITIES):
            save(col, a, f"hub_{name}", 512)
    if (RAW / "hub_facilities2.png").exists():   # 20차: 같은 이름을 덮어씀 (옛 시트의 재봉 상자는 그대로)
        for (col, a), name in zip(grid(RAW / "hub_facilities2.png", 3, 1), ["attic", "tower_door", "diary_board"]):
            save(vignette(col, a), a, f"hub_{name}", 512)
    if (RAW / "hub_room.png").exists():
        Image.open(RAW / "hub_room.png").convert("RGB").save(OUT / "hub_room.png")
        print("wrote hub_room")
    if (RAW / "hub_room2.png").exists():   # 20차: 오브젝트 자리에 맞춰 다시 그린 재봉실 (데이터 hub.backdrop 이 이 그림)
        Image.open(RAW / "hub_room2.png").convert("RGB").save(OUT / "hub_room2.png")
        print("wrote hub_room2")
