# Bug Assessment: 003 T065通信延迟及008收口前置

- **Slug**: t065-communication-delay（用户于2026-09-27确认）
- **Created**: 2026-09-27
- **Source**: pasted text；用户本轮报告、现行任务和既有运行证据；无外部URL
- **Verdict**: valid
- **Severity**: high — 通信超期实际阻断Test运行，安全门禁生效，但延迟机制及修复前后对照尚不足，阻塞008 T055/T070收口。
- **Stage**: 初始评估；本轮另获一次120秒独立通信定位实验授权，实验结果另存，不覆写本评估。

## Report (summarized)

用户要求聚焦003 T065，不重复已验收配方、不改业务代码、配置、任务勾选或桌面worker。核实008当前22项任务20项已勾，T055/T070未勾；选定Test主流程已验收不变。T055明确依赖003 T065，T070依赖T055。Test I/O缓解不得写成根因已解决；原3516ms缺失分段日志不得补造。

## Symptom

同机Host与VirtualPlc出现超过原1秒I/O期限的读写完成空档；原现场曾因有效心跳应答空档3516.2ms触发3秒安全互锁。后续已有有界诊断定位到不同传输观察区段，但仍不能确定内核传输与进程调度各占多少。

## Reproduction

历史事实而非本轮运行：

1. 2026-09-24原3D现场仅保存后补PLC变化/审计、Host日志等；有效echo间隔3516.2ms且PLC请求继续翻转、报警锁停。原窗口Host分段缺失。
2. r15普通Debug/default GC/default socket调度，Q06启动门禁期间无业务run即失败。读取同端点58574的心跳事务78，PLC响应写入完成20:00:14.9550229Z，Host首部读取完成20:00:16.4335785Z。
3. r17仅Host显式Test inline，GROUP-A-E运行2431de00-3dda-4a03-adcb-40f9dce48944期间失败。同端点60123、心跳事务9478，Host写入完成20:15:49.799033Z，PLC首部读取完成20:15:54.5024904Z。

[NEEDS EVIDENCE: 间歇故障的确定触发条件；不能承诺下一次启动必复现。]

## Acceptance Coverage

现行依据：`specs/003-plc-latest-protocol/tasks.md` T065（第267行），008 T055（第93行）及T070（第172行）；最新`evidence/completion-review.md`、`task-audit-night-20260927.md`、`continuation-20260927.md`。

| T065条件 | 已有证据 | 缺口/结论 |
|---|---|---|
| 独立连接、原1秒I/O/3秒互锁 | 源码、进程配置、实际失败及旧安全对照 | 有证据；不改期限 |
| 超期锁动作、未知不盲重发 | r15/r17失败和旧PauseHeartbeat安全对照 | 有证据，按各样本范围复用 |
| Host调度/事务与PLC接收/处理/响应/有效echo有界UTC及单调观察 | r15七窗口、r17十窗口，parseErrors为空，droppedWindows均0 | 应用分段具备；环形历史覆盖不等于全量永久记录，更不等于内核到达时间 |
| 原现场缺失分段明示 | 旧缺陷assessment/fix/test及当前收口报告 | 已明确；不要求补造或永久等待不可恢复历史 |
| 实际延迟机制、最小调度修正及前后定向对照 | 旧最低线程修正与有限成功、r16/r18显式Test I/O比较 | 不充分，T065不可勾；正常Passed不抵扣 |

## Suspected Code Paths

- `backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs:68-225`：每连接门锁、连接、写入、首部/正文读取及1秒取消期限；完成时间为应用观察。
- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs:462-565`：心跳循环调度、边沿读取与echo写入、异常锁停。
- `VirtualPlc/ModbusTcpServer.cs:102-186`：读取完成、处理及响应写入完成，包含连接/端点/事务关联。
- `VirtualPlc/VirtualPlcEngine.cs:208-235,739-752`：请求翻转、有效echo匹配接受、超3秒报警；Modbus写确认不等于有效echo接受。
- `backend/src/Gaode.Host/Program.cs:14`：旧最低线程数修正。后续故障仍发生，不能当作全部延迟已消除。
- `backend/src/Gaode.Infrastructure/Devices/Plc/HeartbeatDiagnosticWindow.cs`：256条环形记录、有界异步输出、丢弃/抑制计数及运行指标。
- `scripts/start-station01-virtual-loop.ps1:183-208`：Test-only所属子进程inline开关；默认关闭，不改父进程和系统环境。
- `LatestProtocolPlcDevice.cs:113-150`：r22初始核验的特殊机构HTTP查询，单独分析。

## Root Cause Hypothesis

**故障与观察区段置信度高，具体机制置信度低；socket完成/调度延迟是中等可信候选。** r15事务78门锁0.0029ms、写入0.0537ms、已连接无需重连；PLC处理及写响应0.0244ms，PLC写响应完成至Host首部读取完成约1478.56ms，正文因期限取消失败。r17事务9478门锁0.0022ms、写入0.0514ms，Host约1000.51ms超期，PLC约4703.46ms后读完首部，随后处理及响应0.0133ms。因此这两个样本的长空档不在已测量的门锁、连接建立或PLC请求处理段，但没有系统TCP/调度时间线，不能分清传输送达、接收调度和完成回调。单次队列为0或GC累计较短不证明全部调度/VM影响被排除。

旧2026-09-24最低线程修正和三次页面通过保留其当时结论；后续r15/r17失败说明不足以关闭本次延迟机制。r16仅Host inline下Q06通过；r17同Host构建仅Host inline仍失败；r18双端inline下GROUP通过。证明限定Test缓解和路线可执行，不证明默认模式、生产或唯一根因解决。

r22 job001恢复初始核验HTTP超过1秒，堆栈明确到`ReadInitialStateAsync`；同构建job002成功只作有限复验，不自动合并为心跳同因或HTTP修复。旧3516ms不能由这些新样本倒填。

## Proposed Remediation

**Preferred**：先执行下述唯一诊断实验，获得系统TCP与线程调度关联后才提出最小修复。本轮不执行bug-fix。若证据支持接收端调度/回调，最小处理对应进程的实际调度路径；若支持传输或PLC处理，则停止错误调度补丁建议，按新证据评估。不得提高期限、伪造echo、盲重发或新增PLC信号。

**Files potentially affected only after evidence**：上述Modbus客户端、心跳循环、PLC服务器/引擎或进程启动路径，最终集合必须由新证据决定；当前不授权业务改动。

**Verification after a future approved fix**：相同冻结构建基线/配置、相同最小负载的机制前后对照，真实超期仍锁动作；必要代表性主流程按实际影响复用或复验，不扩大配方矩阵。未确定机制前不以正常运行关闭T065。

## Next Diagnostic Experiment

一次120秒默认调度通信观察：独立Host/VirtualPlc端点、日志及数据目录，无WPF、无业务启动/运动、无完整配方。冻结当前已验收程序与原Test配置，仅所属两个子进程关闭inline，保留1秒I/O、3秒心跳、原轮询/GC/优先级等其他参数。只读检查端口、已有ETW会话、wpr及profile/权限；不能覆盖或停止他人会话。

系统采样必须同时提供TCP事件与CSwitch/ReadyThread等调度观察，结合双端连接/事务、UTC与单调分段、有效echo接受、超期时刻。跨进程优先UTC加端点/事务，系统QPC需结合ETL时间基准；不能直接相减不同进程未核验的原始tick，不能把Read/Write完成当内核到达。

首次通信超期后保留短后续窗口即结束，否则120秒结束；不循环重试。权限不足或采样不能提供上述信息，启动前停止依赖该条件的实验并记录Blocked，不用应用日志冒充系统时间线。只结束本实验确实创建的会话/进程，保存已有证据。

判定：系统接收事件早而读取完成晚，支持接收调度/回调；系统送达本身晚，定位传输区段；处理/有效echo接受晚则定位相应业务观察段。无法关联则不确定。未复现须写“本次120秒未复现，机制仍不确定”。实验是定位前置，不等于修复验收。

## Risks & Considerations

- 采样有开销，需有界且记录事件丢失/时间关联限制；不用全面性能平台。
- 原process.json部分写poll25ms，实际Test注册及日志为50ms；不把元数据修正误当轮询变更。
- Test I/O只在所属子进程启用，不能泛化生产或默认模式结论。
- 旧缺陷 `.specify/bugs/station01-heartbeat-response-delay/{assessment,fix,test,evidence-index}.md` 保留历史日期及适用范围，当前评估不覆盖。
- 008仍20/22；003 T065、008 T055/T070不勾选。质量清单15/16、生产局部限制保持原事实。

## Evidence Index

- `artifacts/recipe-execution-008/r15-0927/runs/job-000-Q06/Q06/communication-readback.json`、`process.json`、`scheduling.json`及logs。
- `artifacts/recipe-execution-008/r17-0927/runs/job-000-GROUP-A-E/GROUP-A-E/communication-readback.json`、`process.json`、`scheduling.json`及logs。须按端点匹配事务；不同连接可能有相同事务号。
- `artifacts/recipe-execution-008/r16-io-0927/runs/job-000-Q06/Q06/`。
- `artifacts/recipe-execution-008/r18-io-0927/runs/job-001-GROUP-A-E/GROUP-A-E/`。
- `artifacts/recipe-execution-008/r22-page-0927/runs/job-001-RECOVERY-F/RECOVERY-F/` 与独立job002成功包。

## Open Questions

- [NEEDS EVIDENCE: 当前异常空档的内核TCP送达、线程可运行/调度及socket完成续体各自耗时。]
- [NEEDS EVIDENCE: 默认调度短启动是否复现；未复现不允许扩大为无限配方压力测试。]
- [NEEDS EVIDENCE: r22 HTTP超期自身的发送/服务处理/返回/读取时间线，不能由心跳样本代证。]

## Stage Gate

评估已落盘；诊断结果另写独立报告。下一阶段是否进入 `$speckit-bug-fix slug=t065-communication-delay` 由用户依据实验结论决定，本轮不自动执行。
