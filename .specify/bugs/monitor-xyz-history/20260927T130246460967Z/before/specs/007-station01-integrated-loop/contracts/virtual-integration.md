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

新版下料按§3.1.6：写冻结目标Camera_Target_X/Y及Grab_Target_Z(0003/0005/000B)，再命令4；核验本次到位和实际XYZ后清命令。前次动作必要复位及安全条件保持，不再采用写检测Z或命令4只动XY约定。普通盘末Detection→UnloadPreparation→适用Sorting→WholeTrayCompletion→ObservedUnlocked→页面取盘确认→Final；到位不等于可取盘，适用分拣/必要保存/无未知在途门禁必须成立。

以最新版《PLC与上位机通信接口协议》§3.1.7及003 `virtual-plc-boundary.md`为准：检测期间上位机清旧`XYZ_Move_Cmd(4x0001)=0`，PLC保留`XYZ_Pos_Confirmed(4x0002)=1`的本轮到位事实；上位机提交`Inspection_Status(4x0052)=2`后等待`Z_Reset_Status(4x0053)=2`，才将`4x0052`清0；下一合法运动实际受理时PLC开始新一轮反馈。Host仍须匹配本轮坐标和连接代次，并遵守安全与未知动作门禁，不把旧1当作新到位。specs/003-plc-latest-protocol T061 已修正 Host 下一运动门禁及旧协议测试断言，正式 Modbus 3/3 定向测试证据见 `artifacts/plc-latest/t061-20260923-223458/manifest.json`；该证据满足 T015 的此项前置，不等于 T015 完整闭环通过。

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

当前自动翻面按20260925分区协议§3.1.5：当前零件XYZ写0003/0005/000B→命令3→本次到位/实际XYZ核验→清命令→目标面→状态2及实际面匹配→Flip_OK=1→状态0→Flip_OK=0；料盘不翻面。逐个处理仍需后续面的实体，整体不按部位重复；同目标面的不同实体分别闭环。普通翻面协议已定义，受限项仅为实际代码能力、合法点位配置或真机标定。

同盘Sorting先源XYZ(0003/0005/000B)、命令1，状态2只取料成功；清命令后提交真实源槽位Sorting_Part_Index及目标XYZ、命令2；状态3放料完成后清命令、Sorting_OK=1，观察状态0再清ACK并提交完成。4失败、5满盘、命令3满盘报警。Sequence不是槽号；普通OK无需搬运不发取放。源/目标均属当前盘，预留与在途保留至可靠完成。

协议目标身份`plc-upper-20260925-partitioned-ack`及原件SHA需贯穿映射、适配器、反馈快照、VirtualPlc监控、日志与完成来源矩阵。两个ACK分别0054/0055，PC→PLC Int16；预留从0056开始。代码当前仍旧身份，旧通过不迁移。

## 2026-09-27 当前公开名称与历史适用范围（I1同步）

当前公开名称为 `4x0001 = XYZ_Move_Cmd`、`4x0002 = XYZ_Pos_Confirmed`，依据003 spec的2026-09-27最新用户确认，覆盖旧XY名称。两个点位均为Int16，方向分别PC→PLC及PLC→PC；地址、PDU偏移、类型、命令值、握手与清零语义均不变。内部XyMoveCmd等数值常量不要求改名。
来源Word仍保持原内容及SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`，不宣称原件已更新。带日期的旧名称、旧实现快照及旧运行结论仅适用于原构建；当前规则优先，历史证据不得机械替换。
下料命令4及Grab_Target_Z、本次实际XYZ核验保留；普通OK盘末留原位、不发取放；仅适用NG/Pending同盘取放，特殊配方必要搬运及回原位不取消。协议§3.1.5/6/7的清零顺序不变。

历史“代码当前仍旧身份/实际页面证据仍缺”等句为当时快照；本轮实际范围沿[003证据](../../003-plc-latest-protocol/evidence/008-action-diagnostics.md)及[008最新收口入口](../../008-recipe-driven-inspection/evidence/completion-review.md)接续，不扩大007父任务验收。
