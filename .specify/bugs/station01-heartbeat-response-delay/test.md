# Bug Verification: 第一工位心跳应答延迟

- **Slug**: station01-heartbeat-response-delay（用户显式指定）
- **Tested**: 2026-09-24
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

最终修复构建在隔离Test/VirtualPlc中完成三次连续、真正Session 2 WPF/WebView2原型按钮单击：每次仅一次正式POST返回202，3D/算法后继续完成下料、解锁及`FinalUnloadCompletion`；期间未再发生3秒心跳报警。独立`PauseHeartbeat`对照证明Host仍在3031ms后安全锁停。**原现场3516ms发生的具体Host交易/调度分段没有历史记录，不能把新实例定位结果倒填原故障，也不能以三次通过宣称间歇故障彻底消除或真机通过，因此按技能判partial。**

## Checks Performed

| Check | Command / Action | Result | Notes |
|-------|------------------|--------|-------|
| 测试对象身份 | 各`process.json`、`interactive-desktop.json`、构建SHA与前端资源SHA | pass | 最终Host `42ACEFA5…C78C8`、VirtualPlc `C23A1F75…165B6`、WPF exe `A72420FB…893C`、页面runtime `8FBB2CB4…0082`；Test公共/模拟1.2.0、预算1.1.0，独立端口/SQLite/回环，Session 2真实窗口句柄。源码未在本验收阶段修改。 |
| 原WPF入口连续复测1 | `heartbeat-accept-01-20260924/interactive-session2/` | pass | 鼠标点击原型已有按钮，页面网络POST一次/202、run `80360eea-bbe0-4efb-8e28-2653092904ea`；正式查询及SQLite到`FinalUnloadCompletion`，页面“完成”，无通信报警；运行内最长已存PC echo间隔1374.1ms。 |
| 原WPF入口连续复测2 | `heartbeat-accept-02-20260924/interactive-session2/` | pass | POST一次/202、run `120d6cfb-123f-46c3-ba06-1159187146aa`；FinalUnloadCompletion，最长1101.2ms；失败未被丢弃，此轮无失败。 |
| 原WPF入口连续复测3 | `heartbeat-accept-03-20260924/interactive-session2/` | pass | POST一次/202、run `4862d428-d403-41a4-8f43-c791f56b4b0b`；FinalUnloadCompletion，最长1373.2ms；失败未被丢弃，此轮无失败。 |
| 202不假完成/页面与事实一致 | 三包`before-click.png`、`after-202.png`、`final-page.png`、`webview2-page-evidence.json`、`page-api-device-facts.json`、SQLite | pass | 202后页面显示“已受理”而非完成；终态页面显示“完成”及`Test/Simulated · 受控客户端确认取盘；非真实人工`。每包实际`UnloadPreparation Completed`、`WholeTrayCompleted`、`ObservedUnlocked`、`FinalUnloadCompleted`阶段事件及解锁/最终ID可查。 |
| 真实超期心跳安全对照 | 隔离`heartbeat-safety-20260924/safety-interruption.json`，只向VirtualPlc发`PauseHeartbeat` | pass with boundary | 注入前Host connected/safe；最后边沿后3031ms `HeartbeatStoppedChanging`，Host锁新动作、connected/safetyClear均false、`PlcHeartbeatLost`、无启动/运动写。Host先清PC ready，PLC自身未置Alarm_Bits=64；本项不证明PLC报警位。未复位设备或自动恢复。 |
| 独立相关合同回归 | `dotnet test ... --no-build --no-restore --filter VirtualPlcLatestProtocolTests\|ModbusDiagnosticsTests\|HeartbeatInterlockTests` | pass | 验收阶段另存`evidence/verification-heartbeat-contracts.trx`，12/12；修复前6/7间歇失败TRX保留。 |
| 原3516ms分段复核 | 原现场后补PLC变化/审计、Host/worker日志及SQLite在线备份 | inconclusive for segment | PLC收到24.940→28.456的echo间隔3516.2ms且请求持续翻转，27.952置Alarm_Bits=64，Host 28.439记录SafetyInterlockLost；故障时的Host心跳读/写/线程池时间戳未保存，不能确定3516ms中各段比例。 |
| 真机/生产 | 不连接真实设备 | not-run | 本次授权仅Test/VirtualPlc，不能外推真实PLC、物理节拍、算法精度。 |

## Output Excerpts

- 修复前新隔离实例：Host `transaction=5 responseHeaderMs=3222.3, poolThreads=3, pendingWork=5`；VirtualPlc同事务`processingMs=0.1`。这是新实例启动前故障，支持“PLC响应后到Host读取完成”出现长等待与线程池排队；无法排除该区间内全部TCP/OS调度贡献，不证明原3D窗口同段。
- 最终正常三包：每包`POST=202`、`postCount=1`、`wholeTaskState=FinalUnloadCompletion`、`communicationTimedOut=false`、PLC变化游标`gap=false`。Host/PLC原始日志未见本运行的`HeartbeatStoppedChanging`、`SafetyInterlockLost`或VirtualPlc echo timeout。
- 超期对照：Host `lastEdgeAgeMs=3031, thresholdMs=3000`，随后`origin=heartbeat, reason=HeartbeatStoppedChanging`；正式状态`diagnosticCode=PlcHeartbeatLost`、`connectionEpoch=2`、`connected=false/safetyClear=false`。
- 退出后关联索引及全部SHA见[证据索引](./evidence-index.md)，每次实际页面包含点击前、受理后和终态截图、脱敏网络POST/202、正式GET、PLC审计/变化、Host/VirtualPlc/worker日志、SQLite。用户原目录中的失败与修复前6/7测试均未覆盖。

## Residual Risks

- 原现场没有当时的Host逐交易/线程池采样，**3516ms只能直接定位为PLC收到两次PC echo之间的空档**；新隔离失败把相似机制缩小至PLC完成响应后到Host读完成，默认最低线程2且3线程/待执行5项。页面/算法/SQLite/日志对线程池竞争的具体贡献仍无单项因果证据；不能说它们已分别被排除。
- 3次成功是有限回归，不是长稳或所有负载证明；运行中PC echo最大空档仍有约1.37秒，需保留异常窗口诊断以观察后续真实复发。源码中“读到新边沿先更新`edgeAt`再检查停顿”的独立风险未由本轮定向测出，不等同于本次延迟根因，后续若出现需单独评估。
- 页面登录后点击前截图显示`Unknown`、页面在POST后才显示“已受理”；脚本已核对后端就绪并实得202，但没有把点击前的`Unknown`伪写为页面“Ready”。三次验收仅证明原入口能发起并完成本Test流程。
- 超期对照只证明Host侧3秒互锁，不证明PLC自身Alarm_Bits=64；原现场PLC报警事实仍由原PLC变化快照独立证明。Test自动模拟取盘并非真实人工，WPF通过不等于真机或整站验收。

## Recommendation

保留本缺陷为**局部验收通过、原现场根因待确证**：调度容量修正与异常窗口日志可进入后续隔离Test使用，但不关闭原间歇故障或宣称真机通过。若新样本再出现长空档，直接用本次UTC/transaction/单调分段和证据索引回到`$speckit-bug-assess slug=station01-heartbeat-response-delay`核对实际段，不自动重试或放宽3秒门限；003 T065、007 T029及其他历史任务勾选不在bug-test阶段改写。
