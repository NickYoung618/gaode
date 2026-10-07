# 配方编辑、保存与目录API设计

当前集成正文3/合同1.4；本次设计正文4/合同1.5，历史2/3真实读取。新增字段与必选规则见capture-and-gripper.md。界面以2026-10-05手动10×10确认稿及最新原槽规则，完整HTTP候选保留有效隐藏字段，ETag/权限/提交未知/真实SQLite沿用。

状态：012定向设计修订，已消费recipe-contract/1.3、station01-execution/1.0及四份混合API合同。路由/HTTP信封由012维护，业务正文和保存语义只引用[011共同合同](../../011-plc-interaction-update/contracts/recipe-contract.md)。当前读写/API/真实SQLite已实施并按独立弹窗保存/重读和必要拒绝验证；共同plan/bind只消费已提交冻结，源码与证明见当前plan-handoff。合同或组件通过不冒称主项目集成/现场通过。

## HTTP表面

复用/api/v1/recipes；当前plan/bind消费011稳定004的CommittedRecipePlanReader，仅取已提交同run冻结计划，不读活动目录/当前Public用途，不执行V1或重新规划。bind仍调用共同IndependentRecipeApplication并保原保存/期限/取消，不授产品续接；容量仅取冻结Plan。编辑器不调用这些端点生成预览执行路径。

| 方法 / 路由 | 权限 | 请求与结果 |
| --- | --- | --- |
| GET /api/v1/recipes/catalog | Run.Read | 同一IRecipeCatalog.GetSnapshot当前视图的目录投影及准入限制；authoringAccess为本次授权能力。不能从ReleaseStatus猜Review/File来源 |
| GET /api/v1/recipes/{recipeId} | Run.Read | 从同一快照取得当前完整保存表示SavedRecipeEnvelope及ETag；404确实不存在，503读取不可用，不伪装空目录 |
| POST /api/v1/recipes/validate | Config.Validate | AuthoringRequest；直接调用共同ValidateForSave(candidate,current)，返回RecipeValidationResult及独立准入限制；允许新建候选缺少服务端身份，不生成已保存身份/版本，不提交或计划 |
| POST /api/v1/recipes | Recipe.Write | AuthoringRequest转换共同RecipeSaveRequest(Candidate,TargetRecipeId=null,ExpectedVersion=null,RequestId)；仅Saved返回201、Location、ETag及完整内容 |
| PUT /api/v1/recipes/{recipeId} | Recipe.Write | 完整共同正文与If-Match；TargetRecipeId取路由，ETag的共同Version送ExpectedVersion，RequestId只关联，调用同一IRecipeStore.SaveAsync；仅Saved返回200及新ETag/完整内容，不隐式新建 |

固定catalog/validate/plan/bind不被动态recipeId吞并。目录与之后GET可能跨过一次保存，编辑基线始终取完整GET。正常读取、校验、保存不要求设备/Worker在线；共同校验核配置引用和能力相容性，生产准入/实时就绪另列。数据库来源不自动成为生产Available。

## 012 HTTP信封与共同对象映射

本次editor-draft以型号、正式场景、UnitKind、InspectionKind和可选SourceRecipeId精确解析来源，详见API-L00；不沿用当前代码仅Model+UnitKind的筛选。返回共同正文4形状完整的可编辑候选，未填值保留null/待填，不声称候选已合法可保存。`POST /api/v1/recipes/editor-layout`当前已实现数量映射仅为旧实现事实；本次目标改为实际勾选格位/区域及适用面、相机组和可选E配置，不生成执行计划，不提交，不取测试目录；新增项不复制拍照数值。两者要求Recipe.Write。阶段引用选择及专用editor-stage-identities接口已被替代并删除；完整GET的后台阶段信息保留供现有读取，不展示为操作者技术输入。

旧1.4已集成编辑布局HTTP输入为EditorLayoutRequest薄信封（仅历史实现事实，本次替代其count路径）：definition为完整待编辑正文；可选extraE为是否需要额外扫码姿态，cameraPair为AB/CD，material/localFace标识当前成员/面，faces/members沿适用面/成员输入；count为旧数量接口字段，不能作为本轮独立手工槽数量或自动生成布局依据。未提供的操作字段不触发修改；不把这些操作字段混入共同配方正文。HTTP用明确类型绑定，不放宽010未登记业务字段的拒绝规则。实际关联仍取已保存后台配置；新增拍照参数/坐标留空，无设备计划或校验副本。

保存成功信封直接使用共同Saved结果中的完整正文/身份，不在COMMIT后强制再读库作为成功条件；`savedAt`由随后的完整GET返回实际记录时间。读取失败不改变已经确认的Saved事实。

| HTTP字段 | 来源与权限 |
| --- | --- |
| AuthoringRequest.requestId | 本次请求关联，不是业务身份、幂等重试承诺或运行授权 |
| AuthoringRequest.definition | 完整共同RecipeDefinition；可编辑/只读/配置引用见editor-ui字段表，保留未修改字段，不由目录摘要重建 |
| SavedRecipeEnvelope.definition | 实际持久化的完整共同定义；不存在另造recipeRef DTO或表单裁剪正文 |
| SavedRecipeEnvelope.savedAt / requestId | 实际保存记录时间/请求关联；非精确COMMIT时刻；actor依现有审计权限 |
| 保存成功中的共同结果 | 复用RecipeSaveResult.Saved的RecipeId、Version、DefinitionDigest、CatalogDigest及完整内容；HTTP只是表示封装，不新建业务保存端口 |
| catalog / validate的admission | 011既有准入判断与真实限制，不由保存成功推生产批准 |
| catalog.authoringAccess | 后端本次主体权限计算canValidate/canSave，客户端不从角色名称、Run.Start或Availability猜测 |

ETag为`"recipe-read-1.<RecipeId的base64url>.<Version的base64url>"`，PUT将解码Version原值送ExpectedVersion，身份必须与路由一致。共同Version是唯一并发基线；无SaveId/saveToken、CatalogDigest锁或独立身份服务。完整GET保存表示的CatalogDigest是提交时来源，当前目录摘要在catalog；强ETag不覆盖动态准入/授权信息，详见[data-model](../data-model.md)。

新建在服务器调用011 RecipeDefinitionIdentity.CreateRecipeId/CreateVersion；编辑保持TargetRecipeId、调用CreateVersion；DefinitionDigest只调用该共同类的ComputeDefinitionDigest。请求携带只读元数据只作读回上下文，不取得授权；修改Approval/ReleaseStatus的尝试明确拒绝，不能静默批准。既有PlcRecipeId仅保留可读历史值；不提供新必填输入或转作PLC型号。HTTP只负责解码、格式、路由与字段写权，共同业务校验只由011负责。G-01已按1.3 RC08消费关闭；本次新建/编辑保存正文统一recipe-definition/5，历史2/3/4准确读取，编解码只用共同RecipeDefinitionSerialization，HTTP薄信封不产生第二DTO/序列化。G-02/G-03保持关闭。

## RC08字段与正文透传

AuthoringRequest.definition、RecipeSaveRequest.Candidate、RecipeSaveResult完整Definition、完整GET的definition及SQLite DefinitionJson使用同一RecipeDefinition/本次新写正文5（历史2/3/4按原版本读取）；路由/RequestId/ETag仍为HTTP或请求元数据。服务器读写正文均使用011 RecipeDefinitionSerialization；不让普通JSON绑定器用构造默认值把必需数值缺失变成0，不能只落UI已显示字段。

新建与本次编辑保存候选schemaVersion必须recipe-definition/5，RecipeId/Version/DefinitionDigest允许合同规定的空字符串，PlcRecipeId为null；保存阶段按RC04.1形成正式身份。更新按完整GET保留未编辑配置与只读字段，TargetRecipeId和ExpectedVersion仍来自路由/原ETag，不能从最新Head补条件。历史正文2/3/4保持真实缺项；编辑后新写5须明确实际布局、身份关联、适用抓手及逐次采集参数，不能自动补布局/默认值，不能将旧显示号挪到新格。旧冻结按各自原版本/摘要读取，不改013输入。

PhysicalSlotIndex写physicalSlotIndex，不接受protocolSlotIndex作为当前执行正文替代；PurposePoints用途写FlipPick/FlipPutBack/EScan字符串，TargetPose写profileId/profileVersion/poseKey对象；Coordinates与PurposePoints、逐阶段Flip/E及RC10 SortingTargets/SortingCellIds按当前layout-design LD02完整传递。缺点/用途/引用/对象关系全部由共同序列化和校验返回问题，012只定位显示。

旧CoordinateRule、相机PointRefs、测高/单Flip旧结构依RC08.4在适配边界迁移，不能在端点静默删除后重存为“完整”。历史AngleDeg/Rotation按原版本原义读取；正文4角度只在Stages，工位只在RotationWorkstation，原槽放料在OriginPutBack、旧当前Rotation/任意OK出口不写入新业务路径。合法后台字段保真；本轮不增加自动升级API、兼容执行路线或导入导出平台。StageId/运行计划等只读信息由011共同后端形成，不开放前端工艺生成。

## 共同保存结果的HTTP映射

| 共同结果或HTTP失败 | HTTP与页面含义 |
| --- | --- |
| 未认证 / 无写权或篡改受控批准 | 401 / 403；服务端执行，禁用按钮不能替代 |
| JSON、ETag格式或路由身份错误 | 400，安全原因及requestId/traceId |
| RecipeValidationResult.Valid=false（仅检查） | 200及共同Issues：Code、FieldPath、Message及适用对象引用 |
| ValidationFailed（保存） | 422及同一Issues；其中全目录FCode冲突映射409；不另造业务错误码或在API重验唯一性 |
| VersionConflict | 412；来自ExpectedVersion精确比较，不自动合并/重试 |
| 缺少If-Match | 428，必须先完整重读 |
| SaveFailed / 读取不可用 | 503及已知事实；不能把超时直接判定“未提交” |
| CommitUnknown | 503，错误details携共同结果名；无响应也在UI显示未确认，正式重读确切关联后才确认 |
| Saved | 201 / 200；真实COMMIT确认后的完整内容与共同身份，同源后续读取可见；不另设提交后重读/缓存发布门，禁止Accepted或预置响应替代 |

错误复用Station01ApiResults.Error / ErrorContract的code、message、category、traceId、retryable、details、currentRevision。共同问题和保存结果放适用details，不造另一commitState业务枚举，不将配方版本当运行revision。共同Code字符串以011实际类型为准；数据库约束冲突映射共同问题，不能发明第二工艺规则。

校验和保存都具正有限请求/数据库等待，保存必须在写入串行边界内重新ValidateForSave；预检查通过不是提交许可。RecipeSaveRequest的TargetRecipeId/ExpectedVersion来自路由/原读取，不能从提交时最新Head反填。日志记录受理、校验、提交、拒绝、失败/未知以及requestId、actor、RecipeId、Version、DefinitionDigest，沿既有分类和持久诊断。刷新失败不撤销已确认保存，未确认提交不自动重发；SQLite取消限制见research R05。

## 最小权限和Host接线

新增Recipe.Write只映射现有ProcessEngineer/SystemAdministrator；Operator/EquipmentEngineer不因拥有Run.Start获得写权。读取沿Run.Read，检查沿Config.Validate；保存调用共同检查不要求前端先点检查。Station01Authorization.cs及TestAuthenticationHandler.cs由012唯一编辑最小映射。

当前源码只有Station01Test认证，后续真实链须标明该来源；生产认证不在本功能扩建，既有限制保留。客户端不因保存请求设置或提升ApprovalScope。

Program.cs和RecipeEndpoints.cs由本会话012配套唯一编辑：接入共同服务/持久提供者、PUT和If-Match CORS允许项、ETag暴露、PUT结构化诊断；不扩大来源白名单或记录凭据。当前本会话014/012统一负责共同调用与HTTP装配，不并行覆盖；旧011交接规则属原时点。完整读取/保存不依赖设备连接，不调用IPlcRecipePort、规划器、检测器或运行启动入口。

## 共同F与012消费约束（本会话统一设计）

F按料盘编号取得当前已提交共同定义及其内容身份，保存提交后的新解析不受页面旧目录版本钉死。运行已冻结引用继续精确校验；场景/配方身份不符仍由共同服务拒绝。catalog、read、F解析共用一个持久来源，不能一条读数据库、一条读启动文件快照。已接收GetSnapshot、SaveAsync、Match及共同冻结/业务绑定回执语义；目录与保存共用一个SqliteRecipeStore实例，详见[IC-02/04](shared-integration.md)。本会话014/012统一维护共同调用与Host文件，消费唯一共同实现，不用旧PLC配方ACK补软件绑定。


历史1.4编辑映射：POST editor-draft接受Model/UnitKind以及可选SourceRecipeId（当前已读记录的程序上下文，不是操作者配置输入或新配方身份），从同一SQLite取得隐藏值；无已读上下文时要求唯一真实来源，不任取首项，不提升批准；缺失/实质歧义返回不可用。翻面Z沿已有配置保留，不把原型XY卡片扩为额外输入。POST editor-layout现有字段只证明旧接口；本轮消费实际10×10格位/区域、适用成员/面/AB-CD/E，不生成计划或复制校验；缺新姿态不猜值。catalog增UnitKind便于业务场景绑定。

## 当前1.5准确字段及HTTP消费设计

正式保存/完整GET必须保真实际100格的归属/空位、区域号与稳定格位/实体/3D物理号关联、OK检测顺序依据、适用两用途抓手、点位与逐次参数。布局更改不能挪格/补位或按显示号复制参数，冻结读取不回查活动配置。新布局准确共同字段/版本见RC10，以下给新增薄信封；不能把旧count接口宣称已支持；不创建第二保存/校验端口。旧合法历史无布局/无抓手仍真实读取，迁移不得伪造选择；编辑缺项和执行准入须定向设计。

原始料盘、OK区域、实际物理格位及实体身份在取料前明确并冻结，后续旋转/采集/判定/回放沿用同一关联；区域号只用于展示/检测顺序，不能代替原始槽身份。特殊OK原槽回放使用分拣抓手，不切成上料抓手或NoMoveRequired；实际取料及必要保存、转运、放料和安全位确认后才推进下一件，失败不记录完成。

操作者不得为OK选择其他目标槽，不增加“是否回原槽/OK处理方式”开关；原型任意OK目标配置含义退出。若已有明确必要的原槽放料参数，归该原槽取放配置；同槽不推导全部取放坐标、高度/抓手补偿相同，不自动复制全部取料值、不编造新参数。

历史读取保持原记录事实，不把旧任意OK目标/旧完成标记重解释成已按本次原槽规则执行。当前显示与执行必须区分普通无需搬运事实和特殊实际原槽回放事实；缺实际保存/动作证据时不补造完成。字段/版本/历史策略见RC10，本次接口设计如下，当前代码仍未消费。



### API-L00 新建类型、同源解析及配置准备（本次目标设计）

仅基础信息已授权场景/类型业务选择，不新增操作者Source、Route、Pattern或模板选项。场景1普通/特殊选择明确传入InspectionKind，场景2成组、场景3半成品只适用ordinary；UnitKind沿现independentPart/looseGroup/assembledEntity。正式ScenarioId取既有场景配置/目录值，下面“场景1/2/3”是业务名称，不虚构其字面代码。

```csharp
record EditorDraftRequest(string Model, string ScenarioId, string UnitKind,
    RecipeInspectionKind InspectionKind, string? SourceRecipeId = null);
```

JSON精确为model/scenarioId/unitKind/inspectionKind/sourceRecipeId；InspectionKind按RC10 ordinary/specialRotation。Model原值比较、其他键Ordinal精确匹配，不按名称含“特殊”、Test配方号或数据库首条推断。接口Recipe.Write；不要求设备在线，不产生计划或保存/批准。

| 场景与输入 | 允许的UnitKind/InspectionKind | 程序选定Route（不让操作员填） |
| --- | --- | --- |
| 场景1普通 | independentPart / ordinary | ordinaryBatch |
| 场景1特殊 | independentPart / specialRotation | specialType1Part |
| 场景2成组 | looseGroup / ordinary | ordinaryBatch |
| 场景3半成品 | assembledEntity / ordinary | ordinaryAssembly |

此关系的业务校验只由共同RecipeDefinitionValidator及RC10承担；API/UI是选择映射。非法组合不进入查找，不按请求任意Route造执行能力。更新已存配方也须满足同关系，不能改InspectionKind而留下旧Route/UnitKind。旧specialType1WholeAssembly不成为新选择。

1. 从同一SqliteRecipeStore/IRecipeCatalog取得一次一致Snapshot，先按(Model,ScenarioId,UnitKind,InspectionKind及其合法Route)限制来源。正文4要求源显式类型一致。历史2/3仅在现Route、场景、UnitKind及真实配置能准确承接请求类型时作为后台能力来源：ordinaryBatch/ordinaryAssembly对应合法ordinary，specialType1Part仅对应场景1specialRotation。此处是已存在Route语义的来源相容性判断，不回写历史缺InspectionKind、不补历史布局/抓手；旧特殊若不能准确形成两组和共享工位输入就不选首两项/旧出口兜底，须按第5项准备真实来源。
2. SourceRecipeId若提供，必须就在该集合且唯一；不存在、不相容直接拒绝，**不忽略ID后回退另一个来源**。未提供只有该集合恰好一个可用真实来源时选取；0个或多个报告具体配置缺失/歧义，不按Available过滤（生产准入与编辑可用分开），不顺取首项。相同Model普通和特殊可以共存，不互相成为来源。
3. catalog.items追加可空inspectionKind（取实际正文，旧null保持）；原scenarioId/unitKind/route/model保留，用于基础选择上下文与来源ID缓存。SourceRecipeId仅由已完整读取配方/既有后台关联程序取得，不显示技术选择器。正式ScenarioId由已知场景映射/来源catalog取得；无法唯一关联时报具体配置问题，不把UnitKind字符串直接当场景码。改变场景/类型/型号后清除不相容SourceRecipeId和旧候选关联，再发对应draft请求；不得把原普通SourceRecipeId附到特殊请求，也不让迟到旧响应覆盖新选择。历史条目明确null而不是伪造特殊类型标签。
4. 映射器深复制已选来源的有效后台能力：Composition/材料身份与面规则、采集/算法/运动/质量配置、点位单位/基准/已确认姿态引用，**全部有效隐藏字段保留**。返回共同正文4属性形状完整的编辑JSON：RecipeId/Version/DefinitionDigest/CatalogDigest为空、FCode空、PlcRecipeId null、ReleaseStatus draft、批准为空；InspectionKind/ScenarioId/UnitKind/Route按合法业务上下文确定。TrayLayout明确10×10且Cells=[]，Positions/ExecutionPositions/SortingTargets为空；无虚构历史布局或示例槽数。特殊两个StageId按stage:1/2且Action=rotate，引用后台已配置的材料/相机能力，角度、共享工位及各格可编辑坐标/曝光/增益/亮度保持未填；两抓手均null。普通仅适用分拣抓手null，无旋转配置。相机组显示后续业务选择/有效来源，不将AB作为暗默认；新增实际相机项必须有真实后台ROI/光源/算法等配置，三参数留用户填写。必要结构属性不漏，但候选**尚非合法可保存RecipeDefinition**。响应为`{definition, authoringContext:{sourceRecipeId,sourceVersion,model,scenarioId,unitKind,inspectionKind}}`；context取该次实际Snapshot的已选源身份/版本，仅供程序后续编辑使用，不是候选身份或UI技术选项。
5. **合法首次新建的真实准备责任**：014后端负责人依据当前有效相机/光源、算法注册、motion/quality/姿态和special两组/工位的真实配置，提供同型号/场景/类型的共同Definition或可证实的现有Definition来源；012配套负责人将它写入同一SQLite并核完整读取及该tuple唯一来源/显式SourceRecipeId关系。沿现Gaode.StorePrep维护互斥/既有prepare/inspect，增加最小`--prepare-authoring-source <recipeRoot> <definitionFile> <expectedSha256>`准备入口：输入仅**一份完整共同正文4**、校验输入摘要及目标配方库，不是新模板库/第二目录/导入平台；调用同一个ValidateForSave、RecipeDefinitionSerialization与IRecipeStore.SaveAsync作真实提交，记录身份/版本/来源。新来源记录保持draft/未批准，工具不能从输入提升生产批准、捏造硬件参数或覆盖Head；已有来源更新走既有条件更新。用于软件验证的来源要明确真实虚拟配置范围，不能作为生产缺配置的回退；既有seed-test只保持Test用途。

本次实施必须交付“准备实际配置来源→catalog/完整GET→选择场景1特殊→editor-draft→手动布局/输入→检查保存”的可用主流程及证据，不能只返回Unavailable。尚无真实配置值时，记录缺的具体能力/对象、来源文件与责任人，作为该部署首次新建前置；真实来源未到不编造，也不阻已有合法来源的创建/保存。实际准备命令是后续实现义务，本轮没有运行或新增工具代码。

### API-L01 editor-layout设计签名（替换数量生成，不是已实现）

现POST `/api/v1/recipes/editor-layout`沿Recipe.Write，响应仍`{definition:完整共同正文}`，无保存/计划副作用。薄信封：

```csharp
record EditorLayoutRequest(
    JsonElement Definition,
    string? SourceRecipeId, string? SourceVersion,
    RecipeTrayLayout? TrayLayout,
    int? Faces, string? Material, int? LocalFace,
    string? StageId, string? CameraPair, bool? ExtraE, int? Members);
```

共同TrayLayout/定义只引用RC10，不另建Grid业务模型。TrayLayout=null是本次未提交布局变更，非清空；显式Cells=[]表示清空选格的未保存候选（必要填写仍拒绝保存）。移除Count、NgCount、PendingCount及对应自动克隆行为；适用Members/Faces只是既有编辑操作，不能凭数量自动造独立实体/工艺配置。未提供操作不改原输入。

服务端使用唯一共同数据映射：同CellId同Region保身份/有效隐藏配置；删除/改区只清旧绑定；新增格建立程序身份及空待填拍照/取放输入，不复制首格、不配PhysicalSlotIndex=数量+1、不默补抓手。CellId由行列核并按RC10程序生成，无操作员技术输入。已有正式场景配置缺来源时真实指出依赖项，不任取第一条或测试目录。映射器负责结构编辑，保存及工艺准则仍唯一Validator，不生成执行计划。

editor-draft的合法来源准备、类型映射和候选形成按API-L00；特殊首次新建必须实际准备同类型真实配置来源，不能把Unavailable作为主流程设计替代。来源未齐仅保具体部署限制和准备责任，其他完整读写可独立继续。

POST/PUT AuthoringRequest仍`requestId+definition`，Definition必须正文4且完整携带RC10；新增业务字段不另建端点/保存服务。GET旧2/3按实际版本返回、不自动layout迁移；前端显示缺布局，需要明确关联后才能新保存。GET强ETag和If-Match封装共同Version不变，428/412/CommitUnknown语义/权限/有限等待保留。

### API-L01a 编辑中间态与采集组定位

editor-layout携带draft返回的SourceRecipeId/SourceVersion；新增格/成员/面/相机所需隐藏元数据从相同tuple、同RecipeId且Version精确匹配的实际源解析。完整GET编辑则以已读RecipeId/Version作为程序来源上下文；若请求有新对象而来源上下文缺失或已变化，要求重新读取该上下文，不任取别源/当前新值。SourceVersion只是编辑所用真实来源的一致性引用，不参与最终PUT并发，ExpectedVersion仍只有原ETag的目标Version，不形成第二保存版本规则。尚无对象的空draft依此可建立完整成员/面/点位结构，逐槽坐标和三参数空白；保留有来源的单位/基准/后台姿态/算法/ROI等隐藏元数据，不能因Positions初为空就失去新格初始化依据。

EditorLayoutRequest.Definition使用JsonElement，因为未填写XYZ/参数/角度的null属于编辑中间态，直接绑定严格RecipeDefinition会在布局映射前失败。它是共同JSON的暂存形态，**不是第二业务模型或宽松保存协议**。HTTP布局映射保持所有有效属性/后台来源，仅更新指定CellId/Region及实际输入，不能用默认数值补齐；检查/POST/PUT仍经唯一严格RecipeDefinitionSerialization.Deserialize，完整后调用共同ValidateForSave，缺必需数值由共同解析/问题路径拒绝，不成功裁剪重存。

CameraPair变更须同时给Material、LocalFace及StageId（程序由第一组/第二组或现面卡片上下文携带，不提供技术阶段选择）。后端目标精确定位(Stages.Number→StageId,Material,LocalFace)，坐标精确定位(SlotId/实体或成员,StageId,LocalFace,Camera)；不得仅Material/LocalFace要求全配方恰好一个target。重复AB/AB两组可相同LocalFace，仍按StageId分别建立/修改CaptureProfile及坐标，修改stage:1绝不覆盖stage:2。缺StageId或不属于当前候选范围拒绝，不自行取第一组。适用E沿独立扫码姿态/点位关联，不能把组编号当新检测面或E编码。


### API-L02 显示消费边界

现查询/通知设计新增origin/cell/regionOrdinal/StageId及实际ReturnToOrigin/safe引用，规范见014 EX14-05和共同execution-state；公开业务引用不含PLCraw。结果/运行视图依据冻结/实际提交，历史缺项null/Unavailable。组件或计划结果不等实际移动，件级完成不能把全盘写Completed。无需新页面/新运行查询平台。
