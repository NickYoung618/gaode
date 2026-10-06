# 第一工位纵向切片研究与决策

**日期**：2026-09-23  
**依据**：最新 [spec.md](./spec.md)、constitution 1.2.1、现有 001/003/006 规格与合同、正式协议、当前代码和 VirtualPlc。

## 已核验的现状

- 现有唯一入口 `POST /api/v1/station01/runs`、公共准备执行器、配置冻结、3D/F 步骤和 handoff 存储可复用。
- `RecipeRunPlanner` 与配方绑定能力存在，但当前主线仍依赖额外 `/plan`、`/bind` 调用；计划引用未作为同一运行的持久化 handoff 输入。
- `ThreeStageWorkflowExecutor`、`WholeTrayWorkflowOrchestrator`、映射器、阶段事件存储和完成存储已有骨架，但正式 `IDetectionPort`/`IPlcStageActionPort`、编排器尚未在 Host 组合根接通。
- 当前 001 在 handoff 后把运行标为终态，公开快照把配方/分拣/整托状态硬编码为未开始，无法连续进入 003。
- `LatestProtocolPlcDevice` 与仓库既有独立 VirtualPlc 已共用正式 Modbus TCP 协议，不需要第二套 PLC 客户端或模拟服务。
- 当前检测超限返回 Failed/TimedOut，完成来源有硬编码，人工确认与最终完成分两次提交，验证脚本只覆盖 001；均未满足最新 spec。

## 协议与来源证据

- 正式协议允许 Host 通过既有启动、夹紧、运动、分拣、下料准备和解锁点位推进第一工位；业务阶段不等于新增 PLC 点位。
- 启动请求、夹紧状态和解锁结果必须分离；`Pallet_Lock_Cmd=0` 只能在已提交 `WholeTrayCompletion` 之后出现。
- VirtualPlc 只能产生协议设备事实，不得创建业务完成、人工确认或写业务数据库。
- 003 是纯后端 feature：只交付 Host、Domain/Application、正式端口、SQLite、REST API、SignalR 合同和联调客户端 E2E；不修改 `frontend/src`、`frontend/tests`、006 或客户原型。

## 决策记录

| Decision | Rationale | Alternatives considered |
| --- | --- | --- |
| 单 Host、单 owner | 满足 P05，复用现有编排骨架 | 新服务/消息队列/第二 owner：否决 |
| 唯一启动入口 | 保持 001→003 连续身份链 | 第二次启动或手工 plan/bind：否决 |
| 版本化 StartRunContext | 现有 ContextJson 可承载并校验缺失身份 | 003 临时 Guid/测试预造：否决 |
| 持久化 handoff 自动续接 | 只有提交事实可安全恢复 | 内存通知直接推进：否决 |
| 正式端口统一 Real/Simulated | 同逻辑、同状态、可标记来源 | 复制模拟流程/第二 PLC 客户端：否决 |
| 算法异常→Pending | 符合 P04/FR08 且不阻断其他对象 | 默认 OK 或暂停整托：否决 |
| PLC 未知→UnknownHeld | 防止物理动作盲目重发 | 重连自动续跑：否决 |
| FinalUnloadCompletion 为终点 | 完整覆盖解锁读回和人工确认 | WholeTrayCompletion/前端状态作终点：否决 |
| 分段短事务 | 避免跨设备持锁并消除部分可见 | 跨流程长事务/两次非原子最终写：否决 |
| 组件级来源矩阵 | 混合来源不可压成单一 Real；各组件可核验 | 聚合单一 source/quality：否决 |
| 精确重试与固定期限 | 与 FR08/FR13 一致且可用时钟证据验证 | 模糊“有限重试”或重置 deadline：否决 |
| 复用 SignalR，GET 对账 | 满足现有边界且无需新基础设施 | 新事件总线/outbox 框架：否决 |
| 五类独立进程 E2E | 直接覆盖最新验收风险 | 源码/进程内模拟/旧 passed：否决 |
| 拆分跨 feature 门禁 | 001 只阻塞 handoff v2 接线；006 永不阻塞 003 | 把 001/006 合并成全局前置：否决 |

### Decision 1：保持方案 B 与单一 Host 所有权

第一工位由一个 Host 内部应用编排器拥有运行、运动和状态推进。001 和 003 是同一 `runId/trayId` 的连续阶段，不拆微服务、不引入消息系统或第二个 workflow owner。

**否决**：新建整托服务、消息队列或第二套运动控制。它们会破坏单一所有权，并扩大本期范围。

### Decision 2：唯一入口启动完整流程

保留 `POST /api/v1/station01/runs`。Host 持久受理后完成配置冻结、任务创建、启动夹紧、3D、F、配方计划/绑定和 handoff；handoff 提交后自动进入 Detection，不要求第二次启动或客户端手工调用 `/plan`/`/bind`。

`202 Accepted` 不是完成；只有已提交 `FinalUnloadCompletion` 是本期成功终点。

### Decision 3：版本化 `StartRunContext` 固定身份输入

现有启动请求的 `ContextJson` 使用版本化、可校验的 `StartRunContext` schema，承载 `trayId/stationId/lineId/scenarioId/occupiedSlots` 等主流程身份输入。`trayId` 在启动时冻结；F 码是与其绑定的业务标识，不得在 003 临时随机生成 Guid，也不得由测试脚本预造 Detection 请求。

### Decision 4：handoff 是已提交事实和唯一续接边界

001→003 handoff 包含同一身份、配置/预算/用途快照、3D/F 媒体与结果、F 唯一码、冻结 `RecipeRunPlan` 及绑定证据、source/quality 和 revision。它不是动作命令；只有提交成功且与租约/快照/计划一致时 Host 才自动续接。

**否决**：内存通知直接推进、预构造计划跳过公共准备、把 `HandoffReady` 当第一工位完成。

### Decision 5：正式端口统一真实与模拟路径

- Detection 只调用 `IDetectionPort`；真实与 Simulated/Test 适配器使用相同请求/结果、截止时间、事件和保存路径。
- Sorting、UnloadPreparation、UnlockObservation 只调用 `IPlcStageActionPort`；它紧贴并复用 `LatestProtocolPlcDevice` 的单一连接、epoch、安全和单飞控制。
- 独立 VirtualPlc 通过正式 Modbus TCP 接入相同 PLC 适配器；进程内 SimulatedPlc 只用于单元/合同测试。

### Decision 6：算法异常收敛为逐对象 Pending

算法失败、超时、未配置、未接入或无有效结果在有限尝试后必须保存原错误、尝试次数、source/quality，并为受影响对象形成 `Pending`。只要对象身份、位置和 Pending 目标能完整唯一映射，Detection 可完成并继续正式 PLC 分拣到 Pending 区。

只有对象身份、位置或目标缺失、重复、歧义时形成 `MappingFailed` 并暂停。Detection 异常不得投影为 PLC `UnknownHeld`，也不得伪造成 OK/default success。

### Decision 7：PLC 未知事实保持 UnknownHeld

PLC 动作若可能已经派发但没有可信终态，或断联/epoch 变化使事实无法确认，当前物理阶段进入 `UnknownHeld`，保持安全状态和租约且不自动重发。恢复必须通过正式状态读回或人工核对追加新事实；已完成动作不得重放。

### Decision 8：完成与最终完成严格分层

Detection、Sorting、UnloadPreparation 三阶段完成后才可聚合 `WholeTrayCompletion`，其公开状态仅为 `ReadyForUnlock`。解锁读回形成 `ObservedUnlocked`；受控人工确认 API 再原子写 `ManualTrayRemovalConfirmed` 和 `FinalUnloadCompletion`。最终记录才把完整运行投影为 Completed。

**否决**：用裸 bool、非空 Guid、前端状态或 VirtualPlc 状态代替完成引用。

### Decision 9：采用分段短事务而非跨设备长事务

动作意图和投影在设备调用前原子提交；反馈事件与阶段投影在调用后原子提交。三阶段完成后另起聚合短事务重新核验并原子写 `WholeTrayCompletion`；人工确认与 `FinalUnloadCompletion` 使用同一后续短事务。任何数据库事务都不得跨 PLC 或算法调用。

现有 `WholeTrayCompletionStore` 的先读后单独 append，以及人工确认/最终完成两次 append 存在部分可见窗口，本期必须修正。

### Decision 10：采用不可变组件来源矩阵

完成、解锁和人工确认的来源不得在编排器硬编码，也不得把混合来源压成单一 `Real`。`WholeTrayCompletion` 保存 `ReadyForUnlockSourceMatrix`，按 Host、PLC、Camera、Light、Algorithm、ManualActor 区分组件；当前里程碑必需组件逐项保存 source、quality、versionRef 和 evidenceReferences。ManualActor 在 ReadyForUnlock 时只能是 `NotYetRequired`，不能伪造尚未发生的人工事实。

人工确认与 `FinalUnloadCompletion` 同一事务生成新的不可变 `FinalSourceMatrix`，引用原矩阵并加入认证 actor 和调用渠道证据，不回写原矩阵。任一当前必需组件为 Unknown、Missing 或 Unverifiable 时阻止对应完成；任一组件为 Virtual/Simulated/Test 时，整托只可标记 `SoftwareLoopOnly`，不得声明真机或生产验收。

### Decision 11：复用现有通知，GET 是事实源

事件/投影事务提交后复用 Station01 SignalR 发布包含 `runId/revision/eventId/changedFields` 的轻量通知。通知可丢、重、乱序，客户端必须按 persistedRevision GET 对账；通知不控制设备、不生成完成，不引入 outbox 或通用事件总线。

### Decision 12：最小 E2E 固定为五类独立进程场景

真实 Host、独立 VirtualPlc、正式 Modbus TCP、真实 SQLite 和明确标记的 Simulated/Test 相机/光源/算法构成最低软件验收环境。必须覆盖：正常闭环、算法失败→Pending 后继续、完成/解锁门禁拒绝、PLC 断联或 epoch 变化→UnknownHeld 且不重发、Host重启只重建历史查询，故障双端复位/初始成立后新run完整流程；旧单指令/边界续跑由USR-D替代。

每场景固定保存 Host/VirtualPlc 进程、API transcript、Modbus audit、SQLite 事件/投影、source matrix 和 final result 六类证据。历史只完成公共准备、分拣未开始的 passed 证据不能作为本期闭环证据。E2E 不构建、不运行、不依赖 006 页面、`frontend/src`、`frontend/tests` 或原型测试。

### Decision 13：跨 feature 门禁必须拆分

003 冻结 `s01-handoff/2.0`、版本化 API/通知扩展合同和差异记录。2026-09-23用户已有限授权001 handoff直接相关 spec/contracts/plan/tasks 和 producer 接线；共享文档合同先行对齐，实际接线/验证仍由003 T024/T026完成。006仍需独立授权，且不阻塞任何003后端工作或五场景 E2E。

### Decision 14：两类重试共用不可重置阶段期限

临时通信错误最多 4 次总尝试，退避 1/2/4 秒；Detection 算法超时最多 3 次总尝试，退避 2/5 秒。二者共用阶段开始时冻结的 120 秒 `StageDeadlineAt`，重试、退避和重启恢复均不能重置。只有明确未派发的通信调用可重试；PLC 物理动作一旦可能已派发，立即进入 `UnknownHeld`，不消费后续重试次数且永不自动重发。

### Decision 15：旧任务恢复必须经过人工决策

Host 重启、重连或普通复位不得自动重检、自动报废、自动续跑或重放已完成动作。仅当前活动任务的无物理副作用 Detection 尝试可在原 operation 和原 deadline 内继续；旧任务 ReDetect/Scrap 必须先持久化受控人工决定，记录 actor、decidedAt、reason、originalTask/run、evidenceReferences、requestId 和 expectedRevision。该决定授权后续用例，但不能直接制造设备、算法或完成事实。

## Deferred 与未采用方案

Deferred：其他工位；真实 PLC/相机/光源/算法 Worker 和生产验收；生产原型人工确认控件；MES、模型、样本/标注；五类之外的故障全矩阵、压力和长稳；与主线无关的媒体/通知/打包/UI 扩展；OPEN-16/22/26 所依赖的生产规则。

未采用：硬编码成功、跳状态、自动隐藏降级、无限重试、前端直控设备/写库、测试脚本直接改业务库、新 VirtualPlc、新微服务、通用 workflow/saga 框架及无关大规模重构。
