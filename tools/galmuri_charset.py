"""갈무리 비트맵 폰트 에셋에 미리 구워 넣을 글자 목록을 만든다.

python tools/galmuri_charset.py  ->  Temp/galmuri_charset.txt

- UXML 의 글자, GameDatabase 문자열(Temp/db_dump.json — MCP 로 EditorJsonUtility 덤프), 런타임 C# 문자열 리터럴
- ASCII, 자주 쓰는 기호, KS X 1001 한글 2350자 (앞으로 쓸 문구 대비)
빠진 글자는 실행 중 동적으로 더 구워지지만(Dynamic), 미리 넣어 두면 첫 표시 때 끊기지 않는다.
"""
import glob
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
chars = set(chr(c) for c in range(32, 127))
chars |= set("·…—–→←↑↓×÷±%‰°★☆♥♡♪◇◆○●□■△▲▽▼✦✧♜✕✓「」『』《》〈〉【】‘’“”ⅡⅢ")
for f in glob.glob(str(ROOT / "Assets/UI/*.uxml")):
    chars |= set(Path(f).read_text(encoding="utf-8"))
dump = ROOT / "Temp/db_dump.json"
if dump.exists():
    chars |= set(dump.read_text(encoding="utf-8"))
literal = re.compile(r'"((?:[^"\\]|\\.)*)"')
for f in glob.glob(str(ROOT / "Assets/Scripts/Runtime/**/*.cs"), recursive=True):
    for m in literal.findall(Path(f).read_text(encoding="utf-8")):
        chars |= set(m)
for b1 in range(0xB0, 0xC9):
    for b2 in range(0xA1, 0xFF):
        try:
            chars.add(bytes([b1, b2]).decode("euc-kr"))
        except UnicodeDecodeError:
            pass
chars = {c for c in chars if c.isprintable()}
(ROOT / "Temp/galmuri_charset.txt").write_text("".join(sorted(chars)), encoding="utf-8")
print(len(chars), "chars,", sum(1 for c in chars if "가" <= c <= "힣"), "hangul")
