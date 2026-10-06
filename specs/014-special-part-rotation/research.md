# 014技术研究（Phase 0，2026-10-05）

基线为E:/dzk/gaode-1当前已集成源码，宪章9.0.0。三个只读研究分别核共同数据/保存、共同执行/通信和012导航；本轮无代码、构建、测试或数据库操作。来源原件/摘要见basis-receipt，当前源码摘要见plan-design-receipt。设计选择已收敛；现场输入不是未解的软件架构选择，而是具体局部执行依赖。

## R01 稳定格位与物理实体

Decision：采用共同合同RC10的CellId+区域关联；区域号只由行列投影，OK按号排序；PhysicalSlotIndex由明确版本化映射，不计算。Positions只含OK参与对象。looseGroup成员分别ForObject，assembledEntity部位只ForDetection而整体ForObject。

Rationale：RecipeContracts.cs的RecipePosition当前无布局；Validator将物理槽限定Capacity且Planner沿Positions数组，均不足。ExecutionInputs.ForObject与REQ11.1已经区分成组独立件和半成品整体，不能用一个UI导航改变搬运实体。

Alternatives considered：显示号作身份、勾选顺序检测、row*10+col映射3D、把部位当独立搬运件均拒绝。固定100格仅UI/配方格位，不承诺硬件100槽。

## R02 唯一字段和历史读取

Decision：扩现RecipeDefinition/ExecutionInputs，正文4/记录1.5/新冻结3；沿唯一serializer/identity/validator/store/catalog。共享工位Place/Pick，角度继续Stages；OriginPutBack独立用途但目的身份固定原CellId。历史2/3与冻结2准确读取，缺布局/抓手不生成；旧编辑需明确布局和必要关联。

Rationale：当前RecipeDefinitionSerialization正文3/历史2、SqliteRecipeStore记录1.4已真实集成；完整JSON两表无需新的业务库。现RotationTargets各Object挂Entry/Poses/Exits会重复工位并留下任意OK出口。新增字段自然纳入共同摘要并补深复制。

Alternatives considered：复制012私有模型、另存布局文件、历史补连续号/默认抓手1、两个角度来源或任意OK目标都拒绝。修改nullable物理槽必须承接全部编译消费者，不能补0绕过。

## R03 保存可用与执行准入

Decision：同一Validator内区分保存结构校验与现场执行检查；有效点位/参数/类型和明确原槽关联保存时完整，设备在线/正式映射/安全校准不成为保存门。If-Match沿ExpectedVersion，真实SQLite提交后可见，F一次匹配深冻结。

Rationale：ValidateForSave目前调用ExecutionIssue，不能继续让未齐现场映射阻塞无设备保存。RecipeAdmission与StartPublicPreparation已有执行检查，需要按新布局核覆盖参与者。正常保存不提升Approval。

Alternatives considered：前端复制校验、API发假Saved、固定目录回退、跳过唯一性/版本/生产门均拒绝。

## R04 一个全盘快照，特殊件范围调用

Decision：在现ThreeStageWorkflowExecutor内按OK序进行特殊scope循环，复用现Detection/Sorting/Allocator/公共取放端口；普通保持全盘阶段节奏。DetectionRequest追加可空Scope(UnitId,SlotId)，Plan/Inputs永远是同一全盘Frozen；件完成不等盘完成。

Rationale：Planner有TransferToRotation/Rotate/ReturnUnit但DetectionExecutor拒绝旋转，SortingMapper只识SortUnit且过滤OK，ThreeStage先全盘检测再排序，单删拒绝不会形成闭环。WholeTrayWorkflowOrchestrator目前最后Detection Completed事实不能用于逐件后全盘证明，须汇聚全scope事实。

Alternatives considered：第二executor、每件BuildExecutable/重算PlanRevision、前端执行计划、旧specialExits免搬、最后一件代表整盘均拒绝。新增能力只通过现共同分支/语义端口，不建脚本策略平台。

## R05 同组重复及动作真实完成

Decision：既有StageId贯通融合/工作量/worker/投影，四采集独立；特殊OK ReturnToOrigin与普通NoMoveRequired分开。Transfer结果保picked/placed/safe三事实，真实Pick提交门仍在转运前；缺safe或返回失败不能下一件。

Rationale：FaceResultAggregator当前key缺StageId，同AB同epoch会DuplicateFaceCamera；WorkerTargetIdentity和CommittedResultProjection同样有覆写风险。Transfer已有实际safe动作但Completed只核src/tgt；必须承接完成门而不是放宽。

Alternatives considered：增加CoordinateEpoch来伪装第二组、质量OK替代回放、放宽目标占据、取消保存门均拒绝。特殊原槽只有本件已上料腾空事实才可预留返回。

## R06 通信与013保留

Decision：抓手选择有效性归LatestProtocolPlcDevice当前epoch；闭环ensure一次，同号有效沿用；失效重建、本次反馈授权。R用专门绝对角反馈事实。复用Pump唯一生产者/WaitGroup/预建读计划，新增有限字段不另建轮询。

Rationale：只读协议§1.7/2.2/2.4/3.2/4.2及信号表行51/52/54/62/77/85给业务及raw类型，但地址空白。PreparedPlcReadPlans目前bit key只接受0..63，现最高62；本次选择仅扩大内部mask/key为UInt128，保原SignalId值并登记所有消费者/边界检查，不能盲加六字段溢出/豁免，仍只预建实际订阅而非全子集。013现降频、单源和性能偏差保留。

Alternatives considered：业务层raw字段、测试地址冒充生产、反复读取抓手ID、独立高速线程、用Z/面码存R反馈均拒绝。不在本设计编造角容差/固定取料角/安全参数。

## R07 012最小导航预览

Decision：DUI-02在当前格详情内选成员并沿原面/相机卡片；DUI-03同位置选检测部位但整体取放仅一处。独立HTML仅审查导航，参数空白，示意名字不成为业务默认；两项保持待确认。

Rationale：确认稿只展示特殊件，但用户要求保留所有合法普通/成组/半成品能力。不是任意技术编辑器，不能据模型字段扩UI。

Alternatives considered：新页面、成员生成平台、部位各自机械点、复制全部嵌套模型均拒绝。

## 局部外部输入

DEP-014-01正式raw地址/型号承载限制正式通信；02实际格位↔3D槽限制定位产品动作；03安全、取料角/容差限制对应运动；04真机相机/光源映射与应用证据限制硬件Applied结论。无设备的界面与完整保存设计可完成。DUI-02/03导航表达仅等待UI审查，不重问既有业务，不重启013研究。


## R08 本轮新建来源/编辑与验收输入收敛

Decision：新建请求显式InspectionKind/场景，精确同tuple来源及SourceRecipeId；沿同SQLite准备实际共同来源，不建模板库。编辑中间态JsonElement保未填null，完整保存仍严格4；同面不同组的编辑必须携StageId。唯一特殊代表至少两件实际OK各返原槽/safe，重复组必选AB/AB同链或既定CD/CD组件。

Rationale：当前editor-draft只Model+UnitKind且无效SourceRecipeId会被忽略；当前editor-layout按Material/LocalFace会遇两组冲突。正文4未填中间态不能直接绑定非空数值RecipeDefinition。原V14-03只说两参与件/可并入重复组不足证明两个OK返回。

Alternatives considered：普通来源补特殊、第二模板库、补0绑定中间态、取第一Stage、两件以NG结果抵原槽回放均拒绝；不增加同义链或组合穷举。
