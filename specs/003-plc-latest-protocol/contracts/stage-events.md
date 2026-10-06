# 阶段事件、投影与完成证据合同

## 事件与投影

`WholeTrayWorkflowStage` 固定为 Detection、Sorting、UnloadPreparation、UnlockObservation、ManualTrayRemovalConfirmation；`PlcWorkflowStage` 仅用于 PLC 请求。

事件至少支持：IntentRecorded、Started、Accepted、Executing、AttemptFailed、RetryScheduled、PendingRecorded、Completed、Failed、TimedOut、Disconnected、MappingFailed、ManualReviewRequested、ManualReviewConfirmed、RecoveryResumed、WholeTrayCompleted、UnlockRequested、ObservedUnlocked、ManualTrayRemovalConfirmed、FinalUnloadCompleted。

每条事件保存 WorkflowIdentity、stage、operationId、attempt、connectionEpoch、planRevision、stageStartedAt、不可重置的 stageDeadlineAt、occurred/persisted time、组件 source/quality/evidence reference、errorCode 和 payload digest。投影只能由合法已提交序列计算；保存失败不得更新内存/公开状态。

## 事务边界

1. 动作意图与对应投影在设备调用前用一个短事务提交。
2. 反馈事件与阶段投影在设备调用后用一个短事务提交。
3. 三阶段完成事件均已提交后，另起聚合短事务重新核验身份、计划、组件来源矩阵和未解决终态，并原子追加 `WholeTrayCompleted`、写 `WholeTrayCompletion`、`ReadyForUnlockSourceMatrix` 与整托投影。
4. `ManualTrayRemovalConfirmed`、`FinalSourceMatrix` 与 `FinalUnloadCompletion` 在同一个后续短事务原子提交。
5. 不得跨 PLC/算法调用持有数据库事务。

## WholeTrayCompletion

不可变记录必须引用 Detection、Sorting、UnloadPreparation 的 Completed 事件、同一 WorkflowIdentity、plan revision 和 [组件来源矩阵](./component-source-matrix.md)。任一引用未提交、身份不一致、存在 MappingFailed/UnknownHeld，或当前必需组件为 Missing/Unknown/Unverifiable 时不得创建。

ReadyForUnlock 矩阵要求 Host/PLC/Camera/Light/Algorithm 均 Verified；ManualActor 槽为 `NotYetRequired`，不能伪造未来人工证据。明确有效且标记为 Virtual/Simulated/Test 的完整证据允许生成 `SoftwareLoopOnly` 的 `WholeTrayCompletion`，但不得标记为 Real/Production。其状态仅为 `ReadyForUnlock`，不是完整运行 Completed。

## 解锁与最终完成

解锁动作必须重新查询并核验 `WholeTrayCompletionReference`。正式反馈形成 `ObservedUnlocked` 后，Host 才能受理同 run/tray 的人工移除确认。确认和最终完成共同引用同一 WholeTrayCompletion 与 ObservedUnlocked；同一事务生成包含认证 ManualActor 的 FinalSourceMatrix。只有六类组件在各自里程碑均可核验且 `FinalUnloadCompletion` 已提交，第一工位主流程才完成。原 ReadyForUnlock 矩阵不可回写。

人工核对/确认只能追加新事实或授权恢复，不能把未完成阶段直接改写成完成，不能跳过解锁读回。

## 重启、幂等与保留

重启只从已提交事件恢复。已 Completed 阶段及物理动作不重放；可能已派发且没有可信终态的 PLC 动作恢复为 `UnknownHeld`，连接恢复或 epoch 变化不得自动重发。临时通信错误最多 4 次总尝试、退避 1/2/4 秒；算法超时最多 3 次总尝试、退避 2/5 秒；二者共用阶段开始冻结的 按冻结RecipeExecutionBudget确定的 deadline 且不得重置。仅当前活动任务的 Detection 无物理副作用尝试可在原 operation/deadline 内恢复，超限形成 Pending。

旧任务 ReDetect/Scrap 必须先提交受控人工决定，记录认证 actor、decidedAt、reason、originalTask/run、evidenceReferences、requestId 和 expectedRevision。Host 重启、重连或普通复位不得自动重检、自动报废、自动续跑或创建该决定；人工决定也不能直接制造完成事实。

聚合完成可按相同引用幂等重建，但不重发动作。相同幂等键与相同 payload 返回既有结果，不同 payload 冲突。全部阶段事件、完成和最终完成证据至少保留 7 年。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

新事实按适用情况关联scenarioId/route、recipe版本、planRevision、stepId/operationId、entity/part/region/group/assembly/face、heightRound、physicalSlotIndex、source/targetPointRef、reservationRef及结果层级。名称以008数据/公开模型为准，旧字段只保留原记录范围。

意图先提交，可靠反馈再提交；F 3/4/0、扫码Z复位、选择与实际绑定都可追溯。阶段deadline来自冻结计划，重试不重置；算法恢复不重放未知物理动作。新层级/位置/人工确认追加事实，不回填历史。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

新事实采用device-semantics/1，保存实际语义/关联/来源/质量/观察及opaque引用，禁止硬写protocolStatus=2或Virtual/Derived来源。旧PayloadJson字节不重写；未知原始来源不补造。业务实际commit与调用方有效回执分别记录；查到行不能改变超期动作资格。原事件/幂等/投影同短事务、WholeTray及Final独立提交不变。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。


### 009 T040/T041 有限独立进程保存故障接线（2026-10-02）

按009 VG V07/V09，在真实Host独立进程中增加可选`Gaode:TestPersistenceFaultCase`，仅在`VirtualPlcIntegration`、Virtual PLC及全部Test配置下接受。固定值为F05-A/B/C、F06-A/B/C、BA04-late-bound/late-handoff；未知值或其他运行环境启动拒绝，不新增业务API。未配置时不安装任何拦截器。Test根中的`009-fault-arm.json`以caseId、runId和nonce选择本次真实运行；独立编排取得正式启动runId后写入，未命中不能当故障验证通过。唯一命中记录实际EventId/WriteId/EvidenceId、时刻、位置和commit事实到同根`009-fault-events.jsonl`。

A通过实际SQLite触发器拒绝相应插入，原Store负责事务回滚与结果；B仅在真实commit完成后扣住回执；C在同一实际边界另持SQLite独占锁暂阻核查。通信raw故障仍经TraceWriter实际job，取料业务事务仍由StageEventStore承担。BA04严格选择本run的RecipePlanBound或本次handoff，不拦其他保存。Test专用保持窗口最多30秒，或收到匹配nonce的`009-fault-release.json`结束；这个注入外限不是业务期限，保持期间原预算/保存期限照常失效，释放后不能复活动作。

C使用受控Test副本的DELETE日志模式取得真实排他锁；A/B不伪造数据库结果，记录存储不可用时仍仅尽力写诊断文件。009验收必须另查实际SQLite、当前回执、原截止、PLC全部后继写和保守占用；故障日志及进程存活不能独自证明验收。此处只完成必要接线接口对齐；代码及实际运行由009任务证据确认。实施/复核角色为Codex，不冒称客户批准，其他功能历史任务状态不变。
