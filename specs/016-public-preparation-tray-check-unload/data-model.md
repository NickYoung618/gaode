# 016共同数据增量

公共位置继续为FixedPoints.ThreeD和Unload（人工上/下料同点）；F不改。SavePublicPositions(reference, threeD, unload, expectedDigest)返回完整LoadedConfiguration，版本以digest及点位version表达，同run冻结JSON不变。

TraySlotObservation增加CellId、Region、Row、Column；TrayObservation增加完整覆盖集合/映射来源与当前schema。完整覆盖要求集合非空、无重复、逐位置身份成立、无Unknown；FLocation只在执行F时必需。历史观察不凭空补覆盖。RecipeMember散件独立CellId/物理号；半成品仍整体。共同正文/序列化版本增量保留历史读取。

TrayAnomalyDecision包含DecisionId/RunId/ObservationId/轮次/CreatedUtc/DeadlineUtc/异常行列/State/Choice/ChoiceSource/OperatorId/CommittedRef；仅一选择提交；Closed不能恢复超时。

DetectionPortResult.Objects只真实检测，PosePendingHandling另记异常实体/物理号/观察引用/未检测或后续终止；分拣计划合并到最后，用原槽+配方Pending坐标，实际在途/占用保存成立才完成。

WholeTrayCompletionReference增加EndReason(NormalCompletion/ManualIntervention/EmptyTray)、InspectionCompleted、RecipePlanRevision可空、前置观察/决策引用；正常保三个事件；提前结束Detection/Sorting引用空、Unload必需。已有PlanRevision列在提前结束仅为public snapshot执行revision，明确不是配方版本。准备/结束payload用tray-end/2，历史device-semantics/1保持原读取。终态与InspectionCompleted独立，人工自由文本不能覆盖冻结结束原因。

RunSnapshot及API/通知/历史加当前决策和结束原因/完整性；F前格位来自本次观察，绑定后严格关联CellId/Region/成员/坐标；未知mapping拒绝。
