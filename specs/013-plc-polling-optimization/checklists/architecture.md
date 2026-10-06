# Architecture Checklist: 013 PLC轮询降频与通信负载优化

**Purpose**：任务拆解前的定向设计质量审查；评价规则是否完整、一致、可实施、可计量，不评价软件是否运行通过。  
**Created**：2026-10-04  
**Feature**：[spec.md](../spec.md)；[plan.md](../plan.md)  
**Depth / Audience / Timing**：标准深度；设计审查者；进入tasks之前。  
**Scope**：本次降频及直接影响，包括用户指定的八个重点；不扩展恢复、生产验收、全量测试或长期压力平台。

**Note**：由speckit-checklist生成；用户明确授权在Notes逐项辅助评价，未授权勾选。  
**Review Ownership**：自定义清单属于审查者；全部新条目保持[ ]。只有审查者确认需求质量满足后才可标[x]，该标记不代表实现完成或测试通过。  
**Marker Semantics**：下文“满足／部分满足／不满足”只评价文本质量；不改变复选框。speckit-implement只能读取清单状态，不得修改标记。requirements.md有独立生命周期，其CHK015保持NotRun。

CHK编号在各自文件内使用；本文件CHK015是预算设计条目，与requirements.md的功能结果CHK015不是同一项。

## Requirement Completeness：采集与边界完整性

- [ ] CHK001 已确认周期、44字段用途及B/P/F/U/T/局部快档的唯一持续来源，是否完整且无相互矛盾的归属说明？[Completeness, Spec FR-002—010, A01/A02/A06]
- [ ] CHK002 固定读计划的准入、合法连续范围、冻结映射绑定和复用失效条件，是否明确保留非法映射拒绝与非原子观察限制？[Completeness, Spec FR-011/012, A02, D01]
- [ ] CHK003 活动/空闲切换、未完握手及未知状态、按需读合并、多消费者等待和慢轮次丢弃，是否有明确规则而不留下重复轮询或旧闲计时等待？[Coverage, Spec FR-006—010/013/017, A03]
- [ ] CHK004 局部50ms例外是否有实际短态来源，并限定命令后首次读取、同批全部轴、退出200ms、原截止及漏观察处置？[Traceability, Spec FR-015/016, A06, R07]

## Requirement Clarity：时间与调度可实施性

- [ ] CHK005 周期、首发送迟延、连接排队、单PDU交换、块间等待和整组发布耗时是否分别定义，且A03与V06的上界计算一致？[Clarity, Conflict, Spec FR-022, A03, V06]
- [ ] CHK006 共用业务连接时的选择顺序、防饥饿及固定负载可行性条件是否足以支撑基础采集、首次中间态和关键读写的共同目标？[Completeness, Gap, Spec FR-007/013/022, A03/A06, V04/V06]
- [ ] CHK007 心跳首次会话、初值同步、独立3秒截止、两种应答延迟及每次等待/后继派发的原期限和取消资格，是否定义完整？[Coverage, Spec FR-002/017, A05]

## Requirement Consistency：观察、消费者与保存

- [ ] CHK008 各块时间/代次/可靠性、基础与位置独立年龄及普通位置陈旧与设备失联的区分，是否一致且不被新组观察续期？[Consistency, Spec FR-014, A04, D02/D03/D06]
- [ ] CHK009 当前动作反馈、到位后坐标、取放反馈后的坐标及完成前最终位置发布，是否与现共同业务消费者形成完整因果约束？[Completeness, Spec FR-015/018, A04, D05/D06]
- [ ] CHK010 Host位置元数据/API版本、Domain形状、直接消费者和合同同步责任是否明确，且业务端口不承担协议分组或轮询知识？[Consistency, Spec FR-021/028, D06, Plan SY-03/05/07]
- [ ] CHK011 必要原始证据、缺口/分段/历史读取、真实取料与有效在途提交门，以及SC-006有界直接计量要求，是否完整且互不削弱？[Completeness, Spec FR-018—020, SC-006, A08, D07, V01]

## Acceptance Criteria Quality：基线与计量适用性

- [ ] CHK012 改前主项目保留、两侧源码/构建/输入身份及中性计量补丁的记录要求，是否足以阻止旧副本覆盖或基线提前包含013优化？[Traceability, Spec FR-001/023, V02]
- [ ] CHK013 删除PlcPoll后的schema/配置迁移，是否与同条件比较要求一致，并明确两侧版本、批准差异和其他输入的语义等价？[Consistency, Conflict, Plan清理表, V02, Quickstart §2]
- [ ] CHK014 两侧共同资格、仅优化后要求、跨侧比较是否明确分开，25ms/75ms/4950ms的适用环境和超限分类是否不会误拒旧基线或强迫无关环境治理？[Clarity, Gap, Spec SC-001—007, V01/V03—06/V09, Quickstart §4]
- [ ] CHK015 周期预算、离散窗口边界、按需预算、共享扣重和失败请求归属是否能独立复算，且估算未被写成实测或正式PLC保证？[Measurability, Spec FR-023/024, V04/V05]
- [ ] CHK016 run-2的21轴批、33轴启动、108核心写及业务完成预期是否有可追溯来源，并明确只适用于该冻结路线/布局而不进入业务固定协议次数规则？[Traceability, Spec FR-021/025, V05, R10]

## Scenario Coverage / Dependencies：最小验证、接线与承接

- [ ] CHK017 各侧一次60秒空闲、一次相同代表链、直接保护组件、完整010持续L和受影响009检查是否覆盖必要路径且避免重复或扩大专项？[Coverage, Spec FR-025/026, V03/V07—09]
- [ ] CHK018 重复持续采集、旧缓存放行及必需项漏执行的负例要求，是否依赖实际交换/后继命令/独立预期和原报告，而非参数、源码搜索或自报汇总？[Measurability, Spec FR-026, V01/V05/V07/V09]
- [ ] CHK019 真实API观察器与原页面证据的职责、013 profile/manifest接线及未知profile拒绝要求，是否足以避免假ready和009结果冒充013？[Completeness, Spec FR-008/026, V08/V09, Quickstart §1—3]
- [ ] CHK020 SY-01—07及001预算schema依赖是否列明文档先于共享代码、清理对象和有效义务承接，并区分当前规则与历史事实？[Traceability, Spec FR-027/028, Plan SY/清理表]
- [ ] CHK021 正式PLC缺失输入、正常实施工作、运行NotRun与文档设计缺口是否可区分，且外部限制和CHK015未被错误设为本轮全局阻塞？[Clarity, Spec DEP-013-01—06, Plan OPEN, V10]

## Notes

### 审阅方法、依据与适用性

本轮新建本文件，未发现原architecture.md。显式目录为specs/013-plc-polling-optimization；已运行只读前提检查并核对FEATURE_DIR、AVAILABLE_DOCS及checklist-template。feature.json已是013，检查脚本因值相同未写入。前置hooks为空；本轮范围、深度、时点及评价授权已明确，未重复询问业务/频率。

依据索引（L表示本轮只读版本的行号）：
- **S**：[spec.md](../spec.md)；**P**：[plan.md](../plan.md)；**R**：[research.md](../research.md)；**D**：[data-model.md](../data-model.md)。
- **A**：[plc-acquisition.md](../contracts/plc-acquisition.md)；**V**：[verification.md](../contracts/verification.md)；**Q**：[quickstart.md](../quickstart.md)。
- **C1**：[Signals.cs](../../../backend/src/Gaode.Plc.Protocol/Signals.cs) L58—65、L140—190；[PlcSignalAccessor.cs](../../../backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs) L18—34、L59—79。
- **C2**：[IndependentAxisTests.cs](../../../backend/tests/Gaode.Communication.Tests/Devices/IndependentAxisTests.cs) L37、80、113；[ProtocolTcpFixture.cs](../../../backend/tests/Gaode.Communication.Tests/Devices/ProtocolTcpFixture.cs) L23—29；[run-2.json](../../011-plc-interaction-update/examples/joint/run-2.json) L29—31；[VirtualPlcEngine.Axes.cs](../../../VirtualPlc/VirtualPlcEngine.Axes.cs)、[VirtualPlcEngine.cs](../../../VirtualPlc/VirtualPlcEngine.cs) L126—129、L238—268。
- **C3**：[DeviceSemantics.cs](../../../backend/src/Gaode.Domain/Station01/DeviceSemantics.cs) L69—114；[DeviceSemanticProjection.cs](../../../backend/src/Gaode.Host/Api/DeviceSemanticProjection.cs) L7—14、26—38；[AxisObservationProjectionBuilder.cs](../../../backend/src/Gaode.Domain/Station01/AxisObservationProjectionBuilder.cs) L6—11；[runtime.js](../../../frontend/src/runtime.js) L362—363。
- **C4**：[RecipeDetectionExecutor.cs](../../../backend/src/Gaode.Application/Workflow/RecipeDetectionExecutor.cs) L587—595；[RuntimeObservationProjection.cs](../../../backend/src/Gaode.Application/Station01/RuntimeObservationProjection.cs) L47—69；[Transfer.cs](../../../backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.Transfer.cs) L17—74；[Recorder.cs](../../../backend/src/Gaode.Infrastructure/Devices/Plc/CommunicationEvidenceRecorder.cs) L51—76。
- **C5**：[run-2 config/budget.json](../../011-plc-interaction-update/examples/joint/config/budget.json) L2—4、18—26；[001 budget.schema.json](../../001-station01-public-preparation/contracts/budget.schema.json) L18—19、41—53、123；[ConfigurationLoader.cs](../../../backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs) L19；[BusinessBudget.cs](../../../backend/src/Gaode.Domain/Configuration/BusinessBudget.cs) L6。
- **C6**：[RecipeExecution010RunHarness.cs](../../../backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs) L73—102、220—244；[runner.py](../../../scripts/workflow/runner.py) L468—479；[verify_entry.py](../../../scripts/workflow/verify_entry.py) L36；[010-lightweight-cases.json](../../../scripts/workflow/010-lightweight-cases.json)。
- **K**：[009通信维护P03/P04](../../009-plc-protocol-isolation/contracts/protocol-maintenance.md)、[009业务设备](../../009-plc-protocol-isolation/contracts/business-device.md)、[009诊断E01/E02/E04](../../009-plc-protocol-isolation/contracts/diagnostics-history.md)、[010验证VG-01/03/04](../../010-recipe-execution-isolation/contracts/verification.md)、[011通信PC02—05及现行实施承接](../../011-plc-interaction-update/contracts/plc-communication.md)、[011执行状态](../../011-plc-interaction-update/contracts/execution-and-state.md)、[012共享集成](../../012-recipe-authoring/contracts/shared-integration.md)。

按完整条款判断，而不是把用户列举的疑点预先认定为缺陷。以下“满足”仅是本标准深度下文本足够，未运行验证不会使软件被判失败，也不会产生软件通过结论。

### 逐项辅助评价

| 条目 | 评价 | 准确依据与判断 | 实际影响 / 最小修订建议 |
| --- | --- | --- | --- |
| CHK001 | 满足 | A01 L7—38、A02 L44—50、A06 L125与S FR-002—010一致；C1覆盖44字段。B保留全轴准入反馈；动作共享；T承接完整位置并暂停P；F/U无动作不常驻 | 未发现设计要求重复持续源或漏字段。T附带CameraZ/ScanZ有已有消费者且不增PDU，不应误判为全局200ms普通监视；无需改分频参数 |
| CHK002 | 满足 | A02 L55—61、D01明确冻结副本、AdmissionId/映射摘要/字段/方向绑定、重连与定义变更区别；C1显示当前热路径确有重复ReadPlan/ValidatePlan | 合法/非法义务有承接，保留空洞和非原子限制；属于后续实现，不需增加抽象或更大读窗 |
| CHK003 | 满足 | A03 L65—83定义单轮、错过丢弃、立即切档、按需同资格合并、不同因果不能共用、版本通知；A01 L38规定T退出以最后位置时间恢复 | 未完握手/取消/未知不能直接降空闲；没有“按需”反复轮询许可。具体多来源竞争时效归DQ-01，不能据此反过来判所有权条款不存在 |
| CHK004 | 满足 | C2：轴150ms来自IndependentAxisTests；Flip/PutBack80ms来自ProtocolTcpFixture；代表PutBack150ms来自run-2。A06 L123—129限定命令后立即读、全部当前轴、看到中间态即200、原截止/UnknownHeld | 有依据的有限例外，不是为了测试任意恢复全局50。75ms明确为待验证目标，正文已禁止由一般PDU上限推定保证；其调度支撑不足计入DQ-01，不另造“已承诺捕获”问题 |
| CHK005 | 部分满足 | A03 L67记录due/enqueue/wire/publish并允许PDU间让出；V06 L111—116把6×25=150称B轮服务，再用于整组发布375/675等上界；块间等待/发布开销未分配 | 单块满足25仍可能整组超界，当前“轮服务”有歧义。DQ-01：补总时窗分解、块间等待上界及与即时读/发布目标的关系 |
| CHK006 | 部分满足 | A03 L69有三级优先级和单动作限制，但“已到期基础和当前反馈”同级如何选、关键多块核查可插多久、低优先位置如何得到有界机会未定；V04/V06没有固定负载容量条件 | 仅“单动作”不能证明多个读写及首状态的共同时效。DQ-01：补本适配器有限选择规则、最大插入/等待和一个固定负载可行性说明；不要求通用调度器 |
| CHK007 | 满足 | A05 L101—112覆盖初始会话截止、InitialSync、Reset不复活旧动作、读返回先查原点、独立单调监视、排队计入I/O、await后和派发前资格；设备→应答与Host→应答分开 | 原3秒与动作/保存窗口均未放宽。设备侧时间缺失只限制端到端结论；实现及验证待后续，不是设计阻塞 |
| CHK008 | 满足 | A04 L87—91、D02/D03/D06按最旧必需块计龄、独立Position、基础过期闭准入、位置Stale不改Connection；原max(500,Io×5)保持 | 新心跳/快反馈不能给旧基础或坐标续期，默认1000ms附近位置可能Stale已说明；无须延长新鲜度 |
| CHK009 | 满足 | A04 L93—97明确每轴Moving→Arrived→后读坐标、最终完整位置先发布、T先反馈后完整P；C4 L587—595确有完成后PositionForPurpose再核查 | 因果与现消费者闭合；没有用早坐标拼晚完成。无需删除保护或等慢P周期 |
| CHK010 | 满足 | D06 L64—85列四字段SampleStartedUtc/SampleEndedUtc/ConnectionEpoch/Reliability、live 1.1→1.2、Domain形状/历史格式不联动；P SY-03/05/07指向共享文档/消费者；C3支持 | 实施时可靠性取Position.Identity.Reliability（现类型不是独立Position.Reliability属性）；这是已有语义来源，无需扩大Domain端口。前端Axis行已有时间/可靠性，未发现必须改页面 |
| CHK011 | 满足 | A08、D07、V01保留原始交换、8192窗口、gap、分段及真提交，直接计数有界；C4 Transfer L25—74及Recorder L59—76佐证保存门 | 取料事实、证据提交、IPickCommit有效回执与放料分别约束；SC-006不要求全部CPU/RSS/对象下降，不以投递代提交；无需重构数据库 |
| CHK012 | 满足 | V02 L19—21保存主目录未优化产品、双方源码/产物/输入摘要、中性补丁摘要及隔离根；P说明无Git，不虚构身份 | 阻止旧副本覆盖/基线混入优化的文字足够。配置删除后哪些差异合法是独立DQ-02，不否定已有保留规则 |
| CHK013 | 不满足 | P L147拟删PlcPoll/现fixture/schema；V02 L23—29与Q L20/31要求现run-2及同条件输入，却未给两侧schema/配置版本映射。C5现budget为schemaVersion1.1、plcPoll50且schema必填；C6 L84—85分别装入configRoot/schemaRoot | DQ-02：可能使用错schema或误把迁移判不等价，反向推动保旧旁路。需明确基线旧schema/配置、改后版本/路径、允许差异及其他字段语义等价；不需要运行补证 |
| CHK014 | 部分满足 | V06 L108/115/122已标Test工程目标、75非推导保证、4950非物理定理；但V03两侧、V04预算、V06“正常对照”、V09清单与Q L52—57没有逐项分侧矩阵 | DQ-03：可能给旧基线套1110/单源/零准备要求，或要求无关环境先满足全部新门槛。需明确共同资格/仅改后/差分及超限分类，不是重新定频率 |
| CHK015 | 满足 | V04 L47—67与A02合法块可复算；18.333=12+2+3.333+1，49.333=30+15+3.333+1；60秒726+122+201+61=1110；V05 L87各项和为283；共享扣重、失败请求不剔除均有条款 | 算术与请求归属没有发现错误。名义预算与服务可行性不同，不能因数字正确就宣称时效成立；后者DQ-01，适用侧DQ-03 |
| CHK016 | 满足 | V05 L85—104限定run-2：12XY+9单Z=21批、24+9=33轴，99轴写+9其他核心写=108；fixture independent expected、RecipeRunPlanner及Axes/Transfer当前顺序相符；R10明确来源 | 仅是冻结Test路线/当前协议的通信审计预期，不是业务常量、PLC能力或全配方规则；S FR-021阻止协议次数入业务。后续manifest应保持此归属，无需扩矩阵 |
| CHK017 | 满足 | V03/V07—09及Q规定每侧一窗一链、直接保护组件；C6清单实有70项=27dotnet+7scripts+36selfcheck，010 VG-01要求持续L | 不重跑010全动态专项；同源码同attempt的既有L只引用一次。尚未实施runner/用例不构成本轮软件失败；分侧适用性在DQ-03修订 |
| CHK018 | 满足 | V07 L141—144为实际PDU重复源、旧缓存后继命令拒绝、删已冻结必需结果；V05独立预期和V09原TRX/脚本/身份核对排除只信汇总；新增Recorder gap明确为补齐而非假称已有 | 三负例判据与拒绝点明确；实现时用实际交换/原报告与独立预期核对，不能退化为参数/字符串断言。无需新增测试平台 |
| CHK019 | 满足 | V08 L150—152、V09 L160—162、Q L9—16/30—48明确新入口待实施，未知profile回009不可通过；API观察仅本轮后端负载，不冒充页面证据 | C6实际为必须提供PAGE_EVIDENCE_ROOT并校验固定旧目录前缀、管道连接和ready；文中“默认目录”措辞不精确，但缺失接线及替代边界已识别。正常实施时接真实观察，不是假造ready；无额外设计阻塞 |
| CHK020 | 满足 | P L129—152列SY-01—07、先规则/语义/验证/消费者后共享代码，001 schema在L147单独登记需先同步；旧循环/配置/错误测试逐项承接，HeartbeatFlip仍用且保留，历史证据/勾选不改 | 001并未遗漏；版本迁移内容不足见DQ-02。其余是正常后续同步/删除工作，不应因本轮没改其他文档判失败 |
| CHK021 | 满足 | P OPEN、R末节、V10区分正式地址/周期/保持及环境接线、运行NotRun；requirements CHK015仍NotRun | 正式PLC资料只限制对应真机结论；当前任务准备受下述文档缺口影响，绝非因未测试或外部未交付而全局阻塞 |

### 实质问题表

严重度只描述任务拆解所需设计决策的影响。高＝应在冻结tasks依据前定向修订；不是已证实的软件故障、性能失败或现场风险结论。

| 编号 | 严重度 | 准确位置 | 影响 | 最小修订范围 |
| --- | --- | --- | --- | --- |
| DQ-01 调度和整组时效口径未闭合 | 高；阻塞当前文档准备度 | A A03 L67—71、A06 L125—127；V V06 L111—116，关联V04 L51—53 | PDU间允许插入优先请求，六块交换之和150ms不含块间排队；又以此支撑整组发布上界。基础/快档同级选择、防饥饿及固定负载余量未定，无法一致拆出调度及验收任务 | 仅补A03/A06与V06：区分纯交换总和、块间等待、完整轮次墙钟及发布开销；明确同级选择和有界插入/等待；给当前固定负载一个可复核服务条件/时序示例；重算相关发布/即时读上界或明确独立联合目标及适用条件，不改用户确认频率/原截止 |
| DQ-02 废字段迁移与基线等价未闭合 | 高；阻塞当前文档准备度 | P清理表L147；V V02 L19—29；Q §2 L20—31；C5 schema 1.1必填plcPoll、budget L18；C6 L84—85 | 同一输入文件不能同时既含必填旧字段又已删除；缺两侧schema/配置映射会使基线混入优化、加载失败或比较资格含混。已有“各自摘要/批准差异”仍不足指导迁移 | 仅补013 P/V02/Q：基线保持原1.1 schema、version1预算和未优化产品；明确优化后的schema/预算版本与路径、plcPoll删除及013采集策略差异清单；其他业务期限、模拟/配方/算法/负载语义等价；两侧原始摘要+字段差异+中性补丁分别记录。001合同先同步再改对应共享代码，不保旧产品旁路 |
| DQ-03 验收标准分侧与失败分类不足 | 高；阻塞当前文档准备度 | V V03 L33—39、V04 L57—67、V06 L108—122、V09 L160—162；Q §4 L52—57 | 旧实现本就有高频/重复读取，不能先通过013的1110、单源或循环零准备才有资格作基线；未明确哪些25/75等目标约束哪侧，也可能把一次环境超限扩大为无关治理前置 | 仅补V及Q的适用矩阵：共同＝真实相同业务/原保护/可比输入/计量有效；仅改后＝新周期/预算/单源/固定计划及新时效；差分＝净收益和T_after≤T_before+4950。注明保护失败、改后目标不满足、环境不具可比性、未运行、正式设备不可测的分别处置与证据，不倒改门槛或强迫无关治理 |

**DQ-01的静态核算与边界**：在T阶段，业务连接名义B30+T15=45事务/s；若每次实际占用恰为允许的25ms，仅交换即1125ms/s，尚未计关键读写/调度。首Flip/PutBack阶段业务54事务/s对应1350ms/s。此计算只说明“单PDU≤25ms”不足以推出联合时效保证；实际PDU更快时可以满足，不能据此宣称软件不可实现或已经性能失败。A06/V06已经明确75ms是待验证目标，审查不把它歪曲成已承诺捕获。需要的是有限调度/容量条件和完整时间记账，不是提前跑测试或建设实时调度平台。

**DQ-02的等价边界**：允许的采集策略及废字段格式差异是013变更本身；比较要求应是这些批准差异之外的输入/预算/负载一致。两侧配置/schema文件摘要可以不同，但必须有明确语义差异说明。禁止修改原基线以适应新产品、伪称字节完全相同，或为比较在优化后保留可再用旧循环/旧配置旁路。

**DQ-03的已满足部分**：4950=25×175+23×25，文档明确其为固定Test路线的预定回归容许量，非任意运行差值定理；没有理由仅因未实测就删除/否定该目标。超限要保留原始整窗/整链，定位实际原因；环境不具可比性时不得宣称SC通过，但也不自动扩展为线程池、GC或长期压力治理任务。修订分侧口径后仍须维持原截止、模拟时序和全部必需业务步骤。

### 分类与进入tasks的文档准备度

| 分类 | 本轮结论 | 是否构成本轮准备度阻塞 |
| --- | --- | --- |
| 设计缺口 | DQ-01、DQ-02、DQ-03需要上述定向文字修订；不涉及重新确认业务/频率 | 是；建议修订后再冻结tasks |
| 正常实施任务 | 统一采集/准备计划/独立观察/API投影、最小计量、013 runner/manifest/API观察器、受影响文档同步及清理 | 否；尚未实现正是后续任务内容 |
| 运行NotRun | 两侧对照、必要组件、完整L、三负例、所有SC及requirements CHK015都没有本轮运行证据 | 否；不能提前测试来补勾选，也不称软件失败/通过 |
| 外部限制 | 正式地址/保留区可读性、正式心跳周期、最短中间态保持/锁存及设备侧时基 | 否；只限制对应正式PLC兼容/性能结论 |
| 非本次范围 | 新恢复、全量/全配方/长期压力/整机生产验收、无关环境治理和旧失败重判 | 不作为隐藏前置 |

**文档准备度：暂不具备直接进入tasks的条件。** 21项辅助评价中17项满足、3项部分满足、1项不满足；全部复选框仍为[ ]，不是自动审批。需先定向修订DQ-01—03，再复核相关条款；没有要求重建设计、修改已确认参数或提前运行验证。现有单源归属、短态局部例外、观察/保存保护及预算算术不因这三项缺口被整体否定。

本轮仅生成本清单及辅助评价，未自动修改任何设计。只读核查当前主项目及直接相关合同/源码/夹具；未构建、测试、启动/连接设备、操作运行数据库或进行Git写操作，未生成tasks/进入implement。requirements.md、feature.json、原任务与历史证据保持只读。完成后停止，等待上述定向设计修订安排。

收尾文档核查：21项均未勾选、ID唯一、逐项Notes齐全、本地链接无缺失。前后907个既有受保护文件摘要及修改时间一致，仅新增architecture.md，无删除；tasks.md不存在。复查extensions.yml的hooks为空，before_checklist/after_checklist均无附加步骤。以上仅为文档/写入范围核查，不是产品运行验证。


### 2026-10-04 DQ-01—03定向修订后复核 Notes

本节追加于首次审查之后；上面的首次17/3/1评价、实质问题表及当时“不具备tasks条件”保留为历史。本轮按speckit-plan只修013中DQ-01—03及直接关联文字，未重建清单，CHK001—021编号和全部[ ]不变。以下为**文档质量辅助评价**，不是软件通过，也不是审查者自动勾选。

#### 三项问题的实质关闭依据

| 问题 | 本轮文档状态 | 修订位置与关闭依据 | 剩余限制/后续义务 |
| --- | --- | --- | --- |
| DQ-01 | **关闭** | A03.1逐一定义due、入队、首发送、每块交换、块间等待、末响应/发布和最旧依赖；C=Σ交换+Σ块间等待+发布。A03.2固定K/R轮转、B/当前反馈同级选择、快档插一块恢复槽位、P至多两个普通R槽后服务；A03.3规定所有权、过期及取消承接。A03.4列B/P/T/快档实际互斥、K有限突发、业务连接容量和有限时序示例；V06把完整150/50/75等改为独立联合目标，75明确含排队/交换/发布。D04、R04、plan并发段与Q §4一致 | 每PDU≤25不证明容量或捕获；25ms全占满时T/首轴/首翻放业务服务已为112.5%/122.5%/135%。5ms槽只是有条件的可行示例，不是新门槛。实际时序/短态捕获仍NotRun，正式保持时间未知；原3秒/动作/保存截止及4950未放宽 |
| DQ-02 | **关闭** | V02.1、plan配置/清理及迁移顺序、Q §2固定before schema1.1/预算1/模拟1，after预算schema2.0/预算2/模拟2（模拟schema仍1.0）；明确schemaRoot/configRoot、fixture/Start/Host引用、plcPoll删除、通信策略plc-acquisition/013-1装配及批准字段差异。指出两处新运行1.1准入常量和模拟budgetRef校验，不能只改JSON。原始文件摘要、规范化摘要/快照、配置映射与中性补丁分开 | 001现行预算合同/schema和实际消费者需在共享代码前定向同步；当前活动run-1仅联动引用，不增加链执行。before完全保留旧schema/配置，新产品不留旧运行旁路；历史读取/证据不改。本轮没有实际迁移、构建或加载验证 |
| DQ-03 | **关闭** | V01.1与Q §4同一A/B/C矩阵，V04/V05明确新1110/283等只约束after；V06逐项标25/75/完整发布等after目标及双方原期限，4950是跨侧。V09.1明确各自链身份、最终after的L/009/013组件和同attempt一次引用；V10与Q给五类结果和保留失败/必要补项规则，plan/R10同步 | before不必先满足013新频率/单源；after超限不得无依据归环境。不可比不能宣布SC通过，也不自动引入GC/线程池/压测治理。所有能力仍NotRun；未运行/正式不可测不替代必需项通过 |

本轮只读源码核实的DQ-02补充依据：

- [001预算schema](../../001-station01-public-preparation/contracts/budget.schema.json)：schemaVersion const 1.1、plcPoll必填/属性；[configuration-time.md](../../001-station01-public-preparation/contracts/configuration-time.md)的“009 / AL08 当前预算与配方应用合同”仍声明1.1。
- [PublicConfigurationValidator.cs](../../../backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs) L21及L64—77、[RecipeApplicationCoordinator.cs](../../../backend/src/Gaode.Application/Recipes/RecipeApplicationCoordinator.cs) L18：新运行schema及simulation.budgetRef必须相符；[ConfigurationLoader.cs](../../../backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs) L19—47：各侧SchemaRoot与严格解析/摘要。
- [RecipeExecution010RunHarness.cs](../../../backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010RunHarness.cs) L73—106、124—129、151—152：各侧fixture目录限制、config/schema装配、Host/Start三引用；[Station01HostFixture.cs](../../../backend/tests/Gaode.Integration.Tests/Support/Station01HostFixture.cs)的FindWorkspace从DLL路径解析，单改cwd不够。
- [ConfigurationFreezer.cs](../../../backend/src/Gaode.Application/Configuration/ConfigurationFreezer.cs)、[ApprovedExecutionCostProvider.cs](../../../backend/src/Gaode.Infrastructure/Configuration/ApprovedExecutionCostProvider.cs)：预算/模拟摘要及快照身份随批准迁移改变，不能要求字节/摘要全同；[RecipeApplicationHistoryReader.cs](../../../backend/src/Gaode.Infrastructure/Persistence/RecipeApplicationHistoryReader.cs)沿用已存BudgetSource，不需新建历史执行兼容层。
- [JointInputDefinitionTests.cs](../../../backend/tests/Gaode.Contracts.Tests/Recipes/JointInputDefinitionTests.cs)当前version1加载与009公开形状基准均需后续定向承接；未运行这些用例，本轮不改任何产品/测试/配置。

#### 逐项复核与原17项结论保持

| 条目 | 本轮辅助评价 | 依据与实际影响 |
| --- | --- | --- |
| CHK001 | 满足，保持 | A01/A02频率、44字段、T承接P不变；A03固定调度未增加第二持续来源 |
| CHK002 | 满足，保持 | A02/D01冻结映射、合法连续块、计划绑定与非法拒绝均不改 |
| CHK003 | 满足，保持 | A03.1—3进一步明确切档、旧未发块撤销、非追赶和取消承接；未完事实仍保持活动，不用取消制造空闲 |
| CHK004 | 满足，保持 | A06按首start后的每轴因果观察，同批全部齐后200；A03.3取消/截止/未知退出专属50但保留活动观察；150/80及代表150ms原时序不变 |
| CHK005 | **满足，原部分满足已补齐** | A03.1及V06使用完整C、g与最旧依赖年龄，明确联合目标；六块交换之和不再冒充发布上界 |
| CHK006 | **满足，原部分满足已补齐** | A03.2有限轮转/位置服务机会、A03.4固定负载与竞争示例；容量条件和实际时效须同时核查，不推导硬件保证 |
| CHK007 | 满足，保持 | A05原3秒独立截止、迟到先核资格、保存及后继原Window不变；V06两类心跳延迟分侧更明确 |
| CHK008 | 满足，保持 | A04/D02/D03/D06仍用每块实际时间、独立位置与最旧年龄；完整C和新快读不能给旧块续期 |
| CHK009 | 满足，保持 | A04每轴Moving/Arrived→后读坐标、T反馈先于坐标、最终完整位置先发布均不变；PDU让出不改变因果序 |
| CHK010 | 满足，保持 | D06四个Host位置字段、live1.2及既有Domain形状保持；D04新增字段仅通信内部，不暴露协议分组 |
| CHK011 | 满足，保持 | A08/V01/D07原始证据、gap/分段/历史读取、真实取料提交门均不变；仅源头减量和有界直接指标 |
| CHK012 | 满足，保持 | V02/V02.1原基线先冻结、共同中性补丁另记，明确各侧DLL和schema，不混入013优化 |
| CHK013 | **满足，原不满足已补齐** | V02.1已固定版本/完整引用映射、批准差异与摘要；plan001及消费者先同步再代码，无“版本待定”或旧运行旁路 |
| CHK014 | **满足，原部分满足已补齐** | V01.1/V06/V09.1/V10与Q §4分侧及五类结果一致；保护/after超限不可换名通过，不扩大环境治理 |
| CHK015 | 满足，保持 | V04原18.333/49.333、1110和活动公式不变；互斥失败桶补充避免重复计数或漏计；非实测且仅after预算 |
| CHK016 | 满足，保持 | V05静态21/33/108及独立业务预期不变，V01.1/Q明确只审计冻结run-2/当前布局，不下沉业务常量 |
| CHK017 | 满足，保持 | V03/V07—09及Q仍各侧一窗一链、必要组件、完整L、受影响009；V09.1限定最终after身份并去重，未增加run-1执行 |
| CHK018 | 满足，保持 | V07 N1—N3仍依赖真实交换、后继拒绝、发现/原报告；V09.1仅补身份归属，未退化为参数/字符串判断 |
| CHK019 | 满足，保持 | V08/09与Q保留真实API观察器、原页面证据边界、明确013分支/manifest；配置映射不授权假ready或默认009通过 |
| CHK020 | 满足，保持 | plan SY-01—07及DQ-02具体迁移顺序补准001/加载与准入消费者；先文档再共享代码，旧循环义务承接/历史事实均保留 |
| CHK021 | 满足，保持 | plan OPEN、V10及Q区分正常实施、运行NotRun、正式NotMeasurable；不以未测试或外部缺口全局阻塞tasks |

#### 本轮文档准备度与停止

本轮一次只读一致性复核按时间公式/等待边界、配置版本/引用可执行性、合同-plan-quickstart适用侧、原频率/期限/保存及最小验证集合进行，未发现新的阻塞性设计问题。三项DQ的**文档缺口已关闭**；21项辅助评价现为21项满足，全部复选框仍[ ]。**具备进入speckit-tasks的文档准备度，等待设计审查，不自动执行tasks。**

“具备文档准备度”不表示调度时效、降负载收益或正式PLC短态捕获已经验证。实现/接线/迁移和全部运行能力继续NotRun，requirements CHK015不变。正式地址/合法范围、正式翻转周期、最短保持/锁存及设备时基仍限制对应真机结论；不阻断已明确的软件任务拆解。本轮不构建、不测试、不启动/连接设备、不操作运行数据库、不进行Git写操作；未生成tasks或进入implement。

本轮收尾范围核查：预先记录的908个文件中，仅六份获准013设计产物及本architecture追加内容发生变化，其余901个文件摘要/修改时间不变，无新增或删除。首次architecture全文前缀完整，21个CHK编号唯一且全为[ ]；spec、requirements（含CHK015 NotRun）、feature.json、AGENTS、宪章及核查范围内产品/测试/配置/其他功能文件保持。七份文档本地链接无缺失，tasks.md不存在；extensions.yml hooks仍为空，无附加步骤。这是只读文档/范围核查，不是运行验证结果。


Notes（2026-10-05，需求方批准013-acceptance/2）：本次定向区分硬保护/降载与非阻断工程目标，依据验证合同V06.2。原DQ关闭/首次审查/复选框及CHK015 NotRun全部保持；规则版本修改不等于软件通过。T029/T030仅在新判定器正负验证与原证据身份复核后更新，原时效Failed不得覆盖。


run19完成Notes（2026-10-05）：按需求方批准的013-acceptance/2，当前判定器7/7、完整L70/70，原53项组件及真实N3重解析通过，既有run17原始数据复核硬条件/净收益成立，T029/T030完成。仅性能观察项超限不否决；首态75ms、原保护、证据资格均未放宽。原历史评价、所有复选框及CHK015 NotRun保持，运行结论独立见 [run19报告](C:/dzk-work/013-20261005-run19/attempt/final-report.md)。
