# 通信诊断、来源和历史读取合同（目标）

**当前适用范围（2026-10-02 10:54）**：通信代码边界最小收敛。保留现行业务语义/期限/取消/安全/保存规则；当前只核边界、正式接线、构建、既有内容检查/正负例和固定直接语义集合。完整动态PD/36CS/M/MC、持久历史/升级/预算/故障/整机验收转出，不作为本轮完成条件。见spec活动条款、verification-gates BM00及scope-adjustment；转出不是Passed。

合同标识：`plc-evidence/1`。适用FR-020—022、035—039及必要保存失败验证；未实现。不扩建通用日志平台或全量通信归档。

## E01 同次观察与真实捕获

ModbusTcpClient在实际请求/响应点捕获不可变RawExchange；命名信号访问器和解释器用ObservationId关联同次读取及其语义输出。ActionId、run/operation、epoch来自当时已建立的上位机关联，PLC侧本机actionSequence/generation单独保存，不能冒称PLC带有Host身份。多次交换分别记时；读取到的实际坐标与下发目标分字段保存。

必要证据包含实际区域、文档号/PDU地址、方向、原始字/完整报文、字序、协议/解释版本、时间、事务/连接、错误及已有业务关联。无响应则Response=null并保留超时/断线；未知反馈原样保存，不填目标、缓存或已知成功值。

## E02 保存与引用

动作命令/关键反馈、据以放行的状态变化、首末通信失败、未知/超时和关键保存失败上下文为必要批次。正常高频轮询/心跳可以限频或有界缓冲，关键样本在覆盖前提交。现有有限窗口/gap语义保留；截断必须可见，不把部分窗口说成全量。

Infrastructure通信专用writer经现有TraceWriter实例的内部有界队列提交`PlcCommunicationEvidence`，不经过Application raw DTO。业务只得到不透明引用；一个批次提交后可被多个业务事实引用，不能因重复引用创建假报文。

引用生成条件是本批次SQLite事务实际Committed及摘要/身份可查。字符串格式、队列受理、ILogger输出、非空GUID、VirtualPlc内存审计都不替代提交。必要保存超期/失败/未知：不得返回当前动作的可靠完成；无有效成功引用不证明库内无记录（数据模型§4.1）。若已观察取料，关闭后继、保留UnknownHeld并走E02.1，不能写成“取料未发生”。心跳、状态失效判断和停止控制不等待该磁盘队列。晚核查所得持久引用只供历史查询，不能恢复旧动作批准。

通信证据先提交、业务事实后引用是两个短事务，不假装跨设备共同事务。业务提交前失败且回滚确认时可有raw行而无InTransit；业务提交后回执未知时也可有InTransit，必须保留实情。授权后继的业务提交不得指向不存在的必要证据。等待保存时释放传输I/O锁并继续轮询/心跳，原动作占用仍保持；不在通信泵内同步阻塞磁盘，也不让后继动作越过待提交状态。公共3D既有失败清理按BD04保留，但不能借清理制造业务成功或已持久引用。

### E02.1 取料后证据持久化未确认（R05）

1. 通信发送BD B05.1的类型化失败通知，保持当前设备在途占用；业务关闭生产准入并保持预留。观察到取料与物理安全关闭均是不同事实；本出口为UnknownHeld，不是可清占用的普通Failed。
2. 若业务库仍能写，业务保存最小UnknownHeld事件：Run/Operation/Action/Attempt/Plan/Epoch、已提交预留、已知源位置及观察时刻/ID、PhysicalPick=ObservedAtCurrentSource、ActualCommit=ConfirmedRolledBack/Committed/Unknown、ReceiptValidity=None/Invalid（已提交但回执失效不得写成回滚）、原因及禁止后继。未获有效引用保持null；事件自己的真实提交回执只能证明失败记录已存。其payload不含raw，不伪造InTransit或物理来源。
3. 若业务库也不可写，既有RuntimeDiagnostics/ILogger输出关联的结构化错误，Test启动脚本现有stdout/stderr重定向可作为持久载体，但只有实际文件存在且可读才算日志证据。磁盘/全部存储不可用时日志只是尽力，不能保证本次取料事实已经持久；报告该缺失，不能签发诊断保存通过。
4. 重启不因缺失败事件、缺InTransit或投影Held=false释放占用：先只读核对已提交预留、动作意图/Started、终态/恢复决定，可能已派发而无可靠关闭的动作保持Held。依据是`SortingTargetAllocator:46–54`、`ThreeStageWorkflowExecutor:492–499`；不能把`StageEventStore.RecoverAsync`（会追加UnknownHeld）描述为只读操作。
5. 存储恢复后按原批次/事件/幂等身份核查实际行、摘要及关联，区分已提交、回滚确认、仍未知；只允许补记真实核查事实。不能重新执行取料、补造raw或恢复过期放料授权；实际设备状态与USR-D双端复位/显式新轮仍为恢复前置。通信层始终不接管业务事务。

### E02.2 配方应用窗口及迟到事实（完整业务验收转出，直接保存约束保持）

绑定证据沿用同一通信表、业务TraceWriter/既有事实保存责任，不另建平台。通信捕获实际收发并关联当前BindingId/ActionId/ObservationId；业务事实记录冻结BudgetReference、t0/D/T、原适用后段截止、Host接收/校验时刻、必要WriteId、ActualCommit及ReceiptValidity。窗口数据在本次必需绑定/移交或失败事实中保存，不额外制造新的无界准备阶段；意图及冻结来源先真实提交。

上述字段分时产生，不要求同一原始事务包含未来信息：意图只存当时预算/关联；t0登记后才随后续事实保存；自身commit从真实存储记录读，Host回执观察由Application通过既有Audit路径另追加`RecipeApplicationReceiptObserved`，不回写不可变handoff。该观察为关联诊断、非新增成功审批，按模型§3.1有限保存且不递归审计自己的回执；未保存时Host时间/有效性历史为null/NotRecorded，验证证据不完整，不能由行存在合成ValidCurrent。

通信返回DeviceApplied前，必要raw必须实际提交并有当前有效引用；返回后业务绑定/适用handoff保存仍受同一总窗及原CriticalSave约束。设备已确认不是业务完成；RecipePlanBound行存在也不证明适用handoff回执及时。总窗内每次保存按`min(入队+CriticalSave,T)`约束；超期后必要失败/迟到事实沿既有有界保存规则收尾，不新开动作窗口，不恢复Bound/移交授权。

沿E02与模型§4.1分开“已提交”“当前有效回执”：保存前真实失败且确认回滚、提交后回执迟到、提交未知不可混写。实际已开始提交可能在请求失效后完成，历史如实记录并能按WriteId只读核查；没有成功回执不等于库无记录。迟到证据不能补造早于T的Host接收时间，设备时间也不能回填；恢复只读查到提交不能恢复旧动作。库不可写仅尽力日志并报告证据缺失，不承诺新绑定事实已持久。

公开输出只使用影响矩阵§5.5列明的语义来源/时限/结果字段；raw仍只供受权诊断，业务不读取raw判断绑定。旧历史缺预算/窗口/回执元数据时返回null/未记录，禁止补10000、反推旧截止或重写旧RecipePlanBound。V-BIND的BA02—BA05取得同请求的真实通信与SQLite提交/回执时间线；没有必要持久证据不得计入SC-012通过。

## E03 只读查询与API影响

目标接口为受现有Read权限保护的 `GET /api/v1/station01/diagnostics/communication/{evidenceId}`，另支持现有run evidence响应返回引用集合。路径是**待实现设计**。由Infrastructure读取存储，Host只路由/序列化；Application/Domain和业务测试禁止引用reader/RawPayload。响应不提供控制操作、不得供业务判断完成。

诊断响应包含EvidenceId/ObservationId、关联、真实捕获状态、payload schema/摘要/来源、采样/提交时间及原始交换；缺引用404，历史无raw明确RawUnavailable，不能返回编造的空成功记录。校验调用者授权和StoreId归属，不接受外部任意数据库路径。

业务`/status`改为语义状态/报警，目标schema `s01-status/2.0`；run及evidence的新设备事实以`device-semantics/1`标识。协议地址/报警bit不放回业务响应。通知保持提交后提示GET对账的作用；语义结构变化必须在003/006/007/008对应合同先列明并对齐。既有页面只绑定已批准区域，无新页面/控件。

逐字段规范采用[影响矩阵§5](../impact-matrix.md#5-公开字段与历史表示映射r04)：包含旧名/类型、保留/删除/语义替换/诊断归属、目标类型及null、版本、生产者/消费者/调整角色和历史表示。通知目标`s01/notification/2.0`仅固定业务摘要，不再序列化完整RunSnapshot；客户端只据修订提示GET，不从摘要或raw恢复控制。未登记字段不被默许继承。

## E04 历史和来源真实性

新事实使用适配器真实ExecutionOrigin。Host生成的汇总标Derived，同时保留输入组件来源；不得以“Test运行”统一改为Virtual，也不得用真实TCP就把虚拟设备记为Real。

旧Writes/StageEvents/Operations和原报告原样保留。有限历史读取器在Infrastructure按显式版本或已知旧payload形状读取；未记录版本保持Unknown，不能按当前协议表猜历史值。`SortingTargetAllocator`旧合成protocolStatus及写死来源只作为LegacyRecordedClaim展示；不覆盖旧字段、不升级为ActualWire。历史实际PositionEvidence也不能据此生成丢失的请求/响应。

历史业务完成事实可按原提交内容查询；不足的物理证据标Unavailable，不用当前语义倒推旧反馈。历史查询无AllowedActions，不参与新运行准入/恢复完成。该有限读取只维护既有历史责任，不提供旧生产协议在线适配或双协议兼容。

现历史run重建默认没有StartupDiagnostic；旧StartupReadiness Audit也未保存完整StopStage。读取器只解析实际存在且关联成立的字段，不能承诺恢复全部旧诊断；缺完整对象为null并说明Unavailable，部分已存内容只作LegacyRecordedClaim。旧PositionEvidence键/嵌套大小写保持原payload，不把当前camelCase映射倒写历史。历史记录性质、RawAvailability与实际Provider分开，不能把未知来源猜成Virtual。

## E05 数据库与验证

009保留T019受控新Test库、必要通信表/实际writer/只读reader及正式StoreCompatibilityProbe准入，manifest在同库Manifests中，不能只看版本字符串开放，也不由Host自动迁移。旧库单项schema升级完整验收转出，原data-model§6.1/U1规范继续有效，T020/T060历史勾选与证据保留。
当前核诊断捕获/保存/读取职责与业务控制无回路、不补造历史及必要writer接线；成功/未知/超时完整持久读回CS07/CS10及SU转出。直接改动触及必要保存时补最小真实SQLite验证，既有回执/UnknownHeld规则不降低。


## 当前专项验收对接

正式生产者/全部直接消费者及当前业务语义必须闭合并构建；只针对协议边界和直接受影响动作验收。已确认预算/三入口/必要保存/安全不删减，通信传入稳定期限并承担内部握手/取消，不把raw封装给业务。当前检查及直接语义集合见VG BM00；动态S00/PD/M/MC及整机/页面/完整历史/完整预算验收转出不等于完成。


### 010 S修复定向对齐：取放料通信证据接续（2026-10-02）

010实际S暴露取料至放料完成超过有界通信缓冲覆盖期，必要保存按gap拒绝。沿用E02的已提交分段引用：MaterialPicked原始证据真实提交后，通信内部记录从该批次截取点开始的下一段；MaterialTransferred必须关联同一Run/Operation/Action/Epoch的已提交取料段，保留两个实际位置与取放反馈/ACK。分段不重开期限，不产生新的业务许可，不把取料已存等同于业务InTransit已存；放料仍等待当前有效CommitPick回执。任何段缺失、gap、错关联或保存失败保持UnknownHeld并禁止自动重放，失败诊断保留已提交前段引用。业务端口仍只接收语义结果/不透明引用，历史plc-evidence/1 reader不改写旧记录。

010 T030/T044/T047承接此直接阻断：原取放真实SQLite/反馈组件增加两段关联检查，原取料保存失败负例保留；S验证原慢动作输入下完整一次搬运。受影响构建/组件纳入当前B必需清单，旧失败/冻结/E保留原身份；修复后重新B→冻结→E/S→T。此处不重开009动态全集，也不调整轮询、预算或预期。

## 013实施前定向同步（2026-10-04）

SY-02/03：FR-008、P03/P04当前由013 A01—08承接：合法有限读计划在不可变映射准入后生成/校验/复用，非法映射与未知地址扩读仍拒绝；两连接、通信内部单源/有限PDU仲裁，不把协议知识或轮询参数放入业务。E01/E02/E04的基础与Position各自真实采样起止/代次/可靠性，不能用新心跳续旧值、拼接成原子快照；普通位置陈旧不等于断线，关键准入按需实读，完成后实际位置先发布。Domain观察形状及plc-evidence/1保持；Host device-semantics/1.2位置新增SampleStartedUtc、SampleEndedUtc、ConnectionEpoch、Reliability，Axis消费者使用Position.Identity.Reliability。保留8192窗口、gap拒绝、1024分段、真提交/保存门及旧历史原文。T022/观察/证据历史勾选不变；新实现归013 T011—T025。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。
