# 第三批定向验证（2026-09-25）

范围是代码构建、合同/组件验证和Test素材读取；**没有运行Q01/Q02正式业务链、没有WPF点击、没有真机或精度验证**。所有独立worker和VirtualPlc仅由对应测试在本机临时启动并结束。

| 验证 | 执行命令/结果 | 可证明范围 |
| --- | --- | --- |
| Host构建 | `dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --no-restore -v:q`，成功，0警告/0错误 | 新接口及应用/基础设施代码可编译 |
| 冻结门禁/预算/面配对/旧期限合同 | `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter 'FullyQualifiedName~FaceResultAggregatorTests|FullyQualifiedName~RecipeExecutionCoordinatorTests|FullyQualifiedName~StageRetryPolicyTests'`，11/11；另严格Pending与旧三阶段组合18/18 | 静态顺序、身份、预算与旧合同；无产品目标派发 |
| PLC产品握手组件 | `dotnet test ...Gaode.Contracts.Tests.csproj --filter 'FullyQualifiedName~FormalHostPortKeepsPreviousArrivalUntilNextAcceptedMoveAndBindsAfterF'`，2/2 | 注入Test XYZ下Host/VirtualPlc命令2和1/2复位；不批准Q01点位 |
| A/B模拟媒体 | `dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --filter FullyQualifiedName~Q01CameraMediaTests`，1/1，A/B各实际等待4秒并读取不同PNG | 相机身份选择及字节摘要，不含PLC定位 |
| 独立worker | `dotnet test ...Gaode.Integration.Tests.csproj --filter FullyQualifiedName~IndependentWorkerReadsEveryInputAndReleasesEveryLease`，2/2，单/双输入实际Python进程各等待10秒且释放租约 | worker协议和各媒体读取，不含Q01整链或融合SQLite读回 |
| 前端运行时代码 | `npm test`于`frontend/`，35/35；`npm run build`成功；`node --check scripts/capture-station01-webview2-normal.cjs`通过 | 控件准入、请求版本及构建；不等于WPF操作 |
| Q01 Test准备 | `pwsh -File scripts/simulate-station01-load.ps1 -PrepareOnly -FixtureManifest .../fixtures/fixture.json -OutputDirectory .../third-batch-prepared`，退出0；产物[load文件](../../../artifacts/recipe-execution-008/third-batch-prepared/load-s01-007-3b4624f53363464598793132837cc00a.json) | 只生成2.0准备请求，无POST/receipt |
| Q02 CD受限数据 | `dotnet test ...Gaode.Contracts.Tests.csproj --filter FullyQualifiedName~Q02RestrictedCatalogKeepsCdBatchOrderAndNoncontiguousSlots`，1/1；Q02配方SHA、Fixture及case摘要均为`DD9E2470B601BAA8700663F2E974B8D37934C4719F38F36C482FDFB008FF3BB8`；`PrepareOnly`退出0，产物[load文件](../../../artifacts/recipe-execution-008/third-batch-q02-prepared/load-s01-007-64bdc46c7a5348818c4994ae7a4d37e1.json) | 仅证明C批次后D批次及P01/P03计划、数据/媒体清单可装载；Restricted，未派发运动或运行业务 |

`quser`两次均显示administrator会话2为`Disc`；003 T065页面心跳和006/007页面点击保持Blocked。Q01目录仍`Restricted/ProductPointMappingMissing`；高度映射按本批暂缓，不以固定Z代替。所有受影响任务仍未勾选。
