# T065最小机制修复报告

UTC 2026-09-27T05:07:42.335482+00:00


本结论仅为原任务允许的Windows Test/VirtualPlc主流程范围。Native开关显式绑定新正式构建/合法fixture，旧默认/旧冻结程序/全部失败/原assessment及工具fix/test不改。没有声明所有历史故障同因、所有VM调度已解决、r22 HTTP已修复或真实设备/现场标定完成。原3516ms分段日志缺失明确保留。质量清单15/16不动；非阻塞未来工作及生产未知保持原待办。


| 原条件 | 证据及结论 |
|---|---|
| 003 T065机制与最小修复 | 同NativeOverlapped批前后→64→Read直接关联，两个真实1秒超期主要在批后1112/1057ms；同DLL冷启动Native0/1切断该路径；正式最小Test接线，无放宽期限 |
| T065受控对照 | 冻结DLL/config/profile/初始化及inline0相同，只有运行时provider；业务/心跳txn3按连接分列；候选冷启动业务txn3 Read6.3766ms，心跳txn3 Read0.2003ms（后一瞬时指针0，不能伪造64关联）；全部1560响应头最慢20.2574ms |
| T065真实超期安全 | .NET10.0.12 Native=True下真实TCP响应丢弃I/O1000ms及PauseHeartbeat3000ms，均latch、epoch1→2、拒绝新Move；真实TCP1秒超期无自动重发3请求；TRX3/3，无Skipped |
| T065日志 | 连接/事务/PID/TID/QPC关键阶段及失败窗口保留；必要测试验证慢成功与真实失败窗口、deadline/端点/事务/GC字段；正式Q01持久RuntimeFlow/Modbus审计；历史缺日志不补造 |
| 008 T055 | 当前正式新DLL＋既有WPF/runtime，Q01 run6333b690-ca2f-4d10-83bf-bce13b8db8fd；实际页面选用/启动/取盘、冻结R008-Q01 1.1.1-test、公共3D/F、A/B每图/融合、XYZ/复位/PLC、SQLite/四媒体读回、完整尾段/Final、刷新重开；18/18，exit0/cleanuptrue |
| T055直接门禁与依赖复用 | 本轮改动仅运行配置/最低线程API及所属启动接线；旧F不匹配不动产品、产品到位/复位/保存失败不Final及USR-E必要XY/XYZ同值/变化Y，沿task-audit-night-20260927最终节、r19/r21必要合同/TRX、r22新恢复AB真实包及已有Q01-PARAM复用各未改业务分支，原来源/构建/配置差异保留；不声称全旧包来自新DLL |
| 008 T070 | 原最终20/22审计＋本轮T065/T055补齐，对账T049—T070原22项、直接依赖子交付、现行SC选定路线/C01—C08/F1—F6；原两轮converge追加0及静态核查复用，业务算法/配方/数据库/前端分支未改；仅追加本Q01一包，不重跑全矩阵/不修改退出Q历史 |

实际更改：ThreadPoolRuntimePolicy.cs按.NET配置优先级读取Native设置；Host Program、PLC Program、LatestProtocolPlcDevice.StartAsync不对Native调用不支持的最低线程预留；start-station01-virtual-loop.ps1新增仅Windows Test fixture所属进程WindowsNativeThreadPool（双端native1/inline0）；正式Q01工具显式传递并核对PID创建时间。先更新003/008直接spec/contracts/plan/tasks，不新增功能/重复任务；正式代码没有Harmony、socket私有字段反射或诊断事件。诊断复制源码/构建与正式源码/构建分别保留，源diff和前后SHA可审阅。

正式Host/PLC构建均0警告0错误，摘要/依赖见formal-inputs.json。独立诊断入口挂接验证先通过真实回环，真实观察不是完整配方。候选设置先被当作实验变量，取得机制/阶段对照后才成为显式正式Test运行选择；普通及生产默认不静默变化。不修改线程数、优先级/GC、期限、重试、PLC信号、动作业务成功条件。

官方线程池配置及准确10.0.12源码链接见assessment-supplement.md；Native不支持SetMin/Max，内部最低值0作为provider差异如实记录，不宣称预留8。WPT原组件/工具proof未改，固定任务只增加摘要绑定的正式Q01动作，非任意命令入口。新任务request730fc9ad1e3b42538523bbd2d1d8fd6f已经消费，不能重放。
