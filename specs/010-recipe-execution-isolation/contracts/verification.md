# 010 验证与测试迁移合同

状态：仅设计，全部运行项未执行。此文是固定专项集合及证据合同，不是测试报告或tasks。落实FR-011—018；同一用例可承接多项义务，不能以减少运行次数省去独立义务。

## VG-01 接入现有验证入口

计划新增固定profile RecipeExecution010：

    ./scripts/verify.ps1 -Profile RecipeExecution010

现脚本仅支持WaitSeconds，上述参数尚不存在。实施时该参数传到现verify_entry.py，再到runner.run_verify；010工作流的verify阶段明确调用同一profile。010结束后所有项目验收profile（含默认009及后续其他功能）均先执行下述轻量集合L，再执行各自原必需集合；保留009原有效保护与结论范围，不将其他profile升级为010或009完整动态验收。禁止生成无人调用的独立检查脚本。该入口只执行验证，不启动Spec Kit后续阶段。

复用当前工作区锁、每轮独立目录、源码/构建身份、TRX与required ledger、migration_audit及Rules的Roslyn和既有脚本解析设施。010profile构建受影响工程、执行下表固定集合，不沿用009默认solution＋多suite全集。调用方不能用外部filter减少必需项；runner根据审阅后的固定清单生成内部精确筛选，并核实际发现/执行。

计划证据目录：artifacts/recipe-execution-010/{verificationRunId}/。必需集合在运行前由本合同与实施影响差异编制，至少有caseId、method/FQN、dataRowId、obligationIds、evidenceKinds、source/build/input/manifest摘要及预期结果。不能从这次发现的测试反推“应当执行”。同义承接可以减少case，但迁移表中每个独立义务都有确定承接关系。

### VG-01.1 持续轻量集合L与最终拒绝点（计划新增）

选择**统一项目验收入口每轮无条件运行L**。不用Git差异、功能编号或调用者的Role分类决定是否触发，不新建变更检测框架。每轮按VG-02重新枚举实际职责/Compile/链接源及相关合同、装配、测试helpers和验证脚本，记录当前内容摘要；比较的是本次构建前后及判定时的快照，不需要历史Git基线。新增、移动、重分类或枚举失败仍受VG-02保护。单独dotnet test/脚本调试只产生局部证据，不产生项目验收通过。

| 层次 | 固定内容 | 不得冒充的证明 |
| --- | --- | --- |
| L：持续轻量必需集 | 当前职责闭包的B01—B05（B04为正式组合根静态注册/依赖核查），同检查器全部N/P样本的静态部分，G01—G07账本/解析数据样本及C01—C03调用完整性样本；受影响009通信静态边界。构建执行这些检查所需Rules及其必要依赖，记录当前构建身份 | 不启动Host/PLC/Worker/数据库或整盘；N04的静态错误注册拒绝不能替代真实Host调用；不证明共同动态主链通过 |
| 010完整专项 | V00—V07，含L，以及V01真实Host两组环境/缺能力、V02必要组件、V03/04实际分支、V05完整基线、V06冻结替换和V07收口 | 仅证明所选路线及本次义务，不作009整体或生产验收 |

接入复用现有控制流，具体责任如下；所列新凭证校验为计划增量，不声称当前脚本已经支持：

1. `scripts/verify.ps1 → scripts/workflow/verify_entry.py → runner.run_verify/_run_verify` 与 `workflows/auto-dev.yml → step.ps1 → runner.step('verify') → run_verify` 共用同一L执行函数。profile只选择其余必需集，不提供跳过L或外部缩减L的参数。010的V01直接引用本轮L结果，不重复运行同样样本。
2. L由runner当前验证attempt执行并生成结构化凭证：request/verificationAttemptId、profile、开始/结束时间、职责闭包/源码及相关合同摘要、Rules构建摘要、L清单/检查器版本摘要、每case/dataRow实际发现/执行结果与原始报告摘要。凭证记录在现attempt证据目录及verification结果，不另设证据平台。未知/漏枚举或构建期间源码改变直接失败；不接受调用者预填Passed或仅给文件路径。
3. `_run_verify`的总`passed`必须同时满足L凭证校验和当前profile的全部必需项；`verify_entry.main`继续据此输出PASS/FAIL及退出码。即使其他profile的测试全绿，L遗漏、Skip、解析失败、源码/构建不符或旧attempt报告也返回FAIL。
4. 工作流`runner.step('assess')`和`step('finish')`在现有verification/review/source检查外，调用同一凭证核验函数，从当前control指向的验证attempt读取原始ledger/TRX并核摘要。不能只信`verification.passed=true`；缺L或旧L使assessment失败、finish拒绝写完成结论。现runner正常退出码并不等于验收通过，此拒绝必须进入结构化总结果。后续新增验收profile同样走该公共判定，不能以改profile名、工作流或目录绕过。
5. 最终聚合T可以引用其B/S/替换子运行的原始证据，但须核每个真实验证attempt的当前关联凭证；本轮L与V01静态义务可共用同一case/attempt，不为了V07重跑同样静态样本。未经过统一入口的局部测试日志只标Debug/Partial，不能填项目Passed。
6. 现`verify-009-protocol-isolation.ps1`的旁接聚合入口也必须消费同一L：`boundary_minimum.main`在写ledger/result与成功返回前核验；`protocol_isolation.main`在`component/subsetPassed`及返回成功前核验，即使指定case/fixture而没有调用verify_entry也不例外。两者为本轮上下文直接调用同一L执行/校验函数；若由统一runner委托，只能复用同一父attempt/profile/摘要下已核验的L，不能用旧报告补齐。`SelectedCasesOnly`保持子集范围、`overall009Passed=false`不变。脚本单项退出成功不能被上层升级为未经这些最终拒绝点的项目通过。

| 必需样例 | 最小输入与预期 | 证据层级 |
| --- | --- | --- |
| C01 其他profile仍触发 | 默认009、一个非010profile、BoundaryMinimum及SelectedCasesOnly四个现入口/范围参数行均必选L；相关业务helper/合同或装配的N02/N04违规样本使原profile其他结果Passed仍总拒绝 | runner及现旁接聚合调度组件＋同检查器样本；不跑009动态全集 |
| C02 缺失及旧凭证拒绝 | 删除本轮L调用/结果；换旧attempt凭证（含旧source或旧manifest）；在_run_verify、assess、finish及上述两个旁接聚合最终点核拒绝，既有passed=true不能抵消；旧case行改写当前身份而没有VG-04引用证明亦拒绝 | 复用G01/G05和同一个凭证验证函数；新增的旧行冒充只是同类账本数据，不建立第二判定器 |
| C03 当前完整允许 | 非010profile的合法当前源码/完整L凭证和本profile必需结果满足时允许；010聚合对同case/attempt共用不误报漏执行；按VG-04保留原身份且有合法范围/依赖不变证明的跨attempt引用可接受 | 复用G07增加一个合法引用数据项；不启动动态profile或另建报告判定器 |

## VG-02 职责覆盖与有限检查器

检查根：正式RunEndpoints/StartPublicPreparation、F绑定/移交、共同目录校验/规划/坐标/预算/检测/聚合/整盘保存、IDetectionPort实际实现及Host装配。范围包括其直接/间接业务helpers、所有实际Compile与链接源、相关公共合同、正式启动/验证脚本及helpers。

在现Roslyn设施内增加有限规则，沿调用/类型引用形成上述职责闭包；目录或Role登记不能解除保护。引用叶端口的设备协议/图片/Worker实现可在明确适配边界停止业务扩展，但“适配器”若决定工序、聚合或保存门禁仍属共同业务。方法/类型移到其他工程或改名时重新核项目/Compile/调用闭包；根消失、解析不全、未分类源或未知动态注册均使本次检查受限失败，不能产生零违规。

| 规则 | 可执行判据 |
| --- | --- |
| B01 实现/格式依赖 | 共同职责符号不能依赖具体模拟类；输入解析类型/字段及测试用例ID流入动作选择、必检、目标或结果时拒绝。检查实际符号、字面字段访问及可达helper；不因未出现Test字样放行 |
| B02 环境控制工序 | 来源/模式/测试身份及返回这些判定的helper，若控制检测/换面/后段/保存义务分支则拒绝，包括条件内没有工序调用、却通过正常提前返回跳过条件后的必要调用。复用bool helper/标量别名的符号传播，在相关方法CFG比较两后继到必要调用/正常结束的路径；明确throw或既有拒绝合同不作为正常成功。限定本链有限控制流，不建设任意程序分析平台 |
| B03 公共形状 | 递归核对参与本链请求/计划/目标/结果类型；fixture JSON、测试路径、Worker启动详情或嵌套包装逃逸拒绝。语义值、批准、真实来源及不透明证据引用允许 |
| B04 正式装配 | 组合根只注册同一共同检测职责实现；环境不得注册整段模拟结果服务。静态DI检查结合两组合法环境及缺能力的Host实际装配/调用代表 |
| B05 覆盖防绕过 | 闭包与登记不一致拒绝；移动N01/N02 helper、改Role为adapter或移至链接工程仍受同规则约束。被保护根/辅助代码未枚举、动态注册无法解析均不得通过 |

来源值只写日志/事实、准入只明确拒绝依赖动作时可通过；若由准入策略返回“下一步”或另一整段执行器则拒绝。脚本用现解析设施检查准备/启动/观察/授权操作和验证调用链；不得直接续接工序、写完成状态或缩减必需集合。未知动态代码不被当作已检查，本次没有为其建设通用平台的需求。

009既有协议边界检查继续作用于受影响业务、公共合同与正式装配；Wire测试可在通信fixture中使用协议断言，伴随共同业务helpers仍归010保护范围。

## VG-03 同一检查器的架构正负例

样例在测试输入/隔离样本中故意引入，调用与正式源码完全相同的检查函数。不能给检查器一个“这是负例”的特判标签。每例核具体拒绝码、位置/符号及结果；不能仅断言有任意错误。

| Case | 故意错误 / 合法样本 | 必需结果 |
| --- | --- | --- |
| N01a | 共同执行直接调用具体模拟算法/设备实现 | B01拒绝 |
| N01b | 用固定配方/测试编号决定步骤或默认结果 | B01拒绝 |
| N01c | 业务或间接helper解析resolvedDetectionTargetsByFace等fixture字段；换成无Test字样的等价字段也成立 | B01拒绝 |
| N02 | 用环境模式/实际来源或其helper结果决定是否执行检测或续接 | B02拒绝 |
| N03a | 公共动作请求增加TestSourceReference、Worker启动路径 | B03拒绝 |
| N03b | 将fixture JsonElement/字典藏入嵌套DTO，业务继续解释 | B03拒绝 |
| N04 | 图片/Worker缺失时正式DI回退整段模拟OK，或另一环境换整段执行器 | B04静态及装配代表拒绝 |
| N05a | 把N01c helper移至Infrastructure其他路径或链接工程，调用不变 | 仍B01/B05拒绝 |
| N05b | 把N02 helper登记为adapter或从显式分类表移除 | 仍B02/B05拒绝，漏分类本身失败 |
| P01 | 真实Test/Virtual/Simulated/Real事实、不透明来源引用及Unknown，不据此排工序 | 允许；Unknown不伪造成可信依据 |
| P02 | 固定图片/模拟设备/测试算法内部解析素材、模拟并报告真实事实，共同主链不变 | 允许 |
| P03 | Test/Production批准、能力/预算/坐标/标定准入，未批准明确拒绝 | 允许；不能回退成功或换业务链 |
| N02-return-direct | 环境条件直接正常return，工序调用在if外 | 编译及符号绑定有效后，由同一正式Check报告B02，位置为环境条件 |
| N02-return-helper-alias | bool helper判断环境，局部别名控制正常return，后续检测被跳过 | 同上；不是按名称、路径、Role或样本文本特判 |
| P-return-throw / P-return-rejected-result | 环境准入失败明确throw，或返回既有ThreeDAndFRecipeGateDecision(CanLoadAndBind=false, MustLockAndStop=true)；之后仍有正常检测路径 | 允许；按实际构造类型/标志确认拒绝，任意正常返回值不等于拒绝 |
| P-return-origin-log / P-return-business | 来源仅记录日志；完成等正常业务条件提前return | 允许，保留P01—P03原合法义务 |

2026-10-03 B02修补：以上六个精确sample数据行追加到`scripts/workflow/010-lightweight-cases.json`固定L，原64项不变，总70项。正式`run_lightweight`消费该清单并执行同一RecipeExecutionBoundaryChecker；`validate_bundle`重读发现/TRX，`final_gate`核最终通过。新增负例行missing或NotExecuted必须拒绝，即使隔离故障输入仍声明Passed。故障副本仅证明判定器拒绝，不是业务运行证据；原L原始报告保持不变。


以上覆盖AC-10五类及AC-11三类，子项体现独立失效模式而非增加新的业务需求。

## VG-04 执行完整性门禁

复用required ledger，补足当前仅遍历expected而可能忽略额外TRX-PARSE错误的缺口。源码扫描器存在、枚举非空、全部解析成功、分类完整、manifest合法及每份结果解析成功都是独立必需项；任意解析错误使总结果失败。

每个必需case核发现且实际执行、Outcome=Passed、当前run/source/build/input/manifest一致、证据存在且摘要对应；TRX开始时间不早于本轮开始，构建前后源码匹配。Skipped、NotExecuted、零发现、过滤导致缺行、重复/无法唯一识别数据行均不算通过。门禁对旧报告不作“最新文件”猜测，按身份与时间核对。

这里的“本轮/当前”首先按**原子执行attempt**核验：一个TRX/ledger必须匹配它真正发生的run/source/build/input/manifest及时间。T是对B/S/E原子证据的聚合引用，不把它们重写成同一个run或同一次输入；同一T内顺序发生的基线/替换自然保留各自身份。默认拒绝任意旧报告直接充当当前执行。

仅VG-05.1修复后明确未受影响的B义务可申请原证据复用：当前B另记义务ID、原attempt/case/dataRow/run及证据摘要、原受测源/测试/helpers/合同/输入/oracle/必要构建依赖闭包，与当前对应范围内容摘要一致的核对及独立影响审阅依据。原证据须先通过其原上下文全部发现/执行/解析/身份校验；不能修改旧行、时间或来源。依赖闭包不完整、任何有关摘要变化或无法证明无影响则重跑该B项。当前L、实际修改义务、失效冻结下的E/S及旧替换结论均不得这样复用。C02/C03分别证明旧行冒充当前被拒和有完整证明的原身份引用被接受；不放宽G05。旧007/008/009报告仍仅保留原结论，不凭此条纳入本次必需集合。

| 完整性样例 | 故意变造 | 结果 |
| --- | --- | --- |
| G01 | 删除manifest、扫描器或其中一个必需case | 拒绝缺失 |
| G02 | case零发现；或发现后未执行 | 两个独立数据项均拒绝 |
| G03 | 必需case变Skip；外部/内部filter漏掉必需行 | 两个独立数据项均拒绝 |
| G04 | TRX/脚本/源码/manifest解析失败（含额外TRX-PARSE Error行） | 每类实际适用解析链拒绝，不被其他Passed抵消 |
| G05 | 沿用旧时间、旧run、旧source、旧build、旧input或旧manifest | 各身份维度不符均拒绝 |
| G06 | 义务迁移漏数据行、源文件移走/改Role缩减冻结范围、缺证据或重复case | 分别拒绝 |
| G07 | 完整、当前、可关联且全部执行的最小报告 | 允许通过 |

完整性测试调用生产runner同一函数，不维护测试专用判定器。G样例是账本/解析的低成本数据测试，不重复启动整盘。

## VG-05 最小验证集合

本次设计涉及共同输入、采集/算法事实、全部现检测编排和分拣目标；因此以下差异不能用普通单面代替。若实施没有修改某保护实现，可由同一已执行代表承接而避免重复，必须明确义务映射；不能在执行后删除失败的必需项。

| 集合 | 固定覆盖义务及最小代表 | 证据与限制 |
| --- | --- | --- |
| V00 受影响构建 | Host及其Domain/Application/Infrastructure/Plc.Protocol依赖；受影响Contracts/Rules/Integration测试工程；主链所需现虚拟PLC工程/Worker输入检查 | 当前源/SDK/构建摘要、输出/日志。未改Communication测试工程不全跑；引用变化时构建它。无全solution验收要求 |
| V01 边界与装配 | 本轮L（B/N/P/G/C与受影响009静态边界）；同一正式执行器的两组合法环境、缺能力拒绝 | L只执行一次；源文件/符号/Role清单、同检查器结果、真实DI与实际调用、逐必需项账本 |
| V02 共同规则与保存保护 | 下表语义输入/规划/测量/聚合、F、移交、预算/期限、Pending/未知/分拣目标及保存门禁 | 低成本组件/控制时钟，可多义务共用case；不能计独立进程或真实SQLite主链 |
| V03 实际检测组件 | AB与CD各一个；既有WrongArrival/MediaSave/Reset/SaveWindowExpiry四个必要失败数据项；未知F、无效算法结果按受影响入口各一代表 | 实际端口/Worker、媒体/SQLite、身份/来源及失败日志。CD正常可由V05承接，避免再跑同义组件 |
| V04 受影响工艺差异 | 普通双面自动换面一例；组mixed且保留独立成员/目标一例；整体人工换面＋E一例；整体NG/Pending优先与一次搬运一例；特殊旋转一例 | 执行实际共同业务，可从合法已提交输入开始的组件，不必各跑公共3D/F整盘。旋转其余出口/错姿态、E缺码/错误由共同组件数据项承接；四面组目标/步骤至少共同组件实际执行，不能只查计划 |
| V05 完整单配方 | 一个合法Q02/CD/P01,P03代表，经现正式入口、独立虚拟PLC/Worker、实际存储，至授权取盘及FinalUnloadCompleted | 当前独立运行、全身份/必要动作/保存/日志。既有AwaitingManualRemoval测试须补终点，进程内PLC不能替独立进程 |
| V06 冻结替换 | B内V05修复后基线；一次组合等价替换完整链E；预先指定S的整体NG/Pending组件共享V04/V06结果变化义务 | 业务/合同/oracle冻结、环境差异、真实调用及提交比较；五类边界全覆盖，见VG-07 |
| V07 收口 | 逐方法/数据义务迁移、已确认删除、保留历史/旧失败、必需执行完整性 | 100%必需义务有当前有效证据，旧报告仅原范围；部分不能标总通过 |

最少完整正常整盘为基线一次及等价替换一次；V04的合法结果变化在冻结后执行可复用，不增加第三条同义完整正常盘。各失败/受影响分支用最小层级承接，不要求所有配方/页面/设备/全故障矩阵。若代表实际失败，只能如实失败并修复必要依赖，不改变oracle或预算迁就。

### VG-05.1 B/S/T的唯一执行顺序

V编号表示覆盖义务，不是八次相互独立的运行；B/S/T按下表安排实际case/run。独立业务预期、完整清单、替代输入/实现与S输入在B之前确定，不能从B实际结果倒填。顺序为：准备与审阅 → B通过 → 冻结 → E等价替换及S → V07核T。

| 集合 | 明确成员与时点 | 通过和去重规则 |
| --- | --- | --- |
| B：冻结前修复基线 | V00、V01、V02、V03、V05，以及V04除S以外的全部必需代表；CD同义组件可由V05承担。普通双面、组mixed/四面组件、整体人工/E、旋转及其必要独立数据项均仍在B | 全部有修复后有效证据才冻结；S已准备不等于S已执行，但S执行不构成B前置。V05是第一条完整正常主链 |
| S：冻结后共享代表 | VG-06 `RecipeMultiObjectIntegrationTests.AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce`对应整体NG＋Pending明细/实体一次搬运，使用合法已提交输入执行共同组件 | 输入、预期和对应case/数据身份在B前登记；冻结后执行一次，同时承接V04这一行和V06合法结果变化。不另强制一条完整盘，也不把V04其他分支整体推后 |
| E：冻结后等价替换 | V06五类组合替换的完整正式主链 | 第二条完整正常主链，符合VG-07，不能以S结果变化组件替代 |
| T：最终完整集合 | B ∪ S ∪ E ∪ V07收口，覆盖V00—V07全部必需义务 | 按verificationAttemptId/caseId/dataRowId/实际runId及obligationIds映射去重；一次执行可承接多项，不因挂两个V编号重跑；任何最终必需项失败、缺失或未执行，T总失败 |

V07是对前述集合、清理和完整性的收口判定，不再以“T已通过”作为自己的输入，避免循环。计划中的S固定选整体NG/Pending，不再将另一个超时整盘隐含列为同等必需；若实施实际改变Worker超时处理，其已有独立义务仍按VG-06在B承接。选择代表的必要变动只能在执行B之前经影响/义务登记，不能事后换掉失败项。

冻结后业务、相关合同或独立预期变化，立即废止旧替换结论；重新确定影响、重建受影响B证据并形成新完整B/冻结身份，再执行新冻结后的E和S。仅未受影响B证据可按VG-04显式引用原case/run与摘要和独立影响说明；无法证明不受影响就重取相应基线，不能把旧报告改成当前运行。必需集合变动同样重核B/S/T覆盖。

## VG-06 受影响测试义务迁移登记

路径别名：CT=backend/tests/Gaode.Contracts.Tests；IT=backend/tests/Gaode.Integration.Tests。既有迁移行的方法指当前源码，显式标注的NF计划新增项除外；目标V编号是计划验证，不声称已迁移。分类“保留/迁移/替换/删除”针对断言义务，不能因一个方法包含坏断言整文件删除。

独立依据：

| 依据 | 来源 |
| --- | --- |
| B1 | 有效需求V1.1 RCP-001/002/003/005/008、§11.2/11.3；008执行合同与E01—E06/USR-E |
| B2 | POS-001/002/003、ALG-013、§11.5—11.8；008有效测量、面和实体身份规则 |
| B3 | 001采集算法/保存移交、003移交/检测合同的身份及真实提交义务 |
| B4 | SRT-001—005/007—009、DAT-007；008执行/USR-D的质量、实体、分拣及最终完成 |
| B5 | 003有限重试；009 business-device的反馈、取消、期限与未知状态 |
| B6 | 007正式采集、独立Worker、真实媒体/存储及来源义务 |
| B7 | 010共同边界、测试纠正、删除及冻结替换的明确要求 |
| B8 | 009保留的配方应用有限总窗/三入口/真实回执规则，不重开其整体验收 |
| B9 | [003组件来源矩阵](../../003-plc-latest-protocol/contracts/component-source-matrix.md)的当前009联合闭合及Host汇总条款：实际生产者版本、混合来源、Unknown不得补值、Ready/Final不可变、原子保存、Test人工渠道真实性 |
| B10 | 001采集算法合同的Ended/媒体双条件、首个owned buffer和当前关联；010 IB-04/CE-03.4实际采集事实、F Origin关联保存；不把请求当实际应用 |
| B11 | 008 api-results/evidence及006 API有效消费者：已提交事实投影、请求/实际分开、质量/完整性/终态分开、历史身份不得补造 |

### 输入、规划、目标和聚合

| 原文件 / 方法 | 原义务及独立数据项 | 分类 / 依据 | 承接及最小执行 |
| --- | --- | --- | --- |
| CT/Capabilities/TestTrayCodePolicyTests.cs / FixtureFCodeResolvesToReviewCatalogCode | TEST-TRAY-0001的配置映射 | 迁移B1/B7；删除共同业务固定ID断言 | V02解析边界保留映射与原码，F业务用语义引用 |
| 同 / OtherFixtureCodesRemainStable | TEST-TRAY-0042不错误重写 | 迁移B1/B7 | V02解析边界一项 |
| CT/Station01/FScanStepTests.cs / FUsesOneTriggerOneImageAndDoesNotPerformRecipeOperation | 一触发一图/调用/意图，F不预绑定 | 保留B1/B3，迁移固定码 | V02一次F组件，并核实际算法Origin随当前CallId保存；未知不从WorkerSession/预期版本补造 |
| 同 / FailedAlgorithmFactSaveDoesNotSubmitFCompletion | 必要保存失败不提交F完成 | 保留B3/B4 | V02一个失败 |
| CT/Recipes/RecipeRunPlannerTests.cs / SameFrozenInputsCreateStableOrderedPlan | 稳定顺序/版本/摘要 | 保留并补独立预期B1/B7 | V02；两次规划器相等只证确定性 |
| 同 / DuplicateUnknownOrEmptyOccupiedSlotsAreRejected | 重复P01、UNKNOWN、空集合 | 保留B1/B2 | V02三个语义数据项 |
| CT/Recipes/RecipeCatalogTests.cs / Q09IncompleteOrMisboundFaceCannotBecomeExecutable | missing-camera、wrong-source-slot、cross-face | 保留共同校验/迁移JSON B1/B2 | V02三个共同拒绝＋解析映射 |
| 同 / VirtualMultiFaceCatalogsHaveCompleteFrozenTargetsAndWorkload | 11路线、工作量、无重扫及文件交叉引用 | 保留业务/迁移文件B1/B2/B6 | V02低成本11行，见下方数据表 |
| 同 / HistoricalOutOfScopeCatalogRemainsReadableButCannotDispatch | 8个四面范围外条目可读不可派发 | 保留B1 | V02八行，不跑八盘 |
| 同 / Q03TwoFaceFixtureUsesInitialHeightAndExplicitFaceTargets | 4采集2融合1翻0重扫、初始测量和面/轮 | 保留B1/B2，迁移JSON变造 | V02目标/计划，与V04双面承接 |
| 同 / Q01TestCatalogAllowsOnlyMappedSlot | P01获批、P02未获批 | 迁移批准边界B1/B7 | V02准入两项，规划器无TestEligibleSlots |
| 同 / Q02MappedCatalogKeepsCdBatchOrderAndNoncontiguousSlots | C:P01,C:P03,D:P01,D:P03；物理槽1/3 | 保留B1/B2 | V02独立顺序＋V05 |
| 同 / Q01ParamUsesSameRecipeWithChangedCaptureAndTwoSlots | AB两槽、曝光12000/亮度75/模型版本、摘要变化；P02未批 | 拆分保留冻结/迁移格式与批准B1/B6/B7 | V02＋V03实际请求事实，不误称SDK应用 |
| 同 / ReviewCatalogLoadsAllVirtualRecipesAndAssignsStablePlcIds | 0.4文件68项、来源及不可执行、显示ID | 迁移文件项/保留受限B1/B7 | V02读取样本及一未批准拒绝；业务不固定68/ID |
| 同 / PlannerBindsRecipeOnlyFromPostFCodeAndCarriesPlcId | F后唯一、版本和显示标识 | 保留B1 | V02语义F引用；字面格式移边界 |
| 同 / FileProviderSwitchKeepsTheSameRecipeContract | 两字段模式、容量9/8 | 迁移解码/保留共同结果B1/B7 | V02两输入调用同校验；真实替换另由V06 |
| CT/Recipes/RecipeExecutionCoordinatorTests.cs / Q01MappedPlanHasCompleteSingleFaceSteps | 单面必检完整 | 保留B1/B2 | V02 |
| 同 / CaptureMustFollowMatchingPositionAndUnknownStepsCannotBeSkipped | 相机不匹配、未知步骤999 | 保留B2 | V02两个语义负例 |
| 同 / Q01DeadlinesAreAbsoluteAndCountCaptureFusionMotionAndSaves | 2采集1融合0重扫；108s/41s/56100ms属于该批准输入 | 保留核算/迁移测试成本B5/B7 | V02独立预算常量；语义额度绑定冻结BudgetRef/Digest，核新旧三阶段起点/截止一致，共同公式无I/O计数，不作生产默认 |
| 同 / Q03BudgetIncludesBothFacesAndOneFlipWithoutRescan | 4采集2融合1翻0重扫、原起点 | 保留B1/B5 | V02双面预算 |
| 同 / Q03TwoSlotsAllowConsecutiveEntityFlipsBeforeNextFaceCapture | 两实体各翻一次后次面、不重扫 | 保留B1/B2 | V02计划＋V04动作 |
| 同 / StrictRecipeRequestRejectsLegacyPlaceholderPosition | 严格拒绝但非严格接受占位 | 替换错误接受断言B2/B7 | V02共同请求一律拒占位；删除开关 |
| CT/Recipes/PublicPreparationTargetResolutionTests.cs / Q03HandoffResolvesBothFacesFromInitialMeasurement | 10.5＋偏置得110.5/112.5，轮次1 | 保留B2/B3 | V02共同resolver |
| 同 / Q03FutureTargetRejectsWrongIdentity | localFace=1、protocolSlotIndex=2 | 保留身份/迁移字段B2 | V02语义面/物理槽错配两项 |
| 同 / TestTargetsUseMatchingCurrentThreeDHeightAndRejectWrongSample | 两测量组、wrong-scope、missing sample-b及固定来源 | 保留测量/替换来源白名单B2/B7 | V02下表数据项＋V06合法来源替换 |
| 同 / CommittedHandoffResolvesExplicitFrozenFixedTargetsWithoutDefaultHeight | 获批Z110/115、缺目标拒绝 | 保留/迁移格式B2/B3/B7 | V02固定依据正例与缺目标负例 |
| CT/Recipes/FaceResultAggregatorTests.cs / AbAndCdPairOnlyWithinSameObjectFaceAndHeightRound | AB/CD，同对象/面/轮、未齐、重复A | 保留B2 | V02一次聚合数据集，预期独立 |

| 数据所属方法 | 必须独立登记的数据行 |
| --- | --- |
| VirtualMultiFaceCatalogsHaveCompleteFrozenTargetsAndWorkload | Q04=AB,CD；Q05=CD,AB；Q06=CD,CD；Q08=AB,AB,AB,CD；Q09=AB,AB,CD,AB；Q11=AB,CD,AB,AB；Q14=AB,CD,CD,CD；Q15=CD,AB,AB,AB；Q18=CD,AB,CD,CD；Q20=CD,CD,AB,CD；Q21=CD,CD,CD,AB |
| HistoricalOutOfScopeCatalogRemainsReadableButCannotDispatch | q07/q10/q12/q13/q16/q17/q19/q22分别可读且拒FourFaceBusinessScopeExcluded |
| TestTargetsUseMatchingCurrentThreeDHeightAndRejectWrongSample | (10.5,11.0)→Z(110.5,111.0,110.5,111.0)；(11.5,10.25)→(111.5,110.25,111.5,110.25)；wrong-scope；missing sample-b |

### 移交、配方应用及有限窗口

| 原文件 / 方法 | 原义务 / 独立数据项 | 分类 / 依据 | 承接及最小范围 |
| --- | --- | --- | --- |
| CT/Station01/PublicPreparationHandoffV2Tests.cs / OnlyPersistedMatchingV2HandoffCanConstructDetectionRequest | 同run/tray/plan、媒体及实际已提交v2 | 保留、更换占位输入B3 | V02合法typed输入＋V05真实保存 |
| 同 / InMemoryNotificationCannotReplaceCommittedHandoff | 内存通知不是提交 | 保留B3 | V02拒绝 |
| 同 / EmptyReferencesOrMissingCommittedWriteCannotProduceRequest | 空引用、缺Write两项 | 保留B3 | V02两项 |
| 同 / WrongRunTrayOrPlanIsRejected | 错run/tray/plan三项 | 保留B3 | V02三项 |
| IT/Station01/PublicPreparationHandoffV2IntegrationTests.cs / DetectionCanOnlyStartFromCommittedMatchingV2Handoff | 真实移交消费及无移交/无当前回执拒绝；另含固定Simulated、SourcePolicy等值及prebuilt非严格正例 | 拆分保留/替换B3/B7/B8/B9/B10 | 按下方SRC-05a—c逐断言承接：V02拒绝、V05真实提交/来源；不再额外重跑其整盘部分 |
| CT/Station01/RecipeApplicationContractTests.cs / RunCancellationClosesRegisteredBindingTokenWithoutTurningPauseIntoCancellation | 暂停与取消不同 | 保留B5/B8 | V02控制时钟/资格组件 |
| 同 / DeviceConfirmationWithoutActualEvidenceReceiptCannotAuthorizeBinding | 无真实证据回执不授权 | 保留B8 | V02缺证据拒绝 |
| 同 / EarlyPlatformTimerWakeDoesNotCloseOriginalBindingWindow | 9999ms早醒不关10000ms窗口 | 保留B8 | 若时间实现不改由原保留义务登记；预算迁移的窗口代表承接，不增时钟矩阵 |
| 同 / BothCapacityInputsKeepTrayIdentityAndAllNecessarySavesInOneWindow | capacity=true/false，同身份及意图→bound→handoff→receipt | 保留B8 | V02两项；RequiredLegacyHandoff只是测试回调标签 |
| 同 / FailedIntentNeverCallsDeviceOrRegistersTotalWindow | 意图失败无设备调用/t0 | 保留B8 | V02一个失败 |
| 同 / LateOrCancelledDeviceReturnDoesNotCreateBoundOrHandoff | cancelled=false迟到/true取消 | 保留B8 | V02两项 |
| 同 / ExpiredExistingDeadlineRejectsBeforeDeviceAndDoesNotRefreshIt | strict-before-port原期限已到 | 保留B8 | V02；旧GAODE_009_EVIDENCE_ROOT仅指定本轮独立子目录，报告关联010，不读旧009证据 |
| 同 / TimelyRequiredReceiptsRemainValidAfterLaterContinuation | 按时完成不被稍后消费误判 | 保留B8 | V02一个代表 |
| 同 / DeviceConfirmationAloneCannotCompleteWhenRequiredSaveFails | 设备/bound不能替必要handoff保存 | 保留B8 | V02保存失败；V05真实存储 |
| 同 / LastRequiredReceiptUsesOriginalTotalWindow | 9999=true、10000=false、10001=false | 保留B8 | V02三数据项，不声称TCP/SQLite |
| 同 / EarlierDownstreamDeadlineClosesNecessarySaveWithoutRefreshingAnyDeadline | 更早2000ms关窗 | 保留B8 | V02一个代表 |
| 同 / CancellationAfterBoundCommitCannotStartRequiredHandoffSave | bound后取消不得开新handoff保存 | 保留B8 | V02一个代表 |
| scripts/run-009-process-case.ps1 / BA06-downstream legacy-PROCESS、api-none-PROCESS | v1起点与独立入口不造下游窗口 | 保留B8，删除非严格输入依赖 | V02三个入口期限关系承接；不重开009全进程矩阵 |

“保留”不等于所有旧case均必须单独重跑。V02账本应将同义断言映射到实际必需代表；有独立数据项的义务不能仅登记方法名。若直接修改时间判定而不仅注入同值预算，EarlyPlatformTimerWake等直接受影响方法纳入固定集合并在运行前更新登记。

### 模拟整段、重试及分拣

| 原文件 / 方法 | 原义务 / 数据项 | 分类 / 依据 | 承接及最小范围 |
| --- | --- | --- | --- |
| CT/Simulation/DetectionAdapterSourceTests.cs / SimulatedAdapterPreservesInputOutputVersionAndEvidenceWithoutTouchingPlc | 整段生成OK及版本/来源 | 删除错误成功断言、迁移有效事实B6/B7 | V03/05真实适配/共同调用承接，删除旧服务测试 |
| 同 / ProductionRequestIsExplicitlyRejectedWithoutSimulatedSuccess | 生产不得模拟兜底 | 迁移后删除旧测试B7 | V01/02正式准入 |
| 同 / MissingExpectedObjectsDoesNotFabricateResults | 缺对象无造结果 | 迁移后删除旧测试B2/B7 | V02共同请求拒绝 |
| CT/Workflow/ThreeStageWorkflowExecutorTests.cs / CompleteMappingExecutesOnlyDetectionSortingAndUnloadPreparationInOrder | 三阶段责任、不代解锁/最终完成；正文按Detection→Sorting→UnloadPreparation迁移 | 保留、更换JSON B4 | V02上层隔离；方法名字不定义顺序 |
| 同 / MappingFailedPausesDetectionAndNeverCallsPlcPort | 错映射无物理后继 | 保留B2/B4 | V02拒绝 |
| 同 / MissingSortingTargetBlocksBeforeUnloadMotion | 缺分拣目标先拒下料 | 保留、迁移输入B4/B7 | V02语义目标为空 |
| 同 / TwoProblemEntitiesCannotReserveTheSameTargetCell | 预留冲突 | 保留B4 | V02一个冲突 |
| 同 / OccupiedSourceCellCannotBeAssignedAsSortingDestination | 源位占用不能作目的 | 保留、去字符串Replace B4 | V02一个占用代表 |
| 同 / ReservationSaveFailurePreventsEvenUnloadDispatch | 预留提交前不下料 | 保留B4 | V02保存失败 |
| 同 / MixedTrayCommitsNormalMemberRetentionWithoutAnotherSortingAction | OK留位保存、仅问题实体动作 | 保留B4 | V04组mixed承接真实动作，V02必要断言 |
| 同 / CommunicationFailuresUseInitialAttemptPlusThreeRetriesAndOneTwoFourBackoff | 4次、1/2/4 | 保留B5 | V02控制时钟 |
| 同 / AlgorithmTimeoutsUseInitialAttemptPlusTwoRetriesAndTwoFiveBackoff | 3次、2/5 | 保留B5 | V02控制时钟 |
| 同 / SharedStageDeadlineStopsRetryBeforeItCanCrossOneHundredTwentySeconds | 原120s截止及安全Pending | 保留B5 | V02截止代表 |
| 同 / DetectionExecutionAtStageDeadlineCannotDispatchSorting | 名称与实际继续下料/分拣断言冲突 | 替换歧义B4/B5 | V02明确迟到结果不直接成功，可信目标/安全/保存成立才处置Pending |
| 同 / PlcUnknownHeldKeepsAssociationAndNeverRetriesOrAdvances | UnknownHeld保关联、不重发/解锁 | 保留B4/B5 | V02未知代表 |
| 同 / FailedPhysicalDetectionAtDeadlineCannotBecomePendingOrDispatchUnload | 120s、121s两项物理失败 | 保留B4/B5 | V02两项，不混成算法Pending |
| 同 / StaleOperationOrEpochFeedbackBecomesUnknownHeldAndCannotCompleteCurrentAction | 陈旧关联不完成当前动作 | 保留B5 | V02关联代表 |
| 同 / DetectionDisconnectedRemainsDetectionFailureInsteadOfPlcUnknownHeld | 检测断线不冒充PLC未知 | 保留B5 | V02投影 |
| 同 / RecoveryBlocksUnknownPlcActionAndPreservesItsOperationAndEpoch | 旧未知不恢复续跑 | 保留B4/B5 | V02读恢复资格，不扩恢复流程 |
| CT/Workflow/DetectionRetryAndPendingTests.cs / StrictRecipePendingWithWrongObjectPositionCannotDispatchSorting | Pending仍要正确对象/位置 | 保留、去Strict/Test负载B2/B4 | V02错目标拒绝 |
| 同 / ExhaustedDetectionRetriesCreatePendingAndContinueFormalSorting | Disconnected/4次/1,2,4；TimedOut/3次/2,5；来源/期限/保存 | 保留、迁移输入B4/B5 | V02两数据项，上层替身只证协调义务 |
| 同 / SharedDeadlineExpiringDuringFirstAttemptDoesNotResetAndCreatesPending | 初次耗尽120s，无重试、Pending提交后安全处置 | 保留B5 | 可由V02截止代表承接全部独立断言 |
| CT/Workflow/StageRetryPolicyTests.cs / DetectionCommunicationHasFourTotalAttemptsAndOneTwoFourBackoff；AlgorithmTimeoutHasThreeTotalAttemptsAndTwoFiveBackoff；SharedDeadlineWinsAndDetectionBecomesPending；RestartUsesPersistedDeadlineInsteadOfResettingIt；PlcPreDispatchFailureCanRetryButExhaustionIsFailed；PossiblyDispatchedPlcActionNeverRetriesAndIsUnknownHeld | 分别为通信次数、算法次数、总期限、重启原期限、未派发失败、可能派发未知六项 | 保留B5 | V02按这六个独立义务登记，若策略未改由已执行重试/未知代表承接，不强制六条再跑 |

### 正式装配、真实组件与工艺分支

| 原文件 / 方法 | 原义务 / 数据项 | 分类 / 依据 | 承接及最小范围 |
| --- | --- | --- | --- |
| IT/CommunicationFixtures/FormalHostCompositionTests.cs / FormalHostCompositionUsesOneDeviceAndRealStageAndEvidenceProducers | 同一设备各端口、真实阶段/证据、固定Integrated类名 | 替换类名断言、保留装配B5/B6/B7 | V01；该rig的PLC在测试进程，不能当独立PLC |
| IT/CommunicationFixtures/SingleFaceDetectionIntegrationTests.Wire.cs / AbThenCdExecuteThroughVirtualPlcWorkerMediaAndSqlite；IT/Devices伴随RunAsync | Q01/P01/AB/2次，Q02/P01,P03/CD/4次，真实调用保存来源 | 保留、改共同输入B2/B3/B6/B9/B10 | V03 AB＋V05 CD可合并；不用planner产预期；来源版本/ConfiguredOnly按SRC-14核实际生产者，不把PythonWorkerAdapter/1固定为所有提供者 |
| 同Wire / NecessaryFailureStopsBeforeNextProductMove | WrongArrival、MediaSave、Reset、SaveWindowExpiry | 保留B3/B4/B5 | V03四既有失败项，不另扩矩阵 |
| IT/CommunicationFixtures/RecipeRotationIntegrationTests.Wire.cs / SpecialDeviceRunsPoseAndExitWithoutOrdinarySortingReplay；IT/Station01伴随RunRotationAsync | 五行见下表；旋转/出口/不重复普通分拣 | 保留、迁移Test目标B2/B4 | V04实际旋转一例，其余共同组件执行 |
| IT/CommunicationFixtures/RecipeMultiObjectIntegrationTests.Wire.cs / ManualFlipRequiresObservedOccupancyAndAuthenticatedConfirmationThenClearsBeforeNextFace | 独立件人工占用、授权、确认清零 | 保留B4/B5 | V04整体人工可承接交互；独立实体身份由组件承接 |
| 同 / ManualAssemblyWithEUsesSameOccupancyAndFaceConfirmation | 整体人工、E、共享动作 | 保留B2/B4 | V04实际整体人工＋E代表 |
| 同 / AssemblySharesOneFlipAndRetainsPartsWhenECodeIsPresentOrMissing | missingCode=false/true；共享一次翻面、部位保留、缺码继续 | 保留B2/B4 | V04实际整体＋E缺码组件两项 |
| 同 / AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce | NG优先、Pending细节、整体一次搬运 | 保留B4 | 指定S：B前准备输入/oracle，冻结后共同组件执行一次，兼V04/V06；不作B执行前置 |
| IT/Station01/RecipeMultiObjectIntegrationTests.cs / TwoGroupsKeepCompletedMembersAndRunIndependentTargetsThroughRealPorts | mixedResults=false/true且fourFaceModel=false；成员面数不同 | 保留B1/B2/B4 | V04 mixed实际一例；纯OK共同组件承接 |
| 同 / FourFaceGroupsUseSourceCompositionAndDistinctETargets | fourFaceModel=true、组来源组成/E不同目标、完整取盘 | 保留B1/B2/B4 | V04四面组目标及执行组件；不强制再跑完整盘；不能只计划断言 |
| 同 / EWorkerFailureLeavesIssueAndContinuesWholeAssembly | E错误留原因、整体继续 | 保留B2/B4 | V04 E算法边界组件，实际整体保留成功E |
| IT/Station01/VirtualRecipeAndDetectionGateTests.cs / FailedFirstThreeDMotionNeverGuessesSafeZOrStartsF | 公共3D失败不猜Z/启动F | 保留B2/B4 | 若公共动作流程不改不新增整盘失败；其无后继义务由V03错到位承接，修改公共准入时追加该代表 |
| 同 / IndependentWorkerUnmatchedFDoesNotReuseOldRecipe | 实际Worker未知F不得旧配方/分拣 | 保留B1/B6 | V03正式入口失败代表 |
| 同 / InvalidDetectionWorkerResultStopsWithoutDefaultOk | UNMAPPED保留原因/失败，无默认OK | 保留B2/B6 | V03实际算法负例与日志 |
| 同 / LateIndependentDetectionWorkerBecomesFinitePendingAndSorts | 实际超时有限Pending及分拣 | 保留B4/B5/B6 | 若V06采用整体NG/Pending，则本例真实超时只在期限/Worker处理被改时必需；V02不冒充其进程证据 |
| 同 / Current007AmbiguousDetectionMappingBlocksBeforeSorting | 整段替身错映射、唯一StageEvent、零Sorting、终态None及证据输出 | 承接后合并删除B2/B7 | 已确认009-required-cases/009-test-obligations按FQN登记；先定向更新活动映射，补下行错映射项的None及证据输出，再删重复方法；不计主链 |
| IT/Station01/ThreeStageMainFlowIntegrationTests.cs / UnintegratedProducersCannotClaimRealExecutionOrIgnoreCancellation | 未接入Unknown/Unavailable、取消 | 迁移缺能力拒绝B5/B7 | V01/02；删除正式整段NotIntegrated，保留保护 |
| 同 / PersistedHandoffRecordsPendingMappingFailedAndUnknownHeldWithoutFalseCompletion | 断线替身、错映射替身、阶段UnknownHeld三场景 | 保留上层持久保护B3/B4/B7 | V02分别登记三行；合并重复须保全部断言，不计共同检测 |
| 同 / CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite | 正式启动到待取盘，原FinalOutcome=None | 保留并补010终点B3/B4/B6/B7 | V05独立PLC/Worker＋授权取盘＋最终提交 |
| IT/Api/RunMediaCatalogTests.cs / MultiFaceMediaKeepsCommittedFaceRoundAndRescanIdentity | 已提交历史多面/轮次/复扫身份 | 保留历史B3/B7 | V02历史读取一个代表；不得随旧执行链删除 |
| scripts/verify-latest-plc.py / normal、algorithm-pending、unlock-gates、plc-unknown、host-restart | 正常、质量待定、解锁、未知及重启原义务 | 拆分保留/替换整段DetectionTestMode B4/B5/B7 | normal由V05，待定/未知/解锁由V02/04，真实重启若未改不强制重跑；旧未验证状态原样保留 |

| 旋转数据行 (assembly, quality, wrongPose) | 独立义务 / V04承接 |
| --- | --- |
| false, OK, false | 正常姿态及回放出口，选实际代表 |
| false, NG, false | NG物理出口，共同旋转组件 |
| false, Pending, false | 待定出口，共同旋转组件 |
| true, OK, false | 整体旋转只搬实体一次，共同旋转组件 |
| false, OK, true | 错姿态零检测/不放行出口并保留占用，共同旋转组件 |

### 实际生产者、采集、移交与最终来源义务（ARC-001增量）

以下对应architecture历史审查S01—S14，并补同一直接受影响fixture的独立保护。SRC为义务ID而非声称新测试已存在；方法名为当前精确方法，括号内为必需数据项。目标“原方法”指实施时迁入共同语义输入后保留该方法职责；共享目标须在manifest登记实际case/dataRow/obligation映射，不将整类机械设为必跑。

证据层级：UpperIsolation只证明协调规则（可用整段替身）；Component为真实被测规则/投影组件；StorageComponent为真实SQLite组件但不是主链；PortComponent为实际正式适配器/端口；FullRun为V05正式入口独立进程完整链。所有名称、层级及承接均为设计，尚未迁移/运行。

| 义务 / 精确原文件与方法、数据行 | 原有效保护与须纠正断言 | 分类 / 独立依据 | 固定最小承接、层级及复用 |
| --- | --- | --- | --- |
| SRC-01 / CT/Workflow/WholeTrayWorkflowOrchestratorTests.cs / DeviceVersionComesFromActualSemanticProducerNotCurrentProtocolDefinition | PLC VersionRef等于SemanticStageFixture.Origin.ComponentVersion，不取当前协议定义 | 保留业务、迁语义fixture；B9 | V02原方法，UpperIsolation；不声称实际PLC证据 |
| SRC-02 / 同 / TestPurposeDoesNotReplaceEachActualProducerWithSimulatedSource | Test用途下Camera=Virtual、Light=Test、Algorithm=Real/DeclaredUnitAlgorithm/7，Host=Derived、SoftwareLoopOnly | 保留混合来源/版本；B9 | V02原方法，UpperIsolation；声明Real的替身仅检传播规则，不能证明真实硬件或生产；V05正常来源不能抵扣 |
| SRC-03 / 同 / UnknownProducerCannotBeFilledFromPurposeOrHistoricalResultCategory(true,false,"Camera") | 相机未知拒ComponentEvidenceMatrixIncomplete/Camera，无WholeTrayCompleted/ObservedUnlocked | 保留独立负例；B9 | V02精确相机行，UpperIsolation |
| SRC-04 / 同 / UnknownProducerCannotBeFilledFromPurposeOrHistoricalResultCategory(false,true,"Algorithm") | 算法Unknown不能由历史ResultSource.Simulated补值；错误指向Algorithm，无整盘/解锁后继 | 保留独立负例；B9 | V02精确算法行，UpperIsolation；不能由SRC-03或正常链代替 |
| SRC-05a / IT/Station01/PublicPreparationHandoffV2IntegrationTests.cs / DetectionCanOnlyStartFromCommittedMatchingV2Handoff：无已提交移交、无当前绑定回执两子项 | CommittedHandoffV2Required、CurrentRecipeApplicationReceiptRequired；旧prebuilt.IsValid依赖不完整/占位请求 | 保留拒绝，替换非严格合法正例；B3/B7/B8 | V02移交StorageComponent，准备记录只证消费拒绝；合法共同输入正例由V02 OnlyPersistedMatchingV2HandoffCanConstructDetectionRequest承接；旧特权断言删除 |
| SRC-05b / 同方法：当前真实轮提交正例 | 同Run/Tray/Plan/Operation、3D/F媒体、窗内Receipt；RequiredCommits含handoff WriteId/revision | 保留；B3/B8 | V05 FullRun在实际链读取以上事实；不额外运行原方法后半整盘；查询不得重建许可 |
| SRC-05c / 同方法：固定Simulated及SourcePolicy==Handoff.Source两断言 | 来源不能固定环境推导或混作用途/批准 | 替换错误断言，保留真实来源；B7/B9/B10 | V05 FullRun核Handoff.Source=本轮已提交F Origin.Source，来源CallId/提交可追溯；请求用途/冻结ApprovalScope独立核对，WorkerSession/ExpectedVersion不能代实际来源 |
| SRC-06 / IT/Station01/Q01CameraMediaTests.cs / FrozenTestManifestSelectsDistinctReadableAAndBMedia | A/B各自实际SHA与独立manifest预期匹配、媒体可读、2次Detection触发 | 迁移到固定图片边界，保留事实；B6/B10 | V03 AB PortComponent共用一次：逐CaptureId/相机/已知摘要及计数核对，不能仅核两个文件存在/摘要不同；固定文件值不进共同业务 |
| SRC-07a / CT/Acquisition/CaptureCoordinationTests.cs / EndedAndMediaTakenAreIndependentAndFirstOwnedBufferWins | Ended/媒体独立，先[1,2]后[9]仍取首个buffer且等Ended | 保留；B10 | V02原方法，Component；新增采集事实不能代替Ended或覆盖首个buffer |
| SRC-07b / 同 / FrameWithoutEndedAndEndedWithoutContentCannotCrossCompletionGate：frame-only、ended-only | 两种缺半完成分别Take拒绝 | 保留两个独立义务；B10 | V02原方法两子项分别登记，Component |
| SRC-07c / 同 / CapturePortRejectsMissingCommittedIntentBeforeSchedulingCallback | 当前仅断言空IntentWriteId条件false，未实际调用端口/观察回调 | 替换弱断言，保留意图前置；B3/B10 | V03 AB同一正式capture实例附一个缺IntentWriteId请求：明确拒绝、触发不增、无回调/事实；PortComponent，不增加主链。旧条件断言不算该义务已执行 |
| SRC-07d / CT/Acquisition/MessageContractTests.cs / RequiresEndedAndMediaAndRejectsCrossCaptureAssociation | 双完成、另一CaptureRequest拒绝、同Request/epoch匹配 | 保留；B10 | V02原方法，Component；新增事实的不同Capture/陈旧epoch另见NF-CAP |
| SRC-08 / IT/Api/CommittedResultProjectionTests.cs / CommittedImageIsVisibleBeforeObjectAggregateWithoutInventingObjectQuality("OK"/"NG"/"Pending") | 每图质量、CallId/MediaId；对象Disposition=null、NotProduced、Completeness=Unknown；RequestedCapture；不编造置信度/缺陷 | 保留三质量行并补实际参数；B11 | V02原方法三行，Component；无ActualSettings保持未提供、不从requested exposure补值；实际与请求不等的单一额外样本见NF-PROJECTION |
| SRC-09 / IT/Storage/ComponentSourceMatrixStoreTests.cs / MixedSourcesRoundTripAsSoftwareLoopOnlyAndNeverCollapseToReal | SQLite往返混合PLC Virtual/算法Simulated、SoftwareLoopOnly；WholeTrayCompleted为HostDerived/Derived | 保留；B9 | V02原StorageComponent，实际保存/读取但不计FullRun；正常完成不能抵这些来源断言 |
| SRC-10 / 同 / MissingUnknownOrUnverifiableRequiredComponentBlocksCompletion(Missing)、(Unknown)、(Unverifiable) | 三行分别拒WholeTraySourceMatrixIncomplete:Algorithm；WholeTrayCompletions与Matrices均空、Run仍UnloadPreparation | 保留三独立来源/原子拒绝；B9 | V02原StorageComponent三数据行；不是SRC-03/04内存协调器拒绝，不能由V05替代 |
| SRC-11a / 同 / FinalMatrixKeepsReadyEvidenceImmutableAndAddsAuthenticatedHumanActor | 两矩阵、Ready引用不变、Final人工AuthenticatedHuman；scope仍SoftwareLoopOnly；人工事件Real/Measured、汇总HostDerived/Derived | 保留；B9 | V02原StorageComponent；核Ready内容/摘要不可回写。单元声明人工性质不等于实际人工整盘；不声称此成功用例覆盖所有事务失败 |
| SRC-11b / 同 / TestManualActorRetainsActualOriginWhileFinalIsHostDerived | 人工确认Test/Derived，最终HostDerived/Derived；Final ManualActor=Test、SoftwareLoopOnly | 保留；B9 | V05受控Test客户端FullRun的终点读取承接，不额外跑同义存储正例。若V05改用非Test渠道，B前恢复原方法为V02必需，不丢Test主体义务 |
| SRC-12 / IT/Api/Station01SourceMatrixApiTests.cs / EvidenceEndpointReturnsComponentSourcesWithoutCollapsingMixedEvidenceToReal | GET200/ETag、source原枚举、completionId/scope/blocked、组件来源、待取盘Final空及AwaitingFinalUnloadCompletion；WholeTray计划/HostDerived/RecordNature/Quality | 保留投影，替换启动后直写三阶段Completed的准备方式；B9/B11 | V05待取盘GET evidence承接，来源等于本轮已提交事实，不固定Simulated；旧方法只能改为明确查询组件fixture，不能补业务并计主链。不再另跑其旧整盘准备 |
| SRC-13a / IT/Station01/FinalUnloadCompletionIntegrationTests.cs / AuthenticatedManualConfirmationIsTheOnlyStepThatCreatesFinalUnloadCompletion：授权/时序/重放 | 过早确认409；待取盘无Final矩阵/事件；确认后Completed；重放Accepted且replay=true | 保留；B4/B9 | V02最小API准入Component核过早409；V05实际确认及重放、前后查询合并，不增加完整盘，也不依赖“马上发请求”竞速 |
| SRC-13b / 同方法：最终来源及持久引用 | 6组件、blocked空、SoftwareLoopOnly、ManualActor Test；同tray/plan、物理stage epoch；三个完成事件引用，人工/Final事件各1、矩阵2、Run/Terminal Completed | 保留；B4/B9 | V05 FullRun精确承接，与SRC-11b同一次终点读取；认证不等于真实人工移盘 |
| SRC-14 / IT/Devices/SingleFaceDetectionIntegrationTests.cs / RunAsync；Wire入口AbThenCdExecuteThroughVirtualPlcWorkerMediaAndSqlite | ResultSource.Test、实际算法Test/版本、相机Test、光源ConfiguredOnly及真实调用保存 | 保留、替换永久固定提供者版本断言；B6/B9/B10 | V03 AB PortComponent＋V05 CD FullRun，按该轮冻结绑定与实际生产者事实核版本；V06可合法换为ContentSampleWorker/1，禁止按用途补值 |

本次新增事实必须补独立最小承接，不能用旧测试的方法名声称已经覆盖。下列NF为计划新增/扩展义务身份，精确实现方法/FQN在tasks和实施登记时落定；本轮不创建测试。

| 计划义务 / 承接位置 | 独立输入与通过/拒绝判据 | 分类、依据 / 最小范围 |
| --- | --- | --- |
| NF-CAP / CT/Acquisition相邻事实关联组件 | 当前Request/Capture/epoch/RequestedSettingsDigest正例；crossCapture、staleEpoch分别拒绝且不写入当前事实，不能由媒体已到手抵消 | 新事实保护，B3/B10；V02三个独立数据义务；V03 AB读取正式适配器返回及已提交事实证明生产者，不增加整盘 |
| NF-CAP-SOURCE / 采集组件与V03 AB | 实际MediaSource.Unknown不回退Simulation.Fixtures.MediaSource；无ActualSettings保持Unknown/未提供；固定图像仅ConfiguredOnly且如实标重放，无物理SDK已应用 | 保留来源/扩新事实，B9/B10；V02 Unknown负例，V03 AB合法真实适配正例；不要求SDK接入 |
| NF-F-ORIGIN / 扩CT/Station01/FScanStepTests.FUsesOneTriggerOneImageAndDoesNotPerformRecipeOperation | 实际Origin故意不同于ExpectedComponentVersion/WorkerSession；事实取实际值并关联Run/Capture/Call/提交，仍单触发/单调用、F不预绑定 | 保留/补事实B1/B3/B10；V02 Component传播＋V05 SRC-05c真实F保存消费，不把内存writer当SQLite |
| NF-F-REJECT / 计划FSourceFactMustBeCommittedAndMatchCurrentCallForHandoff | missing-or-uncommitted（内存中已有本轮实际Origin，但无对应已提交F来源事实）、wrongCall、unknownOrigin三项分别拒绝可续接来源，无依赖检测；具体原因定位来源提交/关联/可信性 | 新事实保护B3/B9/B10；V02移交Component三行；SRC-05a无handoff/无Receipt不替代这三行 |
| NF-F-SAVE / 扩CT/Station01/FScanStepTests.FailedAlgorithmFactSaveDoesNotSubmitFCompletion | 新Origin随必要算法事实保存失败时无F完成，不先续接后补来源；原采集释放义务保持 | 保留B3/B4/B10；V02原保存失败代表，不另增整盘失败 |
| NF-PROJECTION / 扩SRC-08投影组件 | 无ActualSettings与请求值并列缺失已由原三行承接；另一个已提交实际值不同于请求值的样本分别显示两者，不能复制请求覆盖实际 | 新事实保护B11；V02一个额外投影样本，不把三质量×参数组合扩成矩阵 |

同一旧fixture及投影helpers的直接影响继续按义务登记，避免只修点名方法而遗失有效保护：

| 精确原方法 | 分类/依据及具体承接 |
| --- | --- |
| CT/Workflow/WholeTrayWorkflowOrchestratorTests.MainFlowPersistsWholeTrayUnlockAndManualCompletionInOrder | 保留B4/B9；V05同Run事件/回执核三阶段→ReadyForUnlock→ObservedUnlocked→授权→Final；原UpperIsolation可留，不强制再跑正常行 |
| 同类 UnlockCannotBypassPersistedWholeTrayEvidence | 保留B9；V02原UpperIsolation：无WholeTray记录拒ObserveUnlock且PLC Calls空 |
| 同类 UnknownUnlockFeedbackDoesNotCreateObservedUnlocked | 保留B5/B9；V02原UpperIsolation：UnknownHeld且无ObservedUnlocked，不与来源Unknown混同 |
| 同类 ProductionSourceCannotUseSimulatedStageEvidence | 迁移准入/替换SourcePolicy="Production"旧构造；B7/B9；V01既有生产缺批准/来源不满足的拒绝代表明确原因、无动作；不能拿请求形状任意异常抵扣 |
| IT/Api/CommittedResultProjectionTests.QualityAndRequiredTargetsRemainDistinctFromFlowFinal；LegacyUnassociatedPayloadDoesNotBecomeCurrentObject | 保留B11；若改共用投影构建/身份逻辑，V02原方法分别必需；若仅改采集参数则登记未受影响保留，不机械执行整个类 |
| 同类 EvidenceProjectionPreservesCommittedActualReadbackInsteadOfCopyingTarget；MotionTargetHasFiniteBusinessFieldsAndReadsCurrentSemanticPosition | 保留B5/B11；只在改MotionFacts投影/共用位置字段时纳V02精确原方法；009地址隔离及实际不由目标伪造保持 |

现Final矩阵成功测试证明正常提交/不可回写，不冒充全部事务失败验证。保持原事务边界时，SRC-10三种真实拒绝及既有保存规则足够承接本次来源变化；若实际修改Final事务边界，再按该改动选一个必要保存失败代表，禁止整片扩故障矩阵。

本表不授权删旧证据。执行前migration registry须把每个方法与必要dataRow映射到当前精确FQN/caseId；更名后同时维护正式清单。未找到方法、数据行或承接证据是受限/失败，不能按“已迁移”通过。SRC/NF的独立负例不能由正常V05抵充；标FullRun的共享正例须逐断言被实际完整链承接，条件变化则在B前明确恢复最小组件。D01—D11清理结论见[研究](../research.md)；D10的009活动FQN/映射依赖已确认，先承接再删重复，历史源快照/报告保持。

## VG-07 修复后冻结与替换覆盖

顺序不可倒置：完成修复及必要合同对齐 → 准备替代输入/实现、S及独立业务预期并登记全部最小义务 → VG-05.1的B取得可用修复后基线 → 冻结共同职责文件、相关合同、预算/期限规则及先已审阅的oracle/manifest → 替换环境并重核职责闭包与摘要 → 经共同业务实际执行E与S → 对照必要行为/保存 → V07核T收口。S只在冻结后执行，不是B前置；不得在看到基线结果后倒填预期。

冻结范围按VG-02职责，包含Infrastructure中尚承业务职责的辅助代码；不按“移入adapter”排除。固定清单记录路径、职责/符号、内容SHA256、项目/Compile链接、合同及oracle摘要。替换后重新枚举，丢失/新增未归类/移动后内容改动均使冻结无效。修复后若再改业务、相关合同或预期，按VG-05.1重建受影响基线并形成新完整B与冻结，废止旧替换结论，不能用旧报告补齐。

| 解除的依赖 | 基线 → 实质替换设计 | 比較与覆盖证据 |
| --- | --- | --- |
| 素材 | 原文件图像集 → 另一组实际内容/摘要不同、业务测量及质量等价素材 | 真实采集输入/媒体变，调用及必要动作/保存不变；不只改文件名 |
| 算法测试实现 | 现virtual-station01-algorithm → R10选定的独立ContentSampleWorker/1，以PNG样本内容计算高度/原码/score及双图结果；启动配置只负责接入这个实现 | 按VG-07.1核实质计算差异、实际实现/配置身份、输入处理/结果/释放/保存；路径/名称/参数/摘要变化不能单独证明替换 |
| 坐标提供 | 原fixture坐标解码 → 另一提供方式（如独立语义坐标文件或内存配置提供者） | 同样测量引用、偏置/单位/范围，经共同resolver得到相同目标；配置来源事实可变 |
| 配置/F解析及测试配方标识 | 原JSON fixture模式和TEST码映射 → 不同输入shape及另一合法配方标识/码映射 | 两者进入同一validator/planner；原码/映射/目录摘要如实不同；同轮选择/F/计划一致 |
| 正式装配及模拟设备 | 原环境清单/启动绑定 → 另一合法绑定集合；设备通过相同端口/协议提供等价反馈 | 两轮均正式Host唯一共同执行，无整段服务替换。若设备适配实现此次未改可复用；装配差异至少实际覆盖新相机/算法/坐标/解析绑定 |
| 合法结果变化 | 预先指定S在冻结后执行整体NG＋Pending细节 | 独立规则核NG优先/有限Pending、实体一次搬运、真实保存；不强求结果相同，也不另加正常完整盘 |

五类主要依赖可在一次组合等价完整链中覆盖。算法替代只需覆盖该代表实际调用的公共3D/F、单图及融合等现合同，不开发生产算法或通用插件。替代输入/实现在基线前完成准备及自核，冻结后不允许改共同代码适配它。

oracle来自V1.1、001/003/008/009有效合同及经审阅的输入/预设设备结果：明确实体/面/相机顺序、目标数值、动作关联、必要提交顺序、质量/处置/最终状态及无越权后继。不调用RecipeRunPlanner、共同validator或执行器产生expected；被测产物只作actual。

等价比较允许运行ID、时间、媒体/配置/提供者的真实来源引用变化；应匹配语义动作、对象/面/目标、质量判断和全部保存义务。输入/结果不同则比较相应独立规则，不能强制和基线相同。共同业务源文件内容变更数必须为0。

### VG-07.1 第二算法实现的接受与拒绝

具体选型、执行路径和B前等价输入在[research R10](../research.md)单点维护，绑定边界见IB-03。第二实现是计划新增`010-content-sample-worker.py`/`ContentSampleWorker/1`，复用现NDJSON传输和Host结果解析，但独立解析实际媒体样本并计算；不是原Worker的别名、配置变体或转调包装。仅支持代表需要的Height/FDecode、四单图及两同面融合共八次实际调用，重试发生时保留实际次数/结果，不预填八次成功。

| 判据 | 必需证据及拒绝条件 |
| --- | --- |
| 实质差异 | 对照审阅旧Worker的随机/manifest结果选择与新Worker的微米换算、媒体原码解码、score阈值及双图计算路径；原实现调用/import/runpy/subprocess转调、复制旧结果生成或caseId结果表均拒绝。传输、通用读取/SHA/日志可复用 |
| 输入实际参与 | B前最小组件自核用同一新处理函数的不同score/测量样本核结果随内容变化，预期由输入算术及合同确定；E核实际读媒体、长度/SHA、身份和计算中间值/结果。不读输入、只返回预设整段成功或只有文件名变化均拒绝 |
| 可核验提供者身份 | Host实际启动脚本路径及代码/配置摘要、PID/WorkerSession，实际提供者Test/ContentSampleWorker/1，与逐CallId日志、真实Result/InputReleased和Host保存事实关联；旧/新实现路径/版本声明必须对应执行产物。文件摘要、标签或不同启动参数单独不足以通过 |
| 同合同/同业务 | 保持Worker协议与端口结果形状及租约/释放/退出；Host冻结能力/参数绑定，wire未传入的字段不得声称已由进程接收。S之外的E按B前独立预期比较等价高度、F映射、目标、动作、质量及保存；不能修改业务/合同/oracle迁就第二提供者 |
| 最小范围 | R10准备的新受控输入让两实现事先可推出同等结果；同一E完整链组合证明五类替换，不增加同义完整盘。S继续独立的共同组件结果变化义务，不要求第二Worker扩成E/换面/旋转全能力 |

仅改路径、名称、进程参数、标签、注释或加转调包装，即使源码摘要不同，也判“未证明实现替换”。上述判据是演练接受条件，不增加通用插件、算法认证或性能工程。

## VG-08 本轮结论边界

文档追溯覆盖不等于构建或运行通过。010通过时仍只证明本次实际路线及架构边界；007/008/009原失败、未运行、转出事项保持原状态。阻断010须指出具体依赖与受影响义务，不把所有旧问题自动变成本专项前置。生产批准不足始终只限制其对应入口。

## 011共同执行承接（2026-10-03）

010原验证集合/历史结果保持当时范围；011/012后续只选本次受影响构建、组件回归、架构门禁和联合代表链，不重新执行全部历史专项。共同模型/唯一业务校验须覆盖真实保存重读与F料盘编号唯一匹配、冻结隔离、配置更多面/独立E、三区处置/姿态退出；设备原码/内部握手与Test编号/路径不得回流业务。字段/签名/存储留011/012 plan对齐。替代后核查真实调用、装配、配置、脚本与历史读取，保留保存/关联/取消/期限义务并实际删除无用途旧旁路/测试特权/协议/配置/测试及孤立代码；原失败证据不删。

## 013实施前定向同步（2026-10-04）

SY-04：此前延期的PLC轮询性能由013在通信适配器内实现，不改变共同业务唯一执行路径、CE输入/保存/取消/期限边界。013显式profile必须保留完整持续L（当前70声明项，按实际case/dataRow核对），受影响009同身份结果一次引用；不扩010动态全专项。before测量是有自身源码/构建/输入的子阶段，不套after门槛，不等待最终Passed；013 final保持L外层和必需账本，不能成为通用跳L开关。每侧一次60秒空闲和同一run-2，必要组件含正常HeldFlip失败保存/真实自动1024分段接续/Recorder缺口及三类负例。实际API观察器仅提供2秒后端负载，不能冒充页面ready或改变真实保存/F匹配/冻结/Final。验收严格按013 V01.1 A/B/C分侧和V09.1身份去重。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

## 2026-10-05确认需求的本功能承接

当前来源为高德_文档/new-1/PLC与上位机通信接口协议.docx及同目录信号表，摘要见014 basis-receipt；旧来源只作历史，空白正式地址仍不补。014规格定义场景1特殊两组绝对旋转/逐件立即分拣、两用途抓手有效同号复用/换号或失效重建；翻面无选择握手。012定义所有配方手动10×10实际格位、各区独立号、OK检测序、稳定关联与完整保存。普通面/成员顺序和整盘统一分拣保持，特殊OK需从工位到本件原始OK槽的放料关联，姿态异常跳过后续检测，最后从原槽实际分拣到Pending。

本轮仅确认需求同步，不生成新设计或任务；旧ID/勾选/失败/归档及旧实现限制保留其时点。共享字段/序列化/接口、消费者和后续任务必须在改码前实际对齐；业务层无原码/地址/内部握手，复用唯一校验/执行/公共取放，保原期限/取消/代次/真实取料保存门和日志。014主责必要共同/通信增量，012主责界面保存消费。验证限一条多件特殊、一条受影响普通及必要组件/持续L/受影响通信/原型与执行完整性，不扩大历史专项或重启013性能研究；013-acceptance/2及性能偏差保持。

后续只并入既定特殊/普通代表或必要组件：普通OK无多余分拣搬运；至少两件特殊OK实际返回各自原槽、保OK编号顺序、实际安全位后才下一件；原槽回放失败不记录完成/不推进。保存重读/冻结保原始关联，界面任意OK目标选择为0。不同件/区域同号和重新编号不串值，无同义新完整链或全量故障组合。
错误旧任意OK目标期望须纠正而非删除正确保护；失败/Skip/漏行/假反馈不得通过，009/010/013现行保护和验证门槛保持。本轮未执行上述验证。
