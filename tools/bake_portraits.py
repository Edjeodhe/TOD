"""26차 대화창 얼굴 굽기.

ChatGPT 웹(Claude in Chrome)으로 뽑은 얼굴 그림(art_src/portraits/tod_portrait_<id>_<x>.png, 초록 #00FF00 배경)
→ 초록을 빼서 투명하게, 자잘한 점 지우기, 640x640 칸에 아래 끝을 맞춰 넣기 → Assets/sprites/portraits/portrait_<id>.png

  python tools/bake_portraits.py            # PICK 에 적힌 것 모두
  python tools/bake_portraits.py nun ghost  # 몇 개만

주인공은 오른쪽을 보고(왼쪽 자리), NPC 는 왼쪽을 본다(오른쪽 자리). 반대로 나온 그림은 PICK 의 flip 으로 뒤집는다.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(__file__))
from bake_hub_art import key_green  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "art_src", "portraits")
OUT = os.path.join(ROOT, "Assets", "sprites", "portraits")
SIZE = 640

# id: (고른 파일 접미사, 좌우 뒤집기)
PICK = {
    "hero": ("a", False),
    "nun": ("a", False),
    "ghost": ("c", False),
    "rat": ("a", False),
    "doll": ("a", False),
    "merchant": ("a", False),
    "moira": ("a", False),
}


def bake(pid, suffix, flip):
    src = os.path.join(SRC, f"tod_portrait_{pid}_{suffix}.png")
    rgb = np.asarray(Image.open(src).convert("RGB"))
    col, a = key_green(rgb)
    a = np.where(a > .12, a, 0)
    # 큰 덩어리만 (그림 넓이의 1% 이상) — 초록 배경에 남은 점, 반짝이 지우기
    lab, n = ndimage.label(a > .12)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(a), lab, range(1, n + 1))
        keep = np.isin(lab, 1 + np.nonzero(sizes >= a.size * .01)[0])
        a = np.where(keep, a, 0)
    img = Image.fromarray(np.dstack((col, (a * 255).astype(np.uint8))), "RGBA")
    if flip:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    box = img.getbbox()
    img = img.crop((box[0], box[1], box[2], img.height))   # 아래 끝(가슴에서 잘린 곳)은 그대로 둔다
    k = min(SIZE / img.width, SIZE / img.height)
    img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.paste(img, ((SIZE - img.width) // 2, SIZE - img.height), img)
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"portrait_{pid}.png")
    out.save(path)
    print(path, img.size)


if __name__ == "__main__":
    ids = sys.argv[1:] or list(PICK)
    for pid in ids:
        bake(pid, *PICK[pid])
