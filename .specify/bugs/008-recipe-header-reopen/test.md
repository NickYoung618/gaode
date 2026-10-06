# Bug Verification: 实际绑定配方标题刷新与重开恢复

- **Slug**: 008-recipe-header-reopen（当前修复上下文）
- **Tested**: 2026-09-27
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

正式 Q01 WPF 的相同刷新/登录重开流程验证既有标题与版本从实际绑定恢复；顶部不再丢成未选用配方。原 GROUP-F-PENDING 失效截图保留，未改写。

## Checks Performed

| Check | Command / Action | Result | Notes |
|---|---|---|---|
| 原页面路径自动等价复现 | 正式 Q01-PAUSE Final 后刷新，再登录重开 | pass | `authoritative_recipe_header_refresh_reopen=true`，三个时点与同run recipeId/version一致 |
| 新测试与直接回归 | `node --test frontend/tests/us1/runtime-007.test.ts` | pass 21/21 | 执行身份优先、仅选用身份恢复；既有页面运行/权限/恢复回归 |
| 实际加载资源 | r5/preparation-validation.json 与正式包资源摘要 | pass | 源/dist/WPF frontend/dist 同 SHA256；客户原型只读未改 |
| 作业退出/资源释放 | r5/queue/job-001.result.json | pass | exitCode0、cleanupVerified=true |

## Output Excerpts

Q01 `R008-Q01 / 1.1.1-test`，runId `2b7754cf-b19e-44b4-8c44-d76d8744fae2`；正式包 `runs/job-001-Q01-PAUSE/Q01-PAUSE/recipe-webview2-page-evidence.json` 记录刷新与重开时标题和版本；operation-route-validation全部19项true。

## Residual Risks

原 GROUP-F-PENDING 是修复前证据；当前报告用同一 runtime、控件和刷新/重开路径的 Q01 自动等价复验，后续成组代表仍单独对账。不将本UI修复视为业务结果、算法或生产范围全部通过。

## Recommendation

关闭标题恢复缺陷；持续完成当前批次其他独立验收。
