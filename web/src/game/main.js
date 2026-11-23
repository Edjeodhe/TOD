import { adjustDesire, applyAugment, augmentChoices, beginFloor, beginRound, chooseDesire, createRun,
  desireFraction, FINAL_FLOOR, knotInterval, ROUND_DURATION, update, xpNeeded, yarnMax } from './combat.js';
import { SETS, RANK, addItem, canPlace, dimensions, gearStats, itemDef, loadProfile, placedItems,
  saveProfile, shapeFor } from './gear.js';
import { canvasPoint, createRenderer } from './render.js';
import { createPostFX } from './shader.js';

const get = id => document.getElementById(id);
const buttons = selector => [...document.querySelectorAll(selector)];
const profile = loadProfile();
const canvas = get('world');
const renderer = createRenderer(canvas);
// 유령신부/굶지마 톤 후처리 (?fx=0 으로 끄기)
let postfx = { draw() {} };
try { postfx = createPostFX(canvas, { enabled: new URLSearchParams(location.search).get('fx') !== '0' }); }
catch (error) { console.warn('postfx off:', error); canvas.style.opacity = ''; }
const room = get('room');
const overlay = get('overlay');
const keys = new Set();
let run = null;
let summary = null;
let tab = 'climb';
let paused = false;
let opened = '';
let selected = null;
let turns = 0;
let aim = { x: 635, y: 350 };
let pointer = null;
let fireRequested = false;
let pull = false;
let dash = false;
let bannerUntil = 0;
let noticeUntil = 0;
let sound = false;
let audio;
let lastWhisper = 0;
let previousFrame = 0;
let shop;
let animatingDeath = false;
let rigReady = false;

const starts = { climb: ['등반 준비', 'T H E  A S C E N T'], inventory: ['재봉 상자', 'T H E  S E W I N G  B O X'],
  attic: ['예견의 다락','M O I R A ’ S  A T T I C'] };
const toolNames = { damage: '실 피해', speed: '이동 속도', recharge: '충전 속도', health: '체력', purity: '입장 욕망 감소' };
const upgradeDefs = [
  { key: 'expansion', name: '재봉 상자 확장', text: '가로·세로 1칸씩 추가 · 등급 한도 +2', icon: 'knot', price: 8 },
  { key: 'rows', name: '가로 줄 강화', text: '두 줄씩 강화 · 해당 칸의 장비 능력치 +10%', icon: 'yarn', price: 6 },
  { key: 'capacity', name: '욕망 최대치', text: '다음 등반부터 욕망 최대치 +10', icon: 'desire', price: 7 },
];
const wares = [
  { id: 'heal', name: '잿빛 회복 포션', text: '체력 32 회복', icon: 'potion', base: 14 },
  { id: 'pure', name: '정화 포션', text: '욕망 20 감소', icon: 'desire', base: 19 },
  { id: 'piece', name: '수상한 재봉 조각', text: '재봉 상자에 무작위 조각 추가', icon: 'knot', base: 23 },
  { id: 'buff', name: '달빛 코팅', text: '이번 등반에서 실 피해 +4', icon: 'needle', base: 28 },
];
const sprite = name => `<img src="assets/sprites/icon_${name}.png" alt="">`;
const effectImage = name => `<img class="ui-vfx" src="assets/vfx/${name}.png" alt="">`;

function beep(kind, power = 1) {
  if (!sound || !audio || audio.state !== 'running') return;
  const settings = {
    shoot: [510,255,.047,.027,'triangle'], pull: [135,43,.19,.1,'triangle'], dash: [260,73,.16,.057,'sine'],
    knot: [620,420,.14,.053,'triangle'], dodge: [270,670,.18,.06,'sine'],
    hurt: [130,47,.15,.075,'sawtooth'], loot: [543,830,.12,.044,'sine'], enemyShot: [114,56,.105,.022,'triangle'],
    round: [225,403,.22,.05,'triangle'], desire: [164,48,.78,.075,'sine'], upgrade: [390,892,.43,.07,'triangle'],
    whisper: [55,47,1.25,.035,'sine'],
  }[kind];
  if (!settings) return;
  const [from,to,duration,volume,wave] = settings;
  const now = audio.currentTime;
  const oscillator = audio.createOscillator();
  const gain = audio.createGain();
  oscillator.type = wave;
  oscillator.frequency.setValueAtTime(from,now);
  oscillator.frequency.exponentialRampToValueAtTime(to,now+duration);
  gain.gain.setValueAtTime(.0001,now);
  gain.gain.exponentialRampToValueAtTime(volume*Math.min(1.5,power),now+.015);
  gain.gain.exponentialRampToValueAtTime(.0001,now+duration);
  oscillator.connect(gain).connect(audio.destination);
  oscillator.start(now);
  oscillator.stop(now+duration+.01);
}

function say(message) {
  get('notice').textContent = message;
  get('notice').hidden = false;
  noticeUntil = performance.now()+2600;
}

function banner(message, duration = 1800) {
  get('arena-banner').textContent = message;
  bannerUntil = performance.now()+duration;
}

function textTo(id, value) {
  const target = get(id);
  if (target.textContent !== String(value)) target.textContent = value;
}

function meter(id, value) { get(id).style.width = `${Math.min(100,Math.max(0,value*100))}%`; }

function updateHUD() {
  const stats = run?.gear || gearStats(profile).stats;
  const hp = run?.hp ?? Math.round(100+stats.health);
  const cap = run?.maxHp ?? hp;
  const maxYarn = run ? yarnMax(run) : Math.round(6+stats.yarn);
  const yarn = Math.floor(run?.yarn ?? maxYarn);
  const maxDesire = run?.maxDesire ?? 100 + profile.capacity*10;
  const desire = Math.floor(run?.desire ?? 0);
  textTo('level', `LV. ${run?.level || 1}`);
  textTo('meta-shards',profile.shards);
  textTo('hp-value',`${hp} / ${cap}`);
  textTo('yarn-value',`${yarn} / ${maxYarn}`);
  textTo('thread-count',`실 ${run?.threads.length || 0}가닥`);
  textTo('knot-ready',`매듭까지 ${run ? Math.max(1,knotInterval(run)-run.shotCount) : 5}발`);
  textTo('desire-value',`${desire} / ${maxDesire}${run?.mood === 'frenzy' ? ' · 폭주' : run?.mood === 'awakened' ? ' · 각성' : ''}`);
  textTo('gold',run?.gold || 0);
  textTo('knots',run?.knots.length || 0);
  textTo('xp-value',`${run?.xp || 0} / ${run ? xpNeeded(run) : 5}`);
  meter('hp-bar',hp/cap);
  meter('desire-bar',desire/maxDesire);
  meter('xp-bar',run ? run.xp/xpNeeded(run) : 0);
  const pips = get('yarn-pips');
  const pipCount = Math.min(25,maxYarn);
  if (pips.childElementCount !== pipCount) pips.innerHTML = '<i></i>'.repeat(pipCount);
  const charged = Math.round(yarn/maxYarn*pipCount);
  [...pips.children].forEach((pip,index) => {
    pip.classList.toggle('full',index < charged);
    pip.style.setProperty('--winding',index === charged ? `${(run?.winding || 0)*100}%` : '0%');
  });
  const floor = run?.floor || 1;
  textTo('floor-label',`${String(floor).padStart(2,'0')}F`);
  const best = get('best-record').querySelector('b');
  best.textContent = profile.best ? `${profile.best}층 돌파` : '미기록';
  const round = run?.round || 1;
  const done = run && (run.phase !== 'combat' || run.outcome === 'clear');
  [...get('rounds').children].forEach((li,i) => {
    li.classList.toggle('current',i === round-1 && !done);
    li.classList.toggle('done',i < round-1 || (i === round-1 && Boolean(done)));
  });
  let route = '층마다 세 라운드를 돌파하세요.';
  if (run) {
    const remaining = ROUND_DURATION[round] - run.elapsed;
    if (run.phase === 'shop') route = '떠돌이 상인 단추를 만났습니다.';
    else if (run.phase !== 'combat') route = `${floor}층 · ${round}라운드 돌파`;
    else if (round === 3) route = '공격 예고를 보고 대쉬로 피하세요.';
    else if (remaining > 0) route = `적 등장 종료까지 ${Math.ceil(remaining)}초`;
    else route = `남은 적 ${run.enemies.length}체`;
  }
  textTo('route-info',route);
  const boss = run?.enemies.find(e => e.type === 'boss');
  get('boss-hud').hidden = !boss;
  if (boss) get('boss-hud').querySelector('i').style.width = `${boss.hp/boss.maxHp*100}%`;
}

function showDialog(id, markup) {
  opened = id;
  pointer = null; fireRequested = false; pull = false; dash = false; keys.clear();
  overlay.innerHTML = `<div class="dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title">${markup}</div>`;
  overlay.hidden = false;
  const focusable = overlay.querySelector('button:not(:disabled)');
  if (focusable && matchMedia('(pointer: fine)').matches) focusable.focus({ preventScroll: true });
}

function closeDialog() {
  opened = '';
  overlay.hidden = true;
  overlay.innerHTML = '';
}

function header(label,title,desc) {
  return `<p class="eyebrow">${label}</p><h2 id="dialog-title">${title}</h2>${desc ? `<p>${desc}</p>` : ''}`;
}

function showTab(next) {
  if (run && next !== 'climb') {
    say('재봉 상자와 다락은 등반을 마친 뒤 이용할 수 있습니다.');
    return;
  }
  tab = next;
  const [title, label] = starts[tab];
  textTo('page-title',run ? `${run.floor}층 · 욕망의 탑` : title);
  textTo('page-kicker',label);
  buttons('nav [data-tab]').forEach(button => {
    const active = button.dataset.tab === tab;
    button.classList.toggle('selected',active);
    if (active) button.setAttribute('aria-current','page');
    else button.removeAttribute('aria-current');
  });
  get('welcome').hidden = tab !== 'climb' || Boolean(run) || Boolean(summary);
  get('arena').hidden = tab !== 'climb' || !run;
  get('touch-controls').hidden = !run || paused || Boolean(opened) || animatingDeath;
  room.hidden = tab === 'climb' && !summary;
  room.classList.toggle('attic',tab === 'attic');
  if (tab === 'inventory') renderInventory();
  if (tab === 'attic') renderAttic();
  if (tab === 'climb' && summary && !run) renderSummary();
  updateHUD();
}

function equippedShape(item, rotation, color) {
  const cells = shapeFor(item,rotation);
  const width = Math.max(...cells.map(([x])=>x))+1;
  return `<span class="piece-icon" style="--piece:${color};grid-template-columns:repeat(${width},5px)">${cells.map(([x,y]) => `<i style="grid-column:${x+1};grid-row:${y+1}"></i>`).join('')}</span>`;
}

function renderInventory(focusCell) {
  const [width,height,limit] = dimensions(profile);
  const placed = placedItems(profile);
  const holding = profile.items.find(i => i.id === selected);
  const layout = [...Array(width*height)].map((_,index) => {
    const x = index%width; const y = Math.floor(index/width);
    const occupying = placed.find(piece => piece.cells.some(([a,b]) => x === a && y === b));
    const color = occupying ? SETS[occupying.def.set].color : '#e89bb7';
    return `<button class="cell ${occupying ? 'filled' : ''} ${y < profile.rows*2 ? 'reinforced' : ''}" data-cell="${x}:${y}" style="--piece:${color}" aria-label="${y+1}행 ${x+1}열${occupying ? ' · '+occupying.def.name : ''}">${occupying && x === occupying.x && y === occupying.y ? '✦' : ''}</button>`;
  }).join('');
  const cost = placed.reduce((sum,p)=>sum+p.def.cost,0);
  const { groups } = gearStats(profile);
  const list = profile.items.map(item => {
    const def = itemDef(item);
    const placement = placed.find(p=>p.id === item.id);
    const value = def.value < 1 ? `${Math.round(def.value*100)}%` : def.value;
    return `<button data-piece="${item.id}" aria-pressed="${item.id === selected}" title="${RANK[def.cost]} · ${toolNames[def.stat]} +${value}">${equippedShape(item,item.id === selected ? turns : placement?.turns || 0,SETS[def.set].color)} ${def.name}<small>${placement ? '장착 · ' : ''}${def.cost}</small></button>`;
  }).join('');
  room.innerHTML = `<div class="room-body"><p class="eyebrow">✧ &nbsp; ${width} × ${height} &nbsp; · &nbsp; 등급 합계 ${cost} / ${limit}</p>
    <h2>재봉 상자</h2><p>조각을 선택하고 판을 누르세요. 같은 색의 변을 맞대면 세트 효과가 켜집니다.</p>
    <div class="inventory-layout"><div class="board" style="--columns:${width}" aria-label="조각 배치판">${layout}</div><div class="stash" aria-label="보유 조각">${list}</div></div>
    <div class="sets">${Object.entries(SETS).map(([key,s])=>`<span style="--piece:${s.color}"><b>${s.name}</b> ${groups[key].size}개 · ${groups[key].threshold ? `${groups[key].threshold}세트` : '미활성'} · ${s.bonus}</span>`).join('')}</div>
    <div class="actions"><button class="secondary" data-do="rotate" ${holding ? '' : 'disabled'}>회전 (R)</button>
    <button class="secondary" data-do="unequip" ${placed.some(p=>p.id===selected) ? '' : 'disabled'}>판에서 빼기</button><button class="primary" data-tab="climb">등반 화면</button></div></div>`;
  if (focusCell) room.querySelector(`[data-cell="${focusCell}"]`)?.focus({ preventScroll: true });
}

function previewPiece(cell) {
  for (const tile of room.querySelectorAll('.cell.preview,.cell.invalid')) {
    tile.classList.remove('preview','invalid');
    tile.style.setProperty('--piece',tile.dataset.color || '');
  }
  const item = profile.items.find(entry=>entry.id === selected);
  if (!cell || !item) return;
  const [x,y] = cell.dataset.cell.split(':').map(Number);
  const valid = canPlace(profile,item,turns,x,y);
  for (const [dx,dy] of shapeFor(item,turns)) {
    const tile = room.querySelector(`[data-cell="${x+dx}:${y+dy}"]`);
    if (tile) {
      tile.dataset.color = tile.style.getPropertyValue('--piece');
      tile.style.setProperty('--piece',SETS[itemDef(item).set].color);
      tile.classList.add(valid ? 'preview' : 'invalid');
    }
  }
}

function upgradeCost(upgrade) { return upgrade.price + 5*profile[upgrade.key]; }
function upgradeMax(upgrade) {
  if (upgrade.key === 'expansion') return profile.expansion >= 2;
  if (upgrade.key === 'rows') return profile.rows >= Math.floor(dimensions(profile)[1]/2);
  return profile.capacity >= 10;
}

function renderAttic() {
  room.innerHTML = `<div class="room-body"><p class="eyebrow">✧ &nbsp; 마녀 모이라</p><h2>예견의 다락</h2>
    <p>기억의 파편을 내밀면 모이라가 다음 등반의 미래를 읽습니다.</p>
    <div class="choice-grid">${upgradeDefs.map(u=>`<button class="choice" data-upgrade="${u.key}" ${upgradeMax(u) || profile.shards < upgradeCost(u) ? 'disabled' : ''}>${sprite(u.icon)}
      <b>${u.name}</b><small>${u.text}</small><em>${upgradeMax(u) ? '최대 단계' : `파편 ${upgradeCost(u)}개 · 현재 ${profile[u.key]}단계`}</em></button>`).join('')}</div>
    <div class="actions"><button class="secondary" data-tab="climb">등반 화면</button></div></div>`;
}

function renderSummary() {
  const labels = { clear: '탑의 심장에 닿았다', dead: '끊어진 실', absorbed: '탑에 동화되었다', retreat: '재봉사의 귀환' };
  const effect = summary.outcome === 'clear' ? effectImage('stitch') : summary.outcome === 'absorbed' ? effectImage('boss') : '';
  room.innerHTML = `<div class="room-body">${effect}<p class="eyebrow">✧ &nbsp; 등반 결과</p><h2>${labels[summary.outcome]}</h2>
    <p>${summary.floor}층 도달 · 적 ${summary.kills}체 처치 · 기억의 파편 ${profile.shards}개 보유</p>
    <div class="actions"><button class="primary" data-do="restart">다시 등반</button><button class="secondary" data-tab="inventory">재봉 상자</button>
    <button class="secondary" data-tab="attic">예견의 다락</button><button class="secondary" data-do="lobby">프롤로그</button></div></div>`;
}

function showPause() {
  if (!run || run.phase !== 'combat' || run.outcome || (opened && opened !== 'pause')) return;
  paused = !paused;
  if (paused) showDialog('pause',`${header('등반 중','일시 정지','전투를 계속하거나 기억의 파편을 가지고 귀환할 수 있습니다.')}
    <div class="actions"><button class="primary" data-do="resume">계속하기</button><button class="secondary" data-do="retreat">다락으로 귀환</button></div>`);
  else closeDialog();
}

function finish(outcome) {
  summary = { outcome, floor: run.floor, kills: run.kills };
  saveProfile(profile);
  run = null;
  paused = false;
  animatingDeath = false;
  closeDialog();
  showTab('climb');
}

function start() {
  if (!rigReady || run || opened) return;
  closeDialog();
  summary = null;
  paused = false;
  run = createRun(profile);
  shop = null;
  aim = { x: 635, y: 350 };
  beginFloor(run);
  showTab('climb');
  canvas.scrollIntoView({ block: 'nearest' });
}

function showAugments() {
  const choices = augmentChoices(run);
  showDialog('augment',`${header('증강 선택',`레벨 ${run.level-run.augmentQueue+1}`,'세 가지 증강 중 하나를 선택하세요. 전투는 멈춰 있습니다.')}
    <div class="choice-grid">${choices.map(a=>`<button class="choice" data-augment="${a.id}">${sprite(a.icon)}<em>${a.rule?'규칙형':'수치형'}</em><b>${a.name}</b><small>${a.text}</small></button>`).join('')}</div>`);
}

function showDesire() {
  const offer = run.offer;
  showDialog('desire',`${effectImage('boss')}${header(`${run.floor}층 보스 격파`,offer.name,offer.text)}
    <p>${offer.id === 'refine' ? offer.penalty : `욕망 +20 · ${offer.penalty}`}</p>
    <div class="actions"><button class="primary" data-do="absorb">욕망 흡수</button><button class="secondary" data-do="refuse">그대로 지나가기</button></div>`);
}

function price(ware) {
  return Math.ceil(ware.base * (desireFraction(run) >= .5 ? 1.25 : 1) *
    (shop.sale === ware.id && shop.end > performance.now() ? .5 : 1));
}

function saleLabel() {
  const seconds = Math.ceil((shop.end - performance.now())/1000);
  if (seconds <= 0) return '타임 세일 종료';
  if (shop.sold.has(shop.sale)) return '할인 상품 판매 완료';
  return `타임 세일 ${seconds}초 · ${wares.find(w=>w.id===shop.sale).name} 50% 할인`;
}

function showShop() {
  if (!shop) shop = { sale: wares[Math.floor(run.random()*wares.length)].id, end: performance.now()+20000, sold: new Set() };
  showDialog('shop',`${header('층 사이 휴식처','떠돌이 상인 단추','금화를 내고 다음 층에 들고 갈 물품을 고르세요.')}
    <p id="sale-label">${saleLabel()}</p>
    <div class="choice-grid shop-grid">${wares.map(w=>`<button class="choice" data-buy="${w.id}" ${shop.sold.has(w.id) || run.gold < price(w) ? 'disabled' : ''}>
      ${sprite(w.icon)}<b>${w.name}</b><small>${w.text}</small><em>${shop.sold.has(w.id) ? '판매 완료' : `${price(w)} 골드`}</em></button>`).join('')}</div>
    <div class="actions"><button class="primary" data-do="next-floor">${run.floor+1}층으로 →</button><button class="secondary" data-do="retreat">귀환</button></div>`);
}

function buy(id) {
  const ware = wares.find(w=>w.id===id);
  if (shop.sold.has(id) || run.gold < price(ware)) return;
  run.gold -= price(ware);
  shop.sold.add(id);
  const potion = run.desires.has('hunger') ? .5 : 1;
  if (id === 'heal') run.hp = Math.min(run.maxHp,run.hp+32*potion);
  if (id === 'pure') adjustDesire(run,-20*potion);
  if (id === 'piece') say(`${addItem(profile).name} 획득`);
  if (id === 'buff') run.damage += 4;
  saveProfile(profile);
  beep('loot');
  showShop();
  updateHUD();
}

function syncDialog(now) {
  if (!run) return;
  if (run.outcome === 'dead' && !animatingDeath) {
    animatingDeath = true;
    run.deathClock = now/1000;
    closeDialog();
  }
  if (animatingDeath) {
    if (now/1000 - run.deathClock >= .58) finish('dead');
    return;
  }
  if (run.outcome) { finish(run.outcome); return; }
  if (opened || paused) return;
  if (run.augmentQueue) showAugments();
  else if (run.phase === 'desire') showDesire();
  else if (run.phase === 'shop') showShop();
  else if (run.phase === 'rest') showDialog('rest',`${header(`${run.floor}층 · ${run.round}라운드 돌파`,'잠깐의 숨 고르기',run.round === 2 ? '다음 라운드에서는 층 보스가 나타납니다.' : '다음 라운드에서 다시 털실을 가득 채웁니다.')}
      <div class="actions"><button class="primary" data-do="next-round">${run.round === 2 ? '보스에게 도전' : '다음 라운드'} →</button></div>`);
  else if (run.phase === 'victory') finish('clear');
}

const actions = {
  rotate() {
    if (selected) { turns = (turns+1)%4; renderInventory(); }
  },
  unequip() {
    profile.placements = profile.placements.filter(p=>p.id!==selected);
    saveProfile(profile); renderInventory(); updateHUD();
  },
  resume: showPause,
  retreat() {
    showDialog('retreat',`${header('등반 종료','지금 귀환할까요?','모은 기억의 파편과 장비는 가지고 돌아갑니다. 이번 등반의 골드와 증강은 사라집니다.')}
      <div class="actions"><button class="primary" data-do="confirm-retreat">귀환하기</button><button class="secondary" data-do="cancel-retreat">계속하기</button></div>`);
  },
  'confirm-retreat'() { finish('retreat'); },
  'cancel-retreat'() {
    closeDialog();
    if (paused) { paused = false; showPause(); }
  },
  'next-round'() { closeDialog(); run.round++; beginRound(run); },
  absorb() {
    chooseDesire(run,run.offer.id);
    run.effects.push({ kind:'sprite', sprite:'boss', ...run.player, size:260, time:1.2, max:1.2 });
    beep('desire');
    closeDialog();
  },
  refuse() { chooseDesire(run,null); closeDialog(); },
  'next-floor'() { closeDialog(); shop = null; run.floor++; beginFloor(run); showTab('climb'); },
  restart: start,
  lobby() { summary = null; showTab('climb'); },
  'upgrade-cancel': closeDialog,
};

document.addEventListener('click',event => {
  const button = event.target.closest('button');
  if (!button) return;
  const { tab: next, piece, cell, do: command, augment, buy: buyId, upgrade, confirmUpgrade } = button.dataset;
  if (next && !opened) showTab(next);
  if (piece) {
    selected = Number(piece);
    turns = profile.placements.find(p=>p.id===selected)?.turns || 0;
    renderInventory();
    room.querySelector(`[data-piece="${selected}"]`)?.focus({ preventScroll: true });
  }
  if (cell && tab === 'inventory') {
    const [x,y] = cell.split(':').map(Number);
    const item = profile.items.find(i=>i.id===selected);
    const occupying = placedItems(profile).find(p=>p.cells.some(([a,b])=>x===a && y===b));
    if (!item) {
      selected = occupying?.id || selected;
      turns = occupying?.turns ?? turns;
      renderInventory(cell);
    } else if (!canPlace(profile,item,turns,x,y)) say('조각이 겹치거나 판의 경계·등급 한도를 넘습니다.');
    else {
      profile.placements = profile.placements.filter(p=>p.id!==item.id);
      profile.placements.push({ id:item.id,x,y,turns });
      selected = null;
      saveProfile(profile); renderInventory(cell); updateHUD(); beep('loot');
    }
  }
  if (command) actions[command]?.();
  if (augment && opened === 'augment') {
    applyAugment(run,augment);
    run.effects.push({ kind:'sprite', sprite:'stitch', ...run.player, size:196, time:.75, max:.75 });
    closeDialog(); beep('upgrade');
  }
  if (buyId && opened === 'shop') buy(buyId);
  if (upgrade && tab === 'attic') {
    const choice = upgradeDefs.find(u=>u.key===upgrade);
    showDialog('upgrade',`${header('모이라의 의식','미래를 읽을까요?',`기억의 파편 ${upgradeCost(choice)}개를 써서 ${choice.name}을 강화합니다.`)}
      <div class="actions"><button class="primary" data-confirm-upgrade="${upgrade}">예, 미래를 읽어요</button><button class="secondary" data-do="upgrade-cancel">취소</button></div>`);
  }
  if (confirmUpgrade && opened === 'upgrade') {
    const choice = upgradeDefs.find(u=>u.key===confirmUpgrade);
    if (upgradeMax(choice) || profile.shards < upgradeCost(choice)) return;
    profile.shards -= upgradeCost(choice);
    profile[choice.key]++;
    saveProfile(profile); closeDialog(); renderAttic(); updateHUD(); beep('upgrade');
    const effect = document.createElement('div');
    effect.className = 'room-flash';
    effect.innerHTML = effectImage('stitch');
    room.append(effect);
    setTimeout(()=>effect.remove(),900);
    say(`${choice.name} · ${profile[choice.key]}단계`);
  }
});

room.addEventListener('pointerover',event => {
  if (tab === 'inventory') previewPiece(event.target.closest('[data-cell]'));
});
room.addEventListener('pointerleave',()=>{ if (tab === 'inventory') previewPiece(null); });
get('start').addEventListener('click',start);
get('start').disabled = true;
get('pause').addEventListener('click',showPause);
get('sound').addEventListener('click',async () => {
  const Audio = window.AudioContext || window.webkitAudioContext;
  if (!Audio) { say('이 브라우저에서 소리를 재생할 수 없습니다.'); return; }
  audio ??= new Audio();
  sound = !sound;
  if (sound) { await audio.resume(); beep('loot'); }
  const button = get('sound');
  button.setAttribute('aria-pressed',sound);
  button.setAttribute('aria-label',sound ? '소리 끄기' : '소리 켜기');
  button.title = sound ? '소리 끄기' : '소리 켜기';
  get('sound-mark').setAttribute('d',sound ? 'M16 8q5 4 0 8m2-11q8 7 0 14' : 'm16 10 5 5m0-5-5 5');
});

const isCombat = () => run && run.phase === 'combat' && !paused && !opened && !run.outcome && !run.augmentQueue;
const movement = { w:'w',a:'a',s:'s',d:'d',arrowup:'w',arrowleft:'a',arrowdown:'s',arrowright:'d' };
document.addEventListener('keydown', event => {
  const key = event.key.toLowerCase();
  if (key === 'escape' && !event.repeat) {
    if (opened === 'upgrade') closeDialog();
    else if (opened === 'retreat') actions['cancel-retreat']();
    else showPause();
  }
  if (key === 'enter' && tab === 'climb' && !run && !summary && !opened && !event.target.closest?.('button')) start();
  if (key === 'r' && tab === 'inventory') actions.rotate();
  if (!isCombat()) return;
  if (movement[key]) { event.preventDefault(); keys.add(movement[key]); }
  if (event.code === 'Space' && !event.target.closest?.('button')) { event.preventDefault(); if (!event.repeat) dash = true; }
});
document.addEventListener('keyup',event=>keys.delete(movement[event.key.toLowerCase()]));
function suspend() {
  pointer = null; fireRequested = false; keys.clear(); dash = false; pull = false;
  if (isCombat()) showPause();
}
window.addEventListener('blur',suspend);
document.addEventListener('visibilitychange',()=>{ if (document.hidden) suspend(); });
canvas.addEventListener('contextmenu',event=>event.preventDefault());
canvas.addEventListener('pointermove',event => {
  if (pointer === null || pointer === event.pointerId) aim = canvasPoint(canvas,event.clientX,event.clientY);
});
canvas.addEventListener('pointerdown',event => {
  if (!isCombat()) return;
  event.preventDefault();
  aim = canvasPoint(canvas,event.clientX,event.clientY);
  if (event.button === 2) { pull = true; return; }
  if (event.button === 0) {
    fireRequested = true;
    pointer = event.pointerId;
    canvas.setPointerCapture(event.pointerId);
  }
});
for (const type of ['pointerup','pointercancel','lostpointercapture']) {
  canvas.addEventListener(type,event=>{ if (pointer === event.pointerId) pointer = null; });
}
buttons('[data-move]').forEach(button => {
  button.addEventListener('pointerdown',event => {
    if (!isCombat()) return;
    event.preventDefault();
    button.setPointerCapture(event.pointerId);
    keys.add(button.dataset.move);
  });
  for (const type of ['pointerup','pointercancel','lostpointercapture']) button.addEventListener(type,()=>keys.delete(button.dataset.move));
});
get('touch-pull').addEventListener('pointerdown',event=>{ event.preventDefault(); pull = true; });
get('touch-dash').addEventListener('pointerdown',event=>{ event.preventDefault(); dash = true; });

renderer.ready.then(() => { rigReady = true; get('start').disabled = false; }).catch(() => {
  get('start').textContent = '캐릭터를 불러오지 못했습니다';
  say('프로젝트의 assets 폴더를 확인한 뒤 새로고침해 주세요.');
});

function frame(now) {
  const dt = Math.min((now-previousFrame)/1000,.032);
  previousFrame = now;
  if (now >= bannerUntil && get('arena-banner').textContent) textTo('arena-banner','');
  if (now >= noticeUntil) get('notice').hidden = true;
  if (isCombat()) {
    const canAct = run.freeze <= 0;
    update(run,dt,{ x:Number(keys.has('d'))-Number(keys.has('a')), y:Number(keys.has('s'))-Number(keys.has('w')),
      shoot:pointer !== null || fireRequested, dash, pull, aim });
    if (canAct) { fireRequested = false; pull = false; dash = false; }
    if (desireFraction(run) >= .5 && now-lastWhisper > 8600) { beep('whisper'); lastWhisper = now; }
  }
  if (run) {
    for (const item of run.events.splice(0)) {
      beep(item.type,item.type === 'pull' ? 1 + run.combo*.12 : 1);
      if (item.type === 'loot' || item.type === 'empty') say(item.message);
      else if (item.message) banner(item.message,item.type === 'desire' ? 3200 : 1700);
    }
    renderer.render(run,aim,now/1000,Boolean(isCombat()));
    postfx.draw(now/1000, Math.max(0, desireFraction(run) - .4) / .6);
    updateHUD();
    syncDialog(now);
    if (opened === 'shop') {
      if (now >= shop.end && !shop.expired) { shop.expired = true; showShop(); }
      textTo('sale-label',saleLabel());
    }
  }
  get('touch-controls').hidden = !isCombat();
  requestAnimationFrame(frame);
}

showTab('climb');
requestAnimationFrame(frame);
