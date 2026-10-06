# Bug Verification: 同值解锁与测试监控布局 r9
- Slug: monitor-xyz-history
- Assessment: ./assessment.md；打包增量 ./assessment-supplement-packaging.md
- Fix: ./fix.md
- Date: 2026-09-29
- Result: verified（本轮显示、交互、设计接线及最终包范围）

## Checks Performed
|检查|真实执行及证据|结果|
|---|---|---|
|源码必要检查|node --test scripts/tests/virtual-plc-monitor.test.cjs；source-tests-final.log|16/16|
|原始解锁复现回放|上传write155/transaction6001/action12/change310；既有最近记录列表展示真实写入0及独立状态1→0|pass；不造前值|
|当前编译|dotnet build VirtualPlc/VirtualPlc.csproj -c Debug --artifacts-path artifacts/unlock-monitor-r9-build -v minimal|0警告0错误|
|源码真实浏览器|python -X utf8 本目录/verify-browser.py VirtualPlc/wwwroot source-final|5类交互/布局检查，无脚本异常|
|源码实例|python -X utf8 本目录/verify-compiled.py|新构建、真实Edge、48点位，只GET，无Host/运动；进程/端口已释放|
|最终包全部载荷|final-package-verification.json；CRC与SHA256逐文件校验|277项；仅7项变化，270项与r8一致，30测试入口保留|
|解压包必要检查|GAODE_MONITOR_SCRIPT=本目录/extracted/Gaode-008-Windows/VirtualPlc/wwwroot/app.js；node --test scripts/tests/virtual-plc-monitor.test.cjs|16/16|
|解压包真实程序|python -X utf8 本目录/verify-compiled.py 本目录/extracted/Gaode-008-Windows|48实际点位、地址映射及三个静态资源逐字节一致，无浏览器脚本异常；所属资源已释放|
|解压包原记录重放|python -X utf8 本目录/verify-browser.py 本目录/extracted/Gaode-008-Windows/VirtualPlc/wwwroot package-cdp-final|搜索、方向、滚动暂停且GET继续、恢复、清空不重放、专注模式；pass|
|版本/边界/设计|consistency-verification.json及alignment.md；Start/Select-Test源码及解压件PS AST|pass|

## 实际界面
- [1366×768解压包回放截图](browser-package-cdp-final-overview-1366.png)
- [同值解锁与真实反馈](browser-package-cdp-final-unlock-proof.png)
- [专注记录](browser-package-cdp-final-focused.png)
- [解压VirtualPlc真实空闲实例48点位](browser-package-live-idle-1600.png)
回放截图使用原始上传审计，上传未含state快照，因此明确标识离线重放/无实时快照，不从历史重造当前状态。真实实例截图是本次空闲实例，不能混称为原运行快照。imagegen概念图不是验收截图。

## 失败与调整
原小区域实现布局测得634.5px，不达预定650px，缩减非记录区后1600窗口实测664.5px；1366窗口440.5px，无页面整体垂直溢出及历史横向溢出。
初始空闲实例有PLC_Mode_Auto初始化变化，修正验证器“零历史”错误假设；仍严格验证无实际写入/运动。复用浏览器profile导致旧调试端口连接失败，改为独立profile。包回放第一次用Performance资源计数观察后台GET未能通过；改用CDP Network真实请求事件计数，得到可核对的持续读取证据。失败日志均保留，不将其写成已通过。
旧通用打包与r8精简入口不一致，失败候选磁盘不足；保留失败文件，按补充评估修正基线打包，最终277载荷/30入口及7项差异已核验。

## Limits
只解压并实际执行受影响VirtualPlc及静态资源，其他所有ZIP载荷通过CRC/摘要核验，与r8相同业务文件复用既有证据。未重跑Host/配方/算法/完整Q01—Q22业务；不能称新包整矩阵实跑。原RunningF待采证、七格正式映射、真实设备/现场标定不由本轮关闭。003 T065、008 T055/T070和其他任务勾选原样保留。
