# 009 技术研究与决策

**研究沿用说明（2026-10-02）**：本轮不重新研究。原R/D条目保留为当时事实/决定；D12裁决本次正式专项范围及完成标准。预算/U态等已接受业务规则不撤回，完整交付/验收转出，不冒称此前结果已完成。

日期：2026-10-01。需求基线：[spec.md](spec.md)，SHA256 `11830a2337a1442dc64253beeceebc06c365fd8b811df81bb7385b353bf9cf34`，39项FR、28项AC、12项SC。本轮仅同步已接受的DESIGN-OPEN-01澄清，不重新决定期限。本文是只读源码研究和目标设计，不是实施或运行报告。研究代理只读调查，最终文档由主代理汇总；未运行构建、测试、设备或数据库操作。

## 来源与适用性

1. [AGENTS.md](../../AGENTS.md)、[宪章7.0.0](../../.specify/memory/constitution.md)及本次用户范围决定优先。
2. [有效需求](../../软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.md)§7、§11.3、§11.13与[008业务决定](../008-recipe-driven-inspection/business-decisions-20260926.md)约束工艺、完整新轮、E扫码、人工面来源及四面3＋1。
3. 唯一现行协议为[2026-09-25分区Word](../../高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx)，SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`；[使用说明](../../高德_文档/通信协议使用说明.md)确认其身份。工程 `HexOneBased` 和当前Float32配置不等于PLC厂商现场确认。
4. [001设备合同](../001-station01-public-preparation/contracts/device.md)、[003合同索引](../003-plc-latest-protocol/contracts.md)、[阶段动作](../003-plc-latest-protocol/contracts/plc-stage-action-port.md)、[检测](../003-plc-latest-protocol/contracts/detection-port.md)、[整盘](../003-plc-latest-protocol/contracts/whole-tray-workflow.md)、[恢复](../003-plc-latest-protocol/contracts/recovery-test-execution.md)、[008执行](../008-recipe-driven-inspection/contracts/execution.md)与[证据](../008-recipe-driven-inspection/contracts/evidence.md)采用当前有效增量，带日期的实施状态只说明当时状态。
5. 2026-09-29 MW1000/MW3000、BOOL按字分组讨论稿不作依据；历史运行包和旧任务勾选不作本轮通过证据。源码说明当前实现，不反向确认物理含义。

## 当前正式链路及证据索引

以下行号是本次读取时定位；均相对项目根。详细生产者/消费者见[影响矩阵](impact-matrix.md)。

| ID | 当前证据 | 结论 |
| --- | --- | --- |
| R01 | `backend/src/Gaode.Host/Composition/Station01Registration.cs:26–117`；`Composition/AdapterBindings.cs:39–69` | VirtualPlcIntegration且文件相机/Worker配置齐备才接完整Detection；PLC各端口绑定同一LatestProtocolPlcDevice；阶段适配器复用此设备及RecordPickedAsync回调。Production采集/算法仍明确NotIntegrated，不能把本设计说成生产接入完成。 |
| R02 | `backend/src/Gaode.Application/Motion/MotionCoordinator.cs:8–121`、`MotionAdmission.cs`；`Station01/StartPublicPreparation.cs` | Application拥有运动准入/租约和业务意图；当前准入还解释夹紧、检测及复位原始状态。 |
| R03 | `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs:117–156,245–308,464–619,621–959` | 正式初始化、心跳、轮询、运动、绑定、复位和翻面都在此；业务与心跳有各自受控连接，不应新开阶段客户端。 |
| R04 | `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs:161–324` | 取料观察→清命令→保存回调→关联复核→放料字段/命令→放料反馈及ACK；目标到位采样与安全抬升后的完成位置分开。 |
| R05 | `backend/src/Gaode.Application/Ports/DeviceMessages.cs:21–27`、`StagePortContracts.cs:154–243`、`IInspectionHandshakePort.cs`、`Domain/Station01/RunSnapshot.cs` | 原始状态、位、ProtocolStatus、协议常量、握手端口注释和Domain原始报警均泄漏；重命名而不改控制依据不能解决。 |
| R06 | `backend/src/Gaode.Application/Workflow/SortingTargetAllocator.cs:60–138` | 有真实StageEventStore提交门禁；但payload写死protocolStatus=2，Event helper写死Virtual/Derived。不能把这些字段搬进新原始证据表冒充实收报文。 |
| R07 | `backend/src/Gaode.Host/Api/QueryEndpoints.cs:239–255`；`CommittedResultProjection.cs:140–149` | Host自行解报警位，历史分拣投影按原始2/3及ACK判定；属于必须同步的公开查询/历史读取边界。 |
| R08 | `backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs:181–182,230–240`；`Infrastructure/Diagnostics/RuntimeDiagnosticLogging.cs:44`；`scripts/start-station01-virtual-loop.ps1:192,216` | Exchanges只有256项内存窗口；ILogger经启动脚本重定向时有持久日志，但无完整按证据引用解引用的原始报文保存能力。不能把已有日志全部说成无持久化，也不能把modbus://字符串当保存回执。 |
| R09 | `VirtualPlc/PlcAddressMap.cs:16–138`、`PlcDataStore.cs:38–39,416–438`、`VirtualPlcEngine.cs:492–675,807–898` | 点表、Float32编解码与Host重复。设备状态机独立执行，必须保留；当前线圈64、寄存器128容量足以做有限非连续地址演练。 |
| R10 | `backend/tests/Gaode.Contracts.Tests/Devices/LatestPlcProtocolTests.cs:12–24,31–79` | 已有123.456的独立已知字42F6/E979及四种排列、文字点位和方向检查。保留并扩至所有在用信号，不能误称现状完全没有独立oracle。 |
| R11 | `backend/tests/Gaode.Rules.Tests/Architecture/DependencyRulesTests.cs:9–31`；`Gaode.Contracts.Tests.csproj` | 现有规则只守引用方向/Domain框架；Contracts项目同时引用App/Infrastructure/VirtualPlc，不能按项目名推定都是业务测试。 |
| R12 | `scripts/workflow/runner.py:24,103–115,182–191,289–342`；`scripts/verify.ps1` | 已拒绝零测试、未执行/跳过及TRX失败，且执行restore/build/三套测试；缺必需测试ID集合，删除某门禁仍可能剩余全绿；新通信套件不会自动加入SUITES；源码摘要只覆盖backend/global.json。未发现`.github`云CI，现有是本地workflow。 |
| R13 | `backend/tests/Gaode.Integration.Tests/Support/VirtualLoopTestRig.cs:68–100`、`Station01HostFixture.cs:108` | 现有集成夹具使用同进程WebApplicationFactory和VirtualPlc服务器，虽真实TCP并有独立Worker，不能代替独立Host/PLC进程验收。 |
| R14 | `scripts/start-station01-virtual-loop.ps1:192–259`；`verify-008-backend-route.ps1:1–75` | 启动脚本能启动独立Host/PLC并查Worker归属；backend-route只接受Q04–Q22且仅记录finalOutcome，退出0不足以证明009成功；详见quickstart。 |
| R15 | `backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs:33–35,117`；`StageEventStore.cs:33–34,91–92`；`backend/tools/Gaode.StorePrep/Program.cs:5–24` | TraceWriter有单读者队列，但全库不是单写队列；StageEventStore自行短事务提交。StoreAccessGuard是进程级目录锁。StorePrep目前只准备空根，不能宣称已有原地升级命令。 |
| R16 | `backend/src/Gaode.Application/Workflow/FlipFeedbackCorrelation.cs`及其测试；`LatestProtocolPlcDevice.cs:734` | Application翻面辅助类虽泄漏原始码，目前正式ExecuteFlipAsync不调用它；迁移其有效防旧反馈义务，不能虚构正式调用关系。 |
| R17 | `backend/src/Gaode.Application/Station01/Steps/ThreeDStep.cs:56–62`、`FScanStep.cs:60–66`；`StartClampStep.cs`及正式设备启动实现 | 公共3D在无取消/安全故障时，即使采集或保存失败也尝试周期清理；F另有captureEndedAndRecorded门禁。启动Accepted是完整真实写序列提交确认，随后另等物理夹紧，不能发明新的启动受理信号。 |
| R18 | `StartClampStep.cs:116–130`；`LatestProtocolPlcDevice.cs:650–686,894–919`；`StartPublicPreparation.cs:304–342`；`Host/Api/RecipeEndpoints.cs:38–82` | 区域A在夹紧后/3D前用运行配置容量，B在F后用计划可空容量；独立API有运行容量回退，正式连续链没有。A有PlcAcceptance总窗口；当前B源码无有限总期限，且Bound投影先于handoff保存。本次spec已批准新增有限等待，按D11/B03.2修复，不声称源码已有要求。 |
| R19 | `SortingTargetAllocator.cs:120–135`；`StageEventStore.cs:34,72–101`；`TraceWriter.cs:95–103,241–244` | WaitAsync超时已记录CommitUnknown，不保证回滚；StageEvents/幂等/投影同事务，实际Commit后才回执；TraceWriter有commit后回执前hook，但StageEventStore及拟增rawjob尚无此故障缝。 |
| R20 | `SortingTargetAllocator.cs:46–54`；`ThreeStageWorkflowExecutor.cs:492–499,510–538`；`backend/src/Gaode.Application/Workflow/StageEventing.cs:176–204`；`StageEventStore.cs:141–166` | 预留、意图/Started先存；普通Failed/TimedOut/Disconnected清Held，取料证据失败必须UnknownHeld；重启不能只读Held投影，现RecoverAsync会写事件，不是只读核查。 |
| R21 | `frontend/package.json:16`、`package-lock.json:2076`与本地typescript/package.json:5；`scripts/verify-station01-page-diagnostics.cjs:41–44,66`；`validate-008-operation-evidence.py:83–110,140–150,219` | TypeScript5.9.2已锁定且存在；CJS含报警位/地址、Python含取放2/3/0、轴命令及内部阶段；可复用TS AST/Python ast。其他混合消费者见I22，PS调用链见verify-q01-q02-test-page:320–325。 |
| R22 | `QueryEndpoints.cs:37–51,81–86,245–290`；`Station01NotificationService.cs:60–62`；`RunSnapshot.cs:77–84`；`frontend/src/state/notification-reducer.ts:1` | status字段和旧payload大小写须逐项映射；历史run默认无startup诊断，旧Audit不完整；通知实际传完整snapshot，客户端summary:string不符。I§5形成明确替换，不保留raw影子。 |
| R23 | `backend/tools/Gaode.StorePrep/Program.cs:5–30`；`Station01EntityConfigurations.cs:118`；`StoreAccessGuard.cs:10–23`；`StoreCompatibilityProbe.cs:23–39`；`Station01Registration.cs:144–160` | manifest在Manifests表；当前新空根准备的迁移/manifest分两提交；Probe仅表名/迁移计数不足。目标单事务与U0/U1/U2/UX见模型§6.1，不假设外部manifest。 |
| R24 | `StartPublicPreparation.cs:145–175,291–342`；`LatestProtocolPlcDevice.cs:26–31,322–333,612–613,894–919`；`RunExecution.cs:108–110,171–173` | 严格后段期限在绑定前冻结，旧Detection起点在handoff后；Binding无deadline，AdvanceBinding使用Pump生命周期token且连续写间无请求复核；保存仅等CriticalSave。设计需共享业务总窗口、关闭后台后继准入和限制分项保存剩余时间，不能仅加调用侧WaitAsync。 |
| R25 | `BusinessBudget.cs:3–6`；`PublicConfigurationValidator.cs:21,52–64`；`ConfigurationLoader.cs:19,34–50`；`ConfigurationFreezer.cs:9–27`；001 `contracts/budget.schema.json`；007 `examples/budget.virtual-loop.json` | 当前budget schema1.0且businessMs禁止未知字段，无recipeApplication；已具备完整JSON冻结/摘要及版本冲突拒绝。选择新增必需字段及schema1.1，需模型/schema/校验/新版本实例和所有引用成套对齐。Test10000来自spec本次批准，现CriticalSave2000仅选择背景，非生产或耗时保证。 |
| R26 | `VirtualPlcLatestProtocolTests.cs:376–442`；`OperationIngressTests.cs:11–14`；`DeadlineSchedulerTests.cs:11,44,57`；`TraceStoreTests.cs:99–127`；`VirtualPlc/SimulationModels.cs:36–52`、`Program.cs:91–100`；`scripts/summarize-q01-q02-evidence.py:42–46` | 组件5秒仅看门狗；精确时间边界及真实SQLite commit后扣回执能力可复用，但当前未验证绑定总窗。现故障枚举无“通信健康但绑定不完成”，汇总脚本未核对总期限/后台取消；V-BIND及相关注入/编排均待实现。 |
| R27 | `Application/Station01/PublicPreparationHandoffV2.cs:67–70,90–92`；`Host/Api/QueryEndpoints.cs:155–157,173–174` | 当前后段验证及查询可以仅凭已存handoff推定Bound/Ready；原009要求承接RecipeApplicationReceipt资格；011按共同合同RC05.1迁移为RecipeBindingReceipt，迟到实存不能再授权。意图/不可变handoff无法预知自身未来回执时间，观察另有限追加，不能从行存在循环自证。 |

正式链为：Host `/api/v1/station01/runs`持久受理 → Station01Coordinator/StartPublicPreparation → MotionCoordinator及Application语义端口 → LatestProtocolPlcDevice → 现有ModbusTcpClient → PLC或独立VirtualPlc。公共准备之后经持久handoff进入WholeTray/ThreeStage → IDetectionPort（正式配置为IntegratedDetectionPort，虽位于Simulation目录，实际接Motion、文件采集和Worker）及IPlcStageActionPort → 同一PLC设备/阶段适配器。StageTransport共享连接与在途准入。FullSimulation、NotIntegrated及旧PlcAdapter不是009隔离验收的正式替代入口。

## 信号集中管理缺口

| 路径 | 当前定位（LatestProtocolPlcDevice.cs，另注除外） | 要消除的具体依赖 |
| --- | --- | --- |
| 初始/恢复 | 117–121、190 | 从0读0x22线圈/0x55寄存器、复位读0/9；本地地址索引和内部检查key穿出业务端口 |
| 心跳 | 486；VirtualPlc/ModbusTcpServer.cs:134–136 | 固定offset0读及心跳诊断分类 |
| 持续轮询/安全 | 577–600 | 0/16、0/0x53、AlarmBits起点读2；c[4/3/15]、r[0x12]和Bit4特殊解释 |
| 启动/夹紧 | 625起；Application/Station01/Steps/StartClampStep.cs | c[2]、r[0/1/0x12]；业务比较夹紧1/2 |
| 区域/配方 | 665、902–918 | NG/Pending连续数组、0x26/0x24/0x27/0x3F；HMI配方号及容量在业务端为ushort |
| 检测/扫码/复位 | 264–273；StageActionAdapter:189 | 0/0x13、r[1]/r[0x12]、Pos(0xD/F/11)；Inspection起点读2假定Reset紧邻 |
| XYZ各动作 | 845–860 | offset2连续写XY、Z的8/6偏移、命令offset0；覆盖3D、F/E、每个拍照位 |
| 自动翻面 | 734–805 | 状态起点读2假定实际面紧邻，内部数字/ACK处理 |
| 取放/下料/解锁 | StageActionAdapter及Application StagePortContracts、SortingTargetAllocator、WholeTrayWorkflowOrchestrator | 部分已用Map，仍有Application协议常量/结果码、位置证据原始状态和固定邻接 |
| 人工/特殊Test | LatestProtocolPlcDevice人工/复位路径及IntegratedDetectionPort特殊动作 | 保留已确认人工占用/清零和独立Test HTTP动作，不把它们猜成新生产寄存器 |

Modbus传输自身的MBAP/PDU固定字节位置是合法通信编解码，区别于业务信号固定数组布局。Retry/Teach等表内信号在当前正式主流程无新增消费者，不为“覆盖点表”扩建工艺功能；保留现有通信定义/方向测试，启用新业务须另有需求。

## 技术决策

### D01：直接改造现有正式装配

- **决定**：保留MotionCoordinator/ResourceLease、LatestProtocolPlcDevice连接所有权及LatestProtocolStageActionAdapter接入点；移出业务原始判断，设备实现完整负责协议解释。IntegratedDetectionPort消费语义动作/采集窗口，去掉具体PLC类型和协议版本字符串依赖。
- **理由**：R01–R04证明正式路径已经具备必要资源与保存接线；旁置包装器容易被绕过。
- **备选**：新建平行设备服务/第二TCP客户端、仅增加DTO包装，均不能保证唯一运动所有权及所有入口覆盖，未采用。

### D02：一个小型纯协议程序集

- **决定**：规划`backend/src/Gaode.Plc.Protocol/`，只放当前协议信号描述、地址换算、数据编码、命令/反馈值和原始报警表；无Application/Domain依赖、无网络/数据库/业务状态机。Infrastructure通信实现与VirtualPlc共同引用；通信测试可引用，业务程序集及业务测试不得引用。纯协议模块也不得引用Host共享业务逻辑。
- **理由**：R09有真实重复且两进程均需相同线缆定义。每项描述包含身份、方向、区域、类型/宽度、字序、值表/位表、读写与清零责任、来源/解释版本。读组按描述计算连续合法区间和索引，跨空洞拆分；非连续字段不再靠数组相邻解释。批次记录每次读时刻，跨epoch或超过原新鲜度不能拼成可靠观察。
- **备选**：继续两份生产点表、把点表放Application、通用协议引擎/运行期热切换均不采用。维护一个编译期现行配置；Test变体使用隔离构建，不引入产品选择协议功能。

### D03：语义端口和真实物理槽身份

- **决定**：采用[业务设备合同](contracts/business-device.md)和[data-model](data-model.md)定义的状态/结果；删除业务ProtocolStatus及地址常量。采集窗口开放/业务采集保存结束后释放窗口，是业务可见生命周期；其中命令、检查和复位握手完全内部化。业务数字只按业务含义保留：物理槽号、容量、坐标、面、期限、HMI显示编号等，不用ushort反映寄存器宽度。原配方载荷的槽字段由配置读取边界解释为PhysicalSlotIndex，不更改已冻结配方。
- **理由**：仅Clamped布尔和raw字段并存仍允许绕行；公开语义必须有具体消费者且不与协议枚举数值相等绑定。Recipe_ID继续只用于显示，不能决定计划或质量。
- **行为差异**：R17的启动提交与夹紧等待保留各自期限；正常采集会话保持保存后结束。公共3D另有受控失败清理语义，只退出当前周期、不改原失败或放行后继；F/E/Detection不自动继承该策略。
- **R01修订**：依据R18分开启动区域准备A与F后配方应用B（BD03.1/PC04）。不采用“都等F后”的旧I05表述，不把A成功当配方唯一绑定。两个实际入口的容量缺省处理分别保留。B新增有限总期限的批准依据现为spec Clarifications及FR-035—039，按D11同步；历史003 spec:150–153非规范性解释、组件看门狗或旧报告仍不作为批准依据。
- **备选**：把1/2/3换同值业务枚举、对所有整数/数组禁用，分别会残留泄漏或破坏合法业务，均不采用。

### D04：取料证据→保存回执→放料

- **决定**：保留单次Sorting调用，注入语义`PickCompletionEvidence → PickCommitReceipt`回调。通信确认当前取料与源点、完成必要内部处理并持久化必要原始证据后调用业务；SortingTargetAllocator查原预留、真实短事务提交InTransit及源目标/来源/引用，返回已提交回执。通信复核回执身份、epoch、安全和原deadline后才写任何放料槽/目标/命令。
- **理由**：R04/R06已有关键保存门禁，必须强化可查回执而不改变保存节点。取放到位采样可以早于抬升后完成，不能要求完成瞬间Z仍等于目标。
- **备选**：通信直接写业务库、裸bool批准、入队即批准、先放后补存均拒绝。保存未知/超期即UnknownHeld；不通过自动重放消除不确定性。
- **R02修订**：实际物理观察、ActualCommit、ReceiptValidity和AllowPlace分开（模型§4.1 A—D）。提交前回滚确认与提交后回执失效分别验证；R19证明调用超时不是回滚。只读核查查到提交不恢复超期旧动作；拒绝用fake callback或单一提交前故障覆盖全部F05/F06。

### D05：最小持久通信证据与历史读取

- **决定**：新增通信证据表及Infrastructure专用写/读接口；复用TraceWriter实例的内部有限队列能力增加通信job，不将raw装入Application WriteBatch。原StageEventStore短事务与所有权不改；不声称全库串行。必要证据事务提交后才产生可解引用的opaque reference，业务不持有raw查询接口。Host只路由受权诊断查询，不解报警位。细节见[诊断合同](contracts/diagnostics-history.md)。
- **理由**：R08缺持久引用，FR-020与失败验证无法靠内存满足。关键通信证据保存失败不能发布可靠完成；心跳/停机通道不等待磁盘。动作持有及关闭准入与业务保存失败一致，失败事实不得编造引用。
- **备选**：全量永久抓包、集中日志平台、只重定向控制台、把现有modbus://串当证据均不采用。
- **历史决定**：新语义payload版本化。旧JSON和日志原样保留，有限历史读取器置于Infrastructure，仅按已存形状读取旧事实；未记录版本保持未知，合成protocolStatus不是ActualWire。无原包保持RawUnavailable。此读取不授权当前动作，不支持旧线缆协议在线运行，不新建兼容层。
- **R04/R05修订**：I§5规定S/R/E/N逐字段与空值/来源/历史映射；通知固定业务摘要，避免完整snapshot再次泄漏。raw保存未确认但取料已观察时走BD05.1独立失败通知，业务能写则真实保存UnknownHeld最小事件，不能清Held或假InTransit；全存储不可用只尽力日志，重启依已存预留/意图保守占用（R20/R22），不承诺新事实必持久。
- **R07修订**：本次升级只增证据结构，用单SQLite显式事务包含DDL、EF历史记录和同库Manifests更新，完整核验后开放；中断按模型§6.1 U0/U1/U2/UX分类。复用EF生成本次UpOperations，拒TransactionSuppressed/旧表重建；不沿用当前MigrateAsync与manifest两提交，不新建通用迁移框架。
- **技术依据**：现有EF Core SQLite10.0.12及R23源码；官方[SQLite事务](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)、[BackupDatabase](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup)、[IMigrationsSqlGenerator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.imigrationssqlgenerator.generate?view=efcore-10.0)、[TransactionSuppressed](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.migrationcommand.transactionsuppressed?view=efcore-10.0)支持该有限设计。不是本轮实际数据库事务或迁移结果。

### D06：测试按保护义务拆分

- **决定**：保留Rules及Contracts中的业务断言文件；建立`Gaode.Communication.Tests`承接原始协议/TCP/握手断言。Contracts中未受影响的基础设施契约测试可留原项目，逐文件明确归属，不为项目名纯化搬迁无关测试。Integration保留正式装配、真实保存和Worker验证；业务断言文件与通信探针显式分开。详见[测试迁移矩阵](test-migration-matrix.md)。
- **理由**：R10–R13证明现有测试兼有有效覆盖、混合边界和历史Skip。分类不是删除。既有已知字向量是独立oracle起点，必须扩展而非全盘重写。
- **备选**：保留旧Application协议常量让测试不变、整套Contracts删除、放宽TRX或继续Skip失效案例均不采用。

### D07：有限边界检查与必需执行清单

- **决定**：在Rules测试中用同一有限C#语法/符号检查器扫描正式受保护文件和内存负/正例。采用固定SDK 10.0.401内的Roslyn程序集作为测试引用；只读检查本机两项CodeAnalysis程序集版本均5.9.0.0，实施时显式引用/复制运行依赖，缺失即失败，不动态下载最新版。仓库当前尚无此测试依赖。只做本仓库的依赖、公共端口形状、协议来源传播、强转/位运算/布局、诊断控制与业务测试引用规则；不建设通用分析器平台。
- **理由**：普通字符串扫描不能检测别名、强转和改名raw。整数本身不代表协议；必须结合受保护端口形状/来源及实际控制使用。解析失败、未枚举文件、未分类公共字段也不能当通过。规则边界和人工语义抽查共同覆盖，不能宣称静态工具识别任意自然语言业务意图。
- **接线**：扩现有runner的SUITES、源码摘要和固定必需测试ID清单，逐项核对发现与TRX完成状态/源码摘要/本轮run；已有零发现/跳过拒绝保留。缺门禁文件、未发现、过滤、Skip、陈旧报告均不通过。无云CI接入事实，不新造平台。
- **R03选定方案**：非C#门禁按VG V02.1使用已锁定TypeScript5.9.2的JS/TS AST、Python标准库ast和PS自带解析器；完整文件解析后检查业务断言/条件/汇总的数据依赖闭包，有限别名/直接helper传播，不支持的设备对象逃逸直接失败。A08内容、A09传播、A10文件分类；N11—N17含当前CJS报警位和Python2/3/0，P07—P10保护合法语义/通信断言。混合脚本拆到精确通信probe，原业务断言继续保护；verify→runner强制执行SCRIPT-SOURCE-JS/PY/PS和INVENTORY，逐case合并账本。均待实现，仓内有解析依赖不表示已有检查器。
- **未采用方案**：另加Acorn等解析依赖无必要；只用hash/关键词/人工审阅不够；建设完整跨语言分析平台超范围。有限受支持语法与schema注册表可覆盖本仓库已识别形式，解析失败或未知来源不得“跳过后通过”。
- **备选**：只检查csproj、只禁止十六进制/数字、增加白名单掩盖违规、另建验证调度系统均不采用。

### D08：四个独立协议变体及一个内部处理演练

- **决定**：一次性重构与基线验证完成后，冻结业务/契约/测试/配方和非通信编排；分别从该基线创建隔离Test构建，演练非连续地址、位号、命令反馈编码、字序。每个变体使用独立Host/VirtualPlc TCP、相同语义断言、独立手工协议预期和零冻结文件差异。AC-005另外增加“ACK清零后读回核验”内部步骤的Test演练，复用现有寄存器，不创造PLC物理信号或新完成含义；无该读回不得完成，期限不延长。
- **理由**：单端假传输及共享映射互证不能证明隔离。加入两端共享同一错误映射的负控制，独立oracle必须报差异。详见[验证合同](contracts/verification-gates.md)。
- **备选**：一场同时改四项、改业务夹具配合变体、产品多协议模式、虚构生产新握手均不采用。

### D09：代表主流程及进程真实性

- **决定**：保留现有一/二面要求，四面只选四面3CD＋1AB与新增配置代表；共享初始化/3D/F/下料/解锁证据可复用，成组成员、整体、E、人工和特殊旋转的实际差异用必要代表补齐。不是Q01–Q22、14或8排列穷举。独立进程以现有启动脚本为基础；009完成判据与证据收集入口待实现，不把当前backend-route退出0当通过。
- **理由**：P13与R13/R14区分配方表达、同进程测试、实际独立进程验收。相机实际读文件、Worker实际处理媒体、SQLite真实提交和API最终事实必须同run可查。
- **备选**：全部故障×配方组合、仅内存模拟、用历史报告替本轮证据，均不采用。

### D10：范围、冲突与已解决设计问题

- **决定**：不改变工艺/配方、既有已确认业务期限/保存节点/安全处置；唯一新增期限是spec已明确批准的配方应用有限总窗（D11），在初次冻结前补齐。当前报警安全准入比Word§2.7“警告不影响生产”更保守：Pump要求零报警位，人工等待单独处理。009保持现行可观察准入，语义Severity=Warning不自动授权动作；若要求放宽，列安全业务契约变更，不能悄悄做成位映射修复。
- **其他裁决**：Word§4旧重试/跳过不覆盖USR-D完整新轮；E复用F时序来自USR决定而非Word原文；抓取安全抬升不等于目标Z失配；人工采用命令目标面必须标人工来源；槽号不是计划Sequence；特殊旋转只有既有Test合同，不新增生产信号。
- **OPEN-009-05**：已以R12解决设计接线位置；必需ID验证尚待实现，当前不能宣称门禁通过。
- **OPEN-009-06**：当前仓库消费者已在影响矩阵盘点；外部未登记客户端是否存在无法由仓库证明，发布接口前需盘点接入方，不阻塞内部任务拆解。
- **保留局部OPEN**：009-01地址基准/站号/功能码/字序；009-02物理轴/单位/标定与取放目标可观察窗口；009-03未来生产握手；009-04生产报警新语义。它们限制对应真机接入，当前定义明确的Test设计不依赖臆测。

### D11：落实已接受的配方应用总期限，不重决策略

- **决定来源**：spec Clarifications的三项用户决定及FR-035—039、AC-022—028、SC-011/012；宪章P04说明必须有限，R18/R24只证明现状缺口。初值10000ms是本次新批准Test值，不是历史5秒看门狗、HTTP取消或单次1秒I/O的推导。Production数值保留OPEN-009-07局部拒绝。
- **配置选择**：在现有版本化BusinessBudget新增必需`businessMs.recipeApplication`，budget schema升级1.1，CLR为BusinessDurations.RecipeApplication。新预算实例版本及引用须成套更新；不原版本改内容，不为旧配置补10000。复用现完整JSON/digest/快照机制；预算ID/version/purpose/source/digest/snapshot是该独立业务预算的来源，无需另建配置平台。所有来源/数值非法在业务准入拒绝；已冻结运行不因继续/重连/重试换值。
- **原009职责/端口决定（旧协议历史，011软件绑定见RC05.1）**：Application负责三入口共同的预算获取、当前意图、唯一t0/D/T、必要业务保存和最终授权；独立API从直调PLC改为调用同一业务协调能力，保留其容量回退及已有handoff、不额外启动产品动作。IPlcRecipePort接收当前关联、语义绝对期限和取消资格，通信内部处理现行握手及raw持久化，返回DeviceApplied中间证据。最终Bound/返回成功须等本次所有必要保存的及时有效回执，修正R18所见handoff前过早Bound投影。
- **消费者与非循环证据**：按R27同时修改后段HandoffV2与Query/通知GET投影，handoff行本身不恢复当前Bound/Ready；核验当时按期完成的不可变Receipt及当前资格，不以稍晚now≥T倒判有效结果。原意图先存预算/关联，t0后记；原事务不能预填自身未来commit回执时刻，由Application经既有Audit另有限追加语义ReceiptObserved。该观察仅用于诊断/历史，不成为下一层审批，不递归证明自身回执；缺记录即证据不足，不反推或回写旧payload。
- **原009计时/保存范围（011沿用总窗与业务保存，退出旧设备绑定前置）**：意图提交并有效回执后、排队/端口前起t0；D=t0+冻结预算，T取D及已有适用更早后段截止。排队、设备应用、必要raw、RecipePlanBound和适用handoff共用[t0,T)；各保存另取CriticalSave上限。意图本身仍有限。严格链原绑定前冻结不变，旧链保留handoff后首个Detection起点；API无现存后段期限则不编造。已完成公共3D/F不纳入本次窗口，不改既有RecipeExecutionBudget公式或补偿后段预算。
- **取消/迟到**：同一请求的关闭资格与每次写派发做有限原子仲裁，所有后继await返回/写前/发布前复核；实际访问使用请求级取消与期限，不只使用Pump lifetime。旧资格关闭后新增写和成功授权均为0。已发I/O/已开始提交真实留存，R02的ActualCommit与ReceiptValidity区分原样适用；失败/迟到事实沿原有界保存，不刷新动作预算，心跳/观察/停止独立保持。
- **原009验证选择（历史，不是011全量义务）**：V-BIND/BA01—BA07接同一必需执行账本；两容量健康卡住用独立VirtualPlc、真实TCP且I/O及时成功/心跳持续，不能用断线或F复位失败替代。精确边界复用001时间测试方法；绑定及handoff的commit后扣回执复用R26真实SQLite事务缝，按BindingId/WriteId精确定位。新增注入能力待实现，旧脚本当前不支持。初次完成上述基线后冻结预算值、版本、起终点和业务断言；M01—M05不得改动。
- **未采用方案**：复用PlcAcceptance、把后段起点移到绑定后、为每个保存另开完整10秒、只超时调用方、用协议报文/阶段数量算预算、默认回填旧配置均与已接受决定冲突；不是待用户重新选择的备选。
- **实施前置**：影响矩阵AL01—AL08明确001配置/预算schema、002配方、003移交/API、007版本实例/fixture/启动引用、008 E06/证据实际对齐责任与完成证据。外部合同尚未修改，相关代码仍被这些前置阻断。

DESIGN-OPEN-01在本轮设计层同步关闭；R02—R07决定保留，CHK011/014/038复核见[review-remediation §7](review-remediation.md#7-本轮已接受期限澄清的设计同步)。可进入任务拆解以安排明确前置，不能视作跨功能对齐、实现或验证已完成。OPEN-009-07与原物理OPEN只限制对应生产接入；本轮未运行验证。


## 实施补记：T020 的崩溃隔离（2026-10-01）

本轮在既有 `.station01.store.lock` 中保留未结束维护的意图。该内容是独占维护的崩溃锁存，不另建 store manifest；StoreId/Profile/schema/准备身份的权威值始终来自同库 Manifests。Host 获取锁时拒绝非空意图；中断后的维护进程重新取锁，让 SQLite 自行恢复，再按完整结构、精确迁移记录、同库身份、已核验备份、旧业务值及媒体引用归类 U0/U2/UX。只有目标态全部核验成立才清除维护意图。U0 重做前再次核对，U2 不再执行 DDL，UX 只允许已有同 StoreId 可信备份的受控恢复。

实现为 StoreMaintenance、StoreSchemaInspection 与现有 StoreAccessGuard/StoreCompatibilityProbe；Host Probe 不执行迁移。必要新增表、迁移历史和 manifest 条件更新共用一个非 deferred SQLite 事务。原迁移 ID 保持原文；组件建立源态时使用 EF 自身名称解析规则，之后仍按三个确切原 ID 核验，未改写历史。

当前验证使用实际源码链接构建的独立 StorePrep 组件进程与受控 Test SQLite 副本，代码体与正式工具相同；不是完整 Host/Worker 验收。实际记录在 `artifacts/recipe-execution-008/009-isolation/implementation-20261001T094541Z/t020-store-maintenance/`，最终基线的同轮账本与正式产品构建仍由 T053/T060/T061完成。
## D12 前轮正式通信专项范围与独立结项（历史；当前最小范围见spec）

**决定**：用户2026-10-02明确将009变为独立功能，采用T061通信专项基线而非完整整机基线。D01—04/D06—08边界及独立oracle继续；D05完整历史升级、D09整机链、D11预算完整业务验收转出，直接raw/保存/稳定期限/取消保留。

**理由/证据**：正式Host装配Station01Registration/AdapterBindings生产同一LatestProtocol实例，取放通过LatestProtocolStageActionAdapter→业务CommitPick→StageEventStore；CommunicationEvidenceStore实际writer是可靠结果必要依赖；StoreCompatibilityProbe核结构/schema，受控新Test库足以验证，不必旧库升级。IntegratedDetection是直接语义消费者，其编译/内容边界必需，启动完整Worker/相机/页面并不是报文表示隔离的依赖。当前protocol_isolation.main仍无新专项模式且overall009Passed=False，T053/T054必须定向实现，不把旧子集报告换标签。

**不采用**：过滤原完整清单伪通过、冻结修复前无界/半接线状态、放宽10000ms、删除完整业务义务、另建010/重做研究、通用协议或迁移框架。

**待实施**：S00固定案例、专项入口/账本、专项基线、冻结及变体负控制。文档范围决定不代表运行通过。
