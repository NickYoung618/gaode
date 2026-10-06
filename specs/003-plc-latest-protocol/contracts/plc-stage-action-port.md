# PLC Stage Action Port 合同

2026-09-26夜间008 T057必要接入：正式Host的LatestProtocolStageActionAdapter通过既有IStageEventStore持久化实际PickCompleted及关联的取料目标采样、源/目标、entity/reservationRef（采用已有operationId）、plan/epoch；该回执必须Committed后才继续写放料槽号/XYZ/命令2。保留单次IPlcStageActionPort取放接口，新增的是适配器内部持久回调，不增加PLC地址、外部API或第二套设备状态机。保存未确认时报告UnknownHeld并保留取料在途/占用；完成事件仍在状态3/ACK清零后由008保存。组件端口测试可注入明确回调复现保存门禁，正式Host必须接真实StageEventStore。本增量尚待实施/验收。

## 边界与实现

`IPlcStageActionPort` 只接受 `PlcWorkflowStage.Sorting|UnloadPreparation|UnlockObservation`。Detection 只调用 `IDetectionPort`；001 启动夹紧与 3D/F 运动继续走现有正式 PLC/Motion 端口。

真实 PLC 与独立 VirtualPlc 都经 `LatestProtocolPlcDevice`/薄适配器使用同一 Modbus TCP、地址、完成判据、连接 epoch、安全准入和单动作在途控制；不得另建 PLC 客户端或旁路运动所有者。

## 请求与反馈

请求包含 WorkflowIdentity、stage、已提交 `OperationId`、plan revision、动作参数摘要、epoch、deadline 和幂等键，且只由已持久化动作意图构造。反馈为 Accepted、Executing、Completed、Failed、TimedOut、Disconnected 或 UnknownHeld，并保存 operation、动作序号、epoch、点位快照、source/quality 与 errorCode。

只有当前 operation/epoch 且满足正式协议完成判据的反馈能生成 Completed。旧状态不得通过绑定新 ID 冒充新动作完成。

## 协议映射

- Sorting：同盘分拣：OK留原槽不搬，NG/Pending各去对应区配置目标，姿态异常跳过后续检测，最后从原槽实际分拣到Pending；源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升。真实取料反馈和在途保存先于放料；物理槽号不是Sequence，预留/在途持续到可靠完成。新状态1/2/3只由通信翻译为取料成功/放料完成/失败，旧状态2/3及ACK不再适用。
下料使用本次用途的合法配置及可靠轴反馈。普通盘末Detection→适用Sorting及必要保存→UnloadPreparation→允许取盘/人工取盘→Final；到位、允许取盘、人工确认和保存是独立事实。不能伪造新协议未定义的旧锁紧/解锁信号，地址/恢复/安全延期不猜值。
- UnlockObservation：重新查询并核验 `WholeTrayCompletionReference` 后写 `Pallet_Lock_Cmd=4x0023` 值 0，读取 `Pallet_Lock_Status=4x0024`；只有 0 完成。

启动阶段不得通过该端口写 `Pallet_Lock_Cmd=1`。人工移除确认不是 PLC 点位；VirtualPlc 不能生成它。

## 未知、超时与恢复

阶段开始时冻结 `StageDeadlineAt = StageStartedAt + 冻结阶段预算`。只有能够证明发生在物理动作派发前、且合同标记可恢复的临时通信错误，才允许首次之外最多重试 3 次（总计最多 4 次）并按 1/2/4 秒退避；尝试、退避、重连和重启恢复共用该期限且不得重置。

只要动作可能已派发而结果不可信，包括写后断联、读回超时、锁仍为 1 或 epoch 变化，就不进入通信重试并立即进入对应阶段 `UnknownHeld`，保持设备占用且禁止自动重发。每次尝试和处置保存 attempt、plannedAt、deadline、operationId、epoch、error 和 evidence reference。

恢复须通过正式状态读回或人工核对追加新事实。已完成动作不重放，未证实动作不重发。人工核对保存 actor、时间、阶段、结果和原因，但不能直接制造完成。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

Sorting携实体、真实physicalSlotIndex、sourcePoint、targetRegion/cell/point、预留及结果引用；Sequence不能作ProtocolPartIndex。OPEN-06/24尚未明确的整体或分段取放不能因Sort_Status=2冒认真实源目标完成。普通OK无搬运时留原位，特殊OK须实际回原槽，NG/Pending按已确认目标处理；类型1旋转依B03。

Detection定位沿同一Motion/设备端口，不另建PLC客户端。按新版抓取Z/XYZ、命令4及解锁合同；与F扫码Z复位的轴归属转换须按本次命令及可靠反馈核对，不能把扫码轴反馈当产品/抓取轴事实。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

## 2026-09-26协议身份及适配范围

逐对象按配置取件点定位→PLC按产品型号/目标面翻转→配置放回点定位→放回；相关对象放回后统一3D姿态复查，正常继续、异常跳过后续检测、最后原槽Pending分拣并返回物理槽号，F不重绑。料盘不翻面，整体不按部位重复动作，不沿用旧Flip_OK或独立实际面号前置。

同盘分拣：OK留原槽不搬，NG/Pending各去对应区配置目标，姿态异常跳过后续检测，最后从原槽实际分拣到Pending；源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升。真实取料反馈和在途保存先于放料；物理槽号不是Sequence，预留/在途持续到可靠完成。新状态1/2/3只由通信翻译为取料成功/放料完成/失败，旧状态2/3及ACK不再适用。

当前通信依据为20261001 Word与信号表，哈希及延期差异见011 spec；旧协议身份/地址/ACK仅属历史原件和旧运行，实施须迁移映射、适配器、VirtualPlc与通信断言，业务不感知原码。

## USR-E完整坐标与动作观测

依据USR-20260926-E、协议§2.2/3.1.3/3.1.5—7及宪章7.0.0；本节是目标设计，问题1—5根因仍待运行核验。地址均为4x十六进制文档地址，Float32占双寄存器；PDU偏移/字序由现有映射转换。PC写目标/命令，PLC写实际反馈，禁止反向写入。各行复用唯一运动准入、原deadline、1秒I/O及3秒心跳。

| 动作/来源 | 完整目标及Z轴；受理入口 | 本次反馈与校验 | 清零、保存及后继门禁 |
| --- | --- | --- | --- |
| F/E扫码 | F XY来自首次3D，E配置点，使用扫码Z | 独立轴实际/到位与本动作关联 | 实际采集/码或问题保存后才继续；F唯一绑定门保持；通信内部握手不外露 |
| 公共3D与检测 | 3D采集使用公共配置；检测XYZ来自配方/点位，各相机独立目标 | 各适用轴可靠实际/到位 | 单图分析及保存后有效完成；同面融合不阻塞未采第二输入 |
| 逐实体翻转放回 | 独立取件点→型号/目标面翻转→独立放回点→放回 | 翻转及放回分别可靠完成 | 相关对象放回后统一姿态复查，异常退出，F不重绑 |
| 分拣后下料 | 本次合法下料目标 | 独立轴可靠实际/到位 | 所有适用分拣及必要保存先成立；到位、取盘确认与Final分别保存 |
| 同盘取料 | 真实物理槽、源XY及抓取Z | 可靠取料反馈/当前位置 | 在途保存成功才可放料；序号不是槽号，未知不重发 |
| 同盘放料 | 本盘NG/Pending目标XY及抓取Z | 可靠放料及再抬升 | 完成与占用保存；OK/姿态异常无分拣搬运 |

取放采样边界：§2.2明确抓取后安全抬升由PLC设定，协议没有定义状态2/3时实时Z必须仍等于目标Z。Host须收集本动作内实际到目标的XYZ观察（本次发令后的采样、相同连接代次、正确轴、冻结容差和来源），与后续2/3状态分别判定；不能以旧下料坐标、目标请求或同值旧状态拼成到位。写成功只记传输，设备受理依据其新动作状态（诊断用于辅助采证），不能新增Accepted寄存器或坐标匹配位。

Test设计：VirtualPlc在受理后执行自己的目标到位→取放完成阶段，更新模拟实际寄存器并记录阶段采样；使用有版本的虚拟动作时序，不编造生产安全高度。Host在既有轮询内读取Modbus实际XYZ，保存匹配到位观察再等完成。若到位阶段未被可靠采到，受限而不把完成时安全Z强制等同目标。模拟阶段时间只作Test配置，不能放宽Host既定期限或伪装生产行为；PLC诊断可辅助证明阶段，不能取代Host业务完成反馈。生产目标阶段的可观察窗口/来源未由现有协议明确，实机启用此取放校验前须核实；该限制不影响无需搬运的普通OK或已有定义的检测/翻面/下料。

当前代码静态事实：LatestProtocolPlcDevice.AdvanceMove已有XY批写和适用Z、反馈校验；LatestProtocolStageActionAdapter.WritePositionAsync已有X/Y/GrabZ写入，下料有XYZ读回，WaitSortingAsync先等2才提交放料。但WaitSortingStatusAsync只读状态，VirtualPlcEngine的Sort成功分支只更新状态2/3和取料标记，缺本动作实际XYZ更新/校验。后续在原消费者补齐，不将这些静态发现当作用户运行根因已证明。其他动作优先补可观测性并定向核验，有实际证据再修具体缺漏。

普通OK不发取放，保存无需搬运依据；特殊出口已处置实体不重复分拣。故障进入USR-D完整新轮，未知不重发；RST-01/RST-02、正常暂停及人工继续边界不变。事件字段与两端取证见[VirtualPlc诊断合同](virtual-plc-boundary.md#usr-e动作级诊断增量)。
2026-09-26夜间预算接线：适配器从现有冻结BusinessBudget消费XyCompletion与PlcAcceptance，取料和放料各有独立机械等待上限，ACK等待用受理上限，各自同时受原阶段绝对deadline约束；不重置整段deadline、不增加I/O或心跳上限。取料后关键保存使用原CriticalSave；新阶段预算须计两段机械、ACK、明确I/O写入、必要提交/状态读取及既有受理前有限重试，不把较大整段预算当单动作超时。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

可靠目标采样与抬升后完成位置分开，不新增设备信号或强求完成时Z等于目标。通信先经正式StartAsync定义校验及同实例动作准入；坏映射不允许初始化/动作写入。线缆条款仍有效但仅通信侧解释，业务StageAction结果删除ProtocolStatus/内部PositionEvidence.Phase。
