# Bug Fix: 已提交物理处置接入现有运行查询

- **Slug**: 008-disposition-projection（沿当前assessment上下文）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary

从同run/tray/plan的已提交事实投影既有dispositionState/movements，并将处置结果及事件纳入resultRevision/ETag。保留质量、完整性和流程终态的独立语义。

## Changes

| File | Change | Notes |
|---|---|---|
| CommittedResultProjection.cs | 预留、取料在途、可靠放料/ACK后完成、UnknownHeld；明确无需搬运；真实特殊出口 | 仅Single/Member/Assembly；身份、计划、epoch和反馈一致；无设备调用、回填或新状态库 |
| QueryEndpoints.cs | 读取Sorting事实并组合实际检测出口；响应和ETag纳入物理投影 | 沿既有GET字段，无新API |
| SortingTargetAllocator.cs | 既有预留提交补已核验ordinaryOk实体 | 不增加提交或派发次数 |
| IntegratedDetectionPort.cs | 既有Exit事实补实际sourceSlotId和配置targetPointRef | 不使用Test来源标识冒充物理点；不增加动作/提交 |
| 两个必要测试文件 | 纯投影门禁、混合盘正常成员留原位 | 无实时SQLite读取或真实设备 |

## Validation

resume-2/tests/disposition-projection-r2.trx：15/15；disposition-producer-r2.trx：15/15。两个首次编译失败分别为新测试FixedPoint构造参数漏项与xUnit2031分析器要求，日志保留，修正只涉及测试。

旧Q02-PENDING已结束且cleanuptrue的实际SQLite只读输入，经新独立Host AE559F3573029D42B0E59610EDFCD9D56BEB500FA239E95D6BAFAB319D357813 投影P01→P15 Completed，质量仍Pending，事件引用真实；P03缺旧ordinaryOk依据保持null，Face保持null。见resume-2/ended-q02-projection-result-r2.json。首次离线适配仅SQLite日期空格格式不能反序列化，r2保留原输入并转换为等值ISO格式；原数据库及历史报告不改。

## Deviations from Assessment

共享PLC启动通信阻断后队列暂停，已自动接班且无活动验收作业，先进行源码/独立输出验证；未覆盖原默认程序或fixture。生产者两文件范围为实际必需，已在assessment补充并先行更新plan/tasks/contracts。正式新Pending/P03和特殊出口页面复验尚未完成，不能称完整缺陷验证通过。
