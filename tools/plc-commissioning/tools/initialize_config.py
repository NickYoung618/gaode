"""Local startup configuration; no TCP connections or PLC reads/writes."""
import json
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'src'))
from network import automatic_source

root = Path(__file__).resolve().parents[1]
path = root / 'config/local.json'
defaults = json.loads((root / 'config/default.json').read_text(encoding='utf-8'))
saved = json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else {}
cfg = dict(defaults, **saved)
if not cfg.get('sourceAddress'):
    try:
        cfg['sourceAddress'] = automatic_source(cfg['host']) or ''
    except Exception as error:
        print('PLC网卡未自动确定：' + str(error))
        print('页面仍可打开，请在高级设置指定本机PLC网卡IP。')
path.write_text(json.dumps(cfg, ensure_ascii=False, indent=2), encoding='utf-8')
print('本机PLC连接源地址：' + (cfg.get('sourceAddress') or '连接时识别'))
print('REAL发送/反馈：' + cfg['realWriteOrder'] + ' / ' + cfg['realReadOrder'])
