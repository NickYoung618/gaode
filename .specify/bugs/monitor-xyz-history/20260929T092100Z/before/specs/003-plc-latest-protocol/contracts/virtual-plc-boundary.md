# VirtualPlc 边界合同

## 2026-09-27 当前公开名称与历史适用范围（I1同步）

当前公开名称为 `4x0001 = XY_Move_Cmd`、`4x0002 = XY_Pos_Confirmed`，依据monitor-xyz-history本轮最新用户确认及来源Word，取代此前XYZ命名确认。两个点位均为Int16，方向分别PC→PLC及PLC→PC；地址、PDU偏移、类型、命令值、握手与清零语义均不变。内部XyMoveCmd等数值常量不要求改名。
来源Word仍保持原内容及SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`，不宣称原件已更新。带日期的旧名称、旧实现快照及旧运行结论仅适用于原构建；当前规则优先，历史证据不得机械替换。
下料命令4及Grab_Target_Z、本次实际XYZ核验保留；普通OK盘末留原位、不发取放；仅适用NG/Pending同盘取放，特殊配方必要搬运及回原位不取消。协议§3.1.5/6/7的清零顺序不变。


## 复用与连接

复用仓库既有 `VirtualPlc` 独立程序。Host 必须以独立进程通过正式 Modbus TCP、相同地址/方向、完成判据和 `LatestProtocolPlcDevice` 接入；进程内 SimulatedPlc 或源码盘点只可用于单元/合同测试，不能替代本期 E2E。

只有盘点确认的正式协议缺口允许最小修改 VirtualPlc；不得新建模拟器、协议或业务服务。

## 权限边界

VirtualPlc 可提供 001 所需心跳、启动夹紧、运动事实，以及整托阶段的 Sorting、UnloadPreparation、UnlockObservation 点位事实和故障注入。它不得：

- 执行 Detection、算法映射或业务编排；
- 创建 handoff、WholeTrayCompletion、人工确认或 FinalUnloadCompletion；
- 直接写业务数据库或调用 Host 内部应用服务；
- 自动解锁、硬编码成功或替 Host 重发未知动作。

`operationId` 和 `connectionEpoch` 由 Host/适配器关联，不是新增 PLC 点位。每次结果必须传播 `source=Virtual`、quality、errorCode 和用途，只证明软件协议闭环，不证明真实 PLC/生产验收。

## 强制点位行为

- `PC_Start_Cmd` 后按测试配置产生夹紧成功、失败或保持；启动路径写 `Pallet_Lock_Cmd=1` 必须拒绝。

2026-09-24 T063最小澄清：VirtualPlc的Modbus写入受理与独立扫描之间必须保存一次 `PC_Start_Cmd` 上升沿待采样事实；同一命令即使在下一次扫描前已清零，也应在扫描时按当时安全、自动、就绪与动作门禁判定一次，不得直接制造 `Pallet_Lock_Status=1`。拒绝、受理及实际反馈分别记录；重复高电平不重复受理。该虚拟设备内部锁存不改变Host的正式写入回执/物理夹紧区分，不推定真实PLC也使用相同扫描实现。
- Sorting、`XY_Move_Cmd=4`/`XY_Pos_Confirmed=1` 和解锁 Cmd=0/Status=0 遵守正式协议。
- Host 未核验已提交 WholeTrayCompletion 时不得接受 Cmd=0。
- 写后断联、状态保持、读回超时或 epoch 变化必须可观察；Host 将可能已派发的动作投影为 UnknownHeld 且不自动重发。

2026-09-26 Test 故障注入增量：`FlipAckHold` 仅为 VirtualPlc 本地 `/api/simulator/faults/{fault}` 的受控测试选项。它在真实模拟翻面已报告状态2后，保持 `Flip_Status=2`，即使 PC 写入 `Flip_OK=1` 也不提供状态0确认；由 Host 原有动作/阶段期限判定超时。该选项不添加 PLC 寄存器、不改变正式超时、不制造第二面或 Final。故障证据见 008 当前批次记录。

## 检测与运动清零时序（协议 §3.1.7）

产品检测位置采用协议§2.2寄存器表的 `XY_Move_Cmd=2`（1为上料位）；没有合法冻结目标时Host/VirtualPlc不得模拟受理产品动作。

上位机仅在本轮 `XY_Pos_Confirmed(4x0002)=1`、`Z_Axis_Move_Status=2` 且坐标匹配后置 `Inspection_Status(4x0052)=1`。检测期间清 `XY_Move_Cmd(4x0001)=0` 只复位上位机命令，PLC 保持 `4x0002=1` 的本轮到位事实；这正是003既有 FR04 所要求、此前 VirtualPlc 行为偏离之处。结果记录后上位机置 `4x0052=2`，PLC反馈 `Z_Reset_Status(4x0053)=1→2`；只有读到 2 后，上位机才可清 `4x0052=0` 并发下一运动。PLC 实际受理下一条合法 `4x0001` 时，才将上一轮到位及复位反馈清零并开始新一轮；清旧命令、轮询快照或等待时间都不能提前制造新轮事实。0 是有定义的点位值，不表示“空”或“未提供”。不新增 PLC 点位。

## VirtualPlc 本地监控诊断

`GET /api/simulator/changes?after=<sequence>` 是 VirtualPlc 自身的本地只读诊断查询，不是 Host 业务 API。响应包含 `oldestSequence/latestSequence/gap/changes`；每条实际变化包含递增 `sequence`、时间、点位身份、前值/后值和写入方，按实际写入顺序供页面显示。`GET /api/simulator/state` 仍给当前快照，快照轮询不能替代中间写入事件。页面的“最近变化”排除 `PLC_Heartbeat_Req` 与 `PC_Heartbeat_Resp`，但不删除其实际信号或当前状态。

心跳延迟诊断仅在本地Host/VirtualPlc日志增加有界的事务与时间分段，不增加PLC点位、公开控制接口或模拟成功反馈。VirtualPlc仍按原3秒有效应答期限报警；慢成功与超时均不得被日志本身或测试夹具解释为物理动作完成。

当前实现只在进程内保留最多8192条变化，查询默认每批最多1024条；游标早于最老保留序号时必须返回 `gap=true`，页面提示缺口。页面自身也只保留有限近期显示记录。不得称此接口无限期保留全部事件，或把本地诊断当作 PLC 协议/Host 完成依据。历史实现快照（2026-09-23）：当时监控把部分实际0值渲染成“空”；现已纠正，当前子范围见[动作诊断证据](../evidence/008-action-diagnostics.md)，不据此勾选T062整项。

## 独立进程证据

历史约定（已由2026-09-26新版§3.1.6及本页当前适用条款接续，不用于当前构建）：2026-09-24下料增量：命令4按本次写入的`Camera_Target_X/Y/Z`受理，只驱动X/Y；此前`Z_Reset_Status=2`须是真实复位过程的反馈，复位后的实际Z位置来自虚拟设备配置的Test安全位置。命令4不制造新Z运动/`Z_Axis_Move_Status=2`，不写PLC→PC当前位置反馈作为目标；受理新命令时可清旧XY到位位，完成时更新X/Y与`XY_Pos_Confirmed=1`，保留真实Z位置。相同目标及无法区分的新旧反馈仍按Host未知门禁处理，不为测试伪造完成。来源：协议§2.2/§3.1.7点位与复位顺序，2026-09-24用户项目约定的命令4行为。

五类 E2E 的每个场景均记录六类证据：Host/VirtualPlc 进程与原始日志、API transcript、Modbus audit、SQLite 事件/投影、包含六组件的 source matrix、final result。`manifest.json` 只作索引和哈希，不替代来源矩阵或最终结果。未实际执行的场景标为 Blocked/NotRun；不得用“源码存在”或历史公共准备 passed 记录宣称完成。

## 2026-09-24 008完整执行合同增量

本节原制定依据为宪章5.0.0；当前适用宪章6.0.0、008最新澄清及本轮F协议决定；用于008的当前设计，前文冲突范围仅作历史记录。全部增量尚待实现/验证，历史完成与失败证据不改写。

VirtualPlc沿正式Modbus实现已授权的F周期：受理命令5时清旧反馈，公共XY+Scan_Target_Z定位，整个周期Z反馈归扫码轴；PC到位后写3并清命令，写4才触发本轮一次复位，反馈1→2/失败3，PC可靠读回2后清0。PLC产生实际模拟反馈，Host不写反馈位。F失败可完成安全复位但不能进入产品路线。

该定义来自本轮最小更新的指定协议§3.1.7，待实现/验证，不能拿旧1/2证据替代。E仍待B01-E；旋转、源目标取放/占用等未知信号依B02/03，不通过虚拟地址补造。现有Move/Flip/Sort/ZReset原语不等于完整业务链。

实现阶段与验收统一见[008方案](../../008-recipe-driven-inspection/plan.md)、[执行合同](../../008-recipe-driven-inspection/contracts/execution.md)、[接口合同](../../008-recipe-driven-inspection/contracts/api-results.md)和[证据合同](../../008-recipe-driven-inspection/contracts/evidence.md)。当前tasks已按S0—S5对齐，实施须按其具体前置；旧analysis不作本次依据。
第四批组件验证可用显式`Test/InjectedXYZ`目标通过现有Host/VirtualPlc的Detection命令2与1/2复位状态机；仅验证组件实际调用和关联反馈。该输入不属于Q01/Q02已批准配方点位，不解除正式配方的点位、高度映射及公共移交准入。

## USR-E动作级诊断增量

仅在现有PlcDataStore、VirtualPlcEngine、Modbus处理及Program端点上扩展，不新增日志平台/生产PLC信号。目标合同为`virtual-plc-write-audit/2.0`：复用GET `/api/simulator/audit`，保留writes并增加actions及有限缓存的oldestSequence/latestSequence/gap说明；原state/changes/address-map保留。实施时生产者、采证消费者同批升级，不假称当前1.0已提供这些字段。

- writes每次实际受理都记录sequence/time、connection/transaction、function、startAddress/count、完整rawWords、byteOrder及写回执；同值不省略。批量包可用一个数组，不要求逐轴网络请求。
- actions记录设备本机actionSequence、generation、command、accepted/rejected/targetReached/completed/failed、对应writeSequenceRefs、该动作锁存完整目标XYZ/axisRole、设备模拟实际XYZ采样/阶段/时间及握手状态。本机序号不是新增Modbus寄存器；未取得Host run/action时不在设备伪造身份。
- Host按唯一在途动作、连接/事务、请求及受理/反馈时序关联本地actionSequence与run/operation/action/step/entity/face（存在时）；关联有缺口则标Uncorrelated，不把旧缓存改绑当前动作。串行控制不等于可以忽略旧反馈。
- Host关键事件区分TargetRequested、WriteAcknowledged、DeviceObserved、PositionValidated、AckCleared/Blocked；完整目标/实际XYZ、Z轴、原始双字/映射版本、容差、比较结果、来源/质量、采样时间、协议/配置摘要按动作持久保存。实际值只能来自读回；无值留未知，不以目标补齐。
- 复用TraceWriter/StageEventStore、既有日志索引/查询与证据manifest；设备缓存由采证脚本导出到新包，Host判定/完成事实按原保存门禁提交。缓存溢出明确gap，不将缺记录判作未读写。不永久归档高频全量轮询；只保留动作受理、关键反馈/判定、清零及失败所需读取的完整值。

历史实现快照（USR-E设计前）：changes在相同值时省略；当时PcWriteAudit只保留首ushort、有限缓存，不能独自证明Float32完整XYZ或每次读取。后续验证需同时对齐完整Modbus包、设备锁存、实际反馈和Host判断，区分漏写/漏读、批量展示、同值省略、旧运行包/Provider、反馈/校验缺漏。目标快照不是实际反馈，设备诊断不是业务Final。取放实际坐标执行与阶段限制遵守[动作合同](plc-stage-action-port.md#usr-e完整坐标与动作观测)。

## 当前监控错误分类（I3，既有FR14/T062）

状态HTTP请求失败只表示监控读取失败，不能宣称PLC断联或恢复。当前SimulatorSnapshot仅提供timestamp及communicationTimedOut（设备心跳超时事实），没有TCP Connected字段：true显示PLC通信失效/心跳超时，false显示未报告心跳超时，不等同TCP已连接；字段缺失明确通信状态未提供，不造连接字段。
状态解码/页面渲染、变化记录或动作审计失败分列显示具体错误；不修改后端通信事实，不把错误吞掉。保留最近一次有效快照及其后端时间；读取失败标为旧状态，显示失败保留最近成功显示的时间，不称本次显示成功。恢复须由新的真实读取及成功渲染分别清除各自错误；审计未成功前不得被非审计轮询清除。完整XYZ、方向、同值及缺失不补造规则不变。
当前audit/2.0已经保存完整rawWords、动作引用及实际反馈，不能再将历史“只存首ushort”解释成当前能力。仍为有界本地诊断，不能代替Host业务Final。证据与版本见[动作诊断证据](../evidence/008-action-diagnostics.md)。

## 2026-09-27 monitor-xyz-history 当前纠正（取代此前监控布局与XYZ公开名称要求）
本轮用户确认：4x0001公开名称为XY_Move_Cmd，4x0002为XY_Pos_Confirmed，与来源Word一致。此前2026-09-27 XYZ名称确认仅保留历史适用性，不再约束当前构建；来源Word内容和摘要不变，内部Xy常量不重构。
删除独立完整XYZ栏目；原“最近数值变化”列表逐信号展示每个Move/Sort的实际XYZ发送及对应动作实际反馈，同值、0及连续全同目标均保留。发送来自本次同连接命令前的audit写入收据，反馈来自动作actual，保留动作/阶段/序号、UTC时间、方向及Z用途。禁止目标冒充反馈、快照补历史、旧命令轴拼接。其他状态/握手/清零changes保留；坐标使用audit唯一来源，Z复位从ZReset动作实际采样显示Z，避免重复。特殊Test HTTP动作逐轴显示真实请求/反馈，标注非Modbus点位、时间未提供不造时间。
复用audit/2.0与changes，不变更共享API字段或PLC业务；页面合并有界记录、去重、缺口提示及清空游标，不因同一记录再次轮询而刷屏。缺轴明确缺失；HTTP/页面渲染/心跳状态错误保持分类。删除motionList/motionStatus及相关版本依赖，使用原historyList的版本标记。
普通OK原位、NG/Pending同盘取放、特殊配方必要搬运、下料命令4及安全门禁保持。验证引用本缺陷带时间戳assessment/fix/test；旧29动作/87轴证据与r3—r7包仅按原范围追溯，新显示重放不冒称新业务实跑。008整体验收不由此关闭。
