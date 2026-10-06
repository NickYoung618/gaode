# 补充缺陷评估：T065通信异常延迟（2026-09-27）

- Slug: t065-communication-delay（用户指定，继续已有缺陷）
- 日期: 2026-09-27 09:22—09:35，Asia/Shanghai；原始UTC以证据文件为准
- Verdict: valid
- Severity: high
- 结果: **BlockedBeforeObservation；根因未确定；当前不能确定业务补丁**
- 来源: 本轮用户执行要求、AGENTS、宪章7.0.0、原assessment、已有诊断与代码。原assessment.md保留；本轮仅在本缺陷目录新增补充记录、证据与待执行采样工具，不运行bug-fix，不改任务勾选。

## 当前交付和限制

completion-review.md最终交付节与task-audit-night-20260927.md最终状态优先于早期r8记录：008 T049—T070共22项，已完成20项；T055/T070未勾，003 T065未勾。选定Test主流程、持久结果/处置、人工、旋转、成组/整体、故障旧媒体及完整新轮已有正式证据；不重开发或为定位通信再次跑完整配方。r22 current-primary-summary记录故障run18de5e0b…、新run f8f791a2…及23项操作检查，只在其构建/Test范围有效。

Test I/O缓解及后续成功不证明默认模式或生产根因解决。真实点位/高度标定、取放可靠采样窗口、布局容量、特殊机构生产映射、相机SDK和算法精度/现场节拍依既有局部来源待办；这些不是本次TCP/调度补证的前置条件，不阻塞已验收Test结论。质量清单15/16保持。

当前控制文件current-night-batch仍指历史r17；recovery-request.originalRoot及r22 ready指向r22。live-state.json单独核验PID9428及Session；不以旧指针/ready替代存活事实。本轮未向桌面worker发送作业、停止、恢复或接班命令。

## 本次权限核验和观察结果

当前实际身份10_3_0_13\codexsandboxonline，Medium Mandatory Level、Users组，whoami.exe /all只显示SeChangeNotifyPrivilege和SeIncreaseWorkingSetPrivilege，不是提升管理员会话。文件系统完全访问不意味着系统性能采样权限。

本次在确认WPR无录制后，仅尝试一次wpr -start Network -filemode：UTC 2026-09-27T01:22:48.3666158Z，exitCode -984068079，错误0xc5585011 / Failed to enable the policy to profile system performance。后续WPR仍未录制，MSDTC_TRACE_SESSION保持，未执行cancel/stop、未修改策略/安装工具/借用桌面任务提权。Host/VirtualPlc通信观察启动数0，未产生本轮事务或ETL；**不是120秒未复现**。当前权限条件与前次005851382Z诊断一致，无需再次空转。

旧Network profile含TCPIP、CSwitch、ReadyThread及ProcessThread，仅证明候选配置；尚未证实实际ETL回环TCP覆盖、线程等待/就绪字段、丢失统计和关联质量。准备脚本使用唯一WPR instance，不会停止其他实例；现有录制或独立端口占用时拒绝开始。

## 已重新证实的应用事实

原诊断报告引用的16个manifest条目全部逐字节SHA匹配，见prior-evidence-integrity.json。另独立读回r15/r17已保存通信窗口，按**两端端点+事务**匹配，不把不同连接的相同事务号合并；各得到双端2条关键记录。原7/10窗口parseErrors均空，droppedWindows均0，但256环形覆盖不是全量系统记录。

| 样本 | 精确关联 | 已测长空档 | 已测短段 | 能排除/不能排除 |
|---|---|---|---|---|
| r15 | 58574↔25164，事务78 | PLC responseSentUtc 20:00:14.9550229Z→Host headerReadUtc 20:00:16.4335785Z，1478.5556ms | 门锁0.0029ms，已有连接，写0.0537ms，PLC处理/写回应0.0244ms | 长等待不在已测门锁或PLC处理；不能区分内核送达/回调/读取完成调度 |
| r17 | 60123↔25154，事务9478 | Host requestWrittenUtc 20:15:49.799033Z→PLC headerReadUtc 20:15:54.5024904Z，4703.4574ms | 门锁0.0022ms，已有连接，写0.0514ms，PLC处理/回应0.0133ms | 不能把PLC晚读完当作TCP晚送达；Host约1000.5ms已失败 |

使用UTC比较应用观察段；跨进程tick未核验统一时基不能直接相减。Read/Write完成不是内核到达。旧3516.2ms有效心跳空档缺分段日志的事实保留，不能用新样本补造历史。

## 根因假设和仍缺证据

1. **接收线程/socket续体调度延迟：中等可信候选，未证实。** 两样本均已进入异步读取；若系统已及时收包，而线程长时间Ready未运行或完成回调晚，才支持定位相应接收侧调度。现有最低线程修正/Host inline仍有后续失败，不能直接再加线程或全局改优先级。
2. **系统TCP/VM传输送达延迟：仍可能。** 必须有同连接系统send/receive/ACK时间线才能定位；应用短写不代表字节已及时到达对端。无证据批准系统网络/安全软件/虚拟机策略补丁。
3. **PLC请求处理慢：对这两个已关联事务支持弱。** 处理段分别0.0244/0.0133ms，长等待在该段之前或之后；不能外推所有请求或HTTP路径。
4. **GC/线程池/机器CPU资源压力：未排除，也未证实。** 快照队列0、累计GC短、一次inline成功都不能解释完整失败窗口。需要调度事件和调用栈，不盲改GC/CPU亲和性。

缺口：TCPIP回环事件是否实际存在、字节/方向与MBAP事务映射（事件无payload时用端点/时间/序号明确置信度，不能假称系统事件自带事务号）；接收线程ID/ReadyThread/CSwitch/等待与socket完成关联；有效echo接受而非仅写ACK；ETL丢事件及覆盖范围。首次超期后5秒窗口不得被误作修复后对照。

## r22 HTTP超期独立核查

r22 job001 run3955ced1-bb60-4c67-a3c3-e8886d58122a，恢复POST traceId 0HNOS2KK17424:0000003D，堆栈落LatestProtocolPlcDevice.ReadInitialStateAsync第140行：新HttpClient(IoTimeoutMs=1000)调用GET /api/simulator/special-actions/state。外层RuntimeHttp elapsedMs=1429.3363，失败为HttpClient.Timeout=1s。日志结束status=200、responseStarted=False不是成功响应证据。

VirtualPlc/Program.cs第47行路由调用TestSpecialActions.cs:GetTestSpecialState；该方法只在_gate锁内读取机构字段和specialTask.IsCompleted，没有await等待specialTask结束。源码不能证明锁竞争或HTTP处理很快；PLC日志中无该路径请求记录，缺少HTTP请求发送、服务器开始/结束、返回/读取分段。本次不再次运行恢复配方，不把job002成功当作HTTP修复或归并为心跳同因。

后续若一次系统窗口未涵盖HTTP，该缺口独立保留。必要时另行明确授权最小只读HTTP观察：同冻结程序，GET实际状态，不发Reset/Start/机构动作；先记录连接/HTTP分段。仅凭创建新HttpClient不能确定连接复用补丁，不能提前改超时或复位准入。

## 最小修复建议（尚未达到选补丁条件）

**当前首选是补证，不能确定代码补丁。** 已证实调度之前，维持独立连接、原1秒I/O/3秒保护、未知不重发及安全门禁。Test inline继续只作已记录限定缓解，不改成生产默认，不因此关闭T065。

若新系统证据表明TCP已到达、指定接收续体等待调度：限定到该接收进程和具体调度路径，候选为ModbusTcpClient.cs/LatestProtocolPlcDevice.cs或VirtualPlc/ModbusTcpServer.cs及其启动设置；优先消除已证实阻塞或改实际socket完成路径，不能默认同时改两端/大量兜底。若系统送达迟，转交对应主机/传输层定位，不硬写线程补丁；若HTTP锁等待成立，单独评估TestSpecialActions状态快照锁范围。最终文件集合须由ETL/栈确定。

任何共享接口或配置合同变化先同步003及008对应spec、contracts、plan、tasks，再进入bug-fix；仅实现内部调度修正也须记录适用构建及安全行为不变。本次无新功能规格、无重复任务。

## 可审阅采样工具与唯一外部动作

工具：../observe-default-20260927.ps1；冻结：../observation-freeze-20260927.json。已完成PowerShell AST及-ReviewOnly全部冻结输入SHA核验；未执行有权限真实采样，不能标工具运行已验证。

一次使用原r21 Host（90BDA14E…）、原PLC（66FDD5F6…）、合法Q06 Test配置；不启动配方，真实数据库由既有StorePrep初始化，真实Host/PLC/相机/算法服务接入原路径，不以替身成功。仅私有launcher的子进程inline=0；原运行时/GC和既有Test HighBeforeReadiness、50ms轮询、1s/3s保持。端点26161/26162/26163；最多120秒含启动，首次日志失败检测后最多5秒结束，采样合并/清理另外记录。进程清理仅私有launcher的后代且CreationDate仍匹配，不停止桌面worker；没有POST业务/恢复动作。

唯一需要用户完成的外部动作：在本机具备系统性能采样能力的**提升PowerShell 7**执行一次：

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File 'E:\dzk\gaode-1\.specify\bugs\t065-communication-delay\observe-default-20260927.ps1'
```

结果目录：E:\dzk\gaode-1\artifacts\communication-delay\t065-communication-delay\observe-<UTC>，内含result.json、tcp-scheduling.etl、WPR状态和冻结身份；实际数据库/设备日志路径在runtime-location.json，为既有合法artifacts/recipe-execution-008/t065-observation-observe-<UTC>。最后输出精确结果路径。若仍不能启动ETW，脚本不启动Host/PLC；只回复该目录的error.txt即可，不要求重启worker、修改权限策略或反复重跑。

## 修复前后对照和关闭条件

同一短通信负载、默认inline=0、相同合法配置/1s/3s/轮询/优先级/GC、相同端点布局及采样profile。修复前后分别保存完整冻结依赖摘要、环境、双端连接/事务/有效echo、系统事件/丢失统计及清理结果；明确唯一代码差异。先解释复现失败的实际机制，再证明该对应长段被消除；一次未复现仅算观察，不计根因修复。必要安全负例沿已有有界断联/心跳暂停证据核对变更适用性，确有影响才最少复验；不增加全配方/全故障矩阵。

| 任务 | 关闭所需 | 不可替代条件 |
|---|---|---|
| 003 T065 | 有界双端UTC/单调与有效echo事实；延迟机制可信关联；最小修正前后定向对照；原3s保护/独立连接、超期锁动作/未知不重发；原3516ms缺日志明示 | 一次正常、Test inline成功、原T063完成、权限探测都不能勾 |
| 008 T055 | T065原依赖满足，并引用已验收Q01对应构建、当前页面/冻结F/运动/保存/Final/真实结果及必要失败门禁适用证据；仅变更影响处最少复验 | 不重做已完成主流程，也不以当前Q01 Passed跳过003依赖 |
| 008 T070 | T055及其他适用父任务原条件齐；更新构建/当前证据/SC/C/F差异及生产局部限制；实际最终对账 | 本轮评估不能关闭，文档检查不是业务测试，不能宣称真机/精度/全项目完成 |

## 下一步可直接执行的Spec Kit提示词

取得一次采样后先运行：

```text
$speckit-bug-assess slug=t065-communication-delay
读取supplement-20260927T092247/assessment-supplement-20260927.md及observe-<实际UTC>/result.json、freeze.json、tcp-scheduling.etl和runtime-location.json所指双端日志。保留原评估，新建日期补充。仅分析一次有界窗口：校验ETL回环TCP/CSwitch/ReadyThread覆盖和丢失，关联端点、事务、接收线程及有效echo；区分传输到达、处理、等待调度、续体完成。r22 HTTP单独结论。未复现或无法关联明确不确定，不重跑完整配方、不改业务代码。给出唯一有证据支持的最小补丁和同条件前后对照；证据不足则不进入bug-fix。
```

机制与补丁得到实际支持后再运行：

```text
$speckit-bug-fix slug=t065-communication-delay
以最新日期补充评估中已证实机制为唯一依据；不得把原先候选假设直接当根因。仅实施其中指定最小修复；共享接口变化先同步003/008 spec、contracts、plan、tasks。保留1秒I/O、3秒心跳与安全门禁，保留历史失败；执行修复前后同条件定向对照和实际受影响的最少验证。之后$speckit-bug-test slug=t065-communication-delay按003 T065→008 T055→T070分别判定，证据不齐不勾任务。
```
