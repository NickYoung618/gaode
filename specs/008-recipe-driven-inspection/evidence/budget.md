# 008 T051 第二批计数基础（2026-09-25）

## 2026-09-26 夜间预算事实与剩余计数

当前冻结业务公式为`recipe-budget/3.1-test`，实际运行来自所选版本计划；原2.0等历史值保留，不能作为当前预算。GROUP-A-E当前合法路线总预算1,842,000ms。`scripts/get-008-page-budget.ps1`读取真实目录、计划及现有预算，页面外层另加公共准备、120,000ms采证开销和180,000ms启动/导出/清理开销；这些开销不是业务期限。每job的实际值和程序集摘要写入r4 `queue/job-*.started.json`，同run冻结期限见运行包。

现存分拣计数每应搬实体仅加入一段XyCompletion和一份CriticalSave，尚未完整包含取放两段、ACK及预留/在途提交。该实现缺口由原T051承接，先在spec/contracts/plan中记录，待本冻结队列结束后修正公式版本和必要计数测试。当前实际取放成功及未超期事实仍有效，但不足以关闭合法上界计数要求。不得追改已冻结deadline，不增加PLC I/O一秒或心跳三秒上限。

本批当前GROUP-F混合及纯Pending两条各为两个组/四成员/六面，仅一个问题成员真实取放，其余三个保留。图数、融合数、初始3D/F与额外E应分别按已退出包核对；其余正在运行或排队的路线尚不计通过。


状态：**部分实现，T051 未勾选**。`backend/src/Gaode.Application/Workflow/RecipeWorkload.cs` 从计划中的实际 Capture、独立同面目标和整盘重扫步骤计数，单面 Q01 描述计划得到 C=2、F=1、A=3、重扫=0；`backend/tests/Gaode.Contracts.Tests/TestResults/q01-catalog-second-batch-final.trx` 4/4。此计数只描述工作量，**不批准 Restricted 配方派发**，也不是运行耗时。

Q01 现有 Test 配置给出采集上界 5 秒、worker 模拟每调用 10 秒，故检测采集/worker 小计 2×5 + 3×10 = 40 秒；这仍不含公共准备、产品运动及复位、保存、整盘收尾和有限重试，不能用作整盘期限。后续须按已确认配置把动作/阶段/整盘绝对 deadline 与版本引用保存并检查单面/多槽/四面；外部点位/高度映射缺失不允许借预算推导派发。3 秒心跳保护不变。

## 第三批增量（部分，未勾T051）

`RecipeExecutionBudget.Freeze`现按冻结计划和`budget.virtual-loop.json`版本计算Detection/Sorting/Unload绝对期限，`StartPublicPreparation`在PLC配方绑定前将计划摘要、预算ID/版本、工作量和期限作为必要Audit写入；后续DetectionRequest与外围阶段消费这些期限。Q01描述计划：C=2、F=1、重扫=0；该Test预算下Detection 76秒（产品定位16、采集10、worker30、已列必要保存20），Sorting 10秒，Unload 10秒。`PortEnvelope`使用剩余绝对期限，不再另造120秒产品端口期限。合同测试`RecipeExecutionCoordinatorTests.Q01DeadlinesAreAbsoluteAndCountCaptureFusionMotionAndSaves`通过。该公式尚缺已批准产品复位时长、适用光源配置和有限重试上界，不能把76/10/10当作最终整盘批准预算；Q01仍Restricted，未产生真实运行耗时或完整期限证据。

第四批接线后，公式标记`recipe-budget/2.0-test`，新增每次产品受理2秒、检测复位Test上界5秒、worker释放宽限2秒/调用及每次定位和复位必要提交；同一Q01描述计划的Detection变为104秒，Sorting/Unload各10秒。5秒是当前组件Test复位上界，仍非现场批准预算；实际Test单元使用单独4分钟组件期限，不能据此声称正式Q01可派发。合同期限测试已同步并通过。

## 2026-09-27 当前 r5 预算（正式验收进行中）

上一节3.1及历史失败保留。当前源码与默认正式构建已升级 `recipe-budget/3.2-test`，见 r5/build-freeze.json 与每 job.started.json。仅按已接入动作调整计算，不修改配置上限和历史运行期限。

| 项目 | 当前合法 Test 计数 |
|---|---|
| 每普通分拣实体 | 2×XY(8秒)+ACK(2秒)+17×I/O(1秒)+有限预检退避(7秒)+7×关键读写(2秒)+2×轮询(50毫秒)=56.1秒 |
| 有普通分拣可能的下料 | XY(8秒)+16×I/O(1秒)+退避(7秒)+5×关键读写(2秒)=41秒 |
| 仅特殊出口、无普通分拣的下料 | 37秒；特殊出口已在检测步骤计入，不在盘末重复计算 |
| 无普通分拣实体 | NoAdditionalSortingRequired 必要提交2秒 |
| Q01 | Detection108秒、Unload41秒、Sorting56.1秒，总205.1秒；公共准备99秒另列 |

7次分拣关键读写为4次写及3次读；下料5次包括原3次写与批次预留读/写。实际运动端口已接入每段 XY 8秒、ACK2秒，关键存取2秒，并仍受剩余绝对期限限制。定向失败测试证明取料提交失败不派发放料、局部运动超时不等待全盘期限。必要计数测试在 r5/tests/sorting-budget-affected.trx 通过。正式每条路线计数/冻结期限/实际耗时仍待其已结束包对账，T051未提前关闭。

### 已退出包的实际调用核对（2026-09-27，继续增量）

实际依据为各包 budget-call-audit.json 和独立worker-protocol.jsonl；所有列均来自已退出且cleanuptrue的同run，初始Height/FDecode各1、翻后重扫0，每call实际有返回且每输入租约释放一次，Detection/Unload/Sorting实际完成提交均在本run冻结绝对deadline内。审计12项true；不以计划计数代实际执行。

| 原批及job | 实际Detection | 实际EDecode | 场景整包状态 |
|---|---:|---:|---|
| r5 001 Q01-PAUSE | 3 | 0 | 原验收子范围通过 |
| r5 002 Q01-NG | 3 | 0 | 原验收子范围通过 |
| r5 003 Q02-PENDING | 6 | 0 | 原验收子范围通过 |
| r5 004 GROUP-F-MIXED | 18 | 0 | 原验收子范围通过 |
| r5 005 ASSEMBLY-A-E-NG | 9 | 1 | 原验收子范围通过 |
| r5 006 ASSEMBLY-A-E-PENDING | 9 | 1 | 场景审计Failed，预算只作通过子范围，独立复验待运行 |
| r5 007 ASSEMBLY-A-E-NOCODE | 9 | 1 | 原验收子范围通过 |
| resume-1 010 ROT-PART-NG | 6 | 0 | 原验收子范围通过 |

全部默认业务程序摘要沿r5冻结，恢复仅两项必要工具文件修改。当前GROUP-A-E公式3.2合法路线2241800ms（规划记录），与上文历史3.1的1842000ms分开；尚未实际运行新包，不能提前填写耗时通过。其他未结束代表继续核对。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
