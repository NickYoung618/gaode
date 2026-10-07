"""Explicit loopback only: partial failure and complete BYTE clear/readback."""
import asyncio
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
from playwright.sync_api import sync_playwright
from recipe_browser_test import free_port, get

ROOT=Path(__file__).resolve().parents[1]


async def simulator(root,port):
    from simulator import Simulator
    sim=Simulator(root)
    for mb in range(2000,2006):sim.set(mb,1)
    for mb,value in ((2006,1),(2009,1),(2014,2)):sim.set(mb,value)
    await sim.start(port)
    try:await asyncio.Future()
    finally:await sim.close()


def main():
    evidence=ROOT/'evidence/clear-starts-button';evidence.mkdir(exist_ok=True)
    temp=tempfile.TemporaryDirectory(ignore_cleanup_errors=True)
    root=Path(temp.name)/'package'
    shutil.copytree(ROOT/'release/Gaode-PlcCommissioning-1.1.3-win-x64',root,ignore=shutil.ignore_patterns('logs','__pycache__'))
    (root/'config/local.json').unlink(missing_ok=True)
    port,sim_port=free_port(),free_port();processes=[];handles=[]
    try:
        for name,command in [('sim',[sys.executable,__file__,'--sim',str(root),str(sim_port)]),
                             ('app',[str(root/'runtime/python.exe'),'-X','utf8',str(root/'src/app.py'),'--port',str(port),'--no-browser'])]:
            handle=(evidence/(name+'.log')).open('w',encoding='utf-8');handles.append(handle)
            processes.append(subprocess.Popen(command,cwd=root,stdout=handle,stderr=handle,creationflags=subprocess.CREATE_NO_WINDOW))
        url=f'http://127.0.0.1:{port}'
        for _ in range(80):
            try:s=get(url+'/api/state');break
            except OSError:time.sleep(.1)
        token=get(url+'/api/session')['token'];cfg=s['config'];cfg.update(host='127.0.0.1',port=sim_port,sourceAddress='',pollMs=100)
        request=urllib.request.Request(url+'/api/settings',data=json.dumps(cfg).encode(),headers={'Content-Type':'application/json','X-Console-Token':token})
        urllib.request.urlopen(request).close()
        with sync_playwright() as pw:
            browser=pw.chromium.launch(channel='msedge',headless=True)
            page=browser.new_page(viewport={'width':1500,'height':1000},bypass_csp=True);errors=[]
            page.on('pageerror',lambda err:errors.append(str(err)))
            page.goto(url+'/recipe');page.wait_for_function('state && profile')
            assert page.locator('#clearStarts').is_disabled()
            page.locator('#connect').click();page.wait_for_function('state.fresh && !busy')
            assert get(url+'/api/state')['sent']=={}
            page.locator('#heartbeat').click();page.wait_for_function('state.heartbeat.enabled && !busy')
            def fail_y(route):
                if route.request.post_data_json['axis']=='Y':route.fulfill(status=400,content_type='application/json',body=json.dumps({'error':'回环注入Y写入失败'}))
                else:route.continue_()
            page.route('**/api/clear',fail_y)
            page.locator('#clearStarts').click();page.wait_for_function('!busy && document.querySelector("#clearStatus").textContent.includes("未全部确认")')
            assert '失败' in page.locator('#clearStatus').inner_text()
            assert page.evaluate('value(2002)')==1
            page.unroute('**/api/clear',fail_y)
            page.locator('#clearStarts').click();page.wait_for_function('lastClearVerified && !busy')
            final=get(url+'/api/state')
            def value(mb):
                point=next(p for p in final['points'] if p['mb']==mb)
                return final['values'][point['id']]['value']
            assert all(value(mb)==0 for mb in range(2000,2006))
            assert value(2006)==1 and value(2009)==1 and value(2014)==2
            assert final['heartbeat']['enabled'] and final['heartbeat']['responses']>0
            events=[json.loads(line) for line in (Path(final['logPath'])/'events.jsonl').read_text(encoding='utf-8').splitlines()]
            writes=[e for e in events if e['kind']=='WRITE_ACCEPTED' and e['mb']!=2011]
            assert all(e['mb'] in range(2000,2006) and e['value']==0 for e in writes)
            assert not errors,errors
            page.screenshot(path=str(evidence/'clear-verified.png'),full_page=True)
            page.evaluate('state.values[point(2003).id].value=1;render()')
            assert not page.evaluate('lastClearVerified')
            assert '后续变化' in page.locator('#clearStatus').inner_text()
            shutil.copy2(Path(final['logPath'])/'events.jsonl',evidence/'loopback-TX-RX.jsonl')
            report=dict(status='passed',allSixStartsZero=True,partialFailureNotPassed=True,resetAndFlipUnchanged=True,heartbeatPreserved=True,laterChangesInvalidateSuccess=True,realHardwareContact=False,browserErrors=errors)
            (evidence/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report));browser.close()
    finally:
        for process in processes:
            if process.poll() is None:process.terminate()
            process.wait(timeout=10)
        for handle in handles:handle.close()
        temp.cleanup()


if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='--sim':asyncio.run(simulator(Path(sys.argv[2]),int(sys.argv[3])))
    else:main()
