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
  enemies_a)
    for s in pride greed lust envy gluttony wrath sloth; do
      for t in stalker charger; do run enemy_${t}_parts_$s art_src/raw/enemy_${t}_parts.png; done
    done ;;
  enemies_b)
    for s in pride greed lust envy gluttony wrath sloth; do
      for t in shooter boss; do run enemy_${t}_parts_$s art_src/raw/enemy_${t}_parts.png; done
    done ;;
  enemies_c)   # 뒤에서부터 (다른 줄과 끝에서 만나면 이미 있는 그림은 건너뜀)
    for s in sloth wrath gluttony envy; do
      for t in boss shooter charger stalker; do run enemy_${t}_parts_$s art_src/raw/enemy_${t}_parts.png; done
    done ;;
esac
echo ALLDONE $1
