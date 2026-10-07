"""Reuse or activate a newer independent local console; never starts motion."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time
import urllib.request

URL='http://127.0.0.1:18770'
NAME='Gaode Independent PLC Commissioning'


def get(path):
    return json.load(urllib.request.urlopen(URL+path,timeout=2))


def post(path,data,token):
    request=urllib.request.Request(URL+path,data=json.dumps(data).encode(),headers={'Content-Type':'application/json','X-Console-Token':token})
    return json.load(urllib.request.urlopen(request,timeout=10))


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--target',type=Path,required=True);args=parser.parse_args()
    target=args.target.resolve()
    if not re.fullmatch(r'Gaode-PlcCommissioning-\d+\.\d+\.\d+-win-x64',target.name):raise ValueError('无效独立包目录')
    expected=json.loads((target/'release.ready.json').read_text(encoding='utf-8'))['version']
    try:old=get('/api/state')
    except OSError:return
    if old.get('app')!=NAME:raise ValueError('端口被其他程序占用，未修改')
    previous=Path(old['rootPath']).resolve()
    if previous==target and old['version']==expected:return
    if previous.parent!=target.parent or not re.fullmatch(r'Gaode-PlcCommissioning-\d+\.\d+\.\d+-win-x64',previous.name):raise ValueError('已有服务不属于同一独立包发布目录，未停止')
    run=old.get('recipe',{}).get('run')
    if run and run['state'] in ('running','waiting','paused') or any(a['state'] in ('observing','pending') for a in old['actions']):
        raise ValueError('已有配方或轴动作正在运行，请先完成或结束本盘再打开新版；未停止现有服务')
    for name,data in [('local.json',old['config']),('recipe.local.json',old['recipe']['profile'])]:
        (target/'config'/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    logs=target/'logs';logs.mkdir(exist_ok=True)
    (logs/'before-version-switch.json').write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf-8')
    post('/api/shutdown',{},get('/api/session')['token'])
    for _ in range(100):
        try:get('/api/state');time.sleep(.05)
        except OSError:break
    else:raise RuntimeError('旧服务未退出，未启动第二个服务')
    with (logs/'launcher-output.log').open('ab') as out,(logs/'launcher-error.log').open('ab') as err:
        subprocess.Popen([str(target/'runtime/python.exe'),'-X','utf8',str(target/'src/app.py'),'--port','18770','--no-browser'],cwd=target,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
    for _ in range(100):
        try:
            current=get('/api/state')
            if current['rootPath']==str(target) and current['version']==expected:break
        except OSError:pass
        time.sleep(.05)
    else:raise RuntimeError('新版服务未就绪，请查看launcher-error.log')
    if old.get('tcpConnected'):
        token=get('/api/session')['token']
        post('/api/connect',dict(host=old['config']['host'],port=old['config']['port']),token)
        if old['heartbeat']['enabled']:post('/api/heartbeat',dict(enabled=True),token)
    print('已切换最新独立包，保留现场配置；未发送运动、清零或复位。')


if __name__=='__main__':main()
