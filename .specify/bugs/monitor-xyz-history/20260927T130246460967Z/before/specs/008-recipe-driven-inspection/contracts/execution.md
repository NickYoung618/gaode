> 2026-09-27 当前实施与验收状态见evidence/completion-review.md及evidence/task-audit-night-20260927.md最新节；下方带日期的“待实施/尚未修改/NotRun”是当时记录。复位观察同步和真实权限拒绝绑定已实现并验证，原失败保留。Test选定主流程验收完成，008整体未完成，T055/T070原条件未全齐。原要求、共享接口及生产局部限制不因本入口改变。

2026-09-26人工Test增量按[003人工合同](../../003-plc-latest-protocol/contracts/manual-test-execution.md)，冻结manualWaitMs，不以自动Flip反馈冒充人工。

# 执行与PLC边界合同 s01-recipe-execution/3.0（目标，未实现）

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](../business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

日期2026-09-26；依据最新008 spec、宪章6.0.0、REQ §7/11及003已确认下料约定。
本合同定义业务要求与后续接口增量，不分配未知寄存器；B依赖未解除的动作不能直接实施/验收。

## E01 冻结与动作归属

前端选择引用先保存，公共准备实际F结果唯一匹配且与选择一致后冻结产品计划。公共3D/F仍使用独立公共配置。
请求关联run/tray、recipe/version/digest、planRevision、stepId/operationId/attempt/epoch/deadline、对象及适用组/整体/面/高度轮次、真实源目标/轴/用途/来源。
每步意图提交后才准入派发；可靠反馈另行提交。写成功、计划展开、日志输出均不是动作完成。
普通定位/采集/换面/逐面目标续接及特殊进站/旋转/出口归Detection内应用执行入口；普通盘末处置归外围Sorting；UnloadTray归外围下料。全部步骤必须有唯一消费或有依据的条件不适用记录。
008正式2.0选配方请求须携严格执行标记；旧007 Test上下文仍属原样本范围，其占位位置和局部流程不得抵扣008准入或完整验收。严格请求没有已批准产品位置，或没有按[虚拟 Test 映射增量合同](test-virtual-mapping.md)从本轮3D结果解析的完整 Test 位置时，在采集/分拣前拒绝；算法有限Pending也不能由占位坐标构造物理处置。
继续使用现有线性计划及端口。是否引擎是设计选择，合同不要求插件注册框架或禁止未来合理抽象。

## E02 定位、采集、分析与复位

已定义检测1/2时序：Inspection_Status=0 → 写本轮目标/XYZ_Move_Cmd → 匹配本轮XY到位、适用Z到位及实际XYZ → Inspection_Status=1并清本方移动命令 → 光源/采集→本次单图分析有限终态与必要保存 → Inspection_Status=2 → 本轮Z_Reset_Status=2 → 清Inspection_Status=0 → 下一运动。
检测位置的 `XYZ_Move_Cmd` 值以指定协议§2.2寄存器表为准，为 **2**；值1是上料位。§3.1.7“例如1去上料位”只示例时序，不能定义产品检测命令。
保留已确认清零责任；Host不得写PLC反馈。目标/轴/单位/高度基准不明确时受限，不能因存在一个Z反馈就推定全部检测/扫码/抓取轴等价。
普通A整批后B整批、C整批后D整批。首批单图分析保存后即可复位，不能等尚未采集的第二输入。第二输入齐备后另发同面融合，融合结果保存后才能完成该面质量汇总。
产品XY为配置值，产品Z只能使用绑定正确对象/面/轮次且单位/基准明确的高度或合法固定值。翻后不重采3D；保留初始测量，每面须有明确适用映射/合法固定Z；测量身份不等于执行阶段或设备连接代次。不得复用上一面XYZ或伪造第二轮高度。

## E03 独立算法输入与来源

2026-09-26收口实施：逐图/同面融合15秒预算到期后，晚到结果不采为OK；仅在既有2秒释放宽限内收到实际全部输入释放、必要超时/Pending事实提交且机械状态仍可靠时，完成本图Z复位与清零，继续该面其余必检输入并汇总Pending。融合不能把超时输入恢复为OK；确认NG仍优先。未知释放/机械/保存仍阻断，不重放已完成Detection运动。本规则修复旧晚到worker集成场景，不放宽算法、I/O、心跳或冻结阶段期限。

沿用现有worker进程、调用身份和媒体租约。目标进程协议显式支持单输入分析与双输入同面融合；双输入条目分别携inputKey、byteLength、mediaId、摘要、对象/面/轮次/相机身份。
目标协议由station01-worker/1.0升为station01-worker/2.0，与PythonWorkerAdapter/WorkerProtocolCodec/虚拟worker同步；一个输入释放不能代表两个均释放。worker实际读取两个合法媒体并返回调用/输入关联；Host不能用现成逐图OK代替融合调用。
双输入请求的每个媒体分别携`InputIdentities`中的对象、面、轮次及A/B或C/D相机身份；`TargetIdentity`单值不能把两张图都写成同一个相机。当前`FaceResultAggregator`只在同对象/面/轮次及同AB/CD对齐后生成双输入；产品定位与复位未接入前，正式Detection保持受限，不能靠组件算法调用宣称流程完成。
虚拟算法是Test软件替身，保留真实进程收发、媒体读取、规定计算时长和模拟来源；不声称真实缺陷精度。可按受控输入/种子组织合法OK/NG及失败样本，不能在Host硬编码成功或直接预写业务结果。
E扫码对象按场景表及本次U06确认接入，E失败留问题后继续合法后续。检测算法无有效结论为有限Pending；公共高度/F失败按其依赖阻断，不拿Pending强行进入产品路线。

## E04 扫码、换面与处置

本轮E实现设计采用003[E Test合同](../../003-plc-latest-protocol/contracts/e-test-execution.md)：适用成员/部位0.5目标相机E、独立采集/EDecode、码质量问题与机械/保存门禁分开，预算单列。不存在新增生产PLC信号。

F已按用户授权写入[指定协议§3.1.7](../../../高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx)：Inspection_Status=0，PC先写公共配置Camera_Target_X/Y及Scan_Target_Z，再发XYZ_Move_Cmd=5；PLC清旧反馈并定位。整个F周期（含命令清0与复位）Machine_Current_Pos_Z、Z_Axis_Move_Status、Z_Reset_Status对应扫码Z轴，直到下一有效运动命令受理才按新命令切换。
本轮XY=1、扫码Z=2且实际坐标/安全成立后，PC写3，再清XYZ_Move_Cmd=0，随后采集解码；采集确认结束且结果或有限失败已保存后写4。PLC本轮只执行一次扫码Z复位，反馈1→2，失败3并锁停；PC收到可靠本轮复位2才清Inspection_Status=0。
4只表示扫码操作结束，不表示F识别成功。只有F唯一有效、配方一致且必要保存成功才放行；失败即使复位也不进入产品路线。采集/保存/反馈未知不提前写4或清0。Host不写反馈位，旧2不得抵新周期。F现代码已有3/4与对应Z复位，复用并核验新协议来源；旧通过不等于新版完整链通过。
E由USR-20260926-C明确采用上述F式命令5、扫码Z归属、Inspection_Status=3/4及本轮复位清零；这不是协议原文直接定义E。扫码面暂取下发坐标，需扫码对象按工作簿K19/Q19与型号交叉；码失败或无结果记录问题并保留内部身份继续。机械未知、复位失败和必要保存失败仍阻断依赖动作；F唯一绑定门禁不变。

自动换面须本次Flip_Status及实际面号可靠；人工占用期间不派发冲突运动，人工确认、设备安全及占用清零成立后，采用命令目标面并记录命令默认来源才继续。S2只翻仍需下一面的实体，S3整体姿态只改一次，已完成部位不重采。

当前自动翻面合同（§3.1.5）：当前零件翻面XYZ写0003/0005/000B→XYZ_Move_Cmd=3→核验本次XY到位和实际XYZ→清移动命令→提交Flip_Target_Face→核验Flip_Status=2且实际面匹配→Flip_OK(4x0055, PC→PLC Int16)=1→确认Flip_Status=0→Flip_OK=0。PLC抓手完成内部闭环，料盘不翻面。不再等待两组专用翻面取放地址或动作序号寄存器。Host关联operationId/实体/目标面/连接代次及发令后观测；连续不同实体同目标面必须分别完整动作及清零。未实现与数值缺失分别受限。

阶段全部应翻实体完成后，在同一Detection循环续接下一面；不插入RescanWholeTray。初始公共3D、F绑定、同盘/对象和历史结果保留。每面独立配置point/version/XY及Z合法依据；采用初始测量时明确该面适用的sample/scope/unit/datum/offset/range来源，采用固定Z时明确批准配置。面/执行阶段推进不增加测量轮次，设备重连代次另计。旧PostFlipStageContext与二次3D组件证据仅属旧实现，不能作为正式翻面事实或新版准入。

人工已定义部分（§3.1.5⑧）：占用1禁止运动；人工完成且满足既有安全条件后PC置Manual_Flip_Complete=1，PLC反馈占用0后PC清确认。本次确认以命令目标面作为人工完成后的流程面，人工确认继续时采用并记录来源；故障新轮不能沿用旧采用面，不能冒充Flip_Current_Face实测；自动Flip仍核验实际面。故障完整新轮规则见E05，不扩展页面。

同盘分拣（§3.1.6）：先写源XYZ至0003/0005/000B→Sorting_Cmd=1→状态2仅取料成功→清命令→提交真实源槽号Sorting_Part_Index及同盘目标XYZ→Sorting_Cmd=2→状态3放料完成→清命令、Sorting_OK(4x0054, PC→PLC Int16)=1→确认状态0→Sorting_OK=0→提交分拣完成/目标占用。状态4失败，5满盘；命令3满盘报警。普通OK无需搬运时无取放动作，NG/Pending仍是必做范围；源点、NG穴位、Pending穴位均属本盘。

请求使用实体、真实物理索引、sourcePoint、targetRegion/cell/point、用途、预留和结果引用；软件Sequence不作PLC物理槽号。

2026-09-26夜间T057实施细化：当前固定Test处置点以已配置targetSlotId/point.id标识目标格，容量按既有布局一格一个物理实体检查。所有应处置对象在下料前共同冻结预留；目标冲突、目标仍被本盘原实体占用或既有未决预留时不派发。预留事件复用StageEvents的IntentRecorded，payload记录kind=SortingAssignmentsReserved、run/plan、entity/sourceSlot/targetCell/point、reservationRef=该分拣operationId、resultRef。可靠取料状态2及本次取料目标采样成立后，003设备适配器调用已接入持久通道提交kind=SortingAssignmentInTransit，确认提交后才写放料槽号/XYZ/命令2。放料状态3与ACK清零后，既有Completed事件保存kind=SortingAssignmentOccupied及相同关联。未知保留原预留/在途；保存失败不续接。此增量不新增PLC字段、布局范围、表或第二套状态库；当前尚待实施验证。

对应E06/T051：冻结分拣预算须计实际取料、放料两段的受理/机械等待、ACK和必要预留/在途/完成保存。配置内各步骤上界不变；修正遗漏计数时采用新公式版本、新run冻结，不追改活动或历史deadline。普通OK实际无需搬运的依据仍保存，预算推导不得授权缺配置或超容量动作。
类型1角度/方向及内部机械动作由下位机负责；上位机发旋转请求并等待对应结果。当前VirtualPlc实际接收请求返回模拟结果；003须先明确Test命令/结果合同，不臆造生产地址，不用Host固定返回成功。进出站/占用/返回原槽仍记录适用实际反馈。普通OK无需搬运时留原位；特殊OK实际回原槽，NG/Pending去各自已确认目标。
S2只搬问题成员，NG送本盘NG区，OK留原位；有NG即汇总NG，NG面和未判定面分别保存，纯Pending沿原待判定路径。生产容量延期，复用合法Test布局，不从组NG推导整组搬运。特殊出口已处置实体不能再次盘末分拣。

## E05 保存、下料、恢复及错误出口

相机硬件完成未知、已派发运动/取放反馈未知保持物理占用和UnknownHeld/受限；算法失败的Pending不能掩盖它们。必要保存Failed/CommitUnknown阻断依赖后继和最终完成。
仅已证明无物理副作用、合同允许的算法/派发前操作可有限重试；共用原deadline，不重放整个Detection。
普通盘末按§3.1.6：Detection完成→UnloadPreparation下料定位→适用Sorting→WholeTrayCompletion→ObservedUnlocked→页面取盘确认→Final。下料写0003/0005/000B后命令4，核验本次到位和实际XYZ后清命令；保留前一动作必要复位及安全门禁，不再使用检测Z/只动XY约定。整盘收敛须全部必检和适用分拣已提交、无未处理在途件及必要保存；下料到位不能授权取盘。特殊旋转出口按独立合同。
下料、整盘完成、解锁观察、取盘确认和Final分别保存。前端确认不能替代硬件反馈，也不能在未知状态先结束。
本期故障恢复按[003双端复位合同](../../003-plc-latest-protocol/contracts/recovery-test-execution.md)关闭旧轮、保留故障/媒体/结果，实际初始成立后显式新run从公共3D/F及绑定开始完整流程。正常pause及人工换面同轮继续独立；新旧run/预算/反馈/保存关联隔离。示教与完整机械恢复矩阵延期。

## E06 预算与资源

2026-09-26夜间实现计数定位：3.2-test增量分列普通应处置实体的两段取放、一次ACK、取放XYZ/命令/槽号/ACK写包、受理前状态读取和既有1/2/4秒重试；每实体意图/开始/在途/占用四份提交及三次预留/在途状态读取。批次预留读取/提交在下料前计入下料预算，下料另计原意图/开始/完成三份提交、一次机械和现有明确I/O。这些数量来自实际执行代码；端口每段使用原XyCompletion/PlcAcceptance上限，状态读取/提交各不超过原CriticalSave，原预算值不改，旧deadline不追改。特殊已在检测阶段完成出口的实体不额外分配普通取放，零普通搬运仍留NoAdditionalSortingRequired提交预算。

使用配置中实际动作超时、采集时长、worker时长、保存/控制开销及有限重试计算并冻结动作/阶段/整盘绝对deadline；重连/尝试不能重置。
正常检测图数C=各必检目标相机数之和，同面融合数F=应融合面目标数，检测worker调用A=C+F；初始公共3D/F和适用E另计；本路线翻后重扫次数为0。
对1个单件，按采集上界5秒、每worker 10秒，不含运动/保存/重试时：
- 单面：C=2、F=1、A=3，检测约40秒；
- 两面：C=4、F=2、A=6，检测约80秒，另加实际翻面定位/机械执行/ACK预算；
- 四面：C=8、F=4、A=12，检测约160秒，另加三次适用阶段的逐实体翻面定位/机械执行/ACK预算。
这些是预算推导基数，不是实际耗时或最终超时配置；公共准备约30秒及实际动作上界还须另加。不得把120秒保留为四面通用预算或缩减必检来凑数。
人工等待使用已确认的有限阶段预算，未提供时不默认为无限或自动通过。心跳仍保持既定3秒保护及独立处理，不随整盘预算放宽。
当前`RecipeExecutionBudget`按冻结步骤、版本化`BusinessBudget`及E06的Test采集/worker上界计算绝对Detection/Unload/Sorting期限，并在配方绑定前保存来源与期限。第四批加入Test复位上界和worker释放宽限；现场批准的产品复位耗时、适用光源参数及全部重试上界仍未齐备，不能用预算计算结果反推动作可派发或整盘验收已满足。

## 第四批单面组件接线约定（2026-09-25）

严格Detection请求使用同一冻结计划和公共运动配置，逐个Position步骤附显式目标记录：step序号、pointRef、对象/物理槽/协议槽号、面/高度轮次/相机、XYZ、单位/坐标系、来源及高度依据。组件测试可注入`Test/InjectedXYZ`，只在独立Test运行中验证真实端口调用；此标记不批准Q01/Q02目录，也不能出现在正式配方准入中。公共移交解析冻结位置负载中的显式目标与来源；若没有对应点位或Z依据，返回具体缺失原因，绝不从3D首样本、默认值或旧Review坐标推导。

一次Position/Capture由检测执行入口唯一驱动：先持久提交运动意图，调用现有MotionCoordinator准入及Detection命令2，匹配本轮反馈/实际XYZ并提交动作事实后写Inspection_Status=1；媒体和单图worker及必要事实提交后写2，待本轮Z复位2再清0。每图复位完成才可定位下一目标。某面第二输入复位后才调用双输入融合并提交面事实，然后继续后续定位；第一输入不等待尚未采集的第二输入。任何必要保存失败、反馈未知或复位失败均不得继续后继定位或返回Completed。
先顺序执行并释放已结束调用的租约；待配对媒体可落盘，缺输入融合不占worker；双输入所需容量在配置检查中明确。

## 阻塞登记

| ID | 来源与待确认输入 | 受限动作/最迟确认 | 可独立推进/解除依据 |
| --- | --- | --- | --- |
| B01-F | 本轮授权及指定协议§3.1.7：F 3/4、命令5、Scan_Target_Z/反馈轴及失败清零已定义 | 代码/VirtualPlc接入及S1前验证 | 不再等待F外部定义；Test数值有来源，协议定义不等于设备已实现 |
| B01-E | 本次U06已确认E复用F式§3.1.7；需扫码对象、面默认与失败继续已明确 | 对应实现/共享合同及最少验证 | 业务规则无需再问；独立Test流程继续，未实现/未运行不计通过 |
| B02 | OPEN-06/24、D05：普通翻面/同盘分拣字段及ACK均已定义 | 代码待实现、实体点位待配置；特殊进出站另确认 | 普通OK多面不等待分拣全部完成；不新增专用翻面地址 |
| B03 | 本次U03确认角度/机械过程归下位机；当前Test命令/结果接口由003设计，真机映射延期 | 对应实现/共享合同及最少验证 | 业务规则无需再问；独立Test流程继续，未实现/未运行不计通过 |
| B04 | OPEN-08/26/27、D01/D03：各轴含义、坐标/高度单位基准、占用/对象映射 | 依赖轴/高度/槽位的动作；S1使用前取得对应部分 | 已明确含义的数值可标Test配置；不能模拟臆定轴、占用或高度含义 |
| B05 | 本次U01/U07确认仅问题成员处置；生产逐面映射、布局及容量现场确认 | 对应实现/共享合同及最少验证 | 业务规则无需再问；独立Test流程继续，未实现/未运行不计通过 |
| B06 | 本次U02/U06确认NG优先留明细、E缺码记问题继续 | 对应实现/共享合同及最少验证 | 业务规则无需再问；独立Test流程继续，未实现/未运行不计通过 |
| B07 | USR-D确认人工命令默认面和故障双端复位完整新轮 | 初始检查及新轮消费者按003恢复合同实施验证 | 业务无需再问；真机未知夹具/初始化仅局部受限，Test可继续，未运行不计通过 |
| B08 | 生产容量与布局现场确认；当前OK主链复用有效Test点位和配置，不新增容量范围 | 对应实现/共享合同及最少验证 | 业务规则无需再问；独立Test流程继续，未实现/未运行不计通过 |
| B09-H | 旧Q01/Q02/PARAM已有虚拟页面通过，当前新协议无E2E | 新版页面采证与负载变化时核对心跳 | 保持3秒，不将旧Blocked或旧Passed冒充当前结论 |
| B09-U | 既有正式页面/原型只读，配方/阶段/媒体/取盘绑定需同步新接口 | 新版页面实现及验收 | 006只绑定已有控件，不新建页面/改ZIP；人工缺口只限相应路线 |

B01-F/E及B09-H/U是spec B01/B09的细分，不新增业务范围。未回复的现场输入保留受阻；完成设计不表示它们已解决。

协议身份目标为 `plc-upper-20260925-partitioned-ack`，并携原件SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`。这是待实施身份，不声称Host当前已提供。历史命名约定为“XY_Move_Cmd/XY_Pos_Confirmed名称与地址不变”；名称已由2026-09-27用户确认覆盖为XYZ_Move_Cmd/XYZ_Pos_Confirmed，数值地址不变；预留从0056开始；字节序/地址解释仍需真机校准。

## 派生时序引用（2026-09-26）

按[软件时序](../sequences.md)核对E02/E04中的当前面批采、逐实体翻面和E05普通结束链；[来源关系](../sequence-alignment-20260926.md)保存原图差异及明确依据。Flip/下料/Sorting不得额外套用检测Inspection或创造抓取Z复位握手；检测及F各自复位不省略。软件保存门是既有必要事实提交，不假定PLC/DB共同事务。

## USR-E直接执行增量

当前宪章7.0.0；四面范围及目录准入按[002合同](../../002-plc-xyz-recipes/contracts/recipe-execution.md#usr-e当前业务准入目标未改配置)，不再以Q总数要求全排列运行。扫码、每个拍照位、翻面、下料、分拣的目标XYZ/所用轴/实际反馈及清零按[003动作增量](../../003-plc-latest-protocol/contracts/plc-stage-action-port.md#usr-e完整坐标与动作观测)。取放目标阶段与抬升后实时Z分开，Host不复制目标作反馈；状态2前不得提交放料数据。002提供冻结目标，003负责两端协议/观测，008消费可靠事实及保存门禁，不复制设备状态机。问题1—5仍待运行核验；本轮无实现结论。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

E06原必要预留/占用提交补ordinaryOk；SpecialExitCompleted补真实源槽/目标引用，不新增设备信号或写入次数。


## 2026-09-27 Test Host I/O运行设置（原FR-013/014，T054/T069/T070）

r16在全部构建/测试结束后，以同r15普通构建/原PLC/default server GC/原配方及全部期限，仅所属Host DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，Q06同rund4fc1789-b50b-487f-953c-9acbb14410eb完整Final及全部适用审计通过。单代表不证明全局根因，继续剩余主流程。

采证脚本增加显式HostSocketInlineCompletions Test开关：queue job JSON字段`hostSocketInlineCompletions: true`，wait worker只传递verify CLI `-HostSocketInlineCompletions`，verify传同名开关至start。start必须已有合法purpose=Test fixture，否则拒绝；只以Start-Process -Environment设置该次自身Host，PLC/父进程/系统环境不改变。默认不开启，record.configuration.hostSocketInlineCompletions记实际模式，实际DLL/GC/期限照旧登记。替换仅匹配临时request的诊断接线，不读取遗留request，不新建平台或扩大业务API/页面。

这是当前虚拟Test运行配置，不宣称生产默认或真实PLC已经验收；原默认模式的失败与根因未完全确诊事实保留。没有改算法、配方、设备协议、采集/保存或任何期限，没有新增重试、假完成或跳过。剩余代表沿明确记录的Test设置验证，若失败仍保留并暂停。

## 2026-09-27 虚拟PLC I/O有限比较（T054/T069/T070）

r17 GROUP-A-E/run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，实际XYZ已匹配。Host20:15:49.799033Z写出tx9478，PLC20:15:54.5024904Z才读头、处理0.0133ms；当时PLC累计GC37.593ms，不据此认定GC根因。正式失败保留，不计通过，后继暂停。

增加默认关闭的PlcSocketInlineCompletions开关及queue字段plcSocketInlineCompletions。沿wait→verify→start传递，必须合法Test fixture；仅所属VirtualPlc Start-Process -Environment设置DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，process.configuration.plcSocketInlineCompletions登记实际启用。Host开关原含义保持，父进程/系统环境不修改，不改变DLL、GC、协议、期限或成功条件。先对同成组路线作一次独立有限比较，真实页面、数据库、设备及算法全链仍必须完成。

采证器对非预期恢复场景应识别现有页面阶段的“阻断”状态并保存StoppedOrUnknown，避免只识别中文fault而等待全预算。恢复注入仍沿既有waitingRecovery条件操作，不把阻断视为Final。

## 2026-09-27 复位观察同步必要修复（原003 T072-A、008 T068/T069/T070）

必要顺序用例在Reset 202后立即Check实际返回RecoveryResetNotObserved，两次均未到链接保存注入，原r19 TRX保留。复位直接Modbus Ready已成立但缓存PlcReady仍旧false；原MotionCoordinator必要门禁不放宽。仅ResetAsync在原轮询/原期限内同时等待缓存实际PlcReady、Connected/SafetyClear，使用同一次observed快照；不改变接口、信号、初始判据或生产机械未知边界。评估见.specify/bugs/008-reset-ready-observation/assessment.md。源码当前尚未修改，待当前测试结束；现有两个保存门禁及正式旧图/完整新轮独立新包复验，已有任务承接不追加重复任务。

## 2026-09-27 当前公开名称与历史适用范围（I1同步）

当前公开名称为 `4x0001 = XYZ_Move_Cmd`、`4x0002 = XYZ_Pos_Confirmed`，依据003 spec的2026-09-27最新用户确认，覆盖旧XY名称。两个点位均为Int16，方向分别PC→PLC及PLC→PC；地址、PDU偏移、类型、命令值、握手与清零语义均不变。内部XyMoveCmd等数值常量不要求改名。
来源Word仍保持原内容及SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`，不宣称原件已更新。带日期的旧名称、旧实现快照及旧运行结论仅适用于原构建；当前规则优先，历史证据不得机械替换。
下料命令4及Grab_Target_Z、本次实际XYZ核验保留；普通OK盘末留原位、不发取放；仅适用NG/Pending同盘取放，特殊配方必要搬运及回原位不取消。协议§3.1.5/6/7的清零顺序不变。
