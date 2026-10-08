# 阶段A研究与技术裁决

日期：2026-10-08（研究始于2026-10-07）。基线：`253324492a3ffe9ea5a62b605b5dd812d04b267c`及本工作区只读源码。本文解决阶段A技术问题，不关闭020其他业务/现场OPEN。本文保留设计时正式适配与工具/VirtualPlc的只读核查结论；设计后阶段A实现及离线验证见[validation](validation.md)，未操作硬件。

## R01：在现有通信边界增加清零阶段

**Decision**：LatestProtocolPlcDevice内部统一记录周期、完成事实、撤请求收据和清零证明，复用原窗口/采样/失败保持。轴/R各自完成后清自身；放回结束才清Flip父命令；放料及最终安全位结束才清Sorting父命令。

**Rationale**：Axes.cs、Stages.cs和主文件清PC请求后返回，Transfer.cs也未确认PLC归零。这是通信职责，不需要新业务API或执行器；中间持件、取料保存及抓手保持不变。

**Alternatives considered**：业务轮询raw地址破坏分层；全局清位破坏父闭环；固定延时、写回0不证明反馈清零，均拒绝。

## R02：逐字段真实读取水位证明清零

**Decision**：用PlcExchangeClock记录清请求实际写应答完成tick；原WaitGroup/ReadRound/SignalAccessor提供同epoch且全部相关字段读取发起不早于该tick的样本，消费时核新鲜度/全0/周期未失效。

**Rationale**：SignalValues.Stamps已有Sent/Ended等。仅组Version、发布时间或新PC读配旧PLC反馈会误放行。B组需完整Starts+Axes，R/T/U组需纳入请求和相关反馈的有限计划。

**Alternatives considered**：新poller、新连接、忙等、只检查共用缓存时间戳均不必要；PlcConnectionPump现只剩HeartbeatInterlock，不恢复旧采样器。

## R03：复用资格与当前坐标分别核对

**Decision**：每轴固定内存记录本epoch实际到位及双方清零；复用前另读请求0、反馈0、可靠坐标/安全，使用原容差。新派发、可能移动该轴的其他路径、漂移、未知、安全失效、已观察复位或连接变化使记录失效。

**Rationale**：正式Axes.cs及工具position_satisfied均要求反馈1，清零后失效；旧SamePositionTests直接摆坐标/反馈1不代表历史闭环，须改。无记录只禁止复用，不禁止满足原准入的首次正常请求；不新增开机试走。

**Alternatives considered**：简单将1换0会让静态初始状态冒充完成；硬编码跳过Y/Z或固定墙钟TTL均无依据。记录可保持，但每次使用必须重新验证。

## R04：完成事实不被清零样本覆盖

**Decision**：内部移动保存/返回到位PositionObservation；EmitCompletionAsync和MoveStageAxesAsync消费该事实。ClearanceProof另走既有诊断关联，不新增公共证据schema。

**Rationale**：旋转/翻面已返回完成采样身份，SaveStageEvidenceAsync接受显式positions/observation；普通轴却在清零后Observe。AxisMotion已区分start0/feedback0为Idle，PositionReliability不依赖持续Arrived1，无须整体重写状态。

**Alternatives considered**：保持反馈1违背新确认；把0包装成到位1或制造新完成事件破坏真实性。

**设计复核补充**：Acquisition.cs在底层动作返回后才保存翻面/放回证据，必须把所有权延续到保存成功，失败沿现未知保持，不能让底层finally提前放开准入。对PositionReused，历史完成只作资格；当前新鲜复核形成关联当前Action的定位证据，满足IsCorrelated/RequireKnownPosition及后续采集消费者，不能直接返回旧动作证据。

## R05：原预算包含清零，不编现场秒数

**Decision**：正式沿原ActionWindow.DueTick及phase token剩余预算；工具原actionTimeout用于轴和父闭环末段绝对截止，放料末段含最终安全位和清零；单调时钟、不重开期限。

**Rationale**：PlcIo/IoTimeoutMs仅限单次通信，不能当机械清零周期。本轮可用显式离线预算验证到期阻断，不能据此宣称现场节拍可用。

**Alternatives considered**：无限等待、重试延长deadline、将200ms I/O上限用作整段预算均拒绝；实测不足再按有来源配置修订。

**设计复核补充**：RecipeDetectionExecutor给Flip、PutBack分别建立窗口；父关联跨阶段保留，但Holding不能沿用Flip旧截止。清零消耗PutBack原窗口剩余，不发明整个翻面父周期的新期限。

## R06：工具跨轮次推进，所有写入口受控

**Decision**：提取纯采集方法，worker每轮采集/心跳/观察/recipe各推进一次；AwaitingClear跨轮次等待，锁内只作短采集/完整RMW。引入connectionGeneration及读/写单调水位；保留HTTP路径/state枚举，observing期间reason解释待清零，completed延后到闭环确认。

**Rationale**：poll调用observe_actions/recipe.tick，begin_axis及recipe又poll，直接加等待易递归或锁死。session只是进程身份。/write与/clear若不纳资源守卫可绕过新门。

**Alternatives considered**：并发写队列、持锁等worker、只修recipe均拒绝。页面改词非本阶段必要，现reason可显示等待和明确错误。

## R07：VirtualPlc独立清反馈，保留内部停稳事实

**Decision**：轴完成后撤请求清Confirmed；Sort末端撤请求清ExecStatus；Flip现归零路径补边界测试。设备内部保存轴成功停稳事实供Sort准入，不依赖持续Confirmed1；新移动/复位使其失效。ResetFromPc只作本变更必要的一致失效，不重构现场复位协议。

**Rationale**：Axes仅删除assertedAxes；ProcessSorting命令0不清反馈且要求XYZ反馈1，新机制会破坏分拣。内部完成记忆是设备自身事实，不共享Host记录或从目标伪造位置。

**Alternatives considered**：删除分拣位置门、后端代PLC写反馈、首连静态坐标视同实际完成均拒绝。延迟/保持/断线注入只属测试，不扩现场协议。

## R08：当前合同最小覆盖，历史证据不改

**Decision**：011/014及工具协议加限定阶段A的有效增量；两份同坐标当前spec/contract改新规则，对应plan标历史顺序及020承接；逐文件见document-sync。

**Rationale**：只建020合同会留下旧规则继续指导相反实现；重写版本化验证会伪造新机制通过。正式严格运动证据与工具1.1.4配方坐标完成路径仍为明确差异。

**Alternatives considered**：全库升级、改原XLS/DOCX、历史tasks勾选，顺便改F/安全/公共Z/前端均超阶段A。

**设计复核补充**：工具手工路径的坐标误差原为报告，不同于配方定位强制验收；沿原运动完成判据清请求但必须新增双方清零。未获有效坐标验收不直接建立复用资格（无容差仅有限值精确匹配），此差异显式写入HC-01/TC-03和spec，不隐式收紧手工运动或放宽正式定位。

## 核对入口与剩余范围

- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Axes.cs`：DriveAxesAsync、VerifyAxisPositionsAsync、AxisMovingWatch；同目录Stages、Semantics、Polling及StageActionAdapter.Transfer：完成证据、R/翻面/取放与提交门。
- `backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs`、`PreparedPlcReadPlans.cs`及`ModbusTcpClient.cs`：逐字段水位与有限读计划；Gaode.Plc.Protocol负责原定义/编码。
- `tools/plc-commissioning/src/app.py`、`recipe.py`：cleared、position_satisfied、poll重入、父命令順序及手工写。
- `VirtualPlc/VirtualPlcEngine.cs`、`VirtualPlcEngine.Axes.cs`和工具`tests/simulator.py`：独立反馈、停稳事实和复位。
- `backend/tests/Gaode.Communication.Tests/ProtocolTcpFixture.cs`、SamePositionTests、MemberGripperTests及工具四个定向测试文件：复用离线设施。

本阶段技术未知已裁决；Q2及现场机械/安全参数未在本文猜定，继续按spec局部限制。


## 阶段B研究（2026-10-08，只读源码）

以下为本次plan的新结论，不修改A已完成事实。按speckit-plan安排两个只读研究任务：recipe_plan_b核对配方，mixed_plan_b核对混合组件；本节为合并核查，未运行产品/测试/硬件。

### B-R01 配方全链沿已有正式入口

**Decision**：复用RecipeEndpoints.Authoring的显式来源ID/相容键筛选及草稿/layout/validate/GET/PUT与SqliteRecipeStore，优先使用已有字段。后台源沿DeploymentPrep --seed-authoring受控准备，保留空批准和完整Source/NG/Pending目标校验。
**Rationale**：editor-draft先按相容键筛选，有SourceRecipeId时再按ID唯一选择；editor-layout核对来源版本。页面create/configure沿configurationSourceId传递，不能误禁同型号第二配方。新草稿和SaveAsync均清空新建Approval；现页面已有AB/CD XYZ、翻面取放XY、曝光/增益/亮度。StartPublicPreparation已串联F唯一匹配、槽核对、Admission、BuildExecutable与Freeze。保存后仍需实际执行对账。
**Alternatives considered**：另建导入UI、复制工具recipe、用其他型号隐藏来源、为OK代表路线删除分拣参数校验均不采用。
**证据**：backend/src/Gaode.Host/Api/RecipeEndpoints.Authoring.cs；backend/src/Gaode.Infrastructure/Recipes/SqliteRecipeStore.cs；backend/src/Gaode.Application/Recipes/{RecipeDefinitionValidator,RecipeAdmission,RecipeRunPlanner,CoordinateResolver}.cs；backend/src/Gaode.Application/Station01/StartPublicPreparation.cs；frontend/src/recipe-authoring.js；backend/tools/Gaode.DeploymentPrep/Program.cs。

### B-R02 软件校验保存后可选运行（已确认）

**Decision**：用户明确“填好后软件校验通过就能选择运行”。新建与修改均经共同校验、实际保存后可选运行，不增加人工审核、维护侧批准或版本确认。启动自动复核实际版本、参数/用途及设备条件。
**Rationale**：SaveAsync编辑保留existing.Approval；RecipeAdmission检查用途、非空摘要/依据和槽，但没有证明旧批准对应当前DefinitionDigest。本轮不能再让空Approval成为另行人工批准的前置，也不能靠旧批准字段跳过新版本的软件校验。可选运行与设备当前允许运动分别检查。
**Alternatives considered**：先前逐版本人工确认/外置授准清单提案未被采用；复制源批准、伪造Approval、直接放宽Production或新建审批平台均不采用。沿共同校验与启动准入修改受影响消费者，后续tasks先于改码。

### B-R03 显式混合模式与用途

**Decision**：设计RealDeviceCommissioning模式/用途；PLC与七相机Real，算法/外部光源Simulated，数据真实落地，同一业务执行器。
**Rationale**：Station01Registration只有三旧模式，真实相机只准Production，Production算法NotIntegrated且公共/预算强制Test；PublicConfigurationValidator、ApprovedExecutionCostProvider、Station01Policies和schema也有旧用途门。只改DI会继续失败或误放行。
**Alternatives considered**：Production中悄悄模拟、改Test标签、调用AddSimulationAdapters整体替换真实设备均拒绝。新用途需完整消费者清单，配置缺来源不放行。
**证据**：Host/Composition/{Station01Registration,Station01RuntimeOptions,AdapterBindings,CapabilityRegistration}.cs；Application/Configuration/PublicConfigurationValidator.cs；Application/Capabilities/{Station01Policies,CapabilityRegistry}.cs；Infrastructure/Configuration/ApprovedExecutionCostProvider.cs；001公共/预算schema及维护StoreManifest。

### B-R04 设置、读回和帧同一原子请求

**Decision**：沿现单相机gate增加带设置采集；wire v2同包升级，参数事实按组件保存；旧CaptureOnly无设置仍保持设备参数。
**Rationale**：CameraCaptureAdapter只Trigger且NotApplied/Unknown灯；PersistentCameraGateway.ConfigureAsync直接抛异常；CameraWireMessage v1没有设置、CameraDriver只有Open/Capture/Close。两次独立Configure/Trigger存在串参机会。已有SDK读回/备份/恢复可扩展，不新建worker体系。
**Alternatives considered**：只在日志填Applied、设置成功提示代替读回、两个独立锁动作、默认忽略不支持ROI均拒绝。
**证据**：Infrastructure/Devices/Cameras/{CameraCaptureAdapter,PersistentCameraGateway,CameraWorkerProtocol,ICameraSdkGateway,ILightGateway}.cs；Gaode.CameraWorker/{Program,CameraDriver,GalaxyDriver,CameraProDriver}.cs；Application/Ports/CaptureAlgorithmMessages.cs；019 contracts/capture.md。

### B-R05 虚拟算法必须实际参与

**Decision**：按本轮实际使用角色注册显式提供者，读取真实关联媒体，再输出用户后供受控值；来源/版本/适用配方与输入摘要一起保存。
**Rationale**：现SimulatedAlgorithm仅支持FDecode；CapabilityRegistration只给PythonWorkerAdapter注册检测/融合/码/姿态。直接复用前者不能跑全链，也不能让“默认OK”成为真实运动依据。
**Alternatives considered**：无媒体算法成功、默认有料/坐标0、替换真实相机为图片回放均拒绝。离线可以显式Test数据，不能进入现场配置。

### B-R06 现场布局能力集合与安全未知

**Decision**：按已知现场布局选择所需能力，Teach退出本路线；Model_Number MB2048 REAL及Flip_Target_Face MB2012 INT由适配层编码；F仅XY、E用扫码Z。未确认安全能力显式Unconfirmed。
**Rationale**：ConfirmedMemoryLayout同名投影和ProtocolDefinition全旧RequiredFields冲突；ModelWords和旧ManualZone/报警采样在主文件、Semantics、PreparedReadPlans及A新Handshakes都仍被消费。不能只删定义校验让缺失安全位默认清。
**Alternatives considered**：复制Test地址、把PLC内部互锁解释成所有PC门可删、用历史666或任意坐标均拒绝。已确认D01 bit0–7对应PLC上传MB6056及MB6058无报警＝0；PLC-Q2已关闭；PLC-Q3剩余等级/清除及PLC-Q4未明安全/恢复部分局部冻结。

### B-R07 公共位置与前端边界

**Decision**：公共XYZ读取/保存和现XY移动照旧；Z用途未决仅阻断改变轴集合或宣称Z执行。现页面先复用；确需新增控件再独立前端规格。
**Rationale**：PublicPositionTeaching.ReadCurrent按ThreeD取DetectionZ、ManualLoading取GrabZ，但这些公共移动仅XY；读到Z不证明其运动用途。既有012原型批准不能由020扩大。

### 研究结论与未决门

B-R01/03–07技术选择在上述边界内确定，可进入适用任务设计。B-R02已由用户明确决定并同步RC-020；MB6056对应及MB6058无报警＝0已由用户确认；剩余报警等级/清除及PLC-Q4不能由源码补造，仍保留局部冻结，其余整理不等待。具体现场安全值/型号/机械参数属于待交资料，不把已确认提供方式重开为澄清。


### B研究复核补充

已对两路源码结论及新合同进行只读交叉复核并修正：明确SourceRecipeId优先选择，避免误禁同型号多配方；公共3D/F设置以PublicSettings传递，不伪造Gain/ROI；灯设置后等待唯一归属采集适配层，删除现提前等待的重复责任；现场Parser须有规则来源；成本配置不能沿Test硬编码。StartPublicPreparation实际位于Application/Station01。上述为设计纠正，不是代码或测试已执行。


用户最终澄清：报警由PLC上传，PC只读；“恢复光栅信号”恢复的是报警反馈和安全检查，不是PC启用/屏蔽控制。PLC-Q2关闭，原控制点/时序问题取消。报警对应为PLC上传MB6056，MB6058等级0＝无报警；剩余等级/清除及首次/恢复行为仍局部保留。

2026-10-08分析整改：依据现ProtocolTcpFixture固定旧Test布局、CaptureAlgorithmMessages的AlgorithmRole定义及RecipeDetectionExecutor的单张/融合请求，明确T039独立现场夹具与旧Test夹具分工、Detection角色/能力映射、测试环境和RealDeviceCommissioning用途分别验证。FR-016由T050/T051/T052实施并在T058必要失败用例中验证。以上只补任务责任和证据边界，不产生新的PLC安全规则、代码或运行通过结论。
