# 第一工位实现状态

现行说明更新：2026-10-03。011仅完成澄清与文档同步，尚未设计/实现或运行验证；012交接未接收。当前规则见[011规格](../../../specs/011-plc-interaction-update/spec.md)及[同步清单](../../../specs/011-plc-interaction-update/clarification-sync-20261003.md)。

下列2026-09-22/23实现和测试结果完整保留其历史适用范围，不是20261001新协议或20261003工艺已实现的证明。旧固定F、测高前提、锁状态/ACK及测试专用执行须核查消费者后替代清理。

## 收敛修复复核（2026-09-22，未完成）

本次使用 `speckit-implement` 处理 Phase 17，但没有完成 T078–T086，也没有更改其勾选。下面原有“已验证”数据是历史结果，不是当前代码全量通过的证明。

- 已新增算法有限结果/继续策略、事件关联/迟到证据与物理故障策略及局部测试；策略文件存在不代表已完整接入业务流程。
- 已修改通知为版本化信封，修正事件类型/版本字段顺序；有界合并、必要证据交付和完整合同验收仍未完成。
- 已修改状态 ETag 的确定性计算并纳入当前运行；运行查询仍使用 observedRevision 与 persistedRevision，不能忽略未持久化的公开状态变化。
- 最近两次全量 Integration 均为 41/43 通过，失败组合包含状态条件查询、暂停状态竞争、普通仿真完成。之后局部通过不代表全量通过；回退试验性修改后尚未重新完成全量验证。
- 已撤回仅为避免竞争而增加的固定延时、缩减运行 ETag 版本字段及把取消请求直接标为 Cancelled 的尝试；不得将控制请求等同于实际停止或持久终态。
- 当前 001 合同及部分集成测试仍要求独立实体按钮等待；003 最新 PLC 规格明确覆盖旧定义，以 Pallet_Lock_Status=1 为继续依据，不新增协议外信号。需经授权同步相应合同、计划、任务和测试，不能回改最新协议或伪造反馈。
- 暂停/取消持久化、恢复核对与真正继续执行（T078）、媒体跨重启核对（T081）、完整故障矩阵与证据汇总仍未完成，不能作为前端稳定接入或生产就绪的结论。

本轮 `speckit.implement` 已完成公开 API 的增量实现，并补齐了 PLC、相机和 Worker 的受控适配边界。它不代表真实 PLC、相机、算法组件或生产环境已经接入。

## 已验证

- Host Release 构建：0 个警告、0 个错误。
- Rules：28 个通过。
- Contracts：77 个通过。
- Integration：43 个通过。
- 已有 Host/API 回归测试及公开控制 smoke tests 通过。

## 本轮实现

- 状态快照、结构化错误、ETag、媒体读取和配置校验 API。
- 暂停、取消、恢复核对、继续的受控路由及权限策略。
- Production 模式不再静默绑定进程内相机/算法模拟，改为明确 `NotIntegrated`。
- PLC Modbus 传输边界、协议编码器和有界连接泵。
- 相机 SDK/光源网关边界和单次 F 触发约束。
- Worker NDJSON 协议校验、长度限制、进程生命周期和未接入出口。
- 受控媒体 fixture 清单类型及路径安全校验。

## 尚未完成

## 本次第一工位主流程交付（2026-09-23）

- 已补齐 Host 侧 `Detection → Sorting → UnloadPreparation → UnlockObservation → ManualTrayRemovalConfirmation` 编排入口：前三阶段完成事件全部持久化后才创建不可变 `WholeTrayCompleted` 证据；解锁请求携带并重新核验 `WholeTrayCompletionReference`，只有关联且状态为 0 的反馈才追加 `ObservedUnlocked`；人工确认随后追加 `FinalUnloadCompleted`。
- 完成证据复用既有 `StageEventStore` 单写通道和幂等键，跨重启可从事件链重取；异常解锁保持 `UnknownHeld`，不会自动重发。
- 新增合同/单元证据：`WholeTrayWorkflowOrchestratorTests` 4 项，覆盖正常闭环、解锁前置拒绝、未知反馈和 Production 模式门禁；Contracts 全量 122/122、Integration 全量 43/43 通过。
- 本次流程证据运行模式为 Test/Simulated；没有宣称真实 PLC、相机、算法或生产验收通过。

### Deferred（不阻塞本次主流程交付）

- 其他工位及后续工位页面/流程：Deferred。
- 真实 PLC、相机 SDK、算法 Worker、现场节拍和生产兼容性：Deferred/NotRun。
- 完整暂停/取消持久化竞争、跨重启媒体索引、全量故障矩阵、独立 Host+VirtualPlc 进程证据及前端最终联调：Deferred。

任务清单中未勾选的任务仍保持未完成，尤其是：

- 真实 PLC/相机 SDK 和算法 Worker 的接口核验、适配及现场验证；
- SQLite 保存失败、恢复核对、跨重启媒体索引和持久控制语义的完整矩阵；
- 完整的暂停/取消/迟到事件归约、通知版本合同和资源背压验证；
- T026-T067、T068-T077 所列的全部任务证据、测试文件和最终追溯汇总。

禁止将 `NotIntegrated`、模拟媒体或模拟算法结果解释为真实设备/算法成功。
