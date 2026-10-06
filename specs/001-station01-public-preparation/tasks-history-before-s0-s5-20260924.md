> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。第一工位移交前仍无配方调用。

# 后端任务清单：第一工位——公共准备、3D高度采集与F料盘扫码

**功能标识**：001-station01-public-preparation  
**任务清单版本 / 日期**：1.3.0 / 2026-09-24  
**当前输入版本**：[spec.md](spec.md) 1.3.0；[plan.md](plan.md) 1.3.0；[宪章](../../.specify/memory/constitution.md) 3.1.0；历史任务证据保留产生时版本  
**状态**：继承M1已有证据和T001-T067任务；本次按CL-07至CL-09及公开API审计新增T068-T077，覆盖控制路由、权限、统一错误、事件、ETag、受控媒体和Production模拟绑定边界。新增任务未实现、未勾选；完整取消、恢复、真实适配和前端联调仍未完成。
**使用模板**：[tasks-template.md](../../.specify/templates/tasks-template.md)；既有已勾选项保留其实际证据状态，本次只增加与plan/api合同对应的后端增量任务，不修改原型、前端规格或002/003任务。

> 2026-09-23有限授权仅对齐 `s01-handoff/2.0` producer 合同。既有已勾选 T036 只证明当时的 v1 公共移交；v2 producer/consumer 接线、提交后自动续接和验证使用 active feature 003 的 T024/T026 跟踪，本文件不新增竞争任务编号，也不把授权写成实现完成。

## 0. 基线、范围和使用方法

### 0.1 已读取的实际基线

完整读取上述spec/plan、[research](research.md)、[data-model](data-model.md)、[sequences](sequences.md)、[verification](verification.md)、[quickstart](quickstart.md)、[规格检查](checklists/requirements.md)、[设计检查](checklists/design.md)、[宪章对齐记录](../../.specify/memory/constitution-alignment.md)及当前tasks模板。完整读取contracts中的[公共合同](contracts/common.md)、[API](contracts/api.md)、[设备](contracts/device.md)、[采集/算法](contracts/acquisition-algorithm.md)、[保存/移交](contracts/persistence-handoff.md)、[配置/时间](contracts/configuration-time.md)，以及[公共schema](contracts/public-config.schema.json)、[预算schema](contracts/budget.schema.json)、[模拟schema](contracts/simulation.schema.json)和[示例索引](examples/README.md)所列八份JSON。

初版拆解时扫描原有67个文档文件，尚无tasks.md；本次重读已有68个项目文件及当前tasks，仍无业务工程、适用AGENTS.md、Git元数据或Spec Kit命令脚本。祖先E:/和E:/dzk/亦无AGENTS.md。未初始化Git、创建分支、安装工具或声称执行Spec Kit脚本。已有spec/清单中“没有plan/tasks”是原编制时的历史记录，本次不重写历史或把已完成文档列成已执行开发任务。本次spec/宪章/schema/示例保持不变，仅同步受影响plan/合同/任务/验证文档；既有开发任务不因文档修正而勾选。

### 0.2 固定范围

公共上下文与配置准备 → PLC软件请求/安全/实体按钮/夹紧反馈 → 固定XY的3D整盘采集及高度调用 → 固定XY的F单拍及读码 → 必要保存与公共移交。停止于总时序图第23步后、第24步读取配方前。

不实现产品配方查找/匹配/管理或匹配后加载、后续检测计划、E及缺陷检测、翻面/旋转/分拣/卸料、前端/WPF、独立虚拟PLC服务、完整维护平台或真实算法研发。不得新增回零、Z动作、自动对焦或F重拍。外部部署/工艺/硬件能力验证与生产兼容仍按OPEN限制；本清单只指导本功能后端及必要软件验证。

沿用四个src工程和三个tests工程：Gaode.Domain、Gaode.Application、Gaode.Infrastructure、Gaode.Host；Gaode.Rules.Tests、Gaode.Contracts.Tests、Gaode.Integration.Tests。API在Host内；只有Coordinator写运行业务状态，Motion统一准入，Infrastructure实现端口。真实接口缺失不能阻止全模拟；NotIntegrated不能作为已接入成功。

### 0.3 完成和证据规则

- 以下路径均相对当前项目根，均为未来产物；列出路径不表示文件已经存在。一个任务列多个文件是同一可验证能力，不是新增工程。
- 每项任务的“前置”为直接硬依赖；传递依赖由图导出。必须达到前置完成条件才执行依赖任务。证据来自任务列明的测试文件或检查记录；已完成M1的实际证据见 `artifacts/station01/m1-continuation-20260921/`，其余任务仍为NotRun。
- 每项均列FR、适用原REQ ID、P原则及SV。FR至完整REQ/原则关系沿用spec §6及本文§11，不能借摘要删除来源约束。准备、基础和整体验证/收尾任务是US1至US5共享任务，不绑定单一US；各US任务保留明确标签，不重复实现共享能力。
- 每项未来证据统一归档到 `artifacts/station01/{testRunId}/Txxx/`，其中testRunId为实际验证运行标识、Txxx为当前任务ID；写明输入/配置/能力/预算/模拟版本、环境、实际命令、时钟、身份、事件及保存/文件依据、结果和限制。准备/文档任务保存实际检查记录，不制造测试通过。
- 关键正常与故障验证均必做；参数化用例允许共用文件，不能略去输入和断言。真实SQLite和实际媒体集成必须使用独立空Test根准备；R/C中的保存替身不能代替P证据。
- 所有适用场景共用T003断言：零配方操作、零后续计划/工位启动，F未执行为0且最多一次触发；仅许可本功能既定工艺动作及已确认语义的受控停止；质量始终未判定。无F触发的路径不得为了满足“一次”主动拍照。
- 当前不标记任何[P]：各任务有明确前置，且存在工程、状态模型或组合根共享修改。本清单采用保守依赖顺序；条件满足后的独立工作机会见§9，不指定人员、不启动代理。
- 公共基础已经包含延迟、取消信号、背压和保存门。US1不能先用永远即时成功替身串通流程，再跳过这些约束；后续US负责扩展业务出口及完整验证矩阵。

## 1. 必要准备

**目标**：核实已有开发选择，建立唯一后端入口及可执行的验证入口；不把包缓存当作编译成功。

- [x] T001 核验开发依赖并记录实际版本与包可用性；产物：`backend/docs/development-environment.md`。
  - 类型：准备；前置：无。
  - 追溯：FR-031、FR-032、FR-033、FR-038；REQ：SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、DAT-007、NFR-007、ACQ-010、DAT-008、NFR-004；宪章：P02、P05、P07、P08、P09、P10；场景：SV-16、SV-17、SV-34。
  - 完成条件：只读复核research D01/D04/D05/D06版本；在后续准备阶段实际验证SDK、依赖恢复来源及测试包可取得性，记录未验证/缺包；缓存目录不能代替构建成功。不安装厂商SDK、不关闭OPEN-22。
  - 验证证据：保存实际检查命令、版本、结果和缺失清单；工程恢复/构建证据由T002形成。 归档至上述T001证据目录。

- [x] T002 建立四个后端工程、三个测试工程及锁定依赖；产物：`global.json`、`backend/Gaode.slnx`、`backend/Directory.Build.props`、`backend/Directory.Packages.props`、`backend/src/Gaode.Domain/Gaode.Domain.csproj`、`backend/src/Gaode.Application/Gaode.Application.csproj`、`backend/src/Gaode.Infrastructure/Gaode.Infrastructure.csproj`、`backend/src/Gaode.Host/Gaode.Host.csproj`、`backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj`、`backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj`、`backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj`。
  - 类型：准备；前置：T001。
  - 追溯：FR-031、FR-032、FR-033、FR-038；REQ：SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、DAT-007、NFR-007、ACQ-010、DAT-008、NFR-004；宪章：P02、P05、P07、P08、P09、P10；场景：SV-16、SV-17、SV-34。
  - 完成条件：沿用net10.0/SDK10.0.401、EF/SQLite/Mvc.Testing10.0.12、xUnit2.9.3/runner3.1.5/Test SDK18.10.1/TimeProvider.Testing10.0.0；实际restore/build并锁定解析依赖，各工程旁生成packages.lock.json。Host仅最小入口，不建库/接设备；无15工程或额外服务。
  - 验证证据：记录restore/build输出及三测试工程发现结果；无用例时明确NoTests而非产品通过。 归档至上述T002证据目录。

- [x] T003 建立依赖约束和共同证据断言入口；产物：`backend/tests/Gaode.Rules.Tests/Architecture/DependencyRulesTests.cs`、`backend/tests/Gaode.Contracts.Tests/Support/PortTraceAssertions.cs`、`backend/tests/Gaode.Integration.Tests/Support/Station01Evidence.cs`。
  - 类型：验证；前置：T002。
  - 追溯：FR-025、FR-026、FR-031、FR-033；REQ：RCP-001、RCP-005、HMI-004、HMI-007、DAT-008、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、NFR-007、ACQ-010；宪章：P01、P02、P05、P07、P09；场景：SV-01、SV-16、SV-22、SV-31。
  - 完成条件：自动核对Domain纯规则、Application端口、Infrastructure实现、Api只到Application及唯一Host；定义供后续所有场景调用的计数/身份/版本/时间/保存证据格式。无配方操作/后续启动、F≤1、无额外工艺动作、质量未判定为共同断言。
  - 验证证据：依赖测试结果；断言工具以违反边界的最小轨迹证明能检出，不能只测空轨迹通过。 归档至上述T003证据目录。

## 2. 公共基础能力

**目标**：先实现统一身份、配置、策略、期限、事件、运动、模拟、媒体及保存能力。各US引用这些能力，不重复创建时钟、连接、运动通道或写库入口。

- [x] T004 建立身份、六维状态、错误合同及不可变运行模型；产物：`backend/src/Gaode.Domain/Station01/RunIdentity.cs`、`backend/src/Gaode.Domain/Station01/RunState.cs`、`backend/src/Gaode.Domain/Station01/OperationState.cs`、`backend/src/Gaode.Domain/Station01/RunSnapshot.cs`、`backend/src/Gaode.Domain/Diagnostics/StageError.cs`、`backend/tests/Gaode.Rules.Tests/Station01/StateModelTests.cs`。
  - 类型：实现；前置：T002。
  - 追溯：FR-001、FR-016、FR-017、FR-018、FR-026；REQ：TASK-002、TASK-003、CTL-004、ID-005、ACQ-005、ALG-008、TASK-007、TASK-008、CTL-008、ALG-014、HMI-004、HMI-007、DAT-008；宪章：P05、P07、P08、P09；场景：SV-01、SV-09、SV-11、SV-22。
  - 完成条件：分别表达Run/Action/Capture/Algorithm/Save/Handoff及步骤完整性；Recipe=Unmatched/Quality=NotEvaluated/Sorting=NotStarted；Operation/Attempt/Session独立，不伪造Part/Face。Completed/Cancelled不可复活，观察与持久版本分开。
  - 验证证据：状态转换表驱动的允许/拒绝测试、不可变快照及错误字段断言。 归档至上述T004证据目录。

- [x] T005 定义PLC观察、动作、运动与控制事实端口；产物：`backend/src/Gaode.Application/Ports/IPlcStatePort.cs`、`backend/src/Gaode.Application/Ports/IPlcActionPort.cs`、`backend/src/Gaode.Application/Ports/IMotionPort.cs`、`backend/src/Gaode.Application/Ports/DeviceMessages.cs`、`backend/tests/Gaode.Contracts.Tests/Devices/DeviceMessageContractTests.cs`。
  - 类型：实现；前置：T004。
  - 追溯：FR-007、FR-008、FR-009、FR-018、FR-019、FR-033；REQ：SYS-004、CTL-004、CTL-005、SYS-006、ID-006、CTL-006、CTL-010、POS-001、CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、NFR-007、ACQ-010；宪章：P03、P04、P05、P06、P07、P08、P09；场景：SV-08、SV-09、SV-10、SV-32。
  - 完成条件：完整实现device/common合同信封：关联、版本、期限Phase、用途、ACK/执行/完成/停止分离；运动底层请求通过IPlcActionPort传递语义目标。Start关联PLC夹紧观察，不增加PC夹紧指令、原始寄存器API或复位命令。
  - 验证证据：序列化及信封验证测试：缺ID/旧代次/错版本被拒，ACK不产生完成。 归档至上述T005证据目录。

- [x] T006 定义采集、算法、媒体引用及租约端口；产物：`backend/src/Gaode.Application/Ports/ICapturePort.cs`、`backend/src/Gaode.Application/Ports/IAlgorithmPort.cs`、`backend/src/Gaode.Application/Ports/IMediaStore.cs`、`backend/src/Gaode.Application/Ports/CaptureAlgorithmMessages.cs`、`backend/tests/Gaode.Contracts.Tests/Acquisition/MessageContractTests.cs`。
  - 类型：实现；前置：T004。
  - 追溯：FR-010、FR-011、FR-014、FR-016、FR-020、FR-022、FR-033；REQ：ACQ-002、ACQ-008、CTL-008、CTL-010、ID-005、POS-002、ALG-013、SAF-005、SAF-011、TASK-003、ACQ-005、ALG-008、ACQ-006、ACQ-010、DAT-001、DAT-002、DAT-007、NFR-007；宪章：P03、P04、P05、P06、P07、P08、P09；场景：SV-06、SV-13、SV-23、SV-25、SV-31。
  - 完成条件：包含CaptureEnded与MediaTaken组合证据、原始高度集合及候选、responseReceived、只读媒体租约、Call技术终态；F单帧、不增加运动。相机未知与已可靠结束的内容缺失分开。
  - 验证证据：缺结束依据/仅帧、null与空候选、多个高度自身标识、跨Capture关联的契约测试。 归档至上述T006证据目录。

- [x] T007 定义配置、保存、查询和移交端口；产物：`backend/src/Gaode.Application/Ports/IPublicConfiguration.cs`、`backend/src/Gaode.Application/Ports/ITraceWriter.cs`、`backend/src/Gaode.Application/Ports/ITraceQuery.cs`、`backend/src/Gaode.Application/Ports/IStageHandoffQuery.cs`、`backend/src/Gaode.Application/Ports/PersistenceMessages.cs`、`backend/tests/Gaode.Contracts.Tests/Persistence/ReceiptContractTests.cs`。
  - 类型：实现；前置：T004。
  - 追溯：FR-003、FR-004、FR-017、FR-018、FR-019、FR-021、FR-023、FR-024、FR-030、FR-032、FR-039；REQ：RCP-001、RCP-002、POS-001、RCP-005、DAT-001、TASK-007、TASK-008、ALG-008、CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-007、SAF-010、DAT-010、ACQ-010、NFR-007、DAT-008；宪章：P02、P03、P04、P05、P06、P07、P08、P09、P10、P11；场景：SV-12、SV-15、SV-17、SV-18、SV-19、SV-35。
  - 完成条件：SubmitCritical只回排队回执，Committed包含WriteId/revision；同WriteId异内容冲突。定义AlgorithmIntent批次及终态ExpectedRevision/ExpectedTerminal/候选终态和ConditionRejected结果，原WriteId未知核对语义按保存合同§1。Config/Public/Budget/Simulation快照可追溯；移交只有读取端口。
  - 验证证据：入队/提交未知/实际提交、重复回执和快照引用合同验证。 归档至上述T007证据目录。

- [x] T008 实现三类配置结构及受控文件加载；产物：`backend/src/Gaode.Domain/Configuration/PublicConfiguration.cs`、`backend/src/Gaode.Domain/Configuration/BusinessBudget.cs`、`backend/src/Gaode.Domain/Configuration/SimulationProfile.cs`、`backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs`、`backend/tests/Gaode.Contracts.Tests/Configuration/SchemaInputTests.cs`。
  - 类型：实现；前置：T007。
  - 追溯：FR-003、FR-005、FR-028、FR-034、FR-035、FR-039；REQ：RCP-001、RCP-002、POS-001、NFR-007、RCP-003、CTL-007、CTL-008、ACQ-002、ALG-013、RCP-005、DAT-001、DAT-008；宪章：P03、P04、P07、P08、P09、P10、P11；场景：SV-07、SV-14、SV-16、SV-27、SV-35。
  - 完成条件：按三份schema严格解析并校验类型/未知属性/版本/路径，公共配置、预算和六种profile分别加载；引用ID/版本而非任意路径。实际加载八份原示例，算法可空字段和预算不能一概变成运动错误；delay>budget合法。
  - 验证证据：八份示例及缺项/越界/非法数值/脚本/路径逃逸输入的结构测试；不另造生产默认值。 归档至上述T008证据目录。

- [x] T009 实现受限策略接口及能力注册表；产物：`backend/src/Gaode.Application/Capabilities/ICapabilityPolicy.cs`、`backend/src/Gaode.Application/Capabilities/CapabilityRegistry.cs`、`backend/src/Gaode.Application/Capabilities/Station01Policies.cs`、`backend/src/Gaode.Host/Composition/CapabilityRegistration.cs`、`backend/tests/Gaode.Rules.Tests/Configuration/CapabilityPolicyTests.cs`。
  - 类型：实现；前置：T006、T008。
  - 追溯：FR-028、FR-029、FR-031；REQ：RCP-003、POS-001、NFR-007、RCP-002、ALG-013、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P03、P04、P05、P09、P11；场景：SV-05、SV-14、SV-21。
  - 完成条件：仅注册固定XY、整盘3D、F单帧、高度、原始码及可选测试解析能力；校验输入输出/版本/用途/资源。策略无设备、DB、文件句柄；未知能力不执行，算法就绪独立于能力兼容；通过测试策略扩展验证，无产品型号分支。
  - 验证证据：注册/不兼容/脚本/旁路拒绝，测试新策略经组合根接入且仍经过准入的证据。 归档至上述T009证据目录。

- [x] T010 实现公共配置语义校验与三类快照冻结；产物：`backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs`、`backend/src/Gaode.Application/Configuration/ConfigurationFreezer.cs`、`backend/tests/Gaode.Rules.Tests/Configuration/ConfigurationValidationTests.cs`。
  - 类型：实现；前置：T008、T009。
  - 追溯：FR-003、FR-004、FR-005、FR-006、FR-028、FR-029、FR-039；REQ：RCP-001、RCP-002、POS-001、RCP-005、DAT-001、NFR-007、CTL-003、SAF-011、RCP-003、ALG-013、DAT-008；宪章：P02、P03、P04、P07、P08、P09、P10、P11；场景：SV-05、SV-07、SV-14、SV-15、SV-16、SV-21、SV-23、SV-35。
  - 完成条件：一次校验3D/F全部运动和采集配置、WholeTray、单位/限值/绑定/F单拍及固定终点；按字段域区分blockingControlErrors和algorithmIssues。冻结规范化副本/摘要/能力版本，不热路径反复哈希；Test/Real和Controlled/Real拒绝，同ID版本异内容拒绝。
  - 验证证据：参数化正常/缺F/范围非法/算法缺预算/用途冲突及源文件修改后快照不变的测试。 归档至上述T010证据目录。

- [x] T011 实现统一业务期限调度器；产物：`backend/src/Gaode.Application/Timing/DeadlineScheduler.cs`、`backend/src/Gaode.Application/Timing/DeadlineWindow.cs`、`backend/tests/Gaode.Rules.Tests/Timing/DeadlineSchedulerTests.cs`。
  - 类型：实现；前置：T004、T008。
  - 追溯：FR-014、FR-019、FR-035、FR-036、FR-038；REQ：ALG-013、SAF-005、SAF-011、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-008、NFR-004；宪章：P04、P06、P07、P08、P09、P10；场景：SV-05、SV-29、SV-30、SV-32、SV-34。
  - 完成条件：唯一注入TimeProvider，按Operation/Attempt/Phase注册单调起点和冻结预算；PLC受理、XY总期限含受理、采集、算法含队列、保存及停止各按合同起算。缺算法预算直接NotConfigured；回调只投递事件，不改业务状态。
  - 验证证据：System/Fake共用路径及起算点/ACK不重置预算/UTC校时不改变截止的规则证据。 归档至上述T011证据目录。

- [x] T012 实现入站时间戳、Phase窗口及确定性仲裁；产物：`backend/src/Gaode.Application/Timing/OperationIngress.cs`、`backend/src/Gaode.Application/Timing/IngressDecision.cs`、`backend/tests/Gaode.Rules.Tests/Timing/OperationIngressTests.cs`。
  - 类型：实现；前置：T005、T006、T011。
  - 追溯：FR-018、FR-027、FR-036、FR-038、FR-040；REQ：CTL-008、ACQ-005、ALG-014、NFR-004、NFR-007、SAF-011、CTL-007、ALG-013、DAT-008、ACQ-010；宪章：P04、P06、P07、P08、P09、P10；场景：SV-10、SV-11、SV-29、SV-30、SV-31、SV-32、SV-34。
  - 完成条件：Host有效入站时间为权威，[start,due)接受，恰好到期超时；Accepted不关闭Completion，采集须Ended和MediaTaken或明确内容缺失。短原子仲裁输出不可变事件，早到晚处理不误超时；取消先关准入，迟到只追加证据。
  - 验证证据：D-1/D/D+1、到期回调先后、错误关联、分Phase重复到期、仅帧/仅Ended的裁决测试。 归档至上述T012证据目录。

- [x] T013 建立可控时钟逐时刻推进夹具及核心计时验证；产物：`backend/tests/Gaode.Contracts.Tests/Support/ControlledTimeDriver.cs`、`backend/tests/Gaode.Contracts.Tests/Timing/TimeDriverContractTests.cs`。
  - 类型：验证；前置：T011、T012。
  - 追溯：FR-034、FR-036、FR-037、FR-038；REQ：CTL-007、CTL-008、ACQ-002、ALG-013、SAF-011、NFR-004、SAF-003、SAF-004、DAT-008；宪章：P04、P06、P07、P09、P10；场景：SV-27、SV-29、SV-30、SV-32、SV-33、SV-34。
  - 完成条件：AutoAdvance=0；下一事件时刻Advance后Drain已就绪短链，再推进下一时刻；不等待未来计时器、不假报磁盘完成。不推进时间仍能受理普通查询/控制；虚拟/实际时间与审计UTC区分。
  - 验证证据：多定时器先后/同刻到期、早到响应、静止时钟受理及真实I/O未完成不能被Drain完成的契约证据。 归档至上述T013证据目录。

- [x] T014 建立唯一流程事件所有者和有界控制路径；产物：`backend/src/Gaode.Application/Station01/Station01Coordinator.cs`、`backend/src/Gaode.Application/Station01/FlowMailbox.cs`、`backend/src/Gaode.Application/Station01/ControlLatch.cs`、`backend/src/Gaode.Application/Station01/TerminalReservation.cs`、`backend/tests/Gaode.Rules.Tests/Station01/FlowMailboxTests.cs`。
  - 类型：实现；前置：T004、T007、T012。
  - 追溯：FR-017、FR-018、FR-026、FR-027、FR-037；REQ：TASK-007、TASK-008、ALG-008、CTL-008、ACQ-005、ALG-014、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011、SAF-003、SAF-004；宪章：P04、P06、P07、P08、P09；场景：SV-12、SV-20、SV-31、SV-33。
  - 完成条件：Coordinator仅短事件归约并发布快照，异步操作投递后返回；普通/控制/终态容量按冻结预算，受理前预留唯一终态位。stop/cancel/fault独立锁存，重复非终态可合并，必要终态不丢；初始骨架不编造整条成功流程。
  - 验证证据：队列满仍处理控制/唯一终态、状态仅一个写者、慢端口不占事件线程及重复回执证据。 归档至上述T014证据目录。

- [x] T015 实现统一运动准入、资源租约和Unknown/Held；产物：`backend/src/Gaode.Application/Motion/MotionCoordinator.cs`、`backend/src/Gaode.Application/Motion/MotionAdmission.cs`、`backend/src/Gaode.Application/Motion/ResourceLease.cs`、`backend/tests/Gaode.Rules.Tests/Motion/MotionAdmissionTests.cs`。
  - 类型：实现；前置：T005、T010、T012、T014。
  - 追溯：FR-002、FR-008、FR-009、FR-019、FR-027、FR-031；REQ：TASK-009、CTL-008、CTL-005、SYS-006、ID-006、CTL-006、CTL-010、POS-001、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、NFR-004、NFR-007、SAF-011、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P03、P04、P05、P06、P07、P08、P09；场景：SV-09、SV-10、SV-12、SV-16、SV-20、SV-32。
  - 完成条件：一次预约设备/XY/位置保持，校验动态安全、新鲜反馈、用途、固定目标和已提交意图；统一Start/XY/停止控制权。到位与停止分别确认，未知保持Held；停止独立入口不等算法/写盘，完成不自动释放物理占用。
  - 验证证据：争用/非法目标/旧反馈/Test真实拒绝、ACK非完成、未知状态无盲重发及停止优先测试。 归档至上述T015证据目录。

- [x] T016 实现有界模拟事件调度与故障策略；产物：`backend/src/Gaode.Infrastructure/Simulation/SimulationEventScheduler.cs`、`backend/src/Gaode.Infrastructure/Simulation/ResponsePolicy.cs`、`backend/tests/Gaode.Contracts.Tests/Simulation/ResponsePolicyTests.cs`。
  - 类型：实现；前置：T008、T011、T013。
  - 追溯：FR-034、FR-035、FR-036、FR-037、FR-038；REQ：CTL-007、CTL-008、ACQ-002、ALG-013、SAF-011、NFR-004、SAF-003、SAF-004、DAT-008；宪章：P04、P06、P07、P09、P10；场景：SV-27、SV-28、SV-31、SV-32、SV-33、SV-34。
  - 完成条件：共用同一TimeProvider，支持Respond/Fail/NoResponse及outcome、ignoreCancel、重复偏移；耗时和预算独立。按128等示例容量限制定时事件，最多4次重复，迟到明细与摘要有界；只产生端口事实，绝不直接设置TimedOut。
  - 验证证据：零延迟仍分事件、超预算合法、明确失败/不响应/重复/取消策略及容量拒绝测试。 归档至上述T016证据目录。

- [x] T017 实现独立状态的进程内PLC模拟适配；产物：`backend/src/Gaode.Infrastructure/Simulation/SimulatedPlc.cs`、`backend/src/Gaode.Infrastructure/Simulation/SimulatedDeviceState.cs`、`backend/tests/Gaode.Contracts.Tests/Simulation/SimulatedPlcTests.cs`。
  - 类型：实现；前置：T005、T015、T016。
  - 追溯：FR-007、FR-008、FR-019、FR-033、FR-034、FR-037、FR-040；REQ：SYS-004、CTL-004、CTL-005、SYS-006、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、NFR-007、ACQ-010、CTL-008、ACQ-002、NFR-004、SAF-011、ACQ-005、ALG-014；宪章：P04、P05、P06、P07、P08、P09；场景：SV-08、SV-09、SV-27、SV-28、SV-32、SV-33。
  - 完成条件：设备自行维护安全/按钮/夹紧/XY/在途动作；收到Start只等待合法实体输入，不直接夹紧。PLC受理/夹紧/两次XY按各起点延迟，内部完成与反馈丢失分开；独立心跳及停止状态，不读Workflow期望结果。
  - 验证证据：逐段观察Pending/Accepted/Executing/Completed，未按按钮无夹紧；无反馈但内部完成仍需核对；迟到重复不多动作。 归档至上述T017证据目录。

- [x] T018 实现进程内3D/F采集模拟适配；产物：`backend/src/Gaode.Infrastructure/Simulation/SimulatedCapture.cs`、`backend/src/Gaode.Infrastructure/Simulation/SyntheticMediaFixture.cs`、`backend/tests/Gaode.Contracts.Tests/Simulation/SimulatedCaptureTests.cs`。
  - 类型：实现；前置：T006、T016。
  - 追溯：FR-010、FR-011、FR-020、FR-022、FR-033、FR-034；REQ：ACQ-002、ACQ-008、CTL-008、CTL-010、ID-005、POS-002、ACQ-006、ACQ-010、SAF-011、DAT-001、DAT-002、DAT-007、NFR-007、CTL-007；宪章：P03、P04、P05、P06、P07、P08、P09；场景：SV-13、SV-23、SV-25、SV-27、SV-32。
  - 完成条件：相机模型维护连接代次、曝光/传输及缓冲所有权，按各自延迟发Ended和媒体；F一次触发单帧，3D合成媒体显式WholeTray；可独立注入硬件未知/内容缺失、重复/迟到帧，不假设真实覆盖。
  - 验证证据：真实生成的测试字节/引用、触发次数、重复释放、Frame非Ended及内容/设备故障区别证据。 归档至上述T018证据目录。

- [x] T019 实现进程内高度和读码算法模拟适配；产物：`backend/src/Gaode.Infrastructure/Simulation/SimulatedAlgorithm.cs`、`backend/tests/Gaode.Contracts.Tests/Simulation/SimulatedAlgorithmTests.cs`。
  - 类型：实现；前置：T006、T016。
  - 追溯：FR-011、FR-013、FR-014、FR-033、FR-034、FR-035、FR-040；REQ：ID-005、POS-002、ID-001、RCP-001、ALG-013、SAF-005、SAF-011、NFR-007、ACQ-010、CTL-007、CTL-008、ACQ-002、ACQ-005、ALG-014；宪章：P02、P03、P04、P05、P06、P07、P08、P09、P10；场景：SV-05、SV-06、SV-26、SV-27、SV-29、SV-30、SV-31。
  - 完成条件：从已接管媒体/参数和fixture取得原始输出，分别调度高度/读码延迟；支持未就绪、明确失败、无响应和超期输出。高度返回多个原标识，不映射槽位；调用预算由Application决定，不返回预设Timeout冒充期限。
  - 验证证据：独立两角色结果、原始重复候选及无响应、取消后迟到事实的端口测试。 归档至上述T019证据目录。

- [x] T020 建立本阶段EF模型、约束与单一初始迁移；产物：`backend/src/Gaode.Infrastructure/Persistence/Station01DbContext.cs`、`backend/src/Gaode.Infrastructure/Persistence/Station01EntityConfigurations.cs`、`backend/src/Gaode.Infrastructure/Persistence/Migrations/InitialStation01.cs`、`backend/src/Gaode.Infrastructure/Persistence/Migrations/Station01DbContextModelSnapshot.cs`、`backend/tests/Gaode.Contracts.Tests/Persistence/ModelContractTests.cs`。
  - 类型：实现；前置：T004、T007。
  - 追溯：FR-001、FR-002、FR-016、FR-017、FR-018、FR-019、FR-021、FR-022、FR-024、FR-030、FR-032、FR-039；REQ：TASK-002、TASK-003、CTL-004、TASK-009、CTL-008、ID-005、ACQ-005、ALG-008、TASK-007、TASK-008、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-002、ACQ-010、NFR-007、RCP-005、DAT-008；宪章：P02、P04、P05、P06、P07、P08、P09、P10；场景：SV-10、SV-12、SV-17、SV-18、SV-19、SV-35。
  - 完成条件：按data-model只建当前记录，包含Call意图/派发依据及Run.TerminalOutcome/TerminalRevision、取消命令未决/最终结果；唯一键、外键、revision条件更新及Handoff到Run固定终态/revision的关联约束一致。Cancelled无Handoff，完成与Handoff只能同事务建立；无Recipe/Part/Sorting表。用同一EF模型生成s01-store/1初始迁移，当前未有库，不新增生产迁移。
  - 验证证据：元数据/迁移SQL结构审查与约束测试；实际SQLite加载及结构建立由T021隔离夹具验证。 归档至上述T020证据目录。

- [x] T021 实现共同维护锁、结构核验及独立Test库准备夹具；产物：`backend/src/Gaode.Infrastructure/Persistence/StoreAccessGuard.cs`、`backend/src/Gaode.Infrastructure/Persistence/StoreCompatibilityProbe.cs`、`backend/tests/Gaode.Integration.Tests/Storage/StorePreparation.cs`、`backend/tests/Gaode.Integration.Tests/Storage/StorePreparationTests.cs`。
  - 类型：实现；前置：T020。
  - 追溯：FR-021、FR-023、FR-032；REQ：DAT-001、DAT-007、SAF-010、DAT-010、NFR-007；宪章：P05、P06、P07、P08；场景：SV-17、SV-18、SV-19、SV-20。
  - 完成条件：显式空Test根、规范化路径及OS独占锁，Inspect/Initialize/Verify复用同一初始迁移并输出manifest；已有库不覆盖。Host只读核验再ReadWrite非Create，缺库/版本不合/维护中受限，不EnsureCreated/Migrate；仅测试入口可准备空库。
  - 验证证据：在隔离SQLite实际验证原生加载、外键/唯一约束、WAL/同步配置、两Host/准备器互斥、路径别名、已有库保留及缺库不创建。 归档至上述T021证据目录。

- [x] T022 实现单写通道、独立查询与提交核对；产物：`backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs`、`backend/src/Gaode.Infrastructure/Persistence/TraceQuery.cs`、`backend/src/Gaode.Infrastructure/Persistence/CommitReconciler.cs`、`backend/tests/Gaode.Integration.Tests/Storage/TraceStoreTests.cs`、`backend/tests/Gaode.Integration.Tests/Support/CommitInterleavingGate.cs`。
  - 类型：实现；前置：T007、T011、T014、T021。
  - 追溯：FR-002、FR-017、FR-018、FR-019、FR-021、FR-023、FR-024、FR-026、FR-030、FR-032；REQ：TASK-009、CTL-008、TASK-007、TASK-008、ALG-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-010、HMI-004、HMI-007、DAT-008、ACQ-010、NFR-007；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-10、SV-12、SV-17、SV-18、SV-19、SV-20。
  - 完成条件：独立有界IO消费者、短事务、独立DbContext；WriteId幂等。实现AlgorithmIntent提交及Run revision/None条件终态事务：完成与Handoff原子提交，取消与命令最终结果原子提交；ConditionRejected不冒充保存成功。排队非Committed，超时保留原WriteId/CommitUnknown，排除在途旧写者后才确认未提交；版本变化重新核对，不重用WriteId改载荷。查询预算1000ms，控制/内存查询不等写盘。
  - 验证证据：真实SQLite提交/条件拒绝/重复/冲突/晚回执/短读测试；提供仅测试用的提交前、提交后回执前可控屏障，实际事务仍由真实Writer执行，供T048/T053/T054复用。T022只验证持久原语，不前置完整CancelRun用例。归档至上述T022证据目录。

- [x] T023 实现媒体文件、容量预约及租约所有权；产物：`backend/src/Gaode.Infrastructure/Media/MediaStore.cs`、`backend/src/Gaode.Infrastructure/Media/MediaLeaseRegistry.cs`、`backend/src/Gaode.Infrastructure/Media/MediaCapacity.cs`、`backend/tests/Gaode.Integration.Tests/Media/MediaStoreTests.cs`。
  - 类型：实现；前置：T006、T008、T021、T022。
  - 追溯：FR-020、FR-022、FR-023、FR-027；REQ：ACQ-006、ACQ-010、SAF-011、DAT-001、DAT-002、DAT-007、DAT-010、NFR-004、NFR-007；宪章：P04、P06、P08、P09；场景：SV-13、SV-18、SV-20、SV-29。
  - 完成条件：接管缓冲一次，受控路径临时写/完成刷新/同卷成品/元数据提交分别反馈；内存/文件有界，给F保留容量。Worker未释放/未退出不归还租约；释放不删除必要证据，满盘不静默删图。
  - 验证证据：真实文件及SQLite元数据验证：未完成不可读、逃逸/链接拒绝、重复接管、配额不足、在用文件不删、3D占用不侵吞F预约。 归档至上述T023证据目录。

- [x] T024 实现公共采集协调及安全接管；产物：`backend/src/Gaode.Application/Acquisition/AcquisitionCoordinator.cs`、`backend/src/Gaode.Application/Acquisition/CaptureEvidenceGate.cs`、`backend/tests/Gaode.Contracts.Tests/Acquisition/CaptureCoordinationTests.cs`。
  - 类型：实现；前置：T006、T012、T014、T015、T018、T022、T023。
  - 追溯：FR-010、FR-011、FR-018、FR-020、FR-022、FR-023、FR-027；REQ：ACQ-002、ACQ-008、CTL-008、CTL-010、ID-005、POS-002、ACQ-005、ALG-014、ACQ-006、ACQ-010、SAF-011、DAT-001、DAT-002、DAT-007、DAT-010、NFR-004、NFR-007；宪章：P03、P04、P06、P07、P08、P09；场景：SV-08、SV-10、SV-13、SV-23、SV-25、SV-32。
  - 完成条件：触发前预约终态/媒体/位置保持并保存Capture意图；可靠XY事实提交后才控制光源/相机。Ended和媒体接管分别匹配，文件及元数据提交后才供算法；硬件未知保留资源，可靠Ended内容缺失可收敛。
  - 验证证据：仅帧/仅Ended/错代次/重复帧/意图未提交均不越门；F不重拍；回调不执行推理/写盘/阻塞等待。 归档至上述T024证据目录。

- [x] T025 实现有界算法调度、有限终态与独立回收；产物：`backend/src/Gaode.Application/Algorithms/AlgorithmRuntime.cs`、`backend/src/Gaode.Application/Algorithms/AlgorithmLeaseSupervisor.cs`、`backend/tests/Gaode.Contracts.Tests/Algorithms/AlgorithmRuntimeTests.cs`。
  - 类型：实现；前置：T006、T010、T012、T014、T019、T023。
  - 追溯：FR-014、FR-016、FR-018、FR-021、FR-022、FR-027、FR-029、FR-030、FR-036、FR-037、FR-040；REQ：ALG-013、SAF-005、SAF-011、TASK-003、ID-005、ACQ-005、ALG-008、CTL-008、ALG-014、DAT-001、DAT-007、SAF-010、DAT-002、NFR-004、NFR-007、RCP-002、TASK-007、ACQ-010、SAF-008、SAF-009、CTL-007、SAF-003、SAF-004；宪章：P04、P06、P07、P08、P09、P11；场景：SV-05、SV-11、SV-17、SV-18、SV-19、SV-20、SV-24、SV-29、SV-30、SV-31、SV-33。
  - 完成条件：每角色独立槽/有界队列，复用经T023传递依赖的T007/T022保存端口。登记原Call期限后独立提交完整AlgorithmIntent，只有匹配Committed且原期限有效才派发；Failed/CommitUnknown/条件拒绝不派发，意图不等于Accepted。预算覆盖保存等待、排队/执行，保存中到期记TimedOut(PreDispatch)，晚提交不Execute、不重置预算。缺预算/能力未配置/未接入/未就绪有限终态；保存门未满足不推进。后台隔离/退出核对与媒体租约、高度/F槽分离。
  - 验证证据：真实期限驱动保存中到期/正常派发/排队超时；断言意图缺失、失败或未知时端口Execute=0，意图Committed≠Worker接受，迟到提交不派发，租约释放/角色隔离仍成立。完整SQLite及中断证据归T053/T054。归档至上述T025证据目录。

- [ ] T026 实现PLC通用连接、轮询与可配置协议边界；产物：`backend/src/Gaode.Infrastructure/Devices/Plc/PlcAdapter.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/IPlcTransport.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcProtocolCodec.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcConnectionPump.cs`、`backend/tests/Gaode.Contracts.Tests/Devices/PlcTransportTests.cs`。
  - 类型：实现；前置：T005、T011、T012、T015。
  - 追溯：FR-007、FR-008、FR-018、FR-019、FR-027、FR-033；REQ：SYS-004、CTL-004、CTL-005、SYS-006、CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、NFR-004、NFR-007、SAF-011、ACQ-010；宪章：P04、P05、P06、P07、P08、P09；场景：SV-08、SV-09、SV-10、SV-16、SV-32、SV-33。
  - 完成条件：实现生命周期、代次、单在途与独立停止/心跳入口、有限传输IO、可配置编码与语义映射；使用明确Test字段映射和假传输测试，保留PC轮询≤50ms/3s断联基线。真实地址/按钮/停止/反馈映射缺失时NotIntegrated/Unverifiable，不发真实请求；不研发通用通信平台。
  - 验证证据：假传输中短帧/字序/超时/旧反馈/重连不重放及轮询不被动作等待占住的测试。真实Modbus网络和映射部分按OPEN-08/10/11/24/26待接入。 归档至上述T026证据目录。

- [ ] T027 实现相机光源通用会话及SDK回调隔离边界；产物：`backend/src/Gaode.Infrastructure/Devices/Cameras/CameraCaptureAdapter.cs`、`backend/src/Gaode.Infrastructure/Devices/Cameras/ICameraSdkGateway.cs`、`backend/src/Gaode.Infrastructure/Devices/Cameras/ILightGateway.cs`、`backend/tests/Gaode.Contracts.Tests/Devices/CameraGatewayTests.cs`。
  - 类型：实现；前置：T006、T012、T023、T024。
  - 追溯：FR-010、FR-018、FR-020、FR-022、FR-027、FR-033；REQ：ACQ-002、ACQ-008、CTL-008、CTL-010、ACQ-005、ALG-014、ACQ-006、ACQ-010、SAF-011、DAT-001、DAT-002、DAT-007、NFR-004、NFR-007；宪章：P03、P04、P05、P06、P07、P08、P09；场景：SV-10、SV-13、SV-16、SV-25、SV-32。
  - 完成条件：通过SDK网关接口实现连接复用、参数应用、断线代次、触发关联、结束/接管事实及轻量回调；无厂商SDK时明确NotIntegrated，不自动模拟。SDK不可取消/位数限制登记OPEN-22，不凭空建新设备进程或扫码重试。
  - 验证证据：假SDK网关注入旧回调/异常/重复/阻塞风险验证；无已确认Ended不得放开位置保持；记录厂商网关实现依赖。 归档至上述T027证据目录。

- [ ] T028 实现Python内部协议编解码及输入输出边界；产物：`backend/src/Gaode.Infrastructure/Algorithms/WorkerProtocolCodec.cs`、`backend/src/Gaode.Infrastructure/Algorithms/WorkerMessages.cs`、`backend/tests/Gaode.Contracts.Tests/Algorithms/WorkerProtocolTests.cs`。
  - 类型：实现；前置：T006、T012、T023。
  - 追溯：FR-014、FR-016、FR-018、FR-022、FR-027、FR-031、FR-033；REQ：ALG-013、SAF-005、SAF-011、TASK-003、ID-005、ACQ-005、ALG-008、CTL-008、ALG-014、DAT-001、DAT-002、DAT-007、NFR-004、NFR-007、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、ACQ-010；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-05、SV-06、SV-11、SV-16、SV-31。
  - 完成条件：按合同NDJSON消息/身份/版本/lease实现64KiB有界帧处理，保留受限原始字节及截断原因；未知会话/Call隔离，媒体只读引用，无图像Base64/任意脚本/设备/DB旁路。
  - 验证证据：Hello/Execute/Accepted/Result/Cancel/InputReleased/Health/Shutdown，超长/残帧/错版本/路径归属的假传输契约测试。 归档至上述T028证据目录。

- [ ] T029 实现常驻Worker生命周期与真实未接入出口；产物：`backend/src/Gaode.Infrastructure/Algorithms/PythonWorkerAdapter.cs`、`backend/src/Gaode.Infrastructure/Algorithms/IWorkerProcess.cs`、`backend/src/Gaode.Infrastructure/Algorithms/WorkerProcessSupervisor.cs`、`backend/tests/Gaode.Contracts.Tests/Algorithms/WorkerLifecycleTests.cs`、`backend/tests/Gaode.Integration.Tests/Algorithms/Fixtures/protocol_worker.py`、`backend/tests/Gaode.Integration.Tests/Algorithms/WorkerProcessTests.cs`。
  - 类型：实现；前置：T025、T028。
  - 追溯：FR-014、FR-018、FR-027、FR-031、FR-033、FR-040；REQ：ALG-013、SAF-005、SAF-011、CTL-008、ACQ-005、ALG-014、NFR-004、NFR-007、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、ACQ-010、CTL-007；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-05、SV-11、SV-16、SV-29、SV-30、SV-31、SV-33。
  - 完成条件：按角色独立常驻、stdin/stdout协议与有界stderr消费；显式Python3.12允许路径，超时业务收敛与退出回收分开，InputReleased或确认退出才释放。全模拟不启动Python；组件缺失NotIntegrated。fixture仅测试协议/退出，无真实算法或训练实现。 本任务完成门是通用生命周期及假IWorkerProcess合同；真实解释器启动/算法包兼容证据按H-WORKER另列，不阻断完整进程内模拟。
  - 验证证据：假进程必测退出/无响应/取消不合作/卡stderr及租约回收；协议fixture文件和可执行用例必须具备。H-WORKER在显式解释器具备后验证真实子进程，未执行记NotRun，不能以Skip冒充通过或作为模拟前置。 归档至上述T029证据目录。

## 3. US1：完成公共准备并取得移交结果（优先级：P1）

**目标**：授权调用者提交本次启动上下文，获得请求受理及进度；PLC按规则完成实体启动和夹紧，后端依次采集3D及F，保存后返回仅属于公共阶段的结果。

**独立完成条件**：T038验证公共流程正常保存并移交；只能达成中间里程碑，不能代表US2至US5已完成。

- [x] T030 [US1] 装配唯一Host与正式/模拟绑定和生命周期；产物：`backend/src/Gaode.Host/Program.cs`、`backend/src/Gaode.Host/Composition/Station01Registration.cs`、`backend/src/Gaode.Host/Composition/AdapterBindings.cs`、`backend/src/Gaode.Host/Lifecycle/Station01HostedService.cs`、`backend/tests/Gaode.Integration.Tests/Hosting/HostLifecycleTests.cs`、`backend/src/Gaode.Application/Ports/IReservedIntegrations.cs`、`backend/src/Gaode.Infrastructure/Integrations/NotIntegratedPorts.cs`。
  - 类型：实现；前置：T009、T014、T015、T017、T018、T019、T021、T022、T023、T024、T025。
  - 追溯：FR-006、FR-025、FR-031、FR-032、FR-033、FR-038；REQ：CTL-003、SAF-011、NFR-007、RCP-001、RCP-005、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、DAT-007、ACQ-010、DAT-008、NFR-004；宪章：P01、P02、P04、P05、P07、P08、P09、P10；场景：SV-05、SV-16、SV-17、SV-19、SV-28、SV-34。
  - 完成条件：loopback/Test模式显式绑定进程内适配，共用单一TimeProvider；运行锁/只读结构核验先于业务就绪，不静默建库。未完成运行仅RecoveryRequired，退出收敛控制/保存/租约再释放；真实适配通过后续T049接入验证，缺实现明确NotIntegrated。预留MES/模型/样本仅状态空接口，无业务入口。
  - 验证证据：隔离Test根Host启动/缺库/持锁/未完成记录/停止生命周期测试；证明全模拟不启动Python或访问真实设备。 归档至上述T030证据目录。

- [x] T031 [US1] 实现启动上下文、请求幂等与配置准备；产物：`backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`、`backend/src/Gaode.Application/Station01/CommandRegistry.cs`、`backend/src/Gaode.Application/Station01/StartupReadiness.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/StartUseCaseTests.cs`。
  - 类型：实现；前置：T010、T014、T015、T022、T030。
  - 追溯：FR-001、FR-002、FR-003、FR-004、FR-006、FR-021、FR-039；REQ：TASK-002、TASK-003、CTL-004、TASK-009、CTL-008、RCP-001、RCP-002、POS-001、RCP-005、DAT-001、CTL-003、SAF-011、NFR-007、DAT-007、SAF-010、DAT-008；宪章：P02、P03、P04、P05、P06、P07、P08、P09、P10、P11；场景：SV-01、SV-05、SV-07、SV-10、SV-15、SV-17、SV-35。
  - 完成条件：先建Run/Command，全部3D/F运动采集配置校验后冻结/提交。主体/requestId/kind/scope幂等，同键异内容409、未闭环拒重入；算法缺失单列。配置失败保留可查询上下文并拒绝用continue替换无效快照；接收T014已有控制锁存信号后停止新准入。本任务不实现/验证完整CancelRun及持久取消后新建，该业务要求由T044/T048完成，不反向依赖它们。
  - 验证证据：同键重发、异内容、竞争运行、缺F、算法不可用、快照未提交及基础控制信号关闭准入的合同测试/设备计数；不调用尚未实现的CancelRun API作为本任务完成条件。配置受限取消并新请求启动证据归T048。归档至上述T031证据目录。

- [x] T032 [US1] 实现软件启动、实体启动和夹紧确认步骤；产物：`backend/src/Gaode.Application/Station01/Steps/StartClampStep.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/StartClampStepTests.cs`。
  - 类型：实现；前置：T015、T017、T022、T031。
  - 追溯：FR-001、FR-007、FR-008、FR-010、FR-017、FR-021、FR-034；REQ：TASK-002、TASK-003、CTL-004、SYS-004、CTL-005、SYS-006、ACQ-002、ACQ-008、CTL-008、CTL-010、TASK-007、TASK-008、ALG-008、DAT-001、DAT-007、SAF-010、CTL-007；宪章：P03、P04、P05、P06、P07、P08、P09；场景：SV-01、SV-08、SV-09、SV-27、SV-28。
  - 完成条件：Start及PLC内部夹紧观察意图先提交；匹配受理/安全/实体按钮/夹紧及期限，不自动模拟按钮。等待按钮时现有状态查询和T014控制信号可处理，锁存后不推进依赖步骤；无ACK/旧夹紧越门或新增回零。只验证该信号边界，完整取消API/持久闭环和等待按钮取消交T044/T048，不复制取消逻辑。
  - 验证证据：事件序列/保存先后、无按钮/未夹紧时3D动作=0、控制信号受理不等设备完成；无需完整CancelRun才能通过。归档至上述T032证据目录。

- [x] T033 [US1] 实现整盘3D固定XY、采集和高度结果归约；产物：`backend/src/Gaode.Application/Station01/Steps/ThreeDStep.cs`、`backend/src/Gaode.Domain/Station01/HeightResult.cs`、`backend/src/Gaode.Domain/Station01/HeightValidity.cs`、`backend/tests/Gaode.Rules.Tests/Station01/HeightResultTests.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/ThreeDStepTests.cs`。
  - 类型：实现；前置：T024、T025、T032。
  - 追溯：FR-009、FR-010、FR-011、FR-012、FR-014、FR-016、FR-017、FR-021、FR-022、FR-030；REQ：ID-006、CTL-006、CTL-010、POS-001、ACQ-002、ACQ-008、CTL-008、ID-005、POS-002、POS-003、ALG-013、SAF-005、SAF-011、TASK-003、ACQ-005、ALG-008、TASK-007、TASK-008、DAT-001、DAT-007、SAF-010、DAT-002、ACQ-010、SAF-008、SAF-009；宪章：P03、P04、P06、P07、P08、P09、P10；场景：SV-01、SV-06、SV-13、SV-17、SV-18、SV-19、SV-23、SV-24。
  - 完成条件：3D移动意图→匹配事实→Capture意图→整盘采集/媒体提交→提供Run/Capture/Call/Operation/Attempt、媒体引用、公共/scope/参数/算法版本及调用依据给T025→原期限内AlgorithmIntent提交后高度派发→结果保存。不得绕过提交门或重建Call预算；多项高度保留原标识/单位/基准/有效性，无Part/Face/槽映射、同高假设或Z/扫描动作。
  - 验证证据：高度规则与多样本/scope/媒体到位门；检查AlgorithmIntent字段关联和真实派发先后，保存失败/未知/保存中到期Execute=0。原始结果及终态提交后才允许F；SQLite中断详见T053/T054。归档至上述T033证据目录。

- [x] T034 [US1] 实现F原始码去重和受限解析规则；产物：`backend/src/Gaode.Domain/Station01/FCodeResult.cs`、`backend/src/Gaode.Domain/Station01/FCodePolicy.cs`、`backend/src/Gaode.Application/Capabilities/TestTrayCodePolicy.cs`、`backend/tests/Gaode.Rules.Tests/Station01/FCodePolicyTests.cs`。
  - 类型：实现；前置：T006、T009。
  - 追溯：FR-013、FR-015、FR-016、FR-017、FR-025；REQ：ID-001、RCP-001、CTL-010、POS-003、TASK-003、ID-005、ACQ-005、ALG-008、TASK-007、TASK-008、RCP-005；宪章：P01、P02、P04、P07；场景：SV-02、SV-04、SV-26。
  - 完成条件：保留原始重复候选，Ordinal去重；null响应≠空集合，多个不同值Conflict且无主码，先判冲突后解析。唯一值无规则NotDefined、已定义不符InvalidFormat；Test解析器仅TEST-TRAY-四ASCII数字，不查产品/配方，不按置信/区域择码。
  - 验证证据：参数化原文大小写/字符差异、重复/多值、无响应/无码、未知规则/格式错误测试，质量始终未判定。 归档至上述T034证据目录。

- [x] T035 [US1] 实现固定XY的F单拍、调用和保存步骤；产物：`backend/src/Gaode.Application/Station01/Steps/FScanStep.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/FScanStepTests.cs`。
  - 类型：实现；前置：T024、T025、T033、T034。
  - 追溯：FR-009、FR-010、FR-013、FR-014、FR-015、FR-016、FR-018、FR-021、FR-022、FR-025、FR-030；REQ：ID-006、CTL-006、CTL-010、POS-001、ACQ-002、ACQ-008、CTL-008、ID-001、RCP-001、ALG-013、SAF-005、SAF-011、POS-003、TASK-003、ID-005、ACQ-005、ALG-008、ALG-014、DAT-001、DAT-007、SAF-010、DAT-002、RCP-005、TASK-007、ACQ-010、SAF-008、SAF-009；宪章：P01、P02、P03、P04、P06、P07、P08、P09；场景：SV-01、SV-02、SV-04、SV-11、SV-17、SV-18、SV-19、SV-24、SV-25、SV-26。
  - 完成条件：3D已收敛且必要保存/安全满足才进入F；固定XY/采集意图保存门。唯一Capture单次单张；复用T025独立AlgorithmIntent门，F Call/Operation/Attempt、Run/Capture/输入及版本/依据完整保存后在原期限内派发。原始响应/候选终态保存，失败或提交未知不派发，不重置期限、不重拍或借旧码。
  - 验证证据：成功/候选异常/重复帧及Call意图失败/未知/保存中到期合同用例，Capture/Execute计数、原Call关联和零配方调用；保存/中断集成归T053/T054。归档至上述T035证据目录。

- [x] T036 [US1] 实现完成门和不可变公共移交；产物：`backend/src/Gaode.Domain/Station01/CompletionPolicy.cs`、`backend/src/Gaode.Application/Station01/CompletePublicPreparation.cs`、`backend/src/Gaode.Application/Station01/StageHandoffBuilder.cs`、`backend/tests/Gaode.Rules.Tests/Station01/CompletionPolicyTests.cs`。
  - 类型：实现；前置：T022、T033、T035。
  - 追溯：FR-017、FR-018、FR-019、FR-021、FR-023、FR-024、FR-025、FR-030、FR-039；REQ：TASK-007、TASK-008、ALG-008、CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-010、RCP-001、RCP-005、ACQ-010、DAT-008；宪章：P01、P02、P04、P06、P07、P08、P09、P10；场景：SV-01、SV-03、SV-04、SV-12、SV-18、SV-19、SV-28、SV-35。
  - 完成条件：核对Start/按钮/夹紧/两次XY、采集/算法允许终态及必要保存，无未知物理依赖；构造ExpectedRevision/TerminalOutcome=None完成候选，复用T022同事务保存Run与唯一Handoff，确证Committed才Ready。条件拒绝读取实际终态，未知按原WriteId核对；不凭内存SavingHandoff认定完成。只输出实际信息；物理占用不释放、不松夹/卸料/下一盘。
  - 验证证据：缺证据拒Ready、两种完成、条件拒绝/未知不发布Ready及稳定快照；验证持久终态前置接口，不要求T044完整取消集成先通过，竞争全链在T048/T053/T054。归档至上述T036证据目录。

- [x] T037 [US1] 提供启动、查询、媒体读取、后端认证与通知入口；产物：`backend/src/Gaode.Host/Api/RunEndpoints.cs`、`backend/src/Gaode.Host/Api/QueryEndpoints.cs`、`backend/src/Gaode.Host/Api/MediaEndpoints.cs`、`backend/src/Gaode.Host/Api/Station01Hub.cs`、`backend/src/Gaode.Host/Api/TestAuthenticationHandler.cs`、`backend/src/Gaode.Host/Api/Station01Authorization.cs`、`backend/src/Gaode.Infrastructure/Diagnostics/StructuredStageDiagnostics.cs`、`backend/tests/Gaode.Integration.Tests/Api/StartQueryTests.cs`。
  - 类型：实现；前置：T003、T030、T031、T036。
  - 追溯：FR-001、FR-002、FR-017、FR-024、FR-026、FR-027、FR-031；REQ：TASK-002、TASK-003、CTL-004、TASK-009、CTL-008、TASK-007、TASK-008、ALG-008、DAT-007、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P05、P06、P07、P08、P09；场景：SV-01、SV-10、SV-16、SV-20、SV-22。
  - 完成条件：按API合同实现202和durability、命令/运行/status/handoff查询、ETag及受控mediaId读取；四类角色Test令牌映射，Production无正式身份拒相应控制，无角色Header旁路。中文结构化错误/审计和有界SignalR小消息；慢通知不占控制路径。
  - 验证证据：HTTP状态码/权限/请求断线不取消运行、未Ready返回409、通知错序重查及路径拒绝的集成证据。 归档至上述T037证据目录。

- [x] T038 [US1] 验证使用真实SQLite和媒体文件的最小正常闭环；产物：`backend/tests/Gaode.Integration.Tests/Station01/NormalPublicPreparationTests.cs`、`backend/tests/Gaode.Integration.Tests/Support/Station01HostFixture.cs`。
  - 类型：验证；前置：T003、T013、T017、T018、T019、T021、T030、T031、T032、T033、T034、T035、T036、T037。
  - 追溯：FR-001、FR-007、FR-009、FR-010、FR-011、FR-013、FR-016、FR-017、FR-021、FR-022、FR-024、FR-025、FR-033、FR-034；REQ：TASK-002、TASK-003、CTL-004、SYS-004、ID-006、CTL-006、CTL-010、POS-001、ACQ-002、ACQ-008、CTL-008、ID-005、POS-002、ID-001、RCP-001、ACQ-005、ALG-008、TASK-007、TASK-008、DAT-001、DAT-007、SAF-010、DAT-002、RCP-005、NFR-007、ACQ-010、CTL-007；宪章：P01、P02、P03、P04、P05、P06、P07、P08、P09；场景：SV-01、SV-02、SV-23、SV-25、SV-26、SV-28。
  - 完成条件：normal实际时间及controlled-normal规则/端口共同验证；显式按钮、两个高度/F单拍/未定义解析；实际隔离SQLite和成品媒体，复用延迟/有界/保存能力。包括两次AlgorithmIntent先提交后派发及Run/Handoff原子提交正常证据。只证明M1正常闭环，不前置完整CancelRun、取消竞争或恢复证据；这些是M2必做。
  - 验证证据：保存完整请求/事件/动作/媒体/提交/移交轨迹，证实停止在第23步后；正常时序与未知解析限制均可查。 归档至上述T038证据目录。

## 4. US2：算法失败后有限结束并继续安全步骤（优先级：P1）

**目标**：调用者无需等待算法修复，即可取得高度或扫码的明确异常；后端在真实控制条件满足时继续独立步骤并保存移交结果。

**独立完成条件**：T039至T043证明每种算法异常有限收敛，必要安全/保存条件满足才继续F或异常移交。

- [ ] T039 [US2] 将算法全部有限终态接入公共步骤出口；产物：`backend/src/Gaode.Application/Station01/AlgorithmOutcomePolicy.cs`、`backend/src/Gaode.Application/Station01/Steps/ThreeDStep.cs`、`backend/src/Gaode.Application/Station01/Steps/FScanStep.cs`、`backend/tests/Gaode.Rules.Tests/Station01/AlgorithmOutcomePolicyTests.cs`。
  - 类型：实现；前置：T025、T033、T035。
  - 追溯：FR-012、FR-014、FR-015、FR-020、FR-029、FR-040；REQ：ID-006、POS-002、POS-003、ALG-013、SAF-005、SAF-011、CTL-010、ID-001、ACQ-006、ACQ-010、RCP-002、NFR-007、CTL-007、ACQ-005、ALG-014；宪章：P01、P04、P06、P07、P08、P09、P10、P11；场景：SV-03、SV-04、SV-05、SV-06、SV-29、SV-30。
  - 完成条件：把Error/TimedOut/NotConfigured/NotIntegrated/NotReady/NoResult/InvalidResult/DependencyFailed/Cancelled逐项归约；原始输出实际取得才留存。缺预算不发调用、无高度不填0；可靠Ended内容异常与硬件未知区分，未开展质量判定。
  - 验证证据：技术终态和采集有效性组合表测试；算法持续失败不转设备停机门槛，取消不误移交。 归档至上述T039证据目录。

- [ ] T040 [US2] 实现算法异常后的保存门和安全继续判定；产物：`backend/src/Gaode.Application/Station01/ContinuationPolicy.cs`、`backend/src/Gaode.Application/Station01/Station01Coordinator.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/AlgorithmContinuationTests.cs`。
  - 类型：实现；前置：T015、T022、T036、T039。
  - 追溯：FR-012、FR-014、FR-015、FR-020、FR-021、FR-023、FR-024、FR-040；REQ：ID-006、POS-002、POS-003、ALG-013、SAF-005、SAF-011、CTL-010、ID-001、ACQ-006、ACQ-010、DAT-001、DAT-007、SAF-010、DAT-010、TASK-007、TASK-008、CTL-007、ACQ-005、ALG-014；宪章：P01、P02、P04、P06、P07、P08、P09、P10；场景：SV-03、SV-04、SV-05、SV-13、SV-17、SV-18、SV-29、SV-30。
  - 完成条件：高度失败先保存原关联和继续依据，F独立XY/安全/资源/保存均满足才发；F异常已保存且物理条件完整才允许异常移交。不等Worker回收；PLC/采集未知或保存失败仍受限，禁止把所有失败统一放行。
  - 验证证据：高度异常×安全/保存/资源条件对照；F空码/冲突/格式错误/超时均不重拍；受限处置可解释。 归档至上述T040证据目录。

- [ ] T041 [US2] 验证算法终态、无效高度与F结果规则矩阵；产物：`backend/tests/Gaode.Contracts.Tests/Station01/AlgorithmFailureContractTests.cs`、`backend/tests/Gaode.Rules.Tests/Station01/AlgorithmFailureMatrixTests.cs`。
  - 类型：验证；前置：T034、T039、T040。
  - 追溯：FR-011、FR-012、FR-013、FR-014、FR-015、FR-016、FR-020、FR-029；REQ：ID-005、POS-002、ID-006、POS-003、ID-001、RCP-001、ALG-013、SAF-005、SAF-011、CTL-010、TASK-003、ACQ-005、ALG-008、ACQ-006、ACQ-010、RCP-002、NFR-007；宪章：P01、P02、P03、P04、P06、P07、P09、P10、P11；场景：SV-02、SV-03、SV-04、SV-05、SV-06、SV-13、SV-26。
  - 完成条件：覆盖无结果/非法值/缺元信息/真实未接入、缺预算、不同码中只有一码格式合法也不筛选；超时通过DeadlineScheduler产生，格式错误仅基于已定义规则。
  - 验证证据：每行输入、终态、原始证据、height空/无主码以及是否允许F/移交断言，纳入共同边界断言。 归档至上述T041证据目录。

- [ ] T042 [US2] 验证算法超期、不响应及安全继续的集成保存；产物：`backend/tests/Gaode.Integration.Tests/Station01/AlgorithmFailureIntegrationTests.cs`。
  - 类型：验证；前置：T021、T038、T040、T041。
  - 追溯：FR-012、FR-014、FR-015、FR-018、FR-021、FR-022、FR-024、FR-027、FR-036、FR-040；REQ：ID-006、POS-002、POS-003、ALG-013、SAF-005、SAF-011、CTL-010、ID-001、CTL-008、ACQ-005、ALG-014、DAT-001、DAT-007、SAF-010、DAT-002、TASK-007、TASK-008、NFR-004、NFR-007、CTL-007、ACQ-010；宪章：P01、P02、P04、P06、P07、P08、P09、P10；场景：SV-03、SV-04、SV-05、SV-29、SV-30。
  - 完成条件：对两个角色分别Error/超期/NoResponse/NotIntegrated并含连续失败运行；配置与场景初态显式隔离，不通过完成标志释放旧盘。实际时间Host+SQLite保存原始/异常/继续依据，高度挂起仍能使用F槽及预留媒体。
  - 验证证据：在期限内形成业务终态而非等响应，带异常移交记录；必要保存/安全不满足时F被拒；F触发≤1。 归档至上述T042证据目录。

- [ ] T043 [US2] 验证算法隔离、迟到和媒体租约回收；产物：`backend/tests/Gaode.Contracts.Tests/Algorithms/AlgorithmIsolationTests.cs`、`backend/tests/Gaode.Integration.Tests/Media/WorkerLeaseRecoveryTests.cs`。
  - 类型：验证；前置：T023、T025、T042。
  - 追溯：FR-018、FR-022、FR-023、FR-027、FR-037、FR-040；REQ：CTL-008、ACQ-005、ALG-014、DAT-001、DAT-002、DAT-007、DAT-010、NFR-004、NFR-007、SAF-011、SAF-003、SAF-004、CTL-007、ACQ-010；宪章：P04、P06、P07、P08、P09；场景：SV-11、SV-20、SV-29、SV-30、SV-31、SV-33。
  - 完成条件：故障Height输入仍被消费、Worker失联/退出/InputReleased分别处理；F独立槽不被占、容量仍有界。10秒观察窗口不能强删在用文件，超时/取消不假定进程退出，迟到不改Handoff。
  - 验证证据：真实媒体租约/原终态和移交摘要前后比较，退出/释放证据及容量计数。 归档至上述T043证据目录。

## 5. US3：设备、安全、身份与取消均有真实边界（优先级：P1）

**目标**：调用者能区别等待实体操作、动作受理、动作完成和受限状态；重复请求、迟到事件及取消不能产生额外物理动作。

**独立完成条件**：T044至T049证明未知物理状态、重复/迟到/取消与权限入口不造成额外动作；真实适配未接入仍明确受限。

- [ ] T044 [US3] 实现暂停、取消及控制API；产物：`backend/src/Gaode.Application/Station01/PauseRun.cs`、`backend/src/Gaode.Application/Station01/CancelRun.cs`、`backend/src/Gaode.Host/Api/ControlEndpoints.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/PauseCancelTests.cs`。
  - 类型：实现；前置：T014、T015、T022、T037、T040。
  - 追溯：FR-017、FR-018、FR-019、FR-021、FR-024、FR-026、FR-027、FR-030、FR-031、FR-037；REQ：TASK-007、TASK-008、ALG-008、CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011、ACQ-010、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-09、SV-12、SV-18、SV-19、SV-22、SV-24、SV-33。
  - 完成条件：暂停只停新步骤，在途期限继续；cancel立即关准入/继续资格并独立请求所需停止，不等审计/终态写盘。区分requestAccepted/admissionClosed/stopState/terminalDecision，未决applied=null；可靠停止和必要保存后提交revision/None取消候选，持久完成先胜则applied=false，持久Cancelled才true。复用T022/T036已有条件合同，CommitUnknown先核对WriteId；幂等、授权、版本检查不变。实现配置受限未动作运行取消保存后新请求建立及等待按钮取消，不在T031/T032复制逻辑。
  - 验证证据：各阶段pause/cancel/HTTP断线/重复控制、未决/完成先胜/取消胜出合同测试，StopPending≠Stopped，状态查询可用；配置受限取消及按钮等待取消完整证据在T048，SQLite交叉/重启在T048/T053/T054。归档至上述T044证据目录。

- [ ] T045 [US3] 落实运行层事件去重、乱序和迟到证据隔离；产物：`backend/src/Gaode.Application/Station01/OperationEvidenceReducer.cs`、`backend/src/Gaode.Application/Station01/LateEvidencePolicy.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/EventCorrelationTests.cs`。
  - 类型：实现；前置：T012、T014、T022、T036、T044。
  - 追溯：FR-002、FR-016、FR-018、FR-019、FR-024、FR-027、FR-040；REQ：TASK-009、CTL-008、TASK-003、ID-005、ACQ-005、ALG-008、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、TASK-007、TASK-008、DAT-007、NFR-004、NFR-007、SAF-011、ACQ-010；宪章：P02、P04、P06、P07、P08、P09；场景：SV-10、SV-11、SV-12、SV-31。
  - 完成条件：消费Ingress裁决仍核对Run/Operation/Attempt/Session/connectionEpoch、阶段及版本；有效终态只一次。晚到物理证据独立ResolutionEvidence，未知关联隔离；有界明细与重复计数，不解除Held、不产生新执行机会。
  - 验证证据：重复/倒序/跨Run/连接重建/结束及取消后的事件矩阵；终态、Handoff及动作次数不变。 归档至上述T045证据目录。

- [ ] T046 [US3] 落实设备/采集故障和动作未知的流程限制；产物：`backend/src/Gaode.Application/Station01/PhysicalFaultPolicy.cs`、`backend/src/Gaode.Application/Station01/Station01Coordinator.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/PhysicalFaultTests.cs`。
  - 类型：实现；前置：T015、T024、T032、T040、T044、T045。
  - 追溯：FR-006、FR-008、FR-010、FR-019、FR-020、FR-026、FR-040；REQ：CTL-003、SAF-011、NFR-007、CTL-005、SYS-006、ACQ-002、ACQ-008、CTL-008、CTL-010、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、ACQ-006、ACQ-010、HMI-004、HMI-007、DAT-008、ACQ-005、ALG-014；宪章：P02、P03、P04、P05、P06、P07、P08、P09、P10；场景：SV-08、SV-09、SV-13、SV-32。
  - 完成条件：PLC断联、安全失效、ACK无到位、采集结束未知、光源/相机失败分别受限；动作到期Unknown/Held，不借算法继续规则。迟到Stopped不等于目标完成；恢复连接只更新观察，必要停止语义未接入明确限制。
  - 验证证据：按sequences动作未知路径检查禁止依赖采集/F/移交；可靠Ended但内容异常对照可有限继续。 归档至上述T046证据目录。

- [ ] T047 [US3] 验证配置准入、实体反馈和真实用途拒绝；产物：`backend/tests/Gaode.Integration.Tests/Station01/StartupSafetyTests.cs`。
  - 类型：验证；前置：T010、T017、T031、T032、T037、T046。
  - 追溯：FR-003、FR-005、FR-006、FR-007、FR-008、FR-009、FR-010、FR-020、FR-029、FR-031；REQ：RCP-001、RCP-002、POS-001、NFR-007、CTL-003、SAF-011、SYS-004、CTL-004、CTL-005、SYS-006、ID-006、CTL-006、CTL-010、ACQ-002、ACQ-008、CTL-008、ACQ-006、ACQ-010、ALG-013、SEC-001、SEC-003、SEC-006、SYS-003；宪章：P02、P03、P04、P05、P06、P07、P08、P09、P10、P11；场景：SV-05、SV-07、SV-08、SV-09、SV-13、SV-14、SV-16。
  - 完成条件：逐一移除3D/F点/范围/必要采集参数，另测仅算法参数缺失；不按按钮、夹紧Hold、安全失效、Real入口Test配置和未注册能力。无必需配置时PLC/夹紧/XY/触发全0，算法缺失不阻独立启动。
  - 验证证据：真实Host错误/状态记录和假真实边界发送计数；断联/过期反馈不越过动作门。 归档至上述T047证据目录。

- [ ] T048 [US3] 验证重复请求、取消、错序与迟到不重复动作；产物：`backend/tests/Gaode.Integration.Tests/Station01/IdempotencyCancellationTests.cs`。
  - 类型：验证；前置：T038、T044、T045、T046。
  - 追溯：FR-002、FR-013、FR-017、FR-018、FR-019、FR-021、FR-024、FR-025、FR-030、FR-037、FR-040；REQ：TASK-009、CTL-008、ID-001、RCP-001、TASK-007、TASK-008、ALG-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、RCP-005、ACQ-010、NFR-004、SAF-011；宪章：P01、P02、P04、P06、P07、P08、P09；场景：SV-07、SV-08、SV-10、SV-11、SV-12、SV-18、SV-19、SV-25、SV-31、SV-33。
  - 完成条件：重复/冲突请求及各阶段取消、跨会话帧/迟到；完整验证配置受限运行取消提交后才可新建、等待实体按钮取消后不夹紧/派3D。复用T022屏障，以实际SQLite覆盖保存合同§1.2：完成排队未提交、已提交未回执、CommitUnknown、取消胜出后旧写/回执；控制候选投递时机验证两种胜出，同时遵守Writer处理顺序；取消先胜用旧完成候选稍后投递，不要求取消越过已在途写事务。未知不提前双终态、不重发动作，F不二次Call。中断重启扩展交T054。
  - 验证证据：原Run/Command/Handoff/WriteId、revision、实际SQLite提交与回执时序；API取消applied/过渡状态、内存投影、持久终态一致，立即关准入且停止另确认。SV-07/08完整取消分支在此归档，T031/T032无需反向依赖。归档至上述T048证据目录。

- [ ] T049 [US3] 验证通用真实适配边界并接入Host安全绑定；产物：`backend/src/Gaode.Host/Composition/AdapterBindings.cs`、`backend/tests/Gaode.Contracts.Tests/Devices/RealAdapterBoundaryTests.cs`、`backend/tests/Gaode.Contracts.Tests/Algorithms/WorkerAdapterBoundaryTests.cs`、`backend/tests/Gaode.Integration.Tests/Hosting/AdapterBindingTests.cs`。
  - 类型：验证；前置：T026、T027、T028、T029、T030、T045、T046。
  - 追溯：FR-018、FR-019、FR-020、FR-027、FR-031、FR-033、FR-040；REQ：CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、ACQ-006、ACQ-010、SAF-011、NFR-004、NFR-007、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-09、SV-11、SV-13、SV-16、SV-31、SV-32、SV-33。
  - 完成条件：以假PLC传输/SDK网关/Worker进程验证相同端口语义与受控生命周期，接入显式Real/Hybrid绑定但缺映射/组件仍NotIntegrated。证明无生产故障自动切模拟、无SDK回调阻塞/推理/存图；不要求现场才能完成通用适配测试。
  - 验证证据：模拟与通用适配运行同一合同套件，逐项记假边界与未接入厂商实现；不宣称真实通信/光学/算法通过。 归档至上述T049证据目录。

## 6. US4：配置、保存与中断状态可信（优先级：P1）

**目标**：调用者能确定当前运行使用哪个公共配置版本、哪些事实已经保存，以及恢复前还需核对什么；测试参数不能进入真实运动。

**独立完成条件**：T050至T056证明版本、实际SQLite/文件、保存边界及经授权核对的恢复复用可信。

- [ ] T050 [US4] 完成配置校验API及版本替换隔离验证；产物：`backend/src/Gaode.Host/Api/ConfigurationEndpoints.cs`、`backend/tests/Gaode.Integration.Tests/Configuration/ConfigurationSnapshotTests.cs`。
  - 类型：实现；前置：T010、T031、T037、T047。
  - 追溯：FR-003、FR-004、FR-005、FR-028、FR-029、FR-039；REQ：RCP-001、RCP-002、POS-001、RCP-005、DAT-001、NFR-007、RCP-003、ALG-013、DAT-008；宪章：P03、P04、P07、P08、P09、P10、P11；场景：SV-07、SV-14、SV-15、SV-16、SV-21、SV-35。
  - 完成条件：只校验不生效/不发动作，报告blockingControlErrors和algorithmIssues；两版合法点位/绑定/参数仅改配置，当前快照稳定，新运行按新版本；F单拍/阶段终点不可改写。同版本内容冲突、脚本、Test进入Real均拒绝。
  - 验证证据：API报告、配置差异、两独立Test初态流程轨迹/冻结JSON及无Coordinator改动证据。 归档至上述T050证据目录。

- [ ] T051 [US4] 实现必要保存失败与提交/文件核对流程；产物：`backend/src/Gaode.Application/Station01/PersistenceFailurePolicy.cs`、`backend/src/Gaode.Infrastructure/Persistence/RecoveryEvidenceReader.cs`、`backend/src/Gaode.Infrastructure/Media/MediaReconciler.cs`、`backend/tests/Gaode.Contracts.Tests/Persistence/SaveBoundaryTests.cs`。
  - 类型：实现；前置：T022、T023、T036、T045、T046。
  - 追溯：FR-017、FR-018、FR-019、FR-021、FR-022、FR-023、FR-024、FR-030、FR-032；REQ：TASK-007、TASK-008、ALG-008、CTL-008、ACQ-005、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-002、DAT-010、ACQ-010、NFR-007；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-12、SV-17、SV-18、SV-19、SV-20。
  - 完成条件：必要保存失败/CommitUnknown关闭依赖准入，按原WriteId/事务状态、意图、文件manifest核对；包含AlgorithmIntent和竞争终态批次，查不到记录且旧写者在途不能判失败。ConditionRejected按实际版本/终态处理；已存完成/取消为终态权威，投影不回退。成品补存受控，缺文件/内存丢失如实受限，晚提交不自动恢复，不重做机械动作。
  - 验证证据：所有保存阶段回执及文件间隙的合同测试，诊断明确最后持久边界和必要补存。 归档至上述T051证据目录。

- [ ] T052 [US4] 实现恢复核对、步骤复用及受控继续API；产物：`backend/src/Gaode.Domain/Station01/RecoveryPolicy.cs`、`backend/src/Gaode.Application/Station01/RecoveryCheckUseCase.cs`、`backend/src/Gaode.Application/Station01/ContinueRun.cs`、`backend/src/Gaode.Host/Api/RecoveryEndpoints.cs`、`backend/tests/Gaode.Rules.Tests/Station01/RecoveryPolicyTests.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/RecoveryUseCaseTests.cs`。
  - 类型：实现；前置：T031、T044、T045、T046、T051。
  - 追溯：FR-002、FR-014、FR-016、FR-018、FR-019、FR-021、FR-022、FR-024、FR-030、FR-031；REQ：TASK-009、CTL-008、ALG-013、SAF-005、SAF-011、TASK-003、ID-005、ACQ-005、ALG-008、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-002、TASK-007、TASK-008、ACQ-010、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-17、SV-18、SV-19、SV-22、SV-24。
  - 完成条件：授权核对同盘/装载/原快照/设备/保存，持久Check后显式continue并复查。算法意图提交前、提交后未派发、派发结果未保存按合同§1.1核对；保存意图不证明派发，缺派发记录不证明未发。已登记原Call未终结记Interrupted及派发未知证据，不重算/重拍/重置预算；完整终态复用。先核对Run/Handoff及竞争WriteId，已持久取消请求仍关准入，最终取消/完成不可复活；仅可证明未发起步骤受控开始。
  - 验证证据：正常3D复用→F未发起；Call三窗口缺失/未知证据及未决取消拒continue；人工true不能替代物理/提交事实，核对过期/装载变化/缺快照拒绝；规则/合同明确补存依据。归档至上述T052证据目录。

- [ ] T053 [US4] 验证真实SQLite、文件和保存超时故障矩阵；产物：`backend/tests/Gaode.Integration.Tests/Storage/CriticalPersistenceFailureTests.cs`、`backend/tests/Gaode.Integration.Tests/Media/MediaCommitGapTests.cs`。
  - 类型：验证；前置：T021、T022、T023、T038、T051。
  - 追溯：FR-014、FR-016、FR-017、FR-018、FR-019、FR-021、FR-022、FR-023、FR-024、FR-026、FR-030、FR-032；REQ：ALG-013、SAF-005、SAF-011、TASK-003、ID-005、ACQ-005、ALG-008、TASK-007、TASK-008、CTL-008、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-002、DAT-010、HMI-004、HMI-007、DAT-008、ACQ-010、NFR-007；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-12、SV-17、SV-18、SV-19、SV-20、SV-24。
  - 完成条件：实际SQLite/媒体覆盖Run/快照/动作/Capture意图、文件/元数据、AlgorithmIntent、算法结果及移交故障点；复用T022提交/回执屏障。Call意图失败或未知Execute=0，保存期间原算法到期、晚提交也不Execute，预算不重置。对完成/取消两候选控制提交前后、回执丢失/延迟、CommitUnknown及revision竞争，验证条件事务原子性；无需内存替身代替实际事务。
  - 验证证据：每点记录输入、屏障位置、原WriteId/Call/Attempt、实际SQLite行/约束、文件和Execute/动作计数；两类胜出均核对Run/Handoff及API，不双终态。仅查不到尚未提交写时维持未知；物理停止/必要保存未满足不能最终取消。归档至上述T053证据目录。

- [ ] T054 [US4] 验证暂停、进程中断、重连及核对复用；产物：`backend/tests/Gaode.Integration.Tests/Recovery/InterruptedRunTests.cs`、`backend/tests/Gaode.Integration.Tests/Recovery/RecoveryReuseTests.cs`。
  - 类型：验证；前置：T021、T038、T044、T048、T051、T052、T053。
  - 追溯：FR-002、FR-014、FR-016、FR-017、FR-018、FR-019、FR-021、FR-022、FR-024、FR-030、FR-032；REQ：TASK-009、CTL-008、ALG-013、SAF-005、SAF-011、TASK-003、ID-005、ACQ-005、ALG-008、TASK-007、TASK-008、ALG-014、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-007、SAF-010、DAT-002、ACQ-010、NFR-007；宪章：P02、P04、P05、P06、P07、P08、P09；场景：SV-10、SV-11、SV-12、SV-17、SV-18、SV-19、SV-24。
  - 完成条件：按保存合同§3全部间隙及§1.1三类调用、§1.2五类终态竞争窗口参数化中断并重建Host；保留实际Test库/媒体和独立模拟设备状态。覆盖真实事务提交前回滚、提交后回执丢失、未知提交核对、取消后旧写/回执及仅内存受理取消未保存。已存Call/结果复用或Interrupted不重算，持久终态唯一且重启查询一致；未决核对前0自动动作，不复活终态。
  - 验证证据：原Call意图/派发依据与输入/版本/期限、WriteId/事务事实、Run/Handoff/取消命令、API重启前后对照、Check/Resolution/复用引用及触发计数；未知派发不重算，缺文件/换盘/未知动作/重复continue受限。归档至上述T054证据目录。

- [ ] T055 [US4] 验证容量、慢保存/通知和资源回收；产物：`backend/tests/Gaode.Integration.Tests/Resources/BoundedCapacityTests.cs`、`backend/tests/Gaode.Integration.Tests/Resources/SlowConsumerTests.cs`。
  - 类型：验证；前置：T014、T015、T023、T025、T037、T043、T044、T051。
  - 追溯：FR-019、FR-022、FR-023、FR-026、FR-027、FR-032、FR-037；REQ：CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-001、DAT-002、DAT-007、DAT-010、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011；宪章：P04、P05、P06、P07、P08、P09；场景：SV-12、SV-18、SV-20、SV-29、SV-33。
  - 完成条件：普通队列/Writer/媒体配额/算法槽/通知慢分别注入；已受理终态有保留路径，F预留容量有效，停止/心跳/查询不等磁盘。保留真实提交/文件租约区别，无无限Task/线程或丢必要数据后继续。
  - 验证证据：配置上限与最大实测占用、终态计数、控制先于慢响应事件、内存观察与持久状态及媒体释放依据。 归档至上述T055证据目录。

- [ ] T056 [US4] 验证权限、中文诊断、审计与查询通知一致性；产物：`backend/tests/Gaode.Integration.Tests/Api/AuthorizationAuditTests.cs`、`backend/tests/Gaode.Integration.Tests/Api/DiagnosticsNotificationTests.cs`。
  - 类型：验证；前置：T037、T044、T050、T052、T053、T054、T055。
  - 追溯：FR-017、FR-024、FR-026、FR-027、FR-031、FR-032；REQ：TASK-007、TASK-008、ALG-008、DAT-007、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；宪章：P02、P05、P06、P07、P08、P09；场景：SV-09、SV-18、SV-20、SV-22、SV-24。
  - 完成条件：四角色对每个控制入口逐项允许/拒绝，Recovery/Continue和配置校验权限不混淆；Test令牌不用于Production，未授权零动作。所有异常包含身份/阶段/错误码/版本/保存及继续去向；丢/乱序通知靠revision重查不改变业务。
  - 验证证据：API响应、SQLite操作审计、中文错误及结构化日志字段，慢通知下查询/控制轨迹；无任意文件路径和角色Header越权。 归档至上述T056证据目录。

## 7. US5：观察模拟耗时并验证期限与响应能力（优先级：P1）

**目标**：开发与验证人员可以独立设置设备、采集和算法的模拟耗时，观察等待中的真实业务状态，并用可控时钟重复验证正常、超时及迟到路径；无需独立虚拟下位机服务。

**独立完成条件**：T057至T063证明七环节延迟、双时钟、非阻塞控制、时间证据和既有工艺边界；不能只观察最终成功。

- [ ] T057 [US5] 建立可追溯的七环节故障参数矩阵；产物：`backend/tests/Gaode.Contracts.Tests/Simulation/SimulationScenarioCases.cs`、`backend/tests/Gaode.Integration.Tests/Simulation/ScenarioProfileFactory.cs`。
  - 类型：验证；前置：T008、T010、T013、T016、T017、T018、T019。
  - 追溯：FR-034、FR-035、FR-036、FR-038、FR-039、FR-040；REQ：CTL-007、CTL-008、ACQ-002、ALG-013、SAF-011、DAT-008、NFR-004、RCP-005、DAT-001、ACQ-005、ACQ-010、ALG-014；宪章：P04、P06、P07、P08、P09、P10；场景：SV-27、SV-28、SV-29、SV-30、SV-31、SV-32、SV-33、SV-34、SV-35。
  - 完成条件：以八份方案输入为基线生成显式Test变体，记录最终展开配置和唯一ID/version；七环节各有正常、D-1/D/D+1、Fail、NoResponse、重复/迟到和取消。XY分别定位3D/F并计入受理；不靠扩大预算过测试。
  - 验证证据：参数矩阵可枚举并自检引用/起点/策略一致；非法数值拒绝，Fail与Timeout区分。无实际结果时不输出通过报告。 归档至上述T057证据目录。

- [ ] T058 [US5] 验证七环节延迟中间态与正常结束；产物：`backend/tests/Gaode.Contracts.Tests/Simulation/DelayedStateContractTests.cs`、`backend/tests/Gaode.Integration.Tests/Simulation/RealTimeDelayTests.cs`。
  - 类型：验证；前置：T013、T038、T049、T057。
  - 追溯：FR-007、FR-010、FR-017、FR-033、FR-034、FR-035、FR-038；REQ：SYS-004、CTL-004、ACQ-002、ACQ-008、CTL-008、CTL-010、TASK-007、TASK-008、ALG-008、NFR-007、ACQ-010、CTL-007、ALG-013、DAT-008、NFR-004；宪章：P03、P04、P05、P07、P09、P10；场景：SV-27、SV-28。
  - 完成条件：逐环节观测受理前/受理后执行中/可靠完成及后继许可，XY覆盖两次移动；实际时间Host在响应前可查询，规则/端口采用Controlled。模拟PLC不得复制流程预期状态，按钮单独输入。
  - 验证证据：每环节起点/接收/完成和后继调用计数，正常预算内完成；SQLite和媒体证据用于实际时间集成，虚拟Drain不代替真实IO。 归档至上述T058证据目录。

- [ ] T059 [US5] 验证双时钟、期限边界和调度顺序一致性；产物：`backend/tests/Gaode.Rules.Tests/Timing/DeadlineBoundaryMatrixTests.cs`、`backend/tests/Gaode.Contracts.Tests/Timing/ClockModeEquivalenceTests.cs`。
  - 类型：验证；前置：T011、T012、T013、T025、T057、T058。
  - 追溯：FR-018、FR-035、FR-036、FR-038；REQ：CTL-008、ACQ-005、ALG-014、CTL-007、ALG-013、SAF-011、DAT-008、NFR-004；宪章：P04、P06、P07、P08、P09、P10；场景：SV-27、SV-29、SV-30、SV-31、SV-32、SV-34。
  - 完成条件：相同业务输入/预算/接收顺序对比System和Controlled；边界精确值由Fake覆盖，实际时间留足裕量不依赖OS恰好毫秒。D先超时，改到期回调顺序也不改裁决，UTC前后校时不改单调预算；取消准入优先但不丢已接收事实。
  - 验证证据：D-1/D/D+1与Phase闭合证据，逐时刻Advance/Drain轨迹，两模式归一业务序列相同且Fake无需等长实等。 归档至上述T059证据目录。

- [ ] T060 [US5] 验证高度及F超时、无响应和迟到移交轨迹；产物：`backend/tests/Gaode.Integration.Tests/Simulation/AlgorithmDeadlineProfilesTests.cs`。
  - 类型：验证；前置：T042、T043、T045、T048、T053、T057、T059。
  - 追溯：FR-014、FR-015、FR-018、FR-021、FR-024、FR-027、FR-035、FR-036、FR-037、FR-040；REQ：ALG-013、SAF-005、SAF-011、CTL-010、ID-001、POS-003、CTL-008、ACQ-005、ALG-014、DAT-001、DAT-007、SAF-010、TASK-007、TASK-008、NFR-004、NFR-007、CTL-007、SAF-003、SAF-004、ACQ-010；宪章：P01、P02、P04、P06、P07、P08、P09、P10；场景：SV-03、SV-04、SV-11、SV-29、SV-30、SV-31。
  - 完成条件：执行algorithm-timeouts/no-response/duplicate-late及独立角色变体，分别检验3D异常保存后安全F、F到期异常移交；真实业务期限触发。安全/保存未满足对照拒绝继续；迟到/重复后原终态、Handoff及动作计数不变。
  - 验证证据：调用start/due/received及DeadlineDecision、WriteId、Handoff摘要、F容量/槽占用；无响应rawCandidates=null，不伪造无码/高度。 归档至上述T060证据目录。

- [ ] T061 [US5] 验证非算法超时、失败及完成未知；产物：`backend/tests/Gaode.Contracts.Tests/Simulation/PhysicalTimeoutMatrixTests.cs`、`backend/tests/Gaode.Integration.Tests/Simulation/PhysicalTimeoutIntegrationTests.cs`。
  - 类型：验证；前置：T017、T018、T046、T049、T054、T057、T059。
  - 追溯：FR-007、FR-008、FR-010、FR-019、FR-020、FR-034、FR-035、FR-036、FR-040；REQ：SYS-004、CTL-004、CTL-005、SYS-006、ACQ-002、ACQ-008、CTL-008、CTL-010、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、ACQ-006、ACQ-010、SAF-011、ALG-013、ACQ-005、ALG-014；宪章：P03、P04、P05、P06、P07、P08、P09、P10；场景：SV-08、SV-09、SV-13、SV-27、SV-31、SV-32。
  - 完成条件：PLC受理、夹紧、两次XY、3D/F采集逐项超期/NoResponse/Fail；设备内部完成但回执丢失与Hold分开。ACK不关闭完成预算，Frame不替代Ended；全部按物理故障阻依赖/移交，迟到只核对，无自动重发。
  - 验证证据：Unknown/Held/采集状态、期限及被拒绝步骤、受控停止反馈、恢复核对前后轨迹；motion-timeout示例不得进入F。 归档至上述T061证据目录。

- [ ] T062 [US5] 验证等待、背压和静止虚拟时钟下的响应能力；产物：`backend/tests/Gaode.Contracts.Tests/Timing/FrozenClockControlTests.cs`、`backend/tests/Gaode.Integration.Tests/Simulation/ResponsivenessTests.cs`。
  - 类型：验证；前置：T013、T044、T055、T056、T057、T058、T061。
  - 追溯：FR-019、FR-026、FR-027、FR-034、FR-037、FR-038；REQ：CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011、CTL-008、ACQ-002；宪章：P04、P06、P07、P08、P09、P10；场景：SV-12、SV-20、SV-27、SV-32、SV-33、SV-34。
  - 完成条件：七环节长延迟/NoResponse、慢磁盘/通知分别注入；Controlled不Advance仍受理查询/pause/cancel及已到达心跳事件，System持续处理独立心跳。停止命令可投递但其延迟反馈仍需时钟推进，不能谎称停机；非响应命令不自动停心跳。
  - 验证证据：记录控制/查询受理在原响应之前、停止待核对至真实反馈的序列及有界占用；不新增未经确认的生产响应毫秒指标。 归档至上述T062证据目录。

- [ ] T063 [US5] 验证模拟配置、预算及实际时间证据的持久追溯；产物：`backend/tests/Gaode.Integration.Tests/Simulation/SimulationProvenanceTests.cs`。
  - 类型：验证；前置：T010、T022、T036、T050、T053、T057、T059、T060、T062。
  - 追溯：FR-004、FR-005、FR-033、FR-035、FR-038、FR-039、FR-040；REQ：RCP-005、DAT-001、POS-001、NFR-007、ACQ-010、CTL-007、ALG-013、DAT-008、NFR-004、ACQ-005、ALG-014、SAF-011；宪章：P04、P05、P07、P08、P09、P10、P11；场景：SV-15、SV-16、SV-28、SV-34、SV-35。
  - 完成条件：分别仅改耗时/仅改预算/改时间模式，当前运行冻结完整原快照，后续独立运行才用新值；delay>budget不修正。逐条保存实际发生的受理/执行/结果/到期/取消/迟到，不把预期完成时刻记事实；不关闭真实参数OPEN。
  - 验证证据：SQLite快照/计时事件与配置/实际轨迹对照，非法负值/不兼容模式/Test真机入口拒绝，Handoff含正确版本及clock引用。 归档至上述T063证据目录。

## 8. 第一工位整体验证与收尾

**目标**：把实际软件证据与设计、接口和剩余外部依赖对齐；不扩大为整盘产品验证、现场联调或合同验收。

- [ ] T064 完成跨场景第一工位整体边界验证；产物：`backend/tests/Gaode.Integration.Tests/Station01/Station01BoundarySuiteTests.cs`。
  - 类型：验证；前置：T003、T038、T041、T042、T043、T047、T048、T049、T050、T053、T054、T055、T056、T058、T059、T060、T061、T062、T063。
  - 追溯：FR-010、FR-017、FR-018、FR-024、FR-025、FR-030、FR-033、FR-040；REQ：ACQ-002、ACQ-008、CTL-008、CTL-010、TASK-007、TASK-008、ALG-008、ACQ-005、ALG-014、DAT-007、RCP-001、RCP-005、ACQ-010、SAF-008、SAF-009、SAF-010、NFR-007、CTL-007、SAF-011；宪章：P01、P02、P03、P04、P05、P07、P08、P09；场景：SV-01至SV-35。
  - 完成条件：复用已定义场景与参数矩阵，统一检查全部适用路径：无配方操作/后续启动，F≤1、无额外工艺动作、质量未判定；物理占用不随Handoff释放。对异常/恢复/取消组合跨模块验证，不再实现第二套流程或整盘功能。
  - 验证证据：每SV的输入/断言/文件与提交引用可定位；模块依赖+实际端口调用轨迹共同证明边界，未到达F应为0触发。 归档至上述T064证据目录。

- [ ] T065 执行适用回归并汇总FR/SV/SC完成证据；产物：`backend/docs/station01/verification-results.md`、`artifacts/station01/evidence-index.json`。
  - 类型：验证；前置：T064。
  - 追溯：FR-018、FR-021、FR-024、FR-025、FR-026、FR-027、FR-030、FR-032、FR-033、FR-040；REQ：CTL-008、ACQ-005、ALG-014、DAT-001、DAT-007、SAF-010、TASK-007、TASK-008、RCP-001、RCP-005、HMI-004、HMI-007、DAT-008、NFR-004、NFR-007、SAF-011、ACQ-010、SAF-008、SAF-009、CTL-007；宪章：P01、P02、P04、P05、P06、P07、P08、P09；场景：SV-01至SV-35。
  - 完成条件：基于T064及各任务结果执行当前第一工位及受影响共享模块的规则、端口、实际时间、SQLite/媒体/恢复回归；记录实际build/test命令与版本，无关后续工艺不全量测。复用有效证据，仅对失败/改动影响重跑；全部关键用例非可选。
  - 验证证据：40项FR均有实现与验证证据，35项SV与10项SC逐项状态；NotRun/Blocked/Failed如实保留，不能用缓存/设计检查或内存库代替结果。 归档至上述T065证据目录。

- [ ] T066 登记真实适配完成边界与外部依赖；产物：`backend/docs/station01/adapter-readiness.md`。
  - 类型：文档；前置：T001、T026、T027、T028、T029、T049、T050、T052。
  - 追溯：FR-003、FR-007、FR-009、FR-011、FR-014、FR-019、FR-031、FR-033；REQ：RCP-001、RCP-002、POS-001、SYS-004、CTL-004、ID-006、CTL-006、CTL-010、ID-005、POS-002、ALG-013、SAF-005、SAF-011、CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、SEC-001、SEC-003、SEC-006、SYS-003、NFR-007、ACQ-010；宪章：P02、P03、P04、P05、P06、P07、P08、P09、P11；场景：SV-08、SV-09、SV-13、SV-16、SV-22、SV-23、SV-32。
  - 完成条件：逐项列端口/通用生命周期/假传输协议已验证部分及PLC真实映射、厂商网关、真实算法包、身份/运行环境未接入部分，附对应OPEN及接入前所需证据；S01-Q01后续绑定保持范围外。无外部条件不伪造成功，也不阻断已完成模拟。
  - 验证证据：按本文依赖表给可复核状态和缺项清单；生产/真机未验证明确，NotIntegrated合同证据链接；不执行现场联调。 归档至上述T066证据目录。

- [ ] T067 更新运行说明、追溯及完成限制；产物：`specs/001-station01-public-preparation/quickstart.md`、`specs/001-station01-public-preparation/tasks.md`、`backend/docs/station01/implementation-status.md`。
  - 类型：文档；前置：T065、T066。
  - 追溯：FR-024、FR-025、FR-026、FR-031、FR-032、FR-033、FR-038、FR-039；REQ：TASK-007、TASK-008、DAT-007、RCP-001、RCP-005、HMI-004、HMI-007、DAT-008、SEC-001、SEC-003、SEC-006、SYS-003、SYS-004、NFR-007、ACQ-010、NFR-004、DAT-001；宪章：P01、P02、P05、P07、P08、P09、P10；场景：SV-01、SV-19、SV-22、SV-28、SV-34、SV-35。
  - 完成条件：以实际实现替换quickstart中拟定入口，仅依据已取得证据勾选相应任务；核对spec/plan/合同/宪章P01–P11及FR/SV/SC映射。技术变化先记录影响，不悄悄改工艺；所有未执行真实依赖继续保留，区分两个里程碑。
  - 验证证据：给出可复现Test根准备/模拟运行/验证命令、证据索引与剩余限制；不得宣称培训/验收/真机/生产兼容通过。 归档至上述T067证据目录。

### 8.1 公开接口合同增量（CL-07至CL-09）

本小节承接本次plan的只读实现审计。T044/T050/T052已有较宽的控制、配置和恢复任务，本小节只补齐它们与公开合同之间的DTO、权限、错误、事件、ETag和媒体实现门，不另建第二套流程。所有任务均为后端任务；006前端和客户确认原型不在本清单内。

#### US3 增量：设备、安全、身份与取消均有真实边界

- [ ] T068 [US3] 冻结前端可见API投影和统一错误合同；产物：`backend/src/Gaode.Host/Api/Station01ApiContracts.cs`、`backend/src/Gaode.Host/Api/Station01ErrorMapping.cs`、`backend/tests/Gaode.Contracts.Tests/Api/Station01ApiContractTests.cs`。
  - 类型：实现/合同测试；前置：T003、T007、T031、T037。
  - 追溯：FR-026、FR-031；REQ：HMI-004、HMI-007、SEC-001、SEC-003、SEC-006；宪章：P02、P05、P07、P09；场景：SV-22及CL-07至CL-09。
  - 完成条件：统一定义`ApiCommandReceipt`、`StatusSnapshot`、`NotificationEnvelope`、`MediaReference`和`ErrorContract`的字段、HTTP状态映射、currentRevision和traceId；受理、停止、最终取消、NotIntegrated/NotReady/Unknown/DependencyFailed不混为Success；错误不泄露内部路径/堆栈。
  - 验证证据：契约序列化、400/401/403/404/409/429/503及幂等冲突测试，确认前端只依赖公开字段，未改动002/003合同。归档至`artifacts/station01/{testRunId}/T068/`。

- [ ] T069 [US3] 补齐四类Test身份的权限策略和拒绝审计；产物：`backend/src/Gaode.Host/Api/Station01Authorization.cs`、`backend/src/Gaode.Host/Api/TestAuthenticationHandler.cs`、`backend/tests/Gaode.Integration.Tests/Api/AuthorizationPolicyMatrixTests.cs`。
  - 类型：实现/验证；前置：T037、T068。
  - 追溯：FR-031；REQ：SEC-001、SEC-003、SEC-006；宪章：P02、P05、P09；场景：SV-22及CL-09。
  - 完成条件：逐项注册Run.Read、Run.Start、Run.Pause、Run.Cancel、Recovery.Check、Run.Continue、Config.Validate、Media.Read；Operator、EquipmentEngineer、ProcessEngineer、SystemAdministrator的允许/拒绝矩阵与合同一致；不信任请求体角色或任意Header，Test令牌仅loopback有效。
  - 验证证据：每个路由的允许/拒绝请求、无设备副作用、主体/授权结果/原因审计记录；Production/Hybrid未配置正式身份时控制入口明确拒绝。归档至`artifacts/station01/{testRunId}/T069/`。

- [ ] T070 [US3] 完成暂停、取消、恢复核对、继续路由与Application语义的公开绑定；产物：`backend/src/Gaode.Host/Api/ControlEndpoints.cs`、`backend/src/Gaode.Host/Api/RecoveryEndpoints.cs`、`backend/tests/Gaode.Integration.Tests/Api/PublicControlContractTests.cs`。
  - 类型：实现/集成验证；前置：T044、T052、T068、T069。
  - 追溯：FR-019、FR-030、FR-031；REQ：CTL-007、SAF-003、SAF-004、SAF-008、SAF-009、DAT-007；宪章：P04、P05、P07、P08、P09；场景：SV-12、SV-19、SV-24及CL-07。
  - 完成条件：路由使用requestId/expectedRevision和既有Coordinator/持久条件事务；202只代表受理，`applied=null`表示未裁决，StopPending不等于Stopped；重复、版本冲突、已完成和核对过期按统一错误合同返回；Api不直接写Run或设备。
  - 验证证据：五个取消/移交竞争窗口、恢复核对失败、重复continue、未知动作禁止盲重发及重启查询一致性；不增加重采、重拍或后续工位启动。归档至`artifacts/station01/{testRunId}/T070/`。

- [ ] T071 [US3] 将过渡RunSnapshot通知收敛为版本化SignalR事件；产物：`backend/src/Gaode.Host/Api/Station01NotificationService.cs`、`backend/src/Gaode.Host/Api/Station01Hub.cs`、`backend/tests/Gaode.Integration.Tests/Api/VersionedNotificationTests.cs`。
  - 类型：实现/验证；前置：T068、T070。
  - 追溯：FR-018、FR-026、FR-027；REQ：CTL-008、HMI-004、DAT-008、NFR-004；宪章：P05、P06、P07、P09；场景：SV-10、SV-11、SV-20、SV-31及CL-07。
  - 完成条件：发布StateChanged、OperationChanged、DiagnosticChanged、HandoffReady及schemaVersion/revision/persistedRevision/摘要；有界队列只合并可替代状态，通知失败不改变业务；保留过渡兼容时不得把Clients.All完整快照当冻结合同。
  - 验证证据：乱序、丢失、慢消费者、断线重查和终态通知测试；客户端始终以GET快照为准，通知不被当作动作完成确认。归档至`artifacts/station01/{testRunId}/T071/`。

- [ ] T072 [US3] 补齐状态快照和全状态ETag语义；产物：`backend/src/Gaode.Host/Api/QueryEndpoints.cs`、`backend/src/Gaode.Application/Station01/StatusSnapshotProjection.cs`、`backend/tests/Gaode.Contracts.Tests/Api/StatusEtagCoverageTests.cs`。
  - 类型：实现/合同验证；前置：T068、T071。
  - 追溯：FR-017、FR-026；REQ：HMI-004、HMI-007、DAT-008；宪章：P05、P07、P09；场景：SV-09、SV-20、SV-22及CL-07/08。
  - 完成条件：status分别公开Host、PLC、相机、存储/维护、当前运行及算法就绪/受限状态；算法状态完整覆盖NotConfigured、NotIntegrated、NotReady、Unknown、DependencyFailed、NoResult、InvalidResult、Cancelled及可用Success/Error/TimedOut；ETag覆盖任一公开PLC/设备/存储/算法/运行事实，算法不可用不自动把控制Ready伪造成false。
  - 验证证据：逐维度变化均触发ETag变化，缓存条件请求和断线重查得到一致revision；不修改003协议状态语义。归档至`artifacts/station01/{testRunId}/T072/`。

#### US4 增量：配置、保存与中断状态可信

- [ ] T073 [US4] 补齐公共配置校验公开接口及只校验不生效边界；产物：`backend/src/Gaode.Host/Api/ConfigurationEndpoints.cs`、`backend/tests/Gaode.Integration.Tests/Configuration/PublicConfigValidateContractTests.cs`。
  - 类型：实现/验证；前置：T008、T010、T050、T068、T069。
  - 追溯：FR-003、FR-004、FR-006、FR-028、FR-029；REQ：RCP-001、RCP-002、RCP-003、RCP-005、NFR-007；宪章：P03、P08、P11；场景：SV-07、SV-14、SV-15及CL-07。
  - 完成条件：Config.Validate按受控ID/版本返回blockingControlErrors、algorithmIssues、warnings；不访问设备、不冻结运行、不修改当前生效配置；未知/不兼容能力和路径逃逸返回统一错误。
  - 验证证据：有效、缺F、缺3D、未注册能力、算法未接入和Test/Production用途混用矩阵；确认验证接口没有PLC调用和数据库结构修改。归档至`artifacts/station01/{testRunId}/T073/`。

- [ ] T074 [US4] 为模拟和受控本地fixture建立媒体公开索引与来源/用途元数据；产物：`backend/src/Gaode.Infrastructure/Media/ControlledMediaFixtureIndex.cs`、`backend/src/Gaode.Infrastructure/Media/MediaStore.cs`、`backend/src/Gaode.Host/Api/MediaEndpoints.cs`、`backend/tests/Gaode.Integration.Tests/Media/ControlledMediaFixtureTests.cs`、`specs/001-station01-public-preparation/contracts/test-media-fixture.schema.json`。
  - 类型：实现/集成验证；前置：T006、T007、T020、T023、T068。
  - 追溯：FR-022、FR-023、FR-026、FR-033、FR-039；REQ：DAT-001、DAT-002、DAT-007、DAT-008、NFR-007、ACQ-010；宪章：P06、P07、P08、P09、P10；场景：SV-13、SV-16、SV-18、SV-22及CL-08。
  - 完成条件：媒体只能由受控fixture索引或模拟适配器生成并返回mediaId；fixture manifest明确fixtureId、相对路径、contentType、sha256、enabled、source=Simulated、purpose=Test及允许的Test用途，Host只接受配置的受控根目录并拒绝路径逃逸；公开MediaReference包含contentType、ETag、readiness、source、purpose；禁止任意路径、任意上传或前端直读文件夹/数据库。
  - 验证证据：合成媒体和受控本地照片均能通过mediaId读取；路径逃逸、未知mediaId、未保存媒体、错误contentType和未授权读取均被拒绝。归档至`artifacts/station01/{testRunId}/T074/`。

- [ ] T075 [US4] 实现媒体索引跨重启核对及一致错误/缓存语义；产物：`backend/src/Gaode.Infrastructure/Media/MediaStore.cs`、`backend/src/Gaode.Infrastructure/Persistence/TraceQuery.cs`、`backend/tests/Gaode.Integration.Tests/Media/MediaRestartIndexTests.cs`。
  - 类型：实现/集成验证；前置：T021、T022、T023、T068、T074。
  - 追溯：FR-021、FR-022、FR-023、FR-032；REQ：DAT-007、DAT-010、SAF-010、NFR-007；宪章：P06、P08、P09；场景：SV-17、SV-18、SV-19、SV-20及CL-08。
  - 完成条件：媒体记录和文件状态可在重启后核对；只有已保存Ready媒体可读取；ETag/contentType/404/409/503遵守统一合同；不以进程内字典作为唯一索引，不静默重建或迁移数据库。
  - 验证证据：保存中断、文件缺失、索引存在但文件缺失、重复mediaId和重启读取矩阵；记录WriteId、文件状态和拒绝原因。归档至`artifacts/station01/{testRunId}/T075/`。

- [ ] T076 [US4] 明确Production/Hybrid无真实相机算法时的拒绝或受限绑定；产物：`backend/src/Gaode.Host/Composition/AdapterBindings.cs`、`backend/src/Gaode.Application/Station01/StartupReadiness.cs`、`backend/tests/Gaode.Integration.Tests/Hosting/ProductionAdapterGuardTests.cs`。
  - 类型：实现/边界验证；前置：T006、T009、T049、T068、T072、T074。
  - 追溯：FR-020、FR-033、FR-034；REQ：ACQ-006、ACQ-010、NFR-007、SAF-011；宪章：P04、P05、P09、P10；场景：SV-05、SV-13、SV-16、SV-32及CL-08。
  - 完成条件：FullSimulation/Test可使用模拟相机/算法和受控媒体；Production/Hybrid在真实适配器未注册时返回明确NotIntegrated/NotReady或拒绝启动，绝不静默把模拟Success当真实成功；不改变002/003设备绑定。
  - 验证证据：模式矩阵确认真实动作计数、算法状态、媒体source/purpose和错误合同一致；真实SDK/算法未接入仍标NotRun，不伪造真机通过。归档至`artifacts/station01/{testRunId}/T076/`。

- [ ] T077 [US4] 汇总CL-07至CL-09的公开合同回归并更新实现状态；产物：`backend/tests/Gaode.Integration.Tests/Api/PublicContractRegressionTests.cs`、`backend/docs/station01/implementation-status.md`、`specs/001-station01-public-preparation/tasks.md`。
  - 类型：验证/文档；前置：T067、T068、T069、T070、T071、T072、T073、T074、T075、T076。
  - 追溯：FR-017、FR-022、FR-026、FR-031、FR-033、FR-039；REQ：HMI-004、HMI-007、DAT-007、DAT-008、SEC-001、SEC-003、SEC-006、NFR-007；宪章：P01、P02、P05、P07、P08、P09、P10；场景：SV-01、SV-05、SV-12、SV-13、SV-16、SV-18、SV-22、SV-28至SV-35及CL-07至CL-09。
  - 完成条件：公开合同与实际路由/策略/通知/媒体行为逐项对照；所有NotRun/Blocked/真实未接入限制如实记录；确认前端可依赖的仅是已通过合同测试的后端接口，不把006页面、原型或桌面壳纳入本任务。
  - 验证证据：回归报告、证据索引和实现状态中分别列出已实现、部分实现、缺失及外部依赖；通过后才可进入前端接口联调评审。归档至`artifacts/station01/{testRunId}/T077/`。

## 9. 依赖关系与并行边界

任务条目的前置ID是权威依赖表，编号按拓扑顺序排列；文档按用户场景组织不意味着后一US必须等待前一US所有无关工作。以下为关键路径摘要：

| 能力/路径 | 直接基础与汇合 | 约束 |
| --- | --- | --- |
| 开发入口 | T001 → T002 → T003/T004 | 包可用性、实际恢复和编译分别留证；不提前运行Host或建库 |
| 配置 | T007 → T008 → T009 → T010 → T031 | F及3D必需配置全合法才PLC启动，算法缺项单独处理 |
| 时间与事件 | T011 → T012 → T013/T014；T016复用其时钟 | 一个期限机制，不另写模拟超时状态机 |
| 运动与模拟 | T015 + T016 → T017；T016 → T018/T019 | 独立设备状态，真实/模拟合同相同 |
| 存储 | T020 → T021 → T022 → T023 | 独立Test库准备先于真实SQLite测试；Host不自动建库 |
| 采集和算法 | T024 + T025 → T033/T035 | 保存/租约/期限/容量先具备；Height故障不占F槽 |
| 正常闭环 | T030 → T031 → T032 → T033/T034 → T035 → T036 → T037 → T038 | 复用全部必要基础，结束在配方匹配前 |
| 算法故障 | T039 → T040 → T041 → T042 → T043 | 保存门和安全门不被非阻塞算法规则绕过 |
| 控制/物理故障 | T044 → T045 → T046 → T047/T048；T049汇合通用适配 | 停止受理不等于停止，不能自动重发 |
| 保存恢复 | T050；T051 → T052/T053 → T054；T055/T056汇合资源/API | 核对完成证据及设备现状，明确continue后复用 |
| 公开控制合同 | T003/T007/T031/T037 → T068 → T069；T044/T052与T068/T069汇合后执行T070；T070再汇合T071/T072 | DTO、权限、统一错误和expectedRevision先冻结；Api不得旁路Coordinator |
| 状态通知与ETag | T068 → T071 → T072 | 通知可丢失/乱序，GET快照和ETag为事实来源 |
| 配置/媒体公开投影 | T050 + T068/T069 → T073/T074 → T075 | 只读校验、mediaId和持久索引先于前端联调；禁止任意路径 |
| 生产适配边界 | T049 + T072/T074 → T076 | Production无真实相机/算法时拒绝或受限，不回退模拟成功 |
| 公开合同收尾 | T067与T068至T076 → T077 | 汇总实际路由、测试证据和未接入限制后，才可评审前端联调 |
| 时间全矩阵 | T057 → T058 → T059 → T060/T061 → T062 → T063 | 前期已实现机制，此处覆盖两模式全部场景与追溯 |
| 收尾 | T064 → T065；T066 → T067；T077与T065/T066汇合 | 汇总证据后才判完成；H项单列，不伪造真机通过 |

**可独立安排的机会（均不标[P]、不授权代理）**：完成共同前置且确认不同时编辑共享文件后，T005/T006/T007可分别处理各端口文件；T011与T020可处理时间/保存模型；T017/T018/T019可处理独立模拟端口；T026/T027/T028可处理通用适配。每项仍须满足自身前置，共享props/注册文件/测试夹具变更串行合入；不能从表中推出无需前置即可执行。

T026至T029不属于最小正常闭环的前置，T030的全模拟装配不要求真实适配实现或Python安装。T049才汇合同一合同下的通用真实适配。真实解释器/算法包、厂商SDK和PLC映射未具备时，返回NotIntegrated及受限状态；其现场集成仅按§14限制，不成为全模拟和假传输合同验证的全局前置。

## 10. 两个里程碑

### M1 最小正常闭环（中间里程碑）

**任务集合**：T001至T025、T030至T038，共34项，等于T038的完整前置闭包。必须已有统一期限/Ingress、有界事件/控制、真实保存/媒体、延迟模拟和独立Test库准备，不能替换为全部即时成功或纯内存持久化。

**完成条件**：合法Test配置、显式模拟实体按钮与夹紧反馈；两次固定XY、整盘多项高度、F单拍候选处理；实际SQLite/媒体必要记录及稳定移交；固定质量未判定/配方未匹配/分拣未开展；无后续动作。完成不自动释放模拟物理占用，不把新盘重入作为演示便利旁路。

M1只证明可用的正常公共流程及已具备基础查询/控制信号边界，不要求完整CancelRun API或持久化取消闭环通过。配置受限取消后新建、等待按钮取消及完整取消/恢复验证由T044/T048和T053/T054等M2任务承担；T031/T032不反向依赖T044，也不复制取消逻辑。US2至US5的异常矩阵、取消/恢复、迟到、背压和双时钟完整证据尚未满足时，**不得称完整第一工位完成**。

### M2 完整第一工位后端软件能力

**任务集合**：T001至T077全部77项。US1至US5均满足独立完成条件，T064/T065提供FR-001至FR-040、SV-01至SV-35、SC-001至SC-010的业务证据，T068至T077补齐CL-07至CL-09公开合同、权限、通知、ETag、媒体和Production绑定边界，T066/T067如实列剩余外部边界。

**完成条件**：正常/算法异常/真实边界的模拟故障/保存失败/取消/中断恢复及公开API合同全部按同一业务流程通过；固定阶段终点不被任何路径绕过；版本、时间、媒体来源和原始证据可追溯；关键验证无未执行或失败项冒充通过。通用真实适配的假传输/网关/进程合同验证是必做；H类真实解释器/真实组件及现场兼容验证仍可单列NotRun/Blocked，不能因此宣称真机可投产。若R/C/I/P或T068-T077必做证据缺失，M2也不能标完成。

## 11. FR → 实现任务与验证任务

来源栏逐项保留spec的REQ与P追溯；验证任务可含实现任务中的同名聚焦测试，测试产物/完成条件见对应任务。每项还须经T064共同边界及T065证据汇总，不能只靠编号出现在表内判通过。

| FR | 实现任务 | 验证任务 | 原REQ及宪章来源 |
| --- | --- | --- | --- |
| FR-001 | T004、T020、T031、T032、T037 | T032、T038、T047、T061 | TASK-002、TASK-003、CTL-004；USER；P05/P07 |
| FR-002 | T015、T020、T022、T031、T037、T045、T052 | T031、T045、T048、T051、T052、T054 | TASK-009、CTL-008；P06/P07 |
| FR-003 | T007、T008、T010、T031、T050 | T010、T031、T033、T038、T047、T050、T063 | RCP-001、RCP-002、POS-001；USER、CL-01/03；P03/P08/P11 |
| FR-004 | T007、T010、T031、T050 | T009、T010、T031、T047、T050、T063 | RCP-005、DAT-001；USER、CL-03；P08/P11 |
| FR-005 | T008、T010、T050 | T009、T015、T047、T049、T050、T063 | POS-001、NFR-007；USER；P04/P09/P10 |
| FR-006 | T010、T030、T031、T046 | T010、T015、T021、T022、T025、T031、T041、T042、T046、T047、T053、T061 | CTL-003、SAF-011、NFR-007；USER、CL-03；P02/P04/P10 |
| FR-007 | T005、T017、T026、T032 | T032、T038、T047、T061 | SYS-004、CTL-004；P04/P07 |
| FR-008 | T005、T015、T017、T026、T032、T046 | T015、T031、T045、T046、T047、T048、T061 | CTL-005、SYS-006；P04/P05/P06 |
| FR-009 | T005、T015、T033、T035 | T010、T031、T038、T047 | ID-006、CTL-006、CTL-010、POS-001；P03/P04 |
| FR-010 | T006、T018、T024、T027、T032、T033、T035、T046 | T015、T024、T031、T032、T035、T038、T045、T046、T047、T048、T061 | ACQ-002、ACQ-008、CTL-008、CTL-010；CL-04；P03/P07 |
| FR-011 | T006、T018、T019、T024、T033 | T033、T038、T041 | ID-005、POS-002；USER、CL-01；P03/P07 |
| FR-012 | T033、T039、T040 | T033、T041、T042、T060 | ID-006、POS-002、POS-003；P04/P10 |
| FR-013 | T019、T034、T035 | T024、T034、T035、T038、T041、T042、T045、T048、T060 | ID-001、RCP-001；USER、CL-04/05；P02/P07 |
| FR-014 | T006、T011、T019、T025、T028、T029、T033、T035、T039、T040 | T025、T033、T034、T041、T042、T053、T054、T060 | ALG-013、SAF-005、SAF-011；P04/P06 |
| FR-015 | T034、T035、T039、T040 | T024、T025、T034、T035、T038、T041、T042、T048、T060 | CTL-010、ID-001、POS-003；USER、CL-04/05；P01/P04 |
| FR-016 | T004、T006、T020、T025、T028、T033、T034、T035、T045、T052 | T033、T034、T038、T041、T045、T048、T053、T054、T060 | TASK-003、ID-005、ACQ-005、ALG-008；USER、CL-01；P07 |
| FR-017 | T004、T014、T020、T022、T032、T033、T034、T036、T037、T044、T072 | T015、T034、T038、T041、T042、T046、T047、T048、T053、T054、T060、T061、T072、T077 | TASK-007、TASK-008、ALG-008、HMI-004；USER、CL-07/08；P07/P09 |
| FR-018 | T004、T005、T012、T014、T020、T022、T024、T025、T026、T027、T028、T029、T035、T036、T044、T045、T051、T052 | T031、T044、T045、T048、T053、T054、T060、T062 | CTL-008、ACQ-005、ALG-014；P07/P08 |
| FR-019 | T005、T011、T015、T017、T022、T026、T036、T044、T045、T046、T052 | T015、T044、T046、T047、T048、T051、T052、T053、T054、T056、T061、T062 | CTL-007、SAF-003、SAF-004、SAF-008、SAF-009；CL-02；P04/P07/P08 |
| FR-020 | T006、T018、T023、T024、T027、T039、T040、T046、T076 | T024、T041、T042、T046、T047、T053、T060、T076 | ACQ-006、ACQ-010、SAF-011；CL-08；P04/P09 |
| FR-021 | T007、T020、T021、T022、T025、T031、T032、T033、T035、T036、T040、T051、T052 | T021、T022、T025、T033、T035、T047、T051、T052、T053、T054 | DAT-001、DAT-007、SAF-010；P07/P08 |
| FR-022 | T006、T018、T020、T023、T024、T025、T027、T028、T033、T035、T051、T052、T074、T075 | T024、T038、T041、T042、T046、T047、T051、T053、T054、T060、T074、T075 | DAT-001、DAT-002、DAT-007；CL-08；P06/P08 |
| FR-023 | T007、T021、T022、T023、T024、T036、T040、T051、T074、T075 | T014、T023、T051、T053、T055、T062、T074、T075 | DAT-007、DAT-010；CL-08；P06/P08 |
| FR-024 | T007、T020、T022、T036、T037、T040、T044、T045、T051、T052 | T034、T038、T041、T042、T048、T051、T053、T054、T060 | TASK-007、TASK-008、DAT-007；USER；P02/P07/P08 |
| FR-025 | T030、T034、T035、T036 | T003、T064 | RCP-001、RCP-005的本次排除边界；USER；P01/P02 |
| FR-026 | T004、T014、T022、T037、T044、T046、T068、T071、T072 | T015、T037、T041、T042、T046、T047、T051、T052、T053、T056、T060、T061、T068、T071、T072、T077 | HMI-004、HMI-007、DAT-008；CL-07/08/09；P07/P09 |
| FR-027 | T012、T014、T015、T023、T024、T025、T026、T027、T028、T029、T037、T044、T045 | T014、T023、T025、T041、T042、T044、T048、T055、T062 | NFR-004、NFR-007、SAF-011；P06/P09 |
| FR-028 | T008、T009、T010、T050 | T009、T010、T047、T050、T063 | RCP-003、POS-001、NFR-007；USER；P03/P11 |
| FR-029 | T009、T010、T025、T039、T050 | T009、T010、T025、T041、T042、T047、T050 | RCP-002、ALG-013、NFR-007；USER；P04/P11 |
| FR-030 | T025、T044、T051、T052 | T051、T052、T053、T054、T056 | TASK-007、ACQ-010、SAF-008、SAF-009、SAF-010、DAT-007；CL-02；P07/P08/P09 |
| FR-031 | T009、T015、T028、T029、T030、T037、T044、T052、T068、T069、T070 | T015、T037、T047、T049、T050、T052、T056、T063、T068、T069、T070、T077 | SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；CL-07/09；P02/P05/P09 |
| FR-032 | T007、T020、T021、T022、T030、T051 | T014、T021、T022、T023、T047、T051、T052、T053、T054、T055、T062 | DAT-007、NFR-007；ARCH §12兼容部分；P05/P08 |
| FR-033 | T005、T006、T017、T018、T019、T026、T027、T028、T029、T030、T074、T076 | T010、T013、T015、T038、T047、T049、T050、T058、T059、T062、T063、T074、T076、T077 | NFR-007、ACQ-010；USER、CL-06/08；ARCH §13.1/13.2兼容部分；P05/P09 |
| FR-034 | T008、T016、T017、T018、T019、T032 | T038、T058、T061、T062、T063 | CTL-007、CTL-008、ACQ-002；USER、CL-06；P04/P07/P09 |
| FR-035 | T008、T011、T016、T019 | T010、T038、T042、T043、T045、T046、T048、T049、T050、T058、T060、T061、T063 | CTL-007、ALG-013；USER、CL-06；P04/P09/P10 |
| FR-036 | T011、T012、T016、T025 | T013、T025、T041、T042、T043、T046、T049、T059、T060、T061、T062、T063 | CTL-007、ALG-013、SAF-011；USER、CL-06；P04/P06/P09 |
| FR-037 | T014、T016、T017、T025、T044 | T014、T023、T043、T044、T045、T048、T049、T055、T060、T062 | NFR-004、SAF-003、SAF-004、SAF-011；USER、CL-06；P04/P06/P07/P09 |
| FR-038 | T011、T012、T016、T030 | T013、T043、T049、T055、T058、T059、T061、T062、T063 | DAT-008、NFR-004；USER、CL-06；ARCH §13.2、§14.3兼容部分；P07/P09/P10 |
| FR-039 | T007、T008、T010、T020、T031、T036、T050、T074 | T010、T013、T050、T059、T062、T063、T074、T077 | RCP-005、DAT-001、DAT-008；USER、CL-06/08；P07/P08/P09/P10 |
| FR-040 | T012、T017、T019、T025、T029、T039、T040、T045、T046 | T042、T043、T045、T046、T048、T049、T060、T061 | CTL-007、ACQ-005、ACQ-010、ALG-014、SAF-011；USER、CL-06；P04/P07/P08/P09 |

## 12. SV → 输入、断言及证据任务

R=纯规则/可控时钟，C=端口合同及假传输，I=进程内模拟/实际时间Host集成，P=隔离SQLite/媒体及恢复。分类沿用verification；同一行由多个任务合并提供证据，基础任务自带的测试不重复实现。所有行额外适用T003/T064共同断言；事件记录落在对应任务证据目录。

| SV | 方法 | 实施验证任务 | 输入定位 | 必须可定位的断言/证据 | SC |
| --- | --- | --- | --- | --- | --- |
| SV-01 | I/P | T038 | normal +显式安全/按钮 | 顺序、动作事实、两类媒体和Handoff保存；仅两次固定XY与F一拍 | SC-001、SC-007 |
| SV-02 | R/C/I | T034、T038、T041 | 唯一原文、parser capability=null | 原文保留、NotDefined字段与限制；不推测产品 | SC-008 |
| SV-03 | R/C/I/P | T041、T042、T060 | 高度Error/NoResult/超期及安全不满足对照 | 实际期限终态、高度空、保存；独立安全满足才F | SC-002 |
| SV-04 | R/C/I/P | T034、T041、T042、T060 | F错误/超期/空集/冲突/格式错误 | 分别保存技术/识别/解析状态；异常移交且不重拍 | SC-002、SC-008 |
| SV-05 | R/C/I | T025、T041、T042 | 算法NotReady/NotIntegrated/null预算/持续失败 | 允许启动；调用有限终态；不等Worker恢复 | SC-002 |
| SV-06 | R/C | T033、T041 | 缺单位/基准、NaN入站、不适用高度 | 原始证据及InvalidResult；无猜值或Z动作 | SC-002 |
| SV-07 | R/C/I | T010、T031、T044、T047、T048 | 3D或F点缺失/越界/F参数缺失；配置受限运行取消后新请求 | 启动前动作=0；原上下文保留。T031/T047验拒绝与信号，T044/T048验最终取消提交后新建，不能先借continue替换 | SC-003 |
| SV-08 | C/I | T032、T044、T047、T048、T061 | 不输入实体按钮/夹紧Hold；按钮等待期间取消 | 3D动作=0；T032只验状态/基础信号，T044/T048验完整取消/停止及持久结果 | SC-003 |
| SV-09 | C/I | T015、T046、T047、T061 | 断联、安全失效、ACK无完成 | Unknown/Held；不采集、不自动恢复 | SC-003、SC-004 |
| SV-10 | R/C/I/P | T031、T045、T048 | 相同请求/异内容/不同请求并发、重复反馈 | 同Run/Command、冲突拒绝、动作计数不变 | SC-004 |
| SV-11 | R/C/I/P | T045、T048、T060 | 跨run/session/epoch、旧帧、超期响应 | LateEvidence原关联；终态/移交摘要不变 | SC-004 |
| SV-12 | R/C/I/P | T044、T048、T053、T054、T062 | 各阶段取消；移交排队/提交后无回执/CommitUnknown/取消后旧移交及重启 | 立即关准入；StopPending≠Stopped；实际SQLite条件事务唯一终态，Run/Handoff与API/内存/重启一致；未决applied=null | SC-004 |
| SV-13 | C/I/P | T024、T041、T046、T047、T053 | 相机断线/光源失败/采集未知、内容损坏对照 | 硬件受限与可靠结束内容异常分开；无假媒体 | SC-003、SC-005 |
| SV-14 | R/C | T009、T010、T047、T050 | 未注册/不兼容能力、脚本、算法未就绪对照 | 拒绝未知执行；单纯算法问题不阻启动 | SC-006 |
| SV-15 | R/I/P | T010、T050、T063 | 修改原配置文件或生效版本 | 本Run使用冻结快照，后续Run才读取新值 | SC-006 |
| SV-16 | R/C/I | T015、T047、T049、T050、T063 | Test参数请求Real运动/混合绑定 | 真实设备发送计数0；测试用途/来源记录 | SC-006 |
| SV-17 | C/P | T021、T022、T047、T053 | 快照/动作及AlgorithmIntent提交失败或未知 | 不投递依赖动作/Execute；Call输入/身份/版本/原期限与WriteId关联，意图非Accepted | SC-005 |
| SV-18 | C/P | T051、T053 | 事实/媒体/AlgorithmIntent/结果/Handoff失败或未知；终态事务与回执交叉 | 原WriteId核对，未确认不Ready或Cancelled；意图保存中算法到期不再Execute；SQLite Run/Handoff原子一致 | SC-005 |
| SV-19 | R/C/P | T051、T052、T054 | 动作/文件间隙、Call意图三窗口和完成/取消五窗口中断 | 核对WriteId/Call/Attempt及实际提交；派发Unknown不重算/重拍，唯一持久终态重启一致，无盲重放 | SC-005 |
| SV-20 | C/I/P | T014、T023、T055、T062 | 队列满/磁盘慢/配额耗尽/通知慢 | 有界准入和终态保留；停止/心跳仍可处理，未丢必要证据 | SC-005、SC-009 |
| SV-21 | R/C/I | T009、T050 | 同能力两版点位/绑定/参数及测试注册新策略 | 无需改Coordinator；未知策略无旁路，流程顺序不变 | SC-006 |
| SV-22 | R/C/I/P | T037、T052、T056 | 四角色允许/拒绝、各种异常 | 后端鉴权及审计；中文原因、版本、保存/继续去向 | SC-008 |
| SV-23 | R/C/I | T033、T038 | WholeTray、多项高度/自身标识、非法scope | 冻结范围与Run/Capture/Call关联；无Part/Face/槽拆分 | SC-001 |
| SV-24 | R/C/P | T052、T054、T056 | 核对不通过/快照缺失/未知动作或派发、未决取消、取消/完成、重复continue | 只复用完整保存步骤；原未终结Call存Interrupted和派发依据；终态/未决取消拒continue，不重置原预算 | SC-005 |
| SV-25 | R/C/I | T024、T035、T038、T048 | F成功/异常/超时/未接入/重复帧 | 每次有效F步骤单触发单图；无自动第二次Call流程 | SC-008 |
| SV-26 | R/C | T034、T041 | 空候选、同值重复、多个不同值、格式不符、无响应 | 原文Ordinal去重；冲突不选主码；null≠空集 | SC-008 |
| SV-27 | R/C/I | T058、T061、T062 | 七环节逐项可观察延迟，XY覆盖两次 | 待受理/执行中/完成分开；未完成无后继 | SC-009 |
| SV-28 | C/I/P | T038、T058、T063 | normal两种时间模式 | 期限内正常完成及保存、单拍和阶段边界 | SC-001、SC-009 |
| SV-29 | R/C/I/P | T042、T043、T060 | height延迟2000>1000及NoResponse；安全/保存故障对照 | 由期限产生TimedOut；只有独立条件满足才F | SC-002、SC-009 |
| SV-30 | R/C/I/P | T042、T043、T060 | F decode延迟2000>700及NoResponse | 保存超时、带异常移交、不重拍/伪造无码 | SC-002、SC-009 |
| SV-31 | R/C/I/P | T045、T048、T049、T060 | duplicate-late、终态/取消/跨Run后的设备和算法事件 | 原记录留痕、无重复动作；晚到物理事实只供核对 | SC-004、SC-009 |
| SV-32 | R/C/I | T046、T049、T061 | PLC受理/夹紧/XY/3D/F采集逐项超期、不响应或Fail | 设备/采集完成未知阻断；不套用算法继续 | SC-003、SC-009 |
| SV-33 | R/C/I | T043、T049、T055、T062 | 七环节长延迟/不响应，虚拟时间暂不推进 | 查询/心跳/停止/取消受理在原响应前发生；停止仍需反馈 | SC-009 |
| SV-34 | R/C/I | T013、T059、T062、T063 | 同输入/预算/事件顺序，D-1、D、D+1及UTC校时 | 两模式同期限语义；等于D超时，校时不改预算；快钟不等长实等 | SC-010 |
| SV-35 | R/C/I/P | T010、T050、T063 | 独立改延迟/预算、在途改版本、非法值及真实入口 | delay>budget合法不自动扩预算；记录冻结版本与观察，禁止Test真实动作 | SC-006、SC-010 |

## 13. SC → 完成证据

M1涉及的正常闭环和基础能力已有实际证据；完整10项成功条件仍须由T065汇总M2及外部边界状态，不用“模拟运行成功”一个布尔值代替。

| SC | 验证任务集合 | 完成证据 |
| --- | --- | --- |
| SC-001 | T033、T038、T058 | SV-01/23/28完整顺序、整盘范围及多高度原关联、两次XY/F单拍、已提交移交；无产品计划依赖。 |
| SC-002 | T025、T033、T039、T040、T041、T042、T060 | SV-03至06/29/30实际期限与技术终态；算法异常保存后独立安全F/异常移交，未有效高度为空。 |
| SC-003 | T010、T031、T032、T046、T047、T061 | SV-07至09/32启动/夹紧/XY/触发拒绝计数、实体反馈、Unknown/Held；算法不可用对照仍可启动。 |
| SC-004 | T031、T044、T045、T048、T053、T054、T060 | SV-10至12/31请求与操作原身份、重复/迟到/取消事件、终态/移交不变、无额外动作。实际SQLite条件事务的两类胜出、未决展示及重启Run/Handoff一致。 |
| SC-005 | T021、T022、T051、T052、T053、T054、T055 | SV-17至20/24实际提交/文件故障及保存合同全部恢复间隙、算法意图三窗口/终态竞争五窗口；核对和复用原证据、未知不重发、不自动重采。 |
| SC-006 | T009、T010、T047、T050、T063 | SV-14至16/21/35两版配置、不改Coordinator、快照稳定、未注册/脚本/Test真机入口零调用。 |
| SC-007 | T003、T064、T065 | 全部35项SV适用的共同边界断言与静态依赖检查，质量始终未判定，F≤1且无后续业务。 |
| SC-008 | T034、T035、T037、T041、T048、T056 | SV-02/04/22/25/26原始/去重/主码/解析分开，中文诊断、身份/版本/保存与处置；单拍无筛码。 |
| SC-009 | T057、T058、T060、T061、T062 | SV-27至33七环节中间态，两次XY分别计时；真实期限触发，迟到隔离，查询/心跳/停止在原响应前处理。 |
| SC-010 | T013、T059、T062、T063 | SV-34/35双时钟语义、D边界、静止虚拟时钟受理、独立延迟/预算、完整冻结快照和实际时间证据。 |

## 14. OPEN与外部依赖限制

原12项OPEN及2项S01问题全部保留，以下限制是使用点，**不是所有任务的全局前置**。任务列中的通用实现、假边界测试和全模拟仍必须完成；限制真实部分不能成为“不实现后端”的理由。T066逐项记录实际可用证据，不关闭来源编号。

| 编号 | 来源 / 缺失 | 只限制的任务部分与补充时机 | 当前可独立继续 |
| --- | --- | --- | --- |
| OPEN-05 | SYS-005、CTL-011/013；固定点同步、PLC缓存、区域握手 | T026/T049的真实缓存/区域握手，在接PLC前确认；若强制依赖产品区域，记录spec C02/C07范围冲突，不在任务内提前匹配 | T008至T010公共快照、T017模拟、T031启动流程及假传输合同 |
| OPEN-08 | CTL-001/002/009/010；地址/类型/字序/单位、XY/安全反馈、心跳 | T026/T049真实Modbus传输映射、正式运动启用前；PC≤50ms/PLC≤10ms及3s基线保留，不捏造实际地址 | T005/T015语义边界、T026假传输编码/轮询、T047/T061拒绝规则 |
| OPEN-09 | ID-001、RCP-001、CTL-011；公共配置来源、整盘描述、盘码字段格式 | T008/T010正式配置源、T034正式解析规则、T026握手次序；对应正式配置/规则使用前。整盘/F单拍/多码冲突不再待定 | 全部Test配置及T034原始候选/NotDefined；T050配置替换验证 |
| OPEN-10 | CTL-004、SAF-008；实体按钮/启动/复位反馈 | T026/T049真实启动确认链及停止相关已确认能力使用前；不生成复位/回零任务 | T017独立按钮/夹紧模型、T032顺序、T047/T061故障验证 |
| OPEN-11 | SAF-003/004/010；停机/持件/恢复真实顺序 | T015/T026/T049真实停止和T052现场恢复控制前；禁止猜回位/松夹 | T044 StopPending、T052/T054授权核对复用、未知动作保持受限 |
| OPEN-13 | SAF-005/007；报警等级、码表及可恢复条件 | T026/T046真实错误映射启用前；不借算法异常伪造设备报警 | T004分类、T037中文诊断、T046测试故障及T056处置审计 |
| OPEN-18 | ALG-013、CTL-007、ACQ-006、NFR-004、DAT-008；生产预算/容量/计时口径 | T010/T011/T015/T024/T025生产参数生效及生产性能验证前；团队测试值不能关闭本项 | T011至T019机制、T055资源验证、T057至T063双时钟矩阵 |
| OPEN-20 | DAT-007/010及REQ §10；正式目录、留存、容量及恢复目标 | T021至T023正式存储环境配置和真实留存/恢复参数使用前；不扩为完整维护工具 | 显式隔离Test根、真实SQLite/媒体T053、恢复T054和维护互斥 |
| OPEN-22 | CTL-016、NFR-008；OS/SDK/光源相机/算法包与依赖兼容。2026-09-21已提供设备SDK归档并完成只读目录清单，接口内容尚未审阅 | T026实际传输依赖、T027厂商网关、T029真实Worker组件及生产部署前；详细SDK接口在真实设备适配前核验；资料存在或模拟通过均非兼容证明 | T001/T002验证开发依赖；T026至T029通用适配/假边界合同；全模拟无需Python；资料/接口审阅/适配/真机验证分别登记 |
| OPEN-23 | SEC-001/003；正式身份及启动/取消/恢复授权策略 | T037/T044/T052正式身份来源和策略启用前；未配置正式身份不得真实控制 | 显式Test主体与四角色策略、T056完整后端鉴权/审计验证 |
| OPEN-24 | CTL-008及REQ §10；真实命令/应答ID或等效防旧反馈 | T026/T049真实夹紧/XY匹配依据投入使用前；无ID不能假定PLC已有寄存器 | T005内部关联、T012/T045隔离、T048重复乱序及T054未知恢复拒绝 |
| OPEN-26 | ID-005/006、POS-001/002/003、CTL-006/013；正式XY/限值/整盘覆盖/高度单位基准/失败分支 | T010正式目标、T027真实采集配置、T033高度解释投入真实用途前；本阶段不消费高度驱动Z | Test固定XY/整盘范围、多项高度留原标识、T041无效输出、独立安全F |
| S01-Q01 | ID-005、ACQ-005/OPEN-26；后续工件/面绑定粒度 | 后续消费者需要绑定时补充；不为本阶段添加产品/槽位/Part/Face模型或任务 | T033原始多高度与Run/Capture/Call/scope关联、T036移交未绑定原因 |
| S01-Q02 | TASK-002；入口字段格式及ExternalTask关联 | T031/T037实际调用方对接前；拟定API不能冒充已冻结外部协议 | 内部身份/请求幂等、明确Test工单批次场景、全部后端接口测试 |

### 真实集成证据登记（H类，非新增OPEN）

| 证据类别 | 当前通用任务 | 外部输入及实际接入前所需核验 | 对本次任务完成的解释 |
| --- | --- | --- | --- |
| H-PLC | T026/T049 | 合法映射、通信依赖及设备；编码/新鲜反馈/互锁/实体按钮/夹紧/停止/失联与真实防重复 | 通用连接泵/语义编解码和假传输必做；实际网络/机械验证未执行，未集成须受限 |
| H-CAPTURE | T027/T049 | 已提供SDK归档目录包含2D相机、3D相机及光源资料；仍须核验厂商接口、驱动/位数、光源控制、真实采集结束/接管依据、整盘覆盖与图像格式 | 当前仅“资料已提供、目录已核对”；接口未审阅、适配未实现、真机未验证。网关合同/通用生命周期必做；详细接口核验在真实设备适配前进行，不能编造能力或自动模拟成功 |
| H-WORKER | T028/T029/T049 | 显式解释器、受控算法组件/依赖/输入格式；协议、回收、兼容与算法效果 | 全模拟不启动Python；假进程/协议合同必做。协议fixture的真实解释器执行及真实算法接入分别登记，未执行不影响模拟门但不得写真接入成功 |
| H-STORE | T020至T023/T053/T054 | 生产OS/原生SQLite/磁盘缓存及断电恢复、容量留存 | 开发Test库的原生加载、实际SQLite/文件及恢复测试必做；生产断电/性能仍未验证，不能用rename或模拟断言替代 |
| H-IDENTITY / H-DEPLOY | T001/T002/T037/T066 | 正式身份策略、外部入口字段、生产软硬件与SDK兼容 | 本地开发restore/build及Test鉴权必做；生产兼容、正式权限与外部调用方联调不作为已完成事实 |

若补齐输入后发现契约与实际设备不兼容，应定位spec/plan/合同的冲突、影响及新决策，再修订受影响任务；不在“实现某适配器”里悄悄加入配方前置、额外动作、重拍或默认安全值。

## 15. 关键规则与模板适用性

| 模板/宪章规则 | 本阶段任务或不适用理由 |
| --- | --- |
| 完整配方新增/复制/修改/校验/发布/回滚和后续计划 | 不适用：第一工位尚未匹配产品配方。只做T008至T010/T031/T050公共配置加载校验、冻结和版本隔离，不增加管理平台 |
| P11配置变化、新能力注册、拒绝脚本/旁路 | T009/T010/T050；既有能力内仅变配置，新测试策略必须注册且受Motion/保存门约束 |
| 启动快照与正式/模拟共用、Test真实动作拒绝 | T010/T015/T031/T047/T050/T063；冻结的是公共/预算/模拟快照，不是产品配方 |
| 固定XY、3D仅高度、固定对焦 | T015/T033/T035/T047；不实现Z控制、XY求解或找焦 |
| 翻面身份/点位、A后B整盘批采、输入汇合和NG/Pending融合 | 不适用：未开展这些工艺。T023/T025/T043/T055仍验证Height隔离不耗尽F槽/媒体；不建设A/B批采模块或默认OPEN-16优先级 |
| 算法未接入/未配置/未就绪/失败/超时有限收敛 | T025/T039至T043/T060；安全/保存/容量失效仍限制对应步骤 |
| OPEN-26无效Z与真实安全 | T012/T015/T033/T046/T061；不填0/猜高度，不生成Z目标，不绕过夹紧/互锁 |
| 身份、终态、重复/迟到、不盲重发 | T004/T012/T031/T044至T048/T052/T054/T060；无Part/Face伪造 |
| 必要保存、维护互斥、Host不静默迁移 | T020至T024/T036/T051至T055；独立Test准备不是完整维护平台 |
| 后端权限、中文异常、结构化处置及有界通知 | T037/T044/T052/T056/T062；不依赖页面控制安全 |
| MES/模型/样本空接口与算法调用 | T030仅NotIntegrated端口及状态，无业务接口/联网；T025/T028/T029保留真实算法调用适配边界 |
| 模拟时间与真实事件来源 | T011至T019/T057至T063；共用接收期限机制，不直接改超时终态，不将虚拟时间冒充磁盘/设备经过时间 |

### 宪章检查承接

plan §3的设计前/后结论仍有效。本次检查只确认任务有对应落点，不把真实依赖改为“全部通过”。

| 原则 | 任务依据 | 本轮范围结论 |
| --- | --- | --- |
| P01 | T003/T064/T067及本文件来源/边界/覆盖表 | 与当前spec/plan一致；旧阶段历史记录只读 |
| P02 | T030/T036/T037/T064/T068/T077、模板不适用表 | 后端范围受控，三个空接口不恢复业务；公开合同不纳入前端实现 |
| P03 | T010/T015/T033/T035/T047 | 固定XY/整盘高度/F单拍明确；正式点位/覆盖仅按OPEN限制 |
| P04 | T025/T039至T043/T046/T060/T061 | 算法有限等待与真实机械准入分别验证；未知真实停止分支不猜测 |
| P05 | T002/T003/T014/T015/T030/T049/T068/T070/T071 | 唯一Host/状态所有者/运动权，Api不旁路Application，通知不改变状态 |
| P06 | T014/T016/T022至T025/T043/T055/T062/T071/T074/T075 | 有界消息/资源/终态/媒体，实际容量仍待生产参数 |
| P07 | T004至T007/T012/T031/T034/T045/T052/T068至T075 | 身份和状态分离、原始结果/迟到留痕；公开版本和媒体状态不伪造 |
| P08 | T020至T024/T036/T051至T054 | 必要保存、文件间隙、维护互斥及核对复用；生产留存/恢复目标保留 |
| P09 | T003/T017至T019/T037/T038/T056至T065/T068至T077 | M1相关任务已验证同业务流程、中文诊断和必要边界；T056至T077仍按证据状态执行 |
| P10 | T001/T066及12 OPEN/2 S01表 | 本机依赖只作开发选择，H类未验证如实保留 |
| P11 | T009/T010/T034/T050/T073/T076 | 能力注册与配置边界，算法实时就绪不混入启动门，Production不静默模拟 |
| P12 | T068/T071/T072/T074/T077 | 只提供后端API和状态通知；006独立处理原型、页面和桌面壳，本任务不改客户确认原型 |

初版任务静态检查未识别H01/H02/M01；本次按用户决定同步修正设计与任务，不改变spec业务规则。真实SDK/PLC/算法包缺失已限制到对应适配；实现中如发现矛盾必须显式记录，不能以任务完成替代需求裁决。

## 16. 修订与本轮文档复核

初版1.0.0于2026-09-20生成67项任务，静态图检查为290条依赖无环、M1闭包34项；该检查不包含后来analyze指出的隐式完成条件问题，不能以图无环代替业务依赖成立。

1.0.1（2026-09-20）按用户明确决定修正：
- H01：T007/T020/T022基础保存合同，T025统一调用意图门，T033/T035调用依据，T051/T052核对；T053/T054验证三窗口及原期限。
- H02：T020/T022持久约束，T036完成候选、T044取消候选，T048/T053实际SQLite交叉提交/回执，T054重启核对；T051/T052恢复条件。
- M01：T031/T032只验查询/控制信号边界；完整取消和配置受限/等待按钮分支交T044/T048，M1正常闭环与M2完整能力分开。

未新增FR/T/SV编号；直接依赖表不变，T025经T023已依赖保存T022，T048/T053已有完整取消与持久基础的传递依赖。2026-09-21完成M1的34项并保存逐项证据；其余33项保持未勾选。

M1现已构建并完成规定的软件验证；这不表示M2、完整第一工位、真实设备、算法精度、现场节拍或生产兼容已经验证。局部OPEN继续按原使用点处理。

1.2.0（2026-09-22）按`/speckit.plan`后的实现审计和CL-07至CL-09同步新增：
- T068-T072：公开API投影、统一错误、Test权限、控制路由、SignalR事件和全状态ETag。
- T073-T076：公共配置校验、受控模拟/本地fixture媒体索引、跨重启媒体核对及Production模拟绑定保护。
- T077：公开合同回归和实现状态汇总；未勾选项仍须实际实现和验证后才能完成，不把任务拆解当作代码完成。

## Phase 17: Convergence

本阶段由 `/speckit.converge` 根据当前代码与spec/plan/tasks的交叉复核追加。只描述仍未满足的可执行工作；不修改前述任务、规格、计划、原型或前端范围。完成这些任务后再次运行 `/speckit.converge`，直到没有新的缺口。

- [ ] T078 将暂停、取消、恢复核对、继续及终态竞争接入持久化条件提交和跨重启恢复，保持命令幂等键、ExpectedRevision、WriteId、Run/Handoff唯一终态和未决状态一致；补齐 `Station01ControlCommandService` 的持久实现、`RecoveryEndpoints` 及恢复集成测试，依据 P08、SC-004、SC-005（contradicts，CRITICAL）。
- [ ] T079 统一算法有限终态、异常保存门和安全继续判定，补齐 `AlgorithmOutcomePolicy`、`ContinuationPolicy` 及3D/F步骤的共享出口；覆盖 NotConfigured、NotIntegrated、NotReady、Unknown、NoResult、InvalidResult、Error、TimedOut、Cancelled 和独立F继续，依据 P04、SC-002（partial，HIGH）。
- [ ] T080 实现运行事件关联归约、迟到/重复/乱序证据隔离及物理故障限制；新增 `OperationEvidenceReducer`、`LateEvidencePolicy`、`PhysicalFaultPolicy`，确保旧session/epoch、未知动作和安全失效不产生新动作或改写终态，依据 P07、SC-004（missing，HIGH）。
- [ ] T081 将受控媒体fixture索引和媒体元数据接入Host与MediaStore，建立持久索引、SHA校验、跨重启重建/核对、缓存304及一致错误语义；补齐真实文件间隙和租约恢复测试，依据 P06/P08、SC-005（partial，HIGH）。
- [ ] T082 将SignalR通知改为版本化 `NotificationEnvelope`，实现revision/persistedRevision/changedFields摘要、必要证据不丢失、有界慢客户端合并和断线GET重查；补齐通知合同测试，依据 CL-07、SC-009（partial，HIGH）。
- [ ] T083 补齐七环节独立延迟、算法/物理超时、重复迟到、双时钟边界、冻结配置追溯和静止虚拟时钟响应矩阵；新增T057-T063列明的合同及集成测试，依据 CL-06、SC-009、SC-010（missing，HIGH）。
- [ ] T084 补齐PLC、相机、Worker协议及生命周期的合同/集成边界验证，包含协议fixture、输入输出路径限制、会话代次、取消/退出、未接入和Production拒绝模拟回退；不得将端口替身或NotIntegrated报告为真实接入，依据 OPEN-22、P09、SC-006（partial，HIGH）。
- [ ] T085 完成公开API合同、四类Test身份授权拒绝、统一错误、全状态ETag和Production绑定保护的回归测试；状态投影必须覆盖公开PLC/相机/存储/维护/算法事实并能稳定重取，依据 CL-07至CL-09、FR-026、FR-031（partial，HIGH）。
- [ ] T086 执行第一工位跨场景边界套件并生成验证结果、证据索引、适配就绪限制和实现状态汇总；逐项标注真实/模拟/回放/未执行，不把软件模拟或构建通过解释为真机、生产或前端联调通过，依据 SC-007、SC-010、P09（missing，HIGH）。

## 2026-09-24 主流程启动诊断增量

- [X] T087 [US3] 对应FR-041、SV-36、SC-011、P04/P07–P09/P13：在001已有启动受理、就绪检查及异常出口补足分级分类、`requestId/commandId/runId`贯通、停止阶段/判定依据/实际处置和受控原始异常记录；拒绝建运行时明确任务未创建，通信无法确认安全与设备明确不安全分开，心跳/轮询重复日志受控且必要日志持久可查。依赖现有001启动/错误合同及003 FR15设备边界；完成条件为一次受理后首次PLC通信失败和一次明确不安全对照的保存日志、回执、查询与无后继动作证据可复核，进程退出后仍能按关联定位；Test/Simulated不得标真机通过。已有T037/T056/T085及其历史勾选不抵扣本任务，若公开字段不足须先同步相应合同与006消费者规格。2026-09-24按001 `verification.md` 的受理后通信故障与可靠不安全对照、退出后关联索引及正常启动/查询回归，在Test/VirtualPlc诊断范围完成；实际页面由006 T046/007 T027另判，真实设备及原始故障修复不在此勾选内。

## 2026-09-24 003下料共享配置扩展

- [ ] T088 [US3] 对应003 FR16–FR18、P03/P04/P08：允许公共配置schema中独立Unload固定XYZ并在冻结快照中保留来源/版本，验证旧3D/F配置兼容、Test/生产来源区分及缺生产批准值不得派发下料。仅共享字段/配置验证，003正式动作与007联调分别由T064/T028验收；不复用T087历史勾选。


## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

- [ ] T089 对接008 FR-001/006的公共准备移交与最新扫码边界，核对AB1/CD1两上下文来源及实际握手；依赖008 T007/008和OPEN-29，修改backend/src/Gaode.Application/Station01/PublicPreparationHandoffV2.cs及本功能contracts/device.md/persistence-handoff.md，证据引用008 T013—015；历史001证据不扩作新扫码通过。 追溯：FR-042、宪章3.2.0 P03/P07/P08/P09/P11/P13（006另P12）。

