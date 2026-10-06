# 技术方案：公共准备、3D穴位检查与公共下料

交接完成校验允许Identity.OccupiedSlots为空；实际占用沿已提交3D和F后冻结计划校验，其他交接提交与摘要门保持。此项归T006/T010启动与交接增量。

投影实现增量：用实际SortingAssignment记录关联格位和独立Pending搬运，保留动作与取料提交校验；运行、历史和006同时消费DetectionState/PhysicalDisposition，禁止为复查退出对象补总质量结论。

功能specs/016-public-preparation-tray-check-unload；2026-10-06；[规格](spec.md)；宪章9.0.0及本次最新授权。

## 方案摘要

沿现有StartPublicPreparation→ThreeD/F→RecipeDetectionExecutor→ThreeStageWorkflowExecutor→WholeTrayWorkflowOrchestrator及SQLite，不建第二执行器。共享下料输入脱离DetectionRequest/RecipeRunPlan，使未F短链合法消费同一动作与保存条件。异常观察通过同一后端决策服务，后端截止及一次持久结果，不由UI推进。

| P13边界 | 方案 |
| --- | --- |
| 起止 | 正式启动API至本盘人工确认及真实最终提交 |
| 组件 | 当前语义设备、采集、Python Worker、冻结配方、Trace/StageEvent/WholeTray store、Host/API/通知及006页面 |
| 验证 | spec SC-001—007、quickstart；构建/动态串行 |
| 证据 | 当前源码/程序集/输入摘要及同run记录，组件与主链分列 |
| 延期 | 未确认正式映射/机械/可靠实测只限制现场；不做013/全量/未来治理 |

## 技术上下文（Technical Context）

本轮T018/T020仅补齐保存时序观测与G02/G07受控判定器自检。实际迁移审计仍独立运行、保留原登记。六面final-acceptance-3的writeId d1a96d51-ff5d-4bc2-8b4e-159e979279ba只到DatabaseCommit Started；事务是否执行未知，不沿用其他轮次归因。固定入口在两个阻断项及直接影响定向验证通过后运行。

T018环境差异已核对：直接组件minimumWorkers=2，现有正式Host相同processorCount=2时effectiveMin=8；Host已有策略明确用于避免启动/SQLite阻塞造成续接排队。仅提取原Host公式到Infrastructure.Diagnostics.HostWorkerCapacity.Ensure并供组件调用，不改变任何产品参数/WindowsNative选择；原ThreadPoolRuntimePolicy由VirtualPlc链接，保持原文件以避免两个程序集同名类型。组件仍用原消费者、真实SQLite、2000ms及所有保存保护。关联诊断显示closure-acceptance-1写入排队约2.97秒后才消费并在事务前ConditionRejected；原d1写入数据库中不存在，原具体迟延阶段仍不能倒推。现有同步收集器测量未捕获>100ms回调，不认定其为根因。

沿当前.NET SDK10.0.401/C#、ASP.NET Core、EF Core/SQLite、Python NDJSON Worker和现有HTML/runtime.js。既有配置schema、锁文件、StorePrep迁移、MotionCoordinator、资源lease及取料提交门是事实，不重新选型。当前Production可靠坐标缺映射，示教返回不可用；具名Test/Virtual输入只证明软件。

## 宪章检查（Constitution Check）

| 原则 | 设计前→设计后 | 落点 |
| --- | --- | --- |
| P01 | 冲突待修正→按最新授权定向同步 | document-sync.md |
| P02/P05 | 符合→符合 | 同端口/Host，协议留Infra，不新增执行器 |
| P03 | 异常原槽旧规则待修正→符合新规则 | contracts/public-tray-flow.md |
| P04/P06 | 符合→符合 | 控制闭锁/未知在途保护，原lease/媒体生命周期保留 |
| P07 | F前及组员身份待补→显式身份/覆盖和独立组员 | data-model.md |
| P08 | 提前结束模型不支持→同store扩展 | 新载荷版本，StorePrep受控迁移 |
| P09 | 需新事件→复用RuntimeDiagnostics及真实持久事件 | 命令/选择/下料/结束/失败 |
| P10 | 待现场输入→仅限制依赖能力 | spec待补充与依赖 |
| P11 | 符合→符合 | 唯一共同配方模型/校验/冻结 |
| P12 | 原型增量需授权→用户已明确授权006增量 | 006 public-tray-flow-016合同 |
| P13 | 当前主链未通过→设计覆盖，无预写验证通过 | verification-report.md |

P01/P03/P07/P08的旧行为须在实现前按合同更新；设计检查通过不等于运行通过。

## 结构与职责（Project Structure）

- Domain：PublicConfiguration两位置单一来源；TrayObservation稳定格位/完整覆盖；StageOperationContracts结束原因与独立检测完整性；RunSnapshot新增决策/结束显示。
- Application：PublicPositionTeaching读语义实测/保存；TrayAnomalyDecisionService单决策；StartPublicPreparation首次分支；同RecipeDetectionExecutor复查；RecipeSortingMapper/Allocator异常真实处置；ThreeStage共享下料；WholeTray同准备/允许/确认。
- Infrastructure：ConfigurationLoader完整文件受控替换；PythonWorkerAdapter明确观察；同SQLite准备/结束载荷及必要受控迁移；历史读取保持。
- Host：装配新语义能力；Config示教/保存与决策API；现有人工确认、GET/通知/历史投影。
- Frontend：006指定生产页顶部右上按钮、独立公共位置/异常弹窗及必要状态，保留012导航。

## 数据、契约与状态

见[data-model](data-model.md)、[共同合同](contracts/public-tray-flow.md)。公共reference不引入第二活动坐标源，完整公共JSON原子替换、digest及point version标修订；运行Freeze仍深拷贝固定正文。异常处置与检测Objects分开，不造质量结果；早结束无检测/分拣引用。PlanRevision旧列在提前结束明确承载公共执行revision，RecipePlanRevision保持空。新字段与载荷有明确版本，旧记录读取不升格为当前许可。

## 配方共用逻辑与动作隔离

共同RecipeMember增加显式CellId/PhysicalSlotIndex，散件成员独立，半成品整体不拆；唯一validator/serialization/planner/freeze/save/API/UI映射一起更新。普通正常节奏保持；特殊正常逐件闭环，异常不进入工位，最后原槽Pending。只有实际工位来源才要求RotationLoadingCompletion。

## 并发、资源与异常出口

每run/observation一个10秒选择，锁内争取唯一结果、持久结果后才继续；停止/取消/故障永久关闭本次决策，不恢复超时。初次窗口在F前，检测期限未开始；复查窗口使用同已冻结阶段截止，不能重新Freeze。既有动作/I/O/保存期限不扩大；实际可执行预算必须在合同核对工作量，若10秒及Pending工作超出原公式，先登记公式增量并一次冻结，禁止临时延长截止。

## 保存与恢复

公共配置同reference完整文件替换＋If-Match，保持其他配置；失败不成功。观察/决策/搬运/下料/允许/确认分别真实提交；未知动作不重发，取料提交门保持。准备记录按正常/介入/空盘验证真实必要事实。确认及final同事务，确认后才释放lease。schema/迁移通过StorePrep，本副本空库验证，不改主库。

## 软件验证与证据计划

SC-001配置API实际保存/重启/冻结；SC-002混合代表链；SC-003介入与空盘短链；SC-004一次决策/控制闭锁/覆盖/未确认门；SC-005复查/组员/特殊直接差异；SC-006真实浏览器和当前009/010轻量门禁；SC-007摘要包及三方合并。有限测试由需求独立定义，缺失/Skip/身份不符不通过。采集/算法替身声明来源，经正式API/端口实际调用并写SQLite。

## OPEN、外部依赖与决策记录

研究结论见research.md。正式映射及真实位置轴含义缺失不填坐标/公式；当前设备夹紧就绪已由用户确认，不能推导松夹。合法取盘沿现有Host基于真实下料及无未知在途规则，明确deviceUnlockClaimed=false。现场取盘机械许可仍待正式合同。

## 客户确认原型检查（P12）

006既有原型清单及ZIP SHA保持，新增授权增量单独登记；仅生产页顶部与弹窗/必要状态。Frontend/宿主只经API/通知，不读设备/库。实际页面验证后才能评价。

组员修订落在共同配方模型/验证/冻结/计划/012编辑器，配方写入版本5、旧4只读；不可把父槽号填入所有成员。
ObserveAfterPlacementAsync对初始观察与复查观察逐物理槽身份比对，复用完整覆盖门；T008/T016覆盖此校验。
T010保留技术故障Pending转换中的已有PosePending；T015组按最前成员实际格位确定组顺序，阶段和材料次序不变。
T010/T016在检测器保留逐槽排除依据；SlotParticipation对完整观察明确Absent优先跳过，PoseExcluded只对仍Present保持检测退出。

T018保存窗口诊断增量（历史对照，不归因原d1）：其他轮次曾捕获真实事务提交及回执发布后调用方未在原2秒窗口恢复；原final-acceptance-3 d1只有DatabaseCommit Started且无提交行，不能推定迟延阶段；运行时条件、后台上下文、直接回执及竞争包装对照均未消除此迟延，未采纳为产品修改。原保存窗口、取消、提交未知与对账规则保持。固定用例保留，失败阻止可合并判定；继续其他独立必要验证，不原样循环择优，不扩展013性能治理。

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
