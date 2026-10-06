# 008 首批心跳核对（2026-09-25）

状态：**003 T065 保持未完成**。沿用 `.specify/bugs/station01-heartbeat-response-delay/{assessment,fix,test}.md` 的旧证据和最小 Host 线程池修正，不覆盖旧失败，也不延长正式 3 秒互锁。

本轮在正式 PLC 适配器启动处也设置与 Host 一致的最小工作线程容量并记录前后值，使组件调用与 Host 入口遵守同一调度条件；不改变 Modbus 独立连接、I/O期限或心跳门限。修正后 `backend/tests/Gaode.Contracts.Tests/TestResults/heartbeat-f-hold-current.trx` 6/6（含 F 正常、超时互锁和 Modbus 诊断）。这只是定向合同对照，尚没有当前构建的正式页面与独立进程故障前后对照。

独立进程部分核对见 `artifacts/recipe-execution-008/fixture-tooling-smoke/runtime-current/process.json`、`logs/host.out.log`、`logs/plc.out.log` 与 `heartbeat-observation.json`：当前 Debug Host/VirtualPlc/worker 使用独立端口和 SQLite，启动时 Host/PLC 适配器均记录最低工作线程 8；启动就绪后约 15 秒单次观察 VirtualPlc `communicationTimedOut=false`，期间无业务动作，也没有页面。这能验证当前构建的基础启动与日志接线，不能证明间歇缺陷消失或抵扣旧页面 Blocked。采样结束后仅停止本次组件。

进一步回读 007 **当前构建页面失败包** `artifacts/station01-007/t015-wpf-corrected-20260924-182854/`：Host 于 10:29:32.123Z 写出心跳 echo transaction 191；VirtualPlc 于 10:29:33.314Z 才收到，并在约 0.5 ms 内响应。Host 该交易 `responseHeaderMs=1299.2`，独立 Modbus I/O 期限 1000 ms 先到，随后安全锁停；当时 Host 最低工作线程 8、VirtualPlc 最低值 2。该包**不是 3 秒心跳边沿期限触发**，也不是 F/3D 业务负载中的故障。原始 Host/VirtualPlc 诊断窗口及页面 Blocked 事实均在上述包内，不改写为先前修复已覆盖。

针对这段已观察到的 VirtualPlc 接收调度延迟，`VirtualPlc/Program.cs` 在进程启动时设置与 Host 相同的最低工作线程容量，并记录原值/生效值；不改 1000 ms I/O 期限、3 秒互锁、PLC反馈或通信重发。修正前另一次本批正式 API 运行 `artifacts/recipe-execution-008/heartbeat-current-api-1/heartbeat-run-summary.json` 已到 `AwaitingManualRemoval`、无心跳失败，说明缺陷间歇性存在，不能把任何单次成功当因果证据。修正后同类独立 Test/VirtualPlc 正式 API 运行 `heartbeat-current-api-2/heartbeat-run-summary.json` 亦到 `AwaitingManualRemoval`，`communicationTimedOut=false`、心跳失败 0，VirtualPlc 日志确认最低值 2→8；两包均通过辅助 API 启动旧 007 公共流程，**没有正式前端，也不是 Q01**。进程按各自 `process.json` 身份核对后停止。

本批现行合同组 `backend/tests/Gaode.Contracts.Tests/TestResults/008-foundation-contracts.trx` 9/10；正式 Host 适配与 VirtualPlc 在混合负载中再次发生 `HeartbeatStoppedChanging`。原始日志显示 Host 心跳 transaction 22 的 Modbus 响应首部读取约 2042 ms，Host 当时 4 条线程、待执行 7 项；该合同实例单独设置的超期门限是 2000 ms。失败锁住 3D 移动，未报告运动完成，符合安全阻断。先前本轮另一合同运行的终端原始输出也记录约 2.47 秒轮询调度间隔。隔离 F 正常测试 `f-handshake-current-pass.trx` 1/1 不能抵扣这些失败。

旧 bug-test 的三次旧构建页面完成和 3031 ms 安全锁停证据继续有效于其版本；007 当前构建页面 `evidence/t015-t044-20260924-current-validation.md` 的历史包仍为点击前 Blocked。本轮未执行新页面或产品运行，也没有取得原 3516 ms 故障时缺失的 Host 分段。由于修正前后辅助 API 样本均正常、且当前页面未重新运行，现有证据只能支持针对 transaction 191 分段的最小修正和有限回归，尚不能证明当前正式入口的间歇故障已解决；T065 保持未勾选，007 T029 也不自动勾选。

本轮核对 `quser`：可交互的 administrator Session 2 当前为 `Disc`。后续正式页面对照需在可操作的隔离交互会话中用当前构建单击原入口并保留完整失败或成功包；不得用上述辅助 API 运行代替该页面事实。此 UI 前置只限制 T065 的最终复核和后续 Q01 页面验收，不限制 Q01 受限配方数据、worker 或 API 合同准备。

## 第二批页面复核（2026-09-25）

再次核对 `quser`：仅有 administrator 会话 ID 2，状态仍为 `Disc`，没有可操作的独立交互桌面；核对 5000—5300 端口时也无既有 Host/VirtualPlc 监听。本批未启动 WPF、未点击正式页面，故**当前页面心跳仍为 Blocked/未复核**。`artifacts/recipe-execution-008/Q01-fixture-second-batch-runtime/` 只证明独立组件可启动及受限目录可查询；它使用 `-SkipDesktop`，没有业务 POST 和页面动作，不能抵扣 T065。保留 007 原失败包及上述混合负载失败，不宣称间歇故障消失；T065 继续未完成。

第三批再次`quser`：administrator会话2仍为`Disc`。仅执行无页面的定向构建、媒体及PLC组件测试；未启动WPF或服务，不声称当前页面心跳恢复。T065和007 T029原状态不变。

## 第五批当前构建页面负载

Session 2 后续为Active，隔离Test运行先保留了数个失败包：CDP就绪窗口、已受理后的Modbus超时、以及源点ID不匹配的MappingFailed；均在`artifacts/recipe-execution-008/fifth-batch/`，未用旧构建成功抵扣。修正Test专用轮询/扫描调度与当前构建采证器后，Q01、Q02连续按正式WPF页面操作到Final；各成功包`logs/host.out.log`无`PLC failure latched`，`page-api-device-facts.json`和`verified-facts.json`核对同run PLC变更无间隙。原3秒互锁、独立连接和1秒I/O期限未放宽。见[完整证据](../../008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md)。这证明当前两次负载可用，但原T065要求异常窗口的Host轮询、VirtualPlc接收/处理/响应和有效应答的有界前后对照，且早期历史窗口缺日志，仍不勾T065或007 T029；不能宣称间歇延迟机制已彻底修复。


## 2026-09-27 008直接依赖当前引用

008所用Test能力及正式页面/必要门禁的当前证据见008/evidence/completion-review.md和task-audit-night-20260927.md最新节；r21复位状态同步必要用例与r22完整新轮/Q18真实DLL通过，r20真实401/403页面正确拒绝且无业务记录。T069唯一当前旧图/完整新轮主包为r22 job002；r18旧主包保持原构建事实。本文不修改本功能父任务勾选或补造历史分段日志，T065延迟机制原条件仍未全齐，Default/生产根因及其他父条件不由008选定成功代表抵扣。
