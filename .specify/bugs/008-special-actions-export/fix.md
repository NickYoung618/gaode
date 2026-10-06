# Bug Fix: 保持旋转动作API数组结构

- **Slug**: 008-special-actions-export（从实际失败上下文解析）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Changes

verify-q01-q02-test-page.ps1 单行直接将 Invoke-RestMethod 的API动作数组写入specialActions，去掉额外 @()。API、业务动作、Python验证条件与原失败JSON均不改。后续每个runner会加载当前子脚本；存活worker10536本体与脚本摘要未变，不要求重启。

## Tests / Local Verification

special-actions-export-probe.json：用原包四个真实Completed回执作为未改变输入、PowerShell实际不枚举数组返回及实际赋值AST，证明原结构嵌套、新结构为四个object且状态不丢，PS语法通过，4/4。原job009保持outerexit1，不将前端Final替代完整验收。

新job022与独立run复验ROT-PART-OK，原job010—021不受影响继续；新复核调度r2只控制本root。原dispatcher及失败状态另存，不覆写失败。原business build/fixture不变，追加源冻结差异记录当前必要工具修复。

## Deviations / Follow-ups

无偏离。正式复验与后续NG/Pending/整体出口结束后再写test.md；未知占用不Final的原门禁仍需按构建范围对账。
