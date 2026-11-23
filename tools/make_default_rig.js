// 초기 리그 데이터 생성기 — 한 번 생성 후에는 rig.html 에디터로 수정/저장.
// node tools/make_default_rig.js [--force]
const fs = require('fs');
const path = require('path');
const out = path.join(__dirname, '..', 'assets', 'rig', 'hero.rig.json');
if (fs.existsSync(out) && !process.argv.includes('--force')) {
  console.log('이미 있음 (덮어쓰려면 --force):', out); process.exit(0);
}
const P = (id, img, parent, pivot, attach, z, extra = {}) => ({ id, img: 'hero_' + img + '.png', parent, pivot, attach, z, rest: 0, scale: 1, shade: 0, ...extra });
const B = { shade: 0.38 };
const parts = [
  P('torso', 'torso', null, [125, 372], null, 0),
  P('tail', 'tail', 'torso', [42, 58], [40, 335], -9, { rest: 160 }),
  P('coat', 'coat', 'torso', [171, 38], [78, 215], -8),
  P('thigh_b', 'thigh', 'torso', [90, 38], [108, 372], -6, B),
  P('shin_b', 'shin', 'thigh_b', [58, 36], [90, 228], -6.1, B),
  P('foot_b', 'foot', 'shin_b', [70, 35], [58, 193], -6.2, B),
  P('uarm_b', 'uarm', 'torso', [72, 33], [100, 139], -5, B),
  P('farm_b', 'farm', 'uarm_b', [98, 34], [72, 222], -5.1, B),
  P('hand_b', 'hand', 'farm_b', [73, 35], [93, 196], -5.2, B),
  P('thigh', 'thigh', 'torso', [90, 38], [140, 378], -1),
  P('shin', 'shin', 'thigh', [58, 36], [90, 228], -1.1),
  P('foot', 'foot', 'shin', [70, 35], [58, 193], -1.2),
  P('head', 'head', 'torso', [295, 431], [134, 35], 5),
  P('uarm', 'uarm', 'torso', [72, 33], [120, 139], 2),
  P('farm', 'farm', 'uarm', [98, 34], [72, 222], 3),
  P('needle', 'needle', 'hand', [210, 44], [80, 130], 3.5, { rest: 0 }),
  P('hand', 'hand', 'farm', [73, 35], [93, 196], 4),
];
const anims = {
  idle: { length: 1.2, loop: true, interp: 'step', root: [[0, 0, 0], [0.6, 0, -6], [1.2, 0, 0]], keys: {
    torso: [[0, 0], [0.6, 2]], head: [[0, 0], [0.6, 3]], tail: [[0, 0], [0.4, 10], [0.8, -8]], coat: [[0, 0], [0.6, 4]],
    uarm: [[0, -18], [0.6, -22]], farm: [[0, -25], [0.6, -30]], needle: [[0, -60]], uarm_b: [[0, -4], [0.6, 4]], farm_b: [[0, -10]],
  } },
  run: { length: 0.5, loop: true, interp: 'step', root: [[0, 0, 0], [0.125, 0, -16], [0.25, 0, 0], [0.375, 0, -16]], keys: {
    torso: [[0, 9]], head: [[0, -6], [0.25, -3]],
    thigh: [[0, -38], [0.25, 32]], shin: [[0, 12], [0.125, 8], [0.25, 40], [0.375, 70]], foot: [[0, -10], [0.25, 15]],
    thigh_b: [[0, 32], [0.25, -38]], shin_b: [[0, 40], [0.125, 70], [0.25, 12], [0.375, 8]], foot_b: [[0, 15], [0.25, -10]],
    uarm: [[0, 30], [0.25, -35]], farm: [[0, -20], [0.25, -55]], needle: [[0, -70]],
    uarm_b: [[0, -35], [0.25, 30]], farm_b: [[0, -55], [0.25, -20]],
    coat: [[0, 18], [0.125, 28], [0.25, 18], [0.375, 28]], tail: [[0, 20], [0.125, 35], [0.25, 15], [0.375, 30]],
  } },
  shoot: { length: 0.35, loop: false, interp: 'step', root: [[0, 0, 0], [0.08, -6, 0], [0.35, 0, 0]], keys: {
    torso: [[0, 6], [0.08, 2], [0.35, 4]], uarm: [[0, -95], [0.08, -80], [0.35, -88]], farm: [[0, -5], [0.08, 0]], needle: [[0, -10]],
    uarm_b: [[0, 20]], farm_b: [[0, -30]], head: [[0, -4]], coat: [[0, 10]],
  } },
  pull: { length: 0.5, loop: false, interp: 'step', root: [[0, 0, 0], [0.1, -18, 0], [0.5, 0, 0]], keys: {
    torso: [[0, 10], [0.1, -14], [0.5, 0]], head: [[0, 0], [0.1, -10], [0.5, 0]],
    uarm: [[0, -90], [0.1, -20], [0.2, 25], [0.5, 0]], farm: [[0, -10], [0.1, -80], [0.5, -40]],
    uarm_b: [[0, -80], [0.1, -10], [0.2, 30], [0.5, 0]], farm_b: [[0, -10], [0.1, -90], [0.5, -40]],
    thigh: [[0, -20], [0.1, -35], [0.5, -10]], thigh_b: [[0, 15], [0.1, 30], [0.5, 5]], shin_b: [[0.1, 20]],
    coat: [[0, 0], [0.1, -18], [0.3, 10]], tail: [[0, 0], [0.1, -25], [0.3, 15]], needle: [[0, -30]],
  } },
  dash: { length: 0.3, loop: false, interp: 'step', root: [[0, 0, -10], [0.15, 0, -18], [0.3, 0, 0]], keys: {
    torso: [[0, 28], [0.3, 15]], head: [[0, -18]], thigh: [[0, -60], [0.15, -45]], shin: [[0, 60]], thigh_b: [[0, 45]], shin_b: [[0, 30]],
    uarm: [[0, 70]], farm: [[0, -40]], uarm_b: [[0, 60]], farm_b: [[0, -40]], coat: [[0, 55]], tail: [[0, 40]], needle: [[0, -80]],
  } },
  hurt: { length: 0.35, loop: false, interp: 'step', root: [[0, 0, 0], [0.05, -14, -6], [0.35, 0, 0]], keys: {
    torso: [[0, -16], [0.35, -4]], head: [[0, -18], [0.2, 6]], uarm: [[0, -130], [0.2, -60]], farm: [[0, -40]],
    uarm_b: [[0, -110], [0.2, -40]], farm_b: [[0, -50]], thigh: [[0, -25]], thigh_b: [[0, 20]], coat: [[0, -20]], tail: [[0, 30]],
  } },
  die: { length: 0.9, loop: false, interp: 'step', root: [[0, 0, 0], [0.3, -20, 60], [0.6, -60, 330], [0.9, -60, 340]], keys: {
    torso: [[0, -10], [0.3, -40], [0.6, -88]], head: [[0, -20], [0.6, -10], [0.75, 8]], uarm: [[0, -150], [0.6, -100]], farm: [[0, -30]],
    uarm_b: [[0, -160]], thigh: [[0, -40], [0.6, -80]], shin: [[0, 30]], thigh_b: [[0, -20], [0.6, -60]], coat: [[0, -30], [0.6, -70]], tail: [[0, 40], [0.6, 80]],
  } },
};
const rig = { name: 'hero', stepFps: 12, rootPos: [0, -455], parts, anims };
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(rig, null, 1));
console.log('wrote', out);
