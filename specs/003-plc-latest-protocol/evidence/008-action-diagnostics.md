# 008动作诊断证据入口


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


## 2026-09-27 monitor-xyz-history 最新监控纠正
此前独立完整XYZ栏目和XYZ公开信号命名要求由本轮用户确认取代：原列表完整XYZ逐信号发送/实际反馈、同值保留、公开XY_Move_Cmd/XY_Pos_Confirmed恢复来源Word。设计先同步，原项目源码已修改。见[本轮证据索引](../../../.specify/bugs/monitor-xyz-history/20260927T130246460967Z/evidence-index.md)。源码/最终包各13项、真实Edge原始证据重放7场景、新编译VirtualPlc只读地址表/资源核验通过；最终包截图与载荷摘要可查。
新包 `Gaode-008-Windows-x64-20260927-monitor-history-r8.zip`，SHA256 `d6ef8681580a995da99cf3545b96d09d615dc3ca20605ee9a5a59dfc021fbbf5`；相较r7共7项载荷改变、270项不变。新VirtualPlc来自当前源码构建，旧Host/worker/输入按摘要复用。历史29动作/87轴、r3/r4/r5/r6/r7与原失败保持原适用范围；本轮重放不是新业务run。原RunningF本机日志及七格映射仍待，xyz-sorting-deployment整体partial不变；不宣称008整体通过，不改任务勾选。


## 2026-09-29 公共解锁显示与测试监控布局 r9
本轮沿monitor-xyz-history追加：[评估/修复/验证及原始证据](../../../.specify/bugs/monitor-xyz-history/20260929T092100Z/evidence-index.md)、[当前一致性对照](../../../.specify/bugs/monitor-xyz-history/20260929T092100Z/alignment.md)。
用户上传原始审计write155/transaction6001证实PC实际解锁写入，旧页面漏显示同值0。当前原列表复用audit显示该次真实命令并关联PalletLock动作，状态1→0继续来自changes；不改PLC/Host业务。历史为首屏主要区域，支持搜索/方向、暂停查看但继续接收、滚动冻结、恢复最新、专注记录。
源码与解压包各16项必要检查通过，真实Edge原始审计重放及包内VirtualPlc 48点位只读运行通过；进程和端口已释放。生成图仅布局概念，实测截图独立提供。
最终包：[Gaode-008-Windows-x64-20260929-unlock-monitor-r9.zip](../../../artifacts/Gaode-008-Windows-x64-20260929-unlock-monitor-r9.zip)。SHA256 `d921bef46a8ba41c2ab2c6f0455f6454a53691d1f6dce4dc1d21454825f1e12d`。277载荷，7项本轮变化、270项与r8精简包一致，30个既有入口保留。直接解压运行已验证受影响VirtualPlc/资源；未变业务组件按摘要复用原证据，未新增完整配方运行。
通用旧打包入口丢失精简选择入口及带入额外资源的问题已按补充评估修正为显式r8基线归档。r8原包、r3/r4/r5原始运行/失败与29动作87轴范围保留；旧报告不是当前布局。原RunningF待采证、七格正式映射未确认和真实设备/标定事项不由本次完成。原003 T065、008 T055/T070构建范围及任务勾选不变；不将监控通过冒称008新增整体验收。
