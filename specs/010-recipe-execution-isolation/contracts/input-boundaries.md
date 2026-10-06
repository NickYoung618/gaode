# 010 输入、能力与环境边界合同

状态：计划中的内部共享合同；尚未修改任何其他功能合同或代码。对应FR-003—007、009、017，AC-02/04/05/06/11。模型见[data-model](../data-model.md)。

## IB-01 统一语义加载与校验

保留IRecipeCatalog及目录查询职责，JsonRecipeCatalog负责文件schema、字段/版本解码与路径解析，随后调用唯一RecipeDefinitionValidator。替代输入方式直接产生同一RecipeDefinition并调用同一校验器，不复制ReadRecipe业务条件。

| 现输入内容 | 边界转换后的含义 | 共同层仍必须完成 |
| --- | --- | --- |
| virtualFixedProfiles/profiles、采集参数JSON | CaptureProfile及AlgorithmRequirement | 参数/版本完整、与步骤及能力合同匹配、冻结 |
| resolvedDetectionTargets及ByFace/ByFaceRound | CoordinateDefinition集合 | 按用途核对配置与对象/面/槽身份、单位/基准/范围；检测XYZ取配置，必检恰好一次 |
| 对象/点位配置与来源；旧heightBinding/TestHeightOffset仅历史 | 共同语义的用途坐标及来源依据 | 首次3D只提供已确认观察/F XY，检测XYZ来自配置；旧测高公式不是新执行前提，不建立执行兼容旁路 |
| resolvedFlipPosition、resolvedRotation、resolvedSortingTargets | HandlingTargets，明确自动/人工、实体/姿态/出口/目标 | 共享动作不重复、安全/占用/容量/预留及必要保存门禁 |
| TestEligibleSlots、simulationOnly、purpose | ApprovalScope、用途与来源分别记录 | 准入核允许范围；工序不检查测试身份 |
| virtualExactPayload/exactPayload、TEST-TRAY类原码及固定配方映射 | DecodedTrayCode：实际料盘编号与解码证据，配方身份由唯一匹配取得 | F唯一、结果合法、场景/版本及选择一致；不以原码字面量选流程 |
| 图片路径、清单、Worker可执行文件/参数 | 相机/算法适配器配置 | 业务只接受端口结果及事实；路径不进入RecipeRunPlan或DetectionRequest |

文件解码可拒绝坏格式，不能替共同层隐藏错对象/错测量/缺必检。保留独立语义非法输入验证，证明绕过文件解码也会被共同规则拒绝。旧文件与替代格式各有解析样本，但只有一份业务校验代码。

历史目录可继续查询范围外或未批准条目及原因；“可读取”不等于“可执行”。Common validator返回稳定业务问题，AdmissionDecision补充环境批准/能力问题。目录不可执行原因不得用默认坐标补齐。

## IB-02 坐标与测量

CoordinateResolver只读取明确字段和可信测量事实，顺序为：

1. 核对Run/Tray/Recipe/Plan、实体或成员、物理槽、阶段/面/测量轮、采集/调用及提交引用。
2. 核对单位、基准、坐标系与配置版本；F XY读取本次首次3D定位，检测XYZ及取放目标按用途读配置。
3. 当前011检测不强制MeasurementOffsetBasis或旧3D高度；无合法定位/配置时拒绝依赖动作。旧测量能力仅在确认仍有实际用途时保留，否则按清理义务删除。
4. 检查合法范围及每步目标唯一性，冻结数值与全部依据。
5. 执行前继续核当前对象/状态/安全与既有运动准入；来源或配置合法不等于设备已完成。

目标公共合同的Source不再是Test/InjectedXYZ等三字符串白名单；改由ResolutionKind、来源事实与批准依据分别校验。保留真实Test来源和不透明引用，禁止业务解读引用中的目录、fixture字段或测试用例号。物理索引仍是业务身份，不把协议偏移/握手映射传回业务。

## IB-03 绑定能力与设备

现CapabilityRegistry/Station01Policies扩展本次需要的检测、融合、E解码绑定。配置引用已注册兼容能力；绑定检查业务用途、输入数/身份、参数版本、结果合同、批准用途及具体提供者版本。不得配置任意脚本到业务执行器；Worker命令由既有进程适配器配置管理。

正式Host总是注册RecipeDetectionExecutor。环境可以替换ICapturePort、IAlgorithmPort、设备端口实现、坐标提供及文件解码方式，不能注册整段默认成功检测。运行模式可限制“缺什么能力”；不能据模式选择下一工序。FullSimulation缺适用动作能力时明确受限，不回退SimulatedDetectionPort。

注册兼容但运行时算法暂不可用，与根本未注册/不兼容不同：后者在准入拒绝，前者仍按现有有限重试、Pending与安全处置规则执行。真实CameraCaptureAdapter仍拒绝DetectionCameraSettingsSdkNotIntegrated；本次不接真实SDK。

AuxiliaryHandlingRequest传CoordinateEvidenceReference替代TestSourceReference；辅助模拟通道本身的Test用途准入保留。通信层仍独占地址/原始码/握手/协议槽映射，不因新目标合同泄漏009禁止内容。

010替换演练的第二算法实现选定为[research R10](../research.md)的ContentSampleWorker/1：独立进程从实际PNG样本内容计算Height/F原码/单图与双图结果，环境只绑定其脚本与版本化测试配置。复用PythonWorkerAdapter/Supervisor/Codec，不转调旧Worker结果生成。Host核并冻结能力、参数与实际启动产物身份；当前station01-worker/2.0的Execute没有ParametersVersion/CapabilityId/Version字段，不能把Host绑定当作已发给进程的参数应用事实。本次不扩wire，第二实现所需threshold来自明确启动配置并记录摘要；共同业务不解析样本块或此配置。实际Origin与执行脚本/配置、WorkerSession、CallId相互可核，不能只沿用固定适配器名称证明替换。启动配置仅用于接入第二实现，接受/拒绝见VG-07.1。

## IB-04 来源、批准、准入和采集事实

| 概念 | 谁提供 | 谁消费 | 禁止混用 |
| --- | --- | --- | --- |
| SourceFact | 实际设备/采集/算法生产者及已存证据 | 共同保存、结果/媒体/诊断投影 | 不从运行模式推断实际来源，不授权工序 |
| ApprovalScope | 版本化批准配置 | 启动/步骤依赖的准入 | 不把Test批准用于生产；Real来源不能替代批准 |
| AdmissionDecision | 同一准入服务核当前需求/绑定/批准/预算 | 正式启动及适用设备动作前检查 | 只允许或拒绝，不返回另一套业务服务 |
| RequestedSettings | 冻结CaptureProfile | 相机请求及请求事实保存 | 不代表实际物理应用 |
| CorrelatedCaptureFact | 相机适配器对当前请求/采集/epoch返回 | 共同关联校验、保存、历史查询 | 不能由共同业务填固定physicalSdkApplied或推断SDK已应用 |

固定图片适配器应记录实际图片/摘要/媒体来源、相机/光源提供者及重放事实；它只选图并核部分设置时ApplicationState=ConfiguredOnly，ActualSettings可缺。模拟设备如确有模拟参数应用事实，也只能报告该模拟范围，不冒充物理SDK。未知来源/参数保持Unknown；缺少动作所需可信依据时限制对应动作。

MediaRef用途由运行上下文投影；历史无关联时不可反推Real。原source枚举值保持。F及公共算法调用须从本次实际IAlgorithmPort.Origin获取来源，关联当前CallId并随AlgorithmFactPayload或明确关联的SourceFact真实保存；WorkerSession及ExpectedComponentVersion不是实际来源。移交只能消费该已提交事实，缺失保持未知，不从模式/预期推断。单Source及多组件来源处理见[共同执行CE-03](common-execution.md)。

## IB-05 配置批准与预算

公共准备先冻结公共点位/3D/F能力和预算，不借尚未识别产品解释公共坐标。F唯一后冻结产品语义输入、适用能力/目标和计划。运行编辑或替换输入只影响新轮。

Test/Production批准范围、允许槽位及能力移到准入，不能留在RecipeRunPlanner的工序选择。生产预算、现场坐标/标定/能力未获批准时继续拒绝相应入口，不以Test值兜底。

ExecutionCostProfile封装当前批准语义额度及来源，并携本轮BusinessBudget的Id/Version/Digest及成本版本。环境/批准边界按这份冻结预算形成OrdinarySortDeviceAllowanceMs、UnloadDeviceAllowanceMs等语义额度；共同公式仅按原工作量累计这些额度及原保存/人工等待项，不解释PlcIo/PlcPoll或17/16通信计数。不得用默认预算算出的额度套用另一版本。迁移必须独立核验同一合法配方/预算下Detection/Unload/Sorting三阶段起点和截止与原批准结果完全一致。已批准配方应用10000ms独立总预算照旧，不从I/O数量推算，也不扩大为生产批准。替换演练冻结此profile和期限规则。

## IB-06 边界完成判据

文件shape不再出现在共同业务可达的解析/条件表达式；相同语义输入经两种转换后，共同校验、目标和计划含义一致。等价设备结果经同一执行器产生相同必要行为/保存；独立预期不得调用被测规划器生成。负例必须保留错样本、错对象/面/槽、缺目标/批准或能力代表。

仅改名称、把JSON藏入嵌套DTO、返回预编排步骤、固定测试默认值迁到“公共帮助类”，均视为未完成。职责判定覆盖这些帮助代码。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。
