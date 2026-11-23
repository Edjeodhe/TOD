#!/bin/sh
# 16차 그림: 상인 자세 → 속성 몬스터 파츠(화염부터) . 이미 있는 그림은 건너뜀. 한도에 걸리면 QUOTA 를 찍고 멈춤.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/31fb36cfc4b38f05/codex.exe"
P=tools/codex_prompts

run() { # name ref
  out=art_src/raw/$1.png
  if [ -f "$out" ]; then echo "skip $1"; return 0; fi
  if [ -n "$2" ]; then
    "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$2" - < $P/$1.txt > $P/$1.log 2>&1
  else
    "$CODEX" exec -s workspace-write --skip-git-repo-check - < $P/$1.txt > $P/$1.log 2>&1
  fi
  if [ -f "$out" ]; then echo "done $1"; return 0; fi
  if grep -qiE "usage limit|rate limit|quota|exceeded|try again" $P/$1.log; then echo "QUOTA $1"; exit 3; fi
  echo "FAIL $1"
}

run merchant_poses Assets/Art/Generated/merchant.png
for e in fire ice lightning poison wind light dark blood; do
  for t in stalker charger shooter boss; do
    run enemy_${t}_parts_$e art_src/raw/enemy_${t}_parts.png
  done
done
echo ALLDONE
