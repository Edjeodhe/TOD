"""18차 일기 핏자국 굽기: art_src/raw/blood_stains.png (초록 키, 3×2) → Assets/UI/Paper/blood_1~6.png (긴 변 256).

python tools/bake_blood_stains.py
초록 키 처리와 칸 나누기는 bake_hub_art.py 의 함수를 그대로 쓴다.
"""
from pathlib import Path

_src = (Path(__file__).with_name("bake_hub_art.py")).read_text(encoding="utf-8")
exec(_src[:_src.index('if (RAW / "hub_facilities.png")')])   # key_green, grid, save, RAW, OUT

OUT = ROOT / "Assets/UI/Paper"   # save() 가 쓰는 출력 폴더를 바꾼다
for i, (col, a) in enumerate(grid(RAW / "blood_stains.png", 3, 2, merge=40)):
    save(col, a, f"blood_{i + 1}", 256)
