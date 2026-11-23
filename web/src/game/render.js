import { baseMatrix, computeMatrices, computePose, drawRig, groundedMatrices, loadRig, partWorld } from '../rig/runtime.js';
import { WIDTH, HEIGHT, PROJECTION_Y, clamp, desireFraction, enemyHeight, threadEndpoints } from './combat.js';
import { FLIPBOOKS } from './flipbooks.js';

const Y_TOP = 83;
const Y_SCALE = PROJECTION_Y;
const project = y => Y_TOP + Y_SCALE * y;
const palette = ['#d88ca9', '#ae98ce', '#90bdb8', '#d2a086', '#f096a3'];
const gaitParts = ['near_thigh', 'near_shin', 'near_foot', 'far_thigh', 'far_shin', 'far_foot'];
const heroScale = y => .104 * (.94 + .12 * clamp(y / HEIGHT,0,1));
// Thread elements follow the strongest sewing-box set (chaos / despair / frustration).
const ELEMENT = {
  plain: { color: '#f2a9c3', glow: '#ff86b8', core: '#fff0f6' },
  chaos: { color: '#ff5fa2', glow: '#ff2a7f', core: '#ffe1ef' },
  despair: { color: '#4fe0d0', glow: '#19bcd6', core: '#e2fffb' },
  frustration: { color: '#ffd23f', glow: '#ffa600', core: '#fff8d6' },
  dodge: { color: '#7fd0ff', glow: '#3fa9ff', core: '#e8f7ff' },
};
const SKY = '#6cc8ff';
const TINTABLE = new Set(['launch', 'streak', 'shock', 'fx_sigil']);
const DIRECTIONAL = new Set(['launch', 'streak', 'dodge', 'dash', 'fx_snip', 'fx_yank']);
const DODGE = { duration: .17, grace: .12, scale: .178, frames: [.055, .2] };
const tone = name => ELEMENT[name] || ELEMENT.plain;
const easeOutBack = t => 1 + 2.4 * (t-1) ** 3 + 1.4 * (t-1) ** 2;
// Small deterministic noise so jagged threads re-shape on a fixed beat.
const noise = n => { const s = Math.sin(n * 127.1 + 311.7) * 43758.5453; return s - Math.floor(s); };

export function canvasPoint(canvas, clientX, clientY) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: (clientX-rect.left)*canvas.width/rect.width,
    y: ((clientY-rect.top)*canvas.height/rect.height-Y_TOP)/Y_SCALE,
  };
}

function loadImage(name) {
  const img = new Image();
  img.src = `assets/sprites/${name}.png`;
  return img;
}

export function createRenderer(canvas) {
  const ctx = canvas.getContext('2d');
  const art = Object.fromEntries(['stalker','charger','shooter','boss'].map(type => [type,loadImage(`enemy_${type}`)]));
  const floor = loadImage('floor');
  const needle = loadImage('hero_quarter_needle');
  const icons = Object.fromEntries(['shard','gold','knot'].map(type => [type,loadImage(`icon_${type}`)]));
  const vfx = Object.fromEntries(['pull','dash','boss','needle','stitch','streak','launch','dodge','shock',
    'fx_chaos','fx_despair','fx_frustration','fx_snip','fx_sigil','fx_yank'].map(name => {
    const img = new Image();
    img.src = `assets/vfx/${name}.png`;
    return [name,img];
  }));
  // Blender flipbooks: a white-hot core strip plus a grey rim strip that is colorized per thread element.
  const flips = Object.fromEntries(Object.keys(FLIPBOOKS).map(name => {
    const load = layer => { const img = new Image(); img.src = `assets/vfx/flip/${name}_${layer}.png`; return img; };
    return [name, { core: load('core'), rim: load('rim'), ...FLIPBOOKS[name] }];
  }));
  // Codex-baked whole-body dodge frames; silhouettes are pre-filled with one sky blue.
  const dodgeMeta = [
    { foot: [190,466] }, { foot: [292,470] }, { foot: [287.6,456] }, // v3: 공중 프레임은 꼬리 끝이 가장 낮아 부츠 기준으로 수동 지정
  ];
  const dodgeFrames = dodgeMeta.map((_,i) => loadImage(`hero_dodge_${i+1}`));
  const silhouettes = new Map();
  const tinted = new Map();
  const particles = [];
  let particleClock = null;
  const loaded = img => img && (img instanceof HTMLCanvasElement ? img.width : img.complete && img.naturalWidth);

  function texture(name, tint) {
    const img = vfx[name];
    if (!loaded(img)) return null;
    if (!tint || !TINTABLE.has(name)) return img;
    const color = tint.startsWith('#') ? tint : tone(tint).color;
    const key = `${name}|${color}`;
    if (!tinted.has(key)) {
      const layer = document.createElement('canvas');
      layer.width = img.naturalWidth; layer.height = img.naturalHeight;
      const g = layer.getContext('2d');
      g.drawImage(img,0,0);
      g.globalCompositeOperation = 'source-atop';
      g.globalAlpha = .72;
      g.fillStyle = color;
      g.fillRect(0,0,layer.width,layer.height);
      tinted.set(key,layer);
    }
    return tinted.get(key);
  }

  function rimTexture(name, tint) {
    const book = flips[name];
    if (!loaded(book?.rim)) return null;
    const color = !tint ? tone('plain').color : tint.startsWith('#') ? tint : tone(tint).color;
    const key = `flip|${name}|${color}`;
    if (!tinted.has(key)) {
      const layer = document.createElement('canvas');
      layer.width = book.rim.naturalWidth; layer.height = book.rim.naturalHeight;
      const g = layer.getContext('2d');
      g.drawImage(book.rim,0,0);
      g.globalCompositeOperation = 'multiply';
      g.fillStyle = color; g.fillRect(0,0,layer.width,layer.height);
      g.globalCompositeOperation = 'destination-in';
      g.drawImage(book.rim,0,0);
      tinted.set(key,layer);
    }
    return tinted.get(key);
  }

  // Plays a flipbook frame-by-frame (stepped, no extra fades): the shapes themselves move and break up.
  function drawFlip(fx) {
    const book = flips[fx.sheet];
    if (!book || !loaded(book.core)) return;
    const progress = 1 - fx.time / fx.max;
    const frame = Math.min(book.frames - 1, Math.floor(progress * book.frames));
    const s = book.size;
    ctx.translate(fx.x, project(fx.y) - (fx.lift ?? 12));
    ctx.rotate(fx.angle || 0);
    if (fx.squash) ctx.scale(1, Y_SCALE);
    if (fx.mirror) ctx.scale(1, -1);
    ctx.globalCompositeOperation = 'lighter';
    ctx.globalAlpha = (fx.alpha ?? 1) * (reduced ? .5 : 1);
    const rim = rimTexture(fx.sheet, fx.tint);
    const size = fx.size;
    if (rim) ctx.drawImage(rim, frame * s, 0, s, s, -size/2, -size/2, size, size);
    ctx.drawImage(book.core, frame * s, 0, s, s, -size/2, -size/2, size, size);
  }

  function silhouette(index) {
    const img = dodgeFrames[index];
    if (!loaded(img)) return null;
    if (!silhouettes.has(index)) {
      const layer = document.createElement('canvas');
      layer.width = img.naturalWidth; layer.height = img.naturalHeight;
      const g = layer.getContext('2d');
      g.drawImage(img,0,0);
      g.globalCompositeOperation = 'source-in';
      g.fillStyle = SKY;
      g.fillRect(0,0,layer.width,layer.height);
      silhouettes.set(index,layer);
    }
    return silhouettes.get(index);
  }
  const embers = Array.from({ length: 24 }, (_,i) => ({
    x: (i*463.7)%WIDTH, y: (i*241.3)%HEIGHT, drift: 5+i%7, size: .8+(i%4)*.39,
  }));
  const background = new Map();
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let rig;
  const ready = loadRig('assets/rig/hero-quarter.rig.json', {
    data: globalThis.todOfflineRig,
    hitTesting: false,
  }).then(asset => { rig = asset; });

  function createBackground(tint) {
    const layer = document.createElement('canvas');
    layer.width = canvas.width;
    layer.height = canvas.height;
    const paint = layer.getContext('2d');
    paint.fillStyle = '#100c14';
    paint.fillRect(0,0,1000,590);
    paint.save();
    paint.translate(0,Y_TOP);
    paint.scale(1,Y_SCALE);
    if (floor.complete && floor.naturalWidth) {
      const pattern = paint.createPattern(floor,'repeat');
      pattern.setTransform(new DOMMatrix().scale(.47));
      paint.fillStyle = pattern;
    } else paint.fillStyle = '#332633';
    paint.fillRect(0,0,WIDTH,HEIGHT);
    paint.fillStyle = '#21172873';
    paint.fillRect(0,0,WIDTH,HEIGHT);
    paint.restore();

    const dusk = paint.createLinearGradient(0,0,0,590);
    dusk.addColorStop(0,'#150e17');
    dusk.addColorStop(.13,'#110c15');
    dusk.addColorStop(.42,'#23132c22');
    dusk.addColorStop(.79,'#170e1b00');
    dusk.addColorStop(1,'#140d18');
    paint.fillStyle = dusk; paint.fillRect(0,0,1000,590);

    // The floor is inset into a tower wall, with its boundaries visible.
    paint.strokeStyle = '#ad728750';
    paint.lineWidth = 1.5;
    paint.strokeRect(17, Y_TOP+11, WIDTH-34, HEIGHT*Y_SCALE-23);
    paint.strokeStyle = '#60455670';
    paint.strokeRect(24, Y_TOP+16, WIDTH-48, HEIGHT*Y_SCALE-33);
    for (let x = 42; x < WIDTH; x += 115) {
      paint.beginPath();
      paint.moveTo(x-24,88); paint.lineTo(x-24,56);
      paint.quadraticCurveTo(x-24,34,x,21); paint.quadraticCurveTo(x+24,34,x+24,56); paint.lineTo(x+24,88);
      paint.fillStyle = '#19101d'; paint.fill(); paint.stroke();
    }
    for (const [x,y] of [[128,90],[323,83],[672,83],[872,90],[35,480],[962,480]]) {
      const light = paint.createRadialGradient(x,y,2,x,y,105);
      light.addColorStop(0,`${tint}45`);
      light.addColorStop(1,`${tint}00`);
      paint.fillStyle = light; paint.fillRect(x-105,y-105,210,210);
      paint.fillStyle = `${tint}8f`; paint.fillRect(x-2,y-5,4,8);
      paint.fillStyle = '#663d56'; paint.fillRect(x-6,y+4,12,3);
    }
    const shade = paint.createRadialGradient(500,310,140,500,310,550);
    shade.addColorStop(0,'#10091700'); shade.addColorStop(1,'#10091792');
    paint.fillStyle = shade;
    paint.fillRect(0,0,1000,590);
    return layer;
  }
  floor.addEventListener('load',()=>background.clear());

  function line(a,b,tint,width,dashed=false) {
    ctx.beginPath();
    ctx.moveTo(a.x,project(a.y));
    ctx.lineTo(b.x,project(b.y));
    ctx.strokeStyle = tint;
    ctx.lineWidth = width;
    if (dashed) ctx.setLineDash([8,7]);
    ctx.stroke();
    if (dashed) ctx.setLineDash([]);
  }

  function ring(x,y,radius,color,width=1) {
    ctx.beginPath();
    ctx.ellipse(x,project(y),radius,radius*Y_SCALE,0,0,Math.PI*2);
    ctx.strokeStyle = color; ctx.lineWidth = width; ctx.stroke();
  }

  function shadow(x,y,size) {
    ctx.fillStyle = '#100b15a8';
    ctx.beginPath(); ctx.ellipse(x,project(y)+3,size,size*.34,0,0,Math.PI*2); ctx.fill();
  }

  function sampleHero(p, action, t) {
    const pose = computePose(rig,action,action === 'run' ? p.strideTime : Math.max(0,t));
    if (action === 'shoot') {
      const aim = Math.atan2(Math.sin(p.angle), Math.abs(Math.cos(p.angle))) * 180 / Math.PI * (1-clamp((t-.14)/.12,0,1));
      pose.ang.near_arm += aim * .74;
      pose.ang.near_forearm += aim * .26;
      pose.ang.head += aim * .11;
      if (p.motion > .2) {
        const stride = computePose(rig,'run',p.strideTime);
        for (const part of gaitParts) pose.ang[part] += (stride.ang[part] - pose.ang[part]) * p.motion;
      }
    }
    return pose;
  }

  function heroPose(p) {
    const inDash = p.action !== 'die' && (p.dash > 0 || p.dashGrace > 0);
    const action = inDash ? 'dash' : p.action;
    const t = inDash ? p.dash > 0 ? .17 - p.dash : .29 - p.dashGrace : p.actionTime + (action === 'pull' ? .035 : 0);
    const facing = inDash ? p.dashFacing : p.facing;
    const pose = sampleHero(p,action,t);
    const previous = p.previousPose;
    const blend = clamp(t / (action === 'shoot' || action === 'pull' ? .055 : .085),0,1);
    if (previous && previous.action !== action && action !== 'die' && blend < 1) {
      const weight = 1 - blend * blend * (3-2*blend);
      const from = sampleHero(previous,previous.action,previous.actionTime);
      for (const part of rig.data.parts) pose.ang[part.id] += (from.ang[part.id] - pose.ang[part.id]) * weight;
      pose.root = pose.root.map((value,i)=>value + (from.root[i] - value) * weight);
    }
    // Throw recoil: shoved back hard, then springs past rest and settles (no smooth ease — a jolt).
    let rx = 0, ry = 0;
    if (p.recoil && !reduced) {
      const u = 1 - p.recoil.time / p.recoil.max;
      const k = u < .25 ? u / .25 : Math.exp(-(u - .25) * 9) * Math.cos((u - .25) * 16);
      rx = p.recoil.x * 9 * k; ry = p.recoil.y * 9 * k * Y_SCALE;
    }
    const base = baseMatrix(p.x + rx,project(p.y) + ry,heroScale(p.y),facing);
    const grounded = action === 'run' || action === 'pull' || (action === 'shoot' && p.motion > .2);
    const matrices = grounded ? groundedMatrices(rig,pose,base,action === 'pull' ? undefined : 'run') : computeMatrices(rig,pose,base);
    return { pose, base, matrices };
  }

  // Dodge timeline across the burst and the grace window: launch -> airborne -> skid.
  function dodgeState(p) {
    if (p.action === 'die' || !(p.dash > 0 || p.dashGrace > 0)) return null;
    const elapsed = p.dash > 0 ? DODGE.duration - p.dash : DODGE.duration + DODGE.grace - p.dashGrace;
    const index = elapsed < DODGE.frames[0] ? 0 : elapsed < DODGE.frames[1] ? 1 : 2;
    const hop = Math.sin(Math.PI * clamp((elapsed - .02) / .22,0,1)) * 15;
    return { index, hop, elapsed };
  }

  function drawDodgeFrame(x, y, facing, index, hop, image, options = {}) {
    if (!loaded(image)) return false;
    const scale = DODGE.scale * heroScale(y) / .104 * (options.scale || 1);
    const [fx, fy] = dodgeMeta[index].foot;
    ctx.save();
    ctx.translate(x, project(y) - hop);
    ctx.scale(scale * facing, scale);
    if (options.alpha != null) ctx.globalAlpha = options.alpha;
    if (options.filter) ctx.filter = options.filter;
    if (options.composite) ctx.globalCompositeOperation = options.composite;
    ctx.drawImage(image, -fx, -fy);
    ctx.restore();
    return true;
  }

  function drawHero(p, alpha=1, frame) {
    if (!rig) return;
    const dodge = dodgeState(p);
    if (alpha === 1) shadow(p.x,p.y,25*heroScale(p.y)/.104*(dodge ? 1 - dodge.hop/40 : 1));
    if (dodge) {
      const image = dodgeFrames[dodge.index];
      const glow = silhouette(dodge.index);
      if (glow && !reduced) drawDodgeFrame(p.x,p.y,p.dashFacing,dodge.index,dodge.hop,glow,
        { alpha: .55, scale: 1.06, filter: 'blur(5px)', composite: 'lighter' });
      if (drawDodgeFrame(p.x,p.y,p.dashFacing,dodge.index,dodge.hop,image,{ alpha })) return;
    }
    // Hit: red tint for the first half of the hurt window, then a blink while i-frames last.
    const glow = p.hurt > .31 ? 'sepia(1) saturate(6) hue-rotate(-50deg) brightness(1.05)' : '';
    if (p.hurt > 0 && p.hurt <= .31 && alpha === 1 && Math.floor(p.hurt * 22) % 2) alpha = .45;
    const { pose, base, matrices } = frame || heroPose(p);
    // 통그림 섞기: 리그로는 안 나오는 순간(투척 릴리스·당기기 정점·피격)은 전신 통그림 한 장으로 바꿔 끼운다.
    const cel = celFor(p);
    if (cel && drawCel(p, cel, alpha, glow)) return;
    drawRig(ctx,rig,pose,base,{ alpha, filter: glow, matrices });
  }

  const celImages = new Map();
  function celFor(p) {
    const list = rig?.data.cels?.clips?.[p.action];
    if (!list || reduced) return null;
    const t = p.actionTime + (p.action === 'pull' ? .035 : 0);
    return list.find(c => t >= c.from && t < c.to) || null;
  }
  function drawCel(p, cel, alpha, filter) {
    if (!celImages.has(cel.img)) celImages.set(cel.img, loadImage(cel.img.replace(/\.png$/, '')));
    const im = celImages.get(cel.img);
    if (!loaded(im)) return false;
    const scale = heroScale(p.y) * rig.data.cels.scale;
    ctx.save();
    ctx.translate(p.x, project(p.y));
    ctx.scale(scale * p.facing, scale);
    ctx.globalAlpha = alpha;
    if (filter) ctx.filter = filter;
    ctx.drawImage(im, -cel.anchor[0], -cel.anchor[1]);
    ctx.restore();
    return true;
  }

  function handPosition(p, frame) {
    if (!frame) return p;
    const [x,y] = partWorld(frame.matrices,'needle');
    return { x, y: (y-Y_TOP)/Y_SCALE };
  }

  function drawNeedle(x,y,angle,length,color,glow) {
    ctx.save();
    ctx.translate(x,y);
    ctx.rotate(angle);
    if (glow && !reduced) { ctx.shadowColor = glow; ctx.shadowBlur = 9; }
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.35;
    ctx.beginPath(); ctx.moveTo(-length*.75,0); ctx.lineTo(length*.45,0); ctx.stroke();
    if (loaded(needle)) ctx.drawImage(needle,-length*.56,-2.6,length,5.2);
    ctx.restore();
  }

  function particle(x, y, vx, vy, life, color, size, kind, gravity = 0) {
    if (reduced || particles.length > 420) return;
    particles.push({ x, y, vx, vy, life, max: life, color, size, kind, gravity });
  }

  // Screen-space polyline for one strand, shaped by its element.
  function strandPoints(a, b, element, seed, time, tension = 0) {
    const dx = b.x - a.x, dy = b.y - a.y;
    const length = Math.hypot(dx, dy) || 1;
    const nx = -dy / length, ny = dx / length;
    const segments = Math.max(2, Math.ceil(length / (element === 'frustration' ? 13 : 16)));
    const beat = Math.floor(time * 22);
    const points = [];
    for (let i = 0; i <= segments; i++) {
      const f = i / segments;
      const envelope = Math.sin(Math.PI * f);
      let offset = 0;
      if (element === 'despair') offset = Math.sin(f * length / 11 - time * 7 + seed) * 2.6 * envelope;
      else if (element === 'frustration') offset = (noise(beat * 31 + i + seed) - .5) * 9 * envelope;
      else if (element === 'chaos') offset = (noise(beat * 17 + i * 3 + seed) - .5) * 2.4 * envelope;
      if (tension) offset += Math.sin(f * Math.PI * 3 + time * 90) * tension * envelope;
      points.push({ x: a.x + dx * f + nx * offset, y: a.y + dy * f + ny * offset });
    }
    return points;
  }

  function strokePoints(points, color, width) {
    ctx.beginPath();
    ctx.moveTo(points[0].x, points[0].y);
    for (let i = 1; i < points.length; i++) ctx.lineTo(points[i].x, points[i].y);
    ctx.strokeStyle = color; ctx.lineWidth = width; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    ctx.stroke();
  }

  function drawStrand(a, b, element, seed, time, { width = 1.65, alpha = 1, tension = 0, flight = false } = {}) {
    const t = tone(element);
    const points = strandPoints(a, b, element, seed, time, tension);
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.globalCompositeOperation = 'lighter';
    strokePoints(points, `${t.glow}${element === 'plain' ? '33' : '44'}`, width * 3.4);
    ctx.globalCompositeOperation = 'source-over';
    strokePoints(points, `${t.color}e6`, flight ? width * .8 : width);
    strokePoints(points, `${t.core}aa`, width * .38);
    if (element === 'chaos') {
      // Thorn barbs along the strand.
      ctx.fillStyle = t.color;
      const length = Math.hypot(b.x-a.x, b.y-a.y);
      const ux = (b.x-a.x) / (length||1), uy = (b.y-a.y) / (length||1);
      for (let d = 14, k = 0; d < length - 8; d += 21, k++) {
        const side = k % 2 ? 1 : -1;
        const x = a.x + ux*d, y = a.y + uy*d;
        ctx.beginPath();
        ctx.moveTo(x - ux*3, y - uy*3);
        ctx.lineTo(x + ux*3, y + uy*3);
        ctx.lineTo(x - ux*4 - uy*5*side, y - uy*4 + ux*5*side);
        ctx.fill();
      }
    }
    ctx.restore();
    return points;
  }

  function emitAlong(points, element, rate, dt) {
    if (!dt || reduced) return;
    const t = tone(element);
    const count = rate * dt * points.length;
    for (let n = 0; n < Math.floor(count) + (Math.random() < count % 1 ? 1 : 0); n++) {
      const p = points[Math.floor(Math.random() * points.length)];
      if (element === 'chaos') particle(p.x, p.y, (Math.random()-.5)*18, -24-Math.random()*30, .5+Math.random()*.3, t.color, 1.8, 'ember', -10);
      else if (element === 'despair') particle(p.x, p.y, (Math.random()-.5)*6, 8, .7, t.core, 1.9, 'drop', 170);
      else if (element === 'frustration') particle(p.x, p.y, (Math.random()-.5)*150, (Math.random()-.5)*150, .14, t.core, 1.4, 'spark');
      else particle(p.x, p.y, 0, -8, .4, t.core, 1.3, 'glint');
    }
  }

  function drawThread(r,thread,hand,dt) {
    const { start, end } = threadEndpoints(r.player,thread);
    const from = thread.anchor ? start : hand;
    const element = thread.element || 'plain';
    const t = tone(element);
    const a = { x: from.x, y: project(from.y) };
    const b = { x: end.x, y: project(end.y) - (thread.target ? 23 : 4) };
    const seed = thread.birth * 13;
    const flight = thread.state === 'flight';
    // A freshly stuck strand twangs like a plucked string, then settles taut.
    const twang = thread.state === 'stuck' && !reduced ? 7 * Math.exp(-(r.time - (thread.landed ?? 0)) * 9) : 0;
    const points = drawStrand(a, b, element, seed, r.time, {
      width: thread.anchor ? 1.45 : 1.7, alpha: thread.anchor ? .82 : 1, flight, tension: twang,
    });
    emitAlong(points, element, element === 'frustration' ? .5 : element === 'plain' ? .06 : .16, dt);
    const bead = (r.time * 2.65 + thread.birth) % 1;
    const p = points[Math.floor(bead * (points.length-1))];
    ctx.fillStyle = `${t.core}d0`;
    ctx.fillRect(p.x-1.2,p.y-1.2,2.6,2.6);
    if (thread.state === 'ground') {
      const age = r.time - (thread.landed ?? thread.birth);
      // The needle quivers after it bites into the floor.
      const quiver = Math.sin(age * 46) * Math.exp(-age * 7) * .42;
      ring(end.x,end.y,7,`${t.color}bc`,1.15);
      if (element === 'despair') {
        const wave = (r.time * .8 + thread.birth) % 1;
        ring(end.x,end.y,7 + wave * 16,`${t.color}${Math.round((1-wave)*120).toString(16).padStart(2,'0')}`,1);
      }
      drawNeedle(end.x,project(end.y)-8,1.07 + quiver,24,t.color,t.glow);
    }
    if (flight) {
      const angle = Math.atan2(thread.vy*Y_SCALE,thread.vx);
      const speed = clamp((thread.speed || 1400) / 2700, 0, 1);
      const head = { x: end.x, y: project(end.y)-3 };
      const streak = texture('streak', element);
      if (streak) {
        const length = 46 + speed * 104;
        ctx.save();
        ctx.translate(head.x, head.y);
        ctx.rotate(angle);
        ctx.globalCompositeOperation = 'lighter';
        ctx.globalAlpha = .45 + speed * .55;
        ctx.drawImage(streak, -length * .96, -9 - speed * 5, length, 18 + speed * 10);
        ctx.restore();
      }
      if (!reduced && dt) for (let n = 0; n < 2; n++) {
        particle(head.x - Math.cos(angle)*Math.random()*18, head.y - Math.sin(angle)*Math.random()*18,
          -Math.cos(angle)*40 + (Math.random()-.5)*40, -Math.sin(angle)*40 + (Math.random()-.5)*40, .28, t.core, 1.6, 'glint');
      }
      // The needle stretches with speed, like a motion smear.
      drawNeedle(head.x,head.y,angle,29 + speed*14,t.color,t.glow);
    }
  }

  function updateParticles(dt) {
    for (let i = particles.length - 1; i >= 0; i--) {
      const q = particles[i];
      q.life -= dt;
      if (q.life <= 0) { particles.splice(i,1); continue; }
      q.vy += q.gravity * dt;
      q.x += q.vx * dt; q.y += q.vy * dt;
    }
  }

  function drawParticles() {
    if (!particles.length) return;
    ctx.save();
    ctx.globalCompositeOperation = 'lighter';
    for (const q of particles) {
      const fade = q.life / q.max;
      ctx.globalAlpha = fade;
      ctx.fillStyle = q.color;
      if (q.kind === 'spark') {
        ctx.strokeStyle = q.color; ctx.lineWidth = 1.2;
        ctx.beginPath(); ctx.moveTo(q.x, q.y); ctx.lineTo(q.x - q.vx*.03, q.y - q.vy*.03); ctx.stroke();
      } else if (q.kind === 'drop') {
        ctx.beginPath(); ctx.ellipse(q.x, q.y, q.size*.7, q.size*1.3, 0, 0, Math.PI*2); ctx.fill();
      } else if (q.kind === 'glint') {
        const s = q.size * (1 + fade);
        ctx.fillRect(q.x - s, q.y - .5, s*2, 1); ctx.fillRect(q.x - .5, q.y - s, 1, s*2);
      } else {
        ctx.beginPath(); ctx.arc(q.x, q.y, q.size * fade + .4, 0, Math.PI*2); ctx.fill();
      }
    }
    ctx.restore();
  }

  // Draws a sprite as two halves split by a line through the body centre.
  function drawHalves(im, w, h, angle, gap, side, glow) {
    const dx = Math.cos(angle), dy = Math.sin(angle), nx = -dy, ny = dx, L = Math.max(w, h) * 2, cy = -h / 2;
    for (const s of [1, -1]) {
      ctx.save();
      ctx.beginPath();
      ctx.moveTo(-dx*L, cy - dy*L); ctx.lineTo(dx*L, cy + dy*L);
      ctx.lineTo(dx*L + nx*s*L, cy + dy*L + ny*s*L); ctx.lineTo(-dx*L + nx*s*L, cy - dy*L + ny*s*L);
      ctx.closePath(); ctx.clip();
      // One half is knocked further than the other, so the cut reads as a slice rather than a symmetric pop.
      const push = gap * (s === side ? 1.25 : .6);
      ctx.translate(nx*s*push + dx*s*push*.35, ny*s*push + dy*s*push*.35);
      ctx.drawImage(im,-w/2,-h,w,h);
      ctx.restore();
    }
    if (glow > .05) {
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.globalAlpha = Math.min(1, glow * 1.4);
      ctx.strokeStyle = '#fff6fb'; ctx.lineWidth = 1 + 2.5 * glow;
      ctx.beginPath(); ctx.moveTo(-dx*w*.8, cy - dy*w*.8); ctx.lineTo(dx*w*.8, cy + dy*w*.8); ctx.stroke();
      ctx.restore();
    }
  }

  let t_now = 0;
  function drawEnemy(e, t, attached) {
    t_now = t;
    const y = project(e.y);
    const type = e.type;
    const im = art[type];
    const boss = type === 'boss';
    const h = enemyHeight(e);
    const w = h * (im.naturalWidth/im.naturalHeight || .74);
    const bob = Math.sin(Math.floor(t*12)/12*8 + e.x) * (e.action === 'move' ? 2 : .5);
    shadow(e.x,e.y,e.radius);
    if (e.elite || boss) ring(e.x,e.y,e.radius+5,e.elite ? '#e8bb70ac' : '#ce6d93b5',2);
    ctx.save();
    ctx.translate(e.x,y+bob);
    if (e.jolt) {
      const k = e.jolt.time / .12;
      ctx.translate(e.jolt.x * 5 * k, e.jolt.y * Y_SCALE * 5 * k);
    }
    if (e.stretch && !reduced) {
      // Squash and stretch along the pull direction around the body centre.
      const k = e.stretch.time / .24;
      const pulse = Math.sin(Math.PI * Math.min(1, (1 - k) * 1.6 + .25)) * k;
      const angle = Math.atan2(e.stretch.y, e.stretch.x);
      ctx.translate(0,-h/2);
      ctx.rotate(angle);
      ctx.scale(1 + .34 * pulse, 1 - .2 * pulse);
      ctx.rotate(-angle);
      ctx.translate(0,h/2);
    }
    ctx.scale(e.facing, e.action === 'windupMelee' ? .84 : 1);
    if (e.flash > 0) ctx.filter = e.flash > .15 ? 'brightness(2.6) saturate(.4)' : 'brightness(1.9)';
    if (!(im.complete && im.naturalWidth)) { ctx.fillStyle = '#9c577a'; ctx.fillRect(-w/2,-h,w,h); }
    else if (e.cut && !reduced) {
      // Sliced along the strand: the halves are knocked apart at the impact frame (held through hitstop) and snap back.
      const k = (e.cut.time / e.cut.max) ** 2;
      const angle = e.facing > 0 ? e.cut.angle : Math.PI - e.cut.angle;
      drawHalves(im, w, h, angle, 7 * k, e.cut.side, k);
    } else ctx.drawImage(im,-w/2,-h,w,h);
    ctx.restore();
    if (!boss && (e.hp < e.maxHp || attached)) {
      ctx.fillStyle = '#211626'; ctx.fillRect(e.x-16,y-h-7,32,3);
      ctx.fillStyle = '#d891a7'; ctx.fillRect(e.x-16,y-h-7,32*e.hp/e.maxHp,3);
    }
    if (attached?.length) {
      attached.slice(0,4).forEach((thread,i) => {
        const offset = (i-(Math.min(attached.length,4)-1)/2)*7;
        const t = tone(thread.element);
        // Bite: the needle drives past the surface and springs back like it hit something dense.
        const age = Math.max(0, t_now - (thread.landed ?? 0));
        const bite = thread.bite && !reduced ? 8 * Math.exp(-age * 13) * Math.cos(age * 34) : 0;
        const bx = (thread.bite?.x || 0) * bite, by = (thread.bite?.y || 0) * bite;
        drawNeedle(e.x+offset+bx,y-20-i%2*5+by,1.02+offset*.04,16,t.color,t.glow);
      });
      ctx.font = 'bold 12px Georgia';
      ctx.textAlign = 'center';
      ctx.fillStyle = '#ffd2dd';
      ctx.fillText(`×${attached.length}`,e.x,y-h-11);
    }
  }

  function drawEffects(r) {
    for (const fx of r.effects) {
      if (fx.kind === 'afterimage') continue;
      const fade = fx.time/fx.max;
      const x = fx.x; const y = project(fx.y);
      ctx.save();
      ctx.globalAlpha = fade;
      if (fx.kind === 'flip') {
        drawFlip(fx);
      } else if (fx.kind === 'cut') {
        drawCut(fx, fade);
      } else if (fx.kind === 'sliced') {
        drawSliced(fx, fade);
      } else if (fx.kind === 'sprite') {
        const image = texture(fx.sprite, fx.tint);
        if (image) {
          const progress = 1-fade;
          // Burst sprites pop out fast with a slight overshoot, the rest grow gently.
          const size = fx.burst ? fx.size*(.35+easeOutBack(Math.min(1,progress*2.6))*.75+progress*.15) : fx.size*(.44+Math.sqrt(progress)*.82);
          ctx.translate(x,y-(fx.lift ?? (fx.sprite === 'dash' ? 23 : 13)));
          const spin = reduced || fx.angle != null || DIRECTIONAL.has(fx.sprite) ? 0 : progress*.31;
          ctx.rotate((fx.angle || 0) + spin);
          if (fx.squash) ctx.scale(1, .62);
          ctx.globalCompositeOperation = 'lighter';
          ctx.globalAlpha = Math.min(1,fade*(fx.burst ? 1.7 : 1.26)) * (reduced ? .52 : 1) * (fx.alpha ?? 1);
          ctx.drawImage(image,-size/2,-size/2,size,size);
          if (!reduced) {
            ctx.globalAlpha *= .25;
            ctx.filter = 'blur(7px)';
            ctx.drawImage(image,-size*.52,-size*.52,size*1.04,size*1.04);
          }
        }
      } else if (fx.kind === 'number') {
        const age = 1-fade;
        const pop = fx.pop || 1;
        const scale = (1 + Math.min(pop,6)*.07) * (age < .16 ? .6 + easeOutBack(age/.16)*.4 + (1-age/.16)*.35 : 1);
        ctx.fillStyle = fx.tint; ctx.strokeStyle = '#1c0e21'; ctx.lineWidth = 4;
        ctx.textAlign = 'center'; ctx.font = 'bold 24px Georgia';
        const top = y-(fx.lift ?? 36)-(1-fade)*34;
        ctx.translate(x,top); ctx.scale(scale,scale);
        ctx.strokeText(fx.text,0,0); ctx.fillText(fx.text,0,0);
      } else if (fx.kind === 'combo') {
        const age = 1-fade;
        const scale = age < .18 ? .4 + easeOutBack(age/.18)*.75 : 1.15 - (age-.18)*.2;
        ctx.translate(x, y-118-age*16);
        ctx.scale(scale, scale);
        ctx.rotate(-.06);
        ctx.textAlign = 'center'; ctx.font = 'italic bold 30px Georgia';
        ctx.lineWidth = 6; ctx.strokeStyle = '#1c0e21';
        ctx.strokeText(fx.text,0,0);
        ctx.fillStyle = fx.tint; ctx.fillText(fx.text,0,0);
      } else if (fx.kind === 'dust') {
        const age = 1-fade;
        ctx.fillStyle = '#c9b7c6';
        for (let i = 0; i < 6; i++) {
          const spread = (i - 2.5) * .32 + fx.angle;
          const d = (10 + i * 4) * Math.sqrt(age) * 2.2;
          ctx.globalAlpha = fade * .45;
          ctx.beginPath();
          ctx.ellipse(x + Math.cos(spread)*d, y + Math.sin(spread)*d*Y_SCALE - 3 - age*6, 4 + age*7, (4 + age*7)*.55, 0, 0, Math.PI*2);
          ctx.fill();
        }
      } else if (fx.kind === 'pull' && fx.element) {
        drawPullSnap(fx, fade);
      } else if (fx.kind === 'pull' || fx.kind === 'transfer') {
        const a = { x, y: fx.y };
        const b = { x: fx.toX, y: fx.toY };
        const glow = fx.kind === 'pull' ? '#80f1df' : '#ffc879';
        const t = 1-fade;
        const lead = { x: b.x + (a.x-b.x)*t, y: b.y + (a.y-b.y)*t };
        ctx.shadowBlur = 17*fade; ctx.shadowColor = glow;
        line(a,lead,glow,2.4+4.7*fade);
        line(a,lead,fx.tint || '#fff1cb',1.7+2.4*fade);
        ring(b.x,b.y,12+t*30,glow,1.5);
        ctx.shadowBlur = 0;
      } else {
        const radius = fx.kind === 'slash' ? 29 : 14;
        ring(x,fx.y,5+(1-fade)*radius,fx.tint,2);
        ctx.fillStyle = fx.tint;
        for (let i=0; i<5; i++) {
          const angle = i * Math.PI*2/5 + x;
          ctx.fillRect(x+Math.cos(angle)*(1-fade)*radius-1,y+Math.sin(angle)*(1-fade)*radius-2,3,4);
        }
      }
      ctx.restore();
    }
  }

  // The strand turns into a razor: a tapered blade of light from the anchor through the target and beyond it,
  // which then splits into two hairlines drifting apart.
  function drawCut(fx, fade) {
    const t = 1 - fade;
    const color = tone(fx.element).color;
    const a = { x: fx.x, y: project(fx.y) - 8 };
    const b0 = { x: fx.toX, y: project(fx.toY) - 22 };
    const len = Math.hypot(b0.x - a.x, b0.y - a.y) || 1;
    const ux = (b0.x - a.x) / len, uy = (b0.y - a.y) / len, nx = -uy, ny = ux;
    const reach = len + 70 * Math.min(1, t / .12 + .4);
    const b = { x: a.x + ux * reach, y: a.y + uy * reach };
    ctx.globalAlpha = 1;
    ctx.globalCompositeOperation = 'lighter';
    const blade = (width, fill) => {
      const m = { x: a.x + ux * reach * .72, y: a.y + uy * reach * .72 };
      ctx.beginPath(); ctx.moveTo(a.x, a.y);
      ctx.lineTo(m.x + nx * width, m.y + ny * width); ctx.lineTo(b.x, b.y); ctx.lineTo(m.x - nx * width, m.y - ny * width);
      ctx.closePath(); ctx.fillStyle = fill; ctx.fill();
    };
    if (t < .4) {
      const k = 1 - t / .4;
      blade(9 * k, `${color}66`);
      blade(3.2 * k + .6, '#ffffff');
    }
    if (t > .12) {
      const k = (t - .12) / .88, off = 2 + k * 11;
      ctx.globalAlpha = (1 - k) ** 1.5;
      ctx.strokeStyle = color; ctx.lineWidth = 1.3;
      for (const s of [1, -1]) {
        ctx.beginPath();
        ctx.moveTo(a.x + ux * reach * .25 + nx * off * s, a.y + uy * reach * .25 + ny * off * s);
        ctx.lineTo(b.x + nx * off * s, b.y + ny * off * s); ctx.stroke();
      }
    }
  }

  // A killed enemy falls apart in two pieces along the cut.
  function drawSliced(fx, fade) {
    const im = art[fx.type];
    if (!loaded(im)) return;
    const t = 1 - fade;
    const h = enemyHeight(fx), w = h * (im.naturalWidth / im.naturalHeight || .74);
    const angle = fx.facing > 0 ? fx.angle : Math.PI - fx.angle;
    const dx = Math.cos(angle), dy = Math.sin(angle), nx = -dy, ny = dx, L = Math.max(w, h) * 2, cy = -h / 2;
    ctx.translate(fx.x, project(fx.y));
    ctx.scale(fx.facing, 1);
    for (const s of [1, -1]) {
      ctx.save();
      const spread = 8 + 26 * (1 - (1 - t) ** 2.5) * (s === fx.side ? 1.3 : .8);
      const drop = 30 * t * t;
      ctx.translate(nx * s * spread + dx * s * spread * .5, ny * s * spread + drop);
      ctx.rotate(s * t * .35);
      ctx.globalAlpha = fade < .35 ? fade / .35 : 1;
      ctx.filter = t < .12 ? 'brightness(2.4) saturate(.3)' : 'brightness(.85) saturate(.7)';
      ctx.beginPath();
      ctx.moveTo(-dx*L, cy - dy*L); ctx.lineTo(dx*L, cy + dy*L);
      ctx.lineTo(dx*L + nx*s*L, cy + dy*L + ny*s*L); ctx.lineTo(-dx*L + nx*s*L, cy - dy*L + ny*s*L);
      ctx.closePath(); ctx.clip();
      ctx.drawImage(im, -w/2, -h, w, h);
      ctx.restore();
    }
  }

  // A taut strand trembles during hitstop, then whips back to its anchor with accelerating speed.
  function drawPullSnap(fx, fade) {
    const t = 1 - fade;
    const a = { x: fx.x, y: project(fx.y) - 6 };
    const b = { x: fx.toX, y: project(fx.toY) - 18 };
    const tone_ = tone(fx.rage ? 'chaos' : fx.element);
    const snap = clamp((t - .16) / .84, 0, 1);
    const pulled = snap ** 2.4;
    const lead = { x: b.x + (a.x - b.x) * pulled, y: b.y + (a.y - b.y) * pulled };
    const tension = reduced ? 0 : (1 - snap) * 3.2;
    ctx.globalAlpha = 1;
    ctx.shadowBlur = reduced ? 0 : 14 * fade; ctx.shadowColor = tone_.glow;
    drawStrand(a, lead, fx.element, fx.seed, fx.time * 3 + fx.seed, { width: 2.6 + 2.8 * fade, alpha: Math.min(1, fade * 1.6), tension });
    ctx.shadowBlur = 0;
    if (snap > 0 && snap < 1) {
      ctx.globalCompositeOperation = 'lighter';
      ctx.fillStyle = tone_.core;
      ctx.beginPath(); ctx.arc(lead.x, lead.y, 3 + 4 * (1 - snap), 0, Math.PI*2); ctx.fill();
      if (!reduced && Math.random() < .6) particle(lead.x, lead.y, (Math.random()-.5)*120, (Math.random()-.5)*120, .18, tone_.color, 1.6, 'spark');
    }
    if (t > .9) ring(fx.x, fx.y, 10 + t * 26, `${tone_.color}88`, 1.5);
  }

  function drawAfterimages(r) {
    if (reduced) return;
    for (const fx of r.effects) {
      if (fx.kind !== 'afterimage') continue;
      const fade = fx.time/fx.max;
      const elapsed = fx.progress * DODGE.duration;
      const index = elapsed < DODGE.frames[0] ? 0 : 1;
      const hop = Math.sin(Math.PI * clamp((elapsed - .02) / .22,0,1)) * 15;
      const image = silhouette(index);
      if (!image) continue;
      // One sky-blue silhouette for every ghost, fading and drifting up slightly.
      drawDodgeFrame(fx.x, fx.y, fx.facing, index, hop + (1-fade)*4, image, { alpha: fade * .5 });
      drawDodgeFrame(fx.x, fx.y, fx.facing, index, hop + (1-fade)*4, image, { alpha: fade * .3, composite: 'lighter', filter: 'blur(3px)' });
    }
  }

  function render(r, aim, clock, active) {
    const tint = palette[(r.floor-1)%palette.length];
    if (!background.has(tint)) background.set(tint,createBackground(tint));
    ctx.fillStyle = '#120e16'; ctx.fillRect(0,0,1000,590);
    ctx.save();
    const shake = reduced ? 0 : r.shake*14;
    // Pull kick: the view jumps toward the hero and settles back.
    const kick = r.kick && !reduced ? (r.kick.time / r.kick.max) ** 2 : 0;
    ctx.translate(Math.sin(clock*67)*shake + (r.kick?.x || 0)*kick,Math.cos(clock*49)*shake*.6 + (r.kick?.y || 0)*kick);
    ctx.drawImage(background.get(tint),0,0);
    if (particleClock === null || r.time < particleClock || r.time - particleClock > .5) particleClock = r.time;
    const dt = Math.min(.05, r.time - particleClock);
    particleClock = r.time;
    updateParticles(dt);
    if (!reduced) {
      ctx.fillStyle = tint;
      for (const mote of embers) {
        ctx.globalAlpha = .12 + (Math.sin(clock*.93 + mote.x)*.5+.5)*.25;
        const x = mote.x+Math.sin(clock*.52+mote.x)*16;
        const y = (mote.y + HEIGHT - (clock*mote.drift)%HEIGHT)%HEIGHT;
        ctx.fillRect(x,project(y),mote.size,mote.size);
      }
      ctx.globalAlpha = 1;
    }
    for (const d of r.drops) {
      const y = project(d.y);
      const icon = icons[d.shard ? 'shard' : 'gold'];
      if (icon.complete && icon.naturalWidth) ctx.drawImage(icon,d.x-9,y-12,18,18);
    }
    for (const enemy of r.enemies) {
      if (enemy.action === 'windupDash') {
        const end = { x: enemy.x + enemy.aim.x*340, y: enemy.y + enemy.aim.y*340 };
        line(enemy,end,'#d152695d',20);
        line(enemy,end,'#f5a6a9ca',2,true);
      }
      if (enemy.action === 'windupMelee' || enemy.action === 'windupShot') {
        ring(enemy.x,enemy.y,enemy.action === 'windupMelee' ? 66 : enemy.radius+10,'#ed8aa6aa',2);
      }
    }
    for (const k of r.knots) {
      const im = icons.knot;
      ring(k.x,k.y,15,'#de8baa85',1.4);
      if (im.complete && im.naturalWidth) ctx.drawImage(im,k.x-11,project(k.y)-16,22,22);
    }
    for (const trail of r.trails) line(trail.a,trail.b,`${tone(r.element).color}bb`,1.8,true);
    const player = r.outcome === 'dead' ? { ...r.player, actionTime: Math.min(.55, clock - (r.deathClock || clock)) } : r.player;
    const frame = rig && heroPose(player);
    const hand = handPosition(player,frame);
    const pins = new Map();
    for (const thread of r.threads) {
      drawThread(r,thread,hand,dt);
      if (!thread.target) continue;
      if (!pins.has(thread.target)) pins.set(thread.target,[]);
      pins.get(thread.target).push(thread);
    }
    if (r.echo) for (const thread of r.echo.threads) {
      if (thread.target && thread.target.hp <= 0) continue;
      const { start, end } = threadEndpoints(r.player,thread);
      line(thread.anchor ? start : hand,end,'#dbb99b77',1,true);
    }
    for (const b of r.bullets) {
      ctx.beginPath(); ctx.ellipse(b.x,project(b.y),6,5,0,0,Math.PI*2);
      ctx.fillStyle = b.spent ? '#c88a956e' : '#f08d9f'; ctx.shadowColor = '#f07c9d'; ctx.shadowBlur = 12;
      ctx.fill(); ctx.shadowBlur = 0;
    }
    drawAfterimages(r);
    const actors = [...r.enemies,player].sort((a,b) => a.y-b.y);
    for (const actor of actors) {
      if (actor === player) drawHero(player,r.outcome === 'absorbed' ? .55 : 1,frame);
      else drawEnemy(actor,r.time,pins.get(actor));
    }
    drawEffects(r);
    drawParticles();
    if (active && aim && aim.x >= 0 && aim.x <= WIDTH && aim.y >= 0 && aim.y <= HEIGHT) {
      ring(aim.x,aim.y,10,'#f3c5d1b8',1);
      ctx.fillStyle = '#f3c5d1'; ctx.fillRect(aim.x-1,project(aim.y)-1,2,2);
    }
    ctx.restore();
    if (r.flash > 0) {
      ctx.save();
      ctx.globalCompositeOperation = 'lighter';
      ctx.fillStyle = r.flashTint || '#ffe6f1';
      ctx.globalAlpha = Math.min(.07, r.flash * .5) * (reduced ? .4 : 1);
      ctx.fillRect(0,0,1000,590);
      ctx.restore();
    }
    if (r.snapFlash > 0) {
      // Impact frame: a hard white frame at the moment of the cut, falling off within the hitstop.
      ctx.save();
      // Kept faint: a full-strength white frame on every pull strains the eyes.
      ctx.globalAlpha = (reduced ? .05 : .12) * (r.snapFlash / .11) ** 1.6;
      ctx.fillStyle = '#fff4f8';
      ctx.fillRect(0,0,1000,590);
      ctx.restore();
    }
    if (r.hurtFlash > 0) {
      // Hit: a soft red tint with darker red edges, fading out over HURT_FLASH (.45s).
      const k = (r.hurtFlash / .45) ** 1.5 * (reduced ? .5 : 1);
      ctx.save();
      ctx.fillStyle = `rgba(200,30,40,${.1*k})`;
      ctx.fillRect(0,0,1000,590);
      const edge = ctx.createRadialGradient(500,295,180,500,295,600);
      edge.addColorStop(0,'rgba(170,20,30,0)');
      edge.addColorStop(1,`rgba(170,20,30,${.55*k})`);
      ctx.fillStyle = edge; ctx.fillRect(0,0,1000,590);
      ctx.restore();
    }
    if (desireFraction(r) >= .5) {
      const border = ctx.createRadialGradient(500,295,160,500,295,570);
      const strength = clamp((desireFraction(r)-.3)*.81,0,.65) + (reduced ? 0 : Math.sin(clock*3)*.055);
      border.addColorStop(0,'#5d183400');
      border.addColorStop(1,`rgba(123,31,80,${strength})`);
      ctx.fillStyle = border; ctx.fillRect(0,0,1000,590);
    }
  }
  return { ready, render };
}
