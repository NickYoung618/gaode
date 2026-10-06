# 功能任务清单：新016公共准备、3D穴位检查与公共下料

输入spec.md/plan.md/data-model.md/contracts/public-tray-flow.md；2026-10-06；宪章9.0.0及本次最新授权。只改独立副本，前端沿006增量。

T006/T010包括交接启动身份允许空occupiedSlots，实际占用由已提交3D和F后冻结计划确定；不得去掉真实提交、绑定和摘要门。

## 必要准备与基础

- [x] T001 独立复制、逐文件SHA及原始恢复依据，delivery-baseline/manifest.json；FR-013/P08/P13。
- [x] T002 同步直接冲突的活动spec/contracts/plan/tasks及006独立增量，specs/016-public-preparation-tray-check-unload/document-sync.md；FR-011/012。
- [x] T003 文档清单实质审查、只读分析及定向修订，specs/016-public-preparation-tray-check-unload/review.md；FR-012/P01。

## 用户场景 US1 公共位置及启动

目标/独立结果：真实保存/全读/重启/冻结、可靠实测/拒绝、就绪前不动。FR-001—003/P03/04/08/09。

- [x] T004 [US1] 固定必要保存/冻结与启动顺序验证，backend/tests/Gaode.Integration.Tests/Station01/PublicTrayFlow016Tests.cs及Contracts对应Tests；SC-001。
- [x] T005 [US1] 公共位置完整受控保存和实测服务，backend/src/Gaode.Application/Ports/IPublicConfiguration.cs、Configuration/PublicPositionTeaching.cs及Infrastructure/Configuration/ConfigurationLoader.cs。
- [x] T006 [US1] Host装配/配置权限与API，backend/src/Gaode.Host/Api/ConfigurationEndpoints.cs、Station01Authorization.cs及Composition/Station01Registration.cs；保留当前语义启动顺序并纠正旧注释。

## 用户场景 US2 混合观察/决策/Pending

目标：正常检测、空位跳过、异常单决策→F→最后实际Pending，真实取料门。FR-004—007/P05/07/08/09。

- [x] T007 [US2] 固定完整覆盖/空盘拒绝、一次选择/停止和异常Pending的必要组件，backend/tests/Gaode.Contracts.Tests/Station01/PublicTrayDecision016Tests.cs及Workflow/RecipeSortingMapperTests.cs。
- [x] T008 [US2] 观察完整身份/覆盖及实际Worker边界，backend/src/Gaode.Domain/Station01/TrayObservation.cs、Infrastructure/Algorithms/PythonWorkerAdapter.cs、scripts/010-content-sample-worker.py。
- [x] T009 [US2] 后端单决策/持久审计/原始截止与首次及复查接线，backend/src/Gaode.Application/Station01/TrayAnomalyDecisionService.cs、StartPublicPreparation.cs、Workflow/RecipeDetectionExecutor.Observation.cs及Host/Api/RunEndpoints.cs。
- [x] T010 [US2] 异常独立处置和最后原槽Pending共同搬运，backend/src/Gaode.Application/Ports/StagePortContracts.cs、Workflow/RecipeDetectionExecutor.cs、RecipeSortingMapper.cs、SortingTargetAllocator.cs、ThreeStageWorkflowExecutor.cs；保留CommitPick和MayAuthorizePlace。

## 用户场景 US3 提前结束/共享下料

目标：介入及有效空盘均无F/检测/分拣，实际下料/允许/人工确认/持久原因；未确认非终态。FR-008/009/P07/08。

- [x] T011 [US3] 固定两短链/持久重读/未确认门必要验证，backend/tests/Gaode.Integration.Tests/Station01/PublicTrayFlow016Tests.cs。
- [x] T012 [US3] 同执行器公共下料无配方输入，backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs及WholeTrayWorkflowOrchestrator.cs。
- [x] T013 [US3] 同完成模型/store原因及完整性、历史载荷读取，backend/src/Gaode.Domain/Station01/StageOperationContracts.cs、Application/Workflow/WholeTrayCompletionContracts.cs及Infrastructure/Persistence/WholeTrayCompletionStore.cs；必要迁移才登记StorePrep安装顺序。
- [x] T014 [US3] 实际结束接线/API/通知/运行及历史投影，backend/src/Gaode.Application/Station01/StartPublicPreparation.cs、RuntimeObservationProjection.cs、Domain/Station01/RunSnapshot.cs及Host/Api/QueryEndpoints.cs、RunEndpoints.cs。

## 用户场景 US4 复查/组员/特殊直接差异

目标：原检测事实保留、冻结不重绑、组员独立、半成品整体、014正常闭环和异常最后分拣。FR-006/007/010。

- [x] T015 [US4] 共同组员显式位置/唯一校验/序列化/冻结/保存及012映射，backend/src/Gaode.Application/Recipes/RecipeContracts.cs、RecipeDefinitionValidator.cs、RecipeDefinitionSerialization.cs、RecipeRunPlanner.cs及frontend/src/recipe-authoring.js。
- [x] T016 [US4] 必要复查/全部异常/组员/014差异验证及预算原截止保护，backend/tests/Gaode.Contracts.Tests/Workflow/PublicTrayAffected016Tests.cs、Application/Workflow/RecipeExecutionBudget.cs。

## 006独立页面增量及收尾

T010/T014/T017共同接口细化：SlotState与Movement消费独立检测状态/物理处置及实际分拣证据，PosePending不增加质量结果；历史未记录的InspectionCompleted保持未知。

- [x] T017 授权顶部按钮/独立公共弹窗/异常弹窗/必要状态，frontend/src/pages/a.html、runtime.js；需求归006/contracts/public-tray-flow-016.md，FR-011/P12。
- [x] T018 受影响构建/具名最小回归及当前有效009/010轻量门禁，scripts/verify-016-public-tray-flow.py及artifacts/016-public-tray-flow；SC-001—006，缺失/Skip/身份不符拒通过。
- [x] T019 真实API/SQLite/必要算法代表混合及介入/空盘短链、实际浏览器验证，scripts/verify-016-public-tray-flow.py；SC-002/003/006，不以组件代替完整链。
- [x] T020 清理失效分支/错误测试及扫描登记，backend/tests/Gaode.Rules.Tests/Architecture/009-public-shapes.json及scripts/workflow/010-lightweight-cases.json；FR-012，保留有效历史和提交门。
- [x] T021 收口/验证/同步/交接/摘要/增量恢复包，specs/016-public-preparation-tray-check-unload/implementation-report.md、verification-report.md、document-sync.md、merge-handoff.md、delivery-manifest.json及delivery-package；SC-007。

## 当前阶段范围与完成证据（P13）

正式API新run至人工确认后final；当前设备/采集/算法/配方/SQLite/UI参与，证据artifacts/016-public-tray-flow。组件与主链分别记录，场地资料缺失局部限制，013/全量/未来异常延期。

## 任务追溯与依赖

T001→T002/003→共享接口与实现。US1(T004—006)、US2(T007—010)、US3(T011—014)、US4(T015—016)按顺序实施；US3共用US1/US2真实观察。T017→T018→T019→T020/021。测试先定义必要结果，构建/动态串行；不以[P]授权代理实现。MVP先公共位置/短链，再混合和直接差异，所有当前必要项仍须交付。

FR-001/002/003→T004—006；FR-004/005/006/007→T007—010/016；FR-008/009→T011—014；FR-010→T015/016；FR-011→T002/017/019；FR-012→T002/003/020；FR-013→T001/021。共享保存/身份/权限/日志由各任务承担，软件结论独立登记。

## 客户确认原型检查（P12）

006既有三页和SHA保持，新增授权精确差异登记；不覆盖旧012增量、现场015/016功能或历史任务勾选。未实际完成不得勾选；若存在未提供资料，保留任务受限范围和最小剩余动作。
T008/T016包含初始与复查物理身份连续性校验及拒绝变映射的组件验证。
T010/T015/T016固定故障Pending与姿态Pending并存，以及两组各成员独立物理映射的普通阶段顺序。
T010/T016验证逐槽真实排除引用和复查Absent无移动，不以后续检测写入替代3D依据。

本轮T018/T020收口范围：六面正常行有限关联诊断及有证据的修复；G02/G07使用FixtureOnly受控正例和逐原因负例（不增加顶层G条目），与真实迁移义务审计分离。保留固定46项、原2000ms保存/取消/未知/回执门及所有原始登记；迁移义务按实际016直接与传递影响逐项核查，其他历史失配留存基线比较。仅两项及直接影响定向通过后执行固定完整入口。T016/T018/T020/T021在实际必要证据完成前保持未勾选。

直接影响验证登记：现有ConfiguredDetectionExecutionTests九项（含六面正常和取消）；RecipeBindingSaveProtectionTests五行/ReceiptContractTests一行（真实SQLite保存、迟到、取消与回执）；ThreeStageWorkflowExecutorTests现有25行（本轮共享执行器的迁移保护）；RecipeApplicationReceiptTests两行（真实提交后迟到不授权）。这些是原方法的定向补充，不改变固定46项，不扩展旧009全部运行集；G02/G07仍为原顶层自检，六个负例新增具体原因校验，不另登记为产品成功用例。

T018容量对齐实现：原Host公式原样提取至backend/src/Gaode.Infrastructure/Diagnostics/HostWorkerCapacity.cs::Ensure；backend/src/Gaode.Host/Program.cs和整个ConfiguredDetectionExecutionTests组件夹具共用，记录minimum=2→existing Host minimum=8的来源。保留原VirtualPlc链接策略文件，不加同名别名或兼容层。固定九项及保存/取消/回执定向验证，随后新固定入口；不新增顶层测试，不动原保存窗口或13参数。原FullSimulation迟到回执两行已核对复制基线也缺3D能力，只保留失败和原义务，不作为本轮保存保护成功证据。

## 2026-10-06续修与集成授权
本轮先从主项目接收已完成012导航、014调度完成/最初故障/稳定事件键及harness首失败保护；三方接收清单在artifacts/016-public-tray-flow/reception-20261006/manifest.json，原delivery-baseline不变。检查清单20/20仅审阅属性；T016/T018/T020/T021仍按实际验收与交付证据完成。副本Passed后自动备份并逐文件三方合并到E:/dzk/gaode-1，随后以主项目源码重新构建、专属隔离端口及schema /3测试库最小集成；默认库与生产库不自动升级。
固定原46项不删改；接收直接保护和当前迁移义务在supplemental-verification.json提前登记，单独统计，FixtureOnly只证明判定器正负例。运行条件沿014已有软件证据：DOTNET_ThreadPool_UseWindowsThreadPool=0、DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1、原runtimeconfig ServerGC；不批准生产默认，不改期限、轮询、重试或GC。每次失败按同次真实阶段定位，不原样循环择优。

T018受影响迁移准备：当前多组与程序集原义务仍要求实际Worker/SQLite/正式完整链，不能由四条016数量抵扣。新增五个受控输入（两组正常/单成员NG/四面、程序集E存在/缺失）沿现有harness及固定期限；旧review-recipe-catalog/0.5在复制前已不被当前JsonRecipeCatalog接受，原输入/登记仍保留。虚拟Worker仅补当前TrayPose角色，通过既有PNG内容判定器处理显式观察样本；既有虚拟检测目标输出及取消/保存保护不变，软件模拟不称生产算法。旧FullSimulation迟到回执用例及其缺失TrayPose配置/预算/能力与复制前原始字节相同；不在固定46或当前BoundaryMinimum/轻量登记中，保留为独立历史失败且不计负例通过，当前真实保存/迟到/取消组件仍必验。新增TrayPose角色只按实读PNG执行已有判定器，不注入既有虚拟检测角色的人工10秒等待；原角色10秒、期限、轮询和所有既有模拟时序不变，不成为生产默认。

T018多组同次真实执行发现：QueryEndpoints.ReadHierarchy读取DetectionUnitDecision.parts.faces时遗漏已持久StageId；CommittedResultProjection按stage身份加入真实Face后导致同一面双计。修复仅读取已有StageId并与融合投影使用同一Id，历史缺字段保持null；保留各Stage独立结果、原义务6/14面断言及完整重读。新增两个受控投影数据行（当前Stage与合法历史null）先登记，再实现；不删重复失败证据、不扩大期望数。

本轮最终必要集合登记：固定46保持，补充50行；新增程序集E算法失败及NG/Pending同存两条实际进程行，并纳入先前已声明的共享执行器原25行中未被固定46/014新行覆盖的19行。旧原始义务、失败及initialRegistrationIntegrity不改。完整进程集合实测27分41秒；外层命令监督等待由20分钟改60分钟，仅容许串行收集既有10秒虚拟调用结果，所有产品动作/PLC/I/O/保存窗口和冻结预算原样保留。

最终登记99项C#（固定46+必要补充53）、18项前端组件；补充范围为7条自动多组/程序集迁移进程行、1条接收014真实双特殊件全链、2条阶段身份、必要共享执行器/保存/回执/传输/旧解锁禁止及当前协议保护。迁移职责见migration-affected-map.json；016-migration-impact.py只以本次真实TRX和已验证当前009入口核对31项活动义务，FixtureOnly不参与，全部旧原义务与历史失配单独保留且不称全量历史迁移通过。

接收014真实链预检在启动前被原012页面观察器所属目录拒绝（012PageEvidenceRootRequired），不代表链通过。保留原页面观察职责和实际Edge观察，现有harness及014只读观察器增加严格016所属根的显式入口；原014/012/013入口不变，不混入其他专项环境，不跳过页面观察，不改变保存/动作期限。新专项在业务启动前预热实际浏览器，管道传受控令牌，观察同一run最终状态并核对退出；非业务特权。

同次固定入口收口补充：integrated-copy-acceptance-2实际99项C#/18前端、009127/01073已通过，源码/全部输入及实际构建在审计拒绝时与入口冻结完全相同；Rejected和原报告不改。审计表31条中T39/T44各把原方法名与已登记真实承接重复列为义务，定向合并为29项独立原义务；原193/245/53及initialRegistrationIntegrity不变。同一验收尝试允许仅修正该映射及验证说明后继续末尾检查：全部产品/测试/Worker/UI代码、运行输入及实际构建必须逐文件同摘要；只准明确列出的审计映射/合同计划说明和交付验收引用变化，任何其他变化拒绝。逐个重解析原99TRX、18组件日志、实际浏览器/链证明，保留原执行身份和原Rejected，不跨轮次拼接或冒称重新执行；当前009/010及实际审计仍重新执行，冻结当前身份。该同次收口不改010跨轮次FullRun复用禁止规则。

主项目同次末尾收口登记（2026-10-06）：主项目原有scripts/Inspect-Disk-Capacity.ps1在合并前、正式入口冻结及当前均为SHA256 67671cfc59faf5e3b9c23fe81b7312c369915805b1d3ab775057c8f3914ca914；仅缺009分类导致010 SCRIPT-Repository/A10拒绝。按真实tooling职责补登记scanRequired=true/exemption=false，脚本原字节保留且不执行。允许本次同一入口仅该分类和本016合同/计划/任务说明变化后继续末尾核验；99项原生TRX/18组件报告逐份完整重解析，产品/测试/Worker/UI/输入/构建均原摘要，原Rejected不改。当前009/010与29活动迁移义务实际重验；不跨轮次复用FullRun、不取消历史义务、不新增豁免。副本软件密封保持原身份，主项目独立密封记录该既有工具分类差异。

最终收口（2026-10-06）：副本同次末尾收口Passed及原Rejected均保留；主项目integrated-main-acceptance-1实际99/99 C#、18/18前端、009 BoundaryMinimum 127/127、010轻量73/73和29项活动迁移义务通过。独立历史11项及旧FullSimulation两条提前NotConfigured失败保留，不计通过。T016/T018/T020/T021据此完成；最终主项目集成报告、精确恢复清单与密封见E:/dzk-delivery/016-integration-20261006/main-integration-report.md及final-evidence-index.json。仅专属隔离Test库真实/2→/3升级，不升级默认开发或生产库。
