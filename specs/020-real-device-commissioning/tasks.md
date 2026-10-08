> 当前阶段：阶段A/B确定软件已完成原范围验证；Phase 8 T061–T065按本轮授权完成，T061仅独立前端规格缺口交接，T062–T064增量最小回归12/12，T065证据同步完成，见validation-phase8.md。T055/T056现场Blocked，独立前端实施及整体部署交付未完成。下一步再次speckit-converge；历史说明按原时点保留。

# 功能任务清单：020阶段A——闭环结束后的双方清零确认

**输入**：[spec.md](spec.md)、[plan.md](plan.md)、[research.md](research.md)、[data-model.md](data-model.md)、[HC契约](contracts/closed-loop-handshake.md)、[TC契约](contracts/commissioning-tool.md)、[quickstart.md](quickstart.md)、[文档同步记录](document-sync.md)  
**宪章版本**：9.0.0  
**分支 / 功能目录**：`020-real-device-commissioning` / `specs/020-real-device-commissioning`  
**日期**：2026-10-08  
**状态**：28项阶段A任务已实施及验证收口；正式26项、工具57项、Host构建通过，见[validation](validation.md)。020整体及部署交付未完成。

## 拆解规则

本清单只承接plan的阶段A：正式适配器、联调工具及VirtualPlc完整闭环清零、同坐标兼容、必要证据消费者和最小离线验证。当前任务以US2（P1）为主，US4（P2）仅包含U4-1同步及版本/配置/验证记录，不包含制作部署包、部署、提交或推送。

US1人工配方全链、US3真实七相机/虚拟算法与灯的正式混合链、US4发布回退仍为020整体义务；本阶段未设计部分不生成实施占位任务、不标完成。不得新建重复020、初始化/升级Spec Kit、改客户原型或来源XLS/DOCX、修改旧任务勾选。测试数据仅用于明确Test/loopback环境。

每项任务的需求/宪章/依赖/完成证据在下方追溯表中定义。`[P]`仅表示前置完成后可与无共享文件的同波任务并行，不指派人员、不授权自动多代理。未实际完成不勾选；设计文档已完成不代表代码任务已完成。

## 必要准备（Phase 1）

- [X] T001 对当前阶段执行speckit-analyze，只读核对 `specs/020-real-device-commissioning/spec.md`、`plan.md`、`contracts/`、`tasks.md` 的范围/覆盖/依赖，输出结构化报告及仅限制后续阶段的OPEN；分析期间不修改文件，当前阶段关键冲突未解决不得改代码，报告结论在后续T002记录。
- [X] T002 核对 `D:\gaode` 当前分支、`.specify/feature.json`、已有修改和五个回退标签，记录源码HEAD、工作区差异、Test配置来源及实施前状态到 `specs/020-real-device-commissioning/validation.md`；尚未执行的场景写NotRun，不清理已有文件、不新造现场配置。

## 共用前置（Phase 2）

先补独立设备反馈及现有采样/周期记录，后续用户场景只复用这些能力，不建第二执行器或采样器。

- [X] T003 [P] 修改 `VirtualPlc/VirtualPlcEngine.Axes.cs`、`VirtualPlc/VirtualPlcEngine.cs`：实际完成且PC撤请求后清轴/R及Sort反馈，保持放回末端同时清两个翻面反馈；分拣以设备内部实际完成停稳事实、请求0和有效实测位置准入；新运动/复位使相关事实失效，补ResetFromPc必要一致性，支持最小离线延迟/保持反馈注入，不让Host写PLC反馈或共享其记录。
- [X] T004 [P] 修改 `tools/plc-commissioning/tests/simulator.py` 及 `tools/plc-commissioning/tests/test_recipe.py` 中RecipeSimulator：独立实现完成后撤请求清反馈、翻面/放回双反馈、Sort末端归零及有限延迟/保持/断线注入；初始坐标/反馈不构造历史完成，断言预期来自HC而非App判定。
- [X] T005 [P] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/PreparedPlcReadPlans.cs`、`LatestProtocolPlcDevice.Polling.cs`：在现有单Pump中提供有限清零读用途，B完整读Starts+Axes、R/T含请求、U含父请求和两个反馈；沿 `PlcSignalAccessor.cs` 逐字段真实读取水位排除缓存拼接，不另开线程/连接/全量轮询。
- [X] T006 新增 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Handshakes.cs` 内部partial并在 `LatestProtocolPlcDevice.cs` 生命周期接入：固定资源周期、清写收据、ClearanceProof及逐轴复用记录；用现PlcExchangeClock/SignalValues.Stamps核同epoch、清写后实际新读、全0及新鲜度，沿当前阶段原窗口/取消链，失效/迟到不自动解锁，不新增公共DTO、PLC点或数据库表。
- [X] T007 [P] 修改 `tools/plc-commissioning/src/app.py`：提取纯采集与单轮推进，加入连接generation和逐块读/写单调水位、有限资源周期/复用记录；沿原asyncio锁保证完整RMW，等待清零跨worker轮次不持锁；原poll嵌套调用不得再次推进动作/recipe，断线/重连与已观察复位使旧记录失效。

## 当前阶段范围与完成证据（P13）

| 项目 | 对应规格位置 | 任务或证据 |
| --- | --- | --- |
| 起点与终点 | US2、FR-005–008；plan阶段边界 | T012–T023：通过原准入的动作→物理完成→PC清请求→PLC新鲜全0→必要保存→允许后继；失败有界阻断 |
| 必须参与组件 | FR-010/011/015、HC-07/08 | T003–T007、T012–T020：正式语义端口/单采样链、工具各入口、独立VirtualPlc、真实TCP及SQLite证据 |
| 必要验证 | U2-1–7、quickstart V01–V10 | T008–T011、T021–T023；正式与工具分别留证 |
| 完成证据 | SC-002–004/007阶段A部分 | `validation.md`、`evidence/formal-stage-a/`、`evidence/tool-stage-a/`，均为离线来源；T024–T028收口 |
| 延期项 | US1、US3、U4-2/3及spec局部OPEN | 保留在spec/plan，不作本阶段全局前置、不误记整体完成 |

## 用户场景 US2：双方清零后连续执行（Phase 3，P1）

**目标**：清零未确认前绝不发下一相关请求；父闭环中间不清，清0后的同坐标正确沿用。  
**独立完成条件**：正式TCP+实际VirtualPlc引擎+SQLite证据与工具loopback分别覆盖V01–V09；两轮顺序、失败零后继写入、当前请求身份、保存门和明确日志可复核，均无未知动作自动重发。离线结果不代表真机验收。  
**相关需求/原则**：FR-005–008、009已确认部分、010–011、015–017；SC-002–004/007阶段A部分；P03–P09/P13。

### 适用的软件验证

用户规格明确要求最小回归，以下测试先依据契约补齐，全部通过要求由T022/T023统一执行确认；不要求无关历史测试或完整异常矩阵。

- [X] T008 [P] [US2] 新增 `backend/tests/Gaode.Communication.Tests/HandshakeClosureTests.cs`，复用 `ProtocolTcpFixture.cs` 的真实TCP/实际引擎/SQLite诊断，覆盖六轴映射、两轮动作、清写成功但PLC延迟/不清、写结果未知、清写前已开始的读/过期/异代/断线及迟到全0；审计下一请求时序、零重发和具体错误，不以被测ClearanceProof作为唯一断言。
- [X] T009 [P] [US2] 重写 `backend/tests/Gaode.Communication.Tests/SamePositionTests.cs`：先执行实际模拟动作及双方清零再测全同坐标/混合X移动而Y及检测Z沿用；验证首次/重连/已观察复位/漂移/其他相关运动不能借旧资格，且首次合法正常移动可执行；当前Action定位证据能通过后续位置准入，无多余启动或Moving等待。
- [X] T010 [P] [US2] 在 `tools/plc-commissioning/tests/test_commissioning.py` 增加轴手工/通用write/clear入口的延迟/不清零、过期/断线、未知和两轮回归；确认observing期间plcCompleted可真而cleared仍假、deadline不重计、心跳继续、无poll递归/锁死/重复清写；人工撤请求不制造成功/复用，raw不能绕过资源守卫。
- [X] T011 [P] [US2] 修改 `tools/plc-commissioning/tests/test_same_position.py`、`tools/plc-commissioning/tests/test_recipe.py`：先建立真实loopback闭环资格再验证复用和recipe最终复核；覆盖翻面持件不清父命令、放回后两反馈清0、Place→最终安全轴闭环→父清零、两轮流程；保留严格手工与1.1.4配方新鲜到位+坐标的差异，并验证手工未获坐标验收不直接授复用。

### 当前范围实现

- [X] T012 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Axes.cs`：实际到位/位置成立后保存完成身份、清本批启动并等全0再返回；同坐标用本连接历史闭环加本次新鲜复核逐轴决定，移除持续Arrived1复用条件，保留实际派发后Moving/Arrived和最终全组位置复核，不强求派发前反馈1、不固定跳过Y/Z。
- [X] T013 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Stages.cs` 的R路径：保留原绝对角/容差及完成采样，清RotateStart后新鲜确认RotateStart/RConfirmed全0再完成，清零计入原阶段窗口，不以面号猜角度或额外发R动作。
- [X] T014 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs` 的翻面/放回路径：父关联保持至放回完整结束，翻面完成不清2014；放回本次完成后清2014并等父请求及6050/6052全0。Flip/PutBack各沿原阶段窗口，失败不重发、不用已结束Flip截止封死合法PutBack。
- [X] T015 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Acquisition.cs` 与 `LatestProtocolPlcDevice.cs` 的阶段所有权出口：直到翻面/放回必要证据保存成功才释放当前阶段所有权，避免底层finally提前释放auxiliary；保存失败进入现未知保持，中间成功仅开放已关联父周期的后续步骤。
- [X] T016 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.Transfer.cs`、`LatestProtocolStageActionAdapter.cs`：新取放周期不接受旧Placed替代Idle；保持Pick→可靠保存/原IPickCommit→转运→Place→最终安全位子轴闭环→清SortingCmd→新读请求/反馈0。保留同周期Picked→Place、抓手选择及失败未知保持，不在中途清父命令。
- [X] T017 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Semantics.cs`、`LatestProtocolPlcDevice.Stages.cs`、`LatestProtocolPlcDevice.Axes.cs` 的完成消费者：实际运动返回清零前完成样本；复用按本次复核生成当前Action关联定位证据并更新原reachedPositions，供IsCorrelated/RequireKnownPosition及采集继续使用；清零诊断独立，不能把最新0或旧Action证据当本次到位。
- [X] T018 [US2] 修改 `tools/plc-commissioning/src/app.py` 的begin_axis/observe_actions及write/clear准入：实际完成后一次清写，跨轮次确认新鲜请求/反馈全0再completed；统一同坐标资格和失效，raw/人工入口受相同资源门约束；按TC保持state枚举/HTTP路径，用reason/clearError显示等待和错误，保留手工坐标报告差异，不改页面。
- [X] T019 [US2] 修改 `tools/plc-commissioning/src/recipe.py`：纯采集替代嵌套poll推进；XY/单轴/reuse及最终复核共用App证据；放回后清2014并等两反馈，放料末段先完成安全位轴闭环再清2016/等0，清零计入原actionTimeout末段截止且不另计；last_targets不能授复用，成功单品AB→CD路线不增R/E/NG动作。
- [X] T020 [US2] 在 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Handshakes.cs`、`LatestProtocolPlcDevice.FailureEvidence.cs` 及 `LatestProtocolStageActionAdapter.Transfer.cs` 沿现诊断设施补清写应答/等待/确认/复用/失败日志，关联当前动作/运行/epoch/周期、逐字段读取水位/原值/期限；具体区分PC清写未知、PLC未清、过期/断线，不高频重复写相同等待日志、不建新日志平台。
- [X] T021 [US2] 在 `backend/tests/Gaode.Communication.Tests/HandshakeClosureTests.cs` 补正式父周期及证据回归：翻面持件/放回、取料提交失败不搬运、最终安全位后才清Sort、反馈已0后合法分拣、抓手保持，以及Acquisition保存失败前后所有权、当前复用证据和真实完成身份；必要夹具扩展仅落 `ProtocolTcpFixture.cs`，无业务成功旁路。
- [X] T022 [P] [US2] 按 `specs/020-real-device-commissioning/quickstart.md` 运行HandshakeClosure/SamePosition/MemberGripper定向.NET测试，并构建 `backend/src/Gaode.Host/Gaode.Host.csproj`；将非零匹配数量、实际结果/配置/审计/SQLite诊断引用保存至 `specs/020-real-device-commissioning/evidence/formal-stage-a/`，解决本改动导致的失败后才记通过，不连接硬件。
- [X] T023 [P] [US2] 按 `specs/020-real-device-commissioning/quickstart.md` 运行工具test_commissioning/test_same_position/test_recipe/test_member_gripper四个文件，将数量、结果/配置/事件时序保存至 `specs/020-real-device-commissioning/evidence/tool-stage-a/`；确认现UI可从API获得正确observing/completed及reason且页面文件无变化，不运行真实设备或打新包。

## 用户场景 US4：同步与可回退交付——当前仅同步记录（Phase 4，P2）

**目标**：U4-1的正式/工具改动、差异与新验证可逐项对账，后续部署可查到真实源码/配置基线。  
**独立完成条件**：SC-007阶段A范围逐项有实现位置和双方证据/明确限制，版本及配置关联可复核；不将历史成功拼成新机制通过。U4-2/3发布、部署、远端提交和回退实操不属于本阶段。  
**相关需求/原则**：FR-010/011/016/017、FR-018仅保留后续关联输入；P01/P08/P09/P13。

- [X] T024 [US4] 更新 `specs/020-real-device-commissioning/document-sync.md`：逐项关联15份已同步当前文档到最终代码/测试及HC/TC条款，记录实际完成判据、手工坐标报告、REAL配置形状、模拟范围和正式持久化的必要差异；当前文档只有本次实现发现偏差时最小再修，旧版本validation/delivery/tasks不重写。
- [X] T025 [US4] 在 `specs/020-real-device-commissioning/validation.md` 汇总正式/工具/VirtualPlc实际源码HEAD及工作区差异、Test生效配置/协议摘要与双方证据路径，保留五个回退标签及历史未验证说明，作为后续包/变更说明的输入；不得把未提交工作区宣称为已发布版本，不创建包、不提交或推送。

## 当前规格的关键规则覆盖

| 当前适用规则及来源 | 对应任务 | 必要验证/证据 |
| --- | --- | --- |
| 完整闭环才清父请求，PC/PLC各自所有权，HC-01/02/08 | T003/004/012–T016/019/021 | V01/06/09：双轮请求审计、独立反馈，不清抓手/Ready/心跳 |
| 清写后实际新读、同连接、全0、新鲜度，HC-03 | T005–T008/010/012–T014/018 | V02–V04，禁止旧缓存/写成功替代证明 |
| 原预算、有限失败、无重发，HC-05/06 | T006–T008/010/014/016/018/019 | 原窗口/末段截止、延迟期间零后继、未知/迟到不解锁 |
| 同坐标不启动、当前身份及全组复核，HC-04/07 | T009/011/012/017–T019 | V05/08，Y/检测Z无伪超时，复用证据支持采集 |
| 取料提交、安全位、必要保存，HC-07 | T015/016/017/021 | V06/08实际SQLite及失败所有权证据；不新增schema |
| 单采样/串行RMW、心跳独立、全部入口，TC-02 | T005/007/010/018/019 | V07，无死锁/递归/旁路，前端无PLC直控 |
| 诊断与来源差异，TC-01/03/04 | T010/011/020/022–T025 | V08–V10，关联时序日志及双方结果 |
| 当前不涉及的算法等待、配方编辑/冻结、媒体新增、页面/宿主 | 本阶段不适用 | 保留原门禁/历史事实，后续US1/US3及独立前端规格承接，不新增接口或模拟成功 |

## 任务追溯与依赖

所有路径相对`D:\gaode`；每项具体文件见任务正文。表中“测试证据”在T022/T023实际执行后才成立。

| 任务ID | spec需求/成功条件及来源 | 宪章 | 真实前置 | 完成条件与证据 |
| --- | --- | --- | --- | --- |
| T001 | FR-010、plan一致性门 | P01/P05/P10 | 当前设计/本清单 | 只读分析报告有当前阶段结论/待处理关键问题，后续OPEN局部列明；T002引用 |
| T002 | FR-018保护、SC-007来源 | P01/P13 | T001 | validation.md基线准确，既有修改/标签保留 |
| T003 | FR-005/006/008/011；HC-08 | P04/P07 | T002 | 独立模拟状态机清零、内部停稳/复位失效；T022验证 |
| T004 | FR-005/006/011/017；HC-08 | P07/P13 | T002 | 工具替身可复核清零/故障时序；T023验证 |
| T005 | FR-005/010；HC-03 | P05/P06 | T002 | 有限完整字段读计划及真实水位，无第二采样器；T022验证 |
| T006 | FR-005/007/008；HC-03–06 | P04/P07 | T005 | 内部周期/证明/失效按原窗口，API/schema不扩大 |
| T007 | FR-005/007/011；TC-02 | P05/P06 | T002 | 采集与推进分离、连接身份/水位/锁正确；T023验证 |
| T008 | U2-1–4/7、SC-002/003 | P04/P07/P13 | T003/T005/T006 | 正式最小故障断言独立、测试可发现旧清写即成功行为 |
| T009 | U2-6/7、SC-004 | P07/P13 | T003/T006 | 先实际完成再复用，覆盖当前身份及失效，无静态造证据 |
| T010 | U2-1–4、TC-01/02 | P04/P06/P09 | T004/T007 | 工具全部入口/锁/期限与未知断言完整 |
| T011 | U2-5/6、SC-004/007 | P03/P07/P13 | T004/T007 | recipe及同坐标测试前置真实、父边界与差异有断言 |
| T012 | FR-005/008、SC-002/004 | P04/P07 | T008/T009 | 轴双清零、原运动判据、混合/全复用及最终复核 |
| T013 | FR-005/006、SC-002 | P03/P04 | T012 | R完成身份和新鲜全0，无角度规则扩大 |
| T014 | FR-005/006/007、U2-5 | P03/P04 | T013 | 放回末段清双反馈；跨阶段关联/各自窗口正确 |
| T015 | FR-015、HC-07、V08 | P06/P08 | T014 | 必要保存前不释放，保存失败未知保持 |
| T016 | FR-005/006/015、U2-5 | P04/P08 | T015 | 提交/放料/安全位/父清零顺序，旧Placed不新开周期 |
| T017 | FR-008/015、SC-004、V08 | P07/P08 | T012–T016 | 实际完成与当前复核身份分别保存，现消费者可继续 |
| T018 | FR-005–008/011、TC-01/02 | P04/P06 | T010/T011 | 工具轴/人工/raw无清零旁路，状态兼容且未知不解锁 |
| T019 | FR-006/008/011、TC-03 | P03/P04 | T018 | recipe复核/父清零/末段期限正确，不增主链动作 |
| T020 | FR-016、TC-04 | P09 | T017 | 正式清零/复用/失败日志关联完整且不刷屏 |
| T021 | FR-006/015/017、SC-002/007 | P07/P08/P13 | T008/T017/T020 | 正式父周期、保存失败、完成/复用消费及抓手回归有据 |
| T022 | FR-017、SC-002–004阶段A | P13 | T021 | 定向.NET测试与Host构建通过，非零数量与证据保存 |
| T023 | FR-011/017、SC-002–004阶段A | P13 | T019 | 四个工具测试文件通过，正确时序与来源证据保存 |
| T024 | U4-1、SC-007 | P01/P09 | T022/T023 | document-sync逐项有最终实现与双方验证/限制 |
| T025 | U4-1、FR-018后续输入 | P08/P13 | T024 | validation版本/配置/证据关联真实，无发布宣称 |
| T026 | FR-017、P13阶段完成 | P09/P13 | T025 | V01–V10实际状态和失败修复有据，未执行保留 |
| T027 | FR-010/017、SC-007 | P01/P10 | T026 | 当前文档状态一致，不把阶段完成改成020整体完成 |
| T028 | FR-018保护、阶段范围 | P01/P12/P13 | T027 | 最终diff/指针/标签/测试/原件及页面范围核验有记录 |

### 执行顺序与并行机会

```text
T001 → T002
           ├→ T003（VirtualPlc） ─────┐
           ├→ T004（工具替身） ───┐ │
           ├→ T005 → T006 ──────┼─┼→ US2正式测试/实现 → T022
           └→ T007（工具采集） ─┴─┴→ US2工具测试/实现 → T023
T022 + T023 → US4同步T024 → T025 → T026 → T027 → T028
```

US2是阶段A MVP，必须同时具有正式及工具清零、同坐标、必要失败和保存/身份证据，不能仅做正常轴清零就宣称完成。US4的同步收口依赖US2两条验证均完成；可独立核对其产物，但不在代码尚未验证时写通过。US1/US3和U4-2/3不进入该依赖图。

- 共用前置：T002完成后，T003、T004、T005、T007可同时进行，文件无交叉；T006等待T005。
- US2测试：各自前置完成后，T008、T009、T010、T011可并行补断言；T004必须先结束，避免共同修改test_recipe.py；任何需要扩展共享ProtocolTcpFixture.cs的工作在T021统一串行落地。
- US2实现：T012–T017共用设备状态/partial文件，按表串行；T018→T019可与正式实现并行，但工具app.py的T007必须先结束。
- US2执行：T022/T023可并行，均仅loopback且各写独立证据目录；Host构建只在T022执行，不与同目录构建任务争用输出。
- US4：T024→T025顺序执行，二者引用同一验证汇总，不标并行；不启动部署/推送任务。

### OPEN与外部依赖

| OPEN/依赖 | 来源 | 只限制的任务或联调 | 补充时机 | 可继续工作 |
| --- | --- | --- | --- | --- |
| OPEN-020-02/Q2报警/光栅/首次恢复/软停解释 | spec PLC-Q2–Q4 | 后续现场Real安全/恢复适配；本阶段保留现有门禁，不猜定 | 对应真机动作设计/准入前 | T001–T028离线阶段A全部可推进 |
| OPEN-020-04现场预算适用性 | plan R05及spec | 后续真机清零节拍验收，不能从离线值推断 | 现场时序验证前 | 原阶段窗口/actionTimeout实现和离线超时回归 |
| OPEN-020-05–08、用户具体安全虚拟值 | spec已确认C01/C07与局部依赖 | 后续配方/机械参数、公共Z/相机/前端/混合组件 | 对应功能阶段及真机执行前 | 当前握手与证据消费者，不依赖安全值来跑Test |
| OPEN-020-09历史未验证 | 019/成员抓手/REAL原记录 | 对应硬件/机械/独立编码验收声明 | 后续适用验证 | 当前定向回归不关闭这些历史限制 |
| 实现发现必须更改公共类型/页面 | AGENTS、P05/P12及plan | 仅新发现的接口/页面变更 | 改代码前补适用spec/contracts/plan/tasks；页面独立规格 | 未依赖该变更的本阶段工作可继续，不能绕过原安全门 |

## 收尾与证据（Phase 5）

- [X] T026 汇总 `specs/020-real-device-commissioning/validation.md` 中V01–V10逐项实际结果、必要失败日志和修复后回归、正式/工具差异及NotRun/Blocked/延期范围；仅在本阶段必要运行证据满足时写阶段A软件完成，不把模拟/局部通过写成真机、配方全链或020整体通过。
- [X] T027 对齐 `specs/020-real-device-commissioning/spec.md`、`plan.md`、`checklists/requirements.md`（按implement技能只读核对、保留标记；当前状态见validation）、`quickstart.md`、`document-sync.md` 及本 `tasks.md` 的实际状态，复核HC/TC和宪章；已完成设计不重复建任务，历史未决/未验证与后续目标保留，任务仅凭实际证据勾选。
- [X] T028 核验最终Git差异、`.specify/feature.json`、已有修改/回退标签、原XLS/DOCX与客户原型只读及页面未改，结果写入 `specs/020-real-device-commissioning/validation.md`；确认无Test坐标进入Real默认配置、无无关schema/框架、无硬件/包/推送执行，再报告阶段结果和下一阶段局部依赖。

## 实施策略

先完成T001分析门和T002基线，再补独立反馈/采样/周期前置；按US2的正常双轮主链与同坐标路径落实，随后用相同夹具验证延迟、不清零、过期/断线及中间阶段/保存门。正式与工具沿原边界分别实现，不拷贝整个业务引擎。必要构建和定向测试通过后收口US4记录及文档，不扩大回归到不受影响的全库或危险硬件故障注入。

## 客户确认原型检查（P12）

本阶段不修改正式前端、桌面宿主或工具页面；不生成页面任务。状态通过既有API及reason/clearError显示，前端不能直连PLC。若后续需要新增控件/字段/文案，按独立前端规格和已确认原型承接；原型ZIP及归档只读，不因任务拆解自动授权页面变化。


## 阶段B任务增量（2026-10-08，软件实施及验证已收口）

本节T029–T060为阶段B任务；完成勾选以validation-stage-b.md实际软件证据为依据，物理效果未验和现场阻断另列。上文T001–T028保持原编号、内容和勾选。当前现场解释以spec与SP-020为准，PLC-Q2已关闭。输入为[阶段B计划](stage-b-plan.md)、[RC](contracts/recipe-chain.md)、[MC](contracts/mixed-runtime.md)、[CP](contracts/camera-parameters.md)、[SP](contracts/site-plc-adaptation.md)和宪章9.0.0。

任务生成后已完成阶段B只读分析；C1/I1–I3复核见analysis-stage-b.md。当前软件实现/定向测试/构建已执行，硬件任务未执行。P01–P10指spec参数矩阵，表中宪章P编号另列。所有路径相对仓库根；每任务完成标准及依赖以追溯表为准，数值编号不替代依赖图。T035共同校验/SQLite原实现经API证实可复用；T038对账保留未执行分支，T049只完成SDK软件实现，T054正常链终点为WholeTray已落地并等待人工取料，不声称人工取走后的最终出料或新用途现场链通过。

| P13范围 | 任务/证据 |
| --- | --- |
| 起点：人工制作配方；终点：正式链执行及落地 | T033–T038、T054/T056，字段追溯和动作/媒体证据 |
| 必须参与：API/共同模型/SQLite/正式执行器/PLC/相机/算法/灯 | T032–T054；逐组件真实/虚拟来源 |
| 必要验证 | B-V01–08，T038/T043/T054/T055/T056/T058 |
| 延期 | 包/部署/推送、未涉及工艺、历史硬件未验；不得移除原整体目标 |

### 阶段B必要准备与共享前置（Phase B1–B2）

先分析、保护基线并同步共享接口四份前置，不重新初始化工程。

- [X] T029 执行阶段B只读一致性分析，核对 `specs/020-real-device-commissioning/spec.md`、`stage-b-plan.md`、四份B合同与本任务段，结果保存 `specs/020-real-device-commissioning/analysis-stage-b.md`；实施前解决关键冲突，不能复用A分析结论。
- [X] T030 核对工作区/五个回退标签/原件哈希，记录基线与隔离Test数据根至 `specs/020-real-device-commissioning/validation-stage-b.md`；新证据仅写 `evidence/stage-b/`，保留A验证、旧库和已有修改。
- [X] T031 在 `specs/020-real-device-commissioning/document-sync.md` 按RC/MC/CP/SP完成共享变更前置核对：逐项同步012配方API、019采集v2、001用途/预算、017/011现场合同及各自spec/plan/tasks的当前适用增量；不修改历史验收或勾选，真实页面缺口另立独立前端规格。
- [X] T032 在 `backend/src/Gaode.Application/Ports/CaptureAlgorithmMessages.cs` 和 `backend/src/Gaode.Infrastructure/Devices/Cameras/ICameraSdkGateway.cs` 定义CP-020的PublicSettings互斥载荷、逐组件应用事实及原子设置采集边界；同步显式实现/fixture声明和摘要消费者，旧历史缺字段为Unknown，不添加忽略设置的默认实现。

### US1 人工配方贯通（Phase B3，P1）

独立验收：正式API新增/校验/保存/重启重读/编辑后可选，P01–P10有冻结及实际消费证据；不要求人工批准。T038完整消费依赖US3，但T033–T037可先形成存储/准入增量。

- [X] T033 [P] [US1] 新增 `backend/tests/Gaode.Communication.Tests/RecipeCommissioningChainTests.cs`，经正式API与真实隔离SQLite覆盖来源ID/版本、同型号多配方、新建空Approval可选、编辑重新校验、无效字段拒绝及重启重读；测试执行环境为隔离Test/loopback；其中准入与Freeze用例显式传入RealDeviceCommissioning用途，覆盖空Approval新建/编辑后可选及错误条件；另断言旧Test/Production规则保持。测试数值/fixture不能成为现场配置依据，不连接现场库。
- [X] T034 [US1] 核对并修正 `backend/src/Gaode.Host/Api/RecipeEndpoints.Authoring.cs` 与 `backend/tools/Gaode.DeploymentPrep/Program.cs` 的来源准备/选择：相容键后显式SourceRecipeId优先、layout版本检查、避免重复seed；来源完整且空Approval，无审批或操作员导入入口。
- [X] T035 [US1] 在 `backend/src/Gaode.Application/Recipes/RecipeDefinitionValidator.cs`、`backend/src/Gaode.Infrastructure/Recipes/SqliteRecipeStore.cs` 落实P01–P10共同校验、保存/编辑/完整重读与字段级错误；保留ETag、事务、CommitUnknown及完整分支参数，不用当前OK路线删NG/Pending要求。
- [X] T036 [US1] 修改 `backend/src/Gaode.Application/Recipes/RecipeAdmission.cs`、`RecipeRunPlanner.cs` 及 `backend/src/Gaode.Host/Api/RecipeEndpoints.cs`：RealDeviceCommissioning用途的软件校验保存即具可选资格，启动从实际配置核用途/槽/能力，Freeze不依赖空Approval.Purpose；扫描全部调用者含独立绑定入口，保留其他用途及历史Approval，无伪批准。
- [X] T037 [US1] 修改 `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs` 及 `backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs` 的正式绑定/冻结消费者：公共配置启动冻结、F唯一匹配/实体槽核对后冻结配方；预期配方错配拒绝，编辑不改变在途快照，公共Z保留读存及原XY移动。同步PublicPreparationHandoffV2、CommittedRecipePlanReader及IndependentRecipeApplication的冻结用途与联调快照重读。
- [X] T038 [US1] 扩展 `backend/tests/Gaode.Communication.Tests/RecipeCommissioningChainTests.cs`，经新增/保存/重读的正文驱动正式规划、执行器和离线设备端口，生成 `specs/020-real-device-commissioning/evidence/stage-b/recipe-field-trace.json` 的P01–P10逐字段对账；未执行分支明确标记，不用JSON相等代替实际消费。

### US2 现场适配及A兼容（Phase B4，P1）

独立验收：现场布局loopback证明编码/轴集合/报警只读与安全未知阻断；受影响A双轮/同坐标/保存门通过。既有T001–T028不重做。

- [X] T039 [P] [US2] 新增 `backend/tests/Gaode.Communication.Tests/SiteProtocolAdaptationTests.cs`，同时新增 `backend/tests/Gaode.Communication.Tests/SiteProtocolTcpFixture.cs`：复用现Modbus客户端和MBAP帧约定，夹具独立实现最小FC03/06/16服务器，按XLS/已确认D01定义原始寄存器和设备侧已知反馈转换，不从被测适配器生成预期值；未明安全/恢复不模拟已确认。用该现场布局TCP loopback断言REAL型号/INT姿态、F仅XY、MB6056 Bit0/Bit2及MB6058=0；审计无报警寄存器写入，非零报警/独立故障不被等级0覆盖，旧Teach缺失不导致伪安全。
- [X] T040 [US2] 修改 `backend/src/Gaode.Plc.Protocol/ConfirmedMemoryLayout.cs`、`ProtocolDefinition.cs`、`Signals.cs`、`SignalCodes.cs`：按已知布局定义必需能力/现场报警映射，移除本路线旧Teach必需依赖；只读PLC报警，不新增屏蔽点；Test旧布局保持，缺安全返回Unconfirmed。
- [X] T041 [US2] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/PreparedPlcReadPlans.cs`、`LatestProtocolPlcDevice.Semantics.cs`、`LatestProtocolPlcDevice.Handshakes.cs` 及同目录Axes/Stages/主文件的现场消费者：共用新能力与可靠采样，通信层处理MB2048 REAL/MB2012 INT，F用XY、E保留扫码Z；未明首次/恢复路径仍拒绝，不猜PLC-Q3/Q4。
- [X] T042 [P] [US2] 核对 `tools/plc-commissioning/src/app.py`、`recipe.py` 及 `tools/plc-commissioning/tests/test_commissioning.py`，仅同步现场报警解释/编码/轴用途实际差异；增加光栅/门报警只读及等级0用例，保留手工坐标报告差异，不复制正式SQLite/配方引擎。
- [X] T043 [US2] 在 `backend/tests/Gaode.Communication.Tests/HandshakeClosureTests.cs`、`SamePositionTests.cs` 和 `SiteProtocolAdaptationTests.cs` 回归受现场适配影响的正常双轮、延迟/不清零、过期/断线、同坐标Y/检测Z及父动作/取料保存门；A旧Test布局正常动作继续用ProtocolTcpFixture；现场地址/报警/未明安全阻断用T039的SiteProtocolTcpFixture，明确标注每条用例布局，不能将旧Test通过当现场布局通过；协议零状态与启动运动中分开。

### US3 真实设备与显式虚拟组件（Phase B5，P1）

独立验收：软件装配/参数与帧/媒体算法身份成立；真实七相机和真机代表链另由T055/T056留证。离线fixture不是Real验收。

- [X] T044 [P] [US3] 新增 `backend/tests/Gaode.Communication.Tests/MixedRuntimeCommissioningTests.cs`，覆盖新用途装配矩阵、缺配置/用途错配拒绝、算法读取关联媒体及缺安全输入零后继运动；离线fixture身份不能伪装真实设备验收。
- [X] T045 [US3] 在 `backend/src/Gaode.Host/Composition/Station01RuntimeOptions.cs`、`backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs`、`backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs` 及 `specs/001-station01-public-preparation/contracts/public-config.schema.json`、`budget.schema.json` 贯通RealDeviceCommissioning用途和引用版本/摘要校验；新增有来源联调配置模型，缺现场值不填默认。同步PlcRuntimeOptions/PlcMechanicalConfiguration与Axes/Stages的用途消费者，Host显式下传用途，联调机械依据不混用Test/Production；显式PositionBasis声明实际读数坐标系/单位来源，缺依据拒绝联调装配，不制造安全或完成事实。
- [X] T046 [US3] 修改 `backend/src/Gaode.Infrastructure/Configuration/ApprovedExecutionCostProvider.cs`、`backend/tools/Gaode.DeploymentPrep/Program.cs` 及 `backend/src/Gaode.Infrastructure/Persistence/StoreCompatibilityProbe.cs` 的用途/预算/StoreManifest消费者：使用有来源预算和隔离数据根、现有Profile字段，保留维护互斥与旧模式；全仓核对Profile读写，不复制Test常量或直接迁移现场库。
- [X] T047 [P] [US3] 扩展 `backend/tests/Gaode.Communication.Tests/CameraProtocolTests.cs`、`CameraBusinessRegressionTests.cs` 与 `backend/tests/Gaode.CameraWorkerFixture/Program.cs`：wire v2、同相机两设置/两新帧、读回不符/不支持不触发、未知不重拍、一次等待及正常恢复；保留历史Unknown断言。
- [X] T048 [US3] 修改 `backend/src/Gaode.Infrastructure/Devices/Cameras/CameraWorkerProtocol.cs`、`PersistentCameraGateway.cs` 和 `backend/src/Gaode.CameraWorker/Program.cs`、`CameraDriver.cs`：wire v2一次设置采集与读回响应，版本不符拒绝；同相机原gate覆盖设置至新帧、沿原期限/容量，无降级假成功。
- [X] T049 [US3] 在 `backend/src/Gaode.CameraWorker/GalaxyDriver.cs`、`CameraProDriver.cs` 按SDK实际节点实现适用曝光/增益/全幅校验、读回、备份和正常关闭恢复；3D不套2D参数，不猜单位/步长/容差，不支持请求明确拒绝；物理SDK效果留现场验证。
- [X] T050 [US3] 修改 `backend/src/Gaode.Infrastructure/Devices/Cameras/CameraCaptureAdapter.cs`、`ILightGateway.cs` 及其显式虚拟实现，实际消费亮度/通道/开关，灯设置后只等待一次再触发；同帧记录Camera Real与Light Simulated，PhysicalLightApplied=false，不扩大等待期限。沿现RuntimeDiagnostics/持久日志记录设置受理、读回/触发、虚拟灯消费及失败，关联Run/Capture/Operation、绑定/会话/设置摘要、组件来源与实际原因；不记录每次轮询。
- [X] T051 [US3] 在 `backend/src/Gaode.Application/Station01/Steps/ThreeDStep.cs`、`FScanStep.cs`、`backend/src/Gaode.Application/Workflow/RecipeDetectionExecutor.cs` 及其Observation文件传冻结PublicSettings/DetectionSettings；移除重复或灯设置前等待，不造公共Gain/ROI/Settle值；同步 `CaptureEvidenceGate.cs`（Application/Acquisition）、媒体sidecar和 `backend/src/Gaode.Host/Api/RunMediaCatalog.cs` 的事实保存/查询。沿现日志记录参数证据拒绝、保存失败和依赖阻断，关联当前运行/配方版本/采集/意图，保留错误阶段与处置；不新增日志平台。
- [X] T052 [P] [US3] 保留旧 `backend/src/Gaode.Infrastructure/Simulation/SimulatedAlgorithm.cs` 的Test职责，在同目录新增 `CommissioningAlgorithm.cs` 和 `CommissioningAlgorithmInputs.cs`，沿同一IAlgorithmPort并实现MC-020的显式能力/运行输入接口：只用现AlgorithmRole.TrayPose/FDecode/Detection；SingleDetection与FaceFusion是AlgorithmPurpose能力，两者请求均用Detection，分别核1/2个输入及既有结果合同，不新增同名Role枚举，按版本/hash/作用范围消费用户受控输入；读取本次合法媒体及身份，启动冻结3D/F依据、配方绑定后核摘要；缺值拒绝，不默认OK/有料/坐标0。沿现持久诊断记录算法受理、输入校验/结果/失败/释放，关联Call/Run/Capture、能力/参数版本、媒体和受控输入摘要，分级分类且限制重复日志。
- [X] T053 [US3] 修改 `backend/src/Gaode.Host/Composition/Station01Registration.cs`、`RealCameraRegistration.cs`、`CapabilityRegistration.cs`、`backend/src/Gaode.Application/Capabilities/Station01Policies.cs` 及CapabilityRegistry消费者：新用途明确装配Real PLC/七相机与Simulated算法/灯；能力按合同注册，Parser规则须有来源，Production拒绝隐式模拟，同一正式协调器和唯一PLC写控制端。
- [X] T054 [US3] 新增 `backend/tests/Gaode.Communication.Tests/CommissioningWorkflowTests.cs`，分开记录正常业务链与现场协议阻断：正常已明确Test路线复用ProtocolTcpFixture及合法Test配方/原准入，验证公共准备/F绑定、翻面/放回、采集/算法、真实SQLite/媒体与终态；人工新建空Approval配方的新用途校验/冻结及实际消费者逐字段证据由T033/T038覆盖，不为跑Test正常链复制批准或放宽旧规则。现场布局经SiteProtocolTcpFixture验证已知编码/反馈与安全未明零派发，阻断用例正确报错可通过，不得写现场正常流程通过。新用途完整Host/现场代表链若仍受未决安全阻断，留待T056补齐，正常Test链不能替代它；未走E/R/NG分支不记已验。
- [ ] T055 [US3] 仅在现场前置和执行授权满足后，按 `specs/020-real-device-commissioning/quickstart.md` 在 `evidence/stage-b/site/` 留存七相机角色/参数读回/帧和媒体证据；代表路线未用角色独立采集，记录SDK实际支持/正常恢复，不为凑七台增加运动。
- [ ] T056 [US3] 仅在PLC-Q3/Q4适用答复补齐SP-020及任务、用户安全虚拟值/完整配方/机械映射/预算/码规则有依据并获现场执行授权后，经正式入口执行单品翻面连续两轮；记录 `evidence/stage-b/site/recipe-motion-trace.json`，核同坐标及清零时序，唯一PLC控制端，无自动重发。

### US4 同步与交付输入（Phase B6，P2）

独立验收：正式/工具差异、配置与验证来源可查。包、安装、回退实操、源码提交与GitHub推送继续留后续交付阶段。

- [X] T057 [US4] 核对 `specs/020-real-device-commissioning/document-sync.md` 的B消费者和正式/工具必要差异，记录实际源码/配置/协议版本、新增验证引用及未完成现场项；不覆盖019或A历史结果，不把原工具成功盘当本次正式通过。

### 阶段B收尾与证据（Phase B7）

必要软件验证可先完成，不等待现场资料；真实联调任务未完成不伪称B完整结束。

- [X] T058 按 `specs/020-real-device-commissioning/quickstart.md` 执行B新增配方/现场协议/混合装配/相机/正式链定向测试、受影响A回归及Host/CameraWorker构建；工具运行受影响用例；在既有相机/混合/正式链失败用例中断言设置读回失败、虚拟输入缺失和保存失败的持久日志能定位组件/阶段/关联身份及零后继动作，不另建大异常矩阵。结果存 `evidence/stage-b/software/`，非零匹配、失败修复后验证；不连接硬件。
- [X] T059 在 `specs/020-real-device-commissioning/validation-stage-b.md` 分别汇总软件、真实相机和真机闭环的Passed/NotRun/Blocked及具体原因，关联P01–P10、B-V01–08、源码差异/配置；T055/T056未执行不得宣布B整阶段完成。
- [X] T060 对齐 `specs/020-real-device-commissioning/spec.md`、`stage-b-plan.md`、`quickstart.md` 与 `tasks.md` 当前完成状态；核分支/feature.json/原件/回退标签和原型未改，列下一交付阶段源码提交、包、配置、验收、数据兼容回退及GitHub变更说明所需输入，不执行打包/部署/推送。

### 阶段B任务追溯与依赖

产物路径已逐项写在任务正文；新增测试在对应既有测试工程内，不另建测试平台。

| 任务 | 需求/合同 | 宪章 | 前置依赖 | 完成条件/证据 |
| --- | --- | --- | --- | --- |
| T029 | FR-010/017 | P01/P05/P10 | 本次tasks完成 | B范围分析结论、局部OPEN和共享消费者完整 |
| T030 | FR-017/018 | P01/P08/P13 | T029 | 源码差异、配置身份、只读原件及NotRun清单 |
| T031 | FR-010 | P01/P05/P12 | T030 | 接口变更逐项具备spec/contracts/plan/tasks；无笼统放行 |
| T032 | FR-010/013/015 | P05/P07/P08 | T031 | 共享类型及消费者可编译，CP-01/02/03约束可测 |
| T033 | FR-001/002/003 | P07/P08/P13 | T031 | B-V01失败/成功断言，不直接构造已批准Plan |
| T034 | FR-001/002 | P02/P08/P11 | T033 | RC-01/03来源匹配与版本冲突测试 |
| T035 | FR-001/002/003 | P03/P07/P08 | T034 | 真实SQLite字段对账和错误路径 |
| T036 | FR-003/010 | P04/P05/P08 | T035、T045 | 空Approval新配方运行准入与无效/错用途拒绝 |
| T037 | FR-002/003/004 | P03/P07/P08 | T036 | B-V02输入身份及在途快照隔离 |
| T038 | FR-002/003/004/017 | P07/P08/P13 | T037、T050、T053 | B-V01/02从输入到真实SQLite及动作/采集调用证据 |
| T039 | FR-009/010 | P01/P04/P05/P07 | T031 | B-V05现场独立TCP夹具、地址/方向/已知反馈及未明安全阻断断言 |
| T040 | FR-009/010 | P01/P04/P05 | T039 | 85点来源对照与明确能力差异，无缺字段默认false |
| T041 | FR-005–010/015 | P03/P04/P05/P07 | T040 | 全部采样/准入/完成消费者一致；不只改启动检查 |
| T042 | FR-009/010/011 | P01/P05/P09 | T040 | 正式/工具同步清单，无MB6056/6058写入 |
| T043 | FR-005–009/015/017 | P04/P07/P08/P13 | T041、T042 | B-V05/06请求审计；未知动作零重发 |
| T044 | FR-012/014 | P02/P04/P07/P13 | T031 | B-V03实际提供者及错误路径断言 |
| T045 | FR-012/014 | P01/P04/P11 | T044 | MC-01/02所有配置消费者明确分支 |
| T046 | FR-012/014/015 | P04/P06/P08 | T045、T034 | 预算来源及存储准入一致，无未知模式兜底 |
| T047 | FR-013/015 | P06/P07/P08/P13 | T032 | B-V04协议/媒体事实可复核 |
| T048 | FR-010/013 | P05/P06/P07 | T047 | CP-01/02请求/会话/帧身份一一对应 |
| T049 | FR-013 | P03/P04/P07 | T048 | 两SDK实际支持范围与恢复证据接口 |
| T050 | FR-012/013/016 | P04/P06/P07/P09 | T049 | 虚拟灯真实调用、逐组件应用事实及持久采集/灯诊断 |
| T051 | FR-002/004/013/015/016 | P03/P07/P08/P09 | T037、T050 | CP-03摘要/实际设置/帧/SQLite一致；旧记录Unknown |
| T052 | FR-012/014/016 | P02/P04/P07/P09 | T044、T045 | MC-03角色/能力映射、真实媒体与显式模拟结果及算法诊断；测试环境与用途区分 |
| T053 | FR-010/012/014 | P02/P05/P06/P11 | T041、T046、T051、T052 | B-V03所有用途消费者一致，无整套模拟覆盖真实组件 |
| T054 | FR-002/012–017 | P02/P03/P07/P08/P13 | T038、T043、T053 | B-V01–06联合软件证据及来源矩阵 |
| T055 | FR-012/013/017 | P07/P08/P13 | T058；现场相机绑定/参数依据及当次执行授权 | B-V07真实SDK采集；历史物理断线未验保留 |
| T056 | FR-001–009/012–017 | P03/P04/P07/P13 | T055；现场资料与安全/恢复合同、当次执行授权 | B-V07真机代表链；阻塞时不勾选、不运行 |
| T057 | FR-010/011/016/017 | P01/P07/P09 | T058 | B-V08逐文件同步及差异可审阅 |
| T058 | FR-016/017 | P09/P13 | T054 | B-V01–06 TRX/工具结果、实际命令/计数/日志，无全库扩测 |
| T059 | FR-016/017 | P07/P09/P13 | T057、T058；现场状态据实引用T055/T056 | 软件结论独立成立，真机证据未取得如实保留 |
| T060 | FR-010/017/018 | P01/P08/P12/P13 | T059 | 文档与实际证据一致，交付仍有明确后续义务 |

### 依赖顺序、并行和实施策略

```text
T029 → T030 → T031 → T032
                    ├→ US1 T033→T034→T035→T036→T037 ──────────────┐
                    ├→ US2 T039→T040→T041 + T042 →T043           │
                    └→ US3 T044→T045→T046 + T052                 │
T045→T036（新用途配置依赖）
T032→T047→T048→T049→T050 + T037 →T051                            │
T041+T046+T051+T052 →T053 → T038（另等T037/T050）←────────────────┘
T038+T043+T053 →T054 →T058 →T057 →T059 →T060
                       └→T055（现场相机门）→T056（现场运动门）
```

最小软件增量先完成US1保存/准入（T033–T037），随后接通参数应用、虚拟算法和现场协议；T054分别证明正常Test业务链与现场未明阻断，不将二者拼成新用途完整Host/现场链通过；US1全链完成不能只停在API成功。软件收口不等待现场值，B整阶段完成必须另外取得T055/T056证据。

- 前置T031完成后，US1测试T033、US2测试T039与US3装配测试T044文件独立，可并行；US1实现T034–T037顺序执行。
- T032完成后T047可与不修改相机fixture的配方/PLC工作并行；若需要修改同一测试公共夹具，先串行落地，不能重复并行改公共文件。
- T040完成后，工具T042可与正式T041并行；US2回归T043等待两者。
- T045完成后算法T052可与预算/存储T046及相机路径并行；T053是组合汇合点。DeploymentPrep由T034与T046共享，调度T046必须在T034结束后再写该文件。
- US4 T057在软件验证T058后执行；与现场采集文件可独立，但不在现场任务未完成时宣称真实链通过。T058统一构建/测试，不并发争用同一输出目录。
- `[P]`只表示满足上表前置且文件无冲突时可并行，不授权多代理。实现前下一步先执行speckit-analyze。

### 局部依赖与现场门

| 依赖 | 限制任务及范围 | 可继续 |
| --- | --- | --- |
| PLC-Q2已关闭 | 无阻断；T039–T042落实报警只读，不新增光栅/安全门控制 | 所有确定软件任务 |
| PLC-Q3剩余解释、PLC-Q4首次/复位/软停 | T041只实现已知部分及Unconfirmed；T056前必须补相关合同/spec/plan/tasks并分析，不能通过填false放行 | 已知位/编码/运动中清零、配方、相机和软件验证 |
| 用户安全虚拟值、现场型号/姿态/格位/完整目标/机械范围/预算/Parser规则 | T056及依赖实际值的配置；提供方式不重问，T045/T052不得植入任意测试坐标 | 明确Test夹具、结构/校验/离线链 |
| 七相机配置与SDK能力、当次现场授权 | T055；相机独立采集不等待PLC运动门，但不隐式启动运动 | T047–T054及软件构建 |
| OPEN-020-06公共Z | 仅限制公共控轴改变/删除字段/声称Z已执行 | T037/T051原XY及读存职责 |
| 页面缺口、非全幅ROI | 若实际需要，先独立前端规格/ROI用途澄清和合同；不直接新增页面/软件裁剪 | 原API/原型字段、全幅与支持参数 |
| 019等历史未验证 | 只限制相应SDK超时/物理断线/机械精度等验收声明 | 本轮定向回归；历史结果不重写 |

现场门不是给软件增加人工审批：操作员仍可在软件校验并保存后选择配方；缺少真实动作所需配置或设备状态时由软件明确拒绝。未执行T055/T056不得勾选，T059可完成如实记录但不因此结束全部B义务。

### 原型与交付边界

本任务段不生成前端页面实现任务，保留客户原型及现有交互；前端只经API和状态访问业务。T031处理共享合同不授权变更页面。FR-018部署包、源码提交、配置/验证/数据兼容回退及GitHub说明保留到B验证后的交付计划，不由本次tasks自动执行。


## Phase 8: Convergence

2026-10-08，软件范围收敛核对。结果为tasks_appended，不表示020整体完成。按spec/plan/tasks及宪章9.0.0核对当前实现，核对18项FR、8项SC、18个验收场景、10组计划决定和13条原则；发现partial 5项（HIGH 4、MEDIUM 1），无新增missing/contradicts/unrequested项。FR-018/SC-008及发布回退按原计划保留后续阶段，不在本轮生成发布执行授权。

只读核验实施清单中的428份源码/文档及所列验证证据摘要均一致；stage-b-final.trx为55执行/55通过/0失败/0跳过，工具记录58项通过。本轮未重跑测试或改写验证文件。以下是已通过用例之外的覆盖缺口，不撤销旧通过事实；T001–T060原内容/勾选保持不变。此前T054/T060完成的实际范围仍按validation-stage-b.md限定，新缺口由以下任务独立承接。

- [x] T061 [HIGH] [F1] 将正式宿主/页面的新联调用途入口缺口交接独立前端功能规格，明确desktop/HostConfiguration.cs目前仅接受Test/Simulation/Production，desktop/HostRuntime.cs仅在Test注入preparedStartRequest/token，frontend/src/runtime.js仅在Test读取准备请求且legalPreparedRequest只接受context.purpose=Test，而正式StartPublicPreparation要求RunPurpose.Commissioning；记录宿主模式、身份/权限、准备请求及既有选择配方入口的适配边界和后续页面/API验证要求，引用客户只读原型。不得通过把真实联调伪装Test、删除后端用途门或直接在020追加前端实现解决；先按独立规格完成spec/contracts/plan/tasks，后续由该规格承接源码变更与验收，020仅记录交接与证据引用。per FR-001/010/012、U1-1、SC-001/005、plan:阶段B页面/宿主边界、Constitution P12 (partial)
- [x] T062 [HIGH] [F2] 在backend/tests/Gaode.Communication.Tests/RecipeCommissioningChainTests.cs及CameraBusinessRegressionTests.cs复用现API/SQLite/正式执行器的离线双面路线，补一次坐标与一次单张曝光编辑隔离验证：运行A冻结后通过正式API按ETag保存版本B，实际观察A后续MoveRequest/CaptureRequest仍用A值；新读取并冻结运行B后实际请求使用B值，核版本/摘要、必要SQLite与媒体关联，不仅断言PlanRevision不变或只编辑Model。所有数值保持明确OFFLINE来源，不为测试放宽旧Test/Production准入；定向验证并将增量结果及字段对账引用写入validation-stage-b.md。per FR-002/003/017、U1-2、SC-001、T033/T037/T038、Constitution P07/P08/P13 (partial)
- [x] T063 [HIGH] [F3] 扩展backend/tests/Gaode.Communication.Tests/CommissioningWorkflowTests.cs的旧Test布局正常Host路线，在现WholeTray落地及AwaitingManualRemoval后，经既有人工取盘确认业务/API到最终Completed/FinalUnloadCompleted并重读真实SQLite/状态，核同Run/Tray、人工证据来源和终态事实；复用现有限等待和已确认Test人工入口，不自动假定真实料盘已取走，不新增运动/PLC安全语义，必要失败修复仅限此主流程。增量证据单列，保留旧等待人工边界证据，仍不得宣称新用途现场链通过。per FR-015/017、U3-4、spec:当前功能终点、T054、plan:阶段B终点、Constitution P08/P13 (partial)
- [x] T064 [HIGH] [F4] 在backend/tests/Gaode.Communication.Tests/ControlledCommissioningTests.cs/CommissioningWorkflowTests.cs复用显式OFFLINE联调配置和SiteProtocolTcpFixture，补RealDeviceCommissioning正式DI与StartPublicPreparation入口的最小阻断验证：合法Commissioning运行身份下，缺受控安全输入在首个依赖运动前明确拒绝；输入结构齐备但PLC-Q3/Q4未明时仍按现安全门阻断。审计loopback实际写入，断言没有相关启动/运动请求、没有未知自动重发，保存可按Run定位的状态/诊断；七机worker保持未启动，不接真实SDK，不伪造SafetyClear绕过现门禁。现直接FreezeRun/CallCount和设备端单独拒绝证据保留，新增证据只证明正式新用途入口的失败闭环，不替代T056正常真机链。per FR-012/014/016/017、U3-3、SC-006、T044/T053/T054、Constitution P04/P09/P13 (partial)
- [x] T065 [MEDIUM] [F5] 在T062–T064软件验证后修正specs/020-real-device-commissioning/plan.md阶段B续计划尾部的过时当前进度说明（仍写T029–T060全部未执行、下一步analyze），将历史时点与当前状态清楚分开；同步本功能validation-stage-b.md/document-sync.md及受影响软件证据清单，分别说明本次收敛任务、T061独立前端交接及T055/T056现场阻断。保留原有任务正文/勾选、历史TRX/失败记录、requirements只读和原型边界，不把本次converge后的tasks追加当产品源码变化或发布提交。per FR-010/017、T057/T059/T060、plan:阶段B状态入口、Constitution P01/P13 (partial)

依赖及执行范围：T062/T063/T064复用已有明确离线路线，不等待现场值；有共享fixture时串行实施，不为该表启动多代理。T065等待上述软件增量结果；T061仅登记/交接独立规格，不授权020页面改码，也不作为其余软件验证的全局前置。T055/T056继续保持未完成，现场资料与当次硬件授权满足前不执行；公共Z未决仍仅限制对应动作，历史SDK阻塞/物理断线与机械验收保持未验。包/部署/提交/GitHub及回退实操仍待后续交付计划及授权。本阶段结束后再执行speckit-converge核对实际剩余义务。

2026-10-08 Phase 8实施结果：T061仅按本轮授权完成frontend-handoff.md交接，未创建或完成后续独立前端规格/页面实施；T062–T064新增证据见validation-phase8.md（最终12/12，Host构建通过）；T065进度及清单已同步。仅测试与020文档增量，未改产品或共享接口；原T001–T060正文/勾选不改，T055/T056继续Blocked。历史converge的partial是追加任务时点，不代表本轮尚未执行。
