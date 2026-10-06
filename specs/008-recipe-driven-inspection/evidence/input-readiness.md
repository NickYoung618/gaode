# Q01 输入核对（008 T049 的 Q01 子范围）

2026-09-27 当前 r5 检查点：实际冻结源、目录、配置、媒体清单、程序与桌面身份以 `page-next-closure-20260926-night-r5/build-freeze.json`、`frozen-files.json`、各job manifest/ResourcesResolved为准。Q01-PAUSE、Q01-NG、Q02-PENDING正式WPF已通过；Q01-NG版本1.1.2-test-night目录SHA `A2FD13462DFE1B6E2C426ABBC4B7D597E9F7F18C92DE41F12C2D5148F62177F8`，Q02-PENDING目录SHA `93403F276FD38D6E33A066C711C8ABC51A9C804A3F16DE3681BDD764B8EADF3C`。本次真实worker清单仅指定P01问题，分别沿原批准P14/P15目标。容量冲突/预留/在途提交已实际实现及定向验证，不再是代码待实现；成组/整体/特殊/人工正式验收继续。下方此前缺口均保留核对时点，不能反向覆盖本检查点；生产局部限制不变。

## 2026-09-26 夜间当前 Test 输入适用范围

此增量覆盖本轮实际采用包；以下历史缺口保留原时点，不再次索取已授权的 Test 数值。实际加载摘要、账号、会话和资源身份以 `artifacts/recipe-execution-008/page-next-closure-20260926-night-r4/frozen-files.json` 及各 job 包为准。当前记录不批准生产坐标、真实 SDK 或算法精度。

| 输入类别 | 已确认语义/当前 Test 包 | 验证状态与限制 |
| --- | --- | --- |
| 单面及普通多面 | `contracts/test-virtual-mapping.md`、003产品动作合同；Q01/Q02/PARAM既有版本，当前四面用`fixtures/usr-e-1.0.2` | 前批同run页面与采集/高度事实已登记；Q04/Q05/Q06本批待验。缺轴/错来源不得用Test默认值补齐 |
| 成组 | `contracts/test-multi-object.md`；GROUP-F两个组P01/P03，各成员独立目标；GROUP-A-E保留来源成员组成，四面按合法3＋1 | r4 job-001/002已实际验证问题成员NG/Pending及其他成员保留；A/E本批仍运行中，不先记通过 |
| 整体 | 同一多对象合同；BASE/PIN独立媒体/结果，父位置承担物理翻面及处置；`fixture-assembly-a-e*.json` | Test点位/初始测量引用可执行；本批整体正式页面待验；不增加旋转前置 |
| 特殊旋转 | 003 `contracts/rotation-test-execution.md`；`fixture-rot-part-*.json`及`fixture-rot-assembly-ok.json` | 明确Test Enter/Rotate/Exit姿态、占用与出口；本批正式页面待验。不能据Test请求发明生产寄存器或角度 |
| 人工换面 | 003 `contracts/manual-test-execution.md`；`fixture-q04-manual.json`、`fixture-assembly-a-e-manual.json` | 已有占用/Complete协议与Test信号注入；必须补真实页面确认及安全解除，本批尚未结束 |
| 新指定质量用例 | `fixtures/night-closure-20260926/fixture-q01-ng.json`、`fixture-q02-pending.json` | 使用原批准源点和P14/P15目标，新目录版本及独立worker清单；实际目录/计划可加载，尚非运行通过。Q02只指定P01 Pending，P03保持正常，避免两个实体争用一个目标 |

公共配置/预算/模拟引用均逐包校验，不改007配置。当前固定Test目标只定义已登记格位，不能从一个目标推断更多容量或备用位置；T057仍需实现冲突/预留/取料后保存门禁。生产取放目标采样窗口、真实相机/高度标定等仅限制其实际使用分支，不能阻断其他Test工作。


日期：2026-09-25。仅查资料、配置和代码；没有执行产品运动。本表不表示 B04 已解除，也不表示完整 T049 已完成。来源资料只读。

| Q01动作/输入 | 已确认依据及实际代码/配置 | 当前判定 | 影响及解除条件 |
| --- | --- | --- | --- |
| 人工上料、启动、夹紧 | 需规 V1.1 §11.4 M01；003 已有正式按钮/夹紧合同。`specs/007-station01-integrated-loop/examples/public.virtual-loop.json` 是 Test 公共配置，含模拟 PLC/公共参数。 | Test 公共准备有既有实现和旧版本证据；当前版本页面心跳曾在点击前 Blocked。 | 003 T065 定向处理心跳；新 Q01 页面完整证据留 008 T055。现有配置不得推出生产按钮映射。 |
| 公共区域 ACK 与容量 | 需规 §11.4 M01 要求区域 ACK；`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs` `PrepareZonesAsync/WaitZoneAsync` 实际等待 `ZoneConfigAck`；003/007 旧 Test 配置范围曾联调。 | **仅既有 Test 区域和容量范围可复用**；新布局/目标容量属于 B08。 | Q01 使用同一已确认 Test 公共区域时可复用既有规则；新增区域先提供 ACK/容量来源。不把 ACK 写入成功常量。 |
| 公共 3D/F 固定点位 | 需规 §2.1、§11.4 M02/M03；`public.virtual-loop.json` `motion.points.threeD/f` 的 `id/version/xyz/unit/frame` 为 `SIM_MACHINE/mm` Test 值。指定 PLC 协议 §3.1.7 F① 使用公共 X/Y、`Scan_Target_Z`、命令 5。 | 公共 Test 点和 F 轴语义已定义；数值仅 Test。 | 003 T067 Host 与 VirtualPlc 已接入并有组件证据；Q01 整链仍待验证。F 不从产品配方求坐标，生产点位另待批准。 |
| F 本轮反馈与清零 | 指定 PLC 协议 §3.1.7 F②—F⑥：本轮 XY 到位 1、扫码 Z 到位 2 及实际 XYZ 匹配；PC 写 3 后清移动命令，保存后写 4，PLC 本轮扫码 Z 复位 1→2，PC 可靠核对本轮 2 后清 0；反馈轴归属持续到下一有效移动受理。 | **003 T067 Host/VirtualPlc 组件已实现并通过定向验证**，见 `../../003-plc-latest-protocol/evidence/008-f-handshake.md`；尚未经 Q01 正式前端整链验证。 | 008 T055 仍须按同一 run 核对 F 与冻结配方。E 不适用 Q01，也不能照搬 F。 |
| 有效槽位与内部对象 | `recipe-cases.md` Q01 为业务 S1 的一个有效槽；需规 §11.1/§11.3 规定 S1 单件及 AB 序列。旧 `recipe-catalog-review.json` 的 P01/CAP 只是已有 Test 样本；`RecipeRunPlanner.cs` 当前按 `slotId` 展开，`PublicPreparationHandoffV2.cs` 仍产生 0,0 占位。 | Q01 可预设一个合法 Test 槽及唯一对象；**物理槽索引/对象点位映射未由旧 P01 自动证实**。 | 002 T11 形成强类型引用；001 T090/008后续任务保存实际映射。008 T050 创建 Q01 配方前校验 Test 槽配置；003 T070 不得用序号或 0,0 发运动。 |
| A/B 相机使用轴组 | `高德_文档/上下位机对接/相机与轴组关系图.svg` 将 A/B 与 XY 轴组及检测相机系统关联；`采集动作逻辑图.svg` 明示分别移动至 A/B 采集位。需规 §2.1 表中 A/B/C/D 缺陷采集采用“相机/槽位/面对应固定点”，公用 Z 按配置使用高度。指定 PLC 协议 §3.1.7 检测 1/2 使用 `Camera_Target_X/Y/Z`，反馈 `XY_Pos_Confirmed`、`Z_Axis_Move_Status`、实际 XYZ。 | **共同 XY/检测 Z 的合同方向已确认**；不得将独立 E Z 或 F 扫码 Z 反馈解释为 A/B 到位。具体 A/B 每槽位点位及产品 Z 高度映射仍缺。 | 003 T070 前须取得该 Test 配方所用 A/B pointRef 对 XYZ 的合法映射及各轴反馈归属，或已确认的来源文件位置。生产轴地址/容差另依 OPEN-08。 |
| A/B 目标和本轮反馈关联 | 指定 PLC 协议 **§2.2 寄存器表 4x0001** 与需规 V1.1 §7：`XY_Move_Cmd=2` 是“去检测位”，1 是“去上料位”。§3.1.7 检测动作中的“例如1去上料位”仅是时序举例，不能覆盖寄存器表的检测命令。PC先下 `Camera_Target_X/Y/Z`；本轮 `XY_Pos_Confirmed=1`、`Z_Axis_Move_Status=2` 与 `Machine_Current_Pos_X/Y/Z` 匹配后写 `Inspection_Status=1` 并清移动命令；采集/判定/记录后写 2，PLC `Z_Reset_Status` 本轮 1→2，PC 核对可靠 2 后清 0；下一有效移动受理清旧反馈。 | **检测命令值2、目标字段、到位及复位握手已有来源**；产品角色代码未接线。未确定的是槽位/对象到A/B合法点位、以及3D高度到检测Z的映射。 | 003 T070可实现已确认状态机；没有合法产品目标不得派发。记录命令、目标、实际坐标、epoch、复位代次。 |
| Q01 A/B 的适用 Z | 需规 §2.1 明确“需要识别高度的 Z 由 3D 算法提供”，A/B 缺陷采集“公用 Z 按配置使用高度”；POS-002 与 OPEN-26 明确是否可直接作为 PLC Z 值、适用轴/工步、固定偏置和范围待补。 | **本路线不能自行改成固定 Z**。公共 3D 采集点的固定 Z 与后续 A/B 产品检测 Z 是不同用途。 | 003 T070 和 008 T052—T055 受阻。需给出 Q01 A/B 高度输入到检测 Z 的规则，或来源明确允许该具体工步使用固定值。Test 数值只能在语义确认后配置。 |
| 高度单位、基准、轮次、对象/槽映射 | 需规 §2.1、POS-002、DAT-001 指定保存 `Height_Z`/单位/来源及工件/面；008 E02、data-model `HeightFact` 要求 `scopeId/round/unit/frame/object`。当前公共 Test 配置的 3D scope 为 `SIM_TRAY/mm`，worker 高度样本 datum `SIM_REFERENCE`；`PublicPreparationHandoffV2.cs` 保留公共原始高度，尚不能据此映射 P01 的 A/B Z。 | **公共高度存在，但其到 Q01 目标的单位换算、基准/偏置、有效轮次和对象/槽位映射未确认**。单面 Q01 初始轮次需明确；翻面后轮次本批不处理。 | 003 T070 的产品运动和后续执行受阻。解除须提供 Q01 单槽目标与公共 3D sample/scope 的匹配规则、单位/基准/偏置、允许范围及失败处置来源。不得取首个样本或默认 0。 |
| 光源、采集和融合 | 需规 §11.4 M04 为 A 遍历有效槽、B 同批遍历，随后同面融合；008 E03 两输入 worker 合同。 | 规则明确；007 T032 只交付 worker 基础协议，本批不接完整检测。 | 008 T053 接实际图片/光源与同面融合。worker 基础通过不算 Q01。 |

## 需确认的最小输入（限 Q01）

1. 请指出 Q01 所用有效槽的**物理槽索引、对象身份与 A/B 各自合法检测点位**之间的映射资料（pointRef→目标 X/Y，及该 Test 点位是否已核准）。现有 P01 样本的虚拟格位坐标只说明旧测试布局，不能充当该映射。
2. 公共3D高度到该对象检测Z的sample/scope/轮次、单位、基准/偏置、范围及失败处置仍未配置；按本批决定**暂缓换算与追问**。保留原始高度和来源，不取首样本、不默认0或固定Z。

这两项仅阻塞 Q01/Q02 的产品目标准入、实际运动与对应完整路线验收。**产品检测命令是2**；共同 XY/检测 Z、目标字段、到位和复位规则已有需规与协议来源，均不再列为待答。F组件已通过，Q01整链尚未验证；必要原型控件许可已有决定。

## 第三批依赖分类

| 类别 | 当前条件 | 影响 |
| --- | --- | --- |
| 代码实施前置 | 已有线性计划、正式Host/SQLite/媒体、独立worker双输入、F合同及检测1/2握手；共享接口变更先按对应合同对齐 | 可实现共用AB/CD步骤分派、采集融合、持久查询、尾段和页面绑定；组件注入输入不抵扣整链 |
| 产品动作派发输入 | 本盘真实有效槽/对象对应的合法A/B或C/D pointRef→目标XY；公共3D sample/scope/轮次与该对象检测Z的已批准映射及范围；适用安全目标 | 缺任一项必须在派发前拒绝，不取首个高度、默认0、固定Z或旧Review格位坐标 |
| 完整任务验收证据 | 当前构建正式WPF选配方/启动/适用取盘、Host/VirtualPlc/独立worker/SQLite/媒体同run事实，心跳复核及Final | 只限制对应任务勾选与Q01/Q02完整通过，不阻止独立代码工作 |

## 第五批决定及当前范围（2026-09-25）

用户明确授权仅用于虚拟联调的版本化Test点位与3D高度映射，详见[合同](../contracts/test-virtual-mapping.md)。Q01 P01与Q02 P01/P03的对象、物理槽、源点、A/B/C/D点位及本轮sample关联现已在Test配置中具备，正式WPF虚拟运行均至Final，见[证据](fifth-batch-q01-q02.md)。因此上方当时的Q01/Q02 Test B04缺口描述已由**本批虚拟合同**解除，不再要求用户补Test数值。生产点位、高度标定、真实相机SDK仍未定义或验证；Test映射不适用于生产。其余历史行保留原核对时点和证据。T049是全范围输入任务，不能因这两个Test用例自动勾选。

## 2026-09-27 P03代表输入修正

原Q02 1.1.3-test-p03仅改worker Pending目标，实际job001暴露P03缺resolvedSortingTargets；保留失败，不记可运行通过。独立目录`fixtures/disposition-p03-1.1.4`保存P03原sourcePoint/协议槽3，目的点仅取原Q02已确认同盘P15 Test固定XYZ/版本/单位/坐标系，显式P03→P15 testSourceRef；旧1.1.3及night-closure目录不改。生成器与generation.json记录三份原输入SHA，新目录SHA64CF73AF4D5F00A12D155D46BD46006251C3E774E7B2A2D1C06CD1647F106D40；仅Test，不外推生产标定。正式新run验证尚待完成。

## 接续已核实输入子范围（2026-09-27）

1.1.4 Test P03→P15正式r8 job013/run dd8fdac0-d7cd-475e-9a82-450be56cabc9已完整通过页面、场景、实际预算/调用及取放提交审计，物理源槽3不等动作序号1，原Q02 P01 OK留原位。原1.1.3配置缺失与此前工具错误包保留。该证据仅Test映射，不外推生产标定。

普通Q04人工与整体A/E人工均有真实页面确认、同run及握手/保存/Final通过证据，见[接续记录](continuation-20260927.md)。旋转Pending、普通Q04/Q05/Q06子范围也已通过。剩余成组四面/E、故障媒体后新轮、整体Pending/E错误及旋转OK仍逐条复验。当前明确Test Host I/O运行开关属启动环境，未改配方/媒体/算法及原期限；所有生产局部输入限制保持原来源。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
