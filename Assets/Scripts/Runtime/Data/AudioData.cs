// 24차-hit-audio: 이벤트 하나에 소리를 여러 겹 얹는 표 (당기기 타격음 겹치기).
// 값은 씬 GameDatabase.hitAudio 에 있다. 코드에는 빈 필드와 계산만 둔다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    // 이벤트(trigger)가 나오면 sound 를 한 겹 낸다. count = 이벤트 세기(당기기 타수, 처치 수, 치명타 수).
    [Serializable] public class SoundCue
    {
        public string trigger;          // GameEvent.type (hit, pullkill, crit, bind …)
        public string sound;            // SynthSound id (샘플 또는 합성음)
        public int minCount;            // 이 세기부터 겹이 들어옴 (0·1 = 언제나)
        public float volume;            // 기본 볼륨 배율
        public float volumePerCount;    // 세기 1 늘 때마다 더함
        public float volumeMax;         // 볼륨 배율 상한 (0 = 상한 없음)
        public float volumeJitter;      // ± 비율 (0.15 = ±15%)
        public float pitch;             // 기본 피치 (0 = 1)
        public float pitchPerCount;     // 세기 1 늘 때마다 더함 (음수 = 많이 맞힐수록 묵직)
        public float pitchJitter;       // ± 무작위
        public float delay;             // 이벤트 뒤 첫 소리까지 초
        public float copiesPerCount;    // 세기 1 늘 때마다 더하는 복사본 수 (0.5 = 2타마다 한 겹)
        public int maxCopies;           // 복사본 상한 (0·1 = 하나만)
        public float copyGap;           // 복사본 사이 초 (± 30% 흔들림)
    }

    [Serializable] public class HitAudioConfig
    {
        public SoundCue[] cues;
        public int countCap;            // 세기 상한 (이 위로는 똑같이 둔다, 0 = 없음)
    }

    public struct SoundShot { public string sound; public float volume, pitch, delay; }

    public static class SoundCues
    {
        public static bool Has(HitAudioConfig config, string trigger) =>
            config?.cues != null && Array.Exists(config.cues, c => c.trigger == trigger);

        // trigger 이벤트(세기 count)에 낼 소리들. random01 은 0~1 난수 (테스트는 고정값을 넣음).
        public static List<SoundShot> Plan(HitAudioConfig config, string trigger, int count, Func<float> random01)
        {
            var shots = new List<SoundShot>();
            if (config?.cues == null) return shots;
            count = Mathf.Max(1, count);
            if (config.countCap > 0) count = Mathf.Min(count, config.countCap);
            foreach (SoundCue c in config.cues)
            {
                if (c.trigger != trigger || count < c.minCount || string.IsNullOrEmpty(c.sound)) continue;
                int extra = count - 1;
                float volume = c.volume + c.volumePerCount * extra;
                if (c.volumeMax > 0) volume = Mathf.Min(volume, c.volumeMax);
                float pitch = (c.pitch > 0 ? c.pitch : 1) + c.pitchPerCount * extra;
                int copies = Mathf.Clamp(1 + Mathf.FloorToInt(c.copiesPerCount * extra + 1e-4f), 1, Mathf.Max(1, c.maxCopies));
                for (int i = 0; i < copies; i++)
                {
                    float v = volume * (1 + c.volumeJitter * (random01() * 2 - 1)) * (i == 0 ? 1 : .8f);   // 뒤 겹은 조금 작게
                    float p = pitch + c.pitchJitter * (random01() * 2 - 1);
                    float gap = i == 0 ? 0 : c.copyGap * i * (1 + .3f * (random01() * 2 - 1));
                    shots.Add(new SoundShot { sound = c.sound, volume = Mathf.Max(0, v), pitch = Mathf.Clamp(p, .3f, 3f), delay = Mathf.Max(0, c.delay + gap) });
                }
            }
            return shots;
        }
    }
}
