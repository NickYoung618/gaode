# 测试、架构门禁与隔离验收合同（目标）

**当前适用范围（2026-10-02 10:54）**：通信代码边界最小收敛。保留现行业务语义/期限/取消/安全/保存规则；当前只核边界、正式接线、构建、既有内容检查/正负例和固定直接语义集合。完整动态PD/36CS/M/MC、持久历史/升级/预算/故障/整机验收转出，不作为本轮完成条件。见spec活动条款、verification-gates BM00及scope-adjustment；转出不是Passed。

适用009活动FR-023—032及SC-001—010，补充直接通信/保存/期限义务。已有检查器/旧完整清单和入口成果保留；新专项模式/固定清单调整及变体/报告尚待完成，完整V-BIND转出。本轮未运行任何验证。现有入口的真实能力见[quickstart](../quickstart.md)。

## V00 当前完成条件

当前活动只有BM00定义的边界/直接语义/必需账本与受影响构建。V01.1、V05/V06、V07/V09和S00是保留的后续动态、保存、历史、期限验收规范，不属于本轮必需执行清单；其生产规则和已有实现不撤回。当前源码本身的非法定义校验必须留在正式通信范围，不新增一套PD动态运行前置。

## V01 测试归属

- `Gaode.Rules.Tests`继续纯业务规则及架构检查；`Gaode.Contracts.Tests`中的业务契约测试文件只使用Application/Domain语义。该项目仍可保留未受影响的持久化等基础设施契约测试，逐文件分类，不把整个项目当业务边界，也不为纯化名称移动无关测试。
- 拟增`backend/tests/Gaode.Communication.Tests`，引用Infrastructure、纯协议模块、VirtualPlc及必要公共语义类型，承接实际线缆、编解码、TCP、内部时序、通信诊断和有限历史读取断言。引用业务请求用于调用真实适配器，不将原始字段加回业务端口。
- `Gaode.Integration.Tests`保留Host装配、真实库/媒体/Worker和业务结果断言。仅显式的装配/通信probe文件可接Host/Infrastructure/VirtualPlc；业务断言文件纳入扫描/冻结。禁止把业务断言改名为Support来豁免。
- 旧测试逐行义务承接见[迁移矩阵](../test-migration-matrix.md)。迁移时保留测试ID对应关系；删除只限有依据的失效要求，必须有替代义务验证，不保留Skip逃避门禁。

### V01.1 通信定义的动作前拒绝（C001；待实现）

直接依据为spec“必要边界与失败场景”的坏映射动作前拒绝条款及PC P03；这是既有要求的实现/验收补齐，不新增FR/AC，也不冒充迁移矩阵已存在的测试。唯一主要案例实现任务为T021（拟增ProtocolDefinitionAdmissionTests.cs），T028只复用其逐ID证据并核查所有正式访问路径接线。

**正式调用与阻断位置**：T018在通信范围实现定义和派生访问计划校验，并接入Host实际使用的LatestProtocolPlcDevice.StartAsync准备段，在启动初始化、心跳/轮询派发前完成。定义实例只有全部校验通过才进入可派发状态；命名访问器及同实例的动作准入必须拒绝未通过实例，阶段适配器不得另建旁路。T022—T027承接全部正式通道及装配；接口编排层只接收语义不可用原因，不读取、修补或兜底错误映射。任何本节定义错误均阻止该实例依赖映射的初始化/Ready/Start、心跳响应及动作目标/命令/确认清零写入，不允许先写一部分再报错。此规则针对未完成合法初始化的实例，不改变合法运行实例的心跳、停止及既有取消合同。

**有限校验判据**：占用比较必须包含地址区，同数值Coil与HoldingRegister不是同一存储位置；比较实际存储字段的区间，合法同一报警字段的bit解释不算重复存储字段。字段类型决定宽度，Float32为合法两word，位号须在其已声明字段宽度内。读取方向、写方及各确认/清零责任按PC P02/P04和已确认独立资料逐信号核对，不能用“反馈一律不可清零”等宽泛规则破坏现行合法时序。当前正式路径必需信号不得缺失。地址加宽度须在已确认工程地址约定/适用地址区容量内，生成的每个访问请求须满足对应传输合法长度；数值依据留在通信资料及独立oracle中，不提升为业务规则。

访问器必须先从合法字段计算可安全分组，再校验每个派生请求。可合法拆分的连续区间、非连续字段或跨洞布局应拆分；不能把整表跨度当成单次读取长度并误拒。当前ModbusTcpClient.cs:37—61已有寄存器/Coil读数量及地址边界检查，仍须保留，但不能等动作已经派发才由底层异常替代本节准备校验。测试中的非法访问计划输入仅是隔离fixture，不新增生产配置字段、信号或协议热切换。

**统一触发及观测E/W**：每条负例仅改变一项输入，其他配置、安全/关联及业务意图均合法；E为实际StartAsync准备入口，随后实际调用同实例RequestStartAsync尝试合法启动请求。校验失败必须在准备及动作准入留下相应拒绝，不能不调用入口便宣称零写。W覆盖该实例从E调用前至拒绝处理完成期间business/heartbeat两通道的全部写派发，以及被尝试的初始化/Start写；观察点位于生产访问器/设备到传输的边界，可采用记录型传输或同进程TCP组件证据，不要求每个非法fixture启动完整独立设备进程。不得用测试代码自行抛异常、假业务端口或只观察被改地址代替实际生产校验和完整写计数。

下列ID为“案例组/固定数据行”，全部为必需；预期原因名是本验证合同约定的通信校验诊断分类，不是新的业务状态或PLC编码：

| 稳定Case ID/数据行 | 唯一非法输入及适用判据 | 预期拒绝原因 | 正式触发／写入观测与必需证据 |
| --- | --- | --- | --- |
| PD-N01-overlap/same-area | 同一地址区内两个独立字段的实际占用相交；其他字段合法 | FieldOverlap | E/W；冲突SignalId、区间、判据来源及拒绝；全部受阻写派发0 |
| PD-N02-width/float32-one-word | 将一个Float32声明为一word，按已确认类型宽度应为两word | TypeWidthMismatch | E/W；类型/声明/期望宽度和拒绝；全部受阻写派发0 |
| PD-N03-bit/out-of-field | 一个已声明位号等于该字段bit宽度（从0计，已越界） | BitOutOfRange | E/W；字段宽度/位号和拒绝；全部受阻写派发0 |
| PD-N04-owner/read-direction | 把当前必须读取的反馈定义为不允许该读取方向，其他责任保持 | ReadDirectionConflict | E/W；SignalId、实际读取职责及来源、拒绝；全部受阻写派发0 |
| PD-N04-owner/write-responsibility | 把一个已确认PLC负责写入的反馈改成Host可写，或将其确认/清零责任赋给错误写方；固定选一项独立来源明确的代表 | WriteResponsibilityConflict | E/W；期望/错误写方及适用责任、拒绝；全部受阻写派发0 |
| PD-N05-required/missing | 移除当前正式启动路径必需的一个信号，不能以预留信号代替 | RequiredSignalMissing | E/W；独立必需清单、缺失SignalId、拒绝；全部受阻写派发0 |
| PD-N06-range/address-capacity | 一个合法宽度字段的末地址越过该地址区已声明合法容量；不使用未确认实机容量 | AddressRangeInvalid | E/W；区间/工程容量依据、拒绝；全部受阻写派发0 |
| PD-N06-range/access-length | 在准备阶段的派生访问计划Test输入边界把一个请求长度置0，其他定义合法；同生产计划校验拒绝，不在测试中自行抛错 | AccessLengthInvalid | E/W；原计划/注入后请求及合法长度依据、实际校验拒绝；全部受阻写派发0 |

每例记录fixture及独立判据摘要、实际生产入口/校验器版本、调用时间、拒绝位置/原因、实例可派发状态、请求关联、两通道写派发明细/计数和结果。access-length行采用有限Test输入替换缝，生产正常路径也必须无条件校验生成的请求；不暴露给业务或成为生产显式读组配置。缺正式调用、未分类解析错误、只有测试自抛异常或无法证明观测覆盖均非Passed。

| 稳定Case ID/数据行 | 合法输入与反误拒义务 | 相同入口的必需继续证据 |
| --- | --- | --- |
| PD-P01-area/same-number | 不同地址区使用相同数值地址，分别核对各自类型/责任 | E实际校验通过，RequestStartAsync准入并经W观察到合法初始化/Start派发；不能只断言“未抛异常” |
| PD-P02-width/float32-two-words | Float32占两个连续word，独立已知向量核对 | 同一E/W确有合法派发；字段两word完整、无误报重叠或宽度错误 |
| PD-P03-layout/noncontiguous-split | 字段非连续且整表跨度超过合法单次读长度，但每字段及按P03拆分的请求合法 | 同一E/W确有合法派发；列实际分组和每请求合法长度，无跨洞固定窗口；不能以整表跨度拒绝 |
| PD-P04-owner/confirmed-read-write-clear | 按独立已确认资料保留实际读取、写入及ACK/清零写方与顺序 | 同一E/W通过，复用T021既有真实TCP握手代表证据确认适用读写/清零继续发生；不得靠省略握手或全拒绝通过 |

正例用与负例相同生产校验、入口和配置解析；可复用T021原有同进程VirtualPlc/TCP装置及握手证据，不建设另一套模拟验证器。以上8条负例和4条正例均逐行登记；参数化方法名不是完整Case ID。结构/责任校验只证明定义可安全解释，不能证明地址/编码与实际PLC完全一致；V06合法变体及独立oracle仍各自必需。

## V02 有限可执行边界检查

实现位置：Rules/Architecture内一个检查器与对应测试；扫描输入为实际仓库编译源、csproj引用、已登记业务合同的规范性接口/示例段落、业务测试源及精确分类清单。新Application/Domain源默认保护；新增测试/合同必须分类，遗漏分类失败。扫描器不存在、文件枚举为空、语法/符号绑定失败或存在未核对公共端口字段均失败。

C#检查采用固定SDK 10.0.401的Roslyn程序集（本机只读核对`Microsoft.CodeAnalysis`和`Microsoft.CodeAnalysis.CSharp`均5.9.0.0）；在测试项目显式引用SDK内程序集并复制运行依赖，缺失即失败，不自动降级为grep。此测试依赖已有实施成果；新专项正式运行仍须取得本轮证据。无需新生产依赖或通用分析器服务。

| 规则ID | 检查内容/判失败依据 | 必要限制 |
| --- | --- | --- |
| A01 依赖 | Domain→无业务外依赖；Application→Domain；Infrastructure→Application＋纯协议模块；Host→Application/Infrastructure；VirtualPlc→纯协议模块。按符号检查分类为业务断言的测试文件不引用Infrastructure/VirtualPlc/协议或传输类型 | 保留现有合法BCL/测试框架及Host装配依赖；混合Contracts项目的基础设施测试可保留项目引用，不能据此豁免业务文件；纯协议模块自身不得依赖业务/DB/网络。 |
| A02 公共形状 | 递归核对设备端口、结果、事件、可达DTO/枚举和业务持久投影字段。未登记数值状态、raw数组/字典/JsonElement逃生口、协议枚举值绑定、内部阶段公开均失败 | 采用data-model定义的业务字段形状清单，字段用途有依据；物理槽/坐标/容量/面/期限合法。不是为每个数字建白名单。 |
| A03 原始值使用 | 按符号追踪协议/transport/raw诊断来源经过局部变量、属性、参数、返回值的赋值及有限方法调用；对其比较、switch、强转业务枚举、mask、数组切片/固定偏移或控制分支失败；改名不改变来源 | 公共端口的新DevicePhase:int会先被A02拒绝。普通业务枚举分支与坐标计算不受影响。不承诺识别任意恶意混淆的程序。 |
| A04 协议注入 | Application/Domain及业务测试引用地址/寄存器符号、协议点名/命令常量、内部ACK/清零控制节点、以已知协议值定义业务状态均失败 | 禁止要求业务契约公开这些字段；通信基线说明、明确的禁止条款/历史研究引用不是规范性业务接口，不以全文数字正则误报。 |
| A05 诊断绕行 | 业务引用raw reader、解析诊断引用、从诊断JSON/属性包读取raw再决定准入/成功、把观测目标当实际值均失败；业务日志只能输出已知语义/引用 | 可保存/透传opaque reference；业务已有数据查询/算法载荷不得被一律禁止。 |
| A06 通信旁路 | 正式通信适配器读写业务信号必须经命名访问器；低层transport调用只允许在该访问器/传输、显式通信probe中。重复生产映射/字序实现、硬编码信号范围/索引失败 | MBAP/PDU编解码和字段内部双字布局合法；VirtualPlc设备状态机可处理原始协议，但只取统一定义。 |
| A07 测试/文档归属 | 业务测试断言端口暴露地址/原始状态、业务规范接口表/请求示例要求这些字段、未分类新测试文件失败 | 历史payload原字节断言归通信历史读取测试；不能扩大豁免目录使业务代码逃逸。 |

检查清单在一次性改造审阅后冻结；不得为使违规通过扩大白名单。对全新业务数值/载荷的合法调整需说明实际含义并更新对应业务契约，而非笼统标Allowed。人工审阅仍逐公开状态核查含义，静态检查作为可执行最低门禁。

**009实施期装配规则核对**：Host的`composition-api`分类仍进入同一内容检查器。只允许实际装配所需的四种已登记通信类型作为构造/DI类型参数、明确的PlcRuntimeOptions初始化字段，以及正式设备StartAsync/DisposeAsync生命周期接线；类型名不等同于原始观测值。协议字段、未登记设备调用、原始返回值比较和把设备对象交给任意helper仍须拒绝。`N11-composition-raw`、`N11-composition-helper`与`P07-composition-lifecycle`、`P08-composition-options`进入固定必需清单；不豁免Host目录或装配文件。既有泛型配置形状按已登记定义检查，并继续递归核对实参，不能用泛型包装raw逃过A02。源码扫描通过仍须以完整报告为据，单个合法装配样例通过不代表产品通过。

### V02.1 非C#业务断言的有限内容检查（R03）

采用一个待实现的`scripts/check-009-script-boundary.py`入口调度三个有限检查器；不建立通用跨语言分析平台。JS/CJS/MJS/TS使用仓内`frontend/package-lock.json`锁定且本机已安装的TypeScript **5.9.2**编译器API，`createProgram`/CompilerHost、allowJs/noEmit、语法诊断及AST；Python使用当前解释器标准库`ast.parse`；PowerShell编排/采集采用随当前PowerShell提供的`System.Management.Automation.Language.Parser.ParseFile`。Python/PowerShell版本随门禁报告记录并与冻结工具链一致。解析器缺失/版本不符、语法错误、输入空或不支持的依赖表达不能降级grep或“零违规”。

**固定覆盖范围**：分类清单双向枚举`scripts/`全部第一方上述语言文件、`frontend/tests/`（如存在）的测试文件，以及从这些文件递归可达的本地helper；新增路径/文件/导入须分类，缺目录不得替代已有必需文件的存在检查。只固定排除依赖目录、编译产物和历史证据包，不允许把源脚本移入排除目录作为可执行验证输入。角色限BusinessAssertion、CommunicationProbe、Collection、Orchestration、Tooling；每项记录具体路径、入口/调用方、允许输入schema、输出义务及旧断言承接ID。Tooling只限解析器/runner本身等明确工具，不能包含未审查业务判据。

混合文件先拆：`verify-station01-page-diagnostics.cjs`、`validate-008-operation-evidence.py`、`validate-008-route-evidence.py`、`validate-008-flip-timeout.py`、`audit-008-night-page-route.py`、`validate-008-authorization-page.py`保留语义/API/真实数据库/媒体断言；PLC原始地址、值、内部时序迁入拟增`scripts/communication/`精确探针。`run-station01-diagnostic-api.ps1`中的原始写次数判据也迁出，PS只做编排。`verify-q01-q02-test-page.ps1`、backend-route、auto-removal及其消费者继续执行承接结果，不能删除非零退出传播。`verify-latest-plc.py`及`scripts/tests/virtual-plc-monitor.test.cjs`明确属通信，原始断言合法。业务模块不得导入通信探针或读取其raw输出；编排分别汇总两类结果，业务成功断言不能由通信probe的Passed代替。

| 规则 | 解析范围及拒绝标准 | 合法边界 |
| --- | --- | --- |
| A08-script-content | 解析完整文件，检查assert/check/expect、checks字典、比较/if/switch/match/条件表达式、结果汇总和退出值及其数据依赖闭包；以[影响矩阵§5公开字段表](../impact-matrix.md#5-公开字段与历史表示映射r04)、device-semantics/1事件schema和输入来源注册表标注对象。业务范围访问协议字段、对原始状态数值比较/转换、位运算、固定wire下标、内部ACK/清零断言均拒绝；包含可选链、属性和字符串下标访问。 | HTTP status数字、语义原因/回执/可靠性、物理槽/坐标和图像数组允许；不能仅因字段叫status或数字为2就拒绝。通信角色依据独立oracle访问raw合法，但不得含业务完成判据。 |
| A09-script-closure | 对局部赋值/解构/别名、参数/返回值、列表与生成器推导、直接本地helper调用做有限来源传播；对循环/分支以来源集合合并直到稳定。Python既有prop(obj,"status")按参数化属性访问摘要处理；CJS别名不消除来源。受保护对象的动态键、反射/eval、无法解析的导入/调用、逃逸到未知helper或控制路径来源不明均失败，要求改成可分析的明确语义写法。 | 支持冻结的直接调用图/显式业务schema/实际使用helper摘要；通用HTTP/JSON/文件读取仅在登记的边界适配helper得到明确schema。真实算法载荷另有业务schema，不泛禁JSON；未建摘要不默许外部函数消费设备对象。 |
| A10-script-inventory | 枚举文件/递归本地导入与角色清单双向核对；新增/删除/移动导致必需文件缺失、未分类/未解析文件、未登记公开字段、源码输入不在枚举集均失败。脚本含业务API状态/结果断言却被标CommunicationProbe/Collection/Orchestration时也失败；重新分类必须有拆分后的数据入口限制及旧义务承接，不能整文件豁免。 | Collection只原样导出已登记包，不判断完成；Orchestration只比较进程退出、HTTP受理、路径/文件可用并汇总两份报告；冻结类别之外新业务脚本默认保护而非忽略。 |

PowerShell只保留有限编排/采集能力：AST对已登记API/PLC响应对象经赋值、成员访问和管道`Where-Object`到比较/位运算的消费执行A08/A09；发现业务完成断言必须迁入受保护JS/Python，复杂/不支持传播失败。允许原样导出诊断包及退出码/HTTP错误/文件存在检查，不为PS新增完整业务分析器。无法静态确定的动态路径/命令不能带受保护对象逃逸。这个限制覆盖现有脚本，不以人工审阅代替自动内容拒绝。

结果格式固定为JSON：`schemaVersion, runId, ruleSetVersion, parserVersions, sourceDigest, inventoryDigest, files[{path,role,parsed}], cases[{caseId,kind,expectedRule,observedRule,outcome}], violations[{ruleId,path,line,column,sourcePath,message}], errors[], result`。正式源码应零violations/errors且覆盖全部登记文件；负例的Passed表示正确命中指定违规，不表示该违规源码合格；检查器错误与内容违规分开，均不得签发正式通过。源码、内存/专用负例和正例调用相同解析/传播函数及规则配置，差别仅在输入位置。

冻结摘要只证明修改范围，AST门禁证明上述有限边界拒绝能力，必需执行账本证明实际运行完整性；三者不能互相替代。有限检查不承诺识别任意恶意程序或所有自然语言意图，超出支持语法时拒绝而非忽略，不增加无限白名单。

## V03 使用同一检查器的负例与正例

负例存放测试内存源/专用测试fixture，永不编入生产。运行与正式源码相同入口、规则配置、符号分析；断言具体rule和位置，不能用一个永远失败的假检查器。

| Case ID | 最小违规输入 | 必须失败规则 |
| --- | --- | --- |
| N01-address | Application动作增加寄存器/点位常量或调用原始读写 | A01/A04/A06 |
| N02-renamed-state | 新端口`int DevicePhase`包装原始反馈，业务用数值判断完成 | A02/A03；不能因名称不含Protocol而放过 |
| N03-cast | 原始协议枚举/整数强转业务枚举后比较完成 | A01/A03/A04 |
| N04-bit | 报警位mask进入安全/完成分支 | A03/A04 |
| N05-offset | 固定报文偏移、字数组下标解码后推进业务 | A02/A03 |
| N06-handshake | 业务流程等待ACK/清零内部阶段后自行判动作完成 | A02/A04 |
| N07-diagnostics | 解析诊断引用或raw JSON后判断安全/成功 | A05 |
| N08-business-test | 契约测试要求Application公开地址/编码，或引用共享协议模块生成预期 | A01/A07 |
| N09-business-contract | 业务合同规范接口增加原始状态字段/线缆完成值 | A02/A07文档规则 |
| N10-bypass | 正式适配器新增裸offset读、固定相邻状态数组或第二份生产映射 | A06 |
| N11-cjs-alarm | 当前CJS代表式`const d=facts.run.startupDiagnostic; const f=d.reliableFeedback; check(f?.alarmBits===2,...)`，包括局部别名 | A08；正式脚本扫描同入口检出 |
| N12-python-sorting | 当前Python代表式`next((prop(s,"status") for s in evidence if ...),None)==2`及放料3/清零0、SortingAckCleared断言 | A08/A09；2/3/0三条数据行各有必需caseId |
| N13-script-bit | JS/Python把raw报警别名按位与后作为安全/成功判据 | A08/A09 |
| N14-script-escape | 动态字段名、未知helper接收受保护设备/诊断对象 | A09 |
| N15-script-unclassified | 新增业务脚本/本地helper未登记，或把业务断言重标为通信 | A10 |
| N16-script-parser | 破损语法、解析器缺失/错误版本 | 检查器错误，正式结果非Passed；不能当无违规 |
| N17-ps-raw | 当前PS代表式`Where-Object {$_.documentNumber -eq 9 -and $_.value -eq 1}`用于证明业务无后继 | A08；迁到通信探针后相同raw断言合法 |

合法正例至少P01面数1/2/4及槽号、P02坐标/容差和期限数字、P03稳定语义枚举判断、P04既有图像/算法数组及业务JSON载荷、P05只保存/输出诊断引用、P06传输内合法PDU编码。每例必须被扫描且无违规；不能靠不加载样例通过。基本五类N01/N02/N04/N05/N06全部必须失败，额外强转/诊断/测试负例同属必需。

非C#正例P07-js-semantics（稳定reasonCodes/可靠性、HTTP202/401/403）、P08-python-semantics（当前身份及有效提交回执、物理槽与序号区别、业务图像数组）、P09-opaque-orchestration（JS/Python透传opaque引用及PS只汇总退出码/导出文件）、P10-wire-oracle（通信探针按独立literal校验报警位、取放码及ACK）同属必需。逐例扫描到实际AST且无违规；业务与通信例不得仅靠未分类不加载通过。

## V04 当前固定必需执行账本

T053新增独立范围版本`009-boundary-minimum/1`、Scope=`BoundaryMinimum`的有限固定清单`scripts/workflow/009-boundary-minimum-cases.json`，在发现前从已审阅源/原固定清单登记准确方法和数据行，不从本次发现生成期望。旧`009-required-cases.json`及完整Baseline/Gates/Mutation语义保持。

T054在已有入口增加BoundaryMinimum分支，复用生产`runner.read_trx`、`test_identity`、`validate_required_ledger`和现有脚本/账本selfcheck，仅作构建/发现/测试/核证的有限接线。禁止在此分支启动Host/PLC/Worker/页面或完整动态编排。新分支/清单在实现、验证前标待实现，不混报整体结果。

必需：仓库4项扫描、2层依赖、PinnedRoslyn、C#全部现有N/P与合同负例；JS/CJS/Python/有限PS正式源码及分类、42正负样例；G01—07同生产账本selfcheck；BM00直接语义逐数据行。固定文件不存在、未分类、解析错误、零发现、必需行缺失/重复/过滤/Skip、旧run或旧源/构建均拒绝。合法诊断raw仅允许精确角色及公开字段，不豁免业务消费者。

记录run/source/manifest/build身份、真实构建和测试命令/返回码、发现/TRX/源扫描/逐case账本及附件摘要。source覆盖实际受保护源码/合同/断言/helper/入口/规则/清单（含HTML直接消费者）；不以hash代替内容检查。启动后源发生变化须重新取证；允许最终任务证据注记不影响业务构建，不能拼接旧报告。G01—07使用现有生产校验器，不写新的宽松实现。

## V05 冻结与允许变化范围（后续转出义务，非BoundaryMinimum前置）

冻结时点唯一为T061通信专项基线真实通过之后。其前置包括全部活动实现/直接消费者、受影响构建、固定专项发现执行和TCP/必要DB/PD12证据；不要求完整BA/SU/L/页面、T044/T046/T060。解除转出验收排期不解除真实代码/保存接线依赖。
冻结Application、Domain、正式Host装配/业务API类型和直接消费者、非通信Infrastructure业务编排/持久门禁、业务合同/语义断言、选定配方/点位及现用完整预算配置（含Test10000ms）、取消/期限及保存语义、PD断言、内容检查器/分类/固定清单/专项入口及工具链。记录新增/修改/删除与path/SHA、范围版本、run/build及断言清单；无需Git。当前稳定预算/配置不得为变体调整；无需先完成转出功能的完整验收才冻结现用成果。
允许变体修改的精确路径：Gaode.Plc.Protocol定义/codec；Infrastructure/Devices/Plc必要寻址/解释/内部握手；VirtualPlc协议消费/设备协议处理；精确通信测试/探针及独立变体预期。通信目录里的业务事务/占用/期限门禁不在允许范围，PD文件/断言即使位于通信测试也冻结；业务合同/接口/语义断言不得变。每变体提供逐文件职责和独立批准的Test变化，不笼统允许整个Infrastructure。
M01—05分别从同一通过专项基线派生，不叠加；TCP和相同直接语义断言/必要SQLite验证，不要求完整相机/算法/媒体/页面。预算/时钟/配方/保存/门禁不能改来凑通过。MC02至少包含业务断言及预算/门禁改动拒绝。负控制只在隔离副本，最终基线不得保留错误。

## V06 协议变体和独立预期（后续转出义务，非BoundaryMinimum前置）

| 变体 | 明确Test变化 | 必需验证 |
| --- | --- | --- |
| M01 非连续地址 | 在既有VirtualPlc容量内：HeartbeatReq→Coil0030；TargetX→0060（两word）、Y→0064（两word）；NG/Pending→0068/006A；Inspection/Reset→006C/006E；FlipStatus/Face→0070/0072；AlarmBits/Severity→0074/0076。其余沿基线；先查重叠/方向/宽度 | 初始/心跳/轮询、区域/XYZ、复位/翻面和报警实际用新地址；不存在旧窗口/相邻字段假设。全部只是Test保留区占用，不批准生产使用。 |
| M02 报警位 | 将已确认“抓取失败”测试位从Bit10移到未用Bit13，原Bit10在变体标未定义；含义和严重性不改。单报警＋与急停组合 | 相同语义报警和保守安全处置；新raw位准确；未知位不解释为无警报。 |
| M03 命令/反馈 | 旧009的11/12、31—35仅属原协议Test变体。011受影响通信回归须按新取料成功/放料完成/失败映射核对；变体数值只作受控通信输入 | 真TCP取料→实际在途保存→放料/抬升，旧值/未知值不得当完成；业务只看相同动作语义，不能维护旧2/3或ACK要求。 |
| M04 字序 | 从基线ABCD变为CDAB（若选定实际基线不是ABCD，则显式选不同的受支持排列）；保持配方坐标/单位/容差 | 写入与实际反馈解码两方向；123.456四排列独立已知字检查仍全部执行；实际动作坐标与基线一致。 |
| M05 内部步骤（AC-005，历史专项编号保留） | 旧Sorting_OK后读回变体不直接搬到新协议；仅对实际受影响内部步骤复用通信隔离验证，不新增物理信号或机械工艺 | 缺反馈/超时不能成功，原deadline及取消不放宽；业务/API/断言不能依赖内部步骤，具体选择在011 plan按影响确定。 |

oracle存放通信测试专用资料，以已确认Word逐项人工录入并复核；变体oracle来自本表明确Test说明，独立编写字/地址预期，禁止从生产SignalDescriptor/codec/VirtualPlc表生成预期。独立TCP探针直接核对实际功能码、PDU地址、字/位、方向和时序；可以复用通用报文解析，不能用被测生产映射解释期望。双端原始审计辅助交叉核验，不是业务控制输入。

负控制MC01：在一个独立隔离副本把Host和VirtualPlc共享的同一映射改错，oracle保持原值；即使两端仍协同完成，通信预期核对必须失败。MC02：把任一冻结业务文件改动，范围核验必须失败。负控制失败是预期结果，不能计为产品成功或把其代码保留到生产。

V01.1的PD检查结构/访问/写入责任有效性，非法定义在动作前被拒绝；M01等采用合法定义验证隔离及实际可执行性；MC01应选结构可合法通过但与独立oracle不符的双端共同错误，必须被独立报文预期检出。三者不能互相代替：PD通过不证明映射全正确，M01成功不证明坏定义会拒绝，MC01不能只靠制造重叠等结构错误被PD挡住而算通过。独立expected始终不由生产定义生成。

每个M01—M04均运行同一V-SEM及V-BIND业务断言清单和一个覆盖实际取放保存的完整代表主流程；V-BIND的真实TCP/保存代表也按V09执行，不因变体过滤数据行。还执行覆盖受改信号的通信路线（例如自动翻面/人工占用/复位）。基线最小代表集合见quickstart，公共路径复用但不能漏被变更信号。4/4、冻结文件差异0、业务断言通过100%且动作/安全/期限/保存结果差异0才满足SC-003/004；不把BA全组与全部配方做笛卡尔积。

## V07 原完整主流程及升级验收（已转出，非专项前置）

本节原条款是转出义务和原案例的追溯定义；其“必须/完整/基线”等措辞仅用于后续对应业务验收，不作为009活动门槛。直接通信部分改由S00承接，既有业务规则不失效。

正常代表集合按实际运动顺序选定，见quickstart。必须实际使用Host、独立VirtualPlc、实际Worker、文件相机、SQLite、媒体与API，PID/加载程序集/端口/源码/配方摘要同run记录。仅同进程TCP测试另列为组件证据。后端验收不声称页面或真机通过。

| Failure ID | 注入位置与当前保护义务 | 必须可见结果 |
| --- | --- | --- |
| F01 | 当前动作无反馈/反馈过期/上动作残值（有限代表，不与所有配方相乘） | 无Completed、原deadline到期/反馈失效原因、保持必要占用 |
| F02 | 实际位置错误；自动翻面实际面错误 | 不以目标替反馈，不开始依赖采集/取放/下一面 |
| F03 | 命令写尝试后断线或epoch改变 | UnknownHeld，无自动重发，实际写入数/代次可查 |
| F04 | ACK未闭环或本轮Z复位超时 | 不释放周期/不报告完成；冻结子期限及总期限均未放宽 |
| F05 | 可靠取料后，InTransit真实存储边界的A提交前失败、B提交后失效回执、C暂不可核查 | 三者均无有效放料批准、无放料槽/目标/命令且UnknownHeld；A有确认回滚且无本次提交，B必须查到真实提交行，C不预断行存在与否；raw可查 |
| F06 | 取料已观察后，必要raw批次的A提交前失败、B提交已成但引用回执失效、C暂不可核查 | 无当前有效完成引用、无正常CommitPick批准链、零后继/物理重发；A无批次，B实际批次存在，C待核查。业务最小UnknownHeld失败记录与尽力日志按EC E02.1，不能称取料未发生 |
| F07 | 配方应用中I/O均及时成功、心跳正常但前置始终不成立（BA03两容量），及BA04/05指定迟到/取消点 | 总窗仍关闭准入，失效后新派发绑定写/有效Bound/移交授权/产品动作均0；心跳/观察/必要停止保持；已发I/O和真实提交迟到记录不抹除，不误称无数据库行 |

F05/F06仅选上述必要分支，不与所有配方相乘。待实现的Test故障缝位于实际StageEventStore事务和TraceWriter通信job：A在写入/提交前造成真实SQLite拒绝并确认回滚与任务终止；B先完成真实Commit，再扣住回执至调用窗口/原动作期限失效，独立只读连接确认行存在；C在提交边界阻止调用方确认，并暂阻核查，待开放查询后按实际记录归类。必须记录故障命中时刻、事务/事件/批次ID、commit与回执时间、原deadline、SQLite行/关联/摘要、TCP后继零写及Held。现TraceWriter只对WriteBatch有afterCommitBeforeReceipt hook，不等于StageEventStore/rawjob已有能力；对应入口均待实现。提交前异常或fake callback不能覆盖B/C，不能修改业务行伪造结果。

F06还需有限两种保存可用性：仅rawjob失败而业务可写时，查询真实PickEvidencePersistenceUnconfirmed/UnknownHeld；同库不可写时核验内存准入关闭及设备零后继，重启按已提交预留＋意图保持Held。后者若日志也不能写，报告新增事实缺持久证据，不能把它计入SC-009三种应能保存的正常诊断场景或声称全存储故障仍保存成功。F05-B/F06-B只读恢复核查后再观察一个受控窗口，确认晚记录/引用不复活过期动作、不重发物理操作。

V-HISTORY仅增加本次单项schema升级的中断证据，严格采用模型§6.1。**U1是提交结果未知**：任何中断或异常后保持Host/相关写者停止及维护隔离，由持锁维护进程让SQLite恢复，在独占条件下核查实际结构、精确迁移集合、StoreId/Profile与同库Manifests；完成核查前不开放Host、不重跑DDL。原维护进程退出时，后继维护须重新获得同一独占锁再核查，不删除journal/WAL，不凭异常、退出码、回执缺失或一次查无新表推定回滚。

核查后只归U0完整源态、U2完整目标态或UX不一致/不可读。U0须确认原事务结束、源全项一致且源/备份重新核验后才允许重做唯一升级；U2须核验完整目标、integrity_check/foreign_key_check、旧payload原字节及媒体引用不变，后续维护调用不再执行本次DDL，维护锁释放后Host重新取锁并通过目标Probe才可读写；UX继续拒绝开放和自动重做，仅按模型§6.1已核验备份的受控恢复规则处理。无可信备份或媒体失配继续阻塞，不改旧payload或版本字符串补洞。

| 稳定Case ID/数据行 | 真实故障位置与调用 | 必需状态和完成证据 |
| --- | --- | --- |
| SU01-before-commit/after-ddl；SU01-before-commit/after-history；SU01-before-commit/after-manifest | 三个受控维护副本分别在真实单事务DDL、EF迁移记录、同库manifest写入后且Commit前中断维护进程 | 保持隔离；SQLite恢复后逐项查全，确认U0及原事务结束；重新核验源与备份前重试被拒，核验后才可执行一次升级。记录真实中断点/进程/事务与恢复后结构/精确迁移/身份/manifest，不以预期异常代替 |
| SU02-after-commit/before-receipt | 真实Commit已完成、维护结果回执尚未交付时中断；再调用正式维护入口核查同库 | 查得完整目标须归U2；后续维护调用本次DDL执行次数0，不先尝试DDL再靠“表已存在”异常收场。记录实际提交和失去回执的顺序、完整目标/原数据/媒体不变及Host开放前Probe证据 |
| SU03-unresolved/hold | 在实际中断副本的恢复核查尚未完成时，受控暂缓核查并尝试维护重入及Host准入；不伪造已知提交状态 | U1仍未知，Host被拒且DDL新执行0；核查恢复后才可归U0/U2/UX。记录锁持有/重入拒绝、未分类期间准入和DDL调用观测，不能仅断言“未运行工具” |
| SU04-inconsistent/reject-restore | 既有有限不一致副本经真实Probe拒绝；仅在独占下按已核验同StoreId备份恢复 | UX拒Host/自动重做；保留现场、备份/旧payload及媒体核验，恢复后再归U0。无可信恢复依据仍拒绝，不建设额外灾备流程 |

SU全部6条数据行属于原完整V04账本及后续升级验收，不进入当前专项V04活动集合。每行关联维护副本、StoreId/Profile、实际工具/构建/源摘要、故障时刻、事务阶段、锁及恢复/核查记录；核查必须涵盖实际结构、精确迁移记录、同库manifest、旧payload和媒体引用，不能只比较版本字符串。DDL计数来自实际执行路径/数据库命令观测，不从工具退出码推算；fake异常或单个提交前异常不替代commit后回执未知。以上是待实现、待运行的有限维护验证，不是本轮升级或测试结果。

另按迁移矩阵T17保留公共3D的必要失败清理验证：实际采集/保存失败后，仅在原安全/取消条件允许时尝试结束当前周期；原业务仍失败、无后继动作、无虚构保存引用。清理与原失败分别可查，取消/安全关闭时不清理。F的采集记录成功门禁保持，不把3D的既有退出策略推广为所有采集角色的失败处理。

## V08 当前证据包

T061/T069只签`BoundaryMinimum`。必需受影响工程构建、正式源码/合同/测试及脚本扫描、同入口正负例、固定直接语义发现执行/零Skip、账本拒绝和人工接线证据；任一缺失不能通过。不需要S00/PD/M/MC/TCP/历史升级完整证据，既有失败留存。不把本结果送入旧完整Baseline/Mutation签发逻辑。

## V09 配方应用完整业务验收（已转出；FR-035—039来源保留）

本节BA表是原009旧设备绑定的历史追溯定义，不作为011新F软件绑定或009活动门槛。旧两容量/显示读回/raw绑定用例随协议替代退出当前集合；仍有效的预算、真实提交、关联、取消、回执迟到及独立bind不授权续接保护，定向迁移至011 M08/M09。历史失败和已存证据保留，不重建009验收体系。

以B03.2、P04.1及模型§3.1为判据：`D=t0+冻结预算`，`T=min(D,已有适用截止)`，每次保存另受原CriticalSave。沿原ResponseBeforeDeadline，Host接收且校验完成在[t0,T)；单次I/O或设备时间戳不能关闭/回填总窗口。

| Case ID及必需数据行 | 保护义务/方法 | 必需证据与拒绝条件 |
| --- | --- | --- |
| BA01-source：strict/legacy/api | 三入口读关联运行完整冻结budget；冻结后变更最新配置，活动值不变。Test10000、Production分离 | RunId/BindingId、ID/version/purpose/source/digest/snapshot和值可追溯；缺入口、改成PlcAcceptance或HTTP token即失败（FR-035/039、AC-022、SC-011） |
| BA02-success：capacity/none；api-existing-handoff | 正式链两容量分别真实应用/持久，API核验已有handoff并保存本次意图/绑定事实，不重建已有交接 | 意图真实commit且有效receipt先发生，随后登记t0，再排队/端口；同tick由关联事件序号/准入顺序证明因果，不强求时钟读数递增。设备/raw/绑定/适用handoff回执均<T及各自CriticalSave内；DeviceApplied不能提前Bound。真实TCP/SQLite/API证据缺一未通过（FR-036、AC-023） |
| BA03-healthy-wait：capacity/none | 独立VirtualPlc在B开始后保持其真实前置条件不成立，TCP读写及时成功、心跳继续；等待实际Test总期限。注入只属Test能力，待实现 | 超期原因/窗口可查；失效后新增配方应用写0、有效Bound/handoff授权/产品动作0，观察/心跳/必要停止仍可用。断线、暂停心跳、F公共复位失败或fake端口不能替代；各I/O成功不掩盖无界等待（FR-036/038、AC-024） |
| BA04-boundary：before/exact/after；late-bound/late-handoff | 精确受控Host时钟复用OperationIngress/DeadlineScheduler方法，最后必要回执T−1/T/T＋1；另在真实SQLite RecipePlanBound及适用handoff commit后分别扣回执到T失效，设备先及时完成 | 仅before有效；提前齐备不因稍后调度倒判迟到。late两点必须有实际行/WriteId/commit及延迟receipt，不以提交前异常替代；只读核查后仍无续接。必要raw保存的迟到回执同判据，并承接F06真实存储分支（FR-019/036/038、AC-025、SC-012） |
| BA05-cancel：pending/inflight | 当前请求等前置时取消；一次真实I/O已派发而后继待发时取消。以Test受控派发栅栏复现，业务取消链确实传到内部推进 | 关闭资格/派发原子先后记录与真实TCP对应；取消后新派发该请求写0，不仅是调用Task取消。取消前已派发I/O与已开始提交迟到如实保留、后继成功0；无关联派发证据未通过（FR-038、AC-026） |
| BA06-downstream：strict-before-port/strict-during-save/strict-before-next/legacy/api-existing/api-none | 严格链原三段截止不变：准入前、绑定保存中或进入后段前已过期均拒绝；旧链保持handoff后首个Detection起点；API只用实际已有期限 | 对照保存的原起点/截止与本次T，刷新/补偿数0；无既有期限时不能创造后段窗口。有界保存不能靠晚回执恢复已过期段（FR-037、AC-027） |
| BA07-config：valid-test/missing-or-null/nonpositive/noninteger-or-nonfinite/overflow/version/purpose/source-or-freeze/production-unapproved | 使用新版schema/加载/验证正式链，按各类别展开实际不等价非法值行；旧缺项预算不补默认值，Production不回退Test | 连续链在运行校验时拒绝启动；独立入口绑定写前拒绝；合法10000进入BA02/03。JSON无法表示的非有限值由解析拒绝；内部构造绕过JSON的同类非法值也由语义验证拒绝。无绑定设备写、具体原因/冻结来源可查（FR-039、AC-028、SC-011） |

BA02/04另记录t0前意图保存的有限CriticalSave及已有较早截止：意图未取得有效提交回执不得调用设备。保存测试复用TraceStoreTests的真实事务及afterCommitBeforeReceipt基础，但须按BindingId/WriteId/保存种类精确选择本次绑定事实或handoff；现RunCreated案例不是绑定验证。rawjob故障缝仍待实现。未发起的新成功链保存禁止；已开始的提交留下事实不构成失效后新发写。失败/迟到记录沿原有限保存规则独立收尾，不延长动作资格。

BA04 late-handoff同时核验实际消费者和GET投影：库中handoff存在但回执失效时，PublicPreparationHandoffV2无当前有效Receipt不能续接，QueryEndpoints不得重建可用Bound/Ready，Detection新派发0；不增加另一故障组合。before行则证明按期完成后稍晚调度不倒判超时，后继仍核当前取消/安全/后段截止。原事务没有未来Host回执时间，独立Audit观察与真实commit按WriteId连接，缺观察不推ValidCurrent、不改旧handoff。

BA01/04/06/07的规则级部分允许原有受控时钟，不能用它冒充BA02/03/05的真实链证据。后者使用真实Host、独立VirtualPlc/TCP、必要Worker/文件相机以及SQLite/媒体/API；每组只选覆盖该义务的代表，不穷举配方。窗口、预算来源、Host接收/校验、实际提交/回执、写派发及取消时间线必须落入可持久查询证据；时间日志缺失不能按进程退出0判通过。所有新增fixture、绑定注入缝及编排入口仍待实现。


### V02 实施核对：类型来源精度（2026-10-02）

009-boundary/2继续同入口检查正式源码和N/P。A03/A05的来源按实际Roslyn类型及有限诊断类型集（真实RawExchange/RawHttpExchange、通信批次、存储、实体、reader/document），递归检查数组/泛型参数、局部值、方法返回及所属类型；不按任意测试类名含Handshake/CommunicationEvidence认定raw。N07-qualified-reader/exchange证明改名参数及真实诊断类型仍拒绝；P04-semantic-historical-name/storage-helper-name证明无raw字段/类型的语义历史名称、仅数据库路径helper可通过。此项修复checker183red暴露的误报及RawExchange漏报，不增加任何源路径豁免，不放宽A01/A07的业务测试依赖限制，旧有效负例继续必需。四个新增数据行进入固定清单并受发现/执行/报告核证；检查器通过不等于产品源码或009通过。冻结前仍需完整源扫描和账本。


### V02.1/T50 实施核对：脚本拆分与字典来源（2026-10-02）

`009-script-boundary/2`以同一Python AST把字面键`.get()`、`prop()`、下标和局部别名纳入来源追踪。`plcWriteAudit/plcAudit/plcChanges/plcState`为明确通信审计根，不能由业务脚本读取后据raw判断；不是扫描字符串关键词取代AST。N12-python-sorting/audit-get、changes-alias拒旧代表，P08-python-semantics/business-get允许合法业务值。JS/Python对startup仅承接实际`StartupDiagnosticApi`字段（包括connectionEpoch、observedAtUtc、recordNature、rawAvailability），已不存在的顶层observationId不作为影子兼容；新N/P已固定登记。

T50实际分离入口：`verify-station01-page-diagnostics.cjs`和`semantic_009_evidence.py`保留业务语义；`scripts/communication/check-009-diagnostic-wire.cjs`、`check-009-operation-wire.py`、`check-009-route-wire.py`承接原地址/位/协议值/握手/轴与写入义务。业务不得导入这些probe；`verify-009-page-diagnostics.ps1`、`verify-009-saved-route.ps1`与原`verify-q01-q02-test-page.ps1`分别执行并拒绝任一非零结果。成功的业务子报告不能替代通信子报告，反之亦然；新入口接T053/T054固定清单及本轮证据后才可作完整验收。通信probe本身仍受分类及“不可包含业务成功批准”规则约束。

取放raw核验读取同次真实SQLite `PlcCommunicationEvidence.RawPayloadJson`及摘要，按独立原件oracle解码报文、检查取放与最终清零顺序；不能从语义StageEvents重构历史协议值。组件fixture验证器用例不冒充现场报文或MC01独立进程负控制。原业务槽位/关联/真实InTransit先存后最终转运保存分别保留；全链保存门禁仍须T059的故障及实际派发证据。script202的36样例/正式脚本零违规仅是该扫描执行结果，C#、完整账本和独立链路仍未完成。
## S00 通信专项固定验证集合（规范性，范围版本009-communication-specialist/1）（后续转出义务，非BoundaryMinimum前置）

下列范围及数据行在实施T053时逐字登记，T054接正式入口；当前清单/脚本尚未支持此新范围，本轮没有执行。共享的现有PD/N/P/G继续用原ID，CS为专项场景ID，不新增业务需求。各CS中多个数据行逐项独立记录，不以参数化方法名代替。不能由运行发现自动生成期望。

| 固定ID/必需数据行 | 保护义务及失败判据 | 承接/证据 |
| --- | --- | --- |
| CS01-entry/init；CS01-entry/clamp-reset | 正式初始化、启动/夹紧及复位真实信号；传输成功非动作完成 | T022/023/027/033/039/058；正式DI解析与独立PLC TCP |
| CS02-heartbeat/current；CS02-heartbeat/expired | 心跳/轮询/新鲜度、失联阻断及原deadline不刷新 | T022/043/057/058/059；双通道实际TCP/时间 |
| CS03-motion/matched；CS03-motion/stale；CS03-motion/wrong-target；CS03-motion/disconnected | 当前run/action/epoch、实际XYZ/适用Z；旧反馈/错目标/下发断线不能完成或重发 | T023/025/033/034/057—059；真实TCP、语义断言 |
| CS04-region/A；CS04-region/B-capacity；CS04-region/B-no-capacity | A夹紧后公共3D/F前运行容量；B F唯一后实际输入及显示读回，两阶段不合并 | T023/037/058；正式端口+TCP，不要求完整F扫码/预算业务链 |
| CS05-acquisition/open-release；CS05-acquisition/reset-timeout | 正式检测开放/释放及Z复位握手；复位不成不得新动作 | T024/035/057—059；TCP、直接语义，不要求真实相机/Worker |
| CS06-flip/matched；CS06-flip/wrong-face；CS06-flip/ack-timeout | 实际面/本轮关联、完整内部握手；未知不进入下一动作 | T024/034/035/057—059；实际协议/语义，不假面 |
| CS07-pick/valid；CS07-pick/rolled-back；CS07-pick/committed-invalid；CS07-pick/unknown | 物理取料事实与业务提交/回执分离；仅valid批准放料，其余保持占用、零放料/重发 | T025/036/041/058/059；真实TCP和SQLite，提交前失败与commit后扣回执分别真实举证 |
| CS07-raw/rolled-back；CS07-raw/committed-invalid；CS07-raw/unknown | 已观察取料但必要raw无有效回执不能进入正常CommitPick成功链；不假InTransit/引用 | T019/025/036/041/043/059；实际原始批次/存储事实、FailureNotice与UnknownHeld |
| CS08-unload/known；CS08-unlock/known；CS08-unlock/unconfirmed | 下料/解锁真实反馈、安全及未知准入保持，不把受理当完成 | T025/033/034/058/059；真实TCP及直接语义 |
| CS09-binding/applied；CS09-binding/healthy-capacity；CS09-binding/healthy-no-capacity；CS09-binding/cancel-pending；CS09-binding/cancel-inflight；CS09-binding/late | 正式通信消费冻结语义总截止，健康I/O/心跳但条件不成仍有限结束；失效后新派发/成功资格0；已发I/O保留真实事实，原后段期限不刷新 | T024/037/038/057—059；原Test10000ms及真实TCP，不验完整BA配置/三入口业务总功能 |
| CS10-diagnostics/success；CS10-diagnostics/unknown；CS10-diagnostics/timeout；CS10-diagnostics/missing | 实际raw身份/摘要/引用保存与只读重开可查，失败/部分响应不算成功；缺失历史不补raw/来源，原文不改 | T019/042/043/047/057/059；实际TCP+SQLite重开，不要求历史API/升级SU |
| PD-N01—N06的8行、PD-P01—P04的4行 | V01.1原完整12行；正式StartAsync及同实例RequestStartAsync，E/W全写范围，具体拒绝原因、受阻写0及合法继续 | T018实现/T021主要案例/T028复用/T053固定行/T057执行；不能测试自抛或不触发动作取零写 |
| V02/V03/V04全部实际N/P/G、脚本case数据行及正式扫描 | 地址/改名raw/枚举强转/位/偏移/握手/诊断/业务测试契约/混合脚本/解析与分类；合法语义通过；固定账本漏跑拒绝 | T014—016/T048—053/T056；原ID保留，实际逐行清单先审阅不按当次发现生成 |
| M01；M02；M03；M04；M05-normal；M05-readback-timeout；MC01；MC02 | V06已有具体Test变体；4/4及M05两行、双端同错独立预期检出与冻结修改拒绝 | T061基线→T062冻结→T063—068，逐变体TCP/同语义/必要DB/变更差异 |
| 迁移矩阵T01—T53中活动方法/数据项 | T051逐段展开并登记新ID；只有通信/直接语义义务活动，转出业务义务不删除或算Passed | T048—051/T053/T057，原登记及当前承接差异可复核 |

CS01—CS10共36条固定数据行，加PD12；N/P/G及实际活动迁移方法数由T051展开后固化版本，不以未展开数量声称齐全。T051/T053未给出完整静态逐方法清单前不得报告账本完成。

定义非法拒绝只证明结构/宽度/方向职责有效性；合法变体证明协议隔离和可执行性；MC01证明独立依据可检出结构合法的双端共同错误，三者不得互相代替。独立expected从确认原件及V06显式Test变化人工登记/复核，不导出生产map/codec或VirtualPlc定义。

专项完整结项：T055受影响构建/正式装配，T056—059全部活动固定行与所需真实证据通过形成T061；其后冻结/变体/负控制全部通过到T069。完整BA/SU/L/页面/Worker/相机/媒体不是前置。新009完成只表示通信专项通过，所有转出功能未验事项仍未验。

专项动作若既有语义准入要求保存凭据，必须按原义务取得真实数据库提交并核回执；不以“局部不启动相机/算法”制造已采集/已算法成功、假保存或伪执行来源。纯通信窗口/握手核验只声称窗口/握手，不声称产品检测完成。

## BM00 当前有限必需集合（规范性，009-boundary-minimum/1）

| 集合 | 事先固定来源及精确范围 | 当前证明/限制 |
| --- | --- | --- |
| C#架构 | ProtocolRepositoryBoundaryTests四Fact；DependencyRulesTests两Fact；ParserAvailabilityTests.PinnedRoslynParsesAndBindsRealSymbols；ProtocolBoundaryTests现有21负例、13合法例及N09合同Fact；新增N18受保护路径分类负例7数据行、P09合法诊断Fact（合计50行，登记不得漏数据行） | 正式依赖/公共类型/raw判断/强转/位/布局/握手/诊断/业务断言/旁路；合法业务数字/载荷/诊断引用不误报 |
| 脚本 | 009-script-boundary-cases.json的38固定行及N15两分类、N16两解析器行；SCRIPT-SOURCE-JS/PY/PS、SCRIPT-INVENTORY | 三语言AST、helper和双向分类，42例实际执行；不启动页面/整机 |
| 账本 | 原G01—G07固定ID，test_verify.py --ledger-selfcheck | 同生产固定清单校验器拒绝缺失/过滤/Skip/零发现/旧报告/缺证；合法完整集合通过 |
| 直接接口 | StagePortContractTests七Fact；PlcStageActionPortContractTests七数据行；DeviceMessageContractTests一Fact；RecipeApplicationContractTests.RunCancellationClosesRegisteredBindingTokenWithoutTurningPauseIntoCancellation、BoundReceiptCannotAuthorizeBindingWithoutActualRequiredSave、FailedIntentNeverRegistersTotalWindowOrCreatesBound、LateOrCancelledBoundReturnDoesNotCreateHandoff两行 | 当前稳定语义、身份/实际目标/新鲜度/来源/未知不重发、必要引用及取消/无有效保存不授权；组件证据，不推定物理或数据库事实 |
| 人工及构建 | 当前真实Host同实例注册/StageAction CommitPick回调、公共端口、IntegratedDetection/配方消费者、诊断合法路径及业务测试归属；backend/VirtualPlc必要依赖 | 零旁路及能编译，未改行为不全跑；若改保存门禁追加受控真实SQLite对应最小验证 |

当前固定集合按已审阅的workflow/009-boundary-minimum-cases.json完整发现和执行；新增两项已有架构用例，绑定测试按当前必要保存语义对应方法，不删除取消、期限或无回执保护。清单项数由当前文件计算，不沿用历史125项。N18先真实暴露7个漏检；正式扫描与fixture使用同一ProtectedRole和检查器，Application/Domain/Host/配方根及明确直接消费者不能通过改分类绕过保护；唯一Host原始诊断路由保持合法只读职责。C#正式报告记录注册/实际角色和逐文件摘要/runId，与当前源交叉核对，不能用标签或哈希代替内容判断。

精确case/method/dataRow以T053独立固定清单核对；允许按实际已审阅源纠正本表数量，不能以发现结果自动删行。现有旧通信报文测试保留不删除，完整PD12/CS36/M/MC仍未本轮运行，不计Passed。

### 010实施定向对齐 A09（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A09**：所有验收profile无条件L，统一verify/runner、auto-dev/step及009 boundary_minimum/protocol_isolation旁接共用执行/凭证核验。_run_verify passed、verify_entry退出、assess/finish及旁接ledger/result/subsetPassed最终点拒漏跑/Skip/旧身份/解析失败/伪Passed。L含职责闭包B/N/P、G/C、受影响009静态，不开Host/PLC/Worker/DB、不递归完整验收。010按B→冻结→E/S→T，009动态范围及SelectedCasesOnly overall009Passed=false保持；D10先迁活动映射，历史证据不改。
  生产/消费与010实施承接：Rules/manifest/migration→verify及workflow/旁接aggregator→最终凭证/结论；T033—T042；009活动JSON映射属于T033，历史快照/报告只读。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

## 011受影响验证承接（2026-10-03）

016定向收口：G02/G07的migration admission只验证同一实际判定器的受控FixtureOnly输入，正例先通过，负例核对具体拒绝原因；不能用整套历史登记失配替代负例证明，也不能用受控模拟执行行证明产品或迁移完成。runner真实migration-audit入口及初始登记完整性保持。016直接/传递影响的当前有效迁移义务仍核对实际源码、断言与运行证据；其他历史失配保留原义务，并依据复制基线、本轮变化及原验收范围评估，不自动转出有效活动保护。

保留009架构扫描的有效正例/负例与协议隔离保护，按改动触达的边界复用；面数1/2/4例是合法正例，不是枚举上限，新更多面/额外E的配置不得触发测试专权。旧PD/36CS/M/MC全套及历史运行集合不作为011新门槛。业务断言只验证动作语义、关联/保存/取消/期限；通信测试验证新报文与原码。弃用断言迁移后删除无用途旧用例，保留原失败证据，零发现/跳过不算通过。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

本轮仅确认需求同步，不生成新设计或任务；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。

后续只并入既定特殊/普通代表或必要组件：普通OK无多余分拣搬运；至少两件特殊OK实际返回各自原槽、保OK编号顺序、实际安全位后才下一件；原槽回放失败不记录完成/不推进。保存重读/冻结保原始关联，界面任意OK目标选择为0。不同件/区域同号和重新编号不串值，无同义新完整链或全量故障组合。
错误旧任意OK目标期望须纠正而非删除正确保护；失败/Skip/漏行/假反馈不得通过，009/010/013现行保护和验证门槛保持。本轮未执行上述验证。
