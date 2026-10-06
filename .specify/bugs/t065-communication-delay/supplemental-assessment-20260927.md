# 补充缺陷评估：T065通信异常延迟（2026-09-27）

- Slug：`t065-communication-delay`，用户指定，沿用已有缺陷。
- Source：本轮用户要求；无外部URL。
- Verdict：**valid**；Severity：**high**，阻塞008 T055/T070原收口依赖。
- 本轮状态：**BlockedBeforeObservation**。系统采样启用失败，通信观察没有开始；不能写成“120秒未复现”。
- 输出方式遵从本轮明确授权：保留原`assessment.md`，新增本补充评估、[证据索引](evidence-index-20260927.md)和采样脚本。不修改业务源码、共享接口、功能规格、任务勾选或历史失败。

## 当前范围与已证实事实

依据AGENTS.md、宪章7.0.0及003/008当前spec、contracts、plan、tasks；审计入口实际位于`specs/008-recipe-driven-inspection/evidence/task-audit-night-20260927.md`，不是feature根目录。completion-review与审计末尾“最终本轮交付/最终原条件审计”优先于前面的历史检查点。

1. **选定Test主流程与必要验收已完成，008整体未完成。** T049—T070共22条，20条勾选，T055/T070未勾；003 T065未勾。已验收的暂停、人工、成组、整体、旋转、真实保存/查询、故障完整新轮等能力不重复开发。当前质量清单15/16保留，不引入22/14/8条全实跑要求。
2. r15与r17真实通信超期有效，应用端有双端关联分段，但没有系统TCP与线程调度时间线。Test inline比较仅为缓解/路线可运行证据。已有Host/PLC最低线程8的修正仍不足以消除后续故障；不能再凭该配置认定根因已解决。
3. 本轮执行账户`10_3_0_13\CodexSandboxOnline`、Session0、Medium令牌，没有管理员组或系统性能采样特权；只列SeChangeNotifyPrivilege与SeIncreaseWorkingSetPrivilege。首次`whoami /all`命中Git工具链的同名命令而失败，随后以`C:\Windows\System32\whoami.exe /all`正确核验，权限结论只取后者。
4. 本轮2026-09-27 **01:12:07.9368987Z**唯一WPR启动能力探测：`wpr -start Network -filemode`，exitCode=-984068079，**0xc5585011**，原文`Failed to enable the policy to profile system performance.` 前后均`WPR is not recording`，ETW仍为原MSDTC_TRACE_SESSION。不存在本轮成功创建的采样会话，故没有执行stop/cancel、没有启动Host/PLC/算法/配方。
5. 旧00:58诊断报告引用清单的16个有摘要文件均匹配，本轮验证结果另存；不把摘要检查算业务测试。当前冻结Host SHA256=`90BDA14E495B482F98FD841189314499B829DB8508177C1EB88D7C8B8D295409`，PLC=`66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF`。
6. 桌面worker9428仍为pwsh/Session2，与r22 worker-ready文件相符；本轮仅只读核验，未触发任务、追加队列或改变暂停信号。存在进程不等于新验收通过。

## 已有异常的事务级定位

本轮从原communication-readback.json按**端点＋事务**重新提取记录，副本见证据索引。应用层时间均换算UTC；下述跨进程间隔使用UTC，不直接相减未经核验的不同进程原始tick。重复窗口中的同一记录不计成多个故障。

| 样本 | 可核验时刻（UTC） | 已测区段与结论 |
|---|---|---|
| r15，Host连接5d406340…，端点58574→25164，事务78/function1 | PLC响应写完20:00:14.9550229Z；Host首部读完20:00:16.4335785Z | 间隔**1478.5556ms**；Host门锁0.0029ms、写0.0537ms、无需重连；PLC处理及响应写0.0244ms；Host总1479.6819ms、1秒取消已生效，后续正文读取失败。长空档在响应写完成与接收应用完成之间，非已测请求处理段。 |
| r17，Host连接92f96dca…，端点60123→25154，事务9478/function1 | Host请求写完20:15:49.799033Z；Host失败20:15:50.7994944Z；PLC首部读完20:15:54.5024904Z | 写完至PLC首部读完**4703.4574ms**；Host门锁0.0022ms、写0.0514ms、无需重连、总1000.5098ms；PLC接收后处理及响应0.0133ms。长空档在发送应用完成与接收应用完成之间。 |

这里的“写完/读完”都是应用观察，不能解释为网卡发送/内核送达时刻。r15取消后应用首部才完成也不证明取消计时器为何晚或数据何时已就绪；相关回调、计时器、线程Ready→Running和VM调度仍缺证据。门锁/连接/处理短只排除**这两个样本中相应已测段**，不排除其他样本。

原2026-09-24的3516.2ms有效echo缺口仍为历史事实，原Host分段缺失不能恢复或用新事务倒填。有效应答须看VirtualPlcEngine的`echo-observed.valid`和`lastValidEchoUtc`，不能只看function5确认包。

## 根因假设及证据缺口

**症状及应用区段置信度高，唯一机制置信度低。** 接收线程/Socket完成续体调度延迟是中等可信候选，双端都可能受影响；r17仅Host inline仍失败不能将范围只限Host。TCP传输/回环栈、接收线程未获运行、完成分发排队、进程或VM暂停仍未分开。没有证据支持此时扩大线程数、改变GC、提升优先级或把双端inline设为正式补丁。

缺少同一故障窗口的：

- TCP发送/接收事件及回环覆盖、连接映射，必要的序号/字节量。TCPIP ETW未必含Modbus payload，不承诺凭事务号直接过滤系统事件；要先用PID/连接四元组/时间/报文字节和应用事务关联。若多笔无法唯一匹配，写无法关联。
- Process/Thread、CSwitch、ReadyThread的QPC时间线，线程身份与Socket完成/续体对应；Ready后长等待支持调度等待，线程尚未Ready不能据此宣称线程池问题。
- ETL时间基准、EventsLost/BuffersLost及覆盖窗口；“已启用profile”或“有ETL”均不足以证明有效采样。
- 故障事务之后的有效echo接受/安全锁停及无自动重发的必要窗口。GC累计、瞬间pendingWork=0不能排除区间内停顿。

Network profile清单确实包含TCPIP、CSwitch/ReadyThread；**本会话未能启动**，所以未验证回环事件字段、数量、丢失或关联能力。不能把文件系统权限或profile清单当成实际采样能力。

## r22 HTTP超期独立核查

失败包为r22 job001 RECOVERY-F，旧run=`3955ced1-bb60-4c67-a3c3-e8886d58122a`，Host11284/PLC10448，HTTP端点25152/25153，双端显式inline=1。

- Host日志396/418/419/433/447行关联外部POST recovery-reset的traceId=`0HNOS2KK17424:0000003D`：内部HttpClient.Timeout=1秒超期，堆栈到`LatestProtocolPlcDevice.ReadInitialStateAsync:140`、`FixedMoveRecoveryInteraction.ResetAsync:89`，外部请求总体1429.3363ms。
- 源码113—150行先读Modbus coils/registers，再以新HttpClient调用 **GET `/api/simulator/special-actions/state`**；接口由`VirtualPlc/Program.cs:47`提供，`TestSpecialActions.cs:26`在`_gate`内构造状态。锁存在只是候选等待点，不能断言当次发生锁竞争。
- `RuntimeHttp ended status=200 responseStarted=False`是异常路径中响应尚未开始的日志字段，**不是HTTP成功或复位通过**。重复异常堆栈不计两次故障。
- PLC日志没有该GET请求的开始/结束关联记录；没有内部HTTP四元组、连接/发送/服务受理/取锁/返回/读取分段或ETL。外部1429ms不能全部归给内部GET。job002成功仅保留其完整恢复验收事实，不证明job001机制消失。

因此HTTP异常独立保留。当前心跳空闲观察不调用reset/check、不会复现该恢复HTTP路径，不冒充已补足HTTP证据；待心跳采样提供机制后仍须核对适用性。若HTTP继续阻断必要恢复验证，应只补同冻结环境的一次定向初始核验请求时间线，另行评估，不重复整个配方或自动重试。

## 当前最小方案：补证，暂不能确定业务补丁

有证据支持的动作是一次独立默认Socket完成调度的有界采样；**当前不能确定最小业务补丁，不进入bug-fix**。

| 新证据成立时 | 最小修复候选与可能文件 | 必要验证 |
|---|---|---|
| TCP送达及时，目标线程/完成续体迟迟不能运行 | 仅修实证受影响端的完成/调度路径；候选Host Program.cs、LatestProtocolPlcDevice.cs、ModbusTcpClient.cs；或PLC Program.cs/ModbusTcpServer.cs。最终文件及修改形状由线程/栈证据确定，不预置专用线程/通用调度框架 | 同条件机制前后对照；真实超期仍锁动作，无未知动作重发；受影响的一条代表性主流程 |
| 系统TCP送达本身迟，或VM整个进程无运行机会 | 先限定传输/运行环境原因；不以增加应用线程冒充修复 | 相同端点隔离、负载及系统事件对照，记录可控主机条件；环境处置不写成已做代码修复 |
| PLC处理、引擎锁或有效echo接受迟 | 只修已测阻塞点；候选VirtualPlc/ModbusTcpServer.cs、VirtualPlcEngine.cs、TestSpecialActions.cs（HTTP须独立证明） | 处理/锁等待下降及有效echo接受时间，不仅响应确认 |
| 未复现、事件丢失或无法唯一关联 | 无补丁；记录未确定与缺失字段，停止本轮观察 | 不无限重跑、不放宽期限、不把inline通过计成根因关闭 |

若补充日志须给线程/Socket完成关联，优先最少内部诊断字段及原有有界窗口；不得改公开业务字段来猜机制。涉及共享接口时，**先同步对应spec、contracts、plan、tasks及消费者，再改代码**；当前仅补证脚本，没有共享接口修改。

## 可直接执行的一次采样

[采样脚本](observe-default-20260927.ps1)及[独立启动器](sampling-launcher-20260927.ps1)已准备，冻结清单见sampling-inputs-20260927.json。两个脚本AST无错误，`-ReviewOnly`校验冻结输入通过；**未做管理员端到端执行，不能称脚本实跑验收通过**。

唯一需要用户完成的外部动作：在本机**已提升权限、具备系统性能采样权限的PowerShell**执行一次：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File 'E:\dzk\gaode-1\.specify\bugs\t065-communication-delay\observe-default-20260927.ps1'
```

无需切换/恢复桌面worker、登录触发器或手动跑配方；不要求修改全局采样策略。管理员令牌只是脚本前置，实际WPR成功仍须核验；拒绝时保留错误即结束。

脚本先核对冻结摘要、现有WPR状态、ETW会话、26161/26162/26163端口及profile。只有自己的WPR start成功后才启动设备。原MSDTC等他人会话不停止。独立Test Q06配置只用于加载，不发送运行、reset、运动或人工确认；真实建库沿既有StorePrep `--no-build`，独立Host/PLC及其实际所属算法worker由原启动脚本管理。

两端inline=0只传入独立启动器及子进程；1秒I/O、3秒心跳、实际Test50ms轮询、原GC及Test HighBeforeReadiness保持。“默认调度”指默认Socket完成方式，**不意味着把既有Test High优先级改成Normal**。启动准备也计入观察总窗；119秒留停止余量，最多120秒目标。首次超期检测后留5秒后续窗（总预算剩余不足则截短），不重启不重试。实际时长须读result.json；OS调度/采样开销可能影响停止及时性，超出目标须如实标为观察预算偏差，不能伪报120秒。

结果：

- 缺陷目录 `sampling-<UTC>/`：result.json、tcp-scheduling.etl、WPR启动/停止/前后会话、TCP端点/PID快照、首故障检测及独立启动器日志。
- 独立运行数据 `E:\dzk\gaode-1\artifacts\recipe-execution-008\t065-default-<UTC>\`：真实station01.test.db、process.json、双端日志。路径同时写入result.json，避免依赖手抄时间戳。
- 结束只清理本脚本启动且仍存活的启动器进程树，停止自己成功启动的WPR，不取消他人会话。强制退出可能缺最后未刷日志，须检查窗口dropped/suppressed/overwritten及清理结果，不能声称全量记录；应用故障触发的持久窗口是主关联依据。
- 返回原始ETL并不自动判机制。后续用WPA/可用离线解析器检查TCP、CSwitch/ReadyThread、时间基准和事件丢失；若本地没有解析器，不安装或转发数据冒充完成，保持未关联状态。脚本NoFailureObserved状态仅说明窗口未检测到故障，须确认process.json/连接与心跳实际工作后才可写有界未复现。

## 修复前后对照与逐级关闭条件

冻结前后Host/PLC目录摘要、实际加载位置、配置/fixture/预算版本、运行模式、dotnet runtime、优先级、inline、CPU/VM与背景负载；只改变经证据支持的补丁，其余原期限/基线一致。不同构建必须分别登记，不能将r15程序写成r21。跨两次实验使用不同空数据目录/独立端点，每次有界且保持相同最小负载。

对照表应列：异常事务/连接、Host门锁/连接/写/读、PLC接收/处理/写、TCP送达、线程Ready→Running/续体、有效echo/安全锁停、取消实际执行、ETL丢失及观察时长。修复后无故障是必要结果之一，但间歇故障一次无复现不能单独证明机制消除；必须有对应机制段的变化或可控、来源明确的定向调度验证。不能为重复制造故障改变PLC含义或把历史缺失日志补造。

| 门槛 | 独立关闭条件 |
|---|---|
| **003 T065** | 双端有界UTC/单调、事务/连接、有效echo和调度证据可关联；实际延迟机制及最小修正有定向前后对照；独立连接、1秒I/O/3秒互锁保持，真实超期锁动作且无盲重发；明示旧3516ms窗口缺失。未确定机制、只Test inline通过或仅一次正常运行不得关闭。原任务措辞指调度修正；若新证据推翻调度机制，先评估并按授权对齐原条件，不能自行重定义完成。 |
| **008 T055** | T065先闭环，核对原明确共享依赖与当前构建适用范围；保留已有Q01/API/页面/SQLite/媒体/取盘/Final及F1/F2/F4证据。补丁影响哪些环节就做最少正式代表复验，未变范围限定复用；不重新开发或全量重跑已验收能力。 |
| **008 T070** | T055原全条件成立后，将T058/T059/T062/T066/T067/T069、SC/C/F差异、构建/输入/证据及生产限制逐项对账，更新最终索引/矩阵/收口结论；质量清单与历史失败保持真实。文档汇总不能代运行；当前不勾任务。 |

真实设备及现场标定仍按原B表：真实点位/高度、取放目标阶段可靠采样、布局/容量、特殊机构生产映射、相机SDK/算法精度与现场节拍。这些不是本轮通信Test补证的前置，不编造参数，也不因Test闭环宣称生产验收。

## 下一步Spec Kit提示词

用户执行上述一次采样后，可直接输入：

```text
$speckit-bug-assess slug=t065-communication-delay
继续评估，不覆盖assessment.md或20260927补充报告，不新增功能规格/重复任务。
读取supplemental-assessment-20260927.md、evidence-index-20260927.md及最新sampling-*/result.json所指双端日志/数据库与ETL。
先核验采样覆盖、事件丢失、实际时长和清理，关联端点/事务、系统TCP、CSwitch/ReadyThread、应用完成及有效echo。
只分析此次结果；未复现/未关联如实未确定，不自动再次采样。HTTP r22独立分析，不以心跳代证。
仅在证据充分时给出最小具体补丁/文件及必要前后验证，否则列准确缺证；新增带日期补充评估和索引，不改业务代码或勾选。
```

只有后续报告已确定补丁时使用：

```text
$speckit-bug-fix slug=t065-communication-delay
先读取原assessment.md及全部带日期补充评估，以最新实证结论为准。
仅执行已证实机制对应的最小补丁；证据不足时停止补丁，不放宽1秒I/O/3秒互锁，不自动重发。
修改共享接口前先同步对应spec/contracts/plan/tasks与实际消费者；不新增重复任务，不改历史失败/原型/来源文档。
保留独立修复记录与同基线前后对照，按改动影响做最少003 T065、008 T055/T070必要验证，已通过能力限定复用。
```

随后`$speckit-bug-test slug=t065-communication-delay`须明确消费最新补充评估和修复记录，逐项验证上述门槛；不得因原评估路径约定忽略补充报告或自动覆写旧文件。
