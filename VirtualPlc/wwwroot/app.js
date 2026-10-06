"use strict";

const POLL_INTERVAL_MS = 100;
const CHANGE_HIGHLIGHT_MS = 1500;
const MAX_HISTORY_ITEMS = 8192;
const heartbeatSignalNames = new Set([
  "PLC_Heartbeat_Req",
  "PC_Heartbeat_Resp"
]);

const signalDescriptions = {
  PLC_Heartbeat_Req: "PLC 心跳请求",
  PC_Heartbeat_Resp: "上位机心跳应答",
  PC_System_Ready: "上位机系统就绪",
  PLC_System_Fault: "PLC 系统故障",
  PLC_Mode_Auto: "PLC 自动模式",
  Soft_Stop_Cmd: "软停止命令",
  System_Reset_Cmd: "系统复位请求",
  PLC_Ready_State: "PLC 就绪",
  Teach_Mode_Cmd: "示教模式",
  Teach_Confirm: "示教确认",
  Scan_Target_Z: "扫码目标 Z",
  Grab_Target_Z: "抓取目标 Z",
  Machine_Current_Pos_Z: "实际 Z",
  Teach_Pos_Select: "示教点位 ID",
  Teach_Pos_X: "示教 X",
  Teach_Pos_Y: "示教 Y",
  Teach_Pos_Z: "示教 Z",
  Alarm_Bits: "报警位",
  Alarm_Severity: "报警等级",
  Manual_Zone_Occupied: "人工区域占用",
  Camera_Target_X: "相机目标 X",
  Camera_Target_Y: "相机目标 Y",
  Camera_Target_Z: "相机目标 Z",
  Machine_Current_Pos_X: "设备当前 X",
  Machine_Current_Pos_Y: "设备当前 Y",
  Flip_Target_Face: "翻转姿态配置码",
  Flip_Status: "翻面执行状态",
  Sorting_Cmd: "分拣命令",
  Sorting_Exec_Status: "分拣执行状态",
};

const registerMeanings = {
  Flip_Status: { 0: "空闲", 1: "执行中", 2: "翻面完成", 3: "翻面失败" },
  Sorting_Cmd: { 0: "空闲，等待上位机指令", 1: "取料", 2: "放料" },
  Sorting_Exec_Status: { 0: "空闲", 1: "已取料", 2: "已放料", 3: "抓取失败" },
  Alarm_Severity: { 1: "警告：不影响生产，仅提示", 2: "一般故障：需暂停等待人工处理", 3: "严重故障：必须立即停机" },
};

const binaryRegisterNames = new Set();

const coilMeanings = {
  PLC_Heartbeat_Req: { 0: "低电平", 1: "高电平" },
  PC_Heartbeat_Resp: { 0: "低电平", 1: "高电平" },
  PC_System_Ready: { 0: "未就绪", 1: "已就绪" },
  PLC_System_Fault: { 0: "正常", 1: "故障" },
  PLC_Mode_Auto: { 0: "非自动模式", 1: "自动模式" },
  Soft_Stop_Cmd: { 0: "未请求", 1: "请求软停" },
  Manual_Zone_Occupied: { 0: "无人介入", 1: "人工介入" },
  System_Reset_Cmd: { 0: "未请求", 1: "请求复位" },
  PLC_Ready_State: { 0: "未就绪", 1: "已就绪" },
  Teach_Mode_Cmd: { 0: "自动运行", 1: "示教模式" },
  Teach_Confirm: { 0: "未确认", 1: "已确认" }
};

const elements = Object.fromEntries([
  "connectionDot", "connectionText", "lastRefresh", "signalCount",
  "signalBreakdown", "activeAction", "faultCount", "faultNames", "changeCount", "searchInput",
  "pauseButton", "plcToPcCoilRows", "plcToPcRegisterRows", "pcToPlcCoilRows",
  "pcToPlcRegisterRows", "plcToPcCoilCount", "plcToPcRegisterCount",
  "pcToPlcCoilCount", "pcToPlcRegisterCount", "plcToPcTotal", "pcToPlcTotal",
  "historyList", "clearHistoryButton", "historySearch", "historyDirection", "historyStatus",
  "historyCount", "focusButton", "workspace", "timeZoneLabel"
].map((id) => [id, document.getElementById(id)]));

const monitor = {
  changedAt: new Map(),
  history: [],
  historyVersion: 0,
  renderedHistoryVersion: -1,
  totalChanges: 0,
  lastSequence: 0,
  missedChanges: 0,
  paused: false,
  latestSnapshot: null,
  stateSnapshot: null,
  renderedAt: null,
  timer: null,
  nextAuditAt: 0,
  lastAuditSequence: 0,
  auditWrites: new Map(),
  lastActionSequence: 0,
  auditError: null
};
monitor.frozenHistory = null;
monitor.frozenAtCount = 0;

function pointKey(area, point) {
  return `${area}:${point.address}`;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function directionText(direction) {
  if (direction === "PcToPlc" || direction === 0) return "上位机 → PLC";
  if (direction === "PlcToPc" || direction === 1) return "PLC → 上位机";
  return "方向未提供";
}

function coordinateText(value) {
  return typeof value === "number" && Number.isFinite(value) ? String(value) : "缺少本次记录";
}

const coordinateAddresses = new Set([3, 5, 7, 9, 11, 13, 15, 17, 133, 135]);
function documentNumber(address) { return /^4x[0-9a-f]+$/i.test(address || "") ? parseInt(address.slice(2), 16) : null; }
function publicName(address, original) {
  return original;
}
function trimHistory() {
  if (monitor.history.length > MAX_HISTORY_ITEMS) {
    monitor.missedChanges += monitor.history.length - MAX_HISTORY_ITEMS + 1;
    monitor.history.length = MAX_HISTORY_ITEMS - 1;
    monitor.history.push({ gap: true, message: `页面仅保留最近 ${MAX_HISTORY_ITEMS} 条，较早记录已移出显示` });
  }
}
function addHistory(item) {
  monitor.history.push(item);
  monitor.history.sort((a, b) => (b.sortTime || b.time?.getTime() || 0) - (a.sortTime || a.time?.getTime() || 0)
    || String(b.sequence || "").localeCompare(String(a.sequence || ""), undefined, { numeric: true }));
  monitor.totalChanges += item.gap ? 0 : 1;
  monitor.historyVersion++;
  trimHistory();
}
function sentCoordinates(action, writes) {
  const refs = (action.writeSequenceRefs || []).map(sequence => writes.get(sequence)).filter(Boolean);
  const commandAddress = 0x21; // This helper consumes current sorting actions only.
  const command = refs.find(w => w.documentNumber === commandAddress && w.accepted && w.value === action.command);
  // A previous motion's retained target is not evidence that this motion wrote that axis.
  const prior = command ? Math.max(0, ...[...writes.values()].filter(w => w.accepted && w.connectionId === command.connectionId &&
    w.documentNumber === 0x21 && w.value !== 0 && w.sequence < command.sequence).map(w => w.sequence)) : 0;
  const zAddress = { ScanZ: 9, GrabZ: 11, DetectionZ: 7 }[action.axisRole];
  const coordinates = [];
  for (const [axis, address, name] of [["x", 3, "Camera_Target_X"], ["y", 5, "Camera_Target_Y"],
    ["z", zAddress, { 7: "Camera_Target_Z", 9: "Scan_Target_Z", 11: "Grab_Target_Z" }[zAddress] || "Z点位未提供"]]) {
    const write = command && refs.find(w => w.documentNumber === address && w.accepted && w.connectionId === command.connectionId &&
      w.sequence > prior && w.sequence < command.sequence && w.function === 16 && Array.isArray(w.rawWords) &&
      address - 1 - w.pduOffset >= 0 && w.rawWords.length >= address - 1 - w.pduOffset + 2);
    let value;
    if (write) {
      const at = address - 1 - write.pduOffset;
      const bytes = [write.rawWords[at] >> 8, write.rawWords[at] & 255, write.rawWords[at + 1] >> 8, write.rawWords[at + 1] & 255];
      const order = { ABCD: [0, 1, 2, 3], CDAB: [2, 3, 0, 1], BADC: [1, 0, 3, 2], DCBA: [3, 2, 1, 0] }[write.byteOrder];
      if (order) value = new DataView(Uint8Array.from(order, i => bytes[i]).buffer).getFloat32(0);
    }
    coordinates.push({ axis, address, name, value, write });
  }
  return { coordinates, command };
}
function recordAction(action) {
  if (action.kind === "AxisMove") {
    const axes = { X: [3, 13], Y: [5, 15], DetectionZ: [7, 17], ScanZ: [9, 133], GrabZ: [11, 135] };
    const axis = axes[action.axisRole];
    if (!axis) return;
    const sent = ["accepted", "rejected"].includes(action.phase);
    const address = axis[sent ? 0 : 1];
    const refs = (action.writeSequenceRefs || []).map(ref => monitor.auditWrites.get(ref)).filter(Boolean);
    const actualWrite = refs.find(write => write.accepted && write.documentNumber === axis[0]);
    const value = sent ? actualWrite ? action.target?.value : undefined : action.actual?.value;
    const utc = action.occurredAtUtc;
    addHistory({ sequence: `A${action.sequence}`, time: utc ? new Date(utc) : null, sourceUtc: utc,
      sortTime: Date.parse(utc) || 0, address: `4x${address.toString(16).toUpperCase().padStart(4, "0")}`,
      name: action.axisRole, direction: sent ? "PcToPlc" : "PlcToPc", writer: sent ? "PC" : "PLC",
      value: coordinateText(value), phase: sent ? "实际发送" : "动作实际反馈",
      detail: `动作 ${action.generation}:${action.actionSequence} · ${action.axisRole} · ${action.phase} · 写入 ${refs.map(w => w.sequence).join(",")}`,
      actionKey: `${action.generation}:${action.actionSequence}`, axis: action.axisRole });
    return;
  }
  const sent = ["accepted", "rejected"].includes(action.phase);
  if (action.kind !== "Sort") return;
  const { coordinates, command } = sentCoordinates(action, monitor.auditWrites);
  const role = { ScanZ: "扫码Z", DetectionZ: "检测Z", GrabZ: "抓取Z" }[action.axisRole] || "轴未提供";
  const rows = sent ? coordinates : ["x", "y", "z"].map((axis) => ({
    axis, address: { x: 13, y: 15, z: 135 }[axis], name: `Machine_Current_Pos_${axis.toUpperCase()}`, value: action.actual?.[axis] }));
  for (const row of rows) {
    const utc = sent ? row.write?.occurredAtUtc : action.occurredAtUtc;
    addHistory({ sequence: `A${action.sequence}.${row.axis}`, time: utc ? new Date(utc) : null, sourceUtc: utc,
      sortTime: Date.parse(utc || action.occurredAtUtc) || 0,
      address: row.address ? `4x${row.address.toString(16).toUpperCase().padStart(4, "0")}` : "点位缺失",
      name: row.name, direction: sent ? "PcToPlc" : "PlcToPc", writer: sent ? "PC" : "PLC",
      value: coordinateText(row.value), phase: sent ? "实际发送" : "动作实际反馈",
      detail: `动作 ${action.generation}:${action.actionSequence} · ${action.kind}=${action.command} · ${action.phase} · ${role}` +
        (sent ? ` · 写入#${row.write?.sequence ?? "缺失"} · 连接 ${command?.connectionId ?? "未提供"} · 事务 ${row.write?.transactionId ?? "未提供"}` : " · 设备动作采样"),
      actionKey: `${action.generation}:${action.actionSequence}`, axis: row.axis });
  }
}
async function readMotionAudit() {
  if (Date.now() < monitor.nextAuditAt) return;
  monitor.nextAuditAt = Date.now() + 500;
  const response = await fetch(`/api/simulator/audit?after=${monitor.lastAuditSequence}`, { cache: "no-store" });
  if (!response.ok) throw new Error(`动作记录 HTTP ${response.status}`);
  const batch = await response.json();
  if (batch.latestSequence < monitor.lastAuditSequence || batch.actionLatestSequence < monitor.lastActionSequence) {
    monitor.auditWrites.clear(); monitor.lastAuditSequence = 0; monitor.lastActionSequence = 0;
    addHistory({ gap: true, message: "设备记录序号重启；以下记录属新设备会话" });
  }
  if (batch.gap || batch.actionOldestSequence > monitor.lastActionSequence + 1) {
    addHistory({ gap: true, message: "动作或写入缓存存在缺口；缺少本次记录的坐标不补造" });
  }
  for (const write of batch.writes) {
    monitor.auditWrites.set(write.sequence, write);
  }
  monitor.lastAuditSequence = batch.latestSequence;
  for (const sequence of monitor.auditWrites.keys()) if (sequence < batch.oldestSequence) monitor.auditWrites.delete(sequence);
  for (const action of [...batch.actions].sort((a, b) => a.sequence - b.sequence)) {
    if (action.sequence <= monitor.lastActionSequence) continue;
    recordAction(action); monitor.lastActionSequence = action.sequence;
  }
  renderHistory();
}

function actionText(action) {
  const labels = {
    AxisMove: "独立轴移动",
    Flip: "翻面",
    Sort: "分拣",
    PutBack: "放回"
  };
  return action ? (labels[action] ?? action) : "空闲";
}

function updateChanges(batch) {
  const now = Date.now();
  if (batch.latestSequence < monitor.lastSequence) {
    monitor.lastSequence = 0;
    monitor.history = [];
    monitor.historyVersion += 1;
  }
  if (batch.gap) {
    const missed = Math.max(0, batch.oldestSequence - monitor.lastSequence - 1);
    monitor.missedChanges += missed;
    monitor.history.unshift({ gap: true, message: `事件缓存已覆盖，缺少 ${missed} 条变化` });
    monitor.historyVersion += 1;
  }
  for (const item of batch.changes) {
    if (item.sequence <= monitor.lastSequence) continue;
    monitor.lastSequence = item.sequence;
    if (heartbeatSignalNames.has(item.name)) continue;
    // Coordinates have one canonical per-action source; state/handshake/clear events stay here.
    if (coordinateAddresses.has(documentNumber(item.address))) continue;
    const key = `${item.area === "Coil" ? "coil" : "register"}:${item.address}`;
    monitor.changedAt.set(key, now);
    monitor.totalChanges += 1;
    monitor.history.unshift({
      sequence: item.sequence,
      time: new Date(item.occurredAtUtc), sourceUtc: item.occurredAtUtc,
      address: item.address,
      name: publicName(item.address, item.name),
      direction: item.direction,
      writer: item.writer,
      from: item.previous,
      to: item.current
    });
    monitor.historyVersion += 1;
  }
  if (monitor.history.length > MAX_HISTORY_ITEMS) {
    const removed = monitor.history.length - MAX_HISTORY_ITEMS;
    monitor.history.length = MAX_HISTORY_ITEMS;
    monitor.missedChanges += removed;
    monitor.history.push({ gap: true, message: `页面仅保留最近 ${MAX_HISTORY_ITEMS} 条，较早记录已移出显示` });
    if (monitor.history.length > MAX_HISTORY_ITEMS) monitor.history.length = MAX_HISTORY_ITEMS;
    monitor.historyVersion += 1;
  }
}

async function readChanges() {
  // The cursor follows write-time sequence numbers; intermediate transitions
  // cannot disappear merely because state snapshots are sampled at 100 ms.
  for (let page = 0; page < 8; page++) {
    const response = await fetch(`/api/simulator/changes?after=${monitor.lastSequence}`, { cache: "no-store" });
    if (!response.ok) throw new Error(`事件 HTTP ${response.status}`);
    const batch = await response.json();
    updateChanges(batch);
    if (monitor.lastSequence >= batch.latestSequence || batch.changes.length === 0) break;
  }
}

function isRecentlyChanged(area, point) {
  if (heartbeatSignalNames.has(point.name)) {
    return false;
  }

  const changedAt = monitor.changedAt.get(pointKey(area, point));
  return changedAt && Date.now() - changedAt < CHANGE_HIGHLIGHT_MS;
}

function matchesFilters(point) {
  const query = elements.searchInput.value.trim().toLowerCase();
  if (!query) {
    return true;
  }

  const description = signalDescriptions[point.name] ?? "";
  return `${point.address} ${point.name} ${description}`.toLowerCase().includes(query);
}

function pointNameHtml(point) {
  const name = publicName(point.address, point.name);
  const description = signalDescriptions[name] ?? "";
  return `<span class="signal-name"><span>${escapeHtml(name)}</span><small>${escapeHtml(description)}</small></span>`;
}

function binaryValueHtml(value, meaning, changed) {
  const numeric = value ? 1 : 0;
  const stateClass = value ? "binary-on" : "binary-off";
  const changedClass = changed ? "value-changed" : "";
  return `<span class="value-pill ${stateClass} ${changedClass}">${numeric}</span><span class="value-detail">${escapeHtml(meaning ?? (value ? "ON" : "OFF"))}</span>`;
}

function coilValueHtml(point, changed) {
  const numeric = point.value ? 1 : 0;
  return binaryValueHtml(point.value, coilMeanings[point.name]?.[numeric], changed);
}

function registerTone(point) {
  const value = point.rawValue;
  if (["X_Pos_Confirmed", "Y_Pos_Confirmed", "Z_Camera_Pos_Confirmed", "Z_Scan_Pos_Confirmed", "Z_Grap_Pos_Confirmed"].includes(point.name))
    return value === 1 ? "tone-success" : value === 0 ? "tone-neutral" : "tone-danger";
  if (["Flip_Status", "Flip_Unload_Status", "Sorting_Exec_Status"].includes(point.name)) {
    if (value === 3) return "tone-danger";
    if (value === 2) return "tone-success";
    return value === 0 ? "tone-neutral" : "tone-info";
  }

  if (registerMeanings[point.name]) {
    return value === 0 ? "tone-neutral" : "tone-command";
  }
  return value === 0 ? "tone-neutral" : "tone-data";
}

function registerValueHtml(point, changed) {
  if (point.valueType === "Float32") {
    const changedClass = changed ? "value-changed" : "";
    const raw = point.words.map((word) => word.toString(16).toUpperCase().padStart(4, "0")).join(" ");
    return `<span class="value-pill numeric-value tone-data ${changedClass}">${escapeHtml(point.floatValue)}</span><span class="value-detail">Float32 · ${raw}</span>`;
  }
  const meaning = registerMeanings[point.name]?.[point.rawValue];
  if (binaryRegisterNames.has(point.name)) {
    return binaryValueHtml(point.rawValue === 1, meaning, changed);
  }

  const signed = point.signedValue !== point.rawValue ? ` · 有符号 ${point.signedValue}` : "";
  const detail = meaning ? `${meaning}${signed}` : `UINT16${signed}`;
  const changedClass = changed ? "value-changed" : "";
  return `<span class="value-pill numeric-value ${registerTone(point)} ${changedClass}">${point.rawValue}</span><span class="value-detail">${escapeHtml(detail)}</span>`;
}

function renderRows(area, points, direction, target, countTarget) {
  const directionalPoints = points.filter((point) => point.direction === direction);
  const visible = directionalPoints.filter(matchesFilters);
  const countText = `${visible.length} / ${directionalPoints.length} 个信号`;
  if (countTarget.textContent !== countText) {
    countTarget.textContent = countText;
  }

  if (!target.dataset.initialized) {
    target.replaceChildren();
    target.dataset.initialized = "true";
  }

  const existingRows = new Map(
    [...target.querySelectorAll("tr[data-point-key]")]
      .map((row) => [row.dataset.pointKey, row]));
  const currentKeys = new Set();

  for (const point of directionalPoints) {
    const key = pointKey(area, point);
    currentKeys.add(key);
    let row = existingRows.get(key);
    if (!row) {
      row = document.createElement("tr");
      row.dataset.pointKey = key;
      row.innerHTML = `
        <td class="address-cell">${escapeHtml(point.address)}<small>PDU ${point.pduOffset}</small></td>
        <td>${pointNameHtml(point)}</td>
        <td class="value-cell"></td>`;
      target.appendChild(row);
    }

    const shouldHide = !matchesFilters(point);
    if (row.hidden !== shouldHide) {
      row.hidden = shouldHide;
    }
    const changed = isRecentlyChanged(area, point);
    const value = area === "coil" ? coilValueHtml(point, changed) : registerValueHtml(point, changed);
    const valueCell = row.querySelector(".value-cell");
    // The rendered value already includes the current value and highlight state.
    const valueToken = value;
    if (valueCell.dataset.valueToken !== valueToken) {
      valueCell.innerHTML = value;
      valueCell.dataset.valueToken = valueToken;
    }
  }

  for (const [key, row] of existingRows) {
    if (!currentKeys.has(key)) {
      row.remove();
    }
  }

  let emptyRow = target.querySelector("tr[data-empty-row]");
  if (visible.length === 0) {
    if (!emptyRow) {
      emptyRow = document.createElement("tr");
      emptyRow.dataset.emptyRow = "true";
      emptyRow.innerHTML = '<td colspan="3" class="empty-cell">没有符合筛选条件的信号</td>';
      target.appendChild(emptyRow);
    }
    if (emptyRow.hidden) {
      emptyRow.hidden = false;
    }
  } else if (emptyRow) {
    if (!emptyRow.hidden) {
      emptyRow.hidden = true;
    }
  }
}

function renderOverview(snapshot) {
  const signalTotal = snapshot.coils.length + snapshot.holdingRegisters.length;
  elements.signalCount.textContent = signalTotal;
  elements.signalBreakdown.textContent = `0x ${snapshot.coils.length} 个 · 4x ${snapshot.holdingRegisters.length} 个`;
  elements.activeAction.textContent = actionText(snapshot.activeAction);
  const faults = [...snapshot.activeFaults];
  if (snapshot.communicationTimedOut) {
    faults.unshift("CommunicationTimeout");
  }
  elements.faultCount.textContent = faults.length;
  elements.faultNames.textContent = faults.length ? faults.join("、") : "无故障";
  elements.changeCount.textContent = monitor.missedChanges
    ? `${monitor.totalChanges}（记录缺口 ${monitor.missedChanges}）` : monitor.totalChanges;
}

function historyTime(item) {
  if (!item.time || !Number.isFinite(item.time.getTime())) return "时间未提供";
  return item.time.toLocaleTimeString("zh-CN", { hour12: false }) + "." + String(item.time.getMilliseconds()).padStart(3, "0");
}
function renderHistory(force = false) {
  const frozen = monitor.frozenHistory !== null;
  const source = frozen ? monitor.frozenHistory : monitor.history;
  const pending = Math.max(0, monitor.totalChanges - monitor.frozenAtCount);
  elements.historyStatus.textContent = frozen
    ? `查看已暂停 · 后台继续接收 · 新增 ${pending} 条（有界缓存）` : "实时接收 · 最新在前 · 向下滚动可暂停查看";
  if (!force && (frozen || monitor.renderedHistoryVersion === monitor.historyVersion)) return;
  monitor.renderedHistoryVersion = monitor.historyVersion;
  const query = elements.historySearch.value.trim().toLowerCase();
  const direction = elements.historyDirection.value;
  const rows = source.filter(item => item.gap ||
    ((!direction || directionText(item.direction) === directionText(direction)) &&
     `${item.name} ${item.address} ${item.detail || ""} ${signalDescriptions[item.name] || ""}`.toLowerCase().includes(query)));
  elements.historyCount.textContent = `${rows.filter(x => !x.gap).length} / ${source.filter(x => !x.gap).length} 条`;
  elements.historyList.innerHTML = rows.length ? rows.map(item => {
    if (item.gap) return `<p class="history-empty gap-notice">${escapeHtml(item.message)}</p>`;
    const value = item.value !== undefined ? item.value : item.to;
    const meaning = registerMeanings[item.name]?.[value] || coilMeanings[item.name]?.[value] || "";
    const direction = directionText(item.direction);
    const sending = direction === "上位机 → PLC";
    const utc = item.sourceUtc || (item.time && Number.isFinite(item.time.getTime()) ? item.time.toISOString() : "时间未提供");
    return `<div class="history-item${item.writeKey ? " command-record" : ""}" data-action="${escapeHtml(item.actionKey || "")}" data-axis="${escapeHtml(item.axis || "")}" data-record="${escapeHtml(item.sequence)}">
      <span class="history-time" title="${escapeHtml(utc)}">${historyTime(item)}<small>#${escapeHtml(item.sequence)}</small></span>
      <span class="history-direction ${sending ? "sending" : "receiving"}">${sending ? "PC → PLC" : direction === "PLC → 上位机" ? "PLC → PC" : "方向未提供"}<small>${escapeHtml(item.phase || "数值变化")}</small></span>
      <span class="history-signal"><span class="signal-title">${escapeHtml(item.name)} <span class="history-address">${escapeHtml(item.address)}</span></span><small>${escapeHtml(signalDescriptions[item.name] || "")} · ${direction} · 写入方 ${escapeHtml(item.writer)}</small>
        ${item.detail ? `<details class="record-detail"><summary>关联详情${item.actionKey ? ` · 动作 ${escapeHtml(item.actionKey)}` : ""}</summary><div>${escapeHtml(item.detail)}<br>UTC ${escapeHtml(utc)}</div></details>` : ""}</span>
      <span class="history-change">${item.value !== undefined ? `${item.writeKey ? (item.phase === "写入被拒绝" ? "拒绝写入 " : "写入 ") : ""}${escapeHtml(item.value)}` : `${escapeHtml(item.from)} → ${escapeHtml(item.to)}`}<small>${escapeHtml(meaning)}</small></span>
    </div>`;
  }).join("") : '<p class="history-empty">暂无符合条件的记录；筛选不会改变后台采集。</p>';
}
function setHistoryPaused(paused) {
  if (paused === (monitor.frozenHistory !== null)) return;
  monitor.frozenHistory = paused ? monitor.history.map(item => ({ ...item })) : null;
  monitor.frozenAtCount = monitor.totalChanges;
  elements.pauseButton.textContent = paused ? "恢复最新" : "暂停查看";
  elements.pauseButton.setAttribute("aria-pressed", String(paused));
  renderHistory(true);
  if (!paused) elements.historyList.scrollTop = 0;
}

function renderSnapshot(snapshot) {
  renderOverview(snapshot);
  renderRows("coil", snapshot.coils, "PlcToPc", elements.plcToPcCoilRows, elements.plcToPcCoilCount);
  renderRows("register", snapshot.holdingRegisters, "PlcToPc", elements.plcToPcRegisterRows, elements.plcToPcRegisterCount);
  renderRows("coil", snapshot.coils, "PcToPlc", elements.pcToPlcCoilRows, elements.pcToPlcCoilCount);
  renderRows("register", snapshot.holdingRegisters, "PcToPlc", elements.pcToPlcRegisterRows, elements.pcToPlcRegisterCount);
  const plcToPcCount = [...snapshot.coils, ...snapshot.holdingRegisters]
    .filter((point) => point.direction === "PlcToPc").length;
  const pcToPlcCount = [...snapshot.coils, ...snapshot.holdingRegisters]
    .filter((point) => point.direction === "PcToPlc").length;
  elements.plcToPcTotal.textContent = `${plcToPcCount} 个点位`;
  elements.pcToPlcTotal.textContent = `${pcToPlcCount} 个点位`;
  renderHistory();
  monitor.latestSnapshot = snapshot;
  monitor.renderedAt = snapshot.timestamp || "时间未提供";
}

function showMonitorState(snapshot, errors) {
  const communication = snapshot?.communicationTimedOut;
  elements.connectionDot.className = `status-dot ${errors.length ? "" : communication === false ? "online" : communication === true ? "offline" : ""}`;
  elements.connectionText.textContent = communication === true ? "PLC通信失效（心跳超时）"
    : communication === false ? "监控可读取 · PLC心跳未超时" : "PLC通信状态未提供";
  if (errors.some(error => error.startsWith("状态读取失败"))) elements.connectionText.textContent = "监控读取失败";
  elements.lastRefresh.textContent = [errors.length ? errors.join("；") : "本次状态读取及显示正常",
    `状态时间 ${snapshot?.timestamp || "未提供"}`,
    `最近成功显示 ${monitor.renderedAt || "尚无"}`,
    errors.length ? "显示可能不完整或为旧状态" : "TCP连接状态未提供"].join("；");
}

async function poll() {
  if (!monitor.paused) {
    const errors = [];
    let snapshot;
    try {
      const response = await fetch("/api/simulator/state", { cache: "no-store" });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      snapshot = await response.json();
      monitor.stateSnapshot = snapshot;
    } catch (error) {
      errors.push(`状态读取失败：${error.message}`);
    }
    if (snapshot) {
      try { await readChanges(); }
      catch (error) { errors.push(`变化记录读取/显示失败：${error.message}`); }
      try { renderSnapshot(snapshot); }
      catch (error) { errors.push(`页面显示失败：${error.message}`); }
      try {
        const auditDue = Date.now() >= monitor.nextAuditAt;
        await readMotionAudit();
        if (auditDue) monitor.auditError = null;
      } catch (error) {
        monitor.auditError = error.message;

      }
    }
    if (monitor.auditError) errors.push(`动作记录异常：${monitor.auditError}`);
    showMonitorState(monitor.stateSnapshot, errors);
  }

  monitor.timer = window.setTimeout(poll, POLL_INTERVAL_MS);
}

function rerender() {
  if (monitor.latestSnapshot) {
    try { renderSnapshot(monitor.latestSnapshot); }
    catch (error) { showMonitorState(monitor.stateSnapshot, [`页面显示失败：${error.message}`]); }
  }
}

elements.searchInput.addEventListener("input", rerender);
elements.pauseButton.addEventListener("click", () => setHistoryPaused(monitor.frozenHistory === null));
elements.historySearch.addEventListener("input", () => renderHistory(true));
elements.historyDirection.addEventListener("change", () => renderHistory(true));
elements.historyList?.addEventListener("scroll", () => {
  if (elements.historyList.scrollTop > 24 && monitor.frozenHistory === null) setHistoryPaused(true);
});
elements.focusButton.addEventListener("click", () => {
  const focused = elements.workspace.classList.toggle("history-focused");
  elements.focusButton.textContent = focused ? "显示实时点位" : "专注记录";
  elements.focusButton.setAttribute("aria-pressed", String(focused));
});
elements.clearHistoryButton.addEventListener("click", () => {
  monitor.history = [];
  if (monitor.frozenHistory !== null) monitor.frozenHistory = [];
  monitor.historyVersion += 1;
  monitor.totalChanges = 0;
  monitor.frozenAtCount = 0;
  monitor.missedChanges = 0;
  renderHistory(true);
  elements.changeCount.textContent = "0";
});
const zoneMinutes = -new Date().getTimezoneOffset();
elements.timeZoneLabel.textContent = `本机时间 UTC${zoneMinutes >= 0 ? "+" : "-"}${String(Math.floor(Math.abs(zoneMinutes) / 60)).padStart(2,"0")}:${String(Math.abs(zoneMinutes) % 60).padStart(2,"0")}`;

// Version is on the existing history list; the removed motion panel is not required.
if (elements.historyList?.dataset.historyVersion !== "unlock-history-r9") {
  const revision = "unlock-history-r9";
  const url = new URL(window.location.href);
  elements.connectionDot.className = "status-dot";
  elements.connectionText.textContent = "页面版本不一致";
  elements.lastRefresh.textContent = "正在重新读取当前页面；如仍不一致，请重新解压完整部署包。";
  if (url.searchParams.get("monitor") !== revision) {
    url.searchParams.set("monitor", revision); window.location.replace(url.href);
  }
} else { poll(); }
