2026-09-26 USR-20260926-D任务增量：按[双端复位与完整新轮合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)替换活动任务的旧U05单指令故障重发要求；旧证据仅历史适用，暂停/人工继续及合法局部重试保留。编号、勾选不变，新恢复尚未实现/验证。

2026-09-26 人工子范围增量：T050按[人工执行合同](../003-plc-latest-protocol/contracts/manual-test-execution.md)消费既有占用、完成及清零，目标面来源遵守U04；原编号、历史勾选和完成条件不变。

> 当前有效任务义务已按2026-10-03统一决定定向修订；T编号及历史勾选不变。原历史范围/固定样本/原型限制及证据保留当时含义，不能作为新能力已通过；旧任务原文归档仍只读。本文件仅修订已有006任务，不生成012 tasks。

# 前端任务清单：第一工位前端操作台与 Windows 桌面宿主

**012澄清交付时状态（历史）**：2026-10-03，012副本；已按统一确认定向修订本文件有效条款，历史证据及任务勾选保持原范围。仅文档同步，尚未合入主项目或证明实现/验证通过。

T049直接子范围按[api.md](contracts/api.md)查询E实际媒体身份及ECodeBinding问题；复用既有页面区域，不扩页、不指定未确认相机格位。页面验证及原完整条件未齐不得勾整项。

**输入**：`spec.md`、`plan.md`、`research.md`、`data-model.md`、`contracts/`、`quickstart.md`  
**当前适用宪章版本**：3.2.0；既有历史证据保留产生时版本  
**规格范围**：仅 `006-frontend-station01-console`；Web 前端、WPF/WebView2 桌面宿主、后端公开接口消费和软件验证。  
**日期**：2026-09-24  
**原型基线**：`E:\dzk\gaode\原型.zip`，SHA-256 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`。

> T001–T041的原有编号、勾选和历史证据保持原范围；新增T042–T044仅对应007实际前端联调，未执行前保持未勾选。任务不得修改客户原型归档、`001`、`003` 或其他后端规格，不实现 PLC、相机、算法、数据库或独立虚拟下位机。模拟后端/FullSimulation 证据不得写成真机或生产验收结论。

## 执行规则

- 每项任务都必须保留可复核的文件路径、前置依赖、完成条件和验证证据；只有证据实际产生后才能勾选。
- 归档ZIP和摘要只读；012配方弹窗按已授权映射记录定向差异，其他DOM/布局/文字/控件/导航差异仍须拒绝；不得关闭检查器或豁免整页。
- 前端和桌面宿主只能消费后端公开 HTTP API、SignalR 通知和 `mediaId`；不得引用后端 Domain/Infrastructure，不得访问 PLC、相机、算法进程或业务数据库。
- FE-C01 至 FE-C06 是合同缺口。缺口任务只更新 006 合同记录或等待外部合同，不得私自新增旁路协议；缺口未解决时实现必须显示受限状态。

## Phase 1：准备与基线

- [X] T001 [P] 建立 `frontend/`、`frontend/src/`、`frontend/tests/`、`desktop/`、`desktop/tests/` 和 `artifacts/frontend/` 目录骨架，并在 `frontend/README.md` 记录本功能边界、禁止的设备/数据库直连和开发命令；证据为目录清单和 README。
- [X] T002 [P] 编写 `frontend/scripts/verify-prototype.ps1`，只读检查 `E:\dzk\gaode\原型.zip` 的 SHA-256、HTML 清单（`a.html`、`data-view.html`、`login.html`）和资源清单；校验失败必须退出非零且不得覆盖归档。
- [X] T003 [P] 为 `frontend/package.json`、`frontend/tsconfig.json`、`frontend/vite.config.ts`（或等价工具链文件）锁定可复现的 Web 构建命令和依赖版本；生产构建不得保留 CDN、Google Fonts 或 `unpkg` 外链。
- [X] T004 [P] 为 `desktop/Gaode.Station01.Desktop.csproj`、`desktop/App.xaml`、`desktop/MainWindow.xaml` 建立 WPF 宿主最小工程，并记录 WebView2 SDK/Runtime 版本选择和 Windows 支持范围；不添加设备控制桥接。
- [X] T005 [P] 建立 `frontend/tests/fixtures/` 与 `frontend/tests/contract/` 的测试夹具约定，在 `frontend/tests/README.md` 标明 Test/Simulation/Production 配置、脱敏要求和证据输出目录。

## Phase 2：接口、原型与宿主门禁（所有用户故事的前置条件）

- [X] T006 对照 `contracts/prototype-mapping.md` 完成三页 DOM、文字、字段、控件、布局和交互顺序核对，输出 `artifacts/frontend/prototype/baseline-report.json` 与截图/DOM证据；历史T006证据范围保持；后续仅承接012已授权弹窗差异并保留无关区域检查，不改归档或绕过未授权差异。
- [X] T007 [P] 将三份确认页面及获准的本地依赖复制到 `frontend/src/pages/` 和 `frontend/src/assets/`，保留页面结构与交互，后续对实现副本执行归档清单及012授权差异校验；不得直接把 ZIP 当运行时可写目录。
- [X] T008 [P] 在 `frontend/src/config/runtime-config.ts` 定义只读 API 基地址、SignalR 地址、运行模式、资源版本和原型哈希配置；拒绝任意外部 URL、设备地址、数据库连接串和页面注入角色。
- [X] T009 [P] 在 `frontend/src/api/http-client.ts`、`frontend/src/api/error-contract.ts` 和 `frontend/tests/contract/http-contract.test.ts` 实现并测试 401/403/404/409/429/503、匿名 `error/message`、`ErrorContract`/ProblemDetails 的兼容解析；未知响应必须进入受限错误，不能判定成功。
- [X] T010 [P] 在 `frontend/src/api/station01-api.ts` 和 `frontend/tests/contract/station01-api.test.ts` 固化 `runs`、`status`、`commands`、`handoff`、控制、配置校验、reset 和媒体路由的请求/响应映射；所有命令携带唯一 `requestId`，202 只映射为受理。
- [X] T011 [P] 在 `frontend/src/api/signalr-client.ts`、`frontend/src/state/notification-reducer.ts` 和 `frontend/tests/contract/notification-order.test.ts` 实现 `NotificationEnvelope` 的 schema/version 检查、重复/乱序丢弃、按 `runId` 触发 GET；通知不得直接确认物理动作。
- [X] T012 [P] 在 `frontend/src/state/etag-cache.ts` 和 `frontend/tests/contract/etag-recovery.test.ts` 实现 ETag/`If-None-Match`/304、较新 revision 保护和重连后 GET；区分 observed/persisted revision，不以 ETag 哈希大小推断新旧。
- [X] T013 在 `specs/006-frontend-station01-console/contracts/gaps.md` 补充一次实际代码审计结果，逐项确认 FE-C01–FE-C06 的证据、阻塞范围、临时受限行为和待外部合同动作；不得修改后端源码或 001/003 规格。
- [X] T014 [P] 在 `desktop/HostRuntime.cs`、`desktop/WebView2Host.cs` 和 `desktop/tests/HostRuntimeTests.cs` 实现 WebView2 Runtime 检查、`CoreWebView2Environment` 创建、`https://appassets.local/` 虚拟主机映射、默认 `login.html` 导航、导航失败/崩溃/关闭诊断；禁止 `file://`、外部网页和 CDN 导航。
- [X] T015 [P] 在 `desktop/HostConfiguration.cs` 和 `desktop/tests/HostConfigurationTests.cs` 实现只读配置注入（API/Hub、模式、资源版本、原型哈希）及生产环境导航/CSP/调试开关校验；确认不注入 PLC、相机、算法端口、数据库或内部类型。
- [X] T016 执行 `frontend/scripts/verify-prototype.ps1`、Web 构建和宿主启动门禁，记录 `artifacts/frontend/prototype/`、`artifacts/frontend/desktop/` 证据；门禁失败时暂停后续页面实现并在 `contracts/gaps.md` 留痕。

## Phase 3：US1——查看并启动第一工位运行（P1）

**目标**：登录后在确认原型页面中查看运行事实并提交一次启动请求；断线、超时和权限失败均不重复动作或伪造完成。  
**独立完成条件**：使用 Test/Simulation 后端可验证登录受限行为、启动受理、运行查询和原型内状态绑定；不依赖真实设备。

- [X] T017 [P] [US1] 在 `frontend/src/state/auth-session.ts` 和 `frontend/src/pages/login-bindings.ts` 实现 `Anonymous/Authenticating/Authenticated/Rejected/Unavailable` 状态绑定；无生产认证端点时显示 `AuthUnavailable`，不得从输入框推断角色或成功。
- [X] T018 [P] [US1] 在 `frontend/src/pages/run-console-bindings.ts` 将 `a.html` 已有区域绑定到只读状态快照、运行列表/详情和权限结果；不得新增页面、字段、控件或改变交互顺序。
- [X] T019 [US1] 在 `frontend/src/commands/start-run.ts` 实现唯一 `requestId` 的启动请求、超时保留原请求上下文、原 `commandId`/`runId` 查询恢复和受理/完成分离；禁止自动生成第二个请求重发。
- [X] T020 [P] [US1] 在 `frontend/tests/us1/start-run.test.ts` 验证 202 受理、重复点击幂等、网络超时、原命令查询、401/403 和未知回执；断言页面不会显示设备完成或物理停止。
- [X] T021 [US1] 在 `frontend/tests/us1/prototype-console.test.ts` 对 `a.html` 执行页面对照、启动入口和权限可用性测试，输出 `artifacts/frontend/prototype/us1-report.json`；仅验证批准基线，不扩大页面范围。

## Phase 4：US2——观察流程和设备事实（P1）

**目标**：显示公共准备、夹紧、3D、F扫码、算法、保存和移交的后端事实，保留未接入/未就绪/未知状态。  
**独立完成条件**：模拟响应可证明命令受理、状态快照、质量未判定和设备受限状态不会被 UI 合并成成功。

- [X] T022 [P] [US2] 在 `frontend/src/state/status-projection.ts` 实现 `StationStatusProjection` 和 `RunProjection` 的只读映射，保留 `NotIntegrated`、`NotReady`、`Unknown`、`Pending`、`CommitUnknown`、`HandoffNotReady`；禁止补造缺失字段。
- [X] T023 [P] [US2] 在 `frontend/src/pages/status-bindings.ts` 和 `frontend/src/pages/data-view-bindings.ts` 将状态、结果、媒体引用和 ETag 绑定到 `a.html`/`data-view.html` 已有位置；质量未判定或媒体未就绪不得显示 OK/完成。
- [X] T024 [US2] 在 `frontend/src/media/media-client.ts` 和 `frontend/tests/us2/media-contract.test.ts` 实现仅凭后端 `mediaId` 读取、ETag/304、404/未就绪/未知元数据处理；拒绝本地路径、目录路径和任意 URL。
- [X] T025 [P] [US2] 在 `frontend/tests/us2/status-restriction.test.ts` 覆盖相机、算法、PLC、存储和媒体的 FullSimulation 受限状态、算法失败、保存失败与设备断联；证据标明 `Test/Simulated`，不得声称真机验证。
- [X] T026 [US2] 在 `frontend/tests/us2/etag-status.test.ts` 验证首次快照、304、版本变化、查询结果与通知乱序合并；较低 revision 不得覆盖已显示较新快照。

## Phase 5：US3——处理错误和受限状态（P2）

**目标**：展示中文错误、阶段、错误码、trace/version 和受理/完成区别；权限或能力缺失时只提供原型已有的受限表达。  
**独立完成条件**：所有错误场景均保持事实边界，不由前端推断停止、算法成功或数据库提交。

- [X] T027 [P] [US3] 在 `frontend/src/state/ui-state.ts` 实现 `Loading/Ready/Refreshing/Offline/Unauthorized/Forbidden/NotIntegrated/Unknown/Conflict/Error/HostUnavailable` 转换；状态转换不得调用设备动作或写业务数据。
- [X] T028 [P] [US3] 在 `frontend/src/pages/error-bindings.ts` 将 HTTP/合同错误、阶段、错误码、traceId、retryable、currentRevision 和处理结果绑定到原型已有错误区域；原型没有对应表达时只登记 UI 合同缺口。
- [X] T029 [US3] 在 `frontend/src/commands/control-commands.ts` 实现暂停、取消、恢复核对、继续、配置校验和受限 reset 的权限/expectedRevision/requestId 约束；命令受理不映射为物理停止，`manualStartRequired` 必须如实提示。
- [X] T030 [P] [US3] 在 `frontend/tests/us3/errors-permissions.test.ts` 覆盖无凭据、401、403、404、409、429、503、网络错误、权限变化和受限能力；不得通过请求体角色或本地布尔值绕过后端授权。
- [X] T031 [US3] 在 `frontend/tests/us3/restricted-state.test.ts` 验证相机/算法/PLC 未接入或 Unknown 时只显示受限状态，质量/保存/移交未确认时不显示成功；记录 `artifacts/frontend/status/` 证据。

## Phase 6：US4——查询和通知恢复（P2）

**目标**：SignalR 断线、重复、迟到或乱序时有界重连并以查询快照恢复，不重复命令。  
**独立完成条件**：断线恢复、通知降级和查询冲突均可在模拟后端独立复现。

- [X] T032 [P] [US4] 在 `frontend/src/state/recovery-coordinator.ts` 实现 SignalR 断线有界退避、重连后状态/运行/handoff GET、连接失败的 `Offline/HostUnavailable` 和用户可重试提示；不自动重发启动/控制命令。
- [X] T033 [P] [US4] 在 `frontend/src/pages/notification-bindings.ts` 绑定通知重取和断线提示到原型已有区域；`StateChanged`、`DiagnosticChanged`、`HandoffReady` 均只触发查询，不当作物理完成。
- [X] T034 [US4] 在 `frontend/tests/us4/notification-recovery.test.ts` 注入重复、丢失、迟到、乱序通知和慢客户端，验证有界重连、revision 保护、GET 恢复和无重复动作；输出通知日志到 `artifacts/frontend/notifications/`。
- [X] T035 [US4] 在 `frontend/tests/us4/query-command-recovery.test.ts` 验证命令超时后以原 `commandId`/`requestId` 查询、ETag 冲突重新 GET、429 仅对查询有界退避；禁止创建第二个命令。

## Phase 7：Windows 打包、跨页面验证与收尾

- [X] T036 [P] 在 `desktop/Installer/` 配置 Windows 可执行程序打包、静态资源复制、资源版本/原型哈希清单和 WebView2 Runtime 依赖检查；缺 Runtime 必须明确受限退出，不得伪装页面可用。
- [X] T037 [P] 编写 `desktop/tests/WebView2StartupTests.cs`，验证默认登录页、三页导航、虚拟主机资源、资源缺失、Runtime 缺失、无外部网页服务器/无 CDN 网络启动、窗口关闭和宿主崩溃边界。
- [X] T038 [P] 编写 `frontend/tests/prototype-all-pages.test.ts`，逐页核对 `login.html`、`a.html`、`data-view.html` 的 DOM/布局/文字/字段/控件/交互顺序，并将证据写入 `artifacts/frontend/prototype/`；差异只更新合同记录。
- [X] T039 [P] 在 `frontend/tests/security-boundary.test.ts` 和 `desktop/tests/SecurityBoundaryTests.cs` 验证无设备桥、无任意文件读取、无数据库连接、无外部导航、无 CDN 依赖和无日志泄露 access token。
- [X] T040 保留当时原型、API、权限、ETag、通知、媒体、FullSimulation、宿主和打包验证的历史勾选及证据（原命令见只读历史任务归档）。本次不把其全量命令作为必跑清单，只按实际变化执行构建、必要组件回归、原型/架构门禁及与011/012共用的专项证据；漏跑/Skip/失败不能记通过。
- [X] T041 核对 `spec.md`、`plan.md`、`tasks.md`、`data-model.md`、`contracts/` 和 `quickstart.md` 的范围、路径、合同缺口与 P12 约束一致性，更新 `specs/006-frontend-station01-console/contracts/gaps.md` 中仍未解决的外部依赖；不得勾选未有实现/测试证据的任务。

## Phase 8：007所需有限前端接线（待实施）

既有T010/T019证明的是旧合同客户端与命令恢复行为，T018/T022/T023证明的是旧状态绑定；其勾选不证明007的实际页面请求、Test凭据或Final展示。以下仅补当前未覆盖工作，前端实现归006，Host侧限定Test来源的API/通知跨源和授权接线归007 T010。

- [X] T042 [US1] 在现有`frontend/src/runtime.js`启动入口及必要的`frontend/src/api/`和宿主受控配置传递处，构造正式`requestId/contextJson/publicConfigRef/budgetRef/simulationRef`；`contextJson`按`station01-start-run-context/1.0`携带Test用途、tray/station/line、S1及合法P01占位，版本引用来自007冻结样本；向HTTP/通知客户端传递受控Test Bearer凭据，不把令牌写入页面资源、日志或请求体角色字段，保留原请求幂等恢复。（来源：006 FR-004/005/007、SC-003；007 FR-001/011；依赖：本次006共享合同、007 Test配置可供消费；完成：原型已有入口不再发送空对象，有/无凭据的正式请求分别得到可解释受理/拒绝，202不显示完成；证据：前端合同测试与`artifacts/frontend/007/start-request.json`脱敏记录。）
- [X] T043 [US2] 在现有前端状态投影和`a.html`/`data-view.html`绑定处补齐公共准备、Detection、Sorting、UnloadPreparation、WholeTrayCompletion、ObservedUnlocked、AwaitingManualRemoval、FinalUnloadCompletion及受限/来源的后端GET展示；通知只触发重取，取盘模拟由007客户端完成，页面不新增控件或伪称真实人工。（来源：006 FR-001/005/006、SC-001/003/004；007 FR-011/012/013；依赖：本次006合同，可与T042并行；完成：原型已有位置与后端快照一致，202/整盘/解锁均不提前显示Final，最终Test/Simulated来源可辨；证据：`artifacts/frontend/007/status-mapping.json`、必要页面对照与状态测试。）
- [ ] T044 [US1/US2] 使用实际WPF/WebView2页面与007 Host验证T042/T043交付：从原型已有入口提交CAP/P01合法请求，核对受控Test凭据、API/通知跨源实际连通及401/403，结合Host已返回的快照核对已有位置的阶段展示；T043验证Final映射，specs/007-station01-integrated-loop T015负责实际前端到FinalUnloadCompletion的整链判定，本任务不等待该整链结果。（来源：006 FR-004–008、SC-003/004；007 FR-001/011、SC-001/005；依赖：T042、T043及007 T010 Host侧跨源/授权可用；完成：实际页面请求、查询/通知与授权可核对，未提交Final时不提前显示，无页面设备/数据库旁路、无原型修改；Host未就绪时如实记Blocked/NotRun，不借旧模拟证据勾选；证据：`artifacts/frontend/007/webview2-host-transcript.json`及原型哈希/页面状态对照。）

## 依赖关系与并行机会

### 用户故事完成顺序

```text
Phase 1 (T001-T005)
        ↓
Phase 2 (T006-T016：原型/API/宿主门禁)
        ↓
US1 (T017-T021) ─┐
US2 (T022-T026) ──┼─> Phase 7 (T036-T041)
US3 (T027-T031) ──┤
US4 (T032-T035) ─┘
```

T006、T009–T012、T014–T015 在 T001–T005 产物就绪后可并行；T016 是历史页面实现前的门禁。T017–T035 在 Phase 2 完成后按用户故事分别并行，US2/US3/US4 不依赖 US1 的实现结果。T036–T039 可并行，但必须依赖各自被测实现；T040–T041 汇总历史适用实现与测试。当前007补充工作中T042/T043可并行，T044等待二者与007 T010的Host侧连通；007后端与虚拟组件不等待T044，specs/007-station01-integrated-loop T015才等待双方实际交付。

## 追溯矩阵

| 任务范围 | 规格/合同来源 | 关键原则 | 主要产物 | 完成证据 |
| --- | --- | --- | --- | --- |
| T001–T016 | spec §1/§4/§5/§6，plan，prototype-mapping，host，gaps FE-C01–06 | P02、P05、P09、P10、P12 | `frontend/`、`desktop/`、`contracts/` | 原型哈希、API合同测试、宿主门禁 |
| T017–T021 | US1、api.md 运行与权限路由、data-model AuthSession/CommandReceipt | P02、P07、P12 | 登录/启动绑定与测试 | Test/Simulation 启动、超时、权限证据 |
| T022–T026 | US2、data-model 状态/媒体、api.md status/media/ETag | P04、P07、P09、P12 | 状态投影、媒体客户端与测试 | 受限状态、ETag/媒体证据 |
| T027–T031 | US3、api.md errors/control、gaps FE-C02 | P02、P04、P07、P12 | UI 状态、错误/控制绑定与测试 | 错误、权限、受限状态证据 |
| T032–T035 | US4、api.md SignalR、data-model NotificationProjection | P06、P07、P09 | 重连协调器与测试 | 乱序/断线/原命令查询证据 |
| T036–T041 | quickstart、host.md、prototype-mapping、P12 | P02、P05、P09、P12 | 安装包、跨页/安全/收尾报告 | 历史Windows无网启动及当时验证索引；本次受影响最小验证 |
| T042–T044（当前未勾选） | 006 FR-001/004–008、SC-003/004；007 FR-001/011与T002/T010/T015 | P05、P07、P09、P12/P13 | 实际启动请求/凭据、完整状态、WebView2到Host连通 | 脱敏请求、页面/GET对照、跨源与授权响应；specs/007-station01-integrated-loop T015另判完整闭环 |

### 逐任务追溯、依赖与完成证据

| 任务 | 需求/成功标准 | 宪章 | 依赖 | 完成证据 |
| --- | --- | --- | --- | --- |
| T001 | FR-002、FR-003 | P02、P12 | 无 | 目录清单、README |
| T002 | FR-001、SC-001 | P12 | 无 | 哈希/清单脚本非零校验 |
| T003 | FR-003、SC-002 | P10、P12 | T001 | 锁定依赖、无外链构建检查 |
| T004 | FR-002 | P02、P05 | T001 | WPF 工程和版本记录 |
| T005 | FR-008 | P09 | T001 | 测试夹具约定 |
| T006 | FR-001、SC-001 | P12 | T002 | DOM/截图基线报告 |
| T007 | FR-001、FR-003 | P12 | T002、T006 | 构建副本清单和哈希 |
| T008 | FR-004、FR-005 | P02、P05 | T001 | 只读运行配置测试 |
| T009 | FR-005、SC-003 | P02、P09 | T008 | 错误合同测试报告 |
| T010 | FR-005、FR-007、SC-003 | P02、P07 | T008 | API 路由/回执合同测试 |
| T011 | FR-005、FR-007、SC-003 | P06、P07 | T008 | 通知版本和乱序测试 |
| T012 | FR-005、FR-007、SC-003 | P07 | T008 | ETag/304/版本测试 |
| T013 | FR-005 | P10、P12 | T009–T012 | FE-C01–FE-C06 审计记录 |
| T014 | FR-002、FR-003、SC-002 | P02、P05 | T004、T007 | 宿主启动单测/日志 |
| T015 | FR-002、FR-004 | P02、P05 | T004 | 配置注入和安全边界测试 |
| T016 | FR-001、FR-002、SC-001、SC-002 | P12 | T006、T014、T015 | 门禁命令和证据目录 |
| T017 | FR-005、SC-003 | P02、P07 | T009 | AuthSession 状态测试 |
| T018 | FR-001、FR-005 | P07、P12 | T006、T010 | `a.html` 绑定对照报告 |
| T019 | FR-007、SC-003 | P07 | T010 | 唯一 requestId/原命令查询测试 |
| T020 | FR-007、SC-003 | P07 | T019 | 启动异常测试结果 |
| T021 | FR-001、SC-001 | P12 | T018 | 第一工位页面对照报告 |
| T022 | FR-006、SC-003 | P04、P07 | T010、T012 | 状态投影单测 |
| T023 | FR-001、FR-006 | P04、P12 | T006、T022 | 两页绑定和受限显示证据 |
| T024 | FR-005、FR-006、SC-003 | P08、P12 | T010 | 媒体合同测试 |
| T025 | FR-006、SC-003、SC-004 | P04、P09 | T022 | Simulation 受限状态报告 |
| T026 | FR-005、FR-006、SC-003 | P07 | T012、T022 | 状态 ETag 合并测试 |
| T027 | FR-006 | P04、P07 | T022 | UI 状态转换测试 |
| T028 | FR-005、FR-006、SC-003 | P02、P12 | T009、T022 | 错误区域绑定证据 |
| T029 | FR-005、FR-007、SC-003 | P02、P07 | T010、T022 | 控制命令权限/版本测试 |
| T030 | FR-005、FR-006、SC-003 | P02、P07 | T028、T029 | 权限/错误场景报告 |
| T031 | FR-006、SC-003、SC-004 | P04、P09 | T022 | 受限状态证据 |
| T032 | FR-005、FR-007、SC-003 | P06、P07 | T011、T012 | 重连协调器测试 |
| T033 | FR-001、FR-005 | P07、P12 | T011、T018 | 通知绑定对照证据 |
| T034 | FR-005、FR-007、SC-003 | P06、P07 | T032 | 通知日志和乱序测试 |
| T035 | FR-005、FR-007、SC-003 | P07 | T019、T032 | 原命令恢复报告 |
| T036 | FR-002、FR-003、SC-002 | P02、P05、P12 | T014、T015 | 安装包清单和 Runtime 检查 |
| T037 | FR-002、SC-002 | P02、P12 | T014 | WebView2 启动测试 |
| T038 | FR-001、SC-001 | P12 | T006、T007 | 三页 DOM/截图报告 |
| T039 | FR-004、SC-003 | P02、P05、P12 | T008、T014 | 安全边界测试 |
| T040 | FR-008、SC-003、SC-004 | P09、P12 | T020、T025、T030、T034、T037 | 验证索引和未执行项清单 |
| T041 | FR-001–FR-008、SC-001–SC-004 | P10、P12 | T040 | 规格/计划/任务/合同一致性报告 |
| T042 | FR-004/005/007、SC-003；007 FR-001/011 | P05/P07/P12 | 007冻结Test配置 | 合法上下文/版本引用、受控凭据与脱敏请求 |
| T043 | FR-001/005/006、SC-001/003/004；007 FR-011/012/013 | P07/P09/P12 | 本次006合同；与T042并行 | 既有位置与GET一致，Final不提前展示 |
| T044 | FR-004–008、SC-003/004；007 FR-001/011 | P05/P07/P12/P13 | T042、T043、specs/007-station01-integrated-loop T010 | 实际WebView2到Host的请求/授权/通知与展示；不替代007 T015 |

## 外部依赖与明确不纳入

| 外部项 | 影响任务 | 可继续工作 | 处理方式 |
| --- | --- | --- | --- |
| FE-C01 生产认证合同 | T017、T030 | Test Token、页面投影、合同测试 | 等待认证合同；显示 `AuthUnavailable`，不伪造登录 |
| FE-C02 统一错误合同 | T009、T028、T030 | 兼容解析与受限错误 | 等待后端合同变更；不修改后端实现 |
| FE-C03 SignalR 事件合同 | T011、T032–T034 | GET/ETag 查询和断线状态 | 以 GET 为事实来源，等待事件冻结 |
| FE-C04 媒体元数据合同 | T024、T025 | 已有 `mediaId` 流读取 | 缺元数据显示 Unknown，不读本地路径 |
| FE-C05 宿主API/通知与Test跨源 | 历史T008/T015/T036；当前T042/T044 | 006完成页面配置、受控Test凭据与客户端消费 | specs/007-station01-integrated-loop T010完成Host侧限定Test来源跨源/授权；T044验证实际连通，生产部署合同不自动成为007门禁 |
| FE-C06 原型 CDN 本地化合同 | T003、T007、T038 | 资源审计和差异记录 | 固定依赖并做视觉/DOM 对照，不改 ZIP |
| 真实 PLC/相机/算法/数据库 | 不阻断前端软件任务；限制真机联调 | FullSimulation、契约和受限状态测试 | 不新增实现任务，不把模拟通过写成真实通过 |

## MVP 建议

历史MVP为T001–T021，T022–T035补齐旧状态、媒体、错误和通知恢复，T036–T041完成旧Windows打包及证据汇总；这些已勾选项仅保留当时范围。当前007最小增量为T042–T044，006完成后交付007 T002消费，specs/007-station01-integrated-loop T015再判定实际前端至FinalUnloadCompletion。所有增量保持归档不可变；实现副本只允许012授权弹窗差异，其余只绑定后端事实。

## 收尾检查

T041保留原有任务格式、用户故事独立测试、范围边界和FE-C01–FE-C06缺口复核的历史完成证据。T042–T044是本次新增未勾选增量，只有实际实现及相应证据产生后才可勾选；specs/007-station01-integrated-loop T015另判完整虚拟闭环。

## 当前媒体展示纠正

- [ ] T045 [US2] 在 `frontend/src/runtime.js` 的原型已有 `camGrid` 图片区，依据后端公开的同 runId 媒体清单及明确的相机位/采集步骤对应字段，将每个已参与且已保存的 `mediaId` 经授权 `/api/v1/station01/media/{mediaId}` 读取并绑定到正确原有图片位；保留原型相机名称和布局，未参与/未采集/未就绪显示受限状态，标注 Test/Simulated，不按数组顺序猜映射、不读取本地路径、不复用一张图填满区域。依赖007 T025交付公开业务采集身份与媒体清单，并先在006原型映射合同确认业务身份到七格的对应；006不实现后端接口或修改其他功能合同。（006 FR-001/004–006/008、SC-001/003/004；依赖：specs/007-station01-integrated-loop T025公开`runId/mediaId/captureId/角色/步骤/业务相机/readiness/source`且006合同已确认原型格位映射；完成：真实 WPF/WebView2 运行中至少一张本次采集图片按正确位置显示，未参与区域不冒充采集，授权媒体 GET、页面截图、runId/mediaId 和构建哈希可互证；证据：`artifacts/frontend/007/media-display.json`、脱敏请求及实际页面截图。）

## 2026-09-24 启动受阻显示增量（未完成）

- [X] T046 [US1/US3] 对应FR-009、SC-005、宪章P09/P12/P13：仅在原型已有错误、状态和提示位置消费脱敏公开错误、运行/设备GET与通知重取，保留`requestId`和已返回的`commandId/runId`；分别展示通信失效无法确认安全、设备可靠反馈明确不安全、配置/存储/授权阻断及未知原因的停止阶段、明确限制与可执行查询或人工核查指引，不自动重发启动、不增加控件或推断设备完成。依赖001 FR-041/003 FR15的后端事实及006现有API合同；完成条件为一次Test首次通信失败与明确不安全对照的实际页面截图、脱敏请求/GET和后端关联一致，页面重开/乱序通知不改写事实，Test/Simulated来源明确。旧前端测试与历史勾选不抵扣本任务；若公开事实不足先同步对应合同和消费者规格。2026-09-24已有实现与运行时测试，实际WPF/WebView2失败场景截图未取得是前轮状态；后续两类真实页面点击、截图、脱敏请求/GET、重复点击与重开证据见 `diagnostic-validation.md`，在Test/VirtualPlc范围完成。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

> T047 已由下方当前增量替换；原编号、未完成状态及全文见 tasks-history-before-s0-s5-20260924.md，不作为当前实现任务。


当前012协作义务：T048目录/选用消费012真实保存内容及共同校验，F内容=料盘编号且不同配方不可共码，后续F用新保存内容，已冻结运行不变；T049在已有区域绑定011真实阶段、更多检测面/独立E身份、三区处置及异常物理槽号，不自行编排。与012/011共用保存/重读/扫码/运行隔离及工艺代表证据，不增加全量配方测试。

实施清理义务归对应被替代代码的既有责任任务：T048/T049核调用、装配、配置和脚本消费者后，实际删除无有效用途的旧分支、演示保存/测试特权、错误状态旁路、失效配置/测试及孤立代码；保留有效保存、关联、取消、期限和历史读取。不用注释、永久开关、备用实现或额外兼容层替代删除，不因测试失败直接删断言，历史失败证据只读。本轮不改代码或勾选。

## S0—S5任务增量（2026-09-24，宪章5.0.0）

所属功能：`specs/006-frontend-station01-console`。跨功能依赖写作目录简称+任务ID，完整目录见008 tasks映射表。原则P03/P04/P05/P07/P08/P09/P11/P13，前端另P12及用户最小原型授权；新增任务全部未完成。旧T047被以下任务替换，未受影响的历史待办不取消；公共验证只做必要正常/失败，不构成交叉穷举。

- [ ] T048 [US1] S0/S1在`frontend/src/runtime.js`、`frontend/src/pages/a.html`及实际`frontend/scripts/build.mjs`链绑定catalog.items、型号/配方/版本选用及contextJson/2.0；移除S1/P01硬编码，修正当前401/403误报未知、202短暂404和Blocked阶段显示，通知按revision重取。依赖：specs/003-plc-latest-protocol T068、specs/007-station01-integrated-loop T031；遵守当前prototype-mapping只读边界，核对ZIP不变摘要及授权弹窗/已有控件绑定，输出本次独立证据；旧`specs/006-frontend-station01-console/evidence/008-prototype-delta.md`仅只读引用，新建/读取/编辑保存由012唯一负责并在同一弹窗消费，006不建立第二路径、不新增调试页。必要验证：真实WPF选用产生正式请求、权限拒绝无运行、通知帧/查询一致；008 FR-001/016/018、Q全体；旧T044验收范围保留，新链证据不自动勾旧任务。 USR-E增量：目录和Restricted原因消费003 T068/002 T11，退出四面版本不可页面启动且后端直接入口同拒绝，不仅前端过滤。复用007 T033的实际runtime.js/src→dist/桌面摘要；按本次原型差异合同用新证据验合法选择、退出拒绝及请求一致，运行显示不扩控件；配方弹窗按012授权映射承接，历史完成能力仅在适用范围复用。

- [ ] T049 [US1] S1在`frontend/src/runtime.js`、`frontend/src/pages/a.html`、`frontend/src/pages/data-view.html`绑定已有同页取盘确认及后端真实结果/媒体选择预览，依allowedActions启用，展示期望/F绑定/执行/保存版本和来源、质量/完整性/处置/Final分离；不猜旧七格映射。依赖：T048选用/授权与003 T068公开查询所用子能力；结果投影/实际联调依008 T054已提交事实子能力，取盘联调依003 T069已交付结束子能力；不等待上述父任务全部勾选。交付：正式页面点击确认、结果/媒体与SQLite及最终提交一致的`specs/006-frontend-station01-console/evidence/008-first-route-ui.md`；未解锁/未提交不可显示完成。对应FR-010/011、008 FR-011/016、Q01/C06/F4/F6；完整Q01判定归008 T055。 USR-E增量：既有结果/诊断区域只消费003 T068/008 T054实际事实及引用，完整动作证据可由现有后端日志/API查阅，不新增逐轴页面；目标不作反馈。以本次独立证据验Unknown/保存未决/未解锁无Final及历史run可查，所用子能力可先供恢复，不等全部Q。
  - RES真实结果绑定子交付（FR-010/011、008 FR-016、HMI-003/DAT-004、P05/P07/P08/P12）：实际入口为`frontend/src/runtime.js`的render/refresh/通知及既有媒体切换，按[逐字段原型映射](contracts/prototype-mapping.md#res真实结果字段映射2026-09-26设计未实施)和[API消费](contracts/api.md#res查询消费与页面生命周期2026-09-26)绑定`a.html`/`data-view.html`已有动态区域；固定标题、列名、结构、布局、控件、交互及原型ZIP保持。复用T048选用/授权、003 T068结果投影和008 T054保存子交付；取盘部分仍消费003 T069，不要求这些父任务全部勾选才开始结果绑定。
  - RES字段绑定：#verdictBig仅当前焦点对象的后端disposition，不能将Final写成“完成”、quality当判定、算法Success当OK，也不能固定OK。当前kind/id/parent、面/step/项目与媒体明确；#itemList显示实际单图/融合/项目及实测/规则/原因，#paramList显示实际参数值/单位/版本及RequestedCapture或AlgorithmConfig，#defectList显示已有类型/位置/尺寸/原因。对象置信度、缺陷数、检测时间/用时仅有对象级事实才填；缺陷坐标未知不画标注，不平均单图置信度或按NG面数数缺陷。未提供/不适用/不可查询/尚无结果/待保存分别按合同显示，清除原型演示值后不以旧数据填空；已提交结果无需等Final，流程/完整性/物理处置/来源/事实质量及整盘Final在已有对应区域分开。
  - RES当前对象与刷新：默认消费resultContext，不取results首尾猜对象；已有媒体切换只在后端同run身份明确时更新焦点，切面不把单图质量替代对象质量，Group/Member、Assembly/Part不能混用。不新增选择控件或前端重算质量。通知只触发GET，定时刷新沿原机制按run/焦点/查询代次串行合并，旧revision/旧run回包不覆盖新焦点，resultRevision仅判变化，304只复用同run同版本。页面刷新/重开先GET，切run清旧身份/结果/图片；查询失败明确不可用，不直读SQLite、媒体文件或算法进程。
  - RES-04资源子证据：复用007 T033现有manifest和`scripts/verify-q01-q02-test-page.ps1`/`scripts/wait-008-page-batch.ps1`/采证工具，不重建平台。沿`frontend/scripts/build.mjs`实际src→dist与桌面打包入口，采`desktop/HostRuntime.cs`的本次ResourcesResolved.resourceRoot/runtimeSha256及WebView版本，WPF EXE/Host DLL路径、构建摘要、PID/启动和实际配置/配方摘要（脱敏）。按GAODE_FRONTEND_DIST→EXE旁frontend/dist→开发回退解析，核实际加载runtime.js、HTML及脚本引用；不同摘要不能单独判旧版本，只验src不能判页面已更新，同次直接复制产物的一致性须结合构建与加载记录解释。工具准备不等本任务全项，页面采证才消费本子能力，避免与007 T033整任务互等。
  - RES最少验收/证据：先在`frontend/tests/us1/runtime-007.test.ts`、`frontend/tests/us2/etag-status.test.ts`及必要现有runtime测试验证上述绑定/同run焦点/重取，再通过正式WPF既有路线完成最少OK、NG、Pending代表对账；OK沿008 T055，NG/Pending沿T059可复用合法同批既有路线，不限定新增Q组合。质量区等于同run同对象API及持久结论，NG/Pending在Final后仍真实，适用明细准确且缺失不伪造，刷新/重开/已有对象切换不串结果，已提交面可在Detection结束前显示；原allowedActions、保存/解锁/取盘/Final操作保持。本次新增独立证据记录并引用只读历史`specs/006-frontend-station01-console/evidence/008-first-route-ui.md`，关联页面DOM/截图/操作、API与持久引用、实际资源和构建摘要；同一独立新包供008路线复用，旧FinalPageDisplayed只保留原流程结论。组件通过不抵正式页面，页面子交付通过不勾T049全项，不重复跑另一套整链。当前代表同时核OK分拣不搬、NG/Pending各自目标及姿态异常独立退出/物理槽号；更多面与可选独立E姿态消费011真实业务身份。


- [ ] T050 [US1] 历史人工换面提交子范围已被当前配置Pick/Flip/PutBack及实际姿态复查取代。原编号与勾选保留；当前活动消费者撤销由012 T023承接：删除runtime.js的ConfirmManualFlip提示/许可/提交，保已有DOM、有效取盘/恢复及历史JSON读取，不再新增manual-flip-confirmations或用命令面充当实测。证据引用012稳定清理批与共同011配置翻放/姿态复查证明，不重跑旧人工正例或完整链。

- [ ] T051 [US1] 在`frontend/src/runtime.js`消费003恢复查询/接口，复用`frontend/src/pages/a.html`既有复位/原因/状态区域与启动控件及`frontend/scripts/build.mjs`实际打包链；按USR-D绑定“复位→核验→显式新启动”，不改变原型、扩页或新增控件。依赖T048/T049已交付选用/查询/取盘能力、003 T072-B与008 T068业务子交付；不等待008 T069先通过。替换故障RecoveryCheckAndContinue→continue接线：Recovery.Check仅允许复位核验，具有Run.Start且后端RestartFullRun可用时才显式提交新requestId及restartFrom；收到新receipt后查询新run，既有诊断区保留旧故障及双向关联，未解锁/未保存不可取盘或显示Final。暂停continue保持原run；T050历史人工换面提交已由012 T023消费者清理撤销，不借故障核验或命令默认面替代实际姿态。必要组件/接口验证及实际WPF子操作记录在`specs/006-frontend-station01-console/evidence/008-recovery-ui.md`，主链正式页面选用、复位、核验、启动和取盘事实引用008 T069同一新包，不后台代操作、不另跑重复整链；错误权限/初始不足/陈旧状态明确受限。命令回执和错误可关联Host持久日志，页面不自造成功/日志事实。对应FR-011、008 FR-012/014/016、C07/F5，当前未实现的新条件全部满足后才勾选。 USR-E增量：恢复使用的当前目录与动作先按USR-E必要代表核验，仍T072-B→本任务→008 T069；不等全四面变体或生产取放窗口。旧Q/旧恢复Passed不抵新run，复用T048/T049不重复页面平台。

> 旧协议历史检查点（不代表新版状态）：008自动多面T048/T049子范围：复用已有目录限制显示与启动门禁；T049补同run媒体面/轮选择及实际结果显示，构建/组件可先验，WPF Q03仍须目录Available并走真实完整链。原整项条件/勾选不变。


## 2026-09-26新版协议增量子范围（未实施）

既有编号和勾选只证明原范围，本表所有新版子范围均NotRun；实现前置按所需子能力交付，整项验收仍保留原未齐项。输入为唯一分区协议及008 execution/3.0；日志须可按run/step/operation/实体/面/连接代次追踪意图、派发、反馈、ACK清零、保存及失败。

| 原任务/新版子范围 | 具体消费者（文件简称按原任务路径） | 输入、前置、完成条件及最少验证 |
| --- | --- | --- |
| specs/006-frontend-station01-console T048 / 20260925协议 | `frontend/src/runtime.js`、`frontend/scripts/build.mjs`实际入口 | 前置003 T068新查询；只绑定既有页面配方/阶段/状态，面2不显示假测量轮2，保持原型ZIP不变；验证真实构建入口与查询一致。 |
| specs/006-frontend-station01-console T049 / 20260925协议 | `frontend/src/runtime.js`、结果与媒体API绑定 | 前置003 T068/T069所用能力及008保存；展示逐面媒体、初始测量来源、目标配置和适用分拣完成后下料；未解锁不得启用取盘，正式点击及Final与保存一致。 |
| specs/006-frontend-station01-console T050历史 / 当前012 T023 | `frontend/src/runtime.js`孤立人工确认消费删除 | 2026-10-04已核共同服务无生产调用；当前配置翻放/姿态复查由011提供，保人工区安全阻断。原编号/勾选不变，不实施旧提交正例。 |
| specs/006-frontend-station01-console T051 / 20260925协议 | `frontend/src/runtime.js`恢复状态绑定 | 前置003 T072-B与008 T068实际事实；复位/初始核验后既有启动控件显式新run；故障不走continue。T050旧人工提交不再活动；未知生产状态仅限制对应分支。 |

## USR-20260926-D任务执行边界

本次仅增量任务对齐，来源为008 [计划交接](../008-recipe-driven-inspection/plan-restart-alignment-20260926.md)及[任务对齐记录](../008-recipe-driven-inspection/tasks-restart-alignment-20260926.md)。仅本轮修改的未完成任务承接新规则；已有已勾任务和历史证据保持原适用时期，不可抵扣新恢复。普通幂等、未触发故障的合法有限重试和正常暂停不得误删；原人工换面保护要求仅保其历史时点，现已核无生产调用的活动分支由012 T023删除，实际人工区/姿态/安全保护仍保留。

执行按子交付：003 T072-A设备观察/复位隔离与001 T078持久基础可分别准备（共享文件修改须协调）；001 T052→008 T068业务→003 T072-B API→006 T051既有页面→008 T069唯一C07/F5页面包→008 T070汇总。T072-M人工与上述A/B独立；不等待无关父任务全勾。001 T070只做普通控制路由，不另建故障API。设备/worker/页面实跑串行；非阻塞边界登记，不增加全配方×全故障矩阵。详细子交付输入、证据和局部限制见任务对齐记录。


## USR-E当前依赖与完成口径（2026-09-26）

依据宪章7.0.0，完整归属/验收见[本轮任务交接](../008-recipe-driven-inspection/tasks-six-issues-alignment-20260926.md)。两端协议/诊断子能力＋当前配方准入/目录→必要代表性协议及正式路线验证→USR-D完整新轮。003 T072-A＋001 T078→001 T052→008 T068（复用008 T054及003 T069）→003 T072-B→006 T051→008 T069→008 T070；A/B/M分子交付，003 T069不反向等008 T068，不等全部Q/C/F或特殊生产。共享源码按文件串行交接，设备/worker/页面串行采证；子交付不勾父任务。问题1—5根因待实际包核验，状态2/3实时Z不强制等于取放目标Z；生产采样窗口只局部限制。历史编号/勾选/证据保持；本次只定向修订有效正文，旧Q/旧恢复Passed不抵新验收。

## 2026-09-27 已提交物理处置投影补缺（既有范围，代码待本冻结批次结束后实施）

实际WPF与已退出SQLite已证处置事实存在但运行投影缺失，见 .specify/bugs/008-disposition-projection/assessment.md。按008 [既有API处置合同](../008-recipe-driven-inspection/contracts/api-results.md) 的2026-09-27细化接入：预留/在途/可靠完成/未知/明确无需搬运分开，物理实体身份与冻结版本一致，缺事实仍为空。resultRevision/ETag含处置事实；复用已有提交和页面字段，不加运动、状态库、控件或期限，不补写旧库。当前冻结批次先完成，再改代码和作必要复验。

原T049结果展示/刷新子能力验同run后端处置字段；T050历史人工提交当前已撤销，T051故障独立；父任务不因本字段勾选。

## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

## 2026-09-27 本机复测结果绑定修正（实现前同步）

按既有RES合同修正runtime.js实际消费：itemList绑定原四列的序号、项目/规则、实测/单位、后端判定；defectList绑定原四列类型/位置/尺寸/判定；paramList复用原参数格。不得用整段诊断串替代所有列。运行阶段/阻断原因在原状态与错误区域显示；无对象/未产生结果按已有空值语义，不伪造质量、置信度或缺陷。

只修改运行绑定，不修改客户原型、既有HTML结构/布局/固定标题；后端API字段与空值语义不变。七格正式业务映射尚无确认，不凭名称相似推定。既有T048/T049结果绑定及必要失败显示验证承接，任务勾选不改；本机RunningF原因必须由同run原始证据确认。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次增量唯一代码/验证归属009 T043—T047/T050（006绑定T046）；相关任务直接或传递依赖本次实际对齐和009 T012。保留本功能全部原任务ID和勾选，不以父任务历史完成抵扣。

发布s01-status/2.0、设备事实device-semantics/1；run/evidence显式deviceSchemaVersion。run.state传输及resultSchemaVersion=station01-result-display/1.0不变。移除raw业务字段而不保留影子兼容。诊断查询GET /api/v1/station01/diagnostics/communication/{evidenceId}沿Read授权只读已提交记录；opaque引用不能被业务解析。历史原payload/来源保持，未存raw、观察ID或回执为null/NotRecorded。当前Bound/Ready必须核本次有效RecipeApplicationReceipt，不能从已有handoff恢复。

NotificationEnvelope版本s01/notification/2.0，eventType/runId/revision/persistedRevision/changedFields/occurredAt保留；summary仅{executionState:string,wholeTaskState:string,errorCode:string?}或null，禁止完整RunSnapshot/raw。通知只触发GET对账，不授权动作、不作为真实提交证据；changedFields仅业务路径。frontend/src/state/notification-reducer.ts按对象类型消费，不保留旧summary:string。

009历史增量沿既有runtime.js/notification-reducer及正式动态绑定保留有效义务；当前另承接012授权配方弹窗及011真实阶段/异常槽号，代码未因文档同步而完成。原型ZIP SHA256=3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0，原只读核对覆盖当时页面；当前实现副本的012配方弹窗及新016顶部公共位置/异常弹窗按已授权映射调整，归档、无关结构/布局/文字/控件/导航保持保护，不新增页面。现有来源区域显示真实executionOrigin，不能硬写Test/Simulated；未知保留未知。新后端元数据不自动获得新页面区域。实现归006范围，由009 T046执行并留接口验收证据，006历史勾选不变；T046未实交前009 T061不得签完整基线。

完成证据按009相应任务、固定案例和消费者交付；未运行部分不得报告通过。

### 009 Host汇总与执行来源（实施前定向细化，2026-10-02）

依据009 FR-016/020—022、E04及已对齐组件矩阵合同，由Codex实际执行/复核。ResultSource在既有值末尾增加HostDerived，仅用于Host汇总事实；旧值与历史原文不变。WholeTrayCompleted、FinalUnloadCompleted的事件来源为HostDerived、质量Derived，保持完整组件矩阵和各实际provider，不能将混合来源压成Real/Simulated。独立配方应用业务保存也是HostDerived/Derived，实际设备执行来源仍由同次DeviceEvidence提供。

ManualTrayRemovalConfirmed单独保留本次操作者事实：明确Test来源记Test/Derived，AuthenticatedHuman记Real/Measured；Final的Host汇总不沿用操作者或某一设备来源。此项不批准Production、不新增业务输入/页面/恢复流程，006只绑定既有来源区域实际值，不改变结构/静态文字/控件。新枚举值是记录来源分类而非PLC数值映射。实施归009 T043—T047，运行证据仍单独取得；本段不勾选其他功能历史任务，不冒称他人批准。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

本轮仅确认需求同步，不生成新设计或任务；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
前端独立需求见[public-tray-flow-016合同](contracts/public-tray-flow-016.md)：顶部右上独立示教按钮/弹窗、异常选择及必要状态/结束原因。授权范围保持现有导航/其他控件，原型归档不改。

- [ ] T016-I01 定向同步与消费本功能直接相关公共配置/观察/处置/下料边界，产物以新016 tasks T002及对应共同代码任务追踪；原历史编号和勾选不改。
T016-I02 决策展示及时性与刷新原decisionId/deadlineUtc验证；不增加页面或选项。
