2026-09-26 PARAM事实增量：按共享API合同保存实际CaptureFact/RequestedCaptureSettings并从现有媒体查询投影；008 T054、003 T068承接，原编号及完整条件不变。

2026-09-26夜间既有任务实施定位：T057按execution合同的SortingAssignmentsReserved/InTransit/Occupied增量在ThreeStageWorkflowExecutor与003设备适配器共用现有持久通道，补目标冲突/取料后保存门禁的最少测试。T068/T069消费001 T052正常安全边界Pause/Continue补缺，关联`.specify/bugs/008-pause-continuation/assessment.md`及同run/无重扫实际验证。T059补原指定Q01-NG/Q02-PENDING独立新Test包，T069补已有真实3D媒体后F运动故障的最少正式恢复。上述均为原任务剩余条件，不追加重复任务、不改变勾选。

2026-09-26 USR-20260926-D任务增量：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替换活动任务的旧U05单指令故障重发要求；旧证据仅历史适用，暂停/人工继续及合法局部重试保留。编号、勾选不变，新恢复尚未实现/验证。

2026-09-26旋转Test增量：T065/T067按[虚拟旋转请求/结果合同](../003-plc-latest-protocol/contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。

2026-09-26 人工子范围增量：T060/T068按[人工执行合同](../003-plc-latest-protocol/contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

# 功能任务清单：配方驱动的完整检测执行

T061本轮实施依据[003 E Test合同](../003-plc-latest-protocol/contracts/e-test-execution.md)，在现有逐步执行入口接E相机/EDecode、真实保存、3/4复位及缺码留问题继续；仅实际完整条件满足后勾选。原任务编号不变。

2026-09-26连续收口：T050/T052/T053/T057/T060/T063/T064增量采用[多对象Test合同](contracts/test-multi-object.md)。单成员不填缺多成员目标；GROUP只处置问题成员，ASSEMBLY按部位保存与整体一次动作；T066须实际页面验证后才完成。编号和既有勾选保留。

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

**日期**：2026-09-24；**宪章**：7.0.0；**输入**：spec.md、plan.md、data-model.md、contracts、recipe-cases.md、coverage-matrix.md、quickstart.md。
**历史状态快照（不代表当前运行状态）**：2026-09-26文档对齐；全部既有任务勾选保持。旧协议Q01/Q02与Q01-PARAM正式页面Passed，Q03仅组件；新版Q=0/22、C=0/8，必要F尚待按影响验证。

文件路径相对仓库根；拟新增路径表示交付位置，不能当作已有证据。S0—S5是开发阶段；S1/S2/S3业务场景另称“业务S1/业务S2/业务S3”。任务按用户故事分组，US5结束链提前到S1。共同原则P03/04/05/07/08/09/11/13，前端P12服从当前原型只读、仅绑定现有控件规则；不用修改宪章迁就方案。

## 目录引用与唯一归属

| 简称 | 完整功能目录 | 本次唯一实现职责 |
| --- | --- | --- |
| 001 | specs/001-station01-public-preparation | T090公共期望/F校验和handoff |
| 002 | specs/002-plc-xyz-recipes | T11目录、版本、身份模型及规划校验 |
| 003 | specs/003-plc-latest-protocol | T065心跳修复；T067 F；T068 API；T069结束链；T070产品PLC；T071搬运/旋转PLC；T072人工接口 |
| 006 | specs/006-frontend-station01-console | T048选择/请求/状态；T049取盘结果；T050换面；T051恢复 |
| 007 | specs/007-station01-integrated-loop | T029既有心跳验证；T031环境；T032 worker两端；T033页面采证工具 |
| 008 | specs/008-recipe-driven-inspection | 以下T049—T070；配方数据、执行/汇总/处置及完整验收 |

以下“specs/003-plc-latest-protocol T068”等都是本表完整目录+任务ID引用，不是同号的008任务。共享实现不在008重复登记；008验收消费交付证据，不重新实现它们。

## 历史保留与处置

旧清单逐字节保存于[tasks-history-before-s0-s5-20260924.md](tasks-history-before-s0-s5-20260924.md)，T001—T048原意与未完成勾选不变。

| 旧任务 | 当前处置/承接 | 原因 |
| --- | --- | --- |
| T001 | 替换T049 | F已定义、原型已授权，仅局部输入仍阻塞 |
| T002 | 替换T050及007 T031 | 去固定12配方，改Q/C数据和版本夹具 |
| T003/T004 | 替换002 T11、specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T068、T054 | 一个模型、真实身份及接口双方责任 |
| T005 | 替换T051 | 实际调用量预算 |
| T006/T008/T012 | 替换T052、specs/003-plc-latest-protocol T070、specs/001-station01-public-preparation T090 | 正式执行全部适用步骤 |
| T007 | 替换003 T067 | 新F合同不再等定义 |
| T009/T010/T011 | 替换T053/T054、specs/007-station01-integrated-loop T032 | 逐图保存复位及同面融合 |
| T013—T017 | 替换T055/T059 | 公共步骤随完整Q追溯，取消每M固定两配方验收门槛 |
| T018—T021 | 替换T060/T061、specs/003-plc-latest-protocol T071/T072、specs/006-frontend-station01-console T050 | 自动/人工独立，E独立输入 |
| T022—T025 | 替换T062 | 22完整序列及C02操作场景 |
| T026—T030 | 替换T057/T063/T066、specs/003-plc-latest-protocol T071 | 真实源目标和组策略，不重复PLC实现 |
| T031—T038 | 替换T064—T067、specs/003-plc-latest-protocol T071 | 普通整体独立于旋转 |
| T039/T042 | 替换003 T069、specs/006-frontend-station01-console T049及T055 | 首条路线就必须下料到Final |
| T040/T043 | 延期 | 新增示教本轮已明确延期，不纳入完成门禁 |
| T041/T044 | 替换T068/T069、specs/003-plc-latest-protocol T072、specs/006-frontend-station01-console T051 | 必要人工恢复，未知不重放 |
| T045 | 替换T069，各阶段先验直接失败 | 不把全部失败推到最后 |
| T046 | 替换003 T068、specs/006-frontend-station01-console T049 | 后端先交付事实，前端消费，无互等 |
| T047/T048 | 替换T070 | 当前适用Q/C8/F必要集；旧analysis只属历史，不覆盖 |

## S0：首条路线准备（US1公共基础）

目标：能配置、选择并受理Q01，F/心跳/目录/预算组件各有证据；这不等于Q01已通过。先做003 T065、T067，specs/002-plc-xyz-recipes T11、specs/007-station01-integrated-loop T031/T032及下列任务；前端006 T048不拖到最后。无需工程初始化或全面重构。

- [X] T049 核对并登记实际所用输入，产物`specs/008-recipe-driven-inspection/contracts/execution.md`的B表及`evidence/input-readiness.md`。对应FR-001/002/005—010/012/018；依赖：现有来源与当前代码，无实现前置。先写Q01逐动作输入表：公共ACK既有Test依据、AB产品轴/目标字段/到位反馈、适用Z与高度scope/单位/基准/槽位映射。给来源页段及已定义/缺失/不适用依据；数值可Test，语义不得编造。缺B04时明确阻塞003 T070及Q01运动验证，其他准备继续。E/B02/B03/B05—08只限制各使用点；F与原型许可不是待外部输入。解除条件为已确认来源覆盖实际动作，不能用任务勾选代替现场决定。 输入依据补`specs/008-recipe-driven-inspection/sequence-alignment-20260926.md`逐步骤来源表；先核Q03所用公共/产品/翻面/下料目标，旧图差异不由代码裁决。 USR-E增量：在原input-readiness记录003动作合同取放目标阶段与抬升后实时Z的生产采样限制；不得据状态2编造Z相等。Test语义已可执行，生产限制只对应分支；问题1—5根因待实际包核验。

- [X] T050 建立版本化Q/C配方数据与受控媒体清单，路径`specs/008-recipe-driven-inspection/fixtures/recipes.json`、`fixtures/cases.json`、`fixtures/media-manifest.json`。对应FR-004/015/017/018、当前适用Q/C01—C08；依赖：specs/002-plc-xyz-recipes T11模型、specs/007-station01-integrated-loop T031清单格式；先交付Q01数据，其余按阶段增量，不等待全部B才准备数据。冻结recipe-cases规定的真实序列/合法槽位/身份/参数/F码（核对无冲突）、点位与预算引用；未确认能力可描述但不得标可执行。必要验证：当前采用Q数据编号/F码唯一、用途/版本引用合法、非法映射受限，素材实际可读且标Simulated；不是运行通过证据。 USR-E增量：先改`fixtures/generate-q04-q22-test.py`及GROUP/ASSEMBLY生成器为显式版本化当前集合，不再从历史五列表无差别生成或断言Q03—Q22共20；改造后才生成新目录/fixture/素材-worker清单及所有摘要，旧包不覆盖。允许四面Q08/Q09/Q11/Q14/Q15/Q18/Q20/Q21、退出Q07/Q10/Q12/Q13/Q16/Q17/Q19/Q22；允许/实际采用/验证集合分列，起始Q09/Q18，不设14/8全跑。依002 T11准入、复用007 T031格式，Host实际目录/F唯一/版本/冻结目标与页面及直接入口一致。GROUP保留源表成员面数、Review未有生产依据仍Restricted；生成/加载拒绝证据沿`evidence/input-readiness.md`及新包manifest。

- [X] T051 从冻结计划推导并保存动作/阶段/整盘期限，路径`backend/src/Gaode.Application/Workflow`及`specs/008-recipe-driven-inspection/evidence/budget.md`。对应FR-013/018、Q/C全体；依赖：specs/002-plc-xyz-recipes T11、T050的Q01清单。交付：C图数、F融合数、A=C+F，公共初始3D/F和适用E另计，本路线翻后重扫为0；加入逐实体翻面定位/机械执行/ACK与分拣取料/放料/ACK，期限顺序Detection→Unload→Sorting，运动/保存/重试/人工等待均有配置来源和绝对deadline；必要检查单面/多槽/四面预算，不重置尝试期限、不沿用120秒或放宽3秒心跳。后续新Q沿同公式增量计算，预算检查不代替实际耗时证据。 USR-E增量：在`RecipeWorkload.cs`、`RecipeExecutionBudget.cs`消费T050当前清单，核对Q09/Q18每单对象四面8采集/4融合/12检测worker，初始3D/F另计且翻后3D为0；取放阶段/ACK按合同版本化Test时序计入冻结预算，不放宽1秒I/O、3秒心跳或阶段期限。预算子能力可先供执行，不等全部Q，原budget证据对照实际调用。

阶段结束须标“S0准备完成”或具体组件通过；不能标Q01完成。暂不纳入真机参数定值、示教、编辑保存、热加载及平台化工具。

第三批实施说明（不改变任务完成条件）：T051公式/冻结期限、T052非运动步骤所有权、T053媒体/worker/融合、T054保存与查询投影可先实现并作标明来源的组件验证；实际产品动作派发另等008 T049所用目标和003 T070准入，Q01完整勾选仍等008 T055正式页面证据。产品检测命令依据协议§2.2寄存器表为2，不采用§3.1.7上料位举例的1；高度换算暂缓，缺映射拒绝动作。specs/003-plc-latest-protocol T069后端尾段守卫和006 T049控件也可先独立实现，正式页面点击仍是各自原验收条件。

## S1：US1首条Q01与US5结束链

目标：正式页面选择Q01→公共准备/F→AB定位、逐图分析、同面融合→真实保存→普通OK无搬运→下料/解锁→页面确认取盘→Final。所有参与组件实际调用。独立验收只看同run完整证据。

- [X] T052 [US1] 在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`及`backend/src/Gaode.Host/Composition/AdapterBindings.cs`实现线性冻结步骤的唯一应用入口并接正式Detection，消费Position/适用Z/捕获/保存/Reset等已支持步骤；Sorting/Unload明确移交外围，条件不适用须有依据，未知步骤受限不跳过。对应FR-001/002/004/014/018；依赖：specs/002-plc-xyz-recipes T11、T051、specs/001-station01-public-preparation T090及003 T070；纯执行分派及显式Test目标的单面组件接线可先按合同开发，但Q01实际运动仍须B04解除。意图先提交、反馈关联、期限和诊断贯通，必要检查顺序/错坐标无采集/Unknown无重发；证据`evidence/execution-chain.md`，不能由局部组件宣称Q通过。 USR-E增量：消费003 T070/T071可靠坐标/动作及T054回执，目标意图→实际反馈/校验→保存/复位后才后继；第二点独立目标，同值不借上一点反馈。T054写入子能力按冻结字段先交付，运行验收再与T053汇合，不形成T052→T053→T054→T052整项循环。沿execution-chain验缺轴/错来源/保存未决无依赖动作，PLC握手唯一003。

- [X] T053 [US1] 在`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`、`FileBackedCapture.cs`及`backend/src/Gaode.Application/Workflow/FaceResultAggregator.cs`接入配置光源/曝光/ROI、按对象/面/轮次/相机选择媒体，A整批后B（CD复用），每图独立worker分析/保存后复位，第二输入齐后另调用融合；前批不等融合、不混身份。对应FR-002/003/014、Q01/F3；依赖：T052、specs/007-station01-integrated-loop T032和T050适用媒体。交付：实际单图/双图调用与租约/计时记录；缺输入有限Pending、不伪造采集或融合，必要顺序/身份验证存`evidence/capture-fusion.md`。 时序按`specs/008-recipe-driven-inspection/sequences.md`§3：每图必要保存后本轮检测Z复位，A整批后B整批，同对象B已复位且双输入齐才融合；不新增全批融合等待屏障。 USR-E增量：由T050提供同值Y/变化Y独立第二点，依T052已核验定位及T054保存子能力才采集/复位；媒体/worker按对象/面/相机独立，不重复设备适配。原capture-fusion验单图/融合及第二输入顺序，正式AB/CD复用T055/T056/T059包，不扩全部配方故障矩阵。

- [X] T054 [US1] 在`backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs`、`StageEventStore.cs`及确需时`backend/tools/Gaode.StorePrep`保存步骤意图/反馈、媒体、单图/面/实体结果、真实位置/处置与版本引用；复用事件和短事务，只有必要列才维护迁移，Host不自动改表。对应FR-002/003/014/016、SC-003、F4；依赖：specs/002-plc-xyz-recipes T11及冻结身份/计划合同供基础保存；T053所用执行子能力只为后续保存集成验收前置，不阻基础事件写入；交付已提交事实供003 T068/T069查询，不做第二份API。验证真实SQLite/媒体读回、保存失败/CommitUnknown无后继及无Final，日志退出后可按requestId追溯；证据`evidence/persistence.md`。 USR-D增量：复用上述保存通道记录旧故障/媒体、reset/check逐项事实、旧执行关闭、新旧run关联及新轮独立媒体/结果；命令幂等/检查单次消费的条件事务基础唯一由001 T078交付，008 T068调用，不再建第二套状态库。必要保存失败/CommitUnknown不启动新轮，历史故障不可改成Final；保存子合同证据在原目录，正式恢复引用T069同包。 USR-E增量：按003 data-model/诊断合同持久保存TargetRequested/WriteAcknowledged/DeviceObserved/PositionValidated/AckCleared/Blocked及完整targetXYZ/observedXYZ、轴、采样阶段/时间、rawWords范围、epoch、容差/结论/协议配置摘要；缺口Unknown/Uncorrelated，目标不补实际。复用001 T078/TraceWriter/StageEventStore，必要结构走StorePrep，不建新库。基础事件写入按冻结合同先交付、不等T053全链，T053只为集成验收前置；003 T068随后接投影，不反等API。原persistence验退出后按run/action/entity/face/step可查及必要保存失败不续接/Final，不永久全量归档。
  - RES结果保存子交付（HMI-003/DAT-004、FR-016、P07/P08/P09）：按[结果字段合同](contracts/api-results.md#res结果展示增量2026-09-26目标尚未实现)与[data-model](data-model.md#res结果展示的持久事实边界2026-09-26)，先核对`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`的CaptureFact/AlgorithmFact/单图及面融合事件，复用`backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs`、`StageEventStore.cs`和现有AlgorithmCalls/媒体写入；只补合同要求的事实或关联缺口。不建结果库、不实现公开API，不要求算法增加当前没有的输出。
  - RES保存内容：实际runId/planRevision、对象kind/id/parent及localFace、冻结target/step/camera、captureId/callId/inputCallIds/mediaIds、算法能力/参数版本、单图/融合/对象已提交disposition、technicalState和真实ErrorCode/未判定原因；保留Required/Completed目标引用供查询核完整性。已有RequestedCaptureSettings及冻结算法参数保留真实值/单位/版本/引用，请求参数不写成SDK已应用或算法实测。只在实际输出存在且已定义语义时保存缺陷类型/位置/尺寸/媒体、置信度和对象计数/时间；无输出按合同保留NotProvided，不用配置、目标或演示值补结果，空缺陷集合不等于字段缺失。
  - RES提交与分层：质量disposition、事实source/quality、完整性依据和处置事实分开保存；不以算法Success或默认Complete补判。沿现有WriteId/必要短事务确认单图、面及汇总事实，允许已提交子结果先供查询，未提交对象汇总不得补造。保存失败/CommitUnknown保留原因及可核对引用，未确认事实不冒充已提交；必要保存门禁和USR-D历史留存继续原条件。仅事件payload增量优先复用既有版本化事件，确需表/列才由`backend/tools/Gaode.StorePrep`维护，Host不得静默迁移或回填历史。
  - RES依赖/验收：保存基础及上述关联子能力沿冻结计划/合同、002 T11适用配置和001 T078既有持久基础先交，不等待003 T068公开投影、006页面或T053完整任务；T053实际执行子能力仅用于后续写入/读回集成验证。在`backend/tests/Gaode.Integration.Tests/Storage/`及既有StageEventStore合同测试按必要代表核同run对象/面/单图与融合引用可持久读回，保存未决不公开/不推进依赖动作、缺字段不补造；证据追加原`evidence/persistence.md`的独立本次包，供003 T068投影与006 T049页面对账复用。子交付不勾T054父任务。


第四批子范围记录：T052—T054及003 T070的显式Test目标单面执行单元已按[组件证据](evidence/fourth-batch-validation.md)通过AB/CD和到位不符、必要保存失败、复位失败；这些组件输入没有进入正式Q01/Q02目录。原任务仍需合法目标/高度映射、正式公共移交及其各自剩余验收，以上勾选状态不变。

- [X] T055 [US1] 判定首条Q01正式前端完整运行，产物`specs/008-recipe-driven-inspection/evidence/q01.md`、`evidence/index.md`及`artifacts/recipe-execution-008/Q01/`。对应FR-001—004/011/013—016/018、SC-001/003/005、C06结束段、F1/F2/F4。依赖：T049对应输入、T050—T054、specs/003-plc-latest-protocol T065/T067/T068/T069/T070、specs/001-station01-public-preparation T090、specs/002-plc-xyz-recipes T11、specs/006-frontend-station01-console T048/T049、specs/007-station01-integrated-loop T031/T032/T033；specs/007-station01-integrated-loop T029适用当前心跳验证证据须核对，不等其历史缺失窗口补造。一次真实页面选用/启动与取盘点击，同run关联F/冻结配方、每图和融合、PLC复位/下料/解锁、SQLite/媒体及Final。针对改动至少验证F与所选不匹配不进产品、产品到位/复位失败及必要保存失败不Final，可复用同版本组件失败证据并注明范围。当前页面心跳Blocked须在当前构建解除，旧成功不抵扣；S0或局部集成通过不得勾本项。 USR-E增量：先007 T033核实际运行包/配置，再根据证据修问题1—4；依003 T062/T070/T069及T054所用子交付，正式Q01核F、AB第二点同值/变化Y、000B下料完整XYZ/轴/清零。旧Q仅复用未变事实，不抵USR-E新观测；原q01引用独立新包，不扩全故障。
  - RES验收增量：依006 T049真实结果绑定和003 T068/008 T054结果子交付，把本Q01正常OK作为最少OK页面对账代表；同run、当前对象、适用面/项目的质量区与API/持久结论一致，已提交结果不等Final，参数/媒体及缺失字段真实，刷新/重开后保持。复用本任务原正式选用/启动/取盘/Final及007 T033本次ResourcesResolved证据，引用006原008-first-route-ui结果子包，不另跑一条重复Q01。原协议/门禁验收不减，旧FinalPageDisplayed不能抵本增量。


暂不宣称真实分拣、多面、组、旋转或真机精度完成。US5的下料代码唯一归003 T069；008不另设重复实现任务。

## S2：US1单面扩展与US6配置变化

目标：页面选CD、多槽/非连续槽及NG/Pending配方，真实分流并完整结束；同程序版本配置变化影响实际行为。分拣输入缺失不阻塞独立CD采集准备。

- [X] T056 [US1] 在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`、`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`补CD、多槽/非连续槽批次，维护对象列表与物理槽分离；复用相同执行/融合入口。对应FR-002/003/004、Q02/C01；依赖：T055、T050对应配方/B04槽映射。交付A-all/B-all、C-all/D-all和空槽不采集的实际顺序/媒体身份，必要失败不串对象，证据`evidence/batch-order.md`。 USR-E增量：CD多槽同样消费003 T070完整XYZ/轴校验，T050独立同值Y/变化Y第二点；原batch-order留实际运动/读回/媒体/worker身份，依T052/T054子能力，不重复握手。Q02与T059共享完整run，不以AB或四面前缀抵CD终态。
  - RES结果消费增量：CD、多槽/非连续槽沿本任务原batch-order及T059适用同run包核对象/面/相机/项目身份；当前焦点切换不得混用其他槽、对象或旧run，普通Single逐面明细与API/持久引用一致，空槽不产生结果。复用006 T049绑定、003 T068及008 T054所用结果子能力，不重复实现投影；只有本次映射/执行实质差异需要的新证据才补，不因结果展示无差别重跑全部多槽组合。


- [X] T057 [US1] 在`backend/src/Gaode.Application/Workflow/SortingTargetAllocator.cs`、`RecipeSortingMapper.cs`、`ThreeStageWorkflowExecutor.cs`按实际实体/sourceSlot/protocolSlotIndex与目标区域/点/容量分配预留，可靠取放后提交占用/处置，替换Sequence充槽号和零坐标；对应FR-010、C06/F5/F6。依赖：T054、specs/003-plc-latest-protocol T071对应分拣能力、B02/B04/B08；混合NG+Pending按本次U02有NG优先并保留各面明细，单一结果可独立验证。必要验证：非连续源槽、NG/Pending分区、满位不派发、未知不释放不重发，产物`evidence/sorting.md`；PLC实现不在本任务重复。 按`specs/008-recipe-driven-inspection/sequences.md`§5区分预留、取料在途和放料/ACK完成后的占用提交，原图整体动作不能替代两段反馈。 USR-E增量：只消费003 T071的Modbus目标阶段观察及本次取料2/放料3+ACK，不自行发PLC或将状态2实时Z当源Z。缺采样/保存未决保留预留及在途，取料确认后才交放料、放料/ACK后占用提交。原sorting与T059共包，普通OK无需搬运不等待生产采样。

- [X] T058 [US6] 用同一程序构建从页面分别完整运行Q01基版和Q01-PARAM新版本（可引用T055基版），在无活动运行时装载新目录，不建设热加载。产物`fixtures/`版本数据、`evidence/config-change.md`及实际运行包；对应FR-017/018、SC-002、C01/C08。依赖：T056、specs/006-frontend-station01-console T049、specs/007-station01-integrated-loop T033及对应B04；必要证据：同程序摘要、实际槽位/采集光源/算法参数和执行调用差异，选择/F/冻结/保存一致，旧快照不变。更名或仅计划展开不通过。 USR-E增量：保留closure-new-scope-v3.trx Failed及源码尚未复验，先007 T033核同一实际程序，再用T050当前目录基版/PARAM从页面验证真实参数及结果变化；不能仅比配置或引用旧Passed。原config-change新包核XYZ/轴与旧快照不变。
  - RES参数回归增量：同一实际程序基版/PARAM包按006 T049对账真实RequestedCapture/AlgorithmConfig参数及版本、结果变化和当前对象身份；配置摘要不代替参数值，Test请求不宣称SDK应用，旧快照不变。核本次ResourcesResolved和WPF/Host/配置摘要，引用原config-change及006结果子证据，不另建参数任务。closure-new-scope-v3.trx Failed/未复验继续保留，页面展示绑定通过不能抵本任务真实参数/结果变化全条件。


- [X] T059 [US1] 从正式页面完整运行Q02-MULTI（合法时抵Q02）、Q01-NG与Q02-PENDING，采集/worker真实产生适用结论，下料定位→同盘实际分拣及ACK→解锁/页面取盘→Final。路径`evidence/q02-sorting.md`、`artifacts/recipe-execution-008/`。对应Q02/C01/C06、FR-003/010/015、SC-001/003/005、F3/F6；依赖T056/T057及同S1前端/采证链。算法超时可有限Pending，但机械未知不得借Pending结束；必要满位/缺结果失败与日志证据可复用同版本记录，混合决定按本次U02执行，未判定面明细不可丢失。 USR-E增量：依003 T071取放、003 T062诊断及008 T054保存子能力，新包证明取料2先于槽号/放料XYZ/命令2、放料3+Sorting_OK清零后提交。目标阶段Modbus XYZ与抬升后实时位置分列，诊断不能代反馈；必要失败验未取料成功无放料写包、未采到不假Final，原q02-sorting引用同包，不全Q穷举。
  - RES验收增量：以本任务既有合法NG/Pending路线承接最少两个质量代表，同run正式页面质量区分别等于后端/持久NG或Pending；检测/适用分拣完成至Final后质量仍为NG/Pending，不能变“完成”或OK。适用项目/原因/参数/已有缺陷及媒体按身份对账，缺失字段不造值；刷新、重开及既有对象切换不串结果，取盘和Final门禁保持。依006 T049、003 T068及008 T054结果子交付，结果对账与本次实际取放/盘末同包采证，引用原q02-sorting和006结果子包及ResourcesResolved；不再为T049跑重复整链，历史FinalPageDisplayed不抵本增量。


## S3：US2多面、E与当前适用代表

目标：页面运行两/四面路线，适用自动/人工换面后校验该面合法目标，不重采3D；每条选定适用路线有完整Final证据，允许位置变体不等于全排列实跑。新增E只有合同明确后才执行；不含E路线继续。

- [X] T060 [US2] 在`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs`、`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`及`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`取消本路线强制RescanWholeTray，正式Flip调用specs/003-plc-latest-protocol T071完整定位/双反馈/Flip_OK闭环后，在同一检测循环推进阶段与目标面并解析合法目标。保留初始3D/F/同盘对象/历史结果，已完成成员不重复翻面/检测，整体实体共享动作一次。对应FR-005/012、C02/当前适用多面Q/F2；实现前置为specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090及本功能T050/T051/T054所需数据/保存子交付，自动另specs/003-plc-latest-protocol T071已实现能力与B04合法配置；不等T055整项或全部分拣。人工按本次U04采用命令目标面并记录默认来源，保留占用禁动/确认清零，API唯一归specs/003-plc-latest-protocol T072。完成：新计划无翻后3D、真实面推进、不混测量/连接代次；最少验证Q03双面、同目标面连续实体、错面/缺目标/ACK失败无下一采集，存新版证据，旧faces-height.md只读引用。 逐实体与同一循环续接按`specs/008-recipe-driven-inspection/sequences.md`§2/4核验；图示不替代运行证据。 USR-E增量：四面只当前3＋1且少数组位置按配方，自动消费003 T071完整XYZ/Flip/ACK，不同实体同面分别闭环，逐面目标独立。原faces-height引用T062新代表包；人工命令默认/人工确认来源同run继续，故障归T068；不重采3D。

- [X] T061 [US2] 按本次U06明确的E合同在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`和`VirtualPlc/VirtualPlcEngine.cs`接E采集/解码/绑定/保存/复位；对应FR-006、C02。依赖T060、B01-E/B04/B06明确的轴/清零/异常及放行规则；本次用户已明确复用F式§3.1.7，需同步合同双方后实施；对象按场景表K19/Q19，E缺码记录问题继续，F门禁不变。交付正确内部对象关联外部码，必要解码失败有限结束并记录问题继续，机械/复位/保存失败仍受限，证据`evidence/e-scan.md`；不阻塞不含E路线。 USR-E增量：复用003 T070扫码轴/3/4和完整坐标证据，不重复实现PLC或VirtualPlc握手；唯一负责E适用对象、采集/解码/保存/有限缺码继续，机械未知/复位失败/保存未决不放行，e-scan关联设备子包。

- [X] T062 [US2] 在`specs/008-recipe-driven-inspection/evidence/q03-q22.md`、`evidence/index.md`及独立新包完成当前适用多面正式页面验证，历史文件名不代表22条门槛。一面两面要求保持，本任务覆盖Q03—Q06及起始四面Q09(AB→AB→CD→AB)/Q18(CD→AB→CD→CD)，证明两类3＋1、中间少数组、切换后返回；按实际配方/场景差异增补或复用，不要求22/14/8全部实跑。依T050当前目录、T051预算、T060、003 T070/T071及T054保存所用子交付、006页面和007 T033；人工另006 T050/003 T072-M，E另T061，局部缺输入不阻无E普通路线。沿已通过Q03按构建影响复用，不无差别重跑；每条选定路线实际页面选择/启动/取盘、同run初始一次3D/F、全部独立面目标/完整XYZ/实际Flip和ACK、采集/独立算法/融合、SQLite/媒体、先下料后适用处置或无需搬运依据、可靠解锁与Final。必要错面/ACK超时/缺目标不续检及同值Y/变化Y证据可共享新包，旧Q Final不抵USR-E，NotRun不Passed，历史编号/事实不改。对应FR-004/005/006/014/015、SC-001/003/005、C02/F2。
  - RES多面增量：沿本任务实际选定的一面/两面及Q09/Q18等3＋1代表，消费006 T049/003 T068/008 T054结果子能力，对账同对象各面/单图/融合身份及已提交质量；换面/媒体切换不把单图结论覆盖对象，不复用上一面参数或结果。结果随提交可展示，最终质量/完整性/处置/Final分离；本任务原选定新包附实际ResourcesResolved与API/持久引用。既有OK/NG/Pending共享绑定证据注明构建/字段未变可复用，不为本增量新增8/14/22条全跑门槛；人工/E独立场景保留。


> 历史批次（新版不继承执行限制）：第七批限Q03（不改变T060/T062整项条件）：自动换面是唯一候选；先做两面/两轮Test映射校验、预算与受限配置。协议§2.3/§3.1.5及总时序图已有目标面、双重反馈、人工区禁止运动与PLC内部整体翻面取放；只剩取放点提交字段、本轮触发及旧状态清零/关联未明，不能派发Flip或第二面产品运动，也不能勾T060/T062。人工003 T072/006 T050本批不实施。已通过的Q01/Q02/Q01-PARAM能力按具体证据复用，T058状态保持原样。

> 历史批次（新版不继承执行限制）：第七批实际进度：[Q03受限交付与F复核](evidence/seventh-batch-q03-readiness.md)已完成版本化Test配方、两面/两轮映射校验、冻结预算与无静默跳过验证；PrepareOnly成功且无业务启动。经用户指明来源后，已明确协议的目标面/完成状态/实际面号/人工占用及PLC内部整体闭环直接引用；剩余仅取放点提交字段和本轮触发/旧状态清零归属两项。T050/T051/T060为Q03子范围部分进度，T062未运行；specs/003-plc-latest-protocol T071未派发，人工003 T072/006 T050本批不做，specs/007-station01-integrated-loop T033无合法页面操作可扩。整项勾选不变。

> 历史批次（新版不继承执行限制）：第八批仅推进T060的Q03多面执行子范围和003 T071已定义子范围，直接共享002 T11、specs/001-station01-public-preparation T090及T050/T051按代码实际影响最小对齐。正式Flip仍因取放字段/顺序及本轮反馈关联未配置而受限。允许显式Test翻面后阶段上下文做第二轮3D和第二面AB组件验证，不能抵T060整项、T062正式页面或003 T071全项完成条件；原勾选保持。

> 历史批次（新版不继承执行限制）：当前第八批范围及检查点修正（2026-09-25）：用户扩大为008 T050/T051/T060/T062、specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T071自动、specs/006-frontend-station01-console T048/T049、specs/007-station01-integrated-loop T033的直接子范围。按[阶段证据](evidence/eighth-batch-auto-multiface.md)已交付Q03 Test数据/预算/精确受限原因、首轮移交与轮2解析、后半段真实3D/worker/存储/第二面AB组件、翻面反馈Host判别及媒体面轮查询/轮选；Q03正式WPF及Q04以后无E自动路线均NotRun。上段“本轮反馈关联未配置”是旧判断：正常触发已定义，Host关联属内部实现；真正外部缺口为取料和放料坐标的字段与提交顺序。此缺口阻断正式Flip及后继正式动作，不阻断已交付的独立代码。各任务整项条件未齐，原勾选保持；补齐接口并接003 T071实际Flip后，先Q03正式WPF到Final，再连续覆盖后续适用自动无E路线。

## S4：US3组与US4整体/旋转

目标：页面看成员/组、部位/整体结果及真实应搬单位；普通整体不依赖旋转输入，特殊路线三出口各有事实。未确认组策略/旋转信号仅阻塞相应分支。

- [X] T063 [US3] 在`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`、`RecipeSortingMapper.cs`实现多组成员1/2/4面→组结果及已确认应处置成员集合，保留所有成员明细和group关联；对应FR-007、C03/F6。依赖T054/T060；处置依T057；本次U01/U02已确定仅问题成员处置、NG送本盘NG区及NG优先留明细，禁止整组剔除OK成员/缺员OK。交付组完整性/判定/应搬集合及问题成员实际处置及混合结果明细证据`evidence/groups.md`，API复用003 T068。 USR-E增量：依002 T11/T050逐成员四面3＋1及来源面数，消费T052/T060动作与T057处置；型号F现有1/2面复用、型号A四面Test用合法Q09等，不改源表组成。groups及T066同包验NG优先、只搬问题成员、已完不重采。

- [X] T064 [US4] 在`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`、`RecipeExecutionCoordinator.cs`、`RecipeSortingMapper.cs`实现ordinaryAssembly部位结果汇总、共享姿态与整体一次处置；对应FR-008/010、C04。依赖T054/T060，实际搬运依T057及适用B02/B04/B08；不依赖B03或T065。交付部位只作检测身份、assembly作物理实体、无重复翻面/搬运，证据`evidence/assembly.md`，页面消费现有分层投影。 USR-E增量：002 T11/T050逐部位/对象核四面适用性；BASE两面/PIN一面不因裁剪失效，四面整体合法3＋1且共享动作一次。复用T052/T060/003适配，assembly与T066包验媒体独立/不拆抓。

- [X] T065 [US4] 在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`、`RecipeSortingMapper.cs`实现类型1零件/整体进站占用→批准姿态→相机检测→OK原槽/NG区/Pending区→可靠空闲的业务编排，排除盘末重复搬运；对应FR-008/009/010、C05/F5。依赖T057/T060，整体另T064；specs/003-plc-latest-protocol T071对应能力及B02/B03/B04/B08，混合结论另B06。按本次U03由下位机负责角度和机械过程，Host发请求并等虚拟设备结果；复用003现有Test接入合同，不以现场角度阻塞，不臆造生产地址或Host假成功；交付真实实体/原槽/姿态/三出口保存和未知占用受限，证据`evidence/rotation.md`。 USR-E增量：复用003 rotation-test-execution及T071现有Test请求/结果，角度归下位机，不重建旋转；适用四面对象核3＋1。特殊取放按自身合同，不将普通同盘采样强套未知生产字段；已明确定义Test继续，已处置不重复盘末，rotation/T067共证据。

- [X] T066 [US3] 从正式页面运行GROUP（至少两组；按已确认型号F/A分别覆盖成员1/2及1/4面，不虚构源表没有的组成）与普通ASSEMBLY部位1/2面完整路线，路径`evidence/group-assembly-runs.md`及运行包。对应C03/C04、Q01/Q04及合法四面代表适用完整对象序列、FR-007/008/015、SC-003/005/006；GROUP依T063/B05，ASSEMBLY依T064，各依应有搬运合同；两分支独立验收不互阻。必要验证缺成员不OK、NG优先且只搬问题成员及共享整体一次处置；不可把组结果当单成员搬完，C04不等旋转。 USR-E增量：GROUP四面Test采用Q09等合法3＋1并保留来源成员面数，ASSEMBLY适用四面逐对象校验/整体共享动作一次，现有1/2面可复用。依T050当前目录、003 T071适用搬运子能力；验目标阶段/抬升Z分离和动作保存。允许变体不全跑，group-assembly-runs历史保留新包独立。
  - RES分层页面增量：在原group-assembly-runs同包核Group/Member及Assembly/Part/Face/项目的id/parentId/localFace/结果引用；当前零件不能显示组汇总代替成员或部位，NG优先仍保留NG面与未判定面，组结果不意味着全部成员搬走。消费003 T068/008 T054事实及006 T049既有区域，参数/缺陷/媒体无串层，附实际加载资源。只补本任务原实质场景差异证据，共用质量绑定可引用最少代表包，不额外为每个组/整体重跑三态矩阵。


- [X] T067 [US4] 从正式页面完成ROT-PART及ROT-ASSEMBLY完整运行，实际覆盖OK回原位/NG/Pending三出口；合法共用出口证据须注明未变代码/协议及实体差异。路径`evidence/rotation-runs.md`及运行包；对应C05、FR-009/010/015、SC-003/005/006。依赖T065、specs/007-station01-integrated-loop T033、specs/006-frontend-station01-console T049与B03解除；必要失败含占用/姿态不符不采集、未知搬运不Final，记录真实实体及后续盘末无重复处置。 USR-E增量：适用四面仅3＋1，既有Test旋转不等待现场角度；生产采样/机构未知仅局部Blocked。三出口/整体差异及不重复搬运原条件保持，同包引用T071适用反馈及T054保存，不能用普通Q替代。
  - RES旋转页面增量：沿原rotation-runs适用实体/出口同包核真实质量与处置、Final分离，Assembly/Part和媒体身份准确，特殊已处置不重复搬运。复用006 T049结果绑定、003 T068/008 T054查询/保存及007 T033本次资源证据，已有共用绑定证据注明版本与范围可引用；不以普通Q代旋转出口，也不为显示增加旋转角度输入、新接口或额外全故障矩阵。


## S5：US5必要恢复与覆盖收口

目标：故障旧轮收束、双端真实初始核验后，正式页面显式启动完整新run或准确受限；正常暂停/人工继续独立。T069唯一验收C07/F5，T070汇总必要Q/C/F证据，不扩机械恢复矩阵。

- [X] T068 [US5] 在`backend/src/Gaode.Application/Station01/FixedMoveRecoveryInteraction.cs`、`Steps/FixedMoveStep.cs`、`StartPublicPreparation.cs`、`RunExecution.cs`及`backend/src/Gaode.Application/Motion/MotionCoordinator.cs`、`ResourceLease.cs`承接唯一故障恢复业务编排，复用既有Workflow与正常完整执行链，不新建恢复平台。依赖T054已交付保存子能力、001 T078持久基础、001 T052操作分类子交付和003 T072-A设备子交付；盘末复用003 T069已交付能力，不等待T072-B/006页面/本任务整链验证反向完成。 USR-E增量：恢复所用当前目录/动作先按USR-E必要代表验证，复用T054/003 T069，不等全部Q/C/F或特殊生产整项。新run/epoch只消费本次目标阶段观察，旧诊断不得改绑新完成；原recovery子证据及T069唯一页面主包。
  - 预期行为：停旧派发并实际收束旧采集/worker、控制等待者、阶段游标、临时绑定/面缓存和旧预算；未知物理资源仍保留，心跳/停止/复位通路可工作。旧故障、日志、图片和结果补存核实后仅关闭旧执行，不删除数据库、不造旧Final。消费T072-A实际双端复位/初始检查，只有实际释放及保存成立才结束旧Motion owner并让新run获取；不能沿用ReconcileVerifiedReset保留旧owner重发方案。
  - 新轮：复用POST /runs背后的应用入口，校验restartFrom和动态初始状态，经001 T078条件事务持久新requestId/commandId/runId、旧故障关联及检查消费后才派发；重新公共3D/F、唯一F绑定、完整必检面/处置、保存、解锁、页面取盘及Final。合法版本可再选，旧完成标记、扫码绑定、结果、反馈、预算与媒体不能复用；旧Call/action/epoch/generation晚到只归旧身份。旧同run/same-operation attempt=2故障重发不得保留为现行路径；正常幂等/算法有限重试不误删。
  - 操作分界：正常暂停同run继续归001 T052；人工换面仍按T060/003 T072-M/006 T050同run采用命令默认面、占用/确认清零且不重采3D；故障continue拒绝，不自动无限复位/重启。API唯一003 T072-B，页面唯一006 T051。
  - 验收/证据：在`backend/tests/Gaode.Integration.Tests/Station01/`复用旧故障注入能力做代表性初始拒绝、旧异步隔离、实际释放、关联保存门及启动幂等集成；旧SingleCommandRecovery场景原报告保留但不得抵扣新规则。业务子范围归`specs/008-recipe-driven-inspection/evidence/recovery.md`的后续增量，C07/F5页面主包唯一T069。命令、旧执行结束、资源移交、初始Blocked、新轮关联和失败均持久记录已知request/command/旧新run/reset/check/实体/面/步骤及原因；对应FR-012/014/016、SC-004、P04/P06/P07/P08/P09，不扩机械恢复矩阵。

- [X] T069 [US5] 在`specs/008-recipe-driven-inspection/evidence/failures.md`及独立`artifacts/recipe-execution-008/restart-d-{batchId}/`承接新恢复C07/F5正式页面端到端的唯一主验收；复用`scripts/wait-008-page-batch.ps1`、`scripts/verify-q01-q02-test-page.ps1`及现有真实Host/VirtualPlc/相机/独立worker/SQLite/媒体采证链，必要时按新语义最小适配。依赖T068、003 T072-A/B、001 T052/T078及006 T051（复用T048/T049）；人工对照消费T060/003 T072-M/006 T050已交付能力。001 T054、003 T072及006 T051引用本包，不要求它们先取得相同页面整链证据。 USR-E增量：先核所选普通合法Test的USR-E协议/目录代表证据，再正式页面复位→初始核验→显式新启动→新run完整Final；普通OK不等待无关生产取放窗口。旧Q/旧恢复Passed不抵，原初始拒绝/异步隔离/保存幂等/三操作对照保留，restart-d唯一新包。
  - RES恢复验收增量：依006 T049已交付结果绑定及003 T068/008 T054结果子能力，仍由本任务唯一C07/F5新恢复正式页面主包核新run质量区/适用项目/原因/媒体与本次API及持久事实一致；旧故障/旧对象/旧异步结果不能污染新焦点，旧证据仍可查。刷新/重开、显式切新run后身份一致，Final不覆盖质量；复用本批最少OK/NG/Pending绑定证据，不要求恢复再跑三态×全故障。restart-d新包附本次ResourcesResolved/WPF/Host/配置摘要，旧恢复Passed/旧FinalPageDisplayed不能抵此增量，恢复链及原必要门禁不减。

  - 最少验收：正式WPF实际选用并启动产生故障旧run，页面实际复位、核验、显式新启动，最后页面取盘确认；新request/command/run重新公共3D/F与唯一绑定、全部必检面/处置、必要保存、可靠解锁到持久Final。初始不足（包含Ready为真但占用/XYZ等必要项不成立的代表）不得新PC_Start；旧故障/媒体可查询且旧新run双向关联。注入旧异步结果不污染新轮，必要关联保存失败/CommitUnknown无新动作，重复新启动只一个新run；暂停/人工继续同run无额外公共3D，故障continue拒绝。
  - 证据：构建/配方/配置摘要、页面操作、request/command/旧新run/reset/check关联、真实设备/worker轨迹、SQLite/媒体读回和Final；持久结构化日志定位命令受理、复位、核验、阻断、旧执行结束、新轮关联及失败，不能只截图或后台代确认。旧Q Final、旧单指令恢复Passed和旧build均不能抵新恢复Passed；失败包保留并有限定向复测。
  - 保留原F1—F6其余义务：非法配置/F错绑定、轴/面/ACK、worker无结果、必要保存、缺身份/目标等仅按本次改动影响复用或补验；NG优先留明细按现行规则，不新建容量矩阵，不做22×全部故障穷举。未执行/未复验如实NotRun/Failed，参数回归失败仍由T058承接并由T070汇总。对应FR-001/002/005—007/010—016/018、SC-004/006。

- [X] T070 汇总`specs/008-recipe-driven-inspection/evidence/index.md`、`coverage-matrix.md`及`evidence/completion-review.md`的当前SC-001选定路线及C01—C08实质差异、F1—F6和SC-001—006；依赖T055/T058/T059/T062/T066/T067/T069适用全部证据。逐项可追溯页面选用→F绑定→冻结版本→实际调用/保存→适用处置/下料/取盘/Final，列来源/构建摘要/仍Blocked或NotRun；同程序变化证据不可缺。旧analysis.md保持历史；本轮用户已授权保存新的静态核查记录，analyze阶段只读，记录在独立文档工作阶段保存。交付范围结论不宣称真机、精度或全项目完成，不把本轮文档检查算业务测试。 USR-D收口须分别列普通Q原构建Passed、C07/F5新恢复及生产验收状态；引用T069唯一新恢复包，核对双端初始/新旧run/完整新轮/旧证据可查，任何旧单指令Passed不得抵扣。保留`closure-new-scope-v3.trx`参数回归Failed及源码修订尚未复验事实，由T058按同程序基版/PARAM证据关闭；任务全条件未满足不勾，质量清单仍15/16，不扩历史清理。 USR-E增量：分列允许变体、实际采用配方、必要验证理由及Q09/Q18起始代表；退出Q历史编号/构建/状态保留，问题1—5按实际包单独结论。只汇总适用证据、不反向阻塞实现；新恢复T069、PARAM T058、生产局部限制独立，子能力不得勾父任务。
  - RES收口增量：原index/coverage-matrix/completion-review分列“流程Final”与“真实结果展示”状态，汇总006 T049及008 T055/T059最少OK/NG/Pending对账、其他选定路线实质差异引用和T069唯一新恢复包。每条关联实际加载资源/构建/配置、run/对象/面/项目、API版本/结果与持久引用、页面质量/明细及未提供字段；旧Final证据只记原流程范围，不将其升级为RES展示Passed。子交付、未验收/Failed/Blocked、参数Failed未复验及质量15/16分别保留，不新增验收任务或把008整体宣布完成。


## 阶段依赖、首批范围与停止点

当前新版执行口径（替代旧“Q01后停止/不进多面/等待翻面协议”）：

1. specs/003-plc-latest-protocol T071的协议身份/自动Flip、T069下料及结束顺序先交付；T067/T070复用既有F/检测并核验受影响轴与版本。
2. specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090及本功能T050/T051/T052/T054/T060按合法逐面目标、初始测量、数据与预算同步；目录能力齐才Available。
3. specs/003-plc-latest-protocol T068、specs/006-frontend-station01-console T048/T049、specs/007-station01-integrated-loop T031新版子范围/T033交付查询、页面绑定及新版夹具采证。
4. 本功能T062先Q03普通OK正式页面到Final；核对同run仅初始一次3D、真实Flip/两面AB/融合/保存、下料/解锁/页面确认。相关Q01/Q02/PARAM必要回归由T055/T056/T058保留承接。Q03未通过时停在具体失败，不跳过门禁；通过后继续其余适用无E自动两/四面。
5. T057/T059的NG/Pending、T061的E、T063—T069组/整体/旋转/人工/恢复按实际依赖衔接，全部最终由T070收口；它们不作为普通OK Q03的全局等待。

实现前置只需对应能力/数据已交付及必要验证，整项验收仍覆盖原范围及新版增量；不能以父任务未全勾制造虚假阻塞，也不能省掉安全、目标及保存门禁。详细文件/调用关系与停止点见[实施清单](implementation-checklist-20260926.md)。本轮文档工作完成tasks及其静态核验即停止，不执行speckit-analyze，不运行上述步骤。

没有标[P]：当前任务包含共享文件/状态或未完成前置。条件满足后可并行的示例：S0的目录002 T11与环境007 T031；S1阶段worker007 T032完成后前端与后端不同文件可分工；US2输入核对与已定义CD数据准备；US3/US4共享Aggregator文件须顺序编辑；US6配置运行与其他业务运行共用设备，必须串行；US5失败采证与业务运行也串行。此说明不授权多代理或服务并行。

## 需求、阶段、任务及验证追溯

| 需求/成功条件 | 实现与验证（唯一所有者引用） | 用例/阶段 |
| --- | --- | --- |
| FR-001 | specs/002-plc-xyz-recipes T11、specs/003-plc-latest-protocol T067/T068、specs/001-station01-public-preparation T090；specs/008-recipe-driven-inspection T055/T069 | Q全体，F1；S0/S1 |
| FR-002 | specs/003-plc-latest-protocol T070；specs/008-recipe-driven-inspection T052—T055/T069 | Q全体，F2/F4；S1起 |
| FR-003 | specs/007-station01-integrated-loop T032；specs/008-recipe-driven-inspection T053/T056/T059 | Q01/Q02及后续，F3；S1/S2 |
| FR-004 | specs/002-plc-xyz-recipes T11；specs/008-recipe-driven-inspection T050/T052/T062 | 当前适用Q；S0—S3 |
| FR-005 | specs/003-plc-latest-protocol T071/T072、specs/006-frontend-station01-console T050；specs/008-recipe-driven-inspection T060/T062 | 当前适用多面Q/C02/F2；S3 |
| FR-006 | specs/003-plc-latest-protocol T067、specs/001-station01-public-preparation T090；specs/008-recipe-driven-inspection T061/T062 | 公共F全Q、C02的E；S1/S3 |
| FR-007 | specs/008-recipe-driven-inspection T063/T066/T069 | C03/F6；S4 |
| FR-008 | specs/008-recipe-driven-inspection T064—T067 | C04/C05；S4 |
| FR-009 | specs/003-plc-latest-protocol T071；specs/008-recipe-driven-inspection T065/T067 | C05/F2/F5；S4 |
| FR-010 | specs/003-plc-latest-protocol T071；specs/008-recipe-driven-inspection T057/T059/T064—T067 | C06/C03—05/F5/F6；S2/S4 |
| FR-011 | specs/003-plc-latest-protocol T069、specs/006-frontend-station01-console T049；specs/008-recipe-driven-inspection T055及后续全部完整运行 | C06/Q全体/F4/F6；S1起 |
| FR-012 | specs/003-plc-latest-protocol T072、specs/006-frontend-station01-console T050/T051；specs/008-recipe-driven-inspection T060/T068/T069 | C02/C07/F5；S3/S5 |
| FR-013 | specs/008-recipe-driven-inspection T051/T055/T059/T062 | 全Q调用量/实际耗时；S0起 |
| FR-014 | 各实现内日志；specs/008-recipe-driven-inspection T054/T069 | F1—F6；随阶段 |
| FR-015 | specs/007-station01-integrated-loop T033；specs/008-recipe-driven-inspection T055/T058/T059/T062/T066/T067/T069/T070 | 当前适用Q/C8/F必要集；S1—S5 |
| FR-016 | specs/003-plc-latest-protocol T068/T072、specs/006-frontend-station01-console T048—T051；上述完整运行 | 全Q及C02/C07；S0起 |
| FR-017 | specs/002-plc-xyz-recipes T11；specs/008-recipe-driven-inspection T050/T058 | C08/Q01-PARAM；S2 |
| FR-018 | specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T068；specs/008-recipe-driven-inspection T049—T051/T058/T069 | 全Q/F1；S0起 |
| SC-001 | specs/008-recipe-driven-inspection T055/T059/T062/T070 | 当前一面两面及按差异选定四面3＋1代表完整证据 |
| SC-002 | specs/008-recipe-driven-inspection T058/T070 | 同程序新/变配方实际执行变化 |
| SC-003 | specs/008-recipe-driven-inspection T054及所有运行/T070 | 真实PLC/采集/worker/DB/媒体 |
| SC-004 | specs/008-recipe-driven-inspection T069/T070及阶段失败证据 | F1—F6，必要子项不能漏 |
| SC-005 | specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T068、specs/006-frontend-station01-console T048—T051；specs/008-recipe-driven-inspection T055/T070 | 选择/绑定/冻结/保存一致，人工确认 |
| SC-006 | specs/008-recipe-driven-inspection T058/T059/T062/T066/T067/T069/T070 | C01—C08 |

逐Q/C/F与M来源任务映射补入coverage-matrix.md。必要成功与失败记录均依contracts/evidence.md；没有业务测试产生时保持NotRun。
# 第五批执行说明（2026-09-25历史，不作为新版执行顺序）

按[虚拟 Test 映射合同](contracts/test-virtual-mapping.md)先补Q01/Q02版本化位置/高度绑定、目录准入与本轮3D解析；再复用第四批单面执行单元，完成003 T069、specs/006-frontend-station01-console T048/T049、specs/007-station01-integrated-loop T033所需的正式页面结束链。Q01由本功能T055按原完整条件判定；Q02的多槽CD及完整页面证据归T056与后续对应Q证据，组件通过不得提前勾选。specs/003-plc-latest-protocol T070的Test产品动作可依本合同验证，真机目标仍受限。历史任务状态不因本说明变更。

第五批实跑记录（2026-09-25）：[Q01/Q02同run证据](evidence/fifth-batch-q01-q02.md)证明两条正式WPF Test虚拟正常路线均到Final；当前构建定向规则/失败测试15/15通过。原T049/T050/T051/T052/T053/T054/T055/T056仍各有全范围交付、前置或完整失败/业务差异验收未齐，保持未勾；正常路线Passed不等于这些任务全部完成。specs/003-plc-latest-protocol T065的延迟机制前后对照、specs/001-station01-public-preparation T090的F不匹配/未知提交、specs/006-frontend-station01-console T048/T049的权限及结果/媒体全部验收、specs/008-recipe-driven-inspection T055同版本完整失败范围继续单列。specs/007-station01-integrated-loop T031的Fixture交付已齐并按本次证据勾选；T033工具虽用于两条运行，因006全项前置未齐仍未勾。后续不重复开发本批已验证的单面执行单元。
第六批收口记录（2026-09-25）：[逐任务原条件审计](evidence/sixth-batch-task-audit.md)、[Q01-PARAM配置变化正式WPF证据](evidence/config-change.md)与[必要门禁](evidence/sixth-batch-gates.md)已登记。Q01-PARAM在相同程序构建从页面至Final，C08 Test虚拟Passed；F期望不匹配及关键媒体保存失败定向验证通过，旧共享Final集成测试因其F配置超时失败并保留TRX。T051—T056/T058及直接共享任务均有未齐的原交付、验证或依赖，保持原勾选状态；已通过的Q01/Q02/C08子项不反向宣称整项完成。T060/T062首条Q03的共同、自动/人工分支前置和具体缺失输入见审计。


## 2026-09-26各任务可追踪增量（全部未实施）

本表补足原任务的新版输入、文件、前置、完成及最少验证；不另建实现归属、不改变原勾选。所有任务共同输入为分区协议SHA、spec FR与execution/3.0；必要日志含request/run/实体/面/step/operation/连接代次、目标/实际、意图/派发/反馈/ACK/保存/阻断，持久可查。

| 原任务 | 本轮增量及具体消费者 | 实现前置/完成条件/最少验证 |
| --- | --- | --- |
| T049 | `contracts/execution.md` B与需求OPEN/D按已定义待实现、数值待配置、真机待确认、业务待决定分类 | 读取协议与实际配置；关闭普通翻面/分拣外部协议阻塞，保留E/旋转/组/混合/标定局部限制；每项有来源及所限动作 |
| T050 | `fixtures/recipes*.json`、`generate-q01-q02-test-mapping.py`、`generate-q03-test.py`、`generate-q01-param-test.py`和cases/manifest升级目标0.5 | 前置002 T11目标schema；每面独立配置和初始sample映射、翻面XYZ、协议SHA齐；生成器与产物同批，缺面/错来源拒绝；不执行旧生成器覆盖新版 |
| T051 | `RecipeWorkload.cs`、`RecipeExecutionBudget.cs`删翻后HeightRescanCount贡献，补翻面定位/机械/ACK及分拣取放/ACK | T050实际动作清单；冻结Detection→Unload→Sorting绝对期限，公共3D/F只计初始一次，Q03/四面计数核验；不改3秒心跳 |
| T052 | `RecipeExecutionCoordinator.cs`、`StagePortContracts.cs`中的DetectionStepTarget消费新身份 | 前置001 T090/002 T11；阶段、面、真实测量、目标版本和连接代次分开；错对象/面/样本/配置无运动；复用已实现AB/CD定位单元 |
| T053 | `IntegratedDetectionPort.cs`、`FaceResultAggregator.cs`和采集输入身份 | T052目标；同对象/阶段/面/相机对融合，合法共用初始测量不导致跨面混图；复用007 T032双输入worker，验证两面输入各归其面 |
| T054 | `TraceWriter.cs`、`StageEventStore.cs`、`RunMediaCatalog.cs` | T052身份与协议契约；新动作/ACK/测量来源/配置版本及媒体可读回，不生成假第二次3D；真实保存失败不得续接/Final |
| T055/T056/T058 | 原Q01/Q02/PARAM正式入口与`fixtures`、当前索引 | 新版共享下料/身份已交付后做受影响正常链及必要失败回归；旧Passed保留；PARAM仍需同构建真实参数变化，不以旧证据直接判新协议Passed |
| T057/T059 | `SortingTargetAllocator.cs`（如需新建）、`RecipeSortingMapper.cs`、`ThreeStageWorkflowExecutor.cs` | specs/003-plc-latest-protocol T071取放能力及真实本盘源/目标；预留→取料在途→放料/ACK完成后占用提交；Sequence≠物理源槽例、状态2无整项完成、失败/满位不假完成；NG/Pending完整页面Final |
| T060/T062 | 同一Detection循环、正式Q03及其余适用两/四面 | 先新版Q03到Final，无二次3D；之后按当前适用序列/必要代表完整运行，不以组件或四面前缀抵两面；失败不做22倍穷举 |
| T061 | E独立动作与解码/保存合同 | 仅B01-E/B06明确后接；F3/4不外推E；不阻断无E普通路线 |
| T063/T064/T066 | `IntegratedDetectionPort.cs/FaceResultAggregator.cs/QueryEndpoints.cs`、共享实体动作/结果及页面证据 | 结果层级复用；已完成员不重翻、整体共享动作一次；实际组处置仅依B05/B06适用决定，不猜整组剔除 |
| T065/T067 | 旋转进出站/姿态/三出口及避免盘末重复处置 | B03及适用特殊取放已定义后实现；新版普通同盘协议不自动解决旋转，占用未知不释放；保留三出口必要验证 |
| T068/T069 | 恢复事实与必要F失败、人工确认接口消费者 | §3.1.5⑧已定义部分直接接，人工默认命令面与故障完整新轮均已确认；仅生产未知持件/初始反馈局部受限；未知不盲重发、保存失败/错面/ACK超时等日志可定位 |
| T070 | `evidence/index.md`、`coverage-matrix.md`及未来completion-review | 全部适用Q/C/F新版证据收口；普通Q原构建证据与USR-D新恢复进度分列；参数回归失败/未复验保留，文档检查不计实现通过 |

## 本次业务确认交接（2026-09-26）

来源[business-decisions-20260926.md](business-decisions-20260926.md)。上述T057/T059/T060/T061/T063/T065/T066/T068/T069只更新未完成任务正文，编号和勾选不变。

- T050：S2成员/面数及E需要按源Excel逐格追溯，真实每面相机/初始姿态/坐标高度/容量留待现场；现有合法Test配置可持续扩展。
- T054及003 T068、006 T049：E缺码、人工默认面、故障重发和现场待办均可按run/组/成员/面/步骤查询，复用已有日志/事件/API/页面，不建新平台。
- T061前同步003扫码合同；T060/T068前同步003 T072和006 T050/T051；T065前同步003旋转Test命令/结果合同。先各自spec/contracts/plan/tasks，再共享代码。
- 当前先跑具备条件的OK主链，不因现场参数和区域个数中断；不取消已有NG/Pending/必要失败证据，新增特殊处置只做当前所需最少验证。任务完成仍按实际全项证据判定。

## USR-20260926-D任务执行边界

本次仅增量任务对齐，来源为008 [计划交接](../008-recipe-driven-inspection/plan-restart-alignment-20260926.md)及[任务对齐记录](../008-recipe-driven-inspection/tasks-restart-alignment-20260926.md)。仅本轮修改的未完成任务承接新规则；已有已勾任务和历史证据保持原适用时期，不可抵扣新恢复。普通幂等、未触发故障的合法有限重试、正常暂停和人工换面继续不得误删。

执行按子交付：003 T072-A设备观察/复位隔离与001 T078持久基础可分别准备（共享文件修改须协调）；001 T052→008 T068业务→003 T072-B API→006 T051既有页面→008 T069唯一C07/F5页面包→008 T070汇总。T072-M人工与上述A/B独立；不等待无关父任务全勾。001 T070只做普通控制路由，不另建故障API。设备/worker/页面实跑串行；非阻塞边界登记，不增加全配方×全故障矩阵。详细子交付输入、证据和局部限制见任务对齐记录。


## USR-E当前依赖与完成口径（2026-09-26）

依据宪章7.0.0，完整归属/验收见[本轮任务交接](tasks-six-issues-alignment-20260926.md)。两端协议/诊断子能力＋当前配方准入/目录→必要代表性协议及正式路线验证→USR-D完整新轮。003 T072-A＋001 T078→001 T052→008 T068（复用008 T054及003 T069）→003 T072-B→006 T051→008 T069→008 T070；A/B/M分子交付，003 T069不反向等008 T068，不等全部Q/C/F或特殊生产。共享源码按文件串行交接，设备/worker/页面串行采证；子交付不勾父任务。问题1—5根因待实际包核验，状态2/3实时Z不强制等于取放目标Z；生产采样窗口只局部限制。历史勾选/正文不改，旧Q/旧恢复Passed不抵新验收。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

原T054负责真实载荷/查询，T057负责处置提交，T059/T066/T067代表读回，T070对账；保留编号和勾选，继续原任务，不增新父任务。

执行调整：共享PLC通信阻断时原批队列暂停，无活动验收进程；已实际自动接班到worker9936。继续上述既有任务的处置投影源码及必要纯投影验证，独立构建输出不覆盖原冻结程序/fixture；正式复验仍按实际构建和新job/run判定。此调整不改业务或验收条件，不勾父任务。

T050/057/059/070继续原非连续源槽剩余条件：新生成器`generate-q02-pending-p03-test.py`采用Q02同一合法P01/P03/P15映射、独立1.1.3-test-p03版本和真实worker P03 Pending目标，采证白名单支持Q02-PENDING-P03；新正式run核物理槽3≠动作序号1和处置投影。T054/069仅给已有诊断窗口增加本进程CPU/启动时间上下文，定位实际启动通信延迟，不加业务重试或放宽期限。

T050/051/054/070工具资源接线继续原任务：wait/verify/get-budget传递显式本地Test HostDll/PlcDll和对应预算BinaryRoot，记录实际加载摘要；禁止worker混合构建，已有reload先就绪再接班。不覆盖正在使用的默认程序/fixture，不新建调度平台。

T050/T057/T059/T070：P03代表1.1.3实际SortingTargetUnconfigured，保留失败；独立1.1.4补齐P03自身源点到已确认P15 Test目的点绑定后新job复验。此次配置修正不勾原任务、不修改旧fixture/旧run，不增加运动或新业务需求。

T054/T069继续无人值守主流程：job002修正fixture后在Host启动Connect超1秒，无WPF。Test启动依赖顺序先等待所属PLC健康，再启动Host，限定一次新构建短预检；不放宽通信期限，不把预检成功当业务验收或全局通信根因已修复。

T054/T069既有诊断进一步定位job015：新增Connect完成及Write/ReadHeader/ReadBody进入tick，失败时记录；核对PLC实际响应与Host await前后，不能据CPU/GC猜根因。仅独立构建和单次实际诊断，不改业务期限/协议或增加盲重试。

T054/T069原通信诊断下一步：r7指定job001 Test短预检，仅所属Host/PLC有限20秒EventPipe采样；解析与原交易窗口关联，若trace不完整如实登记。不新增系统权限/全局设置，不采其他账号进程，不据带采样负载的短预检标正式Passed。

T054/T069原日志及通信定位：修9417条查询框架重复Info未受控，保留Gaode关键结构化日志；VirtualPlc现有有界窗口补业务响应和首部读取开始tick，用于实际business超期两端对照。来源已有真实失败，不新增全量永久日志平台、不改变协议/期限，不据日志调整宣称通信根因修复。

T054/T069中断接续启动子交付：将Station01HostedService现有真实持久化初始化前置到Program的设备服务启动之前，一次初始化结果由hosted StartAsync复用。保留数据库恢复、未知不动作及原Stop次序；生命周期必要回归及独立正式WPF代表验证。按008-startup-persistence-order评估推进，不追加任务、不修改原勾选，不改共享接口或业务期限。

2026-09-27 T054/T069启动子范围继续：r13 Q06失败交易发生在VirtualWorkerHostedService与HTTP完成启动之前，已启动PLC心跳与这些冷启动步骤重叠。PlcConnectionHostedService仍负责真实连接及释放，改为等待IHostApplicationLifetime.ApplicationStarted后连接；HTTP监听只表示接口可达，既有PLC连接/安全/worker就绪准入不改，未连接不得动作。失败沿BackgroundService停止Host并保留原设备超期，不放宽期限或新增重试。日志记录等待、真实设备启动及连接完成；既有停止顺序及必要生命周期/正式代表复验。

## 2026-09-27 Test Host I/O运行设置（原FR-013/014，T054/T069/T070）

r16在全部构建/测试结束后，以同r15普通构建/原PLC/default server GC/原配方及全部期限，仅所属Host DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，Q06同rund4fc1789-b50b-487f-953c-9acbb14410eb完整Final及全部适用审计通过。单代表不证明全局根因，继续剩余主流程。

采证脚本增加显式HostSocketInlineCompletions Test开关：queue job JSON字段`hostSocketInlineCompletions: true`，wait worker只传递verify CLI `-HostSocketInlineCompletions`，verify传同名开关至start。start必须已有合法purpose=Test fixture，否则拒绝；只以Start-Process -Environment设置该次自身Host，PLC/父进程/系统环境不改变。默认不开启，record.configuration.hostSocketInlineCompletions记实际模式，实际DLL/GC/期限照旧登记。替换仅匹配临时request的诊断接线，不读取遗留request，不新建平台或扩大业务API/页面。

这是当前虚拟Test运行配置，不宣称生产默认或真实PLC已经验收；原默认模式的失败与根因未完全确诊事实保留。没有改算法、配方、设备协议、采集/保存或任何期限，没有新增重试、假完成或跳过。剩余代表沿明确记录的Test设置验证，若失败仍保留并暂停。

T054/T070证据元数据修正：Station01Registration已按public purpose=Test使用50ms轮询，当前及历史实际PLC starting日志同为50；start脚本process.json却写25。仅将之后作业记录改成实际50，设备/期限/配方/预算未改，不重跑业务；原process.json保持，读回以实际日志为准。r17当前GROUP已加载旧脚本、后继使用元数据修正版，source-freeze-amendment-poll-metadata.json记录两份摘要，不把原冻结源码重写为新版本。

## 2026-09-27 虚拟PLC I/O有限比较（T054/T069/T070）

r17 GROUP-A-E/run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，实际XYZ已匹配。Host20:15:49.799033Z写出tx9478，PLC20:15:54.5024904Z才读头、处理0.0133ms；当时PLC累计GC37.593ms，不据此认定GC根因。正式失败保留，不计通过，后继暂停。

增加默认关闭的PlcSocketInlineCompletions开关及queue字段plcSocketInlineCompletions。沿wait→verify→start传递，必须合法Test fixture；仅所属VirtualPlc Start-Process -Environment设置DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，process.configuration.plcSocketInlineCompletions登记实际启用。Host开关原含义保持，父进程/系统环境不修改，不改变DLL、GC、协议、期限或成功条件。先对同成组路线作一次独立有限比较，真实页面、数据库、设备及算法全链仍必须完成。

采证器对非预期恢复场景应识别现有页面阶段的“阻断”状态并保存StoppedOrUnknown，避免只识别中文fault而等待全预算。恢复注入仍沿既有waitingRecovery条件操作，不把阻断视为Final。

## r18恢复采证时序缺陷（T069）

job002旧run f86b2a63-030b-4792-ad8b-2986f6ada2c8、新run8aaf32b5-c77f-4342-a4a5-d2089db03bec已真正完成双端复位/初始核验/显式新轮及Final，exit1/cleanup=true。23项读回中仅old_actual_media_visible_through_fault_and_reset=false：beforeFault三维图片已在API Ready，却尚未画到页面（页面Idle）；atFault/afterReset实际显示true，旧SQLite/文件/API摘要一致。原包保留Failed，不能以新Final抵整项。

采证修复仅在既有oldMediaProof内有限等待同旧run实际Ready三维媒体的img data-media-id且complete/naturalWidth>0，再保存截图及注入F故障；不改页面、算法、PLC或业务期限。不写DOM/不伪造displayed。后续新作业重新验证全条件，不覆写本包。

## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

## 2026-09-27 复位观察同步必要修复（原003 T072-A、008 T068/T069/T070）

必要顺序用例在Reset 202后立即Check实际返回RecoveryResetNotObserved，两次均未到链接保存注入，原r19 TRX保留。复位直接Modbus Ready已成立但缓存PlcReady仍旧false；原MotionCoordinator必要门禁不放宽。仅ResetAsync在原轮询/原期限内同时等待缓存实际PlcReady、Connected/SafetyClear，使用同一次observed快照；不改变接口、信号、初始判据或生产机械未知边界。评估见.specify/bugs/008-reset-ready-observation/assessment.md。源码当前尚未修改，待当前测试结束；现有两个保存门禁及正式旧图/完整新轮独立新包复验，已有任务承接不追加重复任务。

## 2026-09-27 T065机制修复范围（待本轮验证）

同DLL受控观察已证明Portable批队列派发延迟：Host业务/心跳txn3分别入队后1112.2005/1056.6997ms，入队仅0.006/0.0072ms，真实1秒超期且锁定；独立Native候选2937个非零操作唯一回调，1560个Host响应头最慢20.2574ms。证据入口：`.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/`，旧构建和报告保留。

最小接线为Windows、purpose=Test冻结fixture显式WindowsNativeThreadPool开关，仅本次所属Host/VirtualPlc子进程DOTNET_ThreadPool_UseWindowsThreadPool=1、inline=0；普通启动及旧冻结构建不被静默改写。三个最低线程预留位置依微软支持的实际运行配置区分Native/Portable，Native不调用不支持的SetMinThreads、不虚报预留8。正式构建不含Harmony、socket反射或诊断事件。业务API、信号、1秒I/O、3秒心跳、50ms轮询、GC、优先级、失败锁动作及未知结果不重发条件不变。

本增量沿003 T065和008 T055/T070原任务，追加任务0、勾选不变。只验证该机制路径、原期限真实超期锁动作及当前正式Q01同run前端/配方/PLC/相机算法/SQLite媒体/Final；复用未改分支历史证据。r22 HTTP独立保留，真实设备/标定仍待现场，不增加全运行时证明门槛。只有本轮验证完成后才更新验收状态。

## 2026-09-27T05:09Z 本轮验证完成状态

前述实施前待验证状态由本节接续：003 T065原Test/VirtualPlc机制/对照/安全/日志条件，以及008 T055当前正式Q01和T070适用Test对账均已满足，仅这三项授权勾选更新。新构建显式WindowsNativeThreadPool/inline0，旧默认与冻结程序不改；r22 HTTP、缺失历史日志及真实设备/标定不扩大结论。新证据目录为.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z，verification-proof.json与task-checkbox-changes.json可核对；本轮新增任务0、其他勾选不变。

