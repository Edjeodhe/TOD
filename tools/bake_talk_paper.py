"""26차 대화창 종이 굽기.

ChatGPT 웹으로 뽑은 종이 상자·이름표(art_src/portraits/tod_portrait_paper_a.png, tod_portrait_tag_a.png, 초록 배경)
→ 초록을 빼고 종이만 잘라 Assets/UI/Paper/talk_box.png, talk_tag.png

  python tools/bake_talk_paper.py
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
OUT = os.path.join(ROOT, "Assets", "UI", "Paper")

ITEMS = {"talk_box": "tod_portrait_paper_a.png", "talk_tag": "tod_portrait_tag_a.png"}


def bake(name, src):
    path = os.path.join(SRC, src)
    if not os.path.exists(path):
        print("skip", name)
        return
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_green(rgb)
    a = np.where(a > .12, a, 0)
    lab, n = ndimage.label(a > .12)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(a), lab, range(1, n + 1))
        keep = np.isin(lab, 1 + np.nonzero(sizes >= a.size * .002)[0])
        a = np.where(keep, a, 0)
    img = Image.fromarray(np.dstack((col, (a * 255).astype(np.uint8))), "RGBA")
    box = img.getbbox()
    img = img.crop((max(0, box[0] - 2), max(0, box[1] - 2), min(img.width, box[2] + 2), min(img.height, box[3] + 2)))
    out = os.path.join(OUT, name + ".png")
    img.save(out)
    print(out, img.size)


if __name__ == "__main__":
    for k, v in ITEMS.items():
        bake(k, v)
