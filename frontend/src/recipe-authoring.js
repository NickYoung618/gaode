(function (root) {
  'use strict';

  // Presentation support only: reject fields this form cannot represent; no business/value validation.
  function unsupportedFields(definition) {
    const fields = names => Object.fromEntries(names.split(' ').map(name => [name, null]));
    const point = fields('id version x y z unit frame'), planar = fields('id version x y unit frame');
    const fixed = fields('z unit datum approvalReference configurationVersion');
    const handling = { point, coordinateEvidenceReference: null };
    const pose = fields('profileId profileVersion poseKey');
    const auxiliary = { target: handling, actionTimeoutMs: null };
    const object = { originPutBack: handling, sortingCellIds: { "*": null }, source: handling, coordinates: [{ ...fields('pointRef objectPattern slotId physicalSlotIndex localFace camera stageId configurationVersion sourceFactReference captureProfile'), point: planar, fixed }],
      flip: { stages: { '*': { targetPose: pose, pickPointRef: null, putBackPointRef: null } } },
      rotation: { entry: auxiliary, poses: { '*': auxiliary }, exits: { '*': auxiliary } }, sorting: { '*': handling },
      purposePoints: { '*': { purpose: null, point: planar, fixed, coordinateEvidenceReference: null, captureProfile: null } } };
    const body = { ...fields('schemaVersion recipeId version definitionDigest catalogDigest releaseStatus plcRecipeId fCode model scenarioId unitKind primaryMaterial layoutProfile capacity ngCapacity pendingCapacity route motionProfile qualityProfile sortingGripperId inspectionKind rotationLoadingGripperId'),
      lightExecution: fields('schemaVersion mode'),
      commissioningFPosition: fields('schemaVersion x y'),
      trayLayout: { rows:null, columns:null, cells:[fields('cellId row column region')] },
      traySlotMapping: { ...fields('id version evidenceReference'), bindings:[fields('cellId physicalSlotIndex')] },
      rotationWorkstation: { place:handling, pick:handling }, sortingTargets:{ '*':handling },
      approval: fields('id version digest purpose allowedSlots evidenceReference'),
      positions: [{ ...fields('slotId cellId physicalSlotIndex unitPattern'), members: [fields('material memberPattern handling cellId physicalSlotIndex')] }],
      composition: [fields('material localFaces sortingGripperId')],
      stages: [{ ...fields('number action angleDeg'), targets: [fields('material localFace cameraPair captureProfile algorithmProfile')] }],
      executionPositions: { '*': { slotId: null, physicalEntity: object, members: { '*': object } } },
      captureProfiles: { '*': { id: null, version: null, settings: fields('profileId exposureUs gain roiPixels lightChannel brightnessPercent settleMs') } },
      algorithmRequirements: { '*': fields('id parametersVersion purpose inputCount resultContract') },
      disposition: fields('ok ng pending physicalUnit'),
      eCode: { ...fields('enabled representativeMaterial bindTo requiredForOk readAt captureProfile algorithmProfile scanPointRef'),
        extraPose: { ...fields('poseId scanPointRef pickPointRef putBackPointRef captureProfile algorithmProfile'), targetPose: pose } } };
    const unknown = [];
    function visit(value, shape, path) {
      if (!value || !shape || typeof value !== 'object') return;
      if (Array.isArray(shape)) { if (Array.isArray(value)) value.forEach((v, i) => visit(v, shape[0], `${path}[${i}]`)); return; }
      for (const [key, item] of Object.entries(value)) {
        if (!Object.hasOwn(shape, key) && !Object.hasOwn(shape, '*')) unknown.push(`${path}.${key}`);
        else visit(item, Object.hasOwn(shape, key) ? shape[key] : shape['*'], `${path}.${key}`);
      }
    }
    visit(definition, body, 'definition'); return unknown;
  }

  // UI session only. Definitions and issues remain the common API's complete values.
  // This module neither validates business rules nor creates recipe identities/plans.
  function createSession({ send, requestId, requestTimeoutMs, changed = () => {} }) {
    if (!Number.isFinite(requestTimeoutMs) || requestTimeoutMs <= 0)
      throw new TypeError('A finite authoring request timeout is required');
    let generation = 0;
    let configurationSourceId = null, authoringContext = null;
    const pending = new Set();
    const empty = () => ({ phase: 'Closed', section: 'basic', definition: null, recipeId: null,
      etag: null, catalog: [], canSave: false, canValidate: false, issues: [], message: '', saved: null, refreshing: false,
      unsupported: [] });
    let current = empty();
    const busy = () => current.refreshing || ['Configuring', 'Saving', 'Checking'].includes(current.phase);
    const snapshot = () => structuredClone(current);
    const emit = () => changed(snapshot());
    function clearPending() { for (const controller of pending) controller.abort(); pending.clear(); }
    function begin() { generation++; clearPending(); return generation; }
    async function call(path, init = {}) {
      const controller = new AbortController(); pending.add(controller);
      const timeout = setTimeout(() => controller.abort(), requestTimeoutMs);
      try { return await send(path, { ...init, signal: controller.signal }); }
      finally { clearTimeout(timeout); pending.delete(controller); }
    }
    function fail(error, saving = false) {
      const status = error.status ?? error.contract?.httpStatus;
      const details = error.details ?? error.contract?.details;
      const unknown = saving && (!status || details?.result === 'CommitUnknown' || details?.saveResult === 'CommitUnknown');
      current.phase = unknown ? 'Unknown' : 'Rejected';
      current.issues = details?.issues || [];
      current.message = unknown ? '保存结果未确认，请重新读取核对；未自动重发。' : details?.reason || error.message;
      if (status === 401 || status === 403) current.canSave = false;
      emit();
    }
    function writable(checking) {
      if (current.phase === 'Closed' || busy() || !current.definition) return false;
      if (current.unsupported.length) { current.message = '正文含未支持字段，已限制编辑：' + current.unsupported.join('、'); emit(); return false; }
      if (!(checking ? current.canValidate : current.canSave)) {
        current.message = checking ? '没有检查权限。' : '没有保存权限。'; emit(); return false;
      }
      return true;
    }
    async function catalog() {
      const response = await call('/api/v1/recipes/catalog');
      return { catalog: response.value.items || [], canSave: response.value.authoringAccess?.canSave === true,
        canValidate: response.value.authoringAccess?.canValidate === true };
    }
    function adopt(response) {
      if (!response.value?.definition) throw new Error('未返回完整配方，不能编辑目录摘要。');
      current.definition = structuredClone(response.value.definition);
      current.unsupported = unsupportedFields(current.definition);
      current.recipeId = current.definition.recipeId;
      current.etag = response.etag || null;
      authoringContext = { sourceRecipeId: current.definition.recipeId, sourceVersion: current.definition.version };
      current.saved = { savedAt: response.value.savedAt, requestId: response.value.requestId };
    }
    return Object.freeze({
      get state() { return snapshot(); },
      async open() {
        const id = begin(); current = { ...empty(), phase: 'Loading' }; emit();
        try {
          const value = await catalog(); if (id !== generation) return;
          Object.assign(current, value, { phase: 'Editing', message: '选择配方读取，或新建。' }); emit();
        } catch (error) { if (id === generation) fail(error); }
      },
      close() { begin(); configurationSourceId = null; authoringContext = null; current = empty(); emit(); },
      section(name) { if (['basic', 'points', 'review'].includes(name)) { current.section = name; emit(); } },
      create(definition) {
        if (current.phase === 'Closed' || busy() || !current.canSave) return;
        configurationSourceId = current.recipeId;
        begin(); Object.assign(current, { phase: 'Editing', section: 'basic', definition: structuredClone(definition),
          recipeId: null, etag: null, issues: [], message: '新建内容尚未保存。', saved: null, unsupported: unsupportedFields(definition) }); emit();
      },
      async configure() {
        if (current.recipeId || !current.definition?.model || !current.definition?.unitKind || !current.definition?.scenarioId || !current.definition?.inspectionKind || busy()) return;
        const id = begin(), basic = { commissioningFPosition: current.definition.commissioningFPosition, lightExecution: current.definition.lightExecution, fCode: current.definition.fCode, sortingGripperId: current.definition.sortingGripperId, rotationLoadingGripperId: current.definition.rotationLoadingGripperId };
        current.phase = 'Configuring'; emit();
        try {
          const response = await call('/api/v1/recipes/editor-draft', { method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ model: current.definition.model, scenarioId: current.definition.scenarioId,
              unitKind: current.definition.unitKind, inspectionKind: current.definition.inspectionKind,
              sourceRecipeId: current.catalog.some(c => c.recipeId === configurationSourceId && c.model === current.definition.model &&
                c.scenarioId === current.definition.scenarioId && c.unitKind === current.definition.unitKind &&
                c.inspectionKind === current.definition.inspectionKind) ? configurationSourceId : null }) });
          if (id !== generation) return;
          authoringContext = response.value.authoringContext;
          current.definition = { ...response.value.definition, ...basic }; current.unsupported = unsupportedFields(current.definition);
          current.phase = 'Editing'; current.message = '请填写当前配方的坐标和拍照参数。'; emit();
        } catch (error) { if (id === generation) fail(error); }
      },
      async layout(layout) {
        if (!current.definition || busy() || !current.canSave) return;
        const id = begin(); current.phase = 'Configuring'; current.message = '正在更新配置…'; emit();
        try {
          const response = await call('/api/v1/recipes/editor-layout', { method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ definition: current.definition, ...authoringContext, ...layout }) });
          if (id !== generation) return;
          current.definition = response.value.definition; current.unsupported = unsupportedFields(current.definition);
          current.phase = 'Editing'; current.message = '内容已修改，尚未保存。'; emit();
        } catch (error) { if (id === generation) fail(error); }
      },
      edit(change) {
        if (current.phase === 'Closed' || current.phase === 'Loading' || busy() || !current.canSave || !current.definition || current.unsupported.length) return;
        if (change(current.definition) === false) { current.message = '修改未应用：缺少对应参数或来源，请核对配方'; emit(); return; }
        current.phase = 'Editing'; current.issues = [];
        current.message = '内容已修改，尚未保存。'; emit();
      },
      async load(recipeId) {
        if (current.phase === 'Closed' || busy() || !recipeId) return;
        const id = begin(); Object.assign(current, { phase: 'Loading', definition: null, recipeId: null,
          etag: null, issues: [], message: '正在读取完整配方…' }); emit();
        try {
          const response = await call('/api/v1/recipes/' + encodeURIComponent(recipeId));
          if (id !== generation) return;
          adopt(response); current.phase = 'Editing'; current.message = '已读取保存内容。'; emit();
        } catch (error) { if (id === generation) fail(error); }
      },
      async check() {
        if (!writable(true)) return;
        const id = generation; current.phase = 'Checking'; current.message = '正在检查…'; emit();
        try {
          const response = await call('/api/v1/recipes/validate', { method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ requestId: requestId(), definition: current.definition }) });
          if (id !== generation) return;
          const value = response.value;
          current.issues = value.issues || [];
          current.phase = value.valid === true ? 'Editing' : 'Rejected';
          current.message = value.valid === true ? '检查通过，尚未保存；生产准入按后端限制。' : '检查未通过，请查看问题。'; emit();
        } catch (error) { if (id === generation) fail(error); }
      },
      async save() {
        if (!writable(false)) return;
        if (current.recipeId && !current.etag) {
          fail(new Error('缺少读取版本，请重新读取后保存。')); return;
        }
        const id = generation, target = current.recipeId;
        const headers = { 'Content-Type': 'application/json' };
        if (target) headers['If-Match'] = current.etag;
        const body = JSON.stringify({ requestId: requestId(), definition: current.definition });
        current.phase = 'Saving'; current.issues = []; current.message = '正在保存…'; emit();
        try {
          const response = await call('/api/v1/recipes' + (target ? '/' + encodeURIComponent(target) : ''),
            { method: target ? 'PUT' : 'POST', headers, body });
          if (id !== generation) return;
          if (!response.value?.definition?.recipeId || !response.value.definition.version ||
              !response.value.definition.definitionDigest) throw new Error('保存回执缺少已保存身份');
          adopt(response); current.phase = 'Saved'; current.refreshing = true; current.message = '已保存，正在重读。'; emit();
        } catch (error) { if (id === generation) fail(error, true); return; }
        // COMMIT confirmation and subsequent refresh are deliberately separate facts.
        try {
          const response = await call('/api/v1/recipes/' + encodeURIComponent(current.recipeId));
          if (id !== generation) return;
          adopt(response); current.message = '已保存并重读；已冻结运行继续使用原内容。';
          const refreshed = await catalog(); if (id !== generation) return;
          Object.assign(current, refreshed);
        } catch (_) {
          if (id !== generation) return;
          current.message = '已保存；完整内容或目录重读失败，请重新读取核对。';
        }
        if (id === generation) { current.phase = 'Saved'; current.refreshing = false; emit(); }
      }
    });
  }

  function newDraft(context) {
    // Empty editing content, not a valid or approved recipe. No example quantities/coordinates.
    return { schemaVersion: 'recipe-definition/5', recipeId: '', version: '', definitionDigest: '', catalogDigest: '',
      releaseStatus: '', approval: { id: '', version: '', digest: '', purpose: '', allowedSlots: [], evidenceReference: '' },
      trayLayout: { rows:10, columns:10, cells:[] }, traySlotMapping:null, sortingTargets:{},
      inspectionKind:context?.inspectionKind || null, rotationLoadingGripperId:null, rotationWorkstation:null,
      sortingGripperId: null, plcRecipeId: null, fCode: '', model: context?.model || '', scenarioId: context?.scenarioId || '', unitKind: context?.unitKind || '', primaryMaterial: '', layoutProfile: '', lightExecution:{schemaVersion:'light-execution/1',mode:'Simulated'}, commissioningFPosition:{schemaVersion:'commissioning-f-position/1',x:null,y:null},
      capacity: null, ngCapacity: null, pendingCapacity: null, route: '', motionProfile: '', qualityProfile: '',
      positions: [], composition: [], stages: [], executionPositions: {}, captureProfiles: {}, algorithmRequirements: {},
      disposition: { ok: 'originalSlot', ng: 'NG', pending: 'Pending', physicalUnit: '' },
      eCode: { enabled: null, representativeMaterial: null, bindTo: null, requiredForOk: null, readAt: null,
        captureProfile: null, algorithmProfile: null, scanPointRef: null, extraPose: null } };
  }

  const clone = value => structuredClone(value);
  function orderedCells(definition) {
    const counts = { NG:0, OK:0, Pending:0 };
    return [...(definition.trayLayout?.cells || [])].sort((a,b) => a.row-b.row || a.column-b.column)
      .map(cell => ({ ...cell, ordinal: ++counts[cell.region] }));
  }
  function changeCell(definition, row, column, region) {
    const cellId = `r${row}:c${column}`, cells = definition.trayLayout?.cells || [];
    const previous = cells.find(c => c.cellId === cellId);
    return { rows:10, columns:10, cells:[...cells.filter(c => c.cellId !== cellId),
      ...(previous?.region === region ? [] : [{cellId,row,column,region}])] };
  }
  function objects(definition) {
    return (definition.positions || []).flatMap(position => (position.members || []).map(member => {
      const slot = definition.executionPositions?.[position.slotId];
      const detection = slot?.members?.[member.material] || slot?.physicalEntity;
      const handling = definition.unitKind === 'looseGroup' ? slot?.members?.[member.material] : slot?.physicalEntity;
      return { slotId: position.slotId, slot: definition.unitKind === 'looseGroup' ? member.physicalSlotIndex : position.physicalSlotIndex, member: member.material,
        cellId: definition.unitKind === 'looseGroup' ? member.cellId : position.cellId, name: definition.unitKind === 'independentPart' ? `槽位 ${position.physicalSlotIndex}` : `槽位 ${definition.unitKind === 'looseGroup' ? member.physicalSlotIndex : position.physicalSlotIndex} / ${member.material}`, detection, handling };
    })).filter(item => item.detection && item.handling);
  }
  function cards(definition, item, area) {
    const result = [], add = (title, point, camera, reference, owner) => {
      if (point) result.push({ title, point, camera, reference, owner, item });
    };
    if (area === 'NG' || area === 'Pending') add(`${area}分拣放置点`, item.handling.sorting?.[area]?.point);
    else if (area === 'E') {
      const e = definition.eCode;
      if (e.enabled && e.representativeMaterial === item.member) {
        const ref = e.extraPose?.scanPointRef || e.scanPointRef;
        const point = item.detection.purposePoints?.[ref];
        add('E相机扫码点', point, 'E', ref, item.detection);
        if (e.extraPose) add('最终分拣抓取点', item.handling.source?.point);
      }
    } else if (area.startsWith('face:') || area.startsWith('group:')) {
      const group = area.startsWith('group:') ? Number(area.slice(6)) : null;
      const face = group ? definition.stages.find(s => s.number === group)?.targets.find(t => t.material === item.member)?.localFace : Number(area.slice(5));
      const targets = definition.stages.filter(s => !group || s.number === group).flatMap(s => s.targets).filter(t => t.material === item.member && t.localFace === face);
      for (const coordinate of item.detection.coordinates || [])
        if ((!group || coordinate.stageId === `stage:${group}`) && coordinate.localFace === face && targets.some(t => t.cameraPair.includes(coordinate.camera))) add(`${coordinate.camera}相机拍照点`, coordinate, coordinate.camera, coordinate.pointRef, item.detection);
      if (group) return result;
      const index = definition.stages.findIndex(stage => stage.targets.some(t => t.material === item.member && t.localFace === face));
      const next = definition.stages.slice(index + 1).find(stage => stage.targets.some(t => t.material === item.member));
      if (next) {
        // Present the next face's already configured transition on the current face, as in V3.
        const target = next.targets.find(t => t.material === item.member);
        const coordinate = item.detection.coordinates.find(c => c.localFace === target.localFace && target.cameraPair.includes(c.camera));
        const transition = item.handling.flip?.stages?.[coordinate?.stageId];
        if (transition) {
          add('翻面取件', item.handling.purposePoints?.[transition.pickPointRef]);
          add('翻面放回', item.handling.purposePoints?.[transition.putBackPointRef]);
        }
      } else if (definition.eCode.enabled && definition.eCode.extraPose && definition.eCode.representativeMaterial === item.member) {
        const extra = definition.eCode.extraPose;
        add('翻面取件', item.handling.purposePoints?.[extra.pickPointRef]);
        add('翻面放回', item.handling.purposePoints?.[extra.putBackPointRef]);
      } else add('最终分拣抓取点', item.handling.source?.point);
    }
    else if (area === 'source') add('料盘取料点', item.handling.source?.point);
    else if (area === 'origin') add('原槽放料点', item.handling.originPutBack?.point);
    return result;
  }
  function profileFor(definition, card) {
    const own = card.point.captureProfile;
    if (own) return definition.captureProfiles?.[own];
    // A historical value is real configured data, not a default; current writes acquire a local reference.
    const target = definition.stages?.filter(s => !card.point.stageId || `stage:${s.number}` === card.point.stageId).flatMap(s => s.targets).find(t => t.material === card.item.member && t.localFace === card.point.localFace);
    const reference = card.camera === 'E' ? definition.eCode.extraPose?.captureProfile || definition.eCode.captureProfile : target?.captureProfile;
    return definition.captureProfiles?.[reference];
  }
  // Projection of existing inputs only; material names and stages come from the full API body.
  function facesFor(definition, item) {
    return definition.composition.find(c => c.material === item.member)?.localFaces || [];
  }
  function navigationCards(definition, item, purpose, face) {
    if (purpose === 'photo') return cards(definition, item, 'face:' + face).filter(c => c.camera);
    if (purpose === 'E') return cards(definition, item, 'E').filter(c => c.camera);
    if (purpose === 'sort') return cards(definition, item, 'source');
    if (purpose !== 'flip') return cards(definition, item, purpose);
    // All valid transitions remain editable. An assembled part has one physical handling owner.
    const result = [], seen = new Set();
    const transitions = Object.entries(item.handling.flip?.stages || {});
    if (definition.eCode?.extraPose && (definition.unitKind === 'assembledEntity' || definition.eCode.representativeMaterial === item.member))
      transitions.push(['E', definition.eCode.extraPose]);
    for (const [stageId, transition] of transitions) {
      for (const [key, title] of [['pickPointRef','翻面取料'], ['putBackPointRef','翻面放回']]) {
        const reference = transition[key], point = item.handling.purposePoints?.[reference];
        if (!point || seen.has(reference)) continue;
        seen.add(reference);
        result.push({title: (definition.unitKind === 'assembledEntity' ? '整体' : item.member) + title,
          point, reference, owner: item.handling, item, transitionStageId: stageId});
      }
    }
    return result;
  }
  function editCapture(definition, card, key, value) {
    const basis = profileFor(definition, card);
    if (!basis) return false;
    const own = card.point.captureProfile;
    const usages = Object.values(definition.executionPositions || {}).flatMap(slot => [slot.physicalEntity, ...Object.values(slot.members || {})])
      .flatMap(input => [...(input.coordinates || []), ...Object.values(input.purposePoints || {})]).filter(point => point.captureProfile === own).length;
    const reference = own?.startsWith('photo-editor-') && usages === 1 ? own :
      ['capture', card.item.slotId, card.item.member, card.point.stageId, card.reference, card.camera].map(encodeURIComponent).join('/');
    const profile = clone(basis);
    profile.id = reference; profile.settings.profileId = reference; profile.settings[key] = value;
    definition.captureProfiles[reference] = profile;
    card.point.captureProfile = reference;
    return true;
  }
  function migrateForSave(definition) {
    if (!definition) return;
    for (const item of objects(definition))
      for (const area of [...new Set(item.detection.coordinates.map(c => 'face:' + c.localFace)), 'E'])
        for (const card of cards(definition, item, area))
          if (card.camera && !card.point.captureProfile) {
            const profile = profileFor(definition, card);
            if (profile) editCapture(definition, card, 'exposureUs', profile.settings.exposureUs);
          }
    // Historical bodies require explicit layout editing; never manufacture a migration.
    if (definition.trayLayout && definition.inspectionKind) definition.schemaVersion = 'recipe-definition/5';
  }
  function mount({ document, send, requestId, requestTimeoutMs }) {
    const panel = document.getElementById('recipeAuthoringForm'); if (!panel) return null;
    let state, area = 'face:1', selected = null, selectedCell = null, selectedFace = null, paintRegion = 'NG';
    const node = (tag, text, className = '') => { const e = document.createElement(tag); e.className = className; if (text != null) e.textContent = text; return e; };
    const session = createSession({ send, requestId, requestTimeoutMs, changed: render });
    function button(parent, label, action, className = 'hmi-btn') {
      const b = node('button', label, className); b.type = 'button'; b.onclick = action; parent.append(b); return b;
    }
    function input(parent, label, value, set, type = 'number', options = null) {
      const row = node('label', null, 'recipe-input'); row.append(node('span', label));
      const e = node(options ? 'select' : 'input', null, 'hmi-input');
      if (options) { e.append(new Option('请选择', '')); options.forEach(([key, title]) => e.append(new Option(title, String(key)))); }
      else { e.type = type; if (type === 'number') e.step = 'any'; e.placeholder = '未填写'; }
      e.value = value ?? ''; e.setAttribute('aria-label', label);
      e.onchange = () => set(type === 'number' ? e.value === '' ? null : Number(e.value) : e.value);
      row.append(e); parent.append(row); return e;
    }
    function editCard(card, change) {
      session.edit(d => {
        if (card.item.slotId === 'common') {
          const actual = displayedCards(d,card.item,'station').find(c => c.reference === card.reference);
          return actual ? change(d,actual) : false;
        }
        const cell = d.trayLayout?.cells.find(c => c.cellId === card.item.cellId);
        if (cell && cell.region !== 'OK') {
          const actual = { ...card,point:d.sortingTargets[cell.cellId].point };
          return change(d,actual);
        }
        const item = objects(d).find(it => it.slotId === card.item.slotId && it.member === card.item.member);
        const actual = item && displayedCards(d,item,area).find(c => c.title === card.title && c.reference === card.reference &&
          c.camera === card.camera && c.point.stageId === card.point.stageId && c.transitionStageId === card.transitionStageId);
        return actual ? change(d,actual) : false;
      });
    }
    function displayedCards(d,item,section) {
      if (section === 'station') return ['place','pick'].filter(key => d.rotationWorkstation?.[key])
        .map(key => ({ title:key === 'place' ? '旋转工位放料点' : '旋转工位取料点',
          point:d.rotationWorkstation[key].point,reference:key,item }));
      return usesObjectNavigation(d) && !['NG','Pending'].includes(section)
        ? navigationCards(d,item,section,selectedFace) : cards(d,item,section);
    }
    function cellLabel(d,cell) {
      const current = orderedCells(d).find(c => c.cellId === cell.cellId);
      return `${cell.region}区 · 槽 ${current?.ordinal} · 第${cell.row}行 · 第${cell.column}列`;
    }
    function renderMatrix(parent,d,editable) {
      const board = node('section',null,'recipe-matrix-panel'); parent.append(board);
      if (editable) {
        board.append(node('h2','料盘区域配置'));
        const tools = node('div',null,'recipe-paint-tools'); board.append(tools);
        for (const region of ['NG','OK','Pending']) button(tools,region,()=>{paintRegion=region;render(session.state);},
          'hmi-btn'+(paintRegion === region ? ' active' : ''));
      }
      board.append(node('p',['NG','OK','Pending'].map(region => `${region} ${orderedCells(d).filter(c=>c.region===region).length}`).join(' · '),'recipe-note'));
      const grid = node('div',null,'recipe-matrix');grid.setAttribute('aria-label','10行10列料盘'+(editable?'区域选择':'坐标定位'));board.append(grid);
      const cells = orderedCells(d);
      if (!editable && !cells.some(c => c.cellId === selectedCell)) selectedCell = cells.find(c => c.region === 'OK')?.cellId ?? cells[0]?.cellId ?? null;
      grid.append(node('span','行/列','recipe-matrix-axis'));
      for(let column=1;column<=10;column++)grid.append(node('span',column,'recipe-matrix-axis'));
      for(let row=1;row<=10;row++) {
        grid.append(node('span',row,'recipe-matrix-axis'));
        for(let column=1;column<=10;column++) {
          const cellId=`r${row}:c${column}`,cell=cells.find(c=>c.cellId===cellId);
          if (!editable && !cell) {
            const empty=node('span',null,'recipe-matrix-empty');empty.dataset.emptyCell=cellId;grid.append(empty);continue;
          }
          const b=button(grid,cell?`${cell.region} ${cell.ordinal}`:'+',()=>{
            if(editable)void session.layout({trayLayout:changeCell(d,row,column,paintRegion)});
            else {selectedCell=cellId;selected=null;selectedFace=null;if(cell.region!=='OK')area=cell.region;else if(['NG','Pending'].includes(area))area=d.inspectionKind==='specialRotation'?'source':usesObjectNavigation(d)?'photo':sections(d).find(s=>s.id.startsWith('face:'))?.id;choose(area);}
          },'recipe-matrix-cell'+(cell?' area-'+cell.region.toLowerCase():'')+(selectedCell===cellId&&!editable?' selected':''));
          b.dataset.cellId=cellId;b.setAttribute('aria-label',cell?cellLabel(d,cell):`第${row}行第${column}列，未选择`);
          if(editable){b.setAttribute('role','checkbox');b.setAttribute('aria-checked',String(Boolean(cell)));}
          else b.setAttribute('aria-pressed',String(selectedCell===cellId));
        }
      }
      board.append(node('p',editable?'先选择区域再勾选；同区再次点击取消，改区只清理本格配置。':'位置与第一步一致；填写状态不代表保存、质量或实际到位。','recipe-note'));
    }
    const usesObjectNavigation = d => ['looseGroup','assembledEntity'].includes(d.unitKind);
    const sections = d => d.inspectionKind === 'specialRotation'
      ? [{id:'station',name:'旋转工位'},{id:'source',name:'料盘取料'},
          ...d.stages.map(s => ({id:'group:'+s.number,name:s.number === 1 ? '第一组检测' : '第二组检测'})),
          {id:'origin',name:'原槽放料'},{id:'NG',name:'NG区域'},{id:'Pending',name:'Pending区域'},
          ...(d.eCode?.enabled ? [{id:'E',name:'E扫码'}] : [])]
      : usesObjectNavigation(d) ? [{id:'photo',name:'拍照位置'},{id:'flip',name:'翻面取放'},
        ...(d.eCode?.enabled ? [{id:'E',name:'E扫码位置'}] : []),{id:'sort',name:'NG / Pending分拣'}]
      : [{ id: 'NG', name: 'NG区域' }, { id: 'Pending', name: 'Pending区域' },
        ...[...new Set(d.composition?.flatMap(c => c.localFaces) || [])].sort((a,b) => a-b).map(face => ({ id: 'face:' + face, name: '第' + face + '检测面' })),
        ...(d.eCode?.enabled ? [{ id: 'E', name: 'E扫码' }] : [])];
    function complete(d, card) {
      const p = card.point, xyz = p.fixed ? [p.point.x, p.point.y, p.fixed.z] : [p.x, p.y, p.z];
      return xyz.every(v => Number.isFinite(v)) && (!card.camera || (d.lightExecution?.mode === 'Simulated' ? ['exposureUs', 'gain'] : ['exposureUs', 'gain', 'brightnessPercent']).every(k => Number.isFinite(profileFor(d, card)?.settings[k])));
    }
    const entries = (d, section) => section === 'station'
      ? displayedCards(d,{slotId:'common',member:null,name:'旋转工位'},section).map(card => ({it:card.item,card}))
      : ['NG','Pending'].includes(section)
        ? orderedCells(d).filter(c => c.region === section && d.sortingTargets?.[c.cellId])
          .map(c => ({slotId:c.cellId,cellId:c.cellId,member:null,name:cellLabel(d,c),handling:{sorting:{[section]:d.sortingTargets[c.cellId]}}}))
          .flatMap(it => cards(d,it,section).map(card => ({it,card})))
        : objects(d).flatMap(it => {
          const faces = usesObjectNavigation(d) && section === 'photo' ? facesFor(d,it) : [selectedFace];
          const all = faces.flatMap(face => usesObjectNavigation(d)
            ? navigationCards(d,it,section,face).map(card => ({it,card,face}))
            : cards(d,it,section).map(card => ({it,card})));
          // The overview must not count the same whole-assembly mechanical input per inspection part.
          return d.unitKind === 'assembledEntity' && ['flip','sort'].includes(section) &&
            objects(d).find(x => x.slotId === it.slotId)?.member !== it.member ? [] : all;
        });
    function choose(section, item = null) {
      area=section; selected=item;
      if (state?.definition) {
        const wanted=['NG','Pending'].includes(section)?section:'OK';
        if (!state.definition.trayLayout?.cells.some(c=>c.cellId===selectedCell&&c.region===wanted))
          selectedCell=orderedCells(state.definition).find(c=>c.region===wanted)?.cellId??null;
      }
      session.section('points');
    }
    function locate() {
      const d = state.definition;
      for (const section of sections(d)) for (const { it, card, face } of entries(d, section.id))
        if (!complete(d, card)) { selectedCell=it.cellId??null;selectedFace=face??null;choose(section.id, it.slotId + '/' + it.member); return; }
      session.section('basic');
    }
    function summary(parent, d) {
      parent.append(node('h2', d.fCode || '未命名配方'));
      for (const [label, value] of [['待检槽', (d.trayLayout?.cells.filter(c=>c.region==='OK').length??d.positions.length) + '个'], ['检测面', [...new Set(d.composition?.flatMap(c => c.localFaces) || [])].join(' / ')],
        ['E扫码', d.eCode?.enabled ? '需要' : '不需要'], ['分拣夹爪', d.unitKind === 'looseGroup' ? d.composition.map(m => m.material + '：' + (m.sortingGripperId ? '夹爪' + m.sortingGripperId : '未配置')).join('、') : d.sortingGripperId ? '夹爪' + d.sortingGripperId : '未配置']])
        parent.append(node('p', label + '：' + value));
      parent.append(node('p', '所有OK件最终在本件原始槽；特殊件检测后实际回放，NG / Pending进入对应区域。', 'recipe-note'));
    }
    async function check() { session.edit(migrateForSave); session.section('review'); await session.check(); }
    async function save() { session.edit(migrateForSave); session.section('review'); await session.save(); }
    function render(next) {
      state = next; const d = next.definition;
      const picker = document.getElementById('recipeAuthoringCatalog');
      picker.replaceChildren(new Option('选择已有配方', ''));
      for (const item of next.catalog) picker.append(new Option(`${item.fCode || item.model}`, item.recipeId));
      picker.value = next.recipeId || '';
      document.getElementById('recipeAuthoringNotice').textContent = next.message;
      const busy = next.refreshing || ['Loading', 'Configuring', 'Saving', 'Checking'].includes(next.phase);
      for (const [id, allowed] of [['recipeAuthoringCheck', next.canValidate], ['recipeAuthoringSave', next.canSave]]) document.getElementById(id).disabled = busy || !d || !allowed;
      picker.disabled = busy; document.getElementById('recipeAuthoringNew').disabled = busy || !next.canSave;
      for (const tab of document.querySelectorAll('[data-authoring-section]')) { tab.classList.toggle('active', tab.dataset.authoringSection === next.section); tab.setAttribute('aria-selected', String(tab.dataset.authoringSection === next.section)); }
      panel.replaceChildren();
      panel.dataset.unitKind=d?.unitKind??'';panel.dataset.pointPurpose=area;
      if (!d) { panel.append(node('p', '选择已有配方，或新建配方。')); return; }
      if (next.unsupported.length) { panel.append(node('p', '当前配方包含尚未支持的内容，暂时无法编辑。')); return; }
      if (next.section === 'basic') {
        const layout = node('div', null, 'recipe-setup'), form = node('section', null, 'recipe-panel'), overview = node('aside', null, 'recipe-panel recipe-summary');
        layout.append(form, overview); panel.append(layout); form.append(node('h2', '配方信息'));
        const grid = node('div', null, 'recipe-basic-grid'); form.append(grid);
        const virtualLabel = node('label', '虚拟光源 '), virtualLight = node('input');
        virtualLight.type = 'checkbox'; virtualLight.checked = d.lightExecution?.mode === 'Simulated';
        virtualLight.dataset.lightMode = 'recipe';
        virtualLight.onchange = () => session.edit(x => x.lightExecution = {schemaVersion:'light-execution/1',mode:virtualLight.checked?'Simulated':'Real'});
        virtualLabel.append(virtualLight); grid.append(virtualLabel);
        for (const [axis,label] of [['x','虚拟算法 F读码X (mm)'],['y','虚拟算法 F读码Y (mm)']])
          input(grid,label,d.commissioningFPosition?.[axis],v => session.edit(x => {
            x.commissioningFPosition ??= {schemaVersion:'commissioning-f-position/1',x:null,y:null};
            x.commissioningFPosition[axis]=v;
          }));
        input(grid, '料盘编号', d.fCode, v => session.edit(x => x.fCode = v), 'text');
        input(grid, '检测场景', d.unitKind, v => { session.edit(x => { x.unitKind = v; x.inspectionKind = v === 'independentPart' ? x.inspectionKind : 'ordinary'; const scenarios = [...new Set(state.catalog.filter(c => c.unitKind === v && c.model === x.model).map(c => c.scenarioId))]; x.scenarioId = scenarios.length === 1 ? scenarios[0] : ''; }); void session.configure(); }, 'text', [['independentPart','单品'],['looseGroup','成组'],['assembledEntity','半成品']]);
        input(grid, '零件型号', d.model, v => { session.edit(x => { x.model = v; const scenarios = [...new Set(state.catalog.filter(c => c.unitKind === x.unitKind && c.model === v).map(c => c.scenarioId))]; x.scenarioId = scenarios.length === 1 ? scenarios[0] : ''; }); void session.configure(); }, 'text',
          [...new Set([...next.catalog.map(c => c.model), ...(d.model ? [d.model] : [])])].map(model => [model,model]));
        if (d.unitKind !== 'looseGroup') input(grid, '分拣夹爪', d.sortingGripperId, v => session.edit(x => x.sortingGripperId = v), 'number', [[1,'夹爪1'],[2,'夹爪2']]);
        if (d.inspectionKind !== 'specialRotation') input(grid, '检测面数', d.composition.length ? Math.max(...d.composition.flatMap(c => c.localFaces)) : null, v => void session.layout({ faces: v }));
        if (d.unitKind === 'independentPart') input(grid, '检测类型', d.inspectionKind,
          v => { session.edit(x => x.inspectionKind = v); void session.configure(); }, 'text', [['ordinary','普通零件'],['specialRotation','特殊旋转零件']]);
        if (d.inspectionKind === 'specialRotation') input(grid, '旋转上料夹爪', d.rotationLoadingGripperId,
          v => session.edit(x => x.rotationLoadingGripperId = v), 'number', [[1,'夹爪1'],[2,'夹爪2']]);
        if (d.unitKind === 'looseGroup') {
          input(grid,'每组成员数',d.composition.length || null,v => void session.layout({ members:v }));
          form.append(node('h3', '成员组合模板'));
          for (const member of d.composition) {
            input(form, member.material + '检测面数', member.localFaces.length, v => void session.layout({ material: member.material, faces: v }));
            input(form, member.material + '分拣夹爪', member.sortingGripperId,
              v => session.edit(x => x.composition.find(m => m.material === member.material).sortingGripperId = v),
              'number', [[1,'夹爪1'],[2,'夹爪2']]);
          }
        }
        input(grid,'额外E扫码姿态',d.eCode?.extraPose ? 'yes' : 'no',v => void session.layout({ extraE:v === 'yes' }), 'text', [['no','不需要'],['yes','需要']]);
        summary(overview, d); renderMatrix(panel,d,true); button(form, '下一步：配置坐标 →', () => session.section('points'));
      } else if (next.section === 'points') {
        const list = sections(d); if (!list.some(s => s.id === area)) area = list[0]?.id;
        const layout = node('div', null, 'recipe-editor-layout'), nav = node('aside', null, 'recipe-panel recipe-nav'), slots = node('section', null, 'recipe-panel recipe-slots'), detail = node('section', null, 'recipe-panel recipe-detail');
        layout.append(nav, slots, detail); panel.append(layout);
        nav.append(node('h3', usesObjectNavigation(d) ? '坐标配置' : '区域与检测面'));
        for (const section of list) {
          const all = entries(d, section.id), n = all.filter(e => complete(d,e.card)).length;
          button(nav, `${section.name}  ${n}/${all.length}`, () => choose(section.id), 'hmi-btn recipe-nav-button' + (area === section.id ? ' active' : ''));
        }
        slots.append(node('h3', '料盘总览'));
        renderMatrix(slots,d,false);
        const currentGroup = d.unitKind === 'looseGroup' ? d.positions.find(position =>
          position.members.some(member => member.cellId === selectedCell)) : null;
        const items = objects(d).filter(it => (currentGroup ? it.slotId === currentGroup.slotId : it.cellId === selectedCell) && (usesObjectNavigation(d)
          ? area !== 'E' || d.eCode?.representativeMaterial === it.member : cards(d,it,area).length));
        if (!items.some(it => selected === it.slotId + '/' + it.member)) { const chosen = items.find(it => it.cellId === selectedCell) ?? items[0]; selected = chosen ? chosen.slotId + '/' + chosen.member : null; }
        let item = items.find(it => selected === it.slotId + '/' + it.member);
        const cell = (d.trayLayout?.cells || []).find(c => c.cellId === selectedCell);
        if (cell && cell.region !== 'OK') {
          area = cell.region;
          item = { cellId:cell.cellId,slotId:cell.cellId,member:null,name:cellLabel(d,cell),
            handling:{sorting:{[cell.region]:d.sortingTargets?.[cell.cellId]}} };
        }
        if (area === 'station') {
          item = {slotId:'common',member:null,name:'旋转工位'};
        }
        if (item) {
          detail.append(node('h2', cell ? cellLabel(d,cell) : item.name));
          if (usesObjectNavigation(d) && cell?.region === 'OK') {
            const mechanical = d.unitKind === 'assembledEntity' && ['flip','sort'].includes(area);
            // Whole handling does not inherit a part choice; the API physicalEntity is the owner.
            if (mechanical) item = items[0];
            if (!mechanical) {
              detail.append(node('h3', d.unitKind === 'looseGroup' ? '成员' : '检测部位'));
              const choices = node('div', null, 'recipe-face-rail recipe-object-rail'); detail.append(choices);
              for (const it of items) {
                const b = button(choices,it.member,()=>{selectedFace=null;choose(area,it.slotId+'/'+it.member);},
                  'hmi-btn recipe-small-button'+(selected===it.slotId+'/'+it.member?' active':''));
                b.dataset.objectMaterial=it.member;b.setAttribute('aria-pressed',String(selected===it.slotId+'/'+it.member));
                const all=facesFor(d,it).flatMap(face=>navigationCards(d,it,'photo',face));
                const status=node('small',all.length && all.every(c=>complete(d,c))?'已填写':'待填写','recipe-note');
                status.style.display='block';b.append(status);
              }
            }
            const faces=facesFor(d,item);
            if (!faces.includes(selectedFace)) selectedFace=faces[0]??null;
          }
          if (d.unitKind === 'looseGroup' && item.member) {
            input(detail,'成员物理槽号',item.slot,v => session.edit(x => {
              const position=x.positions.find(p=>p.slotId===item.slotId), member=position.members.find(m=>m.material===item.member);
              member.physicalSlotIndex=v;
              for(const c of x.executionPositions[item.slotId].members[item.member].coordinates)c.physicalSlotIndex=v;
            }));
            const member=x => x.positions.find(p=>p.slotId===item.slotId).members.find(m=>m.material===item.member);
            const field=node('label','成员格位（r行:c列） '), text=node('input');text.value=item.cellId || '';
            text.onchange=()=>session.edit(x=>member(x).cellId=text.value);field.append(text);detail.append(field);
          }
          const rail = node('div', null, 'recipe-face-rail'); detail.append(rail);
          if (usesObjectNavigation(d) && area === 'photo' && cell?.region === 'OK') {
            for (const face of facesFor(d,item)) {
              const b=button(rail,'面 '+face,()=>{selectedFace=face;render(session.state);},
                'hmi-btn recipe-small-button'+(selectedFace===face?' active':''));
              b.dataset.localFace=face;b.setAttribute('aria-pressed',String(selectedFace===face));
            }
          } else if (!usesObjectNavigation(d))
            for (const section of list.filter(s => s.id.startsWith('face:') || s.id === 'E')) button(rail, section.name, () => choose(section.id,selected), 'hmi-btn recipe-small-button');
          if (!usesObjectNavigation(d) && (area.startsWith('face:') || area.startsWith('group:'))) {
            const stage = area.startsWith('group:') ? d.stages.find(s => s.number === Number(area.slice(6))) : d.stages.find(s => s.targets.some(t => t.material === item.member && t.localFace === Number(area.slice(5))));
            const face = stage?.targets.find(t => t.material === item.member)?.localFace, target = stage?.targets.find(t => t.material === item.member && t.localFace === face);
            const pair = node('div',null,'recipe-camera-pair'); pair.append(node('span','当前检测面相机组')); detail.append(pair);
            for (const value of ['AB','CD']) button(pair, value === 'AB' ? 'A / B 相机' : 'C / D 相机',
              () => void session.layout({ material:item.member,localFace:face,stageId:`stage:${stage.number}`,cameraPair:value }), 'hmi-btn recipe-small-button' + (target?.cameraPair === value ? ' active' : ''));
          }
          if (area.startsWith('group:')) input(detail,'R目标角度 (°)', d.stages.find(s => s.number === Number(area.slice(6)))?.angleDeg, v => session.edit(x => x.stages.find(s => s.number === Number(area.slice(6))).angleDeg = v));
          if (area === 'sort') detail.append(node('p','在对应NG或Pending格填写分拣位置。普通OK留原槽，不增加分拣搬运。','recipe-note'));
          const grid = node('div', null, 'recipe-point-grid'); detail.append(grid);
          const currentCards=displayedCards(d,item,area);
          for (const card of currentCards) {
            const box = node('section', null, 'recipe-point-card'); grid.append(box);
            box.dataset.material=item.member??'';box.dataset.stageId=card.point.stageId??card.transitionStageId??'';
            box.dataset.camera=card.camera??'';box.dataset.pointRef=card.reference??'';
            const duplicate=currentCards.filter(c=>c.title===card.title).length>1;
            box.append(node('h3',card.title+(duplicate?' · 第'+(card.point.stageId??card.transitionStageId).replace('stage:','')+'次':'')));
            const axes = node('div',null,'recipe-axis-fields'); box.append(axes);
            const p = card.point, coordinate = p.fixed ? p.point : p;
            const values = [['x','X (mm)'],['y','Y (mm)']];
            if (!['FlipPick','FlipPutBack'].includes(p.purpose)) values.push(['z',card.camera === 'E' ? '扫码Z (mm)' : card.camera ? '检测Z (mm)' : '抓取Z (mm)']);
            for (const [axis,label] of values)
              input(axes,label,axis === 'z' && p.fixed ? p.fixed.z : coordinate[axis],v => editCard(card,(_d,c) => { const point=c.point;if(axis === 'z' && point.fixed)point.fixed.z=v;else (point.fixed ? point.point : point)[axis]=v; }));
            if (card.camera) {
              const parameters = node('div',null,'recipe-capture-fields');box.append(parameters);
              for (const [key,label] of [['exposureUs','曝光 (µs)'],['gain','增益'],['brightnessPercent','光源亮度 (%)']])
                input(parameters,label,profileFor(d,card)?.settings[key],v => editCard(card,(body,c) => editCapture(body,c,key,v)));
            }
          }
        } else detail.append(node('p','当前区域没有配置对象。'));
        const footer = node('div', null, 'recipe-editor-footer'); panel.append(footer);
        button(footer,'← 上一步',() => session.section('basic')); button(footer,'下一未完成项 →',locate); button(footer,'检查并保存 →',() => void check());
      } else {
        const layout = node('div',null,'recipe-review-layout'), review = node('section',null,'recipe-panel'), aside = node('aside',null,'recipe-panel recipe-summary'); layout.append(review,aside); panel.append(layout);
        review.append(node('h2','检查每个零件的配置')); summary(aside,d);
        const table = node('table',null,'recipe-review-table'); review.append(table);
        const head = node('tr'); for(const text of ['配置区域','填写情况',''])head.append(node('th',text)); table.append(head);
        for(const section of sections(d)) {
          const all=entries(d,section.id),n=all.filter(e=>complete(d,e.card)).length,row=node('tr');
          row.append(node('td',section.name),node('td',`${n}/${all.length} ${n === all.length ? '✓' : '待补齐'}`)); const cell=node('td'); button(cell,'编辑 →',()=>choose(section.id),'hmi-btn recipe-small-button');row.append(cell);table.append(row);
        }
        for(const issue of next.issues) { const row=node('p',issue.message || issue.code,'recipe-issue'); button(row,'返回编辑 →',locate,'hmi-btn recipe-small-button'); review.append(row); }
        button(review,'定位缺项 →',locate); button(review,'← 返回基础信息',()=>session.section('basic')); button(aside,'保存配方',()=>void save(),'hmi-btn hmi-btn-primary');
      }
      panel.querySelectorAll('input,select').forEach(e => e.disabled = busy || !next.canSave);
      panel.querySelectorAll('button').forEach(e => e.disabled = busy || (!next.canSave && !e.classList.contains('recipe-nav-button') && !e.classList.contains('recipe-face-rail')));
      if (d.lightExecution?.mode === 'Simulated') panel.querySelectorAll('.recipe-capture-fields label').forEach(label => {
        if (label.textContent.includes('光源亮度')) { const field=label.querySelector('input'); if(field) field.disabled=true; }
      });
    }
    document.getElementById('recipeAuthoringNew').onclick = () => { session.create(newDraft(state.definition)); void session.configure(); };
    document.getElementById('recipeAuthoringCatalog').onchange = e => void session.load(e.target.value);
    document.getElementById('recipeAuthoringCheck').onclick = () => void check();
    document.getElementById('recipeAuthoringSave').onclick = () => void save();
    document.querySelectorAll('[data-authoring-section]').forEach(tab => tab.onclick = () => session.section(tab.dataset.authoringSection));
    render(session.state); return session;
  }

  root.GaodeRecipeAuthoring = Object.freeze({ createSession, mount, objects, cards, navigationCards, facesFor, editCapture, migrateForSave, orderedCells, changeCell });
})(globalThis);

