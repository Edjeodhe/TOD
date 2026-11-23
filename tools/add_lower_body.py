"""하체 파츠(골반·앞 코트 자락)를 현재 리그에 끼워 넣어 상체/하체를 분리한다. 기존 애니메이션은 유지.

python tools/add_lower_body.py      (여러 번 실행해도 같은 결과)
  입력  art_src/raw/hero_flat_lower.png  (ChatGPT: 골반 · 앞자락 근측 · 앞자락 원측, 초록 배경)
  출력  assets/sprites/hero_quarter_{pelvis,skirt_near,skirt_far}.png, assets/rig/hero-quarter.rig.json

구조:  hips ─┬─ pelvis ─┬─ near_thigh ─ shin ─ foot      (하체: 골반이 다리를 들고 다닌다)
             │          └─ far_thigh  ─ shin ─ foot
             └─ torso ──┬─ head / arms                  (상체: 허리에서 따로 굽는다)
                        ├─ skirt_near (앞자락, 다리 앞)
                        └─ skirt_far  (앞자락, 골반 뒤)
그 다음 python tools/symmetric_run.py 로 달리기 대칭 보정을 다시 계산한다.
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, str(Path(__file__).resolve().parent))
import rigfk  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
SHEET = ROOT / "art_src/raw/hero_flat_lower.png"
SPRITES = ROOT / "assets/sprites"

# ---- 조정값 (리그 단위; 골반 원점 = hips) ----
PELVIS_WIDTH = 300      # 골반 그림 폭 → 몸통 허리 폭과 비슷하게
PELVIS_TOP = (8, 20)    # 골반 윗 할핀이 놓일 hips 좌표 (허리띠는 몸통 밑단 아래로 숨는다)
THIGH_WORLD = .45       # 허벅지 최종 배율 (골반이 생긴 만큼 다리는 짧게)
SKIRT_WIDTH = 150       # 앞자락 폭
SKIRT_NEAR = (96, 292)  # 몸통 그림 좌표: 근측 앞자락이 매달리는 곳
SKIRT_FAR = (236, 286)  # 몸통 그림 좌표: 원측 앞자락
FOOT_DEPTH_NEAR = 4


def pieces():
    rgb = np.asarray(Image.open(SHEET).convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    dom = g - np.maximum(r, b)
    alpha = np.clip((95 - dom) / 55, 0, 1)
    mask = alpha > .5
    lab, n = ndimage.label(ndimage.binary_closing(mask, iterations=3))
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    keep = sorted([i + 1 for i, s in enumerate(sizes) if s > 5000], key=lambda i: ndimage.center_of_mass(mask, lab, i)[1])
    assert len(keep) == 3, f"expected 3 pieces, found {len(keep)}"
    clean = np.dstack([r, np.minimum(g, np.maximum(r, b)), b]).clip(0, 255)
    out = []
    for label in keep:
        region = ndimage.binary_dilation(lab == label, iterations=3)
        ys, xs = np.nonzero(region & mask)
        x0, x1, y0, y1 = xs.min() - 3, xs.max() + 4, ys.min() - 3, ys.max() + 4
        a = np.where(region[y0:y1, x0:x1], alpha[y0:y1, x0:x1], 0)
        img = np.dstack([clean[y0:y1, x0:x1], a * 255]).astype(np.uint8)
        # 황동 할핀: 주황 원 → 중심
        cr, cg, cb = img[..., 0].astype(int), img[..., 1].astype(int), img[..., 2].astype(int)
        brass = (cr > 170) & (cg > 110) & (cg < 200) & (cb < 110) & (img[..., 3] > 200)
        pl, pn = ndimage.label(brass)
        pins = [ndimage.center_of_mass(brass, pl, i + 1) for i in range(pn) if (pl == i + 1).sum() > 120]
        out.append((img, [(float(x), float(y)) for y, x in pins]))
    return out


def main():
    (pelvis, ppins), (near, npins), (far, fpins) = pieces()
    for name, img in (("pelvis", pelvis), ("skirt_near", near), ("skirt_far", far)):
        Image.fromarray(img, "RGBA").save(SPRITES / f"hero_quarter_{name}.png", optimize=True)
    top = min(ppins, key=lambda p: p[1])
    # 버클도 황동색이라 할핀처럼 잡힌다 → 가장 아래 두 개가 다리 할핀
    bottom = sorted(sorted([p for p in ppins if p is not top], key=lambda p: -p[1])[:2], key=lambda p: p[0])
    assert len(bottom) == 2, f"pelvis pins: {ppins}"
    skirt_pin = lambda pins, img: min(pins, key=lambda p: p[1]) if pins else (img.shape[1] / 2, 20)

    rig = rigfk.load()
    rig["parts"] = [p for p in rig["parts"] if p["id"] not in ("pelvis", "skirt_near", "skirt_far")]
    by = {p["id"]: p for p in rig["parts"]}
    s_pelvis = PELVIS_WIDTH / pelvis.shape[1]
    s_skirt = SKIRT_WIDTH / near.shape[1]
    parts = rig["parts"]
    idx = parts.index(by["torso"])
    parts.insert(idx, dict(id="pelvis", img="hero_quarter_pelvis.png", parent="hips", pivot=[round(top[0]), round(top[1])],
                           attach=list(PELVIS_TOP), z=-1, rest=0, scale=round(s_pelvis, 4), shade=0))
    # 다리는 골반 할핀에 매단다 (근측 = 왼쪽 할핀, 원측 = 오른쪽 할핀 — 기존 배치와 같은 방향)
    for leg, pin in (("near_thigh", bottom[0]), ("far_thigh", bottom[1])):
        by[leg]["parent"] = "pelvis"
        by[leg]["attach"] = [round(pin[0]), round(pin[1])]
        by[leg]["scale"] = round(THIGH_WORLD / s_pelvis, 4)
    by["far_shin"]["scale"] = by["near_shin"]["scale"]
    parts.append(dict(id="skirt_near", img="hero_quarter_skirt_near.png", parent="torso",
                      pivot=[round(v) for v in skirt_pin(npins, near)], attach=list(SKIRT_NEAR), z=1, rest=0,
                      scale=round(s_skirt, 4), shade=0))
    parts.append(dict(id="skirt_far", img="hero_quarter_skirt_far.png", parent="torso",
                      pivot=[round(v) for v in skirt_pin(fpins, far)], attach=list(SKIRT_FAR), z=-0.5, rest=0,
                      scale=round(s_skirt * .9, 4), shade=.2))
    # 기본 자세에서 앞발이 바닥(깊이 4)에 닿도록 루트 높이 재계산
    for _ in range(3):
        M = rigfk.matrices(rig, *rigfk.pose(rig))
        sole = next(f for f in rig["feet"] if f["id"] == "near_foot")
        y = rigfk.world(M, "near_foot", sole["sole"])[1]
        rig["rootPos"][1] = round(rig["rootPos"][1] + FOOT_DEPTH_NEAR - y, 1)
    M = rigfk.matrices(rig, *rigfk.pose(rig))
    far_sole = rigfk.world(M, "far_foot", next(f for f in rig["feet"] if f["id"] == "far_foot")["sole"])[1]
    next(f for f in rig["feet"] if f["id"] == "far_foot")["depth"] = round(far_sole, 1)
    rig.pop("_groundMean", None)
    rigfk.save(rig)
    print("pelvis pins", [tuple(map(round, p)) for p in ppins], "scale", round(s_pelvis, 3))
    print("skirt pins", npins, fpins, "rootPos", rig["rootPos"])


if __name__ == "__main__":
    main()
