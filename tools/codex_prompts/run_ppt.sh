#!/bin/sh
# PPT 삽화: 세계관 장면, 아버지의 일기. 이미 있는 그림은 건너뜀.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/5449c1f0f26f1f98/codex.exe"
P=tools/codex_prompts
run() { # name ref1 [ref2]
  out=art_src/raw/$1.png
  if [ -f "$out" ]; then echo "skip $1"; return 0; fi
  if [ -n "$3" ]; then "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$2" -i "$3" - < $P/$1.txt > $P/$1.log 2>&1
  else "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$2" - < $P/$1.txt > $P/$1.log 2>&1; fi
  if [ -f "$out" ]; then echo "done $1"; return 0; fi
  if grep -qiE "usage limit|rate limit|quota|exceeded|try again" $P/$1.log; then echo "QUOTA $1"; return 3; fi
  echo "FAIL $1"
}
run "$@"
