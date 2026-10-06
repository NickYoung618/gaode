# Bug Verification: 处置投影

- **Slug**: 008-disposition-projection
- **Tested**: 2026-09-27
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: partial

## Summary

必要投影与混合盘生产者验证通过，已结束真实SQLite事实在新独立投影中正确生成Completed及原质量。正式新run仍未完成，结果保持partial。r6 job001实际run cba580f7-64b0-4be0-b3c3-7abda4f91647在分拣前SortingTargetUnconfigured，无分拣派发；退出/清理true，保留失败。独立1.1.4补齐P03至已有P15 Test目标后job002在Host Connect超1秒、尚未WPF即失败并清理。有限PLC健康先于Host启动后的job014短WPF预检通过且无通信失败；job015正式复验进行中，不能以预检替代。

## Checks Performed

| Check | Action | Result | Notes |
|---|---|---|---|
| 纯投影及原结果回归 | CommittedDispositionProjectionTests＋CommittedResultProjectionTests | pass 15/15 | 预留/在途不完成，可靠回执/身份隔离、UnknownHeld、普通无需搬运和特殊占用释放 |
| 生产者与原阶段门禁 | ThreeStageWorkflowExecutorTests | pass 15/15 | 混合盘只搬问题成员，正常成员留原位提交；内存store/端口，无运行SQLite |
| 实际旧已结束事实 | 只读Q02-PENDING SQLite→新独立Host投影 | pass | 原run真实P01→P15 Completed；P03旧无依据仍null；不是新WPF |
| 正式页面原症状/P03代表 | 新run/Pending、同构建页面/API/已退出SQLite对账 | not-run | 共享PLC启动通信阻断；当前worker9936存活且暂停 |

## Residual Risks

新业务构建尚未用于正式WPF，特殊出口新增真实点位载荷及普通混合盘的新ordinaryOk仍待实际新run读回。不得据单元验证关闭父任务或将旧页面包改Passed。
