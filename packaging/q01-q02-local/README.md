# 第一工位 AB/CD 本地虚拟联调包

此打包源说明对应旧协议工具，现有发布包内容保持只读。2026-09-26文档对齐不代表该包已支持新分区协议；新版需按[008实施清单](../../specs/008-recipe-driven-inspection/implementation-checklist-20260926.md)完成代码、配置、生成器及采证适配后另产新包。下文仅描述旧版使用：此包用于在 Windows 桌面测试已验证的两条单面正常路线：Q01 = AB/P01；Q02 = CD/P01、P03。启动脚本同时启动 Host、VirtualPlc、Host 管理的独立 Python 算法进程、WPF/WebView2 前端，并打开 VirtualPlc 监控页。相机使用随包固定图片，运行数据写入真实 SQLite 和媒体目录。每次只运行一个测试会话。

## 环境

- Windows 10/11 x64，已登录的桌面会话。
- .NET 10 桌面运行时和 ASP.NET Core 运行时、PowerShell 7 (`pwsh`)、Python 3、Microsoft Edge WebView2 Runtime。
- 默认测试端口 5001、5080、1502 空闲。启动后只监听本机回环地址。

## 从 VS Code 终端运行

打开**解压后的本目录**作为 VS Code 工作区，在 PowerShell 终端执行：

```powershell
pwsh -NoProfile -File .\Start.ps1 -Case Q01
```

在前端窗口点击“进入系统”，选择 `R008-Q01 / 1.1.1-test`，点击“启动”；解锁后在页面点击“取盘确认”。虚拟PLC监控页面会自动打开，也可手动访问 `http://127.0.0.1:5080/`。等待页面显示完成，再检查运行记录。结束本次会话：

```powershell
pwsh -NoProfile -File .\Stop.ps1
```

随后测试 CD：

```powershell
pwsh -NoProfile -File .\Start.ps1 -Case Q02
```

在前端选 `R008-Q02 / 1.1.1-test`，按相同页面步骤操作；结束时再次运行 `Stop.ps1`。运行数据库、媒体、日志和 `process.json` 保存在 `artifacts/recipe-execution-008/local-*`。查看 `process.json` 中的 `testRoot`、`caseId`、进程 PID；Host 和PLC日志位于该目录 `logs/`。

这是 Test 虚拟环境，成功只说明对应正常软件流程跑通；不表示真实相机、PLC机械动作或识别精度已验收。Q01/Q02 历史通过证据见仓库 `specs/008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md`。
