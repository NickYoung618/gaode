# 008 新协议实施批次：当前可执行子范围

日期：2026-09-26。以下仅为 Test/VirtualPlc、虚拟相机、独立 worker、SQLite 与正式 WPF/WebView2 的证据；不代表实机端到端验收。历史记录及任务勾选保持不变。

## Q03 正式页面检查点

- 通过包：`artifacts/recipe-execution-008/page-q03-interactive-20260926-v3/`，`validation-result.json` 的 `exitCode=0`；页面证据 `Q03/recipe-webview2-page-evidence.json` 的 `outcome=FinalPageDisplayed`。
- 页面实际选用 `R008-Q03/1.0.0-test`，点击启动并收到 HTTP 202；实际点击“取盘确认”。同一 `runId=0c7fed07-bf13-4af7-b195-a70866097790` 到 `FinalUnloadCompleted`，最终状态 26、`errorCode=null`。`Q03/page-api-device-facts.json` 同时保存 run、阶段、来源矩阵、PLC 状态与变化；`station01.test.db` 和 `media-root/` 为该 run 的实际保存产物。
- 单次公共 3D/F 后，两面 AB 分别采集和 worker 分析/融合，初始 3D 测量身份跨面沿用。PLC 变化序列 310—323：目标面 2，Flip 状态 1→2、实际面 2、Flip_OK 1→状态 0→Flip_OK 0。盘末顺序为 Detection→UnloadPreparation→Sorting（普通 OK 无搬运动作依据）→WholeTrayCompletion→ObservedUnlocked→页面取盘确认→Final。媒体与来源矩阵在最终 API 证据中可查。
- 先前两个页面尝试分别保存在 `page-q03-interactive-20260926/` 与 `page-q03-interactive-20260926-v2/`。前者平台启动期间 PLC 超时，后者在 Running3D 时 Modbus 读超过协议 1 秒并安全阻断；均非通过证据，未自动重发或代确认。

## 连续实体与同盘分拣

- 双槽 `Q03-TwoSlot` 仅为新增 Test 虚拟映射；生成器和夹具为 `fixtures/generate-q03-test.py`、`fixtures/recipes-q03-two-slot.json`、`fixtures/fixture-q03-two-slot.json`。正式 Q03 页面单槽夹具及摘要未改变。通过包 `artifacts/recipe-execution-008/runtime-q03-two-slot-20260926-c/`，`runId=5ec3fd1b-8dc4-4722-9edc-9642818d2eff`；P01、P02 连续各自翻至面 2，两次 Flip 均有状态 1→2、实际面 2、Flip_OK 1→0，完整检测、保存、下料、无搬运与解锁到 `AwaitingManualTrayRemoval`。此包由后端 API 启动，仅证明双实体子能力，不抵正式页面 Final。
- 双槽失败包 `runtime-q03-two-slot-20260926-a/` 暴露第二实体翻面顺序校验错误，已修 `RecipeExecutionCoordinator.ValidateFaceRoundOrder`；`-b/` 证明 P02 Test 来源点外层 ID 错写 P01 会被盘末映射拒绝，已修生成器；两次失败均保留。
- 已明确的单件 NG、Pending 同盘分拣分别保留在 `runtime-q03-ng-20260926-g/`（`runId=ae2467a4-b545-4bbf-9e4c-8924aec1abfc`，P01→P14）和 `runtime-q03-pending-20260926-m/`（`runId=908a416c-44e9-463b-9bd1-bb878089342a`，P01→P15）。真实取料、放料、Sorting_OK/ACK、SQLite 阶段事件及解锁达到 `AwaitingManualTrayRemoval`；这两包是后端诊断，未进行页面取盘确认，不能记作 Final。混合 NG+Pending 的容量/优先决定仍未提供，不据此扩推。
- 随后正式 WPF 页面复核在 `artifacts/recipe-execution-008/page-q03-sorting-interactive-20260926-v2/` 通过，`validation-result.json` 的 `exitCode=0`：`Q03-NG` 的 `runId=47553421-8ccb-4a74-9dca-ed87a6bfeb6c` 从 P01 分拣至 P14；`Q03-Pending` 的 `runId=0551aa03-dd36-4cc9-bcf7-cbab99b728a8` 从 P01 分拣至 P15。两条 run 各自由正式页面选用配方、启动、取盘确认并达 Final（状态 26、`errorCode=null`）；PLC 变化证明各自先取料 `Sorting_Cmd=1→Status=2`，再提交目标与槽号、放料 `Cmd=2→Status=3`，最后 `Sorting_OK=1→Status=0→Sorting_OK=0`。各自 `page-api-device-facts.json`、SQLite、媒体及截图可回查。首轮页面 NG 因运行前 Modbus 超时安全阻断，失败包 `page-q03-sorting-interactive-20260926/` 保留，不算通过。

## 验证与剩余范围

- 合同全集在双槽校验修正前为 232 通过、3 个旧 Rescan Q03 用例跳过；双槽修正后新增定向合同测试通过，Flip 关联、心跳、Q01-PARAM 相关 9 项通过。Q01/Q02 受影响主流程 3 项通过；必要保存失败阻止后续动作与 Final 的集成测试通过；前端 35 项通过；Host 与正式 WPF Release 构建通过。旧 Rescan 用例不能作为新版覆盖证据。
- 缺目标/错来源不得运动、错面不得续检、PLC 通信超时与断联停派发有对应合同/失败包；本批尚无独立的实际 Flip_OK 超时整链失败包，应继续保留在未完成范围。
- `backend/src/Gaode.Infrastructure/Recipes/catalogs/recipe-catalog-review.json` 当前仍为 review 0.4，68 条评审配方中的 2/4 面路线缺少新版合法逐面目标与实体来源/高度映射；这些路线当前为 Restricted。Q03 Test 点位不能自动充当 Q04—Q22 的点位依据。没有可直接继续跑到 Final 的其他无 E 自动多面配方；Q04—Q22、C01—C08、E、人工、旋转与混合分拣仍保持原任务范围与未完成状态。
- 此批为 001 T090、002 T11、003 T071 自动、006 T048/T049、007 T033 及 008 T050/T051/T057/T060/T062 的直接子范围进展；上述父任务和 008 全项的勾选未改变。Q03 页面通过不抵 T062、003 T071 或 008 整项完成。

受保护的协议 DOCX、原始图、需求 Word、客户原型与历史证据共 25 项，本批结束核验 SHA-256 与执行前基线一致，变化数 0。`.specify/extensions.yml` 的 `hooks: {}`，无可执行收尾 hook。

## 本批修改文件清单

- `backend/src/Gaode.Application/Ports/StagePortContracts.cs`
- `backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs`
- `backend/src/Gaode.Application/Station01/PublicPreparationHandoffV2.cs`
- `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`
- `backend/src/Gaode.Application/Workflow/RecipeExecutionBudget.cs`
- `backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`
- `backend/src/Gaode.Application/Workflow/RecipeSortingMapper.cs`
- `backend/src/Gaode.Application/Workflow/RecipeWorkload.cs`
- `backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`
- `backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`
- `backend/src/Gaode.Domain/Station01/SortingMappingContracts.cs`
- `backend/src/Gaode.Host/Program.cs`
- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`
- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`
- `backend/src/Gaode.Infrastructure/Devices/Plc/ProtocolLatestMap.cs`
- `backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs`
- `backend/src/Gaode.Infrastructure/Recipes/RecipeCatalogFactory.cs`
- `backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`
- `backend/tests/Gaode.Contracts.Tests/Devices/InspectionHandshakeSequenceTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Devices/SingleFaceDetectionIntegrationTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Ports/PlcStageActionPortContractTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Ports/StagePortContractTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Recipes/PublicPreparationTargetResolutionTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Recipes/RecipeCatalogTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Recipes/RecipeExecutionCoordinatorTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Workflow/DetectionRetryAndPendingTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Workflow/ThreeStageWorkflowExecutorTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Workflow/WholeTrayWorkflowOrchestratorTests.cs`
- `backend/tests/Gaode.Integration.Tests/Support/Station01HostFixture.cs`
- `frontend/src/runtime.js`
- `frontend/tests/us1/runtime-007.test.ts`
- `frontend/tests/us2/runtime-media-007.test.ts`
- `scripts/start-station01-virtual-loop.ps1`
- `scripts/verify-q01-q02-test-page.ps1`
- `scripts/verify-q03-two-slot-virtual.ps1`
- `specs/008-recipe-driven-inspection/evidence/implementation-batch-20260926.md`
- `specs/008-recipe-driven-inspection/evidence/index.md`
- `specs/008-recipe-driven-inspection/fixtures/cases.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture-q01-param.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture-q02.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture-q03-ng.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture-q03-pending.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture-q03-two-slot.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture-q03.json`
- `specs/008-recipe-driven-inspection/fixtures/fixture.json`
- `specs/008-recipe-driven-inspection/fixtures/generate-q01-q02-test-mapping.py`
- `specs/008-recipe-driven-inspection/fixtures/generate-q03-test.py`
- `specs/008-recipe-driven-inspection/fixtures/media-manifest-q01-param.json`
- `specs/008-recipe-driven-inspection/fixtures/media-manifest-q03.json`
- `specs/008-recipe-driven-inspection/fixtures/recipes-q01-param.json`
- `specs/008-recipe-driven-inspection/fixtures/recipes-q02.json`
- `specs/008-recipe-driven-inspection/fixtures/recipes-q03-two-slot.json`
- `specs/008-recipe-driven-inspection/fixtures/recipes-q03.json`
- `specs/008-recipe-driven-inspection/fixtures/recipes.json`
- `specs/008-recipe-driven-inspection/fixtures/worker-manifest-q01-param.json`
- `specs/008-recipe-driven-inspection/fixtures/worker-manifest-q02.json`
- `specs/008-recipe-driven-inspection/fixtures/worker-manifest-q03-ng.json`
- `specs/008-recipe-driven-inspection/fixtures/worker-manifest-q03-pending.json`
- `specs/008-recipe-driven-inspection/fixtures/worker-manifest-q03.json`
- `specs/008-recipe-driven-inspection/fixtures/worker-manifest.json`
- `VirtualPlc/PlcAddressMap.cs`
- `VirtualPlc/VirtualPlcEngine.cs`
