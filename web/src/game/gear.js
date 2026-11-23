export const SETS = {
  chaos: { name: '혼돈', color: '#e89bb7', thresholds: [2, 4, 6, 8, 10], bonus: '실 피해' },
  despair: { name: '절망', color: '#8cc7be', thresholds: [3, 6, 9], bonus: '털실 최대' },
  frustration: { name: '좌절', color: '#e3c46b', thresholds: [1, 3, 5], bonus: '이동 속도' },
};

const PIECES = [
  { name: '붉은 골무', set: 'chaos', cost: 1, stat: 'damage', value: 2, shape: [[0,0],[1,0]] },
  { name: '잊힌 매듭', set: 'chaos', cost: 2, stat: 'damage', value: 3, shape: [[0,0],[0,1],[1,1]] },
  { name: '낡은 단추', set: 'frustration', cost: 1, stat: 'speed', value: .04, shape: [[0,0]] },
  { name: '새벽의 실패', set: 'despair', cost: 1, stat: 'recharge', value: .12, shape: [[0,0],[0,1]] },
  { name: '봉인된 도안', set: 'despair', cost: 3, stat: 'purity', value: 3, shape: [[0,0],[1,0],[0,1],[1,1]] },
  { name: '거울 실핀', set: 'frustration', cost: 2, stat: 'health', value: 6, shape: [[0,0],[1,0],[2,0]] },
  { name: '운명의 재단틀', set: 'chaos', cost: 4, stat: 'damage', value: 5, shape: [[1,0],[0,1],[1,1],[2,1]] },
];
export const RANK = ['', '일반', '레어', '유니크', '레전더리'];
const SAVE_KEY = 'tower-of-desire.profile.v1';

export function loadProfile() {
  try {
    const saved = JSON.parse(localStorage.getItem(SAVE_KEY));
    if (saved?.version === 1 && Array.isArray(saved.items) && Array.isArray(saved.placements)) return saved;
  } catch { /* A fresh profile also works when browser storage is disabled. */ }
  return { version: 1, shards: 0, best: 0, expansion: 0, rows: 0, capacity: 0,
    items: [0, 1, 2, 3].map((kind, id) => ({ id: id + 1, kind })),
    placements: [], nextId: 5 };
}

export function saveProfile(profile) {
  try { localStorage.setItem(SAVE_KEY, JSON.stringify(profile)); } catch { /* Continue in memory. */ }
}

export function itemDef(item) { return PIECES[item.kind]; }
export function dimensions(profile) { return [6 + profile.expansion, 4 + profile.expansion, 30 + profile.expansion * 2]; }
export function addItem(profile, kind = Math.floor(Math.random() * PIECES.length)) {
  const item = { id: profile.nextId++, kind };
  profile.items.push(item);
  return itemDef(item);
}

export function shapeFor(item, turns = 0) {
  let shape = itemDef(item).shape;
  for (let step = 0; step < turns; step++) {
    const bottom = Math.max(...shape.map(([, y]) => y));
    shape = shape.map(([x, y]) => [bottom - y, x]);
  }
  return shape;
}

export function placedItems(profile) {
  return profile.placements.map(placement => {
    const item = profile.items.find(entry => entry.id === placement.id);
    return { ...placement, def: itemDef(item), cells: shapeFor(item, placement.turns).map(([x,y]) => [x + placement.x, y + placement.y]) };
  });
}

export function canPlace(profile, item, turns, x, y) {
  const [width, height, limit] = dimensions(profile);
  const occupied = placedItems(profile).filter(piece => piece.id !== item.id);
  if (occupied.reduce((sum, piece) => sum + piece.def.cost, itemDef(item).cost) > limit) return false;
  return shapeFor(item, turns).every(([dx, dy]) => {
    const col = x + dx;
    const row = y + dy;
    return col >= 0 && row >= 0 && col < width && row < height &&
      !occupied.some(piece => piece.cells.some(([a,b]) => a === col && b === row));
  });
}

export function gearStats(profile) {
  const pieces = placedItems(profile);
  const stats = { damage: 0, speed: 0, health: 0, recharge: 0, purity: 0, yarn: 0 };
  const groups = {};
  for (const piece of pieces) {
    const reinforced = piece.cells.filter(([,y]) => y < profile.rows * 2).length / piece.cells.length;
    stats[piece.def.stat] += piece.def.value * (1 + reinforced * .1);
  }
  for (const [set, def] of Object.entries(SETS)) {
    const remaining = new Set(pieces.filter(piece => piece.def.set === set));
    let size = 0;
    while (remaining.size) {
      const initial = remaining.values().next().value;
      const cluster = [initial];
      remaining.delete(initial);
      for (const piece of cluster) {
        for (const other of remaining) {
          const adjacent = piece.cells.some(([x,y]) => other.cells.some(([a,b]) => Math.abs(x-a) + Math.abs(y-b) === 1));
          if (adjacent) { cluster.push(other); remaining.delete(other); }
        }
      }
      size = Math.max(size, cluster.length);
    }
    const tiers = def.thresholds.filter(value => value <= size);
    groups[set] = { size, threshold: tiers.at(-1) || 0, tier: tiers.length };
  }
  stats.damage += groups.chaos.tier * 2;
  stats.yarn += groups.despair.tier;
  stats.speed += groups.frustration.tier * .05;
  return { stats, groups };
}
