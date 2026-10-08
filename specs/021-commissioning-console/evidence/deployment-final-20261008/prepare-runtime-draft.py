import hashlib
import json
from pathlib import Path
from zipfile import ZipFile
from xml.etree import ElementTree as ET

installation = Path('D:/Gaode-Station01/commissioning-021-final-1')
source = Path('D:/Gaode-PlcCommissioning-20261006/release/Gaode-PlcCommissioning-1.1.6-win-x64/config/local.json')
document = Path('D:/plc与虚拟上位机联调包/PLC与上位机通信接口协议.docx')
load = lambda path: json.loads(path.read_text(encoding='utf-8-sig'))
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest().upper()
old = load(source)
recipe = load(installation / 'data/recipe-readback.json')['recipes'][0]['definition']
assert old['realReadOrder'] == old['realWriteOrder'] == 'Cdab'
profile = dict(layoutId='confirmed-20261006-v2', confirmed=False,
    source=str(source) + ';SHA256:' + sha(source) + ';DRAFT pending field review',
    pcPduBase=old['pcBase'], plcPduBase=old['plcBase'], plcArea='HoldingRegister',
    boolByteOrder=old['boolOrder'], floatOrder=old['realReadOrder'])
with ZipFile(document) as archive:
    tree = ET.fromstring(archive.read('word/document.xml'))
ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
paragraphs = [''.join(node.text or '' for node in p.findall('.//w:t', ns))
    for p in tree.findall('.//w:p', ns)]
excerpts = []
for heading in ('1.3', '1.5', '4.2'):
    index = next(i for i, text in enumerate(paragraphs) if text.startswith(heading))
    excerpts.append(dict(section=heading, paragraphs=paragraphs[index:index+(2 if heading=='4.2' else 5)]))
report = dict(schemaVersion='commissioning-runtime-preparation/1', state='DraftNotRunnable',
    hardwareAccessed=False, sourceConfig=dict(path=str(source), sha256=sha(source)),
    recipeId=recipe['recipeId'], recipeVersion=recipe['version'],
    plcConnection=dict(host=old['host'], port=old['port'], unitId=old['unitId'],
        localNicAddress=old['sourceAddress'], feedbackFc=old['feedbackFc'],
        effectiveReadOrder=old['realReadOrder'], effectiveWriteOrder=old['realWriteOrder'],
        ignoredLegacyRealOrder=old['realOrder']),
    timingDifference=dict(originalIoTimeoutMs=int(old['timeout']*1000), formalAdapterMaximumMs=1000,
        proposalIoTimeoutMs=1000, proposalActivated=False, noAutomaticReplay=True,
        sourcePollMs=old['pollMs'], sourceActionTimeoutSeconds=old['actionTimeout']),
    sourceAxisSettings=old['axes'], sourceCalibrated=old['calibrated'],
    protocol=dict(path=str(document), sha256=sha(document), excerpts=excerpts),
    blockingSemantics=dict(
        initialRecovery='Ready_State does not explicitly prove axes stopped/reset completed; PC_Start/System_Reset sequence absent',
        stop='Protocol 1.3 describes stop/cut servo; PC.xls Soft_Stop describes automatic return',
        code='Site samples currently remain SafetyUnconfirmed; approved mapping must be implemented, not bypassed with a profile flag'),
    missingFieldBasis=['axis minimum/maximum and unit (null/empty in original)',
        'initial/recovery permission and command sequence', 'startup safe XYZ basis'],
    missingDerivedRuntimeFiles=['reviewed public configuration', 'versioned software budget',
        'reviewed PLC mechanics/field profile', 'controlled identities and desktop template/profile',
        'host settings/runtime profile'],
    existingReadyFiles=['data/recipe-readback.json', 'data/config/commissioning.json', 'config/camera.site.json'],
    originalPackageUnchanged=True)
target = installation / 'data/config-preparation'
target.mkdir(exist_ok=True)
for name, value in [('runtime-preparation.json', report), ('plc-field-profile.draft.json', profile)]:
    with (target / name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
with Path(__file__).with_name('runtime-preparation.json').open('x', encoding='utf-8') as stream:
    json.dump(report, stream, ensure_ascii=False, indent=2)
print(json.dumps(dict(prepared=str(target), effectiveRealEncoding='Cdab',
    profileConfirmed=False, hardwareAccessed=False)))
