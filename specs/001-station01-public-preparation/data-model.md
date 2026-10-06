# 第一工位数据与状态模型

**版本**：1.2.0　**日期**：2026-09-22　**规格**：[1.2.0](spec.md)。以下为拟定模型/公开接口约束；模型迁移及本轮新增接口尚未全部实现。

## 1. 身份与关联

RunId是内部公共运行标识，独立于可空ExternalTaskId、料盘码和工单。每个Run关联一个冻结PublicSnapshot、BudgetSnapshot及可选SimulationSnapshot；两个固定点位分别标版本，3D范围有ScopeId/Version。ActionId、CaptureId、CallId、WriteId各自唯一，Attempt=1为本功能正常执行；没有自动重试/重拍。

PartId/FaceId/GroupId/SlotId在本阶段不存在，只保留明确的NotEstablishedBeforeRecipe原因；HeightSample.SourceElementId是算法自带标识，不解析成这些身份。F码不作为数据库主键或跨Run唯一键；同码可以出现在不同合法运行。

## 2. 逻辑保存模型

| 拟定记录 | 主要字段 | 约束/所有权 |
| --- | --- | --- |
| Run | RunId、RequestKey、contextJson、Stage、RunState、Revision、PersistedRevision、TerminalOutcome、TerminalRevision、terminalResolution、cancelRequested、SnapshotId、started/completedUtc | Station01Coordinator唯一业务写所有者；持久终态由revision/None条件事务唯一裁决，TerminalRevision在终态后不变；物理任务未闭环禁止竞争Run |
| CommandRecord | CommandId、subjectId、requestId、kind、payloadDigest、RunId、ReceiptState、Applied可空、requestAccepted、admissionClosed、stopState、terminalDecision、decisionWriteId、Error | unique(subjectId,requestId,kind,scope)；同键异内容冲突 |
| ConfigSnapshot | SnapshotId、RunId、kind、id/version/source/purpose、canonicalJson、digest、capturedUtc、capabilityVersions | unique(RunId,kind)；不可变；算法配置缺失显式保存，不伪造产品配方 |
| Operation | OperationId、RunId、kind、sequence、Attempt、SessionId、状态、StartTick、DueTick、ClockId、BudgetRef、TerminalReason | unique(RunId,kind,sequence,Attempt)；有效终态一次；业务状态由Coordinator归约 |
| ActionFact | ActionId、OperationId、parentStartId、pointId/version/XY、IntentWriteId、dispatch/accepted/executing/feedback证据 | 夹紧为PLC内部动作观察，不额外生成PC夹紧指令；无新寄存器声明 |
| CaptureFact | CaptureId、OperationId、role、point/scope版本、触发次数、结束证据、媒体接管、connectionEpoch | F每Run唯一有效Capture，triggerCount≤1；未知是否触发不可补发 |
| AlgorithmFact | CallId、OperationId、Attempt、RunId/CaptureId、inputMediaRefs、public/scope/参数/能力/期望组件版本、IntentWriteId、InvocationBasis、DispatchEvidence、TechnicalState、RawArtifactId、responseReceived、结果JSON、deadlineDecision | AlgorithmIntent保存Call与原期限/关联后才可派发；DispatchEvidence=NotDispatched/Dispatched/Accepted/Unknown须有证据，缺日志不能推断NotDispatched；实际组件版本只在取得后保存 |
| HeightSample | CallId、原始序号/sourceElementId、原始值、normalizedValue可空、unit/datum/validity/reason | 一项或多项；不假定整盘同高，不作为本阶段Z动作输入 |
| FResult | CallId、rawCandidates含重复、distinctRawValues、recognitionState、primaryCode、parseState、parsedFields及来源 | 未取得响应rawCandidates=null；冲突primaryCode=null；Ordinal去重不改写原值 |
| MediaArtifact | MediaId、RunId/CaptureId/CallId、kind、relativeKey、byteLength、format、FileState、MetadataState、source、lease情况 | 已完成文件才可登记Ready；路径限定数据根，媒体不存大BLOB |
| PersistenceReceipt | WriteId、RunId、batchKind、expectedRevision、expectedTerminal、candidateTerminal、payloadDigest、CommitState、CommittedRevision、时间/原因 | WriteId唯一；不因重复回执重复推进，不宣称入队已保存；终态条件失败记录ConditionRejected及实际revision/终态 |
| RecoveryCheck | CheckId、RunId、subject/time/reason、sameTray/loading/snapshot依据、设备观察代次、正常暂停复用引用；故障reset/initialCheck及新run关联、结论 | 核对事实不可变；故障不产生continue资格 |
| ResolutionEvidence | 原OperationId、迟到或重新观测证据、核对关联、真实完成/停止结论及保存引用 | 不改写原TimedOut/Unknown；对原动作另记核对结论，只在正常暂停支持有依据复用；故障补存旧事实，不复用为新轮完成 |
| Diagnostic/Audit/LateEvidence | 关联ID、Error合同、首次/末次/计数、原始引用、处理去向 | 关键事件可靠保存；重复明细受限并合并计数，不丢终态 |
| HandoffSnapshot | HandoffId、RunId、schemaVersion、payloadJson、terminalOutcome、completedRevision、WriteId、createdUtc | unique(RunId)；仅Completed类终态；与Run.TerminalOutcome/TerminalRevision匹配且同事务保存，不可变 |
| StoreManifest | storeId、schemaVersion、迁移清单、profile=Test/Production、prepareOperationId | 独立准备入口创建；Host只核验，不静默建库 |

数据库映射候选：SQLite TEXT保存UUID/UTC/枚举及版本化JSON；INTEGER保存revision、长度/计时值；业务域数值检查有限性，点位精度/编码到真实Float32由OPEN-08/26适配验证。UTC以统一格式保存，排序使用显式字段；不用SQLite原生rowversion，采用应用revision与条件更新。必要外键、唯一键及RunId/OperationId/time索引；不创建后续Part/Recipe/Sorting业务表。

持久Run.Revision用于条件更新，观察版本/缓存PersistedRevision不替代事务前置；新增TerminalRevision只在终态提交时固定。RunState的三种终态必须与TerminalOutcome一致，非终态只能对应None；用同一模型约束禁止迟到的一般状态保存将已完成Run改成CancelRequested/Cancelled。Handoff以(RunId,completedRevision,terminalOutcome)关联Run对应唯一键，且仅允许两类Completed值；Cancelled必须无Handoff。完成事务原子更新Run、插入Handoff并保存WriteId；取消事务原子更新Run、保存取消结果及WriteId。条件失败整批回滚，禁止任何其他路径覆写终态；条件拒绝审计只记录该结果，不修改Run/Handoff。未实现初始迁移仍采用同一s01-store/1，不虚构已有库升级。

维护用SchemaVersion与业务配置version分开；初始开发结构建议s01-store/1，尚未生成EF模型和迁移文件，不提供第二套手写DDL作为事实。

## 2A. 公开接口投影模型

下面的类型是API/SignalR向前端公开的稳定投影，不替代上面的领域记录；所有写操作仍由Application协调器和持久化条件事务裁决。

| 公开类型 | 必要字段 | 约束 |
| --- | --- | --- |
| `ApiCommandReceipt` | commandId、runId、requestId、requestAccepted、admissionClosed、stopState、receiptDurability、terminalDecision、decisionWriteId、applied、statusUrl、error | 202只表示受理；`applied=null`表示未裁决，不能把HTTP成功当设备或业务成功 |
| `StatusSnapshot` | schemaVersion、revision、etag、host、plc/camera/algorithm/storage/maintenance、currentRun、capabilities、observedAt | 算法状态必须覆盖NotConfigured、NotIntegrated、NotReady、Unknown、DependencyFailed、NoResult、InvalidResult、Cancelled及可用Success/Error/TimedOut；ETag覆盖所有公开状态事实，算法不可用单独显示，不自动伪造控制失败 |
| `NotificationEnvelope` | eventType、schemaVersion、runId、revision、persistedRevision、changedFields/summary、occurredAt | 事件可丢失或乱序；客户端按版本重查快照，绝不以通知代替动作确认 |
| `MediaReference` | mediaId、runId/captureId/callId、kind、source、purpose、contentType、byteLength、etag、readiness、createdAt | 只允许受控索引中的`mediaId`；测试媒体标记`source=Simulated`、`purpose=Test`，不得暴露任意文件路径 |
| `TestIdentity` | subjectId、role、permissions、mode、tokenId | 仅Test模式的显式本地令牌映射；不接受请求体角色或任意Header冒充生产身份 |
| `ErrorContract` | code、message、category、traceId、retryable、details、currentRevision | 400/401/403/404/409/429/503统一结构；错误不得泄露设备路径、密钥或内部堆栈 |

`StatusSnapshot`和`NotificationEnvelope`均携带可比较的版本；`ApiCommandReceipt`与命令记录共享幂等键。媒体元数据可以引用合成或受控本地fixture，但不能把模拟结果升级为真实相机结论。正式身份、设备能力和算法版本仍按OPEN-22/23/26单独核验。

### 2A.1 受控测试媒体fixture manifest

测试媒体索引是配置资产，不是任意文件浏览接口。manifest中的每个条目至少包含`fixtureId`、`relativePath`、`contentType`、`sha256`、`enabled`、`source=Simulated`和`purpose=Test`；Host从Test配置指定的媒体根目录解析相对路径并校验规范化路径仍位于该根目录内。未列入manifest、哈希不匹配、用途不是Test或扩展名与contentType不一致的文件不得生成公开`mediaId`。manifest本身不允许前端上传、修改或传入绝对路径。

## 3. 状态维度

| 维度 | 状态集合及含义 | 终态/限制 |
| --- | --- | --- |
| Run | Created、Preparing、ConfigurationBlocked、WaitingStartAcceptance、WaitingSafety、WaitingPhysicalStart、WaitingClamp、Running3D、RunningF、SavingHandoff、PauseRequested、Paused、Blocked、StopPending、RecoveryRequired、CancelRequested、Cancelled、Completed、CompletedWithExceptions | Cancelled/两种Completed不可复活；物理状态与Run分离 |
| Action | NotRequested、IntentPending、IntentCommitted、Dispatched、Accepted、Executing、Completed、Failed、Unknown | 超时/断联/取消不记Completed；Unknown附加ResolutionEvidence才可受控核对 |
| Capture | NotRequested、Reserved、Requested、Capturing、Ended、MediaTaken、ContentUnavailable、Failed、Unknown | Ended和MediaTaken须分别有证据；Failed/Unknown设备问题受限；ContentUnavailable仅可靠结束时可异常继续 |
| Algorithm | NotRequested、Queued、Running、Success、Error、TimedOut、NotConfigured、NotIntegrated、NotReady、NoResult、InvalidResult、DependencyFailed、Cancelled | Queued/Running外为明确调用终态；NotRequested仍未执行；Success还需有效性检查 |
| Save | NotQueued、Queued、Writing、Committed、Failed、CommitUnknown、ConditionRejected | 只有Committed可作为保存门；迟到提交回执可补事实，不能自动恢复Run |
| Handoff | NotReady、Saving、Ready、ReadyWithLimitations | Ready仅表示本阶段可读取，不表示已匹配/后续已启动 |

辅助裁决字段terminalResolution=None/Pending/CommitUnknown/Resolved；finalOutcome来自持久TerminalOutcome。取消命令Applied=null表示未裁决，true仅持久Cancelled，false表示NotApplied(AlreadyCompleted)。requestAccepted/admissionClosed/stopState另列，不将停止受理、Stopped或内存cancelRequested当最终取消；未裁决Handoff不得Ready。Algorithm的Queued覆盖意图保存及调度等待，IntentCommitState和DispatchEvidence另列；同一原期限贯穿保存到响应。

固定业务维度：RecipeState=Unmatched、QualityState=NotEvaluated、SortingState=NotStarted、WholeTaskState=NotCompleted。步骤完整性独立为NotStarted/InProgress/ValidEnd/AllowedExceptionEnd/Blocked，不能把动作未知记为AllowedExceptionEnd。

## 4. 运行转换表

| 当前阶段/状态 | 事件与条件 | 转换/保存 | 后继许可 |
| --- | --- | --- | --- |
| 无Run | 合法入口、授权、幂等与设备运行占用准入 | Created，保存上下文；若存储失效仅内存诊断且标未提交 | 尚不发PLC |
| Created/Preparing | 公共运动/采集配置任一非法，包括F | ConfigurationBlocked，保存校验结果 | 禁止启动；不因算法缺项进入此状态 |
| Preparing | 必需配置合法，快照已提交，设备/存储/恢复满足 | 保存Start及夹紧观察意图→WaitingStartAcceptance→投递Start | 等ACK/安全/实体输入，不能直接运动 |
| WaitingStartAcceptance/WaitingSafety | 新鲜匹配受理与安全证据 | WaitingPhysicalStart | 按钮尚未发生则业务等待，处理查询/取消 |
| WaitingPhysicalStart | 匹配本次按钮/PLC夹紧开始证据 | WaitingClamp，登记夹紧期限 | 等完成，不由入口模拟按钮 |
| WaitingClamp | 匹配可靠夹紧且已保存 | Running3D，保存3D移动意图后派发 | 等3D固定XY到位 |
| Running3D | 到位事实已保存且采集准入 | 采集→可靠结束/媒体接管→文件及元数据保存→登记原Call期限/AlgorithmIntent提交→高度派发 | 不新增Z或再次采集 |
| Running3D | 本次完整3D观察已保存 | 有效空盘或人工介入走共享下料；正常或异常继续才RunningF | 无效观察不得判空盘；复查不重绑F |
| RunningF | F到位保存→单拍完成及媒体保存→登记原Call期限/AlgorithmIntent提交后读码→解析终态保存 | SavingHandoff | 有异常可进入；设备未知/保存未确认不进入 |
| SavingHandoff | 完成门满足，Run.Revision/TerminalOutcome=None条件事务Committed且Run/Handoff一致 | Completed或CompletedWithExceptions；Handoff Ready/ReadyWithLimitations | 只可查询，物理占用仍保留 |
| 任一非终态 | PLC断联/互锁/运动或采集未知 | Blocked/RecoveryRequired，保留Held资源及证据 | 禁止依赖动作；算法事件可保存 |
| 任一非终态 | 必要保存Failed/CommitUnknown | Blocked，关闭依赖准入 | 不重做物理动作补记录 |
| 任一活动阶段 | pause | PauseRequested；停新步骤、处理在途事实/期限及所需停止 | 安全静止且必要保存后Paused |
| 任一非终态观察状态（含SavingHandoff） | cancel | 立即关准入并锁存请求，CancelRequested/StopPending；terminalResolution=Pending/CommitUnknown；停止与裁决分开 | 停止/保存满足后条件提交取消；完成先提交则保留完成、取消NotApplied；未知先核对WriteId，不允许continue |
| 终态候选待裁决 | ConditionRejected/回执迟到/重启核对 | 按持久Run/Handoff/WriteId确认唯一终态；版本仅变化时重新核对条件 | 不以旧回执覆盖终态，不自动发动作或释放物理占用 |
| Paused | 未发生故障、无未决取消，核对物料/快照/安全/保存并保存Check | 正常暂停Continue许可 | 故障Blocked/RecoveryRequired不产生Continue许可，转双端复位后新轮 |
| 已核对的正常Paused | 无故障/取消且显式continue及revision/观察仍适用 | 同run复用完成保存步骤，执行未开展步骤 | 故障新轮必须重新公共准备，不适用本行 |
| 重启/重连 | 读取未完成运行/意图 | RecoveryRequired；旧单调期限不复用，调用按Interrupted有限终结 | 不自动连接恢复后派动作 |
| 已Cancelled/Completed | 任何重放、迟到、恢复请求 | 保留原终态，追加诊断或拒绝 | 不复活、不下一工位 |

有效动作/采集/算法终态及保存回执都保留原Operation/Attempt。迟到物理完成可经核对补存完成依据，但原超时事件不删除；正常暂停核对后已有可靠完成及必要媒体/结果可在原run复用；故障只补存原事实，不产生旧步骤续跑资格，新轮重新公共准备。

## 5. 保存与完成门

本阶段Completed要求：合法冻结快照；Start/实体启动/夹紧及两次XY可靠完成；3D、F采集已按规则结束；两个算法有效或允许异常终态；必要媒体/结果/动作/恢复记录已保存；无未知物理依赖和未解除安全阻断；Run终态None/revision条件成立且稳定移交事务已提交；取消请求到达本身不是最终取消，竞争按保存合同§1.2裁决。算法迟到窗口不是完成门，物理资源未卸载也不等于本阶段失败，但禁止新盘重入。

详见[保存与移交合同](contracts/persistence-handoff.md)及[关键时序](sequences.md)。数据库、文件及设备不组成原子事务；软件不可恢复的内存帧必须如实标丢失，不承诺断电后凭记录重造原图。


## 6. 修订记录

- 2026-09-20，1.0.1：H01补齐持久调用依据及派发证据；H02补齐持久终态/过渡裁决、条件回执、Run与Handoff原子一致性（当时基于spec1.1.0）。未生成迁移或执行数据库操作。
- 2026-09-22，1.2.0：增加前端联调所需的命令回执、状态快照、版本化通知、受控媒体、测试身份和统一错误投影；不改变领域状态或002/003数据边界。

## 2026-09-24 008完整执行模型增量

旧模型保留其历史范围。最新完整执行扩展以[008数据模型](../008-recipe-driven-inspection/data-model.md)为准：真实物理槽/源目标、面/成员/组/整体结果、动作/高度轮次、目标预留、来源及冻结预算；旧占位坐标/采集对象不得当作真实搬运实体。沿用已有意图/事实短事务，不回填旧记录。相关实现任务见本功能008对齐增量，当前仅设计。

## USR-20260926-D运行与复位关联

复用现有Run/Command/Write/事件事实，增加必要faultRestart查询投影：faultRunId、resetId、initialCheckId、newRunId、expectedFaultRevision、committedRevision、状态/Blocked项；逐项初始观察携epoch/generation/时间及版本来源。旧轮fault outcome与媒体保留，executionClosed独立于Final；reset/check不能制造成功。新request幂等并单次消费reset/check，关联与新run身份通过现有单写短事务持久，必要模型升级仅StorePrep执行。新run创建独立操作/采集/算法/媒体/预算身份，配置版本可同但事实不复用。完整字段及状态边界见[003合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)。正常暂停/人工面确认仍属原run的独立操作类型，不复用故障RestartFullRun资格。
