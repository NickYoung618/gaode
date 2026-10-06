# Bug Assessment: 第一工位下料准备与最新版 PLC 协议不一致

- **Slug**: `station01-unload-protocol-alignment`（用户指定）
- **Created**: 2026-09-24（Asia/Shanghai）
- **Source**: 用户提供的缺陷描述及本地只读证据；无外部 URL
- **Verdict**: valid（本次前置拒绝及相应代码缺陷已证实；完整下料修复方案尚有待确认输入）
- **Severity**: high（Test/VirtualPlc 主流程在检测、分拣后阻断，且相邻完成判据存在把旧反馈当新完成的安全/真实性风险；不代表真机已发生）

## Report (verbatim or summarized)

用户指定 `E:\dzk\gaode-1\artifacts\station01-007\manual-20260924-033644-b7b9f088` 中的 `runId=68586287-4410-4132-8385-7f32fb212cf8`。2026-09-24 03:39:34（UTC+08）`UnloadPreparation` 记录 `Failed / StagePreconditionStatus:1`。本轮仅评估，不修复或运行程序。2026-09-24 03:50 左右的心跳失效日志及此前两次间歇性心跳故障单独保留，不能据此认定与本次前置拒绝同因或已修复。

## Symptom and directly confirmed cause

SQLite `Runs`：`RequestId=s01-007-a0ce593c120c4541bf8e3d003f0c10d3`、`CommandId=7B519994-874C-4F38-A515-829B65F452A6`（见 `Commands`）、上述 runId、Test 操作身份 `test:Operator`。`StageEvents` 依次有 `Detection/Completed`（2026-09-24 03:39:34.598+08）、`Sorting/Completed`（03:39:34.622+08）、`UnloadPreparation/IntentRecorded`（03:39:34.639+08）、`Started`（03:39:34.645+08）、`Failed`（03:39:34.672+08）。失败的 `operationId=460fc344-cb27-4575-a4fb-10ce3baa14c7`、`connectionEpoch=1`，阶段投影为 `Failed`、`DeviceHeld=0`、`NeedsManualReview=0`，未出现该阶段 `Completed`、`WholeTrayCompletion`、解锁或最终完成。运行主表仍为 `HandoffReady / Terminal=None`；不能把它解释为整盘完成。

**直接原因，高置信度**：`LatestProtocolStageActionAdapter.PreflightAsync` 对 `UnloadPreparation` 固定要求 `XY_Pos_Confirmed(4x0002)=0`；实际读取 `1`，立即返回 `StagePreconditionStatus:1`。`ExecuteAsync` 在该返回路径上先于 `XY_Move_Cmd=4` 写入退出。因此本次失败是 Host 的派发前条件拒绝，而非已证实的下料运动失败、PLC 明确报告不安全、心跳故障或坐标误差。失败事件中的 `1` 是 Host 的读回值；现有保存材料没有逐笔 Modbus 报文或同刻 VirtualPlc 点位快照来进一步独立证明设备内部状态和时间。

证据：`artifacts/station01-007/manual-20260924-033644-b7b9f088/station01.test.db` 的 `Runs`、`Commands`、`StageEvents`、`StageProjections`；`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs:67-89,125-143`。`process.json` 标记环境 `Test/VirtualLoop`、协议 `plc-upper-20260921-hex1-f32`、Host/VirtualPlc 及配置哈希；不能外推真机。`logs/host.out.log` 保存了启动动作关联，但没有本次下料前置读取的独立结构化明细；`logs/plc.out.log` 没有本次下料采样记录。数据库错误码足以定位拒绝分支，不足以还原完整点位/处置链。

## Reproduction

1. 只读复核上述已保存 Test/VirtualPlc 样本；不重新启动或注入。用 SQLite 只读连接查看同 run 的 `Commands`、`StageEvents` 和 `StageProjections`。
2. 对照最新版协议 §2.2、§3.1.7 与适配器 `PreflightAsync`：前一轮到位反馈可以继续为 `1`，而下料预检把 `1` 拒绝。
3. 本次 `UnloadPreparation` 的实际 `IntentRecorded → Started → Failed` 已持久化；要在新的隔离 Test 实例中重现完整页面路径、写入/采样时序和修复后闭环，须待用户批准进入后续阶段。本轮没有运行重现。

## Source hierarchy and six checks

| 核查点 | 原协议明文 / 项目约定 / 代码与样本事实 | 判定 |
| --- | --- | --- |
| `XY_Pos_Confirmed` 的 0/1 | 2026-09-21《PLC与上位机通信接口协议_最新版.docx》§2.2 明文为 `0=运动中、1=到位、2=超时未到位`；§3.1.7 明文允许检测期间清旧 `XY_Move_Cmd=0`，下一运动实际受理时才清旧到位/复位反馈。003 FR04 与 `contracts/virtual-plc-boundary.md` 进一步确认同一语义。 | **没有**“下料前必须先读到 0”的原协议依据；Host 当前门禁与之冲突。不能把项目合同细化语句冒充协议原文。 |
| 避免旧 `1` 冒充本次完成 | 原协议 §3.1.7 描述新运动受理后反馈重新开始；003 FR04/虚拟 PLC 合同要求本轮坐标与连接代次匹配。当前阶段适配器写 4 后直接等 `XY_Pos_Confirmed=1`，不证明新一轮受理/运动反馈，也不核对 `Z_Axis_Move_Status=2` 或目标与实时 XYZ。 | **已证实代码风险，尚未在本次触发**（本次尚未派发）。不能只删除预检后继续用旧 `1` 判完成；需确立同轮反馈和目标匹配证据，无法确认时不完成、不盲重发。 |
| 清命令后等 `0` | 原协议 §3.1.7 仅允许清本方 `XY_Move_Cmd`，不要求随之清 `XY_Pos_Confirmed`；VirtualPlc `VirtualPlcEngine.ProcessMove` 仅在下一运动受理后清旧反馈。当前 `WaitUnloadAsync` 写 `XY_Move_Cmd=0` 后等待 `XY_Pos_Confirmed=0`。 | **已证实代码/协议冲突，尚未在本次触发**；如果下料已到位且无下一动作，可无界等到阶段截止，甚至将已派发动作判未知。 |
| 下料目标坐标 | 原协议 §2.2 规定上位机下发绝对坐标并校验实时 XYZ；§3.1.7 要求下一组运动指令及**新的** X/Y/Z 绝对坐标。§3.1.6 的 `4x000B Grab_Target_Z` 明文用于分拣取料点，不等于已给出下料位的数值。003 合同只写 `XY_Move_Cmd=4 / XY_Pos_Confirmed=1`；`PlcStageActionRequest` 没有下料 XYZ，`ThreeStageWorkflowExecutor` 的摘要仅含命令 4，适配器只写 4。007 测试 `public.virtual-loop.json` 仅有 3D/F 固定点，无下料点。VirtualPlc 对命令 4 使用先前写过的 `Camera_Target_X/Y/Z`。 | **已证实缺少新目标写入**；本次没派发，所以“本次实际运动沿用历史坐标”不是已发生事实。权威下料位坐标的数值/来源及其 Z 寄存器选择在已查资料中未确定，不得猜用 F/检测坐标或把 Test 点当生产坐标。 |
| 完整时序 | 原协议 §3.1.7 明文：检测结果/记录后 `Inspection_Status=2`，PLC Z 复位 `1→2`，Host 读 2 后清为 0，再发下一运动及新坐标；§3.1.6 分拣完成后才可解锁。项目 003 进一步约定 `UnloadPreparation → WholeTrayCompletion → UnlockObservation`（Cmd=0、读回 Status=0）→ Host 人工取盘确认 → `FinalUnloadCompletion`。 | 本次 SQLite 检测和分拣完成，但下料失败，后续均未成立。保存材料未提供本次逐点 Z 复位/新运动采样/解锁事实；项目拆分是项目约定，不是 §3.1.6 对所有业务事件的原文。 |
| 持久诊断 | SQLite 关联了 request/command/run/operation/epoch、停止阶段及错误码和 `Failed` 投影；失败事件 `EvidenceReferences` 含 `modbus://.../<operationId>/<actionId>`，版本为 Virtual。 | **部分足够**：可定位 Host 预检拒绝。**缺失**：该点位读取 UTC 时间、读写事务、预检同时的 `Inspection_Status/Z_Reset_Status/XYZ` 和安全快照、明确“未发送 Cmd=4”、Host 对本次失败的独立结构化日志、页面/处置结果证据。`modbus://` 引用不是保存的原始协议审计文件。 |

VirtualPlc 代码依据：`VirtualPlc/VirtualPlcEngine.cs:417-460,291-313`（合法新运动受理时清旧反馈、到位时写 1；清旧命令不清反馈）。此为代码行为及项目已有虚拟合同；不能证明真 PLC 实现完全一致。

## Suspected Code Paths and confirmed defects

- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs:125-143` — **已证实**下料前错误要求 `4x0002=0`，造成本次拒绝。
- 同文件 `:84-89,176-184` — **已证实**只写命令 4、未写新坐标；之后可把旧 `1` 当新反馈，且清命令后等 `0` 与协议冲突。本次均未执行到，属代码可证风险而非本次已发生后果。
- `backend/src/Gaode.Application/Ports/StagePortContracts.cs:119-143`、`backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs:129-136` — 下料请求缺少可冻结、可验证的目标/来源；无法让正式适配器凭本次目标核验到位。
- `backend/tests/Gaode.Contracts.Tests/Ports/PlcStageActionPortContractTests.cs:24-34` — 旧测试用 `[0,0,1,0]` 并断言清命令后等状态 0，固化了错误时序；测试通过不能证明协议对齐。
- `VirtualPlc/VirtualPlcEngine.cs:417-460` — 目前虚拟行为保留旧到位 1；无证据表明应为了旧测试而改 VirtualPlc。仅在后续证实它偏离权威规则时才考虑修改。

## Root Cause Hypothesis

本次直接根因不是推测：阶段适配器沿用了“新动作前完成位先为 0 / 清命令后再等 0”的旧握手假设，而最新版协议与已确认项目合同规定旧 `1` 保留到下一合法运动实际受理。该假设导致 Host 在 `XY_Pos_Confirmed=1` 时错误拒绝下料。更深层的合同缺口是下料只有命令号和完成位，没有新目标坐标及可证明“本次”受理/到位的判据；其修复必须先对齐接口/配置，不能仅放宽预检。置信度：直接拒绝高；本次设备完整时序及历史坐标值低（缺保存审计）。

## Proposed Remediation (not implemented)

**Preferred**：先明确批准的下料位 XYZ 来源、坐标系/单位/版本、与命令 4 配套的 Z 点位及匹配容差，并在 003/007 合同中定义“旧到位 1 可作为前态，但新动作完成必须有本次派发后重新开始的反馈、当前代次及实时目标匹配；不能观察到本轮事实则保持未知/占用并人工核对”。再扩展阶段请求携带冻结目标和来源；正式适配器在安全、Z 复位和当前连接代次门禁满足后写新坐标与命令 4，记录写入/观察时间和事务，只有本轮运动受理与到位、必要 Z 状态及 XYZ 匹配后才完成；清命令不再等待 PLC 到位位变 0。避免用固定延时、放宽超时、虚构反馈、自动重发或解除互锁解决。

**先对齐的最小文档**：`specs/003-plc-latest-protocol/spec.md`、`plan.md`、`contracts/plc-stage-action-port.md`、`contracts/virtual-plc-boundary.md`、`tasks.md`；`specs/007-station01-integrated-loop/spec.md`、`plan.md`、`contracts/virtual-integration.md`、`tasks.md` 及 Test 坐标示例（确认后）。若公开错误/状态字段需要变化，先同步提供方共享合同和 006 消费者规格；本次未确认这种变化的必要性。

**最小可能代码/测试文件**（仅后续获准修复时）：`backend/src/Gaode.Application/Ports/StagePortContracts.cs`、`backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`backend/tests/Gaode.Contracts.Tests/Ports/PlcStageActionPortContractTests.cs`、受影响的 `backend/tests/Gaode.Contracts.Tests/Devices/VirtualPlcLatestProtocolTests.cs` 及第一工位正式 Modbus 集成回归。配置若新增 Test 下料点，仅在确定来源后修改 `specs/007-station01-integrated-loop/examples/public.virtual-loop.json` 或实际适用配置；生产数值不能从 Test 配置推导。VirtualPlc 引擎不是现有证据指向的首要修复文件。

## Tests and original page acceptance conditions (future work, not run here)

1. 定向合同测试：预检读旧 `1` 不误拒绝；只要未看到本轮动作受理/反馈变化，不把旧 `1` 判完成；清 `XY_Move_Cmd=0` 后到位 `1` 可保留；错误坐标、Z 未复位、状态 2、通信失效/代次变化、派发未知均不解锁或盲重发。
2. 隔离 Test/VirtualPlc 正式 Modbus 回归：同 run 从检测、Z 复位、分拣到下料，记录新 XYZ 的实际寄存器写入、命令 4、PLC 受理后反馈、实时 XYZ/双状态匹配、仅在阶段完成与 `WholeTrayCompletion` 提交后写解锁 0 且读回 0；人工 Test 确认再到 `FinalUnloadCompletion`。保留失败样本与日志，不重跑到通过后丢弃失败。
3. 原页面路径：独立 Test 根/端口/库和版本清单 → WPF/WebView2 实际 `a.html` 既有启动入口单击一次 → 保存点击前后截图、脱敏请求、202 回执及 runId → 通过正式 GET/通知重取观察同一请求、运行、阶段与处置；202 不能显示完成，失败明确受限且不能无反馈重发；正常路径只有本轮下料可靠完成、解锁读回与受控 Test 取盘确认持久化后显示最终完成。页面证据不得以辅助 API 或静态截图替代。全程不宣称真机验收。

## Risks & Considerations

- 仅去掉 `=0` 预检会暴露“旧 1 即刻完成”的更严重风险；必须与本次目标和反馈关联一起修。
- `StagePreconditionStatus:1` 表示读到到位，不等于 PLC 明确不安全；本次软件 `Failed/DeviceHeld=0` 也不等于物理已经安全卸料或解锁。历史心跳故障与本次证据不合并。
- 当前原始点位审计、下料坐标来源和页面点击/显示证据缺失；现有日志只能充分解释前置拒绝，不能证明实际物理下料或全部处置。
- 没有 PLC 回传 operationId；同轮判定只能依赖有序的主机发送/设备反馈、连接代次、目标及状态变化。若过快反馈使过渡状态无法可靠观察，不能据旧 1 宣称完成，应定义可信替代证据或进入未知处置。

## Open Questions and bug-fix readiness

**尚不具备进入“完整下料协议对齐”的 `$speckit-bug-fix` 条件。** 对于错误 `=0` 门禁的局部修正方向已明确，但不能孤立实施，因为后续旧反馈误认与目标来源尚未解决。

- [NEEDS CLARIFICATION] 请提供或确认第一工位**下料位**的权威绝对 XYZ 来源（批准的配置/配方/坐标文件及版本、坐标系/单位），以及 `XY_Move_Cmd=4` 时 Z 应使用的协议点位与本轮到位判据。现查到的 §3.1.6 `Grab_Target_Z=4x000B` 是分拣取料点，§3.1.7 通用运动写 `Camera_Target_X/Y/Z`；不能自行假设两者可代用，也不能沿用 F 点。
- [NEEDS CLARIFICATION] 若现场 PLC 的新运动反馈可能在 Host 首次轮询前完成，是否有已批准的设备动作受理/反馈时序或其他可追溯证据可证明该 `1` 属于本次命令？未有依据时应安全地标为未知，而不是认定完成。

本报告只写入 `.specify/bugs/station01-unload-protocol-alignment/assessment.md`；未改产品/测试代码、协议来源、功能文档或任务状态，未启动或操作任何程序。
