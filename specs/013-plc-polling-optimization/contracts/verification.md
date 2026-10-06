# 最小验证与计量合同

**013 / 2026-10-04 / 设计标准，所有运行结果NotRun**。本文件在首次对比执行前冻结，后续结果不得倒改预算/门槛。若设计缺陷确需修订，应保留失败与旧标准、审查新版本，并重新取得受影响对照；不能把失败换名通过。

## V01 口径与身份

SC-002要求：同条件**空闲事务率下降**，同一完整代表流程**总事务严格减少**；分别报告每种动作的完成数、总事务和每完成单位事务。活动每秒请求率也报告，但不能单凭它判收益。固定必要命令、清零、关键读不强求减少；某动作增读须列原因和数量并纳入总量。相同完成数量、时效、顺序、保存、原截止同时满足才有比较资格。

SC-006采用四个直接计量，不以全进程CPU/RSS或所有类型分配下降为门槛：
1. 无新观察而重新检查同一缓存的唤醒次数：改后为0（取消、截止、真正新版本不算重复）。
2. 固定计划准备次数：每准入/映射一次有限准备，运行循环为0；报告计划数和准备次数，不能只比较缓存方法调用。
3. 受影响处理量：实际返回word/byte、计划排序/分组输入元素数及采集解码/复制字节；至少源头重复计划处理和重复交换处理减少，不要求同块附带字段每项都降。
4. 同完成流程通信证据条数/原始字节/序列化字节及持久字节：真实保存覆盖保持，总通信证据负担降低；分别报告格式/关联开销，不能删除必需证据凑数。

计数基于实际PDU发送（失败请求也算），区分读/写、业务/心跳、周期/局部快档/按需原因；回应不是另一请求。多信号一块为一事务，写多寄存器也按实际一个PDU计。统计同时保留逻辑用途与唯一来源，不把共享采样重复计入总量。

### V01.1 验收适用矩阵

| 类别 | 适用标准 | 判定对象/边界 |
| --- | --- | --- |
| A：两侧共同资格 | 相同业务路线/完成数量；真实VirtualPlc、采集/算法、必要保存与Final；各侧原I/O/受理/动作/保存截止、取消与未知限制成立；V02输入语义可比、计量有效、原始证据完整 | before和after各自须有真实证据。只完成部分动作、漏保存或超原期限均不能进入成功比较 |
| B：仅优化后 | 300、200/500、200、500/1000ms及有限50ms；V04周期/1110空闲上限、V05按需上界、V06新时效；单源、重复缓存唤醒0、循环计划准备0；独立观察语义及013新增行为门禁 | 只约束after。before旧频率、重复采集/准备、原共享观察形状只记事实，不要求先满足013 |
| C：跨侧比较 | 空闲事务率严格下降；相同完整流程总事务严格下降；按种类报告每完成单位事务及必要增读；通信证据和直接重复处理负担按V01比较；T_after≤T_before+4950ms | A合格后比较；同时B须满足才能宣称013成功。固定必要写不强求减少，21轴批/33轴启动/108核心写是冻结run-2协议审计预期，不进入业务代码常量 |

两侧均记录25/75等相同计量边界的数据，但**V06新增数字门槛仅约束after**；before因自身高负载超过新门槛不自动丧失基线资格。原期限和原保护属于A，不因任何侧的性能目标调整。人造慢请求组件只检验原保护/不追赶，不纳入正常性能目标。所有条款按本矩阵解释，分类出口见V10。

## V02 改前保留与可比输入

当前没有Git仓库，不存在可报告的提交号。**产品改动前**保留主项目当前源码的只读基线副本与SHA-256清单，不使用旧workcopies。范围为backend/src、受影响backend/tests和Rules、backend/tools/Gaode.StorePrep完整源码/项目及实际使用锁文件、VirtualPlc、scripts及实际运行所需配置/fixture/配方/机械输入、global.json、backend/Directory.Build.props、backend/Directory.Packages.props及实际引用构建配置/项目/依赖文件。首次修改前逐项目核查ProjectReference、Import/Directory.*、链接源码、复制资源及运行时工具，记录依赖闭包，不能只复制csproj/现成DLL。按明确文件清单复制并排除bin/obj、运行产物、artifacts和旧workcopies；R位于A内时不得递归包含自身。清单记路径、大小、摘要，记录.NET/Python/OS版本、构建模式和命令、时间、所有输入摘要；构建后记录实际DLL/依赖摘要。改后同范围清单与差异对应013批准范围。

先保存未经修改的产品基线，再以**完全相同的中性计量/验证接线补丁**应用于基线和改后测试环境；另记该补丁摘要。补丁只能观察/驱动原正式入口，不能改通信周期、业务动作、时序或成功判定。基线产品DLL也不得包含013优化。两侧各自源码目录、输出目录、端口、隔离临时存储；禁止旧副本覆盖主项目、共用运行库或拿旧multi03日志代替本次基线。

输入采用现011 examples/joint/run-2.json及其实际解析到的配方catalog、机械配置、真实保存/F匹配/冻结驱动。运行前清单解析并记录全链引用摘要，未能解析不得开始比较。保持：
- Motion500ms、Flip3000ms、PutBack150ms、Sorting3000ms、VirtualPlc scan10ms、心跳翻转1000ms、响应jitter0。
- 同一单槽ordinaryBatch、CD→AB、E关闭；同一算法/采集输入、源/目标槽和操作脚本。
- 同一线程池、GC、进程优先级、核心数、宿主选项、构建模式、机器及外部负载；不并跑其他验证/构建。
- 同一2秒只读后端状态查询负载，无额外PLC采集；已有人工授权/运行驱动方式相同。
- 原Test预算：PlcIo1000ms、HeartbeatDisconnect3000、PlcAcceptance2000、XyCompletion8000、SafetyReady1000、StopAcceptance300、StopCompletion1000、CriticalSave2000、RecipeApplication10000、Flip/PutBack10000、Capture8000、FDecode15000。未列项沿当前fixture实际值并记录，不任意调整。
- BusinessMs.HeartbeatFlip500属于现FullSimulation输入，不能替代此VirtualPlc实际1000ms。

### V02.1 明确版本、路径与引用映射

以下为后续实施确定的版本，不是本轮已改配置：删除必填字段是不兼容预算形状变更，预算schema由**1.1→2.0**；代表预算实例和引用它的模拟实例各由**1→2**。B=GAODE_013_BASELINE_ROOT，A=GAODE_013_AFTER_ROOT，R=GAODE_013_ATTEMPT_ROOT；运行前记录各自实际绝对路径和摘要。

| 项目 | before | after |
| --- | --- | --- |
| 产品/构建身份 | B内冻结未优化主项目+中性补丁；B自身构建产物 | A内013产品+同一中性补丁；A自身构建产物 |
| GAODE_011_FIXTURE | R/inputs/before/run-2.json | R/inputs/after/run-2.json |
| fixture schema / configRoot | station01-fixture/2.0；相对config | 相同；相对config |
| Gaode__ConfigRoot | R/inputs/before/config | R/inputs/after/config |
| Gaode__SchemaRoot | B/specs/001-station01-public-preparation/contracts | A/specs/001-station01-public-preparation/contracts |
| budget schema路径/版本 | 上述SchemaRoot/budget.schema.json，const 1.1，必填plcPoll | 同名路径的新schema，const **2.0**，删除plcPoll属性及required项，仍拒绝额外字段 |
| 预算路径/身份 | ConfigRoot/budget.json：s01-budget-011-joint/1，schemaVersion 1.1，plcPoll=50 | ConfigRoot/budget.json：**s01-budget-011-joint/2**，schemaVersion **2.0**，无plcPoll |
| run-2 budgetRef / Host BudgetId、BudgetVersion | s01-budget-011-joint / 1 | s01-budget-011-joint / **2** |
| 模拟路径/身份 | ConfigRoot/simulation.json：s01-sim-011-joint/1，schemaVersion 1.0 | 同名路径：**s01-sim-011-joint/2**，schemaVersion仍1.0 |
| simulation.budgetRef | s01-budget-011-joint/1 | s01-budget-011-joint/**2** |
| run-2 simulationRef / Host SimulationId、SimulationVersion | s01-sim-011-joint/1 | s01-sim-011-joint/**2** |
| public.json、publicConfigRef / Host PublicId、PublicVersion | s01-public-011-joint/1，schemaVersion 1.0 | 身份、内容完全不变 |
| public-config.schema.json / simulation.schema.json | 各侧SchemaRoot下原文件 | 内容摘要与before相同，不随预算升版 |
| 采集策略归属/装配 | 原PlcRuntimeOptions/Host Test50，预算旧字段保留但不把它当新采集开关 | Infrastructure/Devices/Plc的不可变策略，设计身份**plc-acquisition/013-1**；Station01Registration装配，设备准入冻结；参数见A01/A06，摘要由实际冻结策略生成 |

新采集策略不放入businessMs、配方或simulation，不增加业务端口协议知识；原IoTimeout、HeartbeatDisconnect及全部业务期限另按原预算装配。优化产品删除PlcPoll/PollMs旧入口，不提供1.1/2.0运行模式选择、忽略废字段或回退旧轮询的旁路。这里的schema 2.0是新运行准入；历史已冻结快照保留原schema/摘要只读，不触发新运行校验或改写。

输入准备以**冻结before源码中的**specs/011-plc-interaction-update/examples/joint完整依赖树，按字节复制到R/inputs/before和after，后者仅做下述迁移。目录必须包含fixture所有相对引用，不能用../逃出目录去读另一侧。RecipeExecution010RunHarness.Local已有目录约束；workerScriptPath另按各侧源码根解析，脚本内容摘要必须相同。FindWorkspace从测试DLL路径上溯，所以必须用各侧自身构建产物，不能只改变cwd后仍加载主项目新schema或DLL。T007从B自身源码构建并实际使用B的StorePrep（准备库/配方种子）、Host、VirtualPlc和测试产物；工具解析路径/DLL摘要纳入resolved-roots和build-manifest，不回用A的工具产物。Harness在环境变量和Start请求中传递的三组id/version必须与表一致，实际配置加载器返回的引用/摘要也须核对。

批准的配置内容差异仅为：

- budget.json的schemaVersion、version，以及删除businessMs.plcPoll；其余业务期限、limits、HeartbeatFlip、用途/来源/边界保持。
- simulation.json的version与budgetRef.version；所有stages、stop、deviceInitial、clockMode、lateObservationWindowMs和fixtures保持。
- run-2.json的budgetRef.version与simulationRef.version；schema、expected、配方/机械/媒体引用、模拟putBackDurationMs及safeZ等保持。
- 上表预算schema结构变更、通信内部013策略及必要路径/端口/隔离存储身份映射；后者必须列出并证明不改变负载。路径字符串的不同不是输入语义不同，也不能隐藏实质参数变化。

每侧原文件SHA-256、loader规范化BudgetDigest/SimulationDigest、真实SnapshotId、构建身份均分别记录；新增configuration-map记录JSON字段差异、批准理由及字段语义对应。预算/模拟摘要和冻结身份预期不同，**不能宣称所有文件字节相同**，也不能抹除不同后再把原摘要写成相同。既有input-manifest.json可作为复制来源记录，不能用其旧摘要证明迁移后文件；本attempt生成实际inputs manifest。RecipeInputSha256及不变的机械/媒体/算法文件摘要必须相同；运行生成ID、端口、路径和快照摘要做显式对应。

中性计量补丁单独记原文摘要和两侧应用后源码/构建身份，仅包含相同观察器、原入口驱动、实际交换计量/原报告接线；配置映射是另一份变更清单，**不混进基线补丁**。before保持原产品/schema及含plcPoll配置，绝不提前带入013策略或新schema准入。

实施前定向同步001 contracts/configuration-time.md“009 / AL08 当前预算与配方应用合同”中的schema 1.1和budget.schema.json，再对齐009/011/012实际受影响合同及plan/tasks；随后才能改共享代码。实际命中消费者为BusinessDurations、ConfigurationLoader、PublicConfigurationValidator的1.1校验和模拟预算引用一致性、RecipeApplicationCoordinator.RequireBudget的1.1校验（ConfigurationFreezer调用）、ApprovedExecutionCostProvider/RecipeExecutionBudget的引用及摘要核对。后两者保持核对义务，正常生成新版引用/摘要，不加特权。同步JointInputDefinitionTests、Station01RunConfigurationTests的活动输入/旧版本预期以及009公开形状基准；旧历史JSON/证据保持。无影响的012保存、匹配和冻结行为沿用，只核对预算引用正常消费，不重写其业务。

## V03 窗口、次数、归因

每侧只做一次固定空闲观察和一次完整代表链，不为取最好结果重复链。
- Host/VirtualPlc真实就绪后固定预热5秒；所有预热/启动事务另列，不混入空闲结果。
- 空闲窗口为单调时间半开区间[t0,t0+60s)。按实际发送开始归属请求；跨边界响应保留并标记，不丢慢请求。末尾响应耗时可以在窗口后记录，不能缩短发送窗口。
- 空闲必须通信确无未完动作/复位/握手；既定2秒API读取继续。初值心跳同步在预热内完成；否则该次标前置不满足，不挑截一段伪装成功。
- 活动从Host正式Start命令受理（首次PLC启动写之前）至同run Final持久完成；启动前配方保存/初始化以及Final后关闭另列，不能隐藏在总日志之外。核心108次业务写的统计边界见V05；其他交换仍进入活动总量。
- 统计按活动实际阶段切片，报告每种动作总数/完成数及读写/证据量；共享周期事务只进入全流程一次，单位动作报告“动作专属”和“动作时窗内共享”两列，重叠归因不可再次相加。
- 失败/未完成流程不进入成功分母，不因为事务少判优。若环境或已定位故障导致重跑，保留该次失败；只补必要侧/必要项，不自动追加两条完整链或全配方。

## V04 周期预算

采用采集合同A02的当前Test合法块。下表及本节上限仅约束after；before按实际旧实现计量，不套1110或013周期预算。名义值不含边界和按需操作，不是正式PLC硬上限。

| 场景 | 基础 | 位置/动作反馈 | 心跳读+Test应答 | 名义事务/s |
| --- | --- | --- | --- | --- |
| 空闲 | 6/0.5=12 | 2/1=2 | 1/0.3+1 | 18.333 |
| 活动且确认非运动、无专用等待 | 6/0.2=30 | 2/1=2 | 同左 | 36.333 |
| 轴运动/不能排除运动 | 6/0.2=30 | 2/0.5=4 | 同左 | 38.333 |
| 翻/放回完成等待 | 30 | 4+1/0.2=9 | 同左 | 43.333 |
| 取/放等待 | 30 | T三块/0.2=15，独立P暂停 | 同左 | 49.333 |
| 首轴Moving局部快档 | 5/0.2+1/0.05=45 | 4 | 同左 | 53.333 |
| 首翻/放回Executing局部快档 | 30 | 4+1/0.05=24 | 同左 | 58.333 |

Test应答“1/s”来自1000ms实际翻转，不是正式PLC周期。取放两个位置块多附带4word/8byte响应载荷但不增PDU；比较SC-006须如实计入。15～25、35～55仍只是原初估，局部58.333不应被偷偷改成全局恢复50ms。

固定60秒空闲允许的离散边界预算：
B≤6×(ceil(60/0.5)+1)=726；
P≤2×(ceil(60/1)+1)=122；
H读≤ceil(60/0.3)+1=201；
真实翻转应答≤ceil(60/1)+1=61；
合计≤1110次（18.5/s），名义1100次。每项+1仅容许窗口边界/在途相位，不是额外任意读取。空闲无按需动作读写；发生它们则注明窗口不再是合格空闲。请求少但采样迟延/失效也不通过。

活动预算使用预先固定公式，而非事后选择百分比：
N_total = N_periodic + N_demand + N_business_write + N_heartbeat_echo + N_failure。
五项为互斥计数桶：首次请求即使失败也留在原周期/按需/写/应答桶；N_failure仅收失败处置额外产生且尚未归入前四项的真实PDU（包括既有获准有限读重试），不能将同一失败首请求加两次，也不能排除任何已发送请求。
每个连续所有权阶段j、来源g：N_periodic(g,j)≤blocks(g,j)×(ceil(duration(g,j)/period(g,j))+1)。
同一R0080块从B转快档、P转T只计算一个来源；切换至立即必要观察按一次事件计，不能每个消费者加一份。报告实际阶段时长及V06时效，禁止靠拉长duration扩大预算。失败重试全计入N_failure，并使正常对照标记失败/受限，不能靠另设“异常桶”排除总量。

## V05 按需读写与静态完成义务

| 事件/计划 | 单次读/写预算与原因 |
| --- | --- |
| I显式初始状态检查 | 每次获准ReadInitialState检查10读；StartAsync本身不新增整表读。真实调用按所在窗口计入，不能固定“一次/准入”隐藏重复 |
| Start | 1触发基线读、1 PcReady写；写后最多1轮B=6读，已有合格在途/新观察可合并 |
| 每轴子动作批 | 1触发基线读；n轴目标/start/clear共3n写；非最终XY实测1读，最终实测完整P=2读 |
| Flip准备+派发 | 型号和目标面2写、Flip1写；反馈基线1读，随后唯一F源 |
| PutBack | 反馈基线1读、命令及clear2写；随后唯一U源 |
| Pick/Place | 命令各1写、每次反馈基线最多1读；T等待；分拣全部六子动作后Idle1写 |
| 分拣预派发核查 | SortStatus 1读；原有限失败重试另记，成功路径无额外重试 |
| 七次采集释放 | 每次最多1轮新B=6读，满足释放后因果的新观察可共用；不等周期 |
| 关键安全/用途准入 | 同一次入口最多1轮所需B=6及1轮所需P=2；两者都不合格且原语义均依赖时可各读一次，不合并成同一时刻。仅缺所需观察才读，登记原因；不得等待中反复伪装“前核查” |
| Stop/Reset（组件覆盖） | Stop1写；Reset保持现4写及最多1轮新B确认，其余必要初始重连读单列，不在成功代表链虚增 |
| 心跳 | 每个真实翻转最多1写，初始同步单列；失败回执不能盲重写 |

after活动总量上界由V04加上述事件乘数得到；before实际按需/重复读照实计数，不套新283上界。run-2准入消费者在本设计固定如下，后续manifest照录而非从实跑请求数反推：Start 1；Move 9（3D 2、F 1、Detection 4、FlipPick 1、FlipPutBack 1）；Flip 1；PutBack 1；Sorting 1；Unload 1；CaptureOpen 7，合计21入口。Start最多一轮B，其余20入口按原语义依赖最多各一轮B及P，故准入B上界126读、P上界40读。CaptureOpen优先复用前一步已发布的合格最终位置，不能漏登记消费者，也不能强制新增读。状态查询不算入口。

Start写后新B、7次CaptureRelease和轴到位后实读有不同因果边界，已在表中单列；它们与准入读符合相同资格时按实际同一PDU扣重，不能重复报总数。成功活动按需读保守上界为283：准入166＋Start触发1/写后B6＋轴基线21/到位坐标42＋Flip/PutBack基线2＋Pick/Place基线2＋分拣预派发1＋7次释放42。该上界允许必要即时读，不要求花满；原语义不依赖的组不得为了预算而读取。显式初始检查若确需发生另按每次10读登记原因；成功链不能新增无来源的调用，失败/恢复归失败运行。写目标、start、clear不跨未知地址合并。

run-2独立预期（来自fixture/计划源码，不来自本次运行日志）：

| 路段 | XY批 | 单Z批 |
| --- | --- | --- |
| 初次3D | 1 | 0 |
| F定位 | 1 | 1 ScanZ |
| C/D/A/B四次检测定位 | 4 | 4 CameraZ |
| 翻转取位/放回位 | 2 | 0 |
| 放回后3D | 1 | 0 |
| 单NG分拣 | 2 | 4 GrabZ |
| 下料 | 1 | 0 |
| 合计 | 12 | 9 |

21轴子动作批、33轴启动/清零对，必要轴写99次；加StartReady1、型号/目标面2、Flip1、PutBack及清零2、Pick/Place/Idle3，成功核心业务写**108**。不含心跳/启动初始化/失败停止/恢复。不能省略零距离动作来减少这项。坐标按需上界42读（21批×2），实际非最终XY为1块时更少；轴触发基线21读；七次释放新B上界42读。其余准入按上表明确入口，全部进入总数。

必需业务事实：单槽s1、C/D/A/B四次检测定位、翻转1/放回1、姿态观察2、媒体7、算法9、NG1、物理槽1、F期望125/100；真实保存后的新配方被匹配，在途冻结快照不被后续编辑改变。来源run-2独立expected及共同RecipeRunPlanner，不使用被测实现自己的统计代替独立期望。

## V06 采样偏差、时效与耗时门槛

以下全部新数字为**仅优化后、固定Test正常负载**预先选定的独立联合工程目标，尚无测量证明，既非PLC能力承诺，也不是新增超时配置。before同边界记录数值但不据新目标拒绝；两侧原期限/保护始终适用。人为慢响应组件只检查原截止/不追赶/不错误推进，不套正常性能阈值。

时间符号及完整分解以A03.1为准；下表数值分别计量，按V06.2区分硬条件和性能观察，不能从其中一行自动推出另一行：

| after目标 | 计量边界和门槛 | 适用条件/不能据此推导 |
| --- | --- | --- |
| 到期/连接服务 | d→s₁≤25ms；单PDU xᵢ=eᵢ−sᵢ≤25ms，逐块记录q−d、s₁−q及gᵢ | 覆盖全部正常周期请求；按需首发送采用下列k口径。单PDU25适用于正常读写，仅是一项上限，不能证明固定负载可调度 |
| 完整轮次发布 | C=p−s₁：B≤150、P≤50、F/U/H≤25、T≤75ms；局部快读C≤25ms | C含Σ交换+Σ块间等待+末次解码/发布。保留这些数值作为**独立联合目标**，撤销“块数×25即保证”的推导；B快档合成还独立检验旧块年龄 |
| 同来源持续采样 | 相邻实际首发送间隔≤P+25ms；各块间隔、完整发布间隔、missed/缺口同时报告 | 不能只筛选已有合格样本；正常目标不因慢轮跳过而自动合格。所有权切换用新旧实际块连续核算，不重置消除缺口 |
| 相邻观察的保守年龄 | 上轮最早依赖开始→本轮完整p：B活动375/空闲675、P运动575/非运动1075、F/U250、T300、H350ms | 独立核实际最旧依赖时间；同一完整来源满足前两行时才可用P+25+C解释上界。复用旧块不得套新s₁。数值不是新失效阈值 |
| 首Moving/Executing | 激活资格→首可靠读发布≤75ms（首读可仍是旧状态）；随后p下一−s上一≤75ms | 两者都计排队、真实交换、块间竞争/发布；50+L+C≤75独立成立，不能以L≤25且C≤25推导。真实中间态仍按设备时窗及原受理/动作截止核实，失效或无样本报缺口 |
| 必要命令/清零 | 全部派发资格满足时k→实际发送≤50ms | 实测K/R/P及快档竞争都计入，不再声称只会等待一个25ms PDU。原截止更早时按原点拒绝 |
| 即时核查完整返回 | 资格k→可靠观察可消费：单块≤75、完整P≤100、完整B≤200ms | 包括入队/所有块间插入/发布。同一核查同时需B+P，两者均从同一k分别计时，不能串联重起计时得到300ms额度；原截止仍优先 |
| 心跳两类延迟 | Host合格识别翻转→应答完成≤50ms；可靠Test设备翻转→应答完成≤400ms | 前项含应答排队/真实写交换。后项同时需要H保守350ms观察及50ms应答目标、1000ms Test翻转无漏边沿、可关联设备时基，时基误差纳入400ms；未校准不冒充端到端值 |
| 原失效资格与诊断 | 安全年龄仍max(500,IoTimeout×5)；3秒到点即无资格；正常到点→诊断记录≤25ms | 原阈值两侧共同；新增诊断25只约束after，不能让迟到响应先续期，也不让日志迟延延长动作。普通位置过期显式Stale并在必要准入即时读 |

A03.2固定仲裁及A03.4服务需求、短窗口竞争核算是本表的适用前提与解释证据，**不是替代本表的通过证据**。例如T每秒45个业务PDU若全为25ms即已超容量；不得通过阻塞通信、扩大周期、缩短单PDU门槛、放宽原期限或4950ms来造出可行结论。真实耗时待运行获取，超目标按V10分类；不能无依据归为环境问题。

一轮允许的块间等待总量为C目标−Σxᵢ−(p−eₙ)，必须非负且实际Σgᵢ不超出；每个gᵢ也受这项剩余额度约束。它随实际交换/发布耗时变化，不是另外再赠送的排队预算；插入优先请求仍占用此额度。多块即时核查同理从k起计入全部等待，不用每块重新起算掩盖超限。

流程耗时：每个原I/O、受理、动作、保存窗口都须满足。报告Start→Final总时长及每阶段“设备执行、采样等待、排队、保存/算法/人工”等实际耗时。不得加入人工睡眠、改变设备持续时间或推迟发令来压低请求率。

冻结总时长回归容许量为**4950ms**：21个轴到位+Flip/PutBack完成2+Pick/Place完成2，共25个完成观察节点，按旧50→新200的150ms名义差加25ms调度余量；另21批首Moving+2个首Executing共23节点快档仍50ms，仅各25ms余量：25×175+23×25=4950ms。Start就绪、7次释放、最终实坐标改即时核查，不给慢周期等待额度。要求T_after≤T_before+4950ms且V06.2硬条件（含原期限和首态75ms）成立；即时读取工程数字单列观察；这是预先制定的回归容许量，**不是任意两次运行差值的物理定理**。超限保留失败并定位，不能增加4950ms容许量、倒调门槛或以减少事务抵扣。即使时长合格，同完整流程总事务仍须严格下降。


<a id="acceptance-v2"></a>

## V06.2 需求方批准的验收分级修订（013-acceptance/2，2026-10-05）

本节依据本轮需求方明确授权，优先于本文及013设计中将全部V06工程数字作为收口否决条件的旧解释。原数值、计量端点、窗口、全样本及历史Failed均保留；不按实测最大值制定新上限，不宣称旧目标通过。此为标准版本变更，非实现自行“倒改门槛”。FR-022禁止未经需求方批准倒调标准继续有效。

| 旧条款/引用 | 新类别及依据 | 实际判定位置 | 最小验证 |
| --- | --- | --- | --- |
| V06单PDU25、到期→首发25；A03.1/3.4 | 性能观察；013新增工程目标，无正式PLC/业务硬上限依据 | plc_polling_013_compare.after_targets / target_classification：SinglePdu25ms、DueToFirstSend | 超限全量保留，仅此类超限不否决 |
| V06 B150/P50/F/U/H25/T75及快读完整发布25 | 性能观察；保完整Σ交换+Σ块间等待+发布 | CompletePublication及原timing统计 | 不移动端点、不扣除开销 |
| V06普通周期+25、共享块间隔及推导年龄B375/675、P575/1075、F/U250、T300、H350；A04 | 性能观察；真实时间/年龄和原准入失效规则仍为硬条件 | ActualInterval（非局部首态）、SharedBlockInterval、ConservativeAge | 跨失活段不伪造连续间隔，旧观察授权仍拒绝 |
| A06/V06首次Moving/Executing≤75、局部有效观察间隔≤75、50ms阶段 | **硬条件**；轴150/Flip/PutBack80ms短态保护继续保持；真实观察齐后才恢复200ms | FirstStateInterval75、ActivationToFirstPublication75ms、局部ActualInterval:X/F:first/U:first、首态缺口；I-FAST/I-FU | 超限或漏真实中间态仍拒绝；不拉长模拟状态 |
| V06命令派发50、即时核查75/100/200 | 性能观察；数字无另行协议依据，及时优先机制及原截止为硬条件 | CommandEligibilityToSend50ms、DemandComplete | 数字超限独立报告，原期限/后继资格失败拒绝 |
| V06 Host翻转观察→应答50、Test翻转→应答400 | 性能观察；不将Test推导作为正式PLC要求 | HostObservationToEcho50ms、DeviceEdgeToEcho400ms | 真实边沿/事务/时基缺失仍拒绝，不以新心跳续期旧值 |
| V06失效→日志25 | 性能观察；日志真实、可关联可持久及原3秒失效/禁止迟到续期仍硬 | 原日志时间线/组件事实；没有该精度数据标NotMeasured，不伪造统计 | 不关闭日志，不以记录迟延推迟失效 |
| A03.4平均利用率、块间剩余C额度、即时读取联合工程上界 | 仅用于解释上述性能观察，不能隐式再否决 | 同一target_classification；未知/未登记错误默认硬失败 | 单PDU、组上限不能推导短态保证；仍禁止饥饿/积压/追赶 |
| FR-002—021、AC-01—10、SC-001/003/004/005/008中的原期限/身份/真实反馈/坐标/保存/失效及单源、固定计划、确认周期 | **硬条件**；既有业务保护与本次明确降载义务 | 原正式业务断言、53行组件、L/009、commonErrors及afterTargets硬错误 | 任一失败不被降载收益抵扣 |
| V04/05预算1110、分段活动公式、283按需、108写/33启动；SC-002/006及V06总时长4950 | **硬条件**；保持已批准预算和净收益口径 | after_targets计数 / compare收益与流程回归 | 超预算、少步骤、无收益或时长超限拒绝 |
| V09/10、SC-007、N1/N2/N3、身份/可比性/缺口/Skip | **硬条件**；证据不能用新标准自动补齐 | recipe_execution_010.validate_rows、final_gate；013 closure_decision | 缺行/Skip/错误身份/不可比必须拒绝 |

正式输出分别列correctness、loadReduction、performanceObservations、evidenceIntegrity、softwareClosure及fieldLimitations。性能观察保留数值、count/max/mean/p95及超限次数（现有字段能力内），未测项明确NotMeasured；不新建性能平台。硬条件通过且只有性能观察超限时，仅允许结论“013通信降载专项软件收口，保留明确的性能目标偏差和现场验证限制”。业务/原保护失败、首态75失败、证据不完整、不可比或必需项未运行仍否决，不归入告警。

**既有证据复核**：允许产品/构建/输入/计量行为未变时，按本版本离线重算run17原始transport、真实SQLite、原TRX/账本；记录原源码/构建/输入/attempt及旧规则Failed、新规则/判定器摘要，原报告不覆盖。固定正文2前后对照与主项目正文3集成分别评价，不混拼分母；当前判定器/文档变更与产品身份差异逐文件登记。修改判定器后的实际L及离线正负例单列，原53组件/旧L只能按原身份验签/重解析后引用，不改标。代码行为或输入/计量口径变化才补受影响执行；性能观察峰值本身不触发再采证/重跑。正式地址、心跳周期、最短保持、设备时基及真机应用限制保留。

### V06.1 2026-10-04 时效续修计量校正

attempt `20261004-timing-fix-02` 的原始结果保持失败。已证实旧 transport `Ended` 在响应校验、原始证据记录及诊断之后；旧组 `published` 在语义投影/通知之前。校正时保留原 `Ended` 服务结束口径和请求计数，新增同事务的响应收齐、校验完成、证据锁获取/记录、诊断结束时点；V06单PDU取实际发送→响应收齐，后续开销仍完整进入末块→发布或相邻块间隔，不能消失。响应收齐是应用层 ReadExactly 恢复后的观察点，不能声称是内核收包时间。

组记录同时保存唤醒、真实入队、来源/代次/轮次身份及最后发布点。按需归属取该轮实际来源，不按其他并发请求时间猜测。相邻间隔以同来源实际启停/代次判定；停用的X不跨动作相减，持续的B/P以及B↔X、P↔T共同字段仍保留真实采样缺口。旧数据只能按已有字段重算，无法补出的端点明确标缺失，不回填假时间。

仅为此次定位，在既有通信诊断边界增加有界数值时间线（上限32768，溢出计数，结束后输出），关联实际端点、连接、事务及原始功能/地址。不增加请求、不改调度/线程池/GC/优先级/模拟时序；不逐请求输出文本。旧before的共同发送计数、原服务结束、SQLite时长和证据负担仍使用原中性计量；新增内部阶段仅用于after定位，不冒充before已有同等分辨率。记录新增挂点自身耗时及身份；如明显干扰或改变共同边界，则比较资格不成立，不能静默复用before。先补直接计量组件，再按现有最终入口一次补after；冻结25/75及其余门槛全部不变。

## V07 直接保护组件与最小负例

复用现backend/tests/Gaode.Communication.Tests/Devices中的下列通信用例，仅调整采集策略/时序预期；不重建测试体系。每个选择的Theory数据行均需发现/执行，不能只记方法名。

| 集合 | 复用/必要补齐 | 断言实际行为 |
| --- | --- | --- |
| 心跳原截止 | T065OriginalDeadlineTests.RealOriginalDeadlineStillLatchesAndRejectsNewMotion两个分支；HeartbeatInterlockTests的2999ms及3秒案例 | 正常边界、慢读跨3秒仍拒绝动作；迟到翻转不复活 |
| 分轴/取消/用途 | IndependentAxisTests.ScanAndDetectionUseTheirOwnZAndCaptureReleaseWritesNoLegacyAcknowledgement；CancellationAtXyAcceptanceCannotDispatchDependentDetectionZ | 各轴反馈、到位后坐标和用途；取消不派发后继Z |
| 翻/放回本次反馈 | ActionHandshakeTests.FreshFlipAndPutBackUseSeparateFeedbackAndPreparedProgram；UnrelatedOrOldFeedbackCannotAuthorizeFlip原3数据行；SameTargetWithoutDistinguishableFeedbackIsUnknownHeld | 保留先Executing、本次实体/代次、旧完成拒绝 |
| 取放坐标/原期限 | InitiallyMatchingPickPointCannotSurviveLaterWrongXy；MissingTargetStageObservationNeverSubmitsPlaceOrSortingOk；SortingMotionCannotConsumeTheLongerTrayDeadline；ExpiredWindowCannotReleaseCommittedCapture | 后续坐标仍核查，原窗口不改起点 |
| 实存门 | SortingPickPlaceRequiresRealInTransitCommitAndFinalLift；ActualPickAndDatabaseOutcomeRemainSeparateFromPlacePermission原3种提交结果 | 实际取料与保存分离；无有效回执不放料、最终抬升保持 |
| 正常降频失败保存 I-FU | 原HeldFlip迁为HeldFlipPersistsFailureWithoutDeclaringCompletion，保持5000ms原窗口、FlipFeedbackHold及模拟时序 | 真实SQLite失败证据及Action/Run/Operation关联，无完成/后继PutBack；实际未达1024不强求checkpoint/precedingEvidenceReferences |
| 自动阈值接续 I-SEG | FailureEvidenceTests.AutomaticThresholdCheckpointPrecedesRealFailureContinuation，见下方受控触发 | 1023无段、1024自动阈值经生产Recorder真提交；后续失败正确引用已提交段且无虚假完成 |
| 原始缺口 I-GAP | 独立一个CommunicationEvidenceRecorder原始窗口缺口拒绝案例 | 监视游标gap不替代Recorder缺口验证；I-SEG正常段链不能抵扣此项，不扩存储异常矩阵 |
| 准入映射 | ProtocolDefinitionAdmissionTests.FormalEntryAdmission现12数据行 | 合法4/非法8，非法在I/O前拒绝；准备计划不能跨映射 |
| 最少013组件补齐 | 单源/档位/慢轮次共一组；分组年龄/位置投影一组；轴/Flip/PutBack三个首状态阶段 | 实际TCP请求/设备变化时间；同缓存无重复唤醒，旧闲计时立即退出、慢轮不追赶、快档退出200 |

新增组件用现TCP fixture/可控时间设施；短态保持fixture原150/80ms及扫描设置，不延长动作。至少覆盖200ms跨过短态时安全拒绝，以及有限首状态例外实际捕获；不能只改配置断言通过。

I-SEG受控触发仅在通信/证据组件层：在独立现有TCP fixture中记录真实Modbus交换，生产分段游标起点后1023次交换调用自动入口不提交，再第1024次调用同入口产生checkpoint；经真实TraceWriter/CommunicationEvidenceRecorder提交SQLite并读回，再增加真实交换、经同一生产失败接续/RecordFailureAsync边界保存失败窗，查实段引用、字节及关联。使用原ActionWindow/保存预算；准备交换不靠延长业务动作窗口。允许在LatestProtocolPlcDevice.FailureEvidence.cs内部抽取由设备和组件共同使用的分段状态/提交边界，不加业务层测试分支/生产开关。不得只调用force就宣称自动阈值成立；不降低1024、不恢复高频、不改原模拟时序、不向性能链加流量、不直接写结果行或伪造引用。I-FU、I-SEG、I-GAP三项独立，完成事实分别由T028账本承接。

三类负例各一项，测试侧受控变体/输入，不给产品留兼容开关：
- N1：让动作等待重新发出同反馈持续读取。实际PDU来源/预算核对必须拒绝，即使最终动作成功；不是搜索某源码字符串。
- N2：新心跳+旧位置/旧完成观察尝试放行。实际后继命令数为0、错误授权被拒绝；只断言读了缓存方法不算。
- N3：从本次实际执行结果移除一个已冻结013必需项，保留其他通过；现发现/执行/原报告核对须失败。复用L的G02发现/执行、G03跳过/过滤基础逻辑，不另建验证平台。

## V08 一条代表链与最小接线

复用现有ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite及RecipeExecution010RunHarness，使用011 run-2期望。覆盖公共准备、真实保存后的F匹配、启动冻结、轴/翻放/复查/多面采集算法、NG实取提交/放料/抬升、下料、既有授权取盘和Final；未覆盖的取消/失效等由V07补齐。

当前harness的WaitForPageObserverAsync与默认GAODE_011_PAGE_EVIDENCE_ROOT指向旧012工作目录外部观察器，主项目没有可独立调用的该脚本。不能伪造ready文件、删等待或声称当前命令已可独立跑通。

后续最小中性接线：保留同一个正式业务驱动及全部保存/冻结/Final断言，为013计量选择真实只读API观察器（2秒间隔、实际连通后ready），由harness明确区分“013后端负载观察”和原页面验收观察。它只替代本次不验收页面的外部观察前置，不冒充页面证据；原011/012页面验收入口和证据义务不改判。两侧使用同一接线补丁/观察器/驱动，基线先取得成功且独立期望完整后才可比较。若环境缺Python/算法/采集或真实存储条件，标对应代表链NotRun，不绕过。

## V09 架构门禁、发现与实际执行

I-FU-02输入修复承接（2026-10-04）：正常准备完成后，仅在测试通信边界启用StageFeedbackFaultProxy的旧Flip完成响应注入。仅修改合法且包含FlipStatus的读响应对应字，独立预期为Completed=2，保持事务、长度及其他字段；所有命令、心跳及实际模拟执行正常转发，代理不得拒绝Flip写。删除一次性寄存器赋值。必须记录正式FlipAsync调用、同连接/事务的请求及注入前后TCP报文，验证产品明确以TransitionFeedbackNotFresh拒绝、没有向TCP派发Flip命令且真实SQLite无FlipCompleted事实。报文标为受控故障，不冒充PLC事实。I-FU-01正常Flip/PutBack通过同一未启用代理作为正对照；I-FU-03/04原数据行及全部48项义务不变。

此次修复的定向前置只执行I-FU-01—04，随后完整L和其余必要组件。固定013 runner允许同attempt的这四行原报告一次引用：预先绑定最终parent、源码/构建/输入/manifest/checker摘要和执行context，最终入口重核实际产物与报告摘要、重新解析原TRX/发现记录，保持原时间、executionId及context，不重标身份。引用不授予验收资格，不绕过完整L；来源或身份变化即拒绝引用。最终合格账本仍须包含全部48个实际case/dataRow，然后才能运行N3及一次after测量。

现010持续轻量集合L必须完整运行：当前manifest为70行（27 dotnet，其中23 RecipeExecutionBoundary、4 ProtocolRepositoryBoundary；7脚本；36自校验）。保持实际Rules构建与原run_lightweight/run_with_lightweight包装，不缩为三类Rules类；不重新执行010全动态专项。

受影响009：复用L中的ProtocolRepositoryBoundary，追加ProtocolBoundaryTests中受策略成员/装配白名单影响的正负例，以及V07实际映射准入；不新增允许业务层轮询/协议知识的豁免。T024须先更新backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json：013新增合同、C#、Python及其他实际命中文件/直接调用/本地helper逐项登记真实职责、扫描要求和分类依据；业务消费者仍按实际层分类。T027前完成登记，不关闭CHECKER-INVENTORY/A10、不扩排除、不降低scanRequired或增加豁免。相同最终源码和attempt的L结果由013汇总引用，不重复再跑一份同样L。

当前runner仅特别处理RecipeExecution010，未知profile会回落默认009；verify_entry的required_manifest也固定009路径。后续必须加明确PlcPolling013分支及013冻结manifest，并保持外层L包装。013manifest包括V07数据行、N1—3、两侧对照和最终代表链结果引用；按同一源码身份去重已有L项。

最终汇总核对：冻结所需→实际发现→实际执行→原TRX/脚本退出和独立证据。零发现、跳过、过滤掉必需项、源码/输入不匹配、只有汇总无原报告都失败。发现清单在执行前记录，运行后只填结果，不据发现结果缩小必需集合。

### V09.1 执行身份及一次引用

| 集合 | 执行的源码/输入身份 | 去重规则 |
| --- | --- | --- |
| before一次空闲、一次代表链 | V02冻结原产品B+中性补丁、before schema/config、B自己的DLL | 作为013同一attempt的基线测量子项，不要求其实现013新语义/门禁；旧multi03不替代 |
| after一次空闲、一次代表链 | 最终013源码A+同一中性补丁、after schema/config、A自己的DLL | 产品或相关输入随后改变，结果不能继续冒充最终身份；定位后只补必要项并保留旧结果 |
| 完整010 L及受影响009 | 最终A身份；Rules/脚本/活动夹具均对应A | 同源码摘要、补丁、schema/输入及attempt已经执行的同一门禁仅引用一次；旧B或旧attempt的通过不能代替A。基线测量子项无需再复制一份完整L，不扩010动态专项 |
| V07直接保护、013新增组件及N1—N3 | 最终A身份；故意慢响应/负例独立标识，使用原150/80ms夹具 | 最小集合一次；与L/009重叠的同一发现数据行和原报告引用去重。受控负例变体仅测试侧，不写入产品配置或基线 |

013最终manifest给每条结果标before/after/cross、源码/构建/补丁/输入身份及原报告路径；compare引用双方一次完整结果。不得将最终A的源码身份写到B产物，亦不得把只运行009的fallback视为013执行。历史L结果只有身份和attempt确实相同才可引用；如无则执行完整L一次。

## V10 结果文件与停止条件

后续执行使用隔离artifacts/plc-polling-013/<attempt>/，两侧分别保存source/build/input manifests、原日志/TRX、bounded-metrics、事务/阶段汇总、设备心跳记录、真实持久引用和compare结果。不得把设计表中的数字填成实测。

按V01.1分侧同时满足SC-001—008适用的软件结果；正式PLC限制另列。以下为结果分类，不是免责桶；同一次可保留多个分类和其证据：

| 分类 | 判断与处置 |
| --- | --- |
| 业务/原保护失败 | 任一侧原期限、取消/未知、必要反馈/坐标/保存门失败，或缺必要步骤/Final；记录真实业务结果，不算完整成功分母，不得换名环境问题 |
| 优化后目标不满足 | A资格成立但after新周期/时效/单源/准备次数/预算超限，或跨侧收益/4950目标未满足；保留失败标准和整窗/整链，不能以低请求数抵扣 |
| 环境/输入不具可比性 | 有证据的输入语义差异、工具/构建身份错误、外部负载不等、计量遗漏/时间轴不可关联等；指出具体差异，SC对照不通过/不可判。单PDU>25或after变慢本身不足以归此类 |
| NotRun | 指定能力尚未执行，或明确前提缺失使对应项未能开始；不冒充失败运行，也不代替必需项通过 |
| 正式设备信息不足，NotMeasurable | 正式映射、翻转周期、最短保持/锁存或设备时基缺失，限制对应真机预算/捕获/端到端结论；Test软件结果可独立评价 |

超限先保留原结果并沿实际排队/通信/发布/保存等证据定位，只在原因明确且必要时补相应侧/组件；不循环重跑完整链选最好、不挑片段、不改模拟时序/原期限/门槛。环境不具可比性不能宣布SC通过，也不自动要求线程池、GC、优先级或长期压力治理作为013前置。缺必需执行记录仍由发现/执行门禁拒绝，不能用NotRun标记获得通过。本轮只修设计；requirements CHK015及所有未运行能力继续NotRun。

2026-10-04诊断续修的最小补验：I-DIAG-01复用并增强HeartbeatWindowDiagnosticsTests.PlcWindowRetainsFastResponsesAndDistinguishesInvalidEchoFromAcceptedWrite，真实TCP按300ms读，核设备无正常间隔误报、真实间隔保留，再真实错误应答触发可关联窗口；I-DIAG-02用既有AxisResponseDelayed受控传输故障验证实际慢响应仍有诊断，不改模拟动作时序；I-DIAG-03在原串行仲裁器的资格回调中同步提交第二个真实TCP请求后立即返回，以第二项实际入队夹在首项资格检查起止之间、两项均实际收发及前后请求身份校验新增时点。测试不阻塞资格回调等待异步续体，不延长原I/O截止，不能只断言配置或日志字符串。原48行不删除，新增这3行，总51行。三行先在当前最终身份定向运行，原TRX/发现/摘要/context按V09.1一次引用后，再完整L、其余48行及真实N3；仅固定diagnostic-preflight，不提供任意过滤或跳过L开关。上一轮flip-preflight保留在历史证据，本轮入口改为这三行，不重复旧修复的定向前置。

新after运行前冻结取证目的为A08的仲裁阶段分解；新时间点只定位，不改变V06判据。相同请求的完整间隙须包括新增记录本身、锁/异步恢复及后处理，快照截断/记录丢弃或时钟关联缺失如实标注。仍缺证据时，最小下一层取证限定为同机短窗口的运行时任务/锁/GC事件与TCP完成事件，用PID/TID、连接、事务和QPC关联；应用收齐不能冒充内核收包，未取得该证据不能判定线程池、GC或网络为根因。

attempt07实际I-HB-04在Failure已可见、观察代次未递增处失败；N3/after未启动，原TRX保留。I-HB-03/04继续复用T065OriginalDeadlineTests两行，在失败日志入口捕获真实Observe（不阻塞日志、不伪造观察），同时核日志入口与外部观察的新代次、不可用/原因及新动作拒绝。测试等待原日志入口被实际执行后核对，不扩大原20秒测试取消或1000/3000ms产品期限；失败证据/旧动作回调仍使用原代次。51行集合不变，不增加完整链。

2026-10-04 I/O绝对截止补验：原51行全部保留，新增I-TIME-02/03两行（HeartbeatWindowDiagnosticsTests.AbsoluteIoDeadlineRejectsExpiredWorkWithoutWaitingForCancellation，dataRow lateResponse: False/True），总53行。复用真实TCP夹具/探针，先正常请求为正对照，再在资格回调后或请求已在途时受控推进通信内部单调时钟超过原1000ms、保持真实取消计时未主动取消。前者必须无额外实际请求，后者记录真实已发及TimeoutException且禁止后续重发；不拉长模拟时序、不降低原期限。受控时钟是组件故障证据，不计性能；原12准入、I-FU/I-SEG/I-GAP/N1/N2不减，真实合格53行后才N3。先两行定向，再稳定身份完整L/53行及原一次after。新增行与原诊断三行可在同一固定前置中一次引用，禁止遗漏诊断目录环境而误判产品；原失败账本完整保留。

2026-10-04 V06计量修正：attempt12翻转sequence855已由心跳事务524及时应答；单个UTC→QPC锚点的残差使比较器将该应答错排到翻转之前，误选533。设备翻转到应答仍以真实设备收齐为终点，改用同设备记录的翻转UTC与匹配设备请求的receivedAtUtc关联，不用Host发送QPC对换算后的UTC作候选排除。仍保留原UTC/QPC残差作为保守误差项，不扣除端到端耗时。连接、事务、功能、地址、值及本地操作顺序必须匹配；本次翻转必须在下一次翻转前获得对应值的应答，缺失不能借用两次翻转后的同值应答。缺设备记录、错误连接/值、多解仍拒绝，原错误派生结果另存保留。

该修正只改变离线比较器及其最小正负例，不改变产品、输入、周期或计量采集。当前完整L/53行及N3另按实际身份核验；after使用attempt12原测量与原身份，逐文件证明产品源码、构建、输入及采集工具未变，再由新比较器离线重算。不得把旧测量身份改标为新源码、不得因此重启完整业务链。若运行侧任一依赖变化，则此复用不成立。新比较结果仍需满足全部原判据，不能以该误判修正抵扣其他真实超限。

I-DIAG-02断言承接：attempt13真实记录显示既有Task.Delay(1500)故障在QPC上为1490.573ms，正确发出Slow Modbus response，却被“ProcessingMs≥1500”拒绝。1500仍是原故障配置，不是冻结时效门槛或精确实测下限。保留原故障和8秒用例/2秒隔离原始客户端配置；改核真实DelayElapsedAtUtc晚于AppliedAtUtc且实际故障等待超过原1000ms产品I/O期限、同事务实际响应与故障响应一致、既有慢处理告警ProcessingMs≥250。不修改1500等待实现，不将正常轮询间隔作为告警，不放宽任何产品期限或25/75等V06目标。用例ID和53行清单不变。

2026-10-05 I-HB-03/04准备阶段诊断：attempt14两行均在首次心跳翻转等待中取消，尚未应用故障，不能充当原期限保护通过证据。保留原失败，仅在原测试finally持久记录是否已注入、实际HeartbeatEdges、Failure、Observe及最后64条既有日志；不改变准备/故障输入、等待条件、20秒用例及1000/3000ms产品期限。允许仅这两行一次诊断补验来判别准备阶段已锁存故障或心跳观察缺失；该诊断不替代合格53行账本，不据旧N3宣布新身份通过。I-FAST-03与I-FU-01的真实短态遗漏/保护拒绝同时保留，不修改80ms模拟时序，不反复重跑选通过。


2026-10-05 C盘独立续查（run16）：固定归档只读，before/after各自源码重建，所有新库/日志/计量/临时文件落C盘。只先运行I-FAST-03、I-FU-01、I-HB-03/04一次有界诊断，不替代53行或正式对照；原80ms/期限/所有断言保留。为承接attempt13已证实同PID的Infrastructure与VirtualPlc诊断副本争写，PlcTimingTrace输出改按PID及实际程序集身份命名，payload登记组件身份；不移动时点、不修改记录容量或运行调度。比较器明确识别新文件，旧文件仅用于历史只读重算，双份候选拒绝。现有翻放用例在finally保存设备实际动作/写入审计、既有代理报文和设备诊断，含时基锚点与gap，不以安全拒绝替代正常成功断言。新增记录只用于失败定位，不能证明内核完成/Ready或解释所有GC相关峰值。T014/T020/T022/T024承接上述诊断；T028—T030保持未完成，正式性能必须先有负责人确认的运行环境条件。未改变业务端口或53项固定义务。


2026-10-05 主项目012合入承接（run16，T009/T022/T028）：当前完整组件实际发现I-CONFIG-02被RecipeCurrentSchemaRequiredForSave拒绝。RC09已要求正文3新保存，活动joint/catalog仍正文2，属于输入迁移遗漏，不修改有效断言或唯一校验器。只将主项目活动011联合Test夹具迁为正文3：明确选择软件测试夹爪1（同现有Recipe011Data.Candidate的受控Test选择，不是历史默认或PLC信号），每个实际相机坐标按原唯一stage/localFace/camera目标显式引用原detect配置，曝光/增益/亮度、机械坐标、动作时序、算法、媒体和业务预算均原样。同步活动run-1/run-2目录摘要和input-manifest；不增加run-1完整链。013固定归档、派生before/after及原性能输入均不改；主项目新输入只用于合入验证，不能与旧before直接计算收益。原正文2读取及拒绝直接新保存保护保留，不增加自动升级/兜底。该输入/设计身份变化使主项目最终L和53行须在新身份下重核；既有失败保留。固定5项诊断前置必须按既有入口启用GAODE_013_MEASUREMENT_ROOT，其余组件不启用额外采集；这不是放宽诊断断言或新的验收旁路。正式性能仍等待负责人确认的同条件窗口。
