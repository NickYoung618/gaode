# 业务设备边界合同（目标）

**009收敛时适用范围（2026-10-02 10:54，历史）**：通信代码边界最小收敛。保留现行业务语义/期限/取消/安全/保存规则；当前只核边界、正式接线、构建、既有内容检查/正负例和固定直接语义集合。完整动态PD/36CS/M/MC、持久历史/升级/预算/故障/整机验收转出，不作为本轮完成条件。见spec活动条款、verification-gates BM00及scope-adjustment；转出不是Passed。

合同标识：`device-semantics/1`。依据活动FR-002—005、013—019、021—022及001/003/008有效业务义务；现有实现已部分迁移，完整专项尚未验收。FR-035—039完整业务验收转出，其已接受现用约束保留；已有对齐成果以T004—012证据为准，新签名仍先合同对齐。原始映射及握手只在[通信维护合同](protocol-maintenance.md)。

## B01 可见数据与依赖

Application/Domain、业务合同和业务测试只引用[data-model](../data-model.md)的业务状态、请求、结果、身份、期限、位置/面证据、执行来源和不透明诊断引用。每个公开状态用途以该模型§2为准；新增公开状态必须说明哪个业务决定使用它、为何不依赖协议编码，否则不进入端口。

禁止地址、寄存器/线圈号、原始状态/命令码、协议枚举强转、位运算、固定报文索引、内部清零阶段及raw字典/JsonElement通道。将字段改名为DevicePhase、TransportResult或包装枚举/DTO不构成例外。业务不引用纯协议模块、传输接口或原始诊断读取器；Host只负责装配/序列化，不解码位号。

物理槽号、面、坐标、业务期限、HMI配方展示编号、区域物理容量是合法业务数据，保持既有配方载荷和含义；编码范围转换在通信侧。业务不得按显示编号决定配方、流程或质量。

## B02 端口职责与正式接线

| 现有边界/目标职责 | 请求/返回的业务意义 | 必须保持的正式接线 |
| --- | --- | --- |
| IPlcStatePort | 当前DeviceObservation；初始状态评估独立 | 同一LatestProtocolPlcDevice解释真实采样；模拟端口仅在明确模拟环境。 |
| IPlcActionPort / IMotionPort | 启动、停止、合法位置请求、整盘授权后的解锁；返回关联动作事件 | MotionCoordinator/ResourceLease唯一运动准入；同一设备会话。不新增PC夹紧命令，启动后的夹紧来自设备事实。 |
| IInspectionHandshakePort一次性替换为IAcquisitionCyclePort | OpenCaptureWindow / FinishCaptureWindow；用AcquisitionSession表示可采集及周期释放 | 3D/F/E/逐图流程决定实际采集、算法/媒体结束和必要保存；通信决定内部操作/复位。保留同一Motion归口和位置保持。 |
| IPlcResetPort | 请求设备复位与ReadInitialReadiness | 物理反馈/新epoch/初始可用分别核验；业务看就绪和阻断原因，不见各ACK清零检查。 |
| 启动区域/槽位配置 | 保留本次公共准备实际需要的容量与槽位配置及来源；不提前选择产品配方 | 原PrepareZonesAsync区域握手属于旧协议历史，011不因其存在恢复已删除信号；当前公共准备使用新协议已确认条件。 |
| 当前F业务绑定（旧IPlcRecipePort退出011新链） | F唯一匹配、不可变冻结及必要业务提交，返回共同RecipeBindingReceipt | 三入口共用RecipeApplicationCoordinator与原预算/取消/保存保护；型号由实际FlipRequest下发，不再应用旧区域容量/配方ACK或造DeviceApplied。历史设备回执只读。 |
| IPlcStageActionPort | 当前实体取放、盘末下料、观察解锁；完整关联结果 | AdapterBindings注入同一设备和真实PickCommit回调；阶段端口不得再建客户端。 |
| 实际翻面/放回及适用人工步骤 | 当前物理实体、源点、配置目标姿态及独立放回结果 | 共同IPhysicalHandlingPort接同一通信设备；Test机械HTTP旁路替代后删除，人工仅保原批准范围。新协议缺映射局部受限，不保假反馈。 |

本表为必要接线的职责定义，禁止保留一个可绕过新语义边界的“旧端口”用于正式业务。NotIntegrated明确受限，不返回默认完成。原始反馈算法即使原在Application且只被测试引用，也须移出受保护业务程序集。

## B03 关联、完成和占用

同一次请求、运行、盘、对象/成员/整体、计划、面、物理槽、操作、ActionId、Attempt、连接代次及期限按适用维度一致。软件关联不能声称PLC已有ActionId寄存器。通信依据独占在途、发令前后实际变化/当前周期、正确位置/面、采样时间等现有已确认机制建立关联；无法区分的新旧同值反馈为UnknownHeld。

写入成功、受理、执行、可靠物理完成、业务提交和Final分别记录。未知编码不采用邻近值/默认值；写尝试后断线、epoch变化、完成/复位超时、错位置或错面不允许推进和自动重发。预派发可恢复通信重试保留原总次数/退避及绝对期限，由通信内部管理，不将重试阶段暴露为业务状态。

启动当前Accepted只表示本请求完整真实写序列的提交确认，不表示PLC已执行或夹紧；该语义事实真实保存后才开始原夹紧等待窗口。继续以当前可靠夹紧反馈完成启动步骤，不能新增启动受理寄存器或制造按钮事件。原写受理与夹紧期限分别保留。

### B03.1 旧20260925协议区域配置（R01历史范围）

以下A/B表及原来源记录只解释旧协议与历史证据，不是011新公共准备或F绑定前置；当前软件绑定采用B03.2。

| 阶段 | 触发、输入及业务前置 | 完成、失败与期限 |
| --- | --- | --- |
| A 启动区域准备 | 启动提交事实已保存、当前epoch可靠夹紧后；公共3D/F之前。容量来自Station01RuntimeOptions→PlcRuntimeOptions的NgCapacity/PendingCapacity，保持本次运行配置来源；不从尚未绑定的产品配方推导。 | 通信区域确认完成后，业务重新核对连接、自动、安全、设备就绪、夹紧及epoch，再保存夹紧事实并允许Running3D。沿现StartClampStep:116–127单独使用冻结PlcAcceptance窗口，不合并启动写受理或ClampCompletion窗口；失败/取消/超期保持UnknownHeld、请求停止、无3D/F，停止请求不算已停。 |
| B F后配方应用 | 公共3D/F实际完成并保存、F唯一匹配、目录/计划和当前绑定意图已保存且回执有效；实际输入为plan.PlcRecipeId、plan.NgCapacity、plan.PendingCapacity。两个容量同时有值才应用；都为空保留A已有容量，仅处理显示标识；一空一非空拒绝。 | B03.2独立总窗口覆盖适用设备应用、必要通信证据和本次绑定/适用handoff业务保存；设备确认仅为中间事实，全部必要有效回执在有效截止前齐备才发布有效Bound/续接或返回成功。显示编号不决定配方/质量。Test初值10000ms，Production未批准拒绝；不借A的PlcAcceptance。 |

区域内部清零/确认详见PC P04，业务端口只表达这两个用途和结果。A成功不表示F已识别或配方已唯一绑定，不能删除/移动A来统一抽象。B容量缺省的连续链和独立API回退差异须在I05/AL02对齐保留。

**DESIGN-OPEN-01的决定来源**：spec Clarifications（2026-10-01三项已接受决定）及FR-035—039明确补齐现状缺失的有限等待。B03.2落实该新增业务契约；不是此前源码已有期限，也不将无界等待冻结为基线。初次补齐并验证后，预算及下列边界与业务代码/合同/测试一起冻结，协议变体不得修改。

Completed必须带当前动作可核验语义证据与必要已提交诊断引用；不以非空引用、裸bool或Kind字符串单独放行。动作目标证据与完成时位置分别保留。011自动翻转核真实完成并在放回后复查姿态，不恢复已关闭的独立实际面号信号要求；原人工确认仅保历史批准范围及来源，不能冒充实测。

### B03.2 当前011软件绑定及沿用的期限保护

共同模型和回执唯一见[recipe-contract/1.3 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。原009完整专项仍为历史范围，不据本次文档同步宣称通过。

1. 三入口沿原关联运行的完整冻结businessMs.recipeApplication及准入；Test10000ms仅为原测试值，生产未批准保持局部受限。身份/版本/用途/来源/摘要及实际已有截止必须有据。
2. 当前意图真实提交且有效后、任何本次排队/绑定后继前登记唯一t0；T取原预算截止及实际已有更早截止。意图本身仍受CriticalSave和原更早期限。
3. 当前窗口包括冻结绑定及本次适用handoff的实际业务提交，每次保存取CriticalSave与剩余T更早者；不再包含已删除的容量、显示标识或配方ACK，不登记虚构机械动作。
4. RecipeBindingReceipt只由实际Intent/Bound/适用Handoff提交及有效回执形成；匹配或持久行存在不授权续接。独立bind保存本次意图/绑定，核既有handoff但不重建，productContinuationAuthorized=false。
5. 原后段起点、冻结预算及当前适用绝对截止不刷新；按期完成后的稍晚调度不倒判绑定超时，仍核取消/当前准入/后段期限。
6. 保存实际提交与回执有效性分别核验；回执在原截止前齐备且关联有效才成功。取消/超期关闭后台后继准入，已开始提交如实保留，迟到记录不复活，超时不等于回滚。
7. 型号在实际翻转动作内下发，翻转/放回/分拣仍必须核真实设备反馈及必要保存，不因取消旧绑定ACK放宽动作。
8. 当前最小验证见011 M08/M09；迁移旧BA中仍有效的提交、取消、期限和独立绑定不续接保护，旧容量/raw专项只保留历史证据。

PublicPreparationHandoffV2及GET/通知必须核本次RecipeBindingReceipt与handoff/Run/Binding/Plan身份及当前准入；IsVerified只证明持久身份/摘要，不能独立授权。重启/迟到只读核查不得恢复旧动作。意图/绑定/handoff只存当时已知事实，提交后ReceiptObserved按实际WriteId连接，不预填自身回执、不递归审批。

## B04 采集、算法与复位

正常业务顺序为：保存意图→申请合法定位→可靠位置与必要事实保存→开放采集窗口→实际采集/算法及必需保存/释放→请求结束窗口→设备周期可靠释放→下一合法动作。

公共3D失败清理沿现有`ThreeDStep`的finally条件：未取消、无安全故障且token未取消时，即使保存失败也可请求`CloseFailedCaptureWindow`完成受控退出/复位。清理不是业务完成、不放行任何后继，原失败与清理失败分别保留；此分支不要求伪造保存成功引用。F只有实际采集/结果记录成立后才结束，E/Detection沿各自现行保存前置，不能将3D清理策略泛化给其他角色。

通信承担检测、F/E在现行协议下各自的内部时序和正确轴。业务只传角色和已完成的工作/保存依据，不传开始码/结束码/复位码。公共3D与F仍各执行实际周期；每个拍照点完整XYZ。F周期释放不表示解码/唯一绑定成功；E缺码保留内部身份和问题后可继续具备条件的步骤，机械/保存未知仍阻断。批量首相机单图终态保存后可释放，不等待尚未采集的另一输入；融合和媒体租约原规则不变。

## B05 取料提交回调

回调逻辑签名：`CommitPickAsync(PickCompletionEvidence, CancellationToken) → PickCommitReceipt`。不是设备反馈的再解析器。

1. 通信在当前取料可靠、源位置匹配、必要内部处理完成及原始证据已提交后调用一次。
2. 业务读取已提交预留，检查Run/Tray/Object/Operation/Action/Plan/Epoch、源/目标/槽与证据一致，真实提交取料事实和InTransit，保留实际设备来源及引用。
3. 只有真实Committed回执且关联一致，通信才能在重新核对安全/epoch/剩余期限后写放料槽、目标及命令。任何放料相关写入都不能早于此点。
4. 业务保存拒绝/异常/结果未知、回执不匹配或回调后过期，不发放料，保留预留与未知占用。数据模型§4.1的A/B/C均不授权：调用超时不等于回滚，晚查到真实提交也不能恢复失效旧动作。不得以无动作重试、补造回执或仅日志替代数据库提交。
5. 放料可靠及通信内部收尾完成后返回语义结果；业务再提交目标占用和分拣完成。两次必要业务保存不可合并为“最终再存”。

### B05.1 取料已观察、必要证据未确认的独立失败出口（R05）

此时不能构造要求持久引用的PickCompletionEvidence，也不调用正常CommitPick成功批准链。通信交付`PickEvidenceFailureNotice`语义通知：当前关联、源位置/取料已观察事实及时间、ObservationId、真实来源、ActualCommit=ConfirmedRolledBack/Committed/Unknown及ReceiptValidity=None/Invalid（此出口无ValidCurrent）、原因、HoldsDevice=true/CanRetry=false；无raw和伪造引用。物理观察缺失使用PhysicalPick=Unconfirmed，不能与Observed混写。

正式阶段适配器通过独立的失败报告回调交给业务保存负责人；回调只能返回失败记录的真实保存结果，绝不批准放料。业务在数据库可写时以现有IStageEventStore短事务追加`StageEventType.UnknownHeld`、`kind=PickEvidencePersistenceUnconfirmed`，保留预留、已知观察和仍未知的提交状态；不制造SortingAssignmentInTransit、sourceVacated提交或成功回执。实际取料可能已发生这一事实与尚不能提交业务在途状态同时保留。不得用普通Failed/TimedOut/Disconnected清除DeviceHeld。

数据库也不可写时，立即关闭新动作、保持内存/设备占用；失败回调有CriticalSave及剩余原期限上限，不能卡住心跳/停止。结构化错误日志仅尽力输出，不能声称新观察已持久。重启的保守依据、只读核查和不重发规则见EC E02.1及数据模型§4.2。所有新回调和保存分支均待实现/待运行验证。

## B06 整盘和恢复

Detection有限结果→适用分拣/无需搬运依据→可靠下料准备→所有必要保存与无未知在途→WholeTrayCompletion提交→可靠ObservedUnlocked及保存→人工取盘确认及Final提交。检测质量、整盘就绪和最终完成保持区别。软件停止请求不冒充设备已安全停止。

原USR-D故障复位只保留历史批准范围；011新恢复/安全控制按DEP延期，不从旧双端复位推导新协议。已批准的新轮须有真实初始状态及显式启动；保留旧运行日志/图片/结果并建立新旧关联。人工换面后的继续和正常暂停后继续不被误改为故障重启。

## B07 变更分类与验收

只改变地址、位、原始类型/宽度、字序、编码和内部握手而保持本合同的输入/结果含义、安全、关联、期限与保存顺序：通信变更。改变本合同任何上述义务：业务契约变更，须先对齐对应规格/合同/计划/任务。未知物理含义局部阻塞，不由通信配置自行吸收。

当前验收使用BM00的V-BOUND/V-SEM/V-LEDGER及人工直接链核查；V-MAIN/V-PICK/V-FAIL/V-BIND/V-MUT完整验收转出，已有规则继续有效，未运行不得称Passed。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。窗口包含这次意图后设备、raw和绑定事实；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`仅作为旧payload中的原文保留，不生成同名新业务事实，不倒推历史原始值或来源。消费者不以旧名字/裸kind授予动作；当前面关联继续调用FaceEstablishment.Confirms，原证据与保存门禁不减。通信用例仍检验实际清零，业务断言迁移到当前语义事实和来源，两侧均必需；不新增页面、信号、恢复路径或产品兼容层。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。
## 当前专项验收对接

正式生产者/全部直接消费者及当前业务语义必须闭合并构建；只针对协议边界和直接受影响动作验收。已确认预算/三入口/必要保存/安全不删减，通信传入稳定期限并承担内部握手/取消，不把raw封装给业务。当前检查及直接语义集合见VG BM00；动态S00/PD/M/MC及整机/页面/完整历史/完整预算验收转出不等于完成。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 013实施前定向同步（2026-10-04）

SY-02/03：FR-008、P03/P04当前由013 A01—08承接：合法有限读计划在不可变映射准入后生成/校验/复用，非法映射与未知地址扩读仍拒绝；两连接、通信内部单源/有限PDU仲裁，不把协议知识或轮询参数放入业务。E01/E02/E04的基础与Position各自真实采样起止/代次/可靠性，不能用新心跳续旧值、拼接成原子快照；普通位置陈旧不等于断线，关键准入按需实读，完成后实际位置先发布。Domain观察形状及plc-evidence/1保持；Host device-semantics/1.2位置新增SampleStartedUtc、SampleEndedUtc、ConnectionEpoch、Reliability，Axis消费者使用Position.Identity.Reliability。保留8192窗口、gap拒绝、1024分段、真提交/保存门及旧历史原文。T022/观察/证据历史勾选不变；新实现归013 T011—T025。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

### 010实施定向对齐 A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A09（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A09**：所有验收profile无条件L，统一verify/runner、auto-dev/step及009 boundary_minimum/protocol_isolation旁接共用执行/凭证核验。_run_verify passed、verify_entry退出、assess/finish及旁接ledger/result/subsetPassed最终点拒漏跑/Skip/旧身份/解析失败/伪Passed。L含职责闭包B/N/P、G/C、受影响009静态，不开Host/PLC/Worker/DB、不递归完整验收。010按B→冻结→E/S→T，009动态范围及SelectedCasesOnly overall009Passed=false保持；D10先迁活动映射，历史证据不改。
  生产/消费与010实施承接：Rules/manifest/migration→verify及workflow/旁接aggregator→最终凭证/结论；T033—T042；009活动JSON映射属于T033，历史快照/报告只读。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

013 T024定向补充：现行Host/012已消费的axisObservations按011 execution-and-state EX状态表逐字段登记语义字段，不把业务消费者改归通信。Python内建isinstance(value, dict)仅作已知JSON对象类型谓词，不能授权动态键、原始字段或任意helper逃逸；同名重绑定仍拒绝。未知profile显式拒绝，原C01拒绝义务保持，C03合格当前凭据沿显式Default009验证，不允许未知profile回落。


### 014 Phase 1必要消费（2026-10-05）

沿现语义端口增加TransferToRotation/Rotate及用途/抓手ID/安全事实，详见014 EX14-03/04；原始抓手/R字段与地址、编码、位键仅通信适配。新raw字段必须进入在用signal/role/read-plan扫描、准确承接键空间及现门禁；不豁免、不另建采样线程，保013单源降频和旧偏差。业务/请求/结果类型的scope与origin仅业务身份，不能把显示号当协议槽。真实pick receipt和放料/safe完成门保留，地址/安全缺项只限制对应正式动作。此处设计义务未实现/验证，后续任务阶段准确登记消费者。
