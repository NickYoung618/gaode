"""Register reviewed 016 responsibilities and finite semantic fields; no scanner exclusions."""
from pathlib import Path
import hashlib,json
root=Path(__file__).resolve().parents[1]
directory=root/'backend/tests/Gaode.Rules.Tests/Architecture'
p=directory/'009-boundary-inventory.json';d=json.loads(p.read_text(encoding='utf-8-sig'))
entries={e['path']:e for e in d['files']}
review={
 'backend/src/Gaode.Application/Configuration/PublicPositionTeaching.cs':'business',
 'backend/src/Gaode.Application/Station01/TrayAnomalyDecisionService.cs':'business',
 'backend/src/Gaode.Host/Api/TrayAnomalyChoiceApiRequest.cs':'business',
 'backend/src/Gaode.Infrastructure/Persistence/Migrations/PublicTrayEnd.cs':'infrastructure-consumer',
 'backend/tests/Gaode.Contracts.Tests/Station01/PublicTrayDecision016Tests.cs':'business-test',
 'backend/tests/Gaode.Contracts.Tests/Station01/PublicPosition016Tests.cs':'business-test',
 'backend/tests/Gaode.Contracts.Tests/Workflow/PublicTrayAffected016Tests.cs':'business-test',
 'backend/tests/Gaode.Integration.Tests/Station01/PublicTrayFlow016Tests.cs':'business-test',
 'backend/tests/Gaode.Integration.Tests/Storage/PublicTrayUpgrade016Tests.cs':'infrastructure-test',
 'frontend/src/public-tray-flow.js':'business-ui',
 'frontend/tests/us1/public-tray-members-016.test.ts':'business-ui-test',
 'scripts/016-sync-documents.py':'fixture-or-packaging-helper',
 'scripts/016-edit-frontend.py':'fixture-or-packaging-helper',
 'scripts/016-create-test-inputs.py':'fixture-or-packaging-helper',
 'scripts/016-prototype-manifest.py':'fixture-or-packaging-helper',
 'scripts/016-register-boundaries.py':'fixture-or-packaging-helper',
 'scripts/016-browser.py':'business-ui-test',
 'scripts/016-document-sync-matrix.py':'fixture-or-packaging-helper',
 'scripts/verify-016-public-tray-flow.py':'verification-orchestration',
 'scripts/016-package-delivery.py':'fixture-or-packaging-helper',
 'scripts/Extend-E-Volume.ps1':'tooling',
 'specs/006-frontend-station01-console/contracts/public-tray-flow-016.md':'business-contract',
 'specs/016-public-preparation-tray-check-unload/contracts/public-tray-flow.md':'business-contract',
}
for name,role in review.items():
    path=root/name
    if name not in entries:
        entry=dict(path=name,role=role,language=path.suffix[1:],initialSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            classificationBasis='016 reviewed public configuration / semantic tray identity / genuine movement and completion / exact authorized UI / isolated Test evidence. No protocol decoding, fabricated detection, or new exemption.',
            scanRequired=True,migrationPending=False,exemption=False)
        if name=='scripts/Extend-E-Volume.ps1':
            entry['classificationBasis']='Existing baseline offline disk maintenance tool. Registration only; original source unchanged, no execution in 016, no PLC/business decoding, and no scanner exemption.'
        d['files'].append(entry)
d['review016']={'contract':'specs/016-public-preparation-tray-check-unload/contracts/public-tray-flow.md','newFiles':list(review),'exemptionsAdded':False}
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
p=directory/'009-public-shapes.json';d=json.loads(p.read_text(encoding='utf-8-sig'));shapes=d['allowedPublicShapes']
extra={
 'Gaode.Application.Ports.IPublicConfiguration':['SavePublicPositions'],
 'Gaode.Application.Ports.DetectionPortResult':['PosePending','EndBasisReference'],
 'Gaode.Application.Ports.PosePendingHandling':['ObjectId','PhysicalSlotIndex','ObservationId','ObservationReference','DetectionState','Reason'],
 'Gaode.Application.Recipes.RecipeMember':['CellId','PhysicalSlotIndex'],
 'Gaode.Domain.Station01.TraySlotObservation':['CellId','Region','Row','Column','HasPositionIdentity'],
 'Gaode.Domain.Station01.TrayObservation':['SchemaVersion','MappingSourceReference','ExpectedPhysicalSlotIndices','HasCompleteCoverage','IsEmptyTray','HasSamePhysicalMapping'],
 'Gaode.Domain.Station01.SortingActionPlan':['IsPosePending','ObservationReference','DetectionState'],
 'Gaode.Domain.Station01.SlotStateProjection':['DetectionState','PhysicalDisposition','SortingEvidenceRef'],
 'Gaode.Domain.Station01.RunMovementProjection':['IsPosePending','DetectionState','PhysicalDisposition','Reason'],
 'Gaode.Domain.Station01.TrayEndReason':['NormalCompletion','ManualIntervention','EmptyTray'],
 'Gaode.Domain.Station01.TrayAnomalyItem':['PhysicalSlotIndex','CellId','Region','Row','Column','Type','Reason'],
 'Gaode.Domain.Station01.TrayAnomalyDecisionProjection':['DecisionId','RunId','ObservationId','CheckRound','CreatedUtc','DeadlineUtc','Items','State','Choice','ChoiceSource','OperatorId','EvidenceReference'],
}
for name,members in extra.items():shapes[name]=sorted(set(shapes.get(name,[]))|set(members))
shapes.pop('Gaode.Application.Ports.PosePendingDisposition',None)
d['review016']={'contract':'specs/016-public-preparation-tray-check-unload/contracts/public-tray-flow.md','fields':extra,
 'history':'Missing old fields stay unknown; old recipe-definition/4 and completion payloads readable; new writes use recipe-definition/5 and tray-end/2; no raw signal fields exposed.'}
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Reviewed finite 016 files and public semantic fields registered')
