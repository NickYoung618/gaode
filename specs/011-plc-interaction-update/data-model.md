# 011 数据模型与状态承接

日期：2026-10-03。Phase 1设计，未实现或运行验证。共同业务字段的唯一规范是[recipe-contract/1.3](contracts/recipe-contract.md)；本文件描述归属、关系与保存，不重复另一份字段定义。

## 实体、身份和关系

| 实体 / 代码落点 | 主身份和关系 | 生命周期与不变量 |
| --- | --- | --- |
| RecipeDefinition / Application/Recipes/RecipeContracts.cs | RecipeId、Version、DefinitionDigest；FCode和Model分别是料盘号、PLC型号 | 012提供者读写共同定义，011唯一校验；保存不授生产批准 |
| RecipeCatalogSnapshot / 同文件新增 | CatalogDigest、不可变Definitions | 012从独立配方持久库一次一致读取形成；F取得一次视图，后续步骤不反复读取活动目录 |
| RecipeValidationResult / 同目录 | candidate、字段/对象问题及当前目录唯一性 | 保存、读取合法性、规划使用同一业务规则；持久提交原子性由提供者承担 |
| RecipeMatchResult / 同目录 | 一次F输入、目录视图、匹配结果与完整Definition | 未匹配/身份不符/受限保持原因；Matched不等于动作或运行完成 |
| RecipeBindingReceipt / Application共同绑定 | BindingId、实际绑定内容/计划、窗口及适用真实提交引用 | 新绑定是软件操作，无DeviceApplied；取消/过期/提交未知不授权续接，独立bind不授产品续接 |
| RecipeRunPlan / 复用 | Run/Tray、料盘号、配方版本、PlanRevision、有序RecipeStep | 任意配置面序、独立扫码姿态、稳定物理槽、物理实体一次动作；版本不可暗改 |
| FrozenExecutionInputs / ExecutionInputs.cs | execution-inputs/2、完整计划/配置/能力/准入/预算、SemanticDigest及保存引用 | 深不可变；编辑只影响后续F；原execution-inputs/1历史保存不改写 |
| TrayObservation / 新Domain观察值，算法端口承接 | ObservationId、Run/Tray、capture/call、用途/检查轮及物理槽；首次含F定位 | 实际采集/调用/提交，字段见EX01；不能由HeightResult构造正常姿态 |
| SlotParticipation / Domain与运行快照 | PhysicalSlotIndex关联一个物理槽及冻结实体关系 | Unknown→有效Absent/Active/PoseExcluded；异常在本轮保持退出，历史结果不删 |
| Face/Pose执行 / 复用RecipeStep并增目标语义 | 物理实体、LocalFace或ScanPoseId、TransitionId、ActionCorrelation | 扫码姿态不占必检面；换面转姿与放回分别有动作事实 |
| AxisObservation / 现设备语义投影扩展 | Axis用途、Action/epoch、观察/位置/单位 | 五轴独立；缺实际null，禁止原始信号字段进入业务 |
| PhysicalDisposition / 复用 | entity、源物理槽、区域目标、操作与提交引用 | Reserved→InTransit→Completed；UnknownHeld不当完成；OK/异常留原槽有独立依据 |
| RunResult / 复用RunSnapshot与CommittedResultProjection | 技术状态、完整性、质量、姿态、处置、最终保存 | 分别表达；最终返回真实异常物理槽号与已有结果 |
| 完成事实 / 既有StageEvents与保存 | Detection、Sorting、Unload、WholeTray、允许取盘、人工取盘、Final | 各自真实事实及必要提交；分拣未完成不能下料，不能用软件状态伪造机械完成 |

组与整体关系沿已有UnitKind/Member/Part/PhysicalEntity，不把组结果当成员结果，也不按部位重复搬整个实体。姿态异常覆盖真实物理槽映射，观察中无法关联的对象保持受限，不通过排序猜归属。

## 坐标与点位

共同定义保留检测、F扫码、E扫码、翻转取件、翻转放回、分拣源/目标及下料用途，具体字段由RC02/03定义：

- F XY的运行值来自本次首次3D，关联实际Observation/Capture/Call；F扫码Z来自有效公共扫码配置。公共准备不读取尚未匹配的产品配方。
- A/B/C/D的X/Y及检测Z来自冻结配方或点位配置；现ApprovedFixedBasis可承接有效配置Z。原MeasurementOffset/HeightRound不再驱动新检测。
- 姿态复查不更新料盘绑定，也不把检测Z重新计算为观察高度。配置版本、观察轮次、面序与设备连接代次互相独立。
- 取放及分拣点各自关联对象、物理槽、用途、单位/基准/版本/来源，实际读回单独保存。相同料盘位置不合并用途字段。

## 必要保存与读取

012采用独立SQLite配方库，运行事实继续保留在原SQLite运行库及媒体存储，沿已有写回执；共同字段不依赖表结构，不新增版本平台或跨库/设备事务。身份/版本/摘要及条件更新唯一见RC01/RC04.1。

| 保存节点 | 沿用位置 | 真实门禁 |
| --- | --- | --- |
| 配方新建/编辑 | 012独立配方库适配、共同IRecipeStore | 共同校验、码唯一、ExpectedVersion条件、真实提交确认才Saved；后续一致读取见新内容 |
| 公共3D/F及复查 | RunExecution.SaveAsync→ITraceWriter/RunWrite、已有CaptureFact/AlgorithmFact | 当前媒体/call/观察关联且必要提交成功才提供依赖依据 |
| 冻结/绑定意图与结果 | RecipePlanAndBindingIntent及既有运行写链 | 实际内容版本/摘要/配置/预算可重读；不从新目录补旧快照 |
| 动作意图/反馈 | 现IStageEventStore、设备证据保存 | 关联动作、真实回执、窗口和保存状态分别核验 |
| 取料在途 | 现IPickCommitPort/分拣状态保存 | 真实取料且当前有效提交回执取得后才授权放料 |
| 最终完成 | 既有WholeTray/Removal/Final保存 | 所有适用事实齐备，不用入队、日志或页面状态替代 |

新语义负载显式版本化，读取时分辨历史记录；旧handoff record与摘要序列化不任意加字段破坏原摘要，新快照通过已有PlanReference/Run/PlanRevision引用已提交负载。新动作证据无对应字段的历史记录保持Unavailable，不补写旧库、不重建动作授权。若实现确需表变更，必须沿既有受控StorePrep核真实需要；本设计优先版本化既有payload，本轮无数据库操作。

## 状态推进与取消

保存的目录内容、绑定意图、运行冻结、设备已完成、已提交结果、允许人工动作、最终完成是不同事实。所有进度由同一个共同执行者推进；Test/Real只换叶适配和有依据的准入。

取消关闭本轮后续动作资格；相机/算法媒体在实际InputReleased或进程退出确认后释放，不能把取消请求当释放。已经派发但结果未知的动作保持UnknownHeld，迟到成功/提交可作为历史事实保存，但不恢复过期动作资格。

公共/产品运动、翻转放回、复查与E都使用现绝对窗口；工作量改变先调整有来源的冻结预算模型，不在运行中加时。新的恢复、安全控制和ASCII承载仍按DEP各自限制。

## 当前代码与迁移边界

原HeightResult、PlcRecipeId、统一XYZ与旧源枚举是否存在历史读者，按plan逐文件清理表核实；新实现不得保留无用途的当前执行分支。已删除010整段测试执行器不重新建设。数据模型完成不意味着真实算法已输出TrayObservation、目录已可保存、PLC已互通或012页面已消费。


## G-01具体字段交付

共同结构唯一定义见recipe-contract/1.3 RC08：用途点/Flip.Stages/ExtraPose嵌套、TargetPose引用及当前正文2；目录快照recipe-catalog-snapshot/1与冻结execution-inputs/2各有职责。011实现RecipeDefinitionSerialization，012消费并持久完整正文；不得自行补字段/原码或以旧格式补默认值续跑。012已交付1.2最终设计，新1.3设计消费回执及7份增量已接收合入，共同代码仍待交付。
