# 功能任务清单：配方执行与测试环境解耦、共同业务执行层建立及架构防回归

**功能目录**：E:/dzk/gaode-1/specs/010-recipe-execution-isolation  
**日期**：2026-10-02  
**输入**：[spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[quickstart.md](quickstart.md)、[共同执行](contracts/common-execution.md)、[输入边界](contracts/input-boundaries.md)、[验证合同](contracts/verification.md)、[architecture 当前追加复核](checklists/architecture.md)  
**宪章**：7.0.0，P01—P13；保持现行 18 FR、12 AC、8 SC。  
**状态**：原48项已实施，2026-10-02当前完成证据见文末T048（该版本T180通过）；原任务生成/设计审查说明保留为历史记录。2026-10-03仅追加B02门禁修补T049，本Bug专项验证已收口（test.md为verified）。清单勾选仍只表示文档质量，不代表本Bug实现或运行通过。

## 拆解规则与初始化

setup-tasks.ps1 -Json 已返回 FEATURE_DIR=E:\dzk\gaode-1\specs\010-recipe-execution-isolation、TASKS_TEMPLATE=E:\dzk\gaode-1\.specify\templates\tasks-template.md；使用返回模板内容组织本清单。已完整读取上述九份文档、AGENTS.md 和宪章。hooks 为空；未运行 Git、构建、测试、设备或数据库。本次只创建 tasks.md；所有其他文件保持只读。后续清单中的修改/执行均为待办，不是本轮授权实施。

- 下列文件路径以项目根 E:/dzk/gaode-1 为基准；同一文件组中逗号后的文件继承此前目录。首次标“新增”的源码/测试/配置及后续对该路径的引用均为计划产物，证据目录在后续执行时生成；其余为现文件。不新增工程、验证平台或兼容层。
- 每项先满足表中全部直接前置，再进行改动；传递前置同样有效。任务行给交付目标和路径，紧随表给完成判据、证据及追溯。每个义务只有一个主要修改任务，验收任务引用其结果。
- 任务中的测试编制/迁移和实际验收分开。可在后续实施中用受影响局部测试辅助开发，但只有当前统一入口凭证和固定必需集可支撑验收；本轮不运行任何测试。明确要求的测试不能按技能“可选”默认省略。
- 共享接口代码统一被 T002—T007 实际定向对齐阻塞；设计中的 A 表只是登记。其他功能文档仅做必要修改，不勾选其旧任务，不改来源文档、原型或历史证据。
- 无 [P] 的任务按清单顺序串行处理共享写入；依赖图明确允许规则开发提前，但不自动授权共享文件并发。仅两组指定 [P] 在全部前置完成且文件不冲突时可并行；标记不授权自动多代理。
- 所有运行预期来自 VG-06 B1—B11、有效业务合同及审阅输入；被测 validator/planner/executor 的输出只作 actual。所有证据层级须如实标记。
- 新增能力和生产验收不在本次范围；保反馈、真实保存、原期限/取消、未知占用、来源和结构化日志。缺生产预算/坐标/标定/能力/协议批准只限制对应入口，不回退 Test 值。

## 当前阶段范围与完成证据（P13）

| 项目 | 本专项范围 | 主要交付/证据 |
| --- | --- | --- |
| 起点与终点 | 选合法版本/模拟环境，经现正式启动→公共准备/F绑定→提交移交→共同检测/适用处置→整盘保存/解锁→授权取盘 FinalUnloadCompleted | T011—T022；T044 基线及 T046 替换的同 Run 事件/回执、媒体、实际进程/动作、查询和日志 |
| 必须参与组件 | 正式 Host/唯一共同业务、相机/算法/设备语义端口、独立 VirtualPlc/Worker、实际 SQLite/媒体 | T022/T026/T030；进程内 PLC 或整段服务替身不计 FullRun |
| 最小验证 | V00—V07；L 的 B/N/P/G/C；受影响分支组件；正常完整基线和等价替换两条 | T035—T048；S 仅一次共同组件，同时承接 V04/V06 |
| 完成证据 | 当前构建/源码/输入/清单、真实发现执行、保存/来源、冻结差异、迁移/删除、独立预期 | artifacts/recipe-execution-010/{verificationRunId}/；T048 核 T，不能以 L 或单配方成功代替 |
| 明确延期 | 全量测试/配方/页面/设备/故障矩阵、整机验收、轮询性能长稳、生产接入/SDK/新工艺/标定、页面/编辑器/插件平台、历史升级及无关治理 | 沿用 spec/plan 的排除项；T048 记录局部限制，不自动变成前置 |

## A01—A09 实际对齐与消费者承接

文档集简称只用于压缩表格，展开后均为准确项目路径。每个 A 项必须逐份核对所属 spec、相关 contracts、plan、tasks：有冲突则定向修改，无需文本变动则在该项对齐记录给出一致条款及消费者证据；不能用 010 的登记表代替实际核查/必要修改。T002—T007 串行，因共享同一批 spec/plan/tasks。全部完成才允许 T008 共享形状及后续代码修改。

| 文档集 | 所属 spec / plan / tasks（均须核对、必要处定向修改） |
| --- | --- |
| F001 | specs/001-station01-public-preparation/spec.md；specs/001-station01-public-preparation/plan.md；specs/001-station01-public-preparation/tasks.md |
| F002 | specs/002-plc-xyz-recipes/spec.md；specs/002-plc-xyz-recipes/plan.md；specs/002-plc-xyz-recipes/tasks.md |
| F003 | specs/003-plc-latest-protocol/spec.md；specs/003-plc-latest-protocol/plan.md；specs/003-plc-latest-protocol/tasks.md |
| F006 | specs/006-frontend-station01-console/spec.md；specs/006-frontend-station01-console/plan.md；specs/006-frontend-station01-console/tasks.md |
| F007 | specs/007-station01-integrated-loop/spec.md；specs/007-station01-integrated-loop/plan.md；specs/007-station01-integrated-loop/tasks.md |
| F008 | specs/008-recipe-driven-inspection/spec.md；specs/008-recipe-driven-inspection/plan.md；specs/008-recipe-driven-inspection/tasks.md |
| F009 | specs/009-plc-protocol-isolation/spec.md；specs/009-plc-protocol-isolation/plan.md；specs/009-plc-protocol-isolation/tasks.md |
| F010 | specs/010-recipe-execution-isolation/spec.md；specs/010-recipe-execution-isolation/plan.md；specs/010-recipe-execution-isolation/tasks.md；保持 18/12/8 及既有设计业务含义 |

下表的“Fxxx: contracts/...”相对该文档集根目录，绝非另一份新合同。实现消费者不只限表中首个类型；T001 当前调用/历史读取审计是逐项对齐附件，T034/T043 复核。

| A / 对齐主任务 | 必须定向核对的文档集及合同路径 | 生产者→直接/间接消费者；代码承接 |
| --- | --- | --- |
| A01 / T002 | F002: contracts/recipe-execution.md；F008: contracts/execution.md、contracts/test-virtual-mapping.md、contracts/test-multi-object.md | catalog/validator/planner→Start/绑定/预算/移交/检测/分拣→API/fixture/投影；T008/T009/T011/T012/T016—T020 |
| A02 / T003 | F001: contracts/acquisition-algorithm.md、contracts/configuration-time.md、contracts/persistence-handoff.md；F002: contracts/recipe-execution.md；F003: contracts/station01-main-flow-api.md；F008: contracts/execution.md | 解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024 |
| A03 / T004 | F003: contracts/detection-port.md、contracts/public-preparation-handoff.md；F007: contracts/virtual-integration.md；F008: contracts/execution.md | Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028 |
| A04 / T004 | F003: contracts/manual-test-execution.md、contracts/rotation-test-execution.md、contracts/e-test-execution.md；F008: contracts/execution.md；F009: contracts/business-device.md | typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030 |
| A05 / T003 | F001: contracts/acquisition-algorithm.md、contracts/persistence-handoff.md；F007: contracts/virtual-integration.md；F008: contracts/evidence.md、contracts/api-results.md；F006: contracts/api.md | capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029 |
| A06 / T005 | F001: contracts/persistence-handoff.md；F003: contracts/public-preparation-handoff.md、contracts/component-source-matrix.md；F008: contracts/evidence.md；F009: contracts/business-device.md（回执）；F006: contracts/api.md（间接历史消费者） | RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029 |
| A07 / T002 | F001: contracts/configuration-time.md；F002: contracts/recipe-execution.md；F003: contracts/station01-main-flow-api.md；F008: contracts/execution.md E06；F009: contracts/business-device.md B03.2；F006: contracts/api.md（现 v1 启动消费） | Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027 |
| A08 / T006 | F003: contracts/detection-port.md；F007: contracts/virtual-integration.md、contracts/commissioning-cli.md；F008: contracts/execution.md | 组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032 |
| A09 / T007 | F009: contracts/verification-gates.md、contracts/business-device.md（保持原保护及范围）；F010: contracts/verification.md（对应已定 L/迁移规则，核一致而非重做设计） | Rules/manifest/migration→verify及workflow/旁接aggregator→最终凭证/结论；T033—T042；009活动JSON映射属于T033，历史快照/报告只读 |

## 任务分阶段明细

以下每项均为待实施；“完成判据”包含代码/文档可审阅结果及指定实际验收的证据要求，不能仅凭条款存在勾选验证任务。代码提交单元可先完成，最终通过仍由 T044—T048 当前证据决定。

### 阶段1：共享准备与实际合同对齐

目标是把现设计登记转成实施前可核查的接口依据；只做必要定向对齐，不初始化仓库或重建工程。

- [X] T001 登记当前消费者、删除候选及逐方法/数据行义务；文件：backend/tests/Gaode.Rules.Tests/Architecture/010-test-obligations.json（新增）；artifacts/recipe-execution-010/preparation/dependency-audit.json（执行时生成）。
- [X] T002 定向对齐 A01、A07 的配方模型及原期限生命周期；文件：specs/002-plc-xyz-recipes/contracts/recipe-execution.md；下表 A01/A07 对应 spec.md、contracts、plan.md、tasks.md。
- [X] T003 定向对齐 A02、A05 的能力、F 实际来源及采集事实；文件：specs/001-station01-public-preparation/contracts/acquisition-algorithm.md；下表 A02/A05 文档集。
- [X] T004 定向对齐 A03、A04 的共同请求及辅助动作依据；文件：specs/003-plc-latest-protocol/contracts/detection-port.md；下表 A03/A04 文档集。
- [X] T005 定向对齐 A06 的冻结输入保存、F 移交及历史读取；文件：specs/003-plc-latest-protocol/contracts/public-preparation-handoff.md；下表 A06 文档集。
- [X] T006 定向对齐 A08 的唯一正式执行装配和旧模拟模式消费者；文件：specs/007-station01-integrated-loop/contracts/virtual-integration.md；下表 A08 文档集。
- [X] T007 定向对齐 A09 的持续 L、009 旁接入口和活动义务映射；文件：specs/009-plc-protocol-isolation/contracts/verification-gates.md；下表 A09 文档集。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T001 | 无 | 按 VG-06、D01—D11 记录调用、DI、配置、脚本、反射/动态使用、活动清单及历史 reader；分类、B1—B11 依据、原方法/数据行、目标层级和当前失败/未验证状态齐全。此处为修复前盘点，不是 B 或冻结。 | FR-011/012/016/018；AC-07/08/09；SC-005；P01/07/08/13 |
| T002 | T001 | 按 A 表逐消费者对齐 typed 配方/计划、批准边界、context/1.0 与 2.0、三入口原起点/同值预算及真实回执；原 v1 消费合法输入不继承非严格特权，文档修改可审阅且不勾旧任务。 | A01/A07；FR-001/003/004/009；SC-006/008；P01/03/04/08 |
| T003 | T002 | 明确生产者 Origin/CallId、原码解析、能力需求绑定、采集 Request/epoch/实际设置与来源，落实 006/008 查询消费者承接；不把 Requested 或配置版本当设备事实。 | A02/A05；FR-005/007/009；SC-008；P01/07/08/12 |
| T004 | T003 | 消费者接受共同语义请求、坐标依据及独立用途/批准；移除 Strict 特权、固定来源白名单和 TestSourceReference，保持动作反馈与 009 通信边界。 | A03/A04；FR-002/003/004/006/009；SC-003/008；P01/04/05/07 |
| T005 | T004 | 明确连续链 ITraceWriter/RunWrite 意图 payload 与 ITraceQuery、独立绑定原事件链；Source 来自已提交当前 F，v2 字段/摘要与历史 reader 保持，不凭历史查询恢复运行许可。 | A06；FR-003/007/009/016；SC-008；P01/07/08 |
| T006 | T005 | 固定共同检测实现、仅叶端口可替换，缺能力具体拒绝；说明脚本/rig 迁移及 UpperIsolation 不计主链，删除 DetectionTestMode 的后续任务已登记。 | A08；FR-002/006/008/012；SC-001/003；P01/02/05/13 |
| T007 | T006 | 对应 spec/contracts/plan/tasks 写清每 profile 强制 L、最终拒绝点、D10 当前映射迁移及 009 原范围；010 本设计义务保持，已登记不冒充实施，完成记录能逐项核 A01—A09。 | A09；FR-011/013/014/015/018；SC-004/005/007；P01/05/13 |

### 阶段2：共同模型与规则基础

阻塞各故事的共享基础；A01—A09对应接口先对齐，再演进当前模型、规则与消费者。

- [X] T008 建立共同 typed 输入及公共端口事实形状；文件：backend/src/Gaode.Application/Recipes/RecipeContracts.cs；backend/src/Gaode.Application/Recipes/ExecutionInputs.cs（新增）；backend/src/Gaode.Application/Ports/StagePortContracts.cs、DeviceMessages.cs、CaptureAlgorithmMessages.cs、PersistenceMessages.cs；backend/src/Gaode.Application/Ports/ICapturePort.cs、ITraceWriter.cs、ITraceQuery.cs、IStageHandoffQuery.cs。
- [X] T009 提取唯一业务校验与规划/预算共同规则；文件：backend/src/Gaode.Application/Recipes/RecipeDefinitionValidator.cs（新增）、RecipeRunPlanner.cs；backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs、RecipeExecutionBudget.cs。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T008 | T007 | 按 data-model/IB 建 RecipeDefinition、CaptureProfile、AlgorithmRequirement/BoundCapability、CoordinateDefinition/依据、HandlingTargets、DecodedTrayCode、SourceFact/ApprovalScope/AdmissionDecision、FrozenExecutionInputs；公共形状无 fixture JSON/路径/Worker 启动知识，来源枚举不重排。 | A01—A07；FR-001/003/004/005/007/009；SC-003/008；P03/05/07/08 |
| T009 | T008 | 复用对象/面/路线/必检/聚合关联和容量规则；所有目录提供者调用同一 validator/planner；移除 TestEligibleSlots/测试成本解析，使用批准冻结 ExecutionCostProfile/BudgetRef/Digest，保留原数值、起点和绝对截止。 | A01/A07；FR-001/003/004/009；SC-003/006/008；P03/04/08/11 |

### 阶段3：US1 独立运行一个合法模拟配方（P1）

目标：正式启动到同轮 FinalUnloadCompleted 的唯一共同路径。独立业务验收条件：实际 Host/独立 PLC/Worker、采集/数据库/媒体、既有授权确认及必要失败拒绝可观察；最终在 T044 的 V01/V05 验证。依赖 US3 义务迁移和 US4 门禁后方可验收，不宣称故事无跨阶段依赖。依据 RCP-001/002/005、SRT-009、DAT-001/007，P02—P09/P13。

- [X] T010 [US1] 迁移输入、规划、坐标、聚合与期限合同测试；文件：backend/tests/Gaode.Contracts.Tests/Recipes/RecipeCatalogTests.cs、RecipeRunPlannerTests.cs、RecipeExecutionCoordinatorTests.cs、PublicPreparationTargetResolutionTests.cs、FaceResultAggregatorTests.cs；backend/tests/Gaode.Contracts.Tests/Station01/RecipeApplicationContractTests.cs。
- [X] T011 [US1] 分离文件解码、坐标/参数配置及批准准入；文件：backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs、RecipeCatalogFactory.cs；backend/src/Gaode.Infrastructure/Recipes/RecipeEnvironmentDecoder.cs（新增）。
- [X] T012 [US1] 提取共同坐标计算及检测/处置目标解析；文件：backend/src/Gaode.Application/Recipes/CoordinateResolver.cs（新增）；backend/src/Gaode.Application/Station01/PublicPreparationHandoffV2.cs；backend/src/Gaode.Application/Workflow/RecipeSortingMapper.cs。
- [X] T013 [US1] 建立边界 F 解码和能力绑定并删除业务测试码策略；文件：backend/src/Gaode.Application/Station01/Steps/FScanStep.cs；backend/src/Gaode.Application/Capabilities/CapabilityRegistry.cs、Station01Policies.cs、TestTrayCodePolicy.cs；backend/src/Gaode.Host/Composition/CapabilityRegistration.cs；backend/src/Gaode.Infrastructure/Recipes/RecipeEnvironmentDecoder.cs；backend/src/Gaode.Domain/Station01/FCodePolicy.cs；backend/tests/Gaode.Contracts.Tests/Capabilities/TestTrayCodePolicyTests.cs；backend/tests/Gaode.Contracts.Tests/Recipes/RecipeEnvironmentDecoderTests.cs（新增）。
- [X] T014 [US1] 让实际采集端口生产并关联保存采集事实；文件：backend/src/Gaode.Infrastructure/Devices/Cameras/CameraCaptureAdapter.cs；backend/src/Gaode.Infrastructure/Simulation/FileBackedCapture.cs、SimulatedCapture.cs；backend/src/Gaode.Application/Acquisition/AcquisitionCoordinator.cs、CaptureEvidenceGate.cs；backend/src/Gaode.Application/Station01/RunExecution.cs；backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs。
- [X] T015 [US1] 保存当前算法/F 实际生产者并为移交提供可信来源；文件：backend/src/Gaode.Application/Station01/Steps/FScanStep.cs；backend/src/Gaode.Application/Station01/RunExecution.cs；backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs、TraceQuery.cs；backend/src/Gaode.Infrastructure/Algorithms/PythonWorkerAdapter.cs。
- [X] T016 [US1] 改造冻结/移交消费并删除非严格执行特权；文件：backend/src/Gaode.Application/Station01/StartPublicPreparation.cs、StageHandoffBuilder.cs、PublicPreparationHandoffV2.cs、RunExecution.cs；backend/src/Gaode.Application/Recipes/RecipeApplicationCoordinator.cs、IndependentRecipeApplication.cs、IndependentBindingEventWriter.cs。
- [X] T017 [US1] 提取唯一共同检测执行与单图/融合/保存路径；文件：backend/src/Gaode.Application/Workflow/RecipeDetectionExecutor.cs（新增）；backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs。
- [X] T018 [US1] 迁入换面、成组/整体、E 和旋转的现有有效共同分支；文件：backend/src/Gaode.Application/Workflow/RecipeDetectionExecutor.cs；backend/src/Gaode.Application/Ports/DeviceMessages.cs；backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Acquisition.cs。
- [X] T019 [US1] 共同处置消费 typed 目标并维护整盘来源及完成门禁；文件：backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs、RecipeSortingMapper.cs、SortingTargetAllocator.cs、WholeTrayWorkflowOrchestrator.cs、WholeTrayCompletionContracts.cs；backend/src/Gaode.Infrastructure/Persistence/WholeTrayCompletionStore.cs。
- [X] T020 [US1] 承接已提交事实查询与历史读取消费者；文件：backend/src/Gaode.Host/Api/CommittedResultProjection.cs、RunMediaCatalog.cs、RunEndpoints.cs；backend/src/Gaode.Infrastructure/Persistence/TraceQuery.cs、RecipeApplicationProjection.cs、RecipeApplicationHistoryReader.cs、DeviceEvidenceHistoryReader.cs。
- [X] T021 [US1] 统一正式主链装配并移除环境后段开关；文件：backend/src/Gaode.Host/Composition/AdapterBindings.cs、Station01Registration.cs、Station01RuntimeOptions.cs；backend/src/Gaode.Host/Program.cs；backend/src/Gaode.Application/Station01/StartPublicPreparation.cs。
- [X] T022 [US1] 编制正式装配及单配方完整链验证入口；文件：backend/tests/Gaode.Integration.Tests/CommunicationFixtures/FormalHostCompositionTests.cs；backend/tests/Gaode.Integration.Tests/Station01/ThreeStageMainFlowIntegrationTests.cs；backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs（新增）。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T010 | T009 | 按迁移组 M01/M02 先形成语义测试/独立预期；共同请求一律拒占位；精确方法与独立数据行保留，允许在被承接实现完成前暂红，不以规划器输出生成 expected；B 前必须由 T044 验证。 | VG-06 M01/M02；FR-004/009/011/012；AC-06/07；SC-005/006；P03/04/08/13 |
| T011 | T010 | 文件版本、fixture 字段、码/参数/坐标映射留边界，产有语义输入并调用 T009 同一校验；删除 D07 原业务 raw payload 通路，无两套校验/JSON 套壳；保留历史条目可读和未批准拒绝。 | A01/A07；D07（解析部分）；FR-001/003/007；SC-003/008；P03/05/10/11 |
| T012 | T011 | 共同 resolver 执行测量关联、偏置、单位/基准、面/样本/物理槽及范围校验；环境只提供依据不预算业务 Z；生成 typed 检测/分拣目标，移除 TestHeightOffset/testSourceRef/固定来源白名单解释。 | A01/A03/A04；D07（目标部分）；FR-003/004/007；AC-06；SC-003/008；P03/07/11 |
| T013 | T012 | 共同 F 单次采集/算法后消费 DecodedTrayCode，唯一匹配选定版本；需求绑定公共 3D/F、单图/融合及所需 E，无 Test 默认能力/ExpectedVersion 来源兜底；T001 复核消费者后删除 TestTrayCodePolicy，原两码义务迁入边界测试。 两原方法迁入 RecipeEnvironmentDecoderTests 后删除空的旧测试类/文件，不删原码与稳定映射保护。 | A02/A08；D06/D07（能力）；FR-005/006/007；SC-003/008；P03/07/10/11 |
| T014 | T013 | 区分 requested、实际 settings、Applied/ConfiguredOnly/NotApplied/Unknown 与重放事实；匹配 Request/Capture/epoch/digest，保留 Ended+media、首 owned buffer、意图前置与释放；删除 Unknown→fixture、固定 Test MediaRef/假应用事实分支。 | A05；D08（生产者）；FR-007/009/010；SC-008；P06/07/08/09 |
| T015 | T014 | Origin 随当前 Run/Capture/Call 的必要算法事实实际提交后才 F 完成；原码/映射依据可追溯，不取 WorkerSession/预期版本代来源；缺提交、错 Call、Unknown 拒绝依赖后继，保存失败保释放与日志；不扩 worker wire。 | A02/A05/A06；FR-005/007/009/010；SC-008；P06/07/08/09 |
| T016 | T015 | 版本化 typed intent 经既有真实保存链/回执授权，Handoff.Source 仅取已提交匹配 F；v1/v2 均完整共同校验，保留原期限锚点和 v2 摘要；删除 D03 Strict 分支、frozen-plan-0/零坐标/nonStrictPending/skip 特权，不删合法 context/1.0 或独立 plan/bind。 | A03/A06/A07；D03；FR-002/003/005/007/009/010；SC-003/008；P04/07/08/09 |
| T017 | T016 | 迁用现有效相机/算法调用、批次/结果聚合及真实保存，typed 配置/目标与绑定能力决定执行；去 TestCamera、detection.test、test-detection/1、Test Purpose 默认、fixture 解析和测试续接；保留取消/期限/租约/失败日志。旧文件有效义务移完后由 T031 删除。 | A03/A05/A08；D07；FR-001/002/003/006/009/010；SC-001/003/008；P02/04/05/06/09 |
| T018 | T017 | 以冻结实体/面、反馈及结果驱动分支，迁义务不建 Production 执行器；共用初始测量、共享实体动作、授权人工占用/清零、E 缺码/错误、旋转姿态/出口保持，辅助语义依据无 Test 格式，不泄露 009 协议。 | A03/A04/A08；FR-001/002/004/009/010；SC-003/006/008；P02/03/04/05/09 |
| T019 | T018 | 删除 simulationOnly/Test JSON 分拣分支及按环境写来源；保留目标预留/冲突、取料在途提交再放料、未知不重发、WholeTray 保存后解锁、授权取盘后 Final；来源矩阵按实际组件形成且 Unknown/不可验证真实原子拒绝。 | A03/A04/A05/A06；D07/D08；FR-002/004/007/009/010；SC-001/006/008；P04/07/08/09 |
| T020 | T019 | 投影 requested 与 actual 分开、缺 actual 不补值；保同 Run/对象/面/质量/完整性/终态以及 Ready/Final 来源引用和旧枚举值；旧 v1/v2 摘要及 Rescan 历史可读不授权执行。核对 frontend/src/runtime.js 现消费，不新增页面/原型或 schema 升级。 | A05/A06/A07；D08（查询）/D09；FR-007/009/016；SC-008；P07/08/12 |
| T021 | T020 | 正式 IDetectionPort 固定共同 RecipeDetectionExecutor；删除 externalVirtualPlc 控制续接和依图片/Worker 选整段服务，环境仅绑定叶设备/解析/坐标/能力/准入；缺能力返回具体拒绝，不整段假成功。D04/D05 类型/脚本余留由 T032 一次删除。 | A08；FR-001/002/006/008/010；AC-01/02；SC-001/003；P02/05/09 |
| T022 | T021 | V01 两组合法环境实际 DI/调用及缺能力拒绝；V05 经现 HTTP context/2.0 正式启动、独立 VirtualPlc/Worker、真实 SQLite/媒体，到授权取盘 Final；harness 只准备/既有操作/观察/断言，不写阶段完成，不代业务续接。复用源/最终查询断言留给 T029，无额外正常整盘。 | VG-05 V01/V05；FR-006/008/010/012；SC-001/006/008；P02/05/09/13 |

### 阶段4：US2 更换环境输入或实现，共同业务无需修改（P1）

目标：在 B 前准备全部替代者及独立预期；冻结后才执行 E/S。独立验收条件：五类实质替换、共同业务源0内容变化、等价动作/判断/保存，合法结果变化按规则处理；T046/T047完成。依据 RCP-002/003/005、POS-001/002、ALG-013，P03—P05/P07—P11。

- [X] T023 [P] [US2] 实现独立 ContentSampleWorker/1 的代表能力；文件：scripts/010-content-sample-worker.py（新增）。
- [X] T024 [P] [US2] 实现替代坐标/配置提供方式及正式环境绑定；文件：backend/src/Gaode.Infrastructure/Recipes/SemanticRecipeInputProvider.cs（新增）；backend/src/Gaode.Host/Composition/CapabilityRegistration.cs、AdapterBindings.cs；backend/src/Gaode.Host/Composition/Station01RuntimeOptions.cs。
- [X] T025 [US2] 在 B 前准备 B/E/S 输入、五类替换对照及独立预期；文件：backend/tests/Gaode.Integration.Tests/Fixtures/RecipeExecution010/（新增输入目录）；backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010Expectations.cs（新增）。
- [X] T026 [US2] 编制第二实现的实际内容处理自核与替换证据断言；文件：scripts/tests/test_010_content_sample_worker.py（新增）；backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T023 | T022 | 按 R10/IB-03 解析实际 PNG tEXt gaode010.sample.v1；微米→mm、媒体 F 原码、score 单图/双图质量及身份聚合，实际读取/SHA/Accepted/Result/InputReleased/PIDSessionCall 可核；只实现 Height/F/四单图/两融合，禁止转调旧算法/复制结果表；保现协议及代表 10s 配置，不扩 E/旋转能力。 | R10；IB-03；VG-07.1；FR-006/007/017；SC-002/008；P06/07/11 |
| T024 | T022 | 另一 shape/独立语义坐标输入同 validator/resolver；另一合法配方标识/F 映射及相机/算法/坐标/解析绑定，叶设备提供等价反馈；Host 实际启动 T023 并冻结参数/能力/代码与配置身份，wire 未送字段不声称送达；共同实现不随绑定变化。 | A01/A02/A08；IB-01/02/03/05；FR-003/004/005/006/017；SC-002/003；P03/05/11 |
| T025 | T023、T024 | 预先确定 Q02/CD/P01,P03 与 S 身份、不同真实图像内容、双 shape/F 标识/坐标/装配，以及 R10 两实现高度11/11、偏置100、C0.1/D0.2/阈值0.5等价规则；expected 引自 B1—B11/输入算术，不调用被测 validator/planner/executor、不倒抄运行结果，不改旧008 fixture。 | VG-05.1/07；FR-012/017/018；AC-04/05/06；SC-002/006；P03/07/11/13 |
| T026 | T025 | 同一新处理函数的不同测量/score 输入证明计算变化；核 F、双图身份、租约/释放、实际提供者身份/输入 SHA 与 Host 保存对应；路径/摘要/名称/参数不同或包装旧实现不算替换。自核列入 B 的 V02，E 的证据复用 T022/T025，不加第三条正常链。 | VG-07.1；FR-012/017；SC-002/008；P06/07/13 |

### 阶段5：US3 纠正测试保护义务并清理失效代码（P1）

目标：按断言和独立数据行分类承接，有效义务不弱化，确认无用代码实际删除。独立验收条件：VG-06全部原方法/行映射到当前真实证据，D01—D11删除/保留有消费者依据；T048收口。不用UpperIsolation替代主链。依据DAT-001/007、FR-011/012/016，P01/P04/P07—P09/P13。

- [X] T027 [US3] 迁移上层协调、有限期限与移交保护测试；文件：backend/tests/Gaode.Contracts.Tests/Workflow/ThreeStageWorkflowExecutorTests.cs、DetectionRetryAndPendingTests.cs、StageRetryPolicyTests.cs；backend/tests/Gaode.Contracts.Tests/Station01/PublicPreparationHandoffV2Tests.cs；backend/tests/Gaode.Integration.Tests/Station01/ThreeStageMainFlowIntegrationTests.cs。
- [X] T028 [US3] 承接实际生产者、采集事实与 F 移交独立负例；文件：backend/tests/Gaode.Contracts.Tests/Workflow/WholeTrayWorkflowOrchestratorTests.cs；backend/tests/Gaode.Contracts.Tests/Acquisition/CaptureCoordinationTests.cs、MessageContractTests.cs；backend/tests/Gaode.Contracts.Tests/Station01/FScanStepTests.cs、PublicPreparationHandoffV2Tests.cs；backend/tests/Gaode.Integration.Tests/Station01/PublicPreparationHandoffV2IntegrationTests.cs。
- [X] T029 [US3] 承接来源矩阵、查询、最终主体及历史消费者测试；文件：backend/tests/Gaode.Integration.Tests/Storage/ComponentSourceMatrixStoreTests.cs；backend/tests/Gaode.Integration.Tests/Api/CommittedResultProjectionTests.cs、Station01SourceMatrixApiTests.cs、RunMediaCatalogTests.cs；backend/tests/Gaode.Integration.Tests/Station01/FinalUnloadCompletionIntegrationTests.cs；backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs。
- [X] T030 [US3] 迁移实际检测及受影响分支代表并固定 S；文件：backend/tests/Gaode.Integration.Tests/Devices/SingleFaceDetectionIntegrationTests.cs；backend/tests/Gaode.Integration.Tests/CommunicationFixtures/SingleFaceDetectionIntegrationTests.Wire.cs、RecipeMultiObjectIntegrationTests.Wire.cs、RecipeRotationIntegrationTests.Wire.cs；backend/tests/Gaode.Integration.Tests/Station01/RecipeMultiObjectIntegrationTests.cs、RecipeRotationIntegrationTests.cs、Q01CameraMediaTests.cs、VirtualRecipeAndDetectionGateTests.cs。
- [X] T031 [US3] 复核后删除历史复扫/后翻组件及旧检测实现文件；文件：backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs；backend/tests/Gaode.Rules.Tests/Architecture/010-test-obligations.json。
- [X] T032 [US3] 删除整段模拟检测、模式参数与无效服务测试；文件：backend/src/Gaode.Infrastructure/Simulation/SimulatedDetectionPort.cs；backend/src/Gaode.Infrastructure/Integrations/NotIntegratedStagePorts.cs；backend/src/Gaode.Host/Composition/Station01RuntimeOptions.cs、AdapterBindings.cs；backend/src/Gaode.Host/Program.cs；scripts/verify-latest-plc.py；backend/tests/Gaode.Contracts.Tests/Simulation/DetectionAdapterSourceTests.cs。
- [X] T033 [US3] 先迁移 009 活动映射再删除重复错映射测试；文件：backend/tests/Gaode.Integration.Tests/Station01/VirtualRecipeAndDetectionGateTests.cs、ThreeStageMainFlowIntegrationTests.cs；scripts/workflow/009-required-cases.json；backend/tests/Gaode.Rules.Tests/Architecture/009-test-obligations.json。
- [X] T034 [US3] 闭合逐方法迁移与 D01—D11 实际删除/保留清单；文件：backend/tests/Gaode.Rules.Tests/Architecture/010-test-obligations.json；scripts/workflow/migration_audit.py；artifacts/recipe-execution-010/preparation/dependency-audit.json。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T027 | T022 | 落实 M02/M03，精确区分物理失败与算法 Pending、未知占用/旧关联、必要保存与取消，保期限三入口；去 Strict/Test payload/错顺序断言，UpperIsolation 显式标记不充主链，三种持久拒绝独立登记。 | VG-06 M02/M03；FR-009/011/012；SC-005/006/008；P04/07/08/13 |
| T028 | T027 | 按下表 SRC-01—07/NF 精确行及 M02：版本/混合/相机Unknown/算法Unknown分别保留；补真实采集关联及 F 缺提交/错Call/未知Origin，保存拒绝不靠正常链抵扣；旧 prebuilt 特权断言删。SRC-07c 实际端口执行归 T030，SRC-05b/c 正例归 T022/T029。 | VG-06 SRC/NF；FR-007/009/011/012；SC-005/008；P07/08/13 |
| T029 | T026、T028 | SRC-08—13/NF-PROJECTION 按行承接：SQLite Missing/Unknown/Unverifiable 原子拒绝、不可变 Ready/Final、Test 人工主体、提前409/正常确认/重放/引用计数；V05共享实际保存/查询正例，旧直接写阶段完成只可做查询组件 fixture。按实际 helper 变化纳入投影条件行；历史 Rescan reader 保留。 | VG-06 SRC/NF；D09；FR-007/009/011/012/016；SC-005/008；P07/08/12/13 |
| T030 | T026、T029 | 按 M04、SRC-06/07c/14 保真实 AB、四失败、未知F/非法算法，CD由V05；普通双面/组 mixed+四面/人工整体E/旋转必要行实际共同组件；S=AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce 仅准备、不放B执行清单，冻结后一次共担V04/V06。通信 Wire保留，业务helper受门禁。 | VG-05/06；FR-004/009/010/011/012/018；SC-005/006/008；P03/04/05/09/13 |
| T031 | T030 | 复核 T001 当前消费者及活动清单，确认 T017/T018 有效业务和 T030 义务已承接后，实际删除 ExecutePostFlipComponentAsync、TestPostFlipStageContext、RescanWholeTrayAsync、componentHeightRound 专属路径及迁空的旧文件；不保兼容入口，不删 D09 历史枚举/读取。 | D01/D02；FR-016；AC-08；SC-005；P01/03/08/13 |
| T032 | T031 | 实际删除 SimulatedDetectionPort/Profile、NotIntegratedDetectionPort（保留同文件其他合法端口）、DetectionTestMode 参数/分支/脚本及旧三方法；M03 的来源/缺对象/生产拒绝已承接，脚本只驱动已有操作/叶失败输入，无备用整段实现。 | D04/D05；A08；FR-002/006/011/016；SC-003/005；P02/05/13 |
| T033 | T032 | 把唯一 StageEvent、零 Sorting、Terminal None 与证据输出全部映射到 PersistedHandoffRecordsPendingMappingFailedAndUnknownHeldWithoutFalseCompletion 的 wrong-mapping 行；先更新当前 FQN/消费者再删除 Current007AmbiguousDetectionMappingBlocksBeforeSorting；其他动态/配置引用复核，旧报告/源快照不动。 | A09/D10；FR-011/012/016/018；SC-005/008；P01/05/08/13 |
| T034 | T033 | 逐项核 M01—M04、SRC/NF 的原方法→当前FQN/case/dataRow/证据层级，分类与独立依据完整；D01—08/D10 按任务实际删除而非待考虑，D09/D11及有效义务保留；无法确认项列具体当前依赖并阻止虚报删除完成。条件项在B前按改动锁定，不删除失败义务。 | VG-06/08；D01—D11；FR-011/012/016/018；AC-07/08/09；SC-005；P01/08/13 |

### 阶段6：US4 架构回归能够被专项门禁拒绝（P1）

目标：复用现设施实现L及010完整profile，后续所有验收入口不能绕过。独立验收条件：同检查器全部N/P、报告G和调用C样本实际接受/拒绝，当前正式闭包与入口完整；T044验证。规则可在T009后提前建设，强制接线必须早于B。依据FR-013—015及009通信保护，P01/P05/P09/P13。

- [X] T035 [US4] 复用现设施实现职责闭包与五类共同边界检查；文件：backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryChecker.cs（新增）、ProtocolBoundaryChecker.cs；scripts/workflow/recipe_execution_010.py（新增）。
- [X] T036 [P] [US4] 编制同一检查器的全部 N/P 正负样本；文件：backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryTests.cs（新增）；scripts/architecture/010-script-boundary-cases.json（新增）。
- [X] T037 [P] [US4] 固定 L 与 010 必需清单及 B/E/S/T 映射；文件：scripts/workflow/010-lightweight-cases.json（新增）、010-required-cases.json（新增）。
- [X] T038 [US4] 实现当前执行凭证及统一证据完整性判定；文件：scripts/workflow/recipe_execution_010.py；scripts/workflow/runner.py、migration_audit.py。
- [X] T039 [US4] 编制 G01—G07 的当前判定器完整性样本；文件：scripts/workflow/test_verify.py；scripts/workflow/test_recipe_execution_010.py（新增）。
- [X] T040 [US4] 接线每个 profile 强制 L 及全部最终拒绝点；文件：scripts/verify.ps1；scripts/workflow/verify_entry.py、runner.py、step.ps1、boundary_minimum.py、protocol_isolation.py；scripts/verify-009-protocol-isolation.ps1；workflows/auto-dev.yml。
- [X] T041 [US4] 验证 C01—C03 的跨 profile 调用与最终拒绝；文件：scripts/workflow/test_runner.py、test_verify.py、test_protocol_isolation.py、test_recipe_execution_010.py。
- [X] T042 [US4] 实现 010 最小验收的 B→冻结→E/S→V07 调度；文件：scripts/workflow/recipe_execution_010.py、runner.py；scripts/verify.ps1；scripts/workflow/010-required-cases.json。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T035 | T009 | VG-02 B01—B05 沿正式入口/调用/类型/Compile链接和业务 helpers有限传递，递归公共形状与DI；合法端口/文件解码/准入为停止点但不能藏工序/保存；根缺失、未知动态、漏枚举/分类/解析失败拒绝。复用009脚本解析与协议规则，不新建平台。 | A09；FR-013/014/018；AC-10/11；SC-003/004；P05/13 |
| T036 | T035 | 覆盖 N01a/b/c、N02、N03a/b、N04、N05a/b 和 P01/02/03；含无Test字样字段、间接helper、移目录/链接工程/改Role，核具体拒绝码/符号/位置；允许合法来源、固定图和准入，不能按样本标签特判。 | VG-03；FR-013/014；AC-10/11；SC-004；P05/13 |
| T037 | T025、T026、T034、T035 | 独立于发现结果登记所有精确case/FQN/dataRow/义务/evidenceKinds/预期与输入身份；L为B/N/P/G/C和受影响009静态；B含V04除S所有必要项，CD正例只挂V05，S明确仅冻结后。T039/T041 方法按预定身份实现，最终由T043核一致。 | VG-01/04/05.1/06；FR-011/012/015/018；AC-12；SC-005/006/007；P01/13 |
| T038 | T036、T037 | 核attempt/profile/source/闭包/合同/build/checker/manifest/input/时间及实际发现/执行/dataRow/报告摘要，额外TRX-PARSE也拒；只读保原子证据身份，T按映射去重，受影响B/当前L/失效E/S不可旧证据复用；合法未受影响B引用须完整依赖不变证明，不信passed布尔。 | VG-01.1/04；FR-012/015/017；AC-12；SC-007；P07/08/13 |
| T039 | T038 | 同生产函数覆盖必需缺失、零发现/漏执行、Skip/filter、全部解析失败含额外行、各旧身份、遗漏数据行/移动改类/重复/缺证据以及当前完整正例；不运行Host/PLC/Worker/DB，不维护测试专用判定器。 | VG-04；FR-015；AC-12；SC-007；P13 |
| T040 | T039 | 统一run_verify/_run_verify无条件L；默认009/其他profile/BoundaryMinimum/SelectedCasesOnly均执行或合法复用本父attempt L。_run_verify passed、verify_entry退出、assess/finish、两旁接聚合ledger/result/subsetPassed/返回核同凭证，缺/旧/解析失败/伪Passed拒绝。无skipL/forcepass；保overall009Passed=false子集语义。 | A09；VG-01.1；FR-013/015/018；AC-12；SC-007/008；P05/13 |
| T041 | T040 | 四入口/profile行均触发L且N02/N04令其他项全绿仍拒；缺调用/旧凭证/旧行改身份/伪Passed逐最终点拒；合法当前与合法原身份引用可通过。使用同调度/核验组件及报告样本，不递归启动完整验收，不把调试测试标项目通过。 | VG-01.1 C01/C02/C03；FR-013/015/018；SC-007；P05/13 |
| T042 | T041 | RecipeExecution010 profile按T044—T048检查点自动推进并保存独立证据；每步实际必需结果成立才进入后继，S不作B前置；固定内部筛选、按case/run共享，只有两条正常完整链；冻结重枚举与变更作废逻辑落地，无阶段跳过或强制通过参数。 | VG-05.1/07；FR-012/015/017/018；SC-002/006/007；P08/13 |

### 阶段7：跨故事最小验收与最终收口

以下是待执行任务；本轮只写文档。所有开发/迁移/清理/门禁及输入预期准备完成后，统一runner按B→冻结→E/S→V07推进；禁止冻结后安排新的共同代码改动。

- [X] T043 核验实施前置、独立预期和最终必需集合已齐备；文件：scripts/workflow/010-required-cases.json、010-lightweight-cases.json；backend/tests/Gaode.Rules.Tests/Architecture/010-test-obligations.json；artifacts/recipe-execution-010/preparation/。
- [X] T044 经统一入口取得修复后完整基线 B；文件：scripts/verify.ps1（计划实现的 -Profile RecipeExecution010）；scripts/workflow/010-required-cases.json；artifacts/recipe-execution-010/{verificationRunId}/B/。
- [X] T045 在 B 通过后冻结实际共同职责、合同、预算和预期清单；文件：scripts/workflow/recipe_execution_010.py；artifacts/recipe-execution-010/{verificationRunId}/freeze.json。
- [X] T046 冻结后执行五类组合等价替换完整链 E；文件：scripts/workflow/recipe_execution_010.py；backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs；artifacts/recipe-execution-010/{verificationRunId}/E/。
- [X] T047 在同一冻结下执行一次共享代表 S；文件：backend/tests/Gaode.Integration.Tests/CommunicationFixtures/RecipeMultiObjectIntegrationTests.Wire.cs；artifacts/recipe-execution-010/{verificationRunId}/S/。
- [X] T048 V07 核对最终 T、删除证据和剩余限制后声明专项结论；文件：scripts/workflow/recipe_execution_010.py、migration_audit.py；artifacts/recipe-execution-010/{verificationRunId}/result.json。

| ID | 直接前置 | 完成判据与证据 | 承接需求/合同/原则 |
| --- | --- | --- | --- |
| T043 | T042 | 逐项审阅A01—A09实际对齐、共同修复、M/SRC/NF、D清理、L接线与消费者、B/E/S全部输入替代者/oracle及条件义务；原失败/局部限制不改写。清单审阅后封存待B，不以静态齐备宣称B通过。 | FR-001—018；AC-01—12；SC-001—008；P01/10/13 |
| T044 | T043 | 执行V00/V01/L/V02/V03/V05及V04除S：受影响工程构建、所有独立负例/必要组件和第一条Q02完整链实际通过；S准备但未执行不阻B。当前报告逐项有效，无缺失/Skip/filter/解析失败/旧报告；失败停止冻结，按设计修复重取。 | VG-05.1 B；FR-001—018；SC-001/003/004/005/006/007/008；P02/13 |
| T045 | T044 | 检查点生成路径/符号/职责/Compile链接/SHA256清单，含业务helpers、相关合同、预算/期限、先已审阅oracle和manifest；冻结时B身份完整且源未变。此为证据生成/核对任务，不再修改脚本或业务；重分类不能缩范围。 | VG-02/05.1/07；FR-013/017；SC-002/003；P05/08/13 |
| T046 | T045 | 使用T023—T026已备替代者，经同正式入口到Final；五类各有实际差异/处理证据，核ContentSampleWorker/1真实读/算/释放/保存；共同业务零改动、同独立预期动作/判断/保存等价，真实来源/Run/时间差异如实记录；无第三条同义正常链。 | VG-07/07.1 E；FR-003/004/006/007/012/017；AC-04/06；SC-002/008；P07/08/11/13 |
| T047 | T046 | 执行已登记AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce共同组件；核整体NG优先、Pending细节/理由、实体仅一次搬运及真实保存，同时映射V04/V06；不重启完整盘、不追加另一超时整盘，冻结依旧有效。 | VG-05.1 S；FR-004/009/012/017；AC-05；SC-002/006/008；P03/04/08/13 |
| T048 | T047 | T=B∪S∪E∪V07按原attempt/case/dataRow/run与obligation去重，核全部有效义务、实际删除、历史保留、当前L/构建/来源/保存及冻结；任何必需失败/缺失/未执行总拒绝。保旧失败/未验证和生产限制，列实际路线，单配方或L通过不能单独标010完成；此任务不把T已通过作为输入。 | VG-04/05.1/06/08；FR-001—018；AC-01—12；SC-001—008；P01/08/10/13 |

## VG-06 方法、数据项与证据承接

以下是任务到既有 VG-06 的执行索引，分类和独立依据沿用 VG-06 B1—B11，不重写业务预期。CT=backend/tests/Gaode.Contracts.Tests，IT=backend/tests/Gaode.Integration.Tests；相对路径继承该根。同一方法可以拆出不同证据层级，不能以整类必跑代替判断。

T001 起草、T034 闭合的 010-test-obligations.json 必须将下列每个原方法/独立 dataRow 写为唯一义务，保存：原文件/namespace/class/method/dataRow、原断言、B1—B11 条款、保留/迁移/替换/删除、当前 method/FQN/caseId/dataRowId、主要交付任务、B/E/S 所属、V 编号、证据层级、复用目标/逐断言及条件适用理由。已列原方法保留名称时，目标 FQN 取源码 namespace/class 而非猜文件目录；改名/拆行同时更新映射与固定清单，找不到即失败。新 NF 的目标方法在后表明确落定；同一数据集复用不丢独立行。T037 的发现要求不得从运行发现结果反推。

层级：**UpperIsolation** 仅协调规则，允许整段替身；**Component** 为实际规则/投影；**StorageComponent** 真实 SQLite；**PortComponent** 实际正式适配/端口；**FullRun** 为正式入口、独立进程和真实保存至最终完成。声明 Real 的单元输入仍是协调测试，不能升级硬件证据。所有表内实际 B 执行归 T044，S 归 T047，E 归 T046；编写测试不等于执行通过。

### M01 输入/规划/目标/聚合（主要交付 T010，解析迁移 T013）

| 精确原文件 / 方法 | 必要数据项、分类与最小承接 |
| --- | --- |
| CT/Capabilities/TestTrayCodePolicyTests.cs：FixtureFCodeResolvesToReviewCatalogCode；OtherFixtureCodesRemainStable | TEST-TRAY-0001 配置映射/保原码、TEST-TRAY-0042 不乱重写。迁移 B1/B7 到新增 CT/Recipes/RecipeEnvironmentDecoderTests.cs 同名两方法；T013 删除旧空类。V02 配置边界 Component，业务不固定测试ID |
| CT/Station01/FScanStepTests.cs：FUsesOneTriggerOneImageAndDoesNotPerformRecipeOperation；FailedAlgorithmFactSaveDoesNotSubmitFCompletion | 保单触发/单图/调用/意图、F不预绑定及保存失败无完成，迁固定码；T028 的 NF-F-ORIGIN/SAVE 扩同方法，V02；B1/B3/B4 |
| CT/Recipes/RecipeRunPlannerTests.cs：SameFrozenInputsCreateStableOrderedPlan；DuplicateUnknownOrEmptyOccupiedSlotsAreRejected | 保稳定顺序/版本/摘要并补独立预期；重复P01、UNKNOWN、空集合三行均拒绝，V02；B1/B2/B7 |
| CT/Recipes/RecipeCatalogTests.cs：Q09IncompleteOrMisboundFaceCannotBecomeExecutable | missing-camera、wrong-source-slot、cross-face 三行：共同拒绝保留，JSON变造迁边界；V02，B1/B2 |
| 同：VirtualMultiFaceCatalogsHaveCompleteFrozenTargetsAndWorkload | 11行 Q04=AB,CD；Q05=CD,AB；Q06=CD,CD；Q08=AB,AB,AB,CD；Q09=AB,AB,CD,AB；Q11=AB,CD,AB,AB；Q14=AB,CD,CD,CD；Q15=CD,AB,AB,AB；Q18=CD,AB,CD,CD；Q20=CD,CD,AB,CD；Q21=CD,CD,CD,AB。保路线/工作量/无重扫、迁文件交叉引用；V02低成本数据，不跑11盘；B1/B2/B6 |
| 同：HistoricalOutOfScopeCatalogRemainsReadableButCannotDispatch | q07/q10/q12/q13/q16/q17/q19/q22八行可读并拒FourFaceBusinessScopeExcluded；V02，不跑八盘；B1 |
| 同：Q03TwoFaceFixtureUsesInitialHeightAndExplicitFaceTargets；Q01TestCatalogAllowsOnlyMappedSlot；Q02MappedCatalogKeepsCdBatchOrderAndNoncontiguousSlots | Q03=4采集/2融合/1翻/0重扫及初测身份；Q01 P01批准/P02拒绝移准入；Q02=C:P01,C:P03,D:P01,D:P03/物理槽1,3独立顺序。V02，Q02动态正例共享V05；B1/B2/B7 |
| 同：Q01ParamUsesSameRecipeWithChangedCaptureAndTwoSlots；ReviewCatalogLoadsAllVirtualRecipesAndAssignsStablePlcIds | 12000曝光/75亮度/模型版本/摘要/两槽及P02未批，拆冻结与批准；0.4文件68项/来源/显示ID移文件边界，读样本+一未批拒，不在业务固定68。V02；参数实际事实V03；B1/B6/B7 |
| 同：PlannerBindsRecipeOnlyFromPostFCodeAndCarriesPlcId；FileProviderSwitchKeepsTheSameRecipeContract | F后唯一/版本/显示ID，格式移边界；两shape/容量9,8均调用同校验。V02；实质替换由E，不以两文件断言冒充；B1/B7 |
| CT/Recipes/RecipeExecutionCoordinatorTests.cs：Q01MappedPlanHasCompleteSingleFaceSteps；CaptureMustFollowMatchingPositionAndUnknownStepsCannotBeSkipped | 保必检完整；相机错配、未知步骤999两负例；V02，B1/B2 |
| 同：Q01DeadlinesAreAbsoluteAndCountCaptureFusionMotionAndSaves；Q03BudgetIncludesBothFacesAndOneFlipWithoutRescan；Q03TwoSlotsAllowConsecutiveEntityFlipsBeforeNextFaceCapture | 独立108s/41s/56100ms批准输入与原三阶段起点/截止；Q01 2采集1融合0重扫、Q03 4采集2融合1翻0重扫；两个实体先各翻一次才次面。V02，翻面动作V04；非生产默认；B1/B2/B5/B7 |
| 同：StrictRecipeRequestRejectsLegacyPlaceholderPosition | 替换非严格接受为所有共同请求拒占位；删除Strict特权，不删context/1.0；V02，B2/B7 |
| CT/Recipes/PublicPreparationTargetResolutionTests.cs：Q03HandoffResolvesBothFacesFromInitialMeasurement；Q03FutureTargetRejectsWrongIdentity | 10.5+偏置→110.5/112.5、heightRound1；localFace=1与protocolSlotIndex=2两错配独立拒绝；V02，B2/B3 |
| 同：TestTargetsUseMatchingCurrentThreeDHeightAndRejectWrongSample | (10.5,11.0)→(110.5,111.0,110.5,111.0)；(11.5,10.25)→(111.5,110.25,111.5,110.25)；wrong-scope；missing sample-b。四独立项保计算/身份，替来源白名单；V02+E合法来源替换，B2/B7 |
| 同：CommittedHandoffResolvesExplicitFrozenFixedTargetsWithoutDefaultHeight | 已批准Z110/115正例、缺目标负例；V02，不默认高度；B2/B3/B7 |
| CT/Recipes/FaceResultAggregatorTests.cs：AbAndCdPairOnlyWithinSameObjectFaceAndHeightRound | AB/CD同对象/面/轮配对、未齐、重复A；V02一次数据集逐义务登记，B2 |

### M02 移交/回执/期限（T010 与 T027/T028 分工）

| 原文件 / 精确方法 | 必要数据项、承接任务与最小范围 |
| --- | --- |
| CT/Station01/PublicPreparationHandoffV2Tests.cs：OnlyPersistedMatchingV2HandoffCanConstructDetectionRequest；InMemoryNotificationCannotReplaceCommittedHandoff；EmptyReferencesOrMissingCommittedWriteCannotProduceRequest；WrongRunTrayOrPlanIsRejected | T027改typed合法输入，T028补F来源；V02同run/tray/plan/media提交正例、内存拒绝、空引用/缺Write两项、错run/tray/plan三项；保B3，不用正常链抵负例 |
| IT/Station01/PublicPreparationHandoffV2IntegrationTests.cs：DetectionCanOnlyStartFromCommittedMatchingV2Handoff | 按SRC-05a/b/c拆义务；T028负例、T029/T022实际正例，只运行V05一次，不重跑原方法后半整盘 |
| CT/Station01/RecipeApplicationContractTests.cs：RunCancellationClosesRegisteredBindingTokenWithoutTurningPauseIntoCancellation；DeviceConfirmationWithoutActualEvidenceReceiptCannotAuthorizeBinding | T010，V02暂停≠取消、缺真实回执拒绝；保B5/B8 |
| 同：EarlyPlatformTimerWakeDoesNotCloseOriginalBindingWindow | 保9999ms不误关10000ms；若直接改时间实现，T010纳入B必需；仅同值预算注入可由原窗口代表承接全部断言，T034记录判断；B8 |
| 同：BothCapacityInputsKeepTrayIdentityAndAllNecessarySavesInOneWindow；FailedIntentNeverCallsDeviceOrRegistersTotalWindow | T010，V02 capacity=true/false两项保身份/全部保存；意图失败无设备/t0；B8 |
| 同：LateOrCancelledDeviceReturnDoesNotCreateBoundOrHandoff；ExpiredExistingDeadlineRejectsBeforeDeviceAndDoesNotRefreshIt | T010，V02 cancelled=false迟到/true取消两项、strict-before-port已过期不派发/不刷新；既有GAODE_009_EVIDENCE_ROOT只指本010独立子目录不读旧结果；B8 |
| 同：TimelyRequiredReceiptsRemainValidAfterLaterContinuation；DeviceConfirmationAloneCannotCompleteWhenRequiredSaveFails | T010，V02按时提交稍后消费仍有效、必要handoff保存失败不能完成；B8 |
| 同：LastRequiredReceiptUsesOriginalTotalWindow；EarlierDownstreamDeadlineClosesNecessarySaveWithoutRefreshingAnyDeadline；CancellationAfterBoundCommitCannotStartRequiredHandoffSave | T010，V02 9999=true/10000=false/10001=false三行；较早2000ms关窗；bound后取消无新handoff保存；B8 |
| scripts/run-009-process-case.ps1：BA06-downstream legacy-PROCESS、api-none-PROCESS | T027以共同组件核三入口：v2绑定前起点、v1移交后首次检测起点、独立bind不造/续下游窗口；保B8，不重开009全部进程矩阵 |

### M03 整段替身/上层协调（主要 T027，删除 T032/T033）

| 原文件 / 精确方法 | 独立义务与承接 |
| --- | --- |
| CT/Simulation/DetectionAdapterSourceTests.cs：SimulatedAdapterPreservesInputOutputVersionAndEvidenceWithoutTouchingPlc；ProductionRequestIsExplicitlyRejectedWithoutSimulatedSuccess；MissingExpectedObjectsDoesNotFabricateResults | 整段成功断言删除；有效版本/来源由T030 V03/T022 V05、生产不兜底由T022 V01、缺对象由T010 V02承接；T032实际删旧三方法/空类；B2/B6/B7 |
| CT/Workflow/ThreeStageWorkflowExecutorTests.cs：CompleteMappingExecutesOnlyDetectionSortingAndUnloadPreparationInOrder；MappingFailedPausesDetectionAndNeverCallsPlcPort；MissingSortingTargetBlocksBeforeUnloadMotion | V02 UpperIsolation保三阶段责任/实际分拣后再下料、错映射无物理后继、缺目标先拒下料；方法名不定义错误顺序；B2/B4/B7 |
| 同：TwoProblemEntitiesCannotReserveTheSameTargetCell；OccupiedSourceCellCannotBeAssignedAsSortingDestination；ReservationSaveFailurePreventsEvenUnloadDispatch；MixedTrayCommitsNormalMemberRetentionWithoutAnotherSortingAction | V02预留冲突/占用源位/保存失败/OK留位保存独立义务；mixed真实动作共享T030 V04；B4 |
| 同：CommunicationFailuresUseInitialAttemptPlusThreeRetriesAndOneTwoFourBackoff；AlgorithmTimeoutsUseInitialAttemptPlusTwoRetriesAndTwoFiveBackoff；SharedStageDeadlineStopsRetryBeforeItCanCrossOneHundredTwentySeconds；DetectionExecutionAtStageDeadlineCannotDispatchSorting | V02控制时钟：4次1/2/4、3次2/5、原120s；最后方法替歧义断言，迟到不直接成功，可信目标/安全/保存成立才Pending处置；B4/B5 |
| 同：PlcUnknownHeldKeepsAssociationAndNeverRetriesOrAdvances；FailedPhysicalDetectionAtDeadlineCannotBecomePendingOrDispatchUnload；StaleOperationOrEpochFeedbackBecomesUnknownHeldAndCannotCompleteCurrentAction | V02未知保关联/不重发，物理失败120s/121s两行不降Pending，旧operation/epoch不完成当前动作；B4/B5 |
| 同：DetectionDisconnectedRemainsDetectionFailureInsteadOfPlcUnknownHeld；RecoveryBlocksUnknownPlcActionAndPreservesItsOperationAndEpoch | V02断线≠PLC未知、恢复资格保旧未知身份；不扩恢复流程；B4/B5 |
| CT/Workflow/DetectionRetryAndPendingTests.cs：StrictRecipePendingWithWrongObjectPositionCannotDispatchSorting；ExhaustedDetectionRetriesCreatePendingAndContinueFormalSorting；SharedDeadlineExpiringDuringFirstAttemptDoesNotResetAndCreatesPending | V02去Strict/JSON后保错对象/位置拒绝；Disconnected/4次1,2,4与TimedOut/3次2,5两行；首次耗尽120s无重试、Pending保存后安全处置可共享截止代表；UpperIsolation不计共同检测；B2/B4/B5 |
| CT/Workflow/StageRetryPolicyTests.cs：DetectionCommunicationHasFourTotalAttemptsAndOneTwoFourBackoff；AlgorithmTimeoutHasThreeTotalAttemptsAndTwoFiveBackoff；SharedDeadlineWinsAndDetectionBecomesPending；RestartUsesPersistedDeadlineInsteadOfResettingIt；PlcPreDispatchFailureCanRetryButExhaustionIsFailed；PossiblyDispatchedPlcActionNeverRetriesAndIsUnknownHeld | 六独立义务分别登记；策略不变可由实际执行的上述重试/未知代表逐断言共享，否则相应方法纳V02；不可用不含重启原期限/未派发规则的代表空挂；B5 |
| IT/Station01/ThreeStageMainFlowIntegrationTests.cs：UnintegratedProducersCannotClaimRealExecutionOrIgnoreCancellation；PersistedHandoffRecordsPendingMappingFailedAndUnknownHeldWithoutFalseCompletion | T027迁缺能力/取消到V01/02，后者三行disconnected/wrong-mapping/unknown-held分别UpperIsolation持久保护。T033将D10的唯一StageEvent、零Sorting、终态None和证据输出并入wrong-mapping行；B3/B4/B5/B7 |
| IT/Station01/VirtualRecipeAndDetectionGateTests.cs：Current007AmbiguousDetectionMappingBlocksBeforeSorting | T033先改009活动FQN/数据映射，再删重复方法；不删原证据、不计FullRun；B2/B7 |
| scripts/verify-latest-plc.py：normal、algorithm-pending、unlock-gates、plc-unknown、host-restart | T032移除DetectionTestMode；normal→V05，待定/未知/解锁→V02/V04；重启处理未变仅保持原未验证状态，否则按实际改动加必要代表；B4/B5/B7 |

### M04 正式装配/实际端口/工艺分支（T022/T030）

| 原文件 / 精确方法 | 数据行及最小实际承接 |
| --- | --- |
| IT/CommunicationFixtures/FormalHostCompositionTests.cs：FormalHostCompositionUsesOneDeviceAndRealStageAndEvidenceProducers | T022替固定Integrated类名为唯一共同职责/同设备端口/实际调用；V01两环境+缺能力。原进程内PLC rig不计独立PLC；B5/B6/B7 |
| IT/CommunicationFixtures/SingleFaceDetectionIntegrationTests.Wire.cs：AbThenCdExecuteThroughVirtualPlcWorkerMediaAndSqlite；IT/Devices/SingleFaceDetectionIntegrationTests.cs：RunAsync | T030：Q01/P01/AB两次→V03；Q02/P01,P03/CD四次→T022 V05；逐调用/真实存储/来源，SRC-06/14核实际内容和版本；B2/B3/B6/B9/B10 |
| 同Wire：NecessaryFailureStopsBeforeNextProductMove | T030 V03 WrongArrival、MediaSave、Reset、SaveWindowExpiry四个独立数据项，核无下一产品运动及可定位日志；B3/B4/B5 |
| IT/CommunicationFixtures/RecipeRotationIntegrationTests.Wire.cs：SpecialDeviceRunsPoseAndExitWithoutOrdinarySortingReplay；IT/Station01/RecipeRotationIntegrationTests.cs：RunRotationAsync | T030 V04：(false,OK,false)实际端口代表；(false,NG,false)、(false,Pending,false)、(true,OK,false)、(false,OK,true)共同组件实际执行各出口/整体一次搬运/错姿态零检测且保占用；不五次整盘；B2/B4 |
| IT/CommunicationFixtures/RecipeMultiObjectIntegrationTests.Wire.cs：ManualFlipRequiresObservedOccupancyAndAuthenticatedConfirmationThenClearsBeforeNextFace；ManualAssemblyWithEUsesSameOccupancyAndFaceConfirmation；AssemblySharesOneFlipAndRetainsPartsWhenECodeIsPresentOrMissing | T030 V04整体人工+E实际代表共享占用/授权/清零；独立实体身份组件、missingCode=false/true两义务保共享一次翻面及部位/缺码继续；B2/B4/B5 |
| 同Wire：AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce | T030编制，T025预期；**S** FQN=Gaode.Integration.Tests.Station01.RecipeMultiObjectIntegrationTests.AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce，caseId=V04-V06-AssemblyNgPending，dataRowId=default；仅T047冻结后实际共同组件一次，不放B；B4 |
| IT/Station01/RecipeMultiObjectIntegrationTests.cs：TwoGroupsKeepCompletedMembersAndRunIndependentTargetsThroughRealPorts；FourFaceGroupsUseSourceCompositionAndDistinctETargets；EWorkerFailureLeavesIssueAndContinuesWholeAssembly | T030 V04 mixedResults=false/true且fourFaceModel=false：mixed实际端口，纯OK组件；fourFaceModel=true需真实执行组件/不同E目标/组来源，不能只查计划；E错误留原因且整体继续组件。均在B，不随S后移；B1/B2/B4 |
| IT/Station01/VirtualRecipeAndDetectionGateTests.cs：FailedFirstThreeDMotionNeverGuessesSafeZOrStartsF；IndependentWorkerUnmatchedFDoesNotReuseOldRecipe；InvalidDetectionWorkerResultStopsWithoutDefaultOk；LateIndependentDetectionWorkerBecomesFinitePendingAndSorts | T030 V03实际Worker未知F不复用旧配方、UNMAPPED无默认OK；公共准入若改则加入首3D失败，否则无后继可由WrongArrival承接；期限/Worker超时处理若改则真实迟到Pending代表必需，不用内存V02冒充。按影响在B前锁定，非因S而一律删；B1/B2/B4/B5/B6 |
| IT/Station01/ThreeStageMainFlowIntegrationTests.cs：CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite | T022 V05补至授权取盘Final；旧AwaitingManualRemoval/FinalOutcome=None不足。真实独立进程/正式入口，不直写完成；B3/B4/B6/B7 |
| IT/Api/RunMediaCatalogTests.cs：MultiFaceMediaKeepsCommittedFaceRoundAndRescanIdentity | T029 V02历史reader一个代表，保多面/轮次/Rescan身份；D09不随旧执行链删除；B3/B7 |

### SRC 与 NF 精确承接（不能由正常链抵独立负例）

SRC 表按 VG-06 保留原分类；以下每行标主迁移任务、层级和可复用实际代表。相同目标共享执行时仍逐义务核断言。

| 义务 / 精确原方法或数据行 | 主交付 / 当前目标、层级与判据 |
| --- | --- |
| SRC-01：CT/Workflow/WholeTrayWorkflowOrchestratorTests.DeviceVersionComesFromActualSemanticProducerNotCurrentProtocolDefinition | T028，保原方法 V02 UpperIsolation：版本取 SemanticStageFixture.Origin.ComponentVersion，不取协议定义；B9 |
| SRC-02：同.TestPurposeDoesNotReplaceEachActualProducerWithSimulatedSource | T028，原方法 V02 UpperIsolation：Test用途下 Camera Virtual/Light Test/Algorithm Real DeclaredUnitAlgorithm/7、Host Derived/SoftwareLoopOnly；不按用途覆盖实际混合来源，不宣称真实设备；B9 |
| SRC-03：同.UnknownProducerCannotBeFilledFromPurposeOrHistoricalResultCategory(true,false,"Camera") | T028，V02独立相机行：ComponentEvidenceMatrixIncomplete/Camera，无WholeTrayCompleted/ObservedUnlocked；B9 |
| SRC-04：同.UnknownProducerCannotBeFilledFromPurposeOrHistoricalResultCategory(false,true,"Algorithm") | T028，V02独立算法行，历史Simulated不得补Unknown；具体Algorithm拒绝和无后继；不能与SRC-03去重；B9 |
| SRC-05a：IT/Station01/PublicPreparationHandoffV2IntegrationTests.DetectionCanOnlyStartFromCommittedMatchingV2Handoff：无已提交handoff/无当前Receipt | T028，V02 StorageComponent两拒绝分别定位CommittedHandoffV2Required/CurrentRecipeApplicationReceiptRequired（旧测试定位名；011按RC05.1迁移当前软件回执断言）；已删prebuilt占位特权只核实。合法typed正例共享M02原方法；B3/B7/B8 |
| SRC-05b：同方法当前真实轮正例 | T029扩T022 V05 FullRun：Run/Tray/Plan/Operation、3D/F媒体、窗内Receipt，RequiredCommits含handoff WriteId/revision；不另跑原整盘；B3/B8 |
| SRC-05c：同方法固定Simulated、SourcePolicy==Handoff.Source断言 | T029替换为V05已提交F实际Origin.Source/CallId/提交引用；请求用途、ApprovalScope分别核，不以WorkerSession/ExpectedVersion当来源；B7/B9/B10 |
| SRC-06：IT/Station01/Q01CameraMediaTests.FrozenTestManifestSelectsDistinctReadableAAndBMedia | T030迁素材边界；V03 AB同一次PortComponent逐CaptureId/相机核预先已知各SHA/可读/两触发，不仅文件存在或摘要不同；B6/B10 |
| SRC-07a：CT/Acquisition/CaptureCoordinationTests.EndedAndMediaTakenAreIndependentAndFirstOwnedBufferWins | T028 V02 Component，首[1,2]后[9]仍首owned buffer且等Ended；B10 |
| SRC-07b：同.FrameWithoutEndedAndEndedWithoutContentCannotCrossCompletionGate：frame-only / ended-only | T028 V02两独立缺半完成分别Take拒；B10 |
| SRC-07c：同.CapturePortRejectsMissingCommittedIntentBeforeSchedulingCallback | T030替原弱条件断言，V03 AB同正式capture实例缺IntentWriteId请求，实际拒绝/触发不增/无回调与事实；PortComponent，不多跑整盘；B3/B10 |
| SRC-07d：CT/Acquisition/MessageContractTests.RequiresEndedAndMediaAndRejectsCrossCaptureAssociation | T028 V02 Component：双完成/跨Capture拒/同request与epoch；NF-CAP扩新事实行；B10 |
| SRC-08：IT/Api/CommittedResultProjectionTests.CommittedImageIsVisibleBeforeObjectAggregateWithoutInventingObjectQuality：OK / NG / Pending | T029 V02 Component三行，图质量/CallId/MediaId与对象Disposition=null、NotProduced、Completeness=Unknown、RequestedCapture；无ActualSettings不填请求值、不造置信度/缺陷；B11 |
| SRC-09：IT/Storage/ComponentSourceMatrixStoreTests.MixedSourcesRoundTripAsSoftwareLoopOnlyAndNeverCollapseToReal | T029 V02 StorageComponent，SQLite混合PLC Virtual/算法Simulated往返、WholeTray HostDerived/Derived；B9 |
| SRC-10：同.MissingUnknownOrUnverifiableRequiredComponentBlocksCompletion：Missing / Unknown / Unverifiable | T029 V02 StorageComponent三行分别WholeTraySourceMatrixIncomplete:Algorithm，WholeTrayCompletions/Matrices均空且Run仍UnloadPreparation；不是SRC-03/04的内存拒绝；B9 |
| SRC-11a：同.FinalMatrixKeepsReadyEvidenceImmutableAndAddsAuthenticatedHumanActor | T029 V02 StorageComponent，Ready不可回写、Final新增AuthenticatedHuman，两矩阵/引用/scope及人工Real/Measured、汇总Derived；不冒充全事务故障；B9 |
| SRC-11b：同.TestManualActorRetainsActualOriginWhileFinalIsHostDerived | T029共享V05 Test客户端最终读取，ManualActor Test、Final HostDerived/Derived/SoftwareLoopOnly；若改非Test渠道，在B前恢复原方法V02必需，不能丢义务；B9 |
| SRC-12：IT/Api/Station01SourceMatrixApiTests.EvidenceEndpointReturnsComponentSourcesWithoutCollapsingMixedEvidenceToReal | T029共享V05待取盘GET evidence的200/ETag/source原枚举/completionId/scope/blocked/组件/Final空/AwaitingFinalUnloadCompletion及WholeTray计划/HostDerived/RecordNature/Quality；去启动后直写三阶段完成准备；B9/B11 |
| SRC-13a：IT/Station01/FinalUnloadCompletionIntegrationTests.AuthenticatedManualConfirmationIsTheOnlyStepThatCreatesFinalUnloadCompletion：授权/时序/重放 | T029 V02 API Component确定状态核提前409（非竞速）；V05共享确认前无Final、确认后Completed、重放Accepted/replay=true；B4/B9 |
| SRC-13b：同方法最终来源与持久引用 | T029共享V05 FullRun：6组件、blocked空/SoftwareLoopOnly/ManualActor Test、tray/plan/epoch、三完成引用、人工/Final各1、矩阵2、Run/Terminal Completed；与SRC-11b同读取；认证不证明真实人工移盘；B4/B9 |
| SRC-14：IT/Devices/SingleFaceDetectionIntegrationTests.RunAsync；Wire.AbThenCdExecuteThroughVirtualPlcWorkerMediaAndSqlite | T030 V03 AB+T022 V05 CD：ResultSource.Test、算法实际Test/版本、Camera Test、Light ConfiguredOnly；不永久固定PythonWorkerAdapter/1，E可真实为ContentSampleWorker/1；B6/B9/B10 |

NF 的计划新增方法如下，给出类文件/方法以便清单落定；既有方法扩展不再另建同义用例。T037 固定 caseId 为义务名，dataRowId 使用下列显式值，命名调整须同步 T034/T037 且在 B 前完成。

| 义务 / 目标方法（新增标明） | 主要任务 / 数据行、层级、实际执行 |
| --- | --- |
| NF-CAP / CT/Acquisition/CaptureCoordinationTests.CaptureFactsMustMatchCurrentRequestEpochAndSettingsDigest（新增） | T028：current / crossCapture / staleEpoch；V02 Component分别通过/拒且不写当前事实；V03 AB同轮读取正式返回及提交承接生产者，B3/B10 |
| NF-CAP-SOURCE / CT/Acquisition/CaptureCoordinationTests.UnknownCaptureSourceCannotBeFilledFromFixture（新增） | T028：unknown-source，V02 Unknown不从Simulation.Fixtures.MediaSource补、ActualSettings缺失不造；T030 V03固定图ConfiguredOnly/重放正例，无SDK已应用；B9/B10 |
| NF-F-ORIGIN / CT/Station01/FScanStepTests.FUsesOneTriggerOneImageAndDoesNotPerformRecipeOperation（扩展） | T028：actual-origin-differs，Origin故意不同ExpectedComponentVersion/WorkerSession、关联Run/Capture/Call，保单触发/单调用/F不预绑定；V02传播＋V05 SRC-05c实际提交消费；B1/B3/B10 |
| NF-F-REJECT / CT/Station01/PublicPreparationHandoffV2Tests.FSourceFactMustBeCommittedAndMatchCurrentCallForHandoff（新增） | T028：missing-or-uncommitted / wrongCall / unknownOrigin三行；前者已有内存Origin但无对应提交，均具体拒来源/关联/可信性及依赖检测；V02 Component，不由SRC-05a抵扣；B3/B9/B10 |
| NF-F-SAVE / CT/Station01/FScanStepTests.FailedAlgorithmFactSaveDoesNotSubmitFCompletion（扩展） | T028：origin-save-failed，V02必要Origin/算法事实保存失败无F完成、释放不丢；不再加整盘失败；B3/B4/B10 |
| NF-PROJECTION / IT/Api/CommittedResultProjectionTests.CommittedActualCaptureSettingsRemainDistinctFromRequestedSettings（新增） | T029：actual-differs-from-requested，V02一个额外组件样本分别显示已提交实际和请求；无actual三质量行已在SRC-08，不扩笛卡尔积；B11 |

同 fixture 的其余受影响义务也必须登记，不能以点名 SRC 覆盖率代替完整性：

| 原方法 | 主要任务 / 精确最小承接 |
| --- | --- |
| CT/Workflow/WholeTrayWorkflowOrchestratorTests.MainFlowPersistsWholeTrayUnlockAndManualCompletionInOrder | T029共享V05同Run事件/回执三阶段→ReadyForUnlock→ObservedUnlocked→授权→Final；原UpperIsolation可留，不额外强制正常行 |
| 同.UnlockCannotBypassPersistedWholeTrayEvidence；UnknownUnlockFeedbackDoesNotCreateObservedUnlocked | T028 V02 UpperIsolation分别无WholeTray拒ObserveUnlock且PLC Calls空；UnknownHeld无ObservedUnlocked，不与来源Unknown合并 |
| 同.ProductionSourceCannotUseSimulatedStageEvidence | T022迁V01未批准生产/来源不足拒绝，具体原因/零依赖动作；删SourcePolicy="Production"旧构造，不用任意形状异常代替 |
| IT/Api/CommittedResultProjectionTests.QualityAndRequiredTargetsRemainDistinctFromFlowFinal；LegacyUnassociatedPayloadDoesNotBecomeCurrentObject | T029：若改共用投影构建/身份逻辑，V02两原方法必需；只改采集参数且证明未影响则保留并登记理由，不整类跑 |
| 同.EvidenceProjectionPreservesCommittedActualReadbackInsteadOfCopyingTarget；MotionTargetHasFiniteBusinessFieldsAndReadsCurrentSemanticPosition | T029：改MotionFacts投影/共用位置字段才纳V02两原方法；009隔离/实际不由目标伪造保持 |
| Final事务边界 | T019若实际改变边界，T029在B前登记一个必要保存失败代表；原边界不变则SRC-10三行及既有保存保护足够，不扩全故障矩阵 |

## D01—D11 清理的唯一主要交付

每项删除前由 T001 初查、对应任务当时复查、T034 收口核正式调用/DI/配置/脚本、动态及活动 FQN/清单和历史 reader。不能只凭文本未命中或本轮没跑某路线下结论。有效义务先迁、活动映射先更新，再实际删；T044/T048 核承接执行和最终不存在兼容旁路。若出现新的有效消费者，先明确同范围承接及依赖；未解决不能把删除项标完成。

| D | 具体文件/符号及既定决定 | 主要删除/保留任务与验收 |
| --- | --- | --- |
| D01 | backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs：ExecutePostFlipComponentAsync、TestPostFlipStageContext、RescanWholeTrayAsync | T031 删除；当前有效多面执行由T018承接，历史数据类型/reader不随删 |
| D02 | 同文件 componentHeightRound 专属复扫/历史组件调用路径 | T031 删除；保初始测量/合法多面heightRound身份及聚合 |
| D03 | PublicPreparationHandoffV2.cs、StagePortContracts.cs、IntegratedDetectionPort.cs及其消费：activeStrictRecipeExecution、legacyExpected、frozen-plan-0/占位零坐标、nonStrictPending/跳过分支 | T008形状＋T016主要删除，T017移业务、T031清余留；保context/1.0和原期限；T010/T027/M02核两输入都走共同校验 |
| D04 | Simulation/SimulatedDetectionPort.cs：SimulatedDetectionPort/Profile；Integrations/NotIntegratedStagePorts.cs：NotIntegratedDetectionPort；CT/Simulation/DetectionAdapterSourceTests.cs三方法 | T021取消正式装配，T032实际删类型/无效测试；同文件其他合法端口保留，拒绝/身份义务承接M03 |
| D05 | Station01RuntimeOptions.cs/Program.cs/AdapterBindings.cs、scripts/verify-latest-plc.py：DetectionTestMode 参数/分支/旧脚本输入 | T032实际删除，正式唯一执行T021；质量/未知等脚本义务由M03承接，不能保备用开关 |
| D06 | Application/Capabilities/TestTrayCodePolicy.cs及CT/Capabilities/TestTrayCodePolicyTests.cs | T013迁两码边界义务后删旧类/空测试文件，保共同FCodePolicy唯一绑定与原码事实 |
| D07 | JsonRecipeCatalog/RecipeRunPlanner/PublicPreparationHandoffV2/IntegratedDetectionPort/ThreeStageWorkflowExecutor中的ProfilePayloads、PositionPayloads、fixture解析、TestEligibleSlots及测试能力缺省 | T011文件解析、T012共同目标、T013绑定、T017检测、T019分拣各自主要删除；T034核不存在另一raw JSON/双加载校验旁路 |
| D08 | FileBackedCapture/SimulatedCapture/CameraCaptureAdapter、检测/StageHandoffBuilder/查询中的Unknown→fixture、固定Test来源、physicalSdkApplied假事实 | T014采集、T015实际F、T016移交、T019来源矩阵、T020查询；保存旧记录，不回填历史事实；SRC/NF与V03/V05核真实来源 |
| D09 | backend/src/Gaode.Host/Api/RunMediaCatalog.cs、Infrastructure/Persistence/DeviceEvidenceHistoryReader.cs及Rescan历史枚举/摘要 | 明确保留；T020维护读取，T029历史代表，T034核历史消费者，不恢复旧动作入口 |
| D10 | IT/Station01/VirtualRecipeAndDetectionGateTests.Current007AmbiguousDetectionMappingBlocksBeforeSorting；scripts/workflow/009-required-cases.json；Rules/Architecture/009-test-obligations.json | T033先承接独立断言/更新活动映射再删方法；T034核动态脚本/清单无断链，旧报告及源快照不删 |
| D11 | IT/CommunicationFixtures/*.Wire.cs、固定图片适配、SDK未接入明确拒绝、IndependentRecipeApplication/历史读取 | 明确保留有效通信/输入/准入/独立绑定职责；T018/T020/T030/T034核消费者和范围；业务helper仍纳L，不把Wire目录当免责目录 |

## L、B、冻结、E/S、T 的执行合同

T035—T042 是验证设施的实现任务；T044—T048 是其实际执行与证据检查点。待入口实现后，统一命令为：

    ./scripts/verify.ps1 -Profile RecipeExecution010

该参数目前是计划能力，不宣称现脚本已可运行。T044 启动这一轮统一验收；T042 的 runner 自动依次生成 B、冻结、E、S、V07 证据。T045—T048 是同轮后续检查点的责任任务，不要求为每个任务重复启动完整链，也不引入人工“跳过阶段”参数。T043 准备审阅在调用前完成；执行中任一步不成立则停止对应后继、总失败。任务勾选须核各自实际产物。

| 集合 / 主要任务 | 固定义务及通过条件 |
| --- | --- |
| L / T035—T041、T044 | 每个验收profile无条件运行：B01—B05、N01a/b/c/N02/N03a/b/N04/N05a/b、P01—P03静态样本、G01—G07、C01—C03及受影响009通信静态边界；构建Rules及必要依赖，不开Host/PLC/Worker/DB。010 V01直接引用同轮L，不重复同样检查。L通过不代表动态共同执行 |
| B / T044 | V00+V01+V02+V03+V05+V04除指定S的全部必要代表；普通双面自动换面、组mixed/四面组件、整体人工/E、旋转及独立数据项都在B。V05第一条正常完整链，CD正常组件共享它 |
| 冻结 / T045 | B全部通过后才冻结共同业务实际职责/间接helpers/相关合同/预算期限/先已审阅oracle/manifest/Compile链接及内容摘要；替代实现与输入、S独立预期已在B前备好，不能从B结果倒填 |
| E / T046 | 素材实际内容、ContentSampleWorker/1、坐标提供、配置/F解析与配方标识、正式绑定五类组合等价替换；第二条正常完整链。第二实现实际输入处理、身份/结果/释放/提交均可核；启动参数/名称/摘要/包装变化不足 |
| S / T047 | 冻结后一次V04-V06-AssemblyNgPending共同组件执行，承接整体NG优先/Pending细节/实体一次搬运与保存；输入先备、执行不属于B前置。此清单串行E再S以免争用设备/数据库，不宣称并行 |
| T / T048 | B∪E∪S∪V07按verificationAttemptId/caseId/dataRowId/runId及obligation映射去重，保每原子attempt真实source/build/input/manifest/时间，不改写为同轮新身份。V07收口本身不以前置“T已通过”为条件 |

V00 仅构建受影响 backend/src/Gaode.Host/Gaode.Host.csproj 及 Domain/Application/Infrastructure/Plc.Protocol 依赖、backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj、Gaode.Rules.Tests/Gaode.Rules.Tests.csproj、Gaode.Integration.Tests/Gaode.Integration.Tests.csproj，以及主链所需 VirtualPlc/VirtualPlc.csproj；Worker输入/自核按清单。Gaode.Communication.Tests.csproj 只有引用/代码受影响才构建，未受影响不全跑。构建身份关联当前源码及 SDK，不默认全solution测试。

V02 为 M01/M02/M03/SRC/NF 与第二实现自核的最小集合；V03 AB/四失败/未知F/非法算法及必要采集事实；V04 如上并按M04保所有受影响分支，未改时间/投影条件项由T034注明独立承接理由。所有必需日志须能关联命令、阶段、对象/动作/Call、阻断/期限/失败和必要保存。失败实际发生时保留记录，不能调整预期/预算使其变绿。

冻结之后不安排共同业务/合同/oracle改造。若这些或必需集合变化，立即废止旧E/S，回到对应实现/影响登记，重建受影响B并形成新的完整B身份和冻结，再执行新的E/S/T；仅明确未受影响B可按VG-04保原case/run/摘要和完整依赖不变的独立审阅证明引用。当前L、实际修改义务、失效E/S及旧007/008/009报告不能冒充本轮结果。

### L 的持续调用和拒绝点

| 入口/最终消费者 | 待实现责任（T040） / 验证（T041） |
| --- | --- |
| verify.ps1→verify_entry.py→runner.run_verify/_run_verify | 所有profile先L再自身集合；_run_verify总passed与verify_entry PASS/FAIL退出核本轮真实凭证，不能靠profile名/过滤/目录/Passed=true绕过 |
| workflows/auto-dev.yml→step.ps1→runner.step('verify') | 同入口；assess及finish重核control当前attempt原ledger/TRX/摘要，缺L或旧L拒assessment/完成写入；普通进程0退出不是通过 |
| verify-009-protocol-isolation.ps1→boundary_minimum.main | 写ledger/result及返回成功前同L核验，默认/指定case/fixture均不可绕过 |
| 同→protocol_isolation.main | component/subsetPassed/成功返回前同核验；SelectedCasesOnly保持overall009Passed=false，只证明原子集 |
| 合法复用与单独调试 | 旁接若由runner委托，只复用同父attempt/profile/摘要下已核L；单独dotnet test/脚本为Debug/Partial，不产生项目验收通过 |

C01 固定默认009、非010、BoundaryMinimum、SelectedCasesOnly四行，违规N02/N04即使自身结果全Passed仍拒；C02核遗漏L、旧attempt/源/manifest、旧case行伪装当前、伪造Passed在全部最终点拒；C03核当前完整及VG-04合法保原身份引用可通过。与G数据样本用同一判定函数，不建第二检查器，不启动009动态全集。

## 追溯覆盖与主要验收任务

同一主要交付可承接多个要求；以下不是按编号新增任务。T043/T048覆盖核对不能代替表内实际实现和运行。P编号在每任务表中；B1—B11的有效依据见VG-06，来源文件和历史证据不修改。

| FR | 主要交付任务 | 对应AC / SC | 实际验证 |
| --- | --- | --- | --- |
| FR-001 | T008/T009/T011/T017/T021 | AC-01/04；SC-001/003 | T044 V01/V02/V05；T046 |
| FR-002 | T016/T017/T019/T021/T032 | AC-01/10；SC-001/003 | T044 V01/V04/V05/L |
| FR-003 | T008/T011/T012/T017/T024/T035 | AC-04/06/10；SC-002/003 | T044 V02/L；T046 |
| FR-004 | T009/T010/T012/T018/T019/T030 | AC-05/06；SC-006/008 | T044 V02/V04；T047 |
| FR-005 | T013/T015/T016/T028 | AC-01/02；SC-001/008 | T044 F组件/未知F/V05 |
| FR-006 | T013/T017/T021/T022/T024/T032 | AC-01/02/04/10；SC-001/002/003 | T044 V01/V03/V05；T046 |
| FR-007 | T008/T014/T015/T016/T019/T020/T028/T029 | AC-02/03/06/11；SC-008 | T044 SRC/NF/正式来源；T046 |
| FR-008 | T021/T022/T029 | AC-01/03；SC-001/008 | T044 V05最终授权/查询；T046 |
| FR-009 | T009/T010/T014—T019/T027—T030 | AC-03/05/06；SC-006/008 | T044 V02/V03/V04/V05；T047 |
| FR-010 | T014—T019/T021/T022/T030 | AC-01/03/04；SC-001/008 | T044实际失败/主链持久关联日志；T046 |
| FR-011 | T001/T010/T027—T034/T037 | AC-07；SC-005 | T044各精确行；T048迁移闭合 |
| FR-012 | T022/T025/T027—T030/T037/T038 | AC-04/05/07/09/12；SC-005/006/007 | T044/T046/T047层级/独立预期；T048 |
| FR-013 | T035/T038/T040/T041 | AC-10；SC-003/004 | T044 L B/N/C；T045/T046重枚举 |
| FR-014 | T036/T041 | AC-10/11；SC-004 | T044同检查器N/P与调用样本 |
| FR-015 | T037—T042 | AC-12；SC-007 | T044 G/C；T048当前T最终拒绝 |
| FR-016 | T001/T013/T016/T020/T031—T034 | AC-08；SC-005 | T044承接/历史；T048实际删除核对 |
| FR-017 | T023—T026/T038/T042 | AC-04/05/06；SC-002 | T045冻结/T046 E/T047 S/T048差异 |
| FR-018 | T001/T030/T034/T037/T042 | AC-09；SC-006 | T043锁最小集；T044/T048保原范围/旧失败 |

| AC | 主要验收及不可替代义务 |
| --- | --- |
| AC-01 | T044 V05正式单配方至最终保存/授权；T046同入口替换 |
| AC-02 | T044 V01缺能力/未批生产、V02合法性、V03未知F/非法算法；不测试值兜底 |
| AC-03 | T044四实际失败、回执/保存/未知/取消独立行与日志 |
| AC-04 | T045/T046共同源冻结0改动、五类实质替换、动作判断保存等价 |
| AC-05 | T047 S整体NG/Pending一次共同组件，T048映射V04/V06不重复 |
| AC-06 | T044独立坐标/测量/错关联拒绝，T046第二来源同共同resolver |
| AC-07 | T034映射+T044当前执行+T048100%有效义务，无证据越级 |
| AC-08 | T013/T016/T031—T034实际删除/保留，T048核D01—D11及历史 |
| AC-09 | T034/T043实际影响定最小集，T048保旧失败/未验证与路线限制 |
| AC-10 | T036五类错误/全部子项+T044 L同判定器，移动改类不逃逸 |
| AC-11 | T036三合法正例+T044 L实际允许，Unknown不伪可信 |
| AC-12 | T039/T041全部G/C缺失/漏跑/旧凭证拒绝+T044/T048当前完整允许 |

| SC | 主要交付与可衡量证据 |
| --- | --- |
| SC-001 | T021/T022→T044：至少一条正式独立完整单配方，同Run至Final，组件实际参与 |
| SC-002 | T023—T026/T045—T047：五类替换真实、共同业务源内容变化0，E等价/S按独立规则变化 |
| SC-003 | T008—T021/T031/T032/T035→T044/T046：职责闭包/正式装配无禁止耦合或旁路 |
| SC-004 | T036→T044：五类负例及三类正例全部实际按预期接受/拒绝 |
| SC-005 | T001/T010/T027—T034→T048：100%有效迁移义务与确认删除可追，保历史有效消费者 |
| SC-006 | T030/T037→T044/T047/T048：受影响工程构建、各必要分支最小实际证据，无单面抵全部 |
| SC-007 | T037—T042→T044/T048：100%必需发现/执行/有效证据，G/C错误输入总拒，持续L不可漏 |
| SC-008 | T014—T020/T028/T029→T044/T046/T048：实际来源/身份/反馈/必要保存/授权及原期限保持，生产局部限制和009边界不削弱 |

## 真实依赖、并行条件与实施切片

各任务明细的直接前置为权威图，不以US编号假定独立。共同模型/正式链先就绪，US2准备和US3迁移才能闭合，US4规则可提前开发而强制接线必须先于B；US1验收也依赖US3/US4，不能先宣称US1运行已完成再补门禁。

    T001 → T002 → T003 → T004 → T005 → T006 → T007 → T008 → T009
      → T010 → … → T022
      → (T023 ∥ T024) → T025 → T026
    T022 → T027 → T028
    T026 + T028 → T029
    T026 + T029 → T030 → T031 → T032 → T033 → T034
    T009 → T035 → T036
    T025 + T026 + T034 + T035 → T037
    T036 + T037 → T038 → T039 → T040 → T041 → T042 → T043
      → T044(B) → T045(冻结) → T046(E) → T047(S) → T048(V07/T)

| 用户故事/并行组 | 成立条件、互不冲突的写入与限制 |
| --- | --- |
| US1 | 不标[P]；共享公共模型、handoff、F、检测与Host，T010—T022串行。其V01/V05可独立观察业务结果，但验收前置是真实跨故事依赖 |
| US2 T023 ∥ T024 | T022及其全部前置完成；T023只写scripts/010-content-sample-worker.py，T024只写新提供者和Host绑定；双方按已对齐IB-03/R10接口工作。T025等待两者，T026随后；若需要改共享形状则停止并行，先核合同/依赖 |
| US3 | 不标[P]；来源/主链harness/迁移和活动009映射共享文件，T027—T034按序；T029显式等待T026以串行写harness，T030也等待T026 |
| US4 T036 ∥ T037 | T035完成且T025/T026/T034亦完成；T036只写规则样本.cs/脚本样本JSON，T037只写两个manifest；前者不改检查器、后者不回写迁移registry。待T038联合核名字/数据身份 |
| 最终收口 | 不并行；共享工作区锁/设备/数据库/冻结身份，由统一runner串行推进。S不在B，冻结后无新共同代码任务 |

**最小实施切片是进度里程碑**：T001—T022形成“共同语义输入→唯一业务→正式装配/主链harness”的可审阅改动；这只表示第一个实现切片，尚无完整验收结论。最小运行里程碑是所有输入/迁移/清理/L接线完成后的T044修复基线；010完成还必须T045—T048以及全部有效义务。US2替换、US3义务/删除和US4门禁均不可作为可选后续。

## 剩余实施前置、范围与静态检查记录

| 依赖/限制 | 来源及只限制范围 | 待完成任务 / 可继续部分 |
| --- | --- | --- |
| A01—A09待实际对齐 | plan共享合同影响表、AGENTS共享接口文档先行 | T002—T007先完成；本轮只列任务，未改任何其他文档或接口 |
| 当前正式代码/测试/门禁仍待修复 | research、VG-06、architecture文档通过≠实现 | 全部48项未完成；无需重做规格或设计，进入实施前先analyze |
| 第二实现/代表输入/独立预期尚未创建验证 | R10、IB-03、VG-07.1 | T023—T026在B前；不得冻结后改业务迁就提供者 |
| DEP-02生产预算/坐标/标定/能力或协议缺批准 | spec/plan现限制 | 只阻对应生产动作；010批准模拟路径可继续，不编造语义或Test兜底 |
| 007/008/009已有失败、未运行、转出项 | 原历史报告及VG-08 | T001留原状态、T034/T048按具体依赖判010是否阻断，不自动重开全集，不篡改旧报告 |
| 删除候选复核出现有效动态/历史消费者 | FR-016、D表 | 对应删除任务须先承接；不能把未查或未覆盖判无用，不保错误备用旁路 |
| 前端/原型 | F006现API消费，AGENTS/P12 | T020只核后端投影和既有读取；客户ZIP/HTML只读，无新页面/原型/无关数据库升级 |

本轮文档静态检查口径：48个连续唯一ID、全部未勾选；US1=13、US2=4、US3=8、US4=8，共享前置=9、最终收口=6；每项有路径、直接前置、完成判据/证据和需求/原则追溯。18 FR、12 AC、8 SC、A01—A09、D01—D11、VG-06 M/SRC/NF和B/N/P/G/C均由实际交付及验收任务承接。检查依赖无缺失/循环、共享代码均有对齐前置、T043前输入/必需清单与L接线齐备、B→冻结→E/S→T无循环或同义强制重跑、仅两组[P]有明确文件隔离。

此记录仅指任务文档检查，不宣称源代码检查器已通过、测试已迁移、删除已完成或运行证据存在。后续实际影响条件须在B前落到固定清单并真实执行；出现与既有设计不能协调的实质矛盾应记录具体条款和最小修订，不静默删义务。当前任务拆解未发现需要重开设计的实质矛盾。

静态复核实测结果：54条直接依赖无缺失ID/循环，计划新增文件的后续引用均有创建任务前置；两组[P]无相互依赖或写入文件重叠。按VG-06文字与现源码方法声明交叉核对，115个被引用的现有测试方法均已纳入本文件索引，独立数据项另按上述表逐项承接。spec、七份设计、两份checklist、feature.json、AGENTS.md及宪章共13份受保护文件SHA256与本轮读取前一致；前后tasks钩子均为空。这里只进行了文档解析和只读文件核对，没有构建或运行项目测试。

本轮到此停止；下一步建议执行 speckit-analyze，本文件不触发该阶段或 implement。

## 实施记录（2026-10-02，持续追加）

- T001：当前消费者、115个原方法及数据/断言、D01—D11、动态引用和历史限制已登记。证据：`artifacts/recipe-execution-010/20261002T072445Z-implementation/preparation/dependency-audit.json`、`source-before.json`；活动登记：`backend/tests/Gaode.Rules.Tests/Architecture/010-test-obligations.json`。本次专用证据均放在该轮目录，后续验证使用各自真实attempt/run子目录。登记不代表迁移或运行完成。

- T002：A01/A07 所属spec/contracts/plan/tasks完成定向条款及消费者对齐；010已一致条款只核查。证据见本轮preparation/contract-alignment.json；旧任务勾选不变，消费者代码待对应实现任务。

- T003：A02/A05 所属spec/contracts/plan/tasks完成定向条款及消费者对齐；010已一致条款只核查。证据见本轮preparation/contract-alignment.json；旧任务勾选不变，消费者代码待对应实现任务。

- T004：A03/A04 所属spec/contracts/plan/tasks完成定向条款及消费者对齐；010已一致条款只核查。证据见本轮preparation/contract-alignment.json；旧任务勾选不变，消费者代码待对应实现任务。

- T005：A06 所属spec/contracts/plan/tasks完成定向条款及消费者对齐；010已一致条款只核查。证据见本轮preparation/contract-alignment.json；旧任务勾选不变，消费者代码待对应实现任务。

- T006：A08 所属spec/contracts/plan/tasks完成定向条款及消费者对齐；010已一致条款只核查。证据见本轮preparation/contract-alignment.json；旧任务勾选不变，消费者代码待对应实现任务。

- T007：A09 所属spec/contracts/plan/tasks完成定向条款及消费者对齐；010已一致条款只核查。证据见本轮preparation/contract-alignment.json；旧任务勾选不变，消费者代码待对应实现任务。

- T008—T013：共同语义模型、唯一校验/规划与批准边界、文件解码、坐标共同计算和 F 解码/能力绑定已迁移。原两码断言迁至 RecipeEnvironmentDecoderTests，原业务 TestTrayCodePolicy 及旧测试文件已删除；历史源索引保留。开发证据：preparation/host-build-01.log（Host 及依赖 0 错误）、recipes-dev-03.trx（53 项配方断言通过；同报告五项期限测试因证据目录未配置失败，保留原结果）、deadlines-dev-01.trx（配置本轮目录后16项期限测试通过）。修复过程中 recipes-dev-01 保留摘要递归崩溃记录，随后已修复。这里仅完成对应改动与开发定向验证，最终 B/L 尚未运行。

- T014—T022：实际采集事实和当前 F Origin 保存/消费、共同检测与适用处置、冻结移交、查询和唯一装配已交付；合法 context/1.0 与原期限仍保留，整段模式残留类型/选项待 T032 清理。正式独立进程 harness 仅调用既有 API，已编译，完整运行待 T044/T046。开发证据：preparation/handoff-dev-02.trx（17项）、query-source-dev-02.trx（18项）、composition-dev-01.trx（两组实际装配）、admission-dev-01.trx（缺能力及无移交/无回执拒绝）。这些是开发局部证据，不计最终 B/L；工艺分支最小实际回归仍由 T030/T044 承接。

- T023—T026：ContentSampleWorker/1 已实现 PNG 内容测量/F/单图/融合计算，Host 启动核 Ready 的真实 PID、脚本/配置摘要及实现身份；第二目录/CSV坐标/F映射共用 validator/resolver。B/E/S 输入、五类替换表和独立算术预期位于 Integration/Fixtures/RecipeExecution010；完整链断言由 RecipeExecution010Expectations 读取真实提交核对。开发证据：semantic-input-dev-01.trx（独立输入通过）、content-worker-dev-02.log及对应目录（内容变化计算、四能力真实进程协议/逐输入释放通过）。首轮 selfcheck 的资源未关闭警告保留，现已关闭流并给读取加有限等待。这里只完成提供者、输入及验证编制；没有执行 B/E/S 或冻结。

- T027—T029：上层替身层级、三种持久拒绝、三入口原期限、SRC/NF 来源与查询义务已迁移；正常确认/重放/最终来源由 V05 harness 承接，提前409改为确定状态的 API 组件。独立绑定代表只核其绑定/原移交不变及无下游授权，取消本组件后结束，不要求它继续主链。开发证据：migration-dev-01.trx（56项）、upper-dev-03.trx（错映射及查询2项）、upper-dev-05.trx（Pending/未知占用和v1/v2通过；独立行失败保留）、entry-dev-07.trx（独立入口1项通过），另见前述来源存储与投影18项。发现并修复实际F媒体类型与角色混淆、Pending丢采集事实；未知来源仍拒绝。当前只完成迁移交付及局部验证，最终 B/SRC/V05 必需执行仍待 T044。

- T035/T036：在009同一Compile/链接源语义模型上建立010职责闭包、公共形状、环境分支及正式DI检查，输入/叶端口和只读投影有明确停止点并核不得接管工序；跨引用编译的移动helper及改Role仍拒绝。脚本复用009解析，拒直接续接/写完成。活动inventory/public-shapes按A01—A09实际语义新增，旧协议保护未放宽。开发证据：preparation/architecture-dev-07.trx（21项通过，含全部9负例/3正例、当前5规则及009四项）；script-boundary-dev-01.json（160文件、6样例通过）。早期解析/登记失败报告保留。此处完成检查器和样例，不表示L接线/凭证或正式B已完成。

- T030：受影响分支已编制为实际共同执行组件；补普通双面自动换面代表，整体组件保留部位/面查询及E媒体身份，S仍仅冻结后执行。integration-compile-05.log为编译证据（04完整构建因正在运行的测试锁住输出DLL失败，保留记录）。branch开发批次仍在执行，完整必要集合的运行通过判定归T044，当前不声称B通过。

- T031：D01/D02当前调用、装配、配置及脚本复核完成；旧Integrated文件及历史组件入口已移除，本次删除剩余可筛选heightRound的可选参数。多面动作由共同执行器承接；Rescan历史类型和reader保留。dependency-audit.json追加当前消费者结论，运行验证仍归B。

- T032：D04/D05实际删除整段Simulated/NotIntegrated检测类型、旧三测试及DetectionTestMode参数/脚本分支；保合法PLC未接入端口。旧驱动改为007批准配置、当前单面配方、真实Worker媒体及迟到/非法叶结果，来源来自已保存矩阵；自身仅Debug范围。生产拒绝/缺对象迁至FormalHostCompositionTests，取消/未知端口保护保留。新旧接口整批编译/必要行最终仍由B核验，未执行旧脚本整组。

- T033：先为wrong-mapping行补Terminal None、关联事件/零分拣证据，更新009活动manifest及两处迁移目标，再删除Current007重复方法和其专用替身。原009注册source/dataRows/断言快照保留。新证据如实标UpperIsolation，不能计共同主链。

- T034：115原方法/155原数据行逐项映射到171个当前case/dataRow，另登记21项SRC和NF/三入口/普通自动换面新义务；原注册摘要未变。源迁移审计02通过（01缺条件项依据/一个保留路径错误的失败保留）。D01—08/D10实际清理，D09/D11保留消费者明确；弱采集条件及重复Q01适配测试已迁实际AB后删除。执行状态保持RequiredCurrentExecution，不能把此源审计当B/T。

- T037—T039：固定L 64项、B 174项/E 1项/S 1项（V07聚合不以前置自通过为条件）；115方法/155原数据行与当前manifest一致。当前凭证复核原始TRX、发现/数据行、源码/构建/清单/输入/检查器及报告摘要，额外解析错误亦拒。ledger-samples-dev-01.json的36项G/C数据样本通过，均为SyntheticVerifierComponent；入口接线及消费者验证尚待T040/T041，当前不表示L或B验收通过。

- T040—T042：所有profile及009旁接总判定、workflow assess/finish已接入当前L原始凭证核验；010统一编排自动执行B→冻结→E→S→V07，无跳过开关。开发L实际64项通过（L-dev-01.log及相邻L目录）；G/C 36样本含真实入口调用与阶段失败阻断通过（ledger-samples-dev-05.json）。现有runner协调19项通过，一项009活动迁移摘要失配失败保留，按010实际承接更新当前映射后该项通过（runner-wiring-dev-02.log）；193/245原009注册不变，未重开009动态全集。

- T043：A对齐、D实际删除/保留、115方法/155原数据行映射、169当前迁移目标及B/E/S输入/第二实现/独立预期和固定清单已复核，见preparation/T043-preflight.json。最近8项分支修复回归通过；早期失败仍保留。下一步首次正式B，当前无B/冻结/E/S/T通过结论。

- T044进行中：acceptance-01因补齐SRC05及融合独立断言后源码变化，被L拒绝且未进入B；acceptance-02在V02的3项持久协调组件因runner证据目录位于批准根同级而拒绝，已改成批准根子目录，未放宽保护；acceptance-03正式完整链通过、端口8/9通过，迟到Worker fixture因266字符Windows路径读不到真实媒体，Height即退出，未达预期Detection。已沿用现Worker的长路径文件API写法，保根/长度/SHA校验，late-worker-repair-dev-01独立重验通过。所有旧TRX/源码身份仍保留。当前acceptance-04（standalone-d262992e32254726a3231142cfeed2f2-verify-1）L64、构建、Worker自核、V01 2/V02 118+24/V05 1/V03 9均通过，V04 14项仍运行；B/冻结/E/S/T尚未判通过，T044—T048不勾选。

- T044/T045：acceptance-04 的 B/ledger.json 当前174/174通过（V04 14项49m13s，全批无Skip），L64本轮有效。freeze.json于2026-10-02T12:06:36.914001Z建立，baselineAttemptId=961b956b5e424e7094561cfa2aafe3fc，包含实际业务职责闭包及源/构建/合同/预算/预期/输入/清单摘要；E启动在冻结之后。当前尚无E/S/T通过结论。

- T046：同一冻结下E完整链2m37s通过（E/ledger.json、E/full-run/obligations.json及worker-protocol.jsonl），与B使用不同真实RunId；ContentSampleWorker/1实际处理10份输入并产生8次同合同结果/10次释放，固定独立坐标/动作/判断/保存预期通过；5类替换输入均在冻结前准备，共同源内容变化0。S及最终T仍待核验。

- T047首次S失败：实际取/放反馈已到，MaterialTransferred保存因有界通信缓冲gap拒绝，UnknownHeld正确保留；证据为acceptance-04/S（实际12:13:37失败，原始DB/通信记录保留）。原B174/E1仍为其旧源码有效结果，不能当修复后结论；T044—T046当前重新打开。009对应spec/contract/plan/tasks先定向对齐已提交取料段的接续保存，不改业务预期、预算或轮询。修复后重建B及冻结，再重新E/S。

- S必要修复已交付：通信内部仅在真实取料raw提交后保留接续截点和引用，放料最终raw核同Action/Run/Epoch；保存失败/未知及原窗不变。sorting-repair-dev-01正常取放及3行raw保存拒绝通过；额外旧抬升Z行因未观察目标失败，未改断言/预算，独立sorting-z-review重验通过；原失败保留。sorting-repair-dev-02原S慢动作组件3m05s通过，两段实际SQLite证据无gap；这是开发局部验证，不是正式S。当前B增加4个直接受影响通信义务为178项，E1/S1；115/155原快照不变、当前173迁移目标；009活动迁移G07复核通过。准备下一轮统一完整验收，当前T044—T048仍未完成。

- acceptance-05的L架构21项/脚本均通过，但样本临时目录清理出现WinError145导致凭证缺失，门禁拒绝且未启动B。有限G/C样本改在当前L证据目录保留（无设备/库/子进程），不再以临时目录删除成败决定报告生成；ledger-retention-dev的36项正负样本全部通过。旧失败完整保留，下一轮仍重新运行当前L。

- T044/T045当前完成：acceptance-06（standalone-54fa1c4e72904772bd9c91cb27bc7d47-verify-1）L64、B178/178均通过，V04为14项49m16s无Skip；其中新增取放通信保存4项均通过。freeze.json于2026-10-02T13:40:11.084839+00:00建立，baselineAttemptId=435c4302aba84515a2cdb229a42c4c54，冻结1337份源/输入/合同/预算/清单与实际构建及职责闭包；当前内容复核一致。E已在新冻结后启动，旧acceptance-04的B/E及失败S仍原样保留，不作为本轮E/S凭证。

- T046当前完成：acceptance-06/E完整链2m43s通过，RunId=67f75399-0c6d-4b31-a04f-ae6daa8e5c08，实际ContentSampleWorker/1处理媒体并生成/释放同合同结果；B RunId=c9bf58a3-2a15-417b-bb0c-ad0a1ba6bcac。按freeze.json当前1337份内容及构建核无变化，5类替换与必要动作/判断/保存比较通过，共同源内容变化0。S在同一冻结下已启动，T047/T048仍待当前证据。

- T047当前完成：acceptance-06/S在同一13:40:11冻结下3m01s通过，无Skip；整体NG优先、Pending明细及实体一次取放/真实保存成立，一次执行共享V04/V06义务。delivery-source-review.json复核A01—A09、D01—D11、原115/155及当前173迁移目标和受保护文件；该文件只表示源审查，T048仍等待V07及最终入口判定/清理。

- T048完成：acceptance-06的V07、acceptance-result及统一verify_entry最终结果均Passed，T180/180（B178/E1/S1），当前L64有效，无Skip/缺失/解析错误/旧报告替代；115个原方法/155原数据行和173当前迁移目标已与实际账本交叉核验。A01—A09实际文档/消费者对齐、D01—D11实际删除/合法保留、15份受保护文件及源码/构建/冻结已复核。正常完整链只有本轮B和E两条，S一次共同组件共享V04/V06；其他分支为既定组件集合。51条本轮进程归属记录均已退出。旧失败、未验证事项和生产局部限制保留，不代表009整体验收/生产接入/全量或整机通过。最终交付索引：artifacts/recipe-execution-010/standalone-54fa1c4e72904772bd9c91cb27bc7d47-verify-1/result.json。

- 资源保留说明：acceptance-05失败样本的临时目录C:/Users/codexsandboxonline.10_3_0_13/AppData/Local/Temp/gaode-010-ledger-6skvken3删除被自动审批拒绝（blocked by policy），未绕过；8份残余样本已逐SHA核对归档于本轮retained-failed-selfcheck-temp，原目录原样保留。此为无设备/无生产数据的验证样本，不是漏执行的010业务或验证义务；不宣称该临时目录已删除。after_implement hooks={}，没有启动后续阶段。

## B02 定向修补（2026-10-03；不重排原48项）

- [x] T049 修复环境提前正常返回对后续工序的B02漏检，并完成专项L验证；文件：backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryChecker.cs、RecipeExecutionBoundaryTests.cs，scripts/workflow/010-lightweight-cases.json，contracts/verification.md及quickstart.md。前置：本Bugassessment.md与原T035—T042既有门禁设施。承接FR-013/014/015、AC-10环境控制工序、AC-11合法边界、AC-12执行完整性、SC-004/007、VG-02 B02/VG-03/VG-04及持续L，不新增业务能力。

完成判据：先证明原检查器两条有效绑定样本未报B02；同一检查器修复后直接/helper别名均报B02及正确位置，合法throw/既有拒绝结果/来源日志/正常业务return通过；六条新sample/dataRow加入L，原64项保留；正式run_lightweight本轮L全部执行通过，真实final_gate对新增行缺失/未执行拒绝；产品/独立预期/原B/E/S/冻结只读摘要不变。证据：.specify/bugs/010-b02-early-return/{assessment,fix,test}.md、pre-fix.trx、独立本轮L凭证及漏跑拒绝记录。代码交付不能代替上述执行证据。

旧动态证据保留；修改后全套聚合未重新执行。原010历史48项及勾选不改，不将旧全局冻结摘要盖章为当前匹配；不重跑B/E/S、Host/PLC/Worker/数据库、全量及整机验证。

- T049完成（2026-10-03）：原检查器两条有效编译/绑定样本实际漏报B02；修复后直接/helper别名均由同一Check报告B02及有效位置，四个合法正例通过。新固定L70全部实际发现/执行/通过，0Skip，当前凭证：E:/dzk/gaode-1/artifacts/recipe-execution-010/bug-010-b02-early-return-76d2915a436f47c7846b7e0b1ad4898a/L/ledger.json（attempt=05b32b1cdcb244d98b43886ccbaff99c）。原final_gate对N02-return-direct缺失与NotExecuted两项隔离故障输入均拒绝，汇总伪造Passed不能抵消，见.specify/bugs/010-b02-early-return/final-gate-negative及test.md。产品228文件和原1182证据文件摘要不变，独立预期和业务冻结文件未改。原48勾选/历史记录保留；本结论仅B02修补与当前L，不表示旧全局冻结匹配当前源或全套动态重新通过。

## 013实施前定向同步（2026-10-04）

SY-04：此前延期的PLC轮询性能由013在通信适配器内实现，不改变共同业务唯一执行路径、CE输入/保存/取消/期限边界。013显式profile必须保留完整持续L（当前70声明项，按实际case/dataRow核对），受影响009同身份结果一次引用；不扩010动态全专项。before测量是有自身源码/构建/输入的子阶段，不套after门槛，不等待最终Passed；013 final保持L外层和必需账本，不能成为通用跳L开关。每侧一次60秒空闲和同一run-2，必要组件含正常HeldFlip失败保存/真实自动1024分段接续/Recorder缺口及三类负例。实际API观察器仅提供2秒后端负载，不能冒充页面ready或改变真实保存/F匹配/冻结/Final。验收严格按013 V01.1 A/B/C分侧和V09.1身份去重。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

本轮仅确认需求同步，不生成新设计或任务；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。

- [ ] T016-I01 定向同步与消费本功能直接相关公共配置/观察/处置/下料边界，产物以新016 tasks T002及对应共同代码任务追踪；原历史编号和勾选不改。
