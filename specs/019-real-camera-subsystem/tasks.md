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
