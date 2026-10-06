# 009 验证指南（设计，未执行）

**当前指南范围（2026-10-02 10:54）**：通信代码边界最小收敛，依spec与VG BM00。原§1—10及原§11的动态/整机/PD/CS/BA/SU/M/MC方案均保留为历史与后续义务，不是本轮可执行前置；下面当前入口单独标识BoundaryMinimum。

用户已授权本轮本地构建及最小边界/语义验证。历史示例不代表本轮执行；本轮不启动Host/PLC/Worker/页面/完整动态，不操作生产。当前BoundaryMinimum入口已实现；本轮结论仍须当前运行及人工闭合证据，旧动态段落的待实现/验收状态不据此改写。

## 1. 已有入口核对

| 入口 | 实际参数/能力 | 局限 |
| --- | --- | --- |
| `scripts/verify.ps1` | 唯一参数`-WaitSeconds`，0—600，默认600；调用workflow/verify_entry.py和runner.py | 会restore/build/test；当前固定Rules/Contracts/Integration三套。已拒绝零测试/Skip/TRX失败，但无必需门禁ID核验和新通信套件；不是当前已具备的009总验收。 |
| `scripts/start-station01-virtual-loop.ps1` | OperatorToken、TestPageAdministratorToken、TestPageReadOnlyToken、TestRoot、ApiBase、PlcApiBase、PlcPort、HostDll、PlcDll、PageOrigin、FixtureManifest；开关HostSocketInlineCompletions、PlcSocketInlineCompletions、SkipDesktop、WindowsNativeThreadPool、DiagnosticEarlyReady | 启动独立PLC/Host并核对Worker归属；会调用StorePrep建实际库。FixtureManifest必须现存绝对路径/purpose=Test且摘要匹配；fixture模式TestRoot限`artifacts/recipe-execution-008`，无ProtocolVariant参数。 |
| `scripts/simulate-station01-load.ps1` | 已有PrepareOnly、StartRun、OutputDirectory、ApiBase、FixtureManifest等参数 | 使用FixtureManifest时只允许PrepareOnly，与StartRun互斥；准备输入不等于业务完成。后续应从正式API发起运行。 |
| `scripts/verify-008-backend-route.ps1` | `-Case`只接受Q04—Q22、必需`-EvidenceRoot`、可选`-Fault`；独立进程后端API运行 | 根fixtures路径写死，不能指定usr-e-1.0.2目录，也不支持Q01/Q02。最后仅写finalOutcome，退出0不保证Final或009证据完整。不能用它校验本设计四面新版配方而不核对路径。 |
| `scripts/verify-q01-q02-test-page.ps1` | `-Cases`（string[]，也拆逗号）、`-EvidenceRoot`、Interactive/PreflightOnly、HostDll/PlcDll、线程开关和AuthorizationMode | 名字虽为Q01/Q02，源码还支持Q03、适用Q04—Q22、组/整体/E/人工/旋转/恢复等；Q04—Q22实际切usr-e-1.0.2并拒绝退出范围序列。需要WPF交互会话；非交互会创建同用户InteractiveToken任务；指定冻结DLL/线程选项需当前交互worker。PreflightOnly不是运行通过。 |
| `backend/tools/Gaode.StorePrep` | 当前仅`<allowedTestRoot> <newTestRoot>`两绝对路径；拒绝非空旧根 | 只准备新Test库。009原地受控升级入口**待实现**，不能把当前工具说成已支持。 |

`backend/tests/.../Support/VirtualLoopTestRig.cs`的VirtualPlc engine/server在测试进程内，Host用WebApplicationFactory，Worker另进程；这种真实TCP集成有价值，但不能抵扣独立Host/PLC进程要求。独立进程依据启动脚本实际PID/可执行文件/配置记录。

## 2. 已有命令示例（仅说明当前接口）

以下命令在后续验证阶段运行，均会产生运行或文件副作用；本轮未执行。每次EvidenceRoot必须使用新的允许目录，不能覆盖旧证据。示例不会自动形成009通过声明。

```powershell
pwsh -NoProfile -File E:/dzk/gaode-1/scripts/verify.ps1 -WaitSeconds 600

pwsh -NoProfile -File E:/dzk/gaode-1/scripts/verify-008-backend-route.ps1 `
  -Case Q04 `
  -EvidenceRoot E:/dzk/gaode-1/artifacts/recipe-execution-008/009-existing-backend-q04

pwsh -NoProfile -File E:/dzk/gaode-1/scripts/verify-q01-q02-test-page.ps1 `
  -Cases Q01,Q02 `
  -EvidenceRoot E:/dzk/gaode-1/artifacts/recipe-execution-008/009-existing-page-baseline
```

不通过auto-dev/step循环启动验证，避免自动进入tasks/analyze/implement。不把拟新增参数传给现有脚本，也不把当前无009门禁的verify退出0写成隔离验收通过。

## 3. 原完整流程代表集合（转出，非当前前置）

路径基准为`specs/008-recipe-driven-inspection/fixtures/`。运行前读取选定manifest和实际catalog，登记版本、摘要、对象、面/相机顺序、点位/轴依据和预算；不可仅按Q名推断顺序。下表按现有源码/fixture选定；需要的公共步骤在完整路线中复用，不为每个变体重跑所有配方×失败组合。

| Route | 实际fixture | 代表差异与观察终点 |
| --- | --- | --- |
| L01 | `fixture.json`：R008-Q01/1.1.1-test，AB/P01 | 初始化→夹紧→A运行配置区域准备→公共3D/F→唯一配方绑定及B适用容量/显示应用→AB两拍照位→真实算法/保存→下料→WholeTray→解锁→人工确认→Final；普通OK无取放。B按已接受10000ms总窗及已有较早截止完成全部必要保存；A不当绑定完成。新budget版本/fixture引用仍待AL08对齐。 |
| L02 | `fixture-q02.json`：R008-Q02/1.1.1-test，CD/P01/P03 | CD批次和非连续物理槽、相同坐标分量仍完整读写；槽身份不由Sequence代替。 |
| L03 | `usr-e-1.0.2/fixture-q03.json`、q04、q05、q06 | 两面AB→AB、AB→CD、CD→AB、CD→CD适用顺序；真实翻面/当前面、原初始高度、翻转放回后统一3D姿态复查、F不重绑。四条是原一/二面要求，不扩四面排列。 |
| L04 | `usr-e-1.0.2/fixture-q09.json`、`fixture-q18.json` | 实际catalog分别AB→AB→CD→AB和CD→AB→CD→CD；覆盖四面3CD＋1AB与新增配置及少数组不在最后的代表。不是按旧recipe-cases旧序列运行。 |
| L05 | `disposition-p03-1.1.4/fixture-q02-pending-p03.json` | 一个实际需处置对象的源槽/源目标→可靠取料→SQLite InTransit→放料→Occupied；用于V-PICK及四类变体共用主流程。Pending不能由Host硬写，仍经实际Worker/业务有限结果。 |
| L06 | `usr-e-1.0.2/fixture-group-a-e.json` | 成组各成员身份与目标、当前需后续面的实体逐个翻、E实采/worker/保存；组规则不误搬正常成员。差异若正常样本没有问题成员，用现有mixed组fixture最少补证。 |
| L07 | `usr-e-1.0.2/fixture-assembly-a-e.json`及必要的`fixture-assembly-a-e-no-code.json` | 整体共享姿态/搬运不按部位重复；E无结果保内部身份/问题后继续，但机械/保存不可省。 |
| L08 | `usr-e-1.0.2/fixture-q04-manual.json` | 人工占用→当前确认/安全清除→采用命令面，明确人工来源，不冒充实测。 |
| L09 | `usr-e-1.0.2/fixture-rot-part-ok.json`、`fixture-rot-assembly-ok.json` | 既有Test HTTP特殊旋转、单件/整体共享取放、特殊出口不普通重搬；上位机不编造角度/生产寄存器。共用能力无变化可复用本基线证据。 |
| L10 | `usr-e-1.0.2/fixture-recovery-3d.json` | 故障旧轮保留证据、双端复位、初始状态、显式新run完整重新开始；不是重发旧命令。 |

此集合用于受影响义务覆盖。实施时逐路线明确哪些公共能力复用、哪些差异必须实际跑，不把所有故障乘以全部路线。退出范围四面配方只保留历史读取/拒绝启动规则，不新增实跑门槛。

## 4. 原完整009验证入口设计（转出，保留历史）

规划`scripts/verify-009-protocol-isolation.ps1`为有限编排/采证入口，复用现有verify和独立进程启动/准备能力，不另建执行平台。目标参数契约为Mode=Baseline/Gates/Mutation、Variant=M01—M05（仅Mutation）、FixtureManifest、FreezeManifest、HostDll、PlcDll、EvidenceRoot；具体参数实现后须重新核对。本段没有可当作现成命令运行的示例。

1. **前置**：本轮DESIGN-OPEN-01设计已同步关闭；运行前仍须AL01—AL08共享文档实际对齐、一次性实现/迁移完成，新budget schema1.1及合法实例版本/全部引用/完整冻结生效；不能对旧缺项配置默认补10000。受控创建schema2新Test库；旧库按模型§6.1持锁/备份/单事务/完整核验开放，manifest在同库表内。确认固定SDK/解析器、真实Worker和媒体/manifest；缺必需组件未通过。
2. **Baseline**：运行V-SEM/V-WIRE/V-BOUND/V-LEDGER及V-BIND全部BA01—BA07适用数据行；verify必须执行JS/Python/PS内容/分类检查及负正例逐行结果。按代表路线用独立Host/PLC/Worker取得数据库/媒体/API事实；执行F01—F07及V-DIAG/V-HISTORY，F05/F06提交边界与升级验证不减。已发现Skip须有承接后取消，不可忽略。
3. **Freeze**：初次补齐新期限并通过基线后，记录完整文件/构建/配方/预算/媒体及业务断言摘要；显式冻结10000ms、预算schema/实例版本、起终点/原后段起点值、保存/后台取消语义、BA断言和必需ID。冻结范围VG V05；不把修复前无界等待当基线。oracle独立编写/复核，MC01期间不能随共同错误映射改预期。
4. **Mutation**：从同一新基线生成Test隔离构建，只改VG允许通信文件。M01—M04各跑L05真实取放、同一业务断言全集（含V-BIND及其真实TCP/保存代表）和受改信号路线；不改预算/测试时钟/必要保存或过滤BA数据行。M01仍覆盖心跳、区域、检测/复位、翻面、报警分散读取；不以L05单成功推全部映射。
5. **内部步骤**：M05真实ACK清零读回成功/缺失各一次；语义边界/保存节点/期限不变。此Test读回不批准未确认生产握手。
6. **负控制**：两端共享同一错误映射但独立oracle不变必须失败；修改冻结业务文件必须失败；必需门禁缺失/过滤/Skip也必须失败。
7. **结束**：停止本次拥有的进程，保留所有成功/失败包；不以残留进程、历史TRX或内存日志签发通过。

阶段/动作超时使用当前冻结预算；Test运行时线程选项若采用，记录现有明确开关并在基线和变体保持一致，不变更系统环境、不用改I/O/心跳时间换取通过。

## 5. 必需查询和证据

同run查询正式`GET /api/v1/station01/runs/{runId}`和`.../{runId}/evidence`；新raw引用使用待实现`GET /api/v1/station01/diagnostics/communication/{evidenceId}`。实际结果至少满足：

- 命令派发前意图已提交，取料有效后InTransit已提交才有任何放料字段/命令；F05/F06放料命令及相关目标写入为0。
- 绑定预算/窗口按影响矩阵§5.5从本次真实冻结与事实读取：三入口同源，意图回执先于t0，设备应用、raw、绑定及适用handoff在同一T内及时有效；设备确认/单一RecipePlanBound行不当最终Bound。独立API保存本次意图/绑定事实但不重建已有handoff。
- BA03心跳正常、I/O均及时成功仍按总期限结束；BA05取消准入后新派发该请求写为0。BA04真实commit后扣回执时SQLite有行但无当前续接授权，不能报告成未提交；BA06原后段起点/活动deadline刷新数0。具体记录与固定行见VG V09。
- F05-A/F06-A：实际SQLite提交前故障、回滚确认及写任务终止后，核对相应事务行无提交；F05-B/F06-B：实际commit已成，回执扣至旧窗口失效，必须查到真实行却仍零放料；C暂不可核查时不推断有/无行，恢复只读核对也不复活旧动作。分别保存故障位置、commit/回执/原deadline时间线、关联、Held和设备审计，不能只用fake callback或提交前异常。
- F06已观察取料却raw持久未确认时，业务可写则核对真实UnknownHeld最小失败事件；同库不可写时核对重启按已提交预留/意图保持占用，新增事实/日志可能缺失须如实报告。无有效引用不等于无数据库记录，也不能记为未取料。
- 采集文件真实存在、摘要/对象/面匹配，Worker实际进程读取规定输入并返回关联结果，SQLite和API能回查，媒体不因等待算法被提前释放。
- WholeTray、ObservedUnlocked、人工取盘、Final分别有提交记录；202、动作完成或解锁单独不能冒充Final。
- 成功、未知码、超时均有可解引用的实际原始批次；缺响应真实为空；旧合成raw无新实际包、旧历史不被补写。
- 4/4变体、冻结差异0、业务断言100%、独立逐点预期覆盖100%、基本五类及额外负例均按规则失败、合法正例通过；任一必需门禁缺失不出Passed。

包中至少包含freeze/diff、实际二进制/PID/配置来源、oracle、TCP与PLC本机审计、业务断言/TRX/门禁账本、SQLite读回、媒体清单、持久日志、诊断引用。V-MAIN/V-MUT是Test/Virtual软件验收，不是实机、精度、生产节拍或新页面验收。

旧库升级的有限验证只在维护副本中按U0/U1/U2/UX执行：DDL/迁移历史/manifest更新前后及commit后回执前中断，确认实际结构/精确迁移集合/同StoreId与旧payload摘要、媒体引用一致；完整旧态可在复核后再升级、完整新态不重复DDL、混合态一律拒Host。全部入口仍待实现；本指南没有可用于当前工具的旧库升级参数或声称本轮已跑过。

## 6. 当前仍缺的运行能力

语义端口、纯协议定义/访问器、持久通信证据与历史reader、逐字段API/通知、测试迁移、脚本内容检查、必需ID账本、回执故障缝、原子受控升级及009独立进程入口均待实现。新配方应用预算/三入口总窗/后台取消/BA注入也待实现；其需求和设计已经明确，不交实施阶段重新选择。OPEN-009-07生产预算与真机校准/目标窗口只限制对应生产路线。所有运行验证未执行；本阶段停止。

## 7. 配方应用期限的验证准备（待实现，不是现成命令）

现`VirtualPlcLatestProtocolTests.cs:441–442`的5秒只为组件看门狗；`SimulationModels.cs:36–52`没有绑定健康卡住故障，Program拒未知故障；不能向现有`-Fault`传虚构的RecipeApplyHold参数。009入口待增加受控Test注入和固定BA案例编排，仍只用Baseline/Gates/Mutation模式，不新建故障组合平台。

- 配置准备先按AL08更新001预算schema/模型/校验，再发布合法新budget实例并更新007 simulation、008选定fixture及启动脚本的ID/version引用。现启动脚本指001 schema且硬编码旧1.1.0，原manifest尚不具备新预算；表内配方代表保持，所需预算/manifest引用的首次更新明确发生在冻结前。不得改配方业务输入凑过10秒。
- BA01/07覆盖严格链、旧链和独立API各自来源/拒绝点；无效及Production未批准配置在任何绑定写前拒绝。BA02两容量用真实通信、SQLite和全部必要回执，独立API核验已存在handoff且不重建。
- BA03两容量分别在B已起算后维持真实前置不成立，单次I/O仍及时成功、心跳连续；选无更早截止的合法代表观察完整10000ms，另由BA06覆盖较早后段截止。不得用断线/暂停心跳/F复位失败来替代健康卡住。抓取超期关闭后的派发记录/真实TCP，心跳、观察和必要停止通道继续可用。
- BA04精确边界使用原受控Host时钟，另分别在真实RecipePlanBound、handoff的commit后扣回执到截止外，按BindingId/WriteId精确命中；核对实际行存在、有效回执失效、晚核查不续接。BA05在前置等待及已发I/O两个位置取消，区分失效前派发与失效后新增，后者必须0；不能只看调用Task取消。
- 同一BA04 late-handoff还要读GET并观察实际后段消费者：已存handoff不能自行重建可用Bound/Ready或触发Detection。按期完成的before代表不因稍晚调度倒判超时。Host回执观察以独立语义Audit关联原WriteId，不预填原事务的未来时间；缺观察证据未通过，不要求递归记录Audit自己的回执。
- 所有必要保存（包括t0前意图）按原有界规则及适用剩余窗校验；没有有效引用/回执不等于数据库无行。BA06核对严格原起点、旧handoff后起点及独立API实际存在/不存在的后段期限。收尾失败记录不延长动作资格。
- 验证进程看门狗应高于总窗和必要有界收尾，仅用于报告卡死；不得作为业务deadline，原5秒看门狗不能截断本次健康卡住代表。固定BA数据行及真实TCP/DB证据接同一V-LEDGER，缺项/未运行/Skip/陈旧证据均不能通过。

正常Test路线能否在10000ms及原后段约束内完成仍待运行证明；不足时报告失败，不自动延长。上述均为未来验证要求，本轮没有执行任何命令示例或故障注入。


## 8. 实施中的有限只读取证入口（2026-10-02）

以下脚本已创建并有组件/语法检查，尚未在本轮完成的独立进程证据包上通过；不据此关闭T050/T053/T054或009。输入必须为本轮受控Test进程已退出后的独立run目录，输出不存在且不会覆盖历史。不能拿历史报告替本轮证据。

- `scripts/verify-009-page-diagnostics.ps1 -CommunicationRoot <新通信故障包> -UnsafeRoot <新安全拒绝包> -OutputDirectory <不存在的新目录>`：分别核业务页面/GET与独立通信probe，任一退出非零失败。原API诊断脚本结束实际拥有进程后也调用通信probe。
- 原`verify-q01-q02-test-page.ps1`在持久主流程和授权场景均已接业务及通信两组只读核验；主流程通信读实际raw批次和独立oracle。其完整WPF/Worker运行尚待本轮核证，不能仅运行其中一个子脚本。
- `scripts/verify-009-saved-route.ps1`的Route模式需`-EvidenceRoot`、`-Sequence`与正`-Slots`（页面包加`-Page`）；FlipTimeout模式需明确新的后端证据根，不再默认读取历史目录；Scene模式保留原队列cleanupVerified前置；Summary模式需本次Q01/Q02两目录，可加本次参数变体目录。均先业务核验再通信核验，不产生物理控制。

上述范围保持原代表路线和原预算。新增组件正负例在`scripts/tests/009-diagnostic-business.test.cjs`、`test_009_semantic_evidence.py`及`scripts/communication/*test*`；组件通过只证明检查函数/拒绝能力，实际进程、TCP、SQLite/媒体/API、PD/SU/BA固定行和最终账本仍分别必需。


## 9. 实施进展：Test存储及保存故障入口（2026-10-02）

本节更新前文设计时的现状说明；不将未运行能力写成验收通过。

- `start-station01-virtual-loop.ps1`和本地verify可显式使用进程级`GAODE_VERIFY_TEST_PARENT`。独立启动还须Test FixtureManifest且TestRoot为该父目录下不存在的新目录；父目录限当前用户Temp内绝对非链接路径。合法及越界/相对/既有/链接/非fixture的7行正式路径函数组件在platform251通过；真实Host/PLC外部根运行尚待执行。报告登记实际目录，不删或覆盖旧证据。
- 启动脚本新增可选`-TestPersistenceFaultCase`，固定F05-A/B/C、F06-A/B/C、BA04-late-bound/late-handoff，空禁用；仅将明确参数交正式Host的Test选项，禁用会清除继承值。Host生产/Real/FullSimulation/未知故障拒绝逻辑及真实事务接线已写入，尚待构建和运行验证。
- 编排从正式启动回执取得runId，在本次TestRoot原子发布`009-fault-arm.json`，内容为`caseId/runId/nonce`；故障仅命中该run一次。完成本案超期观察后发布同runId/nonce的`009-fault-release.json`。30秒是Test注入保持的有限外限，不是预算。记录`009-fault-events.jsonl`的命中、真实commit、锁、释放时间与事件/批次ID；A/B/C仍按VG查实际库及全部后继写，不以日志单独计通过。
- 正式源摘要改为纯协议模块`backend/src/Gaode.Plc.Protocol/Signals.cs`，旧ProtocolLatestMap文件不再是启动前置。与独立oracle的关系不变；源码摘要不是通信正确性证据。

009总编排入口及全部独立F05/F06/BA运行尚未闭合，T061未签核。这里没有产品通过或实机能力声明。


## 10. 实施中的009进程入口（2026-10-02，尚未完整验收）

`verify-009-protocol-isolation.ps1`现有Mode/Variant/FixtureManifest/FreezeManifest/HostDll/PlcDll/EvidenceRoot/Cases及WindowsNativeThreadPool参数。它调用verify-only和有限进程driver，不调用auto-dev。选Cases/FixtureManifest仅子集，不报告009通过；完整模式缺任何固定义务、消费者或同轮证据仍拒绝。`scripts/architecture/009-process-cases.json`为固定45条L/F/BA/SU义务，当前部分driver尚未实现，不能把清单存在当能力完成。

已写采证支持独立后台路线、F05/F06以及BA02/03/04部分接线；这些入口尚未实际运行核证，不能用于通过声明。采证器保存当前API、进程身份及文件路径，关闭自身进程后分别读取真实SQLite/媒体和独立协议oracle；不制造WPF页面结果。006页面仍需原入口独立证明。当前正式脚本全量扫描结果不覆盖本节刚新增文件，后续会重跑。

选定15份嵌套fixture只升级配置引用到预算/simulation 2.0.0，原字节与差异位于本轮configuration-before-009-selected271；保留业务配方/媒体输入。BA容量测试使用现行9/8 Test输入的版本化副本，保持运动步骤；副本不是Production配置、也不改变10000ms预算。BA03新增正式Test故障选择在意图真实提交后调用已有独立PLC保持接口，后台超期取消判据不变；新C#接线尚待构建运行。

实施增量（联合闭合进度 22）：独立入口已补 L08 人工确认、BA05 pending 取消及 SU 六行正式维护接线。SU 六行在 process289 使用正式 StorePrep 真实中断并核实际数据库后通过；L08/BA05 尚待运行，不能混用结果。script288 全仓及 42 案例通过。首次 L05 的 process292 暴露启动脚本旧 connected 字段，已改语义 connection，重跑待核证。BA05 inflight、L10 等未交付驱动仍明确拒绝，固定必需清单不因驱动缺失而删项。新 C# build283 已成功，fault284 的 8 项受控保存故障组件通过；这些均不代替独立过程和完整基线。

后续增量：BA05 inflight 的有限 Test 派发栅栏已在正式适配器/Host 接线，选 `TestPersistenceFaultCase=BA05-inflight`；仅 B 首次容量写真实响应之后等待原请求取消，后继写逐次仍核原资格，日志不当批准链。脚本以既定 Test 容量副本触发栅栏并调用原 Cancel API。build297 成功，binding299 的 7 项真实 TCP 组件（含原健康等待、两容量、两种取消）通过、0 Skip；独立进程仍待运行。process294 的 L05 实际 Final 和 wire 检查成立，但媒体读取器字段错误导致该轮失败；已改按真实媒体元数据、实际文件和持久 Worker 输入摘要交叉核对，process295 只是原证据只读重查，未覆盖旧失败或签发新运行通过。L10、F01—F04、BA06 等未闭合驱动和总账本仍是 T054 未完成项。

取消接线复核：process302实际暴露连续链只关联Host停止而未关联当前运行取消；现ControlLatch发出原运行取消令牌，严格/旧链和独立API活动运行接同一资格。原暂停、期限、保存及失败占用规则不变。build305、cancel306合同16项与script307全仓检查通过；process308实际独立pending/inflight两行通过，后台资格关闭、真实旧写保留且后继绑定写0。仅子集成功，完整T054/T061尚未通过。

独立保存验证增量（进度24）：process309 的 L05 实际通过；process312 的 BA03 两容量与 F05-A 实际通过，保持原预算。媒体读取使用实际绝对长路径及持久 Worker 输入摘要，不虚构 Media 表不存在的 hash 字段。F06-C 已接同库真实 Host 重启并保留原独立 PLC，以已提交预留/意图、前后原文、恢复 UnknownHeld、实际 API 和零新动作取证；此新增重启流程尚待运行，不声称全存储故障的新取料事实一定持久。完整固定账本仍拒绝缺失项。
## 11. 当前BoundaryMinimum验证方案（有限分支已实现；验收需新结果）

现有verify-009-protocol-isolation.ps1已增加Mode=BoundaryMinimum并接有限workflow/boundary_minimum.py。参数只复用EvidenceRoot，拒绝Variant/FixtureManifest/FreezeManifest/Cases等动态或过滤输入。旧Baseline/Gates/Mutation、原完整固定清单不改变。

1. 按当前已审阅清单事先固定全部必需项（含新增两项现有架构用例及当前必要保存语义对应方法），N18七负例先实际复现重分类漏检；准确方法/case/数据行在workflow/009-boundary-minimum-cases.json，校验必需文件/解析器和分类，无新未分类文件。
2. 以新runId/独立EvidenceRoot记录当前源码/清单摘要；实际构建backend/Gaode.slnx、VirtualPlc/VirtualPlc.csproj（Debug，SDK10.0.401），取得真实输出/依赖摘要。构建失败不继续签通过。
3. 用当前输出发现Rules固定架构类、Contracts固定接口类/四少量绑定方法；逐方法/参数行保存发现。实际执行同集合TRX，runner.read_trx及生产validate_required_ledger核正例/负例/0Skip/0缺失，不从发现生成必需列表。
4. 实际运行check-009-script-boundary.py --run-id --output的RepositoryAndCases模式，不用cases-only；42例及三语言正式源/双向分类完整。运行test_verify.py --ledger-selfcheck现有G01—07（组件fixture证明生产核证器，不是动态设备证据）。
5. 人工核公共语义端口、Host同实例DI/回调、IntegratedDetection及配方/步骤消费者、raw合法诊断职责、业务/通信断言归属。新增文件/本地helper必须注册，不能扩大白名单。
6. 执行账本/摘要/result只用Scope=BoundaryMinimum，缺文件、解析失败、过滤、Skip、旧报告或零发现拒绝。T061/T069需要全部当前证据齐备；完整动态场景一律NotRun/Transferred，不声称Passed。

当前已支持的有限命令（不代表运行已经通过）：

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
pwsh -NoProfile -File scripts/verify-009-protocol-isolation.ps1 -Mode BoundaryMinimum -EvidenceRoot E:/dzk/gaode-1/artifacts/recipe-execution-008/009-isolation/<new-run-id>
```

若本轮直接修改保存/取消/安全准入行为，增加对应少量测试并先固定ID；保存门禁必须用受控真实SQLite，不能用语义double证明commit。未改动不启动完整故障矩阵。任何新结果不能覆写旧证据。

已知旧F04-handshake：business-validation14项通过但wire reader对取消时真实部分MBAP响应报TruncatedMbap；保留process377与red378原失败。当前先核因果，既有reader行为缺陷不能当协议隔离通过或自动改期望。本轮结果只证明代码边界，不证明该完整动态场景已修复。
