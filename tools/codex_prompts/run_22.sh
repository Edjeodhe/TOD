#!/bin/sh
# 22차 그림 (sin-art): sh tools/codex_prompts/run_22.sh <icons|decor|enemies_a|enemies_b>
# 이미 있는 그림은 건너뜀. 한도에 걸리면 QUOTA 를 찍고 멈춤. 실패하면 한 번 다시 시도.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/31fb36cfc4b38f05/codex.exe"
P=tools/codex_prompts

run() { # name ref
  out=art_src/raw/$1.png
  for try in 1 2; do
    if [ -f "$out" ]; then echo "done $1"; return 0; fi
    "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$2" - < $P/$1.txt > $P/$1.log 2>&1
    if [ -f "$out" ]; then echo "done $1"; return 0; fi
    if grep -qiE "usage limit|rate limit|quota|exceeded" $P/$1.log; then echo "QUOTA $1"; exit 3; fi
  done
  echo "FAIL $1"
}

case "$1" in
  icons)
    for k in a b c d; do run sin_icons_$k art_src/raw/item_icons_v3.png; done
    run sin_patterns art_src/raw/pattern_icons.png ;;
  decor)
    for s in pride greed lust envy gluttony wrath sloth; do run decor_$s Assets/sprites/floor.png; done ;;
  # 2판(죄종 느낌 강화): 세 줄이 겹치지 않게 나눔. 없는 그림 먼저, 다시 그리는 탐욕, 색욕은 뒤 (옛 그림은 art_src/raw/sin_v1/)
  enemies_a)
    for s in wrath gluttony sloth; do
      for t in stalker charger; do run enemy_${t}_parts_$s art_src/raw/enemy_${t}_parts.png; done
    done ;;
  enemies_b)
    for s in wrath gluttony lust; do
      for t in shooter boss; do run enemy_${t}_parts_$s art_src/raw/enemy_${t}_parts.png; done
    done ;;
  enemies_c)
    for t in stalker charger shooter boss; do run enemy_${t}_parts_greed art_src/raw/enemy_${t}_parts.png; done
    for t in stalker charger; do run enemy_${t}_parts_lust art_src/raw/enemy_${t}_parts.png; done ;;
  enemies_one)  # sh run_22.sh enemies_one <종류> <죄종>
    run enemy_$2_parts_$3 art_src/raw/enemy_$2_parts.png ;;
esac
echo ALLDONE $1
