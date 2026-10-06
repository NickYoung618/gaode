# Bug Assessment: 原信号列表保留完整XYZ同值记录
- Slug: monitor-xyz-history（用户显式指定）
- Source: pasted text，2026-09-27最新用户纠正
- Verdict: valid
- Severity: medium

## Report
删除“动作发送与反馈 · 完整 XYZ”独立栏目，在原最近数值变化列表逐信号显示每次真实XYZ发送和实际反馈，同值及0保留。恢复协议XY_Move_Cmd/XY_Pos_Confirmed名称。本次确认取代之前独立栏目及XYZ公开命名要求；来源Word/历史运行不修改。

## Symptom / Reproduction
原列表由changes消费，PlcDataStore.RecordChange在previous==current时返回；用户截图在扫码命令5前只见X/Z。app.js把完整audit映射到单独motionList而非原historyList。因此原列表仍无法逐条展示同值Y。代码已直接证实，不依赖用户重跑。

## Suspected Code Paths
VirtualPlc/wwwroot/app.js:updateChanges/readMotionAudit/renderHistory及旧DOM版本检查；index.html独立栏目；styles.css独立栏目样式；PlcAddressMap.cs公开名称；scripts/tests/virtual-plc-monitor.test.cjs和打包入口。

## Root Cause Hypothesis
高置信度：显示消费者选取与布局不符合本次确认，而非本轮证据证明业务漏发。audit/2.0包含完整rawWords、连接、事务、写入时间与sequence，动作包含generation/actionSequence/phase、writeSequenceRefs和当时actual。足以投影原列表，不改PLC命令或业务。

## Proposed Remediation
先同步003/007/008相关spec/contracts/plan/tasks当前条款，旧命名确认标为历史。原列表融合：非坐标changes保留状态与清零；Move/Sort坐标从audit生成逐轴发送/实际反馈记录，避免同一坐标同时作为changes和audit重复显示；ZReset的真实Z反馈从其动作记录保留；特殊HTTP Test路径保留实际请求/结果并明确无Modbus地址。记录时间使用本次写入时间/动作采样时间，未知发送时间不以反馈时间伪造。发送只允许本动作引用的同连接本次命令前收据，并拒绝跨上一运动命令的旧轴引用；缺证据标明缺少。保留有界历史、游标、缺口与错误分类，按动作阶段和轴去重。删除motion DOM、样式及依赖其存在的检查，改为原列表版本标识。

Files: VirtualPlc/wwwroot/{app.js,index.html,styles.css}, VirtualPlc/PlcAddressMap.cs, scripts/tests/virtual-plc-monitor.test.cjs, packaging/windows-local-20260927/{Start.ps1,README.md,build.py}；003/007/008直接相关设计与证据入口。若现有原始证据无法涵盖0/全同值，用明确标记的受控离线夹具，不伪称真实设备实跑。

## Tests
原列表同Y、同Z、连续全同XYZ、0、ScanZ/DetectionZ/GrabZ、Move及Sort取放；缺轴、跨连接/旧命令不拼接；重复轮询/清空不回灌、有限缓存缺口。保留HTTP、页面渲染、心跳状态错误分类。重放旧原始证据并真实Edge截图。当前源码构建VirtualPlc，独立新包比对资源/二进制；旧业务run仅按不变组件复用。

## Risks / Open Questions
audit反馈为虚拟设备本次动作实际采样，不冒称真实PLC实测或每次Host网络读回。旧证据名称保留，重放页面根据稳定地址使用当前XY公开名。无新增业务规则，无阻塞性待确认项。008整体验收不在本次范围。

本轮产物目录：20260927T130246460967Z
