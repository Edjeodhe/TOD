import { buildRig, computePose, drawRig, groundedMatrices, hitTest, baseMatrix, animTime } from './runtime.js';
import { POSES, loadReference, rigHeight, referenceMatrix, fitPose } from './posefit.js';

const RIG_FILE = new URLSearchParams(location.search).get('legacy') === '1' ? 'hero.rig.json' : 'hero-quarter.rig.json';
const $ = (id) => document.getElementById(id);
const cv = $('cv'), ctx = cv.getContext('2d');
const tl = $('tl'), tctx = tl.getContext('2d');

const S = {
  mode: 'setup', data: null, rig: null, imgCache: new Map(),
  sel: null, anim: 'idle', t: 0, playing: false, preview: false,
  zoom: 0.55, pan: [0, 0], undo: [], dirty: false,
  drag: null, selKey: null,
};

// ---------- 로딩 / 저장 ----------
async function load() {
  const res = await fetch('assets/rig/' + RIG_FILE, { cache: 'no-store' });
  S.data = await res.json();
  S.anim = Object.keys(S.data.anims)[0];
  await rebuild();
  S.sel = (S.data.parts.find(p => p.img) || S.data.parts[0]).id;
  fitView();
  refreshUI();
  status('불러옴: assets/rig/' + RIG_FILE);
}
async function rebuild() { S.rig = await buildRig(S.data, S.imgCache); }
async function save() {
  const body = JSON.stringify(S.data, null, 1);
  const r = await fetch('/api/save?file=' + RIG_FILE, { method: 'POST', body });
  if (r.ok) { S.dirty = false; status('저장됨 ✓ ' + new Date().toLocaleTimeString() + ' — 게임 새로고침 시 반영'); }
  else status('저장 실패: ' + r.status);
}
function snapshot() { S.undo.push(JSON.stringify(S.data)); if (S.undo.length > 80) S.undo.shift(); S.dirty = true; }
async function undo() {
  const s = S.undo.pop(); if (!s) return;
  S.data = JSON.parse(s); await rebuild(); refreshUI(); status('되돌림');
}
function status(m) { $('status').textContent = (S.dirty ? '● ' : '') + m; }

// ---------- 뷰 ----------
function resize() {
  const dpr = devicePixelRatio || 1;
  for (const c of [cv, tl]) {
    const r = c.getBoundingClientRect();
    c.width = Math.max(1, r.width * dpr); c.height = Math.max(1, r.height * dpr);
  }
}
function fitView() {
  const r = cv.getBoundingClientRect();
  S.zoom = Math.min(r.width, r.height) / 1150;
  S.pan = [r.width / 2, r.height * 0.88];
}
function viewMatrix() { return new DOMMatrix().translate(S.pan[0], S.pan[1]).scale(S.zoom); }
const anim = () => S.data.anims[S.anim];
function currentPose(t = S.t) {
  if (S.mode === 'setup') return computePose(S.rig, '__none__', 0);
  return computePose(S.rig, S.anim, t, { stepFps: S.data.stepFps });
}

// ---------- 렌더 ----------
function render() {
  const dpr = devicePixelRatio || 1;
  const r = cv.getBoundingClientRect();
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, r.width, r.height);
  if (!S.rig) return;
  if (S.preview) return renderPreview(r);
  const V = viewMatrix();
  // 바닥선
  ctx.strokeStyle = '#3a3042'; ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(0, S.pan[1]); ctx.lineTo(r.width, S.pan[1]); ctx.stroke();
  ctx.beginPath(); ctx.moveTo(S.pan[0], 0); ctx.lineTo(S.pan[0], r.height); ctx.setLineDash([4, 6]); ctx.stroke(); ctx.setLineDash([]);
  ctx.setTransform(new DOMMatrix([dpr, 0, 0, dpr, 0, 0]).multiply(V));
  const base = new DOMMatrix();
  if (S.mode === 'anim' && $('aOnion').checked) {
    const a = anim(), dt = 1 / (S.data.stepFps || 12);
    for (const [off, al] of [[-2 * dt, 0.12], [-dt, 0.22], [dt, 0.22]]) {
      ctx.save(); drawRig(ctx, S.rig, computePose(S.rig, S.anim, animTime(a, S.t + off), { stepFps: S.data.stepFps }), base, { alpha: al, filter: off < 0 ? 'sepia(1) hue-rotate(280deg)' : 'sepia(1) hue-rotate(120deg)' }); ctx.restore();
    }
  }
  const pose = currentPose();
  const ref = REF.current && REF.cache.get(REF.current);
  const drawRef = () => {
    if (!ref || !REF.scale) return;
    ctx.save();
    ctx.setTransform(ctx.getTransform().multiply(referenceMatrix(ref, REF.scale)));
    ctx.globalAlpha = parseFloat($('rAlpha').value);
    ctx.drawImage(ref.canvas, 0, 0);
    ctx.restore();
  };
  if (!$('rFront').checked) drawRef();
  const cel = S.mode === 'anim' && activeCel(S.anim, S.t);
  const M = drawRig(ctx, S.rig, pose, base, cel ? { alpha: 0.12 } : {});
  if (cel) {
    // 통그림 구간: 리그 대신 통그림을 같은 바닥·배율로 그린다.
    if (!REF.celImg?.[cel.img]) { (REF.celImg ||= {})[cel.img] = new Image(); REF.celImg[cel.img].src = 'assets/sprites/' + cel.img; }
    const im = REF.celImg[cel.img];
    if (im.complete) { ctx.save(); ctx.setTransform(ctx.getTransform().scale(S.data.cels.scale).translate(-cel.anchor[0], -cel.anchor[1])); ctx.drawImage(im, 0, 0); ctx.restore(); }
  }
  if ($('rFront').checked) drawRef();
  S.M = M;
  // 선택 파츠 외곽 + 할핀
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const showP = $('vPivots').checked;
  for (const p of S.data.parts) {
    if (p.hidden) continue;
    const W = V.multiply(M[p.id]);
    const isSel = p.id === S.sel;
    if (isSel) {
      const img = S.rig.img[p.id];
      if (img) {
        const cs = [[0, 0], [img.width, 0], [img.width, img.height], [0, img.height]].map(([x, y]) => W.transformPoint(new DOMPoint(x - p.pivot[0], y - p.pivot[1])));
        ctx.strokeStyle = '#E89BB7'; ctx.lineWidth = 1.5; ctx.setLineDash([5, 4]);
        ctx.beginPath(); cs.forEach((c, i) => i ? ctx.lineTo(c.x, c.y) : ctx.moveTo(c.x, c.y)); ctx.closePath(); ctx.stroke(); ctx.setLineDash([]);
      }
      // 부모 연결선
      if (p.parent) {
        const par = S.rig.byId[p.parent];
        const pp = V.multiply(M[par.id]).transformPoint(new DOMPoint(0, 0));
        const me = W.transformPoint(new DOMPoint(0, 0));
        ctx.strokeStyle = 'rgba(140,199,190,.6)'; ctx.beginPath(); ctx.moveTo(pp.x, pp.y); ctx.lineTo(me.x, me.y); ctx.stroke();
      }
      // 회전 방향 표시
      if (S.mode === 'anim') {
        const a = W.transformPoint(new DOMPoint(0, 0)), b = W.transformPoint(new DOMPoint(0, 60));
        ctx.strokeStyle = '#E89BB7'; ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
      }
    }
    if (showP || isSel) {
      const c = W.transformPoint(new DOMPoint(0, 0));
      ctx.fillStyle = isSel ? '#E89BB7' : p.img ? '#c9a45c' : '#8cc7be'; ctx.strokeStyle = '#16131B'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(c.x, c.y, isSel ? 7 : 4.5, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    }
  }
  $('hint').textContent = S.mode === 'setup'
    ? '셋업 모드 · 기본 포즈(rest)만 표시 · 파츠 드래그=부착 이동 · ●드래그=피벗 · 우클릭드래그=기본각'
    : `애니메이션 · ${S.anim} · ${S.data.stepFps ? `스텝 ${S.data.stepFps}fps` : '연속 보간'} · 파츠 드래그=회전`;
}

// ---------- 통그림 비교 / 자동 맞춤 ----------
const REF = { cache: new Map(), current: '', scale: 0 };
// 클립 키 시점별로 어떤 통그림을 맞출지. mirror = 앞/뒤 팔다리 바꾼 반대 박자, mix = 두 포즈 사이, rest = 대기 포즈.
const FIT_PLAN = {
  // idle 은 손으로 만든 숨쉬기 동작을 쓴다 (가는 바늘을 잘못 읽어 엉뚱한 자세가 됐다). 'rest' = 기본 자세.
  // run 은 자동 맞춤에서 뺀다: 박자별로 따로 맞추면 좌우가 어긋나 절뚝거렸다 → tools/symmetric_run.py 로 대칭 생성.
  shoot: [['shoot_windup'], ['shoot_release'], ['shoot_follow'], ['mix', 'shoot_follow', 'rest', .5], ['rest']],
  pull: [['pull_grab'], ['mix', 'pull_grab', 'pull_yank', .45], ['pull_yank'], ['pull_yank'], ['mix', 'pull_yank', 'rest', .5], ['rest']],
  hurt: [['hurt'], ['mix', 'hurt', 'rest', .5], ['rest']],
};
// 통그림 섞기: 리그로 만들기 어려운 순간은 통그림 한 장으로 잠깐 바꿔 끼운다 (시작~끝 초).
// 통그림 섞기 계획 (클립: [[시작, 끝, 포즈]]). 현재 비어 있음: 통그림이 리그보다 머리가 약 1.27배 커서
// 끼우면 그 순간 머리가 커 보였다. 리그 비율에 맞춘 통그림을 새로 뽑으면 여기 다시 추가한다.
const CEL_PLAN = {};
async function writeCels() {
  const clips = {};
  for (const [clip, list] of Object.entries(CEL_PLAN)) {
    clips[clip] = [];
    for (const [from, to, name] of list) {
      const ref = await ensureRef(name);
      clips[clip].push({ from, to, img: `hero_cel_${name}.png`, anchor: [ref.box.cx, ref.box.bottom] });
    }
  }
  if (Object.keys(clips).length) S.data.cels = { scale: REF.scale, clips }; else delete S.data.cels;
}
function activeCel(clip, t) {
  return S.data.cels?.clips?.[clip]?.find((c) => t >= c.from && t < c.to);
}
const SWAP = [['near_thigh', 'far_thigh'], ['near_shin', 'far_shin'], ['near_foot', 'far_foot']];
const SWING = ['near_arm', 'near_forearm', 'far_arm', 'far_forearm', 'needle', 'near_hand'];

async function ensureRef(name) {
  if (!REF.cache.has(name)) REF.cache.set(name, await loadReference(name));
  if (!REF.scale) {
    // 모든 통그림은 같은 배율로 그려졌다 → 대기 포즈 키 = 리그 기본 키
    const idle = REF.cache.get('idle') || await loadReference('idle');
    REF.cache.set('idle', idle);
    REF.scale = rigHeight(S.rig, computePose(S.rig, '__none__', 0)) / (idle.box.bottom - idle.box.top);
  }
  return REF.cache.get(name);
}
function keyTimes(a) {
  const ts = new Set();
  for (const tr of Object.values(a.keys)) for (const k of tr) ts.add(+k[0].toFixed(4));
  for (const k of a.root || []) ts.add(+k[0].toFixed(4));
  return [...ts].sort((x, y) => x - y);
}
async function fitAt(refName, t, startPose) {
  const ref = await ensureRef(refName);
  const start = startPose || computePose(S.rig, S.anim, t, { stepFps: 0, interp: 'linear' });
  const fit = fitPose(S.rig, ref, REF.scale, start, { height: rigHeight(S.rig, computePose(S.rig, '__none__', 0)) * 1.25,
    grounded: !['run_up', 'hurt'].includes(refName) });
  return fit;
}
function writeKeys(clip, t, pose) {
  const a = S.data.anims[clip];
  for (const p of S.data.parts) {
    const v = Math.round((pose.ang[p.id] - (p.rest || 0)) * 10) / 10;
    a.keys[p.id] = (a.keys[p.id] || []).filter((k) => Math.abs(k[0] - t) > 1e-4);
    a.keys[p.id].push([t, v]); a.keys[p.id].sort((x, y) => x[0] - y[0]);
  }
  a.root = (a.root || []).filter((k) => Math.abs(k[0] - t) > 1e-4);
  a.root.push([t, Math.round(pose.root[0] * 10) / 10, Math.round(pose.root[1] * 10) / 10]);
  a.root.sort((x, y) => x[0] - y[0]);
}
const mixPose = (p, q, u) => ({ ang: Object.fromEntries(Object.keys(p.ang).map((id) => [id, p.ang[id] + (q.ang[id] - p.ang[id]) * u])),
  root: p.root.map((v, i) => v + (q.root[i] - v) * u) });
// 반대 박자: 다리는 앞/뒤를 맞바꾸고, 팔은 (바늘 든 손이 휙 돌지 않도록) 사이클 평균을 기준으로 흔들림만 뒤집는다.
function mirrorPose(p, mean) {
  const ang = { ...p.ang };
  for (const [a, b] of SWAP) if (a in ang && b in ang) [ang[a], ang[b]] = [p.ang[b], p.ang[a]];
  if (mean) for (const id of SWING) if (id in ang) ang[id] = 2 * mean[id] - p.ang[id];
  return { ang, root: [...p.root] };
}
async function fitClip(clip) {
  const plan = FIT_PLAN[clip]; if (!plan) return 0;
  const a = S.data.anims[clip];
  const times = plan.map((_, i) => Math.round(a.length * i / (plan.length - 1) * 1e4) / 1e4);
  const fitted = {};
  const prev = S.anim; S.anim = clip;
  for (const [i, step] of plan.entries()) {
    if (step.length === 1 && step[0] === 'rest') fitted.rest ||= computePose(S.rig, '__none__', 0);
    else if (step.length === 1 && !fitted[step[0]]) {
      fitted[step[0]] = await fitAt(step[0], times[i]);
      $('rInfo').textContent = `${clip}: ${step[0]} 맞춤 점수 ${fitted[step[0]].score.toFixed(3)}`;
      await new Promise(requestAnimationFrame);
    }
  }
  if (plan.some((s) => s.includes('rest'))) fitted.rest ||= computePose(S.rig, '__none__', 0);
  const mirrored = plan.filter((s) => s[0] === 'mirror').map((s) => fitted[s[1]]);
  const mean = mirrored.length ? Object.fromEntries(SWING.map((id) => [id, mirrored.reduce((v, f) => v + f.ang[id], 0) / mirrored.length])) : null;
  if (clip === 'run') for (const f of Object.values(fitted)) f.root[0] = 0; // 달리기는 제자리 사이클
  plan.forEach((step, i) => {
    const pose = step[0] === 'mirror' ? mirrorPose(fitted[step[1]], mean) : step[0] === 'mix' ? mixPose(fitted[step[1]], fitted[step[2]], step[3]) : fitted[step[0]];
    writeKeys(clip, times[i], pose);
  });
  // 맞춘 키 시점 외의 남은 키는 제거 (통그림 박자로 통일)
  for (const id of Object.keys(a.keys)) a.keys[id] = a.keys[id].filter((k) => times.some((t) => Math.abs(t - k[0]) < 1e-4));
  a.root = a.root.filter((k) => times.some((t) => Math.abs(t - k[0]) < 1e-4));
  S.anim = prev;
  const scored = Object.values(fitted).filter((f) => f.score != null);
  return scored.reduce((s, f) => s + f.score, 0) / (scored.length || 1);
}

for (const n of POSES) $('rSel').insertAdjacentHTML('beforeend', `<option value="${n}">${n}</option>`);
$('rSel').addEventListener('change', async (e) => { REF.current = e.target.value; if (REF.current) await ensureRef(REF.current); });
$('bFit').onclick = async () => {
  if (!REF.current) return status('레퍼런스를 먼저 고르세요');
  if (S.mode !== 'anim') setMode('anim');
  snapshot();
  const t = snapT(S.t);
  const fit = await fitAt(REF.current, t);
  writeKeys(S.anim, t, fit);
  $('rInfo').textContent = `${REF.current} → ${S.anim} @${t.toFixed(2)}s · 점수 ${fit.score.toFixed(3)} (${fit.evals}회 비교)`;
  refreshUI();
};
$('bFitAll').onclick = async () => {
  snapshot(); setMode('anim');
  const scores = [];
  for (const clip of Object.keys(FIT_PLAN)) if (S.data.anims[clip]) scores.push(`${clip} ${(await fitClip(clip)).toFixed(2)}`);
  await writeCels();
  $('rInfo').textContent = '맞춤 완료 · ' + scores.join(' · ') + ' — 확인 후 저장하세요';
  refreshUI();
};
window.__fit = { fitClip, ensureRef, REF, FIT_PLAN };

// 게임 화면 스케일로 좌우 달리기/멈춤 미리보기
const PV = { x: 0, dir: 1, t: 0, phase: 'run', ph: 0 };
function renderPreview(r) {
  const dpr = devicePixelRatio || 1;
  const g = ctx.createLinearGradient(0, 0, 0, r.height);
  g.addColorStop(0, '#1c1722'); g.addColorStop(1, '#2b2430');
  ctx.fillStyle = g; ctx.fillRect(0, 0, r.width, r.height);
  const gy = r.height * 0.7;
  ctx.fillStyle = 'rgba(0,0,0,.35)'; ctx.beginPath(); ctx.ellipse(r.width / 2 + PV.x, gy, 30, 9, 0, 0, Math.PI * 2); ctx.fill();
  const sc = 0.16;
  const clip = S.data.anims[PV.phase] ? PV.phase : 'idle';
  const pose = computePose(S.rig, clip, PV.t, { stepFps: S.data.stepFps });
  const base = baseMatrix(r.width / 2 + PV.x, gy, sc, clip === 'dash' ? -PV.dir : PV.dir);
  const matrices = clip === 'run' || clip === 'pull' ? groundedMatrices(S.rig,pose,base,clip) : null;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  drawRig(ctx, S.rig, pose, base,{ matrices });
  ctx.fillStyle = '#9d8fa1'; ctx.font = '12px system-ui';
  ctx.fillText(`게임 스케일 미리보기 (x${sc}) · 클립: ${clip} · 다시 누르면 편집 화면`, 12, 20);
}
function stepPreview(dt) {
  PV.t += dt; PV.ph += dt;
  const seq = [['run', 2.2], ['idle', 1.2], ['shoot', 0.9], ['pull', 0.9], ['dash', 0.6], ['run', 2.2], ['hurt', 0.6]];
  let acc = 0, total = seq.reduce((a, s) => a + s[1], 0), u = PV.ph % total;
  for (const [n, d] of seq) { if (u < acc + d) { if (PV.phase !== n) { PV.phase = n; PV.t = 0; } break; } acc += d; }
  const sp = PV.phase === 'run' ? 140 : PV.phase === 'dash' ? 420 : 0;
  PV.x += sp * PV.dir * dt;
  const lim = cv.getBoundingClientRect().width * 0.35;
  if (Math.abs(PV.x) > lim) { PV.x = Math.sign(PV.x) * lim; PV.dir *= -1; }
}

// ---------- 타임라인 ----------
const TL = { left: 110, rowH: 16, top: 18 };
function renderTL() {
  const dpr = devicePixelRatio || 1, r = tl.getBoundingClientRect();
  tctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  tctx.fillStyle = '#1b161f'; tctx.fillRect(0, 0, r.width, r.height);
  if (!S.data) return;
  const a = anim(), W = r.width - TL.left - 10;
  const x = (t) => TL.left + (t / a.length) * W;
  // 눈금
  tctx.fillStyle = '#9d8fa1'; tctx.font = '10px system-ui';
  const fps = S.data.stepFps || 12;
  for (let i = 0; i <= a.length * fps + 1e-6; i++) {
    const t = i / fps, xx = x(t);
    tctx.fillStyle = i % fps === 0 ? '#5a4c62' : '#2c2433'; tctx.fillRect(xx, TL.top - 4, 1, r.height);
    if (i % Math.max(1, Math.round(fps / 4)) === 0) { tctx.fillStyle = '#9d8fa1'; tctx.fillText(t.toFixed(2), xx + 2, 10); }
  }
  // 파츠 행 (스크롤 없이: 키 있는 파츠 + 선택 파츠 우선)
  const rows = tlRows(r.height);
  rows.forEach((id, i) => {
    const y = TL.top + i * TL.rowH;
    tctx.fillStyle = id === S.sel ? 'rgba(232,155,183,.12)' : (i % 2 ? '#1f1a24' : '#1b161f');
    tctx.fillRect(0, y, r.width, TL.rowH);
    tctx.fillStyle = id === S.sel ? '#E89BB7' : '#cbbfc8'; tctx.font = '11px system-ui';
    tctx.fillText(id === '__root' ? '(루트 이동)' : id, 8, y + 12);
    const tr = id === '__root' ? a.root : a.keys[id];
    (tr || []).forEach((k, ki) => {
      const xx = x(k[0]), yy = y + TL.rowH / 2, sel = S.selKey && S.selKey.id === id && S.selKey.i === ki;
      tctx.fillStyle = sel ? '#fff' : (id === S.sel ? '#E89BB7' : '#8CC7BE');
      tctx.beginPath(); tctx.moveTo(xx, yy - 5); tctx.lineTo(xx + 5, yy); tctx.lineTo(xx, yy + 5); tctx.lineTo(xx - 5, yy); tctx.closePath(); tctx.fill();
    });
  });
  // 재생 헤드
  tctx.fillStyle = '#E89BB7'; tctx.fillRect(x(S.t) - 1, 0, 2, r.height);
  $('tNow').textContent = S.t.toFixed(2) + 's / ' + a.length.toFixed(2);
  $('tlInfo').textContent = S.mode === 'setup' ? '※ 셋업 모드에서는 타임라인이 표시만 됩니다' : `${rows.length}개 트랙 · 키 선택 후 Del 삭제 · ←/→ 프레임 이동`;
}
function tlRows(h) {
  const a = anim(), max = Math.floor((h - TL.top) / TL.rowH);
  const withKeys = S.data.parts.map((p) => p.id).filter((id) => a.keys[id]?.length);
  let rows = ['__root', ...withKeys];
  if (S.sel && !rows.includes(S.sel)) rows.splice(1, 0, S.sel);
  return rows.slice(0, max);
}
function tlHit(e) {
  const r = tl.getBoundingClientRect(), mx = e.clientX - r.left, my = e.clientY - r.top;
  const a = anim(), W = r.width - TL.left - 10;
  const t = Math.max(0, Math.min(a.length, ((mx - TL.left) / W) * a.length));
  const rows = tlRows(r.height), ri = Math.floor((my - TL.top) / TL.rowH);
  const id = rows[ri];
  let key = null;
  if (id) {
    const tr = id === '__root' ? a.root : a.keys[id];
    (tr || []).forEach((k, i) => { if (Math.abs(TL.left + (k[0] / a.length) * W - mx) < 6) key = { id, i }; });
  }
  return { t, id, key };
}
tl.addEventListener('mousedown', (e) => {
  const h = tlHit(e);
  if (h.id && h.id !== '__root') selectPart(h.id);
  S.selKey = h.key;
  if (h.key && S.mode === 'anim') { snapshot(); S.drag = { kind: 'key', ...h.key }; }
  else { S.t = snapT(h.t); S.drag = { kind: 'scrub' }; }
});
addEventListener('mousemove', (e) => {
  if (!S.drag) return;
  if (S.drag.kind === 'scrub' || S.drag.kind === 'key') {
    const h = tlHit(e);
    if (S.drag.kind === 'scrub') S.t = snapT(h.t);
    else {
      const a = anim(), tr = S.drag.id === '__root' ? a.root : a.keys[S.drag.id];
      tr[S.drag.i][0] = snapT(h.t);
      const k = tr[S.drag.i]; tr.sort((p, q) => p[0] - q[0]); S.drag.i = tr.indexOf(k); S.selKey = { id: S.drag.id, i: S.drag.i };
    }
  }
});
function snapT(t) { const f = S.data.stepFps || 12; return Math.round(t * f) / f; }

// ---------- 캔버스 조작 ----------
function mouse(e) { const r = cv.getBoundingClientRect(); return [e.clientX - r.left, e.clientY - r.top]; }
let spaceDown = false;
cv.addEventListener('contextmenu', (e) => e.preventDefault());
cv.addEventListener('mousedown', (e) => {
  if (S.preview || !S.M) return;
  const [mx, my] = mouse(e), V = viewMatrix();
  if (e.button === 1 || spaceDown) { S.drag = { kind: 'pan', sx: mx, sy: my, p0: [...S.pan] }; return; }
  // 선택된 파츠의 할핀을 먼저 검사
  if (S.sel && S.mode === 'setup' && e.button === 0) {
    const c = V.multiply(S.M[S.sel]).transformPoint(new DOMPoint(0, 0));
    if (Math.hypot(c.x - mx, c.y - my) < 10) { snapshot(); S.drag = { kind: 'pivot', id: S.sel, last: [mx, my] }; return; }
  }
  const id = hitTest(S.rig, S.M, mx, my, V);
  if (!id) return;
  selectPart(id);
  const p = S.rig.byId[id];
  const c = V.multiply(S.M[id]).transformPoint(new DOMPoint(0, 0));
  const ang0 = Math.atan2(my - c.y, mx - c.x);
  snapshot();
  if (S.mode === 'setup') {
    if (e.button === 2) S.drag = { kind: 'rest', id, c, ang0, v0: p.rest || 0 };
    else S.drag = { kind: 'attach', id, last: [mx, my] };
  } else {
    const pose = currentPose();
    S.drag = { kind: 'rot', id, c, ang0, v0: pose.ang[id] - (p.rest || 0) };
  }
});
cv.addEventListener('mousemove', (e) => {
  if (!S.drag) return;
  const [mx, my] = mouse(e), d = S.drag, V = viewMatrix();
  if (d.kind === 'pan') { S.pan = [d.p0[0] + mx - d.sx, d.p0[1] + my - d.sy]; return; }
  const p = d.id && S.rig.byId[d.id];
  if (d.kind === 'attach' || d.kind === 'pivot') {
    const dx = mx - d.last[0], dy = my - d.last[1]; d.last = [mx, my];
    if (d.kind === 'attach') {
      // 부모 좌표계로 이동량 변환
      const parentM = p.parent ? V.multiply(S.M[p.parent]) : V;
      const inv = parentM.inverse();
      const a = inv.transformPoint(new DOMPoint(0, 0)), b = inv.transformPoint(new DOMPoint(dx, dy));
      const vx = b.x - a.x, vy = b.y - a.y;
      if (p.parent) { p.attach[0] += vx; p.attach[1] += vy; }
      else { S.data.rootPos[0] += vx; S.data.rootPos[1] += vy; }
    } else {
      // 피벗 이동: 그림은 제자리, 피벗만 이동 → attach 보정
      const W = V.multiply(S.M[p.id]), inv = W.inverse();
      const a = inv.transformPoint(new DOMPoint(0, 0)), b = inv.transformPoint(new DOMPoint(dx, dy));
      const lx = b.x - a.x, ly = b.y - a.y; // 파츠 로컬(이미지 px) 이동량
      p.pivot[0] += lx; p.pivot[1] += ly;
      const parentM = p.parent ? V.multiply(S.M[p.parent]) : V;
      const pinv = parentM.inverse();
      const pa = pinv.transformPoint(new DOMPoint(0, 0)), pb = pinv.transformPoint(new DOMPoint(dx, dy));
      if (p.parent) { p.attach[0] += pb.x - pa.x; p.attach[1] += pb.y - pa.y; }
      else { S.data.rootPos[0] += pb.x - pa.x; S.data.rootPos[1] += pb.y - pa.y; }
      // 자식들은 부모 이미지 좌표에 붙어있으므로 그대로 유지됨
    }
    roundPart(p); refreshProps();
  } else if (d.kind === 'rest' || d.kind === 'rot') {
    const ang = Math.atan2(my - d.c.y, mx - d.c.x);
    let deg = d.v0 + ((ang - d.ang0) * 180) / Math.PI;
    deg = Math.round(deg);
    if (d.kind === 'rest') { p.rest = deg; refreshProps(); }
    else if ($('aAuto').checked) setKey(d.id, deg);
  }
});
addEventListener('mouseup', () => { if (S.drag) { S.drag = null; status('편집 중 (저장 안 됨)'); } });
cv.addEventListener('wheel', (e) => {
  e.preventDefault();
  const [mx, my] = mouse(e), f = Math.exp(-e.deltaY * 0.0012);
  S.pan = [mx + (S.pan[0] - mx) * f, my + (S.pan[1] - my) * f]; S.zoom *= f;
}, { passive: false });

function roundPart(p) {
  p.pivot = p.pivot.map((v) => Math.round(v));
  if (p.attach) p.attach = p.attach.map((v) => Math.round(v));
  S.data.rootPos = S.data.rootPos.map((v) => Math.round(v));
}

// ---------- 키 ----------
function setKey(id, deg, t = snapT(S.t)) {
  const a = anim(); a.keys[id] = a.keys[id] || [];
  const tr = a.keys[id], k = tr.find((k) => Math.abs(k[0] - t) < 1e-4);
  if (k) k[1] = deg; else { tr.push([t, deg]); tr.sort((p, q) => p[0] - q[0]); }
}
function keyCurrent(id) {
  const pose = computePose(S.rig, S.anim, S.t, { stepFps: S.data.stepFps });
  setKey(id, Math.round(pose.ang[id] - (S.rig.byId[id].rest || 0)));
}
function deleteKey() {
  const a = anim();
  if (S.selKey) {
    snapshot();
    const tr = S.selKey.id === '__root' ? a.root : a.keys[S.selKey.id];
    tr.splice(S.selKey.i, 1); S.selKey = null; return;
  }
  if (!S.sel || !a.keys[S.sel]) return;
  const t = snapT(S.t), tr = a.keys[S.sel], i = tr.findIndex((k) => Math.abs(k[0] - t) < 1e-4);
  if (i >= 0) { snapshot(); tr.splice(i, 1); }
}

// ---------- UI ----------
function selectPart(id) { S.sel = id; refreshUI(); }
function refreshUI() {
  const ul = $('parts'); ul.innerHTML = '';
  const depth = (p) => (p.parent ? 1 + depth(S.rig.byId[p.parent]) : 0);
  for (const p of S.data.parts) {
    const li = document.createElement('li');
    li.className = p.id === S.sel ? 'sel' : '';
    li.innerHTML = `<span>${'&nbsp;&nbsp;'.repeat(depth(p))}${p.hidden ? '<s>' + p.id + '</s>' : p.id}</span><span class="z">z ${p.z}</span>`;
    li.onclick = () => selectPart(p.id);
    ul.appendChild(li);
  }
  const validParent = p => {
    for (let ancestor = p; ancestor; ancestor = S.rig.byId[ancestor.parent]) {
      if (ancestor.id === S.sel) return false;
    }
    return true;
  };
  const ps = $('pParent'); ps.innerHTML = '<option value="">(루트)</option>' + S.data.parts.filter(validParent).map((p) => `<option>${p.id}</option>`).join('');
  const as = $('aSel'); as.innerHTML = Object.keys(S.data.anims).map((n) => `<option ${n === S.anim ? 'selected' : ''}>${n}</option>`).join('');
  const a = anim();
  $('aLen').value = a.length; $('aLoop').checked = a.loop !== false; $('aInterp').value = a.interp || 'step';
  $('aStep').value = S.data.stepFps ?? 12; $('aStepV').textContent = (S.data.stepFps || 0) ? S.data.stepFps + ' fps' : '끔(부드럽게)';
  refreshProps();
}
function refreshProps() {
  const p = S.rig.byId[S.sel]; if (!p) return;
  $('selName').textContent = p.id;
  $('pParent').value = p.parent || '';
  $('pPx').value = p.pivot[0]; $('pPy').value = p.pivot[1];
  $('pAx').value = p.parent ? p.attach[0] : S.data.rootPos[0]; $('pAy').value = p.parent ? p.attach[1] : S.data.rootPos[1];
  $('pZ').value = p.z || 0; $('pRest').value = p.rest || 0;
  $('pScale').value = p.scale || 1; $('pShade').value = p.shade || 0; $('pHidden').checked = !!p.hidden;
}
function bindNum(id, fn) { $(id).addEventListener('change', async (e) => { snapshot(); await fn(S.rig.byId[S.sel], parseFloat(e.target.value) || 0, e); refreshUI(); }); }
bindNum('pPx', (p, v) => { p.pivot[0] = v; });
bindNum('pPy', (p, v) => { p.pivot[1] = v; });
bindNum('pAx', (p, v) => { if (p.parent) p.attach[0] = v; else S.data.rootPos[0] = v; });
bindNum('pAy', (p, v) => { if (p.parent) p.attach[1] = v; else S.data.rootPos[1] = v; });
bindNum('pZ', (p, v) => { p.z = v; });
bindNum('pRest', (p, v) => { p.rest = v; });
bindNum('pScale', (p, v) => { p.scale = v || 1; });
bindNum('pShade', async (p, v) => { p.shade = Math.max(0, Math.min(0.9, v)); await rebuild(); });
$('pHidden').addEventListener('change', (e) => { snapshot(); S.rig.byId[S.sel].hidden = e.target.checked; refreshUI(); });
$('pParent').addEventListener('change', (e) => {
  snapshot(); const p = S.rig.byId[S.sel];
  p.parent = e.target.value || null; if (p.parent && !p.attach) p.attach = [0, 0];
  refreshUI();
});
$('aSel').addEventListener('change', (e) => { S.anim = e.target.value; S.t = 0; S.selKey = null; refreshUI(); });
$('aLen').addEventListener('change', (e) => { snapshot(); anim().length = Math.max(0.1, parseFloat(e.target.value) || 1); refreshUI(); });
$('aLoop').addEventListener('change', (e) => { snapshot(); anim().loop = e.target.checked; });
$('aInterp').addEventListener('change', (e) => { snapshot(); anim().interp = e.target.value; });
$('aStep').addEventListener('input', (e) => { S.data.stepFps = parseInt(e.target.value); S.dirty = true; refreshUI(); });
$('bKey').onclick = () => { if (S.mode !== 'anim') setMode('anim'); snapshot(); keyCurrent(S.sel); };
$('bDelKey').onclick = deleteKey;
$('bKeyAll').onclick = () => { if (S.mode !== 'anim') setMode('anim'); snapshot(); S.data.parts.forEach((p) => keyCurrent(p.id)); };
$('bNewAnim').onclick = () => {
  const n = prompt('새 클립 이름 (예: victory)'); if (!n || S.data.anims[n]) return;
  snapshot(); S.data.anims[n] = { length: 1, loop: true, interp: 'step', keys: {}, root: [] }; S.anim = n; refreshUI();
};
$('bPlay').onclick = togglePlay;
$('bSave').onclick = save;
$('bUndo').onclick = undo;
$('bPreview').onclick = () => { S.preview = !S.preview; $('bPreview').classList.toggle('primary', S.preview); };
document.querySelectorAll('.tabs button').forEach((b) => (b.onclick = () => setMode(b.dataset.mode)));
function setMode(m) { S.mode = m; document.querySelectorAll('.tabs button').forEach((b) => b.classList.toggle('on', b.dataset.mode === m)); }
function togglePlay() { S.playing = !S.playing; $('bPlay').textContent = S.playing ? '❚❚ 정지 (Enter)' : '▶ 재생 (Enter)'; if (S.playing && S.mode === 'setup') setMode('anim'); }

addEventListener('keydown', (e) => {
  if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
  if (e.key === ' ') { spaceDown = true; e.preventDefault(); }
  if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); save(); }
  else if ((e.ctrlKey || e.metaKey) && e.key === 'z') { e.preventDefault(); undo(); }
  else if (e.key === 'Enter') togglePlay();
  else if (e.key === 'k' || e.key === 'K') $('bKey').onclick();
  else if (e.key === 'Delete' || e.key === 'Backspace') deleteKey();
  else if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
    const f = 1 / (S.data.stepFps || 12), a = anim();
    S.t = snapT(((S.t + (e.key === 'ArrowRight' ? f : -f)) % a.length + a.length) % a.length);
  } else if (e.key === 'Tab') { e.preventDefault(); setMode(S.mode === 'setup' ? 'anim' : 'setup'); }
  else if (e.key === 'f' || e.key === 'F') fitView();
});
addEventListener('keyup', (e) => { if (e.key === ' ') spaceDown = false; });
addEventListener('resize', resize);
addEventListener('beforeunload', (e) => { if (S.dirty) e.preventDefault(); });

let last = performance.now();
function loop(now) {
  const dt = Math.min(0.05, (now - last) / 1000); last = now;
  if (S.data) {
    if (S.playing) { const a = anim(); S.t = a.loop === false ? Math.min(a.length, S.t + dt) : (S.t + dt) % a.length; if (a.loop === false && S.t >= a.length) S.t = 0; }
    if (S.preview) stepPreview(dt);
    render(); renderTL();
  }
  requestAnimationFrame(loop);
}
resize();
load().catch((e) => status('불러오기 실패: ' + e.message));
requestAnimationFrame(loop);
window.__rig = S; // 디버그용
