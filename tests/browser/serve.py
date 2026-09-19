"""Local same-origin API proxy and SPA server used only by browser tests."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
import os
from urllib.request import Request, urlopen
from urllib.error import HTTPError

os.chdir(os.environ['WEB_ROOT'])


class Handler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith('/api/'):
            return self.proxy()
        if not os.path.isfile(self.path.split('?')[0].lstrip('/')):
            self.path = '/index.html'
        super().do_GET()

    def do_POST(self):
        self.proxy()

    def do_DELETE(self):
        self.proxy()

    def proxy(self):
        body = self.rfile.read(int(self.headers.get('content-length', 0)))
        request = Request(f"http://127.0.0.1:{os.environ['API_PORT']}" + self.path,
                          data=body if self.command != 'GET' else None, method=self.command,
                          headers={k: v for k, v in self.headers.items()
                                   if k.lower() not in ['host', 'connection', 'content-length']})
        try:
            response = urlopen(request, timeout=30)
        except HTTPError as error:
            response = error
        with response:
            self.send_response(response.status)
            self.send_header('Content-Type', response.headers.get('Content-Type', 'application/json'))
            self.end_headers()
            self.wfile.write(response.read())


ThreadingHTTPServer(('127.0.0.1', int(os.environ['WEB_PORT'])), Handler).serve_forever()
