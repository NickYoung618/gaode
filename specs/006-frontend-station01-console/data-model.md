# 006 前端数据模型与状态投影

**012澄清交付时状态（历史）**：2026-10-03，012副本；已按统一确认定向修订本文件有效条款，历史证据及任务勾选保持原范围。仅文档同步，尚未合入主项目或证明实现/验证通过。

运行/结果模型是后端公开数据的只读投影；012弹窗编辑状态承载共同合同中的业务配置，经后端读写API提交，不另建业务模型、工艺校验或执行器。前端不复用后端Domain类、不直写数据库、不生成设备/算法事实。

## PrototypePage

| 字段 | 类型 | 约束 |
| --- | --- | --- |
| `page` | `login.html` / `a.html` / `data-view.html` | 必须属于原型归档清单 |
| `assetBase` | URL | 生产指向 WebView2 虚拟主机，不接受任意外部 URL |
| `prototypeSha256` | string | 必须等于批准基线哈希 |

## AuthSession

| 字段 | 类型 | 约束 |
| --- | --- | --- |
| `subject` | string | 来自后端认证结果；不信任页面输入自行声明的角色 |
| `permissions` | string[] | 仅用于显示/启用提示，最终以 API 401/403 为准 |
| `accessToken` | opaque string | 仅存于受控内存/宿主安全配置，不写入页面日志 |
| `state` | `Anonymous/Authenticating/Authenticated/Rejected/Unavailable` | 未有认证端点时为 `Unavailable`，不能变成 Authenticated |

## StationStatusProjection

来源为 `GET /api/v1/station01/status`，保留 `schemaVersion`、`revision`、`etag`、`host`、`plc`、`camera`、`storage`、`maintenance`、`algorithm`、`currentRun`、`capabilities`、`mode`、`stage`、`recipe`、`quality` 和 `activeRuns`。前端不得补造缺失字段。

`algorithm.state`、相机/存储/维护状态统一通过受限状态显示：`Success`、`NotIntegrated`、`NotReady`、`Unknown`、`Pending` 等原样保留。

## RunProjection

来源为 `GET /api/v1/station01/runs/{runId}`，保存：

- `runId`、`requestId`、`subject`；
- `state`、`observedRevision`、`persistedRevision`、`errorCode`、`events`；
- `action`、`capture`、`algorithm`、`save`、`handoff`；
- `finalOutcome`、版本快照引用和后端提供的其他只读字段；011真实阶段及姿态异常物理槽号，不将异常重编号或变成NG/Pending。
- 检测面序/AB/CD及独立E扫码姿态的业务身份；处置区分OK原槽无需搬运、NG/Pending目标搬运和姿态异常退出，均消费已提交后端事实。

关键映射：命令受理、等待实体启动、等待夹紧、运行、保存、暂停请求、未知、完成和异常完成均不能互相推断。只有后端状态明确为完成，前端才显示完成；受限/未知不能显示成功。

## CommandReceipt

启动、暂停、取消、恢复等命令保留 `requestId`、`commandId`、`runId`、`statusUrl`、`requestAccepted`、`receiptDurability`、`terminalDecision`、`applied`、`admissionClosed` 和错误信息。前端超时不生成第二个 requestId 自动重试，必须先查询原命令或运行。

## NotificationProjection

对应后端 `NotificationEnvelope`：

| 字段 | 约束 |
| --- | --- |
| `eventType` | 仅作为触发重取的提示，不能确认物理动作 |
| `schemaVersion` | 必须兼容 `s01/notification/1.0` 或后端声明的新版本 |
| `runId` | 只更新对应运行 |
| `revision`、`persistedRevision` | 旧版本通知丢弃；新版本触发 GET |
| `changedFields` | 仅用于选择性刷新，不替代完整快照 |
| `summary` | 只显示后端提供事实 |

## MediaProjection

只接受后端返回的 `mediaId`、`captureId`、`kind`、`contentType`、`etag`、`readiness`、`source`、`purpose` 和 `byteLength`。读取 URL 必须由宿主配置的后端基地址加受控路由生成，禁止页面传入本地文件路径或任意 URL。

## UIState

页面状态至少包括 `Loading`、`Ready`、`Refreshing`、`Offline`、`Unauthorized`、`Forbidden`、`NotIntegrated`、`Unknown`、`Conflict`、`Error` 和 `HostUnavailable`。UIState控制既有区域及012授权配方弹窗的显示/可用性，不新增页面或无关交互。编辑值、已保存内容、当前运行冻结内容分别显示，不能互相覆盖。

配方编辑状态仅承载共同合同的料盘编号、配方身份、产品型号、检测面/AB-CD、独立E姿态、三区域及各用途点位；接口结构留plan，不另建业务实体。F=料盘编号且不同配方不可共码，真实保存后后续绑定用新内容，已冻结运行保持旧内容。生产准入保持，实际结果按[API消费合同](contracts/api.md)分开表达。

## 状态转换约束

```text
Loading -> Ready | Offline | Unauthorized | HostUnavailable
Ready --notification/ETag--> Refreshing -> Ready | Conflict | Error | Offline
Ready --command--> PendingCommand -> Accepted | Conflict | Forbidden | Error | Unknown
Offline --bounded reconnect + GET--> Ready | Offline
```

状态转换不能触发设备动作；重复通知、迟到通知和重连不能覆盖更高 `revision` 的快照。

## 2026-09-26直接影响

复用既有页面，仅更新runtime.js消费的新阶段、面/实际测量来源、媒体及取盘状态；原型ZIP只读。查询合同见[008接口](../008-recipe-driven-inspection/contracts/api-results.md)，实现归本功能T048/T049，人工/恢复T050/T051局部前置，不阻塞普通自动Q03。

## USR-20260926-D运行与复位关联

复用现有Run/Command/Write/事件事实，增加必要faultRestart查询投影：faultRunId、resetId、initialCheckId、newRunId、expectedFaultRevision、committedRevision、状态/Blocked项；逐项初始观察携epoch/generation/时间及版本来源。旧轮fault outcome与媒体保留，executionClosed独立于Final；reset/check不能制造成功。新request幂等并单次消费reset/check，关联与新run身份通过现有单写短事务持久，必要模型升级仅StorePrep执行。新run创建独立操作/采集/算法/媒体/预算身份，配置版本可同但事实不复用。完整字段及状态边界见[003合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)。正常暂停/人工面确认仍属原run的独立操作类型，不复用故障RestartFullRun资格。
