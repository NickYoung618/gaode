(function () {
  function mount({ document, request, post, refresh, runId }) {
    const positions = document.getElementById('publicPositionsDialog');
    const anomaly = document.getElementById('trayAnomalyDialog');
    if (!positions || !anomaly) return { render() {} };
    let draft, busy = false, currentDecision;
    const make = (tag, text) => { const e = document.createElement(tag); if (text != null) e.textContent = text; return e; };
    const button = (root, text, action) => { const b = make('button', text); b.type = 'button'; b.onclick = action; root.append(b); return b; };
    const feedback = make('p');
    const showError = error => { feedback.textContent = error.message; };
    async function open() {
      positions.replaceChildren(make('h2', '示教／公共位置')); positions.append(feedback); feedback.textContent = '读取中'; positions.showModal();
      try { draft = await request('/api/v1/station01/configuration/public-positions'); renderPositions(); }
      catch (error) { showError(error); button(positions, '取消', () => positions.close()); }
    }
    function renderPositions() {
      positions.replaceChildren(make('h2', '示教／公共位置'));
      for (const [key, kind, label] of [['threeD', 'ThreeD', '3D拍照位置'], ['manualLoading', 'ManualLoading', '人工上下料位置']]) {
        const section = make('section'); section.append(make('h3', label)); positions.append(section);
        for (const axis of ['x', 'y', 'z']) {
          const labelNode = make('label', `${axis.toUpperCase()} (${draft[key]?.unit || 'mm'}) `);
          const input = make('input'); input.type = 'number'; input.step = 'any'; input.value = draft[key]?.[axis] ?? '';
          input.id = `public-${key}-${axis}`;
          input.oninput = () => { draft[key][axis] = input.value === '' ? null : Number(input.value); };
          labelNode.append(input); section.append(labelNode);
        }
        button(section, '读取当前实测位置', async () => {
          if (busy) return; busy = true;
          try {
            const value = await request(`/api/v1/station01/configuration/public-positions/current?kind=${kind}`);
            draft[key] = value.point; renderPositions(); feedback.textContent = `已读取：${value.source}`;
          } catch (error) { showError(error); } finally { busy = false; }
        });
      }
      positions.append(feedback);
      button(positions, '保存', async () => {
        if (busy) return;
        if (![draft.threeD, draft.manualLoading].every(p => p && ['x','y','z'].every(a => Number.isFinite(p[a])))) {
          feedback.textContent = '请填写完整的X、Y、Z坐标'; return;
        }
        busy = true;
        try {
          draft = await post('/api/v1/station01/configuration/public-positions', { threeD: draft.threeD, manualLoading: draft.manualLoading, expectedDigest: draft.digest });
          renderPositions(); feedback.textContent = '公共位置已保存';
        } catch (error) { showError(error); } finally { busy = false; }
      });
      button(positions, '取消', () => positions.close());
    }
    function render(run) {
      const end = document.getElementById('trayEndState');
      if (end) end.textContent = run?.trayEndReason ? `结束原因：${({ NormalCompletion:'正常流程结束', EmptyTray:'空盘结束', ManualIntervention:'人工介入' })[run.trayEndReason] || run.trayEndReason}；检测完整：${run.inspectionCompleted == null ? '未知' : run.inspectionCompleted ? '是' : '否'}` : '';
      const d = run?.trayAnomalyDecision;
      if (!d || d.state !== 'Pending') { currentDecision = null; if (anomaly.open) anomaly.close(); return; }
      currentDecision = d;
      if (anomaly.dataset.decisionId !== d.decisionId) {
        anomaly.dataset.decisionId = d.decisionId; anomaly.replaceChildren(make('h2', '3D穴位异常'));
        for (const item of d.items) anomaly.append(make('p', `${item.region}区 第${item.row}行 第${item.column}列：${item.type}${item.reason ? '（'+item.reason+'）' : ''}`));
        const remaining = make('p'); remaining.id = 'trayAnomalyCountdown'; anomaly.append(remaining);
        for (const [choice, label] of [['ManualIntervention', '人工介入'], ['Continue', '继续']]) button(anomaly, label, async () => {
          if (busy) return; busy = true;
          try { await post(`/api/v1/station01/runs/${runId()}/tray-anomaly-decision`, { decisionId:d.decisionId, choice }); await refresh(); }
          catch (error) { const notice = make('p', error.message); anomaly.append(notice); await refresh(); }
          finally { busy = false; }
        });
      }
      if (!anomaly.open) anomaly.showModal(); countdown();
    }
    function countdown() {
      if (!currentDecision) return;
      const seconds = Math.max(0, Math.ceil((Date.parse(currentDecision.deadlineUtc) - Date.now()) / 1000));
      const label = document.getElementById('trayAnomalyCountdown');
      if (label) label.textContent = `剩余 ${seconds} 秒；到期由后端自动继续`;
    }
    anomaly.addEventListener('cancel', event => event.preventDefault());
    document.getElementById('btnPositions')?.addEventListener('click', () => void open());
    setInterval(countdown, 200);
    return { render };
  }
  window.GaodePublicTrayFlow = { mount };
})();
