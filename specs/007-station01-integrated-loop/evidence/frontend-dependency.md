# T002：006 页面消费者交付核对（2026-09-24）

本记录只登记 006 可供 007 消费的能力。实际 WPF/WebView2 与当前 Host 的连通、401/403、通知和完整运行另归 006 T044、007 T015，不由 T002 代验。

| 完成条件 | 证据与本轮核对 | 结果 |
| --- | --- | --- |
| 文档对齐 | `specs/006-frontend-station01-console/` 的现行 `spec.md`、`plan.md`、`tasks.md`、`contracts/api.md`、`contracts/prototype-mapping.md`、`contracts/gaps.md`；T042/T043 保留既有完成记录，T044 未勾选 | Passed |
| 合法 Test 启动请求、受控凭据 | 当前 `frontend/tests/us1/runtime-007.test.ts` 从 007 冻结样本读公共/模拟 1.2.0、预算 1.1.0，检验引用不改写、一次点击一次 POST、无凭据 401 和 202 非完成；本轮与通知顺序测试合计 14/14，类型检查退出 0。三份历史 WPF 包均有合法 `contextJson`、一次页面 POST/202 和同一 runId | Passed，交付能力可消费 |
| GET/通知驱动的阶段及最终状态绑定 | 当前 `frontend/src/runtime.js`、前述测试及 `frontend/tests/contract/notification-order.test.ts` 验证通知仅触发 GET 重取、旧 revision 不覆盖事实、Final 只取后端最终事实。历史三包 `after-202.png` 为“已受理”，`final-page.png` 为“完成”并注明 Test/Simulated 受控客户端，另有后端 GET、SQLite 对照 | Passed，代码和历史页面范围；本次实际通知链路待 T044/T015 |
| 原型不变 | 只读核验 `E:\dzk\gaode\原型.zip` 包含 `a.html`、`data-view.html`、`login.html`，SHA-256 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`；当前 `frontend/dist/runtime.js` 和桌面内嵌副本均为 `8FBB2CB4309839389416F340C615052F623D3270B17EA65441CBBE20420C0082`，与历史三包页面资源一致 | Passed |

历史页面原始路径：`artifacts/station01-007/heartbeat-accept-01-20260924/interactive-session2/`、`heartbeat-accept-02-20260924/interactive-session2/`、`heartbeat-accept-03-20260924/interactive-session2/`；各包有页面操作、网络回执、截图、GET/PLC/SQLite 和退出后索引。其 Host 构建哈希 `42ACEFA5…C78C8` 不等于本轮独立平台构建 `794F5833…2DDC`，故不能直接抵扣当前构建 T015。旧 `artifacts/frontend/007/webview2-host-transcript.json` 记录的是较早 Runtime 缺失受阻，作为历史保留，不代表当前页面仍缺 Runtime。未获批准的七格媒体映射属于 006 T045，不在此登记为通过。
