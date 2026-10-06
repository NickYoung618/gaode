# 008第三批产品检测PLC组件（003 T070部分）

日期2026-09-25。协议§2.2 `XY_Move_Cmd=2`为去检测位；§3.1.7中的1仅是去上料位示例。`MoveRequest`/`InspectionHandshakeContext`增加明确的`Detection`角色，正式Host通过Camera_Target_X/Y/Z、命令2、匹配本轮XY/Z及XYZ、Inspection_Status 1→2、Z_Reset_Status本轮2→0闭环；VirtualPlc仅在已受理命令2后接受产品1/2复位。未改F命令5及3/4规则，不外推E。

`VirtualPlcLatestProtocolTests.FormalHostPortKeepsPreviousArrivalUntilNextAcceptedMoveAndBindsAfterF`两种分支2/2通过，其中正常分支使用**组件注入的Test XYZ**在F绑定后验证Detection握手。这不是Q01批准点位，也没有从正式配方或公共3D高度计算Z。`PublicPreparationHandoffV2Consumer`已拒绝0,0占位产品目标；`RecipeExecutionCoordinator`在缺点位时拒绝采集。Q01所用物理槽/对象到A/B目标、3D高度到Z映射和正式链到位/复位失败证据尚缺，003 T070未勾。

第四批进一步把上述PLC原语接入同一`IntegratedDetectionPort`：显式Test XYZ下AB两轮、CD四轮均经MotionCoordinator准入、Modbus TCP产品命令2、本轮XYZ反馈、检测1/2及复位2→0，下一定位在前轮复位后；详见[008第四批包](../../008-recipe-driven-inspection/evidence/fourth-batch-validation.md)。到位观察不符0采集、复位失败不发下一轮，两项注入均通过。正式Q01点位/高度映射及前端链仍缺，T070整项不勾。


## 2026-09-27 008直接依赖当前引用

008所用Test能力及正式页面/必要门禁的当前证据见008/evidence/completion-review.md和task-audit-night-20260927.md最新节；r21复位状态同步必要用例与r22完整新轮/Q18真实DLL通过，r20真实401/403页面正确拒绝且无业务记录。T069唯一当前旧图/完整新轮主包为r22 job002；r18旧主包保持原构建事实。本文不修改本功能父任务勾选或补造历史分段日志，T065延迟机制原条件仍未全齐，Default/生产根因及其他父条件不由008选定成功代表抵扣。
