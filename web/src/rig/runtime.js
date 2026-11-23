// 종이 인형 리그 런타임 (게임/에디터 공용)
// - 이미지 파츠와 중심축 노드는 같은 부모 좌표계와 피벗을 사용한다.
// - 각 클립의 interp와 stepFps 설정으로 키프레임을 평가한다.

const SPRITE_DIR = 'assets/sprites/';

export async function loadRig(url, { data, hitTesting = true } = {}) {
  const payload = data ?? await (await fetch(url, { cache: 'no-store' })).json();
  return buildRig(payload, new Map(), { hitTesting });
}

export async function buildRig(data, imgCache = new Map(), { hitTesting = true } = {}) {
  const rig = { data, img: {}, alpha: {} };
  await Promise.all(data.parts.map(async (p) => {
    if (!p.img) return;
    const key = p.img + '|' + (p.shade || 0);
    if (!imgCache.has(key)) imgCache.set(key, loadPart(p.img, p.shade || 0, hitTesting));
    const { canvas, alpha } = await imgCache.get(key);
    rig.img[p.id] = canvas; rig.alpha[p.id] = alpha;
  }));
  rig.byId = Object.fromEntries(data.parts.map((p) => [p.id, p]));
  return rig;
}

function loadImage(src) {
  return new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = rej; i.src = src; });
}

async function loadPart(file, shade, hitTesting) {
  const im = await loadImage(SPRITE_DIR + file);
  const c = document.createElement('canvas'); c.width = im.width; c.height = im.height;
  const g = c.getContext('2d'); g.drawImage(im, 0, 0);
  if (shade) { // 뒤쪽 팔다리용 어둡게
    g.globalCompositeOperation = 'source-atop';
    g.fillStyle = `rgba(22,19,27,${shade})`; g.fillRect(0, 0, c.width, c.height);
    g.globalCompositeOperation = 'source-over';
  }
  const alpha = hitTesting ? g.getImageData(0, 0, c.width, c.height) : null;
  return { canvas: c, alpha };
}

// ---- 포즈 계산 ----
function sampleTrack(track, t, interp) {
  if (!track || !track.length) return null;
  if (track.length === 1 || t <= track[0][0]) return track[0].slice(1);
  for (let i = 0; i < track.length - 1; i++) {
    const a = track[i], b = track[i + 1];
    if (t < b[0]) {
      if (interp === 'step') return a.slice(1);
      const u = (t - a[0]) / (b[0] - a[0]);
      return a.slice(1).map((v, k) => v + (b[k + 1] - v) * u);
    }
  }
  return track[track.length - 1].slice(1);
}

export function animTime(anim, t) {
  if (!anim) return 0;
  return anim.loop === false ? Math.min(t, anim.length) : ((t % anim.length) + anim.length) % anim.length;
}

/** 파츠별 각도와 루트 오프셋 반환 */
export function computePose(rig, animName, t, opt = {}) {
  const anim = rig.data.anims[animName];
  const pose = { ang: {}, root: [0, 0] };
  let tt = animTime(anim, t);
  const step = opt.stepFps ?? rig.data.stepFps ?? 12;
  if (step > 0) tt = Math.floor(tt * step) / step;
  const interp = opt.interp || anim?.interp || 'step';
  for (const p of rig.data.parts) {
    const v = anim && sampleTrack(anim.keys[p.id], tt, interp);
    pose.ang[p.id] = (p.rest || 0) + (v ? v[0] : 0);
  }
  const r = anim && sampleTrack(anim.root, tt, interp);
  if (r) pose.root = [r[0], r[1]];
  return pose;
}

/** 각 파츠의 월드 행렬(DOMMatrix) 계산. base = 캐릭터 발밑 기준 행렬 */
export function computeMatrices(rig, pose, base) {
  const M = {};
  const parts = rig.data.parts;
  const visit = (p) => {
    if (M[p.id]) return M[p.id];
    let m;
    if (!p.parent) {
      const o = rig.data.rootPos || [0, 0];
      m = base.translate(o[0] + pose.root[0], o[1] + pose.root[1]);
    } else {
      const par = rig.byId[p.parent];
      // attach 는 부모 '이미지 픽셀' 좌표 → 부모 pivot 기준으로 변환
      m = visit(par).translate(p.attach[0] - par.pivot[0], p.attach[1] - par.pivot[1]);
    }
    m = m.rotate(pose.ang[p.id] || 0);
    const s = p.scale || 1;
    if (s !== 1) m = m.scale(s, s);
    // 파츠 자신의 좌표계: 원점 = pivot
    M[p.id] = m;
    return m;
  };
  parts.forEach(visit);
  return M; // 이미지 그리기 전에 translate(-pivot) 필요
}

export function baseMatrix(x, y, scale, facing = 1) {
  return new DOMMatrix().translate(x, y).scale(scale * facing, scale);
}

// The two soles touch the projected floor at different depths in the three-quarter view.
function groundOffset(rig, matrices, base) {
  return Math.max(...rig.data.feet.map(foot => {
    const [, soleY] = partWorld(matrices,foot.id,foot.sole);
    return (soleY-base.f)/base.d - foot.depth;
  }));
}

// 클립별 접지 보정 감쇠(rig.data.groundDamp[clip] = 0~1): 사이클 평균 높이를 기준으로 보정량의 일부만 적용한다.
function groundMean(rig, clip) {
  rig._groundMean ||= {};
  if (rig._groundMean[clip] == null) {
    const anim = rig.data.anims[clip], base = new DOMMatrix();
    let sum = 0;
    for (let i = 0; i < 16; i++) sum += groundOffset(rig, computeMatrices(rig, computePose(rig, clip, anim.length * i / 16, { stepFps: 0 }), base), base);
    rig._groundMean[clip] = sum / 16;
  }
  return rig._groundMean[clip];
}

export function groundedMatrices(rig, pose, base, clip) {
  const matrices = computeMatrices(rig, pose, base);
  if (!rig.data.feet?.length) return matrices;
  let offset = groundOffset(rig, matrices, base);
  const damp = clip && rig.data.groundDamp?.[clip];
  if (damp != null && rig.data.anims[clip]) {
    const mean = groundMean(rig, clip);
    offset = mean + (offset - mean) * damp;
  }
  // Cap posture correction so an editor pose with both feet lifted can still jump.
  const correction = Math.min(46, Math.max(-46,offset));
  return Math.abs(correction) < .01 ? matrices : computeMatrices(rig,pose,base.translate(0,-correction));
}

export function drawRig(ctx, rig, pose, base, opt = {}) {
  const M = opt.matrices || computeMatrices(rig, pose, base);
  const order = [...rig.data.parts].sort((a, b) => (a.z || 0) - (b.z || 0));
  for (const p of order) {
    if (p.hidden || !p.img) continue;
    const m = M[p.id];
    ctx.save();
    ctx.setTransform(ctx.getTransform().multiply(m));
    if (opt.alpha != null) ctx.globalAlpha = opt.alpha;
    if (opt.filter) ctx.filter = opt.filter; // 피격 플래시 등
    ctx.drawImage(rig.img[p.id], -p.pivot[0], -p.pivot[1]);
    ctx.restore();
  }
  return M;
}

/** 화면 좌표(px,py; ctx 기준)에 있는 최상단 파츠 id (에디터용) */
export function hitTest(rig, M, px, py, view = new DOMMatrix()) {
  const order = [...rig.data.parts].sort((a, b) => (b.z || 0) - (a.z || 0));
  for (const p of order) {
    if (p.hidden || !rig.alpha[p.id]) continue;
    const inv = view.multiply(M[p.id]).inverse();
    const q = inv.transformPoint(new DOMPoint(px, py));
    const ix = Math.floor(q.x + p.pivot[0]), iy = Math.floor(q.y + p.pivot[1]);
    const a = rig.alpha[p.id];
    if (ix < 0 || iy < 0 || ix >= a.width || iy >= a.height) continue;
    if (a.data[(iy * a.width + ix) * 4 + 3] > 60) return p.id;
  }
  return null;
}

/** 파츠의 월드 위치(pivot 점) */
export function partWorld(M, id, local = [0, 0]) {
  const q = M[id].transformPoint(new DOMPoint(local[0], local[1]));
  return [q.x, q.y];
}
