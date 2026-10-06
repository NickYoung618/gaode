# Bug Assessment: 第一工位心跳应答延迟触发通信报警

- **Slug**: station01-heartbeat-response-delay
- **Created**: 2026-09-24
- **Source**: 用户现场报告及只读保存的 Test/VirtualPlc 实例 `artifacts/station01-007/manual-20260924-060537-8b0db3fa`
- **Verdict**: valid
- **Severity**: high — 实际WPF入口的运行在3D/算法阶段被PLC通信安全报警阻断；安全门禁生效，但正常Test主流程不能继续。

## Report (summarized)

现场一次页面启动以 `requestId=s01-007-8b1eb0d37ff64cd0af6036f49bd5e599`、`runId=29e66a01-c5b1-4a29-8e0d-193ee1a791d4` 返回202并进入Running3D。PLC最后一次收到PC心跳应答为北京时间06:07:24.940，下一次为06:07:28.456，相隔3516ms；06:07:27.952 PLC设置 `Alarm_Bits=64` 并取消Ready，Host随后记录 `SafetyInterlockLost`。Height worker在06:07:25.678受理、06:07:35.682返回成功，但运行已受限。无下料命令4；不同于先前页面配置版本拦截，也不能把历史PLC请求停止变化故障并为同因。

## Symptom and reproduction

1. 使用现场保存的 `process.json` 核对PID：VirtualPlc 2620、Host 9632、WPF 10180仍对应原实例，配置为007 Test样本公共/模拟1.2.0、预算1.1.0，心跳断连阈值3000ms、PLC I/O超时1000ms。没有启动、重启或复位它们。
2. 原现场PLC只读协议观察未保存在原目录；本轮在其仍运行时将 `/api/simulator/state`、`/changes?after=`分页（序号1–1593，导出时无缺口）、`/audit`导出至 `evidence/live-original-20260924/`，并复制当时日志、worker协议、进程/上料记录，以SQLite在线backup生成同目录一致性快照。副本不能冒称点击时即保存的原始协议快照。
3. 补存变化序号110：06:07:24.9401207 PC应答1；序号111/112/113：PLC请求在25.9128814、26.9146758、27.9206431继续翻转；序号114–117：27.9525987起PLC故障、Ready清零、Alarm_Bits=64、Severity=3；序号118：28.4563135 PC应答0。写审计确认两次PC应答写均被接收。时间均为同一VirtualPlc进程墙钟，间隔3516.2ms。
4. `logs/host.out.log` 记录22:07:28.4397809Z `SafetyInterlockLost`，前一可靠观测22:07:28.4343209Z、alarmBits=64；随后同一请求/运行的诊断为Running3D、`Unconfirmed:PlcSafetyInterlockLost`、`BlockedNoAutomaticRetry`。`worker-protocol.jsonl`的Height受理/结果时间与用户时间线一致。SQLite备份中run已创建，3D媒体/采集及Height意图已提交；Height结果在阻断后才返回，不能补造成功握手或完成。

## Suspected Code Paths

- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs:259-295` — 独立heartbeat连接读取请求位、遇变化写应答、判断3秒无变化；当前只有异常首因日志，成功但迟到的读写和调度空档不可见。代码在发现新边沿时先更新 `edgeAt`，然后才检查超时，存在掩盖此前长停顿的独立风险；不能据此反推本次延迟来源。
- `backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs:49-142` — 每连接独立 `SemaphoreSlim` 和1秒整次交换期限；当前仅失败、连接和启动写有详细日志，缺少成功但缓慢的心跳交易分段。business与heartbeat连接已经独立，不把“再次拆连接”列作修复。
- `VirtualPlc/ModbusTcpServer.cs:78-145` — 接收、处理、发送Modbus请求；仅处理耗时>=250ms时日志告警，缺少正常窗口的有界事务关联。
- `VirtualPlc/VirtualPlcEngine.cs:168-189,591-600` — 持续翻转请求位，只有收到与当前请求位相符的PC写入才刷新 `_lastEcho`，超3秒锁故障；现场报警时间与规则一致。
- 007采集/worker、SQLite、页面查询和控制台日志路径是资源竞争候选；现有记录不能确定其中任何一路阻塞了心跳。

## Root Cause Hypothesis

**具体延迟来源尚未知，置信度低；故障与安全处置本身已由协议事实高置信度确认。** 现有日志没有每轮“等待执行→读发送→PLC接收/返回→Host处理→应答写出”的时间戳。未见明确的Modbus超时或VirtualPlc慢请求告警，仅能缩小可能性，不能排除低于告警门槛的累积延迟、任务调度停顿、读写返回后的续体延迟或采样错过偶数次心跳翻转。必须先补有界诊断并在隔离Test中复现，不凭 worker受理时间断言算法阻塞。

## Proposed Remediation

**Preferred staged fix:** 先为本缺陷补最小无公开API变更的有界诊断：Host heartbeat按UTC和单调时钟记录每轮计划/执行间隙、读开始/结束、读位/前值、应答写开始/结束、连接代次及Modbus事务号；Modbus客户端分段记录连接/门锁等待、写请求、读首部/正文和最终耗时，成功但慢也进入有限内存环形窗口；VirtualPlc记录同事务的接收/响应与有效echo时间，在超时或超过阈值的异常窗口一次性持久输出环形摘要。只在慢阈值、状态变化和首次故障时输出，保留首末/计数，避免逐轮控制台日志反向制造阻塞。必要时附采样时的线程池/GC计数及相关日志吞吐，不建设性能平台。

用上述诊断先构造可使3秒应答空档出现的定向、隔离Test对照，确定耗时所在阶段；再只修证据指向的机制。若证据与上述假设不符，停止错误补丁，在新的追加评估记录中更正而不覆盖本报告。修复不能改变3000ms阈值、1000ms I/O期限、报警/互锁、动作未知不重发或必需3D/算法步骤。若确需改共享接口/行为，先同步003与007受影响的spec/contracts/plan/tasks及消费者，再改代码。

**Files likely to change:** `LatestProtocolPlcDevice.cs`、`ModbusTcpClient.cs`、`VirtualPlc/ModbusTcpServer.cs`、`VirtualPlc/VirtualPlcEngine.cs`及对应 `backend/tests/Gaode.Contracts.Tests/Devices/`、007隔离集成测试；最终代码集合须由诊断结果决定。只读导出及后续证据留本缺陷目录，不能覆盖旧样本。

**Required verification:** 修复前失败/修复后通过的机制定向对照；真实心跳中断超过3秒仍报警锁动作；实际WPF单次启动及3D、算法、下料、解锁、Test最终完成；预定少量连续复测逐次保存；用request/run、epoch、transaction、UTC/单调间隔和SQLite证据关联。页面不能由辅助API替代，模拟取盘不能写成真人。

## Risks & Considerations

- 虚拟PLC的3秒报警是有效保护，不可通过放宽超时或伪造echo来“修复”。业务轮询在28.434读到报警，不等于heartbeat循环在该时刻读取了请求位。
- 原始协议变化快照此前未保存；本轮取的是仍在运行实例的后补只读快照。后续采集若丢失早期环形数据，明确标缺失。
- 诊断日志必须限频且不能在心跳热路径同步写大量控制台文本；故障首因保留原始异常和实际处置。
- Test/VirtualPlc证据不外推真机，原下料partial、前端配置独立缺陷和T088不因本评估改变。

## Open Questions

- [NEEDS EVIDENCE: 3516ms主要发生在Host调度、Modbus读/写/响应、VirtualPlc处理或回调续体哪一段？须由新增关联时间线确定，当前不得指定根因。]
- [NEEDS EVIDENCE: 先更新边沿计时是否在隔离复现中漏报长空档；即使确认，也需与本次PLC应答延迟来源分别判定。]

## Stage gate

本报告确认症状与高严重度缺陷，但**未确认具体根因**。按用户连续授权可进入 `$speckit-bug-fix slug=station01-heartbeat-response-delay` 先实施诊断补丁并取得新证据；诊断补丁不算修复。若新证据支持某机制，才继续其定向修复和独立bug-test；否则如实partial并追加评估，不制造结论。
