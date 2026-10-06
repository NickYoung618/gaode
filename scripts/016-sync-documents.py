"""Directed active-document alignment; never operates on source archives/evidence."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
FEATURE = ROOT/'specs/016-public-preparation-tray-check-unload'
prefixes = ('001-','003-','006-','008-','009-','010-','011-','012-','014-')
replacements = {
    '异常不能显示成NG/Pending': '异常不得伪造检测质量结果，必须分别显示未检测或后续终止及真实Pending物理处置',
    '不映射为NG/Pending': '不伪造NG/Pending检测结论，另显示真实Pending物理处置',
    '不能映射为NG/Pending': '不能伪造NG/Pending检测结论，另记录真实Pending物理处置',
    '不能映射成NG/Pending': '不能伪造NG/Pending检测结论，另记录真实Pending物理处置',
    '不得映射为NG/Pending': '不得伪造NG/Pending检测结论，另记录真实Pending物理处置',
    '不跳过必要公共准备/F扫码': '公共准备真实执行；首次继续才F扫码，空盘/介入无需F即可共享下料',
    '本次仅新增012明确授权的配方弹窗制作/保存交互': '本次承接012配方弹窗及新016明确授权顶部公共位置/异常选择弹窗',
    '仅配方弹窗允许012授权差异': '配方弹窗允许012授权差异，顶部公共位置入口/弹窗及异常选择允许新016授权差异',
    '仅承接012授权配方弹窗及既有动态绑定': '承接012授权配方弹窗、新016顶部公共位置与异常弹窗及既有动态绑定',
    '仅承接012已授权弹窗差异及既有动态绑定': '承接012已授权弹窗、新016顶部公共位置及异常弹窗与既有动态绑定',
    '当前仅实现副本的012配方弹窗按已授权映射调整': '当前实现副本的012配方弹窗及新016顶部公共位置/异常弹窗按已授权映射调整',
    '仅a.html已有配方弹窗按012授权承接基础信息、点位配置、检查保存': 'a.html已有配方弹窗按012授权承接基础信息、点位配置、检查保存，顶部公共位置入口/弹窗及异常选择按新016授权承接',
    '| Running3D | 高度成功或允许异常已保存，F安全/配置仍有效 | RunningF，保存F移动意图再派发 | 无有效高度也可F |': '| Running3D | 本次完整3D观察已保存 | 有效空盘或人工介入走共享下料；正常或异常继续才RunningF | 无效观察不得判空盘；复查不重绑F |',
    '姿态异常原槽退出': '姿态异常跳过后续检测，最后从原槽实际分拣到Pending',
    '姿态异常原位退出': '姿态异常跳过后续检测，最后从原槽实际分拣到Pending',
    '异常原位退出，不作NG/Pending搬运': '异常跳过检测，最后从原槽实际分拣Pending',
    '异常原槽保留、无后续检测/翻面/分拣': '异常保留原检测证据、跳过后续检测/翻面、最后从原槽分拣Pending',
    '姿态异常原槽保留、无后续检测/翻面/分拣': '姿态异常保留原检测证据、跳过后续检测/翻面、最后从原槽分拣Pending',
    '姿态异常独立原槽保留，退出后续检测/翻面/分拣': '姿态异常独立处置，保留原检测证据、退出后续检测/翻面，最后从原槽分拣Pending',
    '姿态异常独立原槽退出': '姿态异常独立处置，跳过后续检测并最后从原槽分拣Pending',
    '姿态异常不等于NG/Pending': '姿态异常不伪造检测质量结果，物理处置为Pending',
    '异常槽位无后续检测/翻面/分拣': '异常槽位跳过后续检测/翻面、最后实际Pending分拣',
    '异常原槽退出': '异常跳过后续检测、最后原槽Pending分拣',
    '异常原位退出': '异常跳过后续检测、最后原槽Pending分拣',
    '正常继续、异常原槽退出并返回物理槽号': '异常先经过后端单次10秒选择；继续后跳过该件后续检测并最后原槽Pending，介入走公共下料；F不重绑且返回物理槽号',
}
changes=[]
for directory in sorted((ROOT/'specs').iterdir()):
    if not directory.is_dir() or not directory.name.startswith(prefixes): continue
    active = [directory/n for n in ('spec.md','plan.md','tasks.md','data-model.md','research.md') if (directory/n).exists()]
    active += list((directory/'contracts').glob('*.md'))
    for p in active:
        text=p.read_text(encoding='utf-8-sig')
        original=text
        for before,after in replacements.items():
            if before in text:
                changes.append({'file':p.relative_to(ROOT).as_posix(),'old':before,'new':after})
                text=text.replace(before,after)
        if text!=original: p.write_text(text,encoding='utf-8')
    for name in ('spec.md','plan.md','tasks.md'):
        p=directory/name
        if not p.exists(): continue
        text=p.read_text(encoding='utf-8-sig')
        if '## 新016直接相关增量（2026-10-06）' in text: continue
        increment='\n\n## 新016直接相关增量（2026-10-06）\n\n'
        increment+='本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。\n'
        if directory.name.startswith('006-'):
            increment+='前端独立需求见[public-tray-flow-016合同](contracts/public-tray-flow-016.md)：顶部右上独立示教按钮/弹窗、异常选择及必要状态/结束原因。授权范围保持现有导航/其他控件，原型归档不改。\n'
        if name=='tasks.md':
            increment+='\n- [ ] T016-I01 定向同步与消费本功能直接相关公共配置/观察/处置/下料边界，产物以新016 tasks T002及对应共同代码任务追踪；原历史编号和勾选不改。\n'
        p.write_text(text+increment,encoding='utf-8')
        changes.append({'file':p.relative_to(ROOT).as_posix(),'old':'未登记新016义务','new':'新增直接相关合同/任务追溯'})

p=ROOT/'README.md'
text=p.read_text(encoding='utf-8-sig')
for before,after in replacements.items(): text=text.replace(before,after)
p.write_text(text,encoding='utf-8')
(FEATURE/'document-sync-changes.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'changed_rules':len(changes),'files':len({x['file'] for x in changes})},ensure_ascii=False))
