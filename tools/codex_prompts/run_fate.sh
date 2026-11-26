#!/bin/sh
# 24차 미래 보기: 남은 셀 애니 장을 Codex CLI 로 뽑는다 (ChatGPT 웹 하루 한도 대신).
#   sh tools/codex_prompts/run_fate.sh <죄종> action|loop [시작] [끝]
# action = key.png 를 참고 그림으로, loop = action_016.png 를 참고 그림으로. 프롬프트는 tools/fate_loop_prompts/sins_v3.json.
# 결과 art_src/fate_loop/<죄종>/<action|loop>_###.png. 이미 있는 장은 건너뜀. 한도에 걸리면 QUOTA 를 찍고 멈춤. 실패하면 한 번 다시.
cd /c/Project/TOD
CODEX="$LOCALAPPDATA/OpenAI/Codex/bin/5449c1f0f26f1f98/codex.exe"
SIN=$1; STAGE=$2; FROM=${3:-1}; TO=${4:-16}
[ "$STAGE" = loop ] && [ -z "$4" ] && TO=12
D=art_src/fate_loop/$SIN
LOG=tools/codex_prompts/fate_logs; mkdir -p $LOG
if [ "$STAGE" = action ]; then REF=$D/key.png; else REF=$D/action_016.png; fi
k=$FROM
while [ $k -le $TO ]; do
  n=$(printf %03d $k); out=$D/${STAGE}_$n.png
  for try in 1 2; do
    [ -f "$out" ] && break
    python tools/fate_prompt_codex.py "$SIN" "$STAGE" "$k" "$out" > $LOG/${SIN}_${STAGE}_$n.txt
    "$CODEX" exec -s workspace-write --skip-git-repo-check -i "$REF" - < $LOG/${SIN}_${STAGE}_$n.txt > $LOG/${SIN}_${STAGE}_$n.log 2>&1
    [ -f "$out" ] && break
    if grep -qiE "usage limit|rate limit|quota|exceeded" $LOG/${SIN}_${STAGE}_$n.log; then echo "QUOTA $SIN $STAGE $n"; exit 3; fi
  done
  if [ -f "$out" ]; then echo "done $SIN $STAGE $n"; else echo "FAIL $SIN $STAGE $n"; fi
  k=$((k+1))
done
echo "ALLDONE $SIN $STAGE"
