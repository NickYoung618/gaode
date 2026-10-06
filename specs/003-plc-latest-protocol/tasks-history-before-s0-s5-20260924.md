# 后端任务清单：第一工位完整主流程纵向切片

**功能标识**：003-plc-latest-protocol  
**输入**：[spec.md](./spec.md)、[plan.md](./plan.md)、[data-model.md](./data-model.md)、[contracts.md](./contracts.md)、[quickstart.md](./quickstart.md)  
**当前适用宪章版本**：3.1.0；已勾选任务的历史证据保留产生时版本  
**规格范围**：第一工位后端从唯一 Host 入口到已提交 `FinalUnloadCompletion`  
**日期**：2026-09-24

> 本清单只实现 Host、Domain/Application、正式设备/算法端口、SQLite、后端 API、SignalR 合同、联调客户端和独立进程 E2E。不得修改其他 feature 的 spec/plan/tasks，不得创建 `frontend/src`、`frontend/tests`、生产人工确认 UI 或客户原型任务。

## 拆解规则与门禁

- 每项任务均内嵌需求来源、constitution 原则、真实依赖、完成条件和证据路径；只有依赖及共享文件不冲突时才标 `[P]`。
- `GATE-001-HANDOFF-V2` 表示 001 的 `s01-handoff/2.0` producer 规格/合同已通过单独授权完成对齐。它只阻塞 T024–T026 的实际 producer/consumer 接线和 US4 场景执行，不阻塞 T002–T023、T027–T052。
- 006 页面、前端测试、原型测试和生产人工确认 UI 不进入任何任务依赖；测试/联调客户端只调用 Host API。
- VirtualPlc 只复用现有独立进程和正式 Modbus TCP；若没有经合同确认的协议缺口，不得扩大或重写它。
- 本清单中的测试任务为强制任务。未执行、Blocked、Skipped 或证据不全不得记为 Passed。

### 007共享文档同步后的适用边界

已勾选的T024/T026、T039/T045/T049、T052–T058保留其当时的实现与证据范围，不代表固定图片正式采集、逐Detection采集的独立worker实收发、Test自动模拟取盘来源或006实际页面联调已经通过。007的T001负责先同步上述共享合同，T006–T014负责新增后端接线与真实证据，T015才验证006实际页面到FinalUnloadCompletion；不把这些007工作重新加入003历史完成门禁。确认API共享合同现统一为`POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`，请求体仅`requestId/expectedRevision/reason`，Host从已提交事实和认证上下文取得其余信息；007 T013负责使Test模拟ManualActor不再误记AuthenticatedHuman。006页面请求/凭据/展示归006，Host侧限定Test来源的API/通知跨源与授权接线归007 T010；006未交付不阻塞独立后端接线。

---

## Phase 1：必要准备与跨 feature 协调门禁

**目标**：在不修改其他 feature 的前提下冻结 003 实现边界，并使 001 门禁只影响实际 handoff 接线。

- [X] T001 核对 `s01-handoff/2.0`、API/notification/source-matrix 版本及 001/006 差异登记并写入 `specs/003-plc-latest-protocol/validation.md` 和 `specs/003-plc-latest-protocol/differences.md`（来源：FR09/FR11、plan“跨 feature 依赖”；原则：P01/P05/P12；依赖：无；完成/证据：记录2026-09-23有限授权及001 handoff直接相关 spec/contracts/plan/tasks 对齐结果，明确文档门禁满足不等于接线/测试完成，006非阻塞，且未修改006、其他001需求或原型）

---

## Phase 2：共享领域、端口与持久化基础

**目标**：先完成不依赖 001/006 的来源矩阵、重试期限、恢复决策、启动身份、handoff 消费合同和 SQLite 模型基础。

- [X] T002 [P] 为六组件矩阵、里程碑必需性、`SoftwareLoopOnly` 派生和 Missing/Unknown/Unverifiable 阻断编写规则测试到 `backend/tests/Gaode.Rules.Tests/Station01/ComponentEvidenceMatrixTests.cs`（来源：FR10/FR13、验收7/8、component-source-matrix 合同；原则：P07/P09；依赖：T001；完成/证据：测试覆盖任一 Virtual/Simulated/Test 不得聚合为 Real，ManualActor 在 ReadyForUnlock 为 NotYetRequired，必要来源缺失阻止完成）
- [X] T003 实现 `ComponentEvidence`、`ComponentEvidenceMatrix`、ReadyForUnlock/Final 矩阵校验与摘要派生到 `backend/src/Gaode.Domain/Station01/ComponentEvidenceMatrix.cs`（来源：FR10/FR13、验收7/8；原则：P05/P07/P09；依赖：T002；完成/证据：T002 全部通过，Domain 不引用 HTTP/EF/设备 SDK，矩阵不可变且不产生生产验收结论）
- [X] T004 [P] 为临时通信 4 次总尝试/1-2-4 秒、算法超时 3 次总尝试/2-5 秒和共享不可重置 120 秒期限编写可控时钟测试到 `backend/tests/Gaode.Contracts.Tests/Workflow/StageRetryPolicyTests.cs`（来源：FR08/FR13、验收6；原则：P04/P06/P09；依赖：T001；完成/证据：测试包含 deadline 内、退避越界、重启后不重置和可能已派发动作零重试四类断言，并分别断言 Detection 通信次数耗尽/期限先到形成逐对象 Pending、PLC 派发前失败与派发后 UnknownHeld 不混用）
- [X] T005 实现按故障类型分流的 `StageRetryPolicy` 和冻结 `StageDeadlineAt` 到 `backend/src/Gaode.Application/Workflow/StageRetryPolicy.cs`、`backend/src/Gaode.Application/Workflow/StageEventing.cs` 和 `backend/src/Gaode.Application/Ports/StagePortContracts.cs`（来源：FR08/FR13；原则：P04/P06/P07；依赖：T004；完成/证据：T004 通过，每次 attempt/retry 保存 plannedAt/deadline/error/operationId，Detection 通信次数耗尽或期限先到形成带原异常的逐对象 Pending；PLC 派发前耗尽明确失败，可能已派发时直接 UnknownHeld）
- [X] T006 [P] 为旧任务 ReDetect/Scrap 决策字段、权限边界及无决定拒绝编写规则测试到 `backend/tests/Gaode.Rules.Tests/Station01/ControlledRecoveryDecisionTests.cs`（来源：FR07、验收9；原则：P07/P08/P09；依赖：T001；完成/证据：测试覆盖 actor/时间/原因/原任务/evidenceRefs/requestId/revision，证明重启、重连或复位不能自动生成决定）
- [X] T007 实现不可变 `ControlledRecoveryDecision` 与授权判定到 `backend/src/Gaode.Domain/Station01/ControlledRecoveryDecision.cs` 和 `backend/src/Gaode.Application/Workflow/ControlledRecoveryPolicy.cs`（来源：FR07、验收9；原则：P05/P07/P08；依赖：T006；完成/证据：T006 通过，决定只授权后续用例且不能直接生成算法、PLC 或完成事实）
- [X] T008 [P] 为版本化 `StartRunContext` 的 tray/station/line/scenario/occupiedSlots/purpose 字段和冲突校验编写合同测试到 `backend/tests/Gaode.Contracts.Tests/Station01/StartRunContextTests.cs`（来源：FR11、验收2；原则：P07/P09；依赖：T001；完成/证据：缺失、空值、重复身份和错误 schema version 均产生明确错误，不临时生成 trayId）
- [X] T009 实现 `StartRunContext` 解析、WorkflowIdentity 冻结及 run/tray 隔离到 `backend/src/Gaode.Application/Station01/StartRunContext.cs`、`backend/src/Gaode.Domain/Station01/RunIdentity.cs` 和 `backend/src/Gaode.Domain/Station01/RunSnapshot.cs`（来源：FR07/FR11、验收2/7；原则：P05/P07/P08；依赖：T008；完成/证据：T008 通过，同一身份贯穿快照且命令受理不等于完成）
- [X] T010 [P] 为阶段 deadline、组件矩阵、恢复决定、完成引用和 revision 的 EF 模型约束编写合同测试到 `backend/tests/Gaode.Contracts.Tests/Persistence/Station01MainFlowModelTests.cs`（来源：FR07/FR13、验收7；原则：P07/P08；依赖：T001；完成/证据：测试覆盖不可变引用、唯一键、外键、保留期限和 Host 不静默迁移）
- [X] T011 扩展 SQLite 实体、映射和受控迁移到 `backend/src/Gaode.Infrastructure/Persistence/Station01DbContext.cs`、`backend/src/Gaode.Infrastructure/Persistence/Station01EntityConfigurations.cs`、`backend/src/Gaode.Infrastructure/Persistence/Migrations/Station01MainFlow.cs` 和 `backend/src/Gaode.Infrastructure/Persistence/Migrations/Station01DbContextModelSnapshot.cs`（来源：FR07/FR13、验收7；原则：P07/P08；依赖：T003、T007、T009、T010；完成/证据：T010 通过，迁移保存矩阵/决定/deadline/引用且只通过受控迁移执行）
- [X] T012 [P] 为 `s01-handoff/2.0` 的 003 消费方身份、plan revision、证据引用、幂等与恢复校验编写合同测试到 `backend/tests/Gaode.Contracts.Tests/Station01/PublicPreparationHandoffV2Tests.cs`（来源：FR05/FR11、验收2；原则：P01/P07/P08；依赖：T001；完成/证据：测试不修改 001 产物，证明内存通知、空引用、错 run/tray/plan 不能构造 DetectionRequest）
- [X] T013 实现 003 侧 `PublicPreparationHandoffV2`、持久查询与消费校验端口到 `backend/src/Gaode.Application/Station01/PublicPreparationHandoffV2.cs`、`backend/src/Gaode.Application/Ports/IStageHandoffQuery.cs` 和 `backend/src/Gaode.Infrastructure/Persistence/TraceQuery.cs`（来源：FR11、验收2/7；原则：P05/P07/P08；依赖：T009、T011、T012；完成/证据：T012 通过；该任务可用持久化 fixture 独立完成，不要求修改 001 或 006）

**Foundation 完成条件**：T002–T013 通过规则/合同测试；所有后续任务均可在不依赖 006 的条件下继续，只有实际 handoff producer/consumer 接线保留 `GATE-001-HANDOFF-V2`。

---

## Phase 3：US1——唯一入口、公共准备和已提交 handoff（P1）

**目标**：同一 run/tray 从唯一 Host 启动请求经过夹紧、3D、F、配置/配方 plan+bind，形成已提交 handoff；该 handoff 仍不是最终完成。  
**独立完成条件**：后端合同与集成测试证明 `POST /api/v1/station01/runs` 是唯一入口，FR03–FR05 顺序完整，handoff 持久提交前不进入 Detection，提交后运行保持非终态。  
**相关需求与原则**：FR01–FR07、FR11；验收2/5/7；P03–P09/P11。

### 适用的软件验证

- [X] T014 [P] [US1] 为连续 3 秒无有效心跳后的动作锁定、报警/Restricted 投影以及重连/复位不自动续跑编写合同测试到 `backend/tests/Gaode.Contracts.Tests/Devices/HeartbeatInterlockTests.cs`（来源：FR03、验收5；原则：P04/P06/P07/P09；依赖：T005、T009；完成/证据：可控时钟证明 2999ms 不误触发、3000ms 触发锁停，恢复连接后无新动作派发）
- [X] T015 [P] [US1] 为 XYZ 双状态、三坐标匹配、`Inspection_Status=1→2→0`、`Z_Reset_Status=2` 门禁和新移动重置编写协议测试到 `backend/tests/Gaode.Contracts.Tests/Devices/InspectionHandshakeSequenceTests.cs`（来源：FR04、验收5；原则：P03/P04/P07/P09；依赖：T005、T009；完成/证据：同一 operation/epoch 的完整读写序列可断言，坐标或双状态不匹配时不采集）
- [X] T016 [P] [US1] 为每次 3D/F 正式握手、F 失败锁停、重复/歧义拒绝和唯一 F 成功后才 plan/bind 编写测试到 `backend/tests/Gaode.Contracts.Tests/Station01/ThreeDAndFRecipeGateTests.cs`（来源：FR05、验收5；原则：P03/P04/P07/P11；依赖：T009；完成/证据：失败路径无 RecipeRunPlan/绑定/handoff，成功路径顺序唯一且可追溯）
- [X] T017 [P] [US1] 为配置能力校验、Test/Production 用途、运行快照冻结和运行中发布/回滚隔离编写测试到 `backend/tests/Gaode.Contracts.Tests/Configuration/Station01RunConfigurationTests.cs`、`backend/tests/Gaode.Contracts.Tests/Recipes/RecipeRunPlannerTests.cs` 和 `backend/tests/Gaode.Contracts.Tests/Station01/StartClampStepTests.cs`（来源：FR02/FR05/FR06/FR11、验收5；原则：P03/P08/P11；依赖：T009；完成/证据：未知/不兼容能力与脚本被拒绝，算法未就绪不被误判为配置错误，后续版本不改在途快照；clampCompletion 缺失/非正值被拒绝，预算版本和用途冻结；既有 Test 5000 ms 窗口从受理事实提交后进入 WaitingClamp 计时，可控时钟断言 4999 ms 同代次状态 1 可继续、5000 ms 不可继续，状态 0/未定义值到期为 ClampTimeout/UnknownHeld、状态 2 立即锁停、断联/代次变化为设备未知）

### 后端实现

- [X] T018 [P] [US1] 实现 3 秒心跳互锁、报警/Restricted 状态和禁止自动恢复到 `backend/src/Gaode.Infrastructure/Devices/Plc/PlcConnectionPump.cs`、`backend/src/Gaode.Application/Station01/PhysicalFaultPolicy.cs` 和 `backend/src/Gaode.Domain/Station01/RunSnapshot.cs`（来源：FR03、验收5；原则：P04/P06/P07；依赖：T014；完成/证据：T014 通过，心跳监测不等待算法/SQLite/通知，重连/复位只追加事实不续跑）
- [X] T019 [P] [US1] 实现 XYZ 双状态/三坐标核验及 Inspection/Z_Reset 完整置位清零顺序到 `backend/src/Gaode.Application/Station01/Steps/FixedMoveStep.cs`、`backend/src/Gaode.Application/Ports/IInspectionHandshakePort.cs` 和 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`（来源：FR04、验收5；原则：P03/P04/P07；依赖：T015；完成/证据：T015 通过，每次移动使用相同 operation/epoch，结果持久化前不置 2，Z_Reset_Status 非 2 不清零）
- [X] T020 [US1] 实现 3D/F 每次握手、F 失败锁停和唯一 F 成功门禁到 `backend/src/Gaode.Application/Station01/Steps/ThreeDStep.cs`、`backend/src/Gaode.Application/Station01/Steps/FScanStep.cs`、`backend/src/Gaode.Domain/Station01/FCodePolicy.cs` 和 `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`（来源：FR05、验收2/5；原则：P03/P04/P07；依赖：T016、T019；完成/证据：T016 通过，失败/重复/歧义 F 不加载或绑定配方且保持锁停）
- [X] T021 [P] [US1] 接通配置加载、能力校验、冻结快照及自动 plan/bind 到 `backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs`、`backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs`、`backend/src/Gaode.Application/Configuration/ConfigurationFreezer.cs`、`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs` 和 `backend/src/Gaode.Application/Station01/Steps/StartClampStep.cs`（来源：FR02/FR05/FR06/FR11、验收2/7；原则：P03/P08/P11；依赖：T017；完成/证据：T017 通过，正式/Test 配方共用逻辑，Production 不隐藏回退 Simulated；夹紧观察复用冻结的 businessMs.clampCompletion 及既有 ResponseBeforeDeadline，不硬编码 5000，不另建超时框架）
- [X] T022 [US1] 扩展唯一启动 API 以解析并持久受理 `StartRunContext`，返回 202 command/run 引用且不等待流程完成到 `backend/src/Gaode.Host/Api/Station01ApiContracts.cs`、`backend/src/Gaode.Host/Api/RunEndpoints.cs` 和 `backend/src/Gaode.Application/Station01/CommandRegistry.cs`（来源：FR02/FR11、验收2；原则：P05/P07/P08；依赖：T018、T020、T021；完成/证据：API 合同测试证明无第二入口、无临时身份、202/HandoffReady 均不等于 Completed）
- [X] T023 [US1] 为唯一入口到 `s01-handoff/2.0` 的连续身份、plan revision、媒体/结果引用和提交前禁止 Detection 编写集成测试到 `backend/tests/Gaode.Integration.Tests/Station01/PublicPreparationHandoffV2IntegrationTests.cs`（来源：FR05/FR11、验收2/7；原则：P07/P08/P09；依赖：T013、T022；完成/证据：使用 003 侧持久 fixture 可先验证 consumer，测试证明预造 plan/request 与仅内存通知被拒绝）
- [X] T024 [US1] 在 `GATE-001-HANDOFF-V2` 已满足后接通 v2 producer/consumer 与提交后自动续接到 `backend/src/Gaode.Application/Station01/StageHandoffBuilder.cs`、`backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`、`backend/src/Gaode.Application/Station01/CompletePublicPreparation.cs` 和 `backend/src/Gaode.Host/Lifecycle/Station01HostedService.cs`（来源：FR11、验收2；原则：P01/P05/P07/P08；依赖：T023、GATE-001-HANDOFF-V2；完成/证据：只使用已对齐的001 handoff直接相关合同，已提交 v2 handoff 才触发同一 Host consumer，且没有第二次启动；不扩展001其他需求）
- [X] T025 [US1] 将 HandoffReady 保持为非终态并使运行查询从持久投影读取配置/配方/handoff 状态到 `backend/src/Gaode.Domain/Station01/RunState.cs`、`backend/src/Gaode.Domain/Station01/RunSnapshot.cs` 和 `backend/src/Gaode.Host/Api/QueryEndpoints.cs`（来源：FR07/FR11、验收2/7；原则：P05/P07/P08；依赖：T024；完成/证据：快照不再硬编码 NotStarted，不存在 handoff 被映射为 FinalUnloadCompleted 的路径）
- [X] T026 [US1] 执行 US1 的规则、合同和 SQLite 集成测试并将实际命令、版本、结果及 `GATE-001-HANDOFF-V2` 状态写入 `specs/003-plc-latest-protocol/validation.md`（来源：FR01–FR07/FR11、验收2/5/7；原则：P01/P09；依赖：T018–T025；完成/证据：测试结果文件与日志可复核，只声明“入口到 committed handoff”，不声明第一工位最终完成）

---

## Phase 4：US2——Detection、Sorting 与 UnloadPreparation（P1）

**目标**：从合法、已提交的 v2 handoff 构造 DetectionRequest，经正式算法/PLC 端口依次提交三个阶段完成事实；模拟实现共用正式路径。  
**独立完成条件**：使用持久 handoff fixture 和正式端口合同即可证明 Detection→Sorting→UnloadPreparation 的状态、重试、Pending/MappingFailed/UnknownHeld 行为，无需 001 producer、006 或前端。  
**相关需求与原则**：FR01/FR02/FR07/FR08/FR10/FR13；验收3/4/6/7/8；P04–P11。

### 适用的软件验证

- [X] T027 [P] [US2] 为 Detection 的通信 4 次、1/2/4 秒退避、算法超时 3 次及 2/5 秒退避、共享 120 秒期限及超限 Pending 编写端口/执行器测试到 `backend/tests/Gaode.Contracts.Tests/Workflow/DetectionRetryAndPendingTests.cs`（来源：FR08/FR13、验收6；原则：P04/P06/P09；依赖：T005、T013；完成/证据：原始错误、attempt、deadline、input/result references 均可核对；分别注入通信耗尽、算法超时耗尽、共享期限先到，均断言受影响已知对象 Pending、映射完整后继续正式分拣，身份歧义仍 MappingFailed，失败不变 OK/UnknownHeld）
- [X] T028 [P] [US2] 为 Sorting/UnloadPreparation/UnlockObservation 的正式点位、operation/epoch 匹配、派发前通信重试和可能已派发直接 UnknownHeld 编写合同测试到 `backend/tests/Gaode.Contracts.Tests/Ports/PlcStageActionPortContractTests.cs` 和 `backend/tests/Gaode.Contracts.Tests/Devices/LatestPlcProtocolTests.cs`（来源：FR01/FR02/FR13、验收3/4/5；原则：P04/P07/P09；依赖：T005、T013；完成/证据：4x0020–22、MoveCmd=4/PosConfirmed=1、Cmd=0/Status=0 均覆盖，写后断联动作计数保持 1；逐项对照 FR01 的 Word 权威点表验证地址/方向/类型，验证 PC 不写 PLC 所有信号、Float32 占相邻两个寄存器、各支持字序使用已知数值及预期寄存器对进行双向编解码断言，保存点表行与用例对应证据；仅做合同验证，不扩展其他工位实现）
- [X] T029 [P] [US2] 为逐对象 OK/NG/Pending 映射、Pending 区动作和缺失/重复/歧义 `MappingFailed` 编写规则测试到 `backend/tests/Gaode.Contracts.Tests/Workflow/RecipeSortingMapperTests.cs`（来源：FR08、验收4/6；原则：P04/P07；依赖：T013；完成/证据：Pending 必须生成分拣动作，MappingFailed 禁止任何部分派发）
- [X] T030 [P] [US2] 为独立 VirtualPlc 的心跳、夹紧、运动、分拣、下料、解锁、失败、断联和 epoch 可观察性编写协议测试到 `backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs`（来源：FR01–FR04/FR10/FR12；原则：P05/P09；依赖：T001；完成/证据：测试仅经 Modbus 协议断言设备事实，不允许 VirtualPlc 创建 Detection/业务完成/人工确认/数据库记录）
- [X] T031 [P] [US2] 为 Simulated/Test 相机、光源、算法的输入引用、组件 source/quality/version/evidence 和 Production 无隐藏回退编写合同测试到 `backend/tests/Gaode.Contracts.Tests/Simulation/DetectionAdapterSourceTests.cs`（来源：FR08/FR10、验收6/8；原则：P04/P09/P11；依赖：T003、T013；完成/证据：任一模拟组件保持自身来源，NotIntegrated/NotReady 不伪造成功）

### 后端实现

- [X] T032 [US2] 扩展正式 Detection/PLC 请求结果合同以携带对象错误、attempt、deadline、组件证据和派发判定到 `backend/src/Gaode.Application/Ports/StagePortContracts.cs` 和 `backend/src/Gaode.Application/Workflow/StageEventing.cs`（来源：FR07/FR08/FR13；原则：P05/P07/P09；依赖：T027、T028、T031；完成/证据：对应合同测试编译通过，Detection 不能调用 PLC 端口且结果不直接写业务投影）
- [X] T033 [P] [US2] 实现明确标记的 `SimulatedDetectionPort` 并复用正式相机/光源/算法输入输出到 `backend/src/Gaode.Infrastructure/Simulation/SimulatedDetectionPort.cs`、`backend/src/Gaode.Infrastructure/Simulation/ResponsePolicy.cs` 和 `backend/src/Gaode.Application/Acquisition/CaptureEvidenceGate.cs`（来源：FR08/FR10、验收6/8；原则：P04/P06/P09；依赖：T032；完成/证据：T027/T031 通过，通信重试耗尽/期限先到与算法超时耗尽均输出正式逐对象 Pending 和原始异常/source/quality，由 Workflow 校验映射并推进分拣；空/缺失媒体不伪造结果，适配器不推进 PLC 或完成状态）
- [X] T034 [US2] 实现逐对象 Pending 保留原异常及冻结计划到有序分拣动作的唯一映射到 `backend/src/Gaode.Application/Workflow/RecipeSortingMapper.cs`、`backend/src/Gaode.Domain/Station01/SortingMappingContracts.cs` 和 `backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`（来源：FR08、验收4/6；原则：P04/P07；依赖：T029、T033；完成/证据：T027/T029 通过，MappingFailed 暂停且无默认放行）
- [X] T035 [P] [US2] 实现复用唯一 Modbus 连接的 `LatestProtocolStageActionAdapter` 及 UnknownHeld 分支到 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs` 和 `backend/src/Gaode.Application/Station01/Steps/PalletUnlockStep.cs`（来源：FR01/FR02/FR13、验收3/4/5；原则：P04/P05/P07/P09；依赖：T032；完成/证据：T028 通过，复用 ProtocolLatestMap/PlcProtocolCodec 并验证点位方向/类型、Float32 两寄存器及配置字序；仅明确未派发的通信错误重试，可能已派发时零自动重发并保持占用）
- [X] T036 [P] [US2] 仅按正式协议缺口补齐现有 VirtualPlc 的三阶段事实和故障注入到 `VirtualPlc/VirtualPlcEngine.cs`、`VirtualPlc/SimulationModels.cs`、`VirtualPlc/PlcDataStore.cs` 和 `VirtualPlc/appsettings.json`（来源：FR10/FR12、VirtualPlc 边界；原则：P05/P09；依赖：T030；完成/证据：T030 通过，不新增 PLC 客户端/业务编排/人工确认/数据库访问；若无缺口则记录 NotNeeded 及协议证据）
- [X] T037 [US2] 按固定顺序编排 Detection→Sorting→UnloadPreparation 并提交各阶段意图/反馈到 `backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`、`backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs` 和 `backend/src/Gaode.Application/Workflow/StageEventing.cs`（来源：FR07/FR08/FR13、验收3/4/6/7；原则：P04–P08；依赖：T034、T035、T036；完成/证据：三个 Completed 均来自端口事实；Pending 继续，MappingFailed/UnknownHeld 停在正确阶段）
- [X] T038 [US2] 在 Host 组合根显式注册 Detection、PLC stage、编排器及 Real/Virtual/Simulated 选择到 `backend/src/Gaode.Host/Composition/AdapterBindings.cs`、`backend/src/Gaode.Host/Composition/Station01Registration.cs`、`backend/src/Gaode.Host/Composition/Station01RuntimeOptions.cs` 和 `backend/src/Gaode.Host/appsettings.VirtualPlc.json`（来源：FR06/FR10、验收1/8；原则：P05/P09/P11；依赖：T033、T035、T037；完成/证据：Production 未接入返回 NotIntegrated/NotReady，不自动回退；VirtualPlc 只经正式 Modbus）
- [X] T039 [US2] 从持久化 v2 handoff fixture 执行三阶段并验证事件、Pending/MappingFailed/UnknownHeld 和 SQLite 事实到 `backend/tests/Gaode.Integration.Tests/Station01/ThreeStageMainFlowIntegrationTests.cs`（来源：FR07/FR08/FR10/FR13、验收3/4/6/7/8；原则：P07–P09；依赖：T011、T037、T038；完成/证据：不依赖 T024、006 或前端即可完成；通信耗尽及期限先到用例必须核对 Pending/原始异常落库、完整映射后的正式 Pending 分拣动作与匹配反馈，不能仅断言不为 OK；证据明确停在三阶段事实，尚不宣称 FinalUnloadCompletion）

---

## Phase 5：US3——聚合、恢复、后端 API/通知与最终完成（P1）

**目标**：从三个已提交阶段事实生成来源矩阵和 WholeTrayCompletion，完成解锁观察、受控人工确认、恢复决策和 FinalUnloadCompletion。  
**独立完成条件**：使用持久化三阶段 fixture，通过 SQLite/API/SignalR 集成测试证明矩阵、事务、门禁、恢复和最终状态，无需 006 或页面。  
**相关需求与原则**：FR02/FR07/FR09/FR10/FR13；验收3/4/7/8/9；P05/P07–P09/P12。

### 适用的软件验证

- [X] T040 [P] [US3] 为意图+投影、反馈+投影、三阶段聚合和人工确认+最终完成四类短事务编写 SQLite 故障注入测试到 `backend/tests/Gaode.Integration.Tests/Storage/StageAndCompletionTransactionTests.cs`（来源：FR13、验收7；原则：P07/P08；依赖：T011、T039；完成/证据：回滚点证明无跨设备事务、无部分可见 WholeTrayCompletion 或“确认已见但最终完成缺失”）
- [X] T041 [P] [US3] 为 ReadyForUnlock/Final 矩阵存取、混合来源非 Real 和必要来源缺失阻断编写存储/API 合同测试到 `backend/tests/Gaode.Integration.Tests/Storage/ComponentSourceMatrixStoreTests.cs` 和 `backend/tests/Gaode.Integration.Tests/Api/Station01SourceMatrixApiTests.cs`（来源：FR10/FR13、验收7/8；原则：P07/P08/P09；依赖：T003、T011、T039；完成/证据：Virtual PLC + Simulated 算法输出 `SoftwareLoopOnly`，Unknown/Missing/Unverifiable 时无 WholeTrayCompletion）
- [X] T042 [P] [US3] 为重启/重连/复位不自动重检报废续跑、旧任务等待受控决定和已完成动作不重放编写恢复测试到 `backend/tests/Gaode.Integration.Tests/Station01/ControlledRecoveryTests.cs`（来源：FR07、验收9；原则：P07/P08/P09；依赖：T007、T011、T039；完成/证据：无决定返回受限；决定记录 actor/time/reason/originalTask/evidenceRefs；决定本身不制造完成）

### 持久化、API 与通知实现

- [X] T043 [US3] 将 IntentRecorded/AttemptFailed/RetryScheduled/PendingRecorded/deadline/组件引用与投影分别原子提交到 `backend/src/Gaode.Infrastructure/Persistence/StageEventStore.cs`、`backend/src/Gaode.Application/Workflow/StageEventing.cs` 和 `backend/src/Gaode.Infrastructure/Persistence/Station01EntityConfigurations.cs`（来源：FR07/FR08/FR13、验收6/7；原则：P04/P07/P08；依赖：T040；完成/证据：调用 PLC/相机/算法期间无数据库事务，保存失败不推进公开状态）
- [X] T044 [US3] 在独立聚合短事务中核验三阶段、身份、plan revision、未解决终态及五类当前必需组件并原子保存 WholeTrayCompleted/WholeTrayCompletion/ReadyForUnlockSourceMatrix 到 `backend/src/Gaode.Infrastructure/Persistence/WholeTrayCompletionStore.cs`、`backend/src/Gaode.Application/Workflow/WholeTrayCompletionContracts.cs` 和 `backend/src/Gaode.Domain/Station01/StageOperationContracts.cs`（来源：FR02/FR13、验收3/7/8；原则：P07/P08/P09；依赖：T040、T041、T043；完成/证据：T040/T041 通过，ManualActor 仅为 NotYetRequired，裸 bool/任意 GUID/单一 Real 不授权解锁）
- [X] T045 [US3] 将 ManualTrayRemovalConfirmed、认证 ManualActor 的 FinalSourceMatrix 和 FinalUnloadCompletion 改为同一幂等短事务到 `backend/src/Gaode.Infrastructure/Persistence/WholeTrayCompletionStore.cs`、`backend/src/Gaode.Application/Workflow/WholeTrayCompletionContracts.cs` 和 `backend/src/Gaode.Domain/Station01/StageOperationContracts.cs`（来源：FR09/FR13、验收3/7/8；原则：P07/P08/P09；依赖：T044；完成/证据：移除硬编码 `ResultSource.Virtual`，原 ReadyForUnlock 矩阵不回写，最终六组件均可核验）
- [X] T046 [P] [US3] 持久化 `ControlledRecoveryDecision` 并实现旧任务恢复授权查询到 `backend/src/Gaode.Infrastructure/Persistence/ControlledRecoveryDecisionStore.cs`、`backend/src/Gaode.Application/Workflow/ControlledRecoveryService.cs` 和 `backend/src/Gaode.Infrastructure/Persistence/Station01DbContext.cs`（来源：FR07、验收9；原则：P05/P07/P08；依赖：T007、T011、T042、T043；完成/证据：T042 通过，无决定时 ReDetect/Scrap 拒绝，普通恢复不自动创建决定）
- [X] T047 [US3] 在 Host 生命周期恢复中只重建已提交投影、去重 Completed，并把可能已派发无终态动作置 UnknownHeld 到 `backend/src/Gaode.Application/Workflow/ThreeStageRecoveryService.cs` 和 `backend/src/Gaode.Host/Lifecycle/Station01HostedService.cs`（来源：FR03/FR07/FR13、验收4/9；原则：P07/P08/P09；依赖：T042、T043、T046；完成/证据：重启/重连/复位不重置 deadline、不重放动作、不自动重检/报废/续跑）
- [X] T048 [US3] 扩展运行查询 DTO/endpoint 返回完整阶段、矩阵引用/摘要、blockedComponents 和 persistedRevision 到 `backend/src/Gaode.Host/Api/Station01ApiContracts.cs`、`backend/src/Gaode.Host/Api/QueryEndpoints.cs` 和 `backend/src/Gaode.Domain/Station01/RunSnapshot.cs`（来源：FR07/FR09/FR10、验收3/7/8；原则：P05/P07/P09/P12；依赖：T041、T044、T045；完成/证据：GET 为事实源，不把混合来源压成 Real，不把 HandoffReady/WholeTrayCompletion/ObservedUnlocked 映射为最终完成）
- [X] T049 [US3] 实现人工移除确认和旧任务恢复决定的受控 API、鉴权、幂等与中文错误映射到 `backend/src/Gaode.Host/Api/RunEndpoints.cs`、`backend/src/Gaode.Host/Api/Station01Authorization.cs`、`backend/src/Gaode.Host/Api/Station01ErrorMapping.cs` 和 `backend/src/Gaode.Host/Api/Station01ApiContracts.cs`（来源：FR07/FR09、验收3/9；原则：P02/P05/P07/P08；依赖：T045、T046、T048；完成/证据：actor 只取认证上下文，ObservedUnlocked 前返回 409，存储失败返回 503，202 不替代最终 GET）
- [X] T050 [US3] 在事务提交后发布矩阵引用和阶段 revision，并保留 GET 对账到 `backend/src/Gaode.Host/Api/Station01NotificationService.cs`、`backend/src/Gaode.Host/Api/Station01Hub.cs` 和 `backend/tests/Gaode.Integration.Tests/Api/Station01MainFlowNotificationTests.cs`（来源：FR07/FR10、验收7/8；原则：P05/P06/P08/P09；依赖：T048、T049；完成/证据：重复/乱序/遗漏通知不影响控制，通知不生成设备或完成事实且不依赖前端）
- [X] T051 [US3] 从持久化三阶段 fixture 经 WholeTrayCompletion、解锁读回和 Host API 人工确认到 FinalUnloadCompletion 编写后端集成测试到 `backend/tests/Gaode.Integration.Tests/Station01/FinalUnloadCompletionIntegrationTests.cs`（来源：FR02/FR09/FR10/FR13、验收3/7/8；原则：P07–P09；依赖：T045、T047–T050；完成/证据：同一 run/tray/plan/epoch 引用连续，最终矩阵六组件可核验，只有 FinalUnloadCompletion 将运行置完成）

---

## Phase 6：US4——五类独立进程 E2E 与证据（P1，交付门禁）

**目标**：使用真实 Host、独立 VirtualPlc、正式 Modbus TCP、真实 SQLite 和明确标记的 Simulated/Test 相机/光源/算法，执行五类场景。  
**独立完成条件**：每场景均有 Host/VirtualPlc 进程、API transcript、Modbus audit、SQLite 事件/投影、source matrix、final result 六类证据；任一缺失即 Failed/Blocked/NotRun。  
**相关需求与原则**：FR10/FR12/FR13；验收1–9；P07–P10/P12。

- [X] T052 [US4] 扩展联调客户端为五场景驱动、隔离端口/数据库和六类证据清单到 `scripts/verify-latest-plc.py`（来源：FR10/FR12、quickstart；原则：P05/P08/P09；依赖：T038、T048–T050；完成/证据：支持 `normal|algorithm-pending|unlock-gates|plc-unknown|host-restart|all`，只调用 Host API；不依赖 T024、006、前端或直接写库）
- [X] T053 [P] [US4] 在 `GATE-001-HANDOFF-V2` 满足后运行正常完整闭环并保存六类证据到 `artifacts/plc-latest/<evidence-run-id>/whole-tray/normal/`（来源：FR10–FR13、验收1–3/5/7/8；原则：P07–P09；依赖：T024、T051、T052；完成/证据：同一 run/tray 从唯一入口到 FinalUnloadCompletion，完成前无 Cmd=0、启动无 Cmd=1，final-result 明确 SoftwareLoopOnly）
- [X] T054 [P] [US4] 运行算法超时最多 3 次/2-5 秒→Pending→正式 Pending 分拣→最终完成场景并保存六类证据到 `artifacts/plc-latest/<evidence-run-id>/whole-tray/algorithm-pending/`（来源：FR08/FR10/FR12/FR13、验收4/6/8；原则：P04/P07/P09；依赖：T024、T051、T052；完成/证据：原错误、attempt/deadline、对象映射、分拣动作、source matrix 和 FinalUnloadCompletion 均可核对）
- [X] T055 [P] [US4] 运行 WholeTrayCompletion 前拒绝解锁、ObservedUnlocked 前拒绝人工确认及来源缺失拒绝聚合场景并保存六类证据到 `artifacts/plc-latest/<evidence-run-id>/whole-tray/unlock-gates/`（来源：FR02/FR09/FR12/FR13、验收3/4/7/8；原则：P04/P07–P09；依赖：T024、T051、T052；完成/证据：无非法 Cmd=0/完成记录，API 返回合同错误，Unknown/Missing 组件无 WholeTrayCompletion）
- [X] T056 [P] [US4] 在 Sorting/UnloadPreparation/UnlockObservation 的动作可能已派发后注入断联或 epoch 变化并保存六类证据到 `artifacts/plc-latest/<evidence-run-id>/whole-tray/plc-unknown/`（来源：FR03/FR12/FR13、验收4/5；原则：P04/P07/P09；依赖：T024、T051、T052；完成/证据：对应阶段 UnknownHeld、设备占用保持、动作计数证明零自动重发；派发前通信 4 次/1-2-4 秒由合同测试引用补证）
- [X] T057 [P] [US4] 在已提交边界和在途无终态窗口重启 Host 并保存六类证据到 `artifacts/plc-latest/<evidence-run-id>/whole-tray/host-restart/`（来源：FR03/FR07/FR12/FR13、验收4/7/9；原则：P07–P09；依赖：T024、T051、T052；完成/证据：已完成动作不重放、deadline 不重置、物理未知不执行、无人工决定不自动重检/报废/续跑）
- [X] T058 [US4] 执行 `dotnet test backend/Gaode.slnx`、构建/启动 Host 与 VirtualPlc、汇总五场景结果并写入 `specs/003-plc-latest-protocol/validation.md`（来源：FR01–FR13、验收1–9；原则：P01/P09/P12；依赖：T053–T057；完成/证据：记录实际命令/版本/配置/退出码/TRX/manifest 哈希；不运行或依赖前端测试，历史局部 passed 不复用）

---

## Phase 7：收尾、Deferred 登记与范围审计

- [X] T059 将其他工位、真实设备/算法生产验收、006 页面、生产人工确认 UI、MES/模型/样本、五场景外压力/长稳和 OPEN-16/22/26 生产规则仅登记为 Deferred/NotRun 到 `specs/003-plc-latest-protocol/validation.md` 和 `specs/003-plc-latest-protocol/differences.md`（来源：Deferred 功能列表；原则：P01/P02/P09/P10/P12；依赖：T001；完成/证据：明确本任务是行政登记、不是 US1–US4 完成条件，且没有对应实现任务或伪造通过结论）
- [X] T060 核对 spec、plan、contracts、data-model、quickstart、tasks 与实际证据并记录最终一致性审计到 `specs/003-plc-latest-protocol/validation.md`（来源：FR01–FR13、验收1–9；原则：P01–P12；依赖：T058；完成/证据：无越界路径、无 006/前端依赖、无硬编码成功/跳状态/隐藏降级/无限重试；最终成功只来自同一 run/tray 的 FinalUnloadCompletion）

---

## 用户故事依赖图

```text
Phase 1 范围与协调门禁
    ↓
Phase 2 可独立后端基础
    ├──────────────→ US2 三阶段端口/编排（不依赖 001 producer）
    │                         ↓
    │                    US3 聚合/API/最终完成
    │                         ↓
    └→ US1 公共准备 ──GATE-001──→ 实际 handoff 接线
                                  ↓
                     US4 五场景独立进程 E2E
```

- T002–T023、T027–T052 不依赖 `GATE-001-HANDOFF-V2`，可先完成领域、端口、存储、API/通知和 E2E 驱动。
- 2026-09-23有限授权下001 handoff直接相关规格产物已对齐；T024–T026仍须实际接线和验证，不得以授权或文档对齐代替完成。
- 006、前端代码、原型测试和生产 UI 不在依赖图中。
- US1 的 handoff、US2 的三个 Completed、US3 的 WholeTrayCompletion/ObservedUnlocked 都不是本期终点；只有 US3/US4 中已提交的 FinalUnloadCompletion 是终点。

## 并行执行示例

- Foundation：T002、T004、T006、T008、T010、T012 可并行编写不同合同测试；实现按各自测试收敛。
- US1：T014–T017 可并行；T018、T019、T021 修改不同模块可并行，T020 在运动握手完成后收敛。
- US2：T027–T031 可并行；T033、T035、T036 在 T032/各自合同完成后可并行，最终由 T037/T038 汇合。
- US3：T040–T042 可并行；T046 可与 T044 的聚合实现并行，但 T047/T049 在二者完成后汇合。
- US4：T053–T057 必须使用不同端口、数据库和证据目录后才可 `[P]` 并行；T058 统一汇总。

## 关键规则覆盖

| 规则 | 对应任务ID | 预期证据 |
| --- | --- | --- |
| FR03：3 秒心跳、锁动作、报警、重连/复位不续跑 | T014、T018、T056、T057 | 可控时钟测试、运行投影、Modbus audit、动作计数 |
| FR04：XYZ 双状态/三坐标、Inspection 1→2→0、Z Reset、新移动重置 | T015、T019、T053 | 协议顺序断言和正常场景 Modbus audit |
| FR05：每次 3D/F 握手、F 失败锁停、唯一 F 后 plan/bind | T016、T020–T023、T053 | 合同测试、API transcript、handoff 引用 |
| FR07：旧任务等待受控 ReDetect/Scrap 决策 | T006、T007、T042、T046、T047、T049、T057 | 决策记录和无决定拒绝/不自动续跑证据 |
| 通信 4 次、1/2/4 秒、120 秒不重置 | T004、T005、T027、T028、T056 | 可控时钟事件链和 E2E 引用证据 |
| 算法超时 3 次、2/5 秒、Pending 继续 | T004、T005、T027、T033、T034、T054 | attempts/deadline/error/Pending/分拣证据 |
| PLC 可能已派发直接 UnknownHeld，零自动重发 | T004、T005、T028、T035、T037、T056 | operation/epoch、动作计数和 held 投影 |
| 六组件来源矩阵；模拟不聚合 Real；缺失来源阻止完成 | T002、T003、T031、T041、T044、T045、T048、T053–T055 | 规则/存储/API 测试和 source-matrix.json |
| 分段短事务与原子完成 | T010、T011、T040、T043–T045 | 故障注入、SQLite events/projections、无部分状态 |
| 唯一入口到 FinalUnloadCompletion | T022–T026、T037–T051、T053 | 同一 run/tray 完整事件链 |
| 006/前端/原型不阻塞 | T001、T052、T058–T060 | 依赖审计、仅 Host API 的 E2E transcript |

## 任务追溯汇总

每个任务行已包含来源、原则、依赖和完成证据。以下为阶段级汇总：

| 范围 | 任务ID | 主要需求 | 完成出口 |
| --- | --- | --- | --- |
| 范围与基础 | T001–T013 | FR07/08/09/10/11/13 | 可独立开发的领域、端口、SQLite 和消费合同 |
| US1 | T014–T026 | FR01–FR07/FR11 | 唯一入口到 committed handoff，仍非最终完成 |
| US2 | T027–T039 | FR01/02/07/08/10/13 | 三阶段真实端口事实和正确异常出口 |
| US3 | T040–T051 | FR02/07/09/10/13 | FinalUnloadCompletion、矩阵、API/通知和恢复门禁 |
| US4 | T052–T058 | FR10/12/13 | 五场景独立进程六类证据 |
| Deferred/审计 | T059–T060 | Deferred、验收1–9 | 只登记范围并审计实际交付 |

| OPEN/外部依赖 | 只限制的任务 | 不受影响的任务 |
| --- | --- | --- |
| `GATE-001-HANDOFF-V2` 单独授权与 producer 合同对齐 | T024–T026、T053–T057 的实际跨 handoff 执行 | T002–T023、T027–T052、T059 |
| 006 对齐和客户新原型批准 | 仅未来独立前端 feature；本清单无任务 | T001–T060 全部后端/证据工作 |
| OPEN-16/22/26 与正式设备参数 | 对应生产融合、兼容性和未知 Z 安全分支 | 明确测试配置下的全部软件闭环任务 |
| 真实 PLC/相机/光源/算法 Worker | 真机、精度、节拍和生产验收 | Host + VirtualPlc + Simulated/Test 的五场景软件 E2E |

## 实施策略与 MVP

1. **工程增量 1**：Phase 1–2，先完成无跨 feature 依赖的领域、合同和持久化基础。
2. **工程增量 2**：US2/US3 可用持久化 fixture 独立推进端口、编排、SQLite、API/通知与 E2E 驱动，不等待 001 或 006。
3. **工程增量 3**：`GATE-001-HANDOFF-V2` 满足后完成 US1 实际接线，把同一 run/tray 从唯一入口接入后续流程。
4. **本期交付 MVP**：必须完成 US1–US4，并通过五类独立进程场景；仅 handoff、WholeTrayCompletion、ObservedUnlocked 或页面状态均不得宣称完成。
5. **Deferred**：T059 只登记，不是本期主流程完成条件；生产 UI、真机和其他工位继续 Deferred。

## 客户确认原型检查（P12）

本清单不创建任何页面实现或原型测试任务。`E:\dzk\gaode\原型.zip` 及 `a.html`、`data-view.html`、`login.html` 保持只读；生产人工确认 UI 等待独立 feature、新原型版本、新哈希和明确批准。003 的人工确认仅由测试/联调客户端通过 Host API 完成。

## 2026-09-23 共享媒体合同责任记录

上述按runId媒体清单合同由003共有API文档定义，具体007固定图片身份接线、Host查询和验证归007新增任务；003 T001–T060原勾选、编号及历史验证范围不变。006仅消费公开合同并负责原型格位绑定。

## 2026-09-23 VirtualPlc 清零与监控 Flow-back（T061 已完成，T062 待页面验证）

既有T001–T060及原始证据保持历史范围。T061 已由正式 Modbus 3/3 定向测试验证（`artifacts/plc-latest/t061-20260923-223458/manifest.json`）；T062 的接口与页面逻辑已有定向证据，但实际监控页面操作尚未验证。上述证据均不构成007完整闭环通过。

- [X] T061 [US1/US4] 对应FR04/FR10、验收2/5：按最新版协议§3.1.7最小修正Host正式PLC适配器的下一运动门禁，允许上一轮`XY_Pos_Confirmed(4x0002)=1`在清旧`XY_Move_Cmd(4x0001)=0`后保持；有上一轮检测时仍须核验`Z_Reset_Status(4x0053)=2`后才清`Inspection_Status(4x0052)=0`，并保留坐标、连接代次及安全条件。核对同一适配器依赖`4x0002=0`的相邻门禁，只改实际受影响处；不得把旧1当作新一轮到位或盲重发。同步修正`backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs`中“清4x0001后4x0002=0”的旧断言，定向验证清命令期间保持1、4x0053=2后才清4x0052、下一合法运动实际受理时才开始新反馈、连续两次运动各自匹配本轮坐标/状态，以及F检测复位后的配方绑定能按正式端口完成。（依赖：本次FR04及边界合同；完成/证据：Host正式Modbus与VirtualPlc连续运动及F后配方绑定定向测试、点位写入序列可核对；不重跑003历史五场景，不宣称007 T015通过。）
- [ ] T062 [US4] 对应FR14：针对已先行修改的VirtualPlc本地监控补定向证据，并最小纠正`VirtualPlc/wwwroot/app.js`把实际数值0显示成“空”的既存矛盾；核对`/api/simulator/changes?after=`按写入序号返回中间变化、最近变化排除心跳、有限缓存游标落后时`gap=true`及页面提示，当前状态仍显示数值0。只测本地诊断，不新增PLC点位、业务控制或无限期日志。（依赖：本次FR14及边界合同；完成/证据：隔离查询与页面记录、源码/配置版本和测试结果分列；不能替代T061或007完整联调。）

## 2026-09-24 FR15设备故障诊断增量

- [X] T063 [US1/US4] 对应FR15、FR03/FR07、宪章P04/P07/P09/P13：在正式Host/Modbus边界记录首次通信请求与失败、心跳最后有效翻转及过期、陈旧状态、协议与冻结配置版本、动作请求/匹配反馈、`requestId/commandId/runId/operationId/connectionEpoch`和实际UnknownHeld或拒绝处置；底层异常保留受控原始上下文，高频重复日志限频或聚合并持久可查。依赖001 FR-041的启动关联及既有003协议/状态合同；完成条件为首次正式通信失败、同代次明确不安全对照、心跳/状态过期的定向Test验证，保存原始Host/VirtualPlc日志、协议审计、API状态及关联查询，证明两类安全判断不混同、无旧值放行和盲重发。VirtualPlc证据只证明软件联调，真实PLC另行验证；T001–T062历史勾选及FR12五场景证据不自动算本任务通过。本次不改PLC点位或协议来源。2026-09-24后续按 `validation.md` 的扫描丢边沿定向复现/修复、17/17组合与有限重复、两类独立进程/页面故障证据，在Test/VirtualPlc范围完成；真实PLC及原始人工故障另判。
2026-09-24 T063执行注记：新增独立扫描定向测试与有界启动轨迹，先修VirtualPlc上升沿在Modbus写入和扫描之间丢失，再核验正常/诊断/安全回归；仍按T063原完成条件判定，不能由一次绿灯直接勾选。

## 2026-09-24 下料协议缺陷修复增量

- [X] T064 [US1/US4] 对应FR16–FR18、宪章P03/P04/P07–P09/P13：将冻结下料XYZ和Test/Production来源贯通配置、编排、阶段动作端口与正式Modbus；完成先行Z复位门禁、Camera_Target_X/Y/Z先写再发命令4、只读Machine_Current_Pos_X/Y/Z并按批准目标/容差校验。删除错误的前置XY=0及清命令后等待XY=0，但旧1、同目标、断联/epoch变化均不得假完成；VirtualPlc命令4只动XY，不伪造Z反馈。完成依据为修订合同测试、隔离正式Modbus正常与必要失败回归、日志及SQLite/协议证据；原始人工页面和真机须独立验收，不复用T063勾选。2026-09-24本轮Test/VirtualPlc证据：`artifacts/station01-007/unload-fix-20260924-b/unload-fix-contracts.trx`（31/31）、同目录Host/PLC审计/GET/SQLite及退出后`diagnostic-index.json`；真机与原始页面未验收。

- [ ] T065 [US1] 对应FR03/FR15心跳应答延迟缺陷：保留正式3秒互锁及独立连接，记录异常窗口中Host轮询调度、Modbus事务读写、VirtualPlc接收/处理/响应和有效应答的有界UTC/单调证据；以修复前后定向对照验证实际延迟机制的最小调度修正，真实超时仍锁动作且无盲重发。仅本缺陷Test/VirtualPlc证据可勾选，不以原T063完成或一次正常运行抵扣；原3D现场缺失分段日志须明示。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

- [ ] T066 接入008 E01—E06目标合同，修改backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs与backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs等实际适配，依赖008相应T006—044及B01—08；真实槽位/源目标、物理未知、预算和来源证据归008各M任务，不重复执行或勾选T064/T065。 追溯：FR19、宪章3.2.0 P03/P07/P08/P09/P11/P13（006另P12）。

