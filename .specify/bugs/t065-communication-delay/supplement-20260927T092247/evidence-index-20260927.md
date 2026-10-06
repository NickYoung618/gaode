# 证据索引：t065-communication-delay补充评估（2026-09-27）

本目录assessment-supplement-20260927.md为本轮结论；原../assessment.md保持。全部链接/路径相对项目根E:/dzk/gaode-1；SHA摘要与保护核验分别沿protected-inputs、protection-and-tool-check、原manifest及本轮manifest。

| 证据 | 用途/限度 |
|---|---|
| 本目录identity.txt、wpr-before.txt、wpr-start.txt、wpr-result.json、wpr-after.txt、etw-before/after.txt | 当前普通身份、唯一失败采样尝试0xc5585011，未启动观察，无ETL/本轮HostPLC |
| 本目录transaction-readback.json | 从r15/r17已保存窗口按双端端点+事务提取，不改原窗口；应用观察不是系统送达 |
| 本目录prior-evidence-integrity.json | 上次报告目录manifest16项SHA全部一致，仅核验其列出范围 |
| 本目录live-state.json | PID9428存活Session2；旧current-night-batch指针不是最新root；不操作worker |
| 本目录script-review-only.json、protection-and-tool-check.json | AST/冻结输入全部通过；原assessment、003/008tasks及宪章SHA未变；真实采样未执行 |
| ../observe-default-20260927.ps1、../observation-freeze-20260927.json | 待用户提升会话执行的一次最小工具；有界120秒、不启动配方、唯一WPR instance、所属PIDCreationDate清理；非业务补丁 |
| artifacts/communication-delay/t065-communication-delay/20260927T005851382Z/diagnostic-report.md及全部manifest条目 | 上一次BlockedBeforeObservation，不是120秒未复现 |
| artifacts/recipe-execution-008/r15-0927/runs/job-000-Q06/Q06/communication-readback.json、process.json、scheduling.json、logs | 默认调度响应后等待样本58574/25164事务78；原失败保留 |
| artifacts/recipe-execution-008/r17-0927/runs/job-000-GROUP-A-E/GROUP-A-E/communication-readback.json、process.json、scheduling.json、logs | Host inline仍失败样本60123/25154事务9478；不能以Host inline宣称全解 |
| artifacts/recipe-execution-008/r16-io-0927及r18-io-0927正式通过包 | 限定Test缓解/主流程事实，非根因或生产验收 |
| artifacts/recipe-execution-008/r22-page-0927/runs/job-001-RECOVERY-F/RECOVERY-F/logs/host.out.log | HTTP恢复失败trace0HNOS2KK17424:0000003D、ReadInitialStateAsync:140、外层1429.3363ms；status200/responseStartedFalse不算成功 |
| artifacts/recipe-execution-008/r22-page-0927/current-primary-summary.json、runs/job-002-RECOVERY-F/RECOVERY-F | 独立成功新旧run/旧媒体23项当前Test范围，不是HTTP补丁 |
| specs/008-recipe-driven-inspection/evidence/completion-review.md、task-audit-night-20260927.md、continuation-20260927.md | 最新最终交付20/22与全部历史范围分开；T055/T070及T065未勾 |
| specs/003-plc-latest-protocol/spec.md FR03/15、plan.md心跳诊断增量、tasks.md T065、contracts/virtual-plc-boundary.md及recovery-test-execution.md | 原3秒保护、正式Modbus边界、有界诊断与真实初始状态 |
| specs/008-recipe-driven-inspection/spec.md FR013/014、plan.md、tasks.md T055/T070及contracts/test-virtual-mapping.md | 原期限/日志/合法Test及父任务依赖，不生成新规格/任务 |

候选源码：ModbusTcpClient.cs、LatestProtocolPlcDevice.cs、HeartbeatDiagnosticWindow.cs、Host/Program.cs，VirtualPlc/ModbusTcpServer.cs、VirtualPlcEngine.cs、Program.cs、TestSpecialActions.cs及scripts/start-station01-virtual-loop.ps1。仅阅读；未因候选而改代码。

工具依据：[Microsoft WPR命令行参考](https://learn.microsoft.com/en-us/windows-hardware/test/wpt/wpr-command-line-options)。本机WPR -help已确认-instancename须最后参数。文档/profile只说明可配置采样，不证明当前权限或实际ETL覆盖。唯一用户动作与准确结果路径详见本轮评估，未要求重启桌面或授权任意管理员任务。
