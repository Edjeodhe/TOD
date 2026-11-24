"""17차 다락 방 그림 굽기: Codex 시트(art_src/raw) → Assets/sprites.

python tools/bake_attic_art.py

- attic_room.png (불투명): 다락 방 배경 → attic_room.png (그대로 복사). 19차부터 attic_room2.png(모이라 자리에 맞춘 방)가 있으면 그것
- attic_props.png (초록 키, 2×1): 모이라의 실궤, 뚜껑문 → hub_attic_chest / hub_attic_exit (아래 가운데 피벗)
- moira.png (초록 키, 1장): 모이라 → hub_attic_moira (긴 변 640, 아래 가운데 피벗)
- pattern_icons.png (초록 키, 5×1): 아버지의 도안 아이콘 → item_pattern_1~5 (긴 변 256)
초록 키 처리와 칸 나누기는 bake_hub_art.py 의 함수를 그대로 쓴다.
"""
from pathlib import Path

from PIL import Image

_src = (Path(__file__).with_name("bake_hub_art.py")).read_text(encoding="utf-8")
exec(_src[:_src.index('if (RAW / "hub_facilities.png")')])   # key_green, grid, save, RAW, OUT 만 가져온다

# 19차: attic_room2 = 모이라 자리(가운데 러그, 뒤 달 창, 양옆 촛대, 보랏빛 웅덩이)에 맞춰 다시 그린 방. 있으면 이것을 쓴다
room = RAW / "attic_room2.png" if (RAW / "attic_room2.png").exists() else RAW / "attic_room.png"
if room.exists():
    Image.open(room).convert("RGB").save(OUT / "attic_room.png")
    print("wrote attic_room from", room.name)
if (RAW / "attic_props.png").exists():
    for (col, a), name in zip(grid(RAW / "attic_props.png", 2, 1), ["attic_chest", "attic_exit"]):
        save(col, a, f"hub_{name}", 512)
if (RAW / "pattern_icons.png").exists():
    for i, (col, a) in enumerate(grid(RAW / "pattern_icons.png", 5, 1)):
        save(col, a, f"item_pattern_{i + 1}", 256)
# 모이라 (초록 키, 1장): 탁자 앞에 앉아 점성구를 읽는 예견가 → hub_attic_moira (아래 가운데 피벗). 실궤 자리를 대신한다.
if (RAW / "moira.png").exists():
    for col, a in grid(RAW / "moira.png", 1, 1):
        save(col, a, "hub_attic_moira", 640)
