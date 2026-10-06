# 009 共享接口与文件影响矩阵

**当前范围**：通信代码边界最小收敛。I01—I27路径/生产消费者与AL01—08原对齐义务保留；当前只核正式在用代码/接口/合同/断言边界与构建。完整动态PD/CS/M/MC、预算/历史/升级/页面/整机运行转出，见scope-adjustment与VG BM00。

状态：设计清单，未实施。下列代码、测试、其他功能文档均未在本阶段修改；“需改”是后续实施前的对齐要求。路径相对项目根，`backend/src/`简称SRC，`backend/tests/`简称TEST。新文件标“拟增”。当前源码证据位置见[research](research.md) R01—R26；新增期限依据为spec FR-035—039已接受决定。

## 1. 生产者、消费者与保存格式

| ID | 具体文件/共享对象 | 生产者→消费者 | 一次性变化与保留义务 | 冻结后归属 |
| --- | --- | --- | --- | --- |
| I01 | SRC/Gaode.Application/Ports/DeviceMessages.cs；Domain/Station01/RunSnapshot.cs | LatestProtocolPlcDevice、SimulatedPlc/SimulatedDeviceState、测试double → MotionAdmission/Coordinator、StartupReadiness、PhysicalFaultPolicy、StartClamp/PalletUnlock、StartPublicPreparation、FixedMoveRecoveryInteraction、Host status/startupDiagnostic | 移除raw状态/报警，加入语义可靠性、业务安全原因、观察身份/引用；保留实际XYZ/面、epoch、时间和来源。不从目标填反馈。 | 业务冻结 |
| I02 | SRC/Gaode.Application/Ports/IPlcStatePort.cs、IPlcActionPort.cs、IMotionPort.cs | 同一PLC设备/模拟设备 → MotionCoordinator；Host组合根 | 消除接口注释/契约中的原始ACK/锁命令；唯一租约、意图、启动由PLC夹紧、停止/解锁含义不变。 | 业务冻结 |
| I03 | SRC/Gaode.Application/Ports/IInspectionHandshakePort.cs（替换）；Motion/MotionCoordinator.cs；Station01/Steps/ThreeDStep.cs、FScanStep.cs | 正式PLC/SimulatedPlc → 3D/F及IntegratedDetectionPort | 采集窗口开放/释放语义；Unsupported替身明确受限；采集算法媒体保存先后和原期限不变。 | 业务冻结 |
| I04 | SRC/Gaode.Application/Ports/IPlcResetPort.cs；Station01/FixedMoveRecoveryInteraction.cs；Host/Api/RunEndpoints.cs | PLC真实复位/初始采样 → 完整新轮恢复 | Checks内部清零字典移诊断，公开业务初始状态；新epoch不代替实际初始状态，新旧run关联不变。 | 业务冻结 |
| I05 | SRC/Gaode.Application/Ports/IPlcRecipePort.cs；Motion/MotionCoordinator.cs；Station01/Steps/StartClampStep.cs；Recipes/RecipeContracts.cs、RecipeRunPlanner.cs；Station01/StartPublicPreparation.cs；Host/Api/RecipeEndpoints.cs、Composition/Station01Registration.cs | 运行配置→夹紧后区域准备；F唯一计划→配方应用；独立API→同一Application绑定协调 | A保留公共3D/F前运行容量与PlcAcceptance，非已绑定；B按实际plan可空容量/显示处理。B03.2新增已批准独立总窗，业务传稳定期限/关联/取消，通信消化握手。独立API保留容量回退、不移到连续链；新增本次意图/绑定事实，核验已有handoff不重建。显示号/容量脱离wire ushort而含义不变。 | 初次修复后业务冻结 |
| I06 | SRC/Gaode.Domain/Station01/SortingMappingContracts.cs；Application/Recipes/RecipeContracts.cs、RecipeRunPlanner.cs；Application/Workflow/RecipeExecutionCoordinator.cs、RecipeSortingMapper.cs；Station01/PublicPreparationHandoffV2.cs；Infrastructure/Recipes/JsonRecipeCatalog.cs | 现有recipe JSON → 物理槽/面/步骤计划 → Detection/Sorting | ProtocolSlotIndex改为明确的PhysicalSlotIndex语义；保留真实槽≠Sequence、原JSON载荷、配方版本/摘要；移除ThreeStageWorkflowExecutor中的ushort线缆转换。此处不是删除合法业务数字。 | 全部业务/配方冻结 |
| I07 | SRC/Gaode.Application/Ports/StagePortContracts.cs | LatestProtocolStageActionAdapter、NotIntegratedPlcStageActionPort、测试double → ThreeStageWorkflowExecutor、WholeTrayWorkflowOrchestrator、SortingTargetAllocator | 删除Application地址常量、ProtocolStatus及位置证据raw Status；位置/面/结果/保存引用保持关联；NotIntegrated不得默认成功。 | 业务冻结 |
| I08 | SRC/Gaode.Application/Workflow/SortingTargetAllocator.cs | 预留/语义PickEvidence → IStageEventStore真实提交 → PickCommitReceipt | 保留Reserved/InTransit/Occupied；回执必须真实提交；删除合成protocolStatus和ACK事实；来源继承实际证据。 | 业务冻结 |
| I09 | SRC/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs、WholeTrayWorkflowOrchestrator.cs；Station01/Steps/StartClampStep.cs、PalletUnlockStep.cs；Station01/PhysicalFaultPolicy.cs | 语义端口结果 → 业务控制和StageEvents/Writes | 原始值判断改语义，取放/下料/WholeTray/解锁/人工Final保存节点不变；汇总Derived与设备Provider分开，不能把写死Virtual一律改Real。 | 业务冻结 |
| I10 | SRC/Gaode.Application/Workflow/FlipFeedbackCorrelation.cs | 当前仅对应测试调用，非正式设备调用 | 原始翻面关联算法移通信或移除重复实现；有效旧值/错面/epoch义务在真实ExecuteFlipAsync路径验证。 | 业务冻结 |
| I11 | SRC/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs | Host IDetectionPort正式装配 → 文件相机/Worker/Motion、翻面/人工/特殊Test | 去掉具体LatestProtocolPlcDevice/ProtocolLatestMap依赖，使用有限语义能力；实际采集/算法/媒体/保存不变；移除假定已持久的modbus://引用生成。目录名不决定其业务归属。 | 非通信编排冻结 |
| I12 | SRC/Gaode.Host/Composition/Station01Registration.cs、AdapterBindings.cs、PlcConnectionHostedService.cs；Host/Program.cs | 配置→全部实际端口/存储/Worker | 正式注册替换同一实例接口，注入真实PickCommit与通信证据writer；已有business/heartbeat连接与控制所有权复用。 | 组合根冻结；变体不改DI |
| I13 | SRC/Gaode.Infrastructure/Devices/Plc/ProtocolLatestMap.cs；VirtualPlc/PlcAddressMap.cs、PlcDataStore.cs底部Float32Codec；拟增SRC/Gaode.Plc.Protocol/ | 当前双点表/双codec → 两端通信消费者 | 合并纯定义/codec，保留独立设备状态机；项目参考相应调整。独立oracle保留一份测试预期，不计作重复生产映射。 | 通信定义可变 |
| I14 | SRC/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs、LatestProtocolStageActionAdapter.cs；拟增命名信号访问器/内部解释器 | 业务请求→真实Modbus→语义观察/结果 | 集中所有路径/布局/编码；保留原时序、超时、未知、来源和保存回调；无新增第二连接所有者。 | 仅协议职责部分可变，保存/占用规则不可变 |
| I15 | SRC/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs、IPlcTransport.cs、HeartbeatDiagnosticWindow.cs；VirtualPlc/ModbusTcpServer.cs | 真实TCP与诊断窗口 | 增加真实交换捕获/批次关联；心跳诊断不写死地址；PDU编解码仍属通信。ThreadPoolRuntimePolicy及现行I/O期限无本次行为变化。 | 通信，但变体只改必要映射相关内容 |
| I16 | VirtualPlc/VirtualPlcEngine.cs、PlcDataStore.cs、DeviceActionAudit.cs、Program.cs、VirtualPlc.csproj | TCP命令→独立设备状态→真实反馈/本机审计 | 定义共享、状态机独立；去掉裸值和布局假设；本机generation/actionSequence不冒充Host身份，审计仍可导出且有gap。 | 通信；不共享业务/DB |
| I17 | SRC/Gaode.Infrastructure/Simulation/SimulatedPlc.cs、SimulatedDeviceState.cs；Infrastructure/Integrations/NotIntegratedStagePorts.cs；Host/Composition/UnavailablePlcRecipePort.cs | 模拟/未接入模式→业务端口 | 一次性适配语义边界；不能作为V-MUT真实TCP替代。 | 冻结模拟语义，不随变体改 |
| I18 | SRC/Gaode.Infrastructure/Persistence/TraceWriter.cs、Station01DbContext.cs、Station01EntityConfigurations.cs、StoreCompatibilityProbe.cs、Migrations/（拟增单项）；backend/tools/Gaode.StorePrep/Program.cs | 通信专用job→新增证据表/真实回执；诊断reader→Host | 新schema/受控建库及有限升级；不把raw塞Application WriteBatch；不重构全库写者；现有StageEventStore/WholeTrayCompletionStore/ControlledRecoveryDecisionStore事务不合并。 | 持久边界冻结，变体不改schema |
| I19 | SRC/Gaode.Infrastructure/Diagnostics/RuntimeDiagnosticLogging.cs；Application/Diagnostics/RuntimeDiagnostics.cs（现有链接编译） | 真实语义/诊断事件→ILogger及Test持久日志 | 关键分类/关联/首末失败保持；共享日志工具不能给业务引入raw读取API。原控制台重定向仍可用，但不代替证据提交。 | 业务日志冻结；专用raw捕获属通信 |
| I20 | SRC/Gaode.Host/Api/QueryEndpoints.cs、CommittedResultProjection.cs、Station01ApiContracts.cs、Station01NotificationService.cs；拟增Infrastructure有限历史投影/诊断reader | SQLite新语义/旧payload→status/run/evidence/通知/diagnostics | 按§5逐字段映射；移除Host位解码及新事实raw2/3判断；通知现summary为完整RunSnapshot，目标只发固定摘要。旧历史不重写/补raw，不授权动作。 | API/业务投影/历史规则冻结 |
| I21 | frontend/src/runtime.js；frontend/src/state/notification-reducer.ts | status/run/evidence/通知→已批准页面字段 | 按§5绑定；runtime:345/381当前有写死Test/Simulated来源，目标只在既有来源区域绑定真实来源，不增控件/改原型文案；notification-reducer目前summary:string与Host对象不一致，明确采用§5固定对象摘要。 | 冻结；必要接口对齐属另功能授权 |
| I22 | scripts/verify-station01-page-diagnostics.cjs；validate-008-operation-evidence.py；validate-008-route-evidence.py；validate-008-flip-timeout.py；audit-008-night-page-route.py；validate-008-authorization-page.py；run-station01-diagnostic-api.ps1；verify-latest-plc.py；scripts/tests/virtual-plc-monitor.test.cjs；verify-008-backend-route.ps1；verify-q01-q02-test-page.ps1；watch-station01-auto-removal.ps1及本地helper | 公开查询/设备审计→断言；PS编排→各验证结果 | 按VG V02.1/T50拆分混合断言；JS/Python语义断言内容受A08/A09保护，PS只有限编排/采集，A10拒漏分类；通信raw迁精确scripts/communication探针，保留全部有效断言。复用TypeScript5.9.2、Python ast、PS自带AST；check-009-script-boundary.py及接线待实现。 | 业务脚本/检查器/分类/账本冻结；精确通信探针/oracle可变 |
| I23 | TEST/Gaode.Rules.Tests/Architecture/DependencyRulesTests.cs；拟增有限扫描/负正例/分类清单；TEST/Gaode.Communication.Tests/（拟增） | 真实源/fixture→验证结果 | 精确依赖与内容边界；测试SDK Roslyn引用；旧断言迁移详见单独矩阵。 | 检查器/业务测试冻结；通信测试可变 |
| I24 | scripts/workflow/runner.py、test_runner.py、test_verify.py；scripts/verify.ps1、workflow/verify_entry.py；workflows/auto-dev.yml | verify→suite/必需ID/TRX/源码摘要 | 复用现有入口，加通信套件、必需门禁发现/逐ID账本和范围摘要；不改成忽略Skip，不在009阶段启动auto-dev。 | 门禁全部冻结，不能随变体放宽 |
| I25 | SRC/Gaode.Domain/Configuration/BusinessBudget.cs；Application/Configuration/PublicConfigurationValidator.cs、ConfigurationFreezer.cs；Infrastructure/Configuration/ConfigurationLoader.cs；001 contracts/budget.schema.json、configuration-time.md | 新合法budget→严格schema/模型/用途校验→运行完整冻结→三绑定入口 | 目标schema1.1新增必需businessMs.recipeApplication/RecipeApplication，Test10000，版本/用途/来源/digest/snapshot完整；新实例版本，不改旧版本内容、不默补旧字段。Production未批准拒绝。冻结逻辑复用并覆盖新字段；公共配置schema和budget版本分别校验。 | 预算/配置校验及冻结业务全部冻结 |
| I26 | SRC/Gaode.Application/Station01/StartPublicPreparation.cs、RunExecution.cs、PublicPreparationHandoffV2.cs；Workflow/RecipeExecutionBudget.cs；Host/Api/RecipeEndpoints.cs、QueryEndpoints.cs；Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs；I17绑定替身 | Application窗口/意图→正式Binding→设备中间证据→业务保存/当前Receipt→后段及查询 | t0/总窗/取消贯穿排队、每写及保存；RunExecution取CriticalSave与剩余窗较小者。HandoffV2现IsVerified及Query现仅凭handoff推Bound/Ready均须改为同时核当前有效Receipt，不能复活迟到/重启动作；按期完成稍晚调度不倒判。严格原公式/起点值及旧起点保持，API无后段不造；实际commit/receipt分开。 | 业务/消费者/保存/预算冻结；通信内部可变但不改截止 |
| I27 | specs/007-station01-integrated-loop/examples/budget.virtual-loop.json、simulation.virtual-loop.json；scripts/start-station01-virtual-loop.ps1、simulate-station01-load.ps1、get-008-page-budget.ps1；TEST/Gaode.Integration.Tests/Support/VirtualLoopTestRig.cs、Station01/StrictQ01SaveGateTests.cs、ExpectedRecipeMismatchTests.cs、RuntimeLogFlowIntegrationTests.cs；RecipeExecutionCoordinatorTests.cs及全部BusinessDurations构造者；scripts/summarize-q01-q02-evidence.py | 新版预算/fixture引用→启动与集成测试→语义/通信分别取证 | 盘点全部预算schema/ID/version引用并改用批准新实例；当前启动脚本硬编码旧1.1.0不能称已支持。测试构造者显式提供新预算，组件5秒看门狗不是预算。混合汇总脚本仍按T50拆分，不放宽A08—A10；V-BIND及BA固定行接入现账本。 | 初次对齐后配置/业务fixture/断言/编排冻结；精确通信探针可变 |

## 2. 历史数据和接口发布

| 载体 | 当前内容 | 后续处理 |
| --- | --- | --- |
| StageEvents.PayloadJson | ProtocolStatus、PositionEvidence.Status、sortingAckCleared及分拣预留/在途/占用 | 新记录仅语义schema和引用；旧JSON不重写，合成状态2不得转ActualWire。有限历史读取在Infrastructure，不能授权当前动作。 |
| Writes.PayloadJson、Operations.EvidenceJson、RunSnapshot | 夹紧/解锁raw、startupReliableFeedback报警位、目标/实际位置与来源 | 新观察/证据模型；旧原文和摘要保留。未知/不存在的raw与原版本不补造。 |
| Handoff、RecipeRunPlan/目录JSON | ProtocolSlotIndex/PlcRecipeId及业务容量、对象/面/步骤 | 明确物理槽/HMI语义，配置适配器读取现有载荷，配方不为009演练改写；新内存业务类型不与wire-width绑定。 |
| ComponentSourceMatrix | Device/Host派生来源及引用 | 新事实保留各组件实际来源与质量；旧写死来源标记录性质，不篡改历史，也不据此宣称真机验收。 |
| Modbus内存窗口及VirtualPlc审计 | 有界真实包/本机序号，可能gap | 导出是证据的一部分；新增Host持久引用填补当前缺口，不能声称历史窗口已经落库。 |
| API `/status`、run、evidence | 当前s01-status/1.0和部分raw/JsonElement | 一次性发布明确新语义版本及诊断查询；不保留raw影子业务字段作兼容。不认识新版的客户端明确受限，发布前按实际消费者对齐。 |

OPEN-009-06的仓内消费者盘点已完成；无法从仓库排除未登记外部客户端，发布前核查其版本/使用范围。这不要求当前用户重新确认已知业务，也不阻塞009任务拆解。

## 3. 原设计轮对齐清单（来源保留；实际成果/当前义务见§6及scope-adjustment）

| 功能及实际合同路径 | 后续必须对齐的内容 | 保持不变 |
| --- | --- | --- |
| [001 device](../001-station01-public-preparation/contracts/device.md)、[persistence-handoff](../001-station01-public-preparation/contracts/persistence-handoff.md)、[configuration-time](../001-station01-public-preparation/contracts/configuration-time.md)、[budget.schema](../001-station01-public-preparation/contracts/budget.schema.json)及适用spec/plan/tasks | 语义观测/采集/startup；新增recipeApplication必需字段/schema1.1/合法新版本/冻结/用途/拒绝条件；总窗与CriticalSave及ResponseBeforeDeadline | A原受理/夹紧窗口、其他已确认预算、公共3D/F与原保存节点 |
| [002 recipe-execution](../002-plc-xyz-recipes/contracts/recipe-execution.md)及适用spec/plan/tasks | 物理槽/显示/容量与wire分离；A/B时点及两容量输入、三入口总窗和DeviceApplied与最终Bound区分；旧覆盖口径按当前宪章 | 配方结构、实际输入、独立API原容量回退及持续扩展 |
| [003 plc-stage](../003-plc-latest-protocol/contracts/plc-stage-action-port.md)、[detection](../003-plc-latest-protocol/contracts/detection-port.md)、[whole-tray](../003-plc-latest-protocol/contracts/whole-tray-workflow.md)、[stage-events](../003-plc-latest-protocol/contracts/stage-events.md)、[handoff](../003-plc-latest-protocol/contracts/public-preparation-handoff.md)、[recovery](../003-plc-latest-protocol/contracts/recovery-test-execution.md)、[source-matrix](../003-plc-latest-protocol/contracts/component-source-matrix.md)及spec/plan/tasks | 业务义务与协议表/内部时序分开；取料语义提交回调、可靠完成/证据及历史读取 | 真实协议、原期限、UnknownHeld、整盘/解锁/Final、USR-D完整新轮 |
| [003 API](../003-plc-latest-protocol/contracts/station01-main-flow-api.md)、[notifications](../003-plc-latest-protocol/contracts/status-notifications.md)、[VirtualPlc](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md) | status/evidence版本、语义报警、独立raw只读查询与提交后引用；独立oracle/本机身份 | 通知非事实、GET对账、独立设备和方向责任 |
| [008 execution](../008-recipe-driven-inspection/contracts/execution.md)、[evidence](../008-recipe-driven-inspection/contracts/evidence.md)、[api-results](../008-recipe-driven-inspection/contracts/api-results.md)、[test mapping](../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)及适用spec/plan/tasks | 原语义/取料/来源/测试拆分；E06补绑定总窗按较早截止约束、Host接收/提交/回执证据和失效不授权 | E06严格路径绑定前冻结起点/值及原公式，旧路径起点；工艺/配方/动作/不重扫/3＋1/E/组/整体 |
| [006 api](../006-frontend-station01-console/contracts/api.md)、[host](../006-frontend-station01-console/contracts/host.md)、[prototype mapping](../006-frontend-station01-console/contracts/prototype-mapping.md)及实际受影响spec/plan/tasks | 只评估既有字段版本与诊断引用的数据绑定；移除脚本对raw控制字段的要求 | 客户原型ZIP/结构/文字/控件/交互全部只读，前端仅后端API |
| [007 integration](../007-station01-integrated-loop/contracts/virtual-integration.md)、[CLI](../007-station01-integrated-loop/contracts/commissioning-cli.md)及适用plan/tasks | 现有脚本参数/进程来源和新009入口能力界限，证据导出消费者 | 历史007范围及历史通过不改写，不拿局部模拟抵009 |

共享接口实施前必须完成实际受影响功能的spec/contracts/plan/tasks对齐；当前用户只授权009设计文档，该历史轮不能声称已完成跨功能对齐；当前既有实际对齐成果另按T004—012证据核实。可在后续009任务拆解列为前置依赖，获得相应范围授权后处理。任务勾选和来源文档在本轮均保持原样。

### 3.1 对齐责任、顺序及完成证据（R06）

下列角色是职责分配，不代表已指派人员或已获批准。每行均为**待后续授权完成**；角色同时负责对应功能实际受影响spec/contracts/plan/tasks的一致调整，不能只改合同或任务勾选。完成证据统一包含实际文档差异、具体接口/条款到009映射、生产/消费方复核记录、原义务到待实现测试的承接清单；不得以口头确认或本表存在当完成。

| 对齐ID/条款 | 修订责任角色 | 复核责任角色 | 前置顺序与完成证据补充 | 阻断的共享代码修改 |
| --- | --- | --- | --- | --- |
| AL01 / 001 device §1—4、persistence-handoff §1/3/4；补列[api §1/4/5](../001-station01-public-preparation/contracts/api.md)（明确公开旧reliableFeedback六字段） | 公共准备业务负责人、持久化/API接口负责人 | 架构、公共流程业务及测试评审人 | 先承接BD03.1 A与§5 startup映射；保留Accepted/夹紧分期限和3D失败清理；核对旧API字段实际删除/替换清单 | DeviceMessages、StartupDiagnostic、StartClamp/3D/F、公共保存口及API |
| AL02 / 002 recipe-execution §当前共享模型、直接接口增量 | 配方模型/规划负责人 | 工艺业务、架构及通信评审人 | 与AL01保留A/B；以spec已接受FR-035—039及AL08预算契约为输入，对齐B总窗/两容量/三入口/设备中间事实与最终授权，不重新确认策略；记录引用与替代断言 | IPlcRecipePort、RecipeContracts/Planner、StartPublicPreparation、RecipeEndpoints |
| AL03 / 003 plc-stage-action-port、stage-events、detection、whole-tray、handoff、recovery、component-source-matrix | 设备语义端口负责人、业务保存负责人 | 架构、安全业务、协议及测试评审人 | 继AL01/AL02语义边界；明确BD05/E02.1的实际提交/有效回执、UnknownHeld及原协议义务转PC；与AL06共同复核取料门禁，提交矩阵A—D有承接 | StagePortContracts、阶段适配器、StageEventing/Store、Motion、Workflow及诊断writer |
| AL04 / 003 station01-main-flow-api §USR-E证据投影、status-notifications、VirtualPlc边界；006 api §查询/通知、host §后端边界、prototype-mapping | Host API/通知负责人、前端绑定负责人；虚拟设备接口负责人负责独立探针 | API消费者、原型边界、架构及测试评审人 | 继AL03及§5字段表；run.state传输不变、notification固定摘要类型、旧历史缺失表示及只读诊断分别对齐；核查外部客户端，不声称已完成 | QueryEndpoints、CommittedResultProjection、DTO、通知服务/客户端、runtime及页面采证脚本 |
| AL05 / 007 virtual-integration §真实组件/证据、commissioning-cli §参数/脚本/退出判据 | 联调工具与虚拟设备集成负责人 | 协议、业务验收和测试基础设施评审人 | 继AL03/AL04；按I22逐脚本分类、拆分与参数实表复核，确认新的009入口/门禁仍待实现，非零与缺报告都传播失败 | 启动/采证/自动取盘脚本、runner/verify、独立进程探针及虚拟设备协议接口 |
| AL06 / 008 execution §E01—E06、evidence、api-results §处置证据（现有2/3/ACK0条款）、test-virtual-mapping | 配方执行/处置保存负责人 | 业务、协议、API及测试评审人 | 与AL03共同先对齐取料A—D和证据失败出口；继AL02输入定义，逐字段替换旧raw判据而保留所有工艺/保存条件，表明schema升级依赖 | SortingTargetAllocator、执行器、处置投影、证据读取/脚本、配方共享类型 |
| AL07 / 001 persistence-handoff、003 stage-events/API、008 evidence的本次store升级影响 | 持久化/维护工具负责人 | 数据、架构及测试评审人 | 继AL03/AL04/AL06的新payload/引用，按数据模型§6.1复核唯一迁移和U0—UX；实际升级报告后续提供，本轮无数据库变更 | Station01DbContext/Configurations、单项迁移、StorePrep、StoreCompatibilityProbe |
| AL08 / 001 configuration-time §1—3、budget.schema.json；联动002 recipe-execution、003 public-preparation-handoff及station01-main-flow-api、007预算/simulation实例与CLI、008 execution E06及evidence/api-results | 配置与公共流程负责人牵头；配方绑定/保存、联调配置和执行接口负责人各修本功能 | 架构、业务时间/保存、通信、配置与测试评审人 | 先以009已接受决定对齐001字段/schema1.1/新实例版本/用途/冻结/拒绝，再供AL02/03/05/06并行对齐其消费方；完成证据包括逐条差异、三入口t0/终点/取消表、严格/旧起点对照、新旧配置引用清单、BA01—07承接及各产消方复核。原设计时均待完成；已有T004—T012对齐证据保留，完整业务验收转出 | I25全部共享模型/加载/验证/冻结、I26绑定/保存/后段接线、I27预算/脚本/测试fixture；不能仅修改JSON后进入代码 |

以上是接口文档依赖图，不要求所有代码串行等待不相关项；但任何相应共享接口代码修改必须在所列对齐实成之后。任务拆解可以列明这些前置，不能把“将对齐”当“已对齐”。

本轮追加的具体消费方条款也属于上述前置：AL03须对齐本次RecipePlanBound/handoff真实回执、后段消费者当前Receipt门禁、独立API本次意图且不重建handoff、后台取消及迟到不授权；AL04按§5.5修正Query/通知GET不得从handoff存在恢复Bound/Ready，补语义元数据而不扩页面；AL05须对齐007新版budget/simulation、启动脚本旧版本硬编码、008 fixture引用及看门狗非业务期限；AL06明确E06三段冻结不移动、不补时。回执观察按模型§3.1独立追加审计，不在原事务预填未来信息；这些义务分别阻断HandoffV2/RunExecution/QueryEndpoints/通知投影共享代码。AL01—07原R02—R07义务均保留，AL08不替代各方实际对齐。

## 4. 变化分类结果

| 本轮发现项 | 分类 | 处置 |
| --- | --- | --- |
| 点表/codec重复、偏移/邻接/固定窗口、raw状态流入业务 | 一次性边界修复；后续表示变化为通信维护 | I01—I16，不改变已确认业务义务 |
| 取料/放料、夹紧/解锁、复位完成表达及测试归属 | 一次性语义接口修复 | 保护义务保留；不得以改完成条件解决测试失败 |
| 原始证据缺持久引用、来源合成/硬写、历史反推风险 | 当前明确要求的证据边界修复 | I18—I22，真实保存与只读历史处理 |
| 警告放行策略与当前保守准入差异 | 若要改变则是安全业务契约变更 | 本轮不放宽；明确记录来源冲突 |
| 新工艺输入、新完成含义、保存前放料、期限归属改变 | 业务契约变更 | 不由通信层独自吸收，不在本轮实施 |
| FR-035—039补齐配方应用独立有限总窗 | 本次已明确批准的业务契约增量，仅初次修复 | D11/B03.2及AL08；新版配置/端口/保存/取消先落地并验证，再冻结。后续纯通信演练不得调整该预算或业务断言 |
| 真实轴/单位/校准、未来报警/握手、特殊旋转生产映射 | 缺物理事实、局部OPEN | 对应生产路线受限；当前明确Test设计继续 |

全部已发现项已分类，无用未确认讨论稿填补的设计。后续新发现必须追加具体影响，不能把“通信相关目录”当无限变更授权。

## 5. 公开字段与历史表示映射（R04）

本节为规范性一次性变更清单，均待实现。源码依据：QueryEndpoints:37–51/81–86/200–230/245–290；Station01ApiContracts:34–103；RunSnapshot:3–84；StagePortContracts:206–208；Station01NotificationService:60–62。旧外层API通常camelCase，PositionEvidence从旧PayloadJson直接clone，嵌套可能PascalCase；下表以旧C#字段名标注这类载荷，不能假称历史已统一大小写。

版本约定：S=`s01-status/2.0`；R=run新增`deviceSchemaVersion="device-semantics/1"`，E=evidence同字段，所有新设备事实payload用同版本且camelCase；N=`s01/notification/2.0`（旧为`s01/notification/1.0`）。原run.resultSchemaVersion及业务run.state枚举传输保持，不能借此次统一所有业务枚举。诊断读取另用`plc-evidence/1`，不把raw挂回S/R/E/N。Unknown是明确语义；null表示该事实不存在/不可取得；空数组只表示确实无条目，不能替代未取得观察。

生产者/消费者及调整责任缩写（每行继承）：**PS**=通信语义观察→Host status→runtime/业务脚本，通信与API负责人(AL03/04)；**PR**=StartupReadiness/RunSnapshot及业务持久化→Host run/查询→runtime/业务脚本，公共流程与API负责人(AL01/04)；**PE**=业务动作保存＋Infrastructure历史读者→Host evidence/CommittedResultProjection→runtime/采证脚本，业务保存与API负责人(AL03/06)；**PN**=Coordinator→通知服务→notification-reducer/runtime→GET对账，通知/前端负责人(AL04)。这些角色必须同时修改生产与消费方，不留下raw兼容字段。

历史约定：**H0**=原业务值/类型/缺失保留；**H1**=新语义只用于新事实，旧已存字段由有限历史读者按当时实际记录提供业务摘要，`recordNature=LegacyRecordedClaim`，不提升为当前物理反馈；**H2**=原始字段留旧payload/受权存储审计，不回传业务API、不写进新raw表；无实际包标`rawAvailability=RawUnavailable`；**H3**=未存全对象/字段保留null及Unavailable，不能按新流程补齐。新事实标`recordNature=Captured`（实际采样事实）或`Derived`（Host汇总），真实ExecutionOrigin另列，不把记录性质当Provider。

### 5.1 status逐字段

| 旧字段：含义/当前类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| schemaVersion:string | 替换为S | S；PS | H0，历史包原版本不改 |
| plc:object? | 语义DeviceObservation?，无采样为null；有采样但陈旧时保留值并标reliability=Stale | S；PS | H1/H3 |
| plc.connected:bool | connection:string，Connected/Disconnected | S；PS | H1，失联非已停 |
| plc.automatic:bool | operatingMode:string，Automatic/NonAutomatic/Unconfirmed | S；PS | H1；缺依据Unconfirmed |
| plc.safetyClear:bool | safetyAssessment:string，Clear/ExplicitUnsafe/Unconfirmed | S；PS | H1，未观察不当Unsafe |
| plc.clamped:bool | clamp:string，Secured/Released/Unconfirmed | S；PS | H1 |
| plc.physicalStart:bool | 删除，无替代按钮字段；启动Accepted另由动作提交事实表达，正式设备现固定false不能称真实按钮 | S；PS | H2 |
| plc.palletLockStatus:int | 原值仅通信诊断；业务clamp按真实观察解释，不保留旧字段 | S；PS | H2 |
| plc.waitingForManualOccupancy:bool | manualHandling:string，Waiting/Confirmed/Unconfirmed；按真实人工流程与安全条件形成，不以旧false直接推Confirmed；manualArea独立业务观察 | S；PS | H1/H3 |
| plc.x / y / z:double（逐坐标） | position.actualX/actualY/actualZ:double?，有真实采样才赋；附轴用途/单位依据/观察身份，无值null | S；PS | H0/H3，不以零或目标补值 |
| plc.motionStatus:string | motionAvailability:string，Available/InUse/HeldUnknown；基于当前动作和租约，非原始阶段逐项改名 | S；PS | H1 |
| plc.inspectionStatus:int | 原值仅诊断；acquisitionReadiness:string为Available/Unavailable/Unconfirmed | S；PS | H2 |
| plc.zResetStatus:int | 原值仅诊断；周期释放是业务动作结果，不新增逐阶段reset字段 | S；PS | H2 |
| plc.alarmBits:ushort | 仅诊断，业务alarms及safetyAssessment | S；PS | H2 |
| plc.alarmSeverity:ushort | 仅诊断，alarms[].severity:string Warning/Fault/Critical/Unknown | S；PS | H2，未知不由ordinal推安全 |
| plc.plcSystemFault:bool | 移除线圈镜像；reasonCodes含稳定DeviceFault（有确认依据时）及对应安全评估 | S；PS | H1/H2 |
| plc.connectionEpoch:long | 原名long保留 | S；PS | H0 |
| plc.observedUtc:timestamp | sampleEndedUtc:timestamp；新增sampleStartedUtc/observationId，分别来自真实批次，不承诺原子采样 | S；PS | H0/H3；旧只有结束时间不补起始 |
| plc.diagnosticCode:string? | reasonCodes:string[]仅稳定业务原因（通信不可用/安全受限/证据未确认等）；内部错误文本去诊断 | S；PS | H1/H2 |
| plc.failureOrigin:string? | 内部通道/轮询故障来源仅诊断，不保留同名业务字段 | S；PS | H2 |
| plc.alarms[].bit:int | 删除，位号只在诊断 | S；PS | H2 |
| plc.alarms[].name:string | alarms[].name:string稳定已确认报警名称；severity/reliability为语义字段，组合报警逐项列出；无可靠报警样本时alarms=null | S；PS | H1/H3，不从新位表解旧值 |
| plc.source:string | executionOrigin:{provider,componentVersion?,quality}；provider仅Real/Virtual/Simulated/Unavailable，未记录版本null | S；PS | H1，保留旧来源声明性质，不自动改Real |
| 原无字段 | plc.schemaVersion、reliability、readiness、manualArea、observationId、diagnosticEvidenceReference?；全部依据模型§1/2，引用仅实际提交确认产生 | S；PS | H3；旧无观察ID/引用为null |
| revision:long / eTag:string | 原字段类型及缓存含义保留，摘要必须纳入新语义投影和版本 | S；PS | H0 |
| host:string | 原样保留 | S；PS | H0 |
| camera:{state,source}? / storage:{state,maintenance}? / maintenance:{state,maintenance}? | 原字段及各string类型保留；不可把未知维护变成已就绪 | S；PS | H0 |
| algorithm:{state:string,source:string?,reason:string?} | 原样保留，与设备来源分开 | S；PS | H0 |
| currentRun:object? | 按§5.2 R映射，null不变；禁止旧RunSnapshot整体漏raw | S/R；PS/PR | H0/H3 |
| capabilities:string[] / observedAt:timestamp | 原样保留；后者是响应时间，不取代设备采样时间 | S；PS | H0 |
| mode / stage / recipe / quality:string?；activeRuns:int | 各字段原名/类型/业务含义保留 | S；PS | H0 |

`plcReady/flipStatus/flipCurrentFace/manualZoneOccupied`当前虽在PlcObservation源码中，但不在status投影；不得伪列旧公开字段。目标readiness/manualArea来源于语义解释；自动翻面实际面放相关动作证据，不为对应每个寄存器扩status。

### 5.2 startupDiagnostic、run及历史

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| startupDiagnostic:object? | 保留对象可空；新增schemaVersion/recordNature/rawAvailability/diagnosticEvidenceReference? | R；PR | H3：当前历史重建默认null，旧Audit未保存完整StopStage，不能强行恢复 |
| reasonCodes:string[] | 原名/稳定业务原因保留，不嵌原始值供调用方解析 | R；PR | H0/H3 |
| safetyAssessment:string | 保留原StartupReadiness词汇Other/ExplicitUnsafe/Unconfirmed；设备Clear且因其他原因阻断→Other，ExplicitUnsafe→ExplicitUnsafe，无可靠观察→Unconfirmed（StartupReadiness:27）；不得发明Safe词汇 | R；PR | H0/H3 |
| stopStage:string / disposition:string | 分别原名保留；缺历史完整诊断则对象null，不补默认阶段/BlockedNoDeviceAction | R；PR | H0/H3 |
| connectionEpoch:long? / observedAtUtc:timestamp? | 各原名及可空性保留，与同次观察对应 | R；PR | H0/H3 |
| source:string | 替为executionOrigin对象；旧来源另在recordNature说明，未知provider=Unavailable | R；PR | H1 |
| reliableFeedback:object? | 删除，替semanticObservation:object?；无可靠快照null，不能空对象假定安全 | R；PR | H1/H3 |
| reliableFeedback.connected:bool | semanticObservation.connection:string | R；PR | H1 |
| reliableFeedback.automatic:bool | semanticObservation.operatingMode:string | R；PR | H1 |
| reliableFeedback.safetyClear:bool | semanticObservation.safetyAssessment:string（模型Clear/ExplicitUnsafe/Unconfirmed） | R；PR | H1 |
| reliableFeedback.alarmBits:ushort | 仅诊断，业务只含语义alarms/reasonCodes | R；PR | H2 |
| reliableFeedback.alarmSeverity:ushort | 仅诊断，语义报警严重性同§5.1 | R；PR | H2 |
| reliableFeedback.plcSystemFault:bool | 仅诊断，语义DeviceFault原因，禁止raw影子 | R；PR | H1/H2 |
| run.state:RunState传输值 / executionState:string | 两者保留，仍以executionState作流程展示；不改整个业务状态机 | R；PR | H0 |
| results[].dispositionState:string? | 原名/业务处置含义保留，新事实由已提交语义取放及有效关联形成；不足null | R；PE | H1；旧raw解析仅Infrastructure，结果标记录性质 |
| movements[].entityId:string / physicalSlotIndex:int / sourcePointRef,targetPointRef,state:string / operationId,committedEventId:UUID | 各字段原名/类型保留；状态为已提交处置，不据quality/Final猜已搬运 | R；PE | H0/H1 |
| results[].source:string?、quality:string?；组件来源矩阵 | 业务原字段保留实际来源/质量；新增记录性质不替换组件来源，不能把整盘混合来源压成单一Real或Virtual | R/E；PE | H1，旧声明不“校正” |
| 其余run业务字段（身份/修订、action/capture/algorithm/save/handoff、配置版本、recipe/quality/sorting/wholeTask、计划和提交ID、allowedActions、人工/恢复投影、results检查明细、resultContext、faultRestart） | 原RunSnapshot及各嵌套业务DTO的字段名、类型、null/集合和业务含义原样保留；只递归替换本表明确设备字段。历史allowedActions=[]；结果schema station01-result-display/1.0保持 | R；PR/PE | H0；无授权从历史推进当前动作 |

StartupReadiness顶层词汇必须按当前源码明确对照，不能把设备Clear直接透传覆盖既有页面原因；顶层与semanticObservation同一次观察且含义一致。原字段的缺失不能用新字段默认值回填。历史原值仍存原payload，S/R/E新业务响应只给已存业务摘要和记录性质；原始审计查询不成为业务恢复输入。

### 5.3 run/evidence与保存表示

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| stages[].positionEvidence:JsonElement?（原PositionEvidence数组） | 同名改为显式SemanticPositionEvidence[]?；无证据null。新增schemaVersion/recordNature及ObservationId?、DiagnosticEvidenceReference?，不再透传任意JSON | E；PE | H1/H3；原字节不改 |
| PositionEvidence.Phase:string | 显式映射kind:string：PickTargetObserved→SourcePositionReached；PlaceTargetObserved→TargetPositionReached；PickCompleted→PickObserved；PlaceCompleted→PlaceObserved；UnloadPositionValidated→PositionReached。PickWriteAcknowledged/UnloadWriteAcknowledged/PlaceWriteAcknowledged/SortingAckCleared四种仅诊断；解锁在独立动作结果，不新增位置阶段 | E；PE | H1/H2；不能仅凭旧phase声明新物理成功 |
| AxisRole:string | axisRole:string，已确认业务轴用途，未知Unconfirmed | E；PE | H0/H3 |
| TargetX / TargetY / TargetZ:double（逐坐标） | target.x/y/z:double，另附目标pointRef/version；目标始终为请求依据 | E；PE | H0，旧无目标版本null |
| ActualX / ActualY / ActualZ:double?（逐坐标） | actual.x/y/z:double?；完全无采样actual=null，不复制target | E；PE | H0/H3 |
| Status:ushort? | 删除业务字段；实际raw仅诊断，旧合成值只留原payload | E；PE | H2 |
| Tolerance:double | tolerance:double原业务容差，不因变体变更 | E；PE | H0 |
| ObservedAtUtc:timestamp | observedAtUtc:timestamp真实观察时刻 | E；PE | H0/H3 |
| Matched:bool? | matched:bool?，未知null，须与当前实际/目标关联 | E；PE | H0/H3 |
| stages[].eventId / operationId:UUID；attempt:int；connectionEpoch/sequence:long | 各原名/类型保留；新增actionId仅真实已建立时可空UUID | E；PE | H0/H3，不编造PLC ActionId |
| stages[].stage/eventType/planRevision:string；errorCode:string? | 各原名保留业务含义；UnknownHeld失败不能被Completed覆盖 | E；PE | H0 |
| stages[].source/quality:string | 原名保留，来源按真实事实；新增recordNature区分历史声明 | E；PE | H1 |
| stages[].persistedAtUtc:timestamp；stageStartedAtUtc/stageDeadlineAtUtc:timestamp? | 各原名/类型保留，采样/提交/截止分开 | E；PE | H0/H3 |
| motionEvidence:array?；[].writeId:UUID、committedAtUtc:timestamp | 原字段/提交身份保留；当前成功查询无记录为[]，目标保留[]；历史集合确实不可取得才null/Unavailable | E；PE | H0 |
| motionEvidence[].facts:JsonElement | 改有限语义事实对象；仅下两行、target细表及§5.5已登记业务键和schema/recordNature/引用，拒raw逃生口 | E；PE | H1/H3 |
| facts.kind/pointId/pointVersion/role/zAxis:string?；operationId/actionId:UUID?；attempt:int? | 各业务字段按原含义保留，缺失null；kind中内部协议名称不继续公开，换已保存业务事实用途 | E；PE | H0/H1/H3 |
| facts.actual:坐标对象?（target嵌套详见下表）；tolerance:number?；matched/accepted/completed:bool?；observedAtUtc:timestamp?；deviceEpoch/epoch:long? | 保留真实已存语义及可空性；accepted不等于completed，target不填actual。两epoch旧形状按实际字段读取，目标新事实统一connectionEpoch并保留关联依据 | E；PE | H0/H3；只能从旧真实X/Y/Z投actual |
| runId:UUID / trayId:UUID? / persistedRevision:long / stages:array | 原样保留；新增顶层deviceSchemaVersion | E；PE | H0 |
| wholeTrayCompletionId:UUID? / readyForUnlockSourceMatrix:object? / finalSourceMatrix:object? / finalResult:string | 原样保留含各matrix字段/组件，不由取放或解锁单个状态推Final | E；PE | H0/H1 |
| 新字段diagnosticEvidenceReferences:opaque[] | 仅实际持久可核查引用；数组无项不等于物理未执行，另用rawAvailability标缺失 | R/E；PE | H3，旧无raw不给引用 |

新持久设备事实用device-semantics/1；旧Writes.PayloadJson、StageEvents.PayloadJson、Operations.EvidenceJson及摘要不改。旧protocolStatus=2、sortingAckCleared=true等合成声明不迁为CapturedRaw。新旧payload解读边界在Infrastructure，Host业务投影不解析原始2/3或位。

`facts.target`不能整体透传：FixedMoveStep:122保存的是坐标，而IntegratedDetectionPort:767–769保存完整DetectionStepTarget（StagePortContracts:44–55）。目标使用有限`BusinessMotionTarget`形状，以下逐字段规则均属E/PE责任，H0/H3保留原存事实及缺失，不改变配方载荷。

| 旧target字段/类型 | 目标去向/类型/缺失与历史 |
| --- | --- |
| 简单形状X/Y/Z:double | target.point.x/y/z:double?；只能由已有目标填，其他身份null，不反推对象/面 |
| StepSequence:int | target.stepSequence:int?，业务执行序号，缺失null |
| ObjectId:string | target.objectId:string?，缺失null |
| SlotId:string | target.slotId:string?，缺失null |
| ProtocolSlotIndex:int | target.physicalSlotIndex:int?，真实物理槽，非wire宽度；缺失null，不用StepSequence替代 |
| LocalFace:int | target.localFace:int?，缺失null |
| HeightRound:int | target.heightRound:int?，保留真实测量关联，不由面序号补造 |
| Camera:string | target.camera:string?，已有业务相机角色，缺失null |
| PointRef:string | target.pointRef:string?，已有配置引用，缺失null |
| Point.Id/Version/Unit/Frame:string（四个字段） | target.point.id/version/unit/frame:string?，各自原含义保留，缺失null |
| Point.X/Y/Z:double（逐坐标） | target.point.x/y/z:double?，目标坐标不当实测 |
| Source:string | target.coordinateSource:string?，保留坐标配置来源，**不是设备执行Provider**；缺失null |
| ZBasis:string | target.zBasis:string?，实际配置/高度依据，缺失null |
| IsValid:bool（计算属性） | 删除公开字段；它只是当时配置校验派生，不是动作完成。旧payload字节保留，不从当前模型重算历史真假 |

### 5.4 通知逐字段与消费者

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| eventType:string | 保留StateChanged/DiagnosticChanged/HandoffReady/WholeTrayCompleted/ObservedUnlocked/FinalUnloadCompleted的既有含义 | N；PN | 原通知包不改，不作提交证明 |
| schemaVersion:string | s01/notification/1.0→N | N；PN | 历史包保留版本 |
| runId:UUID；revision/persistedRevision:long | 各字段原样保留；修订只作GET对账提示 | N；PN | H0 |
| changedFields:string[] | 仅业务语义路径；旧error提示统一errorCode，新增startupDiagnostic变化提示其路径；禁止raw/内部握手路径 | N；PN | 旧通知路径原样归档，不作为当前API字段 |
| summary:object?（实际整个RunSnapshot；TS当前声明string不符） | 固定对象或null：{executionState:string,wholeTaskState:string,errorCode:string?}，不含完整run或startup原始反馈。前端TS按对象对齐，通知只提示GET | N；PN | 不补造旧摘要、不重放作新运行状态 |
| occurredAt:timestamp | 原名保留通知产生时间，不等于采样/持久时间 | N；PN | H0 |

消费者实际边界：runtime.js:331–345展示启动原因，:345/381来源需由已有数据区域绑定真实值；notification-reducer.ts:1目前类型不符；CJS报警断言与Python取放码断言按V02.1拆分，旧raw断言由通信探针保留。上述绑定只在006既有页面区域/文字含义内调整数据，不改变客户原型结构、控件、静态文案或交互。外部未登记客户端发布前盘点，不能保留raw影子作兼容。

### 5.5 本轮配方应用期限的语义字段增量（待实现）

沿R/E的`device-semantics/1`及现有业务事实读取，不新增页面或诊断控制入口。下列新元数据仅来自当前真实冻结/观察/保存；历史缺项为null并标NotRecorded，不根据旧RecipePlanBound存在就反推预算、设备应用或及时回执。业务字段形状登记同时进入A02/A08—A10；不是任意JSON兼容通道。

| 旧字段/当前含义 | 目标字段、类型及缺失 | 版本、生产者/消费者与调整责任 | 历史 |
| --- | --- | --- | --- |
| BusinessBudget.businessMs无独立绑定预算；冻结JSON无此键 | `businessMs.recipeApplication:int`必需；新运行不得null/缺项，Test10000 | budget schema1.1；配置作者→Loader/Validator/Freezer→三入口；AL08/I25 | 旧快照原文保留，历史可读不补默认，不授权新绑定 |
| 当前RecipePlanBound事实含计划/版本，未记录完整总窗 | 既有`motionEvidence[].facts`增加有限`recipeApplication`对象；当前绑定事实必需，其他动作不输出此对象 | E及持久device-semantics/1；Application保存→Infrastructure只读投影→Host/业务采证；AL03/04/06/08 | 旧缺项null/NotRecorded，无raw补造 |
| 无当前绑定预算来源字段 | `recipeApplication.bindingId:string`、`budgetReference:{id,version,purpose,source,digest,snapshotId:string,budgetMs:int}` | 同上；来源为当前Run/Binding冻结事实；不由通信定义生成 | 任一历史来源未存则该字段null，整组不可完整取得为null |
| 无绑定唯一总窗及校验时刻 | `recipeApplication.window:{clockId:string,startTick:string,budgetDueTick:string,effectiveDueTick:string,startedAtUtc:timestamp,deadlineAtUtc:timestamp,applicableDeadlineReferences:[{stage:string,startedAtUtc:timestamp,deadlineAtUtc:timestamp}]}`；`hostValidatedTick:string?`。tick以既有Host时钟单位的十进制字符串保存/传输，避免JS整数精度损失；数组仅列实际存在的Detection/Unload/Sorting原截止 | 同上；业务登记/仲裁，不使用PLC时间回填；Host只投影，时钟单位/频率沿既有clockId来源记录 | 无旧时钟/起止记录则null；不得按10秒推算；适用截止历史未知不能填[]冒充确无 |
| 原绑定完成隐含设备成功、未分开保存回执 | `recipeApplication.deviceApplied:bool?`、`outcome:string`（Applying/AwaitingRequiredBusinessCommits/Completed/TimedOut/Cancelled/Failed/HeldUnknown）、`requiredCommits:[{writeId:string,kind:string,actualCommit:string,receiptValidity:string?,committedAtUtc:timestamp?,hostReceivedTick:string?}]`、`diagnosticEvidenceReferences:opaque[]` | 同上；未观察deviceApplied=null；kind限BindingIntent/RecipePlanBound/本次Handoff/RequiredCommunicationEvidence等保存用途。实际commit读存储元数据，Host回执读独立Audit观察，不在自身事务预填。完成须全回执及时有效 | 原行/真实提交不改；缺回执观察null/NotRecorded，不能自动标ValidCurrent |
| RunSnapshot.RecipeState及对应查询Bound/Ready当前可能仅据handoff存在 | 保留业务字段/序列化形状；只有本次所有必要回执按期齐备且当前资格有效才可续接Bound/Ready，已有绑定/handoff行仅为事实。按期Completed后的稍晚调度不重判总窗超时，仍核当前安全/取消/后段 | R；公共流程/当前Receipt→PublicPreparationHandoffV2、QueryEndpoints:155–157/173–174与通知GET；AL02/03/04/08。通知固定摘要不扩字段 | 旧状态仅历史声明；重启或迟到核查无当前资格不得恢复续接 |
| `/api/v1/recipes/bind`现200 `{plan,plcBinding:"CommittedAfterF"}`仅说明端口返回 | 保留plan业务含义；移除plcBinding字面成功，改`deviceSchemaVersion:"device-semantics/1"`与类型化`bindingResult`，包含bindingId、outcome、budgetReference、window、requiredCommits、诊断引用；200仅Completed且及时有效。已知拒绝/超期沿409错误响应携语义reason及关联；HTTP取消可无响应但不减弱后台关闭义务 | Application共同绑定能力→Host端点→API调用/采证；AL02/03/04/08，发布前盘点调用方；无新增页面 | 旧响应原包只读，不转换为新的有效Receipt，不留raw或plcBinding影子字段 |

必要成功/失败记录仍由业务事务持有；通信只产真实设备证据，不能为填上表直接写业务库。全库不可写时只能报告证据缺失，不保证新窗口终态已持久。上述目标结构还需跨功能API/配置合同实际对齐，当前源码和消费者尚未修改。

字段产生顺序是本表的约束：意图只存先已知的预算/关联，t0/D/T随后产生并进后续事实；原绑定/handoff只存写前已知信息与先前回执。自身HostReceivedTick/ReceiptValidity由提交后Application语义Audit观察独立记录，查询按WriteId连接，不改不可变handoff、不要求Audit递归证明自身回执。观察保存有界且仅为证据，不产生新的动作授权；缺失按上表null/NotRecorded并使所需验证证据不完整。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`仅作为旧payload中的原文保留，不生成同名新业务事实，不倒推历史原始值或来源。消费者不以旧名字/裸kind授予动作；当前面关联继续调用FaceEstablishment.Confirms，原证据与保存门禁不减。通信用例仍检验实际清零，业务断言迁移到当前语义事实和来源，两侧均必需；不新增页面、信号、恢复路径或产品兼容层。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。
## 6. 前轮通信专项影响归属（历史；本轮当前义务见§7）

| 项 | 专项活动义务 | 转出部分 / 后续方向 |
| --- | --- | --- |
| I01 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I02 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I03 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I04 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I05 | A/B语义接线及稳定期限/取消/保存共窗直接依赖保留 | 三入口完整预算业务验收转001/002/008 |
| I06 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I07 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I08 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I09 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I10 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I11 | 正式IntegratedDetection直接语义迁移/编译/边界 | 相机/算法/媒体及全配方整机回归转007/008 |
| I12 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I13 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I14 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I15 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I16 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I17 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I18 | 必要raw存储/当前直接公开形状和真实准入保留 | 旧库升级/完整历史投影转003/001/008 |
| I19 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I20 | 必要raw存储/当前直接公开形状和真实准入保留 | 旧库升级/完整历史投影转003/001/008 |
| I21 | 直接消费者类型绑定/构建及无raw控制保留 | 完整页面/通知/发布归006，不作T061前置 |
| I22 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I23 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I24 | 原登记中的通信或直接语义职责继续，精确路径/接口/冻结归属不变 | 不把原整机/完整业务验证关联当专项前置 |
| I25 | 已用预算配置/构造者可编译且不回退，现用来源冻结 | 新增预算完整配置发布和环境/配方耦合修复转001/007/008 |
| I26 | A/B语义接线及稳定期限/取消/保存共窗直接依赖保留 | 三入口完整预算业务验收转001/002/008 |
| I27 | 已用预算配置/构造者可编译且不回退，现用来源冻结 | 新增预算完整配置发布和环境/配方耦合修复转001/007/008 |

AL01—08原已落入其他功能的业务/预算/升级/消费者要求不删除。需要的最小排期及验收范围引用对齐见scope-adjustment；本轮不修改其他功能，新增共享接口仍按原责任/复核/真实证据前置。

## 7. 本轮最小边界影响与真实代码前置

I01—12/I17/I18：正式语义端口、所有直接消费者和Host同实例装配为当前义务，内容检查与构建；原相关保存/期限规则不改。I08取料回调/真实业务事务不得半接线。
I13—16/I19/I20：核原始捕获/存储/reader为合法通信诊断及业务无raw控制/伪造；完整持久历史/迁移中断验收转出。I21前端完整绑定/发布仍归006，不作本轮前置，不新增页面或改原型。
I22：复用已有C#/脚本分类及检查器/测试；T053新增独立有限固定清单、T054只增最小接线，旧完整平台/案例不改。
I23—27：原完整配方/预算/流程验收转出；现有通信消费者签名/Stable窗口/取消与必要writer必须编译且不回退。

若本轮不改变共享签名，复核已有实际跨功能对齐即可；若发现必须改签名，先按对应AL实际spec/contracts/plan/tasks定向对齐后改代码，不修改历史勾选。当前完成定义变更只在009，不删除001/002/003/006/007/008已确认业务规则；其他功能仍按自己完整验收结项，不据BoundaryMinimum Passed声称其通过。
