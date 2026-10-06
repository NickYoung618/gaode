# 第三批 Q02 共用批次准备（008 T056部分）

`fixtures/recipes-q02.json`、`fixture-q02.json`、`media-manifest-q02.json`及`worker-manifest-q02.json`提供独立的受限Test配方：F码`TEST-TRAY-0202`，非连续占用槽P01/P03，单面C/D相机组。两张媒体分别绑定C、D；配方、Fixture和case的目录摘要一致（`DD9E2470B601BAA8700663F2E974B8D37934C4719F38F36C482FDFB008FF3BB8`）。`cases.json`保留协议槽号、物理C/D点位和高度绑定为`null`，目录保持`Restricted/ProductPointMappingMissing`，不得派发产品运动。

`RecipeCatalogTests.Q02RestrictedCatalogKeepsCdBatchOrderAndNoncontiguousSlots` 1/1通过：冻结计划对P01/P03生成C:P01、C:P03、D:P01、D:P03的Capture顺序，工作量4次采集、2次面融合、6次worker调用，`BuildExecutable`拒绝受限配方。`PrepareOnly`只生成[请求文件](../../../artifacts/recipe-execution-008/third-batch-q02-prepared/load-s01-007-64bdc46c7a5348818c4994ae7a4d37e1.json)，未POST或启动设备。共用面配对代码支持C/D，但没有实际Q02单图/融合、SQLite读回、前端完整运行或Final证据。008 T056及T059均未勾。

## 第五批实际Q02

授权的Test映射版本`test-virtual-mapping/1.0.1`和配方`R008-Q02/1.1.1-test`代替上方受限旧版本；现由正式WPF完成C:P01→C:P03→D:P01→D:P03、四次产品复位、两次融合及同run Final。每个槽的3D sample与检测Z分别关联，空的P02无检测动作。逐项读回见[第五批证据](fifth-batch-q01-q02.md)。旧受限描述是第三批历史，不再是当前Q02状态。008 T056还要求完整C01及前置T055原验收，保持未勾。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
