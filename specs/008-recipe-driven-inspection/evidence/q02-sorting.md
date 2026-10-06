# T059 多槽与NG/Pending正式分拣

前批Q02非连续P01/P03、CD完整正式链有效子范围见 implementation-ed-res-20260926.md。当前r5 Q01-NG与Q02-PENDING为原指定用例，使用真实独立worker产生结论，不用Q03替代。

新包须同时证明后端/SQLite/页面NG或Pending至Final仍保留、媒体/项目/参数身份一致，实际取料status2→在途保存→放料XYZ/命令2→status3/ACK清零→占用保存，最后可靠解锁和页面取盘。Q02-PENDING仅P01问题、P03正常，禁止两个实体占同一目标；目标配置来源见 input-readiness。当前尚待结束包正式对账；必要容量/保存失败测试见 sorting.md，父任务未勾。

### r5 job-003 Q02-PENDING 正式通过（2026-09-27）

runId `e3072005-0e53-4160-b36f-e5a9614e769f`，两非连续槽P01/P03，实际worker使P01为Pending、P03为OK，只有P01/protocolSlot1搬至P15。exitCode0、cleanupVerified=true，完整操作验证、场景读回及sorting-commit-audit七项通过。Pending维持至Final及刷新/重开，没有用“完成”代替质量或将其他正常实体搬走。原取料2→在途可靠提交→实际放料写入→放料3/ACK清零→占用提交顺序成立。当前同一worker自动进入job-004 GROUP-F-MIXED。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
