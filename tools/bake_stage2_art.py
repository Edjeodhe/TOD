"""2차 개편 그림 굽기: Codex 시트(art_src/raw) → 투명 PNG (Assets/sprites).

python tools/bake_stage2_art.py

- stat_icons.png (초록 키, 4×2) → stat_*.png        레벨업 스탯 증강 아이콘
- item_icons.png (초록 키, 4×3) → item_*.png        쉬어가기 탑의 아이템(재봉 조각) 아이콘
- tower.png      (초록 키, 1개) → prop_tower.png     쉬어가기 라운드의 탑 (창문처럼 안쪽에 갇힌 키 색 구멍은 빛으로 채운다)
- xp_orb.png     (자홍 키, 1개) → xp_orb.png         경험치 구
- heart_pickup.png (초록 키, 1개) → heart_pickup.png 체력 회복 하트
- levelup_icon.png (자홍 키, 1개) → levelup_icon.png 레벨업 아이콘
- stairs_steps_v3.png (초록 키, 7개) → stair_0~6.png 계단 조각 (0 = 꼭대기 문, 1~6 = 위 단 → 아래 단, 6 = 바닥에 붙은 첫 단)
  (v3 가 없으면 v2/v1 시트, 6개 → stair_0~5.png)
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
RAW = ROOT / "art_src/raw"
OUT = ROOT / "Assets/sprites"

STATS = ["attack", "attack_speed", "move_speed", "health", "reach", "needle_speed", "yarn", "dash"]
ITEMS = ["ember_thimble", "torn_ribbon", "bloody_scissors", "mad_spool",
         "tear_button", "soaked_yarn", "frozen_pincushion", "sleeping_eye",
         "rusty_pin", "tangled_tape", "cracked_watch", "struck_machine"]


def key_alpha(rgb, key):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    if key == "green":
        dom = g - np.maximum(r, b)
        spill = g > np.maximum(r, b)
        fix = (r, np.where(spill, np.maximum(r, b), g), b)
    else:   # magenta: r·b 가 g 보다 얼마나 큰가
        dom = np.minimum(r, b) - g
        lim = g + 0
        fix = (np.where(dom > 0, np.minimum(r, np.maximum(g, b * .0 + g + 40)), r), g, np.where(dom > 0, np.minimum(b, g + 40), b))
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)
    col = np.stack(fix, -1).clip(0, 255).astype(np.uint8)
    return col, a


def pieces(path, key, count, merge, by_y=False):
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb, key)
    mask = a > .16
    lab, n = ndimage.label(ndimage.binary_dilation(mask, iterations=merge))
    found = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        area = int((mask[sl] & (lab[sl] == i + 1)).sum())
        found.append((area, i + 1, sl))
    found = sorted(found, reverse=True)[:count]
    # 읽기 순서: 줄(중심 y 를 120px 단위로 묶음) → x
    found.sort(key=lambda f: (round(((f[2][0].start + f[2][0].stop) / 2) / 160), f[2][1].start))
    if by_y:   # 세로로 쌓인 시트(계단): 위에서 아래 순서만 본다 (가운데 정렬이라 줄 묶음 + x 로는 순서가 뒤집힐 수 있다)
        found.sort(key=lambda f: (f[2][0].start + f[2][0].stop) / 2)
    result = []
    for _, li, sl in found:
        y0, y1 = max(0, sl[0].start - 6), sl[0].stop + 6
        x0, x1 = max(0, sl[1].start - 6), sl[1].stop + 6
        if by_y:   # 계단: 그림에 딱 맞게 자른다(그림 아래 끝 = 스프라이트 아래 끝이라야 조각을 바닥·앞 단에 정확히 붙일 수 있다)
            ys, xs = np.nonzero(mask[sl] & (lab[sl] == li))
            y0, y1 = max(0, sl[0].start + ys.min() - 2), sl[0].start + ys.max() + 3
            x0, x1 = max(0, sl[1].start + xs.min() - 2), sl[1].start + xs.max() + 3
        sub_a = a[y0:y1, x0:x1].copy()
        sub_a[lab[y0:y1, x0:x1] != li] = 0
        result.append((col[y0:y1, x0:x1].copy(), sub_a))
    return result


def fill_holes(col, a, color, top=.45):
    # 그림 위쪽(top 비율 안)에 갇힌 투명 구멍(키 색으로 그린 창문)만 빛 색으로 채운다. 아래쪽 실 고리 안 틈은 그대로 둔다.
    solid = a > .5
    holes = ndimage.binary_fill_holes(solid) & ~solid
    lab, n = ndimage.label(holes)
    keep = np.zeros_like(holes)
    for i, sl in enumerate(ndimage.find_objects(lab)):
        if (sl[0].start + sl[0].stop) / 2 < top * a.shape[0]:
            keep[sl] |= lab[sl] == i + 1
    keep = ndimage.binary_dilation(keep, iterations=1) & (a < .9) | keep
    col[keep] = color
    a[keep] = 1
    return col, a


def save(col, a, name, max_side):
    im = Image.fromarray(np.dstack([col, (a * 255).astype(np.uint8)]), "RGBA")
    scale = min(1, max_side / max(im.size))
    if scale < 1:
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
    im.save(OUT / f"{name}.png")
    print("wrote", name, im.size)


for (col, a), name in zip(pieces(RAW / "stat_icons.png", "green", 8, 30), STATS):
    save(col, a, f"stat_{name}", 256)
for (col, a), name in zip(pieces(RAW / "item_icons.png", "green", 12, 30), ITEMS):
    save(col, a, f"item_{name}", 256)
(col, a), = pieces(RAW / "tower.png", "green", 1, 40)
col, a = fill_holes(col, a, (214, 238, 228))
save(col, a, "prop_tower", 512)
(col, a), = pieces(RAW / "xp_orb.png", "magenta", 1, 8)
save(col, a, "xp_orb", 128)
# 체력 회복 하트 (초록 키) — 4차 이후 추가
if (RAW / "heart_pickup.png").exists():
    (col, a), = pieces(RAW / "heart_pickup.png", "green", 1, 8)
    save(col, a, "heart_pickup", 128)
# 레벨업 아이콘 (자홍 키, 반짝이 별까지 한 장으로) — 6차 추가
if (RAW / "levelup_icon.png").exists():
    (col, a), = pieces(RAW / "levelup_icon.png", "magenta", 1, 40)
    save(col, a, "levelup_icon", 128)
# 8차 새 아이템 2종 (초록 키, 왼쪽 → 오른쪽): 세 갈래 바늘 골무(Multishot) · 굵은 밧줄 실패(ThreadWidth). 바늘 끝 불티가 떨어져 있어 merge 를 크게
if (RAW / "item_icons_v2.png").exists():
    for (col, a), name in zip(pieces(RAW / "item_icons_v2.png", "green", 2, 60), ["triple_thimble", "rope_spool"]):
        save(col, a, f"item_{name}", 256)
# 계단 조각 (초록 키, 위에서 아래: 꼭대기 문 + 단들) — 모두 같은 배율로 둬야 단이 아래로 갈수록 넓어지는 비율이 유지된다
# v2 = 바닥 타일을 참고로 같은 돌·색·결로 다시 뽑은 시트 (v1 은 검은 외곽선·분홍 바늘땀이라 바닥과 따로 놀았다)
# v3 = 종이 질감(찢어진 종이 가장자리·종이 결·잉크 선) + 맨 아래에 바닥에 납작하게 붙은 첫 단을 더한 7조각 (8차)
STAIRS = next(((p, c) for p, c in ((RAW / "stairs_steps_v3.png", 7), (RAW / "stairs_steps_v2.png", 6), (RAW / "stairs_steps.png", 6)) if p.exists()), None)
if STAIRS is not None:
    STAIRS, count = STAIRS
    found = pieces(STAIRS, "green", count, 20, by_y=True)
    if count == 7:   # v3 는 바닥보다 밝고 붉게 나온다 → 돌(붉은 쪽) 부분만 바닥 평균 색 × 1.25 로 맞춘다. 문 안쪽 상아·청록 빛(g ≥ r)은 그대로.
        floor = np.asarray(Image.open(OUT / "floor.png").convert("RGB")).reshape(-1, 3).astype(np.float32).mean(0)
        stone = np.concatenate([c[(al > .5) & (c[..., 0] > c[..., 1])] for c, al in found]).astype(np.float32)
        gain = floor * 1.25 / stone.mean(0)
        print("stairs tone gain", gain.round(3))
        for c, al in found:
            f = c.astype(np.float32)
            w = np.clip((f[..., 0] - f[..., 1]) / 20, 0, 1)[..., None]
            c[:] = (f * (1 - w) + f * gain * w).clip(0, 255).astype(np.uint8)
    for i, (col, a) in enumerate(found):
        save(col, a, f"stair_{i}", 4096)
# 12차 새 세트 아이템 (Codex CLI): v3 = 빛·어둠·피 (초록 키, 4×3) · v4 = 바람·독 (자홍 키, 4×2, 초록 색이 그림 안에 있어서)
ITEMS_V3 = ["candle_thimble", "gilded_button", "choir_ribbon", "lantern_cushion",
            "shadow_button", "black_spool", "raven_quill", "hollow_doll",
            "blood_pin", "bleeding_thimble", "vein_yarn", "heart_cushion"]
ITEMS_V4 = ["feather_needle", "vane_button", "drifting_lace", "pinwheel_spool",
            "venom_pin", "mold_button", "thorn_yarn", "vial_thimble"]
if (RAW / "item_icons_v3.png").exists():
    for (col, a), name in zip(pieces(RAW / "item_icons_v3.png", "green", 12, 30), ITEMS_V3):
        save(col, a, f"item_{name}", 256)
if (RAW / "item_icons_v4.png").exists():
    found = pieces(RAW / "item_icons_v4.png", "magenta", 8, 30)
    found[6], found[7] = found[7], found[6]   # 약병 골무가 위로 솟아 줄 묶음에서 앞 순서로 잡힌다 → 시트 순서대로 바로잡기
    for (col, a), name in zip(found, ITEMS_V4):
        save(col, a, f"item_{name}", 256)
# 12차 맵 장식 (Codex CLI, 초록 키, 4×2): 상호작용 없음, 캐릭터·몬스터보다 항상 뒤(Ground 층)에 그린다
DECOR = ["mannequin", "yarn_pile", "sewing_machine", "rag_doll", "mirror_shards", "candlestick", "stain_decal", "scissors_heap"]
def grid_pieces(path, key, cols, rows):
    """칸(cols×rows)마다 그 안의 그림 전부를 한 장으로 (거울 조각처럼 흩어진 그림도 한 덩어리로)."""
    rgb = np.asarray(Image.open(path).convert("RGB"))
    col, a = key_alpha(rgb, key)
    a = np.where(a > .16, a, 0)
    h, w = a.shape
    out = []
    for r in range(rows):
        for c in range(cols):
            y0, y1, x0, x1 = r * h // rows, (r + 1) * h // rows, c * w // cols, (c + 1) * w // cols
            sub = a[y0:y1, x0:x1]
            ys, xs = np.nonzero(sub > .16)
            if len(ys) == 0:
                continue
            by0, by1 = max(0, ys.min() - 4), min(sub.shape[0], ys.max() + 5)
            bx0, bx1 = max(0, xs.min() - 4), min(sub.shape[1], xs.max() + 5)
            out.append((col[y0 + by0:y0 + by1, x0 + bx0:x0 + bx1].copy(), sub[by0:by1, bx0:bx1].copy()))
    return out


if (RAW / "decor_props.png").exists():
    for (col, a), name in zip(grid_pieces(RAW / "decor_props.png", "green", 4, 2), DECOR):
        save(col, a, f"decor_{name}", 320)
# 14차 치명타 아이콘 (Codex CLI, 초록 키, 1개): 피해 숫자 왼쪽 위 뾰족한 폭발 별
if (RAW / "crit_icon.png").exists():
    (col, a), = pieces(RAW / "crit_icon.png", "green", 1, 30)
    save(col, a, "crit_icon", 256)
