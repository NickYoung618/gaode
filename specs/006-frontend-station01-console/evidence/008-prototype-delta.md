# 008 第二批必要配方控件增量（2026-09-25）

状态：**006 T048 部分完成，未勾选**；当前没有可操作的隔离 WPF 交互会话，未进行正式页面点击验证。`E:/dzk/gaode/原型.zip` 保持只读，SHA-256 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`。

按 `contracts/prototype-mapping.md` 已授权范围，实际构建源 `frontend/src/pages/a.html` 的原产品型号位置接目录下拉，原只读配方 ID 接所选条目，增加只读版本/受限状态；顶部当前配方名/版本改为数据位置，原弹窗“保存”改为预置配方“选用”，没有编辑保存或独立调试页。页面源当前 SHA-256 `9D6C11B21F4EB22CF09BBD34DCEDF24B15283BD06AEEB2DE3DFE8EAC47BCC925`；构建输出 `frontend/dist/a.html` SHA-256 `C7FE15DBA7A2B4323620393EC151CD8E33006EE7AECCCCAA2CE2A1C2B15BC90B`。无关登录、导航和布局未改。

实际执行的 `frontend/src/runtime.js` 从授权的后端 `/recipes/catalog` 加载条目，展示 `Restricted` 原因；仅允许选 `Available`，并在 2.0 准备请求中校验所选 `recipeId/version/catalogDigest`、场景和用途一致。当前 Q01 被后端标 Restricted，前端不能将其选作可启动成功配方。`npm run build`、`node --check frontend/src/runtime.js` 及 `node --test tests/us1/runtime-007.test.ts` 13/13 通过；参数化夹具后，旧 007 样本断言已按实际脚本结构同步，旧 1.0 Test 入口仍限原 S1/P01。上述仅证明构建与模拟页面合同，不是 WPF 页面交互通过。

`runtime.js` 还按已有后端事实处理 401/403 为权限受限、202 后短暂 404 为继续查询原请求，并优先显示运行查询的 Blocked 状态；没有从 HTTP 202 或页面阶段推定 Final。这些代码路径未取得当前正式 WPF 操作证据。

待完成：003 T068 的完整查询投影、007 T031 可执行 Q01 准备结果、正式 WPF 选用→请求与权限拒绝/通知核对，以及上述状态路径的当前页面证据。原型归档未改；旧页面心跳 Blocked 保留。

## 第三批同页增量（T048/T049仍部分完成）

在`frontend/src/pages/a.html`既有运行操作行最小增“确认已取盘”按钮和原因输入；`runtime.js`只在GET的`allowedActions`含`ConfirmManualTrayRemoval`时启用，提交requestId/expectedRevision/reason到正式POST并重查同run。页面在已有结果区展示已提交对象结论及来源、期望配方与实际冻结配方版本；无结果保留空/受限，不预填成功，Final仍看提交事实。未新建页面或配方编辑器。当前页面源SHA-256 `F60B346425DF545328D0B096A19D7C61F4D64F3160A77672CB4A11D0AFB082F2`，runtime摘要`5FEDD162AF5682DC13F494081C6824D8FA593253AB65DC2DA7123490958DC74E`，构建页面摘要`C4A1D1A8BBC059FC0A972F8C898C53BF6A0F85D9092239D4C36015DB6C17D726`。原型ZIP摘要仍为`3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`，未改。

`npm test`在修复测试模拟DOM后35/35通过；其中新增用例验证后端未允许时按钮禁用、允许后页面POST携本轮revision及原因。它是运行时代码测试，**不是实际WPF点击**。本机`quser`只见administrator会话2为Disc；没有可操作桌面，正式页面操作与心跳复核仍Blocked，保留旧失败证据。


## 2026-09-27 实际权限拒绝页面子证据

r20-auth-0927原job000真实401但页面状态丢失使整包Failed，保留原证据；最小runtime状态绑定后job001实际401、job002实际403各9/9且exit0/cleanuptrue。仅CDP实际POST请求身份去掉/使用有效无Run.Start权限的EquipmentEngineer，Host真实拒绝；没有Fetch.fulfillRequest或响应替身。六SQLite表/业务运行/PLC动作均0，页面StartFailed与权限受限保持。随机Test凭据不写日志/CLI，常规启动默认不开此工具。

四份HTML结构/文字/控件及客户ZIP SHA不变，只绑定既有状态/权限拒绝文案；当前r22正常Q18和完整恢复在同前端runtime真实通过。此子证据不等于生产登录/完整权限系统或006父任务全完成。详见.specify/bugs/006-start-permission-display及008 completion-review最新节。
