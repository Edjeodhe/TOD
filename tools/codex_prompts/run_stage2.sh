#!/bin/sh
# 2차 개편 추가 그림: 스탯 아이콘 8, 아이템 아이콘 12, 쉬어가기 탑, 경험치 구
cd /c/Project/TOD
mkdir -p art_src/raw
for n in stat_icons item_icons tower xp_orb; do
  codex exec -s workspace-write --skip-git-repo-check - < tools/codex_prompts/$n.txt > tools/codex_prompts/$n.log 2>&1
  echo "$n exit=$? file=$(ls art_src/raw/$n.png 2>/dev/null)"
done
