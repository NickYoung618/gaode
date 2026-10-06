# Bug Assessment: 长路线PLC变化采证未翻页

- **Slug**: 008-page-plc-pagination（本轮无人值守上下文生成）
- **Created**: 2026-09-26
- **Source**: r4 job-003 GROUP-A-E正式WPF实际失败包
- **Verdict**: valid
- **Severity**: high（阻断已授权长路线完整验收）

## Report

`page-next-closure-20260926-night-r4/queue/job-003.result.json`退出1，cleanupVerified=true；原包保留。`operation-route-validation.json`仅`actual_flip_ack_set_and_clear=false`，其他16项包含真实同run页面Final、目录/冻结、完整相机实体面序列、媒体/独立worker/完整尾段等均true。不能将实际Final与完整验收通过混为一谈。

## Symptom and Reproduction

该真实长路线已运行；导出`plcChanges.oldestSequence=1/latestSequence=2406/gap=false`，但changes仅1024项，末项1024发生在23:07:23，之后的翻面ACK被遗漏。无需再制造同一失败。成组结果含两个组、八个成员、十四个面，API/SQLite读回已结束且所属资源释放。独立场景审计也保留job退出1事实，不改为Passed。

## Suspected Code Paths and Root Cause

`scripts/verify-q01-q02-test-page.ps1`只请求一次`/api/simulator/changes?after=0`。既有`VirtualPlc/PlcDataStore.cs.GetChanges`每页1024项，`latestSequence`为真实最新序号而非本页最后项，调用者须继续传after。实际2406项在8192保存容量内，无丢失；不需要改变协议、容量或API。

## Proposed Remediation

仅修改导出工具，首次读取后冻结latestSequence，按最后已收到序号继续请求到该目标；检查gap和cursor有进展，拒绝未完整的变化证据。既有每请求十秒期限不变，冻结末序号使心跳继续变化不会导致永久采集。不扩大模拟器接口或容量，不改变业务deadline，不修改运行中的冻结脚本。当前队列结束后加载新冻结worker，再以独立新job/new run重验GROUP-A-E。可用已保存2406总数/1024第一页及实际写入审计复核根因，但该复核不是重验通过，不覆盖旧失败。

## Files likely to change

- `scripts/verify-q01-q02-test-page.ps1`：完整游标导出。
- `scripts/validate-008-operation-evidence.py`：必要完整性检查，旧失败保留。
- 现有008证据记录：新job与失败根因/结果关联。

## Tests to add or update

采证工具的有限分页检查（冻结目标/实际cursor推进），以及该已失败长路线的必要正式WPF重验。无需新PLC故障矩阵或全路线重跑。

## Risks & Considerations

不得用计数假补ACK，或用Host日志冒充PLC实际信号；原失败及第一批采证保留。其他正在运行路线不受导出文件改动干扰。

## Open Questions

无新增业务决定。
