# 最新协议验收记录

唯一接口依据：高德_文档/PLC与上位机通信接口协议_最新版.docx。
修改范围：E:/dzk/gaode-1。未上传 GitHub。

## T001 跨 feature handoff 文档门禁（2026-09-23）

- 用户已有限授权仅对齐001中与 `s01-handoff/2.0` producer直接相关的 spec、contract、plan、tasks；已同步同一 run/tray、冻结计划、证据引用、提交后同 Host 自动续接及 v1 兼容规则。
- 003合同版本保持 `s01-handoff/2.0`、`station01-main-flow-api/1.0`、`station01-status-notification/1.0`、`station01-component-source-matrix/1.0`。
- `GATE-001-HANDOFF-V2` 的共享文档合同门禁已满足。该结论不表示T024 producer/consumer代码接线、T026测试或五场景E2E已完成。
- 006仍是非阻塞独立消费者；本次未修改006、`frontend/src`、`frontend/tests`、客户原型或001其他需求。

## T002–T003 组件来源矩阵领域规则（2026-09-23）

- 实现六组件不可变矩阵、ReadyForUnlock/Final里程碑必需性、ManualActor `NotYetRequired`、Missing/Unknown/Unverifiable阻断及来源范围派生。
- 任一Virtual/Simulated/Test组件固定派生 `SoftwareLoopOnly`；全部真实组件也只派生 `ProductionCandidateNotAccepted`，不产生生产验收结论。
- 命令：`dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-restore --filter FullyQualifiedName~ComponentEvidenceMatrixTests --logger "trx;LogFileName=t002-component-matrix.trx"`
- 结果：退出码0，8/8通过；证据：`backend/tests/Gaode.Rules.Tests/TestResults/t002-component-matrix.trx`。

## T004–T005 重试、期限与物理未知分流（2026-09-23）

- 新增按故障类别分流的 `StageRetryPolicy`：通信总尝试4次和1/2/4秒退避、算法超时总尝试3次和2/5秒退避、共享冻结120秒deadline。
- Detection通信/算法耗尽或期限先到收敛为Pending；PLC仅派发前可通信重试，可能已派发直接UnknownHeld且零自动重发；attempt/plannedAt/deadline/error/operationId由决策和阶段证据合同携带。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~StageRetryPolicyTests --logger "trx;LogFileName=t004-stage-retry.trx"`
- 结果：退出码0，6/6通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t004-stage-retry.trx`。

## T006–T007 受控旧任务恢复决定（2026-09-23）

- 实现不可变 `ControlledRecoveryDecision`，保留 request/revision、原任务与原 operation/stage、run/tray、认证 actor/role、时间、原因和证据引用。
- 授权策略仅接受 Host 已鉴权的人工决定；重启、PLC 重连和普通复位均明确拒绝。决定只授权后续用例，不生成算法、PLC 或完成事实。
- 命令：`dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-restore --filter FullyQualifiedName~ControlledRecoveryDecisionTests --logger "trx;LogFileName=t006-controlled-recovery.trx"`
- 结果：退出码0，7/7通过；证据：`backend/tests/Gaode.Rules.Tests/TestResults/t006-controlled-recovery.trx`。

## T008–T009 启动身份与版本化上下文（2026-09-23）

- 实现 `station01-start-run-context/1.0` 严格解析，要求外部提供 tray/station/line/scenario/occupiedSlots/purpose；空身份、重复点位、错误版本和空 trayId 均拒绝，不临时生成 trayId。
- `WorkflowIdentity` 冻结 run/tray、actor、启动时间和三类配置 revision；`RunSnapshot.Next` 保留同一身份，并提供 run/tray scope 核验。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~StartRunContextTests --logger "trx;LogFileName=t008-start-run-context.trx"`
- 结果：退出码0，8/8通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t008-start-run-context.trx`。

## T010–T011 第一工位主流程 SQLite 模型（2026-09-23）

- 新增受控迁移 `202609230001_Station01MainFlow`，保存阶段 plan/deadline、非终态 v2 handoff、六组件来源矩阵、受控恢复决定、三个阶段完成事件引用、source matrix 引用和 persisted revision。
- 模型通过唯一键、受限外键、七年保留 check constraint 和 after-save `Throw` 固定不可变引用；Host 兼容性探针要求三项迁移齐全，只读检查旧库并拒绝，不自行迁移。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~Station01MainFlowModelTests|FullyQualifiedName~ModelContractTests|FullyQualifiedName~StageEventStoreTests" --logger "trx;LogFileName=t010-main-flow-model.trx"`
- 结果：退出码0，12/12通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t010-main-flow-model.trx`。

## T012–T013 `s01-handoff/2.0` 消费合同（2026-09-23）

- 003 侧定义完整 v2 handoff 和独立非终态持久表；只读查询同时核验 run/tray、writeId、revision 和 payload digest，内存通知不能替代提交事实。
- DetectionRequest 只能由已核验的持久 handoff 构造；空证据、错误 run/tray/plan 或缺少提交记录均拒绝。测试使用 003 自有 SQLite fixture，未依赖或修改 006。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~PublicPreparationHandoffV2Tests|FullyQualifiedName~Station01MainFlowModelTests|FullyQualifiedName~ModelContractTests" --logger "trx;LogFileName=t012-handoff-v2.trx"`
- 结果：退出码0，9/9通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t012-handoff-v2.trx`。

## T014/T018 心跳锁停与 Restricted 投影（2026-09-23）

- 可控时钟互锁在 2999ms 保持有效、3000ms 原子锁动作并升报警；重连/普通复位不清闩，人工恢复核对后仍要求新命令准入，不自动续跑。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~HeartbeatInterlockTests --logger "trx;LogFileName=t014-heartbeat-interlock.trx"`
- 结果：退出码0，4/4通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t014-heartbeat-interlock.trx`。

## T015/T019 XYZ 与 Inspection/Z_Reset 时序（2026-09-23）

- inspection 合同现在携带同一 run/operation/connection epoch/XYZ；不匹配时拒绝采集。完整时序记录 `Inspection_Status=1→2→0`，仅在 Z reset 成功为2后清零；下一移动将 Test PLC 的 Z reset 事实重置并建立新关联。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~InspectionHandshakeSequenceTests|FullyQualifiedName~SimulatedPlcTests.InspectionHandshakeRunsOneWayAndClearsOnlyAfterResetSuccess" --logger "trx;LogFileName=t015-inspection-sequence.trx"`
- 结果：退出码0，3/3通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t015-inspection-sequence.trx`。

## T016–T017 F 门禁、配置冻结和夹紧预算（2026-09-23）

- F 门禁仅在 3D/F 握手完成、算法成功且唯一 F 已解析时允许后续 plan→bind；失败、无响应、重复/歧义和格式无效均锁停且不调用计划操作。
- 配置测试区分控制能力错误与算法未就绪，禁止 Production 隐藏 Simulated 回退；冻结版本/用途/能力/`businessMs.clampCompletion`。夹紧策略验证 4999/5000ms `ResponseBeforeDeadline` 边界、状态2、未定义状态、断联和 epoch 变化。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~ThreeDAndFRecipeGateTests --logger "trx;LogFileName=t016-f-recipe-gate.trx"`；结果7/7通过。
- 命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~Station01RunConfigurationTests|FullyQualifiedName~RecipeRunPlannerTests|FullyQualifiedName~StartClampStepTests" --logger "trx;LogFileName=t017-run-configuration.trx"`；结果13/13通过。

## T020–T022 F 后计划绑定与唯一启动入口（2026-09-23）

- `StartPublicPreparation` 只在两次正式 inspection 握手完成、F 算法成功且唯一 F 解析有效后生成冻结 `RecipeRunPlan`；测试解析值通过既有 `TestTrayCodePolicy` 进入同一配方目录逻辑，无硬编码目录别名或失败回退。
- 配方绑定遵守“意图短事务提交→正式 `IPlcRecipePort` 调用→事实短事务提交”；`SimulatedPlc` 仅作为明确 Test 端口记录绑定事实。格式无效、无码、冲突或算法失败均不 plan/bind/handoff，并保持锁停。
- 唯一 `POST /api/v1/station01/runs` 在注册 command/run 前严格解析版本化 `StartRunContext`，冻结调用方提供的 tray/station/line/scenario/slots/purpose；缺失上下文返回400，且 SQLite 无临时 run、PLC 无动作。有效请求返回202引用，不等待流程结束。
- 合同命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~ThreeDAndFRecipeGateTests|FullyQualifiedName~RecipeRunPlannerTests|FullyQualifiedName~Station01RunConfigurationTests" --logger "trx;LogFileName=t020-t021-gates.trx"`；退出码0，12/12通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t020-t021-gates.trx`。
- plan/bind 集成命令：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~NormalPublicPreparationTests.RealElapsedSimulationCommitsHandoffToRealSqliteAndMedia" --logger "trx;LogFileName=t020-t022-plan-bind-integration.trx"`；退出码0，1/1通过；证据：`backend/tests/Gaode.Integration.Tests/TestResults/t020-t022-plan-bind-integration.trx`。断言包括一次配方绑定、配方ID、计划/绑定引用，以及 intent revision 小于 fact revision。
- API 命令：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~StartQueryTests" --logger "trx;LogFileName=t022-start-entry.trx"`；退出码0，15/15通过；证据：`backend/tests/Gaode.Integration.Tests/TestResults/t022-start-entry.trx`。
- 本批仅证明入口至现有持久化公共准备边界；T024–T026 的 v2 producer/自动续接尚未完成，不声明 Detection 或 FinalUnloadCompletion 已完成，也不代表真实设备验收。

## T023 v2 handoff 持久消费门禁（2026-09-23）

- Detection consumer 改为持有 `IStageHandoffQuery` 并在创建请求时自行重新读取 SQLite；公开调用不再接受调用方构造的“已提交”包装对象，因此预造 `DetectionRequest`、内存通知或非空引用不能替代提交事实。
- 文件 SQLite 集成测试先证明无持久 handoff 时，即使预造 request 自身字段合法也被拒绝；随后在单一短事务中提交 run/write/v2 handoff，才可按同一 run/tray/plan revision 生成 DetectionRequest，并核对 3D/F 媒体及结果/绑定证据。handoff 保持非终态。
- 合同命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~PublicPreparationHandoffV2Tests" --logger "trx;LogFileName=t023-handoff-v2-contract.trx"`；退出码0，4/4通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t023-handoff-v2-contract.trx`。
- 集成命令：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~PublicPreparationHandoffV2IntegrationTests" --logger "trx;LogFileName=t023-handoff-v2-integration.trx"`；退出码0，1/1通过；证据：`backend/tests/Gaode.Integration.Tests/TestResults/t023-handoff-v2-integration.trx`。
- T024 producer/consumer 自动续接尚未实现；本节不把 fixture 提交写成 producer 已接线，也不声明 Detection 已执行。

## T024–T026 v2 producer、非终态查询与 US1 门禁（2026-09-23）

- `CompletePublicPreparation` 不再用旧 `Complete` 写入把 run 提前终结；现在以 `HandoffV2` 短事务原子更新非终态 run revision、write 和 `PublicPreparationHandoffsV2`。嵌入 digest 由去除 digest 字段后的完整 v2 载荷计算并在读取时复核。
- 只有提交回执成功后，`StartPublicPreparation` 才调用同 Host 的持久化 consumer 重新读库并构造 DetectionRequest；无第二启动入口。当前批次仅准备真实请求，后续 Detection 端口执行属于 T033–T039，因此没有伪写 Detection 完成。
- `GET /runs/{runId}` 合并持久 run/v2 handoff 投影，返回 `RecipeState=Bound`、plan revision、binding/handoff 引用和 `WholeTaskState=HandoffReady`；`HandoffReady`、202 和 DetectionRequestPrepared 均保持 `Terminal=None`，绝不映射为 `FinalUnloadCompleted`。
- `GET /runs/{runId}/handoff` 返回版本化 `s01-handoff/2.0` 与 ETag；旧 v1 terminal handoff 表不再作为当前主流程成功依据。
- `GATE-001-HANDOFF-V2`：共享 001/003 合同对齐已在 T001 有限授权范围内满足；本批完成实际 producer/consumer 接线。未修改001其他需求、006、frontend 或客户原型。
- US1 合同门禁：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~HeartbeatInterlockTests|FullyQualifiedName~InspectionHandshakeSequenceTests|FullyQualifiedName~ThreeDAndFRecipeGateTests|FullyQualifiedName~Station01RunConfigurationTests|FullyQualifiedName~RecipeRunPlannerTests|FullyQualifiedName~StartClampStepTests|FullyQualifiedName~StartRunContextTests|FullyQualifiedName~PublicPreparationHandoffV2Tests" --logger "trx;LogFileName=t026-us1-contracts.trx"`；退出码0，38/38通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t026-us1-contracts.trx`。
- US1 SQLite/API 集成门禁：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter "FullyQualifiedName~NormalPublicPreparationTests|FullyQualifiedName~StartQueryTests|FullyQualifiedName~PublicPreparationHandoffV2IntegrationTests" --logger "trx;LogFileName=t026-us1-integration.trx"`；退出码0，18/18通过；证据：`backend/tests/Gaode.Integration.Tests/TestResults/t026-us1-integration.trx`。
- 结论仅为“唯一入口→真实公共准备→plan/bind→committed v2 handoff→同 Host consumer 准备 DetectionRequest”；尚未声明三阶段、解锁、人工确认或 FinalUnloadCompletion 完成，也不代表真机/生产验收。

## T027–T039 三阶段纵向切片（2026-09-23）

- Detection 使用冻结 handoff/plan 身份、输入引用、attempt 和不可重置 deadline。通信错误总计4次并按1/2/4秒退避；算法超时总计3次并按2/5秒退避；耗尽或期限先到写入保留原错误和证据引用的逐对象 Pending，再经同一正式 mapper 生成 Pending 分拣动作。缺失、重复或歧义仍写 `MappingFailed`，不部分派发。
- `LatestProtocolStageActionAdapter` 复用 `LatestProtocolPlcDevice` 唯一业务 Modbus 会话。Sorting 固定执行 4x0020→4x0021→读取4x0022→Cmd=0/Status=0；UnloadPreparation 固定 MoveCmd=4/PosConfirmed=1→Cmd=0/Status=0；UnlockObservation 只在持久 WholeTrayCompletion 引用存在时写 Cmd=0 并读取 Status=0。命令写后断联、超时或 epoch 变化直接 `UnknownHeld`，合同测试确认物理命令计数保持1。
- VirtualPlc 现有实现已通过真实 Modbus TCP 验证心跳、夹紧、运动、分拣、下料、解锁、SortingFailure、TCP重连及 Host connection epoch 变化；未发现本期协议缺口，故 T036 为 `NotNeeded`，未让 VirtualPlc 生成 Detection、业务完成、人工确认或数据库事实。
- Host 组合根在 `VirtualPlcIntegration` 中显式绑定独立 VirtualPlc 的正式 Modbus 设备端口与明确标记的 `SimulatedDetectionPort`；Production Detection 未接入时返回 `NotIntegrated`，无隐藏回退。FullSimulation 原有可控/实时模拟绑定得到保留。
- committed `s01-handoff/2.0` 后由同一 Host 自动执行 Detection→Sorting→UnloadPreparation。每个意图/反馈经 `StageEventStore` 独立 SQLite 短事务保存；正常软件切片停在 `ThreeStagesCompleted`，仍为非终态且不会提前创建 WholeTrayCompletion。
- 异常 SQLite 集成覆盖：Detection通信耗尽落 `PendingRecorded` 且正式执行 Pending 分拣；歧义结果落 `MappingFailed` 且无 Sorting 派发；可能已派发的 PLC 断联落 `UnknownHeld`、运行进入 `RecoveryRequired` 且无 WholeTrayCompletion。
- 协议/工作流合同命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~DetectionRetryAndPendingTests|FullyQualifiedName~RecipeSortingMapperTests|FullyQualifiedName~DetectionAdapterSourceTests|FullyQualifiedName~PlcStageActionPortContractTests|FullyQualifiedName~LatestPlcProtocolTests|FullyQualifiedName~VirtualPlcLatestProtocolTests|FullyQualifiedName~ThreeStageWorkflowExecutorTests" --logger "trx;LogFileName=t027-t037-us2-contracts.trx"`；退出码0，37/37通过；证据：`backend/tests/Gaode.Contracts.Tests/TestResults/t027-t037-us2-contracts.trx`。
- VirtualPlc 定向证据：`t030-virtualplc-protocol-r7.trx`，退出码0，2/2通过；阶段动作定向证据：`t028-t035-stage-adapter-r3.trx`，退出码0，40/40通过。
- 正常 SQLite/TCP 集成：`t039-three-stage-mainflow-r3.trx`，退出码0，1/1通过；Pending/MappingFailed/UnknownHeld 集成：`t039-three-stage-exceptions-r2.trx`，退出码0，1/1通过。均位于 `backend/tests/Gaode.Integration.Tests/TestResults/`。
- 本批证据含 Virtual PLC 和 Simulated Detection，只证明 `SoftwareLoopOnly` 后端三阶段软件闭环；不代表真机、真实算法或生产验收，也未声明解锁、人工取盘或 FinalUnloadCompletion 完成。

## 已执行的校验

- Debug 构建：0 警告、0 错误；Release 构建：0 警告、0 错误。最后一次构建在 Provider 配置切换校验修改后完成。
- Contracts：74/74，backend/tests/Gaode.Contracts.Tests/TestResults/stage10-contracts-final-rerun.trx。
- Rules：28/28，backend/tests/Gaode.Rules.Tests/TestResults/stage10-rules.trx。
- Integration：41/41，backend/tests/Gaode.Integration.Tests/TestResults/stage10-integration-final.trx。
- Contracts 首次整套运行出现 1 个既有异步采集时序波动，单项重跑与整套重跑均为 74/74；该波动未涉及 PLC。Provider 配置校验定向测试 3/3 通过。

## 独立进程验收（2026-09-22）

脚本：`python -B scripts/verify-latest-plc.py --empty-store <E:\dzk\gaode-1 内已准备的空测试存储>`。
结果目录：`artifacts/plc-latest/eb617286b2d743bb94c4633fd62c81df`，`passed=true`。验证了 Host↔VirtualPlc 心跳、PC_System_Ready/PC_Start 顺序、15/15、3D/F XYZ、Inspection/ZReset、F 后配方绑定、Host reset 和 PauseHeartbeat 超时锁停。

## 独立进程发现与修复

2026-09-22：正确请求使用 contextJson，错误的 scenarioJson 导致初始上下文保存失败，未发设备动作。
启动握手：PC_System_Ready 在受理后的安全检查阶段置位，不能在单纯 TCP 连接时无条件置位。
Host 检测到停止响应、VirtualPlc 通信超时后锁停；人工 reset 后通讯可恢复，不能自动继续任务。
最新版握手要求 Inspection_Status=1 先于 XY_Move_Cmd 清零，已调整。
3D 结束后转 F 时检测状态缓存滞后造成误拒绝；改为下一移动发送前检查本轮 PLC 读取值，独立进程回归已通过。
历史完整运行记录仅证明旧实现，不计入最新版验收。

## SpecKit 入口

SpecKit 独立入口 `scripts/verify.ps1 -WaitSeconds 0` 最终通过；证据：`artifacts/workflow/standalone-1191b6c9ffc54be896b4b1bec51a0cd2/verify-01/verification.json`。第一次入口因既有异步模拟算法枚举竞争出现 73/74，未涉及 PLC，第二次重跑通过。

## 待完成

Provider=Real 的现场真实 PLC、相机/光源/算法和真实配方目录仍需现场资料；代码与自动化模拟验收已完成。

## 第一工位流程冲突修复（2026-09-22）

问题基线：旧测试 `StartWaitsForExplicitPhysicalButtonBeforeClampCompletes` 和
`CommittedStartIntentPrecedesDispatchAndPhysicalButtonIsExplicit` 要求
`PressPhysicalButton`、`ButtonPressed`、`ClampStarted`、`ClampCompleted`，与最新版协议没有这些 Host↔PLC 信号的事实不一致。

修复证据：

- `StartClampStep` 只记录 `PC_Start_Cmd` 受理并读取 `PalletLockStatus`；启动路径没有 `Pallet_Lock_Cmd=1` 写入。
- 状态 `1` 才进入区域配置/第一条移动；状态 `2`、观察超时、断联或连接代次变化形成 `Unknown/Held` 并请求停止。
- `SimulatedPlc` 在启动受理后内部将 `PalletLockStatus` 从 `0` 变为 `1`，不发出按钮或夹紧完成协议外事件。
- `PalletUnlockStep` 当前仍接收历史 `wholeTrayComplete` 参数，尚未完成 T037 的证据链替换；当前 Host 尚未接入真实整盘编排，因此未生成解锁验收证据。

实际命令与结果：

- `dotnet build backend/Gaode.slnx --no-restore`：通过，0 警告、0 错误。
- `dotnet build VirtualPlc/VirtualPlc.csproj --no-restore`：通过，0 警告、0 错误。
- 完整 `dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-build` 复跑为 42/43；失败为既有暂停竞态 `PublicContractSmokeTests.PauseAndRecoveryCheckRequireExpectedRevisionAndReturnStructuredReceipts`，不涉及第一工位 PLC 夹紧/解锁代码，因此未将其记为本次功能通过。
- `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore`：85/85 通过。
- `dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-build`：39/39 通过。
- `dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-build --filter FullyQualifiedName~NormalPublicPreparationTests`：2/2 通过。
- `dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter FullyQualifiedName~NormalPublicPreparationTests|FullyQualifiedName~HostLifecycleTests|FullyQualifiedName~AlgorithmAdapterFailureTests`：14/14 通过。
- `dotnet build VirtualPlc/VirtualPlc.csproj --no-restore`：通过，0 警告、0 错误。

以上均为进程内模拟/合同验证，不代表真实 PLC、现场按钮、相机或算法验收通过。

## 001/003 澄清后的测试口径（待后续实现）

旧测试名称 `StartWaitsForExplicitPhysicalButtonBeforeClampCompletes`、
`CommittedStartIntentPrecedesDispatchAndPhysicalButtonIsExplicit` 中的独立按钮/夹紧完成断言不再是当前合同；它们应在后续任务中改为验证：

- `PC_Start_Cmd` 受理后只读取本次连接代次的 `Pallet_Lock_Status`；启动路径无 `Pallet_Lock_Cmd=1` 写入；
- `Pallet_Lock_Status=1` 才能进入区域握手和第一条移动，`=2` 锁停，观察期限内 `0`/未定义值进入超时或未知受限状态；
- `WaitingPhysicalStart`、`WaitingClamp` 仍可作为查询/通知业务阶段，但不得被解释为 `ButtonPressed`、`ClampStarted` 或 `ClampCompleted` 事实；
- FullSimulation 的 `0→1`、`2` 或保持 `0` 由 `SimulatedPlc` 按 fixture/profile 产生，并标记为 `source=Simulated`、`purpose=Test`，不作为真实 PLC 或现场按钮验收证据。

本节只更新验收口径，不表示 T035、T041-T044 或 T078-T086 已完成；完成测试修改和全量回归后再补充实际命令与证据路径。

## T040 回归收尾（2026-09-22）

根因与修复：

- 暂停请求与 `StartClampStep` 的 `AdmissionClosed` 判断发生语义混用，导致暂停中的运行被后台错误写成 `Blocked`，恢复核对版本号随之失效。`ControlLatch` 现在区分暂停、取消和安全故障；暂停期间保持等待，恢复状态提交后才释放暂停闩。
- Integration 夹具通过进程级环境变量注入测试存储和配置，xUnit 并行创建夹具时会互相覆盖环境。`Station01HostFixture` 现在串行保护环境变量和 Host 生命周期，确保每个测试使用自己的存储根和配置。
- `/status` 原先分别读取 `CurrentRun` 计算 ETag 和生成响应，运行推进时可能返回与正文不一致的 ETag。现在单次请求复用同一运行快照、主机状态和活动数。

实际命令与结果：

- 指定暂停回归：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter FullyQualifiedName~PublicContractSmokeTests.PauseAndRecoveryCheckRequireExpectedRevisionAndReturnStructuredReceipts`：1/1 通过。
- ETag 回归：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter FullyQualifiedName~StartQueryTests.QueryEtagsAreStableAndChangeWithRunRevision`：1/1 通过。
- 算法适配器回归：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter FullyQualifiedName~AlgorithmAdapterFailureTests.AdapterExceptionIsPersistedAndIndependentStepsReachLimitedHandoff`：2/2 通过。
- Contracts：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore`：85/85 通过。
- Rules：`dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-restore`：39/39 通过。
- 完整 Integration：`dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore`：43/43 通过。

本次仍未修改 `spec.md`、`plan.md`、`contracts.md`，也未改变第一工位 PLC 夹紧/解锁协议；以上结果均为软件和进程内模拟验证，不代表真实 PLC 或现场验收。






## 2026-09-22 003 实现批次证据（非最终 T034/T044）

本批次只覆盖 `003-plc-latest-protocol`，不把模拟结果写成真实 PLC 验收，也未勾选 T034/T044。

| 范围 | 命令 | 结果 | 证据 |
| --- | --- | --- | --- |
| Contracts | `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore` | 85/85 通过 | `backend/tests/Gaode.Contracts.Tests/TestResults/` 最新 TRX |
| Rules | `dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-restore` | 39/39 通过 | `backend/tests/Gaode.Rules.Tests/TestResults/` 最新 TRX |
| Integration 受影响流程 | `dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --no-restore --filter FullyQualifiedName~NormalPublicPreparationTests` | 2/2 通过 | `backend/tests/Gaode.Integration.Tests/TestResults/` 最新 TRX |
| 独立 VirtualPlc | `python scripts/verify-latest-plc.py --empty-store <受控空库>` | 通过；Host 没有接受 `Pallet_Lock_Cmd=1` | `artifacts/plc-latest/e306236fd59145dc935999ea17e828b9/result.json`、`samples.json`、`plc.log`、`host.log` |

本批次新增的 VirtualPlc 写入审计会记录成功写入和拒绝原因；`Pallet_Lock_Cmd=1` 被数据存储层拒绝，未作为启动锁紧路径。整盘检测、分拣、下料编排仍未接入 Host，因此没有生成最终 `Pallet_Lock_Cmd=0` 解锁验收证据。

### 历史结果与本批次结果

- 历史 `42/43` 是 T034 之前的回归记录，保留用于追溯，不作为本批次最终结果。
- 历史 `43/43` 是 T040 的回归收尾证据，仍按 tasks.md 保留 T040 完成状态。
- 本节的 85/85、39/39、2/2 和独立 VirtualPlc 结果是本批次新增证据，但不能替代缺失的整盘完成/解锁闭环。

## T045 设计验证计划（未执行）

本节只记录 T045 的验证设计，不把历史 42/43、43/43 或既有 VirtualPlc 结果改写为整盘完成证据。本轮未运行产品测试、未修改代码、未勾选任务。

实现后必须为每个第一工位运行保存以下证据：

- `WholeTrayStageEvent` 追加事件链：检测、分拣、`UnloadPreparation`、`UnlockObservation` 和 `ManualTrayRemovalConfirmation` 分别包含开始、受理、执行、完成或失败、超时、断联、人工核对和恢复事件；每条事件有运行/托盘/工位/产线、operation、attempt、epoch、source、quality、errorCode。
- `WholeTrayCompletion` 持久化记录及检测/分拣/`UnloadPreparation` 三个 `Completed` 事件引用；记录不可变，未提交前不得出现解锁命令。
- `UnlockObservation` 的 Cmd=0 写入、`Pallet_Lock_Status=0` 读回和 `FinalUnloadCompletion` 的 Host 人工确认记录；人工确认包含操作员、时间、确认阶段、结果和原因，且不作为写 0 前置条件。
- Host 点位写入/读取审计：启动路径没有 `Pallet_Lock_Cmd=1`；完成后写 0；读回 `Pallet_Lock_Status=0` 后才记录 `ObservedUnlocked`。
- VirtualPlc 独立进程日志、拒绝非法启动锁紧写入的原因、失败/保持锁紧/断联配置和 Host 事件关联。
- `RunSnapshot.SortingState` 与 `WholeTaskState` 的事件投影快照，证明不是固定字符串或单一完成布尔值。

### 覆盖矩阵

| 场景 | 必须证明 | 结果状态 |
|---|---|---|
| 正常检测→分拣→下料准备→解锁→人工取盘 | 三阶段完成事件顺序、`WholeTrayCompletion`、解锁读回、`FinalUnloadCompletion` | 待实现/待执行 |
| 任一阶段失败 | 不进入下一阶段、不生成完成记录、不写 0 | 待实现/待执行 |
| 可恢复超时 | 有限重试和退避均持久化，超限局部暂停 | 待实现/待执行 |
| PLC 断联/epoch 变化 | `UnknownHeld`、保持锁紧、禁止自动重发 | 待实现/待执行 |
| 重复请求/重启 | 持久化幂等，已完成阶段不重放，未终态按未知 | 待实现/待执行 |
| 解锁失败/状态保持 1/读回超时 | `UnknownHeld`，禁止自动重发并保持设备占用，不允许人工取盘确认 | 待实现/待执行 |
| Zone ACK 门禁 | `Zone_Config_Ack=1` 前无第一条 XY 动作 | 待实现/待执行 |
| Simulation/Fallback | 六组件 source matrix 的 source/quality/version/evidence reference 齐全，`SoftwareLoopOnly`，不能作为真实验收 | 待实现/待执行 |

### 最新设计验证矩阵（未执行）

| 验证层 | 场景与断言 | 依赖与证据 |
|---|---|---|
| Contracts / T053 | 分拣 4x0020/0021/0022；operation/epoch 关联、拒绝旧反馈；下料映射缺失拒绝 | 点位合同/端口调用日志；下料正常路径依赖 T046 |
| Rules / T053 | 完整映射、缺失/重复/歧义 MappingFailed；临时通信最多 4 次总尝试且退避 1/2/4 秒；算法超时最多 3 次总尝试且退避 2/5 秒；共享且不可重置的 120 秒期限 | 可控时钟事件序列；物理动作可能已派发时零自动重发 |
| Integration / T053 | 意图先提交；事务回滚、重复请求、三完成事件后聚合原子写入 WholeTrayCompletion/ReadyForUnlockSourceMatrix、人工确认与 FinalSourceMatrix/FinalUnloadCompletion 同事务提交 | 数据库事件/投影/完成记录导出；无终态保持 UnknownHeld |
| Integration / T053 | 人工核对 MappingFailed/UnknownHeld；旧任务 ReDetect/Scrap 无受控人工决定时拒绝；恢复不重放；7 年保留及引用完整性 | actor/时间/原任务/结果/原因/evidence references，保留策略边界证据 |
| E2E / five scenarios | 每场景均有进程、API transcript、Modbus audit、SQLite 事件/投影、source matrix、final result；不依赖前端或 006 | 六类文件与 manifest 哈希；缺一即 Blocked/NotRun |
| VirtualPlc / T054 | 独立进程检测/分拣/`UnloadPreparation`、Zone ACK 前无 XY、无 Cmd=1、`WholeTrayCompletion` 后写 0 并读回 0；失败/保持/断联/epoch；人工确认由 Host 入口提供 | T046 合同引用和对应实现完成后保留进程 PID、原始读写和数据库证据；人工确认不作为 PLC 点位 |

任务边界证据要求：T027/T038 只验证 PLC 适配器观察；T028/T052 只验证 VirtualPlc 行为；T039/T054 只验证独立进程证据；T033 只验证整盘完成前禁止 Cmd=0 的集成断言；T037 只验证完成证据引用替换；T045 只验证 Host 编排；T051 只验证 `WholeTrayCompletion` 和解锁业务门控。T048 只验证事件/投影/事务/恢复基础设施，不得生成下料 `Completed`、`WholeTrayCompletion` 或解锁证据。operationId/epoch 只作为 Host/适配器关联元数据，不作为 PLC 新增点位。

T046 已确认下料正式边界：协议版本 2026-09-21 §2.2 的 `XY_Move_Cmd=4`/`XY_Pos_Confirmed=1`、§2.4 的 `Pallet_Lock_Cmd=0`/`Pallet_Lock_Status=0`、§3.1.6 及总时序图的 Host 人工取盘确认。`operationId`/`connectionEpoch` 仍只是 Host/适配器关联元数据，不是 PLC 点位；不得新增独立下料命令、协议外错误码或默认成功。

以上均为待实现/待执行设计，不改变历史测试结果。合同设计已确认，但仍须实际实现和测试证据才能生成下料完成、`WholeTrayCompletion`、`FinalUnloadCompletion` 或解锁闭环并勾选任务。模拟结果永远不能作为真实设备验收。

## T046 正式下料边界冻结证据（2026-09-22）

本轮逐项核对 `spec.md`、`plan.md`、`research.md`、`data-model.md`、`contracts.md` 及 `contracts/*.md`，并以正式协议版本 2026-09-21 的 §2.2、§2.4、§3.1.6 和总时序图为唯一引用来源。003 的正式边界如下：

| 业务阶段 | Host 动作 | PLC 事实 | 完成门槛 |
| --- | --- | --- | --- |
| `UnloadPreparation` | 写 `XY_Move_Cmd=4x0001` 值 `4` | 读 `XY_Pos_Confirmed=4x0002` 值 `1` | 追加 `UnloadPreparation.Completed` |
| `UnlockObservation` | 先核验已提交 `WholeTrayCompletion`，写 `Pallet_Lock_Cmd=4x0023` 值 `0` | 读 `Pallet_Lock_Status=4x0024` | 只有读回 `0` 才追加 `ObservedUnlocked` |
| `ManualTrayRemovalConfirmation` | Host 接收操作员确认 | 无 PLC 点位 | 仅在 `ObservedUnlocked` 后生成 `FinalUnloadCompletion` |

`operationId` 和 `connectionEpoch` 只作为 Host/适配器关联元数据。未定义独立下料命令、协议外错误码或人工取盘 PLC 点位；写失败、读回超时、断联、状态保持 `1` 或 epoch 变化必须进入 `UnknownHeld`，禁止自动重发并保持设备占用。该记录是 T046 的文档证据，不代表真实 PLC 联调已完成。

## T044 003 侧交叉审计清单（2026-09-22）

在不读取或修改其他功能规格的前提下，已建立供 T055 使用的 003 侧审计清单：

- 003 内的启动夹紧事实只来自当前连接代次读取的 `Pallet_Lock_Status=1`；不把业务阶段名当作协议反馈。
- `UnloadPreparation`、`UnlockObservation`、`ManualTrayRemovalConfirmation` 保持三个独立边界；分拣取料/放料仍归 `Sorting`。
- `WholeTrayCompletion` 只聚合检测、分拣和 `UnloadPreparation`，且在解锁前提交；人工取盘确认不作为写 `Pallet_Lock_Cmd=0` 的前置条件。
- `UnknownHeld` 的触发条件、设备占用和禁止自动重发规则已在 003 合同中逐项出现。
- T027/T038 只负责适配器观察，T028/T052 只负责 VirtualPlc 设备事实，T045/T051 分别负责编排和业务完成门控；最终跨规格比对仍由 T055 执行。

此清单只记录 003 侧待核对项，不宣称 001/003 最终交叉审计已经完成。

## 第一轮实现证据（2026-09-22）

- `dotnet build VirtualPlc/VirtualPlc.csproj --no-restore`：通过，0 警告、0 错误。
- `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~LatestPlcProtocolTests|FullyQualifiedName~SimulatedPlcTests|FullyQualifiedName~StartClampStepTests"`：15/15 通过。
- `dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-restore --filter "FullyQualifiedName~CompletionPolicyTests"`：1/1 通过。

本轮证据覆盖 T028 的 PLC 内部夹紧和非法 `Pallet_Lock_Cmd=1` 拒绝、T035 的协议外按钮/夹紧事件不参与准入、T036 的 `Zone_Config_Ack=1` 模拟门禁，以及 T044/T046 文档合同。所有结果均为代码级、进程内或模拟验证，不代表真实 PLC 或现场验收。并行启动测试曾因共享编译输出文件锁失败，改为串行执行后通过；该失败不属于产品测试失败。

## T047 阶段端口和领域合同证据（2026-09-22）

- DetectionPort 合同固定 `RunId/TrayId/StationId/LineId/OperationId/PlanRevision/ConnectionEpoch/DeadlineUtc`、媒体引用、来源和结果状态；结果必须与请求 operation 关联，失败类必须带 `errorCode`，模拟/降级结果不能作为真实验收证据。
- PLC 阶段端口固定 `Sorting_Part_Index=4x0020`、`Sorting_Cmd=4x0021`、`Sorting_Exec_Status=4x0022`，并保留 T046 的 `UnloadPreparation`/`UnlockObservation` 映射；`operationId` 与 `connectionEpoch` 仅是 Host/适配器关联元数据。
- `PlcStageActionResult` 拒绝旧 epoch 完成反馈；`UnknownHeld` 必须 `HoldsDevice=true` 且 `CanRetry=false`。`MappingFailed` 明确禁止部分派发。
- `StageIdempotencyRegistry` 对相同 key/摘要返回 Replay，对相同 key/不同摘要返回 Conflict；`WholeTrayCompletionReference` 和 `FinalUnloadCompletion` 需有持久化身份及阶段引用。

实际验证：

- `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~StagePortContractTests"`：6/6 通过。
- `dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --no-restore --filter "FullyQualifiedName~StageOperationContractTests"`：2/2 通过。

以上仅证明端口和领域合同的软件规则，不代表真实 PLC、相机或算法接入。

## T027 解锁适配器观察证据（2026-09-22）

`LatestProtocolPlcDevice.RequestPalletUnlockAsync` 现在固定捕获发起动作的 `connectionEpoch`，写入 `Pallet_Lock_Cmd=0` 后只接受同 epoch 读回 `Pallet_Lock_Status=0` 的 `Completed`；写失败、状态 `2`、读回异常、连接断开或 epoch 变化都会回调 `UnknownHeld` 并锁停适配器。SimulatedPlc 对应保持锁紧、写失败和 epoch 变化也只产生未知结果，不自动重发。

实际验证：

- `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter "FullyQualifiedName~PalletUnlockStepTests"`：5/5 通过。

以上仅为适配器和进程内模拟合同验证，不代表真实 PLC 设备验收。

## T040–T057 第一工位纵向切片实现与运行证据（2026-09-23）

- T040/T041：SQLite 故障注入证明聚合失败无部分矩阵/完成事实，人工确认失败无“确认已见但最终完成缺失”；混合来源保持 `SoftwareLoopOnly`，Unknown/Missing/Unverifiable 必需组件阻止聚合。
- T043–T045：外部调用前提交 `IntentRecorded`，反馈使用后续短事务；三阶段聚合使用独立事务；认证 ManualActor、ManualTrayRemovalConfirmed、FinalSourceMatrix 和 FinalUnloadCompletion 使用同一后续事务。
- T046/T047：ControlledRecoveryDecision 保存 actor、role、时间、原因、原任务/operation 和证据引用。重启只重建已提交事实，在途 PLC 动作进入 UnknownHeld，保留期限且不重发；重启、重连、复位不能自动产生 ReDetect/Scrap。
- T048–T051：新增持久化证据查询、认证人工确认、受控恢复决定和提交后通知；GET 保持事实源，只有 FinalUnloadCompletion 将运行置为 Completed。

实际测试：

- `t040-t051-us3.trx`：12/13；唯一失败为测试把枚举 JSON 当字符串读取，产品路径未失败。修正后 `t041-source-matrix-r5.trx` 5/5 通过。
- `t043-contracts.trx`：21/21 通过。
- `t043-t045-three-stage-r7.trx`：2/2 通过。
- `t042-t046-t047-t049.trx`：2/2 通过。
- `t045-t048-t049-t051-final-r2.trx`：1/1 通过。
- `t050-notifications-r2.trx`：2/2 通过。

独立 Host + 独立 VirtualPlc + 正式 Modbus TCP + 真实 SQLite 场景：

| 任务 | 场景 | 结果 | 证据根 |
| --- | --- | --- | --- |
| T053 | 正常闭环 | Passed | `artifacts/plc-latest/e2e-normal-20260923-r4/whole-tray/normal/` |
| T054 | 算法三次总尝试、2/5 秒退避、Pending 分拣后完成 | Passed | `artifacts/plc-latest/e2e-algorithm-pending-20260923-r2/whole-tray/algorithm-pending/` |
| T055 | 人工确认 409、Unknown 算法质量阻止聚合、无非法解锁 | Passed | `artifacts/plc-latest/e2e-unlock-gates-20260923-r2/whole-tray/unlock-gates/` |
| T056 | UnloadPreparation 派发后 PLC 断联，UnknownHeld 且 Cmd=4 仅一次 | Passed | `artifacts/plc-latest/e2e-plc-unknown-20260923-r3/whole-tray/plc-unknown/` |
| T057 | UnloadPreparation 在途时 Host 重启，期限不重置、Cmd=4 不重发 | Passed | `artifacts/plc-latest/e2e-host-restart-20260923-r1/whole-tray/host-restart/` |

各目录均含 manifest、进程/日志、API transcript、Modbus 写审计、SQLite 事件/投影、来源矩阵、完成/解锁/人工证据和最终结果。上述均为明确标记的 Virtual/Simulated/Test 软件闭环，不代表真实设备或生产验收。

### T059 Deferred / NotRun 登记

其他工位、真实设备/算法生产验收、006 页面与前端测试、客户原型变更、生产人工确认 UI、MES/模型/样本接线、五场景之外压力/长稳，以及 OPEN-16/22/26 生产规则均为 Deferred/NotRun。它们不是第一工位后端闭环或五场景软件 E2E 的完成条件；本次没有为其实现代码或伪造通过证据。
## T048 事件、投影、事务与恢复证据（2026-09-22）

- 实现范围：`Gaode.Application/Workflow/StageEventing.cs` 定义阶段事件、投影、终态和至少七年保留策略；`Gaode.Infrastructure/Persistence/StageEventStore.cs` 使用 SQLite 短事务原子追加事件、更新投影和幂等索引，并提供提交事件重放与在途恢复；新增 `StageEvents`、`StageProjections`、`StageIdempotencies` 实体及 `202609220001_StageEventing` 迁移。
- 幂等与原子性：`StageEventStoreTests.AppendProjectsInOneShortTransactionAndReplaysByIdempotency` 验证相同键只能 `Replay`、载荷冲突返回 `Conflict`，重复请求不增加事件；`FailedProjectionWriteDoesNotExposeASecondEvent` 验证事件和投影写入失败后无第二条事件或部分投影可见。
- 重启恢复：`RestartRecoversAcceptedExecutingWithoutTerminalAsUnknownHeld` 验证只读取已提交事件，并将无终态的 `Accepted/Executing` 动作追加为 `UnknownHeld`；投影保持 `DeviceHeld=true`、`AutomaticRetryAllowed=false`，无自动重发入口。
- 保留和业务边界：`CommittedEventsCarrySevenYearRetentionAndNoBusinessCompletionIsSynthesized` 验证事件保留期限不少于七年，基础设施不合成 `ObservedUnlocked` 或 `FinalUnloadCompleted`；本任务未生成 `UnloadPreparation.Completed`、`WholeTrayCompletion`、解锁或人工取盘完成证据。
- 验证命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --filter "FullyQualifiedName~StageEventStoreTests|FullyQualifiedName~ModelContractTests" --no-restore`，结果 **7/7 通过**。结果仅为软件合同/持久化模拟证据，不代表真实 PLC 验收。

## T049 检测结果到 RecipeRunPlan 映射证据（2026-09-22）

- 实现范围：`Gaode.Application/Workflow/RecipeSortingMapper.cs` 只读取已完成的 `DetectionPort` 结果和冻结 `RecipeRunPlan` 的 `SortUnit` 步骤，按计划 `Sequence` 生成 `SortingActionPlan`；每个动作保留 `runId`、`trayId`、`recipePlanVersion`、`objectId`、位置、分类、派生稳定 `operationId` 和 `connectionEpoch`。
- 完整映射：检测对象可乱序返回，但输出严格按冻结计划顺序；未调用 `IPlcStageActionPort`，不生成任何 PLC 动作或下料完成证据。
- 拒绝矩阵：缺失对象进入 `MissingObjectIds`；重复检测对象或重复计划目标进入 `DuplicateObjectIds`/歧义；位置标识或计划分类不符、计划外对象、Recipe plan version 不一致进入 `AmbiguousObjectIds`。任何失败均返回空动作列表，追加 `MappingFailed` 事件并由 T048 投影进入 `PausedForManualReview`，设置人工核对标记。
- 幂等/事务边界：失败事件通过 `IStageEventStore` 追加，使用 Detection 请求幂等键的 `:mapping` 派生键和 JSON 摘要；事件、投影及冲突处理由 T048 短事务实现。T049 不实现检测/分拣执行、下料正常路径、`UnloadPreparation.Completed`、`WholeTrayCompletion`、解锁或人工取盘完成。
- 验证命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --filter "FullyQualifiedName~RecipeSortingMapperTests" --no-restore`，结果 **5/5 通过**；受影响合同回归 `RecipeSortingMapperTests|StageEventStoreTests|StagePortContractTests|PalletUnlockStepTests` 结果 **23/23 通过**。结果是软件合同/模拟证据，不代表真实 PLC 验收。

## VirtualPlc 复用、T052 能力盘点与 T054 独立进程证据（2026-09-22，只读核对）

本节保留历史验证记录，不启动进程、不重跑脚本，也不把新增盘点改写成测试通过。

### 已核对的接入链路

| 范围 | 已核对内容 | 当前结论 |
|---|---|---|
| 既有独立程序 | `VirtualPlc/Program.cs` 注册 `VirtualPlcEngine`、`ModbusTcpServer` 和健康/仪表盘；`ModbusTcpServer.cs` 监听 TCP 并支持既定读写功能码；`appsettings.json` 默认 `127.0.0.1:1502`、UnitId `1`、HTTP `5080` | 已有程序可复用；源码盘点不是运行证据 |
| Host 接入 | `Station01Registration` 的 `VirtualPlcIntegration` 使用 `LatestProtocolPlcDevice`；`ModbusTcpClient` 连接 `PlcHost/PlcPort/PlcUnitId`，默认 `127.0.0.1/1502/1` | Modbus TCP 接入链路已存在，仍需按 T054 场景取证 |
| 协议行为 | `PlcAddressMap`/`VirtualPlcEngine` 已包含心跳、运动、分拣、锁紧/解锁和故障注入路径 | 标记为“已实现待验证”，不等于全部 T052 场景通过 |
| 独立验证入口 | `scripts/verify-latest-plc.py` 分别启动 VirtualPlc 与 Host，采集结果、样本和 PLC 日志 | 可作为 T054 入口；脚本实际覆盖范围仍需逐场景核对 |

### T052 状态

源码核对已覆盖：`XY_Move_Cmd=4x0001`/`XY_Pos_Confirmed=4x0002` 地址、`Pallet_Lock_Cmd=4x0023`/`Pallet_Lock_Status=4x0024` 地址、心跳、运动/分拣、锁紧/解锁和故障注入。以下仍属于“待验证”，不能由源码存在替代：

- Cmd=4 后读回 Pos=1 的独立进程正常路径；
- WholeTrayCompletion 之后 Cmd=0 且只接受 Status=0 的解锁观察；
- 写失败、读回超时、状态保持 1、动作超时、断联和 connectionEpoch 变化进入 `UnknownHeld`，保持设备占用且禁止自动重发；
- 启动路径拒绝 Cmd=1，且不生成业务完成证据；
- T052 不负责生成 `WholeTrayCompletion`、`FinalUnloadCompletion` 或人工取盘确认。

因此 T052 的已有能力为“源码已有”，独立场景验证为“待执行”；若实际运行发现行为缺失，才允许新增或修改 VirtualPlc 行为，不能重复实现已有路径。

### 历史 artifacts 覆盖范围

- `artifacts/plc-latest/e306236fd59145dc935999ea17e828b9/` 和 `eb617286b2d743bb94c4633fd62c81df/` 均保留 `result.json`、`samples.json`、`plc.log` 和 `host.log`。两份 `result.json` 的历史结果为 `passed=true`，run state 为 17，且可见 `sortingState=NotStarted`、`wholeTaskState=NotCompleted`；`plc.log` 记录 VirtualPlc 在 `127.0.0.1:1502` 启动、Host 连接和 `PauseHeartbeat` 故障。
- 这些记录证明其历史脚本场景的独立进程启动、Modbus 连接和心跳故障采样；不证明 T052 的全部移动/解锁故障矩阵，也不证明 Detection→Sorting→UnloadPreparation、WholeTrayCompletion、解锁读回或人工取盘闭环。
- 本节不把历史 `passed=true` 解释为新的 T054 完成证据；缺少对应场景的 PID、点位审计、阶段事件/投影和结果引用时，T054 对该场景保持 `Blocked` 或 `NotRun`。

### T054 保留的验收条件

每个独立进程场景必须归档：源码/配置版本、VirtualPlc 与 Host PID、启动命令和退出码、两侧原始日志、Modbus 原始读写/点位审计、`operationId`/`connectionEpoch`、Host `StageEvent`/`StageProjection`、`WholeTrayCompletion`/解锁/人工确认引用和结果 JSON。正常、失败、保持锁紧、超时、断联、epoch 变化、重复请求和重启恢复必须逐项标记结果来源 `Virtual/Simulated`；进程不可用或场景未执行为 `Blocked`/`NotRun`，不得勾选 T054，也不得作为真实 PLC 验收。

## T058 最终回归与五场景统一证据（2026-09-23）

本节是新的实际运行结果，取代上文历史盘点中的“待执行”状态；不反向改写历史记录。

- `dotnet build backend/Gaode.slnx --no-restore -m:1`：退出码 0，0 警告、0 错误。
- `dotnet test backend/Gaode.slnx --no-restore -m:1 --logger "trx;LogFilePrefix=t058-full-regression-final"`：退出码 0，共 299 项通过、0 失败、0 跳过。其中 Contracts 182、Integration 61、Rules 56。
- TRX：`backend/tests/Gaode.Contracts.Tests/TestResults/t058-full-regression-final_net10.0_20260923074205.trx`、`backend/tests/Gaode.Integration.Tests/TestResults/t058-full-regression-final_net10.0_20260923074324.trx`、`backend/tests/Gaode.Rules.Tests/TestResults/t058-full-regression-final_net10.0_20260923074329.trx`。
- `python -B scripts/verify-latest-plc.py --empty-store artifacts/plc-latest/empty-store-20260923-final --scenario all --evidence-run-id e2e-five-scenarios-20260923-final-r1`：退出码 0；五个场景全部 `Passed`，统一 `softwareScope=Virtual/Simulated/Test`。
- 汇总：`artifacts/plc-latest/e2e-five-scenarios-20260923-final-r1/summary.json`，SHA-256 `F14333CBECCC4FE17E1A45DCCFAA76A16C156EEE4F8F628A5E43CB0149A8B15B`。

场景 manifest SHA-256：

| 场景 | 结果 | runId / trayId | manifest SHA-256 |
| --- | --- | --- | --- |
| normal | Passed，显式认证人工确认后到 FinalUnloadCompletion | `2e8de577-e0c7-4340-b409-9499dfe1206f` / `73fab4d6-3bfd-43b1-ab0d-5453e1e73249` | `6B4E4086BFD90EC3559859072C97B942D09883B47AF9E230AAE00CEA34131064` |
| algorithm-pending | Passed，3 次总尝试、2/5 秒退避、Pending 正式分拣后完成 | `72f88f02-45bb-470e-9f3a-ff8693b10145` / `a9d9f18e-3656-4500-8843-17c0ed49af53` | `821EFA4C00C9A7F8100AF3E0578A80517DD85AAFEF4012FADCDE4E2A52717B78` |
| unlock-gates | Passed，人工确认返回 409，Unknown 质量阻止聚合且未非法解锁 | `66617686-a7cc-424b-aa65-3c67d52e3e39` / `403c1835-eba8-4d9c-968e-db9dac37bae5` | `6A478B09E995B35F5A318FDD58944497BAEF447EEF6192C6888400B0B26DEC79` |
| plc-unknown | Passed，派发后断联进入 UnknownHeld，Cmd=4 未重发 | `fc1a09d1-2708-4630-a882-b2fc51a3a957` / `3d6915b0-cd53-4da9-a392-80c7c1f04646` | `36A7B396F3EC9C152AC58834391C63134BD12E718EB985803A34B27992C31962` |
| host-restart | Passed，只重建已提交事实，在途 PLC 动作保持 UnknownHeld | `68251bc2-4340-4978-8f80-fec39b1ebd7a` / `35ce4f09-b58a-4a31-98eb-09ce67baaac7` | `B0F4445F72797474A0192706EE884BD7C459D15E86DF0D146B03B864CAFA32F8` |

每个场景目录均保存独立 Host/VirtualPlc 进程信息与日志、仅 Host API 的 transcript、Modbus audit、真实 SQLite 事件/投影、组件来源矩阵、完成/解锁/人工证据和最终快照。正常与算法 Pending 场景的成功只由同一 run/tray 的已提交 `FinalUnloadCompletion` 判定；联调客户端没有默认自动确认。门禁、PLC 未知和重启场景按各自预期停在拒绝或 `UnknownHeld`，没有伪造最终完成。

## T060 最终一致性与范围审计（2026-09-23）

- active feature 保持 `003-plc-latest-protocol`。`spec.md`、`plan.md`、`contracts/`、`data-model.md`、`quickstart.md` 和连续 T001–T060 的 `tasks.md` 均以第一工位后端纵向切片为范围；当前 feature 没有独立 `checklists/` 目录，因此没有擅自勾选检查清单。
- 短事务一致：动作意图及投影在外部调用前提交，反馈及阶段投影另事务提交；三阶段 Completed 后使用独立聚合事务；人工确认与 FinalUnloadCompletion 同一后续事务。故障注入和 SQLite 测试均已覆盖，无跨设备/算法调用持有事务。
- 重试口径一致：通信总尝试最多 4 次、退避 1/2/4 秒；算法超时总尝试最多 3 次、退避 2/5 秒；共用不可重置的 120 秒阶段期限；可能已派发的 PLC 动作直接 `UnknownHeld` 且不自动重发。旧任务重检/报废只能由带 actor、时间、原因、原任务和证据引用的受控人工决定。
- 来源矩阵保留 Host、PLC、Camera、Light、Algorithm、ManualActor 各组件的 source/quality/version/evidence；任一 Virtual/Simulated/Test 不会聚合成 Real，Unknown/Missing/Unverifiable 必需来源阻止 WholeTrayCompletion。五场景均明确为软件闭环，不是生产验收。
- 003 没有实现或依赖 `frontend/src`、`frontend/tests`、006 页面或客户原型；测试客户端仅调用 Host API。工作区不是 Git 仓库，无法用 `git diff` 证明路径历史，但本轮没有对上述路径执行写入；审计时 frontend 最新文件时间仍为 2026-09-22 09:49:52 +08:00。`constitution.md` 和 `E:\dzk\gaode\原型.zip` 未执行写入，审计快照 SHA-256 分别为 `39015EC33B4B54255A4A4537E90E327811E39D33AA9C9362C99BC1D1722751D8`、`3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`。
- 001 的修改只限已授权的 `s01-handoff/2.0` producer 对齐；006、其他 feature、原型和生产 UI 未纳入接线。Deferred/NotRun 仍只作行政登记，不是第一工位完成条件。
- 没有通过硬编码成功、跳状态、伪造 PLC/算法完成、隐藏降级或无限重试形成通过结论。VirtualPlc 只提供 PLC 事实；Simulated/Test 相机、光源和算法通过正式端口进入相同流程并保留输入、输出、异常和来源证据；人工确认来自认证 API。

审计结论：T001–T060 均已有实现或明确的 Deferred 行政登记及对应实际证据。第一工位软件主流程从唯一启动入口，经公共准备和 committed handoff，完成 Detection、Sorting、UnloadPreparation、WholeTrayCompletion、ObservedUnlocked、显式人工取盘确认，最终到达 `FinalUnloadCompletion`；本结论仅为 Virtual/Simulated/Test 软件闭环通过，不声明真实 PLC、真实相机/光源、真实算法精度、现场节拍或生产验收通过。

## 2026-09-24 T063 诊断增量核验（独立于历史 T001–T060）

- 正式 Host/Modbus 的受理后通信失效样本：`artifacts/station01-007/api-diagnostic-communicationafterreceipt-20260924-020126/`。POST 202 后有 `requestId/commandId/runId`，首次异常 `IOException: HeartbeatStoppedChanging`；GET 为 `Unconfirmed/PlcHeartbeatLost`、`WaitingClamp`、`UnknownHeldNoAutomaticRetry`。进程退出后 `diagnostic-index.json` 可连接 SQLite `operationId`、连接代次和原始 Host/VirtualPlc 日志；协议审计可见故障前一次启动命令，不能宣称从未派发，故障后无盲重发与后继 XY/采集。
- 同代次可靠不安全对照：`artifacts/station01-007/api-diagnostic-unsafebeforerequest-20260924-020351/`。可靠状态 `connected=true/safetyClear=false/alarmBits=2`、epoch=1，公开结果 `ExplicitUnsafe/SafetyInterlockDenied`，无启动动作。两样本均是 `Test/VirtualPlc`，不是现场 PLC 证明。
- 定向规则与过期状态测试：`artifacts/station01-007/diagnostics-20260924/tests/diagnostics-plc-final.trx`，5/5；正常启动/查询 `diagnostics-normal-start-query-final.trx`，15/15。生效 Host `HeartbeatTimeoutMs=3000`，状态过期阈值 `max(500,5×IoTimeoutMs)`，本样本 `IoTimeoutMs=1000` 即 5000ms；来源为 `PlcRuntimeOptions`、`LatestProtocolPlcDevice.Observe` 及 `process.json`，没有为通过测试放宽正式阈值。
- 正常正式 Modbus 回归 `FormalHostPortKeepsPreviousArrivalUntilNextAcceptedMoveAndBindsAfterF` 本轮曾偶发等待夹紧状态超时，独立复测通过（`diagnostics-formal-modbus-r3.trx`），再运行整组 4/5、同项失败（`diagnostics-formal-suite-r4.trx`）。失败时连接与安全反馈正常、`PalletLockStatus=0`；协议变化序列有 `PC_Start_Cmd` 0→1→0，却没有 PLC 锁盘反馈。可定位到启动脉冲未形成设备动作的阶段，但尚不能区分虚拟 PLC 扫描错过脉冲、互锁瞬态或其他机制，也不能将其等同于已确认的原始人工故障。不能据此宣称正常路径稳定。因此 T063 暂不勾选，待复现机制与稳定回归核实；日志诊断能力与故障修复分开判断。

### 2026-09-24 T063 后续定位、修复与完成判定

保留上段当时的未完成判定及全部失败TRX。新增250ms独立VirtualPlc扫描的定向复现 `artifacts/station01-007/diagnostics-20260924/tests/start-scan-before-fix.trx`：Host对 `PC_Start_Cmd` 的高、低写入均有Modbus回执，VirtualPlc写入事件记录 `WriteHigh→WriteLowWithoutSample`；写入时自动/就绪/安全门禁均为真，后续无扫描高电平、无动作受理及锁盘反馈。故障位置为VirtualPlc的Modbus写入和独立扫描之间丢失一次命令边沿；不能从旧样本单独推定此机制。权威协议只规定启动信号及 `Pallet_Lock_Status=1` 完成依据，未给出可任意放宽的保持时长。本次仅在VirtualPlc内部锁存一次上升沿供下一扫描重判门禁，不改Host正式时序、预算/心跳阈值或重发规则，不以锁存直接制造反馈。新增有界轨迹记录UTC、进程内单调tick、Modbus transaction、采样/门禁/动作受理/实际反馈；Host写入日志保留run/operation/action/epoch及相同transaction的Modbus回执，后者仍非物理完成。

修复后 `start-scan-after-fix-r1.trx` 显示 `WriteLowWithoutSample→ScanLatchedEdge→PalletLockAccepted→PalletLockFeedback`，`start-latch-gate-final.trx` 的不安全反向门禁2/2通过。正式Modbus/诊断/安全组合 `t063-final-combined-r1.trx` 与最后日志限频调整后的 `t063-final-combined-r4.trx` 各17/17；依据此前整组4/5的间歇性，有限重复 `virtualplc-after-fix-r2/r3/r4.trx`、`t063-final-repeat-r2/r3.trx`、`virtualplc-final-r1.trx` 均通过。中途 `virtualplc-after-fix-r1.trx` 因新增测试固定等待1秒早于实际第二次250ms扫描而失败，已改为在测试自己的5秒观察期内等待真实反馈；`start-latch-gate-after-fix.trx` 与 `start-latch-gate-debug.trx` 因测试在后台服务初始化前注入而失败，已等待第一心跳后再注入。所有失败记录保留，不以重跑隐藏。

修复后的独立进程/正式Modbus页面联调证据：`artifacts/station01-007/page-diagnostic-communication-valid-20260924-0250/` 有同一请求的Host写入transaction、VirtualPlc采样/反馈、协议审计、GET及SQLite；`artifacts/station01-007/page-diagnostic-unsafe-final-20260924-0245/` 为可靠不安全对照。通信样本在页面202之后才注入，首次 `HeartbeatStoppedChanging` 在注入之后，是故障注入预期，不是上述锁盘边沿缺陷；此前 `page-diagnostic-communication-final-20260924-0240/` 的Host在点击前已有心跳故障，明确作无效A样本保留。3秒心跳观察失效属于无法确认安全的保守阻断，未放宽阈值；原始人工一次启动失败仍缺其独立原始日志，不能据此认定同因或已修复。T063仅在Test/VirtualPlc软件边界满足，真实PLC未验证。

收尾正常启动/查询集成回归 `artifacts/station01-007/diagnostics-20260924/tests/normal-start-query-after-t063.trx` 15/15通过；未以失败注入代替正常路径。Host/VirtualPlc程序构建、Test页面和协议/配置SHA记录于上述进程证据包。
