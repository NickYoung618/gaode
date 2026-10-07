// 012 T043: review actual rendered controls/positions, independently of resource hashes.
export function reviewRenderedEditor(view) {
  const errors=[];
  if(JSON.stringify(view.tabs)!==JSON.stringify(['基础信息','坐标配置','检查保存']))errors.push('ThreeStepsChanged');
  const forbidden=/配置来源|路线引用|Pattern|配置键|阶段引用|内容摘要|准入依据|算法结果合同|OK目标|OK放置位置|是否回原槽|OK处理方式|采集配置管理|参数模板|ROI|光源通道|稳定等待|算法配置/;
  if([...view.labels,...view.headings,...view.buttons].some(s=>forbidden.test(s)))errors.push('UnauthorizedOperatorControl');
  const identities=new Set(view.cells);const expectedCells=Array.from({length:100},(_,i)=>`r${Math.floor(i/10)+1}:c${i%10+1}`);
  if(view.section!=='review'&&(view.cells.length!==100||identities.size!==100||JSON.stringify(view.cells)!==JSON.stringify(expectedCells)))errors.push('ActualMatrixReplaced');
  if(view.emptyEditable>0)errors.push('EmptyCellEditable');
  if(view.genericEditors>0)errors.push('ModelEditorForbidden');
  if(view.section==='points') {
    if(view.pointCards<1)errors.push('DirectPointCardsMissing');
    const allowed=['X (mm)','Y (mm)','检测Z (mm)','扫码Z (mm)','抓取Z (mm)','曝光 (µs)','增益','光源亮度 (%)','R目标角度 (°)'];
    if(view.labels.some(label=>!allowed.includes(label)))errors.push('ExtraPointField');
    if(view.navigation) {
      const n=view.navigation;
      if(n.objectButtonsOutsideDetail)errors.push('ObjectNavigationOutsideDetail');
      if(n.faceButtonsOutsideDetail)errors.push('FaceNavigationOutsideDetail');
      if(n.objectCount && n.objectHeading!==n.expectedObjectHeading)errors.push('WrongObjectNavigationKind');
      if(n.wholeMechanical && (n.objectCount || n.faceCount))errors.push('AssemblyPartHandlingSelector');
      if(n.activeObjectCount!==Math.min(n.objectCount,1) || n.activeFaceCount!==Math.min(n.faceCount,1))errors.push('AmbiguousNavigationSelection');
      if(n.photo && (!n.objectCount || !n.faceCount))errors.push('ObjectFaceNavigationMissing');
      if(n.cards.some(c=>c.camera && (!c.material || !c.stageId || !c.pointRef)))errors.push('CaptureIdentityMissing');
      if(new Set(n.cards.filter(c=>c.camera).map(c=>[c.material,c.stageId,c.pointRef,c.camera].join('/'))).size!==n.cards.filter(c=>c.camera).length)errors.push('CaptureIdentityAliased');
    }
  }
  return {passed:errors.length===0,errors};
}
export const renderedEditorExpression=`(()=>({section:document.querySelector('[data-authoring-section].active')?.dataset.authoringSection,
 tabs:[...document.querySelectorAll('[data-authoring-section]')].map(x=>x.textContent.trim()),
 labels:[...document.querySelectorAll('#recipeAuthoringForm input,#recipeAuthoringForm select')].map(x=>x.getAttribute('aria-label')||''),
 headings:[...document.querySelectorAll('#recipeAuthoringForm h2,#recipeAuthoringForm h3')].map(x=>x.textContent.trim()),
 buttons:[...document.querySelectorAll('#recipeAuthoringForm button')].map(x=>x.textContent.trim()),
 cells:[...document.querySelectorAll('#recipeAuthoringForm [data-cell-id],#recipeAuthoringForm [data-empty-cell]')].map(x=>x.dataset.cellId||x.dataset.emptyCell),
 selected:[...document.querySelectorAll('#recipeAuthoringForm [data-cell-id].area-ng,#recipeAuthoringForm [data-cell-id].area-ok,#recipeAuthoringForm [data-cell-id].area-pending')].map(x=>({cellId:x.dataset.cellId,label:x.getAttribute('aria-label')})),
 emptyEditable:document.querySelectorAll('#recipeAuthoringForm button[data-empty-cell],#recipeAuthoringForm [data-empty-cell] input').length,
 pointCards:document.querySelectorAll('.recipe-point-card').length,
 genericEditors:document.querySelectorAll('#recipeAuthoringForm textarea,#recipeAuthoringForm [contenteditable=true]').length,
 navigation:(()=>{const form=document.querySelector('#recipeAuthoringForm'),kind=form?.dataset.unitKind;
 const detail=document.querySelector('.recipe-detail');const objects=[...document.querySelectorAll('[data-object-material]')],faces=[...document.querySelectorAll('[data-local-face]')];
 if(!detail||!['looseGroup','assembledEntity'].includes(kind)||form?.dataset.pointPurpose==='NG'||form?.dataset.pointPurpose==='Pending')return null;
 return {objectButtonsOutsideDetail:objects.some(x=>!detail.contains(x)),faceButtonsOutsideDetail:faces.some(x=>!detail.contains(x)),
 objectHeading:detail.querySelector('h3')?.textContent,expectedObjectHeading:kind==='assembledEntity'?'检测部位':'成员',
 objectCount:objects.length,faceCount:faces.length,activeObjectCount:objects.filter(x=>x.getAttribute('aria-pressed')==='true').length,
 activeFaceCount:faces.filter(x=>x.getAttribute('aria-pressed')==='true').length,wholeMechanical:kind==='assembledEntity'&&['flip','sort'].includes(form.dataset.pointPurpose),
 photo:form.dataset.pointPurpose==='photo',cards:[...detail.querySelectorAll('.recipe-point-card')].map(x=>({...x.dataset}))};})()}))()`;
