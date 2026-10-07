(function () {
  const host = window.__GAODE_HOST_CONFIG__ || {};
  delete window.__GAODE_HOST_CONFIG__;
  const apiBase = String(host.apiBaseUrl || '').replace(/\/$/, '');
  const token = host.mode === 'Test' ? String(host.testToken || '') : '';
  const prepared = host.mode === 'Test' ? host.preparedStartRequest : null;
  const cache = new Map();
  let runId = null, receipt = null, latestStatus = null, latestRun = null, latestEvidence = null;
  let latestMedia = null, mediaError = null, commandPending = false, startSubmitted = false, lastStartFailure = null;
  let interactionNotice = null;
  let lastStartHttpStatus = null;
  let resultFocus = null, resultFocusRun = null, refreshInFlight = null, refreshPending = false;
  const viewRunKey = `gaode:station01:view-run:${apiBase}:${host.mode || ''}:${prepared?.requestId || ''}`;
  // Only an untrusted run reference survives page reopen; every result is fetched from the API.
  const viewStorage = (() => { try { return window.localStorage || window.sessionStorage; } catch (_) { return null; } })();
  try { runId = viewStorage?.getItem(viewRunKey) || null; } catch (_) {}
  function rememberRun(id) { try { viewStorage?.setItem(viewRunKey, id); } catch (_) {} }

  let selectedRecipe = null;
  const mediaUrls = new Map(), mediaReadErrors = new Map(), mediaSelection = new Map();
  let mediaSelectionRunId = null;
  const diagnosticStates = new Map();
  function diagnostic(event, facts = {}, stateKey = null) {
    try {
      const data = { event, requestId: prepared?.requestId || null, runId, ...facts };
      const text = JSON.stringify(data);
      if (stateKey && diagnosticStates.get(stateKey) === text) return;
      if (stateKey) diagnosticStates.set(stateKey, text);
      // Only selected metadata: never host config, bearer token, context/body or image data.
      console.info('GaodePageDiagnostic', text);
    } catch (_) { /* diagnostics must not affect the existing UI/control behavior */ }
  }

  async function request(path, init = {}) {
    const headers = new Headers(init.headers);
    headers.set('Accept', 'application/json');
    if (token) headers.set('Authorization', `Bearer ${token}`);
    const previous = cache.get(path);
    if ((!init.method || init.method === 'GET') && previous?.etag) headers.set('If-None-Match', previous.etag);
    let response;
    try { response = await fetch(apiBase + path, { ...init, headers }); }
    catch (error) {
      diagnostic('HttpTransportFailed', { method: init.method || 'GET', path: path.split('?')[0],
        errorType: error.name }, `transport:${path}`);
      throw error;
    }
    diagnosticStates.delete(`transport:${path}`);
    if (response.status === 304 && previous) return previous.value;
    if (!response.ok) {
      let body = {};
      try { body = await response.json(); } catch (_) { /* HTTP status remains authoritative */ }
      const error = new Error(body.message || body.detail || body.error || `HTTP ${response.status}`);
      error.contract = { code: body.code || body.error || `HTTP_${response.status}`,
        httpStatus: response.status, details: body.details || null, traceId: body.traceId || null };
      throw error;
    }
    const value = response.status === 204 ? null : await response.json();
    if (!init.method || init.method === 'GET') cache.set(path, { etag: response.headers.get('ETag'), value });
    return value;
  }
  function post(path, body) {
    return request(path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  }
  // No browser recipe cache. Each load obtains a complete representation and its exact read ETag.
  const recipeSession = window.GaodeRecipeAuthoring?.mount({ document, requestId: () => crypto.randomUUID(),
    requestTimeoutMs: 30000, send: async (path, init = {}) => {
      const headers = new Headers(init.headers); headers.set('Accept', 'application/json');
      if (token) headers.set('Authorization', `Bearer ${token}`);
      const response = await fetch(apiBase + path, { ...init, headers, cache: 'no-store' });
      let value;
      try { value = await response.json(); }
      catch (_) { value = {}; }
      if (!response.ok) {
        const error = new Error(value.message || `HTTP ${response.status}`);
        error.status = response.status; error.details = value.details;
        diagnostic('RecipeAuthoringRejected', { path, httpStatus: response.status, code: value.code, traceId: value.traceId });
        throw error;
      }
      diagnostic('RecipeAuthoringResponse', { path, method: init.method || 'GET', httpStatus: response.status,
        recipeId: value.definition?.recipeId, version: value.definition?.version });
      return { value, etag: response.headers.get('ETag') };
    } });
  document.getElementById('btnRecipe')?.addEventListener?.('click', () => void recipeSession?.open());
  window.addEventListener?.('recipe-authoring:closed', () => recipeSession?.close());
  function preparedSelection() {
    if (!selectedRecipe || !prepared) return prepared;
    try {
      const context = JSON.parse(prepared.contextJson);
      return { ...prepared, contextJson: JSON.stringify({ ...context,
        schemaVersion: 'station01-start-run-context/2.0', expectedRecipeRef: {
          recipeId: selectedRecipe.recipeId, version: selectedRecipe.version, catalogDigest: selectedRecipe.catalogDigest
        } }) };
    } catch (_) { return prepared; }
  }
  function legalPreparedRequest(value) {
    const reference = ref => ref && typeof ref === 'object' && !Array.isArray(ref) &&
      [ref.id, ref.version].every(part => typeof part === 'string' && /^[A-Za-z0-9._-]{1,128}$/.test(part));
    if (!value || typeof value !== 'object' || Array.isArray(value) ||
        typeof value.requestId !== 'string' || !value.requestId.trim() ||
        typeof value.contextJson !== 'string' ||
        ![value.publicConfigRef, value.budgetRef, value.simulationRef].every(reference)) return false;
    try {
      const context = JSON.parse(value.contextJson);
      if (context.purpose !== 'Test' || !Array.isArray(context.occupiedSlots) ||
          !context.occupiedSlots.every(x => typeof x === 'string') ||
          ![context.trayId, context.stationId, context.lineId, context.scenarioId].every(x => typeof x === 'string' && x.length > 0)) return false;
      if (context.schemaVersion === 'station01-start-run-context/2.0')
        return [context.expectedRecipeRef?.recipeId, context.expectedRecipeRef?.version,
          context.expectedRecipeRef?.catalogDigest].every(x => typeof x === 'string' && x.length > 0);
      return context.schemaVersion === 'station01-start-run-context/1.0';
    } catch (_) { return false; }
  }
  function setText(id, value) { const node = document.getElementById(id); if (node) node.textContent = String(value); }
  function displayValue(value, missing = '未提供') {
    return value == null ? missing : typeof value === 'object' ? JSON.stringify(value) : String(value);
  }
  function escaped(value) {
    return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;').replaceAll("'", '&#39;');
  }
  function resultRows(id, rows, defect = false) {
    const node = document.getElementById(id);
    if (!node) return;
    const columns = defect ? '36px 1fr 90px 56px' : '28px 1fr 80px 56px';
    node.innerHTML = rows.map(([number, name, detail, measured, verdict]) => {
      const badge = verdict === 'OK' ? 'badge-ok' : verdict === 'NG' ? 'badge-ng' : 'badge-pend';
      return `<div class="grid items-center px-3 py-1.5 text-xs border-b border-slate-700/30" style="grid-template-columns: ${columns};">
        <div class="num-font text-slate-400">${escaped(number)}</div>
        <div class="min-w-0 overflow-hidden"><div class="text-slate-100 truncate" title="${escaped(name)}">${escaped(name)}</div>
        <div class="text-[10px] text-slate-500 truncate" title="${escaped(detail)}">${escaped(detail)}</div></div>
        <div class="num-font text-right text-slate-100 truncate" title="${escaped(measured)}">${escaped(measured)}</div>
        <div class="text-center"><span class="inline-block text-[10px] px-1.5 py-0.5 rounded ${badge}">${escaped(verdict)}</span></div></div>`;
    }).join('');
  }
  function mediaIdentity(item) {
    if (item.role === 'ThreeD' && item.businessCamera === 'ThreeD') return 'ThreeD';
    if (item.role === 'F' && item.businessCamera === 'F') return 'F';
    if (item.role === 'Detection' && ['A', 'B', 'C', 'D'].includes(item.businessCamera))
      return `Detection:${item.businessCamera}`;
    return null;
  }
  // A run-stable, Test-only allocation proves the media path without claiming a process-camera mapping.
  function testSlotAllocation(id) {
    const slots = [0, 1, 2, 3, 4, 5, 6];
    let seed = 2166136261;
    for (const character of id) seed = Math.imul(seed ^ character.charCodeAt(0), 16777619) >>> 0;
    for (let i = slots.length - 1; i > 0; i--) {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
      const j = seed % (i + 1);
      [slots[i], slots[j]] = [slots[j], slots[i]];
    }
    return new Map(['ThreeD', 'F', 'Detection:A', 'Detection:B', 'Detection:C', 'Detection:D']
      .map((key, i) => [key, slots[i]]));
  }
  function mediaCandidates(catalog) {
    const grouped = new Map();
    if (host.mode !== 'Test' || !runId || catalog?.runId?.toLowerCase() !== runId.toLowerCase()) return grouped;
    const allocation = testSlotAllocation(runId);
    for (const item of catalog.items || []) {
      const identity = mediaIdentity(item), slot = allocation.get(identity);
      if (slot === undefined || !item.mediaId || !item.captureId || !Number.isSafeInteger(item.committedRevision)) continue;
      if (!grouped.has(slot)) grouped.set(slot, []);
      grouped.get(slot).push(item);
    }
    for (const list of grouped.values()) list.sort((a, b) => b.committedRevision - a.committedRevision);
    return grouped;
  }
  function selectedMedia(catalog) {
    const selected = new Map();
    if (mediaSelectionRunId !== runId) { mediaSelection.clear(); mediaSelectionRunId = runId; }
    for (const [slot, candidates] of mediaCandidates(catalog)) {
      const pinned = candidates.find(item => item.mediaId === mediaSelection.get(slot) && item.readiness === 'Ready');
      const item = pinned || candidates.find(item => item.readiness === 'Ready') || candidates[0];
      if (item) selected.set(slot, item);
    }
    return selected;
  }
  async function loadMedia(catalog) {
    const selected = selectedMedia(catalog);
    const needed = new Set(Array.from(selected.values()).filter(x => x.readiness === 'Ready').map(x => x.mediaId));
    for (const [id, url] of mediaUrls) {
      if (!needed.has(id)) { URL.revokeObjectURL(url); mediaUrls.delete(id); mediaReadErrors.delete(id); }
    }
    for (const item of selected.values()) {
      if (item.readiness !== 'Ready' || mediaUrls.has(item.mediaId)) continue;
      try {
        const response = await fetch(`${apiBase}/api/v1/station01/media/${encodeURIComponent(item.mediaId)}`, {
          headers: { Accept: 'image/*', Authorization: `Bearer ${token}` }
        });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const blob = await response.blob();
        if (!blob.type.startsWith('image/')) throw new Error('媒体内容不是图像');
        mediaUrls.set(item.mediaId, URL.createObjectURL(blob));
        mediaReadErrors.delete(item.mediaId);
      } catch (error) {
        mediaReadErrors.set(item.mediaId, error.message);
        diagnostic('MediaReadFailed', { mediaId: item.mediaId, errorType: error.name }, `media:${item.mediaId}`);
      }
    }
  }
  function clearPrototypeDemo() {
    for (const selector of ['.kpi .k-value', '.kpi .k-sub', '#donut .num-font', '#donut + div b']) {
      document.querySelectorAll(selector).forEach(node => { node.textContent = '—'; });
    }
    document.querySelectorAll('.scene-tab').forEach(tab => tab.addEventListener('click', event => {
      event.stopImmediatePropagation();
    }, true));
    const verdictPanel = document.getElementById('verdictBig')?.closest('.panel');
    verdictPanel?.querySelectorAll('.num-font').forEach(node => {
      if (node.id !== 'verdictBig') node.textContent = '—';
    });
    const station = document.getElementById('stage');
    station?.querySelectorAll('.num-font').forEach(node => {
      if (!['nowTime', 'verdictBig', 'faultCount', 'selectedRecipeVersion'].includes(node.id)) node.textContent = '—';
    });
    const partInfo = document.getElementById('verdictBig')?.closest('aside')?.children[1];
    partInfo?.querySelectorAll('.mt-0\\.5').forEach(node => { node.textContent = '—'; });
  }
  function runtimeFacts(run) {
    const phase = run?.recipeExecution?.executionPhase || run?.executionPhase;
    const phaseNames = { Initial3D: '首次3D', FScan: 'F扫码', RecipeBinding: '配方绑定', InspectFace: '检测面',
      PositionForFlip: '翻面取件定位', Flip: '翻面', PositionForPutBack: '放回定位', PutBack: '放回',
      PoseRecheck: '姿态复查', EntityCode: 'E扫码', Rotation: '旋转到位', TransferToRotation: '旋转工位上料', ReturnToOrigin: '原槽回放', Sorting: '分拣', Unload: '下料', ManualRemoval: '人工取盘', FinalSave: '最终保存' };
    const abnormal = run?.abnormalPhysicalSlotIndices;
    return [
      ['运行状态', displayValue(run?.executionState || (typeof run?.state === 'string' ? run.state : null))],
      ['实际阶段', displayValue(run?.recipeExecution?.stage)],
      ['当前动作', phase ? `${phaseNames[phase.kind] || phase.kind} · ${displayValue(phase.state)}` : '未提供'],
      ['观察覆盖', displayValue(run?.observationCoverage?.state)],
      ['异常物理槽', Array.isArray(abnormal) ? abnormal.length ? abnormal.join('、') : '未报告异常 · 以观察覆盖为限' : '未可靠观察'],
      ['配方绑定', displayValue(run?.recipeState)],
      ['分拣状态', displayValue(run?.sortingState)],
      ['整盘状态', displayValue(run?.wholeTaskState)]
    ];
  }
  const publicTrayFlow = window.GaodePublicTrayFlow?.mount({ document, request, post, refresh, runId: () => runId });
  function render(status, run, evidence) {
    publicTrayFlow?.render(run);
    const connection=status?.plc?.connection;
    const reliable=status?.plc?.reliability==='Reliable';
    const connectionText=reliable&&connection==='Connected'?'设备已连接':connection==='Disconnected'?'设备已断开':'设备状态未确认';
    const reportedRunState=run?.executionState||(typeof run?.state==='string'?run.state:null);
    const noRun=status&&Object.prototype.hasOwnProperty.call(status,'currentRun')&&status.currentRun===null&&!run;
    setText('stationOperationalStatus',`${connectionText} · ${reportedRunState|| (noRun?'未运行':'运行状态未提供')}`);
    const connectionDot=document.getElementById('stationConnectionDot');
    if(connectionDot)connectionDot.className='dot'+(reliable&&connection==='Connected'?' ok':connection==='Disconnected'?' bad':'');
    const activeRecipe = run?.recipeExecution || run?.recipeSelection;
    const observedVersion = activeRecipe?.recipeVersion || activeRecipe?.version;
    if (activeRecipe?.recipeId && observedVersion) {
      setText('selectedRecipeName', run?.recipeExecution?.model
        ? `${run.recipeExecution.model} · ${activeRecipe.recipeId}` : activeRecipe.recipeId);
      setText('selectedRecipeVersion', observedVersion);
    } else if (run) {
      setText('selectedRecipeName', '运行配方未提供'); setText('selectedRecipeVersion', '未提供');
    }
    const facts = runtimeFacts(run);
    const final = evidence?.finalResult === 'FinalUnloadCompletion' && !!evidence?.finalSourceMatrix;
    const observedState = run?.executionState || (typeof run?.state === 'string' ? run.state : run ? '状态未提供' : status?.stage) || 'Unknown';
    const state = final ? 'FinalUnloadCompletion' :
      observedState === 'Completed' || observedState === 'FinalUnloadCompleted'
        ? '终态待核对' : observedState;
    const results = Array.isArray(run?.results) ? run.results : [];
    if (resultFocusRun !== runId) { resultFocus = null; resultFocusRun = runId; }
    const context = resultFocus || run?.resultContext;
    const current = context && results.find(x => x.kind === context.kind && x.id === context.id);
    const quality = current?.availability === 'Committed' && ['OK','NG','Pending'].includes(current.disposition)
      ? current.disposition : current?.availability === 'NotCommitted' ? '待保存'
        : current?.availability === 'Unavailable' ? '结果不可用' : '尚无结果';
    setText('verdictBig', !run && [401, 403].includes(lastStartHttpStatus) ? '权限受限' : quality);
    const verdictNode = document.getElementById('verdictBig');
    if (verdictNode) verdictNode.style.color = quality === 'OK' ? '#34d399' : quality === 'NG' ? '#fb7185' : '#fbbf24';
    const verdictPanel = verdictNode?.closest('.panel');
    const number = verdictPanel?.children?.[0]?.querySelector('.num-font');
    if (number) { number.textContent = current?.id || '未提供'; number.title = current?.id || '未提供'; }
    const metrics = verdictPanel?.children?.[1]?.querySelectorAll('.grid .num-font');
    const metricValues = [current?.confidence == null ? '未提供' : `${current.confidence}${current.confidenceUnit || ''}`,
      current?.defectCount ?? '未提供', current?.inspectedAtUtc || '未提供',
      current?.inspectionDurationMs == null ? '未提供' : `${current.inspectionDurationMs} ms`];
    if (metrics) Array.from(metrics).filter(x => x.id !== 'verdictBig').forEach((x, i) => x.textContent = String(metricValues[i] ?? '未提供'));
    const canConfirmRemoval = !!runId && !commandPending &&
      Array.isArray(run?.allowedActions) && run.allowedActions.includes('ConfirmManualTrayRemoval');
    const recoveryAction = run?.failedCommandRecovery &&
      run.allowedActions?.find(x => x === 'RecoveryReset' || x === 'RecoveryCheck');
    const removalButton = document.getElementById('manualRemovalButton');
    const removalReason = document.getElementById('manualRemovalReason');
    if (removalButton) {
      removalButton.disabled = !(canConfirmRemoval || recoveryAction && !commandPending);
      removalButton.textContent = recoveryAction === 'RecoveryReset' ? '复位' :
        recoveryAction === 'RecoveryCheck' ? '初始核验' : '确认已取盘';
    }
    if (removalReason) removalReason.disabled = !(canConfirmRemoval || recoveryAction);
    Array.from(document.querySelectorAll('#moduleGrid > div')).forEach((card, i) => {
      if (!facts[i]) return;
      const label = card.querySelector('.text-sm'), detail = card.querySelector('.text-xs.text-slate-400');
      const badge = card.querySelector('.flex.items-center.gap-1.text-xs');
      if (label) label.textContent = facts[i][0];
      if (detail) detail.textContent = facts[i][1];
      if (badge) badge.textContent = facts[i][1];
    });
    const cameraTiles = Array.from(document.querySelectorAll('#camGrid > div'));
    const selected = selectedMedia(latestMedia);
    const candidatesBySlot = mediaCandidates(latestMedia);
    const allocated = runId && host.mode === 'Test' ? new Set(testSlotAllocation(runId).values()) : new Set();
    cameraTiles.forEach((tile, slot) => {
      const image = tile.querySelector('img');
      if (!image) return;
      const item = selected.get(slot), url = item && mediaUrls.get(item.mediaId);
      if (url) {
        image.src = url;
        image.dataset.runId = runId;
        image.dataset.mediaId = item.mediaId;
        image.dataset.captureId = item.captureId;
        image.dataset.businessCamera = item.businessCamera;
        image.dataset.source = item.source;
        if (item.localFace) image.dataset.localFace = item.localFace;
        if (item.heightRound) image.dataset.heightRound = item.heightRound;
      } else {
        image.removeAttribute('src');
        for (const key of ['runId', 'mediaId', 'captureId', 'businessCamera', 'source', 'localFace', 'heightRound']) delete image.dataset[key];
      }
      const marker = tile.querySelector('.flex.items-center.gap-1.text-white');
      const choices = (candidatesBySlot.get(slot) || []).filter(x => x.readiness === 'Ready');
      const currentIndex = choices.findIndex(x => x.mediaId === item?.mediaId);
      const faceLabel = item?.localFace && item?.heightRound
        ? ` · 面${item.localFace}/${item.heightRound === 1 ? '初始测量' : `测量轮${item.heightRound}`}` : '';
      const choiceLabel = choices.length > 1 ? ` · 点击切换${currentIndex + 1}/${choices.length}` : '';
      if (marker) marker.textContent = !runId ? '未采集' : !allocated.has(slot) ? '未参与 · Test' :
        !item ? mediaError ? '媒体查询受限 · Test' : '未采集 · Test' :
        item.readiness !== 'Ready' ? '未就绪 · Test' :
        !url ? `读取受限 · ${item.source}` :
          `${item.source || '来源未知'} · 临时格位 · ${item.role}/${item.businessCamera}${faceLabel}${choiceLabel}`;
    });
    const summary = cameraTiles[cameraTiles.length - 1];
    if (summary && !summary.querySelector('img')) {
      const count = summary.querySelector('.num-font');
      if (count) count.textContent = `${Array.from(selected.values()).filter(x => mediaUrls.has(x.mediaId)).length} / 7`;
      const cadence = summary.querySelector('.text-slate-500');
      if (cadence) cadence.textContent = runId
        ? `${[...new Set(Array.from(selected.values(), item => item.source || '来源未知'))].join(' · ') || '来源未知'} · 临时格位` : '未采集';
    }
    const diagnostic = run?.startupDiagnostic;
    const deviceProvider = diagnostic?.executionOrigin?.provider || status?.plc?.executionOrigin?.provider;
    const sourceLabel = deviceProvider === 'Virtual'
      ? 'Test/VirtualPlc · 非真机' : deviceProvider || '来源未知';
    const finalSources = evidence?.finalSourceMatrix?.components;
    // Existing business matrix enums retain their JSON representation. These are
    // component/source identities, never device feedback or protocol values.
    const componentNames = ['Host', 'Plc', 'Camera', 'Light', 'Algorithm', 'ManualActor'];
    const sourceNames = ['Real', 'Virtual', 'Simulated', 'Test', 'AuthenticatedHuman'];
    const matrixSourceLabel = components => Array.isArray(components) && components.length
      ? components.map(item => `${componentNames[item.component] || '来源未知'}:${sourceNames[item.source] || '来源未知'}`).join(' · ') : '来源未知';
    const finalSourceLabel = matrixSourceLabel(finalSources);
    const removalMatrix = evidence?.readyForRemovalSourceMatrix;
    const removalDetails = run?.manualRemovalAllowedEventId
      ? `人工取盘准入已提交 · 事件 ${run.manualRemovalAllowedEventId} · 来源矩阵 ${displayValue(run.readyForRemovalSourceMatrixId)} · ${matrixSourceLabel(removalMatrix?.components)}；不表示最终完成`
      : null;
    const reason = diagnostic?.safetyAssessment === 'Unconfirmed'
      ? '无法确认设备安全；核查连接和现场状态，仅可查询原任务'
      : diagnostic?.safetyAssessment === 'ExplicitUnsafe'
        ? '设备可靠反馈明确不安全；由授权人员现场排查互锁/报警，仅可查询原任务'
        : diagnostic?.reasonCodes?.length ? `启动受阻：${diagnostic.reasonCodes.join('、')}；查询原任务或联系授权人员`
          : null;
    const fBlocked = run?.errorCode?.includes('FCodeNotUniqueAndParsed')
      ? '扫码操作已结束，但未取得唯一有效F码，配方尚未绑定；请核对本次相机/算法日志' : null;
    const problem = fBlocked || reason || run?.errorCode || lastStartFailure || (status?.host !== 'Ready' ? status?.host : null) ||
      (!token ? 'AuthUnavailable' : null) || (status?.algorithm?.state !== 'Ready' ? status?.algorithm?.state : null);
    const phase = run?.recipeExecution?.executionPhase || run?.executionPhase;
    const slotDetails = (run?.slotStates || []).map(slot => `${slot.region || '区域未提供'}区 第${displayValue(slot.row)}行 第${displayValue(slot.column)}列 · 物理槽${slot.physicalSlotIndex}：${displayValue(slot.presence)}/${displayValue(slot.poseState)}${slot.detectionState ? ' · '+({NotInspected:'未检测',FurtherInspectionTerminated:'后续检测终止',NoMaterial:'无物料'})[slot.detectionState] : ''}${slot.physicalDisposition ? ' · '+(slot.physicalDisposition==='Pending'?'Pending已分拣':'Pending待分拣') : ''}${slot.reasonCodes?.length ? ' · ' + slot.reasonCodes.join('、') : ''}`);
    const phaseDetails = phase ? `当前动作 ${phase.kind}/${phase.state} · 步骤 ${displayValue(phase.stepSequence)} · 采集组 ${displayValue(phase.stageId)} · 原始格位 ${displayValue(phase.cellId)} · 实体 ${displayValue(phase.entityId)} · 物理槽 ${displayValue(phase.physicalSlotIndex)} · 面 ${displayValue(phase.localFace)} · 扫码姿态 ${displayValue(phase.scanPoseId)} · 观察 ${displayValue(phase.observationRef)} · 转换 ${displayValue(phase.transitionId)} · 依据 ${displayValue(phase.evidenceRef)}` : null;
    const abnormal = run?.abnormalPhysicalSlotIndices;
    const abnormalNotice = run ? `异常物理槽：${Array.isArray(abnormal) ? abnormal.length ? abnormal.join('、') : '未报告异常' : '未可靠观察'}；观察覆盖 ${displayValue(run.observationCoverage?.state)} · ${displayValue(run.observationCoverage?.lastObservationRef)}` : null;
    const axes = run ? run.axisObservations : status?.plc?.axisObservations;
    const axisDetails = (Array.isArray(axes) ? axes : []).map(axis => `轴 ${displayValue(axis.axis)} · 实测 ${displayValue(axis.position)} ${displayValue(axis.unit)} · ${displayValue(axis.reliability)} · 时间 ${displayValue(axis.observedAt)} · 连接代次 ${displayValue(axis.connectionEpoch)} · 依据 ${displayValue(axis.evidenceRef)}`);
    setText('faultCount', problem || abnormal?.length ? 1 : 0);
    setText('faultList', [problem ? `${problem} · ${sourceLabel}` : final ? `最终事实已提交 · ${finalSourceLabel}` : '无已报告故障',
      recoveryAction ? `原任务 ${runId}，故障动作 ${run.failedCommandRecovery.operationId}；${recoveryAction === 'RecoveryReset'
        ? '排除故障后填写原因并复位，复位不等于恢复完成'
        : '请核对复位后的料盘、配置和初始状态；填写依据并执行初始核验，成立后通过启动控件开启完整新轮'}` : null,
      phaseDetails, abnormalNotice, ...slotDetails, ...axisDetails, removalDetails, interactionNotice].filter(Boolean).join('；'));
    const inspections = (current?.inspections || []).filter(x => !context?.localFace || x.localFace === context.localFace);
    const identity = current ? `${current.kind}/${current.id}${current.parentId ? ` · 所属 ${current.parentId}` : ''}` : '当前对象未提供';
    const movement = (run?.movements || []).filter(item => item.entityId === current?.id || item.entityId === current?.parentId);
    const movementDetail = movement.map(item => `物理槽${displayValue(item.physicalSlotIndex)} ${displayValue(item.sourcePointRef)} → ${displayValue(item.targetPointRef)} · ${displayValue(item.state)} · 用途 ${displayValue(item.transferPurpose)} · 原始格位 ${displayValue(item.originalCellId)} · 安全位 ${item.safeConfirmed === true ? '已确认' : item.safeConfirmed === false ? '未确认' : '未提供'} · 操作 ${displayValue(item.operationId)} · 提交 ${displayValue(item.committedEventId)}`).join('；');
    const contextDetail = `${identity} · 物理槽 ${displayValue(current?.physicalSlotIndex)} · 姿态 ${displayValue(current?.poseState)} · 参与 ${displayValue(current?.participation)} · 搬运 ${movementDetail || '未提供'} · 流程 ${state}${diagnostic?.stopStage ? ` · 阻断步骤 ${diagnostic.stopStage}` : ''} · 完整性 ${current?.completeness || 'Unknown'} · 处置 ${current?.dispositionState || '未提供'} · 保存 ${current?.saveState || '尚无结果'} · ${current?.source || '来源未提供'}/${current?.quality || '事实属性未提供'}`;
    resultRows('itemList', inspections.length ? inspections.map((x, i) => [String(i + 1),
      x.itemName || x.itemId || x.inspectionId || '项目未提供',
      `${contextDetail} · 面${x.localFace ?? '未提供'} / ${x.businessCamera || '融合'} · ${displayValue(x.rule, '规则未提供')} · ${x.technicalState || '技术状态未提供'}${x.reasonCodes?.length ? ` · ${x.reasonCodes.join('、')}` : ''}`,
      `${displayValue(x.measuredValue)}${x.measuredValue != null && x.unit ? ` ${x.unit}` : ''}`,
      x.disposition || '无结论']) : [['—', '尚无可查询检测结果', contextDetail, '未提供', '无结论']]);
    const defectRows = inspections.flatMap(x => Array.isArray(x.defects) ? x.defects.length
      ? x.defects.map(d => ['', d.type || '类型未提供', `面${x.localFace ?? '未提供'} / ${x.businessCamera || '融合'} · ${displayValue(d.position, '位置未提供')}`,
        `${displayValue(d.size, '尺寸未提供')}${d.size != null && d.unit ? ` ${d.unit}` : ''}`, x.disposition || '无结论'])
      : [['', '已提供空缺陷集合', `面${x.localFace ?? '未提供'} / ${x.businessCamera || '融合'}`, '—', x.disposition || '无结论']]
      : [['', `${x.disposition || '无结论'}，缺陷明细未提供`, x.reasonCodes?.join('、') || '原因未提供', '未提供', x.disposition || '无结论']]);
    resultRows('defectList', defectRows.length ? defectRows.map((row, i) => [String(i + 1), ...row.slice(1)])
      : [['—', '尚无可查询检测明细', contextDetail, '未提供', '无结论']], true);
    const parameters = inspections.flatMap(x => (x.parameters || []).map(p => [
      `组${displayValue(x.stageId)} · 面${x.localFace ?? '未提供'} / ${x.businessCamera || '融合'} · ${p.name} · ${p.kind}${p.reference ? ` / ${p.reference}` : ''}`,
      `${displayValue(p.value)}${p.value != null && p.unit ? ` ${p.unit}` : ''}`]));
    const parameterList = document.getElementById('paramList');
    if (parameterList) parameterList.innerHTML = (parameters.length ? parameters : [['参数', '未提供']]).map(([name, value]) =>
      `<div class="rounded bg-slate-800/40 px-2.5 py-1.5 flex justify-between"><span class="text-slate-400">${escaped(name)}</span><span class="num-font text-slate-100">${escaped(value)}</span></div>`).join('');
    const partInfo = document.getElementById('verdictBig')?.closest('aside')?.children[1];
    const fields = partInfo?.querySelectorAll('.mt-0\\.5');
    const identityFields = ['未提供', '未提供', run?.identity?.scenarioId || '未提供',
      '未提供', '未提供', context?.localFace == null ? current ? '不适用' : '未关联' : `面${context.localFace}`];
    fields?.forEach((node, i) => { node.textContent = identityFields[i] ?? '未提供'; });
    setText('currentScene', runId ? `${run?.identity?.scenarioId || '场景待查'} · ${state}` : '无当前运行');
    setText('partsCount', '—');
    for (const id of ['partsTbody', 'trendChart', 'trendLabels']) { const node = document.getElementById(id); if (node) node.textContent = ''; }
    const donut = document.getElementById('donut'); if (donut) donut.style.background = 'none';
    window.dispatchEvent(new CustomEvent('station01:rendered', { detail: { runId, state, facts, final, source: final ? finalSourceLabel : sourceLabel } }));
    window.dispatchEvent(new CustomEvent('station01:media-rendered', { detail: { runId, temporaryTestAllocation: host.mode === 'Test' && !!runId,
      bindings: Array.from(selected, ([slot, item]) => ({ slot: slot + 1, mediaId: item.mediaId,
        captureId: item.captureId, role: item.role, businessCamera: item.businessCamera,
        objectId: item.objectId, localFace: item.localFace, heightRound: item.heightRound,
        readiness: item.readiness, source: item.source, displayed: mediaUrls.has(item.mediaId) })) } }));
  }
  async function refresh() {
    refreshPending = true;
    if (refreshInFlight) return refreshInFlight;
    refreshInFlight = (async () => {
      while (refreshPending) { refreshPending = false; await refreshCore(); }
    })();
    try { await refreshInFlight; } finally { refreshInFlight = null; }
  }
  async function refreshCore() {
    try {
      latestStatus = await request('/api/v1/station01/status');
      runId ||= latestStatus?.currentRun?.runId || null;
      if (runId) {
        try {
          const requestedRun = runId;
          const responseRun = await request(`/api/v1/station01/runs/${encodeURIComponent(requestedRun)}`);
          if (runId !== requestedRun) { refreshPending = true; return; }
          latestRun = responseRun;
          publicTrayFlow?.render(latestRun);
        } catch (error) {
          if (error.contract?.httpStatus === 404 && receipt?.runId === runId) {
            latestRun = null; latestEvidence = null;
            interactionNotice = '启动已受理，运行记录暂未可查；继续查询原请求';
            render(latestStatus, null, null);
            return;
          }
          throw error;
        }
        if (interactionNotice?.startsWith('启动已受理，运行记录暂未可查')) interactionNotice = null;
        const queryRun = runId;
        const evidence = await request(`/api/v1/station01/runs/${encodeURIComponent(queryRun)}/evidence`);
        if (runId !== queryRun) { refreshPending = true; return; }
        latestEvidence = evidence;
        try {
          const media = await request(`/api/v1/station01/runs/${encodeURIComponent(queryRun)}/media`);
          if (runId !== queryRun) { refreshPending = true; return; }
          latestMedia = media;
          mediaError = null;
          await loadMedia(latestMedia);
          if (runId !== queryRun) { refreshPending = true; return; }
        } catch (error) { latestMedia = null; mediaError = error.message; }
      }
      render(latestStatus, latestRun, latestEvidence);
      diagnostic('StateObserved', { state: latestRun?.state || latestStatus?.stage,
        errorCode: latestRun?.errorCode, stopStage: latestRun?.startupDiagnostic?.stopStage,
        reasons: latestRun?.startupDiagnostic?.reasonCodes,
        finalResult: latestEvidence?.finalResult }, 'state');
      diagnosticStates.delete('queryError');
      window.dispatchEvent(new CustomEvent('station01:status', { detail: { status: latestStatus, run: latestRun, evidence: latestEvidence } }));
    } catch (error) {
      diagnostic('QueryFailed', { code: error.contract?.code || 'HostUnavailable',
        httpStatus: error.contract?.httpStatus, traceId: error.contract?.traceId }, 'queryError');
      const denied = error.contract?.httpStatus === 401 || error.contract?.httpStatus === 403;
      setText('verdictBig', denied ? '权限受限' : 'Unknown'); setText('faultCount', 1);
      setText('faultList', denied
        ? `后端拒绝访问（${error.contract.httpStatus}）；请核对登录与权限`
        : error.message || '后端不可用，当前状态受限');
      window.dispatchEvent(new CustomEvent('station01:error', { detail: error.contract || { code: 'HostUnavailable' } }));
    }
  }
  async function connectNotifications() {
    if (!token || !window.signalR || !host.signalrUrl) return null;
    const connection = new window.signalR.HubConnectionBuilder()
      .withUrl(host.signalrUrl, { accessTokenFactory: () => token,
        transport: window.signalR.HttpTransportType.LongPolling, withCredentials: false })
      .withAutomaticReconnect().build();
    for (const event of ['StateChanged', 'DiagnosticChanged', 'HandoffReady',
      'WholeTrayCompleted', 'ManualRemovalAllowed', 'FinalUnloadCompleted']) {
      connection.on(event, message => {
        if (message?.schemaVersion !== 's01/notification/2.0') return;
        window.dispatchEvent(new CustomEvent('station01:notification', { detail: { event, runId: message?.runId } }));
        void refresh();
      });
    }
    connection.onreconnected(() => void refresh());
    await connection.start();
    window.dispatchEvent(new CustomEvent('station01:connected'));
    return connection;
  }
  window.station01 = Object.freeze({
    config: Object.freeze({ mode: host.mode, apiBaseUrl: apiBase, prototypeSha256: host.prototypeSha256 }),
    status: () => request('/api/v1/station01/status'), run: id => request(`/api/v1/station01/runs/${encodeURIComponent(id)}`),
    start: body => post('/api/v1/station01/runs', body), connectNotifications
  });
  const recipeCatalog = document.getElementById('recipeAuthoringCatalog');
  if (typeof recipeCatalog?.addEventListener === 'function') recipeCatalog.addEventListener('change', () => {
    const editor = recipeSession?.state;
    const item = editor?.catalog.find(item => item.recipeId === (recipeCatalog.value || editor.recipeId));
    if (!item || item.availability !== 'Available') {
      interactionNotice = item?.restriction || '此配方当前不可选用';
      render(latestStatus, latestRun, latestEvidence);
      return;
    }
    selectedRecipe = item;
    if (!runId) {
      setText('selectedRecipeName', `${item.model} · ${item.recipeId}`);
      setText('selectedRecipeVersion', item.version);
    } else {
      interactionNotice = `已选择 ${item.model} 供下次启动使用；当前运行继续使用其冻结配方`;
      render(latestStatus, latestRun, latestEvidence);
    }
  });
  const manualRemovalButton = document.getElementById('manualRemovalButton');
  Array.from(document.querySelectorAll('#camGrid > div')).forEach((tile, slot) => {
    tile.addEventListener('click', async () => {
      const choices = (mediaCandidates(latestMedia).get(slot) || [])
        .filter(item => item.readiness === 'Ready');
      if (choices.length < 2) return;
      const current = selectedMedia(latestMedia).get(slot);
      const next = choices[(choices.findIndex(item => item.mediaId === current?.mediaId) + 1) % choices.length];
      mediaSelection.set(slot, next.mediaId);
      const match = latestRun?.results?.find(x => x.kind !== 'Face' && x.kind !== 'Group' && x.id === next.objectId);
      if (match) { resultFocus = { kind: match.kind, id: match.id, localFace: next.localFace }; resultFocusRun = runId; }
      const selectionRun = runId;
      await loadMedia(latestMedia);
      if (runId !== selectionRun) return;
      render(latestStatus, latestRun, latestEvidence);
    });
  });
  if (typeof manualRemovalButton?.addEventListener === 'function') manualRemovalButton.addEventListener('click', async () => {
    const recoveryAction = latestRun?.failedCommandRecovery && latestRun.allowedActions?.find(x =>
      x === 'RecoveryReset' || x === 'RecoveryCheck');
    if (!commandPending && runId && recoveryAction) {
      const reason = document.getElementById('manualRemovalReason')?.value?.trim();
      if (!reason) { interactionNotice = '请填写故障原因或实物与原任务核对依据'; render(latestStatus, latestRun, latestEvidence); return; }
      commandPending = true;
      manualRemovalButton.disabled = true;
      try {
        if (recoveryAction === 'RecoveryReset') {
          await post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/recovery-reset`, {
            requestId: crypto.randomUUID(), expectedRevision: latestRun.observedRevision, reason
          });
          interactionNotice = '已观察设备复位就绪；仍须核对实物、原任务及冻结配置';
          const reasonInput = document.getElementById('manualRemovalReason');
          if (reasonInput) reasonInput.value = '';
        } else {
          const check = await post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/recovery-checks`, {
            requestId: crypto.randomUUID(), expectedRevision: latestRun.observedRevision,
            sameTray: true, loadingUnchanged: true, snapshotStillApplicable: true,
            reason, evidenceRefs: [`OperatorPhysicalCheck:${reason}`]
          });
          interactionNotice = '初始核验已提交；请通过既有启动控件显式启动完整新轮';
        }
        diagnostic('RecoveryOperationAccepted', { runId, recoveryAction });
      } catch (error) {
        interactionNotice = `恢复受限：${error.contract?.code || error.message}；保留原任务并核查`;
      } finally { commandPending = false; await refresh(); }
      return;
    }
    if (commandPending || !runId ||
        !latestRun?.allowedActions?.includes('ConfirmManualTrayRemoval')) return;
    const reason = document.getElementById('manualRemovalReason')?.value?.trim();
    if (!reason) { interactionNotice = '请填写取盘确认原因'; render(latestStatus, latestRun, latestEvidence); return; }
    commandPending = true;
    manualRemovalButton.disabled = true;
    try {
      const confirmation = await post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/manual-removal-confirmations`, {
        requestId: crypto.randomUUID(), expectedRevision: latestRun.observedRevision, reason
      });
      interactionNotice = `取盘确认已受理；${confirmation?.state || '等待查询最终提交'}`;
      diagnostic('ManualRemovalAccepted', { runId, finalEventId: confirmation?.finalEventId });
      await refresh();
    } catch (error) {
      interactionNotice = `取盘确认受限：${error.contract?.code || error.message}；查询原运行核对`;
      diagnostic('ManualRemovalRejected', { runId, code: error.contract?.code || 'Unknown' });
      await refresh();
    } finally { commandPending = false; render(latestStatus, latestRun, latestEvidence); }
  });
  const startButton = Array.from(document.querySelectorAll('button')).find(button => button.textContent.includes('启动'));
  if (startButton) startButton.addEventListener('click', async () => {
    const preparedRequest = preparedSelection();
    diagnostic('StartClicked', { commandPending, startSubmitted,
      preparedValid: legalPreparedRequest(preparedRequest) });
    if (commandPending) { interactionNotice = '启动请求处理中；查询原请求，不重复提交'; render(latestStatus, latestRun, latestEvidence); return; }
    const faultRestart = latestRun?.faultRestart;
    const restartReady = runId && latestRun?.allowedActions?.includes('RestartFullRun') && faultRestart?.status === 'InitialReady';
    if (runId && !restartReady) { interactionNotice = `已有运行 ${runId}，不能再次启动；仅查询原任务或请授权人员核查`; render(latestStatus, latestRun, latestEvidence); return; }
    if (startSubmitted && !restartReady) { interactionNotice = '启动请求已发送；查询原请求，不重复提交'; render(latestStatus, latestRun, latestEvidence); return; }
    if (!legalPreparedRequest(preparedRequest)) {
      diagnostic('StartRejectedLocally', { code: 'PreparedRequestInvalid', postSent: false });
      setText('faultList', 'Test上料请求未准备或配置不符'); return;
    }
    startSubmitted = true; commandPending = true; setText('verdictBig', '—');
    try {
      diagnostic('StartPosting', { publicConfigRef: prepared.publicConfigRef,
        budgetRef: prepared.budgetRef, simulationRef: prepared.simulationRef });
      const request = restartReady ? { ...preparedRequest, requestId: crypto.randomUUID(),
        restartFrom: { faultRunId: runId, resetId: faultRestart.resetId,
          initialCheckId: faultRestart.initialCheckId, expectedFaultRevision: latestRun.observedRevision } } : preparedRequest;
      receipt = await window.station01.start(request);
      lastStartFailure = null;
      lastStartHttpStatus = null;
      interactionNotice = null;
      if (receipt?.runId !== runId) {
        latestRun = null; latestEvidence = null; latestMedia = null;
        resultFocus = null; resultFocusRun = null;
      }
      runId = receipt?.runId || null;
      if (runId) rememberRun(runId);
      diagnostic('StartAccepted', { commandId: receipt?.commandId, acceptedNotCompleted: true });
      setText('verdictBig', '—');
      window.dispatchEvent(new CustomEvent('station01:command', { detail: { requestId: prepared.requestId, commandId: receipt?.commandId, runId, accepted: true } }));
      await refresh();
    } catch (error) {
      diagnostic('StartFailed', { code: error.contract?.code || 'ResponseUnknown',
        httpStatus: error.contract?.httpStatus, traceId: error.contract?.traceId,
        runCreated: error.contract?.details?.runCreated, disposition: 'QueryOnly_NoAutomaticResend' });
      lastStartHttpStatus = error.contract?.httpStatus;
      lastStartFailure = [401, 403].includes(lastStartHttpStatus)
        ? `后端拒绝访问（${lastStartHttpStatus}）；请核对登录与权限`
        : `${error.message || '启动受限'}；${error.contract?.details?.runCreated === false ? '未创建运行' : '受理状态未知'}；${error.contract?.details?.action || '仅查询或联系授权人员核查'}`;
      setText('verdictBig', 'Unknown'); setText('faultList', `请求 ${error.contract?.details?.requestId || prepared?.requestId || '未知'}；${lastStartFailure}`);
      window.dispatchEvent(new CustomEvent('station01:error', { detail: error.contract || {} }));
    } finally { commandPending = false; render(latestStatus, latestRun, latestEvidence); }
  });
  if (document.getElementById('moduleGrid') || document.getElementById('partsTbody')) {
    clearPrototypeDemo();
    render(null, null, null);
    void refresh();
    void connectNotifications().catch(() => window.dispatchEvent(new CustomEvent('station01:notification-error')));
    setInterval(() => void refresh(), 2000);
  }
})();
