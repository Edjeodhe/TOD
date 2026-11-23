#!/bin/sh
# 16차 그림 2부: 속성 정적 장식 8장 (run_16.sh 다음에)
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/31fb36cfc4b38f05/codex.exe"
P=tools/codex_prompts
for e in fire ice lightning poison wind light dark blood; do
  out=art_src/raw/decor_$e.png
  if [ -f "$out" ]; then echo "skip decor_$e"; continue; fi
  "$CODEX" exec -s workspace-write --skip-git-repo-check -i Assets/sprites/floor.png - < $P/decor_$e.txt > $P/decor_$e.log 2>&1
  if [ -f "$out" ]; then echo "done decor_$e"; continue; fi
  if grep -qiE "usage limit|rate limit|quota|exceeded|try again" $P/decor_$e.log; then echo "QUOTA decor_$e"; exit 3; fi
  echo "FAIL decor_$e"
done
echo ALLDONE
