# T069 必要失败与诊断适用范围

2026-09-27 当前检查点；原失败、旧构建和 NotRun 保留，以下是必要类别对照，不是全项完成声明。各TRX按单项适用实现复用，不能把119个重叠旧检查计为当前构建全部通过。索引：r4/necessary-evidence-reuse.json、r5/necessary-evidence-current.json、implementation-ed-res-20260926.md。

| 类别 | 已有必要证据 | 当前适用范围与剩余 |
|---|---|---|
| F1 配方不合法 | 当前范围准入/缺相机/错来源拒绝；ExpectedRecipeMismatch真实F不匹配保存且无产品动作 | 目录准入未改，旧TRX按原源实现复用；当前r5正式绑定/冻结为新正例；不复跑退出范围 |
| F2 设备条件不足 | 当前r5受影响PlcStageActionPort检查缺目标、Z未复位、目标观察缺失；旧FlipFeedback错面/占用/epoch拒绝 | 新XY/ACK局部期限与取料反馈门禁已验证；正式人工占用解除及完整ACK尚待选定包 |
| F3 算法无有效结果 | 当前r5 DetectionRetryAndPending必要检查；真实Q02-PENDING、成组/整体Pending用独立worker，E缺码/错误另列 | Pending允许有限继续但不能绕过机械/配置；E正式代表尚待结束 |
| F4 必要保存失败 | 当前r5预留保存失败无下料、取料提交失败无放料；旧真实SQLite媒体保存失败阻止下一动作/Final、事务失败回滚 | 新Allocator路径已定向验证；旧TraceWriter/事务实现未改，仅按原分支复用，不把旧实时SQLite拷贝作为本轮退出后对账 |
| F5 物理未知 | 当前受影响端口断联/epoch变更无重发、实际局部运动超时；前批故障双端复位/新run完整Final；新普通暂停同run独立通过 | 原故障首图前不能证明旧图片；r5 RECOVERY-F在实际3D媒体之后故障的新旧媒体/双端复位页面证据尚待结束 |
| F6 结果/处置依据缺失 | RecipeSortingMapper缺/重复/错身份拒绝；当前r5严格Pending错位置无分拣、容量冲突/占用拒绝；组/整体明细正例分别验 | 不缺结果默认OK，不将Sequence充源槽；待成组/整体/特殊代表完整对账及父任务条件审定 |

失败日志沿 RuntimeFlow 的 run/request/operation/step、组件、epoch、完整目标/实际及保存事实定位。正常暂停、人工确认和故障新轮不得混用。当前WPF失败包r4 GROUP-A-E（导出未分页）、整体NG（外层清理身份失败）保留，分别新run重验；旧兼容fixture全AB范围拒绝及首次暂停准入缺陷失败也保留，修正版实际Q01 API和WPF通过单列。

不新增全配方×全故障矩阵或生产门槛；实际未知生产字段沿 input-readiness 只限制对应生产动作。

resume-1 job013 Q04-MANUAL 已实际打开页面并点击开始，run `0cff6304-2358-4889-b1ba-a72070759503` 被StartupNotReady/PlcCommunicationUnknown阻断，epoch2；outer1/cleanuptrue。心跳ReadBody实际3742.5521ms超过原1000ms期限，尚未人工注入/确认或执行换面。诊断 `job013-startup-diagnostics-r2.json` 保留两段可解析窗口及两段原日志不完整JSON的行号/错误，不以部分日志推断OS根因。当前worker复用继续独立路线；复验须有新诊断、修复或可证环境变化，不能把再点启动作为修复。

2026-09-27 新实际启动阻断：resume-1 job011 ROT-PART-PENDING 在平台启动、无业务run/无正式页面时，Host business-poll 读03/寄存器004F计2超1000ms，连接epoch1→2锁未知；outer1/cleanuptrue，原validation-error/process/双端诊断保留。Host heartbeat tx14于16:53:44.245699Z写出，PLC端16:53:46.1629897Z才读头，随后处理0.0721ms；真实接收前延迟约1.917秒，现有GC累计Host31.84ms/PLC10.282ms不能据此解释全部延迟，OS层原因未确定。提取见 job011-startup-diagnostics.json（原重定向日志包含本地编码异常行，首次提取失败单独保留）。不放宽I/O/心跳、不把连接未知作为OK。当前worker10536仍有效，已继续job012及其他不受影响路线；Pending出口不在原条件下盲目重跑，待诊断或条件改变再独立复验。

## r16—r18实际验证增量

r16 Q06同rund4fc1789-b50b-487f-953c-9acbb14410eb正式Final、exit0/cleanup=true及适用场景/预算/取放审计Passed，配置为普通r15 Host/default server GC/原PLC/全部原期限，显式所属Host内联I/O。证明该Test代表完成，不证明默认模式/真机或全局根因。

r17 GROUP-A-E run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，XYZ匹配、tx9478 PLC读头延迟4.7秒、处理0.0133ms；Host单侧设置不足。正式exit1/cleanup=true，保留完整WPF/API/设备/SQLite及原失败。r18 job000算法Ready 5秒失败，PLC未连接/无run，不评价PLC比较；job001同冻结DLL加显式所属PLC设置，新run已进入真实Detection，结果尚待完整退出与审计。Test worker新增两条生命周期audit不改变算法；非预期Blocked采证按实际前端同run StateObserved=20退出，预期恢复仍继续。所有期限、未知不重放与保存门禁保持。

启动次序子交付经7项HostLifecycleTests和实际日志验证，通信根因仍未完全确诊；不能把diagnostic/preflight当正式Passed或将失败覆写。

r18 job006 RECOVERY-F/run800275b5-ffe5-47d1-adfa-758b8b76e48f正式Passed/cleanup=true，23/23 operation-route及scene适用检查通过。旧runaaff6662-b267-4a72-8eb6-dbcab4b0e2df保留FaultRequiresNewRun/finalOutcome0；reset70211adc-439f-4dcb-9e81-7139d2a3a0b2、initialCheck78ce0708-04fc-4ba8-8b32-f6048ce73d75和newRun双向关联已保存。实际旧三维媒体336ea3cf-8988-4747-9af5-b5059c400a32，三次页面displayed均true且SHA/字节长度/SQLite/文件/API相同，新轮初始Height/F重新调用、3 Detection及全部盘末Final成立，不复用旧结果。原job002 beforeFault显示false失败不改；修复仅collector实际图片加载等待，业务/页面及期限不改。恢复验证主包为本job006，旧首图前恢复只作其原子范围，不再替代已有图片条件。


## 2026-09-27 最终当前恢复主包（优先于历史检查点）

唯一当前主验收为 `artifacts/recipe-execution-008/r22-page-0927/runs/job-002-RECOVERY-F/RECOVERY-F`。旧run `18de5e0b-c5c0-4237-9ada-805ceed87316`，新run `f8f791a2-015b-4502-b8ab-fb318905c776`；reset `90678c2b-b4ac-4aed-a3fa-dda048e1667e`、initialCheck `ee49a4ba-9650-422f-9822-9adfa56d5f5f`。正式页面复位→初始核验→显式新启动→完整公共准备/F/AB单图及融合→下料/可靠解锁/取盘→Final，23/23操作、5/5场景与外层exit0/cleanup=true。旧run保留FaultRequiresNewRun/finalOutcome0；双向链接及新身份持久可查。

旧实际三维媒体 `03fdcbf8-a07f-4eeb-8479-3e14a44e8e42` 在故障前、故障时、复位后三次实际DOM加载显示均true，SQLite/文件/API的SHA及99字节相同；新轮重新Height/F各一次、Detection三次，不复用旧完成标记。原r18主包保留其r15构建范围，现由本新包承接T069唯一当前验收；早期首图前恢复不抵旧图条件。

当前正常Debug Host来自r21，SHA `90BDA14E495B482F98FD841189314499B829DB8508177C1EB88D7C8B8D295409`，Application `60A045DEA40FA2825CC87FEBDD76A497F19C377C9069D766428C41F1131AEAFA`，PLC仍原 `66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF`。默认server GC、1秒I/O/3秒心跳/原业务期限，明确所属Host与PLC Test内联I/O，生产和默认模式根因不作通过声明。

r22 job001真实旧run3955ced1-bb60-4c67-a3c3-e8886d58122a，在复位后读取虚拟机构初始状态的HTTP超过1秒而Failed，cleanup=true；原错误、日志和页面保留，未启动新轮。它不再出现r19缓存PlcReady不同步的错误，属于独立运行时超期。定位到ReadInitialStateAsync原HTTP读取及虚拟状态快照路径后，仅一次独立job002定向复验，程序/配置/期限未变；通过不证明该超期机制已修复。不得重复盲跑或放宽期限。
