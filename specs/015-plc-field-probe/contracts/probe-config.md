# plc-field-probe/1

## plc-field-probe/2（0.3新增，旧配置不能直接混用）

- `schemaVersion:2`；connection含protocol=ModbusTcp、host/port/unitId、timeoutMs、pollMs；mapping含pcPduBase/plcPduBase（均零基十进制，与MW1000/MW3000对应）、plcArea=HoldingRegister/InputRegister、boolByteOrder=EvenLow/EvenHigh、floatOrder=Abcd/Cdab/Badc/Dcba、confirmed/source。现场值空缺不连接。
- `signals[]`保存id,label,direction,mw,mb,type=BoolByte/Int16/Float32,enabled,writeEnabled,min,max,allowedValues,note。MB是绝对字节地址；BoolByte可共享寄存器但不可共享同字节。PC块全为HoldingRegister；PLC块区域可配置。PDU=块起点+MW-块MW基址。编解码保留原始字和解码时间。
- `writesConfirmed`和`singleWriterConfirmed`只在现场依据成立后设真；会话默认未启用写入，操作者点击启用写入。人工写值仅限已开放PC点，成功仅表示写应答/读回一致。
- `heartbeat`含confirmed/request/response/timeoutMs；仅已确认EchoOnChange，首次及变化应答；停止心跳不得被描述为急停。启用依赖写入开关和应答点许可。
- `axes[]`含name,target/start/feedback/actual，confirmed,min,max,tolerance,unit,frame,timeoutMs,startValue/idleValue/movingValue/doneValue/errorValues。启动0/1、X/Y反馈0/1/2来自点表注释；其他轴反馈码须现场核对，全部confirmed默认false。动作依赖就绪/自动/无故障、有效心跳边沿和空闲触发，只有采到本次moving再done且实际值在容差内才清零，失败保存unknown。
- `field-notes.json`保存PLC型号/程序版本、现场许可范围、测试结论和未决问题；每轮summary由程序记录通信事实，manual写不生成物理通过结论。
- `return-manifest.json`含每文件相对路径/SHA256/大小；导出包含site.json、所有runs、field-notes、源码、基线清单、来源点表及本地改动。导出不自动上传。

独立工具配置合同，不改变正式 Host 接口。

- schemaVersion=1；purpose 为 Field 或 Virtual；sourceReference 是点表版本及现场确认来源。
- host、port、unitId 必填；IP必须明确，Virtual仅回环。intervalMs>=100、timeoutMs在100..3000。
- addressBase 为0或1，address是该基准下的十进制整数；线上偏移=address-addressBase。禁止40001等区前缀混入编号。区由area显式选择。
- signals[]：enabled/name/label/area/type/address/direction；浮点需byteOrder（ABCD/CDAB/BADC/DCBA）。area为Coil/DiscreteInput/HoldingRegister/InputRegister；位区用Bool，字区用UInt16/Int16/Float32。direction为PLC->PC或PC->PLC，仅表明所有权，监视不意味着写入。
- heartbeat：requestSignal/responseSignal/mode/confirmed；mode=EchoOnChange；confirmed=true表示现场已确认点位及同值应答。只有命令行-Heartbeat且配置confirmed=true时允许FC05。两点必须为独立Coil Bool，名称固定PLC_Heartbeat_Req和PC_Heartbeat_Resp，方向分别PLC->PC、PC->PLC。不允许把任意信号重命名后当安全依据；现场仍须核对实际地址。
- 心跳首次/变化后同值写响应点，核FC05回显，再FC01读回；超过3秒无新心跳边沿时停止并报告失败。只读模式对已配置心跳同样显示变化/超时，但不写应答。
- summary只记录观察事实（连接、读写次数、读回次数、变化数、耗时、失败），不生成生产通过结论；config快照/hash关联本轮。中断/强杀后缺summary时以已落盘日志为准，不能视为完成。

## 用户追加：单轴与回传

motion.confirmed=true/sourceReference表示现场核对依据；actions只留本次轴。name/targetSignal/startSignal/feedbackSignal/actualSignal必须匹配当前合同的X/Y/CameraZ/ScanZ/GrabZ/R四元组。min/max/tolerance为有限数值，unit/frame非空，timeoutMs为现场期限。类型/方向/区域正确，目标在范围内且可在容差内编码Float32；工具不推断机械安全性。

- -AllowMotion依赖-Heartbeat；PLC就绪1、自动1、故障0、心跳已变化、启动0才受理。
- 一次一个动作，actionId关联目标FC16、启动FC05及反馈；不写反馈/就绪/复位。
- 新采样0→1后重读实际位置核容差，再清启动并读回；失败保留unfinishedAction，不计actionsCompleted。
- 交互move不阻塞心跳；-ActionName/-Target为已允许的自动单步。不实现组合动作。

field-notes/1保存设备/程序/点表及确认依据、cases(sessionId/result/observed/issue)、remainingUnverified、localCodeChanges和questionsForDevelopment。result为Passed/Failed/NotRun，没证据不写Passed。

Export生成PLC-return-时间-编号.zip，包含指定runs、site.json、field-notes、package-manifest、实际Probe/Export源码及return-manifest。清单包含导出UTC、工具版本、会话、文件大小/SHA256，不自动上传。回收先比对清单/脚本哈希，复核原始收发；现场陈述与软件观察分别记录。
