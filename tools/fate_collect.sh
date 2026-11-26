#!/bin/sh
# 24차 미래 보기: 다운로드 폴더의 tod_<죄종>_{key,action_###,loop_###}.png 를 art_src/fate_loop/<죄종>/ 로 옮긴다 (중복 '(1)' 파일은 지움).
# sh tools/fate_collect.sh [다운로드 폴더]
ROOT=$(cd "$(dirname "$0")/.." && pwd)
DL=${1:-$HOME/Downloads}
rm -f "$DL"/tod_*"("*").png"
for f in "$DL"/tod_*.png; do
  [ -f "$f" ] || continue
  b=$(basename "$f" .png); rest=${b#tod_}; sin=${rest%%_*}; name=${rest#*_}
  mkdir -p "$ROOT/art_src/fate_loop/$sin"
  mv "$f" "$ROOT/art_src/fate_loop/$sin/$name.png" && echo "$sin/$name.png"
done
