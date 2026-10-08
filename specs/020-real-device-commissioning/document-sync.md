# 阶段A关联文档同步记录

日期：2026-10-08。状态：**阶段A设计、产品实现及离线验证已同步完成**。最新用户确认及[HC-020](contracts/closed-loop-handshake.md)、[TC-020](contracts/commissioning-tool.md)是本阶段统一出处；旧合同其他不变规则继续有效。不是版本发布记录。

## 当前已修改的既有文档

| 文件 | 原冲突/缺项→本次处理 | 受影响消费者与后续证明 |
| --- | --- | --- |
| [011通信](../011-plc-interaction-update/contracts/plc-communication.md) | PC02/03/04只写清触发→入口增量明确等新鲜全0、父子边界及同坐标 | 正式Axes/Stages/Flip/Transfer；V01–V06 |
| [011状态](../011-plc-interaction-update/contracts/execution-and-state.md) | 完成/可继续混淆→区分物理完成、清零、必要保存和当前请求复用证据 | Semantics/Acquisition/Stage消费者；V03/08 |
| [011验证](../011-plc-interaction-update/contracts/verification.md) | 缺新握手最小集合→增020 V01–V10链接及本次实际结果 | 现测试夹具与新验证记录；不改旧报告 |
| [正式同坐标spec](../same-position-axis-move/spec.md) | 启动0+反馈1即沿用→同连接历史闭环资格+当前请求/反馈0及新鲜复核 | 正式轴入口；V05/08 |
| [正式位置证据](../same-position-axis-move/contracts/position-evidence.md) | 旧反馈1和位置即可→实际运动/清零/复用证据分离，当前Action关联 | reachedPositions/采集消费者；V05/08 |
| [正式同坐标plan](../same-position-axis-move/plan.md) | 旧发布顺序易被当当前指令→注明历史顺序由020承接 | tasks后续拆本次代码/测试，不重跑旧发布 |
| [014执行](../014-special-part-rotation/contracts/execution.md) | EX14-03/04缺R/Sort全清与下一件门→限定增量补全 | Rotate/Transfer、取料提交、抓手保持；V01/06/09 |
| [014验证](../014-special-part-rotation/contracts/verification.md) | 缺清零后状态的定向验证→新增待执行要求 | 正式TCP/VirtualPlc；旧结论不动 |
| [工具017协议](../../tools/plc-commissioning/specs/017-independent-commissioning/contracts/protocol.md) | 请求0即cleared、旧1前态→顶部声明HC/TC覆盖范围 | App各入口、API/状态、worker；V02–V09 |
| [工具018 spec](../../tools/plc-commissioning/specs/018-flip-recipe/spec.md) | 放回只清命令/Sort旧安全位顺序→清两反馈、最终安全位后清Sort | recipe父子周期；V01/06 |
| [工具018 plan](../../tools/plc-commissioning/specs/018-flip-recipe/plan.md) | 轮询防重入不具体→纯采集/推进分离和跨轮次清零 | App/recipe；V02/07 |
| [工具同坐标spec](../../tools/plc-commissioning/specs/same-position-axis-move/spec.md) | 与正式同坐标同一旧反馈1冲突→同样修订，保留工具判据差异 | manual/recipe/reuse；V05/09 |
| [工具位置证据](../../tools/plc-commissioning/specs/same-position-axis-move/contracts/position-evidence.md) | 静态位置和反馈1即可→历史资格加本次复核，不能造新完成 | App记录、recipe最终复核；V05/08 |
| [工具同坐标plan](../../tools/plc-commissioning/specs/same-position-axis-move/plan.md) | 旧版本发布顺序→020承接说明 | 新tasks引用；原tasks/validation不改 |
| [工具README](../../tools/plc-commissioning/README.md) | 没有新规则入口→链接当前设计/契约/待执行验证 | 使用/实现入口；明确仍为1.1.6，未发布新版本 |

同一修订日不表示这些功能旧版本已升级。以上15个文档仅改当前有效条文或追加明确覆盖说明。相应产品代码已同步；正式26项、工具57项四文件定向回归与Host构建结果见[validation](validation.md)。

## 020本功能产物

已生成[plan](plan.md)、[research](research.md)、[data-model](data-model.md)、[HC契约](contracts/closed-loop-handshake.md)、[TC契约](contracts/commissioning-tool.md)、[quickstart](quickstart.md)及本文；[spec](spec.md)和[requirements](checklists/requirements.md)更新为阶段A设计完成、整体仍有限制。

[tasks.md](tasks.md)的28项阶段A任务已按实际证据收口：准备/共用前置7项、US2实现验证16项、US4同步记录2项、收尾3项。先前只读analyze为0项当前阶段冲突；本次实施、正式TCP/VirtualPlc/SQLite验证及工具独立loopback结果见[validation](validation.md)，未部署或发布。

## 实际落地与15份文档追溯

| 文档组（对应上表全部15份） | 最终实现 | 对应实际证据及HC/TC |
| --- | --- | --- |
| 011通信/状态/验证（3份） | LatestProtocolPlcDevice.Handshakes / Axes / Stages / Acquisition / Semantics / Polling、PreparedPlcReadPlans；StageActionAdapter.Transfer | HandshakeClosureTests、SamePositionTests；V01–V08、HC-01–07；当前Action定位证据可继续采集及保存 |
| 正式同坐标spec / plan / position-evidence（3份） | Axes逐轴历史闭环资格、当前新鲜0/0与位置复核；Polling漂移/状态失效；Semantics当前请求关联 | SamePosition六项、工具八项同坐标回归；V05/08，首次/复位/漂移不能借静态坐标，HC-04 |
| 014执行/验证（2份） | R清RotateStart再确认全0；Sort取料实际提交→Place→安全位子闭环→父清零；VirtualPlc内部完成事实准入 | SixAxisMappings、SortingUsesActualCommit...、MemberGripper三项；V01/06/09，HC-01/07/08 |
| 工具017协议、018 spec / plan（3份） | App.capture / poll / observe_actions及raw/clear守卫；Recipe.parents / parentClear；Simulator及RecipeSimulator独立清反馈 | test_commissioning、test_recipe；V01–V04/06/07/09，TC-01–04 |
| 工具同坐标spec / plan / position-evidence（3份） | App.axis_closures + position_satisfied；Recipe XY/axis/reuse统一最终复核，不凭last_targets授资格 | test_same_position八项；手工误差未验收允许原运动闭环清零但不授位置复用；HC-04、TC-03 |
| 工具README（1份） | 入口声明工作区新机制已实现/离线验证，1.1.6仍为历史版本标识，未制作新包 | validation、source-snapshot及artifact-index；V10，不把源码工作区称已发布 |

### 必要差异与实现补充

- 正式严格派发后Moving→Arrived及位置验收不变；工具手工严格运动路径保持误差诊断差异，配方保留1.1.4的新鲜到位+位置允许不采到Moving。完成/清零/复用证据分别记录，不互相冒充。
- 正式取料用实际StageEventStore / SortingTargetAllocator和SQLite提交；工具只有诊断流程，未实现假业务提交。正式完成返回到位身份，复用为当前Action重新取证；工具通过原API state/reason/clearError可观察。
- raw轴启动走同一托管轴路径；未关联的raw父命令登记未确认占用。人工写0/重连不会让未知父周期自动成功或授1→2；只有Recipe内同周期关联允许中间转换。没有增加恢复按钮或猜测现场恢复协议。
- 正式单Pump与既有调度不变；R/T有限计划含请求，U含父请求/双反馈。工具纯采集不推进动作/recipe，清零跨worker轮次；高精度单调读写水位和原绝对截止共同约束。
- 工具仍分读/写REAL序；正式仍单序配置。现验证使用双方一致的明确Test来源；未将工具现场布局直接替代缺失安全语义的正式Real定义。
- VirtualPlc和工具模拟器均由设备侧独立清反馈。轴新请求/复位使内部完成事实失效；父请求只在完整末段完成后清。离线延迟/保持和清写丢应答注入不新增现场PLC点或放宽安全门。


## 当前保留及后续处理

| 范围 | 本轮处置 | 后续触发与具体限制 |
| --- | --- | --- |
| [017现场地址合同](../017-confirmed-plc-addresses/contracts/confirmed-memory-layout.md)、011旧型号/报警/F轴用途 | 未修改既有投影/局部阻断；011入口已说明阶段A增量不覆盖这些内容 | 正式现场适配阶段按XLS/D01/用户决定修订；报警/复位未明处先澄清；PLC-Q2已关闭为报警只读；不新增光栅/安全门控制，不阻断独立离线设计 |
| 016公共位置、012前端、019相机 | 不修改行为或规格 | 公共Z、配方入口/隐藏来源、相机参数/混合组合分别在后续阶段承接，页面必须独立规格 |
| 原XLS/DOCX、迁移归档、客户原型 | 只读，不替用户/PLC方修订原件 | 有新确认记录到020，不用源码反推安全语义 |
| 历史validation/delivery、任务勾选及回退标签 | 保留版本化事实与未验证项 | 新验证另写，部署时另建版本关联/回退说明；本轮不打包/推送 |
| AGENTS、宪章、Spec Kit配置/版本 | 治理规则无须变更；功能指针核对正确 | 继续已有020，不初始化、升级或建重复功能 |

## 本次文档核验范围

已检查26份当前涉及的Markdown文件、142个本地链接，无本次新增或020功能内断链；旧011文档有5处历史缺失引用（013/tasks三处、011/verification-report及implementation-verification-map各一处），未补造历史文件。两路只读设计复核的预算/保存所有权/复用身份/手工判据问题已修正。

设计阶段曾核对仅文档增量；本次代码/测试/文档及证据范围见validation，功能指针与分支均为020、五个已有回退标签保留；XLS/DOCX原件SHA-256与spec基线一致。按仓库既有CRLF行尾执行`git -c core.whitespace=cr-at-eol diff --check`通过；未为检查改写全文件行尾。before/after plan钩子为空。本次正式及工具运行记录已独立保存，离线结果不作实机验收。before/after implement钩子为空。requirements按implement只读规则保留规格质量标记，其历史时点说明由validation给出当前实施结果。


## 阶段B设计同步（2026-10-08设计时点，当前实施见末节）

本节不覆盖上文A同步/验证事实。B新增stage-b-plan及RC/MC/CP/SP；spec、plan入口、research、data-model、quickstart追加当前阶段；A任务正文/勾选和证据不改，tasks.md追加独立B任务段。

| 现文档/消费者 | 当前条文或代码冲突 | B承接合同及处理时机 |
| --- | --- | --- |
| 012 contracts/recipe-authoring-api.md、shared-integration.md | 保存与批准分离，新建无资格；编辑旧批准适用范围不明 | RC-020；B-DEC-01已确认校验保存后可选运行，撤销另行授准提案；共同Admission/Freeze/API按此在tasks中承接 |
| 019 contracts/capture.md、对应plan/spec | 现v1保持参数/NotApplied，不能满足配方曝光增益消费 | CP-020；当前追加拟变更边界，实施前同步v2共享接口及所有worker/fixture，历史validation不改 |
| 017 contracts/confirmed-memory-layout.md、011 plc-communication.md | 同名投影/旧RequiredFields/Teach/ModelWords与现场冲突，F/E旧共用扫码Z | SP-020；当前设计入口，MB6056对应及MB6058无报警＝0按用户确认同步SP-020；其余条文待PLC-Q3剩余解释与PLC-Q4，不先删门 |
| 001 public-config/budget schema及配置合同、运行模式/能力/预算/StoreManifest消费者 | 仅Test/Production，旧Real/模拟组合准入不符 | MC-020；schema与代码由B tasks完整关联后变更，本轮不改机器schema文件 |
| 016公共位置、006公共页面合同、012 editor-ui | 公共Z保存/读取与XY运动差异，页面技术字段未批准 | 保留行为；公共Z只限制用途扩张。发现页面缺口时先独立前端规格，不把本表当UI授权 |
| 工具018/实际recipe与README | 成功路线为依据，不能代替正式配方/业务存储/真实七相机 | 仅同步实际受影响现场编码/轴用途与诊断，不复制正式审批/执行引擎；A当前同步不重做 |
| deployment-real-plc/plan.md、后续交付说明 | 历史包准备不等于B混合运行已就绪 | B验证通过后再更新具体发布输入；本轮不打包/安装/推送 |

当前文档新增的是未来设计的明确入口，旧实现/历史验收保持原时点。B阶段PLC待答/NotRun状态以stage-b-plan为准，不把A总勾选或019正常采集通过升级为B全链通过。


阶段B plan收尾核查：原24个阶段A源码/测试文件及3个配置hash均未改变，原254项A证据hash全部保持，28项A任务勾选未改。setup-plan确认沿用当前目录并跳过模板覆盖；before/after plan hooks为空。020文档链接已核对无断链，git按既有CRLF规则diff --check通过。本轮仅新增/追加设计文档和5份当前合同衔接，未运行构建/测试/产品、未接设备、未改现场值、未打包或推送。B待答未关闭，不能声明完整真机方案全部就绪。


2026-10-08 clarify增量：已记录1项用户答复并关闭B-DEC-01，新建/修改的软件校验保存后即可选运行，撤销逐版本人工确认/外置授准提案。spec/RC/MC/plan/research/data-model/quickstart及012接口设计入口同步，产品代码未改。requirements按clarify规则只改勾选：28/31→26/31，无新增通过；两项回退为旧文字仍称A未改码/未验证，与A已完成事实不符，不是功能退化。另3项仍因PLC安全/恢复等局部未决未满足。旧Notes与历史证据不改，不将此清单数字当软件测试结果。


历史光栅澄清的最终更正：用户“去除掉这个”删除PC启用/屏蔽控制，后续“光栅信号恢复”经用户明确解释为恢复PLC上传报警及PC只读响应。此前将恢复解释成控制需求有误，现统一纠正；PLC-Q2关闭，其他安全检查和PLC-Q3–Q4保留。A任务/验证旧OPEN描述作为当时记录保留，不改历史证据。原DOCX/XLS只读，产品未改。


历史报警方向澄清同步（无报警等级随后已补0）：用户回答A，确认D01 §2.5.1位表对应PLC→上位机MB6056；spec/FR-009、SP-020及当前计划/研究/验证指南已同步，不再询问标题或寄存器方向。仅该对应关闭，不推定无报警等级/未知位/清除或首次恢复语义。requirements逐项复核仍26/31，标记不变；其中旧“报警对应关系”文字保留原核查时点，当前状态以spec本次澄清为准。源文档、产品代码、A任务与运行证据均未改。


2026-10-08最终澄清同步：MB6058新增0＝无报警，原1/2/3不变；MB6056报警位由PLC上传、PC仅读取解析响应，Bit0光栅、Bit2安全门。删除误加的启用/屏蔽控制和点位/时序澄清，关闭PLC-Q2。同步spec/FR-009/报警表、SP-020、plan/stage-b-plan/research/quickstart、requirements及017/011设计入口。运动请求清零与PLC报警分开；原件、产品及A证据不变。

本轮clarify共记录5项答复（包含移除后恢复的先后决定）；功能范围、配方数据/交互、约束和术语已明确并同步，测试/完成条件可执行描述保持。外部PLC依赖仍局部Deferred：剩余报警位/清除、首次/复位/停机行为；不由软件猜定。requirements复核26/31→26/31，无新增勾选/回退，仍3项整体未决及2项旧A阶段时点描述不符；不是运行验证结果。可继续确定部分tasks，依赖待答部分不放行。before/after clarify hooks为空。

2026-10-08阶段B tasks增量：tasks.md新增T029–T060（32项未执行），A正文/勾选逐字节保留并加当前入口。同步spec/plan/stage-b-plan/quickstart的任务状态；当前仅文档，未创建测试源码或运行记录。共享改码前置由T029/T031检查，现场T055/T056单独保留配置/协议/执行授权门，软件验证T058不依赖硬件。下一步speckit-analyze。

2026-10-08用户授权分析整改：修正C1/I1/I2/I3。tasks T033/039/043/050–052/054/058及追溯表明确新用途验证、现场独立夹具、日志实现/验证及算法角色；同步MC/CP/SP、stage-b-plan、quickstart、research。保持T029–T060数量、编号和未执行勾选，A正文及勾选不变。复核记录见analysis-stage-b.md；本轮仅文档，产品/配置/原件/硬件未变。

## 阶段B实际同步与差异（implement，2026-10-08）

本节为当前状态，上文各次“未实施/未测试”保留当时事实，不作当前结论。确定软件任务的实施和55项正式/58项工具结果见[validation-stage-b](validation-stage-b.md)；本次对既有源码的改动不是发布版本。共享接口先由RC/MC/CP/SP及B计划/tasks承接，收尾同步所有实际消费者及验证入口。

| 文档/源码域 | 实际变化与证据 |
| --- | --- |
| 012 recipe-authoring-api/shared-integration | 新用途空Approval校验保存后可选；来源ID优先/版本/唯一匹配、ETag、共同校验和SQLite完整保存重读由RecipeCommissioningChainTests验证；Validator/Store正确部分保留，不为任务强行改写。Admission/Planner/Start/handoff/CommittedReader/IndependentRecipeApplication用途从冻结CostProfile核验，联调快照重读不访问空Simulation |
| 001 configuration-time、public-config/budget schema | 新用途及六项RecipeExecution有来源预算；ConfigurationFreezer存联调JSON/hash/source，Store兼容/维护准备/Host查询按新Profile隔离，旧Test常量/旧用途不扩大；MixedRuntime/Controlled测试 |
| 019 capture/spec/plan/tasks | 共享CP-020及wire v2、Public/Detection设置载荷/摘要、SDK原子接口、Gateway/Worker/fixture/两驱动、灯适配和事实门；实际sidecar/媒体查询沿既有序列化携带新字段，无需重复改保存器。历史无字段Unknown；SDK物理效果未验 |
| 017布局、011 plc-communication及当前020 SP | Alarm_Code已获映射确认、PLC只读、severity0不覆盖故障；现场能力集合退出旧Teach依赖；型号Float32与姿态Int16独立，F XY/E扫码Z；通信层单采样/写控制端和原清零机制保留；有来源PositionBasis/Purpose贯通但不作安全许可。Site独立TCP与A旧Test回归分列 |
| 019和001正式消费者、Host装配/能力 | AcquisitionCoordinator和RecipeDetectionExecutor传冻结参数并只等待一次；PythonWorkerAdapter提供实际能力声明，独立CommissioningAlgorithm消费当前媒体和有来源结果，Production不接受隐式模拟；七机绑定与公共3D/F角色核对。通过同一业务端口，后续真实算法/光源可替换提供者，仍须验证真实实现能力及参数合同 |
| tools/plc-commissioning app.py/README与工具合同 | 本轮B仅新增已知报警解释/等级0及只读测试，既有REAL/INT、F XY/E扫码Z与A清零/同坐标已实现，不复制正式执行器。工具保持自身手工坐标报告/1.1.4配方验收、直接loopback和诊断；没有正式SQLite/七机/算法混合链，工具成功不代替正式通过 |
| 020 spec/plan/stage-b-plan/tasks/quickstart/contracts | 修正当前“任务未生成/全部未执行/软件NotRun”，阶段A正文与历史证据保持；字段对账、失败持久日志、源码和配置哈希单列B。requirements只读26/31 |

当前仓库001/011/012/017只保留部分contracts/checklists等资产，根部spec/plan/tasks文件不存在；不为同步生成重复旧功能，现有合同及020任务作为当前入口。019现有spec/plan/tasks已追加共享增量，旧勾选与历史验证不改。公共Z、前端/客户原型和已有示教/公共位置功能未扩张或删除。

现场T055/T056、历史SDK阻塞/物理断线、成员抓手机械切换及现场编码独立核验仍未验。交付还需要正式提交号、现场配置/摘要、适用验收、数据兼容及回退记录和包/GitHub说明；不由软件测试自动授予打包部署权限。

## Phase 8实际同步（implement，2026-10-08）

本轮仅修改4个测试文件：RecipeCommissioningChainTests（正式API保存坐标/单张曝光版本B）、CameraBusinessRegressionTests（冻结后编辑钩子及实际移动/采集参数断言）、CommissioningWorkflowTests（人工确认API、SQLite终态重读）、ControlledCommissioningTests（新用途正式Start两种阻断与持久诊断）。测试输出转到evidence/phase8，阶段A/B历史证据未改。没有产品缺陷修复或共享接口变更，正式产品与联调工具本轮均未改；原同步范围及差异继续适用，工具58项未重跑。

同步020 spec/plan/stage-b-plan/tasks/quickstart/validation-stage-b，并新增[validation-phase8](validation-phase8.md)、[frontend-handoff](frontend-handoff.md)及Phase 8源码/证据清单。T061只交接，后续独立前端spec/contracts/plan/tasks和实施/验收仍待承接；020不新增页面实现任务。最终12项通过、Host构建通过，增量失败及修正如实保留。requirements、原型、frontend/desktop、原件、五个标签及旧证据经哈希核对不变；T001–T060勾选未动，T055/T056仍Blocked。未连接硬件、打包、部署、提交或推送。
