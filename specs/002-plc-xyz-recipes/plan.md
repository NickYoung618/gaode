2026-09-26旋转Test增量：T11按[虚拟旋转请求/结果合同](../003-plc-latest-protocol/contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。

2026-09-26 人工子范围增量：T11按[人工执行合同](../003-plc-latest-protocol/contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

# 实施方案

E直接增量复用成员目标和Position/ReadECode计划，显式面、E采集/解码配置及来源；合同见[003 E Test执行](../003-plc-latest-protocol/contracts/e-test-execution.md)，不增加第二套配方结构。

T11多对象增量复用RecipeMember/RecipeRunPlanner/JsonRecipeCatalog，在成员上保存独立pointRefs，冻结resolvedObjects负载；逐目标验证来源与身份，再交001解析。按[共享合同](../008-recipe-driven-inspection/contracts/test-multi-object.md)执行，不复制父目标填缺。

保留Gaode四层和第一工位流程。迁移协议编解码和配方纯规则，命名空间改为Gaode，无Inspection项目引用。新增双通道PLC通信泵（主状态/命令、心跳），上位机观察缓存带来源/代次/时间。写入结果不等于物理完成。

XYZ通过现有公共快照合并冻结坐标文件及摘要；旧FullSimulation配置仍可使用原XY合同。外部PLC模式强制XYZ和系统时钟。移动状态机消费运行→完成→坐标证据，只写PC信号。

配方用例消费已经提交的Handoff，保留第一工位终态不修改。独立记录配方冻结/PLC绑定意图与结果，防重复与重启重放；后续全配方物理执行仅在协议支持的动作范围内开放，不能以合成回放冒充硬件完成。

Production预检拒绝缺失真实采集/算法或测试坐标；原FullSimulation无网络依赖。

## 2026-09-24 最新需求与008完整执行对齐

当前增量适用宪章7.0.0。既有配方目录、F唯一匹配/绑定和规划器复用；先调整路线兼容与强类型身份再接008执行器。早期“第一工位终态不修改”仅适用当时独立handoff，后续以003 v2非终态连续移交为准。

设计前/后检查：P03/07/08/09/11/13符合目标设计，相关OPEN只限制使用点；前端额外P12。阶段/用例/必要失败/完成门禁统一引用[008方案](../008-recipe-driven-inspection/plan.md)，本功能只承担上述边界，不复制完整执行计划。


当前实现顺序：specs/002-plc-xyz-recipes T11统一目录枚举、版本选择、身份与步骤模型/校验及当前一面两面、四面3＋1适用性表达；随后001 T090/003 T068接双方，specs/008-recipe-driven-inspection T051/T052算预算并消费执行。Q01先交付所需模型，其他路线能力未接入时标受限；后续扩展沿同一模型，不另造配方引擎。C08须同程序版本改变实际行为，目录加载检查不能替代完整运行。
# 008 第五批目录增量（2026-09-25）

specs/002-plc-xyz-recipes T11目录按[008 Test合同](../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)只把映射完整且当前单面能力支持的虚拟Q01/Q02标为Available；其他和未获批生产位置继续Restricted。版本/摘要仍冻结，不能以Review旧坐标或组件注入XYZ放行。

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。

## 2026-09-26直接接口增量（目标，代码待实施）

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。
唯一实现归属：specs/001-station01-public-preparation T090移交，specs/002-plc-xyz-recipes T11模型/目录/规划校验，specs/008-recipe-driven-inspection T052/T060执行消费。

## 009 / AL02 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

正式接线保持原Host与运动/保存所有者，按以下已对齐职责实施：

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

物理槽号与Sequence分离，端口使用业务整数PhysicalSlotIndex；既有配方payload的protocolSlotIndex由配置边界解释为物理槽，不改配方输入。料盘编号、配方身份和PLC产品型号分别管理；型号供PLC执行内置机械程序，不能只按旧显示编号使用，不绑定旧ushort宽度。区域A按运行容量在夹紧后/3D前完成；区域B仅在F唯一匹配及绑定意图有效保存后应用计划输入。连续链两容量均null保留null并跳过B容量握手，独立API原运行容量回退保留，不能为了统一接口改变输入。两容量有值分别应用，随后显示标识真实读回；由通信完成内部握手。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

产品配方、初始测量/各面目标、四面3+1、组/整体及持续扩展规则不变；翻转放回后统一复查3D姿态，F不重绑。BA02两容量、BA03健康I/O但条件不成立、BA04/05边界取消及BA06原截止是必要替代断言。

代码前置：009 T012实际跨功能对齐复核完成，随后严格按tasks各项依赖；不把本节当代码已经交付。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。窗口包含这次意图后设备、raw和绑定事实；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。

### 010实施定向对齐 A01/A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A01**：共同输入不含ProfilePayloads/PositionPayloads或fixture JSON；文件解码和替代语义提供者调用唯一RecipeDefinitionValidator，RecipeRunPlanner保组成/面/必检/身份规则。环境提供CoordinateDefinition，共同CoordinateResolver执行测量关联、偏置/单位/范围及对象/面/轮/槽校验，不由环境预算业务Z。TestEligibleSlots移批准边界。
  生产/消费与010实施承接：catalog/validator/planner→Start/绑定/预算/移交/检测/分拣→API/fixture/投影；T008/T009/T011/T012/T016—T020。
- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
