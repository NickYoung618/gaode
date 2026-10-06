# 技术方案：PLC轮询降频与通信负载优化

> 当前验收解释：2026-10-05需求方批准 **013-acceptance/2**，详见[验证合同V06.2](contracts/verification.md#acceptance-v2)。普通25ms等新增工程目标改为非阻断观察；原期限/保护、局部首态75ms、确认周期、单源/计划、预算/净收益及证据完整性仍硬。原数字和历史失败保留。下文历史“全部V06成立/不得倒改门槛”以本次显式批准范围解释，不能据此修改硬条件。


**功能标识**：013-plc-polling-optimization  
**日期**：2026-10-04；**宪章**：8.0.0  
**规格**：[spec.md](spec.md)  
**目录**：SPECIFY_FEATURE_DIRECTORY=specs/013-plc-polling-optimization  
**状态**：技术研究与Phase 1设计完成；2026-10-04按architecture DQ-01—03增量修订并完成只读复核，等待设计审查；实现和运行验证NotRun。

## 方案摘要

在主项目已集成的011/012上，把后台采集与动作等待改为同一采集来源、多消费者观察。协议准入时冻结映射并准备有限合法读计划；保留原业务执行器、业务/心跳两条连接、动作顺序及真实保存门。关键读写即时排入，分组各用真实时间，业务只看设备语义。

依据013 FR-001—028和本次plan指令。SC-002/006仅按用户指定计量口径最小修订：空闲请求率与同一完整流程事务总量须下降，按动作种类报告；不要求必要固定写和所有内存指标机械下降。CHK015保持NotRun。

| P13阶段边界 | 当前方案 |
| --- | --- |
| 起点/终点 | 同条件主项目基线经正式入口完成同一多面流程；实际通信和重复处理减少，保护、真实保存成立 |
| 必须参与 | 通信、共同执行、协议及证据；代表链使用真实配方保存/匹配/冻结、独立VirtualPlc、采集/算法、隔离业务存储 |
| 必要验证 | 改前/后各一次60秒空闲、各一次相同代表链；直接保护组件、完整010持续轻量门禁L、受影响009检查、三类负例 |
| 完成证据 | 源码/构建/输入摘要、实际交换、相同完成数量、真实持久证据、发现/执行核对；本轮无运行结果 |
| 延期 | 全量、全配方、长期压力、整机/生产验收及无关稳定性改造；旧失败/待办保留 |

## 技术上下文（Technical Context）

| 事项 | 当前选用与依据 | 状态/限制 |
| --- | --- | --- |
| 运行时 | global.json为.NET SDK 10.0.401；net10.0/C#；复用Task、取消及单调时钟 | 源码事实，本轮未构建；不增包 |
| 通信 | LatestProtocolPlcDevice各partial、PlcSignalAccessor、Gaode.Plc.Protocol | 适配器内部协调，不增连接/第二执行器/通用平台 |
| 数据 | 现Recorder、持久回执及SQLite业务链 | 不改数据库架构或操作运行库 |
| 观察 | 复用Domain已有独立PositionObservation.Identity；修正投影来源 | Host位置API最小增加时间/代次/可靠性，历史原始格式不改 |
| 前端/算法 | 现有2秒后端补查、既有采集与算法进程 | 无页面或算法改造 |
| 性能 | 当前Test合法块推导的预算、实际时间标准 | 均非实测；正式地址/心跳/保持时间未交付 |
| 验证 | 现verify入口新增013明确分支，保持完整L包装 | 当前未知profile会回落009；代表链外部观察器依赖须收敛 |

研究结论见[research.md](research.md)。软件设计选择已明确，外部缺口只限制对应真机结论。

## 宪章检查（Constitution Check）

“符合”表示设计符合，不表示软件运行通过。P09按现行宪章“结构化日志与必要诊断”检查。

| 原则 | 检查点 | 设计前 | 设计后 | 依据/限制 |
| --- | --- | --- | --- | --- |
| P01 | 最新决定和追溯 | 符合 | 符合 | SC最小修订、SY-01—07，不改历史事实 |
| P02 | 核心实际接入 | 符合 | 符合 | 验证合同V08正式端口、真实保存 |
| P03 | 配方与顺序 | 符合 | 符合 | 011 run-2、012冻结，无复制执行 |
| P04 | 安全/期限/终态 | 符合 | 符合 | 采集合同A04—A08，未知不继续 |
| P05 | 隔离与唯一控制 | 符合 | 符合 | 地址/分组/策略限通信层 |
| P06 | 有界资源/独立控制 | 符合 | 符合 | 两连接，每组一轮，排队计入原截止 |
| P07 | 身份与事实 | 符合 | 符合 | 分块时间/代次及动作因果 |
| P08 | 真实保存/冻结 | 符合 | 符合 | IPickCommit及Recorder提交门 |
| P09 | 日志诊断 | 符合 | 符合 | 有界聚合、原始失败窗口 |
| P10 | 外部缺口 | 待补充，仅限制所列部分 | 待补充，仅限制所列部分 | 正式地址、翻转周期、最短保持限制真机验证；软件继续 |
| P11 | 配置/能力 | 符合 | 符合 | 通信策略冻结，不新增配方能力 |
| P12 | 原型/页面 | 不适用并说明 | 不适用并说明 | 无页面实现；2秒补查不采PLC |
| P13 | 最小验证/真实完成 | 符合 | 符合 | 冻结预算、次数、必需项，全部运行NotRun |

没有未解决的宪章违反或需用户补定的软件设计输入。验证接线尚需实现，不冒充已具备运行能力。

## 结构与职责（Project Structure）

| 模块 | 拟改职责 | 边界 |
| --- | --- | --- |
| Gaode.Plc.Protocol/ProtocolDefinition | 准入、冻结定义、有限合法计划 | 不跨未知地址 |
| Infrastructure/Devices/Plc/PlcSignalAccessor | 执行准备计划、解码、真实块时窗 | 计划绑定准入实例/映射摘要 |
| LatestProtocolPlcDevice及Axes/Stages/Semantics/FailureEvidence | 一个内部采集协调器；共享观察；原actionAdvance推进 | 非第二业务执行器；每PDU边界及时让出 |
| LatestProtocolStageActionAdapter/Transfer | 原六段取放及实存门 | 不另读持续反馈、不接管业务保存 |
| Domain/Station01/AxisObservationProjectionBuilder | 使用位置自己的Identity/可靠性 | 无协议知识、无新业务端口 |
| Host/Api/DeviceSemanticProjection | 位置时间/代次/可靠性，live API拟升1.2 | 历史原始schema不改 |
| Host/Composition/Station01Registration | 装配013策略，删Test50/其他25覆盖 | 原期限、共同执行不变 |
| 现诊断、Recorder/Reader | 有界计量、原始窗口、gap/段链/提交 | 不增性能平台/写库架构 |
| 验证runner/manifest及代表链harness | 013必需选择、同条件计量接线 | 保持完整L；不虚构ready |

本轮设计产物为plan、research、data-model、两个contracts和quickstart；不生成tasks。

## 数据、契约与状态

[data-model.md](data-model.md)定义内部计划/观察/等待/统计及最小共享输出；
[采集与观察合同](contracts/plc-acquisition.md)规定全部信号归属、调度和因果；
[验证合同](contracts/verification.md)规定预算、时效与必需集合。

基础状态、完整五轴位置、心跳各自定时。位置过期仅使位置不可靠，不能冒充设备失联；基础过期拒绝依赖准入；连接/心跳失效仍使设备不可用。轴反馈有基础用途，只保留一个持续来源。取放动作组的真实坐标观察承接普通监视，不再同时跑坐标周期。

关键前核查、到位后实测、采集释放按需取得；动作完成前发布所需可靠完整位置，满足现RecipeDetectionExecutor完成后再次Observe的消费者。每次等待返回、后继派发和提交回执使用前核对取消、原绝对截止、代次、身份及可靠性。

## 配方共用逻辑与动作隔离

沿用010共同执行、011顺序、012真实保存/F匹配/运行冻结。代表链为run-2单槽CD→翻转/放回→复查→AB→NG取放→下料/授权取盘/Final，不复制业务实现、不改配方或算法。

### 配置与策略扩展设计（P11）

通信内部不可变策略表达300、200/500、200、500/1000ms，以及有Test源码依据的“首次运动中/执行中”局部50ms；见A06。策略身份固定为plc-acquisition/013-1，由Infrastructure/Devices/Plc定义、Station01Registration装配并在设备准入冻结，实际策略摘要进入manifest。正式装配与夹具同样准入，不保留万能PollMs旁路；不将策略放入businessMs/配方，不增加业务策略接口。

预算废字段迁移固定为schema 1.1→2.0、代表预算s01-budget-011-joint/1→2、模拟实例s01-sim-011-joint/1→2并联动budgetRef；模拟schema仍1.0，public配置不变。before用冻结原schema及含plcPoll的旧配置，after删除字段，两侧路径/版本/摘要/语义对应以验证合同V02.1为准。中性计量补丁与配置映射分开记录，不能声称所有输入文件字节相同。

设备实例冻结映射/策略；重连只换代次，定义变化重新准入。I/O、失联、受理、动作、保存预算保持原配置。正式PLC未知保持时间不能凭局部50ms获得兼容结论。

## 并发、资源与异常出口

| 路径 | 所有者/容量 | 期限 | 出口 |
| --- | --- | --- | --- |
| 周期读 | 一个协调器；每组due/在途位，业务连接单PDU | 原I/O含本地排队 | 慢轮次丢弃，不追赶 |
| 关键读写/停止 | 原串行动作；按A03.2有限K/R轮转，授权停止优先 | 原绝对窗口与I/O较小者 | 无资格不派发，结果未知不重发 |
| 心跳 | 独立连接与失效监视 | 300ms读、原3秒从最后有效翻转起算 | 迟到读不能先续期再判断 |
| 多消费者 | 版本化异步通知 | 各自原取消/截止 | 不唤醒检查同缓存；个人取消不取消共享采集 |
| 必要保存 | 原Recorder/业务端口 | min(原动作剩余,原CriticalSave) | 未提交/迟到不放料，保留真实取料事实 |

取消/超时/未知不自动转空闲，保留实际未完工作对应观察和限制。没有新增恢复或盲重试。

调度采用采集合同A03.2固定来源的PDU轮转：B与当前反馈轮转、K每块让出、局部首态到期插一块后恢复槽位、P至多两个普通R槽后获一次服务；快档不重置P等待计数。每轮完整时间为Σ交换+Σ块间等待+发布开销，各块年龄独立。V06的25ms单次、整组发布为仅after性能观察；局部75ms快档按V06.2继续硬判；单PDU≤25不能推出整组150ms或短态捕获。A03.4给当前阶段互斥关系、有限即时突发、容量条件与示例；实际是否达到仍NotRun，原截止和4950ms不变。

## 保存与恢复

DeviceActionEvidence关联实际反馈/坐标Identities，不能拿最新base冒充。保留现8192交换窗口、gap拒绝、翻转1024分段、取料提交前后分段及采集释放段。I1拆为正常降频HeldFlip原5000ms失败保存（未达阈值无需段引用）、独立真实TCP1023/1024自动阈值组件经生产路径/SQLite提交后的失败接续；独立Recorder缺口拒绝保留。仅通信证据内部提取共用测试边界，不改阈值/期限/模拟时序，不在代表链加流量；详见V07及T014/019/021。统计有界聚合，必要原始证据/诊断及真实等待提交不减少。

CommunicationEvidenceReader继续读历史plc-evidence/1及其原schema。历史观察按保存时事实读取，不拿当前在线值回填；无需数据库迁移或新恢复路径。

## 软件验证与证据计划

全部待后续实施和执行授权。细则见验证合同，指南见[quickstart.md](quickstart.md)。

| 需求 | 最小集合 | 证据 |
| --- | --- | --- |
| FR-002—009、SC-001/002 | 改前/后各5秒预热+60秒空闲 | 实际PDU、年龄、两类心跳延迟、相同2秒API负载 |
| FR-010—014、SC-003/006 | 所有权/慢读/切档/固定计划组件 | 无重复持续源/追赶、准备/重复唤醒减少 |
| FR-015—019、SC-004 | 原保护用例定向复用 | 中间态→完成→坐标、取消/期限、真实提交/gap |
| FR-001/023/025、SC-005 | run-2改前/后各一次 | 21轴子动作批、33轴启动、翻/放回各1、7媒体、NG保存/Final |
| FR-021/026—028、SC-007/008 | 完整010 L、受影响009、三类负例 | 发现/执行/原报告及清理承接 |

当前布局名义空闲18.333事务/s，运动38.333，翻/放回完成等待43.333，取放等待49.333；局部首次中间态53.333/58.333。均为设计推导而非实测或正式PLC能力，不受旧35～55初估机械限制。

验收按V01.1分侧：两侧共同满足真实相同业务、原保护、输入语义可比及计量/证据资格；仅after满足013确认周期/硬时效、1110空闲上限，工程时效单列观察、单源/循环零准备及新增语义门禁；跨侧要求空闲率与完整流程总事务下降、报告单位动作/证据处理量，并保持T_after≤T_before+4950ms。L、受影响009、013组件在最终after源码身份执行；两侧链各用自己的源码/构建/schema/config，已在同attempt同身份执行的门禁只引用一次，见V09.1。按V10分别保留原保护失败、优化目标未满足、不可比、NotRun、正式信息不足，不能把after超限无依据改称环境问题或扩大无关治理。

## 定向同步与清理承接

本表是后续拟修订，**本轮未改其他功能文档**。共享代码前完成对应spec/contracts/plan/tasks对齐；不改历史勾选。顺序：SY-01/02规则（含下述001预算合同）→SY-03/05语义→SY-04/06验证→SY-07消费者核对。下列路径均相对于specs/，同列plan/tasks指同功能目录。

| ID | 文件/冲突条款 | 后续拟修订 |
| --- | --- | --- |
| SY-01 | 003-plc-latest-protocol/spec.md FR03/FR15、plan.md心跳补充、contracts/status-notifications.md、tasks.md T065 | 当前50ms正文承接013；慢调度按相对到期迟延，不误报300ms正常间隔。T065旧“50ms不变”保留历史事实并补当前承接说明；3秒/I/O不改 |
| SY-02 | 009-plc-protocol-isolation/spec.md FR-008、plan.md期限/容量、contracts/protocol-maintenance.md P03/P04、tasks.md T022/读计划 | 当前Test50改用途周期；固定计划准入后复用，所有非法定义/连续范围拒绝保留；先于Accessor/配置修改 |
| SY-03 | 009同目录contracts/business-device.md、diagnostics-history.md E01/E02/E04及spec/plan/tasks观察/历史项 | base/position独立时间；Host位置四字段/API版本、Axis投影与消费者；原始格式/gap/段链/回执不变。共享输出前全部对齐 |
| SY-04 | 010-recipe-execution-isolation/spec.md/plan.md“轮询性能延期”、contracts/common-execution.md、verification.md VG-01/03/04、tasks.md必需集合 | 由013承接优化，不改010原完成范围；全profile完整L、013发现/执行核对追加，不缩为三类静态测试 |
| SY-05 | 011-plc-interaction-update/spec.md FR-001/013/016—023、contracts/plc-communication.md PC02—05、execution-and-state.md、plan/tasks T009/T010/T015/T024/T026 | 单源、独立位置时间、即时准入、首中间态局部例外与因果核查；保留正式T009/T010限制；明确完成后Observe、捕获释放及Axis投影消费者 |
| SY-06 | 011同目录contracts/verification.md、plan/tasks T027；verification-report.md、main-project-integration-20261004.md只读 | 当前验证正文承接一条代表链/必要组件；旧报告不改。明确harness观察器依赖和013计量入口，不能假ready或以multi03充基线 |
| SY-07 | 012-recipe-authoring/spec/plan/tasks、contracts/shared-integration.md、editor-ui.md、recipe-authoring-api.md | 配方保存/匹配/冻结/API沿用；仅状态共享处引用位置可靠性。Axis显示已有时间/可靠性，前端及2秒补查不变；不机械重写012 |

| 替代后删除/修订对象 | 有效义务承接 |
| --- | --- |
| PumpAsync整体高频循环、Axes重复反馈读、Stages额外坐标源 | 单协调采集；base全轴反馈；取放仍每200ms真实坐标核查 |
| Reset/AdvanceStart/CompleteInspection/轴最终确认的PollMs缓存等待与独立轮询 | 版本通知、即时base/到位实坐标；逐次取消/原截止 |
| Accessor每轮ReadPlan/ValidatePlan和任意字段热路径 | 准入实例绑定的有限准备计划；非法映射仍在I/O前拒绝 |
| PlcRuntimeOptions.PollMs、Host Test50/其他25、StagePollMs/PollAsync | 用途策略和有限局部例外；I/O与业务期限独立保留 |
| BusinessDurations.PlcPoll及当前fixture/schema键 | 已核实无采集消费者；按V02.1删字段并将budget schema升2.0、代表预算/模拟实例升2，联动run-2引用。先同步001 configuration-time.md的009/AL08当前预算段、budget.schema.json及对应spec/plan/tasks，再改共享模型/加载/准入；before原schema/配置与历史JSON不改 |
| 上项易误删项HeartbeatFlip | FullSimulation仍使用，保留；不能把500ms误称VirtualPlc1000ms或正式心跳 |
| 测试PollMs=5/10/50和旧固定次数/缓存/字符串预期 | 仅调整受影响夹具与错误断言；保留保护案例，验证实际事务/时窗/无后继，不改模拟时序 |
| scripts/start-station01-virtual-loop.ps1、verify-q01-q02-test-page.ps1当前hostPollMs=50说明 | 若仍用于当前计量则改真实策略/摘要；归档50保持，不能全仓替换 |
| Rules ProtocolBoundaryChecker选项白名单 | 只调整通信装配成员，不放开业务层协议知识 |
| Semantics整体时间、Axis顶层Identity投影、Evidence仅当前base | 位置独立身份；证据引用实际观察；旧持久数据仍可读 |

### DQ-02命中的具体迁移顺序

1. 保留未经013修改的主项目源码和原011 joint输入依赖树，含StorePrep完整源码/项目/实际锁文件、backend/Directory.Packages.props和实际构建导入；先审查项目引用/链接/复制资源/运行工具闭包，B从自身源码构建并使用自己的StorePrep/Host/VirtualPlc及测试产物；before的budget schema 1.1、预算/模拟version 1不迁移。R/inputs/before、after及两侧schemaRoot严格采用V02.1路径，禁止主项目新schema误加载基线。
2. 先定向同步001 contracts/configuration-time.md“009 / AL08 当前预算与配方应用合同”（现声明1.1）、budget.schema.json，以及对应spec/plan/tasks的现行预算规则；在SY-02/03/05/07相关合同标明新运行2.0准入、废字段删除、冻结摘要变化与历史只读。原1.1事实、旧任务完成和旧失败保留。
3. 随后才改backend/src/Gaode.Domain/Configuration/BusinessBudget.cs；Infrastructure/Configuration/ConfigurationLoader；Application/Configuration/ConfigurationFreezer、PublicConfigurationValidator和Application/Recipes/RecipeApplicationCoordinator.RequireBudget中对应的引用/1.1准入约束。新运行切2.0，不双版本回退；ApprovedExecutionCostProvider、RecipeExecutionBudget保留id/version/digest一致性校验，正常消费新版。历史RecipeApplicationHistoryReader保留已保存BudgetSource，不调用新运行准入重判历史。
4. 当前011 examples/joint/config/budget.json、simulation.json按V02.1迁移；共享该配置的活动run-1/run-2引用同步为2，现行input-manifest更新真实摘要。run-1仅做必要引用同步，**不增加完整链执行**。两侧013比较仍从冻结旧依赖树制作隔离输入，after只准表列字段差异。必要活动构造/fixture和JointInputDefinitionTests、Station01RunConfigurationTests旧版本预期定向承接；旧归档输入不全仓迁移，不用旧schema启动新产品。
5. backend/tests/Gaode.Rules.Tests/Architecture/009-public-shapes.json定向删除已移除共享字段的形状记录，保持其他边界门禁；同步013 runner、schema/config路径引用和最小输入核对。无影响的012保存/匹配/冻结、业务期限及API消费者注明沿用，不机械重写。版本迁移纳入既定组件/输入资格核对，不引出新全量测试。

## OPEN、外部依赖与决策记录

| 项 | 限制 | 可继续范围 |
| --- | --- | --- |
| DEP-013-01正式地址 | 未交付；所有地址/预算仅Test；仅建议按用途连续、明确保留区可读性，不改来源 | 固定计划、拒绝、Test对照 |
| DEP-013-02正式心跳 | 翻转周期未知；Test为1000ms，3秒失联保持 | 300ms读、Host应答/失效组件 |
| DEP-013-03中间态保持 | Test已有短脉冲源码依据但须实际验证；正式最短保持未知 | 局部50ms及安全失败验证、其余降频 |
| DEP-013-04环境 | 当前完整链依赖外部page observer，013需中性接线；另需现有Python/采集/算法及隔离存储 | 设计/实现/组件；条件具备后完整链才可通过 |
| DEP-013-05计量 | 已冻结方法/预算，实际摘要/构建/结果后续取得 | 不拿旧日志替基线，不现在运行 |
| DEP-013-06稳定性 | 原迟回执/瞬时阻断和失败保持 | 只查013直接影响，不扩全站修复 |

## 客户确认原型检查（P12）

无页面/宿主实现，未声称逐页审阅或修改原型。后端提供真实年龄/可靠性；基准API读取不代表重验012页面。

## 本轮流程与停止点

setup-plan已执行并核对：
FEATURE_SPEC=E:/dzk/gaode-1/specs/013-plc-polling-optimization/spec.md；
IMPL_PLAN=同目录plan.md；FEATURE_DIR=同目录。
原先无plan，setup复制模板后填写。BRANCH返回013-plc-polling-optimization是无Git时的目录回退，**不是Git分支**，无提交号；feature.json内容和修改时间不变。

按技能分派信号预算、观察合同、时序验证三项只读研究并汇总；使用当前主项目，不以旧副本覆盖。没有构建、测试、设备或数据库操作。CHK015保持NotRun；止于技术研究与Phase 1，等待设计审查，不生成tasks或进入implement。

首次plan收尾只做文档核查：八个本轮相关文件的本地链接无缺失；在预先记录的877个当前文件及新增文件中，变化仅为六份013设计产物和获准的spec/requirements修改，无删除；feature.json、AGENTS、宪章和核查范围内的其他功能/产品/测试/脚本均保持摘要及修改时间。tasks.md不存在。已复查.specify/extensions.yml，hooks为空，before_plan/after_plan均无附加步骤。这是写入范围与文档核查，不是软件测试结果。

### 2026-10-04 DQ-01—03增量修订

依据architecture首次审查和本轮定向授权，仅修订调度完整时窗/有限服务、两侧配置版本语义映射、验收适用侧及结果分类。setup-plan源码已先核查其“已有plan则跳过模板”分支；显式013目录执行后返回上述相同路径，原plan未被覆盖，feature.json保持原值/摘要。BRANCH仍仅目录回退；未进行Git写操作。按技能补充委派时序/配置两项只读研究，未重跑specify或重建设计。

修订依据为采集合同A03/A06、验证合同V01.1/V02.1/V06/V09.1/V10；同步实际受影响的research、data-model和quickstart。首次审查记录完整保留，本轮复核只追加architecture Notes，全部复选框保持[ ]。spec、requirements及CHK015 NotRun不改；没有构建、测试、设备或数据库操作，不生成tasks/进入implement。实际调度能力和正式PLC外部限制仍由后续对应证据判定。

2026-10-04时效续修仅落实A03.1/V06.1：响应收齐与后处理分开但完整C不漏，发布在投影/通知之后，按真实轮次身份归因；在既有通信诊断边界补有界逐阶段记录。未定位到的共同停顿保留未知，不改线程池、GC、优先级、扫描时序或期限。旧before共同计数/SQLite时长/证据量不重新标注源码；新增after诊断分辨率及自身负担必须在比较资格中明示。

2026-10-04诊断遗漏与派发续证：T013明确包含VirtualPlc/ModbusTcpServer.cs。设备侧requestGapMs是实际请求到达间隔，取消它的旧250ms告警推断，保留数值及慢响应/异常应答/原3秒故障窗口；Host仍使用plannedDue。T020只补A08列明的仲裁内部缺失时点与有限队列快照，不改调度行为。此次唯一新after要区分：Submit尚未实际入队、Drain恢复迟延、队列锁等待、Select耗时、Eligibility等待和在途交换/后处理；独立心跳仍以同事务应用收发时点分解。若仍缺内核到达与continuation就绪证据，只报告最小运行时/网络取证方案，不先系统调参。新增诊断及减少误报的日志行为属于after批准差异，保留原中性发送/服务结束/完整流程口径，实际成本不扣除；原before需重新核对资格。

2026-10-04当前身份必要组件发现I-HB-04失效发布次序竞争：T013在原状态锁内一并发布Failure、不可用观察和递增代次，避免日志窗口夹在两次发布之间；失败证据与原动作回调保留失效前代次。复用I-HB-03/04两行核日志入口真实观察及新动作拒绝，不改公共合同形状/原期限，不把此局部修复解释为366.7ms派发根因。原attempt07失败与未启动after事实保留。

2026-10-04内核分析11定向实施承接：通信层新增原I/O单调绝对截止复核，修复真实排队约1184.8ms后仍按未触发取消令牌派发的问题；只承接现1000ms而非改变预算。内部TimeProvider默认系统时钟及异步截止上下文留在ModbusTcpClient.cs，业务端口不变。T012/T018/T026—028按新身份重核；原51行加两个直接截止故障数据行，总53行。系统Ready等待仍是独立未解决时效，不用此修复解释全部峰值。


2026-10-05 C盘独立续查（run16）：固定归档只读，before/after各自源码重建，所有新库/日志/计量/临时文件落C盘。只先运行I-FAST-03、I-FU-01、I-HB-03/04一次有界诊断，不替代53行或正式对照；原80ms/期限/所有断言保留。为承接attempt13已证实同PID的Infrastructure与VirtualPlc诊断副本争写，PlcTimingTrace输出改按PID及实际程序集身份命名，payload登记组件身份；不移动时点、不修改记录容量或运行调度。比较器明确识别新文件，旧文件仅用于历史只读重算，双份候选拒绝。现有翻放用例在finally保存设备实际动作/写入审计、既有代理报文和设备诊断，含时基锚点与gap，不以安全拒绝替代正常成功断言。新增记录只用于失败定位，不能证明内核完成/Ready或解释所有GC相关峰值。T014/T020/T022/T024承接上述诊断；T028—T030保持未完成，正式性能必须先有负责人确认的运行环境条件。未改变业务端口或53项固定义务。


2026-10-05 主项目012合入承接（run16，T009/T022/T028）：当前完整组件实际发现I-CONFIG-02被RecipeCurrentSchemaRequiredForSave拒绝。RC09已要求正文3新保存，活动joint/catalog仍正文2，属于输入迁移遗漏，不修改有效断言或唯一校验器。只将主项目活动011联合Test夹具迁为正文3：明确选择软件测试夹爪1（同现有Recipe011Data.Candidate的受控Test选择，不是历史默认或PLC信号），每个实际相机坐标按原唯一stage/localFace/camera目标显式引用原detect配置，曝光/增益/亮度、机械坐标、动作时序、算法、媒体和业务预算均原样。同步活动run-1/run-2目录摘要和input-manifest；不增加run-1完整链。013固定归档、派生before/after及原性能输入均不改；主项目新输入只用于合入验证，不能与旧before直接计算收益。原正文2读取及拒绝直接新保存保护保留，不增加自动升级/兜底。该输入/设计身份变化使主项目最终L和53行须在新身份下重核；既有失败保留。固定5项诊断前置必须按既有入口启用GAODE_013_MEASUREMENT_ROOT，其余组件不启用额外采集；这不是放宽诊断断言或新的验收旁路。正式性能仍等待负责人确认的同条件窗口。
