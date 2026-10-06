# T065最小修复验证及T055/T070对账

UTC 2026-09-27T05:07:42.335482+00:00


| 原条件 | 证据及结论 |
|---|---|
| 003 T065机制与最小修复 | 同NativeOverlapped批前后→64→Read直接关联，两个真实1秒超期主要在批后1112/1057ms；同DLL冷启动Native0/1切断该路径；正式最小Test接线，无放宽期限 |
| T065受控对照 | 冻结DLL/config/profile/初始化及inline0相同，只有运行时provider；业务/心跳txn3按连接分列；候选冷启动业务txn3 Read6.3766ms，心跳txn3 Read0.2003ms（后一瞬时指针0，不能伪造64关联）；全部1560响应头最慢20.2574ms |
| T065真实超期安全 | .NET10.0.12 Native=True下真实TCP响应丢弃I/O1000ms及PauseHeartbeat3000ms，均latch、epoch1→2、拒绝新Move；真实TCP1秒超期无自动重发3请求；TRX3/3，无Skipped |
| T065日志 | 连接/事务/PID/TID/QPC关键阶段及失败窗口保留；必要测试验证慢成功与真实失败窗口、deadline/端点/事务/GC字段；正式Q01持久RuntimeFlow/Modbus审计；历史缺日志不补造 |
| 008 T055 | 当前正式新DLL＋既有WPF/runtime，Q01 run6333b690-ca2f-4d10-83bf-bce13b8db8fd；实际页面选用/启动/取盘、冻结R008-Q01 1.1.1-test、公共3D/F、A/B每图/融合、XYZ/复位/PLC、SQLite/四媒体读回、完整尾段/Final、刷新重开；18/18，exit0/cleanuptrue |
| T055直接门禁与依赖复用 | 本轮改动仅运行配置/最低线程API及所属启动接线；旧F不匹配不动产品、产品到位/复位/保存失败不Final及USR-E必要XY/XYZ同值/变化Y，沿task-audit-night-20260927最终节、r19/r21必要合同/TRX、r22新恢复AB真实包及已有Q01-PARAM复用各未改业务分支，原来源/构建/配置差异保留；不声称全旧包来自新DLL |
| 008 T070 | 原最终20/22审计＋本轮T065/T055补齐，对账T049—T070原22项、直接依赖子交付、现行SC选定路线/C01—C08/F1—F6；原两轮converge追加0及静态核查复用，业务算法/配方/数据库/前端分支未改；仅追加本Q01一包，不重跑全矩阵/不修改退出Q历史 |

本结论仅为原任务允许的Windows Test/VirtualPlc主流程范围。Native开关显式绑定新正式构建/合法fixture，旧默认/旧冻结程序/全部失败/原assessment及工具fix/test不改。没有声明所有历史故障同因、所有VM调度已解决、r22 HTTP已修复或真实设备/现场标定完成。原3516ms分段日志缺失明确保留。质量清单15/16不动；非阻塞未来工作及生产未知保持原待办。

真实命令：两次独立请求ObserveMechanismBaseline/ObserveMechanismCandidate；使用冻结control-build同一DLL和profile，固定入口observe-baseline.ps1/observe-candidate.ps1（分别env native0/1）；随后正式源码dotnet build Host/VirtualPlc --artifacts-path artifacts/recipe-execution-008/t065-mechanism-formal-20260927/build。

安全命令：所属测试pwsh中DOTNET_ThreadPool_UseWindowsThreadPool=1、DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=0；dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --artifacts-path artifacts/recipe-execution-008/t065-mechanism-formal-20260927/test-build --filter 'FullyQualifiedName~T065OriginalDeadlineTests|FullyQualifiedName~SlowSuccessRetainsPriorExchangeAndFailureBypassesWindowRateLimit' --logger 'trx;LogFileName=t065-native-safety.trx' --results-directory .specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/safety-results。只三个原门槛必要用例；首次编译失败保留，修正测试签名/类型歧义后3Passed/0Failed/0Skipped。

正式命令由固定管理员任务执行verify-formal-q01.ps1（入口/输入SHA固定）：scripts/verify-q01-q02-test-page.ps1 -Interactive -EvidenceRoot E:/dzk/gaode-1/artifacts/recipe-execution-008/t065-formal-q01-20260927T050046932Z -Cases Q01 -HostDll E:/dzk/gaode-1/artifacts/recipe-execution-008/t065-mechanism-formal-20260927/build/bin/Gaode.Host/debug/Gaode.Host.dll -PlcDll E:/dzk/gaode-1/artifacts/recipe-execution-008/t065-mechanism-formal-20260927/build/bin/VirtualPlc/debug/VirtualPlc.dll -WindowsNativeThreadPool。实际Host11564/PLC4908/算法2132/WPF7272，Administrator Session2；期末均不存在，原worker9428仍保留；cleanup-result verifiedtrue。新证据不能覆盖原失败。

ETL保存/解析通过，原ETL保留在C工作目录（workspace-path.txt）；分别机制第一窗563085312B、受控基线429916160B、候选810549248B，WPR stop0，解析errors0/lost0，xperf lost events/buffers0，Thread调度连续性已核验；原QPC及应用UTC/QPC锚点保留。流式gzip，不生成无界未压缩JSON。基线启动器未Ready不是无通信，日志真实1秒超期；候选轮询超过内部12秒目标，实际18.3575秒低于本实验20秒上限。原CleanupIncomplete结果原样保留，所属PID延后消失与管理员无WPR另证，不倒改旧结果。事件覆盖对真实非零指针给出数量，零/歧义不硬匹配；入队后不细分CompleteBatch/worker全部内部机制；原TCP Request不冒充用户NativeOverlapped，应用Write不等于内核送达。

结论：原T065 Test/VirtualPlc条件满足；当前新正式Q01/T055通过；T070当前适用Test收口满足。仅获授权三项更新，其他任务不动。真实设备与现场标定不是本轮通过范围。r22初始HTTP仍独立保留，无同因或已修复宣称。

补充计时限制：候选xperf记录ETL覆盖04:48:03.5244Z—04:50:10.4434Z，共126.9190秒，长于脚本名义总预算90秒；有效通信18.3575秒，后段为所属进程清理/CIM及录制收尾。原名义总预算未严格覆盖清理开销，不能宣称总采集在90秒内；原文件保留。此限制不改变同操作入队/回调机制证据，也未启动第二次候选观察；未来若确需复用诊断脚本，应先以已记录PID创建身份缩短CIM清理/先保存所属WPR，不能停止未知会话。正式Q01不做采样，不受此采样计时限制。

