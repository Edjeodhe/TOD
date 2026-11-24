"""모이라 리그 미리보기 (리깅 페이지와 같은 계산): art_src/rig/moira_rig.json → PNG

python tools/preview_moira_rig.py out.png [--talk] [--time T] [--ref 0.35] [--scale 2] [--bones]

--talk 은 카드 들기(raise 끝 자세 + hold), --ref 는 참고 그림 겹치기 세기, --bones 는 뼈·관절점 표시.
"""
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]


def arg(name, default):
    return type(default)(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default


def mul(m, n):
    return [m[0]*n[0]+m[2]*n[1], m[1]*n[0]+m[3]*n[1], m[0]*n[2]+m[2]*n[3], m[1]*n[2]+m[3]*n[3], m[0]*n[4]+m[2]*n[5]+m[4], m[1]*n[4]+m[3]*n[5]+m[5]]


def tr(x, y): return [1, 0, 0, 1, x, y]


def rot(d):
    r = math.radians(d)
    return [math.cos(r), math.sin(r), -math.sin(r), math.cos(r), 0, 0]


def sc(s): return [s, 0, 0, s, 0, 0]


def ap(m, p): return (m[0]*p[0]+m[2]*p[1]+m[4], m[1]*p[0]+m[3]*p[1]+m[5])


def wave(w, u):
    return w.get("offset", 0) + w.get("a1", 0)*math.sin(2*math.pi*(u+w.get("p1", 0))) + w.get("a2", 0)*math.sin(4*math.pi*(u+w.get("p2", 0)))


def shot(m, t, a, b, c):
    if m.get("peakTime", 0) > 0 and t < m["peakTime"]:
        u = t / m["peakTime"]
        if m.get("easeIn"): u = 1 - (1-u)*(1-u)
        return a + (b-a)*u
    if t < m.get("settleTime", 0):
        return b + (c-b)*((t-m["peakTime"])/(m["settleTime"]-m["peakTime"]) if m["settleTime"] > m["peakTime"] else 1)
    return c


def main():
    rig = json.loads((ROOT / "art_src/rig/moira_rig.json").read_text(encoding="utf-8"))
    talk, t, S = "--talk" in sys.argv, arg("--time", 0.0), arg("--scale", 2.0)
    ang = {b["id"]: 0.0 for b in rig["bones"]}
    for m, w in ((rig["idle"], 1), (rig["hold"], 1 if talk else 0)):
        if w == 0: continue
        u = (t % m["length"]) / m["length"]
        for bw in m.get("bones", []):
            if bw["bone"] in ang: ang[bw["bone"]] += wave(bw, u) * w
    if talk:
        m = rig["raise"]
        for bp in m["bones"]:
            if bp["bone"] in ang: ang[bp["bone"]] += shot(m, m["length"], bp["start"], bp["peak"], bp["settle"])
    W = {}
    for b in rig["bones"]:
        m = tr(*rig["rootPos"]) if not b.get("parent") else mul(W[b["parent"]], tr(*b["at"]))
        m = mul(m, rot(b.get("rest", 0) + ang[b["id"]]))
        if b.get("scale", 1) != 1: m = mul(m, sc(b["scale"]))
        W[b["id"]] = m
    size = (int(560*S), int(560*S))
    origin = (size[0]/2, size[1]*.92)
    view = [S, 0, 0, S, origin[0], origin[1]]
    out = Image.new("RGBA", size, (15, 12, 18, 255))
    ref = rig.get("reference")
    alpha = arg("--ref", .0)
    if ref and alpha > 0:
        im = Image.open(ROOT / ref["file"]).convert("RGBA")
        s = ref.get("scale", 1) * S
        im = im.resize((int(im.width*s), int(im.height*s)))
        a = im.split()[3].point(lambda v: int(v*alpha))
        im.putalpha(a)
        out.alpha_composite(im, (int(origin[0] + ref["x"]*S - im.width/2), int(origin[1] + ref["y"]*S - im.height)))
    show_fan = talk
    def part_of(b):
        return b.get("talkPart") if talk and b.get("talkPart") else b.get("part")
    for b in sorted([b for b in rig["bones"] if part_of(b)], key=lambda b: b.get("order", 0)):
        if b["id"] == rig.get("fanBone") and not show_fan: continue
        part = rig["parts"][part_of(b)]
        im = Image.open(ROOT / part["file"]).convert("RGBA")
        if b.get("flip"):
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
            piv = (im.width - part["pivot"][0], part["pivot"][1])
        else:
            piv = tuple(part["pivot"])
        if b.get("shade"):
            dark = Image.new("RGBA", im.size, (40, 30, 50, 255))
            dark.putalpha(im.split()[3])
            im = Image.blend(im, dark, b["shade"])
        m = mul(view, W[b["id"]])
        if b.get("artScale", 1) != 1: m = mul(m, sc(b["artScale"]))   # 그림만 키움 (자식 뼈는 그대로)
        if part.get("rot"): m = mul(m, rot(part["rot"] * (-1 if b.get("flip") else 1)))
        if part.get("scale", 1) != 1: m = mul(m, sc(part["scale"]))
        m = mul(m, tr(-piv[0], -piv[1]))
        # PIL affine 는 출력 → 입력 역행렬
        det = m[0]*m[3]-m[1]*m[2]
        ia, ib, ic, id_ = m[3]/det, -m[1]/det, -m[2]/det, m[0]/det
        ie, if_ = -(ia*m[4]+ic*m[5]), -(ib*m[4]+id_*m[5])
        layer = im.transform(size, Image.AFFINE, (ia, ic, ie, ib, id_, if_), resample=Image.BICUBIC)
        out.alpha_composite(layer)
    if "--bones" in sys.argv:
        d = ImageDraw.Draw(out)
        for b in rig["bones"]:
            p = ap(mul(view, W[b["id"]]), (0, 0))
            if b.get("parent"):
                q = ap(mul(view, W[b["parent"]]), (0, 0))
                d.line([q, p], fill=(214, 176, 110, 200), width=2)
            d.ellipse([p[0]-4, p[1]-4, p[0]+4, p[1]+4], fill=(255, 107, 138, 255))
            d.text((p[0]+5, p[1]-12), b["id"], fill=(255, 255, 255, 255))
    out.save(sys.argv[1])
    print("wrote", sys.argv[1])


if __name__ == "__main__":
    main()
