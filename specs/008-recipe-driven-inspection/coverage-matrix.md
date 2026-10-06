# USR-E当前适用性与验证集合（2026-09-26）

## 夜间当前验收检查点（进行中）

本节为最新事实；下方历史Blocked/22条等统计保留原时点，不作为当前结论。本批Host为`760393F33F5C3213630AF8CB3DC6AFBDD3BA0559D4C2681EAAEE7BA7CE734875`，Application为`F21BE8048C4C12EAC6ACC0BC750AEA3FF2876CEF94B3F66E60728A8CAF773CD6`，PLC为`CD491D80AAF5ABE858C44D01E94B7C38D0D8880D2C1181D7203D445BEC70FD3B`。实际账号/会话/配置/资源摘要见各包；当前代码缺口和完整任务条件见[evidence/completion-review.md](evidence/completion-review.md)。

| 实际采用路线/必要差异 | 版本/摘要来源 | job/runId | 当前结果及适用范围 |
| --- | --- | --- | --- |
| GROUP-F-MIXED：两组、NG优先保留Pending明细、只搬问题成员 | R008-GROUP-F/1.0.0-test；fixture及r4冻结清单 | r4 job-001 / 4993a0b8-096a-4037-bb7e-79bf7a919ab1 | 原验证及场景审计Passed，四成员六面、1次取放、3个OK留原位、实际页面Final及清理；目标预留尚非已验证 |
| GROUP-F-PENDING：纯Pending问题成员 | 同目录，新独立Pending worker清单，原包未覆盖 | r4 job-002 / 22eca098-2b64-47ad-baf9-44fca682248d | 原验证及场景审计Passed，四成员六面、1次取放、实际页面Final及清理 |
| GROUP-A-E：来源八成员、合法Q09四面3＋1、E绑定 | R008-GROUP-A-E/1.0.2-test-usr-e；usr-e-1.0.2 fixture | r4 job-003 / 253e1ff9-3284-4a13-8d6a-db3f2104afdb | **Failed**；实际Final和完整采集/成员/E等16项true，变化导出只读1024/2406条导致ACK证据检查false。保留失败及补充Modbus根因核对，待分页修复后独立新job复验 |

r4运行包根为`artifacts/recipe-execution-008/page-next-closure-20260926-night-r4/runs`，每包包含页面/API/已退出SQLite/媒体/Modbus事实及验证JSON。整体、特殊、人工及Q04/Q05/Q06尚在本批队列，不记Passed。上述成组判定刷新/重开通过的适用范围不包含已发现的配方标题恢复缺陷；不得将当前两个Passed子范围表述为008或父任务整体完成。

当前四面允许Q14/Q18/Q20/Q21所示3CD＋1AB位置，原Q09/GROUP-A-E的三AB历史结果不适用新四面；保持原Passed/Failed，只在新联合代表中核对配置、更多面及独立E。允许变体不要求全跑。


依据宪章8.0.0及011统一澄清：当前四面3CD＋1AB，AB位置由配方确定；更多面只用AB/CD按配置，不推导固定组合。只选受影响代表和必要失败，不全量或重跑009/010全部历史。原Q编号及下表运行状态保持当时范围。

下方原Q批次表与旧统计保留历史，Passed仅适用原构建/配置。本批USR-E后端坐标/取放与参数复验已Passed，正式页面仍Blocked，详细范围见文末本批表。旧PARAM Failed包保留，新同构建后端Passed不抵扣页面验收；质量15/16及生产局部限制保持。后续证据分列currentApplicability、selectedReason、recipe/version/digest、build/process/config、runId、完整序列、页面Final、问题1—5动作证据和状态。非选定的允许变体不自动欠一次运行，退出行不改历史状态。详见[交接](plan-six-issues-alignment-20260926.md)。

---
以下为历史批次与原计划索引，旧覆盖义务已由上方口径替代。

<!-- 20260926-Q-BATCH-BEGIN -->
## 2026-09-26 当前批次逐Q运行矩阵

本表仅将正式 WPF 页面同一 run 到 Final 记作 Passed。后端 API 的 Final 仅列作子能力证据；原下方矩阵为历史设计/运行快照。

| Q | 配方版本 | 完整相机序列 | Host / PLC 构建 SHA-256 | 页面 runId / Final | 证据 | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| Q01 | R008-Q01/1.1.1-test | AB | 56F847FB6640 / 2B8DBC22E8D5 | 137a48c3-a758-4fc1-8c0f-893422872397 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-001-Q01/Q01/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-001-Q01/Q01/route-validation.json) | Passed |
| Q02 | R008-Q02/1.1.1-test | CD | 56F847FB6640 / 2B8DBC22E8D5 | b6d0edec-a250-40ef-a789-0d0956a7972f / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-002-Q02/Q02/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-002-Q02/Q02/route-validation.json) | Passed |
| Q03 | R008-Q03/1.0.0-test | AB→AB | 56F847FB6640 / E53EE2116B19 | 0c7fed07-bf13-4af7-b195-a70866097790 / Final | [正式页面](../../artifacts/recipe-execution-008/page-q03-interactive-20260926-v3/Q03/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-q03-interactive-20260926-v3/Q03/route-validation.json) | Passed |
| Q04 | R008-Q04/1.0.0-test | AB→CD | 56F847FB6640 / E53EE2116B19 | 14cca3c8-b9e5-4a8e-a4d4-6d9cf1e6b749 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926/runs/job-001-Q04/Q04/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926/runs/job-001-Q04/Q04/route-validation.json) | Passed |
| Q05 | R008-Q05/1.0.0-test | CD→AB | 56F847FB6640 / 2B8DBC22E8D5 | 0bf1cf91-da19-41af-b258-b2dcb96a4934 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-003-Q05/Q05/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-003-Q05/Q05/route-validation.json) | Passed |
| Q06 | R008-Q06/1.0.0-test | CD→CD | 56F847FB6640 / 2B8DBC22E8D5 | 56ab5ae4-fc5c-4cca-a633-214e36ab0c82 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-004-Q06/Q06/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-004-Q06/Q06/route-validation.json) | Passed |
| Q07 | R008-Q07/1.0.0-test | AB→AB→AB→AB | 56F847FB6640 / 2B8DBC22E8D5 | f6ec5a25-ffdc-4b40-a5f6-7ecbfd9aaff5 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-005-Q07/Q07/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-005-Q07/Q07/route-validation.json) | Passed |
| Q08 | R008-Q08/1.0.0-test | AB→AB→AB→CD | 56F847FB6640 / 2B8DBC22E8D5 | 8ecf023e-35d3-46dc-bc96-5d74e6e66e1e / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-006-Q08/Q08/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-006-Q08/Q08/route-validation.json) | Passed |
| Q09 | R008-Q09/1.0.0-test | AB→AB→CD→AB | 56F847FB6640 / 2B8DBC22E8D5 | 2c8ee8dd-0dd5-4c81-a508-a330f5658f75 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-007-Q09/Q09/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-007-Q09/Q09/route-validation.json) | Passed |
| Q10 | R008-Q10/1.0.0-test | AB→AB→CD→CD | 56F847FB6640 / 2B8DBC22E8D5 | d73a0cc2-7574-4024-9c63-04cbecd8f87f / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-008-Q10/Q10/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-008-Q10/Q10/route-validation.json) | Passed |
| Q11 | R008-Q11/1.0.0-test | AB→CD→AB→AB | 56F847FB6640 / 2B8DBC22E8D5 | 5f6cb368-dc9c-4510-a919-46d916b71168 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-009-Q11/Q11/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-009-Q11/Q11/route-validation.json) | Passed |
| Q12 | R008-Q12/1.0.0-test | AB→CD→AB→CD | 56F847FB6640 / 2B8DBC22E8D5 | f7f03d2f-5821-4a09-8461-a1efeea56798 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-021-Q12/Q12/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-021-Q12/Q12/route-validation.json) | Passed |
| Q13 | R008-Q13/1.0.0-test | AB→CD→CD→AB | 56F847FB6640 / 2B8DBC22E8D5 | 49c6e972-a86a-4dbd-8f4d-71b1e0eda38c / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-011-Q13/Q13/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-011-Q13/Q13/route-validation.json) | Passed |
| Q14 | R008-Q14/1.0.0-test | AB→CD→CD→CD | 56F847FB6640 / 2B8DBC22E8D5 | a734b93a-df26-46a1-8678-6f3ce78a7525 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-012-Q14/Q14/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-012-Q14/Q14/route-validation.json) | Passed |
| Q15 | R008-Q15/1.0.0-test | CD→AB→AB→AB | 56F847FB6640 / 2B8DBC22E8D5 | 1124bf7a-f0b8-4625-82b1-7fd4eb1f06ba / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-013-Q15/Q15/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-013-Q15/Q15/route-validation.json) | Passed |
| Q16 | R008-Q16/1.0.0-test | CD→AB→AB→CD | 56F847FB6640 / 2B8DBC22E8D5 | 6b6d389c-1a69-483a-a7b8-48ba9f41f992 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-014-Q16/Q16/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-014-Q16/Q16/route-validation.json) | Passed |
| Q17 | R008-Q17/1.0.0-test | CD→AB→CD→AB | 56F847FB6640 / 2B8DBC22E8D5 | 5e8737ac-6648-4938-b9aa-03bd95ef5561 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-015-Q17/Q17/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-015-Q17/Q17/route-validation.json) | Passed |
| Q18 | R008-Q18/1.0.0-test | CD→AB→CD→CD | 56F847FB6640 / 2B8DBC22E8D5 | 0a012bdf-9148-4337-95b6-c2cc839e0360 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-016-Q18/Q18/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-016-Q18/Q18/route-validation.json) | Passed |
| Q19 | R008-Q19/1.0.0-test | CD→CD→AB→AB | 56F847FB6640 / 2B8DBC22E8D5 | c28696bb-75d2-4ef8-81d2-c1a11f0b91dd / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-017-Q19/Q19/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-017-Q19/Q19/route-validation.json) | Passed |
| Q20 | R008-Q20/1.0.0-test | CD→CD→AB→CD | 56F847FB6640 / 2B8DBC22E8D5 | b1c660cf-c91f-4278-99d9-8d4dcaf70595 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-018-Q20/Q20/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-018-Q20/Q20/route-validation.json) | Passed |
| Q21 | R008-Q21/1.0.0-test | CD→CD→CD→AB | 56F847FB6640 / 2B8DBC22E8D5 | e3bce82b-ae33-4989-a5dc-03b301ec50a9 / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-019-Q21/Q21/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-019-Q21/Q21/route-validation.json) | Passed |
| Q22 | R008-Q22/1.0.0-test | CD→CD→CD→CD | 56F847FB6640 / 2B8DBC22E8D5 | 3a10c5eb-4a09-47cd-ab08-c0c53f198fcd / Final | [正式页面](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-020-Q22/Q22/recipe-webview2-page-evidence.json)、[逐步核验](../../artifacts/recipe-execution-008/page-batch-20260926-v2/runs/job-020-Q22/Q22/route-validation.json) | Passed |

另有 Q04 Flip_OK 受控超时失败包 `artifacts/recipe-execution-008/backend-q04-flip-ack-hold-20260926/`：首面后未采第二面、未到 Final。
<!-- 20260926-Q-BATCH-END -->


# 当前统计口径：20260925分区协议

新版Q=0/22、C=0/8，F按影响待验证；下表的既有Passed/部分实跑仅表示旧协议证据，不能抵扣新版。Q/C编号和覆盖目标保持。先新版Q03普通OK到Final，再其余适用自动两/四面；分拣/E/人工/组/旋转按局部依赖收口。

# 前端配方全流程覆盖矩阵

日期2026-09-24；2026-09-25第五批更新Q01/Q02实际Test证据。其余行仍是待验证要求。
前端配方ID/版本、F唯一绑定、执行计划与保存记录必须一致。基础ID及变体见[recipe-cases.md](recipe-cases.md)，阶段见[plan.md](plan.md)，依赖见[执行合同](contracts/execution.md)。
不再使用逐M两配方和“22种仅表达检查”的旧门槛。

## Q01—Q22完整序列

每行关联一个实际检测对象的完整序列及同次前端启动到Final的证据。可用合法差异用例替换基础行的重复运行，但必须登记实际配方/版本、对象与完整run。
状态NotRun表示本轮未运行；依赖列同时指出实施/验收阻塞，不以文档表格形成Passed。

当前Q03行修正（2026-09-25第八批）：[自动多面主链证据](evidence/eighth-batch-auto-multiface.md)已实跑第二轮3D/worker/SQLite/媒体、第二面AB和融合的Test组件；正式Q03仍NotRun。第七批历史记录所述“正常触发/旧清零均未定”已被纠正，当前准确阻塞为取料、放料两组坐标通过哪些字段及怎样提交；Host自行处理本轮反馈关联。后续Q04—Q22自动无E路线不得沿用下表“全部B01/B09”作一律阻塞，应逐条核对实际适用分支，且Q03正式通过前不记其运行Passed。

| 编号 | 完整面序列 | 前端配方/版本 | 实施阶段 | 依赖 | 完成条件 | 当前证据 |
| --- | --- | --- | --- | --- | --- | --- |
| Q01 | AB | R008-Q01 / 1.1.1-test | S1 | Test映射已登记；生产标定仍待办 | 前端选配方至Final；SC001/003/005 | Test虚拟端到端Passed；[同run证据](evidence/fifth-batch-q01-q02.md) |
| Q02 | CD | R008-Q02 / 1.1.1-test | S2 | Test映射已登记；生产标定仍待办 | 前端选配方至Final；SC001/003/005 | Test虚拟端到端Passed；P01/P03非连续槽；[同run证据](evidence/fifth-batch-q01-q02.md) |
| Q03 | AB→AB | R008-Q03 / 1.0.0-test | S3 | 新版§3.1.5字段及ACK已定义，正式实现与合法点位尚待交付；当前旧Test目录Restricted | 前端选配方至Final；SC001/003/005 | NotRun/正式Flip受限；[第八批组件证据](evidence/eighth-batch-auto-multiface.md)已验证轮2 3D与第二面AB，未有PLC翻面/页面Final |

| Q04 | AB→CD | R008-Q04 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q05 | CD→AB | R008-Q05 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q06 | CD→CD | R008-Q06 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q07 | AB→AB→AB→AB | R008-Q07 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q08 | AB→AB→AB→CD | R008-Q08 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q09 | AB→AB→CD→AB | R008-Q09 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q10 | AB→AB→CD→CD | R008-Q10 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q11 | AB→CD→AB→AB | R008-Q11 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q12 | AB→CD→AB→CD | R008-Q12 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q13 | AB→CD→CD→AB | R008-Q13 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q14 | AB→CD→CD→CD | R008-Q14 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q15 | CD→AB→AB→AB | R008-Q15 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q16 | CD→AB→AB→CD | R008-Q16 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q17 | CD→AB→CD→AB | R008-Q17 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q18 | CD→AB→CD→CD | R008-Q18 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q19 | CD→CD→AB→AB | R008-Q19 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q20 | CD→CD→AB→CD | R008-Q20 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q21 | CD→CD→CD→AB | R008-Q21 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |
| Q22 | CD→CD→CD→CD | R008-Q22 / 1.0.0-test或已登记合法替代版本 | S3 | B01/B04/B09及换面B02或B07 | 前端选配方至Final；SC001/003/005 | NotRun；尚无008完整证据 |

## C01—C08实际业务差异

| 编号 | 用例与实际差异 | 阶段/FR | 完成条件及证据 | 受限输入 |
| --- | --- | --- | --- | --- |
| C01 | Q01-PARAM、Q02-MULTI；AB/CD多槽、非连续槽及已确认空槽 | S2；FR002/003/017 | 相机整批顺序、对象无串用、空槽无多余动作，参数实际生效 | B04、占用/布局适用B08 |
| C02 | 多面Q及Q04-MANUAL-E；自动与人工换面、逐面合法目标解析（完成本轮相关对象翻转放回后统一复查3D姿态，F不重绑）及适用E | S3；FR005/006 | 面号/ACK清零/该面合法目标/初始测量引用/后续采集/人工交互及E事实 | B02/04、B07/B09-U；E需B01/06 |
| C03 | GROUP；同盘多组同组成，成员1/2/4面 | S4；FR007/010 | 组成员结果分明、已完成员不重采、按确认策略完成全部应搬成员 | B02/04/05/08；混合B06 |
| C04 | ASSEMBLY；普通S3部位/整体 | S4；FR008/010 | 部位汇总、共享姿态一次、整体搬运一次，非部位拆抓 | B02/04；不依赖B03 |
| C05 | ROT-PART三出口、ROT-ASSEMBLY整体 | S4；FR009/010 | 当前实体逐相机/姿态、OK原槽及NG/Pending目标，工位空闲，无盘末重复分拣 | B02/03/04/08 |
| C06 | Q01-NG/Q02-PENDING及每条路线尾段 | S1尾段、S2处置；FR010/011 | 实体真实源目标、必要保存、下料/解锁/取盘/Final分开成立 | B02/04/08/B09-U；混合B06 |
| C07 | RECOVERY复用必要失败 | S5及受影响阶段；FR012 | 实物/原任务/未知动作核对、人工决定和持续受限证据；无自动重放 | B07/B09-U |
| C08 | Q01→Q01-PARAM，或其他有实际差异的新配方 | S2；FR017/018、SC002 | 程序摘要相同，前端选新版本后实际行为变化，原运行快照未改写 | 目录装载/前端配方映射 |

C01由Q02的P01/P03非连续槽、Q01-PARAM的A整批后B整批及实际参数变化提供部分证据；已确认空槽无多余动作仍待核对。C06普通OK尾段由Q01/Q02/Q01-PARAM提供部分证据，NG/Pending真实处置仍待验证。C08的同程序配置变化已由[第六批对照](evidence/config-change.md)通过。C02—C05/C07仍NotRun。不要求每Q重复每C，不能因为Q序列而省掉C03—C07差异。

## F1—F6必要失败

| 编号 | 最少适用验证 | 阶段/影响需求 | 必须观察 |
| --- | --- | --- | --- |
| F1 | 前端所选与F绑定不一致；缺必要配方参数的校验 | S1/S2；FR001/018 | 具体原因、无产品动作；拒绝与已受理后受限区分 |
| F2 | 选一个产品到位/轴反馈不符；在多面新增实际面号/ACK或当前面目标无效 | S1/S3；FR002/005/006 | 无依赖采集/运动，不得无依据复用上一面XYZ，原因可查 |
| F3 | 一个实际worker超时/无有效结果；双输入缺项验证同面不假OK | S1/S2；FR003/013 | 有限Pending及输入/调用事实，只继续合法步骤；可复用Pending分拣 |
| F4 | 一次关键保存失败 | S1；FR002/011/014 | 不能派发依赖动作或报告Final，日志指明保存边界 |
| F5 | 一次真实已派发取放后反馈未知；复用必要恢复上下文 | S2/S4/S5；FR010—012 | 在途占用保留、不盲重发、不提前下料 |
| F6 | 缺成员/组策略未确认/目标已满中直接影响新增逻辑的最小集 | S4；FR007/010 | 不默认OK或猜处置；定位具体缺失对象/依据 |

已有未受改动影响的必要失败证据可引用原范围；新增路径须验证，不强制所有失败都与22序列组合。
心跳/401/403等当前入口缺口由S0/S1对应修复定向验证，不能用全量回归掩盖未关闭原因。

## M01—M19需求来源追溯

M是动作来源索引，不再是逐项双配方门槛。

| 来源动作 | 对应覆盖/阶段 | 处理 |
| --- | --- | --- |
| M01上料启动、M02公共3D、M03 F绑定 | 所有完整Q及差异run；S1起 | 同run公共前置可共用证据，F新握手已定义，待B01-F实现及验证 |
| M04 AB、M05 CD | 含对应相机组的Q；C01 | 实际定位/批采/单图/融合/保存/复位 |
| M06自动换面、M07人工换面、M08目标续接 | Q03—Q22、C02 | 自动/人工各适用用例，换面后该面合法目标及明确测量/配置来源 |
| M09 E扫码 | Q04-MANUAL-E或独立合法E变体；C02 | 时机及放行必须有来源 |
| M10进旋转位、M11姿态检测、M12 OK回位、M13 NG/Pending出口 | ROT-PART/ROT-ASSEMBLY；C05 | 真实动作、反馈及三个出口，不能只检查计划 |
| M14普通分拣、M15成组处置、M16整体处置 | C03/C04/C06及特殊整体差异 | 按实际搬运实体完成，不混用成员/部位 |
| M17下料与取盘 | 每个完整运行；S1起 | 先接入，最终页面/保存一致 |
| M18示教 | 延期 | 本轮仅用已配置点位；不计未完成主流程覆盖，不伪造示教通过 |
| M19必要恢复 | C07/F5；S5及适用阶段 | 实物和任务核对；完整恢复矩阵延期 |

## FR/SC追溯及汇总

FR001—006对应公共与Q/C01/C02；FR007—010对应C03—C06；FR011/012对应各尾段及C07；FR013/014贯穿预算和全部必要失败；FR015/016要求全部前端证据；FR017/018对应C08/F1。
SC001=Q22/22；SC002=C08；SC003=同run可追查；SC004=必要F；SC005=页面/绑定/实际/保存一致；SC006=C8/8。
未来汇总以[证据合同](contracts/evidence.md)和[evidence/index.md](evidence/index.md)登记Passed/Failed/Blocked/NotRun。当前Test虚拟证据：Q01/Q02 Passed，Q03—Q22 NotRun；C08 Passed，C01/C06部分通过，其余NotRun。失败子项见[第六批门禁证据](evidence/sixth-batch-gates.md)。

## 当前阶段与任务追溯（S0—S5任务更新；历史任务映射）

任务简称目录见[tasks.md](tasks.md)“目录引用与唯一归属”；每个验收任务都要求正式页面完整运行和证据合同，不以计划展开代替。公共S0/S1实现依赖：specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T065/T067—T070、specs/006-frontend-station01-console T048/T049、specs/007-station01-integrated-loop T031—T033、specs/008-recipe-driven-inspection T049—T054；后续按实际分支扩展，不重复实现。

| Q | 当前开发阶段 | 完整运行任务 | 预期证据位置 | 当前结论 |
| --- | --- | --- | --- | --- |
| Q01 | S1 | specs/008-recipe-driven-inspection T055；T070收口 | evidence/q01.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | Test虚拟Passed；见第五批证据 |
| Q02 | S2 | specs/008-recipe-driven-inspection T059；T070收口 | evidence/q02-sorting.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | Test虚拟Passed；见第五批证据 |
| Q03 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q04 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q05 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q06 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q07 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q08 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q09 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q10 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q11 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q12 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q13 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q14 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q15 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q16 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q17 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q18 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q19 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q20 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q21 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |
| Q22 | S3 | specs/008-recipe-driven-inspection T062；T070收口 | evidence/q03-q22.md中对应Q/recipe/run/object与artifacts/recipe-execution-008运行包 | NotRun |

USR-E后T066的合法GROUP/ASSEMBLY完整对象序列可复用Q01/Q04及实际采用的合法四面代表；原Q12仅历史，不要求重复运行，必须给对象级完整序列和Final引用，不能凭含某两个相机就算完整四面Q。基础配方与变体明细仍以recipe-cases.md为准。

| 差异/失败 | 阶段/实现所有者 | 完整操作或必要失败验收任务 | 证据位置/局部阻塞 |
| --- | --- | --- | --- |
| C01多槽/非连续/空槽 | S2；specs/008-recipe-driven-inspection T056、specs/002-plc-xyz-recipes T11 | specs/008-recipe-driven-inspection T058/T059 | evidence/config-change.md、q02-sorting.md；B04实际映射 |
| C02自动/人工换面、逐面目标及适用E | S3；specs/008-recipe-driven-inspection T060/T061、specs/003-plc-latest-protocol T071/T072、specs/006-frontend-station01-console T050 | specs/008-recipe-driven-inspection T062 | evidence/q03-q22.md；自动B02/04，人工B07，E另B01-E/B06 |
| C03多组及成员1/2/4面 | S4；specs/008-recipe-driven-inspection T063、T057 | specs/008-recipe-driven-inspection T066 | evidence/group-assembly-runs.md；B05策略及取放输入 |
| C04普通S3共享姿态/整体 | S4；specs/008-recipe-driven-inspection T064 | specs/008-recipe-driven-inspection T066 | 同上；不依B03 |
| C05特殊件/整体、三出口 | S4；specs/003-plc-latest-protocol T071、specs/008-recipe-driven-inspection T065 | specs/008-recipe-driven-inspection T067 | evidence/rotation-runs.md；B03及取放输入 |
| C06真实NG/Pending及最终结束 | S1尾段003 T069/006 T049；S2搬运003 T071/008 T057 | specs/008-recipe-driven-inspection T055尾段、T059补分拣 | evidence/q01.md、q02-sorting.md；B02/04/08，混合另B06 |
| C07必要恢复 | S5；specs/008-recipe-driven-inspection T068、specs/003-plc-latest-protocol T072、specs/006-frontend-station01-console T051 | specs/008-recipe-driven-inspection T069 | evidence/failures.md、recovery.md；B07适用部分 |
| C08同程序配置变化 | S2；specs/002-plc-xyz-recipes T11、specs/008-recipe-driven-inspection T050/T056 | specs/008-recipe-driven-inspection T058 | evidence/config-change.md；同程序摘要/不同实际行为/旧快照 |
| F1非法目录/绑定/参数/能力 | S0/S1；specs/002-plc-xyz-recipes T11、specs/003-plc-latest-protocol T068、specs/001-station01-public-preparation T090 | specs/008-recipe-driven-inspection T055/T069 | evidence/q01.md、failures.md；F定义不是阻塞 |
| F2到位/轴/面/复位/安全 | S1/S3/S4；specs/003-plc-latest-protocol T067/T070/T071、specs/008-recipe-driven-inspection T060 | specs/008-recipe-driven-inspection T055/T062/T067/T069 | 各运行/失败记录；按受影响能力取最小必要集 |
| F3实际worker缺图/超时 | S1/S2；specs/007-station01-integrated-loop T032、specs/008-recipe-driven-inspection T053 | specs/008-recipe-driven-inspection T059/T069 | evidence/failures.md；有限Pending不掩盖机械未知 |
| F4必要保存失败 | S1；specs/008-recipe-driven-inspection T054、specs/003-plc-latest-protocol T069 | specs/008-recipe-driven-inspection T055/T069 | evidence/persistence.md、failures.md；无假Final |
| F5已派发未知/恢复 | S2/S4/S5；specs/003-plc-latest-protocol T071、specs/008-recipe-driven-inspection T057/T065/T068 | specs/008-recipe-driven-inspection T067/T069 | evidence/failures.md；留占用、不盲重发 |
| F6缺结果/身份/策略/混合/满位 | S1/S2/S4；specs/003-plc-latest-protocol T069、specs/008-recipe-driven-inspection T057/T063/T064 | specs/008-recipe-driven-inspection T055/T059/T066/T069 | evidence/failures.md逐必要子项列状态；未定策略拒绝，无默认 |

| M来源 | 当前任务与完整路线追溯 |
| --- | --- |
| M01上料启动/M02公共3D/M03 F | specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T067/T068；specs/008-recipe-driven-inspection T055及所有后续Q共覆盖 |
| M04 AB/M05 CD | specs/008-recipe-driven-inspection T052—T056；T055/T059及各多面Q |
| M06自动翻面/M07人工翻面/M08目标续接 | specs/008-recipe-driven-inspection T060、specs/003-plc-latest-protocol T071/T072、specs/006-frontend-station01-console T050；specs/008-recipe-driven-inspection T062 |
| M09 E | specs/008-recipe-driven-inspection T061/T062适用E变体；B01-E独立阻塞 |
| M10进旋转/M11姿态检测/M12 OK回位/M13 NG/Pending出口 | specs/003-plc-latest-protocol T071、specs/008-recipe-driven-inspection T065/T067；C05 |
| M14普通盘末分拣 | specs/008-recipe-driven-inspection T057/T059、specs/003-plc-latest-protocol T071；C06 |
| M15成组处置 | specs/008-recipe-driven-inspection T063/T066；C03 |
| M16半成品整体 | specs/008-recipe-driven-inspection T064/T066普通整体；T065/T067旋转整体；C04/C05 |
| M17下料/取盘 | specs/003-plc-latest-protocol T069、specs/006-frontend-station01-console T049；specs/008-recipe-driven-inspection T055起每条Q完整尾段 |
| M18新增示教 | 已明确延期，旧T040/T043归档，不生成本轮实现任务 |
| M19复位/恢复 | specs/008-recipe-driven-inspection T068/T069、specs/003-plc-latest-protocol T072、specs/006-frontend-station01-console T051；C07/F5 |

FR-001—018及SC-001—006逐项映射见tasks.md。上述证据路径为待产出位置，旧协议仅Q01/Q02及C08有实跑Passed证据；新版均NotRun；Q22/C8总体尚未完成，F逐子项见第六批证据。


## USR-E / USR-D / RES本批增量（2026-09-26）

本表区分后端子能力与正式WPF。没有本批页面Passed；旧表保持原适用构建及事实。业务构建Host `0068C38949D8`；最终Host `D3E57BACED1E`仅后续证据投影修订，投影6项及API/持久25项覆盖；四面成组也使用最终Host。前端实际资源待ResourcesResolved。完整摘要、身份/媒体序列和路径见[本批核验](../../artifacts/recipe-execution-008/implement-ed-res-20260926/batch-verification.json)及[实施记录](evidence/implementation-ed-res-20260926.md)。

| 路线 | 当前配方版本 | 采集序列/差异 | 后端runId/状态 | 本批正式页面 |
| --- | --- | --- | --- | --- |
| q01 | R008-Q01/1.1.1-test | AB | ce3f0b14-b76e-4f2d-a0d9-a75d91ca218a / AwaitingManualRemoval | Blocked：交互worker未ready |
| q01-param | R008-Q01/1.2.0-test | AABB | 0820323c-f77b-43ec-a8a8-ab075b628995 / AwaitingManualRemoval | Blocked：交互worker未ready |
| q02 | R008-Q02/1.1.1-test | CCDD | 1f864703-4036-492e-b7c8-526e73888bf9 / AwaitingManualRemoval | Blocked：交互worker未ready |
| GROUP-A-E | R008-GROUP-A-E/1.0.2-test-usr-e | AAAAAAAABBBBBBBBEEAABBCCDDAABB | d1e421a2-8e9b-48cf-a2d3-18891767be3b / Completed | Blocked：交互worker未ready |
| q03 | R008-Q03/1.0.0-test | ABAB | 3fc249c7-418e-4bda-9e68-28cdaa91927b / Completed | Blocked：交互worker未ready |
| q03-ng | R008-Q03/1.0.0-test | ABAB | 5f5cb522-1079-49d5-9eb1-82f961ef75f1 / Completed | Blocked：交互worker未ready |
| q03-pending | R008-Q03/1.0.0-test | ABAB | 3a798793-b626-481e-9d0a-452d38fa53a9 / Completed | Blocked：交互worker未ready |
| q09 | R008-Q09/1.0.2-test-usr-e | ABABCDAB | f7fd1b02-8f66-48a4-906e-f569ca81c26e / Completed | Blocked：交互worker未ready |
| q18 | R008-Q18/1.0.2-test-usr-e | CDABCDCD | d82d5250-a772-4863-a692-908ea2cf1ce8 / Completed | Blocked：交互worker未ready |
| C07/F5 | R008-SINGLEFACE-GATES/1.0.0-test | AB | 911ea50b-f0bb-4798-b278-c66c9b4cf5e1 / Completed | Blocked：交互worker未ready |

Q09=AB→AB→CD→AB，Q18=CD→CD→AB→CD；少数组在中间，均有3次真实逐实体Flip/ACK清零，初始3D/F各一次。Q03三态后端到持久Final；Q01/PARAM/Q02后端到解锁待取盘，不伪称Final。旧故障 `1130f3f5-18de-4e49-a6eb-094098480096` 关联完整新轮 `911ea50b-f0bb-4798-b278-c66c9b4cf5e1`；C07/F5页面唯一归T069，本表后端证据不抵扣。成组失败r1保留，r2按原冻结期限等待后通过；不是放宽应用阶段期限。

允许但未选的新位置变体不新增强制实跑欠项；退出当前范围Q07/Q08/Q09/Q10/Q11/Q12/Q13/Q15/Q16/Q17/Q19/Q22保留旧证据。本批不改独立C范围或宣称008整体/生产验收完成。

## 正式WPF USR-E/D/RES当前选定验收（2026-09-26）

本次仅本批页面范围完成；008整体及生产验收未完成。旧范围/失败事实保持。详见[当前实施记录](evidence/implementation-ed-res-20260926.md)及[逐job完整证据](../../artifacts/recipe-execution-008/page-ed-res-20260926/page-batch-verification.json)。

| Case | 最新job | 页面结果 | runId |
| --- | --- | --- | --- |
| Q03 | job-010 | Passed | 1282f1bd-4f0f-40bf-8245-0aba4b082e96 |
| Q03-NG | job-002 | Passed | d6a5501f-4bce-4b2d-ba90-2c57a6871658 |
| Q03-Pending | job-003 | Passed | 7a171ec1-faec-49eb-9800-70307bff28d3 |
| Q09 | job-004 | Passed | 8a5523a2-4b8e-4777-bb45-78f50a126df2 |
| Q18 | job-005 | Passed | dc67369c-81b6-4a62-986f-3ea251afc3b8 |
| Q01 | job-013 | Passed | 60768df5-1fed-4304-8791-01084b02b1a0 |
| Q01-PARAM | job-011 | Passed | c483b268-ec99-437b-b5f4-d882c87d5c1c |
| Q02 | job-012 | Passed | a4a7f367-d025-4a2e-9365-11b7177d1cc9 |
| RECOVERY-3D | job-009 | Passed | 0ad5dfe7-17d2-4699-9b0a-ae2e5a0b08e6 |

13次尝试中10次Passed、3次Failed原包保留；原构建与查询修复后构建分别记录，未混写为同一运行包。Q01/PARAM最终相同Host摘要且输入/调用/输出实际变化；关闭仅008 T058，其余父任务保留原未齐条件。Q09/Q18仍只为3＋1起始代表，不恢复22/14/8全部实跑门槛。C07/F5页面检查点归008 T069，不代表其全部失败/三操作对照已完成。


## 2026-09-27 当前适用覆盖（本节优先于历史NotRun/Blocked）

原始十三次page-ed-res是独立历史基线：10 Passed/3 Failed，不同构建分别保留。当前接续每包状态、构建与失败沿r22/attempt-inventory-final.json及task-audit-night-20260927，不能仅看exit0。

| 当前必要集合/差异 | 已退出正式页面证据及复用理由 | 状态 |
|---|---|---|
| AB、CD、ABAB | 原当前USR-E page-ed-res Q01/Q02/Q03；Q01/PARAM同最终Host，r5单面质量/暂停与r8 CD非连续P03补实际处置 | Test选定全流程Passed |
| ABCD、CDAB、CDCD | r13 Q04/Q05（R2R比较范围明确）、r16 Q06（普通Debug/原PLC/Test IO）；原稳定业务不因启动/复位或权限绑定重复全跑 | Test选定全流程Passed |
| 3AB+1CD起始Q09 | 原page-ed-res job004 AB→AB→CD→AB；r18 GROUP-A-E中四面成员完整相同序列，初始一次H/F、真实逐实体Flip/ACK | Test选定全流程Passed |
| 3CD+1AB当前Q18 | r22 job000 CD→AB→CD→CD；四面C8/F4/Detection12、Flip3、翻后Height0，完整Final/刷新重开/无搬运依据 | Test当前指定顺序Passed；旧Q18 CDCDABCD保留旧事实 |
| C01/C06多槽/实际分拣 | 原Q02多槽CD，r5 Q01-NG/Q02-PENDING，r8 job013 P03源槽3≠动作1、P15目标、取料2→保存→放料3/ACK0→占用 | Passed |
| C02换面/E | r8 Q04人工、r12整体人工；r18组A四面/E、整体E错误，r5 E缺码；原自动代表及当前门禁 | Passed |
| C03成组 | r5 Group-F两组1/2面混合NG/Pending仅问题成员搬、r4纯Pending原范围；r18 Group-A两组1/4面8成员14面全部结果/ACK | Passed；生产布局/容量另受限 |
| C04普通整体 | r5 NG/NoCode、r18 Pending/EError、r12人工：BASE2/PIN1、整体共享换面/一次搬运，不拆抓 | Passed |
| C05特殊旋转 | Part OK(r18)/NG(resume-1)/Pending(r12)三真实出口；Assembly OK(resume-1)的部位/实体差异。共同Exit代码/协议未变，NG/Pending共享出口按task-audit限定复用 | Passed；未声称每种整体出口均新跑 |
| C07三操作对照 | 正常暂停r5同run，人工r8/r12同run，故障r22 job002显式新run完整Final/旧图留存；r19/r21必要拒绝/事务门禁 | Passed；唯一当前主包r22 job002 |
| C08同程序配方变化 | 原page-ed-res Q01job013/PARAMjob011同最终Host，实际槽/输入/调用/输出变化，旧快照不改 | Passed；T058原X |
| F1/F2 | 旧有效F绑定不匹配、当前18顺序/身份/目标/轴/复位/面/epoch/安全及目录8退出拒绝；r20真实401/403作为附加页面拒绝 | 必要类别Passed，旧分支限定源适用 |
| F3/F4/F5/F6 | 独立worker实际Pending/NG/E失败、真实SQLite未决/晚提交/事务回滚、故障新轮/未知不重发、缺结果/成员/容量/目标冲突；当前35 distinct必要用例最终全Passed | 必要类别Passed，不是全故障矩阵 |

允许集合为一面两面六种与四面Q08/Q09/Q11/Q14/Q15/Q18/Q20/Q21。实际采用以各run recipeRef/catalog/plan为准；必要四面起始Q09/Q18按上述场景差异选定。允许而未选位置变体为NotRun，不新增8/14/22全跑门槛；退出Q07/Q10/Q12/Q13/Q16/Q17/Q19/Q22仅保留历史构建/编号/状态，不标当前Passed。

FR-001/002/003/004/018→T049—T056及当前门禁；FR-005/006/012→T060/T061/T068/T069；FR-007/008/009/010→T057/T063—T067；FR-011→T055/T059及003尾段；FR-013/014/016→T051/T054及API/页面/日志；FR-015/017→各正式路线/T058/T070。US1三、US2四、US3三、US4三、US5三、US6三，共19验收场景。SC-001选定序列、SC-002同程序差异、SC-003持久读回、SC-004必要F/完整新轮、SC-005页面/后端/存储、SC-006八类差异分别具备上述Test证据；008原依赖心跳机制验收未全齐，所以Test选定路线Passed不宣称008全条件完成。

## 2026-09-27T05:07:42.335482+00:00 T065机制修复及当前正式Q01收口（本节优先于历史待办状态）


| 原条件 | 证据及结论 |
|---|---|
| 003 T065机制与最小修复 | 同NativeOverlapped批前后→64→Read直接关联，两个真实1秒超期主要在批后1112/1057ms；同DLL冷启动Native0/1切断该路径；正式最小Test接线，无放宽期限 |
| T065受控对照 | 冻结DLL/config/profile/初始化及inline0相同，只有运行时provider；业务/心跳txn3按连接分列；候选冷启动业务txn3 Read6.3766ms，心跳txn3 Read0.2003ms（后一瞬时指针0，不能伪造64关联）；全部1560响应头最慢20.2574ms |
| T065真实超期安全 | .NET10.0.12 Native=True下真实TCP响应丢弃I/O1000ms及PauseHeartbeat3000ms，均latch、epoch1→2、拒绝新Move；真实TCP1秒超期无自动重发3请求；TRX3/3，无Skipped |
| T065日志 | 连接/事务/PID/TID/QPC关键阶段及失败窗口保留；必要测试验证慢成功与真实失败窗口、deadline/端点/事务/GC字段；正式Q01持久RuntimeFlow/Modbus审计；历史缺日志不补造 |
| 008 T055 | 当前正式新DLL＋既有WPF/runtime，Q01 run6333b690-ca2f-4d10-83bf-bce13b8db8fd；实际页面选用/启动/取盘、冻结R008-Q01 1.1.1-test、公共3D/F、A/B每图/融合、XYZ/复位/PLC、SQLite/四媒体读回、完整尾段/Final、刷新重开；18/18，exit0/cleanuptrue |
| T055直接门禁与依赖复用 | 本轮改动仅运行配置/最低线程API及所属启动接线；旧F不匹配不动产品、产品到位/复位/保存失败不Final及USR-E必要XY/XYZ同值/变化Y，沿task-audit-night-20260927最终节、r19/r21必要合同/TRX、r22新恢复AB真实包及已有Q01-PARAM复用各未改业务分支，原来源/构建/配置差异保留；不声称全旧包来自新DLL |
| 008 T070 | 原最终20/22审计＋本轮T065/T055补齐，对账T049—T070原22项、直接依赖子交付、现行SC选定路线/C01—C08/F1—F6；原两轮converge追加0及静态核查复用，业务算法/配方/数据库/前端分支未改；仅追加本Q01一包，不重跑全矩阵/不修改退出Q历史 |

本结论仅为原任务允许的Windows Test/VirtualPlc主流程范围。Native开关显式绑定新正式构建/合法fixture，旧默认/旧冻结程序/全部失败/原assessment及工具fix/test不改。没有声明所有历史故障同因、所有VM调度已解决、r22 HTTP已修复或真实设备/现场标定完成。原3516ms分段日志缺失明确保留。质量清单15/16不动；非阻塞未来工作及生产未知保持原待办。

本轮新增正式Q01一包Passed（run6333b690-ca2f-4d10-83bf-bce13b8db8fd），在旧48次24Passed/24Failed之外单列；原尝试/失败索引、恢复主包、权限拒绝及35个必要用例的原构建适用关系不覆盖。三项原条件满足后，仅003 T065、008 T055/T070变X；008原22任务当前22/22，质量清单仍15/16，不等于生产现场或全项目已完成。详细新assessment/fix/test/proof、源码diff、ETL与正式包摘要见[本轮证据](../../.specify/bugs/t065-communication-delay/mechanism-20260927T042703015Z/evidence-index.md)。

