# C022-DATA：事实提交、所有权与收尾

依据FR-004/008/016至021。复用TraceWriter/TraceQuery/StageEventStore/MediaStore及已有迁移路径，所有数据库操作在后续实现/验证阶段，本轮不操作业务库。

## 提交与查询

按Run设置有限短提交协调段，覆盖读取当前revision → 构造带固定WriteId/摘要的WriteBatch → SubmitCritical → 有效Committed回执；同Run阶段Append及序号投影亦纳入共同序列。动作主链、AlgorithmIntent/Fact、对象判定、释放/暂停/取消及收尾保存都走此边界，不能只锁算法回调。

协调段不等待设备动作、算法执行、配图或无限回执；保存用原关键保存期限。CommitUnknown/阶段提交未知将该Run相应依赖置为保存受限，按原WriteId/EventId核对；不得据最新revision换新ID重新写、更不得重派算法/设备。后续事实按核对结果有序承接，不把QueuedReceipt当提交成功。数据库短事务不涵盖外部设备/算法/媒体文件。

AlgorithmIntent/真实技术终态继续使用Writes的原类型；新增StageEventType.AlgorithmLifecycleRecorded记录Queued/DispatchEvidence/TechnicalTerminal/ReleaseObservationStarted/ReleaseObservationExpired/InputConsumerReleased/ExecutionEnded/Reclaimed等版本化pipeline/1事实。正常运行期间两类存储均走同Run提交协调，结果保存后才可发布对应可消费终态。

### I1：业务阶段投影与资源投影隔离

StageEvents保留既有事件表及每阶段单调Sequence，不另建事件平台。业务阶段事件的EventId/OperationId/ConnectionEpoch仍表示原业务动作及PLC连接代次；生命周期事件拥有自己的LifecycleEventId（落现EventId）和CallOperationId（落现OperationId），Payload保存CallId/WorkerSessionId、ConsumerId及可用的BusinessOperationId/业务关联引用。算法Worker会话不得写作PLC连接代次；生命周期事件的ConnectionEpoch沿所属业务关联原值，仅作引用，不能推动业务代次。无业务关联的历史记录如实缺项，不填当前机械动作冒充。

| 追加生命周期事件时 | 允许更新 | 必须原值保持 |
| --- | --- | --- |
| 业务StageProjection | Revision作为共同事件流游标推进；RetainUntil仅取更晚保留期限 | Status、CurrentOperationId、ConnectionEpoch、LastEventId、DeviceHeld、NeedsManualReview、UpdatedAt，以及业务完成/动作许可/重试状态 |
| AlgorithmCalls/输入消费者资源投影 | 对应Call/Consumer的排队/派发证据、执行/释放/回收状态、未知原因、释放观察原起点/截止、ResourceLastEventId/ResourceUpdatedAt和资源重放游标 | Run业务终态、业务阶段身份、物理动作许可、冻结快照和已确定技术终态 |

LastEventId明确为最近业务阶段事件，不是事件流最大Sequence对应事件；资源最后事件单独存在资源投影。阶段首记录若仅为生命周期事件，应得到无业务动作身份的NotStarted投影，不能用资源EventId/CallOperationId初始化业务LastEventId/CurrentOperationId；历史字段需允许缺项，不借空GUID造事实。生产结束、对象判定等新增诊断事实也不得替换机械动作身份，只有相应正式业务阶段事件按原合同更新业务投影。

ReadAsync可返回完整审计流；业务投影重读/全量重放/增量重放均先按EventType及pipeline/1事实种类分流，资源事件只推进共同游标，资源投影独立重建。RecoverAsync在按OperationId分组前排除全部资源事件；分组的last、是否有Started/Accepted/Executing、是否终态和用于UnknownHeld/Disconnected的身份只来自业务事件，不能将CallOperationId视作未闭合机械动作。业务阶段已经终态时，不因资源流追加而重开阶段；资源未完由下面G1查询处理。既有确实未闭合业务动作仍按原恢复规则保Unknown，不伪造动作完成。

T004/T005须同时修改StageEventProjection、StageEventStore投影与RecoverAsync及TraceQuery直接消费者；不能只加枚举后复用现无条件替换CurrentOperationId/ConnectionEpoch/LastEventId的Apply尾段。T007的一项定向用例：保存真实业务阶段终态及动作身份，追加同Run/阶段的迟到输入释放事件；SQLite重读、重放、Operation分组恢复后上述业务字段逐项不变，资源投影可见释放且无新动作/业务恢复事件。此处是后续验收要求，本轮未执行。

资源生命周期以StageEvents为权威追加记录，AlgorithmCalls和必要输入消费者状态为重建投影。这样即使异常/取消已经形成Run业务终态、TraceWriter拒绝新Writes，后续可靠资源释放仍可持久追加，不为补写资源解除原终态。Final仍须此前必要资源结束，不允许Final后补必要事实。对象判定/生产结束作为版本化事实在阶段存储保存，只有真正全部完成才写既有Detection.Completed。必要新事实不能借Audit/AlgorithmFact伪造不存在的算法结果。

最小数据增量清单：AlgorithmCalls补任务关联/快照摘要及业务与回收状态；新增仅本管线使用的输入消费者投影（Run/Media/Consumer/Call或FusionKey及其状态）；StageEvents原PayloadJson保存完整必检集合/逐输入证据/原预算，原幂等键及序号索引复用；如新增生命周期枚举存储转换需要迁移，连同新增投影经现维护入口执行。旧JSON缺字段保历史缺项，不补真实Origin或释放。不得在实现时把未知硬件/真实wire字段混入此内部schema。

权威任务查询按Run/Call，输入查询按Run/Media/Consumer，融合按完整面键；对象结果含必检项和提交引用，重读可核文件键/长度/摘要。幂等键包含本Run/Call/阶段事实种类，重复相同摘要重读，重复不同摘要冲突，不覆盖终态。

## 所有权

| 持有者 | 取得/转交 | 结束依据 |
| --- | --- | --- |
| 采集缓冲/相机 | 本次合法请求/Ended/MediaTaken及保存接管 | 可靠采集资源释放，独立于文件租约 |
| 生产预约/交接 | 采集前容量预约，文件完成后持有至任务登记 | 队列及待配图消费者已取得引用，或明确未派发取消/保存阻断仍受管 |
| 本地排队 | AlgorithmIntent/任务登记可靠提交，持有各输入 | 无间隙移交隔离执行；已证明未进入端口的取消可释放本地引用 |
| 单图执行 | IsolatedAlgorithmCall接管 | 本调用输入可信释放或实际同会话退出；Result不够 |
| 待配图/融合 | 首图保存建立完整融合键，单图结果引用独立 | 融合结束且该消费者输入可靠释放；明确终止融合时只释放本地引用，不影响单图仍持有者 |
| 转换文件 | 真实适配层按已确认格式转换 | 最后实际消费者结束，保原图与版本映射 |

计数与持久投影一致；不能用单图InputReleased解除待融合引用。回收执行槽还需派发返回、实际执行结束及必要取消回调结束；所有保留额度有上限，未知占用计入上限。

## 异常、重启和最终完成

运行异常/取消后本地未派发任务持久终态化；已进入/未知保原管理者持续观察，迟到业务结果不覆盖终态，释放证据仍保存。保存故障时保已知运行内事实与已有持久输入登记，禁止宣称全记录可靠完成。

### G1：独立资源查询与Host核对

业务恢复继续使用GetUnfinishedRunsAsync及既有业务策略；资源核对另由TraceQuery提供有限分页的未回收资源查询，不对Runs.Terminal=None作前置过滤，也不沿业务恢复中RecoveryNewRunLinked的跳过条件丢弃旧资源。按Run/Call及Run/Media/Consumer完整键查以下任一项：任务未有可靠Reclaimed事实；输入消费者无可靠释放/终止事实；派发进入或未知且执行结束/输入释放/派发返回/必要取消回调结束任一未确认；释放观察到期但仍Unknown；本地队列/待配图/转换交接尚未闭合。Cancelled/Failed/TimedOut的Run仍在结果集内；异常发现已Final但资源未完亦报告不一致，不改写历史Final或猜成成功。投影缺项/摘要冲突/重建失败不能当空结果，沿已有保存/恢复诊断保该范围受限。

Host启动先在受保护的既有数据根恢复媒体清单和资源投影，完成一次独立资源扫描及受管登记，再开放依赖这些资源的准入；业务扫描和资源扫描分别留证。Cancelled/Failed等Run仅注册资源核对项，不重新注册成可运行业务、不改回Terminal=None，不自动续算、重发调用或恢复运动。旧会话没有可核提供者证明时保持Unknown，保原媒体及额度；不能把新Host时钟或新Worker会话绑定旧Call。分页查询/登记有限，不为每条历史记录创建推理Task。

Host关闭关闭新准入/派发，原监管与保存服务在有限退出预算内继续收集原调用证据、持久保存未回收清单及已知状态；关闭超期没有可靠证据仍Unknown，不把内存管理器Dispose当资源释放。保存未知须留诊断并依原ID核对，不能宣称排空成功。后续人工核对消费同一持久清单及原会话/输入映射，只能提交实际核验到的资源事实，不能以人工确认替代引擎释放或批准未知动作；本轮不增加页面、自动恢复或新的复位能力。

进程确实退出也需核验进程启动身份/WorkerSession及Call归属，才能证明该会话实际执行及其持有结束；仅旧PID不存在、EOF或新进程就绪不够。即使同会话退出成立，本地队列/待配图/其他调用/转换消费者仍分别核对，不能整Run归零。无需本轮实现透明进程重启续算。

T005建立独立查询，T027在A接通实际Host启动/关闭及既有人工核对消费者。R1最低验收直接纳入T027既有异常/关闭用例：隔离Test装配使用临时SQLite/媒体/证据及独立端口，保存Cancelled或Failed的Run及执行/输入Unknown的原Call。调用真实Station01HostedService.InitializePersistenceAsync入口（不是独立scanner或新监管实例单测），断言该Host实际资源消费者注册到原终态Run/Call/输入且业务恢复未注册其为可运行；实际ApplicationStopping停止通知和Station01HostedService.StopAsync消费同一登记。原ReleaseObservationStart/Due/Trigger/Clock/预算引用在初始化、停止、重读后完全不变，不能重开预算；真实Host退出窗口独立。关闭后SQLite仍可查未确认资源/原媒体，业务终态不变、续算/算法重发/机械派发均0，无伪造InputReleased/Exited/Reclaimed。只调用持久初始化和停止消费者，不运行设备HostedService、不建立硬件连接；测试侧断言设备服务Start次数和端口派发次数均0。T029引用此证据并只补实际同步执行链生成/保存资源及收尾衔接，不重复生成扫描器单测或扩大异常组合。

Final前：生产结束；各对象完整判定和物理处置按原规则完成；必要结果/媒体/动作/生命周期事实提交可靠；全部相关计算、输入及取消工作可靠结束；再走原下料/允许取盘/人工确认/Final。全入队、PartialObjects、Result或业务超时都不能直接释放任务占用。异常终态与资源占用分开，保恢复核对路径；不改020 T055/T056未验状态。

迁移只增量并保历史JSON/文件可读，经现有StoreAccessGuard及维护流程在隔离副本验证。运行时不静默改表。回退前先停止准入、核资源和备份副本；未证明解除所有权不能删除输入。旧二进制若不能读取新结构，使用匹配的旧离线副本，不对新库做破坏性降级。


## A阶段增量

A最小实现沿StageEvents权威流分页构造AlgorithmResourceProjection查询投影，不要求新增第二份可漂移的持久表；原AlgorithmCalls意图/结果保存保持。资源独占阶段不创建业务StageProjections行，查询/重放返回NotStarted、CurrentOperationId/LastEventId=null、无业务更新时间。已有业务投影的资源追加仅推进Revision和更晚RetainUntil。事件序号从StageEvents取得，不依赖虚构业务投影；正常业务行原非空身份及原schema保持，无Host自动迁移。RunFactCommitCoordinator覆盖同Run短提交段；未知资源仍由权威流查询，不能把无业务投影或Run终态当空资源。

基本同步接入也可能在Run已异常终态后收到可靠释放，因此I1/G1/U2与实际Host验收不是后台优化专属，必须留A。转换产物来源按C022-ALG保存于既有媒体索引/sidecar及版本化媒体来源事实，原采集/派发输入/消费者三者可重读；具体持久字段先随T004/T005契约核对，再实现T016/T023，不以只保存文件名代替归属。重读/恢复隔离规则同前，基本路径不引入ProductionEnded事件。

### A媒体增量签名（实现前确认）
MediaRef新增可空AlgorithmInputProvenance：SchemaVersion=algorithm-input/1、RawMediaId、RawRelativeKey、RawFormat、RawSha256、InputSha256、ConverterId、ConverterVersion、Width、Height、PixelFormat、PointCount及保留的PointUnitSource/CoordinateSource。它是媒体来源事实，不是AlgorithmFact；来源未知以null记录，不猜设备坐标。IMediaStore.PrepareAlgorithmInputAsync(raw, token)仅处理已提交、元数据摘要可核的GalaxyRaw Mono8和CameraPro XYZ；未知格式拒绝，现有模拟媒体不自动转换或伪称真实。转换生成仍须提交既有Media事实后MarkCommitted；恢复通过原RawMediaId查同采集事实及原文件/摘要，不能为产物伪造CaptureFact。PLY仅提供已验证binary little-endian XYZ无RGB候选，配置未确认不得用于真实派发。
采集Reservation只预约实际单次内存及待写载荷磁盘；元数据和转换文件独立预约磁盘，保留失败部分亦计实际占用。MediaLease另占工作保留额度，消费者Dispose只解除工作保留，不删除文件或减持久占用。三类额度分别可观察，沿既有有限memory/run-file/data限额，C批次机制不启用。

A资源恢复增量：实际Host恢复未确认输入时，对已Ready的原输入重新取得工作保留租约（不增加/减少实际文件磁盘）；缺少输入仍明确Unknown并报告。未回收资源存在时Start公共准入在任何新采集/动作前拒绝AlgorithmResourcesUnconfirmed；不由重启或Test计数清空。资源核对不恢复业务执行。

T026/T038-A关联修正：已绑定WorkerSession的Call拒绝缺失或不同会话事件，匿名InputReleased/WorkerExited不能解除原占用；本调用的成功Exited任务仍是可靠执行结束证据。结果必须保持原FrozenModule模型/参数文件版本及摘要身份，不只匹配配置摘要。

G1分页实现衔接：未回收结果按RunId、OperationId、CallId稳定排序后进行有限OFFSET/LIMIT；同Operation的不同Call仍独立，过滤最新调用状态及Reclaimed必须在分页之前。不新增业务恢复许可。
