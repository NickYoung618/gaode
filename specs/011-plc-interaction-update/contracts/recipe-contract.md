# 共同配方合同 recipe-contract/1.5（当前设计；集成代码1.4）

**当前状态**：2026-10-05主项目已实际集成1.4/正文3；本会话统一负责014共同后端与012前端配套设计。RC10为本次1.5 Phase 1准确增量，未实现/验证。原1.3/1.4交付、独立副本和011集成责任属于历史时点，实际记录保留，不作为当前待交能力。规范性业务定义仅本文件；其他设计消费它。

**唯一定义位置**：`specs/011-plc-interaction-update/contracts/recipe-contract.md`。实际共同代码沿`backend/src/Gaode.Application/Recipes/RecipeContracts.cs`及`ExecutionInputs.cs`扩展；唯一业务校验为同目录`RecipeDefinitionValidator.cs`，准入/冻结沿`RecipeAdmission.cs`。RC01—09按实际1.4集成范围消费；RC10给出准确新增类型与序列化设计，当前代码尚未消费。

## RC01 身份与版本

| 共同字段 / 现有承接 | 含义及约束 | 谁生成/消费 |
| --- | --- | --- |
| RecipeId | 配方稳定身份；编辑保持身份，新建产生新身份；不能由PLC型号或扫码内容替代 | 服务端调用011唯一身份规则生成，012持久化，011绑定及运行记录 |
| FCode（界面称料盘编号） | F实际解码文本就是匹配码；目录内不同RecipeId不得占同一码。按原文本精确匹配，不换成测试码或配方ID，不添加隐含大小写/前后缀转换 | 012存储/目录；011共同唯一匹配 |
| Model | 下发PLC的产品型号，保留已确认ASCII大小写语义；不等于RecipeId、FCode或显示编号 | 012编辑；011语义请求；通信层编码 |
| Version | 服务器生成的不透明内容版本，保存成功响应取得；不得由浏览器指定“已保存版本”，不按字典序大小比较 | 服务端在提交中调用011唯一版本规则生成，012持久化，011冻结原值 |
| DefinitionDigest（新增） | 同一共同定义的内容摘要；011统一计算。覆盖业务/配置/准入内容，排除Version、DefinitionDigest、CatalogDigest等派生元数据；字典按键序规范化，数组保持业务顺序 | 校验、保存读回、冻结对账共用 |
| CatalogDigest | 本次目录视图摘要，仅用于来源和一致性；不是单配方身份，也不是强制后续F使用旧目录的锁 | 012目录视图、011绑定记录 |
| PlcRecipeId（旧） | 现有数字显示号仅保留实际历史读取消费者；新设备应用使用Model，不用该数字冒充型号 | 删除前核旧查询；不成为新保存/执行必填 |
| ScenarioId / Route / UnitKind | 保留现有场景、执行路线、单件/成组/整体含义；场景只在F找到配方后核相容性，不另作跨配方重码命名空间 | 共同校验/规划 |
| Approval / ReleaseStatus | 保留现有用途、槽位/能力及生产准入依据。客户端不能借保存、改用途或改状态授予生产许可 | 012保留受控元数据；011准入 |

Version采用一次成功提交对应一个新不透明令牌的最小实现；不新建发布、分支、回滚或审批平台。摘要函数与类型由011交付，012不得自行发明另一种内容判断。

## RC02 配方内容：复用已有结构

保留现有RecipeDefinition的Composition、Positions、Stages、ECode、Disposition、CaptureProfiles、AlgorithmRequirements、ExecutionPositions、MotionProfile、QualityProfile及适用容量/批准引用；下表定向收敛受影响字段。

| 结构 | 目标字段/关系 | 共同校验规则 |
| --- | --- | --- |
| Composition / RecipeMaterial | Material、LocalFaces[] | 检测面身份非空、正值、对象内唯一；列表没有1/2/4面硬上限 |
| Stages / RecipeStage | Number、Targets[]、已批准动作类别；Targets含Material、LocalFace、CameraPair、CaptureProfile、AlgorithmProfile | 有序且必检目标恰好覆盖；CameraPair仅AB/CD。每个普通路线实际四面检测配置必须恰好1AB+3CD，AB位置由配置；更多面不套固定组合 |
| Positions / RecipePosition | SlotId、PhysicalSlotIndex、UnitPattern、Members及点位引用 | 原物理槽号稳定且唯一；筛除空/异常槽不重编号。旧protocolSlotIndex只作历史读取映射，新传输使用physicalSlotIndex |
| RecipeMember / 物理实体 | MemberPattern、Material、所属Unit及Handling关系 | 检测部位、组成员与搬运实体分清；整体动作一次，异常按实际物理槽映射，不推测新的组处置 |
| ExecutionPositions | 继续按SlotId索引SlotExecutionInputs，区分PhysicalEntity和Members | 输入必须与Positions/对象身份对应；不得按步骤号推导物理槽 |
| CoordinateDefinition / Coordinates | PointRef、Point(Id/Version/X/Y/Unit/Frame)、配置Z及Datum、ConfigurationVersion、SourceFactReference、ObjectPattern、SlotId、PhysicalSlotIndex、LocalFace、Camera、StageId、用途 | 当前检测XY和Z全部来自配方/点位配置；配置单位/基准/限值有来源。不要求旧HeightRound/MeasurementOffset才能执行；缺字段不得填0 |
| ObjectExecutionInputs | Source、Coordinates、Flip.Stages、PurposePoints、可适用旧Rotation、Sorting；具体类型及路径见RC08 | 检测、翻转取件/放回、分拣源/目标分别配置及校验。料盘位置相同不合并点位 |
| 同盘三区 | Positions对应初始OK槽；Sorting按NG/Pending区域分别配置目标和适用容量/占用依据 | 普通OK分拣不搬；特殊OK必须回本件原槽；NG/Pending使用对应区域目标，不能推定额外OK放置区；不从样例坐标/容量批准生产 |
| CaptureProfiles / AlgorithmRequirements | 沿现有类型的参数、版本、用途、输入数量、结果合同 | 共同校验引用完整与能力相容；实际就绪失败另有有限终态，不把当前worker是否运行作为保存门 |
| MotionProfile / QualityProfile | 沿现有受控配置引用 | 有效运动/结果处置保护继续；未知现场参数不设置默认值 |

检测Z可沿已有ApprovedFixedBasis表达配置值/单位/基准/适用批准与配置版本，不再要求先有旧测高结果。旧测高结构是否保留，只为确有消费者的历史读取；不建当前执行兼容分支。

## RC03 E扫码与独立姿态

复用RecipeCodeRule.Enabled、RepresentativeMaterial、BindTo、RequiredForOk、CaptureProfile、AlgorithmProfile及已有在检测面扫码的配置能力。增加可空ExtraPose：

| ExtraPose字段 | 含义 |
| --- | --- |
| PoseId | 稳定的扫码姿态身份，独立于LocalFace，不计入检测面或必检面数 |
| TargetPose | 本配方需要的目标姿态语义引用，由有效PLC型号程序/通信配置对接；不是PLC原码，也不自动赋“5” |
| ScanPointRef | 当前对象在该姿态的扫码点配置，X/Y和扫码Z、单位/基准/版本必须可解析 |
| PickPointRef / PutBackPointRef | 姿态建立涉及已确认取放动作时引用各自用途配置，不以扫码点替代机械取放点 |
| CaptureProfile / AlgorithmProfile | 实际E采集和解码配置，复用同一能力/来源/保存边界 |

ExtraPose存在时，本轮已确认的安排是四个检测面完成后独立执行；它与ReadAt的检测面时机不得产生重复E步骤。关闭时不生成额外姿态。更多面本身已获支持，但不由此推导“任意第N+1姿态”或新的机械程序；超出已确认额外E适用安排的配置指出具体不支持原因，不能静默改变顺序。

E使用扫码Z，不使用检测Z或抓取Z反馈替代。姿态建立只调用已确认的共同取件/翻转/放回动作能力，实际机械程序留PLC。若某TargetPose缺有效通信映射，只限制该姿态实际派发，不能用数值5填补；合法业务结构、保存和其他已确认配置可继续。扫码结果保持物理实体/槽、PoseId、step、capture/call关联，不能因未取得E码伪造码或重新绑定F。

## RC04 唯一业务校验与读写边界

保存与运行使用同一RecipeDefinitionValidator。共同校验包含结构/身份、全目录FCode唯一、配置用途/对象/物理槽关联、面序/组规则、额外E完整性、参数引用及有限预算依据；保留既有有效保护。保存校验不依赖实时设备反馈，不代替RecipeAdmission的运行准入。

目标应用层签名（本轮不写代码）：

```csharp
RecipeValidationResult RecipeDefinitionValidator.ValidateForSave(
    RecipeDefinition candidate, RecipeCatalogSnapshot current);

RecipeCatalogSnapshot IRecipeCatalog.GetSnapshot();

Task<RecipeSaveResult> IRecipeStore.SaveAsync(
    RecipeSaveRequest request, CancellationToken cancellationToken);

RecipeMatchResult RecipeMatcher.Match(
    RecipeCatalogSnapshot snapshot, string trayCode,
    RecipeSelectionIntent? selection, string scenarioId, string purpose);

RecipeRunPlan RecipeRunPlanner.BuildExecutable(
    RecipeDefinition definition, string trayRunId,
    IReadOnlyList<string> occupiedSlots);
```

- RecipeCatalogSnapshot：SchemaVersion、CatalogDigest、不可变Definitions[]；同一视图支持目录摘要、按RecipeId完整读取及F匹配。012不得仅给011目录摘要。
- RecipeSaveRequest：Candidate（共同RecipeDefinition业务内容）、TargetRecipeId（更新目标，新建为空）及ExpectedVersion（编辑时从实际读取取得，新建为空）；RequestId只作请求关联。服务器管理的身份/版本/摘要/准入元数据不从请求取得授权，具体形成与HTTP映射见RC04.1。
- RecipeValidationResult：Valid与Issues[]，每项Code、FieldPath、Message及适用对象引用。012负责格式/HTTP校验和显示，共同工艺校验仅调用这里。
- RecipeSaveResult：Saved / ValidationFailed / VersionConflict / SaveFailed / CommitUnknown；Saved才附实际持久化的RecipeId、Version、DefinitionDigest、CatalogDigest与完整内容。实际提交未知不能回“保存成功”。
- IRecipeStore由012实现；011定义共同接口/结果语义。具体路由、读写权限及序列化由012设计，同一Host Program只装配一个提供者实例供保存/目录/F读取。
- 写入串行边界内读取当前目录、核ExpectedVersion、调用共同校验并提交，确保不同配方不能同时占码；确认真实提交后回复；随后开始的读取从同一持久来源取得一致视图，不依赖另一个活动缓存发布步骤。发生实际保存失败/未知时如实返回；不声称已回滚尚未核实的写入。
- 新建/编辑的必要点位与配置完整性应通过共同规则；生产批准未齐可明确Restricted并真实保存，不把保存当生产准入。缺必需业务配置的校验失败不能假称已存可运行配方。
- 磁盘序列化是适配格式，不将文件路径、测试编号、图片路径、worker特征或通信原码加入共同业务结构。

### RC04.1 1.2保存适配与HTTP条件更新

1. **唯一端口**：012的薄RecipeAuthoringService如保留，只调用本合同IRecipeStore及共同检查能力；不再定义功能重复的“012专用保存端口”。SQLite、表名、事务、SaveId均不进入共同目录/保存接口。
2. **身份形成**：新建时TargetRecipeId=null、ExpectedVersion=null；服务端在本次保存内调用011唯一身份实现产生RecipeId。更新时TargetRecipeId来自路由，ExpectedVersion来自所编辑的完整读取结果，RecipeId保持不变。Candidate复用共同RecipeDefinition业务结构，可不提供服务器管理元数据；若提交这些只读元数据，不得据其授予身份、内容版本、摘要或准入。更新正文已有RecipeId必须与目标一致。检查入口允许新建候选缺少服务器只读身份，不因检查生成已保存身份/版本；保存阶段形成服务端身份后仍调用同一校验规则。
3. **唯一实现位置**：011在Application/Recipes/RecipeDefinitionIdentity.cs提供CreateRecipeId、CreateVersion、ComputeDefinitionDigest；012在真实写入边界调用，不自制另一套版本/摘要。新身份和每次提交版本使用服务器生成的不透明唯一令牌；失败尝试生成的令牌不能作为已保存版本返回。DefinitionDigest采用SHA-256，对共同正文规范化：字段/字典键稳定排序、业务数组保序、文本不隐式变换；覆盖RecipeId及业务/配置/受控准入内容，排除Version、摘要、存储/HTTP/请求审计元数据与动态就绪投影。规范化与摘要只实现一次。
4. **条件更新**：完整GET及保存响应的ETag必须准确关联目标RecipeId与本次共同Version。012确定传输编码，并把原读取ETag中的该Version原样还原为ExpectedVersion；不能拿SaveId、DefinitionDigest或提交时最新Head版本代替。更新条件缺失或不能代表单一明确版本时不得无条件写。IRecipeStore在同一写事务内比较当前Version与ExpectedVersion，不符返回VersionConflict；HTTP映射沿012的428/412及错误信封。
5. **保存结果**：Saved仅在实际提交确认后返回真实RecipeId、Version、DefinitionDigest、CatalogDigest和完整Definition；ValidationFailed携唯一校验Issues，VersionConflict不覆盖现有内容，SaveFailed仅表达已知失败，CommitUnknown不声称已回滚或成功。实际提交后回包/重读失败与未保存分开；取消不证明数据库事务撤回。不以某张表存在或日志写入代替提交。
6. **单一持久来源**：按本轮调度决定，012使用独立SQLite配方库，运行事实库继续保存运行/冻结/动作/结果。该选择仅落在012基础设施适配；正式编辑、完整读取、目录、F GetSnapshot消费同一个配方持久来源，不同时保留活动文件目录，不在库缺失/读取失败时回退Review/File。Head与完整正文在一次一致读取中形成不可变RecipeCatalogSnapshot。事务提交之后开始的新读取必须见到已提交内容；与提交重叠的读取可取得其一致时点的旧或新完整视图，不拼接字段。已冻结运行不再读取Head。
7. **内部SaveId**：如012两表仍需记录主键/Head指针，可保留；仅用于持久行关联与诊断，不作公开业务版本、ExpectedVersion、F匹配或ETag的替代规则。历史不可变正文保留真实关联；没有新增版本管理页面或跨配方库/运行库事务。
8. **有限等待**：目录读取与保存均由012适配落实既有显式正有限存储预算/取消观察，不占PLC运动锁；011绑定总窗和原截止不因数据库接入而刷新。实际读取失败明确传播，不能返回空成功目录或旧缓存冒充当前视图。

## RC05 F匹配、选择意图与冻结

RecipeSelectionIntent复用expectedRecipeRef的RecipeId，原Version/CatalogDigest保留为ObservedVersion/ObservedCatalogDigest的展示来源含义；传输迁移由012消费011定义，当前生产者/消费者一起更新，不建多版本执行兼容层。

1. 正式启动核授权、请求身份、公共配置和当前公共准备条件；选择信息不能代替F事实，也不因展示内容过期阻止必要公共准备/F。
2. 首次3D真实定位F；扫码输出就是料盘编号。未匹配前不执行依赖产品配方的动作。
3. F解码成功后，011只调用一次GetSnapshot；Match按全目录FCode精确唯一查找，再核选择RecipeId、场景、用途及既有准入。输出Matched / Unmatched / Ambiguous / IdentityMismatch / Restricted及具体原因。重复目录仍须拒绝，不任取首项。
4. Matched携该视图内完整Definition、RecipeId/Version/DefinitionDigest/CatalogDigest；准入、规划、能力绑定和冻结只消费这一结果，不再次Resolve。
5. 构造深不可变FrozenExecutionInputs，记录实际RunId/TrayId/料盘编号/配方身份/内容版本及摘要/PlanRevision、配置与能力/预算；保存绑定意图、实际业务绑定及移交，以当前有效真实提交回执完成。新F绑定是上位机业务操作，不要求旧RecipeApplied/配方ACK，不登记虚构物理动作；型号随实际翻转动作下发。Matched本身不是Bound或可检测。
6. 后续保存只改变下一次F匹配读取内容；冻结运行不引用可变目录集合，不更新面、点位、型号或预算，也不重绑F。
7. 不匹配、必要保存失败、反馈未知、取消或过期都保持原真实状态和已有事实，禁止依赖动作。公开结果不得伪造成功。

RC05历史1.3阶段FrozenExecutionInputs/1承接为execution-inputs/2，后已实际实现；本次新增冻结3按RC10，旧1/2保持原读取语义。原v1负载和旧handoff摘要不改写，历史读取保持原语义及Unavailable；新执行不得把缺字段旧记录补默认值后续跑。运行保存仍沿既有RecipePlanAndBindingIntent，不新增版本管理数据库。

### RC05.1 业务绑定回执（1.1定向修订）

RecipeApplicationCoordinator继续作为唯一绑定协调器，但移除新绑定对IPlcRecipePort及DeviceRecipeApplied的依赖，返回RecipeBindingReceipt：BindingId、RunId/TrayId、RecipeId/Version/DefinitionDigest、PlanRevision、ActionWindow、实际IntentCommit/BoundCommit/HandoffCommit（该入口适用时）、ReceivedTick及Validity。只有当前有效且全部适用真实提交齐备才Bound；不含伪DeviceApplied或伪CommunicationEvidence。

绑定预算继续使用原已批准预算来源、意图提交后唯一t0及更早已有截止；取消/过期/提交未知不得续接。删除StartPublicPreparation/IndependentRecipeApplication中只为旧PLC绑定登记的BeginSpecialAction及假设备完成占用，保留运行互斥/控制准入。独立bind不生成检测续接许可，响应productContinuationAuthorized仍为false。旧设备应用记录由有限历史reader如实读取，不补写成新绑定。

本修订只消除已被新协议替代的旧握手前置；实际翻转/放回/分拣的设备完成和保存门禁照常必须。ASCII承载未定只限制相关型号机械动作，不阻止无翻转的单面软件绑定。

### 当前独立plan/bind入口落实（实施增量，不改recipe-contract/1.3正文版本）

011提供`CommittedRecipePlanReader(ITraceQuery,IStageHandoffQuery).ReadAsync(Guid runId,string scenarioId,IReadOnlyList<string> occupiedSlots,CancellationToken)`，返回可空`FrozenExecutionInputs`：无当前可靠V2返回null；有V2则核同run/tray、场景/槽范围、绑定引用、真实已提交Intent的摘要及其execution-inputs/2内容/PlanRevision/FCode/用途。不重新读取活动目录、不重新匹配或生成旧run计划，不以V1执行续接。旧V1只留历史查询。读取不授产品动作许可。

012唯一修改RecipeEndpoints的plan/bind装配：共同reader替换BuildPlan内V1/活动catalog/LoadPublic当前用途分支；两端使用reader返回的Plan，容量取该Plan的完整配置，不能补options默认值。IndependentRecipeApplication仍使用已冻结Public/Budget/Simulation、原截止、运行互斥及取消、实际Intent/Bound保存；核提供Plan等于既有冻结Plan，独立bind回执仍productContinuationAuthorized=false，不改变原run冻结或允许重复检测。正常F主链继续只匹配一次，不经过独立端点。

## RC06 运行与公开状态

实际运行、姿态检查、实体处置、异常物理槽号、分轴观察及changedFields唯一定义见[station01-execution/1.0](execution-and-state.md)。012只绑定真实已提交字段，不从质量结果、本地面序或通知顺序推导动作/完成。

料盘运行内部TrayId（关联标识）与扫码料盘编号FCode区分；物理槽PhysicalSlotIndex与RecipeStep.Sequence区分；技术状态、检测完整性、OK/NG/Pending质量、姿态状态、物理处置及最终保存分别表达。

## RC07 012可立即消费的交付

当前业务合同仍recipe-contract/1.3，012消费同一模型/校验/序列化、实现目录/完整读写并映射授权配方弹窗，不另建面/E/分拣校验或执行器。代码批次分别为D011-common-code-1.3（T003—T005基础、端口、Matcher和深冻结）、D011-runtime-binding-1.3（T011/T012真实绑定/移交及注册清单）、D011-runtime-state-1.0（T015真实查询/通知）；012分别由T002及保存/T018、T020、T021消费。批次划分不新增业务版本或版本平台。

D012-store-api-1.3只含012 T017真实保存/完整重读/版本及相关必要证据，T019适配迁移和T020运行接线另行接收。RC08直接类型消费者按[交接记录](../tasks-handoff-20261003.md)随基础源码分批迁移，相关首次构建先核齐双方该项目的直接消费者；不等最终联合验证、不以类型已交称Host可用、不保旧当前字段/兼容旁路。源码范围、实际构建范围和已验证能力分别登记；完整绑定/运行并未因此提前通过。

固定共享文件：
- `backend/src/Gaode.Host/Api/RecipeEndpoints.cs`：012唯一编辑，调用上述共同入口；不得用旧plan/bind端点另拼主流程。
- `backend/src/Gaode.Host/Program.cs`：012唯一编辑，接011共同注册与012同一目录/保存实例；不恢复整段Test执行器。
- `backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs`、RecipeCatalogFactory.cs、RecipeEnvironmentDecoder.cs、SemanticRecipeInputProvider.cs：012唯一编辑格式和持久边界；删除无用途换码/测高输入前按011清理表承接消费者。
- `backend/src/Gaode.Host/Composition/CapabilityRegistration.cs`、AdapterBindings.cs、Station01Registration.cs：011唯一编辑共同能力/正式执行装配，提供调用要求给Program负责人。

012最终13份交付已消除专用保存端口、SaveId条件更新、两解码文件归属及首版交付状态问题，不再列作当前缺口。G-01生产端定义见RC08；新消费确认与实际合入结果见[任务交接记录](../tasks-handoff-20261003.md)。此前结论保留在[历史对齐记录](../design-alignment-20261003.md)。本合同“已交付”不等于012已消费、代码已实现、联合链已验证或生产批准。联合证据用[最小验证合同](verification.md)，不再建设另一套账本。


## RC08 G-01：历史1.3具体结构及当前消费边界

这是普通技术设计的明确落位，不批准新的工艺或现场参数。依据为当前主项目RecipeContracts.cs、ExecutionInputs.cs及RecipeDefinitionValidator、RecipeRunPlanner、CoordinateResolver、Workflow/RecipeExecutionCoordinator、RecipeDetectionExecutor、012两个解码提供者的实际消费者。现代码只有一个Flip.Target、按相机的PointRefs、测高CoordinateDefinition及按stage读取E，不能直接表达新增用途，以下由011实现；012只消费。

### RC08.1 最少类型声明与路径

下列声明位于`backend/src/Gaode.Application/Recipes/ExecutionInputs.cs`；同名类型替换其当前执行结构。未列的现有业务字段继续保留。字符串键一律Ordinal精确匹配，集合非null；空集合表示确无该用途，不表示缺输入可成功。

```csharp
public sealed record RecipeTargetPose(
    string ProfileId, string ProfileVersion, string PoseKey);
public enum RecipePointPurpose { FlipPick, FlipPutBack, EScan }
public sealed record RecipePurposePoint(
    RecipePointPurpose Purpose, PlanarPoint Point,
    ApprovedFixedBasis Fixed, string CoordinateEvidenceReference);
public sealed record RecipeFlipTransition(
    RecipeTargetPose TargetPose, string PickPointRef, string PutBackPointRef);
public sealed record RecipeFlipInputs(
    IReadOnlyDictionary<string, RecipeFlipTransition> Stages);
public sealed record CoordinateDefinition(
    string PointRef, PlanarPoint Point, string ObjectPattern,
    string SlotId, int PhysicalSlotIndex, int LocalFace, string Camera,
    string StageId, string ConfigurationVersion, string SourceFactReference,
    ApprovedFixedBasis Fixed);
public sealed record ObjectExecutionInputs(
    HandlingPoint? Source, IReadOnlyList<CoordinateDefinition> Coordinates,
    RecipeFlipInputs? Flip, RotationTargets? Rotation,
    IReadOnlyDictionary<string, HandlingPoint> Sorting,
    IReadOnlyDictionary<string, RecipePurposePoint> PurposePoints);
```

`PlanarPoint(Id:string, Version:string, X:double, Y:double, Unit:string, Frame:string)`、`ApprovedFixedBasis(Z:double, Unit:string, Datum:string, ApprovalReference:string, ConfigurationVersion:string)`、`HandlingPoint(Point:FixedPoint, CoordinateEvidenceReference:string)`复用现有声明。所有坐标值须显式存在且有限；Fixed.Unit=Point.Unit、Fixed.Datum=Point.Frame，不能用C# FixedPoint默认Z=0补缺失JSON。Point.Id/Version和Fixed.ConfigurationVersion标识实际配置，不能由保存版本冒充。ApprovedFixedBasis的名字不授予生产批准，Test来源和原生产限制仍如实保留。

`RecipeContracts.cs`中增加下列类型/属性；原RecipeCodeRule的已有字段和检测面扫码功能保留：

```csharp
public sealed record RecipeExtraScanPose(
    string PoseId, RecipeTargetPose TargetPose, string ScanPointRef,
    string? PickPointRef, string? PutBackPointRef,
    string CaptureProfile, string AlgorithmProfile);
// RecipeCodeRule新增属性：
// string? ScanPointRef { get; init; }
// RecipeExtraScanPose? ExtraPose { get; init; }
// RecipeDefinition新增属性：
// required string SchemaVersion { get; init; }
// required string DefinitionDigest { get; init; }
public sealed record RecipeCatalogSnapshot(
    string SchemaVersion, string CatalogDigest,
    IReadOnlyList<RecipeDefinition> Definitions);
public sealed record RecipeStage(int Number, string Action,
    double? AngleDeg, IReadOnlyList<RecipeTarget> Targets);
```

当前RecipeDefinition的其余构造字段沿现有定义，PlcRecipeId改为int?（新建null）；RecipePosition.PhysicalSlotIndex当前执行必填int，JSON字段改为physicalSlotIndex。旧protocolSlotIndex仅在旧格式reader识别。RecipeCodeRule新增两属性默认null，ObjectExecutionInputs.PurposePoints无值时显式空字典；RC08历史1.3的新建候选SchemaVersion为正文2；当前新候选按RC10使用正文4，已集成1.4使用正文3，身份/版本/摘要未保存时允许空字符串，不能把它们当保存结果。保存后完整Definition的三者必须非空。

| 完整嵌套路径（从RecipeDefinition起） | 类型 / 键与必填条件 | 身份及用途 |
| --- | --- | --- |
| ExecutionPositions[slotId] | SlotExecutionInputs；键等于其SlotId及Positions[].SlotId | PhysicalSlotIndex从匹配Position读取；当前输入必填且原槽唯一，不由过滤后序号生成 |
| ExecutionPositions[slotId].Members[material] | ObjectExecutionInputs；键为该槽Members[].Material | 检测部位/成员用其MemberPattern；PhysicalEntity表示UnitPattern对应整体，不能将成员当成多个整体 |
| …PhysicalEntity或Members[material].Coordinates[] | 上述CoordinateDefinition；PointRef在本对象内唯一，(StageId,LocalFace,Camera)唯一 | 检测专用。StageId=`stage:{Number}`，不是`stage:{LocalFace}`；Camera为A/B/C/D之一且属于该Target.CameraPair。ObjectPattern/槽号/面必须对应真实对象 |
| …PurposePoints[pointRef] | RecipePurposePoint，键为局部稳定字符串，不用数组序号 | 只装翻转Pick、PutBack及E扫码配置；Purpose严格核对引用用途；点在所属对象内解析，不跨槽、跨成员隐式查找 |
| …Flip.Stages[stageId] | RecipeFlipTransition；Flip在需要检测换面时必填，Stages键为`stage:{RecipeStage.Number}` | 仅登记该对象实际参与的换面；Pick/PutBack分别引用同对象PurposePoints的FlipPick/FlipPutBack；TargetPose非null。不因LocalFace数字变化推导机械编码 |
| …Source / …Sorting[quality] | 现有HandlingPoint? /字典；搬运源在需要搬运时必填；quality仅NG、Pending | Source是分拣源；Sorting是本盘对应区域配置目标，二者不是翻面取放点。对该配方适用的两类搬运都应配置。OK不要求目标，不添加姿态异常目标 |
| ECode.ScanPointRef | string?；Enabled且ExtraPose=null时必填，ExtraPose存在时为null | 检测面E从代表检测对象的PurposePoints解析EScan；ReadAt保持firstAccessibleFace或stage:n，面身份从该阶段代表Target取得，不用n充面号 |
| ECode.ExtraPose | RecipeExtraScanPose?；关闭额外姿态时null | Enabled=false时ExtraPose/ScanPointRef均null；存在时ReadAt=null避免重复，四检测面后执行；PoseId非空、独立于LocalFace |
| ECode.ExtraPose.TargetPose | RecipeTargetPose，必填 | JSON对象profileId/profileVersion/poseKey；见RC08.2，不接受数字或PLC原码 |
| ECode.ExtraPose.ScanPointRef | string，必填 | 对每槽代表Material选同一业务引用名，在各槽各自检测对象的PurposePoints解析EScan；不同槽数值可以不同 |
| ECode.ExtraPose.PickPointRef / PutBackPointRef | string?，成对出现 | 本轮通过已确认翻转/放回来建立额外姿态时两者必填，在机械实体的PurposePoints解析。没有机械建立动作的新模式未经确认，不借null实现跳过动作；null保留类型可空性，不代表可运行 |
| ECode及ExtraPose.CaptureProfile / AlgorithmProfile | string；E启用时对应执行处必填 | 键分别引用共同CaptureProfiles/AlgorithmRequirements；ExtraPose使用自身引用，不暗取普通检测配置 |

对于independentPart/looseGroup，机械实体解析按现有ForObject的有效身份关系；assembledEntity的机械Pick/PutBack位于PhysicalEntity，检测/E点位在对应Members[material]。独立件未配置Members输入时沿现有ForDetection回到PhysicalEntity，只有明确唯一对象对应才允许，不能遇引用缺失就尝试别的槽/对象。对象身份、点位所属关系和引用唯一性统一由011校验；012不复制规则。

### RC08.2 配置来源、解析与编辑权限

当前保存正文内嵌每个用途的完整点位值及来源，不新增外部点位服务或动态引用平台。编辑选择已有配置时，012把选中版本的完整值放入上述路径；已有授权可编辑XYZ/用途引用时提交显式值。SourceFactReference、CoordinateEvidenceReference、批准引用/版本保留真实来源，不由浏览器生成生产批准。服务器受控配置解析后形成的有效批准元数据仍只读。所有保存值经共同校验；不要求运行时再按PointRef读取活动目录。

PointRef是本对象内引用键；Point.Id/Version是配置身份，两者无需相同但不得混用。检测步骤由(stage,material,camera)找到唯一CoordinateDefinition，把其PointRef及显式XYZ复制为执行目标；翻面和E先按上述所有者解析局部引用。后端唯一CoordinateResolver组合PlanarPoint与Fixed.Z成实际目标，形成来源/对象关联；运行冻结这些值及来源，后续编辑不改变已冻结解析结果。F XY来自首次3D，不放入本配方的静态用途点；E的Fixed.Z仅作扫码Z，机械Pick/PutBack用抓取用途，检测Fixed.Z只作检测Z。

TargetPose的ProfileId必须等于该配方MotionProfile。ProfileVersion与PoseKey来自该型号适用、已有受控运动配置/PLC程序对接清单的语义引用，由配置者选择；它不是用户编写机械程序的入口。保存内嵌引用，不内嵌协议表。通信适配按Model及完整引用查受控映射；地址、编码及型号ASCII载荷都留通信层。尚未取得正式映射时可保存结构合法但运行受限的配置，不能自动映射PoseKey为数字、角度、LocalFace或默认5。具体现场引用值仍需真实配置来源，本合同没有给生产缺省值。

RecipeId/Version/DefinitionDigest、运行中的UnitId/MemberId/PhysicalEntityId、步骤Sequence、StageId、PlanRevision、观察和动作关联均由后端形成。编辑提交SlotId/PhysicalSlotIndex、Material和已有Pattern配置；不提交某次运行ID冒充配置身份。012保存完整共同正文，011冻结为运行库的既有版本化负载，两库不共享事务。

实施入口：`RecipeStageIdentity.Create(int number): string`与`TryParse(string? id, out int number): bool`由011在Application/Recipes唯一定义；正整数形成规范`stage:{Number}`，解析拒绝空白、前导零及非规范值。Planner、唯一校验和012后端编辑映射使用同一入口，无需生成执行计划或取得运行批准。编辑API返回后端形成的StageId，浏览器不实现身份算法。点位/采集/算法/运动引用仍取已保存完整正文及明确受控配置输入；当前没有新生产配置目录API，不能用组件输入补齐空白新建表单。T025的具名Test输入后续单独交付，现场配置缺失只限制依赖它的创建/执行。

### RC08.3 三种序列化版本与唯一实现

| 层次 | 当前设计实际取值 | 存放位置/含义 |
| --- | --- | --- |
| 文档合同修订 | 本次设计`recipe-contract/1.5`（历史1.3/1.4准确读取） | 012持久元数据ContractVersion及交接；不是配方内容Version，不是SQLite schema |
| 目录快照SchemaVersion | `recipe-catalog-snapshot/1` | RecipeCatalogSnapshot.SchemaVersion；GetSnapshot返回形状，Definitions为同次一致读取的完整共同正文 |
| 共同正文SchemaVersion | 本次新写`recipe-definition/5`；历史2/3/4按原版本真实读取 | RecipeDefinition.SchemaVersion，JSON为schemaVersion；API Candidate/完整读取与SQLite DefinitionJson均使用同一正文，不另包第二套业务DTO |
| 冻结负载SchemaVersion | 本次新写`execution-inputs/3`；历史2及原1按实际版本读取 | FrozenExecutionInputs.SchemaVersion；含已解析计划/点位/配置/能力/预算及来源，不是可编辑配方；历史execution-inputs/1保持原读取语义 |

其中历史1.3/1.4与冻结2字段已在原稳定批实现并验证；本次1.5/正文4/冻结3仅设计，未实现/验证；其他绑定/运行接线与联合证据按当前交接另列，不能由共同字段通过推定。`review-recipe-catalog/0.4`、`0.5`、`tray-recipe-catalog/1`、`semantic-recipe-input/1`是现有适配/历史格式，不能填入新的快照或正文SchemaVersion。SQLite自身schema及迁移编号由012维护，不能用它代替上述任何值。RecipeDefinition.CatalogDigest保留保存时来源，Match/运行记录使用本次快照CatalogDigest，不被旧正文来源锁定。

011新增`backend/src/Gaode.Application/Recipes/RecipeDefinitionSerialization.cs`，提供同一`Serialize(RecipeDefinition): string`、`Deserialize(string): RecipeDefinition`及正文版本常量；JSON属性camelCase，RecipePointPurpose为精确字符串FlipPick/FlipPutBack/EScan，禁止数字枚举，字典键保持原文。新正文缺必需数值/字段拒绝，不借默认构造值填齐；可空值用null，集合用[]/{}。012存储/解码/API消费此实现及类型，不自行实现另一正文序列化规则。DefinitionDigest仍只由RecipeDefinitionIdentity计算，覆盖SchemaVersion及合法业务内容；序列化格式与摘要规范化不是两套摘要实现。候选检查/保存的身份空值仍按RC04.1处理，不生成假已保存Version。

### RC08.4 旧字段的定向迁移

实施签名承接（合同1.3不变）：RecipeRunPlan保留已匹配Model、DefinitionDigest及ECode完整配置为必填属性，PlcRecipeId改int?仅保历史展示语义。BuildExecutable(RecipeDefinition,string,IReadOnlyList<string>)只接Matched正文，目录访问留Matcher调用边界；不保旧Build(IRecipeCatalog,...)执行入口。RecipeCatalogSnapshots.Create(string,IEnumerable<RecipeDefinition>)供目录提供者形成只读深快照，Freeze的Definition/RunPlan重载供正文读取及运行冻结复用。CaptureProfile.Settings维持原签名，内部复制ROI数组，读者不能改写快照。012消费批次及实际验证状态以交接记录为准。

| 现有字段/消费者 | 当前执行正文2的处理 | 保留义务/删除条件 |
| --- | --- | --- |
| RecipeStage.Number、Targets及目标Material/LocalFace/CameraPair/参数引用 | 保留，Number为连续阶段顺序，LocalFace是对象检测面；不再将二者当相同身份 | planner/validator/CoordinateResolver同步迁移，更多面不受1/2/4硬限制 |
| RecipeStage.Action | 保留已确认动作语义none、flipAffectedMembersOneByOne；以实际物理实体集合执行取件→翻转→放回，不把成员数当整体动作次数 | 旧special旋转类别不能自动改成新翻转；保其有效配置/历史描述，缺正式机械映射限制依赖路线，删除Test HTTP捷径而非抹掉历史动作 |
| RecipeStage.CoordinateRule | 从当前正文/当前RecipeStage删除 | initial3D/旧measurement依赖由配置检测XYZ及统一姿态复查取代；历史reader按旧schema保留原文，不为新执行做字符串兜底 |
| RecipeStage.AngleDeg、RotationTargets | 保留nullable及既有有意义配置用于有实际消费者的旋转描述；普通翻转/额外E不读取其角度 | 未有有效新映射的旧special配置为Restricted，不把角度转PLC原码；核实无用途部分后删除，不预删有效历史读取 |
| ObjectExecutionInputs.Flip的旧FlipTarget.Target/Mode/ManualWaitMs | 当前替换为RecipeFlipInputs.Stages明确取放及目标引用 | 已确认新动作由PLC内置程序执行，旧手动等待不成为新默认；有效取消/操作关联承接，历史字段只读 |
| Positions/Members.PointRefs按相机的旧字典 | 当前删除；检测用Coordinates的(stage,face,camera)唯一定位，机械/E用PurposePoints | 012两个适配文件转换明确配置；不能仅凭旧单点自动制造新的Pick/PutBack或E点 |
| CoordinateDefinition.HeightRound/Measurement/ResolutionKind | 当前删除；Fixed必填 | 坐标不依赖测高；旧HeightResult/偏移结构只保实际历史reader，不能将旧记录补零升级为可运行 |
| RecipeStep.LocalFace/AngleDeg/HeightRound/PointRef | LocalFace对检测保留；新增StageId:string?、ScanPoseId:string?、TargetPose:RecipeTargetPose?；独立E的LocalFace=null。PointRef保留当前用途引用；HeightRound从新计划移除，以实际ObservationId/CoordinateEpoch关联复查 | Sequence仅步骤顺序；旧AngleDeg只承接有效旋转历史/已准入用途，不映射新翻转；新增计划由011生成，012不编辑 |

新正文与旧适配格式不并行成为正式活动源。012迁移有效配置须提供缺少的新用途输入后调用同一校验；不能自动补齐、静默裁剪后保存。旧冻结负载、绑定事实和错误证据由有限reader读取原格式；历史可读不授予续跑能力，也不形成当前执行兼容路径。

### RC08.5 串联引用的结构示例

下面是**一个正文的相关字段投影**，不是完整可保存配方；省略的捕获/算法/批准/其他检测点等仍必须按共同校验齐备。数值、名称及Test来源仅说明结构，不批准任何现场参数或可运行性。本例同一独立对象在stage:2换面、四面后额外E，检测面与姿态引用分离；其余stage:3/4取放配置省略，不能据省略推断不需要。

```json
{
  "schemaVersion": "recipe-definition/2",
  "recipeId": "example-recipe", "version": "server-content-token",
  "model": "ExampleModel", "motionProfile": "example-motion",
  "unitKind": "independentPart", "fCode": "example-tray",
  "composition": [{"material":"part", "localFaces":[1,2,3,4]}],
  "positions": [{"slotId":"s1", "physicalSlotIndex":1,
    "unitPattern":"{TrayRunId}/s1", "members":[
      {"material":"part", "memberPattern":"{UnitId}/part", "handling":"individual"}]}],
  "stages": [
    {"number":1,"action":"none","angleDeg":null,"targets":[{"material":"part","localFace":1,"cameraPair":"CD","captureProfile":"detect","algorithmProfile":"defect"}]},
    {"number":2,"action":"flipAffectedMembersOneByOne","angleDeg":null,"targets":[{"material":"part","localFace":2,"cameraPair":"AB","captureProfile":"detect","algorithmProfile":"defect"}]},
    {"number":3,"action":"flipAffectedMembersOneByOne","angleDeg":null,"targets":[{"material":"part","localFace":3,"cameraPair":"CD","captureProfile":"detect","algorithmProfile":"defect"}]},
    {"number":4,"action":"flipAffectedMembersOneByOne","angleDeg":null,"targets":[{"material":"part","localFace":4,"cameraPair":"CD","captureProfile":"detect","algorithmProfile":"defect"}]}
  ],
  "eCode":{"enabled":true,"representativeMaterial":"part","bindTo":"physicalEntity",
    "requiredForOk":true,"readAt":null,"scanPointRef":null,"captureProfile":null,"algorithmProfile":null,
    "extraPose":{"poseId":"mark-view","targetPose":{"profileId":"example-motion","profileVersion":"example-v1","poseKey":"mark-access"},
      "scanPointRef":"mark-scan","pickPointRef":"flip-pick","putBackPointRef":"flip-put",
      "captureProfile":"e-capture","algorithmProfile":"e-decode"}},
  "executionPositions":{"s1":{"slotId":"s1","members":{},"physicalEntity":{
    "source":null,"sorting":{},"rotation":null,
    "coordinates":[{"pointRef":"face2-A","point":{"id":"detect-A","version":"test-v1","x":10,"y":20,"unit":"mm","frame":"test-frame"},
      "objectPattern":"{UnitId}/part","slotId":"s1","physicalSlotIndex":1,"localFace":2,"camera":"A","stageId":"stage:2",
      "configurationVersion":"test-v1","sourceFactReference":"example-only",
      "fixed":{"z":3,"unit":"mm","datum":"test-frame","approvalReference":"Test-only","configurationVersion":"test-v1"}}],
    "flip":{"stages":{"stage:2":{"targetPose":{"profileId":"example-motion","profileVersion":"example-v1","poseKey":"face-two"},"pickPointRef":"flip-pick","putBackPointRef":"flip-put"}}},
    "purposePoints":{
      "flip-pick":{"purpose":"FlipPick","point":{"id":"pick","version":"test-v1","x":11,"y":21,"unit":"mm","frame":"test-frame"},"fixed":{"z":4,"unit":"mm","datum":"test-frame","approvalReference":"Test-only","configurationVersion":"test-v1"},"coordinateEvidenceReference":"example-only"},
      "flip-put":{"purpose":"FlipPutBack","point":{"id":"put","version":"test-v1","x":12,"y":22,"unit":"mm","frame":"test-frame"},"fixed":{"z":4,"unit":"mm","datum":"test-frame","approvalReference":"Test-only","configurationVersion":"test-v1"},"coordinateEvidenceReference":"example-only"},
      "mark-scan":{"purpose":"EScan","point":{"id":"scan","version":"test-v1","x":13,"y":23,"unit":"mm","frame":"test-frame"},"fixed":{"z":5,"unit":"mm","datum":"test-frame","approvalReference":"Test-only","configurationVersion":"test-v1"},"coordinateEvidenceReference":"example-only"}
    }
  }}}
}
```

同一物理点在不同用途上即使数值恰好相同，仍明确配置对应引用和Purpose；示例并未强制它们必须不同。012可据RC08直接绑定字段、序列化和保存，不需自行补类型；G-01设计消费已由本轮明确回执核验；012共同基础软件消费已由其保存/适配稳定批回执确认；后续运行入口/新状态页面及联合证据仍待具体交付。

## 修订记录

- 1.0：共同字段、校验、保存可见性、匹配/冻结首版。
- 1.1：软件绑定替代旧设备配方ACK，实际机械反馈/保存保护保持。
- 1.2：本轮调度明确独立SQLite适配；保持IRecipeStore.SaveAsync签名，细化RecipeSaveRequest目标身份、服务器身份/版本、唯一摘要、ExpectedVersion及保存结果，SaveId仅内部用途；共同业务不依赖存储技术。012必须显式消费此版本后交回修订成果。
- 1.3：接收012最终1.2设计，RC08补G-01用途点/逐阶段翻转/E、TargetPose语义引用、三层schema实际值、唯一正文序列化和旧字段迁移；尚未收到012对1.3的消费回执，不代表实现。


T026孤立类型核查（2026-10-04）：MeasurementOffsetBasis仅余声明，无当前/历史reader、装配、脚本或测试的类型消费者；旧历史payload按原JSON读取并不依赖此CLR类型。删除该孤立声明，不保名为历史的空壳；CoordinateResolutionKind旧枚举值及实际DeviceHistoryProjection.HeightRound等仍服务原JSON读取，保留其原值且不授当前执行。共同1.3字段/序列化形状不变，012无迁移字段；完整必要工程编译与当前公开形状门禁验证此删除，不新增用例或链。

## RC09 逐次拍照与配方级夹爪（1.4已集成，历史设计）

共同CoordinateDefinition和RecipePurposePoint新增可空CaptureProfile引用，后者仅EScan使用；原slot/member、StageId、LocalFace、Camera、PointRef或E姿态精确定位每次实际采集。1.4新保存正文3每实际相机点必须引用有效CaptureProfiles；AB/CD分别配置，E仅扫码，F/3D不编辑。曝光正整数µs、增益有限正数、亮度整数0—100%，这是软件有效格式，不推导硬件极限。ROI、光源通道、等待、算法、运动及来源由后台已有配置承接，不是操作员编辑项。

共同RecipeDefinition/RecipeRunPlan新增可空SortingGripperId。新候选检查与保存严格要求1或2且显式选择，历史缺项null；是配方级分拣业务ID，不是PLC原码；“只存不接PLC”属于1.4已完成范围，本轮014将承接选择语义和通信，普通适用动作保护保持。

共同planner从实际对象检测坐标或EScan用途点解析CaptureProfile，将引用携入原步骤和原CaptureRequest.DetectionSettings。保存、目录、F匹配同SQLite已提交来源；共同摘要覆盖新字段，深冻结保证随后保存不改变在途参数和夹爪。不建立012私有类型、第二校验器/身份算法或执行路径。

唯一RecipeDefinitionSerialization接受正文2历史读取和正文3新写；1.4保存只接受3；本次1.5新保存按RC10接受4。新增可空属性null时省略，旧冻结负载的PlanRevision/SemanticDigest重序列化字节不变。旧SQLite行合同1.3保留，新行1.4，整包JSON无需表列迁移；历史缺项不偷偷填1/零，不改013冻结输入。旧正文编辑按已存真实参数建立局部引用并显式选择夹爪后才可保存新正文；旧2/3历史记录与已冻结运行按各自原版本/参数读取，不能给本次新4提供缺项回退。

用户随后明确其他采集、算法和翻面姿态无需在操作者配方里面配置；后台有效信息保留/解析，不扩大UI。新建编辑映射只消费真实配置，缺失/歧义返回不可用，不借Test专用目录或演示成功。SDK/光源最小配置端口在Trigger前消费此次设置；实际硬件与光源映射未验证，组件调用不能宣称Applied，生产Host未接通保护保留。

## RC10 布局及特殊闭环准确类型（recipe-contract/1.5，Phase 1设计）

本节是本次共同新增字段的唯一规范性定义；014/012只引用。当前集成实现仍为1.4/正文3；以下1.5/正文4尚未写代码、验证或软件交付。业务保存签名、身份算法、F精确匹配不另建。场景1仍沿实际ScenarioId与independentPart映射；不发明第四场景或产品名分支。

### RC10.1 类型及归属

以下为现有类型的设计增量，不另建私有RecipeDefinition。可空新增属性在历史序列化中null省略。

| 类型/字段 | 准确类型及JSON | 关联与必要约束 |
| --- | --- | --- |
| RecipeDefinition.TrayLayout | `RecipeTrayLayout?`；`trayLayout` | 新正文4必填；历史缺项null。Rows/Columns各为10，不是可编辑尺寸 |
| RecipeTrayLayout | `(int Rows, int Columns, IReadOnlyList<RecipeTrayCell> Cells)` | JSON rows/columns/cells；只保存选中格，未列出的实际格为空；无自动填洞、数量布局算法 |
| RecipeTrayCell | `(string CellId, int Row, int Column, RecipeTrayRegion Region)` | JSON cellId/row/column/region；行列1..10；同格及CellId唯一。CellId程序确定为`r{Row}:c{Column}`，配方内稳定，不含区域或显示号，不是PLC/3D编码 |
| RecipeTrayRegion | NG / OK / Pending | JSON精确字符串`NG`、`OK`、`Pending`；每格仅一个区域。左NG中OK右Pending沿确认稿组织，不凭稿的示例划定硬件列界或新增自动布局 |
| RecipePosition.CellId | `string?`；cellId | 新正文4指向唯一OK格；原SlotId/UnitPattern/Members不以显示号重建。每个OK格有对应位置，NG/Pending不进入Positions |
| RecipePosition.PhysicalSlotIndex / CoordinateDefinition.PhysicalSlotIndex | `int?`；physicalSlotIndex | 正文4允许无现场映射时null；已配置则正数且与明确映射一致。不限为1..Capacity，不按row/column/区域号算。历史2/3的原非空整数仍原样读取 |
| RecipeDefinition.TraySlotMapping | `RecipeTraySlotMapping?`；traySlotMapping | `(string Id,string Version,string EvidenceReference,IReadOnlyList<RecipeCellPhysicalBinding> Bindings)`；Binding=`(string CellId,int PhysicalSlotIndex)`。后台正式配置建立映射及来源/版本；非操作员技术字段。未有依据时不补0或任何序号；只限制依赖定位的准入 |
| RecipeDefinition.InspectionKind | `RecipeInspectionKind?`；inspectionKind | JSON `ordinary`或`specialRotation`；新正文4必须按业务类型表达，特殊仅场景1独立件。历史null保持未配置，不从产品名推断 |
| RecipeDefinition.RotationLoadingGripperId | `int?`；rotationLoadingGripperId | 特殊必须显式1/2；普通null。复用SortingGripperId，正文4仍要求适用分拣抓手明确1/2；无默认和历史补值 |
| RecipeDefinition.RotationWorkstation | `RotationWorkstationInputs?`；rotationWorkstation | `(HandlingPoint Place,HandlingPoint Pick)`复用现有HandlingPoint；特殊共享旋转工位放/取两个用途，普通null。安全条件、固定取料角和容差沿有效机械配置，不成为新增UI开关 |
| RecipeStage / RecipeTarget | 保留Number、AngleDeg、Targets及现有目标字段 | 正文4特殊恰好两个Action精确为`rotate`的RecipeStage；StageId由现RecipeStageIdentity产生，AngleDeg各组绝对角；每组CameraPair AB或CD，两台都采集。Route复用现`specialType1Part`且UnitKind=independentPart，普通复用ordinaryBatch/ordinaryAssembly；InspectionKind必须与场景/Route一致。旧specialType1WholeAssembly及combined transfer/rotate动作字符串只保历史读取，不据旧类型批准新场景。TransferToRotation由planner在两组前单独生成，无重复上料。角度只有Stages一处，不在工位重复定义；特殊完整性按两个StageId核，LocalFace沿现合法目标含义，可同面分属两Stage，不能用普通面覆盖去重把第二组删掉；普通面数/面序规则保留 |
| ObjectExecutionInputs.OriginPutBack | `HandlingPoint?`；originPutBack | 特殊必需的独立原槽放料点，固定跟随Position.CellId，Source仍原槽取料点。不含可选目的格，不用Sorting["OK"]，不自动复制Source高度/补偿 |
| RecipeDefinition.SortingTargets | `IReadOnlyDictionary<string,HandlingPoint>?`；sortingTargets | 新正文4按NG/Pending目标CellId存各格独立放料点，唯一点位来源；可在还未选OK格时编辑目标区。禁止OK键或任意OK目标。既有allocator目标分配规则不新增 |
| ObjectExecutionInputs.SortingCellIds | `IReadOnlyDictionary<string,string>?`；sortingCellIds | 已有NG/Pending目的点与对应实际区域格的绑定；键仅已有NG/Pending用途。只给既有配置/allocator加稳定格位关联，不创建新的目标分配策略；空或缺项按适用动作校验。正文4只存这层引用，旧Object.Sorting字典保持空；planner解析SortingTargets形成执行输入，避免两个可编辑坐标来源。旧2/3 Sorting原字典准确历史读取 |
| Capture关联 | 复用Coordinates的StageId/LocalFace/Camera/PointRef/CaptureProfile，E的PurposePoints/扫码姿态 | 检测对象=SlotId+实体/成员；同StageId/实际点/相机参数各自独立。曝光µs正整数、增益有限正数、亮度整数0..100是既有软件格式，不能据此批准硬件极限 |

Capacity/NgCapacity/PendingCapacity是上述区域格数量的派生兼容投影，服务器共同校验核一致；没有独立手填数量。区域号按该区域选格(Row,Column)排序从1派生；编辑可重编号但从不用于查找坐标、实体或回放目标。改变/取消区域清理该CellId旧区域的Position、ExecutionPositions与参数绑定；其他CellId不迁移。仍被其他拍照引用的CaptureProfile保留，仅无人引用的局部记录可清理。

成组looseGroup的Members按现Material/MemberPattern身份关联独立实体，ForObject取该成员；CellId是现Position/组位置的布局锚点，PhysicalSlotIndex是当前已确认的观察关联，不据此认定各独立成员同一机械位置。各成员取放仍取其Source/Flip/Sorting引用；现场若按成员给独立3D物理号，须取得该关联依据并在执行准入前落实，不能用组显示号或复制锚点号补齐（DEP02仅限制该输入）。半成品assembledEntity的检测Members代表部位，ForObject仍只取PhysicalEntity。界面成员/部位导航不改变搬运实体。未确认重复Material成员需求不借本次扩展。

### RC10.2 保存与准入，同一个校验器

`ValidateForSave(RecipeDefinition,RecipeCatalogSnapshot)`与已有执行准入仍由唯一RecipeDefinitionValidator承担。正文4保存校验检查布局、区域归属、稳定关联、适用类型/两用途抓手、点位用途、两组/参数引用及FCode唯一；不调用设备或以实时Worker状态作门。现场映射、正式通信地址、安全/容差和实际硬件能力属于依赖执行的准入检查，不让ExecutionIssue把无现场信息的合法保存全部拒绝。

原槽配置引用在保存阶段已经明确：CellId→SlotId→原槽取料/放料/检测对象。运行绑定后冻结实际TrayId和实体，形成`OriginalSlotReference(Guid TrayId,string CellId,int Row,int Column,string SlotId,int PhysicalSlotIndex,string EntityId)`；Region固定OK，区域号另为冻结显示投影。实体/成员、步骤、3D物理号不能互相代换。缺正式PhysicalSlotIndex映射时不形成伪Origin，拒绝其依赖动作。

正文4的NG/Pending点位仅SortingTargets存一份，实际分配引用只沿现明确规则；新目标格不得自动按OK号配对。原NG/Pending显式点位、容量/占据和目标预留保护保留，不从OK序号推导目标分配。新布局观察覆盖由明确映射确定需要的物理槽集合；空格、目标区不是待检参与者，Unknown/Absent/PoseExcluded与可靠覆盖按原规则分开。

### RC10.3 序列化、身份与历史

| 层次 | 本次设计 | 当前实际基线与处理 |
| --- | --- | --- |
| 文档/存储记录合同 | recipe-contract/1.5 | 既有1.3/1.4行准确读取，不批量覆盖 |
| 新共同正文 | recipe-definition/5 | 之前实现4；2/3/4保持历史读取，不自动补布局/抓手或成员物理位置。旧版本json/摘要不经新默认值重写 |
| CatalogSnapshot | recipe-catalog-snapshot/1不变 | Head+完整正文一致读取，同一SqliteRecipeStore |
| 新冻结负载 | execution-inputs/3 | 既有2准确历史读取与旧字节摘要；新增可空字段省略不污染旧负载，按负载版本验证。既有已启动运行不迁移成新布局 |
| SQLite表结构 | 不新增业务表/数据库 | 完整DefinitionJson增加业务字段，沿原维护互斥/Host schema检查；需要工程迁移的版本读取先落实消费者，非Host静默改库 |

沿RecipeDefinitionSerialization唯一严格camelCase编解码；新枚举按本节精确字符串、未知字段拒绝，字典Ordinal，数组依共同含义规范，无宽松旧字段兜底。新保存只接受4；编辑旧2/3先呈现真实缺项，操作者明确选实际格与适用抓手；原隐藏合法配置保留，但只有可证实原身份关联的数据才能绑定选格，不能按旧连续号复制到新格。未建立关联的坐标/拍照值保持待填写，不伪造迁移或批准。

布局选格和Bindings序列化按行列稳定排序；Stages、成员、面序仍保既有业务顺序。DefinitionDigest沿共同RecipeDefinitionIdentity规范化完整正文计算，新字段均纳入；Version/CatalogDigest排除规则不变。ETag/If-Match→ExpectedVersion不变。保存检查、提交、目录、完整GET、F唯一匹配、深冻结都消费同一来源；历史可读不代表缺布局的新生产动作可准入。

### RC10.4 共同计划、采集与实际处置

RecipeRunPlan深复制TrayLayout/映射/两抓手/工位/OriginPutBack；units按冻结OK号形成，跳过保号。普通既有阶段内部按OK顺序遍历，保面、成员、相机/批次节奏；特殊按件scope完成闭环，详见014 execution contract。每件原槽Origin进入全盘冻结计划，新的scope只限定消费范围不改全盘身份。

复用StageId区分两组采集：FaceResultKey、RecipeWorkload融合计数、WorkerTargetIdentity/算法输入、结果投影键增加可空StageId；特殊非空，普通及历史缺项按版本保原语义，不制造新CoordinateEpoch。原相机CaptureRequest携带局部CaptureProfile实际参数，E只扫码，F/3D不成为编辑项。Component证明参数进入端口不能宣称硬件Applied。

普通OK实际NoMoveRequired；特殊OK必须有ReturnToOrigin实际取料/提交/转运/放回/安全位事实，使用SortingGripperId。旧SpecialExitCompleted/任意OK目的只保历史原记录读取，不授权当前动作。质量OK、件scope检测Completed、盘Completed、物理放回、保存结果分别表达；不得以其中一个补造另一个。

修订记录：1.4主项目实际集成见2026-10-05集成报告；1.5为2026-10-05本次Phase 1设计，尚待设计/UI审查、任务承接和实现，不代表能力已交付。


### RC10.5 新建上下文与唯一校验承接

合法新建/编辑上下文固定为：场景1independentPart ordinary→ordinaryBatch或specialRotation→specialType1Part；场景2looseGroup ordinary→ordinaryBatch；场景3assembledEntity ordinary→ordinaryAssembly。正式ScenarioId沿已有场景配置，不凭“1/2/3”造新字面代码。候选InspectionKind/Route/UnitKind/ScenarioId必须一致，特殊仅场景1。

API-L00的请求/同源解析引用此规则。共同RecipeDefinitionValidator负责业务组合及来源能力相容检查，API只传递选择及同tuple/指定RecipeId查询，不另实现工艺规则。已有2/3来源的合法Route可用于判明后台能力与请求类型是否相容，但历史缺InspectionKind原值保持null，不假迁移；源能力不能准确支持本次两组/工位时由正式配置准备，绝不选择普通来源补特殊字段或将旧specialType1WholeAssembly授新场景。

编辑candidate的JsonElement只承载共同正文属性与未填null，非第二配方类型；保存解析严格4不接受编辑null作为有效数值。共同Serializer/Validator/Identity/Store及Catalog不新增替代实现。SourceRecipeId及来源版本只HTTP编辑上下文，不存成新业务身份/审批或生产准入。真正来源准备写同一SQLite并调用同一保存检查，责任/请求详见012 API-L00，不建立模板库。
