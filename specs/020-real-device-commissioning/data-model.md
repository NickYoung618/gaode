# 阶段A内部数据与状态

日期：2026-10-08。以下是阶段A已实施的内部结构/不变量，不是新增PLC点位、公开业务DTO或数据库表。概念字段由现有类型及内部状态承接，具体源码与验证见[document-sync](document-sync.md)和[validation](validation.md)。

## HandshakeCycle

每物理轴X/Y/DetectionZ/ScanZ/GrabZ/R及父资源Flip、Sorting最多一个活跃周期。

| 内容 | 规则 |
| --- | --- |
| Resource、CycleGeneration | 固定资源键、本机递增代次，不能当PLC回显 |
| Operation/Action/Run/Plan关联 | 沿原身份；工具保留适用的recipeRunId |
| ConnectionEpoch/Generation | 正式沿原epoch；工具有效连接变化递增，不用进程session替代 |
| Phase | Executing、PhysicalCompleted、AwaitingClear、Closed、Blocked；父周期另含Holding/Placing中间阶段 |
| CompletionEvidence | 实际完成及位置/角度/放回/放料/安全位原证据引用，清零后不改写 |
| ClearWriteReceipt | 本周期相关PC清写应答及最后完成水位，写失败/未知不构成收据 |
| CurrentStageDeadline | 当前阶段原动作及适用phase最早截止；父关联跨阶段，但Flip与PutBack分别沿原窗口，不能借用已结束阶段的截止；清零不延长末段 |
| ClearanceProof / Failure | 可继续证明或阻断原因；物理完成事实可与清零失败共存 |

状态：Executing→PhysicalCompleted→AwaitingClear→Closed。失败、过期、断线或取消进入Blocked/原未知保持；Closed前不新开相关资源周期。Flip翻转完成→Holding→放回完成；Sort取料→持久提交→Holding/转运→放料→安全位完成，之后才到PhysicalCompleted。

XY/Z子轴完成自己的清零，父命令仍保持合法中间状态；不能因子轴Closed而清父请求。父周期未结束仅允许有据关联的下一阶段/子动作，不允许另一父周期抢占。

## ClearanceProof

字段包含资源/周期/连接、清写收据、所需字段集合、逐字段值及实际读取发起/完成水位、确认时刻、诊断引用。请求/反馈全部0；每次读取发起不早于最后相关清写应答；同连接、有效且在消费时未过期。

允许有限读轮次中不同读块，但不得拼缓存字段。任何字段不满足即无证明；超期迟到全0只可记诊断，不使失败周期自动Closed或重开动作。

## AxisReuseRecord

每轴至多一份：连接/轴代次、最近实际完成身份/目标/实测/单位及ClearanceProof。仅实际动作完成且双方清零后建立；PositionReused不生成新的运动完成事实。

使用时另取当前读取身份，检查请求/反馈0、安全/位置有效、有限坐标及原容差内目标；历史记录只作资格。由本次复核生成关联当前Action的定位满足证据并更新原reachedPositions，诊断注明PositionReused及历史资格引用，不能直接返回旧PositionReachedEvidence。全轴复用不写目标/启动，混合组只驱动需要移动轴，最后复核全部参与轴。

新派发、其他路径可能移动该轴、漂移、异常非零请求/反馈、非法位置、安全/就绪失效、已观察复位、未知或连接变化均失效。不从DB、日志、recipe.last_targets或上连接恢复。无记录的首次动作仍可按原准入合法派发，不能为了造记录自动试走。

## 工具HTTP状态兼容投影

见[工具契约](contracts/commissioning-tool.md)。保留state枚举；内部AwaitingClear投影observing，plcCompleted=true仅表示已观察完成，cleared=false；Closed才completed、cleared=true。复用时completed、reusedPosition=true，明确没有发启动，不制造本次新运动。

可选诊断字段：handshakePhase、connectionGeneration、clearWriteResponded、feedbackClearConfirmed、clearEvidenceRef。旧消费者忽略新字段仍能正确等completed；reason/clearError用于现页面中文等待/错误。人工清请求不能投影为正常运动completed。

## VirtualPlc内部事实

设备自行维护轴运动/完成停稳代次，实际模拟到位才建立；撤请求保留停稳事实但清外部反馈。新移动/复位使其失效；Sort仍核内部完成、当前不运动、请求0及有效匹配实测。不得复制Host记录、按目标直接造完成、用初始静态坐标充历史动作。

## 保存与恢复

清零/复用记录仅内存，生命周期不跨连接/进程。位置/角度/取料/放料按原schema保存；清零阶段与原始样本在原诊断中关联，不建新数据库。实际完成、清零证明、必要保存分别成立后才整体成功。未知控制义务不会因人工写0或后来全0自动解除。


## 阶段B增量设计（未实现）

| 对象 | 必要字段/关系 | 校验与冻结 |
| --- | --- | --- |
| AuthoringSource（既有正文/维护输入） | 同型号/场景/对象/路线键、显式SourceRecipeId及source版本/hash、完整点位/采集/算法配置 | 显式来源ID优先；无ID才要求相容键唯一，共同保存；不复制Approval；不引入前端技术编辑器 |
| RecipeDefinition/RecipeRunPlan（既有） | P01–P10正文、服务器RecipeId/Version/DefinitionDigest、PlanRevision | 保存/重读/编辑同模型；启动公共快照，F后冻结配方；在途不变 |
| CommissioningConfiguration（新增版本化配置概念） | id/version/digest、Purpose=RealDeviceCommissioning、现场公共/预算/PLC/机械/相机/算法/灯引用与来源 | 启动核引用一致、提供者矩阵与版本；缺值不默认；不预建新数据库表 |
| SafeVirtualInput（新增配置概念） | id/version/digest、用户依据、预期配方范围/槽/对象/阶段/角色、受控输出及单位/坐标系 | 启动冻结前置3D/F输入，F后绑定实际Recipe摘要；实际媒体请求关联；错配拒绝依赖动作 |
| RecipeValidation/Admission（复用共同软件检查） | 当前RecipeId/Version/DefinitionDigest、运行用途、实际槽/配置及校验问题 | 校验保存后可选运行，启动复核当前条件；不新增人工授准对象/批准清单，编辑后重新校验 |
| PerCaptureSettings/AppliedEvidence（共享扩展） | 原CaptureRequest设置摘要、相机实际读回/应用状态、灯模拟消费状态、Profile版本 | 同相机gate设置到新帧；不支持失败；逐组件事实不得合并成假全Applied |
| Frame/Media/Algorithm事实（既有扩展） | Run/Capture/Operation/Intent、session/epoch/frame/trigger、媒体引用、算法提供者版本及受控输入摘要 | 原意图/实际提交/MarkCommitted链；历史缺字段Unknown，不改历史摘要 |
| SiteProtocolCapabilities（内部布局能力） | 明确布局标识、已确认信号/编码、所需安全能力可靠性 | 未明安全Unconfirmed；采样/握手/恢复同一来源，不新增PLC信号 |

配置加载→校验→冻结→实际调用→事实提交沿现状态机。软件校验且保存后配方可选运行，启动设备条件另核；受理与完成分开。新的联调用途必须贯通schema/能力/预算/存储准入与历史读取消费者，不能只增加字符串枚举。字段最终落位在tasks中逐一绑定；B-DEC-01已关闭：本轮不新增授准API；现Approval不作为人工审批前置，历史字段保留，其他用途不借本轮放开。


B共享载荷补充：CaptureRequest增加可选PublicSettings承接公共ExposureUs/LightLevel，与DetectionSettings按角色互斥；ThreeDStep/FScanStep/RecipeDetectionExecutor.Observation必须传递冻结值。无设置CaptureOnly单独标记，不能给公共Gain/ROI/等待造默认。设置等待由采集适配层统一在灯设置后执行一次，沿原剩余预算。
