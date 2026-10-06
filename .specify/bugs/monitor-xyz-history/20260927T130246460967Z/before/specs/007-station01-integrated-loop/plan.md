# 技术方案：第一工位完整虚拟集成闭环

**功能标识**：007-station01-integrated-loop  
**日期**：2026-09-24  
**规格**：[spec.md](spec.md) v1.1.0  
**宪章版本**：6.0.0  
**范围**：实际006桌面前端经Host启动，复用003正式工艺/PLC/配方/存储，接入固定图片采集和独立虚拟算法，至同一runId的FinalUnloadCompletion。既有局部实现/证据按任务状态保留；未声称完整前端闭环或本次诊断增量运行通过。  
**分支**：setup-plan报告 `007-station01-integrated-loop`；工作目录当前不是Git仓库，分支名仅为Spec Kit功能标识，不代表已创建Git分支。

## 方案摘要

唯一 Host 保持状态和动作控制权。006实际WPF/WebView2前端在原型已有启动入口向正式API提交带测试身份、版本配置及合法占位的请求。Host使用既有VirtualPlc的Modbus TCP及匹配反馈、正式采集端口读取固定目录图片、正式算法端口与独立虚拟worker通信、正式配方加载/校验/计划及SQLite短事务，自动经过003所定义的公共准备、Detection、UnloadPreparation、Sorting、整盘提交和解锁观察。启动时启用的受控联调客户端在ObservedUnlocked已提交后自动模拟取盘并经后端确认API提交，来源标记Test/Simulated。所有关键事实和媒体由同一runId串联，最终以持久化FinalUnloadCompletion为准。来源：007 FR-001–018/SC-001–008、003当前规范性规格与合同、006 FR-001–008，宪章P03–P09/P12/P13。

| P13阶段边界 | 当前方案 |
| --- | --- |
| 起点与终点 | 测试环境准备和模拟上料后，由006实际桌面原型已有入口发起唯一启动；同runId查到已提交FinalUnloadCompletion及可核对FinalSourceMatrix |
| 必须参与的组件与接口 | 006页面/宿主→授权Host API/通知；Host→VirtualPlc正式Modbus；Host→正式采集/媒体/算法端口→独立worker；版本化模拟配方/计划；实际SQLite；受控自动模拟确认客户端 |
| 必要验证 | 一条完整前端启动闭环；新增采集/worker真实性；F/安全映射、算法有限失败、未知PLC动作、关键保存、确认授权与顺序等直接影响安全/真实性/完成的必要失败路径 |
| 完成证据 | 进程与命令、前端操作/API、同runId调用与版本快照、图片摘要/媒体内容、worker请求响应/耗时、PLC代次和写入审计、SQLite事件/投影/矩阵、最终查询及模拟来源 |
| 延期项 | 其他工位、真机/算法精度/生产验收、生产鉴权平台、完整配方生命周期、长稳压力、非阻塞优化和完整异常矩阵；全部主流程跑通后按规格再安排 |

## 技术上下文（Technical Context）

| 事项 | 当前选用方案 | 决策来源与状态 | 尚缺证据/实施门禁 |
| --- | --- | --- | --- |
| 后端运行时 | 已有.NET10 Host、Application端口与Infrastructure适配，VirtualPlc为独立ASP.NET进程 | 仓库现状及003，已确认复用 | 新接线和完整E2E仍未执行 |
| 算法运行 | Host通过已有WorkerProcessSupervisor管理唯一独立worker子进程，复用NDJSON `station01-worker/1.0`传输边界并新增结果映射；Height/FDecode/Detection调用由正式端口派发 | 007 FR-005及[研究R1](research.md)，设计决定 | 本批已接线并完成辅助API样本的实际收发；006页面启动仍待T015验证，启动平台只核对worker进程事实 |
| 数据 | 现有EF Core/SQLite写入、独立查询、MediaStore与短事务 | 003与当前代码，已确认复用 | 实际007运行的DB/媒体证据尚无 |
| 前端与桌面宿主 | 006批准WPF/WebView2、本地静态资源及后端API/通知；006负责实际页面启动请求、受控Test凭据传递和已有位置的完整状态展示 | 006规格，已批准 | 当前按钮空请求和Bearer传递待006同步、实施；007负责Host侧限定Test来源的API/通知跨源配置、后端授权验证及实际连通性，双方结果是完整前端联调前置 |
| 设备与采集 | 已有VirtualPlc正式Modbus；相机经既有采集端口增固定目录文件读取，无独立相机服务；现有光源模拟端口来源照记 | 007规格/003合同 | 当前SimulatedCapture合成媒体、SimulatedDetectionPort直接返回结果，需最小接线 |
| 配方/版本 | 已有Review模拟配方目录，候选`R-S1-A-CAP` v`0.4.0-review`，仅P01合法占位，F码唯一匹配；预期2次检测采集各配1次算法请求；新版本化007测试预算/图片清单/随机配置 | 007 FR-007/017、[研究R3](research.md) | 实际计划步数、调用数、001预算余量及动作反馈须运行前核验 |
| 时间与容量 | 每采集3–5秒真实墙钟；每算法请求10秒模拟计算；各等待有限；003阶段按冻结RecipeExecutionBudget确定的不重置 | 007规格、003合同 | 现有001窗口0.7–1.5秒确定冲突，必须新版本Test预算；检测次数和余量需实测 |
| 软件验证 | Windows实际桌面启动一条软件闭环，组件实进程，真实SQLite及媒体；必要失败局部注入/有效旧证据复用 | 007 US1–US3/SC-001–008 | 不使用进程内模拟、脚本启动或截图单独证明完整链 |

当前无未决的用户业务选择。上表门禁是实现前必须完成的配置、合同及运行事实核查，均有明确责任和处理时机。

## 宪章检查（Constitution Check）

“设计前”反映读取007规格与现有实现的状态；“设计后”评价本方案是否给出符合路径，不把未实现事项判成运行通过。

| 原则 | 本功能检查点 | 设计前 | 设计后 | 证据/受限范围与可继续部分 |
| --- | --- | --- | --- | --- |
| P01 | 用户决定、003/006与来源冲突可追溯 | 符合 | 符合 | [spec.md](spec.md)来源表、[research.md](research.md)R1–R5；003历史阶段排除及旧F规则不覆盖007 |
| P02 | 后端业务边界、已批准前端/宿主、只建必要适配 | 符合 | 符合 | 本方案唯一Host及现有端口；无相机服务、通用测试平台或预留业务模块 |
| P03 | 003工艺与版本化模拟配方 | 待补充，仅限制完整样本 | 待补充，仅限制运行验收 | 候选CAP/P01及唯一F码已定；实际合法计划/动作数需用冻结快照核验 |
| P04 | 有限算法终态、Z/F/运动安全、模拟参数隔离 | 符合 | 符合 | 003规则与[contracts/virtual-integration.md](contracts/virtual-integration.md)，阶段按冻结RecipeExecutionBudget确定的和安全门禁保留 |
| P05 | 单Host、正式端口、前端与worker无直控 | 待补充，仅限制接线 | 符合 | [结构与职责](#结构与职责project-structure)给出最小适配；实现仍待完成 |
| P06 | 有界资源、媒体所有权、心跳/停止独立 | 待补充，仅限制新worker/采集 | 符合 | 复用媒体租约及现有队列，记录时间预算，长等待不得持有事务/阻塞PLC心跳 |
| P07 | 同runId身份、质量/物理/完成分离、未知动作不盲重发 | 待补充，仅限制新证据 | 符合 | [data-model.md](data-model.md)和003 stage-events/来源矩阵，源记录不冒充真人 |
| P08 | 实际保存、短事务、快照不漂移 | 符合 | 符合 | 003持久化合同与[数据模型](data-model.md)，读取文件不等于媒体已保存 |
| P09 | 同一逻辑、虚拟来源和启动失败可定位 | 符合（历史设计范围） | 待补充（FR-019） | 必要日志持久收集与SC-009尚待T026/T027；旧SoftwareLoopOnly证据不回填通过 |
| P10 | OPEN局部限制，Test参数不冒充生产 | 符合 | 符合 | 新版本预算/清单purpose=Test；OPEN-22不阻断虚拟链，但限制真机 |
| P11 | 配置覆盖已确认能力、快照/准入 | 待补充，仅限制新Test配置 | 符合 | 沿用已注册能力与同一配方加载校验；不建策略扩展框架 |
| P12 | 只读原型、006独立责任、后端API | 待补充，仅限制实际前端启动 | 待补充，仅限制实际前端验收 | 006补页面请求、受控Test凭据及已有位置展示；007补Host侧限定Test来源的API/通知跨源与授权验证；双方实际连通后才可验收，006进度不阻塞独立的007后端/虚拟组件工作 |
| P13 | 有界范围、真实主流程、完成证据 | 符合 | 符合 | 本计划边界表、[quickstart.md](quickstart.md)；完整验收仍待实现运行 |

**门禁判断**：没有需要以设计方案违反宪章的事项。设计后仍受限的P03/P12是具体实现/运行依赖，必须在007完整验收前关闭；003/006共享接口、预算和来源合同在代码变更前先同步。不能通过缩小完成口径处理。

## 结构与职责（Project Structure）

| 模块/端口 | 状态或资源所有者 | 依赖方向 | 外部对接边界 |
| --- | --- | --- | --- |
| 006 Web页面＋WPF/WebView2宿主 | 006拥有页面与宿主生命周期 | 页面→Host公开API/通知；宿主传递受控Test凭据与配置 | 原型三页只读；实际启动请求和已有位置的完整状态展示由006处理 |
| Host API与工作流 | 唯一Host及003编排器；007负责本次Host接线 | API→Application→端口 | 限定Test来源配置API/通知跨源访问并验证后端授权与实际连通；启动202仅受理；same run自动推进；确认受控授权 |
| VirtualPlc与PLC适配 | VirtualPlc独立进程拥有模拟点位/反馈，Host拥有业务动作意图 | Host正式Modbus TCP→PLC | 不新建上料REST，不写夹紧命令冒充PLC内部夹紧 |
| 固定图片采集与光源 | 正式采集端口持有触发/读取，MediaStore接管媒体；光源沿既有模拟边界 | Application→采集/光源→MediaStore | 单一固定目录和冻结清单；无相机服务 |
| 虚拟算法worker | Host通过WorkerProcessSupervisor拥有唯一worker子进程、调用与期限；worker拥有模拟计算及结果 | 正式算法端口→受控worker协议→结果关联 | 启动平台启动Host并核对worker PID，不重复启动；worker不控制PLC、不写业务库 |
| 配方/计划 | 既有catalog/loader/planner与Host冻结快照 | F结果→合法匹配→加载/校验/计划/绑定 | 不另建模拟流程，不实施完整管理生命周期 |
| SQLite、媒体及查询 | TraceWriter/MediaStore为唯一写入通道；Query读取 | Application→Infrastructure→存储，前端通过API读 | 意图/反馈短事务；独立证据查询不写库 |
| 联调PowerShell | 只拥有VirtualPlc/Host/桌面进程编排、worker子进程核对、测试输入与受控API调用 | CLI→Host API；读取日志/查询 | 可辅助启动，自动模拟取盘但无PLC或DB旁路；单独Python worker命令仅供协议调试 |

预计新增文件仅限007所需的图片清单/Test预算、文件采集适配、独立worker与接线、PowerShell启动/模拟上料入口及针对新增风险的验证代码。具体实现文件在后续tasks确定，现阶段不为未来能力预建目录。007不安排006页面/宿主实现任务，也不修改原型。

## 数据、契约与状态

详情见 [data-model.md](data-model.md)、[虚拟组件接线合同](contracts/virtual-integration.md)、[联调命令合同](contracts/commissioning-cli.md)。复用003 `station01-main-flow-api.md`、`detection-port.md`、`whole-tray-workflow.md`、`component-source-matrix.md` 等正式合同；007合同只补本次集成输入、来源和证据，不复制PLC地址及完整业务API。

每个关键调用关联同一runId/trayId/planRevision和独立operationId/captureId/callId/attempt。固定图片清单摘要、配方版本、预算、随机配置及worker版本冻结到运行证据；算法返回中的身份和输入摘要必须与原调用匹配。命令受理、媒体已保存、算法已返回、PLC动作匹配反馈、WholeTrayCompletion、ObservedUnlocked、模拟取盘确认及FinalUnloadCompletion分别有状态与事件，不能合并为“成功”。

自动模拟取盘使用运行启动时明确启用的Test/Commissioning渠道和后端受控身份。客户端只读后端快照确认同run的WholeTrayCompletion与ObservedUnlocked已提交，携同一稳定requestId/expectedRevision调用确认；服务端重检条件并原子保存。003共享文档已统一确认路由、请求字段和Test/Simulated ManualActor来源；T013已在VirtualPlcIntegration记录Test来源并经辅助API样本验证，006实际页面启动的完整联调仍待T015。

## 配方共用逻辑与动作隔离（适用时）

使用已有 `recipe-catalog-review.json` 的simulationOnly配方和正式加载/校验/计划/绑定流程。正常测试候选为S1 `R-S1-A-CAP` v`0.4.0-review`、F精确码 `RC:R-S1-A-CAP:0.4.0-review`、一个占位P01；F worker必须通过实际请求返回合法码，不能从脚本预写F成功。计划预期两项Detection采集，最终以正式生成计划为准；同一运行不得后来删减必检步骤。Test配置和图片清单使用版本标识/摘要，并在启动及正式绑定时冻结。真实设备动作入口拒绝模拟参数；本阶段PLC本身为Virtual。

本阶段只使用现有配方查询/匹配/计划能力，不增加发布、回滚或管理界面。随机结果在对应用途及配方目标域内生成；无法合法映射时走003失败路径，不伪造成功。

### 配置与策略扩展设计（P11）

固定目录、文件用途、延迟、算法种子和版本化测试预算是现有能力内Test配置；保留现有相机/算法/计划端口和Host组合根。若现有worker协议缺少结果/媒体租约所需字段，仅最小扩展当前合同。无需新增产品策略注册、脚本执行能力或跨工位扩展层。未注册/不兼容能力按现有加载错误处理；算法worker暂不可用按P04有限收敛，不变成全局启动门槛。

## 并发、资源与异常出口

| 路径 | 所有者/容量来源 | 等待期限来源 | 失败终态及后续步骤 | 资源释放/保留 |
| --- | --- | --- | --- | --- |
| 公共3D/F采集 | 既有采集端口/MediaStore；007固定清单 | 新版本Test业务预算，各成功调用实际3–5秒 | 缺图/读失败/超时不造媒体；3D安全依赖禁猜值，F失败不绑定配方 | 文件输入只读，媒体交接后由MediaStore负责 |
| 公共Height/FDecode算法 | 既有每角色有界队列、媒体租约；独立worker | 新版本Test预算需覆盖10秒＋IPC/排队/意图保存 | 有限失败；F无唯一合法码锁停，不用旧码/旧配方 | worker使用期持有输入租约，完成/超时按原规则释放 |
| Detection | 003 IDetectionPort，逐冻结计划步骤采集并派发真实worker；单Host | 阶段开始固定120秒，算法3次2/5秒、通信4次1/2/4秒 | 失败有证据的Pending；映射歧义MappingFailed；不得返回批量硬编码OK | 阶段期限到仍须完成必要状态/保存，不能重置期限 |
| Sorting/UnloadPreparation/Unlock | 003正式PLC action port和资源租约 | 各阶段按冻结RecipeExecutionBudget确定的及适用PLC反馈窗口 | 未知动作UnknownHeld；安全条件失效限制相关动作，不重发 | 物理占用保留至可信处置 |
| SQLite/媒体保存 | TraceWriter和MediaStore唯一写入通道 | 既有criticalSave预算、媒体配额及测试根 | Failed/CommitUnknown阻止依赖动作及完成声明 | 短事务；不跨3–5秒采集或10秒计算持锁 |
| 自动模拟取盘 | 受控客户端和后端授权/阶段核验 | 启动时启用，等待整个运行受控结束；不自创阶段成功超时 | 无解锁、无权限、版本冲突或存储失败时明确受限，不报Final完成 | 用稳定requestId幂等确认，不直接控制PLC |

**预算核算与决定**：旧Test窗口 capture3d 1.5秒、height 1秒、captureF 1秒、fDecode 0.7秒，必定与007正常采集/算法冲突。新版本须满足每类窗口 `>5秒+适用采集/媒体开销`、`>10秒+意图/IPC/排队开销`，保持有限。Detection必须先从实际计划与接线求C/A，再证明 `5C+10A+T设备/存储/调度+R重试 <120秒`；正常无重试样本预期CAP/P01有C=2、A=2，仅模拟延迟上界30秒。全15槽CAP的C=30，仅顺序采集的3–5秒上界已达150秒，无法保证120秒正常通过；默认BASE/15槽更不能直接复用。若实际计划或反馈开销使候选超期，先按共享文档规则调整合法Test预算/阶段约定或样本，绝不省略必检步骤。各阶段120秒各自开始，不是全run总期限。详情与来源见 [research.md](research.md)R3。

## 保存与恢复

复用001/003意图先提交、正式调用、反馈再提交的短事务。采集意图及触发、输入文件摘要与新媒体文件、媒体元数据提交、算法意图/派发/响应、计划/移交、Detection/Sorting/UnloadPreparation、WholeTrayCompletion、解锁反馈和最终确认各保留写入ID及revision。图片清单不是采集记录；worker日志不是算法结果已保存；SQLite提交未知时不能靠页面状态补成功。

媒体租约从正式存储接管至算法消费完成/有限取消；迟到结果只能附加原调用证据，不得改写其他对象或触发重复动作。PLC写后断联或代次变化使动作未知时进入UnknownHeld，保持占用，不盲重发。重启从已提交边界核对，已完成物理动作不重放；本阶段只验证与实际接线改动相关的恢复风险，旧003未受影响的证据可复用。数据库维护/备份平台不是007新增范围。

## 软件验证与证据计划

每一运行保留 `artifacts/station01-007/<evidence-id>/` 下的manifest、组件日志、API/前端记录、PLC审计、媒体与算法索引、只读SQLite查询、阶段事件/矩阵和final-result。建议具体文件见[quickstart.md](quickstart.md)。证据目录是将来联调输出，不由本次plan生成伪运行结果。

| 需求/原则 | 正常/失败场景 | 方法与测试输入来源 | 预期可观察结果 | 证据产物 |
| --- | --- | --- | --- | --- |
| FR-001–010/SC-001–005；P03–P09/P12 | 实际前端启动→最终完成 | 006 Windows宿主，CAP/P01冻结Test样本，VirtualPlc、文件采集、独立worker、SQLite，启动时启用模拟取盘客户端 | 同runId的完整阶段、实际调用和保存，ObservedUnlocked后自动模拟确认并到FinalUnloadCompletion；前端已有位置与GET一致 | 进程/前端启动记录、请求体、计划、媒体/worker/PLC/SQLite索引及源矩阵 |
| FR-003–006/SC-002/003；P07 | 图片缺失、worker未接收/错关联 | 受控缺图或worker故障单独注入；验证实际新适配 | 无假媒体/OK/算法成功；必要失败事实保存并有限结束 | 输入清单、文件摘要、调用与错误/期限记录 |
| FR-007/009/017；P03/P04 | 3D安全失败、F无唯一合法码/配方不符 | 必要合同/集成测试，复用未变PLC安全证据 | 不猜Z、不沿用旧F，不生成绑定或依赖动作 | 配置/算法/计划/动作审计 |
| FR-005/009；P04/P07 | Detection worker有限失败和映射有效/歧义 | 只验证新增独立worker风险；003未变分拣映射证据可复用 | 有证据Pending及合法分拣，或MappingFailed受限；不默认为OK | attempts/deadline、对象映射与PLC动作/保存 |
| FR-002/009；P04/P07 | PLC断联/代次或未知动作 | 优先复用003同代码同合同证据；若接线触及相关逻辑则局部重验 | UnknownHeld、无盲目重发/提前解锁 | 模拟PLC写入审计、epoch与阶段事实 |
| FR-008/010；P08 | 必要媒体/数据库提交失败 | 新文件媒体路径、最终提交等直接受影响点的必要失败验证 | 不报完成、不越过写入门禁；CommitUnknown如实保留 | 媒体/写入结果与API受限状态 |
| FR-011–016；P12/P13 | 前端请求/权限/状态、自动模拟确认提前/重复 | 006既有入口实际操作及受控客户端，额外测试未解锁/错版本/无权限 | 前端实际API受理与GET一致；提前确认被拒，重复不产生双份最终事实，Test来源不冒充真人 | 请求/响应、画面映射、身份/来源矩阵及最终查询 |

对已有003/006证据的复用须列证据位置、代码/合同/配置版本、覆盖断言及未受本次改动影响的理由。新增文件采集、worker收发、实际前端启动、自动模拟确认与修订预算没有旧证据可直接替代。最终结果仅标记Passed/Failed/Blocked/NotRun；缺必需组件或实际前端样本为Blocked/NotRun，不宣称项目或现场验收。

## OPEN、外部依赖与决策记录

| 依赖 | 最小处理及实施前同步文档 | 对007的影响 |
| --- | --- | --- |
| 003算法/采集/阶段合同 | 先核对并最小同步003 `spec.md`、`contracts/detection-port.md`、`public-preparation-handoff.md`、必要的worker/媒体约定、`plan.md`、`tasks.md`；固定图片及计划逐项真实调用，每次采集对应一次算法请求，并验算实际C/A和预算 | FR-003–006/017、SC-002/003；既有工艺、按冻结RecipeExecutionBudget确定的及有限重试原则保留 |
| 003最终确认及来源 | 同步003 `contracts/station01-main-flow-api.md`、`whole-tray-workflow.md`、`component-source-matrix.md`、必要的`status-notifications.md`及受影响spec/plan/tasks；统一长/短路由与字段，Test模拟ManualActor不得写AuthenticatedHuman | FR-009/010/012/013、最终矩阵；保留授权/解锁/原子保存 |
| 001版本化测试预算的共享定义 | 新增007 Test预算/模拟配置，若沿用001 schema字段只引用；如需变更字段或期限合同，先同步001受影响spec/contracts/plan/tasks及003相关文档 | 旧0.7–1.5秒窗口确定冲突；新有限窗口与120秒余量是正常链前置 |
| 006实际前端入口与状态 | 先同步006 `spec.md`、`contracts/api.md`、`prototype-mapping.md`、`gaps.md`、`plan.md`、`tasks.md`；006负责实际按钮的完整StartRunContext/版本引用、受控Test凭据传递和已有位置的完整阶段/最终状态展示；007负责Host侧限定Test来源的API/通知跨源配置、后端授权验证及实际连通性，涉及共享接口先同步相应合同 | FR-001/011、SC-001/005；双方实际连通是完整前端联调前置，006未完成不阻塞独立的007后端/虚拟组件实现，脚本样本不能替代前端样本 |
| 006原型版本记录 | 只读ZIP实测SHA-256为`3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`；006 quickstart及部分合同的短哈希需在006内统一 | 原型核对证据；不改ZIP/页面 |
| 既有运行配置/端口 | 核对worker、Host、桌面地址及VirtualPlc端口；不能用旧发布脚本两进程启动声称全组合就绪 | 启动可复现性；独立开发可先做接口与Test配置 |

已有合同的原型条款只读；上述“同步”是实现前待办理，不是本次改动授权。OPEN-22等真实设备/算法来源未提供只限制真机及生产能力，不阻止标明来源的虚拟软件联调。新增任务须逐项对照007 FR/SC；不把003/006全部历史未完项、完整配方生命周期或异常矩阵自动并入007。

## 客户确认原型检查（P12）

006批准的客户只读归档为 `E:\dzk\gaode\原型.zip`，只读复核SHA-256 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`，页面 `login.html`、`a.html`、`data-view.html`。本计划只要求006已有位置绑定后端实际阶段、错误、权限和通知；无取盘确认控件时受控联调客户端自动模拟确认，记录其非前端渠道。不增加控件或宿主业务直控权。006逐页映射、启动请求和凭据传递须在006规格与合同内最小同步；007 Host侧跨源与授权连通须在相关共享合同同步后实施；当前未运行页面实测，不称其已对齐。

## 设计后复核

Phase 0研究已形成[research.md](research.md)，关键时间冲突、现有适配器和006/003合同差异已明确。Phase 1产物为[data-model.md](data-model.md)、[contracts/virtual-integration.md](contracts/virtual-integration.md)、[contracts/commissioning-cli.md](contracts/commissioning-cli.md)及[quickstart.md](quickstart.md)。本计划满足宪章的设计边界；实际通过须在上述共享文档同步、接线实现和[验证计划](#软件验证与证据计划)执行后另行判定。本次只修正007允许范围内的文档，不修改003/006或代码。

## 2026-09-23 媒体查询有限接线

新增一条Host公开同run媒体清单，不扩展图片管理平台。查询从SQLite已提交Media/Writes/StageEvents组合；Detection执行时仅给现有采集事件追加冻结计划业务相机身份，媒体字节仍由原授权路由提供。验证当前run隔离、公共准备与Detection区分、未映射/未就绪、权限及字节摘要；旧完整闭环证据不改写，不运行007整阶段验收。006按独立合同映射原型七格。

## 2026-09-23 VirtualPlc 清零时序回流依赖

最新版PLC协议§3.1.7及003 FR04规定：检测中清旧`4x0001`时`4x0002=1`保持本轮到位事实；收到`4x0053=2`后才清`4x0052`；下一合法运动实际受理时PLC开始新一轮反馈。specs/003-plc-latest-protocol T061 已修正 Host 正式适配器的下一运动门禁及旧协议测试断言，并以正式 Modbus 3/3 定向测试验证（`artifacts/plc-latest/t061-20260923-223458/manifest.json`）；保留坐标、代次、安全与未知动作门禁，不以旧1充作新运动完成。该证据满足 T015 的此项前置，但不代表 007 完整闭环通过。

VirtualPlc本地监控按实际写入序号显示变化、最近变化排除心跳属于003 FR14/T062诊断范围；有限缓存有缺口提示，监控源码已改为显示实际数值0，接口和页面逻辑已有定向证据，但实际页面操作尚未验证，T062 保持未完成。它不新增007业务点位，也不构成T015门禁。局部隔离验证及用户手工反馈只覆盖各自观察到的行为；未重新执行第一工位完整联调，历史007证据不得改写为这次通过。

## 2026-09-24 FR-019日志收集与SC-009失败复核

2026-09-24下料增量设计：先消费003 FR16–FR18及更新的阶段动作/VirtualPlc合同，再在独立Test配置中冻结下料目标、复位位置与容差；007正常闭环证据须追加目标写入顺序、Z复位、命令4后的XY/XYZ可靠反馈、下料阶段提交及其后解锁读回。旧到位1、同目标歧义、代次变化等必要失败路径不得被已有诊断T026/T027或旧T015证据覆盖；页面原入口复核留独立bug-test。

沿既有Host、VirtualPlc、worker和006程序的本地日志/启动说明收集，不新建日志服务。每次受控Test运行用`requestId`建立证据清单；收到`commandId/runId`后补入同一链，设备动作追加`operationId/connectionEpoch`，保留进程/PID、时间基准、组件/协议/冻结配置版本、日志路径与哈希。组件日志必须在进程退出后仍可从证据目录读取，并按关联标识和时间定位；重复心跳/轮询有界聚合，不吞首次故障、阶段转变或终态。公开查询与SQLite/PLC事实另存索引，日志不能替代持久完成证据，令牌和内部秘密须脱敏。

T026负责最小收集与关联索引；T027用一次006实际入口发起、已受理且已建运行的首次PLC通信失败验证：从保存日志定位就绪/首次交互停止点、原始异常或明确未知原因、最后可靠观察、未发送/已发送但反馈未知边界及处置，再对照可靠反馈明确不安全，核对006已有位置的显示与可执行操作。若006尚不可用，可先做Host/VirtualPlc定向证据，但SC-009整体为Blocked/NotRun，不借辅助脚本样本冒充实际前端。虚拟链只支持SoftwareLoopOnly结论；真实设备安全与协议兼容性未验证。旧T015/T019–T024及003 T061证据不自动证明此项通过。

本次实施增加独立端口参数、禁止复用已有PLC实例和本地证据索引脚本；隔离样本须保留独立SQLite、进程PID、配置哈希与退出后的日志哈希。实际WPF/WebView2在会话0不能呈现；会话2尝试及Test专用本地取证桥均须以真正的页面截图和请求事实判定，不能仅凭进程存在勾选T027。首次3秒心跳异常发生在建运行前，仍为待定位的集成缺口；旧正常闭环证据不等于本次回归。

2026-09-24后续实施：会话2的实际WPF/WebView2原型已有按钮已由仅Test/回环的调试通道点击取证；页面POST/GET、截图、Host/VirtualPlc日志、SQLite、协议审计及进程退出后的哈希索引分包保存。点击前已有Host心跳故障的样本明确作无效A样本，后续先以页面正式GET确认可靠就绪，再取得202并在其后注入通信故障；停止阶段、发出/未发出动作和可见处置由只读保存证据复核。实际结果与边界见 `diagnostic-validation.md`，不将此诊断增量扩大为T015完整正常闭环。

`station01-heartbeat-response-delay` 限定增量：FR-021复用既有独立Test根、回环端口、正式Host/VirtualPlc及WPF/WebView2启动入口，先以Modbus transaction、UTC/单调时钟与两进程日志定位长空档，再对实际证据指向的Host调度路径作最小修复。诊断期新实例启动已捕获PLC快速处理而Host读首部延迟约3.22秒，不能倒填为原Running3D现场的分段事实。保留3秒互锁、既有步骤和无盲重发；修复前后定向对照、真实中断安全对照及有限连续页面复测分别存档，未取得页面事实时不宣布SC-001或原故障已验收。

## 2026-09-24 最新需求与008完整执行对齐

本轮依据宪章5.0.0，仅更新设计；复用现有启动/采证脚本、独立VirtualPlc/worker与文件采集端口，不重建测试平台。

给start-station01-virtual-loop.ps1和simulate-station01-load.ps1 -PrepareOnly增加目标参数FixtureManifest：读取008独立Test根、版本化配方目录、公共/预算/模拟配置及素材/worker输入清单；解除PrepareOnly的S1/P01固定限制，保留现有设备就绪和来源约束。该参数本轮未实现，不能按计划命令声称已可运行。页面负责选用和启动，不由脚本自动完成整条业务。

S0/S1先处理当前入口心跳阻塞、接新F 3/4及扫码Z反馈，配套Q01合法点位/素材和完整下料/取盘；S2/S3复用夹具扩CD、多槽/多面，worker/适配器同步station01-worker/2.0单/双输入及租约；S4/S5仅补业务差异/必要失败的配置和证据。每次worker实际读媒体，模拟输出可由受控输入/种子决定，Host不写死成功。

保持采集3—5秒及worker每次10秒，按008实际调用数和运动/保存/人工等待计算冻结预算，心跳3秒不随之放宽。场景/配方新增无活动运行时重载，不建设热加载平台。各阶段先做与本次改动有关的必要检查，再收集正式页面到Final证据，避免重复全量测试。

[008方案](../008-recipe-driven-inspection/plan.md)与[quickstart](../008-recipe-driven-inspection/quickstart.md)给出阶段、目标参数与操作；[当前Blocked记录](evidence/t015-t044-20260924-current-validation.md)保持。设计检查沿008 P01—P13表；当前tasks已对齐，旧analysis保留历史；不勾选原未完成项。

当前任务归属与顺序：参见[本功能tasks](tasks.md)文末S0—S5增量及[008任务](../008-recipe-driven-inspection/tasks.md)首批集合。共享实现只登记一个所有者；历史版本/完成证据保留原范围，最新Q/C/F规则不倒填旧任务。
# 008 第五批Test夹具增量（2026-09-25）

specs/007-station01-integrated-loop T031/T033的Q01/Q02夹具须核对更新后的目录摘要、Test映射和受控OK worker配置；独立根启动实际Host/VirtualPlc/worker/SQLite，WPF控件操作采证，先Q01后Q02。映射语义见[008合同](../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)；辅助API或组件输入不抵扣正式页面。
# 2026-09-26直接共享实施

沿用独立worker入口，008 GROUP Test清单按实际输入对象/面选择模拟质量结果，执行与证据遵守[合同](contracts/virtual-integration.md)，不新增Host结果捷径。

## USR-E最小共享设计（2026-09-26）

当前直接增量依据宪章7.0.0及[动作/采证设计](../008-recipe-driven-inspection/plan-six-issues-alignment-20260926.md)。复用已有公共3D/F、设备适配、独立VirtualPlc与保存，不凭源码关闭用户问题；两端XYZ/关键握手诊断与实际运行包摘要先核对。当前四面仅3＋1，代表性验证与历史Q事实分列，不恢复全Q实跑义务。源码/配置/tasks本轮未修改，USR-D链路不变。
