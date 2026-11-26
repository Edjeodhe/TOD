# 24차: sins_v3.json 에서 ChatGPT 페이지 자동 실행(todRun)용 [{name, text}] JSON 을 찍는다.
# python tools/fate_prompt_js.py <죄종> key|action|loop [시작번호]
import json, os, sys
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
d = json.load(open(os.path.join(ROOT, 'tools', 'fate_loop_prompts', 'sins_v3.json'), encoding='utf-8'))
sin, stage = sys.argv[1], sys.argv[2]
start = int(sys.argv[3]) if len(sys.argv) > 3 else 1
s = d['sins'][sin]
items = []
if stage == 'key':
    items.append({'name': f'tod_{sin}_key.png', 'text': 'Create an image. ' + d['style'] + ' ' + s['key']})
elif stage == 'action':
    for k, beat in enumerate(s['action'], 1):
        if k >= start: items.append({'name': f'tod_{sin}_action_{k:03d}.png', 'text': d['action_tpl'].format(k=k, beat=beat[0].upper() + beat[1:])})
else:
    for k, ph in enumerate(d['loop_phases'], 1):
        if k >= start: items.append({'name': f'tod_{sin}_loop_{k:03d}.png', 'text': d['loop_tpl'].format(k=k, ambient=s['ambient'], phase=ph)})
print(json.dumps(items, ensure_ascii=False))
