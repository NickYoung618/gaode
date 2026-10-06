# 当前012导航交付（2026-10-06）

DUI02/03已确认并已按原预览实现/实页保存验收；准确证据、清理和复用范围见[navigation-implementation-20261006](navigation-implementation-20261006.md)。最终持续门禁与任务计数见artifacts/recipe-layout-012/navigation-20261006/final-index.json；不从授权/旧阶段记录推断通过。

---

## 此前阶段记录（下文保留原时点事实）

> 当前确认（2026-10-06）：DUI02/03已获明确批准，原预览只读；严格按navigation-approval-20261006.md推进剩余六项。下文此前“待审/未批”是当时记录，不再作为当前阻塞。实施/验收状态以本轮实际回执更新，批准不等于Passed。

> 2026-10-05当前主项目核对：012修复53份已实际合入，记录见artifacts/recipe-ui-fix-012/main-integration-20261005-report.md；52份交付摘要一致，登记表保语义合并。下文旧“待合入/独立根”均属其交付时点。本轮014/012Phase 1增量设计已形成，新增布局/旋转未实现，DUI02/03导航待审，见014 source-and-sync及012 prototype-baseline-20261005；不改旧完成事实。

# 012实施交接

## 历史独立UI修复交付（2026-10-05合入前时点，后已实际集成）

实际根 `E:/dzk/gaode-012-ui-fix`，显式功能目录 `specs/012-recipe-authoring`。本次FR-021—024、US4—6增量已实现并完成必要软件验证，T026—036结论见 [验证报告](verification-ui-fix-20261005.md) 和 [增量交接](ui-fix-handoff-20261005.md)。共同合同1.4/正文3仅在本副本；主项目、011/013副本未写入。本次没有接收/合入回执，保持待统一合入。下方旧根、25/25、旧合同/待交接/证据均为原时点事实，不证明本增量已集成。

## 历史最终004实际接收合入及共同006收尾增量（2026-10-04）

011的`receipt-D012-status-004.json`已明确核manifest `d338a5deda50a2830e9e21927f08cbc6e267fa1f8a900a2fec606c5291342415`、接收7文档/27证据，并逐文件合主项目；012只读核7个mainAfterSha256全部与实际目标一致，17项源码摘要与011组成相同。**这是文档合入，主项目产品仍未合入**。此前004包/发布时点待接收状态保持历史，不覆盖immutable原包。

最后明确共同`006-orphan-offset`（manifest `b505090403a2cbb9d0eea768cd2bc5dc0d63e19446ac28eb0bc6159b4c37d538`，1文件/6证据）已逐SHA实际消费：只删除无消费者MeasurementOffsetBasis声明，无字段/接口/序列化/身份或保存内容迁移，不影响现有执行。当前共同001—006、绑定001—030、状态001—007实际已接；本轮36批/349份文件投递记录、19项实际删除，030回执已交。当前完整Integration/Host/StorePrep build18零警告错误；公共A02受影响1项复用该共同固定实际证明，其余8条源规则10/当前页面与保存/冻结证明保原适用范围，无新业务用例/完整链。

本次`implementation-status/005-common006-receipt-and-final-doc-sync`仅交4份直接受影响活动文档（本文件、tasks、quickstart、shared-integration）及共同006实际消费/当前build18/004实际接收证明，校正2处遗漏的终态“仍待”旧正文。01225/25保持，所有清单/006历史勾选保持；待011实际接收该收尾增量，不把其或主项目产品说成已合入。011总审及现场原延期独立，停止等待调度。

## 历史012软件任务完成与最终交接（2026-10-04，旧25项）

**实际根**：`E:/dzk/gaode-012-recipe-authoring`；显式功能目录始终`specs/012-recipe-authoring`，Spec Kit返回已核一致。012全部25项软件完成条件现有实际证据，逐项见`artifacts/recipe-authoring-012/final-20261004/task-completion.json`；architecture24项和requirements16项原勾选不变，006原ID/勾选不改。以下旧节是各时点历史记录，未交/未运行/待补读/待接收不再代表当前；原失败证据仍保留。

**共同消费**：recipe-contract/1.3、station01-result-display/1.1；共同源码001—005、绑定001—030、状态001—007均已逐SHA实际接收，35个本轮增量批/348份文件投递记录、19项明确删除，先前前置保持。当前完整Integration/Host/StorePrep构建17零警告错误，当前源架构10九项通过0Skip；4个保存/序列化负例按不变checker/输入复用。前端构建/typecheck02通过，当前runtime33＋authoring11及适配17、真实SQLite/HTTP10项按实际范围通过/复用。原型正例与5个未授权差异拒绝、必要安全边界共3不同检查有效；首次错cwd失败保留，仅重核失败项，不把该3项原报告全部称通过。

**联合与真实页面**：011唯一single05/multi03代表链、原页/API/通知/保存/F冻结与运行后新保存隔离均有据；更多面/E、Pending/异常槽及失败差异按011具名共同组件/实际SQLite补对应范围，不伪称另一次硬件链。原multi03终态补读20项满足，13项后端历史GET/正常退出0/无新Run有据。030另外只读原run13项GET及旧换面端点404满足。原首Final未settle、历史投影错误Incomplete、旧单面分拣初值、其他失败、辅助标签/长路径/删除键/cwd问题全部保留。当前runtime只删除孤立人工换面分支；精确源差异证明阶段/冻结/结果/分拣/终态未变，当前33组件/原型门禁承接；不声称旧截图采了新字节或用组件替代原同run证据。

**实际删除与有效承接**：012旧目录正式回退/Review产品源、旧PointRefs/测高/换码适配旁路、内联计划/匹配及V1执行回退、旧阶段推断/活动目录冻结页头/过期版本限制、无用途演示监听、死运行参数和孤立换面提交/错误正例已实际删；保当前格式离线适配、合法历史JSON读取、真正取盘/权限/原因/保存/取消/期限/安全。19项011清单删除只按摘要消费，不越界编辑共同实现。具体位置/保留用途/原件见final cleanup、source-identity及接收回执。

**实际接收/合入**：011已收T022005/006（后者manifest `efd826eec7e0fb27bb62f99ac0397d326fed37d674468bf703f924633afc756d`）及T023006（`fffcea9621e0d0383c625a5d61db4bdb877d602da39191719d3e34f17868cd6c`）。T023006的006 spec/plan/tasks/contracts/api.md/contracts/prototype-mapping.md及012 editor-ui共6份文档已由011实际合主项目；3份前端产品/测试/差异清单仅其副本收到。前轮7份与I1—I4的4份及状态003的历史文档接收保持。**004最终7份文档及27证据已由011逐摘要接收并合入文档；主项目产品未合入**；由011唯一总审/集成，不直接回写主项目。

### 本次逐文件最终交付

固定包`implementation-status/004-final-source-and-evidence`供011逐文件合入；共同业务文件无012修改，已有产品源码稳定批按final delivery-ledger顺序及摘要消费，不从在制目录取整包。

| 当前012文档文件 | 本次内容 |
| --- | --- |
| specs/012-recipe-authoring/plan.md | 只更新阶段/接收与实际状态摘要，原方案不重做 |
| specs/012-recipe-authoring/tasks.md | 原25个ID、实际完成条件及本次T020—25勾选，历史不改 |
| specs/012-recipe-authoring/quickstart.md | 当前源/验证入口、失败复核与同run复用范围 |
| specs/012-recipe-authoring/contracts/recipe-authoring-api.md | 实际API/存储消费状态，不新建共同定义 |
| specs/012-recipe-authoring/contracts/editor-ui.md | 当前真实终态证明与孤立消费删除/精确复用 |
| specs/012-recipe-authoring/contracts/shared-integration.md | 当前代码批/回执/单一负责人/局部延期 |
| specs/012-recipe-authoring/plan-handoff.md | 本记录及交付清单/状态边界 |

`artifacts/recipe-authoring-012/final-20261004`的manifest、source-identity、reception-index、task-completion、us1/us2、joint-reference、verification、cleanup、delivery-ledger一并固定交付，包含当前源码摘要/真实原证据位置/最小QV01—05及M对应，旧发布包不覆盖。当前没有阻塞012软件任务的缺失代码；011总审/主项目集成、正式外部3D/地址/产品型号承载/安全校准及原延期恢复分别保其局部影响。具名Test软件完成不授生产批准或现场通过，不要求全量/穷举/009010全历史重跑。

本轮前后extensions hooks均空，未执行钩子，无Git写操作；主项目/011副本/来源原型均无012写入。到此停止，等待调度安排。

## 孤立人工换面消费清理006已稳定交付（2026-10-04）

按011030明确要求，已发布`T023/006-retired-manual-flip-consumer`9文件/10证据：runtime实际删除canConfirmFlip、人工提示、按钮换面和提交分支；替换旧错误正例为历史字段不得授权/提交，保原型DOM、有效人工取盘/恢复/权限/原因/取消和失败。006 spec/plan/tasks及API/原型映射、012 editor-ui已定向同步；006所有ID/勾选不变，归档不改。完整前端build02/typecheck02、runtime03的33/33通过；原型正例与五项负例通过，安全边界首次错cwd ENOENT保留，仅以正确frontend cwd重核1项通过，0Skip，不放宽断言。

`manual-flip-cleanup-source.json`验证当前runtime除具名孤立分支删除外与已采证字节无内容差异；阶段/冻结/槽位/结果/分拣/终态映射保持，原同run实页证据按此范围复用，不冒称旧截图为新字节采证。029的22文件/12证据已经逐SHA接收（028此前6文件/7证据），当前完整Integration/Host/StorePrep构建15零警告错误。当前共同001—005、绑定001—029、状态001—007实际已接。030共同服务/API删除仍待固定；012不跨界改或假称收到。

T022005/006已核011实际接收回执，当前終态20项及011独立9项同run对账满足，13项实际GET/正常退出/无新Run有据；旧“待接收”仅原时点。当前清理006等待实际回执及030稳定组成后必要构建/门禁，任务仍按全部条件逐项收尾，不在共享消费者仍待组成时提前勾整项。

## 同run终态补证006已发布（2026-10-04当前）

稳定`deliveries/T022/006-multi03-terminal-read-proof`已发布2辅助文件和26证据，消费027/状态007及自有Program早批；请011按固定manifest接收，不取在制目录。012原multi03同库/Run只读补读已实际取得Bound冻结Version/Digest、Completed、Sorting Completed、FinalUnloadCompletion、完整弹窗GET及截图：8次API、2次渲染、2截图，errors=[]。与原链及正常退出/不新增Run对账20项满足，`reconciliation-terminal.json`可逐项追溯。原18项同期证明与实时通知不重跑；历史关闭Run补读不要求新实时Final通知。原Incomplete和首Final未settle证据全部保留；本次助手首轮误用旧标签“配方态”导致1项false的报告也保留，按真实DOM“配方绑定”定向纠正，不改产品或放宽Bound。

012当前完整构建12零警告错误，当前9条源架构09通过0Skip；已核011实际GET13项满足、Host正常退出0/noNewRun=true。本批尚待实际接收，不宣称主项目产品合入。当前任务完成条件正逐项收尾，后继028共同清理尚在制，不复制或将其设计声明当交付。

## 027/状态007实际接收及当前构建（2026-10-04）

绑定027的13文件/8证据（manifest `a7a380f0ce59e8f38de1e7d86b1cc9a8aa561eff3b6452855f2551bfe523204a`）和状态007的3文件/8证据（manifest `0a9320d7c7582f494ad6e4b6ea795f7a17d04be0ec7312437e7ee596ea095d46`）已逐项SHA实际接收。当前共同基础001—005、绑定001—027、状态001—007均已收到；本轮新增32批/312份文件投递记录、18项实际删除（同文件后继不当不同文件）。012当前Program签名与027组成后完整Integration/Host/StorePrep构建12实际零警告错误；旧NotRun为接收前时点，当前不再以签名缺失阻塞。状态007共同投影10项及027必要通信10个不同组件的011稳定证据均已核摘要，不冒称012重跑或联合页面通过。

012受影响现行源架构09正在复核，新补读`03-final-page-03`有限等待器已启动（独立日志同名）。共同输出结构不变，前端仍只消费实际字段，不增本地绑定/终态重建。当前19/25；最终同run真实终态页面及当前门禁结果形成后再评价完成条件。

## 重启查询缺口及新补读准备（2026-10-04当前）

同multi03仅GET补读已实际结束，原`joint-pages/011-multi-03-final-page/page-evidence.json`保持Incomplete：运行状态Completed/最终保存Completed/分拣Completed，但共同查询recipeExecution=null、recipeState=Unmatched、wholeTaskState=NotCompleted，终态DOM未满足。未造冻结页头、推测阶段或放宽采证；700次实际API响应/176次渲染及预算后Host关闭的7次代理失败均保留。011已明确承接已提交V2 Handoff的规范摘要验证与历史终态投影缺口，状态007尚未固定交付，不能称已接或页面通过。

按011最新明确安排，012已切换有限等待器至`joint-multi-03-final-page-03/page-connection.json`，输出新叶`joint-pages/011-multi-03-final-page-03`，expectedRunId仍`cc7f1d75-10f0-4ad1-ba46-1bd44feeed50`。只读原库/Run，无Start/人工确认/新链；011先核实际GET Bound/冻结版本/Final并固定状态007后发布连接。旧补读及原链失败证据不覆盖，最终页面证据仍在补读后形成。

绑定025（10文件、2实际删除、9证据）及026（6文件、13证据）已逐SHA接收，相关回执在resume-20261003根。共同清理18项实际删除，有效历史读取/动作与保护保留。T023/005 Program早批已由011实际接收；相配共同签名删除属于待固定027，当前新Program构建NotRun。当前19/25，不把最新查询组件修正或在制源码当接收/软件通过。

## Program死参数消费者已删除并早批交付（当前）

按011明确T026消费者要求，仅实际删除Program中的NgCapacity/PendingCapacity/ZResetTimeoutMs三个旧运行/通信位置实参与配置读取；PlcProvider直接跟PositionTolerance，后续参数次序保持。配方/计划有效三区容量未删、业务校验不改。稳定`T023/005-unused-runtime-option-consumer`已发布单文件Program及3证据，实际manifest `e0ccc4981565fdc6ff71fa910e8d19d3e400ff9613cc3600168e8bc9ab4e60ef`。当前共同构造签名尚未同步收到，删除后构建NotRun；Integration11零警告错误仅属于025及删除前Program，不冒称当前新源通过。011已实际接此批并配套删除自身参数/注册/PlcRuntimeOptions/配置，后续稳定批确定为027（026已用于测高生产删除），012接稳定027后定向验证。无兼容默认/移出编译，不等待联合才交源码。

## multi03实际通过与必要终态页面重读请求（最新）

024的3文件/9证据已逐SHA接收；012随后Host构建03零警告错误。T021003实际回执已核，editor-ui文档已合主项目，产品仅独立副本。multi03真实run=`cc7f1d75-10f0-4ad1-ba46-1bd44feeed50`链实际1/1通过，012原记录193次API、215通知、49渲染、14阶段截图，errors为空，F冻结、完整弹窗GET、真实NG搬运、sortingState Completed、Final矩阵及正常重启18项对账满足。单面05与多面03共用原链、M04更多面/E/异常差异继续引用011具名组件，不重新跑全组合。

**发现采证收尾时点不足**：multi03在15:20:44的首个FinalResult/矩阵到达时结束观察，该DOM仍带上一查询的AwaitingManualRemoval/ManualRemovalAdmission行；原网络随后15:20:45已收到同run真正Completed/FinalSave Completed/FinalUnloadCompletion。原证据不改写，现只修观察器等待实际终态DOM也已渲染，不以新声明冒充旧截图。后端终态实际读取与分拣状态正确，不能凭此宣布旧DOM所有行已settle。

请011在**同一multi03库/Run只读重新启动Host供页面重读**（若原正常重启Host已关闭可再起，无Start/确认/新运行/同义链）：提供具名`joint-multi-03-final-page/page-connection.json`，012输出`joint-pages/011-multi-03-final-page`，expectedRunId固定原Run；沿原CurrentUserOnly管道凭据、有限观察。012只GET现有Run/evidence/media/配方，补实际Completed/最终保存行及同run截图，再与原真实链合并对账，不要求重跑检测/分拣。**012有限等待器已经实际启动**，日志`await-joint-pages-multi03-final-page.log`；新稳定批将交源码/现有证明。该请求只补受影响页面读取证明，不扩大正式验证或现场能力。

## multi03准备与当前真实限制（最新）

multi02已在012真实页面ready后由011正式POST创建Run `f235db29-9c67-495f-a740-f033df123fa3`，随后StartAcceptanceUnknownHeld（原2秒窗口）阻断，无F/检测/Final；不能再称未POST或无Run。实际页面观察仍按原预算保留Incomplete，不重写失败。011明确下一点`joint-multi-03/page-connection.json`/012 `joint-pages/011-multi-03`，024启动派发修正尚在制。**012已启动multi03有限等待器**（`await-joint-pages-multi03.log`），与已固定04版本辅助资源/DOM读取接线相同；请011固定024并完成必要自身前置后唯一启动，不把最终页面证据作启动前置。

023/状态006已经实际接收并完成012完整构建10、runtime33项和最新9条现行架构规则08复核，均通过0Skip；不是依赖未收到这些批。当前实际缺项是024稳定启动能力及修后代表运行/分拣正确投影同run页面证明。单面05原资产补证/16项局部事实仍有效，但分拣NotStarted当前结论为不满足；当前对账助手新增实际分拣API/Final DOM检查，生成新的reconciliation-current而不覆盖已交004历史补充报告。任务继续19/25。T021003发布已被011读到、其实际接收回执尚待，不冒称已合入。

## multi02实际连接已接（当前）

011已发布真实multi02连接、尚未POST；原15分钟012等待器刚到期，其退出记录保留，不当仍在线。012已立即实际重启同一有限等待器（`await-joint-pages-multi02-restart.log`），读取已发布连接并取得当次管道token，启动修后观察器；只在012输出真实ready供011按原期限启动。已接023/状态006，当前完整构建10及显示runtime33项证明已交T021/003（manifest `7eebff34d758c609015837fbec2778c34c9c0543a82140c36cd3c92d8d356305`，2文件/5证据，待实际回执），不等完整链交代码；不提前声明本次Run或Final通过。

## 已接023/状态006（当前）

按全部固定前置，023的8文件/16证据及状态006的6文件/6证据已逐项SHA接收，回执同resume-20261003根。共同正文仍recipe-contract/1.3，状态字段延续station01-result-display/1.1；只从共同输出消费UnknownHeld与sortingState/changedFields，不造默认完成、重试或Pending规则。主项目明确已交执行合同末段的运动保护/分拣已提交投影承接已只读核对，012不代改011合同。

012最新完整Integration/Host/StorePrep构建10零警告错误；新增Completed/UnknownHeld显示与无取盘许可两项组件，整个受影响runtime组33/33通过0Skip，未变authoring11项复用此前证明。原型/资源字节没有变化，不另跑已过的同义门禁。023公共枚举/共同执行及状态006共同投影影响现行源架构，当前9条受影响规则08正在复核，不提前称通过。multi02有限等待器保持已准备；实际同run完成/分拣正确显示仍待011唯一启动。

## 最新状态限制：单面分拣投影与稳定023/状态006

011已实际核SHA接收T022/004的4脚本/25证据，multi02等待准备也已确认；主项目产品不因此视为合入。单面05原真实保存/F冻结/Final/正常重启及16项局部比较有效，favicon助手缺陷的实际定向補证有效；但011独立复核发现最终DOM/实际查询的sortingState仍NotStarted，实际Sorting已完成，说明共同状态投影遗漏。故该16项汇总不构成所有页面状态正确或T021/T022通过，原补充报告及其判断时点保留，新限制明确优先。012没有把未知或后端旧值改成Completed，不重写共同业务。

011正在固定023运动/UnknownHeld保护与状态006已提交分拣事件投影，当前尚未交付；012只暂停依赖它们的当前运行/显示完成证明，multi02有限等待器仍准备中。收到明确稳定批后逐SHA接收，继续必要受影响构建/窄回归，并在同一次multi02用现有sortingState证明真实完成；不增加完整单面链，不用修正后的声明重写旧05页面。任务继续19/25，不提前勾选。

## 单面05补充页面结论及multi02就绪（最新）

**稳定交付已发布**：`deliveries/T022/004-single05-supplemental-and-multi02-preparation`，manifest `3617935ea75bcb96cd679ebd882725cb4ca79af3e2fdf8d928b97ad47044e8a8`，4份辅助源码/等待器、25份证据（含原错误、实际静态HTTP补证、同run后端原件快照、阶段及弹窗截图、020—022接收回执）。请011取此固定批；本批及补充页面判断尚待实际接收，主项目产品仍未合入。

单面05真实run=`17602489-a2f9-4352-bd1c-c9384637f1ed`的原始采证保留：162次实际API响应、157条通知、42次渲染、10张阶段截图及同run弹窗完整读取；实际Final=true，错误唯一为助手对缺失favicon.ico的ENOENT误回502。原page-evidence.status=Incomplete、011原page-completion-reference.matched=false不改写，不把该原报告单独判通过。

已仅修采证代理静态处理：不存在的资源实际404，仍不造图标；其他文件/真实API异常继续失败。对同一辅助函数实际HTTP读a.html/runtime.js/recipe-authoring.js取得原字节/摘要，favicon404，全部4项通过，无API/新业务Run。`joint-page-assets-verification.json`为必要定向补证。`joint-pages/011-single-05/reconciliation-supplemental.json`以原同runHTTP/通知/DOM/截图与011保存/F冻结/Final矩阵/正常关闭重启对账，16项满足，结论VerifiedWithSupplementalObserverAssetEvidence；明确列原Incomplete与唯一原错。03完整正文对账修正见reconciliation-http.json，直接比实际HTTP正文，原false/稳定003保留。请011消费补充材料重新形成当前同run页面接收判断，不改原错误或用其单独放行。

**012 multi02有限等待器已实际启动**（`await-joint-pages-multi02.log`）：011 `implementation-20261003/joint-multi-02/page-connection.json`→012 `joint-pages/011-multi-02`。只在011明确稳定修正并唯一启动后观察，最终证据不是启动前置。原multi01真实第二面检测受理超时、无Final，当前观察器仍按原6分钟预算收尾原始证据；不终止011进程。023执行UnknownHeld/取消改动尚在制，012未复制或宣称收到；已接022算法保护迁移测试/证据，共同正文仍1.3。当前T022不勾，完整多面及受影响正确失败保护仍待同次代表证据。

## single05同期观察器就绪（当前参与安排）

已核011真实记录04在Host初始化未就绪状态中准备失败，未发布连接、未POST、Runs=0。012没有连接04或生成其页面证据。**现已启动有限等待器single05→multi01**（`await-joint-pages-05.log`）：011 `implementation-20261003/joint-single-05/page-connection.json`对应012 `joint-pages/011-single-05`；多面保持multi01。请011据此唯一启动，不等最终页面证据。原04等待器不会接到未来04连接，仅按原有限等待收尾；012不终止011任何进程。修后观察器稳定批T022/003已明确发布，manifest `98a8a86da7389856e31d46966ca44116591996cbdc15ca8afdf7dc7defffb8d3`；03原始modal标注限制见下节。后继`reconciliation-http.json`改用011同run实际HTTP GET正文直接比较，完整正文与页面GET相同，9个局部比较满足；无手写Purpose转换/第二序列化。首轮比较内部诊断枚举数值与HTTP字符串导致false的报告、稳定003均完整保留，不当整链通过。

绑定020/021固定源码和证据已核SHA实际接收，共2次单驱动文件增量；020消费明确Test运动延时，021在原有限准备期等待真实Host初始化状态，不假Ready/改期限。当前共同001—005、绑定001—021、状态001—005已接；完整Integration/Host/StorePrep最新构建见integration-build-08（实际结果待日志）。012观察器源码使用已发布003的实际DOM冻结观察修正，产品代码未增加默认值或工艺逻辑。

## single04同期观察准备与single03事实（最新）

已核SHA实际接收状态005、运行绑定019：共4份稳定文件、17份证据摘要；012随后Host构建零警告错误，当前A06受影响门禁07实际1/1通过、0Skip。019实际移除旧启动脚本两个无消费的Recipes设置，原清理依赖关闭。状态文档003已核011实际接收并逐文件合主项目，产品仍只在独立副本。

single03实际页面记录已落012 `joint-pages/011-single-03/page-evidence.json`，run=`ba698ed2-e9e5-4231-b7b5-b9fed5f5b1d6`，68次真实API响应、112条实际通知、18次渲染、5张阶段截图及同run弹窗完整读取；未发送业务写。实际F冻结采用保存前更新Version，保存后新Version未改变run投影/页头；完整运行在DetectionTimedOut:ProductMoveAndBeginInspection阻断，无Final，页面Incomplete及全部错误保留。03采证助手的modalRead.runFrozenHeader/Version取了前次渲染的旧值，该标注不可作为冻结证明；真实后续captures/API显示冻结Version不变。已仅修采证助手为等待实际DOM显示共同API冻结Version后再记弹窗字段，不改产品规则或03原证据。

**012实际有限等待器已启动single04→multi01**：读取011 `implementation-20261003/joint-single-04/page-connection.json`，输出012 `joint-pages/011-single-04`；随后原multi01。观察器语法已通过，准备日志`await-joint-pages-04.log`。请011按该回执唯一启动；实际token仍按当次管道转环境，最终页面证据不是启动前置。011已确定03驱动未接既有明确Test运动延时，正在定向接入，不改变原预算。012不复制在制修正，下一固定批发布后另核接收。当前19/25，未将这次不完整链勾为完成。

## 012同期观察器准备回执：single-03（当前）

已按稳定前置核全部SHA接收运行绑定017、018，回执在`artifacts/recipe-authoring-012/resume-20261003/receipt-D011-runtime-binding-1.3-017-axis-sampling-and-page-readiness.json`及同根018。只取固定清单文件；不将011已构建证据冒充012新验证。012实际有限等待器现已启动，明确等待011 `implementation-20261003/joint-single-03/page-connection.json`，输出012 `joint-pages/011-single-03`；随后等待`joint-multi-01`，输出`011-multi-01`。读取当次CurrentUserOnly命名管道临时token；实际API/页面就绪才写ready，不发送业务命令。请011按该准备安排唯一启动，同run最终证据仍在运行后形成。single-01页面Incomplete及011真实400/无Run保留；single-02管道超时/未POST也保留，不重用旧连接、不计产品链通过。

 已核011当前记录实际接收T019/004删除批；T020/001、T021/002、保存003、T022/001均已收到011副本，editor-ui文档已合主项目。状态文档003尚未见实际接收回执，产品未证主项目合入。当前仍19/25任务完成，T020—T025不提前勾选。

当前实施授权：2026-10-03。唯一工作根`E:/dzk/gaode-012-recipe-authoring`，`SPECIFY_FEATURE_DIRECTORY=specs/012-recipe-authoring`。主项目及011副本只读；主项目仍由011集成。下方原I1—I4、tasks及更早记录完整保留其时点。

## 恢复实施：当前接收与稳定批次（当前摘要）

共同001—005、运行绑定001—016、状态001—004已按全部稳定前置核SHA接收；本轮新增18批、248份文件投递记录（含后继覆盖）及16项011明确删除。下方阶段记录保其时点。当前独立保存/弹窗/编辑/完整重读/正常重启证据及Matcher/旧快照组件已具备，最新RecipeAuthoring10/10、前端42/42、边界/原型3/3通过，0Skip。保存API/module/HTML/提供者9个同源摘要已核，复用真实页面证据不重跑同义链。016后最新完整Integration/Host/StorePrep构建0警告错误。

015后012当前源架构9/9通过（architecture-04），之前4个保存/唯一序列化负例按规则/用例未变复用，共13个不同用例有据；更早实际失败保留。016只改共同联合驱动/进程所有权，当前受影响A01/A03/A04/A05与A07组已在architecture-05实际2/2复核通过，扫描485文件0违规；其余7条规则仅按未变的根/公开类型/执行闭包复用04，不重复计数。联合产品链和同run页面仍NotRun。

已按真实消费者核查实际删除012无用途产品Review目录（T019/004）：当前Host不读Recipes配置，只有同实例SQLite；Factory不接受Review，当前代码没有原产品文件读取引用。脚本仍留Recipes环境变量但Host不消费，属于011无效脚本清理，不能据死设置保无用途产品源。主项目原字节只读，证据档案保原SHA/内容，离线当前格式适配和历史SQL读取保留。T019整体条件现已满足；19/25任务勾选，T020—T025其余实际条件未满足不勾。清单原勾选不改。

新增稳定`T019/004-obsolete-product-catalog-deletion`，manifest `e4663b9d7093c7d2e4f57753a4344691bcb514acf305554f4b14091530427eaa`，按明确deletions原SHA消费；待011实际回执。T020001已核011接收、未证主项目产品集成；T021002、保存003、T022001及最新状态文档仍待回执。T022001观察器已固定，011可取README对接实际端口/临时连接/就绪握手；不依赖最后页面证据才启动。

最新011记录已明确实际接收T021002/保存003/T022001，editor-ui增量已逐文件合主项目，其余产品/测试/采证脚本只到011副本；旧“待回执”按其时点保留。T019004及当前状态文档仍待回执。011已指定两个真实连接记录路径`joint-single-01/page-connection.json`、`joint-multi-01/page-connection.json`及NamedPipe临时token传递；012已启动有限等待器`await-joint-pages.ps1`，只读取这些具名运行记录、环境传token并在012证据根启动观察器，日志不输出token。连接文件尚未形成不代表链已启动，现阶段没有Final或同run页面通过声明。013—016实际接收回执已齐，当前仍按整个前置顺序消费。

## 恢复批处理记录（以下按时点保留）

已重新核011最新tasks-handoff和全部适用前置，逐文件SHA接收共同005、绑定003—012、状态002—004；回执在`artifacts/recipe-authoring-012/resume-20261003/receipt-*.json`，共同正文仍1.3。16项明确删除按原摘要核对并实际移除。旧“共享Integration消费者/reader/状态尚未交”只对历史时点成立。此前保存002、T021001、状态文档002均已有011实际接收；两批状态文档已合主项目，产品源码只确认收到011副本，不宣称主项目集成完成。

**立即可取稳定入口早批**：`deliveries/T020/001-committed-reader-and-entry`，2文件Program.cs/RecipeEndpoints.cs，manifest SHA-256 `173e28ba308e5afb582ff82a29809360ee77d3b6cde1b30396bb45b6bae93f07`。Program消费绑定012的PlcMechanicsPath并删除旧TestSpecial实参。plan/bind使用共同CommittedRecipePlanReader，删除V1执行、活动目录重算/当前purpose及容量兜底，保权限、取消、期限/保存和独立无续接许可。Host在绑定012后完整构建0警告错误；保存API9/9、0Skip为绑定011时点，本批README准确分列范围。此早批无实际011接收/合入回执，不等联合链才交源码，T020整体尚未勾。

当前继续收敛绑定012后的相关回归、真实取盘事件/轴诊断绑定、精确原型/架构门禁和采证准备；同run页面/联合运行仍NotRun，由011唯一启动，不以最终D012-ui-joint-evidence作为启动前置。后续稳定增量另发，以下状态001及更早记录保留原时点。

**011必要编译增量请求**：稳定绑定012中的`backend/tests/Gaode.Integration.Tests/Support/RecipeExecution010Expectations.cs:74`触发xUnit2013（使用Assert.Equal检查集合长度，需其唯一负责人迁移为正确集合断言并交稳定文件）。日志`resume-20261003/recipe-authoring-03.log`；Host当前可构建，此项只限制最新全Integration构建/执行，保存9项此前绑定011源码证据仍如实保留。012未关闭分析器、排除文件或代改011消费者。

上述编译请求现已关闭：接收013/014稳定源码与摘要，014定向修集合断言。最新完整Integration/Host/StorePrep编译0警告错误，RecipeAuthoring当前10/10、0Skip（`recipe-authoring-04.log/.trx`）：原9项加一个真实Host/SQLite的plan/bind缺移交/输入拒绝/401保护；未伪造Bound或设备完成。011最新回执已确认T020001两文件实际收到011副本，仍无主项目产品合入回执。

**新稳定页面批已可取**：`deliveries/T021/002-manual-removal-and-axis-projection`（4文件、6证据），manifest `0d906d6e66e952cdebb1b8e336dbfdbb043f7b54f911508da86cfadbd8e54b6c`。前端build/typecheck通过，31项runtime＋11项authoring组件42/42，边界/精确原型3/3含5个未授权变更负例，0Skip。同run HTTP/SignalR/页面仍NotRun；此批待011实际接收，不把组件当联合通过。

**当前M10真实失败需011承接**：`resume-20261003/architecture-03.log/.trx`发现9项、7通过/2失败/0Skip，原始扫描有5处违规：`Station01Registration.cs:115`的机械配置调用向受保护Host装配传原始通信符号（A03一处、A04三处），以及`RecipeExecution010RunHarness.cs:13`仍引用`Microsoft.Data.Sqlite`（business-test的A07一处）。两文件唯一负责人011；应在通信装配边界落实实际配置消费、核真实调用后移除无用途依赖，不放宽角色或规则。其余7条当前源及此前4个保存/唯一序列化负例通过。首轮缺7份规范资产、第二轮012新增独立测试文件未登记的失败保留；已补齐主项目只读资产并将端点拒绝用例归入原已登记RecipeAuthoringBindingTests.cs，没有改共享登记/豁免。T024不勾。完整位置见`artifacts/recipe-execution-010/012-resume-20261003-03/csharp-boundary.json`。

**同期采证就绪接口待接**：012提供`artifacts/recipe-authoring-012/resume-20261003/observe-joint-page.mjs`，只观察实际Edge页面/透明API、SignalR，禁止业务写；脚本语法通过，尚未实际运行。011仍唯一启动Host/PLC/worker与两条代表链。请在其T025驱动Host就绪、业务启动前提供实际apiBaseUrl、受控token只读传递及实际run关联（不将token写普通日志/摘要）；012以环境GAODE_012_PAGE_TOKEN启动观察器，配置apiBaseUrl/evidenceRoot/observationTimeoutMs，expectedRunId可在已分配后给定。输出根仅012的`artifacts/recipe-authoring-012/joint-pages/<新批次>`。ready.json只代表页面实际200/就绪，不是最终通过；011取得这一准备信号后启动同次运行，012同期采证，Final后输出page-evidence.json供对账。原业务期限不延长；最终文件不作为启动前置。当前014未发布实际采证连接/正常重启钩子，只限制T022同run证明。

**本轮及时发布清单（不等待联合结果）**：

| 稳定批 | 实际内容/摘要 | 当前接收状态 |
| --- | --- | --- |
| T020/001-committed-reader-and-entry | Program/RecipeEndpoints两文件，`173e28ba308e5afb582ff82a29809360ee77d3b6cde1b30396bb45b6bae93f07` | 已核011实际接收，产品仅到011副本 |
| T021/002-manual-removal-and-axis-projection | 三份前端源码/测试/门禁及editor-ui映射，`0d906d6e66e952cdebb1b8e336dbfdbb043f7b54f911508da86cfadbd8e54b6c` | 待011实际回执 |
| D012-store-api-1.3/003-component-convergence | Binding测试新增真实HTTP拒绝，16批回执及独立故事/10项验证，`10cd12c84352ffdc2311084c7df463c2f5b92e1abfd38ccab2809e3429a02b10` | 待011实际回执；此镜像形成于015接收前，架构当时失败保留 |
| T022/001-page-observer-preparation | 已固定观察器/输入与输出说明，`4425b06d5f4274f7ec06042df393f6fbc347de939ccec6c061856f7585dda40e` | 准备源码已交，连接/同run采证未运行，待011接收对接 |

绑定015已核SHA接收（回执同根），完整Integration/Host/StorePrep在012构建0警告错误（integration-build-05）。015修复上述011-owned M10实现，目前012当前源architecture-04正在复核；无变化的10项FullSimulation存储/API回归及4个保存/序列化负例按实际路径复用，不重复同义执行。旧失败报告和003稳定镜像不改写。T007/T009—T011/T013—T018已满足独立条件，18/25勾选；T019—T025仍未勾，架构清单原勾选不动。

## 状态001接收与T021稳定增量（历史时点）

保存002发布后收到011新状态批，已继续执行，未停在旧“状态未交”的限制。D011-runtime-state-1.0/001-committed-observation-source的11项已核SHA接收，回执receipt-D011-runtime-state-001.json；部分批，不是完整T015完成。包含011唯一负责删除的Infrastructure.csproj旧Review复制项和appsettings.VirtualPlc.json旧Provider配置，012未跨界修改。011后续脚本/Integration夹具迁移及无用途目录最终删除仍待接收核查。

新稳定交付`deliveries/T021/001-runtime-projection/`：manifest SHA-256 `28be9231fefe994f64b0352d004dfe875ed45ac8865f413c893e74cddee8672f`，3份前端源码/测试/资源门禁文件及1份editor-ui合同、6份证据。先消费保存002，再消费T021/001。runtime绑定实际阶段、动作、异常物理槽、覆盖、参与及处置；冻结页头不读活动目录。stageFacts、旧版本锁、S1/P01专用限制和无消费者sameRecipeRef已实际删除，重复启动/既有恢复及人工确认保护保留。允许选下次配方且不改变当前冻结头部，启动仅传共同已有观察引用，业务匹配仍011。

实际结果：Host07完整构建0警告/0错误；前端build06/typecheck06通过；runtime受影响组件29/29通过；边界/原型3/3通过（含5个未授权变更负例），0Skip。新状态HTTP/SignalR/页面同run与联合链仍NotRun，不把组件替身冒称实际运行。T021和T022仍未勾。保存002的真实页面/SQLite证据仅按其源码范围复用，当前runtime状态变化另有上述组件证明，联合证据不提前形成。

当前还需要011：①Integration/Host共享消费者及新通知构造器迁移，含真实recipe-store准备/配置；②完整运行绑定注册/调用及公共TrayPose/预算输入；③状态尚未交的动作关联/轴/媒体增量和同源通知验证；④T025具名Test输入/源摘要/唯一联合驱动；⑤共同架构门禁和脚本消费者清理回执。当前源码可以继续被011接入；保存002、T021/001及本轮文档尚无实际接收/主项目产品合入回执，不宣称完成。

T020具体源码待核：012 RecipeEndpoints.BuildPlan仍有GetHandoffAsync旧V1读取后规划分支；目的取启动配置IPublicConfiguration而独立共同绑定随后读取运行冻结配置。011当前交付的IndependentRecipeApplication也仍接收V1。请后续共同绑定交付明确当前独立plan/bind的准入入口、应保留的历史读取与应删除的旧执行分支，以及冻结purpose/原期限调用清单。012不自行改变共同业务处置，不把这段早迁移当完整T020已交；不新增另一校验器/绑定器。

任务仍8/25已勾，未修改architecture勾选；新证据不能替代剩余条件。文档最新稳定镜像为`deliveries/implementation-status/002-state-consumption/`（5文件：plan-handoff、quickstart、shared-integration、editor-ui、tasks）；001-current镜像保留其发布时点。下方保存002“状态未交”只对当时成立。

## 保存002发布记录（原时点保留）

已核011最新tasks-handoff：前轮7份及I1—I4四份设计文档已实际接收；T019/001、002、003和D012-store-api-1.3/001源码已进入011独立副本，保存API两段合同增量已由011合入主项目文档。**主项目产品/测试源码仍无合入回执**。本节以下所有“待接收/NotRun/只有T001已完成”等均保留其当时事实，不作为当前结论。

012实际接收共同基础001—004，运行绑定001（44文件）和002-component-convergence（6文件）。逐文件SHA已核，回执receipt-D011-runtime-binding-001.json、002.json。002关闭此前xUnit2031编译阻断，完整Contracts工程构建后，012目录/两个适配器定向17项通过、0失败/0Skip；adapter-contracts-01.log失败保留。完整绑定仍部分交付，真实状态包及Integration共享消费者/夹具未收到。

**本次明确稳定交付**：`deliveries/D012-store-api-1.3/002-modal-preparation-source/`，manifest列17份源码/测试及20份证据，SHA-256 `621b95900c8000906f08619f6db05f40108377eb90c9f3a339176b3634d2fba8`。只取该稳定目录，不复制012在制树。包含原弹窗制作/完整读取编辑、实际API、精确原型门禁、冻结初始迁移模型、StorePrep受控Test准备入口和三类Integration测试定义。保存/API真实能力早批已交，不等联合验证；运行接线/状态就绪未交。002当前待011接收，不能宣称已合入。

实际验证（证据根artifacts/recipe-authoring-012/implementation-20261003）：

- Host06、StorePrep05完整受影响构建0警告0错误；frontend build05/typecheck05通过。011共同绑定源只表示可编译消费，不等于完整运行通过。
- 前端14项通过（11会话组件、边界、精确原型正例及五个变更负例）；无Skip。归档三页、无关区域、实际交付脚本均继续受保护。
- 正式弹窗经真实Host/独立SQLite完成关闭不写、新建、编辑、共同检查、完整保存/GET、重开及正常Host重启重读。四面＋独立E/用途点和六面配置已实际API/页面往返，未展示有效字段保留。page-authoring-03、page-rich-01中的四面结果、page-rich-02六面结果、page-choices-01及page-normal-restart-reread.json分别记录范围；测试输入未获生产批准，不是联合配方替身。
- API真实证明重复FCode409、版本冲突412、缺If-Match428、无权403、共同校验422、实际SQLite写锁503；新补真实读取故障GET/目录均503，无回退，恢复原索引后正文/Head不变（http-read-unavailable-01.json）。所有失败/修正前记录保留。
- StorePrep新增`--seed-test-recipes <allowedRecipeRoot> <recipeRoot> <inputFile> <expectedSha256>`，只初始化空配方库并共同验证明确Test来源、生成身份/摘要、完整序列化和真实事务。错误摘要/未批准输入真实拒绝且库仍为空；T025具名输入未收，成功初始化与联合链NotRun。不是生产审批端口、产品导入平台或第二正式目录。

**任务与限制**：当前仅T001/T002/T003/T004/T005/T006/T008/T012已勾（8/25）。T009—T017主体源码和独立HTTP/页面证据已可交，但Integration真实工程尚须011迁移后验证，T013/T017不提前勾；T019首批/目录组件通过不等于剩余清理完成。architecture原0/24标记不改。汇总见us1-authoring.json、us2-reread.json、verification.json、cleanup.json和joint-reference.json；不存在D012-ui-joint-evidence完成声明。

### 需要011的明确下一批（仅限制依赖部分）

1. Integration/Host共享消费者与夹具稳定文件：Station01HostFixture等须真实prepare独立recipes.db、注册当前同一提供者，配置RecipeStore:DatabasePath/ReadWriteTimeoutMs/DbLockTimeoutSeconds；不替换IRecipeStore、不排除正式编译文件。收到后012执行既定RecipeAuthoring窄过滤，完成T007/T014/T018及T013/T017收口。
2. D011-runtime-binding-1.3后续完整注册/调用和公共输入配置（Algorithms.TrayPose、budgetTrayPoseAlgorithm），供T020；不把现有001/002当整包完成。
3. D011-runtime-state-1.0实际查询/通知/阶段/处置/异常物理槽输出，供T021移除stageFacts、旧显示版本锁及S1/P01推断；authoring弹窗就绪不等于运行页面就绪。
4. T025具名Test正文、输入SHA和独立启动安排；012准备工具按源真实写库，随后使用正式保存/API和011唯一联合驱动同次采证。最终页面证据在运行后形成，不能作为启动前置。
5. 受影响架构门禁稳定增量及混合装配清理唯一责任：Infrastructure.csproj旧review文件输出、Host/appsettings.VirtualPlc.json旧Provider=Review、011共享脚本/夹具实际消费者。012不跨界修改。已删除与待删除边界见cleanup.json。

### 文档交付与环境收尾

本次文档增量为本plan-handoff、quickstart、contracts/shared-integration；tasks保留25个ID及实际8项勾选，随文档稳定镜像交付。此前设计已接收事实和全部历史记录保留。主项目由011唯一集成；稳定镜像见`deliveries/implementation-status/001-current/manifest.json`，未获新回执前保持待接收。

012自用Host01—04均已正常关闭，日志记录保存/读取失败和生命周期关闭；没有终止011进程，未运行PLC/联合链，未操作主项目运行库。仅在012工作根改产品/测试/文档和独立证据，没有Git写操作。当前外部前置缺失的任务保持未完成；收到稳定交付后继续。

## 早批历史记录（按各发布时点保留）

## 当前已接收运行绑定早批及需011修复的编译问题

已核摘要接收D011-runtime-binding-1.3/001-observation-and-handoff-source的44文件，回执receipt-D011-runtime-binding-001.json；整包仍部分交付，无真实状态包或联合输入。Host结合012当前提供者再次完整构建通过，host-build-05.log，0警告/错误；本次无在运行的Host锁文件重试。

首次完整Contracts工程构建（仅过滤012的三类适配/目录测试执行，未排除编译文件）失败，未启动任何软件用例：011所有的backend/tests/Gaode.Contracts.Tests/Recipes/RecipeExecutionCoordinatorTests.cs第57、71行触发xUnit2031，要求Assert.Single(collection, predicate)，不能先Where再Single。日志adapter-contracts-01.log保留。请011在稳定小批修复交付，012不跨界修改、不压制分析器、不宣称0测试通过。自身RecipeCatalogTests已稳定交T019/003，待接收。

另交具体清理承接：backend/src/Gaode.Infrastructure/Gaode.Infrastructure.csproj仍复制Recipes/catalogs/recipe-catalog-review.json到正式输出；backend/src/Gaode.Host/appsettings.VirtualPlc.json仍声明Recipes.Provider=Review。这两个混合装配文件未在012唯一责任表列明，交011统筹唯一编辑/移除无效项；012暂不同时写整文件。backend/src/Gaode.Infrastructure/Recipes/catalogs/recipe-catalog-review.json目录内容已无有效产品代码调用，但待装配项实际删除及011脚本/共享夹具迁移回执后收口。历史specs输入和历史reader不因该清理删除。

## 当前补交：T019/003-catalog-tests

011最新交接确认RecipeCatalogTests.cs归012。现已按其要求迁移并发布稳定单文件批`specs/012-recipe-authoring/deliveries/T019/003-catalog-tests/`：保留当前目录/配置保真、错误点位关联、未批准槽及历史来源只读保护，实际删除旧Review、Items/Resolve/Build、自动PLC号、错误四面/测高/无复查断言。7个用例定义NotRun，首次整个Contracts工程构建仍须011共享测试迁移；不排除旧文件凑编译、不扩展组合验收。manifest摘要可直接消费，当前待011回执。

弹窗实际浏览器第二批验证已通过：关闭丢弃未保存改动、完整编辑PUT且未改字段保真、选用已读取配置经POST新建及完整GET、关闭重开重读，见page-authoring-02/summary.json及截图。真实Host和SQLite，无假API；验证输入未批准，仅证明制作保存，不冒称联合F或生产通过。首轮页面报告遇浏览器条件GET返回304，实际保存已成功但采证脚本缺正文；已把配方fetch固定no-store，以每次完整GET核对并保留page-authoring-01/failure.json。首轮并非四项全通过。

## 当前稳定保存/API早批：D012-store-api-1.3/001-store-http-source

稳定包`specs/012-recipe-authoring/deliveries/D012-store-api-1.3/001-store-http-source/`已发布，15源码/测试定义/合同文件及10份证据逐一SHA-256。README详列实际配置、StorePrep入口及未完成范围。真实HTTP/SQLite保存、完整重读、编辑并发、必要拒绝和正常Host重启已证；页面/共享测试工程/绑定/状态/联合链未通过，T017仍未勾。此为及时提供可接入源码子集，不把源码交付冒称整项完成。011统一接收/集成，当前尚无本批回执。

同时已接收011稳定004-stage-identity三文件及RecipeStageIdentity共同入口，摘要见receipt-D011-004-stage-identity.json；T002共同基础接收完成，已勾选。011已实际接收T019/002-frozen-catalog（其receipt-D012-T019-002.json），未合主项目代码。前节阶段标识缺口已关闭；受控配置来源沿正式已存正文/明确输入，无新的生产配置目录API，现场来源缺失如实限制相关新建。012配置选择经实际GET取得完整正文，复制选中参数及来源，不复制Approval/ReleaseStatus、不生成计划。

当前任务勾选T001/T002/T003；其余仅有局部证据，不以代码写完提前勾选。HTTP证据及最近构建情况以本批README为准，以下早批NotRun保留当时时点。

## 当前稳定增量交付：T019/002-frozen-catalog（历史时点）

稳定根`specs/012-recipe-authoring/deliveries/T019/002-frozen-catalog/`，两文件及摘要见manifest。JsonRecipeCatalog/SemanticRecipeInputProvider改用共同Create深冻结；Host/Infrastructure实际可构建，适配用例仍NotRun。001已被011接收，本002尚待回执。T019整项未勾，后续运行和清理分别承接。

## 当前增量：003接收与首次可构建范围

已按稳定003-direct-consumers manifest核SHA-256并接收42文件，回执`artifacts/recipe-authoring-012/implementation-20261003/receipt-D011-003-direct-consumers.json`。012已迁移Program去TrayCodeMapPath参数、RecipeEndpoints改共同Matcher/BuildExecutable及新RecipeBindingReceipt，删除旧PLC绑定端口和RequiredEvidenceCommit直引用。Host在012完整工程构建通过（host-build-02.log，0警告0错误）；首轮局部变量重名失败已修正，host-build-01.log保留。003不是完整运行绑定/状态包，Host可构建不等于运行接线或联合链通过。

011最新tasks-handoff已确认T019/001-adapter-types六文件实际收到011副本；其回执receipt-D012-T019-001.json。没有主项目产品代码合入记录。准入问题已答复关闭：新建draft/空Approval，更新保持服务端受控准入；T025具名Test初始输入由011交，012只提供真实独立数据库的受控准备能力，之后走正式编辑保存/重读/F链；该输入仍待交，组件Recipe011Data不作替代，不新增审批端点。

**尚需011明确的消费输入（只限依赖部分）**：

- 共享测试工程直接消费者迁移尚未稳定交付；012不删除正式编译文件、也不跨界改共享夹具。Station01HostFixture等启动完整Host时须实际prepare独立recipes.db并明确RecipeStore:DatabasePath、ReadWriteTimeoutMs、DbLockTimeoutSeconds；配置名称与端口归012，011所有的夹具消费需同步。
- 编辑UI按RC08保持StageId及点位/采集/算法/运动来源只读。目前共同代码没有可直接消费的StageId形成入口（内部stage:number在Planner形成，编辑不能靠执行准入生成计划）。请交共同阶段标识形成/解析的实际入口，或明确现有可调用入口；012不实现第二身份算法。
- 本地现有后端只有公共配置校验，没有点位/采集/算法/运动选项读取入口。请列出实际受控配置输入/服务及版本引用来源（可与T025输入交付关联，不能由Test示例自动取得生产批准）。012可以从正式API/SQLite已有正文读取并复用完整配置字段；缺现场来源时只限制相关选择/依赖保存验证，不能用空来源补零或浏览器字典伪造。当前空白新建表单相关只读来源仍未齐备，不宣称完整制作已经通过。

前端npm build、typecheck与9项会话组件用例通过（component-authoring-refresh.tap）；只证明会话状态/失败/完整正文保留，不是正式SQLite或联合证据。原型守卫正在承接11处精确替换及实际构建资源，未关闭归档或无关区域保护。

## 当前接收事实与实施准备（早批记录，按时点保留）

**最新接收002-foundation-source**：已从011稳定包核SHA-256并接收全部10文件，回执在本批证据`receipt-D011-002-foundation-source.json`。保存基础实际源码已到：唯一ValidateForSave、身份/版本/摘要、IRecipeStore/IRecipeCatalog、唯一正文序列化、RecipeCatalogSnapshots.Create/Freeze、Matcher和Admission。012存储及适配器使用这些唯一实现；目录由Create深冻结。011直接消费者、BuildExecutable迁移后入口、绑定/状态仍待后续交付；没有运行后端构建或软件测试。下方001接收按当时时点保留。

**具体接入限制交011**：新建HTTP不能取得Approval/ReleaseStatus写权。当前存储按共同合同保存为draft且无批准元数据，合法保存不依赖设备，运行仍Restricted。实际联合输入需要沿011既有受控来源取得有效Test/生产准入元数据；002提供的IRecipeStore请求没有可信批准来源或服务端新建元数据形成入口。请给出既有受控来源及具体调用/消费方式，012不从浏览器候选复制批准、不自建审批/身份服务，也不自动批准新配方。此限制只影响F准入/联合链，不阻止无设备真实保存与重读。

- 已核011主项目`tasks-handoff-20261003.md`：此前7份和定向修订4份均已实际接收合入。当前8个不同012文档（两批有重叠）的主项目/012摘要全部一致；不再要求重复交付。历史“待合入”属于当时事实。
- 已接收`D011-common-code-1.3/001-fields`稳定4文件：RecipeContracts.cs、ExecutionInputs.cs、RecipeDefinitionSerialization.cs及其测试定义。逐文件核交付manifest摘要，来源为011副本`specs/011-plc-interaction-update/deliveries/D011-common-code-1.3/001-fields`，接收证据`receipt-D011-001-fields.json`。只从稳定包取文件，没有复制在制backend。当前T002仅部分接收；唯一校验/身份/摘要、Matcher/深冻结及011直接消费者迁移仍待下一批。绑定与状态两个批次尚未收到。首批源码/测试均由011声明NotRun，不宣称Host可构建。
- T001完成：从主项目复制backend/frontend/desktop/scripts/VirtualPlc/workflows和global.json、dev入口，共689个文件，4份已存在文档原样保留且摘要一致。另补473个缺失的小型现有配置/测试输入（specs的examples/fixtures/contracts中的JSON/CSV/schema）；它们仅为既有消费者资源，不是本功能已保存配方或新验证证据。逐文件来源/目标摘要见`artifacts/recipe-authoring-012/implementation-20261003/source-baseline.json`和`source-baseline-supplement.json`。
- 排除嵌套workcopies、Git、bin/obj/node_modules/dist/artifacts/TestResults、运行库、密钥/凭据文件和链接；没有覆盖012既有设计。所有工程引用在独立副本内，Host工程、前端package.json、global.json已核。SDK实际10.0.401，Node实际24.19.0；未初始化Git，无Docker/ESLint/Prettier发布配置需新建ignore。
- Spec Kit prerequisites显式设置本副本两个环境变量，实际返回012绝对功能路径。hooks为空。requirements 16/16、architecture 0/24（Notes已满足）；按本次明确授权继续，未修改清单标记。

## T003实际消费者与删除承接

静态核查证据：`artifacts/recipe-authoring-012/implementation-20261003/consumer-audit.json`；T003完成只表示消费者核查完成，不表示旧逻辑已替代或软件通过。

| 实际命中 | 替代/删除义务与负责人 | 必须保留或接收的有效用途 |
| --- | --- | --- |
| Program.cs注册RecipeCatalogFactory，RecipeCatalogOptions默认Review；工厂选择Review/File/Semantic | 012 T010/T019改为同一SqliteRecipeStore；替代后删正式文件目录/回退及失效配置 | Host启动schema检查、读取失败事实；不能以空目录兜底 |
| JsonRecipeCatalog及两个适配器；PointRefs、CoordinateRule、测高/单Flip构造；Semantic还重写批准摘要 | 012 T019首批按RC08源码迁移真正编译消费者，后段收敛合法适配用途；不凭名称整删 | 唯一共同序列化/校验，来源和历史读者；缺新用途点必须受限，不补零 |
| CapabilityRegistration注册DecodeReviewCode/ReadCodeMap；前者固定TEST-TRAY-0001换码，后者配置换码 | 此Composition文件011负责；提交接入需求：新F原文匹配不能再调用换码路径。012在共同消费者迁移到位后删无用途换码实现及错误测试 | 若其他合法输入格式仍需适配须由011列真实消费者；不把旧映射沿用为正式F规则 |
| Contracts.Tests的RecipeEnvironmentDecoderTests、SemanticRecipeInputProviderTests及Support/TestConfiguration/SemanticPlanFixture；Integration的RecipeBindingTestSupport、AssemblyComponentExecution、RecipeExecution010RunHarness、VirtualLoopTestRig、SingleFaceDetectionIntegrationTests等 | 012只迁移目录/两适配器测试；共享计划/执行构造由011交必需编译迁移。固定换码、测高依赖旧断言替代后实际删除；通用校验拒绝断言继续 | 不删除有效保护/历史失败；过滤测试不免除整个项目的编译依赖 |
| scripts/start-station01-virtual-loop.ps1的Recipes__Provider/CatalogPath；get-008-page-budget.ps1直接JsonRecipeCatalog；verify-latest-plc.py旧路径 | 联合启动及共享脚本011唯一维护，须收其正式SQLite来源/新字段消费迁移；012提供存储配置及工具参数 | 原截止/预算与合法历史查询；不启动旧Q脚本替代012保存链 |
| RecipeEndpoints内联旧plan/bind、PLC绑定ACK及Program注册 | 012 T010必要签名早批，T020接D011-runtime-binding-1.3后删被替代内联业务；011提供调用/注册清单 | 取消、原期限、实际提交、独立bind无续接许可及历史回执查询 |
| runtime.js旧expectedRecipeRef版本锁、S1/P01限定、stageFacts阶段推断、已有run禁止选用 | 012 T021消费真实绑定/状态后定向替换；编辑会话不依赖这些限制 | sameRecipeRef仍有冻结引用核查用途，不能批量删；localStorage仅不可信run引用并重新GET，保留 |
| a.html原弹窗MES/协议地址/角度/示例输入及flipToggle监听；runtime的选用监听 | 012 T011/T016替代为完整配方表单后删除无用演示输入/监听；不改变其他页面 | 原入口/关闭/风格；clearPrototypeDemo保护其他演示区域继续有效 |
| verify-prototype逐字相等、prototype-console浅标志且覆写us1-report、prototype-all-pages仅存在断言 | 012 T012承接精确授权差异及正负例，输出本批路径；替代无效断言 | ZIP/三页/无关区域保护；原失败证据不删除 |

## 早期分批状态（历史）

仅T001、T003有完成证据并勾选。保存/重读/绑定/状态/联合运行均未验证；T019整项保持未勾。先推进无共同源码依赖的界面与测试准备；收到稳定基础批立即核摘要接收并执行首批迁移，不等待运行批次。需要011提供的共同源码、直接消费者清单、注册调用及状态输出按现有三个D011名称分开记录，不新增替代实现。

本实施增量尚未取得011接收回执。后续稳定交付记录将列明实际文件/摘要、构建范围、结果和未完成部分；准备代码不能作为`D012-store-api-1.3`或联合就绪。

### T019首批稳定源码交付：001-adapter-types

从`specs/012-recipe-authoring/deliveries/T019/001-adapter-types/`读取，不复制在制backend；manifest逐文件SHA-256。6个文件：RecipeEnvironmentDecoder.cs、SemanticRecipeInputProvider.cs、JsonRecipeCatalog.cs、RecipeCatalogFactory.cs及两适配器测试。

已落实：共同正文2只经唯一序列化；当前检测配置为显式XYZ；用途点、逐阶段Flip、额外E完整保留；改GetSnapshot端口；删除固定TEST-TRAY换码、ReadCodeMap、旧测高/单Flip/PointRefs重建、旧Review默认及借加载授批准。保留有实际消费者的JSON/固定坐标表输入适配，只接受明确当前格式，供显式输入/组件，不作为Host正式目录。表格式版本为semantic-recipe-input/2，完整列头在源码常量；只做格式/完整性绑定，共同业务校验由011统一负责。

需要011承接的直接消费者：CapabilityRegistration和Support/SemanticPlanFixture的旧DecodeReviewCode注册改为DecodeTrayCode（原文进、原文出，无换码）；ReadCodeMap正式F路径删除；共享测试/执行构造改GetSnapshot＋共同Matcher，旧review/测高输入不得假升级为当前可执行配置。保留合法历史payload读者，不改写历史证据。012自己的新适配测试定义消费001-fields中的Recipe011Data组件输入，不把该输入用于正式保存或联合验收。

**状态：源码交付，构建NotRun，测试NotRun；T019不勾选。** 当前还缺011唯一校验实现及所属直接消费者迁移，012 Host直引用/SQLite接入将在后续批次完成。此包不宣称T019整项、T017或T020完成；后续变更另发增量，已交包不覆盖。

---

## I1—I4定向修订交接（历史全文）

日期：2026-10-03。工作根：E:/dzk/gaode-012-recipe-authoring；显式SPECIFY_FEATURE_DIRECTORY=specs/012-recipe-authoring。当前只定向修订文档，随后冻结文件做speckit-analyze只读复核；不生成任务、不实现、不复制源码。复核结果只在会话报告，不回写文件。

## 当前接收与本轮增量

已读取[011主项目最新tasks-handoff](E:/dzk/gaode-1/specs/011-plc-interaction-update/tasks-handoff-20261003.md)的“当前交付：跨会话依赖定向修订”及此前7份接收记录。**此前D012-G01-receipt-1.3的7份已实际接收并合入**，本轮写入前逐文件只读核对主项目与012摘要均一致。G-01保持关闭，设计接收不等于共同代码、存储、运行接线或软件证据已交付。

011已先行正式发布共同基础、D011-runtime-binding-1.3、D011-runtime-state-1.0、T017保存首批、T019/T020分别交付和联合启动/完成条件，以及首次构建必要消费者清单。本轮012按该说明及用户明确指令消费；011所属迁移仍由011实施，不跨文件写入。

**发布范围需区分**：本节落稿核到011 tasks-handoff已经先行发布，但主项目tasks.md尚为旧依赖正文（交付表仍扩大D012-store-api-1.3，T027仍把最终页面证据列依赖，尚无D011-runtime-binding-1.3命名）。其交接第“本轮交接与主项目同步”节也明确其他活动文档稍后同步。故可以确认分批说明已交付，不能在此宣称011所有活动正文已经同步；后续只读复核以再次读取实际内容为准。实际源码、构建与验证证据仍全部未交付。

## 先前7份实际合入依据（保留时点）

| 同名相对路径 | 011实际结果 | 本轮写入前核到的主项目/012共同SHA-256 |
| --- | --- | --- |
| specs/012-recipe-authoring/contracts/editor-ui.md | 已合入；本轮写前一致 | 35d0f18386aad749a9e1ea5f9b34a7e436f0393788db8135718ecd76b1d0eef8 |
| specs/012-recipe-authoring/data-model.md | 已合入；本轮写前一致 | 6a13995d5ca91240a14e71688306f8e324e55b56505767f94ee4259320d4e4e2 |
| specs/012-recipe-authoring/contracts/recipe-authoring-api.md | 已合入；本轮写前一致 | dafc232eba5f8dd3e0f5f5eb97c26af681902e3da0f3233933a7f33f3c45f292 |
| specs/012-recipe-authoring/contracts/shared-integration.md | 已合入；本轮写前一致 | 39538efa88a512c58a068a53fbcbc60a62d0152dc233a19c86552ffc3a8fc20f |
| specs/012-recipe-authoring/checklists/architecture.md | 已合入；本轮写前一致 | 35344be54fc658660584ceae172ca280bb4c98d148659d3694b7f2f02802f58a |
| specs/012-recipe-authoring/tasks.md | 已新增合入；本轮写前一致 | e3a0b8070e7630bb65fd4999400f0aea1eae176b412eb42c6b1f0d5808d67ba4 |
| specs/012-recipe-authoring/plan-handoff.md | 已合入；本轮写前一致 | 1c113d9e3291ba06581f374c70f4e8172208ccce11a687b731c1266a88ab7649 |

这7份不再列作未收到；下方原tasks轮“待接收/待合入”保留当时事实。本轮对其中3份和quickstart的后续修改是新的4份交付，尚无011接收回执，不沿用以上摘要宣称已经集成。

## 本轮直接读取的011发布依据

来源根E:/dzk/gaode-1，只读；未将旧来源快照反向覆盖，也未写011副本。以下摘要固定本次修订所读发布时点，后续011同步不倒改这一历史记录。

| 相对路径 | 读取SHA-256 |
| --- | --- |
| specs/011-plc-interaction-update/tasks.md | 3FE90DFECA8FDC370D76BB0D463A138513BF27FEBDAD1351D1EC00DEE558CCD4 |
| specs/011-plc-interaction-update/contracts/recipe-contract.md | E60D8F9F26F96F905B3CC210A938C630E50CE5F810F5B9275104758E678B4383 |
| specs/011-plc-interaction-update/tasks-handoff-20261003.md | 9158520DBE4FC7B99509F37BA20B5EADC7CECAC1977D9B66EB05815787CDF604 |

共同业务仍recipe-contract/1.3、状态station01-execution/1.0、验证011-verification/1.1。spec/plan的旧G-01待补、未生成tasks等阶段文字及原architecture评价按既有时点理解，不重做这些文档。

## I1—I4修订落点（待随后只读复核）

| 问题 | 本轮实际修订 | 限制与责任 |
| --- | --- | --- |
| I1 共同代码范围混同 | tasks T002只接共同基础，T018只接实际Matcher/深冻结；T020接011 T011/T012的D011-runtime-binding-1.3及注册调用，T021接T015的D011-runtime-state-1.0；shared-integration分表 | 已消费011正式分批说明；对应代码尚未交付，不把契约当可用实现 |
| I2 T017交付扩大 | T017提前交真实保存/完整重读/Version/ETag及必要证据，明确未完成范围；T019适配、T020运行接线按各原任务分别补交 | 不等T022才首交；不把T017当运行整包就绪；011活动tasks同步仍需核对 |
| I3 最终证据反向成启动前置 | T022分参与前置与运行后完成证据；页面/API和采证准备就绪后，011唯一启动，012同期采证；D012-ui-joint-evidence仅控制完成声明 | 保留同次单面/多面和必要差异组件，不另建链；缺证据不勾T022。011正式说明已明确，活动任务依赖需最终对齐 |
| I4 首次构建迁移和测试循环 | T019首批只依实际RC08类型/序列化/消费者清单及本方核查，后段才依T009；T010承接Host直接签名/DI。T013/T017和quickstart明确首次构建/执行条件，测试定义不要求先运行通过 | 011 T005基础批带其所有的必要消费者/测试迁移；012只改自身文件，不补零/兼容旧字段、不移出正式编译。T019分批不等于整项完成 |

25个任务ID及全部勾选保留；T019仍一个任务，未新增子任务勾选。接口、字段、正常保存不依赖设备、生产限制、完整重读/并发拒绝、冻结隔离、状态投影、必要删除及QV-01—05范围保持。

## 精简实施顺序与正常前置

后续获实施安排后：准备完整独立源码并核消费者 → 接收已实际交付的RC08共同类型/基础和011必要消费者迁移，同期开展T019首批及测试定义 → 实现SQLite/HTTP/弹窗，承接Host实际命中的签名/DI → 满足所选工程迁移条件后执行T013/T017并先交保存读写 → T019后段与T020绑定/T021状态按各自实际交付接线 → 011唯一启动，012 T022同期采证，运行后各自收敛证据及清理。011的启动仍遵守其T025/T026和双方必要清理条件。

基础代码未交只限制依赖实现；绑定/状态分包只限制相应接线；现场地址、新3D等只限制相应动作和联合证据。完整源码副本准备仍是实施前置，本轮未执行。测试可先定义，实际构建/执行依对应实现和真实编译引用，不让测试通过反向阻止其实现。T019后段、T020或T022未完成不妨碍已有子集按事实交付，也不据此勾整项。

## 供011统一合入的本轮确切文件

相对012工作根，目标为主项目相同路径。**以下4份全部待011实际接收/合入**，只能逐文件审查合入；未列文件不重交。

| 相对路径 | 本轮增量 |
| --- | --- |
| specs/012-recipe-authoring/tasks.md | 保25个ID和勾选；分包前置、T017子集、T019分批、首次构建条件及T022参与/完成分开 |
| specs/012-recipe-authoring/contracts/shared-integration.md | 011实际生产任务/分包、唯一责任、首批消费者及联合启动/证据边界 |
| specs/012-recipe-authoring/quickstart.md | 仅补首次构建条件和联合采证顺序；既有命令与QV最小范围保持，未执行 |
| specs/012-recipe-authoring/plan-handoff.md | 已合入7份回执、本次来源时点、修订位置、4份待交与停止点 |

本轮不改产品/测试代码，不复制源码，不运行构建/测试/设备/数据库或Git写操作，不改feature.json及任何任务勾选。修订结束后只读analyze，不自动修复或进入implement。

---

## 原tasks轮交接全文（历史保留）

以下全文的“当前/本轮/待接收”属于原任务拆解时点；7份现已实际合入，本轮4份新修改的状态以上文为准。

### 012任务拆解与G-01消费交接

日期：2026-10-03。实际工作根E:/dzk/gaode-012-recipe-authoring。当前为tasks轮，以下当前节替代后方历史交接的版本/未接收/未合入状态；不倒改历史事实，不重做整套plan/checklist。

## 当前消费结论：D012-G01-receipt-1.3

已读取主项目共同**recipe-contract/1.3**及011最新[tasks-handoff-20261003](../011-plc-interaction-update/tasks-handoff-20261003.md)。RC08已给本轮要求的准确类型、所属路径、可空性、配置来源及序列化/旧字段迁移；G-01设计消费已关闭，无影响012任务拆解的剩余实质矛盾。G-02身份/摘要规则和G-03独立SQLite选择保持关闭，没有重开。

| G-01核对项 | 011已交准确落位 | 012实际消费位置 |
| --- | --- | --- |
| Pick/PutBack及用途坐标 | RecipePurposePoint/RecipePointPurpose、ObjectExecutionInputs.PurposePoints；RecipeFlipInputs.Stages[stageId]的RecipeFlipTransition及两个局部引用；检测Coordinates独立 | editor-ui G-01表；data-model完整正文；API透传；T009/T011/T019，不把同位点合并用途 |
| E引用与对象归属 | ECode.ScanPointRef、RecipeExtraScanPose完整字段、ReadAt/ExtraPose互斥及代表Material的检测/机械所有者 | editor-ui明确普通E与额外E、成组/整体归属；缺点不跨对象兜底，不从示例自动填值 |
| TargetPose | RecipeTargetPose(ProfileId,ProfileVersion,PoseKey)；profileId关联MotionProfile，语义引用来自受控配置，非PLC数字 | 表单选择/只读来源→targetPose对象→完整读回；设备映射未齐只限制依赖运行 |
| SchemaVersion及序列化 | recipe-definition/2、recipe-catalog-snapshot/1、execution-inputs/2；合同修订recipe-contract/1.3；011唯一RecipeDefinitionSerialization.Serialize/Deserialize、camelCase与字符串Purpose | data-model/API确定同一正文及版本位置；T002接共同实际代码，不另写DTO/领域编解码 |
| 旧Stage/输入迁移 | 删除当前CoordinateRule、相机PointRefs、测高输入和单Flip旧字段；保有效Number/Targets/Action及有实际用途AngleDeg/Rotation/历史读取 | editor-ui正文已修正；T003/T019/T023先核消费者、补真实用途输入后迁移，不静默裁剪/补默认值 |
| 元数据与完整往返 | 新候选身份可空字符串、PlcRecipeId=null；实际Saved身份非空，受控批准只读；StageId/计划等由共同后端形成 | editor-ui输入/只读分类及API写权；完整Definition经统一序列化进入SQLite再完整GET，保未编辑/未展示有效内容 |

1.3未改变IRecipeStore、服务端身份/摘要或ETag→ExpectedVersion保存语义。结构片段RC08.5不是可保存配方/生产批准/默认值。当前关闭的是字段设计消费；**实际D011-common-code-1.3尚未接收、软件未实现或验证**，任务按具体前置执行。

## 本轮接收来源与实际摘要

只接收下面5份明确交付Markdown，同名相对路径从E:/dzk/gaode-1复制到012工作副本，写后核SHA-256一致；没有覆盖自己的006/012/前端文档。此前basis-receipt记录仍保留其接收时点，本轮用本表追加当前依据，不重生成整套来源包。

| 相对路径 | 本轮接收SHA-256 |
| --- | --- |
| specs/011-plc-interaction-update/contracts/recipe-contract.md | D0142FA41ACEBBE0D5704719A4CF2F75B21BD6880DD3A96642BAC5C8F7DC2D6A |
| specs/011-plc-interaction-update/contracts/execution-and-state.md | D223768D3CD0BE5E29CB3B3B0F09DE8B060F273F28615C6957358CA7C1CD1C3D |
| specs/011-plc-interaction-update/contracts/verification.md | 87C5CA6716774560D60EB2AF4293F133DDEC0F578EC492E7A0002F463A4884DC |
| specs/011-plc-interaction-update/clarification-sync-20261003.md | 985EDE66FC774C3AC610EDA83ED61BB08D91E2ED55A938632E31E2AF966901EA |
| specs/011-plc-interaction-update/tasks-handoff-20261003.md | 5D58BF2C11DEBB577DDB028F7232A8202BA13276261E5CE3CD5D58B0EFB0E636 |

状态合同仍station01-execution/1.0、验证合同仍011-verification/1.1；本轮读取其当前1.3引用。四份混合API的既有公开职责继续，由011唯一维护；012不改它们。plan/spec/research/quickstart/requirements的前轮阶段摘要本轮不重写：其中G-01待补/未生成tasks等描述是前轮时点，当前设计状态见本节和受影响合同，技术方案与业务范围未重新生成。

## 实际主项目接收与本轮待交接的区别

011最新tasks-handoff记录此前012最终13份（共同1.2）已经实际接收、逐文件合入；本轮只读核13份主项目摘要全部与该报告所列一致。此前的15份澄清、首版历史收据以及最终13份不是重复待办。**这不意味着当前1.3消费增量或新tasks已经被接收**。

本轮形成D012-G01-receipt-1.3，待011实际接收/统一集成；后续D012-store-api-1.3及D012-ui-joint-evidence仍待实现。共同基础与运行接入代码分别按D011-common-code-1.3实际交付范围登记，不猜011任务ID，也不复制其执行任务。RecipeEndpoints.cs、Program.cs、RecipeEnvironmentDecoder.cs、SemanticRecipeInputProvider.cs仍012唯一编辑，共同类型/身份/摘要/序列化/校验/绑定/执行/通信归011。

## tasks及质量清单的当前变化

已生成[tasks.md](tasks.md)，共**25项**：准备3、基础3、US1制作保存7、US2重读编辑4、US3 F消费/冻结与状态5、收尾3；T001—T025全部[ ]。任务逐项给文件、唯一负责人、真实前置、需求/宪章、完成条件与QV/M证据。四组可并行机会均有相同已满足前置及不重叠文件：T004/T005、T007/T008、T015/T016、T020/T021；同Host/runtime文件后继串行。

MVP为准备/基础＋US1（T001—T013），证明无设备依赖的正式弹窗真实保存；US2补完整编辑/并发/重启读取，并由T017先交D012-store-api-1.3供011接入，T022再收敛联合证据，避免互相等待。US3与011同次单面/多面链核F/冻结/更多面/E/三区/异常槽，完整012不能以MVP替代。最小验证只承接QV-01—05及011-verification/1.1，不重复完整链、不全量、不穷举。实际删除在替代任务及T023承接，消费者/保护/历史读取和失败证据保留。

architecture只在Notes追加CHK002复核：原部分满足→当前满足。连同其他23项保持评价，当前辅助汇总**24满足、0部分满足、0不满足**；24个复选框全部仍未勾选，历史21/3、23/1保留，需求清单历史16个勾选未改。文档评价不是软件验证或实施放行。

## 供011统一合入的确切文件

下列**7份**从012工作根交付至主项目相同相对路径；当前全部**待011接收/合入**。其中6份定向修订、1份新增tasks；只逐文件核差异合入，不整目录覆盖。接收来的5份共享来源和本副本feature.json不反向提交。

| 相对路径 | 本轮修改内容 |
| --- | --- |
| specs/012-recipe-authoring/contracts/editor-ui.md | 按RC08替换旧PointRefs/Flip/坐标/Stage映射，新增字段→输入/只读→正文→完整重读表 |
| specs/012-recipe-authoring/data-model.md | 正文2/快照1/冻结2/合同1.3及统一Serialize/Deserialize消费，保全有效字段和历史迁移边界 |
| specs/012-recipe-authoring/contracts/recipe-authoring-api.md | 正文2请求/响应完整透传、当前字段删除/历史保留、候选空身份及既有ExpectedVersion语义 |
| specs/012-recipe-authoring/contracts/shared-integration.md | IC-01/G-01设计消费关闭，D011共同基础/运行部分及D012交付名称，唯一文件责任 |
| specs/012-recipe-authoring/checklists/architecture.md | 仅在Notes追加CHK002/G-01复核，保留全部历史评价和未勾框 |
| specs/012-recipe-authoring/tasks.md | 新增25项全未勾任务、故事/依赖/并行/需求覆盖与最小证据 |
| specs/012-recipe-authoring/plan-handoff.md | 本轮消费回执、前轮实际合入、本轮确切待交文件及停止点 |

## 路径核对、文档检查及停止

setup-tasks仅在012工作根执行，显式SPECIFY_INIT_DIR=E:/dzk/gaode-012-recipe-authoring及SPECIFY_FEATURE_DIRECTORY=specs/012-recipe-authoring；返回FEATURE_DIR为E:/dzk/gaode-012-recipe-authoring/specs/012-recipe-authoring，TASKS_TEMPLATE为本副本.specify/templates/tasks-template.md，AVAILABLE_DOCS为research/data-model/contracts/quickstart。脚本只维护本副本功能选择信息，未切主项目feature.json，无Git写操作。

静态核对结果：25项格式、连续编号、故事标签及25行追溯一致；依赖无环且无缺失任务，4组并行文件不重叠，FR-001—020及SC-001—008均有承接。7份交付文档的本地文件链接均可解析。5份接收来源与主项目当次SHA-256一致；本副本仅7份012文档和5份接收来源发生变化，无删除，feature.json与006、spec/plan/research/quickstart/requirements保持原摘要。architecture原文完整保留，仅追加Notes。

本轮仅文档：没有准备源码副本、改产品/测试代码、构建、运行测试/设备/数据库或执行implement。以上静态文档检查不算软件验证。before_tasks/after_tasks均hooks为空，无钩子执行。当前停止在任务拆解完成；下一阶段为speckit-analyze，未自动调用或进入implement。

---

## 前轮checklist交接全文（历史保留）

下方“当前/本轮/待接收”均指当时checklist交付状态；此前最终13份现已被011接收合入，1.3新增量仍按上方当前表等待交接。

### 012 Phase 1设计与architecture审阅交接

日期：2026-10-03。实际工作目录E:/dzk/gaode-012-recipe-authoring；显式SPECIFY_FEATURE_DIRECTORY=specs/012-recipe-authoring、SPECIFY_INIT_DIR指向本副本。check-prerequisites -Json -Template checklist-template返回FEATURE_DIR为本副本specs/012-recipe-authoring；本轮不调用setup-plan或重建plan，不改feature.json。

## 当前交付状态

| 状态项 | 当前结果 |
| --- | --- |
| 共同合同接收 | 先接收81份，收尾接收1.2明确交付的18份刷新及2份新文档，累计83份不同共享文件，摘要见basis-receipt；未覆盖自己的006/012/前端文档 |
| 当前共同版本 | recipe-contract/1.2、station01-execution/1.0、011-verification/1.1；四份混合API见下方IC接收 |
| 前轮澄清主项目集成 | 011报告B1确认012此前15份已合入；最新design-alignment另确认Phase 1首版已接收、历史basis-receipt已原样合入。最终修订接收另列 |
| 本轮设计增量 | 下列13份012文档为本轮最终修订/新清单；首版已接收，最终版待011接收及唯一合入，不声称已回写主项目 |
| 已解决历史问题 | 宪章页尾8.0.0、008旧展示版本锁、混合API未交付、两个输入适配责任及G-02/03均已关闭 |
| 需求清单 | 历史16/16保持，未改变勾选；不能覆盖设计/软件结论 |
| architecture辅助评价 | 新建24项，全[ ]；Notes当前23满足、1部分满足、0不满足；保留首次1.1时21/3结论；正式审阅0/24 |
| 实现/软件证据 | 未改产品或测试代码；未生成012 tasks；本轮构建/测试/设备/数据库执行数均0，无Git写操作 |
| 停止点 | 定向设计与辅助清单审阅完成，等待调度设计审查；不自动进入tasks或implement |

## IC-01—06接收和剩余具体差异

IC-01已接收RC01—03共同字段并完成全字段编辑映射，G-01新类型/序列化细节局部未齐；IC-02已接收1.2统一身份/版本/摘要函数规则，G-02关闭；IC-03唯一校验、共同保存结果已接收；IC-04快照/Match/纯软件绑定/冻结合同已接收；IC-05实际投影和四混合API已接收；IC-06联合验证合同已接收，实际软件证据仍NotRun。

| 共享定义 | 当前消费版本 |
| --- | --- |
| specs/011-plc-interaction-update/contracts/recipe-contract.md | recipe-contract/1.2；RC01—07 |
| specs/011-plc-interaction-update/contracts/execution-and-state.md | station01-execution/1.0；EX04/05为现有界面绑定依据 |
| specs/011-plc-interaction-update/contracts/verification.md | 011-verification/1.1；M01—M11，与012 QV共用 |
| specs/001-station01-public-preparation/contracts/api.md | s01-api/1.2 |
| specs/003-plc-latest-protocol/contracts/station01-main-flow-api.md | station01-main-flow-api/1.1 |
| specs/003-plc-latest-protocol/contracts/status-notifications.md | s01/notification/2.0 |
| specs/008-recipe-driven-inspection/contracts/api-results.md | s01-recipe-api/3.0 |

不再要求011“交付全部共同字段/接口”。具体请求及限制：

1. **G-01仍局部未齐**：011在recipe-contract/RecipeContracts/ExecutionInputs补新增Pick/PutBack等用途字段的嵌套、类型、可空性和序列化路径、TargetPose引用类型、Snapshot.SchemaVersion及旧Stage字段迁移。012只限制这些字段绑定/磁盘编码，不另造类型。
2. **G-02已关闭**：1.2 RC04.1交RecipeDefinitionIdentity.CreateRecipeId/CreateVersion/ComputeDefinitionDigest及唯一SHA-256规范化责任。012在写边界调用共同实现，不自己生成另一套版本/摘要规则；实际代码由011后续实施。
3. **G-03已关闭**：011 plan/research及design-alignment D01已同步独立SQLite，接收其当前正文，不重复要求修复旧快照。
4. RC04.1消费回执：一个SqliteRecipeStore实现共同IRecipeStore/IRecipeCatalog；新建TargetRecipeId/ExpectedVersion为空，更新TargetRecipeId来自路由、ExpectedVersion来自原GET的ETag，RequestId仅关联。无SaveId或012专用保存端口。检查允许新建候选缺只读身份；保存调用共同身份/校验。真实COMMIT确认后Saved，同源后续读取可见；不增加提交后重读/缓存发布门，已提交但回包/重读失败另列。
5. RecipeEndpoints.cs、Program.cs、RecipeEnvironmentDecoder.cs、SemanticRecipeInputProvider.cs由012唯一编辑；011交共同语义迁移/消费者/注册调用。共同RecipeDefinitionIdentity、模型/校验/匹配/绑定/执行、通信及Host Composition归011。保存正文CatalogDigest为提交时来源，F冻结用Matched当前视图摘要，不能从旧展示来源锁F。
6. 012正式API/SQLite/弹窗证据与011同次单面/多面链共用；当前未运行。新增EX01.1明确3D观察生产/适配/消费归011；真实外部3D新输出尚未取得只限制依赖链，012仅消费状态，不另实现姿态判断。

本轮收尾核对发现011发布明确交付的新1.2，因此在停止前接收并定向更新；最初按1.1发现的G-02/03不继续当作当前缺口。011 design-alignment D01—D06要求的最终消费修订已在本交付落表，**等待011实际接收回执**，不代其关闭跨会话集成。

## 后续实际删除与保留

| 候选及实际消费者 | 必须落实的替代/删除 | 继续保留 |
| --- | --- | --- |
| runtime.js阶段显示；当前Sorting依赖UnloadPreparationCompleted | 用011真实阶段替代后删除旧推断 | 真实Final/结果/来源事实，无本地完成猜测 |
| runtime.js旧选用/expectedRecipeRef及S1/P01分支；后端StartRunContext/StartPublicPreparation也消费 | 012删无用途UI限制，011替代后端旧version钉死；不能只删前端假装修复 | 合法场景/身份关联、冻结页头sameRecipeRef有效用途、准入/取消/期限 |
| a.html演示PLC地址、固定角度等表单及flipToggle事件 | 新编辑替代后从实现副本实际移除无用途输入/监听和假成功 | 归档/V3只读；clearPrototypeDemo仍有有效页面消费者的部分 |
| Program默认Review构造快照、RecipeCatalogFactory、JsonRecipeCatalog、RecipeEnvironmentDecoder（012） | 正式链改同实例SQLite当前来源，删除无用途启动快照/回退；012按011语义要求迁移格式 | 核VirtualLoopTestRig、AssemblyComponentExecution、FileProvider契约/脚本及历史消费者后保留实际有用适配；不成为第二正式目录 |
| SemanticRecipeInputProvider码映射/旧测高（012唯一编辑） | 011提供语义迁移及CapabilityRegistration、Semantic测试、010Harness消费者要求；012承接后实际删除无用途换码/测高旁路 | 有效类型/坐标输入适配，不因Test名字删除整个适配层 |
| RecipeEndpoints内联plan/bind（012）；旧IPlcRecipePort绑定、DeviceApplied/BeginSpecialAction（011） | 012接RC05.1的共同软件绑定；011移除旧配方ACK/假设备完成及无用途占用。012不跨职责改共同服务 | RecipeBindingReceipt真实Intent/Bound/Handoff提交、原预算/t0/取消/准入；独立bind仍不续接检测；历史reader不补写假新记录 |
| verify-prototype全页字节相同及浅标志“unchanged”测试 | 精确授权差异与有效负例替代失效断言，删除无用途测试/开关 | ZIP/三页/无关区域/离线资源及前后端边界保护，历史失败证据 |
| 新存储及旧共同业务测试 | 011调整RecipeExecutionBoundaryChecker真实可达路径及旧非法四面断言，012补必要保存回归 | 不能以失败为删测理由或增加错误白名单；历史Q/报告留原义 |

核对调用、装配、配置和脚本消费者后，真正无用途者必须实际删除，不以注释、永久开关、备用实现或兼容层保留。runtime localStorage目前仅不可信run引用及重读，不是配方库，不在无条件删除清单。当前仅登记义务，未删代码。

## 最小验证与准备度

仅受影响构建、QV-01保存/完整重读、QV-02必要拒绝/版本冲突/真实失败及未知、QV-03和011共享唯一匹配/冻结/更多面/E/处置证据、QV-04授权原型差异及架构、QV-05删除后必要回归。quickstart给出M01—M11映射；不重复整链、不全量、不穷举、不重跑009/010历史专项，漏跑/失败/Skip/零发现和旧报告不计通过。

已确认需求及012主要设计具备继续设计审查的准备度；不是无条件任务/实现放行。G-01仅限具体新字段编码；G-02/03已关闭，剩余跨负责人状态是011接收本次最终修订/主项目合入；其他设计可独立成立。实施前仍须准备完整独立源码副本并接收011共同实现，当前是文档副本；Station01Test认证及011现场地址/恢复/安全延期只限制对应环境/动作，不靠默认值填补。

## 供011逐文件合入的最终设计增量

下表全部相对E:/dzk/gaode-012-recipe-authoring，目标为主项目相同相对路径；**13份均待011接收/合入**。采用逐文件审查合并，不整目录覆盖；主项目spec/requirements/clarification已有011接收说明应保留其历史事实，按本轮当前摘要更新。接收来的83份共享来源、feature.json、006/前端前轮文档不反向覆盖主项目。

| 相对路径 | 本轮交付内容 |
| --- | --- |
| specs/012-recipe-authoring/plan.md | 定向对齐共同保存、独立SQLite、责任、设计门禁及局部依赖 |
| specs/012-recipe-authoring/research.md | 更新既有R01—08决定与接收状态，保留原研究来源 |
| specs/012-recipe-authoring/data-model.md | 共同RecipeId/Version两表、移除SaveId、单一提供者、提交/可见/冻结/并发 |
| specs/012-recipe-authoring/contracts/recipe-authoring-api.md | 共同IRecipeStore/结果与ETag→ExpectedVersion、读写权限及错误 |
| specs/012-recipe-authoring/contracts/editor-ui.md | 可编辑/只读/配置引用全字段表、EX04/05状态映射和原型保护 |
| specs/012-recipe-authoring/contracts/shared-integration.md | IC-01—06接收、G-01—03、四个指定文件归属及旧残项关闭 |
| specs/012-recipe-authoring/quickstart.md | 必要版本冲突/未知与全字段重读、QV对M共用证据映射 |
| specs/012-recipe-authoring/checklists/architecture.md | 新建24项全未勾选；Notes逐项辅助评价 |
| specs/012-recipe-authoring/basis-receipt.md | 本次81项摘要与前轮69项历史接收 |
| specs/012-recipe-authoring/spec.md | 只更新依据/阶段/交接状态，不重写业务规格 |
| specs/012-recipe-authoring/checklists/requirements.md | 保持16个历史勾选，更新设计审阅与当前限制 |
| specs/012-recipe-authoring/clarification-sync.md | 保留clarify历史，当前摘要标明15份已合入/设计待接收 |
| specs/012-recipe-authoring/plan-handoff.md | 本文件；历史主项目接收、本轮最终逐文件清单与删除义务 |

## 历史合入与历史未接收记录

前轮plan在69份接收时记录“IC-01—05尚未接收、24份累计待合入”，该结论只对当时成立；本轮已有主项目明确交付，不能继续沿用为当前状态。前轮setup-plan曾获准维护012自己的feature.json，没有主项目Git写操作。

下表是011报告B1确认已合入的**15份澄清成果**，本轮只读核其主项目当前SHA-256；不代表本轮plan设计接收。006及前端12份本轮未改、不重复交付；其中012的3份此轮只更新当前阶段/交接状态，列于上方增量。

| 已合入相对路径 | 本轮只读核到的主项目SHA-256 |
| --- | --- |
| specs/012-recipe-authoring/spec.md | ED6F1801A9A98A5AD1B8AF4778D6718A65BC1D2D775201F505F58A7895F04B6B |
| specs/006-frontend-station01-console/data-model.md | A84AF042A89DF633E24A8277FDBD3E7DFE562813BB23F4019D5B1EAC7F756147 |
| specs/006-frontend-station01-console/plan.md | 6FFDA06D2D62D718439C65B4A5B2E6E4E0DB9534ED4ED689A5466E0DC502E9E6 |
| specs/006-frontend-station01-console/quickstart.md | E7D72C1987034404E0171C6B5DF4B39F501E72A0B7060B39CB405828762722C4 |
| specs/006-frontend-station01-console/research.md | 291CCC24F4206F2030E2DC9A5E19356750E7CE8DA5A9222E5AAA1603AD1F7E8A |
| specs/006-frontend-station01-console/spec.md | 1DA78C670376F00563CC50436199A6C1BF358D49EAC1D981C664E7B43133267D |
| specs/006-frontend-station01-console/tasks.md | C2471865EF9FF074CEEC5FF88643F77BBBD735D7A6910E8540BB4E2CD4DD97FB |
| specs/006-frontend-station01-console/contracts/api.md | 425A05F1C7A16BC6A1413D0C55B1E268AF5ED92567E01FE21735EE706B426B36 |
| specs/006-frontend-station01-console/contracts/gaps.md | 6B0A2D822D5997698FC70FDF961629DD9BA1B6E1FA336972D20D9F0B8EE5519A |
| specs/006-frontend-station01-console/contracts/host.md | FA83D1B9EF07631FC3F6E0388689B4C323FC8AD000E03572004A4FE9E82DB437 |
| specs/006-frontend-station01-console/contracts/prototype-mapping.md | CFF2D9BD92AAE9B0D131FECD2344AC7C5C0FE6CC70DD148350D8008AD325AC68 |
| frontend/README.md | EB486FE051053260277B77B6953338D53AF34352C10907F6C49C5E8B5A4D9D1A |
| frontend/tests/README.md | 6FE312F61FA720CC7E219F48A8A9E25795D96BE9C76A9C860A634B56154A0577 |
| specs/012-recipe-authoring/checklists/requirements.md | 75016CAA773DFA81AAE57ABD830F234B2848736B23E9A64D484F5EDFD78E1D8B |
| specs/012-recipe-authoring/clarification-sync.md | 67BB7E163F3F810F6E75A3820C006619E73D7C03A6C28CAAF77C3B9A39C83AB9 |

011随后design-alignment-20261003.md记录已原样合入首版basis-receipt（首版SHA前16位e9acdda691e44f7a），仅是历史来源收据；本轮更新后的basis-receipt仍在13份最终增量内，不能覆盖混淆这两次状态。

## 本轮文档核对与停止

只进行接收复制、Markdown编辑、文档内容/链接/标记/摘要核对；这些不是构建或软件测试。architecture新条目保持[ ]，requirements的16个勾选、006历史任务编号/勾选保持。012 tasks不存在；归档/V3、产品/测试代码、AGENTS及Spec Kit脚本/模板均未编辑。

最终文档核对：13份012责任文档有变化；83份接收来源逐项与接收摘要一致，其中46份相对本轮前副本为新增/刷新内容。授权范围外文件无变化、无删除；本功能相对文档链接无缺失，architecture为24个未勾选条目，requirements仍16个勾选，012 tasks不存在。这些只证明文档状态，不是软件验证。

本轮checklist前后hooks为空，不执行钩子。主项目和011副本无本会话写入。本轮最终修订尚未获得011接收记录；首版已由011明确接收，后续必须按实际接收更新，不能用旧15份澄清及历史basis-receipt已合入推定最终设计增量也已集成。到此停止。

## 2026-10-04新独立修复（当前未合入）

本轮以E:/dzk/gaode-1当前源码建立E:/dzk/gaode-012-ui-fix，不采用旧012整体覆盖。新增范围FR-021—024、T026—036见ui-fix-20261004.md；历史25/25及当时交付原样保留，不能称本增量通过。当前共同设计recipe-contract/1.4/正文3，仅在本副本；主项目未合入。源摘要artifacts/recipe-ui-fix-012/baseline-manifest.json。共享编辑仅必要模型/校验/序列化/冻结/planner/capture端口与012/006文档/API/UI，013通信/预算/任务/输入只读。


## 当前014/012设计交付（2026-10-05，主项目）

根E:/dzk/gaode-1；本会话统一设计014共同后端与012前端增量。共同RC10/1.5是文档设计，代码仍1.4/正文3；没有新的共同代码接收或软件通过声明。旧53份集成事实保持，原未合入记录仅当时时点。完整本轮文件清单、删除/验证义务及局部依赖见[014设计交接](../014-special-part-rotation/plan-handoff-20261005.md)，012方案见layout-design、DUI02/03预览见navigation-preview。无新tasks/勾选、无产品代码或验证操作。


## 当前标准深度设计审查与修订（2026-10-05，后于本日plan交付）

014新architecture22项，012在实际原28项后追加CHK029—042共14项，保全部旧内容/勾选；辅助评价分别21/1与13/1，部分满足仅为DUI02/03精确导航待确认。I01—05设计问题修订关闭；源码仍原集成范围，未声称本轮能力实现。准确位置/影响/修订和剩余类型见[014审查](../014-special-part-rotation/design-review-20261005.md)，最新本轮修改清单及SHA见checklist-review-receipt。

本轮API-L00明确特殊新建类型与同源准备，L01a承接编辑null和StageId；当前新写4、历史2/3读取及旧冻结按原版本，代表链必需两实际OK各原槽/safe及重复组。没有再次specify/重建plan/新tasks，无产品测试代码或软件运行，DUI两预览未改/未批准。


## 2026-10-05任务拆解交付（后于标准深度设计审查，分析前）

当前根E:/dzk/gaode-1，同会话014共同后端/012配套负责。014新增T001—T018全部未勾；012保留原T001—T036全部正文/完成状态，追加T037—T049全部未勾。采用当前RC10/1.5、新写正文4/记录1.5/冻结3和API-L00/L01/L01a，不重做spec/plan/checklist。012 architecture仅修范围笔误CHK029—038→CHK029—042，历史评价与全部复选框保持。

本轮确切文档：specs/014-special-part-rotation/tasks.md（新）、specs/012-recipe-authoring/tasks.md（追加）、specs/012-recipe-authoring/checklists/architecture.md（单处文字）、014 plan-handoff-20261005.md与012 plan-handoff.md（追加当前任务状态），另tasks-generation-receipt-20261005.json（本轮生成收据）。共同合同、源码、产品/测试/脚本、预览原件、013、feature.json及旧证据未由本轮修改。

共享文件只有一个编辑任务；共同字段/校验/冻结/执行/通信014唯一承接，012只存储/HTTP/UI消费。首次实际构建前迁移真实引用闭包types/ports/Worker/nullable/schema/Host/StorePrep/测试/脚本和扫描；签名/消费者首批不等全部运行或DUI导航。真实来源014:T005.SourceReady→012:T039.PreparedSource→实际同库结果为T005完成证据，不以Unavailable代准备，也不形成源准备验收循环。

真实保存/重读/版本/弹窗Core在012:T045提前交。DUI02/03仍待明确确认，只阻对应导航子交付与验收；整任务不得因CoreReady提前勾。014:T016唯一启动特殊/普通代表，012:T045/T046先交Ready，T047.RunEvidence在运行后形成；最终证据不反作启动前置，014:T018仅消费对应RunEvidence，不等待待批导航，两最终报告不互等。

本记录仅任务生成状态，未预写analyze通过。完成生成后分别显式speckit-analyze，只读复核；发现问题只在回复报告，不回写本记录/任务。生成清单、摘要、旧字节/勾选保护和只读基线核对见014 tasks-generation-receipt-20261005.json。无构建/测试/设备/数据库/Git操作，无新实施勾选。
