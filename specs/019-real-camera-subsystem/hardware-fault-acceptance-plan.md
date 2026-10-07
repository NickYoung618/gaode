# 019实机故障验收执行方案（待用户确认，尚未执行）

## 设备与隔离

仅使用Galaxy A：序列号GBZ26080956，物理网卡MAC A0-14-6D-01-36-34。本轮只读Windows网卡检查显示该MAC当前名称为B，不能按名称A选择。历史有效绑定cameraIp=192.168.3.11、hostIp=192.168.3.20，仅供准备，启动后必须以SDK实际绑定重新核实，不强填地址。其他六台不启动worker，不改网卡配置，不触碰MAC A0-14-6D-01-36-3B的PLC网口。

使用原修复包a0e70e3、全新根D:\gaode\artifacts\camera-hardware-fault-20261007-r1、独立SQLite、127.0.0.1:5198。准备单设备site子集，只从现场site复制A的Serial/ExpectedNicMac，不修改原site。不使用已封存的两个数据根。启动命令如下（准备site文件后，待确认执行）：

```powershell
& D:\gaode\artifacts\gaode-camera-019-review-a0e70e3\Start-CameraSubsystem.ps1 -SitePath D:\gaode\artifacts\camera-hardware-fault-20261007-r1\site-a.json -DataRoot D:\gaode\artifacts\camera-hardware-fault-20261007-r1 -Port 5198 -PrepareStore
```

仍为CaptureOnly，不组成PLC/算法/外部光源。成像参数不写入，厂家内置投射不涉及本台2D。

## 开始前必须具备的参数与基线

确认无其他Host/厂家调试工具占用A，检查SDK绑定Serial/MAC/IP及唯一物理NIC一致。保留本会话parameters-before.json、imaging-before.json、galaxy-before.txt、settings-to-restore.json，分别登记摘要；galaxy-before.txt只诊断备份，不整份导入。检查备份覆盖AcquisitionMode、TriggerSelector及FrameStart上下文的TriggerMode/TriggerSource。所有必要软件触发设置已经读回。

默认StartupTimeoutMs=30000、CaptureTimeoutMs=30000、ShutdownTimeoutMs=15000，API总预算60秒；不修改为1ms。Galaxy正式驱动GetImage(10000)。先正常采集一帧，核对新帧、存储/索引/摘要，记录PID/session/openCount、媒体数量和原参数备份。保存故障前原参数的独立副本，不能让恢复后新会话备份覆盖原值。

## 场景H1：真实SDK超时

目标是实际厂家SDK调用超时，不是单纯缩短Host期限。本方案在已Ready的A上短暂阻断相机至主机的UDP回包（含控制/图像），只作用于该相机/主机IP、物理网卡及正式worker程序。它可能使控制读取先超时，不限定一定进入GetImage；只有SDK异常明确为超时并有调用栈/实际耗时，才计作SDK超时通过。设备立即报断连、Host预算先到、或过滤不生效均不得冒充SDK超时。

确认实际SDK地址仍为上述地址、按MAC解析出的唯一网卡仍为B后，待批准的故障注入命令是：

```powershell
New-NetFirewallRule -Name 'Gaode019-A-Timeout-r1' -DisplayName 'Gaode019 A timeout acceptance r1' -Direction Inbound -Action Block -Protocol UDP -RemoteAddress 192.168.3.11 -LocalAddress 192.168.3.20 -InterfaceAlias 'B' -Program 'D:\gaode\artifacts\gaode-camera-019-review-a0e70e3\worker\Gaode.CameraWorker.exe' -Profile Any
```

不调整或停用现有防火墙规则。若实际绑定地址/网卡名称不同，暂停并展示更新后的具体命令，不能沿用历史地址。注入前确认同名规则不存在，取得新增规则对象；仅删除该对象对应规则。该规则位于主机，不能写相机永久配置；须具备管理员权限。不通过替代命令绕过系统拒绝。若驱动绕过Windows防火墙导致正常帧返回，记录“注入未生效”，清理规则并停止本场景，不追加其它注入方式。

新增规则后仅POST一次/api/v1/cameras/A/captures（Operator头从本根读取，不输出令牌）。收集HTTP状态/正文/耗时、worker SDK异常及栈、gateway状态；确认未知结果不保存/不发布、无第二次capture或自动重建。HTTP可能按现有错误映射返回500，若Host预算先到可能504；不能把HTTP代码本身当作SDK超时证明。

注入和请求必须由try/finally包围，完成或失败立即执行以下清理；另设45秒清理看护，不能依赖采集返回才清理。请求最多等待40秒；清理看护仅清除本次规则，不触发相机、不自动恢复。

```powershell
Get-NetFirewallRule -Name 'Gaode019-A-Timeout-r1' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
```

确认规则消失、物理链接正常，再进入下述恢复门禁。H1清理和参数恢复验证通过后才进入H2。

## 场景H2：物理断线

重新建立明确Ready的新会话并保存本场景开始前备份。由现场人员按MAC A0-14-6D-01-36-34定位A的数据网线；当前Windows名称B仅辅助。仅拔此数据线，保持相机供电，不拔其他网口，不以Disable-NetAdapter替代物理断线。记录拔线时间；POST一次A采集，保留HTTP失败/SDK错误/耗时及Faulted状态，再次POST应被Ready门禁拒绝且不再发触发。

仅在记录故障后把同一网线插回同一端口。确认该MAC链接恢复、原IP绑定不变。不得因恢复链接自动恢复或重拍。H2不保证产生SDK超时，错误立即返回也可证明物理断线，但不能填补H1证据。

## 两场景共用恢复门禁

1. 故障前原参数备份独立保存；清除临时规则/恢复物理线缆。检查原worker是否退出及restoration.json，不把Stopped_ParameterRestorationUnconfirmed当作恢复成功。
2. 先通过shutdown正常停止Host并等待worker退出。SDK故障可能使旧句柄无法恢复触发参数；若restored不为true或读回不完整，暂停，不直接启动新worker把临时Software设置当作原值。
3. 必要时由现场人员在厂家Galaxy工具中按Serial及物理NIC核实设备，读取当前参数，与故障前备份比较；仅恢复AcquisitionMode、对应TriggerSelector上下文的TriggerMode/TriggerSource，再恢复原Selector。写后读回。不写Exposure/Gain/Width/Height/PixelFormat，不导入整份配置、不保存UserSet/永久配置。若厂家工具不能明确选择正确设备或读回不符，停止并报告，不宣称恢复。
4. 原触发参数及成像参数读回核实后，重新启动本场景Host（去掉PrepareStore）作为人工明确恢复操作，确认新session。另在仍可安全使用旧句柄的情形才允许显式recover；均不重放原未知请求。
5. 仅发送一个具有新身份的采集请求，核对新帧、提交、索引与读取；它是恢复验收新请求，不能作为旧请求补拍成功。最后正常shutdown并核对参数恢复。自动曝光/增益若发生变化须照实记录，不能写回伪造成像未变。

## 证据、退出与审批

每场景保留：执行命令及审批结果、序列号/MAC/实际IP、原参数及摘要、时间线、HTTP结果、SDK异常/调用栈、触发次数、状态/会话、文件/索引数量、清理及恢复读回。停止后冻结，关联a0e70e3运行DLL及原ZIP摘要；不改旧21份正常实机结论。

本文件仅方案，不包含硬件执行证据。用户确认后才启动A或创建防火墙规则，拔线需现场人员确认操作。任何系统策略拒绝完整保存可见返回信息，立即停止对应动作，不推测原因、不改写命令。若H1未产生真实SDK超时或任一恢复门禁不满足，019仍未最终收敛。
