# 010 最小验证指南

状态：010原48项已实施，2026-10-02独立证据`artifacts/recipe-execution-010/standalone-54fa1c4e72904772bd9c91cb27bc7d47-verify-1/result.json`记录当时L64/B178/E1/S1/T180通过。当前2026-10-03仅修补B02提前返回门禁，验证范围为新反例/正例和当前L；不会重跑B/E/S。检查器、清单和本文变化后，旧全局冻结摘要不再代表当前版本匹配；旧动态证据保留，修改后全套聚合未重新执行。

## 1. 执行前置条件

[plan的A01—A09](plan.md)必要文档/消费者对齐已经原实施阶段交付，证据见上述原运行目录。后续共享接口修改仍须先实际更新所属spec/contracts/plan/tasks。本Bug不修改共享业务接口或跨功能文档。

运行环境沿当前锁定.NET SDK10.0.401、现Python/Worker依赖、PowerShell及正式Host/VirtualPlc。使用独立虚拟PLC进程、独立算法进程、正式采集适配器、真实SQLite及媒体存储。正式组合根绑定唯一共同检测。真实设备/SDK/生产入口仍受原批准限制。

为每轮准备独立工作及证据目录，位于artifacts/recipe-execution-010/{verificationRunId}/，由现010 runner创建；数据库使用现有准备工具及批准schema创建本轮专用库，不使用内存库代替，不升级或回写历史库。凭据通过既有受控配置传入，日志不打印令牌。

明确区分：

| 当前能力 | 实现/证据入口 |
| --- | --- |
| RecipeExecution010 profile与固定B/E/S/T编排 | scripts/verify.ps1、scripts/workflow/recipe_execution_010.py及原运行目录 |
| 唯一共同执行、typed输入及事实消费者 | 原T001—T048实施记录及B/E/S证据 |
| 独立输入、ContentSampleWorker/1及冻结比较 | Integration/Fixtures/RecipeExecution010及原E evidence |
| N/P/G/C、职责闭包及所有profile必经L/最终核验 | 010-lightweight-cases.json、run_lightweight、validate_bundle、final_gate |

完整命令仍会执行固定B→冻结→E→S→T；本Bug不能运行该完整命令后声称仅做L。L单独调用现`run_lightweight(parent, profile)`，由该函数构建并执行全部固定轻量行；专项脚本与本轮凭证在`.specify/bugs/010-b02-early-return/`及独立artifacts目录记录。单独L通过不代表010当前完整动态验收通过。

所有项目验收profile均无条件执行[VG-01.1](contracts/verification.md)的L，然后执行该profile自己的必需集。L只构建/运行必要检查器，做源码边界和报告完整性数据验证，不启动Host/PLC/Worker/数据库，不要求每次开发重跑010主链或009全部动态验证。默认009、其他profile以及现009 BoundaryMinimum/SelectedCasesOnly聚合入口均无豁免；独立调试测试仅是局部证据。

触发由现统一runner及上述旁接聚合器负责；_run_verify总passed、verify_entry退出结论、工作流assess/finish和两个旁接聚合最终点核同一当前attempt凭证。缺执行、旧报告、仅passed布尔或源码/构建/清单不符均拒绝对应通过；step进程退出0不代表验收通过。无需Git或新CI/变更检测平台，详细固定轻量集合与C01—03数据样例集中在VG-01.1。

## 2. 准备一个配方及独立业务预期

最低完整代表使用已有Q02/CD语义、P01/P03非连续槽。现参考文件是specs/008-recipe-driven-inspection/fixtures/usr-e-1.0.2/fixture-q02.json，声明R008-Q02/1.1.1-test、F码TEST-TRAY-0202。它是输入来源资料，不能仅因availability=Available认定当前可运行：其中预算等引用有旧版本，必须与当前已批准完整配置核对。

完整动态运行使用已实现的010输入记录（本Bug不启动），包含：

- 当前目录及内容摘要、合法RecipeId/Version、Scenario、OccupiedSlots；F原码与所选引用匹配。
- 公共配置、预算、模拟配置的实际版本/摘要和批准范围；完整配方应用预算，生产无批准时不回退。
- 3D/F/检测及适用E素材、真实内容摘要、媒体/Worker清单、能力绑定、坐标与测量依据。
- Host/虚拟PLC/Worker实际进程与连接参数、本轮库及媒体路径、配置来源；不包含预填业务完成行。
- 由需求/合同独立审阅的预期动作/目标/质量/处置/保存清单，及其来源条款和摘要。

独立预期至少明确C:P01、C:P03、D:P01、D:P03，实际测量＋偏置得到的目标、单图/融合关联、必要保存、盘末下料/适用分拣、整盘/解锁/最终确认。不能调用被测规划器或执行器生成expected。基线B之前同时准备R10的第二ContentSampleWorker输入及S整体NG/Pending的输入/预期，固定等价原码映射、11mm样本与111mm目标、单图/融合OK的独立数值依据；这是本轮新受控输入，不改008历史fixture，也不从B结果倒填。

保留旧fixture与历史证据。新输入缺预算/坐标/能力时修复本轮批准配置或报告受限，不能修改共同业务默认值。完整运行的010 runner及独立进程harness已接入本轮目录；不得误用simulate-station01-load.ps1的旧StartRun捷径补接流程。本Bug不运行这些准备/启动工具。

## 3. 正式最小验证入口与顺序

需要重新执行完整专项时，从工作区根运行（本Bug明确不执行）：

    Set-Location -LiteralPath 'E:/dzk/gaode-1'
    ./scripts/verify.ps1 -Profile RecipeExecution010

**Profile参数已实现；本次B02修补不执行完整动态命令。** runner及010工作流必须指向[验证合同VG-01](contracts/verification.md)相同实现。不给外部任意filter；内部筛选由已审阅固定manifest生成。单独调试某测试的结果不得当整体通过。

| 顺序 | runner应完成的操作 | 通过/拒绝判据 |
| --- | --- | --- |
| 0 | 准备并审阅B/S/E输入、第二实现和独立oracle、必需清单及VG-06 SRC/NF各方法/数据项承接；记录当前source/input/manifest身份 | S和替代者在B前已确定；缺项、解析失败、无批准或未承接先拒绝，不从运行actual产expected |
| 1 / V00 | 构建Host及受影响依赖、Contracts/Rules/Integration测试工程和主链所需VirtualPlc；核Worker依赖 | 所需工程当前构建成功，源码前后一致；不启动全量测试 |
| 2 / V01 | 执行并核L：正式职责闭包、009适用静态边界、N/P/G/C；另做两组真实Host装配及缺能力 | L只跑一次，真实DI不被静态样本替代；其他profile漏跑、旧凭证及所有完整性坏报告拒绝 |
| 3 / V02—04（除S） | 语义/来源/目标/期限/回执组件；AB及必要失败；双面、组/四面组件、整体人工/E、旋转差异，CD正常可由V05承接 | B中的每项有实际证据；未知来源/错关联/原子拒绝不能由正常链替代；仅指定S移到冻结后 |
| 4 / V05 | 单配方完整正式链，独立PLC/Worker、真实存储和现人工操作 | 同一Run到FinalUnloadCompleted，所需提交及真实来源可查 |
| 5 / 冻结准入 | 按VG-05.1核B=V00/01/02/03/05＋V04除S的全部必要证据；B通过后冻结业务/合同/oracle/清单 | S的执行不作B前置；B其余缺项或失败不得冻结 |
| 6 / V06与S | E：第二条正常完整链组合等价替换；S：预先指定整体NG/Pending共同组件，冻结后一次兼V04/V06 | 五类实质替换、业务零改动；S保留明细/实体一次搬运/真实保存，不增加第三条正常完整链 |
| 7 / V07核T | 核T=B∪S∪E∪收口，迁移、删除、历史保留及必需发现/执行/凭证完整性 | 按case/dataRow/attempt/run及义务映射去重；任一最终必需项失败/未执行则T失败，不以T已通过作为收口前置 |

S固定为AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce的整体NG/Pending共同组件义务，B前只要求输入与独立预期已准备，冻结后才执行；V04其他必要代表仍在B，不许整体推迟或省去。它一次承接V04/V06，不要求基线前再跑同一项。目标方法或代表若需变动须在B前登记，不在失败后删必需项。

## 4. 完整主链的观察和既有操作

runner启动的Host使用正式装配。它提交现API的实际请求，而不是直接构造DetectionRequest：

| 入口/观察 | 所需内容与判据 |
| --- | --- |
| GET /api/v1/recipes/catalog | 取得当前可执行条目及实际id/version/catalogDigest；拒绝受限条目 |
| POST /api/v1/station01/runs | requestId、contextJson字符串、publicConfigRef/budgetRef/simulationRef；context为station01-start-run-context/2.0，含独立tray/station/line身份、scenarioId、occupiedSlots、purpose及expectedRecipeRef |
| 命令/运行查询 | 202只为受理；按receipt中的commandId/runId查询，不能再次启动或直接续接检测 |
| 自动阶段 | 观察公共准备、唯一F/绑定/移交、共同检测及处置。虚拟设备按命令/自身状态响应，算法实际读输入；脚本不能排工序 |
| 适用人工换面 | 使用现GET pending与授权确认入口，核实体/步骤/目标面/版本及实际占用清零；不新增按钮或授权捷径 |
| 授权取盘 | 在实际整盘提交和解锁条件满足、对应取盘事实成立后，读取本轮最新observedRevision，POST /api/v1/station01/runs/{runId}/manual-removal-confirmations，携requestId、匹配该版本的expectedRevision、reason及现凭据；版本已变则重新查询，不绕过校验 |
| 最终结果 | 查询当前Run及必要提交/媒体/组件来源，确认FinalUnloadCompleted及最终状态。AwaitingManualRemoval不是完成 |

上层测试可使用整段stub证明协调器保护，但报告标UpperIsolation，不能计V05。FormalHostComposition的进程内PLC也不能计独立PLC；直接new检测类的真实组件仅计V03/04。实际失败时保存当前诊断、Run/动作/对象及处置，不以“未结束”当完成。

## 5. 冻结与替换的操作判据

按§3准备完所有输入/替代实现/独立预期并取得B通过后，再保存职责闭包文件清单、SHA256、Compile/链接信息、相关合同、预算/期限、独立oracle、manifest及对应基线运行。S尚待冻结后执行，不影响B的定义。冻结清单包含Infrastructure等目录中实际承担业务的helper，不以位置/Role标签排除。

E一次组合覆盖：不同内容素材、R10独立ContentSampleWorker/1、不同坐标提供方式、不同解析shape/码映射/测试配方标识和正式装配输入。第二Worker实际读PNG样本块，独立完成微米换算、F码解码、score阈值和双图计算；启动配置只用于绑定这个实现。记录其代码/配置摘要、实际脚本/PID/Session、逐调用读入/结果/释放及Host保存，与VG-07.1实质差异审阅合用。仅改路径、名字、参数、标签、注释、文件摘要或转调原实现的包装均拒绝，不能改共同预期迁就替代者。

替换后重新枚举职责闭包，核共同源文件内容变更数为0，相关合同/oracle不变；再经正式入口跑完整链并比较语义动作/判断/必要保存。运行ID、时间和真实来源引用可变。共同业务、相关合同或独立预期变化即废止旧替换结论，重建受影响B证据、形成新完整B身份并重新冻结，再执行新冻结后的E和S。仅未受影响B证据可按VG-04核原执行上下文及当前依赖闭包/摘要未变、记录独立影响审阅后按原身份引用；不得改旧行。当前L、修改义务及失效冻结的E/S不可复用。T按各原子attempt核身份，不把基线与替换伪装成同一次输入/运行。

合法结果变化用指定S整体NG＋Pending代表，核独立质量/处置及保存规则，不强迫与基线结果一样。最低为正常基线＋等价替换两条完整链，受影响差异优先组件级承接，不扩全部组合；实际改动涉及超时处理时仍按VG-06保留必要超时代表，不将其默认扩为另一完整盘。

## 6. 结果与拒绝说明

每轮产物至少包括源/构建/输入身份、必需manifest、逐项ledger/TRX、边界/装配/N/P/G结果、运行请求与进程来源、真实提交/媒体/日志引用、迁移/删除核对、冻结/替换对照和总判定。报告文件格式由现runner扩展，不能只给一句“通过”。

必需项缺失、零发现、漏执行、过滤、Skip、解析失败、旧报告或原始证据缺失全部拒绝。旧007/008/009失败及未验证事项只保留其原状态，说明是否确实阻断某010义务；不得抹去或自动变成全套重验前置。

原Phase 1及质量清单仅代表当时文档质量；原48项实现与运行结论见tasks实施记录。当前B02修补状态以本Bugassessment/fix/test和新增凭证为准，不刷新旧010整体Passed、冻结摘要或B/E/S报告。
