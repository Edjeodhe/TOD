#!/bin/sh
# 15차 허브 그림: 재봉실 배경 + 시설 4종 시트. 이미 있는 그림은 건너뜀. 한도에 걸리면 QUOTA 를 찍고 멈춤.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/31fb36cfc4b38f05/codex.exe"
P=tools/codex_prompts
run() { # name ref
  out=art_src/raw/$1.png
  if [ -f "$out" ]; then echo "skip $1"; return 0; fi
  "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$2" - < $P/$1.txt > $P/$1.log 2>&1
  if [ -f "$out" ]; then echo "done $1"; return 0; fi
  if grep -qiE "usage limit|rate limit|quota|exceeded|try again" $P/$1.log; then echo "QUOTA $1"; exit 3; fi
  echo "FAIL $1"
}
run hub_facilities Assets/sprites/floor.png
run hub_room Assets/sprites/floor.png
echo ALLDONE
