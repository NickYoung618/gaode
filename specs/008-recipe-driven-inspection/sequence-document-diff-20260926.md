# 本轮派生时序文档差异

本文件记录本轮修改前后差异，历史报告正文未改。新增派生时序和来源核查记录可直接审阅；以下为既有文件增量。

## specs/001-station01-public-preparation/contracts/device.md

```diff
--- before/specs/001-station01-public-preparation/contracts/device.md
+++ after/specs/001-station01-public-preparation/contracts/device.md
@@ -4,6 +4,8 @@
 
 **版本**：s01-device/1.0；Application语义端口，尚未对接PLC。  
 关联FR-006至FR-010、FR-018/019/027/033/034/040；REQ CTL-001至CTL-010、SAF-003/004/008/011。
+
+前四节保留原001抽象端口/FullSimulation历史边界，其中XY-only及未接PLC不是当前008规则。当前外部PLC公共XYZ/复位按本文008增量及[派生时序](../sequences.md)，不得以旧表覆盖新链。
 
 ## 1. 所有者与端口
 

```

## specs/001-station01-public-preparation/plan.md

```diff
--- before/specs/001-station01-public-preparation/plan.md
+++ after/specs/001-station01-public-preparation/plan.md
@@ -253,5 +253,7 @@
 
 specs/001-station01-public-preparation T090公共移交在虚拟 Test Q01/Q02 中消费已保存的本轮3D高度结果及冻结配方映射，按[008 Test合同](../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)解析显式目标；缺失、错关联或超限拒绝产品派发。原公共3D/F、F码绑定及必要保存要求保持，现场高度标定仍待确认。
 
-008第八批接线：PublicPreparationHandoffV2Consumer首轮目标解析不预取未来轮高度；同一冻结计划下后续轮目标按新提交的3D事实解析。T090仍按原完整条件验收。
-
+> 第八批历史接线按新提交3D解析后续轮。新版T090须保留初始真实测量并按独立逐面配置解析目标，不采第二轮3D；任务原完整条件不因历史组件通过而关闭。
+
+
+当前公共XYZ、3D/F对应轴复位、有效F绑定与非终态v2移交按[派生软件时序](sequences.md)；来源差异与任务追溯见[008本轮记录](../008-recipe-driven-inspection/sequence-alignment-20260926.md)。

```

## specs/001-station01-public-preparation/sequences.md

```diff
--- before/specs/001-station01-public-preparation/sequences.md
+++ after/specs/001-station01-public-preparation/sequences.md
@@ -1,6 +1,6 @@
 # 关键时序
 
-**版本**：1.0.1　**日期**：2026-09-20　**状态**：设计时序，不是运行记录。所有图中的“设备”可由真实或进程内模拟适配实现，复用同一流程和期限。每个长操作都先注册期限/终态容量，投递后返回，Coordinator不等待完整操作。
+**版本**：1.1.0　**日期**：2026-09-26　**状态**：派生软件设计时序，不是原始设备时序图或运行记录。008正式虚拟链使用外部VirtualPlc、实际采集/独立worker和存储；其他适配仅在其声明环境内使用。每个长操作都先注册期限/终态容量，投递后返回，Coordinator不等待完整操作。
 
 图中省略重复的容量预约与Capture意图提交；每次采集仍须按保存合同先保存触发关联，再调用采集端口，不能以图示简写绕过保存门。
 
@@ -8,52 +8,63 @@
 
 ```mermaid
 sequenceDiagram
-  participant A as API
-  participant W as Station01Coordinator
-  participant S as 保存通道
-  participant D as Motion与设备端口
+  participant A as 正式页面经API
+  participant W as 公共准备及移交
+  participant S as 持久保存
+  participant P as PLC适配器与PLC
   participant C as 采集与媒体
-  participant G as 算法端口
-  A->>W: 显式Start及请求身份
-  W-->>A: 202与RunId，保存状态另示
-  W->>S: 上下文、合法公共快照及Start意图
-  S-->>W: Committed
-  W->>D: 请求PLC启动
-  D-->>W: Accepted、安全、实体启动、夹紧完成
-  W->>S: 夹紧事实及3D固定XY意图
-  S-->>W: Committed
-  W->>D: 3D固定XY
-  D-->>W: 匹配完成
-  W->>S: XY完成事实
-  S-->>W: Committed
-  W->>C: 整盘范围3D采集
-  C-->>W: 可靠结束、媒体接管/文件完成
-  W->>S: 采集及媒体元数据
-  S-->>W: Committed
-  Note over W: 登记高度Call原期限，保存等待计入预算
-  W->>S: AlgorithmIntent及完整调用依据
-  S-->>W: Committed（失败或未知则不派发）
-  W->>G: 原期限仍有效才派发高度Call
-  G-->>W: 结果或允许异常
-  W->>S: 原始结果/异常及F移动意图
-  S-->>W: Committed
-  W->>D: F固定XY
-  D-->>W: 匹配完成
-  W->>S: XY完成事实
-  S-->>W: Committed
-  W->>C: F单次触发单帧
-  C-->>W: 可靠结束及文件完成
-  W->>S: 采集及媒体元数据
-  S-->>W: Committed
-  Note over W: 登记F Call原期限，保存等待计入预算
-  W->>S: AlgorithmIntent及完整调用依据
-  S-->>W: Committed（失败或未知则不派发）
-  W->>G: 原期限仍有效才派发F读码
-  G-->>W: 原始候选
-  W->>S: 候选/解析及Run完成/Handoff条件事务
-  S-->>W: Committed
-  W-->>A: HandoffReady引用
-  Note over W,D: 停在配方匹配前，不松夹/卸料/启动下一工位
+  participant G as 独立算法端口
+  A->>W: Start及expectedRecipeRef
+  W->>S: 请求身份和冻结公共配置及启动意图
+  S-->>W: Committed
+  W-->>A: 受理与runId 不等于完成
+  W->>P: 已授权启动及安全检查
+  P-->>W: 本次夹紧事实
+  W->>P: 公共区域配置握手
+  P-->>W: 本次Zone_Config_Ack
+  W->>S: 夹紧及公共配置事实和3D动作意图
+  S-->>W: Committed
+  W->>P: Inspection=0 固定XYZ 0003/0005/0007 后命令2
+  P-->>W: 本次XY=1 检测Z=2 实际XYZ
+  W->>W: 核验安全和本次目标一致
+  W->>P: Inspection=1 后清移动命令0
+  W->>C: 保存触发意图后 光源及初始整盘3D
+  C-->>W: 可读媒体和实际采集事实
+  W->>S: 媒体及算法意图
+  S-->>W: Committed
+  W->>G: 原期限内高度调用 仅Z
+  G-->>W: 真实高度样本或有限失败
+  W->>S: 算法结果与测量来源
+  S-->>W: Committed
+  W->>P: Inspection=2
+  P-->>W: 检测Z复位1到2
+  W->>P: 核验本轮复位后Inspection=0
+  W->>S: 复位事实及F动作意图
+  S-->>W: Committed
+  W->>P: 固定XY及ScanZ 0003/0005/0009 后命令5
+  P-->>W: 本次XY=1 扫码Z=2 实际XYZ
+  W->>W: 核验目标及安全
+  W->>P: Inspection=3 后清移动命令0
+  W->>C: 保存触发意图后 光源及F单帧
+  C-->>W: 可读媒体和实际采集事实
+  W->>S: 媒体及F算法意图
+  S-->>W: Committed
+  W->>G: 原期限内F解码
+  G-->>W: 原始候选或有限失败
+  W->>S: 解码结果及操作结束事实
+  S-->>W: Committed
+  W->>P: Inspection=4 不表示识码成功
+  P-->>W: 扫码Z复位1到2
+  W->>P: 核验本轮复位后Inspection=0
+  W->>W: 有效F唯一绑定且等于页面期望
+  opt 绑定后区域配置需要更新
+    W->>P: 本盘配置更新
+    P-->>W: 本次ACK
+  end
+  W->>S: F绑定和冻结计划及非终态handoff v2
+  S-->>W: Committed
+  W-->>A: 可查已提交移交 同run继续008
+  Note over W,P: 不解锁 不下料 001不执行产品翻面或分拣
 ```
 
 物理按钮由外部实际输入/测试夹具明确产生；并非API收到Start就自动触发。F候选在Domain按Ordinal处理，多不同值无主码。
@@ -77,9 +88,9 @@
   W->>G: 请求取消，后台隔离/回收
   S-->>W: Committed
   alt 高度调用
-    Note over W: F独立XY、安全、资源和保存有效才继续F
+    Note over W: F独立固定XYZ、安全、前次复位和保存有效才继续F
   else F调用
-    Note over W: 必要动作和保存满足后带异常移交
+    Note over W: 保存F失败和机械收敛事实 不放行产品
   end
   G-->>T: 超期结果或重复结果
   T-->>W: LateEvidence，原终态已关闭
@@ -98,10 +109,10 @@
   participant M as Motion
   participant D as PLC或模拟设备
   participant T as 期限仲裁
-  W->>M: 已保存意图的固定XY
+  W->>M: 已保存意图的适用固定XYZ
   M->>D: 命令
   D-->>M: Accepted
-  T-->>W: XY总期限到达，无可靠完成
+  T-->>W: 当前运动总期限到达，无可靠完成
   W->>M: 关闭准入，标Unknown/Held，请求受控停止
   M->>D: Stop（已确认契约）
   Note over W: 禁止采集及依赖动作，查询/心跳继续
@@ -163,7 +174,7 @@
   end
 ```
 
-以上路径共同断言：F触发最多一次、无配方操作、无Z/翻面/旋转/分拣/卸料动作、无下一阶段自动启动。
+以上路径共同断言：当前正常盘F单次触发；公共3D使用检测Z、F使用扫码Z及各自复位；F失败或保存未确认不放行产品。001只提交公共事实和非终态移交，F唯一绑定及冻结按共享职责完成后由008续接；产品翻面/旋转/分拣/下料由对应所有者执行。不能以001公共完成为Final。
 
 
 ## 6. 取消与移交交叉提交
@@ -201,3 +212,11 @@
 ## 7. 修订记录
 
 - 2026-09-20，1.0.1：H01在正常/超时时序加入调用意图提交门和恢复三窗口；H02加入取消/完成条件事务与回执交叉。M1只覆盖正常序列，完整取消/恢复为M2必做。图为设计，尚未执行或渲染验证。
+
+## 8. 当前适用依据与门禁
+
+本轮修正原1.0.1的XY-only、无Z、停在配方前及异常即放行描述；算法期限/动作未知/必要保存/取消竞争图继续适用，不新增恢复业务。公共3D固定XYZ与检测Z沿用[002 FR05用户确认记录](../002-plc-xyz-recipes/spec.md)；公共3D的1/2复位沿用[003既有验收链记录](../003-plc-latest-protocol/differences.md)，不是从当前代码反推。F及检测详细清零按分区协议§3.1.7。原图的“仅XY无Z”与此确有差异，见[来源追溯](../008-recipe-driven-inspection/sequence-alignment-20260926.md)。
+
+Inspection、XY、Z、ZReset为图中信号简称，方向/地址见[008信号表与后继时序](../008-recipe-driven-inspection/sequences.md)。反馈属于当前operation和连接代次，PLC没有软件operationId寄存器；下一有效移动受理清旧反馈。任何到位/复位失败、动作未知或必要保存失败均阻断依赖动作。3D失败不能造零高度；只有F独立目标和安全/复位门禁成立才可收敛到F，后继产品还须各面合法Z依据。
+
+2026-09-26，1.1.0：本轮仅补派生时序与现行边界，任务归specs/001-station01-public-preparation T090和specs/003-plc-latest-protocol T067/T070；渲染与保护检查见008本轮记录。

```

## specs/001-station01-public-preparation/spec.md

```diff
--- before/specs/001-station01-public-preparation/spec.md
+++ after/specs/001-station01-public-preparation/spec.md
@@ -1,4 +1,4 @@
-> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。第一工位移交前仍无配方调用。
+> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。上述无配方移交是旧独立001边界；008连续链采用下文已确认的F唯一绑定及非终态v2移交，当前派生动作见[sequences.md](sequences.md)。
 
 # 功能规格：第一工位——公共准备、3D高度采集与F料盘扫码
 
@@ -525,11 +525,11 @@
 
 本节按宪章5.0.0及REQ §7/11、008最新决定对齐，优先于前文与本节冲突的旧范围声明；旧日期/版本/证据仍作历史保留。
 
-- **FR-042（008对齐）**：公共准备继续使用独立固定XYZ、整盘3D和F单次采集；唯一F绑定后才移交产品执行。F旧1/2仅历史实现；最新F按指定PLC协议§3.1.7的0→3→4→0及扫码Z周期实现，定义已确认，代码和验证尚缺；E不从F推定。高度移交保留来源/单位/基准/轮次，不能把公共未映射高度当产品槽位Z。M01—03由008 Q01—Q22适用完整路线共同追溯，产品多面/分拣不并入001。
+- **FR-042（008对齐）**：公共准备继续使用独立固定XYZ、整盘3D和F单次采集；唯一F绑定后才移交产品执行。F旧1/2仅历史实现；最新F按指定PLC协议§3.1.7的0→3→4→0及扫码Z周期实现，定义已确认，既有F组件可复用，新版受影响整链待验证；E不从F推定。高度移交保留来源/单位/基准/轮次，不能把公共未映射高度当产品槽位Z。M01—03由008 Q01—Q22适用完整路线共同追溯，产品多面/分拣不并入001。
 - 成功条件：仅当008覆盖矩阵中对应Q/C/F的正式前端完整链路证据满足时判本增量通过；本轮未实现。具体M/用例/B依赖见[008矩阵](../008-recipe-driven-inspection/coverage-matrix.md)。
 
 
 当前008增量补充：正式启动保存前端expectedRecipeRef和目录快照；F后唯一匹配须与期望recipeId/version/catalogDigest一致才冻结、绑定和提交非终态s01-handoff/2.0。公共固定XYZ独立于产品配方。002/003/007已经批准的外部VirtualPlc、独立worker和固定媒体参与008正式链；旧进程内模拟、XY-only或公共完成即终态限制不适用于该链。历史验证范围不扩大。
 
-008第八批范围：Q03公共移交只移交首轮已提交高度及冻结身份；第二面高度在换面后按新轮次生成，未配置翻面接口时不得派发后继动作。
-
+> 旧协议第八批历史范围：Q03公共移交只移交首轮已提交高度及冻结身份；第二面按当时新轮3D组件方案验证。该方案不适用于新版；当前保留初始真实测量，按各面合法目标续接，不重采3D，见[008时序](../008-recipe-driven-inspection/sequences.md)。
+

```

## specs/001-station01-public-preparation/tasks.md

```diff
--- before/specs/001-station01-public-preparation/tasks.md
+++ after/specs/001-station01-public-preparation/tasks.md
@@ -824,7 +824,7 @@
 
 所属功能：`specs/001-station01-public-preparation`。跨功能依赖写作目录简称+任务ID，完整目录见008 tasks映射表。原则P03/P04/P05/P07/P08/P09/P11/P13，前端另P12及用户最小原型授权；新增任务全部未完成。旧T089被以下任务替换，未受影响的历史待办不取消；公共验证只做必要正常/失败，不构成交叉穷举。
 
-- [ ] T090 [US1] 承接008 FR-001/018的启动期望与公共移交：在`backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`、`RunExecution.cs`、`PublicPreparationHandoffV2.cs`保存所选目录快照/expectedRecipeRef，F唯一解析与期望一致后绑定冻结计划，移交真实实体/sourceSlot/pointRefs和高度来源，移除0,0占位。依赖：specs/002-plc-xyz-recipes T11、specs/003-plc-latest-protocol T068；F实际运行另依T067，产品映射依008 T049对应B04。交付：原子非终态handoff及匹配/不匹配保存证据；必要验证：同run一致、F冲突不进产品、提交未知不自动续接，证据存`specs/001-station01-public-preparation/evidence/008-handoff.md`；Q01完整证据归008 T055。
+- [ ] T090 [US1] 承接008 FR-001/018的启动期望与公共移交：在`backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`、`RunExecution.cs`、`PublicPreparationHandoffV2.cs`保存所选目录快照/expectedRecipeRef，F唯一解析与期望一致后绑定冻结计划，移交真实实体/sourceSlot/pointRefs和高度来源，移除0,0占位。依赖：specs/002-plc-xyz-recipes T11、specs/003-plc-latest-protocol T068；F实际运行另依T067，产品映射依008 T049对应B04。交付：原子非终态handoff及匹配/不匹配保存证据；必要验证：同run一致、F冲突不进产品、提交未知不自动续接，证据存`specs/001-station01-public-preparation/evidence/008-handoff.md`；Q01完整证据归008 T055。 本轮时序核对依据`specs/001-station01-public-preparation/sequences.md`§1/8：公共XYZ、3D检测Z/F扫码Z分别复位，F合法绑定及保存后非终态移交；后续面只解析初始真实测量的明确映射，不触发新3D。
 
 > 旧协议历史检查点（不代表新版状态）：008第八批T090子范围：仅调整Q03公共移交的当前轮解析；后续轮次依新3D提交，正式翻面未配置时仍Restricted；T090原条件和勾选不变。
 

```

## specs/002-plc-xyz-recipes/contracts/recipe-execution.md

```diff
--- before/specs/002-plc-xyz-recipes/contracts/recipe-execution.md
+++ after/specs/002-plc-xyz-recipes/contracts/recipe-execution.md
@@ -8,7 +8,7 @@
 Q01—Q22、C01—C08、F1—F6及同程序版本参数变化证据见008 recipe-cases/coverage-matrix，历史T01—T09不抵扣新增执行。
 
 目录接口枚举及启动expectedRecipeRef字段以008 contracts/api-results.md v2.0为准；IRecipeCatalog/JsonRecipeCatalog提供同目录快照数据，003 API投影和001 F校验共同消费。recipeId/version/catalogDigest须一致，禁止静默选最新版。冻结模型采用008 data-model，区分InspectionTarget/physicalEntity/part/group/face/heightRound/sourceSlot；步骤有唯一ownerStage，计划序号不得冒充PLC槽号。每种Q通过实际页面完整执行验证，规划器展开仅为辅助检查；Test目录与生产目录隔离，不修改在途快照。
-# Q03 Test受限映射增量（2026-09-25）
+# Q03 Test受限映射增量（2026-09-25历史，不作为新版准入）
 
 `resolvedDetectionTargetsByFaceRound`按`face:heightRound`保存每面A/B位置和Test高度绑定；同一逻辑`pointRef`仍须携对象/槽位/面/轮次解析到不同实际点位ID。目录校验必须发现缺第二面映射或错轮次，完整数据仍因自动翻面未获本轮握手合同而保持Restricted。详情以[008虚拟Test映射合同](../../008-recipe-driven-inspection/contracts/test-virtual-mapping.md)为准。
 

```

## specs/003-plc-latest-protocol/spec.md

```diff
--- before/specs/003-plc-latest-protocol/spec.md
+++ after/specs/003-plc-latest-protocol/spec.md
@@ -81,7 +81,7 @@
 FR02 夹紧观察窗口复用已存在的版本化预算 `BusinessBudget.businessMs.clampCompletion`（单位 ms，正整数且至少 1）；结构来源为 `../001-station01-public-preparation/contracts/budget.schema.json`，随本次运行配置快照冻结并保留 id/version/purpose/source。计时从本次启动受理事实提交后进入 `WaitingClamp` 开始，与 `businessMs.plcAcceptance` 的启动受理期限分离；按既有 `ResponseBeforeDeadline` 规则，仅截止前收到同一连接代次的状态 `1` 才能继续，达到或超过窗口仍未满足时记录 `ClampTimeout/UnknownHeld` 并保持占用；状态 `2` 立即报警锁停，断联或代次变化进入设备未知处置，不等待窗口耗尽。003历史VirtualPlc测试使用 `../001-station01-public-preparation/examples/budgets.virtual-plc.json`（`s01-budget-virtual-plc`，v1.0.0，purpose=Test）的5000 ms夹紧窗口；007须另用覆盖指定采集/算法延迟的新版本Test预算，并保留适用夹紧门禁。此值不是生产默认值，不硬编码进流程，不推定真实设备参数已批准。
 
 FR03 心跳持续翻转/响应；连续 3 秒无有效翻转时必须锁定动作、公开受限/报警状态并禁止自动续跑，重连与复位也不得自行恢复执行。验证证据必须包含心跳时间序列、3 秒期限、锁动作/报警状态和重连后仍未自动续跑的状态记录。
-FR04 除命令 4 下料外，每次移动仍按既有要求验证 XYZ 到位双状态及三坐标匹配，匹配后才置 `Inspection_Status=1`，随后可清本方 `XY_Move_Cmd(4x0001)=0` 并执行采集/算法；该清零不得使 PLC 的 `XY_Pos_Confirmed(4x0002)=1` 到位事实提前变成 0。结果持久化后置 `Inspection_Status(4x0052)=2`，只有读到 `Z_Reset_Status(4x0053)=2` 才能将 4x0052 清为 0；下一条合法运动实际受理时，PLC 才清本轮到位/复位反馈并开始新一轮反馈。来源为最新版《PLC与上位机通信接口协议》§3.1.7；验证须按同一 operation/connection epoch 记录点位读写、坐标匹配和先后时间，不把 0 解释为空值。命令 4 不执行新的 Z 运动，其下料到位判据另见 FR16–FR18；不得将检测运动的双状态直接当作命令 4 的新 Z 动作事实。
+FR04 公共3D及产品检测按既有确认的检测周期执行：固定XYZ、本次XY=1和检测Z=2及实际XYZ匹配后置Inspection_Status=1，再清移动命令0；采集、算法及必要记录完成后置2，核验本轮检测Z_Reset_Status=2后清Inspection_Status=0。F按§3.1.7使用命令5、Scan_Target_Z与3/4及扫码Z复位，不沿用检测1/2。下一有效运动受理才清旧到位/复位并更新轴归属。命令3翻面和命令4下料分别按§3.1.5/§3.1.6使用Grab_Target_Z(000B)及本次到位/实际XYZ判据，不套检测Inspection周期或额外创造抓取Z复位握手；命令4不再采用XY-only。软件operation/connection epoch关联不是新增PLC寄存器。派生细节见[008时序](../008-recipe-driven-inspection/sequences.md)，来源差异见其本轮追溯记录。
 FR05 每次第一工位 3D/F 均必须执行正式握手；F 失败必须锁停且不得加载或绑定配方，只有本次运行取得唯一 F 码成功事实后才允许产品配方加载/绑定。验证证据必须同时覆盖失败锁停、重复/歧义码拒绝和唯一成功后的 plan/bind 顺序。
 FR06 配方 Provider、坐标文件、PLC Provider/网络/字序可配置；不按配方产品名分支。Recipe_ID 仅 HMI 显示。
 FR07 故障、恢复、状态和必要证据必须可查询。旧任务重新检测或报废只能由受控人工决策产生，决定必须记录 actor、时间、原因、原任务和证据引用；Host 重启、连接恢复或普通复位不得自动重检、自动报废或自动重放已完成动作。

```

## specs/003-plc-latest-protocol/tasks.md

```diff
--- before/specs/003-plc-latest-protocol/tasks.md
+++ after/specs/003-plc-latest-protocol/tasks.md
@@ -269,11 +269,11 @@
 
 - [ ] T068 [US1] S0提供前端共享API及保存投影：`backend/src/Gaode.Host/Api/RecipeEndpoints.cs`、`RunEndpoints.cs`和对应应用查询/上下文codec按008 api-results/2.0实现catalog.items、contextJson/2.0、expectedRecipeRef受理保存、recipeSelection/recipeExecution/allowedActions、results/movements及提交引用和媒体身份，通知revision驱动GET。依赖：specs/002-plc-xyz-recipes T11；结果事实未产生时空/受限，不填默认完成；后续层级结果复用同投影。必要合同验证：401/403明确拒绝、202非Final、旧上下文不启动新路线、未提交事实不公开，路径`backend/tests/Gaode.Contracts.Tests`；证据`specs/003-plc-latest-protocol/evidence/008-api.md`。对应FR19、008 FR-001/016/018、Q/C全体。
 
-- [ ] T069 [US4] S1提前接最终结束链：`backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`、`ThreeStageWorkflowExecutor.cs`及`backend/src/Gaode.Host/Api/RunEndpoints.cs`按新版先检测→下料XYZ到位→适用分拣→整盘收敛顺序，替代T064旧命令4规则，分别保存WholeTray、ObservedUnlocked、页面取盘确认及Final；普通OK无搬运不依赖T071。依赖：specs/008-recipe-driven-inspection T054及T068；specs/006-frontend-station01-console T049仅为页面验收依赖，不阻塞后端实现。必要验证：缺结果/保存失败/未解锁拒绝确认不Final，原子最终提交可查；证据`specs/003-plc-latest-protocol/evidence/008-completion.md`。对应FR19及008 FR-011/014、C06/F4/F6，完整链归008 T055。
+- [ ] T069 [US4] S1提前接最终结束链：`backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`、`ThreeStageWorkflowExecutor.cs`及`backend/src/Gaode.Host/Api/RunEndpoints.cs`按新版先检测→下料XYZ到位→适用分拣→整盘收敛顺序，替代T064旧命令4规则，分别保存WholeTray、ObservedUnlocked、页面取盘确认及Final；普通OK无搬运不依赖T071。依赖：specs/008-recipe-driven-inspection T054及T068；specs/006-frontend-station01-console T049仅为页面验收依赖，不阻塞后端实现。必要验证：缺结果/保存失败/未解锁拒绝确认不Final，原子最终提交可查；证据`specs/003-plc-latest-protocol/evidence/008-completion.md`。对应FR19及008 FR-011/014、C06/F4/F6，完整链归008 T055。 按`specs/008-recipe-driven-inspection/sequences.md`§2核对000B下料、本次XYZ、先下料后适用分拣及保存/解锁/页面确认门禁。
 
 - [ ] T070 [US1] S1接产品实际定位/适用Z与检测1/2闭环：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`LatestProtocolStageActionAdapter.cs`、`VirtualPlc/VirtualPlcEngine.cs`扩正式产品角色及真实坐标反馈关联，经唯一Motion/Safety准入。依赖：specs/008-recipe-driven-inspection T049确认所用B04、specs/002-plc-xyz-recipes T11及T067共享状态改动结束；显式Test XYZ组件接线可先做，正式Q01派发仍依B04。交付：产品命令/轴映射有来源；匹配本轮XYZ才允许采集，结果提交后2、复位可靠2后0；必要到位/复位失败测试存`specs/003-plc-latest-protocol/evidence/008-product-motion.md`。对应008 FR-002/F2/Q01；没有轴语义时本任务Blocked，不能补造VirtualPlc规则。
 
-- [ ] T071 [US1] S2—S4共享真实搬运适配唯一归属：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`VirtualPlc/VirtualPlcEngine.cs`按已确认B02/B08源目标/物理索引/取放反馈接分拣、自动翻面及进出旋转站，旋转姿态部分另需B03；参数按B04。依赖：specs/008-recipe-driven-inspection T049对应输入、T070；逐能力解锁，不等待所有分支输入才做已确认部分。交付：各支持动作本轮意图/源点/取/目标点/放/完成证据，未知保留在途不重发；`specs/003-plc-latest-protocol/evidence/008-transfer.md`按能力列Supported/Blocked。对应008 FR-005/009/010、C02/05/06、F2/F5；业务分配/占用提交归008 T057/T065。
+- [ ] T071 [US1] S2—S4共享真实搬运适配唯一归属：`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`VirtualPlc/VirtualPlcEngine.cs`按已确认B02/B08源目标/物理索引/取放反馈接分拣、自动翻面及进出旋转站，旋转姿态部分另需B03；参数按B04。依赖：specs/008-recipe-driven-inspection T049对应输入、T070；逐能力解锁，不等待所有分支输入才做已确认部分。交付：各支持动作本轮意图/源点/取/目标点/放/完成证据，未知保留在途不重发；`specs/003-plc-latest-protocol/evidence/008-transfer.md`按能力列Supported/Blocked。对应008 FR-005/009/010、C02/05/06、F2/F5；业务分配/占用提交归008 T057/T065。 按`specs/008-recipe-driven-inspection/sequences.md`§4/5验证各实体Flip_OK和Sorting_OK清零；状态2仅取料，槽号取料后提交，不为抓取动作新增Inspection/ZReset握手。
 
 - [ ] T072 [US4] S3/S5补人工接口：`backend/src/Gaode.Host/Api/RunEndpoints.cs`及`backend/src/Gaode.Application/Workflow`按008 API新增manual-flip-confirmations并补recovery-checks原因/evidenceRefs持久查询、checkId与continue关联，权限/expectedRevision/stepId由后端核验。依赖：T068；翻面语义依008 T060与B07，恢复依008 T068及适用B07，两部分各自验收不互阻。交付：确认记录与安全门分开、无默认全选/未知动作重放；必要错误权限/陈旧确认拒绝，证据`specs/003-plc-latest-protocol/evidence/008-manual.md`。对应008 FR-005/012/016、C02/C07/F5。
 

```

## specs/008-recipe-driven-inspection/contracts/evidence.md

```diff
--- before/specs/008-recipe-driven-inspection/contracts/evidence.md
+++ after/specs/008-recipe-driven-inspection/contracts/evidence.md
@@ -26,7 +26,7 @@
 ## 人工、物理与历史证据
 
 适用人工换面/取盘/恢复分别记录页面操作者和实际物理/虚拟反馈来源，软件确认不证明硬件安全。
-007自动Test客户端取盘仅是历史渠道事实；008不能将它填成前端人工确认。用户已授权在现有正式页面最小补齐必要控件；006须记录原型基线变化和实际交互。控件及后端尚未完成时对应路线保持Blocked/NotRun，不能用调试界面或辅助API代替。
+007自动Test客户端取盘仅是历史渠道事实；008不能将它填成前端人工确认。本轮只绑定已经交付的正式页面控件；006记录实际交互，客户原型基线只读，不新增或改变控件。控件及后端尚未完成时对应路线保持Blocked/NotRun，不能用调试界面或辅助API代替。
 新增示教不在本期证据要求内。
 
 保留007最新当前构建页面Blocked及旧构建成功包的原始事实；不覆盖、不改判、不抵扣008新定位/融合/多面/组/整体要求。

```

## specs/008-recipe-driven-inspection/contracts/execution.md

```diff
--- before/specs/008-recipe-driven-inspection/contracts/execution.md
+++ after/specs/008-recipe-driven-inspection/contracts/execution.md
@@ -96,3 +96,7 @@
 B01-F/E及B09-H/U是spec B01/B09的细分，不新增业务范围。未回复的现场输入保留受阻；完成设计不表示它们已解决。
 
 协议身份目标为 `plc-upper-20260925-partitioned-ack`，并携原件SHA256 `405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`。这是待实施身份，不声称Host当前已提供。XY_Move_Cmd/XY_Pos_Confirmed名称与地址不变；预留从0056开始；字节序/地址解释仍需真机校准。
+
+## 派生时序引用（2026-09-26）
+
+按[软件时序](../sequences.md)核对E02/E04中的当前面批采、逐实体翻面和E05普通结束链；[来源关系](../sequence-alignment-20260926.md)保存原图差异及明确依据。Flip/下料/Sorting不得额外套用检测Inspection或创造抓取Z复位握手；检测及F各自复位不省略。软件保存门是既有必要事实提交，不假定PLC/DB共同事务。

```

## specs/008-recipe-driven-inspection/plan.md

```diff
--- before/specs/008-recipe-driven-inspection/plan.md
+++ after/specs/008-recipe-driven-inspection/plan.md
@@ -176,3 +176,7 @@
 先共享协议身份/Flip/Unload→多面计划与合法目标/数据/预算→正式页面Q03到Final→其余适用两/四面。普通OK无需等待全部分拣、E、旋转或组策略；NG/Pending、人工和业务差异继续由原S2—S5任务收口。每片实现前置是所用能力/数据交付，不要求所有父任务全勾；整项验收仍按原未完成范围加本次增量。
 
 新合同定义在execution.md、data-model.md及test-virtual-mapping.md，详细消费者/停止点见[实施清单](implementation-checklist-20260926.md)。本轮止于文档与只读一致性分析，不执行implement。
+
+## 派生时序增量（2026-09-26）
+
+[sequences.md](sequences.md)补Q03同run主链、AB批采/逐图分析与对应Z复位、逐实体Flip/ACK、同盘取放/ACK；公共3D/F详见001 sequences。来源差异由[本轮追溯](sequence-alignment-20260926.md)按明确确认依据处理，不以日期或旧报告裁决。现有线性执行入口、模型与API保持，任务仅追加图示步骤验收引用，不增加新框架/寄存器。宪章P01/03/04/07/08/09/10/12/13设计核对成立；生产轴标定/特殊旋转/人工恢复仍局部受限。图示完成不计实施进度。

```

## specs/008-recipe-driven-inspection/quickstart.md

```diff
--- before/specs/008-recipe-driven-inspection/quickstart.md
+++ after/specs/008-recipe-driven-inspection/quickstart.md
@@ -64,3 +64,5 @@
 配方加载、规划展开、组件测试或后台辅助样本均不抵扣前端完整通过。
 结束时只清理本次manifest登记且身份匹配的进程；保留数据库、媒体、原始日志及失败包，退出后核验可定位性。
 虚拟通过只标SoftwareLoopOnly，不宣称真机、精度或生产节拍通过。
+
+实施前按[派生时序](sequences.md)逐步核对Q03及分拣子时序；[本轮来源与保护检查](sequence-alignment-20260926.md)仅为文档验证，不是运行通过。执行步骤、实际能力和必要证据仍按本文与实施清单要求。

```

## specs/008-recipe-driven-inspection/research.md

```diff
--- before/specs/008-recipe-driven-inspection/research.md
+++ after/specs/008-recipe-driven-inspection/research.md
@@ -70,3 +70,9 @@
 本轮按本地speckit-plan技能完成研究及设计；不调用tasks/implement。
 官方[存量项目指南](https://github.github.com/spec-kit/guides/existing-projects.html)强调基于已有架构与测试规划；[规格演进指南](https://github.github.com/spec-kit/guides/evolving-specs.html)支持先更新spec，再修订plan/tasks并分析一致性；[行动指南](https://github.github.com/spec-kit/reference/agentic-sdd.html)支持大型功能分阶段实施。
 这些参考说明工作方法，不替代项目业务合同或当前实现证据。
+
+## 派生软件时序补充研究（2026-09-26）
+
+- Decision：保留原图，另写[派生时序](sequences.md)，按[来源追溯](sequence-alignment-20260926.md)逐项区分接口细化、真实顺序/轴差异和已有确认。
+- Rationale：上一轮漏改001 sequences；原始用户附件已直接确认Flip、无重扫、分拣两阶段和盘末顺序，公共XYZ另有002 FR05用户授权记录、公共3D复位有003既有验收链记录。当前代码仅用来定位待改消费者，不作为设备规则来源。
+- Alternatives considered：未采用重画原图、按日期一概覆盖、复制第二套执行器、把所有未知作为Q03全局阻塞。模型和技术选型不变，不重生data-model或API。剩余特殊旋转/生产标定只限制依赖动作。

```

## specs/008-recipe-driven-inspection/spec.md

```diff
--- before/specs/008-recipe-driven-inspection/spec.md
+++ after/specs/008-recipe-driven-inspection/spec.md
@@ -186,7 +186,7 @@
 | FR-008 | S3按装配后部位检测并汇总整体，换面和搬运以实际整体实体执行；S3不自动等同于旋转路线 | §11.1、§11.7—11.8；M16 | P03/07 | US4 |
 | FR-009 | 特殊类型1完成进站、占用确认、实际姿态核验、当前实体逐相机检测及适用出口；OK回原槽，NG/Pending到各自目标 | §11.7；M10—M13 | P03/04/07 | US4，F2/F3/F5 |
 | FR-010 | 分拣使用实际实体、源槽和目标，容量成立且完成反馈可靠后更新处置；特殊出口已处置实体不再盘末分拣 | §11.7—11.8；M12—M16 | P04/07/08 | US3/4，F5/F6 |
-| FR-011 | 必检与处置收敛、无未处理在途件且必要保存完成后，按安全及位置条件下料；解锁、取盘确认和业务完成分别记录 | §11.9；M17 | P04/07/08 | US5，F4/F5 |
+| FR-011 | 普通路线必检完成及必要保存后先下料定位，再完成适用同盘分拣；检测/下料/处置收敛且无未知在途、必要保存成立后才能解锁；解锁反馈、页面取盘确认和最终提交分别记录 | §11.9；M17 | P04/07/08 | US5，F4/F5 |
 | FR-012 | 支持当前主流程需要的人工恢复核对，保留实物与原任务依据，不自动重放未知动作；本轮使用已配置点位，新增示教延期 | §11.9；M19；本轮澄清第2问 | P04/08/10 | US5 |
 | FR-013 | 等待和整盘预算须覆盖本配方实际采集、分析、换面、搬运、保存及已允许重试；超时形成可查结果，不因重复尝试无限延长 | §11.12；OPEN-18 | P04/06 | US1—US5，F2/F3/F5 |
 | FR-014 | 命令受理、阶段、设备交互、阻断、超时和失败提供分级分类、可关联和可持久查阅的日志；控制重复日志，必要失败可定位 | §11.11；AGENTS.md日志规则 | P09 | US1—US6，F1—F6 |

```

## specs/008-recipe-driven-inspection/tasks.md

```diff
--- before/specs/008-recipe-driven-inspection/tasks.md
+++ after/specs/008-recipe-driven-inspection/tasks.md
@@ -47,7 +47,7 @@
 
 目标：能配置、选择并受理Q01，F/心跳/目录/预算组件各有证据；这不等于Q01已通过。先做003 T065、T067，specs/002-plc-xyz-recipes T11、specs/007-station01-integrated-loop T031/T032及下列任务；前端006 T048不拖到最后。无需工程初始化或全面重构。
 
-- [ ] T049 核对并登记实际所用输入，产物`specs/008-recipe-driven-inspection/contracts/execution.md`的B表及`evidence/input-readiness.md`。对应FR-001/002/005—010/012/018；依赖：现有来源与当前代码，无实现前置。先写Q01逐动作输入表：公共ACK既有Test依据、AB产品轴/目标字段/到位反馈、适用Z与高度scope/单位/基准/槽位映射。给来源页段及已定义/缺失/不适用依据；数值可Test，语义不得编造。缺B04时明确阻塞003 T070及Q01运动验证，其他准备继续。E/B02/B03/B05—08只限制各使用点；F与原型许可不是待外部输入。解除条件为已确认来源覆盖实际动作，不能用任务勾选代替现场决定。
+- [ ] T049 核对并登记实际所用输入，产物`specs/008-recipe-driven-inspection/contracts/execution.md`的B表及`evidence/input-readiness.md`。对应FR-001/002/005—010/012/018；依赖：现有来源与当前代码，无实现前置。先写Q01逐动作输入表：公共ACK既有Test依据、AB产品轴/目标字段/到位反馈、适用Z与高度scope/单位/基准/槽位映射。给来源页段及已定义/缺失/不适用依据；数值可Test，语义不得编造。缺B04时明确阻塞003 T070及Q01运动验证，其他准备继续。E/B02/B03/B05—08只限制各使用点；F与原型许可不是待外部输入。解除条件为已确认来源覆盖实际动作，不能用任务勾选代替现场决定。 输入依据补`specs/008-recipe-driven-inspection/sequence-alignment-20260926.md`逐步骤来源表；先核Q03所用公共/产品/翻面/下料目标，旧图差异不由代码裁决。
 
 - [ ] T050 建立版本化Q/C配方数据与受控媒体清单，路径`specs/008-recipe-driven-inspection/fixtures/recipes.json`、`fixtures/cases.json`、`fixtures/media-manifest.json`。对应FR-004/015/017/018、Q01—Q22/C01—C08；依赖：specs/002-plc-xyz-recipes T11模型、specs/007-station01-integrated-loop T031清单格式；先交付Q01数据，其余按阶段增量，不等待全部B才准备数据。冻结recipe-cases规定的真实序列/合法槽位/身份/参数/F码（核对无冲突）、点位与预算引用；未确认能力可描述但不得标可执行。必要验证：Q数据22唯一、用途/版本引用合法、非法映射受限，素材实际可读且标Simulated；不是运行通过证据。
 
@@ -63,7 +63,7 @@
 
 - [ ] T052 [US1] 在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`及`backend/src/Gaode.Host/Composition/AdapterBindings.cs`实现线性冻结步骤的唯一应用入口并接正式Detection，消费Position/适用Z/捕获/保存/Reset等已支持步骤；Sorting/Unload明确移交外围，条件不适用须有依据，未知步骤受限不跳过。对应FR-001/002/004/014/018；依赖：specs/002-plc-xyz-recipes T11、T051、specs/001-station01-public-preparation T090及003 T070；纯执行分派及显式Test目标的单面组件接线可先按合同开发，但Q01实际运动仍须B04解除。意图先提交、反馈关联、期限和诊断贯通，必要检查顺序/错坐标无采集/Unknown无重发；证据`evidence/execution-chain.md`，不能由局部组件宣称Q通过。
 
-- [ ] T053 [US1] 在`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`、`FileBackedCapture.cs`及`backend/src/Gaode.Application/Workflow/FaceResultAggregator.cs`接入配置光源/曝光/ROI、按对象/面/轮次/相机选择媒体，A整批后B（CD复用），每图独立worker分析/保存后复位，第二输入齐后另调用融合；前批不等融合、不混身份。对应FR-002/003/014、Q01/F3；依赖：T052、specs/007-station01-integrated-loop T032和T050适用媒体。交付：实际单图/双图调用与租约/计时记录；缺输入有限Pending、不伪造采集或融合，必要顺序/身份验证存`evidence/capture-fusion.md`。
+- [ ] T053 [US1] 在`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`、`FileBackedCapture.cs`及`backend/src/Gaode.Application/Workflow/FaceResultAggregator.cs`接入配置光源/曝光/ROI、按对象/面/轮次/相机选择媒体，A整批后B（CD复用），每图独立worker分析/保存后复位，第二输入齐后另调用融合；前批不等融合、不混身份。对应FR-002/003/014、Q01/F3；依赖：T052、specs/007-station01-integrated-loop T032和T050适用媒体。交付：实际单图/双图调用与租约/计时记录；缺输入有限Pending、不伪造采集或融合，必要顺序/身份验证存`evidence/capture-fusion.md`。 时序按`specs/008-recipe-driven-inspection/sequences.md`§3：每图必要保存后本轮检测Z复位，A整批后B整批，同对象B已复位且双输入齐才融合；不新增全批融合等待屏障。
 
 - [ ] T054 [US1] 在`backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs`、`StageEventStore.cs`及确需时`backend/tools/Gaode.StorePrep`保存步骤意图/反馈、媒体、单图/面/实体结果、真实位置/处置与版本引用；复用事件和短事务，只有必要列才维护迁移，Host不自动改表。对应FR-002/003/014/016、SC-003、F4；依赖：T053、specs/002-plc-xyz-recipes T11；交付已提交事实供003 T068/T069查询，不做第二份API。验证真实SQLite/媒体读回、保存失败/CommitUnknown无后继及无Final，日志退出后可按requestId追溯；证据`evidence/persistence.md`。
 
@@ -79,7 +79,7 @@
 
 - [ ] T056 [US1] 在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`、`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`补CD、多槽/非连续槽批次，维护对象列表与物理槽分离；复用相同执行/融合入口。对应FR-002/003/004、Q02/C01；依赖：T055、T050对应配方/B04槽映射。交付A-all/B-all、C-all/D-all和空槽不采集的实际顺序/媒体身份，必要失败不串对象，证据`evidence/batch-order.md`。
 
-- [ ] T057 [US1] 在`backend/src/Gaode.Application/Workflow/SortingTargetAllocator.cs`、`RecipeSortingMapper.cs`、`ThreeStageWorkflowExecutor.cs`按实际实体/sourceSlot/protocolSlotIndex与目标区域/点/容量分配预留，可靠取放后提交占用/处置，替换Sequence充槽号和零坐标；对应FR-010、C06/F5/F6。依赖：T054、specs/003-plc-latest-protocol T071对应分拣能力、B02/B04/B08；混合NG+Pending另依B06，分别单一结果无需等混合策略。必要验证：非连续源槽、NG/Pending分区、满位不派发、未知不释放不重发，产物`evidence/sorting.md`；PLC实现不在本任务重复。
+- [ ] T057 [US1] 在`backend/src/Gaode.Application/Workflow/SortingTargetAllocator.cs`、`RecipeSortingMapper.cs`、`ThreeStageWorkflowExecutor.cs`按实际实体/sourceSlot/protocolSlotIndex与目标区域/点/容量分配预留，可靠取放后提交占用/处置，替换Sequence充槽号和零坐标；对应FR-010、C06/F5/F6。依赖：T054、specs/003-plc-latest-protocol T071对应分拣能力、B02/B04/B08；混合NG+Pending另依B06，分别单一结果无需等混合策略。必要验证：非连续源槽、NG/Pending分区、满位不派发、未知不释放不重发，产物`evidence/sorting.md`；PLC实现不在本任务重复。 按`specs/008-recipe-driven-inspection/sequences.md`§5区分预留、取料在途和放料/ACK完成后的占用提交，原图整体动作不能替代两段反馈。
 
 - [ ] T058 [US6] 用同一程序构建从页面分别完整运行Q01基版和Q01-PARAM新版本（可引用T055基版），在无活动运行时装载新目录，不建设热加载。产物`fixtures/`版本数据、`evidence/config-change.md`及实际运行包；对应FR-017/018、SC-002、C01/C08。依赖：T056、specs/006-frontend-station01-console T049、specs/007-station01-integrated-loop T033及对应B04；必要证据：同程序摘要、实际槽位/采集光源/算法参数和执行调用差异，选择/F/冻结/保存一致，旧快照不变。更名或仅计划展开不通过。
 
@@ -89,11 +89,11 @@
 
 目标：页面运行两/四面路线，适用自动/人工换面后校验该面合法目标，不重采3D；每个Q有完整Final证据。新增E只有合同明确后才执行；不含E路线继续。
 
-- [ ] T060 [US2] 在`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs`、`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`及`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`取消本路线强制RescanWholeTray，正式Flip调用specs/003-plc-latest-protocol T071完整定位/双反馈/Flip_OK闭环后，在同一检测循环推进阶段与目标面并解析合法目标。保留初始3D/F/同盘对象/历史结果，已完成成员不重复翻面/检测，整体实体共享动作一次。对应FR-005/012、C02/Q03—Q22/F2；实现前置为specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090及本功能T050/T051/T054所需数据/保存子交付，自动另specs/003-plc-latest-protocol T071已实现能力与B04合法配置；不等T055整项或全部分拣。人工另B07已定义接口及未决业务子项，API唯一归specs/003-plc-latest-protocol T072。完成：新计划无翻后3D、真实面推进、不混测量/连接代次；最少验证Q03双面、同目标面连续实体、错面/缺目标/ACK失败无下一采集，存新版证据，旧faces-height.md只读引用。
+- [ ] T060 [US2] 在`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs`、`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`及`backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`取消本路线强制RescanWholeTray，正式Flip调用specs/003-plc-latest-protocol T071完整定位/双反馈/Flip_OK闭环后，在同一检测循环推进阶段与目标面并解析合法目标。保留初始3D/F/同盘对象/历史结果，已完成成员不重复翻面/检测，整体实体共享动作一次。对应FR-005/012、C02/Q03—Q22/F2；实现前置为specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090及本功能T050/T051/T054所需数据/保存子交付，自动另specs/003-plc-latest-protocol T071已实现能力与B04合法配置；不等T055整项或全部分拣。人工另B07已定义接口及未决业务子项，API唯一归specs/003-plc-latest-protocol T072。完成：新计划无翻后3D、真实面推进、不混测量/连接代次；最少验证Q03双面、同目标面连续实体、错面/缺目标/ACK失败无下一采集，存新版证据，旧faces-height.md只读引用。 逐实体与同一循环续接按`specs/008-recipe-driven-inspection/sequences.md`§2/4核验；图示不替代运行证据。
 
 - [ ] T061 [US2] 按确认后的E独立合同在`backend/src/Gaode.Application/Workflow/RecipeExecutionCoordinator.cs`、`backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`和`VirtualPlc/VirtualPlcEngine.cs`接E采集/解码/绑定/保存/复位；对应FR-006、C02。依赖T060、B01-E/B04/B06明确的轴/清零/异常及放行规则；目前Blocked，解除须记录来源并同步合同双方，绝不套F规则。交付正确内部对象关联外部码，必要失败有限且受限，证据`evidence/e-scan.md`；不阻塞不含E路线。
 
-- [ ] T062 [US2] 复用正式页面驱动完成Q03—Q22，合法组合可由一运行多个对象分别覆盖完整序列；补Q04-MANUAL和适用Q04-MANUAL-E的C02操作，路径`evidence/q03-q22.md`、`evidence/index.md`及逐run运行包。对应FR-004/005/006/015、SC-001/003/005、C02/F2；依赖T060及各Q实际分支/B输入，人工006 T050/003 T072，含E另T061，采证007 T033。逐Q记录所选配方、对象完整相机序列、各面目标依据/真实测量身份/融合、适用处置、页面取盘及Final；缺E时只记E变体Blocked，不将无E的Q全挡住。不能以22表达检查抵扣22运行。
+- [ ] T062 [US2] 复用正式页面驱动完成Q03—Q22，合法组合可由一运行多个对象分别覆盖完整序列；补Q04-MANUAL和适用Q04-MANUAL-E的C02操作，路径`evidence/q03-q22.md`、`evidence/index.md`及逐run运行包。对应FR-004/005/006/015、SC-001/003/005、C02/F2；依赖T060及各Q实际分支/B输入，人工006 T050/003 T072，含E另T061，采证007 T033。逐Q记录所选配方、对象完整相机序列、各面目标依据/真实测量身份/融合、适用处置、页面取盘及Final；缺E时只记E变体Blocked，不将无E的Q全挡住。不能以22表达检查抵扣22运行。 优先按`specs/008-recipe-driven-inspection/sequences.md`§2跑新版普通OK Q03同run到Final：一次初始3D、两面AB、真实Flip/ACK、合法目标、必要保存、下料、无需搬运依据、解锁观察、实际页面确认；随后其余适用两/四面，原范围不缩减。
 
 > 历史批次（新版不继承执行限制）：第七批限Q03（不改变T060/T062整项条件）：自动换面是唯一候选；先做两面/两轮Test映射校验、预算与受限配置。协议§2.3/§3.1.5及总时序图已有目标面、双重反馈、人工区禁止运动与PLC内部整体翻面取放；只剩取放点提交字段、本轮触发及旧状态清零/关联未明，不能派发Flip或第二面产品运动，也不能勾T060/T062。人工003 T072/006 T050本批不实施。已通过的Q01/Q02/Q01-PARAM能力按具体证据复用，T058状态保持原样。
 

```
