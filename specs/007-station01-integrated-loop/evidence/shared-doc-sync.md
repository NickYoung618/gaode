# T001 共享文档前置核对

日期：2026-09-23。范围：本记录只证明003/006共享文档已按007当前需求对齐，不证明相应代码或运行通过。

- 003 `spec.md`、`plan.md`、`tasks.md` 与 `contracts/detection-port.md`、`public-preparation-handoff.md`：固定图片按正式采集入口逐次保存；冻结计划中每项Detection采集对应一次独立worker算法调用；同runId关联媒体、配方、PLC、SQLite事实；保持120秒阶段期限和安全门禁。
- 003 `contracts/station01-main-flow-api.md` v1.1：唯一正式确认路由为`POST /api/v1/station01/runs/{runId}/manual-removal-confirmations`，请求体仅`requestId/expectedRevision/reason`；Host从同run已提交WholeTrayCompletion、ObservedUnlocked及认证上下文取得其余信息。
- 003 `contracts/whole-tray-workflow.md`、`component-source-matrix.md` v1.1：解锁反馈持久化后方可自动模拟确认；ManualActor源为Test/Simulated，保留认证测试身份与渠道，不宣称真实人工。
- 006 `spec.md`、`plan.md`、`tasks.md` 与`contracts/api.md`、`gaps.md`、`prototype-mapping.md`：前端请求/凭据/已有位置展示归006，Host侧Test来源跨源及授权连通归007；006 T042–T044未勾选，不阻塞独立后端接线。
- 007当前`contracts/virtual-integration.md`与上述共享合同一致；若实现确需改变worker消息结构，先按AGENTS.md规则再同步受影响合同，不把本核对视为授权推测性接口。

当前代码差异：`PythonWorkerAdapter`仍不派发，Host确认源仍写`AuthenticatedHuman`，Host尚无Test跨源接线；这些分别由007 T008、T013、T010实现。003/006已勾选历史任务不作为007运行通过证据。
