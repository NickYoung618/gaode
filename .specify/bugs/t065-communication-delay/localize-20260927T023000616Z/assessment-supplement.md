# T065 一次默认调度通信定位补充评估

记录时间：2026-09-27T02:53Z。使用 speckit-bug-assess；本文件补充原 assessment，不替换工具 fix/test 或历史失败。业务根因未定，003 T065、008 T055/T070 未完成。

## 执行和有效窗口

- 专用 Administrator 任务运行前空闲，原请求已消费。Preflight 请求 `283f1a3440f047ff9e8def000d48fbdd` 在实际任务账户确认无 WPR/ETW 冲突及独立端点监听。复用已验证 WPT 20348.3694、显式配套依赖和冻结清单，未下载、未改全局配置、未取消或停止未知录制。
- 仅提交一次新 Observe：`7d76cee1ddfe4509a7b99bbc5b563d17`。通过 Schedule.Service 固定任务 `GaodeT065CommunicationDiagnostic.Run(null)` 消费请求；实际调用 `observe-default-20260927.ps1` → `observation-freeze-20260927-reconciled.json` 的私有启动链。当前规范脚本/冻结工具及受保护文件的前后 SHA256 一致，见 final-hashes.json。
- Host PID 8524，创建 02:31:24.2733403Z；VirtualPlc PID 10516，创建 02:31:22.1369648Z；所属算法 worker PID 7364。两端 inline=0、Test High 优先级，保留 io1000ms、心跳3000ms、poll50ms 及冻结 GC 参数。双端身份来自 process.json 的 actualPid，不把准备 helper PID1428 当作 PLC。
- 双端连接均建立后有效窗口：02:31:29.9543615Z—02:31:37.3333466Z，约 **7.379 秒**。最早实际失败 02:31:37.1410258Z。观察器 02:30:54.1449619Z—02:31:50.5621010Z 共56.417秒，其中准备约35.809秒，断连后窗口不能计为有效通信。
- 检测循环于02:31:41.4184531Z发现超期，计划保留5秒，实际到50.5621010Z才进入清理：检测延迟约4.277秒，计划后续窗口超出约4.144秒；如实保留，不声称准确5秒。总体未超过120秒，没有再跑。
- 独立 Host 26161 / PLC控制26162 / Modbus26163；Host 心跳49986和业务49987两个连接。无页面、配方、运动或复位；SQLite只读检查 Runs=0、Commands=0，activeRuns=0、stage=Idle。算法进程是现有冻结启动链的组成部分，未执行配方算法作业。

## 实际异常与最有价值的时间线

连接内以两端端点、connectionId、事务号、功能码及请求/响应内容核对，禁止跨连接只匹配事务号。UTC 以下均为原应用日志；相同10MHz单调 Tick 提供应用间对照，其 UTC/Tick 原点样本跨度约0.091ms，仍不报告微秒精度的跨系统因果。

|阶段|业务事务306（49987→26163）|心跳读取112（49986→26163）|
|---|---|---|
|Host开始|02:31:36.1395188Z|02:31:36.2007126Z|
|Host WriteAsync完成Tick|39750223215366|39750223830071|
|Host结果|ReadHeader超期，1001.5023ms，37.1410258Z|ReadHeader超期，1060.9819ms，37.2616996Z|
|PLC读到MBAP头|37.2864157Z，Tick39750234683511|37.2866142Z，Tick39750234685492|
|PLC完整接收|37.2864988Z|37.2866726Z|
|PLC处理/写响应|处理约0.0096ms，总处理及写约0.2867ms|处理约0.0064ms，总处理及写约0.1237ms|
|PLC响应完成|37.2867861Z|37.2867966Z|
|Host接收/有效echo|截止取消后没有接受对应响应|没有事务112的有效echo，不能以PLC读成功代替|

306请求 `0132000000060103004F0002`，PLC响应 `01320000000701030400000000`；112请求 `007000000006010100000001`，PLC响应 `00700000000401010101`。两者在PLC读头前已消耗约1.15秒/1.09秒；这是应用单调时钟间的毫秒级区段判断，不等同于内核在这时段传输了多久。37.1780046Z Host以business-poll来源锁存故障，阻断无自动重试。

最慢已知成功为心跳39：31.8293930Z开始、31.8295463Z写完、32.5701858Z读头、32.5702262Z完成，总740.8281ms。它是慢成功，不是超期。PLC环形窗口未覆盖其完整记录，不编造39的PLC时间线。66条完整双端成功匹配样本最大34.1932ms，不能以该子集否认39。最后完整有效echo是99于35.502844Z；没有据此确认独立3秒心跳看门狗超期。启动早期 LateValidEcho 与本次1秒I/O失败分别记载。

## 系统证据、关联强度和限制

ETL `observe-20260927T023052877Z/tcp-scheduling.etl` 614465536字节。显式WPR Network/filemode start及所属实例stop成功；xperf tracesstats和tracerpt XML退出0。ETL时间02:30:53.6340139Z—02:31:59.7862737Z，丢事件/缓冲区均0。XML6008130709字节，4376518事件；设备已经退出后进行离线分析，没有因解析慢重新启动。

tracerpt存在非目标解码错误111=1706649、15005=107356，不能声称全事件正确解码。Thread v3 PID/TID按已验证微软PerfView布局恢复，原XML和ETL保留。目标TCP provider解码45277条，没有其ProcessingError；有效连接生命周期内6866条端点/TCB候选。旧target-tcp.json只是初筛，遗漏业务客户端TCB且可能跨生命周期污染；以target-tcp-bounded.json为本次范围，保留旧派生结果及首次重复Guid字段提取失败记录。

四个TCB候选：心跳客户端C0E09920、服务端C3413050；业务客户端C40E8450、服务端C0D28BA0（均0xFFFF8104前缀）。心跳客户端有端点与ProcessStartKey直接证明；其余通过同生命周期、发送/接收序列和回环端点邻接推断，不能以事件执行PID判定TCB所属进程。系统记录36.1392347Z在业务服务端完成7字节TcpDeliverySatisfied，Request=C86FD0A0，随后12字节接收Seq3150225942；37.2863712Z在心跳服务端发10字节响应。**TCP事件没有事务payload桥接，不能将前者确定为306，或将后者仅凭时间认定为112。**112应用Write完成附近未找到对应可证明的TCP请求记录，该缺口仍在，零丢事件不保证provider产生每个所需事件。

CSwitch/ReadyThread目标窗口102326个连续切换检查，OldThread和前一NewThread不一致次数0。目标进程存在1.6—2.55秒级工作线程Ready等待，以及日志线程4秒以上等待；这是调度延迟存在的证据，**不是这些线程执行相应socket续体的证明**。Host未知角色线程9072在故障区段约576ms CPU，不能根据名称为空推定业务、GC或具体方法。瞬时pool pending=0/minWorkers=8也不能排除此前队列等待。当前.NET事件主要Loader/JIT，没有取得IO enqueue/dequeue与socket操作身份桥接。

XML错误标注+07:59，按ETL Header FILETIME恢复实际+08:00墙钟偏移，保留原字符串。应用UTC/Tick锚点一致；TCP CachedKQPCValues是缓存而非事件自身QPC，2329样本相对应用映射残差-0.291至14.956ms，不能借其计算伪精确内核延迟。原ETL仍可恢复真实事件QPC。调度等待用同ETL时基计算，跨源使用区段及容差，不混同Write完成与内核送达。

## 机制判断和最小修复边界

已证实：双连接实际1秒I/O失败，PLC读到头显著迟于Host写完成，两PLC操作随后处理很快、几乎同时恢复。优先假设是PLC读完成通知/托管续体恢复延迟，或其对应线程就绪但没有获得CPU；置信度中等。替代解释仍包括发送/接收完成通知之前的内核路径等待、托管队列等待和观测/JIT负载。不能把TCP候选直接当事务证据，也不能认定r22初始HTTP超期同因。

源码已设置双端NoDelay=true，因此没有依据补NoDelay开关。现在不足以选定业务补丁；不改超时、GC、优先级、线程池阈值，不盲加Task.Run或ConfigureAwait。候选涉及 `backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs` 与 `VirtualPlc/ModbusTcpServer.cs` 的socket await路径，但只有取得操作/续体桥接后才能决定改哪一端、哪条队列。若后续涉及共享业务接口，先同步对应spec/contracts/plan/tasks。

唯一关键缺口：**同一socket操作从系统完成、IO队列入队/出队到执行Read续体的身份及时基桥接**。下一步仅补该证据：先离线从当前ETL恢复原始QPC及可用的Request/TCB桥接，若本ETL没有这些事件，再以冻结输入、新独立端点、inline=0做一次准备后有效通信最长20秒的启动阶段短实验；只增加.NET Threading/IO事件及必要的Ready/CSwitch栈，不增加配方负载。区分“完成后托管IO队列迟迟出队”与“已出队/Ready的具体续体线程迟迟未运行”。微软.NET文档列出ThreadPoolIOEnqueue/Dequeue/Pack及NativeOverlapped字段，但必须核验实际运行时是否产生、是否能桥接socket，不能以profile声明为成功：[Microsoft runtime thread events](https://learn.microsoft.com/en-us/dotnet/fundamentals/diagnostics/runtime-thread-events)。若无法无源码完成桥接，先形成限定诊断插桩方案，另行进入bug-fix，不本轮篡改冻结程序。

r15/r17发生于旧Host5A335…；本轮Host90BDA…、workerEDBE…，PLC66F…一致。r17还包含Host inline=1及配方运行；r15是无Run准入失败。当前无Run且双端inline=0仍可失败，因此不是仅完整配方负载才触发，但不能称与历史同构严格对照。历史元数据25ms与实际50ms轮询差异已记录，不据元数据制造参数变化。

修复前后必须固定build/input、双端inline、基线期限/GC/优先级与诊断profile，分别检查连接内事务、有效echo、真实业务失败锁存、CPU/Ready等待及核心流程结果；先定位才选择补丁。003 T065须根因证据、最小修复及同条件前后对照；008 T055须既定合法配方端到端真实数据库/设备/算法/前端验收；T070须完成其要求的流程覆盖和收口审计，工具验证或这次观察不能替代。

## 收尾

原result.json仍为CleanupIncompleteRequiresReview，原清理快照有暂存残留，未覆写为成功。后续进程复查全部所属PID退出；实际Administrator后置Preflight `a844dd7dd419492d9633539b036d8991` 于02:44:35完成，WPR未录制、无所属/冲突collector及26161/26162/26163监听。02:53:11最终任务Ready(3)、instances=0、请求已消费，protectedHashesUnchanged=true；见final-state.json。没有重放Observe，也没有停止他人worker。

## 下一步可复制提示词

```text
$speckit-bug-assess slug=t065-communication-delay
读取 localize-20260927T023000616Z/assessment-supplement.md、evidence-index.md及其ETL。只解决同一socket操作的系统完成→IO队列→Read续体身份桥接。先对现有ETL离线恢复原始QPC、TCB/Request及可用runtime事件，不再运行原120秒Observe。若现有ETL确实缺事件，准备项目内最小profile和固定任务受限接线，核验.NET Threading IOEnqueue/Dequeue/NativeOverlapped实际支持，并执行一次准备后有效通信最长20秒、首次超期短后窗即停止的启动阶段实验；仍用冻结Host/PLC、双端inline=0、新独立端点，保留原期限/GC/轮询/优先级，不跑配方或页面。实验只区分IO队列出队延迟与已Ready的对应续体线程调度延迟；无法建立socket操作映射则如实停止，不循环重采。需要业务诊断插桩时先提出可审阅的最小方案，不直接改冻结程序。保存关联、丢事件、UTC/QPC及清理证据。r22 HTTP独立，T065/T055/T070不勾选；新增时间戳补充评估，保留全部历史。
```
