// 16차: 층 속성 · 상성 · 속성 몬스터 · 부가 발동 · 보스 속성 공격 데이터. 값은 씬 GameDatabase 의 elements 에 MCP 로 넣는다.
using System;
using UnityEngine;

namespace TOD
{
    // 프레임 그림 묶음 (Blender 플립북, Assets/Art/VFX/16). 본체(frames, 보통 합성) + 빛(glow, 가산, 본체 아래).
    // 그림 피벗이 기준점이다: 바닥 피벗(.5, .2) 시트는 발밑에, 가운데 피벗 시트는 몸 가운데에 놓인다.
    // frames 가 비어 있으면 쓰는 쪽이 ElementTone.hit 로 대신 그린다. 메뉴 TOD/Setup/16. Element Art 가 채운다.
    [Serializable] public class FlipClip
    {
        public Sprite[] frames, glow;
        public float fps;                     // 초당 프레임 (0 이면 효과 길이에 맞춰 한 번 돈다)
        public float size;                    // 한 칸을 그리는 크기 (캔버스 px)
        public Color tint = Color.white;      // 곱하는 색 (흰색 = 그림 그대로)
        public bool Has => frames != null && frames.Length > 0 && frames[0] != null;
    }

    // 속성 장식 중 움직이는 것 (불꽃 일렁임, 눈발 ... Blender decor_<속성>_loop). 맵에 흩어 두고 계속 돈다.
    [Serializable] public class AnimatedDecor
    {
        public FlipClip clip;                 // clip.size = 그리는 높이 (캔버스 px)
        public float weight = 1, alpha = 1;
    }

    // 몬스터 리그 파츠 한 장 → 속성 그림 한 장 (Assets/sprites/enemy_<종류>_<파츠>_<속성>.png). 메뉴 TOD/Setup/16. Element Art 가 채운다.
    [Serializable] public class SpriteSwap { public Sprite from, to; }

    // 보스 속성 공격 (보스의 돌진·탄이 주인공에게 맞을 때). value·time·range·count 의 뜻은 속성마다 다르다:
    //   화염 = 화상 초당 value, time 초 · 얼음 = 둔화 value 비율, time 초 · 번개 = 감전 count 번, 한 번에 value, time 초 간격
    //   빛 = 눈부심 time 초 (value = 화면을 덮는 세기 0~1) · 어둠 = 시야 좁힘 time 초, 보이는 반지름 range (캔버스 px)
    //   피 = 준 피해 × value 만큼 보스 회복 · 바람 = range 만큼 time 초 동안 밀쳐냄 · 독 = 중독 한 겹당 초당 value, time 초, 최대 count 겹
    [Serializable] public class BossElementAttack
    {
        public float value, time, range;
        public int count;
        public FlipClip fx;                   // 맞은 자리 속성 이펙트 (boss_<속성>_slam, 발밑). 없으면 ElementTone.hit
        public FlipClip bullet;               // 보스 탄 그림 (boss_<속성>_bullet, +x 가 나아가는 쪽). 없으면 둥근 탄에 bulletTint
        public FlipClip pool;                 // 화상·중독이 걸린 동안 발밑 웅덩이 (boss_fire_pool · boss_poison_pool)
        public Color bulletTint;              // 보스 탄 색 (a = 섞는 양)
    }

    [Serializable] public class ElementDef
    {
        public Element element;
        [TextArea] public string hint;        // 라운드 시작 때 화면 가운데 층 암시 (예: "화염의 기운이 가득한 것 같다...")
        [TextArea] public string fateLine;    // 19차 모이라의 '운명 읽기': 다음 등반 1층이 이 속성일 때 모이라가 하는 암시
        public Color hintColor;
        public Color fateColor;               // 20차 운명 읽기 연출 색: 점성구 유리, 퍼지는 빛, 글자 색조가 이 색을 따른다
        [Header("층 색조 (라운드 컨셉보다 우선)")]
        public Color floorFill;               // 바닥 덮개 (a = 덮는 양)
        public Color lampTint, fogTint;       // 등불·불씨 색, 가장자리 안개 색 (a = 섞는 양)
        [Header("속성 장식: 정적 소품(Codex) + 움직이는 장식(Blender)")]
        public DecorDef[] props;
        public AnimatedDecor[] animated;
        [Header("속성 몬스터")]
        public SpriteSwap[] skins;            // 파츠 그림 교체
        public Color monsterTint;             // 교체 그림이 없는 파츠에 곱하는 색 (a = 섞는 양)
        [Header("부가 세트 발동")]
        public FlipClip procFx;               // 발동 이펙트 (Blender proc_<속성>, 몸 가운데). 없으면 ElementTone.hit 을 procHitScale 배로
        public FlipClip bindBurst;            // 결박 폭발 (bind_<속성>_burst, 발밑) — 어둠, 피, 바람, 독은 예전 결박 폭발이 기본(빛살) 모양이었다
        public string procText;               // 적 머리 위 작은 글자 (예: "화염 폭발") — 비우면 안 띄움
        [Header("보스 속성 공격")]
        public BossElementAttack boss;
    }

    [Serializable] public class AffinityConfig
    {
        public Element[] ring;                // 상성 고리: ring[i] 가 ring[i+1] 에 강하다 (마지막은 처음에)
        public float strong, weak;            // 약점 배율(1.5), 저항 배율(.5)
        public string strongTag, weakTag;     // 피해 숫자 옆 작은 글자 ("약점", "저항")
        public Color strongColor, weakColor;
        public float tagPx;                   // 글자 크기 (캔버스 px)
        public Vector2 tagOffset;             // 숫자 오른쪽 끝 기준 (+x 오른쪽, +y 아래, 캔버스 px)
    }

    [Serializable] public class ElementConfig
    {
        public AffinityConfig affinity;
        public ElementDef[] defs;             // 층 속성 후보 (8속성)
        [Header("층 암시 문구")]
        public float hintDelay, hintFadeIn, hintHold, hintFadeOut;
        [Header("부가 세트 충전 게이지 (ElementHud)")]
        public string procCount;              // "{0}/{1}" (지금 충전 / 필요)
        public string procReady;              // 다 찼을 때 ("준비")
        [Header("속성 장식 배치")]
        public float propDensity;             // 정적 소품: 맵 100만 px² 당 개수
        public float animatedDensity;         // 움직이는 장식: 맵 100만 px² 당 개수
        public float propHeight;              // 소품 그림 높이 기본값 (DecorDef.height 가 0 일 때, 캔버스 px)
        [Header("부가 발동 연출")]
        public float procTime;                // 발동 이펙트 길이 (초)
        public float procHitScale;            // procFx 가 없을 때 ElementTone.hit 크기 배율
        public float procShake, procFlash;    // 화면 흔들림·번쩍임
        [Header("보스 속성 공격 연출")]
        public float bossHitTime, bossHitSize;
        public float blindAlpha;              // 눈부심: 화면을 하얗게 덮는 최대 양 (BossElementAttack.value 가 0 일 때)
        public Color blindColor;
        public Color darkColor;               // 시야 좁힘 바깥 색
        public Sprite visionMask;             // 가운데가 뚫린 둥근 그림 (가운데 투명, 바깥 불투명)
        public float playerDotInterval;       // 주인공 화상·중독 숫자를 모아 띄우는 간격

        public ElementDef Def(Element e) => defs == null ? null : Array.Find(defs, d => d.element == e);

        // +1 = attack 이 target 에 강함(약점), -1 = target 이 attack 에 강함(저항), 0 = 보통 (무속성 포함)
        public int Relation(Element attack, Element target)
        {
            Element[] ring = affinity?.ring;
            if (ring == null || ring.Length < 2) return 0;
            int a = Array.IndexOf(ring, attack), t = Array.IndexOf(ring, target);
            if (a < 0 || t < 0 || a == t) return 0;
            if ((a + 1) % ring.Length == t) return 1;
            if ((t + 1) % ring.Length == a) return -1;
            return 0;
        }

        public float Multiplier(Element attack, Element target)
        {
            int rel = Relation(attack, target);
            return rel > 0 ? affinity.strong : rel < 0 ? affinity.weak : 1;
        }
    }

    // 뷰가 쓰는 층 속성 색조 (라운드 컨셉보다 우선)
    public static class ElementLook
    {
        public static ElementDef Floor(GameDatabase db, RunState r) =>
            r == null || r.floorElement == Element.Plain ? null : db.elements?.Def(r.floorElement);

        public static bool HasFloor(GameDatabase db, RunState r) => Floor(db, r) is { } d && d.floorFill.a > 0;

        public static Color FloorFill(GameDatabase db, RunState r) =>
            Floor(db, r) is { } d && d.floorFill.a > 0 ? d.floorFill : r?.concept != null ? r.concept.floorFill : default;
    }
}
