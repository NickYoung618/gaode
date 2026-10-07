# 007 固定图片与独立虚拟算法集成合同

2026-09-26 Test成员结果增量：受控worker清单可选`dispositionByTarget`数组，按实际输入`objectId`后缀、`localFace`及可选`camera`匹配`OK/NG/Pending`。输入媒体仍实际读回并校验摘要，独立进程仍执行既定10秒；融合输入必须同对象/面，Host不注入结果。仅用于验证U01/U02的NG优先及保留Pending明细，生产算法不受该字段影响。

**状态**：007设计约定；共享003/001接口变更需先同步其spec/contracts/plan/tasks。  
**用途**：限定本阶段正式端口接线和可核验证据，不重定义PLC点位或生产算法。

## 组合与准入

- 唯一Host在`VirtualPlcIntegration`中显式选择真实Modbus VirtualPlc、固定图片采集适配、既有模拟光源、独立虚拟算法进程和正式Detection编排；不得在某组件失败时隐式切回`SimulatedCapture`、`SimulatedAlgorithm`或直接返回OK的`SimulatedDetectionPort`。
- 各适配器只处理自身职责：VirtualPlc根据协议产生物理模拟反馈；文件采集只通过`ICapturePort`交付媒体；worker只处理算法请求；配方与SQLite保存由Host既有逻辑负责。worker、页面、桌面宿主及PowerShell均不得直控PLC或写业务库。
- 运行配置须冻结`purpose=Test`、文件清单及SHA-256、预算id/version、模拟配方/计划版本、随机种子/范围、worker版本和来源。真实设备绑定拒绝测试配方及图片参数。

## 固定图片采集

| 入站 | 行为 | 出站与失败 |
| --- | --- | --- |
| 正式CaptureRequest：run/operation/capture/attempt、role、计划步骤/对象、配置快照和最大字节数 | 按唯一固定目录和冻结清单选择指定文件；每次实际触发读取并模拟3–5秒拍照/读取；记录触发、读开始/结束与真实墙钟耗时 | 原有Accepted/Capturing/Ended/MediaTaken事件、原字节与格式、输入摘要；经MediaStore形成新的mediaId与提交引用。缺失/超限/不可读返回失败，不生成假媒体 |

3D/F公共准备及检测计划所需采集都走正式入口。F仍是一次触发单图，不能为凑预算重拍。3D普通固定图片只作为标记来源的模拟输入，Z结果也只是按合法Test参数计算/返回；不冒充真实3D点云。Light沿已有模拟端口，留来源及动作证据。媒体内容和元数据落地后才能登记算法意图，worker输入仅受控媒体引用或受控租约，不接收任意磁盘路径。

## 独立算法进程

| 入站 | worker必须实际执行 | 出站与失败 |
| --- | --- | --- |
| 正式AlgorithmRequest/DetectionRequest派生的run/call/operation/attempt、role、受控mediaRef、期限和配置版本 | 受控进程从MediaStore租约读取输入，校验媒体可访问及摘要；按Height、FDecode、Detection角色执行10秒真实墙钟模拟计算；由冻结种子和合法值域生成结果 | 发送带原call/attempt/workerSessionId的Result或明确错误，Host保存收发/时间/进程/输出/来源及算法终态；迟到/错关联结果不得写入其他调用 |

复用`WorkerProcessSupervisor`与`station01-worker/1.0` NDJSON信封：Host启动唯一子进程并执行Hello/Ready健康握手，按受控相对`inputKey`派发Execute，worker回Accepted/Result/InputReleased。Result新增`resultJson`承载Height样本、F原码或Detection处置，保留相同callId/attempt/workerSessionId/role/leaseId；错误时携`errorCode`且不得给默认成功。Host只把匹配原调用的结果送入正式算法端口，媒体由Host的MediaStore及TraceWriter保存，worker仅在受控`media-root`内读取输入并记录协议时间日志。worker未运行、超时、无结果或错误关联按现有有限终态处理；Detection可产生带原失败的Pending，F无法合法识别则锁停不绑定配方。

随机数只改变合法模拟结果域，例如Height的测试Z值及Detection的合法OK/NG分类；正常F样本固定返回真实调用计算出的唯一合法测试配方码`RC:R-S1-A-CAP:0.4.0-review`，不能随机到不存在的配方并再用预设成功覆盖。所有调用记录种子、算法模拟版本和原始结果；不得随机伪造runId、对象/位置、PLC反馈、保存成功、解锁或取盘。

## Detection逐项执行与阶段期限

003的`IDetectionPort`仍是Host正式阶段边界，不能用一次直接构造全部ExpectedObjects的OK结果。适配器根据冻结`RecipeRunPlan`对当前完整测试样本逐必检步骤发起采集；每个检测采集步骤对应一个真实派发到独立worker的算法请求，输出原对象身份/位置、质量/处置、媒体/结果引用及来源，再由003原编排触发Sorting等后续动作。实际请求数A写入预算计算和证据。不同对象不能串用结果或用同一预制URI代替采集。

Detection从阶段开始共用120秒，`5C+10A+T+R<120秒`为正常样本的保守预算门槛；各公共准备调用也使用新版本化Test窗口。算法超时最多3次、退避2/5秒，临时通信最多4次、退避1/2/4秒；实际期限先到即收敛，不重置阶段。正常样本候选CAP/P01预计C=2、A=2，仅模拟延迟上界30秒，仍须留PLC/保存/调度余量并核对实际计划。不能把默认BASE/15槽或CAP/15槽当成120秒兼容样本。

## 设备、持久化与源矩阵

VirtualPlc仍通过正式Modbus反馈夹紧、定位、检查握手、分拣、下料和解锁；Host只接受匹配当前连接代次的真实模拟反馈，不发送`Pallet_Lock_Cmd=1`冒充内部夹紧，不在整盘提交前解锁。动作用003意图/反馈双短事务；未知物理动作保留UnknownHeld且不盲重发。媒体和算法证据与同runId的SQLite事件/配方快照关联；源矩阵包含Host/PLC/Camera/Light/Algorithm/ManualActor且各自来源不压扁成单值。最终证据只可为SoftwareLoopOnly。

## 按runId媒体查询（006消费依赖）

复用003 `station01-main-flow-api.md` 的单一路由`GET /api/v1/station01/runs/{runId}/media`。公共准备身份取已提交采集意图和Media写入；Detection身份取同captureId、同runId、同冻结planRevision的已提交采集事件，事件必须写入`stepSequence`与业务`camera`。查询不以时间、文件名、数组索引补身份；旧事件缺字段时返回未映射，不把A/B写进历史。`MediaRead`仍控制字节读取，清单仍须`Read`授权和runId准确筛选。

## VirtualPlc 检测清零与本地诊断边界（2026-09-23）

下料使用本次用途的合法配置及可靠轴反馈。普通盘末Detection→适用Sorting及必要保存→UnloadPreparation→允许取盘/人工取盘→Final；到位、允许取盘、人工确认和保存是独立事实。不能伪造新协议未定义的旧锁紧/解锁信号，地址/恢复/安全延期不猜值。

以最新版《PLC与上位机通信接口协议》§3.1.7及003 `virtual-plc-boundary.md`为准：检测期间上位机清旧`XY_Move_Cmd(4x0001)=0`，PLC保留`XY_Pos_Confirmed(4x0002)=1`的本轮到位事实；上位机提交`Inspection_Status(4x0052)=2`后等待`Z_Reset_Status(4x0053)=2`，才将`4x0052`清0；下一合法运动实际受理时PLC开始新一轮反馈。Host仍须匹配本轮坐标和连接代次，并遵守安全与未知动作门禁，不把旧1当作新到位。specs/003-plc-latest-protocol T061 已修正 Host 下一运动门禁及旧协议测试断言，正式 Modbus 3/3 定向测试证据见 `artifacts/plc-latest/t061-20260923-223458/manifest.json`；该证据满足 T015 的此项前置，不等于 T015 完整闭环通过。

`GET /api/simulator/changes?after=`及本地监控只提供VirtualPlc诊断：事件按实际写入序号排序，最近变化排除心跳，有限缓存超出时提示缺口；实际数值0必须显示为0，不解释为空值。监控源码已纠正0值显示，接口与页面逻辑已定向验证，实际页面操作证据仍缺，specs/003-plc-latest-protocol T062 保持未完成；该诊断接口不供Host业务控制，不新增PLC点位或007阶段完成门禁。

`station01-heartbeat-response-delay` 只增量采集既有正式Modbus心跳的事务时间线与Host/VirtualPlc进程调度证据；007采集、worker、SQLite、页面消费者不获得新的PLC控制权。原心跳期限、状态过期、安全门禁和Test/生产来源隔离均不变，实际页面闭环须与独立的协议和数据库证据交叉核对。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0及008最新澄清；用于新完整路线，旧CAP/P01逐图/120秒和外部自动取盘仅保留原样本范围。新增能力未实现，旧证据不扩用。

- Test环境沿同一正式Host/Modbus/独立VirtualPlc、固定图采集、独立worker、SQLite和媒体，夹具清单选择配置和合法槽位；业务由正式006页面选配方及启动。
- F使用新指定协议§3.1.7：命令5/Scan_Target_Z、到位3清命令、采集解码结束保存后4、本轮扫码Z复位成功后0；4不等于识别成功。虚拟设备自己产生定位/复位反馈，失败不由Host改成功。E及特殊取放/旋转未定义合同仍按B表限制。
- 单图分析完成保存后复位，前批不等后批；同对象/面/轮次双输入齐后额外融合，station01-worker/2.0由两端同步，实际读取两份媒体及摘要，正常A=C+F。模拟输出需来源与输入可追溯，不代替真实缺陷精度。
- Q01 Test清单按`CaptureRequest.CameraBindingId`区分A/B可读图片；旧不含camera的公共3D/F清单仍按角色读取。双输入`AlgorithmRequest.InputIdentities`逐媒体提供A/B或C/D身份，不能以单个TargetIdentity覆盖两个输入；无合法产品目标时只可做组件验证。
- 保持每次采集3—5秒/worker10秒，预算包含公共初始3D/F、翻面定位/执行/ACK、分拣取放/ACK、适用E、有限重试及保存；不降低3秒心跳保护。媒体用真实对象/面/相机/轮次身份，不能重复旧身份冒充新增采集。
- 适用换面、取盘和恢复经正式前端控件，辅助工具只提供合法Test物理准备、注入已定义故障及只读采证；无页面操作不能填写该项E2E通过。
- 每run记录配置/程序/配方版本、请求及提交关联和各组件来源，支持22完整序列及业务差异复用，不做交叉穷举。specs/007-station01-integrated-loop T015/T024及心跳partial状态不变。

共同字段及行为以[008接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)为准。

## 2026-09-26协议身份及适配范围

逐对象按配置取件点定位→PLC按产品型号/目标面翻转→配置放回点定位→放回；相关对象放回后统一3D姿态复查，正常继续、异常原槽退出并返回物理槽号，F不重绑。料盘不翻面，整体不按部位重复动作，不沿用旧Flip_OK或独立实际面号前置。

同盘分拣：OK留原槽不搬，NG/Pending各去对应区配置目标，姿态异常原槽退出；源XY→抓取Z下降→取料→抬升→目标XY→下降→放料→再抬升。真实取料反馈和在途保存先于放料；物理槽号不是Sequence，预留/在途持续到可靠完成。新状态1/2/3只由通信翻译为取料成功/放料完成/失败，旧状态2/3及ACK不再适用。

当前通信依据为20261001 Word与信号表，哈希及延期差异见011 spec；旧协议身份/地址/ACK仅属历史原件和旧运行，实施须迁移映射、适配器、VirtualPlc与通信断言，业务不感知原码。

## 2026-09-27 当前公开名称与历史适用范围（I1同步）

当前公开名称为 `4x0001 = XY_Move_Cmd`、`4x0002 = XY_Pos_Confirmed`，依据monitor-xyz-history本轮最新用户确认及来源Word，取代此前XYZ命名确认。两个点位均为Int16，方向分别PC→PLC及PLC→PC；地址、PDU偏移、类型、命令值、握手与清零语义均不变。内部XyMoveCmd等数值常量不要求改名。
来源Word仍保持原内容及SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`，不宣称原件已更新。带日期的旧名称、旧实现快照及旧运行结论仅适用于原构建；当前规则优先，历史证据不得机械替换。
下料命令4及Grab_Target_Z、本次实际XYZ核验保留；普通OK盘末留原位、不发取放；仅适用NG/Pending同盘取放，特殊配方必要搬运及回原位不取消。协议§3.1.5/6/7的清零顺序不变。

历史“代码当前仍旧身份/实际页面证据仍缺”等句为当时快照；本轮实际范围沿[003证据](../../003-plc-latest-protocol/evidence/008-action-diagnostics.md)及[008最新收口入口](../../008-recipe-driven-inspection/evidence/completion-review.md)接续，不扩大007父任务验收。

## 009 / AL05 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

沿现有verify.ps1→workflow/verify_entry.py→runner.py，不调用auto-dev。增加通信测试套件、固定必需清单与逐case/dataRow执行账本；C#/JS/Python/有限PowerShell正式扫描与同入口正负例均必需。缺清单、未发现、过滤、Skip、解析失败、旧报告或证据缺失非Passed；参数化方法名不能抵数据行。PD12、SU6、BA全部数据行固定登记，不从当次发现生成expected。

新版budget schema1.1必需recipeApplication，发布新版本Test10000ms并同步simulation/fixture/start脚本引用，不在旧1.1.0实例暗补字段。Production未批准拒绝。独立Host/VirtualPlc/实际Worker、文件相机/SQLite/媒体/API/页面证据沿原合同；健康心跳且所有I/O成功但B条件不成立须按有限总窗失败。组件看门狗高于业务总窗和有界收尾，仅防卡死，不改变业务预算。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A03（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
