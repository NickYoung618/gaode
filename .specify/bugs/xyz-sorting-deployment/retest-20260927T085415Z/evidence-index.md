# 本轮证据索引

- [补充评估](assessment-supplement.md)、[实际修复](fix.md)、[验证报告](test.md)、[完整摘要/运行关联](verification-proof.json)。
- 原故障：mixed-html-js-reproduction.json、baseline.json、before/；来源Word只读提取protocol-paragraphs.txt。
- 长路径：long-path-proof.json、long-path-280-before/、long-path-280-after/；初次250字符准备long-path-before/和long-path-after/未删。
- 单元/组件：ui-followup/component-tests.txt（30通过），先前失败component-tests-final.txt及其修正记录保留。
- r3实跑：package-test-inputs.json、package-validation.json、Q01-result.json、Q01-NG-result.json、Q02-PENDING-P03-result.json。
- r4实跑与核查：ui-followup/package-test-inputs.json、Q01-NG-result.json、ng-existing-evidence-review.json、selector-proof.json、selector-dom.html、verification-notes.md。
- r4 Pending：ui-followup/verification-resume/Q02-PENDING-P03-result.json；ui-followup/final-resume/pending-cleanup-proof.json、pending-readback.json。
- r4 Q03：ui-followup/final-resume/Q03-result.json；stop-followup/q03-cleanup-proof.json、q03-readback.json。
- r5：stop-followup/package-test-inputs.json、package-difference.json、stop-verification.json、final-cleanup.json。每个运行原始WPF截图、DOM、SQLite、PLC原始审计/握手和完整XYZ文件绝对路径及摘要见verification-proof.json。
- 只读采证：collector-proof.json、collector-test.txt、collector-artifact.json；工具小包artifacts/Gaode-008-Collect-Diagnostics-20260927.zip。
- 所有专用任务请求/摘要/旧runner备份保留在各目录task-submission.json、request-before.json、runner-before.ps1。任务原始结果位于t065-communication-delay/task-<requestId>/。失败任务不改为成功；独立复核通过不覆盖历史。
- 最终补充位核对：xyz-float32-bit-review.json，29个实际动作/87轴值的原始写入与反馈Float32位模式一致，无业务容差修改。
