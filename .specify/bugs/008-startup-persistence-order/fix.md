# Bug Fix: 持久化初始化先于设备服务启动

- **Slug**: 008-startup-persistence-order
- **Applied**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied，正式复验进行中

## Changes

`Station01HostedService`将原StartAsync中的真实SQLite命令恢复和未完成运行恢复提取为InitializePersistenceAsync；保存同一个初始化Task，hosted StartAsync复用。Program在app.Run之前等待该Task，因而设备hosted services尚未启动。原查询、未知不重放、协调器及停止顺序保留。开始/完成记录StartupPersistence生命周期日志。

008 plan/tasks先登记T054/T069启动子交付。未改变外部API、PLC协议、1秒交换期限、配方、worker或算法，也未添加业务重试。临时EventPipe接线已撤回，启动脚本摘要回到19C2251C791D9EA03E75FFE0B9FAC654A2B5797B3F89759CC8C0A5A914E2A15D。

## Validation

独立输出`artifacts/recipe-execution-008/r12-0927/build`，locked restore后执行现有HostLifecycleTests，7通过、0失败、0跳过，TRX在同根tests/host-startup-lifecycle.trx。第一次no-restore构建缺该新目录assets，已通过locked restore解决，原build.log保留。

原r11 worker通过reloadWorkerRoot成功交班到r12 worker10396/Administrator/Session2；新冻结摘要见r12/build-freeze.json。正式ASSEMBLY-A-E-MANUAL job000使用新Host及原PLC、默认server GC，实际页面复验尚在执行。其结果和启动顺序日志将在bug-test中判定。

## Limits

源码已消除确定的初始化重叠，但不能据7项生命周期测试宣称间歇通信超期根因解决。原r9/r10失败及r11带跟踪预检均保持历史结论；无原任务勾选变更。

## r14比较Failed与r15启动顺序子交付

r14 Q06 runb590291d-f687-4ba0-8522-009955c535f6，exit1/cleanupVerified=true，运行期通信失败、页面未Final。失败期间与独立r15构建资源重叠，明确记录比较干扰，不能以此单包认定内联回调因果；不采用该开关为正常修复。临时hook快照保留，原脚本已撤回，摘要19C2251C791D9EA03E75FFE0B9FAC654A2B5797B3F89759CC8C0A5A914E2A15D；request移同包。

确认r13失败前PLC心跳已开始、worker与HTTP仍在初始化。按原T054/T069先更新plan/tasks/assessment，PlcConnectionHostedService改为BackgroundService等待ApplicationStarted再真实StartAsync设备；真实状态仍唯一准入，HTTP可达不代表设备Ready，原Stop顺序/释放及失败停Host语义保留。不改API/协议/期限，不增加动作重试。源码停服务实现保证先取消/等后台启动、最终释放设备。

r15独立locked restore/普通Debug构建，7项HostLifecycleTests全部通过；构建已退出。后继ready再旧退出，当前r15 worker沿同Administrator/Session2。采用普通构建/default server GC/default socket scheduling/原PLC和fixture，单条Q06正式复验，其他5条差异仍暂停。不将启动次序修正或测试Passed宣称通信根因解决；后续正式运行与构建/测试串行。
