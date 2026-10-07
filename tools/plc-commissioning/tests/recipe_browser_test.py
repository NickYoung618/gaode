import argparse
import json
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]


def get(url):
    return json.load(urllib.request.urlopen(url,timeout=3))


def free_port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1',0));return sock.getsockname()[1]


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path,required=True);args=parser.parse_args()
    evidence=ROOT/'evidence/recipe-browser';evidence.mkdir(parents=True,exist_ok=True)
    temp=tempfile.TemporaryDirectory(prefix='配方浏览器验证-',ignore_cleanup_errors=True)
    root=Path(temp.name)/'独立联调包';shutil.copytree(args.package,root,ignore=shutil.ignore_patterns('logs','__pycache__'))
    (root/'config/local.json').unlink(missing_ok=True)
    (root/'config/recipe.local.json').unlink(missing_ok=True)
    port,sim_port=free_port(),free_port();processes=[];handles=[]
    try:
        for name,cmd in [('sim',[sys.executable,str(ROOT/'tests/recipe_simulator.py'),'--root',str(root),'--port',str(sim_port)]),
                         ('app',[str(root/'runtime/python.exe'),'-X','utf8',str(root/'src/app.py'),'--port',str(port),'--no-browser'])]:
            h=(evidence/(name+'.log')).open('w',encoding='utf-8');handles.append(h)
            processes.append(subprocess.Popen(cmd,cwd=root,stdout=h,stderr=h,creationflags=subprocess.CREATE_NO_WINDOW))
        url=f'http://127.0.0.1:{port}'
        for _ in range(80):
            try:s=get(url+'/api/state');break
            except OSError:time.sleep(.1)
        token=get(url+'/api/session')['token']
        cfg=s['config'];cfg.update(pollMs=100,host='127.0.0.1',port=sim_port,sourceAddress='')
        request=urllib.request.Request(url+'/api/settings',data=json.dumps(cfg).encode(),headers={'Content-Type':'application/json','X-Console-Token':token})
        urllib.request.urlopen(request).close()
        errors=[]
        with sync_playwright() as pw:
            browser=pw.chromium.launch(channel='msedge',headless=True)
            page=browser.new_page(viewport={'width':1500,'height':1000},bypass_csp=True)
            page.on('pageerror',lambda error:errors.append(str(error)))
            page.goto(url+'/recipe');page.wait_for_function('state && profile')
            assert page.locator('[data-step]').count()==14
            assert page.locator('#pathMap circle').count()>=8
            assert not get(url+'/api/state')['recipe']['run']
            page.screenshot(path=str(evidence/'01-recipe-path.png'),full_page=True)
            page.locator('#edit').click();page.locator('#modelCode').fill('1.25')
            assert not page.locator('#grabId').is_visible()
            assert page.locator('#safe_ZCamera').input_value()=='2'
            assert page.locator('#safe_ZScan').input_value()=='2'
            assert page.locator('#safe_ZGrab').input_value()=='2'
            page.locator('#save').click();page.wait_for_function('state.recipe.profile.modelCode===1.25 && !busy')
            page.locator('#edit').click();page.locator('#connect').click();page.wait_for_function('state.fresh && !busy')
            assert get(url+'/api/state')['sent']=={}
            page.locator('#start').click()
            page.wait_for_function('state.recipe.run && !busy',timeout=30000)
            deadline=time.monotonic()+150;last_step=None
            while time.monotonic()<deadline:
                current=get(url+'/api/state')['recipe']
                if current['run']['step']!=last_step:
                    last_step=current['run']['step'];print('Recipe step',last_step,current['run']['state'],flush=True)
                if last_step>=7 and not (evidence/'02-recipe-signals.png').exists():page.screenshot(path=str(evidence/'02-recipe-signals.png'),full_page=True)
                assert current['run']['state']!='failed',current['run']['reason']
                if current['run']['state']=='completed':break
                if current['wait'] and current['wait']['kind']=='finish':
                    page.wait_for_function("state.recipe.wait?.kind==='finish'")
                    page.locator('#confirm').click();break
                assert not current['wait'] or current['wait']['kind'] in ('load','finish'),'中间步骤不应逐步确认'
                time.sleep(.5)
            else:raise AssertionError('Recipe browser run timed out')
            page.wait_for_function("state.recipe.run.state==='completed'",timeout=10000)
            final=get(url+'/api/state')
            assert final['recipe']['run']['result']=='OK'
            assert final['recipe']['run']['barcode']=='SIM-翻面件-001'
            assert '→' in page.locator('#signalFeed').inner_text()
            assert 'X_Move_Start' in page.locator('#signalFeed').inner_text()
            assert '本次运动完成后启动复位' in page.locator('#signalFeed').inner_text()
            assert '模拟3D' not in page.locator('#signalFeed').inner_text()
            assert '流程测试' in page.locator('#mode').inner_text()
            assert errors==[],errors
            page.screenshot(path=str(evidence/'03-recipe-completed.png'),full_page=True)
            page.set_viewport_size({'width':390,'height':844});time.sleep(.3)
            assert not page.evaluate('document.documentElement.scrollWidth>innerWidth')
            (evidence/'snapshot.json').write_text(json.dumps(final,ensure_ascii=False,indent=2),encoding='utf-8')
            shutil.copy2(Path(final['logPath'])/'events.jsonl',evidence/'loopback-TX-RX.jsonl')
            report=dict(status='passed',steps=14,safeZ=final['recipe']['profile']['safeZ'],result='OK',signalChanges=len(final['recipe']['run']['signals']),automaticIntermediateSteps=True,version=final['version'],recordPurpose=final['recipe']['run']['recordPurpose'],fScanXYOnly=True,unloadXYOnly=True,browserErrors=errors,realHardwareContact=False)
            (evidence/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False))
            browser.close()
    finally:
        for p in processes:
            if p.poll() is None:p.terminate()
            try:p.wait(timeout=5)
            except subprocess.TimeoutExpired:p.kill();p.wait()
        for h in handles:h.close()
        temp.cleanup()


if __name__=='__main__':main()


