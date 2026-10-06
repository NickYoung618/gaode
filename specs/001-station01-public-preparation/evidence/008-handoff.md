# 008 第二批启动引用与公共移交（2026-09-25）

状态：**001 T090 部分完成，未勾选**。`StartRunContext` 已接 `expectedRecipeRef`；启动前校验目录身份/用途/场景/版本摘要与 Available，F 后核对唯一解析引用，不匹配写审计并阻断计划。见 `backend/src/Gaode.Application/Station01/StartRunContext.cs`、`StartPublicPreparation.cs` 和 003 `evidence/008-api.md`。

未交付：Q01 合法物理槽、实体、A/B `pointRefs`、公共 3D 高度到对象/检测 Z 的绑定；因此原 `0,0` 占位尚未移除，也未创建 Q01 冻结产品计划/真实 handoff。受限目录的 400 拒绝不是 F 不匹配的运行证据；提交未知、原子非终态移交和同 run 一致性仍待 T090 后续。不得据本文件启动产品运动。
第四批：`PublicPreparationHandoffV2Consumer`现在把已核对冻结计划放入DetectionRequest，并调用显式目标解析：逐步核对pointRef、对象/槽/面/轮次/相机，读取带`approvedSourceRef`的固定XYZ和独立源点；缺点坐标返回`ProductPointCoordinatesMissing`，未配置高度结果换算返回`ProductHeightMappingMissing`，不生成0,0。`PublicPreparationTargetResolutionTests`1/1通过，使用组件构造的批准固定目标样本；不代表Q01/Q02目录已获点位或高度批准。完整来源与正式连续移交仍依T090原条件。
# 第八批当前增量（2026-09-25）

008自动多面首轮handoff只解析第一面/第一轮；`ResolveTargetsForRound`在轮2真实3D结果必要保存后按对象/槽位/面/轮次解析，拒绝首轮callId、错面或错对象。定向规则3/3通过，后半段Test组件实际读回见[008第八批证据](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。该组件没有PLC翻面证据，001 T090整项未因这部分代码而勾选。


## 2026-09-27 008直接依赖当前引用

008所用Test能力及正式页面/必要门禁的当前证据见008/evidence/completion-review.md和task-audit-night-20260927.md最新节；r21复位状态同步必要用例与r22完整新轮/Q18真实DLL通过，r20真实401/403页面正确拒绝且无业务记录。T069唯一当前旧图/完整新轮主包为r22 job002；r18旧主包保持原构建事实。本文不修改本功能父任务勾选或补造历史分段日志，T065延迟机制原条件仍未全齐，Default/生产根因及其他父条件不由008选定成功代表抵扣。
