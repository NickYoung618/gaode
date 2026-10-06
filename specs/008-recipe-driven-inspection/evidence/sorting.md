# T057 当前分拣交付与验证

2026-09-27：`SortingTargetAllocator` 使用当前合法 Test 真实源点/sourceSlot/protocolSlotIndex 与已配置目标点。不同应搬实体目标冲突、已占源点、NG/Pending容量不足或已有预留时拒绝依赖动作，不发明替代格位。预留必要提交在下料前；实际取料完成 status2 后保存 InTransit，成功后才允许放料；放料 status3 与 ACK 清零后保存 Occupied。保存未确认保留既有事实并阻断后继，不以 status2 作为整项完成。

源码：Application/Workflow `SortingTargetAllocator.cs`、`RecipeSortingMapper.cs`、`ThreeStageWorkflowExecutor.cs`；实际 PLC 唯一适配仍为003，不在008另发硬件命令。关键存取2秒、每段XY8秒与ACK2秒也受绝对冻结期限限制。

r5/tests/sorting-budget-affected.trx 41/41（含容量冲突、目标占用、预留保存失败、取料提交失败不放料）；局部运动期限修正验证 sorting-motion-timeout.trx 1/1，最后源码守卫5/5。集合重叠，不相加。

新构建 Q01-NG/Q02-PENDING、成组问题成员及整体一次处置的正式 WPF 与 SQLite 预留→在途→占用顺序待已结束包登记。旧 r4 成组取放成功只证明旧构建明确子范围，不抵新预留实现。原任务保持未勾。生产真实布局/容量/采样限制沿 input-readiness 逐使用点保留。

### r5 job-002 Q01-NG 正式通过（2026-09-27）

runId `4208c38d-d5db-4345-9571-46b3fcd97821`，exitCode0、cleanupVerified=true，页面NG保持至Final/刷新/重开；独立worker实际产生结论，原操作验证及场景审计通过。新实现持久读回 `sorting-commit-audit.json`：P01/protocolSlot1→P14，预留持久时间16:05:49.815690Z先于下料；取料status2后InTransit持久16:05:59.133172Z，实际PlaceWrite16:05:59.238729Z；放料status3/ACK清零后Occupied持久16:06:05.745645Z。6项直接顺序/身份检查全部true。不是状态2完成冒充整体处置，也不是日志替代实际反馈。

同一桌面worker自动接续job-003 Q02-PENDING；批次调度端每条开始后保留暂停信号，结束复核exit/cleanup及场景读回后才放行下一条。调度自身UTC转换准备失败保留为dispatcher-preparation-failure.json，修正版deadline检查真实UTC通过，未中断当前worker或作业。

### r5 job-003 Q02-PENDING 正式通过（2026-09-27）

runId `e3072005-0e53-4160-b36f-e5a9614e769f`，两非连续槽P01/P03，实际worker使P01为Pending、P03为OK，只有P01/protocolSlot1搬至P15。exitCode0、cleanupVerified=true，完整操作验证、场景读回及sorting-commit-audit七项通过。Pending维持至Final及刷新/重开，没有用“完成”代替质量或将其他正常实体搬走。原取料2→在途可靠提交→实际放料写入→放料3/ACK清零→占用提交顺序成立。当前同一worker自动进入job-004 GROUP-F-MIXED。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
