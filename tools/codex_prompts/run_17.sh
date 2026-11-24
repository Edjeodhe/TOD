#!/bin/sh
# 17차 다락 방 그림: 다락 배경, 소품 2종(모이라의 실궤, 내려가는 뚜껑문), 아버지의 도안 아이콘 5종.
cd /c/Project/TOD
P=tools/codex_prompts
run() { # name ref
  out=art_src/raw/$1.png
  if [ -f "$out" ]; then echo "skip $1"; return 0; fi
  codex exec -s workspace-write --skip-git-repo-check -i "$2" - < $P/$1.txt > $P/$1.log 2>&1
  if [ -f "$out" ]; then echo "done $1"; return 0; fi
  if grep -qiE "usage limit|rate limit|quota|exceeded|try again" $P/$1.log; then echo "QUOTA $1"; exit 3; fi
  echo "FAIL $1"
}
run attic_props Assets/sprites/floor.png
run attic_room Assets/sprites/floor.png
run pattern_icons Assets/sprites/item_bleeding_thimble.png
echo ALLDONE
