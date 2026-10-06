# Bug Assessment: 启动错误记录失败导致交互 worker 退出

- **Slug**: 008-worker-launch-error
- **Created**: 2026-09-27
- **Verdict**: valid
- **Severity**: high（本次无人值守执行阻塞）

## Report / Reproduction

r5 job-008 ASSEMBLY-A-E-ERROR 有 started 及合法预算，但无 runnerPid、无运行目录。result exit1、cleanupVerified=true；err.log 长度0。用户窗口显示 wait-008-page-batch.ps1:154 的 Set-Content 因 job-008.err.log 被占用而失败，随后返回 PowerShell 提示符。当前 PID9476 已退出，未发送 finish，也未留下 worker-finished。原启动异常因二次写日志失败而丢失，不能编造其原因。

## Root Cause / Proposed Remediation

Start-Process 使用 err.log 作为标准错误重定向，而 catch 再覆写同一文件。启动部分失败时文件仍可能被占用，catch 的异常使 worker 退出。改为独立 job.worker-error.json 保存原异常。若曾尝试启动却未获取 runner 对象，资源身份未核实，必须暂停并标记 cleanup 未核实，不能按“没有资源启动”成功放行。既有正常 job 和 reloadWorkerRoot 语义不变。

当前旧 worker 已退出，reloadWorkerRoot 无执行主体。Session0 账号先前 InteractiveToken 创建被拒绝0x80070005，不能自行在 Administrator 桌面恢复。准备一次桌面恢复入口：核查旧 job 的进程/端口/文件占用，在新独立暂停 root 启动 worker，保留旧队列与失败。随后自动执行剩余批次并实际验证接班；不提前配置登录启动。

## Files / Tests / Risks

scripts/wait-008-page-batch.ps1；一次性恢复入口；原008证据目录及本缺陷记录。受控独占锁复现旧 catch 失败，执行实际修正版 AST catch/finally 证明异常可保存、资源未知暂停；检查完整脚本语法。恢复阶段须检查 Administrator 当前Session及真实进程；不停止无法确认归属的进程。业务二进制、fixture、期限保持冻结，原 job008 不能改写为成功。
