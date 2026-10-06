# USR-20260926-D计划交接与核验

本轮仅speckit-plan，既有008目录。setup-plan已确认现有plan存在并跳过模板；未创建功能或分支。JSON中的BRANCH=008-recipe-driven-inspection为无Git时功能目录标识，不是新分支。before_plan/after_plan无登记hook。规格质量仍15/16，第一项历史实施备注保留，不虚报全通过。

## 当前事实与历史适用范围

- 普通Q01—Q22页面Final依据[批次报告](evidence/q04-q22-test-batch-20260926.md)、[首批报告](evidence/implementation-batch-20260926.md)及各自运行包的构建/配置摘要；不是当前源码或新恢复通过。原包不覆盖，不重新执行。
- `artifacts/recipe-execution-008/closure-integrations-20260926/closure-new-scope-v3.trx`读回为6项、5 Passed/1 Failed：GROUP-A-E、E失败继续、ASSEMBLY NG优先留Pending、人工整体E及旧单指令恢复通过；当前参数回归失败。旧恢复Passed仅USR-C，不抵USR-D。`four-gates-v3.trx`为2/4通过，后续`two-gates-v4.trx`两项通过，按具体用例对应保留，不把旧失败原文改成成功。
- 当前源码已包含CaptureFact/RequestedCaptureSettings及参数保存修订，不能据此声称最近失败已复测通过。历史`closure-build-v4`与各包构建摘要只对其生成时有效，本轮没有构建/业务测试。
- E已有[003 E合同](../003-plc-latest-protocol/contracts/e-test-execution.md)及实际IntegratedDetectionPort→媒体/EDecode→保存/复位接入；旋转已有[Test请求/结果合同](../003-plc-latest-protocol/contracts/rotation-test-execution.md)及VirtualPlc进程接入，不再把角度当Host输入前置。当前特殊任务复位隔离尚有明确缺口。

## 复用与代码消费者、唯一任务归属

| 能力/实际消费者 | 复用或必须修改 | 唯一归属及下一阶段任务调整 |
| --- | --- | --- |
| `StartPublicPreparation.cs`、`RunExecution.cs`、`Steps/StartClampStep.cs` | 复用新run执行、公共3D/F及绑定；增加restartFrom准入和持久关联；故障不能用RecoveredStart或旧handoff续接 | 001 T052承接公共暂停/故障语义，T078承接持久控制/幂等基础，T054验证公共新轮；001 T051只承担已有事实保存核验，不复制恢复API |
| `FixedMoveRecoveryInteraction.cs`、`Steps/FixedMoveStep.cs`及既有Workflow恢复消费者 | 退出故障同run/same-operation attempt2路径；旧事实保持可查；业务关闭旧轮、收敛worker与资源、完整新轮协调 | 008 T068唯一恢复业务；依T054保存及003设备子能力，不等待无关父任务全部勾选 |
| `LatestProtocolPlcDevice.cs`、`StartupReadiness.cs`、`Ports/DeviceMessages.cs` | 现有ResetAsync仅Ready/Auto/无Fault；补真实初始观察和映射读取，不本地清Unknown成成功 | 003 T072设备/控制合同子范围；003 T069只复用盘末/查询接口，不能重复建恢复API |
| `MotionCoordinator.cs`、`ResourceLease.cs` | 现行ReconcileVerifiedReset保留旧owner；新设计需真实初始及释放证明后结束旧owner，再新run获取 | 008 T068业务协调；003 T072提供可靠设备判据，避免两份协调实现 |
| `VirtualPlcEngine.cs`、`TestSpecialActions.cs`、`SimulatedPlc.cs`（适用） | 实际复位当前活动与占用；屏蔽旧异步回写，保留历史specialResults；补Test状态读取 | 003 T072复位子范围，复用003 T071旋转端口；T071既有三出口范围不被新恢复通过代勾 |
| `ControlEndpoints.cs`、`RunEndpoints.cs`、既有运行查询与RunMediaCatalog | 复用路由；故障continue拒绝，reset/check给逐项初始状态，新POST /runs给独立身份及双向关联 | 003 T072唯一恢复API与查询；003 T068原启动入口不另造第二启动API |
| `TraceWriter.cs`、`StageEventStore.cs`及必要StorePrep模型 | 旧故障、意图、媒体保存保留；短事务提交reset/check、关闭执行、新run关联及单次消费；必要升级不Host自动改库 | 008 T054事实保存，001 T078持久控制幂等；业务关联规则由008 T068使用，不重复基础写入通道 |
| `frontend/src/runtime.js` | 当前RecoveryReset→RecoveryCheckAndContinue→continue是旧U05；换为复位/初始检查、既有启动控件显式新请求；权限不足不得借continue启动 | 006 T051唯一恢复页面绑定；复用T048启动、T049查询与取盘；人工继续归T050，控件/原型不扩大 |
| 旧SingleCommandRecoveryIntegrationTests、InterruptedRun/RecoveryReuse场景及页面采证脚本 | 旧测试/证据保留适用时期；新增或改现行场景为双端初始+完整新轮，不删除门禁或改失败预期求通过 | 008 T069唯一正式C07/F5整链验收；001 T054、003 T072、006 T051各验证其子合同，引用同一包避免重复全链 |

表中源码路径分别在既有`backend/src/Gaode.Application`、`Gaode.Infrastructure`、`Gaode.Host`、`VirtualPlc`及`frontend/src`下。本轮仅只读核对，不修改代码。需要tasks阶段按上述唯一归属调整现有正文；本轮tasks全文件不写、不追加编号、不改勾选。

依赖推进：实际设备初始/特殊Test隔离（003 T072/T071子能力）与持久控制基础（001 T078）→旧轮收敛及新轮协调（008 T068，依008 T054）→复位/新启动API（003 T072，与业务协作但接口只一份）→既有页面绑定（006 T051，复用T048/T049）→C07/F5代表完整新轮与对照（008 T069、001 T054）→后续既有T070收口。人工面来源T060/T050、E T061、组T063/T064、旋转T065原范围保留。

## 最小设计与尚未实现的边界

完整规范见[003恢复合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)。派生时序见[008 sequences](sequences.md)，001公共步骤引用新run；002当前配方合同未受结构变化影响，本轮不修改。

旧轮关闭执行仍保留故障结论；不得造Final。先Host关闭派发/收敛在途及必要保存，再真实PLC复位，实际初始判据及资源释放成立后显式新request/newrun。真实夹紧在新StartClamp执行，复位期间Ready/锁0不能冒充已夹紧。新3D/F与绑定、结果/预算/媒体身份不复用旧轮。普通暂停continue与人工换面manual-confirm分别同run，不重采公共3D；故障continue拒绝。

普通Test初始可由既有SIM坐标系/合法安全范围及当前映射实现，无需生产标定。生产特殊机构占用/回零缺可靠观察时按§4.3人工物理核对，仅限制该分支；生产安全范围待现场，不能用Test范围冒充。E/旋转业务决定不再等待用户重复确认；特殊Test新状态读取及generation隔离是软件实现工作。

## 本轮静态核验与保护

修改前副本、SHA、逐文件并发检查、文档diff及最终核验JSON保存于`C:/Users/codexsandboxonline.10_3_0_13/AppData/Local/Temp/gaode-plan-restart-20260926/`。任何检测到的并发变更不得覆盖。新增本交接文件事前检查不存在；不回写历史报告。

最终核验范围为新增/改动文档的引用与状态语义、派生Mermaid、原编号、tasks完整摘要及所建来源/代码基线。普通路线Passed不代父任务完成；新恢复尚NotRun。本轮无build/test、数据库操作或业务进程操作，也没有执行speckit-tasks/analyze/implement。
