# 011实施最小验证映射（当前收口）

本轮主项目M01/M08/M10实际增量：受影响构建及前端通过、主项目当前13架构检查通过、独立库正式API保存→重读与三页资源加载通过。M06/M07和原同run页面按相同源码/配置/输入范围复用，没有新完整链。26/28，T009/T010局部未完成。详 `main-project-integration-20261004.md`；以下保持原必要验证映射。

当前共同001—006、绑定001—030、状态001—007与012固定批已在011组成；下表是当前状态，后文保留事先登记及当时结果。只使用同代码/合同/输入仍适用的证据；不把重试数量当覆盖率，不重跑009/010历史整套。清单勾选不变。

| M项 | 当前必要证据及适用范围 | 当前结论 |
| --- | --- | --- |
| M01 | 完整Integration含Host/StorePrep/VirtualPlc、Contracts、Rules的orphan-offset构建；Communication的retired-monitor构建；接收012 T023006后本地前端build02 | 通过；首前端构建缺依赖保留，按原锁文件恢复后成功 |
| M02 | 当前独立oracle、分轴/翻放/取料保存及下料；028采集7项；029通信18个不同用例、监视15项；原失败及修正范围见固定批 | 软件Test有效证明；正式未交输入依赖部分Blocked |
| M03 | worker三个计算＋一个实际进程；ThreeD/F/目标/媒体移交；单面05和多面03实际初次/放回复查 | Test真实媒体/worker输出通过；真实外部新3D仍待交 |
| M04 | 配置执行8行含6面/四面E/复查异常/取消；更多面和E差异由必要组件承接，多面03真实翻放后3D且不重F | 通过对应软件范围；不冒称现场姿态编码互通 |
| M05 | 分拣预算14、三阶段10行及实际NG分拣后下料；OK/Pending/异常按独立语义组件 | 软件范围通过；同run真实分拣Completed投影已修并补证 |
| M06 | joint-single-05-results，1/1，run17602489-a2f9-4352-bd1c-c9384637f1ed | 软件链通过；原页面辅助失败和原排序投影缺陷保留，不把原16比较全算正确 |
| M07 | joint-multi-03-results，1/1，runcc7f1d75-10f0-4ad1-ba46-1bd44feeed50；共用同一驱动 | 软件链通过；multi01/02失败保留，不重跑组合或另造驱动 |
| M08 | 共同11组件＋012真实保存/API9项＋冻结reader6项；两代表链真实保存/完整GET/F唯一匹配/再次保存不变冻结/正常重启；012T022005/006 | 软件范围通过；页面原18项＋同run终态20项、011独立9项与实际GET13项有证据 |
| M09 | 实际SQLite绑定5、取料保存门、motion-cancellation10、真实TCP9、隔离12及人工取盘/Final事务 | 必要保存/取消/原期限/未知保护通过，旧失败保留；迟回执未被补为成功 |
| M10 | 现行A01—A07/B1—B5九行规则；未变必要43＋4正负例沿005真实证据复用；22当前8/8，23公开形状1/1；24最新已接合同/源九项实际9/9通过0Skip | 24正确受控根实际扫描明细已生成；规则/断言未放宽。23误名环境变量未生成明细的历史保留 |
| M11 | 历史旧Final1、特殊历史4、状态007历史投影10；当前Host原run13项GET和旧端点404；plan消费者核查/实际19文件删除与内部分支删除 | 有效历史读取保持；当前无自动续接/假历史补值，前后端孤立分支均已删 |

## 历史登记与各次执行事实（保留原时点）

下面原有“当前/尚未/NotRun”描述只对应登记时点；以上表及verification-report当前结论为准。原失败与当时预登记集合不改写。

# 011实施最小验证登记

## 当前最小集合冻结与实施状态（后文保留各次预登记/失败/原时点）

T002的交付/外部输入登记已闭合到具名当前状态；固定必要集合并不表示运行通过。正式地址/ASCII及新3D现场输出仅限制相应互通；Test固定图像/独立worker属于具名Test证据。012 T020001的Program入口及reader端点已接，状态004页面T021002及T022001观察准备已实际接收。

| M项 | 当前必要集合/数量及复用边界 | 当前状态 |
| --- | --- | --- |
| M01 | 完整Application/Infrastructure/VirtualPlc/StorePrep及受影响Contracts/Communication；Host/Integration/Rules当前完整构建 | 上述当前完整构建通过；不是设备/联合运行通过 |
| M02 | IndependentAxisTests两Fact；ActionHandshakeTests翻放6行；ProtocolStartupTests两Fact；分拣/下料13个不同必要用例（原预登记方法/行如下）；PlcMechanicalConfigurationTests Virtual/Real两行 | 通信组件有证据；分拣首轮回滚瞬时失败原因仍未收敛，不因重跑抹除 |
| M03 | ThreeDStep/FScan/目标解析/移交媒体既有预登记集合；worker三个计算＋一个实际进程（方法名见下文） | 局部通过；真实外部新3D输出待交，只阻正式来源链 |
| M04 | ConfiguredDetectionExecutionTests当前7行：6面、4面E有/缺码、2面复查异常、2面取消、1面首次全异常有/缺已保存采集；预算/分拣14项复用 | 7行实际通过，声明语义端口＋真实SQLite，不冒称设备链 |
| M05 | RecipeSortingMapper当前8个Fact；ThreeStageWorkflowExecutor本批正常/期限/保存/未知8行＋Pending异常/缺覆盖两行，共10 | 10行当前通过；实际分拣门复用M02 |
| M06 | ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite，examples/joint/run-1.json一次；期望发现/执行1 | 已完整构建/NotRun；已接012当前入口和页面代码，等待必要清理及实际观察器就绪 |
| M07 | 与M06同一方法/驱动，examples/joint/run-2.json一次；期望发现/执行1 | 已完整构建/NotRun，不增加第二驱动或完整预验收链 |
| M08 | RecipeDefinitionSerializationTests五Fact＋RecipeCommonFoundationTests六Fact=11；012保存API既有9行；绑定/冻结/reader已登记6项；JointInputDefinitionTests一Fact及012工具真实种入/完整重读2份 | 共同基础/实际保存局部证据已取得；活动联合run更新隔离/页面关联仍待联合阶段 |
| M09 | RecipeBindingSaveProtectionTests.OnlyCurrentActualRequiredSavesAuthorizeBinding五行Timely/BoundRollback/BoundLate/HandoffLate/CancelAtBound；现有取料实存、取消、阶段期限保护 | 绑定真实SQLite五行已有当前010证据；变更依赖部分定向补证，不重跑历史专项 |
| M10 | 当前正式源9项＋新增保存/序列化4项；005已验证43项未变规则负例可按实际代码/规则适用性复用，总56不同用例 | 当前06/07共9项有证据，协议485文件0违规、共同440文件452类型；43＋4未变负例按005规则/输入复用 |
| M11 | ManualRemovalHistoryTests.OldFinalRetainsOriginalUnlockReferenceWithoutInventingCurrentAllowance一项已有证据；HistoricalHandling迁移后CommittedDispositionProjectionTests.SpecialExitRequiresReleasedOccupancyActualReferencesAndSameEntity既有4行待复核 | 保留旧读取及失败证据；历史4行随current-protection-01实际通过；不是当前特殊出口授权 |

当前输入验证：joint-input-01的两项既有控制/算法分离保护通过，新增输入加载因schema缺TrayPose绑定枚举失败；精确补该枚举后joint-input-02一项通过。mechanics-config-01两项通过；两个完整工程及StorePrep零警告错误。012工具实际准备/种入独立recipes.db，两份正文所有原字段逐项只读比对通过；没有HTTP/F/设备执行声明。

T025的当前源码准备已接独立SQLite准备/种入及相对输入路径，移除Review/码映射/expectedRecipeRef旧分支，迁移单面/多面独立预期。**当前驱动已完整构建、尚未运行**；同run更新保存/重启对账和012同期页面采证已接线，待实际运行核验，不能据输入可保存勾T025/T027。

## 本次必要增量预登记与实际结果

实际结果：integration-build-03全工程0警告0错误；recipe-store-api-01为9/9，handoff-media-01为15/15，notification-components-01为3/3；均0失败/0Skip。通知三项为Station01MainFlowNotificationTests两个Fact及NotificationBackpressureTests一个Fact，只证明通知传送组件。两个失败构建日志保留；后文预登记顺序及旧时点NotRun保留。

012保存批002接收后，Integration整个正式工程先构建；只运行RecipeAuthoringCreateTests三个Fact、RecipeAuthoringUpdateTests四方法五行（四面独立E/六面两行）、RecipeAuthoringBindingTests一个Fact，预期发现/执行9。真实HTTP/SQLite和同源Matcher/旧目录快照证明归M08，不能称活动run绑定或生产准入已通过。通知三项后续按实际变更另运行，PLC链不包含在此过滤器。

本次移交保存门补充后，Contracts只复核PublicPreparationHandoffV2Tests原7行＋ObservationOrCodeFactCannotReplaceItsCommittedMedia的3D/F两行，共9；共享SemanticHandoffInputs变化还复核PublicPreparationTargetResolutionTests六行，共15。预期媒体与算法关联、未实存拒绝及配置坐标保护；其余已过43集合复用未变范围，不重跑全部历史。

## 当前执行记录（后文保留登记时点）

M03/M05/M11状态投影最小增量预登记3个Fact，期望发现/执行3：RuntimeObservationProjectionTests.MissingOrUncommittedObservationDoesNotBecomeNormalOrCreateRecipe、PhysicalSlotNumbersAndExclusionSurviveRecheckWithoutInventingMissingObservations、ActionIntentIsWaitingAndOnlyCommittedMatchingFactShowsCompletion。只验证读取已提交语义事实与缺证据保护；Host HTTP/通知接线及页面真实同run证据另列，不能据组件称T015完成。

上述首版runtime-projection-01为3/3通过。代码审查进一步发现：F前尚无配置槽集合，不能拿算法返回的集合本身证明完整覆盖；现仅在冻结配置槽已知且实际覆盖齐备时Complete，已有异常立即保留。针对该真实语义加强复核，追加1项FrozenIntentAloneCannotInventBoundRecipeAndConfirmedRunUsesFrozenValues，检查未确认绑定不冒出配方/确认后读冻结值及Complete成立条件。本次02预定4项，前版结果保留，不据旧3项称加强后的实现已验证。

Contracts完整工程及其正式依赖构建02通过，0警告/0错误；构建01两个xUnit断言写法错误已修复，原日志保留。共同基础已按下方事先登记集合执行：foundation-01.trx共11/11通过、0失败、0Skip。只证明组件语义，不包含实际SQLite/设备/联合链。

下一集合在执行前固定43行：RecipeRunPlannerTests 2；RecipeExecutionCoordinatorTests 6；PublicPreparationTargetResolutionTests 6（含face/physical-slot/stage三行）；ThreeDStepTests 2；FScanStepTests 2；PublicPreparationHandoffV2Tests 7（含missing-or-uncommitted/wrongCall/unknownOrigin三行）；RecipeApplicationContractTests 15（capacity两行、cancelled两行、9999/10000/10001三行）；FlipFeedbackCorrelationTests 3。方法和InlineData以这八个源码类对应当前定义为准，结果写preparation-contracts-01。复用当前共同基础结果，不重跑同11项。范围是M03/04/08/09受影响组件，不含主链或通信实测。

本表先于相应运行记录必要集合，范围沿M01—M11。源码副本已准备；所有软件结果当前NotRun。首次构建仍需所选工程双方直接消费者齐备；测试定义可先交，不要求未实现能力先通过。

## 当前共同基础批

M04/M08/M11：Gaode.Contracts.Tests.Recipes.RecipeDefinitionSerializationTests，新增5个Fact，预期发现5、执行5：PurposePointsAndIndependentPoseRoundTripWithoutProtocolNumbers、MissingCoordinateDoesNotBecomeZero、NumericPurposeIsRejected、OldStageFieldsAreNotAnExecutionCompatibilityPath、OmittedServerIdentityDoesNotCreateASavedVersion。输入只标Test结构，不提供保存、设备或生产通过证据。

T004/T005：Gaode.Contracts.Tests.Recipes.RecipeCommonFoundationTests新增6个Fact，预期发现6/执行6：SaveAllowsUnapprovedDraftButEnforcesUniqueRawTrayCode、FourFaceCameraRuleDoesNotLimitMoreFaces、ExtraPoseAndFlipRequireTheirOwnPurposeReferences、DigestPreservesBusinessContentAndIgnoresCommitMetadata、MatchingUsesExactGlobalCodeAndDoesNotLockObservedVersion、SnapshotOwnsDeepCollectionsAndRejectsCallerMutation。全部NotRun。正式M08的保存、重读、重启及并发拒绝必须消费012实际存储，组件不得冒充。

M01已发生：application-build-01失败（未用motion参数，已实际删除）；application-build-02通过；infrastructure-build-01及其Application/Domain/Plc.Protocol完整依赖构建通过，0警告0错误。记录仅适用于当时源码，见稳定003清单；未构建Host/测试工程。日志全部保留在artifacts/011-plc-interaction-update/implementation-20261003。

## 后续原有义务（尚未执行）

新增M03/M04最小worker组件：scripts/tests/test_011_tray_observation_worker.py的TrayObservationWorkerTests，3方法各1行，预期发现/执行3：test_media_measurements_drive_pose_and_f_location、test_missing_observation_never_becomes_normal、test_e_and_both_camera_groups_use_content。Test倾角阈值只为显式组件输入，不是生产工艺参数。此集合只验证媒体样本计算，不冒充独立进程/设备/完整链。

| 集合 | 现有可复用落点与定向承接 | 当前依赖 |
| --- | --- | --- |
| M01 | Application/Host/VirtualPlc及所选Contracts/Communication/Rules/Integration工程、StorePrep | 本次字段消费者迁移；只构建受影响范围 |
| M02 | ProtocolOracle、ProductionDefinitionTests、SignalConformanceTests、ActionHandshakeTests.Flip/Pick | 正式字段未齐仅限依赖部分；不得沿旧码假通过 |
| M03 | ThreeDStepTests、FScanStepTests.FailedAlgorithmFactSaveDoesNotSubmitFCompletion、PublicPreparationTargetResolutionTests | 新观察生产/适配/消费及实际输入 |
| M04 | RecipeRunPlannerTests、RecipeExecutionCoordinatorTests；旧无复查断言须替换 | 配置多面、额外E及真实执行义务 |
| M05 | RecipeSortingMapperTests.OkIsNotDispatchedWhileNgAndPendingKeepDistinctFormalActions；新异常过滤/排序 | 保取料实存门和物理槽关联 |
| M06/M07 | ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite，复用同一驱动 | 012真实保存/接线/页面准备和本批输入；单面/多面各一条 |
| M08 | 新共同匹配/快照组件与012保存证据合并 | 不另建存储或回执替身证明真保存 |
| M09 | RecipeApplicationReceiptTests、RecipeApplicationDeadlineTests、有效取料实存拒绝用例 | 先迁移纯软件绑定及原期限语义 |
| M10 | 原ProtocolBoundary/RepositoryBoundary/RecipeExecutionBoundary正负例 | 同一检查器，无白名单放宽/零发现通过 |
| M11 | 受影响历史reader及plan消费者清理表 | 原事实/失败证据保留，删除后查真实消费者 |

每个后续集合在对应代码/测试定义齐备后、执行前固定必要class/method/dataRow及期望数量，未固定不能拿发现结果反推集合或称完成。已有同源码/合同/输入有效结果复用，不跑全量、组合穷举或009/010历史专项。T002持续维护登记，未完整固定的义务仍未完成。

M03新增实际进程差异用例：`test_011_tray_observation_worker.TrayObservationWorkerTests.test_owned_process_reads_observation_media_and_releases_inputs`，执行前预期发现/执行1。独立本次目录、实际PNG内容、worker脚本/配置摘要、初次与复查输出及缺失观察拒绝、三次真实InputReleased；不复跑010旧专项。明确Test延时100ms/倾角阈值5仅为本次组件配置，移除旧强制延时10000的无业务用途检查，保显式非负延时校验。此前3个计算组件结果复用worker-components-01.log，此新增用例单独执行，不等于设备/联合链。


M02本次分轴增量在运行前固定：IndependentAxisTests.FiveAxesAdvanceIndependentlyAndTargetWritesDoNotBecomeActualPositions、ScanAndDetectionUseTheirOwnZAndCaptureReleaseWritesNoLegacyAcknowledgement两个Fact，期望发现/执行2。前者使用人工来源confirmed-011.json经实际TCP驱动独立五轴；后者使用实际设备适配/通信证据库，只验证轴与采集端口组件，声明的端口工作提交不冒充媒体/算法链实存。显式Test运动时间150ms，不改原业务期限；整个通信工程必须先编译。历史启动夹紧/区域准备仍在共享夹具中，单独列迁移，不据这两项称新启动流程已通过。


T010旧翻转测试定向承接：ActionHandshakeTests.Flip.cs依赖已移除ManualHandlingSession、Flip_OK和额外实际面号，当前正式合同不再提供这些信号/回显。保其历史源码基线和旧运行证据；改为分立Flip/PutBack反馈及程序先于取件XY、缺映射零运动、旧完成/旧epoch/错实体拒绝、有限等待与实际通信分段保留六行。不因编译错误删除有效保护；手动默认面号不作为当前执行兼容路径。新增方法FreshFlipAndPutBackUseSeparateFeedbackAndPreparedProgram、MissingProgramCannotMoveToPick、UnrelatedOrOldFeedbackCannotAuthorizeFlip(三行)、HeldFlipRetainsCommittedSegmentsWithoutDeclaringCompletion，运行前期望6，当前仍NotRun。该范围只为新合同对应必要通信义务，不重跑旧009专项。Test程序映射的目标字23及16字任意载荷只验证传递/关联，不表示现场型号编码；Test放回安全GrabZ=150、放回80ms只为独立模拟机械输入。

当前独立plan/bind迁移的预登记必要组件：CommittedRecipePlanReaderTests冻结正文读取1；旧V1/缺冻结/摘要篡改/错场景槽/取消5项拒绝（共6；创建后实际发现须相符）。无目录依赖；不运行历史独立绑定全套。读取不等产品续接。

轴投影预登记：RuntimeObservationProjectionTests新增1项，目标不能当实际、错run不计、扫码Z不填检测/抓取Z；DeviceSemanticProjectionTests现有2个可靠性数据行及1个startup往返（先核原实际集合，Unavailable行另计），仅受影响API组件，不跑额外完整链。

分拣必要集合预登记：StageMigration正链1（实际SQLite取料提交/抓取Z顺序/末抬升替代旧ACK），缺安全位1（替代已失效的PLC内部后抬升采样预期），Pick.cs现有无效提交3行，SaveFailure.cs实际保存故障3行；共8项。旧ACK/槽号/2取料3放料断言按新源迁移，真实保存/关联/原期限保护不删除。其余旧通信专项不全量跑。

分拣首轮8项通过7项、失败1项；实际回滚用例未到提交点，原日志保留。添加通信故障诊断后只复核该保存方法3项，3/3通过；尚无足够证据确认首轮瞬时阻断原因，不声称通过重跑消除了原因。下料旧XYZ/复位前置断言按PC04定向迁移，补本批必要5项：UnloadConfirmsFreshXyWithoutMovingAnyZ两行、MissingUnloadTargetDispatchesNoTargets、SortingMotionCannotConsumeTheLongerTrayDeadline、DisconnectAfterPickDispatchIsUnknownHeldAndNeverReplays各1。预期5项，复用既有轴和保存证明，不重跑整类。

启动迁移必要证明：StartPreparationStepTests 4项，暂停不丢已派发观察/保存、实际Ready且无旧夹紧区域伪事实、未取得接受超时无运动、断线或代次/取消拒绝；新ProtocolStartupTests 1项实TCP握手/无旧夹紧区域写/Ready保存。期限沿原PlcAcceptance，替代旧夹紧5000ms测试不扩等待，原取消/未知继续验证。完成政策保媒体/必要保存/动作状态拒绝。均实施前登记NotRun。

人工允许取盘尾段：WholeTrayWorkflowOrchestratorTests保原来源/缺来源/生产拒绝，原解锁正例改为主机允许提交→人工→Final，原未知解锁负例定向迁移为允许事件提交未确认不开放；无整盘/下料完成或过期/取消不授允许。预登记该类9项（原8行＋一个取消/过期/错误下料关联Fact），另保StageAndCompletionTransactionTests三阶段保存/人工Final事务及实际事件缺失拒绝的相关方法，后续方法/行数在运行前明确；不跑全量。

人工取盘核查：WholeTray9项已通过（首轮缺原小文件、次轮旧组件未声明新姿态参与输入已定向迁移，未放宽门）。新增租约承接后仅复核该类MainFlowPersistsWholeTrayAllowanceAndManualCompletionInOrder 1项；真实SQLite预登记StageAndCompletionTransactionTests.AggregateFailureRollsBackMatrixCompletionEventProjectionAndRunState、ManualFailureRollsBackConfirmationFinalEventMatrixAndTerminalStateTogether，以及ComponentSourceMatrixStoreTests.FinalMatrixKeepsReadyEvidenceImmutableAndAddsAuthenticatedHumanActor、TestManualActorRetainsActualOriginWhileFinalIsHostDerived四项；新协议删除旧解锁端口以RemovedUnlockStageCannotDispatchLegacyDeviceCommands一项证明无派发。旧PalletUnlockStepTests五项的必要整盘/失败/期限/代次义务转入上述WholeTray/SQLite保护，旧码读零断言失效删除；历史证据原件未删。另最小历史Final字段读取1项（待创建），确保不编造新许可。

上述历史读取唯一方法为ManualRemovalHistoryTests.OldFinalRetainsOriginalUnlockReferenceWithoutInventingCurrentAllowance（1项）；当前通知字段迁移需复核Station01MainFlowNotificationTests两个既有Fact（2项），不重复背压组件/完整链。对应4项SQLite实际已通过removal-store-01，剩余在当前构建后执行。

人工取盘实际结果：removal-contracts-03 9/9；04租约重核与历史2/2；removal-store-01 4/4；wire-01 1/1；notifications-01 2/2；相应完整工程构建0警告错误。01/02旧夹具失败留存。稳定绑定008与状态004已交；Rules/联合链/页面仍未运行。

下一批预登记M04/M05/M09：定向RecipeSortingMapper（新观察/物理槽夹具，原缺失/重复/身份保护；旧特殊出口7项改为1项明确拒绝旧豁免）、配置更多面/E规划及执行组件、预算与期限组件；不增加完整代表链。旧错误豁免不保为通过要求。

M04必要取消补充预登记：放回回执边界收到原取消，后续3D/E/面动作不派发，租约未知保持。只新增此组件1项，不重跑四个未改正例。

M04/M05/M09当前结果：sorting-budget-components-01 14/14；configured-execution-02 4/4；configured-cancel-03 1/1。完整Contracts及Integration编译0/0。001失败保留；只修声明Test公共3D坐标系，运动门未放宽。稳定运行绑定009已发布。

T024/T026前置预登记：共同RecipeApplicationCoordinator＋RunExecution＋真实SQLite的及时提交、Bound回滚、Bound晚回执、Handoff晚回执、提交后取消5个必要组件；受控时钟只触发原冻结截止，不改预算。验证仍记录真实已提交事实但不授授权，为删除旧容量ACK用例承接有效保存/取消/原期限义务。不是Host/PLC完整链。

绑定保存组件01中4份实际结果JSON已形成，但整组因BoundRollback在writer队列消费前抛异常且受控时钟未前进而悬挂，已终止本会话测试会话63028，未生成通过TRX。修正夹具为真实事务SaveChanges拦截回滚，并加实际外层有限等待；不改生产保存门。01日志与独立数据库原样保留，不能把中断记通过。

T026绑定清理落位：RecipeBindingSaveProtectionTests的5项真实SQLite保护已通过（binding-protection-02，01中断保留）。据此删除当前IPlcRecipePort、真实/模拟BindRecipeAsync、容量ACK轮询/占用、UnavailablePlcRecipePort及BA03/BA05专用故障和释放HTTP入口；当前软件绑定不派PLC配方ID/容量。历史RecipeApplication*原payload类型移至HistoricalRecipeApplicationEvidence.cs仅用于原记录读取，当前Receipt仍RecipeBindingReceipt。旧通信容量握手7项及Host健康ACK等待2项失去动作对象而删除，有效期限/保存/取消义务由上述5项、现行通信取消/未知及取料实存门承接；原报告/数据库证据不删。集成夹具的绑定计数改读本独立运行库实际RecipePlanBound写入，不以模拟设备计数冒充软件绑定。012指定源文件不修改。

清理后最小证明：完整受影响Contracts/Communication/Integration构建；重新实际源扫描M10及其必要正负例；已通过共同5项仅在该绑定执行源码未改变时复用。

绑定清理完整Contracts/Communication/Integration构建均0警告错误；binding-cleanup-protection-03删除后5/5、0Skip。此重核由装配/设备删除引起，不与02重复计数。稳定010已发布，Rules待下一步。

M10当前源首轮9项失败：50个新增文件/活动合同登记缺项、52条语义字段未登记、3条输入适配测试旧分类；原architecture-current-01与扫描结果保留。按RC08/EX01-03明确登记新字段并删除被替代字段，不自动从运行结果学习白名单；交付归档不作为现行合同副本重复发现。保存API、唯一序列化/身份、Matcher及SQLite端口实际实现加入必须可达根。预登记后续最小集合：9项当前源规则；9项既有共同执行架构拒绝；3项新增保存越权/序列化测试号/无界正文拒绝；21条既有协议负例及13条合法语义/通信正例。精确方法名见测试源码，本轮期望55项，0Skip；不是009/010历史流程验收。

M10 architecture-current-02实际发现55，53通过/2失败/0Skip；剩余为EX02 TransitionId和012 API薄信封requestId/definition明确字段登记。补齐有限字段后预登记再核9当前源＋4保存/序列化拒绝（新增一个API未知fixtureData拒绝），期望13；原已通过的协议正负例复用02，不重跑历史专项。

architecture-current-03发现13/通过13/失败0/Skip0；实际协议486文件、共同441文件/453可达类型、违规0。与02的43条未变用例合计56不同用例，当前M10受影响范围有证据，未完成Test机械清理/联合链不能据此通过。稳定共同005已发布。

T026 Test机械HTTP清理：预登记ProtocolStartupTests 2（含新增ActualReadyDoesNotInventMissingRecoveryMechanics）＋ActionHandshakeTests.Flip 6个必要当前TCP/原始SQLite证据用例，期望8；仅编译Communication及全部正式依赖，Host/Integration待012 Program参数批。旧原始HTTP历史类型只读保留，设备审计仍有实际用途的resetGeneration从旧特殊文件迁移到VirtualPlcEngine，保真实代次递增；build01遗漏该直接消费者失败，原日志保留。

mechanical-cleanup-01实际8项中6通过/2失败：当前启动已取消旧夹紧，翻转适配残留PalletLockStatus=Locked门导致正例及后继真实故障证据均未进入翻转。按已确认007/PC01规则替换为实际Ready（保安全/当前代次/位置/动作互斥），不是放宽业务断言。修复后只复核这2项；原日志/TRX保留。

配置执行复核：原5行（6面、4面E、E缺码、放回姿态退出、取消）加2行首次全部异常/缺初次采集事实，共7行预登记；因共同Source/EvidenceBasis和初次夹具保存变化均受影响，本次共运行7，不以旧报告替新分支。新增首次来源只证明声明语义输入＋实际SQLite/媒体，不冒称真实3D算法/全链；全空槽没有已批准处置，不借此新分支批准完成。

机械旁路/阶段迁移当前增量：ThreeStageWorkflowExecutorTests原必要8项预选（正常顺序1、阶段期限2、未知关联1、物理失败期限2、陈旧反馈1、保存拒绝1）；首次6失败是测试输入缺现行姿态覆盖，另查实Pending丢状态缺陷，原证据保留。补PendingRetainsPoseExclusionAndMissingObservationCannotAuthorizeSorting false/true两行，下一次预期10项。保持原期限、失败保护、分拣后下料；不以被测输出构造预期。

当前机械/姿态批011结果：mechanical-cleanup-03为8/8、mechanical-workflow-02为10/10、initial-pose-02为7/7，0Skip；对应Contracts/Communication完整构建通过。原失败及修订原因见稳定011 README，Host/Integration与当前M10待012入口签名迁移，正式链NotRun。

Runtime旧Height调用迁移预登记：AlgorithmRuntimeTests原有效算法保存/期限/取消/租约方法及RuntimeLogDistinguishesAlgorithmDeadlineFromDispatchAndRetainsLease；只选受Role和配置来源影响的方法，不全类诊断或历史专项。Integration AlgorithmIntentPersistenceTests三个真实SQLite保护先迁移源码，待012入口后完整构建再验证，不能使用旧DLL。

Runtime迁移当前结果：algorithm-role-build-02完整Contracts构建0警告错误；algorithm-role-01发现13/通过12/失败1，algorithm-role-02针对修复期限夹具1项及配置预算迁移1项，2/2通过0Skip。合计14个不同用例有通过证据，保留01失败。Integration三个真实SQLite方法仅源码迁移；Host入口未收，当前不运行旧DLL。稳定013公开交付，T026不据此整项完成。

012 T020001真实接收后预登记：完整Integration先构建；AlgorithmIntentPersistenceTests既有三个方法3项＋CommittedDispositionProjectionTests.SpecialExitRequiresReleasedOccupancyActualReferencesAndSameEntity既有4行，共7项。前者核迁移为TrayPose后的真实SQLite提交未知/原算法截止/版本拒绝，后者只核历史特殊出口读取，不能据此授权当前特殊出口。当前M10全源9项待同批Rules构建后复核，原未变负例证据按匹配范围复用，不另跑56全套。

M01/T025生命周期必要组件预登记：ThreeStageMainFlowIntegrationTests.OwnedHostNormalShutdownDrainsResourcesWithoutStartingARun一项，期望发现/执行1。复用唯一联合驱动/单面声明输入，只启动实际Host/PLC/worker并证运行库Runs为空，再由本组CTRL_BREAK关闭，核Host真实资源排空/退出码。不是额外完整链、不形成产品/联合验收证据。正常重启后重读已接同一FullRun驱动，但尚未运行。

当前收敛结果：012 T020001两入口接收后current-entry-build-02完整Integration/Host/StorePrep通过，current-protection-01为3项算法真实SQLite保护＋4项历史读取7/7通过。M10 04为7过2失败，05/06为8过1失败；通信解析移到设备构造及去掉无业务数据意义的命名参数标记后07剩1项通过，扫描485文件0违规。06其余8项及共同440文件/452类型检查复用，规则/白名单未改。叶构造用途两行leaf-mechanics-02 2/2，完整Integration/Communication构建通过，发布015。旧失败记录保留；当前新增生命周期驱动仍按其预登记验证，不能据015宣称后续源已通过全部门禁。

T009/M02独立预期定向迁移：旧AllConfirmedFields全旧表/旧分拣码断言由CurrentAxesAndMechanicalFeedbackMatchIndependentLiteralOracle替代，按既有人工confirmed-011验证五轴方向/类型/地址/码及翻放/分拣码；20260925原件保历史。Float32四字序和访问间隔/非法长度保护保留，间隔夹具改200/210、Test容量256以避新模型载荷而非放宽访问门。当前仅这3方法加受影响IndependentAxis(2)/ProtocolStartup(2)/Flip(6)及有效连接/心跳/陈旧安全(3)，预计16项；不运行旧009全专项。结果待实际记录。

当前M02失败与修复承接：current-protocol-01实际发现16、15通过、1失败、0Skip；FreshFlipAndPutBack在放回XY收到AxisActualPositionMismatch。定位DriveAxes原ReadAsync按地址拆请求，低址实际位置先于高址到位读取，可拼成跨完成时刻样本。改为当前动作已见Moving→Arrived后另读实际位置，再核原期限/连接/安全和原容差，仍错位立即失败，不放宽断言或重试掩盖。复核此翻放、独立轴及取料保存相关组件；历史分拣瞬时失败不据此直接宣称同根因已证。

017当前收敛：完整Integration/Host/StorePrep与Communication构建通过；current-protocol-01实际16/15/1/0，轴取样修复后axis-sampling-01为11/11/0/0，另8项未受影响预期/启动/连接沿原结果复用，总19不同组件。current-boundary-08当前9/9通过、0Skip，未改规则/豁免；旧失败全部保留。两条联合链尚NotRun。

T006/T013最小覆盖复核：既有PlcStageActionPortContractTests 7行（含仅保历史含义的Unlock形状，非当前派发）及TrayObservationParticipationTests 2个Fact尚无本次运行证据，安排整个Contracts工程构建后仅此9行。对象/连接/保存引用/位置与未知保护和异常原槽/正常其他槽/缺观察拒绝，均属既定必要义务，不扩矩阵。


T025输入接线：single-03真实XY与检测Z均到位，但两段运动及取证超过原8000ms完成期限。唯一驱动遗漏具名Test simulation.json stages.xyCompletion.delayMs=500，VirtualPlc用了默认3000ms/轴。驱动现显式传该500ms，并以无随机抖动执行确定Test延时，保存源及摘要。业务预算、位置校验、保存门、通过断言保持，03失败原证据保留；这不表示旧输入通过或现场速度获准。下一单面04使用相同驱动修订，仍须实际验证。


T024/T026有效隔离保护迁移预登记：AlgorithmIsolationTests同一AlgorithmRuntimeTests的同步派发占用2、取消回调2、调用线程取消1、明确未取得输入拒绝2、矛盾拒绝2、失败退出1，共10行；旧Height角色/预算仅换为当前TrayPose，保原容量、租约、原截止和实际输入释放断言。WorkerProtocolTests两个受角色字面量影响方法仍核相对路径/越界拒绝。不是新增历史专项，不改生产Runtime或延长期限。


single-04启动准备失败：实际status在Host初始化时algorithm/camera尚为null，驱动直接GetProperty导致异常，尚未发布页面连接或POST，实际Runs=0，Host正常关闭。修正仅在原有限准备等待中将该实际未就绪状态继续等待，不伪作Ready，不改产品预算。下一单面`joint-single-05/page-connection.json`，012输出`joint-pages/011-single-05`；multi01不变。012请将尚未连到04的准备等待改05，最终页面仍非前置。020的运动输入修正尚未运行到动作验证。


isolation-migration-01实际12/12通过0Skip，完整Contracts构建0警告错误：旧Height角色及预算定向改为TrayPose，保有效取消/期限/输入占用与路径拒绝断言，生产Runtime未改。


多面01关联期限修正预登记：ConfiguredDetectionExecutionTests原7行加1个受理不返回行，后者实际50ms组件输入期限、确认设备请求令牌取消/租约Unknown/零后续采集动作；ThreeStageWorkflowExecutorTests新增UnknownHeld前后阶段截止两行，真实保存投影不重试/不Pending/不下料。共新增3行，原7行因共同移动令牌生命周期变更受影响需复核；另必要2个当前轴通信和6个Flip关联用例验证唯一串行动作推进，不扩完整链。单面05已通过的原正向行为根据无断言/输入变更保存其适用证据，修正后的多面02覆盖新派发路径。


当前期限修正：motion-cancellation-01为8个配置执行/取消组件＋2个UnknownHeld阶段投影，10/10通过0Skip。通信另加唯一必要负例CancellationAtXyAcceptanceCannotDispatchDependentDetectionZ：真实TCP在XY已受理时取消，验证后继检测Z无目标或触发写、无完成且保持Unknown；与既有独立轴2/翻放6合计9项，运行前预登记。不以纯声明端口替代真实通信取消证明。

M10 motion-10因本次证据根误设到受控根外，9项全部失败；未取得架构通过。motion-11用既有受控根实际扫描后8通过1失败，A02四条命中均为共同新增DetectionResultKind.UnknownHeld未登记。依据本轮execution-and-state已明确的业务保持语义，精确登记此枚举一个成员；不改变规则/类型范围/原码禁令。只复核A02，其余8项实际结果复用。

start-dispatch-01：11项9过2失败。一个恢复评估独立读取被误改成常规观察子集，遗漏SystemResetCmd，已恢复该显式全字段读取（不改恢复限制）；另2秒Startup实际Ready只耗约15ms，但真实SQLite首次证据PersistedUtc临近截止、回执迟到，保持Unknown，未增加期限。对应DB009-wire-formal-5f71469fa1c242899e71d2c1bdfb1be9原件保留；此迟回执原因未充分定位，不能宣称由恢复读取修复。只复核这2个启动项，原9项复用。
