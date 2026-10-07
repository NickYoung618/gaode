# 保存、维护边界、恢复及移交合同

2026-09-26 T090多对象直接增量：严格handoff消费008[多对象Test合同](../../008-recipe-driven-inspection/contracts/test-multi-object.md)。目标分别解析每成员/部位的初始测量映射；S2预期结果为成员及独立源点，S3为整体父源点且保留部位结果身份。缺对象目标先拒绝，不能用父目标补齐；初始3D/F不重采。

**版本**：s01-persistence/1.1；**修订日期**：2026-09-20；未创建数据库或执行任何SQL。  
关联FR-021至FR-024、FR-030/032/039，CL-02/06，P06/P08。

## 1. 保存端口与顺序

ITraceWriter.SubmitCritical(WriteId, RunId, ExpectedRevision, BatchKind, PayloadDigest, Records)返回排队回执；Persisted事件仅在短事务真正提交后发出，包含WriteId、CommittedRevision及结果。相同WriteId相同内容查询/返回同一提交结果；不同内容冲突。ExpectedRevision指提交时预期的持久Run.Revision，不是API观察版本；调用方从已确认保存边界取得它。终态批次还携带ExpectedTerminal=None、候选终态及必要证据引用，条件不满足返回ConditionRejected及实际持久版本/终态，不能当作Committed或普通磁盘失败。不在事务中等待设备、Worker或文件。

ITraceQuery独立短读连接/上下文，开发查询预算1000ms，超期返回QueryTimedOut且不阻控制路径，当前状态优先读Coordinator不可变快照，区分observedRevision和persistedRevision。数据库失败仍可读取已有内存状态并明确持久化不可用；历史库不可用时返回错误，不伪造历史。

必要顺序：

1. Run及入口审计、公共/预算/模拟快照已提交，再保存Start/夹紧观察意图并请求PLC。
2. 每次XY意图Committed后才派发；匹配完成事实Committed后才允许对应采集。
3. Capture意图/触发关联先保存；MediaStore接管、写临时文件、完成并关闭/刷新、同卷改名为不可变成品，再提交媒体元数据及采集结束事实。不能仅凭rename宣称断电保证，真实磁盘/缓存能力后续验证。
4. 媒体及采集事实提交后，登记Call及原期限，独立提交AlgorithmIntent批次：CallId/OperationId/Attempt、RunId/CaptureId、输入MediaRef及其完成/元数据提交引用、公共/范围/参数/能力/期望算法组件版本、原Session/ClockId/StartTick/DueTick/BudgetRef、调用依据（采集结束、媒体有效性与所选能力）。缺项如实记录，不猜版本；预算未配置时DueTick为空并保存NotConfigured依据，不建立无界期限或实际派发。此批次不改已有采集事务；复用同一SubmitCritical/WriteId机制，不新增保存通道。
5. 只有AlgorithmIntent明确Committed，原期限未到、运行和资源仍允许时才派发算法；Failed/CommitUnknown/ConditionRejected均不得派发。调用意图不是Dispatch/Accepted证据；已有能力缺失等调用异常仍须保存依据和有限终态，但不向未接入端口伪派发。保存原始响应与规范化结果/异常、期限裁决和继续依据，再派依赖下一动作。
6. 核对全部完成门，以§1.2条件事务提交Run完成与唯一HandoffSnapshot；取得提交事实后才通知Ready。动作与算法意图、结果保存不因算法失败省略。

### 1.1 算法意图、期限与中断

选择独立AlgorithmIntent提交，避免让文件保存流程承担算法调度职责；T025管理同一提交门，T033/T035提供各自调用依据。算法预算起点仍为Application登记Call、提交调度之前；先登记原期限，再排队保存意图，因此意图保存等待、调度排队及执行共用原预算，不能在Committed/Execute/Accepted处重新计时。保存另有原2000ms测试预算，两种预算分别到期。

若意图保存期间算法已到期，形成原Call的TimedOut（PreDispatch），即使随后意图Committed也不得发送；需保存该终态和继续依据。若保存失败/未知，算法等待仍有限结束，但必要保存门继续阻断后继，不能以算法超时跳过核对。取消/停止可立即受理。仅对确实派发的调用取得Worker输入租约；未派发引用仍按媒体保存规则保留。

| 中断窗口 | 必须核对的事实 | 处置 |
| --- | --- | --- |
| AlgorithmIntent提交前/提交结果未知 | 原WriteId、批次及Run/Capture；在当前进程提交门未打开可证明未派发，重启不得只因查不到记录就推断原在途事务已失败 | 先排除旧写者/在途事务并核对原WriteId；不派发。能证明未登记/未发起的步骤才可按CL-02受控开始；已登记Call不能换ID或重置预算逃避期限 |
| 意图已提交、实际派发前 | 已持久Call及媒体、期限；“无Dispatch/Accepted记录”不能证明未发出 | 当前会话若有可靠未派发证据且期限仍有效，才可继续原派发门；发生进程中断的未终结Call按原身份保存Error/Interrupted及实际派发依据（未知则Unknown），不得自动重算/重拍 |
| 已派发、结果尚未保存 | CallIntent、可能存在的Dispatch/Accepted/原始结果文件及WriteId；意图本身不证明算法收到 | 原结果身份/完整性及原Host入站时间与期限裁决可核实时才幂等补存原终态；仅有结果文件不能推定期限内成功。否则保存现有结果为补充证据，原Call有限终结Error/Interrupted并保留DispatchUnknown/已知派发证据。核对和必要保存通过后才可继续独立步骤；不再次Execute或Capture |

重启不能复用旧单调tick。已有Call无终态时保存Interrupted是恢复裁决，不重新给予一次算法预算；该规则仅用于旧事实收敛/查询，故障不能在新会话续跑旧后续步骤。USR-D新run重新公共准备和预算，完整旧终态仅查历史；正常暂停按原run继续。意图提交时“尚未派发”的观察只证明该时刻，不证明随后没有Execute；恢复不能把该旧观察当作NotDispatched，缺少覆盖派发窗口的可靠依据必须标Unknown。

### 1.2 完成与取消的唯一终态裁决

单写者在短事务中执行持久条件提交，**第一个满足条件且真正提交的终态事务胜出**；请求到达、入队、内存状态和回执先后不决定终态。遵守单写通道处理顺序，不为取消强行抢占在途事务；排队在先的合法完成可能胜出，取消先胜窗口通过先完成取消提交、旧完成候选后到达体现。Run.TerminalOutcome只有None/Completed/CompletedWithExceptions/Cancelled；最终值不可改写。Coordinator仍是业务候选及内存状态唯一所有者，Writer只执行条件与原子约束，不自行决定工艺。

- 完成批次：条件为Run.Revision=ExpectedRevision、TerminalOutcome=None及原完成证据仍适用；事务内条件更新Run并递增Revision、插入唯一Handoff（绑定相同终态/revision）、保存WriteId提交结果。任一步失败全部回滚。
- 取消批次：必须已有可靠停止/无需停止的物理核对与必要保存证据；相同revision/None条件且无Handoff，事务内将Run设Cancelled、递增Revision，并保存取消命令最终结果和WriteId。取消请求审计可先保存为Pending；它不等于最终取消。
- EF模型/初始迁移必须表达Run终态枚举及RunState/TerminalOutcome一致约束、Handoff.RunId唯一及与Run的(RunId,TerminalRevision,TerminalOutcome)关联约束；Handoff只允许两类Completed值，TerminalRevision终态后固定；所有终态写只走上述事务，条件更新受影响行必须为1，禁止先插Handoff后独立改Run。一般状态保存也不得覆盖既有终态；拒绝审计不修改Run/Handoff。该原子合同由T020/T022实现，T036/T044提交候选。
- ConditionRejected时读取实际Run/Handoff/Write记录：已有终态则投影该终态；仅revision变化且仍None时重新核对证据，明确以新WriteId/当前revision生成候选，保留原拒绝记录。不得修改原WriteId载荷，不得在CommitUnknown时重投竞争候选或盲目提高revision。
- 取消一经受理立即锁存cancelRequested、关闭新动作/继续准入，必要停止走独立控制路径，不等待写者。既有完成候选可能先提交；这时取消最终为NotApplied(AlreadyCompleted)，不会撤销已发停止或释放物理占用。
- 任一竞争终态批次CommitUnknown时，terminalResolution=CommitUnknown，停止/查询仍可用；按原WriteId核对，未排除在途提交不能靠“尚查不到”判失败。核对到确定提交/回滚/条件拒绝后才解析或再生成终态候选，不能同时宣布Cancelled和Ready。
- 命令查询对已持久Pending取消记录可依据Run的已提交完成终态确定NotApplied；该审计回填失败另报保存状态，不把已完成Run显示成Cancelled或永久待裁决。完成已提交的迟到回执只确认原完成事实；取消已提交后旧完成写必须ConditionRejected。重复/旧回执不重写更高版本投影，不触发动作。Run/Handoff不一致视为存储异常并受限，不能任选一个修补终态。

| 取消到达窗口 | 即时处理与最终裁决 | 中断/重启核对 |
| --- | --- | --- |
| 移交已排队、未提交 | 立即关准入/所需停止；不承诺撤销已排队批次。完成若先条件提交则完成胜；若取消先满足物理/保存条件并提交则旧完成被拒绝；入队顺序本身不是完成事实 | 核对两个WriteId、Run终态及Handoff；无终态则保留Pending/RecoveryRequired，不自动发动作 |
| 移交已提交、回执尚未到Coordinator | 条件取消不能覆盖已完成；查询实际Run/Handoff后取消NotApplied；原完成回执晚到不改变结论 | 重启直接读同一已提交终态/移交；取消审计待保存如实显示，不撤销完成 |
| 移交CommitUnknown | terminalResolution=CommitUnknown、cancelApplied=null；停止照常，暂不提交竞争取消终态 | 先核对原WriteId；完成已提交则NotApplied；确认未提交且无旧写者后才按当前revision与物理证据裁决取消 |
| 取消已最终提交后旧移交写/回执到达 | 原None/revision前置失败，不插入Handoff；旧回执按WriteId/实际终态核对隔离，保留Cancelled | 读到Cancelled须无Handoff，继续/完成均拒绝；不得复活 |
| 任一窗口进程中断 | 不以退出表示停止、取消或提交失败；保留所有已提交命令/写入证据 | 启动先核对Run/Handoff/Write与取消命令；未终态且已持久取消请求保持关准入并恢复取消核对。仅内存受理但未保存的请求明确不可证明，运行仍RecoveryRequired，须授权重提请求，不能自动继续或自动补造取消 |

内存/API以已核实的持久终态为权威。未裁决时可展示CancelRequested/StopPending与terminalResolution=Pending/CommitUnknown、finalOutcome=None、Handoff=Saving/NotReady；不得给出最终cancelApplied=true。已完成则保持Completed/CompletedWithExceptions与Ready，另列停止/审计状态；已取消则Cancelled与NotReady。查询发现更高持久版本时交Coordinator更新投影，不让API成为第二状态写者；读取等待无法核实时明确Pending/不可用，不用旧快照断言相反终态。

SQL/媒体实际写入放在独立有界I/O执行通道；Microsoft.Data.Sqlite可能同步执行I/O，因此不能在流程/心跳线程直接调用“Async”方法后假定安全。写失败/超时停后继，停止/取消仍能处理。

## 2. 媒体所有权与容量

MediaStore唯一拥有文件和缓冲；采集一次转交，算法/归档通过租约共享引用。文件逻辑路径拟定为media/{runId}/{captureId-or-callId}/{mediaId}.{format}，不使用外部码或虚构Part/Face目录。拒绝绝对路径、路径逃逸、符号链接跨数据根及用户可执行内容；只有注册格式可读取。

开发限额见budgets.test.json：内存64MiB，单3D最大16MiB、F最大4MiB，运行媒体64MiB，测试数据根256MiB，最小剩余空间512MiB；F保留4MiB内存，触发前另预约其文件预算，3D不能耗尽它。示例不是生产点云容量。容量不足禁止新采集，不在SDK回调等待。

采集缓冲只有在完整文件保存或各实际消费者不再访问后可释放。落盘算法文件租约到InputReleased或进程退出才释放；进程失联不可释放。释放租约不删除需保留的原始媒体。当前功能不自动清理已提交证据；未引用临时文件仅在恢复核对确认无在途消费者、保留诊断且满足明确测试留存策略后由受控准备/清理入口处理，Host不因容量不足静默删图。

## 3. 中断核对矩阵

| 间隙 | 重启观察与核对 | 允许行为 |
| --- | --- | --- |
| 意图未提交，未投递 | 无可靠动作证据；运行原始记录与设备安全核对 | 可证明未发起后，授权continue从该步骤开始 |
| 意图已提交，发送标记未知 | 可能已发，不能凭缺发送日志认定未执行 | 动作Unknown/Held；匹配设备/人工核对，禁止重发 |
| 设备完成但事实未提交 | 原Action反馈/设备会话/历史观察与持久意图 | 有可靠证据可幂等补存；无证据保持未知，不再次动作 |
| 内存接管后进程丢失 | 只有Capture意图，无完整文件 | 记录MediaLost及未满足必要保存；不伪造媒体/自动重拍 |
| 临时文件未完成 | temp及未完成记录 | 不供算法使用；隔离待核对，不记Ready |
| 完整文件已完成、元数据未提交 | 成品文件及manifest关联Run/Capture、大小/格式/一次校验依据 | 在受控核对中幂等登记；不能把其他运行文件借入 |
| 元数据已提交、必要文件缺失 | 引用指向不可访问文件 | SaveFailed/MediaMissing，禁止移交，不伪造完整 |
| 保存提交结果未知 | 原WriteId及事务记录 | 独立查询提交事实；重复保存只允许同WriteId同内容，不重做物理动作 |
| 算法意图提交前、已提交未派发、已派发结果未保存 | 按§1.1三窗口核对WriteId、Call、输入及派发依据；无发送记录不证明未发 | 原Call终态/未发起步骤按§1.1区分；未知不重算/重拍，必要保存及CL-02核对后才继续 |
| 算法终态已保存 | 原Call及输出/异常 | 复用，包括TimedOut/NotIntegrated；迟到仅附加 |
| 移交/取消各提交与回执间隙 | 按§1.2五窗口核对Run/Handoff、两类WriteId及取消命令 | 唯一持久终态投影；未知不双终态，无自动动作；已存移交只查询 |

复用步骤须同时具备：原运行/冻结快照及版本、匹配动作与采集结束依据、所有必要媒体可用及元数据、相应算法允许终态、提交回执；加上授权同盘/装载/设备安全核对。部分证据不能当作整个步骤已完成。所有原异常、晚到证据和人工核对均留痕。

## 4. 开发测试库准备与维护互斥

后续实现测试工程内非正式Host的StorePreparation入口，只面向显式Test数据根执行Inspect/Initialize/Verify，不承担完整生产升级、搬迁、备份平台。复用Infrastructure EF映射及初始迁移，不维护第二套手写建库SQL。

准备时先验证绝对目标在指定测试根、用途清单匹配；获取以规范化数据根为目标的OS独占文件锁（Host也持有相同锁）。Host未退出/读写连接未释放则拒绝，不能靠UI状态或仅检查无写事务。不同路径别名必须归一，禁止两个目标指向同一库绕锁。

Initialize仅接受显式空测试目录；发现已有库不覆盖、不删除，转Inspect或返回AlreadyExists。首次空库不要求旧库备份。准备入口应用固定初始迁移、设置WAL/同步持久化策略、写StoreManifest、核验结构/关键约束并输出准备结果；失败保留现场且不标可用。以后需要升级现有库必须转独立维护方案，先一致性备份、核验版本与维护互斥，本功能不生成通用Upgrade/Restore实现。

Host获得运行锁后先用只读模式检查数据库已存在、manifest/迁移历史与s01-store/1一致、关键表/约束兼容；通过才以ReadWrite（非Create）开库。缺库、错误路径、过新/过旧结构、未完成维护均进入StoreUnavailable/诊断，不调用EnsureCreated/Migrate。每连接设置必要foreign_keys等连接选项不等于允许改表。

停止Host需关新准入、核对机械、收敛必要写入并关闭所有查询/写连接后释放锁；后台重启遇锁仍拒绝访问。开发准备不连接设备/Worker；本次只描述步骤，未执行。未来隔离测试库操作属于内部验证，不借此恢复合同验收或生产维护范围。

## 5. 移交数据合同

`IStageHandoffQuery` 保持只读。`s01-handoff/1.0` 保留兼容查询，其既有字段如下：

| 字段组 | 最低内容 |
| --- | --- |
| identity | handoffId、runId、request/context、主体、时间、source/purpose；Part/Face等未建立 |
| configuration | public/point/scope/budget版本和snapshotId；模拟时simulation版本、clockMode及clockId/事件证据引用 |
| threeD | captureId/callId、scope、实际媒体、原始响应、有效samples及单位/基准/来源，或技术失败/无效状态；无伪造Z |
| f | captureId/callId、单帧媒体、responseReceived、rawCandidates/去重结果、主码或冲突、解析字段及来源/限制 |
| physical | 启动/按钮/夹紧及两次XY匹配证据，最后已知设备/资源状态及观察时间/来源 |
| recovery | 实际正常暂停Check与复用/补存，或故障reset/initialCheck和新旧run关联；无恢复则空，不造记录 |
| limitations | 异常、缺失信息、继续依据、OPEN/S01限制 |
| completion | Completed/CompletedWithExceptions、Ready/ReadyWithLimitations、提交writeId/revision；Recipe=Unmatched、Quality=NotEvaluated、Sorting=NotStarted、WholeTask=NotCompleted |

不输出虚构信息，不将nullable字段默认为成功。v1 的 HandoffReady 是保存确认后的兼容查询/通知状态，不自动松夹、卸料或释放设备级占用。

### 5.1 `s01-handoff/2.0` producer 扩展

2026-09-23用户有限授权第一工位连续主流程使用 v2。v2 不删除或静默重解释 v1；producer 在完成启动夹紧、3D、F、冻结 `RecipeRunPlan` 和正式配方绑定后，复用单写者短事务原子提交不可变 handoff。除 v1 实际证据外，v2 必须保存：

- 同一 `runId/trayId/stationId/lineId`、场景和占用对象；
- 冻结公共配置、点位、budget、purpose/source 和能力摘要；
- 3D/F 媒体、结果、唯一 F 码及证据引用；
- `RecipeRunPlanRef/PlanRevision`、配方绑定引用和载荷摘要；
- `writeId/revision/persistedAt/source/quality`。

只有 v2 提交成功且身份、计划与引用完整时才发布 HandoffReady，并由同一 Host 内部 003 consumer 自动续接 Detection；入队、内存通知、回执等待或任意非空 ID 不能触发续接。v2 handoff 是非终态边界，不把 Run 标为 `FinalUnloadCompleted`，不生成检测、分拣、解锁或人工确认事实。重启只消费已提交 v2，不能重放公共准备物理动作。相同 writeId/identity/payload 幂等返回原记录，同 key 异内容冲突。


## 修订记录

- 2026-09-20，1.1：按用户明确决定修正H01/H02，增加AlgorithmIntent派发门、原期限关系及运行终态条件事务和五窗口核对；范围、OPEN及需求编号不变。仅文档设计。
- 2026-09-23，1.2：按有限授权增加 `s01-handoff/2.0` producer 扩展和提交后同 Host 自动续接规则；v1 保持兼容，未宣称代码或测试已完成。

## 2026-09-24 008完整执行合同增量

适用优先级：本节及008目标合同用于最新需求实现，前文冲突条款仅在本节明确的历史样本范围保留；本轮未改代码或声称协议缺口已关闭。

沿用003 s01-handoff/2.0非终态交接。008扩对象/真实物理槽/点位及高度来源引用，禁止将0,0占位写成物理源坐标。公共原始高度保留单位/基准/范围/轮次；产品适用映射在绑定后明确，未提供不得猜测。F唯一绑定及冻结证据不能由客户端构造。

统一来源：指定分区PLC协议§3.1.7、REQ §7/11、宪章6.0.0；执行/结果/证据分别见008 contracts/execution.md、api-results.md、evidence.md。关联实现任务见本功能tasks中的008对齐增量。

当前共同移交：启动受理保存expectedRecipeRef选择意图；F料盘编号在绑定时唯一解析实际保存的当前配方，选择身份不符保存受限原因，不提交可续接handoff；旧展示版本不强制使用过期内容。v2引用已提交冻结计划、物理实体/sourceSlot/用途pointRefs、姿态观察及配置单位/基准；旧记录只按原字段只读查询，不增续跑旁路。公共3D使用公共配置，F XY来自本次首次3D；冻结及结果保留各组件实际来源。生产者001 T090、消费者008 T052，API由003 T068承接。

第四批接线：严格消费者实际解析冻结位置负载中的每相机显式目标和独立源点，校验pointRef、对象/槽位/面/轮次/相机、协议槽号及坐标系；当前仅允许已有明确依据的`ApprovedFixed` Z，未提供点坐标、源点或适用高度依据分别拒绝。公共3D原始样本不在消费者内自行换算，组件Test注入XYZ不写入v2公共移交或正式配方。该解析路径不解除Q01/Q02仍Restricted的目录准入，也不代表T090整项完成。

### 008第八批当前轮目标增量
当前移交消费已提交首次3D有无/姿态与F定位，检测XYZ取冻结配置；后续相关对象翻转放回后统一3D姿态复查，复查事实不替代配置Z、不重绑F。旧Q03高度轮次与当时证据只证明原版本，不作为011坐标依据。


## 2026-09-26直接接口增量（目标，代码待实施）

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。
唯一实现归属：specs/001-station01-public-preparation T090移交，specs/002-plc-xyz-recipes T11模型/目录/规划校验，specs/008-recipe-driven-inspection T052/T060执行消费。

## 009 / AL01 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

运行/冻结配置、启动/运动意图及各原必要结果先真实提交，再派依赖动作。三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

非终态s01-handoff/2.0行存在不是当前续接许可；PublicPreparationHandoffV2消费者还须本次不可变有效RecipeBindingReceipt及当前准入（定义见011 RC05.1）。ReceiptObserved由提交后有界语义Audit记录，原事务不预填自身未来回执时刻，Audit不递归审批。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。窗口包含这次意图后设备、raw和绑定事实；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 010实施定向对齐 A02/A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。
- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
