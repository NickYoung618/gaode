# 第一工位完整主流程合同

## 起点与终点

- 起点：Host 持久受理 `POST /api/v1/station01/runs`，创建并冻结 WorkflowIdentity 和运行配置。
- 终点：同一 run/tray 已提交 `FinalUnloadCompletion`。
- `202 Accepted`、HandoffReady、三阶段完成、WholeTrayCompletion 或 ObservedUnlocked 均不是终点。

## 强制顺序

`Start accepted → clamp → 3D → F → RecipeRunPlan/build+bind → committed handoff → Detection → Sorting → UnloadPreparation → WholeTrayCompletion → unlock request/ObservedUnlocked → manual removal confirmation → FinalUnloadCompletion`

所有步骤使用同一 `runId/trayId` 和冻结配置/计划。007固定图片的正式采集媒体、独立worker每项实际请求/响应、配方快照、PLC动作/反馈、阶段事件及SQLite事实均沿此身份关联；handoff 提交后由同一 Host 编排器自动续接，不允许第二次启动、客户端手工拼装计划、直接跳到 Detection 或修改前端状态推进。

## 阶段规则

- Detection 只调用 `IDetectionPort`。007 Test接线须按冻结计划逐必检采集，并为每项检测采集经正式端口派发一次独立worker算法调用，实际结果及媒体落地；算法依赖有限尝试后把受影响对象标记 Pending 并保留错误，完整映射后继续。身份/位置/目标歧义为 MappingFailed 并暂停。
- Sorting 只消费已提交 Detection 结果和冻结计划。普通OK无需搬运不生成动作；NG/Pending按本盘实际源/目标完成两段取放及ACK，特殊已处置实体不重复。
- UnloadPreparation 只使用正式 PLC action port 和已有协议点位。
- 按新顺序的三个阶段完成持久化引用由独立聚合事务生成 WholeTrayCompletion 和 ReadyForUnlock 组件来源矩阵。
- UnlockObservation 只接受持久化 WholeTrayCompletionReference；物理结果未知进入 UnknownHeld 且不自动重发。
- ManualTrayRemovalConfirmation 只在已提交 ObservedUnlocked 后由 Host 受控 API 受理，并与 FinalUnloadCompletion 原子提交。007联调客户端可在启动时启用监视，条件满足后自动调用唯一正式确认路由；此动作来源为Test/Simulated，不代表真实人工取盘或006前端控件完成。

## 状态与恢复

阶段公开状态来自持久化投影。临时通信错误最多 4 次总尝试并按 1/2/4 秒退避；算法超时最多 3 次总尝试并按 2/5 秒退避；二者共用阶段开始冻结的 按冻结RecipeExecutionBudget确定的 deadline 且不得重置。Detection 失败收敛为 Pending，不使用 UnknownHeld；UnknownHeld 只用于 PLC 物理动作可能已派发且事实不确定，此时不进入通信重试。重启从最后已提交边界恢复，已完成动作不重放，在途无终态的物理动作不自动执行。

旧任务重新检测或报废必须先有包含 actor、时间、原因、原任务和证据引用的受控人工决定；重启、重连或普通复位不得自动生成决定或续跑。

## 完成判定

成功必须同时满足：同一身份链完整；所有占用对象均有 OK/NG/Pending 结果并完成唯一映射；Sorting 与 UnloadPreparation 有已提交完成事实；WholeTrayCompletion 和 ReadyForUnlockSourceMatrix 已提交；解锁有 ObservedUnlocked；人工确认引用一致；包含 Host/PLC/Camera/Light/Algorithm/ManualActor 的 FinalSourceMatrix 可核验；FinalUnloadCompletion 已提交。任何 Virtual/Simulated/Test 来源都使证据明确标为 `SoftwareLoopOnly`，不能作为真机或生产验收。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

保留Detection→Unload→Sorting→WholeTrayCompletion→ObservedUnlocked→适用取盘确认→FinalUnloadCompletion。第一条Q01就跑完这条尾段；Detection须全部应检目标/面及适用成员/组/整体结果收敛，Sorting只对实际应搬运实体执行，特殊出口不重复。

普通OK可留原位，S3按整体搬运；未知在途件或必要保存未提交时不解锁/Final；适用分拣完成后下料定位但仍须本次运动安全条件。预算依完整计划，不能沿用旧120秒。取盘确认经正式006同页控件并提交，外部客户端旧证据仅作历史；页面完成须对应Final持久记录。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

下料使用本次用途的合法配置及可靠轴反馈。普通盘末Detection→适用Sorting及必要保存→UnloadPreparation→允许取盘/人工取盘→Final；到位、允许取盘、人工确认和保存是独立事实。不能伪造新协议未定义的旧锁紧/解锁信号，地址/恢复/安全延期不猜值。

Stage事件/恢复当前阶段/排序、绝对期限、WholeTrayCompletionStore来源索引和allowedActions同批适配；ReadyForUnlock仍核对全部适用提交。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

普通路线依实际顺序Detection→UnloadPreparation→适用Sorting→WholeTray真实保存→可靠ObservedUnlocked→人工确认与Final真实提交。WholeTray引用必须在解锁前重新核验；单一动作Completed、传输回执或解锁均不等于Final。取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。
