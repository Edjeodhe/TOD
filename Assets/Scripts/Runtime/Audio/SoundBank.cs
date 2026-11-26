// 효과음 (웹판 main.js beep). 오실레이터 합성음을 시작할 때 AudioClip 으로 만들어 둔다.
// 소리 목록과 주파수·길이·볼륨·파형은 GameDatabase.sounds 에 있다. sample 이 지정된 소리는 합성 대신 그 샘플을 쓴다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class SoundBank : MonoBehaviour
    {
        public AudioSource source;
        public int sampleRate;

        readonly Dictionary<string, (AudioClip clip, float volume)> clips = new();
        public bool Enabled { get; set; }
        public float Volume { get; set; } = 1;   // 24차 환경설정 효과음 음량 (전체 음량은 AudioListener.volume)

        public void Init(SynthSound[] sounds)
        {
            foreach (SynthSound s in sounds)
            {
                AudioClip clip = s.sample != null ? s.sample : Synthesize(s);
                clips[s.id] = (clip, s.sample != null ? s.volume : 1);
            }
        }

        // 지수 램프 주파수(from → to), 15ms 어택 후 지수 감쇠 — Web Audio 스케줄과 같은 모양.
        AudioClip Synthesize(SynthSound s)
        {
            int length = Mathf.CeilToInt((s.duration + .01f) * sampleRate);
            var data = new float[length];
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float u = Mathf.Clamp01(t / s.duration);
                float freq = s.from * Mathf.Pow(s.to / s.from, u);
                phase += freq / sampleRate;
                float p = (float)(phase - System.Math.Floor(phase));
                float wave = s.wave switch
                {
                    WaveShape.Triangle => 1 - 4 * Mathf.Abs(p - .5f),
                    WaveShape.Sawtooth => 2 * p - 1,
                    WaveShape.Square => p < .5f ? 1 : -1,
                    _ => Mathf.Sin(p * Mathf.PI * 2),
                };
                const float floor = .0001f;
                float gain = t < .015f
                    ? floor * Mathf.Pow(s.volume / floor, t / .015f)
                    : t < s.duration ? s.volume * Mathf.Pow(floor / s.volume, (t - .015f) / Mathf.Max(.001f, s.duration - .015f)) : 0;
                data[i] = wave * gain;
            }
            AudioClip clip = AudioClip.Create(s.id, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(string id, float power = 1)
        {
            if (!Enabled || Volume <= 0 || !clips.TryGetValue(id, out var entry)) return;
            source.PlayOneShot(entry.clip, entry.volume * Mathf.Min(1.5f, power) * Volume);
        }
    }
}
