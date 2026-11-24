#!/bin/sh
# 20차 그림: 재봉실 시설 3종(어둡게, 비네트) + 모이라 점성구(어둡게, 비네트). 이미 있는 그림은 건너뜀. 한도에 걸리면 QUOTA 를 찍고 멈춤.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/5449c1f0f26f1f98/codex.exe"
P=tools/codex_prompts
run() { # name ref1 ref2
  out=art_src/raw/$1.png
  if [ -f "$out" ]; then echo "skip $1"; return 0; fi
  "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$2" -i "$3" - < $P/$1.txt > $P/$1.log 2>&1
  if [ -f "$out" ]; then echo "done $1"; return 0; fi
  if grep -qiE "usage limit|rate limit|quota|exceeded|try again" $P/$1.log; then echo "QUOTA $1"; exit 3; fi
  echo "FAIL $1"
}
run "$1" "$2" "$3"
