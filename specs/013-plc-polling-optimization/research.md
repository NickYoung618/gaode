# 技术研究与决策

> 当前验收解释：2026-10-05需求方批准 **013-acceptance/2**，详见[验证合同V06.2](contracts/verification.md#acceptance-v2)。普通25ms等新增工程目标改为非阻断观察；原期限/保护、局部首态75ms、确认周期、单源/计划、预算/净收益及证据完整性仍硬。原数字和历史失败保留。下文历史“全部V06成立/不得倒改门槛”以本次显式批准范围解释，不能据此修改硬条件。


**013 / 2026-10-04 / 只读源码与合同研究**。没有构建、运行测试、连接设备或操作运行数据库。按speckit-plan分派信号预算、观察合同、时序验证三项研究，以下为汇总结论。所有数字区分源码事实、设计标准和未验证外部能力。

## R01 主项目基线与最小范围

**Decision**：以E:/dzk/gaode-1当前文件为基线，保留未经产品修改的源码/构建身份后才做对照；没有Git元数据，不虚构分支/提交。保留011/012实际集成与现有失败限制。

**Rationale**：011 main-project-integration-20261004.md、verification-report.md记录既有集成及multi01/02暴露的受理/坐标问题；旧成功不能当013性能基线。用户只批准必要采集优化与一条代表链。

**Alternatives considered**：旧工作副本覆盖、沿用旧multi03计量、全套历史专项均不采用。源码研究使用当前主目录，历史报告只作来源。

## R02 固定计划在准入时准备

**Decision**：冻结映射及有限PreparedPlanSet，绑定设备准入实例/摘要，热路径只执行准备计划。

**Rationale**：[PlcSignalAccessor.cs](../../backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs)每次ReadAsync重新ReadPlan/ValidatePlan；[ProtocolDefinition.cs](../../backend/src/Gaode.Plc.Protocol/ProtocolDefinition.cs)已有连续合并/合法性校验可复用。准入已校验仍每轮排序不提供额外有效保护。

**Alternatives considered**：直接删校验会丢非法映射拒绝；按组名全局缓存会误用其他定义；任意字段动态缓存会保留重复准备/兼容旁路。采用有限用途预编译，定义变化重新准入。

## R03 信号归属与连续块

**Decision**：H一块、B六块、P两块、F/U各一块；T为反馈加完整P三块，期间暂停独立P。轴反馈由B提供，动作共享；首中间态局部快读时B让出同一块。

**Rationale**：[Signals.cs](../../backend/src/Gaode.Plc.Protocol/Signals.cs)当前44字段及HostReadable决定合法范围；Semantics.AxisMotion使用全部轴状态/触发，不能只留当前轴。现Stages.ReadStageSample每次取放反馈仍核XY/GrabZ，T不得省略。完整P同两块附带Camera/Scan四word，不增事务，可直接复用现完整位置对象和消费者。

**Alternatives considered**：仅XY/Grab读取再额外残余位置源会增加四事务/s并引入逐轴共享对象；跨空洞/保留区减少事务无读取依据；块读视为原子快照无协议承诺。T执行先反馈后坐标，以保留完成因果。

**Result**：空闲名义18.333、轴运动38.333、翻放回完成等待43.333、取放49.333事务/s。源、公式和按需预算见验证合同；均不是测量或正式能力。

## R04 只改适配器内部协调

**Decision**：一个采集协调器、每组单轮/到期位、原串行动作执行器、两连接不增；周期从实际首PDU开始，跨周期不追赶。关键读写在PDU边界插入。

**Rationale**：[LatestProtocolPlcDevice.cs](../../backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs)的Pump、Reset、AdvanceStart及CompleteInspection与动作partial共享PollMs；011已修及时串行受理，不能改回慢周期派发。慢读不应占整轮锁导致原受理超时。

**Alternatives considered**：仅把PollMs变200/500会同时放慢心跳/命令、保留重复采集；多个独立无限循环会争线/积压；增加连接或通用调度平台超范围。版本化通知替换重复缓存唤醒，个人取消不干扰其他合法消费者。

**DQ-01增量决策**：A03.1区分d/q/每块s、e/块间g/完整发布p，C=Σ交换+Σ块间等待+末次发布开销，年龄按最旧依赖。A03.2使用固定K/R每PDU轮转、B/当前反馈轮转、首态快档只插一块、P等待两个普通R槽后获得一块，所有权切换撤销旧未发块。A03.4列阶段互斥、有限K突发、实际服务容量及5ms槽的有限示例。保留25ms单PDU和V06整轮目标为独立联合判据；25ms全占满时T/首轴/首翻放业务需求112.5%/122.5%/135%，因此不能从平均率或单次上限证明短态捕获。75ms必须含真实竞争/交换/发布；示例5ms不是新门槛或硬件承诺，实际能力NotRun。不选择新增连接、通用调度器或降低25ms数字来使推导成立。

## R05 独立观察时间与最小共享输出

**Decision**：通信内部保留每块时窗/代次/可靠性；复用Domain已有PositionObservation.Identity，顶层只代表base；Host位置加四个语义元数据字段，Axis投影改用位置Identity。

**Rationale**：当前ProtocolSample共同ObservedUtc可使新安全状态给旧坐标续期；[AxisObservationProjectionBuilder.cs](../../backend/src/Gaode.Domain/Station01/AxisObservationProjectionBuilder.cs)当前顶层时间与拆组不相容。Host DevicePositionApi只有ObservationId不足表达独立年龄。业务无需知道采集组；现页面Axis行已消费时间/可靠性。

**Alternatives considered**：给整个DeviceObservation续新时间错误；普通坐标过期直接断设备会把1000ms监视变成误失联；扩大Domain为协议分组模型破009且无必要。保持原失效阈值，必要准入即时新读。

## R06 到位与动作完成不能等慢监视

**Decision**：轴仍先Moving后Arrived再实读坐标；非最终XY一块、最终批完整P两块并发布，再宣告完成。采集释放使用按需新base。

**Rationale**：[LatestProtocolPlcDevice.Axes.cs](../../backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Axes.cs)保留到位后实读和最终新后台样本；RecipeDetectionExecutor在完成后再次读取用途位置。仅改Evidence而不发布新位置会让共同业务继续拒绝完成；等500/1000ms会徒增流程延时。

**Alternatives considered**：旧缓存、写入目标当实测、用XY实测代替用途Z、删完成后消费者保护均拒绝。两个坐标块仍不是同时原子值。

## R07 短态局部例外有明确来源

**Decision**：命令后首次Moving（同批全部当前轴）/Executing观察前立即读并50ms采集；见到后恢复200ms。只此阶段，唯一来源，不改变模拟时序。

**Rationale**：当前代表run-2为Motion500、Flip3000、PutBack150、Sorting3000ms；协议组件有轴150及Flip/PutBack80ms。VirtualPlc独立scan才启动动作，Modbus写ACK可以早于Executing/Moving，所以一次即时读取加200ms仍可能漏完整短态。取/放没有原先必须Executing的要求，不能新造。

**Alternatives considered**：全局50、删除中间态保护、把旧完成算本次、拉长模拟动作均不允许；仅立即读一次不足；等待PLC正式资料阻断全部软件优化也无必要。局部快档实际≤75ms有效间隔为待验证目标，不承诺未知正式PLC保持时间。

## R08 心跳与期限独立

**Decision**：H300ms、独立连接、独立原3秒截止监视；读返回先核截止，再处理初值/翻转。每个等待和后继派发重新校验原Window/取消；I/O入队即计时。

**Rationale**：当前HeartbeatAsync在读后更新edgeAt再检查，慢读可能掩盖已过原点；降频不能保留这一顺延风险。初值同步沿现行为单独建立基准，不算实际翻转；会话启动先有3秒截止。A05细分初始化、Reset和迟到值。

**Alternatives considered**：读结束才重新起3秒、成功读旧值续期、排队后另给完整I/O、保存后重新起动作期限均拒绝。正式端到端翻转延迟需要设备侧时基，不能用Host采样代替。

## R09 保留真实证据，以源头减量

**Decision**：保留Recorder/Reader、原始plc-evidence/1、8192窗口、gap、分段接续和真实提交；Evidence引用实际反馈/坐标观察集合。正常计量用有界计数/聚合。

**Rationale**：现CommunicationEvidenceRecorder、FailureEvidence、Stages和Acquisition已有真实提交与分段；取料完成与IPickCommit是分离事实。减少源头事务自然减少解码/序列化/证据，不应把必要保存变为后台投递。

**Alternatives considered**：只压日志/删除原始证据、重构数据库队列、全进程性能平台及GC/线程池调参超范围。SC-006选择直接指标，不要求所有类型/常驻内存下降。

## R10 最小验证与耗时判据

**Decision**：每侧5秒预热后60秒空闲一次、相同run-2完整链一次；必要保护组件补齐，不加全配方矩阵。按合法块、按需事件和108固定核心业务写计算预算；固定时效和4950ms流程回归容许量见验证合同。

**Rationale**：静态路线21轴批/33轴启动、翻放回各1、Pick/Place各1、7次媒体捕获，足以覆盖本改动。每秒请求可能因运行变慢减少，故总事务/完成量及原截止必须共同审查。4950ms来自25个完成节点的周期差和23个首状态节点调度余量，是先定的工程容许量而非数学保证或实测结果。

**Alternatives considered**：指定无依据降幅、要求每次固定写都减少、仅验证配置值、无限重复全链取最好、旧日志基线均拒绝。

**DQ-03增量决策**：V01.1将验收分为两侧共同资格、仅after的新周期/预算/时效/单源语义、跨侧净收益及4950ms；旧基线不要求1110、75ms或循环零准备。V09.1使完整L/受影响009/013组件在最终after身份执行一次引用，两侧一窗一链各用自身DLL/schema/config。V10分别记录保护失败、优化目标未满足、证据支持的不可比、NotRun和正式信息不足；after超限不能无依据改名环境问题，也不自动扩大环境治理。

## R11 验证入口和真实阻塞边界

**Decision**：后续新增明确PlcPolling013选择并保持完整010 L；013必需清单通过原发现/执行核对。代表链用同一中性计量/真实只读API观察接线，两侧一致。

**Rationale**：[runner.py](../../scripts/workflow/runner.py)、[verify_entry.py](../../scripts/workflow/verify_entry.py)当前未知profile回默认009，不能直接把拟议命令当已有能力。[010-lightweight-cases.json](../../scripts/workflow/010-lightweight-cases.json)现70行覆盖27 dotnet/7脚本/36自校验，不能缩为少数Rules。RecipeExecution010RunHarness有外部旧012页面观察目录默认和实际ready等待，当前主目录不能凭空调用缺失观察器。

**Alternatives considered**：假ready、取消全部观察/保存、绕过L、重跑完整010动态专项均不采用。013只读API观察仅承担本次后端负载，不声称原页面验收；环境缺失限制对应完整链运行结论，非软件设计阻塞。

## R12 清理与历史区分

**Decision**：替代后删除旧PollMs万能参数、重复循环/缓存等待/动态计划热路径及错误测试预期；每项有效保护映射到新来源。按plan SY-01—07先文档后共享代码。

**Rationale**：PlcRuntimeOptions校验PollMs≤50、Station01Registration Test50/其他25会覆盖新目标；StagePollMs/辅助等待和未用BusinessDurations.PlcPoll需定向核查。HeartbeatFlip仍在FullSimulation被用，不能一起删。脚本当前说明可改，旧证据/任务勾选不可改判。

**Alternatives considered**：保留可切回旧循环的兼容旁路、全仓替换50、重建012配方文档均不采用。

**DQ-02增量决策及源码依据**：当前001 budget.schema.json以const 1.1要求plcPoll；BusinessDurations仍声明该字段，ConfigurationLoader在SchemaRoot校验后严格反序列化。PublicConfigurationValidator和RecipeApplicationCoordinator.RequireBudget均硬校验1.1；前者还核模拟budgetRef。故仅删JSON不闭合，选预算schema **2.0**、s01-budget-011-joint **2**、s01-sim-011-joint **2**（模拟schema仍1.0），对应fixture两引用也升2；其他业务/模拟字段不变。V02.1给出固定两侧路径和摘要映射；原基线保留1.1/version1，优化产品只准2.0新运行，无双版本执行兼容旁路。原RecipeApplicationHistoryReader读取已存BudgetSource的历史职责不变。

Harness.Local约束fixture目录树，FindWorkspace从各侧测试DLL上溯；配置/媒体必须在各自输入树，before不能误用主目录新schema。ConfigurationFreezer/ApprovedExecutionCostProvider正常生成不同的预算、模拟及快照摘要，比较语义而不伪称原字节相同；中性计量补丁与配置迁移分别留摘要。001现行预算合同及实际消费者按plan清理顺序先文档后代码，未受影响的012消费者沿用。此轮仅写013方案，未迁移任何配置或共享代码。

## 未交付外部信息

正式地址/保留区合法读取、正式心跳翻转周期、轴/翻/放回最短中间态保持或锁存承诺、设备侧可校准时基仍缺。来源文档只读，仅提出有利连续读取和反馈保持/锁存的布局建议，不编造地址/ACK/轴含义。局部软件设计明确，正式PLC捕获与性能结论保持受限；所有运行验证NotRun。
