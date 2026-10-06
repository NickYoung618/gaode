# 008故障完整新轮正式页面主包引用（2026-09-27）

主包为008 r18-io-0927/runs/job-006-RECOVERY-F/RECOVERY-F。旧runaaff6662-b267-4a72-8eb6-dbcab4b0e2df，新run800275b5-ffe5-47d1-adfa-758b8b76e48f；reset70211adc-439f-4dcb-9e81-7139d2a3a0b2、initialCheck78ce0708-04fc-4ba8-8b32-f6048ce73d75。outerexit0/cleanuptrue、23/23操作及场景适用审计通过，构建/版本/实际资源摘要沿[接续记录](../../008-recipe-driven-inspection/evidence/continuation-20260927.md)。

正式WPF既有复位→初始核验→显式启动→取盘控制各真实点击；新request/command/run、双端复位与新epoch，不走故障continue、不复用旧结果。旧三维图在故障前/故障时/复位后三次实际显示，API/SQLite/文件SHA和长度相同，新轮Height/F重新各一次、完整Detection/盘末/Final。旧run保留FaultRequiresNewRun/final0，双向关联可查。

job002虽新轮到Final，但故障前图片尚未加载使整包Failed，原包保留；修collector实际图片加载等待后job006整包通过。不把两包拼接成一次通过。正常暂停r5 Q01-PAUSE同run和人工换面另有独立证据，不混用。

当前新增复位状态观察同步修复需新Host实际包复验；r18事实仅按其实际r15 Host/原PLC/default GC及明确所属Test I/O配置成立，不提前称新源验收通过。006 T051、001/003直接子任务只引用此唯一主包和后继新包，不重复建页面整链或修改其他任务勾选。


## 2026-09-27 最终当前恢复主包（优先于历史检查点）

唯一当前主验收为 `artifacts/recipe-execution-008/r22-page-0927/runs/job-002-RECOVERY-F/RECOVERY-F`。旧run `18de5e0b-c5c0-4237-9ada-805ceed87316`，新run `f8f791a2-015b-4502-b8ab-fb318905c776`；reset `90678c2b-b4ac-4aed-a3fa-dda048e1667e`、initialCheck `ee49a4ba-9650-422f-9822-9adfa56d5f5f`。正式页面复位→初始核验→显式新启动→完整公共准备/F/AB单图及融合→下料/可靠解锁/取盘→Final，23/23操作、5/5场景与外层exit0/cleanup=true。旧run保留FaultRequiresNewRun/finalOutcome0；双向链接及新身份持久可查。

旧实际三维媒体 `03fdcbf8-a07f-4eeb-8479-3e14a44e8e42` 在故障前、故障时、复位后三次实际DOM加载显示均true，SQLite/文件/API的SHA及99字节相同；新轮重新Height/F各一次、Detection三次，不复用旧完成标记。原r18主包保留其r15构建范围，现由本新包承接T069唯一当前验收；早期首图前恢复不抵旧图条件。

当前正常Debug Host来自r21，SHA `90BDA14E495B482F98FD841189314499B829DB8508177C1EB88D7C8B8D295409`，Application `60A045DEA40FA2825CC87FEBDD76A497F19C377C9069D766428C41F1131AEAFA`，PLC仍原 `66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF`。默认server GC、1秒I/O/3秒心跳/原业务期限，明确所属Host与PLC Test内联I/O，生产和默认模式根因不作通过声明。

r22 job001真实旧run3955ced1-bb60-4c67-a3c3-e8886d58122a，在复位后读取虚拟机构初始状态的HTTP超过1秒而Failed，cleanup=true；原错误、日志和页面保留，未启动新轮。它不再出现r19缓存PlcReady不同步的错误，属于独立运行时超期。定位到ReadInitialStateAsync原HTTP读取及虚拟状态快照路径后，仅一次独立job002定向复验，程序/配置/期限未变；通过不证明该超期机制已修复。不得重复盲跑或放宽期限。
