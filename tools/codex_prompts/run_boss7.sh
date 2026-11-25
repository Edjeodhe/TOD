#!/bin/sh
# 23차-boss7: 칠죄종 보스 7종 파츠 시트 (sh tools/codex_prompts/run_boss7.sh [죄종...]) — 없으면 7종 모두.
# 이미 있는 그림은 건너뜀. 한도에 걸리면 QUOTA 를 찍고 멈춤. 실패하면 한 번 다시 시도.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/5449c1f0f26f1f98/codex.exe"
P=tools/codex_prompts

ref() {
  case "$1" in
    greed|gluttony) echo art_src/raw/enemy_stalker_parts.png ;;
    wrath) echo art_src/raw/enemy_charger_parts.png ;;
    *) echo art_src/raw/enemy_boss_parts.png ;;
  esac
}

run() {
  out=art_src/raw/boss7_$1.png
  for try in 1 2; do
    if [ -f "$out" ]; then echo "done $1"; return 0; fi
    "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$(ref $1)" - < $P/boss7_$1.txt > $P/boss7_$1.log 2>&1
    if [ -f "$out" ]; then echo "done $1"; return 0; fi
    if grep -qiE "usage limit|rate limit|quota|exceeded" $P/boss7_$1.log; then echo "QUOTA $1"; exit 3; fi
  done
  echo "FAIL $1"
}

LIST="$*"
[ -z "$LIST" ] && LIST="greed wrath pride envy lust gluttony sloth"
for s in $LIST; do run $s; done
echo ALLDONE
