# 022 A阶段实际验证记录

2026-10-09，隔离分支 `022-real-algorithm-pipeline`，源码基线 `eb85aa4b2985e61171b9d1d749af346207282d9f`。本记录仅证明基本接入软件及原同步链；B真实算法/标定/格式接受和C重叠性能没有执行。requirements旧34项完成表述是历史需求检查，保持只读，不作为本轮实现证据。

## 实现与任务状态

保留40个原任务ID；下表的Verified指A明确子范围，整任务含未实施B/C时不勾选。拟新增路径按实际最小落点合并，未另外创建同职能平台或无必要数据库迁移。

| 原任务 | 本轮A状态/实际落点 | 未完成的局部范围 |
| --- | --- | --- |
| T001/T002 | Verified：基线保全、共享契约/实际消费者审查，review.md | 无现场授权 |
| T003 | A直接增量已同步022契约；原008–011维护位置/消费者已登记 | 原来源不回写；若后续需改其独有条款，DEP-DOC-08仍局部待处理，整项不勾 |
| T004 | Verified-A：CaptureCompletionEvidence、PersistedCapture、完整AlgorithmRequest/ResourceState、业务/资源事件身份 | C对象句柄/ProductionEnded未实现 |
| T005 | Verified-A：Station01/RunFactCommitCoordinator、RunExecution、StageEventStore、TraceQuery；沿现StageEvents保存/重读，无DB schema变化 | C对象后台事实未实现；不自动迁移现场 |
| T006/T007 | Verified-A：AcquisitionCoordinator、ThreeD/FScan、检测/复查及真实/虚拟PLC迁移正确类型证明；错误AlgorithmFact/未知提交/其他窗口拒绝；短并发保存与原ID对账 | T006的提前释放仅C；A仍原await |
| T008 | Verified-A：基础消费者及SQLite证据审查 | 无 |
| T009/T010/T014 | Verified-A：Program实际调用Station01OptionsReader→Options→严格独立Loader/schema→Registration/Capability/Validator；冻结描述、未交付/非法/Production拒绝；Test两种装配实际执行 | T010真实用途正向/B审查不完成；真实预算实测未提供 |
| T011/T012/T013 | 交付缺项已核，A边界/NotIntegrated拒绝已实现 | ExternalDependency：真实端口桥、程序、模型、输入确认、标定/规则、应用/取消释放与真实正向验收，未实现未运行 |
| T015 | Verified-A：AlgorithmRuntime.Synchronous、IsolatedAlgorithmCall、Supervisor；单准备槽、原绝对结果/释放预算及全同步消费者 | C后台有限批次队列/共享池优化未实现；A不宣称常驻真实模型 |
| T016 | Verified-A：AlgorithmMediaEncoder、MediaStore.AlgorithmInput、Capacity/LeaseSupervisor；Mono8 PNG、候选binary little-endian XYZ PLY，来源落库/重读，三类额度分开 | C批次预约未实现；RGB/3D伴PNG/其他位深/真正PLY接受待B |
| T017–T022 | Deferred-C；原机械/批次/特殊作用域逻辑保留 | 全部未启用未实施，不跨Run/料盘 |
| T023/T024/T025 | Verified-A：实际3D/F、复查、同步E、单图/融合受管；原NoWorkStarted整段重试门禁；普通和特殊本件出口、最终完成实链与技术失败不分拣 | C对象增量等待/预约不实施；真3D定位/规则不冒称已验 |
| T026 | Verified：完整Envelope、逐输入/对象/面/阶段/轮、会话/模型参数关联，唯一终态、迟到可靠释放 | 实际真提供者B另验 |
| T027 | Verified-A：实际InitializePersistenceAsync/ApplicationStopping/StopAsync、旧Unknown工作保留及新Start拒绝、原截止不重开、无自动续算 | C已提交后台任务暂停满载场景延期；不存在旧进程即已释放的推断 |
| T028/T029/T030 | Verified-A：两Run、同面融合文件/SQLite、代表技术失败经实际ThreeStage拒绝重试、保Unknown及迟到释放，实际Host监管 | C乱序并发/队列满载/AB批次配图未实施未测试 |
| T031/T032/T034 | Verified-A：独立描述/模型参数摘要冻结及Audit SQLite恢复；在途正式Test配方同步消费旧参数，保存新版本后后轮消费新参数，原始/转换/Call/结果落库 | C队列中更新延期；真实已加载模型应用待B。Test已批准配方来源使用测试文件目录持久读写，未改变生产配方保存规则、未冒称正式作者API验收 |
| T033 | Verified-A：直接受影响采集、完整周期、同坐标/六轴/请求释放、媒体保存失败、质量及Final定向回归 | 未修改复位解释/轴底层，其现场证据保持；未全量重跑、未勾020 T055/T056 |
| T035/T036 | Verified-A：实际Host原格式/PNG普通同步两轮、PNG特殊两件两组旋转原序→即时回放/分拣→最终人工确认→SQLite新读取；E/五Role/PLY关联复用必要组件场景 | 没有真实算法；PLY不在设备Host正向链；SC-001重叠及完整SC-004/C容量不通过；B/C保持未完成 |
| T037/T038/T039 | 独立只读审查发现S1/S2/S3，另阶段修复并定向复验；错误窗口重载/直接算法旁路已由受管消费者替换，同步ExecuteAsync及正式虚拟实现保留 | B/C新增实现后再审查，不删除尚有效的串行顺序 |
| T040 | 本记录/task状态/无损差异保全为A子范围记录 | 不自动执行speckit-converge，不宣称全022/实机完成 |

## 实際验证与证据

唯一主证据根：`artifacts/022-A-final-187aee9fc19147bc9597ba6a4c6dcf9b/`。文件SHA256、源码/新增文件清单、基线与原工作区复核见根下 `source-manifest.json`、`preservation-check.json`、`source-diff.patch`、`documentation-diff.patch`。所有输入源只读；输出/SQLite/端口为独立测试环境。构建：dotnet 10.0.401，Communication.Tests项目及其引用Host/Application/Infrastructure构建，0警告0错误；锁定恢复无工具升级。

本轮38个不同的必要用例（原36项中Host两项替换为最新三种情境，再加入短提交1项）均有成功证据；不是一次38/38运行，不累加重复复验作为新覆盖。保留每轮失败与修正，只复验直接影响范围。

| 证据 | 结果与范围 |
| --- | --- |
| A-targeted.trx | 36执行，34通过，2失败：Host业务链已过，但导出试图复制持有中的`.station01.store.lock`；没有把失败记通过 |
| host-export-fix-*/host-retest.trx | 2/2：证据导出排除锁文件；不更改业务条件 |
| short-commits.trx | 1/1：真实SQLite短提交、丢回执原WriteId确认，无新ID重发 |
| review-fixes.trx | 11/11：S1/S2修复直接受影响受管/媒体/生命周期/Host |
| identity-final.trx | 4/4：匿名会话不能回收、模型引用不同不接受；构造采集完成证明时尚无AlgorithmFact，错误类型回执拒绝 |
| stable-pages-final.trx | 4/4：S3稳定Call分页，终态后追加释放业务字段保持、实际Host启动/停止/两次重启Unknown发现、Start被Unknown拒绝 |
| barrier-host-final/host-barriers.trx | 3/3最终实际Host：Native普通、PNG普通（在途更新+等待屏障）、PNG特殊两件两组旋转；每种均两Run及人工Final；新SQLite读取重建Completed |

新增/定向用例文件：RealAlgorithmConfigurationTests（9）、RealAlgorithmMediaTests（2）、RealAlgorithmManagedCallTests（3）、RealAlgorithmPipelineLifecycleTests（4）、FormalTestRecipeUsesPng...（2）、CommissioningWorkflowTests（3）；其余15项为直接影响的既有定向回归。没有新增全量异常组合。

PNG/PLY真实离线输入为原库 `artifacts/camera-runtime/a-download-1.raw` 与 `3d-download-1.zip`，前后SHA一致；`media-a-*`/`media-3d-*`含源/产物/sidecar、PNG解码像素或PLY XYZ原字节、来源对应SQLite与新读取的verification.json。不存在缩放/裁剪/归一化、点过滤、RGB或设备坐标变换。PointUnitSource/CoordinateSource缺项仍null。

最终Host复制在 `barrier-host-final/host-native-*`、`host-png-*`、`host-special-*`：独立原存储文件/SQLite/媒体/trace；普通Native每Run7份media，PNG普通12份，PNG特殊19份。`022-png-frozen-version-two-rounds.json`记录在首轮首单图派发尚未返回时保存新Test配方/参数；首轮4份实际CaptureFact gain=1、6份单图/融合AlgorithmIntent parameters=test-1，后轮gain=2/parameters=test-2；两轮不同冻结计划均可SQLite重读。读取的是已保存旧输入，不只内存字符串。

该文件barrierEvidence记录真实Host Test算法Result尚未发布的100ms内，PLC模拟引擎accepted动作数及实际采集触发总数不增加，覆盖3D/F及单图/融合；仍同步等待。特殊scope的UnitCycleCompleted在下一件TransferToRotation Intent之前，来自实际阶段序列，未扩大机械权限。E同步及原追溯/融合质量在FormalTestRecipe场景验证；真实E输出规则待交付。

## 失败与范围限制

初始Test F Z=0未有可复用历史到位动作导致正式条件拒绝，夹具改为明确Test Z=1；未改生产门禁。特殊首次Test旋转角0未能取得完整动作确认，保RecoveryRequired失败证据；后续使用显式Test角10/90验证既定两组流程。不以模拟器结论推断真实零角初态/机械校准；真实点位仍B依赖。之后出现测试对RotationLoading宽泛JSON匹配同时计入其他事件、CaptureFact字段大小写断言错误，仅修断言。历史文件 `special-host.trx`、`special-retest/*`、`special-version-final/*`保留失败，最终成功在上述最终目录，不拼为成功路线。

Host证据导出曾因未指定环境落到020旧测试证据目录；仅核实并移动本轮新建8份文件到 `review-fix-host-output/`，原020文件/任务不改。现Test默认输出022独立目录，显式证据环境变量只在测试代码中；未作为业务开关。

没有实测真实CPU/GPU/加载节拍/常驻复用。RealAlgorithmLoad.IsReady仍false，文件存在和SHA匹配只FileVerified，不是模型已应用；真实选中仍NotIntegrated及实际能力问题，无真实正向Start、执行或安装。模型/规则/标定、取消/文件释放/退出证据、3D伴PNG/编码/RGB确认及正式预算分别局部阻断T011–T013和对应真实模块，不阻断已验证A软件。

## 六项旧问题与R1/R2/R3

I1：A实现并验证业务投影身份不被资源事件改变；G1：A独立终态资源查询/实际Host发现停止/新Run拒绝已验；I2：T017未启用，C前置继续保留；G2/R2：独立实际配置读取/严格schema/装配/能力拒绝/冻结Audit落库已验，真实用途正向Ready仍B阻断；U1：A内存/工作/实际磁盘计数和消费释放已验，C批次部分延期；U2：每Call原起点/截止和Host退出预算独立、Expired Unknown持久可查已验。R1由实际Host两次重启、ApplicationStopping及StopAsync消费证明；R3/T010前置保A拒绝与冻结证据，C启用仍禁止。延期不是已修复，不用任务数量宣称真实实现完成。

## 回退与后续

实施前完整副本 `C:/Temp/gaode-022-A-before-f541ae18dee444e08eb646b5ff6fe697/`，含原022文档和baseline.json/摘要/feature字节，源码起点为上述HEAD。本轮修改/新增文件内容、源码diff及文档相对原副本的diff保存在本功能证据根；回退在另一个目录读取HEAD对应单文件，与备份及当前内容三方比较，逐块撤回本轮修改；新增文件先核摘要/引用，转移到本功能回退档案目录，不删其他未跟踪内容。先保存后续差异，不直接覆盖现场/当前文件，不reset --hard/clean，不改现场库或任务状态。可按证据/捕获类型→媒体→受管资源/Host→配置冻结消费者分阶段审阅，但共享接口撤回须连直接消费者保持可构建；不把拆阶段当任意混版本运行许可。

最终原D:/gaode的12项修改+133项未跟踪文件摘要及原feature一致；隔离feature/branch/HEAD保持022/eb85aa4；requirements SHA256=2D80B4CEE4D4DD4F684AA6C456B795B8488B5AB616907DCD895466019298F3F7，未变。无安装/现场设备/程序/数据库/配置操作、部署/包/提交/推送；020 T055/T056保持现场未验。

A软件证据具备范围限定的speckit-converge审查条件，尚不能收敛为真实接入完成或全022完成；等待下一条指令。B交付与C延期责任保留，不自动现场验收。


## 2026-10-09 T041–T044定向修复与最终验证

本轮只修A原同步释放/关闭/仲裁/Host预算四项，不实现算法包桥接，不以真实精度、质量规则或模型验收作修复前置。既有A、B/C混合任务状态及历史失败仍保留。唯一新增证据根：`artifacts/022-T041-T044-7db5b1d250354e8f8dcb56a31eac2307/`；最终用例清单在`final-test-index.json`，失败/编译/测试DLL锁定原因和修复见`build-attempt-history.md`及原TRX/log，均不删除。

| 任务 | 实际实现与直接消费者 | 最小验证及最终证据 |
| --- | --- | --- |
| T041 | AlgorithmRuntime公共3D/F等待本Call InputAndExecutionEnded及唯一释放剩余额度后才报告成功；未知保持占用/阻断。WholeTrayCompletionStore首次Final及Reconcile任务释放核本Run最新持久未回收状态 | host-reviewed/host-reviewed.trx：Release3D、ReleaseF实际Host结果后尚未退出时无下一采集/动作，原Call Unknown可新读取；Release3DTimeout观察到期不采F、不形成Final、可靠迟到结束后不自动续算且截止不改。None/Native经正式人工API对注入Unknown返回409 AlgorithmResourcesUnconfirmed，无Final/任务完成；可靠释放后原普通链正常Final并重读 |
| T042 | 公共实际Start mayEnter补Supervisor.AdmissionClosed；仅证明未派发时本地回收 | 同TRX CloseBeforeEntry：测试范围保存登记屏障后实际Host NotifyStopping/StopAsync，端口Requests空，持久NotDispatched/Reclaimed及原截止保持，不连接设备HostedService/真实硬件 |
| T043 | ManagedAlgorithmCall.Result/LateResult独立，Result/Failed/Timeout/Cancel/DispatchError一次裁决；检测/E/融合/复查消费权威Result，去掉重复结果计时；公共资源终态沿Ingress结果 | component-reviewed/component-reviewed.trx：ActualRecipe迟到NG在已确认超时后仅形成原FinitePending，AlgorithmFact TimedOut、资源技术终态及SQLite重读一致；取消先到后迟到结果不能覆盖，原释放Start/Due保持。复用原两轮全身份、模型参数/匿名会话校验、同步PNG/E/融合及技术失败用例 |
| T044 | 实际Host消费Runtime.WaitForShutdownAsync逐Call原窗口，Host/外部Token只截短；Unknown算法租约不再使媒体等待占用完整Host窗口；实际释放状态仍未确认，必要事实保存继续按Host剩余额度 | 同组件TRX activeExpired=true：已超时且原释放观察耗尽的实际在途Call，ApplicationStopping/StopAsync在1s上界返回（配置Host5s），ResourcesDrained=false、remaining执行1，旧释放截止不重开，SQLite清单可查；随后仅可靠退出/消费者结束才Reclaimed；下一次InitializePersistenceAsync仍发现原异常终态Unknown，不续算/重发 |

最终构建：Communication.Tests及引用Host/Application/Infrastructure，dotnet build --no-restore，0警告0错误（build-final.log）。最终组件12/12、实际Host7/7；19个不同必要用例分两批执行，原同步普通/特殊两轮、版本冻结和真实SQLite/媒体证据保留；不是实机或算法包验收。首轮11/12和第二轮10/12及构建锁定失败均有原记录；定向修正后通过不追溯改写失败。

只读实现审查见review.md本轮节；修正重复结果裁决后重新构建/复验；未发现本轮四项范围内剩余确定软件缺陷。可再次执行speckit-converge，但本记录不自行宣布022或B/C已收敛。T017禁用，B未真实激活/验收，C延期，020 T055/T056现场未验证。requirements原SHA256保持，旧检查勾选不是本轮验收来源。

回退：HEAD仍eb85aa4b2985e61171b9d1d749af346207282d9f，分支022-real-algorithm-pipeline。`C:/Temp/gaode-022-T041-T044-before-7db5b1d250354e8f8dcb56a31eac2307/`有before-files.zip、baseline.json、before.patch及本轮首次修改的原WholeTrayCompletionStore文件。只针对本轮差异在隔离副本逐项恢复，先备份本轮后内容；不能整库reset/clean、覆盖旧A改动、删资源输入或操作现场。本轮差异、源码摘要和验证索引在上述独立证据根，不提交/推送/部署。

证据补强（仅测试导出，无产品变更）：host-resource-evidence/host-resources-*.json保存实际Host每次SQLite新读取的Unknown清单、原截止及关闭快照，对这两个已有初始化/停止用例复验2/2，不计新增覆盖。late-recipe保存对应已通过迟到用例的隔离Test SQLite/媒体副本；late-recipe-readback.json再次内存读取数据库/WAL完整性ok、7个调用全部Reclaimed、唯一TimedOut及9份媒体，绝非现场库。
