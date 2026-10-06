# Bug Verification: T065采样工具范围

- **Slug**: t065-communication-delay（显式用户请求，与fix上下文一致）
- **Tested**: 2026-09-27T02:24:00Z
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial（整个通信缺陷）；**本轮采样工具：verified**

## Summary

实际保存与必要TCP/线程调度离线关联通过，正式入口绑定有效；工具范围已修复。没有运行业务观察/完整配方，没有重现或解决通信业务根因，T065/T055/T070仍未完成。

## Checks Performed

| 检查 | 命令/动作 | 结果 | 证据/限制 |
|---|---|---|---|
| 官方来源/部署 | 微软ADK签名bundle清单比对、MSI签名验证；固定PrepareWpt执行msiexec /a | pass | 官方包/core签名Valid；项目目录解包，未系统安装 |
| 真正start/stop | 固定ValidateWpt，Network filemode，独有T065Tool实例，短回环探针 | pass | start/stop均0、ETL非空；无设备 |
| 最初dumper | 原参数、纠正参数、提供者过滤 | fail（已保留） | 参数位置错误；纠正后.NET190v9仍0x80070032，无重复采样 |
| 必要离线读取 | 同版xperf tracestats；管理员固定ParseWpt用显式TraceRpt | pass | 同一ETL，分别0；1023269事件；有非目标解码限制 |
| TCP/PID/端点 | correlate-tool-probe-20260927.py | pass | 253窗口内端点事件、2 PID映射、TCB关联1228条 |
| 调度/线程归属 | CSwitch/ReadyThread实际字段；微软Thread v3原始载荷身份恢复 | pass | 585/196事件；4映射覆盖PID4528的3828/12000 |
| 时间/丢失/解析错误 | FILETIME锚点、tracestats/summary及ProcessingErrorData计数 | pass with limitations | lost events/buffers=0；+07:59校准；111/15005错误及恢复范围详见fix |
| 所属资源清理 | 固定InspectWpt，管理员新WPR status、logman、PID及监听核查 | pass | 命名/默认均未录制，无collector；PID不存在、监听空；未cancel/停止未知会话 |
| 证据缺失阻断 | 验证绑定前实际ReviewOnly拒绝；绑定后ReviewOnly | pass | guard-before-validation.json、formal-review.txt；非换摘要绕过 |
| 正式接线 | 当前实际入口/清单/依赖摘要，外层及内嵌AST，固定管理员Preflight | pass | wiring-check.json、task-a84a1d9b2ff74736bc91363938863d3b；未Observe |
| 保护范围 | 原assessment、宪章、AGENTS、003/008 tasks摘要和业务基线对照 | pass | protected-check.json；未改业务源码/接口/任务状态 |
| 通信/配方回归 | 未运行 | not-run | 用户限定本轮无设备工具验证；不能由此认定通信根因消失 |

## Output Excerpts

```text
WPR 10.0.20348: The trace was successfully saved.
ETL bytes: 248512512; xperf stats exit: 0; Administrator TraceRpt exit: 0
TCP matching: 253; CSwitch: 585; ReadyThread: 196; thread PID mappings: 4
Lost events: 0; lost buffers: 0; Administrator probe PID present: false
Formal ReviewOnly passed; fixed Preflight outcome: PreflightCollected
```

## Evidence and Remaining Scope

[完整证据索引](tool-fix-20260927T020325729Z/evidence-index.md)、[验证证明](tool-fix-20260927T020325729Z/verification-proof.json)保留实际命令、版本、ETL/XML摘要、处理错误、时钟校准、清理及接线依据。历史原tool-result的parse失败保持原样，最终证明是复用同一ETL的后续解析结论，不改写历史。

整体Result保留partial，防止把本轮工具验证当成T065验收。下一步准确Spec Kit提示词在fix.md末尾：一次有界通信关联评估，无依据不得提出确定业务补丁；r22 HTTP超期独立保留。
