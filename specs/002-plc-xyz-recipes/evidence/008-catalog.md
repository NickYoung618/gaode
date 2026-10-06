# 008 首批目录模型核验（2026-09-25）

状态：**部分实现，002 T11 保持未完成**。本批没有 Q01 可派发目录数据，也没有产品高度映射的批准语义。

- `RecipeContracts.cs` 增加只读目录项、用途/摘要/受限原因、物理实体/目标槽位/相机点位/高度轮次的步骤身份；`JsonRecipeCatalog.cs` 校验目录内 F 唯一、显式 PLC 配方 ID、路线与搬运单位、槽位索引及点位引用；`RecipeRunPlanner.cs` 保留可审阅的计划展开，增加 `BuildExecutable` 受限门禁。Review 文件仍为历史 Test 样本，其 68 项均标 `Restricted`，不得直接派发。
- `RecipeCatalogFactory.cs` 可读取由受控 Test 清单指定的 Review 目录路径，目录摘要取原始文件 SHA-256。现有 22 种 Q 序列的描述仍在 008 `recipe-cases.md`；本批模型接收 AB/CD 成对目标和多阶段，但**没有生成或运行 22 个 Q 配方**。
- `008-foundation-contracts.trx` 中目录/规划/worker 协议及 F 旧反馈门禁 9 项通过；同包正式 F 联测遇到心跳超时，原始失败保留。旧 Review 目录的受限断言通过，不能抵扣 Q01 数据和真实执行。

剩余：008 T049 的 Q01 产品 A/B 点位及高度映射尚未确认；008 T050 尚未提供 Q01 版本化数据。取得后须让已确认、完整的 Q01 配方变为 `Available`，并验证非法/未知能力保持 `Restricted`，再判断 002 T11 完成。

第五批当前状态：经用户授权的[虚拟Test映射](../../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)，`R008-Q01`及`R008-Q02/1.1.1-test`在完整Test配置和已支持单面能力下实际Available；两条正式WPF运行至Final，见[同run证据](../../008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md)。旧Review与生产缺映射目录继续Restricted。上方“缺Q01数据/不能Available”是当时状态。T11还要求22种描述的表达/非法能力全部核对，不以两条通过自动勾选。

## 第二批增量（2026-09-25）

`specs/008-recipe-driven-inspection/fixtures/recipes.json` 已提供单条 `R008-Q01/1.0.0-test`，目录摘要 `3381FDA5DC100B02812E7140C2BF022F472C54337F154B5965F8919B11184CA2`，F 精确码 `TEST-TRAY-0101`；S1/P01 的单面 AB 可规划。目录条目由正式 Host GET `/api/v1/recipes/catalog` 实际读回，状态 `Restricted/ProductPointMappingMissing`。`backend/tests/Gaode.Contracts.Tests/TestResults/q01-catalog-second-batch-final.trx` 4/4 验证装载、唯一条目、工作量和受限不可派发。尚缺 P01 物理槽/对象/A、B 点位与公共高度到检测 Z 映射，故 **002 T11 仍未完成**，目录不能标 Available。旧 68 项证据仍只属于 Review 样本。
# 第七批Q03受限目录增量（2026-09-25）

R008-Q03/1.0.0-test独立Test目录当前摘要`FC629D78F6D87FB24AEF347B27A8D5AAD33C05DE118A1D4EFA07BD0CCD67A99F`。P01面1/轮1与面2/轮2的AB Test映射完整，经目录校验仍因自动换面接口未齐为`Restricted:FlipAndHeightContractUnconfirmed`；篡改第二面轮次返回`ProductHeightAssociationMismatch`。冻结计划4图/2融合/1重扫，严格执行校验仍拒绝`FlipMember`。最终代码与Fixture定向测试见`artifacts/recipe-execution-008/seventh-batch-testresults/q03-final-code-and-single-face-regression.trx`，当前PrepareOnly见`artifacts/recipe-execution-008/seventh-batch-prepared-q03-v2/`。这不是Q03实际运动或页面通过。
# 第八批当前增量（2026-09-25）

R008-Q03/1.0.0-test两面/两轮Test映射和预算继续保留；目录限制原因精确为`FlipPickPlaceTransmissionUnconfigured`，不能把Test坐标视为已配置PLC翻面取放字段。目录定向回归3/3通过，详见[008第八批证据](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。002 T11整项条件未齐，未勾选。
