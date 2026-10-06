# T064 普通整体交付

当前计划与执行保留BASE/PIN等各部位独立检测身份；实际物理实体为Assembly，共享姿态/翻面一次，盘末按整体质量处置一次，不拆抓部位。部位媒体、单图/融合/面结果与整体汇总保持实际关联。该普通整体不以旋转输入为前置。

r4 ASSEMBLY-A-E-NG内部业务验证通过但外层进程身份清理失败，整包仍Failed。r5独立新run安排NG、Pending、E缺码和解码错误、人工确认代表，最终只有exitCode0、cleanupVerified与场景审计通过才登记；当前尚待新包结束。容量及取料在途提交见 [分拣记录](sorting.md)。

### r5 整体NG与Pending已退出事实、审计失败及恢复（2026-09-27）

job-005 ASSEMBLY-A-E-NG runId `07bce235-af0a-4251-9c62-9b750150a1e7`：三面、独立BASE/PIN结果及媒体，实际E绑定主对象，整体只取放一次；八项场景、七项分拣提交及十二项预算/调用审计通过，exit0/cleanuptrue。旧r4同类清理失败包保留，不覆盖。

job-006 ASSEMBLY-A-E-PENDING runId `4788a57c-9d3f-4d4e-94cc-4e4e7cb5690b`：业务operation验证通过、exit0/cleanuptrue，但场景审计Failed，调度已暂停。真实Assembly及BASE面2为Pending，其余面/PIN为OK，整体一次P01→P15；分拣提交七项、预算/调用十二项均true，仅作为明确子范围，不将整个包改Passed。根因是审计以endswith("NG")判断，PENDING也匹配，错误期望NG。assessment/fix见008-assembly-pending-audit；修正版实际AST四项通过，原失败保持。新job-020独立new run复验已安排，先恢复其他不受影响路线。

worker9476/Session2仍有效、脚本未改；原629冻结文件再次全部一致，场景Python审计工具单独加载，其原/新摘要由source-freeze-amendment-001.json补充登记，冻结业务构建与配置不变。复核调度r3接续job007—019，随后job020；无人工桌面重启。当前job007 E缺码路线进行中。

### 00:42 新检查点（以上为原时点事实）

job007 E缺码正式路线已结束，run `7a5da230-3c1f-4124-8d8f-be8e64fed5c4`，七项场景、十二项实际预算/调用、三项零分拣检查均通过，exit0/cleanuptrue。E实际一次、初始Height/FDecode各一次、Detection九次；缺码问题持久保留且整体OK继续，未外层取放。

job008在runner身份取得前启动失败；原catch因stderr占用二次异常导致worker9476退出。原包保留，cleanuptrue的旧分支不作为部分启动资源证明；待独立恢复核查。普通业务源码/构建/fixture不变；新独立暂停恢复root已准备，原job009—020及E错误新job021等待桌面恢复后执行，不计通过。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
