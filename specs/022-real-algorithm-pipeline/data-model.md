# 022数据与状态模型

日期：2026-10-09。下列名称是本功能拟定合同模型，不表示已有类型。最小扩展现有Writes/AlgorithmCalls/StageEvents及媒体元数据；不建立独立任务数据库或自动重放引擎。

| 对象 | 必要字段及关系 | 校验与保存 |
| --- | --- | --- |
| RunAlgorithmSnapshot | RunId/TrayId/SessionId/SnapshotId/PlanRevision；公共及产品各自冻结点；ProviderId/实现身份/能力合同版本/模型参数版本摘要/转换与标定版本；C/Q/媒体额度/预算版本 | 引用已保存配置；真实版本应用证据另存，不能只信字段 |
| CaptureCompletionEvidence | 窗口SessionId/WindowOperationId、CaptureOperationId/CaptureId、StepSequence、连接代次、Ended/MediaTaken、关联实际帧/设置、Media及CaptureFact有效提交引用、采集资源释放与文件接管引用 | 各操作分别关联；只证明可发起窗口结束，不能证明已结束 |
| PipelineTask | Run/Tray/Session/Call/Operation/Attempt、对象/成员/整体、StageId/LocalFace/CoordinateEpoch、角色/逐输入Capture/Media/Camera、快照引用、IntentWriteId、原StartTick/DueTick/ClockId及UTC截止、排队/派发/终态/回收证据 | 主键CallId；完整键不可换绑；单图一个CaptureId，融合各输入独立，不强迫两图共CaptureId |
| InputOwnership | Run/Call或融合键、MediaId、consumer kind（生产交接/排队/执行/配图/融合）、取得/移交/释放证据、会话、未知原因 | 引用计数是运行机制，持久登记是恢复依据；双方交接不出现零持有空档 |
| FusionSet | Run/Object/StageId/LocalFace/CoordinateEpoch/AB或CD、两个输入身份、必需单图事实引用、期限与调度状态 | 键完整匹配；未齐不占执行槽；至多一个有效融合终态 |
| PhysicalObjectDecision | Run/PlanRevision/PhysicalObjectId/Slot、必检键集合摘要、逐项结果/融合/E或依法跳过证据、最终质量及技术完整性、观察/来源/提交引用 | 由冻结计划完整集合推导，不能只凭NG或条目存在；组、成员、装配实体不可混用 |
| PipelineCompletion | 生产结束证据、各对象判定保存、任务业务终态/执行结束/全部消费者释放、最终Detection结果引用 | 生产结束不等于Completed；Final仍沿既有设备/人工链 |
| SortingSafetyEvidence | 全量冻结目标/抓手、源占用/参与与观察、候选目标冲突及容量证明、原序及累计已预留/占用引用 | 先于本批第一分拣动作，当前对象另持完整判定和唯一正式预约 |
| AlgorithmResourceProjection | Run/Call/WorkerSession/Consumer；ResourceLastEventId/ResourceUpdatedAt/重放游标；业务与资源状态分轨 | 未回收查询不按Run终态过滤；StageProjection业务四字段及UpdatedAt保原值，事件流Revision独立推进，见C022-DATA I1/G1 |
| ReleaseObservation | Run/Call/原会话/ClockId、TriggerEventId/原因、StartTick/DueTick及UTC映射、冻结释放预算引用、Expired/尚未知条件 | 每Call首个触发一次；公共WorkerReleaseGrace/产品InputReleaseWaitMs；Host退出截止另记，不重开旧Call，见C022-PIPE U2 |
| MediaQuotaOwnership | 逐帧MemoryReservation；批次/唯一Media工作保留预约及消费者；DiskWriteReservation与实际文件字节 | 三类分别取得/转移/回收，工作引用结束不减实际磁盘；沿既有媒体组件，不新增表平台，见C022-PIPE U1 |

任务业务状态：Prepared → IntentCommitted/Queued → DispatchEntered/Accepted/Running → ResultCommitted或Failed/TimedOut/Cancelled终态。Prepared只是本地预约，不代表已提交；Accepted不代表结果。有效结果先通过原截止与身份仲裁，再保存，保存不确认则不形成可消费的判定。

资源状态另轨：LocalHeld → AdapterMayOwn → InputsReleased/ExecutionEnded/DispatchReturned/CancelCallbacksEnded → Reclaimed。InputReleased可逐输入累计，聚合只在本调用全部输入可信释放后成立；回收不得由业务终态直接推导。已证明NotDispatched且未转移输入的本地取消可收回本地租约；普通异常保Unknown。

运行完成后不再追加必要在途业务事实，故结果、释放、收尾先排空再提交Final。异常/Host重启保任务与所有权投影供核对，不自动重发、不宣称恢复推理能力。跨会话单调时钟不可直接比较：持久ClockId/UTC截止用于诊断和原限额核对，重启仍按既有人工/受控恢复规则，禁止重开完整预算。

存储增量使用版本化payload（拟定pipeline/1，限本项目内部），AlgorithmCalls投影补任务/输入/快照及回收关联；Writes保原意图/算法技术事实，StageEvents的AlgorithmLifecycleRecorded保任务与资源生命周期权威事实且不改变原阶段投影状态。异常Run终态之后资源释放仍可追加StageEvents，不能借Writes失败丢弃监管记录。对象判定和生产结束存为阶段事实，真Completed仍独立。必要输入消费者投影与旧表增量见[C022-DATA](contracts/persistence-lifecycle.md)。旧JSON缺字段是历史缺项，不能反向补“已释放/真实来源”。迁移经受控入口，Host不自动改表；不改媒体原始格式。


## A阶段PNG/PLY及Host配置增量

RealAlgorithmHostSnapshot：schema/id/version/purpose/source，原配置JSON/文件摘要，commissioning/public/budget引用，provider与逐module能力/model/parameter FileRef，布局/标定/输入格式；字段类型/缺失规则以C022-ALG R2为准。公共与产品原冻结点分别保存适用绑定、旧版本实例/文件持续受管；Loaded身份与Applied证据分开。

DerivedAlgorithmInput：RawMediaId→DerivedMediaId，保持同CaptureId及Run/对象/面/阶段/轮次，双长度/摘要、实际raw元数据、转换版本/参数摘要、PNG位深颜色或PLY编码/布局及可用保存引用。转换未完成/保存未知不得成为算法可用输入。租约含转换读原媒体、实际派发产物和待融合独立消费者，工作额度结束不减持久磁盘。

A保原同步Detection整体完成，不产生C的ProductionEnded；I1业务/资源事件隔离、G1终态资源查询和U2一次释放窗口仍必需。R1验收使用实际Host持久/停止消费者，不仅资源实体查询测试。C对象级句柄/批次额度实体保留延期，未标完成。
