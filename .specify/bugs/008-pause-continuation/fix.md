# Bug Fix: 正常暂停在已完成动作边界继续

- **Slug**: 008-pause-continuation（从当前评估上下文解析）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

复用 Coordinator 与现有控制锁，在实际动作、反馈、保存和握手完成后转换为 Paused；检查并 Continue 后恢复原阶段、原 run、原绝对期限。

## Changes

| File | Change | Notes |
|---|---|---|
| `backend/src/Gaode.Application/Station01/NormalPauseBoundary.cs` | added | 保存暂停/继续事件；核验连接代次、安全及无未知在途动作 |
| `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs` | modified | 公共流程安全边界消费暂停 |
| `backend/src/Gaode.Application/Station01/Steps/StartClampStep.cs` | modified | 已派发夹紧继续观察反馈，完成后暂停 |
| `backend/src/Gaode.Application/Station01/Steps/FixedMoveStep.cs` | modified | 已派发运动继续观察完成，普通暂停不变成物理未知 |
| `backend/src/Gaode.Application/Station01/Steps/ThreeDStep.cs`, `FScanStep.cs` | modified | 已启动逻辑步骤完成保存及复位后交接暂停 |
| `backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs` | modified | 冻结检测步骤之间暂停 |
| `backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs` | modified | 下料/分拣步骤之间暂停 |
| Host DI、001/003/008 合同与计划 | modified | 设计先于接线；不新增页面控件或业务 HTTP 接口 |

## Tests Added or Updated

正常暂停边界、在途夹紧、3D 完成保存/复位和真实 Q01 API 链；必要原运动/控制回归。

## Local Verification

r5/tests：`pause-affected.trx` 13/13、`pause-sorting-final.trx` 43/43、最后源码 `final-source-guards.trx` 5/5（集合重叠）。`pause-q01-real-chain-r2.trx` 1/1：真实 PLC、独立算法、SQLite，暂停后检查、同 run 继续至 Final，初始 3D/F 各一次。正式 WPF job-001 另行验证，不用 API 测试冒充页面。

## Deviations from Assessment

检查发现 FixedMove 将普通暂停视为在途异常，3D/F 会因暂停跳过复位，因此扩展到这些直接相关文件。首次真实 Q01 暴露无暂停时额外安全准入提前阻断；修正为仅在确有暂停时核验暂停边界安全，保留取消/安全/关闭阻断。失败 TRX 保留。

## Follow-ups

原夹紧复现的公共集成暂停前缀已到 Paused；旧四面全 AB fixture 在 Continue 后因当前业务范围拒绝，不能作为完整路线通过。当前 Q01 正式 WPF、人工确认和故障新轮分别取证后写验证报告。
