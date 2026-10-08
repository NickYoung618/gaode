# 020 阶段 A 实施验证

日期：2026-10-08。范围：闭环清零及同坐标兼容，全部验证仅 Test/loopback；未操作硬件、打包、部署、提交或推送。

## 实施前基线

- 分支：020-real-device-commissioning；HEAD：253324492a3ffe9ea5a62b605b5dd812d04b267c。
- feature.json 指向现有 specs/020-real-device-commissioning。
- T001 只读分析：0 项当前阶段冲突，28 项任务身份及依赖检查通过，82 个本地链接通过；后续真机安全、人工配方全链和混合组件 OPEN 保留。
- 既有修改：document-sync 所列 15 份 Markdown；既有未跟踪目录 backend/tools、frontend/src/assets、specs/020-real-device-commissioning、specs/deployment-real-plc，均保留。
- 保留标签：before-real-device-commissioning-2533244、camera-baseline-20261007-0f91f95、camera-review-baseline-67e4a57、rollback-before-member-gripper-20261007、rollback-before-same-position-20261007。
- 正式测试使用 ProtocolTcpFixture 的实际 VirtualPlc 引擎、127.0.0.1 随机端口及临时 SQLite；工具测试使用独立 loopback Simulator。未生成现场配置。
- requirements：31 项，28 项满足，3 项为后续局部条件。沿用户已确认的阶段 A 范围实施；implement 按技能要求只读此检查清单。

## 运行结果

阶段 A 软件实现与必要离线验证完成。020 整体、真机验收及部署交付尚未完成。

| 检查 | 实际结果 | 记录 |
| --- | --- | --- |
| 正式 HandshakeClosure / SamePosition / MemberGripper | 26 通过，0 失败，0 跳过；非零匹配 | [TRX](evidence/formal-stage-a/final/stage-a.trx)、[输出](evidence/formal-stage-a/final/test-run.txt) |
| 工具四文件定向回归 | 57 通过，0 失败，0 错误；四文件一次完整运行，包含六轴各连续两轮 | [最终四文件输出](evidence/tool-stage-a/final/test-run.txt) |
| 正式 Host 构建 | 成功，0 警告，0 错误 | [输出](evidence/formal-stage-a/host-build.txt) |
| 实际 SQLite / TCP 诊断 | 21 个有设备的正式夹具，110 行通信证据及阶段记录；另5项为纯采样规则/配方与成员验证 | [SQLite索引](evidence/formal-stage-a/final/sqlite-summary.json)；各夹具 sqlite-evidence.json / plc-audit.json / device-events.txt |

实际命令：正式和Host按quickstart的项目与过滤器执行，增加`--no-restore`和TRX/日志输出；工具在tests目录执行`python -m unittest test_commissioning test_same_position test_recipe test_member_gripper -v`。最终工具四文件一次完整运行包含六轴双轮、逐字段过期和通信读写原期限检查。仅运行现有本机依赖，未升级SDK、Spec Kit或插件。

## V01–V10逐项结果

以下 Passed 仅指本阶段适用的软件验证，不外推为真机或020整体通过。

| ID / 契约 | 状态 | 实际用例与证据 |
| --- | --- | --- |
| V01 / HC-01–03 | Passed | SixAxisMappingsClearIndependentlyAndRepeat、SamePosition三组连续定位、FlipHoldsParentUntilPutBackWithItsOwnWindow（同设备两轮）、SortingUsesActualCommitAndFinalSafetyBeforeParentClear（同设备两轮）；工具all_six_axes_two_completed_and_cleared_cycles和two_rounds_preserve_flip_parent_until_terminal_and_clear |
| V02 / HC-03/05 | Passed | DelayedClearHoldsOwnershipAndPreservesArrivalEvidence：请求已0、反馈仍1时不得完成/接后继；工具delay_clear_keeps_heartbeat_and_blocks_all_axis_entries：一次清写、deadline不重计、心跳继续 |
| V03 / HC-05/06 | Passed | NoClearTimeoutAndLateZeroNeverAuthorizeAnotherRequest；UnknownWriteOrDelayedTransportDoesNotReplay含启动应答丢失、读应答延迟、清写应答丢失；工具no_clear_and_late_zero、clear_write_unknown：物理完成事实保留，未知不重发，迟到0不解锁 |
| V04 / HC-03/06 | Passed | 正式ClearanceRejectsOldOrInvalidReads：清写前读、过期、异epoch；DisconnectWhileWaitingClearCannotStartSuccessor为实际TCP断线。工具pre_clear_read_and_old_generation、disconnect_during_clear、read_wait_cannot_extend_original_action_deadline：逐字段代次/水位、过期、重连；正式单Pump在同epoch完整读后发布，未引入缓存清零旁路 |
| V05 / HC-04 | Passed | 正式RepeatedTargetsReuseOnlySatisfiedAxes、StaticPositionWithoutCurrentClosureMustActuallyDispatch(initial/reset/drift)；工具全/混合复用、首次同坐标正常运动、重连/已观察复位、漂移、raw目标写失效及请求块过期；Y和检测Z沿用时无多余启动或Moving等待 |
| V06 / HC-01/07 | Passed | Flip持件不清父请求，PutBack按自己的原窗口清双反馈；Sort使用实际StageEventStore/SortingTargetAllocator，真实SQLite取料提交后才转运。缺动作意图时取料提交拒绝、无Place；最终安全轴证据早于Sort清写，抓手2保持。工具parent_clear_wait_and_original_terminal_deadline、sort_clear_follows_final_safe_axis_and_feedback_zero |
| V07 / TC-02 | Passed | 正式pending/auxiliary和未知保持挡后继；工具axis/raw目标/raw启动/clear/recipe入口守卫，raw无托管关联父动作保持未确认，人工撤请求不授完成/复用，capture_never_advances_actions_or_recipe和原串行RMW/心跳回归通过 |
| V08 / HC-07 / TC-04 | Passed | 实际运动返回清零前位置/到位身份，当前复用产生新Action/新观察并经OpenCaptureWindow→FinishCaptureWindow及SQLite保存；TransitionOwnsDeviceUntilRealSqliteSaveReceipt覆盖Flip/PutBack、实际已提交后等待及回执失败四种，保存返回前后无所有权空隙 |
| V09 / HC-01/04/08 / TC-03 | Passed | 正式及工具手工保留派发后Moving→Arrived；工具配方新鲜到位+位置路径通过，无Moving也如实标记。manual_coordinate_mismatch_clears_without_position_eligibility保留手工误差报告差异；VirtualPlc用内部实际完成事实接受反馈已0的合法Sort，未删除位置门 |
| V10 / FR-010/011/017 | Passed | [document-sync](document-sync.md)逐项关联15份当前文档、正式/工具源码和测试。历史记录、旧任务、原型/现场配置及安全局部OPEN不改；版本/配置/证据关联见下文 |

## 源码、配置与证据关联

- 当前源码为基线HEAD加未提交工作区，未发布。24个本次源码/测试文件及配置SHA-256见[source-snapshot.json](evidence/source-snapshot.json)，后续包必须关联最终提交和重新确认的配置，不能仅写“1.1.6”。工具版本常量保留历史1.1.6，本轮没有制作新版可执行包。
- 正式：Provider=Virtual、127.0.0.1随机端口、UnitId=1、IoTimeoutMs=1000、HeartbeatTimeoutMs=3000、位置容差0.01；实际VirtualPlc扫描5ms、心跳100ms、轴运动150ms、翻面/放回/取放80ms、Test放回安全Z=150。Sorting测试安全Z=9、旋转容差0.01均明确loopback-only。没有写入Real配置。
- 工具：既有default.json和protocol.json；临时配置host=127.0.0.1、随机端口、PDU1000/3000、FC03、EvenLow、REAL读/写Cdab。每个导出用例保留config.snapshot.json、protocol.snapshot.json及events.jsonl；测试覆盖的其他字序/地址基址是各自明确的离线配置，不推定现场采用。
- 正式实际TCP帧、写入和动作审计、SQLite记录分别保存。test.db按现仓库忽略规则仅保留本机；sqlite-evidence.json为可移交的实际数据库内容文本，包含诊断引用及真实请求/应答。文件哈希见[artifact-index.json](evidence/artifact-index.json)。
- final目录是最终通过结果；上层目录保留实施中间输出，不能取其旧数量/失败当最终结论，也不能用前一轮通过代替本轮。

## 实施中发现及修正

1. 旧SamePosition夹具直接设置位置/到位1，没有实际历史闭环；改为先真实TCP运动、双方清零及正式证据保存。旧测试即清写成功置cleared的断言改为等待独立PLC反馈归0。
2. Windows Python单调毫秒计时可能使紧邻读写水位相等；实际水位改用高精度单调perf_counter_ns，绝对预算仍沿原actionTimeout，不加清零延时。
3. 配方60ms离线动作可能不采到Moving；沿既有1.1.4契约保留新鲜到位+坐标，测试不再误要求该路径必有Moving。正式严格运动门没有放宽。
4. 分拣验证补齐真实Run、预留和动作意图后，使用正式SQLite阶段存储和提交服务，未以假提交回执替代核心保存。
5. 现事件列表有容量上限；双轮审计读取持久events.jsonl，避免把内存滚动窗口误当完整记录。
6. 工具通信读写受原动作绝对期限约束，通信配置更长不能延长闭环预算；读取超时停止推进并断开，保留物理完成和结果未知，重连/迟到0不解锁。Windows事件循环可能略提前触发期限取消，按实际预算取消记录明确超时，不因采样时钟细微差异改成可继续状态。
7. 同坐标复用逐字段检查请求、反馈、实测坐标、Ready/Auto的新鲜度与连接代次，不能由另一个新鲜块掩盖旧块。补验stale_request_block_cannot_authorize_position_reuse及read_wait_cannot_extend_original_action_deadline。

## 保护范围与剩余事项

- 分支、feature指针、五个回退标签保持基线；既有15份文档增量和四个未跟踪目录保留。未重置、清理、初始化或升级项目；未修改旧任务勾选。
- PLC.xls、PC.xls、DOCX SHA-256分别为8e846d0f71351f87b42db1bf40790f40766a392fd0a9e0968201f8c2056eb476、37095748a61574d9a1c9bf17ad0ac9118d554fa73ac8f5e10003718b6627511d、0f41552e3886897431c5866c6a93968168bc1f56d49051d3957583c1268f1fe8，与原基线一致。
- 正式frontend/desktop、工具web页面、原型ZIP/迁移原件未改；不宣称本轮重新验证了客户原型归档。无公共DTO、schema/迁移、PLC信号、Real默认配置或新框架变化。新增枚举仅为VirtualPlc的离线清写丢应答注入。
- `git -c core.whitespace=cr-at-eol diff --check`通过；源文件按原有逐行行尾保留，无整文件格式改写。Spec Kit before/after implement钩子为空。
- requirements清单31项/28满足/3未满足保持不变。implement技能明确要求清单只读，本轮仅核对；其中“未运行产品”的Notes属于先前specify/plan核查时点，当前实施状态以本报告为准，不能把真机/整体未满足项代勾为通过。T027按此边界对齐其他当前文档。
- OPEN-020-02/Q2及PLC-Q2–Q4的报警/光栅/首次恢复/软停解释、现场清零节拍适用性、用户具体安全虚拟值、公共Z/机械型号映射、人工配方全链、七真实相机混合运行、独立前端适用前置及019等历史未验证项继续保留。只限制依赖它们的后续工作。
- 未执行硬件、真实运动、七相机现场链、部署包、安装、回退实操、提交或推送。本阶段产物是后续版本/部署输入，不能当作部署包验收。
