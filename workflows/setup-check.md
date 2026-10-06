# Workflow配置检查记录

日期：2026-09-21；当前配置版本：1.1.1。

## 1.0.2请求路径修正（当前结论）

正式运行a71b9236证明1.0.1探针未覆盖真实Phase入口：环境变量指向request.json文件，桥接误当目录。
已修正父目录解析及Worker的request_file传递，并补充计划任务退出状态的有限收敛检查。

- 新增真实请求路径回归：6项通过，包含实际dispatch、PowerShell桥接、计划任务和Worker读取，
  阶段接收器使用隔离诊断程序，不执行业务analyze。
  [results.txt](../artifacts/workflow-selfcheck/06c1783121aa40af9e84da644381c963/results.txt)。
- 原有控制12项、执行与报告13项均通过：
  [控制结果](../artifacts/workflow-selfcheck/fd0ebbea01c840599e9d847ec5daadd7/results.txt)、
  [执行结果](../artifacts/workflow-selfcheck/36d6288f4b494347adb313edc1b2c895/results.txt)。
- 增强后的`dev.ps1 -Doctor`先执行请求路径回归，再执行实际模型/命令/报告/accept。
  request_id=`34cba64ac250445a8d36d82df7fe748d`，run_id=`bc778bff`，completed。
  [result.json](../artifacts/workflow-doctor/34cba64ac250445a8d36d82df7fe748d/result.json)。
- `dev.ps1 -Check`及脚本语法检查通过；181个受保护业务/规格/旧运行文件无变化；无已知残留诊断任务或Worker。
- 本轮未新建/恢复业务运行，未执行业务analyze、implement或产品验证；M1仍未据此判定完成。

完整记录：[repair-report.md](../artifacts/workflow-diagnostics/20260921-request-path-fix/repair-report.md)。

## 1.0.1排障与复核（历史，探针未覆盖真实请求路径）

- `dev.ps1 -Check`：通过，仅代表静态配置有效。
- `test_runner.py`：12项通过，证据：
  [results.txt](../artifacts/workflow-selfcheck/6cd805960b674e2c901204e704c1caee/results.txt)。
- `test_execution.py`：13项通过，覆盖缺报告、错误nonce、假证据、禁止覆盖、真实期限、
  提前识别执行器故障、退出0仍失败、历史日志不误报及子进程回收。证据：
  [results.txt](../artifacts/workflow-selfcheck/c29021fe02ab40e9a385cc6f6aff11ff/results.txt)。
- `dev.ps1 -Doctor`：官方引擎实际调用Codex执行读取、Test-Path及写入，外层校验报告并accept通过；
  request_id=`c799630f7a654145b227f58329245fd1`，run_id=`8ff564f1`。
  [result.json](../artifacts/workflow-doctor/c799630f7a654145b227f58329245fd1/result.json)、
  [acceptance.json](../artifacts/workflow-doctor/c799630f7a654145b227f58329245fd1/acceptance.json)。
- 该探针的引擎状态、执行JSONL、命令退出码、报告和Worker回执均在同一隔离目录中。
  不使用正式业务request/run，不调用implement/verify/fix，不修改M1任务。
- 前一次包装探针`074fe4747f65430c98cdf0f4e5fed900`因排队状态判断及编码问题failed，证据保留。
  它的Codex随后完成不改变原引擎failed结论；修正后以新的**诊断探针**复核，不重开业务运行。
- 排障依据、剩余环境依赖及旧运行保护结果见
  [repair-report.md](../artifacts/workflow-diagnostics/20260921-workflow-repair/repair-report.md)。

以下为1.0.0初次配置时的历史记录，其中“未调用模型/无运行记录”不描述当前状态。

## 1.0.0初次配置检查（历史）

| 检查 | 结果与证据 |
|---|---|
| 本机官方Workflow定义校验 | `dev.ps1 -Check`返回configuration=valid；使用已安装引擎的validate API |
| PowerShell语法 | dev.ps1、step.ps1的ParseFile无语法错误 |
| 工作流控制与入口规则 | 12项自检全部通过，见下方结果文件 |
| 新需求/接续 | 新需求执行三个设计阶段；接续跳过三个设计阶段 |
| 修复与停止 | 正常通过零修复；失败后可修复；最多两轮修复；阻断及耗尽时不执行finish |
| 恢复 | 阶段受阻后resume会重新进入该阶段prompt；验证了本机inputs.json的嵌套结构 |
| 其他规则 | 实际M1为34项依赖闭包；拒绝依赖循环/缺失、越界路径、过期报告；TRX零测试/未全执行/失败不能通过 |
| 实际开发运行记录 | `dev.ps1 -Status`返回空数组；未启动项目开发Workflow |

自检结果：[results.txt](../artifacts/workflow-selfcheck/c652f7f236c843309847de85da4712a1/results.txt)。
自检脚本：[test_runner.py](../scripts/workflow/test_runner.py)。
自检使用官方引擎处理if/do-while/resume，替换shell和prompt执行器，并禁止外部进程调用。
产生的引擎状态仅位于artifacts/workflow-selfcheck下，与正式运行记录分开。

## 1.0.2历史调整与适用边界

以下条目记录1.0.2当时的边界；当前1.1.1复核结果见下方“工作流优化复核”，不要用历史的
“未运行”描述覆盖最新Doctor和Verify证据。

- 恢复读取`inputs.json.inputs`；analyze/implement各自包在可恢复的阶段分支中。
- 工作流文件及阶段规则纳入运行基线摘要；模型不得通过修改验证入口跳过门槛。
- 构建开始与验证结束的源码摘要必须相同，随后审查也不能修改该构建依据。
- 未修改第一工位spec、plan、tasks、宪章或backend；未勾选任务。
- 未调用Codex模型，认证/实际非交互运行尚未验证。
- 未运行产品restore/build/test；未执行数据库、媒体业务或设备操作。
- 测试夹具能否正确使用隔离Test根，须在第一次实际实现/验证时检查；设置环境变量本身不是证明。
- 上述结果只证明配置及所测控制路径，不证明实际模型将完成全部任务，亦不证明产品或真实设备通过。
- 主对话正在实施M1；配置入口不会自动接管该对话。必须先停止其他写入，再启动Workflow。

## 重复配置检查

```powershell
.\dev.ps1 -Check
.\dev.ps1 -Status
```

这些命令不启动开发。真实start/resume会调用模型、修改授权范围代码并执行开发测试，
使用方法见[README.md](README.md)。
## 2026-09-21 工作流优化复核

- 执行包装：38项通过，覆盖容量/429/网络/额度错误分类、thread/session记录、工具调用后resume而非重放、
  未知工具状态拒绝恢复、额度恢复时间门槛、容量失败后的人工恢复、日志限额、报告校正边界、进程超时及子进程回收。
- 引擎控制：12项通过，覆盖M1闭包、Fix上限、恢复和报告身份。
- 桥接：6项通过，覆盖Session 0到已登录交互会话、request.json文件契约和任务清理。
- Verify：9项通过，覆盖统一入口、隔离Test根、TRX计数、失败/超时、源摘要变化和0/1退出码。
- 真实Doctor：请求`5e1776b278ab454d8aff8bd2e66d24c8`通过，运行`87ac95e6`，
  `execution-logs/probe-593d67cb660e4d1181de4fab478bf4de.execution.json`显示真实三条工具命令、
  报告发布和验收均成功；范围仅为Workflow执行与报告传输，不是业务验收。
- 本轮优化后真实Doctor复测：请求`1175bddf4c7b43a79e3817b15a6fc78e`、运行`1a8d3447`通过，
  执行日志记录`tool_started=true`、`session_id=01a0c432-6b8f-7012-a817-d8dd84d5f5b2`、
  `incomplete_tools=[]`、`tool_state_uncertain=false`，日志占用约3.5 KiB；说明当前服务器的真实Codex
  JSONL/工具/报告链路可用，范围仍仅为Workflow执行与报告传输。
- 真实Verify：请求`standalone-104f8728fab44edfb03eba7af0d5c1eb`通过，
  restore/build及Rules 28、Contracts 62、Integration 41全部通过，共131项；证据见
  `artifacts/workflow/standalone-104f8728fab44edfb03eba7af0d5c1eb/verify-01/verification.json`。
- 容量故障实测：请求`83457c1bb7354d9993878f52cd82517a`的specify阶段收到
  `Selected model is at capacity`并安全停止；原始执行日志已保存thread id，未生成业务规格、未进入Fix。
  后续同类错误按30/60/120秒退避并带抖动，工具启动后优先resume原session；重试耗尽或额度耗尽显示为
  `paused_retryable`，不会伪装成业务Fix失败。

一次并发Verify因源码摘要在运行期间变化而按设计失败；说明项目当前不允许并发写入，不能把
该次结果当成产品测试失败。下一次产品Verify必须在WorkspaceIdle确认后重新执行。
