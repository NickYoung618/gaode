from pathlib import Path
p=Path('frontend/src/pages/a.html')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('      <button id="btnRecipe"','      <button id="btnPositions" class="btn px-3 py-1.5 rounded-md bg-cyan-500/20 text-cyan-100 border border-cyan-500/50 text-sm">示教／公共位置</button>\n      <button id="btnRecipe"',1)
dialogs='''<dialog id="publicPositionsDialog" style="background:#0f172a;color:#e2e8f0;padding:24px;border:1px solid #64748b;border-radius:10px"></dialog>
<dialog id="trayAnomalyDialog" style="background:#0f172a;color:#e2e8f0;padding:24px;border:1px solid #64748b;border-radius:10px"></dialog>
'''
s=s.replace('</body>',dialogs+'</body>',1)
# Runtime scripts are injected by the existing offline build, so register the asset there.
s=s.replace('  <main class=','  <p id="trayEndState" class="text-xs px-3 text-slate-300"></p>\n  <main class=',1)
p.write_text(s,encoding='utf-8')
p=Path('frontend/src/runtime.js'); s=p.read_text(encoding='utf-8-sig')
s=s.replace('  function render(status, run, evidence) {','  const publicTrayFlow = window.GaodePublicTrayFlow?.mount({ document, request, post, refresh, runId: () => runId });\n  function render(status, run, evidence) {\n    publicTrayFlow?.render(run);')
p.write_text(s,encoding='utf-8')
p=Path('frontend/src/recipe-authoring.js'); s=p.read_text(encoding='utf-8-sig').replace('recipe-definition/4','recipe-definition/5')
s=s.replace('slot: position.physicalSlotIndex, member: member.material,',"slot: definition.unitKind === 'looseGroup' ? member.physicalSlotIndex : position.physicalSlotIndex, member: member.material,")
s=s.replace('cellId: position.cellId, name:',"cellId: definition.unitKind === 'looseGroup' ? member.cellId : position.cellId, name:")
anchor="          detail.append(node('h2', cell ? cellLabel(d,cell) : item.name));"
s=s.replace(anchor,anchor+'''
          if (d.unitKind === 'looseGroup' && item.member) {
            input(detail,'成员物理槽号',item.slot,v => session.edit(x => {
              const position=x.positions.find(p=>p.slotId===item.slotId), member=position.members.find(m=>m.material===item.member);
              member.physicalSlotIndex=v;
              for(const c of x.executionPositions[item.slotId].members[item.member].coordinates)c.physicalSlotIndex=v;
            }));
            const member=x => x.positions.find(p=>p.slotId===item.slotId).members.find(m=>m.material===item.member);
            const field=node('label','成员格位（r行:c列） '), text=node('input');text.value=item.cellId || '';
            text.onchange=()=>session.edit(x=>member(x).cellId=text.value);field.append(text);detail.append(field);
          }''')
p.write_text(s,encoding='utf-8')
