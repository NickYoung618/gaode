# 组件来源矩阵合同

**合同版本**：`station01-component-source-matrix/1.1`

## 目的

整托证据必须保留混合来源，禁止把 Host、PLC、相机、光源、算法和人工主体压缩成单一 `Real`、`Virtual` 或 `quality` 标记。矩阵是完成记录的不可变组成部分，摘要只用于查询和通知，不能代替矩阵事实。

## 矩阵与组件

矩阵保存 `matrixId/schemaVersion/runId/trayId/planRevision/milestone/createdAt/matrixDigest`，并固定区分：

- `Host`
- `PLC`
- `Camera`
- `Light`
- `Algorithm`
- `ManualActor`

每个组件保存：`component`、`requiredAt`、`evidenceState`、`source`、`quality`、`versionRef`、`evidenceReferences[]`、`capturedAt` 和 `digest`。

`evidenceState` 为 `Verified|NotYetRequired|Missing|Unknown|Unverifiable`。当前里程碑必需的组件只有在 Verified 且 source、quality、versionRef、evidenceReferences 完整可核验时才通过；Missing、Unknown 或 Unverifiable 阻止对应完成。

## 两个不可变快照

`ReadyForUnlockSourceMatrix` 随 `WholeTrayCompletion` 在独立聚合短事务中创建。Host、PLC、Camera、Light、Algorithm 是当前必需组件。ManualActor 槽必须存在，标记 `requiredAt=FinalUnloadCompletion`、`evidenceState=NotYetRequired`；尚未发生的 actor/source/quality/version/evidence 不得伪造，NotYetRequired 不视为 ReadyForUnlock 的缺失证据。

`FinalSourceMatrix` 随 `ManualTrayRemovalConfirmed` 和 `FinalUnloadCompletion` 在同一后续短事务中创建。它引用并重新核验 ReadyForUnlock 矩阵，再加入从认证上下文取得的 actor、角色、确认事件、requestId、调用渠道版本和证据引用。007自动模拟取盘的ManualActor来源必须为Test/Simulated，保留受控Test身份和客户端版本；认证身份不等于真实人工移盘，不得将该来源写成AuthenticatedHuman。原矩阵不可回写。

## 非生产标记

`evidenceScope` 由组件事实派生：

- 任一组件来源为 `Virtual|Simulated|Test`：`SoftwareLoopOnly`，且 `productionClaimAllowed=false`。
- 所有当前必需组件均为 Real/AuthenticatedHuman：最多为 `ProductionCandidateNotAccepted`；仍不得由软件自动声明生产验收。

本期 VirtualPlc、Simulated/Test 相机/光源/算法和 Test/Commissioning 联调渠道使最终证据必然为 `SoftwareLoopOnly`。

## 查询与通知摘要

允许派生 `matrixRef`、`matrixDigest`、`sourceKinds[]`、`containsNonProductionEvidence` 和 `blockedComponents[]`。禁止派生会掩盖混合来源的单一 `Source=Real`。需要验收或恢复判断时必须读取完整矩阵和证据引用。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

每个适用运动、采集、独立worker、媒体/SQLite提交与前端人工操作分别登记来源及关联。计划展开或未执行step不能标Completed；模拟图像、算法、PLC与操作者渠道分开。Q01—Q22同run证据须覆盖全部应检/应处置实体、无未知在途件和Final。008必要人工确认来自正式006页面，旧007外部Test客户端只保留原范围。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

各组件provider/version?/quality从实际生产者透传，Real/Virtual/Simulated/Unavailable与历史声明性质分离，不按Host模式或新语义反推历史raw/执行来源。新设备事实关联同次ObservationId及真实提交后opaque引用；没有实收报文时raw缺失如实null/NotRecorded，旧合成值不变成ActualWire。原Host/PLC/Camera/Light/Algorithm/ManualActor完整矩阵及SoftwareLoopOnly限制保持。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。

当前来源分类的有限补齐：ResultSource在末尾新增Test，保留既有Real/Virtual/Simulated/Fallback的值和历史含义；仅由明确声明Test的实际算法生产者产生，不从RunPurpose猜测。IntegratedDetection的Source与意图来源来自IAlgorithmPort.Origin，未知仍Fallback/Unknown；完整矩阵继续使用AlgorithmOrigin与实际事实。该变化用于消除把独立Test Worker写成Simulated的固定标签，归009 T034/T035/T039及T043—T046，旧记录不重写、现有页面仅绑定来源。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
