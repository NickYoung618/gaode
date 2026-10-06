# Bug Assessment: 旋转API动作数组被额外包装

- **Slug**: 008-special-actions-export
- **Created**: 2026-09-27
- **Verdict**: valid
- **Severity**: high（当前旋转正式验收阻塞）

## Reproduction / Root Cause

night-r5-resume-1 job009 ROT-PART-OK actual WPF pageOutcome FinalPageDisplayed/pageExit0，run3981e5e8-36c3-4eee-a0c8-63e63e9921be；outerexit1/cleanuptrue。validate-008-operation-evidence.py:179收到list而非动作object，未生成operation验证。原page-api-device-facts.specialActions实际为嵌套数组，verify-q01-q02-test-page.ps1:274 的 @(Invoke-RestMethod ...) 对JSON数组返回值再包装一层。实际旋转API原合同返回动作对象数组，不是新业务或设备错误。

## Proposed Remediation / Files / Tests

只将采证赋值改为直接接收 Invoke-RestMethod 返回数组；不加Python兼容flatten，不改API、动作及验收条件。用实际PowerShell数组输出和真实赋值AST验证原包装/修正导出，保留原job009失败与原JSON。新增独立job022 ROT-PART-OK新run复验；当前worker10536存活，后续新runner加载子脚本，不需重载worker或改业务构建。相关报告在原008证据目录。

## Risks / Questions

只在已退出且cleanuptrue后读SQLite；原失败不覆盖为通过。正常/其他路线不受本一行修改影响，继续原冻结批次。无新业务决定。
