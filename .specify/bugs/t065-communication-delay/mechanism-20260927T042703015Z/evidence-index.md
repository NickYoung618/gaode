# 本轮证据索引

UTC 2026-09-27T05:07:42.335482+00:00

- [机制/验收补充评估](assessment-supplement.md)、[修复](fix.md)、[验证](test.md)、[原条件对账](closure-audit.md)、[机器证明](verification-proof.json)。
- [摘要索引](evidence-index.json)、[正式输入](formal-inputs.json)、[源前后差异](formal-source.diff)、[三项勾选核验](task-checkbox-changes.json)。
- [基线故障事务](baseline-critical-transactions.json)、[候选冷启动事务](candidate-cold-transactions.json)、analysis/、analysis-baseline/、analysis-candidate/。
- 原ETL/流式gzip在workspace-path.txt所记C目录/evidence下；各root完整window、stop、lost、parse、cleanup原记录保留，索引列SHA，不倒改失败。
- [原期限安全TRX](safety-results/t065-native-safety.trx)、原首次编译失败safety-build-failed.log、工具/probe及构建日志。
- 正式Q01：`E:\dzk\gaode-1\artifacts\recipe-execution-008\t065-formal-q01-20260927T050046932Z`，run6333b690-ca2f-4d10-83bf-bce13b8db8fd，18/18；SQLite只读核验sqlite-readback.json，原DB/WAL/媒体/截图保留。
- 原assessment、根fix/test、identity/localize、工具修复、r22失败及恢复Passed维持各原结论。
