# 第一工位完整主流程数据模型

## 1. WorkflowIdentity 与 StartRunContext

`WorkflowIdentity` 在唯一启动入口持久化并贯穿全部记录：

- `RunId`、`TrayId`、`StationId`、`LineId`
- `RequestId`、`ScenarioId`、`OccupiedSlots`
- `StartedAt`、`ActorId`、`Purpose`（Production/Test/Commissioning）
- `PublicConfigRevision`、`BudgetRevision`、`SimulationConfigRevision`

现有 `ContextJson` 使用版本化 `StartRunContext` schema 输入这些字段。`TrayId` 是运行身份，F 扫出的业务码另存并绑定到该身份；003 不得临时生成或替换身份。

## 2. Station01RunProjection

运行投影由已提交事件计算，至少包含：

- `RunState`：Accepted、PublicPreparation、HandoffReady、Detection、Sorting、UnloadPreparation、ReadyForUnlock、ObservedUnlocked、AwaitingManualRemoval、FinalUnloadCompleted、Restricted
- `CurrentStage`、`RestrictionReason`、`LastErrorCode`
- `HandoffRevision`、`PlanRevision`、`PersistedRevision`、`LastEventId`
- `DetectionSummary`（OK/NG/Pending 数量）、`SortingState`、`WholeTaskState`
- `WholeTrayCompletionId`、`ObservedUnlockedEventId`、`FinalUnloadCompletionId`
- `SourceMatrixRef`、`SourceMatrixDigest`、`SourceKinds[]`
- `ContainsNonProductionEvidence`、`BlockedComponents[]`

`HandoffReady`、`WholeTrayCompletion` 和 `ObservedUnlocked` 均不是最终 Completed；只有已提交 `FinalUnloadCompletion` 将运行投影为 `FinalUnloadCompleted`。

## 3. PublicPreparationHandoff

不可变 handoff 是 001→003 的唯一续接边界：

- WorkflowIdentity 全字段
- 冻结公共配置、点位、budget、用途和能力注册摘要
- 3D/F 媒体引用、算法结果引用和 F 唯一码
- `RecipeRunPlanRef`、`PlanRevision`、计划摘要及 PLC 绑定证据
- `Source`、`Quality`、`WriteId`、`Revision`、`PersistedAt`

只有已提交且与当前租约、身份、快照和计划版本一致的 handoff 可以构造 Detection 请求。通知或内存对象不能替代它。

## 4. 阶段与 PLC 动作枚举

- `WholeTrayWorkflowStage`：Detection、Sorting、UnloadPreparation、UnlockObservation、ManualTrayRemovalConfirmation
- `PlcWorkflowStage`：Sorting、UnloadPreparation、UnlockObservation

两组枚举不可互换。Detection 不属于 PLC 动作；001 的夹紧和 3D/F 运动继续使用现有正式运动/PLC 端口。

## 5. DetectionObjectOutcome

每个占用对象均保存：

- `ObjectId`、`Slot/Position`、`RunId`、`TrayId`、`PlanRevision`
- `Classification`：OK、NG、Pending
- `TargetZone` 与映射摘要
- `ResultReference`、`InputMediaReferences`
- `AttemptCount`、`ErrorCode`、`RawErrorReference`
- `Source`、`Quality`、`ObservedAt`

算法失败、超时、未配置、未接入或无有效结果在有限尝试后必须形成 Pending 并保留原异常。缺失、重复或歧义的对象身份/位置/目标不生成猜测结果，而形成 Detection 阶段的 `MappingFailed`。

## 6. WholeTrayStageEvent 与 StageProjection

事件追加式保存，字段至少为：

- `EventId`、WorkflowIdentity、`Stage`
- `EventType`：IntentRecorded、Started、Accepted、Executing、AttemptFailed、RetryScheduled、PendingRecorded、Completed、Failed、TimedOut、Disconnected、MappingFailed、ManualReviewRequested、ManualReviewConfirmed、RecoveryResumed、WholeTrayCompleted、UnlockRequested、ObservedUnlocked、ManualTrayRemovalConfirmed、FinalUnloadCompleted
- `OperationId`、`Attempt`、`ConnectionEpoch`
- `OccurredAt`、`PersistedAt`、`Source`、`Quality`
- `ErrorCode`、`PayloadDigest`、`PlanRevision`
- `StageStartedAt`、不可重置的 `StageDeadlineAt`

`StageProjection` 由合法事件序列计算，包含状态、最后终态、revision、最后事件和未解决限制。Detection 通信/算法异常不能投影为 `UnknownHeld`；`UnknownHeld` 仅用于可能已派发但物理事实未知的 PLC 阶段。

## 7. ComponentEvidenceMatrix

组件来源矩阵是不可变事实，不允许把混合来源压缩成单一 `Real`。矩阵至少包含：

- `MatrixId`、`SchemaVersion`、WorkflowIdentity、`Milestone`、`CreatedAt`、`MatrixDigest`
- `EvidenceScope`：`SoftwareLoopOnly` 或 `ProductionCandidateNotAccepted`
- `ContainsNonProductionEvidence`、`SourceKinds[]`、`BlockedComponents[]`
- 固定组件：`Host`、`PLC`、`Camera`、`Light`、`Algorithm`、`ManualActor`

每个 `ComponentEvidence` 包含：

- `Component`、`RequiredAt`（ReadyForUnlock/FinalUnloadCompletion）
- `EvidenceState`：Verified、NotYetRequired、Missing、Unknown、Unverifiable
- `Source`：Real、Virtual、Simulated、Test、AuthenticatedHuman 之一
- `Quality`、`VersionRef`、`EvidenceReferences[]`、`CapturedAt`、`Digest`

当前里程碑必需组件必须为 Verified，且 source、quality、versionRef、evidenceReferences 完整可核验；Missing、Unknown 或 Unverifiable 阻止对应完成。任一组件为 Virtual、Simulated 或 Test 时，矩阵必须派生为 `SoftwareLoopOnly`。全部为 Real 也只能是 `ProductionCandidateNotAccepted`，不能由该矩阵自动声明生产验收。

在 ReadyForUnlock 里程碑，Host/PLC/Camera/Light/Algorithm 为必需；ManualActor 槽必须存在但标记 `RequiredAt=FinalUnloadCompletion`、`EvidenceState=NotYetRequired`，其尚不存在的 actor 来源与引用保持空值，不得伪造。NotYetRequired 不是当前里程碑的缺失证据。

## 8. WholeTrayCompletion

不可变聚合至少包含：

- `WholeTrayCompletionId` 与 WorkflowIdentity
- `DetectionCompletedEventId`、`SortingCompletedEventId`、`UnloadPreparationCompletedEventId`
- `RecipeRunPlanRef`、`PlanRevision`
- 对象结果/分拣摘要及 digest
- 不可变 `ReadyForUnlockSourceMatrix` 及其 digest
- `CreatedAt`、`PersistedRevision`

创建条件：三条完成事件均已提交；身份和计划版本一致；无未解决 MappingFailed/UnknownHeld；ReadyForUnlock 当前必需的五类组件均 Verified。Simulated/Test 可以生成 `SoftwareLoopOnly` 完成，但不得标记为 Real/Production；Unknown、Missing、Unverifiable 或证据引用不可核验时不得创建。

## 9. PalletUnlockEvidence

解锁证据包含：

- `WholeTrayCompletionId` 和三阶段事件引用
- `UnlockOperationId`、`ConnectionEpoch`
- `CommandAcceptedEventId`、`ObservedUnlockedEventId`
- `ObservedAt`、`Source`、`Quality`

解锁必须重新查询并核验持久化完成引用。裸 `bool` 或任意非空 ID 无授权意义。

## 10. ManualTrayRemovalConfirmation 与 FinalUnloadCompletion

受控命令包含：

- `RequestId`、`ExpectedRevision`
- `RunId`、`TrayId`
- `WholeTrayCompletionId`、`ObservedUnlockedEventId`
- `ConfirmedAt`、`Result`、`Reason`
- 来自认证上下文的 `ActorId/ActorRole`

前置引用与当前投影不一致或解锁未观察到时拒绝。成功时在同一短事务写 `ManualTrayRemovalConfirmed` 和不可变 `FinalUnloadCompletion`；重复相同 request/payload 返回原结果，不同 payload 冲突。

`FinalUnloadCompletion` 还必须保存新的不可变 `FinalSourceMatrix`：引用 `ReadyForUnlockSourceMatrix`，复制并核验既有五类组件，加入实际 `ManualActor` 证据。人工证据至少包含认证 actor/role、确认事件、调用渠道版本、requestId 和证据引用；联调客户端渠道标记为 Test/Commissioning，因此本期 E2E 的最终矩阵为 `SoftwareLoopOnly`。原 ReadyForUnlock 矩阵不得回写。

## 11. ControlledRecoveryDecision

旧任务重新检测或报废前必须保存：

- `DecisionId`、`RequestId`、`ExpectedRevision`
- `OriginalTaskId`、`RunId`、`TrayId`、原 operation/stage
- `Decision`：ReDetect 或 Scrap
- 来自认证上下文的 `ActorId/ActorRole`
- `DecidedAt`、`Reason`、`EvidenceReferences[]`

该记录只授权后续应用用例，不能直接生成算法、PLC 或完成事实。Host 重启、重连或复位不得自动创建此记录。

## 12. NotificationProjection

通知信封仅描述已提交变化：

- `EventId`、`RunId`、`TrayId`、`PersistedRevision`
- `Type`、`ChangedFields`、`OccurredAt`
- `SourceMatrixRef`、`SourceMatrixDigest`、`SourceKinds[]`
- `ContainsNonProductionEvidence`、`BlockedComponents[]`

通知不是业务事实。客户端收到重复、乱序或跨 revision 通知后按 `RunId/PersistedRevision` GET 最新快照。

## 13. 原子提交与恢复

1. 动作意图、operation/attempt/epoch 和对应投影在设备调用前同一短事务提交。
2. 反馈事件和阶段投影在设备调用后同一短事务提交。
3. 三阶段完成后，另一个短聚合事务重新核验全部引用并原子写 `WholeTrayCompleted`、`WholeTrayCompletion`、`ReadyForUnlockSourceMatrix` 和整托投影。
4. 人工确认事件、`FinalSourceMatrix` 和 `FinalUnloadCompletion` 在同一短事务提交。
5. 不得跨 PLC、相机或算法调用持有事务。保存失败不推进内存/公开状态。
6. 阶段开始时冻结 `StageDeadlineAt = StageStartedAt + 冻结阶段预算`。临时通信错误最多 4 次总尝试并按 1/2/4 秒退避；算法超时最多 3 次总尝试并按 2/5 秒退避；重试、退避或重启不得重置 deadline。
7. 重启只重放已提交事件。已 Completed 的阶段/动作不重放；可能已派发而无可信终态的 PLC 动作恢复为 `UnknownHeld`。完成聚合可幂等重建，但不得因此重发设备动作。
8. 旧任务 ReDetect/Scrap 无 `ControlledRecoveryDecision` 时必须拒绝；普通恢复不得自动重检、报废或续跑。
9. 阶段事件、完成和最终完成证据至少保留 7 年，期限只能延长。

## 14. 隔离与来源规则

所有幂等键、查询和写入均至少以 `RunId/TrayId/Stage/OperationId` 隔离，并校验 station/line/plan revision。真实、Virtual、Simulated/Test 的组件证据逐事件保存并汇总，禁止后写固定值覆盖或用单一来源摘要替代矩阵。VirtualPlc 不创建业务对象；Simulated 算法不直接推进 PLC 或数据库状态。

## 2026-09-24 008完整执行模型增量

旧模型保留其历史范围。最新完整执行扩展以[008数据模型](../008-recipe-driven-inspection/data-model.md)为准：真实物理槽/源目标、面/成员/组/整体结果、动作/高度轮次、目标预留、来源及冻结预算；旧占位坐标/采集对象不得当作真实搬运实体。沿用已有意图/事实短事务，不回填旧记录。相关实现任务见本功能008对齐增量，当前仅设计。

## USR-20260926-D运行与复位关联

复用现有Run/Command/Write/事件事实，增加必要faultRestart查询投影：faultRunId、resetId、initialCheckId、newRunId、expectedFaultRevision、committedRevision、状态/Blocked项；逐项初始观察携epoch/generation/时间及版本来源。旧轮fault outcome与媒体保留，executionClosed独立于Final；reset/check不能制造成功。新request幂等并单次消费reset/check，关联与新run身份通过现有单写短事务持久，必要模型升级仅StorePrep执行。新run创建独立操作/采集/算法/媒体/预算身份，配置版本可同但事实不复用。完整字段及状态边界见[003合同](contracts/recovery-test-execution.md)。正常暂停/人工面确认仍属原run的独立操作类型，不复用故障RestartFullRun资格。

## USR-E动作证据最小增量

沿用现有动作/事件持久结构，增加完整targetXYZ与observedXYZ、axisRole、samplePhase/time、connectionEpoch、mapping/configDigest、tolerance及validationOutcome，未知不补目标；动作关联沿既有run/operation/action/step/entity/face，设备本机actionSequence只作诊断引用。批量写/同值写引用完整寄存器范围及rawWords，不从变化日志反推缺漏。字段语义唯一见[动作与诊断合同](contracts/virtual-plc-boundary.md#usr-e动作级诊断增量)。优先事件负载/现有查询，不预建新表；必要结构升级走StorePrep，不由Host静默迁移。USR-D新旧run/epoch隔离保持。
