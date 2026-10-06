# 014实施消费者与唯一责任核查

2026-10-05，根E:/dzk/gaode-1。F01仅补18标签，31新任务和原012正文/依赖/勾选未变，关闭收据见artifacts/014-special-part-rotation/f01-closure.json。清单保持只读，按用户明确授权实施，DUI02/03仍未确认。

当前集成基线与完整工程引用、类型/消息/schema/物理槽/mask/API直接消费者位置见implementation-baseline.json；恢复副本仅在recovery按原相对路径保存，不用于覆盖其他会话修改。

| 文件职责 | 唯一任务与首批义务 |
| --- | --- |
| Application Recipes共同类型/校验/serializer/digest/planner/深冻 | 014 T002；新增nullable/new schema先交稳定类型，历史读取不补值 |
| Domain/Ports语义scope/Stage/实际safe/角反馈 | 014 T003；枚举/switch及消息准确签名 |
| 融合/workload/预算/Worker/Python消费者 | 014 T004；StageId贯通，保持原总期限 |
| Matcher/F/public handoff/frozen readers | 014 T006；完整盘冻结及旧版本读取 |
| Protocol/现PLC/Pump关联/VirtualPlc | 014 T007，通信组件T008；UInt128/低高键/Owned，不增加采集源 |
| 检测/排序/编排/事件持久化 | 014 T009/T010/T011，各文件依tasks唯一归属；原槽实际返回、真pick保存/safe |
| Composition/真实运行查询通知投影 | 014 T012；Host Program入口仅012 T040 |
| 测试/扫描 | 014 T013/T014/T015/T016按真实职责；契约/构造迁移与登记先于首次相关工程build |
| SQLite/decoder/provider/catalog | 012 T038；当前4完整保存、旧2/3准确读，不另目录 |
| StorePrep来源准备 | 012 T039；014 T005来源文件先交，同库结果后验收 |
| RecipeEndpoints两文件和Host Program | 012 T040唯一编辑，消费共同配置/校验，不在HTTP复制工艺 |
| recipe-authoring.js与a.html | 012 T041同任务Core/导航后批，DUI未批不得实现待审导航 |
| runtime.js及直接显示回归 | 012 T046；核frontend/tests/us1/runtime-007.test.ts真实投影消费者，若受影响归同任务迁移 |

前端仅API/通知。新prepare source输入不是第二运行目录；现测试/软件配置仅适用具名虚拟范围，不授生产。正式地址/3D格位关联/安全/固定取料角/容差/真机应用缺项按DEP局部限制，无设备保存继续。

删除候选为count截断/首槽克隆/猜物理号、当前任意OK目标、special免搬、scope代盘、旧Stage推断、失效旁路和错误测试；替代接通后才删，保HistoricalHandlingEvidence/SpecialExitCompleted历史reader、占据/真实pick保存/UnknownHeld/期限取消/epoch/并发/冻结与正确负例。实际删除与执行证据后续记录，不把盘点标为已删除或软件通过。

仓库检测无Git，未初始化；无Docker/ESLint/Prettier/Terraform/Helm配置，frontend private不发布包，未引入不适用ignore文件。013文档/基准输入/轮询策略保持。

首批引用补充：PlcRuntimeOptions.cs归014:T007；SortingMappingContracts.cs归014:T003。旋转机械依据为空只限制特殊动作，普通及离线保存不依赖它。实际页面文件是frontend/src/pages/a.html（012:T041）；frontend/src/a.html不是当前构建输入，保持不改。

公开显示DTO直接消费者RunSnapshot.cs唯一归014:T012；追加StageId/CellId/实际safe语义，字段只从冻结及实际提交投影，009 shapes按此登记，不传raw。

014:T013追加直接组件消费者：backend/tests/Gaode.Contracts.Tests/Support/SemanticStageFixture.cs。仅迁移TransferToRotation/SafeReached语义组件替身，原ordinary/historical保护保留，不作为正式TCP或联合证据。

014:T008追加直接消费者ActionHandshakeTests.Flip.cs，只承接原有效翻面/放回组件的抓手零选择断言；不改变013轮询专项、期限或其他历史期望。

- 014:T016 additional direct consumer `scripts/011-owned-host.py`: the actual existing Host lifecycle supervisor must accept the explicitly declared current-workspace 014 joint evidence root, retain exact Host binary ownership and reject simultaneous 013 measurement context. No lifecycle or business success shortcut.

014:T007选择有效性监听生命周期核对：只有已在本连接代次真实确认的选择才在B维护；此前B使用原有字段，G选择期间仍为该值的唯一生产者。有限预建集合及全部原周期/期限保持，不重开013性能研究。T008验证初次选择前无ActiveId读，之后真实失效仍使下一闭环重建。

014:T016 owns shared `scripts/011-owned-host.py` lifecycle admission for exact current-workspace 012 private UI evidence roots; 012:T045 consumes it for actual normal stop/reread via `scripts/014-012-normal-read.py`, without rerunning saves or touching production databases. No business action endpoint or shutdown-success substitute.

014:T012 owns `backend/src/Gaode.Infrastructure/Configuration/RotationExecutionConfigurationFactory.cs`: finite infrastructure conversion of the already validated device mechanical basis into the existing semantic RotationExecutionConfiguration. Host composition only registers that semantic value; it must not read device configuration members. L05 A04 is retained; no checker exception or additional configuration provider is introduced.

014:T016 owns `scripts/014-startup-diagnostic.py` and the diagnostic-only mode of `scripts/014-012-joint-page.mjs`. Startup comparisons use the same actual Host/VirtualPlc, dual connections, 1000ms I/O and 3000ms heartbeat; each new comparison must declare its hypothesis and one principal variable. Normal page/API/notifications remain. No run or PLC command is submitted. Existing bounded application timing is reused only in the 014 diagnosis directory, not as 013 performance or acceptance evidence. Resource samples are read-only and other processes are never controlled. Startup diagnostic completion cannot produce joint Final evidence. The prior two-group cap was superseded by the current continuation authorization; its completed evidence remains historical.

First startup comparison reproduced failure and exposed an observer SSE/lifecycle defect. T016 additionally owns `scripts/014-page-proxy.mjs` and `scripts/tests/014-page-proxy.test.mjs`: stream the actual SignalR response without waiting for EOF, abort its upstream on client closure, retain read-only business access. The second completed comparison kept the full collector and changed this verified proxy implementation. The process-attribution metadata layout was corrected separately; the first group's missing process attribution remains missing and only its system counters are comparable. Neither change is a claim that communication root cause is established.

014:T007 additionally owns the finite arbiter group classification in ModbusTcpClient.cs; T008 owns Devices/RotationFeedbackArbiterTests.cs. G/R first-state already use the existing fast policy, but their normal feedback must join the existing B/F/U/T peer class; P's existing two-slot fairness and all original periods/deadlines stay unchanged. This does not explain a startup failure with G/R unused. The real TCP component queues B/P/G/R simultaneously and rejects the old omission, without changing 013 test expectations. T016 also retains the first test failure before ending the observer and bounds extra CDP operations; no failed run is labeled Final.

Continuation metering is internal to T007 ModbusTcpClient.cs: deadline start/frequency/last check, submit resumption and outcome accompany the existing bounded in-memory per-request trace; no per-request disk output or new sampler. T008 owns the necessary additional deadline metering assertion in RotationFeedbackArbiterTests.cs. T016 explicitly records runtime configuration and one diagnostic hypothesis. Existing runtime policy is read-only for this investigation; any native-threadpool contrast changes a declared process configuration, never the PLC timeout/polling or business path. Current task completion still requires actual representative and same-run evidence.

014:T016 additionally owns atomic publication of observer ready.json and the existing RecipeExecution010RunHarness raw stdout/stderr drain. Ready becomes visible only after its complete document is closed; no reader retry or fabricated readiness. Raw evidence uses bounded StreamWriter buffering, immediate flush at first explicit PLC failure and final disposal flush; no business/protection/log row is removed. This is verifier overhead and ownership repair, not an established public-communication root cause.


### 2026-10-05 action feedback recovery (014 T007/T008)
Special continuation-09 reached F/freeze but missed the 500ms real XY Moving observation: the last start-write await resumed after the simulator had already progressed, and X sampling was enabled only then. Keep the same acquisition pump and periods. Arm the existing X group before start writes and latch only actual Moving samples from the same epoch whose field sampling starts after that axis actual acknowledged response completion tick. Retain one action-scoped bounded watch (one entry per commanded axis), clear it on exit, and still require current Arrived, actual coordinates, original deadline/cancellation and safety. An unsent command, pre-dispatch/stale/foreign-epoch sample or arrival alone cannot establish Moving. No additional polling source or successful feedback synthesis. Contracts tests must reject those cases; representative remains actual TCP/Worker/SQLite/page evidence.


014 T012/T013 projection recovery: sorting-evidence/1 SortingAssignmentInTransit carries PickCompletionEvidence, not DeviceActionEvidence. Parse full action evidence only for its declared completed kind (SortingAssignmentOccupied or RotationReached); committed pick remains Executing, never physical place/safe/whole-tray completion. Keep notification/query live and add the exact persisted pick-shape regression, valid digest/run/plan association and completion rejection.

014 T012/T013: for current frozen3, a declared physical completion kind without its correlated placed/safe or rotation-angle proof remains Unconfirmed; StageEventType.Completed alone must not display a physical action completed. Historical frozen versions retain their original read interpretation. Existing API maps Executing to phase state Running; the new regression must use that established name, not invent Executing.

014 T012 projection: pick/occupied envelopes retain their original scope and transfer purpose by consuming the committed stage-action/1 Started event with the same operation, run/plan and connection epoch. This is an actual operation association, not classification/step inference or a current recipe lookup.


014 T011/T013 scoped event recovery: per-unit calls already carry the frozen DetectionExecutionScope and original detection IdempotencyKey. Namespace wrapper stage-event keys by that scope (unit and slot), including intent/start/completion/retry/pause boundaries. Whole-tray and ordinary scope-null keys remain unchanged. Distinct units cannot conflict at SQLite, and repeated same-unit/same-key/different-content still conflicts. Do not randomize event keys, weaken store idempotency, swallow Conflict or add retry. Existing ThreeStageWorkflowExecutorTests belongs to T013; two-unit component and actual shared representative must cover this.


014 T007/T008 completion recovery: ordinary continuation-02 actual P transaction2 completed queue/I/O within78ms, then Submit resumed about1056ms after arbiter completion; original absolute check rejected1135ms. Remove the scheduler explicit asynchronous completion handoff; publish every normal/cancelled/expired completion outside the arbiter lock, preserving one wire drain and the original final absolute/cancellation check. No extra retry, deadline start change or new executor. Bounded completion timestamp means immediately before signaling, separate from return after any inline consumer. Audit direct transport consumers (only existing PlcSignalAccessor); cancellation/reentry and original deadline tests plus both representatives are required.

014 T008/T016 evidence recovery: an actual TCP probe records its response row after forwarding the real response; prompt inline clients can finish first. Await the finite existing watchdog for the complete real probe rows (still6/2), never reduce counts or invent rows. Creation failure must be persisted before cleanup; a secondary owned shutdown failure is separately persisted and cannot mask the original exception. Disposal failures remain failures.


2026-10-06 T007/T008 first-fault preservation: a subsequent reconciliation rejection must retain its distinct refusal meaning but must not replace the first actual wire/deadline cause in the business failure latch. Keep the first unusable-connection cause internally, cleared only by the existing explicit reset; current 1000ms origin, epoch, cancellation, no replay and raw exchange journal remain unchanged. Required regression: actual dropped response followed by refused reads retains the original cause; ordinary and special acceptance remain separate. No new business recovery path or diagnostics-as-success.

First-cause classification must also retain the original caller token: elapsed I/O time alone cannot convert an earlier caller cancellation into a deadline cause. This is diagnostic cause preservation only; the original linked token and absolute Check behavior are unchanged.
