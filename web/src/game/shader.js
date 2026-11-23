// 화면 후처리 셰이더 — 기획서 비주얼 가이드(유령 신부 + 굶지마)를 아트 대신 코드로 입힌다.
// 플랫하게 그린 그림 위에:
//  · 유령 신부: 낮은 채도, 차가운 청회색 그림자 + 바랜 분홍/상아빛 하이라이트(split tone), 떠 있는 검정
//  · 굶지마: 종이 섬유 결, 거친 잉크 외곽선 덧칠, 손그림 '보일링'(8fps로 선이 떨림), 무거운 비네트
//  · 공통: 12fps 필름 그레인, 촛불 같은 미세한 밝기 깜빡임
// 원본 2D 캔버스는 그대로 두고(입력 좌표 유지), 그 위에 WebGL 캔버스를 겹쳐 결과만 보여준다.

const VERT = `
attribute vec2 p;
varying vec2 uv;
void main() { uv = p * .5 + .5; gl_Position = vec4(p, 0., 1.); }`;

const FRAG = `
precision mediump float;
varying vec2 uv;
uniform sampler2D tex;
uniform vec2 res;
uniform float time, beat, motion, desire;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
  vec2 i = floor(p), f = fract(p);
  vec2 u = f * f * (3. - 2. * f);
  return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0., a = .5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= .5; } return v; }
float lum(vec3 c) { return dot(c, vec3(.299, .587, .114)); }

void main() {
  vec2 st = vec2(uv.x, 1. - uv.y);
  // 손그림 보일링: 화면 전체가 8fps 박자로 1px 안팎 미세하게 떨린다.
  vec2 wob = (vec2(noise(st * 9. + beat * 3.1), noise(st * 9. - beat * 2.7)) - .5) * 2.2 / res * motion;
  vec2 q = st + wob;
  vec3 c = texture2D(tex, q).rgb;

  // 잉크 외곽: 밝기 Sobel → 경계를 어둡게 덧칠, 종이결로 끊어져 거친 붓 느낌.
  vec2 px = 1. / res;
  float tl = lum(texture2D(tex, q + px * vec2(-1, -1)).rgb), tc = lum(texture2D(tex, q + px * vec2(0, -1)).rgb);
  float tr = lum(texture2D(tex, q + px * vec2(1, -1)).rgb), ml = lum(texture2D(tex, q + px * vec2(-1, 0)).rgb);
  float mr = lum(texture2D(tex, q + px * vec2(1, 0)).rgb), bl = lum(texture2D(tex, q + px * vec2(-1, 1)).rgb);
  float bc = lum(texture2D(tex, q + px * vec2(0, 1)).rgb), br = lum(texture2D(tex, q + px * vec2(1, 1)).rgb);
  float gx = -tl - 2. * ml - bl + tr + 2. * mr + br;
  float gy = -tl - 2. * tc - tr + bl + 2. * bc + br;
  float edge = smoothstep(.12, .42, length(vec2(gx, gy)));
  float scratch = smoothstep(.25, .75, fbm(st * vec2(180., 60.) + beat));
  c = mix(c, vec3(.07, .05, .08), edge * (.28 + .3 * scratch));

  // 유령 신부 톤: 채도 낮추고 그림자는 청회색, 밝은 곳은 바랜 분홍/상아.
  float l = lum(c);
  c = mix(vec3(l), c, .8);
  vec3 shadowTint = vec3(.9, .97, 1.18), lightTint = vec3(1.06, .98, .95);
  c *= mix(shadowTint, lightTint, smoothstep(.08, .6, l));
  c = mix(c, c * vec3(.9, .95, 1.08), .35 * (1. - smoothstep(.0, .35, l)));
  c = c * 1.04 + .03; // 떠 있는 검정 + 살짝 밝게

  // 종이: 큰 얼룩 + 가는 섬유결 (곱하기)
  float blot = fbm(st * 3.2);
  float fiber = fbm(st * vec2(520., 140.)) * .6 + fbm(st * vec2(140., 520.)) * .4;
  c *= 1. - .07 * blot - .075 * (fiber - .5);

  // 비네트 (굶지마식 어두운 가장자리) + 욕망 수치가 높으면 붉게 조여 온다.
  vec2 d = (st - .5) * vec2(1.05, 1.25);
  float vig = smoothstep(.4, .95, length(d));
  c = mix(c, vec3(.05, .03, .06) + vec3(.18, 0., .07) * desire, vig * (.55 + .25 * desire));

  // 필름 그레인 (12fps) + 촛불 깜빡임
  float g = hash(st * res + floor(time * 12.)) - .5;
  c += g * .045 * motion;
  c *= 1. + (noise(vec2(time * 2.3, 0.)) - .5) * .035 * motion;
  gl_FragColor = vec4(clamp(c, 0., 1.), 1.);
}`;

export function createPostFX(source, { enabled = true } = {}) {
  const view = document.createElement('canvas');
  view.className = 'postfx';
  view.width = source.width; view.height = source.height;
  view.setAttribute('aria-hidden', 'true');
  Object.assign(view.style, { position: 'absolute', pointerEvents: 'none', left: '0', top: '0' });
  const gl = view.getContext('webgl', { premultipliedAlpha: false, alpha: false });
  if (!gl || !enabled) return { draw() {}, setEnabled() {}, enabled: false };
  source.parentElement.append(view);

  const shader = (type, code) => {
    const s = gl.createShader(type);
    gl.shaderSource(s, code); gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s));
    return s;
  };
  const prog = gl.createProgram();
  gl.attachShader(prog, shader(gl.VERTEX_SHADER, VERT));
  gl.attachShader(prog, shader(gl.FRAGMENT_SHADER, FRAG));
  gl.linkProgram(prog);
  gl.useProgram(prog);
  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
  const loc = gl.getAttribLocation(prog, 'p');
  gl.enableVertexAttribArray(loc);
  gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
  const tex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, tex);
  for (const [k, v] of [[gl.TEXTURE_MIN_FILTER, gl.LINEAR], [gl.TEXTURE_MAG_FILTER, gl.LINEAR],
    [gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE], [gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE]]) gl.texParameteri(gl.TEXTURE_2D, k, v);
  const u = Object.fromEntries(['tex', 'res', 'time', 'beat', 'motion', 'desire'].map((n) => [n, gl.getUniformLocation(prog, n)]));
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let on = true;

  function place() {
    // 원본 캔버스의 화면상 위치·크기를 그대로 따라간다.
    const s = view.style;
    s.left = source.offsetLeft + 'px'; s.top = source.offsetTop + 'px';
    s.width = source.offsetWidth + 'px'; s.height = source.offsetHeight + 'px';
  }

  return {
    enabled: true,
    setEnabled(v) { on = v; view.hidden = !v; source.style.opacity = v ? '0' : ''; },
    draw(seconds, desire = 0) {
      if (!on) return;
      source.style.opacity = '0';
      place();
      gl.viewport(0, 0, view.width, view.height);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
      gl.uniform1i(u.tex, 0);
      gl.uniform2f(u.res, view.width, view.height);
      gl.uniform1f(u.time, seconds);
      gl.uniform1f(u.beat, Math.floor(seconds * 8));
      gl.uniform1f(u.motion, reduced ? 0 : 1);
      gl.uniform1f(u.desire, Math.max(0, Math.min(1, desire)));
      gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    },
  };
}
