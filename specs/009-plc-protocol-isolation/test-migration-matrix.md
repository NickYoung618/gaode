# 009 测试保护义务迁移矩阵

**当前范围与追溯**：原T01—T53/193方法/245数据项和旧断言登记不删改。当前只核错误业务协议断言纠偏、有效语义与通信断言正确归属，以及BM00固定直接语义执行；53组完整执行、动态PD/CS/M/MC/预算/保存故障/历史/整机转出，不是当前前置也不计Passed。

审计日期2026-10-01；仅静态审计，所有迁移/改写/新增验证均待实施。C=`backend/tests/Gaode.Contracts.Tests`，R=`backend/tests/Gaode.Rules.Tests`，I=`backend/tests/Gaode.Integration.Tests`，W=拟增`Gaode.Communication.Tests`。文件后数字是当前方法/断言起始行；同一行组只合并保护义务相同的方法，不授权整文件删除。

依据缩写：BD=[业务设备合同](contracts/business-device.md)，PC=[通信维护合同](contracts/protocol-maintenance.md)，EC=[诊断合同](contracts/diagnostics-history.md)，VG=[验证合同](contracts/verification-gates.md)；来源进一步追溯003对应合同、008 E01—E06及现行Word。每行指定新的可执行归属，不能只说“已有覆盖”。

## 1. 端口、线缆及动作门禁

| ID | 旧测试/断言 | 保护义务与有效依据 | 分类 | 承接位置与验证 |
| --- | --- | --- | --- | --- |
| T01 | C/Ports/StagePortContractTests.cs:11/25/41（Detection关联、来源、受理/执行） | Detection非单PLC动作；有效关联/来源；受理不等于完成，003 detection/BD03 | 保留 | C业务端口测试使用语义结果；V-SEM核对错误身份/来源不能完成 |
| T02 | 同文件:50 `PlcStageContractFixesSortingAndUnloadMappingsAndEpochCorrelation`的地址及完成码断言 | 真实映射需校验；要求Application公开地址/码失效；原测试用Sorting状态2作整项Completed也失效，PC04 | 失效要求＋迁移＋改写 | W独立literal oracle承接地址/码；C只测epoch与“可靠放料才整项完成”；不得只删测试 |
| T03 | 同文件:73/80/93（Unlock前置、UnknownHeld、workflow/stage区分） | 已提交WholeTray、未知保持/no retry、Detection不是PLC stage，BD03/06 | 保留/语义夹具改写 | C业务契约，不含原始状态；V-SEM |
| T04 | C/Ports/PlcStageActionPortContractTests.cs:11 `SortingMotionCannotConsumeTheLongerTrayDeadline` | 独立机械上限不能借大整盘预算，003阶段预算/008 E06 | 拆分 | C保留动作/总期限义务；W检验到期后的线缆零后继写；V-WIRE/F04 |
| T05 | 同:24 `SortingPickPlaceAckRequiresStatusThreeBeforeCompletion` | 取料2非整体完成、放料3与ACK闭环，Word§3.1.6 | 迁移＋业务改写 | W完整原值时序；C以取料/放料语义区分；V-WIRE/V-PICK |
| T06 | 同:42 `SortingLatchesTargetStageBeforeRaisedCompletionZ` | 目标采样与抬升完成分离，003动作合同目标窗口 | 迁移＋保留业务证据 | W真实XYZ/状态时序；C两份关联语义证据，不能要求完成时Z仍等目标 |
| T07 | 同:57 `PickMustBeSavedBeforePlaceFieldsAndSaveFailureKeepsUnknownHold` | 真实取料保存成功前禁止所有放料字段，FR-018/BD05 | 拆分/加固 | W回调前后写顺序；C真实提交回执关联；I及独立进程F05-A提交前回滚、F05-B真实commit后失效回执、F05-C暂不可核查；核对实际行/原期限/零放料，fake回调不能替代 |
| T08 | 同:78 `MissingTargetStageObservationNeverSubmitsPlaceOrSortingOk` | 缺本动作目标证据不可推进，BD03/PC04 | 迁移＋业务改写 | W缺采样无后继线缆；C收到Unconfirmed保持占用；F01/F02 |
| T09 | 同:88/102/115/125/135（UnloadWritesGrabZ、SameTargetWithoutDistinguishableFeedback、ZResetMissing、UnloadMovesFromDifferentZ、MissingFrozenUnloadTarget） | 下料GrabZ/完整XYZ/新旧可区分、前周期复位/冻结目标，Word§3.1.6/BD03 | 迁移＋改写 | W保留实际地址/编码/读回/缺前置零写；C以可靠定位/未知/缺合法目标表示，V-WIRE/F01/F02/F04 |
| T10 | 同:147 `UnlockRequiresCommittedWholeTrayReferenceAndObservesZero` | WholeTray提交门禁＋真实解锁读回，BD06/PC04 | 拆分 | C提交/语义Released；W命令及反馈0的真实断言；V-MAIN |
| T11 | 同:159/173/189（DisconnectAfterCommand、PredispatchCommunication、EpochChangeAfterDispatch） | 写后未知不重发；派发前4次总尝试1/2/4退避；代次关联，003 stage | 迁移＋业务保留 | W断线/尝试及实际写数；C保持UnknownHeld不可重发；V-FAIL F03 |
| T12 | C/Devices/LatestPlcProtocolTests.cs:16/31/66/78 | 四字序已知字42F6/E979、literal地址/方向/宽度、写方责任，Word与独立向量 | 保留并迁移 | W保留独立预期，扩到全部在用信号；V-WIRE/M04。禁止用共享生产定义生成expected |
| T13 | C/Devices/VirtualPlcLatestProtocolTests.cs:23 `FStatusCannotBeClearedBeforeCompletionEvenWithStaleResetTwo` | F本轮复位、旧值不代替，Word§3.1.7/008 E04 | 迁移 | W正式端口真实TCP/设备状态，F04 |
| T14 | 同:38/83/129（TCP epoch、心跳首错/可靠观察、stale安全） | 失联/陈旧不授权、首错证据保留，003 FR03/BD03/EC01 | 迁移＋语义断言保留 | W通信故障/窗口；C用Stale/Unavailable验证准入；V-DIAG/F03 |
| T15 | 同:172/292/323/376（全协议动作、安全门禁、独立scan可见启动、F后绑定/复位）；:441–442组件5秒看门狗 | 真实动作及F失效不绑定仍有效；5秒非已批准B业务总期限，现FR-035—039另有明确增量 | 迁移＋新增期限承接 | W保留真实TCP及A/B区域/显示原协议义务；V-MAIN不把A当F绑定。T51—T53/BA01—07补新业务窗口/取消/真实保存；调整组件看门狗仅为覆盖新等待，不改变冻结业务预算 |
| T16 | C/Devices/InspectionHandshakeSequenceTests.cs:13/39 | 当前operation/epoch/XYZ、本轮复位后才后继，旧Simulated测试同时检查1/2/0 | 改写＋迁移 | C测试采集窗口、关联及Released；W实际PLC端口TCP验证数值时序，不用模拟结果自证协议 |

## 2. 业务规则、步骤、配方和模拟端口

| ID | 旧测试/断言 | 保护义务与依据 | 分类 | 承接位置与验证 |
| --- | --- | --- | --- | --- |
| T17 | C/Station01/ThreeDStepTests.cs:12/34，:25数值序列；当前ThreeDStep.cs:56–62失败清理 | 暂停仍处理已派发动作，一次3D、原样本、intent先fact；3D失败时未关闭安全/取消准入仍清理，BD04 | 保留＋拆分/补必要失败 | C语义窗口/正常保存先后；补保存失败仍尝试受控清理但业务失败、不派后继，且取消/安全关闭不清理；W承接检测1/2/0。不得照F门禁误删3D现有清理 |
| T18 | C/Station01/FScanStepTests.cs:11/34，:24–25/46–47数值断言 | 一次F采集/解码、无越权配方操作、保存失败不结束F，BD04 | 改写＋迁移 | C验证业务保存成功才结束窗口；W保留3/4/0及失败无结束写 |
| T19 | C/Station01/StartClampStepTests.cs:12/32/41/52/73 | pause、冻结deadline精确边界、断线/epoch、PLC内部夹紧、超时不运动，003 FR02 | 改写＋保留 | C构造Secured/Unconfirmed，保留启动受理、夹紧、A区域准备三个窗口及保存后3D顺序；W验证A真实区域握手/失败零3D与不发送PC夹紧命令；B由T15/T32承接 |
| T20 | C/Station01/PalletUnlockStepTests.cs:11/31/46/66/85 | WholeTray前置、失败/超时/epoch未知不重试，BD06 | 改写＋迁移 | C保存前置、Released/UnknownHeld；W原0/1读写及真实错误，F03/F04 |
| T21 | C/Recipes/FlipFeedbackCorrelationTests.cs:15/24/37 | 旧完成、发令后新反馈、目标面/人工区/epoch，BD03 | 迁移算法义务＋改写 | W测试正式ExecuteFlipAsync而非孤立Application辅助类；C只用当前对象的语义面结果，自动/人工来源区别保留 |
| T22 | C/Workflow/WholeTrayWorkflowOrchestratorTests.cs:17/43/55/66 | MainFlowPersist顺序、Unlock不能绕提交、未知解锁无ObservedUnlocked、Simulated不能真实验收 | 保留＋夹具改写 | C移除:131–132原值结果，BD06/V-SEM；实际提交由I承接 |
| T23 | C/Workflow/ThreeStageWorkflowExecutorTests.cs:24/48/64/81/90/102/112 | 完整映射顺序、MappingFailed不调用PLC、缺目标/目标冲突/源占用阻断、预留保存先行、混盘OK留原位 | 保留 | C语义请求/结果/回执；V-SEM/V-PICK，不删除预留或无搬运义务 |
| T24 | 同:128/143/156/175 | Communication有限重试、算法2/5退避、共享绝对deadline和临界点不派Sorting | 保留；通信内部细节迁移 | C保留已授权业务重试分类/总期限；W承接适配器内部传输尝试，不能重复形成双重重试；V-SEM/V-WIRE |
| T25 | 同:188/215/237/253 | UnknownHeld保持关联/no retry、旧operation/epoch不完成、Detection断线不是物理PLC未知、恢复保持身份 | 保留＋raw夹具改写 | C取消:193/380原值依赖；V-SEM/F01/F03，不放宽任何未知判断 |
| T26 | C/Workflow/DetectionRetryAndPendingTests.cs:16/38/72 | 错实际位置不分拣，有限失败Pending及媒体/身份/期限/合法处置，008 E03 | 保留＋夹具改写 | C:151换语义结果；V-SEM/V-MAIN |
| T27 | R/Motion/MotionAdmissionTests.cs:11/29；R/Station01/CompletionPolicyTests.cs:9 | 已存意图、安全/夹紧/合法目标、Test不用于真机、唯一所有权和真实完成门禁，P04/P05/P08 | 保留＋语义观察 | R无需协议程序集；N/P边界正例覆盖正常业务数字 |
| T28 | C/Station01/StartUseCaseTests.cs:25/41、EventCorrelationTests.cs:11/24；C/Devices/DeviceMessageContractTests.cs:9 | 算法未Ready不单独拦合法准入，Disconnected是Unconfirmed、重复/过期不受理、ACK非完成 | 保留 | C稳定语义原因和关联，错误码不能含可供业务解释的raw值；V-SEM |
| T29 | C/Devices/HeartbeatInterlockTests.cs:13/28/53/71；T065OriginalDeadlineTests.cs:22 | 2999/3000ms边界、失联锁停、恢复不自动续跑、原I/O deadline | 拆分保留 | C业务受限/准入；W真实心跳翻转/计时；不为测试延长任何预算 |
| T30 | C/Simulation/SimulatedPlcTests.cs:12/36/45/65/93/106；DeliveryIsolationTests.cs:12/33 | 内部夹紧不造按钮/命令、区域准入、失败、心跳停止不被业务回调阻塞、调用线程隔离、周期结束才释放 | 改写 | 语义模拟器合同留原基础设施测试类别（不作为业务冻结断言或TCP证明）；raw锁/检测序列由W真实协议承接；业务步骤fixture使用语义替身 |
| T31 | C/Recipes/RecipeExecutionCoordinatorTests.cs:22/27/46/67/89/104 | 真实步骤、未知不可跳、绝对预算、两面不重扫、同面不同实体分别翻、拒旧占位目标 | 保留＋新增期限关系断言 | C保持原RecipeExecutionBudget公式/起点/值；BA06证明绑定耗时不刷新严格后段、旧路径原起点保留。新绑定预算不按协议数量生成，配方不随变体变 |
| T32 | C/Recipes/RecipeCatalogTests.cs:17/52/100/113/148/171/190/217/241/258 | 不完整/错面拒绝、冻结目标/工作量、历史退出配方不可动作、初始高度、合法槽/ABCD批次顺序、参数及provider/显示号、F后绑定 | 保留＋名称/业务类型改写 | 保留配置提供器的基础设施契约归属，业务计划断言可抽到C语义测试；ProtocolSlotIndex是物理槽不可删除；PlcId只HMI。V-SEM/配方冻结 |
| T33 | C/Recipes/PublicPreparationTargetResolutionTests.cs:17/65/100/157 | 两面来自初始测量、错误对象/面/轮次拒绝、当前高度来源、固定目标不能用默认高度 | 保留＋槽类型改写 | C业务目标解析；现有payload读取support明确分类；与T36—T38失效重扫要求的替代义务对应 |

## 3. 诊断、真实集成、历史与保存

| ID | 旧测试/断言 | 保护义务与依据 | 分类 | 承接位置与验证 |
| --- | --- | --- | --- | --- |
| T34 | C/Devices/ModbusDiagnosticsTests.cs:13；HeartbeatWindowDiagnosticsTests.cs:16/59/118/158；VirtualPlcMonitorTests.cs:10/37/53 | 真超时/通道、首末窗口、有界缓存gap、同值写receipt、reset代次防晚写，003诊断/EC01 | 保留迁移 | W保留内存/设备审计断言；新增V-DIAG持久解引用和F06，不假称旧测试已验证持久库 |
| T35 | C/Devices/SingleFaceDetectionIntegrationTests.cs:29 `AbThenCdExecuteThroughVirtualPlcWorkerMediaAndSqlite`、:39 `NecessaryFailureStopsBeforeNextProductMove` | 实际AB/CD采集、Worker/媒体/SQLite，错到位/媒体保存/复位失败不后继 | 迁移真实集成＋拆wire | I保留业务结果，W/通信probe承接raw；V-MAIN/F02/F04，标同进程夹具，不冒充独立Host/PLC |
| T36 | 同:45 `Q03PostFlipComponentRescansAndRunsSecondFaceThroughSamePort`（Skip） | “翻后必须重扫”被20260925/008 E04废止；第二面实际执行仍有效 | 失效历史要求 | 以T33首次测量、I当前Q03第二面实际采集/保存和W翻面闭环替代；不继续Skip |
| T37 | 同:49 `Q03FirstFaceComponentStopsBeforeUnconfiguredFlip`（Skip） | 旧未接入Flip前置已不适用于合法现行配方；缺合法目标仍应阻断 | 失效历史要求＋保留条件 | I合法Flip继续、C缺目标拒绝（T23/T33）、W错面/缺ACK阻断；不取消目标校验 |
| T38 | 同:55 `Q03PostFlipFailureNeverStartsSecondFace`（Skip Theory两数据项） | 翻后重扫失败路线失效；当前错面/复位/保存未决不后继仍有效 | 失效历史要求＋改写 | W自动翻面反馈/ACK失败及I必要保存失败替代，保留失败不后继义务，移除Skip须有承接 |
| T39 | I/Station01/ThreeStageMainFlowIntegrationTests.cs:30/89；VirtualRecipeAndDetectionGateTests.cs:19/39/60/86/113；VirtualPlcSafetyGateTests.cs:15 | 持久handoff、Pending、错误映射、未知不解锁、3D不猜Z、不复用旧F、不默认OK | 保留＋拆probe | I业务断言冻结；W/support负责线缆故障注入；V-MAIN/F01/F02/F03 |
| T40 | I/Station01/StrictQ01SaveGateTests.cs:24，:71地址/命令写数 | 真实媒体保存失败阻断后继定位/Final，P08/BD04 | 拆分 | I语义“无后继定位/无Final”；通信probe验证原始零写，V-MAIN/F05/F06各自新增实际存储故障，不偷换故障点 |
| T41 | I/Api/CommittedDispositionProjectionTests.cs:38/56/66/74/88/106 | 取放证据/真实槽≠Sequence、run/tray/plan/epoch/entity、未知不被晚receipt完成、无搬运不造动作、混盘留件、特殊出口 | 改写新语义＋迁历史 | I新语义提交投影；W有限Infrastructure历史读取测试保留:28–34旧raw2/3/ACK原字节与不足证据拒绝；V-HISTORY |
| T42 | I/Api/CommittedResultProjectionTests.cs:13/32/57/76 | 实际值不能复制目标、图像可先于聚合、质量与Final分离、无关联旧payload不能冒当前对象 | 保留 | I新语义事实/读取，W旧payload固定样本；V-HISTORY/V-MAIN |
| T43 | I/Station01/OrdinaryImpactRegressionTests.cs:20（Q03/NG/Pending/Q09/Q18） | 图数/动作次数、不重扫、结果/Final；:47内部FlipAck与:69–70raw sort1/2 | 拆分 | I实际实体翻面/处置及保存次数；W通信ACK/取放命令；选定usr-e新配方冻结，不回旧四面范围 |
| T44 | I/Station01/CurrentConfigRegressionTests.cs:16；RecipeMultiObjectIntegrationTests.cs:70/177；RecipeRotationIntegrationTests.cs:21 | 同构建配方差异、非连续物理槽、各成员目标/整体共享翻面、E问题保留、特殊出口不重复搬运 | 保留业务＋拆通信 | I业务断言及实际Worker/DB；通信/Test HTTP probe单列，旋转不编造生产寄存器；V-MAIN |
| T45 | I/Station01/NormalPublicPreparationTests.cs:21/154，:171锁原值 | 相同Host在真实/受控时间模拟下提交实际SQLite和媒体/handoff | 保留＋语义观察改写 | I保留模拟来源声明及提交；不用于独立TCP隔离证明 |
| T46 | I/Station01/SingleCommandRecoveryIntegrationTests.cs:21/81 | 文件名是历史名称，当前测试保护新轮链接未提交不新启动、复位初始状态及显式完整新run，USR-D | 保留＋拆probe | I新旧运行/旧证据/提交；:45/55/57 Start原始写次数迁通信probe；禁止按文件名退回单指令重发 |
| T47 | C/Persistence/StageEventStoreTests.cs:39/58/91/109/135/144/161；I/Storage/StageAndCompletionTransactionTests.cs:40/62/98 | 事件/投影同事务、幂等、在途未知、检测断线、不合成历史、失败回滚、intent先dispatch、WholeTray/Final提交 | 保留基础设施契约 | 原项目可保留Infra引用；新payload只改表达，保留事务义务；补F05/F06-A/B/C的真实提交/回执分离、UnknownHeld最小记录/全库不可写恢复及U0/U1/U2/UX实际结构核验，V-PICK/V-HISTORY；晚提交存在也不能复活旧动作 |
| T48 | R/Architecture/DependencyRulesTests.cs:9/22 | 原四层依赖及Domain禁框架 | 改写扩展 | 精确允许纯协议项目参考，业务仍禁协议；同检查器A01—A10、N01—N17/P01—P10，V-BOUND |
| T49 | scripts/workflow/test_runner.py、test_verify.py及runner.py:182 | 当前零测试/Skip/失败拒绝有效；缺必需ID检查 | 保留＋加固 | G01—G07逐ID负/正例接实际verify，V-LEDGER含新增V-BIND及BA全部适用数据行、真实TCP/DB证据；新增套件/独立进程案例不得漏注册或过滤后报告全通过 |
| T50 | scripts/verify-station01-page-diagnostics.cjs:66；validate-008-operation-evidence.py:96起 | 业务拒绝原因与持久动作证据必须真实；旧alarmBits===2/PositionEvidence raw为边界耦合 | 拆分 | 原脚本保留语义/API/提交断言，精确通信probe/独立oracle承接raw；VG V02.1的TS/Python/PS AST内容检查及A08—A10/N11—N17/P07—P10接正式verify；拆分细目见§3.1，不能只冻结/哈希，不扩页面 |
| T51 | C/Configuration/ConfigurationValidationTests.cs、Station01RunConfigurationTests.cs；R/Timing/OperationIngressTests.cs:11–14、DeadlineSchedulerTests.cs:11/44/57 | 原用途/版本/冻结和ResponseBeforeDeadline义务保留；新FR-035—039不可由旧测试自动证明 | 保留＋必要新增 | C/R语义测试承接BA01/04/06/07：schema1.1必需新预算、三入口关联快照、10000ms、非法拒绝/Production无回退、T−1/T/T＋1和原截止不刷新；不含协议阶段/报文公式。初次更新后冻结 |
| T52 | T15/T32/T39/T45及scripts/summarize-q01-q02-evidence.py:42–46的RecipePlanBound数/版本/planRevision，后部冻结catalogDigest；当前无BA03健康绑定卡住注入 | F唯一匹配、实际容量/显示、真实绑定/移交和冻结义务有效；组件/历史汇总未证明本次总窗 | 保留＋拆分＋必要新增 | I/业务脚本承接BA02/03/05的DeviceApplied≠Bound、两容量、后台关闭及本次意图/保存；W与独立TCP probe核验真实前置/握手及失效后零新派发。Host/API共用业务协调、VirtualPlc Test注入均待实现；不以fake端口/HTTP取消代替 |
| T53 | I/Storage/TraceStoreTests.cs:99–127（RunCreated实际commit后扣回执）；AlgorithmIntentPersistenceTests.cs:21/45（算法意图保存等待） | 真实提交与及时回执分离、意图先设备的原义务仍有效；原案例不是绑定/移交总窗验证，R02/R05不得弱化 | 保留＋精准扩展 | I复用真实SQLite/TraceWriter故障缝，按BindingId/WriteId精确命中RecipePlanBound和handoff的BA04 late两行；BA02与t0前意图验证真实保存先后，BA05迟到提交仍无授权。rawjob待实现并沿F06 A/B/C验证；不能用提交前异常或fake callback代替commit后事实 |

### 3.1 T50脚本断言逐项承接（R03/R04）

| 旧文件/断言证据 | 有效保护义务及依据 | 分类与替代保护/新归属 |
| --- | --- | --- |
| verify-station01-page-diagnostics.cjs:41–44/66，地址9/1、alarmBits===2 | 实际受限原因、启动零后继及真实报警；BD03/EC03 | 业务条件改语义安全/原因，原始地址/位迁scripts/communication精确probe；两组结果均必需；原形式由N11检出 |
| validate-008-operation-evidence.py:83–110/140–150/219，命令5/3/4、取放2/3/0、ACK、槽offset | 实际轴/源槽/本轮目标、取放保存门禁和闭环；PC04/BD05 | 身份/实际保存/API结果留业务Python；全部wire义务迁通信探针；N12三数据行和N13必须拒错误业务形式 |
| validate-008-route-evidence.py:43–44、validate-008-flip-timeout.py:33–34 | 正确实际面、Flip闭环/超时不后继；PC04/F02/F04 | 业务以FaceObservation/UnknownHeld断言；Flip_OK/Status/Current_Face原值迁通信probe，不能删超时保护 |
| audit-008-night-page-route.py:35–38/105 | 路线动作与ACK真实发生；BD03/PC04 | 页面/路线/保存留业务；PLC动作/ACK核验移通信；不以总Passed替代任一组 |
| validate-008-authorization-page.py:20、run-station01-diagnostic-api.ps1:62–63 | 越权/阻断不得派物理动作；BD02及现有API授权 | 语义拒绝及零后继结果保留，原plcWriteAudit/地址value计数迁通信；PS只编排，N17检出原混合形式 |
| verify-q01-q02-test-page.ps1:320–325、verify-008-backend-route.ps1、watch-station01-auto-removal.ps1 | 正式调用验证、非零拒绝、Final/人工移除准入不放宽；EC03/BD06 | 保留实际消费者/退出传播；业务完成判据委托受保护JS/Python，PS按A08—A10限定；入口缺结果不通过 |
| verify-latest-plc.py、scripts/tests/virtual-plc-monitor.test.cjs | 实际协议/监控审计；PC02—06 | 保留通信断言、显式角色，不错误禁用raw；P10验证合法归属，依旧受账本与独立oracle约束 |
| summarize-q01-q02-evidence.py:42–46及其后raw wire/冻结catalog断言 | 本次唯一绑定事实、配方/计划/配置冻结、实际协议动作；BD03.2/PC04、FR-035—039 | 版本/计划/时限/真实提交留受保护业务脚本并补BA证据；raw/握手迁精确通信探针；A08—A10无新增豁免，PS调用方继续要求两组结果。T52承接旧义务而非删除 |
| notification-reducer.ts及runtime.js绑定、历史API样本（I20/I21） | 新版字段/缺失/实际来源与GET对账；I§5/EC03—04 | API/业务断言核对逐字段版本及无raw，历史固定样本迁有限reader测试；保留旧payload摘要，不新增页面 |

迁移必须给每条旧断言对应的新case ID/角色/入口；新增helper和脚本由A10枚举，不能通过移目录/改名免检。门禁本身与负正例为待实现能力，不把本表当已运行证据。

## 4. 审计完整性和迁移验收

本清单覆盖当前009涉及端口/原始字段/映射引用的源码检索命中，以及其动作安全、保存、历史、验证入口的直接保护测试。不是对所有无关测试重新设计。研究中补查的RecipeCatalog、PublicPreparationTargetResolution、SimulatedPlc、DeliveryIsolation、NormalPublicPreparation及名称过时的SingleCommandRecovery已显式列入，不能遗漏仅因名称不含“Protocol”的消费者。

当前已发现三处Skip属性（含一个两数据项Theory），故不能声称现有整套verify本轮通过。迁移时将表中方法/数据行展开为固定旧ID→新ID/测试归属清单，每个旧有效断言必须有承接；全新发现不得静默排除。清单中“失效”只撤销已被有效业务/协议替代的要求，保留其余保护义务。分类审计完成不等于迁移完成。

当前完成判定：相关业务断言无协议知识，原有效保护义务保留替代定位和后续去向；BM00固定边界/直接语义行真实执行，不要求完整53组运行。旧完整冻结/BA/M验收转出且不报告Passed；不删断言、降条件或新增Skip。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`仅作为旧payload中的原文保留，不生成同名新业务事实，不倒推历史原始值或来源。消费者不以旧名字/裸kind授予动作；当前面关联继续调用FaceEstablishment.Confirms，原证据与保存门禁不减。通信用例仍检验实际清零，业务断言迁移到当前语义事实和来源，两侧均必需；不新增页面、信号、恢复路径或产品兼容层。
## 4. 前轮53组通信专项归属（历史；当前执行按BM00）

| 迁移矩阵ID | 活动义务 / 转出片段 | 任务承接 |
| --- | --- | --- |
| 迁移矩阵T01 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T02 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T03 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T04 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T05 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T06 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T07 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T08 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T09 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T10 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T11 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T12 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T13 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T14 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T15 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T16 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T17 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T18 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T19 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T20 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T21 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T22 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T23 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T24 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T25 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T26 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T27 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T28 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T29 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T30 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T31 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T32 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T33 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T34 | 通信/端口/动作直接语义及当前关联安全期限保存保持；其中配方完整业务回归不要求整机执行 | T021/T028/T029/T033—039/T048/T051/T057 |
| 迁移矩阵T35 | 实际协议/错面/目标/复位/保存不后继活动；相机Worker媒体完整链转出；历史Skip替代义务不得丢 | T024/T035/T041/T049/T051/T057/T059 |
| 迁移矩阵T36 | 实际协议/错面/目标/复位/保存不后继活动；相机Worker媒体完整链转出；历史Skip替代义务不得丢 | T024/T035/T041/T049/T051/T057/T059 |
| 迁移矩阵T37 | 实际协议/错面/目标/复位/保存不后继活动；相机Worker媒体完整链转出；历史Skip替代义务不得丢 | T024/T035/T041/T049/T051/T057/T059 |
| 迁移矩阵T38 | 实际协议/错面/目标/复位/保存不后继活动；相机Worker媒体完整链转出；历史Skip替代义务不得丢 | T024/T035/T041/T049/T051/T057/T059 |
| 迁移矩阵T39 | 直接协议断言/消费者语义活动；完整整机/全部配方/相机算法媒体业务回归转出 | T033—039/T045/T048/T049/T051/T057 |
| 迁移矩阵T40 | 直接协议断言/消费者语义活动；完整整机/全部配方/相机算法媒体业务回归转出 | T033—039/T045/T048/T049/T051/T057 |
| 迁移矩阵T41 | 必要提交/引用及当前投影直接语义、原文/缺失不补造活动；完整历史API/升级SU/业务故障矩阵转出 | T019/T036/T041—043/T045/T047/T049/T051/T057/T059 |
| 迁移矩阵T42 | 必要提交/引用及当前投影直接语义、原文/缺失不补造活动；完整历史API/升级SU/业务故障矩阵转出 | T019/T036/T041—043/T045/T047/T049/T051/T057/T059 |
| 迁移矩阵T43 | 直接协议断言/消费者语义活动；完整整机/全部配方/相机算法媒体业务回归转出 | T033—039/T045/T048/T049/T051/T057 |
| 迁移矩阵T44 | 直接协议断言/消费者语义活动；完整整机/全部配方/相机算法媒体业务回归转出 | T033—039/T045/T048/T049/T051/T057 |
| 迁移矩阵T45 | 直接协议断言/消费者语义活动；完整整机/全部配方/相机算法媒体业务回归转出 | T033—039/T045/T048/T049/T051/T057 |
| 迁移矩阵T46 | 直接协议断言/消费者语义活动；完整整机/全部配方/相机算法媒体业务回归转出 | T033—039/T045/T048/T049/T051/T057 |
| 迁移矩阵T47 | 必要提交/引用及当前投影直接语义、原文/缺失不补造活动；完整历史API/升级SU/业务故障矩阵转出 | T019/T036/T041—043/T045/T047/T049/T051/T057/T059 |
| 迁移矩阵T48 | 内容边界/脚本错误协议断言/发现执行漏跑拒绝全部活动；页面业务验收不是前置 | T014—016/T050—054/T056 |
| 迁移矩阵T49 | 内容边界/脚本错误协议断言/发现执行漏跑拒绝全部活动；页面业务验收不是前置 | T014—016/T050—054/T056 |
| 迁移矩阵T50 | 内容边界/脚本错误协议断言/发现执行漏跑拒绝全部活动；页面业务验收不是前置 | T014—016/T050—054/T056 |
| 迁移矩阵T51 | 原现用语义期限/取消/必要保存与通信相关断言活动；独立新增预算配置及全BA/三入口业务验收转出，不删除其义务 | T029/T037/T038/T041/T048/T051/T053/T057/T059 |
| 迁移矩阵T52 | 原现用语义期限/取消/必要保存与通信相关断言活动；独立新增预算配置及全BA/三入口业务验收转出，不删除其义务 | T029/T037/T038/T041/T048/T051/T053/T057/T059 |
| 迁移矩阵T53 | 原现用语义期限/取消/必要保存与通信相关断言活动；独立新增预算配置及全BA/三入口业务验收转出，不删除其义务 | T029/T037/T038/T041/T048/T051/T053/T057/T059 |

当前T051核原登记完整性及直接错误断言迁移定位；上表原动态活动片段完整运行进一步转出，仅BM00进入本轮固定账本。新CS系列仍后续待实现/核证，不能冒充旧迁移已验证。
