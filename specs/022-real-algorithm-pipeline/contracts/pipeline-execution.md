# C022-PIPE：受管检测流水线、关联与预算

依据FR-006至020、Q1=A/Q2=B/Q3=A。以下能力名为拟定内部接口职责，不是生产wire或实现代码。

## 现行阶段适用性

A沿原IDetectionPort.ExecuteAsync的整体完成和同步await，基本受管Runtime覆盖公共/复查3D、F、同步E、单图及必要融合；唯一仲裁/冻结/一次预算/所有权/Host停止监管先完成，不依T017。以下ProductionEnded/对象句柄及批次重叠调度为C设计保留，本轮入口禁止启用。三类额度的逐帧/保存实际占用和转换保留适用A，批次后台预约属C。R3保T010为C的T017前置，不能以A或Test通过跳过真实用途实际就绪。

## 正式执行合同

IDetectionPort扩展为受管管线启动/取得句柄能力；RecipeDetectionExecutor仍为唯一检测动作生产者。句柄至少提供ProductionEnded、AwaitObjectDecision(PhysicalObjectKey)、Completion和资源状态查询/停止管理。现ExecuteAsync的整体完成语义必须保留为等待Completion，禁止返回部分Completed；正式编排迁入句柄后无用途旧内联检测旁路删除，不永久双轨。

ProductionEnded证明该作用域原计划采集及必要机械步骤/同步E/观察/保存已完成，不证明缺陷齐备。Completion只有全部适用对象判定保存、相关算法执行与输入消费者可靠结束才完成真正Detection.Completed。句柄归Run受管登记所有，调用者退出不使后台失管。

普通盘：生产整盘原序步骤 → ProductionEnded → 按SortUnit原序等待当前物理对象完整判定/合法PosePending → 对象映射/安全预约/正式分拣 → 全部任务与保存排空 → 真正Detection.Completed。原3D复查、全盘采集结束、分拣安全条件保留。特殊件：原scope本件上料/两组采集旋转 → 本件生产结束 → 本件完整判定 → 本件分拣/OK回原槽及安全位 → 下一件；不跨件提前上料。WholeTray及ThreeStage阶段保存按事实分别表达，不能提前给Detection阶段写Completed后继续补结果。

运动顺序由冻结RecipeRunPlan决定。缺陷只从无结果依赖的采集/定位/翻放/本件下一组旋转前置中移除；3D/F、同步E、物理对象判定、动作握手及最终完成屏障不删除。不改变抓手选择、动作角度/坐标或PLC报文。

### I2：入口启用前置

T017接入并启用后台重叠以前，必须已有T026的唯一结果仲裁、T027的暂停/取消/故障/关闭及重启资源监管、T031的公共/产品冻结版本传递；并完成各任务最小离线前置验收。前置组件通过受管内部入口在明确Test环境验证，不依赖T017已经启用，不用生产绕过开关。T015/T016提供未启用的Runtime/租约基础；T026依T005/T015，T027依T009/T016/T026，T031依T009/T015，三者不再等待T025/T030。T017依T006/T010/T015/T016/T026/T027/T031；若任何前置证据未关闭则不能启用。T018/T024/T028/T029/T032仍承担完整执行链集成验证，不能替代前置验收。

## 唯一关联与结果接受

任务键至少Run/Tray/Session/Call/Operation/Attempt；业务输入键Object/Member/Assembly/StageId/LocalFace/CoordinateEpoch/Camera/Media/Capture。融合严格两图逐输入键及所需单图提交齐备；单图结果到达次序不用于配对。未齐集合不假造算法事件、不称检测完成。

接受结果先检查当前调用与worker会话、冻结能力/版本/输入映射、合法输出、原截止和唯一终态。持久AlgorithmFact及对象判定后才能消费；Result到达但保存失败保保存阻断。旧/重复/迟到事件可诊断，不能再融合、覆盖终态或批准动作；可靠迟到释放仍消费以回收原资源。

## 有界资源与配图

AlgorithmRuntime扩展产品调用，复用IsolatedAlgorithmCall与LeaseSupervisor；共享真实Worker/模型池的实际占用必须统一计数。每池执行C>0，排队Q>=0，采集预约/等待准入项也有有限上限；不为每个计划项提前创建等待Task，不以适配器私有队列掩盖超限。公共姿态/码关键调用有明确有限准入，未齐融合不得占其执行位。

执行位仅为可执行任务占有，业务超时且执行未知时仍计实际占用；不能借取消释放slot启动额外推理。队列满在下一采集前限制，原已提交任务可消化，恢复容量后仍核原期限和控制准入。

媒体保留额度独立于Q/C。按冻结CaptureStage的真实批采顺序预计算最大未配图保留集合及逐帧MaxBytes，在该批第一张前核定/预约必要待配图额度和后续单图处理准入；当前A整批后B整批不能只留一个配图槽。缺额只限制本批采集，不改成A/B交错或丢首图。配图完成、融合最后消费者可靠结束后释放相应保留额；必要融合取得有限队列准入后才提交调用。不要求整盘所有图都常驻内存。

### U1：三类额度及责任

| 额度 | 取得、转移与责任方 | 释放和失败 |
| --- | --- | --- |
| 采集内存预约 | MediaCapacity/MediaStore按下一次真实采集MaxBytes取得，保原F预留及同时在途采集上限；采集/保存拥有缓冲期间持有 | 缓冲/相机已可靠归还且保存作业不再持有字节后回收；算法持文件不延长此内存预约。失败也须证明缓冲持有结束，未知不能finally归零 |
| 工作文件保留额度 | 在批首按原AB/CD批采顺序预约最大待配图/算法工作集合，生产者持预约；保存后按Run/Media移交排队、单图、待配图/融合和必要转换消费者。引用交接无零持有空档；同文件总工作保留字节按唯一媒体计，消费者分别计引用，不重复计算同一文件 | 最后必要消费者可靠释放或明确未转移的本地终止，且交接不在途，才回收相应工作额度；单图释放不能解除配图引用。保存或派发未知保原管理者和保留额度，已取得部分租约后失败须逐项回收仅本地未转移部分 |
| 持久文件实际磁盘占用 | MediaStore以真实文件/sidecar清单、现有磁盘检查和元数据为依据；采集前有限磁盘写入预约计入可用空间，保存后转为实际文件字节，未用预约差额可退。按原RunMediaQuotaBytes/DataQuotaBytes/MinFreeDiskBytes限制，不改现预算解释 | 文件仍存在就仍占磁盘；InputReleased、Reclaimed或工作额度归还不减实际磁盘计数。保存失败但已落文件的部分仍计清单，提交未知不删除。仅已有授权维护真实删除并核对后或真实清单核对纠正才改变实际占用；022不新增清理政策 |

批次预约只保留工作额度和必要磁盘写入容量，不调用现Reserve为整批同时占采集内存；实际逐帧取得采集内存。优先在现MediaCapacity/MediaLeaseRegistry/MediaStore内拆清计数与有限预约，不新增存储服务或通用平台。副产转换文件各自计工作保留及实际磁盘，但不覆盖原图。业务资源投影记录Media/消费者/持有原因和工作额度；启动实际磁盘清单独立恢复，未证消费者解除的历史文件仍计工作保留。

工作额度满可等待已提交任务消化，但仍核原准入期限；磁盘不足不会因推理结束自动恢复，保持原媒体不足/有限等待和依赖阻断，不删必要图、不丢任务、不改AB顺序、不新增软停/复位。若批次在原顺序下不能满足最小配图容量，采集前明确拒绝该批准入，不能开始后自锁。T016/T029在同一批采定向用例记录逐帧内存峰值、工作保留峰值和保存后磁盘字节；最后消费者结束只降低工作额度，真实文件及磁盘占用仍在。

## 预算

采集容量等待用原采集/阶段有限窗口；不得未入队无限等。任务唯一StartTick在准备本次算法意图、登记OperationIngress原窗口时确定，早于意图保存，覆盖保存/排队/执行。冻结QueueMaxMs、ExecutionMaxMs及总业务上限；排队截止=min(Start+QueueMax,任务总截止,阶段/运行截止)，开始执行后执行截止=min(Dispatch+ExecutionMax,原总截止,阶段/运行截止)。因此执行可以有分段预算，但不能重启全调用预算。公共已有用途预算保持，产品AlgorithmWaitMs从首次任务登记算而非从分拣开始算。

AwaitObjectDecision、E及Completion只等待所需任务原截止的剩余时间，阶段截止不因后台化后移。生产数值依DEP-CAP-06，测试显式Test短预算不得成为生产默认。

### U2：每Call一次释放观察

调用可能已进入适配器且尚未可靠回收时，Run管理者串行接收的以下首个事件固定ReleaseObservationStartTick：匹配本Call/原会话的结果或失败被接收；派发返回普通异常/退出失败导致执行未知；原业务截止被裁决为超时；取消/安全故障控制被受理；Host首次关闭该Call新准入。Result即使待保存也触发观察，不等待AlgorithmFact提交；未匹配的旧/他Call事件不触发。正常暂停、Accepted/Running及单独输入释放不重开预算，也不把业务超时提前。可靠执行结束/输入释放可早于起点，先逐项登记；如果已完全回收就无须再开观察。本地已证明NotDispatched者直接按本地交接结束，不建立引擎释放等待。

公共3D/F复用冻结BusinessMs.WorkerReleaseGrace，产品单图/融合/E/复查复用冻结CostProfile.InputReleaseWaitMs（来自既有RecipeExecution预算）；具体值及用途依据不新增猜测。ReleaseObservationDueTick=StartTick+该Call冻结释放时限；它是资源观察截止，不延长原业务/阶段/运行截止，也不因这些业务截止已过而放弃监管。Run/Call/原WorkerSession/ClockId、TriggerEventId/原因、StartTick/DueTick及对应StartUtc/DueUtc、预算版本/引用保存在ReleaseObservationStarted的pipeline/1生命周期事实和AlgorithmCalls投影。内存先一次性锁定再持久追加，保存未知按原EventId核对且保监管；不能为等待保存而重选起点。

后到Result/Error/Timeout/Cancel/HostClosing只追加各自证据，复用原起点与截止；取消回调最多按原隔离规则请求一次，不重发调用。最早受理事件确定观察窗口，并发事件由同一管理者顺序化；后续旧时间戳不倒改窗口或延长时限。观察到期追加ReleaseObservationExpired/Unknown，记录仍未确认的输入、执行、派发返回和取消回调；占用仍计C及工作额度，原管理者继续消费可靠迟到释放并保持可查询，不周期性新建完整等待窗口、不自动杀进程/重试/复位，也不批准Final。

Host整体退出预算复用Host已配置的有限HostOptions.ShutdownTimeout，不填新数值。组合根在第一次Host停止通知/关闭准入时固定HostShutdownStartTick/UTC和HostShutdownDueTick=Start+该冻结退出时限，记录其来源及待核对Call集合；Station01HostedService.StopAsync消费该同一窗口，重复StopAsync不重开。现StopAsync外部CancellationToken仍是更早的硬等待上界；Token不能提供绝对截止时，不从Token反推虚构Deadline，只记录实际取消观察时间，立即终止阻塞等待。若Host配置缺有限时限则明确配置缺项，不临时按算法释放时限替代。

Host能阻塞等待的时间=min(Host窗口剩余时间,该Call释放观察剩余时间)，且外部Token取消可提前结束；到期记录未回收，既不重设Call截止也不把未结束资源写成释放。第一次通知时已耗尽的退出预算不因此再分配。Host仍存活时保监管；实际关闭后由C022-DATA持久清单承接下次启动/人工核对。新Host不得重开旧Call完整观察预算或将跨ClockId单调Tick直接比较；保原UTC截止/Expired状态及Unknown，只核实可靠证据。

T015/T027实现上述固定语义，T029复用代表超时用例断言超时→迟到Result→取消/关闭时Start/Due不变、无早回收；不另列全部事件排列组合。此起点只是资源观察计时，保持现技术终态、有限Pending和控制处置，不新增业务放行决定。

## 控制与失败

正常暂停按原安全边界停采集/运动，已可靠保存并提交任务继续派发/执行/保存及已提交输入的必要融合，原截止不延长；不为缺图新开采集，不自动恢复设备。

取消/安全故障/Host关闭关闭新算法派发。本地明确未进入适配器者保存取消/未派发事实，收回仅本地持有；已进入或派发未知者请求取消，继续观察原结果/释放/退出。AlgorithmNotDispatchedException只在可证明未取输入、未启动且无Accepted等取得证据时适用。普通异常不得等同未派发。

失败/超时按原已确认规则保存真实技术终态、有限Pending及明细，不伪造成功事件；保存/派发未知不是可任意分拣的Pending。ThreeStage的整段算法超时重试不适用于已提交后台调用，不能重采/重算整段；已有明确且安全的通信重试只在原允许边界内保留。无需通用重试框架。

A真实同步接入同样不得让ThreeStage现TimedOut/Disconnected分支重新执行包含已派发且结果未知调用或未知设备动作的整段Detection。T023必须衔接本消费者：沿原已确认失败/Pending条件及资源未知监管记录，不用新Call/Attempt重新推理或重采覆盖；只有明确未派发且符合原安全通信边界的重试仍按原许可，不能用普通异常当未派发证明。此限制不等待C句柄改造，T029-A的一项代表超时验证实际ThreeStage消费路径派发次数不增加，原Call/媒体/截止及Unknown状态持久可查；不新增异常组合。
A同步受管增量：AlgorithmRuntime.DispatchSynchronousAsync接已持久AlgorithmRequest、TrayId、冻结算法等待/释放/保存预算；返回可异步Dispose的ManagedAlgorithmCall，Exited只证明该Call执行及输入可靠结束，Dispose结束业务消费者并持久刷新生命周期。原消费者保同步结果/汇总/机械await，首次释放起点由结果/错误/原等待截止/取消/关闭一次建立。所有原检测/E/融合/观察直接RequestAsync迁移此同一受管边界；无T017/批次入口。队列/执行仍沿原有界槽，同步检测槽1不新增后台并发。
A重发门禁：DetectionPortResult.NoWorkStarted仅由端口确认没有派发任何采集、调用或设备动作时为true，默认false。超时/断线而缺此保证不得重试整个Detection序列，也不得自动转为可分拣Pending；保持Failed/原UnknownHeld及原资源监管。Test纯未派发端口夹具可显式提供该保证，业务不看测试名称。

A同步释放等待增量：ManagedAlgorithmCall.RemainingReleaseWait读取同一Call已建立的释放观察截止（Host先触发也沿用），消费者在结果后只等待剩余时间；没有已建立起点时不另开完整预算。执行完成可直接通过已完成任务，观察到期仍未知继续监管。

## T041–T044 A消费者细化

公共InvokeAsync结果接收固定一次释放起点，以本Call原剩余WorkerReleaseGrace等待InputAndExecutionEnded后才可返回Success；到期抛出明确资源未确认错误并保监管，不生成可推进的成功回执。实际Start进入前同时核Host准入关闭，注册成功不能代替进入许可。

ManagedAlgorithmCall.Result为单次权威结果任务，原due到达先于有效结果时以TimeoutException结束，取消先受理以取消结束；Result/Failed/Timeout/Cancel/派发错误共用一次终态裁决。有效结果须匹配身份/会话且在原截止内；重复/迟到不更改该任务。LateResult只记录首个匹配的迟到结果/错误供诊断，原有限Pending只能在原超时且可靠释放、原阶段尚有效等既有条件成立时消费，不能作为正常成功。资源Observe先独立接收可靠释放；唯一释放Start/Due不因任何后续裁决/关闭改变。原业务consumer只接权威结果和资源事件，不能借它接受迟到成功；单图/E/融合/复查均消费权威任务。

Runtime.WaitForShutdownAsync由实际Host消费，逐Call等待=min(Host/外部Token剩余,该Call原释放观察剩余)，返回是否实际全部回收；到期只停止阻塞并Flush Expired/Unknown，不EndBusiness伪释放、不移除占用、不重开预算。Host媒体及事实保存可使用整体剩余预算，其成功不等算法资源排空。

T043唯一裁决消费者补充：产品/E/融合/复查直接等待ManagedAlgorithmCall.Result并保原控制Token，不再以另一结果计时器自行判超时；原due由受管裁决固定并可靠结束Result任务。释放等待仍独立核原Call剩余额度及更早阶段边界。
