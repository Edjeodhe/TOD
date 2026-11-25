"""23차 주인공 그림 다시 그리기 (NPC 화풍): 파츠 그림만 바꾸고 캔버스 크기·관절 자리·GUID 는 그대로.

python tools/hero_restyle.py layout   → art_src/raw/hero_restyle_layout.png (옛 파츠를 초록 바탕에 배율 S로 늘어놓은 것, Codex 형태 참고)
                                         + art_src/rig/hero_restyle_layout.json (자리)
python tools/hero_restyle.py bake [sheet]
                                      → Codex 시트(기본 art_src/raw/hero_restyle_parts.png, 초록 키)에서 자리마다 조각을 찾아
                                         옛 파츠의 그림 영역(알파 테두리 상자)에 맞춰 늘려 Assets/sprites/hero_v3_<파츠>.png 에 덮어씀.
python tools/hero_restyle.py backup   → 옛 파츠를 art_src/sprites/hero_v3_pre23/ 에 복사 (이미 있으면 안 덮음)
python tools/hero_restyle.py check    → 옛/새 파츠 나란히 비교, 할핀(황동 점) 자리 차이 출력

왜 배율 S(.6)로 그리나: 주인공 파츠는 화면에 그림 px 당 약 .15 px 로 그려지는데 NPC 는 약 .53 px 이다 (1080p).
같은 굵기 선이 화면에서 3.5배 가늘어져 회색으로 번져 보였다. 시트에서 작게(캔버스의 .6) 그려 늘리면 NPC 시트와 비슷한
화면 밀도가 되어 선이 같은 굵기로 남는다.
"""
import json
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from bake_moira_parts import key_alpha  # noqa: E402

SPR = ROOT / "Assets/sprites"
OLD = ROOT / "art_src/sprites/hero_v3_pre23"
RAW = ROOT / "art_src/raw"
LAYOUT_PNG = RAW / "hero_restyle_layout.png"
LAYOUT_JSON = ROOT / "art_src/rig/hero_restyle_layout.json"
S = .6
SHEET = (1536, 1024)
GAP = 120

# 리그가 실제로 쓰는 파츠 (Arena/Hero/Rig 뼈의 Art 그림). coat_shoulders, coat_front_* 는 안 씀.
PARTS = ["head", "torso", "coat_body", "coat_back", "coat_hem_near", "coat_hem_far", "skirt",
         "uarm", "farm", "hand", "thigh", "shin", "foot", "tail_base", "tail_mid", "tail_tip", "needle"]


def old_path(p):
    return (OLD if OLD.exists() else SPR) / f"hero_v3_{p}.png"


def bbox(alpha, thr=40):
    ys, xs = np.nonzero(alpha > thr)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def backup():
    OLD.mkdir(parents=True, exist_ok=True)
    for p in PARTS:
        dst = OLD / f"hero_v3_{p}.png"
        if not dst.exists():
            shutil.copy2(SPR / f"hero_v3_{p}.png", dst)
    print("backup", OLD)


def layout():
    # 조각을 S 배율로 줄여 선반 쌓기: 머리는 왼쪽 위, 나머지는 오른쪽과 아래에.
    items = []
    for p in PARTS:
        im = Image.open(old_path(p)).convert("RGBA")
        a = np.asarray(im)[..., 3]
        x0, y0, x1, y1 = bbox(a)
        crop = im.crop((x0, y0, x1, y1))
        crop = crop.resize((max(1, round(crop.width * S)), max(1, round(crop.height * S))), Image.LANCZOS)
        items.append((p, crop, (int(x0), int(y0), int(x1), int(y1))))
    sheet = Image.new("RGBA", SHEET, (0, 255, 0, 255))
    place = {}
    # 머리
    p, crop, box = items[0]
    hx, hy = GAP, GAP
    sheet.alpha_composite(crop, (hx, hy))
    place[p] = {"x": hx, "y": hy, "w": crop.width, "h": crop.height, "old_box": box}
    # 나머지: 머리 오른쪽 영역 → 머리 아래 영역, 각 줄 높이는 그 줄 가장 큰 것
    regions = [(hx + crop.width + GAP, GAP, SHEET[0] - GAP, hy + crop.height + GAP),
               (GAP, hy + crop.height + GAP, SHEET[0] - GAP, SHEET[1] - GAP)]
    rest = items[1:]
    ri = 0
    x, y, rowh = regions[0][0], regions[0][1], 0
    for p, crop, box in rest:
        rx0, ry0, rx1, ry1 = regions[ri]
        if x + crop.width > rx1:
            x, y, rowh = rx0, y + rowh + GAP, 0
        if y + crop.height > ry1 and ri == 0:
            ri = 1
            rx0, ry0, rx1, ry1 = regions[ri]
            x, y, rowh = rx0, ry0, 0
        if x + crop.width > rx1:
            x, y, rowh = rx0, y + rowh + GAP, 0
        assert y + crop.height <= SHEET[1] - GAP // 2, f"{p} 자리 없음"
        sheet.alpha_composite(crop, (x, y))
        place[p] = {"x": x, "y": y, "w": crop.width, "h": crop.height, "old_box": box}
        x += crop.width + GAP
        rowh = max(rowh, crop.height)
    sheet.convert("RGB").save(LAYOUT_PNG)
    LAYOUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    LAYOUT_JSON.write_text(json.dumps({"scale": S, "sheet": SHEET, "parts": place}, indent=1) + "\n", encoding="utf-8")
    print("layout", LAYOUT_PNG)


def components(path):
    im = Image.open(path).convert("RGB")
    if im.size != SHEET:
        im = im.resize(SHEET, Image.LANCZOS)
    rgb = np.asarray(im)
    col, a = key_alpha(rgb)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=6))
    comps = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        keep = mask[sl] & (lab[sl] == i + 1)
        if keep.sum() < 150:
            continue
        comps.append((sl, keep))
    return col, a, comps


def bake(sheet_path):
    lay = json.loads(LAYOUT_JSON.read_text(encoding="utf-8"))
    col, a, comps = components(sheet_path)
    used = {}
    report = []
    for p in PARTS:
        pl = lay["parts"][p]
        cx, cy = pl["x"] + pl["w"] / 2, pl["y"] + pl["h"] / 2
        # 자리 상자와 가장 많이 겹치는 조각 (작은 부스러기는 같은 상자 안이면 합침)
        best = []
        for ci, (sl, keep) in enumerate(comps):
            ys, xs = np.nonzero(keep)
            ys = ys + sl[0].start
            xs = xs + sl[1].start
            inside = ((xs >= pl["x"] - 30) & (xs < pl["x"] + pl["w"] + 30) & (ys >= pl["y"] - 30) & (ys < pl["y"] + pl["h"] + 30)).mean()
            if inside > .6:
                best.append(ci)
        assert best, f"{p}: 조각 없음"
        for ci in best:
            assert ci not in used, f"{p}: 조각이 {used[ci]} 와 겹침"
            used[ci] = p
        m = np.zeros(a.shape, bool)
        for ci in best:
            sl, keep = comps[ci]
            m[sl] |= keep
        ys, xs = np.nonzero(m)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        piece = np.dstack([col[y0:y1, x0:x1], (np.where(m[y0:y1, x0:x1], a[y0:y1, x0:x1], 0) * 255).astype(np.uint8)])
        pim = Image.fromarray(piece, "RGBA")
        # 옛 그림 영역에 맞춰 늘림
        ox0, oy0, ox1, oy1 = pl["old_box"]
        old = Image.open(old_path(p))
        W, H = old.size
        tw, th = ox1 - ox0, oy1 - oy0
        sx, sy = tw / pim.width, th / pim.height
        # 1차: 그림 영역(테두리 상자)에 맞춤. 캔버스 c = s·u + o (u = 조각 px)
        ax, bx, ay, by = sx, float(ox0), sy, float(oy0)
        first = place_piece(pim, (W, H), ax, bx, ay, by)
        # 2차: 할핀(관절 점)이 있으면 옛 할핀 자리에 맞춤 (축마다 배율·이동, 점이 하나면 이동만)
        po, pn = pins(Image.open(old_path(p))), pins(first)
        pairs = []
        for qx, qy, _ in po:
            if pn:
                nx, ny, _ = min(pn, key=lambda c: (c[0] - qx) ** 2 + (c[1] - qy) ** 2)
                if (nx - qx) ** 2 + (ny - qy) ** 2 < 15 ** 2:
                    pairs.append(((nx, ny), (qx, qy)))
        note = "할핀 없음"
        if pairs:
            P = np.array([a_ for a_, _ in pairs], float)
            Q = np.array([b_ for _, b_ in pairs], float)
            fit = []
            for k in range(2):
                if len(pairs) >= 2 and np.ptp(P[:, k]) > 40:
                    g, h = np.polyfit(P[:, k], Q[:, k], 1)
                    g = float(np.clip(g, .94, 1.06))
                    h = float((Q[:, k] - g * P[:, k]).mean())
                else:
                    g, h = 1.0, float((Q[:, k] - P[:, k]).mean())
                fit.append((g, h))
            (gx, hx), (gy, hy) = fit
            ax, bx, ay, by = gx * ax, gx * bx + hx, gy * ay, gy * by + hy
            note = f"할핀 {len(pairs)}개 맞춤 (x×{gx:.3f}{hx:+.1f}, y×{gy:.3f}{hy:+.1f})"
        out = place_piece(pim, (W, H), ax, bx, ay, by)
        out.save(SPR / f"hero_v3_{p}.png")
        report.append(f"{p}: 캔버스 {W}x{H}, 영역 {tw}x{th}, 시트에서 늘림 x{sx:.2f} y{sy:.2f} (기대 {1 / S:.2f}), 시트 위치 차 ({x0 - pl['x']:+d},{y0 - pl['y']:+d}), {note}")
    print("\n".join(report))


def place_piece(pim, size, ax, bx, ay, by):
    """조각 px u → 캔버스 c = a·u + b 로 옮겨 그린 캔버스. 먼저 정수 배율 크기로 LANCZOS 축소/확대한 뒤 남은 소수 이동만 BICUBIC."""
    tw, th = max(1, round(pim.width * ax)), max(1, round(pim.height * ay))
    big = pim.convert("RGBa").resize((tw, th), Image.LANCZOS)   # 미리 곱한 알파로: 가장자리 색 번짐 없음
    # 남은 변환: 캔버스 c → big 픽셀 v = (c - b)·(tw / (w·a))
    kx, ky = tw / (pim.width * ax), th / (pim.height * ay)
    return big.transform(size, Image.AFFINE, (kx, 0, -bx * kx, 0, ky, -by * ky), resample=Image.BICUBIC).convert("RGBA")


def pins(alpha_rgba):
    """황동 할핀 점 중심 (그림 px)."""
    arr = np.asarray(alpha_rgba.convert("RGBA")).astype(int)
    r, g, b, al = arr[..., 0], arr[..., 1], arr[..., 2], arr[..., 3]
    m = (al > 200) & (r > 150) & (g > 90) & (b < 90) & (r - b > 90) & (g - b > 40)
    lab, n = ndimage.label(m)
    out = []
    for i in range(1, n + 1):
        ys, xs = np.nonzero(lab == i)
        if len(xs) > 30:
            out.append((round(float(xs.mean())), round(float(ys.mean())), len(xs)))
    return out


def check():
    rows = []
    for p in PARTS:
        o = Image.open(old_path(p)).convert("RGBA")
        n = Image.open(SPR / f"hero_v3_{p}.png").convert("RGBA")
        po, pn = pins(o), pins(n)
        print(p, "옛 할핀", [(x, y) for x, y, _ in po], "새 할핀", [(x, y) for x, y, _ in pn])
        rows.append((p, o, n))
    W = sum(max(o.width, n.width) + 12 for _, o, n in rows)
    H = max(o.height + n.height for _, o, n in rows) + 12
    sheet = Image.new("RGBA", (W, H), (170, 160, 150, 255))
    x = 0
    for _, o, n in rows:
        sheet.alpha_composite(o, (x, 0))
        sheet.alpha_composite(n, (x, o.height + 12))
        x += max(o.width, n.width) + 12
    out = ROOT / "Temp/Shots/hero23_restyle_parts.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print("check", out)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "layout"
    if cmd == "backup":
        backup()
    elif cmd == "layout":
        layout()
    elif cmd == "bake":
        bake(Path(sys.argv[2]) if len(sys.argv) > 2 else RAW / "hero_restyle_parts.png")
    elif cmd == "check":
        check()
