> 2026-09-27 当前实施与验收状态见evidence/completion-review.md及evidence/task-audit-night-20260927.md最新节；下方带日期的“待实施/尚未修改/NotRun”是当时记录。复位观察同步和真实权限拒绝绑定已实现并验证，原失败保留。Test选定主流程验收完成，008整体未完成，T055/T070原条件未全齐。原要求、共享接口及生产局部限制不因本入口改变。

2026-09-26人工Test增量按[003人工合同](../../003-plc-latest-protocol/contracts/manual-test-execution.md)，冻结manualWaitMs，不以自动Flip反馈冒充人工。

# 执行与PLC边界合同 s01-recipe-execution/3.0（目标，未实现）

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](../business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

现行条款于2026-10-03按011统一澄清及REQ §7/11同步；下列带日期的实施事实仅保留历史范围。
本合同定义业务要求与后续接口增量，不分配未知寄存器；B依赖未解除的动作不能直接实施/验收。

## E01 冻结与动作归属

配方真实保存且共同校验成功后供后续F绑定，已冻结运行保持原内容。F内容为料盘编号；如有人工选择则核对一致，公共准备不依赖未绑定配方。
请求关联run/tray、recipe/version/digest、planRevision、stepId/operationId/attempt/epoch/deadline、对象及适用组/整体/面/高度轮次、真实源目标/轴/用途/来源。
每步意图提交后才准入派发；可靠反馈另行提交。写成功、计划展开、日志输出均不是动作完成。
普通定位/采集/换面及特殊逐件闭环均由共同应用执行承接；特殊每件必须完成公共取放分拣/安全位再后件，普通盘末处置归共同Sorting，全部适用处置后才整盘下料。全部步骤必须有唯一消费或有依据的条件不适用记录。
008正式2.0选配方请求须携严格执行标记；旧007 Test上下文仍属原样本范围，其占位位置和局部流程不得抵扣008准入或完整验收。严格请求没有已批准产品位置，或没有按[虚拟 Test 映射增量合同](test-virtual-mapping.md)读取冻结配方/点位配置的完整 Test 位置时，在采集/分拣前拒绝；算法有限Pending也不能由占位坐标构造物理处置。
继续使用现有线性计划及端口。是否引擎是设计选择，合同不要求插件注册框架或禁止未来合理抽象。

## E02 定位、采集、分析与复位

F XY来自首次3D定位；A/B/C/D检测XY及检测Z取冻结配方/点位配置，保留单位、坐标基准、对象/槽/面、版本、来源和限值校验。E使用扫码Z，取放点按用途配置。X/Y/检测Z/扫码Z/抓取Z独立反馈，不能用目标回显或旧单一到位表示实测。

运动→本次可靠到位与实际坐标核验→真实光源/采集/算法→必要保存→有效动作结束后方可继续。内部触发/清零由通信层实现；旧Inspection_Status及XY_Move_Cmd原码不再由业务编排或业务测试消费。新原件未定义的地址/类型不能猜。

普通A整批后B整批、C整批后D整批；单图分析与同面融合各自实际调用，有限等待和保存门保留，不能等未采第二输入而阻塞第一输入完成。翻转放回后统一姿态复查、F不重绑；检测Z不依赖旧HeightResult或测高偏置。

## E03 独立算法输入与来源

2026-09-26收口实施：逐图/同面融合15秒预算到期后，晚到结果不采为OK；仅在既有2秒释放宽限内收到实际全部输入释放、必要超时/Pending事实提交且机械状态仍可靠时，完成本图Z复位与清零，继续该面其余必检输入并汇总Pending。融合不能把超时输入恢复为OK；确认NG仍优先。未知释放/机械/保存仍阻断，不重放已完成Detection运动。本规则修复旧晚到worker集成场景，不放宽算法、I/O、心跳或冻结阶段期限。

沿用现有worker进程、调用身份和媒体租约。目标进程协议显式支持单输入分析与双输入同面融合；双输入条目分别携inputKey、byteLength、mediaId、摘要、对象/面/轮次/相机身份。
目标协议由station01-worker/1.0升为station01-worker/2.0，与PythonWorkerAdapter/WorkerProtocolCodec/虚拟worker同步；一个输入释放不能代表两个均释放。worker实际读取两个合法媒体并返回调用/输入关联；Host不能用现成逐图OK代替融合调用。
双输入请求的每个媒体分别携`InputIdentities`中的对象、面、轮次及A/B或C/D相机身份；`TargetIdentity`单值不能把两张图都写成同一个相机。当前`FaceResultAggregator`只在同对象/StageId/面/轮次及同AB/CD对齐后生成双输入；产品定位与复位未接入前，正式Detection保持受限，不能靠组件算法调用宣称流程完成。
虚拟算法是Test软件替身，保留真实进程收发、媒体读取、规定计算时长和模拟来源；不声称真实缺陷精度。可按受控输入/种子组织合法OK/NG及失败样本，不能在Host硬编码成功或直接预写业务结果。
E扫码对象按场景表及本次U06确认接入，E失败留问题后继续合法后续。检测算法无有效结论为有限Pending；公共3D观察/F失败按其依赖阻断，不拿Pending强行进入产品路线。

## E04 扫码、换面与处置

F内容为料盘编号，唯一匹配已保存配方；不同配方不能占同一码。保存通过共同校验且真实成功后，后续F绑定新内容，冻结运行维持原内容；未匹配不进入依赖配方动作，必要公共准备及F仍按正式顺序执行。编号、配方身份、PLC型号分开。

四面检测仅3CD＋1AB，AB位置由配方确定；支持更多面序，仅AB/CD，不推导固定组合。四检测面后可配置独立额外E扫码姿态，是否需要/参数由配方表达，使用扫码Z及已确认动作约束；不按产品名、测试号硬编码，姿态序号不是原始PLC码。E码问题保存并关联内部身份，机械/保存失败仍阻断依赖动作。

逐实体按用途取件点定位→型号/目标面翻转→放回点定位→放回；本轮相关对象放回后统一3D姿态复查。异常留原槽退出后续检测、翻面与分拣，最终返回异常物理槽号；正常对象继续配置面，F不重绑，历史结果保留。不强加已关闭的独立实际面号或旧Flip_OK ACK。

同盘OK/NG/Pending三区，初始料在OK区。普通分拣OK原槽不搬；特殊OK使用分拣抓手从旋转工位实际放回本件原始OK槽、NG/Pending各去配置目标；姿态异常独立退出。分拣料盘与上料同位置但检测/翻面取放/分拣点位不合并。OK原槽不省略检测期间必要翻面/放回/扫码。成组按问题成员、整体按实体搬运；特殊当前件立即公共分拣，不以旧SpecialExit/免分拣标记跳过；完成后不再盘末重复搬运。

取放依次源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升，当前取料可靠反馈及在途保存必须先于放料。对象/物理槽号/目标区域点/动作/结果关联，Sequence不是槽号。完成反馈及保存成立才记完成；未知占用不释放、不自动重发。新原码只归通信映射，业务不得沿用旧状态2取料/3放料或Sorting_OK握手。

## E05 保存、下料、恢复及错误出口

相机硬件完成未知、已派发运动/取放反馈未知保持物理占用和UnknownHeld/受限；算法失败的Pending不能掩盖它们。必要保存Failed/CommitUnknown阻断依赖后继和最终完成。
仅已证明无物理副作用、合同允许的算法/派发前操作可有限重试；共用原deadline，不重放整个Detection。
普通盘末：Detection完整→适用Sorting→整盘下料定位→WholeTrayCompletion→已提交ManualRemovalAllowed→页面取盘确认→Final（ObservedUnlocked仅历史，不授当前动作）。下料沿最新协议和通信合同核本次到位/实际位置，不在业务合同规定旧地址/命令；保留前一动作必要复位及安全门禁，不再使用检测Z/只动XY约定。整盘收敛须全部必检和适用分拣已提交、无未处理在途件及必要保存；下料到位不能授权取盘。特殊旋转出口按独立合同。
下料、整盘完成、解锁观察、取盘确认和Final分别保存。前端确认不能替代硬件反馈，也不能在未知状态先结束。
本期故障恢复按[003双端复位合同](../../003-plc-latest-protocol/contracts/recovery-test-execution.md)关闭旧轮、保留故障/媒体/结果，实际初始成立后显式新run从公共3D/F及绑定开始完整流程。正常pause及人工换面同轮继续独立；新旧run/预算/反馈/保存关联隔离。示教与完整机械恢复矩阵延期。

## E06 预算与资源

2026-09-26夜间实现计数定位：3.2-test增量分列普通应处置实体的两段取放、一次ACK、取放XYZ/命令/槽号/ACK写包、受理前状态读取和既有1/2/4秒重试；每实体意图/开始/在途/占用四份提交及三次预留/在途状态读取。批次预留读取/提交在下料前计入下料预算，下料另计原意图/开始/完成三份提交、一次机械和现有明确I/O。这些数量来自实际执行代码；端口每段使用原XyCompletion/PlcAcceptance上限，状态读取/提交各不超过原CriticalSave，原预算值不改，旧deadline不追改。特殊已在检测阶段完成出口的实体不额外分配普通取放，零普通搬运仍留NoAdditionalSortingRequired提交预算。

使用配置中实际动作超时、采集时长、worker时长、保存/控制开销及有限重试计算并冻结动作/阶段/整盘绝对deadline；重连/尝试不能重置。
正常检测图数C=各必检目标相机数之和，同面融合数F=应融合面目标数，检测worker调用A=C+F；初始公共3D/F和适用E另计；翻后姿态复查次数与实际换面轮次一致，计入实际工作量。
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

协议身份目标为 `plc-upper-20260925-partitioned-ack`，并携原件SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`。这是待实施身份，不声称Host当前已提供。历史命名约定为“XY_Move_Cmd/XY_Pos_Confirmed名称与地址不变”；名称已由2026-09-27用户确认覆盖为XY_Move_Cmd/XY_Pos_Confirmed，数值地址不变；预留从0056开始；字节序/地址解释仍需真机校准。

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

当前公开名称为 `4x0001 = XY_Move_Cmd`、`4x0002 = XY_Pos_Confirmed`，依据monitor-xyz-history本轮最新用户确认及来源Word，取代此前XYZ命名确认。两个点位均为Int16，方向分别PC→PLC及PLC→PC；地址、PDU偏移、类型、命令值、握手与清零语义均不变。内部XyMoveCmd等数值常量不要求改名。
来源Word仍保持原内容及SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`，不宣称原件已更新。带日期的旧名称、旧实现快照及旧运行结论仅适用于原构建；当前规则优先，历史证据不得机械替换。
下料命令4及Grab_Target_Z、本次实际XYZ核验保留；普通OK盘末留原位、不发取放；仅适用NG/Pending同盘取放，特殊配方必要搬运及回原位不取消。协议§3.1.5/6/7的清零顺序不变。

## 009 / AL06 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

E06增量：三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

原RecipeExecutionBudget公式、机械/ACK上限及工艺顺序不变；同盘源槽不等于序号、组成员/整体共享动作、E规则、不重扫和3+1继续有效。业务断言以语义证据/真实提交检验，线缆值仅通信断言承接。


### 009 检测保存窗口接线补充（2026-10-01）

本功能产品检测执行按[003检测合同具体接线](../../003-plc-latest-protocol/contracts/detection-port.md)传递本轮冻结 CriticalSaveBudgetMs 与原 Detection 截止；必要保存共受两者约束，不由固定两秒或协议步骤推导。算法意图与派发共享真实会话/时钟/起止时间。实现及验证归009 T035/T048/T049，先前任务勾选保持；本节不声明运行通过。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`保留在历史payload/证据原文，不产生同名新事实或授动作。当前使用相关Flip/PutBack及真实3D姿态观察，不再要求已删除的手工/ACK清零报文。对应旧孤立确认接口与无消费分支实际删除，有效关联、安全、保存、取消与期限由现行组件承接；历史失败不改写。


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

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A03/A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。
- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

前轮specify仅确认需求同步；本次014 Phase 1及012配套设计见当前设计引用，不生成新tasks；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。

原始料盘、OK区域、实际物理格位及实体身份在取料前明确并冻结，后续旋转/采集/判定/回放沿用同一关联；区域号只用于展示/检测顺序，不能代替原始槽身份。特殊OK原槽回放使用分拣抓手，不切成上料抓手或NoMoveRequired；实际取料及必要保存、转运、放料和安全位确认后才推进下一件，失败不记录完成。

操作者不得为OK选择其他目标槽，不增加“是否回原槽/OK处理方式”开关；原型任意OK目标配置含义退出。若已有明确必要的原槽放料参数，归该原槽取放配置；同槽不推导全部取放坐标、高度/抓手补偿相同，不自动复制全部取料值、不编造新参数。

历史读取保持原记录事实，不把旧任意OK目标/旧完成标记重解释成已按本次原槽规则执行。当前显示与执行必须区分普通无需搬运事实和特殊实际原槽回放事实；缺实际保存/动作证据时不补造完成。具体共同字段、序列化/版本、历史编辑限制和接口签名在后续设计实际对齐，不能宣称仅文意同步即结构交付。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。
