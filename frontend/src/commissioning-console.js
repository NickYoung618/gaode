(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.GaodeCommissioningConsole = api;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';
  function confirmIdentity(identity, profile) {
    if (!identity || identity.schemaVersion !== 'station01-identity/1' ||
        identity.mode !== 'RealDeviceCommissioning' || identity.purpose !== 'Commissioning' ||
        identity.authenticationSource !== 'PreconfiguredCommissioning' ||
        !['Operator', 'ProcessEngineer'].includes(identity.role) || !identity.subjectId || !identity.displayName ||
        !Array.isArray(identity.permissions) || !profile || identity.profileId !== profile.profileId ||
        identity.subjectId !== profile.expectedSubjectId || identity.role !== profile.expectedRole)
      throw new Error('CommissioningIdentityMismatch');
    return Object.freeze({ ...identity, permissions: Object.freeze([...identity.permissions]) });
  }
  function loginMatches(identity, user, selectedRole) {
    return user.trim() === identity.displayName && selectedRole === (identity.role === 'Operator' ? 'L1' : 'L2');
  }
  function bindLogin(document, identity, navigate) {
    const input = document.getElementById('user');
    if (!input) return null;
    input.value = identity.displayName;
    const expected = identity.role === 'Operator' ? 'L1' : 'L2';
    document.querySelectorAll('.role-chip').forEach(c => c.classList.toggle('active', c.dataset.role === expected));
    input.addEventListener('input', () => input.setCustomValidity(''));
    return event => {
      event.preventDefault();
      const role = document.querySelector('.role-chip.active')?.dataset.role;
      if (!loginMatches(identity, input.value, role)) {
        input.setCustomValidity('所选角色或用户与后台确认身份不符'); input.reportValidity(); return false;
      }
      input.setCustomValidity(''); navigate('prototype.html'); return true;
    };
  }
  function newStart(template, selected, newId) {
    if (template?.schemaVersion !== 'commissioning-console-template/1' || template.mode !== 'RealDeviceCommissioning' ||
        template.contextTemplate?.purpose !== 'Commissioning' || !template.id || !template.version || !template.sourceReference ||
        !selected?.recipeId || !selected.version || !selected.catalogDigest ||
        [template.publicConfigRef,template.budgetRef,template.simulationRef].some(r=>!r?.id||!r.version))
      throw new Error('CommissioningStartPreparationMissing');
    return {requestId:newId(),contextJson:JSON.stringify({...template.contextTemplate,
      schemaVersion:'station01-start-run-context/2.0',trayId:newId(),scenarioId:selected.scenarioId,
      expectedRecipeRef:{recipeId:selected.recipeId,version:selected.version,catalogDigest:selected.catalogDigest}}),
      publicConfigRef:template.publicConfigRef,budgetRef:template.budgetRef,simulationRef:template.simulationRef};
  }
  function mediaSlot(item) {
    const key = item.role === 'Detection' ? 'Detection:'+item.businessCamera : item.role;
    return new Map([['Detection:C',0],['Detection:D',1],['Detection:A',2],['Detection:B',3],['E',4],['ThreeD',5],['F',6]]).get(key);
  }
  function recoveryNotice(run) {
    if (run?.commissioningRecovery?.status === 'ClosedAfterVerifiedReset')
      return 'PLC复位及旧任务恢复核验已通过，旧任务已结束；核对配方后手动点击启动，开启完整新一轮，不继续旧动作。';
    if (!run || !['RecoveryRequired', 'Blocked', 'Restricted'].includes(run.state)) return null;
    const actions = run.allowedActions || [];
    if (actions.includes('CommissioningRecoveryReset')) return '旧任务待恢复：排除现场故障后，点击“复位并结束旧任务”或一键复位；后台会核验本次复位、旧反馈清零和资源退出，成功后才允许手动开始新一轮。';
    if (actions.includes('RecoveryReset')) return '旧任务待恢复：先排除故障，填写原因并执行复位；复位成功后仍须初始核验。';
    if (actions.includes('RecoveryCheck')) return 'PLC复位已观察，旧任务仍待核验：核对料盘、零件和冻结配置，填写依据并执行初始核验。';
    if (run.faultRestart?.status === 'InitialReady') return '旧任务初始核验已通过；请显式点击启动，开启完整新轮，不继续旧动作。';
    if (run.state === 'RecoveryRequired' || run.action === 'Unknown')
      return `旧任务 ${run.runId || '编号未提供'} 待恢复核验，当前身份没有可用的恢复操作入口。请由具备运行权限的操作员使用一键复位完成恢复核验；若仍无入口，请维护人员核对安装及确认配置。重开程序或重复点击启动无效，不要删除原任务。`;
    return null;
  }
  function canStartAfterRecovery(run, admission) {
    return run?.state === 'Cancelled' && run?.commissioningRecovery?.status === 'ClosedAfterVerifiedReset' &&
      !!run.commissioningRecovery.recoveryWriteId && admission?.state === 'Available';
  }
  function resetFailure(message) {
    if (message?.startsWith('PreviousResetRequestNotReleased')) return '上次复位请求仍为1，本次没有重新发送；请核对上次复位结果和后台记录。';
    if (message?.startsWith('RecoveryExecutionStillActive')) return '旧流程尚未退出，请等待停止后查询状态。';
    if (message?.startsWith('RecoverySoftwareResourcesNotReleased')) return '旧采集、算法或存图资源尚未释放，旧任务继续保持阻断。';
    if (message?.startsWith('RecoveryInitialStateIncomplete')) return '复位后的安全、零位或请求/反馈清零核验尚未全部通过，旧任务未放行；请查看后台缺项。';
    return '复位恢复结果未确认，旧任务未放行；请查询原任务与后台日志，不要重复点击。';
  }
  return Object.freeze({ confirmIdentity, loginMatches, bindLogin, newStart, mediaSlot, recoveryNotice, canStartAfterRecovery, resetFailure });
});
