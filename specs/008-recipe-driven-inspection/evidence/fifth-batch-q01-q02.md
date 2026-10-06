# 第五批：Q01、Q02 正式 WPF 虚拟 Test 整链

日期：2026-09-25。两个运行按 Q01、Q02 顺序，在隔离 Test 根和端口中，由 Administrator Session 2 的正式 WPF/WebView2 页面通过鼠标/键盘选配方、启动及各点击一次取盘确认。来源仅为 Test/VirtualPlc、模拟相机、独立 Python worker；不代表生产坐标/高度标定、真实相机 SDK 或识别精度验收。两个运行均经公共准备、实际 3D 高度分析、F 解码绑定、产品命令 2、逐图采集/分析/保存/复位、同面双输入融合、下料命令 4、解锁、页面取盘确认，最终同 run 的 API/SQLite 与页面为 Final。

| 项目 | Q01 | Q02 |
| --- | --- | --- |
| 运行包 | [Q01](../../../artifacts/recipe-execution-008/fifth-batch/run-20260925-041141/Q01/) | [Q02](../../../artifacts/recipe-execution-008/fifth-batch/run-20260925-042012-q02/Q02/) |
| runId | `bed5e53a-cc74-410d-93d7-cc0a9d99b278` | `56f2048e-8c6e-4430-a91e-4bdd560110bd` |
| 配方/版本/摘要 | `R008-Q01/1.1.1-test` / `2391FD64ECBBE7558CAFAB9A30909DC2448EA29705B48676DB3A975A9B30417D` | `R008-Q02/1.1.1-test` / `3B4B705CF3A6653F35F3098CBDA22C6C74F4451172C68AFD90F28D2FC76F0967` |
| 高度和运动 Z | `sample-a=10.13 mm`，A/B:P01 均 `110.13 mm` | `sample-a=10.61 mm`→C/D:P01 `110.61 mm`；`sample-b=11.144 mm`→C/D:P03 `111.144 mm` |
| 检测顺序 | A:P01→B:P01 | C:P01→C:P03→D:P01→D:P03 |
| 检测复位/融合/媒体读回 | 2/1/4 | 4/2/6 |
| 独立 worker 检测结果 | 单图 2 + 融合 1 | 单图 4 + 融合 2 |
| 页面/已提交状态 | `完成` / `FinalUnloadCompletion` | `完成` / `FinalUnloadCompletion` |

两次使用相同Host DLL SHA-256 `258591629D801B5F4129E199C201ACE1186A8B27E1754F1A1E6BD0D9258AC4D5`、VirtualPlc DLL SHA-256 `B9619E74B940DC67ED9AB254F005F28BB46889718093AE317B16F878E2EBC3D9`；前端运行时源码摘要 `5FEDD162AF5682DC13F494081C6824D8FA593253AB65DC2DA7123490958DC74E`。配方摘要不同且各自与所选、F绑定、冻结及保存引用核对一致。

每包 `verified-facts.json` 为只读脚本 `scripts/summarize-q01-q02-evidence.py` 对页面、API、SQLite、PLC 变化、worker 回执及媒体文件的交叉核对；`recipe-webview2-page-evidence.json` 含正式页面点击和截图记录；`page-api-device-facts.json` 含提交事实与设备摘要；`process.json` 含 Host/VirtualPlc 构建 SHA-256；`station01.test.db`、`media-root/media/`、`media-root/worker-protocol.jsonl` 和 `logs/plc.out.log` 均在同一包内。Q01采证器旧版的文字终止判断把页面已完成误记为 `DeadlineExceeded`；独立的 `final-page-readonly.json/.png`、页面末状态、API和SQLite均证实该 run 已 Final。Q02采证器已修正，结果为 `FinalPageDisplayed`。不删除 Q01 工具误报记录。

相同程序构建下，Q01与Q02的 `sample-a` 实测值从 `10.13` 变成 `10.61 mm`，P01 目标 Z 随之从 `110.13` 变成 `110.61 mm`；Q02 的 P03 使用其匹配 `sample-b` 而非首样本。Test 公式、偏置、范围、scope/对象/槽位/轮次和版本见[虚拟映射合同](../contracts/test-virtual-mapping.md)。映射只对本批版本化 Test 配方开放；生产目录仍 Restricted。

缺失/错关联高度由 `PublicPreparationTargetResolutionTests.TestTargetsUseMatchingCurrentThreeDHeightAndRejectWrongSample` 验证在产品运动前拒绝。到位不符、必要保存失败、复位失败的组件证据见[第四批记录](fourth-batch-validation.md)；其版本和组件范围不能直接当作本批完整前端失败用例。历史本批失败包（页面就绪、Modbus超时、源点 ID 映射错误）均保留在 `artifacts/recipe-execution-008/fifth-batch/`；修正过程不能删除或改写其结论。成功两包未出现 `PLC failure latched`，但 003 T065 所需心跳延迟机制前后对照尚未全部满足，不能由两次正常运行宣称缺陷彻底关闭。

本批只判 Q01、Q02 的 **Test 虚拟端到端正常路线通过**。原任务 008 T055 另要求同版本 F 冲突、产品到位/复位及必要保存失败与全部前置任务验收，尚未据此勾选；008 T056 的完整 C01 差异及任务前置也另判。Q03—Q22、C03—C07及实际 NG/Pending 分拣、恢复、真实标定/SDK均未通过。

当前源码定向复核：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter 'FullyQualifiedName~PublicPreparationTargetResolutionTests|FullyQualifiedName~RecipeCatalogTests|FullyQualifiedName~RecipeExecutionCoordinatorTests|FullyQualifiedName~SingleFaceDetectionIntegrationTests'`，15/15通过、0警告/0错误，涵盖缺失/错关联高度、目录门禁、AB/CD实际组件和错到位/媒体必要保存/复位失败停止。该组件测试不替代正式页面失败分支。同次`Gaode.Rules.Tests`过滤命令只编译且未发现匹配测试，不计15项；适用测试类实际位于`Gaode.Contracts.Tests`。
