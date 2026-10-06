# 008 converge 第一轮：仍有现有任务工作

本轮在处置投影实现及必要验证后按speckit-converge执行，只读审查18项FR、6项SC、19项故事验收条件、8项计划决策和13项宪章原则。追加任务0项：当前缺口已由既有T049—T070承接，未重建任务或改变编号。无新增任务不等于验收收敛，当前Test范围和008整体均未完成。

审查覆盖目录合法组合及来源、RecipeRunPlanner线性计划、共享阶段执行器、公共初始高度/F、逐面采集与独立worker/输入释放、真实SQLite和媒体、身份及冻结配置、预算3.2、普通预留/取放提交、组/整体投影、旋转出口、人工换面、正常暂停和故障完整新轮、后端API/通知及原型页面绑定。实际实现位置包括CurrentJsonRecipeCatalog、RecipeRunPlanner、IntegratedDetectionPort、Application/Workflow/FaceResultAggregator、SortingTargetAllocator、ThreeStageWorkflowExecutor、RecipeExecutionCoordinator、QueryEndpoints及CommittedResultProjection。

| 分类 | 处理结果 | 继续归属 |
| --- | --- | --- |
| 原物理处置API缺口 | 实现提交事实投影、ordinaryOk及特殊出口实际点位载荷；15项投影/原结果与15项阶段测试通过；旧已退出Q02事实回放通过 | T054/T057及T059/T066/T067正式新构建读回 |
| 人工、旋转Pending、GROUP-A-E、故障前旧媒体等证据缺口 | 保留前次失败及未执行包，继续最少代表正式验收 | T062/T066/T067/T069/T070 |
| 共享启动通信延迟 | 实际诊断区分PLC迟收与Host迟读，未确定系统根因；仅新增本进程CPU上下文，不放宽期限 | T054/T069，实际失败窗口核查 |
| 一次性桌面恢复/自动接班 | 固定按需任务真实恢复并复用；reloadWorkerRoot先就绪再退出原worker，已实际两次验证 | 当前工具验收，登录自动启动仍延后 |
| 生产局部输入 | 真实PLC采样、点位/高度、相机/算法依据按原输入限制记录，不扩大为全部Test阻断 | T049/T050原边界 |

下一步是完成当前独立构建的正式页面及持久/设备对账，然后按原任务完整条件复核并再做converge。不能以子能力、测试数量或本轮未追加任务关闭父任务。质量15/16及T058原准确状态保持。


## 第二轮当前全范围converge（2026-09-27）

正式WPF已结束后仅只读审查FR18、SC6、用户验收19、计划决策8、宪章原则13及008合同/001/003/006直接依赖。代码范围为实际JsonRecipeCatalog、Planner、Coordinator、IntegratedDetectionPort、FaceResultAggregator、Mapper/Allocator/ThreeStage、TraceWriter/StageEventStore、CommittedResultProjection/QueryEndpoints、Control/恢复/Motion/ResourceLease、Frontend/VirtualPlc及采证工具；无新增missing/contradicts/unrequested代码任务。partial HIGH：T055依赖003 T065的实际延迟机制对照尚未全齐；partial MEDIUM：T070不能在T055未齐时勾父任务。已由原任务覆盖，不重复追加。当前已选Test页面/必要门禁证据齐备，生产局部输入与未选位置变体保持各自范围。

追加0任务。converge阶段tasks字节SHA3C0A4FF32655B98147F55FD878603AFE8CE572151BF627361B7B43A85F0A3918不变，没有空Convergence节或应用代码/规格写入。以上记录在converge结束后的独立证据阶段写入；后续19项勾选属于实施验收阶段。第一轮既有缺口的处理及当前原条件见task-audit-night-20260927.md和completion-review.md，不把追加0解释为008整体完成。
