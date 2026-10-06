# 014/012标准深度设计审查与定向修订

日期2026-10-05，工作根E:/dzk/gaode-1。审阅对象为014新Phase 1及012本次布局/特殊配套增量；本会话统一负责共同后端和配套设计。不是specify重跑、plan重建或软件测试。user已给标准深度、重点、界面审批及停止边界，未重复提问。

Spec Kit分别显式014/012执行check-prerequisites -Json -Template checklist-template并核实际路径；调用内存加NoPersist，原脚本/feature.json未改，未虚构Git分支。当前宪章9.0.0，checklist前后hooks为空。两checklist用于需求/设计质量，不判代码实现通过。

## 发现、影响、最小修订与关闭依据

| 问题 | 原准确位置及影响 | 本轮最小修订/当前落点 | 修订后结论 |
| --- | --- | --- | --- |
| I01 特殊新建来源不准确 | 012 API“012 HTTP信封与共同对象映射”/原L01仅泛称真实来源，未给kind请求；当前只读RecipeEndpoints.Authoring.cs:13及154—170实际仅Model+UnitKind筛选、无效SourceRecipeId可能被忽略；frontend configure/basic仅UnitKind。相同型号普通/特殊混源或特殊入口未定义 | API-L00给EditorDraftRequest Model/ScenarioId/UnitKind/InspectionKind/可选SourceRecipeId、合法Route表、一次Snapshot精确来源、指定ID不相容拒绝、类型切换清上下文；catalog只新增实际kind投影。RC10.5唯一校验。第5项明确014真实配置、012同SQLite/StorePrep共同保存准备义务；不能以Unavailable替代首次主流程 | **设计关闭**；真实来源文件/配置和实现仍需后续准备，不称源码已修 |
| I02 当前正文3/4冲突 | 012 API“身份/RC08字段与正文透传”仍写当前保存3/新建本次候选必须3；Data-model版本表仍称3当前；共同RC08版本表与RC10不一致。会使新布局字段或正确新结构被旧测试拒绝 | 当前新建/编辑保存统一4、记录1.5、新冻结3；历史2/3和旧冻结按原版本读取。修API、data-model、capture消费及共同RC08/RC09明确历史范围，保旧实际实施/验证。Cleanup与V14-CONTRACT登记准确测试迁移 | **设计关闭**；无当前规范要求本次写3，3保留只作历史事实 |
| I03 代表链证据不足 | 014 Verification V14-03原仅两参与件，允许结果不为两OK；重复组只“可并入”。Quickstart/plan据此不能必然证明两原槽不串件及重复组四次采集 | V14-03/V14-INPUT明确一条至少两件实际OK，各origin/placed/safe/必要提交，U2动作在U1实际闭环后；本次优先AB/AB同链、StageId1/2各两相机、每件4/两件至少8capture。CD/CD仅既定必要组件替代、实例不可省。NGPending/错误反馈/保存/回放失败优先组件；同步plan/quickstart | **设计关闭**；输入/Worker实际OK和采证未执行，不能伪造结果 |
| I04 候选与后续新格初始化不闭合 | API-L01先前Definition直接RecipeDefinition，而新候选XYZ/参数需null；Positions清空后未明确新增格如何取隐藏元数据，可能请求还未映射就解析失败或丢失引用 | API-L00响应authoringContext、L01携SourceRecipeId/SourceVersion+JsonElement Definition，L01a定义同源元数据建立新格，source变化显式重读；SourceVersion仅编辑来源引用，保存并发仍原ETag。正式检查/保存仍唯一严格4，不补0/不第二模型 | **设计关闭**；薄HTTP中间态不是另一个业务保存结构 |
| I05 重复组编辑定位不准确 | L01原请求无StageId；当前只读Authoring.cs CameraPair路径只按Material/LocalFace、要求targets.Length=1。重复组同面合法时会拒绝或覆盖另组 | L01/L01a携带程序StageId，按Stage/Material/LocalFace及对象/Camera精确定位，缺/错Stage拒绝，不首条兜底；LD02、Editor UI、Cleanup/V14-CONTRACT同步 | **设计关闭**；StageId是原组卡片上下文，无新技术阶段控件 |

“关闭”是文档设计缺口关闭；当前产品/测试仍原代码，所有替代/构建/软件证据是后续正常实施义务。未删除有效保护或旧失败证据。

## 其余重点复核

| 项目 | 设计依据/结论 | 尚限制内容 |
| --- | --- | --- |
| 稳定格/区号/实体/3D | RC10.1/2：CellId行列稳定，区域号排序投影；取消/改区只本格，物理号不能由显示号/Capacity求得 | 真实3D及组成员关联来源DEP02 |
| 成组与半成品 | ForObject独立成员与PhysicalEntity整体，LD04与两预览一致，部位切换不拆搬运实体 | 仅具体导航表达待用户确认 |
| 冻结与逐件范围 | EX14同全盘Frozen/PlanRevision，scope不重规划/重新身份；所有参与/排除事实齐才盘终态 | 后续实现scope/聚合消费者，非设计缺口 |
| 普通/特殊OK | 普通NoMoveRequired；特殊SortingGripper实际ReturnToOrigin+pick/保存/placed/safe，失败不下一件 | 真实动作与硬件证据未运行 |
| 选择/保存/期限 | 两用途1/2显式，epoch有效同号沿用，失效本次确认，flip无选择；true pick gate/UnknownHeld/预算deadline/cancel不放宽 | 正式映射/安全/容差DEP01/03 |
| 采集组身份 | RC10.4/EX14-05 StageId贯通fusion、workload、worker、保存及查询，CoordinateEpoch不充当组号 | 代码/消息/测试迁移待任务阶段 |
| 009/010/013 | raw仅通信、同Pump/WaitGroup/有限read plan；内部mask迁移有消费者义务，013策略/偏差不变 | 不重新测013，真实正式地址仍缺 |
| 删除与门禁 | Cleanup明确直接consumer/正确负例/合法历史reader/测试迁移；V14-CONTRACT/07必需发现执行及关联证据 | 仅后续实际替代后删除，不以名称/测试失败删除 |

## 清单与辅助评价

- [014 architecture](checklists/architecture.md)：新建22项CHK001—022，21满足、1部分满足（DUI导航），0不满足；全部新项[ ]，不是正式审批或软件结果。
- [012 architecture](../012-recipe-authoring/checklists/architecture.md)：现文件**原有28项**与所有历史Notes保留原字节前缀；本轮追加14项CHK029—042，13满足、1部分满足（CHK039，DUI导航），0不满足，新增全部[ ]。旧完成与requirements勾选不改，不把旧24/28评价计新增通过。

条目围绕要求的完整性/清晰度/一致性/可衡量/覆盖/依赖与冲突，不写实现操作测试步骤。追溯覆盖全部36项。标准深度仅当前主流程，不扩恢复平台/边界穷举。

## 分开记录剩余内容

1. **已关闭设计问题**：I01—05，位置及证据如上；特殊新建/正文目标/最小代表输入准确，未留影响这些主流程的同类文档冲突。
2. **待用户确认导航**：DUI02/03只待具体成员/部位导航表达。方案关联/原型范围已审，无未授权技术选项或机械实体变化；本轮不修改两个待审HTML、不改来源、不实现产品UI，不自行批准。
3. **局部外部输入**：DEP01正式地址/型号承载；02格位及组成员3D真实关系；03工位安全、固定取料角/容差；04真机相机/光源物理映射/应用证据。限制对应现场定位/派发/硬件Applied，不能冒称正式互通，不阻已有合法来源的离线保存。
4. **后续正常实施任务**：来源真实配置准备及同SQLite落地、准确请求/UI映射、正文4/冻结3与全部消费者迁移、scope/worker/结果投影/公共取放接口、实际删除/扫描登记、必要软件验证。它们尚未实现不是本轮需求再次待确认；不生成任务ID/勾选。若部署所需真实配置值缺失，记录具体源/能力/责任，仅限该新建部署，不用测试默认兜底。

## 定向一致性复核与边界

本轮修订后仅作一次必要静态文档复核：活动当前写4/历史2-3/旧冻结原版本一致；kind→route→source→candidate→layout上下文一致；至少两实际OK与重复组实例必需；scope/安全/身份保护保持；所有新条目未勾、旧前缀/任务不变。准确结果和before/after SHA在checklist-review-receipt。静态文档核对不等产品测试。

本轮没有产品/测试代码修改、构建、测试、设备、运行库操作或Git写操作；feature.json、旧任务及013成果保持。没有正式软件或硬件通过结论，不进入tasks/implement。

## 两份待确认预览

- [DUI02成组成员](../012-recipe-authoring/previews/dui-02-group-navigation.html)
- [DUI03半成品检测部位](../012-recipe-authoring/previews/dui-03-assembly-navigation.html)

预览是单独待审设计资产，不是客户来源原件、正式保存/参数或真实render验收。本轮停止，提交设计审查。


## 本轮实际文件清单

共18个文件：下列17项加设计审查回执本身。上一阶段41份设计清单保持当时记录，未重写原plan-design-receipt；本轮只交本次增量。

| 文件 | 操作 |
| --- | --- |
| `specs/012-recipe-authoring/contracts/recipe-authoring-api.md` | 定向修订/追加当前记录 |
| `specs/012-recipe-authoring/data-model.md` | 定向修订/追加当前记录 |
| `specs/012-recipe-authoring/contracts/capture-and-gripper.md` | 定向修订/追加当前记录 |
| `specs/011-plc-interaction-update/contracts/recipe-contract.md` | 定向修订/追加当前记录 |
| `specs/012-recipe-authoring/layout-design-20261005.md` | 定向修订/追加当前记录 |
| `specs/012-recipe-authoring/contracts/editor-ui.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/contracts/verification.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/quickstart.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/plan.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/research.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/cleanup-and-consumers.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/checklists/architecture.md` | 新增 |
| `specs/012-recipe-authoring/checklists/architecture.md` | 追加审查，历史前缀保持 |
| `specs/014-special-part-rotation/design-review-20261005.md` | 新增 |
| `specs/012-recipe-authoring/contracts/shared-integration.md` | 定向修订/追加当前记录 |
| `specs/012-recipe-authoring/plan-handoff.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/plan-handoff-20261005.md` | 定向修订/追加当前记录 |
| `specs/014-special-part-rotation/checklist-review-receipt-20261005.json` | 新增前后SHA与只读核对回执 |
