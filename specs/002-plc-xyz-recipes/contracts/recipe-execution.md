2026-09-26人工Test目标resolvedFlipPosition可显式mode=manual/manualWaitMs；原自动字段保持。历史人工参数来源及消费见[合同](../../003-plc-latest-protocol/contracts/manual-test-execution.md)。

# 配方执行边界（2026-10-03现行同步，历史增量保留）

历史2026-09-26 T11 E接入记录（仅说明原Test范围，当前E需要性和独立姿态由011共同合同RC03配置，不按K19/Q19或产品名分支）：原增量采用[003 E Test合同](../../003-plc-latest-protocol/contracts/e-test-execution.md)：成员目标面带E pointRef及0.5目标，Position(E)紧接ReadECode，显式采集/解码参数；适用对象依来源K19/Q19，不给全部配方开启E。

2026-09-26 T11增量：[多对象Test合同](../../008-recipe-driven-inspection/contracts/test-multi-object.md)定义0.5 resolvedObjects与成员pointRefs、S2 problemMembersOnly及S3整体共享动作。旧目录未有完整来源仍Restricted；此合同不声明实现或页面通过。

对应FR11及当前T11（旧T10已替换归档），当前宪章8.0.0。复用已有RecipeContracts/JsonRecipeCatalog/RecipeRunPlanner，不创建第二份生产配方模型。
共同字段及唯一校验见[recipe-contract/1.3](../../011-plc-interaction-update/contracts/recipe-contract.md)；008数据模型及执行合同承接相关结果关系和仍有效的历史语义：scene与route分开，实体/部位/组/成员与物理槽位分开，面与相机名分开，参数/点位/预算版本冻结。
公共3D及按首次定位执行的F先于F后产品绑定；产品计划不反向决定公共点。F必须唯一有效匹配并与人工场景一致；修改配置只影响后续盘。
ordinaryBatch、ordinaryAssembly、specialType1是少量明确路线，当前适用序列共用同一规划逻辑，旧表达能力不因范围收窄删除。Review目录的全AB和全组同槽不作为生产确认值；组策略、旋转参数/信号未确认不能默认。
测试和生产共用加载/校验/计划/执行，Test参数禁止真实动作；加载成功或计划生成不等于执行完成。
保留Q编号的当前适用路线、C01—C08、F1—F6及同程序版本参数变化证据见008 recipe-cases/coverage-matrix，历史T01—T09不抵扣新增执行。

共同配方和绑定定义唯一见011 recipe-contract/1.3；目录HTTP与启动消费仍由008 api-results/012读写合同承接；IRecipeCatalog/JsonRecipeCatalog提供同目录快照数据，003 API投影和001 F校验共同消费。当前绑定及冻结后的recipeId/version/catalogDigest须一致，后续F绑定读取经共同校验并真实保存成功的新内容，冻结运行保持原内容，禁止暗改在途快照。冻结模型见011共同合同RC05及data-model，区分InspectionTarget/physicalEntity/part/group/检测面/独立扫码姿态/观察轮次/物理槽；旧heightRound仅用于历史读取；步骤有唯一ownerStage，计划序号不得冒充PLC槽号。008 SC-001选定路线通过实际页面完整执行验证，四面位置变体不等于全部实跑门槛，规划器展开仅为辅助检查；Test目录与生产目录隔离，不修改在途快照。
# Q03 Test受限映射增量（2026-09-25历史，不作为新版准入）

`resolvedDetectionTargetsByFaceRound`按`face:heightRound`保存每面A/B位置和Test高度绑定；同一逻辑`pointRef`仍须携对象/槽位/面/轮次解析到不同实际点位ID。目录校验必须发现缺第二面映射或错轮次，完整数据仍因自动翻面未获本轮握手合同而保持Restricted。详情以[008虚拟Test映射合同](../../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)为准。

008第八批Q03：按face:round读取已有Test映射；第一轮只要求1:1结果，第二轮仅在新3D实际保存后读取2:2。目录在Flip接口缺项时保持Restricted；组件阶段输入不提升目录可用性。


## 2026-09-26直接接口增量（目标，代码待实施）

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。
唯一实现归属：specs/001-station01-public-preparation T090移交，specs/002-plc-xyz-recipes T11模型/目录/规划校验，specs/008-recipe-driven-inspection T052/T060执行消费。

## USR-E当前业务准入（目标，未改配置）

按每个实际检测对象的完整序列校验，四面AB/CD计数为1/3，AB位置由配方确定；更多检测面仅用AB/CD按配方表达，不套四面计数；一面两面不变，S2逐成员、S3逐适用对象/部位核对。共享实体动作不改变必检面定义。不按Q编号硬编码、不把当前3＋1做成永久引擎限制。旧配方保留历史查询，但退出范围版本不得在当前目录标Available或绕过页面派发，沿用Restricted及原因。冻结在途快照不回写，不新增发布平台。生成器/版本化Test集合见[交接](../../008-recipe-driven-inspection/plan-six-issues-alignment-20260926.md)。

## 009 / AL02 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

物理槽号与Sequence分离，端口使用业务整数PhysicalSlotIndex；既有配方payload的protocolSlotIndex由配置边界解释为物理槽，不改配方输入。料盘编号、配方身份和PLC产品型号分别管理；型号供PLC执行内置机械程序，不能只按旧显示编号使用，不绑定旧ushort宽度。旧区域A/B容量应用及显示标识读回属于20260925协议和009历史绑定范围，原记录可读；011新F绑定不执行这些已替代的握手。当前容量是实际槽位/目标区域配置输入，不伪造PLC容量回执。

当前011三入口共同消费[recipe-contract/1.3](../../011-plc-interaction-update/contracts/recipe-contract.md)的业务绑定回执；新F绑定只做实际冻结与必要软件提交，不再要求旧PLC配方ACK、设备DeviceApplied或对应raw保存。型号在实际翻转动作内部下发，该动作仍有真实反馈门。复用已有独立recipeApplication预算来源，Test10000ms只为原版本化测试值；意图真实提交后唯一t0，取原预算与已有适用截止更早者，每次必要保存再取CriticalSave/剩余窗口。取消/超期关闭续接资格，晚记录不复活；独立API不制造后段窗口或重复handoff，productContinuationAuthorized=false。

产品配方、初始测量/各面目标、四面3+1、组/整体及持续扩展规则不变；翻转放回后统一复查3D姿态，F不重绑。原BA02两容量/BA03设备绑定断言随旧协议退出当前绑定验证；将仍有效的必要保存、取消及原截止保护迁移到RC05.1真实业务提交和实际机械动作，不删除历史失败证据。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。011当前窗口包含意图后的冻结绑定及适用handoff真实提交；旧窗口的设备/raw记录只属原协议历史；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。

### 009 旧设备绑定必要通信证据（2026-10-02历史范围）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。旧协议生产者当时必须携实际必要通信证据保存回执，以下字段只说明历史读取：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。

旧DeviceApplied/CommunicationEvidence记录仅由有限历史reader按真实WriteId、Correlation及不透明引用只读查询，原记录不补写；缺失保持null/NotRecorded。当前011业务RecipeBindingReceipt只来自实际意图、RecipePlanBound及适用handoff提交，记录真实已知回执与窗口；不预填自身提交后的观察，不用空结构或假设备反馈代替真实提交。所有适用保存仍共享原总窗与CriticalSave，不新增成功审批或延长期限。

旧RecipeApplicationReceipt的完成资格当时包含此有效必要证据回执；011当前绑定资格改为RC05.1的RecipeBindingReceipt；旧历史不能因行存在或新模型默认恢复当前资格。不存在的raw引用、提交未知/失效均不授予后续动作；存储部分不可用仍沿F05/F06。真实实现及先失败/后验证归009 T037—T045/T047，三入口与独立进程BA验证分别计证。公开API仍使用已对齐§5.5形状，不新增页面字段或修改旧payload。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。

### 010实施定向对齐 A01/A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A01**：共同输入不含ProfilePayloads/PositionPayloads或fixture JSON；文件解码和替代语义提供者调用唯一RecipeDefinitionValidator，RecipeRunPlanner保组成/面/必检/身份规则。环境提供CoordinateDefinition，共同CoordinateResolver按配置XY/检测Z与单位/范围及对象/面/观察轮/槽校验；新执行不以测高或HeightRound作为检测Z前提，原测量偏置输入只供历史读取。TestEligibleSlots移批准边界。
  生产/消费与010实施承接：catalog/validator/planner→Start/绑定/预算/移交/检测/分拣→API/fixture/投影；T008/T009/T011/T012/T016—T020。
- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 011统一业务合同同步（2026-10-03）

依据[011确认记录](../../011-plc-interaction-update/spec.md#clarifications)，本节表达已确认业务约束；字段和签名已由011 Phase 1共同合同唯一定义，012承接存储及HTTP设计，不新增第二套业务模型或执行路径。

| 规则 | 共同合同义务 |
| --- | --- |
| 保存与冻结 | 经唯一业务校验并真实保存成功后，后续F绑定使用新内容，已冻结运行保持原内容；保留生产准入，无新增发布审批/版本平台 |
| F身份与唯一性 | F内容就是料盘编号，不同配方不能占同一匹配码；编号、配方身份、PLC型号分开。未匹配不进依赖配方动作，公共准备/F仍按正式流程 |
| 面与扫码姿态 | 支持1/2/4外更多面，仅AB/CD，四面3CD＋1AB且AB位置由配置确定；不推导更多面固定组合。四检测面后可选独立E姿态，参数/需要性来自配方，不按产品名或测试号分支 |
| 坐标与异常 | F XY来自首次3D，检测XYZ来自配置，E用扫码Z；翻转放回后统一姿态复查、不重绑F。异常物理槽原位退出后续检测/翻面/分拣并返回槽号 |
| 处置与顺序 | 同盘OK/NG/Pending，初始在OK区；OK原槽、NG/Pending到各区配置点。用途点位不合并；适用分拣及保存后再下料 |
| 责任 | 011主责共同模型、唯一业务校验、绑定/冻结、通信及执行；012提供编辑保存/目录/API/前端消费，接收状态见011同步清单 |
| 实施清理 | 检查真实调用、装配、配置/脚本/历史读取消费者，承接有效保存/关联/取消/期限后实际删除替代且无用途的旧逻辑、协议、旁路/测试特权/配置/测试/孤立代码，保留失败证据 |
| 验证 | 联合代表链实际证明保存重读/F唯一性/快照、更多面/额外E、三区及姿态退出和受影响架构负例/失败保护；不全量/穷举/重跑009或010全历史 |

