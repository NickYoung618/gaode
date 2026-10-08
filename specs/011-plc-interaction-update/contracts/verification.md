# 011/012 最小联合验证合同

## 2026-10-08有效增量：020阶段A定向验证

新清零规则按[020验证指南](../../020-real-device-commissioning/quickstart.md)V01–V10验证，当前NotRun。覆盖连续两轮、延迟/不清零、过期/断线、清零后同坐标及失效、中间不清父请求、取料提交/最终安全位、完成身份与复用时本次复核身份、各入口无旁路。正式和工具分别留证；旧集合、版本化报告及局部Blocked保留原事实，不将旧通过当新机制通过，不要求不受影响的全部历史验证重跑。

版本：011-verification/1.1（仅保存边界与联合映射定向修订）。日期：2026-10-03。设计交付时软件项均NotRun，该状态仅属设计历史；当前实现/运行结论及来源、局部Blocked见[验证记录](../verification-report.md)和[集合映射](../implementation-verification-map.md)。复用009/010检查器、代表链及证据设施，不重建治理体系或要求历史全重验。

## 必需集合

| 编号 / 对应规格 | 最小正常/失败义务 | 复用实际位置与定向变化 | 当次证据 |
| --- | --- | --- | --- |
| M01 / V01 | 受影响Host、VirtualPlc及必要消费者构建 | Host及其Domain/Application/Infrastructure/Plc.Protocol依赖；Contracts/Communication/Rules/Integration测试工程；联合链沿StorePrep产物，012新增配方库准备分支触及工具时共用其必要构建，不重跑运行库升级矩阵 | 当前源码、SDK、产物摘要/命令/退出结果 |
| M02 / V02 | 分轴/独立Z、E扫码Z、翻转与放回两个事实、新分拣状态；独立预期 | Communication.Tests/ProtocolOracle/ProductionDefinitionTests、Devices/SignalConformanceTests、ActionHandshakeTests.Flip/Pick/StageMigration；迁移旧原码/ACK断言 | 独立来源、当前报文/动作与发现/执行结果；正式未定字段Blocked |
| M03 / V03、V04 | 首次3D实际有无/姿态/F定位；配置XYZ；首次/翻后异常跳过后续检测、最后原槽Pending分拣，物理槽不变且先前结果在 | Contracts.Tests/Station01/ThreeDStepTests、FScanStepTests及Recipes/PublicPreparationTargetResolutionTests；迁移测高断言，在真实算法/媒体链核观察 | 同run媒体/call/观察/配置/实际目标与异常槽；缺算法输出不得通过 |
| M04 / V05 | 单个合法更多面代表、四面1AB+3CD、额外E开/关；放回全齐才复查；F不重绑 | RecipeRunPlannerTests、RecipeExecutionCoordinatorTests，迁移Q03BudgetIncludesBothFacesAndOneFlipWithoutRescan；不穷举面数/AB位置 | 实际配置与步骤/采集/动作、有限预算、实体/面与PoseId关联 |
| M05 / V06 | OK不搬、NG/Pending各区、姿态异常无后续检测并最后实际Pending；分拣后下料 | RecipeSortingMapperTests.OkIsNotDispatchedWhileNgAndPendingKeepDistinctFormalActions已存在，保留；补姿态退出和新顺序，不另写第二mapper | 本盘实体集合、来源/目标、真实分拣/下料阶段和保存 |
| M06 / V07 | 一条单面完整链，实际正式入口→公共3D/F→软件绑定保存→检测→适用分拣→人工取盘→Final | Integration.Tests/Station01/ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite，复用RecipeExecution010RunHarness并定向适配新输入/期望 | 独立Host/VirtualPlc/Worker、实际媒体和SQLite，正式API/012必要页面、全部身份与Final提交 |
| M07 / V07 | 一条多面完整链含真实翻转放回、统一姿态复查、有效NG/Pending分拣及最终下料 | 复用M06正式驱动和RecipeMultiObjectIntegrationTests多面业务差异；旧OrdinaryTwoFaceAutoFlipExecutesBothFacesFromInitialHeight只是待迁移组件基础，不能当独立完整链 | 与M06同一构建/合同批次，自己的runId、动作/观察/保存；若型号承载未具备则Blocked而非跳过 |
| M08 / V09、SC005 | 012实际保存→完整重读→F唯一匹配；新保存供后续F，已冻结运行不变；重复码/保存失败不假成功 | 012新读写组件与011ExpectedRecipeMismatchTests共同；完整链输入必须来自实际保存结果，不能人工拼成功回执 | 服务器RecipeId及两个Version、唯一DefinitionDigest、ETag还原ExpectedVersion与过期写拒绝、实际SQLite提交/完整读回/正常重启重读、F/绑定/冻结记录；保存失败/重复码的真实拒绝，012页面仅按实际事实 |
| M09 / V06、SC006 | 必要保存、取消/原期限、未知反馈保护；绑定纯软件保存，实际翻转才下发型号 | RecipeApplicationReceiptTests/RecipeApplicationDeadlineTests按新RecipeBindingReceipt迁移；Communication ActionHandshakeTests.InvalidPickReceiptNeverDispatchesAnyPlaceField及ObservedPickWithoutDurableReferencePreservesReservation保有效义务 | 实际持久失败/提交未知及无后继动作；绑定无旧PLC配方写入，取消不复活动作，失败日志能定位 |
| M10 / V08 | 受影响协议泄漏/按测试来源分流负例被同一检查器拒绝，合法适配/共同执行正例通过 | Rules.Tests/Architecture/ProtocolBoundaryTests、ProtocolRepositoryBoundaryTests、RecipeExecutionBoundaryTests及既有检查器，按触及规则保正负例/正式源码扫描 | 当前具体规则/符号/位置与拒绝；不豁免新模型/接口，不以零发现通过 |
| M11 / V09、FR023 | 有效历史读取、旧逻辑实际替代删除、全部必需项执行结论真实 | 现DeviceEvidenceHistoryReader、RecipeApplicationHistoryReader、RunMediaCatalog受影响一个历史代表；plan清理表消费者/装配/配置/脚本核查 | 历史原payload不变、新缺字段不假造；删除后的实际构建/最小测试与消费者证据 |

M03/M05/M08/M09可复用M06/M07同次证据或必要组件结果，避免同义链重复。首次与翻后姿态异常、无料尽量合并同盘；若受现实际对象容量限制，可用必要组件补缺，不新增全异常矩阵。更多面和E差异优先组件；不能仅靠规划器列表通过声明实际执行已支持。

## 两条代表链的独立依据

- 单面代表：沿现有单面共同链，具体AB或CD选择依据保存的真实配方；F变化改变定位、配方XYZ变化改变检测目标。证明绑定无需旧PLC配方ACK仍必须真实提交。
- 多面代表：在合法已保存配置中选择一次能触及翻转/放回/复查及需搬运结果的链；可采用现有两面代表并补四面/更多面/E组件，避免为了更多面增加多条完整链。
- 真实扫码值、3D观察、目标点、面序、E姿态及预期处置在运行前来自版本化输入与已确认工艺，不能从被测规划器反推oracle。临时虚拟数值明确Test用途，不能填现场协议缺口。
- 012保存/重读/F快照用同一联合证据包。界面未参与的链只证明后端；组件替身不能算正式保存/整段共同执行。固定媒体必须由实际采集及worker读取，来源如实标记。

## 现有测试的迁移原则

旧“首轮测高算各面Z”“翻后不复查”“3AB+1CD合法”“先下料后分拣”“旧配方ACK/DeviceApplied”“Test HTTP特殊出口替代三区域”断言按新规则定向迁移，保关联/真实保存/期限/取消及错误拒绝。失败本身不是删除理由。

旧绑定故障注入AfterBindingCapacityWriteForTest、RecipeApplicationPreconditionHold依赖失效容量写/设备等待；将有效保存失败/取消/期限义务放到真实业务提交或真实翻转动作边界，不保留假容量写。旧BA/ACK专项不再全跑，历史通过/失败资料不删。

010禁止调用名字、历史迁移账本与有限历史reader仍有效，不因“旧符号命中”误删。已删除的IntegratedDetectionPort等只核实，无新删除任务。

## 首次构建、联合启动与完成判据

M01按受影响项目分批；源码早交与项目可构建/功能已验证分别记录。T003—T005改共同类型/端口时，011既有任务中的直接消费者迁移随基础批交，012收源码后先做T019及Host调用必要早批；所选Application/Infrastructure/Host/StorePrep及测试项目的直接类型消费者必须在各自首次构建前迁移。不能用窄过滤排除编译依赖、保错误字段/兼容层或假返回换通过。基础迁移不等待T027，T005源码不等待012保存验证才首次交付；012存储仍按T017早交。

**T027启动前置**：T025唯一驱动准备、T026及双方必要清理、D011共同基础/运行绑定/状态代码、012 T017存储交付、T019适用迁移/T020运行接线/T021页面与API代码、实际输入均就绪。M01和相应必要组件先于代表链运行，外部输入按依赖子项处理。

**执行与完成**：011统一启动M06单面/M07多面各一条，012 T022同期采证；更多面/E差异仅沿M04等必要组件。D012-ui-joint-evidence在运行后形成，是T027页面完成判据，绝不是启动前置；随后双方分别汇总真实结果。T025不另做完整链预验收，T026清理不等待联合成功。证据源码/合同/输入不再匹配时仅补受影响验证，不能复用失效旧报告。

## 执行与证据收口

实施及tasks阶段先把本表映射到真实class/method/dataRow，新增必要用例有明确ID；不是以本次发现结果反向生成必需集合。命令过滤必须覆盖事先选定项并记录发现、执行、通过、失败、Skip/NotRun/Blocked，零发现不能Passed；不新增验证平台。

证据保留当前源码/合同/构建/配置版本、实际命令、原始结果、Run/Tray/Plan/Action/Capture/Call/WriteId、查询及必要保存读回、来源与失败日志。复用同run事实注明对应义务；独立run不能拼成一条完整链。不要用旧报告填新变更缺口。

任务设计时已承接至tasks但未产生M01—M11结果；当前实际执行结果逐项见上述验证记录，不沿用设计时NotRun。后续若依赖的正式地址、ASCII、恢复/安全等尚未交付，明确标相应项目/子项Blocked，并继续无依赖的共同保存、模型和组件验证；局部通过不得宣称全范围或生产通过。


## 与012已接收验证设计的映射

012最终设计QV-01/02/03对应M08并复用M06/M07，QV-04对应受影响M10及012原型保护，QV-05对应M11。两端记录同批源码/共同合同1.3/实际组件来源、RecipeId/Version/DefinitionDigest、F/Run/快照与真实提交引用；012负责独立配方库/HTTP/页面，011负责冻结/执行/算法/通信。保存/目录/F必须是同一持久来源，SaveId不作为业务版本或ExpectedVersion。新3D生产/适配/消费/实际验证路径见EX01.1；角色明确不代表能力已就绪。012最终13份1.2设计已接收合入；1.3新增类型设计消费回执已接收，代码及联合运行证据在设计交接时仍待交付；当前已接固定批及同run证据见tasks-handoff和verification-report，不把文档接收当联合通过。

当前实施承接（非历史阶段描述）：T002最小集合/具名待交和局部来源已登记，逐项class/method/dataRow及数量见implementation-verification-map当前表；T003—T005源码/直接消费者已随共同001—004交付，唯一序列化/校验/匹配/深冻结11项与实际存储API9项、冻结reader6项及当前完整Contracts构建有据，具备各自完成判据。后续新增机械配置/Host入口依赖归T025/T026，不反向把基础交付说成运行能力。该段记录基础批交付时联合准备状态；当前T025驱动及单面05/多面03已实际运行，T027按当前验证记录收口。旧时点NotRun、仅设计或未授权说明按其历史范围读取。

T025保存/冻结联合承接：在同一既有代表驱动中，通过012正式HTTP完整读取及If-Match保存明确Test质量配置引用，先保存再启动F；真实V2/Intent/Bound形成后再保存第二版本。核当前运行的正文摘要、版本和计划保持初次冻结，活动目录/完整GET使用第二版本，实际执行事实仍关联原计划。仅编辑具名Test引用，不改变现场参数、不新建执行器/保存器；每条链仍只启动一次。原始HTTP响应/ETag及真实SQLite读取同run记录。正常重启及012页面仍须实际已交入口/生命周期安排，不以源码或旧报告补齐。

T025正常重启实施：scripts/011-owned-host.py仅负责011既有驱动启动的Host子进程，Windows独立隐藏控制台/进程组，控制输入只发关闭请求到已记录的本组PID，不按进程名/端口扫描终止其他会话。驱动在Final/输入释放后请求正常关闭，核真实Host关闭日志的流程/写入/资源排空及退出码，再用同库/同配置启动只读对账；不新开配方运行。原强制kill仅异常收尾，不计正常重启。脚本归011，012只消费启动/采证安排，不改其Program或业务端口，不新增生产控制接口。


T025输入接线：single-03真实XY与检测Z均到位，但两段运动及取证超过原8000ms完成期限。唯一驱动遗漏具名Test simulation.json stages.xyCompletion.delayMs=500，VirtualPlc用了默认3000ms/轴。驱动现显式传该500ms，并以无随机抖动执行确定Test延时，保存源及摘要。业务预算、位置校验、保存门、通过断言保持，03失败原证据保留；这不表示旧输入通过或现场速度获准。下一单面04使用相同驱动修订，仍须实际验证。

## 013实施前定向同步（2026-10-04）

SY-05/06：FR-001/013/016—023、PC02—05及执行状态沿013 A01—08/D02—07：两连接、单源用途采集；H300、B200/500、动作200、位置500/1000ms，首Moving/Executing局部50ms齐备后恢复200；原期限/取消/未知及中间态→完成→到位后实坐标因果保持。各块真实身份，基础与位置独立可靠性；最终位置发布后才返回完成，关键准入/采集释放即时核查。Host位置增加SampleStartedUtc、SampleEndedUtc、ConnectionEpoch、Reliability并升live/1.2，Domain和持久原格式不变。预算schema2.0/代表预算2/模拟2（模拟schema1.0）及Start/Host/fixture引用先迁移。实际取料、原始证据与有效在途提交后才Place不弱化。正常HeldFlip原5秒失败保存不强求未达1024的段；自动阈值真提交接续另用通信证据组件，原缺口拒绝独立保持。013每侧一次同run-2+空闲及必要组件/完整L/受影响009；真实只读API观察器只替013后端负载前置，011/012页面证据义务与历史报告不改。原T009/T010正式PLC缺口仍局部限制，历史任务勾选不变。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

前轮specify仅确认需求同步；本次014 Phase 1及012配套设计见当前设计引用，不生成新tasks；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。

后续只并入既定特殊/普通代表或必要组件：普通OK无多余分拣搬运；至少两件特殊OK实际返回各自原槽、保OK编号顺序、实际安全位后才下一件；原槽回放失败不记录完成/不推进。保存重读/冻结保原始关联，界面任意OK目标选择为0。不同件/区域同号和重新编号不串值，无同义新完整链或全量故障组合。
错误旧任意OK目标期望须纠正而非删除正确保护；失败/Skip/漏行/假反馈不得通过，009/010/013现行保护和验证门槛保持。本轮未执行上述验证。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。


当前014/012新增验证只消费014 contracts/verification.md V14-01—07；原M/QV证据保持原范围，受影响普通代表和至少两件特殊代表各一条，共同scope/原槽/safe/重复组/参数/抓手优先同run或必要组件证明。不要求两方先交最终证据才启动；本会话统一设计，运行阶段依批准任务组织同次采证。当前未运行，不全量/历史重跑或013性能重测。
