# 关键时序

**版本**：1.1.0　**日期**：2026-09-26　**状态**：派生软件设计时序，不是原始设备时序图或运行记录。008正式虚拟链使用外部VirtualPlc、实际采集/独立worker和存储；其他适配仅在其声明环境内使用。每个长操作都先注册期限/终态容量，投递后返回，Coordinator不等待完整操作。

图中省略重复的容量预约与Capture意图提交；每次采集仍须按保存合同先保存触发关联，再调用采集端口，不能以图示简写绕过保存门。

## 1. 正常公共流程

```mermaid
sequenceDiagram
  participant A as 正式入口
  participant W as 共同准备与F绑定
  participant S as 实际保存
  participant P as 设备端口
  participant C as 采集算法端口
  A->>W: 人工上料确认启动
  W->>S: 请求关联与公共配置意图
  S-->>W: 实际提交
  W->>P: 必要公共准备与3D位置
  P-->>W: 本次可靠反馈
  W->>C: 初次3D实际采集与调用
  C-->>W: 有无、姿态、F绝对XY及有效性
  W->>S: 观察/采集/调用事实
  alt 有效F定位与必要保存成立
    W->>P: 本次F XY及扫码Z
    P-->>W: 独立轴到位与实际位置
    W->>C: F单拍并实际解码
    C-->>W: 料盘编号或问题
    W->>W: 唯一匹配已保存配方
    W->>S: 冻结快照与绑定/移交
    S-->>W: 实际提交
    W-->>A: 同run继续产品执行
  else 定位/匹配/保存不成立
    W->>S: 失败原因与已发生事实
    W-->>A: 对应依赖动作受限
  end
```

F内容就是料盘编号，不同配方不能占同一码；保存并校验成功的新内容供后续F，已冻结运行保持原内容。公共准备不依赖未绑定配方。图中省略内部通信步骤，不能将地址、原码、ASCII或内部ACK放入业务DTO。001不重复实现产品检测/翻转/分拣。

## 2. 算法期限及迟到

```mermaid
sequenceDiagram
  participant W as Coordinator
  participant T as 期限仲裁
  participant G as 高度或读码端口
  participant S as 保存
  W->>T: 登记Call起点和冻结预算
  W->>S: AlgorithmIntent（Call/关联/媒体/版本/依据）
  S-->>W: Committed
  Note over W,T: 保存中到期则TimedOut PreDispatch，不再Execute
  W->>G: Committed且原期限有效才投递Call
  G-->>W: Accepted/Running
  T-->>W: 到期，唯一TimedOut事实
  W->>S: 超时终态、原采集及继续依据
  W->>G: 请求取消，后台隔离/回收
  S-->>W: Committed
  alt 3D观察调用
    Note over W: 无有效本次F定位不派发F移动；保存原因、有限结束，不猜固定点
  else F调用
    Note over W: 保存F失败和机械收敛事实 不放行产品
  end
  G-->>T: 超期结果或重复结果
  T-->>W: LateEvidence，原终态已关闭
  W->>S: 原Call迟到证据，不改移交
```

意图保存失败/CommitUnknown时禁止投递Call；原算法窗口仍有限收敛，保存门未满足不能推进F或移交。晚Committed不会重置期限。意图提交前、提交后未派发、派发后结果未保存三窗口按保存合同§1.1核对；已提交意图却无Dispatch记录时不假定未发出。

Worker退出与媒体释放不由“超时”推断。实际时间和可控时钟均由T触发，不能由G直接设置运行超时。

## 3. 动作完成未知

```mermaid
sequenceDiagram
  participant W as Coordinator
  participant M as Motion
  participant D as PLC或模拟设备
  participant T as 期限仲裁
  W->>M: 已保存意图的适用固定XYZ
  M->>D: 命令
  D-->>M: Accepted
  T-->>W: 当前运动总期限到达，无可靠完成
  W->>M: 关闭准入，标Unknown/Held，请求受控停止
  M->>D: Stop（已确认契约）
  Note over W: 禁止采集及依赖动作，查询/心跳继续
  D-->>M: 迟到到位或停止观察
  M-->>W: 原动作的补充事实
  Note over W: 迟到事实仅补旧故障证据，故障continue拒绝；按第5节双端复位后显式新轮
```

若停止不响应仍Unknown；设备内部移动结束但回执丢失也不能自动完成工位。

## 4. 必要保存失败

```mermaid
sequenceDiagram
  participant W as Coordinator
  participant S as 单写通道
  participant D as 设备
  W->>S: 动作意图WriteId
  alt 意图未提交
    S-->>W: Failed或CommitUnknown
    Note over W,D: 不投递生产动作，允许诊断/停止处理
  else 意图已提交
    S-->>W: Committed
    W->>D: 动作
    D-->>W: 匹配完成事实
    W->>S: 完成事实及必要媒体/结果
    S-->>W: Failed或CommitUnknown
    Note over W: 保留已观察与未保存区别，不发依赖步骤/不移交
    W->>S: 恢复后按同WriteId核对提交
    Note over W,D: 不用再次动作补记录，核对保存不自动恢复运动
  end
```

## 5. 正常暂停核对与故障旧轮收束

操作分界以[003恢复合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md)为准。客户端断线只影响响应传输；重连先查询后端实际状态，不能把断线直接判为设备故障或把运行中的流程当作Paused。Host崩溃/故障后重建的是历史查询及受限状态，不恢复旧故障步骤。

```mermaid
sequenceDiagram
  participant A as 授权API调用者
  participant W as Coordinator
  participant S as 持久记录与媒体
  participant D as 设备状态端口
  W->>S: 读取原run及Run/Handoff/WriteId和取消事实
  S-->>W: 已提交状态及未决保存信息
  alt 正常暂停 原run真实Paused且未发生设备或保存故障
    A->>W: recovery-checks 同盘装载及快照依据
    W->>D: 核对实际停止 当前安全和物理状态
    D-->>W: 可靠观察或不足
    W->>S: 核对必要保存及原run可继续条件并保存Check
    S-->>W: Committed或未确认
    A->>W: continue 携CheckId和Revision
    W->>D: 再检查动态安全与原物料快照适用性
    alt Check已提交且有效 所有准入成立且未取消未完成
      Note over W: 同run保留已保存事实和原期限 继续未开展步骤 不重采公共3D/F
    else 条件不足或核对失效
      W-->>A: 明确受限 不派发依赖动作
    end
  else 故障或Host崩溃中断的旧轮
    Note over W: 关闭旧轮派发及continue资格 保持心跳和停止复位通路
    W->>D: 只观察原动作及连接代次 未知保持UnknownHeld
    W->>S: 按原WriteId核实提交 保存旧故障及已发生事实
    Note over W,S: 未终结原Call记录Interrupted或迟到依据 旧图片结果日志保留 不重算旧步骤
    S-->>W: 已提交或仍CommitUnknown
    W-->>A: 旧轮受限及原因 故障continue返回FaultRequiresNewRun
    Note over W,D: 转003恢复合同及008新轮图 实际收束释放 双端复位 真实初始核验
    Note over A,W: 仅条件成立后显式新启动新request/command/run 完整重新公共3D/F及绑定
  else 已终态或取消尚未裁决
    Note over W: 终态只读 未决取消保持关闭准入 不通过continue复活
  end
```

故障后续只引用[008完整新轮时序](../008-recipe-driven-inspection/sequences.md#usr-20260926-d派生软件设计故障双端复位后完整新轮)，不在本图另建恢复接口。原WriteId核对只解决旧事实保存，不授权原故障轮运动；已知未派发也不构成例外。人工换面按既有manual-flip-confirmations、占用与确认清零及安全/保存条件同run继续，采用命令目标面并标明来源；不走故障新轮，也在完成放回后复查3D姿态，F不重绑。请求幂等及合法局部重试不变。

以上路径共同断言：当前正常盘F单次触发；公共3D使用检测Z、F使用扫码Z及各自复位；F失败或保存未确认不放行产品。001只提交公共事实和非终态移交，F唯一绑定及冻结按共享职责完成后由008续接；产品翻面/旋转/分拣/下料由对应所有者执行。不能以001公共完成为Final。


## 6. 取消与移交交叉提交

```mermaid
sequenceDiagram
  participant A as API
  participant W as Coordinator与Motion
  participant S as 单写条件事务
  W->>S: Completion候选 WriteH及ExpectedRevision/None
  A->>W: cancel
  Note over W: 立即锁存取消并关新准入，所需停止不等DB
  W-->>A: 202，Applied=null，Pending或CommitUnknown
  alt WriteH已提交（含回执丢失）
    W->>S: 按WriteH核对Run和Handoff
    S-->>W: 同一持久Completed与Handoff
    W-->>A: 保留完成，cancel NotApplied
  else WriteH结果未知
    Note over W: 不提交竞争终态，不提前宣布Cancelled/Ready
    W->>S: 核对原WriteH及在途写者
    S-->>W: 明确提交/回滚/条件拒绝后再裁决
  else 已确认无完成提交且取消条件满足
    Note over W: 可靠停止或无需停止核对，必要记录已保存
    W->>S: Cancel候选 WriteC及当前Revision/None
    S-->>W: 条件事务提交Cancelled或拒绝并返回实际终态
    W-->>A: 依据持久终态投影，取消胜出才Applied=true
    W->>S: 旧WriteH到达
    S-->>W: Cancelled下ConditionRejected，不插Handoff
  end
  Note over W,S: 重启核对两类WriteId、Run/Handoff/命令，未知保持受限
```

完成候选排队未提交不等于已完成；取消请求也不等于最终取消。两个有效候选以第一个实际条件提交为准；完成提交与Run/Handoff同事务，取消提交与最终取消命令结果同事务。仅Run revision变化时重新核对条件，不能把版本冲突当作已取消；CommitUnknown必须先核对，不能盲重投。五个窗口的停止、API展示和重启行为逐项见[保存合同§1.2](contracts/persistence-handoff.md)。

## 7. 修订记录

- 2026-09-20，1.0.1：H01在正常/超时时序加入调用意图提交门和恢复三窗口；H02加入取消/完成条件事务与回执交叉。M1只覆盖正常序列，完整取消/恢复为M2必做。图为设计，尚未执行或渲染验证。

## 8. 当前适用依据与门禁

本轮修正原1.0.1的XY-only、无Z、停在配方前及异常即放行描述；算法期限/动作未知/必要保存/取消竞争图继续适用，不新增恢复业务。公共3D固定XYZ与检测Z沿用[002 FR05用户确认记录](../002-plc-xyz-recipes/spec.md)；公共3D的1/2复位沿用[003既有验收链记录](../003-plc-latest-protocol/differences.md)，不是从当前代码反推。F及检测详细清零按分区协议§3.1.7。原图的“仅XY无Z”与此确有差异，见[来源追溯](../008-recipe-driven-inspection/sequence-alignment-20260926.md)。

Inspection、XY、Z、ZReset为图中信号简称，方向/地址见[008信号表与后继时序](../008-recipe-driven-inspection/sequences.md)。反馈属于当前operation和连接代次，PLC没有软件operationId寄存器；下一有效移动受理清旧反馈。任何到位/复位失败、动作未知或必要保存失败均阻断依赖动作。3D失败不能造零高度；只有F独立目标和安全/复位门禁成立才可收敛到F，后继产品还须各面合法Z依据。

2026-09-26，1.1.0：本轮仅补派生时序与现行边界，任务归specs/001-station01-public-preparation T090和specs/003-plc-latest-protocol T067/T070；渲染与保护检查见008本轮记录。

## USR-20260926-D公共准备边界

故障后的全链派生软件时序见[008故障双端复位新轮](../008-recipe-driven-inspection/sequences.md#usr-20260926-d派生软件设计故障双端复位后完整新轮)。新run重新StartClamp、公共3D/F及移交，不消费旧测量/扫码/完成记录。前文同WriteId核实提交属于旧事实保存，不代表故障从旧步骤续跑；正常暂停同run受控继续仍适用。原图及历史证据不变。
