// 정적 파일 서버 + 리그 저장 API (의존성 없음)
// node server.js  →  http://localhost:5173  (게임: /, 리깅: /rig.html)
const http = require('http');
const fs = require('fs');
const path = require('path');

// 웹판은 web/ 에 동결되어 있고, 아트는 Unity 프로젝트와 공유한다.
// /assets/* 는 Unity Assets 폴더를 먼저 찾고, 없으면 Assets 밖으로 뺀 원본(art_src)에서 찾는다.
const ROOT = __dirname;
const PROJECT = path.join(__dirname, '..');
const ASSET_ROOTS = [path.join(PROJECT, 'assets'), path.join(PROJECT, 'art_src')];
const PORT = Number(process.env.PORT) || 5173;
const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.svg': 'image/svg+xml',
  '.md': 'text/markdown; charset=utf-8',
};
// 저장 허용 경로 (리그 데이터만)
const SAVE_DIR = path.join(PROJECT, 'assets', 'rig');

http.createServer((req, res) => {
  const url = new URL(req.url, 'http://localhost');
  if (req.method === 'POST' && url.pathname === '/api/save') {
    const name = path.basename(url.searchParams.get('file') || '');
    if (!/^[\w.-]+\.json$/.test(name)) { res.writeHead(400); return res.end('bad name'); }
    let body = '';
    req.on('data', (c) => { body += c; if (body.length > 5e6) req.destroy(); });
    req.on('end', () => {
      try { JSON.parse(body); } catch { res.writeHead(400); return res.end('bad json'); }
      fs.mkdirSync(SAVE_DIR, { recursive: true });
      fs.writeFileSync(path.join(SAVE_DIR, name), body);
      res.writeHead(200, { 'content-type': 'application/json' });
      res.end('{"ok":true}');
    });
    return;
  }
  let p = decodeURIComponent(url.pathname);
  if (p === '/') p = '/index.html';
  let candidates;
  if (p.startsWith('/assets/')) {
    const rest = p.slice('/assets/'.length);
    candidates = ASSET_ROOTS.map(base => [base, path.normalize(path.join(base, rest))]);
  } else candidates = [[ROOT, path.normalize(path.join(ROOT, p))]];
  if (candidates.some(([base, file]) => !file.startsWith(base))) { res.writeHead(403); return res.end(); }
  const file = (candidates.find(([, f]) => fs.existsSync(f)) || candidates[0])[1];
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404); return res.end('not found'); }
    res.writeHead(200, { 'content-type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream', 'cache-control': 'no-store' });
    res.end(data);
  });
}).listen(PORT, () => console.log(`욕망의 탑 dev server: http://localhost:${PORT}  (rig: /rig.html)`));
