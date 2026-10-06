# 功能规格：OperationIngress 参数诊断

**功能标识**：005-ingress-parameter-diagnostics  
**创建日期**：2026-09-21  
**状态**：草稿，可进入技术方案阶段  
**输入**：request `b7f458ee21b94a579d37db5c9c779807`：修正 `OperationIngress` 构造函数参数校验，补充规则测试，并保持现有运行时行为不变。  
**宪章版本**：1.1.0

> 本规格只覆盖后端 `OperationIngress` 构造函数的参数错误归属及其规则测试，不扩展期限策略、接口、设备或数据职责。

## 来源与职责边界

| 来源 | 章节 / 需求ID / 原则 | 本功能采用规则 | 冲突裁决或说明 |
| --- | --- | --- | --- |
| 用户当前决定 | request `b7f458ee21b94a579d37db5c9c779807` | 非法 `duplicateSummaryLimit` 或 `operationLimit` 时，异常必须指向实际非法参数；补充规则测试；保持运行时行为 | 用户决定限定本次修正范围，优先于既有缺陷实现 |
| 现有实现 | `backend/src/Gaode.Application/Timing/OperationIngress.cs` 构造函数及证据跟踪逻辑 | `duplicateSummaryLimit < 0` 非法，`operationLimit <= 0` 非法；`lateDetailLimit` 继续非负校验；其他边界与证据上限保持不变 | 当前联合条件固定以 `lateDetailLimit` 作为异常参数名，属于待修正诊断缺陷，不是新业务规则 |
| 现有规则测试 | `backend/tests/Gaode.Rules.Tests/Timing/OperationIngressTests.cs` | 延迟、重复摘要和跟踪操作上限继续作为回归基线 | 新测试只补充参数归属，不删除或放宽既有断言 |
| 宪章 | P01、P05、P09、P10 | 保留需求追溯、纯后端职责、有限有界证据和可判定的软件验证 | 不涉及设备、数据库、前端或生产参数 |
| REQ V1.1 / ARCH V1.3兼容部分 | 不适用 | 本功能不新增业务流程、设备动作、数据字段或架构边界 | 用户诊断决定与既有应用层实现已足够；不据此引入无关需求 |

**后端本次实现**：构造函数对两个目标参数的独立校验及 `Gaode.Rules.Tests` 中可判定的异常参数名测试。  
**本次不包含**：期限判定、证据跟踪算法、公共 API 形状、数据库、设备、前端、独立虚拟下位机和性能调优。  
**外部协作边界**：无外部设备或服务依赖；测试使用现有时钟/调度器替身。  
**独立开发方式**：在隔离的后端规则测试入口中构造 `OperationIngress`；不得连接设备或生产库。

## 用户场景与软件验证

### 场景 US1：构造函数报告准确的非法参数（优先级：P1）

调用者配置重复摘要和证据操作保留上限。参数非法时，构造应立即抛出 `ArgumentOutOfRangeException`，且 `ParamName` 精确对应实际非法参数；参数合法时应成功构造。

**对应需求/原则**：用户当前决定；P01、P09、P10。  
**优先理由**：准确的参数归属直接影响调用方诊断，且不需要改变运行时流程。  
**独立验证**：`Gaode.Rules.Tests.Timing.OperationIngressTests` 的构造函数规则测试。

| 场景 | 给定 | 当 | 则：状态、保存与后续处置 | 证据方式 |
| --- | --- | --- | --- | --- |
| `duplicateSummaryLimit` 非法 | `duplicateSummaryLimit < 0`，其余参数合法 | 构造 `OperationIngress` | 抛出 `ArgumentOutOfRangeException`，`ParamName == "duplicateSummaryLimit"`；不进入运行时处理 | xUnit 异常断言 |
| `operationLimit` 非法 | `operationLimit <= 0`，其余参数合法 | 构造 `OperationIngress` | 抛出 `ArgumentOutOfRangeException`，`ParamName == "operationLimit"`；不进入运行时处理 | xUnit 异常断言 |
| 合法边界 | `duplicateSummaryLimit == 0`、`operationLimit > 0` 且 `lateDetailLimit >= 0` | 构造并执行既有证据路径 | 构造成功；重复摘要、迟到详情和操作跟踪保持既有语义 | 新增边界断言与现有回归测试 |

### 场景 US2：既有期限和证据行为不回归（优先级：P1）

保留现有已接受、迟到、超时、重复、迟到详情截断、重复摘要和有界操作跟踪断言；参数诊断修正不得改变这些结果。

**对应需求/原则**：用户当前决定；P05、P09。  
**独立验证**：运行现有 `Gaode.Rules.Tests` 相关测试，不使用真实设备或生产数据。

## 边界与失败场景

- `duplicateSummaryLimit == 0` 仍是合法值；不得把“无重复摘要”误判为非法。
- `operationLimit == 1` 仍是合法正值；`operationLimit == 0` 及负值非法。
- `lateDetailLimit < 0` 的既有校验和参数名必须保持不变；其合法边界不得被本功能改写。
- 多个参数同时非法时的优先级未被用户确认；测试一次只违反一个参数，不把执行顺序写成业务规则。
- 不得通过吞掉异常、放宽校验、修改异常类型或删除回归断言来“修复”测试。
- 不改变 `Receive`、`ReceiveIfExpired`、`GetEvidence`、证据操作淘汰或期限判定的运行时语义。

## 功能需求

| 功能需求编号 | 必须 / 不得行为 | 需求来源 | 宪章原则 | 软件验证场景 |
| --- | --- | --- | --- | --- |
| FR-001 | 当 `duplicateSummaryLimit < 0` 且其他构造参数合法时，必须抛出 `ArgumentOutOfRangeException`，其 `ParamName` 为 `duplicateSummaryLimit`。 | 用户当前决定；现有构造参数语义 | P01、P09 | US1-非法 `duplicateSummaryLimit` |
| FR-002 | 当 `operationLimit <= 0` 且其他构造参数合法时，必须抛出 `ArgumentOutOfRangeException`，其 `ParamName` 为 `operationLimit`。 | 用户当前决定；现有有界跟踪语义 | P01、P09、P10 | US1-非法 `operationLimit` |
| FR-003 | `duplicateSummaryLimit == 0`、正 `operationLimit` 及其他既有合法边界必须继续构造成功，并保持证据语义。 | 现有实现与规则测试 | P05、P09 | US1-合法边界、US2 |
| FR-004 | `lateDetailLimit < 0` 的既有校验行为和 `ParamName` 必须保持。 | 现有构造参数语义 | P05、P09 | US2-构造校验回归 |
| FR-005 | 参数校验修正不得改变期限判定、重复/迟到结果、详情上限、溢出计数、重复摘要存在性或操作跟踪数量上限。 | 用户当前决定；现有规则测试 | P05、P09、P10 | US2-运行时回归 |
| FR-006 | 规则测试必须分别断言两个目标参数的异常类型及 `ParamName`，并保留现有运行时断言。 | 用户当前决定 | P01、P09 | US1、US2 |

## 配方与启动快照约束

不适用。本功能不涉及配方、固定点位、启动快照、设备动作或配置驱动能力；不得借此新增相关任务。

## 关键对象与状态

- **OperationIngress**：应用层期限结果入口，接收已注册操作的结果并维护有界证据；本功能只约束其构造参数校验。
- **duplicateSummaryLimit**：重复结果摘要的既有构造参数；负值非法，零值仍合法。
- **operationLimit**：被跟踪证据操作数量上限；正值合法，零值及负值非法。
- **lateDetailLimit**：迟到详情上限；保留现有非负校验和运行时截断语义。
- **ArgumentOutOfRangeException.ParamName**：调用方诊断所需的可观察参数身份，必须对应触发失败的参数。

本功能不改变技术执行状态、检测完整性、质量结论、物理分拣、任务完成或模拟/真实来源状态。

## 成功条件与证据

| 编号 | 可观察且可验证的后端结果 | 方法与证据 | 适用范围/限制 |
| --- | --- | --- | --- |
| SC-001 | `duplicateSummaryLimit` 非法输入报告 `ParamName == "duplicateSummaryLimit"`。 | 规则测试构造非法值并断言异常类型和参数名 | 进程内规则测试；不代表生产环境验证 |
| SC-002 | `operationLimit` 的零值和负值均报告 `ParamName == "operationLimit"`。 | 规则测试分别覆盖 `0` 与负值 | 进程内规则测试；不定义多参数同时非法优先级 |
| SC-003 | 现有期限、迟到/重复证据和有界跟踪测试继续通过，合法 `duplicateSummaryLimit == 0` 不被拒绝。 | 现有 `OperationIngressTests` 回归及新增合法边界断言 | 不声称设备、节拍、算法精度或生产库验证 |
| SC-004 | 规则测试覆盖 FR-001 至 FR-006，未删除或放宽既有行为断言。 | 测试代码审查与规则测试结果 | 仅限本功能授权的后端规则范围 |

## 待补充与依赖

| OPEN编号 | 来源与缺失项 | 只限制的功能/动作 | 补充时机 | 可独立推进内容 | 测试配置标识 |
| --- | --- | --- | --- | --- | --- |
| 不适用 | 本功能为纯参数诊断和规则测试修正，不消费现场设备、算法、存储或接口参数 | 不限制任何真实设备动作；本功能本身不执行设备动作 | 不适用 | 可在现有隔离规则测试入口完成 | 现有规则测试替身；非生产配置 |

## 范围结论

本规格只定义 `OperationIngress` 构造函数中两个参数的精确异常归属、既有边界保持和对应规则测试。实现阶段不得扩大到新的期限策略、证据模型、设备联调、数据库或其他功能规格。
