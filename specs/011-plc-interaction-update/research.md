# 011 技术研究（Phase 0）

日期：2026-10-03。状态：Phase 1已完成，本轮仅接收012并定向设计对齐/清单评审；没有执行构建、测试、设备、数据库或Git写操作。主项目与文档副本均未发现Git元数据，setup-plan返回的011-plc-interaction-update是功能目录回退标识，不是真实分支。

## 依据与范围

用户统一澄清及本次plan指令、[spec](spec.md)、[change-request](change-request.md)、宪章8.0.0优先。通信来源为只读20261001 Word和交互信号表；此前协议及测试数据不补齐新协议缺口。代码证据均读取主项目 `E:/dzk/gaode-1`；本副本没有完整源码，以下相对代码链接在集成至主项目后解析。

按speckit-plan分派共同配方、执行/通信和混合合同三个研究子任务；均未执行软件验证。混合合同任务随后仅修订用户指定四份Markdown。主项目澄清集成已先完成88份逐文件检查与写后摘要核对，详见[交接记录](clarification-sync-20261003.md)。本文件以后的设计增量独立登记。

## R01 沿现有工程扩展，不新建流程平台

- **Decision**：保留Application共同规划/执行、Infrastructure适配、Domain状态规则、Host单一装配及Gaode.Plc.Protocol纯通信定义。继续扩展RecipeRunPlanner与RecipeDetectionExecutor。
- **Rationale / 实证**：backend/Directory.Build.props为net10.0、nullable、warnings-as-errors；Infrastructure项目使用EF Core SQLite；Host/Composition/AdapterBindings.cs当前只有IDetectionPort→RecipeDetectionExecutor正式装配。现RecipeStage/LocalFaces已是列表，不需要为更多面引入脚本引擎。
- **Alternatives considered**：另建011执行器、012业务模型、通用编程平台均增加第二执行定义或无需求抽象，不采用。已有能力不因未采用通用引擎失去配置扩展性。

## R02 唯一共同定义与校验

- **Decision**：业务合同唯一为[recipe-contract/1.3](contracts/recipe-contract.md)，代码归属沿backend/src/Gaode.Application/Recipes/RecipeContracts.cs、ExecutionInputs.cs、RecipeDefinitionValidator.cs；保存、目录、F绑定、规划均消费同一类型和规则。
- **Rationale**：当前RecipeDefinition已有RecipeId/Version/Model/FCode/PlcRecipeId，校验已集中在RecipeDefinitionValidator；目录重复码规则仍散在JsonRecipeCatalog构造器，应收回共同校验，提供者负责持久化一致性。
- **Alternatives considered**：012复制字段校验、前端作为业务准入、把原码包装成DTO，均不采用。传输外壳可不同，但不能重定义业务字段或动作规则。

## R03 保存生效和一次F读取

- **Decision**：共同目录提供一个不可变的完整目录读取视图；F解码后取得一次当时已成功保存的视图，匹配、准入、规划、冻结全部使用该对象。012实现真实保存和目录可见性，011不依赖构造时目录缓存。
- **Rationale**：Infrastructure/Recipes/JsonRecipeCatalog.cs在构造时读文件；Host/Program.cs注册单例。Application/Station01/StartPublicPreparation.cs约257—281行的期望比较、准入和Build各Resolve一次，支持更新后会混用版本。
- **Alternatives considered**：只在无活动运行时重载、重启才生效、每步骤再次读目录、绑定旧选择版本均违背确认。将全运行锁住而拒绝配方保存也不采用。
- **线性化**：保存实际提交确认后才回复成功；F从同一持久库的一次一致读取与该提交有确定先后。保存返回成功之后的新F读取必须见新内容；已取得并冻结的运行不再读取活动目录。

## R04 身份、选择意图与内容版本

- **Decision**：现有FCode作为料盘匹配码，Model作为PLC产品型号语义，RecipeId作为配方身份；Version是服务器管理的不透明内容版本。内容摘要和目录摘要分别记录，不把任一个当PLC型号。旧PlcRecipeId仅核实历史展示/读取用途，退出新型号下发。
- **Rationale**：现IRecipeCatalog.Resolve(scenarioId,fCode)与ExpectedRecipeRef严格版本比较把场景/选择/内容锁定混在一起；源码已有Model文本但设备请求仍DisplayRecipeId。
- **Alternatives considered**：扫码换码成配方ID、按型号反查、用目录摘要代表所有配方内容版本、复杂发布审批均不采用。选择身份不符仍阻止产品动作；旧展示版本本身不阻止必要公共准备和F扫码。

## R05 当前存储与读写边界（本轮调度对齐）

- **Decision**：接收012首版research R03—05与data-model，按本轮调度决定采用独立SQLite配方库，复用现有技术栈；运行事实库职责不变。共同业务只依赖IRecipeCatalog与IRecipeStore，012的Head/完整正文及事务不进入领域接口；最终设计已删除SaveId。
- **Rationale**：同一事务落实真实保存、当前Head切换和FCode唯一约束；保存/完整读取/目录/F使用同一持久来源和一次一致视图，冻结运行保留旧值。不构造跨配方库/运行库/设备事务，不放宽运行库维护门禁。
- **Alternatives considered**：撤销011上一轮临时文件替换形成正式可写目录的方案；文件/Review只保确有消费者的测试或历史职责，不能作为第二活动来源或库失败回退。内存/localStorage/假保存及新审批平台均不采用。
- **承接状态**：012最终13份设计已按1.2接收/合入，原专用保存端口、SaveId条件更新和归属差异关闭。1.3 RC08已补G-01具体类型，生产端定义完成，012新设计回执及7份增量已接收合入，共同代码未交付；当前见[任务交接](tasks-handoff-20261003.md)，旧D结论保留历史。

## R06 更多面、四面与独立E

- **Decision**：复用Composition.LocalFaces、Stages.Targets及CameraPair AB/CD；四面按每个实际检测对象恰好1AB+3CD校验；其他面数不推导固定组合。RecipeCodeRule扩展独立ExtraPose，面/姿态不同身份。
- **Rationale**：RecipeDefinitionValidator.ExecutionProblem当前允许四面AB数量1或3；ReadAt只支持firstAccessibleFace/stage:N，当前E与LocalFace耦合。RecipeRunPlanner已能按列表展开，最小变更是规则与独立姿态动作支持。
- **Alternatives considered**：把E加成第五检测面、按大底座名称分支、添加相机组或枚举所有面数组合不采用。新增姿态的PLC编码不在业务合同填5。

## R07 首次3D、坐标和姿态退出

- **Decision**：真实算法结果新增TrayObservation：槽位有无、姿态、首次F绝对XY及采集/调用/来源；检测XYZ取冻结配置。运行中维护稳定物理槽的参与状态，异常退出后续对象动作，保先前事实。
- **Rationale**：ThreeDStep当前调用AlgorithmRole.Height，CaptureAlgorithmMessages只有HeightSamples；FScanStep读取公共固定F点；CoordinateResolver仍按HeightResult+MeasurementOffset计算检测Z。旧算法输出不能证明新姿态/F定位能力。
- **Alternatives considered**：从旧高度猜姿态、无输出当无料、目标值回显为观察、压缩列表重编号均不采用。成组/整体关系沿既有物理实体映射，姿态异常不是自动NG/Pending。
- **局部依赖**：现真实/虚拟算法都需提供新合同；只能通过实际采集/算法调用验证，已有worker只会测高时相应链受限，不伪造输出。

## R08 翻转放回与阶段顺序

- **Decision**：共同计划表达取件定位→翻转→放回定位→放回；同轮相关物理实体全放回后统一3D复查，再继续正常对象下一面；F只初次绑定。检测/适用E后先Sorting再UnloadPreparation。
- **Rationale**：RecipeExecutionCoordinator拒绝PostFlipRescanNotSupported，RecipeDetectionExecutor遇RescanWholeTray即拒绝；ThreeStageWorkflowExecutor目前170—226行先Unload后Sorting，RecipeExecutionBudget也沿旧顺序。
- **Alternatives considered**：只改显示顺序、Flip完成等于PutBack完成、先下料再在UI显示分拣完成均不采用。预算计实际新增动作/采集/算法/保存，冻结绝对窗口及更早截止，不因重排重置期限。
- **已有正确行为**：RecipeSortingMapper已排除OK并区分NG/Pending；保留并补姿态异常退出，不重复建设OK规则。特殊旋转/出口旧Test HTTP路径不能替代新同盘区域动作。

## R09 通信与VirtualPlc

- **Decision**：在Gaode.Plc.Protocol、LatestProtocolPlcDevice及VirtualPlc通信边界替换旧合并轴/状态解释，业务端口只传用途、目标、关联和语义结果。VirtualPlc经Modbus收命令后以自身状态推进。
- **Rationale**：Signals/SignalCodes和VirtualPlcEngine仍有合并XY/单Z、FlipCurrentFace、Sorting旧状态及ACK；新文档要求五轴用途独立和新分拣反馈。LatestProtocolStageActionAdapter.Transfer已有真实取料后IPickCommitPort门禁，必须承接。
- **Alternatives considered**：在Application解码、业务DTO携原码、Host写虚拟完成、旧状态再包装为新成功均不采用。正式地址、ASCII承载及速度/报警缺口见DEP，不填数。

## R09A 新F绑定不再等待旧PLC配方ACK

- **Decision**：改造RecipeApplicationCoordinator为共同纯业务绑定；新RecipeBindingReceipt只证明真实意图/绑定/适用移交提交及当前有效窗口。型号在实际FlipRequest中下发，不把寄存器写成功包装为DeviceRecipeApplied。
- **Rationale**：新Word §2.3只在翻转写Model_Number，§3.1 F后绑定是上位机业务；没有新RecipeApplied反馈。当前StartPublicPreparation、IndependentRecipeApplication却将绑定登记为机械动作，RecipeApplicationProjection还固定DeviceApplied=true，必须一起迁移。
- **Alternatives considered**：继续等旧ACK、新造一个ACK、返回默认DeviceApplied、因ASCII未定阻断所有单面绑定均不采用。纯业务绑定保原保存节点、Run/Tray/Plan关联、唯一t0/更早截止与取消，删除失效物理占用。独立bind仍不授产品续接，历史设备回执只读保真。
- **交付修订**：共同合同首次1.0已提前交付；本轮来源复核后升1.1明确此边界，主项目与4份混合合同引用一起修正。不是新的用户业务问题。

## R10 保存、取消、期限与历史读取

- **Decision**：复用RunExecution.SaveAsync/ITraceWriter及IStageEventStore既有职责，冻结内容进入RecipePlanAndBindingIntent；保当前有效业务/动作回执、取消关闭准入、设备未知占用、取料在途提交成功后放料和最终保存门。
- **Rationale**：009/010已建ActionCorrelation、ActionWindow、RequiredCommitEvidence与两条保存路径；RunMediaCatalog和DeviceEvidenceHistoryReader有实际历史读取消费者。
- **Alternatives considered**：新事务协调器、重建009/010账本、取消即设备停止、迟到提交恢复旧动作权限、重写历史payload均不采用。新负载版本显式区别旧记录；旧缺字段只读Unknown/Unavailable，不能用于新续跑。

## R11 实际替代与已删除核实

活跃候选及消费者见[计划删除表](plan.md#实际替代和删除义务)。已核实IntegratedDetectionPort、SimulatedDetectionPort、NotIntegratedDetectionPort、TestTrayCodePolicy、activeStrictRecipeExecution/nonStrictPending/frozen-plan-0、DetectionTestMode、ProfilePayloads/PositionPayloads等没有当前产品实现；010已完成删除，不重造任务。scripts/workflow/recipe_execution_010.py的forbidden_calls、010-test-obligations.json历史来源和原失败证据保留。

仍在用的是RecipeEnvironmentDecoder.DecodeReviewCode特定换码、SemanticRecipeInputProvider.ReadCodeMap/测高输入、CapabilityRegistration按ContentSampleWorker版本登记E、旧协议/动作及TestSpecialActions HTTP机械路径。删除必须等共同替代承接并核调用/装配/配置/脚本，不以测试失败为依据。

## R12 最小验证与职责决定

- **Decision**：复用一条单面完整链及一条多面含翻转放回/姿态复查/有效分拣的完整链，012共用保存、重读、F匹配和快照证据；更多面/独立E等缺口用组件代表补足。受影响架构负例和保存/取消/期限保护必需。
- **Rationale**：实际工程已有RecipeExecution010RunHarness、ThreeStageMainFlowIntegrationTests、RecipeMultiObjectIntegrationTests、RecipeSortingMapperTests、ProtocolBoundaryTests及RecipeExecutionBoundaryTests。旧单面测试名称AbThenCd不证明实际两个组都运行，须按代码及实际发现/执行核对。
- **Alternatives considered**：全库测试、面数穷举、009/010历史全重验或只做规划器测试均不采用。具体集合与当前未执行状态见[验证合同](contracts/verification.md)。
- **文件责任**：Program.cs、RecipeEndpoints.cs及Infrastructure/Recipes目录提供者由012唯一编辑；Application共同定义/绑定/执行、CapabilityRegistration、PLC/VirtualPlc及运行查询由011。路径明细见plan，双方不同时修改共享文件。

## 已解决选择与保留限制

本轮普通技术选择均已落定；无新增业务澄清问题。DEP-01正式地址、DEP-02 ASCII承载、DEP-05速度、DEP-06报警只限制依赖字段；DEP-03/04恢复/安全不从旧合同猜值。虚拟测试映射须明确标记用途，不能证明正式PLC互通。012最终1.2设计已接收合入；G-01生产端1.3已交，设计消费回执已接收，共同代码待交。完整独立源码副本仍须实施前准备，本轮tasks承接。以上不是未完成技术研究，也不是所有无依赖工作的全局前置。


## R12 G-01定向类型落位

共同RC08保留检测Coordinates列表，明确配置Fixed必填；ObjectExecutionInputs新增局部PurposePoints及逐阶段Flip.Stages，复用现有点值/来源类型，不新建点位服务或流程编程。TargetPose为版本化语义引用；Stage.Number与LocalFace分开，当前删除旧CoordinateRule/测高依赖，保仍有效旋转描述及历史读取。三个schema取值、唯一正文序列化和012精确路径均在RC08，不在本文件另造定义。实际源码消费者核对和迁移义务见tasks；这不是新业务澄清。
