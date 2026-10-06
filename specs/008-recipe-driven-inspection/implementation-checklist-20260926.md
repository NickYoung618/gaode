# 008后续实施清单：20260925分区协议

**2026-09-26业务确认增量**：以[USR-20260926-C：本次用户业务确认](business-decisions-20260926.md)为本次已确认规则；下方带日期的实施状态为历史快照，实际进度见运行证据。本次仅同步文档，不变更任务编号、勾选、代码或既有运行结果。

日期：2026-09-26。文档目标已对齐，代码/测试/运行配置/fixture/生成器仍为原实现，本轮未执行implement、构建、服务、业务测试或数据库操作。

依据：[execution/3.0](contracts/execution.md)、[目标映射0.5](contracts/test-virtual-mapping.md)、[数据模型](data-model.md)、[任务](tasks.md)。协议原件SHA256：`405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`。协议目标软件身份`plc-upper-20260925-partitioned-ack`为待实施值；当前代码仍`plc-upper-20260921-hex1-f32`。名称XY_Move_Cmd/XY_Pos_Confirmed和原地址保持；Float32字节序/一基地址是待真机校准的工程配置，不宣称已确认。

## 第一片：共享协议、自动翻面、下料

| 唯一任务 | 实际消费者与当前行为 | 具体改动与前置 | 最少验证/停止点 |
| --- | --- | --- | --- |
| specs/003-plc-latest-protocol T071 | `backend/src/Gaode.Infrastructure/Devices/Plc/ProtocolLatestMap.cs`、`VirtualPlc/PlcAddressMap.cs`缺ACK；`backend/src/Gaode.Application/Ports/DeviceMessages.cs`、`LatestProtocolPlcDevice.cs`读取Flip状态/面 | 同批增加0054 Sorting_OK、0055 Flip_OK，PC→PLC Int16，预留0056；协议版本及原件摘要贯穿观察、适配器证据URI/日志 | 映射方向/类型一致，保留真实反馈；不只改两个常量 |
| specs/003-plc-latest-protocol T071 | `VirtualPlc/VirtualPlcEngine.cs`原清目标面复位；正式Flip仍受限 | 当前实体flipPosition→0003/0005/000B→cmd3→本次到位及实际XYZ→cmd0→目标面→status2且面匹配→Flip_OK1→status0→ACK0；Host操作ID及连接代次关联。无需新地址/序号；数字配置由008 T050 | 两个不同实体连续同目标面各自完整握手；错面或ACK超时不进下一面；已完成成员不重复/整体共享一次 |
| specs/003-plc-latest-protocol T069 | `LatestProtocolStageActionAdapter.cs`当前Unload写0007、前置Z已等目标；VirtualPlc cmd4只动XY | 下料改写000B，模拟器cmd3/4使用GrabTargetZ及实际XYZ；保留前次必要复位/安全，移除旧XY-only假设；本次到位及XYZ后cmd0 | 正常新目标下料和到位不符/未知不得完成；不能拿旧T064通过抵新规则 |
| specs/003-plc-latest-protocol T069 | `backend/src/Gaode.Application/Workflow/ThreeStageWorkflowExecutor.cs`当前Detection→Sorting→Unload，恢复排序同样旧；`RecipeExecutionBudget.cs`同顺序 | 普通链改Detection→UnloadPreparation→适用Sorting；stageStarted/current stage、事件/恢复排序与绝对deadline同步。普通OK保留无搬运依据，不等待全部分拣 | 下料到位仍不可取盘；动作预算不重置，未知物理状态不重放 |
| specs/003-plc-latest-protocol T069 | `WholeTrayWorkflowOrchestrator.cs`硬编码旧协议；`backend/src/Gaode.Infrastructure/Persistence/WholeTrayCompletionStore.cs`按三阶段来源索引汇总 | 来源取真实新版动作事实；全部必检/适用分拣提交、无未知在途/必要保存后WholeTray，再解锁反馈、页面确认、Final。校核索引与事件ID对应 | 缺保存/未解锁不Final，矩阵协议版本/来源一致；复用SQLite和现有事务 |
| specs/003-plc-latest-protocol T067/T070 | `FScanStep.cs`→`LatestProtocolPlcDevice.cs`已F命令5/ScanZ/3→4→复位→0；检测1/2已接 | 复用并核验改动影响的轴归属及协议版本，操作结束/复位/解码/绑定/保存分开 | F失败不因复位放行，检测复位不串扫码/抓取轴；E不套F |

第一片只要求上述Q03所用能力及必要验证交付，不要求T071分拣/旋转全项完成。安全、目标配置、反馈和必要保存仍是实际派发门禁。

## 第二片：合法多面目标、计划、保存及预算

| 唯一任务 | 调用/消费事实 | 实施内容 | 必要验证 |
| --- | --- | --- | --- |
| specs/002-plc-xyz-recipes T11 | `RecipeRunPlanner.cs`按epoch推HeightRound，每翻面插Rescan；`JsonRecipeCatalog.cs`要求rescanWholeTray3D/newCoordinateEpoch且多面固定Restricted | `RecipeContracts.cs`及实际`backend/src/Gaode.Infrastructure/Recipes/catalogs/recipe-catalog-review.json`同步目标0.5；面推进独立，不生成重扫。替换旧双字段受限理由为实际未实现能力/数值缺失；不添加兼容旁路 | Q03无Rescan，缺某面目标/来源受限，合法已实现多面Available；不只编辑测试fixture |
| specs/001-station01-public-preparation T090 | `StartPublicPreparation.cs`冻结计划/预算；`PublicPreparationHandoffV2.cs::ResolveTargetsForRound`轮2强制新call | 移交保存实际初始公共3D/F结果、对象与冻结配置；按阶段/面解析，允许有明确逐面映射的初始测量引用；`StagePortContracts.cs::DetectionStepTarget`同步 | 面2引用初始sample合法但独立XY/Z依据；错scope/sample/面/对象拒绝，保存未知不得续接 |
| specs/008-recipe-driven-inspection T052/T060 | `RecipeExecutionCoordinator.cs`依Rescan才清flipPending；`IntegratedDetectionPort.cs`正式Flip固定受限，PostFlip组件只支持旧Q03轮2 | 完整Flip/ACK后在原Detection循环推进执行阶段/实际面，解析该面目标，保留同盘/对象/F/历史结果；普通新链不走ExecutePostFlipComponentAsync，不伪造第二次3D。定位/逐图分析/复位/融合复用 | 首面→真实Flip→次面完整续接；面号、step、目标、measurement/config/connection身份持续校验 |
| specs/008-recipe-driven-inspection T050 | `fixtures/recipes.json`、`recipes-q02.json`、`recipes-q03.json`、`recipes-q01-param.json`及三个generate-*-test*.py | 同批升级生成器与四组fixture/media-manifest/worker-manifest/cases；每面独立point/version/XY，Z显式InitialMeasurementOffset或ApprovedFixed，flipPosition带Test来源；摘要重算由未来实现执行 | 同一初始sample不等于同一XYZ，缺面映射不派发；旧生成器不得直接覆盖新设计，历史运行包不改 |
| specs/008-recipe-driven-inspection T051 | `RecipeWorkload.cs`/`RecipeExecutionBudget.cs`仍计HeightRescanCount，未独立计翻面ACK | 公共3D/F一次；每面C=2、F=1、A=C+F，Q03 C4/F2/A6、四面C8/F4/A12；删除翻后重扫约15/45秒基数，加入逐实体定位/翻面/ACK、分拣取/放/ACK、保存及有限尝试；期限按新阶段冻结 | 单面/双面/四面和多槽计数；采集3—5秒、worker10秒现有Test约定不缩短；3秒心跳不放宽 |
| specs/008-recipe-driven-inspection T053/T054 | `FaceResultAggregator.cs`、`TraceWriter.cs`、`StageEventStore.cs`及媒体保存 | 输入按run/object/stage/face/camera关联；实际measurementRef及coordinateConfigVersion独立，deviceConnectionEpoch只反馈关联；保存意图、实际反馈、ACK清零与目标来源；不生成二次3D媒体 | 两面AB不串图/融合；必要保存Failed/CommitUnknown不得下一运动或Final；真实SQLite/媒体读回 |

## 第三片：查询、正式页面Q03到Final

| 唯一任务 | 文件与动作 | 前置与完成条件 |
| --- | --- | --- |
| specs/003-plc-latest-protocol T068 | `backend/src/Gaode.Host/Api/RunEndpoints.cs`、`RunMediaCatalog.cs`、现有通知/状态投影 | 已提交新事实；阶段、媒体面/实际测量来源、目标版本、协议身份可查；RescanMediaCommitted仅旧记录含义，不回填新记录；allowedActions在可靠解锁后才允许取盘 |
| specs/006-frontend-station01-console T048/T049 | 实际`frontend/src/runtime.js`及`frontend/scripts/build.mjs`复制入口，既有`frontend/src/pages/a.html`、`data-view.html`绑定 | 查询所需子能力可用即接，不等所有后端任务；原型ZIP只读，既有页面绑定新阶段、面/来源、媒体及Final，面2不标假测量轮2；不只改未打包TS |
| specs/007-station01-integrated-loop T031新版子范围/T033 | `scripts/start-station01-virtual-loop.ps1`、`capture-station01-webview2-normal.cjs`、`verify-q01-q02-test-page.ps1`、`summarize-q01-q02-evidence.py` | 新fixture/生成器/schema、Host/VirtualPlc协议身份/原件SHA及配置摘要一致；保留实际页面点击，禁止后台代启动/代取盘；PrepareOnly只是准备，不计Passed |
| specs/008-recipe-driven-inspection T062 | 新版Q03普通OK同run从正式WPF选用/启动，经公共3D/F、真实Flip、两面AB/融合/保存、下料定位、无搬运依据、解锁/页面取盘至Final | 首个新版整链停止检查点：实际只有一次公共3D，Flip完整清零，面2来源合法，SQLite/媒体/来源矩阵/页面一致。失败保留包并修具体阻断，不跳过。通过后继续其余适用自动无E两/四面 |
| specs/008-recipe-driven-inspection T055/T056/T058 | 新版Q01/Q02/PARAM受影响回归 | 复用未变旧能力，补本次下料/身份/版本/保存影响；PARAM仍在同一构建证明真实参数差异。旧2/22和C08不自动迁移新协议 |

页面Q03不依赖全部T057/T059、E、旋转或组策略，但依赖真实安全、每面有效目标、核心调用及必要保存。若当前页面不存在某人工控件，仅登记该人工路线缺口，不以扩页替代授权。

## 并行衔接与最终保留范围

- specs/003-plc-latest-protocol T071同盘分拣适配：`LatestProtocolStageActionAdapter.cs`原PartIndex→cmd1→status2完成须改为源XYZ/取料1→status2→cmd0→真实源槽号+目标XYZ/放料2→status3→cmd0+Sorting_OK1→status0→ACK0。VirtualPlc引擎及`VirtualPlc/wwwroot/app.js`枚举同批改为4失败/5满盘/命令3满盘报警；此监控不是客户原型。
- specs/008-recipe-driven-inspection T057/T059：`RecipeSortingMapper.cs`保留OK过滤；`ThreeStageWorkflowExecutor.cs`移除Sequence充槽号，配置真实源/目标与预留，status2只在途，完成ACK及保存后目标占用。验证非连续源槽、NG/Pending各一路、放料失败/满位无错误完成。混合NG/Pending按本次U02有NG优先，NG面与未判定面分别记录。
- specs/008-recipe-driven-inspection T061/T063—T069继续保留E、组、整体、旋转三出口及必要恢复；specs/003-plc-latest-protocol T072和specs/006-frontend-station01-console T050/T051按具体人工/恢复子输入接入。人工占用禁动/确认清零已定义，人工采用命令目标面并标明默认来源；故障双端复位且初始成立后显式新run完整重跑。
- specs/008-recipe-driven-inspection T070最终核对Q01—Q22、C01—C08及必要F。特殊旋转按原图实际进出站/三出口，不机械套普通盘末顺序；新版ACK不解决旋转角度、占用或组策略。

## 必要测试调整位置（后续实施时执行，本轮未运行）

复用`backend/tests/Gaode.Contracts.Tests/Recipes/RecipeRunPlannerTests.cs`、`PublicPreparationTargetResolutionTests.cs`、目录/Coordinator测试、`Station01/PublicPreparationHandoffV2Tests.cs`及对应IntegrationTests；调整强制Rescan/face=round断言。设备用LatestPlcProtocolTests、VirtualPlcLatestProtocolTests、PlcStageActionPortContractTests及FlipFeedbackCorrelationTests验证地址/双握手/新下料。结束链用ThreeStageWorkflowExecutorTests、WholeTrayWorkflowOrchestratorTests、FinalUnloadCompletionIntegrationTests、StageAndCompletionTransactionTests、ComponentSourceMatrixStoreTests核对阶段与保存门禁。媒体用`backend/tests/Gaode.Integration.Tests/Api/RunMediaCatalogTests.cs`及frontend既有runtime测试核对新身份/allowedActions。

最小必要失败：错面/旧完成或ACK超时不得次面；缺合法目标/错来源不得运动；状态2不得整项分拣完成；放料失败/满位保留在途；必要保存失败/未解锁不得Final；3秒断联停派发。每类按一个适用代表并复用同版本有效证据，不做22路线×全部故障。

所有关键位置保留分级分类结构化日志：command acceptance、意图/派发、阶段变化、目标/实际、反馈/ACK清零、必要保存、阻断/超时/失败，携requestId/runId/recipeVersion/stepId/operationId/entity/face/deviceConnectionEpoch。退出后可持久查询，重复轮询限频；必要失败须能定位具体阶段和动作。

## 下一条实施提示词

> 在E:/dzk/gaode-1使用现有speckit-implement，先读取AGENTS.md、008的spec/plan/tasks及implementation-checklist-20260926.md、protocol-alignment-20260926.md和static-validation-20260926.md。按当前分区协议及SHA实施008直接子范围，保留旧勾选/历史证据。先共享协议身份、真实自动Flip/ACK和000B下料/新结束顺序，再合法逐面目标、初始测量身份、预算及页面查询/夹具；首个目标为新版普通OK Q03正式WPF同run到Final，翻后不重采3D。复用现有AB/CD、独立worker、媒体、SQLite及宿主，不扩页、不改原型。普通OK不等待全部分拣/E/旋转/组策略，但保留最终范围。执行必要主流程与直接失败验证；在Q03完整检查点报告证据和剩余范围，未满足时只修具体阻断，不伪造成功或勾未完成任务。
