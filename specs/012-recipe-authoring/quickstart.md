> 2026-10-06当前增量：DUI02/03已确认，导航/实际保存验收见navigation-implementation-20261006.md；沿现有任务定向收口，不重开设计/全量/013或完整代表链。旧阶段描述按其原日期适用。

# 012最小验证指南

2026-10-05当前根E:/dzk/gaode-1；旧012修复已实际集成。本次定向增量见[layout-design](layout-design-20261005.md)及014 verification/quickstart，仅设计，未执行。014/012同会话统一负责，旧独立根、011统一合入、G-01待交等以下内容均为所标历史时点，不是本次前置。

## 历史2026-10-03/04研究与验证记录（保留原证据范围）


本次仅运行ui-fix-20261004.md最小验证7类。运行前核013公开测量状态；本副本专用库/端口/证据。旧25项报告不作为新字段/UI通过证据，不全量、不重跑旧完整链。

日期：2026-10-04。012独立根E:/dzk/gaode-012-recipe-authoring；25/25软件任务完成。当前共同001—006、绑定001—030、状态001—007已实际接收；完整Integration/Host/StorePrep build18和前端build/typecheck02通过，零警告错误。实际RecipeAuthoring HTTP/SQLite10项、current-format适配17项、runtime33＋authoring11项及原型/安全3个不同检查有据；源架构10九项有效，共同006后受影响A02按固定1项证明复用、其余8条不变；4个同源保存/序列化负例有效。失败/Skip/原证据按实际范围保留，未跑全量。

同源实际弹窗新建/检查/保存/完整GET/编辑重开/正常重启证明复用；011唯一single05/multi03链及原multi03页面/通知/冻结/实际NG处置对账有据。首Final DOM未settle及历史查询失败原件保留，状态007修后原库只读重开补证20项全部满足、Host正常退出0/无新Run；不是新链。030同run13项GET与旧人工换面404已交。当前删除的孤立前端换面消费不改变阶段/冻结/终态映射，精确源变更及当前33组件/原型门禁明确复用范围，不把旧截图称新字节采证。

最终证据在`artifacts/recipe-authoring-012/final-20261004`，当前批次/摘要见plan-handoff。源码/证明已分批交011，主项目仍仅011集成，未有最终回执不宣称主项目产品合入。当前结论为具名Test软件范围，正式地址/外部3D/型号承载/安全校准及恢复原延期不冒称通过。

### 受控联合输入准备（T006/T022承接）

011已明确由012工具将其T025具名Test输入实际写入独立配方库。增量入口为`Gaode.StorePrep --seed-test-recipes <allowedRecipeRoot> <recipeRoot> <inputFile> <expectedSha256>`：只允许已准备且内容为空的库，复用维护互斥；输入须为recipe-catalog-snapshot/1，摘要匹配且共同准入确认Test范围。使用共同身份、正文序列化及ValidateForSave，一次真实事务写入完整正文和Head，输出输入摘要及实际身份。不得用于生产批准、覆盖既存库或HTTP批准；Host仍只读同一SQLite来源。具名run-1/run-2、catalog及受控输入清单已按稳定012/014接收；011批012已实际使用同源012工具种入两份并完整重读，其准备证据可引用但不是运行/页面通过。

## 前置与环境

1. 在调度准备的完整012独立源码副本中工作，核对源码基线及[共同合同](contracts/shared-integration.md)IC-01—06及recipe-contract/1.3；G-01设计和前轮7份接收已闭合，实际共同代码按tasks分批核验。主项目只读，不能在E:/dzk/gaode-1落运行产物。
2. 代码与必要任务获审查后，按global.json的10.0.401及现有前端依赖准备环境，不顺手升级。仅运行下面受影响入口。
3. 配方库与运行库采用不同明确路径。受控准备、实际Host、页面及日志路径均在本次授权验证根内，运行原有生产限制保持。固定图片/虚拟PLC可参与真实调用，但必须记录来源；假API不能作为联合证据。
4. 验证输入从正式弹窗/读写API进入；点位有明确用途、对象及单位依据，不把Test码、默认数量或旧Q作为产品分支。更多面与四面后E需011共同合同可表达并经校验。
5. 共同001—006、绑定001—030、状态001—007及真实共享消费者已实际接收；旧“未交/NotRun”仅原时点。只接固定清单，不复制在制目录。独立保存与同run冻结/状态证明见当前报告；已经具备实际证据的范围不重跑同义链。旧产品Review/回退/环境设置/孤立换面消费已实际删除，有效格式适配及合法历史读取保持。

## 受影响构建与定向组件入口

后端构建及npm命令在完整源码副本根执行；Node前端测试明确切换到frontend后执行，避免输出到副本外。现有工程路径已只读核对。无变化的宿主桌面工程不要求重建；如实际更改则补其必要构建。

**首次实际构建/执行条件**：按所选工程实际引用完成T019首批RC08消费者/测试输入迁移及011所属必要共同消费者迁移，并准备当前要验证的实现；Host命中的直接调用/DI由T010承接，StorePrep不附加无引用的Host条件。首批只依赖已交类型/序列化和消费者清单，不等T009或后段运行；涉及真实目录的T019后段才依赖SqliteRecipeStore。T006工具及T007/T014/T018测试可以先编写，构建/运行必须满足各自引用和实现条件；T013/T017不得跳过，也不得要求这些测试先通过才编写实现。首批完成不代表T019整项完成。

~~~powershell
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --configuration Debug
dotnet build backend/tools/Gaode.StorePrep/Gaode.StorePrep.csproj --configuration Debug
npm --prefix frontend run build
npm --prefix frontend run typecheck
~~~

以下RecipeAuthoring定向测试当前实际发现10项并全部通过，报告在`artifacts/recipe-authoring-012/resume-20261003/test-results/recipe-authoring-04.trx`，日志同根。新批次只补受影响项；零发现不是通过，不扩大到全量。

~~~powershell
dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --filter "FullyQualifiedName~RecipeAuthoring" --logger trx
Push-Location -LiteralPath frontend
try { node --test tests/us1/recipe-authoring.test.ts }
finally { Pop-Location }
~~~

只对本次受影响的现有runtime-007用例、RecipeApplicationReceipt保存保护补定向过滤，不重跑整个历史集合。架构门禁复用既有入口，由011更新共同路径断言，012提供新存储/HTTP路径和负例：

~~~powershell
dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --filter "FullyQualifiedName~RecipeExecutionBoundaryTests" --logger trx
$recipeEvidenceRoot = Join-Path (Get-Location) ('artifacts/recipe-authoring-012/' + (Get-Date -Format 'yyyyMMddTHHmmss'))
New-Item -ItemType Directory -Path $recipeEvidenceRoot | Out-Null
pwsh -File frontend/scripts/verify-prototype.ps1 -Output (Join-Path $recipeEvidenceRoot 'prototype-report.json')
$env:GAODE_FRONTEND_EVIDENCE_ROOT = $recipeEvidenceRoot
Push-Location -LiteralPath frontend
try { node --test tests/us1/prototype-console.test.ts tests/prototype-all-pages.test.ts tests/security-boundary.test.ts }
finally {
  Pop-Location
  Remove-Item Env:GAODE_FRONTEND_EVIDENCE_ROOT
}
~~~

GAODE_FRONTEND_EVIDENCE_ROOT已接入前端测试采证，原prototype-console硬编码历史us1-report.json已替换。使用新批次证据目录；verify-prototype必须显式-Output，不覆盖历史报告或写副本外。

verify-prototype已按[精确差异设计](contracts/editor-ui.md)实现归档逐项替换及交付资源摘要核验，正例与五个未授权变更负例通过。不能关闭检查、整页豁免或改写旧报告。若变更触及011通信边界，只补受影响门禁；不跑009/010全部专项。

## 配方库准备与启动接入

Gaode.StorePrep已实现以下两个入口并实际准备/检查独立SQLite，覆盖已有目录会拒绝：

~~~text
Gaode.StorePrep --prepare-recipes <allowedRecipeRoot> <newRecipeRoot>
Gaode.StorePrep --inspect-recipes <allowedRecipeRoot> <recipeRoot>
~~~

prepare在授权根内的全新配方目录创建recipes.db及配方schema，拒绝覆盖已有数据；inspect只核配方schema/表/索引，不运行任何产品动作。复用既有受控维护方式，准备与使用互斥；Host只探测，不调用迁移。旧运行库prepare/upgrade的参数和允许范围保持，不能用新分支绕过其保护。

Host新增显式配置键：RecipeStore:DatabasePath、RecipeStore:ReadWriteTimeoutMs、RecipeStore:DbLockTimeoutSeconds（正有限、锁等待受请求剩余预算限制）。Program已将IRecipeStore/IRecipeCatalog绑定同一个SqliteRecipeStore；缺库/错误schema不能回退Review/File。数值按实际验证预算记录，不能采用设备生产缺省值冒充批准。

Host启动沿完整副本现有运行入口及011已交付配置；不原样复制旧固定Q启动脚本。当前未形成已可执行的新联合启动命令，必须由011脚本唯一负责人接收012采证需求后更新。单独保存场景不要求PLC连接成功，更不能为了保存发起设备动作。

## 最小验证集合与预期

| 编号 | 一次序列要证明的内容 | 合格证据与边界 |
| --- | --- | --- |
| QV-01 弹窗保存重读 | 新建→完整输入→检查→真实保存→GET完整正文→编辑有业务差异→保存→重开→Host正常重启→重读；三部分往返、关闭不提交及现有风格 | 正式页面网络请求/响应、实际RecipeId/Version/DefinitionDigest、真实数据库行、完整业务值一致；不能只截成功提示 |
| QV-02 必要拒绝/失败 | 缺必要点/四面不合法由共同校验拒绝；另一配方重复料盘号；旧Version更新冲突；无写权；实际只读/受控不可写存储导致提交失败，实际读取不可用 | 真实拒绝和诊断可定位，Head/目录不错误前移、旧记录仍可核；ExpectedVersion冲突为412。故障在独立验证库施加，不用假成功或预置API响应 |
| QV-03 联合代表 | 正式保存后F唯一匹配，未匹配保留公共准备/F事实并阻断产品动作；运行冻结期间编辑保存，旧run不变、后续F用新内容；更多面/可选E/OK原槽/NG/Pending目标/姿态异常退出 | 与011共用同构建/共同合同/运行集合及真实组件证据。四面后E与更多面按确有差异选必要代表，不强行一配方覆盖所有不相容条件，也不穷举 |
| QV-04 保护 | 授权弹窗变化可通过、未列页面差异仍失败；新保存消费者仍走唯一校验，前端不直控/无第二模型执行/无素材协议泄漏 | 检查实际发现数、正负例及摘要；必要组件mock单独标明，不抵真实API或运行证据 |
| QV-05 删除与承接 | 按消费者清单替代旧限制/推断/假演示并实际删除无用途代码 | 真实调用/装配/配置/脚本核对记录，保留合法适配/历史读取及保存/关联/期限/取消；失败证据只读 |

QV-02用少量代表覆盖必要机制，不扩成全异常矩阵。保存后刷新失败和无回执未知状态的显示可用组件测试证明；真实持久化结果必须由实际查询证实，不凭组件模拟响应。更复杂网络恢复/编辑合并只记待办。

## 与011最小集合共用，不重复整链

已接收[011-verification/1.1](../011-plc-interaction-update/contracts/verification.md)。下表只是责任/证据映射，不新加一套验收；已有保存/页面及组件证据见当前交接。011单面05和多面03软件链已实际通过；最终页面证明需承接同multi03库重启后的真实冻结/整盘终态补读，原失败保持，不把组件当该页面证据。

| 012集合 | 011对应 | 最小承接 |
| --- | --- | --- |
| 受影响构建 | M01 | 实际改动工程一次构建，双方引用同次构建身份 |
| QV-01真实保存/重读 | M08 | 正式弹窗/API及独立SQLite；全字段往返含只读/引用/未改属性，不仅断言数量或成功提示 |
| QV-02必要失败/冲突/未知 | M08/M09 | 唯一性、ExpectedVersion、真实保存失败与取消/有限等待；响应未知的组件显示单列，实际提交状态仍需真实记录核查 |
| QV-03 F与运行隔离 | M03/M05/M06/M07/M08 | 共享单面完整链和多面完整链；M03观察及M05处置复用其中事实，不另跑同义链 |
| QV-03更多面/额外E | M04及M07已有可用事实 | 少量有效代表，四面E开/关和更多面可用必要组件证据补差异，但必须覆盖实际共同动作及关联，不能只跑planner；不穷举面数组合 |
| QV-04授权差异/架构 | M10；受影响通信边界由M02提供 | 真正正负例、共同执行及真实API；未授权区域仍拒绝，错误架构不能获白名单 |
| QV-05消费者清理 | M11 | 012适配/UI/目录和011共同逻辑各自记录实际删除与有效义务承接，历史证据不删 |

单面/多面两条联合代表由011整合，012提供其正式保存内容、重读及页面绑定证据。更多面与额外E若未进入同一代表，必要组件只能补对应差异；不能把组件mock、旧报告或规划器展开称为联合实际动作。异常物理槽退出、OK原槽、NG/Pending真实目标、保存/取消/期限保护仍须在适用证据中可追溯。

## 联合证据交接

T017先交D012-store-api-1.3真实保存/完整重读/Version及必要证据，T019/T020后续补适配与运行接线，各批保留未完成范围。页面/API和采证准备完成后，由011 T025准备唯一驱动、T027满足对应前置后统一启动，012 T022同期采证并对账；D012-ui-joint-evidence在运行后形成，不能作为启动该运行的前置。若实际输出或必要证据未齐，保持对应未完成/Blocked，不另起同义完整链。011正式发布说明的当前核对状态见plan-handoff，不把约定当代码已交。

在本次独立证据根artifacts/recipe-authoring-012/<本次批次>/记录：

- manifest：源码构建身份、011/012合同快照摘要、时间/组件来源、实际入口、测试过滤器及发现/通过/失败/Skip数。
- authoring：脱敏HTTP、requestId、共同RecipeId/Version/DefinitionDigest、真实保存与完整重读，页面可观察结果；不记录令牌或素材本地敏感路径。
- joint-reference：011同次联合报告路径、run/料盘/F/冻结身份及动作/结果查询位置；不复制一轮同义验收。
- cleanup：替代/删除位置、有效消费者承接及必要回归结果。

漏跑、阻塞、失败、Skip、零发现、旧证据分别记录；只有实际执行的范围可判通过。已有历史报告仅对照，不拼接成新完整链。实际运行权限以当前用户授权为准；本轮已授权在012独立副本执行最小构建/验证，011唯一启动联合链。
