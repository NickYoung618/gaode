# Bug Verification: 正常暂停进入 Paused 并继续原 run

- **Slug**: 008-pause-continuation（当前修复上下文）
- **Tested**: 2026-09-27
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

缺失的 Paused/Continue 路径已在真实 Q01 API 链及正式 WPF 链验证：当前 3D 动作完成保存/复位后暂停，经检查继续原 run 至 Final，未重采初始3D/F。夹紧在途暂停的原症状由实际步骤组件检查及公共 API 暂停前缀验证；不把被范围拒绝的旧全AB路线当成完整通过。

## Checks Performed

| Check | Command / Action | Result | Notes |
|---|---|---|---|
| 原夹紧等待症状的自动等价验证 | 在途夹紧测试请求暂停后继续反馈/可靠保存/清除动作；PublicContractSmoke 等待 Paused 后检查受理 | pass | 旧全AB fixture 的后续完整路线失败保留，原因 RecipeRestricted:FourFaceBusinessScopeExcluded |
| 必要直接回归 | r5/tests/final-source-guards.trx | pass 5/5 | 实际源最后修正后运行，含暂停边界、夹紧、3D和局部运动期限 |
| 真实 API 整链 | r5/tests/pause-q01-real-chain-r2.trx | pass 1/1 | currentRecipe Q01，VirtualModbus/独立Python/SQLite，非WPF |
| 正式 WPF 整链 | r5/queue/job-001.json，Q01-PAUSE | pass | 同run Final、初始3D/F各一次；exitCode0、cleanupVerified=true |
| 源码/构建 | 默认Host与Release桌面构建；PowerShell/JS/Python解析 | pass | 构建0警告0错误，摘要见r5/build-freeze.json |

## Output Excerpts

`runId=2b7754cf-b19e-44b4-8c44-d76d8744fae2`；`normal_pause_check_continue_same_run=true`；`one_initial_3d_f_no_rescan=true`；`same_run_actual_page_final=true`。正式包 `runs/job-001-Q01-PAUSE/Q01-PAUSE/operation-route-validation.json` 全部19项true。

## Residual Risks

正式暂停注入通过已授权辅助控制API，页面为实际WPF，未新增页面暂停控件。人工换面与故障新轮分别独立取证；本报告不替代008父任务全部验收或生产设备验证。

## Recommendation

正常暂停状态不可达及在途夹紧跳过观察缺陷已验证修复。继续当前008代表路线和完整原条件对账，不因本缺陷通过提前关闭父任务。
