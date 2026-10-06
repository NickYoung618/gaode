# 公共合同与身份规则

**合同版本**：s01-contract/1.0　**文档修订**：1.1（信封线版本不变）　**日期**：2026-09-20　**状态**：拟定内部合同，未实现。  
关联：FR-001/002/016/017/018/019/026/031、P05/P07/P09。字段不表示PLC已存在寄存器或外部协议已冻结。

## 1. 消息信封

| 字段 | 类型/必需性 | 语义 |
| --- | --- | --- |
| contractVersion | string，必须 | s01-contract/1.0；不兼容版本明确拒绝 |
| messageId / correlationId | UUID/string，必须 | 消息去重与原请求关联；不以料盘码代替身份 |
| runId | UUID，运行建立后必须 | 后端创建一次公共运行身份；未请求成功建立前可空 |
| operationId | UUID，动作/采集/算法/保存消息必须 | 不同操作独立；由Host产生 |
| captureId / callId / actionId | UUID，按消息需要 | 与相应记录一致，不把一个ID兼作所有身份 |
| attempt | integer≥1，操作消息必须 | 当前功能正常执行为1；协议重连不得自动加Attempt重新执行 |
| sessionId / connectionEpoch | UUID / integer，设备或Worker消息必须 | Host会话与适配器连接代次；旧代次只能归档/核对 |
| snapshotId / pointVersion / scopeVersion | string，按操作需要 | 引用本次已冻结配置；非运动消息不强填点位 |
| providerKind / purpose | enum，必须 | Real/Simulated/Replay与Production/Test；ManualObservation仅是核对证据来源 |
| receivedUtc / clockId / receivedTick | 时间/时钟身份/单调tick，Host入站生成 | 审计与业务时间分开；外部报告时间另存，不作为期限权威 |
| ingressSequence | int64，Host生成 | 单会话入站顺序；不声称PLC提供 |
| deadline | phase、起点事件、budgetMs、dueTick、clockId，需等待的操作 | 来源为冻结预算；到期不能用下一次ACK重新起算 |
| expectedRevision | int64，改变既有运行的API请求必须 | 防止调用方用过期快照继续；不是PLC序号 |

身份、契约版本、用途、长度与类型在API/设备/Worker入站校验；动态安全在动作准入再次检查。不逐层重复解析/哈希。未知Part_ID、Face_ID、Group_ID、Slot_ID保持null及NotEstablishedBeforeRecipe，不为本阶段建空壳工件。

## 2. 受理、终态和幂等

- API CommandReceipt表示命令进入控制入口；receiptDurability=Pending/Committed/Failed明确是否持久保存，202不表示PLC受理、动作完成或工位完成。提交失败也能查询短期内存诊断，不声称重启后仍有未保存记录。
- 请求幂等键为调用身份+requestId+操作类型+runId（启动的runId为空）。保存请求的规范化摘要只在入口计算一次。同键同内容返回同一commandId/runId；同键不同内容返回409 RequestConflict。
- 运行状态只由Station01Coordinator修改。Completed类与Cancelled的最终选择以同一Run的持久revision/TerminalOutcome=None条件事务为权威；Coordinator依据提交事实更新内存，不能凭事件先后指定相互排斥终态。各操作端口的受理/完成消息是事实输入；期限仲裁器只管理等待窗口及输出不可变事件，不直接改运行、动作台账或数据库。
- 同一Operation/Attempt只有一个有效业务终态。重复、乱序、旧连接及跨运行事件不得推进；有明确原关联的迟到响应单独保存LateEvidence。无法关联事件进入有界诊断，不绑定“当前运行”。
- 物理操作跨崩溃不能承诺exactly-once；意图已保存但发送/执行不确定时保持Unknown，不自动重发。崩溃或故障旧轮只允许状态核对、执行收束、保存已发生事实和历史查询；即使证明某步骤尚未发起，也不能继续旧故障轮。按[003恢复合同](../../003-plc-latest-protocol/contracts/recovery-test-execution.md)双端复位、真实初始核验后，由授权调用者显式新启动完整新run，重新公共3D/F及唯一绑定；故障continue拒绝，旧故障、图片、结果和日志保留。
- 正常暂停仅在原run真实Paused且未发生设备/保存故障、物料/快照仍适用、安全和必要保存成立、核对仍有效时同run继续未开展步骤；人工换面继续按上述合同独立处理。请求幂等、原WriteId提交核对及原合同允许的有限局部重试不变；保存核对不授予运动资格，不延长原期限。客户端HTTP断线本身不是设备故障，按后端实际运行状态判定，不能据断线自动复位或另起新轮。

## 3. 错误合同

统一Error含code、category、中文message、source、run/operation关联、stage、configVersion、occurredUtc、observation、disposition、persistenceState、recoveryRequirements。真实原因未知时与观察事实分开。

| category | 示例code | 业务出口 |
| --- | --- | --- |
| Input / Authorization | InvalidRequest、Forbidden、RevisionConflict | 不派动作；保留适用审计 |
| Configuration | PointInvalid、CaptureConfigMissing、CapabilityUnsupported | 全部必需运动/采集配置未通过时禁止PLC启动；算法配置问题按该调用终态 |
| Control / Safety | PlcDisconnected、InterlockDenied、ActionDeadlineExceeded、FeedbackUnmatched | 关闭依赖准入，Unknown/Held及受控核对 |
| Acquisition | CaptureFailed、CaptureUnknown、CompletedWithoutUsableMedia | 前两者受限；只有可靠采集结束的内容异常可按规格继续 |
| Algorithm | Error、TimedOut、NotConfigured、NotIntegrated、NotReady、NoResult、InvalidResult | 有限终态；不单独要求停盘/人工确认 |
| Persistence / Capacity | SaveFailed、CommitUnknown、MediaNotSaved、CapacityUnavailable | 必要保存或容量未满足时不推进依赖步骤、不发布移交 |
| Lifecycle | PauseRequested、CancelRequested、RecoveryRequired | 按状态与物理证据处理，不推定已停 |

未知异常在所属边界捕获并转明确事实；不捕获所有异常后默认成功。算法技术失败不复用PLC停机报警等级。

## 4. 期限、取消和事件顺序

详细规则见[configuration-time.md](configuration-time.md)。HTTP连接断开只取消响应传输，不取消已受理运行。操作取消令牌只撤销尚未发起的工作或提出取消请求，不证明设备/Worker退出。

取消命令的requestAccepted、admissionClosed、stopState和terminalDecision分开；applied在Pending/CommitUnknown时为null，最终取消提交后true，完成先提交则false。Run与Handoff原子一致，详见[persistence-handoff §1.2](persistence-handoff.md)。

停止/取消关闭新生产动作准入的处理优先且不等待磁盘；停止请求使用同一Motion控制权和独立控制入口。所需审计异步补存，失败明确可见；这不是允许未保存意图就发新生产动作。查询快照包含observedRevision、persistedRevision及未提交事实。

通知可合并；必需动作完成、保存回执和算法终态不可用“丢旧事件”策略处理。API通知不是设备命令总线。


修订记录：2026-09-20，1.1，H02补充持久终态权威及取消未决语义，身份、幂等和动作安全边界不变。
