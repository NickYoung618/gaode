# Bug Verification: 日志文件占用时保留原异常并暂停

- **Slug**: 008-worker-launch-error（从当前fix解析）
- **Tested**: 2026-09-27
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Checks Performed

| 检查 | 实际证据 | 结果 |
|---|---|---|
| 原文件锁错误等效复现及修复 | desktop-worker-control/launch-error-probe.json；执行实际worker catch/finally AST，原catch独占锁失败，新catch保持存活并保存原异常 | 7/7 pass |
| 语法 | script-parse.json：worker、恢复、授权三个PS脚本 | pass |
| 初次授权准备失败修复 | grant-missing-task-probe.json；实际Scheduler查询不存在任务＋实际AST解包HResult | pass；原用户错误保留 |
| 权限配置 | task-grant.json、codex-task-query.json；固定InteractiveToken任务，triggerCount0，Codex查询/执行0x1200a9 | pass |
| 从Session0自动恢复 | codex-task-run.json、attempt-20260926T164350661-11812/resource-reconciliation.json、result.json；核旧进程/端口/日志锁后实际worker10536/Administrator/Session2就绪 | pass |
| 再次触发复用 | codex-task-reuse-run.json及独立attempt/result.json，真实创建时间与ready/命令行匹配，仍PID10536 | pass；没有新worker或停止原worker |
| 实际后续runner | resume-1/job009.started与真实WPF页面，runner7708正常启动，页面Final、清理true；job010随后runner4372启动 | 启动路径pass；job009整体仍因另一数组导出缺陷Failed，不计完整业务pass |

## Residual Risks / Recommendation

原job008初始Start-Process异常被旧catch丢失，无法还原，未声称其原因已确定。验证关闭的是已确定的文件锁导致worker二次退出和错误清理成功声明；原result不修改。权限/自动恢复已实际可用，当前桌面须保持登录。后续reloadWorkerRoot已实际验证10536→9936→5040两次：各root queue/job-008.worker-reload.json及result记录后继同账号Session2 ready后原worker退出；resume-2接班前固定任务实际复用9936，未重复启动。当前有效worker5040继续r6；登录自动启动尚未配置，不中断当前验收。
