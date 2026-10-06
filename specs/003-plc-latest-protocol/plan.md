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

003 是第一工位后端主流程 feature，只实现单一 Host、Domain/Application、正式设备与算法端口、SQLite、后端 REST API、SignalR 合同，以及由测试/联调客户端驱动的独立进程 E2E。`POST /api/v1/station01/runs` 保持唯一入口；Host 在同一 `runId/trayId` 下完成配置冻结、公共准备、配方计划/绑定和已提交 handoff，再自动续接 Detection、Sorting、UnloadPreparation、整托完成聚合、解锁观察、人工移除确认和最终完成。`202 Accepted` 不是完成，只有已提交的 `FinalUnloadCompletion` 是成功终点。

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

状态主线：`Accepted → PublicPreparation → HandoffReady → Detection → Sorting → UnloadPreparation → ReadyForUnlock → ObservedUnlocked → AwaitingManualRemoval → FinalUnloadCompleted`。可能已派发的 PLC 动作无可信终态时进入当前阶段 `UnknownHeld`，禁止自动重发。

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
3. Detection、Sorting、UnloadPreparation 的 Completed 均已提交后，独立聚合短事务重新核验身份、plan revision、组件矩阵和未解决终态，再原子写 `WholeTrayCompleted`、`WholeTrayCompletion` 与整托投影。
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
4. 接通唯一入口、Detection、Sorting、UnloadPreparation、聚合、解锁、人工确认和最终完成。
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

下料使用本次用途的合法配置及可靠轴反馈。普通盘末Detection→适用Sorting及必要保存→UnloadPreparation→允许取盘/人工取盘→Final；到位、允许取盘、人工确认和保存是独立事实。不能伪造新协议未定义的旧锁紧/解锁信号，地址/恢复/安全延期不猜值。

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

逐对象按配置取件点定位→PLC按产品型号/目标面翻转→配置放回点定位→放回；相关对象放回后统一3D姿态复查，正常继续、异常跳过后续检测、最后原槽Pending分拣并返回物理槽号，F不重绑。料盘不翻面，整体不按部位重复动作，不沿用旧Flip_OK或独立实际面号前置。

同盘分拣：OK留原槽不搬，NG/Pending各去对应区配置目标，姿态异常跳过后续检测，最后从原槽实际分拣到Pending；源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升。真实取料反馈和在途保存先于放料；物理槽号不是Sequence，预留/在途持续到可靠完成。新状态1/2/3只由通信翻译为取料成功/放料完成/失败，旧状态2/3及ACK不再适用。

当前通信依据为20261001 Word与信号表，哈希及延期差异见011 spec；旧协议身份/地址/ACK仅属历史原件和旧运行，实施须迁移映射、适配器、VirtualPlc与通信断言，业务不感知原码。

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

现行验收按新接口语义：逐件翻转/另行放回后姿态复查，F不重绑；取料反馈及真实在途保存先于放料，分拣完成后下料。通信原码由通信测试验证，业务测试不保留旧ACK/原码条件。

此子修复由既有003映射/监控/动作验收任务承接；不追加重复任务或改变勾选。实现范围：VirtualPlc公开点名和监控消费者/必要测试，后端数值寄存器接线不变。证据目录见xyz-sorting-deployment/active-retest.json；实施及验证完成后另写同目录时间戳报告，不覆盖历史通过。

## monitor-xyz-history 本轮同步
按[003当前监控纠正](../003-plc-latest-protocol/spec.md)及其[诊断合同](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md)：公开XY名称、原列表同值XYZ、删除独立栏目；复用audit/changes，保持业务/地址/期限。此前XYZ命名条款仅限旧构建。


## 2026-09-29 公共解锁记录与测试监控布局（r9）
依据本轮用户确认及上传write155/transaction6001：Pallet_Lock_Cmd=0实际写入必须显示，即使数值相同；命令受理与Pallet_Lock_Status=0反馈分开，不改变业务握手。复用audit/2.0，命令以实际写入为唯一来源；没有前值不补造0→0，拒绝明确拒绝；状态仍用changes，动作引用仅在同连接真实审计匹配时关联。其他握手/清零、XY公开名称及完整XYZ规则保持。
虚拟PLC只读页面独立布局见[测试监控页面设计](monitor-test-workspace.md)：原历史列表为首屏主区域，点位侧栏可收起；搜索/方向筛选、暂停查看继续接收、恢复最新、去重/有界缓存/缺口提示。此页面不是006客户原型，不修改正式业务前端或协议来源。

本轮打包实际采用packaging/windows-local-20260927/build.py显式--base-package冻结r8-minimal、archive.py合成完整ZIP并校验全部载荷；覆盖暂存不作为运行目录。保留已存在的Start-Test/Select-Test/测试配方清单及30个入口。最终已验证范围仅监控及资源，见003/evidence/008-action-diagnostics.md。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

正式接线保持原Host与运动/保存所有者，按以下已对齐职责实施：

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

可靠目标采样与抬升后完成位置分开，不新增设备信号或强求完成时Z等于目标。通信先经正式StartAsync定义校验及同实例动作准入；坏映射不允许初始化/动作写入。线缆条款仍有效但仅通信侧解释，业务StageAction结果删除ProtocolStatus/内部PositionEvidence.Phase。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

新事实采用device-semantics/1，保存实际语义/关联/来源/质量/观察及opaque引用，禁止硬写protocolStatus=2或Virtual/Derived来源。旧PayloadJson字节不重写；未知原始来源不补造。业务实际commit与调用方有效回执分别记录；查到行不能改变超期动作资格。原事件/幂等/投影同短事务、WholeTray及Final独立提交不变。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

IntegratedDetectionPort虽位于Simulation目录仍是正式文件相机/Worker消费方，不得引用具体协议类型或协议版本作为业务输入；通过语义采集窗口及当前结果保留每图/融合/媒体/算法和保存节点。算法有限失败Pending不替代机械安全/未知保持。公共3D受控失败清理是原特例，F/E/产品检测不得自动继承。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

普通路线依实际顺序Detection→UnloadPreparation→适用Sorting→WholeTray真实保存→可靠ObservedUnlocked→人工确认与Final真实提交。WholeTray引用必须在解锁前重新核验；单一动作Completed、传输回执或解锁均不等于Final。取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

同Host自动续接只在本次所有必要回执及时有效后；已有handoff行只证明保存事实，不能让Query/后段在迟到、取消或重启后重建Bound/Ready资格。保留当前run/tray/plan/对象/面/初始测量/源槽及参数快照；独立API本次绑定意图与事实需保存但不重建旧handoff。当前有效早到回执不因稍晚调度倒判，下一动作仍核原截止/取消/安全。ReceiptObserved有界后置审计不预填自身回执、不递归授权。

维持USR-D：故障旧轮关闭，双端复位及实际初始状态核查后显式新run，从公共3D/F及绑定重新开始。原状态/握手由通信内部解释为复位/初始可靠结果；业务不用raw判断清零。旧事务提交核查只是事实核查，不续跑、不重发未知物理动作；已观察取料但raw失败按已存意图/预留保持占用，数据库全不可用不保证新事实持久。正常暂停及人工换面仍按原合同，人工命令默认面来源不得冒充PLC实测。

各组件provider/version?/quality从实际生产者透传，Real/Virtual/Simulated/Unavailable与历史声明性质分离，不按Host模式或新语义反推历史raw/执行来源。新设备事实关联同次ObservationId及真实提交后opaque引用；没有实收报文时raw缺失如实null/NotRecorded，旧合成值不变成ActualWire。原Host/PLC/Camera/Light/Algorithm/ManualActor完整矩阵及SoftwareLoopOnly限制保持。

代码前置：009 T012实际跨功能对齐复核完成，随后严格按tasks各项依赖；不把本节当代码已经交付。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready必须核本次有效RecipeApplicationReceipt，不能从已有handoff恢复。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

006尚未交付可推进后端子集验证，但不签009完整基线。

实施先实际完成本功能共享合同对齐，再经009 T012职责复核，才修改对应共享代码。当前只完成文档接口决定，新增生产/消费能力和真实验收未完成。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。

实施先实际完成本功能共享合同对齐，再经009 T012职责复核，才修改对应共享代码。当前只完成文档接口决定，新增生产/消费能力和真实验收未完成。


### 009 独立绑定保存的实施细化（2026-10-01）

依据009 B03.2/FR-035—039：独立绑定读取关联运行已提交的冻结配置和既有handoff，不创建新运行或重建handoff。旧v1公共准备的Completed/CompletedWithExceptions连同Run.State/Revision/TerminalRevision及旧handoff/payload保持不可变；不改TR_Run_TerminalImmutable，不扩大本次schema升级。独立入口的RecipePlanAndBindingIntent、RecipePlanBound及ReceiptObserved使用既有IStageEventStore的有限RecipeApplication业务分类，真实EventId/Sequence/PersistedAt作为本次保存回执；沿用当前run/tray/plan/绑定动作身份。该分类仅记录本次配方应用，不是新的工艺阶段或动作端口。无完整已存身份时拒绝，不合成tray。取消运行拒绝；记录提交不恢复旧动作或生成产品续接许可。

连续链仍使用原Run保存通道；独立入口由业务保存适配提交真实StageEvent事务，不让通信接管数据库。窗口包含这次意图后设备、raw和绑定事实；每次保存同受CriticalSave/剩余总窗，ReceiptObserved仍非递归批准链。实际EventId也是历史引用的明确类型，不能拿它冒称Writes表行。重复本次WriteId只核原事件，不自动重发设备。

实施与验证归属009 T032/T037/T039/T040/T043—045：Codex执行，真实SQLite核三类新记录及原Run/旧handoff字节不变；历史查询须同时读RecipeApplication分类并明确event引用。首次试作Run追加被实际TerminalImmutable拒绝（binding-terminal-01，2失败）；该试作已撤回，约束未放宽。文档对齐不表示最终实现或运行通过；不改变历史任务勾选。


### 009 检测保存窗口接线补充（2026-10-01）

本功能检测入口按[003检测合同具体接线](../003-plc-latest-protocol/contracts/detection-port.md)传递本轮冻结 CriticalSaveBudgetMs 与原 Detection 截止；必要保存共受两者约束，不由固定两秒或协议步骤推导。算法意图与派发共享真实会话/时钟/起止时间。实现及验证归009 T035/T048/T049，先前任务勾选保持；本节不声明运行通过。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。

当前来源分类的有限补齐：ResultSource在末尾新增Test，保留既有Real/Virtual/Simulated/Fallback的值和历史含义；仅由明确声明Test的实际算法生产者产生，不从RunPurpose猜测。IntegratedDetection的Source与意图来源来自IAlgorithmPort.Origin，未知仍Fallback/Unknown；完整矩阵继续使用AlgorithmOrigin与实际事实。该变化用于消除把独立Test Worker写成Simulated的固定标签，归009 T034/T035/T039及T043—T046，旧记录不重写、现有页面仅绑定来源。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`仅作为旧payload中的原文保留，不生成同名新业务事实，不倒推历史原始值或来源。消费者不以旧名字/裸kind授予动作；当前面关联继续调用FaceEstablishment.Confirms，原证据与保存门禁不减。通信用例仍检验实际清零，业务断言迁移到当前语义事实和来源，两侧均必需；不新增页面、信号、恢复路径或产品兼容层。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。


### 009 T040/T041 有限独立进程保存故障接线（2026-10-02）

按009 VG V07/V09，在真实Host独立进程中增加可选`Gaode:TestPersistenceFaultCase`，仅在`VirtualPlcIntegration`、Virtual PLC及全部Test配置下接受。固定值为F05-A/B/C、F06-A/B/C、BA04-late-bound/late-handoff；未知值或其他运行环境启动拒绝，不新增业务API。未配置时不安装任何拦截器。Test根中的`009-fault-arm.json`以caseId、runId和nonce选择本次真实运行；独立编排取得正式启动runId后写入，未命中不能当故障验证通过。唯一命中记录实际EventId/WriteId/EvidenceId、时刻、位置和commit事实到同根`009-fault-events.jsonl`。

A通过实际SQLite触发器拒绝相应插入，原Store负责事务回滚与结果；B仅在真实commit完成后扣住回执；C在同一实际边界另持SQLite独占锁暂阻核查。通信raw故障仍经TraceWriter实际job，取料业务事务仍由StageEventStore承担。BA04严格选择本run的RecipePlanBound或本次handoff，不拦其他保存。Test专用保持窗口最多30秒，或收到匹配nonce的`009-fault-release.json`结束；这个注入外限不是业务期限，保持期间原预算/保存期限照常失效，释放后不能复活动作。

C使用受控Test副本的DELETE日志模式取得真实排他锁；A/B不伪造数据库结果，记录存储不可用时仍仅尽力写诊断文件。009验收必须另查实际SQLite、当前回执、原截止、PLC全部后继写和保守占用；故障日志及进程存活不能独自证明验收。此处只完成必要接线接口对齐；代码及实际运行由009任务证据确认。实施/复核角色为Codex，不冒称客户批准，其他功能历史任务状态不变。


### 009 T033/T039 后段退出后的故障保存版本交接（2026-10-02）

integration238的实际三阶段UnknownHeld案例已提交检测事实至Run revision43，公共准备RunExecution仍持旧revision，故障收尾保存被CAS拒绝。仅在已等待后段执行返回UnknownHeld/解锁失败、当前执行停止后，故障协调器通过既有ITraceQuery有界读取同run的已提交状态，核对runId/requestId/subjectId/context、无终态、版本不回退且不存在活动配方应用保存窗口，再把现有运行保存游标交接到真实已提交revision。随后原CriticalSave与CAS不变；不循环重试冲突，不重放物理动作，不把只读核查或迟到记录当绑定、取放或后继动作成功许可。

原始故障ErrorCode及UnknownHeld事实保留，FaultRequiresNewRun仍作为恢复规则/事件和旧continue拒绝码，不覆盖已记录的设备故障原因。查询/保存失败仍无成功保存声明，不调整预算或重启规则。实施归009 T033/T039，验证保留原ThreeStageMainFlowIntegrationTests未知保持与零WholeTray断言，并补游标交接拒绝条件；当前修复未运行验证，不改其他功能历史任务状态。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 013实施前定向同步（2026-10-04）

SY-01：FR03/FR15当前采集按013 A01/A03/A07：独立心跳300ms；基础活动200/空闲500ms、动作反馈200ms、位置运动或未知500/确认静止1000ms。首Moving/Executing仅有依据的局部50ms、齐备后200ms。必要读写/清零即时，不等慢周期；相对plannedDue计调度迟延。原I/O、3秒、动作/保存截止及T065真实超期锁动作不变。旧T065“50ms不变”为当时构建事实，不再约束013现行策略；新周期不代替真实反馈或当前安全证据。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

### 010实施定向对齐 A02（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A03/A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。
- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
