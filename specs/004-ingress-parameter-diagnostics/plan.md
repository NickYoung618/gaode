# 技术方案：OperationIngress 参数诊断

**功能标识**：004-ingress-parameter-diagnostics  
**日期**：2026-09-21  
**规格**：[spec.md](spec.md)  
**宪章版本**：1.1.0  
**范围**：仅后端应用层构造函数及 `Gaode.Rules.Tests` 规则测试；不连接设备或生产库。

## 方案摘要

`OperationIngress` 当前把 `lateDetailLimit < 0`、`duplicateSummaryLimit < 0` 和
`operationLimit <= 0` 合并在一个条件中，并始终以 `lateDetailLimit` 作为
`ArgumentOutOfRangeException.ParamName`。方案将校验拆成按参数顺序的三个独立分支：保留既有边界，
但分别报告触发失败的参数名。随后在现有 `OperationIngressTests` 中加入一次只违反一个参数的异常类型和
`ParamName` 断言，以及零/正边界的构造成功断言；现有期限、重复、迟到详情和有界跟踪测试保持原样。

不新增 API、数据表、运行时状态、期限策略或依赖。实现仍位于 `Gaode.Application`，测试仍使用现有
`Gaode.Rules.Tests`、`FakeTimeProvider` 和 `DeadlineScheduler` 替身。

## 技术上下文

| 事项 | 选用方案 | 决策来源与状态 | 尚缺证据/OPEN |
|---|---|---|---|
| 后端运行时 | .NET SDK `10.0.401`，现有四工程结构 | `global.json`；当前工程配置，版本证据已存在 | 仅需开发环境实际恢复/构建 |
| 目标实现 | `backend/src/Gaode.Application/Timing/OperationIngress.cs` | 现有实现；本功能只改参数校验 | 未执行实现阶段验证 |
| 规则测试 | `backend/tests/Gaode.Rules.Tests/Timing/OperationIngressTests.cs` | 现有 xUnit 入口和测试替身 | 未新增测试，直至 implement 阶段 |
| 依赖 | `Gaode.Domain` 项目引用；`Microsoft.Extensions.TimeProvider.Testing` | `Gaode.Application.csproj`、Rules 测试项目文件 | 不安装新包 |
| 数据/设备 | 不涉及 | spec 明确排除 | 不适用 |
| 性能与容量 | 保留已有 `lateDetailLimit`、`duplicateSummaryLimit`、`operationLimit` 语义 | 现有代码及回归测试 | 不定义生产预算 |
| 软件验证 | 规则测试 + 代码审查；实现后按既有隔离测试入口运行 | spec FR-001 至 FR-006 | 未代表真实设备/生产验证 |

## 宪章检查

| 原则 | 本功能检查点 | 设计前 | 设计后 | 证据/受限范围 |
|---|---|---|---|---|
| P01 | 用户请求、004 spec 与现有代码/测试可追溯 | 符合 | 符合 | `spec.md`、`OperationIngress.cs`、测试文件 |
| P02 | 仅后端规则；无前端、设备、虚拟下位机 | 符合 | 符合 | 方案仅列 Application 与 Rules.Tests |
| P03 | 不涉及固定点位/采集/配方 | 不适用 | 不适用并说明 | 纯参数诊断 |
| P04 | 不改变有限等待或安全动作 | 不适用 | 不适用并说明 | 不触及期限/设备路径 |
| P05 | 保留 Domain/Application 边界和现有入口 | 符合 | 符合 | 仅构造函数分支及规则测试 |
| P06 | 不改变并发、队列、资源所有权 | 不适用 | 不适用并说明 | 参数校验发生于构造期 |
| P07 | 不改变身份、结果、迟到/重复关联 | 符合 | 符合 | 现有运行时断言作为回归 |
| P08 | 不涉及保存、数据库或恢复 | 不适用 | 不适用并说明 | 无持久化改动 |
| P09 | 可判定后端验证，保留替身边界 | 符合 | 符合 | xUnit 异常断言与现有回归 |
| P10 | 不消费 OPEN 或生产参数 | 符合 | 符合 | 无现场依赖 |
| P11 | 不新增配置/策略能力 | 不适用 | 不适用并说明 | 构造参数诊断不改变能力注册 |

## 结构与职责

| 模块/文件 | 状态或资源所有者 | 依赖方向 | 外部对接边界 |
|---|---|---|---|
| `Gaode.Application/Timing/OperationIngress.cs` | `OperationIngress` 构造函数负责参数不变量 | Application → Domain 的既有 `OperationKey`/结果类型 | 无设备、HTTP、数据库边界变化 |
| `Gaode.Rules.Tests/Timing/OperationIngressTests.cs` | xUnit 测试独立创建 scheduler/clock | Tests → Application/Domain | 仅进程内替身，不分派外部开发 |

不得复制新的校验逻辑到调用方；不得修改 `Receive`、`ReceiveIfExpired`、`GetEvidence`、`Track` 或
`DeadlineScheduler`。

## 数据、契约与状态

本功能无新增持久化模型或对外契约。构造参数不变量如下：

| 参数 | 非法条件 | 合法边界 | 异常 `ParamName` |
|---|---|---|---|
| `lateDetailLimit` | `< 0` | `0` 及正数 | `lateDetailLimit`（保持既有行为） |
| `duplicateSummaryLimit` | `< 0` | `0` 及正数 | `duplicateSummaryLimit` |
| `operationLimit` | `<= 0` | `1` 及正数 | `operationLimit` |

多个参数同时非法的优先级不作为本功能规则；测试一次只违反一个参数。运行时证据快照、操作淘汰、
重复摘要存在性和迟到详情截断均保持既有实现。

## 配方与动作隔离

不适用。无配方、固定点位、启动快照、动作、算法或保存逻辑；不生成相关任务，也不创建生产库。

## 并发、资源与异常出口

构造校验在任何调度注册、接收和证据跟踪前完成，因此不持有或释放运行时资源。非法参数立即抛出
`ArgumentOutOfRangeException`；合法参数进入既有运行时路径。该设计不改变任何等待期限或后台工作。

## 保存与恢复

不适用。该类没有本功能新增的文件、数据库或恢复步骤；既有证据仅存于运行时内存并保持原语义。

## 软件验证与证据计划

详细矩阵见 [verification.md](verification.md)。实现阶段至少验证：

1. `duplicateSummaryLimit = -1` 抛出 `ArgumentOutOfRangeException` 且 `ParamName` 精确为该参数。
2. `operationLimit = 0` 与负值分别报告 `operationLimit`。
3. `lateDetailLimit < 0` 仍报告 `lateDetailLimit`，合法零边界可构造。
4. 原有期限、迟到/重复、详情截断、溢出计数和跟踪数量测试未删除、未放宽且继续通过。

验证产物由实现/verify 阶段写入既定 artifacts 或测试结果目录；本 plan 阶段不运行产品测试。

## OPEN、外部依赖与决策记录

本功能不消费 REQ 现场 OPEN。依赖仅为当前四个后端工程、锁定 .NET SDK 和已有 xUnit 测试包；不安装
新 SDK/厂商包。采用显式顺序校验而非复合条件，因为异常参数归属是用户明确的可观察要求；不定义多参数
同时非法的优先级，以遵守 spec 的未确认边界。

设计复核结论：范围保持在 004 spec，既有运行时行为通过回归测试保护，未新增前端、虚拟设备、数据库或
生产承诺。
