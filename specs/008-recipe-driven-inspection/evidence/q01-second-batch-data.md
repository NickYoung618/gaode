# Q01 第二批数据准备（2026-09-25）

状态：**受限数据准备**，不是 Q01 启动、运动或端到端通过。008 T050 仍未完成（Q02—Q22/C 仍待后续阶段），T049 的 Q01 子范围仍有 B04 输入缺口。

- `../fixtures/recipes.json`：`R008-Q01/1.0.0-test`，S1/普通单件、初始面 AB、F 精确码 `TEST-TRAY-0101`；目录 SHA-256 `3381FDA5DC100B02812E7140C2BF022F472C54337F154B5965F8919B11184CA2`。源于现有 Review 模型与 `recipe-cases.md` Q01，只保留该用例所需的单面阶段。产品 `protocolSlotIndex`、A/B `pointRefs` 均未赋值；Review 的虚拟格位中心不能作为获批产品点位。
- `../fixtures/cases.json`：P01/sourceSlotId、单件/成员身份模式、相机序列、未确认映射和 `Restricted` 原因明确列出。该 Test 槽位标识用于准备和查目录，不证明物理槽索引或对象与测高样本的对应关系。
- `../fixtures/media-manifest.json`：复用 007 已校验的 ThreeD、F、Detection PNG；文件实际可读且摘要匹配，来源 `Test/FixedImage`。A/B 当前均指向 Detection 模拟素材，后续按对象/面/相机实际取图仍属 008 T053。
- `../fixtures/worker-manifest.json`：独立进程使用的 Test F 码与目录一致，保留原 10 秒模拟时长和 `Test` 来源。
- `../fixtures/fixture.json`：Q01 FixtureManifest 绑定目录摘要、公共配置/预算/模拟版本、媒体与 worker，`availability=Restricted`；`PrepareOnly` 产物在 `artifacts/recipe-execution-008/Q01-prepared-second-batch/`，未发业务启动。

当前正式 Host 目录查询证据见 `artifacts/recipe-execution-008/Q01-fixture-second-batch-runtime/catalog-and-process.json`；当前错误合同下受限启动请求返回 409 `RecipeRestricted/ProductPointMappingMissing`、`runCreated=false`，见 `artifacts/recipe-execution-008/Q01-api-rejection-v2/restricted-start-rejection.json`。早先 400 通用错误包保留作修正前事实。合同测试为 `backend/tests/Gaode.Contracts.Tests/TestResults/q01-catalog-second-batch-final.trx` 4/4。Test 数值和模拟图片均不解除点位/高度语义缺口；待输入见 `input-readiness.md`。
