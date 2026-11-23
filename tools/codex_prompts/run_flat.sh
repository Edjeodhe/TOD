#!/bin/sh
# 1) 새 파츠 아틀라스  2) 그 아틀라스를 참고로 회피 3프레임
cd /c/Project/TOD
codex exec -s workspace-write --skip-git-repo-check -i assets/raw/hero_quarter_atlas.png - < tools/codex_prompts/hero_flat.txt > tools/codex_prompts/hero_flat.log 2>&1
if [ -f assets/raw/hero_flat_atlas.png ]; then
  codex exec -s workspace-write --skip-git-repo-check -i assets/raw/hero_flat_atlas.png - < tools/codex_prompts/dodge_flat.txt > tools/codex_prompts/dodge_flat.log 2>&1
fi
ls -la assets/raw/hero_flat_*
