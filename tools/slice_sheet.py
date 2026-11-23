"""크로마키(초록) 시트 → 투명 PNG 조각들.

사용:
  python tools/slice_sheet.py art_src/raw/hero_parts.png assets/sprites hero --min 800
  → assets/sprites/hero_00.png ... + hero_sheet.json(bbox) + hero_preview.png(번호표)
  python tools/slice_sheet.py ... --names head,torso,...   (읽기 순서대로 이름 지정)
  python tools/slice_sheet.py art_src/raw/floor.png assets/sprites floor --whole  (키 없이 복사)
"""
import argparse, json, os
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage


def chroma_alpha(rgb):
    r, g, b = [rgb[..., i].astype(np.float32) for i in range(3)]
    # 초록 우세도: g 가 r,b 보다 얼마나 큰가
    dom = g - np.maximum(r, b)
    a = np.clip(1.0 - (dom - 40) / 60.0, 0, 1)  # dom<=40 → 불투명, dom>=100 → 투명
    # 디스필: 초록이 남은 가장자리 픽셀의 g 를 max(r,b)로 눌러줌
    spill = g > np.maximum(r, b)
    g2 = np.where(spill, np.maximum(r, b), g)
    out = np.stack([r, g2, b], -1).clip(0, 255).astype(np.uint8)
    return out, (a * 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('outdir'); ap.add_argument('prefix')
    ap.add_argument('--min', type=int, default=600, help='최소 조각 면적(px)')
    ap.add_argument('--merge', type=int, default=6, help='가까운 조각 병합 반경(px)')
    ap.add_argument('--pad', type=int, default=6)
    ap.add_argument('--names', default='')
    ap.add_argument('--row', type=int, default=120, help='같은 줄로 볼 y 허용치')
    ap.add_argument('--whole', action='store_true')
    args = ap.parse_args()
    os.makedirs(args.outdir, exist_ok=True)
    im = Image.open(args.src).convert('RGB')
    if args.whole:
        im.save(os.path.join(args.outdir, f'{args.prefix}.png'))
        return
    rgb = np.asarray(im)
    col, alpha = chroma_alpha(rgb)
    mask = alpha > 40
    grown = ndimage.binary_dilation(mask, iterations=args.merge)
    lab, n = ndimage.label(grown)
    objs = ndimage.find_objects(lab)
    comps = []
    for i, sl in enumerate(objs):
        area = int((mask[sl] & (lab[sl] == i + 1)).sum())
        if area < args.min:
            continue
        y0, y1, x0, x1 = sl[0].start, sl[0].stop, sl[1].start, sl[1].stop
        comps.append((i + 1, x0, y0, x1, y1, area))
    # 읽기 순서: 중심 y 로 줄 묶고 x 정렬
    comps.sort(key=lambda c: (c[2] + c[4]) / 2)
    rows, cur = [], []
    for c in comps:
        cy = (c[2] + c[4]) / 2
        if cur and abs(cy - np.mean([(d[2] + d[4]) / 2 for d in cur])) > args.row:
            rows.append(cur); cur = []
        cur.append(c)
    if cur: rows.append(cur)
    ordered = [c for r in rows for c in sorted(r, key=lambda c: c[1])]
    names = [s.strip() for s in args.names.split(',')] if args.names else []
    meta = []
    prev = im.copy(); dr = ImageDraw.Draw(prev)
    H, W = mask.shape
    for k, (li, x0, y0, x1, y1, area) in enumerate(ordered):
        x0p, y0p = max(0, x0 - args.pad), max(0, y0 - args.pad)
        x1p, y1p = min(W, x1 + args.pad), min(H, y1 + args.pad)
        sub_a = alpha[y0p:y1p, x0p:x1p].copy()
        sub_a[lab[y0p:y1p, x0p:x1p] != li] = 0
        rgba = np.dstack([col[y0p:y1p, x0p:x1p], sub_a])
        name = names[k] if k < len(names) and names[k] else f'{args.prefix}_{k:02d}'
        if names and k < len(names): name = f'{args.prefix}_{name}'
        Image.fromarray(rgba, 'RGBA').save(os.path.join(args.outdir, name + '.png'))
        meta.append({'name': name, 'bbox': [int(x0p), int(y0p), int(x1p), int(y1p)], 'area': area})
        dr.rectangle([x0p, y0p, x1p, y1p], outline=(255, 0, 0), width=3)
        dr.text((x0p + 4, y0p + 4), f'{k}', fill=(255, 255, 0))
    prev.save(os.path.join(args.outdir, f'_{args.prefix}_preview.png'))
    with open(os.path.join(args.outdir, f'_{args.prefix}_sheet.json'), 'w', encoding='utf-8') as f:
        json.dump(meta, f, ensure_ascii=False, indent=1)
    print(f'{len(meta)} pieces')
    for m in meta: print(m['name'], m['bbox'], m['area'])


if __name__ == '__main__':
    main()
