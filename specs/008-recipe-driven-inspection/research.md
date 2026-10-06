# 当前研究结论：20260925分区协议（2026-09-26）

Decision：沿用现有线性计划/唯一运动准入/独立worker/SQLite/媒体/桌面宿主；移除本路线强制重扫，将执行阶段与测量/配置/连接身份分离。
Rationale：当前AB/CD单面、F3/4和双输入融合已接入，Q01/Q02/PARAM有旧协议页面通过；缺口集中在正式Flip固定受限、目录强制重扫、目标轮次耦合、旧分拣与下料顺序。实际调用关系见[实施清单](implementation-checklist-20260926.md)。
Alternatives：不新建通用框架、不重建worker/数据库/宿主，不以组件PostFlip上下文旁路正式Flip，不增加协议未定义专用字段。原型、协议原件和历史证据只读。

目标schema为review-recipe-catalog/0.5，执行合同3.0；真实初始测量加逐面明确Test映射或批准固定值是目标依据，不能默认继承面1XYZ。生产标定、E、特殊旋转与组/混合处置分别局部待输入，不阻断已有合法普通OK多面。

---
以下保留2026-09-24研究过程和当时授权/实现判断，均为历史；其中旧当前字样不再表示本轮状态，不授权修改原型或协议原件。

# 研究与当前实现核对

日期：2026-09-24；依据宪章5.0.0及008三项澄清。静态代码、配置、原型和历史证据查阅；未构建、运行服务、测试或迁移数据库。
功能解析为008-recipe-driven-inspection；setup-plan保留已有plan；前置hooks为空。目录无Git仓库，本轮不创建分支或初始化。

## 2026-09-24历史能力核对（不作为当前实现事实）

下表路径相对仓库根。“已有”表示代码事实；历史验证只保留记录中的版本和范围。

| 能力 | 当前实现位置 | 已验证范围/状态 | 当前缺口 | 最小复用/修改 |
| --- | --- | --- | --- | --- |
| 公共准备/唯一F绑定 | backend/src/Gaode.Application/Station01/StartPublicPreparation.cs、RunExecution.cs；Domain/Station01/FCodePolicy.cs | 已有调用与保存；007旧样本有公共准备事实 | 代码仍旧1/2；本轮已定义F 3/4合同但未接入；尚无前端期望版本比较 | 复用公共协调器，接入新F合同，F完成后比较所选引用 |
| 配方目录 | Infrastructure/Recipes/JsonRecipeCatalog.cs、RecipeCatalogFactory.cs | 有Review/File装载、唯一F索引、用途校验 | IRecipeCatalog无列表查询；File拒绝simulation目录；普通S3/特殊单件被当前限制排除 | Test使用独立Review目录，扩只读摘要和已明确场景/路线校验 |
| 配方/计划冻结 | Application/Recipes/RecipeRunPlanner.cs、RecipeContracts.cs | 已展开批采、翻面、重扫、E及特殊路线步骤 | 展开未证明执行；同实体共享换面/成员完成需修正 | 保留数据化顺序，补身份/实际目标及动作归属 |
| 实际检测 | Infrastructure/Simulation/IntegratedDetectionPort.cs:30；Host/Composition/AdapterBindings.cs | 正式固定图/独立worker/媒体/SQLite有局部证据 | 只过滤Capture；其余计划动作未消费，单图结果代替同面融合 | 应用执行入口接管业务步序，复用适配器执行采集/进程/保存 |
| PLC定位/复位 | Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs；Application/Motion | 公共3D/F运动与正式Modbus已有 | 产品相机/适用轴、F/E 3/4及取放/旋转合同未全齐 | 扩已确认动作，使用原唯一准入和会话，不增旁路客户端 |
| 高度/对象身份 | Application/Station01/PublicPreparationHandoffV2.cs | handoff有提交和摘要核对 | 从SortUnit构造期望对象且位置为0,0，部位与整体不一致 | 从冻结对象/目标映射构造期望集合，真实点位和高度轮次可追溯 |
| 单图/融合算法 | Infrastructure/Algorithms/*；scripts/virtual-station01-algorithm.py | 独立进程实际读单媒体、摘要、等待10秒及返回；F为Test配置返回 | 协议inputKey单输入；无双输入融合/E角色；随机输出无法直接作为确定覆盖方案 | 扩单/双输入协议、共享租约及参数，Test输出仍由进程读取输入后产生 |
| 分层结果/处置 | Application/Workflow/RecipeSortingMapper.cs、ThreeStageWorkflowExecutor.cs | 非OK映射/外围阶段已有 | 缺面/组/整体聚合、真实目标；Sequence被用作协议索引 | 分开结果与搬运单位，真实源目标预留及反馈后提交 |
| 下料/最终完成 | Application/Workflow/WholeTrayWorkflowOrchestrator.cs；LatestProtocolStageActionAdapter.cs | 003命令4安全Z/XYZ和解锁有局部证据 | 在途门禁与新对象层级接入；本轮前端完整证据不足 | 首条路线即复用尾段并补必要门禁，不等最后阶段 |
| 虚拟设备 | VirtualPlc/VirtualPlcEngine.cs | 已有Move/Flip/Sort/Lock/Zone/ZReset/Manual原语 | 原语不等于业务链；无已确认旋转/源目标完整合同 | 只实现已确认信号的设备状态转换，不生成业务结果 |
| 前端真实入口 | frontend/src/runtime.js:54、:219；frontend/scripts/build.mjs；desktop/HostRuntime.cs | 当前页面可实际POST/查询；运行入口S1/P01限制仍在 | 目录API仅摘要，未真实选配方；状态/错误/通知及人工交互映射缺口 | 006绑定已有控件与正式API；必须修改实际build使用的runtime.js |
| 测试环境/脚本 | scripts/start-station01-virtual-loop.ps1、simulate-station01-load.ps1 | 独立进程、Test库、日志和旧CAP样本入口可复用 | 固定007路径/配置/worker清单；PrepareOnly拒绝非S1/P01；自动取盘来自外部客户端 | 最小参数化目录/用例清单/版本及Test根；页面启动；禁止辅助API抵扣 |
| 心跳和当前页面验证 | .specify/bugs/station01-heartbeat-response-delay/test.md；007 evidence/t015-t044-20260924-current-validation.md | 心跳修复历史为partial；最新页面run d39ee415-23e3-46ea-aa60-8d2b5d0f8b3c在启动前失联而Blocked | 当前正式页面闭环、通知帧、401/403/阶段显示未完成 | S0/S1最小诊断/修复及定向验证；不降低安全门限、不重复扩大全故障矩阵 |

测试目录F编码可使用现有TestTrayCodePolicy接受的TEST-TRAY-四位数字格式，并在目录中唯一登记；0001有旧特殊映射，新的测试集避开该旧样本码。不由前端提供识别结果。

## 决策与替代方案

| ID | 决定 | 理由 | 对比后本轮未采用 |
| --- | --- | --- | --- |
| R01 | 复用正式Host和现有外围阶段 | 基础通信/保存/完成链已存在，改动聚焦缺口 | 新建独立模拟业务链、重写系统 |
| R02 | 扩现有线性计划及应用执行入口，按需提取聚合函数/小类 | 当前问题是步骤未消费与身份错误；22序列是数据组合 | 22份分支/型号if；提前建设通用脚本/插件引擎。以后是否引入引擎仍由实际成本决定 |
| R03 | 首条单面就包含页面及下料Final | 尽早暴露共享入口、持久化和末端问题 | 所有后端完成后再接页面，或把片段称为完整通过 |
| R04 | 先顺序执行单图分析，齐备后独立同面融合 | 保留需求顺序且减少线程/资源协调工作；前批不等后批 | 用单图判断冒充融合，预建高并发调度 |
| R05 | 已有Review目录装载Test配方，按用例配置而非写死运行成功 | 保留用途隔离，无需为测试建设发布系统 | 将simulation目录改成生产File用途、修改原68条目录证明覆盖 |
| R06 | 目录提供只读条目，启动携期望配方引用，F后核对实际绑定 | 实现前端配方/实际执行/保存一致；不绕过公共准备 | 前端直接bind、写F结果、F前执行产品运动 |
| R07 | 配方变更无活动运行时重载；不做热加载 | 新增配置可在同程序版本生效；在途与历史快照不改写 | 编辑发布UI、后台热更新机制 |
| R08 | 22条S1参数化基础用例，业务差异按缺口附加或复用 | 可审阅、合法、成本可控，不需优化求数学最小集合 | 非法混型号合盘、全场景×22×全故障穷举 |
| R09 | 按用户授权定义F握手并补入指定协议；E/产品轴/取放/旋转/组处置保留明确输入缺口 | F的3/4复位规则由上位机约定；其他现场语义不能由此推出 | 擅自把F约定外推到E或取放、把测试坐标当真实生产依据 |
| R10 | 保留实际3—5秒/10秒Test时长，按调用量核算预算 | 新增同面融合和多面会增加耗时；心跳不能被放宽掩盖 | 固定120秒或为了过测缩短已要求时长 |
| R11 | 模板仅取结构；检查按当前宪章解释 | 本地模板仍有“统一策略/注册”旧措辞，与5.0.0架构自由不完全一致 | 为模板凑模块、修改宪章迁就实现；模板不在本轮修改范围 |

## 原型与构建事实

只读ZIP含a.html、data-view.html、login.html；摘要见plan。
a.html第198行附近btnRecipe打开现有配方配置弹窗；第373行附近产品型号select；第395行附近只读配方ID；顶部第185—187行配方/版本。
这些位置尚未接入22配方。原型运行区只有启动、暂停、急停，缺少换面、取盘与恢复核对控件。用户已撤回独立调试界面，授权为必要控件最小修改客户原型：006在现有正式页面补齐三个入口，绑定后端允许动作及实际状态；具体映射见006合同。当前缺口是实施和验证，不再等待原型修改授权。本轮未修改原型ZIP或页面。
build.mjs复制src/pages及src/runtime.js并移除输出中的TS绑定文件；沿用实际HTML/JS入口，不按旧plan的Vue称谓另建框架。

## 技术研究与外部输入的分界

语言、运行时、存储、执行组织、用例方式已选定；研究阶段没有通过猜测关闭现场合同。
用户已授权上位机定义F握手，参照指定协议§3.1.7最小补充：命令5采用公共XY及Scan_Target_Z，当前周期反馈归扫码Z；到位后3并清移动命令，采集/解码有限结束且保存后4，PLC复位成功后PC清0。4不等于识别成功，F唯一绑定和保存成功另作产品放行门禁。
该Word仅新增7段及补充2个字段说明；其他15个ZIP部件内容不变，哈希及备份见对齐记录。新协议已定义，B01-F仍待Host/VirtualPlc实现和实际验证；B01-E仍缺外部输入。B09-U为已授权正式页面控件实施，B09-H为心跳实现/验证。其他B依赖按动作分别保留，详见执行合同。

## 流程参考

本轮按本地speckit-plan技能完成研究及设计；不调用tasks/implement。
官方[存量项目指南](https://github.github.com/spec-kit/guides/existing-projects.html)强调基于已有架构与测试规划；[规格演进指南](https://github.github.com/spec-kit/guides/evolving-specs.html)支持先更新spec，再修订plan/tasks并分析一致性；[行动指南](https://github.github.com/spec-kit/reference/agentic-sdd.html)支持大型功能分阶段实施。
这些参考说明工作方法，不替代项目业务合同或当前实现证据。

## 派生软件时序补充研究（2026-09-26）

- Decision：保留原图，另写[派生时序](sequences.md)，按[来源追溯](sequence-alignment-20260926.md)逐项区分接口细化、真实顺序/轴差异和已有确认。
- Rationale：上一轮漏改001 sequences；原始用户附件已直接确认Flip、无重扫、分拣两阶段和盘末顺序，公共XYZ另有002 FR05用户授权记录、公共3D复位有003既有验收链记录。当前代码仅用来定位待改消费者，不作为设备规则来源。
- Alternatives considered：未采用重画原图、按日期一概覆盖、复制第二套执行器、把所有未知作为Q03全局阻塞。模型和技术选型不变，不重生data-model或API。剩余特殊旋转/生产标定只限制依赖动作。

## USR-20260926-D研究结论（2026-09-26只读现状）

早期代码清单与阶段状态保留为历史快照，不作现状依据。当前StartPublicPreparation可创建新run并执行完整链，重复requestId返回旧run；FixedMoveRecoveryInteraction仍等待continue并同operation attempt=2，不符合D。ResetAsync只检查Ready/Auto/无Fault，StartupReadiness未含完整初始检查；VirtualPlc普通reset不清specialActive/occupant，旧specialTask可能晚回写。详见[消费者和证据交接](plan-restart-alignment-20260926.md)。

- Decision：复用既有recovery-reset/checks及POST /runs，增加最小restartFrom与持久关联；故障不走continue。Rationale：真实新身份及公共准备不被旧完成标记旁路。Alternatives considered：同operation attempt2改名、恢复旧handoff均违反D；新恢复平台/新页面超出当前主链需要，拒绝。
- Decision：按协议§1.5/4.3/5.2、现有方向/握手和U05观察实际初始状态，未知物理/特殊占用不放行。Rationale：Ready只证明协议就绪；epoch只关联隔离。Alternatives considered：只清内存、硬写坐标成功或创造生产旋转反馈均无依据。
- Decision：复用已有GROUP/ASSEMBLY、NG优先明细、IntegratedDetectionPort E/F式复位与VirtualPlc旋转Test请求；特殊复位仅补必要Test状态观察。Rationale：实际源码及TRX已体现这些能力，不重建引擎。生产坐标/高度/容量按U07延期，不要求重复业务确认。

## 2026-09-26 USR-E当前决策（替代历史R08覆盖义务）

Decision：复用Host/VirtualPlc/目录和日志，按实际差异选择3＋1代表；分请求、接收、实际反馈、Host校验。
Rationale：批量XY、同值变化日志省略影响可见性；分拣两端实际XYZ存在静态缺口，用户运行根因仍需核验。
Alternatives：不删Y校验、不复制目标当实际、不放宽超时、不22/14/8全跑、不重写引擎。取放目标采样与安全抬升分开，生产缺观察仅局部受限。来源和消费者见[交接](plan-six-issues-alignment-20260926.md)。
