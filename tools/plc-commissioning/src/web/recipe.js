'use strict';
const $=s=>document.querySelector(s), esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let state=null,token='',busy=false,editing=false,profile=null,selectedStep=null,lastReceived=0,lastClearVerified=false;
const number=v=>v==null?'—':Number.isFinite(v)?String(Number(v.toFixed(4))):String(v);
const statusNames={queued:'等待',running:'执行中',waiting:'等待确认',paused:'已暂停',completed:'完成',skipped:'已跳过',failed:'失败',aborted:'已结束'};
const names={2000:'R启动',2001:'X启动',2002:'Y启动',2003:'检测Z启动',2004:'扫码Z启动',2005:'抓取Z启动',2006:'PC就绪',2007:'PC启动',2008:'软停止',2009:'系统复位',2011:'PC心跳应答',2012:'目标面编号',2014:'翻面/放回指令',2016:'公共取放料指令',2018:'抓手选择',2024:'X目标',2028:'Y目标',2032:'检测Z目标',2036:'扫码Z目标',2040:'抓取Z目标',2048:'现场型号原码',6015:'PLC就绪',6016:'PLC自动模式',6036:'通信报警',6038:'PLC心跳请求',6040:'X到位反馈',6042:'Y到位反馈',6044:'检测Z到位反馈',6046:'扫码Z到位反馈',6048:'抓取Z到位反馈',6050:'翻面反馈',6052:'放回原槽反馈',6054:'公共取放料反馈',6062:'实际选用抓手'};
function fresh(){return state?.fresh&&performance.now()-lastReceived<3000;}
function point(mb){return state?.points.find(p=>p.mb===mb);}
function value(mb){const p=point(mb);return fresh()&&state.values[p?.id]?.quality==='Good'?state.values[p.id].value:null;}
function toast(text){$('#toast').textContent=text;$('#toast').hidden=false;setTimeout(()=>$('#toast').hidden=true,5000);}
async function call(name,data={}){if(busy)return;busy=true;try{const response=await fetch('/api/'+name,{method:'POST',headers:{'Content-Type':'application/json','X-Console-Token':token},body:JSON.stringify(data)});const result=await response.json();if(!response.ok)throw Error(result.error);toast(result.message||'已受理');await refresh();}catch(e){toast(e.message);}finally{busy=false;render();}}
async function refresh(){try{const r=await fetch('/api/state',{cache:'no-store'});state=await r.json();lastReceived=performance.now();if(!profile){profile=structuredClone(state.recipe.profile);$('#host').value=state.config.host;$('#port').value=state.config.port;buildEditor();}if(!editing)profile=structuredClone(state.recipe.profile);render();}catch(e){$('#network').textContent='本地服务不可用：'+e.message;}}
function meaning(mb,before,after){
 if([2001,2002,2003,2004,2005].includes(mb))return after===1?'请求PLC启动本轴；读回1不等于已经运动':'启动请求已释放，等待下次新请求';
 if([6040,6042,6044,6046,6048].includes(mb))return ({0:'PLC反馈运动中；本次运动证据',1:'PLC反馈到位；实际坐标进入±0.1容差后清启动并继续',2:'PLC反馈运动超时'})[after]||'反馈状态待核对';
 if(mb===2014)return ({0:'翻面/放回指令清零',1:'请求PLC翻到目标面；翻面Z由PLC内部执行',2:'请求PLC放回原槽'})[after]||'未知指令';
 if(mb===2016)return ({0:'公共取放料指令清零',1:'请求取料',2:'请求放料'})[after]||'未知指令';
 if(mb===6050)return ({0:'翻面空闲',1:'本次翻面执行中',2:'PLC返回翻面完成',3:'翻面失败，停止推进'})[after]||'未知反馈';
 if(mb===6052)return ({0:'放回空闲',1:'本次放回执行中',2:'PLC返回放回完成',3:'放回失败，停止推进'})[after]||'未知反馈';
 if(mb===6054)return ({0:'公共取放料空闲',1:'PLC返回取料成功',2:'PLC返回放料完成',3:'抓取空/失败，停止推进'})[after]||'未知反馈';
 if(mb===2012)return '目标面设为第'+after+'面';if(mb===2048)return '现场确认的型号REAL原码，不是字母ASCII';
 if(mb===2018)return '请求选用抓手'+after;if(mb===6062)return 'PLC确认当前有效抓手'+after;
 if([2024,2028,2032,2036,2040].includes(mb))return '设置'+names[mb]+'坐标为'+number(after)+'；启动另发';
 if(mb===2009)return after===1?'复位保持为1，需核对现场复位逻辑':'复位请求已释放';
 if(mb===6038||mb===2011)return '心跳请求/同值应答；不代表设备动作成功';
 return point(mb)?.codes?.find(c=>c.value===after)?.label||point(mb)?.description||'保留原信号值';
}
function render(){if(!state||!profile)return;const run=state.recipe.run,w=state.recipe.wait,f=fresh();
 const hb=state.heartbeat;
 $('#heartbeatRequest').textContent=f&&[0,1].includes(hb.lastRequest)?hb.lastRequest:'—';
 $('#heartbeatResponse').textContent=f&&hb.enabled&&[0,1].includes(hb.lastResponse)?hb.lastResponse:'—';
 $('#heartbeatState').textContent=!f?'数据过期':!hb.enabled?'未启用':({following:'同值应答中',waiting:'等待请求',no_edge:'心跳未变化',failed:'应答失败',invalid:'请求无效'})[hb.state]||hb.state;
 $('#heartbeatWindow').classList.toggle('stale',!f||!hb.enabled);
 $('#heartbeatWindow').classList.toggle('alarm',f&&hb.enabled&&['no_edge','failed','invalid'].includes(hb.state));
 $('#safeHint').textContent=`XY范围0～100；按配方.docx执行XY握手；仅A/B/C/D在XY完成后启动检测Z=5，不附加扫码Z、抓取Z或退Z动作；到位容差±${profile.positionTolerance??0.1}；到位=1且坐标匹配即可继续，不强制等待运动中0。`;
 $('#version').textContent='v'+state.version;$('#network').textContent=f?'PLC正在应答 · '+(state.localEndpoint?.join(':')||'')+' → '+state.config.host:state.error||'未连接PLC';
 $('#error').hidden=!state.error;$('#error').textContent=state.error;
 const hints=[];if(f){if(value(2009)===1)hints.push('系统复位MB2009仍为1');if(value(2006)===0)hints.push('PC就绪MB2006为0，核对PLC使能逻辑');if(value(6020)===1||value(6035)===1)hints.push('PLC急停/故障，配方不能推进');}
 if(profile.modelCode==null)hints.push('型号REAL原码尚未填写，执行到翻面步骤将等待填写');if(!profile.flowTestOnly&&profile.grabId==null)hints.push('抓手编号尚未填写，NG/Pending分拣时将等待填写');
 $('#notice').hidden=!hints.length;$('#notice').textContent=hints.join('；');
 $('#mode').textContent=profile.cameraMode==='simulated'?'流程测试 · 模拟相机5秒 · 非真实检测结果':'真实现场 · 相机结果由操作员确认';
 $('#positions').innerHTML=[['X',6064],['Y',6076],['检测Z',6084],['扫码Z',6088],['抓取Z',6092]].map(([n,mb])=>`<div><small>${n} · ${f?'实时反馈':'最后一次数据'}</small><b>${number(f?value(mb):state.values[point(mb)?.id]?.value)}</b></div>`).join('');
 $('#runState').textContent=run?(statusNames[run.state]||run.state):'未启动';$('#currentTitle').textContent=run?'第'+run.step+'步 · '+profile.steps[run.step-1].name:'准备开始新的一盘';
 $('#waitText').textContent=w?.message||run?.reason||'人工放料后点击开始；F扫码和下料仅XY，所有运动等待本次真实反馈。';
 $('#checkpoint').hidden=!(run?.state==='waiting'&&w);$('#barcodeLabel').hidden=w?.kind!=='scan';$('#outcomeLabel').hidden=!['pose','poseReview','scan','photo','result'].includes(w?.kind);
 $('#confirm').textContent=w?.kind==='load'?'已放盘且设备就绪，继续':w?.kind==='finish'?'已取盘，结束本盘':w?.kind==='parameters'?'参数已保存，继续':'确认并继续';
 const active=run&&['running','waiting','paused'].includes(run.state);$('#start').disabled=busy||!f||active;$('#gripperCheck').disabled=busy||!f||active;$('#pause').disabled=busy||!active||run?.state==='paused';$('#resume').disabled=busy||run?.state!=='paused'||!f;$('#abort').disabled=busy||!active;$('#clearStarts').disabled=busy||!f||active;$('#confirm').disabled=busy||!f;
 $('#connect').disabled=busy||state.tcpConnected;$('#disconnect').disabled=busy||!state.tcpConnected;$('#heartbeat').disabled=busy||!f;$('#heartbeat').textContent=state.heartbeat.enabled?'停用心跳':'启用心跳';
 $('#barcodeState').textContent='扫码绑定一次：'+(run?.barcode||'尚未绑定');$('#resultState').textContent='本盘结果：'+(run?.result||'待汇总')+(run?.simulated?'（流程测试，非真实检测结果）':'');
 $('#steps').innerHTML=profile.steps.map(s=>{const record=run?.steps[s.id-1];return `<div class="step ${run?.step===s.id?'active':''} ${record?.state||''}" data-step="${s.id}"><span class="number">${s.id}</span><div><b>${esc(s.name)}</b><small>${s.x!=null?'X='+number(s.x)+'，Y='+number(s.y):s.kind==='flip'?'目标面=2，翻面Z由PLC执行':s.kind==='unload'?'放回完成后清翻面指令':s.kind==='result'?'流程测试OK · 工件留原槽 · 不分拣':''}${s.z!=null?'，'+({ZScan:'扫码Z',ZCamera:'检测Z',ZGrab:'抓取Z'})[s.zAxis]+'='+number(s.z):''}</small></div><span class="step-status">${statusNames[record?.state]||''}</span></div>`;}).join('');
 document.querySelectorAll('[data-step]').forEach(el=>el.onclick=()=>{selectedStep=Number(el.dataset.step);drawMap();toast(profile.steps[selectedStep-1].name+'：'+(profile.steps[selectedStep-1].x!=null?'X='+profile.steps[selectedStep-1].x+' / Y='+profile.steps[selectedStep-1].y:'见PLC指令与完成条件'));});
 const signals=(state.signalTimeline||run?.signals||[]).filter(e=>point(e.mb)&&![2011,6038].includes(e.mb));
 const items=signals.slice(-200).reverse();
 $('#changeCount').textContent=signals.length+'条真实信号记录';
 $('#signalFeed').innerHTML=items.map(e=>{const p=point(e.mb),reset=[2000,2001,2002,2003,2004,2005].includes(e.mb)&&e.value===0,action=state.actions.find(a=>a.actionId===e.actionId),completed=reset&&action?.plcCompleted===true;
 return `<article class="change ${reset?'reset-change':''}"><time>${esc(e.at?.slice(11,19))}${e.recipeStep?' · 第'+e.recipeStep+'步':''}</time><span class="direction">${p.direction==='PC->PLC'?'上位机 → PLC':'PLC → 上位机'} · ${e.kind==='READBACK'?(e.matched?'写入已读回':'读回不符'):'实读变化'}</span><div class="value-change">${esc(p.logicalName||p.name)} · %MB${e.mb}：${number(e.before)} → ${number(e.value)}</div><div class="meaning">${reset?(completed?'本次运动完成后启动复位（清零），PLC已读回0':e.kind==='READBACK'?'启动信号清零，PLC已读回0；不代表运动完成':'启动信号已变为0'):esc(meaning(e.mb,e.before,e.value))}${e.kind==='READBACK'?' · 上次实读值→本次读回值':''}</div></article>`;}).join('')||'<p class="small">暂无真实信号变化；心跳在右上角单独显示。</p>';
 const simulations=(run?.milestones||[]).filter(e=>e.kind==='SIMULATION'||e.kind==='RECIPE_RESULT').slice(-12).reverse();
 $('#simulationFeed').innerHTML=simulations.map(e=>`<article class="change simulated"><time>${esc(e.at?.slice(11,19))}</time><span class="direction">流程测试 · 模拟，非PLC反馈</span><div>${esc(e.title)}${e.outcome?' → '+esc(e.outcome):''}</div></article>`).join('')||'<p class="small">相机每次模拟等待5秒，结果不作为真实检测结果。</p>';
 if(lastClearVerified&&f&&[2000,2001,2002,2003,2004,2005].some(mb=>value(mb)!==0)){$('#clearStatus').textContent='启动状态已有后续变化；上次清零记录不代表当前全部为0。';$('#clearStatus').className='clear-status warning';lastClearVerified=false;}$('#logPath').textContent='本地日志：'+state.logPath;drawMap();
}
function drawMap(){if(!profile)return;document.querySelectorAll('.map-key .ng,.map-key .pending').forEach(i=>i.parentElement.hidden=!!profile.flowTestOnly);const run=state?.recipe.run,sx=x=>55+x*4.6,sy=y=>485-y*4.2;let svg='<defs><marker id="arrow" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto"><path d="M0 0L6 3L0 6" fill="#9db5b4"/></marker></defs>';
 for(let i=0;i<=100;i+=10){svg+=`<path d="M${sx(i)} ${sy(0)}V${sy(100)}M${sx(0)} ${sy(i)}H${sx(100)}" fill="none" stroke="#e8efed"/><text x="${sx(i)}" y="510" text-anchor="middle" font-size="10" fill="#7b9695">${i}</text><text x="35" y="${sy(i)+4}" text-anchor="end" font-size="10" fill="#7b9695">${i}</text>`;}
 svg+='<text x="532" y="491" font-size="12" fill="#6b8288">X</text><text x="48" y="49" font-size="12" fill="#6b8288">Y</text>';
 const points=profile.steps.filter(s=>s.x!=null);let prev=null;for(const s of points){if(prev)svg+=`<path d="M${sx(prev.x)} ${sy(prev.y)}L${sx(s.x)} ${sy(s.y)}" fill="none" stroke="${s.id===(selectedStep||run?.step)?'#0b8c80':'#9db5b4'}" stroke-width="${s.id===(selectedStep||run?.step)?3:1.4}" stroke-dasharray="5 5" marker-end="url(#arrow)"/>`;prev=s;}
 const sort=profile.sorting;for(const [x,y,c] of (profile.flowTestOnly?[]:[[sort.ngX,sort.ngY,'#b94040'],[sort.pendingX,sort.pendingY,'#ad7112']]))svg+=`<path d="M${sx(sort.sourceX)} ${sy(sort.sourceY)}H${sx(x)}V${sy(y)}" fill="none" stroke="${c}" stroke-width="2" stroke-dasharray="4 4"/><rect x="${sx(x)-6}" y="${sy(y)-6}" width="12" height="12" rx="2" fill="${c}"/>`;
 const groups=new Map();for(const s of points){const k=s.x+','+s.y;if(!groups.has(k))groups.set(k,[]);groups.get(k).push(s);}
 for(const ss of groups.values()){const s=ss[0],current=ss.some(p=>p.id===(selectedStep||run?.step)),label=ss.map(p=>p.camera||({load:'上/下料',pose:'3D',scan:'F扫码',position:'翻面/原槽',poseReview:'复查',finish:'上/下料'})[p.kind]||p.name).filter((v,i,a)=>a.indexOf(v)===i).join(' / ');svg+=`<circle cx="${sx(s.x)}" cy="${sy(s.y)}" r="${current?7:5}" fill="${current?'#087f76':'white'}" stroke="#087f76" stroke-width="2"><title>${esc(ss.map(p=>p.id+'. '+p.name).join('；'))} (${s.x},${s.y})</title></circle><text x="${sx(s.x)}" y="${sy(s.y)-13}" font-size="11" text-anchor="${s.x>80?'end':s.x<10?'start':'middle'}" fill="#2d6165">${esc(label)}</text>`;}
 const trail=run?.trail||[];if(trail.length>1)svg+=`<polyline points="${trail.map(p=>sx(p.x)+','+sy(p.y)).join(' ')}" fill="none" stroke="#087f76" stroke-width="3" opacity=".7"/>`;
 const x=value(6064),y=value(6076);if(x!=null&&y!=null&&x>=0&&x<=100&&y>=0&&y<=100)svg+=`<circle cx="${sx(x)}" cy="${sy(y)}" r="11" fill="#087f76" opacity=".15"/><circle cx="${sx(x)}" cy="${sy(y)}" r="4" fill="#087f76"><title>实时反馈 X=${x} Y=${y}</title></circle>`;
 $('#pathMap').innerHTML=svg;
}
function buildEditor(){const p=profile;$('#gripperMembers').value=(p.gripperMembers||[]).map(m=>[m.memberId,m.material,m.grabId??''].join(',')).join('\n');$('#grabId').closest('label').hidden=!!p.flowTestOnly;$('#sortingEditor').hidden=!!p.flowTestOnly;$('#sortingEditor').previousElementSibling.hidden=!!p.flowTestOnly;$('#sortingEditor').previousElementSibling.previousElementSibling.hidden=!!p.flowTestOnly;$('#positionTolerance').value=p.positionTolerance??0.1;$('#cameraMode').value=p.cameraMode;$('#modelCode').value=p.modelCode??'';$('#grabId').value=p.grabId??'';for(const a of ['ZCamera','ZScan','ZGrab']){$('#safe_'+a).value=p.safeZ[a]??2;$('#safe_'+a).closest('label').hidden=!!p.flowTestOnly;}
 $('#pointEditor').innerHTML=p.steps.map(s=>`<tr><td>${s.id} · ${esc(s.name)}</td>${['x','y','z'].map(k=>`<td>${s[k]!=null?`<input id="step_${s.id}_${k}" type="number" min="0" max="${k==='z'?10:100}" step="any" value="${s[k]}">`:'—'}</td>`).join('')}</tr>`).join('');
 const labels={sourceX:'原槽X',sourceY:'原槽Y',ngX:'NG去向X',ngY:'NG去向Y',pendingX:'Pending去向X',pendingY:'Pending去向Y',pickZ:'公共取料Z',placeZ:'公共放料Z',transferZ:'公共转移Z'};
 $('#sortingEditor').innerHTML=Object.entries(p.sorting).map(([k,v])=>`<label>${labels[k]}<input id="sort_${k}" type="number" step="any" min="0" max="${k.endsWith('Z')?10:100}" value="${v}"></label>`).join('');
 $('#simulationEditor').innerHTML='<label>模拟扫码字符串<input id="sim_barcode" value="'+esc(p.simulation?.barcode||'SIM-翻面件-001')+'"></label>'+['pose','poseReview','A','B','C','D'].map(k=>`<label>${({pose:'3D初次姿态',poseReview:'3D姿态复查'})[k]||k+'拍照处理'}<select id="sim_${k}"><option value="OK">OK / 正常</option><option value="NG">NG / 姿态异常</option><option value="Pending">Pending / 待复核</option></select></label>`).join('');
 for(const k of ['pose','poseReview','A','B','C','D']){$('#sim_'+k).value=p.simulation?.[k]||'OK';$('#sim_'+k).disabled=!!p.flowTestOnly;}$('#cameraMode').disabled=!!p.flowTestOnly;
}
$('#edit').onclick=()=>{editing=!editing;$('#settings').hidden=!editing;if(editing){profile=structuredClone(state.recipe.profile);buildEditor();$('#settings').scrollIntoView({behavior:'smooth'});}};
$('#save').onclick=()=>{try{const p=structuredClone(profile);p.positionTolerance=Number($('#positionTolerance').value);p.cameraMode=$('#cameraMode').value;p.modelCode=$('#modelCode').value.trim()===''?null:Number($('#modelCode').value);p.gripperMembers=$('#gripperMembers').value.split(/\r?\n/).filter(line=>line.trim()).map(line=>{const parts=line.split(/[,，]/).map(x=>x.trim());if(parts.length!==3)throw Error('每行填写：成员名称,零件类型,夹爪编号');return {memberId:parts[0],material:parts[1],grabId:parts[2]===''?null:Number(parts[2])};});p.grabId=$('#grabId').value===''?null:Number($('#grabId').value);for(const a of ['ZCamera','ZScan','ZGrab']){if($('#safe_'+a).value.trim()==='')throw Error('请填写三个确定的Z安全高度');p.safeZ[a]=Number($('#safe_'+a).value);}for(const s of p.steps)for(const k of ['x','y','z'])if(s[k]!=null)s[k]=Number($('#step_'+s.id+'_'+k).value);for(const k of Object.keys(p.sorting))p.sorting[k]=Number($('#sort_'+k).value);p.simulation={barcode:$('#sim_barcode').value};for(const k of ['pose','poseReview','A','B','C','D'])p.simulation[k]=$('#sim_'+k).value;profile=p;drawMap();call('recipe-settings',{profile:p});}catch(e){toast(e.message);}};
$('#connect').onclick=()=>call('connect',{host:$('#host').value,port:Number($('#port').value)});$('#disconnect').onclick=()=>call('disconnect');$('#heartbeat').onclick=()=>call('heartbeat',{enabled:!state.heartbeat.enabled});$('#start').onclick=()=>call('recipe-start');$('#gripperCheck').onclick=()=>{const saved=(state.recipe.profile.gripperMembers||[]).map(m=>[m.memberId,m.material,m.grabId??''].join(',')).join('\n');if($('#gripperMembers').value.trim()!==saved){toast('请先保存成员夹爪配置');return;}call('recipe-gripper-check');};$('#pause').onclick=()=>call('recipe-pause');$('#resume').onclick=()=>call('recipe-resume');$('#abort').onclick=()=>call('recipe-abort');$('#confirm').onclick=()=>call('recipe-confirm',{outcome:$('#outcome').value,barcode:$('#barcode').value});
(async()=>{try{token=(await(await fetch('/api/session')).json()).token;await refresh();}catch(e){toast(e.message);}setInterval(refresh,500);})();

$('#clearStarts').onclick=async()=>{
 if(busy||!fresh())return;
 if(state.recipe.run&&['running','waiting','paused'].includes(state.recipe.run.state)){toast('请先结束本盘，再清零启动信号');return;}
 busy=true;lastClearVerified=false;render();const output=$('#clearStatus'),results=[];
 output.className='clear-status';output.textContent='正在逐项清零启动字节并读回，心跳继续保持……';
 try{
  for(const [axis,label,mb] of [['R','R',2000],['X','X',2001],['Y','Y',2002],['ZCamera','检测Z',2003],['ZScan','扫码Z',2004],['ZGrab','抓取Z',2005]]){
   try{
    const response=await fetch('/api/clear',{method:'POST',headers:{'Content-Type':'application/json','X-Console-Token':token},body:JSON.stringify({axis})});const reply=await response.json();
    if(!response.ok)throw Error(reply.error||'请求失败');
    if(!reply.result?.writeResponded||!reply.result?.readbackMatched||reply.result.readback!==0)throw Error('PLC写应答或读回0未确认');
    results.push({label,mb,ok:true});
   }catch(error){results.push({label,mb,ok:false,error:error.message});}
   output.textContent=results.map(r=>r.label+'：'+(r.ok?'PLC应答、读回0 ✓':'失败 '+r.error)).join('；');
  }
  const after=Date.now(),deadline=after+Math.max(3500,state.config.pollMs*3);let freshRead=false;
  while(Date.now()<deadline){
   await refresh();freshRead=fresh()&&results.every(r=>{const p=point(r.mb);return (state.values[p.id]?.receivedEpoch||0)*1000>=after;});
   if(freshRead)break;await new Promise(resolve=>setTimeout(resolve,150));
  }
  const verified=results.every(r=>r.ok)&&freshRead&&results.every(r=>value(r.mb)===0);
  lastClearVerified=verified;output.className='clear-status '+(verified?'verified':'warning');
  output.textContent=(verified?'本次已确认六轴启动均为0（'+new Date().toLocaleTimeString()+'）。':'未全部确认清零，请查看失败项；不要直接重新启动。')+' '+results.map(r=>r.label+'：'+(r.ok?'PLC应答、读回0':'失败 '+r.error)).join('；')+(freshRead?'':'；最终监视数据尚未更新，不能确认全部为0。');
 }catch(error){output.className='clear-status warning';output.textContent='清零未完成：'+error.message;}
 finally{busy=false;render();}
};
