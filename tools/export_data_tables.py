"""GameDatabase 밸런스 데이터 → 기획서/데이터테이블.xlsx (내보내기만, 가져오기 없음).

다시 뽑기:
  1. Unity 에서 MCP execute_code 로 tools/dump_gamedb.cs 본문 실행 → Temp/gamedb_dump.json
  2. python tools/export_data_tables.py   (인자: [덤프 json] [출력 xlsx])

시트 형식: 1행 = 열 이름, 2행 = 자료형(int / string / float / enum), 3행부터 데이터.
범위 = 밸런스(게임 규칙·수치)만. 화면 문구, 색, 이펙트 크기, 연출 시간, 그림은 뺐다.
효과류는 effect(enum) + value(float) + value_type(enum) 열로 통일. value 는 게임 데이터 값 그대로 (percent 0.3 = 30%).
enum 이름과 번호는 Assets/Scripts 의 C# enum 에서 읽는다 (문서용 enum 은 이 파일의 DOC_ENUMS).
"""
import json
import os
import re
import sys

from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill
from openpyxl.utils import get_column_letter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DUMP = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Temp", "gamedb_dump.json")
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "기획서", "데이터테이블.xlsx")
SCRIPTS = os.path.join(ROOT, "Assets", "Scripts", "Runtime")

d = json.load(open(DUMP, encoding="utf-8"))


# ---------- C# enum 읽기 ----------
def read_enums():
    enums = {}
    for base, _, files in os.walk(SCRIPTS):
        for f in files:
            if not f.endswith(".cs"):
                continue
            src = open(os.path.join(base, f), encoding="utf-8-sig").read()
            src = re.sub(r"//[^\n]*", "", src)
            for name, body in re.findall(r"\benum\s+(\w+)\s*\{([^}]*)\}", src):
                values, n = [], 0
                for item in [x.strip() for x in body.split(",") if x.strip()]:
                    if "=" in item:
                        item, num = [x.strip() for x in item.split("=")]
                        n = int(num)
                    values.append((item, n))
                    n += 1
                enums[name] = values
    return enums


CS = read_enums()


def ename(enum, num):
    for name, n in CS[enum]:
        if n == num:
            return name
    raise KeyError(f"{enum} {num}")


def num(v):
    """float32 잡음 없애기 (0.30000001 → 0.3)"""
    if isinstance(v, float):
        r = float("%.6g" % v)
        return int(r) if r.is_integer() and abs(r) < 1e15 else r
    return v


# ---------- 문서용 enum (게임 코드에는 없음, 표를 읽기 위한 것) ----------
DOC_ENUMS = {
    "DataType": [
        ("int", "정수"), ("float", "실수"), ("string", "글자"), ("enum", "이름으로 적은 enum 값 (번호는 이 시트)"),
    ],
    "ValueType": [
        ("flat", "그대로 더하는 수 (피해 +3, 체력 +15)"),
        ("percent", "비율. 게임 값 그대로 0.3 = 30%"),
        ("multiplier", "곱하는 배율 (0.9 = ×0.9)"),
        ("per_second", "초당 수치 (화상 초당 2)"),
        ("seconds", "시간 (초)"),
        ("count", "개수, 횟수, 겹, 마리"),
        ("distance", "거리 (전투장 px)"),
    ],
    "Scaling": [
        ("fixed", "단계와 상관없이 그 값"),
        ("per_tier", "세트 단계마다 곱함 (값 × 단계)"),
    ],
    "SetEffect": [
        ("sin_damage", "당기기 한 타마다 더하는 죄종 피해"),
        ("burn_dps", "분노: 화상 초당 피해"),
        ("burn_time", "분노: 화상 지속"),
        ("low_hp_damage", "분노: 내 체력이 낮을수록 피해 +(1 − 체력 비율) × 값"),
        ("slow", "나태: 둔화 비율 (맞을 때마다 1겹)"),
        ("slow_cap", "나태: 둔화 상한"),
        ("slow_time", "나태: 둔화 지속"),
        ("sleep_stacks", "나태: 이 겹이면 잠듦"),
        ("sleep_time", "나태: 잠드는 시간"),
        ("boss_sleep_scale", "나태: 보스가 잠드는 시간 배율"),
        ("poison_dps", "시기: 독 한 겹당 초당 피해"),
        ("poison_time", "시기: 독 지속"),
        ("poison_max_stacks", "시기: 독 최대 겹"),
        ("spread_range", "시기: 독 걸린 적이 죽으면 옮기는 반경"),
        ("spread_count", "시기: 옮겨 가는 적 수"),
        ("lifesteal", "식탐: 준 피해 중 회복 비율"),
        ("kill_yarn", "식탐: 처치하면 털실 +"),
        ("charm_time", "색욕: 매혹 시간 (공격 안 함)"),
        ("charm_range", "색욕: 주변 적이 함께 끌려오는 반경"),
        ("pull_bonus", "색욕: 끄는 거리 증가"),
        ("mortgage_taken", "탐욕: 저당 잡힌 적이 받는 피해 증가"),
        ("mortgage_time", "탐욕: 저당 지속"),
        ("mortgage_kill_gold", "탐욕: 저당 잡힌 적 처치 시 금화 +"),
        ("proud_damage", "교만: 정예, 보스, 체력이 가득한 적에게 피해 증가"),
    ],
    "BossSinAttack": [
        ("burn_dps", "분노: 주인공 화상 초당 피해"),
        ("burn_time", "분노: 화상 지속"),
        ("slow", "나태: 주인공 둔화 비율 (최대 0.9)"),
        ("slow_time", "나태: 둔화 지속"),
        ("blind_power", "교만: 눈부심이 화면을 덮는 세기"),
        ("blind_time", "교만: 눈부심 지속"),
        ("dark_time", "탐욕: 시야 좁힘 지속"),
        ("dark_radius", "탐욕: 보이는 반지름 (캔버스 px)"),
        ("boss_heal", "식탐: 준 피해 × 값만큼 보스 회복"),
        ("pull_distance", "색욕: 보스 쪽으로 끌려가는 거리"),
        ("pull_time", "색욕: 끌려가는 시간"),
        ("poison_dps", "시기: 독 한 겹당 초당 피해"),
        ("poison_time", "시기: 독 지속"),
        ("poison_max_stacks", "시기: 독 최대 겹"),
    ],
    "DesireEffect": [
        ("thread_transfer", "집착: 적이 죽으면 실이 가까운 적에게 옮겨 붙음 (수치 없음)"),
        ("yarn_max_down", "집착(대가): 털실 최대 −"),
        ("knot_threshold", "격분: 매듭이 이 수 이상이면"),
        ("pull_damage", "격분: 당기기 피해 배율"),
        ("damage_taken", "격분(대가): 받는 피해 배율"),
        ("kill_heal", "갈망: 처치하면 체력 +"),
        ("potion_effect", "갈망(대가): 회복, 정화 포션 효과 배율"),
        ("thread_damage", "잔향: 실 한 가닥 피해 +"),
    ],
    "UpgradeEffect": [
        ("board_line", "판 가장자리에 줄 하나 추가"),
        ("cost_limit", "등급(코스트) 한도 +"),
        ("line_stat_bonus", "고른 줄 위 조각 능력치 +"),
        ("desire_max", "다음 등반부터 욕망 최대치 +"),
        ("starter_slots", "등반에 챙겨 갈 퍼즐 칸 +"),
        ("fate_set_weight", "예언한 죄종 세트 조각 가중치 + (다음 등반 한 번)"),
    ],
    "RewardPlace": [
        ("run", "쉬어가기 라운드에서 말을 걸 때"),
        ("hub", "재봉실에서 등반마다 한 번"),
    ],
    "PotionSource": [
        ("start", "등반 시작 때 받음"),
        ("elite_drop", "정예 처치 드롭 후보 (count = 뽑힐 비중)"),
    ],
}

# 게임 enum 설명 (값 이름 → 설명). 비우면 설명 없음.
GAME_DESC = {
    "StatKind": {"Damage": "실 피해 (flat)", "Speed": "이동 속도 (percent)", "Health": "최대 체력 (flat)",
                 "Recharge": "털실 충전 속도 (percent)", "Purity": "층마다 오르는 욕망 감소 (flat)",
                 "Yarn": "털실 최대 (count)", "Multishot": "한 번에 더 나가는 바늘 (count)",
                 "ThreadWidth": "실 굵기, 당김 판정 (percent)"},
    "RoundKind": {"Survival": "시간을 버티는 라운드", "Rest": "쉬어가기 (탑, 상인, NPC)", "Boss": "층 보스"},
    "WeatherKind": {"None": "없음", "GainDown": "얻는 경험치, 골드 감소", "HealDown": "회복량 감소",
                    "Vision": "시야 반지름 감소", "CritDown": "치명타 확률 감소",
                    "Drain": "초당 최대 체력 비율만큼 줄어듦 (1 아래로는 안 내려감)",
                    "DamageTakenUp": "받는 피해 증가", "MoveDown": "이동 속도 감소"},
    "BossMoveKind": {"Slam": "주인공 자리 원 (연타 count)", "Cone": "보스 앞 부채꼴 (width = 각도°)",
                     "Charge": "직선 예고 뒤 돌진 (radius = 길이, width = 폭)", "Nova": "보스 둘레 원, inner > 0 이면 고리",
                     "Rain": "주인공 둘레 spread 안에 원 count 개", "Lines": "보스에서 뻗는 직선 count 갈래",
                     "Volley": "탄 count 발 (spread = 부채 각도°)", "Leap": "주인공 자리로 뛰어올라 내려앉음",
                     "Pull": "speed 만큼 끌어당긴 뒤 보스 둘레 원"},
    "NpcKind": {"Blessing": "축복 (수녀 재봉사)", "Gift": "조각 선물 (행상 쥐)", "Memory": "경험치 (촛불 유령)",
                "Gamble": "도박 (단추 눈 인형)"},
    "NpcRewardKind": {"Effects": "능력치 효과 (이번 등반 동안)", "Piece": "무작위 조각 (없으면 value 만큼 대체 보상)",
                      "Xp": "경험치 = 지금 레벨 필요량 × value", "Gold": "골드", "Shards": "기억의 파편",
                      "Heal": "최대 체력 비율 회복", "Hurt": "최대 체력 비율 잃음", "Boon": "다음 등반 시작 때 능력치 효과",
                      "Nothing": "아무것도 없음"},
    "PotionEffect": {"Heal": "체력 회복", "Purify": "욕망 감소", "Yarn": "털실 채움 (0 = 가득)"},
    "Element": {"Plain": "세트 없음 (기본 실)", "Unused3": "안 씀", "Dodge": "대쉬 흔적 (세트 아님)"},
}
for e in d["elements"]["defs"]:
    GAME_DESC["Element"][ename("Element", e["element"])] = e["sinName"]
GAME_DESC["RunStat"] = {ename("RunStat", i): n for i, n in enumerate(d["npcs"]["statNames"])}
GAME_DESC["ItemEffect"] = {"None": "없음"}
for t in d["gear"]["effectTexts"]:
    GAME_DESC["ItemEffect"][ename("ItemEffect", t["effect"])] = f'{t["name"]}: {t["text"]}  ({{0}} = value, {{1}} = param)'

GAME_ENUMS = ["Element", "RoundKind", "WeatherKind", "StatKind", "ItemEffect", "RunStat", "BossMoveKind",
              "NpcKind", "NpcRewardKind", "PotionEffect"]

# ---------- 시트 모음 ----------
SHEETS = []   # (이름, [(열, 자료형)], rows)


def sheet(name, cols, rows):
    SHEETS.append((name, cols, rows))


S = lambda n: (n, "string")
I = lambda n: (n, "int")
F = lambda n: (n, "float")
E = lambda n: (n, "enum")

sin = lambda el: ename("Element", el)
sin_name = {e["element"]: e["sinName"] for e in d["elements"]["defs"]}

# 설정 (세로 키·값) ------------------------------------------------------
SETTINGS = [
    ("arena", "arena", ["width", "height", "finalFloor"]),
    ("hero", "hero", ["baseHp", "baseDamage", "strandBonus", "strandBonusMax", "critChance", "critMultiplier",
                      "baseYarn", "minYarn", "moveSpeed", "encumbrance", "minSpeedFactor", "frenzySpeed",
                      "shotInterval", "rechargeSeconds", "idleRechargeMultiplier", "frenzyRechargeMultiplier",
                      "idleMotionThreshold", "knotInterval", "earlyKnotInterval", "hurtTime",
                      "pickupRadius", "magnetRadius", "magnetSpeed", "bulletHitRadius",
                      "multishotSpread", "multishotMax", "threadWidthMax", "threadHitWidth"]),
    ("needle", "needle", ["reach", "launch", "settle", "kick", "start", "curve"]),
    ("dash", "feel", ["dashCharges", "dashCooldown", "dashMinCooldown", "dashDuration", "dashDistance", "dashGrace"]),
    ("yank", "feel", ["yankReach", "yankBoss", "yankTime", "yankMinGap"]),
    ("bind", "bind", ["strands", "damageBonus", "pullTime"]),
    ("enemy", "enemies", ["hpPerFloor", "damagePerFloor", "speedPerFloor", "eliteHp", "eliteDamage", "eliteGold",
                          "waitJitter", "approachSlack", "retreatSlack", "retreatSpeed", "meleeTrigger", "meleeRange",
                          "windupMelee", "windupShot", "windupDash", "enragedWindupDash", "chargeTime", "chargeSpeed",
                          "bossChargeSpeed", "enragedChargeSpeed", "chargeHitPad", "restAfterCharge",
                          "enragedRestAfterCharge", "restAfterMelee", "restAfterShot", "enragedWaitMultiplier",
                          "enrageAt", "bossShotChance", "bossThresholds", "circleShots", "bigFanShots",
                          "smallFanShots", "bigFanFromFloor", "fanSpread", "circleSpeed", "fanSpeed", "bulletLife"]),
    ("spawn", "spawn", ["maxEnemies", "spawnDistanceMin", "spawnDistanceMax", "floorRateStep", "minInterval"]),
    ("round", "rounds", ["merchantChance", "artifactChoices", "interactRadius", "stairsRadius"]),
    ("reward", "rewards", ["xpBase", "xpStep", "xpNormal", "xpElite", "xpBoss", "shardElite", "shardBoss",
                           "shardFloorClear", "eliteItemChance", "bossDropsItem", "healChance", "healEliteChance",
                           "healAmount"]),
    ("augment", "augments", ["choices", "ruleWeight", "statWeight", "echoWindow", "minFireInterval"]),
    ("desire", "desire", ["maxBase", "perFloor", "absorb", "awakenChance", "awakenReset", "frenzyReset",
                          "awakenDamage", "highFraction", "priceMarkup"]),
    ("gear", "gear", ["baseColumns", "baseRows", "baseCostLimit", "starterSlotsBase"]),
    ("shop", "shop", ["saleSeconds", "saleDiscount"]),
    ("hub", "hub", ["shopStock", "packPicks", "shopRefreshHours", "interactRadius"]),
    ("potion", "potions", ["slots", "stack", "eliteChance", "cooldown"]),
    ("npc", "npcs", ["chance", "unmetWeight", "interactRadius"]),
    ("boss", "bosses", ["staggerTime", "roarTime", "refillTime", "roarRadius", "roarKnock", "roarKnockTime", "chargePad"]),
]


def kv_type(v):
    if isinstance(v, bool):
        return "int"
    if isinstance(v, int):
        return "int"
    if isinstance(v, float):
        return "float"
    return "string"


rows = []
for group, src, keys in SETTINGS:
    for k in keys:
        v = d[src][k]
        vals = [(k, v)] if not isinstance(v, list) else [(f"{k}[{i}]", x) for i, x in enumerate(v)]
        for key, x in vals:
            rows.append([group, key, kv_type(x), int(x) if isinstance(x, bool) else num(x)])
sheet("설정", [S("group"), S("key"), E("type"), F("value")], rows)   # value 의 실제 자료형은 type 열 (DataType)

# 라운드 ------------------------------------------------------------------
rows, si = [], 0
for i, k in enumerate(d["rounds"]["sequence"]):
    kind = ename("RoundKind", k)
    sec = None
    if kind == "Survival":
        sec = num(d["rounds"]["survivalSeconds"][si]); si += 1
    rows.append([i + 1, kind, sec])
sheet("라운드", [I("order"), E("kind"), F("seconds")], rows)

# 컨셉 / 컨셉 적 가중치 -------------------------------------------------------
sheet("컨셉", [S("id"), S("name"), F("spawn_rate"), F("hp_scale"), F("speed_scale"), F("elite_scale")],
      [[c["id"], c["name"], num(c["spawnRate"]), num(c["hpScale"]), num(c["speedScale"]), num(c["eliteScale"])]
       for c in d["concepts"]])
sheet("컨셉_적가중치", [S("concept"), S("enemy"), F("weight")],
      [[c["id"], w["type"], num(w["weight"])] for c in d["concepts"] for w in c["typeWeights"]])

# 스폰 / 적 ------------------------------------------------------------------
sheet("스폰", [S("enemy"), F("interval"), F("interval_jitter"), F("start_delay"), I("count"), I("count_jitter"),
             F("elite_chance")],
      [[s["type"], num(s["interval"]), num(s["intervalJitter"]), num(s["startDelay"]), s["count"], s["countJitter"],
        num(s["eliteChance"])] for s in d["spawn"]["table"]])
sheet("적", [S("id"), F("hp"), F("speed"), F("damage"), F("radius"), F("wait"), F("gold"), F("preferred_distance")],
      [[t["id"], num(t["hp"]), num(t["speed"]), num(t["damage"]), num(t["radius"]), num(t["wait"]), num(t["gold"]),
        num(t["preferredDistance"])] for t in d["enemies"]["types"]])

sheet("적_보스소환", [I("order"), S("enemy")], [[i + 1, x] for i, x in enumerate(d["enemies"]["bossSummons"])])

# 죄종 · 층 날씨 ---------------------------------------------------------------
WEATHER_TYPE = {"GainDown": "percent", "HealDown": "percent", "Vision": "percent", "CritDown": "percent",
                "Drain": "percent", "DamageTakenUp": "percent", "MoveDown": "percent"}
rows = []
for e in d["elements"]["defs"]:
    w = e["weather"]
    kind = ename("WeatherKind", w["kind"])
    rows.append([sin(e["element"]), e["sinName"], w["name"], kind, num(w["baseValue"]), WEATHER_TYPE.get(kind),
                 num(w["perFloor"]), num(w["cap"]), num(w["extra"]), "percent" if w["extra"] else None])
sheet("죄종_날씨", [E("sin"), S("name"), S("weather"), E("effect"), F("value"), E("value_type"), F("per_floor"),
                  F("cap"), F("extra"), E("extra_type")], rows)

# 보스 / 보스 패턴 / 보스 죄종 공격 ---------------------------------------------------
B = d["bosses"]["defs"]
sheet("보스", [E("sin"), S("name"), S("phase2_name"), F("hp"), F("damage"), F("speed"), F("radius"),
             F("preferred_distance"), F("phase2_hp"), F("phase2_speed"), F("phase2_tempo"), F("idle")],
      [[sin(b["element"]), b["name"], b["phase2Name"], num(b["hp"]), num(b["damage"]), num(b["speed"]),
        num(b["radius"]), num(b["preferredDistance"]), num(b["phase2Hp"]), num(b["phase2Speed"]),
        num(b["phase2Tempo"]), num(b["idle"])] for b in B])
if any(b["phase2Summons"] for b in B):
    sheet("보스_소환", [E("sin"), S("enemy")], [[sin(b["element"]), s] for b in B for s in b["phase2Summons"]])
MOVE_COLS = ["windup", "damage", "radius", "inner", "width", "count", "interval", "spread", "speed", "lead", "knock", "rest"]
rows = []
for b in B:
    for phase, key in ((1, "moves"), (2, "moves2")):
        for i, m in enumerate(b[key]):
            rows.append([sin(b["element"]), phase, i + 1, m["name"], ename("BossMoveKind", m["kind"])] +
                        [num(m[c]) for c in MOVE_COLS])
sheet("보스_패턴", [E("sin"), I("phase"), I("order"), S("name"), E("kind")] +
      [I(c) if c == "count" else F(c) for c in MOVE_COLS], rows)

BOSS_ATTACK = {  # 죄종 → [(BossSinAttack, 필드, value_type)]
    "Wrath": [("burn_dps", "value", "per_second"), ("burn_time", "time", "seconds")],
    "Sloth": [("slow", "value", "percent"), ("slow_time", "time", "seconds")],
    "Pride": [("blind_power", "value", "percent"), ("blind_time", "time", "seconds")],
    "Greed": [("dark_time", "time", "seconds"), ("dark_radius", "range", "distance")],
    "Gluttony": [("boss_heal", "value", "multiplier")],
    "Lust": [("pull_distance", "range", "distance"), ("pull_time", "time", "seconds")],
    "Envy": [("poison_dps", "value", "per_second"), ("poison_time", "time", "seconds"),
             ("poison_max_stacks", "count", "count")],
}
rows = []
for e in d["elements"]["defs"]:
    for eff, field, vt in BOSS_ATTACK[sin(e["element"])]:
        rows.append([sin(e["element"]), eff, num(e["boss"][field]), vt])
sheet("보스_죄종공격", [E("sin"), E("effect"), F("value"), E("value_type")], rows)

# 세트 / 세트 단계 / 세트 효과 --------------------------------------------------------
G = d["gear"]
unlock = {u["set"]: u["floor"] for u in G["setUnlockFloor"]}
sheet("세트", [S("id"), S("name"), E("sin"), F("weight"), I("unlock_floor"), I("proc_charge"), F("proc_damage"),
             F("proc_damage_per_tier"), F("proc_effect_scale")],
      [[s["id"], s["name"], sin(s["element"]), num(s["weight"]), unlock.get(s["id"], 0), s["procCharge"],
        num(s["procDamage"]), num(s["procDamagePerTier"]), num(s["procEffectScale"])] for s in G["sets"]])
sheet("세트_단계", [S("set"), I("tier"), I("pieces")],
      [[s["id"], i + 1, t] for s in G["sets"] for i, t in enumerate(s["thresholds"])])

SET_EFFECT = {  # 죄종 → [(SetEffect, 필드, value_type, scaling)]  (SetDef 주석, Combat.cs 기준)
    "Wrath": [("burn_dps", "effectPerTier", "per_second", "per_tier"), ("burn_time", "effectTime", "seconds", "fixed"),
              ("low_hp_damage", "extra", "percent", "per_tier")],
    "Sloth": [("slow", "effectPerTier", "percent", "per_tier"), ("slow_cap", "effectCap", "percent", "fixed"),
              ("slow_time", "effectTime", "seconds", "fixed"), ("sleep_stacks", "extra", "count", "fixed"),
              ("sleep_time", "extraTime", "seconds", "fixed"), ("boss_sleep_scale", "bossSleepScale", "multiplier", "fixed")],
    "Envy": [("poison_dps", "effectPerTier", "per_second", "per_tier"), ("poison_time", "effectTime", "seconds", "fixed"),
             ("poison_max_stacks", "effectCap", "count", "fixed"), ("spread_range", "effectRange", "distance", "fixed"),
             ("spread_count", "extra", "count", "fixed")],
    "Gluttony": [("lifesteal", "effectPerTier", "percent", "per_tier"), ("kill_yarn", "extra", "count", "fixed")],
    "Lust": [("charm_time", "effectTime", "seconds", "fixed"), ("charm_range", "effectRange", "distance", "fixed"),
             ("pull_bonus", "effectPerTier", "percent", "per_tier")],
    "Greed": [("mortgage_taken", "effectPerTier", "percent", "per_tier"), ("mortgage_time", "effectTime", "seconds", "fixed"),
              ("mortgage_kill_gold", "extra", "flat", "per_tier")],
    "Pride": [("proud_damage", "effectPerTier", "percent", "per_tier")],
}
rows = []
for s in G["sets"]:
    rows.append([s["id"], "sin_damage", num(s["damagePerTier"]), "flat", "per_tier"])
    for eff, field, vt, sc in SET_EFFECT[sin(s["element"])]:
        rows.append([s["id"], eff, num(s[field]), vt, sc])
sheet("세트_효과", [S("set"), E("effect"), F("value"), E("value_type"), E("scaling")], rows)

# 등급 ------------------------------------------------------------------------
prices = d["hub"]["piecePrices"]
sheet("등급", [I("rank"), S("name"), F("weight"), I("shop_price")],
      [[r, G["rankNames"][r], num(G["rankWeights"][r]) if r < len(G["rankWeights"]) else 1,
        prices[r] if r < len(prices) else None] for r in range(1, len(G["rankNames"]))])

# 조각 / 조각 모양 ---------------------------------------------------------------
STAT_TYPE = {"Damage": "flat", "Speed": "percent", "Health": "flat", "Recharge": "percent", "Purity": "flat",
             "Yarn": "count", "Multishot": "count", "ThreadWidth": "percent"}
ITEM_TYPE = {  # ItemEffect → (value_type, param_type)   effectTexts 의 {0} = value, {1} = param
    "None": (None, None), "Brink": ("percent", "percent"), "QuickBind": ("count", None), "Spite": ("percent", None),
    "HeavyKnot": ("percent", None), "Leap": ("percent", "seconds"), "Greed": ("percent", "percent"),
    "Rewind": ("count", None), "Giant": ("percent", None), "GoldThread": ("percent", "count"),
    "Feast": ("percent", "seconds"), "Devour": ("flat", None), "Embrace": ("percent", None),
    "Crown": ("percent", None), "Pedestal": ("percent", None), "Tailor": ("percent", None),
}
rows, cells = [], []
for i, p in enumerate(G["pieces"]):
    stat = ename("StatKind", p["stat"])
    eff = ename("ItemEffect", p["effect"])
    vt, pt = ITEM_TYPE[eff]
    has = eff != "None"
    rows.append([i, p["name"], p["set"] or None, p["cost"], len(p["shape"]), stat, num(p["value"]), STAT_TYPE[stat],
                 eff if has else None, num(p["effectValue"]) if has else None, vt,
                 num(p["effectParam"]) if pt else None, pt, num(p["desireStart"]) or None])
    cells += [[i, c["x"], c["y"]] for c in p["shape"]]
sheet("조각", [I("id"), S("name"), S("set"), I("rank"), I("cells"), E("stat"), F("stat_value"), E("stat_type"),
             E("effect"), F("effect_value"), E("effect_type"), F("effect_param"), E("param_type"), F("desire_start")], rows)
sheet("조각_모양", [I("piece"), I("x"), I("y")], cells)

# 증강 -------------------------------------------------------------------------
OP_TYPE = {"Add": "flat", "Multiply": "multiplier"}


def effect_rows(effects):
    return [(ename("RunStat", x["stat"]), num(x["value"]), OP_TYPE[ename("StatOp", x["op"])]) for x in effects]


sheet("증강", [S("id"), S("name"), E("effect"), F("value"), E("value_type")],
      [[a["id"], a["name"], *er] for a in d["augments"]["augments"] for er in effect_rows(a["effects"])])

# 욕망 -------------------------------------------------------------------------
D = d["desire"]
DES = {
    "obsession": [("thread_transfer", None, None), ("yarn_max_down", D["obsessionYarn"], "count")],
    "rage": [("knot_threshold", D["rageKnots"], "count"), ("pull_damage", D["rageMultiplier"], "multiplier"),
             ("damage_taken", D["rageTaken"], "multiplier")],
    "hunger": [("kill_heal", D["hungerHeal"], "flat"), ("potion_effect", D["hungerPotion"], "multiplier")],
    "refine": [("thread_damage", D["refineDamage"], "flat")],
}
rows = []
for x in D["desires"] + [D["refine"]]:
    for eff, v, vt in DES[x["id"]]:
        rows.append([x["id"], x["name"], eff, num(v) if v is not None else None, vt])
sheet("욕망", [S("id"), S("name"), E("effect"), F("value"), E("value_type")], rows)

# 포션 / 포션 획득 ---------------------------------------------------------------
P = d["potions"]
POTION_TYPE = {"Heal": "flat", "Purify": "flat", "Yarn": "count"}
sheet("포션", [S("id"), S("name"), E("effect"), F("value"), E("value_type")],
      [[p["id"], p["name"], ename("PotionEffect", p["effect"]), num(p["amount"]),
        POTION_TYPE[ename("PotionEffect", p["effect"])]] for p in P["potions"]])


def counted(ids):
    out = {}
    for x in ids:
        out[x] = out.get(x, 0) + 1
    return out.items()


sheet("포션_획득", [E("source"), S("potion"), I("count")],
      [["start", k, n] for k, n in counted(P["start"])] + [["elite_drop", k, n] for k, n in counted(P["eliteDrops"])])

# 상점 (쉬어가기 상인) ---------------------------------------------------------------
WARE_EFFECT = {"buff": ("Damage", "flat")}   # 포션이 아닌 물품 (GameController.cs 의 case)
rows = []
for w in d["shop"]["wares"]:
    eff, vt = WARE_EFFECT.get(w["id"], (None, None))
    rows.append([w["id"], w["name"], w["basePrice"], w["potion"] or None, eff, num(w["amount"]) if eff else None, vt])
sheet("상점", [S("id"), S("name"), I("base_price"), S("potion"), E("effect"), F("value"), E("value_type")], rows)

# 강화 (모이라, 단추) / 강화 효과 ---------------------------------------------------------
H = d["hub"]
ups = [[u["key"], u["name"], u["price"], u["priceStep"], u["max"]] for u in d["upgrades"]]
ups.append(["fate", H["fateName"], H["fatePrice"], 0, None])
sheet("강화", [S("key"), S("name"), I("price"), I("price_step"), I("max")], ups)
UPG = {
    "expansion": [("board_line", 1, "count"), ("cost_limit", G["costLimitStep"], "count")],
    "lines": [("line_stat_bonus", G["rowBonus"], "percent")],
    "capacity": [("desire_max", D["capacityStep"], "flat")],
    "starter": [("starter_slots", G["starterSlotsStep"], "count")],
    "fate": [("fate_set_weight", G["fateSetBonus"], "percent")],
}
sheet("강화_효과", [S("key"), E("effect"), F("value"), E("value_type")],
      [[u[0], eff, num(v), vt] for u in ups for eff, v, vt in UPG[u[0]]])

# NPC / NPC 보상 ----------------------------------------------------------------
N = d["npcs"]["npcs"]
sheet("NPC", [E("kind"), S("id"), S("name"), F("weight"), F("hp_cost")],
      [[ename("NpcKind", n["kind"]), n["id"], n["name"], num(n["weight"]), num(n["hpCost"])] for n in N])
REWARD_TYPE = {"Piece": "flat", "Xp": "multiplier", "Gold": "flat", "Shards": "count", "Heal": "percent",
               "Hurt": "percent"}
rows = []
for n in N:
    for place, key in (("run", "rewards"), ("hub", "hubRewards")):
        for i, w in enumerate(n[key]):
            kind = ename("NpcRewardKind", w["kind"])
            base = [n["id"], place, i + 1, kind, num(w["weight"]), w["name"] or None, int(w["bad"])]
            if w["effects"]:
                rows += [base + list(er) for er in effect_rows(w["effects"])]
            else:
                vt = REWARD_TYPE.get(kind)
                rows.append(base + [None, num(w["amount"]) if vt else None, vt])
sheet("NPC_보상", [S("npc"), E("place"), I("no"), E("kind"), F("weight"), S("name"), I("bad"), E("effect"), F("value"),
                  E("value_type")], rows)

# 일기 보상 ----------------------------------------------------------------------
pname = lambda k: G["pieces"][k]["name"] if 0 <= k < len(G["pieces"]) else None
sheet("일기_보상", [I("floor"), I("read_shards"), I("pattern"), S("pattern_name"), I("tainted"), S("tainted_name")],
      [[x["floor"], x["readShards"], x["patternKind"], pname(x["patternKind"]),
        x["patternTainted"], pname(x["patternTainted"])] for x in d["diary"]["entries"]])

# value_type 정의 (맨 앞 시트) -------------------------------------------------------
# (이름, 뜻, 계산, 예시). 번호는 DOC_ENUMS["ValueType"] 순서와 같아야 함.
VALUE_TYPE_DEF = [
    ("flat", "단위 없는 수를 그대로 더하거나 뺌",
     "결과 = 기준 + value (감소 효과면 기준 − value)",
     "증강 attack: Damage 3 → 실 피해 +3 / 포션 heal 32 → 체력 +32"),
    ("percent", "비율. 게임 값 그대로 0~1 (0.3 = 30%)",
     "늘리는 효과: 기준 × (1 + value) / 줄이는 효과(…Down, slow 등): 기준 × (1 − value) / 확률·비율에 더하는 효과: 그 확률에 value 를 더함(%p)",
     "날씨 GainDown 0.3 → 경험치·골드 ×0.7 / 조각 Speed 0.04 → 이동 속도 ×1.04 / 왕좌 0.15 → 치명타 확률 +15%p"),
    ("multiplier", "곱하는 배율",
     "결과 = 기준 × value (1 = 그대로, 1보다 작으면 줄어듦)",
     "증강 attackSpeed: FireInterval 0.9 → 발사 간격 ×0.9 (10% 더 자주) / 격분 pull_damage 2 → 당기기 피해 ×2"),
    ("per_second", "1초마다 value 만큼 (지속 시간 동안)",
     "총량 = value × 지속(초)",
     "세트 분노 burn_dps 2 (per_tier) → 3단계면 화상 초당 6 / 보스 분노 burn_dps 6, burn_time 3 → 18"),
    ("seconds", "시간 (초)",
     "효과가 value 초 동안 유지, 또는 value 초 안에",
     "세트 색욕 charm_time 1.5 → 1.5초 매혹 / 조각 Leap param 1 → 대쉬 후 1초 안"),
    ("count", "개수 (정수로 씀): 털실, 바늘, 겹, 마리, 매듭, 금화, 파편",
     "결과 = 기준 + value 개 (최대 겹·조건 수면 그 개수)",
     "조각 Rewind 1 → 처치마다 털실 +1 / 시기 poison_max_stacks 6 → 독 6겹까지"),
    ("distance", "거리 (전투장 좌표 px, 플레이어 이동 속도와 같은 단위)",
     "반경·거리 = value",
     "세트 시기 spread_range 220 → 220 안의 적에게 독이 옮음"),
]
assert [x[0] for x in VALUE_TYPE_DEF] == [x[0] for x in DOC_ENUMS["ValueType"]]
VT_NAMES = {x[0] for x in VALUE_TYPE_DEF}
used = {v: [] for v in VT_NAMES}
for name, cols, data in SHEETS:
    for i, (c, t) in enumerate(cols):
        if t != "enum":
            continue
        vals = {r[i] for r in data if r[i] is not None}
        if vals and vals <= VT_NAMES:
            for v in vals:
                used[v].append(f"{name}.{c}")
SHEETS.insert(0, ("value_type", [S("value_type"), I("value"), S("meaning"), S("formula"), S("example"), S("used_in")],
                  [[n, i, m, f, e, ", ".join(used[n])] for i, (n, m, f, e) in enumerate(VALUE_TYPE_DEF)]))

# enums -------------------------------------------------------------------------
rows = []
for en in GAME_ENUMS:
    for name, n in CS[en]:
        rows.append([en, name, n, GAME_DESC.get(en, {}).get(name)])
for en, vals in DOC_ENUMS.items():
    for n, (name, desc) in enumerate(vals):
        rows.append([en, name, n, desc])
sheet("enums", [S("enum"), S("name"), I("value"), S("desc")], rows)

# ---------- 검사: 자료형 4가지만, enum 값이 enums 에 있는지 ----------
known = {}
for en, name, _, _ in rows:
    known.setdefault(name, set()).add(en)
for name, cols, data in SHEETS:
    for c, t in cols:
        assert t in ("int", "string", "float", "enum"), (name, c, t)
    for r in data:
        assert len(r) == len(cols), (name, r)
        for (c, t), v in zip(cols, r):
            if v is None:
                continue
            if t == "enum":
                assert isinstance(v, str) and v in known, (name, c, v)
            elif t == "int":
                assert isinstance(v, int) and not isinstance(v, bool), (name, c, v)
            elif t == "float":
                assert isinstance(v, (int, float)), (name, c, v)
for r in next(x for x in SHEETS if x[0] == "설정")[2]:
    assert r[2] in ("int", "float") and isinstance(r[3], (int, float)), r

# ---------- xlsx ----------
wb = Workbook()
wb.remove(wb.active)
bold = Font(bold=True)
gray = PatternFill("solid", fgColor="D9D9D9")
gray_font = Font(color="595959", italic=True)


def width(v):
    s = "" if v is None else str(v)
    return sum(2 if ord(ch) > 0x2E80 else 1 for ch in s)


for name, cols, data in SHEETS:
    ws = wb.create_sheet(name)
    ws.append([c for c, _ in cols])
    ws.append([t for _, t in cols])
    for r in data:
        ws.append(r)
    for cell in ws[1]:
        cell.font = bold
    for cell in ws[2]:
        cell.fill = gray
        cell.font = gray_font
    ws.freeze_panes = "A3"
    for i, (c, t) in enumerate(cols, 1):
        w = max([width(c), width(t)] + [width(r[i - 1]) for r in data])
        ws.column_dimensions[get_column_letter(i)].width = min(60, w + 2)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
wb.save(OUT)
print(OUT)
for name, cols, data in SHEETS:
    print(f"  {name}: {len(data)}행 × {len(cols)}열")
