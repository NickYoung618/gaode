# 007 T015 / 006 T044 当前构建页面验收记录（2026-09-24）

本次显式目标为 `specs/007-station01-integrated-loop`；仅处理 006 T044、007 T015/T024。项目既有的 Session 2 同用户、有限权限交互会话机制经无业务动作探测可用，随后在独立 Test/VirtualPlc 端口、SQLite、WebView2 用户数据目录和证据根中，使用真实 WPF/WebView2 原型登录与启动控件的 CDP 鼠标事件。没有直接由夹具调用启动 API、页面内部启动函数、PLC 动作或真实设备；每个实际启动样本仅点击一次。临时计划任务、所创建的 Host/PLC/worker/桌面进程均在执行后退出；不管理其他会话进程。

## 当前构建身份与复用

- `artifacts/station01-007/t015-wpf-corrected-20260924-182854/process.json`：Host DLL `B1B4CA7A…98D28`、VirtualPlc DLL `46B1066C…7E9693`；007 公共/模拟配置 1.2.0、预算 1.1.0，SHA 见原件；Modbus 与 API 均独立回环端口，Test 数据库独立。
- 同目录 `interactive-desktop.json` 与 `logs/desktop-runtime.jsonl`：WPF Session 2/窗口句柄 `5637064`，桌面 exe `A72420FB…6893C`、实际逻辑 DLL `A494C04F…15DA6`，加载的 `frontend/dist/runtime.js` `8FBB2CB4…0082`。受控 PrepareOnly 请求在桌面初始化前注入，其哈希见原件；没有把旧构建成功当作此构建通过。
- 007 T002/T019 保留原有完成记录；T019 的辅助 API 样本 `artifacts/station01-007/t019-platform-direct-20260924-154834/platform-smoke.json` 仅证明其记录版本的平台能力，不抵扣页面。
- T020–T023 原必要失败证据 `artifacts/station01-007/necessary-failures-20260924/evidence-reuse.md` 保持有效范围；本次只读重新核对 PLC 阶段适配器、固定图片、独立 worker、整盘存储、003 阶段合同及公共/模拟/预算配置的 SHA 均与其中所列相同，没有重跑或覆盖原 TRX。心跳缺陷 `.specify/bugs/station01-heartbeat-response-delay/test.md` 仍为 partial。
- 历史三份 `heartbeat-accept-01/02/03-20260924/interactive-session2/` 均为真实旧构建页面完成包，但 Host/PLC DLL 不同；仅复用它们原版本的成功事实，不当作当前构建 T015 通过。

## 本次页面与后端事实

首个独立包 `artifacts/station01-007/t015-wpf-current-20260924-182637/` 已发生真实一次 POST/202，但采证脚本把 202 后暂时不可查的运行过早判成终态，且输出格式不能被既有收集脚本消费；`validation-error.json` 和原始截图、请求、SQLite、`diagnostic-index.json` 全部保留。此包不作产品通过或失败结论。只修正验收脚本的等待和字段后，在**另一独立根**补跑一次；这不是心跳失败后的自动重试。

补跑包 `artifacts/station01-007/t015-wpf-corrected-20260924-182854/`：

| 核对项 | 实际事实 | 原始证据 |
| --- | --- | --- |
| 原入口及受理 | Session 2 真实登录/启动鼠标点击一次；前端原样提交公共/模拟 1.2.0、预算 1.1.0，唯一 POST 返回 202、`receiptDurability=Pending`，`requestId=s01-007-6caa10bf5d654e3e903890d01a341ba6`，`commandId=10025879-6bcb-4dde-9e1c-8c6ef92ab219`，`runId=d39ee415-23e3-46ea-aa60-8d2b5d0f8b3c`；202 后页面为“已受理”，未假报完成 | `normal-webview2-page-evidence.json`、`normal-02-before-start.png`、`normal-03-after-post.png`、`frontend-operation.json`、`api-transcript.json` |
| 页面/查询 | 随后页面显示 `Blocked`、“无法确认设备安全”；正式 GET/SQLite 为 `StartupNotReady`，停止阶段 `StartupReadiness`，`PlcHeartbeatLost`、`Unconfirmed`、`BlockedNoDeviceAction`，代次 2；`FinalUnloadCompletion` 未产生，未进入 3D/F/Detection/下料/解锁 | `normal-04-final.png`、`page-api-device-facts.json`、`station01.test.db`、`final-result.json`、`diagnostic-index.json` |
| 时序与安全 | Host 保存的最后观察为 `2026-09-24T10:29:32.122Z`；页面 POST 在 `10:30:03.872Z`，故此次心跳已在点击前丢失，不能归因于 3D 算法或下料。VirtualPlc 没有明确不安全报警反馈；Host 因无法确认安全阻断，SQLite 没有后续动作 operation，未盲重发 | `logs/host.out.log`、`logs/plc.out.log`、`page-api-device-facts.json`、`diagnostic-index.json` |
| API/通知 | WebView2 到 Host 的 SignalR negotiate/LongPolling 已取得 HTTP 200、正式运行 GET 已返回 200；本包没有取得可独立复核的 `StateChanged/DiagnosticChanged/HandoffReady` 帧，所以**通知触发重取仍未验收** | `normal-webview2-page-evidence.json` 的 network/notifications |

该次运行在心跳阻断后停止自动正常路径重试。Host/PLC 原始异常窗口只证明**本次**点击前心跳失效，不能反证或修复原现场 3516 ms、此前点击前间歇故障的根因。`manifest.json`、`frontend-operation.json`、`api-transcript.json`、`final-result.json` 均标记 Blocked，不得当作 T015 完成索引。

另用与正常运行隔离、无授权启动成功的页面包检查 401/403：

- `artifacts/station01-007/t044-auth-wpf-20260924-183240/`：实际页面点击一次，WebView2 POST 为 401；采证器原先等待空的错误响应体而超时，原始 `auth401-webview2-page-evidence.json` 和截图仍在。页面在点击前仍停留原型页显示 `HTTP 401`，未实现 006 API 合同建议的回登录页提示。该包 SQLite `Runs=0, StageEvents=0`。缺少点击后截图，因此不声称 401 最终页面处理已验收。
- `artifacts/station01-007/t044-403-wpf-20260924-183452/`：实际页面点击一次，权限有限的受控 Test 工程师令牌使 POST 返回 403；`auth403-04-final.png`/`auth403-webview2-page-evidence.json` 显示页面保留启动按钮并提示 `HTTP 403；受理状态未知`，而 SQLite `Runs=0, StageEvents=0`。这是可复现的**页面授权拒绝误报为受理未知**，与 `specs/006-frontend-station01-console/contracts/api.md` 的 403 权限不足/禁用或隐藏无权限操作映射不一致。定位于 `frontend/src/runtime.js` 的启动 catch：仅在 `details.runCreated === false` 时显示“未创建运行”，未按明确 401/403 分类；本轮未修改业务源码或降低验收标准。

阻断后的页面还把“公共准备”卡片保留为“进行中”，虽主判定和故障区明确为 `Blocked`；此为待修正的状态展示不一致，不能把它当成阶段正在执行。202 后存在短暂 `/runs/{id}/evidence` 404，页面一度显示 `Unknown/运行不存在`，随后正式查询恢复并显示 Blocked；不把暂态误写为终态，但也不判当前展示完整通过。

## 任务结论与后续门禁

| 任务 | 本次状态 | 未满足条件 |
| --- | --- | --- |
| 006 T044 | 未完成 | 401/403 页面处理不符合现行映射；无通知事件帧及正常运行阶段展示的当前构建完整证据。 |
| 007 T015 | 未完成 | 唯一当前构建正常页面补跑因点击前心跳失效在 StartupReadiness 阻断；无 3D/F、C/A 必检次数/耗时、Detection 120 秒余量、下料、解锁、Test 模拟取盘或 Final 的同 run 事实。 |
| 007 T024 | 未执行、未勾选 | T044/T015 前置未满足；不得把 T019 辅助样本、T020–T023 定向失败或三份旧构建完成包拼成当前 SoftwareLoopOnly 通过。后续进入 T024 时仍需最小同步现行 FR-019–021、SC-009–010 的证据口径，并逐项 Passed/Failed/Blocked/NotRun。 |

不勾选 006 T045、007 T029 或其他任务；未对真实设备、生产参数、客户原型和 PLC 来源协议作改动。未批准七格媒体映射不作为正确展示证据，Test 客户端模拟取盘也不等于真实人工。
