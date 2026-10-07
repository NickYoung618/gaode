# 前端全流程证据合同 s01-route-evidence/2.0

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](../business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

日期2026-09-24；宪章6.0.0。覆盖单位为Q完整序列及C业务差异，不再要求每个M两配方。
未来运行目录：artifacts/recipe-execution-008/<caseId>/<runId>/；当前未产出008运行包。索引见../evidence/index.md。

## 每个完整运行的最小证据

1. manifest：用例、前端配方及版本/目录摘要、冻结计划/点位/预算、程序与组件摘要、启动命令/PID/端口、时间和来源。虚拟环境标SoftwareLoopOnly及productionClaimAllowed=false。
2. 页面操作：实际加载与选用配方、正式启动控件、请求/受理、进度/结果和适用人工操作；关联requestId/commandId/runId。可使用页面自动化真实控件事件，不以内置函数调用、后台StartRun或预造handoff替代。
3. 配方一致性：所选引用、实际F返回与唯一绑定、冻结执行配置、最终保存四者一致。仅页面显示一个名字不能证明配方驱动。
4. 实际执行：对象/面/相机/轮次、步骤意图/派发/匹配反馈/复位，适用源目标、实体位置与旋转占用。每步有所有者；合法不搬运有依据；缺事实不能靠日志判完成。
5. 采集及worker：媒体摘要/可读性、真实参数、PID、单图及双输入调用/输入关联、耗时、结果或原始失败及输入释放；模拟结果不冒充真实算法精度。
6. 实际保存：从该run的SQLite和媒体核对快照、动作/结果事实、面/成员/组/整体结果、处置及Final提交。API查询与只读库核对可辅助采证，但启动和必要交互仍须实际页面。
7. 最小持久诊断：受理、阶段、设备交互、阻断/失败及处置可关联；原始异常保留，轮询重复受控；进程结束后仍可定位。
8. 结论：实际覆盖Q/C/F、对象完整序列、版本和各项Passed/Failed/Blocked/NotRun；局部通过不算整盘，受阻和未运行不计通过。

## 复用规则

- 一个合法完整run可覆盖多个对象的不同Q及多个C，逐项指向对象/步骤/提交事实；不能截取四面片段代替两面终态。
- 页面脚本、公共准备、尾段及通用核验方法可复用；每条SC-001选定适用验证路线仍有相应完整前端记录。
- C08至少一次同程序版本的新增/变化配方完整运行，证明实际参数/槽位/步骤变化，原快照未改写。
- F类别在适用用例验证最小集合，不与全部Q交叉。直接后端测试标AuxiliaryApi/Component，不抵扣Q前端通过。
- 普通/特殊、组/整体、人工/自动差异不能互相替代；相同未变的公共能力证据可引用其真实原范围。
- 新共享执行逻辑变更后，对此前Q证据按影响决定是否补跑；最终结论必须说明程序/配置兼容范围，不能拼接相互不兼容的历史包。

## 人工、物理与历史证据

适用人工换面/取盘/恢复分别记录页面操作者和实际物理/虚拟反馈来源，软件确认不证明硬件安全。
007自动Test客户端取盘仅是历史渠道事实；008不能将它填成前端人工确认。本轮只绑定已经交付的正式页面控件；006记录实际交互，客户原型基线只读，不新增或改变控件。控件及后端尚未完成时对应路线保持Blocked/NotRun，不能用调试界面或辅助API代替。
新增示教不在本期证据要求内。

保留007最新当前构建页面Blocked及旧构建成功包的原始事实；不覆盖、不改判、不抵扣008新定位/融合/多面/组/整体要求。
本轮文档检查只证明文档结构与对齐，不是运行、数据库、设备或端到端通过。

本次U01—U08业务确认不是本矩阵C编号的替代：必要代表验证包括组NG成员搬运而OK留原位、NG+Pending明细、E无有效码但保存问题并经复位继续、人工采用命令面且来源明确、初始不足不新启动/双端复位后完整新run并保留原故障、旋转真实Test请求与结果。生产参数延期不能被写成生产通过。

## USR-E当前验收与问题取证

当前宪章7.0.0替代旧全Q数量义务。一面两面保持，四面按实际配方/运动差异选3＋1两类代表，不以全部允许位置变体逐条实跑为门槛。每条选定路线仍须实际页面全链；历史批次只按原构建配置引用，退出Q不删证据、不改Passed/Failed。

问题1—5在新独立包关联通信层新有效写包、VirtualPlc实际接收、独立轴实测读回及Host业务动作校验，缺证据不能仅凭日志显示或源码关闭。取放须证明本次可靠取料及必要在途保存先于放料，随后真实放料与再抬升及完成保存；新原码由通信测试核对，业务证据不保留旧状态2/3或Sorting_OK义务；目标到位采样与抬升分开。包需记录实际进程路径/PID/端口、Host/PLC/桌面/runtime.js摘要、配置/Provider/字序及冻结配方摘要。相同Y/XY连续点仍取证，变更日志可省略但实际读写不可省略。具体[动作合同](../../003-plc-latest-protocol/contracts/plc-stage-action-port.md#usr-e完整坐标与动作观测)及[plan交接](../plan-six-issues-alignment-20260926.md)为口径；本轮没有新运行包。

2026-09-27冻结构建采证增量：当前工具链可显式传递本项目本地Test HostDll/PlcDll，预算读取同Host目录中的实际Application/Domain/Infrastructure，并核查实际加载位置，禁止同一worker混用已加载的不同构建。新源码测试/诊断输出与原冻结默认程序并存，变更构建由已有reloadWorkerRoot先就绪再接班；逐job记录实际参数及摘要，不以磁盘目标摘要冒充实际加载程序集。此为既有ResourcesResolved范围的工具接线，不提供生产部署或其他程序执行入口。

## 2026-09-27 Test Host I/O运行设置（原FR-013/014，T054/T069/T070）

r16在全部构建/测试结束后，以同r15普通构建/原PLC/default server GC/原配方及全部期限，仅所属Host DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，Q06同rund4fc1789-b50b-487f-953c-9acbb14410eb完整Final及全部适用审计通过。单代表不证明全局根因，继续剩余主流程。

采证脚本增加显式HostSocketInlineCompletions Test开关：queue job JSON字段`hostSocketInlineCompletions: true`，wait worker只传递verify CLI `-HostSocketInlineCompletions`，verify传同名开关至start。start必须已有合法purpose=Test fixture，否则拒绝；只以Start-Process -Environment设置该次自身Host，PLC/父进程/系统环境不改变。默认不开启，record.configuration.hostSocketInlineCompletions记实际模式，实际DLL/GC/期限照旧登记。替换仅匹配临时request的诊断接线，不读取遗留request，不新建平台或扩大业务API/页面。

这是当前虚拟Test运行配置，不宣称生产默认或真实PLC已经验收；原默认模式的失败与根因未完全确诊事实保留。没有改算法、配方、设备协议、采集/保存或任何期限，没有新增重试、假完成或跳过。剩余代表沿明确记录的Test设置验证，若失败仍保留并暂停。

## 2026-09-27 虚拟PLC I/O有限比较（T054/T069/T070）

r17 GROUP-A-E/run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，实际XYZ已匹配。Host20:15:49.799033Z写出tx9478，PLC20:15:54.5024904Z才读头、处理0.0133ms；当时PLC累计GC37.593ms，不据此认定GC根因。正式失败保留，不计通过，后继暂停。

增加默认关闭的PlcSocketInlineCompletions开关及queue字段plcSocketInlineCompletions。沿wait→verify→start传递，必须合法Test fixture；仅所属VirtualPlc Start-Process -Environment设置DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，process.configuration.plcSocketInlineCompletions登记实际启用。Host开关原含义保持，父进程/系统环境不修改，不改变DLL、GC、协议、期限或成功条件。先对同成组路线作一次独立有限比较，真实页面、数据库、设备及算法全链仍必须完成。

采证器对非预期恢复场景应识别现有页面阶段的“阻断”状态并保存StoppedOrUnknown，避免只识别中文fault而等待全预算。恢复注入仍沿既有waitingRecovery条件操作，不把阻断视为Final。

## r18恢复采证时序缺陷（T069）

job002旧run f86b2a63-030b-4792-ad8b-2986f6ada2c8、新run8aaf32b5-c77f-4342-a4a5-d2089db03bec已真正完成双端复位/初始核验/显式新轮及Final，exit1/cleanup=true。23项读回中仅old_actual_media_visible_through_fault_and_reset=false：beforeFault三维图片已在API Ready，却尚未画到页面（页面Idle）；atFault/afterReset实际显示true，旧SQLite/文件/API摘要一致。原包保留Failed，不能以新Final抵整项。

采证修复仅在既有oldMediaProof内有限等待同旧run实际Ready三维媒体的img data-media-id且complete/naturalWidth>0，再保存截图及注入F故障；不改页面、算法、PLC或业务期限。不写DOM/不伪造displayed。后续新作业重新验证全条件，不覆写本包。

## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

## 2026-09-27 T065机制修复范围（待本轮验证）

同DLL受控观察已证明Portable批队列派发延迟：Host业务/心跳txn3分别入队后1112.2005/1056.6997ms，入队仅0.006/0.0072ms，真实1秒超期且锁定；独立Native候选2937个非零操作唯一回调，1560个Host响应头最慢20.2574ms。证据入口：`.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/`，旧构建和报告保留。

最小接线为Windows、purpose=Test冻结fixture显式WindowsNativeThreadPool开关，仅本次所属Host/VirtualPlc子进程DOTNET_ThreadPool_UseWindowsThreadPool=1、inline=0；普通启动及旧冻结构建不被静默改写。三个最低线程预留位置依微软支持的实际运行配置区分Native/Portable，Native不调用不支持的SetMinThreads、不虚报预留8。正式构建不含Harmony、socket反射或诊断事件。业务API、信号、1秒I/O、3秒心跳、50ms轮询、GC、优先级、失败锁动作及未知结果不重发条件不变。

本增量沿003 T065和008 T055/T070原任务，追加任务0、勾选不变。只验证该机制路径、原期限真实超期锁动作及当前正式Q01同run前端/配方/PLC/相机算法/SQLite媒体/Final；复用未改分支历史证据。r22 HTTP独立保留，真实设备/标定仍待现场，不增加全运行时证明门槛。只有本轮验证完成后才更新验收状态。

## 2026-09-27T05:09Z 本轮验证完成状态

前述实施前待验证状态由本节接续：003 T065原Test/VirtualPlc机制/对照/安全/日志条件，以及008 T055当前正式Q01和T070适用Test对账均已满足，仅这三项授权勾选更新。新构建显式WindowsNativeThreadPool/inline0，旧默认与冻结程序不改；r22 HTTP、缺失历史日志及真实设备/标定不扩大结论。新证据目录为.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z，verification-proof.json与task-checkbox-changes.json可核对；本轮新增任务0、其他勾选不变。

## 009 / AL06 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

取料顺序为当前可靠取料/源点关联→必要内部处理及raw真实提交引用→业务SortingTargetAllocator经StageEventStore真实提交InTransit→当前有效关联回执→通信才允许任何放料槽/目标/命令。事务仍归业务，通信不得直接写业务台账。区分A实际未提交且确认回滚、B实际已提交但回执迟到/丢失、C实际提交未知、D已提交且当前有效回执；只有D在原期限/安全/epoch有效时批准放料，A/B/C均保持占用、不自动重发。无有效引用不等于库无记录，晚只读核查不恢复过期旧动作。已观察取料但raw未确认走独立失败通知，不能说未取料或伪造InTransit；业务库可写时最小UnknownHeld失败记录，不可写时日志尽力而已，重启按已提交预留/意图保守占用。

三入口（严格连续链、旧连续链、独立绑定）使用001 schema1.1独立recipeApplication完整冻结来源，Test10000ms；Production未批准拒绝且无回退。绑定意图真实提交取得有效回执后，在端口/排队前唯一t0；D=t0+预算，T取D与已有适用绝对截止最早者。011当前软件绑定的RecipePlanBound及本次适用handoff真实提交/回执共窗，不再含旧配方设备应用或raw前置，每次保存另取CriticalSave和剩余T较小者。Bound仅由当前有效RecipeBindingReceipt形成，不能补造DeviceApplied；取消/超期原子关闭后台后继派发和成功资格，已发I/O/已开始提交如实保存，晚记录不复活。严格链原绑定前三截止起点/值不变；旧链仍handoff后首次Detection；独立API无已有后段不虚构、不重复已有handoff。

009新增证据分量：同次语义ObservationId关联PlcCommunicationEvidence实际记录，查询只读；必要raw保存失败无有效引用不等于库无行。F05/F06必须分别实证提交前失败、commit后回执失效、暂不可核查；BA必须实证健康通信卡住/两容量/后台取消。原独立Host/VirtualPlc/Worker、SQLite媒体/API、实际前端渠道和来源义务不变；组件、同进程TCP与独立进程明确区分，历史通过不抵扣本轮。

## 009 / AL07 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次只做s01-store/1→2单项受控Test副本升级。Host及其他同库/媒体写者停止，维护进程全程持StoreAccessGuard独占.station01.store.lock。源核唯一Manifests StoreId/Profile=Test/版本、准确三个旧迁移及全部实际表/列/类型/可空/键/索引；拒未知/混合态、活动写者和journal OFF/MEMORY、synchronous OFF。以SQLite BackupDatabase含WAL一致备份，重新打开核完整性、身份、结构、旧表逐行payload摘要与媒体引用/文件摘要，失败不启动升级。Manifests位于同一SQLite库，不存在外部控制manifest。

从唯一EF UpOperations生成并限制为新增PlcCommunicationEvidence表和指定索引，同一SqliteConnection显式非deferred事务执行DDL、精确本次迁移记录和条件更新同StoreId/Profile的Manifests，恰一行；只最后一次Commit，不单独SaveChanges manifest、不改旧payload、不接受事务外PRAGMA/VACUUM或旧表重建。

U1始终是提交结果未知：任何中断/异常后保持维护隔离，SQLite自行恢复，独占重开核真实结构/精确迁移/同库manifest及原数据后归类U0/U2/UX；未归类不开放Host、不重跑DDL。U0完整源态且原事务结束、源/备份重新核验后才可重做。U2完整目标态经integrity_check/foreign_key_check及旧payload/媒体引用不变核验后开放，不重复DDL。UX拒绝且不自动修复，只能独占用已核同StoreId备份受控恢复归U0；无可信备份保持受限。异常、退出码、回执缺失或一次查无新表不证明回滚。

Host不启动自动迁移；维护成功释放锁后Host取得同锁并再次完整目标Probe才可读写。新空库也必须目标结构/manifest齐备。SU01三真实提交前中断、SU02 commit后回执前真实中断(U2且下一维护DDL0)、SU03未分类期间真实重入/Host拒绝、SU04不一致拒绝与受控恢复全部必需；不能用fake异常或版本字符串代替状态核查。

独立证据包包含中断位置、维护进程PID、实际数据库结构/迁移/同库manifest、旧payload及媒体引用前后核验、正式Host准入和后续DDL派发数。报告文件只是证据索引，不是另一个Host开放manifest。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。

当前来源分类的有限补齐：ResultSource在末尾新增Test，保留既有Real/Virtual/Simulated/Fallback的值和历史含义；仅由明确声明Test的实际算法生产者产生，不从RunPurpose猜测。IntegratedDetection的Source与意图来源来自IAlgorithmPort.Origin，未知仍Fallback/Unknown；完整矩阵继续使用AlgorithmOrigin与实际事实。该变化用于消除把独立Test Worker写成Simulated的固定标签，归009 T034/T035/T039及T043—T046，旧记录不重写、现有页面仅绑定来源。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

原009旧设备绑定的历史字段：RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当时正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。 此段只解释旧payload/回执，不是011当前F绑定前置；当前定义见[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)。

旧LatestProtocol/FullSimulation设备绑定回执仅供有限历史读取，按真实WriteId/Correlation及不透明引用核验，原payload不改、缺失为null/NotRecorded。011当前RecipeBindingReceipt只记录实际意图、绑定及适用handoff的业务提交；型号随实际翻转动作下发，其设备反馈仍必须真实。所有适用必要保存保原总窗/CriticalSave、关联及取消约束，自身回执不得预填，不新增成功审批或递归批准。

原009设备绑定资格包含上述通信回执，原T037—T045/T047及失败证据保持历史范围。011当前按[011 RC05.1](../../011-plc-interaction-update/contracts/recipe-contract.md#rc051-业务绑定回执11定向修订)核必要业务提交，不因旧行存在恢复资格，不伪造设备成功。实际机械动作继续核自己的真实通信证据及保存；旧绑定专项只定向迁移仍有效的保存、取消、期限断言，不重跑009全部验收。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 011/012联合代表证据范围（2026-10-03）

同次实际保存/重读、F料盘编号唯一匹配及冻结隔离，配置驱动更多检测面/四面后额外E，OK原槽/NG与Pending各区、姿态异常跳过后续检测，最后从原槽实际分拣到Pending并输出物理槽号，翻转另放回后姿态复查与分拣后下料，以及受影响通信隔离/共同执行/取消/期限/保存失败保护。优先复用现有有效组件与同次证据，不全量、组合穷举或009/010全部历史重验。原日期的恢复/ACK和旧Q范围仅保留其历史来源，延期恢复不新增为011主流程门槛，旧包不补本次通过。
