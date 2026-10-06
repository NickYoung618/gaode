# 技术方案：配方执行与测试环境解耦、共同业务执行层建立及架构防回归

**功能标识**：010-recipe-execution-isolation  
**日期**：2026-10-02  
**规格**：[spec.md](spec.md)，18条FR、12项AC、8项SC  
**宪章版本**：7.0.0  
**阶段**：Phase 1增量修订，针对architecture ARC-001—004；当前文档复核结论见文末及architecture追加记录。实现、构建及运行验证均未执行。  
**实际目录**：E:/dzk/gaode-1/specs/010-recipe-execution-isolation  
**范围**：第一工位公共准备、F绑定/移交、配方检测及适用处置、最终完成的共同执行与环境隔离；不扩大为生产接入或整机验收。

## 初始化与本轮边界

用户本次明确授权plan，spec中“本轮仅specify”属于上轮记录，保持只读。已核AGENTS.md、宪章、feature.json和实际规格。按speckit-plan技能运行setup-plan.ps1 -Json，返回：

| 字段 | 实际值 |
| --- | --- |
| FEATURE_SPEC | E:/dzk/gaode-1/specs/010-recipe-execution-isolation/spec.md |
| IMPL_PLAN | E:/dzk/gaode-1/specs/010-recipe-execution-isolation/plan.md |
| FEATURE_DIR | E:/dzk/gaode-1/specs/010-recipe-execution-isolation |
| BRANCH | 010-recipe-execution-isolation；脚本的目录名回退，不证明存在Git分支 |

工作区及已检查父目录未发现.git，未进行Git写入或初始化。feature.json选择已一致，保持原字节。extensions.yml的hooks为空，没有前/后plan钩子要执行。技能要求的并行研究仅只读，结论汇入research.md。

初次plan仅写本目录plan、research、data-model、quickstart和三个必要contracts，未改spec/checklist及其他项目文件；下述初次复核记录保留。2026-10-02本次增量授权限于七份现有设计中必要部分和architecture.md，后者保留历史并追加逐项复核后才勾选；不重建spec，不生成tasks，不修改其他规格/配置/源码/测试/历史证据，不执行构建、测试、设备、数据库或Git写操作。再次setup-plan返回上述相同路径并跳过模板复制，现有plan未被覆盖。

## 方案摘要

复用现有规划、检测、聚合、设备及保存实现，建立唯一共同链。IntegratedDetectionPort的有效业务迁到Application，正式IDetectionPort固定绑定该实现；删除externalVirtualPlc后段开关、整段模拟结果装配、非严格占位执行和历史复扫入口。环境负责格式解码、坐标来源、能力/设备绑定及批准准入；共同层保留测量/身份/范围、工序、结果、反馈、期限和保存规则。

既有context/1.0仍有活动消费者且009保留其期限起点，故保留输入格式及期限生命周期，让它同样经过共同校验/执行，删除Strict=false特权。2.0保留选择与F匹配，作为010单配方验收入口。历史记录只读，不增加升级工程。

| P13阶段边界 | 当前方案 |
| --- | --- |
| 起点与终点 | 一个合法配方版本及模拟环境，经现正式启动到同轮FinalUnloadCompleted真实提交，含适用授权取盘 |
| 必须参与组件 | 正式Host/共同执行、现设备/采集/算法/保存端口、独立虚拟PLC/Worker、真实SQLite与媒体 |
| 必要验证 | 受影响构建、边界/装配、N/P/G门禁、最少受影响分支、完整单配方、冻结替换、义务/删除/执行核对 |
| 完成证据 | 当前源/构建/输入、同Run动作/调用/来源/提交、独立预期、冻结差异和逐项证据 |
| 延期项 | 全量测试/整机/全页面/全配方/全故障、性能轮询长稳、新工艺/标定/真实SDK、编辑器/插件平台、历史升级及无关治理 |

依据RCP-001/002/003/005/008、POS-001—003、ALG-013、SRT-001—005/007—009、DAT-001/007，001/003/007/008有效合同和009保留的设备/期限义务。决策及替代方案见[research.md](research.md)，不把旧测试结构当作业务要求。

## 技术上下文（Technical Context）

| 事项 | 当前选用方案 | 决策来源与状态 | 尚缺证据/OPEN |
| --- | --- | --- | --- |
| 后端 | 现.NET10/C#、ASP.NET Core，SDK10.0.401/rollForward disable，nullable/警告视错误 | global.json及Directory.Build.props，复用 | 本轮未构建 |
| 算法 | 现独立Python Worker、IAlgorithmPort、PythonWorkerAdapter/Supervisor；补语义需求绑定；第二实现选ContentSampleWorker/1 | R04/R10，复用传输但独立处理实际媒体内容；wire不扩字段 | 第二脚本/绑定及实际身份待实施验证，不以改启动配置代替实现 |
| 数据 | 现SQLite/EF Core10.0.12及事件/媒体存储 | 现依赖版本，不增表 | 新payload提交、旧摘要读取待验证 |
| 前端/宿主 | 012负责授权配方弹窗及006现有运行绑定；011提供真实投影 | 不新增页面、不改原型归档 | 只核受影响绑定与联合代表，012设计交付另行接收 |
| 设备/采集 | 现语义端口、固定图片、独立VirtualPlc；CaptureEvent补实际应用事实 | 复用正式接口，不接真实SDK | 生产坐标/标定/能力/协议缺失继续局部受限 |
| 期限/容量 | 原冻结预算/容量/媒体租约/重试；配方应用Test10000ms | 保留009期限锚点；阶段测试成本移批准输入 | Production预算未批，不回退；不优化性能 |
| 验证 | xUnit2.9.3、Test SDK18.10.1、runner3.1.5、SDK Roslyn、现workflow/TRX | 统一验收每轮L＋010完整profile；B/S/T去重顺序，VG-01.1/05.1 | profile/L/正负例未实现，运行全部未验证 |

## 宪章检查（Constitution Check）

设计前是当前源码相对本专项的缺口；设计后仅审查方案，不代表运行通过。现状违反项均有修复设计，无复杂度豁免。

| 原则 | 检查点 | 设计前 | 设计后 | 依据/限制 |
| --- | --- | --- | --- | --- |
| P01 | 最新规则、需求与证据追溯 | 符合 | 符合 | 18/12/8追溯、旧结果保持，跨功能登记不冒充已对齐 |
| P02 | 主流程真实参与、范围克制 | 违反待修正 | 符合 | R01/CE-01/04删除整段模拟；V05独立组件与真实存储 |
| P03 | 共用配方与实际运动 | 违反待修正 | 符合 | R02/03、IB-01/02保留工艺，移出测试格式知识 |
| P04 | 有限等待、失败、安全依据 | 违反待修正 | 符合 | 删除非严格旁路；CE-02/03保持Pending/物理失败区别、期限和未知保护 |
| P05 | 分层、单Host/控制权 | 违反待修正 | 符合 | 唯一Application执行，通信边界保持，helper纳入检查 |
| P06 | 必需资源与有界等待 | 符合 | 符合 | 下方资源表；复用媒体租约、释放/退出，不扩并发 |
| P07 | 身份、来源与状态分离 | 违反待修正 | 符合 | R05/IB-04去来源兜底，补实际采集事实 |
| P08 | 保存、冻结与恢复 | 符合 | 符合 | CE-03真实回执；typed输入使用既有事件提交，历史只读 |
| P09 | 结构化日志与必要诊断 | 符合 | 符合 | CE-03/V03必要失败日志；无新日志平台 |
| P10 | OPEN局部限制 | 待补充，仅限制所列部分 | 待补充，仅限制所列部分 | DEP-02仅限制未批准生产入口；模拟设计可推进 |
| P11 | 配置变化与新能力区分 | 违反待修正 | 符合 | IB-01/03/05和冻结替换，无新引擎/平台/兼容旁路 |
| P12 | 原型只读、前端经后端 | 符合 | 符合 | 仅核消费者影响，不实施页面 |
| P13 | 最小验证与完成真实性 | 违反待修正 | 符合 | V05补授权取盘终点，VG-01—08固定范围及完整性 |

## 结构与职责（Project Structure）

以下是实施位置，不是本轮已创建源码。

| 模块 / 拟定位置 | 保留、提取或迁移 | 状态/资源所有者与依赖 |
| --- | --- | --- |
| Application/Station01/StartPublicPreparation及现步骤 | 保留公共准备/F后业务，去externalVirtualPlc条件 | 唯一Run推进，使用批准期限策略及当前回执 |
| Application/Recipes/RecipeContracts、RecipeDefinitionValidator、ExecutionInputs | 演进原模型，从JsonRecipeCatalog提取唯一共同校验 | 组成/面/路线/身份/容量规则，只收语义字段 |
| Infrastructure/Recipes/JsonRecipeCatalog及解码辅助 | 保留文件入口，承接fixture/坐标/参数/映射格式 | 产typed定义与环境绑定，调用共同validator，不安排工序 |
| Application/Recipes/RecipeRunPlanner、CoordinateResolver | 复用展开/工作量，提取测量/偏置/范围规则 | 冻结计划目标，移出TestEligibleSlots及JSON |
| Application/Workflow/RecipeDetectionExecutor | 迁移Integrated有效业务，删测试依赖/历史入口 | 唯一检测编排，依赖现相机/算法/动作/保存端口 |
| ThreeStageWorkflowExecutor、WholeTrayWorkflowOrchestrator | 保留处置/完成，改消费typed目标和实际来源 | 预留/在途/提交/解锁/人工确认所有者保持 |
| Application/Capabilities、Host组合根 | 现注册补检测/融合/E及码解析绑定，固定共同执行 | 提供者选择/批准在边界，缺能力拒绝 |
| Infrastructure采集/算法/设备、TraceWriter/查询 | 复用并补采集事实，删除来源推断，历史只读 | 适配器持有设备/媒体/进程资源；009边界不变 |
| Rules/Architecture、scripts/workflow | 复用检查/账本，扩有限010规则/profile | 正式验证入口核必需发现/执行/证据，不调度业务工序 |

不新建工程、Production执行器或通用引擎。TestTrayCodePolicy、SimulatedDetectionPort/Profile/DetectionTestMode及确认无用的复扫链在义务承接后删除；[研究D01—D11](research.md)给出调用/装配/配置/脚本/动态/历史核查。仅移动文件不算完成。

## 数据、契约与状态

模型见[data-model.md](data-model.md)，路径与状态见[共同执行合同](contracts/common-execution.md)，格式/坐标/能力/准入见[输入边界合同](contracts/input-boundaries.md)。共同输入无ProfilePayloads、PositionPayloads、PayloadJson或fixture嵌套DTO。SourceFact、ApprovalScope、AdmissionDecision分开，CaptureEvent区分请求参数与真实应用事实。

typed输入保存在既有意图事件的版本化payload；v2移交字段/摘要不变。当前续接必须同时具有真实提交、当前有效回执、匹配身份/计划和未失效期限，不能从历史查询重建执行许可。

### 共享合同与消费者影响表

**全部待实际对齐**。本轮其他文档只读，此表不是对齐完成证据。每组共享接口代码变更前，须在获准范围内完成所属spec/contracts/plan/tasks的必要定向更新，登记消费者承接后再改代码；不重写无关部分。

| ID / 旧合同 | 拟调整 | 生产者 | 直接 → 间接消费者 | 所属规格与合同 | 顺序 |
| --- | --- | --- | --- | --- | --- |
| A01 TrayRecipe/RecipeRunPlan及profile/position/disposition负载、TestEligibleSlots | typed语义/共同校验/目标，批准独立，目录历史只读 | JsonRecipeCatalog/validator/planner | Start/绑定/预算/移交/检测/分拣 → catalog/plan API、fixture及投影 | 002 recipe-execution；008 execution/test-virtual-mapping/test-multi-object | ①输入及计划 |
| A02 F解析及能力注册 | 原码/映射证据，已绑解析/检测/融合/E需求，无默认Test能力；实际算法Origin按CallId保存，不能以WorkerSession/预期版本代替 | 解码器、现能力绑定、算法事实 | FScan/FCodePolicy/检测 → 冻结、F选择、Worker及测试 | 001 acquisition-algorithm/configuration-time/persistence-handoff；002 recipe；003主链；008 execution | ①输入，②绑定/事实 |
| A03 DetectionRequest/Target、Strict/SourcePolicy | typed输入/用途/来源/准入，去占位及三来源白名单 | Handoff consumer/CoordinateResolver | 共同检测/ThreeStage → 整盘、结果、stub及集成测试 | 003 detection-port/public-preparation-handoff；007 virtual-integration；008 execution | ③共同请求 |
| A04 AuxiliaryHandlingRequest.TestSourceReference | 语义坐标依据，保留辅助通道批准和动作关联 | 共同检测/目标解析 | LatestProtocolPlcDevice.Acquisition/虚拟辅助通道 → 动作证据/查询/通信fixture | 003 manual/rotation/e-test-execution；008 execution；009 business-device | ③先语义再端口 |
| A05 CaptureEvent/MediaRef、AlgorithmFactPayload、固定physicalSdkApplied | 实际采集关联/来源/应用状态、运行用途；公共算法实际Origin与CallId关联提交 | 相机/算法适配及协调 | 共同采集/检测/TraceWriter → Handoff、RunMediaCatalog/CommittedResultProjection/006 | 001 acquisition-algorithm/persistence-handoff；007 virtual-integration；008 evidence/api-results；006 api | ②事实，④投影 |
| A06 移交Source/引用与冻结输入 | Source取已新增提交的实际F来源、多组件分开；旧字段/摘要保持；连续链typed意图沿ITraceWriter/RunWrite保存、ITraceQuery读取，其他阶段/独立绑定沿原存储 | StageHandoffBuilder/现保存链 | HandoffConsumer/TraceQuery → 独立plan/bind、历史/状态/006 | 001 persistence-handoff；003 handoff/component-source-matrix；008 evidence；009回执 | ③保存/授权，④历史 |
| A07 context1.0/2.0、Budget/Receipt | 均完整共同校验，保留原期限锚点/值与有效回执 | Start/预算/RecipeApplicationCoordinator | Handoff/ThreeStage/独立绑定 → 前端启动/模拟脚本/009 BA06 | 001 configuration-time；002 recipe；003主链；008 E06；009 B03.2 | ①期限原则，③生命周期，④消费者 |
| A08 整段模拟DI/DetectionTestMode | 唯一共同执行，缺能力拒绝；结果注入移设备/算法 | AdapterBindings/Registration/Program | 正式Host → verify-latest-plc、rig、007联调/008单配方 | 003 detection；007 virtual-integration/commissioning-cli；008 execution | ③装配，⑤测试 |
| A09 验证manifest/runner/migration | 每个验收profile无条件L＋010完整集、职责闭包/N/P/G/C、B/S/T及当前凭证；009活动FQN/目标映射定向承接，原义务/历史证据不删 | Rules/脚本解析/runner及现009旁接聚合器 | verify.ps1/全部workflow → _run_verify/verify_entry、assess/finish；BoundaryMinimum/SelectedCasesOnly最终点 → 当前凭证和对应范围总判定 | 010 verification；009既有边界保护与迁移登记 | ⑤先义务/入口定向对齐，再改共享验证代码及删除合并方法 |

消费者核查：[006 API](../006-frontend-station01-console/contracts/api.md)及runtime.js仍接受1.0 Test/S1/P01，2.0核所选配方。source显示依赖现枚举值，不能重排。RunMediaCatalog/CommittedResultProjection输出已提交身份/媒体及RequestedCapture；新实际设置缺失不得填请求值。旧handoff及Rescan媒体/动作有真实读取消费者，保留原摘要/历史显示。不新增页面、控件或数据库升级。

## 配方共用逻辑与动作隔离

本功能共同业务消费012的真实保存/目录/完整读取，保存后F唯一绑定时取得新内容并冻结；不在010/011另建编辑器或发布平台。正式启动及既有授权操作共用后端准入；测试仅准备/驱动现操作/核验。独立/recipes/plan与/bind保留合法读取/绑定，不成为第二次续接入口。

### 配置与策略扩展设计（P11）

既有工艺内素材、码映射/标识、参数/坐标提供及模拟实现变动只作用于环境边界，必须真实影响执行且可追溯。共同层仍核工序/对象/必检/结果；不承诺新工艺只改配置。复用现能力注册的小型明确映射，不建插件平台。

合同兼容和实时就绪分开；未注册/不兼容明确受限，运行时暂不可用按已有有限算法规则处理。参数、预算、批准及版本随Run冻结；替换演练不调大预算、不改共同规则。

## 并发、资源与异常出口

| 路径 | 所有者/容量来源 | 等待期限来源 | 失败终态及后续 | 资源释放/保留 |
| --- | --- | --- | --- | --- |
| 配方应用/运动 | 现协调器、同一设备及唯一准入；容量按原入口区别 | 冻结预算及既有较早期限，绑定Test10000ms | 无有效回执不续接、未知不盲重发 | 失效关闭后继，已发事实保留 |
| 采集/检测/融合/E | 现协调/Worker及媒体租约，原批次/容量 | 冻结采集/算法/阶段期限 | 质量故障有限Pending，物理/保存失败不得降格 | 由实际input-release/退出证明释放 |
| 自动/人工换面/旋转 | 现辅助动作及授权，typed目标/物理实体 | 冻结人工等待/动作/阶段期限 | 占用/姿态不明限制后继，不编造语义 | 沿现清零/观察，暂停不当故障恢复 |
| 下料/分拣 | 现预留/在途/单动作控制 | 原Sorting/Unload期限 | 缺目标/冲突/保存失败先拒；取料在途提交才放料 | 未知占用不软件抹除 |
| 保存/停止 | 现存储队列/短事务、独立控制 | CriticalSave及更早期限 | 必要保存未确认不放行，记录迟到/失败 | 心跳/观察/必要停止不等算法/磁盘/UI |

不增加并发平台或防御矩阵；缺配对输入不得无限占据可执行资源，媒体批次规则保持。

## 保存与恢复

保留公共配置、采集/算法意图与事实、媒体metadata、F、冻结计划/目标/能力/预算、绑定/移交及回执、检测/融合/E、动作、预留/在途/完成、整盘/解锁/最终取盘的必要保存。新typed数据沿现事件payload，不改终态不可变规则、历史行或数据库schema。

窗口内实际提交与稍后观察分开。历史v1完成/v2摘要/复扫读取不恢复旧动作；正常暂停沿现继续，故障双端复位后显式完整新轮，不新增故障续跑或兼容入口。

## 软件验证与证据计划

方法/数据行迁移、最小集、门禁及替换表集中在[验证合同](contracts/verification.md)，操作顺序见[quickstart](quickstart.md)。

本次定向修订不增加FR/AC/SC：VG-06 SRC-01—14及NF登记来源、采集、F移交、最终主体与投影的具体义务，正常链不能抵未知/错关联/原子保存负例。VG-05.1统一准备输入/替代实现/独立预期 → B通过 → 冻结 → E及S → V07核T；B含V04除指定S的其他全部必要分支，S为整体NG/Pending共同组件，仅冻结后一次共享V04/V06。正常完整链仍为B中V05及E两条。变化后重建受影响基线和新冻结，旧替换结论失效。

VG-01.1的L在所有项目验收profile无条件执行，不依赖Git或变更分类；_run_verify、assess/finish及现BoundaryMinimum/SelectedCasesOnly聚合最终点核本轮凭证，缺失或旧证据失败。L只含源码/有限检查器与报告数据样本，不运行Host/PLC/Worker/数据库；正式DI和动态主链仍属010完整集。R10及VG-07.1固定第二独立内容样本Worker的实现差异与输入处理证据，不允许仅以路径/参数/名称/摘要变化证明替换。

| 需求/原则 | 场景 | 方法/输入 | 预期 | 证据 |
| --- | --- | --- | --- | --- |
| FR-001—010，P02—09 | 完整单配方、缺能力/F不匹配/非法算法、动作或保存失败 | V02—05，正式入口及实际端口/存储 | 同一业务推进，具体拒绝，无伪成功 | 同Run事实/日志/最终提交 |
| FR-003—007/017，P03/07/11 | 等价替换与合法结果变化 | 修复后冻结，V06五类替换 | 共同文件零内容变化、行为/保存符合独立规则 | 冻结/差异/oracle及两轮事实 |
| FR-011/012/016，P01/08/13 | 测试迁移、删除及历史保留 | V07方法/数据项与D01—11 | 有效义务100%承接，确认无用100%删除 | 源差异/清单/当前证据 |
| FR-013—015/018，P05/13 | 五负类/三正类/无效报告/后续profile漏跑 | L及V01 B/N/P/G/C，同正式判定函数 | 错误拒绝、合法通过，其他profile不能绕过，缺项/旧报告不能通过 | 当前manifest/ledger、父attempt凭证及源/构建身份 |

### FR → 设计 → AC/SC追溯

| FR | 设计落点 | AC | SC | 验证集合 |
| --- | --- | --- | --- | --- |
| FR-001 | R01/02、CE-01/04 | 01/04/10 | 001/002/003 | V01/02/05/06 |
| FR-002 | R06/07、CE-01/03 | 01/02/03/10 | 001/003/008 | V01/02/05 |
| FR-003 | IB-01—03、语义模型 | 04/06/10 | 002/003 | V01/02/06 |
| FR-004 | IB-02、CE-02、聚合 | 03/05/06 | 002/006/008 | V02/03/04/06 |
| FR-005 | IB-01/03/05、R04 | 01/02/04/07 | 001/002/005 | V02/05/06/07 |
| FR-006 | CE-04、IB-03、R10/VG-07.1 | 01/02/04/10/11 | 001/002/003/004/008 | V01/05/06 |
| FR-007 | IB-04、R05/07、VG-06 SRC/NF | 02/04/06/11 | 002/003/008 | V01/02/03/05/06 |
| FR-008 | CE-05、quickstart | 01/03 | 001/008 | V05 |
| FR-009 | CE-02/03、期限/资源/保存 | 03/09 | 006/008 | V02/03/04/05 |
| FR-010 | CE-03、VG-05失败证据 | 01/03/09 | 001/006/008 | V03/04/05 |
| FR-011 | VG-06方法/数据行、SRC/NF | 07/08/09 | 005/006/007 | V02—05/07 |
| FR-012 | CE-02/05、VG-07 oracle | 01/04/05/07 | 001/002/005/008 | V03—07 |
| FR-013 | VG-01.1/02/03 | 10 | 003/004 | L/V01 |
| FR-014 | VG-02/03 N/P | 10/11 | 003/004/008 | L/V01 |
| FR-015 | VG-01.1/04/05.1/06 G/C与B/S/T | 09/12 | 006/007 | L/V01/07 |
| FR-016 | 研究D01—11、VG-06 | 08 | 005 | V07 |
| FR-017 | VG-05.1 B/S/T、VG-07/07.1、R10 | 04/05/06/10 | 002/003 | V01/06 |
| FR-018 | VG-05/08、A01—09 | 01—12 | 001—008 | V00—07 |

文档追溯18/18 FR、12/12 AC、8/8 SC均有设计及拟验证去向。成功指标仍是：SC-001完整一条；002冻结零改动且五类实质替换；003零非法依赖；004五负类/三正类；005义务承接与确认删除100%；006受影响构建/分支；007必需执行证据100%；008真实来源/保存/授权及零新增协议泄漏。没有运行通过声明。

## OPEN、外部依赖与决策记录

| 事项 | 影响及下一步 | 可继续部分 |
| --- | --- | --- |
| DEP-01 / A01—09 | 共享接口代码前完成所属spec/contracts/plan/tasks定向对齐；本轮尚未完成 | 010设计清单审查 |
| DEP-02 / OPEN-07/14/18/26 | 生产预算/坐标/标定/能力及未定义机械语义仍限制对应生产入口 | 合法模拟/共同架构 |
| DEP-03 | D01—08明确删除/承接，实施前复核；D09/11保留；D10已确认009清单/登记依赖，先承接其独有义务并更新活动映射再合并删除 | 不把清理候选一律延期，不删除历史源快照 |
| DEP-04 | 实施后取得VG-05.1的B修复基线再冻结；S不作B执行前置，冻结后与E完成再核T；旧失败按具体依赖判断阻断 | 全部运行仍未验证，不能冒充已通过 |
| fixture版本漂移 | 当前usr-e-1.0.2/Q02声明Available但引用旧budget；准备时核当前完整批准预算/摘要，缺项拒绝 | 准备受控新轮输入，不改旧fixture/证据来伪造 |
| 未决设计问题 | ARC-001—004按本次SRC/NF、B/S/T、持续L及R10定向修订；最终文档复核见architecture追加记录 | 不新增范围；实现及验收仍待后续 |

## 客户确认原型检查（P12）

010不实施页面/宿主，不触碰原型ZIP或HTML。本轮只核现runtime及006 API消费者，不声称逐页原型对齐，不要求重跑全部页面。A05—07若需调整现字段投影，先定向合同对齐再绑定真实数据；不新增页面/控件或改变人工确认流程。

## 本轮文档复核与停止点

已核七份设计产物、18条FR追溯行及33个有效本地链接；未保留模板占位或需要改变010范围的未决澄清。复核纠正了F格式字段归属、连续链真实保存接口、F实际来源保存缺口、语义额度与预算版本关联、基线前独立预期顺序及取盘expectedRevision。spec、requirements、feature.json、AGENTS及宪章的SHA256与初始化前一致，tasks.md不存在，后置hooks仍为空。

结论仅为技术研究及Phase 1文档可进入设计清单审查。A01—09尚待实际定向对齐，所有实现/构建/运行门禁及成功指标仍待后续验证；本轮在plan停止。

### 2026-10-02 ARC定向修订后的文档复核（当前）

以上为初次plan记录，保留其当时结论。本次根据architecture的ARC-001—004增量修订并重新审查：VG-06的SRC/NF补足实际来源/采集/移交/最终矩阵及独立负例；VG-05.1区分B/S/T且S不作冻结前置；VG-01.1将持续轻量L接入所有现项目验收与旁接聚合最终点；R10/IB-03/VG-07.1选定第二独立内容样本算法。未变更18 FR、12 AC、8 SC的业务含义。

本轮额外复核发现的原子attempt当前性与未受影响B证据复用歧义，已在VG-04及C02/C03明确：原证据保留原身份，仅在对应依赖摘要不变且有独立影响审阅时引用；当前L、修改义务及失效E/S不可复用，新冻结后重新执行E/S。现旁接BoundaryMinimum/SelectedCasesOnly、同fixture未点名来源保护和旧移交非严格断言也已纳入定向承接，没有扩成009或API全量运行。

宪章设计后复核：P01追溯与原证据保持；P02/03/05仍唯一共同主链、无新引擎/平台；P04/06/08/09原期限、反馈、取消、保存、未知和日志保持；P07增加实际来源的具体测试义务；P10生产局部限制、P11环境替换边界、P12原型只读保持；P13以L持续拒绝、B/S/T最小两条完整链及独立预期落实，不把文档审查当运行通过。未发现需要扩大010范围的未决设计矛盾。

当前33项设计质量的逐项结论及ARC关闭依据见[architecture本轮追加复核](checklists/architecture.md)。文档具备进入speckit-tasks的准备度；A01—A09仍全部待实际定向对齐，D候选消费者复核/承接后删除、预算/能力批准及所有实际构建/验证均未完成。这里只结束plan，未生成tasks或启动实施。

## 011共同执行承接（2026-10-03）

010原验证集合/历史结果保持当时范围；011/012后续只选本次受影响构建、组件回归、架构门禁和联合代表链，不重新执行全部历史专项。共同模型/唯一业务校验须覆盖真实保存重读与F料盘编号唯一匹配、冻结隔离、配置更多面/独立E、三区处置/姿态退出；设备原码/内部握手与Test编号/路径不得回流业务。字段/签名/存储留011/012 plan对齐。替代后核查真实调用、装配、配置、脚本与历史读取，保留保存/关联/取消/期限义务并实际删除无用途旧旁路/测试特权/协议/配置/测试及孤立代码；原失败证据不删。

## 013实施前定向同步（2026-10-04）

SY-04：此前延期的PLC轮询性能由013在通信适配器内实现，不改变共同业务唯一执行路径、CE输入/保存/取消/期限边界。013显式profile必须保留完整持续L（当前70声明项，按实际case/dataRow核对），受影响009同身份结果一次引用；不扩010动态全专项。before测量是有自身源码/构建/输入的子阶段，不套after门槛，不等待最终Passed；013 final保持L外层和必需账本，不能成为通用跳L开关。每侧一次60秒空闲和同一run-2，必要组件含正常HeldFlip失败保存/真实自动1024分段接续/Recorder缺口及三类负例。实际API观察器仅提供2秒后端负载，不能冒充页面ready或改变真实保存/F匹配/冻结/Final。验收严格按013 V01.1 A/B/C分侧和V09.1身份去重。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

前轮specify仅确认需求同步；本次014 Phase 1及012配套设计见当前设计引用，不生成新tasks；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。


## 新016直接相关增量（2026-10-06）

本次仅承接[新016共同合同](../016-public-preparation-tray-check-unload/contracts/public-tray-flow.md)的直接相关边界。公共上下料与3D位置沿整机公共配置，配方不重复坐标；初次3D完整观察后空盘/介入可不执行F并合法下料、人工确认和结束；正常首次继续才F绑定。姿态异常是独立处置依据，跳过后续检测，最终从原槽真实Pending分拣，不伪造算法结果；复查保留已完成事实。组内每实际零件有独立位置。后端拥有单次10秒决策及原始截止，前端只显示/提交；本盘结束不表示全部检测完成。协议/实际取料提交门/反馈/保存及未知保护不变。
