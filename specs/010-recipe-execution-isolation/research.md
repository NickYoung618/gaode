# 010 技术研究

日期：2026-10-02。范围：[规格](spec.md)的18条FR、12项AC、8项SC。方法：读取现行需求、合同、正式调用/装配、测试、配置及脚本；本轮没有执行构建、测试、设备、数据库或Git操作。以下“确认”仅表示静态依赖与设计决定，运行状态全部未验证。

## 研究结论与依据

### R01 唯一共同执行，复用已有业务

**决定**：将 [IntegratedDetectionPort](../../backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs) 的有效业务迁入 Application 的 RecipeDetectionExecutor；正式 IDetectionPort 只绑定此实现。保留 RecipeRunPlanner、RecipeExecutionCoordinator、FaceResultAggregator、ThreeStageWorkflowExecutor、WholeTrayWorkflowOrchestrator、RecipeApplicationCoordinator 及设备/采集/算法/保存端口。逐段提取输入解释，禁止平行创建 Production 执行器。

**依据**：[StartPublicPreparation](../../backend/src/Gaode.Application/Station01/StartPublicPreparation.cs) 已完成公共3D/F、计划、绑定及移交，却由 externalVirtualPlc 包住后段调用；[AdapterBindings](../../backend/src/Gaode.Host/Composition/AdapterBindings.cs) 根据模式、图片和Worker配置替换整段检测。Integrated 已实际执行定位、采集、单图检测、同面融合、E、自动/人工换面、特殊旋转及保存，重写会重复有效工艺。

**排除方案**：复制两套执行器、仅改类名/目录、保留模式开关选择整段服务、脚本另调后段入口。正式缺能力应拒绝依赖动作；上层隔离测试仍可在测试工程使用局部替身。

### R02 一次语义转换，一个共同校验器

**决定**：保留一个 JsonRecipeCatalog 文件入口，把格式解码与业务校验分开。文件解码器产出 RecipeDefinition、坐标定义及环境绑定；RecipeDefinitionValidator 唯一承接组成、路线、必检目标、面序、E、物理实体和容量规则。目录展示与可执行校验共用它的结果，审批不足的历史条目可读但不可派发。RecipeRunPlanner 只消费共同校验通过的语义定义，执行前必须另有准入结果。

**依据**：[JsonRecipeCatalog](../../backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs) 混合文件schema与共同规则；[RecipeContracts](../../backend/src/Gaode.Application/Recipes/RecipeContracts.cs) 的 ProfilePayloads、PositionPayloads、RecipeDisposition.PayloadJson 被下游继续解析。TestEligibleSlots 是环境批准范围，不是工艺步骤规则。

**排除方案**：第二套Real加载器、把原JSON套进DTO、环境自行生成全部计划或提前算出测量Z。旧JSON字段只留格式边界，共同业务不得再识别其拼写或fixture结构。明确的语义缺失必须仍由共同校验拒绝。

### R03 坐标来源与共同计算分开

**决定**：环境提供带身份、单位/基准、版本及来源的坐标定义；共同 CoordinateResolver 关联本轮测量，执行测量值加偏置、范围核验，生成检测/换面/旋转/分拣目标。批准固定Z作为另一种有明确批准依据的业务输入，不是缺测量时兜底。

**依据**：[PublicPreparationHandoffV2](../../backend/src/Gaode.Application/Station01/PublicPreparationHandoffV2.cs) 的样本、对象、面、测量轮次、单位及范围规则有效；其 resolvedDetectionTargets/ByFace/ByFaceRound、TestHeightOffset、testSourceRef 解析不应留在共同层。[ThreeStageWorkflowExecutor](../../backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs) 的分拣同样混入 simulationOnly/Test 文件限制。008现行流程使用初始测量，翻后重扫不获准。

**排除方案**：删除来源核验、允许任意字符串替代可信坐标、环境直接给最终Z而绕过共同测量关联、用软件序号代替物理槽位。已有 protocolSlotIndex JSON 名称仅由文件边界转为物理槽身份，不恢复009已移除的协议语义。

### R04 能力需求、具体实现及实际采集事实分开

**决定**：复用现能力注册/绑定设施，以 SingleDetection、FaceFusion、EDecode 的用途、输入数量/身份、参数版本及结果合同绑定具体能力。共同业务转交冻结绑定，不判断 detection.test、test-detection/1、TestCamera 或Worker命令。FScan 使用已绑定的码解析边界，FCodePolicy 保留唯一、格式、场景/版本及所选引用一致性。

**依据**：当前 Station01Policies 尚无检测/融合能力绑定；Python Worker 按role及单/双输入执行，可复用 IAlgorithmPort、PythonWorkerAdapter、WorkerProcessSupervisor。TestTrayCodePolicy 固定映射仅被FScan及两个契约测试消费，映射应由版本化测试输入配置承接。

CaptureEvent 目前没有参数应用回执；Integrated 固定记录 physicalSdkApplied=false。[FileBackedCapture](../../backend/src/Gaode.Infrastructure/Simulation/FileBackedCapture.cs) 只核对部分请求字段并按role/camera选图；[CameraCaptureAdapter](../../backend/src/Gaode.Infrastructure/Devices/Cameras/CameraCaptureAdapter.cs) 明确拒绝未接入的DetectionSettings。设计增加关联采集的来源/设置应用事实：固定图片报告重放及 ConfiguredOnly，真实SDK未接入继续受限，未知不推断Applied。

**排除方案**：建立插件平台、给能力缺失填测试默认值、把请求参数当实际应用事实、以算法已注册推断实时就绪。注册/合同不兼容在准入拒绝；运行时算法暂不可用仍按现行有限重试/Pending规则处理。

### R05 来源、批准及准入三种事实

**决定**：SourceFact 表示实际生产者；ApprovalScope 表示配置获准的用途/范围；AdmissionDecision 表示当前输入是否可执行。Test/Real识别合法存在于来源、批准和准入边界，不能选择工序。删除 AcquisitionCoordinator 在来源Unknown时回退 Simulation.Fixtures.MediaSource 的行为；MediaRef.Purpose 从当前运行用途投影，不固定Test。WholeTray中的用途/批准与Host来源推断拆开，来源矩阵逐组件记录。

**依据**：[ComponentEvidenceMatrix](../../backend/src/Gaode.Domain/Station01/ComponentEvidenceMatrix.cs) 已支持各组件独立来源、版本及Unknown状态，无需发明一个“全Real”总标签。StageHandoffBuilder 当前从Simulation配置推来源且未知默认Test，不能据此授权或证明实际执行。FScan当前AlgorithmFactPayload尚未完整保存实际Origin；必须新增本次IAlgorithmPort.Origin与CallId关联的真实保存，移交再消费该事实，不能从WorkerSession或ExpectedComponentVersion推断。暂停/完成记录中的固定Virtual或按用途推Host来源也按实际生产者/操作者事实承接。

**选择**：不增加或重排现有来源枚举，保持006前端枚举投影。新产生的v2移交单字段 Source 取已提交F识别事实的实际来源，只说明这条来源事实；其他组件由 EvidenceReferences 对应已提交事实表达，不能以单字段概括混合链。必要来源未知时记录Unknown及受限原因，不强塞默认枚举形成可续接移交。其语义须按计划中的共享合同表定向对齐。

### R06 保留活动v1输入及已批准期限，删除非严格业务旁路

**决定**：context/1.0 与2.0是同一入口的既有输入版本，均转同一共同校验、目标、计划及执行路径。2.0保留 ExpectedRecipeRef 与F一致性；1.0在F唯一后取得合法配方再准入。删除 StrictRecipeExecution、零坐标占位、跳过运动/聚合/保存等执行特权。010单配方验收使用2.0。

**依据**：frontend/src/runtime.js 仍接收1.0 Test/S1/P01；simulate-station01-load.ps1、verify-latest-plc.py、run-009-process-case.ps1 仍生成1.0。009 [business-device B03.2](../009-plc-protocol-isolation/contracts/business-device.md) 及保留的FR-035/037、AC-022/027明确：v2绑定前已有后段期限；v1移交后首次生成Detection期限；独立绑定仅沿用真实已有期限，不创造窗口。这些义务虽不重开009完整动态验收，仍有效。

**期限决定**：在入口解析时形成已批准的期限策略引用，不把版本/环境身份传进工序选择。v2保持原冻结起点及数值；v1保持原handoff后Detection首建起点及既有后继期限规则；任何已存在期限不刷新。配方应用意图提交后、端口排队前唯一建立其总窗，当前已批准Test值10000ms，Production未批准拒绝，始终取已有更早适用期限。

RecipeExecutionBudget 的采集5000ms、算法10000ms、复位5000ms及现累计项移至批准且冻结的成本输入；共同层按语义工作量计算，保留现数值/计数/截止。17/16次I/O等内部估算不作为新业务字段/公式泄漏，边界按本次冻结BusinessBudget版本/摘要生成OrdinarySortDeviceAllowanceMs、UnloadDeviceAllowanceMs等额度，共同公式不再解释PlcIo/PlcPoll或通信计数。不得将默认预算的额度用于另一版本；独立核验同输入的新旧三阶段起点/截止完全一致。本修复不调大预算，人工等待来自typed输入，配方应用10000ms独立预算不按报文/握手数量计算。

**排除方案**：把所有1.0消费者误判无用而删除、把所有后段期限提前或推迟到统一时刻、留Strict=false兼容通道。保留既有输入不等于保留不完整输入可执行；不合法旧输入明确拒绝。无需变更010范围或让用户再次决定工艺。

### R07 持久化与历史读取保持事实

**决定**：将新的语义输入及摘要纳入现有RecipePlanAndBindingIntent版本化负载；连续链沿RecipeApplicationCoordinator→RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite保存，并由ITraceQuery读取，保持实际回执类别。其他阶段事实及独立绑定保留其原IStageEventStore链，不替换连续链存储路径。当前消费者通过Run/PlanRevision/RecipeRunPlanReference核验，不能仅靠内存传递。v1首次期限也取得真实提交后才进入检测。保留v2移交字段/序列化摘要算法，不给record追加字段破坏旧摘要。

**依据**：TraceQuery.GetCommittedV2Async 同时核查行、Writes与payload摘要；改record序列化会让历史有效数据失效。独立 /recipes/plan、/recipes/bind 仍读取v1/v2，独立绑定不重建handoff、不授权重复产品动作。RunMediaCatalog/DeviceEvidenceHistoryReader仍读历史复扫媒体及运动事实。

**排除方案**：历史回写、迁移整库、通过查询重建当前执行授权、用旧报告或旧绑定事实代替本次回执。旧payload只作已存事实读取；没有新语义输入的旧运行不能恢复旧动作。新负载使用各自既有运行/阶段保存链，不新增数据库表/枚举阶段升级工程。

### R08 复用门禁入口，但补足拒绝能力

**决定**：计划给 scripts/verify.ps1、verify_entry.py 和 runner 增加固定 RecipeExecution010 profile；工作流010验证也调用同一profile。所有项目验收profile在公共入口无条件执行VG-01.1轻量集合L，再执行各自必需项；010完整profile复用L，不重跑同样检查。复用Rules的Roslyn、脚本解析、TRX、必需清单及迁移审计设施，新增有限010规则及负/正例。按正式启动、共同业务符号、正式DI和间接帮助代码的职责闭包检查，不能仅按路径或注册角色豁免。

**依据**：当前 verify.ps1 仅WaitSeconds，runner硬编码009清单/solution构建；直接沿用会扩大本专项。ProtocolRepositoryBoundaryTests已有语义编译/链接源检查，ProtectedRole单按路径不能证明移文件后仍覆盖。validate_required_ledger已拒缺失/重复/漏执行/Skip/身份不符，但只遍历expected，额外TRX-PARSE错误不自动失败；010必须把解析及扫描有效性列为必需项，并让任意解析错误总失败。

**持续接入依据**：runner._run_verify形成结构化passed；step('assess')和step('finish')分别判断可收口及写completion，step进程退出0本身不代表验证通过。verify_entry才将passed映射退出码。verify-009-protocol-isolation的BoundaryMinimum/SelectedCasesOnly还可经boundary_minimum/protocol_isolation直接聚合，不能漏掉。上述最终点共用同一当前L凭证核验；缺失、旧attempt、摘要不符或仅passed布尔均拒绝。现source_digest已能直接枚举文件内容，不需Git；选择每轮无条件L，避免新建比较基线及变更检测框架。L只做源码/检查器/报告数据验证，真实Host/设备/数据库/整盘仍在010完整集合。

**排除方案**：无人调用的独立脚本、仅给010profile加检查、仅看step退出码、依赖本工作区不存在的Git、全仓搜索Test、从本轮发现列表反推必需集合、开发通用静态分析平台。具体入口、L与C01—C03最小接线正负义务见[验证合同VG-01.1](contracts/verification.md)。009原有效保护和旧结论不变。

### R09 完整主链证据与组件证据分开

**决定**：最低一个配方通过现正式HTTP入口、独立虚拟PLC和独立Worker、真实采集/SQLite/媒体、既有授权取盘，到最终提交。正式装配测试及真实组件测试保留其各自义务；不得冒充整链。

**依据**：FormalHostComposition注释明确PLC在测试进程内；SingleFace直接构造检测类；CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite仅到AwaitingManualRemoval，并断言FinalOutcome=None及无FinalUnloadCompleted。旋转/多对象许多场景同样未到最终取盘；四面组代表已有完整取盘终点，但不能因此替所有路线背书。

**排除方案**：把测试绿灯或handoff存在当全流程完成。冻结后等价替换至少覆盖素材、算法实现、坐标提供、解析及装配依赖，允许组合而非每类整盘。合法结果变化可与受影响组/整体/分拣代表合用。

**基线决定（ARC-002修订）**：统一按VG-05.1：先准备输入、替代实现和独立预期，B通过后冻结，再执行等价完整链E与共享代表S，最后V07核T。B包含V00/01/02/03/05及V04除S的全部必要代表；S明确选整体NG/Pending组件，B前准备、冻结后执行一次兼V04/V06，不能同时作为B执行前置。T按case/run去重覆盖全部最终义务；保持正常基线和等价替换两条完整链。业务/合同/预期变化废止旧替换结论，重建受影响基线和新完整B/冻结；未受影响证据只有经明确影响核对才可保留原身份引用。

### R10 第二算法测试实现：媒体内容样本Worker

**决定**：计划新增有限独立测试进程 `scripts/010-content-sample-worker.py`，实现名 `ContentSampleWorker/1`，使用Python标准库和现`station01-worker/2.0` NDJSON协议。它解析真实PNG中的命名tEXt块 `gaode010.sample.v1`（UTF-8内容以ASCII JSON转义存储），从其中的测量、原码和数值样本计算结果。它是明确标Test的受控输入算法，不是生产视觉算法；仅覆盖选定Q02/CD/P01/P03代表的Height、FDecode、Detection单图与双图融合，E关闭，不为它增加新工艺能力。

**实质差异及依据**：现 [virtual-station01-algorithm.py](../../scripts/virtual-station01-algorithm.py) 读取并核媒体长度/SHA后，按配置seed及摘要随机产生高度、读取manifest的fCode、从disposition列表/目标override选择检测结果；双图沿同一结果选择逻辑。第二实现不使用这套结果生成：

| 能力 / Q02实际调用 | 第二实现处理路径 | Host接收结果 |
| --- | --- | --- |
| Height / 1次 | 读取当前PNG样本块的sample-a/b微米整数及基准，按1000换算毫米；缺失样本不填默认高度 | 既有heightSamples/sourceElementId/value/unit/datum |
| FDecode / 1次 | 从本次F媒体样本块解码实际原码，不读旧manifest.fCode | 既有rawCodes，后续映射/F唯一/配方一致仍由边界与共同业务完成 |
| Detection单图 / 4次 | 实际读C或D输入的valid与score，按第二实现版本化配置threshold计算：有效且score≥阈值为NG，有效低于阈值为OK，无有效依据为Pending；缺失/坏格式按现错误合同拒绝 | 既有Detected/disposition/inputMediaIds/inputDigests |
| Detection双图 / 2次 | 重读两份输入，核wire已有objectId/localFace/heightRound及C/D身份、媒体/调用关联，再计算有效score最大值；有NG为NG，否则无效依据为Pending，其余OK | 既有Fused结果；不返回预设融合状态或下一工序 |

现FileBackedCapture原样读媒体且当前报告png，因此这次用真实PNG容器；不宣称端口只能支持PNG。样本块只由第二进程解析，不进入公共业务DTO；业务仍接收媒体引用及语义结果。第二进程核受控路径、实际字节长度/SHA与当前输入身份，按现协议真实发送Accepted、Result、逐输入InputReleased，保留input-release/退出义务，不把取消当释放。

**绑定与复用**：复用现WorkerProcessSupervisor、WorkerProtocolCodec、PythonWorkerAdapter的进程传输/结果解析/租约设施，环境启动绑定选择第二脚本及其版本化测试配置；共同执行器不变。允许复用标准库、协议结构、媒体读取/SHA和日志工具，禁止import/runpy/subprocess转调旧结果生成实现、复制其随机/target override逻辑或仅增加转调包装。当前WorkerProtocolCodec.Execute不把ParametersVersion/CapabilityId/Version送上wire；本次不扩协议。Host能力边界核并冻结这些配置，第二脚本从已登记启动配置读threshold；不能声称wire已经传入参数。绑定描述和实际启动脚本/配置摘要、PID/WorkerSession、逐CallId事实关联，IAlgorithmPort.Origin如实标Test/ContentSampleWorker/1；不能仍以统一PythonWorkerAdapter/1名称作为第二实现证明。

**B之前确定等价输入**：新建本轮受控环境输入（实施阶段），不改008旧fixture。基线仍用旧Worker，但明确把heightMm范围定为[11.0,11.0]（原Q02范围内），detectionDisposition=[OK]且无target override，F原码由已审阅输入指定。第二媒体sample-a/b分别为11000微米，C/D score为0.1/0.2、valid=true、threshold=0.5；二者高度均11mm、四单图/两融合均OK，目标按既有P01→sample-a/P03→sample-b及偏置100独立得111mm。新解析/F映射演练的新原码/合法配方标识也在B前固定，F媒体解码与选择相符。不从基线输出或被测planner倒填任何值，也不称旧Q02历史manifest原本就是固定11。

现旧Worker硬要求并实际等待delayMs=10000；这是该模拟环境事实，不是协议通用要求。本次等价演练两套输入预先约定同一10s模拟处理延迟并记录实际耗时，保留原冻结预算/期限；不能缩短约定延迟或调大预算以消除失败。独立规则准备和第二提供者自核在B前完成，实际预算不可满足则如实受限。

**接受与排除**：源代码审阅指出上述真实计算路径及与原实现差异；实际启动身份、源码/配置/媒体摘要、八次调用输入/计算/结果/释放与Host保存共同证明替换。B前以同一第二函数的最小输入对（只变score或测量值）自核结果随内容改变，此组件证据可复用V02，不另跑整盘。仅改路径、名称、参数、标签、注释、文件摘要或加转调包装均拒绝；仅摘要不同不证明算法不同。完整接受/拒绝判据见VG-07.1，不开发生产算法或插件平台。

## 删除候选与保留项核查

核查范围：backend/src、backend/tests、scripts、workflows、配置/示例及相关合同；动态核查包括类型名/方法名字符串、反射/程序集加载、DI注册与脚本选项。源码快照研究不是永久无消费者证明，实施删除前重查当时差异，但以下已确认决定不能一律推迟。

| ID / 候选 | 调用与装配 | 配置、脚本、动态使用 | 历史读取/有效义务 | 实施决定及承接 |
| --- | --- | --- | --- | --- |
| D01 ExecutePostFlipComponentAsync、TestPostFlipStageContext、RescanWholeTrayAsync | 只有定义及私有相互调用；正式执行校验拒PostFlipRescanNotSupported | 未发现配置/脚本/反射入口；已有反射用途是其他查询/Worker测试 | 旧工艺不获准；历史记录另有reader | 删除整条执行链，不保留入口 |
| D02 ExecuteCoreAsync的componentHeightRound参数及相应裁剪/跳过分支 | 仅D01传额外参数，正式ExecuteAsync不传 | 无独立绑定/动态消费者 | 初始测量及当前多面义务由共同resolver承接 | 随D01删除，不删当前多面执行 |
| D03 StrictRecipeExecution、legacyExpected、frozen-plan零坐标、非严格Pending/跳过校验 | 当前仍被Start/Handoff/检测/ThreeStage调用，是错误活动旁路 | 1.0脚本/前端仍活动，不能认定整个版本无用 | 期限起点及独立绑定有效 | 完整语义输入与R06承接后删除开关及全部旁路，保留合法1.0解码 |
| D04 SimulatedDetectionPort/Profile及生产侧整段NotIntegrated检测替代 | AdapterBindings正式注册；3个专用测试 | Program/RuntimeOptions的DetectionTestMode及verify-latest-plc仍使用；无动态注册证据 | 来源/缺对象/未接入拒绝有效；不涉及历史序列化类型 | 正式唯一共同执行后删除整段生产实现；上层局部stub留测试工程，3测试迁移后删除 |
| D05 DetectionTestMode及模式到整段结果映射 | Program、RuntimeOptions、AdapterBindings | verify-latest-plc normal/pending/unknown等场景读取 | 失败/解锁/未知保护不能删 | 删除选项及整段结果注入；场景按实际设备/算法端口结果注入，010只跑受影响代表 |
| D06 TestTrayCodePolicy | 生产仅FScan调用，测试仅两方法 | 固定测试码/配方字面量；未发现反射 | 原始F码、映射和唯一绑定有效 | 映射由环境配置/解析器承接后删除Application策略及固定业务断言 |
| D07 测试JSON解析/固定来源白名单/固定能力默认值 | 当前业务真实使用，非死代码 | 现fixture仍需要解码；不能删除素材适配器 | 测量/范围/来源/批准均有效 | 将格式解释承接到边界后删除共同层原解析、白名单及默认值，不留双读分支 |
| D08 来源Unknown→fixture回退、MediaRef固定Test、固定SDK应用事实 | 当前采集、媒体投影和检测使用 | 无独立动态能力；不是可保留配置策略 | 真实来源、请求及应用事实必须承接 | 删除假推导，使用实际事实和运行用途；旧记录不补造 |
| D09 RescanWholeTray枚举/RescanMediaCommitted/ThreeDRescanMoveConfirmed历史reader | RunMediaCatalog、DeviceEvidenceHistoryReader及查询直接使用 | 已存事件类型及历史API消费者 | 历史身份/证据显示有效 | 保留只读读取与明确“不支持执行”判断；不恢复复扫，不删原记录 |
| D10 Current007AmbiguousDetectionMappingBlocksBeforeSorting重复断言 | 与PersistedHandoffRecords…错映射分支重合 | 已确认scripts/workflow/009-required-cases.json及Rules/Architecture/009-test-obligations.json按精确FQN登记；.specify/bugs内还存历史源快照 | StageEvent唯一性、无Sorting、终态None及原证据输出义务须承接 | 合并至持久错映射代表，补最终None及当前证据输出；先定向更新活动必需清单/迁移映射再删除重复方法。保留历史源快照与旧报告，不留重复备用测试 |
| D11 .Wire.cs通信fixture、文件相机、真实SDK拒绝、独立/recipes/bind | 正式端口/查询/测试均有消费者 | 当前环境和009边界依赖 | 协议隔离、历史及独立绑定义务有效 | 保留；不因目录带Test/Simulation或本轮不全跑而删除 |

D01/D02实施前若发现新的有效动态消费者，必须具体记录并先承接，不得秘密留旁路。D10已查到活动FQN清单依赖，因此采用先承接并对齐登记、再合并删除的具体顺序；不是因未运行便认定无用。009登记中的原义务/原源码证据保持，更新的是活动目标映射。本轮未实际删除任何代码或测试。

## 研究收口

技术选择全部基于本仓库当前有效实现，无新增外部SDK/框架选型。没有需要改变010业务范围的未决问题；当前限制是共享合同定向对齐、实施及实际取证未完成。生产预算/坐标/标定/设备能力仍按spec DEP-02限制对应生产入口。旧失败、旧报告和未运行状态均保留，是否阻断010仅看实际依赖。

后续Phase 1见[计划](plan.md)、[数据模型](data-model.md)、[共同执行](contracts/common-execution.md)、[输入边界](contracts/input-boundaries.md)及[验证合同](contracts/verification.md)。
