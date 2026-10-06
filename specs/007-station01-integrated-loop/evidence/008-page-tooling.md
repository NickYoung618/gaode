# 008第三批页面采证工具（007 T033部分）

`scripts/capture-station01-webview2-normal.cjs`新增`Recipe <recipeId>`模式：通过实际WebView2 CDP鼠标/键盘打开既有配方窗、选择条目、点击选用与启动；在后端允许且按钮启用时点击同页取盘确认。保留请求、通知、页面诊断、截图及错误包，脚本只驱动页面，不调用页面内部业务函数或辅助POST。选择受限配方时报错并保存当前状态，不伪称完成。`node --check`通过。

2026-09-25 `quser`只见administrator会话2为`Disc`。未运行WPF工具；页面心跳、取盘点击、SQLite/worker/PLC同run以及Final证据缺失。007 T033未勾，旧007成功包不能抵扣本批。

## 第五批实际页面采证

Session 2 后续恢复Active；`scripts/verify-q01-q02-test-page.ps1`在隔离Test根调用同一`scripts/capture-station01-webview2-normal.cjs`，由WebView2 CDP鼠标/键盘选配方、点击启动和同页取盘确认，没有内部业务函数或辅助POST。Q01与Q02包均有页面交互、网络/通知、截图、组件摘要、Host/VirtualPlc日志、worker/SQLite/媒体及Final关联，见[008第五批证据](../../008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md)。原失败包保留。Q01采证器旧版终止文字判断误报DeadlineExceeded，已用独立只读页面截图和同run提交事实核对；Q02采证器修正后返回FinalPageDisplayed。T033工具交付已实用，但006 T048/T049的全部原验收条件尚未逐项关闭，故暂保留任务未勾。
# 第八批自动多面采证进度（2026-09-25）

继续复用已有正式WPF/WebView2采证工具；Q03目录仍Restricted，没有合法页面启动条件，本批未运行工具生成Q03页面包，也不以API或Test翻面后组件代替点击。媒体查询现可返回已提交的面/轮身份，待实际Flip取放通信合同明确并接入后再由同一工具采集Q03至Final。007 T033原完成条件和未勾状态保持。详见[008第八批证据](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。


## 2026-09-27 monitor-xyz-history 最新监控纠正
此前独立完整XYZ栏目和XYZ公开信号命名要求由本轮用户确认取代：原列表完整XYZ逐信号发送/实际反馈、同值保留、公开XY_Move_Cmd/XY_Pos_Confirmed恢复来源Word。设计先同步，原项目源码已修改。见[本轮证据索引](../../../.specify/bugs/monitor-xyz-history/20260927T130246460967Z/evidence-index.md)。源码/最终包各13项、真实Edge原始证据重放7场景、新编译VirtualPlc只读地址表/资源核验通过；最终包截图与载荷摘要可查。
新包 `Gaode-008-Windows-x64-20260927-monitor-history-r8.zip`，SHA256 `d6ef8681580a995da99cf3545b96d09d615dc3ca20605ee9a5a59dfc021fbbf5`；相较r7共7项载荷改变、270项不变。新VirtualPlc来自当前源码构建，旧Host/worker/输入按摘要复用。历史29动作/87轴、r3/r4/r5/r6/r7与原失败保持原适用范围；本轮重放不是新业务run。原RunningF本机日志及七格映射仍待，xyz-sorting-deployment整体partial不变；不宣称008整体通过，不改任务勾选。


## 2026-09-29 公共解锁显示与测试监控布局 r9
本轮沿monitor-xyz-history追加：[评估/修复/验证及原始证据](../../../.specify/bugs/monitor-xyz-history/20260929T092100Z/evidence-index.md)、[当前一致性对照](../../../.specify/bugs/monitor-xyz-history/20260929T092100Z/alignment.md)。
用户上传原始审计write155/transaction6001证实PC实际解锁写入，旧页面漏显示同值0。当前原列表复用audit显示该次真实命令并关联PalletLock动作，状态1→0继续来自changes；不改PLC/Host业务。历史为首屏主要区域，支持搜索/方向、暂停查看但继续接收、滚动冻结、恢复最新、专注记录。
源码与解压包各16项必要检查通过，真实Edge原始审计重放及包内VirtualPlc 48点位只读运行通过；进程和端口已释放。生成图仅布局概念，实测截图独立提供。
最终包：[Gaode-008-Windows-x64-20260929-unlock-monitor-r9.zip](../../../artifacts/Gaode-008-Windows-x64-20260929-unlock-monitor-r9.zip)。SHA256 `d921bef46a8ba41c2ab2c6f0455f6454a53691d1f6dce4dc1d21454825f1e12d`。277载荷，7项本轮变化、270项与r8精简包一致，30个既有入口保留。直接解压运行已验证受影响VirtualPlc/资源；未变业务组件按摘要复用原证据，未新增完整配方运行。
通用旧打包入口丢失精简选择入口及带入额外资源的问题已按补充评估修正为显式r8基线归档。r8原包、r3/r4/r5原始运行/失败与29动作87轴范围保留；旧报告不是当前布局。原RunningF待采证、七格正式映射未确认和真实设备/标定事项不由本次完成。原003 T065、008 T055/T070构建范围及任务勾选不变；不将监控通过冒称008新增整体验收。
