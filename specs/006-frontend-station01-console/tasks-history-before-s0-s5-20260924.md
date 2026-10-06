# 前端任务清单：第一工位前端操作台与 Windows 桌面宿主

**输入**：`spec.md`、`plan.md`、`research.md`、`data-model.md`、`contracts/`、`quickstart.md`  
**当前适用宪章版本**：3.2.0；既有历史证据保留产生时版本  
**规格范围**：仅 `006-frontend-station01-console`；Web 前端、WPF/WebView2 桌面宿主、后端公开接口消费和软件验证。  
**日期**：2026-09-24  
**原型基线**：`E:\dzk\gaode\原型.zip`，SHA-256 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`。

> T001–T041的原有编号、勾选和历史证据保持原范围；新增T042–T044仅对应007实际前端联调，未执行前保持未勾选。任务不得修改客户原型归档、`001`、`003` 或其他后端规格，不实现 PLC、相机、算法、数据库或独立虚拟下位机。模拟后端/FullSimulation 证据不得写成真机或生产验收结论。

## 执行规则

- 每项任务都必须保留可复核的文件路径、前置依赖、完成条件和验证证据；只有证据实际产生后才能勾选。
- 原型副本只从只读 ZIP 生成并校验哈希；任何 DOM、布局、文字、字段、控件或交互差异都必须停止对应实现并登记变更，不得在代码中绕过。
- 前端和桌面宿主只能消费后端公开 HTTP API、SignalR 通知和 `mediaId`；不得引用后端 Domain/Infrastructure，不得访问 PLC、相机、算法进程或业务数据库。
- FE-C01 至 FE-C06 是合同缺口。缺口任务只更新 006 合同记录或等待外部合同，不得私自新增旁路协议；缺口未解决时实现必须显示受限状态。

## Phase 1：准备与基线

- [X] T001 [P] 建立 `frontend/`、`frontend/src/`、`frontend/tests/`、`desktop/`、`desktop/tests/` 和 `artifacts/frontend/` 目录骨架，并在 `frontend/README.md` 记录本功能边界、禁止的设备/数据库直连和开发命令；证据为目录清单和 README。
- [X] T002 [P] 编写 `frontend/scripts/verify-prototype.ps1`，只读检查 `E:\dzk\gaode\原型.zip` 的 SHA-256、HTML 清单（`a.html`、`data-view.html`、`login.html`）和资源清单；校验失败必须退出非零且不得覆盖归档。
- [X] T003 [P] 为 `frontend/package.json`、`frontend/tsconfig.json`、`frontend/vite.config.ts`（或等价工具链文件）锁定可复现的 Web 构建命令和依赖版本；生产构建不得保留 CDN、Google Fonts 或 `unpkg` 外链。
- [X] T004 [P] 为 `desktop/Gaode.Station01.Desktop.csproj`、`desktop/App.xaml`、`desktop/MainWindow.xaml` 建立 WPF 宿主最小工程，并记录 WebView2 SDK/Runtime 版本选择和 Windows 支持范围；不添加设备控制桥接。
- [X] T005 [P] 建立 `frontend/tests/fixtures/` 与 `frontend/tests/contract/` 的测试夹具约定，在 `frontend/tests/README.md` 标明 Test/Simulation/Production 配置、脱敏要求和证据输出目录。

## Phase 2：接口、原型与宿主门禁（所有用户故事的前置条件）

- [X] T006 对照 `contracts/prototype-mapping.md` 完成三页 DOM、文字、字段、控件、布局和交互顺序核对，输出 `artifacts/frontend/prototype/baseline-report.json` 与截图/DOM证据；发现差异时只更新 `contracts/gaps.md`，不得改原型或继续越过差异实现。
- [X] T007 [P] 将三份确认页面及获准的本地依赖复制到 `frontend/src/pages/` 和 `frontend/src/assets/`，保留页面结构与交互，并让 `frontend/scripts/verify-prototype.ps1` 对构建副本执行哈希/清单校验；不得直接把 ZIP 当运行时可写目录。
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
- [X] T040 执行 `pwsh -File frontend/scripts/verify-prototype.ps1`、`npm ci --prefix frontend`、`npm run typecheck --prefix frontend`、`npm test --prefix frontend`、`dotnet test desktop/tests/Gaode.Station01.Desktop.Tests.csproj`、`pwsh -File desktop/Installer/build.ps1 -Runtime win-x64` 和安装包冒烟验证，覆盖 `quickstart.md` 中原型、API、权限、ETag、SignalR、媒体、FullSimulation、宿主和安装包场景；记录实际命令、输入版本、环境、结果和证据索引至 `artifacts/frontend/verification-index.md`，未执行项必须明确标记。
- [X] T041 核对 `spec.md`、`plan.md`、`tasks.md`、`data-model.md`、`contracts/` 和 `quickstart.md` 的范围、路径、合同缺口与 P12 约束一致性，更新 `specs/006-frontend-station01-console/contracts/gaps.md` 中仍未解决的外部依赖；不得勾选未有实现/测试证据的任务。

## Phase 8：007所需有限前端接线（待实施）

既有T010/T019证明的是旧合同客户端与命令恢复行为，T018/T022/T023证明的是旧状态绑定；其勾选不证明007的实际页面请求、Test凭据或Final展示。以下仅补当前未覆盖工作，前端实现归006，Host侧限定Test来源的API/通知跨源和授权接线归007 T010。

- [X] T042 [US1] 在现有`frontend/src/runtime.js`启动入口及必要的`frontend/src/api/`和宿主受控配置传递处，构造正式`requestId/contextJson/publicConfigRef/budgetRef/simulationRef`；`contextJson`按`station01-start-run-context/1.0`携带Test用途、tray/station/line、S1及合法P01占位，版本引用来自007冻结样本；向HTTP/通知客户端传递受控Test Bearer凭据，不把令牌写入页面资源、日志或请求体角色字段，保留原请求幂等恢复。（来源：006 FR-004/005/007、SC-003；007 FR-001/011；依赖：本次006共享合同、007 Test配置可供消费；完成：原型已有入口不再发送空对象，有/无凭据的正式请求分别得到可解释受理/拒绝，202不显示完成；证据：前端合同测试与`artifacts/frontend/007/start-request.json`脱敏记录。）
- [X] T043 [US2] 在现有前端状态投影和`a.html`/`data-view.html`绑定处补齐公共准备、Detection、Sorting、UnloadPreparation、WholeTrayCompletion、ObservedUnlocked、AwaitingManualRemoval、FinalUnloadCompletion及受限/来源的后端GET展示；通知只触发重取，取盘模拟由007客户端完成，页面不新增控件或伪称真实人工。（来源：006 FR-001/005/006、SC-001/003/004；007 FR-011/012/013；依赖：本次006合同，可与T042并行；完成：原型已有位置与后端快照一致，202/整盘/解锁均不提前显示Final，最终Test/Simulated来源可辨；证据：`artifacts/frontend/007/status-mapping.json`、必要页面对照与状态测试。）
- [ ] T044 [US1/US2] 使用实际WPF/WebView2页面与007 Host验证T042/T043交付：从原型已有入口提交CAP/P01合法请求，核对受控Test凭据、API/通知跨源实际连通及401/403，结合Host已返回的快照核对已有位置的阶段展示；T043验证Final映射，007 T015负责实际前端到FinalUnloadCompletion的整链判定，本任务不等待该整链结果。（来源：006 FR-004–008、SC-003/004；007 FR-001/011、SC-001/005；依赖：T042、T043及007 T010 Host侧跨源/授权可用；完成：实际页面请求、查询/通知与授权可核对，未提交Final时不提前显示，无页面设备/数据库旁路、无原型修改；Host未就绪时如实记Blocked/NotRun，不借旧模拟证据勾选；证据：`artifacts/frontend/007/webview2-host-transcript.json`及原型哈希/页面状态对照。）

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

T006、T009–T012、T014–T015 在 T001–T005 产物就绪后可并行；T016 是历史页面实现前的门禁。T017–T035 在 Phase 2 完成后按用户故事分别并行，US2/US3/US4 不依赖 US1 的实现结果。T036–T039 可并行，但必须依赖各自被测实现；T040–T041 汇总历史适用实现与测试。当前007补充工作中T042/T043可并行，T044等待二者与007 T010的Host侧连通；007后端与虚拟组件不等待T044，007 T015才等待双方实际交付。

## 追溯矩阵

| 任务范围 | 规格/合同来源 | 关键原则 | 主要产物 | 完成证据 |
| --- | --- | --- | --- | --- |
| T001–T016 | spec §1/§4/§5/§6，plan，prototype-mapping，host，gaps FE-C01–06 | P02、P05、P09、P10、P12 | `frontend/`、`desktop/`、`contracts/` | 原型哈希、API合同测试、宿主门禁 |
| T017–T021 | US1、api.md 运行与权限路由、data-model AuthSession/CommandReceipt | P02、P07、P12 | 登录/启动绑定与测试 | Test/Simulation 启动、超时、权限证据 |
| T022–T026 | US2、data-model 状态/媒体、api.md status/media/ETag | P04、P07、P09、P12 | 状态投影、媒体客户端与测试 | 受限状态、ETag/媒体证据 |
| T027–T031 | US3、api.md errors/control、gaps FE-C02 | P02、P04、P07、P12 | UI 状态、错误/控制绑定与测试 | 错误、权限、受限状态证据 |
| T032–T035 | US4、api.md SignalR、data-model NotificationProjection | P06、P07、P09 | 重连协调器与测试 | 乱序/断线/原命令查询证据 |
| T036–T041 | quickstart、host.md、prototype-mapping、P12 | P02、P05、P09、P12 | 安装包、跨页/安全/收尾报告 | Windows 无网启动、全量软件验证索引 |
| T042–T044（当前未勾选） | 006 FR-001/004–008、SC-003/004；007 FR-001/011与T002/T010/T015 | P05、P07、P09、P12/P13 | 实际启动请求/凭据、完整状态、WebView2到Host连通 | 脱敏请求、页面/GET对照、跨源与授权响应；007 T015另判完整闭环 |

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
| T044 | FR-004–008、SC-003/004；007 FR-001/011 | P05/P07/P12/P13 | T042、T043、007 T010 | 实际WebView2到Host的请求/授权/通知与展示；不替代007 T015 |

## 外部依赖与明确不纳入

| 外部项 | 影响任务 | 可继续工作 | 处理方式 |
| --- | --- | --- | --- |
| FE-C01 生产认证合同 | T017、T030 | Test Token、页面投影、合同测试 | 等待认证合同；显示 `AuthUnavailable`，不伪造登录 |
| FE-C02 统一错误合同 | T009、T028、T030 | 兼容解析与受限错误 | 等待后端合同变更；不修改后端实现 |
| FE-C03 SignalR 事件合同 | T011、T032–T034 | GET/ETag 查询和断线状态 | 以 GET 为事实来源，等待事件冻结 |
| FE-C04 媒体元数据合同 | T024、T025 | 已有 `mediaId` 流读取 | 缺元数据显示 Unknown，不读本地路径 |
| FE-C05 宿主API/通知与Test跨源 | 历史T008/T015/T036；当前T042/T044 | 006完成页面配置、受控Test凭据与客户端消费 | 007 T010完成Host侧限定Test来源跨源/授权；T044验证实际连通，生产部署合同不自动成为007门禁 |
| FE-C06 原型 CDN 本地化合同 | T003、T007、T038 | 资源审计和差异记录 | 固定依赖并做视觉/DOM 对照，不改 ZIP |
| 真实 PLC/相机/算法/数据库 | 不阻断前端软件任务；限制真机联调 | FullSimulation、契约和受限状态测试 | 不新增实现任务，不把模拟通过写成真实通过 |

## MVP 建议

历史MVP为T001–T021，T022–T035补齐旧状态、媒体、错误和通知恢复，T036–T041完成旧Windows打包及证据汇总；这些已勾选项仅保留当时范围。当前007最小增量为T042–T044，006完成后交付007 T002消费，007 T015再判定实际前端至FinalUnloadCompletion。所有增量仍遵守原型不可变和后端事实边界。

## 收尾检查

T041保留原有任务格式、用户故事独立测试、范围边界和FE-C01–FE-C06缺口复核的历史完成证据。T042–T044是本次新增未勾选增量，只有实际实现及相应证据产生后才可勾选；007 T015另判完整虚拟闭环。

## 当前媒体展示纠正

- [ ] T045 [US2] 在 `frontend/src/runtime.js` 的原型已有 `camGrid` 图片区，依据后端公开的同 runId 媒体清单及明确的相机位/采集步骤对应字段，将每个已参与且已保存的 `mediaId` 经授权 `/api/v1/station01/media/{mediaId}` 读取并绑定到正确原有图片位；保留原型相机名称和布局，未参与/未采集/未就绪显示受限状态，标注 Test/Simulated，不按数组顺序猜映射、不读取本地路径、不复用一张图填满区域。依赖007 T025交付公开业务采集身份与媒体清单，并先在006原型映射合同确认业务身份到七格的对应；006不实现后端接口或修改其他功能合同。（006 FR-001/004–006/008、SC-001/003/004；依赖：007 T025公开`runId/mediaId/captureId/角色/步骤/业务相机/readiness/source`且006合同已确认原型格位映射；完成：真实 WPF/WebView2 运行中至少一张本次采集图片按正确位置显示，未参与区域不冒充采集，授权媒体 GET、页面截图、runId/mediaId 和构建哈希可互证；证据：`artifacts/frontend/007/media-display.json`、脱敏请求及实际页面截图。）

## 2026-09-24 启动受阻显示增量（未完成）

- [X] T046 [US1/US3] 对应FR-009、SC-005、宪章P09/P12/P13：仅在原型已有错误、状态和提示位置消费脱敏公开错误、运行/设备GET与通知重取，保留`requestId`和已返回的`commandId/runId`；分别展示通信失效无法确认安全、设备可靠反馈明确不安全、配置/存储/授权阻断及未知原因的停止阶段、明确限制与可执行查询或人工核查指引，不自动重发启动、不增加控件或推断设备完成。依赖001 FR-041/003 FR15的后端事实及006现有API合同；完成条件为一次Test首次通信失败与明确不安全对照的实际页面截图、脱敏请求/GET和后端关联一致，页面重开/乱序通知不改写事实，Test/Simulated来源明确。旧前端测试与历史勾选不抵扣本任务；若公开事实不足先同步对应合同和消费者规格。2026-09-24已有实现与运行时测试，实际WPF/WebView2失败场景截图未取得是前轮状态；后续两类真实页面点击、截图、脱敏请求/GET、重复点击与重开证据见 `diagnostic-validation.md`，在Test/VirtualPlc范围完成。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

- [ ] T047 在008目标API实现后核对并绑定原型已有位置的步骤/结果/来源/受限事实，产物frontend/src/runtime.js及本功能contracts/api.md/prototype-mapping.md/gaps.md；依赖008 T046及合法映射，缺位置先记录；本任务先产出两配方页面/GET/通知对照证据，供008 T047最终收集；本任务不依赖008 T047完成，历史T044/T045/T046结论不修改。 追溯：FR-010、宪章3.2.0 P03/P07/P08/P09/P11/P13（006另P12）。

