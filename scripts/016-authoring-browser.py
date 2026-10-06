"""Actual Edge page against the owned Host through a local same-origin HTTP proxy."""
import json,sys,threading,time,urllib.request,urllib.error
from pathlib import Path
from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
from playwright.sync_api import sync_playwright

connection=Path(sys.argv[1]).resolve(strict=True);root=connection.parent;repo=Path(__file__).resolve().parents[1]
assert root.is_relative_to(repo/'artifacts/016-public-tray-flow')
c=json.loads(connection.read_text(encoding='utf-8'));frontend=repo/'frontend/dist'
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def do_GET(self):self.handle_request()
    def do_POST(self):self.handle_request()
    def do_PUT(self):self.handle_request()
    def handle_request(self):
        path=self.path.split('?')[0]
        if path.startswith('/api/') or path.startswith('/hubs/'):
            headers={k:v for k,v in self.headers.items() if k.lower() not in ('host','content-length','connection','origin')}
            data=self.rfile.read(int(self.headers.get('content-length','0'))) if self.command in ('POST','PUT') else None
            req=urllib.request.Request(c['apiBaseUrl']+self.path,headers=headers,data=data,method=self.command)
            try:response=urllib.request.urlopen(req,timeout=15)
            except urllib.error.HTTPError as error:response=error
            streaming=response.headers.get('Content-Type','').startswith('text/event-stream')
            try:
                self.send_response(response.status)
                for k,v in response.headers.items():
                    if k.lower() not in ('transfer-encoding','content-length','connection'):self.send_header(k,v)
                if streaming:
                    self.end_headers()
                    while chunk:=response.read1(2048):self.wfile.write(chunk);self.wfile.flush()
                else:
                    body=response.read();self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
            except (BrokenPipeError,ConnectionAbortedError,ConnectionResetError):pass # A real page reload closes its old observer.
            finally:response.close()
            return
        file=(frontend/('a.html' if path=='/' else path.lstrip('/'))).resolve()
        if not file.is_relative_to(frontend) or not file.is_file():self.send_error(404);return
        body=file.read_bytes()
        if file.suffix=='.html':
            config={'mode':'Test','apiBaseUrl':'','testToken':c['testToken']}
            body=body.replace(b'<head>',b'<head><script>window.__GAODE_HOST_CONFIG__='+json.dumps(config).encode()+b';</script>',1)
        self.send_response(200);self.send_header('Content-Type',{'.html':'text/html','.js':'text/javascript','.css':'text/css','.woff2':'font/woff2'}.get(file.suffix,'application/octet-stream'))
        self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)

server=ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
evidence=root/'authoring-browser';evidence.mkdir()
def api(path):
    request=urllib.request.Request(c['apiBaseUrl']+path,headers={'Authorization':'Bearer '+c['testToken']})
    return json.load(urllib.request.urlopen(request,timeout=15))
def target(definition,camera):
    owner=definition['executionPositions']['P01']['members']['BASE']
    coordinate=next(x for x in owner['coordinates'] if x['stageId']=='stage:1' and x['camera']==camera)
    return definition['captureProfiles'][coordinate['captureProfile']]['settings']
errors=[]
try:
    catalog=api('/api/v1/recipes/catalog')['items'];selected=next(x for x in catalog if x['fCode']==c['fCode'])
    before=api('/api/v1/recipes/'+selected['recipeId'])['definition']
    original_b=target(before,'B').copy()
    with sync_playwright() as p:
        browser=p.chromium.launch(executable_path='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless=True)
        page=browser.new_page(viewport={'width':1600,'height':1000});page.on('pageerror',lambda error:errors.append(str(error)))
        page.goto(f'http://127.0.0.1:{server.server_port}/a.html',wait_until='domcontentloaded')
        def open_editor():
            page.locator('#btnRecipe').click();page.locator('#recipeAuthoringCatalog').select_option(selected['recipeId'])
            page.get_by_label('料盘编号',exact=True).wait_for();page.locator('[data-authoring-section=points]').click()
            page.locator('#recipeAuthoringForm [data-cell-id="r2:c4"]').click()
            page.locator('.recipe-nav-button').filter(has_text='拍照位置').click()
            page.locator('[data-object-material="BASE"]').click()
            page.locator('[data-local-face="1"]').click()
        open_editor()
        assert page.locator('.recipe-object-rail [data-object-material]').count()==2
        card=page.locator('.recipe-point-card[data-material="BASE"][data-stage-id="stage:1"][data-camera="A"]')
        for label,value in [('曝光 (µs)','711'),('增益','1.71'),('光源亮度 (%)','41')]:
            card.get_by_label(label,exact=True).fill(value);card.get_by_label(label,exact=True).dispatch_event('change')
        page.screenshot(path=str(evidence/'member-face-camera.png'))
        page.locator('#recipeAuthoringSave').click();page.get_by_text('已保存并重读',exact=False).wait_for(timeout=20000)
        saved=api('/api/v1/recipes/'+selected['recipeId'])['definition']
        actual=target(saved,'A');assert actual['exposureUs']==711 and actual['gain']==1.71 and actual['brightnessPercent']==41
        assert target(saved,'B')==original_b
        assert saved['positions']==before['positions'] and saved['composition']==before['composition']
        assert saved['sortingGripperId']==before['sortingGripperId']
        page.locator('#recipeModal button[onclick*="closeRecipe"]').first.click()
        open_editor();assert card.get_by_label('曝光 (µs)',exact=True).input_value()=='711'
        page.locator('[data-object-material="PIN"]').click();assert page.locator('.recipe-point-card[data-material="PIN"]').count()>0
        assert page.locator('.recipe-point-card[data-material="BASE"]').count()==0
        page.screenshot(path=str(evidence/'independent-other-member.png'))
        if before['unitKind']=='assembledEntity':
            page.locator('.recipe-nav-button').filter(has_text='翻面取放').click()
            assert page.locator('.recipe-object-rail').count()==0
            page.screenshot(path=str(evidence/'whole-handling.png'))
        assert not errors,errors
        (root/'authoring-browser-result.json').write_text(json.dumps({'result':'Passed','recipeId':selected['recipeId'],'actualEdge':True,
            'before':before,'saved':saved,'independentCameraBUnchanged':True,'fullHiddenPositionsRetained':True,'reopenVerified':True,
            'scope':'Actual page PUT/full GET; same owned Host/SQLite; process input version and later restart separately asserted','errors':errors},ensure_ascii=False,indent=2),encoding='utf-8')
        browser.close()
finally:server.shutdown()
