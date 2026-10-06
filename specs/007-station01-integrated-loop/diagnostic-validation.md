# 2026-09-24 T026/T027 诊断增量验收记录

本地收集器 `scripts/collect-station01-diagnostics.ps1` 与只读关联查询 `scripts/query-station01-diagnostic.py` 已复用独立 Test 证据根、Host/VirtualPlc 原始日志及 SQLite，进程退出后生成 `diagnostic-index.json`，附文件哈希、行号、请求/任务/设备动作关联。`api-diagnostic-communicationafterreceipt-20260924-020126/` 可从 POST 202 的 `requestId` 追到 `commandId/runId`、SQLite `operationId`、epoch=2 和首个 `IOException: HeartbeatStoppedChanging`；停止在 `WaitingClamp`，处置 `UnknownHeldNoAutomaticRetry`。`api-diagnostic-unsafebeforerequest-20260924-020351/` 则显示同代次可靠不安全 `alarmBits=2`，未派发启动动作。两者均为 VirtualPlc 软件证据；没有真实设备故障注入。

生效 3 秒心跳阈值及状态过期阈值来源见 003 `validation.md` 和各样本 `process.json`。建运行前心跳失效样本另存 `api-diagnostic-communicationafterreceipt-20260924-015903/`，无 `runId`，不冒充受理后故障。进程退出后的索引成立，但没有取得实际 006 页面请求/显示证据，T026 的完整跨页面链路保持未完成。

T027 必须从实际 WPF/WebView2 原有启动入口完成点击及两类失败显示截图。本轮桌面会话尝试未取得有效页面截图或点击回执（尝试材料见 `artifacts/station01-007/diagnostics-unsafe-20260924-*`），辅助 API 样本不替代该条件；T027/SC-009 保持未完成。006 T044/007 T010 的实际页面连通前置亦未证实，未以 T015 完整正常闭环作为前置。正常启动/查询回归 15/15；正式 Modbus 既有回归有间歇性夹紧等待超时，见 003 记录，不能声明稳定。

## 2026-09-24 后续T026/T027实际验收

上段为先前未完成状态，不覆盖历史。T063的VirtualPlc启动边沿缺陷已由003定向复现、修复及有限重复验证。实际页面前置由WPF进程会话2、WebView2资源SHA、页面POST/GET和Host跨源/授权回执证实；不要求T015完整正常闭环。两包均使用独立Test端口、SQLite、VirtualPlc、Host和证据根，无真机注入，凭据不写入页面抓包或索引：

| 场景 | 退出后证据包 | 页面与正式事实 |
| --- | --- | --- |
| A 已受理后通信失效 | `artifacts/station01-007/page-diagnostic-communication-valid-20260924-0250/` | 点击前GET可靠就绪；页面一次POST 202及runId后才注入`PauseHeartbeat`，首次Host `HeartbeatStoppedChanging`晚于注入；`requestId→commandId/runId→operationId/epoch`、Host Modbus transaction与PLC审计可追。启动信号已发一次，随后GET为`WaitingClamp/Unconfirmed/PlcHeartbeatLost/UnknownHeldNoAutomaticRetry`；无XY/采集/算法后继，SoftStop若仅请求不写成已确认物理停止。页面显示查询/人工核查，二次点击无第二POST，重开仍读正式事实。 |
| B 同代次明确不安全 | `artifacts/station01-007/page-diagnostic-unsafe-final-20260924-0245/` | 页面一次POST 202；GET为`StartupReadiness/ExplicitUnsafe/SafetyInterlockDenied/BlockedNoDeviceAction`，可靠反馈`connected=true/safetyClear=false/alarmBits=2`、epoch=1。协议审计无启动/XY命令，未编造operationId；页面显示授权现场排查，不写成网络故障，二次点击无第二POST，重开仍读正式事实。 |

每包 `webview2-page-evidence.json` 仅保存脱敏URL、正文、状态码及鼠标点击点，不存Authorization；`01`至`06`截图、`interactive-desktop.json`、`page-api-device-facts.json`、原始Host/PLC日志、实际`station01.test.db`、`process.json`及进程退出后`diagnostic-index.json`均保留。索引含版本、配置、时间、PID、协议及资源哈希、截图哈希与SQLite关联；无动作时明确无operationId。`artifacts/station01-007/diagnostics-20260924/page-diagnostic-verification.json` 是仅读取两份已保存包、不调用活API的复核结论，逐项比对请求/回执/GET/页面、故障时间、原始日志、设备写入计数及截图哈希。此前 `page-diagnostic-communication-final-20260924-0240/` 因点击前已有Host心跳故障，仅作无效样本保留，不计T027。

T026、T027和SC-009仅在上述Test/WPF-WebView2/VirtualPlc诊断增量范围完成。真实设备、原始人工启动故障根因、T015完整正常闭环和历史未完成任务均未因此通过。

## 2026-09-24 独立缺陷：006页面与007冻结配置引用漂移

保留上方T026/T027诊断证据及原结论。本次从007现行公共/模拟`1.2.0`、预算`1.1.0`样本读取前端测试引用，并核对 `simulate-station01-load.ps1` 和 `start-station01-virtual-loop.ps1` 当前供给值；页面不再维护具体版本白名单。局部测试、后端拒绝及新旧桌面资源哈希见 `.specify/bugs/station01-start-config-version-mismatch/fix.md`。本轮无实际WPF/WebView2新点击、无正常整链下料验收；007 T015、原下料缺陷partial结论、001 T088和间歇性心跳问题均不因这次修复改变。
