> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。第一工位移交前仍无配方调用。

# 设备观察、运动与停止端口

**版本**：s01-device/1.0；Application语义端口，尚未对接PLC。  
关联FR-006至FR-010、FR-018/019/027/033/034/040；REQ CTL-001至CTL-010、SAF-003/004/008/011。

前四节保留原001抽象端口/FullSimulation历史边界，其中XY-only及未接PLC不是当前008规则。当前外部PLC公共XYZ/复位按本文008增量及[派生时序](../sequences.md)，不得以旧表覆盖新链。

## 1. 所有者与端口

| 端口/操作 | 请求 | 返回/事件 |
| --- | --- | --- |
| IPlcStatePort.Observe / ReadCurrent | 设备绑定、session/connectionEpoch | StateObservation：连接、模式、安全/人工占用、就绪、实体启动边沿、夹紧、XY、当前动作及可信度 |
| IPlcActionPort.RequestStart | Start ActionId、runId、已保存意图引用、冻结公共配置依据、预算 | LocalQueued不等于PLC Accepted；随后Accepted/Rejected、实体启动与夹紧事件 |
| IMotionPort.SubmitFixedXY | ActionId、pointId/version、固定x/y/unit/frame、snapshotId、资源、期限、保存意图引用 | 准入/投递回执；Accepted、Executing、Completed/Failed/Unknown分别表达 |
| IMotionPort.RequestStop | StopOperationId、关联在途ActionId、原因、停止语义、预算 | StopRequested、StopAccepted、StoppedObserved/Unknown；不自动回位/松夹 |
| IPlcActionPort.ObserveAction | 原ActionId及适配映射依据 | 可关联事实或Unverifiable；只观察，不自动重发 |

IMotionPort由Application MotionCoordinator实现，唯一持有运动控制权；它调用IPlcActionPort，真实适配内部再使用IPlcTransport编码/通信。Workflow只发语义请求；API不暴露原始运动/寄存器接口。

Start意图及将由PLC自动执行的夹紧观察关联先保存；夹紧有自己的内部操作身份和parentStartActionId，**不是额外PC夹紧命令**。真实PLC若仅提供组合反馈，适配必须根据已确认的启动/按钮/夹紧握手关联，不能填造不存在的设备序号。

## 2. 动态准入和完成

每次发运动必须同时满足：原运行仍有控制资格、有效冻结固定XY、用途/Provider正确、设备当前就绪/自动/通信有效、无阻断故障、无人占用、安全位置与夹紧有效、所需资源一次性可预约、意图已提交。参数校验不能替代PLC自己的互锁。

测试新鲜性上限采用最近3个轮询周期即150ms，仅用于开发场景；正式新鲜性/到位容差和安全反馈按OPEN-08/24/26，不给生产默认值。不支持的Z轴状态不能被单一Z位冒充，但本功能不发Z运动。

Completed事件包含matchedActionId、连接代次、原命令类型及目标版本、设备报告状态、当前XY（实际可取得时）、安全/夹紧观察、反馈时间、关联证据。仅ACK、旧到位位或目标相同均不够。迟到Stopped不等于原目标已完成；晚到真正到位也不能自动继续，转恢复证据。

资源包括设备级运行占用、XY移动租约和采集期间位置保持。移动及采集不得互相冲突；一次预约全部所需资源，不持线程锁等反馈。曝光结束且媒体被安全接管后才可放开位置保持，仍需保存/调用终态满足流程后继条件。工位完成后料盘仍夹紧，设备级物理占用不释放。

## 3. 通信通道

一个适配器连接所有者管理真实PLC会话和轮询，不让运动等待占用通信泵。请求发送、心跳应答及停止各有受限入口；停止优先于未发送的生产动作，关闭新准入不等待算法/磁盘。传输I/O有期限，单次卡住不得拖过整条心跳路径；PLC断联后禁止动作，重连不自动继续。

保留REQ PC轮询≤50ms、PLC扫描≤10ms及3s失联基线。真实Modbus地址基准、站号、功能码、Float32字序、按钮、停止/完成及等效防旧反馈映射仍为OPEN-08/10/11/24。示例配置不填PLC IP或寄存器，不照抄预留地址作为新定义。

若PLC不能提供ActionId，应提供经验证的单在途+清零/新边沿握手映射。跨重启/断线无法证明所属动作时必须Unverifiable；禁止在plan中宣称严格跨设备去重已实现。存在产品区域握手前置依赖时按OPEN-05/09登记范围冲突，不提前匹配配方。

## 4. 停止/恢复边界

取消或动作期限触发先关闭生产准入、标记Unknown/Held，再按已确认停止能力请求停止。软停不是硬件急停；PC失效后的保护依赖PLC与硬件安全，不能依赖PC崩溃后继续发命令。

Motion独立控制路径允许在必要保存失效时提出安全停止并记录待补审计；不得借此派新的生产移动。停止超时保持Unknown。Query、StateObservation和诊断仍工作；任何连接恢复、单次心跳或人工声明都不单独解除物理限制。人工核对不覆盖反馈，详见[data-model.md](../data-model.md)。

当前没有单步、示教、复位、卸料实现入口；未来相应功能必须使用同一Motion准入。真实适配器未集成时返回NotIntegrated/Unverifiable并限制对应真实操作，不能切模拟成功。

## 2026-09-24 008完整执行合同增量

适用优先级：本节及008目标合同用于最新需求实现，前文冲突条款仅在本节明确的历史样本范围保留；本轮未改代码或声称协议缺口已关闭。

公共固定XYZ与已定义检测1/2时序保留。F按指定PLC协议§3.1.7：状态0先写Camera_Target_X/Y、Scan_Target_Z，再命令5；本轮XY=1、扫码Z=2、实际XYZ及安全匹配后PC写3并清移动命令0，再采集/解码；采集结束及结果或有限失败保存后PC写4，PLC一次执行扫码Z复位1→2（失败3锁停），PC确认本轮可靠2后清Inspection_Status=0。整个F周期直到下一有效移动受理，Z位置/到位/复位反馈属于扫码Z；旧2不得充本轮反馈。4不等于识别成功，失败不放行产品，Host不写PLC反馈。定义已确认但实现未验收，E规则仍待独立确认。不得把4映射2。产品定位通过008共享唯一Motion/PLC会话，不把公共role=3D/F限制当作产品设备已支持。下料沿用003已批准命令4判据。

统一来源：指定分区PLC协议§3.1.7、REQ §7/11、宪章6.0.0；执行/结果/证据分别见008 contracts/execution.md、api-results.md、evidence.md。关联实现任务见本功能tasks中的008对齐增量。

## USR-E公共XYZ观测

公共3D与F分别复用[003动作合同](../../003-plc-latest-protocol/contracts/plc-stage-action-port.md#usr-e完整坐标与动作观测)的检测Z/扫码Z及1/2、3/4握手，不把批量XY日志未逐轴显示当漏写。每次目标/实际XYZ和轴、代次、清零判据可关联持久查阅；未知反馈或必要保存未决不进入后继。唯一运动与设备消费者不变，不新增公共PLC客户端。RST-01关闭后的故障旧轮不续跑；正常暂停、人工继续及WriteId核对保持原合同。

## 009 / AL01 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

业务只消费业务语义观察、动作请求/结果和不透明证据引用；地址、寄存器、原值、位运算、内部ACK/清零阶段不得公开。MotionCoordinator/ResourceLease和同一LatestProtocolPlcDevice保持唯一所有权，IntegratedDetection也消费同一语义端口；原值解释和内部握手完全由通信负责。结果核当前run/operation/action/attempt、对象/物理槽/面、epoch、实际位置/来源/可靠性及原期限；未知或旧反馈不生成完成，不将目标当实际。

区域A严格保留在夹紧可靠成立后、公共3D/F之前，使用本轮运行配置NG/Pending容量和独立PlcAcceptance窗口；通信内部完成Ready0/Ack0/分别写容量/Ready1/Ack1，业务只得RegionPrepared，再保存夹紧事实后进入公共准备。启动Accepted仅为真实写序列受理，后续夹紧期限独立；Host不发送额外夹紧命令。A不是F唯一配方绑定。正常采集保存成立后关闭语义窗口；公共3D在安全/取消许可仍有效时保留失败清理，但不得将失败变成功、不得派后继；F不继承该放宽。

### 009 必要通信证据的真实保存回执（实施前接口细化，2026-10-02）

本节执行/复核者为Codex，依据009 FR-019/020/036/038、E02.2及影响矩阵§5.5；不代表客户批准或运行通过，不改变既有任务勾选。

RecipeApplicationEvidence及RecipeApplicationReceipt增加可空RequiredEvidenceCommit（复用RequiredCommitEvidence）。当前正式生产者必须携实际必要通信证据保存回执：同一Correlation、真实WriteId、ActualCommit/Validity、CommittedUtc及通信适配器实际收到存储回执的Host单调ReceivedTick；SavePurpose固定RequiredCommunicationEvidence。BusinessCommitRecordKind在末尾增加CommunicationEvidence，只标实际表身份，不暴露raw；无修订号则PersistedRevision=null，不伪称RunWrite或StageEvent。该证据不包含地址、报文、协议码或内部握手，业务不得据诊断原文补造它。

正式LatestProtocol与明确FullSimulation各从本次存储返回产生；设备DeviceApplied仍只在必要证据实际Committed、回执当前有效且原窗口内后返回。Application记录这份先已知保存回执随RecipePlanBound及提交后ReceiptObserved审计；自己的绑定/handoff回执仍不得预填。独立API及历史有限投影按真实WriteId/Correlation/不透明引用核对保存记录，只在确有观察时输出Host接收时刻/有效性，历史缺失保持null/NotRecorded。它与其他必要保存共用原总窗及CriticalSave，不新增成功审批或延长期限，不用空结构代替真实回执。

当前RecipeApplicationReceipt的完成资格包含此有效必要证据回执；旧历史不能因行存在或新模型默认恢复当前资格。不存在的raw引用、提交未知/失效均不授予后续动作；存储部分不可用仍沿F05/F06。真实实现及先失败/后验证归009 T037—T045/T047，三入口与独立进程BA验证分别计证。公开API仍使用已对齐§5.5形状，不新增页面字段或修改旧payload。
