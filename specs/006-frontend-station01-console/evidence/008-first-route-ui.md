# 008正式页面 Q01/Q02 Test证据

Administrator Session 2 的正式 WPF/WebView2 页面通过CDP鼠标和键盘分别选择`R008-Q01/1.1.1-test`和`R008-Q02/1.1.1-test`、启动、在后端允许后点击同页取盘确认，并显示已提交的Final。页面交互、网络/通知、截图和同run SQLite/媒体/PLC/worker核对见[008第五批记录](../../008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md)。Q01旧采证器误报已单列，Q02自动判`FinalPageDisplayed`。页面没有直接控制设备或写数据库。006 T048所需401/403真实WPF拒绝和T049全部结果/媒体选择验收尚未核完，两项保留未勾。
# 第八批自动多面页面子范围（2026-09-25）

现有页面媒体格按已提交查询事实区分对象、面、轮次；同角色多份媒体可轮选，缺事实留空。前端语法及构建通过，媒体API定向测试1/1通过。Q03目录仍Restricted，未用正式WPF操作Q03，也未形成换面、取盘或Final页面证据；006 T048/T049整项未勾。见[008第八批证据](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。


## 2026-09-27 实际权限拒绝页面子证据

r20-auth-0927原job000真实401但页面状态丢失使整包Failed，保留原证据；最小runtime状态绑定后job001实际401、job002实际403各9/9且exit0/cleanuptrue。仅CDP实际POST请求身份去掉/使用有效无Run.Start权限的EquipmentEngineer，Host真实拒绝；没有Fetch.fulfillRequest或响应替身。六SQLite表/业务运行/PLC动作均0，页面StartFailed与权限受限保持。随机Test凭据不写日志/CLI，常规启动默认不开此工具。

四份HTML结构/文字/控件及客户ZIP SHA不变，只绑定既有状态/权限拒绝文案；当前r22正常Q18和完整恢复在同前端runtime真实通过。此子证据不等于生产登录/完整权限系统或006父任务全完成。详见.specify/bugs/006-start-permission-display及008 completion-review最新节。

## 2026-09-27 XYZ部署复测及I1—I4接续（partial）

本节接续当时构建范围，不覆盖既有T065/T055/T070通过及勾选。最新缺陷整体仍partial：原本机RunningF缺同run日志，七格正式业务/相机映射未确认；Test临时格位不宣称正式映射。r22 HTTP超期仍独立保留。

| 入口 | 实跑版本/run | 可复用范围与限制 |
|---|---|---|
| Q01 | r3 / 0b877606-15de-4688-aedb-61747485266c | 5动作；普通OK不盘末取放、下料命令4及Final；r3页面统计错位是原失败，后续绑定改动不能倒填r3 |
| Q01-NG | r4 / f51c4aa4-a5e3-4e4e-bc76-ed013d89daff | 7动作；NG源槽→同盘P14、取料2/放料3/Sorting_OK清零；原显示检查器误选5节点失败保留，实际截图及离线复核另列 |
| Q02-PENDING-P03 | r4 / c1049872-0aa7-417b-8ca3-fd738f2c9d46 | 9动作；P03→同盘P15、P01 OK留原位；DOM/监控通过，原Stop拒绝身份和后续按身份清理均保留 |
| Q03 | r4 / 17d03621-1923-422c-9b67-98c73308dc6a | 8动作；翻面目标/面反馈/Flip_OK清零、下料及Final；原Stop拒绝和独立清理保留 |

合计29个动作/87轴：同动作引用、连接、事务、命令前完整原始双字与实际反馈Float32位核验；相同Y仍有本次写入，不能用changes是否出现判漏轴。该范围是既有Test实跑，不是本轮新设备测试。
r3→r4仅两个runtime副本与README改变；r4→r5仅Stop.ps1/README改变、343文件同摘要。r5只实际启动/停止，无配方启动；不能称r5重新完成这四例。r5 ZIP SHA256为70C5D53EA4E5CC19070516943A736085903439A8996AD2CD6CD5BA62A83CDF2E，原包保持。
本轮r6仅合同/离线脚本、监控分类/版本及包说明接线：新包差异、解压资源和必要显示验证以新报告为准，业务证据按逐文件未变范围复用，不重复配方矩阵。索引更新不是新增运行验收，不能据此修改任务勾选。

- [原复测证据索引](../../../.specify/bugs/xyz-sorting-deployment/retest-20260927T085415Z/evidence-index.md)、[原验证报告](../../../.specify/bugs/xyz-sorting-deployment/retest-20260927T085415Z/test.md)、[原运行/摘要](../../../.specify/bugs/xyz-sorting-deployment/retest-20260927T085415Z/verification-proof.json)。
- [I1—I4增量修复](../../../.specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/fix.md)、[增量验证](../../../.specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/test.md)、[增量证据索引](../../../.specify/bugs/xyz-sorting-deployment/sync-20260927T100625Z/evidence-index.md)。

本轮明确新包：`E:\dzk\gaode-1\artifacts\Gaode-008-Windows-x64-20260927-xyz-sorting-r6.zip`，revision `xyz-sorting-r6-sync`，SHA256 `d5b3115bb9d9063d6d36ff4e3809fac8774d67c9e085653d0b700553f0edb0f3`。r5→r6仅README.md、Start.ps1、VirtualPlc/wwwroot/app.js、index.html四项载荷不同，341项未变（包括Host/PLC业务DLL、worker、前端、Stop及冻结输入）。新包仅显示/接线验证，详见增量报告，不计新配方运行。
