# 第一工位完整主流程合同索引

## 协议与业务边界

- 第一工位唯一外部入口为 `POST /api/v1/station01/runs`；202 仅表示持久受理。
- 同一 `runId/trayId` 完成启动夹紧、3D、F、配方计划/绑定、001→003 handoff，再自动进入整托流程。
- `WholeTrayWorkflowStage` 与 `PlcWorkflowStage` 分离；Detection 不是 PLC 动作。
- `WholeTrayCompletion` 只授权解锁；`FinalUnloadCompletion` 才是完整主流程成功。
- 算法异常有限收敛为逐对象 Pending 并继续；映射歧义暂停；PLC 物理结果未知进入 UnknownHeld 且不自动重发。

## 合同清单

| 合同 | 作用 |
| --- | --- |
| [station01-main-flow-api.md](./contracts/station01-main-flow-api.md) | 启动、查询、命令和人工移除确认 API |
| [public-preparation-handoff.md](./contracts/public-preparation-handoff.md) | 001→003 持久化交接与连续身份链 |
| [whole-tray-workflow.md](./contracts/whole-tray-workflow.md) | 整体状态顺序、门禁和成功判定 |
| [detection-port.md](./contracts/detection-port.md) | Detection 请求、逐对象结果、Pending 与映射 |
| [plc-stage-action-port.md](./contracts/plc-stage-action-port.md) | Sorting、UnloadPreparation、UnlockObservation |
| [stage-events.md](./contracts/stage-events.md) | 追加事件、投影、事务、恢复和完成证据 |
| [component-source-matrix.md](./contracts/component-source-matrix.md) | Host/PLC/Camera/Light/Algorithm/ManualActor 组件来源矩阵 |
| [status-notifications.md](./contracts/status-notifications.md) | 提交后通知与 GET 对账 |
| [virtual-plc-boundary.md](./contracts/virtual-plc-boundary.md) | 独立 VirtualPlc 的权限和证据边界 |

## 跨 feature 合同门禁

003 定义 `s01-handoff/2.0`、`station01-main-flow-api/1.0`、`station01-status-notification/1.0` 和差异记录。2026-09-23有限授权允许同步001 handoff直接相关产物；006仍不修改。

- 001：2026-09-23已有限授权并完成 handoff v2 直接相关 spec/contracts/plan/tasks 对齐；实际 producer/consumer 接线和验证仍由003 T024/T026完成。该状态不阻塞其余003工作，也不等同测试通过。
- 006：其规格、代码和测试变更需要单独授权；是否对齐不阻塞任何 003 后端工作或五场景 E2E。

不得静默改旧合同，不得把前端内容追加到 003，也不得修改客户原型。

## 明确非合同能力

Virtual/Simulated 证据不等于真实设备或生产验收；通知不等于业务事实；前端状态不等于后端完成；裸 bool、非空 GUID、测试脚本预造计划或数据库改值均不能授权解锁或完成。

## 2026-09-24 008合同适用增量

Detection仍不是单一PLC枚举动作，但008完整Detection内部包含经唯一Motion/设备端口执行的实际运动；不能把本索引旧简写解释为整阶段无物理副作用。最新执行/结果/预算/证据规则见[008执行合同](../008-recipe-driven-inspection/contracts/execution.md)；本目录对应合同已有同日增量。来源为REQ第7/11章，原阶段和下料已确认边界保留。

## 2026-09-27 T065机制修复范围（待本轮验证）

同DLL受控观察已证明Portable批队列派发延迟：Host业务/心跳txn3分别入队后1112.2005/1056.6997ms，入队仅0.006/0.0072ms，真实1秒超期且锁定；独立Native候选2937个非零操作唯一回调，1560个Host响应头最慢20.2574ms。证据入口：`.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/`，旧构建和报告保留。

最小接线为Windows、purpose=Test冻结fixture显式WindowsNativeThreadPool开关，仅本次所属Host/VirtualPlc子进程DOTNET_ThreadPool_UseWindowsThreadPool=1、inline=0；普通启动及旧冻结构建不被静默改写。三个最低线程预留位置依微软支持的实际运行配置区分Native/Portable，Native不调用不支持的SetMinThreads、不虚报预留8。正式构建不含Harmony、socket反射或诊断事件。业务API、信号、1秒I/O、3秒心跳、50ms轮询、GC、优先级、失败锁动作及未知结果不重发条件不变。

本增量沿003 T065和008 T055/T070原任务，追加任务0、勾选不变。只验证该机制路径、原期限真实超期锁动作及当前正式Q01同run前端/配方/PLC/相机算法/SQLite媒体/Final；复用未改分支历史证据。r22 HTTP独立保留，真实设备/标定仍待现场，不增加全运行时证明门槛。只有本轮验证完成后才更新验收状态。

## 2026-09-27T05:09Z 本轮验证完成状态

前述实施前待验证状态由本节接续：003 T065原Test/VirtualPlc机制/对照/安全/日志条件，以及008 T055当前正式Q01和T070适用Test对账均已满足，仅这三项授权勾选更新。新构建显式WindowsNativeThreadPool/inline0，旧默认与冻结程序不改；r22 HTTP、缺失历史日志及真实设备/标定不扩大结论。新证据目录为.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z，verification-proof.json与task-checkbox-changes.json可核对；本轮新增任务0、其他勾选不变。


## 2026-09-27 monitor-xyz-history 当前协议名称

最新用户确认信号名为 `XY_Move_Cmd`（4x0001）及 `XY_Pos_Confirmed`（4x0002）。本次纠正确认取代此前XYZ公开命名要求，恢复来源Word的XY名称；工作区来源Word仍为原SHA，原件只读，不能把该用户增量冒称Word已改。仅公开名字统一，地址/功能码/值/方向不变；运动目标X/Y/适用Z必须全部实际写入，到位须读回本动作实际XYZ核验。不能由changes数值变化是否出现推断某轴是否发送。

按原3.1.5逐零件翻面及Flip_OK清零、3.1.6取料成功2后清命令并提交真实槽位/放料XYZ、放料成功3后Sorting_OK握手清零、3.1.7检测/F操作结束后对应Z复位及清零继续验收。F结束4不推出解码成功；UnloadPreparation命令4、普通OK留原位、NG/Pending同盘处置及特殊必要搬运不变。

此子修复由既有003映射/监控/动作验收任务承接；不追加重复任务或改变勾选。实现范围：VirtualPlc公开点名和监控消费者/必要测试，后端数值寄存器接线不变。证据目录见xyz-sorting-deployment/active-retest.json；实施及验证完成后另写同目录时间戳报告，不覆盖历史通过。
