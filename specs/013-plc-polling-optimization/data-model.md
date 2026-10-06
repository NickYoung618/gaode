# 数据与观察模型

> 当前验收解释：2026-10-05需求方批准 **013-acceptance/2**，详见[验证合同V06.2](contracts/verification.md#acceptance-v2)。普通25ms等新增工程目标改为非阻断观察；原期限/保护、局部首态75ms、确认周期、单源/计划、预算/净收益及证据完整性仍硬。原数字和历史失败保留。下文历史“全部V06成立/不得倒改门槛”以本次显式批准范围解释，不能据此修改硬条件。


**013 / Phase 1 / 2026-10-04**。这是现通信适配器内部模型及最小共享语义调整，不新增数据库实体、业务调度器或协议型业务端口。细则见[采集合同](contracts/plc-acquisition.md)。

## D01 PreparedPlanSet（通信内部）

| 字段/关系 | 规则 |
| --- | --- |
| AdmissionId、DefinitionVersion、MappingDigest | 标识本设备准入及冻结映射；摘要规范化覆盖地址空间、地址、宽度、方向、可读性和编码定义 |
| FrozenDefinition | 准入后不可变副本；调用者继续改原集合不能改变已准入设备 |
| Plans[Purpose/FieldSet] | 有限用途的不可变块数组、解码映射；准入时生成/校验 |
| Block(space,start,length,fields) | 仅已确认合法连续范围，不跨空洞，保留完整字段宽度 |
| PreparedCount | 每准入准备一次集合；每计划一项准备记录，运行不重建 |

先完成所有定义和计划校验才允许I/O。计划来自其他AdmissionId/摘要必须拒绝；同映射重连仍可复用计划，但观察全部换代。映射变化重新准入；不提供动态任意字段计划兼容入口。

## D02 ReadStamp / BlockObservation（通信内部）

每次实际块读记录ConnectionId、ConnectionEpoch、Sequence、WireStarted/Ended（单调时钟）、SampleStarted/EndedUtc、Reliability、RawTransactionRefs及真实解码值。排队/计划到期不冒充采样时间。响应后核对原截止/代次，迟到值可供诊断，不能发布成当前可靠值。

Reliability沿用现可靠/陈旧/未知等语义，不创造“读成功即业务可用”。同一块只被一个持续源发布，消费者通过引用共享；内存只保留当前观察及现有有界原始窗口，不能无限累计历史样本。

## D03 GroupObservation（通信内部）

| 对象 | 组成 | 消费规则 |
| --- | --- | --- |
| Heartbeat | H块、最后有效边沿单调时间、原3秒截止 | 与业务观察独立；初值同步不计边沿 |
| Base | 六块及各自ReadStamp | 准入/故障/模式/占用；R0080块快档时复用该唯一来源 |
| Position | 两块完整五轴及各自ReadStamp | 一次发布完整五轴，不用最新一轴改写其他旧轴时间 |
| Flip / PutBack | 各自反馈块+当前命令基线 | 不同动作/实体/代次不能互用 |
| Transfer | Sorting反馈及完整Position块引用 | 每个等待观察仍核XY/GrabZ；同Position供监视 |

组Identity的SampleStarted为最早必需块开始，SampleEnded为最晚必需块结束，仅描述实际窗口；不是原子快照。内部资格/年龄用所依赖最旧块及每块因果边界，不单凭最新SampleEnded。缺块、跨代次、过期或不满足顺序都不能宣称整组可靠。

B在轴首状态快档时由其余五块+B所引用的快反馈组成，仍保留各块真实时间。它不因为快反馈新而刷新其他五块年龄。T的完整位置是两个实际块，不能把Sorting反馈时刻当五轴同时发生。

## D04 AcquisitionDemand / ObservationWait（通信内部）

| 字段 | 规则 |
| --- | --- |
| DemandPurpose、RequiredFields、CurrentOwner | 只存在通信层；协议组不进业务参数 |
| ActionId/Attempt/Entity/Purpose、ConnectionEpoch | 当前动作身份/用途；业务提供已有语义身份，适配器绑定通信事实 |
| MinimumReadStart / CommandBoundary | 确定新观察必须在哪个因果点之后开始 |
| AbsoluteDeadline、Cancellation | 使用原ActionWindow；I/O截止从入队计，不另起完整预算 |
| RequiredVersion / notification | 多消费者等下一版本；原子注册防丢唤醒，不轮询同缓存 |
| NextDue、InFlight、MissedCount | 每组至多一轮；错过只计数、不入队追赶 |
| PlannedDue、Enqueued、FirstWireStart、LastWireEnd、Published | 对应A03.1的d/q/s₁/eₙ/p；与每块sᵢ/eᵢ联合计算首排队、块间让出及完整发布时间，不能只累加交换耗时 |
| OwnerGeneration、PlanCursor、NextSlot、PendingRCount | 固定来源的所有权版本/当前块游标、K/R下一槽及P等待普通R次数；快档插入不重置计数，切换撤销旧所有者未发块；不是业务调度接口 |
| PolicyId / PolicyDigest | 通信内部plc-acquisition/013-1及实际冻结值摘要；与设备准入实例关联，V02.1记录两侧策略差异，不进入BusinessDurations |

发布仅唤醒“重新核查资格”，不直接批准动作。等待者的取消不取消共享采集；取消/截止、连接变化可立即终止个人等待。后继派发再次检查资格，防等待返回与写之间的竞态。

首态从第一条当前start命令完成激活；同批后续start仍按序，每轴记录自己的CommandBoundary，全部当前轴观察齐才退出快档。已发旧所有者PDU返回只保留真实stamp，不能重新派出被让渡块或用无因果资格旧值完成新动作。完整发布耗时C与最旧依赖age分开，快档保守间隔为下一p减上一s，含全部等待和发布；具体有限选择、服务机会和条件见A03.2—4及V06。

## D05 状态转换

| 对象 | 转换 | 不允许的捷径 |
| --- | --- | --- |
| 采集 | Idle→WorkPending即时切档；Moving/UnknownMotion→500位置；ConfirmedStationary→1000 | 没有配方/页面空闲不能判设备空闲 |
| 轴批 | Baseline→CommandSent→AwaitMoving（局部50）→AwaitArrived（200）→ReadActualAfterArrival→Validate→Clear→Complete | 直接旧Arrived、目标当实测、坐标读在到位前、漏同批轴Moving |
| Flip/PutBack | Baseline→CommandSent→AwaitExecuting（局部50）→AwaitCompleted（200）→原证据/完成 | 用另一反馈或旧完成；缺Executing仍成功 |
| Transfer | SourceXY→SourceGrabZ→Pick→本次反馈/证据→真实InTransit提交→Lift→TargetXY→TargetGrabZ→Place→本次反馈→Lift→Idle清零→Complete | 保存未获有效提交就Place；坐标只在最初检查一次 |
| Inspection release | 释放意图→新的合格base观察→原证据段/完成 | 等旧缓存自动变新；恢复已删除采集ACK |
| Heartbeat | 初值同步→有效边沿/应答→原3秒到期锁定 | 新值迟到先续期；成功读旧值续命 |
| 任意等待 | Cancel/Expired/EpochChanged/UnknownHeld→保留事实与限制 | 自动当空闲、重起截止、盲重发或派发后继 |

## D06 最小共享语义输出

**Domain端口形状保持**。现DeviceObservation.Identity/Reliability承载基础状态；PositionObservation.Identity/Reliability已经存在，013使用独立真实位置身份。AxisPositionSet标量必须来自同一次完整位置观察，不能逐轴更新后借一个时间对外宣称新鲜。Connection按真实连接/心跳给出，普通Position.Stale不伪装断线。

现Host DevicePositionApi只有标量和ObservationId，无法表达其年龄是否与顶层一致；拟最小增加：

| 字段 | 语义 |
| --- | --- |
| SampleStartedUtc | 该完整位置最早实际块读开始 |
| SampleEndedUtc | 该完整位置最后实际块读结束；非原子承诺 |
| ConnectionEpoch | 该位置所属代次 |
| Reliability | 该位置整体可用性，不能继承最新base |

保留ObservationId/已有坐标字段；live DeviceObservationApi由device-semantics/1.1升至1.2以声明独立位置语义。Domain原SchemaVersion及持久plc-evidence/1不联动升级。字段是观察事实，无地址、功能码、读计划、分组名或轮询参数。

AxisObservationProjectionBuilder.From(DeviceObservation)改用Position.Identity/Reliability生成现有各轴行的ObservedAt、Reliability、ConnectionEpoch、EvidenceRef；其输出字段不增加。普通轴行时间来自实际位置结束，可靠性由保守最旧块判断。现前端读取轴行时间/可靠性，无需页面改动；2秒API查询仅读后端内存，不触发I/O。

| 当前消费者 | 采用事实/影响 |
| --- | --- |
| MotionAdmission、StartPreparationStep、IndependentRecipeApplication、PhysicalFaultPolicy | 基础安全/可用状态；不因新心跳刷新旧base |
| FixedMoveStep、FixedMoveRecoveryInteraction、RecipeDetectionExecutor、捕获用途位置判定 | 独立Position；必要因果读在通信层完成；完成前发布新完整位置 |
| AxisObservationProjectionBuilder、DeviceSemanticProjection | 修正时间来源/添加最小位置元数据；先同步共享合同 |
| RuntimeObservationProjection及历史API | 沿用已保存事实和原版本，不用在线新值回填旧记录 |
| 012真实保存/F匹配/运行冻结、配方API | 沿用，无形状/行为变化，不机械重写消费者 |

## D07 Evidence与BoundedMetrics

DeviceActionEvidence复用已有Observations集合，引用真正参与动作判断的反馈/位置身份；可包含多个不同时间窗口，不能伪称共同瞬间。原始交换引用、动作/运行/代次、段链和真实提交回执保持。旧历史schema读法不变，无迁移。

指标维度限连接角色、固定采集用途、按需原因和有限动作种类；计数、sum/max及固定直方图有界。动作明细利用既有日志/证据归因，内存不按无限RunId增长。统计实际PDU/字节、计划准备、重复等待唤醒、解码/复制处理、证据条数/字节及时间段。常规窗口结束或既有诊断快照时序列化一次；失败保留现有必要窗口，不每次采样追加新性能日志。

每项计量可追溯原始交换/实际持久提交，不能以“调用保存API次数”代替提交。指标不改变线程池、GC、优先级、模拟时序或任何期限。
