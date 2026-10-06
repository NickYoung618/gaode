# 前端合同差异与变更任务

**012澄清交付时状态（历史）**：2026-10-03，012副本；已按统一确认定向修订本文件有效条款，历史证据及任务勾选保持原范围。仅文档同步，尚未合入主项目或证明实现/验证通过。

这些差异只作为前后端合同变更记录，不能由前端通过本地模拟、直连设备或修改原型绕过。

| ID | 差异 | 当前证据 | 变更任务 | 前端临时行为 |
| --- | --- | --- | --- | --- |
| FE-C01 | 006 需要登录页，但当前 Host 主要提供 Test Bearer Token，没有独立生产登录端点 | `backend/src/Gaode.Host/Api/Station01Authorization.cs` | 确认生产认证端点或外部认证注入合同、令牌生命周期和四类角色映射 | Test 环境使用配置令牌；无端点时显示 AuthUnavailable，不伪造成功 |
| FE-C02 | 部分错误返回匿名 JSON/ProblemDetails，不全是 `ErrorContract` | `RunEndpoints.cs`、`RecipeEndpoints.cs`、`ControlEndpoints.cs` | 统一错误字段、状态码、traceId、retryable、currentRevision | 兼容 `error`/`message`，未知错误显示通用受限提示 |
| FE-C03 | SignalR 事件集合、版本升级和重取语义需要冻结 | `NotificationEnvelope` 与 `Station01NotificationService` | 固化事件名、schemaVersion、revision/persistedRevision、断线 GET 和合并规则 | 通知只触发 GET，乱序/旧 revision 丢弃 |
| FE-C04 | 媒体读取有流和 ETag，但独立媒体引用/就绪元数据查询不完整 | `MediaEndpoints.cs`、`MediaReference` | 确认媒体元数据来源、readiness/source/purpose 和缓存合同 | 只显示已有 mediaId 和读取结果；元数据缺失显示 Unknown |
| FE-C05 | 007实际WebView2页面到Host的Test跨源/授权连通尚未验证；生产HTTPS/部署配置仍按后续实际需求处理 | `desktop/HostRuntime.cs`使用`https://appassets.local/`，`backend/src/Gaode.Host/Program.cs`当前无CORS接线 | 006负责宿主只读API/通知地址及受控Test凭据传递；specs/007-station01-integrated-loop T010负责Host侧仅限Test来源的API/通知跨源配置、后端授权与实际请求验证；specs/007-station01-integrated-loop T015核对双方整链 | 页面不能自行放宽Host授权或写死设备地址；未连通如实显示受限 |
| FE-C06 | 原型 HTML 依赖 CDN，而生产要求本地资源 | 原型 ZIP HTML 的外链脚本/字体引用 | 建立依赖本地化和视觉/DOM 对照合同，不修改原型归档 | 构建阶段本地打包固定依赖；差异阻塞发布 |
| FE-C07 | 012制作保存及011新规则尚未实装共同合同/目录读写 | 现有RecipeEndpoints与目录主要提供只读摘要，runtime配方入口以选用为主 | 012负责真实目录/完整读取/编辑保存API，消费011共同模型与唯一校验；校验保存后后续F用新内容，料盘编号唯一，冻结运行不变；承接更多面/额外E/三区域点位语义；结构/签名/存储在plan确定 | 缺能力如实不可用，不以浏览器保存、假API或固定测试配方兜底；共同合同待011交接，业务决定不再提问 |

FE-C01至FE-C06不自动纳入001/003历史后端任务。007当前已明确承接FE-C05的Host侧Test跨源/授权接线；006仅承接页面/宿主请求、凭据与展示。其他后端合同变化仍须按相应功能规格先同步文档再改代码。

## 007有限联调差异（2026-09-23历史，后续已有正式页面取盘）

- 现有`frontend/src/runtime.js`启动入口仍提交空对象；007要求实际页面提交`requestId/contextJson/publicConfigRef/budgetRef/simulationRef`，其中contextJson为合法CAP/P01 Test上下文。006有限任务负责修正页面请求及证据，旧202/幂等单测不能替代实际请求。
- 现有页面尚未证明受控Test Bearer凭据已从宿主传到HTTP/通知客户端，也未证明已有位置完整展示Detection至FinalUnloadCompletion。006有限任务负责这两项；无原型取盘控件时不新增控件，007联调客户端在解锁提交后自动模拟确认并标记Test/Simulated。
- specs/007-station01-integrated-loop T010负责Host侧限定Test来源的API/通知跨源配置和授权验证；006页面侧与Host实际连通后，specs/007-station01-integrated-loop T015才可判定完整虚拟闭环。006既有已勾选任务、模拟测试与打包证据保持原日期和范围，不记录为本次联调通过。

## 006 实现阶段审计记录（2026-09-22）

- 已实现：前端只读 HTTP 客户端、错误兼容解析、ETag/304 缓存、SignalR 通知顺序投影、受控 `mediaId` 校验、WPF 宿主配置边界和本地静态资源加载。
- 仍受限：当前 Host 没有生产登录端点，页面运行时显示 `AuthUnavailable`；Test/Simulation 只能使用宿主注入的凭据。
- 仍受限：匿名错误和 `ProblemDetails` 兼容解析保留 HTTP 状态，但前端不推断缺失的业务字段。
- 仍受限：通知只触发快照重取；事件集合和版本合同未冻结前，不把通知当作动作完成。
- 仍受限：媒体端点可读取已返回的 GUID 和 ETag；没有独立元数据时显示 `Unknown`，不读取本地路径。
- 验证边界：前端测试、宿主单测和安装包静态冒烟均属于软件/模拟验证，不表示真实 PLC、相机、算法或生产环境通过。

## FE-C04 当前细化（2026-09-23）

007已承接按runId的已提交媒体清单与业务相机身份，解决“已知mediaId才能读取”及Detection A/B无法公开关联的问题；specs/006-frontend-station01-console T045待消费该接口。**仍缺原型七格与业务ThreeD/F/A/B的确认映射**，见`prototype-mapping.md`；确认前页面维持未关联，不把后端身份推成客户确认格位。旧运行缺业务相机事件时也维持Unknown，不回填历史。

## 2026-09-24 008完整执行合同增量

本节依据宪章5.0.0、008最新澄清和用户最新原型授权，优先于前文冲突范围；旧记录保留原日期和范围。全部新增能力尚未实现/验证，当前tasks已追加S0—S5唯一归属任务，旧analysis仅历史，本次只读报告在会话输出。

| 缺口 | 当前设计处理 | 尚待实施/验证 |
| --- | --- | --- |
| GAP-008-RECIPE | catalog.items、明确版本选用、启动expectedRecipeRef及F一致性；解除S1/P01限制 | 正式Host和runtime.js同步接线，不能把当前摘要API当作目录列表 |
| GAP-008-CONTROLS | 用户已授权最小补配方选用、换面/取盘/恢复必要同页控件，映射已定义 | 保留原型基线，只绑定已存在控件、后端命令状态及实际页面交互 |
| GAP-008-RESULT | 查询投影及同页对象/面/组/整体结果、质量/处置/Final分开 | 后端实际保存和页面一致；阶段/受限显示沿现有诊断核验 |
| GAP-008-MEDIA | 用真实对象/面/相机清单选择和预览，不猜旧七格意义 | 新身份媒体查询及页面绑定；旧T045状态保留 |
| GAP-008-ENTRY | 心跳当前partial/Blocked需早期定向处理 | 保持3秒保护；当前版本真实WPF/WebView2运行证据 |

前文“runtime提交空对象/尚无CORS”是2026-09-23时点，当前已具备请求/连通代码，不能列为全未实现；当前仍缺上述008增量及适用页面完整证据。T044及007最新Blocked记录保留。API已明确部分直接进入后续任务，客户原型当前仍只读；设备未知合同仍按008 B表阻塞对应动作。

共同字段及行为以[008接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)为准。

## 2026-09-26协议关联设计（目标，未实施）

实际构建入口为`frontend/src/runtime.js`（build.mjs复制该文件），新面/执行阶段与实际测量来源分别显示，不把面2标成测量轮2；媒体选择来自同run已提交对象/面/相机事实，初始3D不伪装新采集。普通盘末展示检测→适用同盘分拣→下料定位→整盘收敛→解锁→页面确认→Final，取盘按钮仅消费后端allowedActions，不能见下料完成就启用。目标查询身份见[008 API合同](../../008-recipe-driven-inspection/contracts/api-results.md)，实现归本功能T048/T049；人工/恢复只按T050/T051局部前置。
