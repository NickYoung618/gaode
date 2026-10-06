# 007 数据与证据模型

**日期**：2026-09-23。仅描述007新增关联及现有003/001事实的复用；不规定新数据库表或迁移。所有运行证据来自正式端口和实际SQLite/媒体存储，不能从日志或页面反推已提交。

| 对象 | 本阶段必需字段/关联 | 校验与来源 |
| --- | --- | --- |
| 联调配置快照 | configId/version/purpose=Test、固定图片根与清单摘要、业务预算引用、配方候选、随机种子/范围、worker协议/版本、启用模拟取盘标记 | 启动前版本化；只用于Virtual/Simulated组合；当前run不可因文件/配置改动而漂移 |
| 运行身份 | runId、trayId、stationId、lineId、scenarioId、occupiedSlots、requestId、启动渠道/身份 | 由正式StartRunContext和Host持久受理；同run/tray贯穿全部阶段；前端启动和辅助脚本启动分开 |
| 配方/计划快照 | F原始码及结果引用、recipeId/version、planRef/revision、完整步骤/必检对象、测试来源 | F必须通过实际worker返回合法唯一值后绑定；无预造handoff；当前计划全部必检项均执行 |
| 固定图片输入 | 用途/role、相对文件标识、文件长度/格式/SHA-256、清单版本 | 固定一个本地根，运行前冻结；只读读取；路径本身不证明采集成功 |
| 采集/媒体事实 | runId、captureId、operationId/attempt、计划步骤/对象、相机role、输入摘要、采集起止及真实耗时、mediaId、持久化引用、source/quality | 每次正式触发3–5秒；相同图片字节允许，但每次调用身份和保存事实必须独立；缺图/保存失败不伪造引用 |
| 算法调用/响应 | callId、operationId/attempt、runId/captureId或输入媒体组、workerSessionId/PID、role、deadline、请求和响应摘要、实际计算起止、随机种子/范围/输出、source/quality/错误 | 独立进程实际接收和处理，每有效请求模拟计算10秒后返回；结果身份与所有输入一致；无响应按有限失败终态 |
| PLC动作/观察 | operationId、stage、planRevision、连接代次、命令/反馈点位摘要、时间、intent/feedback写入ID、source | 动作意图先提交；只信当前代次匹配反馈；UnknownHeld保留占用且不盲重发 |
| 阶段/完成事实 | Detection、Sorting、UnloadPreparation事件，WholeTrayCompletion、ReadyForUnlockSourceMatrix、ObservedUnlocked、模拟取盘确认、FinalSourceMatrix、FinalUnloadCompletion及各writeId/revision | 顺序依003；整盘与解锁不是最终完成；确认及最终完成原子保存 |
| 组件来源矩阵 | Host、PLC、Camera、Light、Algorithm、ManualActor的source/quality/version/evidenceReferences、matrixDigest/evidenceScope | 复用003结构；ManualActor此次来自Test/Simulated受控确认客户端，必须在003来源合同最小同步后正确表达；不能写AuthenticatedHuman |
| 联调证据索引 | evidenceId、runId/trayId、样本/代码/合同版本、进程/配置、原型哈希、文件摘要、各证据路径、结果状态 | manifest只作索引；Passed依赖实际查询、媒体和SQLite；无证据写Blocked/NotRun |

## 身份与状态关系

```text
联调配置快照 ──1:N──> RunIdentity(runId/trayId)
RunIdentity ──1:1──> F识别与冻结RecipeRunPlan
RunIdentity ──1:N──> CaptureFact ──1:N──> 持久化MediaRef
CaptureFact/MediaRef ──N:1或N:N──> AlgorithmCall ──1:1──> Response或有限失败
RunIdentity + PlanRevision ──1:N──> PLCActionIntent/MatchedFeedback
已提交检测/分拣/下料 ──> WholeTrayCompletion/ReadyForUnlockMatrix
WholeTrayCompletion ──> ObservedUnlocked ──> Test模拟确认
Test模拟确认 + 已核验来源矩阵 ──> FinalUnloadCompletion
```

本阶段每个检测采集步骤对应一次独立算法请求，使用该步骤可追溯MediaRef并真实计算10秒。CAP/P01预期两次检测采集与两次算法请求；实施前在003共享合同中同步此接线，并用正式生成计划及实际调用数核算120秒，不能为预算跳过任何算法调用。

## 状态迁移与完成条件

公共准备及handoff经003正式流程自动进入Detection；随后Sorting、UnloadPreparation，三阶段完成事实提交后才聚合WholeTrayCompletion。Host发起解锁并收到当前代次匹配反馈后提交ObservedUnlocked，运行处于AwaitingManualRemoval。启动时已启用的联调客户端读取同run的提交事实后自动调用受控确认API，服务端核对revision/权限/整盘/解锁，原子提交ManualTrayRemovalConfirmed与FinalUnloadCompletion。自动模拟确认的语义仅为软件联调中的Test操作，不能声称真实人工移盘。

技术状态、质量OK/NG/Pending、物理处置和运行完成必须分别记录。关键保存失败或提交未知时停止依赖动作，不能靠页面或内存状态报告完成。算法失败收敛为带原因的Pending并在映射合法时走正式Pending分拣；F失败/不唯一阻止配方绑定；PLC动作未知为UnknownHeld且不重发。全部Virtual/Simulated/Test证据最终为SoftwareLoopOnly、productionClaimAllowed=false。

## 复用边界

现有001/003事件、媒体、计划、whole-tray和SQLite事实是权威存储；本模型仅增加固定文件来源、独立worker调用和Test模拟确认所需的最小关联。具体字段与API签名以受影响共享合同同步后的版本为准，不因本文件单独改变003数据库或006前端合同。

## 2026-09-24 008完整执行模型增量

旧模型保留其历史范围。最新完整执行扩展以[008数据模型](../008-recipe-driven-inspection/data-model.md)为准：真实物理槽/源目标、面/成员/组/整体结果、动作/高度轮次、目标预留、来源及冻结预算；旧占位坐标/采集对象不得当作真实搬运实体。沿用已有意图/事实短事务，不回填旧记录。相关实现任务见本功能008对齐增量，当前仅设计。
