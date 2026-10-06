# 008 当前证据索引（宪章7.0.0，2026-09-26夜间）

2026-09-27 恢复后最新状态：r5已结束七次实际WPF，其中六个原验收子范围通过、job006场景审计失败保留；job008为页面启动前工具失败，不计实际WPF通过。已退出包的实际分类见 r5/checkpoint-20260927-0047.json；前一0046汇总误把未生成独立场景审计的Q01-PAUSE当失败，修正依据为原operation19项及新真实读回三项，不覆盖原快照。全部通过子范围仍不抵物理处置查询缺口。

worker9476在job008错误记录文件锁异常后已退出。按用户询问授予固定无登录触发器恢复任务的查询/执行权后，Codex从Session0实际启动Administrator/Session2 worker10536，重触发实际复用同PID。当前批次续跑目录 `page-next-closure-20260926-night-r5-resume-1`，job009旋转OK已派发；原剩余路线及Pending/E错误独立复验自动串行继续。未停止任何原有效worker，业务构建/fixture不变。完整事实与任务授权见 desktop-worker-control；自动reload仍待当前批次结束后实际验证，登录启动尚未配置。下方为历史时点。

2026-09-27 最新检查点：当前批次为 `page-next-closure-20260926-night-r5`，复用现有 Administrator / Session 2 worker PID9476。job-000 真实 WPF 短预检与 job-001 Q01-PAUSE 正式路线均 exitCode=0、cleanupVerified=true。后者 runId `2b7754cf-b19e-44b4-8c44-d76d8744fae2`，暂停/检查/原 run 继续至 Final、初始3D/F一次、标题刷新与重开恢复等19项检查全部通过。T057容量/预留及在途/占用、T051预算3.2、T068正常暂停的代码缺口已实施并有必要定向/API证据；其他正式代表及全条件审计继续，008尚未完成。下段 r4 为此前检查点，原事实保留，详见[持续实施记录](implementation-night-20260926.md)。

本轮进行中：[无人值守修复、实际预检、完整任务条件审计及正式路线记录](implementation-night-20260926.md)。当前证据目录 `artifacts/recipe-execution-008/page-next-closure-20260926-night-r4`；成组 F 混合正式 WPF 已通过，同run读回见 `runs/job-001-GROUP-F-MIXED/GROUP-F-MIXED/operation-route-validation.json` 与 `scene-acceptance-audit.json`。其余新作业逐条验收，未结束不计Passed。T057目标预留/占用与T068正常暂停仍有实际实现缺口，008尚未完成。

前批最终普通/结果/参数/故障新轮页面证据见[前批记录](implementation-ed-res-20260926.md)；T058准确完成状态保留。当前四面仅3＋1代表，不将下方历史22/22、旧协议构建或旧单指令恢复视为本轮完成条件。下方全部原索引为历史检查点，日期与原事实保留。

---

# 历史索引（原宪章6.0.0）

## 当前批次结论：普通无 E 自动路线（2026-09-26）

Q01—Q22 的 Test/VirtualPlc 正式 WPF 页面整链为 22/22 Passed；配方版本、构建摘要、runId、完整相机顺序和逐步证据见[覆盖矩阵](../coverage-matrix.md)及[Q04—Q22 批次报告](q04-q22-test-batch-20260926.md)。Q03 NG/Pending 页面证据仍见[前一实施批次](implementation-batch-20260926.md)。C01—C08、E、人工、组策略、旋转、混合分拣、生产标定及旧 007 测试适配仍按各自条件保留，不因普通 Q 路线通过而整体完成。

## 历史检查点：20260925分区协议静态对齐（2026-09-26）

新版尚无端到端通过证据：Q=0/22，C=0/8，必要F待按影响补验。代码/fixture仍旧版；本次仅文档静态对齐。
旧协议Q01/Q02为2/22正式WPF Test通过，Q01-PARAM为C08历史通过；Q03只有二次3D/第二面组件证据，无实际PLC翻面与正式页面Final。历史材料正文和任务勾选不改，历史事实不自动迁移。
新翻面字段/ACK已定义，后续先共享协议/翻面/下料，再多面合法目标/预算，优先Q03到Final；见[对齐记录](../protocol-alignment-20260926.md)和[实施清单](../implementation-checklist-20260926.md)。


## 旧协议历史结论：第八批自动多面主链（2026-09-25）

[阶段检查点、当前代码和组件读回](eighth-batch-auto-multiface.md)：Q03第二轮3D→轮2目标解析→第二面AB的隔离Test组件运行已通过；当时正式自动Flip因取放双坐标提交映射未明而受限（该外部阻塞已被新§3.1.5替代），目录仍Restricted，Q03从WPF到Final仍**NotRun**。Q04以后自动无E路线尚未开始正式运行。第七批报告内把正常翻面触发和全部反馈关联也列为外部缺口的判断已由本批纠正；保留其历史原文。Q01/Q02/Q01-PARAM的既有正式WPF Test虚拟通过不受影响。

## 旧协议历史结论：第五至七批（2026-09-25）

第七批Q03仍为**NotRun/动作受限**：[自动换面接口核对、受限Test配方、面轮次校验及F失败复核](seventh-batch-q03-readiness.md)。Q01/Q02和C08原Test虚拟通过事实不变；Q03无页面至Final证据。下方第五、六批当前结论仍按其实际范围成立。

[Q01/Q02正式WPF虚拟Test整链证据](fifth-batch-q01-q02.md)：Q01与Q02先后从页面选配方、启动到同页取盘确认和Final，2/22条Q正常路线为 **Test虚拟端到端通过**；其余20条NotRun。两包的`verified-facts.json`交叉核对3D/F、产品PLC、worker、SQLite、媒体和页面。Q01采证器旧版终止标记误报已在证据中解释，原报告保留。C01的Q02非连续槽位部分有实跑证据，C06的普通OK尾段有实跑证据；各C项完整条件及F项不可据此整体判通过。下方旧状态段落保留作历史，不作为当前总数。

| 历史验收项 | 当前状态 | 依据 |
| --- | --- | --- |
| Q01、Q02正常路线 | Test虚拟端到端Passed，2/22 | [第五批同run证据](fifth-batch-q01-q02.md)；任务勾选另按原完整条件 |
| Q03—Q22 | NotRun，20/22 | [覆盖矩阵](../coverage-matrix.md) |
| C01、C06 | 部分实跑，整项未判Passed | Q02非连续槽位；Q01/Q02普通OK尾段 |
| C08 | Test虚拟Passed | [同程序新版本正式运行与实际参数差异](config-change.md) |
| C02—C05、C07及F1—F6整项 | NotRun或仅有组件子范围 | [覆盖矩阵](../coverage-matrix.md)与[本批必要门禁](sixth-batch-gates.md) |

[Q01-PARAM正式WPF运行](config-change.md)从页面选R008-Q01/1.2.0-test至Final，同程序构建相对Q01基版实际改变P01/P03槽位、采集请求和算法参数；C08据实记为Test虚拟Passed。第六批[F不匹配/保存/Final门禁](sixth-batch-gates.md)及[逐任务审计和多面前置](sixth-batch-task-audit.md)分别列明负例与未关闭条件。旧共享Final集成测试本次F超时，失败TRX保留，不计通过。

## 历史快照：计划与前四批（下方旧状态表不代表当前总数）

2026-09-24完成计划与合同修订；2026-09-25累计新增前三批部分组件、页面代码与定向证据，仍未产生008完整业务运行包。Q01—Q22、C01—C08及F1—F6全部未执行。细项见[覆盖矩阵](../coverage-matrix.md)，用例见[recipe-cases](../recipe-cases.md)，包要求见[证据合同](../contracts/evidence.md)。

| 历史验收项 | 记录方式 | 状态 |
| --- | --- | --- |
| Q01—Q22 | 前端配方版本→实际对象完整序列→run/设备/算法/保存/Final | NotRun；0/22通过 |
| C01—C08 | 对应合法完整用例与业务差异，可与Q共用run | NotRun；0/8通过 |
| F1—F6 | 适用必要失败、受限或合法收敛及可定位诊断 | NotRun；不与所有Q交叉穷举 |
| 文档核查 | [plan-document-validation.json](plan-document-validation.json) | 仅静态文档及保护范围核查，不计业务通过 |
| 首批/第二批输入与组件 | [Q01输入](input-readiness.md)、[Q01受限数据](q01-second-batch-data.md)、[预算计数](budget.md)、[002目录](../../002-plc-xyz-recipes/evidence/008-catalog.md)、[003 F](../../003-plc-latest-protocol/evidence/008-f-handshake.md)、[003心跳](../../003-plc-latest-protocol/evidence/008-heartbeat.md)、[003 API](../../003-plc-latest-protocol/evidence/008-api.md)、[001移交](../../001-station01-public-preparation/evidence/008-handoff.md)、[006页面](../../006-frontend-station01-console/evidence/008-prototype-delta.md)、[007夹具](../../007-station01-integrated-loop/evidence/008-fixture-tooling.md)、[007 worker](../../007-station01-integrated-loop/evidence/008-worker.md) | specs/003-plc-latest-protocol T067、specs/007-station01-integrated-loop T032组件完成；第二批目录、Test夹具、API受理、预算计数与页面控件仅部分完成，Q01仍受限；不计 Q01 通过 |

历史007当前构建页面Blocked及旧构建成功包保留原范围，不能抵扣新路线。后续实际证据目录为artifacts/recipe-execution-008/<caseId>/<runId>/；目前这些只是计划位置。

## 第三批组件与页面代码进度（2026-09-25）

定向命令、通过数和边界见[第三批验证记录](third-batch-validation.md)。
Q02非连续槽位与C/D受限数据的具体证据见[批次准备](batch-order.md)。

## 第四批单面执行单元（2026-09-25）

[第四批验证记录](fourth-batch-validation.md)保存AB单槽、CD非连续两槽的VirtualPlc/TCP、模拟媒体、独立worker及SQLite/媒体读回证据，以及到位不符、必要保存失败、复位失败三项停止证据。所有产品XYZ均为显式`Test/InjectedXYZ`组件输入；Q01/Q02正式配方仍Restricted，Q/C/F完整运行状态仍为NotRun。新组件证据不抵扣正式前端、公共F/3D映射或Final。

已新增[执行门禁](execution-chain.md)、[采集融合代码](capture-fusion.md)、[保存/查询边界](persistence.md)，并更新[预算期限](budget.md)。共享证据：[产品PLC组件](../../003-plc-latest-protocol/evidence/008-product-motion.md)、[API投影](../../003-plc-latest-protocol/evidence/008-api.md)、[前端同页控件](../../006-frontend-station01-console/evidence/008-prototype-delta.md)、[Fixture](../../007-station01-integrated-loop/evidence/008-fixture-tooling.md)、[页面采证脚本](../../007-station01-integrated-loop/evidence/008-page-tooling.md)。Q01模拟媒体清单按A/B区分；Q02受限Test数据按C/D及P01/P03区分，配方摘要与夹具一致，计划顺序测试1/1通过。两份PrepareOnly文件分别位于`artifacts/recipe-execution-008/third-batch-prepared/`和`third-batch-q02-prepared/`，均无业务POST。定向验证：后端门禁/预算/面配对及原期限合同11/11、严格Pending与旧三阶段回归18/18、Host/VirtualPlc检测握手2/2、A/B文件采集1/1、独立worker单/双输入2/2、前端运行时全套35/35；各自仅证明所测组件。产品点位/高度映射未确认，桌面会话断开，正式入口仍Restricted；Q01/Q02没有完整运行，Q/C/F状态仍为NotRun。相关新任务全部未勾。

---

# 以下为旧3.2.0计划索引（已失效的验收分配，仅作历史）

旧每M两配方、旧T编号及示教条目不再作为执行要求；保留原表是为了追溯当时计划，不能据此更新勾选。

# 008证据索引（计划位置）

2026-09-24：本轮只有文档静态核查，无新增业务运行、PLC动作、数据库迁移或端到端测试。
所有下列状态是NotRun；列出的路径是未来证据约定，不是已存在通过包。
运行包应在artifacts/recipe-execution-008/<caseId>/<runId>/，结构见../contracts/evidence.md。

| 流程 | 两配方及操作变体 | 验证任务 | 当前状态 | 实际run/operation/commit/证据包 |
| --- | --- | --- | --- | --- |
| M01 | AB1、CD1 | T013 | NotRun；依赖见矩阵 | 未产出 |
| M02 | AB1、CD1 | T014 | NotRun；依赖见矩阵 | 未产出 |
| M03 | AB1、CD1 | T015 | NotRun；依赖见矩阵 | 未产出 |
| M04 | AB1、AB2 | T016 | NotRun；依赖见矩阵 | 未产出 |
| M05 | CD1、CD2 | T017 | NotRun；依赖见矩阵 | 未产出 |
| M06 | MX1-AUTO、MX2-AUTO | T022 | NotRun；依赖见矩阵 | 未产出 |
| M07 | MX1-MANUAL、MX2-MANUAL | T023 | NotRun；依赖见矩阵 | 未产出 |
| M08 | MX1、MX2 | T024 | NotRun；依赖见矩阵 | 未产出 |
| M09 | MX1、MX2 | T025 | NotRun；依赖见矩阵 | 未产出 |
| M10 | R1、R2 | T034 | NotRun；依赖见矩阵 | 未产出 |
| M11 | R1、R2 | T035 | NotRun；依赖见矩阵 | 未产出 |
| M12 | R1-OK、R2-OK | T036 | NotRun；依赖见矩阵 | 未产出 |
| M13 | R1-NG/Pending、R2-NG/Pending | T037 | NotRun；依赖见矩阵 | 未产出 |
| M14 | AB2-NG、CD2-Pending | T029 | NotRun；依赖见矩阵 | 未产出 |
| M15 | G1、G2 | T030 | NotRun；依赖见矩阵 | 未产出 |
| M16 | A1、A2（R2另覆旋转） | T038 | NotRun；依赖见矩阵 | 未产出 |
| M17 | AB1、CD1 | T042 | NotRun；依赖见矩阵 | 未产出 |
| M18 | AB1-TEACH、CD1-TEACH | T043 | NotRun；依赖见矩阵 | 未产出 |
| M19 | AB2-RECOVER、CD2-RECOVER | T044 | NotRun；依赖见矩阵 | 未产出 |

后续每行分别登记两配方，不可只填一个总报告链接；共享run须提供该M对应动作/保存索引。
M13两配方各自包含NG和Pending出口；M16普通A1/A2及旋转R2均需跟踪。
历史007当前构建页面Blocked记录见../../007-station01-integrated-loop/evidence/t015-t044-20260924-current-validation.md，不抵扣本表。

## 当前任务索引（文档更新，未运行）

Q01：specs/008-recipe-driven-inspection T055；Q02/C01/C06：T059；同程序配置变化C08：T058；Q03—Q22/C02：T062；C03/C04：T066；C05：T067；C07及F1—F6必要补验：T069；汇总T070。当前任务详见../tasks.md及../coverage-matrix.md。旧记录中的每M两配方仅为历史，不适用当前验收。本文档更新不产生运行证据。

2026-09-26 新版协议实施批次的 Q03 正式页面 Final、双实体连续翻面、单件 NG/Pending 分拣及剩余受限范围，见[本批运行证据与进度](implementation-batch-20260926.md)。上方历史索引中的“未运行”不覆盖该新增证据；各任务整项勾选仍以对应完成条件为准。


## 最新夜间入口（2026-09-27，进行中）

当前状态以[完成条件复核](completion-review.md)末尾检查点和[实施记录](implementation-night-20260926.md)增量为准。当前独立构建批次`artifacts/recipe-execution-008/page-next-closure-20260927-projection-r6`；前批成功和失败包全部保留，不因工具/配置修复改写Passed。[第一轮converge](converge-night-20260927.md)追加0任务，仍继续既有任务验收，未宣布Test/008完成。

## 2026-09-27 02:31当前检查点（优先于历史状态）

当前root为page-next-closure-20260927-log-control-r8，worker4992/Administrator/Session2持续复用；r7→r8自动接班先新ready再旧退出，累计四次真实reloadWorkerRoot接班。无登录触发器配置，待本批完成后处理。

当前冻结Host A2AD71BB9A3ABEB8A264BDC4968AC106FA11103F553EA3F0CFFCFBE818D1B653、PLC 66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF（实际完整SHA以build-freeze.json为准）、Application 7E086195C239F4D0B9511998A874220A5FB7BE62306BE98AE276443EA8733634。框架重复查询日志已由实际包验证受控；现有业务超时未放宽，此前通信超期根因仍未知。r7临时进程内采样已移除，部分截断trace只作诊断，不计业务Passed。

r8 job001真实run ab6b70d9-c28a-47b8-b403-1e718d170b09：P03 Pending→P15可靠提交Completed，P01 OK NoMoveRequired，页面刷新/重开处置正确；24检查23通过，唯一源槽位工具错误使整包Failed，原包保留。真实协议只要求放置前一次slot3写入，已将工具改为精确回显/偏移/动作关联。三项必要工具读回通过，新job013在同worker自动执行；其他job002—012按原冻结顺序等待。不能以已通过子范围代整包或任务完成。T058及质量15/16不变。

## 接续检查点：2026-09-27 03:22（优先于前文历史状态）

最新持续事实见[中断接续记录](continuation-20260927.md)。r8 Q02-PENDING-P03及Q04-MANUAL正式WPF已分别通过场景/预算/取放适用审计，退出清理可靠；非连续P03及普通人工此前缺口的这些子条件已齐。整体人工r8因260字符图片路径、r9因通信失败，均保留Failed；r10复制程序切换GC模式比较仍启动通信失败，不采用为修复。r11有限启动trace完整，预检通过不计正式业务。

当前r12-0927使用新Host，真实SQLite持久化初始化先于设备服务启动；7项HostLifecycleTests全部通过。原PLC/default server GC/协议期限保持，冻结信息见r12/build-freeze.json。既有reload交班到Administrator/Session2 worker10396，新正式ASSEMBLY-A-E-MANUAL job000已启动，run53762f43-c488-4153-a8b8-82eb2d629903，尚未计Passed。启动日志已证明初始化完成→PLC启动；不能据此宣称通信根因已解决。其余选定代表待本条完整读回后逐条放行。T058及清单15/16保持，008/Test总体仍进行中。


最新接续根目录已至r13-0927；r12整体人工/旋转Pending通过、Q04启动超期保留。预编译比较和后续结果以[接续记录](continuation-20260927.md)最新检查点为准，008总体尚未收口。

当前接续至r18-io-0927，r17成组Failed保留，具体运行、范围和后续以[接续记录](continuation-20260927.md)最新检查点为准；尚未关闭008/Test或父任务。


## 2026-09-27 最终当前入口

当前Test选定主流程及必要验收完成，008整体未完成。完整当前状态、20/22任务条件、原失败统计/构建适用和恢复入口见[收口报告](completion-review.md)最新节、[逐任务审计](task-audit-night-20260927.md)最新节及[覆盖矩阵](../coverage-matrix.md)最新节。r22/current-primary-summary.json为T069唯一当前恢复主包，r22 job000为当前Q18；历史状态不覆盖本入口，T055/T070及直接共享父任务其余条件不冒充完成。

## 2026-09-27T05:07:42.335482+00:00 T065机制修复及当前正式Q01收口（本节优先于历史待办状态）


| 原条件 | 证据及结论 |
|---|---|
| 003 T065机制与最小修复 | 同NativeOverlapped批前后→64→Read直接关联，两个真实1秒超期主要在批后1112/1057ms；同DLL冷启动Native0/1切断该路径；正式最小Test接线，无放宽期限 |
| T065受控对照 | 冻结DLL/config/profile/初始化及inline0相同，只有运行时provider；业务/心跳txn3按连接分列；候选冷启动业务txn3 Read6.3766ms，心跳txn3 Read0.2003ms（后一瞬时指针0，不能伪造64关联）；全部1560响应头最慢20.2574ms |
| T065真实超期安全 | .NET10.0.12 Native=True下真实TCP响应丢弃I/O1000ms及PauseHeartbeat3000ms，均latch、epoch1→2、拒绝新Move；真实TCP1秒超期无自动重发3请求；TRX3/3，无Skipped |
| T065日志 | 连接/事务/PID/TID/QPC关键阶段及失败窗口保留；必要测试验证慢成功与真实失败窗口、deadline/端点/事务/GC字段；正式Q01持久RuntimeFlow/Modbus审计；历史缺日志不补造 |
| 008 T055 | 当前正式新DLL＋既有WPF/runtime，Q01 run6333b690-ca2f-4d10-83bf-bce13b8db8fd；实际页面选用/启动/取盘、冻结R008-Q01 1.1.1-test、公共3D/F、A/B每图/融合、XYZ/复位/PLC、SQLite/四媒体读回、完整尾段/Final、刷新重开；18/18，exit0/cleanuptrue |
| T055直接门禁与依赖复用 | 本轮改动仅运行配置/最低线程API及所属启动接线；旧F不匹配不动产品、产品到位/复位/保存失败不Final及USR-E必要XY/XYZ同值/变化Y，沿task-audit-night-20260927最终节、r19/r21必要合同/TRX、r22新恢复AB真实包及已有Q01-PARAM复用各未改业务分支，原来源/构建/配置差异保留；不声称全旧包来自新DLL |
| 008 T070 | 原最终20/22审计＋本轮T065/T055补齐，对账T049—T070原22项、直接依赖子交付、现行SC选定路线/C01—C08/F1—F6；原两轮converge追加0及静态核查复用，业务算法/配方/数据库/前端分支未改；仅追加本Q01一包，不重跑全矩阵/不修改退出Q历史 |

本结论仅为原任务允许的Windows Test/VirtualPlc主流程范围。Native开关显式绑定新正式构建/合法fixture，旧默认/旧冻结程序/全部失败/原assessment及工具fix/test不改。没有声明所有历史故障同因、所有VM调度已解决、r22 HTTP已修复或真实设备/现场标定完成。原3516ms分段日志缺失明确保留。质量清单15/16不动；非阻塞未来工作及生产未知保持原待办。

本轮新增正式Q01一包Passed（run6333b690-ca2f-4d10-83bf-bce13b8db8fd），在旧48次24Passed/24Failed之外单列；原尝试/失败索引、恢复主包、权限拒绝及35个必要用例的原构建适用关系不覆盖。三项原条件满足后，仅003 T065、008 T055/T070变X；008原22任务当前22/22，质量清单仍15/16，不等于生产现场或全项目已完成。详细新assessment/fix/test/proof、源码diff、ETL与正式包摘要见[本轮证据](../../../.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/evidence-index.md)。
