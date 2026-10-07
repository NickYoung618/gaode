# 第一工位API合同

**版本**：s01-api/1.2；2026-10-03按011/012统一澄清定向修订。共同配方语义唯一引用[recipe-contract/1.3](../../011-plc-interaction-update/contracts/recipe-contract.md)，运行阶段与异常槽位唯一引用[station01-execution/1.0](../../011-plc-interaction-update/contracts/execution-and-state.md)。这些是目标设计，不代表实现或运行验证完成。  
共同规则：[common.md](common.md)。只管理公共阶段，不提供任意PLC读写、单步运动、重拍、配方匹配或下一工位启动入口。

## 0. 实现基线与冻结门

2026-09-22只读审计确认：Host当前已有`POST /runs`、运行/命令/handoff/status查询、受控media读取及`/hubs/station01`；暂停、取消、恢复核对、继续、公共配置校验路由及对应策略尚未全部实现。当前通知仍是过渡性的`RunSnapshot`广播，状态ETag、统一错误和媒体跨重启索引也未完全符合本合同。以下合同描述目标行为，标为`[IMPLEMENTATION GAP]`的项目必须先进入任务清单，再修改代码。

冻结门：DTO和上下文字段、ErrorContract、权限策略、revision/ETag覆盖范围、SignalR事件载荷、mediaId/source/purpose元数据及Test身份映射均通过合同测试后，才允许006前端把接口视为稳定依赖。本合同消费011共同配方和执行合同；012负责配方目录、完整读写API与006页面绑定，不在公共准备API另建模型、校验或执行入口。

## 1. 命令与查询

| 方法与拟定路径 | 输入 | 响应与语义 |
| --- | --- | --- |
| POST /api/v1/station01/runs | requestId、context、publicConfigRef、budgetRef；Test模式另需simulationRef | 202 CommandReceipt，含runId、commandId、receiptDurability、statusUrl；先建上下文，异步校验/冻结，不等整个流程 |
| GET /api/v1/station01/runs/{runId} | runId | 200运行快照：revision、六类状态、阶段、配置/计时依据、观察事实及保存/限制 |
| GET /api/v1/station01/runs/{runId} 的 `startupDiagnostic` | 已受理启动 | 可空语义启动诊断；具体字段、版本及历史缺失按本文009/AL01节，不再公开原始reliableFeedback镜像 |
| GET /api/v1/station01/commands/{commandId} | commandId | 受理、准入/保存结论、当前处理结果；不存在404 |
| POST /api/v1/station01/runs/{runId}/pause | requestId、expectedRevision、reason | `[IMPLEMENTATION GAP]` 202暂停请求；禁止安排新步骤，等待本次在途操作和安全反馈；不是立即Paused |
| POST /api/v1/station01/runs/{runId}/cancel | requestId、expectedRevision、reason | `[IMPLEMENTATION GAP]` 202取消受理；立即关闭新动作准入，requestAccepted/admissionClosed/stopState分别返回；terminalDecision=Pending或CommitUnknown时applied=null；持久完成先胜则applied=false，取消条件事务已提交才applied=true |
| POST /api/v1/station01/runs/{runId}/recovery-checks | requestId、expectedRevision、sameTray、loadingUnchanged、snapshotStillApplicable、evidenceRefs、reason | `[IMPLEMENTATION GAP]` 202核对请求；后端再读取实际设备/保存状态，保存核对结果；人工true不是可靠完成证据 |
| GET /api/v1/station01/runs/{runId}/recovery-checks/{checkId} | 标识 | `[IMPLEMENTATION GAP]` 操作种类、正常暂停可复用步骤；故障initialCheck/reset及Blocked项、设备观察版本，不返回故障续跑资格 |
| POST /api/v1/station01/runs/{runId}/continue | requestId、expectedRevision、checkId | `[IMPLEMENTATION GAP]` 仅正常Paused且核对仍适用、状态允许并重新检查动态安全后202；故障返回FaultRequiresNewRun；核对过期/条件变化409或明确受限；不能重复启动/重采 |
| GET /api/v1/station01/runs/{runId}/handoff | runId | 尚未完成返回409 HandoffNotReady及限制；完成返回不可变移交快照及ETag |
| POST /api/v1/station01/public-config/validate | configRef | `[IMPLEMENTATION GAP]` 200分项校验报告：blockingControlErrors、algorithmIssues、warnings；只校验，不发设备请求或生效 |
| GET /api/v1/station01/status | 无 | Host、设备、存储/维护、当前运行及算法可用性分别列示；算法Unavailable不自动令控制Ready=false |
| GET /api/v1/station01/media/{mediaId} | mediaId | 授权后按记录读取已保存媒体；不存在/未保存明确拒绝，不能提交任意文件路径 |

context包含调用方实际提供的workOrder、batch、scenario及可空externalTaskId；内部runId无条件独立生成。开发样例使用这些字段，不校验未提供的现场业务格式；最终对接规则仍为S01-Q02。purpose/provider不由不可信请求随意覆盖，以Host模式和已加载配置交叉验证。

`GET /status`必须分别公开host、PLC、相机、存储/维护、当前运行和算法可用性；算法状态至少覆盖`NotConfigured`、`NotIntegrated`、`NotReady`、`Unknown`、`DependencyFailed`、`NoResult`、`InvalidResult`、`Cancelled`以及可用`Success/Error/TimedOut`，必须如实显示，不得返回虚假Success。ETag覆盖所有会改变公开状态的事实，而不只覆盖活动运行数或连接代次。

publicConfigRef/budgetRef/simulationRef是受控配置目录内的ID与版本，不是路径或远程URL。配置失败保留当前上下文；重发原启动只查询原结果，补配置不自动PLC启动。未取得合法冻结快照的配置受限运行不能冒充恢复成功；补齐配置后，显式取消尚未动作的受限运行并保存结果，再提交新请求建立新运行。保留原上下文及错误，不借continue更换快照。

CommandReceipt不附加硬件成功语义。若队列无容量且尚未受理，429 Busy；存储在提交前失败返回/更新503 SaveUnavailable及明确durability，运行保持受限。已发停止请求不得因审计失败撤回。400结构错误、401未认证、403无权限、404未知标识、409状态/幂等/版本冲突统一采用中文Error合同。

## 2. 暂停、取消与恢复

- pause到达后不安排下一动作；已发动作不能靠取消Task宣称停止。需要硬件停止时交Motion按已有契约请求，未知状态保持PauseRequested/RecoveryRequired。
- 暂停不暂停已经开始的算法/动作期限；完成事件仍保存，算法到期仍形成终态，但不触发下一物理步骤。安全静止且必要记录已保存后Paused。
- cancel立即关闭新动作与继续资格，算法可收敛为Cancelled；在途动作未确认停止时StopPending。设备已核对且必要保存完成，只表示可以提交取消候选，不能直接把内存Run置Cancelled。最终按[persistence-handoff §1.2](persistence-handoff.md)运行版本/None前置的条件事务裁决。
- CommandReceipt及命令查询分开返回requestAccepted、admissionClosed、stopState、receiptDurability、terminalDecision（Pending/CommitUnknown/Cancelled/NotApplied）、decisionWriteId、applied（未决null、最终取消true、完成先胜false）与reason。重复取消返回同一Command；HTTP202、停止受理和审计入队均不是最终取消。
- 移交排队未提交时取消先关准入，但不承诺取消一定胜出；移交已提交仅丢回执时读取实际Run/Handoff并返回NotApplied(AlreadyCompleted)。移交CommitUnknown时保留原WriteId核对，不能先返回Cancelled；取消已提交后旧移交写/回执不能使handoff接口Ready。
- Run查询同时返回cancelRequested、terminalResolution、finalOutcome、observedRevision/persistedRevision及Handoff状态；未决可为CancelRequested/StopPending，Handoff为Saving/NotReady。读到已核实持久终态才由Coordinator更新投影；API不独立改状态，通知仅引用相同持久版本。缓存尚未核对时标Pending/CommitUnknown，不能声称相反的最终状态。
- 重启查询按持久Run/Handoff/Write与命令记录返回同一裁决。已持久取消请求未完成时继续关闭准入，须核对物理与保存后才能提交取消；仅内存受理且未保存的请求标不可证明，不虚构持久命令。未终态运行仍RecoveryRequired，不自动继续。完成已提交不因迟到取消回退，所需停止与审计独立处理，不撤回停止或自动释放占用。
- 正常暂停continue必须引用已持久化核对结论及对应revision；再次检查同盘、装载、快照、必要保存和设备状态，条件改变使核对失效。Cancelled/Completed/CompletedWithExceptions不可复活。
- 调用方离线不取消运行，不形成新动作。重复请求遵循common幂等规则。

## 3. 权限

正式策略按OPEN-23补齐。本阶段定义权限Run.Read、Run.Start、Run.Pause、Run.Cancel、Recovery.Check、Run.Continue、Config.Validate、Media.Read。四类角色保持独立：

| 开发测试角色 | 显式测试授权 |
| --- | --- |
| Operator | Read/Start/Pause/Cancel/Media.Read |
| EquipmentEngineer | Read/Pause/Cancel/Recovery.Check/Continue/Media.Read |
| ProcessEngineer | Read/Config.Validate/Media.Read |
| SystemAdministrator | 本表全部；仍不得绕过设备安全和保存 |

Test模式的认证替身使用显式配置的本地测试令牌到主体/权限映射，仅监听loopback；不信任请求体或任意角色Header，不预置通用生产密码。Production/Hybrid真实动作必须绑定正式身份来源，否则拒绝相应控制入口；不开发完整账户管理页面/平台。所有关键命令记录主体、请求、授权结果、原因及保存状态。

## 4. 状态通知

拟定SignalR地址/hubs/station01，只发版本化的StateChanged（runId、revision、persistedRevision、变化摘要）、OperationChanged、DiagnosticChanged及HandoffReady引用。客户端先GET快照，丢失/乱序通知按revision重新GET；通知发送失败不推进/回退运行。单连接有界缓冲合并可替代的状态，不能将通知当动作确认。无大图、点云或任意文件路径广播。现有完整RunSnapshot广播只可作为过渡兼容，不是冻结合同。

## 5. 公开投影与媒体边界

错误响应统一采用`ErrorContract`（code、message、category、traceId、retryable、details、currentRevision），覆盖400/401/403/404/409/429/503；不泄露内部堆栈、设备路径或密钥。媒体只接受由后端返回的受控`mediaId`，响应包含contentType、ETag、readiness、source和purpose；Test媒体可以是合成或manifest登记的受控本地fixture。manifest必须提供fixtureId、相对路径、contentType、sha256、enabled、source=Simulated、purpose=Test，Host只允许Test受控根目录内的规范化相对路径，禁止任意路径、任意上传和直接文件夹访问。媒体索引重启后仍须能按记录拒绝未知或未保存媒体。

## 6. 测试控制边界
启动入口拒绝时现有 `ErrorContract.details` 仅给 `requestId`、`runCreated=false` 和允许的查询或人工核查提示，不捏造 `runId`；受理后的阻断详情从运行快照查询。合同不是实现已完成的证据。

可控时钟Advance/Drain与安全输入为进程内测试夹具控制口，不作为正式业务REST接口。第一工位启动使用当前连接代次的可靠夹紧语义事实；测试夹具不得凭额外按钮或合成事件代替正式准入事实。实际时间观察由开发集成驱动器操作同一模拟设备输入，PLC内部夹紧不得由Host伪造完成。信号地址、原始码及内部握手仅由通信合同定义，不是API请求字段或业务断言。003协议可通过独立VirtualPlc进程做协议联调，但其证据标注`source=Virtual`、`purpose=Test`，不得当作真实PLC验收。未推进虚拟时间时，HTTP/进程内命令受理和当前快照查询照常处理。


修订记录：2026-09-20，1.1，H02区分取消受理/停止/持久终态及未决展示；2026-09-22，1.2，登记实际路由审计、控制接口缺口、版本化事件、统一错误、受控媒体及Test身份冻结门；沿用002/003边界，未添加真实外部协议假设。

## 008正式启动增量（2026-09-24，宪章5.0.0）

既有station01-start-run-context/2.0中的expectedRecipeRef表达页面选择意图，保存该意图用于关联，不能冻结尚未进行F绑定的旧目录内容。首次公共3D提供本盘姿态观察及F扫码XY；F内容即料盘编号，由唯一共同目录匹配。配方通过共同校验并真实保存成功后，后续F绑定取得新内容，已冻结运行继续使用原内容。料盘编号、配方身份、内容版本及PLC产品型号分别表达。未匹配只阻止依赖配方的产品动作，不阻止正常流程所需公共准备和F扫码；不接受客户端自报匹配或保存成功。请求形状迁移与选择意图比较详见[共同配方合同](../../011-plc-interaction-update/contracts/recipe-contract.md)，已有启动权限、幂等、拒绝/受理后Blocked边界详见[008 api-results](../../008-recipe-driven-inspection/contracts/api-results.md)。006 T048、003 T068、001 T090编号和历史完成状态保留原义；不能使用旧目录引用或兼容默认值绕过当前规则。

## USR-20260926-D故障边界

2026-09-26夜间T052正常暂停细化：pause仍先返回PauseRequested受理。执行方在已派发动作可靠结束、必要保存/握手完成且无在途操作的安全边界记录Paused；暂停本身不证明PLC停止。既有recovery-checks核验原盘/配置，既有continue核验Paused及非故障后解除同一ControlLatch，执行方恢复原阶段/步骤及原run，不重做已完成3D/F、不延长业务期限。安全/连接代次变化、取消或Host关闭仍走原阻断路径；故障continue始终拒绝。无新增路由或字段。

历史USR-20260926-D的双端复位/初始核对和restartFrom记录仍见[003恢复合同](../../003-plc-latest-protocol/contracts/recovery-test-execution.md)，不自动成为011新协议的恢复控制依据。011恢复与安全控制按DEP-03/04保持延期，只限制依赖部分；正常暂停、取消关闭准入、原期限及必要保存保护继续有效。现有recovery-checks的sameTray/loading/snapshot仅能记录实际人工核对或正常暂停核对，不能授权故障步骤复用。旧保存回执未知仍用原WriteId核验，不重新执行旧动作。2026-09-22 IMPLEMENTATION GAP及后续路由审计属于各自当时状态，本次文档修订不宣称这些路由已按新协议实现。

## 009 / AL01 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

startupDiagnostic删除source和reliableFeedback原镜像，新增executionOrigin:{provider,componentVersion?,quality}及semanticObservation?；仅同代次可靠观察才非null。保留reasonCodes/safetyAssessment(Other/ExplicitUnsafe/Unconfirmed)/stopStage/disposition/connectionEpoch?/observedAtUtc?，新增schemaVersion/recordNature/rawAvailability/diagnosticEvidenceReference?。semanticObservation仅connection/operatingMode/safetyAssessment/语义alarms等，不含alarmBits/alarmSeverity/plcSystemFault。原始诊断仅受权独立只读查询，缺历史raw不补造；历史startup无完整事实则null。运行其余字段不变。


## 011/012运行查询与职责（2026-10-03）

同run的查询和通知分别表达首次3D/F、绑定、检测、翻面取放、放回后的统一3D姿态复查、按配置适用的独立E扫码、分拣、下料及人工取盘/最终保存事实；阶段字段和异常物理槽号以011执行合同为唯一定义。姿态异常独立记录并保留先前结果，退出后续检测和翻面，保留原检测事实；最后从原槽真实分拣到配置Pending目标，不能伪造NG/Pending检测结论。OK在分拣阶段留原槽，NG/Pending分别到对应区域配置点；分拣完成后才下料，不能以handoff或质量判定冒充Final。012仅将真实阶段、结果和异常物理槽号绑定至已有界面；配方编辑授权由012规格管理。

共同业务、校验、F绑定、快照、通信和执行归011。后续实现中`backend/src/Gaode.Host/Api/RecipeEndpoints.cs`与`backend/src/Gaode.Host/Program.cs`由012唯一编辑，011提供共同能力及装配要求；本轮只修订合同，不改代码。
