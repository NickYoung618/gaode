# Bug Fix: 复位响应与实际就绪观察同步

- Slug: 008-reset-ready-observation（沿当前评估上下文）
- Fixed: 2026-09-27
- Assessment: ./assessment.md
- Status: applied

LatestProtocolPlcDevice.ResetAsync的原轮询读取一次observed，并将实际缓存PlcReady加入既有返回条件；未变Modbus Ready/Auto/无Fault、Connected/SafetyClear、PollMs、期限或运动门禁。不填假状态。

独立旧fixture问题：VirtualManualCompletionGateTests改用现行合法Q01 Test目录useCurrentRecipe:true，全部授权/提前取盘/旧revision/失败提交/幂等及真实SQLite断言保持；不改125秒上限，不允许缺分拣目标放行。已在评估中记录，非生产软件缺陷。

r19首次18合同测试通过；7项集成4通过3失败保留。新独立r21-reset-0927仅重跑三条失败用例并冻结当前Host。新程序正式恢复/当前Q18待验证，尚未声明该修复验收成功。

2026-09-27 后续验证已完成，以上为修复当时状态。r21三条原失败用例全通过，r22当前Q18及job002完整恢复整包通过；详见test.md。该结论仅关闭缓存Ready同步缺陷，不关闭r22 job001独立HTTP超期或003 T065延迟机制条件。
