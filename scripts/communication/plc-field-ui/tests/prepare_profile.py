"""Generate the field template from the same confirmed table embedded by the Host protocol project."""
from pathlib import Path
import json, shutil

ROOT = Path(__file__).resolve().parents[4]
TOOL = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'configuration/plc/confirmed-20261006'
layout = json.loads((SOURCE / 'points.json').read_text(encoding='utf-8'))
signals = []
for point in layout['points']:
    mb, kind, name = point['memoryByteAddress'], point['valueType'], point['id']
    allowed = [0, 1] if kind == 'BoolByte' else []
    if name in ('Flip_Sorting', 'Sorting_Cmd', 'Grab_ID'): allowed = [0, 1, 2]
    if name == 'Retry_Cmd': allowed = [1, 2, 3]
    label = point['comment'].split('；')[0].split('。')[0].split('（')[0]
    if label.startswith('0=') or len(label) > 30: label = name
    signals.append(dict(id=name, label=label or name, direction=point['direction'], mw=mb//2, mb=mb,
        type=kind, enabled=True, writeEnabled=False, min=None, max=None, allowedValues=allowed,
        note=f"{point['sourceFile']} 第{point['sourceRow']}行：{point['comment']}"))

axes=[]
for name,target,start,feedback,actual in [
    ('X','Camera_Target_X','X_Move_Start','X_Pos_Confirmed','Machine_Current_Pos_X'),
    ('Y','Camera_Target_Y','Y_Move_Start','Y_Pos_Confirmed','Machine_Current_Pos_Y'),
    ('CameraZ','Camera_Target_Z','Z_Camera_Move_Start','Z_Camera_Pos_Confirmed','Machine_Current_Pos_Z'),
    ('ScanZ','Scan_Target_Z','Z_Scan_Move_Start','Z_Scan_Pos_Confirmed','Scan_Current_Pos_Z'),
    ('GrabZ','Grab_Target_Z','Z_Grab_Move_Start','Z_Grap_Pos_Confirmed','Flip_Grap_Current_Pos_Z'),
    ('R','Rotate_Target_R','Rotate_Start','R_Pos_Confirmed','Machine_Current_Pos_R')]:
    axes.append(dict(name=name,target=target,start=start,feedback=feedback,actual=actual,confirmed=False,
        min=None,max=None,tolerance=None,unit='',frame='',timeoutMs=None,
        startValue=1,idleValue=0,movingValue=0,doneValue=1,errorValues=[2]))
config=dict(schemaVersion=2,layoutId=layout['layoutId'],purpose='Field',sourceReference=layout['sourceReference'],
    plcProgramVersion='',connection=dict(protocol='ModbusTcp',host='',port=502,unitId=1,timeoutMs=1000,pollMs=200),
    mapping=dict(pcPduBase=1000,plcPduBase=3000,plcArea='HoldingRegister',boolByteOrder='EvenLow',floatOrder='Abcd',
        confirmed=False,source='软件初值，尚未现场校准；不代表实际PLC映射已确认'),
    writesConfirmed=False,singleWriterConfirmed=False,
    heartbeat=dict(confirmed=False,request='PLC_Heartbeat_Req',response='PC_Heartbeat_Resp',timeoutMs=3000),
    signals=signals,axes=axes)
(TOOL/'site.template.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
shutil.copy2(SOURCE/'points.json', TOOL/'confirmed-points.json')
shutil.copytree(SOURCE/'sources',TOOL/'sources/confirmed-20261006',dirs_exist_ok=True)
print(json.dumps(dict(layoutId=layout['layoutId'],signals=len(signals),axes=len(axes),hardwareTested=False)))
