2026-09-26 USR-20260926-D恢复设计：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替代旧U05同轮单指令重发。双端复位、真实初始状态成立后显式新启动；旧运行和证据保留。设计尚未实施，tasks保持只读，原编号和勾选不变。

2026-09-26 人工子范围增量：T050按[人工执行合同](../003-plc-latest-protocol/contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

# 技术方案：第一工位前端操作台与 Windows 桌面宿主

**012澄清交付时状态（历史）**：2026-10-03，012副本；已按统一确认定向修订本文件有效条款，历史证据及任务勾选保持原范围。仅文档同步，尚未合入主项目或证明实现/验证通过。

008 T061/T054查询采用既有API媒体和事件入口，E角色含实体/面/step及真实保存引用；006 T049承接问题可查，格位映射仍按原型合同，不扩页或推定E显示格位。

**功能标识**：006-frontend-station01-console  
**日期**：2026-09-24  
**规格**：`specs/006-frontend-station01-console/spec.md`  
**当前适用宪章版本**：7.0.0；历史验证证据保留产生时版本  
**范围**：仅前端页面、WebView2 桌面宿主、前端与现有后端公开接口的对接验证；不修改 001、003 或其他后端规格，不实现 PLC、相机、算法、数据库和后续工位。

## 方案摘要

本功能以客户确认的只读原型 `E:\dzk\gaode\原型.zip` 为页面基线，SHA-256 为 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`。归档页面与前端实现的对应关系固定为：`a.html` 第一工位操作台、`data-view.html` 数据查看页、`login.html` 登录页。a.html已有配方弹窗按012授权承接基础信息、点位配置、检查保存，顶部公共位置入口/弹窗及异常选择按新016授权承接，视觉融入现有UI；其他结构/布局/文字/控件/导航不变，只绑定后端事实。客户归档不修改，授权差异记录在实现副本清单。

前端采用 Web 技术实现，交付 Windows WPF + WebView2 桌面程序。WebView2 使用本地虚拟主机映射加载随程序打包的静态资源，例如将 `frontend/dist` 映射为 `https://appassets.local/`，启动后导航到 `login.html`；生产页面资源不依赖 CDN、外部网页服务器或 `file://` 任意路径。WPF 宿主只负责窗口、WebView2 生命周期、资源加载、运行时依赖检查和宿主级错误边界，业务数据仍只能来自后端 API 与状态通知。

前端将对齐现有后端的运行、查询、状态、控制、媒体和 SignalR 入口；发现登录端点、统一错误、通知载荷或媒体元数据不足时，只登记合同变更任务和受限占位行为，不在页面或桌面壳中私自实现旁路协议。未接入的相机、算法、PLC、存储或媒体能力显示 `NotIntegrated`、`NotReady`、`Unknown`、`Pending` 或对应错误，不显示虚假成功。

012负责配方编辑保存、目录提供者及读写API，消费011共同模型/唯一校验；本006计划只承接相关前端消费、现有运行显示及验证义务，不代替尚未生成的012 plan。字段/签名/存储留对应plan，既有后端及宿主技术不因本轮重构。

## 技术上下文

| 事项 | 选用方案 | 决策来源与状态 | 尚缺证据/合同变更 |
| --- | --- | --- | --- |
| 页面技术 | HTML/CSS/JavaScript，使用锁定版本的 TypeScript/Vite 工具链组织构建 | 006 FR-001/FR-003、P12；实现任务锁定版本 | 不改变原型基线；工具链版本须写入 `frontend/package.json` |
| Windows 宿主 | WPF + WebView2 | 006 Clarifications，用户明确 | WebView2 SDK/Runtime 版本和安装包工具需在实现前锁定并记录 |
| 页面加载 | `CoreWebView2.SetVirtualHostNameToFolderMapping` 映射打包目录，导航 `https://appassets.local/<page>.html` | 本计划决定，避免 `file://` 相对路径与跨源问题 | 需宿主启动测试验证映射、资源缺失和 CSP/网络策略 |
| 静态资源 | 随桌面程序打包 `frontend/dist`；原型引用的 CDN 依赖必须本地化并做视觉/交互对照 | 生产不可依赖外部网页服务器；原型 ZIP 只读 | 本地化资源不得改变原型结构、文字、控件或行为；差异需合同记录 |
| 后端 API | `/api/v1/station01` 运行、查询、状态、控制、配置校验、媒体；`/hubs/station01` 通知 | 读取当前 Host 路由与 006 spec | 登录、统一错误、通知完整合同、媒体元数据存在缺口，见 `contracts/gaps.md` |
| 身份权限 | 前端只携带后端颁发/配置的身份凭据，按钮可见性由权限和后端响应共同决定 | P02、OPEN-23、现有 Test 身份策略 | 当前代码主要提供 Test Bearer Token，未提供生产登录端点；不得把页面输入伪装成生产认证 |
| 状态缓存 | 快照以 `ETag`/版本为依据；通知只触发重取或合并提示，不直接覆盖较新快照 | 006 US2/US4、后端状态合同 | 需补齐通知事件名、断线重连和快照版本合同 |
| 媒体 | 只接受后端返回的 `mediaId`，通过后端媒体读取和 `ETag` 缓存 | 006 spec、后端 `/media/{mediaId}` | 当前读取接口返回文件流而非完整媒体元数据，需记录合同差异 |
| 数据库/设备 | 前端和宿主均不访问 | P02/P05/P12 | 不适用；所有业务事实由后端提供 |
| 生产限制 | 禁止外链页面资源、模拟成功冒充真实成功、宿主直控设备或数据库 | 006 spec、P02/P09/P12 | 需安装包和运行时缺失测试 |

## 原型基线与页面映射

| 原型文件 | 前端职责 | 允许绑定的数据 | 禁止变化 |
| --- | --- | --- | --- |
| `a.html` | 操作台、运行/结果状态及已有配方入口 | 后端事实与012授权弹窗的基础信息/点位/检查保存 | 只在配方弹窗承接授权差异；不新增页面或改无关布局/导航 |
| `data-view.html` | 数据展示、结果/媒体/运行记录查看 | 后端查询结果、媒体 `mediaId`、ETag、结果状态 | 不把缺失数据填成成功，不增加未经批准的质量结论 |
| `login.html` | 登录/身份输入与失败提示 | 后端认证结果或 Test 身份配置状态 | 不在前端自行验证角色或伪造登录成功 |

原型 ZIP 及其 HTML/资源只读。实现可在独立前端目录生成打包副本，但配方弹窗允许012授权差异，顶部公共位置入口/弹窗及异常选择允许新016授权差异；记录原归档哈希、授权差异、实现摘要及无关区域对照，不更新归档或豁免整页。

## WebView2 宿主方案

1. WPF 进程启动时检查 WebView2 Runtime；缺失或版本不满足时显示宿主级受限页面/错误提示，退出或进入明确不可用状态，不显示业务成功。
2. 创建 `CoreWebView2Environment`，将随程序安装的静态资源目录映射到固定虚拟主机，例如 `https://appassets.local/`，并导航到 `login.html`。不得允许页面导航到任意外部站点或本地任意路径。
3. 前端 API 基地址、SignalR 地址和运行模式通过宿主注入的只读配置或受控配置文件提供；页面不假设静态资源与后端 API 同源。宿主不得注入 PLC 地址、数据库连接串、算法端口或可直接执行设备动作的桥接对象。
4. WPF 与页面之间只保留生命周期、窗口关闭和宿主错误边界；不提供 `postMessage`/JS bridge 形式的业务旁路。页面关闭、导航失败、WebView2 崩溃和后端断线分别显示对应受限状态。
5. 安装包必须携带页面和静态资源，并在无外部网页服务器、无 CDN 网络的环境中完成页面加载；后端请求是否可用单独显示为连接状态。

## 结构与职责

| 模块 | 计划职责 | 依赖方向 | 禁止行为 |
| --- | --- | --- | --- |
| `frontend/`（具体工程目录由实现任务确认） | 原型页面副本、API/SignalR 客户端、状态投影、错误/加载/断线视图 | 只调用后端公开 HTTP/SignalR | 不引用后端 Domain/Infrastructure，不访问 PLC/相机/算法/数据库 |
| `desktop/` 或等价 WPF 宿主目录 | WebView2 初始化、虚拟主机映射、启动/关闭、Runtime 检查、宿主错误 | 只承载前端静态资源 | 不承载业务编排，不提供设备控制桥接 |
| 后端公开 API | 提供运行、状态、权限、错误、媒体事实 | 前端 HTTP 客户端 | 前端不猜测或补造接口结果 |
| `/hubs/station01` | 版本化状态/诊断/移交通知 | 前端 SignalR 客户端 | 通知不是动作确认，乱序/迟到不能覆盖新快照 |

## 前端状态、错误与恢复设计

- 加载：页面首次进入先显示原型既有加载状态；API 成功后填充数据；API 不可用显示连接受限，不填充成功值。
- 未接入：相机、算法、PLC、存储或媒体状态为 `NotIntegrated`、`NotReady`、`Unknown`、`Pending` 时原样显示并保留来源/原因；不转换为 `Success`、`OK` 或“已完成”。
- 错误：优先解析 `ErrorContract`；对现有匿名错误响应保留兼容解析，但在 `contracts/gaps.md` 登记统一合同变更，不在前端推断错误类别。
- 命令：启动/暂停/取消/恢复等请求使用唯一 `requestId` 和后端返回的受理回执；超时或断线时不自动重发，先查询命令或运行状态确认结果。
- ETag：查询携带 `If-None-Match`；304 保留当前快照；412/409 显示版本冲突并重新查询；不以本地时间覆盖服务端版本。
- SignalR：连接成功只用于触发重取；事件按 `revision`/`persistedRevision` 丢弃旧消息；断线采用有界重连并重新 GET，重连失败显示受限状态。
- 媒体：只有后端返回的 `mediaId` 才能请求；404/尚未保存显示媒体不可用，304 使用缓存；不接受任意本地路径或前端上传替代。
- 权限：按钮可见性可按后端权限结果调整，但最终以 API 401/403 为准；前端不以隐藏按钮替代鉴权。

## 现有后端对齐与合同差异

当前 Host 已有运行、查询、状态、控制、配置校验、媒体和 SignalR 路由，详细映射见 `contracts/api.md`。计划明确记录以下差异，不在前端绕过：

1. 当前 Host 没有独立生产登录端点，只有 Test Bearer Token 认证；006 登录页需要 `AuthContract` 变更或明确采用外部认证配置，不能在页面内伪造登录成功。
2. 部分启动/冲突/配方错误仍返回匿名 JSON 或 `ProblemDetails`，未全部符合 `ErrorContract`；前端先做兼容解析，后端合同变更任务补齐统一结构。
3. SignalR 已有 `NotificationEnvelope` 类型，但事件完整集合、客户端重取规则和慢客户端合并合同仍需固化；前端不得把广播当动作确认。
4. 媒体端点当前主要返回文件流和 ETag，完整 `MediaReference` 元数据未作为独立查询合同公开；前端只使用已有 `mediaId` 读取能力，元数据缺口登记变更任务。
5. API/通知基地址由宿主只读配置注入页面，受控Test凭据供HTTP/通知客户端使用且不写入原型或日志；WebView2页面从`https://appassets.local/`访问Host须经实际跨源与授权验证。006负责页面侧请求和凭据传递；specs/007-station01-integrated-loop T010负责Host侧限定Test来源的API/通知跨源配置与后端授权连通，生产HTTPS/部署合同另按实际需求处理。

## 007有限前端接线与交付顺序

006在原型已有启动入口提交`requestId/contextJson/publicConfigRef/budgetRef/simulationRef`，其中`contextJson`按`station01-start-run-context/1.0`携带trayId、stationId、lineId、scenarioId、occupiedSlots与purpose；CAP/P01 Test样本不得提交空请求或预写配方、设备、算法成功。受控Test Bearer凭据经宿主配置和页面HTTP/通知客户端传递，身份权限由Host核验。页面在已有位置用GET为事实源展示公共准备、Detection、Sorting、UnloadPreparation、WholeTrayCompletion、ObservedUnlocked、AwaitingManualRemoval、FinalUnloadCompletion及相应受限/错误；通知只触发重取，不把202或解锁观察当最终完成。现有正式页面已有取盘确认入口；仅按后端允许状态提交，历史外部客户端代确认不作为当前页面Final证据。

有限实施先完成006页面请求/凭据与状态展示的独立实现及合同验证；007可并行实施Host侧跨源/授权、文件采集和worker。双方就绪后由006核对WebView2页面到真实Host的API/通知连通，再由007 T015完成从实际页面启动到FinalUnloadCompletion的整链联调。既有006已勾选任务及验证证据保留原范围，不倒填本次联调通过；缺少007后端或页面接线时相应结果为Blocked/NotRun。

## 宪章检查

| 原则 | 本功能检查点 | 设计前 | 设计后 | 证据/受限范围 |
| --- | --- | --- | --- | --- |
| P01 | 原型、规格和接口差异可追溯 | 符合 | 符合 | 原型哈希、006 spec、contracts/gaps.md |
| P02 | 前端与桌面壳范围受控，不扩展后端业务 | 符合 | 符合 | 本计划范围、页面映射和禁止旁路 |
| P03 | 配方弹窗表达已确认业务配置，运行事实只读 | 已确认 | 待实现核验 | 012更多检测面/可选E/三区域配置；011唯一校验和执行，不在前端生成流程 |
| P04 | 算法/设备未知不显示成功 | 符合 | 符合 | 状态投影和错误测试 |
| P05 | 唯一 Host 和后端控制权 | 符合 | 符合 | 无设备桥接、仅公开 API/SignalR |
| P06 | 有界通知/重连/媒体缓存 | 待补充 | 符合（前端策略） | SignalR/ETag 快速验证；生产容量仍属后端 |
| P07 | 身份、状态、错误和版本分离 | 符合 | 符合 | 数据模型和权限测试 |
| P08 | 前端不写数据库、不替代持久化 | 符合 | 符合 | 宿主/页面边界测试 |
| P09 | 模拟来源及启动受阻可定位展示 | 符合（历史范围） | 待补充（FR-009） | 旧FullSimulation/Windows证据不覆盖一次启动失败的原因与可执行操作；T046待验证 |
| P10 | 外部运行时和 API 配置差异局部限制 | 待补充 | 待补充 | WebView2/后端生产配置需实现时锁定 |
| P11 | 012配置制作/目录读写消费共同合同 | 已确认 | 待plan及实现 | 不复制模型/校验器，不按产品名或测试编号分支 |
| P12 | 归档只读、配方弹窗授权差异及其余区域保护 | 历史T006/T038保留 | 本次待验证 | 检查器定向承接012差异，不关闭旧保护；历史字节一致不代表新弹窗通过 |

保存消费规则：料盘编号=F内容，不同配方不可共码；校验且真实保存后后续F用新内容，旧运行冻结不变，生产准入不放宽。011输出阶段/异常槽号，前端显示OK分拣原槽、NG/Pending配置目标及异常独立退出；料盘分拣位等于上料位，各用途点位不合并，OK留原槽不取消检测期动作。

替代实现完成后，T048/T049及012后续实施必须核对实际调用、装配、配置、脚本消费者，删除无有效用途的旧目录快照分支、演示保存/执行旁路、错误阶段推断、失效配置/测试与孤立代码；是否无用须按真实消费者核定。承接有效保存/关联/取消/期限/历史读取，不以注释、永久关闭开关、备用实现或额外兼容层保留错误；失败本身不构成删测试理由，历史失败证据保留。

## 软件验证与证据计划

| 范围 | 正常/失败场景 | 方法 | 预期结果 | 证据 |
| --- | --- | --- | --- | --- |
| 原型对照 | 三个 HTML 页面、布局、文字、控件、交互 | 固定 ZIP 哈希，截图/DOM/资源清单对照 | 无未经批准结构差异 | `artifacts/frontend/prototype/` |
| API 合同 | 启动、查询、状态、媒体、控制、错误 | Mock/FullSimulation 后端合同测试 | 字段、状态、ETag、错误映射一致 | `artifacts/frontend/api/` |
| 权限 | 四类 Test 身份、401/403、按钮授权 | 使用后端 Test Token 和真实响应 | 页面不越权，401/403 可解释 | `artifacts/frontend/auth/` |
| 通知 | StateChanged/DiagnosticChanged/HandoffReady、乱序、断线 | SignalR 测试代理+GET 重取 | 旧通知不覆盖新快照，断线可恢复 | `artifacts/frontend/notifications/` |
| WebView2 宿主 | Runtime 缺失、资源加载、关闭、崩溃/导航失败 | Windows 宿主自动化/手工验证 | 明确宿主错误，不显示业务成功 | `artifacts/frontend/desktop/` |
| 打包 | 无外部服务器、无 CDN、安装后启动 | Windows 安装包隔离环境 | 三页可加载，后端断开显示受限 | `artifacts/frontend/package/` |
| 未接入能力 | 相机/算法/PLC/媒体 NotIntegrated/Unknown | FullSimulation/故障 fixture | 原样显示受限状态，不伪造 Success | `artifacts/frontend/status/` |

## 实施阶段建议

1. 先完成合同差异记录、原型资源清单和前端工程/桌面宿主骨架，不改原型 ZIP。
2. 再实现基于宿主配置的后端 API 客户端、认证状态、快照/ETag、SignalR 重取和受限状态投影；不假设页面静态资源与后端 API 同源。
3. 按页面映射绑定 `login.html`、`a.html`、`data-view.html`，只注入后端事实。
4. 对本次变化执行受影响构建、必要组件回归、原型/架构门禁及专项收敛；宿主/打包未变化部分沿用有适用范围的证据，不要求全套前端或历史专项重跑。

前端计划不修改 001、003 的 `spec.md`、`plan.md`、`tasks.md`，不把后端 PLC 任务追加到 006；若需要后端合同变更，只在 006 `contracts/gaps.md` 中登记并由后端规格单独处理。

## 2026-09-23 媒体绑定最小依赖

007负责单一按runId媒体清单及授权读取，返回稳定业务采集身份、提交顺序和来源；specs/006-frontend-station01-console T045先取得已确认的七格映射，再从公开清单选取同业务相机最大已提交revision的Ready项，经原授权媒体API取字节并绑定原位置。若身份未映射或不可读，保留原相机名称和受限状态；不以重建资源或本地文件替代。T044实际页面验证与T045媒体展示证据分别核对。

## 2026-09-24 FR-009受阻呈现增量

在原型已有错误/状态/提示位置绑定后端脱敏错误与GET快照，不动原型结构、控件、固定文字或交互。页面保留同一`requestId`及已收到的`commandId/runId`供重取；202只显示受理，超时或断线先查原请求。以现有`code/category/message/details/currentRevision`、运行限制原因和设备状态作分支：通信失效或状态过期仅提示安全无法确认并建议重新查询、核对连接/联系有权限人员；可靠反馈明确不安全则展示对应互锁/报警及现场排查指引；配置、保存、授权失败按后端已知结果提示允许的下一步。若后端没有给出可靠原因，显示原因尚未知和查询关联，不编造设备故障或自动重发。原始异常只留后端受控日志，前端不展示堆栈、令牌或内部地址。

T046使用一次已受理启动后通信失败和明确不安全对照，核对已有位置文本/状态、GET事实、同一请求关联及可执行操作；再覆盖未知原因和页面重开/乱序通知。来源为Test/Simulated时截图与结论须如此标记。006仅消费既有公开字段，合同不改；如后续发现公开事实缺口，先同步001/003对应合同及006规格，再实施。旧T006/T038/T042–T045和历史证据不回填本项通过。

实施核验已发现正式GET缺少 `StartupNotReady` 明细，因此已先同步001/003共享合同和本规格/API合同，再绑定既有 `faultList/itemList/verdictBig` 位置。点击后已有 `runId` 时给出受限查询指引，不清除任务、不重发；入口拒绝信息在定时刷新后仍保留。实际页面截图和失败场景对照仍须单独取证，单元测试不能替代T046验收。

2026-09-24后续实施：仅Test模式的WPF宿主可按本地配置启用回环WebView2调试端口，用于对实际嵌入页面发送鼠标事件、采集渲染截图与脱敏HTTP；生产模式不启用。正式API/通知仍是唯一业务事实源，不新增JS业务桥接或客户原型控件。重复点击限制作为已有错误位置的动态提示随状态重绘，不能覆盖后来GET的安全分类；实际A/B页面与退出后证据见 `diagnostic-validation.md`。
具体表达位置须按既有`contracts/prototype-mapping.md`逐页核对；若原型没有容纳原因或操作指引的现有位置，只登记UI映射缺口并申请批准，不增加静态提示文案或新控件。该缺口只限制对应页面展示，不授权前端猜测安全状态。

## 2026-09-24 最新需求与008完整执行对齐

本节依据宪章5.0.0、008最新澄清和用户最新原型授权，优先于前文冲突范围；旧记录保留原日期和范围。全部新增能力尚未实现/验证，当前tasks已追加S0—S5唯一归属任务，旧analysis仅历史，本次只读报告在会话输出。

沿实际build.mjs复制的HTML/JS入口及frontend/src/runtime.js接线，保留WPF/WebView2与既有HTTP/通知/媒体访问。旧“Vue”称谓不作为重写技术栈的依据。先补后端目录items及contextJson/2.0选择引用，再移除legalPreparedRequest和展示中的S1/P01限制，按后端数据组织场景/槽位；前端不生成配方执行计划或F结果。

- S0/S1：现有配方弹窗选用版本、正式启动、状态/最终结果及同页取盘确认；同步003公共F新合同与007心跳/夹具。首条Q01即从页面跑到Final。
- S2/S3：复用同一套选择/查询/媒体绑定，定向覆盖更多检测面、四面后可选独立E姿态及四面3CD＋1AB的必要代表，不穷举面数/排列；翻面消费共同配置Pick/Flip/PutBack及复查的实际阶段，不提供孤立人工确认分支。媒体使用实际对象/面/相机身份，不猜原七格语义。
- S4/S5：补组/整体结果、旋转处置及必要恢复核对/决定；前端确认与真实设备事实分开。

必要变化限[映射合同](contracts/prototype-mapping.md)：原归档和摘要只读，实现副本承接012授权配方弹窗、新016顶部公共位置与异常弹窗及既有动态绑定；更新定向差异清单/实现摘要并核无关区域，不新增页面。配方制作保存及目录读写由012负责，本006消费其共同合同，不建立另一编辑业务路径。

设计检查沿用[008 P01—P13表及阶段计划](../008-recipe-driven-inspection/plan.md)。现有T044当前页面401/403/通知/阶段缺口和T045旧媒体映射结论保持；后续用新实际页面证据验证增量，不因文档或局部测试勾选完成。

当前任务归属与顺序：参见[本功能tasks](tasks.md)文末S0—S5增量及[008任务](../008-recipe-driven-inspection/tasks.md)首批集合。共享实现只登记一个所有者；历史版本/完成证据保留原范围，最新Q/C/F规则不倒填旧任务。
# 008 第五批正式页面验证增量（2026-09-25）

specs/006-frontend-station01-console T048/T049仅沿用已授权的同页配方选用、状态/结果展示和取盘确认，由正式WPF操作Q01再Q02，消费后端真实投影；页面不计算Test高度或改写结果。对应Test映射及受限规则见[008合同](../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)。客户原型归档仍只读。

008自动多面页面增量：在runtime.js既有Test媒体格位内切换同相机不同面/实际测量来源的已提交Ready媒体，显示来源与轮次；Q03目录Restricted期间仅可查看受限原因，正式运行待PLC接口。


## 2026-09-26直接影响

复用既有页面，仅更新runtime.js消费的新阶段、面/实际测量来源、媒体及取盘状态；原型ZIP只读。查询合同见[008接口](../008-recipe-driven-inspection/contracts/api-results.md)，实现归本功能T048/T049，人工/恢复T050/T051局部前置，不阻塞普通自动Q03。

## USR-20260926-D直接共享设计增量

现行故障恢复以[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)为准；旧日期的实现/缺口列表是当时快照，不代表当前能力。数据库提交核验/查询重建不等于从已提交业务边界续跑。正常暂停保留原run；当前配置翻面后的姿态必须实际复查，不把历史人工命令默认面当测得姿态。故障关闭旧轮并真实初始核验后新启动，完整重新公共准备与绑定。

本功能复用原职责：006只绑定既有复位核对区域与启动控件，按后端allowedActions和权限发请求，展示原故障/新run，不新增页面或改变客户原型。

设计前旧故障续跑与P01/P07有冲突；本次合同替换后设计符合P01/P04/P06/P07/P08/P12/P13。实际实现和C07/F5证据仍待后续任务阶段调整与实施；生产未知特殊占用/初始安全范围仅限制对应分支，Test可按既有范围实施。tasks只读，具体唯一归属和依赖见[008计划交接](../008-recipe-driven-inspection/plan-restart-alignment-20260926.md)。

## USR-E页面边界

只复用T048/T049现有目录/状态/结果及诊断查询区域；当前适用配方/Restricted原因由后端给出，页面不自行判断PLC坐标或筛选替代后端准入。完整动作诊断可在既有后端日志/事件查询及采证包查阅，不要求新增页面或逐轴控件。恢复页面T051按USR-D不变。

## RES真实结果展示最小设计（2026-09-26）

006 T049在实际frontend/src/runtime.js的render/refresh和现有数据绑定修复：verdictBig只用当前对象disposition，itemList/paramList/defectList消费项目事实，清除演示值后只填已知数据；不改原型结构或固定文字。T048选用/授权复用，003 T068公开字段＋008 T054持久事实为前置；任务仍未完成，不修改历史勾选。

依据HMI-003、DAT-004、008 FR-016及006 FR-010，唯一设计/验收与任务细化建议见[结果展示交接](../008-recipe-driven-inspection/plan-result-display-alignment-20260926.md)。本增量不新增需求，冻结契约后下一轮细化既有tasks再实现。保留USR-E六问题、四面3CD＋1AB及更多面/额外E的必要代表集合、USR-D子交付链及RST-01/RST-02结论；VirtualPlc延迟模式不推进。宪章7.0.0 P02/P05/P07/P08/P09/P12/P13检查：后端真实事实、身份/保存、原型边界和必要代表验收满足设计约束；没有新平台或全排列测试。设计不代表代码/页面已通过。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

复用runtime已有处置显示位置及后端GET重取；不增加控件或页面。

## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

## 2026-09-27 本机复测结果绑定修正（实现前同步）

按既有RES合同修正runtime.js实际消费：itemList绑定原四列的序号、项目/规则、实测/单位、后端判定；defectList绑定原四列类型/位置/尺寸/判定；paramList复用原参数格。不得用整段诊断串替代所有列。运行阶段/阻断原因在原状态与错误区域显示；无对象/未产生结果按已有空值语义，不伪造质量、置信度或缺陷。

只修改运行绑定，不修改客户原型、既有HTML结构/布局/固定标题；后端API字段与空值语义不变。七格正式业务映射尚无确认，不凭名称相似推定。既有T048/T049结果绑定及必要失败显示验证承接，任务勾选不改；本机RunningF原因必须由同run原始证据确认。

本轮解压包截图复核补充：编号必须单独绑定当前对象id，四个统计值只定位各自统计格，不能通过包含编号的全部num-font集合顺序赋值；配方号不是批次号，parentId不是MES订单，缺失的名称/批次/MES字段不得冒用其他字段。原判定色仅绑定实际OK/NG/Pending状态，不改原型结构。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready必须核本次有效RecipeApplicationReceipt，不能从已有handoff恢复。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

009历史增量沿既有runtime.js/notification-reducer及正式动态绑定保留有效义务；当前另承接012授权配方弹窗及011真实阶段/异常槽号，代码未因文档同步而完成。原型ZIP SHA256=3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0，原只读核对覆盖当时页面；当前实现副本的012配方弹窗及新016顶部公共位置/异常弹窗按已授权映射调整，归档、无关结构/布局/文字/控件/导航保持保护，不新增页面。现有来源区域显示真实executionOrigin，不能硬写Test/Simulated；未知保留未知。新后端元数据不自动获得新页面区域。实现归006范围，由009 T046执行并留接口验收证据，006历史勾选不变；T046未实交前009 T061不得签完整基线。

实施先实际完成本功能共享合同对齐，再经009 T012职责复核，才修改对应共享代码。当前只完成文档接口决定，新增生产/消费能力和真实验收未完成。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

本轮仅确认需求同步，不生成新设计或任务；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
前端独立需求见[public-tray-flow-016合同](contracts/public-tray-flow-016.md)：顶部右上独立示教按钮/弹窗、异常选择及必要状态/结束原因。授权范围保持现有导航/其他控件，原型归档不改。
016通知/轮询取得当前run后先更新决策弹窗，再读取证据和媒体；后台截止仍唯一。
