# 009 语义与证据数据模型

**当前模型适用边界**：通信代码边界最小收敛；现有业务/证据语义模型与保存/取消规则保持，当前只核类型与责任边界及直接接线。完整预算/持久历史/升级/故障动态验收转出，U1提交未知不改变。活动判据见[verification-gates BM00](contracts/verification-gates.md#bm00-当前有限必需集合规范性009-boundary-minimum1)，不是S00/PD/M动态通过。

状态：现有设计和部分实现保留，新专项整体尚未验收。类型名称与现用代码核对，不因文档存在宣称已实现/通过。业务对象不携带协议字段；原始模型只供通信、持久诊断和通信测试使用。

## 1. 身份、时间和来源

| 对象 | 字段及约束 | 所有者/用途 |
| --- | --- | --- |
| ActionCorrelation | RunId、TrayId（已建立时）、OperationId、ActionId、Attempt、SessionId、ConnectionEpoch、PlanRevision/SnapshotId、ObjectId/GroupId/AssemblyId、LocalFace、PhysicalSlotIndex按动作适用提供 | Application建立业务身份；通信关联当前独占动作。缺失身份为null，不从旧动作/请求目标补造反馈身份。物理槽号是实物位置，不是步骤Sequence或寄存器号。 |
| ActionWindow | 原StartTick/DueTick/ClockId或StageStartedUtc/StageDeadlineUtc、冻结动作预算、反馈观察时间 | 沿用现行计时来源和ResponseBeforeDeadline。具体握手子期限在通信侧取原子预算与剩余阶段期限的较小者，重试不刷新绝对期限。 |
| ExecutionOrigin | Provider=Real/Virtual/Simulated/Unavailable；ComponentVersion；事实质量Measured/Derived/Degraded/Unknown；可关联组件证据 | 由实际适配器/采集/Worker来源形成。Host汇总事实可为Derived，但必须保留各输入真实Provider，不能统一写Virtual或Real。 |
| ObservationIdentity | ObservationId、ConnectionEpoch、SampleStartedUtc、SampleEndedUtc、各证据实际ObservedUtc、可靠性 | 由通信采样生成。同批可能有多次Modbus交换，不宣称PLC原子快照；跨epoch、过期或动作前样本不能成为当前完成依据。 |
| DiagnosticEvidenceReference | 不透明EvidenceId、StoreId及引用schema；业务只保存/转交 | 仅确认必要通信证据真实提交后返回；业务不解析/查raw/自行生成。无有效引用只说明调用方未确认，不能推断库内无行；恢复取得历史引用不恢复旧动作授权。 |

数值约束来自业务：坐标有限、容差非负、物理面/槽合法、期限正向、当前计划身份相符。Int16/Float32字数、bit范围、寄存器可表示范围由通信验证，不进入这些类型。原配方JSON字段保持现有载荷；配置适配器将旧名`protocolSlotIndex`解释为既有物理槽身份，不把其名字当成传输类型。

## 2. DeviceObservation：公开状态及实际用途

所有状态均为字符串序列化的语义值，无与PLC数值相同的显式枚举赋值，无整数强转恢复协议。表中状态是本次业务端口的完整分类边界；不把每个内部握手阶段映射成一个公开状态。

| 属性/状态 | 业务消费者及用途 | 为何独立于协议 |
| --- | --- | --- |
| Reliability：Reliable / Stale / Unavailable | MotionAdmission、启动诊断和恢复拒绝无当前依据的动作 | 由采样/连接/期限判定；不是某个寄存器值。 |
| Connection：Connected / Disconnected；ConnectionEpoch | 所有动作关联、失联后关闭准入；新代次不接续旧动作 | 连接事实，不是PLC动作完成。 |
| OperatingMode：Automatic / NonAutomatic / Unconfirmed | 当前自动流程准入 | 通信把现用模式信号解释为业务模式。 |
| Readiness：Ready / NotReady / Unconfirmed | 启动与恢复初始状态 | 综合已确认的设备就绪条件；业务不见清命令/ACK检查表。 |
| SafetyAssessment：Clear / ExplicitUnsafe / Unconfirmed | 启动、动作准入、人工继续；保留现行保守准入 | 不以Severity枚举序号比较安全，不公开报警掩码。 |
| Clamp：Secured / Released / Unconfirmed | 启动夹紧等待、运动准入、解锁事实；夹紧失败通过失败原因表达 | 使用夹紧/释放的物理含义；任意未知线值不成为Secured或Released。 |
| MotionAvailability：Available / InUse / HeldUnknown | MotionCoordinator和资源占用展示；租约仍由Application负责 | 是动作能否申请的结果，不暴露Moving/Arrival/Reset的编码。 |
| AcquisitionReadiness：Available / Unavailable / Unconfirmed | 3D、F/E、产品采集申请窗口的前置 | 只表达可否启动采集周期；内部检查检测空闲、轴及前周期复位。 |
| ManualArea：Clear / Occupied / Unconfirmed；ManualHandling：Waiting / Confirmed / Unconfirmed | 人工换面准入/继续和现有状态展示 | “确认”还需当前请求、占用清除与安全核验；不公开完成线圈/清零阶段。 |
| PositionObservation | 当前实际X/Y/Z、已确认的轴用途、单位/基准来源、采样身份/时间/质量 | 业务空间事实，可与合法目标核验；从真实采样得到，不从请求复制。无适用轴依据则Unconfirmed。 |
| FaceObservation | 实际/采用面、对象/动作关联、FaceSource=DeviceObserved或CommandDefaultManualConfirmed、时间 | 业务确需知道当前对象的面。仅已确认人工流程允许采用请求面并明确来源，自动翻面必须设备实测匹配。 |
| Alarms | 稳定语义名称/原因、Severity=Warning/Fault/Critical/Unknown、可靠性 | 已确认报警含义集合见通信合同；通信解析多位组合。业务只消费安全评估和语义原因，不做位运算；Severity不单独决定放行。 |
| ObservationId及可选DiagnosticEvidenceReference | 业务日志、动作事实和只读查询建立关联 | 身份/引用不承载原始值，不是另一套完成判据。 |

不公开`InspectionStatus`、`ZResetStatus`、`FlipStatus`、`ProtocolStatus`、`AlarmBits`、`AlarmSeverity`原值、`PalletLockStatus`、ACK阶段或`Checks["SortingCleared"]`。设备初始状态改为`InitialReadinessAssessment`：Ready/Blocked/Unconfirmed及业务阻断原因（安全、占用、设备未就绪、位置无依据、未结束设备工作），附观察引用；具体清零清单只在通信诊断。

## 3. 业务动作、结果与采集窗口

### DeviceActionRequest / DeviceActionResult

请求保留现有启动、定位、停止、自动翻面/人工继续、取放、下料、解锁、恢复、区域容量与显示配方绑定等目的。包含ActionCorrelation、已提交意图引用、合法目标/面/实体/物理槽、用途/Provider、原期限与必要业务前置。解锁必须引用已提交WholeTrayCompletion，不接受裸bool。

结果Kind保持Accepted、Executing、Completed、Failed、TimedOut、Disconnected、UnknownHeld的区别。结果包含关联、业务FailureReason、ExecutionOrigin、ObservationIds、语义位置/面证据和已提交诊断引用；不含编码或内部阶段名。

- Accepted在现有启动路径中的语义是“当前请求提交已确认”：完整真实启动写序列获得提交确认，随后先保存该提交事实，再开始原有独立夹紧等待窗口。它不表示设备已执行、实测按钮或夹紧完成，不要求协议不存在的额外启动受理反馈。原始逐报文ACK仍只在通信诊断；业务只见该请求的提交结果。原启动受理期限与夹紧期限保持分离。
- Completed只在当前动作/epoch/身份、原期限、安全和该动作所需物理完成、必要内部处理及证据保存均成立时产生。业务仍需另行提交动作/占用/整盘事实，Completed不等于Final。
- Failed表示有依据的拒绝/失败；TimedOut和Disconnected不隐藏是否已派发。可能派发且结果不确定时，必须`HoldsDevice=true、CanRetry=false`并以UnknownHeld影响业务占用。
- 未识别反馈、错面/错位置/旧反馈、必需证据保存未知均不产Completed。新语义错误分类至少覆盖FeedbackUnrecognized、FeedbackNotCurrent、PositionUnconfirmed、FaceMismatch、DeadlineExceeded、ConnectionChanged、RequiredEvidenceUnavailable、RequiredCommitUnconfirmed；详细raw只在诊断。

`PositionReachedEvidence`记录目标的业务版本、实际XYZ、轴用途、容差、当前动作、观察时间/ID和匹配结果；它与后来的物理取料/放料完成证据分开。目标到位后PLC抬升不撤销已可靠采到的目标证据，也不能以早于本动作的采样替代。

### AcquisitionSession

替换公开的协议握手上下文，保留业务可见生命周期：`Requested → CaptureAllowed → WorkCommitted → Released`；失败为Blocked或HeldUnknown。Requested/CaptureAllowed/Released是会话合同结果，不扩展为全局设备寄存器状态。

会话持有当前定位动作/OperationId、角色3D/F/E/Detection、目标/轴、epoch、原期限和定位证据。开放窗口只在当前可靠定位和安全准入后成功；正常路径由业务完成真实光源/采集、独立算法、媒体释放及必要保存，然后带真实保存引用请求结束。通信执行对应操作结束、轴复位和内部清零，返回Released及证据。Released不代表F绑定成功或图像质量OK。失败/保存未确认不得靠正常结束调用跳过门禁。

保留当前公共3D的失败清理差异（`ThreeDStep.cs:56–62`）：未取消、无安全故障且取消令牌未取消时，原实现即使采集/算法/保存抛错仍尝试周期清理。语义端口为该既有路径提供`CloseFailedCaptureWindow`，携当前会话与原失败；只返回清理观察及关联证据，不进入正常WorkCommitted/Released成功链，不授权后继运动、不把原业务失败改为Completed。清理失败或必要证据不可保存则保持未知及真实错误。该能力仅承接公共3D现有行为；F的`captureEndedAndRecorded`门禁以及E/Detection各自当前保存门禁保持，不扩大为统一“失败也结束”。

### 3.1 配方应用预算、总窗口与成功回执（完整功能验收转出，已确认约束保留）

以下为本次已接受澄清的设计落实，不改变§4取料提交四态或已有后段预算。预算的完整来源属于业务事实；通信只接收执行需要的业务关联、有效绝对期限及取消资格，不解释配置版本或重算业务预算。

| 对象/字段 | 类型、校验与来源 | 生命周期/消费者 |
| --- | --- | --- |
| BusinessDurations.RecipeApplication / JSON `businessMs.recipeApplication` | 必需正整数ms，有限且加到当前时钟不会溢出。目标预算schemaVersion=`1.1`（现1.0无该字段）；预算实例ID/新version/purpose/source/digest及完整快照引用必须有效。Test初值10000；Production须另有批准值 | 复用BusinessBudget、ConfigurationLoader/Freezer；公共配置自身schema仍按其合同，不随budget字段盲目升版。旧budget缺项不能补默认值，活动运行不重新加载。新版本配置/001合同尚待对齐 |
| RecipeApplicationBudgetReference | BudgetId、BudgetVersion、Purpose、Source、Digest、SnapshotId、RecipeApplicationBudgetMs；均不可空 | 业务从关联运行冻结快照提取；意图及后续事实保存可追溯引用。三类入口共用；独立API不借已有handoff跳过本次意图 |
| RecipeApplicationWindow | ClockId、StartTick=t0、BudgetDueTick=D、EffectiveDueTick=T、已有适用后段Deadline引用及审计UTC；D=t0+冻结ms，T取D及这些原截止最小值 | 意图实际提交且有效回执后、排队/端口前唯一产生；沿现Host单调时钟计时，UTC仅供审计，同域转换不得刷新起点。保持活动值到请求终态 |
| RecipeApplicationRequest | ActionCorrelation（含RunId、当前BindingId/ActionId、PlanRevision、epoch）、IntentReference、业务显示标识、可空的两区域容量、有效ActionWindow及取消资格 | 通信受理前核对当前身份/占用/剩余时间。两容量同空或同有值；连续链与独立API输入差异按B03.1保留。不带寄存器、ACK阶段、重试次数预算公式 |
| RecipeApplicationEvidence | DeviceApplied；当前关联、输入摘要、真实ExecutionOrigin、ObservationIds、HostReceived/ValidatedTick、已提交DiagnosticEvidenceReferences | 仅确认适用设备应用与必要raw证据，供业务保存使用；不表示RecipePlanBound/handoff已保存，不得直接发布有效Bound。未知/超期/取消无DeviceApplied成功回执，已知设备事实可另记录 |
| RecipeApplicationReceipt | 当前BindingId及Window/BudgetReference、适用HandoffReference/Run/Plan关联、必要WriteId/提交修订/诊断引用集合、各ActualCommit/ReceiptValidity、最后HostValidatedTick、授权用途 | 仅全部本次必要依据及有效回执在[t0,T)齐备生成不可变成功结果；本次准入资格另核当前取消/安全。独立API只授权本次绑定返回，不重复产品动作。消费者不能仅凭handoff行重新生成Receipt；沿§4.1分开实际提交与及时回执 |
| RecipeApplicationFailure | 当前关联/预算/窗口、DeadlineExceeded/Cancelled/RequiredEvidenceUnavailable/RequiredCommitUnconfirmed等语义原因、已知DeviceApplied事实（可空）、实际保存状态和回执有效性、HoldsDevice/CanRetry | 取消/超期关闭后继准入；已派发但不确定保留占用、不重发。失败记录使用原有限保存规则；无持久回执只表示未确认，不能宣称库无记录或保证存储全失效时新事实已保存 |

业务生命周期为`IntentCommitted → Applying → AwaitingRequiredBusinessCommits → Completed`，失败出口为TimedOut/Cancelled/Failed，可能已经派发但结果不可靠时保留HeldUnknown。Applying由当前绑定请求使用；AwaitingRequiredBusinessCommits说明尚不能发布可续接Bound；Completed说明全部必要保存回执有效。这些是局部业务过程状态，不扩充全局DeviceObservation或照搬通信阶段。`RecipePlanBound`是不可抹除的已存事实；有效Bound展示/续接必须等待本次适用handoff也获得及时回执，二者不可混用。

总窗口包含排队、前置等待、适用容量/显示处理、必要通信证据以及绑定/适用handoff保存；每次保存到期为`min(入队tick+CriticalSave,T)`，t0之前意图保存仍受原CriticalSave及已有较早截止限制。设备已应用不关闭总窗口。最后必要回执在T−1ms可有效，T或之后无效；提前取得并完成校验者不因稍后调度倒判迟到，后继仍检查当前安全/取消/后段期限。

严格连续链沿用绑定前冻结Detection/Unload/Sorting起点和值；旧路径沿用handoff后首次生成Detection起点；独立API使用关联运行实际存在的适用期限，无则仅D。不得重算原RecipeExecutionBudget公式或添加10秒补偿后段。超期/取消的同一准入资格约束通信写派发、设备结果发布、业务成功链保存发起及授权；已开始I/O/提交如实记录，不能以取消等待推断撤销。

配置缺失/null、零/负、非整数/非有限/超范围、缺版本/冲突、用途错误、缺来源/digest/冻结引用均拒绝。连续链运行配置校验阶段拒绝启动；独立API在任何绑定写前拒绝。来源/时限元数据存入现有冻结配置及本次意图、绑定/移交或失败事实的`device-semantics/1`载荷，不另建预算存储平台或修改旧payload。精确公开映射见影响矩阵§5.5；没有历史记录的值返回null并标未记录，不回填10000或伪造旧deadline。

**字段产生与持久化次序**：意图事务只写预算/计划/当前关联，意图有效回执后才登记t0，随后绑定/handoff或失败事实保存这个窗口。原事务只能包含写前已知事实和先前已获得回执；自身实际commit由存储提交记录证明，不能在自身payload预填HostReceivedTick/ValidCurrent或事后改写不可变handoff。Application在接收/校验时产生语义`RecipeApplicationReceiptObserved`，由既有TraceWriter的Audit路径关联WriteId追加观察（仅用于持久审计/历史查询，不是新的成功审批事务、不是通信直接接管业务保存）。活动窗口内的观察保存取原CriticalSave与T剩余较小者，终态后的失败/迟到/收尾观察按原有限保存规则；不刷新动作窗口。该固定观察不加入业务批准的requiredCommits集合，不递归要求“其自身回执的回执”，不作为新的后继授权来源。其保存失败如实标证据缺失，不能签发V-BIND证据通过，也不能用查到记录复活旧动作。

当前有效Receipt由本次调用获得并完成校验，后段消费同时检查同一请求尚未被取消/安全关闭及适用后段期限。已经按期Completed的总窗不因调度稍晚变成TimedOut；没有当前有效资格的重启/历史读取只展示事实。Host查询用已记录语义观察与真实提交元数据构建历史结果，缺观察则null/NotRecorded，不从数据库行反推及时有效。

## 4. PickCompletionEvidence与PickCommitReceipt

| 对象 | 必须字段 | 校验及生命周期 |
| --- | --- | --- |
| PickCompletionEvidence | 完整ActionCorrelation、实体/源槽、ReservationReference、源/目标版本与摘要、SourcePositionReached、PickObservedUtc、观察ID、真实ExecutionOrigin、已提交DiagnosticEvidenceReferences | 仅通信侧在当前取料可靠成立且必要内部处理完成后生成；不含“状态2”、ACK或位信息。 |
| PickCommitReceipt | State=Committed/Rejected/Unknown；EventId/WriteId、Run/Tray/Operation/Action/Epoch、ReservationReference、AssignmentDigest、EvidenceId集合、CommittedUtc/PersistedRevision；ReceivedUtc及原ActionWindow | 只有真实事务提交才可给Committed；还需§4.1独立核验当前回执有效性。Rejected/Unknown不携虚构Commit ID；已知已提交但超期回执保留事实并标Invalid，不重解释为回滚。 |
| SortingAssignment（业务保存） | 实体、物理源槽、源/目标点与版本、处置及结果引用、预留引用、状态、相关完成/提交引用、真实来源 | `Reserved → InTransit（可靠取料及真实提交）→ Occupied（可靠放料及完成提交）`；中间不确定保持Held/预留，不因断联释放。 |

顺序约束：真实取料反馈＋本次源位置证据 → 必要通信内部处理 → 必要原始证据提交 → 业务回调查预留并提交InTransit → 同动作Committed回执 → 再核验连接、安全和原期限 → 放料槽/目标/命令 → 可靠放料及内部闭环/原始证据提交 → 业务Occupied提交。动作间不制造设备/SQLite共同事务。取料保存超时可能实际已提交，仍按未知保持占用并只读核对，不自动重派动作。

### 4.1 实际提交、调用回执与授权判据（R02）

三个维度独立：PhysicalPick=Unconfirmed/ObservedAtCurrentSource；ActualCommit=ConfirmedRolledBack/Committed/Unknown；ReceiptValidity=None/Invalid/ValidCurrent。它们描述证据事实而非PLC编码，不按枚举序号推导。只有D允许后继。

| 情形 | 已知物理事实 | 数据库实际状态及判据 | 调用方回执 | 后继及核查 |
| --- | --- | --- | --- | --- |
| A 提交前失败 | 当前动作源点匹配且已观察取料 | 本次事务未提交、回滚已确认、写任务已终止不可能晚提交；核对幂等键/事件/投影均无本次提交 | 无有效成功回执 | 禁止任何放料写/物理重发，保留预留及UnknownHeld。仅超时或一次查无行不足以判A。 |
| B 已提交但回执失效 | 同A | 事务已Committed，实际行/修订和关联可核验 | 回执丢失、延迟到调用窗口失效，或身份/epoch/原deadline不符；没有ValidCurrent | 禁止放料/物理重发；保留真实已提交行。恢复只读发现成功只能增加历史核查事实，不能复活旧动作。 |
| C 提交未知 | 同A | 提交任务/连接结果或当前读回不足，暂不能确认Committed或RolledBack | None/Unknown，无ValidCurrent | 同样保持UnknownHeld和预留；既不断言无记录也不补写成功。只读核对EventId/WriteId/幂等键及三表原子一致性，不能以重新派动作求证。 |
| D 有效提交回执 | 同A | 实际Committed且行/引用匹配 | 当前动作在原窗口内收到Committed；Run/Tray/Object/Operation/Action/Plan/Reservation/源目标/Epoch及证据一致 | 再核对当前连接、安全、占用和原期限才允许放料；任一失效退为不授权，不能刷新期限。 |

提交在数据库成立与调用方及时知道提交不是共同事务。通讯原始批次也遵守A/B/C/D的“实际提交≠及时回执”区分；晚核查可产生历史持久引用，但不是旧动作的有效完成或放料批准。及时且有效的D不因正常微小传输延迟变成B；B指丢失或已失效，不能把所有延迟一律视为数据库失败。

### 4.2 取料证据失败及重启占用（R05）

`PickEvidenceFailureNotice`与`PickCompletionEvidence`互斥用于本次失败出口：前者含ActionCorrelation、ReservationReference、SourcePositionReached（若确已观察）、PhysicalPick、PickObservedUtc?、ObservationId?、真实ExecutionOrigin、ActualCommit及ReceiptValidity（同§4.1，失败出口无ValidCurrent）、FailureReason、HoldsDevice=true、CanRetry=false；可空字段为null，不填目标值或持久引用。通知与业务最小失败记录对应BD B05.1/EC E02.1。

业务可写时新增的只是UnknownHeld失败事实，不是假InTransit；若实际证据批次或InTransit后来被只读确认存在，记录核查结果而不覆盖旧错误、不重发/续旧动作。业务库不可写时不能保证新观察持久化：重启至少读取已提交`SortingAssignmentsReserved`和关联`IntentRecorded/Started`，凡物理执行可能发生且没有已确认安全关闭/受控恢复决定，仍保守Held。不能只用当前StageProjection.DeviceHeld或缺InTransit判断可释放；现有IntentRecorded默认不设置Held。读回核查本身不调用会追加事件的RecoverAsync；如需追加恢复事实由业务另行事务处理，仍不授权旧动作。

公开字段的精确替换、空值及历史标记采用[impact-matrix §5](impact-matrix.md#5-公开字段与历史表示映射r04)；这是本模型的规范性序列化映射，未映射的新公开字段按A02/A10失败。

## 5. 通信专属模型

| 模型 | 字段 | 边界 |
| --- | --- | --- |
| SignalDescriptor | SignalId、区域/文档号/PDU约定、方向、数据类型/宽度、字序、已知值/位表、写方/清零责任、版本及来源摘要 | 纯协议模块；描述当前协议，不是通用脚本/动态引擎。Float32双字布局属于字段类型；字段之间无默认邻接。 |
| RawExchange | 实际Request/Response字节、事务ID、通道/连接ID、站号/功能码、收发时间、方向、实际地址/数量/原始字、错误 | 只从transport收集。失败可有request但无response；不填默认成功包。 |
| CommunicationEvidenceBatch | EvidenceId、ObservationId/ActionId/epoch、已有业务关联、来源/解释版本/字序、原始交换集合、实际解释结果、采样起止、批次摘要、保存时间 | 多交换组成的观察明确非原子；保存成功后只导出opaque引用。高频正常轮询只保留有界窗口，关键动作/状态变化/失败取出必要批次提交。 |

## 6. 持久模型与历史生命周期

- 目标增加`PlcCommunicationEvidence`表：EvidenceId主键、StoreId、ObservationId、可空RunId/OperationId/ActionId、ConnectionEpoch、ObservedStart/EndUtc、PersistedUtc、PayloadSchema、PayloadDigest、RawPayloadJson；索引为Run/Operation、Action/Epoch和ObservationId。完整报文作为同一批次payload保存，避免新建多张通用日志表。诊断内部payload允许协议字段，业务端口不可达。
- 必需批次在一个短事务中落库；引用生成在Commit后。业务事实保存引用而非复制raw。普通当前观察ID可先存在于内存；只有被用于关键门禁或失败证据的批次必须晋升为持久记录，内存ID不是可解引用回执。
- 使用现有SQLite/EF Core、StoreAccessGuard和TraceWriter内部通信job；不改变各既有StageEventStore等短事务所有权，不虚构全库单写者。容量沿用当前有限队列/冻结CriticalSave预算，不新增生产容量常数。队列满/超期视为必要证据不可用，不能挤占心跳与停止通道。
- 新StageEvents/Writes/Operations payload显式使用`device-semantics/1`，描述业务事实、来源与证据引用；当前Host的Store manifest目标为`s01-store/3`，保留原有/2通信证据结构，新增完成表可空FK的单项EF迁移。新测试库通过既有StorePrep建库路径创建；现有/1→/2维护入口保留，新016增加/2→/3必要受控升级入口：Host停止、目录互斥、备份、核对源schema、仅本次必要schema变更和manifest、读回核验。禁止Host启动自动迁移，禁止改旧JSON或补造raw。升级/建库本轮均不执行。
- 旧运行读取继续通过现有API业务查询；已知旧payload由Infrastructure只读历史投影读取，旧原始字段标`LegacyRecordedClaim`，确有原始包才可称CapturedRaw，无原包为RawUnavailable。没有版本就不推断协议版本；历史Provider原文保留并标记录性质，不“校正”旧来源。未知payload为Unavailable。
- 历史记录和当前运行准入分开，旧运行AllowedActions保持空，不能用旧完成记录解锁新盘。原`s01-store/1`归档不能被新Host悄悄转为可执行库；受控升级后旧行仍按历史规则读取。独立原归档保持只读。

数据验证由[验证合同](contracts/verification-gates.md)的V-PICK、V-DIAG、V-HISTORY和V-FAIL承担；本文件没有实现或数据库迁移执行记录。

<a id="61-本次schema升级的事务与中断语义r07"></a>

### 6.1 本次schema升级的事务与中断语义（R07；完整升级验收转出）

此前`s01-store/1 → s01-store/2`受控维护保留；新016承接/2→/3单项升级，不扩展通用迁移/灾备平台。真实manifest位于SQLite `Manifests`表（Station01EntityConfigurations:118）；当前StorePrep:24–30分别迁移和保存manifest，不具备下述原子旧库升级能力。以下全部待实现、待运行验证。

**源状态与互斥**：Host及会写同库/媒体的Worker等进程停止；维护进程在批准根内获取现有StoreAccessGuard独占`.station01.store.lock`，整个检查、备份、升级、核验期间不释放。核对源唯一Manifests行的StoreId/Profile=Test/SchemaVersion、准确旧EF迁移集合、所有既有表/列/类型/nullability/主外键/索引；旧历史固定集合为`202609210001_InitialStation01`、`202609220001_StageEventing`、`202609230001_Station01MainFlow`（以实际迁移文件及Probe复核；若不符拒绝，不能只比数量）。源不匹配/未知版本/活动写者直接拒绝。不用工具输出或版本字符串代替实际结构；持久事务模式必须支持回滚/崩溃恢复，拒绝journal_mode=OFF/MEMORY或synchronous=OFF等不能满足保证的配置，不在升级时暗改模式。

**备份及范围**：独占期间通过SQLite BackupDatabase取得同库完整一致备份，不只复制可能含WAL的主db文件。只读重新打开备份，核验StoreId、结构、迁移/manifest、完整性及所有旧业务表的稳定主键/行数/逐行payload原字节摘要；记录media-root中既有引用对应文件的路径、长度、摘要和缺失情况。此次仅新增表，媒体不迁移、不生成替代文件；数据库备份与媒体清单共同界定恢复范围。备份不可核验即不开始。核验报告/摘要可以是维护证据文件，**不是另一个控制Host开放的store manifest**。

**单一提交边界**：从本次唯一EF migration的UpOperations用现有IMigrationsSqlGenerator生成命令，仅允许新增PlcCommunicationEvidence表及指定索引；拒绝TransactionSuppressed、事务外PRAGMA/VACUUM、重建旧表或修改旧业务payload。使用同一SqliteConnection显式非deferred事务，全部DDL绑定该事务，再由EF history服务生成本次迁移记录插入，同事务条件更新唯一Manifests行的SchemaVersion（原版本、StoreId/Profile匹配且受影响行恰为1）。StoreId/Profile和既有准备身份不变；只有最后一次Commit，没有单独manifest SaveChanges或中途提交。执行前逐命令确认可在该事务内执行；不以“EF会回滚”代替此依据。新空库准备需创建同目标结构/manifest并通过同一目标Probe；未完成的新根不能被Host接入。

| 中断状态 | 可观察数据库事实 | 拒绝、核查及再次执行条件 |
| --- | --- | --- |
| U0 完整源态 | 旧结构、准确旧迁移集合、同StoreId旧manifest全部一致 | 新Host拒绝读写；已确认原事务结束，重新核验源及备份后才可再次执行唯一升级。一次查无新表不等于已确认回滚。 |
| U1 提交结果未知 | 维护进程在事务/commit回执期间中断，实际结果尚未复核 | 保持停机，由持锁维护进程让SQLite自身恢复；不删除journal/WAL，不据异常推断回滚。重开并核验后只能归U0、U2或UX；未归类不开放、不重跑DDL。 |
| U2 完整目标态 | 新表/列/索引、精确旧集合＋本次迁移、同StoreId/Profile的v2 manifest一致，即使原工具输出丢失 | 不重复DDL；重新只读检查结构、integrity_check/foreign_key_check、旧业务行与payload摘要、媒体清单/引用不变；全部符合才结束维护并允许Host。 |
| UX 不一致/不可读 | 结构、迁移、manifest任一不一致，身份不明、损坏或旧业务摘要改变 | 拒绝Host读写及自动重做，保留现场；仅在独占维护中用已核验同StoreId完整备份恢复数据库，媒体不变条件复核后归U0。无可信备份/媒体失配则保持阻塞，不修补版本号蒙混。 |

Host的StoreCompatibilityProbe必须按上述完整目标结构、精确迁移集合、唯一身份和manifest共同准入；当前“6个表名＋3个迁移计数”检查不足。U2维护核验成功、维护锁释放后，Host重新获取同一锁并再次目标兼容检查才可开业务读写。业务旧JSON、来源和历史schema一律不改；历史语义读取按§6/EC E04。缺升级/核验结果不得声称数据库可用。有限升级失败验证列为V-HISTORY/U0—UX，不能把未运行文档设计称为升级成功。


### 联合闭合实施字段约束（2026-10-01）

必要业务提交回执增加明确RecordKind（RunWrite/StageEvent），与实际WriteId或EventId、Sequence/Revision、CommittedUtc对应；引用类型不能由非空ID猜测。独立绑定对旧已终态运行使用既有StageEvent业务保存，不修改Run.State/Revision/TerminalRevision或旧handoff，不改本次schema范围；保存分类RecipeApplication不新增工艺动作。连续链仍用原RunWrite。历史读者必须按实际记录类型核查，旧缺信息返回未记录。该技术细化承接B03.2已有独立入口必须真实保存的要求，实施/验证见tasks联合闭合续记，未宣称历史/API已完成。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。
