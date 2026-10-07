# 第一工位主流程 API 合同

011实施增量：当前人工取盘门为Host实际提交ManualRemovalAllowed；查询输出readyForRemovalSourceMatrix/readyForRemovalSourceMatrixId/manualRemovalAllowedEventId，通知eventType=ManualRemovalAllowed，仍用s01/notification/2.0信封及同run GET。旧ObservedUnlocked/ReadyForUnlock及旧字段只对应历史原记录，不产生当前动作许可或物理解锁成功声明。Final仍须实际人工确认和原子保存，许可/Final失败不释放运行租约；Final当前提交后才释放。012仅在现有界面绑定新字段/事件，不重建业务判断。

**合同版本**：`station01-main-flow-api/1.1`；2026-10-03按011/012统一澄清定向同步。共同配方唯一定义为[recipe-contract/1.3](../../011-plc-interaction-update/contracts/recipe-contract.md)，阶段、姿态与异常物理槽号唯一定义为[station01-execution/1.0](../../011-plc-interaction-update/contracts/execution-and-state.md)。均为目标合同，不代表实现/验证完成。

2026-09-24诊断增量：已受理启动后的阻断从 `GET /runs/{runId}` 的可空 `startupDiagnostic` 查询；其脱敏`reasonCodes/safetyAssessment/stopStage/disposition/connectionEpoch/observedAtUtc`及当前`executionOrigin/semanticObservation`按001合同解释；旧source/reliableFeedback字段只保留历史记录。建运行前连接失败无 `runId`，不得假装成受理后首次故障。PLC投影诊断字段见 `status-notifications.md`；原始Modbus异常只在受控持久日志中。

## 启动

`POST /api/v1/station01/runs` 是唯一外部启动入口。请求沿用现有幂等 `requestId` 与配置引用，并在版本化 `StartRunContext` 中提供可校验的 workflow identity/场景/占用对象。服务持久受理后返回 `202`、`commandId`、`runId`；这不表示任何设备动作或流程完成。

同一启动命令驱动Host就绪检查、操作员确认后启动、PLC内部夹紧的可靠语义观察、公共准备、F唯一匹配/计划绑定、handoff和后续整托流程。信号地址、原始码及内部握手仅留通信层；Host不伪造夹紧完成，不要求客户端再次启动或调用`/plan`、`/bind`才能继续。首次3D提供F扫码XY，F内容即料盘编号；共同校验并真实保存成功后的内容用于后续F绑定，已冻结运行不变。未匹配只阻止依赖配方的产品动作，不阻止匹配前必要公共准备与F扫码。

## 查询

`GET /api/v1/station01/runs/{runId}`返回同一运行的公共准备、实际绑定内容与快照、handoff、Detection/Sorting/UnloadPreparation、独立姿态状态及异常物理槽号、Pending/MappingFailed/UnknownHeld、WholeTrayCompletion、ManualRemovalAllowed、人工确认和FinalUnloadCompletion投影，并含完整 component source matrix（或可解析 matrix reference）、matrix digest、sourceKinds、containsNonProductionEvidence、blockedComponents、persistedRevision、限制原因与引用。不得用单一`Source=Real`压缩混合来源。检测期间按实际执行输出检测面、翻面取放、统一3D姿态复查和适用的独立E扫码姿态；不重新F绑定。所有适用分拣完成后才进入下料，OK分拣留原槽，NG/Pending到各自配置区域；姿态异常跳过后续检测，最后从原槽实际分拣到Pending后续检测、翻面和分拣。异常槽号保留物理编号和真实检查关联，不从质量或列表序号推断。

`GET /api/v1/station01/commands/{commandId}` 保持现有幂等命令查询语义。GET 快照是通知丢失、重复或乱序时的事实源。

007 WebView2 Test客户端从`https://appassets.local/`访问Host公开API及`/hubs/station01`时，Host仅在受控Test配置下允许明确配置的页面来源及所需跨源请求/预检；跨源许可不授予业务权限，启动、读取和确认仍逐请求执行原后端认证授权。该Host配置与实际连通由007实现和验证，006只负责页面请求、受控Test凭据传递与展示；本条不改变通知消息字段或生产部署合同。

## 人工移除确认

唯一正式路由：`POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`。旧文档中的`/manual-tray-removal-confirmations`不作为别名或兼容路由。

请求体仅包含`requestId`、`expectedRevision`、`reason`；`runId`来自路径。Host从同run的已提交WholeTrayCompletion、ManualRemovalAllowed和当前快照取得并核验trayId、引用及revision，由服务端记录确认时间和结果。actor identity/role与可信Test/Commissioning渠道来自认证上下文和受控服务端配置，不接受请求体自报身份、来源、完成结果或事件引用。

Host 仅在所有服务端取得的引用属于同一 run/tray、WholeTrayCompletion 已提交、ManualRemovalAllowed 已提交且 revision 匹配时持久受理。前置条件/引用冲突返回 409，存储不可用返回 503；相同 request/payload 幂等返回原结果，不同 payload 冲突。成功提交时ManualTrayRemovalConfirmed、FinalSourceMatrix与FinalUnloadCompletion原子保存；202 本身仍不代替最终查询结果。007自动模拟取盘由启动时启用的受控联调客户端在上述事实提交后调用此路由，ManualActor标记Test/Simulated及客户端渠道版本，不写成AuthenticatedHuman或真实人工移盘。

本接口供测试/联调客户端完成本期软件验收。生产页面控件 Deferred，不得复用启动按钮或隐藏交互代替。

## 旧任务恢复决定

`POST /api/v1/station01/runs/{runId}/recovery-decisions`

请求包含 `requestId`、`expectedRevision`、`originalTaskId`、`decision=ReDetect|Scrap`、`reason` 和 `evidenceReferences[]`；actor identity/role 与 decidedAt 由认证上下文和 Host 生成。无旧任务引用、证据或权限时拒绝；相同 request/payload 幂等，不同 payload 冲突。

该命令只持久化受控决定并授权后续用例，不能直接创建算法、PLC、阶段或完成事实。Host 重启、重连和普通复位不得自动调用此接口或生成等价记录。

## 消费者边界

本合同保持消费方中立：011负责共同业务/校验/执行/通信及真实状态生产；012负责目录/完整读写API、006现有界面与必要消费验证，不另建模型、工艺校验或执行路径。后续实现的RecipeEndpoints.cs与Program.cs由012唯一编辑，011交付绑定和装配要求。本轮不修改前端代码或客户原型；联合最小验证复用保存、重读、F绑定、快照隔离和代表链，不重新要求历史五场景E2E。

## 同运行媒体清单（007共享增量）

`GET /api/v1/station01/runs/{runId}/media` 使用既有`Read`授权，未知runId返回404；只返回该runId已提交Media行及其已提交身份事实，不返回文件路径。响应`{runId,items[]}`；每项为`mediaId,captureId,role,stepSequence,businessCamera,committedRevision,committedAtUtc,readiness,source`。公共准备角色`ThreeD/F`的业务采集身份分别为`ThreeD/F`，步骤为空；Detection角色由同captureId的冻结计划事件给出步骤序号和`A/B/...`业务相机身份。缺少可验证事件时`businessCamera=null`、`stepSequence=null`，不得猜测。`readiness`只在媒体已提交且当前媒体存储可读时为`Ready`，否则为`NotReady`。同一业务相机重复采集时，当前显示候选为本run该相机已就绪记录中`committedRevision`最大者；相同revision不得产生两条不同媒体。

图片内容仍仅由既有`GET /api/v1/station01/media/{mediaId}`按`MediaRead`授权读取；清单的`Read`授权不替代字节读取权限。原型格位映射由006合同拥有，不写入后端业务身份。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前消费宪章8.0.0和2026-10-03统一澄清。历史完成与失败证据保持原适用范围；本次设计更新不代表实现或运行验证完成。

目录/读写消费及既有结果字段沿[008 api-results](../../008-recipe-driven-inspection/contracts/api-results.md)；共同配方、匹配、保存可见性、内容冻结及运行状态增量分别唯一引用011共同配方和执行合同，不在各API复制模型。原context/2.0的expectedRecipeRef是选择意图，不能强制后续F绑定旧内容。GET的recipeSelection/recipeExecution/allowedActions、分层results、真实movements与最终提交引用均依据实际事实。202仍仅受理，不新增设备直控接口。

历史manual-flip-confirmations批准工艺与操作者记录仅按原版本读取；当前recipe-contract/1.3没有此待确认动作生产者，011已删除孤立接口/注册/ConfirmManualFlip许可，不以旧服务或页面提交恢复执行。当前自动翻转使用真实取件、配置目标翻转、放回及统一3D复查；人工区域安全阻断、人工取盘、暂停/继续和取消继续承接有效权限、关联、期限与保存，未配置恢复和安全控制仍局部延期。

当前代码尚未具备全部增量，旧007外部取盘渠道不抵扣008页面操作。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

多面媒体保持实际对象、物理槽、检测面或独立扫码姿态、用途及capture/call关联。首次3D与放回后统一姿态复查均从各自已提交采集事实取得；复查不重新F绑定，不产生检测测高依据。Detection/E从同planRevision的实际采集提交事件取得身份。原heightRound只在历史确有测量事实时如实读取；不以新面序/复查轮次补造旧高度字段。无对应提交不推断身份。



2026-09-26 PARAM采集事实增量：实际相机调用完成后必要保存CaptureFact，包含captureId/mediaId、对象/面/步骤、CaptureProfile及RequestedCaptureSettings（曝光、增益、ROI、光源通道/亮度、稳定时间）；媒体查询仅从同run已提交事实投影requestedCaptureSettings。Test文件相机不宣称真实SDK应用或标定。

## USR-20260926-D恢复API历史适用范围

旧故障复位/初始核对、restartFrom、权限和幂等记录见[recovery-test-execution.md](recovery-test-execution.md)，仅保留原批准范围，不自动成为011新协议恢复规则。正常暂停的同run继续、故障不自动续跑、取消关闭准入、旧WriteId核验及必要保存保护仍有效；核对不制造完成事实。新恢复、安全控制按011 DEP-03/04延期，仅限制依赖部分。路由存在不证明已适配新协议。

## RES已提交结果查询增量（2026-09-26，目标）

查询字段唯一依[008结果契约](../../008-recipe-driven-inspection/contracts/api-results.md#res结果展示增量2026-09-26目标尚未实现)，由003 T068在QueryEndpoints/RunSnapshot实现；仍Read授权、同一GET，不新增结果库或接口。从已提交单图、融合、对象/组事实构建results/inspections/resultContext，而非只等Detection Completed；disposition与quality意义不同。已有内存coordinator无run时先查持久运行，存在则只读重建投影，不恢复执行；历史缺字段可Unavailable而非编造。默认Complete不能当完整性证据。

从同一持久读取边界生成resultRevision及ETag，覆盖结果、原因、媒体关联、保存及处置引用变化，避免旧Observed/PersistedRevision未变化却返回304。保持现有运行revision含义，不引入另一状态机或持久计数平台。缺失关联、读取失败使用既有错误/诊断，公开响应脱敏不带本地路径。006只负责页面映射，008 T054只负责事实保存；两端协议及USR-D恢复入口不被此查询增量改写。


### USR-E既有证据查询投影

既有`GET /runs/{runId}/evidence`在stage记录中增加可空`positionEvidence`，从已提交StageEvent的动作观察投影目标阶段、按X、Y、检测Z、扫码Z、抓取Z用途区分的实际读回、时间、状态、冻结容差和比较结论；取放完成时的实时坐标单列，不能反推目标到位。增加可空`motionEvidence`，仅投影已提交ActionFact的动作/operation身份、角色、按用途区分的目标/实际轴位置、代次和校验时间；不公开本地路径或任意原始payload。不新增接口、库或业务完成依据。缺少旧历史字段保留未知。公开查询归003 T068，原事实保存及设备反馈仍按001/008及003设备任务，既有任务正文已承接动作级诊断。

结果显示状态映射补充：既有run.state枚举传输保持不变，增加只读executionState字符串（由后端该run的State名称投影），006以executionState显示流程；quality/disposition不由它推导。历史run不得拿另一个currentRun的stage替代自身状态。无新增接口。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

以008 contracts/api-results.md为单一字段语义；设备反馈可按committedEventId追查现有evidence。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready核RC05.1的RecipeBindingReceipt及适用真实业务提交，不要求旧DeviceApplied；不能从已有handoff恢复续接资格。

逐字段生产者/消费者/类型/缺失/历史映射：


本节为规范性一次性变更清单，均待实现。源码依据：QueryEndpoints:37–51/81–86/200–230/245–290；Station01ApiContracts:34–103；RunSnapshot:3–84；StagePortContracts:206–208；Station01NotificationService:60–62。旧外层API通常camelCase，PositionEvidence从旧PayloadJson直接clone，嵌套可能PascalCase；下表以旧C#字段名标注这类载荷，不能假称历史已统一大小写。

版本约定：S=`s01-status/2.0`；R=run新增`deviceSchemaVersion="device-semantics/1"`，E=evidence同字段，所有新设备事实payload用同版本且camelCase；N=`s01/notification/2.0`（旧为`s01/notification/1.0`）。原run.resultSchemaVersion及业务run.state枚举传输保持，不能借此次统一所有业务枚举。诊断读取另用`plc-evidence/1`，不把raw挂回S/R/E/N。Unknown是明确语义；null表示该事实不存在/不可取得；空数组只表示确实无条目，不能替代未取得观察。

生产者/消费者及调整责任缩写（每行继承）：**PS**=通信语义观察→Host status→runtime/业务脚本，通信与API负责人(AL03/04)；**PR**=StartupReadiness/RunSnapshot及业务持久化→Host run/查询→runtime/业务脚本，公共流程与API负责人(AL01/04)；**PE**=业务动作保存＋Infrastructure历史读者→Host evidence/CommittedResultProjection→runtime/采证脚本，业务保存与API负责人(AL03/06)；**PN**=Coordinator→通知服务→notification-reducer/runtime→GET对账，通知/前端负责人(AL04)。这些角色必须同时修改生产与消费方，不留下raw兼容字段。

历史约定：**H0**=原业务值/类型/缺失保留；**H1**=新语义只用于新事实，旧已存字段由有限历史读者按当时实际记录提供业务摘要，`recordNature=LegacyRecordedClaim`，不提升为当前物理反馈；**H2**=原始字段留旧payload/受权存储审计，不回传业务API、不写进新raw表；无实际包标`rawAvailability=RawUnavailable`；**H3**=未存全对象/字段保留null及Unavailable，不能按新流程补齐。新事实标`recordNature=Captured`（实际采样事实）或`Derived`（Host汇总），真实ExecutionOrigin另列，不把记录性质当Provider。

### 5.1 status逐字段

| 旧字段：含义/当前类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| schemaVersion:string | 替换为S | S；PS | H0，历史包原版本不改 |
| plc:object? | 语义DeviceObservation?，无采样为null；有采样但陈旧时保留值并标reliability=Stale | S；PS | H1/H3 |
| plc.connected:bool | connection:string，Connected/Disconnected | S；PS | H1，失联非已停 |
| plc.automatic:bool | operatingMode:string，Automatic/NonAutomatic/Unconfirmed | S；PS | H1；缺依据Unconfirmed |
| plc.safetyClear:bool | safetyAssessment:string，Clear/ExplicitUnsafe/Unconfirmed | S；PS | H1，未观察不当Unsafe |
| plc.clamped:bool | clamp:string，Secured/Released/Unconfirmed | S；PS | H1 |
| plc.physicalStart:bool | 删除，无替代按钮字段；启动Accepted另由动作提交事实表达，正式设备现固定false不能称真实按钮 | S；PS | H2 |
| plc.palletLockStatus:int | 原值仅通信诊断；业务clamp按真实观察解释，不保留旧字段 | S；PS | H2 |
| plc.waitingForManualOccupancy:bool | manualHandling:string，Waiting/Confirmed/Unconfirmed；按真实人工流程与安全条件形成，不以旧false直接推Confirmed；manualArea独立业务观察 | S；PS | H1/H3 |
| plc.x / y / z:double（逐坐标） | 当前按011执行合同表达X、Y及检测Z/扫码Z/抓取Z的独立观察，附用途/单位依据/观察身份；缺失为null，不保留无用途的统一actualZ作为新反馈 | S；PS | H0/H3，不以零或目标补值 |
| plc.motionStatus:string | motionAvailability:string，Available/InUse/HeldUnknown；基于当前动作和租约，非原始阶段逐项改名 | S；PS | H1 |
| plc.inspectionStatus:int | 原值仅诊断；acquisitionReadiness:string为Available/Unavailable/Unconfirmed | S；PS | H2 |
| plc.zResetStatus:int | 原值仅诊断；周期释放是业务动作结果，不新增逐阶段reset字段 | S；PS | H2 |
| plc.alarmBits:ushort | 仅诊断，业务alarms及safetyAssessment | S；PS | H2 |
| plc.alarmSeverity:ushort | 仅诊断，alarms[].severity:string Warning/Fault/Critical/Unknown | S；PS | H2，未知不由ordinal推安全 |
| plc.plcSystemFault:bool | 移除线圈镜像；reasonCodes含稳定DeviceFault（有确认依据时）及对应安全评估 | S；PS | H1/H2 |
| plc.connectionEpoch:long | 原名long保留 | S；PS | H0 |
| plc.observedUtc:timestamp | sampleEndedUtc:timestamp；新增sampleStartedUtc/observationId，分别来自真实批次，不承诺原子采样 | S；PS | H0/H3；旧只有结束时间不补起始 |
| plc.diagnosticCode:string? | reasonCodes:string[]仅稳定业务原因（通信不可用/安全受限/证据未确认等）；内部错误文本去诊断 | S；PS | H1/H2 |
| plc.failureOrigin:string? | 内部通道/轮询故障来源仅诊断，不保留同名业务字段 | S；PS | H2 |
| plc.alarms[].bit:int | 删除，位号只在诊断 | S；PS | H2 |
| plc.alarms[].name:string | alarms[].name:string稳定已确认报警名称；severity/reliability为语义字段，组合报警逐项列出；无可靠报警样本时alarms=null | S；PS | H1/H3，不从新位表解旧值 |
| plc.source:string | executionOrigin:{provider,componentVersion?,quality}；provider仅Real/Virtual/Simulated/Unavailable，未记录版本null | S；PS | H1，保留旧来源声明性质，不自动改Real |
| 原无字段 | plc.schemaVersion、reliability、readiness、manualArea、observationId、diagnosticEvidenceReference?；全部依据模型§1/2，引用仅实际提交确认产生 | S；PS | H3；旧无观察ID/引用为null |
| revision:long / eTag:string | 原字段类型及缓存含义保留，摘要必须纳入新语义投影和版本 | S；PS | H0 |
| host:string | 原样保留 | S；PS | H0 |
| camera:{state,source}? / storage:{state,maintenance}? / maintenance:{state,maintenance}? | 原字段及各string类型保留；不可把未知维护变成已就绪 | S；PS | H0 |
| algorithm:{state:string,source:string?,reason:string?} | 原样保留，与设备来源分开 | S；PS | H0 |
| currentRun:object? | 按§5.2 R映射，null不变；禁止旧RunSnapshot整体漏raw | S/R；PS/PR | H0/H3 |
| capabilities:string[] / observedAt:timestamp | 原样保留；后者是响应时间，不取代设备采样时间 | S；PS | H0 |
| mode / stage / recipe / quality:string?；activeRuns:int | 各字段原名/类型/业务含义保留 | S；PS | H0 |

`plcReady/flipStatus/flipCurrentFace/manualZoneOccupied`当前虽在PlcObservation源码中，但不在status投影；不得伪列旧公开字段。目标readiness/manualArea来源于语义解释；自动翻面实际面放相关动作证据，不为对应每个寄存器扩status。

### 5.2 startupDiagnostic、run及历史

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| startupDiagnostic:object? | 保留对象可空；新增schemaVersion/recordNature/rawAvailability/diagnosticEvidenceReference? | R；PR | H3：当前历史重建默认null，旧Audit未保存完整StopStage，不能强行恢复 |
| reasonCodes:string[] | 原名/稳定业务原因保留，不嵌原始值供调用方解析 | R；PR | H0/H3 |
| safetyAssessment:string | 保留原StartupReadiness词汇Other/ExplicitUnsafe/Unconfirmed；设备Clear且因其他原因阻断→Other，ExplicitUnsafe→ExplicitUnsafe，无可靠观察→Unconfirmed（StartupReadiness:27）；不得发明Safe词汇 | R；PR | H0/H3 |
| stopStage:string / disposition:string | 分别原名保留；缺历史完整诊断则对象null，不补默认阶段/BlockedNoDeviceAction | R；PR | H0/H3 |
| connectionEpoch:long? / observedAtUtc:timestamp? | 各原名及可空性保留，与同次观察对应 | R；PR | H0/H3 |
| source:string | 替为executionOrigin对象；旧来源另在recordNature说明，未知provider=Unavailable | R；PR | H1 |
| reliableFeedback:object? | 删除，替semanticObservation:object?；无可靠快照null，不能空对象假定安全 | R；PR | H1/H3 |
| reliableFeedback.connected:bool | semanticObservation.connection:string | R；PR | H1 |
| reliableFeedback.automatic:bool | semanticObservation.operatingMode:string | R；PR | H1 |
| reliableFeedback.safetyClear:bool | semanticObservation.safetyAssessment:string（模型Clear/ExplicitUnsafe/Unconfirmed） | R；PR | H1 |
| reliableFeedback.alarmBits:ushort | 仅诊断，业务只含语义alarms/reasonCodes | R；PR | H2 |
| reliableFeedback.alarmSeverity:ushort | 仅诊断，语义报警严重性同§5.1 | R；PR | H2 |
| reliableFeedback.plcSystemFault:bool | 仅诊断，语义DeviceFault原因，禁止raw影子 | R；PR | H1/H2 |
| run.state:RunState传输值 / executionState:string | 两者保留，仍以executionState作流程展示；不改整个业务状态机 | R；PR | H0 |
| results[].dispositionState:string? | 原名/业务处置含义保留，新事实由已提交语义取放及有效关联形成；不足null | R；PE | H1；旧raw解析仅Infrastructure，结果标记录性质 |
| movements[].entityId:string / physicalSlotIndex:int / sourcePointRef,targetPointRef,state:string / operationId,committedEventId:UUID | 各字段原名/类型保留；状态为已提交处置，不据quality/Final猜已搬运 | R；PE | H0/H1 |
| results[].source:string?、quality:string?；组件来源矩阵 | 业务原字段保留实际来源/质量；新增记录性质不替换组件来源，不能把整盘混合来源压成单一Real或Virtual | R/E；PE | H1，旧声明不“校正” |
| 其余run业务字段（身份/修订、action/capture/algorithm/save/handoff、配置版本、recipe/quality/sorting/wholeTask、计划和提交ID、allowedActions、人工/恢复投影、results检查明细、resultContext、faultRestart） | 原RunSnapshot及各嵌套业务DTO的字段名、类型、null/集合和业务含义原样保留；只递归替换本表明确设备字段。历史allowedActions=[]；结果schema station01-result-display/1.0保持 | R；PR/PE | H0；无授权从历史推进当前动作 |

StartupReadiness顶层词汇必须按当前源码明确对照，不能把设备Clear直接透传覆盖既有页面原因；顶层与semanticObservation同一次观察且含义一致。原字段的缺失不能用新字段默认值回填。历史原值仍存原payload，S/R/E新业务响应只给已存业务摘要和记录性质；原始审计查询不成为业务恢复输入。

### 5.3 run/evidence与保存表示

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| stages[].positionEvidence:JsonElement?（原PositionEvidence数组） | 同名改为显式SemanticPositionEvidence[]?；无证据null。新增schemaVersion/recordNature及ObservationId?、DiagnosticEvidenceReference?，不再透传任意JSON | E；PE | H1/H3；原字节不改 |
| PositionEvidence.Phase:string | 显式映射kind:string：PickTargetObserved→SourcePositionReached；PlaceTargetObserved→TargetPositionReached；PickCompleted→PickObserved；PlaceCompleted→PlaceObserved；UnloadPositionValidated→PositionReached。PickWriteAcknowledged/UnloadWriteAcknowledged/PlaceWriteAcknowledged/SortingAckCleared四种仅诊断；解锁在独立动作结果，不新增位置阶段 | E；PE | H1/H2；不能仅凭旧phase声明新物理成功 |
| AxisRole:string | axisRole:string，已确认业务轴用途，未知Unconfirmed | E；PE | H0/H3 |
| TargetX / TargetY / TargetZ:double（逐坐标） | 当前目标按011执行合同关联具体轴用途、pointRef/version；目标始终为请求依据，不以单一Z代表三个执行用途；历史target.x/y/z仅依真实原记录解释 | E；PE | H0，旧无目标版本null |
| ActualX / ActualY / ActualZ:double?（逐坐标） | 当前实际位置按真实轴用途分别关联；完全无采样为null，不复制target，不以某Z反馈代其他Z；历史actual.x/y/z只按实际原记录读取 | E；PE | H0/H3 |
| Status:ushort? | 删除业务字段；实际raw仅诊断，旧合成值只留原payload | E；PE | H2 |
| Tolerance:double | tolerance:double原业务容差，不因变体变更 | E；PE | H0 |
| ObservedAtUtc:timestamp | observedAtUtc:timestamp真实观察时刻 | E；PE | H0/H3 |
| Matched:bool? | matched:bool?，未知null，须与当前实际/目标关联 | E；PE | H0/H3 |
| stages[].eventId / operationId:UUID；attempt:int；connectionEpoch/sequence:long | 各原名/类型保留；新增actionId仅真实已建立时可空UUID | E；PE | H0/H3，不编造PLC ActionId |
| stages[].stage/eventType/planRevision:string；errorCode:string? | 各原名保留业务含义；UnknownHeld失败不能被Completed覆盖 | E；PE | H0 |
| stages[].source/quality:string | 原名保留，来源按真实事实；新增recordNature区分历史声明 | E；PE | H1 |
| stages[].persistedAtUtc:timestamp；stageStartedAtUtc/stageDeadlineAtUtc:timestamp? | 各原名/类型保留，采样/提交/截止分开 | E；PE | H0/H3 |
| motionEvidence:array?；[].writeId:UUID、committedAtUtc:timestamp | 原字段/提交身份保留；当前成功查询无记录为[]，目标保留[]；历史集合确实不可取得才null/Unavailable | E；PE | H0 |
| motionEvidence[].facts:JsonElement | 改有限语义事实对象；仅下两行、target细表及§5.5已登记业务键和schema/recordNature/引用，拒raw逃生口 | E；PE | H1/H3 |
| facts.kind/pointId/pointVersion/role/zAxis:string?；operationId/actionId:UUID?；attempt:int? | 各业务字段按原含义保留，缺失null；kind中内部协议名称不继续公开，换已保存业务事实用途 | E；PE | H0/H1/H3 |
| facts.actual:坐标对象?（target嵌套详见下表）；tolerance:number?；matched/accepted/completed:bool?；observedAtUtc:timestamp?；deviceEpoch/epoch:long? | 保留真实已存语义及可空性；accepted不等于completed，target不填actual。两epoch旧形状按实际字段读取，目标新事实统一connectionEpoch并保留关联依据 | E；PE | H0/H3；只能从旧真实X/Y/Z投actual |
| runId:UUID / trayId:UUID? / persistedRevision:long / stages:array | 原样保留；新增顶层deviceSchemaVersion | E；PE | H0 |
| wholeTrayCompletionId:UUID? / readyForRemovalSourceMatrix:object? / finalSourceMatrix:object? / finalResult:string | 原样保留含各matrix字段/组件，不由取放或允许取盘单个状态推Final | E；PE | H0/H1 |
| 新字段diagnosticEvidenceReferences:opaque[] | 仅实际持久可核查引用；数组无项不等于物理未执行，另用rawAvailability标缺失 | R/E；PE | H3，旧无raw不给引用 |

新持久设备事实用device-semantics/1；旧Writes.PayloadJson、StageEvents.PayloadJson、Operations.EvidenceJson及摘要不改。旧protocolStatus=2、sortingAckCleared=true等合成声明不迁为CapturedRaw。新旧payload解读边界在Infrastructure，Host业务投影不解析原始2/3或位。

`facts.target`不能整体透传：FixedMoveStep:122保存的是坐标，而IntegratedDetectionPort:767–769保存完整DetectionStepTarget（StagePortContracts:44–55）。目标使用有限`BusinessMotionTarget`形状，以下逐字段规则均属E/PE责任，H0/H3保留原存事实及缺失，不改变配方载荷。

| 旧target字段/类型 | 目标去向/类型/缺失与历史 |
| --- | --- |
| 简单形状X/Y/Z:double | target.point.x/y/z:double?；只能由已有目标填，其他身份null，不反推对象/面 |
| StepSequence:int | target.stepSequence:int?，业务执行序号，缺失null |
| ObjectId:string | target.objectId:string?，缺失null |
| SlotId:string | target.slotId:string?，缺失null |
| ProtocolSlotIndex:int | target.physicalSlotIndex:int?，真实物理槽，非wire宽度；缺失null，不用StepSequence替代 |
| LocalFace:int | target.localFace:int?，缺失null |
| HeightRound:int | 历史target.heightRound:int?仅保留真实旧测量关联；新姿态复查使用实际观察关联，不由面序号或复查次数补造高度 |
| Camera:string | target.camera:string?，已有业务相机角色，缺失null |
| PointRef:string | target.pointRef:string?，已有配置引用，缺失null |
| Point.Id/Version/Unit/Frame:string（四个字段） | target.point.id/version/unit/frame:string?，各自原含义保留，缺失null |
| Point.X/Y/Z:double（逐坐标） | target.point.x/y/z:double?，目标坐标不当实测 |
| Source:string | target.coordinateSource:string?，保留坐标配置来源，**不是设备执行Provider**；缺失null |
| ZBasis:string | 当前检测Z只来自冻结配方/点位配置；历史target.zBasis:string?按原配置/高度依据如实读取，不授权新测高依赖，缺失null |
| IsValid:bool（计算属性） | 删除公开字段；它只是当时配置校验派生，不是动作完成。旧payload字节保留，不从当前模型重算历史真假 |

### 5.4 通知逐字段与消费者

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| eventType:string | 保留StateChanged/DiagnosticChanged/HandoffReady/WholeTrayCompleted/ManualRemovalAllowed/FinalUnloadCompleted的既有含义 | N；PN | 原通知包不改，不作提交证明 |
| schemaVersion:string | s01/notification/1.0→N | N；PN | 历史包保留版本 |
| runId:UUID；revision/persistedRevision:long | 各字段原样保留；修订只作GET对账提示 | N；PN | H0 |
| changedFields:string[] | 仅业务语义路径；旧error提示统一errorCode，新增startupDiagnostic变化提示其路径；禁止raw/内部握手路径 | N；PN | 旧通知路径原样归档，不作为当前API字段 |
| summary:object?（实际整个RunSnapshot；TS当前声明string不符） | 固定对象或null：{executionState:string,wholeTaskState:string,errorCode:string?}，不含完整run或startup原始反馈。前端TS按对象对齐，通知只提示GET | N；PN | 不补造旧摘要、不重放作新运行状态 |
| occurredAt:timestamp | 原名保留通知产生时间，不等于采样/持久时间 | N；PN | H0 |

消费者实际边界：runtime.js:331–345展示启动原因，:345/381来源需由已有数据区域绑定真实值；notification-reducer.ts:1目前类型不符；CJS报警断言与Python取放码断言按V02.1拆分，旧raw断言由通信探针保留。上述绑定只在006既有页面区域/文字含义内调整数据，不改变客户原型结构、控件、静态文案或交互。外部未登记客户端发布前盘点，不能保留raw影子作兼容。

### 5.5 当前软件绑定期限与真实业务提交（011定向设计，2026-10-03）

当前绑定的唯一定义为[recipe-contract/1.3 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。F匹配、冻结和必要业务提交完成绑定，不再通过IPlcRecipePort创建设备绑定动作；产品型号在实际翻转动作中下发，机械动作仍需真实设备完成。下表承接公开查询和独立bind消费，不新增页面或第二套执行。

| 目标字段/来源 | 当前合同 | 历史与边界 |
| --- | --- | --- |
| 独立recipeApplication预算 | 使用原已批准预算来源与Test10000ms版本化测试值；不是新现场默认值。意图真实提交后唯一t0，保留已有更早截止与CriticalSave | 旧快照不补预算，不延长原窗口 |
| RecipeBindingReceipt | 唯一字段见RC05.1：绑定/运行/料盘/配方内容/计划身份、ActionWindow、实际IntentCommit/BoundCommit/适用HandoffCommit、ReceivedTick和Validity | 不含DeviceApplied、RequiredCommunicationEvidence，不登记虚构机械占用 |
| 查询中的recipeApplication及bindingResult | 由同run当前真实绑定事实与提交后回执观察投影；预算来源、窗口与requiredCommits只取实际存在的业务提交，缺项为null/NotRecorded | 旧deviceApplied及通信回执仅由有限历史reader如实读取，不给新绑定补true或空成功对象 |
| Bound/Ready及handoff资格 | 本次适用业务回执均按期有效且当前取消/运行准入仍成立才续接；Matched、已有绑定行或handoff存在均不充分 | 按期Completed后的稍晚调度不重判绑定超时，后段仍核其原截止；旧状态不能恢复执行资格 |
| POST /api/v1/recipes/bind | 012负责RecipeEndpoints迁移，消费共同绑定能力；移除plcBinding字面成功和旧IPlcRecipePort/设备通信包装。返回类型化bindingResult，保留plan身份，productContinuationAuthorized=false | 200仅本次绑定真实Completed；已知拒绝/超期沿既有409/语义原因，HTTP取消不减弱后台关闭。页面不以此路由替代自然F绑定 |
| 通知 | 沿s01/notification/2.0提示GET重取，不复制绑定回执或授予产品动作 | 固定摘要不扩成设备/原始数据容器 |

意图只保存当时已知身份/预算；t0和后续回执随真实事件产生。绑定/handoff不得预填自身提交后的ReceivedTick或Validity，提交后观察按WriteId关联，沿现有ReceiptObserved而不递归批准自身。全库不可写时报告证据缺失，不保证终态已持久。取消、过期或提交未知关闭续接；已有意图、事实与失败证据保留。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。

历史查询只做有限旧payload解读，不补raw、不授权执行；API诊断引用只由实际已提交必要证据产生，无有效引用不能推断数据库无记录。

### 009 旧设备绑定通信保存回执（2026-10-02历史适用范围）

原009 FR-019/020/036/038及T037—T045/T047要求RecipeApplicationEvidence/RecipeApplicationReceipt携RequiredEvidenceCommit：同Correlation的真实WriteId、ActualCommit/Validity、CommittedUtc及实际接收时刻，SavePurpose=RequiredCommunicationEvidence。原DeviceApplied还要求该通信保存回执及时有效。以上说明已产生的旧协议事实和原验证义务，不作为011新F软件绑定前置，也不重做009历史验收。

有限历史reader按真实WriteId/Correlation及不透明引用读取原设备应用与通信保存记录；未存为null/NotRecorded，不改旧payload、不由行存在恢复资格。011当前资格与必要业务保存采用上方§5.5及RC05.1；实际翻转、放回、分拣等动作继续保存和核实其真实设备证据，不能因移除旧绑定握手放宽实际动作门禁。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：011当前执行顺序为Detection→Sorting→Unload；按本轮实际配置工作量冻结对应预算依据，不能因重排放宽期限或在阶段切换重置截止。旧context/1.0、2.0预算起点记录保持历史含义；独立bind只读既有有效截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：解码边界提供实际DecodedTrayCode及Run/Capture/Call关联；码文本即料盘编号，不由解码叶选择配方或插入产品名/测试编号映射。共同FCodePolicy对绑定时已成功保存内容唯一匹配，删除仍存在且无有效用途的Test固定码旁路；已在010删除的只核实，不重复删除义务。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。


## 011/012定向承接（2026-10-03）

共同合同支持1/2/4以外更多检测面，组限AB/CD；四面仍3CD＋1AB且AB位置由配方决定，不推导更多面固定组合。四检测面后可按配方执行独立E扫码姿态，扫码姿态与检测面分开，“第5”不是PLC原码。配置点位、三区域、姿态异常退出、物理槽位及内容快照均引用011合同。012完整读写消费同一模型和校验；保存/重读/F匹配/快照隔离证据与011共用。后续只验证受影响接口、必要失败保护及代表链；不因本次文档同步重做009/010历史存储升级或全量验收。正式地址、ASCII承载、速度、报警、恢复与安全控制按011局部延期，不补默认值或假成功。
