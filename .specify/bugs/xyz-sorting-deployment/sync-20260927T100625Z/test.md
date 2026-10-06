# Bug Verification: I1—I4同步修复

- Slug: xyz-sorting-deployment（explicit）
- Tested: 2026-09-27T10:13:10.262591+00:00
- Assessment: ./assessment-supplement.md及原assessment
- Fix: ./fix.md
- Result: verified（I1—I4限定范围）；缺陷整体仍partial

## 实际验证
|检查|真实命令/动作|结论及范围|
|---|---|---|
|I2旧XY完整汇总|python -X utf8 .specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/verify-offline.py|旧Q01/Q02证据副本完整脚本exit0；源64文件摘要不变；fixture仅投影原保存身份，不生成运行配方|
|I2名称与干扰|同命令|改名副本、异地址/异命令干扰完整汇总exit0；实际新XYZ Q01/Pending只核本轮两条运动表达式，缺下料且异地址同名不能通过|
|I3必要回归|GAODE_MONITOR_SCRIPT=新包解压app.js；node --test scripts/tests/virtual-plc-monitor.test.cjs|10/10，无Skipped；状态成功渲染错、HTTP失败、实际心跳超时/缺状态/恢复、完整XYZ同值/方向/旧页面版本|
|真实浏览器|python -X utf8 .specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/verify-browser.py 新包解压根|实际Edge读取解压资源，环回只读重放保存证据；HTTP/心跳/渲染异常由明确测试夹具控制，不是新设备故障或新业务运行|
|新包|python packaging/windows-local-20260927/build.py --plc-dir artifacts/xyz-r3-build/bin/VirtualPlc/debug；python packaging/windows-local-20260927/archive.py artifacts/windows-package-20260927T100843Z Gaode-008-Windows-x64-20260927-xyz-sorting-r6.zip --candidate；解压后CRC/摘要对账|345项摘要及src接线一致；新r6与旧r5四项载荷不同、341项不变。旧r5摘要未复用为新包摘要|
|I1/I4只读复核|python -X utf8 .specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/verify-consistency.py|以源码、实际包与原始证据核对，勾选/来源/历史摘要保护；结论见consistency-review.json|

## 新包
E:\dzk\gaode-1\artifacts\Gaode-008-Windows-x64-20260927-xyz-sorting-r6.zip
SHA256: d5b3115bb9d9063d6d36ff4e3809fac8774d67c9e085653d0b700553f0edb0f3
revision: xyz-sorting-r6-sync
实际解压根: E:\dzk\gaode-1\.specify\bugs\xyz-sorting-deployment\sync-20260927T100625Z\extracted\Gaode-008-Windows

## 边界与原失败
无Host/VirtualPlc/算法/WPF/配方运行，本轮显示重放不冒充真实设备通信定位。当前API无TCP连接状态字段，communicationTimedOut为心跳超时，只显示该事实；HTTP成功不单独认定PLC连接，字段缺失未知。
原r3/r4各失败、清理失败及后续复核保持；r3普通OK、r4 NG/Pending/Q03和r5启动停止按其真实构建复用。29动作/87轴为旧实跑范围。r6只变监控/版本/README，不重复矩阵或改业务期限。
该脚本未装入旧r5，也未装入新r6，属于仓库离线工具；不倒写包能力。客户原型/Word、地址/握手/运动源码及任务勾选不动。
本机原RunningF仍需原run日志和实际包摘要；七格正式映射仍需业务确认；r22 HTTP独立。I1—I4可关闭，整体缺陷partial，不能据此修改T065/T055/T070或宣布本机验收完成。

## 保留的验证工具失败
首次Edge渲染错误注入断言失败：内联赋值先于defer app.js，注入被函数声明覆盖，未触发目标边界。browser-first-attempt保存原DOM、stderr、脚本及failure.json；改为DOMContentLoaded后注入，实际Edge四项均exit0且断言通过，不改产品包、不启动设备。初次一致性保护检查的路径字符串斜杠差异造成tasks.md被错误纳入全文件摘要保护，修正验证器Path.as_posix后确认全部勾选行不变；不是任务勾选失败。
当前会话CIM进程枚举拒绝访问，未申请管理员、未操作未知进程；最终Get-Process msedge无残留，测试环回监听由server.shutdown/server_close释放。文件系统权限不冒充管理员权限。
