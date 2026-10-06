# 008 当前完成条件复核（2026-09-26夜间，进行中）

本记录为T070要求的收口入口，尚非完成声明。完整22项任务条件对照见[实施记录](implementation-night-20260926.md)，本轮批次为`artifacts/recipe-execution-008/page-next-closure-20260926-night-r4`。质量清单15/16保持，T058保留原准确勾选；其他任务须满足各自完整条件后更新。

| 范围 | 当前有效证据 | 仍须完成 |
| --- | --- | --- |
| 无人值守工具 | r4当前Administrator/Session2 worker及job-000实际WPF连接/退出/完整清理；独立合法路线预算；HTTP/CDP有限退出探针 | 保持现有worker，正式作业逐条核实cleanup后再读SQLite；后继脚本/程序集变更必须加载新worker |
| 当前普通及参数 | 前批9条最终选定路线，同最终Host的Q01/PARAM；[前批记录](implementation-ed-res-20260926.md) | Q04/Q05/Q06本轮差异验收、Q01-NG/Q02-PENDING原指定用例；新变更影响范围再作必要复验，不全量重跑 |
| 成组C03 | r4 job-001/002两组四成员、NG优先/Pending、问题成员一次取放、正常成员保留，所有成员/面结果及Final对账 | GROUP-A-E合法四面/E当前运行中；容量预留/在途提交缺口T057与必要缺成员门禁证据 |
| 整体C04 | 已有后端部位/整体子证据；当前正式队列已安排NG、Pending、E缺码及E错误路线 | 当前正式包结束后逐部位身份、媒体、整体一次共享动作和处置审计 |
| 特殊旋转C05 | 已有Test后端三出口及整体OK子证据 | 当前正式三出口/整体差异、占用/姿态门禁与不重复外层搬运证据 |
| 人工换面C02 | 已有Test握手子能力；正式普通/整体两条已排队 | 真实页面确认、安全解除/Complete、同run且初始3D/F一次；不抵扣正常暂停或故障恢复 |
| 正常暂停 | 缺陷已定位，见`.specify/bugs/008-pause-continuation/assessment.md`；直接依赖文档增量已完成 | 安全边界Paused/Continue实现、当前动作握手继续完成、同run完整Final及必要测试 |
| 故障新轮C07/F5 | 前批双端复位/初始核验/显式新启动/完整新run Final，旧故障可查 | 原故障首图前，无旧图片；补实际3D媒体之后F运动故障的最少页面包，不以工具保存样本替代 |
| 页面结果 | 前批OK/NG/Pending/媒体关联及同构建参数事实；本批已结束成组判定刷新/重开正确 | 实际截图另证当前配方标题重开丢失，按既有后端投影修复并检查标题/版本，旧截图保留 |
| 预算T051/处置T057 | 已有实际冻结期限、当前取放真实成功 | 补两段取放/ACK/必要保存的合法计数；固定目标冲突/占用/预留及可靠取料后提交，不能以实际快于错误预算关闭 |
| converge | 当前审计发现由现有任务覆盖，无理由新增重复任务 | 本轮实施/正式验收后执行技能，按来源逐项检查；实现缺口继续原任务，证据缺口补验收 |

当前仍有环境可解决的实现及验收缺口，继续执行。生产PLC采样窗口、真实点位/高度标定、真实相机与算法精度只按原输入来源记录实际局部限制，不新增生产门槛。当前Test范围和008整体均未宣告完成。

## 2026-09-27 r5 当前状态（优先于上文旧检查点）

r4已结束的两条成组子范围仍有效；GROUP-A-E旧job-003验收失败（PLC导出未分页）；整体NG旧job-004业务验证成功但外层清理失败，二者均不可计整包Passed。原r4剩余排队作业未执行。现在复用r5交互worker PID9476，实际WPF短预检job-000 exitCode0/cleanupVerified=true，正式Q01-PAUSE job-001进行中。

T057预留/容量/在途/占用提交，T051 3.2计数及局部期限，T068正常暂停安全边界、配方标题恢复和PLC分页已实际实现并完成必要定向/API验证，详见implementation-night-20260926.md及三个缺陷fix.md。代码缺口已修复不等于全任务验收结束；旧表中的这些代码缺口为历史检查点。当前仍须正式页面、实体/媒体/PLC/持久读回、必要门禁适用范围和converge收口。登录自动启动配置延后到本批验收结束；后续优先复用有效worker，必要时验证reloadWorkerRoot先就绪再交班，不再次依赖人工批次启动。

### 恢复后续跑检查点

r5 Q01暂停、Q01-NG、Q02-Pending、Group-F混合、Assembly-NG、Assembly-E缺码六个原验收子范围已通过；Assembly-Pending原场景审计工具错误仍Failed，已安排独立job020。job008启动异常被stderr锁二次异常遮盖，worker9476已退出；原启动失败及不充分cleanup分支保留。固定无触发器任务获查询/执行权限后，Codex真实恢复worker10536并再次触发验证复用同PID；旧进程/端口/日志锁已独立核查释放。续跑root night-r5-resume-1承接剩余代表及E错误新job021；目前旋转OK进行中。

已定位原处置查询缺口：SQLite有可靠完成事件，API dispositionState为空、movements为空。003/006/008直接合同与设计已先更新，由既有T054/T057/T059/T066/T067/T070承接；当前冻结批次完成后才修改业务源码/构建并最少复验。非连续P03实际分拣代表同该复验补齐。converge和父任务完整证据判定尚未完成，不能声称Test或008整体完成。


## 最新检查点：2026-09-27 01:43，前文为历史

当前root为`page-next-closure-20260927-projection-r6`，有效Administrator/Session2 worker5040。固定按需恢复授权已真实使用，不再需要每批手动启动；10536→9936→5040两次reloadWorkerRoot均在后继ready之后原worker退出，无登录触发器配置。Host独立诊断构建5CB0250D…、PLC7AFDE51A…、Application FD4EE7C7…；默认旧程序及原型不覆盖。

处置投影源码已实现，必要纯投影15/15及阶段生产者15/15，旧已退出Q02 SQLite回放正确；正式新构建WPF预检通过。新P03 job001实际进入业务，在分拣前因Test fixture未给P03绑定目的点而SortingTargetUnconfigured，未派发分拣动作，当前等待采证有限退出清理；不能算Passed。独立1.1.4准备复用已有同盘P15目的点，旧1.1.3失败保留，清理后才切换入口。

第一轮[converge](converge-night-20260927.md)未发现需追加的新任务；正式证据及既有父任务条件仍未齐。当前继续实现/验收，Test和008整体均未完成。

## 2026-09-27 02:31当前检查点（优先于历史状态）

当前root为page-next-closure-20260927-log-control-r8，worker4992/Administrator/Session2持续复用；r7→r8自动接班先新ready再旧退出，累计四次真实reloadWorkerRoot接班。无登录触发器配置，待本批完成后处理。

当前冻结Host A2AD71BB9A3ABEB8A264BDC4968AC106FA11103F553EA3F0CFFCFBE818D1B653、PLC 66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF（实际完整SHA以build-freeze.json为准）、Application 7E086195C239F4D0B9511998A874220A5FB7BE62306BE98AE276443EA8733634。框架重复查询日志已由实际包验证受控；现有业务超时未放宽，此前通信超期根因仍未知。r7临时进程内采样已移除，部分截断trace只作诊断，不计业务Passed。

r8 job001真实run ab6b70d9-c28a-47b8-b403-1e718d170b09：P03 Pending→P15可靠提交Completed，P01 OK NoMoveRequired，页面刷新/重开处置正确；24检查23通过，唯一源槽位工具错误使整包Failed，原包保留。真实协议只要求放置前一次slot3写入，已将工具改为精确回显/偏移/动作关联。三项必要工具读回通过，新job013在同worker自动执行；其他job002—012按原冻结顺序等待。不能以已通过子范围代整包或任务完成。T058及质量15/16不变。

## 接续检查点：2026-09-27 03:22（优先于前文历史状态）

最新持续事实见[中断接续记录](continuation-20260927.md)。r8 Q02-PENDING-P03及Q04-MANUAL正式WPF已分别通过场景/预算/取放适用审计，退出清理可靠；非连续P03及普通人工此前缺口的这些子条件已齐。整体人工r8因260字符图片路径、r9因通信失败，均保留Failed；r10复制程序切换GC模式比较仍启动通信失败，不采用为修复。r11有限启动trace完整，预检通过不计正式业务。

当前r12-0927使用新Host，真实SQLite持久化初始化先于设备服务启动；7项HostLifecycleTests全部通过。原PLC/default server GC/协议期限保持，冻结信息见r12/build-freeze.json。既有reload交班到Administrator/Session2 worker10396，新正式ASSEMBLY-A-E-MANUAL job000已启动，run53762f43-c488-4153-a8b8-82eb2d629903，尚未计Passed。启动日志已证明初始化完成→PLC启动；不能据此宣称通信根因已解决。其余选定代表待本条完整读回后逐条放行。T058及清单15/16保持，008/Test总体仍进行中。


最新接续根目录已至r13-0927；r12整体人工/旋转Pending通过、Q04启动超期保留。预编译比较和后续结果以[接续记录](continuation-20260927.md)最新检查点为准，008总体尚未收口。

当前接续至r18-io-0927，r17成组Failed保留，具体运行、范围和后续以[接续记录](continuation-20260927.md)最新检查点为准；尚未关闭008/Test或父任务。


## 2026-09-27 最终本轮交付（本节优先于全部历史检查点）

当前Test选定主流程和必要验收已完成；008整体仍未完成。原22任务本轮完整审计后20项勾选，T058保持原X；T055因明文003 T065共享心跳延迟机制验收未全齐、T070因原T055依赖保留未勾。没有将未勾的父任务改名成“已完成子范围”，也不修改001/003/006父任务勾选；具体全条件、直接依赖及复用边界见[逐任务审计](task-audit-night-20260927.md)和[当前矩阵](../coverage-matrix.md)。质量清单15/16不动，不把文档或预检计为业务测试。

实现/修复包括：可靠取放预留/在途/占用及预算、真实物理处置投影、普通暂停安全边界与同run继续、实际旧媒体/恢复采证、命令/设备/保存持久结构化日志与重复查询受控、持久初始化先于PLC启动、有限预检/清理/接班、显式所属Test Host/PLC I/O配置。本次新修复为复位等待实际缓存PlcReady、旧Final门禁测试采用当前合法fixture、真实401/403权限状态保持；均有原失败、先合同后代码、独立构建及必要验证。源码/脚本/前端产物SHA与实际ResourcesResolved沿r20/r21/r22冻结包，普通默认配置和原业务/I/O/心跳期限保持。

当前独立必要验证35个不重复用例最终全Passed：r19当前合同18，SQLite/恢复/Final七项首次4Passed/3Failed原TRX保留，r21只复验三失败用例全部Passed，当前目录退出八项+容量/目标冲突两项10Passed。原投影15+生产者15、生命周期7及其余必要旧TRX仅按各未改源分支复用，不相加成当前全套测试。frontend build及PowerShell AST/Node采证语法通过，HTML四份与客户原型ZIP不变；新实际WPF runtime SHA3E6BCE15A6A560761CC055FD6E8009BEC8E6A2E64E792E5D830837CA372BBB29。当前正常Debug r21 Host实际Q18和恢复均通过，原PLC/默认server GC，只有所属Test进程明确I/O开关。

本接续正式路线尝试共48，整包Passed 24、Failed 24。其中exit0为25，但r5 job006整体Pending的场景Failed，故不能把exit0都算整包通过。权限拒绝页面3次：2Passed/1Failed，另外预检13次11成功/2失败，worker自动接班16次均成功，分别统计、不计普通路线。原page-ed-res 13次10Passed/3Failed是独立历史批次，不冒充本新构建。所有失败包位置见r22/failed-attempts-final.json，逐尝试定义/清理/各审计及源路径见r22/attempt-inventory-final.json；历史清理失败也保留原状态，不声称所有历史cleanuptrue。

最新Q18 run e4260221-41e2-4574-b705-33b6e8cebed6实际CD→AB→CD→CD，4面8采集4融合12Detection、初始Height/F各一次、3Flip/ACK、翻后Height0，普通OK无搬运但已提交NoMoveRequired，整盘Final/刷新/重开/实际字段一致。旧Q18 CD→CD→AB→CD合法历史不得改写成当前指定顺序。完整恢复主包见failures.md最新节和r22/current-primary-summary.json。

两轮converge：第一轮发现已有物理投影/人工/特殊/组/恢复及通信验证缺口，全部继续原T049—T070，追加0；第二轮在上述代码/必要测试/正式WPF后复核FR18/SC6/故事场景19/计划决策8/宪章原则13，无新增代码任务，追加0，检查期间tasks SHA3C0A4FF32655B98147F55FD878603AFE8CE572151BF627361B7B43A85F0A3918字节不变。既有T055/T070依赖条件保留，随后独立实施/证据阶段才更新19个勾选。最终analyze只读，没有Critical/High新增需求冲突，历史带日期状态及要求中既有实现备注为Low且当前证据入口明确；独立静态记录不算运行测试。

真正剩余原条件：003 T065的异常延迟机制及修复前后对照还不足，现有Test I/O缓解与多个完整成功代表只证明当前选定路线；r22 job001机构初始状态HTTP1秒超期也保留，不能宣称默认模式/VM调度根因已解决。过去缺失的3516ms窗口日志不能补造，后续需可控主机与有权限的系统级调度/I/O采样做定向前后对照；不重复同条件盲跑。生产局部原B限制包括真实点位/高度标定、取放目标阶段可靠采样、布局/容量及特殊机构生产映射，真实相机SDK/算法精度和现场节拍未验，不能通过Test反推参数。以上不是新增全Test门槛。

非阻塞待办：允许但未选位置变体、完整异常组合、性能长稳、示教/前端编辑保存、历史文档清理，以及Windows登录自动启动。固定恢复任务已有真实查询/执行授权、重复复用和16次自动接班；登录触发器仍0。脚本scripts/enable-008-desktop-recovery-at-logon.ps1已准备并AST检查，未执行，不宣称配置或登录实测成功；Codex仅获固定任务读/执行权限，不能改触发器。用户若需要自动登录接班，在Administrator交互PowerShell执行 `& "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -File "E:\dzk\gaode-1\scripts\enable-008-desktop-recovery-at-logon.ps1"`。该用户动作仅管理登录触发器，不影响本轮正式验收。

现场保留：r22 worker9428/Administrator Session2等待且queue/batch-paused.signal保留；新调度禁止自动下一业务job，所属Host/PLC/WPF/worker子资源按两个成功包与失败包各自cleanupVerified释放。桌面worker不提前关闭。若会话丢失，可按已有固定Gaode008DesktopRecovery任务执行恢复脚本，该任务仅复用有效worker或启动独立暂停root，不自动重跑业务。当前恢复请求root r22及worker加载SHA96787623D2CE21C9ED6C53FAF53FDD84D5001636B706401336D281341259DF85保留，生产/真实设备无操作，无提交/推送/发布。

范围澄清：早期Codex账户157ms ready探测使用错误worker/1.0 echo，只作该探测事实，不代表Administrator当前Host/2.0协议或正式页面验证。r12 source-differences比较37个源/脚本、7改动，不能称629项源相同；缺失baseline必须NotComparable，不造同构建证明。r22 build-freeze继承previousRoot=r17的元数据错误由source-freeze-amendment-metadata独立说明，实际worker前继r20、Host构建r21，原文件不覆写。

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

## 2026-09-27 XYZ部署复测及I1—I4接续（partial）

本节接续当时构建范围，不覆盖既有T065/T055/T070通过及勾选。最新缺陷整体仍partial：原本机RunningF缺同run日志，七格正式业务/相机映射未确认；Test临时格位不宣称正式映射。r22 HTTP超期仍独立保留。

| 入口 | 实跑版本/run | 可复用范围与限制 |
|---|---|---|
| Q01 | r3 / 0b877606-15de-4688-aedb-61747485266c | 5动作；普通OK不盘末取放、下料命令4及Final；r3页面统计错位是原失败，后续绑定改动不能倒填r3 |
| Q01-NG | r4 / f51c4aa4-a5e3-4e4e-bc76-ed013d89daff | 7动作；NG源槽→同盘P14、取料2/放料3/Sorting_OK清零；原显示检查器误选5节点失败保留，实际截图及离线复核另列 |
| Q02-PENDING-P03 | r4 / c1049872-0aa7-417b-8ca3-fd738f2c9d46 | 9动作；P03→同盘P15、P01 OK留原位；DOM/监控通过，原Stop拒绝身份和后续按身份清理均保留 |
| Q03 | r4 / 17d03621-1923-422c-9b67-98c73308dc6a | 8动作；翻面目标/面反馈/Flip_OK清零、下料及Final；原Stop拒绝和独立清理保留 |

合计29个动作/87轴：同动作引用、连接、事务、命令前完整原始双字与实际反馈Float32位核验；相同Y仍有本次写入，不能用changes是否出现判漏轴。该范围是既有Test实跑，不是本轮新设备测试。
r3→r4仅两个runtime副本与README改变；r4→r5仅Stop.ps1/README改变、343文件同摘要。r5只实际启动/停止，无配方启动；不能称r5重新完成这四例。r5 ZIP SHA256为70C5D53EA4E5CC19070516943A736085903439A8996AD2CD6CD5BA62A83CDF2E，原包保持。
本轮r6仅合同/离线脚本、监控分类/版本及包说明接线：新包差异、解压资源和必要显示验证以新报告为准，业务证据按逐文件未变范围复用，不重复配方矩阵。索引更新不是新增运行验收，不能据此修改任务勾选。

- [原复测证据索引](../../../.specify/bugs/xyz-sorting-deployment/retest-20260927T085415Z/evidence-index.md)、[原验证报告](../../../.specify/bugs/xyz-sorting-deployment/retest-20260927T085415Z/test.md)、[原运行/摘要](../../../.specify/bugs/xyz-sorting-deployment/retest-20260927T085415Z/verification-proof.json)。
- [I1—I4增量修复](../../../.specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/fix.md)、[增量验证](../../../.specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/test.md)、[增量证据索引](../../../.specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/evidence-index.md)。

本轮明确新包：`E:\dzk\gaode-1\artifacts\Gaode-008-Windows-x64-20260927-xyz-sorting-r6.zip`，revision `xyz-sorting-r6-sync`，SHA256 `d5b3115bb9d9063d6d36ff4e3809fac8774d67c9e085653d0b700553f0edb0f3`。r5→r6仅README.md、Start.ps1、VirtualPlc/wwwroot/app.js、index.html四项载荷不同，341项未变（包括Host/PLC业务DLL、worker、前端、Stop及冻结输入）。新包仅显示/接线验证，详见增量报告，不计新配方运行。


## 2026-09-27 monitor-xyz-history 最新监控纠正
此前独立完整XYZ栏目和XYZ公开信号命名要求由本轮用户确认取代：原列表完整XYZ逐信号发送/实际反馈、同值保留、公开XY_Move_Cmd/XY_Pos_Confirmed恢复来源Word。设计先同步，原项目源码已修改。见[本轮证据索引](../../../.specify/bugs/monitor-xyz-history/20260927T130246460967Z/evidence-index.md)。源码/最终包各13项、真实Edge原始证据重放7场景、新编译VirtualPlc只读地址表/资源核验通过；最终包截图与载荷摘要可查。
新包 `Gaode-008-Windows-x64-20260927-monitor-history-r8.zip`，SHA256 `d6ef8681580a995da99cf3545b96d09d615dc3ca20605ee9a5a59dfc021fbbf5`；相较r7共7项载荷改变、270项不变。新VirtualPlc来自当前源码构建，旧Host/worker/输入按摘要复用。历史29动作/87轴、r3/r4/r5/r6/r7与原失败保持原适用范围；本轮重放不是新业务run。原RunningF本机日志及七格映射仍待，xyz-sorting-deployment整体partial不变；不宣称008整体通过，不改任务勾选。
