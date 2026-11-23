#!/bin/sh
# 3차 그림: 종이 UI 키트 + 몬스터 4종 파츠 (참고 이미지 = 지금 몬스터 통그림)
cd /c/Project/TOD
codex exec -s workspace-write --skip-git-repo-check - < tools/codex_prompts/ui_kit.txt > tools/codex_prompts/ui_kit.log 2>&1
echo "ui_kit exit=$? $(ls art_src/raw/ui_kit.png 2>/dev/null)"
for n in stalker charger shooter boss; do
  codex exec -s workspace-write --skip-git-repo-check -i Assets/sprites/enemy_$n.png - < tools/codex_prompts/enemy_${n}_parts.txt > tools/codex_prompts/enemy_${n}_parts.log 2>&1
  echo "$n exit=$? $(ls art_src/raw/enemy_${n}_parts.png 2>/dev/null)"
done
