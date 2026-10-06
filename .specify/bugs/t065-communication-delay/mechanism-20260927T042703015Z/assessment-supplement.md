# A/C机制判定及最小诊断实施依据

Slug: t065-communication-delay；用户明确指定；Verdict valid，Severity high。保留原assessment和所有历史报告。本轮依T065原条件，定位充分后立即修复/对照/安全及正式Q01，不要求证明全部运行时细节。

| 假设 | 支持/否定 | 方法实际观测 | 补丁门槛/放弃条件 |
|---|---|---|---|
| A 已入队派发晚 | 同操作BatchEnqueue返回早，64回调晚；相反短间隔不支持A | Poll调用BatchEnqueue前后原始QPC＋nativeOverlapped；64回调＋Read身份 | 队列后的主要延迟已定位，并受控切断该路径符合预测后选最小修正 |
| C 更早完成/IO轮询 | 本连接响应内核记录早，BatchEnqueue晚，随后64快 | 连接内字节账本、Batch前后、64；不冒称区分内核通知和poller每个内部子步骤 | 针对已定位轮询/完成派发路径的受控实验；不新增完整运行时证明门槛 |
| 方法失败 | 目标真实非零overlapped没有前后成对事件、native64无法覆盖或源码调用形状不符 | 缺记录不是未入队 | 停止依赖该方法，不采普通重复窗口、不选业务补丁 |

选一个实现：Lib.Harmony在独立进程对PortableThreadPool.IOCompletionPoller.Poll作transpiler，只在准确BatchEnqueue调用前后注入事件2。复制栈上的value-type Event到新增局部，动态helper直接读nativeOverlapped字段，不修改值、不改变循环/参数/错误/回调顺序；每个边界只有ETW typed二进制事件与QPC，避免逐次JSON/反射。方法记录处在caller内，避免BatchEnqueue被内联导致10ms快照式覆盖问题。补丁ID仅该诊断进程，退出时随进程消失，原runtime/冻结DLL不改。

先独立本地回环socket验证挂接和真实操作字段（只工具/诊断探针，不模拟PLC）。通过后才接线唯一真实短观察。正式观察不加队列快照、不改变原1秒/3秒/50ms、GC、优先级、inline0。诊断挂接/初始化开销与基线固定，受控实验一次一因素；不能用预热前后正常代替修复。

## 已捕获机制与受控反事实（实验，不先设默认）

678个非零socket操作均唯一64并成对Batch前后；Host10188/op117业务tx28、op118心跳tx12分别入队至64等待230.7873/229.8716ms，入队之前仅2.2074/2.1887ms。此样本支持托管入队后派发延迟A，不同于旧21、不能倒填旧21入队事件。此窗实际3秒心跳故障，Host周期调度等待4285.4026ms；作用路径还包含定时继续调度，不冒称此次1秒I/O复现。

选择最低成本受控机制变量：DOTNET_ThreadPool_UseWindowsThreadPool=0/1，仅所属双端。同一control-build DLL和同样Poll挂接初始化/profile、输入、冷启动状态/负载、GC/优先级、1秒/3秒/50ms/inline0，分别一次短基线与候选。Native模式仍运行相同Harmony初始化/安装，但Portable.Poll未使用，不以缺事件误判；实际runtime64、NativeOverlapped和具体Read边界继续核验。原生ThreadPoolBoundHandle通过CreateThreadpoolIo/OnNativeIOCompleted直接完成回调，切断Portable.IOCompletionPoller→typed batch queue→worker高优先级工作项路径；预期内核响应至64/Read及定时调度空档缩短。反例：相同长空档保留在更早传输或Native回调/CPU调度段，或只因观测初始化差异变好，不支持这条修复。

微软明确Windows原生池不支持SetMinThreads/MaxThreads。为了让同一实验DLL可启动两种模式，三个旧最低线程检查在实际Native模式记录native-managed而不虚报预留8；Portable模式保留原8。此为provider选择的API/策略后果，不宣称两个实现拥有相同的内部最小线程数；不改任何Deadline、安全、IO或业务完成判据。既有API设置差异及源差异明确列入对照。源码未证明原生设置在本项目有效前不改正式默认；取得前后具体阶段证据后再正式最小接线及安全/页面验收。

## 最后机制证据、受控结果及修复实施

基线请求aa31dcbc977f4fa6b06fe1d6826bf23c，Host5380、PLC7140，程序与候选DLL摘要完全相同。业务连接72e6bf11-fc41-4e52-a13c-9a51d2a9bd70、端点58475→26863的txn3/op14，与心跳连接9a106309-7a72-497e-8eed-9d06a68e676d、58474→26863的txn3/op16分列，不能跨连接混认txn3。对应NativeOverlapped分别2142794697840/2142794696880；从Read开始到批前仅0.6107/0.7041ms，批前后0.006/0.0072ms，批后至64却1112.2005/1056.6997ms；64到Read返回0.0846/0.0339ms。真实1秒超期，Host因启动阶段BackgroundService异常退出，日志保存failure latched、新epoch、禁新动作/不重发。启动器StartupFailed及CleanupIncomplete原结果保留，不能称完成就绪/无复现；但实际双端socket操作及目标机制证据有效。准备04:44:33.8306→实际第二连接04:44:57.6152；首次故障已落在应用日志04:44:58.77附近，观察器发现04:45:00.1502Z不能替代应用故障时间。窗口结束04:45:01.6295，后窗实际约2.8秒，不把就绪前失败计入正常持续运行。

候选请求d7fff7a16f0b4dd687c90493f23825bc，Host1664、PLC10660，两端实际Native=True/inline0；同DLL/同初始化/同诊断profile、同Test Q06配置，仅运行时provider0→1，端点和数据隔离；Windows原生池内部线程最低值0是其API策略后果，不虚报与Portable8完全相同。9362操作，2937非零指针全部唯一关联64，Native模式63/64实际采到、typed Batch事件0符合所切断源码路径；1560Host响应头最慢20.2574ms，最慢64→Read0.0206ms。实际连接窗口04:48:33.9831→04:48:52.3406，共18.3575秒，轮询因开销越过内部12秒目标，仍低于本次机制实验20秒上限；不伪称严格12秒。观察时间不含保存/离线解析/清理；原CleanupIncomplete保留，后续所属PID不存在及管理员WPR/ETW空闲另作补核。

结论A由真实故障操作直接支持；无需继续要求运行时每个内部细节。BatchEnqueue只证明已入托管批队列，不单独证明CompleteBatch请求worker的时刻或哪个worker饥饿。修复范围覆盖其后批派发路径：Windows Native ThreadPoolBoundHandle的CreateThreadpoolIo→OnNativeIOCompleted直接回调，绕过Portable Poll/typed批派发。正常候选不独立证明机制，但上述边界＋同DLL变量对照符合预测，足以选该Test运行接线的最小修复。高置信度适用于新故障样本；旧txn21、3516ms及r15/r17没有新增入队记录，不回填、不一概同因。冷启动、Harmony初始化保持相同；宿主VM/其他账户瞬时CPU不可完全控制，不能宣称所有环境性能保证。

官方依据：[微软线程池配置](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/threading)、[.NET10.0.12 ThreadPoolBoundHandle源码](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Threading/ThreadPoolBoundHandle.WindowsThreadPool.cs)、[配置优先级源码](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/AppContextConfigHelper.cs)。按用户授权已先同步003/008直接spec/contracts/plan/tasks，再实施三个最低线程API守卫、共享小型运行配置判断及Test launcher/正式Q01开关。无业务期限/信号/API/流程更改；旧默认与冻结程序保留，正式构建无Harmony/诊断插桩。正式Q01限定新DLL、Windows Test fixture显式开关，新固定任务动作只消费摘要固定的入口/输入，不扩为任意命令。

原期限必要安全三测试Passed：真实TCP响应被丢弃后I/O1109.307ms进入PlcCommunicationUnknown、epoch1→2、Connected/SafetyClear=false且新Move被PlcUnavailableOrActionInFlight拒绝；PauseHeartbeat后3057.287ms进入PlcHeartbeatLost、epoch1→2且拒新Move；真实1秒Modbus测试保持3个请求、无自动重发并保留超期/慢成功日志。原首次测试编译错误保留，当前TRX3Passed/0Failed/0Skipped，实际.NET10.0.12 Native=True。拒绝用Test目标只用于证明门禁，在拒绝后不会发送PLC运动，不定义生产点位或轴含义。正式Q01仍在执行，未勾T065/T055/T070，完成后按原条件逐项对账。


## 原条件最终审定


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

正式Q01 route validation18/18、SQLite只读quick_check=ok、cleanup=true，已逐条件审定；授权的三项勾选已更新，其他勾选逐行比较不变。

补充计时限制：候选xperf记录ETL覆盖04:48:03.5244Z—04:50:10.4434Z，共126.9190秒，长于脚本名义总预算90秒；有效通信18.3575秒，后段为所属进程清理/CIM及录制收尾。原名义总预算未严格覆盖清理开销，不能宣称总采集在90秒内；原文件保留。此限制不改变同操作入队/回调机制证据，也未启动第二次候选观察；未来若确需复用诊断脚本，应先以已记录PID创建身份缩短CIM清理/先保存所属WPR，不能停止未知会话。正式Q01不做采样，不受此采样计时限制。

