# Bug Assessment: B02 环境提前返回漏检

- **Slug**: 010-b02-early-return（用户显式指定）
- **Created**: 2026-10-03
- **Source**: pasted text
- **Verdict**: valid
- **Severity**: high（持续架构门禁可放行环境控制工序；不是已证明产品业务执行存在该缺陷）

## Report

用户报告：共同业务中 `if (purpose == "Production") return; detection.ExecuteAsync();` 可能不报告B02。要求直接写法及既有bool helper/别名传播覆盖；明确拒绝、来源记录与正常业务返回保持合法。新增行必须进入L，实际证明当前L及最终漏跑拒绝，不启动设备或完整主链。

## Symptom

当前B02仅在环境if的后代调用含Progress时拒绝。上述调用在if之后，分支仅含return，条件成立会正常结束而跳过工序，但语法扫描不报告B02。此为源码证据判断；评估阶段尚未执行复现。

## Reproduction

1. 在同一检查器样本测试中添加直接return和bool helper/别名return两行；先断言编译零错误和ExecuteAsync实际符号绑定。
2. 原检查器执行该两行，预期B02断言失败；保留原检查器摘要、TRX和绑定输出，排除样本/解析问题。
3. 修复后通过同一生产Check证明B02及源条件有效定位；新增合法拒绝/来源记录/业务返回正例。

## Suspected Code Paths

- backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryChecker.cs：Check中if只查branch.DescendantNodes的Progress，现有environmentMethods/environmentValues已支持bool helper和标量别名。
- backend/tests/Gaode.Rules.Tests/Architecture/RecipeExecutionBoundaryTests.cs：N02/N05b只覆盖条件内部Proceed调用；CheckCore已核编译错误但未记录本问题特定调用绑定。
- scripts/workflow/010-lightweight-cases.json：目前64条，固定方法/dataRow身份；新增反例须进入该集合。
- scripts/workflow/recipe_execution_010.py：run_lightweight、validate_bundle、final_gate为既有正式执行和最终判定；无需修改判定器。

## Root Cause Hypothesis

置信度高：B02缺乏对条件两个后继到正常结束/后续必要调用的有限控制流比较，因而遗漏否定式提前返回。是否真实运行复现留给紧接的bug-fix，不将推断写成已执行。

## Proposed Remediation

**Preferred**: 复用现环境传递及Progress识别，在本方法Roslyn CFG中检查环境条件是否存在未执行工序而正常结束的路径，同时另一后继可达必要工序。明确抛错及既有明确拒绝结果不当正常成功；正常业务条件不误伤。限定相关方法和已有helper闭包，不引入分析平台。

**Files likely to change**:
- RecipeExecutionBoundaryChecker.cs、RecipeExecutionBoundaryTests.cs；必要相邻测试辅助代码。
- scripts/workflow/010-lightweight-cases.json；仅确有必要的直接完整性样例/消费者。
- specs/010-recipe-execution-isolation/contracts/verification.md 的B02及N/P样例。
- specs/010-recipe-execution-isolation/tasks.md 追加最少修补任务/当前状态，不改原48项。
- specs/010-recipe-execution-isolation/quickstart.md 修正过时状态。
- 本Bug fix.md/test.md及独立证据。

**Tests to add or update**: 直接提前return、helper+别名提前return；带后续工序的明确throw/既有拒绝结果、来源记录和正常业务return。全部使用同一检查实现；完整L原必需项保留。通过已有final_gate对本轮完整L的隔离故障副本验证新增行missing/notExecuted拒绝，不修改原凭证。

## Risks & Considerations

不得禁止全部return或Purpose/Source；不能按样本路径/Role/名称特判。产品业务、独立预期与原B/E/S/冻结只读；baseline.json记录本轮读取前摘要。旧全局冻结改变后不再声称匹配当前版本。只运行检查器及L，不启动Host/PLC/Worker/数据库。原失败及被拒删除的临时目录保持不变；hooks={}无后续自动阶段。

## Open Questions

无影响当前授权范围的业务未决问题；实际复现和合法拒绝结果的CFG表示由修复阶段核实。
