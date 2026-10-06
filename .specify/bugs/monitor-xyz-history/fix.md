# Bug Fix: 原列表完整XYZ及协议XY命名
- Slug: monitor-xyz-history（explicit）
- Assessment: ./assessment.md
- Status: applied

## Summary
完成先设计后代码。原historyList融合非坐标changes与动作audit逐轴真实发送/反馈，同值/零值保留，按动作事件序号去重，缺失不补造。独立motion栏目/样式/DOM检查删除。公开XY命名恢复，协议地址/值/握手/运动不变。

## Changes
|文件|变化|
|---|---|
|VirtualPlc/wwwroot/app.js|原项目修复/验证/打包接线|
|VirtualPlc/wwwroot/index.html|原项目修复/验证/打包接线|
|VirtualPlc/wwwroot/styles.css|原项目修复/验证/打包接线|
|VirtualPlc/PlcAddressMap.cs|原项目修复/验证/打包接线|
|scripts/tests/virtual-plc-monitor.test.cjs|原项目修复/验证/打包接线|
|packaging/windows-local-20260927/Start.ps1|原项目修复/验证/打包接线|
|packaging/windows-local-20260927/README.md|原项目修复/验证/打包接线|
|packaging/windows-local-20260927/build.py|原项目修复/验证/打包接线|
|specs/003-plc-latest-protocol/spec.md|当前设计/任务说明同步（不改既有勾选）|
|specs/003-plc-latest-protocol/plan.md|当前设计/任务说明同步（不改既有勾选）|
|specs/003-plc-latest-protocol/tasks.md|当前设计/任务说明同步（不改既有勾选）|
|specs/007-station01-integrated-loop/spec.md|当前设计/任务说明同步（不改既有勾选）|
|specs/007-station01-integrated-loop/plan.md|当前设计/任务说明同步（不改既有勾选）|
|specs/007-station01-integrated-loop/tasks.md|当前设计/任务说明同步（不改既有勾选）|
|specs/008-recipe-driven-inspection/spec.md|当前设计/任务说明同步（不改既有勾选）|
|specs/008-recipe-driven-inspection/plan.md|当前设计/任务说明同步（不改既有勾选）|
|specs/008-recipe-driven-inspection/tasks.md|当前设计/任务说明同步（不改既有勾选）|
|specs/003-plc-latest-protocol/contracts.md|当前设计/任务说明同步（不改既有勾选）|
|specs/003-plc-latest-protocol/contracts/virtual-plc-boundary.md|当前设计/任务说明同步（不改既有勾选）|
|specs/007-station01-integrated-loop/contracts/virtual-integration.md|当前设计/任务说明同步（不改既有勾选）|
|specs/008-recipe-driven-inspection/contracts/execution.md|当前设计/任务说明同步（不改既有勾选）|

## Local Verification
node --test scripts/tests/virtual-plc-monitor.test.cjs：13/13。真实Edge重放原Q01/NG/Pending/Q03及三种错误场景7/7；新源码dotnet build成功0警告0错误。新编译VirtualPlc独立临时端口仅GET address-map/app.js验证通过，进程/端口已释放。

## Data provenance
发送来自本次引用、同连接、上一运动命令之后且本命令之前的rawWords收据，不以target补值；实际反馈来自DeviceActionAudit.actual，非轮询快照。ZReset只保留本次实际Z；其他状态/清零仍用changes。特殊HTTP动作无Modbus寄存器，缺发送UTC明确未提供。旧协议名字按稳定地址映射当前公开XY名，只改重放显示不改历史文件。

## Deviations / Limits
未改共享API字段。验证增加独立编译地址表/静态资源GET，未运行Host/配方。CLI首次截图未正确展示列表（含空白截图），保留原文件；改用CDP真实滚动后截图。原业务29动作/87轴只作旧run复用。本轮不解决用户原RunningF日志缺失或七格映射、不宣布008收口。

## Next
接续speckit-bug-test：最终新包资源解压核验、13项必要检查与浏览器截图、设计一致性/来源保护检查。

本轮完整报告：20260927T130246460967Z/fix.md
