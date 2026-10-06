# 既有人工换面接口的Test执行接入

当前适用范围（011 T026）：本合同下方描述原协议已批准人工翻面步骤的历史事实。当前recipe-contract/1.3无人工翻面待确认生产者，旧确认线圈及孤立manual-flip接口/服务已删除；不得据此对当前代码要求报文、默认面或续接许可。当前自动翻转/放回与3D复查、真实人工区安全阻断、独立人工取盘与保存/期限/取消规则由011现行合同承接。原历史审批/证据不改写。

## 旧协议历史条款（不作011当前执行或测试前置）


依据协议§3.1.5⑧和U04。版本化Test位置的`resolvedFlipPosition.mode=manual`及`manualWaitMs`明确等待预算；未填mode仍是automatic。先实际定位并结束本次命令，再进入人工等待；占用期间所有运动准入关闭，心跳继续。

只在预先进入人工等待、无在途动作且报警仅人工区域占用位时，把该占用作为预期等待，不能当安全清除。其余报警、停止、断联仍锁定。实际观察ManualZoneOccupied后才开放已有人工完成操作；API按run、实体、步骤、目标面和revision确认，认证主体由Host确定。

收到确认后写已有ManualFlipComplete=1，等待实际占用与报警消除、安全恢复，清ManualFlipComplete=0并读回。记录目标面和采用面、来源ManualConfirmed/CommandDefault；不得称为传感器实测，不发自动Flip目标或借用Flip_OK。清零及必要保存完成后才续检下一面。等待使用冻结整盘期限与显式manualWaitMs；不延长IO、心跳或自动Flip期限。

正式页面绑定006已有人工换面控件；无交互会话时后端证据单列，不算C02页面通过。

### 009 换面业务事实命名对齐（2026-10-02，代码修改前）

本次执行与文档复核者为Codex，不冒称客户或其他人员批准；实现/运行归009 T035/T039/T049，原任务勾选不变。
现有人工/自动完成条件、真实通信、必要保存和期限不变。新的业务ActionFact及StageEvent使用`schemaVersion=device-semantics/1`：自动事实`FaceEstablished`，人工事实`ManualFaceEstablished`。人工含当前flipOperation、实体、步骤、目标面、实际已保存确认、`evidence`语义动作证据及`sensorMeasuredFace=false`；采用面来源仍为CommandDefaultManualConfirmed。此事实表示原占用/认证确认/安全恢复条件已满足后的业务面成立，不复制任何确认位或清零阶段。必要内部握手由通信实现及通信测试检验；业务日志阶段使用ManualFaceEstablishment。
旧`ManualFlipCompletionCleared`及`FlipAckCleared`保留在历史payload/证据原文，不产生同名新事实或授动作。当前使用相关Flip/PutBack及真实3D姿态观察，不再要求已删除的手工/ACK清零报文。对应旧孤立确认接口与无消费分支实际删除，有效关联、安全、保存、取消与期限由现行组件承接；历史失败不改写。

### 010实施定向对齐 A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
