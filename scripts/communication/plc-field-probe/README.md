# PLC 现场通信探针 0.2.0

把本目录复制到 Windows 中控机可写目录即可使用。依赖 Windows 自带 PowerShell 5.1，不用安装项目、Python、.NET SDK或第三方Modbus软件。不需要管理员权限。CMD只对本次脚本进程设置执行策略，不修改系统策略。

功能：只读信号表、变化时间线、原始报文、心跳应答，以及人工逐次发起单轴运动。默认只读，默认执行60秒；Start.cmd监视5分钟。工具没有自动整机流程，不承诺替代正式上位机最终联调。

## 先让现场 Codex 准备配置

把下面这段发给中控机的 Codex：

> 请读取本目录 FIELD-CODEX.md 和 README.md，按现场已确认资料生成 site.json 和 field-notes.json。先核实是否 Modbus TCP、PLC IP/端口/站号及真实点表。不要使用 virtual.example.json 的地址或坐标驱动真实 PLC。按只读、心跳、人工单轴顺序协助测试，保留失败日志，最后用 Export.ps1 生成回传包。涉及真实动作时逐项取得操作员对轴、目标位置及设备就绪的确认。

1. 复制 site.template.json 为 site.json；复制 field-notes.template.json 为 field-notes.json。保留已经填写的现场文件。
2. 填入 host/port/unitId/sourceReference。项目采用Modbus TCP；只知道“Modbus”不能确认是TCP还是串口RTU。若现场是RTU，本工具不适用，回传设备接口信息。
3. 明确 addressBase：PDU零基地址填0；文档确实为一基点号才填1。address用十进制、只写点号，不带0x/4x区前缀；数据区由area指定。不要自动把厂家D/M地址或40001换算为Modbus偏移。
4. 仅启用已有正式地址的信号（enabled=true），其他点保持disabled。Float32必须核实ABCD/CDAB/BADC/DCBA字序，可对照PLC监控中的已知实际位置；不得通过试写运动目标寻找字序。模板中的名称/类型来源于项目，现场仍需核实。

**当前项目 Gaode.Plc.Protocol/Signals.cs 是测试映射，正式地址仍缺。Word中的示例地址也不直接当正式点表。** virtual.example.json只能连接本机VirtualPlc，不可改成Field后当现场配置。

## 1. 只读监视

双击 Start.cmd，或在本目录打开 PowerShell：

```powershell
.\Probe.ps1 -Config .\site.json -DurationSeconds 300
```

屏幕显示方向、名称、中文说明、数据区/PDU偏移、原始值、解码值及采样时间；下方显示最近交互。Q结束。无交互终端可加-NoDashboard，输出追加日志。

只读模式不写心跳、不就绪使能。若PLC需要PC应答，它可能按自身程序报失联；这和TCP/Modbus读取成功是不同事实。工具检测到已配置的PLC心跳3秒不变化会报告失败并保存日志。可先只启用其他已确认状态点排查读取，不把未测试心跳写为通过。

## 2. 心跳收发

核对PLC_Heartbeat_Req和PC_Heartbeat_Resp真实地址，确认现场采用“首次及变化后回写相同位值”；两点均enabled，heartbeat.confirmed=true。保证无其他上位机同时写这些点，再执行：

```powershell
.\Probe.ps1 -Config .\site.json -Heartbeat -DurationSeconds 300
```

日志分别显示 `Signal`（PLC心跳变化）→ `HeartbeatTX`（PC应答）→ `WriteResponse`（PLC写响应）→ `Readback`（读回一致）。这是心跳通信闭环，不是设备动作完成。

## 3. 人工单轴动作

允许X、Y、CameraZ（检测Z）、ScanZ（扫码Z）、GrabZ（抓取Z）、R的已确认映射。删除motion.actions里本次不测的条目，填写保留轴的min/max/tolerance/unit/frame/timeoutMs，补齐相应目标/触发/到位/实际位置点并启用。motion.sourceReference写现场依据，确认后才设motion.confirmed=true。所有运动坐标必须由现场提供，虚拟配置的范围不可复用。

工具要求PLC_Ready_State=1、PLC_Mode_Auto=1、PLC_System_Fault=0、心跳已变化、该轴启动信号=0。PLC就绪如何成立由现场现有初始化流程处理；本版不擅自写PC_System_Ready、不复位和清除PLC报警。若现场需要PC初始化才能就绪，请回传确切握手，不能跳过准入。实际机械空间、夹持和互锁需现场确认。

```powershell
.\Probe.ps1 -Config .\site.json -Heartbeat -AllowMotion -DurationSeconds 1800
```

启动后输入 `move 轴名 现场确认的目标数值` 并回车。输入期间心跳继续运行。比如轴名可为X；这里不给真实目标数值。一次一个轴，完成后再输入下一条。`quit`在没有执行中动作时结束。

每步依次记录：命令受理 → FC16写目标 → FC05置启动 → 本次反馈0（运动中）→ 1（到位）→ 重新读实际位置并核容差 → 启动清零并读回。反馈2、错误码、超时或实测不符明确失败。不能把旧到位1当本次成功；如果动作太快而轮询没看见0，本版不会假定完成。

现场Codex需要以非交互命令执行一条已由操作员确认的动作时，可使用 `-ActionName 轴名 -Target 目标数值`，同时必须带 `-Heartbeat -AllowMotion`；它会在条件成立后自动执行一次。未确认前只准备命令，不执行。

**工具停止或异常退出会停止心跳。** 它不是急停按钮；有动作未完成时没有自动重试/自动清触发，现场按PLC实际处置流程处理。断联时保留最后样本并标明停止，不将旧值当当前值。

本版不触发翻面、取放、抓手切换或连续配方。它们还依赖型号承载、安全位置及完整步骤，不能靠随意写一个寄存器代替。分别把相关点作为只读观察项加入即可；需要主动测试时回传已确认握手后扩展。

## 结果与回传

每轮保存在runs/唯一目录：

| 文件 | 用途 |
| --- | --- |
| config.snapshot.json | 本轮实际配置，启动后修改site.json不会改变本轮 |
| events.jsonl | 级别/分类/会话/信号/动作ID、变化和失败 |
| wire.jsonl | 每次TX/RX/ERROR、事务号、请求上下文、原始十六进制、耗时 |
| summary.json | 统计、终态、未完成动作、配置SHA256 |

Completed表示本次观察时段或已记录单轴测试结束，不是整机通过。强杀/关窗可能只有已刷新日志而没有summary，应记录为中断。轮询不能保证捕获所有短脉冲；请求响应耗时包含本工具和系统调度开销，不是纯PLC扫描周期。

填写field-notes.json中的现场程序版本、参数确认依据、各测试sessionId、操作员实际观察、未测项和问题。双击Export.cmd，或运行：

```powershell
.\Export.ps1
```

生成PLC-return-日期-编号.zip，含所有runs（包括失败）、现场配置/记录、程序与哈希清单，不自动上传。把ZIP回传给开发端即可。文件只包含本工具指定的内容，不收集业务数据库、相机图像或其他目录。多轮记录很大时可用-SessionDirectory指定一个完整会话目录，另在field-notes注明范围。

开发端将依据回传确认真实映射/字序/握手差异，更新015验证记录；如需改正式共享通信定义，再先更新受影响spec/contracts/plan/tasks后改代码。正式Host与此探针不是同一实现，仍需做一次正式软件连真机的代表链验证。
