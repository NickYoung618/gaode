# 2026-09-24 T046 诊断增量验收记录

前端在原型已有 `faultList/itemList/verdictBig` 位置消费正式 POST 错误与运行/PLC GET 的脱敏诊断；保留 `requestId/commandId/runId`，显示 `Test/VirtualPlc` 来源。已有 `runId` 时重复点击仅说明受限原因，不清空任务、不自动重发。本轮 `npm test` 24/24、`npm run typecheck` 通过，包含通信无法确认安全、可靠反馈明确不安全和重复点击的运行时测试；这属于代码级证据，不是页面截图。

实际 WPF/WebView2 页面启动入口曾尝试在交互会话运行并截图，尝试记录位于 `artifacts/station01-007/diagnostics-unsafe-20260924-*`。未取得有效页面点击和显示截图，因此 API/进程内测试不能代替 T046 页面验收。T046 保持未完成；不得把页面未显示推断为业务原因已修复。

## 2026-09-24 后续实际页面验收

上述未完成结论是前一轮状态，保留历史。后续使用会话2中实际 `Gaode.Station01.Desktop.exe` 的WebView2本地 `appassets.local` 页面，通过页面坐标鼠标事件点击原型已有登录和启动按钮；Test限定的本机调试端口只用于此页面取证，不提供设备/业务桥接。程序/资源SHA-256、会话、PID和隔离端口见各包 `interactive-desktop.json`、`process.json`。截图均由该WPF实例的WebView2渲染器取得，不是普通浏览器、辅助API或静态拼图。

- A有效样本：`artifacts/station01-007/page-diagnostic-communication-valid-20260924-0250/` 的 `02-before-start.png`、`03-after-receipt.png`、`04-failure-displayed.png`、`05-second-click.png`、`06-after-reload.png`、`webview2-page-evidence.json`。点击前正式GET确认Host/PLC可靠就绪；实际页面POST一次202，随后注入通信故障。页面显示`WaitingClamp`、无法确认设备安全及仅查询/人工核查，202只显示已受理；第二次点击明确“已有运行，不能再次启动”，POST总数仍1。页面重开后从GET恢复受阻事实。
- B对照：`artifacts/station01-007/page-diagnostic-unsafe-final-20260924-0245/` 同名截图及请求记录。页面显示`StartupReadiness`、设备可靠反馈明确不安全及授权人员现场排查指引，未混同通信失效；二次点击仍无重复POST，重开后保持正式查询事实。
- 前端 `npm test` 24/24、`npm run typecheck`通过；乱序通知事实源规则由既有前端测试核对，实际页面已验证重开重取，但未向页面伪造PLC反馈。重复点击提示曾被刷新覆盖，已改为与当前GET原因并存且不保存旧安全分类；中间23/24失败测试及其修正保留本轮记录。

两场景的页面请求、正式API/PLC事实和退出后哈希索引由007 `diagnostic-validation.md`汇总。首次页面A尝试 `page-diagnostic-communication-final-20260924-0240/` 因点击前Host已经心跳失效，不计入受理后注入验收；有效A样本独立重做。T046在Test/VirtualPlc实际WPF/WebView2范围完成；不宣称真实设备或原始人工故障修复。

## 2026-09-24 独立缺陷：启动配置版本页面拦截

本段只记录 `station01-start-config-version-mismatch` 的局部修复，不改变上方T046历史验收。006合同已规定具体版本来自007冻结Test样本；页面此前把公共/模拟版本限定为旧值，导致当前007请求在POST前被拦。修复后页面只核对受控Test请求的结构、身份和S1/P01上下文，原样传递三个配置引用；版本存在性、兼容关系和安全门禁仍由Host判断。`frontend/tests/us1/runtime-007.test.ts` 从007样本读取引用，并核对准备/启动脚本与样本一致；正反向页面测试、后端配置拒绝回归和隔离构建结果见 `.specify/bugs/station01-start-config-version-mismatch/fix.md`。本轮未点击实际WPF/WebView2页面，006 T044保持原状态；当前运行桌面仍加载旧资源，不能把代码级通过写成页面验收。
