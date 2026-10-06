# Bug Verification: 复位就绪观察同步

- Slug: 008-reset-ready-observation
- Tested: 2026-09-27
- Assessment: ./assessment.md
- Fix: ./fix.md
- Result: verified（仅本缺陷）

r19首次两链接保存用例在Reset202后立即Check错误RecoveryResetNotObserved，第三旧007 fixture因正确缺配置拒绝失败；原TRX保留。r21独立正常Debug构建按原命令run-gates.ps1重跑三失败用例，3/3 Passed，立即核验进入预期Failed/CommitUnknown链接保存门禁且无新物理启动，Final授权/可靠解锁/提交回滚原断言不变。仅复位返回等待已有观察的实际PlcReady，未放宽判据或期限。

实际新DLL WPF：r22 job000 Q18整包Passed，job002完整旧图/新轮23/23操作与5/5场景Passed，exit0/cleanuptrue。旧run18de5e0b-c5c0-4237-9ada-805ceed87316，新runf8f791a2-015b-4502-b8ab-fb318905c776，实际reset/check与旧图引用见r22/current-primary-summary.json。r19十八门禁按未变源分支Passed，r21当前目录/容量十项Passed；不重跑完整矩阵。

r22 job001复位后虚拟机构HTTP1秒超期是独立运行时问题，原失败保留，不被本修复治愈声明或job002正常成功掩盖。默认运行模式、系统延迟机制及生产机械未验；本报告仅关闭评估中的缓存Ready同步缺陷和合法fixture适配，不关闭003 T065。
