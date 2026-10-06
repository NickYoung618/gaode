# 014共同执行与通信消费设计

状态：2026-10-05 Phase 1拟定接口增量，不是代码/PLC交付。共享配方只引用011 RC10，前端表面只引用012 API；本文件定义014实际执行接入，不复制业务模型或raw协议。

## EX14-01 正式入口及scope

人工上料→现StartPublicPreparation公共3D/F→同SqliteRecipeStore一次Match→共同准入/规划/深冻结→现ThreeStageWorkflowExecutor。现RecipeDetectionExecutor旋转NotStarted必须在真实动作端口、机械准入和全部消费者就绪后才删除；只删拒绝不算能力接通。

在现StagePortContracts新增`DetectionExecutionScope(string UnitId,string SlotId)`，DetectionRequest追加`DetectionExecutionScope? Scope`，普通null。Inputs/Plan始终同全盘FrozenExecutionInputs；每scope只查询所属原Sequence/StageId/实体/成员，不BuildExecutable单槽、不重算RecipeId/Version/Digest/PlanRevision。

DetectionPortResult保持Request及scope；Completed只代表这个范围的检测。RecipeExecutionCoordinator/Executor对完整Frozen身份先核再筛SelectedSteps；范围内完整性/ExpectedObjects/算法要求按scope核，不能对scope结果要求其他件，也不能删全盘期望。RecipeSortingMapper.MapAsync沿原签名从detection.Request.Scope核SortUnit或ReturnUnit；普通null走原全盘。

## EX14-02 特殊逐件闭环

按冻结OK号（含原跳过号）依次：本件原槽关联及有效参与→用RotationLoadingGripperId上工位实际取放/保存门→组1本次R到位与实际角→两台实际采集→组2相同要求→共同算法判定/必要结果保存→用SortingGripperId处置当前件→安全位事实→下一件。E如适用只沿既有明确安排，不因特殊加入未确认扫码顺序。

上料在原scope的TransferToRotation步骤消费；Rotate及Capture属于同scope检测；ReturnUnit仅由排序映射消费，不能检测端和分拣端各搬一次。质量OK映射ReturnToOrigin，原槽固定且源为工位当前取料点；普通OK保NoMoveRequired。NG/Pending沿既有明确点/容量/预留规则，不发明目标策略。姿态异常在原槽排除不进工位。

Allocator特殊依据工位实际占据、同件上料腾空原槽事实，固定原槽返回需要相同Origin关联；普通原源占据保护不放宽。IPickCommitPort维持真实pick后、转运前的receipt/window/action/epoch/reservation/digest门；任何必要提交失败留UnknownHeld，不假继续。

全参与scope与排除事实对账后才形成唯一全盘聚合、整盘下料/WholeTrayFinal及人工取盘原门；不拿最后一件Completed代整盘证据。ProductionStageEvidenceRequired等现有生产限制保留。

## EX14-03 现端口最小变化与完成证据

同IPlcStageActionPort.ExecuteAsync原签名不变；PlcWorkflowStage增语义枚举TransferToRotation、Rotate，现Sorting转运请求加`TransferPurpose`（RotationLoading/Sorting/ReturnToOrigin）、`RequestedGripperId:int?`及必要`SafeTarget`；翻面不携选择用途。实际点复用HandlingPoint/现有公共取放；对应stage/fact和真实注册保持一套。PlcStageActionRequest增可空TransferPurpose、RequestedGripperId、FixedPoint? SafeTarget、RotationTarget? RotationTarget；enum合法用途按上述精确名称，历史null省略。特殊加载/分拣必须有合法用途、抓手及safe，翻面禁止以这些值触发选择。DeviceStageContract.IsKnownStage和result switch同步，不用默认分支冒充支持。

Rotate请求用`RotationTarget(double AngleDeg,string StageId,double AngleToleranceDeg,string MechanicalEvidenceReference)`关联绝对角、scope/StageId与已批准安全/容差配置引用；角度单位沿正式R轴合同，不把面号当raw旋转码。回执使用专门`AngleReachedEvidence(ActionCorrelation Correlation,double TargetAngleDeg,double ActualAngleDeg,double ToleranceDeg,DateTimeOffset ObservedAtUtc)`，连同本次可靠到位观察及source作为DeviceActionEvidence的可空角度证据，带本次operation/epoch/到位及ActualR；无正式容差/地址就限制派发，不补默认。

Transfer的StageActionResult.Completed必须核source/target及safeReached证据（位置/关联均匹配）；safe不是目标请求值，不允许用判定或已放料替代。place失败、安全位失败均不记录件完成/不下一件。原总绝对期限/取消/连接代次保持；ExecutionBudget扩工作量纳Transfer/Rotate/Return与必要保存，预算来源沿批准配置，不能每件重开deadline或猜硬件时长。

注册只改现Host Composition/AdapterBindings.cs与Station01Registration.cs、现LatestProtocolStageActionAdapter；不用第二executor/测试分支/目录。

## EX14-04 抓手和R通信边界

业务仅用途/业务ID1或2/角度/目标证据。raw地址、Grab_ID/Active_ID、RotateTargetR/ActualR/RotateStart/RConfirmed及编码只在Signals/SignalCodes/ProtocolDefinition/现PLC适配；原DOCX/XLSX是依据，正式地址空白仍空，不借测试地址批准。

LatestProtocolPlcDevice按当前connection epoch持有效选择；闭环开始ensure一次：有效同号直接沿用，首次/换号/初始化/重启/失效重新建立且本次匹配反馈才动作。闭环中不重复写/核ID，正常保有效选择；清零不表示松手，持料状态另存，翻面不选择。反馈错号/超时/epoch变化阻断动作。

R核本次RConfirmed及ActualR，不增完成信号。013的Pump唯一采样/WaitGroup/预建有界读计划继续，新字段准确进入现扫描及计划键。当前0..63键空间剩余不足，本次设计仅把内部FieldMask/Dictionary/Key统一扩为UInt128（0..127，未知ID仍拒绝），保现SignalId数字且新字段在未用内部枚举值分配，不把内部索引当PLC地址；RequireOwned与所有调用/检查纳迁移。仍准入时预建实际有限订阅集，不穷举128位，不blind溢出或开新poller；现降频策略及性能偏差保留，不重启优化。

## EX14-05 采集、状态与历史

StageId增入FaceResultKey、RecipeWorkload、WorkerTargetIdentity/算法结果关联、CommittedResultProjection。特殊重复组各两相机完成，局部参数经原CaptureRequest进入原适配器；component request proof与设备Applied严格分开。

件事件带scope，件完成不是盘终态；整盘EvidenceMatrix汇聚所有scope事实/排除与最终保存。现查询/通知增加实际origin/cell/regionOrdinal/StageId和return/safe引用投影，012只绑定现区域；历史缺值null不从活动目录补。原HistoricalHandlingEvidence及SpecialExitCompleted真实历史reader保留原含义，不授当前免搬。


014 T007/T008 recovery obligation: arm the existing X acquisition before axis start dispatch; action-scoped Moving latch requires the same epoch and field sampling at or after that axis actual acknowledged response completion. Unsent/stale/foreign-epoch/Arrived-only cannot satisfy acceptance. Clear at exit; current arrival, coordinates, safety, cancellation and original windows remain required. No new sampler. See plan action feedback recovery; actual chain remains pending.


014 T012/T013 projection recovery: sorting-evidence/1 SortingAssignmentInTransit carries PickCompletionEvidence, not DeviceActionEvidence. Parse full action evidence only for its declared completed kind (SortingAssignmentOccupied or RotationReached); committed pick remains Executing, never physical place/safe/whole-tray completion. Keep notification/query live and add the exact persisted pick-shape regression, valid digest/run/plan association and completion rejection.

014 T012/T013: for current frozen3, a declared physical completion kind without its correlated placed/safe or rotation-angle proof remains Unconfirmed; StageEventType.Completed alone must not display a physical action completed. Historical frozen versions retain their original read interpretation. Existing API maps Executing to phase state Running; the new regression must use that established name, not invent Executing.

014 T012 projection: pick/occupied envelopes retain their original scope and transfer purpose by consuming the committed stage-action/1 Started event with the same operation, run/plan and connection epoch. This is an actual operation association, not classification/step inference or a current recipe lookup.


014 T011/T013 scoped event recovery: per-unit calls already carry the frozen DetectionExecutionScope and original detection IdempotencyKey. Namespace wrapper stage-event keys by that scope (unit and slot), including intent/start/completion/retry/pause boundaries. Whole-tray and ordinary scope-null keys remain unchanged. Distinct units cannot conflict at SQLite, and repeated same-unit/same-key/different-content still conflicts. Do not randomize event keys, weaken store idempotency, swallow Conflict or add retry. Existing ThreeStageWorkflowExecutorTests belongs to T013; two-unit component and actual shared representative must cover this.


014 T007/T008 completion recovery: ordinary continuation-02 actual P transaction2 completed queue/I/O within78ms, then Submit resumed about1056ms after arbiter completion; original absolute check rejected1135ms. Remove the scheduler explicit asynchronous completion handoff; publish every normal/cancelled/expired completion outside the arbiter lock, preserving one wire drain and the original final absolute/cancellation check. No extra retry, deadline start change or new executor. Bounded completion timestamp means immediately before signaling, separate from return after any inline consumer. Audit direct transport consumers (only existing PlcSignalAccessor); cancellation/reentry and original deadline tests plus both representatives are required.


2026-10-06 T007/T008 first-fault preservation: a subsequent reconciliation rejection must retain its distinct refusal meaning but must not replace the first actual wire/deadline cause in the business failure latch. Keep the first unusable-connection cause internally, cleared only by the existing explicit reset; current 1000ms origin, epoch, cancellation, no replay and raw exchange journal remain unchanged. Required regression: actual dropped response followed by refused reads retains the original cause; ordinary and special acceptance remain separate. No new business recovery path or diagnostics-as-success.

First-cause classification must also retain the original caller token: elapsed I/O time alone cannot convert an earlier caller cancellation into a deadline cause. This is diagnostic cause preservation only; the original linked token and absolute Check behavior are unchanged.
