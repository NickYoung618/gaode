> 2026-09-27 当前实施与验收状态见evidence/completion-review.md及evidence/task-audit-night-20260927.md最新节；下方带日期的“待实施/尚未修改/NotRun”是当时记录。复位观察同步和真实权限拒绝绑定已实现并验证，原失败保留。Test选定主流程验收完成，008整体未完成，T055/T070原条件未全齐。原要求、共享接口及生产局部限制不因本入口改变。

> USR-E/宪章7.0.0当前设计见[六项问题交接](plan-six-issues-alignment-20260926.md)。问题1—5根因待运行核验；先协议/诊断与当前配方代表验证，再接USR-D完整新轮。下方带日期的旧状态不作为现状，tasks本轮只读。

2026-09-27接续T054/T069启动子范围：实际持久化命令/未完成运行初始化先于app.Run及PLC服务开始，以同一个初始化任务供hosted StartAsync复用；真实查询、恢复和服务停止次序保留，失败阻止启动。来源`.specify/bugs/008-startup-persistence-order/assessment.md`。该内部启动次序不改变业务接口、模型、协议或原deadline；独立构建及生命周期/正式页面代表验证前不宣称通信根因解决。

2026-09-27 实施检查点：下述夜间补缺已按先合同后代码执行。T057固定合法目标冲突/占用检查、预留→取料在途→放料/ACK占用提交，T051 3.2-test实际取放/持久读写计数和局部期限，T068普通暂停安全边界与同run继续已接正式Host；必要定向/API检查及r5 Q01-PAUSE、Q01-NG、Q02-PENDING正式WPF通过。具体构建适用范围、原失败、剩余原条件见[evidence/implementation-night-20260926.md](evidence/implementation-night-20260926.md)。任务原编号、历史及质量清单保持；仅T058保留已确认完成勾选。当前正式批次复用有效交互worker；必要接班用reloadWorkerRoot先验证后继就绪再结束旧worker。登录自动启动配置在本批验收结束后处理。

2026-09-26夜间授权实施补缺：沿T057在ThreeStageWorkflowExecutor复用现有固定Test目标执行一格冲突/源占用检查、全体预留与完成占用，不建设动态分区或新容量配置。LatestProtocolStageActionAdapter接既有IStageEventStore，在真实取料完成后、放料数据写入前提交在途事实；提交不确认则UnknownHeld保留。沿T068/001 T052给Coordinator加入原run安全边界暂停等待，由公共准备、检测及盘末消费；夹紧已派发时继续观察反馈/期限，结束并保存后才Paused，继续恢复原阶段。沿既有TraceWriter/StageEvents写暂停/继续事实，不延长业务deadline、不增加UI控件或故障恢复旁路。受影响测试与最少页面路线在无活动冻结job时串行构建/冻结；已有不同构建证据按影响限定复用。现有任务编号/条件保留，不因发现缺口重复追加。

T051关联核对发现当前sortingMs仅为每SortUnit一份XyCompletion＋一份CriticalSave，未逐项覆盖真实取料与放料两段及新接入的预留/在途提交。后续仍按原E06的实际动作公式计数修正并版本化：取料/放料的受理与机械上界、ACK、实际必要保存分别计入，从本轮冻结绝对起点推导；不改变各动作配置上界、I/O或心跳值，不手动延长运行中deadline或原失败重试。已结束3.1包的实际未超期事实保留，但不冒充最坏合法取放预算齐备。该缺口仍由T051原条件承接，不添加通用超时优化任务。


2026-09-26 PARAM事实增量：按共享API合同保存实际CaptureFact/RequestedCaptureSettings并从现有媒体查询投影；008 T054、003 T068承接，原编号及完整条件不变。



2026-09-26 USR-20260926-D恢复设计：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替代旧U05同轮单指令重发。双端复位、真实初始状态成立后显式新启动；旧运行和证据保留。设计尚未实施，tasks保持只读，原编号和勾选不变。



2026-09-26旋转Test增量：T065/T067按[虚拟旋转请求/结果合同](../003-plc-latest-protocol/contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。



2026-09-26 人工子范围增量：T060/T068按[人工执行合同](../003-plc-latest-protocol/contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。



# 技术方案：配方驱动的完整检测执行



**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。



**功能标识**：008-recipe-driven-inspection  

**日期**：2026-09-24  

**规格**：[spec.md](spec.md)，含当日三项澄清  

**宪章版本**：7.0.0  

**状态**：2026-09-26既有spec/contracts/plan/tasks文档已对齐；代码待实施，旧analysis保留历史，新复核单独记录。外部输入仅限制实际依赖的路线。



**实施进度（2026-09-25）**：上方状态是计划编制时快照。第五批Q01/Q02已按版本化虚拟Test映射从正式WPF到Final；[实际证据](evidence/fifth-batch-q01-q02.md)与[当前索引](evidence/index.md)优先。以上是旧协议历史事实。新版尚无端到端证据，后续按[实施清单](implementation-checklist-20260926.md)优先Q03普通OK完整多面链。



## 方案摘要



E接入在IntegratedDetectionPort现有逐步入口消费Position(E)/ReadECode，复用真实媒体/worker/保存和003扫码Z握手；目标、身份和预算依[003 E合同](../003-plc-latest-protocol/contracts/e-test-execution.md)。码失败继续不影响F唯一绑定，也不释放未知机械状态。



2026-09-26连续收口设计：多成员/部位目标采用[多对象Test共享合同](contracts/test-multi-object.md)，增量复用目录、冻结计划、初始测量解析、检测及外围分拣，不新建替代引擎。T050/T052/T053/T057/T060/T063/T064各按实体子能力交付，正式页面证据仍由T066承接。



在现有正式Host中补齐“冻结配方计划→实际动作→分层结果→适用处置→最终完成”，复用公共准备、Modbus、采集端口、独立worker、SQLite、外围整盘编排和006页面。

复用旧协议已通过的普通单面链，先完成新版Q03普通OK两面正式页面到Final，再覆盖一面两面及四面3CD＋1AB及更多面/额外E的必要代表，补足成组、整体、旋转、人工交互和必要失败。

第一阶段就接入配方选用、下料和最终状态；不能等所有后端能力完成后才开始页面联调。



本次选择**扩展现有线性步骤计划及一个应用层执行入口**。这是基于现有代码的成本选择，不是禁止通用引擎的项目规则；理由与替代方案见[research.md](research.md)。

历史第四批沿同一`IntegratedDetectionPort`接入`MotionCoordinator`和显式目标记录；`RecipeExecutionCoordinator`保留步骤/身份门禁，公共移交只解析冻结负载中有完整来源的目标。组件Test注入目标用于验证PLC、相机、worker和SQLite的实际调用，不改变正式Q01/Q02受限状态。单图必要保存及本轮复位先于下一定位；第二输入复位后同入口执行融合。

不同面序列使用同一执行逻辑和数据化用例，不复制22套实现。先顺序执行必要采集/worker/保存，避免为当前目标增加并发调度框架。



| P13阶段边界 | 当前方案 |

| --- | --- |

| 起点 | 006从后端加载预先准备的配方，选用确定版本并从原型正式控件启动；公共3D/F实际完成，F唯一绑定与所选版本一致后冻结产品计划 |

| 终点 | 全部必检及应处置实体收敛、必要保存完成、无未知在途件，完成下料、解锁、适用取盘确认并在前端核对最终提交 |

| 必须参与 | 006/WPF/WebView2、正式Host、唯一运动准入、独立VirtualPlc/Modbus、虚拟采集/光源、独立worker、配方目录、SQLite和媒体存储 |

| 必要验证 | Q01—Q06及按实际配方/差异选定四面3＋1代表完整前端运行；C01—C08实际差异及F1—F6必要失败，按覆盖缺口复用，不做全组合 |

| 证据 | 同一run的前端配方/操作、F绑定、实际步骤、媒体/worker、保存及Final；状态分Passed/Failed/Blocked/NotRun |

| 延期 | 新增示教、前端编辑保存、通用脚本/流程编辑器、无当前需要的兼容层、全故障矩阵、性能长稳、生产精度及现场验收 |



## 技术上下文（Technical Context）



| 事项 | 沿用/选择 | 已知限制 |

| --- | --- | --- |

| 后端 | 已有C#/.NET 10、ASP.NET Core单Host及分层目录 | 不升级框架、不另建服务 |

| 前端/宿主 | WPF/WebView2、当前HTML/JS运行入口及TS辅助模块；按实际build.mjs接线 | 当前构建复制runtime.js，不能只改未被打包执行的TS文件；不是另起Vue重写 |

| 配方 | JsonRecipeCatalog、RecipeCatalogFactory、RecipeRunPlanner及不可变运行快照 | 现有catalog API只有摘要；目录/模型尚有限制，见研究表 |

| 设备 | 最新PLC适配器、单一逻辑设备会话/运动准入，保留已有业务与心跳连接隔离 | F握手已有实际组件实现，复用并验证新版身份/受影响整链；E按本次确认复用F式握手；真实设备未明确部分现场确认 |

| 采集/算法 | FileBackedCapture、媒体存储、PythonWorkerAdapter/WorkerProcessSupervisor | 已有独立worker单/双输入融合；仅新身份消费需适配，E按场景表和本次决定实施 |

| 数据 | EF/SQLite、TraceWriter、StageEventStore和独立StorePrep | 只新增必要事实/查询；正常Host启动不自动迁移 |

| 验证 | 复用007脚本、006页面操作及现有定向测试 | 已有参数化fixture及正式页面采证；新版schema/来源与Q03需适配，本轮未运行 |

| 预算 | 配方实际动作数量与既有采集3—5秒、worker每次10秒等Test配置计算 | 取消把旧120秒套到所有路线；不降低3秒心跳保护掩盖问题 |



没有待选的编程语言、框架或架构方案。外部未确认字段单独列在[执行合同](contracts/execution.md)；它们不伪装成已解决的技术研究项。



## 当前实现与最小增量



完整“能力→代码→证据→缺口→复用”表见[研究记录](research.md#当前能力核对)。

历史实施前快照：当时已有单面链，规划器仍有重扫/面轮耦合、正式Flip受限。该缺口列表不是当前代码现状；当前普通Q多面及正式页面证据、共享能力与新恢复缺口见[计划交接](plan-restart-alignment-20260926.md)。

历史实施前口径：当时新版0/22、Q03仅组件。当前普通Q01—Q22页面证据已存在，适用其原构建/配置；USR-D恢复尚NotRun。任务条件和旧勾选不因路线Passed自动变化。



## 宪章检查（Constitution Check）



“符合”仅指设计；运行是否通过必须另有证据。设计前按旧plan核对，设计后按本版复核。



| 原则 | 设计前 | 设计后 | 依据/剩余限制 |

| --- | --- | --- | --- |

| P01 来源和冲突 | 违反待修正 | 符合 | 最新spec优先；清理旧双配方、仅表达及维护门槛；alignment-audit记录 |

| P02 核心接入 | 待补充，仅限制所列部分 | 待补充，仅限制所列部分 | 正式链及适配职责明确；F及页面已有能力复用；B表剩余输入只限制依赖路线 |

| P03 配方顺序 | 违反待修正 | 符合 | 当前适用路线与必要代表实际完整执行，不以变体总数为实跑门槛 |

| P04 动作依据 | 待补充，仅限制所列部分 | 待补充，仅限制所列部分 | E02—E05保留有限等待、必要阻断；未知现场输入仍受限 |

| P05 唯一控制 | 符合 | 符合 | 动作归属表；006只调用后端，VirtualPlc不编排业务 |

| P06 资源 | 符合 | 符合 | 顺序执行、有界媒体/worker租约；前批不等后批融合 |

| P07 身份事实 | 待补充，仅限制所列部分 | 符合 | data-model定义组/成员/整体/位置、结果分层及未知状态 |

| P08 保存冻结 | 符合 | 符合 | 版本冻结、真实保存、维护迁移；旧记录不回填 |

| P09 诊断 | 符合 | 符合 | 持久结构化日志，F1—F6定向失败可定位 |

| P10 缺失输入 | 符合 | 符合 | B表逐项来源、最迟确认时点、可独立推进内容 |

| P11 架构选择 | 违反待修正 | 符合 | 以现有实现选择线性步骤；不预设引擎禁令 |

| P12 原型 | 待补充，仅限制所列部分 | 符合 | 原型只读，006只绑定既有控件与新状态；待验证 |

| P13 完成标准 | 违反待修正 | 符合 | Q/C/F矩阵、前端完整证据；受阻与未执行不计通过 |



## 结构与职责（Project Structure）



| 现有位置/拟增量 | 所有者及职责 | 最小修改 |

| --- | --- | --- |

| Application/Recipes；Infrastructure/Recipes | 配方装载、校验、冻结 | 补可选配方摘要、实际点位与对象映射；解开场景与机械路线的错误绑定 |

| Application/Station01；Host/Api | 正式启动、公共准备、F绑定与handoff | 保存所选配方引用并与F结果比较；保留公共配置独立 |

| Application/Workflow | 外围阶段及产品执行唯一所有者 | 在现有IDetectionPort边界接入应用执行入口，按需提取面/对象聚合；不预建插件体系 |

| Application/Motion；Infrastructure/Devices/Plc | 唯一设备准入、动作与反馈 | 已确认产品定位/复位及后续动作复用端口；保留心跳独立 |

| Infrastructure/Simulation、Algorithms、Media | 实际采集、进程协议及媒体租约 | 从适配器移出业务步序；增按对象/面/相机取素材和双输入调用 |

| Infrastructure/Persistence；StorePrep | 实际提交、查询与维护 | 最少新增分层结果/动作事实投影；优先复用事件及现有表 |

| frontend/src/runtime.js、既有页面绑定；desktop | 006独立前端实现 | 原型现有选择/显示控件绑定后端；解除S1/P01限制，核对实际构建入口 |

| VirtualPlc；scripts | 003设备事实、007测试环境和采证 | 已确认信号才扩展；脚本参数化，不重建平台或代替页面发业务命令 |



### 动作归属与执行顺序



| 计划动作/阶段 | 执行所有者 | 不可省略的结束依据 |

| --- | --- | --- |

| 公共准备/3D/F | 现有Station01公共协调器 | 本次反馈、采集算法与必要保存；唯一F绑定 |

| PositionForCapture/Capture/ReadECode | Detection内应用执行入口 | 到位/适用轴→采集→本次算法与保存→复位；E需扫码合同 |

| FlipMember/逐面目标续接 | Detection内同一应用执行入口 | 正确实体取件定位、型号/目标面翻转、另定位放回并可靠完成，解析当前面合法目标；翻转放回后统一3D姿态复查、F不重绑，整体实体只翻一次 |

| TransferToRotation/Rotate/ReturnUnit | Detection内特殊路线 | 当前014：可靠上料/本次R绝对目标及ActualR/本件处置与safe；Host业务角度由共同Stages表达，通信映射内部编码，不沿旧Test无数值角契约 |

| DecideUnit | 应用结果汇总 | 单图→面→成员/整体/组，完整性与质量分开 |

| SortUnit | 普通路线交外围Sorting；特殊出口在Detection内处理 | 应搬实体真实源目标；已出站处置的实体不再次盘末分拣 |

| UnloadTray及Final | 现有外围整盘编排 | 全部依赖提交、无未知在途、下料/解锁/取盘/Final分别成立 |



计划中的全部动作必须有明确所有者及消费记录。Sorting/Unload交接不是静默丢弃步骤；按配方合法不需搬运的普通OK，保存“不需搬运”的判定依据，不能伪造抓取。



## 数据、契约与状态



[data-model.md](data-model.md)定义配方选择、冻结计划、实际对象位置、面/组/整体结果及动作事实。

[API合同](contracts/api-results.md)定义目录条目、启动引用、绑定核对和查询投影；[执行合同](contracts/execution.md)定义动作、预算与B依赖；[证据合同](contracts/evidence.md)定义前端完整验证。

只在共享合同及消费者任务对齐后改代码；当前字段均为目标增量，不声称现有API已经提供。



## 配方共用逻辑与动作隔离



先使用独立Test目录及已有Review加载路径，保留simulationOnly用途；不把测试目录伪装成生产File目录。

每条测试配方预先定义场景、对象、面序列、点位/参数/预算引用及唯一F码。前端选择仅记录本次期望配方，实际F成功后仍由后端唯一解析并一致性校验。

一个运行使用同一目录快照。准备新版本不改在途快照；现阶段在无活动运行时重启/重新装载既有目录，使下一盘采用新版本，不建设热加载或发布系统。

同一程序版本新增配方的验证可复用Q01及其参数变化版本；版本差异必须进入实际调用和保存。



### 配置与策略扩展设计（P11）



选择在现有RecipeStepKind和端口上实现明确动作分派；场景差异集中在对象映射、汇总和处置。新类仅在职责实际需要时提取，不规定“每动作一个策略类”。

现有能力内新增序列只改数据；真实新增工艺能力才增加动作与已确认适配。无合法动作实现、必要参数或依赖时，明确受限，不跳过后报成功。



## 并发、资源与异常出口



先串行运动、采集及单图算法；普通A整批再B、C整批再D。每图实际分析并保存后复位；配对媒体落盘，前批不持有worker等待后一批。同对象/面/轮次第二输入齐后再执行独立融合。

双输入使用同一worker进程及现有租约，适配器扩展显式1/2输入合同；本期不建设一般DAG或多worker池。

心跳、停止与PLC安全观察不等待算法、存储或页面。必要超时形成可查询原因；算法失败为有限Pending，物理反馈未知保留占用，必要保存失败阻断后继。



## 保存与恢复



动作意图先提交，派发和匹配反馈另记事实；媒体、单图/面结果、实体处置和Final复用短事务保存。不把数据库和设备动作视为一个原子事务。

普通下料/分拣/盘末顺序保持。故障恢复按[003双端复位合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)：关闭旧执行、保存故障、真实初始成立后显式新run从公共准备重做全链。人工换面同轮采用命令目标面并保存默认来源，正常换面完成放回后统一复查3D姿态，F不重绑；完整机械恢复矩阵延期。



## 开发阶段、顺序与完成条件



以下S0—S5为覆盖/开发分组，不是新版执行的全项串行前置；新版Q03优先按本页当前口径及实施清单推进。以下S0—S5为开发阶段；当前tasks按这些依赖拆分，编号T049—T070及唯一跨功能所有者任务。



| 阶段 | 用户可见结果/修改范围 | 前置输入 | 覆盖 | 必要验证及完成条件 | 尚未包含 |

| --- | --- | --- | --- | --- | --- |

| S0 首条路线准备 | 处理心跳、接入已定义F合同；准备Test目录/身份/预算、API及必要同页控件 | B01-F实现验证、B04相关语义、B09；无需等旋转/组策略 | 首条路线准备；FR001/013/016/018 | 合同和数据可执行、准备脚本支持选定用例；未运行不报E2E | 所有Q仍未通过 |

| S1 首条单面完整路线 | 前端选Q01配方→公共准备→AB实际定位采集/融合→普通OK无需搬运→保存→下料/解锁/适用取盘→Final | S0；B01/B04/B09-U/H，所用公共ACK依据 | Q01；C06下料部分；FR001—004/011/013—018，SC001/003/005 | 同一run完整页面/设备/worker/DB/Final证据；F不匹配、保存失败等直接改动风险定向验证 | 不声明分拣/多面/组/旋转完成 |

| S2 单面扩展与普通分拣 | CD、多槽和非连续槽；真实NG/Pending源目标；新增配方参数实际生效 | S1；搬运B02/04/08；仅混合处置依B06 | Q02及Q01变化版本；C01/C06/C08；FR003/010/017/018 | 前端完整CD及必要分拣运行；原快照不变；失败按矩阵复用 | NG/Pending可分开运行；不猜混合策略 |

| S3 多面及当前适用代表 | 自动/人工换面、逐面目标续接（完成本轮相关对象翻转放回后统一复查3D姿态，F不重绑）、配方时机E；通过前端运行其余完整序列 | S1；自动B02/04，人工B07/B09-U，E另B01/06 | Q03—Q06及选定四面3＋1代表、C02；FR005/006，SC001按实际差异给完整证据 | 每对象完整两/四面到Final；逐面合法目标、初始测量保留、已完对象不重复；复用同一页面脚本 | 特殊/组差异不能由基础Q通过替代 |

| S4 组、整体与旋转差异 | 多组成员汇总/处置，普通S3部位及整体一次搬运，场景1特殊按OK号逐件上料/两组四采集/原槽或NG-Pending处置/safe | 所需S2/S3；S2 B05，旋转B03，取放B02/04/08 | C03—C05；FR007—010；复用适用Q序列 | 分层结果身份正确、源目标真实、共享实体不重复动作；各适用出口实际完成 | 不增加其他特殊类型/任意混装 |

| S5 必要恢复与覆盖收口 | 必要人工恢复、补剩余失败/证据缺口，前端结果与保存一致 | 对应路线及B07/B09-U | C07、F1—F6及SC001—006最终核对 | 当前SC001选定路线、C01—C08差异及必要F均有适用证据；当前构建影响范围补验 | 示教、编辑保存、长稳及完整边界矩阵延期 |



S2的分拣分支受阻时，已明确的多面检测能力及普通S3结果模型可独立推进；依赖缺失的完整路线仍不得计通过。S4普通S3无需等待旋转B03。

首条路线选择合法OK输入只能用于证明其完整路径，不能用此选择取消后续NG/Pending处置。

每阶段只验证已覆盖范围；如新F握手或必要页面确认尚未实际接入验证，S1仅能交付组件/集成结果，完整E2E继续Blocked。



## 软件验证与证据计划



[recipe-cases.md](recipe-cases.md)保留22个历史编号并区分允许变体、当前配方及必要代表；[coverage-matrix.md](coverage-matrix.md)映射Q/C/F及M来源。

一面两面保持既有要求。四面选择合法3CD＋1AB配置代表；原Q09三AB退出当前范围，另按实际新增更多面/独立E配置补必要代表；按实际采用配方和场景差异增补或复用，不固定永久数量，不引入非法混装。

用例数不是新硬门槛；最终要求是008 SC-001当前选定路线及C01—C08实质差异有证据，不要求14条或8条全部实跑。页面操作/采证脚本可复用，不能用后台直调抵扣某条前端运行。

必要失败按F类别选最少适用场景，不与每Q交叉。相同代码/合同且未受改动影响的历史局部证据可引用原范围，新路径和当前完整页面链必须新证据。

操作步骤见[quickstart.md](quickstart.md)，全部标明“本轮未运行”。



## OPEN、外部依赖与决策记录



[执行合同B表](contracts/execution.md#阻塞登记)列出每项输入、来源、影响、最迟确认及可独立推进工作。

首条完整路线优先实现并验证新定义F 3/4握手，接入前端配方选择与必要同页控件，核对产品轴/高度含义并处理心跳阻塞。

技术研究已作选择；现场合同和客户页面决定不能通过资料推理伪造。它们未关闭时只将依赖部分判为不可直接实施/验收，不中断无关设计。



## 客户确认原型检查（P12）



只读检查原型ZIP：SHA-256 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`；a.html/data-view.html/login.html。

a.html已有配方配置按钮、产品型号选择框、只读配方ID及顶部版本区，优先直接绑定。既有页面控件按006独立规格绑定数据/状态；当前原型归档只读，不新增控件或改变交互。

后续页面仅绑定新身份/阶段/媒体/取盘状态，保存原型不变摘要与必要展示证据；本轮未改页面。



## 下一步文档门禁



本次speckit-tasks已更新008及直接协作功能任务；下一步speckit-analyze只读输出新报告，再据具体输入条件安排implement，本轮不实施。

旧48项tasks逐字节保存在tasks-history-before-s0-s5-20260924.md，旧analysis原文不动；不得直接照旧T002→T006执行。用户本次授权的001/002直接共享spec/contracts/plan和任务已作最小对齐，见alignment-audit当前首节。当前批次改为新版Q03优先集合；B04只阻断其具体缺少目标依据的动作。

# 旧协议历史：第五批实施增量（2026-09-25）



先按[虚拟 Test 映射合同](contracts/test-virtual-mapping.md)使Q01/Q02目录在配置完整时可用；公共3D结果经严格移交解析到现有检测单元，保留冻结计划和摘要。复用第四批Modbus/采集/worker/SQLite执行链，接通既有下料、解锁、页面确认及Final。先Q01后Q02连续运行，当前构建页面心跳须以同一Test运行包核对。001公共移交、002目录、003运动/结束、006页面、007夹具各按原任务归属；不修改真机生产映射。



## 当前后续实施口径（2026-09-26）



旧第七/第八批设计与组件事实只读引用[eighth-batch-auto-multiface.md](evidence/eighth-batch-auto-multiface.md)，其中两组翻面字段阻塞和二次3D要求已被新协议替代。



先共享协议身份/Flip/Unload→多面计划与合法目标/数据/预算→正式页面Q03到Final→其余适用两/四面。普通OK无需等待全部分拣、E、旋转或组策略；NG/Pending、人工和业务差异继续由原S2—S5任务收口。每片实现前置是所用能力/数据交付，不要求所有父任务全勾；整项验收仍按原未完成范围加本次增量。



新合同定义在execution.md、data-model.md及test-virtual-mapping.md，详细消费者/停止点见[实施清单](implementation-checklist-20260926.md)。本轮止于文档与只读一致性分析，不执行implement。



## 派生时序增量（2026-09-26）



[sequences.md](sequences.md)补Q03同run主链、AB批采/逐图分析与对应Z复位、逐实体Flip/ACK、同盘取放/ACK；公共3D/F详见001 sequences。来源差异由[本轮追溯](sequence-alignment-20260926.md)按明确确认依据处理，不以日期或旧报告裁决。现有线性执行入口、模型与API保持，任务仅追加图示步骤验收引用，不增加新框架/寄存器。宪章P01/03/04/07/08/09/10/12/13设计核对成立；生产轴标定/特殊旋转/人工恢复仍局部受限。图示完成不计实施进度。



## 本次确认的最小实施增量



依据[business-decisions-20260926.md](business-decisions-20260926.md)，按现有能力依赖分批推进：



1. 结果汇总与分拣：有NG优先NG但保留Pending面；S2只剔除问题成员，S3仍整体搬运。复用现有结果/实体/取放能力，当前先OK主链，补一个必要混合结果与成员处置代表验证。

2. E扫码：源Excel K19/Q19决定对象，配方提供当前Test扫码坐标；复用F式运动/复位能力但保留E/F不同失败放行规则，E缺码写入问题记录后继续。

3. 人工换面同轮确认与占用/确认清零后采用目标面并标注来源；故障双端复位/初始检查后新启动完整新轮，隔离旧预算、结果和反馈，不能同operation attempt=2续接。

4. 旧2026-09-26旋转Test记录：Host发逻辑动作等VirtualPlc，旧无数值角度设计已被014绝对角和本次实际反馈设计替代；旧证据范围不变，正式地址仍不编造。此设计工作不再以现场角度缺失为阻塞。

5. 查询：现有事件/结果投影保存run、组/成员、面、步骤、问题、处理方式和待现场项，006只绑定已有区域。现场真实参数延期，保留合法Test数值和必要保存/反馈校验。



共享接口改动须先同步对应003/006/002的spec、contracts、plan、tasks；本文件不声明该共享设计或代码已交付。验证复用现有Q证据并按改动补相关回归，不重跑无影响的全部组合。



## USR-20260926-D当前计划（优先于旧恢复条款）



范围为008既有普通/组/整体路线及直接共享恢复设计；起点为已保存故障，终点为双端真实初始成立后正式页面显式启动的完整新run Final，或准确InitialBlocked。必需参与Host、VirtualPlc、实际相机/worker、SQLite/媒体与006正式WPF页面；本轮仅设计不运行。



技术沿现有.NET Host/Application/Motion、单写TraceWriter、独立worker及WPF/WebView2，不增加通用恢复框架。规范流程、实体及API见[003恢复合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)、[data-model](data-model.md)、[派生软件时序](sequences.md)；研究和取舍见[research](research.md)，唯一消费者/任务归属及证据状态见[计划交接](plan-restart-alignment-20260926.md)。



设计前旧单指令恢复违反最新P01及身份隔离P07；本次设计后P01/P02/P03/P04/P05/P06/P07/P08/P09/P10/P11/P12/P13按现有职责、真实初始门禁、保存和原型只读边界符合。该结论仅设计门；实现尚不符合新恢复，C07/F5未运行；生产特殊夹具状态/初始安全范围无依据时局部受限，不用假值填平。质量清单保持15/16，历史实现备注不重写。



GROUP只处置问题成员、NG汇总保留Pending面，E按源Excel对象/用户确认复用F式§3.1.7、失败记录后合法继续，旧旋转Test角度归PLC的记录仅历史；本次共同Stages明确绝对角，通信核本次ActualR，问题用已有日志/查询/区域；已实现能力直接复用。002配方合同不需改变：选用合法版本继续走原校验/冻结，但新run重新测量和绑定，因此本轮不修改002。



必要验证只覆盖C07/F5新轮/拒绝/隔离/历史可查与暂停人工对照，按消费者影响回归代表普通/多面/E/旋转，不重跑所有Q或扩故障矩阵。旧U05证据不可抵扣，预算及I/O/心跳保持。本轮完成Phase0/Phase1后停止，tasks/analyze/implement均未执行。



## RES真实结果展示最小设计（2026-09-26）



008 T054先交同run对象/面/项目及媒体关联的持久事实子能力，003 T068投影后由006 T049展示；008既有T055/T059等适用路线与T069新恢复验收复用同一页面结果证据，不新增008前端实现任务。完整结果展示不等于008整体完成。



依据HMI-003、DAT-004、008 FR-016及006 FR-010，唯一设计/验收与任务细化建议见[结果展示交接](../008-recipe-driven-inspection/plan-result-display-alignment-20260926.md)。本增量不新增需求，冻结契约后下一轮细化既有tasks再实现。保留USR-E六问题、四面3＋1代表集合、USR-D子交付链及RST-01/RST-02结论；VirtualPlc延迟模式不推进。宪章7.0.0 P02/P05/P07/P08/P09/P12/P13检查：后端真实事实、身份/保存、原型边界和必要代表验收满足设计约束；没有新平台或全排列测试。设计不代表代码/页面已通过。



USR-D实施细化：双向恢复关联与initialCheck消费由003恢复合同规定的单Writer短事务提交；新轮Created仅受理占位，不授予物理派发。复用T054保存门禁及T068编排，003 T072-B只消费其结果。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

在现有QueryEndpoints/CommittedResultProjection接处置事实；SortingTargetAllocator及IntegratedDetectionPort仅补必要载荷。

执行调整（2026-09-27）：当前共享PLC启动通信阻断，队列保持暂停且无活动WPF/job。已实际自动reload并确认新worker9936先就绪、旧worker退出；现在可串行实施处置投影及纯投影必要验证。原默认二进制/fixture保持冻结，新测试采用独立artifacts输出；剩余原队列保留，正式复验须另记实际构建，不能把新源码测试算成原冻结二进制验收。登录启动配置仍延后。

P03代表使用`generate-q02-pending-p03-test.py`生成独立新目录，并将Q02-PENDING-P03加入当前采证白名单/预算定位；不改变原配方。实际通信失败诊断在既有有界窗口记录本进程CPU累计、启动时间和优先级，不查询其他进程，不修改I/O/心跳期限，不以诊断字段推定修复。

当前采证入口将显式本地Test HostDll/PlcDll传给既有环境入口，预算选同Host构建目录并校验实际加载位置。独立构建保持原默认程序只读；每worker仅一个实际构建，构建改变自动reload，无人工批次重启。现有T050/051/054/070承接资源身份和必要验证。

2026-09-27 P03正式路线在分拣前暴露fixture目标绑定缺失。原1.1.3及失败包保留；实施独立1.1.4，仅给P03绑定既有同盘P15 Test目的点，源点仍为P03。当前job清理后才切换工具fixture入口；Host/PLC构建不变、worker复用，不改变期限或业务协议。

P03修正后的新job002尚未到WPF即Connect超1秒，真实诊断有进程CPU上下文。启动脚本同时冷启动PLC/Host且未在Host前等待PLC健康；只修正Test启动依赖顺序，在既有准备窗口内等所属PLC健康后启动Host，仍保留1秒I/O/3秒心跳。旧构建此前该探针未消除后续读延迟，不声称本改动解决全局通信；新明确构建只作一次短WPF预检，失败即保存诊断而不批量盲派。

job015对照：PLC于17:50:02.2134893Z已返回完整首个心跳响应，Host于17:50:03.2899122Z才记首部读取完成，相差约1076ms；CPU/GC窗口不足以确认根因。T054/T069继续已有有界诊断：仅补交换各await进入的单调tick及Connect完成tick，在失败日志输出，以区分等待前代码与socket续体延迟；不变业务接口、socket重试或期限。独立诊断构建/新暂停root，通过现有自动接班加载，旧失败及程序不覆盖。

r7短预检工具页面退出成功但共享通信失败，禁止放行业务。新诊断确认Host写入后立即进入ReadHeader，PLC首部读到约1128ms后，处理0.015ms；仍未确定阻塞根因。仅对下一条指定Test短预检的本轮所属PLC/Host各采20秒EventPipe（16MB缓冲），工具局部安装dotnet-trace 10.0.745401，不需系统ETW权限，不采其他进程，不变全局环境或安全配置；trace产生额外负载，不作正常业务验收。记录启用路径、工具SHA、PID和退出/分析完整性，原日志及nettrace保留。

采样job002 Host于外部collector连接前已退出，只有PLC trace，明确不是完整Host采样。下一条指定短预检改为仅本轮Host的Start-Process Environment设置EventPipe启动输出（缓冲16MB，至该有限预检进程退出，外层240秒），不改父进程/机器环境、不启动额外采样进程；原失败和20秒采样保留。只分析新得到的实际记录，不据未捕获窗口推断根因。

实际退出包有9417条ASP.NET框架Info、3487条GET/OPTIONS开始/结束、478条RuntimeFlow，查询中间件排除未控制框架重复日志。T054/T069将框架Microsoft.AspNetCore类别限制Warning以上，Gaode自身命令/阶段/设备/保存/失败结构化日志保持；该日志缺陷与通信未定根因分开。与此同时VirtualPlc已有256环形诊断补业务响应，原只记录心跳无法对照最新business ReadBody延迟；每个响应仅内存有界记录、失败时原窗口输出，不增加逐请求控制台日志或改期限。

2026-09-27 T054/T069启动子范围继续：r13 Q06失败交易发生在VirtualWorkerHostedService与HTTP完成启动之前，已启动PLC心跳与这些冷启动步骤重叠。PlcConnectionHostedService仍负责真实连接及释放，改为等待IHostApplicationLifetime.ApplicationStarted后连接；HTTP监听只表示接口可达，既有PLC连接/安全/worker就绪准入不改，未连接不得动作。失败沿BackgroundService停止Host并保留原设备超期，不放宽期限或新增重试。日志记录等待、真实设备启动及连接完成；既有停止顺序及必要生命周期/正式代表复验。

## 2026-09-27 Test Host I/O运行设置（原FR-013/014，T054/T069/T070）

r16在全部构建/测试结束后，以同r15普通构建/原PLC/default server GC/原配方及全部期限，仅所属Host DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，Q06同rund4fc1789-b50b-487f-953c-9acbb14410eb完整Final及全部适用审计通过。单代表不证明全局根因，继续剩余主流程。

采证脚本增加显式HostSocketInlineCompletions Test开关：queue job JSON字段`hostSocketInlineCompletions: true`，wait worker只传递verify CLI `-HostSocketInlineCompletions`，verify传同名开关至start。start必须已有合法purpose=Test fixture，否则拒绝；只以Start-Process -Environment设置该次自身Host，PLC/父进程/系统环境不改变。默认不开启，record.configuration.hostSocketInlineCompletions记实际模式，实际DLL/GC/期限照旧登记。替换仅匹配临时request的诊断接线，不读取遗留request，不新建平台或扩大业务API/页面。

这是当前虚拟Test运行配置，不宣称生产默认或真实PLC已经验收；原默认模式的失败与根因未完全确诊事实保留。没有改算法、配方、设备协议、采集/保存或任何期限，没有新增重试、假完成或跳过。剩余代表沿明确记录的Test设置验证，若失败仍保留并暂停。

## 2026-09-27 虚拟PLC I/O有限比较（T054/T069/T070）

r17 GROUP-A-E/run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，实际XYZ已匹配。Host20:15:49.799033Z写出tx9478，PLC20:15:54.5024904Z才读头、处理0.0133ms；当时PLC累计GC37.593ms，不据此认定GC根因。正式失败保留，不计通过，后继暂停。

增加默认关闭的PlcSocketInlineCompletions开关及queue字段plcSocketInlineCompletions。沿wait→verify→start传递，必须合法Test fixture；仅所属VirtualPlc Start-Process -Environment设置DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，process.configuration.plcSocketInlineCompletions登记实际启用。Host开关原含义保持，父进程/系统环境不修改，不改变DLL、GC、协议、期限或成功条件。先对同成组路线作一次独立有限比较，真实页面、数据库、设备及算法全链仍必须完成。

采证器对非预期恢复场景应识别现有页面阶段的“阻断”状态并保存StoppedOrUnknown，避免只识别中文fault而等待全预算。恢复注入仍沿既有waitingRecovery条件操作，不把阻断视为Final。

## r18恢复采证时序缺陷（T069）

job002旧run f86b2a63-030b-4792-ad8b-2986f6ada2c8、新run8aaf32b5-c77f-4342-a4a5-d2089db03bec已真正完成双端复位/初始核验/显式新轮及Final，exit1/cleanup=true。23项读回中仅old_actual_media_visible_through_fault_and_reset=false：beforeFault三维图片已在API Ready，却尚未画到页面（页面Idle）；atFault/afterReset实际显示true，旧SQLite/文件/API摘要一致。原包保留Failed，不能以新Final抵整项。

采证修复仅在既有oldMediaProof内有限等待同旧run实际Ready三维媒体的img data-media-id且complete/naturalWidth>0，再保存截图及注入F故障；不改页面、算法、PLC或业务期限。不写DOM/不伪造displayed。后续新作业重新验证全条件，不覆写本包。

## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

## 2026-09-27 复位观察同步必要修复（原003 T072-A、008 T068/T069/T070）

必要顺序用例在Reset 202后立即Check实际返回RecoveryResetNotObserved，两次均未到链接保存注入，原r19 TRX保留。复位直接Modbus Ready已成立但缓存PlcReady仍旧false；原MotionCoordinator必要门禁不放宽。仅ResetAsync在原轮询/原期限内同时等待缓存实际PlcReady、Connected/SafetyClear，使用同一次observed快照；不改变接口、信号、初始判据或生产机械未知边界。评估见.specify/bugs/008-reset-ready-observation/assessment.md。源码当前尚未修改，待当前测试结束；现有两个保存门禁及正式旧图/完整新轮独立新包复验，已有任务承接不追加重复任务。

## 2026-09-27 T065机制修复范围（待本轮验证）

同DLL受控观察已证明Portable批队列派发延迟：Host业务/心跳txn3分别入队后1112.2005/1056.6997ms，入队仅0.006/0.0072ms，真实1秒超期且锁定；独立Native候选2937个非零操作唯一回调，1560个Host响应头最慢20.2574ms。证据入口：`.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/`，旧构建和报告保留。

最小接线为Windows、purpose=Test冻结fixture显式WindowsNativeThreadPool开关，仅本次所属Host/VirtualPlc子进程DOTNET_ThreadPool_UseWindowsThreadPool=1、inline=0；普通启动及旧冻结构建不被静默改写。三个最低线程预留位置依微软支持的实际运行配置区分Native/Portable，Native不调用不支持的SetMinThreads、不虚报预留8。正式构建不含Harmony、socket反射或诊断事件。业务API、信号、1秒I/O、3秒心跳、50ms轮询、GC、优先级、失败锁动作及未知结果不重发条件不变。

本增量沿003 T065和008 T055/T070原任务，追加任务0、勾选不变。只验证该机制路径、原期限真实超期锁动作及当前正式Q01同run前端/配方/PLC/相机算法/SQLite媒体/Final；复用未改分支历史证据。r22 HTTP独立保留，真实设备/标定仍待现场，不增加全运行时证明门槛。只有本轮验证完成后才更新验收状态。

## 2026-09-27T05:09Z 本轮验证完成状态

前述实施前待验证状态由本节接续：003 T065原Test/VirtualPlc机制/对照/安全/日志条件，以及008 T055当前正式Q01和T070适用Test对账均已满足，仅这三项授权勾选更新。新构建显式WindowsNativeThreadPool/inline0，旧默认与冻结程序不改；r22 HTTP、缺失历史日志及真实设备/标定不扩大结论。新证据目录为.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z，verification-proof.json与task-checkbox-changes.json可核对；本轮新增任务0、其他勾选不变。


## monitor-xyz-history 本轮同步
按[003当前监控纠正](../003-plc-latest-protocol/spec.md)及其[诊断合同](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md)：公开XY名称、原列表同值XYZ、删除独立栏目；复用audit/changes，保持业务/地址/期限。此前XYZ命名条款仅限旧构建。

## 009 / AL06 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

正式接线保持原Host与运动/保存所有者，按以下已对齐职责实施：

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

E06增量：三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

原RecipeExecutionBudget公式、机械/ACK上限及工艺顺序不变；同盘源槽不等于序号、组成员/整体共享动作、E规则、不重扫和3+1继续有效。业务断言以语义证据/真实提交检验，线缆值仅通信断言承接。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

009新增证据分量：同次语义ObservationId关联PlcCommunicationEvidence实际记录，查询只读；必要raw保存失败无有效引用不等于库无行。F05/F06必须分别实证提交前失败、commit后回执失效、暂不可核查；BA必须实证健康通信卡住/两容量/后台取消。原独立Host/VirtualPlc/Worker、SQLite媒体/API、实际前端渠道和来源义务不变；组件、同进程TCP与独立进程明确区分，历史通过不抵扣本轮。

当前处置公开状态Reserved/InTransit/Completed/UnknownHeld/NoMoveRequired含义不变。InTransit要求当前可靠取料及真实业务提交，Completed要求可靠放料及必要内部协议闭环后占用提交；API/业务测试不判断原始2/3或ACK0。实际提交与当前有效回执分离，迟到实存不能恢复旧动作资格。status/run/evidence及通知逐字段版本/null/历史映射按009影响矩阵§5，本合同业务结果station01-result-display/1.0不变；旧raw仅Infrastructure有限历史reader，原字节不改，不补造。

既有protocolSlotIndex JSON保持配方载荷，加载边界解释为业务PhysicalSlotIndex，真实槽与Sequence分别验证。位置、源目标、初始测量/当前面及E/人工/特殊动作原Test输入不变；Test字序/地址不批准生产。首次补齐schema1.1新预算及实例引用在基线前完成，随后冻结配方、业务输入及10000ms预算；M01—M05不得修改它们。纯协议模块仅共享定义/codec，独立VirtualPlc不共享上位机业务状态机/数据库；独立expected不从生产定义生成。

代码前置：009 T012实际跨功能对齐复核完成，随后严格按tasks各项依赖；不把本节当代码已经交付。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。

实施先实际完成本功能共享合同对齐，再经009 T012职责复核，才修改对应共享代码。当前只完成文档接口决定，新增生产/消费能力和真实验收未完成。


### 009 检测保存窗口接线补充（2026-10-01）

本功能产品检测执行按[003检测合同具体接线](../003-plc-latest-protocol/contracts/detection-port.md)传递本轮冻结 CriticalSaveBudgetMs 与原 Detection 截止；必要保存共受两者约束，不由固定两秒或协议步骤推导。算法意图与派发共享真实会话/时钟/起止时间。实现及验证归009 T035/T048/T049，先前任务勾选保持；本节不声明运行通过。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。

当前来源分类的有限补齐：ResultSource在末尾新增Test，保留既有Real/Virtual/Simulated/Fallback的值和历史含义；仅由明确声明Test的实际算法生产者产生，不从RunPurpose猜测。IntegratedDetection的Source与意图来源来自IAlgorithmPort.Origin，未知仍Fallback/Unknown；完整矩阵继续使用AlgorithmOrigin与实际事实。该变化用于消除把独立Test Worker写成Simulated的固定标签，归009 T034/T035/T039及T043—T046，旧记录不重写、现有页面仅绑定来源。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`仅作为旧payload中的原文保留，不生成同名新业务事实，不倒推历史原始值或来源。消费者不以旧名字/裸kind授予动作；当前面关联继续调用FaceEstablishment.Confirms，原证据与保存门禁不减。通信用例仍检验实际清零，业务断言迁移到当前语义事实和来源，两侧均必需；不新增页面、信号、恢复路径或产品兼容层。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。


### 009 T033/T039 后段退出后的故障保存版本交接（2026-10-02）

integration238的实际三阶段UnknownHeld案例已提交检测事实至Run revision43，公共准备RunExecution仍持旧revision，故障收尾保存被CAS拒绝。仅在已等待后段执行返回UnknownHeld/解锁失败、当前执行停止后，故障协调器通过既有ITraceQuery有界读取同run的已提交状态，核对runId/requestId/subjectId/context、无终态、版本不回退且不存在活动配方应用保存窗口，再把现有运行保存游标交接到真实已提交revision。随后原CriticalSave与CAS不变；不循环重试冲突，不重放物理动作，不把只读核查或迟到记录当绑定、取放或后继动作成功许可。

原始故障ErrorCode及UnknownHeld事实保留，FaultRequiresNewRun仍作为恢复规则/事件和旧continue拒绝码，不覆盖已记录的设备故障原因。查询/保存失败仍无成功保存声明，不调整预算或重启规则。实施归009 T033/T039，验证保留原ThreeStageMainFlowIntegrationTests未知保持与零WholeTray断言，并补游标交接拒绝条件；当前修复未运行验证，不改其他功能历史任务状态。

### 010实施定向对齐 A01/A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A01**：共同输入不含ProfilePayloads/PositionPayloads或fixture JSON；文件解码和替代语义提供者调用唯一RecipeDefinitionValidator，RecipeRunPlanner保组成/面/必检/身份规则。环境提供CoordinateDefinition，共同CoordinateResolver执行测量关联、偏置/单位/范围及对象/面/轮/槽校验，不由环境预算业务Z。TestEligibleSlots移批准边界。
  生产/消费与010实施承接：catalog/validator/planner→Start/绑定/预算/移交/检测/分拣→API/fixture/投影；T008/T009/T011/T012/T016—T020。
- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A02/A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。
- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A03/A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。
- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

前轮specify仅确认需求同步；本次014 Phase 1及012配套设计见当前设计引用，不生成新tasks；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
