import { addItem, gearStats, saveProfile } from './gear.js';
import { FLIPBOOKS } from './flipbooks.js';

export const WIDTH = 1000;
export const HEIGHT = 700;
export const PROJECTION_Y = .67;
export const FINAL_FLOOR = 5;
export const ROUND_DURATION = [0, 24, 28, 0];
export const clamp = (n, min, max) => Math.min(max, Math.max(min, n));
export const distance = (p, q) => Math.hypot(p.x - q.x, p.y - q.y);
export const xpNeeded = r => 5 + (r.level - 1) * 3;
export const yarnMax = r => Math.max(2, Math.round(6 + r.gear.yarn + r.yarnBonus - (r.desires.has('obsession') ? 2 : 0)));
export const desireFraction = r => r.desire / r.maxDesire;
export const knotInterval = r => r.rules.has('earlyKnot') ? 4 : 5;
export const enemyHeight = e => ({ stalker: 57, charger: 62, shooter: 69, boss: 132 }[e.type] * (e.elite ? 1.11 : 1));

const ATTACK_Y = .74;
const WORLD_VERTICAL = ATTACK_Y / PROJECTION_Y;
const NEEDLE_REACH = 420;
// Needles kick out of the hand (a short punch of acceleration), then drag to a heavy stop near the end of their reach.
const NEEDLE_SPEED = { launch: 3100, settle: 560, kick: .04, start: .38 };
const needleSpeed = (thread, now) => {
  const left = clamp(thread.range / NEEDLE_REACH, 0, 1);
  const cruise = NEEDLE_SPEED.settle + (NEEDLE_SPEED.launch - NEEDLE_SPEED.settle) * left ** 2.6;
  const kick = clamp((now - thread.birth) / NEEDLE_SPEED.kick, 0, 1);
  return cruise * (NEEDLE_SPEED.start + (1 - NEEDLE_SPEED.start) * (1 - (1 - kick) ** 3));
};
// Feel knobs for needle hits and the pull cut.
const RECOIL = { hero: 9, time: .13, camera: 3.5 };
const STICK = { freeze: .035, push: 11, time: .13 };
const CUT = { time: .26, split: 7, flash: .11 };
const HURT_FLASH = .45; // seconds the red screen edge lasts after the hero takes a hit
const LEFT = 25, RIGHT = WIDTH - 25, TOP = 24, BOTTOM = HEIGHT - 25;
const DASH = { duration: .17, grace: .12, distance: 150, ease: 2.2, slow: .4, ghostInterval: .022, ghostLife: .5 };
const dashTravel = u => DASH.distance * (1 - (1 - clamp(u, 0, 1)) ** DASH.ease);
const YANK = { reach: 34, boss: 10, time: .16 };

// The thread element follows the strongest active set on the sewing board.
export function threadElement(groups) {
  let best = 'plain';
  let score = 0;
  for (const set of ['chaos', 'despair', 'frustration']) {
    const group = groups?.[set];
    const value = group ? group.tier * 100 + group.size : 0;
    if (group?.tier && value > score) { best = set; score = value; }
  }
  return best;
}

export const AUGMENTS = [
  { id: 'bounce', name: '되돌아오는 바늘', text: '벽에 맞은 바늘이 한 번 튕깁니다.', rule: true, icon: 'needle' },
  { id: 'dashThread', name: '질주의 흔적', text: '대쉬한 길에 실을 깔아 지나가는 적을 꿰맵니다.', rule: true, icon: 'yarn' },
  { id: 'earlyKnot', name: '빠른 매듭', text: '바늘 4발마다 매듭을 묶습니다.', rule: true, icon: 'knot' },
  { id: 'echo', name: '남은 올', text: '당긴 후 0.5초 안에 한 번 더 당길 수 있습니다.', rule: true, icon: 'yarn' },
  { id: 'fire', name: '재봉사의 손놀림', text: '바늘 발사 속도가 15% 빨라집니다.', icon: 'needle' },
  { id: 'damage', name: '팽팽한 실', text: '실 한 가닥의 피해가 4 증가합니다.', icon: 'yarn' },
  { id: 'supply', name: '여분의 실패', text: '털실 최대 +2 · 충전 속도 +20%', icon: 'yarn' },
  { id: 'light', name: '가벼운 매듭', text: '매듭 감속 −30% · 대쉬 대기시간 −20%', icon: 'knot' },
  { id: 'calm', name: '심호흡', text: '현재 욕망을 즉시 15 낮춥니다.', icon: 'desire' },
];

export const DESIRES = [
  { id: 'obsession', name: '집착', text: '적이 죽으면 실이 가까운 적에게 옮겨 붙습니다.', penalty: '털실 최대 −2' },
  { id: 'rage', name: '분노', text: '매듭 3개 이상에서 당기기 피해가 두 배가 됩니다.', penalty: '받는 피해 +20%' },
  { id: 'hunger', name: '갈망', text: '적을 처치할 때 체력을 1 회복합니다.', penalty: '회복·정화 포션 효과 −50%' },
];

const TYPES = {
  stalker: { hp: 25, speed: 83, damage: 10, radius: 18, wait: 1.15, gold: 3 },
  charger: { hp: 40, speed: 87, damage: 13, radius: 23, wait: 1.6, gold: 5 },
  shooter: { hp: 34, speed: 75, damage: 9, radius: 19, wait: 2.2, gold: 5 },
  boss: { hp: 238, speed: 68, damage: 17, radius: 36, wait: 1.1, gold: 25 },
};

const position = (x, y) => ({ x, y });

function unit(x, y) {
  const length = Math.hypot(x, y) || 1;
  return { x: x / length, y: y / length };
}

export function lineDistance(p, a, b) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const length = dx * dx + dy * dy || 1;
  const t = clamp(((p.x-a.x) * dx + (p.y-a.y) * dy) / length, 0, 1);
  return Math.hypot(p.x - a.x - dx * t, p.y - a.y - dy * t);
}

function firstImpact(a, b, enemies) {
  const dx = b.x - a.x;
  const dy = (b.y - a.y) / WORLD_VERTICAL;
  const lengthSq = dx * dx + dy * dy;
  let first = null;
  let earliest = Infinity;
  for (const e of enemies) {
    if (e.hp <= 0) continue;
    const x = a.x - e.x;
    const y = (a.y - e.y) / WORLD_VERTICAL;
    const offset = x * x + y * y - e.radius ** 2;
    const dot = x * dx + y * dy;
    const discriminant = dot * dot - lengthSq * offset;
    if (discriminant < 0) continue;
    const t = offset <= 0 ? 0 : (-dot - Math.sqrt(discriminant)) / lengthSq;
    if (t >= 0 && t <= 1 && t < earliest) {
      earliest = t;
      first = e;
    }
  }
  return first;
}

export function threadEndpoints(player, thread) {
  return { start: thread.anchor || player, end: thread.target || thread };
}

export function createRun(profile, random = Math.random) {
  const { stats: gear, groups } = gearStats(profile);
  const hp = Math.round(100 + gear.health);
  return {
    profile, random, gear, floor: 1, round: 1, time: 0, elapsed: 0, spawnIn: .85, spawned: 0, level: 1, xp: 0,
    element: threadElement(groups), gold: 0, kills: 0, desire: 0, maxDesire: 100 + profile.capacity * 10, mood: 'normal',
    hp, maxHp: hp, yarn: Math.round(6 + gear.yarn), yarnBonus: 0, recharge: 1 + gear.recharge,
    damage: 14 + gear.damage, fireRate: 1, dashRate: 1, encumbrance: .14, shotCount: 0,
    winding: 0, dry: 0, nextKnot: 0,
    player: { x: WIDTH / 2, y: HEIGHT / 2, facing: 1, vx: 0, vy: 0, dash: 0, dashGrace: 0,
      dashCooldown: 0, ghostIn: 0, hurt: 0, shotCooldown: 0, angle: -.45, motion: 0, strideTime: 0, action: 'idle', actionTime: 0 },
    knots: [], enemies: [], threads: [], bullets: [], drops: [], trails: [], effects: [],
    rules: new Set(), desires: new Set(), augmentQueue: 0, freeze: 0, shake: 0, combo: 0, kick: null, flash: 0,
    echo: null, outcome: null, phase: 'combat', events: [],
  };
}

function event(r, type, message) { r.events.push({ type, message }); }

export function adjustDesire(r, value) {
  r.desire = clamp(r.desire + value, 0, r.maxDesire);
  if (r.desire < r.maxDesire) return;
  if (r.mood === 'frenzy') {
    r.outcome = 'absorbed';
    return;
  }
  r.mood = r.random() < .25 ? 'awakened' : 'frenzy';
  r.desire = r.maxDesire * (r.mood === 'awakened' ? .5 : .7);
  r.effects.push({ kind: 'sprite', sprite: r.mood === 'awakened' ? 'stitch' : 'boss', ...r.player, size: 248, time: 1.05, max: 1.05 });
  event(r, 'desire', r.mood === 'awakened' ? '각성 · 실 피해 +20%' : '폭주 · 이동과 충전이 느려집니다');
}

export function beginFloor(r) {
  r.round = 1;
  r.player.x = WIDTH / 2;
  r.player.y = HEIGHT / 2;
  adjustDesire(r, Math.max(0, Math.round(5 * r.floor - r.gear.purity)));
  if (!r.outcome) beginRound(r);
}

export function beginRound(r) {
  r.phase = 'combat';
  r.elapsed = 0;
  r.spawned = 0;
  r.spawnIn = .85;
  r.bullets = [];
  r.threads = [];
  r.knots = [];
  r.trails = [];
  r.echo = null;
  r.shotCount = 0;
  r.winding = 0;
  r.dry = 0;
  r.nextKnot = 0;
  r.yarn = yarnMax(r);
  if (r.round === 3) spawnEnemy(r, 'boss', { x: WIDTH / 2, y: 120 });
  event(r, 'round', `${r.floor}층 · ${r.round === 3 ? '층 보스' : `${r.round}라운드`}`);
}

export function augmentChoices(r) {
  const pool = AUGMENTS.filter(a => !a.rule || !r.rules.has(a.id));
  const choices = [];
  while (choices.length < 3) {
    let pick = r.random() * pool.reduce((n, a) => n + (a.rule ? 2 : 1), 0);
    const index = pool.findIndex(a => (pick -= a.rule ? 2 : 1) < 0);
    choices.push(pool.splice(index, 1)[0]);
  }
  return choices;
}

export function applyAugment(r, id) {
  if (AUGMENTS.find(a => a.id === id)?.rule) r.rules.add(id);
  switch (id) {
    case 'fire': r.fireRate *= 1.15; break;
    case 'damage': r.damage += 4; break;
    case 'supply': r.yarnBonus += 2; r.recharge *= 1.2; r.yarn = Math.min(yarnMax(r), r.yarn + 2); break;
    case 'light': r.encumbrance *= .7; r.dashRate *= .8; break;
    case 'calm': adjustDesire(r, -15); break;
  }
  r.augmentQueue--;
}

export function chooseDesire(r, id) {
  if (id) {
    if (id === 'refine') r.damage += 4;
    else r.desires.add(id);
    r.yarn = Math.min(r.yarn, yarnMax(r));
    adjustDesire(r, 20);
  }
  if (!r.outcome) r.phase = r.floor === FINAL_FLOOR ? 'victory' : 'shop';
}

function spawnEnemy(r, type, location, elite = false) {
  const def = TYPES[type];
  if (!location) {
    const edge = Math.floor(r.random() * 4);
    location = edge < 2 ? position(edge ? WIDTH-25 : 25, 55+r.random()*(HEIGHT-110)) :
      position(45+r.random()*(WIDTH-90), edge === 2 ? 27 : HEIGHT-27);
    if (distance(location, r.player) < 180) location.x = WIDTH - location.x;
  }
  const hp = Math.round(def.hp * (1 + .18*(r.floor-1)) * (elite ? 1.6 : 1));
  const enemy = { ...location, type, elite, hp, maxHp: hp, radius: def.radius, wait: def.wait * (.85 + r.random()*.3),
    speed: def.speed*(1+.04*(r.floor-1)), damage: def.damage*(1+.10*(r.floor-1))*(elite ? 1.2 : 1),
    action: 'move', timer: 0, aim: position(1,0), facing: 1, threshold: 0, flash: 0 };
  r.enemies.push(enemy);
  return enemy;
}

function gainXp(r, amount) {
  r.xp += amount;
  while (r.xp >= xpNeeded(r)) {
    r.xp -= xpNeeded(r);
    r.level++;
    r.augmentQueue++;
  }
}

function collect(r, drop) {
  gainXp(r, drop.xp);
  r.gold += drop.gold;
  if (drop.shard) r.profile.shards += drop.shard;
  if (drop.item) event(r, 'loot', `${addItem(r.profile).name} 획득`);
  if (drop.shard || drop.item) saveProfile(r.profile);
}

function kill(r, e, stuck) {
  r.kills++;
  const boss = e.type === 'boss';
  r.drops.push({ x: e.x, y: e.y, xp: boss ? 10 : e.elite ? 3 : 1, gold: TYPES[e.type].gold * (e.elite ? 2 : 1), shard: boss ? 5 : e.elite ? 1 : 0,
    item: boss || (e.elite && r.random() < .64) });
  if (r.desires.has('hunger')) r.hp = Math.min(r.maxHp, r.hp+1);
  if (stuck.length && r.desires.has('obsession')) {
    const nearby = r.enemies.filter(other => other.hp > 0 && other !== e).sort((a,b) => distance(a,e)-distance(b,e))[0];
    if (nearby) r.threads.push(...stuck.map(thread => ({ ...thread, target: nearby })));
  }
  if (e.cut) r.effects.push({ kind: 'sliced', type: e.type, elite: e.elite, x: e.x, y: e.y, facing: e.facing, angle: e.cut.angle,
    side: e.cut.side, time: .62, max: .62 });
  else r.effects.push({ kind: 'burst', x: e.x, y: e.y, time: .38, max: .38, tint: boss ? '#eb96b5' : '#b99aa6' });
}

function damagePlayer(r, amount) {
  const p = r.player;
  if (p.dash > 0 || p.dashGrace > 0) {
    if (!p.dodged) {
      p.dodged = true;
      r.effects.push({ kind: 'number', x: p.x, y: p.y, text: '회피!', time: .62, max: .62, tint: '#9fdcff' });
      r.effects.push({ kind: 'sprite', sprite: 'shock', x: p.x, y: p.y, lift: 30, size: 150, time: .34, max: .34, tint: 'dodge', burst: true });
      event(r, 'dodge');
    }
    return false;
  }
  if (p.hurt > 0) return false;
  const dealt = Math.round(amount * (r.desires.has('rage') ? 1.2 : 1));
  r.hp = Math.max(0, r.hp - dealt);
  p.hurt = .62;
  act(p, 'hurt');
  r.shake = .18;
  // Hit feedback: red screen edge (render.js), red hero tint + blink, damage number, small red burst.
  r.hurtFlash = HURT_FLASH;
  r.effects.push({ kind: 'number', x: p.x, y: p.y, lift: 96, text: `-${dealt}`, time: .7, max: .7, tint: '#ff5a5a' });
  r.effects.push({ kind: 'burst', x: p.x, y: p.y, time: .3, max: .3, tint: '#e0464f' });
  event(r, 'hurt');
  if (r.hp === 0) { act(p, 'die'); r.outcome = 'dead'; }
  return true;
}

function act(player, action) {
  player.previousPose = player.action === action ? null : {
    action: player.action, actionTime: player.actionTime, strideTime: player.strideTime,
    angle: player.angle, motion: player.motion,
  };
  player.action = action;
  player.actionTime = 0;
}

function aimAtEnemy(r, pointer) {
  let target = pointer;
  for (const e of r.enemies) {
    const height = enemyHeight(e);
    const dx = (pointer.x - e.x) / (e.radius + 3);
    const dy = ((pointer.y - e.y) * PROJECTION_Y + height / 2) / (height / 2 + 10);
    if (e.hp > 0 && dx * dx + dy * dy < 1 && (target === pointer || target.y < e.y)) target = e;
  }
  return target;
}

function rangeToWall(thread) {
  const x = thread.vx < 0 ? LEFT : RIGHT;
  const y = thread.vy < 0 ? TOP : BOTTOM;
  return Math.max(0, Math.min(thread.vx ? (x - thread.x) / thread.vx : Infinity,
    thread.vy ? (y - thread.y) / thread.vy : Infinity));
}

function fire(r, pointer) {
  const p = r.player;
  if (p.shotCooldown > 0 || p.dash > 0) return;
  if (r.yarn < 1) {
    if (!r.dry) event(r, 'empty', '털실이 바닥났어요.');
    r.dry = .6;
    return;
  }
  const aim = aimAtEnemy(r, pointer);
  const dx = aim.x - p.x;
  const dy = (aim.y - p.y) * PROJECTION_Y;
  if (Math.hypot(dx, dy) < 3) return;
  const dir = unit(dx, dy);
  r.yarn -= 1;
  p.shotCooldown = .34 / r.fireRate;
  p.facing = dir.x >= 0 ? 1 : -1;
  p.angle = Math.atan2(dir.y, dir.x);
  act(p, 'shoot');
  r.threads.push({ x: p.x, y: p.y, vx: dir.x, vy: dir.y / PROJECTION_Y, speed: NEEDLE_SPEED.launch * NEEDLE_SPEED.start,
    range: NEEDLE_REACH, anchor: null, target: null, state: 'flight', bounce: r.rules.has('bounce'), birth: r.time, element: r.element });
  const screenAngle = Math.atan2(dir.y, dir.x);
  // Recoil: the hero is shoved back against the throw and springs forward again; the camera twitches with it.
  p.recoil = { x: -dir.x, y: -dir.y, time: RECOIL.time, max: RECOIL.time };
  r.kick = { x: -dir.x * RECOIL.camera, y: -dir.y * RECOIL.camera, time: .08, max: .08 };
  r.effects.push({ kind: 'flip', sheet: 'muzzle', x: p.x + dir.x * 20, y: p.y + dir.y / PROJECTION_Y * 20, lift: 34,
    angle: screenAngle, size: 150, tint: r.element, ...flipTime('muzzle') });
  r.shotCount++;
  event(r, 'shoot');
  if (r.shotCount >= knotInterval(r)) {
    const knot = { x: p.x, y: p.y, index: r.nextKnot++ };
    for (const thread of r.threads) if (!thread.anchor) thread.anchor = knot;
    r.knots.push(knot);
    r.shotCount = 0;
    event(r, 'knot');
  }
}

const flipTime = sheet => ({ time: FLIPBOOKS[sheet].duration, max: FLIPBOOKS[sheet].duration });

function attach(r, thread, e) {
  thread.target = e;
  thread.state = 'stuck';
  thread.landed = r.time;
  const dir = unit(thread.vx || 0, (thread.vy || 0) * PROJECTION_Y);
  thread.bite = dir; // the needle drives in past the surface, then springs back
  e.flash = .08;
  e.jolt = { x: dir.x, y: dir.y, time: .16, max: .16 };
  // Physical shove along the needle's path, eased like the pull yank.
  const push = e.type === 'boss' ? STICK.push * .25 : STICK.push;
  e.push = { x: dir.x * push, y: dir.y / PROJECTION_Y * push, done: 0, time: STICK.time };
  r.freeze = Math.max(r.freeze, STICK.freeze);
  r.shake = Math.max(r.shake, .06);
  const angle = Math.atan2(dir.y, dir.x);
  r.effects.push({ kind: 'flip', sheet: 'puncture', x: e.x, y: e.y, angle, size: 150, tint: thread.element, ...flipTime('puncture') });
}

function updateNeedles(r, dt) {
  for (const thread of r.threads) {
    if (thread.state !== 'flight') continue;
    const wall = rangeToWall(thread);
    thread.speed = needleSpeed(thread, r.time);
    const step = Math.min(thread.range, wall, thread.speed * dt);
    const next = position(thread.x + thread.vx * step, thread.y + thread.vy * step);
    const hit = firstImpact(thread, next, r.enemies);
    thread.x = next.x;
    thread.y = next.y;
    thread.range -= step;
    if (hit) attach(r, thread, hit);
    else if (wall <= step && thread.range > 0 && thread.bounce) {
      if (thread.x <= LEFT + .001 || thread.x >= RIGHT - .001) thread.vx *= -1;
      if (thread.y <= TOP + .001 || thread.y >= BOTTOM - .001) thread.vy *= -1;
      thread.bounce = false;
    } else if (wall <= step || thread.range <= 0) {
      thread.state = 'ground';
      thread.landed = r.time;
      r.shake = Math.max(r.shake, .03);
      r.effects.push({ kind: 'flip', sheet: 'thunk', ...next, lift: 4, size: 96, squash: true, tint: thread.element, ...flipTime('thunk') });
    }
  }
}

function pull(r) {
  const p = r.player;
  const primary = r.threads;
  const echoed = !primary.length && Boolean(r.echo);
  const threads = echoed ? r.echo.threads.filter(t => !t.target || t.target.hp > 0) : primary;
  if (!threads.length) {
    event(r, 'empty', '먼저 바늘로 실을 꿰매세요.');
    return;
  }
  const rage = r.knots.length >= 3 && r.desires.has('rage');
  if (!echoed) {
    r.threads = [];
    r.yarn = Math.min(yarnMax(r), r.yarn + Math.ceil(threads.length / 2));
    r.echo = r.rules.has('echo') ? { until: r.time + .5, threads: threads.map(t => ({ ...t })) } : null;
  } else r.echo = null;
  r.knots = [];
  r.shotCount = 0;
  act(p, 'pull');

  const living = r.enemies.filter(e => e.hp > 0);
  const pinned = new Set(threads.map(t => t.target).filter(Boolean));
  const direct = new Map();
  const cutAngle = new Map();
  let impacts = 0;
  for (const thread of threads) {
    const { start, end } = threadEndpoints(p, thread);
    r.effects.push({ kind: 'pull', x: start.x, y: start.y, toX: end.x, toY: end.y, time: .3, max: .3,
      element: thread.element || r.element, rage, seed: r.random() * 100 });
    // The strand itself becomes a blade: a razor line from the anchor straight through the target and past it.
    const along = Math.atan2((end.y - start.y) * PROJECTION_Y, end.x - start.x);
    r.effects.push({ kind: 'cut', x: start.x, y: start.y, toX: end.x, toY: end.y, angle: along, time: CUT.time, max: CUT.time,
      element: thread.element || r.element });
    const a = position(start.x, start.y / WORLD_VERTICAL);
    const b = position(end.x, end.y / WORLD_VERTICAL);
    for (const e of living) {
      if (e !== thread.target && lineDistance(position(e.x, e.y / WORLD_VERTICAL), a, b) > e.radius + 4) continue;
      direct.set(e, (direct.get(e) || 0) + 1);
      if (!cutAngle.has(e) || e === thread.target) cutAngle.set(e, along);
      impacts++;
    }
  }
  const damage = r.damage * (rage ? 2 : 1) * (r.mood === 'awakened' ? 1.2 : 1);
  const wounded = [];
  let transfers = 0;
  for (const e of living) {
    const own = direct.get(e) || 0;
    const transferred = pinned.has(e) ? impacts - own : 0;
    const count = own + transferred;
    if (!count) continue;
    wounded.push(e);
    if (transferred) transfers++;
    const dealt = Math.round(damage * count);
    e.hp = Math.max(0, e.hp - dealt);
    e.flash = .21;
    const dy = (p.y-e.y) * PROJECTION_Y;
    const gap = Math.hypot(p.x-e.x, dy) || 1;
    // The yank is eased over a few frames instead of teleporting the enemy.
    const yank = Math.min(e.type === 'boss' ? YANK.boss : YANK.reach, Math.max(0, gap - 75));
    const toward = position((p.x-e.x) / gap, (p.y-e.y) / gap);
    e.yank = { x: toward.x * yank, y: toward.y * yank, done: 0, time: YANK.time };
    e.stretch = { x: toward.x, y: toward.y * PROJECTION_Y, time: .24 };
    const element = threads.find(t => t.target === e)?.element || r.element;
    const angle = Math.atan2(toward.y * PROJECTION_Y, toward.x);
    r.effects.push({ kind: 'number', x: e.x, y: e.y, text: dealt, time: .72, max: .72, tint: transferred ? '#ffd293' : '#d9fff4', pop: Math.min(count, 6) });
    // Gwen-style snip: a crescent slash across the strand, scissors closing on heavy hits, and the body split along the cut.
    const cut = cutAngle.get(e) ?? angle;
    const heavy = count >= 2 || e.hp <= 0;
    const big = e.type === 'boss' ? 1.6 : 1;
    e.cut = { angle: cut, time: CUT.time, max: CUT.time, side: r.random() < .5 ? -1 : 1 };
    r.effects.push({ kind: 'flip', sheet: 'slash', x: e.x, y: e.y, lift: enemyHeight(e) * .5, angle: cut, mirror: e.cut.side < 0,
      size: (170 + Math.min(count, 5) * 14) * big, tint: element, ...flipTime('slash') });
    if (heavy) r.effects.push({ kind: 'flip', sheet: 'snip', x: e.x, y: e.y, lift: enemyHeight(e) * .5, angle: cut + Math.PI / 2,
      size: (150 + Math.min(count, 5) * 12) * big, tint: element, ...flipTime('snip') });
    r.effects.push({ kind: 'flip', sheet: 'flash', x: e.x, y: e.y, lift: enemyHeight(e) * .5, angle: cut,
      size: 150 * big, tint: element, ...flipTime('flash') });
    if (transferred) {
      const source = [...direct.keys()].find(other => other !== e);
      if (source) r.effects.push({ kind: 'transfer', x: source.x, y: source.y, toX: e.x, toY: e.y, time: .34, max: .34 });
    }
  }
  for (const e of wounded) {
    if (e.hp <= 0) kill(r, e, threads.filter(t => t.target === e));
    else if (e.type === 'boss') bossThresholds(r, e);
  }
  r.combo = impacts;
  r.freeze = impacts ? Math.min(.17, .08 + impacts*.016) : 0;
  r.shake = impacts ? Math.min(.37, .11 + impacts*.038) : .05;
  if (impacts) {
    // The camera kicks toward the hero, the same way the threads were yanked.
    let kx = 0, ky = 0;
    for (const e of wounded) { const u = unit(p.x - e.x, (p.y - e.y) * PROJECTION_Y); kx += u.x; ky += u.y; }
    const k = unit(kx, ky);
    const power = Math.min(16, 6 + impacts * 1.6);
    r.kick = { x: k.x * power, y: k.y * power, time: .2, max: .2 };
    r.flash = Math.min(.16, .05 + impacts * .018);
    r.flashTint = '#ffe6f1';
    // Impact frame: one hard white frame across the screen, held through the hitstop.
    r.snapFlash = CUT.flash;
    r.effects.push({ kind: 'flip', sheet: 'flash', x: p.x, y: p.y, lift: 34, angle: 0, size: 150 + Math.min(impacts, 8) * 8,
      tint: r.element, alpha: .45, ...flipTime('flash') });
    if (impacts >= 2) r.effects.push({ kind: 'combo', x: p.x, y: p.y, text: `${impacts} HIT`, time: .8, max: .8, tint: rage ? '#ffb27a' : '#fff1f7' });
  }
  event(r, 'pull', impacts ? `실 명중 ${impacts}회${transfers ? ` · 피해 전달 ${transfers}체` : ''}` : '실을 거뒀습니다');
}

function bossThresholds(r, e) {
  const ratio = e.hp / e.maxHp;
  while (e.threshold < 2 && ratio <= [.66,.33][e.threshold]) {
    e.threshold++;
    r.effects.push({ kind: 'sprite', sprite: 'boss', x: e.x, y: e.y, size: 215, time: .64, max: .64 });
    for (const dx of [-83,83]) spawnEnemy(r, e.threshold === 2 ? 'charger' : 'stalker',
      position(clamp(e.x+dx,27,WIDTH-27), clamp(e.y+65,27,HEIGHT-27)));
    event(r, 'round', '실타래의 지배자가 하수인을 불러냅니다');
  }
}

function enemyShots(r, e, circle) {
  const angled = Math.atan2(e.aim.y,e.aim.x);
  const count = circle ? 13 : e.type === 'boss' || r.floor > 2 ? 5 : 3;
  for (let i=0; i<count; i++) {
    const angle = circle ? angled + i * Math.PI*2/count : angled + (i-(count-1)/2)*.24;
    const speed = circle ? 238 : 280;
    r.bullets.push({ x: e.x, y: e.y, vx: Math.cos(angle)*speed, vy: Math.sin(angle)*speed, hit: e.damage, age: 0 });
  }
  event(r,'enemyShot');
}

function updateEnemy(r, e, dt) {
  const p = r.player;
  const gap = distance(p,e);
  const near = unit(p.x-e.x,p.y-e.y);
  const boss = e.type === 'boss';
  const enraged = boss && e.hp / e.maxHp <= .5;
  e.flash = Math.max(0,e.flash-dt);
  if (e.stretch && (e.stretch.time -= dt) <= 0) e.stretch = null;
  if (e.jolt && (e.jolt.time -= dt) <= 0) e.jolt = null;
  if (e.push) {
    const eased = 1 - clamp((e.push.time - dt) / STICK.time, 0, 1) ** 3;
    e.x += e.push.x * (eased - e.push.done);
    e.y += e.push.y * (eased - e.push.done);
    e.push.done = eased;
    if ((e.push.time -= dt) <= 0) e.push = null;
  }
  if (e.cut && (e.cut.time -= dt) <= 0) e.cut = null;
  if (e.yank) {
    const eased = 1 - clamp((e.yank.time - dt) / YANK.time, 0, 1) ** 3;
    e.x += e.yank.x * (eased - e.yank.done);
    e.y += e.yank.y * (eased - e.yank.done);
    e.yank.done = eased;
    if ((e.yank.time -= dt) <= 0) e.yank = null;
  }
  e.timer -= dt;
  if (e.action === 'move') {
    e.facing = near.x < 0 ? -1 : 1;
    e.wait -= dt;
    const preferred = e.type === 'shooter' ? 255 : boss ? 165 : 0;
    const speed = gap > preferred + 26 ? e.speed : gap < preferred - 42 ? -e.speed*.64 : 0;
    e.x += near.x * speed * dt;
    e.y += near.y * speed * dt;
    if (e.wait <= 0 && (e.type !== 'stalker' || gap < 64)) {
      e.aim = near;
      e.action = e.type === 'stalker' ? 'windupMelee' : e.type === 'shooter' || (boss && r.random() < .51) ? 'windupShot' : 'windupDash';
      e.timer = e.action === 'windupDash' ? (enraged ? .49 : .68) : .43;
    }
  } else if (e.action === 'charge') {
    const speed = enraged ? 560 : boss ? 495 : 440;
    e.x += e.aim.x*speed*dt;
    e.y += e.aim.y*speed*dt;
    if (gap < e.radius + 18) damagePlayer(r,e.damage);
    if (e.timer <= 0) { e.action = 'rest'; e.timer = enraged ? .52 : .9; }
  } else if (e.timer <= 0) {
    switch (e.action) {
      case 'windupDash': e.action = 'charge'; e.timer = .53; break;
      case 'windupMelee':
        if (gap < 69) damagePlayer(r,e.damage);
        r.effects.push({ kind: 'slash', x: e.x, y: e.y, time: .24, max: .24, tint: '#e89caa' });
        e.action = 'rest'; e.timer = .37; break;
      case 'windupShot': enemyShots(r,e,enraged); e.action = 'rest'; e.timer = .44; break;
      case 'rest': e.action = 'move'; e.wait = TYPES[e.type].wait * (enraged ? .56 : 1); break;
    }
  }
  e.x = clamp(e.x, 25, WIDTH-25);
  e.y = clamp(e.y, 21, HEIGHT-21);
}

export function update(r, dt, input) {
  if (r.phase !== 'combat' || r.outcome) return;
  r.shake = Math.max(0, r.shake-dt);
  r.flash = Math.max(0, r.flash-dt);
  r.snapFlash = Math.max(0, (r.snapFlash || 0)-dt);
  r.hurtFlash = Math.max(0, (r.hurtFlash || 0)-dt);
  if (r.player.recoil && (r.player.recoil.time -= dt) <= 0) r.player.recoil = null;
  if (r.kick && (r.kick.time -= dt) <= 0) r.kick = null;
  r.effects = r.effects.filter(fx => (fx.time -= dt) > 0);
  if (r.freeze > 0) { r.freeze -= dt; return; }
  const p = r.player;
  r.time += dt;
  r.elapsed += dt;
  p.actionTime += dt;
  p.dashCooldown = Math.max(0,p.dashCooldown-dt);
  p.dashGrace = Math.max(0,p.dashGrace-dt);
  p.hurt = Math.max(0,p.hurt-dt);
  p.shotCooldown = Math.max(0,p.shotCooldown-dt);
  r.dry = Math.max(0,r.dry-dt);
  if (r.echo?.until <= r.time) r.echo = null;

  const move = unit(input.x,input.y);
  const moving = Boolean(input.x || input.y);
  if (input.dash && p.dash <= 0 && p.dashCooldown <= 0) {
    const dir = moving ? move : position(-Math.cos(p.angle), -Math.sin(p.angle));
    p.dx = dir.x;
    p.dy = dir.y;
    p.dash = DASH.duration;
    p.dashDone = 0;
    p.dashCooldown = Math.max(.24,.95*r.dashRate);
    p.dashFrom = position(p.x,p.y);
    p.dodged = false;
    p.ghostIn = 0;
    const away = Math.atan2(p.dy*ATTACK_Y,p.dx);
    r.effects.push({ kind: 'sprite', sprite: 'dash', x: p.x + p.dx*40, y: p.y + p.dy*40, angle: away, size: 180, time: .32, max: .32, alpha: .5 });
    r.effects.push({ kind: 'sprite', sprite: 'dodge', x: p.x, y: p.y, lift: 30, angle: away + Math.PI, size: 150, time: .3, max: .3, burst: true });
    r.effects.push({ kind: 'dust', x: p.x, y: p.y, angle: away + Math.PI, time: .36, max: .36 });
    if (p.dx) p.facing = -Math.sign(p.dx);
    p.dashFacing = p.facing;
    act(p,'dash');
    event(r,'dash');
  }
  if (p.dash > 0) {
    // Ease-out burst: most of the distance is covered in the first frames.
    const step = Math.min(dt, p.dash);
    const travel = dashTravel(1 - (p.dash - step) / DASH.duration);
    const moved = travel - p.dashDone;
    p.dashDone = travel;
    p.x = clamp(p.x + p.dx * moved, LEFT, RIGHT);
    p.y = clamp(p.y + p.dy * moved * WORLD_VERTICAL, TOP, BOTTOM);
    p.dash = Math.max(0, p.dash - dt);
    p.motion = 1;
    p.ghostIn -= step;
    if (p.ghostIn <= 0) {
      p.ghostIn += DASH.ghostInterval;
      r.effects.push({ kind: 'afterimage', x: p.x, y: p.y, facing: p.dashFacing,
        progress: 1 - p.dash / DASH.duration, time: DASH.ghostLife, max: DASH.ghostLife });
    }
    if (!p.dash) {
      p.dashGrace = DASH.grace;
      r.effects.push({ kind: 'dust', x: p.x, y: p.y, angle: Math.atan2(p.dy*ATTACK_Y,p.dx), time: .3, max: .3 });
      if (r.rules.has('dashThread')) r.trails.push({ a: p.dashFrom, b: position(p.x,p.y), time: 2.4, used: new Set() });
    }
  } else {
    p.motion += ((moving ? 1 : 0) - p.motion) * Math.min(1, dt * 13);
    const factor = Math.max(.3, 1 - r.knots.length*r.encumbrance);
    const speed = 232 * (1+r.gear.speed) * factor * (r.mood === 'frenzy' ? .8 : 1);
    p.vx = move.x*speed;
    p.vy = move.y*speed*WORLD_VERTICAL;
    p.x = clamp(p.x+p.vx*dt,LEFT,RIGHT);
    p.y = clamp(p.y+p.vy*dt,TOP,BOTTOM);
    if (moving) p.strideTime += dt * (.55 + factor*.45);
    if (p.actionTime > ({ shoot: .26, pull: .48, hurt: .28, dash: .29 }[p.action] ?? 0)) {
      const state = moving ? 'run' : 'idle';
      if (p.action !== state) act(p,state);
      if (move.x) p.facing = Math.sign(move.x);
    }
  }
  if (input.shoot) fire(r,input.aim);
  if (r.yarn < yarnMax(r)) {
    r.winding += dt / 1.05 * r.recharge * (p.motion < .2 && p.dash <= 0 ? 2 : 1) * (r.mood === 'frenzy' ? .6 : 1);
    if (r.winding >= 1) { r.yarn++; r.winding = 0; }
  } else r.winding = 0;
  updateNeedles(r, dt);
  if (input.pull) pull(r);
  r.enemies = r.enemies.filter(e => e.hp > 0);

  const duration = ROUND_DURATION[r.round];
  if (duration && r.elapsed < duration) {
    r.spawnIn -= dt;
    if (r.spawnIn <= 0 && r.enemies.length < 20) {
      r.spawned++;
      const type = r.round === 1 && r.spawned <= 4 ? 'stalker' : ['stalker','charger','shooter'][(r.spawned+r.floor)%3];
      spawnEnemy(r, type, null, r.spawned % (r.round === 1 ? 7 : 5) === 0);
      r.spawnIn = Math.max(.68, (r.round === 1 ? 2.2 : 1.75)-r.floor*.09);
    }
  }
  const worldDt = p.dash > 0 ? dt * DASH.slow : dt;
  for (const e of r.enemies) {
    if (r.outcome) break;
    updateEnemy(r,e,worldDt);
    for (const trail of r.trails) {
      if (!trail.used.has(e) && lineDistance(e, trail.a, trail.b) < e.radius+7) {
        trail.used.add(e);
        const thread = { x: e.x, y: e.y, anchor: trail.a, birth: r.time };
        r.threads.push(thread);
        attach(r,thread,e);
      }
    }
  }
  r.trails = r.trails.filter(t => (t.time -= dt) > 0);
  r.bullets = r.bullets.filter(b => {
    const a = position(b.x,b.y);
    b.x += b.vx*worldDt; b.y += b.vy*worldDt; b.age += worldDt;
    if (!b.spent && !r.outcome && lineDistance(p,a,b) < 18) {
      if (damagePlayer(r,b.hit)) return false;
      b.spent = true;
    }
    return b.age < 3.8 && b.x > 0 && b.y > 0 && b.x < WIDTH && b.y < HEIGHT;
  });
  r.drops = r.drops.filter(d => {
    const gap = distance(d,p);
    if (gap < 30) { collect(r,d); return false; }
    if (gap < 140) {
      const dir = unit(p.x-d.x,p.y-d.y);
      const step = Math.min(gap,260*dt);
      d.x += dir.x*step;
      d.y += dir.y*step;
    }
    return true;
  });
  if (r.outcome) return;
  if ((duration && r.elapsed >= duration && !r.enemies.length) || (r.round === 3 && !r.enemies.some(e => e.type === 'boss'))) {
    r.drops.forEach(d => collect(r,d));
    r.drops = [];
    r.enemies = [];
    r.bullets = [];
    r.threads = [];
    r.knots = [];
    if (r.round === 3) {
      r.profile.shards += 2;
      r.profile.best = Math.max(r.profile.best,r.floor);
      saveProfile(r.profile);
      const unowned = DESIRES.filter(d => !r.desires.has(d.id));
      r.offer = unowned[Math.floor(r.random()*unowned.length)] || {
        id: 'refine', name: '실에 스민 잔향', text: '실 한 가닥의 피해가 4 증가합니다.', penalty: '욕망 +20',
      };
      r.phase = 'desire';
    } else r.phase = 'rest';
  }
}
