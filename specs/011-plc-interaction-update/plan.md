# 技术方案：PLC交互与共同检测流程更新









人工取盘实施落位（W §2.4最后一段、D16/EX03）：在Detection、Sorting、UnloadPreparation真实完成及WholeTrayCompletion必要提交后，由共同业务核同盘/计划/下料完成事实并实际追加ManualRemovalAllowed事件，才输出允许人工取盘；不再调用旧解锁设备指令或把解锁观察伪造为完成。状态ReadyForRemoval/ManualRemovalAllowed、RunSnapshot.ManualRemovalAllowedEventId和ManualTrayRemovalConfirmationRequest.ManualRemovalAllowedEventId承接当前引用，最终人工确认必须匹配此已提交事件再形成Final。当前Stage使用ManualRemovalAdmission，有限窗口/取消/持久化失败门继续成立。原Unlock/ReadyForUnlock枚举与旧历史Final/快照字段只留读取原记录，不能授当前执行或推断安全控制完成；新Final使用ManualRemovalAllowedEventId，旧UnlockObservedEventId仅保留原JSON读取。012仅消费状态/通知和现有ConfirmManualTrayRemoval操作，不新增页面、传感器或安全屏蔽假反馈；没有真实提交不开放人工确认。









启动实施落位（W §1.2/§3.1、CR D16及关闭旧信号确认）：人工上料后的正式启动请求承接已提交StartIntent和运动租约；StartPreparationStep只确认同连接代次的实际设备就绪/自动/安全观察，保原PlcAcceptance窗口、取消和真实ActionFact保存。PC_System_Ready/PLC_Ready_State用于就绪握手，不再发送旧PC_Start_Cmd触发夹紧，不要求或伪造Clamp/PhysicalButton/ZoneConfigACK。新StartPreparationEvidence(Accepted,DeviceReady,DeviceEpoch,Observation及Operation/Action/Intent身份)替代当前StartClampEvidence；V1历史StageHandoff字段仍只读。共同移动/独立绑定/当前V2移交消费实际Ready和有效租约，不再以旧Clamp作为准入。无需新增页面或第二个人工按钮前置；新源Start_Button作为真实外部输入未被观察时不能伪称已按。012不需改共同类型，消费新运行批后仅接既有真实阶段。旧StartClampStep、ClampObservationPolicy及无用区域准备端口/当前装配删除；有效保存、动作互斥、有限等待和暂停/取消/未知保持。









**功能标识**：011-plc-interaction-update（功能目录标识，非真实Git分支）  




**日期**：2026-10-03  




**规格**：[spec.md](spec.md)  




**宪章版本**：8.0.0  




**范围**：首次公共准备/3D/F→共同配方冻结及实际保存→配置检测/翻转放回/姿态复查/适用E→正确分拣→下料、人工取盘与最终保存。012负责制作、目录读写及现有界面消费；011不增加页面。









实施接口承接：T006/T010使用MoveRequest.FlipPreparation（业务型号/姿态/实体/槽/TransitionId）保证取件XY之前完成通信映射，Domain保XY与独立五轴观察；不新增业务执行器。新协议去掉的夹紧、解锁和采集ACK按当前正式动作改写，旧记录保历史读取，不伪造旧反馈。









### 当前独立plan/bind入口落实（实施增量，不改recipe-contract/1.3正文版本）









011提供`CommittedRecipePlanReader(ITraceQuery,IStageHandoffQuery).ReadAsync(Guid runId,string scenarioId,IReadOnlyList<string> occupiedSlots,CancellationToken)`，返回可空`FrozenExecutionInputs`：无当前可靠V2返回null；有V2则核同run/tray、场景/槽范围、绑定引用、真实已提交Intent的摘要及其execution-inputs/2内容/PlanRevision/FCode/用途。不重新读取活动目录、不重新匹配或生成旧run计划，不以V1执行续接。旧V1只留历史查询。读取不授产品动作许可。









012唯一修改RecipeEndpoints的plan/bind装配：共同reader替换BuildPlan内V1/活动catalog/LoadPublic当前用途分支；两端使用reader返回的Plan，容量取该Plan的完整配置，不能补options默认值。IndependentRecipeApplication仍使用已冻结Public/Budget/Simulation、原截止、运行互斥及取消、实际Intent/Bound保存；核提供Plan等于既有冻结Plan，独立bind回执仍productContinuationAuthorized=false，不改变原run冻结或允许重复检测。正常F主链继续只匹配一次，不经过独立端点。









实施轴输出落实：RunSnapshot增加AxisObservations（AxisObservationProjection列表），DeviceObservationApi同名列表；每项Axis、Position?、Unit?、Reliability、ObservedAt?、ConnectionEpoch?、EvidenceRef?。名字X/Y/CameraZ/ScanZ/GrabZ，不带原始报文。运行投影只从同run已提交ActionFact的实际DeviceObservation或相关DeviceActionEvidence.Positions取值；未保存/目标值/其他run不进入实测。位置用途XY只贡献X/Y；单位未有直接来源为null。状态子schema及当前Run.DeviceSchemaVersion显式为device-semantics/1.1，结果仍station01-result-display/1.1；旧记录不回填。012消费新增列表，不按一个ActualZ填三个轴，不增加页面。









分拣实施落位：通信配置PlcRuntimeOptions.SortingSafePosition使用PlcGrabSafetyPosition(GrabZ,Unit,Frame,Purpose,SourceReference)，没有默认高度；只有来源/用途/单位/坐标系匹配目标才允许依赖分拣。Test组件显式提供自己的值，现场仍待有效配置。主机按PC04逐轴驱动；Sorting_Exec_Status按源0空闲/1取料完成/2放料完成/3失败，不保旧Executing/4失败/5满位或Sorting_OK。取料确认及IPickCommitPort真实提交先于抬升和搬运；放料完成后再抬升，最终清命令不以清零充动作反馈。下一次取料核非持件及新位置/当前指令关联，不能把上次取料完成当本次反馈。Unload仅XY，不写GrabZ/旧XY命令。普通槽身份只在业务事实关联，新源不下发旧Sorting_Part_Index。未知或取消不派发后续动作，未确认输入不补安全高度。









## 方案摘要









沿现有Application规划/执行与Infrastructure叶适配更新，不建立第二套配方模型、校验器或执行器。共同定义唯一为[recipe-contract/1.3](contracts/recipe-contract.md)，已提前交付主项目。F绑定时从012独立配方库一次一致读取已真实保存的当前内容，使用不可变运行快照；旧选择版本不锁定新F，冻结运行不受编辑影响。









新PLC协议下F绑定是软件业务提交，不再调用不存在的旧配方ACK。型号由实际翻转请求携带；独立轴、翻转/放回和新分拣反馈留通信层。首次3D提供有无、姿态及F XY；检测XYZ取配置，异常槽原位退出；所有本轮相关实体放回后统一复查，分拣完成后才下料。









上一轮已完成双方澄清88份逐文件主项目集成及四份混合合同、技术研究/设计。设计增量和修订单独记在[交接记录](clarification-sync-20261003.md)，本轮已接收并合入012最终13份设计增量；G-01由共同合同1.3 RC08补齐，D012-G01-receipt-1.3及7份增量已接收合入，共同代码待交付，详见tasks-handoff-20261003.md。









| P13阶段边界 | 当前方案 |




| --- | --- |




| 起点与终点 | 正式启动、公共输入有效；终点为适用分拣/下料/人工取盘/Final真实提交及异常物理槽号可查询 |




| 必须参与的组件与接口 | 同一Host业务、实际目录保存、正式PLC端口/独立VirtualPlc、实际采集/worker、SQLite/媒体；联合前端证据由012实际操作API/已授权界面 |




| 必要验证 | 一单面完整链、一多面含换面放回复查及有效分拣完整链；必要配置/通信/保存/取消与架构负例 |




| 完成证据 | [M01—M11](contracts/verification.md)当前源码/构建/合同/配置、Run/动作/采集/提交及实际查询，来源如实记录 |




| 延期项 | 正式地址/ASCII承载/速度/报警/恢复/安全及生产输入依赖；非主流程边界、性能、全量组合、新平台均不新增 |









## 技术上下文（Technical Context）









| 事项 | 当前选用 | 依据与状态 | 尚缺证据/局部限制 |




| --- | --- | --- | --- |




| 后端 | C#、net10.0、ASP.NET Core；既有Domain/Application/Infrastructure/Host/Plc.Protocol | 主项目Directory.Build.props和实际csproj，已只读核实 | 本副本未有完整源码，未构建 |




| 算法 | 现PythonWorkerAdapter/WorkerProcessSupervisor与实际媒体协议；增加TrayPose结果合同 | 当前只有Height等结果，不能冒充新姿态能力 | 新观察能力/实际worker输出需实施；无输出限制对应链 |




| 数据 | 运行事实SQLite/EF Core、既有媒体保持；配方采用012独立SQLite库 | 当前JsonRecipeCatalog构造时读文件，不能满足动态保存生效 | 012最终1.2消费设计已接收/合入；1.3新增类型设计消费已接收，共同代码未交付，不建版本平台 |




| 前端/宿主 | 006/012授权弹窗与既有运行区域；现WPF/WebView2及API/通知 | 用户明确边界；012唯一消费 | 本轮不逐页验收、不改归档 |




| 设备 | 正式Modbus TCP、PC主/PLC从；VirtualPlc共用通信定义 | 新Word/表及现协议工程 | 正式地址、型号承载等按DEP，不继承旧ACK |




| 性能/容量 | 冻结配方实际工作量与既有有限预算、实体/区域容量 | 新动作必须计数，不能猜节拍/现场速度或加时凑通过 | 现场参数缺失仅限制依赖动作 |




| 软件验证 | 既有xUnit规则/合同/通信/集成及同一架构检查器 | 实际测试工程与M表已核实 | 全部本轮NotRun；不全库、不全历史 |









研究决策/替代方案见[research.md](research.md) R01—R12及R09A。普通技术选择已决定，不重新作为用户业务澄清；外部未决保持真实状态。









## 宪章检查（Constitution Check）









“符合”只评价设计，不表示实现/测试/生产通过。设计前依据spec/研究，设计后依据下列产物复核。









| 原则 | 检查点 | 设计前 | 设计后 | 证据/局部限制 |




| --- | --- | --- | --- | --- |




| P01 | 最新用户决定优先及来源追溯 | 符合 | 符合 | 五项确认、88份集成、旧ACK与目录冲突已定向修订；历史保留 |




| P02 | 核心模块真实接入且不假成功 | 符合 | 符合 | EX01—03、M06/07；实现尚未执行，旧worker缺新观察不得通过 |




| P03 | 配方驱动实际运动/面/姿态 | 符合 | 符合 | RC02/03、EX02；四面1AB+3CD，更多面不推固定组合 |




| P04 | 可靠反馈、有限等待、实际准入 | 待补充，仅限制所列部分 | 待补充，仅限制所列部分 | EX03/05、PC06；缺正式现场输入不推安全或默认完成 |




| P05 | 唯一业务/通信隔离及控制所有权 | 符合 | 符合 | RC唯一定义、下面路径责任、R09A去旧绑定假设备成功 |




| P06 | 既有资源互斥/媒体所有权 | 符合 | 符合 | 并发与资源表，取消与InputReleased保留，不新加资源平台 |




| P07 | 稳定对象身份及状态分离 | 符合 | 符合 | data-model、EX01/04，异常槽不重编号，动作与质量分别保存 |




| P08 | 真保存/冻结与关键门 | 符合 | 符合 | RC04/05、EX03、M08/09，深不可变、取料提交门、Final门 |




| P09 | 可关联结构化日志 | 符合 | 符合 | EX05及M09；复用持久日志，必要失败能定位 |




| P10 | 未决局部限制，不造值 | 待补充，仅限制所列部分 | 待补充，仅限制所列部分 | DEP-01—06、PC06；单面软件绑定不等ASCII |




| P11 | 配置扩展及唯一校验 | 符合 | 符合 | 现列表规划扩展，RC04同一校验；无通用脚本平台 |




| P12 | 前端边界和只读归档 | 符合 | 符合 | 006/012独立规格；本轮只读消费合同，不声明页面通过 |




| P13 | 必要验证、真实证据及停止阶段 | 符合 | 符合 | M01—11、quickstart；本轮NotRun且止Phase 1 |









没有用复杂度理由豁免规则，也未将延期输入写成全局阻塞。需求清单仍14/16的全范围输入限制不等于禁止已授权设计；本表不据此勾成全部软件通过。









## 结构与职责（Project Structure）









不新增工程。下列代码路径均相对主项目，后续在完整独立源码副本实施；本轮只写文档。









| 模块/现有位置 | 状态所有者 | 依赖/职责 |




| --- | --- | --- |




| Application/Recipes | 011 | 共同类型、唯一校验、Match、Plan、Admission、冻结及纯软件绑定；不解析文件/协议 |




| Application/Station01、Workflow | 011 | 公共准备、姿态参与集合、唯一执行/预算/保存/完成，不依环境知识选业务 |




| Domain/Station01 | 011 | 观察/物理槽状态及纯规则，技术/质量/姿态/物理/最终状态分开 |




| Infrastructure/Recipes | 012 | 目录/格式/持久提供者，调用011规则，不生成执行步骤 |




| Infrastructure/Devices/Plc、Gaode.Plc.Protocol、VirtualPlc | 011 | 正式协议映射、分轴/翻转放回/分拣及真实设备事实 |




| Infrastructure/Algorithms、采集叶适配 | 011 | 新观察结果、实际来源/媒体/能力关联；Test/Real共用业务 |




| Infrastructure/Persistence与Host运行查询/通知 | 011 | 既有保存链/历史reader、真实阶段/异常槽号输出 |




| Host配方读写端点/组合根、frontend | 012 | 调用共同能力及必要数据绑定，不另写校验或执行 |









### 共享文件唯一负责人及交付要求









| 实际路径 | 唯一编辑者 | 另一方需要的内容 |




| --- | --- | --- |




| backend/src/Gaode.Host/Api/RecipeEndpoints.cs | 012 | RC04共同目录/保存；plan/bind改消费RC05.1软件回执，去IPlcRecipePort/WithCommunicationAsync；独立bind不授产品续接 |




| backend/src/Gaode.Host/Program.cs | 012 | 单一目录/保存实例供编辑与F读取；调用011注册函数，不恢复旧整段Test执行器或旧配方设备端口 |




| backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs | 012 | 当前正式读取改接独立配方库一致视图；文件类只保有效测试/历史消费者，不作活动回退；调用共同校验 |




| backend/src/Gaode.Infrastructure/Recipes/RecipeCatalogFactory.cs | 012 | 接真实可写提供者；Review/File格式不是业务策略，不另造工序 |




| backend/src/Gaode.Infrastructure/Recipes/RecipeEnvironmentDecoder.cs | 012 | 按共同结构解码，移除无用途Test码转换/旧必填显示号；保合法历史读取 |




| backend/src/Gaode.Infrastructure/Recipes/SemanticRecipeInputProvider.cs | 012 | 新配置XYZ/姿态/码输入格式，移除无用途码映射和测高偏移，禁止预排业务 |




| backend/src/Gaode.Application/Recipes/RecipeContracts.cs、ExecutionInputs.cs、RecipeDefinitionValidator.cs | 011 | 012直接消费类型/验证/摘要/接口；不在提供者复制业务规则 |




| backend/src/Gaode.Application/Recipes/RecipeAdmission.cs、RecipeRunPlanner.cs、RecipeApplicationCoordinator.cs、IndependentRecipeApplication.cs | 011 | 012端点调用；一次定义、深冻结、新RecipeBindingReceipt及有效预算/保存保护 |




| backend/src/Gaode.Host/Composition/CapabilityRegistration.cs、AdapterBindings.cs、Station01Registration.cs | 011 | 提供Program接线；去旧配方端口/worker特征特权，保合法叶能力登记 |




| backend/src/Gaode.Application/Station01/StartRunContext.cs、StartPublicPreparation.cs、PublicPreparationHandoffV2.cs、StageHandoffBuilder.cs、RunExecution.cs | 011 | 012请求/状态消费；旧选择为意图，不重复Resolve/伪机械绑定 |




| backend/src/Gaode.Host/Api/Station01ApiContracts.cs、RunEndpoints.cs、QueryEndpoints.cs、DeviceSemanticProjection.cs、CommittedResultProjection.cs、RunMediaCatalog.cs、Station01NotificationService.cs | 011 | EX04/05真实状态/媒体/异常物理槽；012仅更新页面消费者或在RecipeEndpoints调用 |




| backend/src/Gaode.Domain/Station01/RunSnapshot.cs、backend/src/Gaode.Application/Station01/RuntimeObservationProjection.cs（新增）、backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs、StageEventStore.cs | 011 | T015公开语义字段、唯一已提交事实投影与提交后重读通知；无第二执行器，失败通知不回滚已知提交事实 |




| backend/src/Gaode.Infrastructure/Gaode.Infrastructure.csproj、backend/src/Gaode.Host/appsettings.VirtualPlc.json | 011 | T026接收012明确指出的混合装配清理：删已无正式提供者消费的Review目录输出项及Provider=Review；其余有效配置保留，独立SQLite设置由012已有端口消费 |




| backend/src/Gaode.Infrastructure/Persistence/RecipeApplicationProjection.cs、RecipeApplicationHistoryReader.cs、DeviceEvidenceHistoryReader.cs | 011 | 新绑定不固定DeviceApplied=true，历史缺字段如实可读，不改旧记录 |




| backend/tests/Gaode.Contracts.Tests/Recipes/RecipeCatalogTests.cs、RecipeEnvironmentDecoderTests.cs、SemanticRecipeInputProviderTests.cs | 012 | 011提供共同合法/非法配置语义，012迁移提供者/保存断言 |




| backend/tests/Gaode.Contracts.Tests/Recipes/RecipeRunPlannerTests.cs、RecipeExecutionCoordinatorTests.cs、PublicPreparationTargetResolutionTests.cs；Workflow/RecipeSortingMapperTests.cs | 011 | 012复用共同验证证据，不复制校验测试实现 |




| backend/tests/Gaode.Integration.Tests/Station01/ExpectedRecipeMismatchTests.cs；Storage/RecipeApplicationReceiptTests.cs；Support/RecipeExecution010RunHarness.cs、RecipeExecution010Expectations.cs | 011 | 012交真实保存/读写调用需求，联合同次证据；不各写第二条正式执行链 |




| scripts/010-content-sample-worker.py及受影响worker输入/协议适配 | 011 | 实际媒体派生/明确虚拟来源的新观察能力，不能按测试编号编排或假正常 |




| frontend/src/runtime.js、frontend/src/pages/a.html、frontend相关组件/原型检查与测试 | 012 | 消费RC/EX及已有006映射，归档只读，不扩大页面 |




| specs/011-plc-interaction-update及本次4份混合合同 | 011 | 唯一共同规范；012通过交付记录提消费需求 |




| specs/006-frontend-station01-console、specs/012-recipe-authoring及frontend说明 | 012 | 011仅集成已明确交付版本，不改012副本或复制未交付设计 |




| backend/src/Gaode.Application/Recipes/RecipeDefinitionIdentity.cs（新增） | 011 | RC01/RC04.1唯一身份、版本与摘要实现；012调用，不能复制规则 |




| backend/src/Gaode.Application/Recipes/RecipeDefinitionSerialization.cs（新增） | 011 | RC08唯一正文2序列化和必填输入保护，012直接调用；012最终设计已取消拟议RecipeAuthoringService |




| backend/src/Gaode.Infrastructure/Recipes/RecipeStore*（012拟新增）及其配方库schema适配 | 012 | 独立SQLite的短事务/唯一约束/完整一致读取；运行库不扩职责，无文件回退 |




| backend/src/Gaode.Host/Api/Station01Authorization.cs、TestAuthenticationHandler.cs | 012 | 沿已有角色补最小保存权限，保真实Test来源，不扩生产认证平台 |




| backend/tools/Gaode.StorePrep/Program.cs | 012 | 配方库显式prepare/inspect；011如有运行库维护需求提具体变更，不并行改同文件 |




| backend/src/Gaode.Application/Ports/CaptureAlgorithmMessages.cs；backend/src/Gaode.Infrastructure/Algorithms/PythonWorkerAdapter.cs、WorkerProcessSupervisor.cs | 011 | EX01.1新观察合同、实际输出适配及来源/释放/取消；具体路径均在backend/src的对应工程 |




| scripts/collect-station01-page-facts.ps1及联合运行脚本；RecipeExecutionBoundaryChecker及其测试 | 011 | 012提供保存/重读/前端与新增存储可达路径需求，011统一承接必要采证和架构负例 |









未列的新重叠路径在实施前登记一个实际负责人；默认按上述模块归属，同一文件不得并行覆盖。共享接口代码变更须等授权tasks承接，不能以本plan代替任务阶段。









### 012最终交付接收与当前对齐









已接收并逐文件合入012 plan-handoff最终13份增量。其独立SQLite、唯一IRecipeStore、服务端身份/版本、共同摘要、ExpectedVersion及两个输入适配文件归属已对齐1.2，D01—D06首版差异关闭。G-01由1.3 RC08补齐：PurposePoints、Flip.Stages、E引用、TargetPose与三层schema均有具体定义；012新设计消费回执及本次7份增量已接收合入，共同代码和后续代码增量未交付。当前记录见[任务交接](tasks-handoff-20261003.md)，[首版对齐记录](design-alignment-20261003.md)仅保留当时结论。









新3D生产者/适配/消费者与验证责任已按实际文件收敛到[EX01.1](contracts/execution-and-state.md#ex011-新3d观察的生产适配消费与验证责任)，未得到实际算法输出只限制对应观察链。









### 分批交付与首次构建顺序









本轮只调整已有任务的交付依赖，不改变recipe-contract/1.3业务合同。D011-common-code-1.3为T003—T005共同类型、唯一校验/身份/序列化、端口、Matcher及深冻结；D011-runtime-binding-1.3为T011/T012协调器、真实绑定/移交与注册清单；D011-runtime-state-1.0为T015真实查询/通知。012分别在T002及保存任务/T018、T020、T021消费，不能把基础源码接收当运行能力已交。









D012-store-api-1.3只接012 T017真实保存、完整重读、版本及必要证据。012 T019适配迁移、T020运行接线分别接收；其首次构建必需部分先交，全部适用运行接线在联合启动前核齐。独立保存不等联合验证。









RC08删除PointRefs、旧Stage/坐标/Flip字段及新端口签名会影响整个编译项目。已只读核Application的Planner/Validator/CoordinateResolver、Workflow执行器/协调器/预算，Infrastructure两个012适配及目录，Host共同调用，StorePrep的Infrastructure项目引用和必要测试消费者。准确路径、字段和负责人见[当前交接消费者表](tasks-handoff-20261003.md#首次构建所需消费者迁移只读源码核查未改代码)。011已有任务内维持保存链和相关首次构建所需迁移前移T003—T005基础批；012在接基础源码后完成其T019及T010/T020直接消费早批，不以存储验证或完整运行任务作为这些迁移的前置。只前移必要直接迁移，完整动作/状态及其验证仍在原任务；不保错误旧字段、加兼容旁路、排除源码编译或伪造返回。









每批独立记录源码、已实际构建范围、已验证能力；代码可以早交，相关项目必须在双方直接消费者齐备后才构建/运行。T005源码不等012迁移结果才首次交；其组件若引用Infrastructure，则证据等待012相应迁移，而不是让012等T005全部验证。必要的直接源码迁移不用最终联合验证解锁，原任务未全完成不勾选。









T025准备唯一驱动，T026及双方必要清理完成、相关代码/页面/API/实际输入齐备后，由011在T027统一启动单面/多面各一条，012 T022同期采证。D012-ui-joint-evidence是运行后完成判据，不是启动前置；双方随后分别收口。外部地址/3D等仍只限实际依赖部分，复用当前有效证据，不新增完整链。









012随后明确交付本轮tasks、shared-integration、quickstart及plan-handoff共4份，已逐文件接收并合入主项目；已取得对先行分批说明的设计消费回执。其关于011活动正文尚未同步的记载保留落稿时点，当前011正文已实际同步；代码/构建/联合证据仍未交付。此前7份不重复处理，未复制在制品。









## 数据、契约与状态









- [data-model.md](data-model.md)：对象关系、版本化负载、姿态参与状态与真实保存。




- [recipe-contract/1.3](contracts/recipe-contract.md)：唯一模型/校验、料盘码/身份/型号、保存可见性、匹配与冻结；1.1明确取消旧PLC配方ACK。




- [station01-execution/1.0](contracts/execution-and-state.md)：真实工序、物理槽异常与运行/API/通知投影。




- [plc-interaction/1.0](contracts/plc-communication.md)：分轴、翻转放回、新分拣、VirtualPlc及未决边界；原码仅通信。




- [011-verification/1.1](contracts/verification.md)：受影响最小集合、证据复用与未执行边界。









## 配方共用逻辑与动作隔离









正式入口仍POST station01/runs；012读写经既有授权边界，保存/读取失败真实返回。F之前只冻结公共配置及必要公共输入，不能拿未匹配产品配方解释同一次3D；F之后一次匹配定义用于准入、规划、冻结和实际绑定提交。









所有运行均走共同类型、校验器、规划器、执行器，只有设备/采集/算法/目录等叶适配按实际环境变化。生产准入保持，真实保存不表示已批准生产；文件/媒体路径和worker版本不参与业务动作选择。已实现OK不搬逻辑直接保留并补姿态剔除。









### 配置与策略扩展设计（P11）









新增能力限已确认分轴/放回/姿态检查、更多面及独立E：Stages/Targets复用列表，四面规则只对四面适用，E独立PoseId。型号/面/姿态业务意图交通信适配映射，不解析PLC内部机械程序。缺配套能力/参数只拒绝依赖动作并输出原因，不造备用默认流程。









既有特殊旋转/出口的Test HTTP路径须审计并承接有效实体义务；没有新有效机械映射时不能用HTTP旁路证明新主链。不把特定产品名称或旧route名字当已批准新控制序列。









## 并发、资源与异常出口









| 路径 | 所有者/容量来源 | 期限来源 | 失败与资源处理 |




| --- | --- | --- | --- |




| 配方保存/目录读取 | 012独立配方库短事务条件写/唯一约束；011从同一来源只取一次一致视图 | 请求取消及既有有限存储调用约束，不猜现场时限 | 真实失败/提交未知，不能Saved；正在运行快照不变 |




| 工位/运动 | 现Coordinator/运动准入与通信端口，同一资源所有者 | 当前ActionWindow及已冻结阶段截止 | 关联不符/未知不得下一动作；保未知占用，不盲重发 |




| 采集/算法 | 现相机端口、Worker监督、有界队列及媒体租约 | 冻结采集/算法/释放额度 | 必要输入失败阻依赖动作；缺陷技术失败按有效Pending规则，不能把姿态失败放行 |




| 翻转放回/统一复查 | 共同执行器按相关物理实体集合 | 计入实际动作+复查+保存，不刷新窗口 | 放回不齐无复查/下一面；异常槽退出、正常继续 |




| 分拣/保存 | 现Allocator、IPickCommitPort及StageEventStore | 现绝对截止、CriticalSave及剩余窗更早者 | 取料实存失败无放料；UnknownHeld保留关联与原槽/目标 |




| 心跳/停止/通知 | 原独立控制/通信调度 | 原有效配置 | 不被媒体/算法/保存/前端等待阻塞，无新长稳平台 |









## 保存与恢复









独立配方SQLite库事务与运行事实库提交分别真实执行；共同IRecipeStore不暴露SQLite，不能包装两库、PLC或算法为一个共同事务。RunExecution和StageEventStore原职责保持；新观察/快照/动作增量优先使用版本化既有payload，不主动做历史库升级。媒体实际取得后保存并维持所有权至真实释放。









F业务绑定保Intent→Bound→适用handoff的实际回执与原期限；不需要设备RecipeApplied。实际翻转下发型号仍需要真实动作完成。软件保存失败不能记录虚构“PLC配方动作未知”；已有真实派发动作未知照常UnknownHeld。









正常暂停、取消、保存未知裁决及有限历史读取继续；新故障恢复、Retry、软停自动回位、安全控制按DEP延期，不从旧USR-D/旧寄存器推导新恢复。旧日志、图片、失败与提交事实只读保留。









## 实际替代和删除义务









以下来自本轮实际源码/消费者核查，不是“文件名看起来旧就删除”。替代完成、有效义务承接并核调用/装配/配置/脚本/历史读者后必须实际删除；本表制定的设计时点未删代码，当前实际删除及保留依据见verification-report的清理表和固定批manifest。









| 活跃旧逻辑/路径 | 已见消费者 | 替代及必须保留 |




| --- | --- | --- |




| JsonRecipeCatalog构造缓存；StartPublicPreparation三次Resolve及旧ExpectedRecipeRef锁版本 | Program单例、RecipeEndpoints、StartPublicPreparation、RecipeRunPlanner | 012同一配方库一致读取＋011一次匹配/不可变冻结；保目录真实读取、身份不符拒绝、准入与提交 |




| RecipeEnvironmentDecoder.DecodeReviewCode、SemanticRecipeInputProvider.ReadCodeMap | CatalogFactory、CapabilityRegistration及测试输入/脚本 | 扫码即料盘编号；删除无用途特定换码、字面测试ID，保实际解码来源与历史原文 |




| CoordinateResolver.MeasurementOffset、ExecutionInputs/Validator的旧HeightRound、SemanticRecipeInputProvider测高CSV | planner/coordinator/检测目标及配置/组件测试 | 配置检测XYZ＋姿态观察；删无用途活动测高依赖，旧HeightResult历史读取保真 |




| RecipeDefinitionValidator旧四面1或3AB；RecipeCodeRule仅stage绑定E；PostFlipRescanNotSupported | planner、RecipeExecutionCoordinator、RecipeDetectionExecutor、旧Q03测试 | 四面恰好1AB、更多面列表、独立E、真实放回复查；保必检/对象/相机/预算保护 |




| RecipeApplicationCoordinator依IPlcRecipePort；LatestProtocolPlcDevice.Binding.cs旧RecipeId/容量ACK | StartPublicPreparation、IndependentRecipeApplication、HandoffV2、Station01Registration/AdapterBindings、012端点 | 共同软件RecipeBindingReceipt；型号随真实翻转；保Intent/Bound/Handoff实存、原t0/截止/取消，删除失效设备绑定及轮询/占用分支 |




| RecipeApplicationProjection固定DeviceApplied=true、旧SimulatedPlc绑定与UnavailablePlcRecipePort | 查询、模拟装配、ReceiptTests | 新绑定仅实际软件提交；有限历史reader不补真，删除无用成功模拟/端口 |




| AfterBindingCapacityWriteForTest、HoldAfterBindingCapacityWriteAsync、RecipeApplicationPreconditionHold | ControlledTestPersistenceFault、Station01Registration、旧BA/通信测试 | 迁移有效失败/取消/期限断言至真实业务保存或翻转边界，不保留假容量写入 |




| Signals/SignalCodes、LatestProtocolPlcDevice各partial、StageActionAdapter、VirtualPlcEngine/DeviceActionAudit | 正式通信、虚拟监控、通信oracle/脚本 | 独立轴、新翻转放回/分拣；删无用途旧ACK/旧原码/合并Z/独立实际面号门，保真实反馈、关联、保存、取消 |




| RecipeDetectionExecutor特殊Transfer/Rotate/Exit、SpecialHandlingCompleted/noAdditionalSorting；TestSpecialMessages/Actions及BaseUri | LatestProtocolPlcDevice.Acquisition、VirtualPlc/Program、Station01Registration、RecipeRotationIntegrationTests/Wire与监控测试 | 共同确认动作/本盘区域处置承接；缺映射局部受限，替代后删无用途Test机械HTTP旁路/配置/失效断言 |




| ThreeStageWorkflowExecutor先Unload后Sorting、RecipeExecutionBudget旧排序 | 阶段投影/完成聚合、API/前端 | 实际分拣后下料，预算和查询一起迁移；保取料实存、完成保存和人工结束 |




| CapabilityRegistration按ContentSampleWorker/1决定E能力 | 能力注册、worker/配置消费者 | 按明确能力合同/用途登记，不按worker特征赋业务特权；真实来源仍记录 |




| 当前分拣/历史保护 | RecipeSortingMapper已排除OK；HistoryReader/RunMediaCatalog仍实际使用旧记录 | 保正确OK过滤与NG/Pending分离；新增姿态剔除，历史/有效关联保护不能随旧分支误删 |









**已删除，仅核实**：010的IntegratedDetectionPort、SimulatedDetectionPort、NotIntegratedDetectionPort、TestTrayCodePolicy及旧相关测试、Strict/nonStrict/frozen-plan-0/DetectionTestMode/ProfilePayloads/PositionPayloads等未在当前活动实现命中。不新增重复删除任务。forbidden_calls门禁、010迁移账本中的历史名称和原失败证据保留。









无用代码不能用注释、永久开关、备用实现或新兼容层保留；失败本身不构成删除依据。每个删除项由后续tasks映射到已有真实消费者及最小证明，本轮tasks定向承接，不勾历史任务。









## 软件验证与证据计划









完整最小集合见[M01—M11](contracts/verification.md)。共同配置/保存/F/快照、更多面/E/姿态/处置、通信隔离/同执行业务及受影响失败保护全部有对应义务，不用“快速收敛”删必需主流程。









| 需求/原则 | 最小方法 | 预期观察 | 当前状态 |




| --- | --- | --- | --- |




| FR003/014/015，P07/08/11 | 012实际保存/重读＋011后续F匹配及旧运行冻结 | 新F读新内容，旧运行内容不变，码唯一/真实失败 | NotRun；与012共证 |




| FR001—010，P03/04/07 | 单面/多面代表＋必要更多面/E/姿态组件 | 新坐标/轴、真实放回复查、异常槽退出 | NotRun；受DEP及新观察能力限制 |




| FR011—013，P07/08 | 同盘正确分拣与实际下料、取料保存失败 | OK/异常不搬，NG/Pending去配置区，失败不放料 | NotRun |




| FR016—023，P01/05/09/13 | 既有架构正负例、必要取消/期限/保存、清理消费者核对 | 原码/Test知识不回流，失败可定位，无虚构反馈/完成 | NotRun |









同次运行证据可承接多项；不能用旧报告补新义务、把零发现/Skip/失败写Passed。普通非阻塞边界记录待办，之后只有新变化、失败或未解决的直接风险才扩大验证。命令及前提见[quickstart](quickstart.md)，本轮不执行。









## OPEN、外部依赖与决策记录









| 依赖 | 影响/补充时机 | 可继续范围 |




| --- | --- | --- |




| DEP01正式地址 | 正式读写前补映射，不用旧Word地址代替 | 模型/保存/语义与显式测试映射下组件 |




| DEP02 ASCII承载 | 实际型号机械动作前对齐；不能装Float32默认ASCII | F软件绑定、无翻转单面、保存/规划；依赖动作未验证 |




| DEP03/04恢复/安全 | 新恢复/安全控制及现场验收前 | 已确认正常流程设计、必要失败真实记录与既有取消 |




| DEP05/06速度/报警 | 对应参数使用及编码处置前 | 无依赖动作；不填0、方向或等级 |




| 新3D/算法观察能力 | 首次F定位/姿态链实际调用前提供兼容输出 | 共同结果合同、模型与真实失败保护；旧高度结果不能计通过 |




| 012设计接收与修订 | 最终13份已实际合入、1.2消费问题关闭；1.3/G-01设计消费回执及7份增量已接收合入 | 共同代码未交付，限制实际接线；设计消费已闭合，后续增量仍由011集成 |




| 完整独立源码副本及tasks | 实施前准备并获对应阶段安排 | 本轮研究/设计和文档集成已完成，不在此构建 |









实施前在指定011独立副本补齐可构建源码、实际依赖工程/脚本及必要受控配置基线，排除主项目workcopies递归、Git元数据、bin/obj、运行实例/数据库和历史大证据；核独立路径、引用/装配/源摘要，并吸收已正式交付共享改动。不得将当前约束下的最小文档副本假装可直接构建。本轮只生成文档tasks，不复制源码、不进入implement。









上一轮setup-plan仅维护011副本功能选择；显式SPECIFY_INIT_DIR与SPECIFY_FEATURE_DIRECTORY实际返回都在本副本。主项目无Git仓库、BRANCH返回目录回退标识；未执行Git写。当前无before_plan/after_plan钩子。









## 客户确认原型检查（P12）









原型归档为`E:/dzk/gaode/原型.zip`，既有006核验SHA256=`3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0`，页面a.html、data-view.html、login.html；本轮只引用该历史核验，未重新解包或逐页验收。









012仅在授权配方弹窗及已有运行区域消费共同定义/真实状态，归档与无关结构只读。011不变更页面/宿主或新建界面。设计/文档合入完成不代表原型检查、前后端联合链或生产通过。









Phase 1设计保留；本轮只补G-01、接收集成和任务拆解，不重跑plan/checklist。tasks后下一阶段为speckit-analyze，由调度另行安排。









尾段资源承接：允许取盘事件不释放运行运动租约；只有同盘ManualTrayRemovalConfirmation与Final真实提交成功后，共同WholeTrayWorkflowOrchestrator才释放已核无活动/未知动作的原运行租约，供下一轮人工上料启动。失败、取消、未知或未提交保持占用。复用既有ResourceLease，不引入恢复平台。









执行预算/旧出口实施承接（T018—T022）：DetectionRequest增加FrozenBusinessDurations（现有BusinessDurations），只由当前run已冻结Budget.BusinessMs形成；派生动作窗口取各项冻结期限与原阶段截止较早者，翻转/放回、复查3D各用对应项，无固定3s/10s或通用算法预算替代。预算按实际XY/用途Z移动、复查移动采集/释放及保存义务计数；不刷新外层绝对截止。RequiredForOk为true时缺少有效E码不能形成OK，沿已确认DEC-06/OPEN-16缺结果Pending且NG优先，并保码失败原因/内部身份。









旧特殊旋转语义仍留规划/历史来源；本轮新协议没有该路线所需有效机械映射，当前执行在任何产品动作前返回RotationProtocolMappingUnavailable，不使用Test机械HTTP或假动作证明。删除检测期SpecialExit与noAdditionalSorting当前执行豁免，现行分拣不接受旧SpecialHandlingCompleted作为免搬依据；旧字段只读取原历史，实际三区规则依当前姿态/质量。此限制仅该缺映射路线，普通更多面/额外E继续推进。









T026绑定清理落位：RecipeBindingSaveProtectionTests的5项真实SQLite保护已通过（binding-protection-02，01中断保留）。据此删除当前IPlcRecipePort、真实/模拟BindRecipeAsync、容量ACK轮询/占用、UnavailablePlcRecipePort及BA03/BA05专用故障和释放HTTP入口；当前软件绑定不派PLC配方ID/容量。历史RecipeApplication*原payload类型移至HistoricalRecipeApplicationEvidence.cs仅用于原记录读取，当前Receipt仍RecipeBindingReceipt。旧通信容量握手7项及Host健康ACK等待2项失去动作对象而删除，有效期限/保存/取消义务由上述5项、现行通信取消/未知及取料实存门承接；原报告/数据库证据不删。集成夹具的绑定计数改读本独立运行库实际RecipePlanBound写入，不以模拟设备计数冒充软件绑定。012指定源文件不修改。









### Test机械旁路删除实施承接（T026）









010/009已交真实软件绑定、普通配置执行/Flip＋PutBack、三区分拣及未知/保存保护后，删除当前AuxiliaryHandling端口、TestSpecial HTTP DTO/实现/VirtualPlc路由与监控专用消费者；旧JSON历史投影保留。VirtualPlc监控文件VirtualPlc/wwwroot/app.js由011维护，仅删除失效数据源。旧HTTP证据类型仍供通信历史读取，当前TCP动作不伪造HTTP交换。









Station01RuntimeOptions和PlcRuntimeOptions删除TestSpecialActionsBaseUri。Host Program.cs实参由012唯一移除，011已提前在tasks-handoff交准确要求；未接收前不构建Host/Integration，不保无用途参数或排除源码绕过依赖。ReadInitialStateAsync不再请求Test HTTP；正式恢复机械/安全输入仍未配置时返回Blocked并明确RecoveryProtocolNotConfigured，不推断Ready，不清除未知占用。旧Reset/人工翻面协议的剩余迁移另由T009/T026核有效消费者，本批不声明已通过。









旧AuxiliaryEvidenceTests的HTTP动作正例及VirtualPlcMonitor的TestSpecial reset用例已无当前对象：实际TCP翻放关联/未知保护由ActionHandshakeTests.Flip现行6项承接；真实原始交换保存继续通信组件，新增1项恢复缺输入拒绝；旧报告不删除。旧Rotation Test机械链及其驱动在普通更多面/E组件承接后删除，缺正式旋转映射仍明确拒绝，不能换成备用HTTP。









### 初次姿态退出的真实来源承接（T017/T018/T021）









当已冻结计划中全部配置工件都因首次已提交3D观察退出时，检测、翻转、E、分拣不再派发；仍沿共同整盘/下料和人工确认流程。不能因为没有产品拍照而捏造检测算法/相机来源，也不能将异常件补为OK或Pending。









DetectionPortResult新增EvidenceBasis业务枚举（Unspecified、ProductInspection、InitialPoseExclusion）。共同执行器只在实际复核同run/观察write/Call/Capture、原始摘要及关联已保存3D媒体/采集事实后，复用首次3D的真实Camera/Light/Algorithm来源；CaptureFacts保真实原始操作身份，不改成检测采集。完成事件同步EvidenceBasis，状态和历史可区分实际检测与仅姿态退出。缺必要初次保存事实继续拒绝，不能补正常或授权人工取盘。正常路径沿实际检测调用来源。









这是既有姿态退出/真实来源保护的实现细化；recipe-contract/1.3不变，不新增恢复或边界平台。最小新增验证为现有配置执行组件两行：全部首次异常且真实初次保存存在时无产品调用；缺相关采集事实时拒绝；复用已证更多面/E/取消。012不需编辑此共同字段。









人工翻面死分支核查（T026）：全源码/装配/脚本无BeginManualFlipAsync、CompleteManualFlipAsync及WaitingForManualOccupancy消费者。删除这两个私有旧机械方法、其永不进入的hold标志/采样等待/安全豁免分支；实际ManualZoneOccupied、故障、自动模式和取消/未知准入继续阻断，不把现场安全信号改成默认安全。历史ManualHandling枚举及原记录继续可读。仅复核受影响TCP翻放/未知/启动组件，不重跑历史专项。









检测期限转Pending的现行承接：保留最后同运行/动作已关联结果的姿态观察及物理槽参与状态，正常故障槽沿既有技术Pending规则；姿态异常保留独立3D依据和实际Pending处置，排在正常处置后；无料槽不生成任何移动。没有有效观察覆盖则真实暂停并记PendingObservationCoverageUnknown，不把姿态异常伪装为检测质量结论，实际Pending处置独立承接、不重新F绑定，不据过期检测结果冒称OK。分拣仍先于下料，未知反馈不得进入下料；原重试次数/截止/取消和必要提交不变。









T025配置入口落位：Station01RuntimeOptions末尾追加可空PlcMechanicsPath，仅是通信配置文件路径。012在唯一Program入口用具名实参PlcMechanicsPath: section["PlcMechanicsPath"]传入；不把型号载荷或原码放入业务DTO。011 Station01Registration仅经设备构造参数转交文件路径；LatestProtocolPlcDevice在通信层构造时调用PlcMechanicalConfiguration.Apply读取一次plc-mechanics/1（purpose/sourceReference/posePrograms/sortingSafePosition）；同连接生命周期固定，不动态切配置。不提供时保空映射/空安全位，对应动作按现有明确缺输入拒绝，不回退第二文件。显式Test配置只供Virtual；Production仍须已确认来源及正式协议定义，不由本文件批准现场值。源码/普通文件解析组件可先交；实际Host接线与代表链验证待012入口消费及必要输入齐备。









T025当前输入结构补齐：001公共配置schema定向增加algorithms.trayPose，预算schema增加trayPoseAlgorithm/flipCompletion/putBackCompletion；与已交Domain字段一致。旧height/clampCompletion保历史读取字段但不再是当前启动必需，当前不回退测高或夹紧。缺TrayPose及对应预算仍按既有准入拒绝依赖准备流程，翻转/放回预算在冻结计划处验证。011维护这两份后端schema，当前产品/配置变更仅独立副本、随稳定批交付；不写主项目运行配置。单面/多面具名输入在本功能examples/joint准备，全部明确Test来源及预期，未运行不称联合通过。









T026旧测高调度承接：只读核AlgorithmRuntime公开Invoke的Height已无现行业务调用，实际调用仅算法租约/期限/SQLite保存保护测试。将这些有效用例迁移为TrayPose及已加载的明确姿态配置/预算，再删除Runtime的Height派发分支与Height预算选择；保旧原始payload/字段的历史读取。当前ThreeDStep/复查仍消费真实Observation，不提供测高回退。SimulatedAlgorithm/旧worker独立测试等剩余消费者单独核查，不能据本次Runtime迁移宣称全部旧算法代码已清理。









T025保存/冻结联合承接：在同一既有代表驱动中，通过012正式HTTP完整读取及If-Match保存明确Test质量配置引用，先保存再启动F；真实V2/Intent/Bound形成后再保存第二版本。核当前运行的正文摘要、版本和计划保持初次冻结，活动目录/完整GET使用第二版本，实际执行事实仍关联原计划。仅编辑具名Test引用，不改变现场参数、不新建执行器/保存器；每条链仍只启动一次。原始HTTP响应/ETag及真实SQLite读取同run记录。正常重启及012页面仍须实际已交入口/生命周期安排，不以源码或旧报告补齐。









M10当前收敛：机械载荷解析必须在Infrastructure通信叶适配中，Host Composition只转交文件路径，不调用含原码的解析器。LatestProtocolPlcDevice构造的可空mechanicalConfigurationPath参数承接已交Host路径，由通信适配器内部读取；不向原始PlcRuntimeOptions增添业务可访问属性；无第二来源或缺值兜底。联合驱动只依赖具体持久事实读类型，不导入含原始通信证据的整个Persistence命名空间。保持原架构规则/分类/负例，不扩大白名单；本次先改设计再改源码。









T025正常重启实施：scripts/011-owned-host.py仅负责011既有驱动启动的Host子进程，Windows独立隐藏控制台/进程组，控制输入只发关闭请求到已记录的本组PID，不按进程名/端口扫描终止其他会话。驱动在Final/输入释放后请求正常关闭，核真实Host关闭日志的流程/写入/资源排空及退出码，再用同库/同配置启动只读对账；不新开配方运行。原强制kill仅异常收尾，不计正常重启。脚本归011，012只消费启动/采证安排，不改其Program或业务端口，不新增生产控制接口。









T026低层死分支定向清理：VirtualPlc原合轴Move、采集ZReset已无PendingAction生产调用，删除其完成/失败/重试/审计分支和无调用MoveZSignal、ResetMoveFeedback；保实际独立Axes、Flip/PutBack/Sort、安全/心跳与实际复位代次。旧StartTrace只服务已替代的PC_Start/夹紧断言，一并删除该虚拟追踪及失效消费者；有效连接/旧反馈/保存/取消保护由现行ProtocolStartup、IndependentAxis、Flip关联及Pick保存组件承接。此批不声称所有旧控制/地址已清完，区域/夹紧/旧复位握手实际消费者继续逐项迁移，不猜新恢复。









T025/012同期采证落位：唯一既有驱动在真实Host就绪、POST运行之前发布page-connection.json（实际API地址、具名输入、012证据根、仅本用户命名管道名，无token）。012通过该一次性管道取得本轮临时Test读取凭据并用已交T022观察器连接；实际ready.json匹配API才启动。等待仅发生在运行期限开始前且有限；运行后Final页面文件作为完成对账，不作启动前置。Final后留有限采证排空窗口再正常关闭/重启；不延长业务预算、不增加运行或设备动作。011不代写012页面证据，异常/缺证如实记录。









T009/M02独立预期定向迁移：旧AllConfirmedFields全旧表/旧分拣码断言由CurrentAxesAndMechanicalFeedbackMatchIndependentLiteralOracle替代，按既有人工confirmed-011验证五轴方向/类型/地址/码及翻放/分拣码；20260925原件保历史。Float32四字序和访问间隔/非法长度保护保留，间隔夹具改200/210、Test容量256以避新模型载荷而非放宽访问门。当前仅这3方法加受影响IndependentAxis(2)/ProtocolStartup(2)/Flip(6)及有效连接/心跳/陈旧安全(3)，预计16项；不运行旧009全专项。结果待实际记录。









T015/T026现行运动状态消费者修正：通信Sample仍从旧单Z状态推导MotionStatus，虽业务动作由pending/auxiliary占用，但独立PLC轴活动可能被查询为Available。迁移为五轴实际触发＋各自反馈解码；当前移动输出InUse，未知码/超时保持HeldUnknown，未触发初始0不伪称运动。保连接/安全/当前动作保护；只改通信投影不改DTO、原码不泄漏。扩展既有IndependentAxis两项中的真实TCP观察断言，不新跑完整链或追加组合。














T025输入接线：single-03真实XY与检测Z均到位，但两段运动及取证超过原8000ms完成期限。唯一驱动遗漏具名Test simulation.json stages.xyCompletion.delayMs=500，VirtualPlc用了默认3000ms/轴。驱动现显式传该500ms，并以无随机抖动执行确定Test延时，保存源及摘要。业务预算、位置校验、保存门、通过断言保持，03失败原证据保留；这不表示旧输入通过或现场速度获准。下一单面04使用相同驱动修订，仍须实际验证。














多面01实证修正（T010/T019/T024）：第二面受理2秒期限先到，后台整表轮询尚未让出动作推进；旧MoveAndBegin失败后只标租约Unknown，未取消设备请求，导致期限后仍下发轴触发。保持原期限，运动请求在准入后由同一串行动作任务及时推进，不等待下一整表轮询；仍等上一动作结束且核当前身份/代次/安全，不新增并行动作或回执假成功。每次业务移动持独立链接取消令牌，超时/取消立即取消未完成请求并保持Unknown。共同DetectionResultKind增UnknownHeld（当前执行合同修订，recipe-contract/1.3不变），失去运动确认以此返回并持久化UnknownHeld，不得显示可重试或转Pending分拣；未派发/纯算法失败按原语义。









T015实际页面对账修正：single05实际已提交Sorting后，RunSnapshot.SortingState仍沿初值NotStarted，不能接受该显示作为正确分拣状态。共同RuntimeObservationProjection只从同run/tray/冻结Plan且摘要有效的已提交Sorting事件形成状态；多个预留操作全部有完成事实才显示Completed，未知保持及失败不得显示成功。查询与通知共用投影，sortingState纳入changedFields/修订摘要。保原单面失败字段证据，仅对新投影组件和同一多面代表复核；012不新增工艺判断。









T010启动受理实证修正：multi02在012真实ready后已创建run=f235db29-9c67-495f-a740-f033df123fa3，StartAcceptanceUnknownHeld，无F/Final，Host正常退出0。启动旧AdvanceStart仍等两次整表轮询，未沿023同串行任务即时推进。启动也在同一actionAdvance内发送就绪并等同代次、新采样起点的实际Ready/安全观察及证据真实提交；保持2秒受理/取消门。常规轮询只读Sample/AxisMotion实际消费的观察信号，不再无意义轮询PC配置/载荷/命令全表；不减少安全/心跳/当前反馈或扩大读取范围。必要ProtocolStartup原2项将实际请求窗口限定2秒；不重跑整套协议。下一链预留multi03，先代码/组件稳定及012新准备再启动。









T026本批定向实际删除：VirtualPlc的ProcessManualConfirmation/ProcessRetryCommand/ProcessZoneConfig/ProcessPalletLock、PalletLock/ZoneConfig动作/故障/时长及无消费ZReset模拟时长不再被当前启动/翻放/分拣调用；旧重试不能在未知后重放机械动作，旧人工线圈不能清报警授准入。当前Flip/PutBack/Sort/五轴的安全条件保持原值，取消/心跳/已提交人工取盘协议保持。核查9个实际消费者（obsolete-control-consumers-before.json）；两个VirtualPlcSafetyGateTests旧解锁故障方法/包装随替代删除，未知不重放/保存失败不Final义务已有motion-cancellation、Pick保存及人工取盘组件承接。009原始test-obligations和历史失败原件不改，活动inventory将已删除路径移入replacedFiles并记承接；不是通过删失败换通过。012指定文件均不在修改清单。旧协议监视/历史读取及未核其他符号不据此全删，本任务仍需最终核对。








T026测高生产端最终承接：AlgorithmRuntime当前只派发TrayPose/FDecode等现行能力，PythonWorkerAdapter、ContentSampleWorker和SimulatedAlgorithm旧Height产出不再是历史读取所需，删除其生产/解析分支。保Height枚举、旧payload及历史数据库读取原值；无当前执行回退。SimulatedAlgorithm仅支持其实际具备的FDecode，未接TrayPose等在受理前明确拒绝，不发布空Result或正常姿态；NotIntegratedAlgorithm按实际Role记录调用。原模拟取消/有限退出/F原始码和worker真实媒体/关联/InputReleased义务保留，旧Height独立断言迁移为现行内容能力和明确拒绝；复用已有011真实TrayPose媒体/进程测试。当前单面/多面证据按其实际代码摘要保留，清理后只补受影响组件/完整构建/当前边界，不再次跑代表链。






T009/T026失效ACK/区域字清理：当前翻转/放回和分拣已有各自动作事实及提交门，旧Sorting_OK/Flip_OK/Retry_Cmd、区域数量/Ready/Ack及PLC整数Recipe_ID不再消费，删除其活动Test映射、码表和无用复位/UI描述。SignalId保其余成员既有数值，避免改变保留成员身份。ReadInitialState仍因RecoveryProtocolNotConfigured明确阻断，不把去掉旧ACK检查当已实现恢复。共同RecipeId/PLC型号/实际配方容量不受此通信字段删除影响；正式地址/ASCII仍待输入。旧全表规格断言已由confirmed-011独立预期替代，只保其中有效宽度/所有权和访问计划保护，不重跑009全套。




T015/M11真实重启读取修正：multi03同库页面补读发现内存snapshot缺失时配方绑定/整盘状态使用初值，不能当正常终态显示。共同RuntimeObservationProjection必须依同run/冻结Plan的真实已提交Intent/Bound/Handoff/Receipt和Final事件构造当前读取；校验摘要、关联、必要回执及提交依据，不凭运行State单值补Bound或Final，不读取新活动目录，也不授历史运行续接。增补必要历史投影组件和同一库仅GET复核，不重开代表链；原未通过显示/报告保留。


T009/T026必要定义组件承接：retired-ack-components-01为9项5过4失败。四项自定义协议正例沿用旧ProtocolTcpFixture(input!=null)不装真实CommunicationEvidenceRecorder，当前启动必要保存无法完成；夹具同时只等Accepted而吞掉Failed诊断。删除该测试专用装配差异，所有协议组件均使用真实独立SQLite证据写；正例实际Failed立即失败并输出设备诊断，保原受理期限与所有断言。不通过无回执放行或重跑已过5项解决；只复核受影响4正例和一个原拒绝负例（确保无通信派发）。

T009/T026采集复位失效消费者定向迁移：新协议采集结束不发送Inspection_Status/Z_Reset ACK。旧5行Capture正负例迁移为实际独立轴/用途、错误动作身份拒绝、未提交或未释放输入拒绝、真实TCP断开后HeldUnknown；有限窗口和SQLite分段通信证据/轮询覆盖仍保留。删除VirtualPlc无生产者的ZResetFailure/ZResetFeedbackHold故障；现有DetectionCommunicationFixture的释放故障在真实关闭时断开其自有TCP服务，不伪造释放结果。旧009脚本F04-reset在任何进程启动前明确拒绝已替代场景，原历史证据及清单不改。仍只运行受影响采集组件，不重跑009历史流程。

T009/T026当前监视字段最终迁移：旧PC_Start、Manual_Flip_Complete、合轴命令/反馈、单Z状态、Flip_Current_Face、Sorting_Part_Index、Pallet锁命令/状态及采集/复位ACK已无当前动作生产者，活动Test定义、码表、轮询私有状态、虚拟PLC处理/审计/UI中实际删除。有效独立五轴、Flip/PutBack/Sort、真实就绪/安全/心跳不变；恢复检查继续明确RecoveryProtocolNotConfigured，不补机械/安全事实。历史载荷和原证据不改。两种有限TCP响应故障改对实际X轴触发，不能再识别已删除的“Unload原码”；旧无生产者的UnloadMissing/Previous注入和无效原码断言删前由现行反馈/UnknownHeld/无重放保护承接。必要旧测试按新语义迁移，首次构建全部消费者齐备；不以保错字段、排除编译或历史全量复跑解决。

T026孤立人工翻面交互清理：以当前recipe-contract/1.3的动作定义、实际共同执行无ManualFlipInteraction.WaitAsync消费者、旧手工确认通信线圈已被替代为依据，删除当前孤立服务/注册/manual-flip GET及confirm POST/ConfirmManualFlip许可生成和其两项失效整链断言。旧批准工艺、原操作者/面来源和证据仍按历史适用范围读取；本轮不为其增加替代机械信号或宣布现场恢复。RunSnapshot.WaitingManualFlip/ManualFlipProjection保留历史JSON字段，不生成当前续接许可。保真实人工区域安全阻断；有效权限、对象关联、保存、期限、取消分别由当前翻放/UnknownHeld及人工取盘组件承接。012独占清理运行页面此孤立消费分支，保原型和当前人工取盘。删除前消费者表与旧摘要在retired-manual-consumers-before.json，产品服务无当前调用；不因测试失败删除有效保护。


T026孤立类型核查（2026-10-04）：MeasurementOffsetBasis仅余声明，无当前/历史reader、装配、脚本或测试的类型消费者；旧历史payload按原JSON读取并不依赖此CLR类型。删除该孤立声明，不保名为历史的空壳；CoordinateResolutionKind旧枚举值及实际DeviceHistoryProjection.HeightRound等仍服务原JSON读取，保留其原值且不授当前执行。共同1.3字段/序列化形状不变，012无迁移字段；完整必要工程编译与当前公开形状门禁验证此删除，不新增用例或链。

## 013实施前定向同步（2026-10-04）

SY-05/06：FR-001/013/016—023、PC02—05及执行状态沿013 A01—08/D02—07：两连接、单源用途采集；H300、B200/500、动作200、位置500/1000ms，首Moving/Executing局部50ms齐备后恢复200；原期限/取消/未知及中间态→完成→到位后实坐标因果保持。各块真实身份，基础与位置独立可靠性；最终位置发布后才返回完成，关键准入/采集释放即时核查。Host位置增加SampleStartedUtc、SampleEndedUtc、ConnectionEpoch、Reliability并升live/1.2，Domain和持久原格式不变。预算schema2.0/代表预算2/模拟2（模拟schema1.0）及Start/Host/fixture引用先迁移。实际取料、原始证据与有效在途提交后才Place不弱化。正常HeldFlip原5秒失败保存不强求未达1024的段；自动阈值真提交接续另用通信证据组件，原缺口拒绝独立保持。013每侧一次同run-2+空闲及必要组件/完整L/受影响009；真实只读API观察器只替013后端负载前置，011/012页面证据义务与历史报告不改。原T009/T010正式PLC缺口仍局部限制，历史任务勾选不变。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

当前012独立副本追加局部采集引用和夹爪业务字段设计，见012 ui-fix-design-20261004.md。共同规划只消费配置，不新增执行器/PLC信号；不改013。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

前轮specify仅确认需求同步；本次014 Phase 1及012配套设计见当前设计引用，不生成新tasks；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
