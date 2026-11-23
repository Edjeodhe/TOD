"""USS 의 글꼴을 구운 갈무리 비트맵 폰트 에셋으로 바꾸고, 글자 크기를 고유 픽셀 크기의 배수로 맞춘다.

python tools/snap_ui_fonts.py            (Assets/UI/*.uss 를 고친다)

갈무리는 픽셀 글꼴이라 고유 크기(Galmuri9=10px, Galmuri11=12px, Galmuri14=15px)의 정수배에서만 선명하다.
규칙마다 원래 크기에 가장 가까운 (글꼴, 크기) 조합을 고르고 -unity-font-definition 으로 구운 에셋(Assets/Fonts/Baked)을 쓴다.
굵은 글씨는 Galmuri11-Bold(12px 배수), 중간 크기는 Galmuri14 로 대신한다.
"""
import glob
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGULAR = [("Galmuri9", 10), ("Galmuri11", 12), ("Galmuri14", 15), ("Galmuri11", 24), ("Galmuri14", 30),
           ("Galmuri11", 36), ("Galmuri14", 45), ("Galmuri11", 48), ("Galmuri14", 60)]
BOLD = [("Galmuri11-Bold", 12), ("Galmuri14", 15), ("Galmuri11-Bold", 24), ("Galmuri14", 30),
        ("Galmuri11-Bold", 36), ("Galmuri11-Bold", 48)]
NATIVE = {"Galmuri9": 10, "Galmuri11": 12, "Galmuri11-Bold": 12, "Galmuri14": 15}


def asset(font):
    return f'-unity-font-definition: url("project://database/Assets/Fonts/Baked/{font}-{NATIVE[font]}px.asset")'


def nearest(size, bold):
    return min(BOLD if bold else REGULAR, key=lambda o: abs(math.log(size / o[1])))


FONT = re.compile(r'-unity-font:\s*url\("project://database/Assets/Fonts/(Galmuri[0-9A-Za-z-]+)\.ttf"\)')
SIZE = re.compile(r'font-size:\s*([0-9.]+)px')
BLOCK = re.compile(r'([^{}]+)\{([^{}]*)\}')

for path in glob.glob(str(ROOT / "Assets/UI/*.uss")):
    text = Path(path).read_text(encoding="utf-8")
    changes = []

    def fix(m):
        selector, body = m.group(1), m.group(2)
        font = FONT.search(body)
        size = SIZE.search(body)
        if not font and not size:
            return m.group(0)
        name = font.group(1) if font else None
        bold = name == "Galmuri11-Bold"
        if size:
            px = float(size.group(1))
            target, target_px = nearest(px, bold)
            body = SIZE.sub(f"font-size: {target_px}px", body, count=1)
        else:
            target, target_px = name, None
        if font:
            body = FONT.sub(asset(target), body, count=1)
        else:
            body = body.rstrip().rstrip(";") + f"; {asset(target)};"
        changes.append(f"{selector.strip()[:48]:48} {name or '(inherit)':16} {size.group(1) if size else '-':>5} -> {target} {target_px or ''}")
        return selector + "{" + body + "}"

    text = BLOCK.sub(fix, text)
    Path(path).write_text(text, encoding="utf-8")
    print(Path(path).name, len(changes), "rules")
    for c in changes:
        print("  ", c)
