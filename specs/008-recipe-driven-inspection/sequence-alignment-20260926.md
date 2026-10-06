# 派生软件时序补充对齐记录

日期：2026-09-26。本次是既有008及直接关联设计增量；上一轮protocol-alignment/file-impact/static-validation/implementation-checklist保持原文，本记录补充并纠正遗漏，不重写历史报告。**文档设计已补齐，代码尚待实施；新版Q03尚未端到端通过。**

## 来源关系及证据等级

- 原始来源：[分区协议](../../高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx)正文及全部表格；[总时序SVG](../../高德_文档/上下位机对接/总时序图.svg)/[PNG](../../高德_文档/上下位机对接/总时序图.png)；整体、单零件、分拣、采集、相机轴组和特殊零件闭环原图。读取SVG文字及节点关系，并查看总图/普通闭环/分拣PNG；没有用派生图覆盖来源。
- U26：用户上一轮原始附件“请在E:/dzk/gaode-1中完成一次面向008后续实施的全项目关联文档与任务对齐”，第三部分第2—7项。它明确要求自动Flip逐步握手、翻后不重采3D、分拣取放及槽号时机、普通盘末先下料定位再适用分拣、F3/4与对应Z复位。这是用户确认依据，不是旧对齐报告自证。
- U21：既有[002 spec](../002-plc-xyz-recipes/spec.md)开头记录2026-09-21用户附件授权，FR05明确公共固定XYZ、3D Camera Z、F Scan Z；[001 research](../001-station01-public-preparation/research.md)也保存该授权增量。原始20260921附件本轮未找到，不宣称读过原附件；采用其明确确认记录而非代码推测。
- U3D：[003 differences](../003-plc-latest-protocol/differences.md)关于§3.1.7的记录明确“用户本次明确的验收链0→1→2→0”，与003 FR04/05承接公共3D。此处只用于公共3D；其当时F1/2不复活，F以U26/分区§3.1.7的3/4为准。分区协议未单独写出“公共3D”字样，不把既有确认冒充协议逐字原文。
- 固定XY、3D仅Z、普通批采和F后唯一绑定按需求V1.1§2、§7、§11.2—11.9、§11.13及已有确认；软件保存/身份/页面门禁另按宪章P04/07/08/09/12及既有003 Clarifications。总图、协议、确认决定、软件设计不是同一层资料，不宣称原文完全一致。

U26附件本机路径：`C:\Users\codexsandboxonline.10_3_0_13\.codex\attachments\1c5cc009-2700-4069-88e8-074f05cbe0fc\已粘贴的文本.txt`；SHA256：`0741ED6EEF0A65AA64ED8DFDB322BB643265A9974940CD497B1E469CED6F424A`。关键确认的引用段落为第6项：“检测完成→下料位置到位→适用同盘分拣→整盘条件收敛→解锁反馈→页面取盘确认→Final。”

## 原图步骤→协议→确认→软件→任务

| 原图步骤/节点 | 协议条款 | 差异类别与确认依据 | 派生设计及唯一任务 |
| --- | --- | --- | --- |
| 总图1—11，上料/安全/实体按钮/夹紧；整体图启动链 | §3.1.1；§2.1/2.4 | 启动含真实物理输入；U21/002 FR03/04只确定现行虚拟启动与公共区域先行，生产双手按钮映射仍局部待现场确认；不能将API受理伪装实体按钮 | 001图§1保存启动与夹紧后公共区域ACK；specs/001-station01-public-preparation T090；已有启动能力复用，不扩大真机结论 |
| 总图12—17及“仅XY，无Z” | §2.2、§3.1.7检测周期 | 实质轴差异：U21 FR05明确3D Camera Z，U3D明确公共验收链；固定XY/3D仅Z另有既有决定。不是图示漏画Z | 001§1固定0003/0005/0007、命令2、XYZ及检测Z、1/2复位/清0；specs/003-plc-latest-protocol T070、specs/001-station01-public-preparation T090 |
| 总图18—29及F“仅XY，无Z” | §3.1.7①—⑦ | 实质轴差异，U26第7项明确F命令5、ScanZ及3/4；解码/绑定独立于复位 | 001§1；specs/003-plc-latest-protocol T067新版子范围、specs/001-station01-public-preparation T090；失败/不匹配/保存失败不放行 |
| 总图31—46；单零件图A全批→B全批→融合；CD47—61 | §3.1.3/3.1.4/3.1.7 | 原图未画逐图算法保存/复位；按需求§11.3/11.5和既有保存门细化，不能把概览内零件loop当成覆盖已确认整批顺序 | 008§3；specs/008-recipe-driven-inspection T052/T053/T054；specs/003-plc-latest-protocol T070；A批不等B融合，B输入齐并复位后可融合 |
| 总图62—65：取/放坐标、整体翻面、完成 | §3.1.5①—⑥ | 内部取翻放业务保留；U26第2项明确单次翻面位置及目标面提交，ACK原图省略，原双坐标文案不再要求两组专用字段 | 008§4，XYZ000B→3→本次到位→清Move→目标面→状态2且面匹配→Flip_OK1→状态0→ACK0；specs/003-plc-latest-protocol T071、specs/008-recipe-driven-inspection T060 |
| 总图66直接下一面；单零件闭环“进入下一面” | §3.1.5⑦ | 原图并未要求再次3D；旧软件强制Rescan才是冲突，U26第3项明确取消 | 008§1/2逐面目标合法来源；specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090、specs/008-recipe-driven-inspection T050/T051/T060 |
| 总图124—130“整体取放、不单独下发内部坐标”；分拣闭环取→放 | §3.1.6③—⑧及§2.4 | 业务先取后放一致，但整体接口提交粒度存在真实差异，不只是省略ACK；U26第5项直接明确两阶段 | 008§5取料1→状态2→清命令→真实源槽+目标→放料2→状态3→清命令/ACK1→状态0→ACK0→提交；specs/003-plc-latest-protocol T071、specs/008-recipe-driven-inspection T057/T059 |
| 总图123—130后131下料；整体图先分拣后下料 | §3.1.6①—⑨ | 真实业务顺序冲突，U26第6项明确裁决，不能凭日期判断 | 008§2先检测→000B下料定位→适用Sorting，OK保存无搬运依据；specs/003-plc-latest-protocol T069、specs/008-recipe-driven-inspection T054/T062 |
| 总图132—137松盘/人工取盘/确认/最终输出 | §3.1.6⑨及003既有确认合同 | 原图省略读回与保存细节，U26第6项及003 Clarifications确认物理解锁与页面确认分别成立 | 008§2，WholeTray/来源提交→Cmd0→Status0→ObservedUnlocked提交→页面确认→Final提交；specs/003-plc-latest-protocol T068/T069、specs/006-frontend-station01-console T049、specs/007-station01-integrated-loop T033 |
| 总图67—73 E；76—121特殊进站/旋转/三出口 | 普通§3.1.5/6未定义特殊旋转完整接口 | 不从F或普通取放推定E/旋转信号；本轮无需裁决其未知字段 | 008§6保留范围，B01-E/B03及适用B02/B04/B07局部限制；specs/008-recipe-driven-inspection T061/T065/T067/T068，不阻塞普通无E Q03 |

本轮发现的主链实质差异均有上述明确确认依据，未发现需要重新裁决的Q03来源冲突，因此未重复触发speckit-clarify。剩余现场/特殊业务输入继续按原B表，未宣称已解决；若后续实际输入与确认记录冲突，再仅对所涉动作启动clarify。

## 实际代码消费核对

| 调用/消费链 | 当前事实及最小后续调整 | 任务 |
| --- | --- | --- |
| ThreeDStep/FScanStep→FixedMoveStep/MotionCoordinator→LatestProtocolPlcDevice | 已有公共真实采集/worker/保存及握手；保留复用，核对F3/4、公共3D1/2与对应轴及本次清零，失败不能因finally机械清理放行 | specs/001-station01-public-preparation T090；specs/003-plc-latest-protocol T067/T070 |
| RecipeRunPlanner→RecipeExecutionCoordinator→IntegratedDetectionPort | 仍有Rescan及Flip受限/面轮耦合，按同循环实现Flip后合法逐面续接，不重建AB/CD、worker或存储 | specs/002-plc-xyz-recipes T11；specs/008-recipe-driven-inspection T052/T053/T060 |
| LatestProtocolStageActionAdapter→VirtualPlc；ThreeStageWorkflowExecutor→WholeTrayWorkflowOrchestrator | 当前取料即完成/旧下料Z和Sorting先行仍待改；协议按图细化，同时保留真实保存和来源矩阵门禁 | specs/003-plc-latest-protocol T069/T071；specs/008-recipe-driven-inspection T057/T059 |
| 结果/媒体/来源投影→API→frontend/src/runtime.js→既有页面确认 | 实际构建入口及页面取盘控件复用，查询必须反映已提交同run事实，不能靠旧媒体轮号伪造第二次3D | specs/003-plc-latest-protocol T068；specs/006-frontend-station01-console T048/T049；specs/007-station01-integrated-loop T033；specs/008-recipe-driven-inspection T054/T062 |

读取具体消费者的相关方法和调用处并与旧实施清单对照，未声称全仓逐行审计。文件路径和更完整下批改动见[既有实施清单](implementation-checklist-20260926.md)；本轮没有任何源码/配置/fixture/生成器/运行脚本修改。

## 本轮文件与上一轮遗漏纠正

上一轮file-impact将001/sequences.md列为“无直接增量”，该判断遗漏了仍适用的公共XYZ、F复位和移交描述；本轮已修正该软件设计。旧影响清单和静态报告保留当时原文，其对001时序完整性的判断应结合本记录重新理解。

实际修改15份既有文档，差异详见[逐文件diff](sequence-document-diff-20260926.md)：

- `specs/001-station01-public-preparation/contracts/device.md`
- `specs/001-station01-public-preparation/plan.md`
- `specs/001-station01-public-preparation/sequences.md`
- `specs/001-station01-public-preparation/spec.md`
- `specs/001-station01-public-preparation/tasks.md`
- `specs/002-plc-xyz-recipes/contracts/recipe-execution.md`
- `specs/003-plc-latest-protocol/spec.md`
- `specs/003-plc-latest-protocol/tasks.md`
- `specs/008-recipe-driven-inspection/contracts/evidence.md`
- `specs/008-recipe-driven-inspection/contracts/execution.md`
- `specs/008-recipe-driven-inspection/plan.md`
- `specs/008-recipe-driven-inspection/quickstart.md`
- `specs/008-recipe-driven-inspection/research.md`
- `specs/008-recipe-driven-inspection/spec.md`
- `specs/008-recipe-driven-inspection/tasks.md`

新增：`sequences.md`、本核查记录及`sequence-document-diff-20260926.md`。需求MD、Word、XLSX本轮无需改动：现有§11已有步骤映射/原图及差异，本次详细派生链接放在功能设计和本记录，避免重复改写派生需求。Word整个文件未改，内嵌原图因此也未替换。

## 渲染、文档与保护检查

- 在本机临时目录使用Mermaid CLI 12.0.0将001的6图、008的4图共10段全部渲染为SVG，退出0；非仅围栏计数。渲染器安装于npm缓存，不改项目依赖或Spec Kit。源PNG/SVG/Word内图不作为渲染输出目标。
- 顺序语义逐条核对：初始3D/F、A批/B批与独立融合、逐实体Flip、ACK清零、面2独立目标、无重采3D、000B下料、无搬运依据、适用分拣、保存后解锁及实际页面确认；PC写和PLC反馈方向明确。Flip/分拣/下料没有新增Inspection或未定义抓取Z复位握手。
- 6份既有tasks共274个编号/勾选序列与修改前一致；仅8条未完成任务正文补引用和步骤核验，不新增/删除/勾选任务。008仍22项T049—T070；18FR+6SC原追溯保持，US1—US6覆盖不缩减。
- 新增/修改当前Markdown链接及任务引用将在最终只读analyze再次核对；未来evidence路径仍是实施交付位置。原始20260921附件缺失已明确，不伪称读取。
- 修改前建立231文件摘要基线（specs、治理、资料、需求及外部原型），最终只存在上述15份授权Markdown变化，其余216文件摘要一致，无缺失。覆盖全部原图11组PNG/SVG、协议原件/备份、需求Word及其内嵌图、原型ZIP、既有specs历史证据/四份上轮记录。运行包保持只读，本轮没有启动/写入，也不把此231文件基线夸大为运行包全量哈希验证。
- 修改前副本、摘要和10张渲染产物在本机临时目录`C:\Users\codexsandboxonline.10_3_0_13\AppData\Local\Temp\gaode-sequences-20260926`；可审查的文档diff已保存到功能目录。以下关键来源前后SHA256一致：

| 受保护来源 | 前后相同SHA256 |
| --- | --- |
| `高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx` | `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519` |
| `高德_文档/上下位机对接/上下位机职责边界图.png` | `521265476AA37BC66A17F58C870BC40B2CC5B1A8CDC46D0F1B5168D5EB7A7EA0` |
| `高德_文档/上下位机对接/上下位机职责边界图.svg` | `8C7D79FA1F5D82888F0C74D27E38AC0EFD2A7746B3CEE7FCE055A806C9A8D603` |
| `高德_文档/上下位机对接/分拣动作单独闭环图.png` | `1269637C0C95614077E3C76355F922D2013EC55ABCB3718810D1066C570D141C` |
| `高德_文档/上下位机对接/分拣动作单独闭环图.svg` | `942ED189C0A48A8F61A7E945B7A88AB731928C98BC541EE4A0294EBB3E9BAB96` |
| `高德_文档/上下位机对接/单零件检测闭环图.png` | `FF08A5E73BD2BCA8C39CF9A4516672C7D4BAC1FB409491C91CA06094B2797FEE` |
| `高德_文档/上下位机对接/单零件检测闭环图.svg` | `DB676B1F1FB467E50F7B04EB461BCC926D0AE4EEB417E454CE1BB49C51C00B8E` |
| `高德_文档/上下位机对接/安全与实体按钮处理流程图.png` | `3C5244ADF81D2FE0CFC42E2BA1B4F8416A26ECA24553F7E69F76FF9B17C05EE2` |
| `高德_文档/上下位机对接/安全与实体按钮处理流程图.svg` | `C7A36922C187FADDF70EDD4FFC6F4C85190787723887A147D09C0E943895501F` |
| `高德_文档/上下位机对接/总时序图.png` | `AB1BAE26E5F46749FAFA1B77D2FD3D1842BAC82054D6F53719ABF8FD03FB1A95` |
| `高德_文档/上下位机对接/总时序图.svg` | `9D72F80F209BAF12B40AEBF6FE8C38373D236330E05C7F0E93A77B2587458612` |
| `高德_文档/上下位机对接/整体流程图.png` | `0FC109BEBC35BC173F7BD63A8C8EB4DC07D7C4D893E1940FB2DF434236A61689` |
| `高德_文档/上下位机对接/整体流程图.svg` | `BE6A642636A72A34BBC6E28309CA8F8EA73F341625A5455601BE6FDF821FB5D5` |
| `高德_文档/上下位机对接/特殊零件单个检测闭环图.png` | `869FFA27FE5D5B85103D8FD550C67574DF2332DC224DDF205A1DBB27FA5DD364` |
| `高德_文档/上下位机对接/特殊零件单个检测闭环图.svg` | `DECB79AA3679B711E16A89C3495272353D8648F951F6CC6D952350DDFE1FEE3E` |
| `高德_文档/上下位机对接/特殊零件单个闭环流程.png` | `262DA9957F9804D355ED95BE16B95425C340F2B0934968FC4D00DEF28DEB5832` |
| `高德_文档/上下位机对接/特殊零件单个闭环流程.svg` | `26F2478284B6AEE16192B315AA507875EFC70FC83539752C3FA5D670EB66F844` |
| `高德_文档/上下位机对接/特殊零件类型判断图.png` | `DAA29AFB83E4BEADE99E8F07BF4F8276934ECC3C6279848AF68F32AF75A68C08` |
| `高德_文档/上下位机对接/特殊零件类型判断图.svg` | `B68687C38E3C83CEBA4AB65D41E61D32E06C86BF0C8D796AF8BD408D7D79F06A` |
| `高德_文档/上下位机对接/相机与轴组关系图.png` | `4DB22930316214267AC1FA7CB6CE4E927FB9DA7B90BD3A901E82B0AE55B14A2D` |
| `高德_文档/上下位机对接/相机与轴组关系图.svg` | `E981E3E3A8D54B3B0F99D0CFA72F3F9F30EA4F01A2825D025FC84FE6680F5250` |
| `高德_文档/上下位机对接/采集动作逻辑图.png` | `793E7E96A90BB012DA109013FB4306F06F818762F02784F91FC1A811191F783F` |
| `高德_文档/上下位机对接/采集动作逻辑图.svg` | `3294BD7B8B3265F834D091CF3B0C06196DFDF7716422AFD7469C72B94D5F7E2A` |
| `高德_文档/通信协议历史备份/20260925_分区版启用/PLC与上位机通信接口协议_最新版.docx` | `CF5ADCD1EFC5AF86A4AF254317C7D0A8697212BDDFB66D061D4AB5C421B1ADDF` |
| `高德_文档/通信协议历史备份/20260925_分区版启用/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx` | `E9F976D31558D96ACF9A749B96DFA9A5EECEF3850120F35B2670067C59ACC897` |
| `软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.docx` | `126DF5362E3B7FCE7C64F1922BE5BF1BC4FBF6103BD38E3C171F69C4790C7A43` |
| `E:\dzk\gaode\原型.zip` | `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0` |

## 技能步骤与实施前置

先读AGENTS/宪章及plan/tasks/analyze技能，检查setup-plan/setup-tasks/common副作用；feature.json已指既有008，setup-plan跳过模板复制，setup-tasks只返回模板未重生任务。未升级工具、初始化Git或创建新功能。hooks为空，前后钩子均无动作。plan Phase0只读研究来源/实际消费者，Phase1增量设计保持现有模型/技术；tasks只作正文最小对齐，最后analyze单独只读，不写文件。

设计来源与步骤前置满足普通Q03的实施拆分；**运行前置尚未满足**：正式Flip、无重扫计划/目录、各面合法目标和初始测量映射、下料/阶段顺序、身份/预算/保存/页面/夹具仍须按既有任务交付并验证。不能因为Mermaid可渲染就将Q03标Passed。

## 下一条implement指令

> 使用现有speckit-implement，在E:/dzk/gaode-1读取AGENTS、008 spec/plan/tasks、sequences.md、sequence-alignment-20260926.md和implementation-checklist-20260926.md，并读取001 sequences。按明确来源先交付共享协议/自动Flip及ACK/000B下料与新普通盘末顺序，再打通合法逐面目标、初始测量、预算和正式页面同run链；优先完成普通OK Q03两面AB到Final，翻后不重采3D。保留原型、来源和历史证据，复用已有核心能力；普通OK不等待全部分拣/E/旋转/组策略，最终范围不缩减。按任务做必要主流程与直接失败验证，在Q03检查点报告真实证据，随后按授权推进其余适用两/四面；缺失设备或业务输入只限制依赖分支，未完成不勾选。

本轮到文档对齐及最终只读复核停止，未执行上述implement指令。
