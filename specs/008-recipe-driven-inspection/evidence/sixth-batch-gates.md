# 第六批必要门禁定向验证

日期：2026-09-25；仅Test/VirtualPlc、独立worker、真实SQLite/媒体路径。正常Q01/Q02正式WPF运行及第四批到位不符、逐图复位失败、必要保存失败组件证据继续引用[第五批报告](fifth-batch-q01-q02.md)和[第四批组件报告](fourth-batch-validation.md)，未重做故障矩阵。

| 边界 | 本次结果 | 可核对事实及范围 |
| --- | --- | --- |
| F实际码与前端期望R008-Q01不匹配 | 定向集成测试1/1通过 | `ExpectedRecipeMismatchTests`以独立worker返回`TEST-TRAY-0202`；保存`ExpectedRecipeFMismatch`，无绑定、无产品计划、无Final，产品动作0。公共3D的一次命令2不误算为产品动作。包位于`artifacts/station01-007/necessary-failures-20260924/expected-recipe-f-mismatch-622ebfcc10fd4700a36a661c0e4c34db/`。这是Host组件集成，不冒充正式页面负例。 |
| Q01第三次必要媒体保存失败 | 定向集成测试1/1通过 | `StrictQ01SaveGateTests`使用实际Host/VirtualPlc/worker/SQLite，注入一次TraceWriter媒体写失败；A后不派B、无卸载完成或Final，提前取盘确认HTTP 409。证据包`artifacts/station01-007/necessary-failures-20260924/q01-product-media-save-gate-4179623cd5df4cc39300a547ca070e98/`。 |
| 正常Final | 正式页面通过 | Q01、Q02与Q01-PARAM同run均有下料、解锁、页面取盘确认、SQLite和Final；见第五批与`config-change.md`。 |
| 旧共享Final测试 | 本次失败，保留 | `FinalUnloadCompletionIntegrationTests.AuthenticatedManualConfirmationIsTheOnlyStepThatCreatesFinalUnloadCompletion`在RunningF因旧测试配置F XY超时，未到Final断言；TRX见`artifacts/recipe-execution-008/sixth-batch-testresults/final-gate.trx`。不得将其写为通过或作为历史心跳修复证据。当前严格Q01保存负例的409及三条正常WPF Final形成不同范围的证据，但旧测试兼容配置仍待定位。 |


复测记录：合并筛选运行两项测试时，`artifacts/recipe-execution-008/sixth-batch-testresults/sixth-batch-focused-gates.trx`为1通过/1失败；F测试30秒处读取到空ErrorCode，未形成F不匹配结论。同一F测试随后**单独顺序运行**，`sixth-batch-f-mismatch-isolated.trx`为1/1通过，实际新运行包为`artifacts/station01-007/necessary-failures-20260924/expected-recipe-f-mismatch-0ffc5bfbb01c400293929475848d687f/`。保存门禁在合并运行中1/1通过。合并失败的根因尚未从并行测试资源或时序中唯一确定，不归因为产品F协议缺陷，也不删除失败记录；后续涉及同一VirtualPlc/worker实例的测试须顺序运行。

当前页面心跳在三条正式流程中未触发Blocked；这只证明对应运行正常，不补造历史缺失的调度分段日志，003 T065按原条件仍未完成。F1/F4各有必要子项证据；F2沿用第四批组件；F3及后续业务专属失败仍须按其原任务验证。
