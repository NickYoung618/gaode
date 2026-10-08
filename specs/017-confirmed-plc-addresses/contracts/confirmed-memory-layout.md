> 020阶段B当前实施（2026-10-08）：按用户确认，Alarm_Code明确映射AlarmBits，Alarm_Level映射AlarmSeverity且0无报警；MB6056 Bit0光栅/Bit2门，PLC写PC只读。Model_Number用独立Float32语义，不转旧Words。现场Required集合不依赖退役Teach/ManualZoneOccupied，缺失安全含义返回Unconfirmed并拒绝相关运动；F XY/E扫码Z。[SP-020](../../020-real-device-commissioning/contracts/site-plc-adaptation.md)优先于下述历史未映射说明，软件证据见[验证](../../020-real-device-commissioning/validation-stage-b.md)。

# 确定内存布局与访问合同

本合同为 009/011/014 中已知点位的现场布局增补，以及 015/016 联调模板更新依据，不更改配方动作顺序或前端原型。

- 共享来源：configuration/plc/confirmed-20261006/points.json；layoutId 固定为 confirmed-20261006-v2，携带两份 XLS 的 SHA256。所有现场模板由该表生成。
- memoryByteAddress 是十进制 MB 地址。MW = floor(MB/2)，byteOffset = MB%2。零基 PDU = 指定方向块的 PDU base + floor((MB - 块 MB base)/2)，PC 块 MB2000，PLC 块 MB6000。不得把 MB 本身或带 4x 前缀的展示地址作为 PDU。
- BOOL 使用 BoolByte，int 使用 Int16，real 使用 Float32。INT/REAL 必须偶数 MB 起始。相邻 BOOL 可同字，不能同字节；整字/REAL 不可覆盖 BOOL。字节偏移与线上高低字节通过 EvenLow/EvenHigh 显式选择。
- PC 命令区使用 HoldingRegister；反馈区域由已确认配置决定，工具支持 FC03/FC04，当前正式适配器只支持 HoldingRegister/FC03，其他配置须明确拒绝。不得静默换区。
- PlcPoint 的 DocumentNumber 保留原约定 PDU+1；DocumentAddress 保留原 %MB 文本；ByteOffset 表示该字段在寄存器中的逻辑字节偏移，仅 BoolByte 使用。
- 同一个 PlcDefinitionAdmission 持有共享字节写锁；业务/心跳连接的读原字→改本字节→写字必须在锁内。只保护本程序；现场仍需单上位机写入。
- 读取计划允许相邻字节共享同一个读取字，按字段提取 0/1；异常字节值不解释为真。单独读取一个 BOOL 可包含它的邻字节，但不能据此生成不存在的业务字段。
- 新 Host 参数 Gaode:PlcFieldProfilePath 指向现场 Profile JSON。profile 明确 layoutId、confirmed、source、pcPduBase、plcPduBase、plcArea、boolByteOrder、floatOrder；模板 confirmed=false，其余未知参数不猜测。
- 只映射同名且含义相同的已有 SignalId。Model_Number(REAL) 不转成旧 ModelPayload(Words)，Alarm_Code 不擅自转成 AlarmBits；额外状态保留在共享点表/工具中。缺失业务字段仍由正式 Definition 校验列出并阻断动作。
- 字节和状态元信息进入现有结构化通信证据；工具配置快照记录布局标识和原件摘要。回传需携带源版本、配置、TX/RX 和动作阶段。
