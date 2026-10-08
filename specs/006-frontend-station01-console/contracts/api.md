2026-10-04当前消费：现行翻面由共同配置Pick/Flip/PutBack及姿态复查执行；011消费者核查确认旧人工换面服务无生产调用并在稳定030删除活动端点/注册。前端删除ConfirmManualFlip及manual-flip提交消费，人工取盘按钮仅保有效取盘/已确认恢复用途。2026-09-26来源人工合同保留历史原件，其人工换面端点不再作为当前实现依据；历史WaitingManualFlip只读，不授当前动作。030尚未实际接收时按交付记录如实限定后端删除状态。

# 前端—后端公开接口合同

**012澄清交付时状态（历史）**：2026-10-03，012副本；已按统一确认定向修订本文件有效条款，历史证据及任务勾选保持原范围。仅文档同步，尚未合入主项目或证明实现/验证通过。

2026-09-26 E直接增量：原run媒体目录保留原字段，Role新增E，BusinessCamera=E，携实际ObjectId/LocalFace/HeightRound/StepSequence及已提交媒体身份。ECodeBinding事件查询包含externalCode或issue、内部对象、配置扫码面来源及保存/复位事实。码问题不等于运动失败或Final；接口不批准原型格位映射。

本文件只描述 006 前端允许消费的公开接口，不新增后端实现，不把当前接口缺口在前端绕过。所有请求使用宿主提供的后端基地址和认证凭据。

## 运行与查询

| 方法 | 路径 | 前端用途 | 关键规则 |
| --- | --- | --- | --- |
| POST | `/api/v1/station01/runs` | 启动公共准备 | 提交唯一 `requestId`、上下文和版本引用；202 只表示受理，不表示设备完成 |
| GET | `/api/v1/station01/runs/{runId}` | 运行快照 | 使用 ETag/版本；快照状态是唯一业务事实来源 |
| GET | `/api/v1/station01/runs/{runId}` 的 `startupDiagnostic` | 已受理启动阻断详情 | 可空；使用脱敏 `reasonCodes/safetyAssessment/stopStage/disposition/connectionEpoch/observedAtUtc/executionOrigin/semanticObservation`，仅同代次可靠反馈时 `semanticObservation` 非空；不得把 `Unconfirmed` 显示为设备明确不安全 |
| GET | `/api/v1/station01/commands/{commandId}` | 命令回执查询 | 断线/超时后查询原 commandId，不自动重发 |
| GET | `/api/v1/station01/status` | Host、PLC、相机、存储、维护、算法和当前运行状态 | 使用 ETag；受限状态必须原样显示 |
| GET | `/api/v1/station01/runs/{runId}/handoff` | 公共移交查询 | 409 `HandoffNotReady` 不是成功，不得填充虚假移交 |

007实际页面启动请求体使用当前Host字段`requestId`、`contextJson`、`publicConfigRef`、`budgetRef`、`simulationRef`。三个配置引用均为`{id,version}`；`contextJson`是序列化的JSON字符串，schemaVersion为`station01-start-run-context/1.0`，其中包含非空`trayId`、`stationId`、`lineId`、`scenarioId`、非空且不重复的`occupiedSlots`及`purpose=Test`。CAP/P01候选使用S1与P01占位，具体配置版本来自007冻结Test样本。请求由原型已有入口发起，不从页面伪造runId、F结果、配方绑定或设备完成；202后按原commandId/runId查询。

受控Test Bearer凭据由宿主配置传给页面HTTP/通知客户端，请求身份和权限由Host鉴别；页面不能从登录输入推断已认证、在请求体自报role/source或记录令牌。006负责客户端请求、凭据传递及WebView2页面侧消费；007负责Host仅允许指定Test页面来源的API/通知跨源响应及后端授权验证。两侧实际联通由007 T015最终核对，单侧合同测试不能替代。

## 控制与配置

| 方法 | 路径 | 权限 | 前端规则 |
| --- | --- | --- | --- |
| POST | `/api/v1/station01/runs/{runId}/pause` | `Run.Pause` | 携带 `requestId`、`expectedRevision`；显示暂停请求与实际状态的区别 |
| POST | `/api/v1/station01/runs/{runId}/cancel` | `Run.Cancel` | 同上；不把受理当作物理停止 |
| POST | `/api/v1/station01/runs/{runId}/recovery-checks` | `Recovery.Check` | 仅显示后端返回的核对结论 |
| POST | `/api/v1/station01/runs/{runId}/continue` | `Run.Continue` | 只提交后端认可的核对引用，不自行恢复 |
| POST | `/api/v1/station01/public-config/validate` | `Config.Validate` | 只显示校验结果，不把校验成功当作运行成功 |
| POST | `/api/v1/station01/reset` | `Run.Start` | 仅在原型已有入口且权限允许时调用；返回 `manualStartRequired` 时明确提示人工重新启动 |

## 媒体

| 方法 | 路径 | 规则 |
| --- | --- | --- |
| GET | `/api/v1/station01/media/{mediaId}` | 只能使用后端返回的 GUID `mediaId`；支持 ETag/304；404 或未就绪显示媒体不可用，不访问本地路径 |

## 状态通知

| 地址 | 事件 | 处理 |
| --- | --- | --- |
| `/hubs/station01` | `StateChanged` | 按 runId/revision 触发快照重取 |
| `/hubs/station01` | `DiagnosticChanged` | 触发状态/错误重取，不把诊断当完成 |
| `/hubs/station01` | `HandoffReady` | 重取 handoff 和运行快照后再显示移交就绪 |

通知载荷采用 `NotificationEnvelope`：`eventType`、`schemaVersion`、`runId`、`revision`、`persistedRevision`、`changedFields`、`summary`、`occurredAt`。事件可能重复、乱序或丢失，必须以 GET 快照为准。

007完整状态展示沿同一runId的GET与通知重取，至少区分公共准备、Detection、Sorting、UnloadPreparation、WholeTrayCompletion、ObservedUnlocked、AwaitingManualRemoval和最终`FinalUnloadCompletion`，并保留PLC/相机/算法/媒体/保存的真实可用性、Pending/Unknown/CommitUnknown及来源。仅当后端最终持久化查询确认时显示Final完成；页面不得把202、整盘完成或解锁观察提前映射为Final。当前复用正式页面既有取盘控件，在已提交解锁后由实际页面调用003唯一正式路由`POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`（请求体`requestId/expectedRevision/reason`）；006绑定已有控件、allowedActions及后端确认事实，保留Test/Simulated渠道；旧联调客户端自动确认不能代替页面证据。

## 错误与 HTTP 处理

目标合同是 `ErrorContract(code, message, category, traceId, retryable, details, currentRevision)`，覆盖 400/401/403/404/409/429/503。当前 Host 部分路由仍返回匿名 JSON/ProblemDetails，前端必须兼容读取 HTTP 状态和 `error`/`message` 字段，但不得猜测业务成功；该差异记录在 `gaps.md`。

建议映射：

| HTTP/代码 | 页面行为 |
| --- | --- |
| 401 | 清除前端会话，回到原型登录页并提示认证不可用/已过期 |
| 403 | 保留页面，禁用或隐藏无权限操作并显示权限不足 |
| 404 | 显示资源不存在/媒体未保存 |
| 409 | 显示冲突/状态未就绪，重新 GET；不自动重复动作 |
| 429 | 显示容量繁忙，按 `retryable` 或有界退避重试查询，不重发命令 |
| 503 | 显示 Host 停止/不可用，等待用户重新查询 |
| 网络/解析错误 | 显示 Offline/HostUnavailable，执行有界重连和 GET |

## 007媒体清单消费增量

先以`Read`权限调用`GET /api/v1/station01/runs/{runId}/media`，只消费当前run的`mediaId,captureId,role,stepSequence,businessCamera,committedRevision,committedAtUtc,readiness,source`；Unknown身份或NotReady不请求图片。同相机多次采集选择最大已提交revision的Ready项，不按返回数组顺序。确认006原型格位映射后，才以`MediaRead`权限调用已有`GET /api/v1/station01/media/{mediaId}`取图；跨run条目不得显示。当前共享合同及Host实现归007，本段不授权前端直读SQLite/媒体目录。

## 2026-09-24 008完整执行合同增量

本节依据宪章5.0.0、008最新澄清和用户最新原型授权，优先于前文冲突范围；旧记录保留原日期和范围。全部新增能力尚未实现/验证，当前tasks已追加S0—S5唯一归属任务，旧analysis仅历史，本次只读报告在会话输出。

共同字段及行为以[008接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)为准。

| 页面行为 | 正式接口/字段 | 约束 |
| --- | --- | --- |
| 加载/选用配方 | 既有GET /api/v1/recipes/catalog及共同身份引用 | 目录提供者由012承接真实保存内容；选用不直接bind，F按料盘编号绑定新保存内容，冻结运行不变；完整读取/保存按下述业务合同设计 |
| 启动 | POST /api/v1/station01/runs，contextJson/2.0含expectedRecipeRef | 保留其他请求/配置字段，合法多场景/槽位；公共F后核对实际绑定 |
| 状态/结果 | GET /runs/{runId}的recipeSelection/recipeExecution/allowedActions、results/movements/完成链 | 通知只触发GET；202不等于Final，缺事实不推测 |
| 翻面查询 | 现有run查询的共同实际阶段/槽位/观察事实 | 配方配置Pick/Flip/PutBack及真实复查；无人工确认HTTP提交，不用命令默认面代替姿态 |
| 确认已取盘 | 既有POST /runs/{runId}/manual-removal-confirmations；Run.Start | requestId/expectedRevision/reason；同run下料/整盘完成/解锁已保存，服务端核验 |
| 恢复核对与决定 | 既有recovery-checks、controlled-recovery-decisions；Recovery.Check | 显示原任务、最后可靠位置及未知动作，输入核对/原因/必要证据；保存决定不等于恢复完成 |
| 允许的继续 | 既有continue；Run.Continue | 仅正常暂停认可的checkId/revision；故障返回FaultRequiresNewRun，必须双端初始就绪后新POST /runs |
| 媒体 | 既有run媒体清单及/media/{mediaId} | 关联对象/面/实际测量来源次/相机、提交及来源，不能读本地目录或跨run |

2026-10-03统一确认的配方消费要求（012责任，尚未实现；签名/字段结构/存储技术留plan）：

| 业务能力 | 当前要求 | 单一责任与限制 |
| --- | --- | --- |
| 目录、完整读取、新建/编辑保存 | 目录与完整读取来自真实保存内容；保存调用011共同业务校验，成功后后续F用新内容，冻结运行用原内容；不得仅返回摘要冒充完整读取 | 012负责目录提供者/读写API，011定义共同模型及唯一校验；不建第二模型/校验器 |
| 料盘唯一匹配 | F扫码内容就是料盘编号；不同配方不能占用同码，重复码提交须拒绝；无匹配仅阻断依赖配方的产品动作，公共准备真实执行；首次继续才F扫码，空盘/介入无需F即可共享下料 | 012保存/目录与011匹配/绑定保持一致；料盘编号、配方身份、PLC型号分开 |
| 面与点位 | 更多检测面仍选AB/CD；四面3CD＋1AB且AB位置由配方定；四面后可按配方选额外E扫码姿态，用扫码Z，姿态不等于PLC码 | 输入配置和读取完整保留共同语义，不按型号/测试编号分支，不新增相机组 |
| 区域与处置 | 同盘OK/NG/Pending，初料在OK；OK分拣原槽不搬，NG/Pending去各自配置目标；分拣料盘位=上料位，检测/翻面取放/分拣点位用途不合并 | 姿态异常独立处置，保留原检测证据、退出后续检测/翻面，最后从原槽分拣Pending，返回异常物理槽号；OK不搬不取消检测期必要动作 |
| 真实结果与权限 | 受理、校验、持久保存、准入、F绑定、冻结运行、质量及物理处置分别显示；必要日志持久可关联 | 生产准入与现有权限保持；不新增发布审批，不以localStorage/假API/测试配方兜底 |
| 运行状态显示 | 011输出真实阶段及异常槽号；前端在既有运行/结果位置显示；适用分拣完成后下料，不从质量或通知推断动作完成 | 012负责数据绑定，不增加页面/设备控制；具体接口交接未完成不能声称已接入 |

路径/runs在表中均以/api/v1/station01为前缀。actor/source由Host认证/受控配置确定，前端不能自报。恢复提交沿既有请求字段；当前ControlEndpoints核对入口尚未完整消费reason/evidenceRefs，实施时须让必要人工依据实际保存并可查询，不能只补一个按钮。

旧007“006不调用取盘确认、由外部客户端代办”仅是其旧样本边界；008必须经正式页面操作。页面失联或revision变化先查事实，不自动重发启动或未知物理动作。现有授权/脱敏/3秒心跳边界保留。

## 2026-09-26协议关联设计（目标，未实施）

实际构建入口为`frontend/src/runtime.js`（build.mjs复制该文件），新面/执行阶段与实际测量来源分别显示，不把面2标成测量轮2；媒体选择来自同run已提交对象/面/相机事实，初始3D不伪装新采集。普通盘末展示检测→适用同盘分拣→下料定位→整盘收敛→解锁→页面确认→Final，取盘按钮仅消费后端allowedActions，不能见下料完成就启用。目标查询身份见[008 API合同](../../008-recipe-driven-inspection/contracts/api-results.md)，实现归本功能T048/T049；人工/恢复只按T050/T051局部前置。

## USR-20260926-D故障新轮绑定（设计未实施）

复位/核对沿已有recovery-reset/recovery-checks，Recovery.Check；显式新启动沿现有POST /runs及Run.Start，携restartFrom，字段/状态以[003恢复合同](../../003-plc-latest-protocol/contracts/recovery-test-execution.md)为唯一准则。EquipmentEngineer只有复位/核对权限不能借continue获得Start；可由有Run.Start的Operator后续明确启动，SystemAdministrator可分别执行两操作。无新角色/新页面/新控件，原因来自现有输入，状态及关联来自后端GET。故障旧轮可查询且不自动切换成新轮成功；收到新receipt后页面按新run查询，旧run引用留在已有诊断区域。通知只触发查询。正常暂停continue及取盘manual-removal-confirmations保持各自准入；不保留无生产调用的人工换面提交路径。

## RES查询消费与页面生命周期（2026-09-26）

006 T049消费既有GET /runs/{runId}的[结果投影增量](../../008-recipe-driven-inspection/contracts/api-results.md#res结果展示增量2026-09-26目标尚未实现)，T048既有请求/授权复用；不增加第二结果接口。现有results[].disposition是质量判定、quality是事实质量，必须按契约显式映射。resultContext选当前对象，inspections及既有媒体查询提供面/项目和证据；保存未决、未产生、字段不可用与后端Pending分开。

runtime.js的refresh/通知/定时轮询复用，按run、焦点及查询代次接收响应，串行合并重取；不得将较旧revision覆盖较新快照。ETag按003结果版本变化更新，304只复用同run同版本缓存。重开同Host或查询持久历史run都先GET，不使用内存旧页面结论或触发执行；新run清理旧焦点/图片/结果，旧异步响应丢弃。已提交结果在Detection未全部结束时也可展示；Final、算法技术Success和质量分离。缺陷未提供时动态说明缺失，不宣称后台没有任何事实。

取盘、暂停/人工继续和USR-D复位/核验/显式新启动继续原合同。实现必须同步实际build.mjs复制入口及WPF加载资源，验证规则见[交接](../../008-recipe-driven-inspection/plan-result-display-alignment-20260926.md)。

结果流程状态绑定采用同run只读executionState名称；保留state原枚举，质量区域仍只消费当前对象disposition。缺少名称且state不是字符串时明确状态未提供，不用其他run的状态或Final推断质量。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

既有结果字段dispositionState与movements消费011提交的真实处置：OK分拣留原槽，NG/Pending到各自目标；姿态异常独立保留原物理槽号，不伪造NG/Pending检测结论，另显示真实Pending物理处置。质量OK不能自行推导已无需搬运；需要后端明确处置事实。更多检测面与独立E姿态身份分别显示，不伪造第5检测面或PLC码。


## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready必须核本次有效RecipeApplicationReceipt，不能从已有handoff恢复。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

逐字段消费范围：


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
| plc.x / y / z:double（逐坐标） | position.actualX/actualY/actualZ:double?，有真实采样才赋；附轴用途/单位依据/观察身份，无值null | S；PS | H0/H3，不以零或目标补值 |
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
| TargetX / TargetY / TargetZ:double（逐坐标） | target.x/y/z:double，另附目标pointRef/version；目标始终为请求依据 | E；PE | H0，旧无目标版本null |
| ActualX / ActualY / ActualZ:double?（逐坐标） | actual.x/y/z:double?；完全无采样actual=null，不复制target | E；PE | H0/H3 |
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
| wholeTrayCompletionId:UUID? / readyForUnlockSourceMatrix:object? / finalSourceMatrix:object? / finalResult:string | 原样保留含各matrix字段/组件，不由取放或解锁单个状态推Final | E；PE | H0/H1 |
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
| HeightRound:int | target.heightRound:int?，保留真实测量关联，不由面序号补造 |
| Camera:string | target.camera:string?，已有业务相机角色，缺失null |
| PointRef:string | target.pointRef:string?，已有配置引用，缺失null |
| Point.Id/Version/Unit/Frame:string（四个字段） | target.point.id/version/unit/frame:string?，各自原含义保留，缺失null |
| Point.X/Y/Z:double（逐坐标） | target.point.x/y/z:double?，目标坐标不当实测 |
| Source:string | target.coordinateSource:string?，保留坐标配置来源，**不是设备执行Provider**；缺失null |
| ZBasis:string | target.zBasis:string?，实际配置/高度依据，缺失null |
| IsValid:bool（计算属性） | 删除公开字段；它只是当时配置校验派生，不是动作完成。旧payload字节保留，不从当前模型重算历史真假 |

### 5.4 通知逐字段与消费者

| 旧字段：含义/类型 | 处理及目标字段/类型/缺失 | 版本；产消/责任 | 历史 |
| --- | --- | --- | --- |
| eventType:string | 保留StateChanged/DiagnosticChanged/HandoffReady/WholeTrayCompleted/ObservedUnlocked/FinalUnloadCompleted的既有含义 | N；PN | 原通知包不改，不作提交证明 |
| schemaVersion:string | s01/notification/1.0→N | N；PN | 历史包保留版本 |
| runId:UUID；revision/persistedRevision:long | 各字段原样保留；修订只作GET对账提示 | N；PN | H0 |
| changedFields:string[] | 仅业务语义路径；旧error提示统一errorCode，新增startupDiagnostic变化提示其路径；禁止raw/内部握手路径 | N；PN | 旧通知路径原样归档，不作为当前API字段 |
| summary:object?（实际整个RunSnapshot；TS当前声明string不符） | 固定对象或null：{executionState:string,wholeTaskState:string,errorCode:string?}，不含完整run或startup原始反馈。前端TS按对象对齐，通知只提示GET | N；PN | 不补造旧摘要、不重放作新运行状态 |
| occurredAt:timestamp | 原名保留通知产生时间，不等于采样/持久时间 | N；PN | H0 |

消费者实际边界：runtime.js:331–345展示启动原因，:345/381来源需由已有数据区域绑定真实值；notification-reducer.ts:1目前类型不符；CJS报警断言与Python取放码断言按V02.1拆分，旧raw断言由通信探针保留。上述绑定只在006既有页面区域/文字含义内调整数据，不改变客户原型结构、控件、静态文案或交互。外部未登记客户端发布前盘点，不能保留raw影子作兼容。

### 5.5 本轮配方应用期限的语义字段增量（待实现）

沿R/E的`device-semantics/1`及现有业务事实读取，不新增页面或诊断控制入口。下列新元数据仅来自当前真实冻结/观察/保存；历史缺项为null并标NotRecorded，不根据旧RecipePlanBound存在就反推预算、设备应用或及时回执。业务字段形状登记同时进入A02/A08—A10；不是任意JSON兼容通道。

| 旧字段/当前含义 | 目标字段、类型及缺失 | 版本、生产者/消费者与调整责任 | 历史 |
| --- | --- | --- | --- |
| BusinessBudget.businessMs无独立绑定预算；冻结JSON无此键 | `businessMs.recipeApplication:int`必需；新运行不得null/缺项，Test10000 | budget schema1.1；配置作者→Loader/Validator/Freezer→三入口；AL08/I25 | 旧快照原文保留，历史可读不补默认，不授权新绑定 |
| 当前RecipePlanBound事实含计划/版本，未记录完整总窗 | 既有`motionEvidence[].facts`增加有限`recipeApplication`对象；当前绑定事实必需，其他动作不输出此对象 | E及持久device-semantics/1；Application保存→Infrastructure只读投影→Host/业务采证；AL03/04/06/08 | 旧缺项null/NotRecorded，无raw补造 |
| 无当前绑定预算来源字段 | `recipeApplication.bindingId:string`、`budgetReference:{id,version,purpose,source,digest,snapshotId:string,budgetMs:int}` | 同上；来源为当前Run/Binding冻结事实；不由通信定义生成 | 任一历史来源未存则该字段null，整组不可完整取得为null |
| 无绑定唯一总窗及校验时刻 | `recipeApplication.window:{clockId:string,startTick:string,budgetDueTick:string,effectiveDueTick:string,startedAtUtc:timestamp,deadlineAtUtc:timestamp,applicableDeadlineReferences:[{stage:string,startedAtUtc:timestamp,deadlineAtUtc:timestamp}]}`；`hostValidatedTick:string?`。tick以既有Host时钟单位的十进制字符串保存/传输，避免JS整数精度损失；数组仅列实际存在的Detection/Unload/Sorting原截止 | 同上；业务登记/仲裁，不使用PLC时间回填；Host只投影，时钟单位/频率沿既有clockId来源记录 | 无旧时钟/起止记录则null；不得按10秒推算；适用截止历史未知不能填[]冒充确无 |
| 原绑定完成隐含设备成功、未分开保存回执 | `recipeApplication.deviceApplied:bool?`、`outcome:string`（Applying/AwaitingRequiredBusinessCommits/Completed/TimedOut/Cancelled/Failed/HeldUnknown）、`requiredCommits:[{writeId:string,kind:string,actualCommit:string,receiptValidity:string?,committedAtUtc:timestamp?,hostReceivedTick:string?}]`、`diagnosticEvidenceReferences:opaque[]` | 同上；未观察deviceApplied=null；kind限BindingIntent/RecipePlanBound/本次Handoff/RequiredCommunicationEvidence等保存用途。实际commit读存储元数据，Host回执读独立Audit观察，不在自身事务预填。完成须全回执及时有效 | 原行/真实提交不改；缺回执观察null/NotRecorded，不能自动标ValidCurrent |
| RunSnapshot.RecipeState及对应查询Bound/Ready当前可能仅据handoff存在 | 保留业务字段/序列化形状；只有本次所有必要回执按期齐备且当前资格有效才可续接Bound/Ready，已有绑定/handoff行仅为事实。按期Completed后的稍晚调度不重判总窗超时，仍核当前安全/取消/后段 | R；公共流程/当前Receipt→PublicPreparationHandoffV2、QueryEndpoints:155–157/173–174与通知GET；AL02/03/04/08。通知固定摘要不扩字段 | 旧状态仅历史声明；重启或迟到核查无当前资格不得恢复续接 |
| `/api/v1/recipes/bind`现200 `{plan,plcBinding:"CommittedAfterF"}`仅说明端口返回 | 保留plan业务含义；移除plcBinding字面成功，改`deviceSchemaVersion:"device-semantics/1"`与类型化`bindingResult`，包含bindingId、outcome、budgetReference、window、requiredCommits、诊断引用；200仅Completed且及时有效。已知拒绝/超期沿409错误响应携语义reason及关联；HTTP取消可无响应但不减弱后台关闭义务 | Application共同绑定能力→Host端点→API调用/采证；AL02/03/04/08，发布前盘点调用方；无新增页面 | 旧响应原包只读，不转换为新的有效Receipt，不留raw或plcBinding影子字段 |

必要成功/失败记录仍由业务事务持有；通信只产真实设备证据，不能为填上表直接写业务库。全库不可写时只能报告证据缺失，不保证新窗口终态已持久。上述目标结构还需跨功能API/配置合同实际对齐，当前源码和消费者尚未修改。

字段产生顺序是本表的约束：意图只存先已知的预算/关联，t0/D/T随后产生并进后续事实；原绑定/handoff只存写前已知信息与先前回执。自身HostReceivedTick/ReceiptValidity由提交后Application语义Audit观察独立记录，查询按WriteId连接，不改不可变handoff、不要求Audit递归证明自身回执。观察保存有界且仅为证据，不产生新的动作授权；缺失按上表null/NotRecorded并使所需验证证据不完整。


### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。


## 021受控联调用途增量（2026-10-08）

本次仅定向更新021消费者合同，旧用途/历史证据保持原范围。独立规格与设计见[021规格](../../021-commissioning-console/spec.md)、[计划](../../021-commissioning-console/plan.md)、[任务](../../021-commissioning-console/tasks.md)。新用途为RealDeviceCommissioning，运行purpose为Commissioning。

预配置单身份由后台核权，Operator运行/ProcessEngineer编辑，不用Test令牌或客户端角色授权；identity GET、固定appassets.local来源、头认证和宿主内存凭据按[IH合同](../../021-commissioning-console/contracts/identity-host.md)。按主体/requestId只读启动查询、Final持久提交后同Run普通释放及只读start-admission按[SC合同](../../021-commissioning-console/contracts/start-and-completion.md)；未知不重发，保留原故障恢复。

七格显示已由用户确认C/D/A/B/E/3D/F；本新用途的对应问题关闭，旧Test临时映射证据不改。当前Run已提交图像/结果、未参与不补图和三页既有承载按[RM合同](../../021-commissioning-console/contracts/recipe-media-ui.md)，不改变采集工艺/硬件绑定或原型布局。
