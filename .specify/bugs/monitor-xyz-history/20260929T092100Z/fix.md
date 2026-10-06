# Bug Fix: 公共解锁写入与测试监控布局
- Slug: monitor-xyz-history（会话既有目录，追加报告）
- Assessment: ./assessment.md
- Status: applied
- Date: 2026-09-29

## Summary
按评估先同步003 spec、virtual-plc-boundary、plan及T062增量，追加独立诊断页面设计，再修改原项目静态前端。PLC/Host业务源码和共享API不变。

## Changes
- VirtualPlc/wwwroot/app.js：0x23命令从既有audit唯一投影，含同值、拒绝、序号/连接/事务及原UTC；PalletLock真实写引用关联动作，不补内部锁紧命令。状态反馈保持changes来源。后台接收与暂停查看分离，筛选/方向/恢复/清空/专注记录。
- index.html、styles.css：历史首屏大区域、固定列头、紧凑行、关联详情折叠、实时点位侧栏，保留状态与错误分类；页面版本unlock-history-r9。
- scripts/tests/virtual-plc-monitor.test.cjs及fixtures/virtual-plc-unlock-audit.json：从原上传文件逐字段选取真实write155/action12/change310；16项必要检查。拒绝/新增记录行为是明确受控夹具，不冒称原实际运行。
- packaging/windows-local-20260927/{Start.ps1,build.py,README.md}：版本接线、新包修复说明，移除当前说明中旧独立XYZ栏目说法。
- 003独立监控页面设计及003/007/008证据入口跟随本轮记录，不改任务勾选。

## Local Verification
源码16项检查通过。当前VirtualPlc构建0警告0错误。实际Edge原上传记录重放：搜索/方向、暂停仍GET、恢复、清空等通过；1600窗口历史664.5px，1366窗口440.5px，无页面整体垂直溢出/历史横向溢出。新编译VirtualPlc GET与实际Edge读取地址表/资源通过，所属进程和端口已退出。新包解压后的最终验证接续test.md。

## Deviations / failures preserved
首次布局检查634.5px未达到预定650px，已压缩标题和工具栏并重新验证。初始只读实例会产生PLC_Mode_Auto初始化变化，因此“空闲时必须零条历史”的测试假设错误，改验无写入/运动记录及activeAction为空；不修改实际设备记录。第二次复用旧浏览器profile留下DevToolsActivePort造成连接失败，验证工具改用每次独立profile。保留相应日志。
本次打包目录包含旧脚本复制但manifest明确排除的win-x64重复副本；删除暂存副本的操作被自动策略拒绝，未执行。为保留所有内容改用Windows compact仅压缩本次新建暂存目录，文件字节及交付摘要不变。未改全局策略或旧目录。

## Scope
设计图为内置imagegen生成的布局概念，图中示例数据不作为协议或证据。来源Word及客户原型只读；保留r8和旧失败；正式业务/七格映射/真实设备验收不由本次完成。

## 最终打包补充
按assessment-supplement-packaging.md修正build.py/archive.py，显式冻结r8-minimal基线，恢复Select-Test/Start-Test/测试配方清单到项目打包源目录；最初通用候选不交付。最终ZIP277项载荷、7项变更、270项不变、30入口保留。详细test.md；本轮新报告只增补，不改旧失败结论。

## 暂存归档位置
本次未采用的通用打包暂存已完整迁移到会话另一workspace：C:/Users/codexsandboxonline.10_3_0_13/.codex/visualizations/2026/09/27/01a0e069-3811-7aa2-9e28-9404084c8fa7/r9-general-staging-20260929；失败ZIP同目录r9-failed-general-package-20260929.partial.zip。保留失败文件和原build-result中的历史路径；最终有效包仍在E:/dzk/gaode-1/artifacts，E盘空间已释放。
