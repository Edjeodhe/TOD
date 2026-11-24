"""모이라 리그 파츠 굽기: Codex 시트(art_src/raw/moira_parts.png, 초록 키) → Assets/sprites/moira_<파츠>.png
카드 그림(art_src/raw/moira_cards.png) → Assets/UI/Moira/card_front.png, card_back.png
카드 넷을 쥔 손(art_src/raw/moira_hand_cards.png) → Assets/sprites/moira_hand_cards.png (말할 때 손 그림)

python tools/bake_moira_parts.py

시트는 읽기 순서(위 줄 → 아래 줄, 줄 안은 왼쪽부터): 머리, 몸, 탁자, 점성구, 윗다리, 아랫다리(손), 카드 부채.
관절 분석: 다리 마디는 세로로 곧게 그려서, 위쪽 둥근 끝의 중심 = 회전 중심(pivot), 아래쪽 둥근 끝의 중심 = 끝 관절(end).
손이 달린 아랫다리는 손목(구슬 팔찌 아래 손바닥)이 카드를 쥐는 끝 관절이다(손가락 시작 높이를 폭 변화로 찾는다).
결과 크기와 관절은 art_src/rig/moira_parts.json 에 쓰고, 리깅 페이지(tools/rig_editor/moira.html)가 읽는다.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"
CARDS = ROOT / "Assets/UI/Moira"
RIG = ROOT / "art_src/rig"
SCALE = .6
NAMES = ["head", "body", "table", "ball", "leg_upper", "leg_lower", "fan"]


def key_alpha(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    dom = g - np.maximum(r, b)
    spill = g > np.maximum(r, b)
    col = np.stack((r, np.where(spill, np.maximum(r, b), g), b), -1).clip(0, 255).astype(np.uint8)
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    return col, a


def pieces(path, merge=18):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        keep = mask[sl] & (lab[sl] == i + 1)
        if keep.sum() < 2000:
            continue
        found.append((sl, keep))
    # 줄: 세로 가운데가 시트 위/아래 절반
    h = a.shape[0]
    found.sort(key=lambda f: (int((f[0][0].start + f[0][0].stop) / 2 > h / 2), f[0][1].start))
    out = []
    for sl, keep in found:
        ys, xs = np.nonzero(keep)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        sub = keep[y0:y1, x0:x1]
        c = col[sl][y0:y1, x0:x1]
        al = np.where(sub, a[sl][y0:y1, x0:x1], 0)
        out.append((c, al))
    return out


def save_rgba(col, a, path, scale):
    im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
    pad = 4
    canvas = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    canvas.paste(im, (pad, pad))
    if scale != 1:
        canvas = canvas.resize((round(canvas.width * scale), round(canvas.height * scale)), Image.LANCZOS)
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path)
    return canvas


def cap_centers(alpha):
    """세로 막대 그림의 위/아래 둥근 끝 중심 (그림 px)."""
    m = alpha > 100
    rows = np.nonzero(m.any(1))[0]
    top, bot = rows.min(), rows.max()
    widths = m.sum(1)

    def center(y0, y1):
        ys, xs = np.nonzero(m[y0:y1])
        return float(xs.mean()), float(y0 + ys.mean())

    w_top = np.median(widths[top: top + max(3, (bot - top) // 12)])
    w_bot = np.median(widths[bot - max(3, (bot - top) // 12): bot + 1])
    cx_t, _ = center(top, top + max(3, int(w_top)))
    cx_b, _ = center(bot - max(3, int(w_bot)), bot + 1)
    return [round(cx_t), round(top + w_top / 2)], [round(cx_b), round(bot - w_bot / 2)], widths, top, bot


def wrist(alpha):
    """손 달린 아랫다리: 아래에서 위로 올라가며 폭이 가장 좁아지는 곳(손목) 바로 아래 = 손바닥 중심."""
    m = alpha > 100
    widths = m.sum(1).astype(float)
    rows = np.nonzero(widths > 0)[0]
    top, bot = rows.min(), rows.max()
    lo = top + int((bot - top) * .55)
    hi = top + int((bot - top) * .8)
    y = lo + int(np.argmin(ndimage.uniform_filter1d(widths[lo:hi], 5)))
    palm = min(bot, y + int((bot - y) * .3))
    xs = np.nonzero(m[palm])[0]
    return [round(float(xs.mean())), int(palm)], int(y)


def main():
    parts = pieces(RAW / "moira_parts.png")
    assert len(parts) == len(NAMES), f"파츠 {len(parts)}개 (기대 {len(NAMES)})"
    info = {}
    for name, (col, a) in zip(NAMES, parts):
        im = save_rgba(col, a, OUT / f"moira_{name}.png", SCALE)
        alpha = np.asarray(im)[..., 3]
        w, h = im.size
        entry = {"file": f"Assets/sprites/moira_{name}.png", "w": w, "h": h}
        if name in ("leg_upper", "leg_lower"):
            top, bottom, _, _, _ = cap_centers(alpha)
            entry["pivot"], entry["end"] = top, bottom
            entry["found"] = [top, bottom]
            if name == "leg_lower":
                palm, waist = wrist(alpha)
                entry["end"] = palm
                entry["found"] = [top, palm, [palm[0], waist]]
        elif name in ("head", "fan", "ball", "table"):
            ys, xs = np.nonzero(alpha > 100)
            entry["pivot"] = [round(float(xs[ys > ys.max() - 6].mean())), int(ys.max())]   # 아래 가운데
        else:   # 몸: 목 = 위 가운데
            ys, xs = np.nonzero(alpha > 100)
            entry["pivot"] = [round(float(xs[ys < ys.min() + 6].mean())), int(ys.min())]
        info[name] = entry
        print(name, w, h, entry.get("pivot"), entry.get("end"))
    RIG.mkdir(parents=True, exist_ok=True)
    (RIG / "moira_parts.json").write_text(json.dumps(info, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")

    # 카드 넷을 쥔 손(말할 때 RB_low 그림): 위를 향해 그렸다(관절 끝이 아래) → rot 180.
    # 배율 = 아랫다리의 관절 끝 → 팔찌 길이에 맞춘다. cardPoint = 부채 가운데 (카드가 날아 나가는 자리)
    if (RAW / "moira_hand_cards.png").exists():
        (col, a), = pieces(RAW / "moira_hand_cards.png", merge=10)
        alpha_raw = (a > .5)
        widths = alpha_raw.sum(1)
        bot = int(np.nonzero(widths)[0].max())
        cap_r = widths[bot - 20] / 2
        band = np.arange(int(len(widths) * .4), int(len(widths) * .6))
        bracelet = int(band[np.argmax(widths[band])])
        low = info["leg_lower"]
        low_alpha = np.asarray(Image.open(OUT / "moira_leg_lower.png"))[..., 3] > 100
        lw = low_alpha.sum(1)
        low_bracelet = int(np.argmax(lw[low["pivot"][1] + 40:]) + low["pivot"][1] + 40)
        scale = (low_bracelet - low["pivot"][1]) / ((bot - cap_r) - bracelet) * 1.08
        im = save_rgba(col, a, OUT / "moira_hand_cards.png", scale)
        alpha = np.asarray(im)[..., 3] > 100
        ys, xs = np.nonzero(alpha)
        bottom = int(ys.max())
        piv_x = round(float(xs[ys > bottom - 8].mean()))
        cap = round(cap_r * scale)
        top_rows = ys < ys.min() + (bottom - ys.min()) * .3
        info["hand_cards"] = {"file": "Assets/sprites/moira_hand_cards.png", "w": im.width, "h": im.height,
                              "pivot": [piv_x, bottom - cap], "rot": 180,
                              "cardPoint": [round(float(xs[top_rows].mean())), round(float(ys[top_rows].mean()))]}
        print("hand_cards", im.size, info["hand_cards"]["pivot"], "scale", round(scale, 3))
        (RIG / "moira_parts.json").write_text(json.dumps(info, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")

    cards = pieces(RAW / "moira_cards.png", merge=10)
    for name, (col, a) in zip(["card_front", "card_back"], cards):
        im = save_rgba(col, a, CARDS / f"{name}.png", 1)
        print(name, im.size)


if __name__ == "__main__":
    main()
