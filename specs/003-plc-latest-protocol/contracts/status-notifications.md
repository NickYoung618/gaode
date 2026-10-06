# 第一工位状态通知合同

011实施增量：当前人工取盘门为Host实际提交ManualRemovalAllowed；查询输出readyForRemovalSourceMatrix/readyForRemovalSourceMatrixId/manualRemovalAllowedEventId，通知eventType=ManualRemovalAllowed，仍用s01/notification/2.0信封及同run GET。旧ObservedUnlocked/ReadyForUnlock及旧字段只对应历史原记录，不产生当前动作许可或物理解锁成功声明。Final仍须实际人工确认和原子保存，许可/Final失败不释放运行租约；Final当前提交后才释放。012仅在现有界面绑定新字段/事件，不重建业务判断。

**合同版本**：通知信封沿用`s01/notification/2.0`；本页于2026-10-03按011/012澄清定向同步。运行阶段和异常槽位引用[station01-execution/1.0](../../011-plc-interaction-update/contracts/execution-and-state.md)，配方/冻结内容引用[recipe-contract/1.3](../../011-plc-interaction-update/contracts/recipe-contract.md)；均为目标设计，非实现完成证明。

当前`GET /status`按`s01-status/2.0`提供语义设备观察、真实观察时间、reliability、executionOrigin及受控诊断引用；无可靠安全依据为Unconfirmed，不能推导ExplicitUnsafe或Clear。2026-09-24的原始字段投影只保留历史读取范围，不继续公开diagnosticCode/failureOrigin等内部通道信息。受理后停止依据以001运行快照的`startupDiagnostic`及其semanticObservation为准，通知仅触发GET重取。

## 复用边界

复用现有 `/hubs/station01`、`NotificationEnvelope` 和有界通知通道，不新增消息系统、事件总线或通用 outbox。通知仅在对应数据库事务提交后发布。

## 信封

当前信封只采用下方009/AL04已定的`s01/notification/2.0`形状：eventType、runId、revision、persistedRevision、changedFields、occurredAt及有限summary对象；不另建本页旧1.0形状。来源矩阵、盘身份、处置及完整状态经同run GET读取，不复制整个RunSnapshot到通知。changedFields仅使用真实发生变化的业务路径，覆盖配方绑定/冻结引用、实际执行阶段、姿态异常物理槽号、handoff、处置、WholeTrayCompletion、ManualRemovalAllowed和FinalUnloadCompletion；具体路径以011执行合同为准。缺失观察不得生成空异常集合假称正常，通知不得替代真实来源矩阵。

保留现有 StateChanged、OperationChanged、DiagnosticChanged、HandoffReady 语义；HandoffReady 不得让客户端显示第一工位最终完成。

## 一致性

通知是变化提示，不是设备事实或完成证据。客户端收到重复、乱序、遗漏或 revision 跳跃时，按 runId GET 持久化快照并以 `persistedRevision` 对账。通知失败不能阻塞 PLC 安全路径，也不能让内存状态领先数据库。

## 消费者边界

本合同只定义Host发布和GET对账语义。011负责真实阶段、配方绑定/快照及异常物理槽号生产；012负责006已有界面的数据绑定与必要消费验证，不新增页面，也不重算质量、姿态或动作完成。独立Test/联调客户端继续按真实来源标识消费同一合同。共享的保存→重读→F匹配→快照隔离及代表链采用同次证据；本轮不重新要求历史五场景或全量E2E。前端仍不得直连PLC、算法或业务数据库。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前消费宪章8.0.0及2026-10-03统一澄清。原实施完成与失败证据保持原范围；本次目标更新不代表实现/验证完成。

继续用已提交revision通知触发GET重取。012/006按当前后端快照绑定recipeSelection/recipeExecution、检测面与独立E扫码姿态、对象/物理槽/姿态、结果/位置与allowedActions，不根据通知先后或固定1/2/4面列表猜流程。实际顺序包括翻转放回后的统一3D复查且不重新F绑定、分拣后下料；姿态异常跳过后续检测，最后从原槽实际分拣到Pending，OK分拣不搬运，NG/Pending按各自目标点执行。未知字段如实显示未知，动作受理、质量、姿态、处置和Final分别表达。保留startupDiagnostic及来源矩阵；原始异常只在受控日志，公开结果脱敏。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready核RC05.1的RecipeBindingReceipt及适用真实业务提交，不要求旧DeviceApplied；不能从已有handoff恢复续接资格。

## 013实施前定向同步（2026-10-04）

SY-01：FR03/FR15当前采集按013 A01/A03/A07：独立心跳300ms；基础活动200/空闲500ms、动作反馈200ms、位置运动或未知500/确认静止1000ms。首Moving/Executing仅有依据的局部50ms、齐备后200ms。必要读写/清零即时，不等慢周期；相对plannedDue计调度迟延。原I/O、3秒、动作/保存截止及T065真实超期锁动作不变。旧T065“50ms不变”为当时构建事实，不再约束013现行策略；新周期不代替真实反馈或当前安全证据。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。
