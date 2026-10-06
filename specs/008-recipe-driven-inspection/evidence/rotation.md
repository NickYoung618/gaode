# T065 特殊旋转交付

当前 RecipeExecutionCoordinator / IntegratedDetectionPort 复用003现有 Test Enter/Rotate/Exit请求与实际设备结果，占用实体、批准姿态与原槽关联保存。角度和机械过程由虚拟下位机执行。特殊出口分别为OK原槽、NG区、Pending区，可靠完成后释放；SpecialHandlingCompleted排除外层普通重复分拣。

旧后端三个出口与整体OK子证据不能冒充新正式页面。r5选ROT-PART-OK/NG/PENDING及ROT-ASSEMBLY-OK验证实际差异；未知占用/错姿态门禁按原必要测试适用范围复用，不全配方×故障穷举。当前正式结果尚待结束。

2026-09-27 新结束证据：resume-1 job010 ROT-PART-NG run `08eec80b-4b87-41cf-bb08-fb25e6e934e2`，outer0/cleanuptrue，操作17项、场景5项、实际预算/调用12项通过。真实Enter→pose1/pose2 Rotate→NG Exit四回执Completed，Exit后occupiedEntityId为空，普通取放0/0。两面独立结果/媒体与页面NG刷新/重开保持，初始Height/FDecode各一次、Detection六次。实际实体/姿态/出口来源仍只为已批准Test，生产角度/寄存器没有扩展。OK旧job009导出失败保留，新job022复验等待；Pending/整体差异继续。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
