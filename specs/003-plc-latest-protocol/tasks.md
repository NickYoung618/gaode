2026-09-26 PARAM事实增量：按共享API合同保存实际CaptureFact/RequestedCaptureSettings并从现有媒体查询投影；008 T054、003 T068承接，原编号及完整条件不变。

2026-09-26夜间008 T057直接依赖：沿T071同盘取放接入plc-stage-action-port所述取料在途持久回调，并在既有PlcStageActionPortContractTests补保存顺序与保存失败不放料的最少验证。T072正常暂停/人工/故障对照引用001 T052及008 T069，不重复主验收包或勾选父任务。

2026-09-26 USR-20260926-D任务增量：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替换活动任务的旧U05单指令故障重发要求；旧证据仅历史适用，暂停/人工继续及合法局部重试保留。编号、勾选不变，新恢复尚未实现/验证。

2026-09-26旋转Test增量：T071按[虚拟旋转请求/结果合同](contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。

2026-09-26 人工子范围增量：T072按[人工执行合同](contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

> 当前008执行以文末“S0—S5任务增量”为准；此前原版本范围/固定样本/两配方/原型只读等冲突条款仅属历史。未受影响任务保留原状态。原文已逐字节归档：[历史任务](tasks-history-before-s0-s5-20260924.md)。

# 后端任务清单：第一工位完整主流程纵向切片

008 T061共享子范围：T067/T070按[E Test执行合同](contracts/e-test-execution.md)将已确认F扫码Z/3/4与本轮清零用于E角色，保留旧完成事实；E新增实际证据与保存门禁必须独立验证，不把合同定义计作代码完成。

**功能标识**：003-plc-latest-protocol  
**输入**：[spec.md](./spec.md)、[plan.md](./plan.md)、[data-model.md](./data-model.md)、[contracts.md](./contracts.md)、[quickstart.md](./quickstart.md)  
**当前适用宪章版本**：6.0.0；已勾选任务的历史证据保留产生时版本  
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

已勾选的T024/T026、T039/T045/T049、T052–T058保留其当时的实现与证据范围，不代表固定图片正式采集、逐Detection采集的独立worker实收发、Test自动模拟取盘来源或006实际页面联调已经通过。007的T001负责先同步上述共享合同，T006–T014负责新增后端接线与真实证据，T015才验证006实际页面到FinalUnloadCompletion；不把这些007工作重新加入003历史完成门禁。确认API共享合同现统一为`POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`，请求体仅`requestId/expectedRevision/reason`，Host从已提交事实和认证上下文取得其余信息；specs/007-station01-integrated-loop T013负责使Test模拟ManualActor不再误记AuthenticatedHuman。006页面请求/凭据/展示归006，Host侧限定Test来源的API/通知跨源与授权接线归007 T010；006未交付不阻塞独立后端接线。

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
- [X] T037 [US2] 按固定顺序编排 Detection→Sorting→UnloadPreparation 并提交各阶段意图/反馈到 `backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`、`backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs` 和 `backend/src/Gaode.Application/Workflow/StageEventing.cs`（来源：FR07/FR08/FR13、验收3/4/6/7；原则：P04–P08；依赖：T034、T035、T036；完成/证据：三个 Completed 均来自端口事实；Pending 继续，MappingFailed/UnknownHeld 停在正确阶段） （勾选保留历史版本完成事实；011新规则由后续实施与验证承接。）
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
| 通信 4 次、1/2/4 秒、按冻结RecipeExecutionBudget确定的不重置 | T004、T005、T027、T028、T056 | 可控时钟事件链和 E2E 引用证据 |
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

上述按runId媒体清单合同由003共有API文档定义，具体007固定图片身份接线、Host查询和验证归007新增任务；specs/003-plc-latest-protocol T001–T060原勾选、编号及历史验证范围不变。006仅消费公开合同并负责原型格位绑定。

## 2026-09-23 VirtualPlc 清零与监控 Flow-back（T061 已完成，T062 待页面验证）

既有T001–T060及原始证据保持历史范围。T061 已由正式 Modbus 3/3 定向测试验证（`artifacts/plc-latest/t061-20260923-223458/manifest.json`）；T062 的接口与页面逻辑已有定向证据，但实际监控页面操作尚未验证。上述证据均不构成007完整闭环通过。

- [X] T061 [US1/US4] 对应FR04/FR10、验收2/5：按最新版协议§3.1.7最小修正Host正式PLC适配器的下一运动门禁，允许上一轮`XY_Pos_Confirmed(4x0002)=1`在清旧`XY_Move_Cmd(4x0001)=0`后保持；有上一轮检测时仍须核验`Z_Reset_Status(4x0053)=2`后才清`Inspection_Status(4x0052)=0`，并保留坐标、连接代次及安全条件。核对同一适配器依赖`4x0002=0`的相邻门禁，只改实际受影响处；不得把旧1当作新一轮到位或盲重发。同步修正`backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs`中“清4x0001后4x0002=0”的旧断言，定向验证清命令期间保持1、4x0053=2后才清4x0052、下一合法运动实际受理时才开始新反馈、连续两次运动各自匹配本轮坐标/状态，以及F检测复位后的配方绑定能按正式端口完成。（依赖：本次FR04及边界合同；完成/证据：Host正式Modbus与VirtualPlc连续运动及F后配方绑定定向测试、点位写入序列可核对；不重跑003历史五场景，不宣称007 T015通过。）
- [ ] T062 [US4] 对应FR14：针对已先行修改的VirtualPlc本地监控补定向证据，并最小纠正`VirtualPlc/wwwroot/app.js`把实际数值0显示成“空”的既存矛盾；核对`/api/simulator/changes?after=`按写入序号返回中间变化、最近变化排除心跳、有限缓存游标落后时`gap=true`及页面提示，当前状态仍显示数值0。只测本地诊断，不新增PLC点位、业务控制或无限期日志。（依赖：本次FR14及边界合同；完成/证据：隔离查询与页面记录、源码/配置版本和测试结果分列；不能替代T061或007完整联调。） USR-E增量：在`VirtualPlc/PlcDataStore.cs`、`VirtualPlc/VirtualPlcEngine.cs`、`VirtualPlc/Program.cs`及Modbus受理消费者复用state/changes/audit/address-map，按virtual-plc-boundary升级audit/2.0：同值实际写亦记录完整rawWords、批量范围、事务/连接、字序、回执，动作锁存XYZ/axisRole、实际采样/阶段、actionSequence/generation/write引用及gap。不造Host身份或PLC点位，目标快照不作实际反馈。设备诊断可先独立交付，T071共享文件串行；Host事件由T070/T071/T069生产、008 T054持久、T068查询。必要同值Y/变化Y/批量及缺口验证存`specs/003-plc-latest-protocol/evidence/008-action-diagnostics.md`，007 T033导出新包；不永久全量轮询归档。

## 2026-09-24 FR15设备故障诊断增量

- [X] T063 [US1/US4] 对应FR15、FR03/FR07、宪章P04/P07/P09/P13：在正式Host/Modbus边界记录首次通信请求与失败、心跳最后有效翻转及过期、陈旧状态、协议与冻结配置版本、动作请求/匹配反馈、`requestId/commandId/runId/operationId/connectionEpoch`和实际UnknownHeld或拒绝处置；底层异常保留受控原始上下文，高频重复日志限频或聚合并持久可查。依赖001 FR-041的启动关联及既有003协议/状态合同；完成条件为首次正式通信失败、同代次明确不安全对照、心跳/状态过期的定向Test验证，保存原始Host/VirtualPlc日志、协议审计、API状态及关联查询，证明两类安全判断不混同、无旧值放行和盲重发。VirtualPlc证据只证明软件联调，真实PLC另行验证；T001–T062历史勾选及FR12五场景证据不自动算本任务通过。本次不改PLC点位或协议来源。2026-09-24后续按 `validation.md` 的扫描丢边沿定向复现/修复、17/17组合与有限重复、两类独立进程/页面故障证据，在Test/VirtualPlc范围完成；真实PLC及原始人工故障另判。
2026-09-24 T063执行注记：新增独立扫描定向测试与有界启动轨迹，先修VirtualPlc上升沿在Modbus写入和扫描之间丢失，再核验正常/诊断/安全回归；仍按T063原完成条件判定，不能由一次绿灯直接勾选。

## 2026-09-24 下料协议缺陷修复增量

- [X] T064 [US1/US4] 对应FR16–FR18、宪章P03/P04/P07–P09/P13：将冻结下料XYZ和Test/Production来源贯通配置、编排、阶段动作端口与正式Modbus；完成先行Z复位门禁、Camera_Target_X/Y/Z先写再发命令4、只读Machine_Current_Pos_X/Y/Z并按批准目标/容差校验。删除错误的前置XY=0及清命令后等待XY=0，但旧1、同目标、断联/epoch变化均不得假完成；VirtualPlc命令4只动XY，不伪造Z反馈。完成依据为修订合同测试、隔离正式Modbus正常与必要失败回归、日志及SQLite/协议证据；原始人工页面和真机须独立验收，不复用T063勾选。2026-09-24本轮Test/VirtualPlc证据：`artifacts/station01-007/unload-fix-20260924-b/unload-fix-contracts.trx`（31/31）、同目录Host/PLC审计/GET/SQLite及退出后`diagnostic-index.json`；真机与原始页面未验收。

- [X] T065 [US1] 对应FR03/FR15心跳应答延迟缺陷：保留正式3秒互锁及独立连接，记录异常窗口中Host轮询调度、Modbus事务读写、VirtualPlc接收/处理/响应和有效应答的有界UTC/单调证据；以修复前后定向对照验证实际延迟机制的最小调度修正，真实超时仍锁动作且无盲重发。仅本缺陷Test/VirtualPlc证据可勾选，不以原T063完成或一次正常运行抵扣；原3D现场缺失分段日志须明示。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

> T066 已由下方当前增量替换；原编号、未完成状态及全文见 tasks-history-before-s0-s5-20260924.md，不作为当前实现任务。


## S0—S5任务增量（2026-09-24，宪章5.0.0）

所属功能：`specs/003-plc-latest-protocol`。跨功能依赖写作目录简称+任务ID，完整目录见008 tasks映射表。原则P03/P04/P05/P07/P08/P09/P11/P13，前端另P12及用户最小原型授权；新增任务全部未完成。旧T066被以下任务替换，未受影响的历史待办不取消；公共验证只做必要正常/失败，不构成交叉穷举。

- [X] T067 [US1] S0接新F握手，唯一实现所有者：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`和`VirtualPlc/VirtualPlcEngine.cs`按指定PLC协议§3.1.7及008 E04实现命令5/公共XY/ScanZ、0→3清命令→4→本轮扫码Z复位→0；到下一有效运动前保持扫码轴反馈归属。依赖：已定义协议；不等E或产品B04。交付：双方实际状态机与必要关联日志；`backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs`验证正常、解码失败不放行、复位失败/旧2不清0，保存失败不写4；证据`specs/003-plc-latest-protocol/evidence/008-f-handshake.md`。Q01整链另依T065，不用旧T061证明新F。

- [ ] T068 [US1] S0提供前端共享API及保存投影：`backend/src/Gaode.Host/Api/RecipeEndpoints.cs`、`RunEndpoints.cs`和对应应用查询/上下文codec按008 API合同s01-recipe-api/3.0（结果展示schema为station01-result-display/1.0）实现catalog.items、contextJson/2.0、expectedRecipeRef受理保存、recipeSelection/recipeExecution/allowedActions、results/movements及提交引用和媒体身份，通知revision驱动GET。依赖：specs/002-plc-xyz-recipes T11；结果事实未产生时空/受限，不填默认完成；后续层级结果复用同投影。必要合同验证：401/403明确拒绝、202非Final、旧上下文不启动新路线、未提交事实不公开，路径`backend/tests/Gaode.Contracts.Tests`；证据`specs/003-plc-latest-protocol/evidence/008-api.md`。对应FR19、008 FR-001/016/018、Q/C全体。 USR-E增量：复用002 T11准入和008 T054已提交动作事实，目录/页面/POST直接启动统一拒绝退出版本，历史配方/run仍可查询。在既有`RunEndpoints.cs`、`RecipeEndpoints.cs`及查询消费者投影完整目标/实际XYZ、轴、采样阶段、校验/清零及来源；未知不填成功。按冻结合同先交API子能力供001 T090，真实事实再接T054，不等待T054整项形成循环；沿008-api必要准入/查询合同验证，不建第二日志平台。
  - RES公开结果子交付（FR19、008 FR-016、HMI-003/DAT-004）：实际修改`backend/src/Gaode.Host/Api/QueryEndpoints.cs`、`backend/src/Gaode.Domain/Station01/RunSnapshot.cs`及必要现有持久读取；沿[唯一结果字段](../008-recipe-driven-inspection/contracts/api-results.md#res结果展示增量2026-09-26目标尚未实现)与[查询合同](contracts/station01-main-flow-api.md#res已提交结果查询增量2026-09-26目标)扩原GET /runs/{runId}，不新增接口/结果库。先交查询结构及读取基础供既有目录/启动消费者，再接008 T054已提交事实形成结果投影；后者是事实子交付依赖，不等待T054全任务或006 T049/008整链验收。
  - RES投影内容：从同一持久读取边界关联冻结计划、已提交WriteBatch/StageEvents、AlgorithmCalls及媒体；按run/plan/对象/面/项目/step/call/media核对，普通Single也公开面及单图/融合关系，Group/Member和Assembly/Part层级不得互替。投影resultSchemaVersion、resultRevision、resultContext及results/inspections：disposition为OK/NG/Pending或null，quality为Derived等事实质量，source为Simulated等来源；state、completeness、dispositionState、saveState及持久Final独立。只按已提交业务决定给质量，完整性对比必检与已提交引用，无依据Unknown，不用默认Complete。
  - RES明细/时机：项目/能力、参数及其kind/单位/引用、实测值、原因、已有缺陷/置信度/媒体遵守合同字段及detailAvailability；尚未产生NotProduced、提交未确认NotCommitted、历史缺字段/关联Unavailable、算法Pending及技术异常分开，不从null推质量。图像级结论不能伪装命名缺陷项目，缺字段不能填0或演示数据。解除只等Detection Completed/Final才投影已提交单图或面的限制；对象/组汇总未提交则保持无结论，查询不重新融合或重算业务质量。
  - RES版本/历史：ETag覆盖resultRevision和本次结果/原因/媒体/保存/处置投影变化，既有run revision含义不变，resultRevision不作数值排序；提交新结果不能误返回旧304。coordinator内存无run但持久run存在时只读重建查询，历史缺字段如实Unavailable，不恢复执行、不改旧记录、不授予旧run继续资格。保持Read权限、脱敏响应及既有错误合同。
  - RES必要验收/证据：在`backend/tests/Gaode.Contracts.Tests/`及`backend/tests/Gaode.Integration.Tests/Api/StartQueryTests.cs`/同目录相关测试验证已提交子结果提前可查、未提交质量不公开、Single/分层身份准确、缺失与Pending区分、结果变化ETag更新且相同版本304正确、内存缺失的持久历史只读访问无运动/写回。追加`specs/003-plc-latest-protocol/evidence/008-api.md`的结果子包，关联008 T054持久引用、同run API响应及结果版本，供006 T049和既有路线复用；不以源码字段存在判展示通过。


- [ ] T069 [US4] S1提前接最终结束链：`backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`、`ThreeStageWorkflowExecutor.cs`及`backend/src/Gaode.Host/Api/RunEndpoints.cs`按新版先检测→适用分拣→下料到位→整盘收敛顺序，替代T064旧命令4规则，分别保存WholeTray、ObservedUnlocked、页面取盘确认及Final；普通OK无搬运不依赖T071。依赖：specs/008-recipe-driven-inspection T054保存子能力及本功能T068共享查询；不依赖008 T068故障恢复任务，后者复用本任务已交付盘末能力；specs/006-frontend-station01-console T049仅为页面验收依赖，不阻塞后端实现。必要验证：缺结果/保存失败/未解锁拒绝确认不Final，原子最终提交可查；证据`specs/003-plc-latest-protocol/evidence/008-completion.md`。对应FR19及008 FR-011/014、C06/F4/F6，完整链归008 T055。 按`specs/008-recipe-driven-inspection/sequences.md`§2核对000B下料、本次XYZ、先下料后适用分拣及保存/解锁/页面确认门禁。 USR-E增量：在`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`及`VirtualPlc/VirtualPlcEngine.cs`核对本次下料完整XYZ/抓取Z000B、实际反馈/比较/命令0；先核实际运行包再修具体漏写读。依T062诊断字段及008 T054保存子能力，先下料后适用分拣；普通OK不等T071分拣或生产采样。沿008-completion和008 T055/T059新包验同值/变化Y及缺保存/未解锁不Final，绝不反向等008 T068。

- [ ] T070 [US1] S1接产品实际定位/适用Z与检测1/2闭环：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`LatestProtocolStageActionAdapter.cs`、`VirtualPlc/VirtualPlcEngine.cs`扩正式产品角色及真实坐标反馈关联，经唯一Motion/Safety准入。依赖：specs/008-recipe-driven-inspection T049确认所用B04、specs/002-plc-xyz-recipes T11及T067共享状态改动结束；显式Test XYZ组件接线可先做，正式Q01派发仍依B04。交付：产品命令/轴映射有来源；匹配本轮XYZ才允许采集，结果提交后2、复位可靠2后0；必要到位/复位失败测试存`specs/003-plc-latest-protocol/evidence/008-product-motion.md`。对应008 FR-002/F2/Q01；没有轴语义时本任务Blocked，不能补造VirtualPlc规则。 USR-E增量：同时承接公共3D/F、适用E、单面及第二拍照位XYZ观测，复用已完成T067的扫码3/4而不改历史完成事实。实际消费者LatestProtocolPlcDevice.AdvanceMove/复位、VirtualPlc.ProcessMove/Inspection；依T062诊断字段子交付，不等其监控页面整项。先核实际Host/PLC/Provider/字序/配置/产物摘要，再按证据修复；分完整请求/批量范围/接收/实际读回/Host逐轴判断/复位清零，同值Y也可证明读写。检测Z0007与扫码Z0009、1/2与3/4不得混用；必要保存先于后继。沿008-product-motion及007 T033新包验同值Y/变化Y、缺轴/错来源/实际不匹配不采集、复位/保存失败不续接；源码存在不能关闭问题1—3。

- [ ] T071 [US1] S2—S4共享真实搬运适配唯一归属：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`VirtualPlc/VirtualPlcEngine.cs`按已确认B02/B08源目标/物理索引/取放反馈接分拣、自动翻面及进出旋转站，旋转Test复用`specs/003-plc-latest-protocol/contracts/rotation-test-execution.md`及已实现请求/结果，角度归下位机，B03只限制未定义的生产映射；复位隔离新增归T072-A，不重建旋转能力；参数按B04。依赖：specs/008-recipe-driven-inspection T049对应输入、T070；逐能力解锁，不等待所有分支输入才做已确认部分。交付：各支持动作本轮意图/源点/取/目标点/放/完成证据，未知保留在途不重发；`specs/003-plc-latest-protocol/evidence/008-transfer.md`按能力列Supported/Blocked。对应008 FR-005/009/010、C02/05/06、F2/F5；业务分配/占用提交归008 T057/T065。 按`specs/008-recipe-driven-inspection/sequences.md`§4/5验证各实体Flip_OK和Sorting_OK清零；状态2仅取料，槽号取料后提交，不为抓取动作新增Inspection/ZReset握手。 USR-E增量：落实两端分拣静态缺口：VirtualPlc.ProcessSort/CompleteDueAction真实模拟目标到位→取放完成并更新目标阶段实际XYZ；Host WaitSortingAsync/WaitSortingStatusAsync经Modbus取得本动作可靠目标阶段观察并校验，再依本次取料2清命令、提交真实源槽及放料XYZ/命令2，放料3及Sorting_OK清零后才持久完成。状态2/3可能已抬升，禁止要求实时Z等于取放目标Z；未可靠采到保持受限，诊断不能代反馈，不造生产寄存器/安全高度/保持位。依T070/T062所用子能力及008 T054保存通道，设备能力不反等008 T057业务/页面。沿008-transfer及008 T059/T062同包验实际采样、未取料成功无放料写包、抬升位置区分、采样缺失不假完成、状态3+ACK及连续实体Flip。生产采样窗口只阻对应生产分支，不阻合法Test/普通OK。

- [ ] T072 [US4] 在既有设备/控制入口按`specs/003-plc-latest-protocol/contracts/recovery-test-execution.md`完成USR-D双端复位/真实初始状态及恢复API；人工换面子范围保持独立。对应008 FR-005/012/014/016、C02/C07/F5及P04/P06/P07/P08/P09；以下A/B/M是本任务子交付标签，不是新任务编号，全部完成才可勾T072。 USR-E增量：A复用本批已核验的T070/T071适用动作/观察及T062诊断关联，不等全部Q/C/F或特殊生产分支；A/B/M原子交付链保持，A不反等008 T068/B，诊断不替真实初始，新恢复页面唯一008 T069。
  - A——前置设备能力：修改`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`backend/src/Gaode.Application/Station01/StartupReadiness.cs`、`backend/src/Gaode.Application/Ports/DeviceMessages.cs`、`VirtualPlc/VirtualPlcEngine.cs`及`VirtualPlc/TestSpecialActions.cs`。复用已有System_Reset、映射、1秒I/O/3秒心跳及T070/T071已交付的动作能力，补本次epoch/generation下实际Ready、模式/安全/占用、XYZ合法初始范围、锁0、命令/ACK清零等逐项观察；Ready或本地清Unknown不能单独算初始完成。特殊Test复用既有Enter/Rotate/Exit端口，真实收束旧specialTask/occupant、隔离旧异步回写并保留历史结果，按rotation-test-execution.md补Test状态读取；不新增生产信号或索要旋转角度。A只交付设备观察/物理复位子能力，Host旧执行收束与保存/资源门禁由008 T068组合为完整初始准入；A不依赖008 T068或B；必要设备合同测试先证明初始不足拒绝、旧generation不污染、实际清零/释放后才ReadyForNewRun，证据`specs/003-plc-latest-protocol/evidence/008-restart-device.md`（后续产物）。
  - B——后续API与查询接入：在`backend/src/Gaode.Host/Api/ControlEndpoints.cs`、`RunEndpoints.cs`及既有查询投影消费A、001 T052/T078与008 T068已交付业务，不反向要求这些前置完成整个T072。复用现有recovery-reset/recovery-checks及POST /runs，按合同传递restartFrom、new request/command/run、reset/check单次消费和双向关联；故障continue拒绝FaultRequiresNewRun，普通start/全局reset不得旁路门禁。Recovery.Check、Run.Start、Run.Continue分别核验，202不等于物理初始或Final；故障/媒体、逐项Blocked原因、committedRevision与allowedActions持久可查询，原因/evidenceRefs不丢。依本功能T068已有共享查询子能力，不重复catalog/API建设。必要权限、陈旧检查、重复新启动、保存未决无动作合同测试归`backend/tests/Gaode.Contracts.Tests/`及`backend/tests/Gaode.Integration.Tests/Station01/`，子证据`specs/003-plc-latest-protocol/evidence/008-restart-api.md`；C07/F5正式页面唯一引用008 T069包，不让B反向依赖T069验收通过。
  - M——原人工换面：复用`ControlEndpoints.cs`的manual-flip-confirmations及`specs/003-plc-latest-protocol/contracts/manual-test-execution.md`，依008 T060对应人工子能力；认证确认→占用0→确认清零及必要安全/保存成立后，同run采用命令目标面并记录默认/人工来源，不能当自动Flip实测，也不能触发故障新轮。原C02证据归`specs/003-plc-latest-protocol/evidence/008-manual.md`，006 T050消费；M与A/B互不构成整项等待。
  - 日志与复用：命令受理、实际复位、逐项检查、阻断/超时/失败按requestId、旧run、resetId/checkId、epoch及已建立新run持久关联，限频不吞关键变化；旧动作合法局部重试和普通幂等保留。A/B/M各自交付即可供依赖者使用，不因子范围通过提前勾T072。

当前首批沿用T065：实际涉及`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`及其通信/心跳适配、`VirtualPlc/VirtualPlcEngine.cs`；依`.specify/bugs/station01-heartbeat-response-delay/assessment.md`、fix.md、test.md及007当前Blocked包定位必要修改。交付仍按T065原条件，保存当前构建分段/安全对照，旧任务不因新文档勾选。

> 旧协议历史检查点（不代表新版状态）：008第八批T071自动翻面子范围：已定义的命令3、目标面、状态2/实际面号2、人工占用禁动和PLC内部整体闭环为来源边界；取放字段/顺序及旧反馈本轮关联未配置，动作适配仍不派发。T071整项和勾选不变。


> 旧协议历史检查点（不代表新版状态）：008自动多面T068/T071直接子范围：T068查询投影复用已提交媒体事实附加面/轮身份，缺事实留空；T071读已有Flip状态与实际面号并由Host关联本轮，取放两组参数提交未定时不派发。原整项条件/勾选不变。

> 旧协议历史进度（已被新版字段/ACK定义替代；当前代码/数值仍待适配）：当前T071自动翻面进度：`FlipFeedbackCorrelation`已实现Host内部旧完成排除和本轮新反馈判别，设备观察已有定义状态/实际面号/人工区字段；规则测试3/3通过。正常触发按协议§3.1.5定义，不再作为外部缺口。取料、放料两组坐标的PLC字段/提交顺序仍未定义，故003尚未派发命令3或目标面，T071整项未勾；见008 [第八批证据](../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。


## 2026-09-26新版协议增量子范围（未实施）

既有编号和勾选只证明原范围，本表所有新版子范围均NotRun；实现前置按所需子能力交付，整项验收仍保留原未齐项。输入为唯一分区协议及008 execution/3.0；日志须可按run/step/operation/实体/面/连接代次追踪意图、派发、反馈、ACK清零、保存及失败。

| 原任务/新版子范围 | 具体消费者（文件简称按原任务路径） | 输入、前置、完成条件及最少验证 |
| --- | --- | --- |
| specs/003-plc-latest-protocol T067 / 20260925协议 | `FScanStep.cs`、`LatestProtocolPlcDevice.cs` | 已完成旧F任务的新版可追踪核验子范围NotRun：复用3/4、命令5及扫码Z，核对新版身份与本轮清零；F失败/绑定或保存失败不因机械复位放行，不重建F。 |
| specs/003-plc-latest-protocol T068 / 20260925协议 | `RunEndpoints.cs`、`RunMediaCatalog.cs`、状态通知/GET投影 | 以新保存合同为前置；公开面/阶段/实际测量来源/目标版本和Detection→Unload→Sorting进度，allowedActions只有已提交解锁才允许取盘；验证媒体跨面不混及到位不显示可取盘。 |
| specs/003-plc-latest-protocol T069 / 20260925协议 | `LatestProtocolStageActionAdapter.cs`、`ThreeStageWorkflowExecutor.cs`、`WholeTrayWorkflowOrchestrator.cs`、`WholeTrayCompletionStore.cs`、`VirtualPlcEngine.cs` | 取代旧T064已完成的0007/XY-only子范围：000B与命令4本次XYZ、新普通盘末顺序、恢复排序/阶段期限和来源矩阵同批适配；前置有效目标/安全/必要保存，不依全部NG分拣；验证正常OK到Final、未解锁/缺提交不得取盘。 |
| specs/003-plc-latest-protocol T070 / 20260925协议 | `LatestProtocolPlcDevice.cs`、`VirtualPlcEngine.cs`、产品Motion准入 | 已有检测1/2复用；区分检测Z/扫码Z/抓取Z与本轮反馈归属，协议版本同源；验证F/检测复位及新Flip/Unload切轴，不增加新设备抽象。 |
| specs/003-plc-latest-protocol T071 / 20260925协议 | `ProtocolLatestMap.cs`、`PlcAddressMap.cs`、`DeviceMessages.cs`、`LatestProtocolPlcDevice.cs`、`LatestProtocolStageActionAdapter.cs`、`VirtualPlcEngine.cs`、`VirtualPlc/wwwroot/app.js` | §2.3/2.4/3.1.5/3.1.6为输入；同批ACK地址/状态机/快照/监控枚举/协议身份及SHA。自动Flip先交付供普通OK Q03；分拣后续按真实源目标接线。验证不同实体同目标面完整闭环、状态2仅取料、状态3+ACK才完成、4/5失败满盘、无序号代槽位。旋转Test已确认并复用；B03仅限制生产未知映射，不等待角度输入。 |
| specs/003-plc-latest-protocol T072 / 20260925协议 | `ControlEndpoints.cs`及人工接口/状态消费者 | 人工占用1禁动→完成且安全后确认1→占用0→清确认已定义；人工采用命令默认面；故障初始判据与新启动分别按T072-A/B。B07仅局部生产未知状态，不重复索要已确认业务。仅依适用业务子交付，不反向等待整个006；人工点击不制造设备事实。 |

## USR-20260926-D任务执行边界

本次仅增量任务对齐，来源为008 [计划交接](../008-recipe-driven-inspection/plan-restart-alignment-20260926.md)及[任务对齐记录](../008-recipe-driven-inspection/tasks-restart-alignment-20260926.md)。仅本轮修改的未完成任务承接新规则；已有已勾任务和历史证据保持原适用时期，不可抵扣新恢复。普通幂等、未触发故障的合法有限重试、正常暂停和人工换面继续不得误删。

执行按子交付：003 T072-A设备观察/复位隔离与001 T078持久基础可分别准备（共享文件修改须协调）；001 T052→008 T068业务→003 T072-B API→006 T051既有页面→008 T069唯一C07/F5页面包→008 T070汇总。T072-M人工与上述A/B独立；不等待无关父任务全勾。001 T070只做普通控制路由，不另建故障API。设备/worker/页面实跑串行；非阻塞边界登记，不增加全配方×全故障矩阵。详细子交付输入、证据和局部限制见任务对齐记录。


## USR-E当前依赖与完成口径（2026-09-26）

依据宪章7.0.0，完整归属/验收见[本轮任务交接](../008-recipe-driven-inspection/tasks-six-issues-alignment-20260926.md)。两端协议/诊断子能力＋当前配方准入/目录→必要代表性协议及正式路线验证→USR-D完整新轮。003 T072-A＋001 T078→001 T052→008 T068（复用008 T054及003 T069）→003 T072-B→006 T051→008 T069→008 T070；A/B/M分子交付，003 T069不反向等008 T068，不等全部Q/C/F或特殊生产。共享源码按文件串行交接，设备/worker/页面串行采证；子交付不勾父任务。问题1—5根因待实际包核验，状态2/3实时Z不强制等于取放目标Z；生产采样窗口只局部限制。历史勾选/正文不改，旧Q/旧恢复Passed不抵新验收。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

原T068查询子能力消费008载荷，T069完成链不重建；不修改其他勾选。

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

## 2026-09-27 T062监控分类必要补验（I3，无新增任务）

沿FR14及virtual-plc-boundary当前监控错误分类：HTTP读取、communicationTimedOut心跳事实、页面渲染/记录错误分别显示；不存在TCP连接字段时不得推定Connected。保留最后有效快照时间及旧状态标识，恢复分别核验，不把渲染异常作为断联。仅复用scripts/tests/virtual-plc-monitor.test.cjs验证读取成功但渲染失败、HTTP失败、实际心跳超时、恢复及既有XYZ/方向/版本。新包解压资源复验，不重跑配方，T062编号和勾选不变。

## monitor-xyz-history 本轮同步
按[003当前监控纠正](../003-plc-latest-protocol/spec.md)及其[诊断合同](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md)：公开XY名称、原列表同值XYZ、删除独立栏目；复用audit/changes，保持业务/地址/期限。此前XYZ命名条款仅限旧构建。 本轮工作归003既有T062的监控子范围；验证通过仅记录本轮证据，不自动关闭该任务其他范围或T065/T055/T070。


### 2026-09-29 T062监控增量（保留原编号和勾选）
按FR14及monitor-test-workspace.md补齐同值解锁实际写入、动作关联、首屏大历史与只读筛选/暂停查看。仅本次监控范围记录在monitor-xyz-history/20260929T092100Z，不以此勾选包含其他验收义务的整个T062。复用上传原始审计，验证源码/解压包、真实浏览器及版本一致；007打包与008证据入口接续，不变业务接口。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

既有ID/顺序/勾选保持；以下共享增量唯一实施归属009 T017—T027/T033—T045/T047—T049，原任务完成不能抵扣本次新增义务。

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

依赖：先本节spec/contracts/plan实际对齐，再009 T012复核，代码依009各任务的合同/基础前置；按迁移矩阵承接有效断言。完成证据：V-SEM/V-WIRE、V-PICK F05/F06、V-BIND BA01—07及实际消费者验证，按任务适用项分别取证；新增能力未实施、运行未执行。本功能历史任务不自动勾选。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次增量唯一代码/验证归属009 T043—T047/T050（006绑定T046）；相关任务直接或传递依赖本次实际对齐和009 T012。保留本功能全部原任务ID和勾选，不以父任务历史完成抵扣。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready必须核本次有效RecipeApplicationReceipt，不能从已有handoff恢复。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

006尚未交付可推进后端子集验证，但不签009完整基线。

完成证据按009相应任务、固定案例和消费者交付；未运行部分不得报告通过。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次增量唯一代码/验证归属009 T019/T020/T042—T044/T047/T060；相关任务直接或传递依赖本次实际对齐和009 T012。保留本功能全部原任务ID和勾选，不以父任务历史完成抵扣。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。

完成证据按009相应任务、固定案例和消费者交付；未运行部分不得报告通过。


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

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

## 013实施前定向同步（2026-10-04）

SY-01：FR03/FR15当前采集按013 A01/A03/A07：独立心跳300ms；基础活动200/空闲500ms、动作反馈200ms、位置运动或未知500/确认静止1000ms。首Moving/Executing仅有依据的局部50ms、齐备后200ms。必要读写/清零即时，不等慢周期；相对plannedDue计调度迟延。原I/O、3秒、动作/保存截止及T065真实超期锁动作不变。旧T065“50ms不变”为当时构建事实，不再约束013现行策略；新周期不代替真实反馈或当前安全证据。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A03/A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。
- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。

- [ ] T016-I01 定向同步与消费本功能直接相关公共配置/观察/处置/下料边界，产物以新016 tasks T002及对应共同代码任务追踪；原历史编号和勾选不改。
