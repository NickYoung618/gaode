# 替代、消费者和验证承接（设计义务，尚未删除）

只删除替代完成且无有效消费者的代码；名字、测试失败或历史身份不是删除依据。本轮无产品/测试修改、无实施勾选。

| 现对象/直接消费者 | 替代/实际删除义务 | 必须保留及验证 |
| --- | --- | --- |
| RecipeEndpoints.Authoring.cs EditorLayoutRequest及111—137数量截断/复制、physicalSlotIndex=数量+1；recipe-authoring.js基本数量/旧槽列表，a.html旧slot样式 | 100格共同CellId映射替代，删除count布局生成、首槽克隆/自动编号物理号、孤立监听/模板入口；核runtime打开关闭/目录调用及tests | 完整隐藏正文、取消/迟到、权限/If-Match/Unknown保护；矩阵重编号不串参/改区局部清除 |
| RecipeRunPlanner.cs沿Positions数组顺序、Validator物理槽<=Capacity/旧specialType1WholeAssembly当前准入、combined转运旋转动作 | 冻结OK行列序及正式映射替代，删除旧顺序/范围及未授权特殊整体分支，新特殊Action=rotate/专门上料；旧负载只真实历史读取，核测试/decoder/配置消费者；所有nullable消费者迁移后首次构建 | 原面/成员/相机/整体批次顺序、Absent/PoseExcluded跳过保号；普通代表 |
| ObjectExecutionInputs.Rotation/RotationTargets.Exits及任意Sorting[OK]当前用法；Planner.ReturnUnit消费者 | 共享工位/两Stage/OriginPutBack固定Origin替代当前任意OK目标路径；旧类型只在准确历史reader确有用途时保留，不留当前备用分支 | Same physical cell不强制Source=PutBack；特殊原槽、普通NoMoveRequired分离 |
| RecipeDetectionExecutor旋转blanket NotStarted、ThreeStageWorkflowExecutor.specialExits/特殊免搬残支 | 真实端口+准入+scope循环交付后删除拒绝/免搬；不能先删保护或新建执行器 | 未配置机械仍局部拒绝；RecipeSortingMapperTests.LegacySpecialExitCannotExemptCurrentNgSorting保留正确负例 |
| FaceResultAggregator/RecipeWorkload/WorkerTargetIdentity/CommittedResultProjection现缺StageId keys | 替换缺组身份的当前key/去重，删除被替代算法关联；核worker序列化/输入合同及图像step消费者 | 普通历史nullable组意义，重复AB/CD四真实采集与逐参数不串用 |
| ThreeStageWorkflowExecutor、WholeTrayWorkflowOrchestrator取最后件Completed当盘证据 | scope事实及全盘汇聚替代；件完成不能授下料/Final | 真实ProductionStageEvidenceRequired、所有参与与排除事实/最终保存门 |
| LatestProtocolStageActionAdapter.Transfer及StageActionResult.Completed只src/tgt | 实际safeReached入Evidence及门，删除不核safe的当前完成判定 | 真pick提交/UnknownHeld/epoch/reservation/占据；失败不下一件，不删正确断言 |
| PLC协议现过期特殊Test地址/出站免搬/单独polling（仅若当前调用存在） | 按真实调用/DI/配置/脚本扫描定位后删除被替代用途；新grab/R纳现signal registry/readplan检查 | 009 raw边界/010共同执行/013降频单源与旧性能偏差；空正式地址不填 |
| HistoricalHandlingEvidence.cs及CommittedResultProjection.SpecialExitCompleted历史reader | 有实际原JSON消费者，保留只读；不是删除对象 | 不改旧事实，不授当前return或免搬；旧失败证据保留 |
| recipe-authoring.js成组/半成品现卡片拼接与重复整体取放 | 导航批准后改为成员独立/部位检测、整体取放只一处；删除已无人引用旧slot/member列表与样式事件 | 成组真实独立搬运；半成品整体，不因UI删除合法成员/面/E能力 |

直接消费者核查范围：Application recipe/ports/workflow/StartPublicPreparation；Infrastructure serializer/store/provider/PLC现适配；Host注册/投影/API；StorePrep；Python Worker的实际消息/结果identity；frontend editor/runtime和精确prototype manifest；相关组件及009/010注册/ledger。nullable物理槽、StageId和schema升级不得以补0、移除正式编译文件或假成功解决。

后续tasks需列具体文件新增/修改/删除与消费者核对结果，本轮只登记可定位设计义务。无消费者的旧测试在替代后删除；正确保存/期限/取消/持料/占据/版本/历史读取测试承接。负例失败本身不能删断言，历史失败报告不删。测试集合仅verification V14-01—07，不全量。


## 本轮直接消费者与契约测试迁移义务

- RecipeEndpoints.Authoring.cs:EditorDraftRequest/筛Model+UnitKind/忽略不匹配SourceRecipeId：以API-L00精确上下文及明确ID拒绝替代；frontend recipe-authoring.js configure/basic selection/catalog缓存与payload白名单承接InspectionKind/布局/抓手/工位新字段。必要来源准备由现StorePrep消费同Validator/Store，不能另模板目录或Test回退。
- EditorLayoutRequest保持JsonElement编辑中间态，消费StageId组定位；删除Material/LocalFace选target恰好1的过期假设，按组更新不影响另组。未填null不进正式保存，不补0；完整GET所有合法隐藏字段保留。
- Gaode.Contracts.Tests/Recipes/RecipeDefinitionSerializationTests.cs的“正文3当前写入”期望迁移为4；保留2/3及冻结原版本摘要准确读负载用例。RecipeRunPlannerTests/FaceResultAggregatorTests、集成RecipePerCaptureTests/RecipeCaptureAdapterTests/RecipeAuthoringCreateTests/RecipeAuthoringUpdateTests/RecipeAuthoringBindingTests，以及frontend/tests/us1/recipe-authoring.test.ts只定向承接新增正文、来源类型、StageId和原槽/冻结关联。历史具名输入/旧报告不改，不继续把3作为当前格式，不删未知字段/缺值拒绝、权限/版本/保存保护。
- 上述测试的实际输入和源码消费者迁移需后续任务阶段登记；本轮只明确义务，未修改测试代码或运行。两件实际OK/8次AB采集、逐Stage worker/保存/查询覆盖优先并入同一代表/必要组件，错误架构或未发现用例仍拒绝。
