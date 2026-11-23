// 통그림(전신 키포즈 레퍼런스) 대비 리그 포즈 자동 맞춤.
// 레퍼런스와 리그를 같은 격자에 그린 뒤, 실루엣 IoU + 색 영역(털/코트/피치/회색/황동) IoU 가 최대가 되도록
// 관절 각도와 루트 위치를 좌표 하강법으로 찾는다. 결과는 사람이 에디터에서 겹쳐 보며 미세 조정한다.
import { computeMatrices, drawRig, partWorld } from './runtime.js';

export const POSES = ['idle', 'idle_in', 'run_contact', 'run_down', 'run_pass', 'run_up',
  'shoot_windup', 'shoot_release', 'shoot_follow', 'pull_grab', 'pull_yank', 'hurt'];
const GRID = { w: 150, h: 180, floor: 10 };

// 플랫 팔레트 → 비교용 색 영역. 외곽선(아주 어두운 색)은 비교에서 뺀다.
const CLASSES = [
  [1, [217, 143, 166]], [1, [180, 111, 136]], [1, [232, 167, 185]], [1, [201, 129, 155]], [1, [228, 106, 138]],
  [2, [122, 45, 69]], [2, [86, 31, 51]], [2, [91, 43, 59]],
  [3, [239, 192, 163]], [3, [207, 156, 132]], [3, [231, 210, 196]],
  [4, [141, 133, 146]], [4, [207, 200, 210]], [4, [159, 151, 166]],
  [5, [200, 154, 75]],
];
function classify(data, out) {
  for (let i = 0, j = 0; i < data.length; i += 4, j++) {
    const a = data[i + 3];
    if (a < 128) { out[j] = 0; continue; }
    const r = data[i], g = data[i + 1], b = data[i + 2];
    if (r + g + b < 140) { out[j] = 255; continue; } // outline: silhouette only
    let best = 1, bd = 1e9;
    for (const [c, [cr, cg, cb]] of CLASSES) {
      const d = (r - cr) ** 2 + (g - cg) ** 2 + (b - cb) ** 2;
      if (d < bd) { bd = d; best = c; }
    }
    out[j] = best;
  }
  return out;
}

function chromaCanvas(img) {
  const c = document.createElement('canvas');
  c.width = img.naturalWidth; c.height = img.naturalHeight;
  const g = c.getContext('2d', { willReadFrequently: true });
  g.drawImage(img, 0, 0);
  const d = g.getImageData(0, 0, c.width, c.height);
  let top = c.height, bottom = 0, left = c.width, right = 0;
  for (let y = 0; y < c.height; y++) for (let x = 0; x < c.width; x++) {
    const i = (y * c.width + x) * 4;
    if (d.data[i + 3] > 128) { top = Math.min(top, y); bottom = Math.max(bottom, y); left = Math.min(left, x); right = Math.max(right, x); }
  }
  return { canvas: c, box: { top, bottom, left, right, cx: (left + right) / 2 } };
}

export async function loadReference(name) {
  const img = new Image();
  img.src = `assets/ref/poses/pose_${name}.png`;
  await img.decode();
  return { name, img, ...chromaCanvas(img) };
}

/** 리그 기본 자세의 키(리그 단위): 바닥(0)에서 가장 높은 점까지 */
export function rigHeight(rig, pose) {
  const M = computeMatrices(rig, pose, new DOMMatrix());
  let top = 0;
  for (const p of rig.data.parts) {
    const img = rig.img[p.id];
    if (!img || p.hidden) continue;
    for (const [x, y] of [[0, 0], [img.width, 0], [0, img.height], [img.width, img.height]]) {
      top = Math.min(top, M[p.id].transformPoint(new DOMPoint(x - p.pivot[0], y - p.pivot[1])).y);
    }
  }
  return -top;
}

/** 레퍼런스를 리그 좌표(바닥 중앙 원점)에 놓는 행렬. refScale = 리그 단위 / 레퍼런스 px */
export function referenceMatrix(ref, refScale, nudge = [0, 0]) {
  return new DOMMatrix().translate(nudge[0], nudge[1]).scale(refScale).translate(-ref.box.cx, -ref.box.bottom);
}

export function fitPose(rig, ref, refScale, start, opt = {}) {
  const k = (GRID.h - GRID.floor - 6) / (opt.height || 320);
  const G = new DOMMatrix().translate(GRID.w / 2, GRID.h - GRID.floor).scale(k);
  const c = document.createElement('canvas'); c.width = GRID.w; c.height = GRID.h;
  const g = c.getContext('2d', { willReadFrequently: true });
  // 레퍼런스 영역 맵
  g.setTransform(G.multiply(referenceMatrix(ref, refScale)));
  g.drawImage(ref.canvas, 0, 0);
  const refMap = classify(g.getImageData(0, 0, GRID.w, GRID.h).data, new Uint8Array(GRID.w * GRID.h));
  const mine = new Uint8Array(GRID.w * GRID.h);
  const ids = (opt.parts || rig.data.parts.map((p) => p.id)).filter((id) => rig.byId[id]);
  const ang = { ...start.ang };
  const base0 = { ...start.ang };
  let root = [...start.root];
  let evals = 0;
  const score = () => {
    evals++;
    g.setTransform(1, 0, 0, 1, 0, 0);
    g.clearRect(0, 0, GRID.w, GRID.h);
    g.setTransform(G);
    drawRig(g, rig, { ang, root }, new DOMMatrix());
    classify(g.getImageData(0, 0, GRID.w, GRID.h).data, mine);
    let sI = 0, sU = 0, cI = 0, cU = 0;
    for (let i = 0; i < mine.length; i++) {
      const a = mine[i], b = refMap[i];
      if (a || b) { sU++; if (a && b) sI++; }
      const ca = a !== 0 && a !== 255, cb = b !== 0 && b !== 255;
      if (ca || cb) { cU++; if (ca && cb && a === b) cI++; }
    }
    let reg = 0;
    for (const id of ids) reg += ((ang[id] - base0[id]) / 90) ** 2;
    let ground = 0;
    if (opt.grounded && feet.length) {
      // 접지: 가장 낮은 발바닥이 바닥선(0)에 닿아 있어야 한다 (발마다 깊이 보정).
      const M = computeMatrices(rig, { ang, root }, new DOMMatrix());
      const low = Math.max(...feet.map((f) => partWorld(M, f.id, f.sole)[1] - f.depth));
      ground = (low / 25) ** 2;
    }
    return sI / (sU || 1) + 0.9 * cI / (cU || 1) - 0.012 * reg - 0.05 * ground;
  };
  const feet = rig.data.feet || [];
  let best = score();
  const range = opt.range ?? 170;
  // 팔·바늘 외에는 손으로 만든 원래 자세 근처에서만 조정한다.
  // 바늘은 가늘어서 실루엣 비교로는 방향을 못 읽는다 → 손 기준 거의 고정.
  const limit = (id) => (sweep.includes(id) ? range : id === 'needle' ? 20 : /tail|coat|skirt/.test(id) ? 45 : /hips|torso|head/.test(id) ? 35 : 70);
  // 1) 큰 동작 파츠는 넓게 훑어서 국소해(예: 머리 뒤로 젖힌 팔)를 피한다. 부모→자식 순서로 두 번.
  const sweep = ['near_arm', 'near_forearm', 'far_arm', 'far_forearm'].filter((id) => ids.includes(id));
  for (let pass = 0; pass < 2; pass++) {
    for (const id of sweep) {
      const center = ang[id];
      let keep = center;
      for (let d = -150; d <= 150; d += 25) {
        if (!d || Math.abs(center + d - base0[id]) > limit(id)) continue;
        ang[id] = center + d;
        const s = score();
        if (s > best + 1e-5) { best = s; keep = ang[id]; }
      }
      ang[id] = keep;
    }
  }
  for (const step of [24, 12, 6, 3, 1.5]) {
    for (let pass = 0; pass < 3; pass++) {
      let improved = false;
      // 루트 위치
      for (const axis of [0, 1]) for (const dir of [1, -1]) {
        const old = root[axis];
        root[axis] = old + dir * step * 0.9;
        // 레퍼런스는 바운딩 박스 중심 정렬이라 좌우 위치는 믿을 수 없다 → x 는 조금만 허용
        if (axis === 0 && Math.abs(root[0] - start.root[0]) > (opt.rootX ?? 18)) { root[0] = old; continue; }
        const s = score();
        if (s > best + 1e-5) { best = s; improved = true; } else root[axis] = old;
      }
      for (const id of ids) for (const dir of [1, -1]) {
        const old = ang[id];
        const next = old + dir * step;
        if (Math.abs(next - base0[id]) > limit(id)) continue;
        ang[id] = next;
        const s = score();
        if (s > best + 1e-5) { best = s; improved = true; } else ang[id] = old;
      }
      if (!improved) break;
    }
  }
  return { ang, root, score: best, evals };
}
