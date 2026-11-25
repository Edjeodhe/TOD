# 24차 미래 보기 구슬 속 반복 그림: art_src/fate_loop/<죄종>/frame_###.png (ChatGPT 원본 1254²) → Assets/Art/Fate/<죄종>/frame_###.png (768², RGB)
# 쓰는 법: python tools/bake_fate_loop.py [죄종 ...]   (없으면 원본 폴더가 있는 죄종 모두)
# 원본에 없는 번호는 만들지 않는다. Unity 연결은 MCP 로 FateOrbView.loops 에 있는 파일을 차례대로 넣는다.
import os, sys, glob
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SIZE = 768  # 줌 때 구슬 유리 지름이 1080p 화면에서 약 750px

def bake(sin):
    src = os.path.join(ROOT, 'art_src', 'fate_loop', sin)
    dst = os.path.join(ROOT, 'Assets', 'Art', 'Fate', sin)
    os.makedirs(dst, exist_ok=True)
    files = sorted(glob.glob(os.path.join(src, 'frame_[0-9][0-9][0-9].png')))
    for f in files:
        im = Image.open(f).convert('RGB')
        w, h = im.size
        s = min(w, h)  # 정사각이 아니면 가운데를 자름
        im = im.crop(((w - s) // 2, (h - s) // 2, (w - s) // 2 + s, (h - s) // 2 + s)).resize((SIZE, SIZE), Image.LANCZOS)
        im.save(os.path.join(dst, os.path.basename(f)), optimize=True)
    print(sin, len(files), [os.path.basename(f)[6:9] for f in files])

if __name__ == '__main__':
    sins = sys.argv[1:] or sorted(d for d in os.listdir(os.path.join(ROOT, 'art_src', 'fate_loop')) if os.path.isdir(os.path.join(ROOT, 'art_src', 'fate_loop', d)))
    for s in sins:
        bake(s)
