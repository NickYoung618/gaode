# 数据模型增量（目标，未实施）

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

原制定日期2026-09-26；2026-10-03按宪章8.0.0定向同步。共同配方/选择/冻结字段唯一见[recipe-contract/1.3](../011-plc-interaction-update/contracts/recipe-contract.md)，本页承接执行结果关联。复用RecipeContracts/RecipeRunPlan、s01-handoff/2.0引用、TraceWriter/StageEventStore及SQLite，不另建业务库。
以下为必须表达的字段与关联；优先扩展现有记录/事件投影，不要求每行新增一张表。

| 对象 | 必要数据 | 校验、状态与关系 |
| --- | --- | --- |
| RecipeChoice | recipeId、version、catalogDigest、model、scenarioId、purpose、route、可用性及受限原因 | 后端目录只读条目；前端选择为期望，不是已完成F绑定；一个目录中F唯一 |
| StartSelection | requestId、已有运行上下文、expectedRecipeRef={recipeId,version,catalogDigest} | 在正式POST中保存选择意图；旧version/catalogDigest仅为展示时观察来源，不锁定F时内容。F时核RecipeId/场景/用途，读取当时真实保存内容；冻结后不可暗改 |
| RecipeExecutionSnapshot | recipeId/version/digest、scenario/route、point/profile/budget引用、planRevision及对象/步骤 | 启动冻结公共配置；F料盘编号唯一匹配已保存的新内容并冻结完整产品计划，旧运行保持原快照 |
| ObjectBinding | runId、unitId、可空groupId/memberId/assemblyId/partId、material、physicalEntityId、sourceSlotId、physicalSlotIndex、pointRefs（旧protocolSlotIndex别名只供历史读取） | S1单件实体；S2组内每成员是实际实体；S3部位归属一个整体实体；序号不是物理槽号 |
| InspectionTarget | objectId、localFaceId、cameraPair、capture/light/algorithmProfile、pointRef、executionStageId、observationRef、coordinateConfigVersion | 一个面用AB或CD两路；同相机不同面/对象不能混用；对象必检列表独立于SortUnit |

> 旧协议历史实现范围：第四批组件接线中，`DetectionStepTarget`是一次Position步骤的冻结输入，另携step序号、sourceSlot/protocolSlotIndex、相机、显式XYZ/单位/坐标系、目标来源及Z依据。`Test/InjectedXYZ`仅供隔离组件运行，正式公共移交只解析带批准来源引用的冻结点位和独立源点；高度结果到Z的换算仍未配置。
| RecipeStep | 沿用kind/sequence及对象/面/profile；补ownerStage、actionTarget引用、实际搬运实体、flipMode及依赖 | 每步有唯一所有者；普通Sorting/Unload由外围消费；受限步骤不可按完成跳过 |
| HeightFact | scopeId、round、测量输入/call、单位/基准、对象映射、有效性和来源 | 初始实际测量事实保持；实际3D观察有无/姿态/F XY，翻后复查另存观察事实；检测XYZ来自配置，保留历史测量读取但不设执行前提 |
| StepIntent/Fact | run/plan/step、operationId、attempt、deviceConnectionEpoch、deadline、目标/实际轴坐标/面姿态、错误及source | 意图提交→准入/派发→可靠反馈→结果提交；写成功不等于动作完成 |
| FaceResult | runId/planRevision/objectId/faceId/executionStageId/observationRef/coordinateConfigVersion（旧measurementRef只读历史事实）/batch/cameraPair、两mediaId、单图call及fusionCall、completeness/quality | 只有同键且不同对应相机输入齐全才融合；输入缺失不判OK，缺失依据保留 |
| Object/Group/AssemblyResult | id/kind/parentId、requiredTargetRefs、子结果、完整性、quality、decisionRuleRef、dispositionState | 单图不是面；面不是组；S3部位汇总后整体搬运；NG优先，分别保留NG面与Pending面；S2仅问题成员处置，组结果不覆盖成员事实 |
| SortingAssignment | entityId、sourceSlot/point、targetRegion/cell/point、reservationRef、resultRef及group关联 | 源目标与实际槽索引齐备才派发；目标容量来源有据；不同实体不能同时占同格 |
| PositionFact | entityId、origin/lastKnown/current位置、在途/旋转占用、关联动作/epoch/source | 匹配放料反馈才更新；未知不释放。当前NG/Pending到同盘各自配置区，姿态异常跳过后续检测，最后从原槽实际分拣到Pending；旧特殊出口记录仅为历史，不成为跳过当前分拣的旁路 |
| ManualOperation | actor、channel、run/plan/action、操作种类、expectedRevision、确认依据和结果 | 仅本期换面/取盘/必要恢复；软件确认不替代安全反馈；不新增示教模型 |
| Completion | Detection、Disposition、Unload、WholeTray、ObservedUnlocked、Removal、Final提交引用 | 每层事实独立；全体必检与应处置收敛，无未处理在途且必要保存成功才能Final |

## 身份与生命周期

1. 前端加载后端条目并选定期望配方，后端受理为Accepted；此时不是RecipeBound。
2. 必要公共准备、实际3D和F完成后，只取一次当前已保存目录快照，F料盘编号唯一匹配并核选择RecipeId/场景/用途。展示时旧version/catalogDigest不阻止使用新内容；匹配或身份不符保存实际原因并阻止产品动作。当前Bound只由RC05.1必要业务提交回执形成，不要求旧PLC配方ACK。
3. 冻结计划含本盘实际对象、必检项、点位/参数和预算。每个输入/动作沿相同run/plan及适用对象/面/轮次关联。
4. 检测完整性、质量、动作状态、处置状态与任务完成分别保存；Pending不表示物理安全，超时不表示设备已停止。
5. 012负责共同校验后的实际保存与目录重读；已冻结运行不重载。真实保存成功后，后续F绑定使用新内容，旧快照保持不变。
6. 未知动作保留原任务和位置，由已确认恢复流程核对；不因重启、重连或重新读取handoff自动重放。

## 持久化与版本

对象及位置不再从SortUnit和0,0占位推导。s01-handoff/2.0信封继续引用已提交冻结计划；新执行所需数据从版本化计划/对象映射读取，缺必要事实的历史handoff不能用于新路线续跑。
计划及API投影新增字段需要明确schema/修订摘要并同步生产者、消费者和相关测试；历史记录保持原结构与含义，不回填新动作、不增加为了旧测试启动新流程的兼容分支。
先复用已有事件及结果存储；确需新增表/列才通过StorePrep维护迁移。Host不得自动改表；机械动作与DB事务分开提交。
来源按组件保留Virtual/Simulated/Test/Real、组件版本、输入及证据引用。公开API不返回本地路径或原始堆栈。

## 必要诊断

日志按命令/流程、设备/安全、采集/算法、保存分类分级，携requestId/commandId/runId及适用recipeVersion/planRevision/object/face/operationId/captureId/callId/epoch。
记录期望、实际、原因与处置；原始异常存受控日志，重复轮询有界聚合。日志索引提交事实，不替代数据库提交。

## 新协议最小身份合同（目标，生产者与消费者同批修改）

检测面localFace、执行阶段executionStageId、真实测量measurementRef、坐标配置coordinateConfigVersion与设备deviceConnectionEpoch分别表达。heightRound若保留仅表示实际测量轮次，禁止face2自动生成round2；coordinateEpoch不得兼任连接代次。DetectionStepTarget携上述身份、该面独立XYZ及zBasis/sourceRef。采集/融合按同对象/阶段/面/相机对关联，测量可被多个有明确映射依据的面引用；不据同measurementId合并不同面。

初始3D观察绑定真实capture/call、物理槽位、有无/姿态/F XY；翻后姿态复查另存真实观察，检测XYZ来自冻结配置，旧HeightFact只承接有效历史消费者。见[点位合同](contracts/test-virtual-mapping.md)。SortingAssignment的源/目标均绑定本盘，预留→取料后InTransit→可靠放料及后续抬升完成→完成提交/目标Occupied；未知保持预留及在途，不因状态2完成。

协议身份、原件SHA、配置版本和实际动作证据贯穿保存/查询/来源矩阵。普通盘末阶段顺序为Detection→Sorting→UnloadPreparation→WholeTrayCompletion→ObservedUnlocked→Removal→Final；到下料位不得产生allowedActions取盘许可。

本次确认增量：人工采用面须区分commandedFace与采用来源（命令默认/人工确认），不得填成实测；故障恢复保留failedActionRef、旧attempt与reset/check；新run完整执行产生独立动作/attempt和反馈，并关联旧故障。问题记录复用事件/结果投影，含run/组/成员/面/步骤、原因、继续依据和待现场项。字段以共享接口设计为准，不要求新表。

## USR-20260926-D故障新轮模型

faultRestart字段和状态由[003合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)唯一规定。旧run保留故障结果和必要媒体，新增关闭执行及reset/check关联事实；新run的Plan/InitialMeasurement/RecipeBinding/Detection/Movement/Final均重新产生。合法recipe/config版本允许继续选用，但snapshot、初始测量、F绑定、结果引用和预算不得跨旧新run。问题按run/recipe/group/entity/face/step关联，faceSource区分命令默认人工确认与设备实测。采用现有事件/查询及必要最小字段，不新建问题平台。

## USR-E配方适用性与动作证据

RecipeChoice/冻结对象按[002准入合同](../002-plc-xyz-recipes/contracts/recipe-execution.md#usr-e当前业务准入目标未改配置)检查四面3＋1，历史可查与当前可执行分开；不重写旧快照。覆盖记录分允许变体、实际采用配方、选定验证理由及构建适用性，不以总行数判完成。StepIntent/Fact沿用[003动作诊断字段](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md#usr-e动作级诊断增量)，分请求/设备接收/实际采样/Host校验；SortingAssignment只有本次取料完成及所需坐标证据成立后才进入放料提交。保存与USR-D身份隔离规则不变。

## RES结果展示的持久事实边界（2026-09-26）

现有FaceResult/ObjectResult的业务quality在公开API映射为disposition，API quality仅指事实质量；详见[唯一字段定义](contracts/api-results.md#res结果展示增量2026-09-26目标尚未实现)。继续复用WriteBatch事件、StageEvents、AlgorithmCalls及媒体，不建立页面结果库。单图AlgorithmFact关联callId/captureId/mediaId/冻结步骤，面融合关联输入callId/mediaId及object/localFace，汇总关联子结果与必检引用。仅关联完整且已提交的事实可投影到对应层级。

当前PythonWorkerAdapter只传递检测disposition，IntegratedDetectionPort保存DetectionDisposition/RawCodes/ErrorCode、调用/采集/面融合关联；没有缺陷坐标或置信度事实不能补造。已有参数只从冻结配置及已保存RequestedCaptureSettings取得，配置版本本身不等于参数值。008 T054补必要关联/已提供事实的保存缺口，不要求本轮算法增加输出；若字段实际由算法提供且当前适配遗漏，先保留原响应证据并在既有事实保存链接入其已定义语义，不扩展算法业务。

仅需事件payload补充时沿既有版本化事件；确需表/列变化才由StorePrep维护迁移，Host不静默改库。resultContext、resultRevision和inspections是只读投影，不是新执行状态；当前对象来自已提交步骤，查询不得写回或触发物理动作。旧事实原样保存，缺失不回填，USR-D旧轮证据保留及新轮身份隔离不变。


USR-D事务落实：RecoveryNewRunLinked Audit payload携带newRunExpectedRevision/newRunLinkWriteId；Writer同事务校验新Runs及Commands身份，更新两轮revision并写RecoveryFromFaultRun。既有Writes承载两端事实，commit前不可发布检查消费或派发资格；不新增数据库结构。
