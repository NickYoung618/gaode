# 第三批保存与查询边界（008 T054部分）

现有TraceWriter/StageEventStore和MediaStore保持唯一持久化路径；产品端口原有单图意图、媒体、算法事实保存路径加入同面双输入意图/结果保存。提交失败会抛出或返回Failed，外围不得由缺结果进入Final。008严格运行的Pending不能用旧`frozen-plan`零坐标补位；缺实际位置时提交人工复核事实并阻断Sorting/Final。严格运行的NG/Pending在真实目标未配置时提交`SortingTargetUnconfigured`，不以步骤序号冒充物理槽。GET运行投影只从已提交Detection Completed事件读取对象结论；未提交返回空，movements在缺实际搬运事实时为空。原有WholeTray→解锁→页面取盘→Final保存链仍由003负责。

本次只做构建、静态门禁、模拟媒体与PLC组件验证；合法Q01目标仍缺，没有通过正式路线产生可读回的面/对象/位置/Final事实，也未执行本次必要保存失败注入。因此008 T054和003 T069均未满足完整验收，保持未勾。
`DetectionRetryAndPendingTests.StrictRecipePendingWithWrongObjectPositionCannotDispatchSorting`验证严格路线对象位置不匹配时保留人工复核事实、无PLC排序或Completed；历史007重试合同13/13经严格标记分流后保持通过。该内存事件测试不抵扣真实SQLite必要保存失败。

## 第四批更新

2026-09-27处置投影增量：当前源码沿同run/tray/plan已提交事件接入既有处置字段及ETag，预留/在途/完成/未知/明确无需搬运分开；特殊Exit提交补真实源槽和目标点。独立构建投影验证15/15、阶段生产者15/15通过。已结束r5 Q02-PENDING真实只读SQLite在新独立Host投影为P01→P15 Completed且质量Pending保留，P03缺旧无需搬运依据仍null、Face为null；scope为离线读回，不是新正式页面。见resume-2/ended-q02-projection-result-r2.json、008-disposition-projection/fix.md及partial test.md。当前队列因共享PLC启动通信阻断暂停，未关闭父任务。

上述“尚无读回/必要保存失败注入”是第三批时点记录。第四批AB/CD单面组件经真实SQLite提交并读回运动意图/事实、媒体、算法调用、复位和融合事实，磁盘媒体文件逐项存在；详见[第四批记录](fourth-batch-validation.md)。注入媒体元数据必要保存失败后端口返回Failed、动作占用Unknown，未发下一定位或完成。此范围未生成正式Q01位置/处置/Final事实，T054仍未满足原任务整项条件。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
