# T065现场及版本核查补充评估

- Slug：t065-communication-delay（用户指定，继续已有缺陷）
- 日期：2026-09-27；本目录时间戳015331348Z为本轮开始UTC。
- Verdict：valid；Severity：high。
- 结论：本轮唯一补充观察确实启动双端、未检测超期，但系统ETL保存失败。**不足以确定业务补丁，T065/T055/T070不关闭。**
- 原assessment、补充评估、失败包、任务勾选保持；仅修采样工具和新增核查证据。

## 现场、请求及旧采样事实

1. 实际已授予固定任务`GaodeT065CommunicationDiagnostic`的Read/Execute权限；注册操作为固定pwsh→run-t065-diagnostic-task.ps1，Administrator交互令牌/HighestAvailable，触发器0，IgnoreNew。本轮通过Schedule.Service只读确认最初State3/Instances0/LastTaskResult0。**任务退出0不代表观察成功。**
2. 原Observe请求`2c19da537c2948b182a26bd2746333be`于01:43:56Z在Administrator/Session2执行，01:44:46Z返回；存在result，已消费，本轮没有重放。原Preflight `aa081bbf…`亦已完成。原请求及task runner在本目录归档，后续请求使用全新ID。
3. `sampling-20260927T011909797Z`为找不到dotnet的启动失败，原result/最终清理未齐，不能写通信未复现。其旧默认录制后续由已记录的用户确认恢复操作保存到`recovery-20260927T013620973Z/previous-recording.etl`，长度3429892096字节、stop exit0；identity.json估计开始01:19:12.2407Z，对应确认的01:19:11窗口，管理员wpr-after为未录制。**该ETL没有该次成功双端运行，不能作为根因关联证据。**
4. `sampling-20260927T012026523Z`只在前置检查发现既有录制75秒后拒绝，未启动第二次设备；原MSDTC及他人跟踪不停止。
5. 原014401观察的Host10636/PLC944/worker12156确实运行，process.json、Host通道57522/57523→26163、实际Ready可核。观察01:44:02.9544945→01:44:37.1423847Z（34.1878902秒）。原“FailureObserved”是**工具误判**：大小写不敏感的`HeartbeatTimeout`匹配Host启动行`heartbeatTimeoutMs=3000`，不是超期。保留原result，不回改Passed。
6. 原014401命名WPR保存exit=-2147417850，`0x80010106 / Cannot change thread mode after it is set`，没有输出ETL。原cleanup记录4个同身份仍存活；本轮新管理员只读检查`f1eb9917…`在01:53:55Z确认原10个身份已不存在、端口无监听、默认及命名WPR均未录制。原清理瞬间失败事实保留，当前已退出不倒填旧cleanup。

## 当前调用链、摘要及并发边界

本轮初始快照initial-hashes.json、修正前observer-before.ps1/task-runner-before.ps1/freeze-before.json与最终manifest分别记录版本；原37个冻结条目全部匹配。观察入口及清单从01:43/01:25版本到本轮接管前未出现新写入；专用任务Idle才改工具，执行期间不改活动脚本。不能由有限快照证明所有隐藏执行方永久不存在。

当前实际链：固定任务→`run-t065-diagnostic-task.ps1`→`observe-default-20260927.ps1`→`observation-freeze-20260927-reconciled.json`；入口将**内嵌**`private-launch.ps1`写入本次独立证据目录→现有`scripts/start-station01-virtual-loop.ps1`→StorePrep/Host/VirtualPlc及所属Python worker。

不使用sampling-inputs或sampling-launcher；其文件仅为另一套历史工具，未混用。内嵌启动器对子进程PATH设置dotnet、pwsh及Python312固定目录，本轮实际process.json显示dotnet10.0.401、Host/PLC及算法session启动，说明修正在**实际链**生效，而不是仅旧AST通过。新清单另存不覆盖observation-freeze原文件，并加入工具可执行文件摘要。

观察实际使用的入口SHA256=`DD88E51F4F8170ED9A61C79E3689E66E52D0CCE841B97B8CF92A9347DFD56F64`，清单SHA=`7519A6E5B109344753D2E4D5AC8732B167B0CAEAADFB36D0DC09E1C41F950558`，见new-observation-request.json/tool-verification.json。结束后入口另加已证实保存故障阻断，**最终入口摘要与执行版本不同**，以最终manifest为准，不能冒充此次实际运行版本。

## 本次仅工具修正

- 首次失败检测改为实际交换失败、心跳首次失败/有效echo超期或明确失败记录；必要验证证明3000ms配置行不再命中，真实交换失败及echo超期会命中。
- 内嵌启动器捕获准备失败写独立startup-failure.txt，观察器立即识别并收尾；process.json的startupFailure亦单独处理。
- 必须有真实Host/PLC PID存活、可靠connected状态才登记actualPairConfirmed；仅启动器或WPR存活不计双端观察。启动失败、未确认双端、未检测超期、发现失败待关联、清理未齐、ETL保存失败分别有状态。
- 对旧命名实例保存失败，尝试原本机曾成功的默认实例保存方式；本轮仍失败，故**排除“仅命名实例导致保存失败”的假定**。该尝试未改变业务期限、GC、优先级或公开接口。
- 新增wpr-save-blocker-20260927.json，当前故障WPR摘要不变时入口在启动跟踪前拒绝再次通信观察。不是自动重试，不执行cancel，不修改系统全局策略。

工具语法验证同时覆盖外层与实际内嵌启动器，冻结输入/三条检测验证通过；不计业务测试。启动失败标记路径虽有源码检查，本轮未主动注入失败，不宣称已完成失败注入实测。

## 本轮唯一补充观察与最终管理员核验

在确认专用任务Idle、管理员默认/命名WPR未录制、端口无监听、旧身份全退、原请求已消费后，提交全新Observe `754b6885aa1842efb024b4165a552237`。管理员Session2实际执行，未启动完整配方或页面、reset/运动/人工确认；独立数据真实StorePrep建库，Host/PLC/算法实际参与，无跳过核心组件。

- 证据根：`artifacts/communication-delay/t065-communication-delay/observe-20260927T015550635Z`。
- 数据根：`artifacts/recipe-execution-008/t065-observation-observe-20260927T015550635Z`。
- 实际观察01:55:51.8892001→01:57:48.6993227Z，**116.8101226秒**（含准备，少于120秒）；firstFailure=null，actualPairConfirmed=true。
- 原1秒I/O、3秒心跳、50ms Test轮询、原GC/Test HighBeforeReadiness、两端所属inline=0保持；冻结Host90BDA14E…/PLC66FDD5F6…、合法Q06 fixture仅加载，不执行配方。
- 最终outcome=`SamplingSaveFailedRequiresReview`，WPR再次0x80010106，输出ETL不存在。因此只可写“本次实际有界观察未检测到超期，系统采样结果不完整”，不能写T065通过或根因消失。
- 双端日志派生new-application-readback.json有2个完整PLC窗口、1个截断JSON；完整SocketFailure窗口发生在结束清理附近，不作为运行中首次超期。droppedWindows=0仅表示完整可解析窗口内部计数，不能覆盖截断记录、整个日志或系统事件丢失。
- 原cleanup瞬间仍存在部分进程，本轮再以全新只读Inspect请求`eed0b1ff9aa84005a20c1311c7e37dac`于01:58:56Z核验Administrator现场：默认与旧命名标签均未录制、ETW无WPR collector、全部11个原身份已不存在、无诊断监听，原端点残余TIME_WAIT不是存活设备。没有发stop/cancel或杀历史PID。
- 管理员TEMP里未找到本次时间窗后的WPR命名临时ETL候选；此限定搜索不是证明机器所有位置永远无原始文件。本轮没有可关联TCP/调度ETL可分析。
- task-final.json记录最终Instances0/State3/LastTaskResult0；当前请求是已完成的只读Inspect，不是待重放Observe。

## 事实、假设及唯一下一步

已有r15/r17应用分段与本轮旧样本613.4208ms慢成功仍可复用，但没有TCP送达/ReadyThread/CSwitch、完成续体、有效echo全链。旧613ms小于1秒，不能写成I/O超期。r22 HTTP初始核验超期独立保留，本轮没有reset操作，不能认定与心跳同因。**当前没有证据支持最小业务补丁。**

当前唯一补证前置已从“缺管理员能力”变为**WPR保存工具故障**，不需要用户重复授予任务权限。微软性能诊断文档针对同一0x80010106建议WPT build19650或更高版本；当前工具显示10.0.17763。来源：[Microsoft WPR Start and Stop Commands](https://devblogs.microsoft.com/performance-diagnostics/wpr-start-and-stop-commands/)。这是有来源的工具处置方向，不能直接当成已证实通信根因，也不能保证更换后必成功。

下一步仅解决系统采样工具：优先在独立目录使用来源可核验的支持版本WPR/WPT，不替换System32文件、不改系统全局配置；沿既有专用任务权限，在无冲突的管理员上下文做**不启动设备的最短保存验证**，先证明TCP/调度profile可启用、ETL可保存及离线读取、结束无遗留。未完成该前置，不再提交Observe。不得为了验证保存再次盲跑业务。

本轮不安装/下载工具、不再追加通信观察。入口已阻断当前已知失败工具；准确现状复核命令为`& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File 'E:\dzk\gaode-1\.specify\bugs\t065-communication-delay\observe-default-20260927.ps1' -ReviewOnly`（只验证冻结输入，不解决保存错误）。不是要求用户再次运行旧Observe。

## 下一步可直接执行提示词

```text
$speckit-bug-assess slug=t065-communication-delay
读取reconcile-20260927T015331348Z/assessment-supplement.md、evidence-index.md、wpr-save-blocker-20260927.json及当前任务/工具链。
只解决WPR 17763 stop的0x80010106工具问题；不得重放754b6885/2c19da53 Observe，不再跑通信或配方。
沿已授予专用管理员任务权限，先只读核验当前录制/身份/请求。优先独立目录中来源可核验的支持版WPT，禁止替换System32或改全局配置。
工具准备好后仅做一次无设备的最短TCP/调度采样保存验证，确认ETL能离线读、无事件/时间基准缺口与无遗留；失败如实记录且停止，不连续重试。
输出版本/摘要/真实调用链、保存及解析结果、后续通信定位的具体条件；保留原assessment和历史，新增时间戳评估；不改业务源码、任务勾选或宣称T065/T055/T070完成。
```

取得实际故障TCP/线程调度关联后才评估具体补丁；如涉及共享接口，仍先同步对应spec/contracts/plan/tasks与消费者。
