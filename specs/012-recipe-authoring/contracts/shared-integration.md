> 当前确认（2026-10-06）：DUI02/03已获明确批准，原预览只读；严格按navigation-approval-20261006.md推进剩余六项。下文此前“待审/未批”是当时记录，不再作为当前阻塞。实施/验收状态以本轮实际回执更新，批准不等于Passed。

# 014/012共同接入与本次唯一责任

2026-10-05当前主项目E:/dzk/gaode-1，旧012修复已实际集成，记录见当前plan-handoff。当前共同RC10/1.5由本会话014统一设计，012消费；共享业务模型、校验、身份、保存、目录、F和执行只有一套。本次根不是文档副本，不等待历史已交共同批。

| 本次文件域 | 唯一职责 | 当前状态 |
| --- | --- | --- |
| RecipeContracts/ExecutionInputs/Serialization/Validator/Identity/Snapshots/Admission/Planner、Ports/Workflow、PLC现适配/Protocol、业务注册与事实投影 | 本会话014后端 | 本次设计；未实现 |
| RecipeEndpoints.cs/RecipeEndpoints.Authoring.cs/Program.cs、RecipeEnvironmentDecoder.cs/SemanticRecipeInputProvider.cs、SqliteRecipeStore及维护消费者、前端editor/runtime/原型差异 | 本会话012配套 | 消费共同RC10，无私有模型/规则/identity |
| 混合API与现状态/通信合同 | 本会话按014共同语义、012显示/HTTP定向同步 | 唯一定义引用，不并行覆盖 |
| 013降频/单源与性能证据 | 保留现成果/门槛，不进入本次修改 | 不重测/不改输入 |

新代码前置为后续tasks承接RC10 nullable/版本/StageId/scope全部消费者与扫描。不是缺历史D011代码；也不是先要新测试通过才写实现。DUI02/03已批准，严格限制在两原预览表达；DEP现场项只限制对应运行/硬件结论。

## 历史012/011职责及分批接收（以下按原时点保留）


2026-10-04本独立副本新增共同字段、唯一校验/序列化、planner和必要capture端口由012负责增量；原Program/RecipeEndpoints/decoder/provider前端同侧编辑。禁止修改013通信/预算/测量。本轮无外部交付假定，缺正式配置只限对应新建/新增面，硬件Applied不宣称。

当前状态：2026-10-04，012独立软件范围25/25任务已有实际完成条件。前轮7份/I1—I4的4份及此前状态文档已有011真实接收/文档合入；最终004的7份文档已实际接收合入，产品合入与本次共同006接收增量仍逐批独立记录，不能从历史合入推定完成。共同实现仅接固定批，不将设计当代码。

## 当前实施接收（历史设计条款按原时点保留）

共同基础001—006、运行绑定001—030、状态001—007均已逐项核SHA实际接收，本轮36批/349份文件投递记录（含同文件后继）与19项明确删除；之前前置接收保持。当前Program、SQLite提供者、common binder/reader及真实查询组成后完整Integration/Host/StorePrep build18零警告错误，源架构10九条通过0Skip；共同006后受影响A02按该固定1项证明复用、其余8条不变，保存/序列化4负例同源复用。当前runtime33项和authoring11项、适配17项、真实SQLite/HTTP10项有据。前端build/typecheck02和当前原型/安全3个不同检查完成；首次错cwd的1项失败保留，未重跑已过2项或放宽断言。

T022005/006及T023006已有011实际接收回执；后者6份文档已合主项目，3份前端产品/测试/差异清单仅到011副本。最终004的7份文档/27证据已由011实际接收且文档合主项目，7个目标摘要已只读核对；本次共同006接收增量仍待新回执，主项目产品仍未证合入，按plan-handoff精确列交付。

single05/multi03真实软件链与原页、冻结/保存/通知/实际NG处置有据。旧单面分拣初值、首Final未settle、原库历史查询Incomplete全部保留；状态007修后原multi03库只读GET/页面补证20项满足、13项历史查询/正常退出/无新Run有据。更多面/E、Pending/异常槽及失败差异用011具名必要共同动作/真实SQLite组件，不冒称另一设备链。030共同孤立人工换面服务/API/注册已实际删除接收，历史JSON只读、取盘/姿态/安全保留。012精确删除对应前端孤立消费、当前组件/原型回归通过，阶段/冻结/分拣/终态映射未变；原实页证据按精确变更范围复用，不把旧截图当新字节采证。

当前没有阻塞012软件条件的未交共同代码。011跨会话总审/主项目集成及原正式地址/外部3D/安全/恢复延期仍按其责任，软件Test完成不等于现场准入。

## 已接收版本与IC逐项结论

**当前依据**：共同recipe-contract/1.3、station01-execution/1.0及011-verification/1.1不变；G-01设计交接已闭合，G-02/G-03不重开。此次已读取011 tasks-handoff先行正式发布的分批交付/首次构建/联合启动说明；其余活动正文发布核对见plan-handoff当前节；本副本旧011来源快照和此前5份/83份接收摘要保留其历史时点，不作为最新交付状态。

前轮checklist历史接收：先按主项目clarification-sync-20261003.md B1/B2/B3接收81份，收尾核对发现011明确交付1.2及design-alignment-20261003.md，再接收18份定向刷新和2份新文档，共83份不同共享文档，逐文件摘要见[basis-receipt](../basis-receipt.md)。没有覆盖012负责的006/012/前端文档。当时已确认012澄清15份及Phase 1首版接收，最终13份尚待接收；最新tasks-handoff已确认这13份实际合入。前轮1.3消费的7份现已接收合入；本次I1—I4增量在当时待接收，当前已收事实见页首。

| 依赖 | 本次实际接收 | 012当前消费与剩余限制 |
| --- | --- | --- |
| IC-01 共同正文 | recipe-contract/1.3 RC01—03/RC08：准确类型、路径、schema、唯一序列化及迁移 | G-01已消费关闭；editor-ui给字段→输入/只读→正文→重读映射，不把结构示例当默认数据 |
| IC-02 身份/匹配键 | RC01/05：服务端RecipeId/Version，FCode原文本全局唯一，DefinitionDigest由011统一，CatalogDigest视图来源 | 已落实RecipeId＋Version、ETag→ExpectedVersion，无SaveId；1.2 RC04.1已给RecipeDefinitionIdentity及CreateRecipeId/CreateVersion/ComputeDefinitionDigest规则，G-02关闭；012仅调用共同实现 |
| IC-03 唯一校验 | RC04：ValidateForSave(candidate,current)、Valid/Issues(Code/FieldPath/Message/对象引用)、保存结果五态 | 合同已接收；检查和保存共用，写边界内重新校验；生产批准与保存、设备在线分离。唯一共同校验已收到；实际012保存/API均调用它 |
| IC-04 当前读取/冻结 | RC04/05/05.1：GetSnapshot、SaveAsync、Match、BuildExecutable、选择意图、execution-inputs/2及RecipeBindingReceipt | 合同已接收；1.2保存请求含TargetRecipeId/ExpectedVersion/RequestId，同实例同库、一次快照与深冻结；纯软件绑定无旧PLC ACK。当前Program/Endpoints已接共同注册/协调器/reader及真实绑定，不新造端口 |
| IC-05 公开状态/API | station01-execution/1.0；s01-api/1.2、station01-main-flow-api/1.1、s01/notification/2.0、s01-recipe-api/3.0 | 合同已接收；编辑/运行引用分离、executionPhase/槽位/覆盖/结果/处置/通知映射已落editor-ui。历史null/Unavailable保留 |
| IC-06 联合证据 | 011-verification/1.1 M01—M11及单面/多面联合代表约束 | 验证计划已接收，QV与M映射见quickstart；单面05/多面03软件链实际通过；同run页面终态补证20项已完成，原Incomplete保留，软件链与页面证明范围分列 |

四份混合API的唯一定义文件：001 contracts/api.md、003 contracts/station01-main-flow-api.md、003 contracts/status-notifications.md、008 contracts/api-results.md。012只消费，不写这些共享文件或复制其业务字段规范。

## 具体交接G-01—03及当前状态

| 编号 | 当前状态/明确依据 | 责任与影响 |
| --- | --- | --- |
| G-01 新字段完整类型落位 | 已关闭（设计消费）：1.3 RC08.1—04交用途点/逐阶段Flip/E、TargetPose、实际schema值、唯一序列化与旧Stage迁移 | D012-G01-receipt-1.3已由011接收，前轮7份实际合入；共同基础代码已分批接收，运行能力按当前实施节区分；不自行建型/编解码/校验 |
| G-02 统一身份/摘要调用 | 已关闭：1.2 RC04.1明确Application/Recipes/RecipeDefinitionIdentity.cs及CreateRecipeId/CreateVersion/ComputeDefinitionDigest；SHA-256、规范化范围和唯一实现责任已交付 | 012只调用，011在代码阶段交实际实现与类型，不另建012身份服务。数值/null等编码细节由011唯一实现收敛，不再视为未给设计规则 |
| G-03 011存储旧选择 | 已关闭：011 design-alignment D01、plan技术上下文和research R05已改独立SQLite；本轮接收其当前文件 | 不再要求修旧快照，不保留活动文件目录或失败回退 |

前轮checklist首次按1.1评价时，G-02/03曾未关闭；当轮收尾接收明确交付的1.2后按实际内容更新，历史评价见architecture Notes。前轮接收1.3关闭G-01设计消费，本轮核实011已接收回执；I1—I4只调整交付/执行依赖，不改变已确认主流程。

## 保存与F接线顺序

HTTP格式/权限 → 同一个SqliteRecipeStore实现共同SaveAsync → 写边界读取GetSnapshot、TargetRecipeId/ExpectedVersion、共同RecipeDefinitionIdentity及ValidateForSave → SQLite真实提交 → 共同Saved。检查只调用同一校验器，不写库；新建检查允许缺服务端只读身份。真实提交确认才Saved，后续读取同源可见；不存在额外活动缓存发布/提交后重读门，已提交后回包或重读失败不倒写为未提交。

正式公共准备/F真实解码 → 011一次GetSnapshot → Match全目录原码唯一再核身份/场景/用途/准入 → 同一Matched完整值规划/冻结 → 实际绑定保存与移交 → 共同执行/状态投影。保存不调用规划器/设备，未匹配不能取消此前必要公共准备。

012提供当前Snapshot.CatalogDigest；不可变保存正文保留提交时派生CatalogDigest。011规划/冻结使用Matched的本次视图来源，不能拿旧正文来源或ObservedVersion/ObservedCatalogDigest充当F锁；recipeId/场景/冻结关联保护继续。011交Endpoints/Program的共同注册调用，012同时更新生产者与消费者，不加执行兼容层。

## 物理文件唯一编辑者（后续代码阶段）

| 文件或明确范围 | 唯一编辑者 | 对方提供的内容 |
| --- | --- | --- |
| backend/src/Gaode.Host/Api/RecipeEndpoints.cs | 012（用户指定） | 011 T011/T012以D011-runtime-binding-1.3交plan/bind调用、回执及保护要求；012 T020接线并移除被替代内联业务，业务服务仍011 |
| backend/src/Gaode.Host/Program.cs | 012（用户指定） | 011交D011-runtime-binding-1.3实际注册调用/生命周期清单；保存提供者先接，运行装配后补，012不重复注册共同服务 |
| backend/src/Gaode.Infrastructure/Gaode.Infrastructure.csproj；backend/src/Gaode.Host/appsettings.VirtualPlc.json | 011（状态001明确） | 011已删除旧Review输出/Provider配置并稳定交付，012仅核摘要接收，不并行编辑 |
| 001/003/008上述混合API文档；需规/宪章/共享根README | 011（用户指定） | 012提交本文消费要求及文档交付清单，不回写主项目 |
| Gaode.Application/Recipes/RecipeContracts.cs、ExecutionInputs.cs、RecipeDefinitionIdentity.cs、RecipeDefinitionSerialization.cs、RecipeDefinitionValidator.cs、RecipeRunPlanner.cs及绑定/执行/预算/准入；共同IRecipeStore/IRecipeCatalog及结果类型 | 011 | 012提交持久读取、检查和内容身份所需调用，不另造接口定义或业务校验 |
| Gaode.Infrastructure/Recipes/SqliteRecipeStore、RecipeStoreDbContext及专用配方迁移（新增）；RecipeCatalogFactory.cs、JsonRecipeCatalog.cs | 012 | 011提交共同序列化/解码要求；旧适配仅按有效消费者核查 |
| Gaode.Infrastructure/Recipes/SemanticRecipeInputProvider.cs、RecipeEnvironmentDecoder.cs | 012（用户指定，RC07一致） | 011提供共同语义迁移及消费者要求；012落实格式/输入适配及无用途换码/测高清理，不改共同业务规则 |
| Gaode.Host/Composition/CapabilityRegistration.cs、AdapterBindings.cs、Station01Registration.cs | 011（RC07） | 提供Program注册调用及适配消费者要求，012不平行改这些文件 |
| Gaode.Host/Api/Station01Authorization.cs、TestAuthenticationHandler.cs | 012 | 最小Recipe.Write映射，现有权限与Test来源不变 |
| Gaode.Host/Api/Station01ApiContracts.cs及运行查询/通知文件 | 011 | 012复用现有ErrorContract；所需变更列需求，不复制基础错误体系 |
| backend/tools/Gaode.StorePrep/Program.cs（混合维护入口） | 012 | 增加配方库专用受控入口，既有011运行库维护需求以补丁请求交012；不放宽原门禁 |
| 运行库Station01DbContext/StoreSchemaInspection/StoreMaintenance、RecipeApplicationHistoryReader | 011 | 012采用独立库，本轮设计无需改这些文件；历史读取/保存义务保留 |
| frontend/src/pages/a.html、runtime.js、必要API绑定及frontend/scripts/verify-prototype.ps1与差异清单 | 012 | 011 T015以D011-runtime-state-1.0交实际查询/通知/投影，012 T021消费 |
| RecipeCatalogTests.cs、RecipeEnvironmentDecoderTests.cs、SemanticRecipeInputProviderTests.cs及frontend相关定向测试；新增后端RecipeAuthoring*测试 | 012 | 联合运行部分引用011证据 |
| 既有共同校验/绑定/执行/通信测试；RecipeExecutionBoundaryChecker及其测试 | 011 | 012交新保存可达路径和架构负例，由011集中调整门禁 |
| scripts/collect-station01-page-facts.ps1及联合启动/运行脚本 | 011 | 012交实际弹窗保存/重读采证要求，复用同次链，不各改一份旧脚本 |

表中Gaode.*路径均在backend/src下。未列出的新重叠文件在依赖实现前登记到本表，由调度指定唯一编辑者；不能以目录相邻推定双方可改。此表中新增细分归属为012方案，用户指定归属和011 design-alignment/plan已接收的细分归属按双方记录执行；未列的新路径仍待唯一指派，原设计时未写代码；当前已按本表独立实施并实际接收共同代码。

## 任务前置交付与消费边界

以下任务来源均为已读取的011实际任务编号，不猜造任务。按用户本轮明确指令分包消费；正式发布说明与实际代码分别核验，缺交付只限制相应批次。

| 名称/版本 | 明确生产范围与012消费 | 当前状态/完成边界 |
| --- | --- | --- |
| D012-G01-receipt-1.3 | 前轮012字段映射/API/正文/接入及7份交付；011已核验 | 已接收合入；本轮I1—I4修订另交，不倒改历史待接收记录 |
| D011-common-code-1.3 | 011 T003—T005共同类型、身份/摘要、唯一序列化、ValidateForSave/Admission、IRecipeStore/IRecipeCatalog及结果，Matcher/深冻结 | 001—006已接；T002接收及T018实际SQLite/Matcher/旧冻结组件通过，无第二业务实现 |
| D011-runtime-binding-1.3 | 用户指定011 T011/T012的协调器、RecipeBindingReceipt、Intent/Bound/Handoff、原期限/取消及注册调用清单 | 001—030已接，T020端点冻结读取/Program接线已交；缺移交/拒绝HTTP与单面05/多面03实际绑定/冻结、当前组成构建18通过，保存/期限/取消/当前门禁有效 |
| D011-runtime-state-1.0 | 011 T015真实阶段/姿态/异常物理槽及同源查询/通知 | 001—007已接，页面直接消费实际阶段、ManualRemovalAllowed、轴、准入矩阵及sortingState；33项runtime组件通过。multi03同run页面/通知与修后原库历史终态補证20项满足；当前孤立分支删除不改投影映射，精确差异/33组件/原型证明承接，不重跑链 |
| RC08必要共同消费者迁移 | 011只迁移其所有的实际编译/调用消费者，交源码身份、签名及受影响引用清单 | 与012 T019首批一起满足首次Host/StorePrep/相关测试构建条件；不凭任务编号推定已迁移，不要求把后续全部运行能力一并实现 |
| D012-store-api-1.3首批 | 012 T017：真实SQLite、SaveAsync/GetSnapshot、读写HTTP、完整重读、Version/ETag及必要失败证据 | 先交实际保存读写子集、文件及未完成范围；不等T022，不包含尚未完成的适配/运行接线 |
| 012 T019适配、T020运行接线分别补交 | 按原任务分别登记各实际完成批次的文件、适配/目录及绑定接线 | 关联T017的保存读写交付，分别说明范围和验证状态；不把D012-store-api-1.3自动扩大成这些能力已齐，不另建正式目录 |
| D012-ui-joint-evidence | 012 T022在011唯一启动的同次单面/多面链中采页面/API/保存及状态对账 | 运行后形成的完成证据，不作011启动该运行的前置；缺项/失败保持对应任务未完成 |

### 首次构建与T019分批

T019首批只依赖完整独立源码、T003对应消费者核查及已经实际取得的RC08类型/唯一序列化和迁移要求（由T002登记已交范围）；不机械等待T009、绑定注册或查询状态。先迁移012拥有的旧PointRefs/RecipeStage/测高/Flip等必要消费者，并迁移所选项目实际命中的012目录/适配测试。011在T005基础批同步交其所属的RecipeRunPlanner/CoordinateResolver、执行器/预算、绑定/装配/历史投影及受影响测试的直接类型消费者迁移；完整动作验证仍留原任务。T010承接RecipeEndpoints/Program的必要直接签名和DI变化，T020后续补实际绑定，StorePrep只承接真实命中。未完成这部分时可以编写定义与不受限实现，不能把Host、StorePrep或引用它们的测试工程首次构建/执行记为具备条件。T013/T017必须检查此条件。

T019后续才按实际依赖接真实SqliteRecipeStore、目录/提供者及剩余有效清理；相关调用在T020前就绪。每批分别记录源码/摘要、实际构建项目及软件证据；源码可先交，不把未构建或未验证写可用。首批与后续保留同一任务ID、分别记录交付，整项义务和必要验证未齐不勾T019。测试定义先行不是测试通过先行；T009等实现消费定义就绪，实际构建与执行等相应实现/消费者迁移，消除反向测试依赖。

011唯一序列化负责必需字段、格式版本与正文类型，012不建领域编解码/校验器。不得用旧字段兼容、补零、移除正式编译文件、假保存、备用目录或第二业务实现消除依赖；保留真实有用的适配、历史读取、关联、取消、期限及保存保护。

### 联合启动与证据

012先提供真实保存读写，再按完成情况补适配/运行接线；T021形成现有页面/API接线及采证准备。011 T025维护唯一驱动，T027在实际代码/输入、双方适用必要清理（012 T023对应范围）和其既有前置满足后统一启动；012 T022同期采证、对账。运行结束后双方共用同批构建/run/保存身份收敛最终证据，D012-ui-joint-evidence只控制页面/功能完成声明，不反向成为该运行启动条件。

这一区分不增加第二套联合验证、不减少既有单面/多面及QV-01—05/M证据。若011活动依赖正文尚未同步，记录具体待核对处，不宣布跨会话依赖已闭合；不阻塞独立保存读写设计或消费者核查。

## 已关闭历史残项

| 原残项 | 当前证据与结论 |
| --- | --- |
| 宪章页尾7.0.0/旧治理说明 | 已核主项目当前页尾8.0.0/2026-10-03并接收；关闭，不再要求011修旧快照 |
| 008 data-model身份与生命周期第2项锁旧version/digest | 已改为选择意图和保存后F新内容/冻结旧内容；关闭，保留正确身份/场景约束 |
| 混合API未交付、002对旧008 API版本引用 | 前轮已核四份合同交付及002改引用当时的011 recipe-contract/1.2与008/012消费，不再旧v2.0锁定；原残项保持关闭。本轮共同字段直接消费1.3 |
| 两个输入适配文件所有者不明确/曾列011 | 用户本轮及RC07明确012唯一编辑；plan、本文和清理表同步，011只提供语义与消费者要求 |

关闭的是旧文档残项，不是已实现或软件验证通过。后续最小证据及当前未合入文件见[plan-handoff](../plan-handoff.md)。

## 013实施前定向同步（2026-10-04）

SY-07消费核对：013保留012真实保存→F匹配新内容→运行快照隔离，唯一共同模型/校验/执行不变；editor-ui.md、recipe-authoring-api.md及012 spec/plan/tasks无本次行为变更，沿用不重写。Host位置live/1.2独立时间/代次/可靠性由011/013生产，现有轴投影消费Position.Identity.Reliability，页面2秒状态补查仍只取API且不产生PLC采集。新版预算schema2.0及代表预算/模拟2仅改变引用/摘要，保存/匹配/冻结按原校验正常消费；旧历史只读。013真实API观察器只证明后端负载，不冒充012页面ready或验收证据。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。


## 当前2026-10-05配套设计消费

所有配方共用原弹窗三步和手动10×10实际布局，槽数量仅统计；第二页保原位/空位、区内独立编号及高亮。稳定格位+区域关联点位/成员/每次拍照，取消/改区只清本格，显示重编号不串值。OK号决定阶段内检测序，跳过保号；普通面/成员序、翻面和整盘统一分拣保持。特殊属于场景1，独立两用途抓手、共用工位取放/两组绝对角及逐槽本件原始OK槽的放料关联，逐件闭环由014共同执行负责。

旧已实现count自动生成/顺序依数组/旋转不可执行/分拣抓手只存不接PLC是基线事实，不再是本轮目标；特殊OK不得套普通NoMoveRequired。旧任务ID/勾选和旧证据不改，不用旧完成证明新增需求。共同RC10及layout-design/API已经给出字段/版本/接口与消费者/历史策略，DUI02/03预览于2026-10-06批准，现有任务承接，不重建plan/tasks。没有新的012模型、业务校验/匹配器或执行路径。014主责必要共同和通信增量，012只编辑保存和界面消费。

原始料盘、OK区域、实际物理格位及实体身份在取料前明确并冻结，后续旋转/采集/判定/回放沿用同一关联；区域号只用于展示/检测顺序，不能代替原始槽身份。特殊OK原槽回放使用分拣抓手，不切成上料抓手或NoMoveRequired；实际取料及必要保存、转运、放料和安全位确认后才推进下一件，失败不记录完成。

操作者不得为OK选择其他目标槽，不增加“是否回原槽/OK处理方式”开关；原型任意OK目标配置含义退出。若已有明确必要的原槽放料参数，归该原槽取放配置；同槽不推导全部取放坐标、高度/抓手补偿相同，不自动复制全部取料值、不编造新参数。

历史读取保持原记录事实，不把旧任意OK目标/旧完成标记重解释成已按本次原槽规则执行。当前显示与执行必须区分普通无需搬运事实和特殊实际原槽回放事实；缺实际保存/动作证据时不补造完成。准确共同字段、序列化/历史策略和接口设计已在RC10/layout-design/API定向对齐，但未实现/运行。


## 当前标准深度设计审查增量（2026-10-05，后于Phase 1）

本会话014/012统一职责不变：014承担合法后台相机/算法/姿态/特殊两组及工位共同配置准备；012沿现StorePrep维护和同IRecipeStore/IRecipeCatalog/SQLite实际落地，调用唯一Serializer/Validator/Identity，交特殊draft→layout→保存完整GET能力。准确请求/解析/SourceRecipeId及来源版本上下文见API-L00/L01a，代码尚未修改，配置值未提供不虚构。

当前目标新写正文4/记录1.5/冻结3，历史2/3与旧冻结原版本读取；同面重复组编辑必须StageId。不建立第二模板库、来源目录、模型或校验。I01—05设计关闭，DUI02/03导航于2026-10-06批准、DEP局部现场输入保留；完整审查见014 design-review，所有实施/契约迁移/删除/采证留后续正常任务，本轮未生成tasks。
