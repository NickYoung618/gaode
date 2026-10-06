# T063 成组交付

当前 IntegratedDetectionPort / FaceResultAggregator 保留所有实际组成员和必检面，完整性与质量分开；RecipeSortingMapper 只选问题成员，NG优先且保留Pending明细，正常成员不随组剔除。新目标预留见 [分拣记录](sorting.md)。

r4 GROUP-F-MIXED 与 GROUP-F-PENDING 的两组/四成员/六面、单个问题成员取放及三个OK保留已通过旧构建明确子范围，runId及原包见 [持续实施记录](implementation-night-20260926.md)。r4 GROUP-A-E真实Final但采证第一页遗漏ACK，原失败保留。新r5 GROUP-F-MIXED及GROUP-A-E完整正式对账尚待结束；不将前缀或组层判定当作成员完整验收。父任务未勾。

### r5 job-004 GROUP-F-MIXED 已结束子范围通过（2026-09-27）

runId `1b6869ad-de49-4e66-a419-4ca2425553a2`，两组/四成员/六面，八项场景审计true、operation-route-validation通过、exitCode0及cleanupVerified=true。NG优先且Pending面明细保留，仅P01/M01实际取放一次，其余三OK保留；新分拣提交审计七项true。实际旧媒体/结果刷新、登录重开与顶部配方恢复均通过。处置查询字段缺口已登记008-disposition-projection，不能由本子范围通过推称全部结果投影齐备；旧包不补写新字段。当前同一worker自动接续整体NG。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
