# 功能任务清单：正式桌面与页面的受控设备联调入口

**输入**：[spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[contracts](contracts/identity-host.md)、[quickstart.md](quickstart.md)  
**宪章版本**：9.0.0  
**日期**：2026-10-08  
**规格范围**：既有桌面/登录进入→人工配方读存/选择→正式启动与实际状态/媒体→人工取盘及持久Final；依赖现场条件时准确阻断。真实PLC/七机，算法/外部光源为可替换虚拟实现；不另建执行流程。  
**当前状态**：软件实施进行中；45项中T001–T010及T038/T039/T041/T042/T043/T044/T045已完成其限定范围，其他任务保持逐项原状态；各故事完整验收另计。C1/I1已只读复核关闭；已有部分后台身份代码和定向测试，不代表桌面或流程验收通过。

实际分支仍为020-real-device-commissioning，feature.json定位021。沿现目录及工具，不初始化/升级，不覆盖已有修改/回退标签。历史任务编写阶段限制保留原记录；当前已授权本机打包、独立安装及离线验证，不连接硬件、不提交推送。requirements保持只读。

## 拆解规则

每项以当前FR/SC和P原则为依据，表中给出前置及可检查证据；只凭实际完成勾选。共享接口先完成T002/T003文档同步，后改代码。所有V01–V06为规格要求的最小验证，不作全组合测试或推测性抽象。待现场值只限制依赖动作，不阻断独立软件任务。

用户报告PLC和相机在本机已能连接交互，正式流程尚未测，之前成功的是虚拟上位机工具。这是现场状态输入，不是021验收证据，不关闭020 T055/T056。安全虚拟算法值由用户后续提供，离线测试值不能用于真实运动。

## 当前阶段范围与完成证据（P13）

| 项目 | 对应规格位置 | 任务或证据 |
| --- | --- | --- |
| 起点与终点 | US1–US4及P13边界表 | T007–T028；身份确认至持久Final或准确拒绝 |
| 必须参与的组件 | 正式桌面、三页、API、配方、SQLite/媒体及020执行端口 | T004/T005、各故事实现/实测；无设备直控旁路 |
| 必要验证 | V01–V06及SC-001–006 | T011/T015/T021/T027/T028、T029/T030 |
| 完成证据 | 实际交互、版本/请求、保存/重读、运动采集请求及持久诊断 | evidence/v01–v06记录、validation.md及源码摘要 |
| 延期项 | 现场正常链/真实算法灯、账号体系、历史平台、发布及完整异常矩阵 | 本文外部依赖/延期；不作为独立软件全局门禁 |

## Phase 1：必要准备

不初始化工程。T001–T003已完成基线与共享合同同步，证据见validation.md及source-state-before.json；后续仅凭实际证据勾选。

- [x] T001 实施前重核feature.json、实际020分支、021有效文档、020历史验证和原型摘要，登记工作区与标签、源码/证据基线及用户报告的连通状态到 specs/021-commissioning-console/evidence/source-state-before.json 和 validation.md；区分用户报告、旧证据与本功能未验证，不安装/初始化/升级。
- [x] T002 [P] 按021变更登记同步 specs/006-frontend-station01-console/contracts/host.md、api.md、prototype-mapping.md、public-tray-flow-016.md，登记新用途/身份、原请求查询、Final释放/准入、媒体七格；闭合此次映射未决，保留旧用途及历史证据。
- [x] T003 [P] 同步 specs/012-recipe-authoring/contracts/editor-ui.md、recipe-authoring-api.md、shared-integration.md 的021消费者及实际必要差异，并在 specs/021-commissioning-console/contracts/shared-change-register.md 登记；不补造旧spec/plan/tasks或改020任务。

## Phase 2：共同验证基础

仅建立本功能真实页面/宿主验证必需的隔离夹具和精确原型保护，不引入产品测试旁路。T005探针源码/构建与需要交互会话/Runtime的实际执行分开；不能因探针暂时不能运行阻止T006和后续独立实现。

- [x] T004 [P] 创建 backend/tests/Gaode.CommissioningUiFixture/Gaode.CommissioningUiFixture.csproj 和 Program.cs，复用现正式Host入口、loopback协议夹具及隔离SQLite/媒体，提供V01–V06所需身份/实际配方目录/故障注入/观测；为V06建立专属Commissioning Run与实际离线采集/提交链，经真实SQLite/媒体及正式受认证GET供页面读取，来源/请求逐项留证，不直写伪造目录或成功记录；新用途媒体组件、新用途失败与Legacy Test正常来源分开，禁止固定Catalog替代页面保存内容。
- [x] T005 [P] 创建 desktop/tests/Gaode.Desktop.CommissioningProbe/Gaode.Desktop.CommissioningProbe.csproj 和 Program.cs，Windows STA引用公开HostRuntime.InitializeAsync并驱动持有WebView2的ExecuteScriptAsync/DOM，观测真实导航、API及界面；缺Runtime/会话标Blocked，不给产品开放远程调试或测试桥。
- [x] T006 调整 frontend/package.json 的实际Node测试入口；按RM-04在 frontend/scripts/recipe-authoring-012-differences.json、verify-prototype.ps1、build.mjs 中建立021具名授权绑定核验，保留原精确替换规则；后续各页面修改须同步授权差异，不允许仅刷新总摘要。

## Phase 3：US1 受控身份进入（P1）

**目标**：正式桌面以后台确认身份进入；操作员运行、工艺工程师编辑。

**独立完成条件**：V01真实桌面导航及后台核权，必要拒绝零越权、秘密不持久；软件与Runtime阻断分别记。

**适用软件验证先定义于T007，执行于T011。**

- [x] T007 [US1] 在 backend/tests/Gaode.Communication.Tests/CommissioningIdentityTests.cs 和 frontend/tests/commissioning-console/identity.test.mjs 先定义IH-01–05必要正常/拒绝用例：两角色权限、缺/无效/重复配置、用途隔离及输入不能提权；后台必要身份/权限用例随T008执行并留证，真实桌面路线由T011执行。
- [x] T008 [US1] 实现 backend/src/Gaode.Host/Api/CommissioningAuthenticationHandler.cs、IdentityEndpoints.cs 并调整 Station01Authorization.cs 和 backend/src/Gaode.Host/Program.cs；独立配置认证与非test主体、现权限策略、identity只读/no-store和固定来源CORS，沿LongPolling头认证，拒绝query凭据，旧模式不扩权。
- [x] T009 [US1] 在 desktop/HostConfiguration.cs、HostRuntime.cs、DesktopRuntimeLog.cs 接入IH非秘密profile/模板路径及本进程所选环境凭据；只注入受控来源内存，遮蔽选定秘密，拒绝不完整配置，无默认管理员/Test回退，保持联调调试限制。
- [x] T010 [US1] 在 frontend/src/pages/login.html、frontend/src/runtime.js、frontend/src/config/runtime-config.ts 绑定identity、确认角色/显示名及用途；沿现校验位置拒绝输入错配/L3，导航不携身份权限/凭据，每页重新核验，失败清内存权限和媒体；不新增登录控件。
- [ ] T011 [US1] 执行V01真实桌面登录→运行页及最小后台身份拒绝回归，核固定CORS/通知认证、URL/storage/持久日志无凭据、无联调调试端口；在 specs/021-commissioning-console/evidence/v01-identity.json 和 validation.md 留证，不保存真实秘密；缺Runtime/会话只记桌面Blocked，不阻断后台/页面独立工作，桌面子项未验不得勾选本任务。

## Phase 4：US2 人工配方制作与选择（P1）

**目标**：既有弹窗手填、校验、保存重读及选择，编辑不污染在途。

**独立完成条件**：V02真页面/共同服务/SQLite，适用P01–P10归属完整；坐标及单张曝光A旧B新实际消费。

**适用软件验证先定义于T012，执行于T015。**

- [ ] T012 [US2] 在 frontend/tests/commissioning-console/recipe.test.mjs 和 backend/tests/Gaode.Communication.Tests/RecipeCommissioningChainTests.cs 定义V02实际页面新增/重读/选择及坐标、单张曝光A/B隔离验证；沿共同API/SQLite及正式执行器观测，不只断言计划版本。
- [ ] T013 [US2] 逐项核对020 P01–P10与 frontend/src/recipe-authoring.js、backend/src/Gaode.Host/Api/RecipeEndpoints.Authoring.cs 的实际消费者，填写 specs/021-commissioning-console/recipe-field-map.md：控件/公共/后台归属、读存/冻结/执行路径及具体缺口；明确PlcRecipeId显示编号与PoseProgram现场型号分离。
- [ ] T014 [US2] 修复 frontend/src/recipe-authoring.js、frontend/src/runtime.js、frontend/src/pages/a.html 已授权配方读存/目录/选择绑定；保留隐藏字段与原ETag，逐张profile真实生效、错误定位、不自动重存；新用途保存后可选，区分意图与F冻结，后台已正确部分不强改。
- [ ] T015 [US2] 执行V02真实页面新增→校验保存→关闭重开→选择，在正式执行器A冻结后经页面改坐标与单张曝光保存B，核A旧值/后续合法绑定B新值；输出 specs/021-commissioning-console/evidence/v02-recipe.json 并关联实际SQLite/媒体/版本摘要。

## Phase 5：US3 正式启动与可理解阻断（P1）

**目标**：合法准备经正式Start，受理不等于完成；响应未知只查原请求。

**独立完成条件**：V03原主体关联查询，V04新用途失败链零相关运动且日志可定位；无自动重发。

**适用软件验证先定义于T016，执行于T021。**

- [ ] T016 [US3] 在 backend/tests/Gaode.Communication.Tests/CommissioningStartQueryTests.cs、frontend/tests/commissioning-console/start-recovery.test.mjs 先定义SC-01/02最小受理丢失、原主体查询、持久重开、404仍未知及数据库失败不当404用例；准备V04新用途缺输入/安全未明观测。
- [ ] T017 [US3] 在 backend/src/Gaode.Application/Station01/CommandRegistry.cs、Ports/ITraceQuery.cs、backend/src/Gaode.Infrastructure/Persistence/TraceQuery.cs 和 backend/src/Gaode.Host/Api/QueryEndpoints.cs 实现按认证主体/requestId只读查询Start回执，查活跃登记及现SQLite Commands；同步全部实际端口实现/测试替身，保持Pending/Committed真实含义。
- [ ] T018 [US3] 在 frontend/src/runtime.js 和 desktop/HostRuntime.cs 接通版本化CommissioningStartTemplate及每次显式启动的新requestId/trayId，冻结选择引用并用现Start正文提交；保留旧Test路径，缺准备资料明确拒绝，不从页面填写安全值/坐标或冒造F绑定。
- [ ] T019 [US3] 在 frontend/src/runtime.js、frontend/src/api/station01-api.ts 实现PendingOperationReference及原请求GET恢复；仅存非秘密ID/摘要并隔离站点/模式/主体，断线/重开只查询，404保留未知，不能改ID重复POST或借currentRun代替原请求。
- [ ] T020 [US3] 在 frontend/src/runtime.js、frontend/src/pages/a.html 绑定受理、执行、清零等待、同坐标沿用、受限/未知及冻结版本/来源；沿 desktop/DesktopRuntimeLog.cs 与 backend/src/Gaode.Application/Diagnostics/RuntimeDiagnostics.cs 记录可关联持久诊断，错误沿既有区域显示，不在UI判到位/清反馈。
- [ ] T021 [US3] 执行V03回执丢失/重开原请求查询和V04新用途正式DI失败闭环，经真实页面验证缺安全输入及PLC语义未明时相关运动零派发；输出 specs/021-commissioning-console/evidence/v03-start-query.json、v04-blocked.json 及持久日志引用。

## Phase 6：US4 媒体、人工取盘与最终完成（P1）

**目标**：同Run媒体/结果及来源准确显示；后台允许时显式取盘确认，保存Final后支持普通下一轮。

**独立完成条件**：V05离线正式正常链真实落库/重读与两轮，V06七格和数据页真实关联；不改故障恢复，不声明现场已验。

**适用软件验证先定义于T022，执行于T027/T028；媒体/数据页规则在T025/T026验证。**

- [ ] T022 [US4] 在 backend/tests/Gaode.Communication.Tests/CommissioningCompletionAdmissionTests.cs 和 frontend/tests/commissioning-console/completion.test.mjs 定义V05普通Final释放/下一轮最小测试：先保存、保存失败/未知不释放、旧确认不清新owner、GET无副作用及原故障链保持。
- [ ] T023 [US4] 在 backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs 的共同完成路径、Station01/CommandRegistry.cs、backend/src/Gaode.Host/Api/RunEndpoints.cs 与 QueryEndpoints.cs 接入持久Final核对后的普通释放及start-admission只读GET；保留faultRestartOwner，核重放/恢复一致性，缺证明不释放。
- [ ] T024 [US4] 在 frontend/src/public-tray-flow.js、frontend/src/runtime.js 贯通既有人工取盘确认、原ID未知查询、Final重读及下一轮准入；只有Final保存成立且Available才解除普通启动锁，下次仍显式点击；暂停/取消/故障/页面关闭不走普通释放。
- [ ] T025 [P] [US4] 在 frontend/src/runtime.js 与 frontend/tests/commissioning-console/media.test.mjs 实现/核验当前Run媒体到C/D/A/B/E/3D/F七格，保留对象/面/采集身份、最新可读默认及点击固定选择，切Run释放旧URL；非Test不沿Test散列，不新增E采集动作。
- [ ] T026 [P] [US4] 在 frontend/src/pages/data-view.html 与 frontend/tests/commissioning-console/data-view.test.mjs 绑定有权查询的已知Run结果/媒体及来源，重查后台事实；停用联调mock产品/趋势、随机报告ID和本地质量判定，缺后台能力的现控件明确受限，不扩历史搜索/报表平台。
- [ ] T027 [US4] 执行V05：在明确Legacy Test离线布局经实际页面/正式Host到人工取盘确认及Final保存重读，再显式启动下一轮；核旧确认不能释放新owner、保存失败/未知保留占用，定向回归原故障路径；输出 specs/021-commissioning-console/evidence/v05-completion.json。
- [ ] T028 [US4] 执行V06真实媒体文件与数据页验证，覆盖同角色多帧选择、跨Run不串图、未参与不补图和重开终态；将关联/来源/三页实际DOM操作写 specs/021-commissioning-console/evidence/v06-media.json；使用T004建立的专属Commissioning Run及实际离线采集/提交记录，经正式受认证目录/媒体/运行查询进入新用途页面；至少一次同相机多帧及两个不同Run核关联、各角色到七格对应和一次未参与不补图，不扩面序组合；不复用V05旧Test Run、不改用途标签、不注入伪造响应或成功目录。

## Phase 7：收尾与证据

只做改动适用构建、定向回归和证据/文档同步；阻塞和未验不改Passed。

- [ ] T029 按实际变更执行 frontend/package.json build/typecheck/非零Node测试、frontend/scripts/verify-prototype.ps1，并构建 backend/src/Gaode.Host/Gaode.Host.csproj、desktop/Gaode.Station01.Desktop.csproj 及新夹具/探针；结果存 specs/021-commissioning-console/evidence/build-and-prototype.json，原型输出不覆旧文件。
- [ ] T030 执行受改动直接影响的最小回归：frontend/tests/core/member-gripper.test.mjs、backend/tests/Gaode.Communication.Tests 新查询/身份/完成用例及现故障恢复/清零/同坐标必要代表；将确切过滤、通过失败数和020未改证据复用范围写 specs/021-commissioning-console/evidence/regression.json，不扩大全量异常组合。
- [ ] T031 汇总 specs/021-commissioning-console/validation.md 和 evidence/source-state-final.json，核V01–V06真实操作、输入版本、各组件来源、SQLite/媒体、持久日志和源码摘要；保留所有失败记录，分别列已验/改而未验/未实现/外部阻断，不把软件离线证据拼成现场通过。
- [ ] T032 根据实际结果同步 specs/021-commissioning-console/spec.md、plan.md、quickstart.md、contracts/shared-change-register.md 及 tasks.md 进度和精确夹具/探针命令，给出软件收口/剩余阻断与converge入口；requirements清单、020任务/证据及原型只读，不打包部署/提交推送。

## Phase 8：用户新增可配置联调与虚拟光源（FR-017/018）

来源：2026-10-08用户明确要求，不把本增量算作原验证已通过。遵守[CC合同](contracts/configurable-commissioning.md)，沿现目录/任务ID，requirements只读。

- [ ] T033 定版共享增量：核RecipeDefinitionSerialization历史版本/共同正文、冻结计划、CaptureRequest/摘要/证据和全部消费者；在CC、data-model及012共享合同登记准确字段、版本和历史读取映射；确定多配方虚拟输入集合及绑定前公共输入的匹配/冻结；修正当前单ExpectedRecipe限制及ReadECode不支持的任务范围，不编造安全值。依赖T013，先于T034/T035及相关T018装配修改。
- [ ] T034 按CC实现后端可配置虚拟输入匹配及Simulated/Real光源模式：共同校验/读存/规划/冻结/请求/摘要/采集门禁/来源全部贯通；虚拟模式跳过光源参数和控制，保留相机采集/SQLite/媒体；真实模式缺装配拒绝，公共3D/F独立配置。补定向组件验证，沿既有接口不建新引擎，不实现未知真实光源协议。依赖T033。
- [ ] T035 在既有frontend/src/recipe-authoring.js配方弹窗加入唯一授权“虚拟光源”复选框及亮度状态，贯通新建/校验/保存重读/编辑/选择，不丢隐藏字段或在途快照；按具名精确差异更新原型核验，不改归档。依赖T033、T034共享API与T014既有编辑接线。
- [ ] T036 执行CC-04的V07/V08：两套实际步骤不同配方及各自OFFLINE输入通过正式链；真实DOM勾选/保存重读、虚拟缺灯参数零灯派发且真实采集保存、Real缺装配阻断、OFFLINE端口调用顺序、模式编辑冻结隔离。输出evidence/v07-configurable-recipes.json和v08-light-mode.json，不扩全组合、不连接硬件；现场缺值仍只阻断依赖动作。依赖T034/T035，桌面缺环境按原局部规则处理，不能假通过。
- [ ] T037 更新validation、字段表、shared-change-register、plan/quickstart/tasks与最终源码/验证清单，纳入新增模式和多配方构建/原型最小检查；旧V01–V06及020证据保留原范围，明确已验/未验/现场阻断，T055/T056及requirements不改。依赖T036实际结果，与T029–T032收尾合并执行后再判断converge。

T018/T025消费新增输入/来源时以T033合同定版及T034对应实现为前置；其不依赖该增量的查询/界面工作继续。T031/T032最终汇总也须纳入T036/T037，不能按原32项口径宣称本功能已完成。

## 当前规格的关键规则覆盖

| 当前适用规则及来源 | 对应任务ID | 必要证据 |
| --- | --- | --- |
| 正式入口实际调用，受理不等于完成 | T004/T005/T018–T021/T027 | DOM/API/实际Run与终态；旧Test正常和新用途失败分列 |
| 双端清零、同坐标、未知不重发 | T019–T021/T030 | 页面仅读后台；020有效证据复用，改动相关代表回归 |
| 主体/请求/Run/Tray/配方/媒体身份 | T008/T017/T019/T025/T028 | 原主体查询、跨Run不串图、来源明确 |
| 保存和快照 | T013–T015/T023/T027 | 实际SQLite、A/B请求、Final先于释放 |
| 鉴权、API边界、原型只读 | T006–T011/T029 | 真实宿主及精确归档/授权差异核验 |
| 配置及策略 | T009/T013/T018 | 版本模板和共同模型，不新增引擎或技术UI |
| 适用工位协议/工艺 | 020有效合同，T020/T021/T030 | 不新增PLC信号，不放宽运动安全及复位语义 |

## 任务追溯与依赖

以下证据路径相对于specs/021-commissioning-console；源码产物见对应任务正文。表中前置为实施依赖，不表示已完成。

| 任务ID | 当前spec需求/成功条件 | 宪章原则 | 产物路径 | 真实前置依赖 | 完成条件与证据 |
| --- | --- | --- | --- | --- | --- |
| T001 | FR-015/016；SC-006 | P01/P10/P12/P13 | specs/021-commissioning-console/evidence/source-state-before.json（详见任务正文） | analyze无未处理阻塞 | 基线可复核；T055/T056仍Blocked；无原型或旧证据改写 |
| T002 | FR-001–004/009–015；SC-001/004–006 | P01/P05/P07/P12 | specs/006-frontend-station01-console/contracts/host.md、api.md、prototype-mapping.md、public-tray-flow-016.md，登记新用途/身份、原请求查询、Final释放/准入、媒体七格；闭合此次映射未决，保留旧用途及历史证据（详见任务正文） | T001 | 逐项引用IH/SC/RM；共享变更先于产品代码 |
| T003 | FR-005–008/015；SC-002/003/006 | P01/P03/P08/P12 | specs/012-recipe-authoring/contracts/editor-ui.md、recipe-authoring-api.md、shared-integration.md（详见任务正文） | T001 | 正文/权限/保存与选择规则一致；不扩原型控件 |
| T004 | FR-016；SC-001–005 | P05/P08/P09/P13 | backend/tests/Gaode.CommissioningUiFixture/Gaode.CommissioningUiFixture.csproj（详见任务正文） | T002、T003 | 夹具明确OFFLINE、仅loopback、无真实SDK；输出身份关联/实际请求及持久事实，不造成功；V06专属Commissioning Run及实际采集/提交链 |
| T005 | FR-015/016；SC-006 | P05/P12/P13 | desktop/tests/Gaode.Desktop.CommissioningProbe/Gaode.Desktop.CommissioningProbe.csproj（详见任务正文） | T002、T003 | 真实宿主可观测，探针与产品职责隔离；不以浏览器替代桌面 |
| T006 | FR-015/016；SC-006 | P09/P12/P13 | frontend/package.json（详见任务正文） | T004（不依赖T005实际执行） | 测试非零匹配；归档/三页/别名/资源可精确核对，历史授权保留 |
| T007 | FR-001–004/014/015；SC-001/006 | P04/P05/P09/P12 | backend/tests/Gaode.Communication.Tests/CommissioningIdentityTests.cs（详见任务正文） | T006 | 断言后台权限及零越权，不镜像字段实现或扩全认证矩阵 |
| T008 | FR-001–004/014；SC-001 | P04/P05/P07/P09 | backend/src/Gaode.Host/Api/CommissioningAuthenticationHandler.cs、IdentityEndpoints.cs（详见任务正文） | T007 | IH角色权限/用途一致并通过T007后台必要用例；GET无设备副作用；后台持久诊断不含秘密 |
| T009 | FR-001/002/004/009/014/015；SC-001/006 | P04/P05/P09/P12 | desktop/HostConfiguration.cs、HostRuntime.cs、DesktopRuntimeLog.cs（详见任务正文） | T008 | 单进程单身份；外部导航/子帧不获秘密；日志不打印配置或headers |
| T010 | FR-001–004/014/015；SC-001/006 | P04/P05/P07/P12 | frontend/src/pages/login.html、frontend/src/runtime.js、frontend/src/config/runtime-config.ts（详见任务正文） | T009 | 实际构建页面生效；用户名密码不作认证请求，角色选择不能提权 |
| T011 | FR-001–004/014–016；SC-001/006 | P04/P09/P12/P13 | specs/021-commissioning-console/evidence/v01-identity.json（详见任务正文） | T010 | 有效两角色进入且越权零成功；缺环境明确Blocked，不能只计组件测试通过 |
| T012 | FR-005–008；SC-002/003 | P03/P07/P08/P13 | frontend/tests/commissioning-console/recipe.test.mjs（详见任务正文） | T010、T008后台必要身份验证通过（不依赖T011桌面验收） | 用例覆盖正文保留、原ETag冲突/保存未知及A/B实际请求，OFFLINE值有来源 |
| T013 | FR-005/006/008/015；SC-002/003 | P03/P08/P11/P12 | frontend/src/recipe-authoring.js、backend/src/Gaode.Host/Api/RecipeEndpoints.Authoring.cs（详见任务正文） | T012 | 所有适用项有可追溯归属；缺UI承载不自行新增、仅阻断依赖项 |
| T014 | FR-005–008/014/015；SC-002/003 | P03/P07/P08/P11/P12 | frontend/src/recipe-authoring.js、frontend/src/runtime.js、frontend/src/pages/a.html（详见任务正文） | T013 | 完整正文与实际保存重读一致，无默认坐标/抓手/曝光补齐，无新增批准/导入 |
| T015 | FR-005–008/016；SC-002/003 | P03/P07/P08/P13 | specs/021-commissioning-console/evidence/v02-recipe.json（详见任务正文） | T014 | 实际Move/Capture请求与页面新旧正文相符；组件范围明确，不当真机整链通过 |
| T016 | FR-009–011/014；SC-004 | P04/P07/P08/P09 | backend/tests/Gaode.Communication.Tests/CommissioningStartQueryTests.cs、frontend/tests/commissioning-console/start-recovery.test.mjs（详见任务正文） | T008后台必要身份验证通过、T006（不依赖T011） | 断言零自动重发/零跨主体冒接；请求查询无设备副作用 |
| T017 | FR-011/014；SC-004 | P05/P07/P08/P09 | backend/src/Gaode.Application/Station01/CommandRegistry.cs、Ports/ITraceQuery.cs、backend/src/Gaode.Infrastructure/Persistence/TraceQuery.cs（详见任务正文） | T016 | 不调用Replay/Start；多条不一致记录明确错误；无新表/迁移或成功兜底 |
| T018 | FR-007/009/010/014/015；SC-004 | P04/P05/P07/P08/P11 | frontend/src/runtime.js（详见任务正文） | T014、T017、T010（不依赖T015整体验收） | 202仅受理；正式新用途context匹配；模板版本/来源与本次请求关联 |
| T019 | FR-011/014/015；SC-004 | P04/P07/P08/P12 | frontend/src/runtime.js、frontend/src/api/station01-api.ts（详见任务正文） | T018 | 恢复查原请求/Run且无自动动作；换身份不接管原主体未知请求 |
| T020 | FR-008/010/014/015；SC-004/006 | P04/P07/P09/P12 | frontend/src/runtime.js、frontend/src/pages/a.html（详见任务正文） | T019 | 缺项/影响步骤/Run可定位，高频受控，不记录秘密或上下文全文 |
| T021 | FR-009–011/014/016；SC-004 | P04/P07/P09/P10/P13 | specs/021-commissioning-console/evidence/v03-start-query.json、v04-blocked.json（详见任务正文） | T020 | loopback审计心跳单列，七worker不启动真实SDK；未知不重发；不伪造SafetyClear |
| T022 | FR-010/011/013；SC-005 | P04/P07/P08/P13 | backend/tests/Gaode.Communication.Tests/CommissioningCompletionAdmissionTests.cs（详见任务正文） | T017、T020（不依赖T021整体验收） | 同Run/Tray持久证明，不能用页面完成或ReadyForRemoval当Final |
| T023 | FR-010/011/013/014；SC-005 | P05/P07/P08/P09 | backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs（详见任务正文） | T022 | 现StageEvents/Run证明Final后锁内比较同owner；Available只为软件准入，不是运动许可 |
| T024 | FR-010/011/013–015；SC-005 | P04/P07/P08/P12 | frontend/src/public-tray-flow.js、frontend/src/runtime.js（详见任务正文） | T023 | 受理/等人工/质量OK不当最终完成，未知不自动再次确认，不加人工换面 |
| T025 | FR-012/014/015；SC-005/006 | P02/P06/P07/P08/P12 | frontend/src/runtime.js（详见任务正文） | T024 | 未参与不补图，提交不可读明确受限；真实采集/虚拟算法/虚拟灯分别展示 |
| T026 | FR-012/014/015；SC-005/006 | P05/P07/P08/P12 | frontend/src/pages/data-view.html（详见任务正文） | T024 | 跨页只传非秘密Run引用；页面重新鉴权，未提供指标不造数，无新布局 |
| T027 | FR-010/011/013/014/016；SC-005 | P04/P07/P08/P09/P13 | specs/021-commissioning-console/evidence/v05-completion.json（详见任务正文） | T025、T026 | 实际SQLite Final、人工来源、两轮请求及owner可复核；不是新用途真机正常链 |
| T028 | FR-012/014–016；SC-005/006 | P06/P07/P08/P12/P13 | specs/021-commissioning-console/evidence/v06-media.json（专属Commissioning Run，详见任务正文） | T004、T025、T026（不依赖T027旧Test正常链） | 新用途实际采集/提交/正式GET/页面关联，七格对应及未参与留空；不是整机正常完成证明 |
| T029 | FR-015/016；SC-006 | P09/P12/P13 | frontend/package.json（详见任务正文） | T008–T010、T014、T017–T020、T023–T026的实际实现（不依赖桌面实测完成） | 真实命令/退出码/匹配计数；精确差异通过，不通过则修复并定向重验 |
| T030 | FR-008/010/011/016；SC-003/004/006 | P04/P07/P09/P13 | specs/021-commissioning-console/evidence/regression.json，不扩大全量异常组合（详见任务正文） | T029对应受测组件构建就绪；其余构建受限只影响对应组件 | 故障恢复与双端清零门禁未被普通释放削弱；旧Test/Production准入未放宽 |
| T031 | FR-014–016；SC-001–006 | P01/P09/P10/P13 | specs/021-commissioning-console/validation.md（详见任务正文） | T011/T015/T021/T027/T028/T029/T030结果已记录（可含明确Blocked，不能当Passed） | 证据均可定位且无秘密；T055/T056和历史未验保持，缺证据不勾完成 |
| T032 | FR-015/016；SC-006 | P01/P10/P12/P13 | specs/021-commissioning-console/spec.md、plan.md、quickstart.md、contracts/shared-change-register.md（详见任务正文） | T031 | 只凭实际验证勾选；独立说明软件完成与现场/交付未完成 |

### 故事依赖与执行顺序

```text
analyze → T001 → (T002 + T003) → T004 → T006
T002 + T003 → T005（探针建立；运行环境不作其他实现前置）
T006 → T007→T008（含后台身份验证）→T009→T010
T010 + T005 + 实际桌面环境 → T011（独立验收门）
T010 + T008后台验证 → US2 T012→T013→T014→T015
T008后台验证 + T006 → US3 T016→T017
T014 + T017 + T010 → T018→T019→T020→T021
T017 + T020 → US4 T022→T023→T024→(T025 + T026)→T027
T004 + T025 + T026 → T028（专属Commissioning媒体组件，不依赖T027）
相关实现 → T029→T030（按受测组件就绪执行）
全部验证结果已记录（含Blocked但不冒称通过）→T031→T032
所需实际证据齐备后 → converge
```

所有故事均P1；后台身份和接口实际可用是对应操作前置，T011完整桌面验收不是其他软件实现的全局前置。US4依赖运行关联，不能先用mock补完成。US2的A/B组件证据与US3阻断证据不拼成新用途正常整链。

### C1：实现依赖与验收依赖

依赖表列硬性实现/接口条件；表中“结果已记录”用于汇总，可包含Blocked，不等于前置验收通过。T011、T015、T021、T027、T028内若只有桌面子项受环境限制，继续其不依赖桌面的API/组件/可用页面子项及后续独立实现，分别记录状态；任务整体保持未完成。T005无法构建/执行只限制探针；T029桌面构建受限不阻断已构建后端/前端的T030回归。所有实际桌面/页面及正式主流程成功条件保留，T031/T032只汇总事实，不能据部分结果宣称021已验收。

### 并行机会及各故事示例

- 准备：T001后T002与T003合同文件不同，可并行；两项完成后T004与T005独立测试项目可并行。
- US1：认证、宿主注入和页面核验按T008→T009→T010串行，T011统一实测；不并行修改runtime.js/Program.cs或共用配置。
- US2：T013字段对账后T014单一修改配方/运行共享脚本；T015可并行读取已冻结A与已保存B的证据，但不能并发编辑同一配方版本或共享夹具。
- US3：T008后台身份验证通过且T006完成后，T016/T017仅在不修改US2夹具/RecipeCommissioningChainTests时可与US2并行；T018汇合后runtime.js恢复/状态任务串行。
- US4：T024后T025(runtime.js)与T026(data-view.html)独立可并行；两者如都需改构建/差异清单，则共享变更交T029统一核验，不能同时写清单。

[P]仅表示满足前置且文件/状态不冲突时的机会，不指定人员、不授权启动多代理。构建输出、测试数据根和证据文件不得并发争用。

### 实施策略

最小增量先完成后台身份验证与接口/宿主/页面实现，再推进US2配方和US3查询/启动，US4闭合人工终点与普通下一轮。V01桌面实测独立排期；缺Runtime/会话不阻止表中独立实现、构建及组件验证。每个故事仍须实际证据才收口，Blocked子项不得勾选；可用真实构建页面验证的路线标页面环境，不能代替桌面证据。全软件结束后才converge；正常现场流程待后续授权和资料，不在本任务清单隐式执行。

## OPEN、外部依赖与延期

| OPEN/外部依赖 | 来源 | 只限制的任务或联调 | 补充时机 | 可继续的任务 |
| --- | --- | --- | --- | --- |
| DEP-021-01：PLC-Q3/Q4、用户安全算法值及现场配置 | 021spec、020有效合同 | 正常真实运动，不限制本清单离线T001–T032 | 对应现场动作前 | 所有确定软件/零派发；不能自行填值 |
| DEP-021-02：T055/T056现场证据 | 020 tasks与用户“已连通、流程未测” | 七机效果/正式真机两轮通过声明；此处不追加现场执行任务 | 后续现场授权 | 页面媒体/宿主及离线流程 |
| DEP-021-03：旧根部资产/012原型说明缺失 | 021spec/research | 不得以旧勾选声称当前完成；新增承载须确认 | T002/T003/T013核对 | 以原件/现合同/本规格实施；不补造历史 |
| DEP-021-04：真实身份秘密/配置 | IH设计已完成 | 真实本机身份配置/部署，不阻断离线专用凭据 | 实际使用前受控配置 | T007–T011及所有离线任务 |
| 公共Z及历史SDK/机械未验 | 020历史记录 | 对应动作/精度/超时现场声明 | 独立后续任务 | 不改公共位置职责，软件当前范围继续 |

延期真实账号体系、全库历史查询/趋势/复检/报表平台、真实算法灯及质量指标、压力/全异常矩阵、部署包/版本发布/回退实操。缺必要现场资料不得把正常链改成“无动作”后计通过；离线旧Test正常仅说明其标明范围。

## 客户确认原型检查（P12）

原件E:/dzk/gaode/原型.zip只读，SHA-256为3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0；HTML为a.html、data-view.html、login.html。遵守021/现006/012/016授权，只绑定数据/权限/状态/错误，不改布局/文字/控件/导航。实际构建源frontend/src/pages，prototype.html为别名；根部旧副本/未使用TS辅助文件不是验收对象。

七格已确认环面1=C、环面2=D、孔底1=A、孔底2=B、来料2D=E、来料3D=3D、读码参数=F。显示对应不新增采集工艺；未参与不填历史图。遇必要新UI或协议歧义，列具体字段/动作影响再澄清，其余独立任务继续。

任务生成时未执行产品实现；目前已进入软件实施，进度以逐项标记和validation.md实际证据为准。历史任务生成说明不代表当前状态。硬件连接与Git提交推送仍未授权；后续本机打包部署授权见本轮新增交付授权，历史证据和requirements保持只读。

本次用户明确授权的“虚拟光源”控件及关联字段状态为原型限制的唯一新增定向例外，见FR-018/CC-02/T035；原型ZIP只读，其他范围不变。

## 本轮新增交付授权

用户明确解除打包/部署限制，新增T038/T039；历史“不打包部署”按旧授权解释，Git提交/推送仍禁止。

- [x] T038 在软件实现及必要验证结果已实际记录后制作版本关联的本机部署包：包含正式Host/桌面/前端/相机worker、配置模板、源码摘要、验证清单、使用/阻断/回退说明及校验值；现场安全值缺失不代填，包明确待配置且禁止依赖运动启动。保留旧部署目录与标签，不将缺口包称完整现场通过。
- [x] T039 检查本机部署位置、依赖与现有进程；在独立版本目录完成可逆安装和无设备连接的入口/配置拒绝验证，真实运动及现场流程仍须安全输入。记录实际部署状态与回退路径，不提交推送。

- [ ] T040 用户确认F位置录入：在共同正文、配方基础页、校验保存重读、启动选择引用及虚拟3D首次F定位中贯通commissioningFPosition，按已保存版本生成来源并冻结，不补默认XY；验证字段保存、行程限制及运行编辑隔离，原真实算法及公共位置职责不变。依赖T033/T034/T035，与V07/V08及收尾同步；现场XY等待用户实际填写。


2026-10-08 F位置实施增量：T040的共同正文/页面/启动引用已实现，桌面保存重读及离线Run冻结隔离/行程拒绝已有实际证据；正式运动请求验证未完成，保持未勾选。最新证据及目录/媒体重试问题见validation.md“F读码位置录入”增量。该条为F位置增量当时状态；后续包/安装进度见2026-10-08交付增量及validation.md。


- [x] T041 按本轮用户授权转换当前本机联调成功配方，逐字段记录来源/跳过占位/仍缺现场条件，包制作后经正式维护保存入口录入独立安装SQLite，正式重读和规划验证，原件/旧安装不改；固定OK不派发分拣，不用占位编造安全语义或运动坐标。依赖T038，验证不连接硬件。
- [x] T042 修正真实PLC阶段端口把Commissioning误拒为非Production的确定用途判断，严格匹配设备实际用途；定向构建/清零阶段回归并保留现场未验证限制，不绕过安全准入。


## 2026-10-08 最终包与安装实证

T038/T039：正式Release产物复制成gaode-commissioning-console-021-final-1，manifest文件校验实际795项通过，安装脚本再次校验后在D:/Gaode-Station01/commissioning-021-final-1全新目录完成SQLite准备、正式配方保存/新实例重读和虚拟输入正式加载。默认启动检查返回ConfigurationRequired/FieldRuntimeProfileMissing，设备派发0。此处完成的是已约定“待现场配置”的交付包及可逆安装，不是现场完整流程或所有021验收。T041：实际保存RecipeId=5e8ab0f51041427f8946108b84b46b81，Version=ac5b30ee8d264939bb53be38042862ef，六项结果作用域绑定该版本；最终离线2项通过。T042：严格用途匹配修复已Release构建及定向回归；真实现场派发仍未验证。最终交付审计见evidence/deployment-final-20261008。T040及其余未验子项保持未完成，020 T055/T056不变。


- [x] T043 修复正式离线采集器SimulatedCapture把F一次触发错误限制在整个Host生命周期的问题；按Run限制同轮重拍，累计计数仅计已受理请求。执行同Host两轮完整旧Test链与同Run拒绝验证，记录实际Final/媒体/准入，保留首轮失败及现场阻断。发现于T027软件验证，不把后台通过当完整桌面V05通过。修复进入后续部署包，旧包保留。


- [x] T044 修复ThreeStageWorkflowExecutor阶段事件键缺少Run/Tray命名空间导致同Host第二轮T050StageEventIdempotencyConflict；保持同Run回放/冲突与SQLite索引，定向正常两轮、原Final重读及相关三阶段幂等回归，保留失败证据。共享持久键语义先同步SC合同/plan，再改生产端，不扩大为数据库迁移。


2026-10-08 T015编辑隔离子项实际通过：evidence/recipe-20261007-230250含真实WebView2/正式API保存重读、两个正式执行器Run的实际运动及单张曝光请求、SQLite便携备份/已提交媒体。不是从零新增完整验收，T015整体不勾；T027/V07及现场未验项不借此关闭。仅测试探针和文档增量，final-2产品源码未变，便携验证补充包单独关联该部署ZIP。


- [x] T045 按既有IH合同和已确认预配置身份策略，在本机独立安装生成操作员/工艺工程师随机受控身份、非秘密桌面profile及业务start模板；设置当前维护用户ACL并核对，维护脚本仅加载进程环境不启动设备。秘密不入包/证据/源码，配置引用取本安装实际已保存配方/输入，不把模板当运动许可。记录确定配置与剩余外部依赖，不重复澄清身份策略。

## 本轮PLC确认落实与补丁交付

- [x] T046 同步SC-021-PLC-R2契约并实施现场MB2007映射、显式来源的siteOperations及安全零位准入，保留旧Test和未确认配置阻断。
- [x] T047 实施MB2009本次Ready 0→1后清零、首个实际运动闭环后清MB2007，软停/断线停住且未知不续发；定向现场loopback和旧清零/同坐标回归，保存实际证据。
- [x] T048 重新发布受影响程序集，生成针对final-2的哈希校验补丁及独立新版本目录，含源码增量/配置/来源/回退；不覆盖旧包/安装，不连接硬件。
- [x] T049 核验补丁应用文件及正式配方保存/重读，更新验证与部署说明；不把离线通过当T055/T056现场通过，完整运行配置仍按实际状态报告。



- [x] T050 按用户本轮授权装配final-3本机运行配置：复用已保存配方、成功联调连接参数、七相机历史采集参数和已确认PLC语义/范围，使用正式加载器、用途矩阵及依赖注入注册进行无连接检查；仅检查通过后生成runtime-profile及受控身份启动入口。保留原包、旧安装、秘密ACL及现场未验状态；不启动Host/worker、不连接设备、不推送。

- [x] T051 修正启动入口在后台身份接口就绪前打开桌面的确定竞态；先核对已配置身份的正式响应，再打开桌面，后台退出/超时/身份不符则明确阻断。保留失败日志、定向无硬件验证，记录本机PLC专用直连路由及回退；不自动重启设备，不改登录页面或鉴权规则。

- [x] T052 按用户要求增加本安装“一键关闭.cmd”及按绝对可执行文件路径匹配的关闭脚本，先桌面、后台、残留相机worker；保留关闭日志，不发送PLC命令，不误关其他版本；源码发布脚本同步，已执行WhatIf核对9个本版本目标，未实际关闭。

- [x] T053 按用户要求新增“一键复位.cmd”，仅调用正式后台现有reset API，校验停止/故障阶段、记录调用结果和原任务准入；不直接写Modbus、不自动重试/启动、不擅自解除Held，安装入口配置及语法检查通过，现场复位未执行。


## 现场软停恢复增量

- [x] T054 按SC-021-PLC-R3修复MB2006就绪、MB2008取消软停及新鲜读回均先于MB2009上升沿；保留本次Ready 0→1及安全零位核验；离线验证软停前置、读回失败零复位派发、旧请求不重发及原闭环回归。
- [ ] T055 既有页面故障通知与一键复位脚本明确区分PLC复位、旧Run恢复核验及新轮放行；R4恢复入口已实现；当前仅本安装应用与现场交互未验证。发布经校验补丁，保留旧安装和运行库；运行中不覆盖程序集，不连接硬件执行复位。依赖T054。
- [x] T056 补齐Commissioning旧Run（含Host重启后）受控恢复：真实复位事实、设备/实物与配置核验、原执行退出及资源释放、持久审计、授权操作和新Run关联；先明确复位后持料/归位/翻转状态，再更新接口契约并实现，不清库或无条件解除Held。仅此恢复放行依赖该现场确认；020 T055/T056保持原现场阻断。


2026-10-08 R3实际结果：T054定向Release回归32/32通过（reset-order-r3-final.trx），涵盖初始软停0/1、前置读回失败零派发、旧复位请求不重发及原闭环回归。首轮发现未预编译读取集合导致ReadPurposeNotPrepared，已增加现场专用ResetPreconditions读取计划并重跑，首轮失败记录保留。T055页面/脚本说明已实施，组件2/2、构建及原型校验通过；7文件补丁和安装目录“应用复位修正.cmd”已准备，当前9个本版进程仍在运行，更新脚本实际拒绝覆盖，故未应用、不勾整体完成。T056尚未实现；待确认复位后持料/归位/翻转状态，仅阻断完整旧Run恢复放行。未触发现场复位、运动或重启；020 T055/T056不变。


- [x] T057 按用户要求把R3补丁合入final-4完整部署包，保留final-3/补丁/当前安装；同步最新启动、关闭及复位脚本和安装复制范围、schema来源、源码快照和验证记录，核完整清单及ZIP内容一致。仅制作部署产物，不连接设备，不覆盖运行中安装，不解除旧任务。


2026-10-08 T057：final-4已合入R3产品二进制/前端、最新维护脚本和021运行schema，源码快照及32项回归/2项组件/原型核对证据随包；完整目录886项哈希通过。完整ZIP及其摘要由同一目录生成并逐项核对，结果见evidence/deployment-final-4-20261008/package-audit.json。现安装未应用补丁，T055保留未完成；旧Run恢复T056未实现，不声明真机可再次运行。


2026-10-08 用户已确认PLC复位全部恢复，T056现场语义依赖关闭；按SC-021-PLC-R4实现正式Commissioning复位/旧任务取消提交/释放/新轮关联，保留结果未知不重发及旧Test范围。
- [x] T058 定向验证本Host旧任务、Host重启旧任务的复位恢复、真实SQLite取消审计及重读、新轮关联；验证资源未退出、反馈未清零或保存失败时不放行；正常/无软停回归不扩大全量矩阵。
- [x] T059 按用户要求原地更新现有final-4完整目录/ZIP与本安装待应用补丁，保存上一版摘要和必要回退文件，不新增版本号、不自动应用到运行中安装或触发硬件；同步实证状态及旧说明的替代关系。


R4软件实证：T056/T058已完成，40/40后端、3/3组件及原型核对通过，见validation最新R4段。T055仅保留实际安装尚未应用/桌面现场未验，旧‘T056未实现/待确认实物状态’文字为历史记录，不代表当前软件状态。T059须待同名包/ZIP与补丁文件校验完成再关闭。


T059实际完成：原final-4目录963项及同名ZIP964项逐项SHA一致；36项本安装补丁基线/目标校验、离线真实备份应用通过，已同步原updates/reset-r3待应用目录并核157项分发文件。上一版ZIP及R3补丁备份保留。收尾文档与测试日志纳入后再次冻结/核对，最终数量和摘要以package-audit-r4.json为准。本安装未应用，020现场阻断未变。

2026-10-08 R4安装及GitHub交付状态更新：已按用户授权把R4完整包解压更新至原D:/Gaode-Station01/commissioning-021-final-3，36文件定向补丁亦已应用；964项安装文件校验通过，原配方库及运行库哈希保持不变，账号配置保留，备份位于data/deployment-backups/before-r4-20261008-160556及data/hotfix-backups。此前“补丁待应用/未覆盖本安装”属于该时刻历史记录，当前已由本段替代。T055的软件分发/安装子项已完成，仅实际桌面恢复交互与真机流程未验证，整体不借此勾选。用户后续启动后17:07只读状态为PlcHeartbeatLost、旧Run仍RecoveryRequired/PhysicalRunHeld；本次交付不证明已复位恢复或运行放行。部署包产品源码348文件与待推送源码完全一致，无新增产品改动，不需要再打二进制补丁。版本对应通过ZIP旁source-version.json及安装data/git-source-version.json记录；保留原封包sourceHead/dirtySource构建事实，不倒改历史清单。详见github-delivery-20261008.md。

## 2026-10-09 现场诊断后最小修正

- [x] T060 启动Host前按唯一目录保留已有stdout/stderr，CheckOnly不归档、失败不覆盖，保持现有当前日志路径。
- [x] T061 记录复位阶段、耗时、请求取消/预算到期观察以及PLC关键握手事实，保留原设备调用、失败保持与业务放行条件。
- [x] T062 离线验证重启日志保全及复位取消诊断，回归原握手/恢复门禁，记录附件核对结果及未解决的现场依赖。21/21定向后端及隔离启动脚本验证通过，见reset-diagnosis-review-20261009.md；未部署或进行现场恢复。
- [ ] T063 获得PLC对旧MB2007/MB2009结果未知时释放/再次触发条件的明确规则后，再设计受控恢复和现场验证；不在本次诊断修正中自动清请求或解锁Run。
