> 当前确认（2026-10-06）：DUI02/03已获明确批准，原预览只读；严格按navigation-approval-20261006.md推进剩余六项。下文此前“待审/未批”是当时记录，不再作为当前阻塞。实施/验收状态以本轮实际回执更新，批准不等于Passed。

# 012数据与状态设计

历史1.4已集成数据映射见[唯一共同增量合同](contracts/capture-and-gripper.md)：正文3局部CaptureProfile与配方级SortingGripperId，旧正文2历史读取/null字段不写出，SQLite表形状不变。

日期：2026-10-03。独立SQLite已获调度确认。本文定义012持久化封装及编辑状态；业务字段、校验及结果唯一定义为[recipe-contract/1.4](../011-plc-interaction-update/contracts/recipe-contract.md)。字段消费见[editor-ui](contracts/editor-ui.md)，G-01消费与代码交接见[shared-integration](contracts/shared-integration.md)。

## 共同内容：引用，不重定义

| 共同对象 | 012使用方式 | 合同依据 |
| --- | --- | --- |
| RecipeDefinition | 完整读取、编辑、序列化保存；未修改的合同字段原值保留，不由目录摘要重建 | RC01—03/RC08；G-01准确类型与序列化规则已接收 |
| RecipeId / Version | 服务器调用011的RecipeDefinitionIdentity.CreateRecipeId/CreateVersion；新建身份稳定，编辑保持RecipeId，每次成功提交新Version | RC01/RC04.1；不透明唯一令牌，不作大小比较；012只调用011唯一实现 |
| FCode / Model | FCode原文本全目录唯一，不Trim、不改大小写；Model保留独立产品型号含义 | RC01/05；不以场景分区放宽唯一性 |
| DefinitionDigest | 调用011统一函数取得并原值持久化；校验/读回/冻结对账共用 | RC01/RC04.1；ComputeDefinitionDigest及SHA-256/规范化责任已交付，G-02关闭；代码阶段由011唯一实现 |
| CatalogDigest | 当前目录视图的来源摘要，不能限制后续F使用新内容 | RC01/04/05；与单配方版本并发规则分开 |
| RecipeValidationResult / RecipeSaveResult | 原样消费共同问题及Saved、ValidationFailed、VersionConflict、SaveFailed、CommitUnknown | RC04；无第二校验或保存结果枚举 |
| RecipeCatalogSnapshot / 冻结输入 | 完整不可变目录交011一次匹配；运行使用execution-inputs/2冻结内容 | RC04/05；012不生成步骤/计划 |

更多面、四面3CD＋1AB、独立E、用途点位及三区域均由共同配置表达和校验，不设产品名称、测试编号或固定专用分支。结构合法保存不要求设备在线；生产准入单独判断。

## 最小持久化封装

独立配方库两表；已有EF迁移元数据不属于业务表。物理列不是新业务模型。RecipeId、Version的权威值仍为共同合同身份；删除先前设计的SaveId/saveToken，不保留额外并发或业务版本。

| 表 / 列 | 来源 | 约束与用途 |
| --- | --- | --- |
| RecipeHead.RecipeId | 共同RecipeId | 主键 |
| RecipeHead.FCode | 原始料盘编号 | 非空全表唯一，SQLite BINARY比较；不做裁剪、大小写折叠或场景前缀 |
| RecipeHead.CurrentVersion | 共同Version | 与RecipeId组成指向同身份正文的外键；切换与正文插入同事务 |
| RecipeSavedContent.RecipeId / Version | 服务器生成的共同身份/版本 | 联合主键；正文不可变，避免并行SaveId |
| RecipeSavedContent.DefinitionDigest | 011统一函数结果 | 和正文一致，不能以文件hash替代 |
| RecipeSavedContent.ContractVersion | 本记录采用的共同合同修订标识 | 集成1.4/本次新行设计1.5；是适配元数据，不冒充Snapshot.SchemaVersion或execution-inputs负载版本 |
| RecipeSavedContent.DefinitionJson | 完整共同定义 | 保留所有合同内有效字段和用途引用；无演示脚本、寄存器、素材路径 |
| RecipeSavedContent.SavedUtc / ActorId / RequestId | 服务器实际时间、已认证主体、请求关联 | 审计及未知结果核对；不是第二版本/幂等平台 |

旧正文仅承接历史关联与已确认保存查询，不增加历史版本管理、审批、发布、回滚界面。历史运行依据仍为011运行库中的冻结事实，配方库不与运行库建立跨库事务。磁盘适配直接调用011 RecipeDefinitionSerialization.Serialize/Deserialize；不支持的新结构明确限制读写，不能丢字段后“成功”重存。

## G-01完整正文与序列化消费

已接收1.3 RC08，G-01设计消费关闭；下表明确历史1.4与本次4/3层次，唯一定义见RC10。精确“字段→表单→保存→重读”表以[当前完整字段映射](layout-design-20261005.md#ld02-共同字段输入保存完整重读)为消费记录，唯一定义仍为011合同，不复制C#领域类型。

| 层次 | 本轮确定值/消费 |
| --- | --- |
| 持久记录ContractVersion | 当前1.4、本次设计1.5（旧1.3保留），记录适用合同修订，不等于配方Version |
| RecipeDefinition.SchemaVersion | 本次新候选/保存4；已集成1.4写3属历史范围，2/3按原版本真实读取；API/DefinitionJson/完整GET同一正文 |
| RecipeCatalogSnapshot.SchemaVersion | recipe-catalog-snapshot/1；GetSnapshot一次一致完整读取 |
| FrozenExecutionInputs.SchemaVersion | 本次新冻结execution-inputs/3；历史2（及现有真实1历史reader）按原版本/摘要读取，不自动升级；012不编辑冻结负载 |
| 正文编解码 | 011 RecipeDefinitionSerialization.Serialize(RecipeDefinition): string / Deserialize(string): RecipeDefinition及同一版本常量；camelCase，Purpose精确字符串，字典键Ordinal原文 |
| 缺值/可空 | 必需字段与数值不自动补齐；null、[]、{}按RC08适用性表达；新PlcRecipeId=null，候选身份可空字符串；Saved身份非空 |

完整保存Coordinates检测XYZ、PurposePoints的FlipPick/FlipPutBack/EScan、Flip.Stages逐阶段引用、ExtraPose/TargetPose对象及Source/Sorting各自用途。元数据、受控配置来源及有有效用途的AngleDeg/Rotation都须往返保真；运行和目录后续读取不重新解析活动点位来源。

正文2不包含CoordinateRule、旧相机PointRefs、测高HeightRound/Measurement/ResolutionKind或FlipTarget.Target/Mode/ManualWaitMs。012两个适配文件迁移有效配置必须补有真实来源的新用途输入后再调用共同规则；不足的旧配置只保合法历史读取/受限事实，不静默裁剪、补零或成为第二正式来源。SQLite表结构、两表关联及1.2提交语义不改。

## 单一提供者与保存一致性

一个SqliteRecipeStore实例同时实现共同IRecipeStore和IRecipeCatalog；同一Host的检查、提交、目录、完整读取及F取样使用它。无012专用保存端口、AuthoringService业务层或内容身份服务。薄API只负责权限、HTTP与共同调用；GetSnapshot每次从已提交Head和正文取得一个一致不可变视图，不保留第二正式文件目录或启动固定缓存。

1. HTTP权限及格式检查后，PUT将If-Match的共同Version直接作为RecipeSaveRequest.ExpectedVersion；POST为null。TargetRecipeId从PUT路由取得，POST为null；Candidate是RecipeDefinition，RequestId仅关联。客户端元数据不能授予身份、版本、摘要或生产批准。
2. 在正有限的写入串行边界和短事务内读当前快照，核ExpectedVersion；编辑保持RecipeId并保留受控Approval/ReleaseStatus，新建调用011 CreateRecipeId生成RecipeId。调用011 CreateVersion生成待提交Version并使用已有受控准入元数据来源，不能从请求制造生产批准。
3. 同一边界调用RecipeDefinitionValidator.ValidateForSave(candidate,current)。保存不依赖先前检查结果，不重写面/E/点位规则；共同验证含全目录F唯一性，数据库唯一约束保证并发落地。
4. 通过后调用011统一DefinitionDigest函数，以当前Head和候选形成下一目录视图，原子插入完整正文并切Head。目录视图摘要由012提供者按共同视图语义生成；采用按RecipeId排序的RecipeId/Version/DefinitionDigest清单稳定编码计算，属于CatalogDigest，不替代011的DefinitionDigest。
5. 正文中的派生CatalogDigest保存为该次提交视图来源，完整GET返回这个不可变保存表示。当前目录Snapshot.CatalogDigest在其他配方提交后可变化；F的RecipeMatchResult记录取样视图摘要，不拿正文中的历史来源或旧展示值作锁。011规划/冻结须消费Match携带的当前来源，这是IC-02/04的接线要求。
6. COMMIT确认后即依据本次实际提交记录返回Saved及RecipeId/Version/DefinitionDigest/CatalogDigest与完整内容，不另设活动缓存发布或提交后重读门。后续GetSnapshot直接读取同一持久来源，提交后的新读取可见新Head和完整正文。提交后回包/重读失败与未保存分开：已知提交不降为SaveFailed；只有COMMIT本身无法证实时为CommitUnknown，无回执时UI保留未确认。
7. F实际解码后由011只取一次GetSnapshot并Match；保存之前取到旧视图的在途匹配仍使用该一致值，提交之后开始取样必见已提交新内容。Match之后准入、规划与深冻结只用该值，运行中不Resolve最新Head。
8. VersionConflict不写库，实际验证失败/已证实未提交失败不前移Head。取消/超时且无法确认COMMIT用CommitUnknown；重读与RequestId/身份确切关联后才解除未知，不自动重发或回滚UI。新建回执丢失且无法关联时保持未知，不新增恢复平台。

CatalogDigest的具体稳定编码沿提供者实现确定并记录，不新增对外摘要算法协议；Snapshot.SchemaVersion使用recipe-catalog-snapshot/1，不能填合同修订号。旧G-01—03与共同基础实际接收已关闭，本次共同RC10为设计增量，尚未实现。

## HTTP并发映射

完整GET的强ETag使用表示格式号及RecipeId、Version的可逆base64url编码：`"recipe-read-1.<RecipeId编码>.<Version编码>"`。这是共同身份/版本的HTTP封装，无SaveId或额外并发规则。GET正文只含固定格式的不可变保存表示与保存审计信息，动态准入、权限和当前目录摘要在catalog/validate返回；正文原有CatalogDigest按上文保留提交时来源。相同Version的保存表示不因其他配方更新而改变。

PUT解析并核ETag中的RecipeId与路由一致，将Version原值送ExpectedVersion。服务器在写边界与当前Version精确比较，VersionConflict映射412；缺If-Match为428，格式无效为400。新建ExpectedVersion为空。不能比较CatalogDigest、摘要、时间或字符串版本大小作为第二覆盖条件。表示格式若未来变化另作明确协议变更，本轮不设计多版本兼容。

## UI状态

| 状态 | 依据 | 后续与限制 |
| --- | --- | --- |
| Closed / Loading | 关闭 / 正式完整读取中 | 不浏览器持久草稿；读取失败不套默认配方 |
| Editing / Checking | 编辑 / 调共同检查 | 三部分保留完整值；仅输入格式提示，不在前端判断工艺 |
| Saving | 正式保存等待 | 禁重复提交；关闭取消本地等待不宣称撤回事务 |
| Saved | 共同Saved结果 | 更新版本基线并重读；重读失败另示“已提交、重读失败” |
| Rejected / Unknown | 明确拒绝 / CommitUnknown或无回执 | 保留本次输入，重读核实；不假成功、自动重试或提升生产批准 |

界面状态不是业务执行枚举。迟到读取不能覆盖另一选中对象；运行页按runId及冻结recipeExecution显示，弹窗新内容和目录变化不覆盖它。


## 2026-10-05稳定格位与完整保存设计

所有配方共用原弹窗三步和手动10×10实际布局，槽数量仅统计；第二页保原位/空位、区内独立编号及高亮。稳定格位+区域关联点位/成员/每次拍照，取消/改区只清本格，显示重编号不串值。OK号决定阶段内检测序，跳过保号；普通面/成员序、翻面和整盘统一分拣保持。特殊属于场景1，独立两用途抓手、共用工位取放/两组绝对角及逐槽本件原始OK槽的放料关联，逐件闭环由014共同执行负责。

旧已实现count自动生成/顺序依数组/旋转不可执行/分拣抓手只存不接PLC是基线事实，不再是本轮目标；特殊OK不得套普通NoMoveRequired。旧任务ID/勾选和旧证据不改，不用旧完成证明新增需求。共同字段/版本/接口与消费者及历史读取已在RC10和layout-design落型；导航DUI02/03于2026-10-06批准，当前按既有T041—T049承接，不重新生成任务。没有新的012模型、业务校验/匹配器或执行路径。014主责必要共同和通信增量，012只编辑保存和界面消费。

原始料盘、OK区域、实际物理格位及实体身份在取料前明确并冻结，后续旋转/采集/判定/回放沿用同一关联；区域号只用于展示/检测顺序，不能代替原始槽身份。特殊OK原槽回放使用分拣抓手，不切成上料抓手或NoMoveRequired；实际取料及必要保存、转运、放料和安全位确认后才推进下一件，失败不记录完成。

操作者不得为OK选择其他目标槽，不增加“是否回原槽/OK处理方式”开关；原型任意OK目标配置含义退出。若已有明确必要的原槽放料参数，归该原槽取放配置；同槽不推导全部取放坐标、高度/抓手补偿相同，不自动复制全部取料值、不编造新参数。

历史读取保持原记录事实，不把旧任意OK目标/旧完成标记重解释成已按本次原槽规则执行。当前显示与执行必须区分普通无需搬运事实和特殊实际原槽回放事实；缺实际保存/动作证据时不补造完成。准确字段/版本/历史策略只见RC10；012映射见layout-design及API，本设计交付不代表代码或运行通过。


当前新正文4/冻结3/记录1.5仅设计；完整往返矩阵关联、映射、抓手、OriginPutBack和逐Stage参数见RC10。原数据表/ETag/提交结果语义不变。


## 新建来源及编辑暂存（本次设计审查修订）

来源解析和最小配置准备只见API-L00；没有第二来源库。SourceRecipeId仅程序上下文，须同(Model,ScenarioId,UnitKind,InspectionKind/Route)相容，不忽略非法ID回退。draft响应的authoringContext/sourceVersion只供后续新格隐藏配置映射，不是第二保存版本/锁，PUT仍只原ETag ExpectedVersion。空待填candidate使用共同JSON的JsonElement暂存，完整保存仍唯一严格正文4/Validator；未填值不补0。两组采集的编辑请求携带现StageId，不能只用LocalFace。
