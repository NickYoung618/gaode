# 心跳异常窗口取证（2026-09-24）

本增量仅增强既有 Host/VirtualPlc 诊断，不改变 PLC 点位、公开 API、3 秒安全门限、I/O 期限、运动时序或未知动作禁止重发规则。不是心跳故障已彻底修复的声明，也不修改 Spec Kit 任务完成状态。

## 生效与保存

- 正常结束当前测试后，下一次使用不带 `-SkipBuild` 的一键启动。已经运行的进程不会自动加载新 C# 代码，不为日志升级强行重启设备。
- 日志继续保存在该次 `artifacts/station01-007/manual-*/logs/host.out.log` 与 `plc.out.log`。搜索 `PLC diagnostic window:`，后面为 `plc-heartbeat-window/1` JSON。
- 三个来源分别为 `Host/heartbeat-loop`、`Host/heartbeat-transport`、`VirtualPlc/heartbeat`。记录包含进程 ID、记录器身份、UTC、单调时钟及频率；连接身份、端点和事务号用于跨进程配对，不能只凭会回绕的 transaction 编号关联。
- 每个来源只保留最近 256 条。慢交换、调度/应答延迟触发保存，普通窗口间隔至少 5 秒；首次锁停/交易失败/PLC 超时不受该普通限频限制。约 2 秒后若仍有新记录，再输出一个后续窗口。
- 内存快照和 JSON 序列化/日志输出分离；后台待输出队列最多 2 份，优先保留已经排队的关键故障。`overwrittenRecords`、`suppressedWindows`、`droppedWindows` 明示覆盖、限频和排队损失，不能声称无限期保留全部历史。
- 正常 Dispose/Stop 会等待已排队窗口输出。强制杀进程、断电或日志磁盘故障可能丢失尚未输出的数据；无后续活动时也可能没有后续窗口。日志窗口不是抓包或全线程执行跟踪。

## 分段定位

1. Host 心跳循环：上一周期到本周期的等待、读/写耗时、读写事务号、上次应答时间、读到的新旧位，以及**更新边沿计时之前**的旧边沿年龄。即使迟到后又读到变化，也保留停顿事实，不以更新后的计时掩盖历史空档。
2. Host Modbus：连接/锁等待、请求写完、响应头读取完成、响应体读取完成，均有 UTC/单调时间；成功和失败均保留在内存窗口，失败含原始异常及发生阶段。失败锁存时还附带当前尚未完成交易的阶段。
3. VirtualPlc：快速的正常心跳响应也留在窗口中，记录 TCP 两端、连接身份、事务号、请求读完与响应写完时间、请求/响应内容。扫描周期延迟和引擎锁等待另记。
4. 有效应答：记录实际请求位、收到的应答位、是否相符、上次有效应答时间及写入事务号。Modbus 写回执不等于有效 echo，更不等于物理动作完成。
5. 因果关联：Host 窗口附已知 runId/operationId/actionId、连接代次、配置及最近观察。当前没有活动动作时，最近一次动作明确标记 `LastSubmittedActionNotCurrentStage`，不冒充当前业务阶段；用 runId 关联已有启动诊断、worker 日志和 SQLite 事件。未建运行时明确 `NoRun`。
6. 资源迹象：心跳周期前后保存线程池线程数、待执行/已完成工作计数及累计 GC 暂停时间，避免只留下恢复之后的一个采样点；触发窗口另存最小线程数/可用配额、CPU 核数和各代 GC 次数。可用线程配额不是正在空闲的已创建线程数；单次队列积压不能单独证明线程池饥饿。跨机器 UTC 需要先核对时钟同步，不跨进程/机器盲比单调计时值。

业务轮询先发现 PLC 安全报警时也会强制保存窗口，不能只依赖 `HeartbeatStoppedChanging` 异常触发。

## 结论边界

- 同一连接/事务中，PLC 快速写出响应而 Host 很晚读取完成，可把长等待缩小到返回链路与 Host 恢复执行；仅靠应用层日志不能进一步排除 TCP/OS 调度或直接确认具体阻塞线程。需要时再对隔离复现采集运行时线程/调度跟踪，不能把推测写成根因。
- 两次有效应答的间隔、单次 Modbus 往返耗时、PLC 心跳翻转周期是不同量，均以日志单位为准。3 秒等于 3000 毫秒。
- 复现后保留同次 process.json、Host/PLC/worker 原始日志、SQLite 及媒体、页面截图和请求标识。不要复位、自动重发或覆盖失败目录来制造成功；当前 Test/VirtualPlc 证据不外推真机。

## 定向验证

`HeartbeatWindowDiagnosticsTests` 在独立回环端口验证慢成功的前序交易保存、异常不被普通限频吞掉、256 条容量与覆盖计数，以及 Modbus 接收写入但 echo 不匹配的区分。与既有 `ModbusDiagnosticsTests`、`HeartbeatInterlockTests`、`VirtualPlcLatestProtocolTests` 一同执行，不对正在运行的 PLC 注入故障。

本次最终组合验证为 **16/16 通过**，含业务安全报警先触发时仍保存 Host 两类窗口的定向验证。证据为 `artifacts/station01-007/heartbeat-log-window-20260924/tests/heartbeat-windows-final.trx`；构建输出使用同目录的隔离 `build/`，未覆盖正在运行的程序。

首次组合结果 `heartbeat-windows.trx` 为 15/16：新增测试错误地假定第二笔交易一定是首次慢交易，实际首笔连接也触发了慢记录。按真实事务序号核对关键故障窗口仍保留第二笔慢交易后修正测试断言，随后 `heartbeat-windows-r2.trx` 与最终构建测试均16/16；首次失败文件保留，不冒充全部首次通过。此次未执行新的实际页面/真机验收，也未关闭原始间歇故障。
