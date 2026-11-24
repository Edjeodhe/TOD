"""모이라 리그 JSON → Unity 조립용 한 줄 형식 (art_src/rig/moira_rig_unity.txt)

python tools/moira_rig_export.py

MCP execute_code(CodeDom C# 6)는 JSON 형식을 정의할 수 없어서, 줄마다 '|'로 나눈 값을 쓴다.
그림 자리(Art 의 localPosition/Rotation/Scale)는 여기서 미리 계산한다 (Unity 단위 = 그림 px / 100, 위 +y, 회전은 반시계 +).
  META|height|talkFollow|swapAfter|cardBone
  BONE|id|parent|atx|aty|rest|order|scale|shade|pivx|pivy
  ART|bone|file|posx|posy|rotz|sx|sy            (그 뼈의 그림, 없으면 줄 없음)
  TALK|bone|file|posx|posy|rotz|sx|sy|cardx|cardy (말할 때 그림 + 카드가 날아 나가는 자리, 뼈 기준 Unity 단위)
  LOOP|idle 또는 hold|length
  WAVE|idle 또는 hold|bone|offset|a1|p1|a2|p2
  SHOT|raise|length|peakTime|settleTime|keepEnd|easeIn
  POSE|raise|bone|start|peak|settle
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PPU = 100.0


def art(part, bone):
    """뼈 원점 = 그림의 pivot. 스프라이트 피벗은 가운데. flip 은 pivot 세로축 거울, rot 는 그림 회전(캔버스 시계 +)."""
    w, h = part["w"], part["h"]
    vx, vy = w / 2 - part["pivot"][0], h / 2 - part["pivot"][1]   # 캔버스 px (아래 +)
    s = bone.get("artScale", 1) * part.get("scale", 1)
    flip = -1 if bone.get("flip") else 1
    vx, vy = vx * s * flip, vy * s
    r = math.radians(part.get("rot", 0) * flip)
    rx, ry = vx * math.cos(r) - vy * math.sin(r), vx * math.sin(r) + vy * math.cos(r)
    return [rx / PPU, -ry / PPU, -part.get("rot", 0) * flip, s * flip, s]


def point(part, bone, p):
    """그림 안 한 점(px) → 뼈 기준 Unity 단위"""
    s = bone.get("artScale", 1) * part.get("scale", 1)
    flip = -1 if bone.get("flip") else 1
    vx, vy = (p[0] - part["pivot"][0]) * s * flip, (p[1] - part["pivot"][1]) * s
    r = math.radians(part.get("rot", 0) * flip)
    rx, ry = vx * math.cos(r) - vy * math.sin(r), vx * math.sin(r) + vy * math.cos(r)
    return [rx / PPU, -ry / PPU]


def main():
    rig = json.loads((ROOT / "art_src/rig/moira_rig.json").read_text(encoding="utf-8"))
    f = lambda v: f"{v:.4f}".rstrip("0").rstrip(".") if isinstance(v, float) else str(v)
    lines = [f"META|{rig['height']}|{rig.get('talkFollow', 6)}|{rig.get('swapAfter', .8)}|{rig.get('cardBone', '')}"]
    for b in rig["bones"]:
        part = rig["parts"].get(b.get("part") or "", None)
        piv = part["pivot"] if part else [0, 0]
        at = rig["rootPos"] if not b.get("parent") else b["at"]
        lines.append("|".join(map(f, ["BONE", b["id"], b.get("parent") or "", float(at[0]), float(at[1]), float(b.get("rest", 0)),
                                       b.get("order", 0), float(b.get("scale", 1)), float(b.get("shade", 0)), float(piv[0]), float(piv[1])])))
        if part:
            lines.append("|".join(map(f, ["ART", b["id"], part["file"], *[float(v) for v in art(part, b)]])))
        if b.get("talkPart"):
            tp = rig["parts"][b["talkPart"]]
            cp = point(tp, b, tp.get("cardPoint", tp["pivot"]))
            lines.append("|".join(map(f, ["TALK", b["id"], tp["file"], *[float(v) for v in art(tp, b)], *[float(v) for v in cp]])))
    for name in ("idle", "hold"):
        m = rig[name]
        lines.append(f"LOOP|{name}|{m['length']}")
        for w in m.get("bones", []):
            lines.append("|".join(map(f, ["WAVE", name, w["bone"], *[float(w.get(k, 0)) for k in ("offset", "a1", "p1", "a2", "p2")]])))
    m = rig["raise"]
    lines.append(f"SHOT|raise|{m['length']}|{m['peakTime']}|{m['settleTime']}|{int(m.get('keepEnd', False))}|{int(m.get('easeIn', False))}")
    for p in m["bones"]:
        lines.append("|".join(map(f, ["POSE", "raise", p["bone"], float(p["start"]), float(p["peak"]), float(p["settle"])])))
    out = ROOT / "art_src/rig/moira_rig_unity.txt"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("wrote", out, len(lines), "lines")


if __name__ == "__main__":
    main()
