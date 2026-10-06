# 采集、媒体与算法调用合同

**版本**：s01-capture-algorithm/1.2，拟定端口与Worker协议。
关联FR-010至FR-016、FR-018/020/022/027/029、FR-033至FR-040。

## 1. 采集端口

ICapturePort提供RequestCapture与ObserveCapture，不同步等待整次采集。请求含信封、captureId、role=ThreeD/F、设备/光源绑定、固定pointId/version、scopeId/version（3D）、参数快照、容量预约、Motion位置保持许可与deadline。

事件顺序：CaptureAccepted → Exposure/TransferInProgress → CaptureEnded → MediaReceived，或CaptureFailed/Unknown。CaptureEnded表示相机/光源按契约结束、允许后续运动的可靠依据；MediaReceived表示软件安全接管缓冲，两者均不等于文件保存。支持同一事件同时提供两类证据，但字段分别明确。拿到一帧不代表采集动作必然结束。

- F请求强制单次触发、单张图像；重复帧属于重复投递，不触发重拍或新Call。相机/光源完成未知限制流程，不能套用算法非阻塞。
- 3D用公共整盘scope，不自动分槽、不添加扫描运动；真实覆盖方式、点云格式和相机曝光次数仍需OPEN-22/26。内部媒体集合可以保留多个实际产物，但不能据此授权未确认的真实采集步骤。
- 触发前取得内存/磁盘及终态回执容量，复用相机连接。SDK回调只校验最小元数据、接管引用并投递；不能等队列空位、做推理或存图。预算外/重复帧立即按所有权释放并记录，不覆盖原帧。
- 重连增connectionEpoch并重新应用参数；旧连接回调保留原captureId或隔离，不绑定新的当前采集。未能接管且实际数据丢失须记MediaLost，不能声称恢复可重建。

## 2. 媒体引用

MediaRef字段：mediaId、runId、captureId、kind（PointCloud/Depth/Image/RawAlgorithmResponse）、source、byteLength、format、维度/布局（实际有时）、storageState、逻辑artifactKey、leaseId及参数/范围版本。外部API只暴露mediaId；内部Worker只拿分配给它的受控只读文件引用，不接收任意路径。

MediaStore拥有缓冲和文件；采集移交所有权一次，归档与算法各获得租约，不竞争读取同一Channel。已完整落盘的文件可供算法读，未完成文件不可见；仅收到内存不发Worker。需要留存的媒体先完成文件与元数据提交，再启动算法，简化本阶段恢复；不承诺此保守顺序满足现场节拍。

采集可靠结束但确无可用媒体时记录缺失/损坏及来源，调用形成DependencyFailed/InvalidResult；按spec允许的内容异常继续。真实硬件未知或已得到的必要文件保存失败不适用这一出口。

## 3. 算法端口和终态

IAlgorithmPort.SubmitHeight / SubmitFDecode输入：
callId、captureId、operationId、attempt、sessionId、原runId、公共/scope/参数版本、输入MediaRef列表、期望契约/组件版本、Host期限、用途与来源；Part/Face为空且原因明确。

派发前必须按[persistence-handoff §1.1](persistence-handoff.md)保存独立AlgorithmIntent：原Call/Operation/Attempt、Run/Capture、输入媒体引用、配置/能力/期望算法版本和调用依据。Runtime只接受匹配的Committed证据作为派发门，Failed/CommitUnknown不得Execute；保存意图不表示端口或Worker已收到。实际Dispatch/Accepted作为独立证据，无法证明则Unknown；不因无记录自动重算或重拍。

回执区分意图提交、本地调度接受、实际派发、Worker接受、Running、终态。Application在登记Call并提交调度之前启动原Host总期限，覆盖意图保存等待、排队和Worker执行。意图提交期间到期则原Call TimedOut(PreDispatch)，晚提交不再派发，必要保存仍须完成；Committed/Accepted不重置期限。缺预算/能力未配置为NotConfigured，未接入为NotIntegrated，不就绪/执行槽不可用为NotReady；不得要求整个运行先等算法就绪。

| 输出 | 内容与限制 |
| --- | --- |
| 3D观察 | 首次有无/姿态及F绝对XY，关联真实槽号、运行/采集/调用；翻后姿态复查保留实际结果，具体字段留011 plan |
| 3D观察失败 | 保持原始证据与失败原因，限制依赖姿态/F定位的动作，不补0、固定F点或正常姿态；旧高度结果承接历史读取 |
| 读码 | responseReceived标记、原始响应引用、rawCandidates[]保留重复项、actual码制/置信度（若有） |
| F规范化结果 | 相同原始string按Ordinal去重；0值NoCode、1值Unique、多个不同值Conflict且primaryCode=null；响应未取得时rawCandidates=null而非空集 |
| F语义 | Unique内容即料盘编号；共同业务唯一匹配保存配方，不能把码内容当配方身份或PLC型号；原文/候选与失败关联保留 |

调用技术状态包括Queued、Running、Success、Error、TimedOut、NotConfigured、NotIntegrated、NotReady、NoResult、InvalidResult、DependencyFailed、Cancelled。Error可带Interrupted原因。质量始终NotEvaluated。一次有效终态后迟到结果仅保存LateEvidence，不改终态/移交。

### 3.1 进程内端口隔离和所有权澄清（T025，2026-09-21）

不改变原预算、F独立槽及Worker线协议。以下是内部端口的失败/所有权语义，不是新增现场能力：

| 边界/事实 | 业务与资源裁决 |
|---|---|
| 调用前 | AlgorithmIntent必须Committed；等待执行槽仍计入原期限。过期/控制关闭后尚未进入端口，不再派发。 |
| 进入RequestAsync、尚未返回ValueTask/Task | 独立有界隔离执行；可能已取得输入，缺Accepted不能推定未派发。原业务窗口继续计时。 |
| 已返回派发任务、未受理/未完成 | 任务返回不是Worker接受、输入释放或执行结束。到期仍有限结束业务，底层占用继续保留。 |
| 发出取消/执行取消回调 | 取消令牌不与业务调用者直接链接。取消通知最多一次，在隔离执行中处理；业务不等待回调，回调挂起或抛异常单独保留并观察。 |
| 可靠未派发拒绝 | 内部AlgorithmNotDispatchedException必须携带匹配CallId和依据，且保证拒绝发生在取得/转交任何输入、启动工作或发布Accepted/Running/Result之前。只适用于适配器能证明的边界；普通同步/异步异常均不是这种证据。已观察到输入使用后再声称拒绝不得释放。 |
| InputReleased | 仅证明此调用不再使用输入，幂等释放媒体；不证明执行槽或取消回调已结束。 |
| WorkerExited / AlgorithmDispatch.Exited成功完成 | 是按既有合同核实的实际执行结束依据，可释放输入；任务Faulted/Cancelled不是退出依据。 |

每个角色最多WorkerPerRole个实际占用，队列上限沿用AlgorithmQueuePerRole。每个已占用槽最多一个派发隔离任务和一个取消隔离任务；挂起不创建替代执行槽。只有业务已终结、派发调用已返回、实际执行可靠结束（或可靠未派发拒绝）、已请求的取消回调也结束后，才回收对应槽及取消源。高度和F分别计算限额。输入可依InputReleased先于执行槽释放。

迟到返回/异常/Accepted不改既有业务终态；迟到结果仍经原Ingress归档，释放事件只更新资源证据。活动占用、取消回调状态、异常类别和可靠结束事实可查询；完成回收记录保留有界最近64项，业务Call/Intent/Fact仍按既有持久化合同保存。Host关闭记录尚未回收的算法占用，不伪造退出。进程内不可强制终止的调用保持Unknown/隔离，人工处置及完整恢复仍属后续范围。

旧code.test-tray-format/fixture-1是历史测试样例格式，不限制现行料盘编号，不保留测试业务分支。当前所有输入经共同唯一匹配，不能靠格式筛掉冲突候选；旧样例读取与调用保护按真实消费者承接。

## 4. Python Worker内部协议

开发候选：Host管理常驻Python子进程，按角色Height/FDecode各1个执行槽，串行处理单Worker任务。全模拟时绑定进程内替身、不启动Python。真实算法尚待组件提供，只有协议与适配责任设计，不编写算法实现。

控制采用UTF-8 NDJSON，一行一消息，最大64KiB；stdout只承载协议，stderr有界持续消费诊断。消息类型Hello/Ready、Execute、Accepted、Result、Cancel、InputReleased、Health、Shutdown，带contractVersion、workerSessionId、callId、attempt及输入lease引用。Execute不含原图Base64；算法产物先在受控输出目录完成文件，由Host校验归属、大小和格式后接管；Worker不能写业务库或控制设备。

Host本地单调时间为期限权威，Worker预算只作协作信息。取消不依赖Worker确认才终结业务窗口；超时后后台回收。媒体InputReleased或进程退出后才释放Worker租约；失联不等于退出。F的槽与高度故障回收分离，缺F算法时仍有限NotReady/NotIntegrated。

协议异常、超长/不完整行、退出、无响应及错误版本均形成可关联技术终态；保留实际收到的原始字节证据（超限输入仅记录受限片段、原长度/截断原因，不伪称完整响应）。未知call/session消息隔离。生产组件路径为Host允许清单，配置不允许任意脚本上传/执行。具体算法包、依赖、相机格式及兼容性按OPEN-22/26验证。


修订记录：2026-09-20，1.1，按H01补齐调用前持久依据和派发门；Worker消息线协议仍为既定版本，不声称真实组件已支持新增现场协议。

2026-09-21，1.2：按第二次独立审查及用户授权补充同步派发隔离、取消回调和未派发拒绝/输入所有权语义；不修改FR/CL、现场OPEN、线协议或阶段边界。

## 2026-09-24 008完整执行合同增量

适用优先级：本节及008目标合同用于最新需求实现，前文冲突条款仅在本节明确的历史样本范围保留；本轮未改代码或声称协议缺口已关闭。

公共3D/F输入/单次F语义不变。产品AB/CD双输入按008 FaceEvidence键配对并经独立worker实际调用，不能用公共height/F角色冒充缺陷检测。首批媒体持久引用及后批容量共用既有所有权机制；产品capture/light/algorithm profile必须实际消费。公共高度不自行推定对象/面/槽位。

统一来源：REQ §7/11、宪章3.2.0；执行/结果/证据分别见008 contracts/execution.md、api-results.md、evidence.md。关联实现任务见本功能tasks中的008对齐增量。

### 009联合闭合：当前组件来源由生产者给出（2026-10-02）

本节细化既有真实来源与混合来源矩阵义务（009 FR-016/020—022，EC E04，T034/T035/T039/T043—T046），不增加工艺、页面或新恢复流程。实施者/复核者为Codex；不是客户或其他人员批准，不改历史勾选。

现源码WholeTrayWorkflowOrchestrator按SourcePolicy/Test推定Camera/Light，且硬编码PLC协议版本；IntegratedDetection按固定字符串保存媒体来源。以上不能作为新事实来源依据。共享代码修改前，本节在001/003/008 spec、contracts、plan、tasks实际同步：

- 复用现有ComponentEvidenceSource，新增有限元数据ComponentExecutionOrigin（Source可空、VersionRef可空、Quality可空）；Unknown不自动补默认来源。ICapturePort由实际实例公开CameraOrigin/LightOrigin，IAlgorithmPort公开Origin；不含地址、协议编码或设备内部阶段。
- FileBackedCapture声明Test文件相机/仅配置光源，不能声称真实光源SDK已执行；SimulatedCapture/Algorithm声明实际模拟profile版本；PythonWorkerAdapter声明本次Test独立Worker适配器身份，并保持真实WorkerSession/call引用。NotIntegrated和未给元数据的替身为Unknown，不批准完整来源矩阵。
- DetectionPortResult的AlgorithmOrigin随实际生产者返回并随Completed或有限Pending事实保存；Host派生Pending保留已知失败尝试来源，不因Test目的猜来源。原Source/Quality分类不改写历史，完整来源以本次实际Origin及可关联事实为准。
- WholeTray矩阵的Camera/Light取本次实际capture实例元数据及已保存输入媒体/检测事实；Algorithm取已提交检测事实的AlgorithmOrigin；PLC取已提交stage-action/1的ExecutionOrigin。Host汇总标Derived，不在Application写协议版本常量。缺失/未知来源仍Missing/Unknown并阻断所需完成，不能合成Verified；历史旧payload保持原样，历史无新Origin不推造。
- 实施/验证由009 T034/T035/T039承接生产消费，T043—T047承接持久查询和既有消费者；先补语义正反例（同Test请求不同真实来源、缺失来源拒绝）再改正式生产者与消费者。独立进程证据仍另行验证，文档对齐本身不算实现通过。


### 009 采集完成业务事实对齐（2026-10-02，代码修改前）

本次由Codex执行并核对实际生产者和消费者，不代表客户或其他人员批准。009 T033/T035/T049/T050继续承担实施与运行证据，历史任务勾选不变。
业务的采集完成/释放仍要求原来的实际采集、必要业务保存及通信端完成确认，之后才允许后继动作；通信内部复位与清零仍由原通信协议和wire测试约束。新业务事实使用`schemaVersion=device-semantics/1`、`kind=AcquisitionReleased`，携原当前动作/步骤/epoch关联；不把复位成功码或内部阶段暴露给业务。现有已保存`DetectionResetConfirmed`、`RescanResetConfirmed`原文只供历史读取，禁止回写或补造。
ThreeDAndFRecipeGate的两个输入表达“公共3D/F采集业务已完成”，拒绝原因分别为ThreeDAcquisitionIncomplete/FAcquisitionIncomplete；原先“Handshake”字样不再作为新的业务状态。判据、顺序、必要保存、10秒配方应用预算及后段起点均不变，实际3D/F步骤成功后才传入完成值。
当前生产者为StartPublicPreparation及IntegratedDetectionPort；业务集成断言和summarize-q01-q02-evidence.py按当前语义事实计数(acquisitionReleaseCount)，原始复位/ACK次数及顺序在通信断言/探针中保留。客户页面无新增字段或文字变化。历史报告不被重新解释为新版本通过；实现后须重新取得当前源码证据。

### 010实施定向对齐 A02/A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A02**：边界提供DecodedTrayCode（F料盘编号及真实解码依据；不编码配方身份），FCodePolicy保持唯一匹配，删除TestTrayCodePolicy业务固定码。公共3D/F、单图/融合/E需求绑定实际能力，无Test默认能力/版本。F实际IAlgorithmPort.Origin关联Run/Capture/Call并随必要算法事实提交；WorkerSession/ExpectedComponentVersion不能代来源。
  生产/消费与010实施承接：解码/能力注册/实际算法事实→FScan/FCodePolicy/检测→冻结/Worker/F选择；T008/T013/T015/T016/T024。
- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
