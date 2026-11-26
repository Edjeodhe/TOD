# 24차-hit-audio: 당기기 타격음 겹(WAV) 만들기 + 파형 그림.
#   python tools/make_hit_audio.py [C:/Project/TOD]
# 결과: Assets/Audio/Hit/hit_*.wav (44.1kHz mono 16bit, 꼭짓점 0.95로 맞춤),
#       Temp/Shots/w24_hitaudio_waves.png (파형 + 소리 크기 곡선)
# 얼마나 크게·높게 낼지는 게임 데이터(GameDatabase.sounds 의 volume, hitAudio.cues)에서 정한다.
# 여기서는 소리의 '결'만 만든다. seed 가 고정이라 다시 돌려도 같은 파일이 나온다.
import os
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw

SR = 44100
ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Audio", "Hit")
SHOT = os.path.join(ROOT, "Temp", "Shots")
rng = np.random.default_rng(2411)


def t_axis(sec):
    return np.arange(int(sec * SR)) / SR


def env(t, attack, decay):
    """선형 어택 뒤 지수 감쇠 (decay = 1/e 까지 걸리는 초)."""
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    return a * np.exp(-np.maximum(t - attack, 0) / decay)


def sweep(t, f0, f1, curve):
    """f0 → f1 로 지수 곡선으로 내려가는 주파수의 위상."""
    f = f1 + (f0 - f1) * np.exp(-t / curve)
    return 2 * np.pi * np.cumsum(f) / SR


def biquad(x, kind, freq, q):
    """RBJ 바이쿼드 (lowpass / highpass / bandpass). freq 는 상수 또는 배열(샘플마다 바뀜)."""
    n = len(x)
    freq = np.broadcast_to(np.asarray(freq, dtype=float), (n,))
    y = np.zeros(n)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(n):
        w = 2 * np.pi * min(freq[i], SR * .45) / SR
        cw, sw = np.cos(w), np.sin(w)
        alpha = sw / (2 * q)
        if kind == "low":
            b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
        elif kind == "high":
            b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
        else:
            b0, b1, b2 = alpha, 0.0, -alpha
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
        yi = (b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x[i], y1, yi
        y[i] = yi
    return y


def noise(n):
    return rng.uniform(-1, 1, n)


def fade_tail(x, ms=6):
    k = int(SR * ms / 1000)
    x[-k:] *= np.linspace(1, 0, k)
    return x


def normalize(x, peak=.95):
    x = biquad(x, "high", 28, .7)   # 낮은 소리의 치우침(직류) 걷어내기
    return fade_tail(x / (np.max(np.abs(x)) + 1e-9) * peak)


# ---------------------------------------------------------------- 겹 1: 몸에 맞는 '쿵'
def thud():
    t = t_axis(.3)
    ph = sweep(t, 150, 46, .035)
    body = np.sin(ph) * env(t, .002, .06)
    sub = np.sin(ph * .5) * env(t, .004, .07) * .5   # 한 옥타브 아래를 같은 위상으로 (맥놀이로 꼬리가 다시 커지지 않게)
    click = biquad(noise(len(t)), "low", 2400, .7) * env(t, .0005, .006) * .9   # 맞닿는 순간의 딱
    paper = biquad(noise(len(t)), "band", 520, 1.2) * env(t, .001, .03) * .5    # 종이 몸통이 눌리는 퍽
    return normalize(np.tanh(2.2 * (body + sub + click + paper)))


# ---------------------------------------------------------------- 겹 2a: 천 찢기
def tear():
    t = t_axis(.2)
    n = len(t)
    # 찢어지는 결: 촘촘한 알갱이(올이 하나씩 끊김). 앞은 빽빽하고 뒤로 갈수록 성기게.
    grains = np.zeros(n)
    pos = 0.0
    while pos < .17:
        i = int(pos * SR)
        width = int(SR * rng.uniform(.0008, .0025))
        grains[i:i + width] += rng.uniform(.4, 1) * np.hanning(width)[: len(grains[i:i + width])]
        pos += rng.uniform(.0025, .006) * (1 + pos * 14)
    center = 4200 * np.exp(-t / .12) + 1500   # 찢길수록 결이 낮아짐
    body = biquad(noise(n), "band", center, 1.4) * grains
    hiss = biquad(noise(n), "high", 3000, .7) * env(t, .002, .05) * .35
    return normalize(body * env(t, .003, .09) * 1.6 + hiss)


# ---------------------------------------------------------------- 겹 2b: 실 튕김 (Karplus-Strong)
def twang():
    t = t_axis(.42)
    n = len(t)
    f0 = 220.0
    period = int(SR / f0)
    buf = biquad(noise(period * 2), "low", 5000, .7)[:period]
    out = np.zeros(n)
    line = list(buf)
    for i in range(n):
        # 줄이 풀리며 살짝 내려감: 지연 길이를 조금씩 늘린다
        a = line[0]
        b = line[1] if len(line) > 1 else a
        v = .497 * (a + b)
        out[i] = a
        line.pop(0)
        line.append(v)
        if i % 900 == 0 and len(line) < period * 1.06:
            line.append(v)
    out = biquad(out, "high", 120, .7)
    snap = biquad(noise(n), "high", 2500, .8) * env(t, .0004, .004) * .6
    return normalize(out * env(t, .001, .16) + snap)


# ---------------------------------------------------------------- 강조: 처치 = 가위 싹둑
def snip():
    t = t_axis(.16)
    n = len(t)
    x = np.zeros(n)
    for start, pitch, amp in ((0.0, 1.0, 1.0), (.038, 1.18, .8)):
        tt = np.maximum(t - start, 0) * (t >= start)
        click = biquad(noise(n), "band", 5200 * pitch, 2.5) * env(tt, .0003, .008) * (t >= start)
        ring = (np.sin(2 * np.pi * 3150 * pitch * tt) + .6 * np.sin(2 * np.pi * 4870 * pitch * tt)) * env(tt, .0005, .02) * (t >= start)
        x += amp * (click * 1.2 + ring * .5)
    blade = biquad(noise(n), "high", 6000, .7) * env(t, .01, .04) * .25   # 날이 스치는 쉭
    return normalize(x + blade)


# ---------------------------------------------------------------- 강조: 치명타 = 바늘 반짝 핑
def crit():
    t = t_axis(.5)
    partials = ((1760, 1.0, .18), (2794, .6, .12), (4430, .45, .07), (6040, .25, .05))
    x = sum(a * np.sin(2 * np.pi * f * t) * env(t, .0008, d) for f, a, d in partials)
    strike = biquad(noise(len(t)), "high", 4000, .7) * env(t, .0003, .006)
    return normalize(x + strike * .8)


# ---------------------------------------------------------------- 강조: 결박·여러 타 = 깊은 둥
def boom():
    t = t_axis(.7)
    body = np.sin(sweep(t, 110, 30, .07)) * env(t, .003, .14)
    rumble = biquad(noise(len(t)), "low", 260, .8) * env(t, .01, .14) * 1.4
    crack = biquad(noise(len(t)), "band", 1100, .9) * env(t, .0006, .012) * .8
    return normalize(np.tanh(1.8 * (body + rumble + crack)))


SOUNDS = {
    "hit_thud": thud,
    "hit_tear": tear,
    "hit_twang": twang,
    "hit_snip": snip,
    "hit_crit": crit,
    "hit_boom": boom,
}


def write_wav(path, x):
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def draw(waves, path):
    """소리마다 한 줄: 파형(회색 띠) + RMS 소리 크기(검은 선). 가로는 0~0.6초 공통."""
    width, row, pad = 1200, 150, 40
    img = Image.new("RGB", (width, row * len(waves) + pad), (246, 240, 228))
    d = ImageDraw.Draw(img)
    span = .6
    for k, (name, x) in enumerate(waves.items()):
        top = pad // 2 + k * row
        mid = top + row // 2
        d.line([(110, mid), (width - 20, mid)], fill=(200, 190, 175))
        for sec in np.arange(0, span + 1e-6, .1):
            px = 110 + sec / span * (width - 130)
            d.line([(px, top + 8), (px, top + row - 8)], fill=(225, 215, 200))
        cols = width - 130
        per = span * SR / cols
        for c in range(cols):
            a, b = int(c * per), int((c + 1) * per)
            seg = x[a:b]
            if len(seg) == 0:
                break
            lo, hi = seg.min(), seg.max()
            d.line([(110 + c, mid - hi * (row / 2 - 12)), (110 + c, mid - lo * (row / 2 - 12))], fill=(150, 70, 60))
            rms = np.sqrt(np.mean(seg ** 2))
            if c:
                d.line([(110 + c - 1, prev), (110 + c, mid - rms * (row / 2 - 12))], fill=(30, 30, 30), width=2)
            prev = mid - rms * (row / 2 - 12)
        d.text((10, mid - 14), name, fill=(40, 30, 30))
        d.text((10, mid + 2), f"{len(x) / SR:.2f}s", fill=(110, 100, 90))
    for sec in np.arange(0, span + 1e-6, .1):
        d.text((110 + sec / span * (width - 130) - 8, img.height - 16), f"{sec:.1f}", fill=(110, 100, 90))
    img.save(path)


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(SHOT, exist_ok=True)
    waves = {}
    for name, make in SOUNDS.items():
        x = make()
        waves[name] = x
        write_wav(os.path.join(OUT, name + ".wav"), x)
        print(f"{name}: {len(x) / SR:.3f}s peak {np.max(np.abs(x)):.2f} rms {np.sqrt(np.mean(x ** 2)):.3f}")
    draw(waves, os.path.join(SHOT, "w24_hitaudio_waves.png"))
    print("saved", OUT)


if __name__ == "__main__":
    main()
