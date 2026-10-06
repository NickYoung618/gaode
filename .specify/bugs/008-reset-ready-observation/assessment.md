# Bug Assessment: 复位响应与运动状态观察不同步

- Slug: 008-reset-ready-observation
- Created: 2026-09-27
- Source: r19-current-gates-0927 current-storage-recovery-gates（必要验证）
- Verdict: valid
- Severity: high

## Symptom / Reproduction

UnconfirmedRestartLinkNeverDispatchesNewPhysicalStart的Failed和CommitUnknown两用例在复位202后立即Check，实际409 RecoveryResetNotObserved，尚未进入预定链接保存注入。正式WPF和较慢完整恢复可通过，不能替代该时序条件。

## Code evidence

LatestProtocolPlcDevice.ResetAsync末尾直接读PLC Ready/Auto/Fault，等待Observe.Connected/SafetyClear后返回，未等待同一缓存PlcReady；MotionCoordinator.ReconcileVerifiedReset再次读state.Observe且要求PlcReady。ReadInitialStateAsync只更新本地XYZ/锁并以新coils建立Ready检查，缓存PlcReady可能仍旧false，两个当前顺序用例均命中该不一致。不是链接保存门禁失效，后者未被调用。

## Proposed Remediation / Files likely to change

仅LatestProtocolPlcDevice.ResetAsync在已有轮询中读取一次完整observed，要求observed.PlcReady为true后才返回。保留实际Modbus Ready/Auto/无Fault和Connected/SafetyClear全部条件，保留原CancellationToken、PollMs及BusinessMs.XyCompletion，不改变接口、PLC信号或门禁，不增加重试及兜底。

共享合同Ready本已是必要条件。003/008现有spec/contracts/plan/tasks先登记该观察同步细化，当前活动测试结束后才改代码/重建，不覆盖r19第一次TRX。

## Tests to add or update

新独立构建按原两条必要用例复验，并保留完整新轮初始/资源/重复门禁现有测试。新Host实际RECOVERY-F唯一旧图/新轮页面代表复验；当前Q18顺序差异待同新Host正式验证，普通业务路径其余沿未变代码复用。不增加全故障矩阵。

## Risks & Considerations

仅复位API返回时序受影响；缓存长期不Ready须在既有期限内失败，不伪造同步状态。对有实际故障/未知生产机构的拒绝保持。未验证前不声明修复或运行成功。

## 验证中发现的独立旧fixture问题（不归因为复位缺陷）

同轮7测试最终4 Passed/3 Failed。第三个是VirtualManualCompletionGateTests沿旧007默认fixture，必要SortingTargetUnconfigured使主流程在取盘之前Blocked，125秒等待到期。这是现行执行正确拒绝缺处置配置，不能放宽为成功或改业务门禁。该测试仅验证授权/可靠解锁/Final提交，改为VirtualLoopTestRig.CreateAsync(..., useCurrentRecipe:true)使用已确认当前Q01合法OK Test配置，不改变任何断言或等待期限。文件增列backend/tests/Gaode.Integration.Tests/Api/VirtualManualCompletionGateTests.cs；原失败TRX保留。该必要测试fixture适配与复位修复在新构建顺序复验，只重跑三条失败用例。
