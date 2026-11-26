// 24차 환경설정 (Esc 창): 기본값, 기본 키, 고를 수 있는 프레임 목록. 값은 씬 GameDatabase.settings 에 MCP 로 넣는다.
// 플레이어가 바꾼 값은 PlayerPrefs(saveKey) 에 JSON 으로 저장한다 (Settings.cs).
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TOD
{
    // 다시 정할 수 있는 키. Esc(창 닫기, 일시 정지)와 마우스(사격, 당기기)는 고정.
    public enum KeyAction { MoveUp, MoveDown, MoveLeft, MoveRight, Dash, Interact, Inventory, Rotate, StartRun, Potion1, Potion2, Potion3, Potion4, Potion5 }

    [Serializable]
    public struct KeyBind
    {
        public KeyAction action;
        public Key key;
    }

    [Serializable]
    public class SettingsConfig
    {
        public string saveKey;                // PlayerPrefs 키
        [Header("소리 (0~1)")]
        public float master;                  // 전체 음량 기본값 (0 = 꺼짐)
        public float sfx;                     // 효과음 기본값
        [Header("그래픽")]
        public bool vsync;                    // 수직 동기화 기본값
        public int frameCap;                  // 최대 프레임 기본값 (0 = 제한 없음)
        public int[] frameCaps;               // 고를 수 있는 최대 프레임 (0 = 제한 없음)
        public Vector2Int minResolution;      // 해상도 목록에 넣을 가장 작은 크기
        public float shake;                   // 화면 흔들림 기본값 (1 = 100%)
        [Header("키")]
        public KeyBind[] keys;                // 기본 키 (행동마다 하나)
        public KeyBind[] altKeys;             // 늘 같이 먹는 고정 보조 키 (방향키, 숫자 패드). 다시 정할 수 없다
        [Header("창 문구 (항목 이름은 Main.uxml dialog-settings)")]
        public string[] screenModeNames;      // GameController.ScreenModes 순서: 전체 화면, 독점 전체 화면, 창 모드
        public string on, off;                // 켬 / 끔
        public string unlimited;              // 최대 프레임 0 ("제한 없음")
        public string fpsFormat;              // "{0}"
        public string resolutionFormat;       // "{0} × {1}"
        public string percentFormat;          // "{0}%"
        public string listening;              // 새 키를 기다리는 칸 ("키를 누르세요")
        public string vsyncNote;              // 수직 동기화가 켜져 최대 프레임이 안 먹을 때 값 자리 ("수직 동기화")
    }
}
