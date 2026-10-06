# 前端配方与运行结果合同 s01-recipe-api/3.0（目标，未实现）

011实施增量：当前人工取盘门为Host实际提交ManualRemovalAllowed；查询输出readyForRemovalSourceMatrix/readyForRemovalSourceMatrixId/manualRemovalAllowedEventId，通知eventType=ManualRemovalAllowed，仍用s01/notification/2.0信封及同run GET。旧ObservedUnlocked/ReadyForUnlock及旧字段只对应历史原记录，不产生当前动作许可或物理解锁成功声明。Final仍须实际人工确认和原子保存，许可/Final失败不释放运行租约；Final当前提交后才释放。012仅在现有界面绑定新字段/事件，不重建业务判断。

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](../business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

原制定日期2026-09-26；2026-10-03按宪章8.0.0和统一澄清定向修订。复用正式Host授权、启动、查询、媒体和通知；011定义共同业务及执行，012负责目录/读写API和006页面绑定。
本节字段是后续实施目标，当前API尚不具备完整能力。旧007 CAP/P01请求及后台bind仅保留历史范围，不作为008页面入口。

## A01 配方目录与选用

沿用GET /api/v1/recipes/catalog及Read授权，保留已有目录摘要与items能力。目录展示料盘匹配码、配方身份、内容版本、PLC产品型号、用途、可用性和限制；这些含义及共同业务定义唯一引用[recipe-contract/1.3](../../011-plc-interaction-update/contracts/recipe-contract.md)，本页不另建配方模型或工艺校验。原version/catalogDigest等字段的迁移和选择意图比较按共同合同执行，历史引用如实保留。

012负责目录提供者、完整读取及新建/编辑保存API，具体HTTP签名由012设计；完整读取必须返回可编辑的实际保存内容，不能仅返回目录摘要。写入调用011唯一业务校验，检查不同配方不得占用同一料盘匹配码，并以真实持久化成功为保存成功依据。失败不返回成功；保存成功后后续F绑定使用新内容，已冻结运行继续使用原内容。既有生产准入保留，不增加发布审批或复杂版本管理平台。

012在已有授权的006配方控件及012编辑弹窗绑定目录、内容和真实读写结果；不改变只读原型归档或扩大其他页面。页面选择是意图，后端不接受客户端自报匹配、设备应用、动作结果或保存成功。availability依据必要配置和已批准能力，不将算法当前未就绪作为全部动作的通用拒绝理由，也不绕过实际动作依赖。

## A02 正式启动与F一致性

沿用POST /api/v1/station01/runs、Run.Start授权及现有requestId/contextJson/publicConfigRef/budgetRef/simulationRef。原station01-start-run-context/2.0的expectedRecipeRef保存选择意图与来源，不锁死尚未F绑定的旧目录版本；当前引用形状和比较规则以011共同合同为准，不在API或前端复制匹配逻辑。

公共准备使用适用公共配置；首次3D真实提供有无/姿态观察与F扫码XY，不能在F绑定前用产品配方解释公共采集。F解码内容就是料盘编号，取得绑定时已成功保存且经共同校验的唯一配方内容。料盘编号、配方身份、内容版本及PLC产品型号含义分开；不按型号/测试编号/图像路径猜配方。绑定成功后冻结实际内容与执行输入；后续保存不得修改已冻结运行。页面过期内容引用不能强制F使用旧内容。

未匹配、重复匹配或必要输入缺失保存实际原因，仅阻止依赖配方的产品动作；匹配前必要公共准备和F扫码仍按正式流程执行。新F绑定使用当前有效业务提交回执，不要求旧PLC配方ACK或DeviceApplied；实际机械动作仍需有效设备回执，必要真实提交、关联、取消及原期限门禁继续有效，不因目录匹配成功直接显示Bound/Ready。

无效请求在建运行前返回具体4xx并声明未创建；已受理后F不匹配以该run受限状态查询，不伪装成请求未受理。202仍仅表示受理。008/012页面不调用POST /api/v1/recipes/bind替代自然F绑定，不从脚本预造handoff，不用兼容默认值绕过当前必需输入；旧请求和历史证据保留原适用范围。

## A03 正式查询投影

GET /api/v1/station01/runs/{runId}保留现有行为，新增recipeSelection与recipeExecution：
- recipeSelection：实际保存的本次选择意图及受理身份，不能当作已绑定配方。
- recipeExecution：实际绑定的配方身份、内容版本/摘要与运行快照引用、planRevision、scenario/route及真实stage/step/对象/检测面/独立扫码姿态/执行关联。具体字段唯一见[station01-execution/1.0](../../011-plc-interaction-update/contracts/execution-and-state.md)；无已提交事实不从页面或目录当前版本补齐。
- 流程state、检测completeness、业务质量disposition、独立姿态状态、物理dispositionState分别表达；quality仅为事实质量标签（如Derived），详见RES增量。异常物理槽号来自实际首次3D或放回后统一3D检查，保留物理编号、检查关联及先前结果；未知观察不是空异常集合。
- restriction：reasonCode、受限动作、已知原因、可执行核查；未知不说成明确不安全。
- allowedActions[]：后端依据当前阶段、可靠事实和权限给出允许动作；当前不生成ConfirmManualFlip。waitingManualFlip仅作为历史JSON字段保留，不能单独授权当前动作。有效人工取盘须真实已提交许可和匹配请求，页面不自行推导安全放行。
- results[]：kind=Face/Member/Group/Assembly或Single，id/parentId、required/completedTargetRefs、业务disposition/完整性、结果引用与来源；RES增量补项目明细及空值语义。
- movements[]：entityId、实际physicalSlotIndex、source/targetPointRef、状态、operationId、已提交语义反馈；缺事实明确未知，不用0,0。检测、翻面取放和分拣点各按用途配置；同盘位置相同不合并目标。OK分拣留原槽，NG/Pending分别搬到对应区配置点；姿态异常退出后续检测和翻面，保留已有检测事实，最后从原槽实际Pending并关联3D依据。
- 完成链：检测/必要翻面放回与统一3D复查/适用独立E完成后进入分拣，全部适用分拣完成后才下料；沿用WholeTray/ManualRemovalAllowed/Removal/Final的真实提交引用，不能把前三者任一提前显示为Final。OK不分拣搬运不取消检测期必要翻面、放回与扫码。

媒体沿现有GET /runs/{runId}/media及/media/{mediaId}。对象/阶段/面/真实测量来源/相机身份须来自已提交采集事实，不按文件名或格位推测。
页面仅按006在已有位置绑定必要操作状态、对象/面身份及最终结果，原型ZIP只读。完整细节可以在证据包/正式查询中核验，但不能把后台查询代替必要页面操作及最终结果展示。

## A04 通知、权限及人工操作

沿现有NotificationEnvelope事件通知revision并触发GET重取，不因消息顺序推断动作完成。
401/403明确授权拒绝；202后短暂未可查不代表终态。006负责对齐当前页面状态及原型允许的提示，不新增非必要权限平台。
原始异常存受控日志，公开诊断脱敏。

保留现有人工取盘、正常暂停/继续和取消的权限、expectedRevision、有限等待及必要保存保护；012只绑定已有授权控件和后端实际allowedActions，不因本地推断启用动作。
历史人工换面批准条款和原面来源只在旧记录的适用范围读取，不转换成当前确认或续接许可。当前recipe-contract/1.3无人工翻面待确认生产者，孤立manual-flip接口及业务服务删除；当前自动翻转完成取件、翻转、放回后统一3D复查且不重新F绑定。人工区安全、取消/期限与保存门保持；不新增现场恢复或替代协议。
历史故障双端复位、restartFrom和旧任务决定见[恢复合同](../../003-plc-latest-protocol/contracts/recovery-test-execution.md)，仅保留原适用范围；011新恢复、安全控制按DEP-03/04局部延期，不自动继承旧协议动作。核对、受理、人工记录和重连均不授权未知动作重放，不制造完成事实。
对008适用且需要用户确认的步骤，必须有上述正式页面实际交互。007外部Test客户端自动取盘仍只说明其原范围，不能填成008前端人工确认。
VirtualPlc可以模拟已确认的物理反馈；模拟反馈的来源及页面操作要分别记录。

## A05 跨功能实施边界

011持有共同模型、唯一业务校验、F绑定、快照、通信、正式执行和状态合同；012持有目录、完整读写API与006页面映射，007既有环境和采证按受影响范围复用。RecipeEndpoints.cs与Program.cs在后续实现由012唯一编辑，011提供绑定/执行及装配要求。
本轮这些设计同步后，仍须通过tasks更新共享变更的生产者、消费者及必要验证，再改代码。
012已获配方编辑保存授权；本合同不另设示教、页面或通用流程编程平台。更多检测面仍只选择AB/CD，四面仍3CD＋1AB且AB位置由配方表达，更多面不套固定组合。额外E是四检测面后按配置需要执行的独立扫码姿态，不计作检测面或PLC原始编码，使用已确认扫码Z与动作约束。

## 自动多面媒体增量（第八批）
首次3D和翻转放回后统一姿态复查的媒体均来自各自真实capture/call及用途关联；后者不重新F绑定，也不作为检测Z的测高来源。检测媒体携对象/物理槽/检测面/相机/配置版本，独立E媒体携实际扫码姿态关联，不从文件名、列表顺序或“第5”猜PLC码。



2026-09-26 PARAM采集事实增量：实际相机调用完成后必要保存CaptureFact，包含captureId/mediaId、对象/面/步骤、CaptureProfile及RequestedCaptureSettings（曝光、增益、ROI、光源通道/亮度、稳定时间）；媒体查询仅从同run已提交事实投影requestedCaptureSettings。Test文件相机不宣称真实SDK应用或标定。

## RES结果展示增量（2026-09-26，目标，尚未实现）

依据HMI-003、DAT-004、008 FR-016及006 FR-010；本节细化A03，不改变质量规则或另开结果接口。查询仍为GET /api/v1/station01/runs/{runId}，由003提供，006只消费；保存归008。新增结果投影标识resultSchemaVersion=`station01-result-display/1.0`。以下是设计字段，不声称当前API已具备。

### 唯一语义与提交边界

- results[].disposition是后端质量结论OK/NG/Pending或null；quality是既有事实质量标签（如Derived），source是Simulated等来源，三者不得混用。算法technicalState=Success不推出OK。
- state表示流程执行；results[].completeness表示检测完整性；movements及对象dispositionState表示物理处置；既有完成链的持久Final表示整盘完成。五者独立；NG/Pending可以随合法处置到Final，Final不能覆盖质量结论。
- 已提交单图、面或项目结果立即可查询，不等整个Detection Completed或Final。对象/组汇总只有对应已提交决定才有disposition；前端不得从子项重算。普通Single也须投影已提交的面及输入关系，不只Group/Assembly提供明细。
- 未产生结果=NotProduced；已有输入但尚未取得提交回执=NotCommitted；因历史字段/关联不足=Unavailable；这些都不是Pending。算法异常独立technicalState/reason，只有后端按既有业务规则提交Pending时才显示Pending。未确认提交及保存失败不能成为已提交质量事实。
- completeness取Complete/Incomplete/Unknown：比较冻结必检targetRefs与已提交完成引用，只在后端可证明全齐时Complete；禁止沿用当前模型默认Complete作为证明。质量NG与缺失/Pending面同时保留。

### 字段、事实来源和关联

未注明可空的字段在对应条目存在时必需。所有引用属于响应runId和同planRevision；缺关联则availability=Unavailable，不按文件名、列表位置、相机字母猜对象。results保持原kind/id/parentId层次，补Part以匹配当前整体部位投影。不得把Group汇总作为某Member的判定，Assembly汇总不得替代Part结果。

| 字段/类型 | 持久来源、可空条件和关联 | 用途 |
| --- | --- | --- |
| runId UUID；resultSchemaVersion string；resultRevision string | 当前持久run；resultRevision为本次已提交结果/采集/处置投影的版本标识，不作时间或数值大小比较 | 同run及刷新版本 |
| resultContext {kind,id,localFace?,stepSequence?}或null | 后端从已提交的当前执行步骤映射对象；无活动检测步骤时保留最后已提交检测步骤对应对象；并列按冻结步骤序号确定，未开始/无法关联为null | 默认当前对象；不是从最后一条results推断 |
| results[].kind/id string；parentId string?；localFace int?；planRevision string | 冻结计划与已提交结果；kind=Single/Member/Group/Assembly/Part/Face；顶层无parent，非面可无localFace，Face沿parent关联具体对象 | 类型、对象、所属组/整体及面 |
| disposition enum?；availability enum；reasonCodes string[] | 已提交对象/面/项目决定及有限失败事实；availability=Committed/NotProduced/NotCommitted/Unavailable；null质量不冒充Pending，reasonCodes只取实际错误/未判定原因 | 质量与无结论原因 |
| completeness enum；requiredTargetRefs/completedTargetRefs string[]? | 冻结计划/实际完成事件；无法恢复集合时null且Unknown，不填空集合证明Complete | 完整性 |
| dispositionState string? | 已提交搬运/无需搬运依据及movements关联；无事实为null，不用质量disposition或Final替代 | 物理处置 |
| resultReference string?；committedEventId UUID?；committedRevision number?；saveState string | 已提交事件/WriteId核验及持久序号；NotProduced可无引用；未知提交无committed字段，saveState用既有保存状态词汇，查询不执行补写 | 保存与审计 |
| source string?；quality string? | 对应事实来源/质量标签；缺失如实null，不能依据Host运行模式补造 | Test/Simulated/Derived等独立说明 |
| inspections[]，每项inspectionId string、stepSequence int?、localFace int?、businessCamera string?、captureId/callId UUID?、mediaIds UUID[] | 同run已提交CaptureFact/AlgorithmFact及冻结步骤关联；inspectionId用已有callId或结果引用稳定标识；融合另有callId及inputCallIds/mediaIds，不混成第二个缺陷项目 | 单图/融合输入与面/步骤追溯 |
| inspections[].itemId/itemName/rule string?；technicalState string?；disposition enum?；reasonCodes string[] | 已有冻结算法能力/项目配置与实际算法结果；仅有图像级判定时只显示实际能力身份及“项目未提供”，不能把一张图包装成命名缺陷项目 | 检测项/规则/技术失败 |
| inspections[].measuredValue string或number?；unit string? | 算法已提供且保存的测量值及单位；无值为null，不能使用目标参数充当实测 | 实测栏 |
| inspections[].parameters[] {name:string,value:string或number或已保存结构值,unit?:string,kind:enum,reference:string} | 实际保存的RequestedCaptureSettings或冻结算法参数及版本；kind=RequestedCapture或AlgorithmConfig；只有确有设备应用事实才可标Applied，Test文件相机请求不冒充设备实效 | 设定参数 |
| inspections[].defects[]? {type?:string,position?:string,coordinateSystem?:string,size?:string或number,unit?:string,disposition?:enum,mediaId?:UUID,reference:string}；confidence number?；confidenceUnit string? | 仅算法实际提供且已保存的事实；position须附坐标系/单位及对应媒体，未知则不画标注。当前worker未提供缺陷/置信度，返回null及NotProvided；空数组仅用于明确提供了空缺陷集合 | 缺陷类型/位置/尺寸、置信度 |
| inspections[].detailAvailability {parameters,defects,confidence,measurement} | 每项Provided/NotProvided/NotApplicable/Unavailable，由事实是否提供、是否适用及是否可查询决定；不以null猜原因 | 区分未提供、不适用及查询缺失 |
| results[].confidence number?；confidenceUnit string?；inspectedAtUtc datetime?；inspectionDurationMs number?；defectCount int? | 对象范围真实检测事实；对象置信度仅在已有对象级算法/汇总事实明确提供时可用，不能平均单图置信度；保存时间不冒充检测时间，单次call耗时不冒充整对象耗时；无对象级计数事实为null，前端不按NG面数计缺陷数 | 原型已有置信度/时间/用时/缺陷数 |

inspections的质量、技术状态、保存/来源/结果引用沿上述同名规则；对象参数/缺陷不能从其他对象或前一轮补齐。现有算法只提供图像判定时保留这种粒度，不新增算法输出义务；未来已有输出可通过同一保存/投影链展示，本轮不设计通用字段平台。原图/标注图只使用已有run媒体清单Ready的mediaId及授权媒体API；没有标注图时明确未提供，不生成假标注。

### 查询、选择和更新

003从StageEvents、WriteBatch提交事件、AlgorithmCalls、媒体和冻结计划构造同一持久读取边界；不依赖内存执行器重新运行业务。历史run若不在coordinator内存但持久存在，允许只读恢复查询投影，旧字段缺失保留Unavailable，不回填历史。新增字段的ETag必须覆盖resultRevision及当前投影变化，不能仅沿用未随算法/面提交变化的运行revision导致304漏更新。

006默认采用resultContext；已有媒体/对象切换交互提供明确同run对象身份时，可切换展示焦点，并在现有基础信息/检测项标明对象、面和来源。媒体切换不改对象质量为单图质量；只切换面时对象判定保持。Group汇总只在已有结果列表的组层展示；“当前零件”聚焦Member、Single或明确Assembly/Part身份，不把组当零件。无明确对象保持空态，不取results[0]或数组最后项。切换run清除旧焦点，页面重开重新查询后按后端上下文选取；不能靠旧本地结果恢复成功。

通知仍只触发GET，定时刷新复用。按run与页面查询代次隔离回包，单个焦点的刷新串行/合并触发，避免旧请求覆盖新选择；已有revision拒绝回退，resultRevision仅判相等/变化。跨run通知不自动切换显示；显式启动收到新receipt或既有历史查看动作才切run。API缺字段/查询失败显示结果不可用而非OK/Pending，不保留未标明来源的旧质量。已确认的正常暂停、人工换面及USR-D新轮隔离不变。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF/已退出SQLite确认：质量与Final正确，处置字段为null且movements为空；真实取放/特殊出口事实已有。缺陷见 `.specify/bugs/008-disposition-projection/assessment.md`，已有任务覆盖，不另追加重复编号。

沿既有GET运行字段接入同run、同冻结planRevision的已提交处置事实，禁止以质量或Final推测。普通Reserved（可靠预留提交）、InTransit（可靠取料且在途真实提交）、Completed（可靠放料及通信内部闭环完成且占用提交）、UnknownHeld分别表达；NoMoveRequired只来自明确已提交无需搬运依据。分层结果中物理实体Single/Member/Assembly绑定对应状态；Group/Part/Face没有独立搬运事实时保持null，不复制成员/整体状态冒充其实际搬运。

movements继续使用既有entityId/physicalSlotIndex/sourcePointRef/targetPointRef/state/operationId/committedEventId；引用真实配置点与实际反馈事件，按用途区分的实际轴位置沿既有evidence查询，不新增直控设备入口。当前NG/Pending处置依据本盘对应区域配置点及实际完成事实；旧特殊出口仅作历史事实读取，不能替代新三区域处置或拼造生产映射。没有这些事实的历史条目保持null/空，不补写旧库、不建兼容层。resultRevision/ETag须反映已提交处置变化，页面刷新由既有API与通知触发。

现有普通批次预留事件记录已核验且无需搬运的ordinaryOk实体集合（混合盘正常件也明确留原位）；无普通动作的既有NoAdditionalSortingRequired继续作为依据。仅增加已有必要提交的载荷，复用当前读写次数和绝对期限；本段原投影修复不增加运动；当前011处置已按同盘OK原槽、NG/Pending对应区域和异常跳过后续检测、最后原槽Pending分拣更新，不沿用旧特殊出口作为新流程。先文档后代码，正式运行期间不覆盖冻结源/程序/配置。必要投影验证覆盖仅预留/仅在途不完成、可靠反馈后完成、实体/版本隔离、无需搬运、NG/Pending实际处置与姿态异常退出；联合代表中的Pending页面证明字段来自实际后端保存，新旧构建范围分列。
本节细化A03与RES既有dispositionState/movements可空语义，字段结构不新增。

## 009 / AL06 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

当前处置公开状态Reserved/InTransit/Completed/UnknownHeld/NoMoveRequired含义不变。InTransit要求当前可靠取料及真实业务提交，Completed要求可靠放料及必要内部协议闭环后占用提交；API/业务测试不判断原始2/3或ACK0。实际提交与当前有效回执分离，迟到实存不能恢复旧动作资格。status/run/evidence及通知逐字段版本/null/历史映射按009影响矩阵§5，本合同业务结果station01-result-display/1.0不变；旧raw仅Infrastructure有限历史reader，原字节不改，不补造。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。真实保存、关联、取消、期限、未知占用、来源真实性及生产局部限制保持；反馈按011已确认新协议承接。012授权配方编辑保存由其独立规格承接，本合同不扩大无关页面、真实SDK或历史数据库升级范围。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。
