const $=id=>document.getElementById(id);
const actionAnchor=$('live').querySelector('.section-title');
$('live').insertBefore($('axisTargets'),actionAnchor);$('live').insertBefore($('singleAxis'),actionAnchor);
const esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let cfg,state={},notes={},busy=false;
const stamp=v=>v?new Date(v).toLocaleTimeString('zh-CN',{hour12:false})+'.'+String(new Date(v).getMilliseconds()).padStart(3,'0'):'—';
function notice(text,error=false){$('notice').textContent=text;$('notice').className=error?'error':'';}
async function api(path,data){const r=await fetch('/api/'+path,data===undefined?{}:{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});const v=await r.json();if(!r.ok)throw Error(v.error||'请求失败');return v;}
async function action(fn){if(busy)return;busy=true;try{await fn();await refresh();}catch(e){notice(e.message,true);}finally{busy=false;}}
document.querySelectorAll('[data-tab]').forEach(b=>b.onclick=()=>{document.querySelectorAll('.page').forEach(p=>p.hidden=p.id!==b.dataset.tab);document.querySelectorAll('[data-tab]').forEach(x=>x.classList.toggle('active',x===b));});
const connectionKeys=['host','port','unitId','timeoutMs','pollMs'];
const mappingKeys=['pcPduBase','plcPduBase','plcArea','boolByteOrder','floatOrder','source'];
const numbers=new Set(['port','unitId','timeoutMs','pollMs','pcPduBase','plcPduBase']);
function fillConfig(){
 for(const key of connectionKeys)$('cfg-'+key).value=cfg.connection[key]??'';
 for(const key of mappingKeys)$('cfg-'+key).value=cfg.mapping[key]??'';
 for(const key of ['purpose','sourceReference','plcProgramVersion'])$('cfg-'+key).value=cfg[key]??'';
 for(const key of ['writesConfirmed','singleWriterConfirmed'])$('cfg-'+key).checked=cfg[key];
 $('cfg-confirmed').checked=cfg.mapping.confirmed;$('cfg-heartbeatConfirmed').checked=cfg.heartbeat.confirmed;
 $('advanced').value=JSON.stringify(cfg,null,2);
 $('pointConfig').innerHTML=cfg.signals.map((p,i)=>`<tr data-point="${i}"><td><b>${esc(p.label)}</b><small class="signal-id">${esc(p.id)}</small></td><td><input type="checkbox" data-key="enabled" ${p.enabled?'checked':''}></td><td><input type="checkbox" data-key="writeEnabled" ${p.writeEnabled?'checked':''} ${p.direction!=='PC->PLC'?'disabled':''}></td><td><input type="number" step="any" data-key="min" value="${esc(p.min)}" ${p.direction!=='PC->PLC'?'disabled':''}></td><td><input type="number" step="any" data-key="max" value="${esc(p.max)}" ${p.direction!=='PC->PLC'?'disabled':''}></td><td><input data-key="allowedValues" value="${esc((p.allowedValues||[]).join(','))}" ${p.direction!=='PC->PLC'?'disabled':''}></td></tr>`).join('');
 renderSignals();renderCoordinates();$('axis').innerHTML=cfg.axes.map(a=>`<option value="${esc(a.name)}">${esc(a.name)}${a.confirmed?'':'（待确认）'}</option>`).join('');axisInfo();
}
function collect(){
 const next=structuredClone(cfg);
 for(const key of connectionKeys)next.connection[key]=numbers.has(key)?($('cfg-'+key).value===''?null:Number($('cfg-'+key).value)):$('cfg-'+key).value.trim();
 for(const key of mappingKeys)next.mapping[key]=numbers.has(key)?($('cfg-'+key).value===''?null:Number($('cfg-'+key).value)):$('cfg-'+key).value.trim();
 for(const key of ['purpose','sourceReference','plcProgramVersion'])next[key]=$('cfg-'+key).value;
 for(const key of ['writesConfirmed','singleWriterConfirmed'])next[key]=$('cfg-'+key).checked;
 next.mapping.confirmed=$('cfg-confirmed').checked;next.heartbeat.confirmed=$('cfg-heartbeatConfirmed').checked;
 document.querySelectorAll('[data-point]').forEach(row=>{const p=next.signals[Number(row.dataset.point)];row.querySelectorAll('[data-key]').forEach(input=>{const key=input.dataset.key;p[key]=input.type==='checkbox'?input.checked:key==='allowedValues'?(input.value.trim()?input.value.split(',').map(v=>{if(v.trim()===''||!Number.isFinite(Number(v)))throw Error('允许枚举须用逗号分隔数值');return Number(v)}):[]):input.value===''?null:Number(input.value);});});
 return next;
}
async function save(value){await api('config',value);cfg=await api('config');fillConfig();notice('配置已保存。连接时会生成本轮不可变快照；保存不会自动写PLC。');}
$('saveConfig').onclick=$('savePoints').onclick=()=>action(()=>save(collect()));
$('reloadConfig').onclick=()=>action(async()=>{cfg=await api('config');fillConfig();notice('已重新载入保存的配置。');});
$('syncJson').onclick=()=>{try{$('advanced').value=JSON.stringify(collect(),null,2);notice('表单已复制到JSON框，尚未保存。');}catch(e){notice(e.message,true);}};
$('saveJson').onclick=()=>action(()=>save(JSON.parse($('advanced').value)));
function address(p){const base=p.direction==='PC->PLC'?cfg.mapping.pcPduBase:cfg.mapping.plcPduBase;return base==null?'待确认':base+p.mw-(p.direction==='PC->PLC'?1000:3000);}
function renderSignals(){
 for(const [id,direction] of [['pcSignals','PC->PLC'],['plcSignals','PLC->PC']])$(id).innerHTML=cfg.signals.filter(p=>p.direction===direction).map(p=>`<tr data-signal="${esc(p.id)}"><td title="${esc(p.note)}"><b>${esc(p.label)}</b><small class="signal-id">${esc(p.id)}</small><small>MW${p.mw} / MB${p.mb} · PDU ${address(p)} · ${esc(p.type)}</small></td><td><div class="value" data-value>—</div><small class="raw" data-raw>尚未采样${p.enabled?'':' · 未启用'}</small>${direction==='PC->PLC'?'<small data-time>—</small>':''}</td><td>${direction==='PC->PLC'?`<div class="inline"><input type="number" step="${p.type==='Float32'?'any':'1'}" aria-label="${esc(p.id)} 待发值" placeholder="待发值" data-input><button data-send="${esc(p.id)}" disabled>发送</button></div><small>${p.writeEnabled?'已配置可写':'未开放写入'}</small>`:'<small data-time>—</small>'}</td></tr>`).join('');
 document.querySelectorAll('[data-send]').forEach(b=>b.onclick=()=>action(async()=>{const input=b.closest('tr').querySelector('[data-input]');if(input.value==='')throw Error('请先填写待发值');const result=await api('write',{signal:b.dataset.send,value:Number(input.value)});$('lastWrite').textContent=`${b.dataset.send}：待发 ${result.requested} → PLC 写应答 → 读回 ${result.readback}，一致。操作标识 ${result.operationId}；不表示机械完成。`;notice('写入应答与读回已记录，可按操作标识筛选日志。');}));
 filterSignals();
}
function filterSignals(){const q=$('signalFilter').value.toLowerCase();document.querySelectorAll('[data-signal]').forEach(r=>r.hidden=!r.textContent.toLowerCase().includes(q));}
function renderCoordinates(){
 const rows=[...cfg.axes.map(a=>({...a,label:({X:'X 轴',Y:'Y 轴',CameraZ:'检测 Z',ScanZ:'扫码 Z',GrabZ:'抓取 Z',R:'R 旋转'})[a.name]||a.name}))];
 $('coordinateRows').innerHTML=rows.map(a=>{const p=cfg.signals.find(s=>s.id===a.target);if(!p)return '';return `<tr data-coordinate="${esc(a.name)}" data-target="${esc(a.target)}" data-actual="${esc(a.actual)}"><td><b>${esc(a.label)}</b><small>${esc(a.target)}</small></td><td><span data-position>${a.actual?'尚未采样':'反馈未提供'}</span><small data-position-time></small></td><td><input type="number" step="any" data-coordinate-input placeholder="填写目标" aria-label="${esc(a.name)} 目标坐标" style="width:130px"></td><td>${esc(a.unit||'单位待填')}<small>${p.min??'待填'} ~ ${p.max??'待填'} · ${esc(a.frame||'坐标系待确认')}</small></td><td><button data-coordinate-write disabled>只写目标</button><button data-use-target>带入单轴测试</button></td></tr>`}).join('');
 $('coordinateRows').querySelectorAll('[data-coordinate-write]').forEach(b=>b.onclick=()=>action(async()=>{const row=b.closest('tr'),input=row.querySelector('[data-coordinate-input]');if(input.value==='')throw Error('请先填写该轴目标坐标');const r=await api('write',{signal:row.dataset.target,value:Number(input.value)});$('lastWrite').textContent=`${row.dataset.target}：目标 ${r.requested} → PLC 写应答 → 读回 ${r.readback}。本次没有发送轴启动信号。操作标识 ${r.operationId}`;notice('目标已写入并读回；轴启动是另一个步骤。');}));
 $('coordinateRows').querySelectorAll('[data-use-target]').forEach(b=>b.onclick=()=>{const row=b.closest('tr');$('axis').value=row.dataset.coordinate;$('target').value=row.querySelector('[data-coordinate-input]').value;axisInfo();$('target').scrollIntoView({behavior:'smooth',block:'center'});$('target').focus();});
}
$('signalFilter').oninput=filterSignals;
function axisInfo(){const a=cfg.axes.find(x=>x.name===$('axis').value);$('axisInfo').textContent=a?`${a.confirmed?'已确认':'未确认'} · 范围 ${a.min??'待填'} ~ ${a.max??'待填'} ${a.unit||'单位待填'} · 坐标系 ${a.frame||'待填'} · 容差 ${a.tolerance??'待填'} · 启动/空闲 ${a.startValue??'待填'}/${a.idleValue??'待填'}`:'暂无轴配置';}
$('axis').onchange=axisInfo;
$('connect').onclick=()=>action(async()=>{await api('connect',{});notice('已建立只读会话，未启用写入。信号值来自实际Modbus读取。');});
$('disconnect').onclick=()=>action(async()=>{await api('disconnect',{});notice('已断开，界面保留最后样本。设备动作须现场确认，断开没有发送停止命令。');});
$('writes').onclick=()=>action(async()=>{await api('writes',{enabled:!state.writesEnabled});notice('已更新本轮写入开关。');});
$('heartbeat').onclick=()=>action(async()=>{await api('heartbeat',{enabled:!state.heartbeatEnabled});notice('已更新心跳应答开关。');});
$('move').onclick=()=>action(async()=>{if($('target').value==='')throw Error('请填写单轴目标');const r=await api('move',{axis:$('axis').value,target:Number($('target').value)});notice('单轴已受理，动作标识 '+r.actionId+'；请观察阶段和现场实际动作。');});
function renderLogs(){
 const opened=new Set([...$('wire').querySelectorAll('details[open]')].map(x=>x.dataset.sequence));const wireScroll=$('wire').scrollTop,eventScroll=$('events').scrollTop;
 const q=$('logFilter').value.toLowerCase();
 const match=e=>!q||JSON.stringify(e).toLowerCase().includes(q);
 $('events').innerHTML=(state.events||[]).filter(match).slice().reverse().map(e=>`<div class="log-item"><small>${stamp(e.atUtc)} · ${esc(e.kind)} · ${esc(e.operationId||'')}</small><div class="${esc(e.level)}">${esc(e.message)}</div></div>`).join('')||'<div class="log-item muted">暂无匹配记录</div>';
 $('wire').innerHTML=(state.wire||[]).filter(e=>match(e)&&(!$('hidePoll').checked||!e.context.startsWith('轮询:'))).slice().reverse().map(e=>`<details data-sequence="${e.sequence}" class="log-item ${esc(e.direction)}" ${opened.has(String(e.sequence))?'open':''}><summary><b>${esc(e.direction)}</b> ${stamp(e.atUtc)} · FC${String(e.function).padStart(2,'0')} · TID ${e.transaction} · ${e.elapsedMs}ms<br><small>${esc(e.context)} · ${esc(e.operationId||'')}</small></summary><pre>${esc(e.hex.match(/.{1,2}/g)?.join(' ')||'未收到字节')}</pre>${e.error?`<div>${esc(e.error)}</div>`:''}</details>`).join('')||'<div class="log-item muted">暂无匹配报文。可取消隐藏轮询，查看只读请求与响应。</div>';
 $('wire').scrollTop=wireScroll;$('events').scrollTop=eventScroll;
}
$('logFilter').oninput=$('hidePoll').onchange=renderLogs;
async function refresh(){
 state=await api('state');const connected=state.status==='Connected';
 const labels={Disconnected:'未连接',Connecting:'连接中',Connected:'已连接',Stopped:'已停止 · 最后样本',Failed:'通信失败 · 最后样本'};
 $('status').textContent=labels[state.status]||state.status;$('status').className='tag '+(state.status==='Failed'?'bad':connected?'':'gray');
 $('purpose').textContent=state.purpose==='Virtual'?'本机测试 PLC':'现场 PLC';$('session').textContent=state.sessionId?'会话 '+state.sessionId:'尚未开始测试';
 $('counts').textContent=`${state.statistics.txCount} / ${state.statistics.rxCount}`;$('writeCount').textContent=state.statistics.writeCount;$('edges').textContent=state.heartbeatEdges;$('sampleTime').textContent=stamp(state.lastCycleUtc);
 $('freshness').textContent=connected?'采样中 · 数值来自PLC；下方原始字为十六进制。':'未实时更新 · 数值如有显示，仅为最后一次采样。';$('freshness').className=connected?'':'stale';
 $('connect').disabled=connected;$('disconnect').disabled=!connected;$('writes').disabled=!connected;$('heartbeat').disabled=!connected||!state.writesEnabled;
 $('writes').textContent=state.writesEnabled?'关闭写入':'启用写入';$('writes').classList.toggle('enabled',state.writesEnabled);$('heartbeat').textContent=state.heartbeatEnabled?'停用心跳应答':'启用心跳应答';$('heartbeat').classList.toggle('enabled',state.heartbeatEnabled);
 const active=state.move&&!['Completed','Unknown'].includes(state.move.phase);
 $('move').disabled=!connected||!state.writesEnabled||!state.heartbeatEnabled||active||state.move?.phase==='Unknown';
 for(const id of ['saveConfig','savePoints','saveJson','reloadConfig'])$(id).disabled=connected;
 $('export').disabled=$('exportNotes').disabled=connected;
 const sampleMap=new Map(state.samples.map(s=>[s.signal,s]));
 $('coordinateRows').querySelectorAll('[data-coordinate]').forEach(row=>{const s=sampleMap.get(row.dataset.actual),p=cfg.signals.find(x=>x.id===row.dataset.target);if(s){row.querySelector('[data-position]').textContent=s.value??'解码待核对';row.querySelector('[data-position-time]').textContent=stamp(s.atUtc);}row.querySelector('[data-coordinate-write]').disabled=!connected||!state.writesEnabled||!p?.writeEnabled||!p?.enabled||active;});
 for(const p of cfg.signals){const row=document.querySelector(`[data-signal="${CSS.escape(p.id)}"]`);if(!row)continue;const s=sampleMap.get(p.id);if(s){row.querySelector('[data-value]').textContent=s.value==null?'解码待核对':Number(s.value).toLocaleString('en-US',{maximumFractionDigits:6,useGrouping:false});row.querySelector('[data-raw]').textContent=s.raw+(s.decodeError?' · '+s.decodeError:'');row.querySelector('[data-value]').title=s.decodeError||String(s.value);const t=row.querySelector('[data-time]');if(t)t.textContent=stamp(s.atUtc);row.classList.toggle('changed',connected&&Date.now()-new Date(s.changedUtc).getTime()<1200);}const b=row.querySelector('[data-send]');if(b)b.disabled=!connected||!state.writesEnabled||!p.writeEnabled||!p.enabled||active;}
 if(state.move){const m=state.move;const names={Accepted:'已受理',TargetWritten:'目标已读回',AwaitMoving:'等待本次运动中反馈',AwaitDone:'已观察运动，等待到位',Completed:'本次单轴观察通过',Unknown:'结果未知，需现场处置'};$('moveResult').textContent=`${m.axis.name} → ${m.target} · ${names[m.phase]||m.phase} · ${m.actionId}${m.error?' · '+m.error:''}`;const steps={Accepted:0,TargetWritten:1,AwaitMoving:2,AwaitDone:3,Completed:5,Unknown:0};document.querySelectorAll('.steps li').forEach((el,i)=>el.classList.toggle('done',i<(steps[m.phase]||0)));}
 else{$('moveResult').textContent='尚未开始单轴测试';document.querySelectorAll('.steps li').forEach(el=>el.classList.remove('done'));}
 if(state.lastError)notice(state.lastError,true);renderLogs();
}
const noteKeys=['plcModel','programVersion','operator','observations','remainingQuestions','localCodeChanges'];
async function saveNotes(){for(const k of noteKeys)notes[k]=$('note-'+k).value;await api('notes',notes);}
$('saveNotes').onclick=()=>action(async()=>{await saveNotes();notice('现场记录已保存。');});
async function doExport(){const r=await api('export',{});$('exportResult').innerHTML=`已生成：<a href="${esc(r.download)}" download>${esc(r.filename)}</a><br>本机路径：${esc(r.path)}<br>SHA256：${esc(r.sha256)}`;notice('回传包已生成在 exports 文件夹；到「现场记录与回传」可点击下载。');document.querySelector('[data-tab="notes"]').click();}
$('export').onclick=()=>action(doExport);$('exportNotes').onclick=()=>action(async()=>{await saveNotes();await doExport();});
async function init(){try{[cfg,notes]=await Promise.all([api('config'),api('notes')]);fillConfig();for(const k of noteKeys)$('note-'+k).value=notes[k]||'';await refresh();}catch(e){notice('启动读取失败：'+e.message,true);return;}setInterval(()=>{if(!busy)refresh().catch(e=>{notice('本地后端不可达：'+e.message+'。界面显示最后样本，请核对后台。',true);$('status').textContent='本地后端不可达';$('status').className='tag bad';document.querySelectorAll('[data-send]').forEach(b=>b.disabled=true);for(const id of ['move','writes','heartbeat','connect'])$(id).disabled=true;$('freshness').textContent='未实时更新 · 本地后端不可达，以下为最后样本。';});},750);}
init();
