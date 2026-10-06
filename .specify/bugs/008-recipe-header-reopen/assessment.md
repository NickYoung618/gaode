# Bug Assessment: 页面重开后当前配方标题丢失

- **Slug**: 008-recipe-header-reopen（无人值守自动生成）
- **Created**: 2026-09-26
- **Source**: 正式WPF实际截图与已退出同run API事实
- **Verdict**: valid
- **Severity**: medium

## Report

用户要求页面选用/F绑定/冻结/保存一致，刷新与重开正常。`page-next-closure-20260926-night-r4/runs/job-002-GROUP-F-PENDING/GROUP-F-PENDING/recipe-04-final.png` 显示Pending及Final已提交，但顶部“当前配方”变成“未选用配方”。同包API的recipeState=Bound，recipeSelection与recipeExecution仍返回已提交配方身份。原验收脚本只比较刷新/重开判定，没有检查配方标题。

## Symptom

页面重新加载或从登录页重开后，结果和原run恢复，但当前配方标题/版本没有从后端运行投影恢复。不能将该标题与流程绑定丢失混为一谈：真实F/冻结/保存链仍通过。

## Reproduction

1. 正式WPF从既有控件选GROUP-F配方并完整运行。
2. 正常刷新，再从登录页重开原页面。
3. 结果仍为Pending、Final仍提交，顶部却是未选用配方；该实际截图及API已保存，不需要重新制造失败。

## Suspected Code Paths

- `frontend/src/runtime.js`：仅在配方选择按钮处理中写selectedRecipeName/selectedRecipeVersion；render没有消费运行recipeSelection/recipeExecution。
- `scripts/capture-station01-webview2-normal.cjs`：刷新/重开只记录并核对verdict，遗漏配方身份显示。
- `frontend/tests/us1/runtime-007.test.ts`：复用既有API投影/DOM测试组织。

## Root Cause Hypothesis

置信度高。选择只保存于JS内存；重开仅保留非可信run引用，标题需要通过现有后端已提交投影绑定，而不是新增客户端业务状态或硬编码。

## Proposed Remediation

render用后端recipeExecution（已冻结执行）或recipeSelection（明确所选）的真实recipeId/version恢复既有标题/版本；现有目录可提供对应名称，缺目录名称时显示实际recipeId，不能生成虚构型号。保持原页面结构/布局/字段/交互，后端接口不变，不编辑原型。只有UI元数据绑定，不改启动门禁或算法结果。

在必要前端测试及后续最少正式页面中检查选用、刷新、重开三时点的配方ID/版本与同run事实一致。修改实际源runtime并同步构建输出及WPF副本；仅在冻结批次结束和资源释放后操作。旧截图/通过子能力及失败事实保留，不把当前判定刷新通过改写为全部页面状态通过。

## Risks & Considerations

- 有运行时以实际后端执行为准，不能从本地选择反推已绑定。
- 不改变客户确认原型；无需扩展006页面规格范围或共享API。
- 当前批次仍用原程序完成，后续构建/证据的适用范围单列。

## Open Questions

无新增业务输入。
