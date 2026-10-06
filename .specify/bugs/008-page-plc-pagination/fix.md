# Bug Fix: PLC 变化按冻结游标完整导出

- **Slug**: 008-page-plc-pagination（从当前评估上下文解析）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

首次请求冻结 latestSequence，继续 after 游标到该目标；拒绝 gap、序号不连续与游标停滞。单请求仍十秒，心跳不会无限延长导出。

## Changes

| File | Change | Notes |
|---|---|---|
| `scripts/verify-q01-q02-test-page.ps1` | modified | Get-CompletePlcChanges 完整导出及页数/末序号 |
| `scripts/validate-008-operation-evidence.py` | modified | 新包完整变化证据核验 |

## Tests Added or Updated

受控工具验证使用实际脚本 AST：1024→2048→2406，最新值继续到2440时仍止于冻结2406；空游标有限失败。

## Local Verification

r4/tool-fixes-probe-result.json 对分页及 PID 身份清理受控检查全部通过；r5 PowerShell 解析通过。原 GROUP-A-E 失败及其1024第一页、2406总序号和根因复核保留，不改 Passed。

## Deviations from Assessment

无模拟器接口或容量修改。进程身份清理另属当前无人值守必要修复；该受控验证包同时记录两个检查，但不把它作为正式 WPF。

## Follow-ups

以新 run 正式重验 GROUP-A-E，完整真实翻面 ACK 信号和场景读回通过后写 test.md。
