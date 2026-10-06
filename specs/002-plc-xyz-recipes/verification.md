# 验证记录

验证已完成，全部项目修改仅位于 `E:/dzk/gaode-1`。

自动化回归：

- `dotnet build backend/Gaode.slnx --no-restore`：0 警告，0 错误。
- 分程序集运行 `dotnet test`：规则 `28/28`、契约 `67/67`、集成 `41/41`，合计 `136/136` 通过；对应 TRX 分别为 `backend/tests/Gaode.Rules.Tests/TestResults/final-after-capacity-test.trx`、`backend/tests/Gaode.Contracts.Tests/TestResults/recipe-capacity-switch.trx`、`backend/tests/Gaode.Integration.Tests/TestResults/integration-final-isolated.trx`。
- 整个解决方案并行运行时出现过 1 个既有算法异常测试时序波动（`Blocked` 先于 `CompletedWithExceptions`），该测试按程序集单独复跑的 3 个参数组合全部通过，未改动该既有测试或业务代码。
- 契约测试覆盖 Review 目录 68 条配方、唯一 `plcRecipeId`、测试 F 码规范化及 `Provider=File` 切换。

独立进程联调证据（2026-09-21，运行号 `2d2e3f5e-875c-4ad3-93cc-615cf4544743`）：

- VirtualPlc：`127.0.0.1:1502`、Unit ID 1、控制页 `http://127.0.0.1:5080/`；Gaode Host：`http://127.0.0.1:5001/`。
- 心跳持续互通，运行期间 `communicationTimedOut=false`、无活动故障。
- 第一工位事件顺序为 `WaitingStartAcceptance -> WaitingPhysicalStart -> WaitingClamp -> Running3D -> RunningF -> SavingHandoff -> PublicHandoffCommitted`。
- 实体按钮由 VirtualPlc 测试入口产生上升沿并释放；夹紧和 `15/15` 公共区域握手成功。
- XYZ 到位回读一致：3D `Z=150`，F `Z=90`；最终 `XY_Move_Cmd=0`、`XY_Pos_Confirmed=0`，坐标保留在 PLC 实际位置寄存器。
- F 码原始值为 `TEST-TRAY-0001`，解析后的目录键为 `RC:R-S1-A-BASE:0.4.0-review`；第一工位完成时 `RecipeState=Unmatched`。
- F 后 `POST /api/v1/recipes/plan` 生成 `R-S1-A-BASE`、`plcRecipeId=1` 计划；`POST /api/v1/recipes/bind` 回读提交成功，VirtualPlc 最终 `Recipe_ID=1`、`NG_Zone_Count=15`、`Pending_Zone_Count=15`、`Zone_Config_Ack=1`。

Speckit 工作区入口 `scripts/verify.ps1` 本轮未能获取锁：`E:/dzk/gaode-1/.specify/workflows` 已有其他运行中的工作流持有 `runner.lock`，入口返回 `PermissionError`，未修改或终止这些外部工作流。上述构建、136 项测试和独立进程联调均已独立完成并保留 TRX/运行存储证据。

真实接入边界：真实 PLC 的现场地址/字序、实体按钮映射、相机/光源/算法 SDK、真实配方目录和坐标仍需现场资料；切换真实配方时只改 `Recipes:Provider=File` 与 `Recipes:CatalogPath`，目录必须符合 `tray-recipe-catalog/1` 并为每条配方提供唯一 `plcRecipeId`。切换真实 PLC 时改 `Gaode:Mode` 与 PLC 连接配置；真实设备未提供前 `Production` 会明确拒绝启动，不会把模拟组件伪装成真实设备。
