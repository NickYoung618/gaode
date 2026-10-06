# Bug Fix: B02 环境提前返回漏检

- **Slug**: 010-b02-early-return（显式用户参数）
- **Fixed**: 2026-10-03
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

同一正式RecipeExecutionBoundaryChecker保留原条件内部调用规则，补充相关方法Roslyn CFG后继比较：环境条件的一侧正常结束、另一侧可达必要工序时，报告B02并定位完整环境条件。既有bool helper/标量别名传播继续复用；无新平台、跳过开关或样例身份特判。

## Changes

| 文件 | 改动 | 边界 |
| --- | --- | --- |
| backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryChecker.cs | 版本010-boundary/2；有限CFG补足早返；识别既有明确拒绝构造合同 | 仅检查器，不改产品业务/009规则 |
| 同目录RecipeExecutionBoundaryTests.cs | 两条新负例、四条正例；反例先核编译及方法符号；核B02及条件精确行列 | 与正式源码共用Check |
| scripts/workflow/010-lightweight-cases.json | 保留原64行，追加六条精确sample/dataRow，总70 | 原生产runner自动消费；不改消费者或判定策略 |
| specs/010-recipe-execution-isolation/contracts/verification.md | B02早返语义及六条样例/L身份说明 | FR/AC/SC不变 |
| 同目录quickstart.md | 修正尚未实现/plan状态，区分原动态通过与当前修补 | 不改旧报告/冻结 |
| 同目录tasks.md | 当前状态及追加T049 | 原48编号/勾选/实施记录保留 |
| .specify/bugs/010-b02-early-return/ | 三阶段记录、源摘要、复现/验证脚本及独立证据 | 脚本只调用既有生产执行/判定函数；不是新判定器 |

## Tests Added or Updated

- EnvironmentalEarlyReturnIsRejected：N02-return-direct；N02-return-helper-alias。两行编译无错误、ExecuteAsync/RequiresApproval符号实际绑定，断言B02在环境if条件精确路径/行/列。
- LegitimateEarlyReturnAndOriginUseRemainAllowed：P-return-throw；P-return-rejected-result（引用真实ThreeDAndFRecipeGateDecision类型的明确拒绝构造）；P-return-origin-log；P-return-business。
- 原N01—N05/P01—P03、当前职责闭包和009四条适用静态保护保留，纳入本轮L。

## Local Verification

1. 修复前：`dotnet test ... --filter FullyQualifiedName=...EnvironmentalEarlyReturnIsRejected`，exit=1；实际2失败/0通过/0Skip。两行StdOut均compilationErrors=0且绑定IDetectionPort.ExecuteAsync和bool helper，rules为空；失败明确来自Expected B02断言。见pre-fix.log、pre-fix/pre-fix.trx、pre-fix-reproduction.json及original-checker.cs.txt；不是编译/解析/配置错误。
2. 修复后开发验证：新增两个方法定向执行，exit=0；6通过/0失败/0Skip，见post-fix-dev-01.log及TRX。之后仅补了定位输出、正式清单和状态合同，当前最终源码/构建/清单由生产L重新验证。
3. 正式完整L通过`run-current-l.py`调用既有run_lightweight（scope=B02LightweightOnly，不调用run_profile或动态链）。本修复记录写入时该轮仍在执行，最终实际计数及当前凭证以current-l.json/后续test.md为准，不能把开发6项当完整L通过。

## Deviations from Assessment

无实质偏离。不需要修改L判定器或清单消费者：生产入口按manifest精确方法/数据行执行，原final_gate/validate_bundle已提供漏跑拒绝。明确拒绝仅依据既有类型/构造标志，不将任意返回对象/名为Rejected的调用视作拒绝。额外Added拒绝合同不是新增业务语义。

## Follow-ups

自动接续已授权bug-test：复核本輪完整L的当前原始凭证，复用同源码/构建/清单下已执行检查；对其明确隔离故障副本移除/标记未执行N02-return-direct，通过原final_gate核拒绝，即使假汇总Passed。核产品源码/独立预期/原B/E/S/冻结只读摘要后，才标verified/T049完成。

旧动态证据保留；修改后全套聚合未重新执行。原010证据对应standalone-54fa1c4e72904772bd9c91cb27bc7d47-verify-1及2026-10-02T13:40:11.084839+00:00冻结，不把旧全局摘要改写为当前匹配。未启动Host/PLC/Worker/数据库、不重跑B/E/S/T、全量或整机；被拒删除的旧临时目录不触碰。

## 当前验证收口补记（2026-10-03）

上述执行中的生产L随后完成：E:/dzk/gaode-1/artifacts/recipe-execution-010/bug-010-b02-early-return-76d2915a436f47c7846b7e0b1ad4898a/L/ledger.json，70/70通过，原27项dotnet/7脚本/36完整性数据行实际完成，无Skip。bug-test只重读同轮凭证，并通过原final_gate证明新增行missing及NotExecuted均拒绝，见test.md及verification.json；未修改被测源码。产品228文件及旧证据1182文件摘要均不变。该补记保留上文阶段时点记录，不将原010动态结论重盖为当前通过。
