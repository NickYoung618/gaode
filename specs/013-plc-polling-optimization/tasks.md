# 功能任务清单：PLC轮询降频与通信负载优化

> 当前验收解释：2026-10-05需求方批准 **013-acceptance/2**，详见[验证合同V06.2](contracts/verification.md#acceptance-v2)。普通25ms等新增工程目标改为非阻断观察；原期限/保护、局部首态75ms、确认周期、单源/计划、预算/净收益及证据完整性仍硬。原数字和历史失败保留。下文历史“全部V06成立/不得倒改门槛”以本次显式批准范围解释，不能据此修改硬条件。


**功能**：013-plc-polling-optimization  
**项目根**：E:/dzk/gaode-1  
**显式目录**：SPECIFY_FEATURE_DIRECTORY=specs/013-plc-polling-optimization  
**日期**：2026-10-04；**宪章**：8.0.0  
**输入**：[spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[采集合同](contracts/plc-acquisition.md)、[验证合同](contracts/verification.md)、[quickstart.md](quickstart.md)、[architecture最新追加复核](checklists/architecture.md)。  
**状态**：20261004-kernel-analysis-11正在复核：原08构建、L70/70、组件51/51/N3及正式after均保留为原身份历史证据。当前新增预览脚本未登记导致L的A10真实拒绝；T027/T028暂退未完成，待本身份完整L和51行/N3实际通过后恢复。T029/T030仍未完成，诊断不替代正式after。历史失败及审查勾选不变。

最新architecture复核认定DQ-01—03文档缺口关闭，21项辅助评价满足且全部[ ]；首次“不具备tasks条件”是历史，不能作为当前阻塞。DQ关闭的是设计缺口，其实施/计量义务仍由本清单承接。正式PLC外部限制保留，不编造协议输入。

2026-10-04实施前定向复核：U1由T001/V02完整StorePrep源码、锁文件、集中包版本及引用/导入/链接/资源/运行工具闭包，T007独立构建和实际工具路径承接；C1由T024/V09实际inventory登记先于T027承接；I1由I-FU正常失败保存、I-SEG生产自动1024阈值真提交接续及独立I-GAP承接。三项文档缺口关闭，实际实现/运行仍NotRun。30个ID、原依赖及三组[P]不变；I-SEG定义由T019、内部生产边界由T021顺序完成，无新共享写冲突。不修改architecture/requirements勾选，按本轮明确授权继续实施。

## 拆解规则与路径约定

- 本轮只新建本tasks.md；下述复制、同步、代码、构建和运行均为后续任务，不在生成清单时执行。不改其他文档、清单勾选或feature.json，不自动analyze/implement。
- 每个任务给实际目标文件、依赖、需求/宪章、完成判据和证据；“新增”是后续实现选定落点，不表示文件已存在。所有未带根的产品/脚本路径相对A；证据路径均在R。文件检索/文档登记不能替代实现或运行通过。
- A=E:/dzk/gaode-1；R=A/artifacts/plc-polling-013/<attempt>，attempt由T001一次生成并记录实际绝对路径；B0=R/baseline-pristine为不改原件，B=R/baseline-source为仅应用同一中性补丁的改前运行根。GAODE_013_BASELINE_ROOT=B、GAODE_013_AFTER_ROOT=A、GAODE_013_ATTEMPT_ROOT=R。没有Git，不虚构分支/提交，不进行Git写操作。
- T001保存的基线不是旧workcopies；B/B0隔离与按清单复制不能被R在A内部的目录递归破坏。两侧DLL/SchemaRoot由各自源码根解析；只切cwd不算隔离。
- 测试定义任务只交付用例/独立预期，待实现齐备后在T028统一执行。定义完成不能标“保护通过”；任何NotRun/失败不计软件完成。
- [P]仅表示指定前置完成后的三个无共享写入冲突的静态工作对；不授权自动多代理。所有构建、L/组件、空闲计量和代表链串行，测量期间不得并跑构建/测试或改变外部负载。
- 保持H300、B200/500、F/U/T200、P500/1000ms、首中间态局部50ms及既定退出；两条连接/唯一共同业务路径、原I/O/3秒/受理/动作/保存期限、实际反馈/坐标/真保存均不放宽。禁止新增通用调度平台、线程池/GC/优先级调参、恢复体系、页面/算法/数据库/VirtualPlc扫描重构。

## 当前阶段范围与完成证据（P13）

| 项目 | 范围及任务/证据 |
| --- | --- |
| 起点 | 当前已集成011/012主项目，T001先保留未优化源码/schema/完整输入；T007/T008取得真实before身份/证据 |
| 终点 | 相同完整流程真实完成，T030证明SC-001—008适用软件义务；分阶段代码/文档交付均不冒充013完成 |
| 必须参与 | 原通信适配、共同执行、独立VirtualPlc、现采集/算法、真实配方保存/F匹配/冻结及隔离业务存储；无新增业务执行入口 |
| 最小运行集合 | 每侧一次5秒预热后60秒空闲、每侧同一run-2一条；表列直接组件/N1—N3，完整010 L和受影响009，受影响构建和发现/执行核对 |
| 完成证据 | R内源码/构建/配置/输入/中性补丁摘要、实际PDU/设备时窗/真实持久引用、原日志/TRX、组件账本、compare及final-report |
| 延期/局部限制 | 正式地址/周期/保持/时基、原稳定性待办和失败；全量/全配方/长期压力/010完整专项/整机生产验收不加入当前前置 |

## Phase 1：必要准备

本阶段只保留原主项目及身份，不初始化新工程、升级依赖或维护运行数据库。

- [X] T001 保存未经013优化的主项目源码及完整输入依赖树。文件/交付物：`artifacts/plc-polling-013/<attempt>/baseline-pristine/`、`artifacts/plc-polling-013/<attempt>/baseline-source/`、`artifacts/plc-polling-013/<attempt>/baseline-manifest.json`。

  **依赖**：无；必须最先完成。**追溯**：FR-001/023；AC-11；SC-002/005；V02/02.1；P01/07/13。  
  **完成判据**：任何中性钩子、产品、schema或活动配置写入前，按V02保留backend/src、所需backend/tests/Rules、backend/tools/Gaode.StorePrep完整源码/项目及实际锁文件、VirtualPlc、scripts、global.json、backend/Directory.Build.props和backend/Directory.Packages.props及实际导入构建配置、项目/依赖锁文件、001全部实际加载schema和011 joint完整引用树；排除bin/obj、artifacts、旧workcopies。原件只读，B由该原件形成运行工作副本；不从旧副本覆盖A。记录实际绝对B0/B/A/R、文件大小/SHA-256、时间、OS/.NET/Python身份及历史失败引用；当前无Git，不造提交号。首次修改前逐项目核查ProjectReference、Import/Directory.*、链接Compile、复制资源和运行时工具依赖，记录实际依赖闭包及不存在项；不只保留csproj或现成DLL。R位于A内时按明确文件清单复制，不能递归复制R自身。  
  **必要证据**：R/baseline-manifest.json及B0只读原件；B0/B与捕获时A的逐文件摘要对应，未发生产品/schema/输入优化。

## Phase 2：共同前置——合同、可启动的计量与版本迁移

先T002规则→T003语义→T004验证/消费者，再改共享代码。T007/T008是before分支，T009是after迁移分支，两者共同依赖T006而不相互等最终通过；默认尽早取得before数据，真实测量与其他构建/测试不并行。若before环境缺失，仅其测量/最终比较保持未完成，已保留原件后明确的软件工作可继续。

- [X] T002 先同步现行分频、合法计划及预算合同。文件/交付物：`specs/003-plc-latest-protocol/spec.md`、`specs/003-plc-latest-protocol/plan.md`、`specs/003-plc-latest-protocol/contracts/status-notifications.md`、`specs/003-plc-latest-protocol/tasks.md`、`specs/009-plc-protocol-isolation/spec.md`、`specs/009-plc-protocol-isolation/plan.md`、`specs/009-plc-protocol-isolation/contracts/protocol-maintenance.md`、`specs/009-plc-protocol-isolation/tasks.md`、`specs/001-station01-public-preparation/spec.md`、`specs/001-station01-public-preparation/plan.md`、`specs/001-station01-public-preparation/tasks.md`、`specs/001-station01-public-preparation/contracts/configuration-time.md`、`specs/001-station01-public-preparation/contracts/budget.schema.json`。

  **依赖**：T001。**追溯**：FR-021/028；SY-01/02；DQ-02；SC-008；P01/05/11/13。  
  **完成判据**：按plan同步003 FR03/FR15、T065当前承接、009 FR-008/P03/P04/T022及001“009/AL08当前预算”条款；预算schema发布2.0、删除plcPoll必填和属性，仍拒绝额外字段。文档先解释原1.1→新运行2.0及历史只读，再允许T009改共享模型/消费者；保留旧50ms事实和任务勾选，修正现行约束，其他条款不重建。此时A可能暂不可构建/运行，禁止提前用新schema加载旧产品；B/B0旧schema不动。  
  **必要证据**：R/document-sync.md逐文件差异及SY-01/02、001条款承接；当前合同先于对应代码的记录，历史勾选差异为0。

- [X] T003 同步分组观察、共享输出与动作保护合同。文件/交付物：`specs/009-plc-protocol-isolation/contracts/business-device.md`、`specs/009-plc-protocol-isolation/contracts/diagnostics-history.md`、`specs/009-plc-protocol-isolation/spec.md`、`specs/009-plc-protocol-isolation/plan.md`、`specs/009-plc-protocol-isolation/tasks.md`、`specs/011-plc-interaction-update/spec.md`、`specs/011-plc-interaction-update/contracts/plc-communication.md`、`specs/011-plc-interaction-update/contracts/execution-and-state.md`、`specs/011-plc-interaction-update/plan.md`、`specs/011-plc-interaction-update/tasks.md`。

  **依赖**：T002。**追溯**：FR-014—019/021/028；SY-03/05；DQ-01/02；P01/04/05/07/08。  
  **完成判据**：将A03—08/D02—07承接到009 E01/E02/E04及011 PC02—05、FR-013/016—023对应正文：独立位置年龄、四个Host字段/live1.2、本次轴/翻放因果、首次50ms局部阶段、最终位置先发布、取放保存门、预算新版引用。修改共享输出前完成spec/contracts/plan/tasks对齐；原始plc-evidence/1、Domain观察形状、正式T009/T010限制及原任务事实保持。  
  **必要证据**：追加R/document-sync.md的SY-03/05字段→消费者→后续代码任务映射；明确Position.Identity.Reliability来源及历史读取承接。

- [X] T004 同步最小验证、共同执行和012实际消费者责任。文件/交付物：`specs/010-recipe-execution-isolation/spec.md`、`specs/010-recipe-execution-isolation/plan.md`、`specs/010-recipe-execution-isolation/tasks.md`、`specs/010-recipe-execution-isolation/contracts/common-execution.md`、`specs/010-recipe-execution-isolation/contracts/verification.md`、`specs/011-plc-interaction-update/contracts/verification.md`、`specs/011-plc-interaction-update/plan.md`、`specs/011-plc-interaction-update/tasks.md`、`specs/012-recipe-authoring/contracts/shared-integration.md`。

  **依赖**：T003。**追溯**：FR-001/025—028；SY-04/06/07；DQ-03；AC-12；P01/03/05/12/13。  
  **完成判据**：明确013承接轮询延期、完整L/发现执行核对、后端API观察与页面验收边界、各侧一窗一链及A/B/C适用侧。核对012 spec/plan/tasks、editor-ui.md、recipe-authoring-api.md仅实际命中位置/预算引用处；无影响的保存/F匹配/冻结/页面消费者在shared-integration和R/document-sync.md登记沿用，不机械重写。旧verification-report和main-project-integration记录只引用。中性计量/新profile所需共享合同必须在T005/T006前就绪。  
  **必要证据**：R/document-sync.md覆盖SY-01—07和001依赖；旧证据、原任务完成事实及原型修改数为0。

- [X] T005 接通两侧共同中性计量及真实2秒API观察器。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs`、`scripts/013-api-observer.py（新增）`。

  **依赖**：T004。**追溯**：FR-008/019/020/023；SC-006；V01/02/08；DQ-02/03；P02/05/07/09/13。  
  **完成判据**：优先复用实际交换/诊断/原日志，补齐必要计数或时点时只加相同中性观察钩子；两侧同一补丁/统计格式，记录补丁摘要，禁止包含采集优化、schema升级、业务顺序或模拟时序变化。观察器实际连通后端且读到有效响应才报告自己的backend-observer-ready，2秒只读API、不触PLC、不冒充page-ready。harness区分013后端负载与原页面入口，仍走真实保存/F匹配/冻结/Final。以有界聚合采集实际发送（包括失败）、排队/响应、准备/重复唤醒/字节/证据量；采集钩子不新增控制等待或高频JSON。  
  **必要证据**：R/neutral-measurement.patch及SHA-256、两侧应用清单、R/measurement-definition.json；接线代码与原入口断言可审查，实际连通与非干扰证据留T008/T029，不提前宣称成功。

- [X] T006 建立013显式profile、分阶段驱动及冻结必需清单。文件/交付物：`scripts/workflow/runner.py`、`scripts/workflow/verify_entry.py`、`scripts/workflow/plc_polling_013.py（新增）`、`scripts/workflow/013-required-cases.json（新增）`、`scripts/verify.ps1`。

  **依赖**：T005。**追溯**：FR-022—026；AC-11/12；SC-007；V01.1—06/08/09.1；DQ-03；P01/05/13。  
  **完成判据**：明确PlcPolling013分支及required_manifest，拒绝未知profile回009；保留run_with_lightweight外层。固定013辅助模块提供同attempt的before测量子阶段与final收口，不建通用平台；before仅依赖自身构建/输入/中性观察器，既不等after通过也不重复L。最终入口复用已捕获before证据、只执行缺失的当前身份项。先冻结下方case/dataRow义务、路线独立预期、A/B/C判据、V04—06预算和输出路径；新增方法在T024绑定实际发现身份，不能按发现结果删必需项。最终比较代码可以未完成，但before分支不能调用或等待它。  
  **必要证据**：R/required-cases.frozen.json、R/measurement-contract.json与profile选择/子阶段依赖审阅记录；启动可用性由T007/T008取得，不能用缺after结果阻止before启动。

- [X] T007 构建改前最小运行闭包并冻结before装配身份。文件/交付物：`backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj`、`backend/src/Gaode.Host/Gaode.Host.csproj`、`VirtualPlc/VirtualPlc.csproj`、`R/inputs/before/run-2.json`、`R/before/build-manifest.json`。

  **依赖**：T006。**追溯**：FR-001/023/025；V02.1/03/08；DQ-02；P02/07/13。  
  **完成判据**：在B自身路径构建上述项目及真实传递依赖，不全解决方案构建/测试。由B0中011 joint完整树形成before输入：budget schema1.1、预算1、模拟1（模拟schema1.0）、plcPoll50；SchemaRoot=B/specs/001.../contracts，ConfigRoot=R/inputs/before/config。harness必须由B测试DLL定位B，Host/VirtualPlc及StorePrep也必须从B自身完整源码构建并实际使用B产物（包含建库及配方种子两次工具调用），日志记录绝对工具DLL及摘要，禁止回用A产物；核环境和Start请求的public/budget/simulation引用、所有输入摘要、Python/算法/媒体与隔离存储路径。不得拿A新schema替before。此任务不运行代表链。  
  **必要证据**：R/before/build.log、build-manifest.json、input-manifest.json、resolved-roots.json；包含DLL/依赖及schema摘要，证明不是仅更换cwd。

- [X] T008 取得唯一一组改前空闲及run-2完整链证据。文件/交付物：`scripts/workflow/plc_polling_013.py`、`backend/tests/Gaode.Integration.Tests/Station01/ThreeStageMainFlowIntegrationTests.cs`、`R/before/idle/`、`R/before/run-2/`。

  **依赖**：T007。**追溯**：FR-001/023/025；AC-01/11；SC-002/005/006；V01.1/03/08；P02/07/08/13。  
  **完成判据**：在静默外部负载下经before子阶段执行就绪后5秒预热、一次[t0,t0+60s)空闲，再调用现CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite同一run-2一次。真实VirtualPlc/采集/算法/配方保存/匹配/冻结/取放提交/Final完整发生，按V03保留边界及失败请求。仅按A共同资格评价，旧基线不要求1110、新周期/75ms、单源或循环零准备。未具备前提记具体NotRun并保持本任务未完成；实际失败保留原结果而不计合格基线，不循环择优。  
  **必要证据**：R/before/idle/metrics.json、run-2原日志/TRX/真实持久引用及qualification.json；合格完成后形成before-result.json。此任务不依赖任何after实现/验收；T009可在基线已保留后独立推进，但T030必须等本任务取得有效证据。

- [X] T009 迁移优化侧预算模型、准入、活动配置和所有直接构造消费者。文件/交付物：`backend/src/Gaode.Domain/Configuration/BusinessBudget.cs`、`backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs`、`backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs`、`backend/src/Gaode.Application/Configuration/ConfigurationFreezer.cs`、`backend/src/Gaode.Application/Recipes/RecipeApplicationCoordinator.cs`、`backend/src/Gaode.Infrastructure/Configuration/ApprovedExecutionCostProvider.cs`、`specs/011-plc-interaction-update/examples/joint/config/budget.json`、`specs/011-plc-interaction-update/examples/joint/config/simulation.json`、`specs/011-plc-interaction-update/examples/joint/run-1.json`、`specs/011-plc-interaction-update/examples/joint/run-2.json`、`specs/011-plc-interaction-update/examples/joint/input-manifest.json`、`backend/tests/Gaode.Contracts.Tests/Configuration/Station01RunConfigurationTests.cs`、`backend/tests/Gaode.Contracts.Tests/Recipes/JointInputDefinitionTests.cs`、`backend/src/Gaode.Application/Workflow/RecipeExecutionBudget.cs`。

  **依赖**：T006。**追溯**：FR-001/017/021/027/028；SY-02/07；DQ-02；V02.1；P01/05/07/08/11。  
  **完成判据**：在T002已对齐schema后删除BusinessDurations.PlcPoll，更新两处1.1运行准入为2.0及全部直接构造调用/必要活动fixture；预算同id升2，模拟实例升2且budgetRef升2，模拟schema仍1.0，run-1/run-2的budgetRef/simulationRef随之更新。Host/Start/fixture一致，loader严格拒绝废字段、不自动补值；RecipeExecutionBudget继续核引用/摘要，历史Reader读取旧BudgetSource不改。其他业务预算/limits/模拟时序/public及来源保持；HeartbeatFlip仍用，不能删除。run-1只迁移引用不增加完整链。全部必要构造/旧版本预期必须在T026首次after构建前齐备，不保旧运行兼容旁路。  
  **必要证据**：R/configuration-migration.json逐字段差异和消费者清单、活动配置新摘要；配置义务准备好不等于通过加载/冻结验证，后者由T028及真实链证明。B/B0与before输入不修改。

## Phase 3：US1 待机负载降低且能及时进入动作（P1）

**目标**：实际闲档降频、活动及时切换、独立心跳按原3秒失效。  
**独立验收**：I-ACQ/I-HB真实请求与原期限保护通过；T008/T029的唯一空闲窗口承担比较，不另开故事专用60秒窗口。US1仅完成代码时还不能称故事/013运行通过。  
**模型/合同**：D01—04、A01—03/A05、V04/V06；共用合法计划先落在最早使用它的US1，US3复核准备/非法映射义务。

- [X] T010 [P] [US1] 准备空闲、切档与心跳必要行为用例。文件/交付物：`backend/tests/Gaode.Communication.Tests/Devices/PlcPolling013AcquisitionTests.cs（新增）`、`backend/tests/Gaode.Communication.Tests/Devices/T065OriginalDeadlineTests.cs`、`backend/tests/Gaode.Communication.Tests/Devices/HeartbeatInterlockTests.cs`。

  **依赖**：T009。**追溯**：FR-002/003/005/007—009/017；AC-01—03；SC-001/004；P04/06/09/13。  
  **完成判据**：复用原TCP/可控时间夹具，定义I-ACQ以及I-HB表列数据行：真实闲档、未完动作/复位/握手不误闲、动作立即唤醒、单源、前端2秒查询不新增PLC请求、2999/3000ms和慢响应跨原截止；断言实际交换/派发而非配置字符串。只准备测试代码和静态case说明，不要求T012/T013尚未实现时先通过；不改PLC动作保持时长。  
  **必要证据**：R/cases/us1-definition.json列义务→实际方法/dataRow和独立预期，T024统一入manifest，运行证据在T028/T029。

- [X] T011 [P] [US1] 实现准入冻结及有限合法读计划复用。文件/交付物：`backend/src/Gaode.Plc.Protocol/ProtocolDefinition.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PreparedPlcReadPlans.cs（新增）`。

  **依赖**：T009。**追溯**：FR-010—012；AC-10；SC-003；A01/02；D01；P01/04/05/07。  
  **完成判据**：准入校验后复制冻结映射，生成A02有限用途/轴子集计划；绑定AdmissionId、版本/映射摘要、字段/方向，重连只换观察代次，换定义重新准入。热循环不再ReadPlan/ValidatePlan或排序分组；只读明确定义合法连续块。保留I完整初始检查十块、写方向/宽度约束、非法定义拒绝及不同映射计划通信前拒绝；块读不当原子快照。向T012提供每PDU执行/游标接口，不把整轮锁成不可插入工作。  
  **必要证据**：R/implementation/plan-binding.md及准入准备计数接线；实际12条准入行、热循环零准备和跨映射拒绝由T019/T028核验。

- [X] T012 [US1] 实现单源采集策略、分块观察及有限调度。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Polling.cs（新增）`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcAcquisitionPolicy.cs（新增）`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcRuntimeOptions.cs`、`backend/src/Gaode.Host/Composition/Station01Registration.cs`。

  **依赖**：T011。**追溯**：FR-003—010/013/014/022；AC-01/02/04/09；SC-001/003；A01—04；D02—05；DQ-01；P04/05/06/07/11。  
  **完成判据**：装配并冻结plc-acquisition/013-1：H300，B活动200/闲500，P运动/未知500与确认非运动1000，当前F/U/T200。依实际未完工作切档，单业务连接每PDU仲裁、独立心跳连接保留，不加执行器。实现A03全部有限K/R轮转、B/反馈轮转、P两R槽后服务、快读插入不重置服务计数、所有权撤销旧未发块、周期从首发送起算和missed丢弃；每组单轮，版本通知多消费者而非缓存Delay循环。内部ReadStamp/GroupObservation保留每块真实时间/代次/可靠性，并提供A06局部需求接口；活动反馈消费者在T016/T017接入后才闭合。原PollMs的最后消费者必须随T025清除，禁止对外保留选择旧行为的开关。  
  **必要证据**：R/implementation/acquisition-ownership.md逐字段对应A01的44信号与唯一来源；d/q/sᵢ/eᵢ/gᵢ/p、轮次C和最旧age实际计量点；尚未实际运行的时效明确NotRun。

- [X] T013 [US1] 接入独立心跳原截止及即时启动、停止、复位派发。文件/交付物：`VirtualPlc/ModbusTcpServer.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/HeartbeatDiagnosticWindow.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs`。

  **依赖**：T012。**追溯**：FR-002/006/007/009/017/020；AC-02/03；SC-001/004；A05/07；DQ-01；P04/06/09。  
  **完成判据**：保持独立H300与真实翻转后立即应答；会话初值同步单列、原3秒单调失效监视不等慢读。响应到达先核原截止再接受边沿，迟到值不复活旧动作；Reset不借新会话恢复旧资格。Start触发/写后基础、必要clear和授权Stop走即时K机会，I/O从入队受原窗口限制；不等整轮或旧闲计时。心跳相对plannedDue诊断替代旧正常间隔误报，同时保留慢交换/失效日志。  
  **必要证据**：R/implementation/heartbeat-deadline.md及两种心跳延迟计量接线；I-HB/T065原分支、启动/停止/复位实际观察由T028，常态由T029。

## Phase 4：US2 降频后仍只凭本次可靠观察推进（P1）

**目标**：单源动作反馈、独立观察身份、短态/到位坐标与真实取料保存门闭合。  
**独立验收**：I-AX/FU/TR/SAVE/OBS/FAST/PROJ及N2，以实际TCP/设备变化、无错误后继和真实提交核验；完整业务证据引用同一T029 run-2，不加配方矩阵。  
**依赖**：US1的策略/通知/心跳代码，不能复制另一动作执行器；D02—07、A04—08。

- [X] T014 [P] [US2] 准备本次动作、短态、坐标与保存门必要用例。文件/交付物：`backend/tests/Gaode.Communication.Tests/Devices/IndependentAxisTests.cs`、`backend/tests/Gaode.Communication.Tests/Devices/ActionHandshakeTests.Flip.cs`、`backend/tests/Gaode.Communication.Tests/Devices/ActionHandshakeTests.StageMigration.cs`、`backend/tests/Gaode.Communication.Tests/Devices/ActionHandshakeTests.StaleStages.cs`、`backend/tests/Gaode.Communication.Tests/Devices/ActionHandshakeTests.SaveFailure.cs`、`backend/tests/Gaode.Communication.Tests/Devices/PlcPolling013ObservationTests.cs（新增）`。

  **依赖**：T013。**追溯**：FR-014—018/025；AC-04—08；SC-004；V07；DQ-01；P04/07/08/13。  
  **完成判据**：按下方I-AX/I-FU/I-TR/I-SAVE/I-OBS/I-FAST登记实际case/dataRow。复用轴150、Flip/PutBack80ms时序，新增三个首态行核200可能跨过时安全拒绝、有限50实际观察、同批全部轴齐后退出、取消/未知不延续快档；run-2 PutBack150由代表链覆盖。I-OBS兼作N2真实新心跳+旧位置后继拒绝，旧完成由既有三行覆盖。I-FU将HeldFlip改为正常降频失败保存义务：保持原5000ms窗口、FlipFeedbackHold和模拟时序，核真实SQLite失败行、Action/Run/Operation关联、无完成/PutBack后继；实际未达1024不要求checkpoint或precedingEvidenceReferences。已提交段接续另由I-SEG承接，不通过提高流量制造HeldFlip分段。保留取消/原期限及实际取料/原始证据/有效在途提交后才Place的断言；定义先行，不要求未实现逻辑提前通过。  
  **必要证据**：R/cases/us2-definition.json逐行义务、读窗/设备变化预期与无后继断言；不新增全故障矩阵，实际结果由T028。

- [X] T015 [P] [US2] 发布基础与位置独立语义并接通直接消费者。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Semantics.cs`、`backend/src/Gaode.Domain/Station01/AxisObservationProjectionBuilder.cs`、`backend/src/Gaode.Host/Api/DeviceSemanticProjection.cs`、`backend/src/Gaode.Application/Station01/RuntimeObservationProjection.cs`、`backend/tests/Gaode.Contracts.Tests/Station01/RuntimeObservationProjectionTests.cs`。

  **依赖**：T013。**追溯**：FR-014/021/028；AC-05/06；SC-004；A04；D06；SY-03/05/07；P05/07/12。  
  **完成判据**：用每组真实依赖生成Identity；Position按最旧位置块判龄，新H/base不能续位置，位置Stale不伪造断线，基础过期不放行，原max(500,IoTimeout×5)不变。Host只增加SampleStartedUtc/SampleEndedUtc/ConnectionEpoch/Reliability四字段，live 1.1→1.2；Domain形状和持久plc-evidence/1不变。Axis投影使用Position.Identity.Reliability，历史投影沿已存事实；仅必要消费者改动，无影响者登记沿用。复用I-PROJ指定一例承接独立时间和已提交实际位置，不扩页面。  
  **必要证据**：R/implementation/semantic-consumers.md、字段/消费者差异；I-OBS、I-PROJ与T029最终位置/2秒API证据，未暴露组名/地址/轮询参数。

- [X] T016 [US2] 将轴及翻转放回等待接入唯一观察源和有限首态快档。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Axes.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Stages.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`。

  **依赖**：T015。**追溯**：FR-004/006/010/015/016；AC-04—07；SC-003/004；A04/06；DQ-01；P03/04/05/07。  
  **完成判据**：保留011目标/start/clear顺序及每轴本次Moving→Arrived。首start完成激活唯一R0080快读、各轴只认自己命令后观察、同批全部齐后归还B200；F/U首Executing前同源50，观察后200。取消/截止/UnknownHeld结束专属快档但不误闲、不盲重发。到位后坐标开始晚于相关到位观察结束，最终子动作完整P两块先核用途并发布再完成，满足RecipeDetectionExecutor后续Observe；无旧轴反馈独立循环。保留完整当前/非当前轴准入检查，不恢复旧ACK。  
  **必要证据**：R/implementation/action-observation.md记录原循环→新观察消费及每轴因果界线；I-AX/I-FU/I-FAST实际行为与75ms联合目标在T028/T029核验。

- [X] T017 [US2] 将取放、捕获释放及真实提交门接入分组观察。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Stages.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Acquisition.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.Transfer.cs`。

  **依赖**：T016。**追溯**：FR-004/006/010/018/019；AC-04/05/08；SC-004/005；A04/07/08；P03/04/07/08。  
  **完成判据**：T每200ms按Sorting反馈→完整P两块，暂停独立P，保持每轮XY/GrabZ保护；用于完成的坐标晚于完成反馈。六段取放、最终Lift/Idle清零不删，实际取料→原始证据真提交→有效IPickCommit→才放料。捕获释放即时取得新B、必要保存仍等有效提交，不靠新时间或后台投递放行；完成后恢复P实际到期，不拉长旧闲计时。通信不接管业务保存决定。  
  **必要证据**：R/implementation/transfer-save-gate.md及I-TR/I-SAVE、原始提交/在途回执/无Place证据；完整同run冻结和Final由T029。

- [X] T018 [US2] 闭合所有等待返回与后继派发的原资格检查。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Axes.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Stages.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.Transfer.cs`。

  **依赖**：T017。**追溯**：FR-006/007/009/017/018；AC-02/08/09；SC-004；A03.3/04/05；P04/06/07/08。  
  **完成判据**：逐一承接原Start/Reset/CompleteInspection、轴/翻放/取放和必要保存的取消、原绝对截止、epoch、Action/Attempt/实体资格；版本通知只唤醒复核，个人取消不取消他人合法共享采集。I/O排队与保存消耗原剩余期限，结果未知不继续、不盲重发；按需仅缺同资格证据时读，不把轮询改名为前核查。无第二业务执行路径或Test特权。  
  **必要证据**：R/implementation/wait-obligations.md逐等待点→资格/承接函数/用例映射；取消、过期释放、慢请求和真实提交三行由T028，不能仅源码搜索判通过。

## Phase 5：US3 通信变慢时不积压、不伪造节流收益（P1）

**目标**：慢轮丢弃、等待不重复唤醒、计划复用/真实证据负担可核查，原截止保持。  
**独立验收**：I-SLOW/I-MAP/I-GAP、复用分段/保存用例及SC-006实际指标；人造慢响应只测原保护，正常时效依T029固定负载，不能凭少请求通过。  
**依赖**：US2当前动作/等待完成承接后，再将实际观察接入有界诊断/证据；D07、A03.1/4/A08、V01/V06/V07。

- [X] T019 [P] [US3] 准备慢轮、映射和原始证据缺口的最少组件。文件/交付物：`backend/tests/Gaode.Communication.Tests/Devices/PlcPolling013SlowCycleTests.cs（新增）`、`backend/tests/Gaode.Communication.Tests/Devices/ProtocolDefinitionAdmissionTests.cs`、`backend/tests/Gaode.Communication.Tests/Devices/FailureEvidenceTests.cs`。

  **依赖**：T018。**追溯**：FR-011—013/017/019/020；AC-09/10；SC-003/004/006；P04/06/08/09/13。  
  **完成判据**：复用现TCP故障代理/时间设施，定义I-SLOW慢轮无并发积压/无追赶/多消费者无缓存重复唤醒，原期限独立；I-MAP保留12行并补有限计划准备一次/跨映射拒绝；I-GAP只补一个Recorder原始缺口拒绝；另以I-SEG一个受控组件验证生产自动1024阈值和真实已提交段接续：实际TCP交换到1023时调用生产分段入口无段，新增第1024交换后同入口自动提交，继续实际交换并走生产失败记录路径；真实SQLite核段引用、字节、关联及不声明完成。独立组件不在性能代表链加流量、不延长动作窗口；可将现分段游标/阈值/记录提取成通信证据内部组件供设备和该测试共同调用，不用force、伪引用或直接写结果行。虚构正常性能阈值不套人造慢响应；不扩到长期压测/所有存储异常。仅完成测试定义，T028才运行并据事实判定。  
  **必要证据**：R/cases/us3-definition.json：精确行、独立请求预期/提交拒绝断言；I-FU正常低量失败、I-SEG自动阈值接续和I-GAP缺口拒绝分别登记，不能互相抵扣，不增加完整链。

- [X] T020 [P] [US3] 落实完整时间与直接负担的有界计量。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Polling.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/HeartbeatDiagnosticWindow.cs`。

  **依赖**：T018。**追溯**：FR-013/019/020/022—024；AC-03/09—11；SC-002/006；A03.1/4；D07；DQ-01；P06/07/09/13。  
  **完成判据**：使用T005同一计量机制，优化代码挂接真实d/q/sᵢ/eᵢ/gᵢ/p、连接代次/唯一owner、最旧依赖年龄、missed及观察版本；完整C包含块间竞争/发布，不能拿6×25替代。按V01记录重复缓存唤醒、固定准备、受影响字节/处理量、单位流程通信证据负担；失败首次请求留原桶，额外失败路径单计，总量不漏不重。正常固定维度聚合，异常保留必要窗口/结构化诊断，计量不增加控制等待、高频序列化、CPU/RSS平台或运行调参。  
  **必要证据**：R/measurement-definition.json追加优化挂点与前后字段映射，T005共同补丁原文不变；实际计数交叉核验/容量窗口及SC-006由T028—T030。

- [X] T021 [US3] 承接真实动作证据、缺口、分段及历史读取。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.FailureEvidence.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Acquisition.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/CommunicationEvidenceRecorder.cs`。

  **依赖**：T020。**追溯**：FR-018—020/027；AC-08/10/11；SC-004/006/008；A08；D07；P07/08/09。  
  **完成判据**：DeviceActionEvidence引用实际反馈/坐标身份而非最后base时间；保留8192原始窗口、gap/空证据拒绝、1024翻转段阈值、取料前后/采集释放段及真提交预算。减少源头交换及不变计划处理，不删诊断或改为不等待保存；CommunicationEvidenceReader及RecipeApplicationHistoryReader旧schema/摘要原文沿用，不迁移库或补造旧时间。仅实际命中的证据拼接改动；I1允许在本文件内部提取可测试的分段状态/提交边界，设备与I-SEG调用同一生产自动阈值、Recorder及失败接续路径。阈值保持1024，通信真实原始字节来自TCP缓冲，只有实际有效提交回执才推进游标/引用；共同业务层无测试分支或产品测试开关。  
  **必要证据**：R/implementation/evidence-obligations.md：旧窗口/段链/历史义务→承接点；I-FU正常HeldFlip失败保存、I-SEG自动1024触发及真实提交接续、独立I-GAP缺口拒绝、真实提交数据行及T029持久引用，不以保存API调用次数代替提交。

## Phase 6：US4 维护者能复核净收益和必要验证完整性（P1）

**目标**：明确两侧配置语义、独立预期/计量比较、013入口及最小负例，删除旧旁路。  
**独立验收**：A/B/C判据、N1—N3、完整发现/执行/原报告及清理映射成立；真实净收益由T030消费双方证据，不以比较器编写完成代替SC通过。  
**依赖**：US1—3实现/用例定义及共同before接线；V01.1/02.1/04—10。清理必须在最终构建/取证前。

- [X] T022 [US4] 实现两侧配置映射和可复核对照计算。文件/交付物：`scripts/workflow/plc_polling_013.py`、`R/inputs/after/run-2.json`、`R/configuration-map.json`、`R/compare.json`。

  **依赖**：T021。**追溯**：FR-001/022—025；AC-11；SC-002/005/006；V01.1—06；DQ-01—03；P01/07/13。  
  **完成判据**：由B0原输入树形成after副本，仅应用V02.1批准字段差异；校核T009当前活动配置同一迁移、两侧schemaRoot/configRoot及预算/模拟/Host/Start引用，保存原文件与规范化摘要/快照对应，中性补丁不混迁移。实现比较器：A双方真实完成与可比性；B仅after新周期、1110/活动公式、283按需保守预算、单源/零循环准备、V06联合时效；C空闲率/完整流程总事务下降、单位动作报告、SC-006变化及T_after≤T_before+4950。25/75/完整发布不能相互推导或当硬件保证；失败/缺口不滤掉。固定21/33/108只在测试预期，必要写不机械减。  
  **必要证据**：R/configuration-map.json、比较器及预算分解可审阅；缺任一侧原始结果时输出NotRun/不可判，绝不生成虚假Passed，真实比较执行留T030。

- [X] T023 [US4] 接通三类最小负例与真实拒绝证据。文件/交付物：`backend/tests/Gaode.Communication.Tests/Devices/PlcPolling013NegativeTests.cs（新增）`、`backend/tests/Gaode.Communication.Tests/Devices/PlcPolling013ObservationTests.cs`、`scripts/workflow/test_plc_polling_013.py（新增）`、`scripts/workflow/plc_polling_013.py`。

  **依赖**：T010、T014、T019、T022。**追溯**：FR-021/026；AC-12；SC-007；V07/09；P05/07/13。  
  **完成判据**：N1用测试侧受控重复读取变体使同反馈出现第二持续源，实际TCP计数/归因/预算校验必须拒绝，产品不留开关；N2直接引用T014的I-OBS实际旧缓存拒绝，不重建同义测试；N3复用G02/G03核对器，对本attempt冻结的组件子清单先用真实完整账本判合格，再删除一条指定实际已执行行，其余原报告不变，必须拒绝。子清单执行前冻结且不含N3自身，避免自依赖/未执行项已失败导致假负例。禁止参数/源码字符串/被测自报统计替代证据。  
  **必要证据**：R/negative-definitions.json明确原始合格对照、受控缺陷、拒绝点和被移除case/dataRow；T028获取三类真实拒绝结果。

- [X] T024 [US4] 迁移受影响架构断言并完成必需项实际接线。文件/交付物：`backend/tests/Gaode.Rules.Tests/Architecture/ProtocolBoundaryChecker.cs`、`backend/tests/Gaode.Rules.Tests/Architecture/ProtocolBoundaryTests.cs`、`backend/tests/Gaode.Rules.Tests/Architecture/009-public-shapes.json`、`backend/tests/Gaode.Rules.Tests/Architecture/009-test-obligations.json`、`backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json`、`scripts/workflow/013-required-cases.json`、`scripts/workflow/verify_entry.py`。

  **依赖**：T023。**追溯**：FR-021/025—028；AC-12；SC-007/008；SY-02/04；V09；P01/05/13。  
  **完成判据**：只更新通信装配选项白名单及已删PlcPoll的公开形状/义务承接，业务层仍不得含协议知识或新调度参数。按实际新增/命中的013合同、C#、Python和本地helper/直接调用逐文件登记009-boundary-inventory.json真实职责、scanRequired及分类依据，在T027前完成；不关闭CHECKER-INVENTORY/A10、不扩排除、不降低扫描或添加豁免，不把业务消费者归为通信实现。将T010/14/19/23定义绑定到真实case/dataRow和报告路径，保留完整010 L现70行（27 dotnet、7脚本、36自校验），受影响009正负行在运行前冻结。013必需项含before/after/cross及组件账本，不以零发现/过滤缩小集合；相同身份和attempt证据可引用一次，009 fallback不算013。  
  **必要证据**：R/required-cases.frozen.json最终冻结副本及同义项去重表、R/implementation/boundary-obligations.md；实际发现/执行在T026—T030核对。

- [X] T025 [US4] 在最终取证前删除全部被替代路径并完成义务承接核对。文件/交付物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Axes.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Stages.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.Transfer.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/PlcRuntimeOptions.cs`、`backend/src/Gaode.Host/Composition/Station01Registration.cs`、`backend/tests/Gaode.Communication.Tests/Devices/ProtocolTcpFixture.cs`、`backend/tests/Gaode.Communication.Tests/Devices/ProtocolTcpFixture.Formal.cs`、`backend/tests/Gaode.Integration.Tests/CommunicationFixtures/DetectionCommunicationFixture.cs`、`scripts/start-station01-virtual-loop.ps1`、`scripts/verify-q01-q02-test-page.ps1`、`backend/tests/Gaode.Communication.Tests/Devices/VirtualPlcLatestProtocolTests.cs`、`backend/tests/Gaode.Communication.Tests/Devices/T065OriginalDeadlineTests.cs`、`backend/tests/Gaode.Communication.Tests/Devices/HeartbeatWindowDiagnosticsTests.cs`。

  **依赖**：T024。**追溯**：FR-010/013/021/027/028；AC-10/12；SC-008；plan清理表；P01/04/05/07/13。  
  **完成判据**：替代义务已由T011—T021承接后，删除旧Pump整体高频、Axes/Stages重复源、Reset/AdvanceStart/CompleteInspection缓存轮询、StagePollMs/PollAsync、PollMs及Test50/其他25装配和动态计划热入口；删除失效断言但将有效行为迁到已列case。定向查所有实际PollMs/PlcPoll构造、脚本当前hostPollMs描述和schema/fixture引用，必要签名迁移在首次after构建前完成；历史报告/任务/失败及旧只读JSON不改。不得注释关停或保留可切回的兼容旁路；不改VirtualPlc扫描/时序及ThreadPoolRuntimePolicy。  
  **必要证据**：R/retirement-map.json逐旧成员/调用点→新保护/历史读取/实际验证项；当前活动消费者无废字段，源码检查只是清理证据，行为通过另由T028/T029。

## Phase 7：收尾——最小构建、必要验证与实际软件收敛

默认只启动一次013最终入口，由同一attempt依次承担T027→T028→T029→T030并引用已完成T008；不对每个任务再手动重复完整入口/链。T027完整L保留在外层，组件和链随后运行。若任一前置确实缺失，记录相应未完成，不能将汇总Passed作为运行启动条件。

- [X] T026 完成首次优化侧受影响工程构建与最终身份冻结。文件/交付物：`backend/src/Gaode.Host/Gaode.Host.csproj`、`backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj`、`backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj`、`backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj`、`backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj`、`VirtualPlc/VirtualPlc.csproj`、`R/after/build-manifest.json`。

  **依赖**：T025。**追溯**：FR-001/023/025/027；SC-005/007/008；V02.1/09.1；P01/07/13。  
  **完成判据**：先核T009配置/共享构造与T025轮询参数/夹具清理全部完成，再构建受影响项目及必需传递依赖；未改VirtualPlc也只为本次链取得明确产物身份，不改其源。复用同身份已有构建，不全解决方案验证。冻结A源码/中性补丁/策略/schema/fixture及DLL摘要，静态发现实际方法/dataRow与T024清单核对，缺项修复接线不得缩清单。此步只保证构建/发现及运行输入可用，不要求软件已通过验收才允许后续运行。  
  **必要证据**：R/after/build.log、build-manifest.json、discovered-cases.json及final-source-manifest.json；与B构建/schema根明确隔离。

- [X] T027 执行或引用最终身份的完整010轻量L和受影响009边界。文件/交付物：`scripts/verify.ps1`、`scripts/workflow/recipe_execution_010.py`、`scripts/workflow/010-lightweight-cases.json`、`backend/tests/Gaode.Rules.Tests/Architecture/ProtocolBoundaryTests.cs`、`R/gates/`。

  **依赖**：T026。**追溯**：FR-021/026；AC-12；SC-007；V09/09.1；P05/13。  
  **完成判据**：通过013最终入口保持原run_with_lightweight顺序，完整L现70行全部核实际发现/执行/原报告，追加T024冻结的受影响ProtocolBoundary正负例；ProtocolRepositoryBoundary若已在L执行只引用，不重跑。映射准入12行由T028执行并最终并入009相关义务，不重复启动。仅同最终A源码/构建/输入/补丁及同attempt证据可复用，before或历史旧L不能冒充。不运行010完整动态专项或009全部历史专项。  
  **必要证据**：R/gates/L/原TRX/脚本退出、自校验结果及009-boundary结果、身份/引用去重账本；门禁失败不缩范围或扩大业务白名单。

- [x] T028 执行最终身份的必要保护组件及三类负例。文件/交付物：`scripts/workflow/plc_polling_013.py`、`scripts/workflow/013-required-cases.json`、`R/components/`、`R/negatives/`。

  **依赖**：T027。**追溯**：FR-002—021/025/026；AC-02—10/12；SC-001/003/004/006/007；P04/05/07/08/09/13。  
  **完成判据**：仅运行下表冻结的实际case/dataRow及直接预算/投影承接项，正向、原失败保护和N1—N3均核真实交换/设备时窗/存储回执/原报告。完整组件子账本形成后才做N3删除一行核验；其结果进入外层013总清单。相同最终身份已执行项只引用一次。人造慢响应只验原保护、不套正常性能阈值；轴150/翻放80保持，漏中间态不判成功。必要日志能定位阶段/动作/处置，所有取消/未知后继与真保存门均有实际断言。  
  **必要证据**：R/components/原TRX/时窗/提交引用、R/negatives/N1—N3及discovered-executed ledger；任何必需行NotRun/Skip/失败不充通过，正常组件通过不代替代表链。

- [X] T029 取得唯一一组优化后空闲及相同run-2完整链证据。文件/交付物：`scripts/workflow/plc_polling_013.py`、`backend/tests/Gaode.Integration.Tests/Station01/ThreeStageMainFlowIntegrationTests.cs`、`R/after/idle/`、`R/after/run-2/`。

  **依赖**：T028、T022。**追溯**：FR-001—010/014—025；AC-01—08/11；SC-001—006；V03—06/08；P02/03/04/07/08/13。  
  **完成判据**：使用T026最终A构建和T022 after输入，串行执行与before同条件的5秒预热+一次60秒空闲及同一代表方法/run-2一次。真实独立VirtualPlc/采集/算法、配方保存/F匹配/运行快照、21轴批/33轴启动/108核心写及7媒体/9算法/NG保存/Final按冻结预期核验；禁止写成业务常量。空闲≤1110且V06.2硬条件成立，普通工程时效完整报告而不一票否决，活动按实际阶段公式/按需写/失败全计，记录真实状态年龄、完整发布、两类心跳延迟和负担。启动只需构建、接线、合法输入及必要保护已具备，不要求最终净收益/compare先通过。  
  **必要证据**：R/after/idle/metrics.json、run-2原日志/TRX/设备变化/持久引用及qualification.json；不复跑run-1，不因已有共享链证据再手工启动一条。失败保留，按已定位原因仅补必要项。

- [X] T030 执行最终净收益比较、完整性核对并记录013软件收敛结果。文件/交付物：`scripts/workflow/plc_polling_013.py`、`R/compare.json`、`R/final-report.md`、`specs/013-plc-polling-optimization/tasks.md`。

  **依赖**：T008、T029。**追溯**：FR-001/019—028；AC-11/12；SC-001—008；V01.1/09/10；DQ-01—03；P01/07/08/09/13。  
  **完成判据**：消费双方实际证据、最终L/009/组件/三负例和清理映射，逐项核A共同资格+B仅after+C跨侧目标；空闲事务率及同完整流程总事务严格下降，单位动作必要增读有说明，SC-006直接负担和真证据覆盖成立，T_after≤T_before+4950且原期限均保持。核必需→发现→执行→原报告，所有结果身份正确、没有漏项或重复计数。只有按013-acceptance/2分级的SC-001—008全部适用硬义务满足，且性能观察/现场限制已如实列明，才能宣布通信降载专项软件收口并勾实际完成任务；未满足项保留未完成/真实结果及定位，不倒改门槛。记录正式外部NotMeasurable与运行NotRun，不宣布生产通过。  
  **必要证据**：R/compare.json、final-report.md的需求/数据行/原始证据索引及未完成项；源/输入若变化，保留原失败和变更影响记录，只补受影响侧/项，不循环挑最好整链。



## 必需用例与数据行承接（供T006冻结、T024绑定、T028执行）

下列是义务清单，不是本轮执行记录。现有方法保留语义、仅调整被替代的读取方式和断言；同一case可以覆盖多项义务且只运行一次。新增项先冻结稳定的义务ID/数据行和独立预期，在T024绑定实际全名，不得根据发现结果缩减。下表文件均为上述任务列出的实际文件；Devices类位于Gaode.Communication.Tests.Devices，合同类位于Gaode.Contracts.Tests对应命名空间。

| 义务 | 必需case / dataRow及独立预期 | 定义→执行/复用 |
| --- | --- | --- |
| I-ACQ | 新PlcPolling013AcquisitionTests：真空闲分频、未完动作/复位/握手不误闲、立即切档、共享反馈单源和2秒API查询不触采集；以实际TCP时间线/事务为依据 | T010→T028；长窗口仅T029 |
| I-HB | T065OriginalDeadlineTests.RealOriginalDeadlineStillLatchesAndRejectsNewMotion的false/true两行；HeartbeatInterlockTests.TwoThousandNineHundredNinetyNineMillisecondsDoesNotTrip、ThreeSecondsWithoutEdgeLocksActionsRaisesAlarmAndProjectsRestricted。覆盖2999/3000ms、原截止、慢/迟到响应不能先续期再判；不扩恢复专项 | T010→T028 |
| I-AX | IndependentAxisTests.ScanAndDetectionUseTheirOwnZAndCaptureReleaseWritesNoLegacyAcknowledgement；CancellationAtXyAcceptanceCannotDispatchDependentDetectionZ。承接本轴反馈、实测坐标及取消后不派发从属Z | T014→T028 |
| I-FU | ActionHandshakeTests.FreshFlipAndPutBackUseSeparateFeedbackAndPreparedProgram；UnrelatedOrOldFeedbackCannotAuthorizeFlip的old-complete、stale-epoch、wrong-entity三行；SameTargetWithoutDistinguishableFeedbackIsUnknownHeld；原HeldFlipRetainsCommittedSegmentsWithoutDeclaringCompletion迁为HeldFlipPersistsFailureWithoutDeclaringCompletion：原5000ms窗口/故障/时序，实际失败持久化及关联、无完成/无PutBack；低于阈值不要求分段引用 | T014→T028；分段接续独立I-SEG承接 |
| I-TR | ActionHandshakeTests.InitiallyMatchingPickPointCannotSurviveLaterWrongXy；MissingTargetStageObservationNeverSubmitsPlaceOrSortingOk；ExpiredWindowCannotReleaseCommittedCapture；SortingMotionCannotConsumeTheLongerTrayDeadline；SortingPickPlaceRequiresRealInTransitCommitAndFinalLift | T014→T028；真实取放链复用T029 |
| I-SAVE | ActionHandshakeTests.ActualPickAndDatabaseOutcomeRemainSeparateFromPlacePermission三行：(rollback-before-commit,false,false)、(committed-receipt-withheld,true,false)、(committed-temporarily-unreadable,true,true)，字符串均保留原PICK-STORE/前缀；实际已取料/已提交与当前可放料分别判断 | T014→T028 |
| I-OBS / N2 | 新PlcPolling013ObservationTests：新心跳不能给旧基础/位置续期、跨代次观察不得授权、到位后坐标因果/最终位置真实发布；N2直接复用“新心跳+旧坐标”实际拒绝派发证据。断言Position独立可靠性，普通位置陈旧不伪造设备断线 | T014/T015→T028，不重复建立N2 |
| I-FAST | 三个定向行：axis-150ms、flip-80ms、putback-80ms；各核原时序下200ms可能漏态时不得放行，有限50ms真实观察、同批全部轴观察齐后恢复200ms、退出和原截止。保留当前VirtualPlc扫描与时长；代表PutBack150ms由原run-2链承接 | T014→T028；代表时序T029，不增加完整链 |
| I-SLOW | 新PlcPolling013SlowCycleTests：慢PDU/跨轮请求无重叠追赶、等待新版本无重复缓存唤醒、取消/期限独立且后继不能越权；受控慢组件不套正常V06性能门槛 | T019→T028 |
| I-MAP | ProtocolDefinitionAdmissionTests.FormalEntryAdmission全部12行，见下表；增加最少固定计划一次准备/循环零准备/映射身份不混用的直接行为承接。非法映射须在实际通信前拒绝，已确认不连续布局合法拆分 | T019→T028；009复用同一结果 |
| I-GAP | FailureEvidenceTests中增加/调整一个CommunicationEvidenceRecorder原始证据缺口拒绝例；不得以监控游标gap代替原始持久证据缺口。保存门复用I-SAVE；独立缺口拒绝不能被I-SEG正常分段抵扣 | T019/T021→T028 |
| I-SEG | FailureEvidenceTests.AutomaticThresholdCheckpointPrecedesRealFailureContinuation：真实TCP交换1023→无段，1024→同一生产自动阈值入口/Recorder真SQLite提交，再后续真实交换及生产失败记录核precedingEvidenceReferences指向已提交段、关联/字节/无完成；内部组件层级，不宣称运行了业务Flip或用force验证自动阈值 | T019定义/T021生产边界→T028；不增加完整链，不改1024、期限或模拟时序 |
| I-CONFIG | JointInputDefinitionTests.CurrentJointInputsUseUniqueSharedDefinitionsAndLoadExplicitPoseBudgets；Station01RunConfigurationTests.FrozenSnapshotKeepsVersionsCapabilitiesPurposeAndClampBudget；定向承接2.0活动输入、引用/摘要核对及废字段拒绝。只补真实命中的加载/准入义务，不重建配置异常矩阵 | T009→T028 |
| I-PROJ | RuntimeObservationProjectionTests.AxisProjectionUsesCommittedActualValuesAndLeavesMissingAxesUnknown；承接新位置身份/可靠性及已提交实际值，不运行全部投影专项 | T015→T028 |
| N1 | 新PlcPolling013NegativeTests：测试侧注入第二持续读取来源，以独立TCP发送/原报告校验真实拒绝；合法对照必须成立，不能只依赖设备自报统计 | T023→T028 |
| N3 | 新scripts/workflow/test_plc_polling_013.py：冻结组件子清单（不含N3自身）在同attempt真实完整账本下先被接受，再删一条已执行case/dataRow，G02/G03必须拒绝；原报告保留，不能用本来就不完整的输入证明漏项识别 | T023→T028 |
| 010 L / 009 | 010-lightweight-cases.json现声明70项：27个dotnet项、7个脚本项、36个自校验项；不得缩为只跑ProtocolBoundary。按实际发现/dataRow展开核对；ProtocolRepositoryBoundary已在L覆盖的不再跑。009只跑T024冻结的受影响ProtocolBoundary正负例及复用I-MAP | T024→T027/T028；同身份同attempt只引用一次 |
| 代表链 | ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite，经RecipeExecution010RunHarness及同一run-2输入。before/after各一次，其他必要路径由组件补齐 | T008/T029，不再运行run-1或010动态全专项 |

I-MAP原12个实际数据行（不能用“1个Theory”代替12项）：

| dataRow | 预期拒绝原因；null表示合法准入 |
| --- | --- |
| PD-N01-overlap/same-area | FieldOverlap |
| PD-N02-width/float32-one-word | TypeWidthMismatch |
| PD-N03-bit/out-of-field | BitOutOfRange |
| PD-N04-owner/read-direction | ReadDirectionConflict |
| PD-N04-owner/write-responsibility | WriteResponsibilityConflict |
| PD-N05-required/missing | RequiredSignalMissing |
| PD-N06-range/address-capacity | AddressRangeInvalid |
| PD-N06-range/access-length | AccessLengthInvalid |
| PD-P01-area/same-number | null |
| PD-P02-width/float32-two-words | null |
| PD-P03-layout/noncontiguous-split | null |
| PD-P04-owner/confirmed-read-write-clear | null |

组件发现后记录实际case/dataRow、框架ID、过滤表达式、原报告和义务对应；参数化显示名变化不得丢行。以上清单不扩大为整个测试程序集执行。现有方法受013影响而改名/拆分时，T024保留原义务到新行的承接，不用方法数量抵扣。

## 配置、计量与判据的执行约束

本节摘录现有V01—V06执行要点，不替代合同或新增阈值。T006在任何测量前冻结，T022落实比较器。

| 装配项 | before（T007） | after（T009/T022/T026） |
| --- | --- | --- |
| 产品/schema根 | B自身源码/DLL；B/specs/001-station01-public-preparation/contracts | A自身源码/DLL；A下同相对路径 |
| fixture/config根 | R/inputs/before/run-2.json；相对config；Gaode__ConfigRoot=R/inputs/before/config | R/inputs/after/run-2.json；相对config；Gaode__ConfigRoot=R/inputs/after/config |
| budget.schema.json / budget.json | schema1.1，s01-budget-011-joint/1，plcPoll=50保留 | schema2.0，s01-budget-011-joint/2，删除plcPoll并严格拒绝废字段 |
| simulation.schema.json / simulation.json | schema1.0，s01-sim-011-joint/1，budgetRef版本1 | schema仍1.0，s01-sim-011-joint/2，budgetRef版本2 |
| Host/Start/fixture引用 | BudgetVersion=1、SimulationVersion=1；完整依赖树和各侧实际加载摘要 | 两者均2；当前run-1仅同步引用；schemaRoot、configRoot、budgetRef和实际快照一致 |
| 公共配置/其他输入 | public/1、配方/机械/媒体/算法、原预算/limits、模拟时序及负载 | 语义与before相同；public及不变输入原摘要相同 |
| 采集策略 | 原装配和原轮询，不能混入013策略 | 通信内部不可变plc-acquisition/013-1，由Station01Registration装配/准入冻结；不进入业务预算 |
| 摘要/补丁 | 原始文件SHA-256、规范化摘要、真实SnapshotId和同一中性补丁分别记 | 批准配置迁移单列；允许预算/模拟/schema摘要不同，不虚称文件字节全同或修改before适配新schema |

引用解析、端口和隔离存储按V02.1显式映射；worker脚本按各侧源码根加载且内容摘要相同。两侧运行前核实际DLL来源和加载器结果；单改cwd无效。历史BudgetSource/既存快照仍只读，不走新的运行准入，不批量升级历史JSON。

| 判据层 | 适用侧及冻结要求 | 任务证据 |
| --- | --- | --- |
| A共同资格 | 双方路线/完成量相同，真实设备模拟/采集/算法/保存/Final；原I/O、3秒、动作/受理/保存截止、取消与未知保护；输入语义和计量有效、证据完整。before不被要求通过013新观察形状/频率/单源 | T008/T029原始证据；T030核可比性 |
| B新目标 | 仅after：新周期/局部快档、1110空闲上限、活动及按需预算、V06联合时效、单源/零重复唤醒/零循环准备、新观察语义及新增门禁 | T028/T029；参数配置与源码字符串不能替代实际结果 |
| C净收益 | 空闲事务率及完整相同流程总事务严格下降；按种类报告每完成单位事务/必要增读；直接处理及通信证据负担；T_after≤T_before+4950ms | T030消费双方实际结果；不能作为T008/T029启动前置 |

V06的25ms到期→首发迟延、单PDU交换≤25ms均为**after正常Test环境的独立目标**，不是PLC承载保证，更不能据此推导B六块完整发布≤150ms。完整C=p−s₁=Σ交换+Σ块间等待+发布开销；另报计划到期d、入队q、每块发送sᵢ/完成eᵢ、相邻块等待gᵢ、发布p和最旧依赖实际年龄。B/P/FU/H/T完整C的150/50/25/25/75ms及局部快档C≤25ms须各自实测满足；实际开始间隔、旧起点至新发布、关键即时核查分别按V06核对，不能以块数乘单块代替。

局部50ms的首个可靠发布≤75ms和随后p_next−s_prev≤75ms必须包含连接竞争、实际交换和发布；首次可能尚未观察到Moving/Executing，不能当作保护已满足。50+到期迟延+完整C≤75是联合目标，不能同时把两个25上限代入却声称保证75。正常Host观察翻转至应答完成≤50ms、可靠设备时基下翻转至应答≤400ms；无可靠设备时基则相应项不可测，不能以Host读取时刻冒充翻转时刻。人造慢请求仅检验原保护；before同样记录指标但不套新25/75等门槛。原状态失效阈值、原3秒及业务截止独立，不因统计超限放宽或重起。

A03有限仲裁的容量证据必须覆盖B、P、T、首态快档、按需读写的真实同时关系：T接管两个位置块时P暂停；轴快档接管B的R0080；F和U按当前阶段互斥。按业务连接计算平均服务需求，区分快态窗口竞争/截止与均值。单PDU25ms不足以证明高负载可行；合同示例5ms不是新增硬门槛。原有限K/R、B/反馈轮转、P服务机会和快档插入规则须以实际时间线核验，不新增通用调度平台。

预算只按V04/V05推导：空闲名义18.333/s，取放等待49.333/s，有限首态另计；60秒离散空闲上限726+122+201+61=1110。活动以周期、按需、业务写、心跳应答、未入前四桶的失败处置额外PDU五个互斥桶计数，阶段周期上界为blocks×(ceil(duration/period)+1)，切换不得重复计算共享来源。必要按需保守上界283按V05事件清单审计，108核心写不含所有额外交换；失败首次发送留原桶并全计。每种动作必要固定命令/清零不强求减少。21轴批、33轴启动、108核心写仅属于冻结run-2协议布局的独立审计预期，不写成业务常量；静态预期还包括7媒体/9算法及真实保存/Final。保持Motion500、Flip3000、PutBack150、Sorting3000、scan10、心跳翻转1000ms和jitter0，不用延长时序增加预算。

结果必须按V10分开记录：业务保护失败、after目标不满足、已定位环境/输入不具可比性、NotRun、正式设备信息不足的对应NotMeasurable。保护失败或改后超限不能随意改名“环境问题”；不可比时不判SC通过，也不自动加入线程池/GC或长压治理。原失败不删；仅有已定位原因/源码或输入变更时补受影响侧/项，不循环运行完整链挑最好结果。

## 需求、同步与审查问题覆盖

下表是任务到义务的静态追溯，不表示FR/AC/SC已经运行通过。实施完成以任务证据、实际case/dataRow及T030结果为准。

| FR | 实施/验证任务 | 直接承接 |
| --- | --- | --- |
| FR-001 | T001/T007/T008/T009/T029/T030 | 当前主项目与同条件真基线 |
| FR-002 | T010/T012/T013/T028/T029 | H300及原3秒 |
| FR-003 | T010/T012/T028/T029 | B200/500实际装配 |
| FR-004 | T012/T016/T017/T023/T028/T029 | F/U/T200、无动作停止、唯一来源 |
| FR-005 | T012/T015/T016/T028/T029 | P500/1000、未知运动归属 |
| FR-006 | T012/T016/T017/T018/T028/T029 | 关键即时观察与到位后实际坐标 |
| FR-007 | T012/T013/T016/T017/T028/T029 | 即时命令/清零/应答 |
| FR-008 | T005/T010/T028/T029 | 真实2秒API观察不触采集 |
| FR-009 | T010/T012/T018/T028 | 未完成工作判档与立即唤醒 |
| FR-010 | T011/T012/T016/T017/T023/T025/T028 | 单源及无第二执行器/连接 |
| FR-011 | T011/T019/T024/T028 | 合法计划有限准备/映射绑定 |
| FR-012 | T011/T015/T019/T028 | 合法连续块/非原子观察 |
| FR-013 | T012/T018/T019/T020/T025/T028 | 不积压追赶/不反复缓存唤醒 |
| FR-014 | T012/T014/T015/T018/T028 | 独立身份、最旧年龄/代次 |
| FR-015 | T014/T016/T017/T018/T028/T029 | 本次中间态→完成→坐标 |
| FR-016 | T014/T016/T028/T029 | 三个局部首态行及原时序 |
| FR-017 | T009/T010/T013/T014/T018/T028/T029 | 原期限、取消/未知不继续 |
| FR-018 | T014/T016/T017/T018/T021/T028/T029 | 真实坐标与取料提交门 |
| FR-019 | T005/T020/T021/T022/T030 | 源头减量与必要原始证据/真实保存 |
| FR-020 | T005/T013/T020/T021/T028 | 有界统计、诊断分类/持久关联 |
| FR-021 | T002/T003/T004/T015/T024/T025/T027 | 009/010边界及共同执行 |
| FR-022 | T006/T012/T020/T022/T028/T029 | 冻结完整时间、调度/时效 |
| FR-023 | T001/T005/T006/T007/T008/T022/T029/T030 | 双侧身份、按完成单位比较 |
| FR-024 | T006/T022/T029/T030 | 预算推导、初估不冒充硬阈值 |
| FR-025 | T008/T010/T014/T019/T023/T028/T029 | 一条代表链加必要组件 |
| FR-026 | T006/T023/T024/T027/T028/T030 | 完整L、受影响009及三负例 |
| FR-027 | T009/T021/T025/T030 | 实际删除/保护承接/历史保留 |
| FR-028 | T002/T003/T004/T009/T025 | 定向文档先行及历史不改判 |

| AC | 必需任务/执行点 | 关联SC |
| --- | --- | --- |
| AC-01 | T005/T010/T012/T008/T029 | SC-001/002 |
| AC-02 | T010/T012/T013/T018/T028 | SC-001/004 |
| AC-03 | T010/T013/T020/T028/T029 | SC-001/004 |
| AC-04 | T012/T014/T016/T017/T023/T028 | SC-003/004 |
| AC-05 | T014/T015/T016/T017/T028/T029 | SC-004/005 |
| AC-06 | T014/T015/T018/T023/T028 | SC-004 |
| AC-07 | T014/T016/T028/T029 | SC-004/005 |
| AC-08 | T014/T017/T018/T021/T028/T029 | SC-004/005/008 |
| AC-09 | T012/T019/T020/T028 | SC-003/006 |
| AC-10 | T011/T019/T021/T025/T028 | SC-003/006/008 |
| AC-11 | T001/T005/T006/T008/T022/T029/T030 | SC-002/005/006 |
| AC-12 | T002/T003/T004/T023/T024/T025/T027/T028/T030 | SC-007/008 |

| 同步/设计义务 | 具体先后关系 |
| --- | --- |
| SY-01 | T002先同步003当前分频/诊断约束→T012/T013；T010/T028承接T065原截止，旧50ms事实/失败不改 |
| SY-02及001预算 | T002更新009合法计划/分频和001预算合同/schema2.0→T009配置/全部直接消费者、T011计划；两侧根/摘要T007/T022核，首次after构建T026之前完成 |
| SY-03 | T003对齐009共享语义/原始证据及历史读取→T015/T021；保护I-OBS/I-GAP/I-SAVE在T028 |
| SY-04 | T004对齐010当前承接范围/共同执行/必需项→T006入口、T024清单→T027完整L；不重新运行010动态全专项 |
| SY-05 | T003对齐011 PC02—05与执行/状态合同→T016/T017/T018；动作次序/真坐标/提交门由T028/T029 |
| SY-06 | T004对齐011本次代表链与组件引用→T006/T008/T029；旧验证记录仅引用，不能充013证据 |
| SY-07 | T004在012 shared-integration登记实际Host输出/预算引用责任→T009/T015；无影响的spec/plan/tasks/editor-ui/API保存与冻结条款沿用，不机械重写或扩大页面验证 |
| DQ-01 | T012有限调度/来源切换→T016/T017首态/按需→T020完整时点→T022独立联合目标→T028/T029实际核验；不拿单块25推导整组或75保证 |
| DQ-02 | T001原基线→T002合同/schema→T0092.0/2/2迁移；T005共同中性补丁单列；T007/T022两侧引用/摘要/自身DLL；T025废字段清理→T026首次after构建 |
| DQ-03 | T006启动与结果分离/A-B-C冻结→T008只评A→T022分侧比较→T027/T028当前身份门禁→T029 after目标→T030跨侧实际结果/分类 |

SC-001由T028/T029实际周期/观察证据收口；SC-002由T008+T029→T030净事务与时长比较；SC-003由I-ACQ/I-MAP/I-SLOW/N1及T029；SC-004由表列保护组件及真实链；SC-005由双侧同run-2及实际保存/Final；SC-006由T020/T021及T030直接负担对照；SC-007由T027/T028/实际发现账本；SC-008由T025删除承接、T028失败证据和T030核对。不存在“设计条款已登记所以软件已通过”的路径。

## 依赖、并行与实施里程碑

主要依赖为：

- 共用前置：T001→T002→T003→T004→T005→T006。
- before证据分支：T006→T007→T008；没有任何after任务或SC通过作为其前置。
- after实现主路径：T006→T009→T011→T012→T013→T015→T016→T017→T018→T020→T021→T022→T023→T024→T025→T026→T027→T028→T029。
- 测试定义支线：T009→T010，T013→T014，T018→T019；三者均在T023合流，未实现能力不要求提前通过。
- 最终合流：T008与T029均取得真实证据后→T030；缺before时可推进after代码，但净收益仍不可判。T030的分类/比较启动依赖原始结果到位，不依赖T008/T029已标Passed或任务已勾选；失败结果也必须送入分类，缺一侧只可输出NotRun/不可判。只有双方A资格、after的B目标及C净收益成立，才可完成T030和宣称收敛。
- T026首次after构建的传递前置包含T002预算schema、T009全部加载/消费者/fixture迁移、T015输出消费和T025全部废字段调用清理；不能在迁移未齐时拿构建失败当功能验证。
- T027—T030是同一个最终profile内的顺序执行阶段，不分别再启动一次完整profile；本attempt同最终身份已跑门禁/组件只引用一次。启动资格检查读取构建/输入/接线/必要保护条件，最终比较才读取双方结果。

仅以下三对可并行（已标[P]）；其余按显式依赖串行或在不产生运行竞争的调度中安排：

| 前置完成后 | 可并行任务 | 无冲突依据与边界 |
| --- | --- | --- |
| T009 | T010 ↔ T011 | 前者仅US1测试文件，后者仅协议/访问器/准备计划；共同fixture改动由后续任务统一完成，不并行执行测试 |
| T013 | T014 ↔ T015 | 前者通信动作测试，后者语义/Host/Domain/Application及独立合同投影测试；不共同写ActionHandshake或采集实现 |
| T018 | T019 ↔ T020 | 前者慢轮/映射/缺口组件文件，后者通信计量实现；测试定义引用已冻结合同，实际运行统一T028 |

T007/T008与T009分支虽无数据依赖，但不标[P]：基线测量需要固定外部负载，禁止同时构建/运行另一分支。所有任务完成需自身判据和证据成立；只创建文件或开始工作不能勾选。

| 里程碑 | 最小交付/完成边界 |
| --- | --- |
| M0：可复核基线 | T001—T008完成，原未优化产品/完整输入可追溯、真实before结果可用；不表示任何013优化已交付 |
| M1：最小实施里程碑（US1优先） | T009—T013的配置迁移、准备计划、单源分频/调度与心跳代码完成；允许检视首要改动。US2动作消费者、清理及运行证据尚缺，不宣称US1已验证或013完成，不另加一轮60秒/整链 |
| M2：具备最终验证入口 | T014—T026完成，四故事实现、必要定义、profile/manifest、删除承接和首次after构建齐备；所有运行SC仍待T027—T030，代码交付不冒充软件收敛 |
| M3：013完整软件收敛 | 双侧合格一次空闲+同一链、必要组件/三负例、完整L/受影响009、最终身份执行核对、清理承接及SC-001—008适用软件义务均有实际证据；任何必需失败/Skip/NotRun不抵扣。正式PLC对应未测项仍保留，不宣称现场/生产验收 |

## OPEN、客户原型与停止边界

- 正式地址/映射、正式心跳周期、Moving/Executing最短保持、设备可靠时基和真实承载能力仍需对应现场资料/验证。仅限制各自正式分组预算、短态捕获或设备翻转延迟结论；当前明确Test软件任务可继续，不能编造输入或默认复用Test地址。
- 原稳定性待办和迟回执/瞬时阻断失败保留。若实际阻断本次路径，只按已定位原因补必要侧/项；不升级为全量恢复、线程池/GC、全配方或长期压力治理。
- 客户原型归档、页面和来源文档只读。013真实API观察器是后端负载观察，不提供页面ready，不抵扣011/012页面证据；本功能无新增前端故事。
- 本清单共30项：US1 4、US2 5、US3 3、US4 4、共同准备/收口14。静态任务格式、连续编号、依赖无环、FR/AC/SC、SY/DQ、基线顺序、迁移前置和[P]文件冲突逐项核查；这些检查只说明任务可安排，不产生运行Passed。
- 本轮只生成本文件并做静态核对。未复制基线、未修改其他文档/勾选/代码/配置、未构建/测试/连接设备/访问运行数据库/操作Git；CHK015及所有未运行能力保持NotRun。完成后停在tasks，等待后续明确指令，不自动analyze或implement。

实施记录（2026-10-04）：T009—T025交付见 artifacts/plc-polling-013/20261004-implement-01 下configuration-map、cases/*-definition、implementation、architecture-registration及retirement-map。仲裁归现ModbusTcpClient.cs运输边界，未增A06豁免。冻结47个实际组件case/dataRow；N3从合格真实组件账本删行核拒绝。after尚未构建/运行，T026—T030未完成。C1登记实际命中既有011/012文件，前端仍为业务消费者，未改其产品内容。

T026运行记录：after四个受影响测试工程及Host/StorePrep/VirtualPlc传递产物构建成功，最终补构建0警告/0错误；47/47必需case/dataRow已真实发现，布尔参数按xUnit显示名精确绑定。证据为R/after/build-manifest.json、final-preparation-build、discovered-cases-final.json和final-source-manifest.json。前一次发现42/47记录保留；未运行的T027—T030仍待实际结果。

最终实施记录（2026-10-04）：当前证据见 artifacts/plc-polling-013/20261004-timing-fix-02/final-report.md。完整L70/70、组件47/47、N1/N2/N3及真实run-2通过；空闲7721→1079，完整流程7061→2500，61.773195→59.223865秒，通信证据1516135→675523字节。V06单PDU317.0455ms、B完整发布585.8269ms、Host应答117.8343ms及其他间隔/年龄超限；分类OptimizationTargetsNotMet，T029/T030虽已执行但不得勾完成。原before只执行一次，修复明确T到期起点后只补after；全部原失败及首次测量保留。没有放宽判据或开展无关治理。

2026-10-04时效续修：先用attempt02真实同请求建立timeline（artifacts/plc-polling-013/20261004-timing-analysis-03）；B/P慢轮主要耗在PDU内，X真实到期唤醒迟延37.4031ms，未误跨停用。按V06.1补全响应收齐/后处理/发布及来源代次计量，有限诊断复用既有边界，新增直接计量组件验证后再补当前身份L、必要组件及一次after。旧before原中性指标默认保留，新增诊断的自身开销/比较资格单列；不得以边界校正消除端到端慢样本。任务ID和原最小验证范围不变。

本次T028追加唯一直接计量组件I-TIME-01：HeartbeatWindowDiagnosticsTests.ResponseCollectionPrecedesSlowDiagnosticButServiceEndIncludesIt，真实TCP响应后受控诊断延迟，核响应收齐早于原服务结束且后处理耗时保留；原47行完整保留，当前48行。聚合器连续来源/按需归因以有限数据回放核算，不增加业务链。

时效续修结果（2026-10-04，20261004-timing-analysis-03）：T020端点/轮次身份与有界诊断补齐，新增I-TIME-01真实TCP后诊断延迟验证通过；T022连续来源/按需归属修正及旧attempt只读重算完成，旧失败仍在。T026四工程0警告0错误；T027当前身份完整L70/70及受影响009两行通过。T028实际48行，47通过、I-FU-02 old-complete失败：原始基线读取Idle=0而非注入Completed=2，随后本次真实Executing→Completed；一次赋值与VirtualPlc命令0清反馈扫描竞争，不能算旧完成值负例通过，也不据此声称产品误用旧值。N1/N2通过，N3因合格正账本缺失未运行。入口按失败停止，T029本次空闲/代表链及T030新比较未运行；旧attempt02实测不可标成本源码证据。完整结果/时点/最小待办见同根final-report.md及I-FU-02-analysis.json。不修改模拟器扫描/持续时长、线程池/GC/优先级或冻结时效，不择优重跑。

2026-10-04故障输入续修承接：T014/T028按verification V09的I-FU-02条款，复用StageFeedbackFaultProxy在准备完成后仅注入合法Flip反馈读响应Completed=2，核正式调用、同连接/事务前后报文、TransitionFeedbackNotFresh、无TCP Flip派发及无SQLite完成事实；删除一次性寄存器赋值。I-FU-01未启用代理正对照与原stale-epoch/wrong-entity数据行保持。T006/T028固定四行定向前置与最终profile共用预先冻结context，原报告验签重解析后一次引用；完整L仍必需，48行合格后才执行N3。只改测试输入，不改产品、模拟器或任何期限；新身份T026/T027重核前暂退未完成，历史通过不删除。

2026-10-04故障输入修复/最终补验结果（20261004-feedback-fix-04）：源码27077ea75692b1fd7b1568d99e1d88869e5b784fdf8e6a97642ef84543245c7a。StageFeedbackFaultProxy在准备后注入合法Flip旧Completed；实际事务54响应0000→0002，产品TransitionFeedbackNotFresh，TCP无Flip后继派发，真实SQLite失败窗无完成。未启用代理的正常Flip/PutBack及stale-epoch/wrong-entity原行通过。4行原报告以同一预冻结context只引用一次，完整L70/70、必要集合48/48（含原I-FU/I-SEG/I-GAP）、N1/N2通过；合格账本删I-GAP-01后N3准确拒绝。T026/T027/T028恢复完成。before原129项产物及复用输入/证据摘要核对无差异，不重跑。一次after空闲1087/60s、完整流程2522事务/59883.314ms，证据676229字节/1947交换，业务真实Final完成；相较before下降，但单PDU248.5899ms、B完整865.6249ms、P328.4713ms、心跳应答169.4559ms、X有效101.0214ms等仍超限，T029/T030不勾。新增诊断无溢出；同连接/事务拆分确认应用收发等待、唤醒及派发间隙，未证明线程池/GC/扫描或013局部代码根因，未扩调参或重复完整链。完整报告及原始关联见artifacts/plc-polling-013/20261004-feedback-fix-04/final-report.md、after-timeline.json和final/*/compare.json；旧attempt失败保留。

2026-10-04诊断遗漏续修承接：T013补设备端正常300ms到达间隔不得沿旧250ms告警，数值/慢处理/应答异常/3秒故障窗口保留，Host plannedDue诊断不变。T020按A08补Submit实际入队、Drain恢复、队列锁、Select、Eligibility及await恢复时点，快照最多16项，不改Yield/锁/排序；仅诊断时启用。T028新增I-DIAG-01/02/03三行，复用现有HeartbeatWindowDiagnosticsTests及真实TCP/既有故障路径，原48行全保留为51行；先三行定向补验，再同身份完整L及其余集合，正账本合格才N3。T006固定前置改为diagnostic-preflight三行、原报告一次引用，旧Flip前置仅历史保留；无任意过滤/跳过L通道。此次after仅一次，目的明确为分解366.7ms类间隙及核日志变更，未有新原因不重复完整链。T024按新增内部helper真实职责更新现有inventory，扫描与豁免不变。

2026-10-04 I-HB-04真实失败承接：T013修正LatestProtocolPlcDevice.LatchFailure中失效状态/代次分开发布；在原状态锁内完成递增，证据及失败回调保留旧代次。T010/T028复用T065OriginalDeadlineTests原两行在失败日志入口捕获真实Observe，核新代次/不可用/原因及后继拒绝，不增加case或放宽期限。T024登记实际通信测试职责，T026—028按新身份补受影响构建、完整L及既定集合；旧attempt07 L通过但I-HB-04失败、N3和after未运行的事实保留。

2026-10-04本轮实际收口：证据为artifacts/plc-polling-013/20261004-diagnostic-gap-08/final-report.md及同attempt原始报告。T013：I-DIAG-01/02正常300ms不误报、真实慢响应/错误应答保留；I-HB-03/04真实失效时日志入口和外部观察均为新代次且新动作拒绝，原1000/3000ms不变。T020：同请求新增时点3132笔业务交换无次序错误，队列快照最多2项/无截断，Host/设备记录无溢出；只定位不调参。T026受影响四工程构建通过；T027完整L70/70；T028原48行加3诊断行全51/51、N1/N2/N3通过，三前置行同身份引用一次。T029本次1080空闲请求/60秒、2533完整流程事务、59747.121ms、690467证据字节，收益真实但时效仍超限；T030不得宣告软件收敛。旧366.7219ms派发间隙缺少旧内部时点，不能追溯性补写原因；本次最长实际入队后空闲连接派发等待53.2544ms主要在初次Yield恢复53.2332ms，底层就绪/执行原因仍未知，未删除Yield或修改仲裁。attempt05证明序列化失败、06阻塞测试编排超期、07 I-HB-04失效发布失败全部保留，各次均未运行after；本轮仅08一条after。

2026-10-04内核证据续核承接（20261004-kernel-analysis-11）：T020/T022仅在新诊断目录修正离线事务关联，按双方各自收发操作生命周期及连接、事务、完整报文核唯一关联；原10的ETL/RESULT及身份不改。T024针对本轮L实际发现的 scripts/start-recipe-preview.py 补登记：该现有启动器调用正式Host/StorePrep和实际API，是business-script，非通信文件；保留scanRequired=true/exemption=false及本地helper闭包扫描，不修改其产品行为或扩大页面验收。登记后以新身份执行完整L与原51行/N3；旧A10失败保留。CPU Ready等待取证不授权调优优先级/GC/线程池，未证实的局部修复不实施，诊断采集不能冒充T029。

2026-10-04新实证局部缺陷承接：分析11请求2676真实排队约1184.8ms后仍获准发送，取消回调迟到；T012/T018补同一原I/O单调绝对截止于选取/资格后/连接与收发返回/写前/结果返回的复核，通信内部TimeProvider与异步截止上下文不进业务。T014/T028在现HeartbeatWindowDiagnosticsTests增I-TIME-02/03两个真实TCP受控时钟数据行，原51行完整保留为53，先固定前置5行再当前L70/其余48及真实N3。T024同步既有文件内部helper职责；T026构建四工程及传递依赖。相关任务暂退未完成，原11 L70/51加单行补验/N3是修复前身份，不能改标。修复不宣称消除系统Ready等待，正式after仍需原全部时效。

2026-10-04 T022/T024计量关联补修：attempt12已实际完成四工程构建、L70/53行/N3和一次after；时效失败保留。心跳翻转sequence855因UTC/QPC锚点残差误关联到两次翻转后的事务533，实际事务524已应答。修订plc_polling_013_compare.py的离线关联并补scripts/workflow/test_plc_polling_013.py最小实际正例/缺失/错误值或连接/多解拒绝，登记扫描。当前L及必要账本按新身份核验；运行侧源码/DLL/输入与采集器不变时，核摘要后仅重算原attempt12测量，不重新跑业务链、不改标旧身份。T029/T030仍按全部原时效判据，禁止凭消除该误判勾选。

2026-10-04 T014/T028定向收敛：attempt13诊断编排错误地为所有并行组件启用额外时序采集，造成同PID的Infrastructure/VirtualPlc诊断副本争写输出，四项收尾失败；恢复正式runner固定五项前置的既有采集范围，不修改产品或关闭正常诊断。随后I-DIAG-02捕获原1500配置实际1490.573ms和真实慢响应告警，定位“配置值等于实际最小时长”的错误断言。按验证合同改核真实故障超过原I/O期限及原250ms慢处理阈值/事务/响应关联；原53行完整保留。只有实际受影响测试工程重建，当前L及必要集合按新身份核验；原after仅在实际运行侧依赖完全未变时按原身份复用重算。全部原失败保留。

2026-10-05 T014/T028补证：attempt14完整L70通过，但53行账本49通过、I-HB-03/04准备阶段未注入即取消、I-FAST-03/I-FU-01漏过真实80ms执行中状态后被产品拒绝；原失败保留，T028不可恢复完成，N3未运行。T065OriginalDeadlineTests仅增加finally有界失败诊断，按验证合同分别记录准备/故障阶段、实际失效和观察，随后只对两条心跳行补一次判别证据，不改变输入或断言，不重复完整链。测试源码变化后的L须按实际身份核验；未受影响组件只可在源码/产物闭包对应证明成立时引用，原失败不得被后续偶发通过覆盖。

2026-10-05实际停止状态（20261005-protection-diagnosis-15）：源码cd7c303d147054ae4875ea913d2d0f9f69ce8db0defef40665e3532cb6684c50，通信测试受影响构建0警告/错误，当前完整L70/70及受影响009两行通过。新增finally诊断后的I-HB-03/04本次2/2通过，均实际进入原故障分支、失效观察/代次与新动作拒绝成立；原准备超时未复现，没有改写attempt14失败或把两项诊断拼成合格53行。T012/T018的绝对截止修复已有attempt12真实TCP两行及整链证据，运行侧129项产物持续未变，恢复实现项；T026/T027按当前构建/发现/L/009证据恢复。当前27/30，T028/T029/T030未完成。最新完整组件集合仍49/53失败；N3缺合格正账本未再执行。原before及attempt12正式after按原身份保留，新比较器离线重算：7721→1087空闲请求/60秒、7061→2537流程事务、61773.195→59914.214ms、1516135→689001证据字节；单PDU629.0808ms、B1497.3603ms、P989.1567ms、心跳Host应答282.9514ms/设备翻转应答484.8744ms、X有效77.1251ms等仍失败。内核已证实个别样本Ready未运行231.2889/192.6733ms，不能据此解释全部新峰值或豁免判据。无进一步有依据的013局部修复，不原样重跑；需管理员提供不受已识别外部高优先级作业竞争的验证时段，再重核双方比较资格和仅必要补验。该条件不等于成功保证，正式PLC未知限制不变。详见本attempt final-report.md，全部原失败保留，审查清单未改。


2026-10-05 C盘独立续查（run16）：固定归档只读，before/after各自源码重建，所有新库/日志/计量/临时文件落C盘。只先运行I-FAST-03、I-FU-01、I-HB-03/04一次有界诊断，不替代53行或正式对照；原80ms/期限/所有断言保留。为承接attempt13已证实同PID的Infrastructure与VirtualPlc诊断副本争写，PlcTimingTrace输出改按PID及实际程序集身份命名，payload登记组件身份；不移动时点、不修改记录容量或运行调度。比较器明确识别新文件，旧文件仅用于历史只读重算，双份候选拒绝。现有翻放用例在finally保存设备实际动作/写入审计、既有代理报文和设备诊断，含时基锚点与gap，不以安全拒绝替代正常成功断言。新增记录只用于失败定位，不能证明内核完成/Ready或解释所有GC相关峰值。T014/T020/T022/T024承接上述诊断；T028—T030保持未完成，正式性能必须先有负责人确认的运行环境条件。未改变业务端口或53项固定义务。


2026-10-05 主项目012合入承接（run16，T009/T022/T028）：当前完整组件实际发现I-CONFIG-02被RecipeCurrentSchemaRequiredForSave拒绝。RC09已要求正文3新保存，活动joint/catalog仍正文2，属于输入迁移遗漏，不修改有效断言或唯一校验器。只将主项目活动011联合Test夹具迁为正文3：明确选择软件测试夹爪1（同现有Recipe011Data.Candidate的受控Test选择，不是历史默认或PLC信号），每个实际相机坐标按原唯一stage/localFace/camera目标显式引用原detect配置，曝光/增益/亮度、机械坐标、动作时序、算法、媒体和业务预算均原样。同步活动run-1/run-2目录摘要和input-manifest；不增加run-1完整链。013固定归档、派生before/after及原性能输入均不改；主项目新输入只用于合入验证，不能与旧before直接计算收益。原正文2读取及拒绝直接新保存保护保留，不增加自动升级/兜底。该输入/设计身份变化使主项目最终L和53行须在新身份下重核；既有失败保留。固定5项诊断前置必须按既有入口启用GAODE_013_MEASUREMENT_ROOT，其余组件不启用额外采集；这不是放宽诊断断言或新的验收旁路。正式性能仍等待负责人确认的同条件窗口。


2026-10-05 run16当前主项目静态收口：源码91be24a679b8815e6eaddfb67ec6389cf44c69fb01b9b7825521d510f78a7f53，构建5c00b2886e0f628673cd90c3526ca1272e43971d35a5c2d75dafd22f23c69ebc；实际源码已增量合入E:/dzk/gaode-1，在C:/dzk-work/013-20261005-run16/main-integration从逐文件等内容源码构建。完整L70/70、当前53/53（含009两项、N1/N2、原绝对截止/真实保存/1024分段/缺口拒绝）及合格原账本后的N3均实际成立，恢复T028。5项诊断前置/43项普通通信/2项009/3项合同分别有原TRX及发现记录；未拼接不合格组件账本。T029/T030仍未完成，当前28/30；正式同条件before/after及主项目必要代表链NotRun，不能以组件通过或旧收益宣布软件收敛。原四项诊断本次未复现，不解释为历史根因已解决。详见C:/dzk-work/013-20261005-run16/attempt/final-report.md及main-validation-v2；原归档/失败/CHK015与审查勾选不改。


2026-10-05 run17正式补验：用户确认窗口内完成C盘独立before/after各一次60秒空闲及同run-2；固定after完整L70/70、组件53/53、N1/N2/N3与执行核对合格。新空闲8002→1080、完整流程7558→2505、58924.573→58772.867ms、证据1577087→674913字节；单PDU617.2716/25ms、B完整1177.4251/150ms、P696.1796/50ms、Host应答591.1973/50ms等仍失败。主项目等内容C盘集成源码91be24a679b8815e6eaddfb67ec6389cf44c69fb01b9b7825521d510f78a7f53复用验签后的原L70/53/N3，新正文3 run-2真实保存/冻结/Final通过，但自身单PDU256.4617/25ms等仍超限，不与固定正文2before混算。无新产品修改或阈值调整；3609笔同事务关联无缺失，应用收发之间等待缺少本次内核Ready/I/O证据，不套用旧原因。本次实际令牌缺性能采集权限，已准备C盘单命令20秒管理员交接，普通业务身份/保护不变。T028维持完成，T029/T030未完成，28/30；完整新结果、各侧身份、全部失败及唯一外部动作见 C:/dzk-work/013-20261005-run17/attempt/final-report.md。固定归档/原before/012主项目/审查清单及历史事实保持。

2026-10-05 T022/T023/T024/T027/T029/T030定向承接013-acceptance/2：先文档后判定器，未知错误保持硬拒绝；同一生产判定函数验证仅工程超限可带限制收口、硬失败/首态75拒绝、缺失/Skip/身份/可比性拒绝。不重编号、不减少主项目原53行，不采用run18未合入草稿替换当前产品或清单。优先原run17固定比较与单独主项目集成离线复核；新规则/新L/判定器验证与旧运行身份分开记录，T029/T030待实际结果再勾。


### 2026-10-05 run19：需求方批准013-acceptance/2收口

本轮仅验收依据/实际判定器/离线回归及登记定向修订，未改产品行为/输入/计量边界，012成果保持。新Rules构建0警告0错误、完整L70/70、判定器7/7；原固定与主项目53/53组件逐行重解析，N1/N2引用，N3真实53行先接受再删I-GAP-01唯一拒绝。run17正式既有60秒及代表链离线复核，原Failed不覆盖，不冒充新运行：空闲8002→1080、流程7558→2505、58924.573→58772.867ms、证据1577087→674913字节；主项目正文3独立业务/硬条件成立，不与正文2混比。局部首态最大74.1348≤75ms；原期限、预算1110/4950及所有保护保留。617.2716ms等工程偏差继续报告目标未达到，按本次批准仅不阻断软件收口。

T029/T030依据新版本全部适用硬条件和真实证据完成，30/30。原run18附加诊断下I/O截止/流程失败及退出异常不改判、不宣称已修；未完成独立副本草稿未合入。收口适用冻结正式软件条件，不保证任意外部或诊断负载；现场信息限制不变。

新结论及身份、原始证据、分级映射见 [run19最终报告](C:/dzk-work/013-20261005-run19/attempt/final-report.md) 和 [真实判定](C:/dzk-work/013-20261005-run19/attempt/final-result.json)。原attempt报告与任务历史保持，审查复选框/CHK015未改。
