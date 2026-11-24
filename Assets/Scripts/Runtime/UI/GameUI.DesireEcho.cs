// 23차 보스 욕망을 고른 뒤 잠깐 뜨는 한 줄 (GameUI 의 나머지 부분).
// 삼킴 = "달다. 그래서 더 무섭다." 검붉은 잉크, 다 쓰이면 종이 모서리에 핏자국이 번짐, 사라질 때 살짝 부풀며 옅어짐.
// 안 삼킴 = "손을 거두자, 욕망이 식어 간다." 잿빛 잉크, 사라질 때 아래로 가라앉으며 식어 감.
// 찢어진 종이 띠가 내려앉고 → 글자가 한 자씩 번지며 쓰이고(아래 바느질 실이 같이 꿰매짐) → 잠시 머문 뒤 사라진다. 소리는 없다.
// 구조는 Main.uxml(desire-echo), 모양은 Main.uss(.desire-echo-*), 문구와 시간은 데이터 desire(swallowLine, refuseLine, echo*).
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace TOD
{
    public partial class GameUI
    {
        VisualElement desireEcho;
        IVisualElementScheduledItem desireEchoTick;

        // 한 줄이 다 사라지는 때 (Time.unscaledTime). ElementHud 가 새 층 암시 문구를 이 뒤로 미뤄 둘이 겹치지 않게 한다.
        public float DesireEchoEnd { get; private set; } = -1;

        public void ShowDesireEcho(bool swallowed)
        {
            DesireConfig d = db.desire;
            string line = swallowed ? d.swallowLine : d.refuseLine;
            desireEcho ??= root?.Q("desire-echo");
            if (desireEcho == null || string.IsNullOrEmpty(line)) return;
            desireEchoTick?.Pause();
            desireEcho.EnableInClassList("swallow", swallowed);
            desireEcho.EnableInClassList("refuse", !swallowed);
            Label text = desireEcho.Q<Label>("desire-echo-text");
            VisualElement paper = desireEcho.Q("desire-echo-paper"), thread = desireEcho.Q("desire-echo-thread"), blot = desireEcho.Q("desire-echo-blot");
            text.enableRichText = true;
            float start = Time.unscaledTime + Mathf.Max(0, d.echoDelay);
            float appear = .45f, perChar = Mathf.Max(.01f, d.echoCharTime), write = line.Length * perChar;
            float fadeAt = appear + write + Mathf.Max(0, d.echoHold), fade = Mathf.Max(.05f, d.echoFade);
            DesireEchoEnd = start + fadeAt + fade;
            var sb = new StringBuilder();
            desireEcho.style.opacity = 0;
            Show(desireEcho, true);

            void Step()
            {
                float t = Time.unscaledTime - start;
                if (t < 0) { desireEcho.style.opacity = 0; return; }
                if (t > fadeAt + fade) { desireEchoTick.Pause(); Show(desireEcho, false); return; }
                float a = EaseOutCubic(Mathf.Clamp01(t / appear));
                float o = Mathf.Clamp01((t - fadeAt) / fade);
                desireEcho.style.opacity = a * (1 - o * o);
                // 종이: 위에서 살짝 기울어 내려앉음. 사라질 때 삼킴은 부풀며, 안 삼킴은 가라앉으며
                float drift = swallowed ? -10 * o : 22 * o;
                paper.style.translate = new Translate(0, Mathf.Lerp(-22, 0, a) + drift);
                paper.style.rotate = new Rotate(Mathf.Lerp(-3.5f, -1f, a));
                paper.style.scale = new Scale(Vector3.one * (swallowed ? 1 + .05f * o : 1 - .03f * o));

                // 글자: 쓰인 글자는 진하게, 지금 쓰는 글자는 번지는 중(반투명), 나머지는 자리만 (줄 폭이 흔들리지 않게)
                float written = Mathf.Max(0, t - appear * .5f) / perChar;
                int done = Mathf.Min(line.Length, Mathf.FloorToInt(written));
                sb.Clear();
                sb.Append(line, 0, done);
                if (done < line.Length)
                {
                    // 잉크 색(USS 의 swallow/refuse 색)에 알파만 바꾼 color 태그 (<alpha> 태그는 UI Toolkit 에서 듣지 않음)
                    string ink = ColorUtility.ToHtmlStringRGB(text.resolvedStyle.color);
                    int alpha = Mathf.RoundToInt(Mathf.Clamp01(written - done) * 255);
                    sb.Append("<color=#").Append(ink).Append(alpha.ToString("X2")).Append('>').Append(line[done]).Append("</color>");
                    if (done + 1 < line.Length) sb.Append("<color=#").Append(ink).Append("00>").Append(line, done + 1, line.Length - done - 1).Append("</color>");
                }
                string s = sb.ToString();
                if (text.text != s) text.text = s;
                thread.style.width = Length.Percent(Mathf.Clamp01(written / line.Length) * 100);

                // 삼킴: 다 쓰인 순간 핏자국이 번짐 (크게 나타났다 제자리로)
                if (swallowed)
                {
                    float b = Mathf.Clamp01((written - line.Length) / 4f);
                    blot.style.opacity = .8f * b;
                    blot.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.4f, 1f, EaseOutCubic(b)));
                }
            }

            desireEchoTick = desireEcho.schedule.Execute(Step).Every(16);
            Step();
        }
    }
}
