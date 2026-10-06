2026-09-26 PARAM事实增量：按共享API合同保存实际CaptureFact/RequestedCaptureSettings并从现有媒体查询投影；008 T054、003 T068承接，原编号及完整条件不变。

2026-09-26夜间008直接依赖细化：T071同盘取放消费008固定目标预留，真实取料反馈及必要在途保存成立后才写放料数据；保存未确认不得继续放料或释放占用。内部持久接入见plc-stage-action-port合同，不新增生产协议或页面范围。正常暂停由001/008安全边界消费，同run核验继续；故障continue拒绝及完整新run规则不变。

2026-09-26 USR-20260926-D恢复设计：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替代旧U05同轮单指令重发。双端复位、真实初始状态成立后显式新启动；旧运行和证据保留。设计尚未实施，tasks保持只读，原编号和勾选不变。

2026-09-26旋转Test增量：T071按[虚拟旋转请求/结果合同](contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。

2026-09-26 人工子范围增量：T072按[人工执行合同](contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

# 003 最新 PLC 协议接入

008 T061直接增量依据U06及[E Test执行合同](contracts/e-test-execution.md)：E复用F式扫码Z及3/4复位，采用独立E采集/EDecode和对象问题记录；不新增PLC信号、不放宽F门禁，机械/保存失败仍阻断。

权威来源为 2026-09-25分区版 最新版 Word 协议及用户本次要求，覆盖 001/002 中冲突定义。保持 gaode-1 分层、Coordinator、持久化意图和唯一运动控制。VirtualPlc 独立进程，真实/虚拟共用通信实现；相机、光源、算法当前模拟。

## Clarifications

### Session 2026-09-22

- Q: 第一工位启动阶段由谁执行料盘夹紧，以及上位机应如何与夹紧状态交互？ → A: 采用方案 B：人工放盘并满足实体启动条件后由 PLC 内部执行夹紧；上位机只读取 `Pallet_Lock_Status`，不发送 `Pallet_Lock_Cmd=1`，不等待协议未定义的独立夹紧命令应答；流程完成时上位机写 `Pallet_Lock_Cmd=0` 解锁。
- Q: `Pallet_Lock_Cmd=0` 应在第一工位的哪个阶段执行？ → A: 只有整盘检测、分拣和下料准备（移动到下料位）全部完成后，才允许上位机写 `Pallet_Lock_Cmd=0` 释放料盘供人工取盘。第一工位准备完成、3D 检测完成或向后续工位移交，均不代表整盘流程完成，不得在这些阶段提前解锁。
- Q: 没有独立的实体启动反馈信号时，如何确认夹紧完成？ → A: 不新增协议外信号或虚拟的夹紧启动应答；发送 `PC_Start_Cmd` 后，只能以 PLC 返回 `Pallet_Lock_Status=1` 作为夹紧完成事实并允许流程继续。不能根据上位机输出、时间经过或自定义 `ClampStarted` 等状态推断夹紧完成。
- Q: 写入 `Pallet_Lock_Cmd=0` 后何时才算解锁完成？ → A: 该写入只能发生在整盘检测、分拣和下料准备全部完成后；写入受理不等于物理解锁完成，必须随后读取 `Pallet_Lock_Status=0` 才能记录为已解锁并进入人工取盘确认。写入失败、断联、观察超时或状态仍为 `1` 时保持设备占用并进入人工处置。
- Q: 001 中的“等待实体启动”和“等待夹紧”是否需要新增独立按钮反馈信号才能保留？ → A: 不需要。003 的 PLC 点位和时序是权威来源；保留两个业务状态作为可查询的流程阶段，但不把它们解释为独立按钮事实，夹紧完成只能由本次连接代次下读取到的 `Pallet_Lock_Status=1` 证明。
- Q: FullSimulation 如何产生合法的 `Pallet_Lock_Status=1`？ → A: 由 `SimulatedPlc` 在 `PC_Start_Cmd` 受理后按模拟配置内部产生 `0→1`（也可配置为 `2` 或在观察期限内保持 `0`）；Host 不写 `Pallet_Lock_Cmd=1`、不注入 `PhysicalStart`/`ClampStarted`，所有结果必须标记为 Test/Simulated，不能作为真实 PLC 验证。
- Q: 整盘检测、分拣、下料三个阶段由哪个模块负责发起、推进和判定完成？ → A: 由 Host 编排器统一执行；RecipeRunPlanner 只提供冻结的步骤计划，不能直接充当完成事实来源，PLC 只提供点位动作和状态事实。
- Q: 检测、分拣和下料的真实动作分别通过什么端口执行？ → A: 检测调用检测端口，分拣和下料调用 PLC 动作端口；每个端口必须返回受理、执行、完成或失败事实，Host 编排器负责顺序和业务状态。
- Q: 三个阶段需要保存哪些开始、完成、失败、超时、断联和恢复证据？ → A: 每个阶段保存独立、追加式事件链，至少记录开始、受理、执行、完成或失败，并记录超时、断联、连接代次、恢复核对和人工处置结果；事件必须持久化并可按运行和阶段查询。
- Q: 三个阶段全部完成后，如何生成完整托盘完成证据并交给解锁流程？ → A: Host 只有在检测、分拣和下料准备的完成事件全部持久化后，才能生成表示“可安全解锁”的不可变 `WholeTrayCompletion` 并把引用传给解锁流程；解锁流程仍必须写 0 并读回 `Pallet_Lock_Status=0`。人工取盘确认后再追加不可变 `FinalUnloadCompletion`，不反向作为解锁前置条件。
- Q: 三个阶段失败或断联后如何恢复，虚拟相机、虚拟算法和 VirtualPlc 分别负责什么？ → A: 临时通信失败和算法超时允许有限次退避重试；算法在有限重试后仍失败、超时、未配置、未接入或无有效判定时，须为受影响工件形成带原始异常证据的 `Pending` 检测结果，并通过冻结的 `RecipeRunPlan` 和正式 PLC 动作端口分拣到 Pending 区，其他工件继续；不得转为 OK、使用默认结果或跳过分拣阶段。PLC 动作完成未知必须进入 `UnknownHeld` 并保持锁紧。恢复只能从最后一个已确认完成阶段继续，人工核对须记录操作员、时间、阶段、结果和原因。虚拟相机与虚拟算法只提供 `DetectionPort` 结果；VirtualPlc 只模拟 PLC 点位、动作完成、失败和断联。所有模拟/降级结果必须标记为模拟或降级，不能作为真实设备验收证据，异常不得绕过分拣、下料或解锁条件。
- Q: 分拣动作应采用哪套 PLC 合同？ → A: 采用最新版协议现有点位 `Sorting_Part_Index=4x0020`、`Sorting_Cmd=4x0021`、`Sorting_Exec_Status=4x0022`；完成必须匹配当前 `operationId`、连接 `epoch` 和协议完成状态，错误码沿用最新版协议定义。
- Q: 下料动作应采用哪套 PLC 合同？ → A: 不新增独立下料点位。下料准备使用 `XY_Move_Cmd=4x0001` 值 `4` 和 `XY_Pos_Confirmed=4x0002` 值 `1`；解锁使用 `Pallet_Lock_Cmd=4x0023` 值 `0`，并读回 `Pallet_Lock_Status=4x0024` 值 `0`；人工取盘确认由 Host 事件记录。动作关联使用 Host/适配器的 `operationId` 和 `connectionEpoch`，不要求 PLC 回传协议外标识。
- Q: 检测结果如何映射为分拣计划？ → A: Host 根据冻结的 `RecipeRunPlan`，使用 DetectionPort 返回的稳定对象标识、位置和分类映射为有序分拣动作；缺少匹配、重复匹配或结果歧义时记录 `MappingFailed`，暂停当前托盘并进入人工核对，不跳过对象或默认放行。
- Q: 各类错误的最大重试次数、退避策略和阶段总超时应如何固定？ → A: 临时通信错误在首次调用之外最多重试 3 次（总计最多 4 次尝试），退避 1/2/4 秒；Detection 算法超时在首次调用之外最多重试 2 次（总计最多 3 次尝试），退避 2/5 秒；单阶段总期限 按冻结RecipeExecutionBudget确定的且不得重置；可能已经派发的 PLC 物理动作永不自动重发，必须进入 `UnknownHeld`。
- Q: 阶段事件、WholeTrayCompletion、重启恢复和保留期限应采用哪种一致性合同？ → A: 动作意图及对应投影先用一个短事务提交，提交后才调用设备或算法；反馈事件及对应阶段投影再用一个短事务原子提交。Detection、UnloadPreparation、Sorting 的 Completed 事件均已提交后，使用独立聚合短事务重新核验并原子写入 `WholeTrayCompleted`、`WholeTrayCompletion` 和整托投影；人工确认与 `FinalUnloadCompletion` 使用同一个后续短事务。不得跨 PLC、相机或算法调用持有事务。重启只重放已提交事件，可能已派发且无终态的 PLC 动作进入 `UnknownHeld`；阶段事件和完成证据保留 7 年，期限可延长但不能缩短。 （历史答复中的旧顺序/面组/不重扫条款已替代：当前四面3CD＋1AB；完成翻转放回后统一3D复查且F不重绑；检测、适用分拣、下料依次执行。原历史答复不证明新版本通过。）
- Q: 下料阶段是否拆分为移动到下料位、解锁读回和人工取盘确认三个子阶段？ → A: 是。Host 依次记录下料准备（`XY_Move_Cmd=4` 且 `XY_Pos_Confirmed=1`）、解锁（写 `Pallet_Lock_Cmd=0` 且读回 `Pallet_Lock_Status=0`）和人工取盘确认；三者不能合并为单一布尔完成事实。
- Q: `WholeTrayCompletion` 是否表示人工取盘后的最终完成？ → A: 不表示。它表示检测、分拣和下料准备完成且可以进入解锁流程；解锁读回成功后，人工取盘确认再生成独立的 `FinalUnloadCompletion`，两者均为不可变记录。
- Q: 总时序图第 135 步“确认下料完成”的输入来源是什么？ → A: 由 Host 受控 API 接收操作员确认，不新增 PLC 点位或传感器点位；本期软件验收由测试/联调客户端显式调用，当前客户原型不新增控件。确认必须持久化操作员、时间、确认阶段、结果和原因，并生成 `FinalUnloadCompletion`。
- Q: T046 是否应从寻找独立下料 PLC 命令改为确认现有协议边界？ → A: 是。T046 只确认移动到下料位、解锁读回和 Host 人工取盘确认的正式边界，记录协议版本和章节引用；不得新增独立命令、反馈点位或协议外错误码。
- Q: VirtualPlc 在下料边界中应模拟哪些行为？ → A: 就下料子阶段而言，模拟正式协议已有的下料位移动、解锁命令及 `Pallet_Lock_Status` 读回；这不限制 VirtualPlc 整体还可通过正式 Modbus TCP 提供第一工位所需的心跳、夹紧、运动和分拣事实。人工取盘确认由 Host 测试入口产生并持久化，不模拟为 PLC 点位或真实设备验收事实。
- Q: `PlcWorkflowStage`、完整整盘流程阶段和阶段事件应如何分层，检测阶段是否允许调用 PLC 动作端口？ → A: `PlcWorkflowStage` 只表示 PLC 动作阶段，固定为 `Sorting`、`UnloadPreparation`、`UnlockObservation`；新增通用 `WholeTrayWorkflowStage` 表示完整业务阶段，固定顺序为 `Detection → UnloadPreparation → Sorting → UnlockObservation → ManualTrayRemovalConfirmation`。`StageEvent`、`StageProjection`、`RetryPolicy` 和 `RecoveryPolicy` 使用 `WholeTrayWorkflowStage`，PLC Action 请求仍使用 `PlcWorkflowStage`。`Detection` 只调用 `IDetectionPort`，不得调用 `IPlcStageActionPort`。检测结果到分拣计划的缺失、重复或歧义追加 `MappingFailed`，归属 `Detection` 阶段并暂停当前托盘进入人工核对；`UnknownHeld` 归属产生未知物理结果的 PLC 动作阶段，分拣、下料准备和解锁分别归属 `Sorting`、`UnloadPreparation`、`UnlockObservation`，保持设备占用且禁止自动重发；人工核对事件归属触发它的阶段，`ManualTrayRemovalConfirmation` 只在 `ObservedUnlocked` 后由 Host 接收，不能由 PLC 端口产生。 （历史答复中的旧顺序/面组/不重扫条款已替代：当前四面3CD＋1AB；完成翻转放回后统一3D复查且F不重绑；检测、适用分拣、下料依次执行。原历史答复不证明新版本通过。）

### Session 2026-09-23

- Q: 当真实算法不可用，或某个工件的算法在有限重试后仍失败时，本期第一工位应如何处理该工件并继续整盘流程？ → A: 将受影响工件明确记为 `Pending`，保留原失败、超时、未配置、未接入或无有效判定证据，并通过正式 `RecipeRunPlan` 和 PLC 动作端口分拣到 Pending 区；其他工件继续，不得转为 OK、使用默认结果或跳过分拣阶段。
- Q: 在客户确认原型没有“人工取盘确认”控件且不得修改原型的前提下，本期应通过什么入口产生 `ManualTrayRemovalConfirmation`？ → A: Host 提供受控、记录操作员身份的人工确认 API，软件验收由测试/联调客户端在 `ObservedUnlocked` 后显式调用；当前客户原型保持不变，生产页面入口等待新原型版本、哈希和明确批准。
- Q: 本期第一工位完整主流程的最小端到端验收，应采用哪种运行组合？ → A: 启动真实 Host 进程和仓库现有的独立 VirtualPlc 进程，通过正式 Modbus TCP、正式业务端口和真实 SQLite 持久化运行完整流程；相机、光源和算法允许使用明确标记的 Simulated 适配器，人工确认由测试/联调客户端调用 Host API。
- Q: 从人工放盘到 `FinalUnloadCompletion` 的本期完整主流程，是否必须在同一个运行身份中依次包含 001 的启动夹紧、3D 高度、F 扫码和配方绑定，再自动进入 003 的整盘检测与分拣？ → A: 必须使用同一 `runId/trayId` 和冻结配置连续执行 001 公共准备与 003 整盘闭环；3D、F 扫码、配方计划/绑定和持久化移交完成后由 Host 自动进入 Detection，不再次人工启动，也不允许测试代码直接跳到 003。
- Q: 本期独立 Host + VirtualPlc 端到端验收最少必须覆盖哪组场景，才能判定第一工位完整主流程可交付？ → A: 覆盖一条正常完整闭环，以及算法失败转 Pending 后继续、完成证据前拒绝解锁/人工确认、PLC 动作断联或连接代次变化进入 `UnknownHeld` 且不自动重发、Host重启后只重建持久故障查询，双端复位/初始状态后新轮完整执行并关联旧轮四类关键场景。
- Q: 本次一致性修复后哪些章节是当前规范性来源？ → A: “第一工位本期范围”、FR01–FR13、“第一工位端到端验收标准”和“Virtual/Simulated 实现边界”是当前规范性来源；Clarifications 与旧 T045 段落只保留历史追溯，并已同步为不与规范性章节冲突的表述。

### Session 2026-09-24

以下为“2026-09-24用户确认的项目约定”，不是 2026-09-21 Word 协议原文，也不是已完成的真机验证事实；与此前澄清的历史表述不一致时，以本节及下列现行 FR 为后续对齐依据。

- Q: 下料目标能否沿用上一次运动留在 PLC 寄存器中的坐标？ → A: 不能。下料必须有独立、可追溯的 XYZ 目标配置和下发能力；每个运动命令先下发本次适用的 XYZ 目标，再发送运动命令。下料目标不得取历史寄存器值；其他命令既有合法点位映射保持不变。Test/VirtualPlc 与真机参数分开，具体下料数值、单位、坐标系及用途须由获批准的配置确定，不编造生产坐标。
- Q: 命令4当前依据是什么？ → A: 新版下料按§3.1.6：写冻结目标Camera_Target_X/Y及Grab_Target_Z(0003/0005/000B)，再命令4；核验本次到位和实际XYZ后清命令。前次动作必要复位及安全条件保持，不再采用写检测Z或命令4只动XY约定。普通盘末Detection→UnloadPreparation→适用Sorting→WholeTrayCompletion→ObservedUnlocked→页面取盘确认→Final；到位不等于可取盘，适用分拣/必要保存/无未知在途门禁必须成立。 2026-09-24旧约定与T064证据仅保留历史适用范围。
- Q: 如何证明本轮下料已执行并完成？ → A: 以本次命令 4、本次下发目标、连接代次及命令后的实际设备反馈形成可追溯关联，同时保持既有安全门禁；不得把旧的 `XY_Pos_Confirmed=1`、写入回执或页面刷新单独视为本轮完成，也不得把采到短暂的 `XY_Pos_Confirmed=0` 当作唯一前提。若目标与原位置相同且现有反馈无法区分新旧动作，应保持结果未知、占用和人工核对门禁，不自动重发或新增未经确认的 PLC 点位。

## 第一工位本期范围

- 本节、FR01–FR18、“第一工位端到端验收标准”和“Virtual/Simulated 实现边界”构成本 feature 的当前规范性要求；Clarifications、旧 T045 任务说明和兼容解释仅用于历史追溯，不得覆盖这些规范性章节。2026-09-24 用户项目约定的现行要求见 FR16–FR18；原协议原文与未完成的真机验证事实须分别标注。
- 本期软件主流程从人工放盘后的 Host 启动请求开始：Host 完成就绪检查并置 `PC_System_Ready`，操作员确认后发送 `PC_Start_Cmd`，PLC 内部夹紧，Host 在当前连接代次读到 `Pallet_Lock_Status=1` 后才允许继续。
- 同一 `runId/trayId`、配置快照、连接代次关联和证据链必须连续覆盖 001 公共准备的 3D 高度、F 单次扫码、配方计划/绑定及持久化 handoff，再由唯一 Host 编排器自动进入 003 的 `Detection → Sorting → UnloadPreparation → WholeTrayCompletion → UnlockObservation → ManualTrayRemovalConfirmation → FinalUnloadCompletion`。001→003 之间不得要求第二次人工启动，不得以测试代码、预造完成状态或临时冻结计划跳过公共准备与移交。
- 001 handoff 只有在必要动作、媒体/结果和配置/配方引用均按合同持久化，且运行、料盘和配置身份与后续 `DetectionRequest` 一致时，才允许触发 003；handoff 受理、入队或内存状态不等于可执行移交。
- 所有设备动作均经过正式端口、统一 Motion/Safety 准入和已提交意图；RecipeRunPlanner 只产生绑定到当前运行快照的冻结计划，不能产生检测、分拣或完成事实。
- 本期完整主流程终点是同一运行/料盘的人工取盘确认和不可变 `FinalUnloadCompletion` 均持久化提交；命令受理、`WholeTrayCompletion`、解锁命令写入或 `ObservedUnlocked` 单独出现均不构成最终成功。
- 最小端到端验收必须真实启动 Host 和仓库既有独立 VirtualPlc 两个进程，经正式 Modbus TCP 连接，使用正式 Application 端口和同一业务状态机，并实际提交 SQLite 阶段事件、投影、动作意图、完成引用、人工确认及最终完成记录。
- 相机、光源和算法在本期可由明确标记 `Simulated/Test` 的适配器实现，但必须使用正式接口、相同请求/响应、有限重试、Pending、映射、状态转换和持久化流程；不得由 Workflow 硬编码成功、直接修改前端状态、跳过步骤或把模拟结果写成 Real/Production 证据。
- 测试/联调客户端只通过 Host API 发起启动、查询状态和提交人工取盘确认，不直接控制 VirtualPlc、相机、算法进程或写 SQLite。
- 每个动作意图及对应投影必须先在一个短事务中提交，提交后才调用 PLC、相机或算法；每个反馈事件及对应阶段投影使用另一个短事务原子提交。三个物理阶段不得跨阶段共用一个事务，也不得在任何数据库事务中等待设备或算法。
- 旧任务重新检测或报废只能由受控人工决策产生；Host 重启或恢复不得自动重检、自动报废或自动重放已完成动作，人工决定必须保存 actor、时间、原因、原任务和证据引用。

### 007虚拟集成对共享边界的适用

003既有后端与五场景证据按原运行组合保留；007增加的是本次正式接线与完整前端联调，不能把既有局部通过改写成007通过。007的固定图片必须在单一冻结目录经正式采集入口逐次读取并保存独立媒体；公共3D/F和冻结计划中的每项Detection采集都须使用本次媒体。每项Detection采集对应经正式算法端口向独立虚拟worker实际发送的一次请求及结果，保留同一runId/trayId/planRevision、对象/步骤、媒体、算法call/attempt、PLC状态和SQLite提交引用；不得批量返回预制OK、跳过必检步骤或以旧媒体代替本次事实。CAP/P01候选预期Detection采集/算法各2次，实际以正式计划与运行记录为准；每次采集3–5秒、每次算法10秒，使用新版本化Test预算核对120秒阶段期限，不缩短延迟制造通过。VirtualPlc与Host仍经正式Modbus交互；007的006页面实现归006，Host侧限定Test来源的API/通知跨源及授权接线归007，双方实际连通只作为007完整前端验收前置，不追溯改变003历史验收范围。

### 阶段枚举与事件边界

- `PlcWorkflowStage` 仅用于 PLC 动作端口和协议动作关联：`Sorting`、`UnloadPreparation`、`UnlockObservation`。它不表示检测或人工取盘业务阶段。
- `WholeTrayWorkflowStage` 用于整盘业务流程、阶段事件、状态投影、重试策略和恢复策略：`Detection`、`Sorting`、`UnloadPreparation`、`UnlockObservation`、`ManualTrayRemovalConfirmation`。
- 阶段顺序固定为 `Detection → Sorting → UnloadPreparation → UnlockObservation → ManualTrayRemovalConfirmation`。`WholeTrayCompletion` 位于前三个阶段完成之后、`UnlockObservation` 之前，是后续解锁门控证据，不新增为 PLC 动作阶段。
- `Detection` 通过 `IDetectionPort` 产生检测事实和 `MappingFailed`；未形成完整映射前不得调用 `IPlcStageActionPort`。`MappingFailed` 使 `Detection` 投影进入人工核对暂停状态。
- 算法失败、超时、未配置、未接入或无有效判定与对象身份/位置映射失败必须区分：前者在有限重试后形成该对象的 `Pending` 检测结果并继续完整映射，后者因无法安全确定分拣对象或目标而追加 `MappingFailed` 并暂停。`Pending` 不是 OK，也不允许省略对应分拣动作。
- `UnknownHeld` 只用于 PLC 物理动作结果未知、断联、读回超时或 `connectionEpoch` 变化，并沿用发生未知动作的 `WholeTrayWorkflowStage`；它保持设备占用并禁止自动重发。检测端口的算法超时或通信断联按 `Detection` 阶段有限重试并收敛为带异常证据的 Pending，不伪装成 PLC `UnknownHeld`。
- 人工核对属于产生异常的业务阶段；它只能确认新的事实或授权恢复，不能将未完成阶段改写为完成。人工取盘确认属于 `ManualTrayRemovalConfirmation`，只能在 `ObservedUnlocked` 后由 Host 接收。
- `ManualTrayRemovalConfirmation` 通过 Host 唯一路由 `POST /api/v1/station01/runs/{runId}/manual-removal-confirmations` 接收。请求体只有 `requestId`、`expectedRevision`、`reason`；Host从认证上下文取得actor/role，并从同run已提交事实取得tray、WholeTrayCompletion与ObservedUnlocked引用，服务端记录时间和结果。API受理不等于最终完成，只有确认事件、FinalSourceMatrix和`FinalUnloadCompletion`原子持久化后才算完成。007自动模拟取盘由启动时启用的受控Test联调客户端在解锁事实提交后调用，ManualActor来源为Test/Simulated而非AuthenticatedHuman或真实人工；客户确认原型不新增控件。

FR01 全部点位/方向/类型与本次用户确认的分区协议原件一致，Float32 两寄存器且字序可配。扫码、检测各拍照位、下料、翻面和取放按协议下发完整目标XYZ并读取本次实际XYZ核验；各动作的Z轴按现行协议归属，不混用。
FR02 人工上料确认后按新有效接口执行公共准备，设备反馈与人工确认分开，不伪造旧夹紧/区域ACK/解锁信号。依赖动作必须有可靠反馈及必要保存；适用分拣完成后才下料，取盘确认与Final各自真实保存。新恢复/安全控制保持延期，不能反向猜信号。

FR02 等待使用已冻结且适用的有限预算，从相应实际受理/阶段事实计时，截止/取消/错关联不得续派；原5000ms夹紧样例只属旧Test配置，不设新生产默认，保存/设备未知保持真实记录。

FR03 心跳持续翻转/响应；连续 3 秒无有效翻转时必须锁定动作、公开受限/报警状态并禁止自动续跑，重连与复位也不得自行恢复执行。验证证据必须包含心跳时间序列、3 秒期限、锁动作/报警状态和重连后仍未自动续跑的状态记录。
FR04 公共3D观察有无/姿态/F XY，产品检测XYZ取配置，F/E使用扫码Z；各轴独立目标、实际与到位。可靠反馈、实际采集/算法及保存先于后继；原码与内部握手仅通信处理，不将旧Inspection/Move/ACK值包装为业务DTO。
FR05 每次第一工位 3D/F 均必须执行正式握手；F 失败必须锁停且不得加载或绑定配方，只有本次运行取得唯一 F 码成功事实后才允许产品配方加载/绑定。验证证据必须同时覆盖失败锁停、重复/歧义码拒绝和唯一成功后的 plan/bind 顺序。
FR06 配方、设备适配及点位可配置；共同校验/执行不按产品名、测试编号或路径分支。F料盘编号、配方身份、PLC型号分开。支持更多检测面及四面后独立额外E；仅AB/CD，四面3CD＋1AB，参数由配方表达。
FR07 故障、恢复、状态和必要证据必须可查询。旧任务重新检测或报废只能由受控人工决策产生，决定必须记录 actor、时间、原因、原任务和证据引用；Host 重启、连接恢复或普通复位不得自动重检、自动报废或自动重放已完成动作。
FR08 算法失败、超时、未配置、未接入或无有效判定在有限重试后必须为受影响工件形成可追溯的 `Pending` 结果，并按冻结 `RecipeRunPlan` 经正式 PLC 动作端口分拣到 Pending 区；其他工件继续。Detection 算法超时在首次调用之外最多重试 2 次（总计最多 3 次尝试），退避 2/5 秒，单阶段总期限 按冻结RecipeExecutionBudget确定的且不得重置。对象身份、位置或目标无法唯一映射时仍按 `MappingFailed` 暂停，不得把算法异常、映射失败或 Pending 转为 OK、默认成功或省略分拣动作。
FR09 Host 必须提供上述唯一路由的受控取盘确认 API；仅在已核验同一运行/料盘的 `WholeTrayCompletionReference`、已提交 `ObservedUnlocked` 事件及匹配revision后接受确认，并持久化认证身份、幂等键、服务端确认时间/结果、原因、渠道/来源和事件引用，原子生成不可变 `FinalUnloadCompletion`。007受控客户端的自动模拟确认记录Test/Simulated，不写成真实人工操作。当前客户确认原型不新增或改写控件；生产页面入口须等待新原型版本、哈希和明确批准。
FR10 本期最小端到端验收必须由真实 Host 进程通过正式 Modbus TCP 接入仓库既有独立 VirtualPlc 进程，并使用正式业务端口、同一状态机和真实 SQLite 完成全链路持久化；相机、光源和算法可使用明确标记 `Simulated/Test` 的正式适配器，人工确认由测试/联调客户端调用 Host API。进程内全模拟只作为规则/合同/集成测试证据，不替代该端到端验收，也不得宣称真实 PLC、相机、算法、节拍或生产兼容性通过。
FR11 第一工位完整主流程必须以同一 `runId/trayId`、冻结配置和可追溯身份连续执行 001 启动夹紧、3D 高度、F 单次扫码、配方计划/绑定、持久化 handoff 及 003 整盘闭环；只有已提交且身份一致的 handoff 才能由 Host 自动触发 Detection。不得再次人工启动、直接构造 003 起始状态、预造冻结计划或绕过公共准备步骤。
FR12 本期独立进程端到端验收至少覆盖五类场景：正常完整闭环；算法有限重试失败后以 Pending 继续并完成；`WholeTrayCompletion` 前拒绝解锁且 `ObservedUnlocked` 前拒绝人工确认；Sorting、UnloadPreparation 或 UnlockObservation 的 PLC 动作发生断联/连接代次变化时进入 `UnknownHeld`、保持占用且不自动重发；Host 重启后只按已提交事件恢复、已完成物理动作不重放且在途无终态动作不自动执行。任一场景缺少实际进程、协议、数据库或日志证据均不满足本期完成条件。
FR13 临时通信错误在首次调用之外最多重试 3 次（总计最多 4 次尝试），退避 1/2/4 秒；所有尝试和退避共用单阶段 按冻结RecipeExecutionBudget确定的总期限且不得重置。动作意图及对应投影、反馈事件及对应阶段投影分别使用设备/算法调用前后的短事务；Detection、Sorting、UnloadPreparation 的 Completed 事件均已提交后，另用独立聚合短事务重新核验身份、plan revision、source/quality 和未解决终态，并原子写入 `WholeTrayCompleted`、`WholeTrayCompletion` 和整托投影；`ManualTrayRemovalConfirmed` 与 `FinalUnloadCompletion` 使用同一个后续短事务。不得跨 PLC、相机或算法调用持有事务；可能已派发的 PLC 物理动作永不自动重发，必须进入 `UnknownHeld`。
FR14 VirtualPlc 本地监控作为联调诊断，须按点位实际写入序号显示变化；“最近变化”不显示 PLC/上位机心跳，但当前值仍可观察。只保留有限事件缓存，游标落后时明确报告缺口；本地查询不得成为 Host 控制或业务完成接口。数值 0 是实际点位值，不得显示成空值或无事实。此项依据本次用户手工测试反馈，不作为来源协议新增点位或生产规则。

FR15 首次PLC通信失败、连续3秒无有效心跳、状态观测过期、协议解析或配置版本不匹配，以及动作请求或反馈失败时，Host必须沿既有`requestId/commandId/runId`及适用的`operationId`、尝试序号、`connectionEpoch`记录分级分类的结构化诊断。记录实际发送/未发送、所用协议合同与冻结配置版本、请求和反馈的适用身份及原始错误上下文、最后一次可靠状态及其时间、停止阶段、判定依据和实际限制/恢复核对结果；不能把缓存旧值、通信失败或超时解释为PLC明确报告不安全，也不能因连接恢复推定安全或动作完成。PLC以本次可靠反馈明确报告不安全时，必须与“通信失效导致无法确认安全”分别分类和呈现，均保持既有安全门禁与无盲重发要求。心跳/轮询及同类重复故障日志须限频或聚合，保留首末时间、次数、状态转变和关键失败请求/反馈；必要日志须在进程退出后可按请求、运行和动作关联查阅，凭据不得写入日志。（来源：2026-09-24用户诊断决定、AGENTS.md“主流程日志与诊断”、宪章3.1.0 P09；不新增PLC点位或改变协议来源。）

FR15最小验证：一次已受理启动后在首次正式Modbus请求注入通信失败，并对照一次同代次可靠状态明确不安全；保存Host与VirtualPlc原始日志、API回执/运行查询及适用的协议请求/反馈。仅凭这些已保存资料须定位`requestId→commandId/runId→停止阶段`，区分未发送、发送失败、已发送但反馈未知与明确不安全，给出底层异常或明确标注未知原因、实际受限处置及无后续动作的证据。再以心跳过期或陈旧状态的定向测试核验旧值不被当作当前安全事实，重复日志降噪不吞状态变化。VirtualPlc证据只标SoftwareLoopOnly，真实PLC须另行实测；历史FR12五场景通过不自动算本增量通过。

2026-09-24 心跳应答延迟缺陷限定补充（`station01-heartbeat-response-delay`）：FR03/FR15 的 3 秒保护及心跳与业务独立连接保持不变。Host 正常启动、媒体/算法、SQLite 和页面查询负载不能使已收到的PLC心跳请求因本进程工作调度长期排队而失去及时应答；应以有界、可持久的 UTC/单调耗时及 Modbus 事务诊断区分调度等待、读写交换、VirtualPlc处理与后续应答。慢但最终成功亦须可定位，真实超时仍锁动作且不自动续跑；诊断只限 Test/VirtualPlc 与本缺陷必要路径，不外推真机或覆盖旧证据。

FR16 新版下料按§3.1.6：写冻结目标Camera_Target_X/Y及Grab_Target_Z(0003/0005/000B)，再命令4；核验本次到位和实际XYZ后清命令。前次动作必要复位及安全条件保持，不再采用写检测Z或命令4只动XY约定。普通盘末Detection→UnloadPreparation→适用Sorting→WholeTrayCompletion→ObservedUnlocked→页面取盘确认→Final；到位不等于可取盘，适用分拣/必要保存/无未知在途门禁必须成立。 Float32/地址解释须按现场校准，不把Test坐标用于真机。

FR17 下料前仍须前次动作的Inspection_Status完成→本轮Z_Reset_Status=2→清Inspection_Status；失败或未知禁止依赖运动。命令4采用抓取Z目标及本次实际XYZ核验，不继承检测Z安全位置等于下料Z的旧假设。

FR18 保存下料意图、冻结目标/来源、协议版本、写入与读回、本轮连接代次和实际XYZ；写成功/旧到位/缓存不等于本次完成，无法关联时UnknownHeld且不自动重发或解锁。

**历史追溯：T045 编排责任边界（非规范性，现行要求以上述规范性章节为准）**

- Host 编排器是整盘检测、分拣和下料三个阶段的唯一流程推进者，负责按冻结的 RecipeRunPlan 顺序发起动作、接收真实端口结果、处理超时/断联/重复和恢复，并更新公开运行状态。
- 此处 Host 编排器指由唯一 Host 托管的 Application/Workflow 编排模块；Host 层负责装配和 API，物理动作仍经过统一 Motion 准入及 PLC 适配器，不新增竞争状态所有者。
- RecipeRunPlanner 只生成不可变、可追溯的步骤计划；它不返回阶段成功，不生成完成 ID，也不绕过动作端口。
- PLC、相机和算法适配器只提供各自端口的动作受理、执行结果和设备事实；它们不能直接推进 Host 的业务状态或生成整盘完成结论。
- 整盘检测通过检测端口执行并形成检测阶段事实；分拣和下料通过 PLC 动作端口执行并形成各自的物理完成事实。三个阶段均必须由 Host 编排器记录阶段结果；计划生成不是完成，状态读取必须匹配本次动作、连接代次和合同完成条件，不能仅凭缓存状态推断完成。
- 三个阶段采用独立追加式事件链保存证据：开始、端口受理、执行、完成/失败，以及超时、断联、连接代次、恢复核对和人工处置结果都必须带运行 ID、阶段、动作尝试、持久化写入 ID、时间和来源。状态投影可以汇总事件，但不能覆盖或删除历史事实。
- 分拣阶段必须使用最新版协议的 `Sorting_Part_Index(4x0020)`、`Sorting_Cmd(4x0021)` 和 `Sorting_Exec_Status(4x0022)` 合同；只有反馈与当前 `operationId`、连接 `epoch` 及协议完成状态匹配时，Host 才能追加分拣 `Completed` 事件。错误码沿用最新版协议定义，未知反馈不得转为成功。
- 下料阶段不使用独立的协议外命令。`UnloadPreparation` 使用最新版协议 §2.2 的 `XY_Move_Cmd=4x0001` 值 `4` 和 `XY_Pos_Confirmed=4x0002` 值 `1`；`UnlockObservation` 使用 §2.4 的 `Pallet_Lock_Cmd=4x0023` 值 `0` 并读回 `Pallet_Lock_Status=4x0024` 值 `0`；人工取盘确认是 Host 事件。T046 必须记录 §2.2、§2.4、§3.1.6 和总时序图对应步骤的引用，不得新增协议外点位或默认成功。
- 下料阶段在业务上拆为三个有序子阶段：`UnloadPreparation` 负责移动到下料位并确认 `XY_Pos_Confirmed=1`；`UnlockObservation` 负责写入 `Pallet_Lock_Cmd=0` 并确认 `Pallet_Lock_Status=0`；`ManualTrayRemovalConfirmation` 负责记录人工取盘确认。三阶段各自追加事件和失败/未知结果，不能用单一布尔值合并或跳过其中任一阶段。
- 第 135 步“确认下料完成”由 Host 暴露的受控操作员确认 API 产生，不映射为 PLC 反馈或新增传感器点位；确认事件必须记录操作员身份、幂等键、时间、确认阶段、结果、原因、`WholeTrayCompletionReference` 和 `ObservedUnlocked` 事件引用，并在持久化成功后追加 `FinalUnloadCompletion`。软件验收使用测试/联调客户端显式调用，当前客户确认原型不新增确认控件。
- 就下料子阶段而言，VirtualPlc 覆盖 `UnloadPreparation` 的 `XY_Move_Cmd=4`/`XY_Pos_Confirmed=1` 和 `UnlockObservation` 的 `Pallet_Lock_Cmd=0`/`Pallet_Lock_Status=0` 以及失败、保持和断联结果；VirtualPlc 整体仍可通过正式 Modbus TCP 提供第一工位所需的心跳、夹紧、运动和分拣事实。`ManualTrayRemovalConfirmation` 必须由 Host 测试入口提供，不得作为 PLC 点位或默认模拟成功。
- DetectionPort 结果必须由 Host 按冻结的 `RecipeRunPlan` 映射为有序分拣动作；任何缺失、重复或歧义映射都产生 `MappingFailed` 事件并暂停当前托盘，未形成完整映射前不得调用分拣 Action Port。
- 动作意图及对应投影在设备/算法调用前使用短事务提交，反馈事件及对应阶段投影在调用后使用另一个短事务原子提交；三个阶段的 Completed 事件均已提交后，另用独立聚合短事务核验并原子创建 `WholeTrayCompleted`、`WholeTrayCompletion` 和整托投影。人工确认与 `FinalUnloadCompletion` 使用同一个后续短事务；不得跨设备或算法调用持有事务。重启时只重放已提交事件，可能已派发且无终态的 PLC 动作进入 `UnknownHeld`，不得自动重发。阶段事件和完成证据至少保留 7 年，保留期限只能延长。
- Host 只有在检测完成、分拣完成和 `UnloadPreparation` 完成事件均已提交后，才能创建不可变的 `WholeTrayCompletion` 记录；该记录必须引用三个阶段的持久化事件链和同一运行/料盘身份，再作为解锁流程的唯一业务前置证据。它不是布尔值、临时 GUID、计划结果或 PLC 单点状态。
- `WholeTrayCompletion` 自身也必须提交后才能交付记录引用；解锁流程须查询并核验记录及关联证据，不能把任意非空 ID 当成授权。解锁读回 `Pallet_Lock_Status=0` 后，Host 才能记录 `UnlockObservation` 完成并等待人工取盘确认；人工确认后追加不可变 `FinalUnloadCompletion`。阶段失败、超时或断联事件不是完成事件；分拣、下料准备、解锁和人工取盘的前置条件不能因存在终态记录而被放宽。

**历史追溯：T045 失败、断联和恢复边界（非规范性，现行要求以上述规范性章节为准）**

- 临时通信失败、算法超时等可恢复故障允许有限次数重试，重试次数、退避策略和每次尝试必须进入阶段事件链；算法在上限后仍失败、超时、未配置、未接入或无有效判定时，为受影响工件记录 `Pending` 及原始异常证据，并继续后续可执行工件和正式 Pending 分拣，不默认暂停当前托盘或整条产线。只有对象身份、位置或目标无法唯一映射、安全互锁不满足、Pending 区容量不足或物理动作结果未知时，才按对应合同暂停或保持设备占用。
- 重试策略固定为：临时通信错误在首次调用之外最多重试 3 次（总计最多 4 次尝试），退避 1/2/4 秒；Detection 算法超时在首次调用之外最多重试 2 次（总计最多 3 次尝试），退避 2/5 秒；每个阶段总期限 按冻结RecipeExecutionBudget确定的且不得重置。可能已派发的 PLC 物理动作不允许自动重发，必须进入 `UnknownHeld`；每次尝试和退避均追加到阶段事件链。
- 重试通信读取或算法调用不等于重发物理动作；PLC 动作执行结果未知时禁止自动重发。重试策略不得暂停心跳/安全监测或绕过已有安全互锁；有限次重试及局部暂停依据用户本次明确决定，不被解释为无限算法等待。
- PLC 动作完成状态未知、连接断联、读回超时或连接代次变化时，阶段必须进入 `UnknownHeld`，保持设备锁紧和资源占用，不能使用默认成功值、旧快照或降级结果继续分拣、下料或解锁。
- 人工恢复只能以最后一个已确认持久完成的阶段为边界，核对通过后继续其后的未完成工作；旧任务重新检测或报废必须由受控人工决策产生并持久化 actor、时间、原因、原任务和证据引用。Host 重启或恢复不得自动重检、自动报废或自动重放已完成动作；未完成阶段不得被标记为完成。
- 虚拟相机和虚拟算法只实现/提供 `DetectionPort` 的检测结果，包括明确标记来源的 OK、NG、Pending 及算法异常收敛结果；VirtualPlc 只模拟 PLC 点位、动作完成、失败和断联。Simulation/Fallback 结果必须标记为模拟或降级来源，不能作为真实 PLC、真实相机或真实算法验收证据。
- 任意异常都不得通过默认值、固定返回值、临时标识或模拟授权绕过分拣、下料、完整托盘完成或解锁前置条件。
- 后续 plan 需把 T046 定义为现有下料边界的正式引用和合同冻结：移动到下料位、解锁读回、Host 人工取盘确认及 `operationId/connectionEpoch` 关联；不再寻找独立下料点位，不得把分拣取放料重复建模为下料动作。
- 003历史范围只纳入当时的虚拟组件：相机、光源和算法以正式端口下明确标记的`Simulated/Test`适配器参与同一流程，当时不新增独立虚拟服务。007已批准独立虚拟算法worker及固定图片正式采集，按上文007共享边界另行实现；客户原型保持不变，前端页面实现仍归006。

历史兼容解释（001/003，非规范性）：

- `WaitingPhysicalStart` 和 `WaitingClamp` 是面向调用方的业务阶段，不是协议字段，也不是独立的实体按钮反馈。它们可用于显示“启动请求已受理、尚未取得本次夹紧事实”和“正在观察本次夹紧状态”，但不得被测试或代码当作按钮已经发生的证据。
- `PC_Start_Cmd` 受理后，Host 只能读取本次连接代次的 `Pallet_Lock_Status`。只有读到 `1` 才能进入区域握手、第一条移动和后续检测；读到 `2`、观察期限内保持 `0`/未定义值、断联或代次变化时，必须进入对应受限/未知状态。
- FullSimulation 由 `SimulatedPlc` 按配置调度 `Pallet_Lock_Status` 的设备内部变化；测试断言应验证点位读写顺序、准入门槛和失败处置，不断言不存在的 `ButtonPressed`、`ClampStarted` 或 `ClampCompleted` 事件。模拟通过不得记为真实 PLC、现场按钮或生产验证。
- `Pallet_Lock_Cmd=0` 仅在整盘检测、分拣和 `UnloadPreparation` 完成并生成 `WholeTrayCompletion` 后产生，并须读回 `Pallet_Lock_Status=0`；第一工位准备、3D/F 完成或工位移交不得提前解锁。人工取盘确认属于解锁后的 `FinalUnloadCompletion`，不作为写 0 的前置条件。

## 第一工位端到端验收标准

用户所列 20 项继续映射至 `validation.md`，未执行、阻塞、跳过或仅有源码不得标成通过。本期完成还必须同时满足以下标准：

1. **运行组合真实**：分别启动 Host 和仓库既有 VirtualPlc 两个进程，保存各自 PID、启动命令、配置版本和原始日志；二者只通过正式 Modbus TCP 协议交互。进程内全模拟不能替代本项。
2. **起点连续**：从现有 Host 启动 API 发起唯一请求，以同一 `runId/trayId`、冻结配置和证据链完成 001 启动夹紧、3D 高度、F 单次扫码、配方计划/绑定、持久化 handoff，并由 Host 自动进入 003 Detection；无第二次人工启动、预造完成状态或直接构造 003 请求。
3. **正常终点**：依序真实执行 Detection、Sorting、UnloadPreparation，提交三个阶段完成事实和 `WholeTrayCompletion`；随后写 `Pallet_Lock_Cmd=0` 并读回同一连接代次的 `Pallet_Lock_Status=0`，提交 `ObservedUnlocked`；最后由测试/联调客户端调用受控 Host API，持久化操作员确认和不可变 `FinalUnloadCompletion`。只有上述记录均已提交才判定成功。
4. **五类最小场景**：独立进程证据必须覆盖正常完整闭环、算法失败转 Pending 后继续、完成证据前拒绝解锁/人工确认、PLC 动作断联或连接代次变化进入 `UnknownHeld` 且不自动重发、Host重启后只重建持久故障查询，双端复位/初始状态后新轮完整执行并关联旧轮。
5. **设备与状态事实真实**：只以匹配设备反馈和已提交事实完成；保持3秒心跳、F3/4与检测1/2对应轴复位；下料按新版FR16—18。当前自动翻面按20260925分区协议§3.1.5：当前零件XYZ写0003/0005/000B→命令3→本次到位/实际XYZ核验→清命令→目标面→状态2及实际面匹配→Flip_OK=1→状态0→Flip_OK=0；料盘不翻面。逐个处理仍需后续面的实体，整体不按部位重复；同目标面的不同实体分别闭环。普通翻面协议已定义，受限项仅为实际代码能力、合法点位配置或真机标定。 同盘分拣：OK留原槽不搬，NG/Pending各去对应区配置目标，姿态异常跳过后续检测，最后从原槽实际分拣到Pending；源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升。真实取料反馈和在途保存先于放料；物理槽号不是Sequence，预留/在途持续到可靠完成。新状态1/2/3只由通信翻译为取料成功/放料完成/失败，旧状态2/3及ACK不再适用。
6. **算法异常不伪造**：Detection 算法超时首次调用之外最多重试 2 次（总计最多 3 次尝试），退避 2/5 秒；有限尝试后的失败、超时、未配置、未接入或无有效判定必须形成带原异常证据的 Pending，并经正式分拣动作进入 Pending 区；不得转为 OK、默认放行或省略 Sorting，单阶段 按冻结RecipeExecutionBudget确定的总期限不得重置。
7. **持久化可核对**：真实 SQLite 中可按同一身份查询配置/配方快照、handoff、动作意图、阶段事件、投影、连接代次、重试/Pending、`WholeTrayCompletion`、`ObservedUnlocked`、人工确认和 `FinalUnloadCompletion`；证据必须证明意图/反馈分别短事务提交、三阶段完成后才执行独立聚合短事务、人工确认与最终完成同事务提交，且事务不跨 PLC、相机或算法调用。必要提交失败不得显示成功。
8. **来源明确**：所有相机、光源、算法和 VirtualPlc 证据标记为 `Virtual/Simulated/Test`，报告只证明软件协议和业务闭环，不宣称真实设备、算法精度、现场节拍或生产验收通过。
9. **恢复人工门禁**：旧任务重新检测或报废必须由受控人工决定并保存 actor、时间、原因、原任务和证据引用；Host 重启、重连或复位不得自动重检、自动报废、自动续跑或重放已完成动作。

## Virtual/Simulated 实现边界

- **允许 Virtual**：只复用仓库既有独立 `VirtualPlc`，其整体能力可通过与真实 PLC 相同的正式 Modbus TCP 地址、类型、方向和状态语义产生第一工位所需的心跳、夹紧、运动、分拣、下料、解锁、失败、断联和 `connectionEpoch` 事实；“下料子阶段覆盖 UnloadPreparation 和 UnlockObservation”不限制这些整体能力。VirtualPlc 不执行 Detection、业务编排、人工确认或业务数据库写入。
- **允许 Simulated**：相机、光源和算法可使用实现正式端口的 `Simulated/Test` 适配器；必须接收正式输入并输出带来源、质量、身份、时间和结果引用的正式响应，经过与生产适配器相同的 Host 编排、有限重试、Pending、映射、阶段状态和持久化路径。
- **必须真实执行**：Host 进程、唯一 Workflow、001→003 handoff、正式端口调用、Motion/Safety 准入、Modbus TCP 通信、SQLite 提交、API 调用、状态查询、错误处理、恢复判断和证据归档不得用固定返回值、直接改状态或伪造日志替代。
- **确认输入边界**：测试/联调客户端只在已提交`ObservedUnlocked`后通过Host受控API提交确认；007自动模拟取盘记录受控Test身份、渠道及Test/Simulated ManualActor，不表述为真实人工移盘。不直接写数据库、不调用PLC/相机/算法端口，不把脚本启动或默认成功当作确认事实。
- **生产门禁**：Production 不得自动回退到 Virtual/Simulated；真实适配器未接入时必须明确 `NotIntegrated/NotReady`。Virtual/Simulated 通过只满足本期软件闭环，不构成真实设备或生产验收。

## Deferred 功能列表

以下内容明确延期，不属于本期第一工位完整主流程完成条件，也不得阻塞上述五类软件场景：

- 第二工位及其他工位的页面、流程、设备适配、工艺编排和联调。
- 真实 PLC、真实相机/光源 SDK、真实算法 Worker、现场点位/节拍、硬件安全和生产兼容性验收；这些内容为 `Deferred/NotRun`，不得由 Virtual/Simulated 证据替代。
- 客户原型中的生产人工取盘确认控件及相应页面交互；当前 ZIP 保持只读，等待新原型版本、新哈希和明确批准。Host 人工确认 API 及测试/联调客户端调用仍属于本期。
- MES 联网、上传、缓存补传和对账；模型管理、训练、发布和回滚；样本管理、标注和样本库业务，仅保留 `NotIntegrated` 边界。
- 五类最小端到端场景之外的全组合故障、压力、长稳、性能、现场容量和生产恢复矩阵；直接影响主流程门控的规则/合同测试仍须完成，但不扩大本期独立进程验收集合。
- 不改变第一工位主流程正确性的跨重启媒体索引扩展、通用通知优化、安装包扩展、额外页面和体验优化。
- OPEN-16 的 NG/Pending 并存最终优先级、OPEN-26 的未知安全替代动作及 OPEN-22/OPEN-23 的生产环境与正式权限参数，只限制各自依赖的生产规则；本期不得猜测或声称已关闭。

## 2026-09-23 共享媒体查询最小增量

007的固定图片联调需要以同一runId公开已提交的媒体身份。后端只依据已提交Media写入、公共准备采集意图和Detection采集阶段事件关联captureId、角色、冻结步骤及业务相机身份；缺失身份保留Unknown，不从文件名、时间或数组位置推定。媒体字节仍经现有授权媒体读取入口提供，前端/宿主不读数据库或本地路径。本增量只服务007已批准的真实媒体展示依赖，不追溯改变003历史验收。

## 2026-09-24 最新需求与008完整执行对齐

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

- **FR19（008对齐）**：正式共同执行覆盖定位/采集/分析/保存、配置更多面与可选额外E、翻转定位放回与姿态复查、分拣后下料。新通信原码/地址/内部握手不泄漏业务；真实槽位、反馈及保存/取消/期限门保持。
- 采用上述新版FR16—18及独立保存门禁；普通OK不等待全部分拣能力。
- 成功条件：按[008覆盖矩阵](../008-recipe-driven-inspection/coverage-matrix.md)保留Q01—Q22编号及历史证据，按008 SC-001验证当前一面/两面及四面3CD＋1AB及配置驱动的更多面/额外E从正式前端到Final、适用C01—C08和F1—F6。取消每M固定两配方及固定按冻结RecipeExecutionBudget确定的作为新验收门槛。任务与证据状态仍未更新。

当前任务归属与顺序：参见[本功能tasks](tasks.md)文末S0—S5增量及[008任务](../008-recipe-driven-inspection/tasks.md)首批集合。共享实现只登记一个所有者；历史版本/完成证据保留原范围，最新Q/C/F规则不倒填旧任务。

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。


## 新版直接要求（2026-09-26）

逐对象按配置取件点定位→PLC按产品型号/目标面翻转→配置放回点定位→放回；相关对象放回后统一3D姿态复查，正常继续、异常跳过后续检测、最后原槽Pending分拣并返回物理槽号，F不重绑。料盘不翻面，整体不按部位重复动作，不沿用旧Flip_OK或独立实际面号前置。

同盘分拣：OK留原槽不搬，NG/Pending各去对应区配置目标，姿态异常跳过后续检测，最后从原槽实际分拣到Pending；源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升。真实取料反馈和在途保存先于放料；物理槽号不是Sequence，预留/在途持续到可靠完成。新状态1/2/3只由通信翻译为取料成功/放料完成/失败，旧状态2/3及ACK不再适用。

人工区占用1禁止运动；人工完成且既有安全成立后确认1→占用0→清确认；面确认/持件恢复仍按B07局部输入。当前F3/4已实现的未变部分复用，E不自动外推。

## USR-20260926-E：两端坐标与取放证据需求

本次依据USR-20260926-E（六项问题及四面范围确认）。协议原件为E:/dzk/gaode-1/高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx；正文为上下位机信号分区版，修订日期2026年9月25日，SHA-256=405ac9ee2ae2d765951d9f523dc7195cc77f6cd1f38dbc0ad144a0013586c519。Office内置修订号2、修改时间2026-09-24T04:06:16Z仅为文件元数据，不代替正文修订日期；用户已确认该实际文件，不因口述名称差异重复确认。协议原件只读。

问题1—5为用户本地已观察、根因待运行核验的问题；不能凭源码存在逻辑直接关闭，也不能未经复现认定全部为通信缺漏。本次仅记录需求与验收，不声明复现、修复或运行通过。问题6为已确认范围变更，不是尚待复现的通信故障。

各动作按现有协议下发完整目标XYZ，读取本次实际XYZ并校验，使用该动作规定的Z轴；目标、实际坐标及关键握手须按运行、对象、面、步骤和动作关联查询。变化日志未显示相同值，不等于未实际读写；应以两端实际请求、接收及反馈证据区分目标或通信缺漏、实际反馈缺漏与日志显示省略。VirtualPlc必须实际接收指令并产生模拟反馈，Host不得伪造反馈。

问题1（扫码）、问题2（单面检测）、问题3（第二拍照位）、问题4（下料）对应FR01/FR04/FR15及适用FR16/FR19；按实际请求、VirtualPlc接收、实际XYZ反馈及本次校验逐项验收。FR15必要持久诊断须能区分未写入、写入失败、反馈缺失及变化日志省略；值相同仍可查完整动作上下文，不要求无限全量轮询日志。问题5沿用FR19及同盘Sorting规范、§3.1.6：先本次取料成功状态2，后清命令、提交放料槽号及目标XYZ，再放料命令；缺可靠反馈不进入放料。

固定四面流程仅保留三个CD＋一个AB；全同组和二比二组合退出当前业务配方及必做验证范围。一面、两面规则保持；少数组所在面及执行位置由配方决定，不限定最后一面，不新增全排列实跑要求。成组成员和整体检测部位中的四面对象也须检查适用性；成组、整体、E扫码、旋转和人工换面等独立场景保留。

两端实施位置为E:/dzk/gaode-1/backend及E:/dzk/gaode-1/VirtualPlc。本次不改任一端；未复现问题不因源码存在相应逻辑而关闭，也不推定所有现象均为通信缺漏。USR-D完整新轮及RST-01/RST-02已关闭结论保持；历史范围和运行事实保留。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

003既有运行查询消费008已提交事实；PLC协议、握手、动作含义和权限保持既有定义。

## 2026-09-27 复位观察同步必要修复（原003 T072-A、008 T068/T069/T070）

必要顺序用例在Reset 202后立即Check实际返回RecoveryResetNotObserved，两次均未到链接保存注入，原r19 TRX保留。复位直接Modbus Ready已成立但缓存PlcReady仍旧false；原MotionCoordinator必要门禁不放宽。仅ResetAsync在原轮询/原期限内同时等待缓存实际PlcReady、Connected/SafetyClear，使用同一次observed快照；不改变接口、信号、初始判据或生产机械未知边界。评估见.specify/bugs/008-reset-ready-observation/assessment.md。源码当前尚未修改，待当前测试结束；现有两个保存门禁及正式旧图/完整新轮独立新包复验，已有任务承接不追加重复任务。

## 2026-09-27 T065机制修复范围（待本轮验证）

同DLL受控观察已证明Portable批队列派发延迟：Host业务/心跳txn3分别入队后1112.2005/1056.6997ms，入队仅0.006/0.0072ms，真实1秒超期且锁定；独立Native候选2937个非零操作唯一回调，1560个Host响应头最慢20.2574ms。证据入口：`.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/`，旧构建和报告保留。

最小接线为Windows、purpose=Test冻结fixture显式WindowsNativeThreadPool开关，仅本次所属Host/VirtualPlc子进程DOTNET_ThreadPool_UseWindowsThreadPool=1、inline=0；普通启动及旧冻结构建不被静默改写。三个最低线程预留位置依微软支持的实际运行配置区分Native/Portable，Native不调用不支持的SetMinThreads、不虚报预留8。正式构建不含Harmony、socket反射或诊断事件。业务API、信号、1秒I/O、3秒心跳、50ms轮询、GC、优先级、失败锁动作及未知结果不重发条件不变。

本增量沿003 T065和008 T055/T070原任务，追加任务0、勾选不变。只验证该机制路径、原期限真实超期锁动作及当前正式Q01同run前端/配方/PLC/相机算法/SQLite媒体/Final；复用未改分支历史证据。r22 HTTP独立保留，真实设备/标定仍待现场，不增加全运行时证明门槛。只有本轮验证完成后才更新验收状态。

## 2026-09-27T05:09Z 本轮验证完成状态

前述实施前待验证状态由本节接续：003 T065原Test/VirtualPlc机制/对照/安全/日志条件，以及008 T055当前正式Q01和T070适用Test对账均已满足，仅这三项授权勾选更新。新构建显式WindowsNativeThreadPool/inline0，旧默认与冻结程序不改；r22 HTTP、缺失历史日志及真实设备/标定不扩大结论。新证据目录为.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z，verification-proof.json与task-checkbox-changes.json可核对；本轮新增任务0、其他勾选不变。


## 2026-09-27 本机复测XYZ名称确认（历史，已由monitor-xyz-history取代）

历史用户确认（已被本轮XY恢复确认取代）信号名为 `XYZ_Move_Cmd`（4x0001）及 `XYZ_Pos_Confirmed`（4x0002）。此确认覆盖既有文本中的XY名称；工作区来源Word仍为原SHA，原件只读，不能把该用户增量冒称Word已改。仅公开名字统一，地址/功能码/值/方向不变；运动目标X/Y/适用Z必须全部实际写入，到位须读回本动作实际XYZ核验。不能由changes数值变化是否出现推断某轴是否发送。

现行验收按新接口语义：逐件翻转/另行放回后姿态复查，F不重绑；取料反馈及真实在途保存先于放料，分拣完成后下料。通信原码由通信测试验证，业务测试不保留旧ACK/原码条件。

此子修复由既有003映射/监控/动作验收任务承接；不追加重复任务或改变勾选。实现范围：VirtualPlc公开点名和监控消费者/必要测试，后端数值寄存器接线不变。证据目录见xyz-sorting-deployment/active-retest.json；实施及验证完成后另写同目录时间戳报告，不覆盖历史通过。

## 2026-09-27 monitor-xyz-history 当前纠正（取代此前监控布局与XYZ公开名称要求）
本轮用户确认：4x0001公开名称为XY_Move_Cmd，4x0002为XY_Pos_Confirmed，与来源Word一致。此前2026-09-27 XYZ名称确认仅保留历史适用性，不再约束当前构建；来源Word内容和摘要不变，内部Xy常量不重构。
删除独立完整XYZ栏目；原“最近数值变化”列表逐信号展示每个Move/Sort的实际XYZ发送及对应动作实际反馈，同值、0及连续全同目标均保留。发送来自本次同连接命令前的audit写入收据，反馈来自动作actual，保留动作/阶段/序号、UTC时间、方向及Z用途。禁止目标冒充反馈、快照补历史、旧命令轴拼接。其他状态/握手/清零changes保留；坐标使用audit唯一来源，Z复位从ZReset动作实际采样显示Z，避免重复。特殊Test HTTP动作逐轴显示真实请求/反馈，标注非Modbus点位、时间未提供不造时间。
复用audit/2.0与changes，不变更共享API字段或PLC业务；页面合并有界记录、去重、缺口提示及清空游标，不因同一记录再次轮询而刷屏。缺轴明确缺失；HTTP/页面渲染/心跳状态错误保持分类。删除motionList/motionStatus及相关版本依赖，使用原historyList的版本标记。
普通OK原位、NG/Pending同盘取放、特殊配方必要搬运、下料命令4及安全门禁保持。验证引用本缺陷带时间戳assessment/fix/test；旧29动作/87轴证据与r3—r7包仅按原范围追溯，新显示重放不冒称新业务实跑。008整体验收不由此关闭。


## 2026-09-29 公共解锁记录与测试监控布局（r9）
依据本轮用户确认及上传write155/transaction6001：Pallet_Lock_Cmd=0实际写入必须显示，即使数值相同；命令受理与Pallet_Lock_Status=0反馈分开，不改变业务握手。复用audit/2.0，命令以实际写入为唯一来源；没有前值不补造0→0，拒绝明确拒绝；状态仍用changes，动作引用仅在同连接真实审计匹配时关联。其他握手/清零、XY公开名称及完整XYZ规则保持。
虚拟PLC只读页面独立布局见[测试监控页面设计](monitor-test-workspace.md)：原历史列表为首屏主区域，点位侧栏可收起；搜索/方向筛选、暂停查看继续接收、恢复最新、去重/有界缓存/缺口提示。此页面不是006客户原型，不修改正式业务前端或协议来源。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

本次共享需求增量（AL03）如下，约束实际本功能生产者/消费者；不扩工艺或页面：

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

可靠目标采样与抬升后完成位置分开，不新增设备信号或强求完成时Z等于目标。通信先经正式StartAsync定义校验及同实例动作准入；坏映射不允许初始化/动作写入。线缆条款仍有效但仅通信侧解释，业务StageAction结果删除ProtocolStatus/内部PositionEvidence.Phase。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

新事实采用device-semantics/1，保存实际语义/关联/来源/质量/观察及opaque引用，禁止硬写protocolStatus=2或Virtual/Derived来源。旧PayloadJson字节不重写；未知原始来源不补造。业务实际commit与调用方有效回执分别记录；查到行不能改变超期动作资格。原事件/幂等/投影同短事务、WholeTray及Final独立提交不变。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

IntegratedDetectionPort虽位于Simulation目录仍是正式文件相机/Worker消费方，不得引用具体协议类型或协议版本作为业务输入；通过语义采集窗口及当前结果保留每图/融合/媒体/算法和保存节点。算法有限失败Pending不替代机械安全/未知保持。公共3D受控失败清理是原特例，F/E/产品检测不得自动继承。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

普通路线依实际顺序Detection→UnloadPreparation→适用Sorting→WholeTray真实保存→可靠ObservedUnlocked→人工确认与Final真实提交。WholeTray引用必须在解锁前重新核验；单一动作Completed、传输回执或解锁均不等于Final。取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

同Host自动续接只在本次所有必要回执及时有效后；已有handoff行只证明保存事实，不能让Query/后段在迟到、取消或重启后重建Bound/Ready资格。保留当前run/tray/plan/对象/面/初始测量/源槽及参数快照；独立API本次绑定意图与事实需保存但不重建旧handoff。当前有效早到回执不因稍晚调度倒判，下一动作仍核原截止/取消/安全。ReceiptObserved有界后置审计不预填自身回执、不递归授权。

维持USR-D：故障旧轮关闭，双端复位及实际初始状态核查后显式新run，从公共3D/F及绑定重新开始。原状态/握手由通信内部解释为复位/初始可靠结果；业务不用raw判断清零。旧事务提交核查只是事实核查，不续跑、不重发未知物理动作；已观察取料但raw失败按已存意图/预留保持占用，数据库全不可用不保证新事实持久。正常暂停及人工换面仍按原合同，人工命令默认面来源不得冒充PLC实测。

各组件provider/version?/quality从实际生产者透传，Real/Virtual/Simulated/Unavailable与历史声明性质分离，不按Host模式或新语义反推历史raw/执行来源。新设备事实关联同次ObservationId及真实提交后opaque引用；没有实收报文时raw缺失如实null/NotRecorded，旧合成值不变成ActualWire。原Host/PLC/Camera/Light/Algorithm/ManualActor完整矩阵及SoftwareLoopOnly限制保持。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready必须核本次有效RecipeApplicationReceipt，不能从已有handoff恢复。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

006尚未交付可推进后端子集验证，但不签009完整基线。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。窗口包含这次意图后设备、raw和绑定事实；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。


### 009 检测保存窗口接线补充（2026-10-01）

本功能检测入口按[003检测合同具体接线](../003-plc-latest-protocol/contracts/detection-port.md)传递本轮冻结 CriticalSaveBudgetMs 与原 Detection 截止；必要保存共受两者约束，不由固定两秒或协议步骤推导。算法意图与派发共享真实会话/时钟/起止时间。实现及验证归009 T035/T048/T049，先前任务勾选保持；本节不声明运行通过。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。

当前来源分类的有限补齐：ResultSource在末尾新增Test，保留既有Real/Virtual/Simulated/Fallback的值和历史含义；仅由明确声明Test的实际算法生产者产生，不从RunPurpose猜测。IntegratedDetection的Source与意图来源来自IAlgorithmPort.Origin，未知仍Fallback/Unknown；完整矩阵继续使用AlgorithmOrigin与实际事实。该变化用于消除把独立Test Worker写成Simulated的固定标签，归009 T034/T035/T039及T043—T046，旧记录不重写、现有页面仅绑定来源。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`仅作为旧payload中的原文保留，不生成同名新业务事实，不倒推历史原始值或来源。消费者不以旧名字/裸kind授予动作；当前面关联继续调用FaceEstablishment.Confirms，原证据与保存门禁不减。通信用例仍检验实际清零，业务断言迁移到当前语义事实和来源，两侧均必需；不新增页面、信号、恢复路径或产品兼容层。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。


### 009 T040/T041 有限独立进程保存故障接线（2026-10-02）

按009 VG V07/V09，在真实Host独立进程中增加可选`Gaode:TestPersistenceFaultCase`，仅在`VirtualPlcIntegration`、Virtual PLC及全部Test配置下接受。固定值为F05-A/B/C、F06-A/B/C、BA04-late-bound/late-handoff；未知值或其他运行环境启动拒绝，不新增业务API。未配置时不安装任何拦截器。Test根中的`009-fault-arm.json`以caseId、runId和nonce选择本次真实运行；独立编排取得正式启动runId后写入，未命中不能当故障验证通过。唯一命中记录实际EventId/WriteId/EvidenceId、时刻、位置和commit事实到同根`009-fault-events.jsonl`。

A通过实际SQLite触发器拒绝相应插入，原Store负责事务回滚与结果；B仅在真实commit完成后扣住回执；C在同一实际边界另持SQLite独占锁暂阻核查。通信raw故障仍经TraceWriter实际job，取料业务事务仍由StageEventStore承担。BA04严格选择本run的RecipePlanBound或本次handoff，不拦其他保存。Test专用保持窗口最多30秒，或收到匹配nonce的`009-fault-release.json`结束；这个注入外限不是业务期限，保持期间原预算/保存期限照常失效，释放后不能复活动作。

C使用受控Test副本的DELETE日志模式取得真实排他锁；A/B不伪造数据库结果，记录存储不可用时仍仅尽力写诊断文件。009验收必须另查实际SQLite、当前回执、原截止、PLC全部后继写和保守占用；故障日志及进程存活不能独自证明验收。此处只完成必要接线接口对齐；代码及实际运行由009任务证据确认。实施/复核角色为Codex，不冒称客户批准，其他功能历史任务状态不变。


### 009 T033/T039 后段退出后的故障保存版本交接（2026-10-02）

integration238的实际三阶段UnknownHeld案例已提交检测事实至Run revision43，公共准备RunExecution仍持旧revision，故障收尾保存被CAS拒绝。仅在已等待后段执行返回UnknownHeld/解锁失败、当前执行停止后，故障协调器通过既有ITraceQuery有界读取同run的已提交状态，核对runId/requestId/subjectId/context、无终态、版本不回退且不存在活动配方应用保存窗口，再把现有运行保存游标交接到真实已提交revision。随后原CriticalSave与CAS不变；不循环重试冲突，不重放物理动作，不把只读核查或迟到记录当绑定、取放或后继动作成功许可。

原始故障ErrorCode及UnknownHeld事实保留，FaultRequiresNewRun仍作为恢复规则/事件和旧continue拒绝码，不覆盖已记录的设备故障原因。查询/保存失败仍无成功保存声明，不调整预算或重启规则。实施归009 T033/T039，验证保留原ThreeStageMainFlowIntegrationTests未知保持与零WholeTray断言，并补游标交接拒绝条件；当前修复未运行验证，不改其他功能历史任务状态。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 013实施前定向同步（2026-10-04）

SY-01：FR03/FR15当前采集按013 A01/A03/A07：独立心跳300ms；基础活动200/空闲500ms、动作反馈200ms、位置运动或未知500/确认静止1000ms。首Moving/Executing仅有依据的局部50ms、齐备后200ms。必要读写/清零即时，不等慢周期；相对plannedDue计调度迟延。原I/O、3秒、动作/保存截止及T065真实超期锁动作不变。旧T065“50ms不变”为当时构建事实，不再约束013现行策略；新周期不代替真实反馈或当前安全证据。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A03/A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。
- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
