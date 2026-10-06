# 第一工位完整主流程验证指南

## 验证目标

证明同一 `runId/trayId` 从唯一 Host API 入口，真实经过公共准备、配方计划/绑定、持久化 handoff、Detection、Sorting、UnloadPreparation、整托完成、解锁观察、人工移除确认，最终提交 `FinalUnloadCompletion`。不得用预造计划、直接跳阶段、改前端状态或写数据库制造完成。

## 固定环境

- 真实构建并独立启动的 Gaode Host 进程。
- 仓库既有 VirtualPlc 独立进程，通过正式 Modbus TCP 接入。
- 真实 SQLite 文件和正式事件/投影/完成存储。
- 明确标记 `Simulated/Test` 的相机、光源和算法正式端口适配器；禁止隐藏 fallback。
- 测试客户端只调用 Host API；不得直控 PLC/相机/算法或写业务数据库。
- 不构建、不运行、不依赖 006 页面、`frontend/src`、`frontend/tests` 或客户原型测试；它们不是五场景的前置条件或验收证据。

进程内 SimulatedPlc、单元测试和源码检查只能作为分层证据，不能替代以上最低 E2E。

## 构建与启动命令

从仓库根目录先执行：

```powershell
dotnet test backend/Gaode.slnx
dotnet build VirtualPlc/VirtualPlc.csproj
```

调试时可在两个终端分别启动；端口和数据库位置必须来自本次测试配置并写入 manifest，不能依赖未记录的本机默认值：

```powershell
dotnet run --project VirtualPlc/VirtualPlc.csproj
dotnet run --project backend/src/Gaode.Host/Gaode.Host.csproj
```

现有验证入口可用 `python scripts/verify-latest-plc.py --empty-store <evidence-root>` 启动基线联调，但它目前只覆盖公共准备等局部路径，不能产生本期通过结论。实施任务须在同一脚本增加显式五场景选择（例如 `--scenario normal|algorithm-pending|unlock-gates|plc-unknown|host-restart|all`）；该参数和完整证据未实现前，对应场景必须标记 NotRun，不得手工补写 passed。

## 正常闭环步骤

1. 启动 VirtualPlc，记录 PID、命令、地址、UnitId 和日志路径。
2. 启动 Host，记录 PID、命令、配置摘要、数据库路径和适配器 Real/Virtual/Simulated 矩阵。
3. 通过 `POST /api/v1/station01/runs` 发送版本化 StartRunContext，保存 202 回执；不得调用第二个启动入口。
4. 轮询运行查询并保存通知，观察同一身份依次完成启动夹紧、3D、F、RecipeRunPlan 构建/绑定与 handoff 提交。
5. 确认 Host 从该已提交 handoff 自动进入 Detection；没有预造 plan/request 或直接跳到 003。
6. 检测结果提交后，先观察UnloadPreparation本次XYZ到位，再按适用对象完成同盘Sorting取/放及ACK；普通OK无需搬运无取放命令。
7. 确认聚合事务提交 WholeTrayCompletion 后才出现解锁 Cmd=0，并保存 `ObservedUnlocked`。
8. 测试客户端调用人工移除确认 API；确认人工事件与 FinalUnloadCompletion 原子提交。
9. 运行查询终态为 `FinalUnloadCompleted`，并能追溯所有事件、引用、source/quality 和 revision。

## 五类必跑场景

| 场景 | 注入/动作 | 必须观察到 |
| --- | --- | --- |
| 正常完整闭环 | 正常 VirtualPlc + Simulated/Test 算法 | 同 run/tray 连续到 FinalUnloadCompletion；完成前无解锁 |
| 算法失败→Pending | 让算法有限尝试均失败/超时 | 最多 3 次总尝试、2/5 秒退避、按冻结RecipeExecutionBudget确定的 deadline 不重置；保存原错误；对象 Pending；完成映射和 Pending 分拣后继续 |
| 门禁拒绝 | 在 WholeTrayCompletion 前触发/检查解锁；ObservedUnlocked 前提交人工确认 | 前者无非法 Cmd=0，后者 409/受限；无伪造完成 |
| PLC 断联/epoch 变化 | 在可能已派发动作后断联或重连 | 对应物理阶段 UnknownHeld；动作计数证明未自动重发 |
| Host 重启 | 在已提交边界及在途无终态窗口分别重启 | 仅重建已提交查询；物理未知不自动执行，双端复位且初始状态成立后显式新run完整重做公共准备及流程，旧故障保存可查 |

MappingFailed 另作为合同/集成测试覆盖：对象身份、位置或目标歧义必须暂停，不得错误转 Pending 或继续分拣。

派发前临时通信错误另用可控时钟合同/集成测试验证：最多 4 次总尝试、1/2/4 秒退避，与算法重试共用阶段开始冻结的 按冻结RecipeExecutionBudget确定的 deadline。物理动作可能已派发时断言零自动重发并进入 UnknownHeld；不为此新增第六个 E2E 场景。

## 证据目录

每个场景独立保存到 `artifacts/plc-latest/<evidence-run-id>/whole-tray/<scenario>/`，至少包含：

- `manifest.json`：场景、commit/build/config/contract/protocol 摘要、run/tray、时间、文件哈希和证据引用；它只作索引；
- `process.json`、`host.log`、`plc.log`：进程命令、PID、端口、退出码和原始日志；
- `api-transcript.json`：启动、查询、人工确认请求响应与幂等键；
- `modbus-audit.json`：地址、方向、值、时间、operation/epoch 关联；
- `sqlite-events.json`、`sqlite-projections.json` 及真实 SQLite 文件/摘要；
- `source-matrix.json`：Host、PLC、Camera、Light、Algorithm、ManualActor 的 source/quality/version/evidence reference、矩阵 digest 和 `SoftwareLoopOnly` 结论；
- `final-result.json`：Passed/Failed/Blocked/NotRun、理由和全部证据引用；
- `whole-tray-completion.json`、`unlock-evidence.json`、`manual-final.json`、`run-snapshot.json`、合同/集成测试结果和 TRX。

正常场景必须证明无启动 `Pallet_Lock_Cmd=1`、WholeTrayCompletion 前无 Cmd=0。异常场景必须保留 errorCode、source/quality、尝试和期限证据。历史 `Sorting=NotStarted`、`WholeTask=NotCompleted` 的 passed 结果不得复用为本期成功证据。

## 判定边界

- Virtual/Simulated 通过只证明软件纵向切片和协议适配闭环，不证明真实 PLC、相机、算法或现场生产验收。
- 任一必需进程、场景或证据未运行时标为 Blocked/NotRun，不得推断 Passed。
- 本指南不要求修改客户确认原型；人工确认由联调客户端调用 Host API。生产页面入口 Deferred。
- 006 是否实现或对齐不影响本指南执行；测试客户端不是生产 UI，也不得伪装为客户确认原型。
