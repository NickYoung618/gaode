# 008业务确认文档核验（2026-09-26）

本记录是文档写入后的只读核验，不是业务测试或完整共享接口设计通过。来源与决定见[business-decisions-20260926.md](business-decisions-20260926.md)。

## 核验结果

| 项目 | 结果 |
| --- | --- |
| 来源保护 | 28项SHA-256保持，包括协议、源场景Excel、原PNG/SVG、原型、AGENTS、宪章与feature选择 |
| 任务 | 274个任务编号和勾选保持 |
| 需规同步 | Word 32个受影响条目与MD读回一致，52张表结构保留；XLSX 21个需求/OPEN条目行为读回一致 |
| Office范围 | Word仅document.xml变更，内嵌图及其他成员保持；XLSX仅sheet2/3/12变更，其他成员及开发状态I列保持 |
| 链接与图 | 本次Markdown链接无缺失；008的4段Mermaid正文未变，不重复渲染 |
| 008范围 | 18项FR、6项SC保留；T049—T070编号及勾选保持，变更相关未完成任务正文 |
| 澄清 | 采用用户已提供答案，未重复提问；spec新增5条合并澄清记录，详细拆为U01—U08避免与覆盖矩阵C编号混淆 |
| 规格质量清单 | 既有16项勾选未变；本次核对来源/业务规则/可测试行为与任务承接，不重写历史质量记录 |

## 本次改动文件

下面14项包含新决定记录及13项既有文档；本核验记录另新增。
- [specs/008-recipe-driven-inspection/business-decisions-20260926.md](business-decisions-20260926.md)
- [specs/008-recipe-driven-inspection/spec.md](spec.md)
- [specs/008-recipe-driven-inspection/contracts/execution.md](contracts/execution.md)
- [specs/008-recipe-driven-inspection/plan.md](plan.md)
- [specs/008-recipe-driven-inspection/tasks.md](tasks.md)
- [specs/008-recipe-driven-inspection/data-model.md](data-model.md)
- [specs/008-recipe-driven-inspection/contracts/api-results.md](contracts/api-results.md)
- [specs/008-recipe-driven-inspection/sequences.md](sequences.md)
- [specs/008-recipe-driven-inspection/recipe-cases.md](recipe-cases.md)
- [specs/008-recipe-driven-inspection/implementation-checklist-20260926.md](implementation-checklist-20260926.md)
- [specs/008-recipe-driven-inspection/contracts/evidence.md](contracts/evidence.md)
- [软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.md](../../软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.md)
- [软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.docx](../../软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.docx)
- [软件需求规格说明书/需求开发跟踪表_V1.1.xlsx](../../软件需求规格说明书/需求开发跟踪表_V1.1.xlsx)

## 剩余设计工作

本次确认不是实现完成。修改共享接口前，003/006/002仍须按决定同步各自spec/contracts/plan/tasks；特别是旋转Test请求/结果的传递与关联、恢复新尝试的有限预算和对应页面绑定。它们归软件设计/实施，不再要求用户提供已延期的真实角度、坐标高度或料盘容量。旧历史报告/现有覆盖快照保留，不回填运行结果。

本次没有启动或停止平台、运行业务测试、改源码/运行配置/fixtures、写数据库、改原型或修改历史证据。文件备份和逐文件前后哈希在本机临时目录 `C:\Users\codexsandboxonline.10_3_0_13\AppData\Local\Temp\gaode-business-decisions-20260926`；核验时间窗口之外其他会话的运行产物不纳入不变承诺。

## 关键来源摘要

| 来源 | SHA-256 |
| --- | --- |
| `检测场景及零件整理(20260313).xlsx` | `2d3d0494b960fe04dab5fc997bdb7a2d9b9c6406d3cfe23b060d32e5592657c3` |
| `PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx` | `405ac9ee2ae2d765951d9f523dc7195cc77f6cd1f38dbc0ad144a0013586c519` |
| `原型.zip` | `3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0` |
