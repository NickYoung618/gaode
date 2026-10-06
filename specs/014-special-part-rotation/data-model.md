# 014数据与状态设计

2026-10-05 Phase 1；准确配方类型、序列化和校验只见[共同RC10](../011-plc-interaction-update/contracts/recipe-contract.md)。当前实现1.4/正文3，本设计1.5/正文4未实现。不重复另一套共同合同。

## 关联链

`RecipeId+Version → TrayLayout.CellId/Region → Position.SlotId → UnitPattern/实体/成员 → StageId/LocalFace/Camera/PointRef → CaptureProfile`。

运行另关联实际TrayId、正式PhysicalSlotIndex、全盘PlanRevision及OriginalSlotReference；冻结OK号是顺序投影，非目的地、PLC地址或3D号。NG/Pending实际目标格只与既有分拣配置关联，不变成待检实体。

矩阵变更集合按CellId比较：未变格原配置不迁移；删除或改区仅清其旧绑定/无消费者局部参数。区域号重算不改SlotId/成员/拍照关联。新加入OK格需明确输入，不能复制第一格或取料点。

## 搬运与检测实体

| 类型 | 检测输入 | 搬运输入/顺序 |
| --- | --- | --- |
| independentPart普通 | 原实体每面/每相机 | 既有阶段内OK序，普通OK不额外分拣 |
| looseGroup | 对应成员Material/MemberPattern的拍照 | 成员是独立实体，既有成员序/独立取放保持 |
| assembledEntity | 同一整体内各部位拍照 | PhysicalEntity统一搬运/翻面/分拣，部位导航不分裂实体 |
| specialRotation | 两StageId各AB或CD，四次采集 | 场景1独立件，OK序逐件上料/判定/处置/safe，特殊OK回自身原槽 |

## 件scope与事实

DetectionExecutionScope仅UnitId/SlotId，不复制Recipe或Frozen；其实体/成员/步骤必须属于同一完整Plan。组内完整性、融合/参数/算法事实以StageId隔离，旧CoordinateEpoch保持原姿态观察轮次含义。

每件事实关联RunId/TrayId/PlanRevision/Scope/OriginalSlotReference/OperationId/connection epoch，状态分别为技术、质量、保存与物理处置。特殊OK的ReturnToOrigin必须有实际picked+提交receipt+placed+safeReached；普通OKNoMoveRequired必须有原槽事实。件ScopedDetectionCompleted不作为整盘Completed；所有参与件完成且排除记录齐全才全盘检测/处置聚合、下料、最终保存。

## 版本读取与存储

新布局/抓手/原槽/参数纳共同内容摘要及深冻结。正文4新写，2/3真实历史读；冻结3新写，2版本校验与原字节身份保持；旧布局缺失显示未配置，不能自动10×10生成。SQLite沿既有完整JSON/Head事务，不建第二库或额外版本平台；ETag、并发拒绝、未知提交、生产准入保护不改。详见012data-model与共同RC10。
