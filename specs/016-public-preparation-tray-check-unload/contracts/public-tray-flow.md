# public-tray-flow/1 — 实现前共同合同

2026-10-06；消费FR-001—013，替代冲突活动规则；历史证据不变。

## 公共配置与示教

IPublicConfiguration增加保存两公共位置能力，完整正文同唯一SourceFile原子替换；If-Match使用原digest，409拒并发。固定config reference不改，点位version及canonical digest为保存修订，运行按Freeze冻结；保存不改F/limits/bindings。示教读取IPlcStatePort语义可靠本次位置：3D DetectionZ、上下料GrabZ，轴/frame/unit/epoch/可靠来源不齐拒绝，不用目标值补齐。Production只接受真实可靠实测；Test受控Virtual明确来源。

GET /api/station01/configuration/public-positions：完整两点及digest；POST同路径按配置权限保存；GET .../current?kind=ThreeD|ManualLoading 返回实测与来源或明确不可用。保存及读取沿现有认证/错误合同，不扩生产权限。

## 观察、映射与决策

当前tray-observation/2显式完整槽集合和来源，每槽physical/CellId/区域/行列；结构有效与覆盖Complete分别校验。只Complete无Unknown且全部Absent为空盘；F定位不是提前结束前提。有料必须匹配冻结配方，额外空位可保留；不得因空位编缺件。

每run/observation仅一DecisionId，创建时冻结10秒deadline。GET运行/通知返回原截止；POST /api/station01/runs/{runId}/tray-anomaly-decision 提交decisionId+Continue|ManualIntervention。服务锁内仲裁一次，记录operator/source/时间并持久后返回；失效/重复不得执行第二分支，超时后端Continue。Stop/Cancel/Fault关闭并持久，不自动恢复，UI只显示提交。初次决策在F前；复查同服务且保持冻结输入与已有证据。

## 检测、Pending及期限

Objects不伪造质量结果。独立异常处置保留Observation及已有检测证据，正常检测/处置后最后Pending。复用SortingTargetAllocator、CommitPick、MayAuthorizePlace、真实MaterialTransferred与占用提交，原槽取件/配方Pending点；无合法目标/容量/抓手/反馈拒绝。特殊异常不进入工位，不要求未发生RotationLoading；正常工位件保既有条件。

10秒选择独立于原业务子期限；任何窗口不得改原I/O/动作/保存/取消上限。复查沿原整盘冻结deadline，不能重Freeze。需求实际新增工作量须先更新版本化预算公式、按确定次数一次累计，不能运行时无限延长。

## 共同下料及结束

公共Unload请求只含真实run/tray/station/line/session/epoch/publicSnapshot/执行revision/目标/tolerance/purpose/window，不要求配方或伪造DetectionRequest。正常与两短链调用同ThreeStage动作函数，意图提交→设备反馈和实际到位→保存Completed。既有无未知在途、ownership及控制门保持。

同WholeTrayCompletion store按EndReason检查：正常必需产品检测/分拣/下料事实；介入/空盘必需本次已提交完整3D观察、介入决策或全Absent依据、下料事实，检测/分拣引用空。使用真实3D capture/algorithm与Unload origin构建原组件矩阵。新tray-end/2 payload明确原因、InspectionCompleted、可空配方revision；旧载荷维持原事实读取。必要DB列迁移通过StorePrep，历史字段不伪造。

通信证据表及载荷不变，CommunicationEvidenceStore/Reader对已知s01-store/2和/3保持同一StoreId、前置引用、摘要和提交门校验；Host整体流程准入仍只接受已完整验证的/3。不得因升级而关闭就绪、取料或历史原始证据持久化。

人工取盘允许沿持久准备+本次Unload事件/epoch/无未知在途，明确deviceUnlockClaimed=false。本次夹紧Ready不推导松夹信号。人工确认与final/run终态同事务；未确认不终态，原因来自准备冻结，提交后释放运动lease。下一盘新start/new run，无多余上料移动。

## 前端追溯

运行与历史增量：SlotStateProjection增加DetectionState、PhysicalDisposition及SortingEvidenceRef；RunMovementProjection增加IsPosePending、DetectionState、PhysicalDisposition、Reason。处置来自真实预留/取料/放置持久事实，不创建检测结果行。复查退出的对象不新增总质量结论；已提交图像/融合事实保留。历史缺失InspectionCompleted为null/未知，不补造完整检测。

页面权责在006/contracts/public-tray-flow-016.md，012仅成员身份与配置边界映射；新016不承载独立页面规格。

预算确定增量：复查窗口次数=冻结plan的HeightRescanCount，每次10000ms一次累计于Detection预算；不增加I/O/动作/保存上限，不在复查时延长deadline。异常Pending已在SortUnit次数内。SQLite Detection/Sorting事件FK改nullable，新schema受控安装，旧非空FK原值保留；原因放版本化payload，不造空事件。

## 组成员共享接口修订
新写入配方为recipe-definition/5；recipe-definition/4仅保留历史读取。looseGroup每个RecipeMember显式填写cellId及physicalSlotIndex，必须与OK格位和映射一致且成员之间唯一；组父位置是业务分组，不代表每个成员共享一个物理穴位。assembledEntity成员检测仍采用整体原槽。冻结计划每个成员步骤携带其实际物理槽号，F匹配核对3D返回身份。012编辑器显示独立成员映射字段，空值阻止执行，不从序号生成槽号。
复查必须保留初始3D物理集合及各physicalSlotIndex对应的cellId/region/row/column，允许存在/姿态变化，不允许以变化的物理映射覆盖原身份。
组顺序按其成员最前的已映射OK格位排序，成员仍按既定材料/阶段顺序；超时Pending转换保留已有PosePending独立处置事实，正常故障Pending不替换它。
每个姿态排除槽保留首次导致排除的实际ObservationId及其已提交AlgorithmFact引用，不能以之后的图像写入或其他槽的观察替代。复查明确Absent时取消该槽全部后续动作；再次Present正常不恢复已终止检测。

保存回执仍以真实事务、关联WriteId/RunId及原绝对保存窗口为准。迟到、未知、未提交不得授权下一动作，TraceWriter回执、原对账与有限队列规则保持；诊断上的已提交不替代窗口内实际接收。

本轮T018诊断按WriteId关联队列消费、提交任务调度/实际执行、SQLite事务开始/提交调用/提交返回、任务返回、回执发布、调用方观察及原截止检查。诊断不修改2000ms窗口或保存结果；组件失败后保留有限尾部，区分故障瞬间与writer排空后的事实。

T018组件执行环境对齐：Host现有ThreadPoolRuntimePolicy为Managed池使用min(maximum,max(previous,8,processorCount+4))，WindowsNative保持原值；并非016新增容量参数。ConfiguredDetectionExecutionTests原先绕过Host启动，在同机minimum=2，而正式Host日志effectiveMin=8。将原公式原样提取为Infrastructure.Diagnostics.HostWorkerCapacity.Ensure，由Host启动和整个组件夹具调用；返回仅框架容量前/后值及WindowsNative选择，记录来源。原ThreadPoolRuntimePolicy被VirtualPlc链接编译，保持原文件/原策略，避免测试同时引用两程序集同名类型。原Task.Run消费者/真实SQLite/取消/CommitUnknown/回执与2000ms门均不改；不按六面或成功/失败条件分支，不改GC/优先级，不扩大期限，不宣称历史d1写入已提交。容量对齐的定向证据通过后再跑固定入口。

本轮T018/T020将G02/G07判定器受控自检与当前项目迁移义务审计分开。受控输入明确FixtureOnly，独立构造符合判定器固定登记结构的来源、映射及模拟执行行，调用同一migration_audit.audit；先要求完整正例通过，再验证缺行、初始义务改变、过滤、Skip、旧执行身份和来源变化的对应拒绝原因。该证据只证明判定器。真实009登记、initialRegistrationIntegrity及真实迁移审计保持；016直接与传递影响义务须逐项核实承接/断言/实际运行，其他历史失配须保留并比较复制基线与本轮变化，不凭自检结果转出活动保护。

## 2026-10-06续修与集成授权
本轮先从主项目接收已完成012导航、014调度完成/最初故障/稳定事件键及harness首失败保护；三方接收清单在artifacts/016-public-tray-flow/reception-20261006/manifest.json，原delivery-baseline不变。检查清单20/20仅审阅属性；T016/T018/T020/T021仍按实际验收与交付证据完成。副本Passed后自动备份并逐文件三方合并到E:/dzk/gaode-1，随后以主项目源码重新构建、专属隔离端口及schema /3测试库最小集成；默认库与生产库不自动升级。
固定原46项不删改；接收直接保护和当前迁移义务在supplemental-verification.json提前登记，单独统计，FixtureOnly只证明判定器正负例。运行条件沿014已有软件证据：DOTNET_ThreadPool_UseWindowsThreadPool=0、DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1、原runtimeconfig ServerGC；不批准生产默认，不改期限、轮询、重试或GC。每次失败按同次真实阶段定位，不原样循环择优。

T018受影响迁移准备：当前多组与程序集原义务仍要求实际Worker/SQLite/正式完整链，不能由四条016数量抵扣。新增五个受控输入（两组正常/单成员NG/四面、程序集E存在/缺失）沿现有harness及固定期限；旧review-recipe-catalog/0.5在复制前已不被当前JsonRecipeCatalog接受，原输入/登记仍保留。虚拟Worker仅补当前TrayPose角色，通过既有PNG内容判定器处理显式观察样本；既有虚拟检测目标输出及取消/保存保护不变，软件模拟不称生产算法。旧FullSimulation迟到回执用例及其缺失TrayPose配置/预算/能力与复制前原始字节相同；不在固定46或当前BoundaryMinimum/轻量登记中，保留为独立历史失败且不计负例通过，当前真实保存/迟到/取消组件仍必验。新增TrayPose角色只按实读PNG执行已有判定器，不注入既有虚拟检测角色的人工10秒等待；原角色10秒、期限、轮询和所有既有模拟时序不变，不成为生产默认。

迁移完整链预检group-normal-1在检测开始后原120秒默认窗口正确拒绝：新夹具误用了不带ExpectedRecipeRef的公共准备context/1。原T44完整配方义务使用context/2及正式目录身份；夹具改为先实际GET目录，提交真实RecipeId/Version/CatalogDigest和声明占位，从F匹配后原有RecipeExecutionBudget一次冻结开始。无产品预算/公式/期限修改；保留该失败，不以延长默认120秒修复。

T018多组同次真实执行发现：QueryEndpoints.ReadHierarchy读取DetectionUnitDecision.parts.faces时遗漏已持久StageId；CommittedResultProjection按stage身份加入真实Face后导致同一面双计。修复仅读取已有StageId并与融合投影使用同一Id，历史缺字段保持null；保留各Stage独立结果、原义务6/14面断言及完整重读。新增两个受控投影数据行（当前Stage与合法历史null）先登记，再实现；不删重复失败证据、不扩大期望数。

012获批成员导航与016独立成员物理格合并：点击任一成员格时，当前组仍提供全组成员→面→相机导航，默认选中该格所属成员；每个成员的CellId/PhysicalSlotIndex仍独立保存，不将父格覆盖成员格。实际浏览器在多组正常及程序集E存在的同次harness中做逐相机编辑/保存/重开/完整重读，随后沿本运行核实际冻结参数与正常重启。

迁移夹具修订：四面组合沿原义务一AB三CD，捕获数30/独立成员8/面14不变；当前翻面按同transition真实FlipPickCompleted、FlipPutBackCompleted及3D复查提交核查，旧FaceEstablished名称不代表当前设备回执。实际页面定位必须匹配原导航按钮含填写计数的名字，不修改产品文字。浏览器同源代理须透明转发真实SignalR事件流并关闭上游，避免全流缓存制造刷新迟延；新增有限请求/刷新/决策读取时序仅诊断，不增加原10秒窗口、业务成功旁路或重试。

程序集迁移核对区分两个Part/各自面结果与一个实际WholeAssembly DetectionObjectResult：原义务是部位细节保留、整体共享翻面和整件一次搬运，不能按Part数要求两个物理对象。期望members/parts=2、objects=1分别登记；多组仍每成员独立对象4/8，不减少真实执行层级或搬运/部位断言。

本轮最终必要集合登记：固定46保持，补充50行；新增程序集E算法失败及NG/Pending同存两条实际进程行，并纳入先前已声明的共享执行器原25行中未被固定46/014新行覆盖的19行。旧原始义务、失败及initialRegistrationIntegrity不改。完整进程集合实测27分41秒；外层命令监督等待由20分钟改60分钟，仅容许串行收集既有10秒虚拟调用结果，所有产品动作/PLC/I/O/保存窗口和冻结预算原样保留。

最终登记99项C#（固定46+必要补充53）、18项前端组件；补充范围为7条自动多组/程序集迁移进程行、1条接收014真实双特殊件全链、2条阶段身份、必要共享执行器/保存/回执/传输/旧解锁禁止及当前协议保护。迁移职责见migration-affected-map.json；016-migration-impact.py只以本次真实TRX和已验证当前009入口核对31项活动义务，FixtureOnly不参与，全部旧原义务与历史失配单独保留且不称全量历史迁移通过。

接收014真实链预检在启动前被原012页面观察器所属目录拒绝（012PageEvidenceRootRequired），不代表链通过。保留原页面观察职责和实际Edge观察，现有harness及014只读观察器增加严格016所属根的显式入口；原014/012/013入口不变，不混入其他专项环境，不跳过页面观察，不改变保存/动作期限。新专项在业务启动前预热实际浏览器，管道传受控令牌，观察同一run最终状态并核对退出；非业务特权。

同次固定入口收口补充：integrated-copy-acceptance-2实际99项C#/18前端、009127/01073已通过，源码/全部输入及实际构建在审计拒绝时与入口冻结完全相同；Rejected和原报告不改。审计表31条中T39/T44各把原方法名与已登记真实承接重复列为义务，定向合并为29项独立原义务；原193/245/53及initialRegistrationIntegrity不变。同一验收尝试允许仅修正该映射及验证说明后继续末尾检查：全部产品/测试/Worker/UI代码、运行输入及实际构建必须逐文件同摘要；只准明确列出的审计映射/合同计划说明和交付验收引用变化，任何其他变化拒绝。逐个重解析原99TRX、18组件日志、实际浏览器/链证明，保留原执行身份和原Rejected，不跨轮次拼接或冒称重新执行；当前009/010及实际审计仍重新执行，冻结当前身份。该同次收口不改010跨轮次FullRun复用禁止规则。

主项目同次末尾收口登记（2026-10-06）：主项目原有scripts/Inspect-Disk-Capacity.ps1在合并前、正式入口冻结及当前均为SHA256 67671cfc59faf5e3b9c23fe81b7312c369915805b1d3ab775057c8f3914ca914；仅缺009分类导致010 SCRIPT-Repository/A10拒绝。按真实tooling职责补登记scanRequired=true/exemption=false，脚本原字节保留且不执行。允许本次同一入口仅该分类和本016合同/计划/任务说明变化后继续末尾核验；99项原生TRX/18组件报告逐份完整重解析，产品/测试/Worker/UI/输入/构建均原摘要，原Rejected不改。当前009/010与29活动迁移义务实际重验；不跨轮次复用FullRun、不取消历史义务、不新增豁免。副本软件密封保持原身份，主项目独立密封记录该既有工具分类差异。
