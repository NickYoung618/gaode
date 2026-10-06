# 014/012 Phase 1设计交接与停止点

2026-10-05；根E:/dzk/gaode-1。014新plan流程、012保留既有设计的配套增量；每次setup显式对应feature目录并核实际返回路径。setup原脚本仅在内存调用加NoPersist，脚本文件/feature.json不改；返回BRANCH仅功能标签，无Git分支创建。before/after_plan hooks为空。

## 当前结论与版本

014 research/plan/data-model/必要contracts/quickstart已形成；唯一共同RC10设计1.5、新写正文4/冻结3，当前集成代码仍1.4/正文3。012矩阵/保存/显示设计已消费同一合同，无第二模型、校验、身份、目录或执行器。旧012修复已实际合入，不再要求旧批次重交，不用旧25/25证明新内容。当前是设计准备度，软件执行数0。

DUI02成组成员、DUI03半成品部位各有最小待审方案及单独HTML。业务实体关系已确认，导航具体位置/文案/填写状态表达尚未用户确认；012两条未勾要求仍保留，不能宣称012全部设计通过。当前requirements/architecture仅纠正路径/阶段与当前说明，未生成新清单/任务/实施勾选。

## 共同字段及闭环

稳定CellId关联实际10×10行列与区域，独立区域号只投影；Coordinates/成员/参数不能按重编号迁移。NG/Pending目标点按目标CellId只存一份，既有分配引用不新增策略；OK目的固定本件原槽。PhysicalSlotIndex来自正式关联，不从行列/显示号/Capacity推断。

特殊场景1独立件，两用途抓手明确1/2，共享工位Pick/Place及两Stage绝对角，各件四采集逐组/相机参数独立。一个全盘Frozen，特殊scope仅限定消费；件实际取放/保存/原槽回放/safe完整后才下一件，全部参与与排除事实齐后整盘下料/Final。普通原阶段及批次节奏保持，普通OK无额外分拣。抓手有效同号沿用、失效重建、本次确认，翻面无选择；raw/R反馈只通信并沿013同采样源。

## 直接设计影响及同步状态

| 原活动要求/缺口 | 新设计与实际文件落点 | 责任功能/同步阶段 |
| --- | --- | --- |
| count克隆/连续编号、无稳定格位、物理号<=容量 | 011RC10、012layout-design/data-model/API及014research | 014唯一模型/012消费，Phase 1已写；代码/tasks未改 |
| 任意OK目标/旧特殊免搬、质量OK代回放 | 014execution/cleanup、011execution-state、012editor/API，008execution/plan、010common | 当前设计已修正文义/引用；实际替代删除待tasks/implement |
| 特殊计划存在但入口拒绝/盘末排序/最后件代盘 | 014scope执行和safe/聚合门、011活动plan/state | Phase 1已设计；实际端口/注册/预算/投影未接通 |
| 两次同相机组融合/worker/投影键碰撞 | RC10.4、014EX14及008/010受影响合同 | 共同StageId设计已同步；实际消息/消费者/扫描待任务阶段 |
| UI成员与部位机械实体可能混淆 | 012两预览/navigation-preview/LD04及006映射 | 业务区别确定，导航表达待用户确认，不产品落实 |
| 旧012独立根/011统一合入仍写当前 | 012spec/plan/两清单路径、shared-integration/plan-handoff；research/quickstart旧段标历史 | 当前主项目/同会话责任已纠正，历史日期/证据保持 |
| 新grab/R可能重建高频采集或mask溢出 | 014EX14、011PLC、009business-device/protocol-maintenance | 设计内部UInt128有限key迁移，保单源/期限/009扫描；013文档/代码/证据未动 |

未重新写维护需求说明或宪章：前轮9.0.0及REQ最新原槽规则与本设计一致，本轮不重复重写。当前014未生成tasks；现有任务文件全部原字节保留。后续共享代码前须任务阶段实际承接所有准确字段/接口/消费者/编译迁移和扫描，不能把登记当实施完成。

## 清理及最小验证

具体当前代码、直接消费者、替代删除条件、仍有效的历史reader/保护/负例见cleanup-and-consumers。保留HistoricalHandlingEvidence原JSON消费、LegacySpecialExit负例、普通源占据、实际pick提交/UnknownHeld/期限/取消/epoch/并发/冻结/历史保护；删除须替代真实接入后，不能仅删断言或失败证据。

最小集合V14-01—07见contracts/verification：受影响构建/typecheck/组件；矩阵与类型；真实API/SQLite往返/版本/F/冻结；至少两件特殊链+一条普通链；抓手/重复组/NGPending/safe失败优先同链或组件；持续009/010及原型正负例/执行完整性。无全量、全历史、组合穷举、013性能重测。没有新的通过报告或硬件Applied声明。

## 局部待确认/依赖

DUI02/03仅导航表达待用户审查；DEP01正式地址/型号承载、02格位及组成员与3D真实关联、03安全/固定取料角及容差、04真机相机光源应用，限制对应实际产品定位/运动/硬件结论。它们不阻无设备合法保存、完整重读和本次界面设计。没有默认地址、布局、抓手、角度/补偿；旧负载真实缺项需明确关联后才新写/准入，不能改013冻结输入。

## 实际修改清单

以下为本轮39项已写产物；另新增本交接和plan-design-receipt，共41个文件。完整before/after SHA与99项只读基线核对记录在plan-design-receipt，历史specify/clarify receipts保持原时点。预览HTML仅文档设计资产，不是产品代码。

| 路径 | 操作 |
| --- | --- |
| `specs/012-recipe-authoring/checklists/requirements.md` | 定向修改 |
| `specs/012-recipe-authoring/spec.md` | 定向修改 |
| `specs/011-plc-interaction-update/contracts/recipe-contract.md` | 定向修改 |
| `specs/014-special-part-rotation/research.md` | 新增 |
| `specs/014-special-part-rotation/data-model.md` | 新增 |
| `specs/014-special-part-rotation/contracts/execution.md` | 新增 |
| `specs/014-special-part-rotation/contracts/verification.md` | 新增 |
| `specs/014-special-part-rotation/quickstart.md` | 新增 |
| `specs/014-special-part-rotation/plan.md` | 新增 |
| `specs/014-special-part-rotation/cleanup-and-consumers.md` | 新增 |
| `specs/014-special-part-rotation/spec.md` | 定向修改 |
| `specs/012-recipe-authoring/plan.md` | 定向修改 |
| `specs/012-recipe-authoring/research.md` | 定向修改 |
| `specs/012-recipe-authoring/quickstart.md` | 定向修改 |
| `specs/012-recipe-authoring/data-model.md` | 定向修改 |
| `specs/012-recipe-authoring/plan-handoff.md` | 定向修改 |
| `specs/012-recipe-authoring/contracts/shared-integration.md` | 定向修改 |
| `specs/012-recipe-authoring/contracts/capture-and-gripper.md` | 定向修改 |
| `specs/012-recipe-authoring/layout-design-20261005.md` | 新增 |
| `specs/012-recipe-authoring/contracts/recipe-authoring-api.md` | 定向修改 |
| `specs/012-recipe-authoring/contracts/editor-ui.md` | 定向修改 |
| `specs/012-recipe-authoring/previews/dui-02-group-navigation.html` | 新增 |
| `specs/012-recipe-authoring/previews/dui-03-assembly-navigation.html` | 新增 |
| `specs/012-recipe-authoring/navigation-preview-20261005.md` | 新增 |
| `specs/012-recipe-authoring/prototype-baseline-20261005.md` | 定向修改 |
| `specs/006-frontend-station01-console/contracts/prototype-mapping.md` | 定向修改 |
| `specs/008-recipe-driven-inspection/contracts/execution.md` | 定向修改 |
| `specs/008-recipe-driven-inspection/contracts/api-results.md` | 定向修改 |
| `specs/008-recipe-driven-inspection/plan.md` | 定向修改 |
| `specs/010-recipe-execution-isolation/contracts/common-execution.md` | 定向修改 |
| `specs/010-recipe-execution-isolation/contracts/input-boundaries.md` | 定向修改 |
| `specs/010-recipe-execution-isolation/plan.md` | 定向修改 |
| `specs/011-plc-interaction-update/contracts/execution-and-state.md` | 定向修改 |
| `specs/011-plc-interaction-update/contracts/plc-communication.md` | 定向修改 |
| `specs/011-plc-interaction-update/contracts/verification.md` | 定向修改 |
| `specs/011-plc-interaction-update/plan.md` | 定向修改 |
| `specs/009-plc-protocol-isolation/contracts/business-device.md` | 定向修改 |
| `specs/009-plc-protocol-isolation/contracts/protocol-maintenance.md` | 定向修改 |
| `specs/012-recipe-authoring/checklists/architecture.md` | 定向修改 |
| `specs/014-special-part-rotation/plan-handoff-20261005.md` | 新增 |
| `specs/014-special-part-rotation/plan-design-receipt-20261005.json` | 新增设计回执/摘要清单 |

静态核对只检查文档链接/模板、摘要及只读边界，不运行软件测试。99项登记基线（含产品/脚本相关源码、feature.json、现任务与013规格/plan/验证）摘要未变；原型/协议原件重新核摘要不变。保护核对范围是登记清单，不冒称整库软件验证。停止在Phase 1设计及导航待审，不进入checklist/tasks/implement。


## 当前标准深度审查后增量（2026-10-05，保留上方Phase 1原交付）

本轮speckit-checklist分别显式014/012 NoPersist核目录。I01—05（特殊来源、当前正文、必需代表输入、编辑JSON/source上下文、StageId编辑定位）已定向设计修订关闭，详见design-review-20261005.md。014architecture22项新建；012实际原28项之后追加14项CHK029—042，全部新条目[ ]，历史事实/任务勾选不变；辅助评价满足不等正式审批/软件通过。

两导航表达仍待用户确认，现场DEP只限制其依赖动作。实际后端/来源准备、消费者迁移、删除及必要测试是后续正常实施任务；当前未代码/构建/运行，未生成tasks或进入implement。此前41份设计清单及原plan-design-receipt仅记录当时交付，不覆盖本轮修改；本轮最终文件和SHA以checklist-review-receipt及design-review末尾清单为准。


## 2026-10-05任务拆解交付（后于标准深度设计审查，分析前）

当前根E:/dzk/gaode-1，同会话014共同后端/012配套负责。014新增T001—T018全部未勾；012保留原T001—T036全部正文/完成状态，追加T037—T049全部未勾。采用当前RC10/1.5、新写正文4/记录1.5/冻结3和API-L00/L01/L01a，不重做spec/plan/checklist。012 architecture仅修范围笔误CHK029—038→CHK029—042，历史评价与全部复选框保持。

本轮确切文档：specs/014-special-part-rotation/tasks.md（新）、specs/012-recipe-authoring/tasks.md（追加）、specs/012-recipe-authoring/checklists/architecture.md（单处文字）、014 plan-handoff-20261005.md与012 plan-handoff.md（追加当前任务状态），另tasks-generation-receipt-20261005.json（本轮生成收据）。共同合同、源码、产品/测试/脚本、预览原件、013、feature.json及旧证据未由本轮修改。

共享文件只有一个编辑任务；共同字段/校验/冻结/执行/通信014唯一承接，012只存储/HTTP/UI消费。首次实际构建前迁移真实引用闭包types/ports/Worker/nullable/schema/Host/StorePrep/测试/脚本和扫描；签名/消费者首批不等全部运行或DUI导航。真实来源014:T005.SourceReady→012:T039.PreparedSource→实际同库结果为T005完成证据，不以Unavailable代准备，也不形成源准备验收循环。

真实保存/重读/版本/弹窗Core在012:T045提前交。DUI02/03仍待明确确认，只阻对应导航子交付与验收；整任务不得因CoreReady提前勾。014:T016唯一启动特殊/普通代表，012:T045/T046先交Ready，T047.RunEvidence在运行后形成；最终证据不反作启动前置，014:T018仅消费对应RunEvidence，不等待待批导航，两最终报告不互等。

本记录仅任务生成状态，未预写analyze通过。完成生成后分别显式speckit-analyze，只读复核；发现问题只在回复报告，不回写本记录/任务。生成清单、摘要、旧字节/勾选保护和只读基线核对见014 tasks-generation-receipt-20261005.json。无构建/测试/设备/数据库/Git操作，无新实施勾选。
