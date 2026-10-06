# T066 成组与整体正式路线

成组和整体分别验收，不能用一条分支勾父任务。当前批次r5沿 [持续实施记录](implementation-night-20260926.md) 和逐job原包登记：成组F混合、成组A四面/E；整体NG、Pending、缺码、解码错误、人工确认。全部成员/部位/面、独立媒体、真实worker与融合、应搬实体/实际次数、页面刷新/重开及Final必须同run对账。

旧r4成组两条有效子范围及GROUP-A-E/整体NG失败均保留。新正式结果尚待结束；未运行/运行中不计Passed。场景读回工具仅在外层cleanupVerified后查询SQLite。

### r5 job-004 GROUP-F-MIXED 已结束子范围通过（2026-09-27）

runId `1b6869ad-de49-4e66-a419-4ca2425553a2`，两组/四成员/六面，八项场景审计true、operation-route-validation通过、exitCode0及cleanupVerified=true。NG优先且Pending面明细保留，仅P01/M01实际取放一次，其余三OK保留；新分拣提交审计七项true。实际旧媒体/结果刷新、登录重开与顶部配方恢复均通过。处置查询字段缺口已登记008-disposition-projection，不能由本子范围通过推称全部结果投影齐备；旧包不补写新字段。当前同一worker自动接续整体NG。

### r5 整体NG与Pending已退出事实、审计失败及恢复（2026-09-27）

job-005 ASSEMBLY-A-E-NG runId `07bce235-af0a-4251-9c62-9b750150a1e7`：三面、独立BASE/PIN结果及媒体，实际E绑定主对象，整体只取放一次；八项场景、七项分拣提交及十二项预算/调用审计通过，exit0/cleanuptrue。旧r4同类清理失败包保留，不覆盖。

job-006 ASSEMBLY-A-E-PENDING runId `4788a57c-9d3f-4d4e-94cc-4e4e7cb5690b`：业务operation验证通过、exit0/cleanuptrue，但场景审计Failed，调度已暂停。真实Assembly及BASE面2为Pending，其余面/PIN为OK，整体一次P01→P15；分拣提交七项、预算/调用十二项均true，仅作为明确子范围，不将整个包改Passed。根因是审计以endswith("NG")判断，PENDING也匹配，错误期望NG。assessment/fix见008-assembly-pending-audit；修正版实际AST四项通过，原失败保持。新job-020独立new run复验已安排，先恢复其他不受影响路线。

worker9476/Session2仍有效、脚本未改；原629冻结文件再次全部一致，场景Python审计工具单独加载，其原/新摘要由source-freeze-amendment-001.json补充登记，冻结业务构建与配置不变。复核调度r3接续job007—019，随后job020；无人工桌面重启。当前job007 E缺码路线进行中。

r18 GROUP-A-E/job001/rund85fefeb-c937-4764-9b71-4537c5e0750b正式Passed/cleanup=true：2组8成员14面、C28/F14即42 Detection、Height/F各1、E2、翻面6、翻后重扫0；全成员OK，实际普通取放0且8个实体NoMoveRequired均有已提交依据，组/面不复制物理处置。scene10/10、budget12/12、sorting适用3/3全部通过。页面刷新/重开/对象切换、E内部对象与外部码关联成立。正常Debug r15 Host和原PLC同冻结摘要、default GC/原1秒I/O与3秒心跳，所属Host及PLC明确Test I/O设置；不宣称默认模式或生产根因已解决。

r18 job002恢复Final仍整体Failed，23读回22通过，beforeFault显示时序缺口不放宽。修collector等待真实图片后job006独立验证，同冻结配方/程序/期限；剩余整体Pending/E错误/旋转OK保持暂存，仅job006整包验证及scene Passed后放行。新控制器不把job002改成Passed。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
