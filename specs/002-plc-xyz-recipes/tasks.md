2026-09-26旋转Test增量：T11按[虚拟旋转请求/结果合同](../003-plc-latest-protocol/contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。

2026-09-26 人工子范围增量：T11按[人工执行合同](../003-plc-latest-protocol/contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

> 当前008执行以文末“S0—S5任务增量”为准；此前原版本范围/固定样本/两配方/原型只读等冲突条款仅属历史。未受影响任务保留原状态。原文已逐字节归档：[历史任务](tasks-history-before-s0-s5-20260924.md)。

# 开发任务

T11 E子交付：按[003 E合同](../003-plc-latest-protocol/contracts/e-test-execution.md)验证适用物料、显式E目标/面/参数、冻结计划与实际消费者；配合008 T061原验证范围，任务编号和历史事实保留。

T11直接增量：按008[多对象合同](../008-recipe-driven-inspection/contracts/test-multi-object.md)同步成员pointRefs、冻结负载、准入校验与实际生成配置；保留任务编号、历史状态，完整原条件和相关验证齐备后收口。

- [x] T01 保留并记录原测试基线。
- [x] T02 独立VirtualPlc、按钮夹紧、信号所有权。
- [x] T03 Gaode原生协议、连接泵、心跳、启动、XYZ、清零、区域和配方ID。
- [x] T04 XYZ配置合并冻结、容差/限位与模式准入。
- [x] T05 MotionCoordinator统一运动期限。
- [x] T06 F后独立配方绑定、快照、数字ID及区域覆盖。
- [x] T07 Host配置、运行脚本、独立存储准备。
- [x] T08 原测试回归、跨进程第一工位和F后配方联调、故障验证。
- [x] T09 记录证据、文件清单、真实接入边界。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

> T10 已由下方当前增量替换；原编号、未完成状态及全文见 tasks-history-before-s0-s5-20260924.md，不作为当前实现任务。


## S0—S5任务增量（2026-09-24，宪章5.0.0）

所属功能：`specs/002-plc-xyz-recipes`。跨功能依赖写作目录简称+任务ID，完整目录见008 tasks映射表。原则P03/P04/P05/P07/P08/P09/P11/P13，前端另P12及用户最小原型授权；新增任务全部未完成。旧T10被以下任务替换，未受影响的历史待办不取消；公共验证只做必要正常/失败，不构成交叉穷举。

- [ ] T11 [US1] 对应FR11和008 FR-001/004/017/018，在`backend/src/Gaode.Application/Recipes/RecipeContracts.cs`、`RecipeRunPlanner.cs`、`backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs`及`RecipeCatalogFactory.cs`扩目录只读枚举、版本/摘要/用途、scene-route解耦及实体/检测目标/槽位/面/轮次/步骤owner模型；同一目录F唯一，未知能力和缺参数标受限。依赖：现行008 data-model和contracts，无实现前置。交付：Q01先可用，其余当前适用数据可表达且未实现能力不可派发，供003 T068与001 T090消费；`backend/tests/Gaode.Rules.Tests/Recipes`只验证当前适用表达、身份和非法配置必要断言，证据`specs/002-plc-xyz-recipes/evidence/008-catalog.md`。同程序版本行为变化完整验证归008 T058，不能以加载通过抵扣。 USR-E增量：按USR-E逐实际对象校验固定四面仅3AB＋1CD或3CD＋1AB、AB位置由配方确定；S2成员/S3部位同检，一面两面保持，不按Q硬编码或删扩展能力。历史版本可查，退出当前业务版本Restricted且直接启动拒绝，旧快照不回写。先交目录/校验供008 T050，再接001 T090/003 T068；原catalog证据验证合法中间少数组、退出组合拒绝、历史可查及页面/直接入口一致，完整运行归008。

第七批T11子范围：按008 Test映射合同增加Q03两面/两轮位置负载校验，缺映射或错轮次给具体Restricted原因；已配置完整仍因Flip合同未接入保持Restricted，原T11全范围勾选条件不变。

> 旧协议历史检查点（不代表新版状态）：008第八批T11子范围：Q03面/轮映射可供当前轮解析，但未配置Flip接口继续Restricted；T11整项原验收和勾选不变。

> 旧协议历史检查点（不代表新版状态）：当前检查点：目录现返回精确限制`FlipPickPlaceTransmissionUnconfigured`，Q03配方/预算/面轮次仍可表达但不可正式派发；目录回归3/3，详见[008第八批证据](../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。T11整项未勾；后续无E自动路线仅在Q03正式通过后按适用数据增量推进。


## 2026-09-26新版协议增量子范围（未实施）

既有编号和勾选只证明原范围，本表所有新版子范围均NotRun；实现前置按所需子能力交付，整项验收仍保留原未齐项。输入为唯一分区协议及008 execution/3.0；日志须可按run/step/operation/实体/面/连接代次追踪意图、派发、反馈、ACK清零、保存及失败。

| 原任务/新版子范围 | 具体消费者（文件简称按原任务路径） | 输入、前置、完成条件及最少验证 |
| --- | --- | --- |
| specs/002-plc-xyz-recipes T11 / 20260925协议 | `RecipeContracts.cs`（含RecipeRunPlan）、`RecipeRunPlanner.cs`、`JsonRecipeCatalog.cs`、`catalogs/recipe-catalog-review.json` | 依据008目标0.5；取消rescanWholeTray3D/newCoordinateEpoch强制标记及多面固定Restricted；显式执行阶段/面/测量/配置版本与flipPosition，能力和数值齐才Available；验证Q03无Rescan、逐面目标齐/缺、整体不重复。 |


## USR-E当前依赖与完成口径（2026-09-26）

依据宪章7.0.0，完整归属/验收见[本轮任务交接](../008-recipe-driven-inspection/tasks-six-issues-alignment-20260926.md)。两端协议/诊断子能力＋当前配方准入/目录→必要代表性协议及正式路线验证→USR-D完整新轮。003 T072-A＋001 T078→001 T052→008 T068（复用008 T054及003 T069）→003 T072-B→006 T051→008 T069→008 T070；A/B/M分子交付，003 T069不反向等008 T068，不等全部Q/C/F或特殊生产。共享源码按文件串行交接，设备/worker/页面串行采证；子交付不勾父任务。问题1—5根因待实际包核验，状态2/3实时Z不强制等于取放目标Z；生产采样窗口只局部限制。历史勾选/正文不改，旧Q/旧恢复Passed不抵新验收。

## 009 / AL02 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

既有ID/顺序/勾选保持；以下共享增量唯一实施归属009 T017/T030—T040/T048，原任务完成不能抵扣本次新增义务。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

物理槽号与Sequence分离，端口使用业务整数PhysicalSlotIndex；既有配方payload的protocolSlotIndex由配置边界解释为物理槽，不改配方输入。料盘编号、配方身份和PLC产品型号分别管理；型号供PLC执行内置机械程序，不能只按旧显示编号使用，不绑定旧ushort宽度。区域A按运行容量在夹紧后/3D前完成；区域B仅在F唯一匹配及绑定意图有效保存后应用计划输入。连续链两容量均null保留null并跳过B容量握手，独立API原运行容量回退保留，不能为了统一接口改变输入。两容量有值分别应用，随后显示标识真实读回；由通信完成内部握手。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

产品配方、初始测量/各面目标、四面3+1、组/整体及持续扩展规则不变；翻转放回后统一复查3D姿态，F不重绑。BA02两容量、BA03健康I/O但条件不成立、BA04/05边界取消及BA06原截止是必要替代断言。

依赖：先本节spec/contracts/plan实际对齐，再009 T012复核，代码依009各任务的合同/基础前置；按迁移矩阵承接有效断言。完成证据：V-SEM/V-WIRE、V-PICK F05/F06、V-BIND BA01—07及实际消费者验证，按任务适用项分别取证；新增能力未实施、运行未执行。本功能历史任务不自动勾选。


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

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。
