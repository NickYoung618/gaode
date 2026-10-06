# Bug Assessment: 持久化冷启动与已启动PLC轮询重叠

- **Slug**: 008-startup-persistence-order（当前连续执行自动命名）
- **Created**: 2026-09-27
- **Source**: 008接续实跑r9/r10及r11完整启动跟踪、现有源码
- **Verdict**: valid
- **Severity**: high，当前启动通信受阻使正式主流程不能继续

## Report / Symptom

r9整体人工页面StoppedOrUnknown，r10比较在页面前Host启动失败。原Modbus交换期限1秒被触发，未知状态保留。r10同连接64709的tx16，PLC于19:08:43.2456835Z发回，Host直到19:08:44.3139438Z才继续首部读取，两端累计GC暂停0。不能把该失败归因于GC或PLC处理耗时。

## Reproduction

1. 独立Administrator/Session2 worker启动已有ASSEMBLY-A-E-MANUAL冻结Test配置及正式Host/VirtualPlc。
2. 原协议期限保持，观察启动门禁和两端诊断。间歇失败已保留r9/r10；并非每次必现。
3. r11只做12秒JIT/GC/线程跟踪短预检，trace完整解析67337事件，11101个JittingStarted；短预检通过不抵正式业务。既有r7跟踪的JIT路径含EF模型和查询构建；原截断文件不作完整根因证明。

## Suspected Code Paths

- Station01Registration.cs：PlcConnectionHostedService先注册/启动，Station01HostedService后启动。
- Station01HostedService.StartAsync：命令恢复、EF查询及未完成运行恢复在PLC服务已启动之后执行。
- Program.cs：app.Run之前没有完成这份真实持久化初始化。
- ModbusTcpClient：有原1秒期限，已发回结果的读取续体亦计入期限；不能放宽或把迟到当成功。

## Root Cause Hypothesis

**初始化与心跳重叠：高置信度源码事实；其对本次间歇超期的因果贡献：中等置信度，需实跑验证。** 持久化准备本来属于设备动作准入前的工作，不需要与心跳并发。将它前置可去除一个已证实的启动竞争源，不宣称解决所有OS调度、网络或运行期GC问题。

## Proposed Remediation

把现有持久化启动主体提取为一次性InitializePersistenceAsync；Program在app.Run/设备hosted services启动前等待其完成，Station01HostedService.StartAsync复用同一完成任务。保留所有真实SQLite查询/恢复、原服务顺序及反序停止、未知不重放规则，不预造数据，不改变外部API或协议。

Files likely to change: backend/src/Gaode.Host/Program.cs、backend/src/Gaode.Host/Lifecycle/Station01HostedService.cs。在008原T054/T069计划和任务中记录启动次序子交付。临时trace接线采证结束后撤回，不作为业务修复保留。

Tests: 现有HostLifecycleTests和Host重启恢复相关必要测试；独立构建后最少短WPF预检与实际整体人工路线，核真实数据恢复、无启动自动动作、PLC期限不变、实际采集/worker/保存/Final。不新增仅镜像调用结构的测试。

## Risks & Considerations

初始化失败必须阻止设备服务启动/HTTP受理；不可静默忽略。Initialize不派发运动；服务StopAsync顺序不改变。若正式运行仍超期，记录failed/partial并继续诊断，不将“降低冷启动竞争”写成通信根因已确定。

## Open Questions

其余运行期延迟来源仍待证据；不影响本次明确启动重叠的局部修正和必要验证。

2026-09-27 T054/T069启动子范围继续：r13 Q06失败交易发生在VirtualWorkerHostedService与HTTP完成启动之前，已启动PLC心跳与这些冷启动步骤重叠。PlcConnectionHostedService仍负责真实连接及释放，改为等待IHostApplicationLifetime.ApplicationStarted后连接；HTTP监听只表示接口可达，既有PLC连接/安全/worker就绪准入不改，未连接不得动作。失败沿BackgroundService停止Host并保留原设备超期，不放宽期限或新增重试。日志记录等待、真实设备启动及连接完成；既有停止顺序及必要生命周期/正式代表复验。
