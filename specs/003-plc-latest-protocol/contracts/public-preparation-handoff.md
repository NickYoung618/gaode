# 001→003 持久化 handoff 合同

016同步：Identity.OccupiedSlots保留原启动值并可为空；实际占用取已提交完整3D与F后的显式配方映射及冻结计划。其余交接证据、绑定、保存和摘要校验保持。

## 版本与语义

本合同由 003 定义为 `s01-handoff/2.0` 消费扩展，不静默改变既有 `s01-handoff/1.0`。handoff 是不可变提交事实，不是动作命令；`HandoffReady` 仅表示可进入 Detection，不表示第一工位完成。

003 可以独立实现消费者、领域规则、存储端口和合同测试。2026-09-23用户已有限授权只对齐 001 的 v2 producer 直接相关 spec/contracts/plan/tasks，并在共享合同对齐后实施 producer/consumer 接线；该授权不扩展001其他需求，也不涉及006或客户原型。授权本身不等于接线完成或测试通过。

## 必需内容

handoff 至少包含同一 `runId/trayId/stationId/lineId`、场景/占用对象、冻结公共配置/点位/budget/用途/能力摘要、3D/F 媒体和结果引用、F 唯一码及绑定证据、冻结 `RecipeRunPlanRef/PlanRevision`、source/quality、writeId/revision/persistedAt。

007虚拟集成沿用此内容：公共3D/F从固定图片经正式采集入口形成各自本次媒体，独立worker实际处理对应算法请求；图片清单/摘要、Test预算/配方版本、worker版本与随机配置作为冻结引用留证。F合法唯一结果须来自实际调用，经正式配方加载、校验、计划和绑定后再提交handoff；固定目录图片、worker日志或预造计划均不能代替已提交媒体、算法结果和配方快照。同一runId的handoff由Host自动续接逐计划检测，不新增客户端第二次启动。

## 生产与消费

Host 在完成公共准备、生成计划、正式绑定并保存证据后原子提交 handoff。提交成功前不得进入 Detection；提交成功后同一 Host 内部编排器自动消费，无需第二个外部 API。

消费者重新读取持久化 handoff，核验当前资源租约、配置快照、料盘身份、计划版本和引用完整性后构造 DetectionRequest。通知、内存状态、测试脚本拼装或仅非空引用均不能替代提交事实。

## 幂等与恢复

相同 writeId/identity/payload 返回既有 handoff；同 key 不同 payload 冲突。重启从已提交 handoff 恢复，不重做已经提交的公共准备物理动作。handoff 后若 PLC 动作可能已派发而终态未知，遵守 UnknownHeld，不通过重新消费 handoff 重发。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

继续使用s01-handoff/2.0非终态信封和持久引用，不由脚本预造。启动先保存expectedRecipeRef；公共F使用首次3D定位取得真实料盘编号，在绑定时一次读取当前已保存配方，与选择身份相容后冻结产品计划；不按启动前目录版本锁住后续F，不在业务合同暴露原码。

ObjectBinding、检测配置目标、用途点位、姿态检查轮次和预算来自冻结对象集合；禁止从SortUnit猜应检集合或以0,0补位置。S3部位归原Assembly，质量/检测目标与搬运单位分开。具体见[008数据模型](../../008-recipe-driven-inspection/data-model.md)；新活动生产者/消费者一起更新，旧保存和证据不补写。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。

当前修正：协议§3.1.5的“到位后写目标面”已定义正常触发。发令前后快照、连接代次、新反馈观察、旧完成排除与双重完成核验由Host实现；不要求新增序号字段或固定清零序列。仍未配置的设备接口仅是总时序图要求的取料/放料两组坐标如何映射到协议字段及提交顺序。在此输入明确前，正式Flip派发和后继保持受限；组件Test阶段不解除限制。当前实测及读回见008 [第八批证据](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。


## 2026-09-26直接接口增量（目标，代码待实施）

逐面续接保留同盘/F绑定、对象及已发生结果；本轮相关对象完成翻转和放回后，统一3D姿态复查，再让正常槽位进入下一面。检测XY和检测Z来自对应配方/点位配置，不依赖旧3D高度；执行阶段、检查轮次、物理槽位和连接代次分别关联，不能复用上一面目标或伪造观察。具体目标schema见008 contracts/test-virtual-mapping.md；历史二次3D组件事实只读保留。
唯一实现归属：specs/001-station01-public-preparation T090移交，specs/002-plc-xyz-recipes T11模型/目录/规划校验，specs/008-recipe-driven-inspection T052/T060执行消费。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

同Host自动续接只在本次所有必要回执及时有效后；已有handoff行只证明保存事实，不能让Query/后段在迟到、取消或重启后重建Bound/Ready资格。保留当前run/tray/plan/对象/面/初始测量/源槽及参数快照；独立API本次绑定意图与事实需保存但不重建旧handoff。当前有效早到回执不因稍晚调度倒判，下一动作仍核原截止/取消/安全。ReceiptObserved有界后置审计不预填自身回执、不递归授权。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。窗口包含这次意图后设备、raw和绑定事实；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。

### 010实施定向对齐 A03（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
