# Bug Assessment: 第一工位页面启动被旧配置版本校验拦截

- **Slug**: station01-start-config-version-mismatch
- **Created**: 2026-09-24
- **Source**: 用户提供的现场描述、保存的 Test/VirtualLoop 实例及本地源码/合同
- **Verdict**: valid
- **Severity**: high — 当前 007 冻结 Test 请求在实际桌面页面被同步拒绝，启动 POST 根本未发出；暂无设备动作或数据损坏证据。
- **范围**: 006 页面与 007 配置供给的集成边界；不是 003 PLC 协议或下料修复。

## Report (summarized from user input)

实例 `artifacts/station01-007/manual-20260924-053022-9392198b` 中，操作员在 WPF/WebView2 页面点击启动，页面显示“Test上料请求未准备或配置不符”。保存的 `load-s01-007-97931f613058462ea7e400a915da6d96.json` 使用公共配置 `s01-public-virtual-loop/1.2.0`、模拟配置 `s01-sim-virtual-loop/1.2.0`、预算 `s01-budget-virtual-loop/1.1.0`。用户提供的 2026-09-24 05:34:26 截点为启动 POST=0、Runs=0、StageEvents=0。用户要求后续修复不要再次因写死校验而阻断正常流程；本轮仅评估，不改源码、规格或运行实例。

## Symptom

页面在本地 `legalPreparedRequest` 校验中拒绝与现行 007 Test 样本一致的请求，直接显示上述提示并返回，未走正式 `station01.start`。预期是受控且结构合法的冻结请求可到达正式启动 API，由后端权威加载、校验版本与绑定关系并执行原有鉴权和安全门禁；202 仍仅表示受理。

## Reproduction and evidence

1. 保存的请求文件 `artifacts/station01-007/manual-20260924-053022-9392198b/load-s01-007-97931f613058462ea7e400a915da6d96.json` 含 `requestId=s01-007-97931f613058462ea7e400a915da6d96`，`purpose=Test`、`scenarioId=S1`、占位 `P01` 及上述三组版本；文件 SHA-256 为 `6DD4C3D2D25D3EF6E7C152309F4893E84C216DBC5D0989B75021448608A662CD`。其版本与 `scripts/simulate-station01-load.ps1:34-36` 及 `specs/007-station01-integrated-loop/examples/` 当前公共、预算、模拟样本一致。
2. `frontend/src/runtime.js:44-46` 把公共和模拟配置版本均限定为 `1.1.0`；`frontend/src/runtime.js:253` 校验失败显示现场同一文案并 `return`，`frontend/src/runtime.js:256` 才会调用 `window.station01.start(prepared)`。将保存请求代入该纯本地条件必然为 false；无需假设 PLC 或浏览器缓存故障。
3. `artifacts/station01-007/manual-20260924-053022-9392198b/process.json` 记录实际桌面进程 4844、配置 `1.2.0/1.1.0/1.2.0`、`frontendRuntimeSha256=3CDD927C72B1B400D023015FBABDA1B53ED17AE341BCFD6D2EEE1E8C8E27D59C`。只读核对运行进程路径为 `desktop/bin/Release/net10.0-windows10.0.17763.0/Gaode.Station01.Desktop.exe`；其同级 `frontend/dist/runtime.js`、`frontend/dist/runtime.js`、`frontend/src/runtime.js` 均为该哈希，且含旧限制。另一个 `win-x64` 输出目录的不同文件并非此进程加载路径。当前源码、构建产物和现场实际桌面资源一致，因此不能简单归因为缓存。
4. 用户提供的 05:34:26 截点与随后只读复核分别记录：该实例 `station01.test.db` 中 Runs、StageEvents、Commands 均为 0，`logs/host.out.log` 中未匹配本次 `/api/v1/station01/runs` 启动 POST；检查时未见本次心跳故障标记。随后复核不是对 05:34:26 状态的替代或对未来日志的保证。进程仍可能运行，后续新增记录应按时间另行判定。
5. 现有 `frontend/tests/us1/runtime-007.test.ts:14-16` 的正向请求夹具也把公共、模拟版本写为 `1.1.0`，因此当前页面测试只证明旧夹具能通过，未核对现行 007 冻结样本。现场页面提示来自用户观察；此目录检查时未发现独立保存的点击截图或网络抓包，不能把它写成已独立复验的页面影像证据。

## Suspected Code Paths

- `frontend/src/runtime.js:36-46,249-256` — 页面启动入口和本地前置校验；直接阻断 API。
- `frontend/tests/us1/runtime-007.test.ts:9-16,87` — 过时的正向夹具掩盖版本漂移。
- `desktop/HostRuntime.cs:39-47,53` — Test 模式从 `GAODE_TEST_PREPARED_LOAD_PATH` 注入受控准备请求，按 `GAODE_FRONTEND_DIST` 或桌面内嵌资源加载前端；目前未见其改写配置版本。
- `scripts/simulate-station01-load.ps1:22,34-36` — 当前 007 请求版本的供给端；`-PrepareOnly` 不发启动 POST。
- `frontend/scripts/build.mjs`、`desktop/Gaode.Station01.Desktop.csproj:16`、`scripts/start-station01-manual-test.ps1:40-62` — 源码到 `frontend/dist` 再到桌面资源的构建/复制链；现有哈希检查只核对两份运行资源相等，不保证页面校验与 007 样本兼容。
- `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs:115-121`、`backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs:59-73` — 后端加载三组配置、校验 Test/Virtual 绑定及引用一致性并冻结快照，不能由前端硬编码替代。

## Root Cause Hypothesis

**高置信度，直接原因已确认。** 006 页面校验把应来自 007 冻结 Test 样本的公共/模拟版本写成旧常量；007 供给端已经升级为 `1.2.0`，但页面源码及测试夹具未同步。`legalPreparedRequest` 在发送前拒绝，因此该次点击没有到达后端，更未进入 PLC、下料或心跳故障路径。`specs/006-frontend-station01-console/contracts/api.md:16` 已规定具体配置版本来自 007 冻结 Test 样本，页面实现与测试违反这一集成合同。已知证据足以进入最小修复；缺少原点击影像不改变代码层面的确定性结论，但原入口验收仍须独立补做。

## Proposed Remediation

**Preferred:** 在 006 页面保留受控 Test 模式、请求结构、必填上下文/引用、合法占位和授权传递校验，取消对具体配置版本（以及不必要的可变配置标识）的本地常量白名单；将 Host 注入的本次准备请求引用原样提交。页面只做可即时判断的格式与 Test 范围校验，不能自行宣布配置已获批准。后端继续对配置实际存在、版本、跨引用一致性、来源、鉴权及设备安全门禁作权威判断并冻结，明确返回拒绝原因；不得通过删除所有校验、把样本降回旧版或伪造成功消除症状。不引入配置平台。

将现行 007 样本作为**跨边界测试输入**，而非仅把页面测试夹具中的两个 `1.1.0` 改为 `1.2.0`：测试应从 007 既有样本/准备输出读取引用，或在同一测试中校验 fixture 与该输出一致；以后版本再升级时，不同步页面常量也应能通过合法请求测试。对不合法引用，前端拒绝结构/用途错误，后端拒绝不存在或不兼容的版本，二者分工须有断言。常规构建重新生成 `frontend/dist` 和桌面内嵌资源，核对实际运行路径和哈希；不得只手改 dist。

**Files likely to change in bug-fix**:

- `frontend/src/runtime.js` — 缩小本地校验到结构、Test 范围和必要身份；不固定具体版本。
- `frontend/tests/us1/runtime-007.test.ts` — 与现行 007 样本联动的正/反向测试和单次 POST/202 断言；如现有测试架构无法直接读取样本，可在同目录新增最小跨样本契约测试。
- 构建产物 `frontend/dist/**`、`desktop/bin/.../frontend/dist/**` 只通过正常构建更新，不作为手工源码修改。`desktop/HostRuntime.cs`、`scripts/simulate-station01-load.ps1`、启动脚本目前看不需要语义修改；若实际修复发现受控传递或构建校验缺口，仅限该缺口补强并记录依据。

**006/007 document synchronization:** `specs/006-frontend-station01-console/contracts/api.md:16` 的版本来源原则已正确，无须改合同语义；修复记录应明确页面/后端校验责任和新验收证据。007 的冻结样本及 `specs/007-station01-integrated-loop/contracts/commissioning-cli.md` 已提供供给边界，保持既有版本和 Test 用途；如新增跨样本验证步骤，仅对受影响的 006/007 验证说明/任务记录作最小增量，不重走 specify/plan/tasks 或创建新 feature。只有修复确实改变公开合同才先同步相应规格/合同。

**Required tests and independent acceptance:**

1. 现行 `public/simulation=1.2.0`、`budget=1.1.0` 的 007 冻结准备请求从页面入口可到正式 POST；一次点击只发一次请求，202 不显示为运行完成，后续以正式查询事实为准。
2. 缺失/畸形引用、非 Test 用途或不合法上下文仍被页面明确拒绝；结构合法但不存在/互不兼容的版本到后端后由权威校验明确拒绝，不制造运行成功或绕过安全门禁。
3. 跨样本测试读取或核对 007 当前冻结样本，且包含一个与旧 `1.1.0` 页面白名单不一致的合法版本；正常构建后比对源码、dist、实际桌面资源的内容/哈希，避免“两个旧文件哈希相同”假阳性。
4. 隔离 Test/VirtualPlc 下实际 WPF/WebView2 原型已有按钮重新点击并保存页面前后、请求/回执、日志、SQLite。辅助 API 或普通浏览器测试不能替代。下料缺陷现有 partial 验收、001 T088 与间歇性心跳问题分别维持原结论，不由本缺陷修复外推。

## Risks & Considerations

- 如果前端完全放行任意请求，会模糊 Test 范围和用户提示；如果前端仍决定具体版本，则再次漂移。边界应是“页面结构/用途 + 后端权威版本/安全”。
- `-SkipBuild` 现有相等哈希检查不能发现源码和 007 样本的语义漂移；验收必须核对真正加载的桌面进程、资源路径和现行样本。不要触碰其他会话进程或真实设备。
- 当前观察是启动前页面拒绝，不能宣称原下料问题、T088、心跳故障或第一工位整站已修复；Test/VirtualPlc 成功也不等于真机验收。
- 缺少原点击的保存截图/抓包；修复后需取得同一路径的新页面证据，不能用本评估补造历史证据。

## Open Questions

- 无阻塞 bug-fix 的配置版本问题；当前 007 样本及 006 合同已给出足够依据。原现场截图/网络抓包未保存，属于独立验收证据缺口，而非再次询问配置版本的理由。

## Readiness

**具备进入 `$speckit-bug-fix slug=station01-start-config-version-mismatch` 的条件。** 最小修复应落实上面的“受控引用传递 + 后端权威校验 + 现行样本防漂移测试”，随后再通过独立 bug-test 实际复核 WPF/WebView2 页面。完整功能开发流程不是本次已确认缺陷的前置，本评估本身不实施修复。
