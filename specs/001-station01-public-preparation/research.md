> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。第一工位移交前仍无配方调用。

# 技术研究与决策

**功能**：001-station01-public-preparation　**方案版本**：1.2.0　**日期**：2026-09-22  
**基线**：[spec.md](spec.md) 1.2.0、宪章1.2.0。本文件记录开发设计选择和实现对齐结果，不是生产选型确认或运行报告。

## 1. 本轮只读证据

重新读取规格、规格检查清单、宪章及对齐记录、plan模板、REQ V1.1全文及ARCH V1.3全文，以实际文件为准。REQ实际位置为“软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.md”。优先级为本次用户要求、有效spec/已确认决定、REQ业务基线、ARCH兼容设计。

| 检查 | 2026-09-20实际结果 | 结论及限制 |
| --- | --- | --- |
| 当前目录 | E:/dzk/gaode-1；只有来源资料、.specify模板/宪章及本功能spec和清单 | 没有可复用业务工程；不从其他项目复制实现 |
| 指令文件 | 当前项目递归及E:/、E:/dzk/祖先均未发现AGENTS.md | 没有额外项目指令；不需要引入插件/技能 |
| 工程配置 | 未发现sln/slnx、csproj、global.json、NuGet锁文件、Python依赖文件 | 下表是新开发选择，不冒充项目已有约束 |
| Spec Kit/Git | 没有.git、.specify/scripts或命令模板；git程序可用，版本2.55.0.windows.4 | 按现有plan模板直接写设计资产；没有初始化、分支或CLI执行记录 |
| dotnet --info | SDK 10.0.401；MSBuild 18.9.11；.NET/ASP.NET Core运行时10.0.12；win-x64，OS build 10.0.17763 | 支持选择net10.0做本机模拟开发；仅命令可读，不代表工程构建或该OS获生产支持 |
| python --version、py -0p | PATH Python 3.12.10；另列3.13，后者位于其他项目目录 | 选显式Python 3.12路径，不依赖py默认3.13，不使用其他项目环境 |
| NuGet本地目录 | EF Core/SQLite/Mvc.Testing 10.0.12；xunit 2.9.3、runner 3.1.5、Test SDK 18.10.1、SQLitePCLRaw native包2.1.12 | 只见缓存目录，未restore/build/load或验证原生SQLite版本 |
| 可控时钟包 | Microsoft.Extensions.TimeProvider.Testing未见缓存 | 设计采用10.0.0；将来restore验证依赖，不在本次安装 |
| SQLite命令行 | PATH未发现sqlite3 | 方案不依赖其预装，不执行建库/读库检查 |

Python路径：C:/Users/Administrator/AppData/Local/Programs/Python/Python312/python.exe。PATH检查及包目录读取未启动Worker。未运行dotnet restore/build/test、设备SDK或数据库命令。

## 2. 决策记录

| 决策 | 开发选择及理由 | 未采用方案 / 受限边界 |
| --- | --- | --- |
| D01 运行时 | C#、net10.0、ASP.NET Core；实现时以global.json固定SDK 10.0.401，开发包固定已观察到的10.0.12系列 | 不是生产OS/SDK兼容确认；不增加WPF、前端；OPEN-22保留 |
| D02 工程边界 | Domain、Application、Infrastructure三个库及唯一Host；Api作为Host内独立目录/端点模块，只有其适配HTTP | 不预建15个工程、设备服务、微服务或通用事件总线；依赖约束可用架构测试验证 |
| D03 状态推进 | Station01Coordinator唯一写运行状态；MotionCoordinator统一动作准入；完成/取消以持久Run revision/None条件事务最终裁决，Run/Handoff原子一致，停止准入不等待DB | 不在唯一事件消费者await设备、算法或磁盘；不全量事件溯源 |
| D04 时间 | TimeProvider.System与FakeTimeProvider共用Application期限调度器及入站仲裁；单调时间计算预算 | 不用系统日期判期限、不靠Task.WhenAny竞争顺序定义边界、不直接让替身返回Timeout证明计时有效 |
| D05 数据 | EF Core/SQLite 10.0.12开发候选；单写消费者、短事务、独立读、媒体文件；同一SubmitCritical独立保存AlgorithmIntent后派发，原算法期限涵盖保存等待 | SQLite异步API不能保证线程不阻塞，写执行隔离到独立有界消费者；业务循环不执行SQL |
| D06 Worker | Python 3.12.10候选；常驻子进程，stdin/stdout小型NDJSON，stderr持续有界消费，媒体只读引用 | 模拟不需Python；真实算法包、NumPy/OpenCV/GPU等尚无输入，不指定模型/精度，不实现训练或管理 |
| D07 隔离 | 模拟PLC、相机、算法均在Host进程内；真实PLC适配内部封装Modbus客户端；相机连接复用 | 厂商SDK若不可取消/有位数限制，OPEN-22补齐后另作适配隔离决定；本阶段不预建DeviceHost或独立虚拟PLC |
| D08 文件配置 | 严格JSON结构、版本、用途与来源；注册能力ID；加载校验后冻结规范化副本 | 不提供任意脚本、产品分支、热切换Provider或动态类加载 |
| D09 API与安全 | REST命令/查询、SignalR状态通知；loopback默认监听，后端权限策略；开发认证替身只限显式Test模式 | 不建完整账号平台；生产身份来源/策略按OPEN-23，不能用请求体角色或匿名访问作为真机授权 |
| D10 开发库 | 测试工程内离线准备入口复用正式EF映射/迁移，独占维护锁并输出清单；Host只读核验结构后使用已有库 | 不开发完整维护工具；不能在Host正常启动EnsureCreated/Migrate或用自动建库掩盖路径错误 |
| D11 故障隔离 | 高度与读码有独立可替换执行槽；测试每角色1槽、无无限排队；超时立刻封闭业务调用窗口，回收后台进行 | 高度Worker未恢复不能挡F；若F槽确实不可用，形成NotReady而非无限等；物理占用不随算法结束释放 |
| D12 原始码 | Ordinal原始字符串相等去重，保留原始重复候选；唯一值用注册解析器提取已有字段 | 不按置信度、码制、区域或产品筛选，不生成配方端口 |
| D13 公开合同门 | 以实际Host路由、策略和集成测试为实现基线；先冻结DTO、统一错误、ETag、权限和事件语义，再补齐缺失路由 | 不把现有局部实现写成合同已完成；不在Api层旁路Application或设备控制 |
| D14 控制接口 | 暂停、取消、恢复核对、继续和公共配置校验都复用Application/持久化语义，使用expectedRevision和幂等回执 | 不把HTTP受理、停止请求或内存状态当最终终态；不改变002/003专属接口 |
| D15 状态通知 | SignalR使用版本化事件摘要；客户端以快照为准，乱序/丢失按revision重查；有界缓冲只合并可替代状态 | 不继续把完整RunSnapshot广播或Clients.All广播当作稳定公开合同 |
| D16 测试媒体 | 公开结果只暴露受控mediaId及source=Simulated、purpose=Test等元数据；可选本地fixture必须由受控索引选择 | 不接受任意路径/上传作为媒体来源，不把合成或fixture媒体称为真实相机结果 |
| D17 真实适配边界 | Production不得静默回退SimulatedCapture/SimulatedAlgorithm；真实相机、算法Worker和正式身份按OPEN-22/23/26另行验证 | 不因前端联调提前宣称真机、算法精度或生产权限已完成 |

D04的基础API及FakeTimeProvider能力由[TimeProvider说明](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview)和[官方测试指南](https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing)支持；边界优先级、期限窗口和事件仲裁是本方案的设计决定，不能依赖框架计时器回调的偶然执行次序。

D05依据[Microsoft.Data.Sqlite异步限制](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)、[SQLite WAL](https://www.sqlite.org/wal.html)和[DbContext生命周期](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)：集中写入，读事务保持短小，不共享DbContext。WAL不是多个写者并行的许可，原生版本和断电能力仍需后续验证。

D10依据[SQLite迁移限制](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)及[EF迁移应用方式](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)：开发准备必须使用明确源/目标版本；不假设SQLite支持通用幂等迁移脚本，也不把EF迁移锁当作Host/工具共同维护互斥。

## 3A. 2026-09-22 后端实现只读审计

本轮只读检查了 `backend/src/Gaode.Host/Api`、`Composition`、模拟适配器及现有集成测试，未修改代码、数据库或设备。审计结论用于更新plan和合同，不替代实现验证。

| 证据位置 | 事实 | 设计影响 |
| --- | --- | --- |
| `Api/RunEndpoints.cs`、`QueryEndpoints.cs` | 已有启动、运行/命令/handoff/status和受控媒体读取；暂停、取消、恢复核对、继续、配置校验路由尚缺 | 先冻结公开DTO、权限、错误和版本语义，再实现控制路由 |
| `Station01Authorization.cs`、`TestAuthenticationHandler.cs` | 测试身份映射已有四类角色，但已注册策略只有Read/Start/Media.Read | 控制策略必须逐项注册；正式身份仍由OPEN-23管理 |
| `Station01NotificationService.cs` | 当前发送完整RunSnapshot，且使用广播；未形成合同中的四类版本化事件 | 增加事件摘要、revision/persistedRevision、断线重查和有界合并语义 |
| `QueryEndpoints.cs`及状态模型 | 当前status/ETag未覆盖合同要求的全部设备、存储、算法和PLC事实 | ETag必须随任一公开状态事实变化，算法不可用不自动伪造控制失败 |
| `MediaEndpoints.cs`、`AdapterBindings.cs`、`SimulatedCapture` | 媒体索引主要驻留内存；采集/算法在Production仍为模拟适配器 | 增加受控媒体来源/用途元数据；Production拒绝或明确受限，禁止静默模拟 |
| `003-plc-latest-protocol`、`002-plc-xyz-recipes` | PLC最新协议和配方接口属于各自功能边界 | 001计划只消费其公共语义，不重定义或回退这些接口 |

## 3. 来源冲突裁决及解释

继续采用spec C01–C11及宪章对齐记录，不修订来源：

- ARCH中3D姿态/XY求解、翻面重算、F成功后匹配配方均不进入本功能；REQ ID-005、CTL-010与CL-01/04/05优先。
- ARCH算法就绪/持续失败停机规则由REQ ALG-013、SAF-011及CL-06覆盖；只有真实安全、保存、容量等失效才限制对应步骤。
- 项目级配方快照本阶段具体化为独立公共快照；质量保持NotEvaluated，不提前生成Pending/OK/NG或虚构Part/Face。
- “五层”是ARCH逻辑职责分类，不是五个.NET工程；Domain/Application/Infrastructure是代码依赖边界，Api/Host不新增业务层。
- spec“规格不指定类名/目录”约束规格阶段；本次plan明确要求设计目录和接口，因此可给出拟定命名，但不生成工程实现。
- spec §12.1及规格检查清单“没有plan/tasks”是编制规格时的检查事实；该描述仅指初版设计时点；当前tasks.md已有77项任务（其中34项已有M1证据，新增T068-T077未完成）。保持业务规格及原规格检查记录只读，当前产物状态以[plan.md](plan.md)和[tasks.md](tasks.md)为准。
- 本功能无工件融合/A-B批采、完整配方生命周期、单步/示教页面、分拣、完整备份维护工具；模板对应内容说明不适用，不据此补建后续工位。

## 4. 决策有效性

设计编制时点（2026-09-20）的事实：当时的选择足以制定独立模拟开发任务，但包恢复、编译、原生加载、实际时间集成及Worker通信均尚未执行，包缓存不能作为可构建证据。该历史NotRun不随实现追溯改写。2026-09-21已取得M1开发验证，随后独立审查发现局部缺口；定向修复见[修复证据](../../artifacts/station01/m1-review-fixes-20260921-02/repair-report.md)，第三次独立审查及当前状态见[独立审查证据](../../artifacts/station01/m1-independent-review-20260921-03/independent-review.md)，原失败证据保留。真实Worker通信和现场兼容仍未验证。实际OS、SDK/驱动、PLC地址、算法能力及生产预算继续由OPEN-22等约束；不得用本机版本或测试参数关闭它们。


## 5. 2026-09-20最小修订依据（方案1.0.1）

- H01：采用独立AlgorithmIntent关键提交，复用现有Writer及回执，避免耦合媒体事务与算法调度。Call登记时的原预算包含提交等待；提交失败/未知不派发，意图不证明Accepted。三类中断窗口保留实际派发证据，未知不重算/重拍。没有新增通道或改测试耗时。
- H02：依用户明确决定采用持久条件事务作为唯一终态裁决；不以取消请求或内存事件先后承诺最终取消。立即关准入与所需停止独立执行，WriteId未知先核对，SQLite五窗口交叉及重启一致性为必做证据。
- M01：保留T031/T032基础查询/控制信号责任，把完整取消用例和集成证据归T044/T048；M1正常闭环不宣称取消/恢复完成。没有新增任务编号或反向依赖。
- 本次依据为最新设计全文和用户明确修正，不是新生产技术选型；开发栈、CL-01至CL-06、12项OPEN及S01-Q01/Q02不变。该条记录属于设计修正时点（2026-09-20）的文档静态复核，故当时全部软件验证为NotRun；当前M1软件验证状态以[第三次独立审查](../../artifacts/station01/m1-independent-review-20260921-03/independent-review.md)为准，M2仍未开始。

历史证据说明：`m1-review-fixes-20260921-02/final-verification-summary.json`曾记录文件计数163，最终完整性检查的当前清单为164。保留旧摘要和测试结果不变；以当前清单及源码逐文件SHA-256一致为准。该计数差异不影响源码哈希一致性或M1验证结论。

## 6. 2026-09-21外部资料登记

本节只登记用户补充的资料来源及当前核验层级，不改变第一工位范围、M1任务集合或既有技术选择。只读清单和归档哈希见 `artifacts/station01/m1-continuation-20260921/external-source-inventory.md`。

| 资料 | 来源与只读清单结论 | 当前用途 | 核验状态及后续时机 |
| --- | --- | --- | --- |
| 外部前端原型 | `E:/dzk/gaode/原型.zip`；归档含3个HTML、10个JPG和3个PNG文件 | 仅作为后续前端页面和交互参考 | 资料已提供、归档目录已核对；页面行为及其与后端API的一致性尚未审阅。当前只开发后端，不新增前端任务；在前端联调前再核对原型与API。原型不得覆盖当前spec、CL-01至CL-06、工艺、安全或权限规则。 |
| 设备SDK资料 | `E:/dzk/gaode/软件开发SDK.zip`；归档目录可见Galaxy 2D相机SDK/Runtime 2.6.2608.9131、3D Camera Viewer 3.0.25、3D相机SDK说明书及光源说明书 | 作为T026/T027/T049真实设备适配前的资料输入 | 资料已提供、归档目录已核对；接口内容尚未审阅，真实适配尚未实现，未安装SDK、未连接设备、未作真机验证。详细接口核验安排在真实设备适配前。资料存在不证明OS、驱动、位数、回调/取消、协议或现场能力兼容，因此OPEN-22继续保留。 |

状态必须分别记录为“资料已提供”“接口已审阅”“适配已实现”“真机已验证”，不得以其中任一状态替代其他状态。本次M1仍使用进程内模拟适配器和既有算法有限等待规则，不改用真实设备或真实算法。
