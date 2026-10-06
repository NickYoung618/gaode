# 008第三批页面采证工具（007 T033部分）

`scripts/capture-station01-webview2-normal.cjs`新增`Recipe <recipeId>`模式：通过实际WebView2 CDP鼠标/键盘打开既有配方窗、选择条目、点击选用与启动；在后端允许且按钮启用时点击同页取盘确认。保留请求、通知、页面诊断、截图及错误包，脚本只驱动页面，不调用页面内部业务函数或辅助POST。选择受限配方时报错并保存当前状态，不伪称完成。`node --check`通过。

2026-09-25 `quser`只见administrator会话2为`Disc`。未运行WPF工具；页面心跳、取盘点击、SQLite/worker/PLC同run以及Final证据缺失。007 T033未勾，旧007成功包不能抵扣本批。

## 第五批实际页面采证

Session 2 后续恢复Active；`scripts/verify-q01-q02-test-page.ps1`在隔离Test根调用同一`scripts/capture-station01-webview2-normal.cjs`，由WebView2 CDP鼠标/键盘选配方、点击启动和同页取盘确认，没有内部业务函数或辅助POST。Q01与Q02包均有页面交互、网络/通知、截图、组件摘要、Host/VirtualPlc日志、worker/SQLite/媒体及Final关联，见[008第五批证据](../../008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md)。原失败包保留。Q01采证器旧版终止文字判断误报DeadlineExceeded，已用独立只读页面截图和同run提交事实核对；Q02采证器修正后返回FinalPageDisplayed。T033工具交付已实用，但006 T048/T049的全部原验收条件尚未逐项关闭，故暂保留任务未勾。
# 第八批自动多面采证进度（2026-09-25）

继续复用已有正式WPF/WebView2采证工具；Q03目录仍Restricted，没有合法页面启动条件，本批未运行工具生成Q03页面包，也不以API或Test翻面后组件代替点击。媒体查询现可返回已提交的面/轮身份，待实际Flip取放通信合同明确并接入后再由同一工具采集Q03至Final。007 T033原完成条件和未勾状态保持。详见[008第八批证据](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。
