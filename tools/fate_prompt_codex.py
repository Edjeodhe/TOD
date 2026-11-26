# 24차: Codex CLI 용 미래 보기 셀 애니 한 장 프롬프트 (sins_v3.json 틀). tools/codex_prompts/run_fate.sh 가 부름.
# python tools/fate_prompt_codex.py <죄종> action|loop <번호> <저장 경로>
import json, os, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
d = json.load(open(os.path.join(ROOT, 'tools', 'fate_loop_prompts', 'sins_v3.json'), encoding='utf-8'))
sin, stage, k, out = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4]
s = d['sins'][sin]
if stage == 'action':
    beat = s['action'][k - 1]
    body = d['action_tpl'].format(k=k, beat=beat[0].upper() + beat[1:])
else:
    body = d['loop_tpl'].format(k=k, ambient=s['ambient'], phase=d['loop_phases'][k - 1])
sys.stdout.reconfigure(encoding='utf-8')
print('Use your built-in image generation tool to edit the attached image (do not write code to draw it). '
      'Then copy the final generated PNG file into the current working directory at the path given below (create folders if needed). '
      'Do not modify anything else. Reply with only the saved path.')
print('Save path: ' + out)
print('Size: square 1024x1024.')
print('Style to keep: ' + d['style'])
print(body)
