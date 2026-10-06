import {test} from 'node:test';
import assert from 'node:assert/strict';
import {reviewRenderedEditor} from '../../scripts/recipe-layout-review.mjs';
test('render review rejects technical controls, a nested model editor and aliased cells',()=>{
 const view={section:'points',tabs:['基础信息','坐标配置','检查保存'],labels:['X (mm)','Y (mm)','检测Z (mm)','曝光 (µs)','增益','光源亮度 (%)'],headings:['A相机拍照坐标'],buttons:[],cells:Array.from({length:100},(_,i)=>`r${Math.floor(i/10)+1}:c${i%10+1}`),pointCards:2,genericEditors:0};
 assert.equal(reviewRenderedEditor(view).passed,true);
 assert.equal(reviewRenderedEditor({...view,labels:[...view.labels,'配置键']}).passed,false);
 assert.equal(reviewRenderedEditor({...view,pointCards:0,genericEditors:1,labels:['RecipeId']}).passed,false);
 assert.equal(reviewRenderedEditor({...view,emptyEditable:1}).passed,false);
 assert.equal(reviewRenderedEditor({...view,cells:[...view.cells].reverse()}).passed,false);
 assert.equal(reviewRenderedEditor({...view,cells:view.cells.map((_id,i)=>i===1?'r1:c1':_id)}).passed,false);
});
test('approved navigation review rejects displaced selectors, per-part mechanical handling and aliased captures',()=>{
 const view={section:'points',tabs:['基础信息','坐标配置','检查保存'],labels:['X (mm)'],headings:['成员'],buttons:[],cells:Array.from({length:100},(_,i)=>`r${Math.floor(i/10)+1}:c${i%10+1}`),pointCards:2,genericEditors:0,
 navigation:{objectButtonsOutsideDetail:false,faceButtonsOutsideDetail:false,objectHeading:'成员',expectedObjectHeading:'成员',objectCount:2,faceCount:2,activeObjectCount:1,activeFaceCount:1,wholeMechanical:false,photo:true,cards:[{material:'part',stageId:'stage:1',pointRef:'same',camera:'A'},{material:'part',stageId:'stage:2',pointRef:'same',camera:'A'}]}};
 assert.equal(reviewRenderedEditor(view).passed,true);
 for(const navigation of [{...view.navigation,objectButtonsOutsideDetail:true},{...view.navigation,faceButtonsOutsideDetail:true},
 {...view.navigation,wholeMechanical:true},{...view.navigation,cards:[view.navigation.cards[0],view.navigation.cards[0]]}])
   assert.equal(reviewRenderedEditor({...view,navigation}).passed,false);
});
