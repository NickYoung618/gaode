# 多对象虚拟Test配置与共享实体执行增量（2026-09-26）

依据业务确认U01/U02/U07及原GROUP/ASSEMBLY任务。仅开放版本化simulationOnly/Test目录；生产坐标仍受限。既有普通配置保持原结构与摘要。

每个位置允许`resolvedObjects`按来源物料名索引。每个成员/部位有独立`pointRefs`、`resolvedSourcePoint`、`resolvedDetectionTargetsByFace`及适用`resolvedFlipPosition/resolvedSortingTargets`。目标身份沿用0.5合同，objectId对应该成员模式，sourceSlotId/协议槽号仍是实际物理槽；点位、测量来源、面、相机、版本、范围逐项校验，不能用父位置目标填缺成员。

S2散件：成员独立检测/翻面/源目标；只翻仍有必检面的成员。每组保存成员明细及NG优先汇总；OK成员无搬运动作，NG/Pending成员按自己的可靠结果和合法目标逐个处置。`physicalUnit=problemMembersOnly`明确采用U01，不将旧整组处置目录自动开放。

S3整体：部位目标独立、媒体和算法身份独立；物理整体使用父位置源点/翻面点/处置点。每阶段整体只翻一次，已完成部位不重采；部位结果汇总为整体NG优先/Pending/OK，保存部位明细、整体结果及实际共享动作。外围Sorting只消费整体结果，不能拆抓部位。

首次3D与F绑定在公共准备执行，翻转放回后另行3D姿态复查但不重绑F；多对象按物理槽位关联观察，各检测目标来自配置。预算从真实展开图数、融合数、逐实体翻面及适用处置计算。新目录、素材、worker清单和实际Host加载入口同步；不以计划或组件成功替代正式页面Final。

## USR-E四面对象适用性

S2逐成员、S3逐实际检测对象检查：固定四面仅3CD＋1AB且AB位置由配方确定；一面两面及源表成员组成、面数不变。GROUP旧Q12仅历史，当前Test四面必须采用3CD＋1AB配置，Q09仅历史，不当生产标定。实际GROUP型号F 1/2面及ASSEMBLY BASE两面/PIN一面未因此自动失效；四面整体仍共享实体一次翻面/处置。E、旋转、人工和组/整体独立场景不删除。

### 010实施定向对齐 A01（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A01**：共同输入不含ProfilePayloads/PositionPayloads或fixture JSON；文件解码和替代语义提供者调用唯一RecipeDefinitionValidator，RecipeRunPlanner保组成/面/必检/身份规则。环境提供CoordinateDefinition，共同CoordinateResolver执行测量关联、偏置/单位/范围及对象/面/轮/槽校验，不由环境预算业务Z。TestEligibleSlots移批准边界。
  生产/消费与010实施承接：catalog/validator/planner→Start/绑定/预算/移交/检测/分拣→API/fixture/投影；T008/T009/T011/T012/T016—T020。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
