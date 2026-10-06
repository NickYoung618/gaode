# Bug Fix: T065 WPR保存工具阻断

- **Slug**: t065-communication-delay（用户显式指定）
- **Fixed**: 2026-09-27T02:24:00Z
- **Assessment**: ./assessment.md（未修改）；本轮依据为reconcile-20260927T015331348Z/assessment-supplement.md的“WPR保存工具故障处置”
- **Status**: partial（整个通信缺陷）；**本轮工具范围：applied**

## Summary

原System32 WPR 17763的0x80010106保存阻断已通过项目独立目录WPT 20348.3694实际保存验证解除。必要TCP/线程调度证据可以离线关联，正式入口已绑定验证证据；通信根因未确定，未修改业务源码、接口、期限、任务勾选或历史失败记录。

## Changes

| 文件 | 修改 | 原因 |
|---|---|---|
| tool-fix-20260927T020325729Z/portable/ | 官方WPT完整Onecore及Desktop组件管理映像解包 | 同版本WPR/控制DLL/xperf及解析插件，保留旧工具，不进行系统安装 |
| wpt-toolchain-20260927.json | 显式可执行文件、配套依赖与采样脚本摘要 | WPR控制全部20348.3694；统计用同版xperf；离线展开用显式系统TraceRpt/TDH |
| resolve-wpt-toolchain-20260927.ps1 | 清单校验与实际验证证据检查 | 清单/证明/ETL改变或证明缺失时拒绝；无旧WPR回退 |
| validate-wpt-tool-20260927.ps1、tcp-tool-probe.cs | 固定任务无设备短验证和回环探针 | 只检查TCP与调度采集、保存、解析，不模拟PLC或业务成功 |
| correlate-tool-probe-20260927.py | 关联探针端点、TCB、PID/TID与时间窗 | 恢复微软已公开的Thread v3身份前缀，并记录解析错误/时间限制 |
| run-t065-diagnostic-task.ps1 | 固定PrepareWpt/ValidateWpt/ParseWpt/InspectWpt动作；现有预检改用验证链 | 无任意命令参数；不改变任务权限、账户、全局策略或重放Observe |
| observe-default-20260927.ps1 | 显式验证链；唯一命名会话start/stop；成功保存后离线统计/展开 | 只收尾所属实例；解析失败独立报告，关联完成标志仍false |
| observation-freeze-20260927-reconciled.json | 增加实际消费的工具清单摘要 | 原1秒I/O、3秒心跳、50ms轮询、GC/优先级/inline及冻结程序均保持 |
| wpr-save-blocker-20260927.json | 保留原错误/旧摘要，增加验证证明与清单绑定 | 不是删除阻断文件或只换摘要绕过；未宣称通信已解决 |

## Tests Added or Updated

固定任务短录制与离线关联构成本轮必要验证；未建设通用诊断平台，未新增业务测试。原脚本、冻结清单与失败记录在tool-fix-20260927T020325729Z保存；原assessment保持只读。

## Local Verification

实际命令与每次返回码见[commands.json](task-25481d379a1c4c72a28459c8549df6b5/commands.json)。以下WPR/xperf路径均为`tool-fix-20260927T020325729Z/portable/Windows Kits/10/Windows Performance Toolkit/`的绝对路径；ETL绝对路径为`E:\dzk\gaode-1\.specify\bugs\t065-communication-delay\task-25481d379a1c4c72a28459c8549df6b5\tool-validation.etl`。

```powershell
& $wpr -start Network -filemode -instancename T065Tool-1b471653c8a94407852e8fd682777dc3
& $wpr -stop $etl -instancename T065Tool-1b471653c8a94407852e8fd682777dc3
& $xperf -i $etl -o $traceStats -a tracestats
& 'C:\Windows\System32\tracerpt.exe' $etl -of XML -o $eventsXml -summary $summary -y
python .specify/bugs/t065-communication-delay/correlate-tool-probe-20260927.py .specify/bugs/t065-communication-delay/task-e9d214ee9622494699c5a6cc93399446/events.xml .specify/bugs/t065-communication-delay/task-25481d379a1c4c72a28459c8549df6b5/probe.json .specify/bugs/t065-communication-delay/tool-fix-20260927T020325729Z/correlation.json
```

Start/stop均0；ETL 248512512字节，离线统计/TraceRpt均0，1023269事件。探针PID4528，线程3828/12000，127.0.0.1:62293↔62292，02:09:40.4542355→02:09:42.5861387Z，491520字节实际发送/接收；253条窗口内TCP端点事件，2条PID+端点映射、1228条同TCB事件，585条CSwitch、196条ReadyThread，4条线程生命周期身份映射覆盖两个线程。ETL时窗02:09:39.7418081→02:09:52.4028967Z；lost events/buffers均0。探针退出、会话未录制、无所属collector/监听。正式ReviewOnly及固定管理员Preflight成功，未执行Observe。

完整证明、摘要、管理员上下文与结果路径见[证据索引](tool-fix-20260927T020325729Z/evidence-index.md)、[验证证明](tool-fix-20260927T020325729Z/verification-proof.json)与[test.md](test.md)。

## Deviations from Assessment

官方方向是更换支持工具，而非保证版本更换即可成功。Onecore最小包不足以提供解析插件，因此补齐相同版本Desktop包。ADK带features的layout失败、普通账户MSI解包1601、普通账户TraceRpt WMI不可用均保留；沿已有Administrator固定任务解决，未新增权限或安装产品。

保存成功后最初xperf参数位置错误已纠正；全量dumper仍因其他进程.NET Event190 v9返回0x80070032，提供者过滤亦未解决。改用系统TraceRpt在管理员上下文离线展开同一ETL，没有重复录制。TraceRpt仍有111共99545、15005共8214个处理错误；Thread v3原始载荷按微软PerfView偏移0/4恢复4096个身份前缀，目标两个线程映射成立。其他事件不宣称完整解码；TCP/CSwitch/ReadyThread目标字段已实际读取。

XML时间后缀+07:59不正确，ETL头FILETIME和显示墙钟建立+08:00锚点；UTC校准不使用错误后缀，导出精度1µs。后续必须继续保留ETL、应用UTC/QPC锚点，不把墙钟近似直接当调度因果。系统TraceRpt/TDH嵌入签名检查NotSigned已如实记录，未宣称签名Valid；其System32来源、版本和摘要固定。WPT官方包和核心组件签名Valid。

## Follow-ups

只解除采样工具阻断。通信根因、最小业务补丁仍待一次有界系统/应用关联；r22初始HTTP核验超期单独保留，不认定与心跳同因。003 T065、008 T055/T070均未完成。

下一次可直接执行：

```text
$speckit-bug-assess slug=t065-communication-delay
继续既有通信缺陷，只做一次最长120秒默认调度通信定位，不跑完整配方、不改业务代码。
先读AGENTS/宪章、原assessment、reconcile补充、fix.md/test.md、tool-fix-20260927T020325729Z/evidence-index.md和verification-proof.json、当前任务请求/结果、正式入口及实际冻结清单。
先只读核验Administrator上下文无冲突WPR、专用任务空闲且当前Preflight已消费；不重放旧Observe，不运行cancel，不停止未知会话。工具和依赖摘要或证据变化必须先处理，不绕过绑定。
满足前置后沿已有固定任务授权提交全新Observe一次；复用冻结Host/VirtualPlc、合法Test输入、独立端点/数据目录，保留1秒I/O、3秒心跳、GC/轮询/优先级等原基线。首次超期保留必要后续窗口后结束；未复现或不能关联如实记录，不重跑。
关联双端日志、事务/连接/TCB、实际TCP到达、ReadyThread/CSwitch与PID/TID，保留ETL/UTC/QPC锚点；使用本轮显式工具链，处理已记录的Thread v3及时间后缀限制。r22 HTTP问题独立核查，不认定同因。
新增带时间戳补充评估和证据索引，原assessment/失败记录不覆盖；给出有依据的最小补丁或明确证据不足，以及T065→T055→T070各自关闭条件。修改共享接口前先同步spec/contracts/plan/tasks，不修改任务勾选或宣称验收完成。
```
