"""Finite counterparts for the existing route/authorization/timeout/scene scripts.

Literal handshake expectations originate in confirmed protocol §2.4/2.5. These
checks observe communication only, and cannot replace business/DB/page checks.
"""
import argparse
import json
from pathlib import Path


def load(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def prop(value,name,default=None):
    return next((v for k,v in value.items() if k.lower()==name.lower()),default)


def check(mode, facts, expected):
    checks={}
    if mode=='authorization':
        audit=facts['plcWriteAudit']
        checks['no_device_actions']=not audit.get('actions',[]) and audit.get('actionGap') is False
    if mode in {'route','flip-timeout','summary','scene'}:
        batch=facts['plcChanges'];changes=batch['changes']
        checks['complete_changes']=batch.get('gap') is False
        def count(name,value):return sum(c.get('name')==name and str(c.get('current'))==value for c in changes)
        if mode=='route':checks['flip_ack_clears']=count('Flip_OK','1')==count('Flip_OK','0')==expected['flips']
        if mode=='flip-timeout':
            checks['actual_face_two']=count('Flip_Status','2')>0 and count('Flip_Current_Face','2')>0
            checks['ack_set_without_clear']=count('Flip_OK','1')>0 and count('Flip_OK','0')==count('Flip_Status','0')==0
        if mode=='summary':
            checks['actual_capture_moves']=sum(c.get('address')=='4x0001' and str(c.get('current'))=='2' for c in changes)>=expected['captures']
            checks['actual_unload_move']=sum(c.get('address')=='4x0001' and str(c.get('current'))=='4' for c in changes)==1
            checks['actual_resets']=count('Z_Reset_Status','2')>=expected['captures']
        if mode=='scene':
            audit=facts['plcWriteAudit']
            sorts=[a for a in prop(audit,'actions',[]) if prop(a,'kind')=='Sort' and prop(a,'phase')=='completed']
            checks['complete_actions']=prop(audit,'actionGap') is False
            checks['actual_pick_place_counts']=all(sum(prop(a,'command')==command for a in sorts)==expected['sorts'] for command in (1,2))
            if expected.get('manual'):
                checks['manual_occupancy_complete']=all(count(name,value)>0 for name in ('Manual_Zone_Occupied','Manual_Flip_Complete') for value in ('1','0'))
                checks['no_automatic_substitution']=count('Flip_OK','1')==0
            if expected.get('special'):
                exits=[a for a in facts['specialActions'] if prop(prop(a,'request'),'kind')=='Exit']
                checks['actual_special_exit']=len(exits)==expected['exits'] and all(
                    prop(prop(a,'request'),'poseId')==expected['pose'] and prop(a,'status')=='Completed' and prop(a,'occupiedEntityId') is None for a in exits)
    if not checks:raise ValueError('UnsupportedCommunicationCheck')
    return dict(scope='CommunicationOnly',mode=mode,checks=checks,passed=all(checks.values()))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode',choices=['authorization','route','flip-timeout','summary','scene'])
    parser.add_argument('root',type=Path)
    parser.add_argument('--page',action='store_true')
    args=parser.parse_args();root=args.root.resolve()
    if args.mode=='authorization':facts=load(root/'authorization-api-device-facts.json');expected={}
    elif args.mode in {'route','flip-timeout'}:
        facts=load(root/'page-api-device-facts.json') if args.page else dict(plcChanges=load(root/'plc-changes.json'))
        expected=load(root/'route-validation.json') if args.mode=='route' else {}
        if args.mode=='route':expected={'flips':expected['expectedFlipActions']}
    else:
        facts=load(root/'page-api-device-facts.json')
        business=load(root/('scene-acceptance-audit.json' if args.mode=='scene' else 'verified-facts.json'))
        expected=business['communicationExpectation']
    result=check(args.mode,facts,expected)
    with (root/(args.mode+'-wire-validation.json')).open('x',encoding='utf-8') as output:json.dump(result,output,indent=2)
    print(json.dumps(result))
    return 0 if result['passed'] else 1


if __name__=='__main__':raise SystemExit(main())
