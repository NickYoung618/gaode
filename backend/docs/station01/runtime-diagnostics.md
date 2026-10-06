# 第一工位运行日志与复现取证（2026-09-24）

本次补齐的是当前第一工位主流程的内部诊断，不是重做控制流程或宣称所有故障已修复。沿用 AGENTS.md 与宪章 P09；没有新增公开 API/PLC 字段，没有修改客户原型、协议来源、功能规格、计划或任务勾选。

## 生效与日志位置

正常结束当前会话后，再使用**不带 `-SkipBuild`** 的一键启动。运行中的 Host 和桌面不会热更新；本轮隔离构建不覆盖它们的二进制文件。

同次 `artifacts/station01-007/manual-*/` 目录保存：

| 文件 | 用途 |
| --- | --- |
| `process.json` | 本次进程、配置、资源哈希、准备请求位置；桌面日志路径也在这里 |
| `logs/launcher.jsonl` | 构建、平台就绪、请求准备和桌面启动阶段；失败阶段及异常；不记录令牌。脚本 finally 才落盘，不在 SQLite 准备前污染空目录 |
| `logs/desktop-runtime.jsonl` 和 `.previous` | WebView 初始化/导航/崩溃、实际资源 SHA256、页面点击、本地拒绝、POST、受理关联、受阻状态和查询失败 |
| `logs/host.out.log`、`host.err.log` | 原有启动/安全/协议诊断，加 `RuntimeFlow`、`RuntimeHttp` 与原始异常堆栈；Host 启动早于诊断订阅的异常仍查 stderr |
| `logs/plc.out.log`、`plc.err.log` | PLC 扫描、信号与心跳诊断；与 Host 连接/事务配对 |
| SQLite、媒体、准备请求、原有自动取盘客户端证据 | 核实已提交事实、媒体和最终确认来源；不得仅凭一条日志推定提交或物理完成 |

从非一键入口打开桌面且没有 Test 准备请求路径时，桌面日志在当前用户 LocalAppData 下 `Gaode/Logs`。普通浏览器没有 WPF 的持久化接收器，页面诊断只在控制台，不能冒充已保存。日志目录不可写、磁盘满、日志级别关闭、强制杀进程或断电，都可能造成证据缺失。当前一键结束使用强制结束进程，不保证尚在缓冲区中的输出完成；异常出现时先保留现有证据，不反复重启覆盖现场。

## 覆盖与事实边界

- 页面：`StartClicked`、`StartRejectedLocally`、`StartPosting`、`StartAccepted`、`StartFailed`、`StateObserved`、`QueryFailed`。重复点击记录当时门禁；不重复 POST。原型布局/文字不变，仍只通过正式 API 工作。
- 受理/配置：请求、命令、运行、会话、配置引用及冻结摘要；配置阻断项和启动就绪的可靠观察。API 记录状态码/耗时/traceId，不记录请求体或 Authorization。
- 夹紧/运动/检测握手：动作与操作编号、目标、连接代次、期限、受理和完成判定、实际位置/安全观察；3D/F 主体原始异常在清理握手前记录，避免后续清理错误掩盖先前原因。
- 采集/媒体：角色、绑定、采集编号、请求/回调关联、被忽略的不匹配反馈、完成判定；媒体排队、文件保存结果、部分文件保留位置。真实相机适配器也保留开设备/开灯/触发/关灯失败原因，但本轮不构成真机验证。
- 算法：调用/操作/输入媒体编号、能力版本、排队与原始期限、是否派发/受理、超时/返回/输入释放区别；Worker 消息类型、关联字段、原始收发/解析异常及有界 stderr，不记录图像或完整算法载荷。
- 保存：入队、writeId、预期修订、开始提交、提交结果/条件拒绝/未知；数据库返回失败回执时仍保留底层异常。保存失败不会被日志改成成功。
- 配方/后续流程：计划加载与绑定、handoff、Detection/Sorting/Unload 的阶段事件及持久化结果、解锁观察和最终取盘确认。`Returned` 只代表调用返回；须看结果中的 status、ErrorCode、DeviceHeld、来源与已提交证据。
- 心跳：继续使用 [心跳窗口说明](heartbeat-diagnostics.md)，未调整安全门限、动作时序或自动重发策略。

`RuntimeFlow` 后是 `station01-runtime/1` JSON，包含分类、级别、UTC、进程、单调时钟及频率、runId、step、outcome；有起止的调用使用相同 spanId 和 elapsedMs。根记录把 requestId/commandId/runId 串起；后续通过 operationId/actionId/captureId/callId/writeId、连接代次与配置快照继续追踪。多个会话不能只凭短暂状态或会回绕的事务号关联。

## 排查顺序

1. 先按 `process.json` 确认**同一次**运行目录和实际加载的资源版本。页面点了却没 POST：看 `StartRejectedLocally`、重复点击门禁、页面初始化/脚本异常；不能直接判定 PLC 故障。
2. 按页面 requestId 找启动受理，得到 runId 和 commandId。沿 runId 找最后阶段及 Warning/Error；再按操作/采集/算法/保存编号缩小范围。通常最近的外层 Blocked 是结果，先前底层异常才是线索。
3. 对每个 `Started` 找同 spanId 的 `Returned/Threw/Cancelled`；有开始无结束仅说明尚无结束证据，可能仍在等待、线程停顿、进程退出或日志丢失，**不能自动宣布根因**。结合该操作期限、最近回调、心跳窗口和进程状态。
4. 采集失败先看 CameraFileCapture/CameraSdk；算法失败看 WorkerDispatch/WorkerFeedback/WorkerFailure 和 workerSessionId；保存失败看 DatabaseCommit 的原始异常与 writeId。Detection 另外记录当前 capture/call 和 phase，区分采集等待、媒体保存、算法返回、输入释放、结果保存。
5. 下料/解锁 Unknown 检查最新可靠反馈和原始异常；写回执不是动作完成。人工/受控 Test 取盘来源以保存证据为准。
6. 保存同次完整目录和截图。运行中复制 SQLite 时还需考虑 WAL/SHM 一致性，不仅复制单个 db 文件；不要为了“通过”复位或重发未知动作。

快速文本查询例（把路径与标识替换为本次实际值）：

```powershell
rg -n -F '本次requestId或runId' 'E:\dzk\gaode-1\artifacts\station01-007\manual-本次目录\logs'
```

## 输出控制

- 不增加 PLC 读取、运动指令、自动重试或扫描轮询日志；只记录关键节点与决策。常规 INFO 不能用于推断完成。
- 采集/算法/运动回调每次操作最多输出前 16 条；终局判定另记。页面状态和重复查询错误去重；不能承诺每一条高频重复回调永久保留。
- Worker stderr 最多保留最近 32 行，每行最多 2048 字符；前 8 行即时记录，故障/流结束记录尾部与省略计数。常见 token/password/secret/Bearer 文本脱敏，仍应把原始诊断目录当受控资料，分享前核查。
- 桌面日志单文件约 2 MiB 轮换，保留一个 previous。新事件不包含完整宿主配置、身份令牌、图片字节或完整业务请求。
- 日志输出失败不替代业务返回结果；没有配置诊断接收器的独立库调用不自动拥有持久日志。Host 已在启动服务前注册接收器。

## 本轮验证范围

证据根：`artifacts/station01-007/runtime-log-20260924/`；构建仅在隔离目录。

- 新增保存原始入队异常、算法期限与未释放资源、采集失败/错代次限频、SQLite 原始错误、stderr 有界保留/脱敏的定向测试；断言从保存的日志重新读取，不借在线 API 猜原因。
- 新增当前 007 配置引用的正式 Host API + 回环 Modbus/VirtualPlc + SQLite 正常流程日志关联验证，走到 FinalUnloadCompletion。该项相机/算法使用既有模拟端口、取盘为 Test API 客户端，**不是实际 WPF 页面或独立 Python 全流程验收**。
- 前端 34 项测试和类型检查通过；桌面 6 项测试及隔离构建通过。桌面测试验证文件记录、轮换与令牌隐藏，不等于实际 WebView 点击落盘已验收。
- 首次旧整站回归 `runtime-baseline.trx` 为 3/5，两个测试引用 001 旧配置，缺少当前要求的下料目标，原失败记录保留。没有放松生产配置门禁或回填它们为通过。
- 一次 51 项合并回归为 49/51：两项心跳窗口测试在并行负载下发生连接中断及普通窗口限频（先出现 ScanDelay）。失败 TRX 保留，不能据此宣称所有组合负载稳定。后续分组结果另行记录，不覆盖失败。
- 随后不修改产品门限或放宽断言，分组执行：`runtime-contracts-isolated.trx` **47/47**；`runtime-heartbeat-isolated.trx` **4/4**。这证明所列分组通过，不抹去合并回归的失败。
- 当前 007 正常流程及保存/媒体门禁组合：`runtime-integration-r2.trx`、最终重建的 `runtime-integration-final.trx` 均 **6/6**；桌面最终 `desktop-runtime-final.trx` **6/6**。正常链路与五类诊断样本保存在 `diagnostic-samples/`，可脱离在线实例重读。

尚未执行新的真实 WPF/WebView2 点击落盘或真机验收，也不把原始间歇心跳问题、第一工位总验收或其他 Spec Kit 待办标成完成。应用日志应迅速指出失败环节与直接依据；操作系统调度、网络/驱动等问题可能仍需抓包或运行时跟踪，不能保证任何故障只凭日志百分之百确认根因。
