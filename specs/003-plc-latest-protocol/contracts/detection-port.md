# DetectionPort 合同

## 阶段边界

Detection 只能调用正式 `IDetectionPort`。请求必须由同一 `runId/trayId` 的已提交 001→003 handoff 构造；不得由测试客户端预造，不得直接调用 PLC 或推进分拣、下料、解锁。

## 请求

`DetectionRequest` 至少包含：WorkflowIdentity、`Stage=Detection`、已持久化 `OperationId`、`RecipeRunPlanRef/PlanRevision`、截止时间、占用对象、输入媒体引用、用途和来源策略。计划、身份、媒体或租约与 handoff 不一致时明确拒绝。

正式适配器负责通过既有采集/算法端口产生并保存所需输入引用；Simulated/Test 适配器也必须产生可追溯输入，不能用空引用或编排器硬编码结果。

用于007虚拟集成时，Host按同一已提交handoff的冻结`RecipeRunPlan`逐项执行全部必检Capture步骤：每项经正式采集入口从运行前冻结的单一目录读取对应固定图片，实际采集并保存本次独立媒体；每项检测采集对应一次经正式算法端口派发到独立虚拟worker的请求和响应。采集/算法都关联同一runId、trayId、planRevision、对象、步骤、captureId/mediaId、callId/attempt及来源；不得用一次批量固定OK或复用预制媒体URI代替。公共3D/F仍经各自正式采集与算法入口，F保持单次触发。

## 结果

端口事实消息使用同一 operation ID：`Accepted`、`Executing`、`Completed`、`Failed`、`TimedOut`、`Disconnected`。每个对象结果包含稳定 object ID、position、`OK|NG|Pending`、target hint、结果/媒体引用、attempts、原始 error reference、source、quality、run/tray/plan identity。

算法失败、超时、未配置、未接入或无有效结果经过有限尝试后，适配器/应用边界必须形成带原异常的逐对象 Pending 结果；这不是成功伪造。只要身份和目标完整唯一映射，Detection 可提交 Completed 并继续 Pending 分拣。

缺失、重复、歧义对象身份/位置/目标时追加 `MappingFailed`，暂停当前托盘并进入人工核对；不得猜测或默认分拣。Detection 异常不使用 PLC `UnknownHeld`。

## 重试与期限

阶段开始时冻结 `StageDeadlineAt = StageStartedAt + 冻结阶段预算`。临时通信错误在首次调用外最多重试 3 次（总计最多 4 次），退避 1/2/4 秒；Detection 算法超时在首次调用外最多重试 2 次（总计最多 3 次），退避 2/5 秒。两类尝试与退避共用同一个 `StageDeadlineAt`，重试、重连或 Host 重启均不得重置、嵌套或倍增期限。

007 Test样本的固定图片采集每次实际耗时3–5秒、worker每次实际模拟计算10秒；新版本化Test预算须覆盖公共准备窗口，并以正式计划和实际调用数核算Detection的`5C+10A+T+R<按冻结RecipeExecutionBudget确定的`。CAP/P01候选预期C=2、A=2，最终以冻结计划和运行证据为准；不得减少必检步骤或缩短指定延迟制造通过。

每次尝试、失败和重试计划均追加 attempt、error、plannedAt、deadline、operationId 和 evidence reference。Detection 临时通信错误/断联在 4 次总尝试耗尽或共享期限先到时，必须为受影响已知对象生成带原始异常、attempt、deadline、输入/结果引用及 source/quality 的 Pending；算法超时在 3 次总尝试耗尽或共享期限先到时同样按逐对象 Pending 收敛，身份/位置/目标映射完整后由 Workflow 提交 Detection 完成事实并继续正式 Pending 分拣；缺失、重复或歧义映射仍为 MappingFailed，不借 Pending 猜测对象或伪造媒体。不得将 Detection 通信失败转为 OK 或 PLC UnknownHeld，不无限等待、不自动切换隐藏适配器。只有当前活动任务的无物理副作用尝试可在原 operation/deadline 内恢复；旧任务重新检测必须先有受控人工恢复决定。

## Real 与 Simulated/Test

真实和 Simulated/Test 实现同一接口、请求、结果、状态和保存流程，由 Host 组合根显式选择。每条事实传播 source/quality；Simulated/Test 结果可用于软件闭环证据，不得标记为 Real/Production。端口不得生成 PLC 完成、整托完成、人工确认或直接写业务投影。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

IDetectionPort承接008冻结计划，由应用执行入口消费全部归属Detection的适用步骤，经唯一Motion/设备端口派发；“不得直接调用PLC”仍禁止基础适配器旁路，不禁止正式应用运动。普通盘末Sorting及Unload由外围唯一消费，特殊出口已处置实体不再重复。

每图实际单输入分析有限收敛并保存后完成本相机复位，前批不等后批；同对象/面/轮次两个输入齐后另发独立融合，正常检测调用A=C+F，公共/E/重扫另计。双输入及租约按008执行合同，旧每Capture一次及120秒只适用旧样本。机械动作已派发而未知必须UnknownHeld/保持占用；可恢复算法尝试不重启整个Detection。F合同已定义待接入，E及其他B依赖只限制对应动作。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

IntegratedDetectionPort虽位于Simulation目录仍是正式文件相机/Worker消费方，不得引用具体协议类型或协议版本作为业务输入；通过语义采集窗口及当前结果保留每图/融合/媒体/算法和保存节点。算法有限失败Pending不替代机械安全/未知保持。公共3D受控失败清理是原特例，F/E/产品检测不得自动继承。


### 009 / AL03 检测保存窗口的具体接线（2026-10-01）

此处细化既有“必要保存受冻结 CriticalSave 及剩余阶段期限约束”，不新增预算或刷新 Detection 起点。`DetectionRequest.CriticalSaveBudgetMs` 是本轮冻结 `BusinessBudget.BusinessMs.CriticalSave` 的毫秒值，由 `StartPublicPreparation` 从同一 `RunExecution.Config` 填入，与既有 SessionId/SnapshotId/ClockId 一起传递；不是协议字段。缺失或非法值使正式 IntegratedDetection 在派发前拒绝，不填固定两秒默认值。独立组件测试须显式提供其预算来源。

每份必要检测事实、媒体元数据及阶段事件保存，在查询/排队前建立 `min(原 Detection 剩余期限, 冻结 CriticalSave)` 的有限窗口。窗口和取消传至真实 writer/事件保存入口，排队尚未开始提交的工作超期不再开始。提交已经开始后，超时不宣称回滚；没有当前有效回执不得续接机械动作。保存后须核实际提交及当前期限，失败事实仍沿既有失败记录规则。

单图/融合的算法意图使用实际当前 SessionId、ClockId 和同一调用的单调起止时间，与随后 AlgorithmRequest 一致；既有算法等待上限和释放规则不变，不再保存伪造的 1/2 时间戳。当前仅完成接口对齐，实现和验证由009 T035/T048/T049承接。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。

当前来源分类的有限补齐：ResultSource在末尾新增Test，保留既有Real/Virtual/Simulated/Fallback的值和历史含义；仅由明确声明Test的实际算法生产者产生，不从RunPurpose猜测。IntegratedDetection的Source与意图来源来自IAlgorithmPort.Origin，未知仍Fallback/Unknown；完整矩阵继续使用AlgorithmOrigin与实际事实。该变化用于消除把独立Test Worker写成Simulated的固定标签，归009 T034/T035/T039及T043—T046，旧记录不重写、现有页面仅绑定来源。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。

### 010实施定向对齐 A03（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
