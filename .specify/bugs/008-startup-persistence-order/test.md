# Bug Verification: 持久化初始化先于设备启动

- **Slug**: 008-startup-persistence-order（沿当前assess/fix上下文）
- **Tested**: 2026-09-27
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

确定的启动顺序问题已由真实启动日志验证：StartupPersistence Completed在PLC starting之前。修正后整体人工正式WPF同run至Final、退出清理及全部适用读回通过。原症状是间歇通信超期，单条通过不证明其全局根因已解决，因此保留partial，不据此关闭所有通信诊断。

## Checks Performed

| Check | Command / Action | Result | Notes |
| --- | --- | --- | --- |
| 必要回归 | dotnet test Gaode.Integration.Tests.csproj --no-restore --artifacts-path r12/build --filter FullyQualifiedName~HostLifecycleTests | pass | 7/7；真实旧运行恢复、未知不重放及停止行为 |
| 启动次序 | r12 job000 logs/host.out.log | pass | 开始/完成初始化，之后PLC启动、worker Ready、HTTP监听 |
| 正式重现条件 | 原ASSEMBLY-A-E-MANUAL fixture、原PLC/default server GC/1秒期限，新Host | pass | run53762f43-c488-4153-a8b8-82eb2d629903；本次未因通信超期中断 |
| 正式场景读回 | scene-acceptance-audit.json | pass | 12/12；人工页面与握手、全部3面、同run物理投影、刷新重开 |
| 预算/调用 | audit-budget-calls-r2.py | pass | 12/12；Height/F各1、Detection9、E1，无翻后3D，各阶段实际提交在期限内 |
| 取放审计 | audit-sorting-commits.py | pass | 3/3；本路线OK无需搬运，未造取放成功 |
| 退出清理 | job-000.result.json/cleanup.json | pass | exit0、cleanupVerified=true |
| 额外短预检 | 未另跑 | skipped | 已有同构建真实正式路线覆盖启动、页面及结束，避免重复低价值运行 |

## Output Excerpts

`已通过! - 失败: 0，通过: 7，已跳过: 0，总计: 7`。真实worker受理Height1/FDecode1/Detection9/EDecode1，各调用有结果和输入释放，正式run最终保存及页面FinalUnloadCompletion一致。

## Residual Risks

- 原间歇通信超期因果仍未完全确认；后续正式代表继续按原期限，不增加盲重试或把未知当完成。
- 外层控制器首次缺selected-route-budgets-r2.json暂停，原错误保留。补清单从实际冻结Application和同fixture独立预计算，不读取该run事实；随后对同一已退出SQLite/worker协议审计通过，未重复业务。
- 证据仅Test/VirtualPlc，不宣称真机或算法精度。

## Recommendation

启动次序子交付可使用；保留通信诊断为未完全收口，继续原T054/T069及剩余正式验收。全局任务仍按完整条件判定。

## 后续代表：Q04启动超期再次出现

r12 job002 exit1/cleanupVerified=true，正式页面未启动，无业务run。Host心跳同连接53940 tx2：PLC于19:36:56.0944677Z发回，Host直到19:36:57.192502Z读完首部；原1秒期限在正文读取处触发。Host累计GC暂停33.7ms，PLC处理18.4948ms，完整7个诊断窗口解析无JSON截断。该证据不支持通信已修复；上文启动顺序子交付与整体人工Passed保留，通信诊断仍未收口。

r12 job001旋转Pending exit0/cleanupVerified=true，场景、预算调用及取放适用读回通过。原selected剩余未启动作业继续暂停。下一步仅独立r13 ReadyToRun编译比较以减少已观测JIT冷启动工作，源码/GC/协议/期限/fixture不改；不是部署、不宣称根因确定，也不以比较准备替代正式验收。

r13 job000 Q04、job001 Q05正式Passed，r13 job002 Q06启动心跳tx5读延迟再次Failed/cleanuptrue。PLC同端点56526于19:50:30.4639422Z发回，处理0.0537ms，Host直到19:50:31.8794103Z读完首部，原deadline取消正文；GC累计31.19ms。预编译不足以解除当前通信阻断，不认定根因解决。完整7个窗口解析无截断，无业务run或正式页面通过。

下一有限比较root r14-io-0927，原r13 DLL/PLC/GC/fixture/期限全部相同，仅指定该次自身Host进程DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1。已核实实际安装.NET10.0.12的PortableThreadPool.cs代码：Windows默认从IO poller派发线程池，开关改为内联回调；仅诊断候选，不据一次结果认定线程池根因。临时启动hook只匹配明确expectedTestRoot，Start-Process -Environment仅该Host，PLC/父进程/系统未设该变量。正常启动脚本完整副本保存，结束后撤回。原未启动job移held-jobs保留，新worker先就绪再旧退出，剩余正式代表仍暂停。

## r15默认调度失败与无构建干扰比较

r15 Q06 exit1/cleanupVerified=true，在启动门禁期间失败，尚无正式业务run。真实日志完整证明持久化完成→worker Ready→HTTP ApplicationStarted→PLC连接，启动次序子交付成立，但不足以解除全部通信阻断。Host心跳tx78同端点58574，PLC20:00:14.9550229Z响应、处理0.0244ms；Host20:00:16.4335785Z读完首部，GC累计34.747ms。原期限拒绝正文，7个窗口无截断。

r16-io-0927保持r15源码/DLL/原PLC/default server GC及全部期限，重新作一次明确自身Host内联回调比较。所有独立构建/测试已退出，临时request精确匹配单Q06 TestRoot，不修改父进程/PLC/系统设置。结果待读回，原失败及诊断干扰记录保持；启动顺序及通信根因结论分开。

## r16—r18实际验证增量

r16 Q06同rund4fc1789-b50b-487f-953c-9acbb14410eb正式Final、exit0/cleanup=true及适用场景/预算/取放审计Passed，配置为普通r15 Host/default server GC/原PLC/全部原期限，显式所属Host内联I/O。证明该Test代表完成，不证明默认模式/真机或全局根因。

r17 GROUP-A-E run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，XYZ匹配、tx9478 PLC读头延迟4.7秒、处理0.0133ms；Host单侧设置不足。正式exit1/cleanup=true，保留完整WPF/API/设备/SQLite及原失败。r18 job000算法Ready 5秒失败，PLC未连接/无run，不评价PLC比较；job001同冻结DLL加显式所属PLC设置，新run已进入真实Detection，结果尚待完整退出与审计。Test worker新增两条生命周期audit不改变算法；非预期Blocked采证按实际前端同run StateObserved=20退出，预期恢复仍继续。所有期限、未知不重放与保存门禁保持。

启动次序子交付经7项HostLifecycleTests和实际日志验证，通信根因仍未完全确诊；不能把diagnostic/preflight当正式Passed或将失败覆写。
