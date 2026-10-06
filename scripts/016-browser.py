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
    def handle_request(self):
        path=self.path.split('?')[0]
        if path.startswith('/api/') or path.startswith('/hubs/'):
            headers={k:v for k,v in self.headers.items() if k.lower() not in ('host','content-length','connection','origin')}
            data=self.rfile.read(int(self.headers.get('content-length','0'))) if self.command=='POST' else None
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
errors=[];evidence=root/'browser';evidence.mkdir()
timeline=[]
def mark(stage,**facts):
    if len(timeline)<96:timeline.append(dict(stage=stage,utc=time.time(),monotonic=time.monotonic(),**facts))
    (root/'browser-timeline.json').write_text(json.dumps(timeline,ensure_ascii=False,indent=2),encoding='utf-8')
def actual_decision():
    headers={'Authorization':'Bearer '+c['testToken']}
    mark('StatusQuerySubmitted')
    status=json.load(urllib.request.urlopen(urllib.request.Request(c['apiBaseUrl']+'/api/v1/station01/status',headers=headers)))
    run=status['currentRun']['runId'];mark('StatusQueryObserved',runId=run)
    value=json.load(urllib.request.urlopen(urllib.request.Request(c['apiBaseUrl']+'/api/v1/station01/runs/'+run,headers=headers)))
    mark('DecisionQueryObserved',decisionId=value['trayAnomalyDecision']['decisionId'],deadlineUtc=value['trayAnomalyDecision']['deadlineUtc'],state=value['trayAnomalyDecision']['state'])
    return value['trayAnomalyDecision']
try:
    with sync_playwright() as p:
        browser=p.chromium.launch(executable_path='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless=True)
        page=browser.new_page(viewport={'width':1600,'height':1000})
        page.on('pageerror',lambda error:errors.append(str(error)))
        page.goto(f'http://127.0.0.1:{server.server_port}/a.html',wait_until='domcontentloaded')
        page.locator('#btnPositions').click();page.locator('#public-threeD-x').wait_for()
        original=page.locator('#public-threeD-x').input_value()
        page.get_by_role('button',name='读取当前实测位置',exact=True).first.click()
        page.get_by_text('已读取：Test/Virtual',exact=True).wait_for(timeout=10000)
        page.screenshot(path=str(evidence/'measured-candidate.png'))
        page.get_by_role('button',name='取消',exact=True).click()
        page.locator('#btnPositions').click();page.locator('#public-threeD-x').wait_for()
        assert page.locator('#public-threeD-x').input_value()==original
        page.locator('#public-threeD-x').fill('109');page.get_by_role('button',name='取消',exact=True).click()
        page.locator('#btnPositions').click();page.locator('#public-threeD-x').wait_for()
        assert page.locator('#public-threeD-x').input_value()==original
        page.get_by_role('button',name='保存',exact=True).click();page.get_by_text('公共位置已保存',exact=True).wait_for()
        page.screenshot(path=str(evidence/'positions.png'))
        page.get_by_role('button',name='取消',exact=True).click()
        (root/'browser-ready.json').write_text(json.dumps({'state':'ActualPageReady','cancelDiscarded':True,'saveVerifiedByApi':True}),encoding='utf-8')
        # The normal production page picks the actual current run from GET status.
        page.locator('#trayAnomalyDialog').wait_for(state='visible',timeout=60000);mark('InitialDialogObserved')
        page.get_by_text('OK区 第2行 第6列：3D姿态异常',exact=False).wait_for()
        page.screenshot(path=str(evidence/'anomaly.png'))
        before=actual_decision()
        deadline=page.locator('#trayAnomalyCountdown').inner_text()
        mark('ReloadSubmitted');page.reload(wait_until='domcontentloaded');mark('ReloadDomContentLoaded')
        page.locator('#trayAnomalyDialog').wait_for(state='visible',timeout=5000);mark('ReloadDialogObserved')
        after=actual_decision()
        assert (before['decisionId'],before['deadlineUtc'])==(after['decisionId'],after['deadlineUtc'])
        page.get_by_role('button',name='继续',exact=True).click()
        page.locator('#trayAnomalyDialog').wait_for(state='hidden',timeout=15000)
        (root/'browser-choice.json').write_text(json.dumps({'state':'ContinueSubmitted','refreshKeptOriginalDeadline':True,'decisionId':before['decisionId'],'deadlineUtc':before['deadlineUtc'],'originalCountdown':deadline}),encoding='utf-8')
        page.locator('#trayEndState').get_by_text('结束原因：正常流程结束',exact=False).wait_for(timeout=90000)
        page.screenshot(path=str(evidence/'awaiting-removal.png'))
        assert not errors,errors
        (root/'browser-result.json').write_text(json.dumps({'passed':True,'browser':'Actual Edge headless','sameHost':c['apiBaseUrl'],'errors':errors}),encoding='utf-8')
        browser.close()
except Exception as failure:
    mark('BrowserFailure',type=type(failure).__name__,message=str(failure)[:900]);raise
finally:server.shutdown()
