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
- Q: 阶段事件、WholeTrayCompletion、重启恢复和保留期限应采用哪种一致性合同？ → A: 动作意图及对应投影先用一个短事务提交，提交后才调用设备或算法；反馈事件及对应阶段投影再用一个短事务原子提交。Detection、UnloadPreparation、Sorting 的 Completed 事件均已提交后，使用独立聚合短事务重新核验并原子写入 `WholeTrayCompleted`、`WholeTrayCompletion` 和整托投影；人工确认与 `FinalUnloadCompletion` 使用同一个后续短事务。不得跨 PLC、相机或算法调用持有事务。重启只重放已提交事件，可能已派发且无终态的 PLC 动作进入 `UnknownHeld`；阶段事件和完成证据保留 7 年，期限可延长但不能缩短。
- Q: 下料阶段是否拆分为移动到下料位、解锁读回和人工取盘确认三个子阶段？ → A: 是。Host 依次记录下料准备（`XY_Move_Cmd=4` 且 `XY_Pos_Confirmed=1`）、解锁（写 `Pallet_Lock_Cmd=0` 且读回 `Pallet_Lock_Status=0`）和人工取盘确认；三者不能合并为单一布尔完成事实。
- Q: `WholeTrayCompletion` 是否表示人工取盘后的最终完成？ → A: 不表示。它表示检测、分拣和下料准备完成且可以进入解锁流程；解锁读回成功后，人工取盘确认再生成独立的 `FinalUnloadCompletion`，两者均为不可变记录。
- Q: 总时序图第 135 步“确认下料完成”的输入来源是什么？ → A: 由 Host 受控 API 接收操作员确认，不新增 PLC 点位或传感器点位；本期软件验收由测试/联调客户端显式调用，当前客户原型不新增控件。确认必须持久化操作员、时间、确认阶段、结果和原因，并生成 `FinalUnloadCompletion`。
- Q: T046 是否应从寻找独立下料 PLC 命令改为确认现有协议边界？ → A: 是。T046 只确认移动到下料位、解锁读回和 Host 人工取盘确认的正式边界，记录协议版本和章节引用；不得新增独立命令、反馈点位或协议外错误码。
- Q: VirtualPlc 在下料边界中应模拟哪些行为？ → A: 就下料子阶段而言，模拟正式协议已有的下料位移动、解锁命令及 `Pallet_Lock_Status` 读回；这不限制 VirtualPlc 整体还可通过正式 Modbus TCP 提供第一工位所需的心跳、夹紧、运动和分拣事实。人工取盘确认由 Host 测试入口产生并持久化，不模拟为 PLC 点位或真实设备验收事实。
- Q: `PlcWorkflowStage`、完整整盘流程阶段和阶段事件应如何分层，检测阶段是否允许调用 PLC 动作端口？ → A: `PlcWorkflowStage` 只表示 PLC 动作阶段，固定为 `Sorting`、`UnloadPreparation`、`UnlockObservation`；新增通用 `WholeTrayWorkflowStage` 表示完整业务阶段，固定顺序为 `Detection → UnloadPreparation → Sorting → UnlockObservation → ManualTrayRemovalConfirmation`。`StageEvent`、`StageProjection`、`RetryPolicy` 和 `RecoveryPolicy` 使用 `WholeTrayWorkflowStage`，PLC Action 请求仍使用 `PlcWorkflowStage`。`Detection` 只调用 `IDetectionPort`，不得调用 `IPlcStageActionPort`。检测结果到分拣计划的缺失、重复或歧义追加 `MappingFailed`，归属 `Detection` 阶段并暂停当前托盘进入人工核对；`UnknownHeld` 归属产生未知物理结果的 PLC 动作阶段，分拣、下料准备和解锁分别归属 `Sorting`、`UnloadPreparation`、`UnlockObservation`，保持设备占用且禁止自动重发；人工核对事件归属触发它的阶段，`ManualTrayRemovalConfirmation` 只在 `ObservedUnlocked` 后由 Host 接收，不能由 PLC 端口产生。

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
- 同一 `runId/trayId`、配置快照、连接代次关联和证据链必须连续覆盖 001 公共准备的 3D 高度、F 单次扫码、配方计划/绑定及持久化 handoff，再由唯一 Host 编排器自动进入 003 的 `Detection → UnloadPreparation → Sorting → WholeTrayCompletion → UnlockObservation → ManualTrayRemovalConfirmation → FinalUnloadCompletion`。001→003 之间不得要求第二次人工启动，不得以测试代码、预造完成状态或临时冻结计划跳过公共准备与移交。
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
- 阶段顺序固定为 `Detection → UnloadPreparation → Sorting → UnlockObservation → ManualTrayRemovalConfirmation`。`WholeTrayCompletion` 位于前三个阶段完成之后、`UnlockObservation` 之前，是后续解锁门控证据，不新增为 PLC 动作阶段。
- `Detection` 通过 `IDetectionPort` 产生检测事实和 `MappingFailed`；未形成完整映射前不得调用 `IPlcStageActionPort`。`MappingFailed` 使 `Detection` 投影进入人工核对暂停状态。
- 算法失败、超时、未配置、未接入或无有效判定与对象身份/位置映射失败必须区分：前者在有限重试后形成该对象的 `Pending` 检测结果并继续完整映射，后者因无法安全确定分拣对象或目标而追加 `MappingFailed` 并暂停。`Pending` 不是 OK，也不允许省略对应分拣动作。
- `UnknownHeld` 只用于 PLC 物理动作结果未知、断联、读回超时或 `connectionEpoch` 变化，并沿用发生未知动作的 `WholeTrayWorkflowStage`；它保持设备占用并禁止自动重发。检测端口的算法超时或通信断联按 `Detection` 阶段有限重试并收敛为带异常证据的 Pending，不伪装成 PLC `UnknownHeld`。
- 人工核对属于产生异常的业务阶段；它只能确认新的事实或授权恢复，不能将未完成阶段改写为完成。人工取盘确认属于 `ManualTrayRemovalConfirmation`，只能在 `ObservedUnlocked` 后由 Host 接收。
- `ManualTrayRemovalConfirmation` 通过 Host 唯一路由 `POST /api/v1/station01/runs/{runId}/manual-removal-confirmations` 接收。请求体只有 `requestId`、`expectedRevision`、`reason`；Host从认证上下文取得actor/role，并从同run已提交事实取得tray、WholeTrayCompletion与ObservedUnlocked引用，服务端记录时间和结果。API受理不等于最终完成，只有确认事件、FinalSourceMatrix和`FinalUnloadCompletion`原子持久化后才算完成。007自动模拟取盘由启动时启用的受控Test联调客户端在解锁事实提交后调用，ManualActor来源为Test/Simulated而非AuthenticatedHuman或真实人工；客户确认原型不新增控件。

FR01 全部点位/方向/类型与本次用户确认的分区协议原件一致，Float32 两寄存器且字序可配。扫码、检测各拍照位、下料、翻面和取放按协议下发完整目标XYZ并读取本次实际XYZ核验；各动作的Z轴按现行协议归属，不混用。
FR02 人工放盘并满足实体启动条件后由 PLC 内部执行夹紧。上位机完成安全检查并置 `PC_System_Ready`，经人工确认后发送 `PC_Start_Cmd`；上位机不得发送 `Pallet_Lock_Cmd=1`，也不得等待协议未定义的独立夹紧命令或应答。发送启动命令后，上位机只读取 `Pallet_Lock_Status`：仅当返回 `1` 时认定夹紧完成并允许流程继续；返回 `2` 时报警锁停；在规定观察窗口内保持 `0` 或出现其他未定义值时，按夹紧未完成处理并进入超时/异常流程。只有整盘检测、分拣和下料准备（移动到下料位）全部完成后，Host 才能生成 `WholeTrayCompletion` 并发送 `Pallet_Lock_Cmd=0` 解锁；写入后必须读取 `Pallet_Lock_Status=0` 才能认定解锁完成并进入人工取盘确认，写入失败、断联、观察超时或状态仍为 `1` 时保持设备占用并进入人工处置。人工取盘确认只能生成后续 `FinalUnloadCompletion`，不得成为解锁前置条件。第一工位准备完成、检测完成或工位移交阶段不得提前解锁。PLC 不写 PC 信号。

FR02 夹紧观察窗口复用已存在的版本化预算 `BusinessBudget.businessMs.clampCompletion`（单位 ms，正整数且至少 1）；结构来源为 `../001-station01-public-preparation/contracts/budget.schema.json`，随本次运行配置快照冻结并保留 id/version/purpose/source。计时从本次启动受理事实提交后进入 `WaitingClamp` 开始，与 `businessMs.plcAcceptance` 的启动受理期限分离；按既有 `ResponseBeforeDeadline` 规则，仅截止前收到同一连接代次的状态 `1` 才能继续，达到或超过窗口仍未满足时记录 `ClampTimeout/UnknownHeld` 并保持占用；状态 `2` 立即报警锁停，断联或代次变化进入设备未知处置，不等待窗口耗尽。003历史VirtualPlc测试使用 `../001-station01-public-preparation/examples/budgets.virtual-plc.json`（`s01-budget-virtual-plc`，v1.0.0，purpose=Test）的5000 ms夹紧窗口；007须另用覆盖指定采集/算法延迟的新版本Test预算，并保留适用夹紧门禁。此值不是生产默认值，不硬编码进流程，不推定真实设备参数已批准。

FR03 心跳持续翻转/响应；连续 3 秒无有效翻转时必须锁定动作、公开受限/报警状态并禁止自动续跑，重连与复位也不得自行恢复执行。验证证据必须包含心跳时间序列、3 秒期限、锁动作/报警状态和重连后仍未自动续跑的状态记录。
FR04 公共3D及产品检测按既有确认的检测周期执行：固定XYZ、本次XY=1和检测Z=2及实际XYZ匹配后置Inspection_Status=1，再清移动命令0；采集、算法及必要记录完成后置2，核验本轮检测Z_Reset_Status=2后清Inspection_Status=0。F按§3.1.7使用命令5、Scan_Target_Z与3/4及扫码Z复位，不沿用检测1/2。下一有效运动受理才清旧到位/复位并更新轴归属。命令3翻面和命令4下料分别按§3.1.5/§3.1.6使用Grab_Target_Z(000B)及本次到位/实际XYZ判据，不套检测Inspection周期或额外创造抓取Z复位握手；命令4不再采用XY-only。软件operation/connection epoch关联不是新增PLC寄存器。派生细节见[008时序](../008-recipe-driven-inspection/sequences.md)，来源差异见其本轮追溯记录。
FR05 每次第一工位 3D/F 均必须执行正式握手；F 失败必须锁停且不得加载或绑定配方，只有本次运行取得唯一 F 码成功事实后才允许产品配方加载/绑定。验证证据必须同时覆盖失败锁停、重复/歧义码拒绝和唯一成功后的 plan/bind 顺序。
FR06 配方 Provider、坐标文件、PLC Provider/网络/字序可配置；不按配方产品名分支。Recipe_ID 仅 HMI 显示。当前四面配方范围服从008及需规11.3的3AB＋1CD或3CD＋1AB规则，一面两面不变；不新增全排列实跑门槛。
FR07 故障、恢复、状态和必要证据必须可查询。旧任务重新检测或报废只能由受控人工决策产生，决定必须记录 actor、时间、原因、原任务和证据引用；Host 重启、连接恢复或普通复位不得自动重检、自动报废或自动重放已完成动作。
FR08 算法失败、超时、未配置、未接入或无有效判定在有限重试后必须为受影响工件形成可追溯的 `Pending` 结果，并按冻结 `RecipeRunPlan` 经正式 PLC 动作端口分拣到 Pending 区；其他工件继续。Detection 算法超时在首次调用之外最多重试 2 次（总计最多 3 次尝试），退避 2/5 秒，单阶段总期限 按冻结RecipeExecutionBudget确定的且不得重置。对象身份、位置或目标无法唯一映射时仍按 `MappingFailed` 暂停，不得把算法异常、映射失败或 Pending 转为 OK、默认成功或省略分拣动作。
FR09 Host 必须提供上述唯一路由的受控取盘确认 API；仅在已核验同一运行/料盘的 `WholeTrayCompletionReference`、已提交 `ObservedUnlocked` 事件及匹配revision后接受确认，并持久化认证身份、幂等键、服务端确认时间/结果、原因、渠道/来源和事件引用，原子生成不可变 `FinalUnloadCompletion`。007受控客户端的自动模拟确认记录Test/Simulated，不写成真实人工操作。当前客户确认原型不新增或改写控件；生产页面入口须等待新原型版本、哈希和明确批准。
FR10 本期最小端到端验收必须由真实 Host 进程通过正式 Modbus TCP 接入仓库既有独立 VirtualPlc 进程，并使用正式业务端口、同一状态机和真实 SQLite 完成全链路持久化；相机、光源和算法可使用明确标记 `Simulated/Test` 的正式适配器，人工确认由测试/联调客户端调用 Host API。进程内全模拟只作为规则/合同/集成测试证据，不替代该端到端验收，也不得宣称真实 PLC、相机、算法、节拍或生产兼容性通过。
FR11 第一工位完整主流程必须以同一 `runId/trayId`、冻结配置和可追溯身份连续执行 001 启动夹紧、3D 高度、F 单次扫码、配方计划/绑定、持久化 handoff 及 003 整盘闭环；只有已提交且身份一致的 handoff 才能由 Host 自动触发 Detection。不得再次人工启动、直接构造 003 起始状态、预造冻结计划或绕过公共准备步骤。
FR12 本期独立进程端到端验收至少覆盖五类场景：正常完整闭环；算法有限重试失败后以 Pending 继续并完成；`WholeTrayCompletion` 前拒绝解锁且 `ObservedUnlocked` 前拒绝人工确认；Sorting、UnloadPreparation 或 UnlockObservation 的 PLC 动作发生断联/连接代次变化时进入 `UnknownHeld`、保持占用且不自动重发；Host 重启后只按已提交事件恢复、已完成物理动作不重放且在途无终态动作不自动执行。任一场景缺少实际进程、协议、数据库或日志证据均不满足本期完成条件。
FR13 临时通信错误在首次调用之外最多重试 3 次（总计最多 4 次尝试），退避 1/2/4 秒；所有尝试和退避共用单阶段 按冻结RecipeExecutionBudget确定的总期限且不得重置。动作意图及对应投影、反馈事件及对应阶段投影分别使用设备/算法调用前后的短事务；Detection、UnloadPreparation、Sorting 的 Completed 事件均已提交后，另用独立聚合短事务重新核验身份、plan revision、source/quality 和未解决终态，并原子写入 `WholeTrayCompleted`、`WholeTrayCompletion` 和整托投影；`ManualTrayRemovalConfirmed` 与 `FinalUnloadCompletion` 使用同一个后续短事务。不得跨 PLC、相机或算法调用持有事务；可能已派发的 PLC 物理动作永不自动重发，必须进入 `UnknownHeld`。
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
3. **正常终点**：依序真实执行 Detection、UnloadPreparation、Sorting，提交三个阶段完成事实和 `WholeTrayCompletion`；随后写 `Pallet_Lock_Cmd=0` 并读回同一连接代次的 `Pallet_Lock_Status=0`，提交 `ObservedUnlocked`；最后由测试/联调客户端调用受控 Host API，持久化操作员确认和不可变 `FinalUnloadCompletion`。只有上述记录均已提交才判定成功。
4. **五类最小场景**：独立进程证据必须覆盖正常完整闭环、算法失败转 Pending 后继续、完成证据前拒绝解锁/人工确认、PLC 动作断联或连接代次变化进入 `UnknownHeld` 且不自动重发、Host重启后只重建持久故障查询，双端复位/初始状态后新轮完整执行并关联旧轮。
5. **设备与状态事实真实**：只以匹配设备反馈和已提交事实完成；保持3秒心跳、F3/4与检测1/2对应轴复位；下料按新版FR16—18。当前自动翻面按20260925分区协议§3.1.5：当前零件XYZ写0003/0005/000B→命令3→本次到位/实际XYZ核验→清命令→目标面→状态2及实际面匹配→Flip_OK=1→状态0→Flip_OK=0；料盘不翻面。逐个处理仍需后续面的实体，整体不按部位重复；同目标面的不同实体分别闭环。普通翻面协议已定义，受限项仅为实际代码能力、合法点位配置或真机标定。 同盘Sorting先源XYZ(0003/0005/000B)、命令1，状态2只取料成功；清命令后提交真实源槽位Sorting_Part_Index及目标XYZ、命令2；状态3放料完成后清命令、Sorting_OK=1，观察状态0再清ACK并提交完成。4失败、5满盘、命令3满盘报警。Sequence不是槽号；普通OK无需搬运不发取放。源/目标均属当前盘，预留与在途保留至可靠完成。
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

- **FR19（008对齐）**：完整Detection通过应用执行入口及唯一运动准入消费全部适用定位、采集、逐图分析、复位、同面融合、换面/逐面合法目标续接与E步骤，不只执行Capture。F握手已按授权写入指定协议§3.1.7：命令5采用公共XY及Scan_Target_Z，到位后3并清移动命令，采集解码结束且保存后4，扫码Z本轮复位成功后清0；F唯一绑定/配方一致/保存成功另外决定放行。旧1/2代码待修改，E仍待B01-E，取放/旋转/产品轴依B02—B04。真实槽位及源目标不得用序号或0,0代替。
- 采用上述新版FR16—18及独立保存门禁；普通OK不等待全部分拣能力。
- 成功条件：按[008覆盖矩阵](../008-recipe-driven-inspection/coverage-matrix.md)保留Q01—Q22编号及历史证据，按008 SC-001验证当前一面/两面及四面两类3＋1流程从正式前端到Final、适用C01—C08和F1—F6。取消每M固定两配方及固定按冻结RecipeExecutionBudget确定的作为新验收门槛。任务与证据状态仍未更新。

当前任务归属与顺序：参见[本功能tasks](tasks.md)文末S0—S5增量及[008任务](../008-recipe-driven-inspection/tasks.md)首批集合。共享实现只登记一个所有者；历史版本/完成证据保留原范围，最新Q/C/F规则不倒填旧任务。

新版逐面续接保留初始公共3D/F绑定、同盘/对象及历史面结果；翻后不重采3D。按executionStageId/localFace解析该面独立目标及measurementRef/coordinateConfigVersion，deviceConnectionEpoch只关联设备反馈。初始测量可经明确逐面映射复用，不能直接复用上一面XYZ、伪造round2或删除身份校验。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。


## 新版直接要求（2026-09-26）

当前自动翻面按20260925分区协议§3.1.5：当前零件XYZ写0003/0005/000B→命令3→本次到位/实际XYZ核验→清命令→目标面→状态2及实际面匹配→Flip_OK=1→状态0→Flip_OK=0；料盘不翻面。逐个处理仍需后续面的实体，整体不按部位重复；同目标面的不同实体分别闭环。普通翻面协议已定义，受限项仅为实际代码能力、合法点位配置或真机标定。

同盘Sorting先源XYZ(0003/0005/000B)、命令1，状态2只取料成功；清命令后提交真实源槽位Sorting_Part_Index及目标XYZ、命令2；状态3放料完成后清命令、Sorting_OK=1，观察状态0再清ACK并提交完成。4失败、5满盘、命令3满盘报警。Sequence不是槽号；普通OK无需搬运不发取放。源/目标均属当前盘，预留与在途保留至可靠完成。

人工区占用1禁止运动；人工完成且既有安全成立后确认1→占用0→清确认；面确认/持件恢复仍按B07局部输入。当前F3/4已实现的未变部分复用，E不自动外推。

## USR-20260926-E：两端坐标与取放证据需求

本次依据USR-20260926-E（六项问题及四面范围确认）。协议原件为E:/dzk/gaode-1/高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx；正文为上下位机信号分区版，修订日期2026年9月25日，SHA-256=405ac9ee2ae2d765951d9f523dc7195cc77f6cd1f38dbc0ad144a0013586c519。Office内置修订号2、修改时间2026-09-24T04:06:16Z仅为文件元数据，不代替正文修订日期；用户已确认该实际文件，不因口述名称差异重复确认。协议原件只读。

问题1—5为用户本地已观察、根因待运行核验的问题；不能凭源码存在逻辑直接关闭，也不能未经复现认定全部为通信缺漏。本次仅记录需求与验收，不声明复现、修复或运行通过。问题6为已确认范围变更，不是尚待复现的通信故障。

各动作按现有协议下发完整目标XYZ，读取本次实际XYZ并校验，使用该动作规定的Z轴；目标、实际坐标及关键握手须按运行、对象、面、步骤和动作关联查询。变化日志未显示相同值，不等于未实际读写；应以两端实际请求、接收及反馈证据区分目标或通信缺漏、实际反馈缺漏与日志显示省略。VirtualPlc必须实际接收指令并产生模拟反馈，Host不得伪造反馈。

问题1（扫码）、问题2（单面检测）、问题3（第二拍照位）、问题4（下料）对应FR01/FR04/FR15及适用FR16/FR19；按实际请求、VirtualPlc接收、实际XYZ反馈及本次校验逐项验收。FR15必要持久诊断须能区分未写入、写入失败、反馈缺失及变化日志省略；值相同仍可查完整动作上下文，不要求无限全量轮询日志。问题5沿用FR19及同盘Sorting规范、§3.1.6：先本次取料成功状态2，后清命令、提交放料槽号及目标XYZ，再放料命令；缺可靠反馈不进入放料。

固定四面流程仅保留三个AB＋一个CD或三个CD＋一个AB；全同组和二比二组合退出当前业务配方及必做验证范围。一面、两面规则保持；少数组所在面及执行位置由配方决定，不限定最后一面，不新增全排列实跑要求。成组成员和整体检测部位中的四面对象也须检查适用性；成组、整体、E扫码、旋转和人工换面等独立场景保留。

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


## 2026-09-27 本机复测XYZ名称确认（实现前同步）

最新用户确认信号名为 `XYZ_Move_Cmd`（4x0001）及 `XYZ_Pos_Confirmed`（4x0002）。此确认覆盖既有文本中的XY名称；工作区来源Word仍为原SHA，原件只读，不能把该用户增量冒称Word已改。仅公开名字统一，地址/功能码/值/方向不变；运动目标X/Y/适用Z必须全部实际写入，到位须读回本动作实际XYZ核验。不能由changes数值变化是否出现推断某轴是否发送。

按原3.1.5逐零件翻面及Flip_OK清零、3.1.6取料成功2后清命令并提交真实槽位/放料XYZ、放料成功3后Sorting_OK握手清零、3.1.7检测/F操作结束后对应Z复位及清零继续验收。F结束4不推出解码成功；UnloadPreparation命令4、普通OK留原位、NG/Pending同盘处置及特殊必要搬运不变。

此子修复由既有003映射/监控/动作验收任务承接；不追加重复任务或改变勾选。实现范围：VirtualPlc公开点名和监控消费者/必要测试，后端数值寄存器接线不变。证据目录见xyz-sorting-deployment/active-retest.json；实施及验证完成后另写同目录时间戳报告，不覆盖历史通过。
