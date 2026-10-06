2026-09-26 PARAM事实增量：按共享API合同保存实际CaptureFact/RequestedCaptureSettings并从现有媒体查询投影；008 T054、003 T068承接，原编号及完整条件不变。

2026-09-26夜间008 T057最小依赖：LatestProtocolStageActionAdapter在PickCompleted后通过注入的既有持久回调提交在途事实；AdapterBindings给正式Host接入IStageEventStore。保存失败保持UnknownHeld且不提交放料。必要端口测试记录保存发生于取料2与放料写包之间，并验证失败无放料。没有新的PLC字段、表、接口路由或通用调度设计。

2026-09-26 USR-20260926-D恢复设计：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替代旧U05同轮单指令重发。双端复位、真实初始状态成立后显式新启动；旧运行和证据保留。设计尚未实施，tasks保持只读，原编号和勾选不变。

2026-09-26旋转Test增量：T071按[虚拟旋转请求/结果合同](contracts/rotation-test-execution.md)实施U03；生产寄存器不扩展，原编号、历史勾选及完成条件不变。

2026-09-26 人工子范围增量：T072按[人工执行合同](contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

# 技术方案：第一工位后端完整主流程纵向切片

008 E直接共享设计见[E Test合同](contracts/e-test-execution.md)。LatestProtocolPlcDevice在现有F扫码角色分支接E，VirtualPlc仍消费同一命令5及扫码Z；采集/worker/预算增量由008 T061，必要门禁与查询沿用既有管线。

**功能标识**：003-plc-latest-protocol  
**日期**：2026-09-24  
**规格**：[spec.md](./spec.md)  
**当前适用宪章版本**：7.0.0；历史验证证据保留产生时版本  
**范围**：只交付第一工位从唯一 Host 启动入口到 `FinalUnloadCompletion` 的后端纵向切片。

## 方案摘要

003 是第一工位后端主流程 feature，只实现单一 Host、Domain/Application、正式设备与算法端口、SQLite、后端 REST API、SignalR 合同，以及由测试/联调客户端驱动的独立进程 E2E。`POST /api/v1/station01/runs` 保持唯一入口；Host 在同一 `runId/trayId` 下完成配置冻结、公共准备、配方计划/绑定和已提交 handoff，再自动续接 Detection、UnloadPreparation、Sorting、整托完成聚合、解锁观察、人工移除确认和最终完成。`202 Accepted` 不是完成，只有已提交的 `FinalUnloadCompletion` 是成功终点。

003历史E2E固定使用真实Host进程、仓库既有独立VirtualPlc、正式Modbus TCP和真实SQLite。相机、光源和算法可使用明确标记的Simulated/Test适配器，但必须复用正式接口、同一状态机、重试、映射、事务和持久化路径。确认由测试/联调客户端调用Host API；003历史证据不依赖006页面、前端构建或前端测试，也不替代007新增的实际前端/独立worker闭环。

VirtualPlc 整体只经正式 Modbus TCP 提供本工位需要的心跳、夹紧、运动、分拣、下料、解锁、失败、断联和 connection epoch 设备事实；它不执行 Detection、业务编排、人工确认或业务数据库写入。“UnloadPreparation/UnlockObservation”仅是下料子阶段边界，不是 VirtualPlc 整体能力边界。

### 2026-09-23 VirtualPlc 先行修改回流

规范依据是最新版《PLC与上位机通信接口协议》§3.1.7及FR04，不以先行代码反向批准规则。VirtualPlc 现已在检测期间清旧 `4x0001` 后保留 `4x0002=1`，并在下一合法运动受理时重启反馈；`4x0052` 仅在 `4x0053=2` 后清零。独立监控新增按实际写入序号查询/展示、最近变化排除心跳、有限缓存和缺口提示；本地接口与快照仅供诊断。监控源码已改为将实际 0 值显示为数值 0，实际页面操作验证尚未取得，T062 保持未完成。

specs/003-plc-latest-protocol T061 已修正 Host 正式适配器的下一运动门禁及 `VirtualPlcLatestProtocolTests` 旧断言，保留连接代次、坐标、安全互锁与未知动作不重发。`artifacts/plc-latest/t061-20260923-223458/manifest.json` 记录源码哈希及正式 Modbus 连续两次运动等 3/3 定向测试通过；该证据只覆盖 T061，不代表第一工位完整联调通过，specs/007-station01-integrated-loop T015 仍未完成。

| 分类 | 本期含义 |
| --- | --- |
| 真实实现 | Host、唯一编排、配置/配方冻结、正式端口、Motion/Safety 准入、Modbus TCP、SQLite、REST API、SignalR、恢复判断、E2E 驱动 |
| Virtual/Simulated | 既有独立 VirtualPlc；实现正式相机/光源/算法端口的 Simulated/Test 适配器；全部来源逐组件留证 |
| Deferred | 006 页面实现、生产人工确认 UI、真实现场设备/算法验收、其他工位及末尾列出的扩展工作 |

## 技术上下文（Technical Context）

| 事项 | 当前选用方案 | 决策来源与状态 | 尚缺证据/OPEN |
| --- | --- | --- | --- |
| 后端运行时 | 现有 .NET 10 / ASP.NET Core 单 Host | P05；不拆服务 | OPEN-22 只限制生产兼容性声明 |
| 数据 | EF Core + SQLite，单写者、分段短事务 | P08、FR13 | 聚合与最终完成原子提交待实现 |
| PLC/运动 | `LatestProtocolPlcDevice` + 正式 `IPlcStageActionPort` + Modbus TCP | FR01–FR05、FR13 | 真机/现场点位验收 Deferred |
| 采集/算法 | 正式 `IDetectionPort`；Real 或 Simulated/Test 适配器 | FR08、P04/P09/P11 | 真实 SDK/Worker、生产模型 Deferred |
| 配方 | 版本化加载、校验、冻结 `RecipeRunPlan` 并持久绑定 | FR05/FR06/FR11 | OPEN-16/26 仅限制相关生产规则 |
| 对外合同 | Host REST API + `/hubs/station01`，GET 快照为事实源 | FR07/FR09 | 006 是独立非阻塞消费者 |
| 软件验证 | Host + VirtualPlc + 正式 Modbus + SQLite + 联调客户端，五类场景 | FR10/FR12 | 不替代生产验收 |

## 宪章检查（Constitution Check）

| 原则 | 本功能检查点 | 设计前 | 设计后 | 证据/边界 |
| --- | --- | --- | --- | --- |
| P01 | 决策和冲突可追溯 | 符合 | 符合 | [research.md](./research.md)、[differences.md](./differences.md) |
| P02 | 仅第一工位后端闭环 | 符合 | 符合 | 006、其他工位、外部系统均不进入实现 |
| P03 | 固定 XY、3D 仅 Z、配方驱动 | 符合 | 符合 | 沿用冻结配置，不新增运行时 XY 求解 |
| P04 | 有限等待且不伪造结果 | 待补充，仅限制所列部分 | 符合 | 算法超限形成 Pending；映射歧义暂停 |
| P05 | 单 Host、分层端口、API 受理/完成分离 | 待补充，仅限制所列部分 | 符合 | 不建新服务或第二 owner |
| P06 | 资源与等待有界 | 待补充，仅限制所列部分 | 符合 | 精确重试、固定 按冻结RecipeExecutionBudget确定的 deadline、未知动作不重发 |
| P07 | 身份/动作/结果可追溯 | 待补充，仅限制所列部分 | 符合 | 同一 run/tray/plan/epoch；组件来源矩阵 |
| P08 | 意图先存、事实后存、短事务恢复 | 待补充，仅限制所列部分 | 符合 | 两类短事务、独立聚合事务、最终原子事务 |
| P09 | 模拟共用正式路径、来源标记及FR15诊断 | 待补充，仅限制所列部分 | 待补充（FR15） | 来源矩阵沿用；首次通信、心跳过期与失败反馈的日志关联及持久查询待T063验证 |
| P10 | OPEN 仅局部限制 | 符合 | 符合 | OPEN-16/22/26 不阻塞软件切片 |
| P11 | 配置与策略统一注册 | 待补充，仅限制所列部分 | 符合 | Host 组合根显式注册正式端口 |
| P12 | 原型只读，前端实现独立 | 符合 | 符合 | 003 前端实现不适用；只定义后端消费合同 |

设计后无宪章违反项。003 不创建或修改 `frontend/src`、`frontend/tests`、`desktop`、`specs/006-frontend-station01-console` 或客户原型。

## 第一工位纵向切片

夹紧窗口按 FR02 复用冻结预算 `businessMs.clampCompletion`（ms，正整数至少 1），来源为 001 `contracts/budget.schema.json`；从启动受理事实提交后进入 `WaitingClamp` 起计时，截止前状态 1 才可继续，达到/超过期限为 `ClampTimeout/UnknownHeld`，状态 2 立即报警锁停，断联/代次变化按设备未知处理。003历史独立VirtualPlc联调使用既有 `examples/budgets.virtual-plc.json` 的Test v1.0.0 / 5000 ms；007使用覆盖3–5秒采集、10秒算法的新版本Test预算，不沿用旧公共准备短窗口。此值不是生产默认值。T017/T021保留历史配置冻结及4999/5000 ms边界证据，不新增预算模型或另一套计时策略。

| 顺序 | 主流程步骤 | 真实实现职责 | Virtual/Simulated 允许范围 | 提交事实/出口 |
| --- | --- | --- | --- | --- |
| 1 | 第一工位入口 | Host 持久受理启动命令，创建 run/tray 和资源租约 | 联调客户端只调用 Host API | 命令回执与运行投影，非完成 |
| 2 | 配置/配方加载 | 加载、能力校验并冻结配置、点位、budget、用途和策略版本 | Test 配方共用结构并标记用途 | 不兼容明确拒绝，无自动降级 |
| 3 | 任务与公共准备 | `PC_System_Ready → PC_Start_Cmd → Pallet_Lock_Status=1`，再执行 3D、F、plan/bind；Host 不写 Cmd=1 | VirtualPlc 提供正式协议事实；采集可 Simulated | 已提交的事件、媒体和结果引用 |
| 4 | 001→003 handoff | 版本化保存身份、冻结计划和证据引用，提交后续接 | 不允许脚本预造或跳 Detection | `s01-handoff/2.0` 不可变记录 |
| 5 | Detection | `IDetectionPort` 产生逐对象结果并完成唯一映射 | 相机/光源/算法 Simulated/Test 走相同端口 | OK/NG/Pending；歧义为 MappingFailed |
| 6 | Sorting | 先存意图，再经 `IPlcStageActionPort` 执行冻结动作 | VirtualPlc 经相同 Modbus 点位返回事实 | 反馈事件/投影；Pending 进入 Pending 区 |
| 7 | UnloadPreparation | 正式 PLC 端口执行下料位移动 | VirtualPlc 提供点位事实 | Completed 或 UnknownHeld |
| 8 | 整托聚合 | 独立短事务核验三阶段和组件证据矩阵 | 完整模拟证据可完成软件闭环 | `WholeTrayCompletion` / ReadyForUnlock |
| 9 | UnlockObservation | 核验持久完成引用后写 Cmd=0 并读回 Status=0 | VirtualPlc 只产生协议事实 | ObservedUnlocked 或 UnknownHeld |
| 10 | 人工移除与最终完成 | 联调客户端调用受控 API；同事务保存确认、最终来源矩阵和 `FinalUnloadCompletion` | 调用渠道标记 Test，actor 来自认证上下文 | 唯一成功终点 |
| 11 | API/通知 | GET 提供持久快照；提交后发布轻量 SignalR 通知 | 联调客户端消费；通知不生成事实 | 可重查、可去重、版本化后端合同 |

状态主线：`Accepted → PublicPreparation → HandoffReady → Detection → UnloadPreparation → Sorting → ReadyForUnlock → ObservedUnlocked → AwaitingManualRemoval → FinalUnloadCompleted`。可能已派发的 PLC 动作无可信终态时进入当前阶段 `UnknownHeld`，禁止自动重发。

## 当前实现差距与最小改动面

- handoff 后当前运行被过早标记 Completed；需改为非终态边界并由同一 Host 续接。
- `/plan`、`/bind` 仍是主流程外部步骤；需复用其应用逻辑实现自动 plan/bind，保留调试端点但不依赖它们。
- `IDetectionPort`、`IPlcStageActionPort`、映射器和整托编排尚未完成组合根接线；只补薄适配和注册。
- Detection 超限当前为 Failed/TimedOut；需按 FR08 保存 Pending 与原始异常后继续合法映射。
- `WholeTrayCompletionStore` 存在核验与写入间隙；人工确认和最终完成仍分开提交。
- 当前聚合仅有单一 source/quality；需改为不可变组件证据矩阵，禁止把混合来源压成 `Real`。
- 运行查询/SignalR 尚未覆盖 003 投影、矩阵摘要和恢复结果；只扩展后端合同，不安排页面接线。
- 验证脚本只覆盖局部公共准备；需扩展为五场景和统一证据包，不新建模拟服务。

## 结构与职责（Project Structure）

| 模块/端口 | 所有者 | 本期变化 |
| --- | --- | --- |
| `Gaode.Domain` | 状态、身份、映射、完成和来源矩阵规则 | 补 Pending、UnknownHeld、矩阵和最终完成不变量 |
| `Gaode.Application/Station01` | 唯一运行编排与 handoff 消费 | 从已提交 handoff 续接，不第二次启动 |
| `Gaode.Application/Workflow` | Detection/Sorting/Unload 状态机 | 精确重试、映射、阶段事实与恢复门禁 |
| `Gaode.Infrastructure/Devices` | Modbus/连接 epoch/PLC 事实 | 实现正式 stage action adapter，不写业务库 |
| `Gaode.Infrastructure/Algorithms` | 相机/光源/算法适配 | Real/Simulated/Test 都实现 `IDetectionPort` 并传播证据 |
| `Gaode.Infrastructure/Persistence` | SQLite 单写、事件/投影/聚合 | 补分段事务、组件矩阵、恢复查询 |
| `Gaode.Host` | API、组合根、后台执行、SignalR | 注册端口、扩展查询、人工确认/恢复决策 API |
| `VirtualPlc` | 独立协议设备事实 | 复用现有程序；只在已确认协议缺口时最小修改 |
| `scripts/verify-latest-plc.py` | E2E 驱动与证据归档 | 只经 Host API 驱动五场景，不写业务库 |

明确排除：`frontend/**`、`desktop/**`、`specs/006-frontend-station01-console/**` 和 `E:\dzk\gaode\原型.zip`。不新增项目层级、微服务、消息系统、通用 workflow 框架或第二套 PLC 客户端。

## 数据、契约与跨 feature 依赖

数据不变量见 [data-model.md](./data-model.md)，合同索引见 [contracts.md](./contracts.md)。003 内冻结以下版本化后端合同：

- `s01-handoff/2.0`：同一身份、冻结计划、组件证据和提交后续接。
- `station01-main-flow-api/1.0`：启动、查询、人工移除确认和受控恢复决定。
- `station01-status-notification/1.0`：提交后通知、矩阵引用和 GET 对账。
- [component-source-matrix.md](./contracts/component-source-matrix.md)：ReadyForUnlock 与 Final 两个不可变矩阵快照。

依赖门禁必须拆开处理：

| 边界 | 当前 003 可做 | 仍需实际完成 | 阻塞范围 |
| --- | --- | --- | --- |
| 003 内部 | Domain/Application 端口、SQLite、Host API/SignalR、合同测试、E2E 驱动 | 无 | 不受 001/006 对齐阻塞 |
| 001 handoff | 在 003 定义 `s01-handoff/2.0` 消费合同和差异；2026-09-23已有限授权并对齐001直接相关规格产物 | 实际 producer/consumer 接线与验证由T024/T026执行 | 文档门禁已满足；在T024完成前仍只阻塞穿越该边界的最终E2E执行 |
| 006 前端 | 在 003 定义 consumer-neutral API/通知和差异 | 修改 006 规格、代码或测试 | 不阻塞任何 003 后端工作或五场景 E2E |

003 仅按2026-09-23有限授权修改001 handoff直接相关产物，不修改001其他需求或006。差异登记在 [differences.md](./differences.md)；不得通过代码静默改变未授权合同。

## 007虚拟集成共享接线约束

007复用本计划的安全、阶段与短事务规则。固定目录图片由正式采集端口按公共3D/F和冻结Detection计划逐次读取并经MediaStore保存；每项Detection采集对应一次正式算法端口向独立虚拟worker实际派发。Host通过WorkerProcessSupervisor管理唯一worker子进程，采集媒体、call/attempt、配方快照、PLC反馈及SQLite写入均关联同一runId。候选CAP/P01预期Detection C=2、A=2，采集3–5秒/算法10秒须使用新版本Test预算核对120秒阶段期限；不删除必检步骤。此段只同步003共享合同，不重写已勾选历史任务或把003旧五场景证据记为007完整联调通过。

## 组件来源与质量聚合

`WholeTrayCompletion` 保存不可变 `ReadyForUnlockSourceMatrix`，至少区分 Host、PLC、Camera、Light、Algorithm、ManualActor。每个当前必需组件保存 `source`、`quality`、`versionRef` 和 `evidenceReferences`；ManualActor 在解锁前使用显式 `NotYetRequired` 生命周期槽，不能伪造尚未发生的人工事实。

人工确认与 `FinalUnloadCompletion` 同一事务创建新的不可变 `FinalSourceMatrix`：引用前述矩阵并加入认证 actor 和 Test/Commissioning 调用渠道证据，不回写原矩阵。最终成功要求六类组件在其适用里程碑均可核验。当前必需组件为 Unknown、Missing 或 Unverifiable 时阻止对应完成；任一组件包含 Virtual、Simulated 或 Test 来源时，整托证据固定为 `SoftwareLoopOnly`，不得声明真机或生产验收。即使全部为 Real，也只能是 `ProductionCandidate/NotAccepted`，不能由矩阵自动生成生产验收结论。

查询和通知可提供 `matrixRef/matrixDigest/sourceKinds/containsNonProductionEvidence/blockedComponents` 派生摘要，但不得保存或返回会掩盖混合来源的单一 `Real` 标记。

## 配方、配置与策略扩展

003 只复用现有配方生命周期和配置基础设施来完成本次运行的加载、校验、计划生成、绑定和快照冻结，不扩大为新的配方管理 feature。正式与 Test/Simulated 配方共用数据结构、字段校验、发布版本读取、`RecipeRunPlan` 生成和冻结逻辑；每份运行快照记录 purpose、source、recipe revision、point revision、budget revision 和 capability versions。运行中发布或回滚只影响后续任务，不改写在途和历史快照。

已有能力内的产品、槽位、检测面和点位变化只能通过版本化配置处理；新增能力必须实现统一端口并在 Host 组合根显式注册，主流程不得堆产品分支或执行任意脚本。加载时拒绝未知/不兼容能力，但算法运行时未就绪必须按有限尝试形成 Pending，不能伪装成配置错误。真实动作准入独立拒绝 Test/Simulated 参数；Virtual/Simulated 选择由显式配置完成，Production 不允许隐藏回退。

沿用任务冻结的有界采集/媒体 budget 和现有 A/B 批采资源规则，不自创生产容量或节拍；缺输入任务不得占用可执行 Worker，心跳和停止不等待算法、数据库、通知或 UI。

## 重试、期限与恢复

| 路径 | 精确策略 | 失败出口 |
| --- | --- | --- |
| Detection 临时通信错误/断联 | 首次调用外最多重试 3 次，总计最多 4 次；退避 1/2/4 秒 | 次数耗尽或共享 deadline 先到即为受影响已知对象形成带原异常的 Pending；映射完整后继续正式 Pending 分拣，不转为 OK 或 PLC UnknownHeld |
| PLC 派发前临时通信错误 | 仅确认未派发时允许首次调用外最多重试 3 次，总计最多 4 次；退避 1/2/4 秒 | 次数耗尽或 deadline 到期明确失败且不得声明物理完成；若物理动作可能已派发则不进入重试，立即 UnknownHeld |
| Detection 算法超时 | 首次调用外最多重试 2 次，总计最多 3 次；退避 2/5 秒 | 逐对象 Pending 并保留原始异常；映射完整后继续 |
| 阶段期限 | 阶段开始冻结 `StageDeadlineAt = start + 冻结阶段预算`；同run内两类重试、退避共用且不得重置；故障新run独立冻结预算 | 超期按对应合同收敛，不嵌套倍增 |
| 映射歧义 | 不自动重试或猜测 | MappingFailed，保持托盘并等待核对 |
| PLC 已派发/可能派发 | 零自动重发 | UnknownHeld，保持占用，等正式读回或受控人工决定 |

每次尝试和退避都追加 attempt、error、plannedAt、deadline、operationId 和 epoch 证据。Host 重启、重连或普通复位不得自动重检、自动报废、自动续跑或重放已完成动作。未进入设备/保存故障的当前Detection算法有限重试仍使用原operation和deadline；发生故障按USR-D完整新轮，不能把此算法重试用于故障续跑；旧任务 ReDetect/Scrap 必须先提交受控人工决定，记录 actor、decidedAt、reason、originalTask/run、evidenceReferences、requestId 和 expectedRevision。

## 保存与恢复

1. 动作意图及对应投影用一个短事务提交，提交后才调用 PLC、相机或算法。
2. 反馈事件及对应阶段投影用另一个短事务原子提交。
3. Detection、UnloadPreparation、Sorting 的 Completed 均已提交后，独立聚合短事务重新核验身份、plan revision、组件矩阵和未解决终态，再原子写 `WholeTrayCompleted`、`WholeTrayCompletion` 与整托投影。
4. `ManualTrayRemovalConfirmed`、最终组件矩阵和 `FinalUnloadCompletion` 用同一后续短事务原子提交。
5. 不跨 PLC、相机或算法调用持有数据库事务。重启只使用已提交事件；已完成动作不重放，未知物理动作进入 UnknownHeld。

## API、通知和后端边界

- `POST /api/v1/station01/runs`：唯一入口，返回 202 和 command/run 引用。
- `GET /api/v1/station01/runs/{runId}`：返回完整阶段、限制、组件矩阵引用/摘要和 persistedRevision。
- `POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`：唯一确认路由，请求体为`requestId/expectedRevision/reason`；核验服务端取得的同run WholeTrayCompletion 与 ObservedUnlocked，actor/role来自认证上下文，确认时间/结果及Test/Simulated渠道来源由服务端记录。旧长路由不是兼容入口。
- 受控旧任务恢复决定 API：只记录并授权 ReDetect/Scrap 决定，不直接制造完成或设备事实。
- `/hubs/station01`：事务提交后发布轻量通知；GET 快照始终是事实源。

这些是 consumer-neutral 后端合同。003 历史后端实现与证据不安排 `frontend/src` 或 `frontend/tests`；006页面请求、Test凭据传递及已有位置展示归006，007 Host侧限定Test来源的API/通知跨源和授权连通接线归007。双方实际联通是007完整前端闭环前置，不追溯成为003历史任务完成条件。

## 软件验证与证据计划

最低 E2E 组合：真实 Host + 独立 VirtualPlc + 正式 Modbus TCP + 真实 SQLite + Simulated/Test 相机/光源/算法正式适配器 + 只调用 Host API 的联调客户端。不得构建、运行或依赖 006 页面、`frontend/src`、`frontend/tests` 或客户原型测试。

| 场景 | 关键断言 |
| --- | --- |
| 正常完整闭环 | 同一 run/tray 从入口经 handoff 到 FinalUnloadCompletion；完成前不解锁 |
| 算法失败→Pending | 算法最多 3 次、2/5 秒、共享 deadline；保存原错误并继续 Pending 分拣 |
| 门禁拒绝 | WholeTrayCompletion 前无 Cmd=0；ObservedUnlocked 前人工确认返回冲突 |
| PLC 断联/epoch 变化 | 可能已派发动作进入 UnknownHeld，恢复后零自动重发 |
| Host 重启 | 只重建已提交历史查询；双端复位且初始成立后显式新轮，不续跑旧轮 |

每个场景必须同时保存六类证据：① Host/VirtualPlc 进程与原始日志；② `api-transcript.json`；③ `modbus-audit.json`；④ SQLite 事件与投影导出；⑤ `source-matrix.json`；⑥ `final-result.json`。`manifest.json` 只记录索引、哈希、版本和证据引用，不能替代来源矩阵或最终结果。精确的派发前通信 4 次与 1/2/4 秒通过可控时钟合同/集成测试补证，不新增第六个 E2E 场景。

## 实施顺序与任务拆分输入

1. 先在 003 内冻结领域不变量、版本化 handoff/API/notification 合同、组件来源矩阵和跨 feature 差异记录。
2. 并行实现不依赖 handoff v2 接线的 Domain/Application 端口、SQLite 事务/恢复、Host API/SignalR 和五场景 E2E 驱动。
3. 复用2026-09-23有限授权后的共享合同接通 `s01-handoff/2.0` producer/consumer；授权与文档对齐不替代T024/T026的实际实现和验证，006不参与此依赖链。
4. 接通唯一入口、Detection、UnloadPreparation、Sorting、聚合、解锁、人工确认和最终完成。
5. 运行后端规则/合同/集成测试及五场景独立进程 E2E，归档统一六类证据。

`tasks.md` 已按上述后端依赖顺序生成；后续只定点维护受影响任务和证据要求，不要求重新生成整份计划或任务。规格修复不等于实现完成，任务勾选须依据实际实现与测试。

## Deferred 与禁止项

Deferred：006 前端页面和测试、生产人工确认 UI、其他工位、真实 PLC/相机/光源/算法 Worker 与现场生产验收、MES/模型/样本、五场景外全组合故障矩阵/压力/长稳、OPEN-16/22/26 依赖的生产规则、与主流程无关的 UI/媒体/通知/打包扩展。

003历史实现范围内禁止：新微服务、消息系统、通用工作流框架、第二PLC客户端或无需求的新模拟服务、大规模重构；硬编码成功、跳状态、伪造设备/算法结果、前端改状态推进；VirtualPlc或联调客户端直接写业务库/控制业务流程；隐藏降级、无限重试、自动绕过门禁。007已批准的Host管理唯一独立虚拟算法worker属于其明确接线范围，不据此扩展通用服务架构。

## OPEN 与设计后结论

OPEN-16/22/26 只限制对应生产规则和声明，不阻塞明确标记的 Virtual/Simulated 软件闭环。真实 SDK、PLC 与生产算法缺失不阻塞本期后端实现，但所有证据必须保持非生产标记。

设计后 Constitution Check 通过，可按现有 tasks 执行不依赖门禁的开发。001 未获对齐授权时，只阻塞 handoff v2 实际接线和跨该边界的最终 E2E 执行；006 未对齐不阻塞任何 003 后端开发或五场景 E2E。

## 客户确认原型检查（P12）

`E:\dzk\gaode\原型.zip` 及其中 `a.html`、`data-view.html`、`login.html` 保持只读。003 不修改、覆盖、重新生成、测试或绑定这些页面，不新增控件、文字、布局或交互；生产人工确认 UI 保持 Deferred。

## 2026-09-23 共享媒体查询同步

007在Host查询层组合已提交Media写入、公共准备采集意图和Detection阶段采集事件，提供同run媒体清单；Detection新事件补存冻结步骤的业务相机身份。保留现有媒体流与授权。旧事件缺身份返回Unknown且不回写历史数据；007负责实现和必要验证，003历史任务和五场景证据不重判。

## 2026-09-24 FR15设备诊断最小设计

新版下料按§3.1.6：写冻结目标Camera_Target_X/Y及Grab_Target_Z(0003/0005/000B)，再命令4；核验本次到位和实际XYZ后清命令。前次动作必要复位及安全条件保持，不再采用写检测Z或命令4只动XY约定。普通盘末Detection→UnloadPreparation→适用Sorting→WholeTrayCompletion→ObservedUnlocked→页面取盘确认→Final；到位不等于可取盘，适用分拣/必要保存/无未知在途门禁必须成立。

沿正式Modbus调用边界及Host编排事件记录首次请求、各次尝试、心跳最后有效翻转、观测时间与过期判定、请求发送结果、匹配反馈和`connectionEpoch`。日志使用命令/流程、设备/安全等分类及信息/警告/错误等级，贯通已知`requestId/commandId/runId/operationId`；配置快照记录协议合同、地址/字序配置的版本引用，不把未确认生产参数写成事实。底层网络/协议异常保留类型、消息、堆栈、内部异常及适用原始错误码，凭据脱敏。通信失败时只能断定当前安全无法确认；同代次可靠反馈为不安全时才断定设备明确不安全。状态过期不得沿用旧安全位，动作已派发但无匹配反馈仍进入UnknownHeld，不因重连而自动恢复。

日志保存与查询复用项目现有本地进程日志及既有运行关联，不引入集中平台或新公开字段；心跳、轮询和重复故障限频或聚合，保留首次/末次、次数及状态变化。T063只验证FR15必要的首次通信失败、明确不安全对照及陈旧状态/心跳边界，并把Host/VirtualPlc原始日志、API状态和协议审计关联到同一受控Test运行；旧T001–T062勾选及FR12五场景证据不改判。若公开状态无法表达已知限制，先走共享合同与006消费者同步，本次合同不变。

实施核验修正：正式PLC `GET /status` 原缺观测时间与失败来源，因此先同步 `contracts/status-notifications.md` 和 `station01-main-flow-api.md`，再增列脱敏 `observedUtc/diagnosticCode/failureOrigin`。生效阈值来自 `Station01Registration` 读取冻结预算的 `heartbeatDisconnect`；007 Test样本原8秒与FR03三秒基线不一致，现改为3000ms，`plcIo=1000ms` 时 `Observe` 状态过期门为 `max(500,5×plcIo)=5000ms`，真实运行日志须逐项核实。首次隔离运行在未建runId前出现 `HeartbeatStoppedChanging`，仅归运行前故障，不能抵扣受理后T063场景；保留原始日志和VirtualPlc变化事实，不放宽阈值。

2026-09-24 心跳延迟缺陷增量：保留独立heartbeat/business连接和3秒PLC/Host保护；在现有Modbus/VirtualPlc边界只加异常窗口的有界UTC、单调耗时、transaction及线程池队列诊断。隔离诊断实例已见PLC约0.1ms处理而Host读首部约3222ms才完成，同时Host待执行工作5项/线程3条；这只证明新实例的响应后长等待和工作排队，原3D现场未存分段记录。用定向前后对照验证限定的Host调度容量修正，不改PLC点位或公开API；若后续证据不支持调度机制，保留失败样本并回评估，不以正常一次运行推定修复。
2026-09-24 T063实施校正：定向250ms独立扫描测试保存了 `WriteHigh→WriteLowWithoutSample`、写入时门禁正常且无 `ScanHigh` 的证据；VirtualPlc须在Modbus写入事件到扫描之间锁存一次上升沿，并在扫描时重新判定门禁，反馈仍由模拟动作完成产生。Host现有写入回执仅为受理，不调整正式启动时序、期限或重发策略；修复后以定向复测和有限整组重复验证证明，真实PLC行为另验。

## 2026-09-24 最新需求与008完整执行对齐

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

复用正式Host、LatestProtocol会话/Coordinator、Motion准入、StageEventStore及外围编排。在IDetectionPort边界接入008应用执行入口，传递冻结计划/身份/点位/预算，适配器不承担重复业务调度。共享API同步前端配方引用、运行投影、允许动作及人工换面命令。

S0/S1先接入已定义F 3/4与扫码Z反馈归属、处理当前心跳阻塞、对齐公共handoff并复用下料尾段；S3再接换面/逐面目标续接，S4接输入已确认的组和旋转。F定义已解决，Host及VirtualPlc仍需实际验证；E/特殊取放/旋转等未定义信号不能由模拟器补造。不放宽3秒心跳保护。

设计前后检查按008 plan P01—P13表；来源/保存/日志/动作占用保留，具体B依赖只限制相关动作。阶段与完成条件见[008方案](../008-recipe-driven-inspection/plan.md)，本功能不另复制22套流程。历史tasks已归档，当前增量见本功能tasks；旧analysis仅历史，本次分析只读输出。

当前任务归属与顺序：参见[本功能tasks](tasks.md)文末S0—S5增量及[008任务](../008-recipe-driven-inspection/tasks.md)首批集合。共享实现只登记一个所有者；历史版本/完成证据保留原范围，最新Q/C/F规则不倒填旧任务。
# 008 第五批虚拟 Test 增量（2026-09-25）

specs/003-plc-latest-protocol T070沿用已确认产品命令2、检测1/2及本轮反馈状态机，虚拟Test目标由[008映射合同](../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)在应用层解析后传入，不新增PLC信号。T069仍须由已提交结果守卫下料、解锁、页面取盘和Final；T065以当前WPF运行验证心跳，不复用旧构建结论代替。

008自动多面查询增量：RunMediaCatalog复用已提交Media/CaptureIntent/StageEvent，追加face/round/object身份；不新增媒体库或模拟完成状态。


## 2026-09-26协议身份及适配范围

当前自动翻面按20260925分区协议§3.1.5：当前零件XYZ写0003/0005/000B→命令3→本次到位/实际XYZ核验→清命令→目标面→状态2及实际面匹配→Flip_OK=1→状态0→Flip_OK=0；料盘不翻面。逐个处理仍需后续面的实体，整体不按部位重复；同目标面的不同实体分别闭环。普通翻面协议已定义，受限项仅为实际代码能力、合法点位配置或真机标定。

同盘Sorting先源XYZ(0003/0005/000B)、命令1，状态2只取料成功；清命令后提交真实源槽位Sorting_Part_Index及目标XYZ、命令2；状态3放料完成后清命令、Sorting_OK=1，观察状态0再清ACK并提交完成。4失败、5满盘、命令3满盘报警。Sequence不是槽号；普通OK无需搬运不发取放。源/目标均属当前盘，预留与在途保留至可靠完成。

协议目标身份`plc-upper-20260925-partitioned-ack`及原件SHA需贯穿映射、适配器、反馈快照、VirtualPlc监控、日志与完成来源矩阵。两个ACK分别0054/0055，PC→PLC Int16；预留从0056开始。代码当前仍旧身份，旧通过不迁移。

## USR-20260926-D直接共享设计增量

现行故障恢复以[双端复位与完整新轮合同](contracts/recovery-test-execution.md)为准；旧日期的实现/缺口列表是当时快照，不代表2026-09-26当前能力。数据库提交核验/查询重建不等于从已提交业务边界续跑。正常暂停保留原run，人工换面采用命令默认面；故障关闭旧轮并真实初始核验后新启动，完整重新公共准备与绑定。

本功能复用原职责：003提供现有PLC复位/状态读取、特殊Test复位隔离和唯一主流程API；不创造生产寄存器，不把Ready单独当物理初始。

设计前旧故障续跑与P01/P07有冲突；本次合同替换后设计符合P01/P04/P06/P07/P08/P12/P13。实际实现和C07/F5证据仍待后续任务阶段调整与实施；生产未知特殊占用/初始安全范围仅限制对应分支，Test可按既有范围实施。tasks只读，具体唯一归属和依赖见[008计划交接](../008-recipe-driven-inspection/plan-restart-alignment-20260926.md)。

## USR-E最小共享设计（2026-09-26）

当前直接增量依据宪章7.0.0及[动作/采证设计](contracts/plc-stage-action-port.md)。复用已有公共3D/F、设备适配、独立VirtualPlc与保存，不凭源码关闭用户问题；两端XYZ/关键握手诊断与实际运行包摘要先核对。当前四面仅3＋1，代表性验证与历史Q事实分列，不恢复全Q实跑义务。源码/配置/tasks本轮未修改，USR-D链路不变。

## RES真实结果展示最小设计（2026-09-26）

003 T068先按冻结结果合同提供查询结构，再消费008 T054已提交事实。修改位置为QueryEndpoints.cs、RunSnapshot.cs及必要的现有存储读取，补普通对象面/项目关联、实际完整性、历史run只读读取与ETag变化；不另建API或状态库。接口子交付不等待006页面或008整链验收，避免循环。

依据HMI-003、DAT-004、008 FR-016及006 FR-010，唯一设计/验收与任务细化建议见[结果展示交接](../008-recipe-driven-inspection/plan-result-display-alignment-20260926.md)。本增量不新增需求，冻结契约后下一轮细化既有tasks再实现。保留USR-E六问题、四面3＋1代表集合、USR-D子交付链及RST-01/RST-02结论；VirtualPlc延迟模式不推进。宪章7.0.0 P02/P05/P07/P08/P09/P12/P13检查：后端真实事实、身份/保存、原型边界和必要代表验收满足设计约束；没有新平台或全排列测试。设计不代表代码/页面已通过。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

仅现有Host查询接线；不另建PLC驱动、存储或完成链。

## 2026-09-27 复位观察同步必要修复（原003 T072-A、008 T068/T069/T070）

必要顺序用例在Reset 202后立即Check实际返回RecoveryResetNotObserved，两次均未到链接保存注入，原r19 TRX保留。复位直接Modbus Ready已成立但缓存PlcReady仍旧false；原MotionCoordinator必要门禁不放宽。仅ResetAsync在原轮询/原期限内同时等待缓存实际PlcReady、Connected/SafetyClear，使用同一次observed快照；不改变接口、信号、初始判据或生产机械未知边界。评估见.specify/bugs/008-reset-ready-observation/assessment.md。源码当前尚未修改，待当前测试结束；现有两个保存门禁及正式旧图/完整新轮独立新包复验，已有任务承接不追加重复任务。

## 2026-09-27 T065机制修复范围（待本轮验证）

同DLL受控观察已证明Portable批队列派发延迟：Host业务/心跳txn3分别入队后1112.2005/1056.6997ms，入队仅0.006/0.0072ms，真实1秒超期且锁定；独立Native候选2937个非零操作唯一回调，1560个Host响应头最慢20.2574ms。证据入口：`.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/`，旧构建和报告保留。

最小接线为Windows、purpose=Test冻结fixture显式WindowsNativeThreadPool开关，仅本次所属Host/VirtualPlc子进程DOTNET_ThreadPool_UseWindowsThreadPool=1、inline=0；普通启动及旧冻结构建不被静默改写。三个最低线程预留位置依微软支持的实际运行配置区分Native/Portable，Native不调用不支持的SetMinThreads、不虚报预留8。正式构建不含Harmony、socket反射或诊断事件。业务API、信号、1秒I/O、3秒心跳、50ms轮询、GC、优先级、失败锁动作及未知结果不重发条件不变。

本增量沿003 T065和008 T055/T070原任务，追加任务0、勾选不变。只验证该机制路径、原期限真实超期锁动作及当前正式Q01同run前端/配方/PLC/相机算法/SQLite媒体/Final；复用未改分支历史证据。r22 HTTP独立保留，真实设备/标定仍待现场，不增加全运行时证明门槛。只有本轮验证完成后才更新验收状态。

## 2026-09-27T05:09Z 本轮验证完成状态

前述实施前待验证状态由本节接续：003 T065原Test/VirtualPlc机制/对照/安全/日志条件，以及008 T055当前正式Q01和T070适用Test对账均已满足，仅这三项授权勾选更新。新构建显式WindowsNativeThreadPool/inline0，旧默认与冻结程序不改；r22 HTTP、缺失历史日志及真实设备/标定不扩大结论。新证据目录为.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z，verification-proof.json与task-checkbox-changes.json可核对；本轮新增任务0、其他勾选不变。


## 2026-09-27 本机复测XYZ名称确认（历史，已由monitor-xyz-history取代）

历史用户确认（已被本轮XY恢复确认取代）信号名为 `XYZ_Move_Cmd`（4x0001）及 `XYZ_Pos_Confirmed`（4x0002）。此确认覆盖既有文本中的XY名称；工作区来源Word仍为原SHA，原件只读，不能把该用户增量冒称Word已改。仅公开名字统一，地址/功能码/值/方向不变；运动目标X/Y/适用Z必须全部实际写入，到位须读回本动作实际XYZ核验。不能由changes数值变化是否出现推断某轴是否发送。

按原3.1.5逐零件翻面及Flip_OK清零、3.1.6取料成功2后清命令并提交真实槽位/放料XYZ、放料成功3后Sorting_OK握手清零、3.1.7检测/F操作结束后对应Z复位及清零继续验收。F结束4不推出解码成功；UnloadPreparation命令4、普通OK留原位、NG/Pending同盘处置及特殊必要搬运不变。

此子修复由既有003映射/监控/动作验收任务承接；不追加重复任务或改变勾选。实现范围：VirtualPlc公开点名和监控消费者/必要测试，后端数值寄存器接线不变。证据目录见xyz-sorting-deployment/active-retest.json；实施及验证完成后另写同目录时间戳报告，不覆盖历史通过。

## monitor-xyz-history 本轮同步
按[003当前监控纠正](../003-plc-latest-protocol/spec.md)及其[诊断合同](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md)：公开XY名称、原列表同值XYZ、删除独立栏目；复用audit/changes，保持业务/地址/期限。此前XYZ命名条款仅限旧构建。
