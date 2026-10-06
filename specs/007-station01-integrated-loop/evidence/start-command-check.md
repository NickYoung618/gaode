# T018 命令核对（2026-09-23）

- `dotnet build VirtualPlc/VirtualPlc.csproj`、`dotnet build backend/src/Gaode.Host/Gaode.Host.csproj`：现有项目入口；Host构建通过，VirtualPlc现有DLL用于本次平台启动。
- `pwsh -NoProfile -File scripts/start-station01-virtual-loop.ps1`：在`artifacts/station01-007/runtime-20260923-153539/process.json`记录VirtualPlc、Host、Host子worker、自动确认监视与当前net10 WPF/WebView2桌面PID、配置摘要；进程实际存活，006页面请求与状态交付仍未核验。
- `pwsh -NoProfile -File scripts/start-station01-virtual-loop.ps1 -SkipDesktop`：在`artifacts/station01-007/runtime-20260923-151249/process.json`记录后端调试组合；跳过桌面时只作后端样本。
- `pwsh -NoProfile -File scripts/simulate-station01-load.ps1 -PrepareOnly`：在`artifacts/station01-007/runtime-20260923-151249/`生成`channel=PrepareOnlyFor006`清单，无业务启动。
- `pwsh -NoProfile -File scripts/simulate-station01-load.ps1 -StartRun`：同目录生成`channel=AuxiliaryApi`的202回执、runId及最终SQLite/API证据；它不是006前端启动样本。
- `scripts/watch-station01-auto-removal.ps1`由平台启动，按后端`/status`、run和`/evidence`观察已提交整盘与ObservedUnlocked后才调用确认API；同目录`auto-<runId>.json`及`manual-final.json`记录其Test渠道。
- Python worker只由Host的WorkerProcessSupervisor管理。`runtime-20260923-152518/media-root/worker-protocol.jsonl`含Hello/Ready握手；单独Python命令仅适用于手工协议调试。

旧两进程`artifacts/windows-framework-publish-20260922/start-local.ps1`不代表007全组合。所有进程停止只针对对应`process.json`登记的PID，不能自动重置活动运行。
