# Bug Fix: 整体质量期望按完整token判断

- **Slug**: 008-assembly-pending-audit（当前评估上下文）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

用完整末段NG/PENDING判断已支持整体case，避免PENDING字符串也匹配NG后缀；保留实际Pending明细等其他条件。

## Changes

| File | Change | Notes |
|---|---|---|
| `scripts/audit-008-night-page-route.py` | 单条件修改 | 仅工具，业务构建/fixture/worker脚本未改 |
| r5/source-freeze-amendment-001.json | 新增工具摘要补充 | 原冻结文件清单及失败审计保留 |
| r5/review-and-dispatch-r3.ps1 | 恢复本批复核 | 先继续无关代表，最后新job-020重验Pending；原job-006失败不覆盖 |

## Tests Added or Updated

实际脚本AST的该判断表达式，NG/Pending正确输入各通过，错误OK输入各拒绝；r5/scene-audit-suffix-probe.json四项true。此探针不操作SQL/WPF，不抵正式复验。

## Local Verification

Python解析及实际AST检查通过；当前有效worker9476/Session2保持存活。审计由每次独立Python加载，无需重启worker。job-006退出0/cleanuptrue但场景失败仍保留，独立new run job-020已安排。

## Deviations from Assessment

无业务变化。恢复调度采用本批独立r3脚本，不覆盖已退出r2的失败日志和状态文件。

## Follow-ups

job-020正式Pending复验通过后写test.md；不将原同run失败改成Passed。
