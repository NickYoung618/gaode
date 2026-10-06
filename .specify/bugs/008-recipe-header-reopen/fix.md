# Bug Fix: 配方标题从后端运行恢复

- **Slug**: 008-recipe-header-reopen（从当前评估上下文解析）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

render 优先消费实际 recipeExecution，其次 recipeSelection；现有目录匹配身份提供名称，缺名称时显示实际 recipeId/version。

## Changes

| File | Change | Notes |
|---|---|---|
| `frontend/src/runtime.js` | modified | 恢复既有标题和版本，不增加客户端业务状态 |
| `frontend/tests/us1/runtime-007.test.ts` | updated | 已绑定与仅选用投影；执行身份优先 |
| `scripts/capture-station01-webview2-normal.cjs` | modified | 采集刷新、重开标题/版本 |
| `scripts/validate-008-operation-evidence.py` | modified | 新包标题与同 run 后端身份核验 |

## Tests Added or Updated

两项配方标题恢复测试，复用现有运行投影与 DOM 桩。

## Local Verification

`node --test frontend/tests/us1/runtime-007.test.ts` 21/21；源、dist、WPF frontend/dist runtime 摘要一致，见 r5/preparation-validation.json。原型归档摘要不变。正式页面刷新/重开复验另行登记。

## Deviations from Assessment

无。

## Follow-ups

新正式 WPF 包确认标题/版本与绑定一致后写 test.md；旧失效截图保留。
