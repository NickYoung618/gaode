# Bug Fix: 第一工位心跳应答延迟

- **Slug**: station01-heartbeat-response-delay（用户显式指定）
- **Fixed**: 2026-09-24
- **Assessment**: ./assessment.md
- **Status**: applied（限定代码修正已实施；原WPF/3D现场及真机尚未独立验收）

## Summary

先补有界的Host/VirtualPlc事务分段诊断。隔离修复前实例在启动期复现PLC约0.1ms完成Modbus读响应、Host读首部却等待3222.3ms；Host当时只有3条工作线程、5项待执行工作，默认最低工作线程数为2（2核）。在Host进程启动任何组件前，将工作线程最低值提高到不超过上限的`max(原最低值, 8, CPU数+4)`，避免已完成的心跳socket异步续体排在慢速线程注入后面；不增加新通信连接、不改变PLC 3000ms安全期限、I/O期限或动作语义。此修正针对新隔离样本已定位的调度机制；原Running3D现场没有保存分段日志，不能倒填为同因已证实。

## Changes

| File | Change | Notes |
|------|--------|-------|
| `backend/src/Gaode.Host/Program.cs` | modified | 启动初期限定Host工作线程最低容量并记录原/生效值；不改安全超时 |
| `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs` | modified | 心跳慢周期记录UTC、单调计时、代次、交易号、线程池/GC指标；原互锁和独立连接不变 |
| `backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs` | modified | 心跳交易成功但缓慢时分段记录门锁、连接、写请求、读首部/正文；5秒限频及抑制计数 |
| `VirtualPlc/ModbusTcpServer.cs`、`VirtualPlc/VirtualPlcEngine.cs` | modified | 对照交易接收/响应、有效echo及首次超时；慢重复信息5秒限频，不制造反馈 |
| `backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs` | modified | 间歇失败时保留心跳变化、写审计、Host诊断的有界原始上下文 |
| `specs/003-plc-latest-protocol/{spec.md,plan.md,tasks.md,contracts/virtual-plc-boundary.md}` | appended | FR03/FR15限定补充与T065待验，不改旧勾选/合同点位 |
| `specs/007-station01-integrated-loop/{spec.md,plan.md,tasks.md,contracts/virtual-integration.md}` | appended | FR-021及T029待验；不更改006/下料行为 |

## Tests Added or Updated

- `VirtualPlcLatestProtocolTests.FormalHostPortKeepsPreviousArrivalUntilNextAcceptedMoveAndBindsAfterF`：遇间歇失败时输出设备心跳变化、写审计和Host首因；不把单次成功当作稳定性证明。
- 运行原 `HeartbeatInterlockTests`、`ModbusDiagnosticsTests` 与正式VirtualPlc合同组，维持3秒安全互锁与通信故障原始上下文。
- 修复前/后使用同一007 Test配置、独立回环端口和独立SQLite启动真实Host/VirtualPlc进程；修复前失败和修复后首次就绪分别保存于下述目录。

## Local Verification

| 检查 | 实际结果 |
|------|----------|
| 修复前隔离启动 `artifacts/station01-007/heartbeat-diagnostic-20260924-0635/` | 失败；Host `transaction=5`读首部3222.3ms，VirtualPlc同事务请求接收后处理约0.1ms；Host待执行5项/线程3条，`HeartbeatStoppedChanging`锁停；原始`process.json`和双进程日志保留。此发生在建run之前，不是原Running3D样本。 |
| 修复后同配置首次启动 `artifacts/station01-007/heartbeat-postfix-01-20260924/` | 就绪、状态`connected=true/safetyClear=true`；Host记录最低线程2→8，观察到数百毫秒抖动和最长可见echo间隔约1631ms，未出现3秒报警；此进程使用诊断限频调整前的修复构建，不算最终版本页面验收。 |
| 修复前合同组 `evidence/diagnostic-plc-contracts.trx` | 6/7，间歇`HeartbeatStoppedChanging`失败原样保留；单项`diagnostic-plc-isolated-01.trx` 1/1、第二整组`diagnostic-plc-contracts-02.trx` 7/7，不以重跑成功抹掉失败。 |
| 最终构建 `evidence/final-build/` | Host、VirtualPlc Release构建各0警告/0错误；Host SHA-256 `42ACEFA574206F83DB0DD4DF4B67D032A5E00A80925DD20600C8030AAD1C78C8`，VirtualPlc SHA-256 `C23A1F7529FCFDF2CEAB8C27E943B26113DC5F5C0598646332962B809C1165B6`。 |
| 最终相关合同组 `evidence/final-heartbeat-contracts.trx` | 12/12通过。包含真实心跳中断的既有定向互锁合同，但尚待独立进程/页面复核。 |
| 输出目录占用 | 一次中间构建因本轮自建Test Host占用DLL失败，核验PID后仅停止这组自建进程；原现场及其他会话未动。最终新输出目录构建成功；失败不计产品回归。 |

## Deviations from Assessment

- 评估时具体根因未知。新增诊断在**另一个隔离启动样本**定位到PLC响应后Host读取续体长等待与工作队列积压，因此扩展修复文件至`backend/src/Gaode.Host/Program.cs`，先同步003/007限定文档，再实现。`backend/tests/Gaode.Integration.Tests/AssemblyInfo.cs`原已在测试进程将最低线程设为16，解释为何现有集成测试不等同真实Host启动容量验证；本轮未改该历史测试配置。
- 原现场3516ms仅有PLC接收两次echo及中间请求翻转，未存该窗口的Host交易/线程池分段，因此“原样本具体延迟段=Host调度”只能作为有依据的同机制候选，不能写成已直接证实。评估的其他候选仍需原入口复测时核对。
- 未更改公开状态字段、PLC来源协议或采集/算法步骤；无需修改006消费者合同。日志仅对异常窗口输出并限频。

## Follow-ups

- 立即以 `$speckit-bug-test slug=station01-heartbeat-response-delay` 在最终构建的独立Test/VirtualPlc与实际WPF/WebView2原入口做有限复测，保存截图、请求链、事务/PLC变化、SQLite和完整进程哈希；若无法操作桌面则如实partial。
- 独立验证真实超过3秒中断仍报警和锁动作；别把Test自动模拟取盘写成真人。T065/T029及原始Running3D故障只按实际证据判定，不动T088、下料或历史心跳结论。
