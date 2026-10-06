> 2026-10-06当前增量：DUI02/03已确认，导航/实际保存验收见navigation-implementation-20261006.md；沿现有任务定向收口，不重开设计/全量/013或完整代表链。旧阶段描述按其原日期适用。

# 012技术研究

2026-10-05当前根E:/dzk/gaode-1；旧012修复已实际集成。本次定向增量见[layout-design](layout-design-20261005.md)及014 verification/quickstart，仅设计，未执行。014/012同会话统一负责，旧独立根、011统一合入、G-01待交等以下内容均为所标历史时点，不是本次前置。

## 历史2026-10-03/04研究与验证记录（保留原证据范围）


2026-10-04追加研究见[增量设计Phase 0](ui-fix-design-20261004.md)：实际粒度不足、硬件NotIntegrated、正式新建配置缺失及最小读取处理，不以字段存在判满足。

日期：2026-10-03。范围：Phase 0，设计选择经本轮定向对齐并接受辅助清单审阅；不是实现或运行结论。依据[规格](spec.md)、[接收清单](basis-receipt.md)、宪章8.0.0及只读主项目源码。研究分别核对界面与存储/API，再统一收敛；未修改代码或运行软件。

## R01 依据与依赖

**Decision**：已从主项目按明确交付刷新/接收83份依据，消费recipe-contract/1.2、station01-execution/1.0、011-verification/1.1及四份混合API；独立SQLite由调度确认。

**Rationale**：RC04已给唯一校验、快照、保存及匹配签名；RC01给服务端身份/版本及011统一摘要，RC05/05.1明确选择意图、冻结和软件绑定回执。此前“69份、详细合同未交付”是前轮研究时点，当前不沿用。本轮收尾接收1.2，G-02身份/摘要规则和G-03存储选择已关闭，仅剩新增字段序列化G-01，见[共同接入](contracts/shared-integration.md)。

**Alternatives considered**：不另建012保存端口/身份服务/校验器，不把旧源码类型当新合同已实现。已关闭宪章页尾、008旧版本锁及混合合同未交付残项，不重复要求修复历史快照。

## R02 沿现有前端交付入口

**Decision**：保留静态HTML、runtime.js、现有CSS与WPF/WebView2；只替换现有recipeModal内编辑内容及必要数据绑定，不引入UI框架或页面。

**Rationale**：frontend/scripts/build.mjs实际复制src/pages和runtime.js；package.json含Tailwind 3.4.17、SignalR 8.0.7、TypeScript 5.9.2。a.html:363—535为现有弹窗，:721—732已有打开/关闭行为；runtime.js:76—94只有目录摘要读取，:484—498只有选用。只改未进入构建的TS模块不能满足交付。

**Alternatives considered**：新路由、整页V3、通用JSON/流程编辑器均扩大范围。基础信息、点位配置、检查保存三部分足够；临时表单值留当前弹窗内存。现有localStorage只保存不可信run引用并重取事实，保留有效用途，禁止以其保存配方。

## R03 独立配方SQLite库

**Decision**：复用工程已有.NET SDK 10.0.401/net10和EF Core SQLite 10.0.12，增加独立RecipeStoreDbContext及显式配方库路径。保存Head和不可变完整正文两张业务存储表；不增加审批、回滚、版本管理页面。

**Rationale**：backend/Directory.Packages.props、global.json为源码依据，不升级依赖。Station01DbContext承载运行事实；StoreSchemaInspection精确核对运行库表/索引及四个迁移，StoreMaintenance只承接已授权009升级。向其随意加表会破坏有效门禁。独立库保存配方，运行库保留011已冻结快照及实际结果；两者不构造跨库/设备事务。

**Alternatives considered**：覆盖单JSON文件难以沿现有能力原子维护唯一料盘号和完整历史引用；直接塞入运行库需扩大旧迁移门禁；另引数据库/服务无必要。前轮为012技术选择；本轮调度已明确采用独立SQLite。011本轮已修订plan/research采用SQLite，G-03关闭，不保留正式文件目录。

## R04 提交、目录与唯一性

**Decision**：同一个SqliteRecipeStore实现共同IRecipeStore/IRecipeCatalog；写入串行边界内核TargetRecipeId/ExpectedVersion，调用011唯一身份/版本/摘要函数及ValidateForSave；在单个配方库事务内写不可变正文并切换当前Head，料盘匹配键在Head上唯一。目录和F读取走持久库当前视图；不保留进程启动时固定快照为正式保存消费者，不做保存前缓存发布。

**Rationale**：Program.cs:44—50将IRecipeCatalog注册为构造时快照，JsonRecipeCatalog/SemanticRecipeInputProvider不能自然反映新保存。一次读取应连同Head及正文返回一致内容，交011绑定服务冻结。保存与F相遇的可观察边界是提交和F共同解析取样；已取样冻结的旧运行不重读Head。RC01/05已明确FCode原文本全局唯一，012在服务端调用011 RecipeDefinitionIdentity生成RecipeId/Version并落实BINARY唯一索引，DefinitionDigest只调用011函数。删除SaveId/saveToken，ETag直接封装共同Version送ExpectedVersion；目录/完整读取/F用同源GetSnapshot，真实提交确认后返回Saved，同源后续读取可见；不增加提交后重读门。

SQLite事务提供原子提交，且同一时刻只允许一个未提交写事务，采用短事务，不把设备或算法等待包进去。[Microsoft事务说明](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)。普通列唯一索引用于重复键拒绝，不依赖新版本复杂类型特性。[EF Core唯一索引](https://learn.microsoft.com/en-us/ef/core/modeling/indexes#index-uniqueness)。

**Alternatives considered**：改完文件再通知缓存、按scene局部唯一、按所选旧version锁死后续F、API自行匹配/编排均不采用。编辑带共同Version作一次条件提交，拒绝过期写入；不建立多人锁/合并平台。

## R05 有限等待与维护

**Decision**：新增显式RecipeStore配置，包含独立DatabasePath及正有限ReadWriteTimeoutMs/DbLockTimeoutSeconds。SQLite锁等待不得超过请求总剩余预算；无无限等待，无隐式默认生产预算。普通配置值由部署/测试环境显式给出，不放进配方。沿StorePrep工具增加配方库专用prepare/inspect入口，Host只检查schema，不自动建库/迁移。

**Rationale**：Microsoft.Data.Sqlite的异步ADO.NET方法实际同步，不能承诺CancellationToken能即时中止全部数据库操作。[异步限制](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)。CommandTimeout和连接DefaultTimeout都必须有界，0为无限，默认值不能冒充本功能预算。[锁等待说明](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors#locking-retries-and-timeouts)。数据库等待不占用PLC运动锁；已有控制与心跳路径保留。超时/断线不能反推没有提交，前端显示结果未确认，重读核实。

**Alternatives considered**：Host EnsureCreated/Migrate、放宽旧运行库Schema检查、把SQLite包Task.Run就宣称取消可靠，均不采用。这里只设计首次受控建库/核验；备份平台、自动恢复和跨版本转换延期。

## R06 HTTP、权限与诊断

**Decision**：012设计catalog、完整读取、validate、新建/更新API；请求封装共同正文，业务校验只在共同服务。保留Run.Read读取，检查用Config.Validate；新增最小Recipe.Write权仅映射已有ProcessEngineer/SystemAdministrator。页面消费后端授权事实及401/403，不由Run.Start推导写权限。

**Rationale**：RecipeEndpoints目前只有catalog/plan/bind；Station01Authorization及TestAuthenticationHandler只有本地Test认证，尚非生产认证。新增写权映射是技术设计，不能提升生产准入。Program.cs需承接PUT、If-Match、ETag暴露及PUT诊断；这些不新增通信能力。保存日志关联requestId、actor、RecipeId/Version/DefinitionDigest及共同保存结果，失败原文进持久诊断，API只给安全原因。

**Alternatives considered**：所有目录可读者自动可写、另建认证平台、业务API直接处理PLC、异步Accepted冒充已保存均不采用。生产认证未接通时只限制相应环境使用，不能将Test认证伪装生产交付。

## R07 显示与原型门禁

**Decision**：run投影与弹窗编辑状态分开。以011真实阶段、处置、异常物理槽号更新现有区域，不由页面排序猜完成。保留原型ZIP哈希与三页归档保护，以精确授权差异清单替换“实现页必须全字节相同”的错误断言。

**Rationale**：runtime.js:230—231仍按先下料后分拣推断；sameRecipeRef既用于旧选择限制，也用于冻结页头，必须按消费者定向替代。verify-prototype.ps1当前全页字节比对不能容纳授权弹窗；现有prototype-console/prototype-all-pages测试又主要仅查标志/存在。门禁需核原文、替换段及依据，应用差异后全文件比对，不能豁免整页/整段script。具体见[界面合同](contracts/editor-ui.md)。

**Alternatives considered**：删除检查、重生成归档、整个a.html免检或保留“identical=true”均不采用。

## R08 交付与清理收敛

**Decision**：后续实施前准备完整独立源码副本，按已接收011共同接口及G-01新增类型补交项，并按[唯一负责人表](contracts/shared-integration.md)接线。先核调用/装配/配置/脚本消费者，再真正删除无用途替代逻辑；保留有效适配器、历史读取、正确保护及失败证据。

**Rationale**：012目前是文档副本，不含可构建源码。001/003/008混合合同归011；RecipeEndpoints.cs/Program.cs及RecipeEnvironmentDecoder.cs/SemanticRecipeInputProvider.cs归012；共同语义、校验、绑定/执行和通信归011。联合链使用弹窗实际保存的内容和011同次运行证据，不拿老Q或组件mock抵联合通过。

**Alternatives considered**：同时编辑混合文件、保留备用错误业务分支、全量重跑009/010或从失败测试反推删断言均不采用。后续范围见[最小验证](quickstart.md)，本轮没有软件验证结果。
