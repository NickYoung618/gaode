# T065补充证据索引（2026-09-27）

本索引不改历史结论。目录内JSON提取是便于审阅的派生证据，原日志/原readback为最终核对依据。PowerShell读取JSON可能把UTC表示成等价本地偏移，不能据表示差异推定时间变化。

| 证据 | 来源/用途及限制 |
|---|---|
| [原评估](assessment.md) | 保留原文；本轮结论见supplemental-assessment-20260927.md |
| `artifacts/communication-delay/t065-communication-delay/20260927T005851382Z/diagnostic-report.md`及preflight.json、privileges.txt、network-profile-details.txt、wpr-start-result.json、全部WPR/ETW前后文本、protection-validation.json | 旧采样前置失败，设备启动0；不是120秒未复现。旧manifest的16个列出文件已核摘要 |
| [旧manifest核验](evidence-20260927/old-manifest-validation.json) | 16项均匹配；仅该清单范围，不外推全项目 |
| [本轮令牌](evidence-20260927/token.txt)、[本轮探测](evidence-20260927/probe.json)、[原始启动错误](evidence-20260927/wpr-start.txt) | 正确System32 whoami结果，Session0，01:12:07Z，0xc5585011；无法启用系统采样 |
| `evidence-20260927/{etw-before,etw-after,wpr-before,wpr-after}.txt` | WPR未录制、原MSDTC保留，没有成功会话，未调用stop/cancel |
| [r15事务提取](evidence-20260927/r15-0927-transaction.json) | 原`artifacts/recipe-execution-008/r15-0927/runs/job-000-Q06/Q06/communication-readback.json`、logs、process.json、scheduling.json；仅58574↔25164事务78；窗口重复记录不是新故障 |
| [r17事务提取](evidence-20260927/r17-0927-transaction.json) | 原`artifacts/recipe-execution-008/r17-0927/runs/job-000-GROUP-A-E/GROUP-A-E/communication-readback.json`、logs、process.json、scheduling.json；仅60123↔25154事务9478 |
| `artifacts/recipe-execution-008/r16-io-0927/runs/job-000-Q06/Q06/`、`r18-io-0927/runs/job-001-GROUP-A-E/GROUP-A-E/` | 沿原评估/最终审计引用的显式Test inline比较；不是本轮默认模式通过，不证明唯一机制 |
| [r22 HTTP摘录](evidence-20260927/r22-http-extract.txt) | 原r22-page-0927/runs/job-001-RECOVERY-F/RECOVERY-F/logs/host.out.log，行号352、396、418—464；同目录process.json及plc.out.log。内部GET没有服务端请求关联分段；traceId是外部POST，不能直接当内部GET连接ID |
| `artifacts/recipe-execution-008/r22-page-0927/runs/job-002-RECOVERY-F/RECOVERY-F/` | 最终审计引用的成功恢复主包，仅按原构建/操作范围复用；未重验，不改job001失败 |
| `specs/008-recipe-driven-inspection/evidence/{completion-review,task-audit-night-20260927}.md`、coverage-matrix.md | 最终节：Test选定路线完成、20/22、T055/T070保留；真实设备限制独立 |
| `specs/003-plc-latest-protocol/{spec,plan,tasks}.md`、contracts.md、contracts/status-notifications.md、contracts/recovery-test-execution.md | FR03/FR15、原T065和恢复边界；仍独立连接/原期限/未知不重发 |
| `specs/008-recipe-driven-inspection/{spec,plan,tasks}.md`、contracts/{execution,evidence,test-virtual-mapping}.md | T055/T070依赖、证据适用和Test输入边界；末尾现状优先于历史描述 |
| `backend/src/Gaode.Infrastructure/Devices/Plc/{ModbusTcpClient,LatestProtocolPlcDevice,HeartbeatDiagnosticWindow}.cs`、backend/src/Gaode.Host/Program.cs | 应用读写完成、取消、心跳独立连接和最低线程修正；无系统TCP送达事实 |
| `VirtualPlc/{ModbusTcpServer,VirtualPlcEngine,TestSpecialActions,Program}.cs` | 请求处理/写响应、有条件有效echo、安全超期、HTTP状态锁；源码锁存在不等当次锁竞争 |
| [采样脚本](observe-default-20260927.ps1)、[启动器](sampling-launcher-20260927.ps1)、[冻结输入清单](sampling-inputs-20260927.json) | 仅准备；AST及ReviewOnly通过，管理员实跑未验。包括冻结程序集及依赖、Test fixture/配置、原启动脚本/StorePrep，不build、不调用配方启动 |
| [静态验证](evidence-20260927/script-validation.json)、[保护输入](evidence-20260927/protected-inputs.json)、[保护复核](evidence-20260927/protection-final.json) | AST/摘要检查是静态验证；保护检查只声明列出文件，不冒充仓库diff（此目录无.git） |

本轮新增文件摘要另存`evidence-manifest-20260927.json`，不包含自身，避免自引用。后续采样结果的UTC目录由脚本打印并写入result.json；目前尚无成功sampling-*结果，不提供虚构ETL/PID/连接。
