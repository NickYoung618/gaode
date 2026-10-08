> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。第一工位移交前仍无配方调用。

# 公共配置、模拟延迟与期限合同

020阶段B当前增量：RealDeviceCommissioning公共/预算schema、六项RecipeExecution额度和StoreManifest.Profile已接入，同一正式协调器使用真实PLC/七机及显式模拟算法/灯。联调快照另存CommissioningJson/Digest，不加载Test SimulationProfile；RunPurpose.Commissioning对应新用途。旧Test/Production行为和下文历史设计保留，精确引用/来源/机械坐标依据见[MC-020](../../020-real-device-commissioning/contracts/mixed-runtime.md)，实际边界见[验证](../../020-real-device-commissioning/validation-stage-b.md)。

**版本**：s01-config-time/1.1　**状态**：设计资产，未执行。  
依据：FR-003至FR-006、FR-014/019/027/028/029、FR-033至FR-040，CL-01至CL-06，P03–P11。

## 1. 配置结构与校验

机器可检查结构：[公共配置schema](public-config.schema.json)、[预算schema](budget.schema.json)、[模拟schema](simulation.schema.json)。示例索引见[examples/README.md](../examples/README.md)。结构校验之外必须执行下列语义校验：

1. ID/版本在受控配置根解析；拒绝任意路径、URL、脚本、未知属性和不兼容schema。加载取得一致内容，规范化并计算一次摘要；ID/版本相同而内容不同视为版本冲突。
2. 公共3D采集配置与F扫码Z按单位/坐标系/版本/范围校验；首次3D给出F XY后再核对其合法性，不在启动前要求尚未产生的定位值。公共采集范围有据，不推定单曝光覆盖。
3. 绑定ID唯一且角色正确；capture3d/F关联对应相机与光源。能力ID+contractVersion必须在Host注册表；capability元数据声明输入/输出契约、所需资源、支持用途及schema版本。
4. motion/capture能力或必需参数错误为blockingControlErrors，包含F扫码所需静态配置在内必须在PLC启动前通过；运行时F XY在首次3D后校验。算法/解析引用缺失或不兼容单列algorithmIssues；不执行未知算法，调用时形成NotConfigured/InvalidResult等明确终态，不恢复算法启动门槛。整个JSON无法解析时不能提取可靠运动配置，按公共配置错误处理。
5. F固定frameCount=1、automaticRetry=false；stopAfter=PublicHandoff、qualityState=NotEvaluated不可配置成其他行为。策略不得新增回零、重拍或Z扫描；F绑定只由共同业务入口执行。
6. Test用途、Simulated绑定和模拟原点不得用于任何真实动作；Production必须有正式配置及能力依据，不能只改purpose字符串放行。Real设备与Controlled时钟组合拒绝。Provider在Host安全停机/维护且无未知占用时装配，运行中不切换。
7. 预算以ms为单位、正且有限；算法预算可明确为null并记录NotConfigured，不能发起无界调用。延迟为非负整数；delay>budget合法。不响应使用NoResponse而非无限值；Fail必须有Failure及错误码，Respond不能配Hold。重复次数/偏移有界。
8. 当前运行冻结公共、预算、模拟配置的完整规范化快照及注册能力版本，不只记路径。后续改文件或回滚版本不影响在途运行。已冻结快照不可被continue替换。

注册策略为Application的ICapabilityPolicy（校验、生成受限操作描述、解释结果），本阶段只注册公共3D有无/姿态/F定位、F单帧、原始候选和可选字段解析策略；Host组合根显式注册。策略不能取得PLC/数据库/文件句柄，操作描述必须经Motion、Acquisition和保存边界执行。新增能力实现相同接口和契约测试再注册；现有能力内换点位、绑定/参数只改配置，不改Station01Coordinator。公共准备不读取未绑定产品配方；观察输出真实槽位及定位，后续由共同F绑定与计划消费，不伪造产品身份。

## 2. 七环节耗时与预算起点

下表数值全部来自团队定义的s01-budget-dev/1.0.0及s01-sim-normal/1.0.0，仅用于开发测试。

| 配置键 | 模拟延迟起点 → 正常响应 | 正常delayMs | 业务预算起点及budgetMs |
| --- | --- | --- | --- |
| plcAcceptance | 模拟设备收到启动/移动命令 → 受理 | 80 | Host投递对应命令之前注册，300；排队/传输也计入 |
| clampCompletion | 模拟安全满足且实体按钮输入发生、进入夹紧执行 → 夹紧完成 | 250 | 匹配本次实体启动的入站事件，1500；未知按钮/夹紧关系不猜测 |
| xyCompletion | 本次移动已受理且独立设备模型进入Moving → 到位 | 500 | 对应移动投递时起总期限1500，包含受理等待；3D与F分别计时 |
| capture3d | 合法触发进入Capturing → 采集结束及媒体移交 | 400 | Application受理本次采集请求、任何灯光/SDK等待之前，1500 |
| heightAlgorithm | 算法替身受理调用 → 原始高度响应 | 600 | Application登记Call并提交调度之前，1000，包含AlgorithmIntent保存、队列等待 |
| captureF | 本次唯一F触发进入Capturing → 单帧及采集结束 | 150 | 受理采集请求时，1000；不得迟到就再拍 |
| fDecode | 算法替身受理本次图像调用 → 原始候选 | 200 | 登记Call并提交调度之前，700，包含AlgorithmIntent保存、队列等待 |

AlgorithmIntent采用独立关键提交，登记原期限后提交，明确Committed且当前时刻<dueTick才派发。算法窗口在保存期间到期也由同一DeadlineScheduler/OperationIngress收敛；晚提交只补保存事实，不重新派发或重置预算。保存Failed/CommitUnknown时即使算法终态已收敛也不得绕过必要保存门。正常测试必须让保存等待与响应总耗时落在原预算内，不自动放宽示例数值。

模拟事件计时和业务计时可能起点不同，但每一条的规则在两种时钟下相同。Worker记录其实际开始/执行耗时用于诊断，不延长Host总期限。动作已接受后不能重置XY总期限；设备未回ACK时受理预算先使动作Unknown。

其他显式测试预算：安全就绪从PLC启动投递起1000ms；实体按钮由测试驱动器明确输入，不自动产生，也不把人未按按钮视为算法超时。受控停止受理300ms、从停止投递起总完成1000ms，模拟受理80ms、进入Stopping后200ms确认停止。关键保存从入队起2000ms，到期为CommitUnknown/SaveFailed，不推定未写入。Worker释放宽限500ms与业务期限解耦。独立查询预算1000ms、PLC单次传输I/O预算40ms也是显式测试值，不能把传输返回当作设备命令受理或完成。

REQ CTL-002/SAF-003已有PC轮询≤50ms、PLC扫描≤10ms及3s断联基线保留；开发示例采用50ms轮询、500ms模拟心跳翻转、3000ms断联检测。500ms翻转、测试安全快照新鲜性等不是现场值；真实翻转、映射及链路能力仍待OPEN-08/11。模拟无响应的某个命令不自动停止独立心跳。

## 3. 同一计时与确定性仲裁

- Host注入唯一业务TimeProvider；实际时间使用System，可控模式使用FakeTimeProvider（AutoAdvance为0）。Domain只接收经过时间/期限判断结果，不读取系统时钟。
- 等待窗口按(OperationId, Attempt, Phase)区分：PLC Acceptance只由匹配受理/拒绝关闭；XY Completion从投递起计总预算，只由完成/失败关闭，Accepted/Executing不关闭此窗口。算法Result窗口也不被Accepted/Running关闭。采集Completion须有可靠结束且媒体已接管或明确内容缺失的组合证据；仅Ended或仅一帧不能关闭全部采集等待。任一窗口超时后按操作类别收敛，重复窗口事件不能产生第二个业务终态。
- Application DeadlineScheduler为操作登记startTick、budget、dueTick和clockId，通过该TimeProvider创建定时通知。设备/算法事件经OperationIngress短临界区打Host接收时间戳，与同一操作的期限窗口仲裁。适配器报告的发生时间保留作证据，不能回填更早时间逃过期限。
- **接收窗口为[startTick,dueTick)**。响应在dueTick之前进入并通过关联/契约校验才可胜出；等于或大于dueTick一律先形成超时事实、响应记迟到。若到期定时器尚未运行，入站也执行相同到期检查，不能因线程调度延误放宽期限。
- 在短临界区中先登记且满足该Phase结束条件的有效早到响应会关闭该窗口；之后到期事件是无效重复。窗口输出不可变、带序号的仲裁结果，再交唯一流程所有者处理；不要让高优先级队列重排为“早到响应被晚处理所以超时”。停止/取消到达先关闭新动作准入，即使之前的有效响应随后被处理，也只能保存事实，不能继续下一步。
- OperationIngress仅拥有等待窗口和事件顺序，不拥有运行状态、机械资源或持久化台账；业务终态写入与流程变化仍由Station01Coordinator执行。计时器/SDK回调只投递小事件，不做磁盘、推理或长等待。
- 可控测试按下一个到期时刻逐点推进，处理该时刻所有就绪事件并排空短事件链，再推进下一时刻；不要一次大幅Advance后把较早模拟响应都打成最终时间。Advance/Drain是测试夹具能力，只执行已就绪事件，不等待未来计时器/真实I/O。
- 实际时间允许OS调度抖动；比较两种模式时使用同一Host接收顺序/预算语义，期限内正常集成场景留足余量。恰好边界用可控时钟覆盖D-1ms、D、D+1ms。线程或SDK迟送到Host的结果按实际接收时间处理，不假称其按期返回。
- 不推进虚拟时间时，已提交命令和查询仍由普通执行通道处理，定时事件暂不发生；禁止让入口await时钟推进或延迟完成。测试收到Accepted后可查询Running、发cancel，最后再推进Stop反馈。受理不证明设备已停。

实际审计UTC和clockId/elapsedTick分开保存。跨Host重启不能用旧单调tick继续计时；未终结调用记录为Error/Interrupted并在恢复核对中保存，旧已结束结果保留仅供查询；故障双端复位/初始后新run重新冻结预算并完整公共准备，正常暂停未开展步骤仍按原预算；不为未知物理动作重新发命令。

## 4. 独立模拟状态与故障注入

模拟PLC仅接收端口命令及测试驱动器的安全/按钮输入，维护Connected、Mode、Safety、Clamp、AxisPosition、CurrentAction、Accepted/Running/Completed状态，不读取Workflow状态或期望的下一步。启动请求不直接夹紧；夹紧是其安全/实体输入满足后的设备内部转换。移动须检查本模型互锁，再独立延迟改变位置并反馈。

相机模型维护连接代次、参数、是否正在曝光/传输、触发次数与缓冲租约；3D模拟数据描述整盘scope，F仅一个Capture/一帧。算法替身消费媒体/参数和预置测试输出，按自身时钟调度响应，不直接改运行或调用终态。

Respond按延迟输出；Fail按延迟输出明确失败；NoResponse不向调用者发送结果。设备outcome与反馈投递策略分开：Hold保持执行中；Success可在设备内部完成但丢反馈，必须以之后可靠观察及授权核对解除Unknown，不能自动放行。ignoreCancel仅用于模拟不合作/迟到反馈，不影响真实业务取消窗口。重复反馈复用原operation身份与内容，不生成第二次设备动作。

模拟队列与计时事件有界；示例最多128个待发定时事件，单操作最多4次重复投递、16条迟到明细，超额同源重复合并计数并保留首次/末次，不能淹没终态保留容量。10秒迟到观察窗口仅限制测试夹具的观察/清理，不延长业务期限。仍被访问的媒体不能按观察窗口到期强删；需消费者释放或进程退出证据。

## 5. 资源退出

算法到期立即释放“业务等待资格”，取消/隔离该Worker；真实Worker仍读取输入时保留媒体租约，释放宽限到后受控终止并确认退出才归还租约。未退出则标Quarantined计入容量，不让其占住F的独立执行槽。实际容量耗尽按容量异常，不按连续算法失败停盘。

运动或采集超时保留Unknown/Held资源，停止请求按既有契约发出；迟到完成只补证据，用于原故障证据核对和补存，不能授予旧步骤续跑；故障新run完整重新执行。保存超时保留writeId及未知提交状态，核对后再决定，不用重新动作修补记录。待定算法回调不阻止已满足物理/保存条件的移交，但物理Unknown必须阻止移交。


修订记录：2026-09-20，1.1，H01明确原算法期限包含意图保存且不得在提交/派发时重置；七环节延迟和独立预算示例未改值。

## 2026-09-24 008完整执行合同增量

适用优先级：本节及008目标合同用于最新需求实现，前文冲突条款仅在本节明确的历史样本范围保留；本轮未改代码或声称协议缺口已关闭。

公共3D公共采集点及F扫码配置配置独立于F后产品配方。008完整路线按冻结计划计算动作/阶段/整盘预算，公共预算与产品预算关联但不互相覆盖；既有样本版本期限保留其历史范围。人工阶段预算口径须显式，不以无限等待代替未定义规则。

统一来源：REQ §7/11、宪章3.2.0；执行/结果/证据分别见008 contracts/execution.md、api-results.md、evidence.md。关联实现任务见本功能tasks中的008对齐增量。

## 009 / AL08 当前预算与配方应用合同（2026-10-01）

本节为009 FR-035—039已经接受的业务增量，取代本功能旧版对配方应用预算的缺省/无界解释；不修改原算法、受理、夹紧、区域A或后段预算。budget schema当前运行版本为2.0（013删除plcPoll；原1.1只读历史），businessMs.recipeApplication为必需正整数毫秒；运行使用新实例版本，当前Test初值10000。缺字段、零/负/非整数/溢出、不符用途、ID/版本/摘要或来源不完整均在绑定写入前拒绝，禁止隐式补值。Production尚未批准，拒绝对应绑定入口且不回退Test；不影响合法Test路线。旧1.0快照只读历史，不能启动新绑定。

完整规范化budget快照随运行冻结，含id/version/purpose/source/digest/snapshotId及recipeApplication；文件后来改变、暂停继续、重连均不刷新。预算来源与协议地址、报文数量和握手阶段无关。

严格连续链、旧连续链、独立/recipes/bind共用：绑定意图真实提交且取得有效回执后，在端口调用/入队前唯一登记t0；D=t0+冻结recipeApplication，T=min(D,当前实际适用后段绝对截止)。011当前排队、RecipePlanBound及本次适用handoff的必要业务提交/有效回执共用[t0,T)；旧容量/显示读回/raw绑定前置退出当前路径。每项保存另受原CriticalSave自入队起的上限，取其与剩余T较小者。RecipeBindingReceipt核全部适用实际业务提交后才完成，不虚构设备确认；原意图提交本身也按原必要保存预算有界。

严格链保持原Detection/Unload/Sorting在绑定前冻结的起点、公式和值，不补偿绑定耗时；旧链保持handoff后首次Detection起点；独立入口只使用实际已有截止，不造后段期限、不重复已有handoff。已到期不得重设起点后续接。沿原ResponseBeforeDeadline，等于T及之后为迟到；在T前Host收到并校验成功的完整回执不因稍晚调度倒判，但任何后续动作仍检查取消、安全及后段截止。

取消/超期关闭同一请求的后台后继派发及成功续接资格，不能只结束调用方等待。已发I/O/已开始提交如实保留；调用超时不等于回滚，迟到保存不能复活Bound/Ready或产品动作。心跳、观察、必要停止独立保持。当前验证采用011 M09，承接精确边界、真实commit后回执迟到、取消后零新派发及三入口有效预算/保存保护；原BA容量设备应用仅属旧协议历史，不增加全套重跑。当前仅合同对齐，实现归009 T030—T040，运行尚未验证。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 013实施前定向同步（2026-10-04）

013预算迁移：AL08当前新运行budget schema为2.0，删除businessMs.plcPoll/BusinessDurations.PlcPoll，额外字段仍拒绝。代表预算s01-budget-011-joint/2、模拟s01-sim-011-joint/2引用预算2，模拟schema仍1.0；HeartbeatFlip及全部业务预算值保持。加载/准入/冻结和所有活动构造/引用消费者在首次新构建前迁移，不增加旧运行模式或忽略废字段。原1.0/1.1及已冻结BudgetSource只读历史不重判；改前B自身原schema1.1/预算1/模拟1保持。实际两侧路径、摘要与批准差异按013 V02.1，不能宣称字节相同。共享模型/加载/准入改动由013 T009承接，历史任务事实不变。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

013首次改后构建的实际消费者核对：007 examples及010当前content-config中的s01-budget-virtual-loop活动实例同样迁至schema 2.0/实例3.0.0；s01-sim-virtual-loop保持schema 1.0并迁实例3.0.0、budgetRef 3.0.0。仅删除plcPoll和更新绑定/摘要，业务预算与模拟时序不变。007/010历史证据、原任务勾选及013冻结B0/B保持旧版本；这项必要加载迁移不增加007或010完整动态链验证。

020增量：BusinessBudget可选RecipeExecution（仅RealDeviceCommissioning必需），含CaptureMs、AlgorithmMs、AcquisitionReleaseMs、CaptureWaitMs、AlgorithmWaitMs、InputReleaseWaitMs；全部正整数，分别对应既有ExecutionCostProfile的执行额度与等待期限，来源采用预算Source/版本/digest。动作预算继续沿既有字段，不新增PickWaitMs/PlaceWaitMs/SafeWaitMs。旧Test成本不变。新联调配置身份、模拟输入与Parser来源见MC-020；缺现场依据不得准入。
