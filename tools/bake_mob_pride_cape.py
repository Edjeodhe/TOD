"""25차 교만 차저 돌진 꼬리: Blender 낱장(art_src/vfx/24/pride_cape/frame_00~07.png) → 게임 시트.

  python tools/bake_mob_pride_cape.py
출력: Assets/Art/VFX/16/mob_pride_dash.png (8칸 256px, 다른 시트처럼 얇은 거친 잉크 외곽)
      Assets/Art/VFX/16/mob_pride_dash_glow.png (어두운 보랏빛 번짐, 가산 합성용이라 세기를 낮게)
      Temp/Shots/mob_pride_dash_preview.png (바닥색 위 본체 + 빛)
옛 2D 리본판(23차)은 art_src/vfx/24/pride_cape/old_mob_pride_dash*.png 로 남김.
"""
import shutil
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "art_src/vfx/24/pride_cape"
OUT = ROOT / "Assets/Art/VFX/16"
FRAMES, SIZE = 8, 256
INK = np.array([0x1b, 0x15, 0x17], np.float32) / 255
INK_RADIUS = 1.8
GLOW = np.array([0x4a, 0x2e, 0x7a], np.float32) / 255
GLOW_SIGMA, GLOW_MAX = 9, .32


def inked(px, seed):
    a = px[..., 3]
    dist = ndimage.distance_transform_edt(a <= .3)
    rng = np.random.default_rng(seed)
    noise = ndimage.gaussian_filter(rng.random(a.shape), 6)
    noise = (noise - noise.min()) / (np.ptp(noise) + 1e-6)
    rad = INK_RADIUS * (.6 + .8 * noise)
    ink = np.clip(rad - dist + .5, 0, 1) * (a <= .3) + 0
    oa = a + ink * (1 - a)
    rgb = (px[..., :3] * a[..., None] + INK * (ink * (1 - a))[..., None]) / np.maximum(oa, 1e-4)[..., None]
    return np.dstack([rgb, oa])


def main():
    for name in ("mob_pride_dash.png", "mob_pride_dash_glow.png"):
        old = SRC / f"old_{name}"
        if not old.exists() and (OUT / name).exists():
            shutil.copy(OUT / name, old)
    bodies, glows = [], []
    for i in range(FRAMES):
        px = np.asarray(Image.open(SRC / f"frame_{i:02d}.png").convert("RGBA")).astype(np.float32) / 255
        bodies.append(inked(px, seed=i * 97 + 13))
        g = ndimage.gaussian_filter(px[..., 3], GLOW_SIGMA)
        glows.append(np.dstack([np.broadcast_to(GLOW, g.shape + (3,)), np.clip(g * GLOW_MAX * 1.6, 0, GLOW_MAX)]))
    save = lambda arr, path: Image.fromarray((np.concatenate(arr, 1) * 255).clip(0, 255).astype(np.uint8), "RGBA").save(path)
    save(bodies, OUT / "mob_pride_dash.png")
    save(glows, OUT / "mob_pride_dash_glow.png")
    print("wrote", OUT / "mob_pride_dash.png")

    bg = Image.new("RGBA", (SIZE * FRAMES, SIZE), (58, 44, 74, 255))
    glow = Image.open(OUT / "mob_pride_dash_glow.png")
    g = np.asarray(glow).astype(np.float32) / 255
    base = np.asarray(bg).astype(np.float32) / 255
    base[..., :3] = np.clip(base[..., :3] + g[..., :3] * g[..., 3:4], 0, 1)
    prev = Image.fromarray((base * 255).astype(np.uint8), "RGBA")
    prev.alpha_composite(Image.open(OUT / "mob_pride_dash.png"))
    shots = ROOT / "Temp/Shots"
    shots.mkdir(parents=True, exist_ok=True)
    prev.save(shots / "mob_pride_dash_preview.png")


if __name__ == "__main__":
    main()
