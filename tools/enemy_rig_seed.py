"""몬스터 리그 동작의 첫 값: art_src/rig/enemy_rigs.json 에 idle·walk·행동·피격 동작을 써 넣는다.

python tools/enemy_rig_seed.py   (그다음 python tools/preview_enemy_rig.py <몬스터> walk out.png 로 확인)

씬에 조립한 뒤에는 씬(Arena/Enemies/Rigs/<종류>의 MonsterRig)이 원본이다. 여기 값은 처음 한 번 넣는 용도.
걷기 위상 u(0~1): near 다리는 u=0 에 앞으로 디딤 → u=.5 에 뒤로 밀고 → 들어 올려 앞으로(무릎은 u=.75 에 가장 굽음).
팔은 반대쪽 다리와 같이 흔들리고, 아래팔·머리는 조금 늦게 따라온다.
"""
import json
from pathlib import Path

P = Path(__file__).resolve().parents[1] / "art_src/rig/enemy_rigs.json"
S = json.loads(P.read_text(encoding="utf-8"))


def W(bone, offset=0, a1=0, p1=0, a2=0, p2=0): return dict(bone=bone, offset=offset, a1=a1, p1=p1, a2=a2, p2=p2)
def V(x=0, y=0): return {"x": x, "y": y}
def wave(offset=0, a1=0, p1=0, a2=0, p2=0): return dict(offset=offset, a1=a1, p1=p1, a2=a2, p2=p2)
def loop(length, bones, rootX=None, rootY=None): return dict(length=length, rootX=rootX or wave(), rootY=rootY or wave(), bones=bones)


def shot(length, peak, settle, bones, rs=(0, 0), rp=(0, 0), rset=(0, 0), keep=False, ease=False):
    # bones: {뼈: (시작, 최고, 정리)}
    return dict(length=length, peakTime=peak, settleTime=settle, keepEnd=keep, easeIn=ease,
                rootStart=V(*rs), rootPeak=V(*rp), rootSettle=V(*rset),
                bones=[dict(bone=b, start=v[0], peak=v[1], settle=v[2]) for b, v in bones.items()])


def follow(prev, length, peak, settle, peakv, settlev, rp, rset, keep=False):
    # 앞 동작의 정리 자세에서 이어 시작한다 (예고 → 마무리 사이가 튀지 않게)
    start = {b["bone"]: b["settle"] for b in prev["bones"]}
    rs = (prev["rootSettle"]["x"], prev["rootSettle"]["y"])
    names = list(dict.fromkeys(list(start) + list(peakv) + list(settlev)))
    return shot(length, peak, settle, {b: (start.get(b, 0), peakv.get(b, 0), settlev.get(b, 0)) for b in names}, rs, rp, rset, keep)


def legs(thigh, shin, extra=()):
    return [W("near_thigh", a1=thigh, p1=.75), W("far_thigh", a1=thigh, p1=.25),
            W("near_shin", offset=shin / 2, a1=shin / 2, p1=.5), W("far_shin", offset=shin / 2, a1=shin / 2, p1=0)] + list(extra)


def arms(a, fa, lag=.08):
    out = [W("near_arm", a1=a, p1=.25), W("far_arm", a1=a, p1=.75)]
    if fa: out += [W("near_forearm", a1=fa, p1=.25 - lag), W("far_forearm", a1=fa, p1=.75 - lag)]
    return out


def hurt(extra=None):
    b = {"torso": (0, -12, -4), "head": (0, -16, -5), "near_arm": (0, 22, 6), "far_arm": (0, 18, 5)}
    b.update(extra or {})
    return shot(.32, .05, .16, b, rp=(-10, 2), rset=(-3, 0))


# ---- stalker: 웅크린 채 종종걸음, 할퀴기
st = S["stalker"]
st.update(height=390, stride=258, maxStepRate=3.2, walkFull=45, walkFollow=8)
st["idle"] = loop(1.8, [W("torso", a1=1.5, p1=.05), W("head", a1=-2.5, p1=.15), W("near_arm", a1=3, p1=.1), W("far_arm", a1=3, p1=.3),
                        W("near_forearm", a1=4, p1=.2), W("far_forearm", a1=4, p1=.4)], rootY=wave(a1=3))
st["walk"] = loop(1, legs(22, 28) + arms(18, 10) + [W("torso", offset=4, a2=1.5, p2=.125), W("head", offset=-3, a2=-2.5, p2=.2)],
                  rootY=wave(a2=6, p2=.125))
st["melee"] = shot(.43, .3, .43, {"torso": (0, -14, -16), "head": (0, 8, 10), "near_arm": (0, -125, -135), "near_forearm": (0, -20, -25),
                                  "far_arm": (0, 25, 30), "near_thigh": (0, -12, -14), "near_shin": (0, 22, 26), "far_thigh": (0, -6, -8), "far_shin": (0, 18, 22)},
                   rp=(-8, 14), rset=(-10, 16), keep=True, ease=True)
st["strike"] = follow(st["melee"], .37, .07, .2,
                      {"torso": 22, "head": -6, "near_arm": 25, "near_forearm": 10, "far_arm": -20, "near_thigh": -20, "near_shin": 20, "far_thigh": 15, "far_shin": 10},
                      {"torso": 8, "near_arm": 10, "far_arm": -6}, (22, 6), (8, 0))
st["hurt"] = hurt()

# ---- charger: 새 다리 성큼걸음 + 머리 까딱, 웅크렸다 돌진
ch = S["charger"]
ch.update(height=470, stride=369, maxStepRate=3.5, walkFull=45, walkFollow=8)
ch["idle"] = loop(1.4, [W("torso", a1=1.5), W("head", a1=3, p1=.2), W("near_arm", a1=4, p1=.1), W("far_arm", a1=4, p1=.35),
                        W("near_forearm", a1=4, p1=.2), W("far_forearm", a1=4, p1=.45)], rootY=wave(a1=3))
ch["walk"] = loop(1, legs(20, 24, [W("near_foot", offset=-4, a1=12, p1=.45), W("far_foot", offset=-4, a1=12, p1=.95)]) + arms(15, 10)
                  + [W("torso", offset=3, a2=2, p2=.125), W("head", offset=2, a2=-5, p2=.05)], rootY=wave(a2=8, p2=.125))
ch["dash"] = shot(.68, .45, .68, {"torso": (0, -14, -16), "head": (0, -14, -16), "near_arm": (0, 45, 50), "far_arm": (0, 40, 45),
                                  "near_forearm": (0, 20, 25), "far_forearm": (0, 20, 25), "near_thigh": (0, -25, -28), "far_thigh": (0, -25, -28),
                                  "near_shin": (0, 35, 40), "far_shin": (0, 35, 40), "near_foot": (0, -10, -12), "far_foot": (0, -10, -12)},
                  rp=(-18, 30), rset=(-20, 32), keep=True, ease=True)
lean = {"torso": 24, "head": 18, "near_arm": 70, "far_arm": 65, "near_forearm": 30, "far_forearm": 30}
ch["charge"] = follow(ch["dash"], .53, .1, .53, lean, lean, (10, 10), (10, 10), keep=True)
ch["brake"] = follow(ch["charge"], .9, .15, .5, {"torso": -12, "head": -10, "near_arm": 20, "far_arm": 20, "near_thigh": -20, "near_shin": 25},
                     {}, (-10, 6), (0, 0))
ch["hurt"] = hurt()

# ---- shooter: 뒤뚱걸음, 고개 젖혀 입 벌렸다 쏘기
sh = S["shooter"]
sh.update(height=465, stride=315, maxStepRate=3, walkFull=40, walkFollow=8)
sh["idle"] = loop(2.0, [W("torso", a1=1.2), W("head", a1=-2, p1=.15), W("jaw", offset=3, a1=3, p1=.3), W("near_arm", a1=4, p1=.1), W("far_arm", a1=4, p1=.35)],
                  rootY=wave(a1=4))
sh["walk"] = loop(1, [W("near_leg", a1=22, p1=.75), W("far_leg", a1=22, p1=.25), W("torso", a1=3, a2=1.5, p2=.125), W("head", a1=-3, p1=.1, a2=2, p2=.2),
                      W("jaw", offset=4, a2=5, p2=.3)] + arms(14, 0), rootY=wave(a2=7, p2=.125))
sh["shot"] = shot(.43, .3, .43, {"head": (0, -18, -20), "jaw": (0, 30, 34), "torso": (0, -8, -9), "near_arm": (0, -40, -45), "far_arm": (0, 30, 34)},
                  rp=(0, 6), rset=(0, 7), keep=True, ease=True)
sh["fire"] = follow(sh["shot"], .44, .06, .25, {"head": 14, "jaw": 40, "torso": 6, "near_arm": 20, "far_arm": -10}, {"jaw": 6}, (-12, 0), (-2, 0))
sh["hurt"] = hurt({"jaw": (0, 25, 6)})

# ---- boss: 치마 끌며 미끄러지듯, 팔 들어 쏘기·뒤로 젖혔다 돌진
bo = S["boss"]
bo.update(height=767, stride=330, maxStepRate=2, walkFull=35, walkFollow=6)
bo["idle"] = loop(2.4, [W("skirt", a1=1.5, p1=.1), W("torso", a1=1.5), W("head", a1=-2, p1=.15), W("near_arm", a1=4, p1=.1), W("far_arm", a1=4, p1=.3),
                        W("near_forearm", a1=5, p1=.2), W("far_forearm", a1=5, p1=.4)], rootY=wave(a1=4))
bo["walk"] = loop(1, [W("skirt", offset=3, a1=4, a2=1.5, p2=.2), W("torso", offset=2, a1=-2, p1=.1), W("head", a1=2.5, p1=.2)] + arms(10, 8),
                  rootY=wave(a2=5, p2=.125), rootX=wave(a2=3, p2=.2))
bo["shot"] = shot(.43, .3, .43, {"near_arm": (0, -110, -118), "near_forearm": (0, -20, -24), "far_arm": (0, -60, -64), "torso": (0, -6, -7), "head": (0, -10, -12)},
                  keep=True, ease=True)
bo["fire"] = follow(bo["shot"], .44, .07, .25, {"near_arm": -70, "near_forearm": 0, "far_arm": -40, "torso": 6, "head": 6}, {}, (-8, 0), (0, 0))
bo["dash"] = shot(.68, .45, .68, {"torso": (0, -10, -12), "head": (0, -8, -10), "near_arm": (0, 40, 45), "far_arm": (0, 35, 40), "skirt": (0, -6, -7)},
                  rp=(-6, 10), rset=(-8, 12), keep=True, ease=True)
lean = {"torso": 14, "head": 10, "near_arm": 45, "far_arm": 40, "skirt": 14}
bo["charge"] = follow(bo["dash"], .53, .12, .53, lean, lean, (0, 4), (0, 4), keep=True)
bo["brake"] = follow(bo["charge"], .9, .18, .5, {"torso": -8, "skirt": -8, "head": -6, "near_arm": 15, "far_arm": 15}, {}, (-6, 0), (0, 0))
bo["hurt"] = hurt({"skirt": (0, 5, 2)})

# ---- 접지: 발마다 뒤꿈치·발끝 (파츠 그림 px). 가장 낮은 점을 바닥에 맞춘다. 보스는 치마라 끔.
def feet(bones, heel, toe): return [{"bone": b, "sole": s, "depth": 0} for b in bones for s in (heel, toe)]
st.update(groundFollow=1, maxGroundCorrection=60, feet=feet(("near_shin", "far_shin"), [18, 140], [122, 136]))
ch.update(groundFollow=1, maxGroundCorrection=60, feet=feet(("near_foot", "far_foot"), [22, 80], [130, 88]))
sh.update(groundFollow=1, maxGroundCorrection=40, feet=feet(("near_leg", "far_leg"), [10, 97], [104, 97]))
bo.update(groundFollow=0, maxGroundCorrection=0, feet=[])

P.write_text(json.dumps(S, ensure_ascii=False, indent=1), encoding="utf-8")
print("wrote", P)
