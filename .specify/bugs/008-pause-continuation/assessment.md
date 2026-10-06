# Bug Assessment: 正常暂停无法进入可继续状态

- **Slug**: 008-pause-continuation（无人值守自动生成）
- **Created**: 2026-09-26
- **Source**: 用户要求分别验证正常 pause/continue、人工换面与故障新轮；当前代码条件审计
- **Verdict**: valid
- **Severity**: high

## Report

“正常pause/continue和manual-flip-confirmation保持原run且不重采3D；故障continue拒绝，故障新轮才再采3D/F。”来源：003 recovery-test-execution 与008 T068/T069，现有任务范围，不追加重复任务。

## Symptom

正常 Pause 将运行置为 PauseRequested。StartClamp 等待循环停止观察且无限等待暂停标记解除；Continue 又只允许 Paused，而整个实现没有进入 Paused 的赋值路径。检测阶段也没有消费正常暂停准入，可能继续派发步骤。预期在当前动作可靠结束/保存后暂停，阻止后继派发，再经核验继续原执行。

## Reproduction

1. 启动合法 Test 普通配方。
2. 在公共夹紧等待期间请求 pause，获得 PauseRequested。
3. 核验原盘/原配置后请求 continue：ContinueAsync 明确拒绝非 Paused。

现有 PublicContractSmokeTests 只断言 PauseRequested 和检查受理，未验证继续或整链。代码证明上述状态不可达；修复前后补必要实际集成验证，不能据此声称已跑过复现。

## Suspected Code Paths

- `backend/src/Gaode.Application/Station01/ControlCommandService.cs` RequestAsync/ContinueAsync：PauseRequested 与 Paused 准入不闭合。
- `backend/src/Gaode.Application/Station01/Steps/StartClampStep.cs`：PauseRequested 时跳过本次夹紧反馈观察，机械期限未检查。
- `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`：公共安全边界缺正常暂停等待。
- `backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`：冻结检测步骤循环缺正常暂停门禁。
- `backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`：下料/分拣动作间也须消费同一准入。
- `backend/src/Gaode.Application/Station01/Station01Coordinator.cs` 与 ControlLatch：现有原run、状态及暂停锁，不另建状态库。

## Root Cause Hypothesis

置信度高。PauseRequested 请求标记已实现，但缺执行方安全边界到 Paused 的转换及继续等待路径。取消、物理未知与故障新轮不能通过普通暂停路径解除。

## Proposed Remediation

复用同一 Coordinator/ControlLatch，在无在途动作且本次反馈/必要保存/握手已完成的边界等待正常暂停；记录 Paused 及原步骤、恢复原阶段后继续，不重扫3D/F、不重建run/计划。保持既有绝对期限，不延长当前动作、机械或检测预算；安全/epoch变化仍阻断。夹紧在途继续观察已有反馈，完成后再暂停，不能发明 PLC 停止信号。普通 Continue 权限、核验和故障拒绝沿现有 API。

先在直接相关001/003/008合同与计划记录最小内部接入；无新增业务接口/页面控件。修改上述实现及必要接线；补正常暂停继续至Final、同run/初始3D/F一次与故障拒绝的最小集成对照。原人工正式页面证据独立取得。仅在冻结页面队列结束/暂停且资源释放后构建。

## Risks & Considerations

- 暂停请求不能充当真实机械停止，不中断已派发未知动作或释放占用。
- 等待期间取消、Host关闭和安全故障须保持既有阻断路径。
- 不增加生产协议或页面范围；共享接口变更如确有必要，先同步现有设计。

## Open Questions

无新增业务决定。期限不放宽，现有确认规则不变。尚待修复前后实际验证。
