"""모이라 리깅 페이지 서버 (로컬 전용).

python tools/rig_editor/server.py [port]

- 프로젝트 폴더를 그대로 정적 파일로 내준다 (리깅 페이지 = /tools/rig_editor/moira.html, 그림 = /Assets/sprites/...).
- POST /save?path=Assets/Rig/<이름>.json 으로 받은 JSON 을 저장한다. Assets/Rig, art_src/rig 안에만 쓸 수 있다.
  Assets/Rig 의 리그 JSON 은 Unity 가 TextAsset 으로 그대로 읽는다(MoiraRig). 저장하면 Unity 에 바로 반영된다.
"""
import http.server
import json
import sys
from pathlib import Path
from urllib.parse import parse_qs, urlparse

ROOT = Path(__file__).resolve().parents[2]
RIG_DIRS = [(ROOT / "Assets" / "Rig").resolve(), (ROOT / "art_src" / "rig").resolve()]


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_POST(self):
        url = urlparse(self.path)
        if url.path != "/save":
            self.send_error(404)
            return
        rel = parse_qs(url.query).get("path", [""])[0]
        target = (ROOT / rel).resolve()
        if not any(d in target.parents for d in RIG_DIRS) or target.suffix != ".json":
            self.send_error(403, "Assets/Rig, art_src/rig 의 *.json 만 저장할 수 있습니다")
            return
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        try:
            data = json.loads(body.decode("utf-8"))
        except ValueError:
            self.send_error(400, "JSON 이 아닙니다")
            return
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(b'{"ok":true}')

    def log_message(self, fmt, *args):
        if self.command == "POST":
            super().log_message(fmt, *args)


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 5180
    print(f"rig editor: http://localhost:{port}/tools/rig_editor/moira.html", flush=True)
    http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
