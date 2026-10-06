> 2026-09-21 用户授权增量：外部PLC、XYZ、PC_Start_Cmd按钮、15/15及F后独立配方按 `specs/002-plc-xyz-recipes` 执行。下文XY-only及仅进程内模拟是原001基线，保留用于FullSimulation回归；不得用于否决002的新要求。第一工位移交前仍无配方调用。

# 设计追溯与软件验证方案

**版本**：1.0.1　**日期**：2026-09-20　**基线**：spec1.1.0 / plan1.0.1 / tasks1.0.1。以下验证全部待实现后执行；本次只做文档和JSON设计资产检查。

2026-09-24 增量矩阵（不改写上方历史范围）：FR-041 → T087、`contracts/api.md` 的 `startupDiagnostic`、启动/设备受控日志；SV-36 → 已受理后首次通信失败与同代次明确不安全对照，检查请求/回执/GET/原始日志/无后继动作；SC-011 → 进程退出后凭保存的日志和索引定位停止阶段、判定依据、原始异常或未知原因、实际处置。建运行前连接失败单列拒绝/Host未就绪，不冒充SV-36。上述三项必须以本次新证据独立判定，旧SV-22/SC-008不抵扣；实际页面证据归006 T046及007 T027。

## 1. 验证分类与共同断言

R=纯规则/可控时钟；C=端口契约（假传输/假保存输入，不接设备）；I=进程内模拟/实际时间Host集成；P=隔离SQLite与媒体/中断恢复；H=真实设备或组件能力。R/C/I/P通过不等于H通过。P必须通过独立Test库准备及维护锁，禁止Host自动建库。

每个场景都检查：产品配方查找/选择/匹配/匹配后加载及后续计划/工位调用数为0；F未到该步骤则0触发，到达有效步骤至多1；生产运动仅许可本功能Start与两次固定XY，不能产生Z/翻面/旋转/分拣/回零/卸料；故障路径只允许已确认语义的受控停止，不追加工艺动作；质量未判定。架构测试同时确认Application不依赖产品配方匹配服务、API/Worker无设备/DB旁路，模拟与真实适配共享契约。

每项证据记录spec/plan版本、用例、配置/能力/预算/模拟版本、source/purpose、clockId/模式、Run/Operation/Write关联、输入、事件序列、实际动作计数、文件及提交事实、结果和限制。建议输出到未来artifacts/station01/{testRunId}；本次不生成虚构测试结果。

## 2. FR逐项映射

| 功能需求 | 设计落点 | REQ/原则追溯（沿用spec） | 软件验证 |
| --- | --- | --- | --- |
| FR-001 | 运行/请求身份；[设计](contracts/common.md) | TASK-002、TASK-003、CTL-004；USER；P05/P07 | SV-01、SV-08 |
| FR-002 | 启动幂等与禁止重入；[设计](contracts/api.md) | TASK-009、CTL-008；P06/P07 | SV-10、SV-19 |
| FR-003 | 独立公共配置；[设计](contracts/configuration-time.md) | RCP-001、RCP-002、POS-001；USER、CL-01/03；P03/P08/P11 | SV-07、SV-15、SV-23 |
| FR-004 | 校验后冻结及隔离；[设计](contracts/configuration-time.md) | RCP-005、DAT-001；USER、CL-03；P08/P11 | SV-07、SV-15、SV-21 |
| FR-005 | 模拟共用/真实拒绝；[设计](contracts/configuration-time.md) | POS-001、NFR-007；USER；P04/P09/P10 | SV-16、SV-21 |
| FR-006 | 启动检查及算法独立；[设计](plan.md) | CTL-003、SAF-011、NFR-007；USER、CL-03；P02/P04/P10 | SV-05、SV-07、SV-09、SV-17 |
| FR-007 | 实体启动/夹紧顺序；[设计](contracts/device.md) | SYS-004、CTL-004；P04/P07 | SV-01、SV-08 |
| FR-008 | 统一运动准入/资源；[设计](contracts/device.md) | CTL-005、SYS-006；P04/P05/P06 | SV-09、SV-10 |
| FR-009 | 合法固定XY；[设计](contracts/configuration-time.md) | ID-006、CTL-006、CTL-010、POS-001；P03/P04 | SV-01、SV-07 |
| FR-010 | 可靠到位及F单拍；[设计](contracts/acquisition-algorithm.md) | ACQ-002、ACQ-008、CTL-008、CTL-010；CL-04；P03/P07 | SV-01、SV-08、SV-09、SV-10、SV-25 |
| FR-011 | 整盘3D给出有无/姿态和F XY；[设计](contracts/acquisition-algorithm.md) | ID-005、POS-002；USER、CL-01；P03/P07 | SV-01、SV-06、SV-23 |
| FR-012 | 无效高度不补值；[设计](data-model.md) | ID-006、POS-002、POS-003；P04/P10 | SV-03、SV-06 |
| FR-013 | 原始候选/去重/解析；[设计](contracts/acquisition-algorithm.md) | ID-001、RCP-001；USER、CL-04/05；P02/P07 | SV-02、SV-04、SV-11、SV-25、SV-26 |
| FR-014 | 调用有限终态；[设计](contracts/configuration-time.md)；§6调用意图/原期限与恢复 | ALG-013、SAF-005、SAF-011；P04/P06 | SV-03、SV-04、SV-05、SV-06、SV-17、SV-18、SV-19、SV-24 |
| FR-015 | 允许算法异常继续；[设计](data-model.md) | CTL-010、ID-001、POS-003；USER、CL-04/05；P01/P04 | SV-03、SV-04、SV-05、SV-25、SV-26 |
| FR-016 | 高度关联不伪造工件；[设计](data-model.md)；§6调用意图/原期限与恢复 | TASK-003、ID-005、ACQ-005、ALG-008；USER、CL-01；P07 | SV-01、SV-02、SV-11、SV-17、SV-18、SV-19、SV-23、SV-24 |
| FR-017 | 六类状态分离；[设计](data-model.md)；§6终态条件事务 | TASK-007、TASK-008、ALG-008；USER；P07 | SV-01、SV-03、SV-04、SV-09、SV-12、SV-18、SV-19 |
| FR-018 | 重复/旧/迟到拒绝；[设计](contracts/common.md)；§6终态条件事务 | CTL-008、ACQ-005、ALG-014；P07/P08 | SV-10、SV-11、SV-12、SV-18、SV-19 |
| FR-019 | 动作超时/取消未知；[设计](contracts/device.md)；§6终态条件事务 | CTL-007、SAF-003、SAF-004、SAF-008、SAF-009；CL-02；P04/P07/P08 | SV-09、SV-12、SV-18、SV-19、SV-24 |
| FR-020 | 硬件/内容/算法分类；[设计](contracts/acquisition-algorithm.md) | ACQ-006、ACQ-010、SAF-011；P04/P09 | SV-03、SV-13 |
| FR-021 | 意图/事实保存门；[设计](contracts/persistence-handoff.md)；§6调用意图/原期限与恢复 | DAT-001、DAT-007、SAF-010；P07/P08 | SV-17、SV-18、SV-19、SV-24 |
| FR-022 | 实际媒体及原始响应；[设计](contracts/persistence-handoff.md)；§6调用意图/原期限与恢复 | DAT-001、DAT-002、DAT-007；P06/P08 | SV-01、SV-03、SV-13、SV-17、SV-18、SV-19、SV-24 |
| FR-023 | 必要保存及容量；[设计](contracts/persistence-handoff.md) | DAT-007、DAT-010；P06/P08 | SV-18、SV-20 |
| FR-024 | 完成门/持久移交；[设计](contracts/persistence-handoff.md)；§6终态条件事务 | TASK-007、TASK-008、DAT-007；USER；P02/P07/P08 | SV-01、SV-03、SV-04、SV-12、SV-18、SV-19 |
| FR-025 | 禁止配方/后续业务；[设计](contracts/api.md) | RCP-001、RCP-005的本次排除边界；USER；P01/P02 | SV-01至SV-35共同断言 |
| FR-026 | 状态/中文诊断；[设计](contracts/common.md) | HMI-004、HMI-007、DAT-008；P07/P09 | SV-03、SV-09、SV-18、SV-22 |
| FR-027 | 有界/停止/媒体；[设计](plan.md) | NFR-004、NFR-007、SAF-011；P06/P09 | SV-05、SV-12、SV-20 |
| FR-028 | 配置变化与策略边界；[设计](contracts/configuration-time.md) | RCP-003、POS-001、NFR-007；USER；P03/P11 | SV-14、SV-15、SV-21 |
| FR-029 | 注册兼容≠算法就绪；[设计](contracts/configuration-time.md) | RCP-002、ALG-013、NFR-007；USER；P04/P11 | SV-05、SV-14 |
| FR-030 | 恢复核对/步骤复用；[设计](contracts/persistence-handoff.md)；§6调用意图/原期限与恢复 | TASK-007、ACQ-010、SAF-008、SAF-009、SAF-010、DAT-007；CL-02；P07/P08/P09 | SV-17、SV-18、SV-19、SV-24 |
| FR-031 | 后端授权及本地运行；[设计](contracts/api.md) | SEC-001、SEC-003、SEC-006、SYS-003、SYS-004；P02/P05/P09 | SV-16、SV-22 |
| FR-032 | 单写/查询/维护边界；[设计](contracts/persistence-handoff.md) | DAT-007、NFR-007；ARCH §12兼容部分；P05/P08 | SV-17、SV-18、SV-19、SV-20 |
| FR-033 | 统一端口替换；[设计](plan.md) | NFR-007、ACQ-010；USER、CL-06；ARCH §13.1/13.2兼容部分；P05/P09 | SV-16、SV-28、SV-34、SV-35 |
| FR-034 | 七环节独立延迟；[设计](contracts/configuration-time.md) | CTL-007、CTL-008、ACQ-002；USER、CL-06；P04/P07/P09 | SV-27、SV-28 |
| FR-035 | 耗时/预算独立与故障；[设计](contracts/configuration-time.md) | CTL-007、ALG-013；USER、CL-06；P04/P09/P10 | SV-28、SV-29、SV-30、SV-31、SV-32、SV-35 |
| FR-036 | 实际期限机制超时；[设计](contracts/configuration-time.md) | CTL-007、ALG-013、SAF-011；USER、CL-06；P04/P06/P09 | SV-05、SV-29、SV-30、SV-32、SV-34 |
| FR-037 | 等待时响应及取消；[设计](contracts/device.md) | NFR-004、SAF-003、SAF-004、SAF-011；USER、CL-06；P04/P06/P07/P09 | SV-12、SV-20、SV-31、SV-33 |
| FR-038 | 双时钟同语义；[设计](contracts/configuration-time.md) | DAT-008、NFR-004；USER、CL-06；ARCH §13.2、§14.3兼容部分；P07/P09/P10 | SV-27、SV-33、SV-34 |
| FR-039 | 版本/时间证据保存；[设计](contracts/persistence-handoff.md) | RCP-005、DAT-001、DAT-008；USER、CL-06；P07/P08/P09/P10 | SV-15、SV-34、SV-35 |
| FR-040 | 超时分类与晚到隔离；[设计](data-model.md) | CTL-007、ACQ-005、ACQ-010、ALG-014、SAF-011；USER、CL-06；P04/P07/P08/P09 | SV-29、SV-30、SV-31、SV-32 |

## 3. SV逐项方法与证据

| 场景 | 分类 | 输入/故障 | 必须观察的结果和证据 | 成功条件 |
| --- | --- | --- | --- | --- |
| SV-01 | I/P | normal +显式安全/按钮 | 顺序、动作事实、两类媒体和Handoff保存；实际公共3D配置动作及本次定位F一拍 | SC-001、SC-007 |
| SV-02 | R/C/I | 唯一原文、parser capability=null | 原文保留、NotDefined字段与限制；不推测产品 | SC-008 |
| SV-03 | R/C/I/P | 高度Error/NoResult/超期及安全不满足对照 | 实际期限终态、高度空、保存；独立安全满足才F | SC-002 |
| SV-04 | R/C/I/P | F错误/超期/空集/冲突/格式错误 | 分别保存技术/识别/解析状态；异常移交且不重拍 | SC-002、SC-008 |
| SV-05 | R/C/I | 算法NotReady/NotIntegrated/null预算/持续失败 | 允许启动；调用有限终态；不等Worker恢复 | SC-002 |
| SV-06 | R/C | 缺单位/基准、NaN入站、不适用高度 | 原始证据及InvalidResult；无猜值或Z动作 | SC-002 |
| SV-07 | R/C/I | 3D或F点缺失/越界/F参数缺失；配置受限运行取消后新请求 | 启动前动作=0；原上下文保留。T031/T047验拒绝与信号，T044/T048验最终取消提交后新建，不能先借continue替换 | SC-003 |
| SV-08 | C/I | 不输入实体按钮/夹紧Hold；按钮等待期间取消 | 3D动作=0；T032只验状态/基础信号，T044/T048验完整取消/停止及持久结果 | SC-003 |
| SV-09 | C/I | 断联、安全失效、ACK无完成 | Unknown/Held；不采集、不自动恢复 | SC-003、SC-004 |
| SV-10 | R/C/I/P | 相同请求/异内容/不同请求并发、重复反馈 | 同Run/Command、冲突拒绝、动作计数不变 | SC-004 |
| SV-11 | R/C/I/P | 跨run/session/epoch、旧帧、超期响应 | LateEvidence原关联；终态/移交摘要不变 | SC-004 |
| SV-12 | R/C/I/P | 各阶段取消；移交排队/提交后无回执/CommitUnknown/取消后旧移交及重启 | 立即关准入；StopPending≠Stopped；实际SQLite条件事务唯一终态，Run/Handoff与API/内存/重启一致；未决applied=null | SC-004 |
| SV-13 | C/I/P | 相机断线/光源失败/采集未知、内容损坏对照 | 硬件受限与可靠结束内容异常分开；无假媒体 | SC-003、SC-005 |
| SV-14 | R/C | 未注册/不兼容能力、脚本、算法未就绪对照 | 拒绝未知执行；单纯算法问题不阻启动 | SC-006 |
| SV-15 | R/I/P | 修改原配置文件或生效版本 | 本Run使用冻结快照，后续Run才读取新值 | SC-006 |
| SV-16 | R/C/I | Test参数请求Real运动/混合绑定 | 真实设备发送计数0；测试用途/来源记录 | SC-006 |
| SV-17 | C/P | 快照/动作及AlgorithmIntent提交失败或未知 | 不投递依赖动作/Execute；Call输入/身份/版本/原期限与WriteId关联，意图非Accepted | SC-005 |
| SV-18 | C/P | 事实/媒体/AlgorithmIntent/结果/Handoff失败或未知；终态事务与回执交叉 | 原WriteId核对，未确认不Ready或Cancelled；意图保存中算法到期不再Execute；SQLite Run/Handoff原子一致 | SC-005 |
| SV-19 | R/C/P | 动作/文件间隙、Call意图三窗口和完成/取消五窗口中断 | 核对WriteId/Call/Attempt及实际提交；派发Unknown不重算/重拍，唯一持久终态重启一致，无盲重放 | SC-005 |
| SV-20 | C/I/P | 队列满/磁盘慢/配额耗尽/通知慢 | 有界准入和终态保留；停止/心跳仍可处理，未丢必要证据 | SC-005、SC-009 |
| SV-21 | R/C/I | 同能力两版点位/绑定/参数及测试注册新策略 | 无需改Coordinator；未知策略无旁路，流程顺序不变 | SC-006 |
| SV-22 | R/C/I/P | 四角色允许/拒绝、各种异常 | 后端鉴权及审计；中文原因、版本、保存/继续去向 | SC-008 |
| SV-23 | R/C/I | WholeTray、多项高度/自身标识、非法scope | 冻结范围与Run/Capture/Call关联；无Part/Face/槽拆分 | SC-001 |
| SV-24 | R/C/P | 核对不通过/快照缺失/未知动作或派发、未决取消、取消/完成、重复continue | 只复用完整保存步骤；原未终结Call存Interrupted和派发依据；终态/未决取消拒continue，不重置原预算 | SC-005 |
| SV-25 | R/C/I | F成功/异常/超时/未接入/重复帧 | 每次有效F步骤单触发单图；无自动第二次Call流程 | SC-008 |
| SV-26 | R/C | 空候选、同值重复、多个不同值、格式不符、无响应 | 原文Ordinal去重；冲突不选主码；null≠空集 | SC-008 |
| SV-27 | R/C/I | 七环节逐项可观察延迟，XY覆盖两次 | 待受理/执行中/完成分开；未完成无后继 | SC-009 |
| SV-28 | C/I/P | normal两种时间模式 | 期限内正常完成及保存、单拍和阶段边界 | SC-001、SC-009 |
| SV-29 | R/C/I/P | height延迟2000>1000及NoResponse；安全/保存故障对照 | 由期限产生TimedOut；只有独立条件满足才F | SC-002、SC-009 |
| SV-30 | R/C/I/P | F decode延迟2000>700及NoResponse | 保存超时、带异常移交、不重拍/伪造无码 | SC-002、SC-009 |
| SV-31 | R/C/I/P | duplicate-late、终态/取消/跨Run后的设备和算法事件 | 原记录留痕、无重复动作；晚到物理事实只供核对 | SC-004、SC-009 |
| SV-32 | R/C/I | PLC受理/夹紧/XY/3D/F采集逐项超期、不响应或Fail | 设备/采集完成未知阻断；不套用算法继续 | SC-003、SC-009 |
| SV-33 | R/C/I | 七环节长延迟/不响应，虚拟时间暂不推进 | 查询/心跳/停止/取消受理在原响应前发生；停止仍需反馈 | SC-009 |
| SV-34 | R/C/I | 同输入/预算/事件顺序，D-1、D、D+1及UTC校时 | 两模式同期限语义；等于D超时，校时不改预算；快钟不等长实等 | SC-010 |
| SV-35 | R/C/I/P | 独立改延迟/预算、在途改版本、非法值及真实入口 | delay>budget合法不自动扩预算；记录冻结版本与观察，禁止Test真实动作 | SC-006、SC-010 |

SV-27/32/33采用七环节参数化矩阵，分别测试期限前、期限边界、超期、无响应、失败和取消；不是只测最终状态。其余重复/错序/跨运行依SV-31及共同断言。规则测试用可控时钟，集成正常延迟用实际时间；绝不直接写TimedOut状态代替期限测试。

## 4. SC逐项映射

| 成功条件 | 设计证据 | 验证方法/用例 |
| --- | --- | --- |
| SC-001 | sequences正常、data-model完成门 | SV-01、SV-23、SV-28：完整轨迹与移交，3D整盘身份正确 |
| SC-002 | configuration-time实际期限、acquisition-algorithm终态 | SV-03至SV-06、SV-29、SV-30：有限结束、安全继续及异常移交 |
| SC-003 | device准入、全部配置启动前校验 | SV-07至SV-09、SV-32：拒绝调用计数及Unknown |
| SC-004 | common关联/仲裁、api幂等 | SV-10至SV-12、SV-31：终态不覆盖/动作不重复；§6真实SQLite条件提交、API及重启一致 |
| SC-005 | persistence-handoff间隙矩阵、恢复核对 | SV-17至SV-20、SV-24：Call意图三窗口、终态竞争五窗口、保存回执/文件及复用依据（§6） |
| SC-006 | schema、注册策略、不可变快照、真实用途门 | SV-14至SV-16、SV-21、SV-35：配置替换与拒绝旁路 |
| SC-007 | API不提供范围外能力、Coordinator终点固定 | SV-01至SV-35共同断言；静态依赖及动态调用轨迹 |
| SC-008 | F原始/去重/解析模型、中文Error | SV-02、SV-04、SV-22、SV-25、SV-26：字段和动作次数 |
| SC-009 | plan有界通道、七环节耗时、独立控制路径 | SV-27至SV-33：中间态、期限及停止/查询响应证据 |
| SC-010 | 同一时间仲裁、版本化配置/计时记录 | SV-34、SV-35：两种时间模式对照及快照隔离 |

## 5. 必要补充验证与真实依赖

以下验证直接针对设计风险，不扩展业务：

- Host/API/Domain依赖与唯一状态写所有者；通知/查询不会持有业务锁。
- 所有已受理操作终态预留容量，普通队列满/Writer卡住时停止锁存及心跳路径仍可处理；不无限创建Task/线程。
- 存储超时后实际提交迟到，按WriteId核对而非重做动作；必要媒体租约未释放时不清理文件；高度隔离不占F执行槽。
- 开发库缺失/过新/过旧/准备失败/锁被持有，Host均拒绝业务DB访问且不创建/迁移；两个Host/准备器不能同时持锁。此项验证同一初始结构，不能扩展为完整升级/搬迁产品。
- FakeTimeProvider未推进时命令可受理；Advance逐时刻排空事件；边界结果与到期回调无论调度先后都遵守接收窗口；持久化真实I/O另用集成验证，不能靠虚拟时间证明磁盘耗时。
- 真实PLC编码、轮询/心跳、物理按钮/夹紧/停止、真实相机/光源结束依据、SDK取消/阻塞、生产点位/整盘覆盖、算法效果、Python依赖、OS/SQLite原生加载及断电保存能力属于H，按OPEN使用时机验证。当前全部未覆盖。
- 不做前端、独立虚拟下位机、合同验收、培训/签署；没有用第一工位模拟耗时推算≤10秒/面或7×24实机指标。


## 6. H01/H02/M01修正的必做验证（沿用现有SV编号）

以下是现有场景的具体输入/断言，不新增或删减SV。T025/T033/T035的合同测试负责提交门，T053/T054必须提供真实SQLite和文件证据；T022的测试屏障可控制写者提交前及提交后回执发送前，不能由测试直接把Run改成期望终态。

| 发现/既有场景与任务 | 输入及可控窗口 | 预期结果 | 必需证据 |
| --- | --- | --- | --- |
| H01：SV-17/18，T025/T033/T035/T053 | 两种算法各自的AlgorithmIntent入队后失败、事务未提交/结果未知、正常提交 | 未Committed时Execute=0；Call/Operation/Run/Capture/Attempt、媒体/版本/依据齐全且意图不表示Accepted；正常仅原期限内派发一次 | 实际SQLite批次/WriteId和输入媒体、端口派发/接受时间分别记录 |
| H01：SV-18及SV-34既有期限边界，T053 | 原Call登记后阻塞意图提交回执，在原D-1/D/D+1核对；另在D前提交但结果D才入站 | 保存耗时计入同一原预算；D及之后不派发或结果判迟到；晚提交不能重开窗口；保存未知仍阻后继 | StartTick/DueTick保持不变、期限事件、实际提交及回执时间、Execute计数；可控时钟只推进计时，真实I/O由屏障核对 |
| H01：SV-19/24，T052/T054 | 提交前、提交后未派发、已派发结果未保存分别中断；派发事实及原入站期限依据有/无证据对照 | 先核对原WriteId；无Dispatch记录不等于未发。原Call无终态有限Interrupted并保存派发Unknown；完整结果须身份及原入站/期限裁决可核实才补存原终态，仅有文件不推定按期成功；不能再次Execute/Capture或换Call逃避预算 | 原库/文件、旧进程退出及事务核对、Call/Attempt与期限依据、恢复Check及零重算/重拍计数 |
| H02：SV-12/18，T048/T053 | WriteH排队但未提交时cancel；另以合法取消先提交、旧WriteH后投递为对照（控制候选投递时机，不越过Writer顺序） | cancel立即关准入但applied=null；先真正条件提交者胜。完成胜则Run完成+唯一Handoff且取消NotApplied；取消胜则Cancelled且无Handoff，旧完成拒绝 | 两WriteId/revision/条件更新结果、实际SQLite一致快照、命令/API/内存投影和动作计数 |
| H02：SV-12/18，T048/T053 | 实际WriteH已提交，扣住回执再cancel；持续至保存期限为CommitUnknown | 不能先宣布Cancelled；按原WriteH核对后返回既有完成/NotApplied，不撤回停止，不再生成移交 | 提交与回执两个独立屏障时间，CommitUnknown→核对→唯一终态，API/Run/Handoff及稳定摘要 |
| H02：SV-12/18，T048/T053 | WriteH未提交仍在途而查询暂不存在；cancel到达；最终回滚或提交分别测试 | 暂查不到不能当失败，不提交竞争取消终态；排除旧写者并确认结果后才裁决。仅revision变化须复核证据，不改原WriteId载荷 | 在途写者、事务结局、WriteId查询、候选条件与裁决时间；停止/查询不等裁决 |
| H02：SV-12/18，T048/T053 | Cancelled已提交后旧WriteH、重复/旧回执到达；停止未知或必要保存缺失作为反例 | 旧完成条件失败且无Handoff；回执不覆盖终态；反例不可最终Cancelled，停止受理不代表Stopped | 真实约束/事务及条件拒绝，StopPending/保存限制、终态与版本稳定 |
| H02：SV-19/24，T054 | 上述每个窗口中断重启，另覆盖取消仅内存受理未保存/已保存未裁决 | 持久Run/Handoff/Write/命令一致；未知不双终态。已存取消仍关准入；未保存取消不伪造记忆，未终态RecoveryRequired并授权重提；无自动动作/终态复活 | 同一Test根重启前后查询对照，保留模拟物理状态、实际事务回滚/提交依据、零自动动作 |
| M01：SV-07/08/12/33，T031/T032与T044/T048 | 基础阶段直接投递T014控制信号；完整阶段经CancelRun API取消配置受限/按钮等待运行 | 前者只验查询与关准入，不要求取消终态；后者验物理及必要保存条件、最终取消、新请求建立并保留原Run | 各任务独立测试入口及依赖；M1不得引用M2尚未执行的完整取消/恢复结果 |

实际SQLite验证采用独立Test根、同一EF模型和初始迁移；提交屏障只用于测试注入，Writer真实执行事务，不用内存库或直接写预期Run状态替代。重启测试须确保旧Host/写者已退出、锁释放，才能确认回滚或Committed；迟到回执注入仍带原身份。证据须包含ConditionRejected与相同WriteId核对，不能只检查最终字符串。T053/T054即使假时钟未推进，也不能宣称真实I/O已结束。

## 7. 修订记录

- 2026-09-20，1.0.1：H01增加派发前保存/期限/中断证据；H02增加真实SQLite五窗口裁决与重启一致性；M01将完整取消分支落实到T044/T048。40 FR、35 SV、10 SC及CL-01至CL-06不变，全部软件验证NotRun。

## 2026-09-24 诊断增量实际核验（1.0.2；不改写历史结果）

FR-041/SV-36/SC-011的本轮Test证据：

- 受理后首次通信失效：`artifacts/station01-007/api-diagnostic-communicationafterreceipt-20260924-020126/` 的 `api-diagnostic-transcript.json`、`diagnostic-index.json`、`logs/host.out.log`、`station01.test.db`。正式API返回202，故障注入发生在回执之后；`requestId→commandId/runId→SQLite operationId`可追，Host首次原始异常为`IOException: HeartbeatStoppedChanging`。GET公开`Unconfirmed/PlcHeartbeatLost`、`WaitingClamp`、`UnknownHeldNoAutomaticRetry`、epoch/观察时间；设备动作未知且未见后继采集、算法和XY运动，SoftStop只代表请求，不宣称物理停止已确认。索引在进程退出后生成并保留原始日志哈希。该场景的PC_Start_Cmd已在故障前受理，不能伪写成“从未派发设备动作”。
- 同代次明确不安全：`artifacts/station01-007/api-diagnostic-unsafebeforerequest-20260924-020351/` 的同类四份证据。GET公开`ExplicitUnsafe/SafetyInterlockDenied`、epoch=1、可靠反馈`connected=true/safetyClear=false/alarmBits=2`；PLC审计仅心跳应答，无PC_Start_Cmd、XY运动、采集或算法。配置、协议、Host/PLC组件哈希见`process.json`；`Test/VirtualLoop`不作真实设备证明。
- 入口拒绝与正常回归：`artifacts/station01-007/diagnostics-20260924/tests/diagnostics-normal-start-query-final.trx`（15/15）覆盖原有正常启动/查询及无运行的结构化拒绝；`diagnostics-plc-final.trx`（5/5）覆盖断联与可靠不安全、心跳首次故障码保持及过期旧安全位拒绝。旧历史勾选不作为本次证据。

本轮只判日志诊断可定位：SV-36/SC-011在VirtualPlc软件联调范围内满足；真实PLC及原始人工启动故障根因/修复仍未确认。006实际页面/007 T027另判，不因本节通过而勾选。建运行前心跳异常另有`api-diagnostic-communicationafterreceipt-20260924-015903/`，该样本没有runId，不作为SV-36受理后证据。
