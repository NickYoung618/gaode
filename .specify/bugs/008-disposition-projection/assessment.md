# Bug Assessment: 已提交处置事实未进入运行结果投影

- **Slug**: 008-disposition-projection（当前实际页面复核）
- **Created**: 2026-09-27
- **Source**: r5 Q02-PENDING Final截图、API与已退出SQLite；旧Q03-NG查询对照
- **Verdict**: valid
- **Severity**: high（原FR-010/016、T054/T057/T059/T070、006结果绑定条件）

## Report

r5 `runs/job-003-Q02-PENDING/Q02-PENDING/recipe-04-final.png` 质量Pending与Final正确，顶部配方正确；检测明细的“处置”仍为“未提供”。同run API `results[].dispositionState=null`、`movements=[]`，但SQLite实际有目标预留、可靠取料在途和放料/ACK后的占用提交，P01→P15一次取放。退出及清理已验证。旧前批Q03-NG的movements同样空，故这是既有查询交付缺口，不归因为r5新提交造成的业务失败。

## Reproduction

已实际完成Q02-PENDING正式WPF；刷新/重开后处置未提供。新包 sorting-commit-audit.json 七项true，证明事实存在；不需要重复制造失败。当前操作/质量验收通过仅限其原检查范围，不等于全部处置展示通过。

## Suspected Code Paths and Root Cause

RunResultProjection 已声明可空 DispositionState；008 contracts/api-results.md 已要求已提交搬运/无需搬运依据及movements关联。QueryEndpoints/CommittedResultProjection只接检测结果，没有把已提交Sorting事件或实际特殊出口映射到这些既有字段。运行页面忠实显示缺字段，不能用Final或质量代填。

## Proposed Remediation

先更新003/006/008现有spec/contracts/plan/tasks的直接字段消费者说明，再从同run、同冻结计划的已提交处置事件投影既有movements和对象dispositionState；只在可靠放料/ACK/占用或实际特殊出口证据成立时显示已处置，预留/在途分别表达，未知不得完成。普通OK无需搬运也须已提交无需搬运依据，不从质量/Final猜测。整体按物理Assembly，部位/组不得冒充独立搬运。前端绑定既有区域，不加控件或布局。

保留无旧处置事实的历史null语义，不建兼容层、不补写旧数据库、不新增状态库或设备调用。必要定向投影测试证明仅预留/仅在途不完成、占用有反馈才完成、实体不串联；最少新正式Pending代表复验，NG和整体可与已选定差异共享。当前r5批次先完成已冻结路线及原检查范围，不在正在运行WPF中修改源码/输出；结束后自动reload交班再构建/冻结，不要求用户手动重启。

## Files likely to change

- `backend/src/Gaode.Host/Api/QueryEndpoints.cs`、必要时 `CommittedResultProjection.cs`。
- 现有 `RunMovementProjection` 字段定义（仅确有缺失时；共享设计先行）。
- 相关必要API/查询投影测试及正式采证检查。
- 003/006/008直接合同、计划、任务增量与原证据文件；编号/勾选不因子能力改变。

## Risks & Considerations

不得从PLC日志猜测业务完成，不从NG/Pending或Final填“已处置”。旧成功/失败包保持原内容，新的字段复验单列。此缺口不阻断当前真实检测/取放主链，继续本批；登录自动启动仍等当前验收和必要修复后配置。

## Open Questions

无新增业务决定。既有API字段/实际提交事实可实现；未确认生产参数仍仅局部限制。

## 实施定位补充

原批共享PLC启动通信阻断后所有活动job已退出并清理，队列暂停；实际自动reload到9936且旧worker已退出。先作独立源码/纯投影验证，保留原二进制与fixture，正式复验按新的实际构建登记。除查询两文件，已确认必须给SortingTargetAllocator既有预留事件补ordinaryOk、IntegratedDetectionPort既有Exit提交补真实sourceSlotId/targetPointRef；不增加提交次数或设备动作。这两文件是实际发现的必要生产者范围，plan/contracts/tasks已经先行登记。
