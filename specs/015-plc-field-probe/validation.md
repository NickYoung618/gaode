# 015 开发验证与现场回收

日期：2026-10-05。工具0.2.0。**HardwareTested=false**；没有连接现场PLC，没有真实机构动作。本次使用speckit-specify建立独立规格，现有共享接口/业务代码、来源文档、客户原型和其他规格/任务未修改。

## 已完成的软件验证

- Windows PowerShell **5.1.17763.8755** 实际执行脚本，非仅语法检查。
- `dotnet build VirtualPlc/VirtualPlc.csproj -c Release --no-restore`：成功，0警告/0错误，SDK10.0.401。独立进程仅监听本机，验证后关闭自建进程。

| 项目 | 实际结论 | 证据目录（相对项目根） |
| --- | --- | --- |
| 四种Float32字序、Int16负数、位读取、TCP分片 | 按独立字节常量解码正确，全部为读请求 | artifacts/015-plc-field-probe/20261005-174137/wire-decode |
| Modbus异常02、错误事务号、读取超时 | 拒绝错误响应，终态Failed；原始/部分报文和请求序号保留 | artifacts/015-plc-field-probe/20261005-173824/wire-exception、wire-transaction、wire-timeout |
| 未确认写入、Virtual指向非本机 | TCP会话创建前阻断，核对具体拒绝原因 | artifacts/015-plc-field-probe/20261005-174137/write-not-confirmed、virtual-not-loopback |
| 独立VirtualPlc只读 | 有心跳变化，写入次数0，独立服务写审计为空 | artifacts/015-plc-field-probe/20261005-174350/virtual-readonly、readonly-audit.json |
| 独立VirtualPlc心跳 | PLC翻转→PC同值应答→FC05响应→读回匹配 | 同目录virtual-heartbeat、heartbeat-audit.json |
| PLC未就绪拒绝动作 | 明确PLC_Ready_State不成立，没有目标/启动写入 | 同目录virtual-motion-blocked |
| 单轴X完整闭环 | 目标12.5（仅虚拟数据），一次FC16、启动1、本次运动中0→到位1、实测12.5、启动清零0及读回 | 同目录virtual-motion、motion-audit.json |
| 回传导出 | ZIP成功；配置、源码、清单、现场记录齐全；实际失败会话4个证据文件逐字节一致 | 同目录export-tool、results.json |

当前Probe/Export源码哈希见20261005-174350/source-hashes.json。协议解码/错误响应验证复用对应早先运行；后续只改动作准入心跳时效、终端输入拒绝处理、版本显示及导出程序集，不改变这些已验证读响应逻辑。新增动作准入和导出改动已定向重测，不将失败历史改为成功或重跑无关业务全套。

开发脚本：`python scripts/communication/plc-field-probe/tests/verify_probe.py`；可指定decode/exception/transaction/timeout/admission/virtual/export做受影响复核。开发验证需要Python与当前VirtualPlc构建；**现场运行包不依赖这些开发工具**。

## 开发中发现并修复

- 默认参数中PSScriptRoot取值时机问题，移到参数绑定后初始化。
- 本环境Windows PowerShell无法自动解析Get-FileHash，改为内置.NET SHA256；运行时长从TCP连接后计算，且零次有效读取不能Completed。
- 独立测试夹具2秒空闲截止早于PowerShell首轮启动，修正夹具空闲等待为30秒；没有放宽探针I/O或PLC心跳期限。
- 导出显式加载System.IO.Compression及FileSystem程序集，Windows PowerShell 5.1实际导出通过。
- 已有动作时新的命令只阻断新命令，保留原动作与心跳；派发前再次检查心跳期限。

对应失败目录20261005-173118、173824、174137原样保留。终端交互渲染与现场操作手感待中控机确认；自动化已覆盖同一动作执行函数及实际TCP流程。

## 现场回收及后续Spec Kit更新

1. 现场Codex按包内FIELD-CODEX.md核对TCP/RTU、PLC型号/版本、IP/端口/站号、正式点表/基准/字序。
2. 填site.json和field-notes.json。单轴范围/单位/坐标系/容差/期限及机械工况由现场确认，真实动作须操作员明确允许；不使用虚拟坐标。
3. 顺序验证只读、心跳、已允许的单轴，记录sessionId/actionId、PLC工程端实际观察、失败/未测项。
4. Export生成回传ZIP；开发端先核版本及文件哈希，再逐条核配置快照、报文、反馈与实际观察。
5. 在015追加带来源的现场结论和剩余问题。涉及正式协议定义/共享接口时，先更新受影响spec/contracts/plan/tasks再改代码；不能把探针结果自动变为正式Host或整机验收。

真实PLC尚未验证；Modbus TCP尚待现场确认。初始化/复位、翻面/取放/抓手组合、正式型号承载、相机与算法和生产节拍仍未验。需要哪些追加动作，以现场回传的真实资料和当前主流程需要决定。
