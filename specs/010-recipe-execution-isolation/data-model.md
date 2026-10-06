# 010 数据模型

状态：Phase 1设计，尚未实现/运行。现有记录按[研究R07](research.md)只读保留；下列名称是本次重构的具体目标，不另建引擎或注册平台。

## 语义对象与关系

关系：环境解码 → RecipeDefinitionValidator → RecipeDefinition；定义＋本轮输入＋批准/能力 → AdmissionDecision；定义＋占用槽位 → RecipeRunPlan；计划＋配置XYZ＋姿态观察关联 → ResolvedExecutionTargets；这些冻结为 FrozenExecutionInputs。绑定和移交提交后，DetectionRequest 引用冻结输入和当前有效回执，进入唯一共同执行。

| 对象 / 归属 | 必要字段 | 校验及关联 |
| --- | --- | --- |
| RecipeDefinition / Application Recipes | RecipeId、Version、DefinitionDigest、ScenarioId、组成/成员/整体身份、物理槽及索引、必检面、相机组、阶段顺序、采集/算法需求、E规则、处置规则、可空容量对 | 一个共同校验器核验组成、路线、恰好一次必检、面/对象匹配、四面3＋1有效范围。无fixture字段、路径、Worker命令、TestEligibleSlots或不透明业务JSON |
| RecipeRunPlan / 复用现类型收敛 | Run/Tray、RecipeRef、PlanRevision、有序RecipeStep、实体/成员/面/测量轮、语义目标及需求引用、实际工作量 | 复用现展开与批次次序；不得由环境提供步骤清单替代规划。显示配方编号保持独立于身份和工序；不含ProfilePayloads/PositionPayloads |
| CaptureProfile / 复用DetectionCaptureSettings | Id/Version、ExposureUs、Gain、RoiPixels、LightChannel、BrightnessPercent、SettleMs | 单位明确、版本冻结；是请求，不是实际参数应用证明 |
| AlgorithmRequirement / Application | Purpose=SingleDetection/FaceFusion/EDecode等现业务用途、参数版本、输入数量、所需身份维度及结果合同 | 单图与同对象/面/测量轮双图输入不得混用；无具体测试能力默认值 |
| BoundCapability / 现注册设施扩展 | RequirementRef、CapabilityId/Version、ProviderIdentity/Version、合同版本、ApprovalRef | 由环境绑定兼容性检查；共同业务仅核验冻结引用及传递；不得按ID排工序。Worker路径只在适配配置 |
| CoordinateDefinition / Application | TargetId/Version、Purpose、Entity/Member/Slot/PhysicalIndex、Stage/Face/HeightRound/Camera、X/Y、Unit、Frame/Datum、Limits、ZBasis、SourceFactRef | 目标用途分检测、E、翻转、旋转、分拣；实体动作不因多个部位重复；来源引用不供业务解析文件格式 |
| MeasurementOffsetBasis / 旧版本历史依据 | 原测量scope/sample/单位/偏置/引用 | 仅用于真实存在的历史读取；011检测XYZ来自配置，活动主流程不再按旧高度＋偏置计算。核消费者后删除无用途活动分支，历史事实不改写 |
| ApprovedFixedBasis / ZBasis之二 | FixedZ、单位/基准、ApprovalRef、配置版本 | 必须有适用批准，缺测量不得自动转为固定值；生产现场批准缺失仍拒绝 |
| ResolvedExecutionTargets / Application | 坐标数值、目标身份、ResolutionKind、MeasurementRef或FixedApprovalRef、DefinitionDigest、SourceFactRefs | 每步引用唯一可信目标；没有0,0/frozen-plan占位；保留物理槽、对象、面、轮次及来源核验 |
| HandlingTargets / Application | 原位、翻转位及Automatic/Manual和既有等待预算、特殊旋转姿态及OK/NG/Pending出口、按实体和质量分类的分拣目标 | 复用当前工艺，不创造新模式；分拣容量/预留/在途/完成继续由共同业务核验 |
| DecodedTrayCode / 解析边界输出 | 实际扫码文本、料盘编号及EvidenceRef | 扫码文本就是料盘编号，不转成配方标识或按测试码换码。共同绑定按当前已保存目录唯一匹配，检查身份相容并冻结实际内容版本 |
| SourceFact / Domain既有来源模型复用 | ComponentKind、实际Provider/Version、Source可未知、关联采集/动作/调用、Media/Result/Config引用及可得摘要 | 事实不授予批准或动作权；允许同轮混合来源。不增/重排现来源枚举 |
| ApprovalScope / 配置与准入 | ApprovalId/Version/Digest、Purpose、允许场景/槽位/设备/能力/配方版本范围、依据引用 | 可区别Test/Production；允许槽位属于批准。与SourceFact分开，来源是Real也不能补足批准 |
| AdmissionDecision / Application边界 | InputDigest、批准/能力/预算引用、Eligible或Restricted、具体原因、受限动作 | 不返回另一执行器或预排下一工序。缺能力、坐标或批准时拒绝依赖动作，不默认OK |
| ExecutionCostProfile / 配置与预算 | Id/Version/Purpose/Source/Digest、FrozenBusinessBudgetRef/Digest、采集/算法/复位额度、OrdinarySortDeviceAllowanceMs、UnloadDeviceAllowanceMs及其他现语义动作额度、期限策略引用 | 边界按本轮冻结预算生成额度，不跨版本套默认；共同公式不解释PlcIo/PlcPoll或17/16计数。保留原三阶段工作量/起点/截止。绑定预算独立，Test10000ms不作生产默认 |
| FrozenExecutionInputs / Application | SchemaVersion、Run/Tray/Recipe/PlanRevision、以上定义/目标/能力/批准/预算快照及SemanticDigest | F唯一后冻结、真实意图提交；编辑只能影响后续轮。原文件摘要独立留作来源证据，业务摘要按语义数据计算 |

新解析格式与旧JSON格式的差异不得影响共同校验结果；非等价值会改变实际结果。测试配方ID可变但同轮F/选择/冻结/保存的身份链必须相符。它不能被当作预计动作的oracle。

## 请求、设备事实和保存

| 记录 | 设计内容 | 不变量 |
| --- | --- | --- |
| DetectionRequest / 收敛StagePortContracts | 当前调用关联、FrozenExecutionInputs引用、执行期限、运行用途及已准入配置引用、RecipeBindingReceipt（011 RC05.1；旧RecipeApplicationReceipt只读历史）、CommittedHandoff | 删除StrictRecipeExecution及把SourcePolicy混作来源/用途的职责；请求无默认对象或默认目标 |
| AuxiliaryHandlingRequest / 既有端口修订 | 动作/实体/目标/绝对期限、语义CoordinateEvidenceReference | 替代TestSourceReference；引用只是可核验证据，不能携带fixture对象供端口/业务解释；009通信合同保持 |
| CorrelatedCaptureFact / CaptureEvent扩展 | Request/Capture/epoch、MediaSource、CameraOrigin、LightOrigin、RequestedSettingsDigest、ApplicationState、可选ActualSettings、EvidenceRefs | 必须来自实际适配器且当前关联一致；ApplicationState为Applied/ConfiguredOnly/NotApplied/Unknown。固定图片选图只证明ConfiguredOnly并标重放；缺回执Unknown，不补SDK事实 |
| AlgorithmRequest/Event / 复用 | Run/Tray/Object/Face/Capture/Call、实际能力/参数版本、输入身份、意图提交、结果/释放/退出事实 | 不默认Test用途/能力/质量；媒体归属及input-release由真正事件证明，取消请求不等于释放 |
| RunWrite/StageEvent及必要保存回执 / 复用 | WriteId或EventId、Sequence/PersistedRevision、PersistedAt、Run/Tray/Plan/Binding、版本化payload和digest | 连续链沿RunExecution.SaveAsync→ITraceWriter/RunWrite；阶段事实/独立绑定沿原IStageEventStore。不得交换保存类别。只有真实提交产生回执，不预填自身提交后的观察时刻 |
| 结果聚合/最终结果 / 复用 | 技术状态、必检完整性、质量、物理处置、任务状态及明细 | NG优先且保留Pending细节；算法质量不掩盖动作/保存失败。受理、动作完成、提交、整盘完成、解锁、最终取盘完成分开 |

捕获请求值与实际应用事实并列存储。现查询若仍展示 RequestedCapture 应保持该语义；新ActualSettings缺失显示未提供，不用请求值填充。MediaRef用途对当前记录由运行上下文投影；历史无法关联时保持未知/未记录。F及公共算法调用事实新增本次实际IAlgorithmPort.Origin的关联保存（随AlgorithmFactPayload或关联SourceFact）；CallId/WorkerSession或请求的ExpectedComponentVersion不能代替实际来源。移交读取的是已提交来源，不能只读当前端口状态或历史推断。

## 冻结与期限记录

连续链冻结输入保存在现RecipePlanAndBindingIntent的版本化负载中，沿RecipeApplicationCoordinator→RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite及真实Write回执保存，由ITraceQuery按Run/Tray/PlanRevision/引用/摘要读取，不增表、不改009回执类别。其他阶段事实及独立绑定保留各自原IStageEventStore路径；不把连续链移到另一存储接口。缺少或不匹配直接拒绝续接。源配置路径不进入工序决策，原内容及摘要供诊断和审计。

| 入口 | 起点、期限与保存 |
| --- | --- |
| context/2.0 连续链 | 原绑定前位置冻结Detection/Sorting/Unload原期限并提交；绑定和保存耗时计入原剩余窗口 |
| context/1.0 连续链 | 原handoff提交后首次建立Detection期限；取得期限事实提交后才进入检测，既有后继阶段期限规则不改变 |
| 独立/recipes/bind | 读取关联运行已存预算和真实已有期限；没有下游期限就不创建；新意图/绑定/回执真实保存，不重建handoff、不授权产品续接 |
| 三者共有 | 配方应用总窗在本次意图提交后、排队/设备调用前唯一登记；取独立截止和已存在适用截止较早值。已建窗口不随重试、重连、暂停、配置更新刷新 |

v1/v2差异在已批准生命周期/期限策略，不进入检测业务分支。v1不再获得非严格校验特权。原绑定值10000ms与阶段成本分开；替换时不得调预算“让它通过”。

## 移交及历史记录

PublicPreparationHandoffV2的字段和摘要序列化保持；不向record追加字段使旧摘要失效。新语义输入通过原RecipeRunPlanReference、Run/PlanRevision找到已提交意图中的数据。验证包括真实handoff行/Write、digest、当前有效RecipeBindingReceipt（011 RC05.1；旧RecipeApplicationReceipt只读历史）、配置/计划/目标摘要和期限。历史读取不重建执行许可。

新v2的Source从已提交F识别事实取得，仅对应这条来源；每组件实际来源由EvidenceReferences查询，不概括整个运行。旧v2单值按原记录展示，不重新解释成全链真实。无法取得必要来源时留下Unknown诊断并限制续接；不写默认Test。该含义变更须先对齐003/008证据合同。

既有RescanWholeTray、RescanMediaCommitted及ThreeDRescanMoveConfirmed读模型保留；删除旧复扫执行不删除历史枚举/读取证据。旧事件没有应用回执、typed输入或身份时显示NotRecorded/Unavailable，禁止自动补造。

## 状态推进

共同链：运行受理 → 公共配置冻结 → 公共3D/F及必要提交 → F唯一配方 → 共同校验/准入/冻结计划 → 配方应用有效回执 → 已提交移交 → 检测及适用换面/旋转/E → 盘末适用分拣后下料 → 整盘结果提交 → 解锁反馈 → AwaitingManualRemoval → 已有授权取盘确认 → FinalUnloadCompleted提交。

迟到算法结果不能直接授权成功；按有效规则生成并保存Pending，且目标、安全、动作可信时才可处置。物理失败/UnknownHeld/必要保存失败不得混成质量Pending。取消使后继准入失效；已发动作/已开始提交只记录事实。正常暂停/人工继续不变成故障续跑；故障沿既有双端复位后显式新轮。
