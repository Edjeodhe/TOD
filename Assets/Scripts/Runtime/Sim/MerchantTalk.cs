// 16차 상인 단추: 지금 자세와 대사, 트윈 계산. Unity 그리기와 무관(테스트 대상). 허브와 쉬어가기 라운드가 같이 쓴다.
using System;
using UnityEngine;

namespace TOD
{
    public struct MerchantFrame
    {
        public MerchantMood mood;
        public float scaleX, scaleY;          // 1 기준 배율 (발밑 기준)
        public float angle;                   // 도, 시계 방향 +
        public float hop;                     // 위로 뜬 높이 (캔버스 px)
    }

    public class MerchantTalk
    {
        readonly MerchantConfig cfg;
        readonly Func<float> random;

        public MerchantMood Mood { get; private set; } = MerchantMood.Idle;
        public float MoodStart { get; private set; } = -99;
        public string Line { get; private set; }
        public float LineStart { get; private set; } = -99;
        public MerchantCue LineCue { get; private set; }
        float lastApproach = -999;
        int lastIndex = -1;

        public MerchantTalk(MerchantConfig config, Func<float> random)
        {
            cfg = config;
            this.random = random;
        }

        // 대사 하나를 고르고(같은 줄이 연달아 나오지 않게) 자세를 바꾼다. 고른 줄을 돌려준다 (없으면 null).
        public string Say(MerchantCue cue, float now)
        {
            MerchantLines set = cfg?.lines == null ? null : Array.Find(cfg.lines, l => l.cue == cue);
            if (set == null || set.lines == null || set.lines.Length == 0) return null;
            int index = Mathf.Min(set.lines.Length - 1, (int)(random() * set.lines.Length));
            if (set.lines.Length > 1 && index == lastIndex && LineCue == cue) index = (index + 1) % set.lines.Length;
            lastIndex = index;
            Line = set.lines[index];
            LineStart = now;
            LineCue = cue;
            SetMood(set.mood, now);
            return Line;
        }

        // 다가옴 혼잣말: 쿨다운이 지났고 다른 대사가 떠 있지 않을 때만
        public string Approach(float now)
        {
            if (now - lastApproach < cfg.approachCooldown || Showing(now)) return null;
            lastApproach = now;
            return Say(MerchantCue.Approach, now);
        }

        public void SetMood(MerchantMood mood, float now)
        {
            Mood = mood;
            MoodStart = now;
        }

        public void Hush() { Line = null; }

        public float LineLength => Line == null ? 0 : Line.Length / Mathf.Max(1, cfg.charsPerSecond) + cfg.lineHold;
        public bool Showing(float now) => Line != null && now - LineStart < LineLength;

        // 지금 찍혀 있는 글자 수
        public int Visible(float now) => Line == null ? 0 : Mathf.Clamp(Mathf.FloorToInt((now - LineStart) * cfg.charsPerSecond), 0, Line.Length);

        // 자세 유지 시간이 지나면 기본 자세로 (말이 떠 있는 동안은 유지)
        public void Update(float now)
        {
            if (Line != null && !Showing(now)) Line = null;
            if (Mood != MerchantMood.Idle && now - MoodStart > cfg.poseHold && !Showing(now)) SetMood(MerchantMood.Idle, now);
        }

        public MerchantFrame Pose(float now)
        {
            var f = new MerchantFrame { mood = Mood, scaleX = 1, scaleY = 1 };
            float t = now - MoodStart;
            // 숨쉬기: 세로로 살짝 늘었다 줄었다 (가로는 반대로 조금)
            float breath = Mathf.Sin(now * cfg.breathSpeed * Mathf.PI * 2) * cfg.breathAmount;
            f.scaleY += breath;
            f.scaleX -= breath * .5f;
            // 자세를 바꾼 순간: 눌렸다가 늘어나며 통 튀고 펴진다
            if (cfg.popTime > 0 && t < cfg.popTime)
            {
                float u = t / cfg.popTime;
                float squash = Mathf.Sin(u * Mathf.PI * 2) * (1 - u) * cfg.popSquash;
                f.scaleY -= squash;
                f.scaleX += squash * .7f;
                f.hop += Mathf.Max(0, Mathf.Sin(u * Mathf.PI)) * cfg.popHop;
            }
            switch (Mood)
            {
                case MerchantMood.Greet:
                case MerchantMood.Farewell:
                    if (cfg.tiltTime > 0 && t < cfg.tiltTime) f.angle += Mathf.Sin(t / cfg.tiltTime * Mathf.PI) * cfg.tiltAngle * (Mood == MerchantMood.Farewell ? -1 : 1);
                    break;
                case MerchantMood.Happy:
                    if (cfg.hopTime > 0 && t < cfg.hopTime) f.hop += Mathf.Abs(Mathf.Sin(t / cfg.hopTime * Mathf.PI * Mathf.Max(1, cfg.hopCount))) * cfg.hopHeight;
                    break;
                case MerchantMood.Refuse:
                    if (cfg.shakeTime > 0 && t < cfg.shakeTime)
                    {
                        float u = t / cfg.shakeTime;
                        f.angle += Mathf.Sin(u * Mathf.PI * 2 * Mathf.Max(1, cfg.shakeCount)) * (1 - u) * cfg.shakeAngle;
                    }
                    break;
            }
            // 말하는 동안(글자가 찍히는 동안) 살짝 까딱
            if (Line != null && Visible(now) < Line.Length) f.angle += Mathf.Sin(now * cfg.talkSpeed * Mathf.PI * 2) * cfg.talkAngle;
            return f;
        }

        public Sprite Sprite(MerchantMood mood)
        {
            if (cfg?.poses == null) return null;
            MerchantPose pose = Array.Find(cfg.poses, p => p.mood == mood && p.sprite != null) ?? Array.Find(cfg.poses, p => p.mood == MerchantMood.Idle);
            return pose?.sprite;
        }
    }
}
