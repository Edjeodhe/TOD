// 효과음 (웹판 main.js beep). 오실레이터 합성음을 시작할 때 AudioClip 으로 만들어 둔다.
// 소리 목록과 주파수·길이·볼륨·파형은 GameDatabase.sounds 에 있다. sample 이 지정된 소리는 합성 대신 그 샘플을 쓴다.
// 24차-hit-audio: GameDatabase.hitAudio 에 겹(cue)이 있는 이벤트는 그 겹들을 피치·볼륨을 흔들어 voices(MCP 로 만든 AudioSource 풀)로 낸다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class SoundBank : MonoBehaviour
    {
        public AudioSource source;
        public AudioSource[] voices;   // 24차-hit-audio: 피치를 바꿔 낼 소스 풀 (GameController/SoundVoices 아래, MCP 로 만듦)
        public int sampleRate;
        public bool logCues;           // 검증용: 겹을 낼 때마다 콘솔에 적음 (평소 꺼짐)
        HitAudioConfig cueTable;
        int nextVoice;

        readonly Dictionary<string, (AudioClip clip, float volume)> clips = new();
        public bool Enabled { get; set; }
        public float Volume { get; set; } = 1;   // 24차 환경설정 효과음 음량 (전체 음량은 AudioListener.volume)

        public void Init(SynthSound[] sounds, HitAudioConfig cues = null)
        {
            cueTable = cues;
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

        // 게임 이벤트 하나: 겹 표에 있으면 겹들을, 없으면 예전처럼 같은 이름의 소리 하나.
        public void PlayEvent(string type, int count, float power = 1)
        {
            if (!SoundCues.Has(cueTable, type)) { Play(type, power); return; }
            if (!Enabled || Volume <= 0) return;
            List<SoundShot> shots = SoundCues.Plan(cueTable, type, count, () => Random.value);
            foreach (SoundShot shot in shots) PlayShot(shot);
            if (logCues) Debug.Log($"[hit-audio] {type} x{count}: " + string.Join(", ", shots.ConvertAll(s => $"{s.sound} v{s.volume:0.00} p{s.pitch:0.00} +{s.delay * 1000:0}ms")));
        }

        void PlayShot(SoundShot shot)
        {
            if (!clips.TryGetValue(shot.sound, out var entry)) return;
            if (voices == null || voices.Length == 0)
            {
                source.PlayOneShot(entry.clip, entry.volume * shot.volume * Volume);   // 풀이 없으면 피치·지연 없이
                return;
            }
            // 쉬는 소스를 먼저, 다 바쁘면 차례로 (가장 오래된 것을 끊음)
            AudioSource voice = null;
            for (int i = 0; i < voices.Length && voice == null; i++)
            {
                AudioSource v = voices[(nextVoice + i) % voices.Length];
                if (v != null && !v.isPlaying) voice = v;
            }
            voice ??= voices[nextVoice % voices.Length];
            nextVoice = (nextVoice + 1) % voices.Length;
            if (voice == null) return;
            voice.Stop();
            voice.clip = entry.clip;
            voice.pitch = shot.pitch;
            voice.volume = Mathf.Clamp01(entry.volume * shot.volume * Volume);
            if (shot.delay > 0) voice.PlayScheduled(AudioSettings.dspTime + shot.delay);
            else voice.Play();
        }
    }
}
