# 身份关联诊断实施依据（执行中，结论追加）

- Slug: t065-communication-delay，用户显式指定；Verdict: valid；Severity: high。
- 原assessment、工具fix/test及localize评估保留。本轮授权评估后接续最小诊断修复/验证；业务根因充分后才接续业务修复。

## 已有证据优先与明确缺口

已对同一614465536字节ETL使用微软TraceEvent3.2.6重新读取，4376518事件、lost=0，原始事件QPC取得。原Network profile runtime keyword=0x20098，不包含Threading=0x10000；原ETL中全局事件61—65计数为0，是采集缺口，不能靠改解析器补出IO身份。

实际.NET运行时10.0.12；对应tag源码已归档。PortableThreadPool默认Poll用GetQueuedCompletionStatusEx，BatchEnqueue进入ThreadPoolTypedWorkItemQueue；其BatchEnqueue没有逐操作IOEnqueue事件。事件64在Event.Invoke执行回调前记录NativeOverlapped；事件65在分配overlapped时记录，可用PID+NativeOverlapped+生命周期区分。不能预设63/64覆盖所有批入队操作；不能把NativeOverlapped当TCB或IRP。

原ETL在Header到来前，TraceEvent的转换UTC甚至显示未来日期。新增派生文件保留这些值而不直接用于跨源比较；以原始QPC及可靠Header/应用双锚点判断，校准或偏差不充分时不输出伪精确延迟。第一次启用Dynamic的解析器等待系统元数据，停止仅本轮已核实身份的解析器，保留partial；改为Kernel/CLR注册后同一ETL重读完成，没有重采。

## 最小诊断插桩方案（speckit-bug-fix依据）

公开NetworkStream/ValueTask不会给出socket操作到NativeOverlapped的业务身份。按本轮明确授权，在**独立源码副本及诊断构建**实现以下插桩；不改工作区业务源码，不替换旧冻结DLL。

1. 对Host ModbusTcpClient和PLC ModbusTcpServer的实际WriteAsync及ReadExactly循环记录唯一opId、connId、端点、socket handle、事务（PLC头读取之前未知，随后以接收到的MBAP实际字节关联）、PID、原生TID、Stopwatch QPC、边界和长度。
2. 在ReadAsync/WriteAsync返回待完成ValueTask之后，仅读取内部_obj所引用SocketAsyncEventArgs的_pendingOverlappedForCancellation值；不解引用或改写指针、不接管完成回调。该字段在10.0.12 ProcessIOCPResult的可取消IOPending分支赋值，完成清理会清零。同步完成/竞争清理或缺字段均明确记零/缺失，不硬造关联。
3. 新项目内ETW provider记录边界，profile增加CLR Threading、Kernel CSwitch/Ready及必要栈。PID+非零NativeOverlapped+本次操作时间区间关联runtime64/65，再与Read返回实际TID核对；await前后TID不充当队列身份。批入队时刻仍须系统完成事件支持，否则标注A/B分解不完整。
4. 插桩含反射、JSON与ETW开销，记录自身采样跨度；这是独立诊断构建，不与旧原构建声称严格控制对照。只为20秒有效通信，一次新请求，无配方/页面；保存后还原正式调用链或保留显式独立受限诊断入口，诊断构建不进入生产/页面。

PDB文档SHA256已核验Gaode.Host、Infrastructure、Application与VirtualPlc全部非生成源文件匹配旧冻结构建；副本由这些来源创建。保留副本插桩前文件、构建命令和摘要。私有实现和独立ETW诊断合同不修改共享业务接口，当前无需更改业务spec/contracts/plan/tasks；如果后续修复涉及共享接口，则先同步四类设计文件。

## 必要验证及根因修复门槛

### 首个有效观察后的必要诊断增量

首个有效短观察已取得359个唯一NativeOverlapped→IODequeue映射。事务21的Host操作86直接匹配NativeOverlapped 0x14363EDD2B0、IODequeue线程6588；出队至Read返回仅0.0523ms，之前等待1837.6367ms。该线程期间实际执行其他工作，最长Ready间隔约6ms，不能以早期泛化Ready等待选补丁。原profile没有内核Thread/Enqueue或Dequeue（62/63）；CLR63存在的低位“指针”是其他work item的内部标识，不能冒充该socket的批入队。

因此在现有诊断副本增加只读、只针对已追踪NativeOverlapped的批队列快照。对应10.0.12源码已核对PortableThreadPool.ThreadPoolInstance→_ioCompletionPollers→_events→ThreadPoolTypedWorkItemQueue._workItems（ConcurrentQueue）。独立后台诊断线程每10ms只读ToArray，不移除/重排/改写工作项；每个匹配记录NativeOverlapped、opId、拷贝开始/结束QPC及队列长度，空缺/读取失败明示，25秒自动退出。快照的观测时刻是区间，不冒充精确enqueue时刻；快照有分配及段保留开销，另记最大采样间隙。只有观察到同一指针持续在此队列，才能给出可证实队列等待下界。

这个新增字段具有明确判别能力，按用户第三节授权执行独立queue-build及一次有效通信最长8秒的验证；保留旧diagnostic-build及其helper快照。不是同条件重跑：唯一目的为证明/否定操作是否已进入托管批队列。不扩大负载或自行设置线程池/GC/优先级/inline。

实际短实验须证明非零NativeOverlapped与CLR64/65的生命周期关联、连接内MBAP事务关联、具体TID及QPC时序，记录零值/丢失/无法映射比例；并核验清理。若证据能区分IO完成后队列等待与对应线程Ready后CPU等待，追加根因/置信度/最小补丁再修复；若未复现，不把正常运行关闭T065。r22 HTTP独立。

003 T065仍须实际机制最小修复及前后对照、真实超期安全锁；008 T055须其当前Q01正式页面全条件和前置；T070须各适用证据对账。未满足不改勾选。已读取直接任务/合同、当前completion-review与task-audit，复用已有通过事实，不重做配方矩阵。

## 执行后补充结论（2026-09-27，原结论不覆盖）

**诊断身份桥接已建立；通信业务根因未达到补丁门槛。** 已实施、验证必要诊断插桩和采样工具修正，未实施业务补丁。诊断只在独立副本和构建内，旧冻结构建保持原样。

### 对原ETL的优先分析

原事务306/112的ETL没有CLR Threading身份事件，原事件61—65为0；这是采集缺口。重读恢复了原QPC，但不能补出未采集的IO身份。后续新故障事务21不能倒推为原306/112同一操作、同一具体原因。

已依据实际.NET10.0.12官方源码核对：默认IO轮询用GetQueuedCompletionStatusEx后批量入队；IODequeue64在实际回调前带NativeOverlapped，批入队没有逐操作IOEnqueue63。官方源码归档及链接见证据索引。原评估中“必要栈”是准备项：实际这次没有把完整栈作为身份桥接证据；TCB、IRP Request和用户NativeOverlapped保持不同类型，不互换。

### 分次执行、失败及边界

| 请求/目录尾缀 | 实际状态 | 本轮处理 |
|---|---|---|
| 3e4e04ca… / 031204112Z | 准备时空日志Regex失败；没有有效双端窗口 | 保存所属ETL，保留失败入口，不算未复现 |
| 0edd4762… / 031416275Z | PowerShell NullString仍传入Regex；准备失败 | 用真实零字节日志验证空值门禁，保留失败入口 |
| 0b2ecbb0… / 031655683Z | 真实事务21超期，成功取得socket→NativeOverlapped→IODequeue→Read身份 | 同ETL离线分析及连接序号关联；359/359非零快照唯一映射 |
| 38ec38b3… / 033201901Z | 新只读批队列探针；启动阶段真实超期，探针初始化晚于关键操作 | 不能称完成有效观察；完整ETL已保存；磁盘满导致第一次解析失败，释放仅本轮派生数据后重读同一ETL |
| aa925da0… / 034312546Z | 诊断预热/就绪门禁修正后的最短验证，无超期 | 双端实际连接并Host Ready；约8.397秒有效通信，未运行页面/配方 |

不是相同条件循环采样：准备失败仅修实际脚本问题；身份插桩之后只读队列样本是新增判别字段；发现队列探针启动过晚后仅修覆盖问题再验证。现在停止采样，不原样重复8/20/120秒窗口。

正式基线仍是冻结Test/HighBeforeReadiness、双端inline=0、io=1000ms、heartbeat=3000ms、poll=50ms及原Server GC。既有Host启动日志的worker minimum 8是旧源码行为，不是本轮调整。新诊断构建反射、JSON/ETW、只读队列快照及预热均增加开销/改变冷启动负载；与旧冻结构建不构成严控的业务修复对照。

### 最有价值的因果时间线：连接58564→26463内业务事务21

Host PID5736；PLC PID8268；Host Read操作86；NativeOverlapped=0x14363EDD2B0。下面是原始QPC，频率10000000，来自同机Stopwatch与ETL原始QPC；UTC转换只用于锚点核对，不使用Header之前错误的转换UTC。

| 边界 | QPC | 身份依据 |
|---|---:|---|
| Host请求写入完成，op85 | 39777665138015 | 本连接请求0015000000060103004F0002；应用完成不冒称内核送达 |
| PLC读完请求头，op86 | 39777665140611 | 真实收到MBAP头；连接内事务21 |
| PLC读完请求体，op87 | 39777665142525 | 实际内容03004F0002 |
| PLC响应写开始，op88 | 39777665142849 | 响应00150000000701030400000000 |
| Host TCB HeaderDeliverySatisfied1158 | 39777665143348 | 本连接TCB＋7字节＋RcvNxt986095776；Request不是NativeOverlapped |
| Host TCB Receive1074 | 39777665143629 | 13字节、Seq986095776；21次完整有序响应字节账本匹配 |
| Host IODequeue64 | 39777683514133 | PID5736、同NativeOverlapped、在本操作生命周期内，TID6588 |
| Host Read头返回，op86 | 39777683514656 | 收到00150000000701，TID6588与IO回调线程相同 |
| 随后Read体取消，op87 | 39777683522831 | 原1秒期限真实取消，未伪造成功 |

Host写完成→PLC读到头约0.260ms；PLC响应写至内核记录约0.08ms。主要延迟在Host内核收包记录到IODequeue前，约1837ms；IODequeue→Read返回0.0523ms。socket→NativeOverlapped→回调/续体是直接关联；TCP字节→该响应是连接生命周期、序号及完整字节账本支持的**推断关联**，未获取内核IRP→用户overlapped直接映射。核验旧TraceRpt已知TCP字段元组，1074/1158/1033的expected-minus-decoded均0；额外元组来自更完整QPC读取，不当作模式匹配失败。

目标TID6588期间执行其他工作，最长单次Ready等待约6ms，不支持“该线程一次Ready等了1.8秒”。同期Host/PLC Tiered Compilation线程占用CPU较多（2CPU），只说明竞争候选，不能证明应改GC、优先级、线程池或inline。原306/112延迟偏向PLC接收前，本轮21偏向Host接收后，不能未经证据合并具体原因。r22 HTTP超期继续独立。

### 队列探针验证与限制

033201901Z只有8个操作，1个非零overlapped唯一映射到取消完成；探针到03:32:31才Started，关键Read已在03:32:29.990取消，故没有覆盖。未把零QueuePresent当成未排队。该样本的应用日志输出延迟/启动开销也存在，不拿总交换日志时长代替实际Read阶段。

修正后只在独立诊断副本constructor阶段初始化反射/EventSource及事件形状，等待探针Started才进入业务通信；20秒就绪门禁失败即准备失败，不延长任何I/O期限。只读探针寿命90秒是覆盖准备的总上限，后台线程随所属进程退出；空tracked集不拷贝队列，快照仍10ms。不写/取出/排序队列；ToArray有分配、段保留及测量扰动，不能叫零开销。

034312546Z共3248个socket操作，1074个非零快照、1072个唯一64映射。两个PLC末尾Read没有匹配64，不编造；清理边界的未完成操作单列。8条队列事件中6条处在真实操作生命周期内，均单次快照、queueSize=1：只能证明真实操作在队列中存在，持续排队下界是0，不能证明超过1秒。预热模拟事件id=0、ptr=0全部排除。进程在90秒探针期限前清理，没有自然结束Summary，最大扫描间隙未取得，不能声称整窗每10ms必定采到；ETW lost=0亦不等于采样快照无盲区。

本次窗口未检测到超期，Host最慢socket26.1994ms，PLC最慢RequestHeader101.0058ms（含等待新请求，不能当处理耗时/故障）。预热降低了冷启动诊断开销，未证明消除了旧构建根因；不能以本次正常完成关闭T065。

### 工具、清理与保护

三份本轮新增大JSONL已逐流压缩并以解压SHA256核对后换成.gz；保留原ETL、旧XML、应用日志及历史失败。磁盘满那份157868032字节partial原样保留，同580911104字节ETL离线重读：2834701事件，errors=0、lost=0。流式压缩解析器从原解析器独立构建并绑定，原reader保留；新验证ETL484442112字节，2879641事件，1038173条派生记录，errors=0、lost=0，WPR stop=0。旧failure/result未覆盖，后补恢复结果单独存放。

最终管理员Preflight请求3b827b5c…完成，实际WPR未录制、ETW会话清单保留。所属Host/PLC/worker/launcher/conhost全部已核验不存在；端点只剩TIME_WAIT PID0，无Listen或所属存活连接。未停止其他worker/未知录制，未执行wpr-cancel。旧默认观察入口没改业务调用；新增ObserveIdentity是固定路径、固定SHA的受限任务分支，不接受任意脚本/命令。

原assessment/fix/test、任务勾选及来源文档保持不变。正式工具链新增诊断依赖的摘要有祖先备份，WPT/TDH实际字节与原工具proof不变；不存在“只改摘要解除工具故障”。正式默认ReviewOnly已重新核验当前工具清单。

### 最小补丁及关闭条件

当前**不能确定业务补丁**。唯一关键缺口：在同一真实超期socket操作上，已知NativeOverlapped何时进入托管批队列（或对应IOCP返回边界），以区分A已完成后批队列久等与C完成通知/轮询更早环节；B“目标回调线程单次Ready长等”在事务21不获支持。工具/诊断的最小修改已按fix/test完成，不能冒称业务修复。

如将来证实A，才评估更改那个已证实的派发机制；如C，才针对完成通知/轮询路径。未经同操作边界证据，UseWindowsThreadPool、调大worker、改GC/优先级/inline均仅是假设，不提交生产补丁。修复前后须保留同端点独立集、构建/运行时、原参数、冷启动阶段/负载与profile一致，比较具体阶段队列/完成延迟，并验证真实1秒I/O和3秒心跳超期继续锁动作。诊断预热前后不是该业务修复对照。

- 003 T065：尚缺上述根因分解、机制最小修复的受控前后对照及真实超期安全锁验证；本轮真实取消只验证传输拒绝，未运行运动安全锁场景。不勾选。
- 008 T055：前置T065未解除；当前版本USR-E正式Q01页面同run完整链及必要F失败/位移变化证据按原任务核验，复用未受影响的已有合格能力，不能用诊断Idle或旧Q01Passed替代。不重开发已验收Test主流程。
- 008 T070：T055及现有其他未通过当前验收项未闭合，仍需当前适用Q/C/F与SC证据对账；真实设备/现场标定限制单独保留。无权用本轮工具通过或短窗口正常代替总验收，不勾选。

### 已有栈的离线补查（不是新增采样）

Network基线实际声明CSwitch、ReadyThread等System Stacks；本次确认它们确实在旧故障ETL内，而不是仅profile声明。原紧凑reader未导出StackWalk，属解析范围缺口，已独立扩充reader-stacks注册CLR Rundown后重读同一031655683Z ETL，未启动设备。初次ClrRundown API/namespace编译失败日志均保留，准确命名空间由已安装TraceEvent3.2.6 XML核对后构建。

全ETL StackWalk/Stack为1161796条；故障附近导出222525条，目标Host回调6588、IO轮询9092和Tiered Compilation8036在线性QPC窗口内68736条；原始帧数检查0不一致。解析总事件3325027，errors=0、lost=0。Stack payload内trigger QPC和header QPC分别保留，绝不使用故障Header之前的错误UTC。

尝试以本PID CLR方法地址范围与实际Rundown命名，未得到可信托管符号匹配；771999个帧未命名，不编造原生符号或把帧频率当CPU时长。栈记录不含该操作NativeOverlapped参数和入队时刻，不能凭IO轮询线程的栈给事务21配入队边界。原生符号及地址历史的局限见fault-stack-analysis.json。补查没有解除唯一A/C操作边界缺口，也没有增加采样次数。
