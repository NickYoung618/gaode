# 核心验证与回退

2026-10-07。仅本机真实Modbus TCP回环，无实际设备连接或运动，无整轮配方验收。

正式源码：dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj，3/3通过。用既有虚拟PLC引擎、真实TCP和SQLite证据记录；核对轴启动审计：X移动/Y与检测Z沿用、全沿用、Y与检测Z移动/X沿用。未改虚拟PLC引擎。

工具：test_same_position 4/4通过（手动同位置无写入、缓存失效不能沿用、配方混合轴、全沿用）；原有2项通过（旧到位且实际不符不得完成；X/R运动中→到位并清零）。启动器版本选择1/1通过。app.js语法检查通过。

正式修改集中在LatestProtocolPlcDevice.Axes.cs，复用既有有限读取计划，不新增调度器、PLC信号或公共接口。联调工具保留1.1.4配方的原有状态＋位置容差处理，增加启动前位置复用及结束复核；手动未配置容差时仅位置完全相等可沿用。

联调回退：结束配方、处理在途动作、页面断开PLC后运行工作区或包内Rollback-1.1.4.ps1。入口拒绝连接中、配方或轴动作在途时切换；停新版本地服务，禁用1.1.5发布标记，启动保留的1.1.4，不自动连接PLC。旧版使用自身原配置，人工核对。重新使用1.1.5可重新解压完整ZIP恢复其发布标记后启动。固定路径脚本用于本电脑。

源码回退：Git标签rollback-before-same-position-20261007对应修改前b0e0098。建议git worktree add D:\gaode-rollback-same-position rollback-before-same-position-20261007生成独立旧源码目录，避免覆盖当前工作。原工具源码/配置备份：D:\Gaode-PlcCommissioning-20261006\backups\before-same-position-20261007。

现场仍需用已确认的轴容差与安全条件核对一次X变化/Y不变及检测Z不变；本次回环结果不代替实机验收。

交付检查：正式Host编译通过（0警告/0错误）；1.1.5手动/配方页面HTTP 200；两个工作区入口选中1.1.5；实际执行回退入口，确认1.1.4后恢复1.1.5；全过程TCP未连接PLC、心跳关闭。
