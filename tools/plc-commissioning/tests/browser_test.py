"""Isolated browser/UI verification against the explicit loopback simulator."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import os
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import zipfile
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]


def get(url):
    with urllib.request.urlopen(url,timeout=3) as response:return json.load(response)


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--package',type=Path)
    args=parser.parse_args()
    evidence=ROOT/'evidence'/'browser';evidence.mkdir(parents=True,exist_ok=True)
    temporary=tempfile.TemporaryDirectory(prefix='PLC联调解压测试-',ignore_cleanup_errors=True)
    testroot=Path(temporary.name)/'独立联调包'
    if args.package:
        shutil.copytree(args.package,testroot,ignore=shutil.ignore_patterns('logs','startup*.log','__pycache__'))
        interpreter=str(testroot/'runtime/python.exe')
    else:
        testroot.mkdir()
        for directory in ('src','config','sources','specs'):shutil.copytree(ROOT/directory,testroot/directory)
        interpreter=sys.executable
    (testroot/'config/local.json').unlink(missing_ok=True)
    handles=[];processes=[]
    def free_port():
        with socket.socket() as sock:
            sock.bind(('127.0.0.1',0));return sock.getsockname()[1]
    ui_port,sim_port=free_port(),free_port()
    try:
        for name,cmd in [('simulator',[sys.executable,str(ROOT/'tests/simulator.py'),'--port',str(sim_port)]),
                         ('app',[interpreter,'-X','utf8',str(testroot/'src/app.py'),'--port',str(ui_port),'--no-browser'])]:
            handle=(evidence/(name+'.log')).open('w',encoding='utf-8');handles.append(handle)
            processes.append(subprocess.Popen(cmd,cwd=testroot,stdout=handle,stderr=handle))
        url=f'http://127.0.0.1:{ui_port}'
        for _ in range(80):
            try:initial=get(url+'/api/state');break
            except OSError:time.sleep(.1)
        else:raise AssertionError('UI backend did not start')
        assert initial['status']=='disconnected'
        assert not initial['heartbeat']['enabled']
        token=get(url+'/api/session')['token']
        unauthorized=urllib.request.Request(url+'/api/connect',data=json.dumps(dict(host='127.0.0.1',port=sim_port)).encode(),headers={'Content-Type':'application/json'})
        try:urllib.request.urlopen(unauthorized);raise AssertionError('unauthorized POST accepted')
        except urllib.error.HTTPError as error:assert error.code==403
        errors=[]
        with sync_playwright() as playwright:
            browser=playwright.chromium.launch(channel='msedge',headless=True)
            page=browser.new_page(viewport={'width':1536,'height':1024},bypass_csp=True)
            page.on('pageerror',lambda error:errors.append(str(error)))
            page.goto(url)
            page.wait_for_function('state && built')
            assert page.locator('#pcSignals .signal').count()==26
            assert page.locator('#plcSignals .signal').count()==59
            assert page.locator('[data-axis]').count()==6
            assert page.locator('#host').input_value()=='192.168.0.10'
            assert page.locator('#port').input_value()=='502'
            page.locator('#host').fill('127.0.0.1');page.locator('#port').fill(str(sim_port))
            page.locator('#connect').click()
            page.wait_for_function('state.fresh && !busy')
            connected=get(url+'/api/state')
            assert not connected['heartbeat']['enabled']
            assert connected['sent']=={}
            assert all(e.get('function') in (None,3) for e in connected['events'])
            page.screenshot(path=str(evidence/'01-readonly-signals.png'),full_page=True)
            page.locator('[data-send="pc.System_Reset_Cmd"][data-value="1"]').click()
            page.wait_for_function("state.values['pc.System_Reset_Cmd'].value===1 && !busy")
            assert page.locator('#interlockHints').is_visible()
            assert 'MB2009' in page.locator('#interlockHints').inner_text()
            page.locator('[data-send="pc.System_Reset_Cmd"][data-value="0"]').click()
            page.wait_for_function("state.values['pc.System_Reset_Cmd'].value===0 && !busy")

            page.locator('#input_pc_Camera_Target_X').fill('-12.5')
            time.sleep(1.1)
            assert page.locator('#input_pc_Camera_Target_X').input_value()=='-12.5'
            page.locator('#target_X').fill('-12.5')
            page.locator('[data-axis=X]').click()
            page.wait_for_function("state.actions.some(a=>a.axis==='X' && a.state==='completed' && a.cleared)",timeout=12000)
            page.locator('#target_R').fill('45.25')
            page.locator('[data-axis=R]').click()
            page.wait_for_function("state.actions.some(a=>a.axis==='R' && a.state==='completed' && a.cleared)",timeout=12000)
            page.locator('#heartbeat').click()
            page.wait_for_function('state.heartbeat.responses>=2 && state.heartbeat.changes>=1',timeout=8000)
            page.locator('#axes').scroll_into_view_if_needed()
            page.screenshot(path=str(evidence/'02-axes-completion.png'),full_page=True)
            page.locator('#cmd_2014').fill('1');page.locator('[data-command="2014"]').click()
            page.wait_for_function('!busy')
            page.locator('#note').fill('回环模拟器验证：X/R本次运动与清零。真实设备尚未测试。<script>按文字保存</script>')
            page.locator('#saveNote').click();page.wait_for_function('!busy')
            page.locator('[data-locate]').first.click()
            assert page.locator('#operationFilter').input_value()
            assert page.locator('#events .event').count()>0
            assert 'TX' in page.locator('#events').inner_text()
            page.locator('#events details').first.locator('summary').click()
            opened=page.locator('#events details[open]').first.get_attribute('data-event')
            time.sleep(1.1)
            assert page.locator(f'#events details[data-event="{opened}"]').get_attribute('open') is not None
            page.screenshot(path=str(evidence/'03-txrx-timeline.png'),full_page=True)
            with page.expect_download() as download:page.locator('#export').click()
            download.value.save_as(evidence/'loopback-session.zip')
            with zipfile.ZipFile(evidence/'loopback-session.zip') as archive:
                for name,digest in json.loads(archive.read('manifest.sha256.json')).items():
                    assert hashlib.sha256(archive.read(name)).hexdigest()==digest
                snapshot=json.loads(archive.read('snapshot.json'))
                assert len(snapshot['points'])==85
                for axis in ('X','R'):
                    action=next(a for a in snapshot['actions'] if a['axis']==axis)
                    assert action['plcCompleted'] is True and action['cleared'] is True
                    assert action['coordinateAcceptance'] is None
            page.locator('#showSettings').click()
            assert page.locator('#realWriteOrder').input_value()=='Cdab'
            assert page.locator('#realReadOrder').input_value()=='Cdab'
            page.locator('#realReadOrder').select_option('Abcd');page.locator('#saveSettings').click()
            page.wait_for_function("!state.tcpConnected && state.config.realReadOrder==='Abcd' && state.config.realWriteOrder==='Cdab' && !busy")
            assert '最后一次数据' in page.locator('#pcSignals').inner_text()
            assert page.locator('[data-axis=X]').is_disabled()
            assert page.locator('#pcSignals .lamp.on').count()==0
            assert page.locator('#plcSignals .lamp.on').count()==0
            page.set_viewport_size({'width':430,'height':900})
            assert not page.evaluate('document.documentElement.scrollWidth > innerWidth')
            page.screenshot(path=str(evidence/'04-disconnected-mobile.png'),full_page=True)
            assert errors==[],errors
            browser.close()
        result=dict(status='passed',runtime='embedded-x64' if args.package else 'source-python',
                    points=85,pcPoints=26,plcPoints=59,axes=6,X=-12.5,R=45.25,
                    connectWrites=0,heartbeatExplicit=True,actionsCompletedAndCleared=True,
                    oldDataGrey=True,settingsSaved=True,exportHashesVerified=True,browserErrors=errors,
                    realHardwareContact=False)
        (evidence/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(result,ensure_ascii=False))
    finally:
        for process in processes:
            subprocess.run([str(Path(os.environ['SystemRoot'])/'System32/taskkill.exe'),'/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            process.wait(timeout=10)
        for handle in handles:handle.close()
        temporary.cleanup()


if __name__=='__main__':main()

