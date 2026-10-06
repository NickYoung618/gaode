> 当前008执行以文末“S0—S5任务增量”为准；此前原版本范围/固定样本/两配方/原型只读等冲突条款仅属历史。未受影响任务保留原状态。原文已逐字节归档：[历史任务](tasks-history-before-s0-s5-20260924.md)。

# 功能任务清单：第一工位完整虚拟集成闭环

**输入**：[007规格](spec.md)、[技术方案](plan.md)、[研究](research.md)、[数据模型](data-model.md)、[虚拟集成合同](contracts/virtual-integration.md)、[联调入口合同](contracts/commissioning-cli.md)、[验证指南](quickstart.md)及003/006当前有效合同  
**宪章版本**：6.0.0  
**日期**：2026-09-24  
**状态**：首批有限实现已完成有证据的T001/T003–T014/T016–T018；T002/T015及后续验收任务保持未完成。  
**规格范围**：006实际WPF/WebView2前端经正式Host API启动，独立VirtualPlc、固定目录虚拟相机、独立虚拟算法、版本化模拟配方、真实SQLite和媒体存储协同执行003已确认工艺；解锁事实提交后由受控客户端自动模拟取盘，最终同一runId达到FinalUnloadCompletion。

> 每项任务都列明007 FR/SC、宪章原则、真实前置、完成条件和证据。T001是相关共享后端接口代码的文档前置；T002记录006页面/宿主交付依赖，只限制实际前端联调及完整验收，不阻塞不依赖前端的007后端与虚拟组件实现。007清单不授权直接修改003/006，也不改其任务勾选。006页面和宿主实现始终归006。本清单完成只证明已实际执行的007虚拟软件闭环，不证明真机、精度、生产验收或全项目完成。

## 阶段1：准备与跨功能门禁

- [X] T001 同步003受影响的共享规则与合同：核对并在独立授权下最小修订 `specs/003-plc-latest-protocol/spec.md`、`contracts/detection-port.md`、`contracts/public-preparation-handoff.md`、`contracts/station01-main-flow-api.md`、`contracts/whole-tray-workflow.md`、`contracts/component-source-matrix.md`、`plan.md`、`tasks.md`，仅在实际接口变化涉及时同步该目录其他合同；统一逐检测采集对应独立算法调用、确认API唯一路由/字段、Test模拟ManualActor来源，保留原安全/授权/短事务和既有任务勾选。（007 FR-005/009/010/012/013/017、SC-003/008；P01/P07/P08/P12/P13；依赖：对应文档授权；完成：共享合同、设计、任务互相一致且未将模拟取盘记为AuthenticatedHuman；证据：文档差异与引用核对记录 `specs/007-station01-integrated-loop/evidence/shared-doc-sync.md`。）
- [X] T002 [P] 核对006外部依赖并登记可消费证据于 `specs/007-station01-integrated-loop/evidence/frontend-dependency.md`：006按独立授权同步其 `spec.md`、`contracts/api.md`、`contracts/prototype-mapping.md`、`contracts/gaps.md`、`plan.md`、`tasks.md`，随后由006完成已有页面入口的完整启动请求体、受控Test凭据传递、WebView2页面侧API/通知消费、完整阶段/最终状态已有位置绑定及原型哈希勘误；007负责Host侧限定Test来源的跨源配置、后端授权验证和实际连通性，归T010与T015，不分派给006页面/宿主代码任务。（007 FR-001/011、SC-001/005；P01/P12/P13；依赖：006独立授权和实施；完成：006交付可核对的合法StartRunContext、受控凭据传递与GET/通知状态展示能力，原型三页及完整SHA-256不变；与007 Host实际连通在T015共同验收，006未完成不阻塞独立的007后端/虚拟组件任务；证据：006合同版本、其验证路径、007只读API/页面对照记录。）
- [X] T003 统一独立worker的启动所有权与命令说明：按现有 `backend/src/Gaode.Infrastructure/Algorithms/WorkerProcessSupervisor.cs` 由Host启动、管理和停止唯一独立worker子进程，先在 `specs/007-station01-integrated-loop/plan.md`、`contracts/commissioning-cli.md`、`quickstart.md` 对齐进程树、启动/停止/PID语义；PowerShell平台启动Host并核对worker实际PID/健康/日志，不重复启动worker，单独Python命令仅作手动协议调试。（007 FR-005/014/016、SC-003/007；P05/P06/P13；依赖：T001；完成：三个007文档与本任务对唯一进程所有权、可执行命令及证据一致；证据：文档差异、Host/worker预期进程树说明 `specs/007-station01-integrated-loop/evidence/worker-startup-boundary.md`。）

## 阶段2：基础测试配置与预算

- [X] T004 [P] 建立仅供007的版本化Test配置与固定图片清单，产物 `specs/007-station01-integrated-loop/examples/virtual-loop.json`、`examples/virtual-algorithm.json`、`examples/images.json`及单一 `fixtures/images/`：冻结输入SHA-256、3D/F/Detection用途、随机种子/合法值域、S1 `R-S1-A-CAP` v`0.4.0-review`、P01占位及唯一F测试码；复用已有配方加载/校验而不预造handoff。（007 FR-003/004/006/007/013、SC-002/004/008；P03/P07/P08/P11；依赖：007现行合同；完成：清单中的固定图片可读、来源为Test、F码只指向该合法模拟配方且不含原型归档修改；证据：版本/摘要清单 `specs/007-station01-integrated-loop/evidence/fixture-manifest.json`。）
- [X] T005 基于现有001 schema新增版本化有限联调预算 `specs/007-station01-integrated-loop/examples/budget.virtual-loop.json`，并在 `specs/007-station01-integrated-loop/evidence/budget-calculation.md` 核算公共3D/F每次3–5秒采集、10秒算法及保存/IPC余量，预估CAP/P01 Detection C=2、A=2、模拟延迟上界30秒，保留003每阶段120秒和原重试；不改写旧1.0.0测试预算。（007 FR-003/005/017、SC-001/002/003；P04/P06/P08/P11/P13；依赖：T001、T004；完成：新预算能经现有schema加载，窗口均严格覆盖指定正常延迟与适用开销，计算式 `5C+10A+T+R<120秒` 有明确余量假设；证据：配置校验输出及预算计算表。）

## 阶段3：US1 实际前端启动并完成同runId闭环

**目标**：复用003正式工艺和006实际前端，通过全部指定虚拟组件及真实保存，达到FinalUnloadCompletion。  
**独立完成条件**：至少一次从006实际WPF页面启动的CAP/P01合法完整样本，运行时正式计划与实际采集/算法次数一致；同runId的媒体、worker、配方、PLC、SQLite及模拟取盘来源均可核验，最终状态来自后端持久化查询。T015完成本场景验证；T002是明确外部前置。

- [X] T006 [P] [US1] 实现固定目录图片的正式 `ICapturePort` 适配于 `backend/src/Gaode.Infrastructure/Simulation/FileBackedCapture.cs`，读取T004冻结清单，按角色/计划步骤产生独立captureId及原始字节，实际墙钟3–5秒完成拍照读取并交现有MediaStore；缺图/超限返回真实失败，不使用SyntheticMediaFixture冒充。（007 FR-003/004/017、SC-002；P03/P06/P07/P08；依赖：T001、T004、T005；完成：每次正式调用都有输入摘要、新mediaId和起止时间，所有正常采集耗时在指定范围；证据：`artifacts/station01-007/<evidence-id>/input-images.json`、`media-index.json`及可读取媒体摘要。）
- [X] T007 [P] [US1] 实现独立虚拟算法worker `scripts/virtual-station01-algorithm.py`，按T003确定的Host管理边界使用现有NDJSON信封，实际读取受控媒体输入；Height/FDecode/Detection各请求模拟10秒计算并按T004冻结种子/合法域返回相应结果，正常F实际返回CAP唯一测试码。（007 FR-005/006/013、SC-003/008；P03/P04/P05/P07；依赖：T001、T003、T004、T005；完成：独立PID接收Execute并返回可按种子复核的Result，身份/媒体关联不由worker伪造PLC或保存事实；证据：`artifacts/station01-007/<evidence-id>/algorithm-calls.json`及worker收发/时间日志。）
- [X] T008 [US1] 将正式 `IAlgorithmPort` 接到独立worker，最小补全 `backend/src/Gaode.Infrastructure/Algorithms/PythonWorkerAdapter.cs`、`WorkerMessages.cs`、`WorkerProtocolCodec.cs` 与受控媒体租约映射；保持已有算法意图先提交、有限期限、迟到结果隔离，不能在断联时隐式退回进程内模拟。（007 FR-005/006/008/017、SC-003/004；P04/P05/P06/P07/P08；依赖：T001、T003、T007；完成：Host真实派发公共Height/F及Detection调用，worker PID/会话、callId、媒体摘要和合法响应被保存，10秒计算不占用数据库事务；证据：`artifacts/station01-007/<evidence-id>/algorithm-calls.json`与算法意图/结果SQLite引用。）
- [X] T009 [US1] 把003正式Detection入口按冻结 `RecipeRunPlan` 逐必检步骤接至T006采集和T008算法端口，在 `backend/src/Gaode.Host/Composition/AdapterBindings.cs` 及必要的 `backend/src/Gaode.Application/Workflow/` 现有编排文件中只改当前接线；每个检测采集步骤对应一次worker请求，保留对象/位置/结果/媒体关联和003的Sorting/UnloadPreparation续接，禁止直接构造整批OK。（007 FR-005/007/009/010/017、SC-001/003/004；P03/P04/P05/P07/P13；依赖：T001、T005、T006、T008；完成：正常CAP/P01正式计划预计两次检测采集、两次算法请求且均有独立保存证据，真实后续工艺不靠第二次启动；证据：`artifacts/station01-007/<evidence-id>/plan-and-stage-events.json`、媒体及worker索引。）
- [X] T010 [US1] 在 `backend/src/Gaode.Host/Composition/Station01Registration.cs`、`backend/src/Gaode.Host/Api/QueryEndpoints.cs` 及必要的Host启动/授权配置中显式选择007文件采集、独立worker和T009 Detection，同时仍用已有VirtualPlc正式Modbus及模拟光源；状态查询/通知反映实际来源与可用性，不再对已接通组件固定显示NotIntegrated。由007交付仅在Test配置下允许受控页面来源访问API/通知的Host跨源配置，验证预检、实际请求/通知及后端授权结果；涉及共享接口先经T001同步合同，不放宽业务授权，也不等待T002完成才实施Host侧工作。（007 FR-001/002/011/013、SC-001/005；P02/P05/P07/P09/P12；依赖：T001、T006、T008、T009；完成：Host只保留一个PLC控制入口，实际连接代次反馈和来源正确，GET/通知不以演示值替代事实；限定Test来源的跨源响应及有/无授权的Host侧API/通知连通性可核对，006页面联通仍由T015验收；证据：`artifacts/station01-007/<evidence-id>/process.json`、`modbus-audit.json`、`api-transcript.json`及Host跨源/授权请求记录。）
- [X] T011 [US1] 核对T004测试配方经现有加载/校验、真实F识别、计划构建/绑定和已提交handoff，在 `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`、`backend/src/Gaode.Application/Recipes/RecipeRunPlanner.cs` 仅对阻断CAP/P01正规路径的实际缺口做最小接线；不减少配方必检步骤或预写成功移交。（007 FR-001/007/009/017、SC-001/004；P03/P07/P08/P11；依赖：T001、T004、T008、T009；完成：同一runId冻结F码/配方版本/planRevision与实际占位，运行时正式计划数和算法请求数明确，120秒阶段余量按T005公式复核；证据：`artifacts/station01-007/<evidence-id>/recipe-plan-budget.json`及提交的handoff引用。）
- [X] T012 [US1] 核对并补足新增文件采集/worker/阶段事实通过既有 `backend/src/Gaode.Infrastructure/Media/MediaStore.cs`、`backend/src/Gaode.Infrastructure/Persistence/StageEventStore.cs` 和真实SQLite保存链的最小关联；保留意图/实际调用/反馈短事务和媒体可读性，不新建旁路写入通道。（007 FR-004/008/010/013、SC-004/008；P06/P07/P08；依赖：T001、T006、T008、T009；完成：同runId可从只读SQLite查询到每个媒体、算法、配方、PLC及阶段写入引用，媒体文件摘要与清单相符；证据：`artifacts/station01-007/<evidence-id>/sqlite-events.json`、`sqlite-projections.json`、`media-index.json`。）
- [X] T013 [US1] 按T001统一后的确认合同最小修正 `backend/src/Gaode.Host/Api/RunEndpoints.cs`、`Station01ApiContracts.cs`及必要的 `backend/src/Gaode.Application/Workflow/WholeTrayWorkflowOrchestrator.cs`：受控Test客户端在同run整盘/ObservedUnlocked已提交后确认，记录Simulated/Test ManualActor与授权测试身份/渠道，保留revision、幂等、短事务及最终原子提交，不再将自动模拟操作硬编码AuthenticatedHuman。（007 FR-009/010/012/013、SC-001/004/008；P04/P07/P08/P12；依赖：T001；完成：提前/无权限/错版本不能Final完成，成功确认与FinalUnloadCompletion原子保存且FinalSourceMatrix为SoftwareLoopOnly；证据：`artifacts/station01-007/<evidence-id>/manual-final.json`、矩阵和API记录。）
- [X] T014 [US1] 实现启动时可启用的受控自动模拟取盘监视 `scripts/watch-station01-auto-removal.ps1`：仅从后端公开状态发现前端新runId，核对已提交WholeTrayCompletion和ObservedUnlocked，以稳定requestId/当前revision调用T013确认API并查询最终事实；不得在启动瞬间、计时结束或运行失败时提前确认。（007 FR-012/014/016、SC-001/007；P05/P07/P08/P12/P13；依赖：T001、T013；完成：前端启动样本无需再人工输入，监视可重复查询而不双确认，明确Test客户端渠道；证据：`artifacts/station01-007/<evidence-id>/auto-removal-transcript.json`。）
- [ ] T015 [US1] 待T002的006页面交付及T010的007 Host侧限定Test来源跨源/授权配置均可消费后，使用实际WPF/WebView2现有入口、T004合法CAP/P01完整样本和全部指定组件执行一条正式前端启动到FinalUnloadCompletion联调；核对页面带受控Test凭据的启动请求、API/通知实际连通及已有位置的完整后端状态，并按 `specs/007-station01-integrated-loop/quickstart.md` 保存进程/图片/worker/配方/PLC/SQLite全链事实；不得由脚本启动样本、旧进程内模拟或静态页面替代。（007 FR-001–013/017、SC-001–005/008；P03–P09/P12/P13；依赖：T002、T005–T014及003 T061，其中T002与T010共同构成实际页面连通前置；完成：specs/003-plc-latest-protocol T061的连续两次运动定向证据通过，且同runId实际前端经授权提交合法请求，CAP/P01必检计划无删减、C=2/A=2或按实际合法计划重新核算，采集3–5秒/算法10秒且Detection在120秒内，最终持久化事实与页面GET/通知一致；证据：`artifacts/station01-007/<evidence-id>/manifest.json`、`frontend-operation.json`、`api-transcript.json`、`final-result.json`及其索引全部原始文件。）

## 阶段4：US3 简易平台启动、模拟上料与自动确认

**目标**：操作员用可复制命令启动全组合，准备模拟上料；脚本可辅助发起正式运行，亦可仅准备输入供006实际前端启动；自动确认随启动启用。  
**独立完成条件**：启动/模拟上料入口在空闲Test根可运行，输出进程、配置、runId或准备清单和实际查询；脚本辅助启动的完成状态不冒充US1前端样本。

- [X] T016 [US3] 编写 `scripts/start-station01-virtual-loop.ps1`，复用现有VirtualPlc、Host、006桌面程序；由Host通过WorkerProcessSupervisor启动唯一独立worker子进程，平台核对并记录worker PID/健康/日志，不另起worker或相机服务；设置007Test配置/图片根、Host与桌面一致地址及受控凭据，启动T014监视并记录各进程命令/退出方式。（007 FR-014/016、SC-007；P02/P05/P06/P09；依赖：T003–T005、T007、T010、T014；完成：平台可启动/复用全组合并查询各组件事实，006页面依赖未就绪时如实报受限而不阻塞脚本与后端/虚拟组件交付，活跃run不被自动重置；实际页面连通仍受T002与T015约束；证据：`artifacts/station01-007/<evidence-id>/process.json`及启动日志。）
- [X] T017 [P] [US3] 编写 `scripts/simulate-station01-load.ps1`，从T004配置准备Test托盘/占位与版本引用，提供PrepareOnly供006前端启动及显式StartRun辅助模式；后者只调用授权Host `POST /api/v1/station01/runs` 并查询原requestId/commandId/runId，不直写VirtualPlc完成反馈、Host投影或SQLite。（007 FR-001/015/016、SC-007；P04/P05/P07/P12；依赖：T004、T005、T010；完成：CAP/P01输入合法，PrepareOnly无业务启动，StartRun的202仅记录受理且可继续查询；证据：`artifacts/station01-007/<evidence-id>/load-and-start-transcript.json`。）
- [X] T018 [US3] 按T003确定的唯一启动所有权补齐 `specs/007-station01-integrated-loop/quickstart.md` 与 `contracts/commissioning-cli.md` 中实际可复制的PowerShell/Host/桌面/worker命令、前置条件、配置及日志位置；明确旧两进程脚本和辅助API样本的证据范围。（007 FR-014/016、SC-007/008；P01/P09/P13；依赖：T016、T017；完成：指南中每条标为可执行的命令均对应现存入口，文档不称未运行项通过；证据：命令核对记录 `specs/007-station01-integrated-loop/evidence/start-command-check.md`。）
- [X] T019 [US3] 执行T016/T017最小平台冒烟和一次辅助API样本，核对全组件来源、准备输入、实际授权启动、状态查询及解锁后T014自动确认；与T015实际前端样本分开记载。（007 FR-014–016、SC-007/008；P05/P07/P09/P13；依赖：T016–T018；完成：脚本退出/健康/202/最终查询各有独立状态，未达到Final不报通过；证据：`artifacts/station01-007/<evidence-id>/platform-smoke.json`、日志及辅助runId。）

## 阶段5：US2 必要失败不伪造安全与完成

**目标**：只验证新增接线和直接决定流程安全、数据真实性、完成条件的失败；旧003/006证据仅在相同代码/合同/配置行为未受影响时复用。  
**独立完成条件**：F1–F7各风险由一次相关验证或有效旧证据支持，失败事实保存可查，且没有假媒体/OK、未知动作重发、提前确认或漏保存却报告Final完成。

- [x] T020 [P] [US2] 在 `backend/tests/Gaode.Integration.Tests/Station01/VirtualMediaAndWorkerFailureTests.cs` 覆盖新增固定图片缺失/媒体保存失败、worker未接收/超时或错callId/媒体引用的必要路径，验证无假媒体、默认OK、无限等待或隐式模拟fallback。（007 FR-003–006/008/017、SC-002/003/006；P04/P07/P08；依赖：T006–T010、T012；完成：每类新增失败均有明确终态和原始错误/调用/期限关联，受影响完成条件未被误判；证据：测试结果与 `artifacts/station01-007/<evidence-id>/necessary-failures.json`。）
- [x] T021 [US2] 在 `backend/tests/Gaode.Integration.Tests/Station01/VirtualRecipeAndDetectionGateTests.cs` 验证3D安全Z失败、F码不唯一/配方不匹配、Detection算法有限失败后的合法Pending或MappingFailed，并只复用未改动的003映射/分拣断言；不以失败路径代替正常CAP/P01通过。（007 FR-006/007/009/017、SC-006；P03/P04/P07/P11；依赖：T008–T011；完成：无猜Z、旧F/旧配方绑定或硬编码OK；合法Pending仍走正式分拣，歧义受限；证据：测试结果、计划/动作/SQLite引用与复用依据。）
- [x] T022 [P] [US2] 在 `backend/tests/Gaode.Integration.Tests/Station01/VirtualPlcSafetyGateTests.cs` 对T010受影响的PLC连接代次、心跳/断联、已派发动作未知和整盘前解锁门禁做最小验证；相同行为未改时引用003已有证据并说明版本/未影响理由。（007 FR-002/009/017、SC-006；P04/P06/P07；依赖：T010；完成：当前代次外反馈不算完成，UnknownHeld不盲重发且未提交WholeTrayCompletion前无解锁；证据：Modbus写入审计、阶段事件及 `artifacts/station01-007/<evidence-id>/evidence-reuse.md`。）
- [x] T023 [US2] 在 `backend/tests/Gaode.Integration.Tests/Api/VirtualManualCompletionGateTests.cs` 验证必要SQLite提交失败/提交未知及T013确认API无权限、未ObservedUnlocked、错revision/重复requestId的处置，复用未改003原子提交证据而只重验新Test来源与接口变更处。（007 FR-008/010/012/013/016、SC-004/006/008；P04/P07/P08/P12；依赖：T012–T014；完成：未提交不能Final完成，重复查询/调用不产生双份确认，FinalSourceMatrix不冒充AuthenticatedHuman；证据：API响应、只读SQLite事件/矩阵与测试结果。）

## 阶段6：收尾与完成判定

- [ ] T024 汇总T015/T019/T020–T023及未受改动影响的003/006证据，在 `artifacts/station01-007/<evidence-id>/final-result.json` 和 `evidence-reuse.md` 逐项列007 FR-001–018、SC-001–008的Passed/Failed/Blocked/NotRun、配置/代码/合同版本及原始文件；只在实际前端样本全部必需组件、必要失败和真实持久化证据均满足时判定SoftwareLoopOnly通过。（007 FR-018、SC-001–008；P01/P08/P09/P12/P13；依赖：T015、T019–T023；完成：同runId可复核FinalUnloadCompletion与来源矩阵，延期、阻塞、未执行无通过标记，不声明真机、精度、生产或全项目验收；证据：最终索引、SQLite/媒体/PLC/worker/前端原始引用。）

## 当前阶段范围与完成证据（P13）

| 项目 | 对应007规格 | 任务与证据 |
| --- | --- | --- |
| 起点与终点 | US1-A/C、FR-001/009、SC-001 | T002、T015；实际006前端202受理到同runId的FinalUnloadCompletion |
| 必须参与的组件 | FR-002–010/012–015 | T004–T014、T016/T017；进程、正式端口、媒体、worker、配方、PLC、SQLite及模拟确认原始证据 |
| 必要验证 | US1–US3、F1–F7、SC-002–007 | T015、T019–T023；新增路径实际验证，未改路径附复用依据 |
| 完成证据 | FR-010/013/018、SC-004/008 | T024；同runId索引、只读DB/媒体核对、ReadyForUnlock及FinalSourceMatrix，来源SoftwareLoopOnly |
| 延期项 | 007“本次不包含”及P13 | 其他工位、生产鉴权/真机/精度、完整配方管理、长稳压力与非阻塞异常优化待项目主流程跑通后再安排；不设007门禁 |

## 依赖顺序与并行机会

```text
T001(003最小同步) ──> T003、T005、T006/T007 ──> T008 ──> T009 ──> T010/T011/T012
T004(配置/图片) ──────┘                         └──────────> T013 ──> T014
T002(006页面请求/凭据/展示) ────────────────────────────┐
T005–T014(含T010 Host跨源/授权) + T002 + specs/003-plc-latest-protocol T061 ──> T015(双方实际连通与前端完整链)
T003/T007/T010/T014 ──> T016(不等T002)；T004/T005/T010 ──> T017；T016/T017 ──> T018/T019
T006–T014 ──> T020–T023；T015/T019/T020–T023 ──> T024
```

T002可与T001及T004独立推进；006尚未交付不阻塞T006–T014的007后端/虚拟组件和T016脚本实现，双方在T015实际连通验收汇合。T006文件采集与T007worker在共同文档/配置门禁关闭后可并行；T016启动脚本与T017模拟上料脚本使用不同文件，可在各自前置完成后并行；T020与T022验证不同受影响路径及独立Test根，可并行。所有运行证据使用独立runId/数据根，不并发争用同一VirtualPlc实体状态。

**建议的最小可验收增量**：先关闭T001共享后端文档前置和T003启动所有权差异，即可推进T004–T014及T016/T017中不依赖页面的工作；T002由006独立推进。T002与T010的双方交付及003 T061定向修正就绪后，以T015证明一条CAP/P01真实前端闭环；T016–T019简易平台及T020–T023必要失败完成后，T024才给007阶段结论。T015以前的局部通过不能宣称007完成。

## 关键规则与外部依赖

| 当前适用规则及来源 | 对应任务 | 必要事实 |
| --- | --- | --- |
| 003工艺、正式入口及VirtualPlc安全反馈 | T001、T009–T011、T015、T022 | PLC匹配代次，整盘后解锁，未知动作不重发 |
| 3–5秒图片和10秒独立算法真实收发、有限期限 | T004–T009、T015、T020/T021 | 固定文件内容/媒体SHA、worker请求响应/PID与墙钟，CAP/P01实际C/A、按冻结RecipeExecutionBudget确定的余量 |
| 版本化模拟配方、实际SQLite及媒体 | T004/T005、T011/T012、T015、T023 | F真实识别、快照/计划/handoff及短事务提交可查 |
| Test模拟取盘、授权/来源矩阵 | T001、T013/T014、T015、T023 | 已提交ObservedUnlocked后客户端自动确认；ManualActor明确Test而非真人 |
| 006独立前端、只读原型和后端API边界 | T002、T010、T015 | 006实现页面请求/受控Test凭据/已有位置完整展示；007实现Host侧限定Test来源的API/通知跨源和授权连通，T015核对双方实际通信 |
| 证据复用与阶段结论 | T020–T024 | 旧证据逐项解释未受影响；未执行/阻塞/延期不得勾选通过 |

| 外部依赖/门禁 | 来源 | 只限制的任务 | 何时关闭 | 可独立推进 |
| --- | --- | --- | --- | --- |
| 003共享合同与确认API、ManualActor来源 | 007 plan R5/共享文档规则；T001 | T006–T014中涉及共享接口的代码及T015/T023 | 相关代码修改前，需独立授权最小同步003 spec/contracts/plan/tasks | T004图片/测试配置、006外部对齐 |
| 006页面启动请求、受控Test凭据、已有位置完整状态展示及哈希 | 006独立规格；T002 | T015、T024完整结论；不阻塞T010或T016脚本实现 | 006按其独立任务完成并给可核对证据，且T010 Host侧限定Test来源的跨源/授权配置可实际连通后 | 007后端/worker/图片/预算、启动平台及辅助API样本 |
| 007 worker启动所有权 | 已在007方案/命令合同/指南统一Host唯一管理worker；T003仍需核对交付证据 | T007/T008/T016及命令验收 | worker/启动脚本代码前核对进程树、PID/健康/日志与文档一致 | T004/T005/Test配方 |
| 旧预算低于指定延迟 | research R3；T005 | T006–T015正常时间证据 | 新版本Test预算经schema校验并测量余量 | 003/006文档对齐 |

## 客户确认原型检查（P12）

客户只读 `E:\dzk\gaode\原型.zip` 的SHA-256为`3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`，页面 `a.html`、`data-view.html`、`login.html`。T002由006独立处理现有位置与API映射，T015只检验实际前端作为消费者的请求和展示。原型没有取盘控件时由T014受控客户端完成Test模拟确认，不修改原型、添控件、复用启动按钮或让前端直控设备/数据库。

**任务勾选准则**：产物存在还需完成该条的行为和证据；文档同步、脚本可启动、Host返回202、局部单测、整盘提交均不能单独替代最终软件闭环。真正阻塞本功能主流程的问题须解决，不以非阻塞优化或历史任务清理扩大本阶段。

## 2026-09-23 媒体查询有限增量

- [x] T025 [US1/US2] 对应007 FR-003/008/010/011、SC-002/005：在Detection采集的既有已提交事件中记录冻结步骤`camera`，并由Host提供003共享合同的单一`GET /runs/{runId}/media`查询；只组合该run的已提交Media/Writes/StageEvents，公共ThreeD/F与Detection A/B身份可区分，旧事件缺身份保持Unknown，图片仍走原`MediaRead`路由。验证run隔离、Read/MediaRead授权、未就绪/未参与、同相机最新revision规则及实际媒体字节摘要；不改采集/算法时延与必检步骤。（依赖：003/006/007本次共享文档先同步；完成：定向测试与脱敏响应可复核，原型格位待006确认且007不实现前端；证据：`artifacts/station01-007/media-query-20260923/`。）

## 2026-09-24 启动失败诊断增量

- [X] T026 [US1/US3] 对应FR-019、SC-009、宪章P07–P09/P13：复用现有本地日志和007证据目录，为一次启动保存006请求/回执、Host/VirtualPlc/worker适用原始日志、API查询及SQLite/协议事实索引；记录时间基准、PID、组件/协议/配置版本、Test来源、路径和哈希，并建立`requestId→commandId/runId/任务标识→operationId/epoch`的可查询关联。收集索引可独立准备，最终可定位结论须消费001 T087、003 T063的必要关键节点；完成条件为进程退出后仍可按关联定位受理、就绪、首次交互、阻断和处置，保留原始异常或未知原因，心跳/轮询降噪不丢状态变化，凭据脱敏；不建集中日志平台，日志不代替数据库事实。2026-09-24已产生局部隔离样本及索引，完整失败链和006页面请求已由两份实际WPF/WebView2包、退出后索引及只读交叉核验完成，见 `diagnostic-validation.md`；历史证据不抵扣。
- [X] T027 [US2] 对应FR-019、SC-009、宪章P04/P07–P09/P12/P13：由006实际已有启动入口执行一次已受理且建立`runId`的首次PLC通信失败，并以可靠反馈明确不安全作对照；仅凭T026已保存日志与既有GET定位请求/任务、停止阶段、判定依据、原始异常或原因未知、未发送/反馈未知边界及实际处置，核对006已有位置的明确原因和可执行操作、无后继动作/假完成。依赖T026、006 T046及006 T044/007 T010的实际页面连通能力，不要求T015完整正常闭环先通过；若仅完成辅助API或Host/VirtualPlc定向验证，本任务及SC-009仍为Blocked/NotRun。证据分别标SoftwareLoopOnly与真实设备未验证，不改历史T015/T019–T025勾选或结论。2026-09-24实际页面A/B点击、截图、正式API/PLC/SQLite/原始日志及退出后只读交叉核验见 `diagnostic-validation.md`，仅Test/VirtualPlc范围完成。

## 2026-09-24 下料协议修复集成增量

- [X] T028 [US1] 对应新增下料验收约定、003 FR16–FR18、宪章P03/P04/P07–P09/P13：隔离Test配置提供独立Unload XYZ、复位/安全Z与容差，正式Host+VirtualPlc经Modbus完成前次Z复位、目标先写、命令4及本轮反馈，再核对SQLite下料完成、WholeTrayCompletion、解锁读回和最终完成条件；保留旧1、同目标未知及断联对照的原始证据与进程/配置/哈希索引。仅实际执行且证据完整后勾选；T026/T027诊断或旧T015不自动抵扣，WPF/WebView2原始入口和真机另判。2026-09-24本轮隔离正式Host/VirtualPlc Modbus两次正常闭环证据在`artifacts/station01-007/unload-fix-20260924-a/`及`unload-fix-20260924-b/`；B含最新构建、旧1→新0→本轮1、三目标先写、Z复位、GET最终态、SQLite/解锁事实、失败对照31/31 TRX及退出后哈希索引。仅SoftwareLoopOnly，未执行原WPF入口或真机。

- [ ] T029 [US1] 对应FR-021心跳调度回归：隔离实例记录本次版本/配置、PLC请求/PC应答最长空档及Host/VirtualPlc事务分段；经实际WPF/WebView2单次启动和3D/算法后继续核对下料、解锁、Test最终完成，按事先限定次数保留连续复测的全部成功/失败包。与修复前定向失败、修复后对照和3秒真实中断仍锁动作的证据一起核对；没有页面截图/正式请求链或原失败窗口分段证据时只报partial，不外推真机或改写T015/T088历史结论。

T024原有FR-001–018/SC-001–008汇总范围保留历史记录；007当前完成判定还须单独纳入FR-019/SC-009与T026/T027实际结果。未完成或缺少实际前端失败样本时，不得仅凭T024旧字段生成整体通过结论。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

> T030 已由下方当前增量替换；原编号、未完成状态及全文见 tasks-history-before-s0-s5-20260924.md，不作为当前实现任务。


## S0—S5任务增量（2026-09-24，宪章5.0.0）

所属功能：`specs/007-station01-integrated-loop`。跨功能依赖写作目录简称+任务ID，完整目录见008 tasks映射表。原则P03/P04/P05/P07/P08/P09/P11/P13，前端另P12及用户最小原型授权；新增任务全部未完成。旧T030被以下任务替换，未受影响的历史待办不取消；公共验证只做必要正常/失败，不构成交叉穷举。

- [X] T031 [US1] S0参数化既有`scripts/start-station01-virtual-loop.ps1`、`scripts/simulate-station01-load.ps1`及008 `fixtures/`的FixtureManifest入口，传入Test目录/场景/合法槽位/公共配置/预算/媒体/worker摘要和独立端口/数据库根；PrepareOnly不启动业务，为008关闭外部自动取盘。依赖：008现行quickstart/recipe-cases；Q01实际装载依008 T050，接口无需等现场输入。交付：受控根/用途检查、组件版本清单和可供正式页面使用的准备结果；最小脚本参数检查证据`specs/007-station01-integrated-loop/evidence/008-fixture-tooling.md`，不把准备成功算Q通过。对应FR-022和008 FR-015/018。

- [X] T032 [US1] S1同步独立worker协议两端：`backend/src/Gaode.Infrastructure/Algorithms/WorkerMessages.cs`、`WorkerProtocolCodec.cs`、`PythonWorkerAdapter.cs`、`WorkerProcessSupervisor.cs`与`scripts/virtual-station01-algorithm.py`实现station01-worker/2.0显式单/双输入及各自租约释放，实际读取字节/校验摘要并计算，受控输入产生结果而非Host写死成功。依赖：008 execution E03及data-model，接口已定义；与008 T053按共同合同接线。交付：单图与融合调用及缺输入/迟到错关联必要验证，保留每次10秒模拟来源/实际耗时，证据`specs/007-station01-integrated-loop/evidence/008-worker.md`。对应FR-022、008 FR-003/013/F3。

- [ ] T033 [US1] S1准备并复用正式WPF/WebView2页面操作/只读采证工具，维护`scripts/capture-station01-webview2-normal.cjs`现有007页面驱动与`specs/008-recipe-driven-inspection/evidence/index.md`需要的包格式，接FixtureManifest/Q标识；一次页面选用/启动和适用人工确认，保存请求/通知帧/截图/组件版本/PLC审计/worker/SQLite/媒体/Final引用。依赖：T031、specs/006-frontend-station01-console T048/T049；不能调用页面内部启动函数、后台StartRun或自动取盘替代点击。交付：工具与证据格式实际可用，失败包不丢弃、202暂时404继续查询但不判Final；证据`specs/007-station01-integrated-loop/evidence/008-page-tooling.md`。对应FR-022/019及008 FR-015/016，Q01通过判定唯一归008 T055，余Q复用同工具。 USR-E增量：实际消费者包括`scripts/verify-q01-q02-test-page.ps1`、`scripts/wait-008-page-batch.ps1`及启动脚本，先记录Host/VirtualPlc DLL、桌面EXE、frontend/dist/runtime.js的实际路径/PID/命令行/端口/摘要、Provider/字序/协议/配置/fixture及src→build→dist，核对旧运行包后定位问题1—5。依008 T050改造后的当前目录及003 T062审计子交付；manifest/工具准备不等006整项，页面采证才依T048/T049所用能力。采完整批量包、同值写、接收、实际XYZ/Host判断及清零，缓存缺口明确，新包持久保存且历史不覆盖。生成器唯一008 T050、不重做已勾T031；设备/worker/页面串行、不后台代操作，最终判定归008路线任务。

当前首批沿用T029验证责任：消费003 T065交付，使用`scripts/capture-station01-webview2-normal.cjs`及既有诊断采证脚本，证据按原心跳缺陷test.md与新隔离运行包保留。Q01的实际心跳负载证据随008 T055产生；T029原任务缺失现场窗口仍标partial，不能为凑Q01反向补造。

> 旧协议历史检查点（不代表新版状态）：008自动多面T033子范围：现有页面采证器保留，Q03仍Restricted时只记录不可启动原因；待可用后复用同工具页面选用/启动/取盘，采集各面媒体与真实测量来源与提交事实。原整项条件/勾选不变。


## 2026-09-26新版协议增量子范围（未实施）

既有编号和勾选只证明原范围，本表所有新版子范围均NotRun；实现前置按所需子能力交付，整项验收仍保留原未齐项。输入为唯一分区协议及008 execution/3.0；日志须可按run/step/operation/实体/面/连接代次追踪意图、派发、反馈、ACK清零、保存及失败。

| 原任务/新版子范围 | 具体消费者（文件简称按原任务路径） | 输入、前置、完成条件及最少验证 |
| --- | --- | --- |
| specs/007-station01-integrated-loop T031 / 20260925协议 | `scripts/start-station01-virtual-loop.ps1`及008四组fixture/media/worker/cases清单 | 已勾旧任务新增可追踪新版子范围NotRun：协议身份/原件SHA、目标0.5/schema与初始测量引用、翻面XYZ及新预算同步；先由008 T050准备合法数据。PrepareOnly核验不用旧生成器覆盖新版；不得将准备成功记Q通过。 |
| specs/007-station01-integrated-loop T033 / 20260925协议 | `capture-station01-webview2-normal.cjs`、`verify-q01-q02-test-page.ps1`、`summarize-q01-q02-evidence.py` | 工具实现只依新字段/fixture合同；实际页面验收依006所需绑定可用，无需其全项勾完。记录Q03一次公共3D、Flip ACK、两面检测、下料/分拣适用事实、解锁/点击/Final及协议SHA；保留失败包，不代点后台接口。 |
2026-09-26：既有T032/T033直接依赖补充身份选择的受控Test worker清单与GROUP混合面质量验证；编号及历史勾选不变，原完成条件不变。[合同](contracts/virtual-integration.md)。


## USR-E当前依赖与完成口径（2026-09-26）

依据宪章7.0.0，完整归属/验收见[本轮任务交接](../008-recipe-driven-inspection/tasks-six-issues-alignment-20260926.md)。两端协议/诊断子能力＋当前配方准入/目录→必要代表性协议及正式路线验证→USR-D完整新轮。003 T072-A＋001 T078→001 T052→008 T068（复用008 T054及003 T069）→003 T072-B→006 T051→008 T069→008 T070；A/B/M分子交付，003 T069不反向等008 T068，不等全部Q/C/F或特殊生产。共享源码按文件串行交接，设备/worker/页面串行采证；子交付不勾父任务。问题1—5根因待实际包核验，状态2/3实时Z不强制等于取放目标Z；生产采样窗口只局部限制。历史勾选/正文不改，旧Q/旧恢复Passed不抵新验收。

## monitor-xyz-history 本轮同步
按[003当前监控纠正](../003-plc-latest-protocol/spec.md)及其[诊断合同](../003-plc-latest-protocol/contracts/virtual-plc-boundary.md)：公开XY名称、原列表同值XYZ、删除独立栏目；复用audit/changes，保持业务/地址/期限。此前XYZ命名条款仅限旧构建。 本轮工作归003既有T062的监控子范围；验证通过仅记录本轮证据，不自动关闭该任务其他范围或T065/T055/T070。

## 009 / AL05 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

本次增量唯一代码/验证归属009 T015/T016/T031/T050/T053/T054；相关任务直接或传递依赖本次实际对齐和009 T012。保留本功能全部原任务ID和勾选，不以父任务历史完成抵扣。

沿现有verify.ps1→workflow/verify_entry.py→runner.py，不调用auto-dev。增加通信测试套件、固定必需清单与逐case/dataRow执行账本；C#/JS/Python/有限PowerShell正式扫描与同入口正负例均必需。缺清单、未发现、过滤、Skip、解析失败、旧报告或证据缺失非Passed；参数化方法名不能抵数据行。PD12、SU6、BA全部数据行固定登记，不从当次发现生成expected。

三入口独立冻结Test10000ms，Production无回退；实际源码参数与拟增009入口分开。

完成证据按009相应任务、固定案例和消费者交付；未运行部分不得报告通过。

2026-10-02 AL05存储位置子范围（原任务ID及勾选不变）：009 T053承接受控临时Test父目录的可选进程级开关、拒绝相对/越界/链接/已存在运行根、登记绝对位置及默认行为回归。先实际对齐本功能spec/plan/CLI合同，再修改runner与其测试。实施和复核执行者均为Codex，检查依据为CLI合同与真实测试根规则；未代任何人员签批，运行证据待对应组件执行产生。


### 009 / AL05 独立进程Test根补充（2026-10-02）

009 T041/T054的实际进程证据需要容量足够的独立Test根。`start-station01-virtual-loop.ps1`在显式提供已验证Test FixtureManifest时，同样接受进程级`GAODE_VERIFY_TEST_PARENT`：必须为当前用户临时目录内绝对非链接父目录，TestRoot必须是其下全新且不存在的目录，拒绝相对、越界、任一链接祖先、既有目录及非fixture调用。父目录不是数据库；StorePrep仍真实建库、StoreAccessGuard仍互斥并核路径，Host仍受现有Test准入。未提供开关的现有仓内路径行为不变。

启动记录必须写实际TestRoot和允许父目录，009汇总引用实际SQLite/媒体/持久日志及PID，不以目录存在计通过；不变更预算、设备语义或页面。对应实现/组件边界验证及独立进程验证归009 T041/T054/T055，先完成本段接口对齐再修改脚本。本段由Codex执行和复核，不表示其他人员签批；不修改007历史任务勾选。


### 009 T040/T041 有限独立进程保存故障接线（2026-10-02）

按009 VG V07/V09，在真实Host独立进程中增加可选`Gaode:TestPersistenceFaultCase`，仅在`VirtualPlcIntegration`、Virtual PLC及全部Test配置下接受。固定值为F05-A/B/C、F06-A/B/C、BA04-late-bound/late-handoff；未知值或其他运行环境启动拒绝，不新增业务API。未配置时不安装任何拦截器。Test根中的`009-fault-arm.json`以caseId、runId和nonce选择本次真实运行；独立编排取得正式启动runId后写入，未命中不能当故障验证通过。唯一命中记录实际EventId/WriteId/EvidenceId、时刻、位置和commit事实到同根`009-fault-events.jsonl`。

A通过实际SQLite触发器拒绝相应插入，原Store负责事务回滚与结果；B仅在真实commit完成后扣住回执；C在同一实际边界另持SQLite独占锁暂阻核查。通信raw故障仍经TraceWriter实际job，取料业务事务仍由StageEventStore承担。BA04严格选择本run的RecipePlanBound或本次handoff，不拦其他保存。Test专用保持窗口最多30秒，或收到匹配nonce的`009-fault-release.json`结束；这个注入外限不是业务期限，保持期间原预算/保存期限照常失效，释放后不能复活动作。

C使用受控Test副本的DELETE日志模式取得真实排他锁；A/B不伪造数据库结果，记录存储不可用时仍仅尽力写诊断文件。009验收必须另查实际SQLite、当前回执、原截止、PLC全部后继写和保守占用；故障日志及进程存活不能独自证明验收。此处只完成必要接线接口对齐；代码及实际运行由009任务证据确认。实施/复核角色为Codex，不冒称客户批准，其他功能历史任务状态不变。


009本地verify的组件主流程证据随同一受控Test根保存：runner为`GAODE_009_INTEGRATION_ROOT`设置本轮TestRoot下的`component-routes`，VirtualLoopTestRig仅接受该批准Test父根内绝对、非链接路径；不改变默认未设置时的旧路径。实际目录写入verification.json；快照、媒体和origin仍真实逐run保存，不以外移或目录存在代替执行/核证。大文件不重复复制到容量不足的仓盘，小报告和引用仍保留仓内。原子新run、防覆盖、摘要及同轮关联义务不变。此为009 T049/T053的Test证据位置接线，不是独立Host验收或产品数据格式变化。


### 009 T053 本地完整套件看门狗（2026-10-02）

实际integration238完整运行188项用了87分钟，原runner对每条命令1200秒的外部看门狗不足以执行既定全部路线。仅将Gaode.Integration.Tests整套命令外限设为10800秒（3小时），其他命令仍1200秒；独立于Test10000ms及所有业务期限，不能由失败用例修改预算。每测试15分钟的VSTest挂起看门狗大于现有最长多子场景方法的有限窗口，超限终止本轮测试进程并使报告非通过，不跳过或重试失败。使用本机dotnet test --help已核实的blame-hang-timeout、dump-type=none，保留实际TRX/序列/逐项日志；不生成大转储占用证据盘。外部超时仍返回非0，必需账本仍逐数据行核证。由Codex实施/复核，运行结果另留证；本段不改变007历史勾选，也不批准生产或整体通过。


### 009 T054 独立进程编排的当前接口（2026-10-02）

由Codex执行/复核，先对齐本段再实现。新增 `verify-009-protocol-isolation.ps1` 接受 Mode=Baseline/Gates/Mutation、Variant=M01—M05（仅Mutation）、FixtureManifest、FreezeManifest、HostDll、PlcDll、EvidenceRoot，以及有限 Cases 选择和 WindowsNativeThreadPool 开关。Cases只能选择009固定清单，不从发现结果生成期望；选择子集不代表完整基线。Gates调用已有verify-only入口，不调用auto-dev或改变阶段。独立路线复用现有启动/准备/API，实际Test根由既有GAODE_VERIFY_TEST_PARENT规则控制；报告留在仓内新证据目录。006页面仍由既有WPF入口单独验收，后端辅助启动不生成假页面结果。

新增有限run-009-process-case.ps1只采集既定场景及本次拥有的进程事实，业务断言和通信探针分别执行；控制文件完整关闭后原子发布。进程结束只处理本次记录且PID/实际可执行文件/创建时间仍一致者。所有派生报告逐run/source/build/manifest绑定，缺必需数据行、解析失败、故障未命中或任一子程序非零均非通过。固定位于scripts/architecture/009-process-cases.json的L01—L10及F/BA数据行列出实际fixture，不创建通用故障平台。

009选定代表fixture仅将原预算/simulation引用定向更新为已批准2.0.0，保留原配方catalog/媒体/Worker及输入摘要；原文件先保留只读证据。此为T031/T054配置闭合，不改历史结果或任务状态；Production仍未批准、Test10000ms不变。新入口的组件检查及独立运行以009实际证据为准，不将本段当通过。


009 T040/T054的BA03独立接线补充：在既有受控Test故障选项中新增唯一`BA03-healthy-wait`。只命中已arm的当前run的真实RecipePlanAndBindingIntent提交回执边界，在t0尚未登记时调用独立VirtualPlc已有RecipeApplicationPreconditionHold测试接口；必须取得实际accepted响应，否则本案例失败。该接口只阻止现有绑定前置，TCP/心跳不停止，不跳过F清理。原CriticalSave仍约束意图回执，没有给业务增加时间。主流程起算和全部保存仍由业务协调器承担。Test接口调用及意图真实commit/安装时刻保留故障日志；BA03的成功判据另由实际10秒窗口、无后继、数据库与通信观察证明。此接线不允许任意URL或生产故障注入；仅已批准VirtualPlcIntegration/Test配置。由Codex实施/复核，不声明运行通过。

009 T038/T040/T054 的 BA05 inflight 前置对齐（2026-10-02）：在既有 TestPersistenceFaultCase 固定选择中增加 BA05-inflight，只允许原 VirtualPlcIntegration/Virtual/Test 组合。正式通信适配器在本次 B 容量首写取得真实响应后、任何后继绑定写入前进入有限 Test 派发栅栏；不拦 A 阶段，不暂停心跳和观察，不延长单次 I/O 或业务总窗。栅栏只命中已 arm 的当前 RunId/nonce，记录 BindingId/ActionId、实际已完成首写及单调时刻；随后由原请求取消/截止关闭，不能由 release 文件恢复旧请求。编排取得该实际命中后调用原 Cancel API，核关闭后的后继派发为零和原写入事实保留。该测试钩子不进入业务端口，不把协议阶段交给 Application。实现/复核为 Codex，先完成本段文档对齐再修改共享接线；实际组件和独立运行证据仍由009任务补齐，不改007历史勾选、不声明已通过。

### 010实施定向对齐 A05（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A05**：采集适配器产当前Request/Capture/epoch、RequestedSettingsDigest、CameraOrigin/LightOrigin/MediaSource、ApplicationState、可选ActualSettings与重放事实。Unknown不从fixture补，固定图只ConfiguredOnly不声称SDK应用。共同层核关联、Ended+media/首owned buffer，必要事实真实保存；请求/实际设置分别投影，缺实际保持未提供。既有API/source枚举和006页面保持。
  生产/消费与010实施承接：capture/algorithm/协调→TraceWriter/共同检测→Handoff/RunMediaCatalog/CommittedResultProjection/006；T008/T014/T015/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A03（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A03**：DetectionRequest使用typed FrozenExecutionInputs/目标、当前回执、用途与批准；删除StrictRecipeExecution特权/frozen-plan-0/占位零坐标/nonStrictPending。context/1.0合法但同样完整校验。共同RecipeDetectionExecutor承接有效检测，ThreeStage消费typed分拣目标；来源不选择工序。
  生产/消费与010实施承接：Handoff/目标resolver→共同检测/ThreeStage→整盘/结果/上层stub；T008/T012/T016—T019/T027/T028。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A08（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A08**：正式IDetectionPort固定RecipeDetectionExecutor，externalVirtualPlc不控制后段，图片/Worker不选择整段业务；删除SimulatedDetectionPort/Profile、NotIntegratedDetectionPort、DetectionTestMode，同文件其他合法端口保留。环境只绑叶设备/相机/算法/坐标/解析/准入，缺能力明确拒绝；完整链正式HTTP/独立PLC和Worker/真实SQLite到授权Final。整段替身只UpperIsolation。
  生产/消费与010实施承接：组合根→Host→verify-latest-plc、rig/单配方；T013/T021/T022/T024/T032。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。
