# 任务：通信代码边界最小收敛

## 2026-10-02 当前正式范围：通信代码边界最小收敛

本轮目标是在约4小时内使协议知识退出业务代码、业务端口、业务合同和相关业务测试，并形成可执行的边界防回归。不是临时少跑测试；这是用户批准的当前正式完成定义。保留正式通信、命名信号/编解码、语义迁移及有效业务保护义务。

当前活动31 FR（FR-001—029、033/034）、16 AC（AC-006—021）、6 SC（SC-001/002/005/007/008/010）。原FR-030—032、AC-001—005、SC-003/004/006/009的动态演练或完整证据验收进一步转出；原FR-035—039、AC-022—028、SC-011/012继续转出。已接受Test10000ms、三入口、期限/取消、安全、动作关联、未知占用及真实保存门禁不失效。

任务ID T001—T069及历史勾选保留。当前49项含活动义务，20项整体转出；全部原义务和本次拆分见scope-adjustment。原勾选不能证明本轮通过；拆分项的完成只指本轮义务。

完成条件：受影响工程及必要依赖构建成功；正式入口和全部直接消费者语义/接线闭合；当前源码/合同/相关断言的C#、JS/CJS、Python、有限PowerShell内容检查与同入口正负例通过；事先固定的检查和直接语义测试实际发现、执行且有当前源码证据。缺失、未执行、零发现、过滤、Skip、解析失败或旧报告均不得Passed。合法通信诊断raw可保留，不得回流业务控制。

36条完整动态场景、整套PD运行、M01—M05/MC动态演练、完整历史/升级/预算/保存故障、全配方/页面/相机/算法/媒体/整机及性能工作不作本轮完成前置；保留编号、实现和旧证据。直接改动若影响既有保存/期限/安全规则，须增加对应最小真实验证，不能借转出绕过；保存门禁验证使用真实数据库。

本轮直接实施和验证；在新结果齐备前状态为未通过。只可声明本范围通过，不声明完整通信动态验收、全系统、生产或旧库通过。

## 拆解规则与权限

- 任务路径相对 `E:/dzk/gaode-1`；原“拟增”保留原任务来源，是否已经存在以当前源码为准，不据此误报缺文件；新专项模式/固定Case接线已实现，是否验收以当前本轮证据为准。证据根E统一为 `artifacts/recipe-execution-008/009-isolation/{runId}/`，由未来运行生成，不复用历史报告，不覆盖旧证据。
- BD/PC/EC/VG分别指上述业务、通信、诊断历史、验证合同；I/AL取影响矩阵，模型章节取data-model。A/N/P/G/F/BA/M/MC/L均沿设计原编号。**任务T001等三位编号**与**迁移矩阵T01—T53两位编号**不同，后者均注明“迁移矩阵”。
- 每项任务下的“依据/原则、前置、交付、验收”构成完整任务定义；前置必须实际完成才能动对应产物。跨功能文档必须先实际对齐spec/contracts/plan/tasks，不能把009已有AL表或本任务登记当已完成。用户本轮已授权必要定向文档/代码/最小验证，仍不得越界。
- 测试是本功能核心交付，不能按模板“可选”省略。所有负例与正式扫描共用检查器；当前源码摘要、内容拒绝检查、执行账本分别证明证据身份/边界/执行完整性；动态冻结任务转出，不以hash替代内容。
- 仅T002/T003及T014/T015标[P]，分别表示共同前置已完成后不同文件可并行；不授权自动启动代理。同一接口生产者/消费者、同一文件、真实进程/端口/DB及账本更新按依赖串行，不虚构故事独立性。
- 完整新增期限业务验收已转出；现用已批准业务约束不变：独立版本化运行冻结预算，Test10000ms，严格/旧/独立API三入口；意图有效保存后、排队/端口前唯一t0，设备及本次必要保存共窗；严格保留原三段起点/值、旧链保留handoff后Detection首起点、API尊重实际已有截止。每保存受CriticalSave及剩余窗，取消/超期关闭后台新派发及成功资格。Production未批准拒绝且不回退；不得在任务中重选策略或延长预算凑通过。
- 本功能不引入通用协议引擎/兼容层/热切换/新页面/大规模异常矩阵。006完整页面/通知数据交付及发布验收归另功能，仓内直接消费者类型与边界仍闭合，原型只读；虚拟Test不批准物理信号、轴、生产预算或现场验收。

## 当前阶段范围与完成证据（P13）

69个ID和历史勾选保留。当前49项活动、20项转出；活动包含拆分的有限义务，原完整任务未验仍在scope-adjustment，不自动成为完成。
正式通信/直接语义消费者与保存接线 → 固定有限清单与最小入口 → 构建/内容门禁/直接语义/人工核查 → T061/T069。完整动态/冻结/变体/历史/整机前置解除，真实代码依赖保留。

## Phase 1：必要准备（T001—T003）

沿用现有工程，不初始化项目、不创建分支。准备的是当前功能的来源、边界与可执行验收依据。此处尚不冻结修复前状态。

- [X] T001 核对正式入口、来源和局部OPEN，登记 `artifacts/recipe-execution-008/009-isolation/{runId}/preparation.json`（运行时拟增）

  **依据**：FR-001/014/033/034；AC-006/011/014；SC-010；PC P01/P06，BD B07；宪章P01/P10/P13。**宪章原则**：P01/P05/P10/P13。  
  **前置**：无。
  **交付**：只读核对当前spec、plan和review-remediation§7、Host装配及当前协议原件；记录来源摘要、工程地址约定、正式生产/消费链、工具链、允许Test根和本轮变更分类。历史“未同步”不得覆盖最新结论；Production预算/物理OPEN按范围登记。现有实现/旧证据逐项复用，新专项运行不把文档状态当通过。  
  **验收与证据**：经架构/业务/测试角色复核的准备记录、受影响路径及局部阻塞表；任何新事实冲突有具体条款，不能由实现人员猜测。

- [X] T002 [P] 建立受保护文件、公共字段和旧断言登记，拟增 `backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json`、`009-public-shapes.json` 与 `009-test-obligations.json`

  **依据**：FR-002/003/004/023/024/026/027/029；AC-015—021；SC-001/007/008；VG V01—V04，I§5，迁移矩阵§1—4。**宪章原则**：P01/P05/P10/P13。  
  **前置**：T001。
  **交付**：按用途分类C#、规范业务合同接口段、全部第一方JS/CJS/MJS/TS/Python/有限PS及递归本地helper；列明每个公开字段用途/来源、生产者/消费者。逐一展开迁移矩阵T01—T53的方法、Theory数据行和脚本断言，标保留/迁移/改写/失效及未来新case ID；三处历史Skip的替代义务单列。登记是初次改造清单，尚不是冻结或通过。  
  **验收与证据**：双向文件枚举无未分类；可逐旧断言追溯原依据；T51—T53新期限义务与已有义务分开。新ID先登记期望，未实现/未发现均不得算Passed。

- [X] T003 [P] 编写独立协议预期，拟增 `backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json`

  **依据**：FR-001/006/025/031/034；AC-001—006/012/016；SC-006/010；PC P01—P06，VG V06。**宪章原则**：P01/P05/P10/P13。  
  **前置**：T001。
  **交付**：从已确认原件和明确Test工程约定逐项人工转录在用点位/方向/宽度/类型/编码/位/责任/字序及独立Float32已知向量，记原件摘要/页节及复核角色；不导入或从生产映射生成expected。预留M01—M05独立变体预期与MC01不随双端错误改变的校验规则。  
  **验收与证据**：协议/测试角色交叉核对每项在用信号；当前缺物理事实保留局部OPEN，不能用VirtualPlc实现自证。oracle缺项导致后续V-WIRE失败。

## Phase 2：共享前置（T004—T020）

先完成AL01—AL08实际跨功能文档对齐，再改变对应共享接口。T004先交付预算生产契约，T006—T010承接各消费方，T012复核闭合。以下跨功能对齐已有实际修改成果，须核当前文档而非只看历史approval文件；本轮无共享产品签名变化，不重复授权或改写其他功能勾选。完整功能的后续验收仍转出。

T013—T016尽早建立拒绝错误的门禁。当前正式源码/脚本有已知泄漏及未实现案例，必须记录预期失败；检查器自身正负例应正确运行，不能用Skip或临时无限白名单让整体验证转绿。T017—T020只建多故事共用的语义、协议和存储基础。接口迁移期间消费者尚未闭合时，不报告可发布或独立故事通过。

- [X] T004 先完成AL08预算生产契约：对齐 `specs/001-station01-public-preparation/contracts/configuration-time.md`、`budget.schema.json` 及该功能 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-014/035—039；AC-005/006/022—028；SC-004/010—012；BD B03.2，模型§3.1，AL08。**宪章原则**：P01/P04/P08/P10/P13。  
  **前置**：T001。
  **交付**：预算已对齐文档成果复用；本轮不再发布新预算完整功能，任何直接语义签名变化仍先对齐001时间合同。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T005 完成AL01公共准备对齐：修订 `specs/001-station01-public-preparation/contracts/device.md`、`persistence-handoff.md`、`api.md` 及同功能 `spec.md`、`plan.md`、`tasks.md`

  **依据**：FR-002/007—010/016—019/021；AC-007/008/010—013；SC-001/002/004/009；BD B02—B04，I§5，AL01。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T004。
  **交付**：公共准备业务和持久化/API负责人实施；架构、公共业务、测试复核。明确Accepted→夹紧→A运行容量准备→保存→3D/F的原时点与分期限，A不等于F唯一绑定；保留3D必要失败清理与F保存门禁，删除旧startup raw六字段要求并对齐语义/缺失表示。  
  **验收与证据**：实际spec/contracts/plan/tasks差异及旧义务承接；阻断DeviceMessages、StartupDiagnostic、StartClamp/ThreeD/F和公共保存/API共享修改。

- [X] T006 完成AL02配方输入与绑定对齐：修订 `specs/002-plc-xyz-recipes/contracts/recipe-execution.md` 及同功能 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002/009/010/014/016/035—039；AC-006—008/022—028；SC-001/004/010—012；BD B03.1/B03.2，PC P04/P04.1，AL02/AL08。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T004、T005。
  **交付**：保留区域A/B输入及绑定语义接口对齐；三入口完整新增预算验收转出，但不得改变已确认输入、起点或取消。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T007 完成AL03设备与保存对齐：修订 `specs/003-plc-latest-protocol/contracts/plc-stage-action-port.md`、`stage-events.md`、`detection-port.md`、`whole-tray-workflow.md`、`public-preparation-handoff.md`、`recovery-test-execution.md`、`component-source-matrix.md` 及003 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/009—011/016—022/035—038；AC-007—014/022—027；SC-001/004/005/009/012；BD B01—B06，EC E02/E02.1/E02.2，AL03/AL08。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T005、T006。
  **交付**：保留003设备/动作/保存语义对齐；不得把整体整机验收排期带回009。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T008 完成AL06执行与证据对齐：修订 `specs/008-recipe-driven-inspection/contracts/execution.md`、`evidence.md`、`api-results.md`、`test-virtual-mapping.md` 及008 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-009/011/016—019/021/022/035—039；AC-008—014/022—028；SC-004/005/009/011/012；BD B03.2/B05，EC E02—E05，AL06/AL08。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T006、T007。
  **交付**：保留008取料保存、动作关联及直接消费者接口对齐；全配方/算法/媒体回归转出。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T009 完成AL04公开API与消费者对齐：修订003 `contracts/station01-main-flow-api.md`、`status-notifications.md`、`virtual-plc-boundary.md` 和006 `contracts/api.md`、`host.md`、`prototype-mapping.md` 及两功能 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002/004/015/020—024/031/036/038；AC-009/012—017/023—026；SC-001/006/009/012；EC E03/E04，I§5/§5.5，AL04/AL08。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T007、T008。
  **交付**：保留公开语义类型和所有仓内直接消费者签名对齐/构建；006页面运行及发布验收转出。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T010 完成AL05集成工具对齐：修订 `specs/007-station01-integrated-loop/contracts/virtual-integration.md`、`commissioning-cli.md` 及007 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-015/023/026—029/035—039；AC-007—011/015—028；SC-007/008/011/012；VG V02.1/V04/V07/V09，AL05/AL08。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T004、T007、T009。
  **交付**：保留正式通信启动/探针和架构内容分类对齐；整机联调环境验收转出。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T011 完成AL07存储升级对齐：修订001 `contracts/persistence-handoff.md`、003 `contracts/stage-events.md`/`station01-main-flow-api.md`、008 `contracts/evidence.md` 及各功能 `spec.md`、`plan.md`、`tasks.md`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-019/020/022/034/036/038；AC-009/012/014/023/025/026；SC-005/009/010/012；EC E02—E05，模型§6.1，AL07。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T007、T008、T009。
  **交付**：保留已对齐的必要证据schema/兼容准入合同；旧库升级及SU验收转出，U1仍是提交未知，不能自动重跑DDL。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T012 核对AL01—AL08实际对齐闭合，形成 `artifacts/recipe-execution-008/009-isolation/{runId}/alignment-approval.json`（运行时拟增）

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-014/023/033—039；AC-006/015—018/022—028；SC-007/010—012；BD B07，VG V01/V09，AL01—AL08；宪章P13。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T004、T005、T006、T007、T008、T009、T010、T011。
  **交付**：核对实际AL文档及已交付证据；活动共享接口对应的对齐必须真实完成，不把alignment-approval文件当批准本身。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T013 接入通信测试项目及固定分析依赖：更新 `backend/Gaode.slnx`、`backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj`，拟增 `backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj`

  **依据**：FR-023—029/031；AC-015—021；SC-006—008；VG V01—V04，PC P01；AL05。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T002、T003、T010、T012。
  **交付**：新增项目只承担通信/有限历史读取测试，明确业务断言文件不借混合项目引用逃逸；Rules显式引用固定SDK10.0.401内Roslyn5.9.0.0并复制运行依赖，缺失拒绝。复用仓内锁定依赖，不升级工具链/引入生产分析平台。  
  **验收与证据**：测试发现可列新套件、固定解析器可加载；此项仅基础设施就绪，不可报告009通过。后续runner接线T016，纯协议生产项目引用T018。

- [X] T014 [P] 先实现C#及业务契约边界检查和同入口样例，更新 `backend/tests/Gaode.Rules.Tests/Architecture/DependencyRulesTests.cs`，拟增同目录 `ProtocolBoundaryChecker.cs`、`ProtocolBoundaryTests.cs`

  **依据**：FR-002—004/012/021/024/027/028；AC-013/017/019/021；SC-001/002/008；VG V02/V03 A01—A07、N01—N10、P01—P06。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T002、T007、T009、T012、T013。
  **交付**：同一Roslyn/符号检查覆盖依赖、递归公共形状、raw来源传播/比较/强转/位/布局/内部阶段、诊断绕行、正式通信旁路和规范合同/测试错误要求。逐样例校验规则与位置，合法业务数字/图像载荷/opaque引用和底层PDU通过；不全文正则禁数字。  
  **验收与证据**：先对当前泄漏复现失败并保存命中清单；负例按规则拒绝、正例确被加载。此时正式源码预期失败，不能Skip/扩大白名单转绿；最终清零由T052/T056验证。

- [X] T015 [P] 先实现非C#内容门禁，拟增 `scripts/check-009-script-boundary.py`、`scripts/architecture/check-009-js.cjs`、`check-009-powershell.ps1` 和 `009-script-boundary-cases.json`

  **依据**：FR-003/004/021/024/027—029；AC-013/017/019—021；SC-001/008；VG V02.1—V04 A08—A10、N11—N17、P07—P10；AL05。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T002、T009、T010、T012、T013。
  **交付**：统一Python入口调度锁定TypeScript5.9.2 AST、Python ast和PowerShell自带AST；按已设计有限闭包追踪局部别名/解构/helper/prop/推导与PS管道，检查断言/控制/退出和输入schema。全scripts/及frontend/tests/本地helper双向分类，新增未分类/解析失败/动态未知逃逸失败。当前CJS alarmBits===2和Python取放2/3/0逐行、PS原始写判断由正式同入口检出，合法语义及通信oracle实际加载。  
  **验收与证据**：输出VG固定JSON含runId/版本/摘要/files/cases/violations位置/errors；当前混合脚本先预期拒绝，无人工/关键词/哈希替代内容扫描。与T014不同文件，可在前置齐备后并行。

- [X] T016 尽早接入固定必需执行账本：更新 `scripts/workflow/runner.py`、`test_runner.py`、`test_verify.py`、`verify_entry.py` 和 `scripts/verify.ps1`，拟增 `scripts/workflow/009-required-cases.json`

  **依据**：FR-023/026—029/035—039；AC-018—020/022—028；SC-007/008/011/012；VG V04/V09 G01—G07；AL05。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T002、T010、T012、T013、T014、T015。
  **交付**：独立于当次发现结果登记固定.NET/脚本/BA case及数据行；保留零测试/Skip/notExecuted拒绝，添加通信套件、JS/PY/PS/INVENTORY四扫描组及自检必需执行。核对发现→执行→TRX/JSON→当前构建/sourceDigest/runId，缺项/过滤/旧报告非0；BA未验行保持Pending并使原完整动态验收失败，不进入当前BoundaryMinimum清单。 另预登记VG V01.1的PD固定12数据行及V07的SU固定6数据行（ID见合同）；原PD/SU实施/执行承接仍保持，未验仍Pending，不由发现结果删减；整套运行不作当前前置。  
  **验收与证据**：G01—G07通过同runner校验函数覆盖.NET和脚本报告fixture、自检确实被verify调用；早期报告如实列现源码/未实现失败，禁止临时豁免签发通过。最终案例完善T053。

- [X] T017 建立稳定业务端口和证据模型：更新 `backend/src/Gaode.Application/Ports/DeviceMessages.cs`、`IPlcStatePort.cs`、`IPlcActionPort.cs`、`IMotionPort.cs`、`IPlcResetPort.cs`、`IPlcRecipePort.cs`、`StagePortContracts.cs`、`PersistenceMessages.cs` 与 `backend/src/Gaode.Domain/Station01/RunSnapshot.cs`、`SortingMappingContracts.cs`

  **依据**：FR-002—004/016—022/035—038；AC-007—014/017/022—027；SC-001/004/005/009/012；BD B01—B06，模型§1—5，I01—I09/I25/I26；AL01—AL04/AL06/AL08。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T004、T005、T006、T007、T008、T009、T012、T016。
  **交付**：替换IInspectionHandshakePort为拟增同目录IAcquisitionCyclePort.cs；限定观察质量/来源/身份/实际位置与面、关联结果/opaque引用、PickEvidence/CommitReceipt/独立失败通知及RecipeApplication语义期限/回执。取消Application地址/原始编码/ACK阶段，不靠同值枚举包装；PhysicalSlotIndex等合法业务数保留。以模型清单约束各公开字段用途，区分DeviceApplied与最终授权及物理事实/提交状态/回执有效性。  
  **验收与证据**：公共形状及语义合同样例由T014验证；建立生产者/消费者待迁移清单。接口切换中未迁移编译失败可记录，但不算可发布或完整故事验收；T039/T048—T053前必须闭合。

- [X] T018 提取纯协议定义、编解码和命名信号访问：拟增 `backend/src/Gaode.Plc.Protocol/Gaode.Plc.Protocol.csproj`、`Signals.cs`、`Float32Codec.cs` 与 `backend/src/Gaode.Infrastructure/Devices/Plc/PlcSignalAccessor.cs`；在 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs` 接入正式准备与动作准入

  **范围状态**：拆分；原通信动态/完整PD/保存/握手验收义务按原ID保留在scope-adjustment，当前只验代码边界及直接语义。
  **依据**：FR-001/006—012/025/031；AC-001—005/007—012/016/019；SC-002/006；PC P01—P06，VG A01/A06；AL03/AL05。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。 直接追溯spec“必要边界与失败场景”的坏映射动作前拒绝及PC P03；补VG V01.1 PD，不新增需求编号。  
  **前置**：T003、T007、T010、T012、T013、T016。
  **交付**：更新backend/Gaode.slnx、Infrastructure.csproj、VirtualPlc/VirtualPlc.csproj及通信测试引用；从ProtocolLatestMap.cs/VirtualPlc/PlcAddressMap.cs/PlcDataStore.cs提取单一生产定义和codec，覆盖全部在用信号/责任/布局。命名访问按已声明字段组计算请求，不依赖整表固定范围、邻接/固定下标；原IPlcTransport/ModbusTcpClient连接所有权保留。纯协议程序集不引用业务/网络/DB，业务不引用它。 在通信范围校验同地址区实际占用重叠、类型/宽度、位号、读取方向/写方及清零责任、必需信号、地址容量和逐请求合法访问长度；区分地址区及字段内合法bit解释，Float32两word合法，可合法分组的跨度先拆分不误拒。校验由正式LatestProtocolPlcDevice.StartAsync在初始化/心跳/pump派发前调用，定义与派生计划全通过后才授予该实例可派发状态；同实例RequestStartAsync及访问器拒绝未通过实例，依赖该映射的初始化/Ready/Start/目标/命令/确认写入均不得发生。只在隔离Test输入边界允许PD非法定义/计划fixture进入同生产校验，不能增加业务raw参数或生产热切换。T022—T027继续闭合全通道和Host装配，不留下未使用校验函数。  
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T019 实现必要原始证据的真实保存基础：更新 `backend/src/Gaode.Infrastructure/Persistence/TraceWriter.cs`、`Station01DbContext.cs`、`Station01EntityConfigurations.cs`，拟增同目录 `CommunicationEvidenceStore.cs` 与 `Migrations/CommunicationEvidence.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-018—022/036/038；AC-009/012—014/023/025/026；SC-005/009/012；EC E01—E05，模型§4—6.1；AL03/AL06/AL07。**宪章原则**：P01/P04/P05/P07/P08/P09/P13。  
  **前置**：T007、T008、T011、T012、T017、T018。
  **交付**：闭合必要raw表/专用writer/真实回执及受控新Test库准备；正式Host准入核真实结构/schema。旧库升级转出，禁止Host自动迁移。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T020 实现本次有限store升级及兼容探测：更新 `backend/tools/Gaode.StorePrep/Program.cs`、`backend/src/Gaode.Infrastructure/Persistence/StoreCompatibilityProbe.cs`、`StoreAccessGuard.cs`，补 `backend/tests/Gaode.Integration.Tests/Storage/StorePreparationTests.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。

## Phase 3：US1 协议维护不触碰业务层（P1，T021—T028）

**目标**：正式通信链所有在用协议表示和握手由通信职责解释，业务不依赖地址/码/布局。
**独立验收条件**：真实实现/接口依赖闭合后，以BM00适用内容检查、构建、直接语义及人工消费路径证据验收；完整动态/历史/保存故障运行转出，不作前置。
**适用验证先行**：复用现有保护和检查器，T053固定当前集合、T055—057取得新证据；转出不计Passed。

- [X] T021 [US1] 先承接线缆保护断言，拟增 `backend/tests/Gaode.Communication.Tests/Devices/SignalConformanceTests.cs`、`ActionHandshakeTests.cs`、`TransportFeedbackTests.cs`、`ProtocolDefinitionAdmissionTests.cs`

  **依据**：FR-006—012/015—018/025/026/031；AC-001—005/007—012/016/018；SC-002/005—007；PC P02—P06，VG V01/V06/S00，迁移矩阵T02/T04—T16/T21/T29/T34。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。 另直接承接spec“必要边界与失败场景”及PC P03，VG V01.1的PD固定正负例是既有要求补齐，不列作旧迁移矩阵已覆盖测试。  
  **前置**：T003、T013、T017、T018、T019。
  **交付**：保留已迁入通信工程的原有效报文断言及正式入口校验成果，核与业务断言分离；整套PD12/握手动态运行转出，不删除断言。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T022 [US1] 迁移正式初始化、心跳和轮询：更新 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`HeartbeatDiagnosticWindow.cs`、`ModbusTcpClient.cs`

  **范围状态**：拆分；原通信动态/完整PD/保存/握手验收义务按原ID保留在scope-adjustment，当前只验代码边界及直接语义。
  **依据**：FR-006—008/012/015—017/020；AC-007/011/012/016/019；SC-002/004/006/009；PC P02—P05，EC E01，BD B03；AL01/AL03/AL05。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T005、T007、T010、T012、T017、T018、T019、T021。
  **交付**：全部信号读写经命名访问器，集中可靠观察/报警语义/新鲜度和原始批次关联；消除心跳地址、整表读取范围/邻接数组解释。保留现有business/heartbeat通道、原I/O/心跳期限与epoch/首末错误记录，不把重连解释为恢复动作。 承接T018正式准备校验及未通过实例写入阻断，heartbeat/business两通道均不得早于合法定义准入；保持校验失败可观测，合法运行时原心跳/停止合同不变。  
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T023 [US1] 迁移运动、启动、A区域准备和复位：更新 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`

  **范围状态**：拆分；原通信动态/完整PD/保存/握手验收义务按原ID保留在scope-adjustment，当前只验代码边界及直接语义。
  **依据**：FR-007/009/010/012/016/017/019；AC-004/007/008/010/011；SC-002/004/006；BD B03/B03.1/B04，PC P02—P04；AL01—AL03。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T005、T006、T007、T012、T017、T018、T021、T022。
  **交付**：逐适用XYZ/轴/命令/反馈统一编解码，复用运动租约和当前动作关联；Accepted/夹紧/A独立窗口不合并，A在3D/F前以运行容量准备，不移到B。系统复位和初始状态输出业务语义，实际位置不能由目标填充，恢复不造按钮或PC夹紧命令。  
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T024 [US1] 迁移采集/F/E复位和翻面内部握手：更新 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`

  **范围状态**：拆分；原通信动态/完整PD/保存/握手验收义务按原ID保留在scope-adjustment，当前只验代码边界及直接语义。
  **依据**：FR-009—012/016—019/025；AC-008/011/016/018；SC-002/004/006；BD B03/B04，PC P04，迁移矩阵T13/T16—T18/T21；AL03/AL06。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T007、T008、T012、T017、T018、T021、T023。
  **交付**：IAcquisitionCycle语义窗口隐藏检测/F状态码与复位；自动/人工翻面解释留通信，校验本次反馈/面/epoch/位置、原超时与ACK清零。实际保存完成后释放，公共3D原允许的失败清理不扩大到F/E；删除对Application FlipFeedbackCorrelation原值算法的依赖。  
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T025 [US1] 迁移取放、下料与解锁通信并接语义回调协议：更新 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`、`LatestProtocolPlcDevice.cs`

  **范围状态**：拆分；原通信动态/完整PD/保存/握手验收义务按原ID保留在scope-adjustment，当前只验代码边界及直接语义。
  **依据**：FR-009—012/016—019/020/025；AC-008—012/016/018；SC-002/004—006/009；BD B05/B05.1/B06，PC P04，EC E02/E02.1；AL03/AL06/AL07。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T007、T008、T011、T012、T017、T019、T021、T024。
  **交付**：先观察真实取料和必要握手/raw有效保存，再调用业务CommitPick；验证当前有效回执/关联/安全/期限后才允许槽位、放料目标及放料命令。raw持久未确认走独立语义失败通知而非伪PickCommit。保持下料目标阶段采样与抬升完成两证据、WholeTray前置/真实解锁、派发前有限重试和派发后UnknownHeld，通信不接管业务事务。  
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T026 [US1] 迁移独立VirtualPlc到统一定义/codec：更新 `VirtualPlc/VirtualPlcEngine.cs`、`PlcDataStore.cs`、`PlcAddressMap.cs`、`ModbusTcpServer.cs`、`DeviceActionAudit.cs`、`Program.cs`

  **范围状态**：拆分；原通信动态/完整PD/保存/握手验收义务按原ID保留在scope-adjustment，当前只验代码边界及直接语义。
  **依据**：FR-006—012/015/020/025/031；AC-001—005/007—012/016；SC-002/006/009；PC P01—P06，EC E01，AL04/AL05。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T003、T009、T010、T012、T018、T021、T025。
  **交付**：只共享纯协议定义/编解码，设备状态机/本机代次与动作序号保持独立；全信号/批读取布局不依赖相邻。保持真实TCP命令到反馈，Host不能写反馈，审计gap与设备本机身份如实记录；不共享上位机业务数据库或状态机。  
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T027 [US1] 接入正式通信基础装配：更新 `backend/src/Gaode.Host/Composition/Station01Registration.cs`、`AdapterBindings.cs`、`PlcConnectionHostedService.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-006—012/015/020/033；AC-007—012/016；SC-002/006/010；BD B02，PC P01/P02，EC E02；AL01/AL03—AL05/AL07。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T005、T007、T009、T010、T011、T012、T019、T022、T023、T024、T025、T026。
  **交付**：正式Host同实例装配真实端口/取料回调/raw writer；用正式DI解析/消费者编译及TCP实例证据，不能只改未装配包装器。
  **验收与证据**：当前依BM00的构建、正式命名访问/内容报告、固定正负例与直接语义、人工正式入口/动作准入/所有权及保存链核查；不以源码代替原真实TCP/整套PD/故障/握手验收。原条件保留后续、未验不算Passed。若本轮改产品行为则追加相应最小真实验证；本轮未改产品动作/保存/预算实现。

- [X] T028 [US1] 补齐全部在用信号的访问证据与通信探针，拟增 `backend/tests/Gaode.Communication.Tests/Devices/SignalAccessCoverageTests.cs`、`scripts/communication/check-009-wire-evidence.py`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-006—012/015/025/031/032；AC-001—005/007—012/016/019；SC-002/006；PC P02/P03/P06，VG V06，AL05。**宪章原则**：P02/P04/P05/P06/P07/P09/P13。  
  **前置**：T003、T010、T012、T018、T021、T022、T023、T024、T025、T026、T027。
  **交付**：核正式适配器/VirtualPlc统一定义和命名访问，不留业务旁路或隐含布局；原全信号TCP覆盖/独立oracle运行证据保留后续。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

## Phase 4：US2 正式主流程行为和保存门禁保持（P1，T029—T041）

**目标**：正式直接消费者语义闭合，原安全/身份/占用/期限及必要保存不回退；完整预算业务验收转出。
**独立验收条件**：真实实现/接口依赖闭合后，以BM00适用内容检查、构建、直接语义及人工消费路径证据验收；完整动态/历史/保存故障运行转出，不作前置。
**适用验证先行**：复用现有保护和检查器，T053固定当前集合、T055—057取得新证据；转出不计Passed。

- [X] T029 [US2] 先建立行为保持语义验收，更新 `backend/tests/Gaode.Contracts.Tests/Ports/StagePortContractTests.cs`、`Workflow/ThreeStageWorkflowExecutorTests.cs`，拟增 `Station01/RecipeApplicationContractTests.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002/009—011/016—019/024/035—038；AC-007—011/015—018/022—027；SC-001/004/005/007/012；BD B01—B06，VG V01/V07/V09。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T017、T021、T025、T028。
  **交付**：建立直接受影响语义验收：动作身份/所有权、安全、原有限期限、完成区分与必要保存；完整新增预算配置/三入口BA转出。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T030 [US2] 实现独立预算schema消费及冻结校验：更新 `backend/src/Gaode.Domain/Configuration/BusinessBudget.cs`、`backend/src/Gaode.Application/Configuration/PublicConfigurationValidator.cs`、`ConfigurationFreezer.cs`、`backend/src/Gaode.Infrastructure/Configuration/ConfigurationLoader.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T031 [US2] 发布合法新版Test预算并更新全部实际引用：更新 `specs/001-station01-public-preparation/examples/budgets.test.json`、`budgets.virtual-plc.json`、`specs/007-station01-integrated-loop/examples/budget.virtual-loop.json`、`simulation.virtual-loop.json` 及选定008 fixture、启动脚本和测试构造者

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T032 [US2] 实现Application统一绑定总窗口与必要保存协调，拟增 `backend/src/Gaode.Application/Recipes/RecipeApplicationCoordinator.cs`，更新 `Station01/RunExecution.cs`、`Workflow/RecipeExecutionBudget.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T033 [US2] 迁移公共步骤、运动与恢复业务消费者：更新 `backend/src/Gaode.Application/Motion/MotionCoordinator.cs`、`MotionAdmission.cs`、`Station01/StartupReadiness.cs`、`PhysicalFaultPolicy.cs`、`FixedMoveRecoveryInteraction.cs` 及 `Station01/Steps/StartClampStep.cs`、`ThreeDStep.cs`、`FScanStep.cs`、`PalletUnlockStep.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/007—011/016—019/021/024；AC-007—011/013/015—017；SC-001/002/004/005；BD B02—B06，PC P04，AL01/AL03/AL06。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T005、T007、T008、T012、T017、T023、T024、T025、T029。
  **交付**：核公共步骤/运动/恢复全部直接消费者只解释语义；区域A/B原触发、预算、保存不移动。按本轮源码扫描、少量接口语义和构建证据验收，不跑整机。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T034 [US2] 迁移配方计划及阶段业务执行：更新 `backend/src/Gaode.Application/Recipes/RecipeContracts.cs`、`RecipeRunPlanner.cs`、`Workflow/RecipeExecutionCoordinator.cs`、`RecipeSortingMapper.cs`、`ThreeStageWorkflowExecutor.cs`、`WholeTrayWorkflowOrchestrator.cs`、`FlipFeedbackCorrelation.cs` 及 `backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/009—011/016—019/024/037；AC-008—011/015—018/027；SC-001/004/005/007/012；BD B03—B07，I06/I09/I10，AL02/AL03/AL06。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T006、T007、T008、T012、T017、T024、T025、T029、T033。
  **交付**：核配方计划/阶段/取放下料直接消费者语义迁移闭合，原JSON/工艺/槽身份保持；当前内容检查与构建，不要求全配方或Final。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T035 [US2] 迁移正式检测编排到有限语义能力：更新 `backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/009/011/015—022/024；AC-008—015/017；SC-001/004/005/009；BD B02—B04，EC E02—E04，I11，AL03/AL06。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T007、T008、T012、T017、T024、T029、T033、T034。
  **交付**：既有IntegratedDetection迁移成果保留；消除具体协议依赖和假持久引用，正式消费者编译及相关语义/架构验证闭合；完整Worker/相机/媒体回归转出。
  **验收与证据**：按VG BM00的适用固定Case/数据行及本任务直接保护义务核实际产物、正式调用链和本轮报告；仅代码/文档存在不算运行通过，证据未齐保持未勾选。具体路径沿本项标题及原任务路径，原任务全文保存于scope-adjustment。

- [X] T036 [US2] 落实真实CommitPick与取料后raw失败业务出口：更新 `backend/src/Gaode.Application/Workflow/SortingTargetAllocator.cs`、`StageEventing.cs`、`backend/src/Gaode.Infrastructure/Persistence/StageEventStore.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-016—022/024；AC-009—015/018；SC-004/005/009；BD B05/B05.1/B06，EC E02/E02.1，模型§4.1/4.2，AL03/AL06/AL07。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T007、T008、T011、T012、T017、T019、T025、T029、T034、T035。
  **交付**：保留正式取料→必要raw→业务真实InTransit有效回执→放料回调；只核/修边界、接口及正式接线；A/B/C不放料/不重发规则保持。若本轮代码修改涉及保存，补最小真实SQLite验证，完整故障矩阵转出。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T037 [US2] 接通严格链、旧链和独立绑定API及后段消费者：更新 `backend/src/Gaode.Application/Station01/StartPublicPreparation.cs`、`PublicPreparationHandoffV2.cs`、`RunExecution.cs` 与 `backend/src/Gaode.Host/Api/RecipeEndpoints.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-010/016/017/019/035—039；AC-008/011/022—028；SC-004/011/012；BD B03.1/B03.2，EC E02.2，I26，AL02—AL04/AL06/AL08。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T004、T006、T007、T008、T009、T012、T017、T019、T033、T034、T036。
  **交付**：核严格链/旧链/独立API及handoff当前语义端口接线，冻结10000ms/原后段期限/必要保存保持；只验直接接口语义/构建，不再将完整三入口预算或动态条件等待作为本轮前置。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T038 [US2] 实现通信后台绑定取消与期限仲裁及最小Test故障缝：更新 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`、`VirtualPlc/SimulationModels.cs`、`VirtualPlcEngine.cs`、`Program.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-010/015—017/035—039；AC-008/011/022—028；SC-002/004/011/012；PC P04/P04.1，BD B03.2，VG V09，AL02/AL03/AL05/AL08。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T004、T006、T007、T010、T012、T017、T022、T023、T024、T025、T026、T037。
  **交付**：核通信内部持有取消/期限及后继派发资格，不把内部步骤外露；不改变既有资格关闭规则。完整健康I/O等待/后台故障动态验收转出；本轮直接改动该逻辑时补对应最小测试。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T039 [US2] 闭合全部正式生产者/消费者及替身装配：更新 `backend/src/Gaode.Host/Composition/Station01Registration.cs`、`AdapterBindings.cs`、`UnavailablePlcRecipePort.cs`，`backend/src/Gaode.Infrastructure/Simulation/SimulatedPlc.cs`、`SimulatedDeviceState.cs`、`Integrations/NotIntegratedStagePorts.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/015—019/021/033/035—038；AC-007—013/017/022—027；SC-001/004/005/010—012；BD B02—B06，I12/I17/I26，AL01—AL06/AL08。**宪章原则**：P03/P04/P05/P06/P07/P08/P10/P13。  
  **前置**：T004、T005、T006、T007、T008、T009、T010、T012、T017、T027、T033、T034、T035、T036、T037、T038。
  **交付**：核正式Host同一设备实例及全部直接生产者/消费者、替身签名编译一致，零业务旁路；不用假成功或FullSimulation声称动态完成。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [ ] T040 [US2] 实现全部BA数据行和绑定真实提交故障验证：更新 `backend/tests/Gaode.Contracts.Tests/Configuration/ConfigurationValidationTests.cs`、`Station01RunConfigurationTests.cs`、`Recipes/RecipeExecutionCoordinatorTests.cs`、`backend/tests/Gaode.Rules.Tests/Timing/OperationIngressTests.cs`，拟增 `backend/tests/Gaode.Integration.Tests/Station01/RecipeApplicationDeadlineTests.cs` 和 `Storage/RecipeApplicationReceiptTests.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T041 [US2] 实现取料与raw保存A/B/C精确故障及保守重启验证：更新 `backend/tests/Gaode.Contracts.Tests/Persistence/StageEventStoreTests.cs`、`backend/tests/Gaode.Integration.Tests/Storage/StageAndCompletionTransactionTests.cs`、`TraceStoreTests.cs`，拟增同目录 `PickCommitFailureTests.cs`、`CommunicationEvidenceFailureTests.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。

## Phase 5：US3 原始通信证据可追溯但不控制业务（P1，T042—T047）

**目标**：必要通信批次真实持久、身份可关联、直接公开语义无raw，诊断不回流控制；完整历史和页面验收转出。
**独立验收条件**：真实实现/接口依赖闭合后，以BM00适用内容检查、构建、直接语义及人工消费路径证据验收；完整动态/历史/保存故障运行转出，不作前置。
**适用验证先行**：复用现有保护和检查器，T053固定当前集合、T055—057取得新证据；转出不计Passed。

- [ ] T042 [US3] 先建立诊断、历史和公开字段验收，拟增 `backend/tests/Gaode.Integration.Tests/Api/CommunicationDiagnosticsTests.cs`、`DeviceSemanticProjectionTests.cs` 与 `backend/tests/Gaode.Communication.Tests/History/LegacyDevicePayloadTests.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T043 [US3] 闭合实际通信捕获、关联与有界日志：更新 `backend/src/Gaode.Infrastructure/Devices/Plc/ModbusTcpClient.cs`、`HeartbeatDiagnosticWindow.cs`、`LatestProtocolPlcDevice.cs`、`Persistence/CommunicationEvidenceStore.cs`（T019拟增）和 `Diagnostics/RuntimeDiagnosticLogging.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-008/016—022/036/038；AC-009/011—014/023/025/026；SC-004/005/009/012；EC E01/E02/E02.1/E02.2，BD B03/B05.1，AL03/AL06/AL07。**宪章原则**：P05/P07/P08/P09/P12/P13。  
  **前置**：T007、T008、T011、T012、T019、T022、T025、T036、T038。
  **交付**：核真实raw捕获/存储/查询位于精确通信诊断职责，业务只持不透明引用；原writer及失败保守规则不拆除。完整持久三场景和历史重开转出。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [ ] T044 [US3] 实现有限历史投影和只读原始证据读取，拟增 `backend/src/Gaode.Infrastructure/Persistence/LegacyDeviceHistoryReader.cs`、`CommunicationEvidenceReader.cs`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T045 [US3] 落实所有公开字段版本和当前资格投影：更新 `backend/src/Gaode.Host/Api/QueryEndpoints.cs`、`CommittedResultProjection.cs`、`Station01ApiContracts.cs`、`Station01NotificationService.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/017/020—024/036/038；AC-009/012—017/023/025/026；SC-001/007/009/012；EC E03/E04，I§5/§5.5，BD B03.2，AL04/AL06/AL08。**宪章原则**：P05/P07/P08/P09/P12/P13。  
  **前置**：T004、T008、T009、T012、T017、T037、T039、T043。
  **交付**：核Host status/startup/run/evidence/通知只生产消费语义，不解释报警位或raw，不保留raw业务影子；只修本轮边界/直接接口问题，完整历史/页面发布转出。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [ ] T046 [US3] 验收006必要数据绑定交付与API消费者版本，登记 `artifacts/recipe-execution-008/009-isolation/{runId}/consumer-alignment.json`（运行时拟增），核对 `frontend/src/runtime.js`、`frontend/src/state/notification-reducer.ts`

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T047 [US3] 完成诊断生产/消费和历史保存一致性检查：更新 `backend/tests/Gaode.Integration.Tests/Api/CommittedDispositionProjectionTests.cs`、`CommittedResultProjectionTests.cs` 及T042诊断测试

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。

## Phase 6：US4 测试保护正确的边界（P2，T048—T051）

**目标**：逐旧断言承接有效保护，业务与wire断言分离，历史失效要求有依据、有替代。
**独立验收条件**：真实实现/接口依赖闭合后，以BM00适用内容检查、构建、直接语义及人工消费路径证据验收；完整动态/历史/保存故障运行转出，不作前置。
**适用验证**：T051逐片段追溯，T053专项固定账本与T057—059实际承接；复用T021/T029/T041/T042相关测试，完整BA/SU不作前置。

- [X] T048 [US4] 完成端口、步骤、配方、规则和模拟测试迁移：更新 `backend/tests/Gaode.Contracts.Tests/Ports/`、`Station01/`、`Recipes/`、`Workflow/`、`Simulation/` 及 `backend/tests/Gaode.Rules.Tests/Motion/MotionAdmissionTests.cs`、`Station01/CompletionPolicyTests.cs`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/016—019/023—026/035—039；AC-007—011/015—018/022—028；SC-001/004/005/007/011/012；VG V01，BD B01—B06，迁移矩阵T01—T33/T51—T53。**宪章原则**：P03/P04/P07/P08/P13。  
  **前置**：T021、T029、T033、T034、T035、T036、T037、T038、T039。
  **交付**：核相关业务测试不再要求Application暴露wire知识，有效业务断言保留；必要报文断言已交通信测试。执行BM00有限直接接口语义集；无关业务测试整理转出。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T049 [US4] 完成实际集成与三处历史Skip替代：更新 `backend/tests/Gaode.Contracts.Tests/Devices/SingleFaceDetectionIntegrationTests.cs`、`Persistence/StageEventStoreTests.cs` 及 `backend/tests/Gaode.Integration.Tests/Station01/`、`Api/`、`Storage/` 对应矩阵方法

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-015—026/035—039；AC-007—018/022—028；SC-004—007/009/011/012；VG V01/V07/V09，迁移矩阵T34—T47/T51—T53。**宪章原则**：P03/P04/P07/P08/P13。  
  **前置**：T035、T043、T045、T048。
  **交付**：核直接受影响集成/混合测试的通信断言归属及原历史Skip替代记录，不删除有效断言或新增Skip；完整替代场景的动态运行/相机Worker链转出，不能计Passed。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T050 [US4] 拆分混合脚本断言并保持调用方拒绝结果：更新 `scripts/verify-station01-page-diagnostics.cjs`、`validate-008-operation-evidence.py`、`validate-008-route-evidence.py`、`validate-008-flip-timeout.py`、`audit-008-night-page-route.py`、`validate-008-authorization-page.py`、`summarize-q01-q02-evidence.py` 及相关PS调用者

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-003/004/021/023—029/035—039；AC-013/015—021/022—028；SC-001/007/008/011/012；VG V02.1/V04/V09，迁移矩阵T50/T52及§3.1；AL04—AL06/AL08。**宪章原则**：P03/P04/P07/P08/P13。  
  **前置**：T004、T008、T009、T010、T012、T015、T016、T028、T037、T038、T045、T048、T049。
  **交付**：复用已拆分脚本与三语言AST，相关业务断言只读语义，probe只读通信诊断；递归helper/分类新增检查，不用重分类逃避。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T051 [US4] 闭合全部旧义务到新可执行案例的追溯：更新 `backend/tests/Gaode.Rules.Tests/Architecture/009-test-obligations.json`（T002拟增），生成运行证据 `migration-audit.json`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-005/023—026/033/035—039；AC-006/015—018/022—028；SC-007/010—012；VG V01/V04/V09，迁移矩阵T01—T53。**宪章原则**：P03/P04/P07/P08/P13。  
  **前置**：T002、T021、T028、T048、T049、T050。
  **交付**：保留53组193方法/245数据行原登记和既有替代定位；只核本轮涉及的协议断言纠偏及BM00语义方法，其余完整执行/原SC007全迁移运行转出。旧登记完整性不改，不把源对应视为执行通过。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

## Phase 7：US5 协议知识回流时门禁失败（P2，T052—T053）

**目标**：正式内容扫描和逐case账本阻止错误架构及漏跑“通过”。
**独立验收条件**：真实实现/接口依赖闭合后，以BM00适用内容检查、构建、直接语义及人工消费路径证据验收；完整动态/历史/保存故障运行转出，不作前置。
**适用验证**：早期检查器已先实现；本阶段闭合最终注册/输入/执行入口，不能把架构门禁全部推到重构末尾。

- [X] T052 [US5] 收紧到已实现语义边界并验证正式源码和契约：更新 `backend/tests/Gaode.Rules.Tests/Architecture/009-boundary-inventory.json`、`009-public-shapes.json`、`ProtocolBoundaryTests.cs` 及 `scripts/architecture/009-script-boundary-cases.json`

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/006/012/021/024/027/028/033；AC-006/013/017/019/021；SC-001/002/008/010；VG V02/V02.1/V03 A01—A10、N01—N17、P01—P10。**宪章原则**：P05/P07/P13。  
  **前置**：T014、T015、T017、T018、T022、T023、T024、T025、T026、T028、T033、T034、T035、T036、T037、T038、T039、T043、T045、T048、T049、T050、T051。
  **交付**：核完整受保护源/公共端口/合同/业务测试与脚本分类；正式扫描零违规、正负例同checker，无宽白名单、排除或Skip；构建/人工补全实际消费范围。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T053 [US5] 闭合专项固定案例账本及漏跑拒绝：scripts/workflow/009-required-cases.json、runner.py、test_runner.py、test_verify.py、verify_entry.py、scripts/verify.ps1

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-023/026—029/035—039；AC-018—020/022—028；SC-007/008/011/012；VG V04/V09 G01—G07及全部BA数据行；AL05/AL08。**宪章原则**：P05/P07/P13。 补VG V01.1 PD和V07 SU固定数据行，直接追溯spec必要边界/PC P03及模型§6.1。  
  **前置**：T012、T016、T048、T050、T051、T052。
  **交付**：在发现前新增独立scripts/workflow/009-boundary-minimum-cases.json（拟增），按BM00审核准确方法/固定case数据行，保留原009-required-cases.json不变。复用生产validate_required_ledger核缺失/未执行/过滤/Skip/旧报告；不登记PD/CS/M/MC为当前必需。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

## Phase 8：当前最小验证与收口；转出动态任务保留（T054—T069）

本节当前只执行BoundaryMinimum构建/内容门禁/直接语义与收口。T058—060/T062—068保持转出，原测试与记录不删除；不启动完整通信动态编排。

- [X] T054 接通有限边界验证入口：scripts/verify-009-protocol-isolation.ps1、scripts/workflow/boundary_minimum.py

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-015/018—020/026/029—039；AC-001—012/018—020/022—028；SC-003—012；VG V04—V09，quickstart§1/3—7；AL05/AL08。**宪章原则**：P01/P02/P04/P05/P07/P08/P09/P10/P13。  
  **前置**：T016、T053。
  **交付**：在scripts/verify-009-protocol-isolation.ps1增加明确BoundaryMinimum分支并拟增scripts/workflow/boundary_minimum.py有限接线，复用runner.read_trx/test_identity/validate_required_ledger及现有脚本/selfcheck；不启动动态进程、不建设专项平台。原Baseline/Gates/Mutation保持原意义。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T055 准备已实现版本的Test运行环境与构建，登记 `artifacts/recipe-execution-008/009-isolation/{runId}/environment.json` 和 `build-manifest.json`（运行时拟增）

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-001/015/033—039；AC-006/007/022/028；SC-010/011；PC P01，VG V07—V09，quickstart§1/3/4/7。**宪章原则**：P01/P02/P04/P05/P07/P08/P09/P10/P13。  
  **前置**：T012、T039、T043、T045、T048、T050、T052、T053、T054。
  **交付**：构建backend/Gaode.slnx及VirtualPlc/VirtualPlc.csproj必要依赖，记录实际命令、返回码、源码及加载二进制摘要；不运行设备/整机。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T056 运行正式架构与完整性门禁，输出 `{runId}/native/csharp-boundary.json`、`script-boundary.json`、Rules TRX及`ledger-selfcheck.json`（证据根E）

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002—004/012/021/024/027—029；AC-013/017/019—021；SC-001/002/008；VG V02—V04 A01—A10/N01—N17/P01—P10/G01—G07。**宪章原则**：P01/P02/P04/P05/P07/P08/P09/P10/P13。  
  **前置**：T014、T015、T016、T052、T053、T054、T055。
  **交付**：实际执行四正式仓库边界扫描、两依赖及固定Roslyn、所有既有C#及42脚本正负例、三语言正式内容/分类与G01—07同生产漏跑拒绝。缺任一必需行不能Passed，负例正确拒绝不等于产品动态通过。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [X] T057 运行本轮固定直接语义组件验证，输出证据根E的 `Gaode.Contracts.Tests.trx` 与执行账本

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-002/006—029/035—039；AC-007—021/022—028；SC-001/002/004—009/011/012；VG V01/V04/V07/V09，BD B03—B06，PC P02—P05。**宪章原则**：P01/P02/P04/P05/P07/P08/P09/P10/P13。  
  **前置**：T029、T048、T053、T055、T056。
  **交付**：实际执行BM00固定的20条直接端口/少量绑定语义测试，0Skip、0缺失；只有直接代码改动触及保存/取消时增加适用最小真实验证，不能用模拟回执证明SQLite提交。整套协议动态/预算/故障验收不运行。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [ ] T058 执行正式适配器与独立VirtualPlc正常通信专项：artifacts/recipe-execution-008/009-isolation/{runId}/main/

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T059 执行通信直接失败与保存准入专项：artifacts/recipe-execution-008/009-isolation/{runId}/failure/

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T060 运行诊断历史及受控升级中断验证，输出 `artifacts/recipe-execution-008/009-isolation/{runId}/history-store/`（运行时拟增）

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T061 签核通信专项基线：artifacts/recipe-execution-008/009-isolation/{runId}/baseline-acceptance.json

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-001—029/033—039；AC-006—028；SC-001/002/004—012；VG V01—V05/V07—V09，BD/PC/EC全部适用条款。**宪章原则**：P01/P02/P04/P05/P07/P08/P09/P10/P13。  
  **前置**：T012、T039、T043、T045、T048、T050、T051、T052、T053、T054、T055、T056、T057。
  **交付**：签核BoundaryMinimum：本轮构建、完整正式源码/业务合同/测试/脚本内容、所有固定正负例/漏跑拒绝、20直接语义行及人工正式接线核查真实通过。仅当前范围证据，PD/CS/M/MC或旧库未验不得Passed。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。

- [ ] T062 冻结修复后业务、预算、契约和门禁，生成 `artifacts/recipe-execution-008/009-isolation/{runId}/freeze-manifest.json`（运行时拟增）

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T063 执行M01非连续地址演练，在 `artifacts/recipe-execution-008/009-isolation/{runId}/variants/M01/`（运行时拟增）保留隔离构建和证据

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T064 执行M02报警位演练，在 `artifacts/recipe-execution-008/009-isolation/{runId}/variants/M02/`（运行时拟增）保存结果

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T065 执行M03命令/反馈编码演练，在 `artifacts/recipe-execution-008/009-isolation/{runId}/variants/M03/`（运行时拟增）保存结果

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T066 执行M04字序演练，在 `artifacts/recipe-execution-008/009-isolation/{runId}/variants/M04/`（运行时拟增）保存结果

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T067 执行M05内部步骤变化演练，在 `artifacts/recipe-execution-008/009-isolation/{runId}/variants/M05/`（运行时拟增）保存结果

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [ ] T068 执行MC01双端同错及MC02冻结修改负控制，保存 `artifacts/recipe-execution-008/009-isolation/{runId}/negative-controls/`（运行时拟增）

  **范围状态**：转出；历史勾选与成果保持，不计当前活动/通过率，不作当前代码收敛前置。
  **后续义务**：原完整条款及证据见scope-adjustment对应原ID，仍未验部分不是Passed；已有期限/保存/安全规则和在用实现不得撤回。
- [X] T069 签核009专项完成并登记转出限制：artifacts/recipe-execution-008/009-isolation/{runId}/acceptance.json、acceptance.md

  **范围状态**：拆分；只验收以下009义务，原完整义务见转出记录。
  **依据**：当前活动FR/AC/SC以coverage-matrix对应任务行为准；FR-001—039；AC-001—028；SC-001—012；BD/PC/EC/VG全部适用条款；宪章P01—P13。**宪章原则**：P01/P02/P04/P05/P07/P08/P09/P10/P13。  
  **前置**：T001、T003、T012、T051、T056、T057、T061。
  **交付**：逐当前31FR/16AC/6SC活动义务核BoundaryMinimum本轮真实报告/固定账本、构建和人工核查，登记真实剩余及旧失败。仅本范围全部有证据才宣告009通信代码边界最小收敛通过；不宣告完整动态、整机、旧库或Production。
  **验收与证据**：按VG BM00及coverage-matrix本轮义务核真实源码/构建/发现/执行/人工正式链证据；原动态义务转出不算Passed，缺当前证据保持未勾选。


## 用户故事依赖与阶段完成条件

已完成来源/实际跨合同/基础通信成果复核 → US1正式通信定义/访问及US2语义消费者联合闭合 → US3合法诊断边界/当前Host语义 → US4相关断言归属 → US5分类/内容门禁 → T053固定有限清单 → T054最小接线 → T055构建 → T056扫描/正负例/账本拒绝 → T057直接语义 → T061当前基线 → T069本轮结项。

动态T058/059和T062—068不作当前前置；所有真实接口/生产者消费者和必要保存能力仍必须闭合。没有新增[P]；原T002/003与T014/015仅限既有共同前置和不同文件，不并行改同一接口。

## 当前覆盖与转出

逐活动FR/AC/SC实现、验证、具体拒绝证据见coverage-matrix；原编号引用若超出活动集合，是来源/保留约束，不使整套原验收重新活动。AL01—08具体已有对齐及新签名前置仍按T004—012/impact-matrix，不以矩阵假装其他合同已修改。

迁移矩阵T01—T53原义务、193方法/245数据行登记保留；T048—051当前只处理边界错误与其对应直接语义，原完整运行去向见scope-adjustment。PD12/CS36/SU6/BA/M/MC原固定清单保留后续；BM00全部固定C#/脚本/N/P/G及20接口行由T053登记、T054接线、T056/057执行、T061/069核证。不能互相代替或将转出Passed。

当前活动任务49项：T001, T002, T003, T004, T005, T006, T007, T008, T009, T010, T011, T012, T013, T014, T015, T016, T017, T018, T019, T021, T022, T023, T024, T025, T026, T027, T028, T029, T033, T034, T035, T036, T037, T038, T039, T043, T045, T048, T049, T050, T051, T052, T053, T054, T055, T056, T057, T061, T069。

整体转出20项：T020, T030, T031, T032, T040, T041, T042, T044, T046, T047, T058, T059, T060, T062, T063, T064, T065, T066, T067, T068。原[X]保留历史，不证明本轮新范围；当前拆分验收勾选只有对应证据齐备后更新，原完整验收未完成仍在追溯表保留。

本轮约4小时时间盒，提前满足即停止；到期不降低标准、无限延长或自动Passed。


## 本轮最小收敛验收（2026-10-02；最新结论）

仅BoundaryMinimum通过：当前49项活动义务均具本轮构建、125项固定执行及人工边界/正式接线证据。原完整任务的转出义务不因此完成。证据：`artifacts/recipe-execution-008/009-isolation/boundary-min-20261002T032400Z/acceptance.json`、`manual-review.json`、`consistency-review.json`、`result.json`、`execution-ledger.json`。

本轮新增勾选：T033, T034, T036, T037, T038, T039, T043, T045, T048, T049, T050, T051, T052, T053, T054, T055, T056, T057, T061, T069。原33个勾选保留；合计53勾选、16未勾选（均转出），不把历史转出勾选计作当前验收。所有原ID和任务来源完整保留。

实际新运行：两个构建零警告/错误；C#50、脚本正负例42、脚本正式源/分类4、G账本7、直接语义20，共125，0Skip/缺失/违规。434个C#与规范合同报告条目、157个脚本文件被记录；内容检查与哈希身份校验分别完成。

本轮仅新增/加固检查器、七分类负例及合法诊断正例、最小接线；没有修改产品动作/保存/期限/取消实现，没有设备/数据库操作。当前源码及加载构建与结果摘要相符。原53组193方法245数据行源/替代断言完整性保留，不表示其完整运行通过。

F04-handshake旧wire失败继续保留；PD/完整36动态/M01—M05/MC、完整保存故障/历史升级/预算、配方/前端/相机/Worker/整机等未本轮执行。当前通过不批准生产或完整动态行为。

### 010实施定向对齐 A07（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A07**：context/2.0仍绑定前冻结原Detection/Unload/Sorting起点和值；context/1.0仍handoff后首次Detection建立；独立bind只读已有截止，不造下游窗口。配方应用意图真实提交后、排队/调用前唯一t0，Test10000ms及更早截止/必要回执门保持。ExecutionCostProfile从本轮批准预算形成语义额度/引用/摘要，共同公式不解释PlcIo/PlcPoll或17/16通信次数，生产未批局部拒绝且不回退。
  生产/消费与010实施承接：Start/预算/RecipeApplicationCoordinator→Handoff/ThreeStage/独立绑定→frontend/src/runtime.js、模拟脚本、BA06；T008—T010/T016/T020/T027。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A06（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A06**：typed冻结输入保存在既有RecipePlanAndBindingIntent版本payload，经RunExecution.SaveAsync(ActionIntent)→ITraceWriter/RunWrite回执，ITraceQuery按Run/Tray/Plan/引用/摘要读取；独立绑定仍用原IStageEventStore。v2字段/旧摘要不改，Source仅取当前Call匹配且已提交F Origin.Source，多组件各读实际事实。缺提交/错Call/Unknown拒续接；历史reader/Rescan保留不回填、不恢复许可。
  生产/消费与010实施承接：RunExecution/StageHandoffBuilder→ITraceWriter/RunWrite/ITraceQuery/consumer→独立绑定/历史/状态API；T008/T015/T016/T019/T020/T028/T029。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。

### 010实施定向对齐 A09（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A09**：所有验收profile无条件L，统一verify/runner、auto-dev/step及009 boundary_minimum/protocol_isolation旁接共用执行/凭证核验。_run_verify passed、verify_entry退出、assess/finish及旁接ledger/result/subsetPassed最终点拒漏跑/Skip/旧身份/解析失败/伪Passed。L含职责闭包B/N/P、G/C、受影响009静态，不开Host/PLC/Worker/DB、不递归完整验收。010按B→冻结→E/S→T，009动态范围及SelectedCasesOnly overall009Passed=false保持；D10先迁活动映射，历史证据不改。
  生产/消费与010实施承接：Rules/manifest/migration→verify及workflow/旁接aggregator→最终凭证/结论；T033—T042；009活动JSON映射属于T033，历史快照/报告只读。

完整字段和判据见[IB](../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。

本节实现和取证归上述010任务，旧任务状态不变。有效测试断言按VG-06迁移，不能删除来源/身份/必要保存保护以取得通过。


### 010 S修复定向对齐：取放料通信证据接续（2026-10-02）

010实际S暴露取料至放料完成超过有界通信缓冲覆盖期，必要保存按gap拒绝。沿用E02的已提交分段引用：MaterialPicked原始证据真实提交后，通信内部记录从该批次截取点开始的下一段；MaterialTransferred必须关联同一Run/Operation/Action/Epoch的已提交取料段，保留两个实际位置与取放反馈/ACK。分段不重开期限，不产生新的业务许可，不把取料已存等同于业务InTransit已存；放料仍等待当前有效CommitPick回执。任何段缺失、gap、错关联或保存失败保持UnknownHeld并禁止自动重放，失败诊断保留已提交前段引用。业务端口仍只接收语义结果/不透明引用，历史plc-evidence/1 reader不改写旧记录。

010 T030/T044/T047承接此直接阻断：原取放真实SQLite/反馈组件增加两段关联检查，原取料保存失败负例保留；S验证原慢动作输入下完整一次搬运。受影响构建/组件纳入当前B必需清单，旧失败/冻结/E保留原身份；修复后重新B→冻结→E/S→T。此处不重开009动态全集，也不调整轮询、预算或预期。

## 013实施前定向同步（2026-10-04）

SY-02/03：FR-008、P03/P04当前由013 A01—08承接：合法有限读计划在不可变映射准入后生成/校验/复用，非法映射与未知地址扩读仍拒绝；两连接、通信内部单源/有限PDU仲裁，不把协议知识或轮询参数放入业务。E01/E02/E04的基础与Position各自真实采样起止/代次/可靠性，不能用新心跳续旧值、拼接成原子快照；普通位置陈旧不等于断线，关键准入按需实读，完成后实际位置先发布。Domain观察形状及plc-evidence/1保持；Host device-semantics/1.2位置新增SampleStartedUtc、SampleEndedUtc、ConnectionEpoch、Reliability，Axis消费者使用Position.Identity.Reliability。保留8192窗口、gap拒绝、1024分段、真提交/保存门及旧历史原文。T022/观察/证据历史勾选不变；新实现归013 T011—T025。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

013 T024定向补充：现行Host/012已消费的axisObservations按011 execution-and-state EX状态表逐字段登记语义字段，不把业务消费者改归通信。Python内建isinstance(value, dict)仅作已知JSON对象类型谓词，不能授权动态键、原始字段或任意helper逃逸；同名重绑定仍拒绝。未知profile显式拒绝，原C01拒绝义务保持，C03合格当前凭据沿显式Default009验证，不允许未知profile回落。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

本轮仅确认需求同步，不生成新设计或任务；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。

- [ ] T016-I01 定向同步与消费本功能直接相关公共配置/观察/处置/下料边界，产物以新016 tasks T002及对应共同代码任务追踪；原历史编号和勾选不改。


新016 T018/T020门禁清单修订：依据当前源码显式登记CurrentCompositionOptionsRemainAcceptedWithoutRetiredPollingKnob及CommunicationCollectionPolicyCannotEnterTheCommonBusinessLayer；四个旧绑定方法对应到BoundReceiptCannotAuthorizeBindingWithoutActualRequiredSave、FailedIntentNeverRegistersTotalWindowOrCreatesBound及LateOrCancelledBoundReturnDoesNotCreateHandoff两数据行。原caseId和保护语义保留，新增两项，不减少必跑项、不放宽失败/Skip/身份规则。StagePortContractTests保留全部断言，用显式完整XYZ/ExpectedObjects及实际阶段枚举修正旧夹具；声明组件输入不冒硬件。生产存储准入不变，新016测试支持层只读其专属运行库，不调用离线兼容性入口。
