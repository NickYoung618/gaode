# T003 worker进程所有权核对

日期：2026-09-23。`backend/src/Gaode.Infrastructure/Algorithms/WorkerProcessSupervisor.cs`的`StartAsync`直接启动受控子进程，并持有stdin/stdout、会话ID和退出任务；007 `plan.md`、`contracts/commissioning-cli.md`、`quickstart.md`现统一为Host通过该类型管理唯一独立worker。

预期进程树：PowerShell平台启动或复用VirtualPlc、Host、006桌面；Host启动一个worker子进程；平台读取Host/worker PID、版本、健康和日志，不调用第二个Python worker实例。单独`python scripts/virtual-station01-algorithm.py ...`只用于脱离平台的手动协议调试。

本记录只核对文档与已有进程管理器的所有权；007 T007/T008/T016仍须实现并留下实际进程树、PID/会话和停止证据。
