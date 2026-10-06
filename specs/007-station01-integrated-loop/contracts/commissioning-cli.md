# 007 简易联调入口合同

**状态**：PowerShell入口已写入仓库；进程及辅助API样本须按各次运行证据分别判定，006实际页面联调仍待交付。

## 命令和职责

| 规划入口 | 最小输入 | 正常输出 | 边界 |
| --- | --- | --- | --- |
| `scripts/start-station01-virtual-loop.ps1` | `GAODE_TEST_OPERATOR_TOKEN`、可选绝对TestRoot、Host地址、Test页面来源、`-SkipDesktop` | 准备空Test SQLite根并启动或明确复用VirtualPlc；启动Host及可用的006桌面；Host通过WorkerProcessSupervisor启动和管理唯一独立虚拟算法worker子进程，平台核对其PID与Host组件状态；登记端口及日志并启用自动模拟取盘监视 | 平台不另起第二个worker，不自动写业务成功，不隐式重置活动运行；缺必需组件显示NotReady |
| `scripts/simulate-station01-load.ps1` | 测试trayId/场景/合法占位、配置引用、`PrepareOnly`或显式`StartRun` | 冻结模拟上料清单；可仅准备供006前端启动，或经授权Host API辅助启动并输出runId/commandId | 不直写PLC完成位/SQLite，不预造F/计划/最终事实；辅助启动样本不得替代前端启动样本 |
| 启动脚本中的自动确认监视 | 启用标记、授权测试身份、由Host取得的当前runId与持久化revision | 已提交WholeTrayCompletion和ObservedUnlocked后，以稳定requestId向正式确认API提交Test模拟取盘，查询最终事实 | 客户端不在启动瞬间提交；若运行失败/受限不强行确认；不冒充真实人工或006页面 |

前端实际启动样本的runId从Host`/status`的currentRun或正式运行查询观察；不要通过直接读Host内存或数据库发现。003共享合同现已统一正式路由`POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`及请求体`requestId/expectedRevision/reason`，同run整盘/解锁引用由服务端取得并核验。合同同步不代表Host已正确记录模拟来源；T013须完成Test/Simulated ManualActor接线和实际验证。

006负责已有页面入口的实际启动请求、受控Test凭据传递及已有位置的完整状态展示；007负责Host侧仅在Test配置下允许指定页面来源访问API/通知、保持并验证后端授权及实际连通性。启动平台可先实现并报告组件就绪事实；006未交付时实际前端闭环保持Blocked/NotRun，不把辅助API样本记为页面启动。双方实际连通是T015前置，不阻塞不依赖页面的007后端与虚拟组件实现。

监视重复轮询或网络响应丢失时复用原requestId/原命令查询，不生成不同请求造成两次确认；409/401/403/503如实输出受限，不能显示Final完成。自动确认服务端仍重新校验授权、同run、WholeTrayCompletion、ObservedUnlocked及expectedRevision，原子保存ManualTrayRemovalConfirmed/FinalUnloadCompletion。ManualActor须记录Test受控客户端身份和版本；Host代码已改为Test来源，实际最终事务仍须以本次运行证据核对。

## 命令受理与证据

`StartComponents`仅代表进程已起或健康检查通过；`StartRun`的202只代表持久受理；脚本Watch结束也不能直接代表最终完成。成功判定需Host最终GET、实际SQLite/媒体核对与FinalSourceMatrix。记录是否通过006实际页面启动；PowerShell只作为辅助入口、自动模拟取盘客户端及证据收集工具。运行失败或部分组件未实现时将相应项标为Blocked/NotRun，不伪造passed。

VirtualPlc现无“上料”REST；仅借测试输入和Host正式启动形成模拟上料场景，由VirtualPlc按`PC_Start_Cmd`内生夹紧。相机仍是Host内采集适配，不列单独相机启动命令。单独Python worker命令仅供手动协议调试，不作为平台并列启动步骤；平台以Host子进程事实核对唯一worker。脚本实际入口见quickstart，辅助API启动不能替代006页面启动证据。

## 009 / AL05 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

沿现有verify.ps1→workflow/verify_entry.py→runner.py，不调用auto-dev。增加通信测试套件、固定必需清单与逐case/dataRow执行账本；C#/JS/Python/有限PowerShell正式扫描与同入口正负例均必需。缺清单、未发现、过滤、Skip、解析失败、旧报告或证据缺失非Passed；参数化方法名不能抵数据行。PD12、SU6、BA全部数据行固定登记，不从当次发现生成expected。

现入口实际范围：verify.ps1仅WaitSeconds；verify-008-backend-route仅Q04—Q22、EvidenceRoot/Fault，固定旧fixture目录、退出0不保证Final；verify-q01-q02-test-page虽名Q01/Q02支持扩展Cases并选usr-e，PreflightOnly不算运行；start-station01-virtual-loop支持FixtureManifest/HostDll/PlcDll及既有开关，没有ProtocolVariant。009新增verify-009-protocol-isolation.ps1的Baseline/Gates/Mutation入口及参数、健康绑定卡住故障待009 T038/T054实现；不得将拟增参数当现成能力。混合业务断言与wire探针按009迁移矩阵T50拆分，精确通信探针保留原协议断言，PS仅有限编排/采集，任何子步骤非零/缺报告传播失败。

### 009 / AL05 测试存储位置补充（2026-10-02）

完整verify可通过进程级`GAODE_VERIFY_TEST_PARENT`显式选择当前用户操作系统临时目录之下的绝对、非链接目录；只能在其下创建本轮唯一`{requestId}/verify-{iteration}/test-data`新目录。拒绝相对路径、临时目录以外、链接及已存在的运行目录，不复用数据库；不改变`GAODE_ENVIRONMENT=Test`和受控Test库规则。未指定时保持原仓内测试根。验证报告仍留在原仓内证据根，登记实际绝对测试根及purpose/runId；实际数据库快照与媒体摘要必须按各案例要求采集，外部测试根本身不是通过证据。此开关仅解决测试磁盘容量，不改变预算、源摘要、内容检查、证据准入、独立进程或生产入口。本次由Codex实施并复核上述边界，无客户批准声明；实现和正负例归009 T053，不运行auto-dev或重新初始化项目。


### 009 / AL05 独立进程Test根补充（2026-10-02）

009 T041/T054的实际进程证据需要容量足够的独立Test根。`start-station01-virtual-loop.ps1`在显式提供已验证Test FixtureManifest时，同样接受进程级`GAODE_VERIFY_TEST_PARENT`：必须为当前用户临时目录内绝对非链接父目录，TestRoot必须是其下全新且不存在的目录，拒绝相对、越界、任一链接祖先、既有目录及非fixture调用。父目录不是数据库；StorePrep仍真实建库、StoreAccessGuard仍互斥并核路径，Host仍受现有Test准入。未提供开关的现有仓内路径行为不变。

启动记录必须写实际TestRoot和允许父目录，009汇总引用实际SQLite/媒体/持久日志及PID，不以目录存在计通过；不变更预算、设备语义或页面。对应实现/组件边界验证及独立进程验证归009 T041/T054/T055，先完成本段接口对齐再修改脚本。本段由Codex执行和复核，不表示其他人员签批；不修改007历史任务勾选。


### 009 T040/T041 有限独立进程保存故障接线（2026-10-02）

按009 VG V07/V09，在真实Host独立进程中增加可选`Gaode:TestPersistenceFaultCase`，仅在`VirtualPlcIntegration`、Virtual PLC及全部Test配置下接受。固定值为F05-A/B/C、F06-A/B/C、BA04-late-bound/late-handoff；未知值或其他运行环境启动拒绝，不新增业务API。未配置时不安装任何拦截器。Test根中的`009-fault-arm.json`以caseId、runId和nonce选择本次真实运行；独立编排取得正式启动runId后写入，未命中不能当故障验证通过。唯一命中记录实际EventId/WriteId/EvidenceId、时刻、位置和commit事实到同根`009-fault-events.jsonl`。

A通过实际SQLite触发器拒绝相应插入，原Store负责事务回滚与结果；B仅在真实commit完成后扣住回执；C在同一实际边界另持SQLite独占锁暂阻核查。通信raw故障仍经TraceWriter实际job，取料业务事务仍由StageEventStore承担。BA04严格选择本run的RecipePlanBound或本次handoff，不拦其他保存。Test专用保持窗口最多30秒，或收到匹配nonce的`009-fault-release.json`结束；这个注入外限不是业务期限，保持期间原预算/保存期限照常失效，释放后不能复活动作。

C使用受控Test副本的DELETE日志模式取得真实排他锁；A/B不伪造数据库结果，记录存储不可用时仍仅尽力写诊断文件。009验收必须另查实际SQLite、当前回执、原截止、PLC全部后继写和保守占用；故障日志及进程存活不能独自证明验收。此处只完成必要接线接口对齐；代码及实际运行由009任务证据确认。实施/复核角色为Codex，不冒称客户批准，其他功能历史任务状态不变。

启动脚本显式参数`-TestPersistenceFaultCase`只接受上列8值（空为禁用），非Fixture调用拒绝；禁用时清除同名继承环境值，process.json登记实际选择。该参数不是生产能力或业务预算。

独立进程的Worker归属取证：优先用系统父进程查询；该查询被当前账户拒绝时，必须匹配本次Host持久RuntimeFlow中的Started与Ready、相同workerSessionId/实际PID，并核该Python进程仍存在且晚于本Host启动。明确标注Host实际启动记录归属、系统父进程查询不可用，不能只凭Ready状态推造PID。本次补齐脚本接线，实际进程样本仍待009验证。


009本地verify的组件主流程证据随同一受控Test根保存：runner为`GAODE_009_INTEGRATION_ROOT`设置本轮TestRoot下的`component-routes`，VirtualLoopTestRig仅接受该批准Test父根内绝对、非链接路径；不改变默认未设置时的旧路径。实际目录写入verification.json；快照、媒体和origin仍真实逐run保存，不以外移或目录存在代替执行/核证。大文件不重复复制到容量不足的仓盘，小报告和引用仍保留仓内。原子新run、防覆盖、摘要及同轮关联义务不变。此为009 T049/T053的Test证据位置接线，不是独立Host验收或产品数据格式变化。


### 009 T053 本地完整套件看门狗（2026-10-02）

实际integration238完整运行188项用了87分钟，原runner对每条命令1200秒的外部看门狗不足以执行既定全部路线。仅将Gaode.Integration.Tests整套命令外限设为10800秒（3小时），其他命令仍1200秒；独立于Test10000ms及所有业务期限，不能由失败用例修改预算。每测试15分钟的VSTest挂起看门狗大于现有最长多子场景方法的有限窗口，超限终止本轮测试进程并使报告非通过，不跳过或重试失败。使用本机dotnet test --help已核实的blame-hang-timeout、dump-type=none，保留实际TRX/序列/逐项日志；不生成大转储占用证据盘。外部超时仍返回非0，必需账本仍逐数据行核证。由Codex实施/复核，运行结果另留证；本段不改变007历史勾选，也不批准生产或整体通过。

故障arm/release控制文件由本轮编排先写同目录新临时文件、完成关闭，再以不覆盖的原子改名发布固定文件名；接收方不读取半写入JSON、不忽略解析错误。错误控制输入仍使本次验证失败。此为既定Test接线的发布规则，不影响业务数据库事务或期限。


### 009 T054 独立进程编排的当前接口（2026-10-02）

由Codex执行/复核，先对齐本段再实现。新增 `verify-009-protocol-isolation.ps1` 接受 Mode=Baseline/Gates/Mutation、Variant=M01—M05（仅Mutation）、FixtureManifest、FreezeManifest、HostDll、PlcDll、EvidenceRoot，以及有限 Cases 选择和 WindowsNativeThreadPool 开关。Cases只能选择009固定清单，不从发现结果生成期望；选择子集不代表完整基线。Gates调用已有verify-only入口，不调用auto-dev或改变阶段。独立路线复用现有启动/准备/API，实际Test根由既有GAODE_VERIFY_TEST_PARENT规则控制；报告留在仓内新证据目录。006页面仍由既有WPF入口单独验收，后端辅助启动不生成假页面结果。

新增有限run-009-process-case.ps1只采集既定场景及本次拥有的进程事实，业务断言和通信探针分别执行；控制文件完整关闭后原子发布。进程结束只处理本次记录且PID/实际可执行文件/创建时间仍一致者。所有派生报告逐run/source/build/manifest绑定，缺必需数据行、解析失败、故障未命中或任一子程序非零均非通过。固定位于scripts/architecture/009-process-cases.json的L01—L10及F/BA数据行列出实际fixture，不创建通用故障平台。

009选定代表fixture仅将原预算/simulation引用定向更新为已批准2.0.0，保留原配方catalog/媒体/Worker及输入摘要；原文件先保留只读证据。此为T031/T054配置闭合，不改历史结果或任务状态；Production仍未批准、Test10000ms不变。新入口的组件检查及独立运行以009实际证据为准，不将本段当通过。


009 T040/T054的BA03独立接线补充：在既有受控Test故障选项中新增唯一`BA03-healthy-wait`。只命中已arm的当前run的真实RecipePlanAndBindingIntent提交回执边界，在t0尚未登记时调用独立VirtualPlc已有RecipeApplicationPreconditionHold测试接口；必须取得实际accepted响应，否则本案例失败。该接口只阻止现有绑定前置，TCP/心跳不停止，不跳过F清理。原CriticalSave仍约束意图回执，没有给业务增加时间。主流程起算和全部保存仍由业务协调器承担。Test接口调用及意图真实commit/安装时刻保留故障日志；BA03的成功判据另由实际10秒窗口、无后继、数据库与通信观察证明。此接线不允许任意URL或生产故障注入；仅已批准VirtualPlcIntegration/Test配置。由Codex实施/复核，不声明运行通过。

009 T038/T040/T054 的 BA05 inflight 前置对齐（2026-10-02）：在既有 TestPersistenceFaultCase 固定选择中增加 BA05-inflight，只允许原 VirtualPlcIntegration/Virtual/Test 组合。正式通信适配器在本次 B 容量首写取得真实响应后、任何后继绑定写入前进入有限 Test 派发栅栏；不拦 A 阶段，不暂停心跳和观察，不延长单次 I/O 或业务总窗。栅栏只命中已 arm 的当前 RunId/nonce，记录 BindingId/ActionId、实际已完成首写及单调时刻；随后由原请求取消/截止关闭，不能由 release 文件恢复旧请求。编排取得该实际命中后调用原 Cancel API，核关闭后的后继派发为零和原写入事实保留。该测试钩子不进入业务端口，不把协议阶段交给 Application。实现/复核为 Codex，先完成本段文档对齐再修改共享接线；实际组件和独立运行证据仍由009任务补齐，不改007历史勾选、不声明已通过。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
