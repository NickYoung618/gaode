# WPR工具修复证据索引（2026-09-27）

原assessment及reconcile历史只读。本目录保存修复前入口/固定任务/请求/阻断文件、原冻结清单、保护文件摘要；正式结论见本目录verification-proof.json与父目录fix.md、test.md。

| 环节 | 证据 | 可证明范围 |
|---|---|---|
| 初始管理员现场 | ../task-c1517369fb324082ac47ae8e9e42689f/ | 管理员默认/旧命名WPR无录制；未重放Observe |
| 官方包与来源 | official-provenance.json、adksetup.exe、bundle/0、WPTx64-Onecore.msi、WPTx64-Desktop.msi | 微软官方URL；签名Valid；MSI SHA1与签名bundle清单一致 |
| 版本/OS/摘要 | versions-signatures.json、../wpt-toolchain-20260927.json | Server2019 17763；WPT 20348.3694及完整独立目录依赖；系统TraceRpt/TDH版本与摘要 |
| 解包 | ../task-786428b3b9e34ab7aaa59740c25d6dd4/、../task-f6b5bf6bacb5424a9e0d8947db988d51/ | msiexec /a项目内管理映像解包，退出0；非系统安装 |
| 原短验证 | ../task-25481d379a1c4c72a28459c8549df6b5/ | commands.json、start/stop日志、probe.json、248512512字节ETL；最初解析失败保留 |
| 真实执行命令 | execution-ledger.json | 绝对工具路径、实际参数向量与成功离线调用依据；保留最初失败调用，不作为自动重放脚本 |
| 修正后统计 | 同上offline-correction.json、trace-stats-corrected.txt | 同一ETL离线读取、UTC 02:09:39.7418081→02:09:52.4028967、lost events/buffers=0 |
| 全量xperf失败 | 同上parse-corrected.txt、parse-targeted.txt | .NET Event190 v9的0x80070032；过滤未解决，未据此再采样 |
| 实际TraceRpt解析 | ../task-e9d214ee9622494699c5a6cc93399446/ | 管理员离线处理同一ETL，退出0；events.xml、summary.txt、1023269事件 |
| 关联与兼容修正 | correlation-before-schema-recovery.json、correlation.json、KernelTraceEventParser-source.cs | TCP端点/TCB/PID、CSwitch/ReadyThread、Thread v3原始载荷恢复；记录15005和时间后缀限制 |
| 管理员清理 | ../task-006d4c5629cd47b091d754ba09ead1d0/ | 本次命名和默认WPR均未录制、无所属collector、探针PID4528不存在、端口无监听 |
| 正式接线 | wiring-check.json、formal-review.txt、formal-preflight-task.json | 当前实际入口与冻结输入、依赖摘要、固定任务管理员Preflight；不执行Observe |
| 最终核验 | verification-proof.json、protected-check.json、final-task.json | 保存/解析/关联/清理有效证据绑定；原assessment及任务文件未变；请求已消费、任务空闲 |

## 微软依据与适用限制

[Microsoft WPR Start and Stop Commands](https://devblogs.microsoft.com/performance-diagnostics/wpr-start-and-stop-commands/)对stop的0x80010106明确给出build19650及以上工具处置方向；这是工具修复依据，不是通信根因证明。
[WPT系统要求](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/)列出WPR/WPA Windows8及以后；[官方ADK下载页](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install)提供Server2022重发包。兼容性最终由本机实际保存与读取证明，不能仅按版本号判断。
[Microsoft PerfView ThreadTraceData](https://github.com/microsoft/perfview/blob/main/src/TraceEvent/Parsers/KernelTraceEventParser.cs)的FixupData给出v>=2原始载荷PID/TID在偏移0/4；本轮只恢复v3线程生命周期事件身份，不推断业务语义。

系统TraceRpt/TDH的Get-AuthenticodeSignature返回NotSigned，未宣称其嵌入签名有效；显式使用本机System32原文件并绑定版本和摘要。WPR控制与WPT依赖全部来自同一20348.3694独立目录，没有回退旧WPR。
