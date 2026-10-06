# 功能任务清单：配方驱动的完整检测执行

**输入**：[spec.md](spec.md)、[plan.md](plan.md)、contracts及coverage-matrix  
**宪章版本**：3.2.0；**日期**：2026-09-24  
**状态**：48项全部未实施，不因本轮写完文档而勾选。文件路径相对仓库根；新增文件路径表示拟创建，不代表现存。
P0只准备本功能，不初始化工程。每项完成须满足下表及证据合同；默认P03/04/05/07/08/09/11/13，前端另P12。
各阶段独立目标、两用例、必要失败和完成条件详见plan的P0—P6；E2E必须正式入口，直接调用函数只能辅助验证。

## 必要准备 P0：合同、用例与基础

- [ ] T001 收集并冻结B01—B08缺失输入，逐项更新合同及受影响规格，记录不能关闭项。产物：`specs/008-recipe-driven-inspection/contracts/execution.md`。追溯：FR-001/005/006/007/009/010/012。依赖：无；仅实际使用点阻塞。完成：状态转换/业务决定有来源，未答项仍Blocked。

- [ ] T002 建立12个Test配方及身份区分素材清单，落实recipe-cases的参数/槽位差异与F唯一登记。产物：`specs/008-recipe-driven-inspection/fixtures/recipes.json`。追溯：FR-004/015。依赖：spec/recipe-cases；不等全部B。完成：可共用正式目录加载；未定语义不可生成可派发步骤。

- [ ] T003 固化强类型步骤/对象/搬运映射、结果持久化和公开投影字段；同步消费者合同后实现模型及StorePrep迁移。产物：`backend/src/Gaode.Application/Recipes/RecipeContracts.cs；backend/src/Gaode.Infrastructure/Persistence；backend/tools/Gaode.StorePrep`。追溯：FR-001/007/008/010/016。依赖：T002。完成：无0,0事实占位，组/成员/整体身份可表达；维护迁移有版本。

- [ ] T004 补22序列同一规划器表达检查与必要能力/用途拒绝，解开场景和机械路线绑定。产物：`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs；backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs；backend/tests/Gaode.Rules.Tests/Recipes`。追溯：FR-004；SC-002。依赖：T003。完成：22项加载/顺序断言，普通S3合法，未知能力不执行。

- [ ] T005 由计划及既有采集/worker延迟计算动作/阶段/整盘预算，保存绝对deadline与配置来源。产物：`backend/src/Gaode.Application/Workflow；specs/008-recipe-driven-inspection/evidence/budget.md`。追溯：FR-013。依赖：T004。完成：给出AB/CD与多槽/多面调用数及余量，不套120秒、不重置期限。

## 用户场景 US1：单面AB/CD完整执行

- [ ] T006 [US1] 实现应用层有限执行器消费Position等全部已支持步骤，经唯一Motion/Safety准入，未支持步骤明确受限。产物：`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`。追溯：FR-002/014。依赖：T003—005。完成：已定义定位1/2链可逐步观察；意图提交后才派发，日志可关联。

- [ ] T007 [US1] 在已确认B01合同下接入F扫码3/4及适用轴/清零，并保留公共点位独立和唯一绑定。产物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs；VirtualPlc/VirtualPlcEngine.cs`。追溯：FR-001/006。依赖：T006；B01/B04适用语义。完成：F失败阻断产品，最新扫码握手实际闭合，旧1/2证据不抵扣。

- [ ] T008 [US1] 接通产品检测XYZ/适用Z、实际到位、Inspection1/2及复位；公共handoff携真实对象/点位引用。产物：`backend/src/Gaode.Application/Station01/PublicPreparationHandoffV2.cs；backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`。追溯：FR-001/002。依赖：T006；B04适用轴。完成：XYZ不匹配/复位失败无采集或下一动作。

- [ ] T009 [US1] 实现AB/CD按相机整批、实际应用光源/曝光/ROI参数及按对象面相机选固定素材。产物：`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs；backend/src/Gaode.Infrastructure/Simulation/FileBackedCapture.cs`。追溯：FR-002/003。依赖：T008/T002。完成：AB1/2、CD1/2实际参数/顺序不同，媒体独立可读。

- [ ] T010 [US1] 保留每图独立worker单输入分析并在保存后复位，双输入齐后另发同面融合请求，配对键含对象/面/轮次，前批不等融合。产物：`backend/src/Gaode.Application/Workflow/FaceResultAggregator.cs；backend/src/Gaode.Infrastructure/Algorithms`。追溯：FR-003。依赖：T009。完成：worker实际读双输入，缺输入有限Pending；不把相机当面。

- [ ] T011 [US1] 保存采集/算法意图事实及面结果，关键保存失败阻断后继；保留分级分类异常关联。产物：`backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs；backend/src/Gaode.Infrastructure/Persistence/StageEventStore.cs`。追溯：FR-002/003/014。依赖：T010。完成：SQLite与媒体能查回，失败未假完成；原异常上下文存在。

- [ ] T012 [US1] 将正式Host Detection装配到公共应用执行器，验证到位不符/worker超时/保存失败的最小集。产物：`backend/src/Gaode.Host/Composition/AdapterBindings.cs；backend/tests/Gaode.Integration.Tests`。追溯：FR-001/002/003/014。依赖：T007—011。完成：正式链实际调用；物理未知不因算法Pending放行。

- [ ] T013 [US1] 端到端验证M01上料与启动：AB1、CD1分别从正式入口完成上料→授权启动→安全/夹紧→公共区域ACK→M02，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M01。依赖：T012；B07/B08真机；B01/B09完整链。完成：两配方各有设备/适用采集worker/SQLite/终态事实；整盘启动事实；搬运整盘。

- [ ] T014 [US1] 端到端验证M02公共3D采集：AB1、CD1分别从正式入口完成公共点→实际到位→光源/3D→高度/异常保存→复位，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M02。依赖：T012；B04真实轴/高度；B01/B09完整链。完成：两配方各有设备/适用采集worker/SQLite/终态事实；整盘公共高度；不据此伪造槽位占用。

- [ ] T015 [US1] 端到端验证M03F料盘扫码及唯一绑定：AB1、CD1分别从正式入口完成F点到位→单次扫码→保存→适用复位→唯一绑定/冻结，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M03。依赖：T012；B01；生产码登记B04/B05适用部分。完成：两配方各有设备/适用采集worker/SQLite/终态事实；TrayRun/Recipe；无产品搬运。

- [ ] T016 [US1] 端到端验证M04普通AB批采与融合：AB1、AB2分别从正式入口完成A遍历有效槽→逐次保存/复位→B同批→同面融合→单件结果，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M04。依赖：T012；B04轴；B01/B09完整链。完成：两配方各有设备/适用采集worker/SQLite/终态事实；相机→面→单件/成员/部位；本步不搬运。

- [ ] T017 [US1] 端到端验证M05普通CD批采与融合：CD1、CD2分别从正式入口完成C整批→D整批，各自定位/采集/保存/复位→同面融合，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M05。依赖：T012；B04；B01/B09完整链。完成：两配方各有设备/适用采集worker/SQLite/终态事实；同M04。

## 用户场景 US2：多面与人工介入

- [ ] T018 [US2] 实现自动换面实体选择/取放上下文/面号核验，S3共享整体姿态只执行一次。产物：`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs；VirtualPlc/VirtualPlcEngine.cs`。追溯：FR-005。依赖：T012；B02/B04。完成：面号不符阻止重扫/检测，单面成员不重复。

- [ ] T019 [US2] 接通正式人工介入与后端授权完成确认、实际安全/面号门禁和持久审计。产物：`backend/src/Gaode.Host/Api/RunEndpoints.cs；backend/src/Gaode.Application/Workflow`。追溯：FR-005/012。依赖：T018；B07。完成：占用时不发运动，人工确认不能解除硬件安全。

- [ ] T020 [US2] 执行RescanWholeTray并使旧高度失效，保存新轮次及对象/面映射。产物：`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs；backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`。追溯：FR-005。依赖：所选AUTO用T018，MANUAL用T019；B04。完成：两/四面实际重扫，后续Z来自本轮有效输入。

- [ ] T021 [US2] 按确认的E码时机/放行及独立轴合同接入采集/worker/保存/复位。产物：`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs；backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`。追溯：FR-006。依赖：T020；B01/B04/B06。完成：E码属于正确内部对象，失败原始上下文保存。

- [ ] T022 [US2] 端到端验证M06自动翻面：MX1-AUTO、MX2-AUTO分别从正式入口完成前面结束/复位→翻转位→整体取翻放→匹配面号→M08，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M06。依赖：T018—021适用项；B02/B04/B05；B01/B09完整链。完成：两配方各有设备/适用采集worker/SQLite/终态事实；S1单件、S2成员、S3整体；保留原ID。

- [ ] T023 [US2] 端到端验证M07人工翻面介入：MX1-MANUAL、MX2-MANUAL分别从正式入口完成占用停派发→人工换面→安全恢复→授权确认→面核验→M08，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M07。依赖：T018—021适用项；B07/B04；B01/B09完整链。完成：两配方各有设备/适用采集worker/SQLite/终态事实；实际被翻实体，不能软件确认硬件安全。

- [ ] T024 [US2] 端到端验证M08翻面后整盘重扫：MX1、MX2分别从正式入口完成保持身份→换本面固定点→旧高失效→整盘3D→新轮次保存→下一面，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M08。依赖：T018—021适用项；B04；前置M06/07合同。完成：两配方各有设备/适用采集worker/SQLite/终态事实；整盘测量及适用对象高度；不改变搬运身份。

- [ ] T025 [US2] 端到端验证M09E工件扫码：MX1、MX2分别从正式入口完成到配置面→E点/独立Z→采集/解码→身份绑定/异常→清零复位，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M09。依赖：T018—021适用项；B01/B04/B06。完成：两配方各有设备/适用采集worker/SQLite/终态事实；成员/组/整体外部码，不替内部ID。

## 用户场景 US3：组结果与真实分拣

- [ ] T026 [US3] 实现面→成员→组结果汇总和已确认的组处置策略；删除review默认策略充生产规则的路径。产物：`backend/src/Gaode.Application/Workflow/ObjectResultAggregator.cs；backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs`。追溯：FR-007。依赖：T010/T003；物理处置需B05/B06。完成：组保成员明细，不缺员OK；未定策略受限。

- [ ] T027 [US3] 实现真实源槽/成员物理索引、源目标与容量预留，替换Sequence充槽位及handoff零坐标。产物：`backend/src/Gaode.Application/Workflow/SortingTargetAllocator.cs；backend/src/Gaode.Application/Workflow/RecipeSortingMapper.cs；backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`。追溯：FR-010。依赖：T026；B02/B05/B08适用布局。完成：非连续槽及NG/Pending分区正确，不同实体不重占。

- [ ] T028 [US3] 按已确认源/目标握手实现PLC与VirtualPlc取放并保存真实占用/组处置完成。产物：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs；VirtualPlc/VirtualPlcEngine.cs`。追溯：FR-007/010。依赖：T027；B02/B08。完成：无提前覆盖源点；满位/未知反馈不完成/不重发。

- [ ] T029 [US3] 端到端验证M14普通盘末分拣：AB2-NG、CD2-Pending分别从正式入口完成结果/原槽→目标预留→源到位取→目标到位放→匹配完成，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M14。依赖：T026—028；B02/B08；B06混合场景。完成：两配方各有设备/适用采集worker/SQLite/终态事实；S1单件，S2成员，S3整体；NG/Pending分区。

- [ ] T030 [US3] 端到端验证M15成组处置：G1、G2分别从正式入口完成面→成员→组结果→已确认策略→全部应搬成员→组处置完成，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M15。依赖：T026—028；B05/B02/B08；B06混合。完成：两配方各有设备/适用采集worker/SQLite/终态事实；判定Group；搬运Part，不能一员代全组。

## 用户场景 US4：整体与旋转

- [ ] T031 [US4] 实现普通S3部位结果汇总整体、共享姿态和整体分拣，支持ordinaryAssembly。产物：`backend/src/Gaode.Application/Workflow/ObjectResultAggregator.cs；backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs`。追溯：FR-008。依赖：T003/T010；适用T020/T028。完成：A1/A2一实体一处置，部位ID不作抓取ID。

- [ ] T032 [US4] 按B03确认合同实现类型1进站/占用/旋转姿态与逐相机检测；共享PLC会话。产物：`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs；backend/src/Gaode.Infrastructure/Devices/Plc；VirtualPlc/VirtualPlcEngine.cs`。追溯：FR-009。依赖：T031/T028；B02/B03/B04。完成：无猜测角度/地址，实际反馈后才采集。

- [ ] T033 [US4] 实现类型1OK原槽回放、NG/Pending分区放料及出站空闲，排除盘末重复分拣。产物：`backend/src/Gaode.Application/Workflow/RecipeSortingMapper.cs；backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`。追溯：FR-008/009/010。依赖：T032；B06混合结论。完成：三出口匹配实际反馈，位置未知保持在途。

- [ ] T034 [US4] 端到端验证M10特殊件进旋转工位：R1、R2分别从正式入口完成保存原槽→源点→取料→安全抬升→旋转位→放料/占用，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M10。依赖：T031—033；B02/B03/B04。完成：两配方各有设备/适用采集worker/SQLite/终态事实；Part或Assembly搬运；S2须保Group关联。

- [ ] T035 [US4] 端到端验证M11旋转与多姿态检测：R1、R2分别从正式入口完成可靠装载→指定姿态→实际到位→A/B或C/D定位采集→保存/复位→下一姿态，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M11。依赖：T031—033；B03/B04；不猜90/180度。完成：两配方各有设备/适用采集worker/SQLite/终态事实；物理实体姿态，部位/面结果。

- [ ] T036 [US4] 端到端验证M12特殊OK回原位：R1-OK、R2-OK分别从正式入口完成旋转位取→安全抬升→原槽→放料→空闲→提交，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M12。依赖：T031—033；B02/B03。完成：两配方各有设备/适用采集worker/SQLite/终态事实；单件/整体回原位，不重复分拣。

- [ ] T037 [US4] 端到端验证M13特殊NG/Pending放料：R1-NG/Pending、R2-NG/Pending分别从正式入口完成结果→选独立区域目标→取放→完成核验→占用提交，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M13。依赖：T031—033；B02/B03/B06混合优先级。完成：两配方各有设备/适用采集worker/SQLite/终态事实；物理实体；两用例各测NG与Pending出口。

- [ ] T038 [US4] 端到端验证M16半成品处置：A1、A2（R2另覆旋转）分别从正式入口完成部位全检→整体结果→整体一次处置→可靠位置，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M16。依赖：A1/A2用T031及适用T020/T028，不依赖旋转；R2补验另需T032/T033及B03；B02/B04。完成：两配方各有设备/适用采集worker/SQLite/终态事实；Assembly搬运，部位仅质量追溯。

## 用户场景 US5：下料、示教和恢复

- [ ] T039 [US5] 将无在途/全部应处置/必要保存门禁接入下料与解锁，复用003已修复XYZ/安全Z判据。产物：`backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs；backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`。追溯：FR-011。依赖：适用T028/T033。完成：解锁/取盘/Final事实独立，未完成不提前解锁。

- [ ] T040 [US5] 实现授权维护示教正式后端入口、PLC坐标读回、Point类型/配方版本保存及退出就绪复核。产物：`backend/src/Gaode.Host/Api；backend/src/Gaode.Application/Recipes；backend/src/Gaode.Infrastructure/Devices/Plc`。追溯：FR-012。依赖：T003；B04/B08。完成：AB1/CD1不同点位保存新版本，不改旧快照，不新增原型控件。

- [ ] T041 [US5] 完善故障复位/人工核对配方与实物的恢复路径及原任务审计，不自动续跑未知动作。产物：`backend/src/Gaode.Application/Workflow；backend/src/Gaode.Host/Api/RunEndpoints.cs`。追溯：FR-012/014。依赖：T039；B07。完成：复位就绪不是完成；人工决定/最后可靠状态可追溯。

- [ ] T042 [US5] 端到端验证M17下料与人工取盘：AB1、CD1分别从正式入口完成全盘收敛保存→无在途→安全Z/下料到位→解锁读回→取盘确认→Final，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M17。依赖：T039—041适用项；B09/B01完整链；前置处置依赖。完成：两配方各有设备/适用采集worker/SQLite/终态事实；整盘；解锁/人工确认分开。

- [ ] T043 [US5] 端到端验证M18示教：AB1-TEACH、CD1-TEACH分别从正式入口完成停止自动→授权维护→实际移动/选点→读XYZ→确认保存→退出→就绪复核，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M18。依赖：T039—041适用项；B04/B08现场点位/缓存；维护入口实现。完成：两配方各有设备/适用采集worker/SQLite/终态事实；点位版本/操作者；无额外搬运或算法要求。

- [ ] T044 [US5] 端到端验证M19故障复位与恢复：AB2-RECOVER、CD2-RECOVER分别从正式入口完成停派发→保留意图/反馈→人工排障→复位→实际就绪位置→人工核对→受限或确认，对照配置差异及必要失败。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001；M19。依赖：T039—041适用项；B07；不把复位成功当任务恢复。完成：两配方各有设备/适用采集worker/SQLite/终态事实；原任务/实体/位置，不自动重放。

## 收尾与证据 P6

- [ ] T045 汇总尚未覆盖的必要失败：绑定非法、缺输入Pending、坐标/面号不符、复位失败、满位、断联UnknownHeld、保存失败及日志定位。产物：`backend/tests/Gaode.Integration.Tests；specs/008-recipe-driven-inspection/evidence/failures.md`。追溯：FR-001/002/005/010/012/014；SC-004。依赖：适用实现及旧证据适用性审核。完成：只补当前必要缺口，进程退出后可从requestId定位，不全组合重测。

- [ ] T046 实现已提交的层级结果/当前步骤/真实搬运投影与通知，先交付可消费后端合同供006 T047绑定。产物：`backend/src/Gaode.Host/Api；specs/006-frontend-station01-console/contracts/api.md`。追溯：FR-016；SC-005。依赖：T003及适用结果实现；页面B09。完成：后端GET/通知同一已提交事实且字段可消费；不以页面完成作为本任务前置，随后006 T047产页面证据。

- [ ] T047 执行矩阵全量追溯核对，收齐每M两个不同配方的正式入口/PLC/采集/worker/SQLite/终态和差异对照。产物：`specs/008-recipe-driven-inspection/evidence/index.md；artifacts/recipe-execution-008`。追溯：FR-015；SC-001/002/003。依赖：19个E2E任务与T045/046及006 T047页面证据；对应B解除。完成：19/19各两例真实证据；22表达检查；缺项保持Blocked/NotRun。

- [ ] T048 复核spec/contracts/plan/tasks及最终范围，记录虚拟来源、未完成/受限/延期，不宣称真机精度或全项目完成。产物：`specs/008-recipe-driven-inspection/analysis.md；specs/008-recipe-driven-inspection/evidence/index.md`。追溯：FR-015；SC-001—005。依赖：T047。完成：仅实际通过范围有结论，历史记录和证据不覆盖。

## 任务追溯与依赖

P0 T002→T003→T004→T005→P1 T006；T001收集输入仅阻塞实际依赖项。P1→P2；P3结果模型可在P1基础上独立推进，但G1/G2多面E2E依赖P2。P4普通S3可先建模，旋转需B03；P5使用相应路线结果，P6汇总。

独立工作示例：T001输入记录与T002用例定义可同时推进；已完成T003后预算资料核算与独立worker接口研究可分开。执行器/规划器/Mapper共享文件，未标[P]任务应顺序编辑，避免冲突；不授权本轮自动开工。

最小增量是AB1/AB2及CD1/CD2的定位到复位真实链；缺B01时只能报告该已执行片段，不能称完整E2E。最终仍需19类每类两配方，不能将M06—19移出验收以缩小目标。

## 当前阶段范围与关键规则证据

| 规则 | 任务 | 证据 |
| --- | --- | --- |
| 公共准备到最后状态的正式链 | T007/T012及19个M任务、T047 | 同run入口、适用设备/采集/worker/DB/终态 |
| 配置/22序列及计划预算 | T002—005 | fixtures、规则结果、预算及实际调用对照 |
| 物理安全、未知不重发、必要保存 | T006—012、T028/T033/T039/T041/T045 | 意图/反馈、UnknownHeld、无后继命令、保存结果 |
| 组与整体结果、真实源目标 | T026—038 | 对象结果链、槽位/预留/完成事实 |
| 诊断/来源与前端边界 | T045—048、006 T047 | 持久日志、GET/通知/原型既有位置 |

延期项见plan；缺协议/处置是局部Blocked，非边界优化。所有已有功能任务和勾选维持历史状态。
