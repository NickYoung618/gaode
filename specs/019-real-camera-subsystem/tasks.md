# 功能任务：019 正式真实相机子系统

日期2026-10-07；spec/plan/contracts/data-model/research为依据；宪章9.0.0。用户授权完整流程，保留既有功能状态。无前端任务。

## Phase 1: Setup

- [x] T001 同步CL-001及治理/设计前置产物于 specs/019-real-camera-subsystem/（FR-019）。
- [x] T002 核查SDK依赖/版本/许可，记录 specs/019-real-camera-subsystem/research.md（FR-020）。

## Phase 2: Foundation

- [x] T003 定义真实帧metadata、按设备epoch/容量、可选光源与journal契约于 backend/src/Gaode.Application/Ports/（FR-007/009/012/013）。
- [x] T004 实现有界持久管道framing/会话关联于 backend/src/Gaode.Infrastructure/Devices/Cameras/CameraWorkerProtocol.cs（FR-008/011）。

## Phase 3: US1 独立采集

独立验证：正式Host单台Galaxy完成请求、新帧、保存与读取。

- [x] T005 [US1] 实现正式worker工程及Galaxy驱动/绑定/参数备份读回于 backend/src/Gaode.CameraWorker/（FR-001/002/005/006/011）。
- [x] T006 [US1] 实现CameraPro发现/绑定/XYZ深度IR完整包于 backend/src/Gaode.CameraWorker/CameraProDriver.cs（FR-001/010/018）。
- [x] T007 [US1] 实现持久进程gateway和正式事件adapter于 backend/src/Gaode.Infrastructure/Devices/Cameras/（FR-002/003/008/012/015）。
- [x] T008 [US1] 实现共用业务接管/采集服务于 backend/src/Gaode.Application/Acquisition/CameraAcquisitionService.cs（FR-009/011/016）。
- [x] T009 [US1] 正式Host纯采集组合根/后端接口/配置及Production真实接线于 backend/src/Gaode.Host/（FR-004/009/010/016）。
- [x] T010 [US1] 正式后端验证一台2D连续3帧并保存证据于 specs/019-real-camera-subsystem/evidence/（SC-001/002/003）。

## Phase 4: US2 生命周期

独立验证：至少两设备独立生命周期，超时/断线后不重放原请求。

- [x] T011 [US2] 实现独立状态/有限超时/受控恢复/正常关闭于 backend/src/Gaode.Infrastructure/Devices/Cameras/PersistentCameraGateway.cs（FR-003/006/017）。
- [x] T012 [US2] 修正既有消费者按binding epoch/实际容量/共用接管及F业务单次作用域于 backend/src/Gaode.Application/Acquisition/AcquisitionCoordinator.cs 和 Workflow/RecipeDetectionExecutor.cs（FR-009/013/015/016）。
- [x] T013 [US2] 核心协议会话/截断及故障不重拍验证于 backend/tests/Gaode.Communication.Tests/CameraProtocolTests.cs（SC-005）。

## Phase 5: US3 存储重启退出

独立验证：提交前不Ready，提交后重启仍可读；正常关闭恢复参数。

- [x] T014 [US3] MediaStore阶段保存/附加元数据/提交后发布/重启恢复于 backend/src/Gaode.Infrastructure/Media/MediaStore.cs（FR-012/014）。
- [x] T015 [US3] 复用现有SQLite表实现独立采集意图/原子索引事实/失败事实于 backend/src/Gaode.Infrastructure/Persistence/CameraCaptureJournal.cs（FR-014/017）。
- [x] T016 [US3] 既有业务保存后发布及启动索引恢复于 backend/src/Gaode.Host/Lifecycle/Station01HostedService.cs 和 Application/Workflow/RecipeDetectionExecutor.cs（FR-014）。
- [x] T017 [US3] 必要媒体提交/失败/重启读取与既有6个通信测试于 backend/tests/Gaode.Communication.Tests/（SC-004/005/007）。
- [x] T018 [US3] 正式后端验证3D连续3帧、XYZ深度IR结构、保存/退出/重启于 specs/019-real-camera-subsystem/evidence/（SC-001–006）。

## Phase 6: Delivery

- [x] T019 正式后端逐台覆盖七台、跨相机独立性/必要故障和退出于 specs/019-real-camera-subsystem/evidence/（SC-001–007）。
- [x] T020 交付发布脚本/可运行包、依赖配置及回退说明于 scripts/Publish-CameraSubsystem.ps1 和 specs/019-real-camera-subsystem/quickstart.md（FR-020/SC-008）。
- [x] T021 执行converge并如实记录未验证/限制于 specs/019-real-camera-subsystem/tasks.md（FR-019）。

## 依赖与实施策略

T001/002→T003/004→T005–009；一台2D验证T010后3D硬件T018，再七台T019。存储T014/015必须在真实采集提交前完成，T011与SDK生命周期同批；T012/016在新共用接口落地后完成。主流程增量先保证单台完整链再扩大设备。无需额外进程池/总线/算法/光源或前端。可并行的只读SDK研究已按plan skill委派，写同一文件禁止并行；实现代理仅承担明确互不重叠文件，主代理负责集成。未实际完成不得勾选，未执行硬件项标未验证。

## Phase 7: Convergence

- [x] T022 完成最终发布包清理、精确源码revision/文件SHA256清单、ZIP与摘要，并同步最终验证和部署记录 per FR-020/SC-008（partial）。

收敛核对20 FR/8 SC/3用户故事、plan关键决策与宪章P01–P13：当前可实现源码未发现阻断缺口；交付包清单尚需与正式提交对齐，追加T022。SC-005实机超时/物理断线未验证，如validation.md所列；不由离线通过推导硬件通过。

实施回合已完成T022：交付清单见evidence/delivery.json，运行二进制对应7f1b1ac，交付文档对应9c13c53，ZIP不包含运行token、现场配置、数据库、CameraPro native DLL。T013/T017/T019必要故障覆盖为协议/离线超时和实际SQLite/文件失败；实机SDK超时及物理断线仍未验证，不以勾选任务声称硬件故障验收通过。

## Phase 8: Review fixes（67e4a57）

- [x] T023 核实审查问题/建立修复回退点/同步spec contracts plan tasks，保持历史证据（FR-019）。
- [x] T024 修复PersistentCameraGateway进程退出观察、状态同步和停止准入，使用真实离线可控worker进程验证Ready后退出/迟到事件/不重拍（FR-003/011/017）。
- [x] T025 修复MediaCapacity/MediaStore存量载荷配额恢复及失败文件结算，验证满额重启/重复恢复/失败残留/已提交仍可读（FR-013/014）。
- [x] T026 修复recover Faulted门禁/新会话成功/失败异常及HTTP状态映射；离线真实gateway验证，受拒的模拟Host HTTP不执行且明确未验证（FR-017）。
- [x] T027 完善独立验收及产品真实帧结构门禁，固定3D通道和元素/尺寸关系、2D身份/格式/可解释布局，实际服务负例验证（FR-001/011/018）。
- [x] T028 选择AcquisitionCoordinator/RecipeDetectionExecutor/Observation必要虚拟业务回归，确认来源/意图/期限/取消/动作关联/保存门禁（FR-004/009/014/016）。
- [x] T029 审计并保留旧清单差异与最终21份媒体对应关系；发布确切新源码二进制，必要新版本实机正常链/退出/重启验证，停止后冻结新证据/包清单（FR-020/SC-001–008）。
- [ ] T030 更新validation quickstart workflow及交付/回退，执行converge；未满足的产品入口/硬件条件保持未完成（FR-019/020）。

依赖：T023/一致性analyze→T024–027→T028→T029→T030；所有本轮任务按实际验证勾选，不改变前轮历史完成事实，不以本轮文档覆盖旧验收限制。

本轮执行结果：T026源码及离线恢复验证、真实健康recover HTTP409已完成；故障HTTP503/意外退出HTTP拒绝受APR-002限制未测，T026保持未完成。T029新二进制七台各3帧/重启21份/两轮14份退出恢复已验证，新根219文件已冻结；发布ZIP及最终交付记录正在完成。前轮历史勾选不代表本轮缺口关闭。

T029交付已完成：新ZIP/manifest见evidence/delivery.json，确切运行源码a0e70e3、交付材料aff4a92。停止后219项冻结再次全量摘要一致；旧交付记录及214项原清单分别保留。T026/T030的故障HTTP验收条件尚未满足，保持未完成。

## Phase 9: Convergence

- [ ] T031 在允许的审批及运行条件下补齐正式入口故障恢复HTTP503、worker意外退出后HTTP采集拒绝的验收证据，并补齐SC-005实机SDK超时/物理断线证据；沿用现有实现，关联validation.md中的APR-002/APR-001，不改写命令绕过拒绝，不以离线验证替代产品入口或实机结论（FR-017/SC-005，partial/HIGH；承接T026/T030）。

本轮converge核对20 FR、8 SC、3用户故事及验收场景、计划关键决策和宪章P01–P13。源码修复与正常实机链有证据，剩余故障验收门禁为partial/HIGH；追加T031，T026/T030保持未完成。结果为tasks_appended，019尚未最终收敛。

## 2026-10-07剩余故障验收授权

用户明确重新授权隔离模拟worker＋正式Host的127.0.0.1 HTTP复验，使用全新独立目录/SQLite，不连接真实设备；先前APR-002保留为历史拒绝，不再将其解释为本轮用户禁止。执行脚本scripts/Invoke-CameraHttpFaultAcceptance.ps1，固定测试fixture且SDK路径指向空目录，核实监听地址、无SDK模块、Ready后Exit(17)记录、HTTP409拒绝、显式恢复失败503及新会话Ready，最后正常关闭。发生新的系统拒绝则原样保存并停止，不改写命令。实机SDK超时/物理断线仅形成设备/步骤/参数备份与恢复方案，待用户另行确认；T031仍按实际证据分项关闭，不以HTTP离线结果替代实机。

T031进展：重新授权的正式Host HTTP退出采集409、失败恢复503及新会话Ready200已完成，见evidence/http-fault-20261007/；剩余实机两场景见hardware-fault-acceptance-plan.md，尚未执行/等待用户确认，因此T031/T030保持未完成。

020阶段B共享实施前置（2026-10-08）：当前worker升级v2，新增原子设置/读回/触发及逐组件事实；无设置CaptureOnly保持参数；历史v1证据不改。精确增量以[020合同](../020-real-device-commissioning/contracts/camera-parameters.md)及[任务T031–T060](../020-real-device-commissioning/tasks.md)为准；只承接本次已授权接口，不扩大页面。保留原正文/勾选及历史验收时点。
