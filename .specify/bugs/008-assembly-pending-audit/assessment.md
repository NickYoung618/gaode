# Bug Assessment: 整体Pending被审计脚本误判期望NG

- **Slug**: 008-assembly-pending-audit（当前实际失败包）
- **Created**: 2026-09-27
- **Verdict**: valid
- **Severity**: high（阻断本轮自动验收调度）

## Report and Reproduction

r5 job-006 ASSEMBLY-A-E-PENDING业务operation-route-validation通过、exitCode0、cleanupVerified=true，但scene-acceptance-audit仅whole_disposition_and_pending_detail=false，调度已保守暂停。实际同run `4788a57c-9d3f-4d4e-94cc-4e4e7cb5690b` 的Assembly与BASE第二面均Pending、PIN及其他面OK，整体一次搬往Pending区，故不能归因为业务误判。

## Root Cause

`scripts/audit-008-night-page-route.py` 在NG/PENDING两种已明确整体case中用 `case.endswith("NG")` 决定期望值；`PENDING` 也以 `NG` 结尾，所以错误期望NG。这是实际脚本缺陷，不改变质量或处置规则。

## Proposed Remediation

仅按完整末段token区分NG与PENDING；保留其余场景与实际明细检查。原失败JSON不覆盖、不重验同run改成Passed；新的独立job/new run正式复验Pending。增加最小工具层受控检查，使用实际脚本AST的该判断分支对真实支持的两种输入核对。当前无在途WPF作业，worker9476保持存活；审计由独立Python调用，每次加载新源，无需重启worker或改冻结业务程序集。记录该工具的原/新摘要，旧冻结清单和失败包保留。

## Files likely to change

- `scripts/audit-008-night-page-route.py` 单条件。
- 本批冻结补充、调度恢复和原证据记录。

## Risks and Open Questions

无新业务决定，不改算法或期望Pending。先继续其他不受影响代表，Pending重验使用新job；旧failed审计不可改写为Passed。
