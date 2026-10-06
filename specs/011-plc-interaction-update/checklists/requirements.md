# Specification Quality Checklist: PLC交互与共同检测流程更新

**Purpose**: 审阅011规格质量与准备度，供调度会话安排后续工作  
**Created**: 2026-10-03（Asia/Shanghai）  
**Feature**: [spec.md](../spec.md)  
**Review Ownership**: 011 plan阶段重新核对当前准备度；specify/clarify原结论保留历史  
**Marker Semantics**: `[x]`只表示相应需求质量条件已满足，不表示实现、测试或生产接入通过。

## Content Quality

- [x] CHK001 No implementation details (languages, frameworks, APIs)：没有技术栈、接口签名、表结构或代码方案；协议和分工名称是用户指定约束。
- [x] CHK002 Focused on user value and business needs：围绕真实检测、异常槽位处置、顺序和可查结果。
- [x] CHK003 Written for non-technical stakeholders：以操作员/维护人员场景、业务行为及可观察结果描述；来源中的技术名称仅用于追溯边界。
- [x] CHK004 All mandatory sections completed：已按解析到的项目spec-template保留章节顺序并完成必需内容。

## Requirement Completeness

- [x] CHK005 No [NEEDS CLARIFICATION] markers remain：U-01—U-03已由2026-10-03最终统一指令解决；五项决定各有一条Q/A及正文落点，未再次提问。
- [x] CHK006 Requirements are testable and unambiguous：已确认业务语义可检验；字段/签名/存储留plan，正式地址、承载等延期按DEP逐项限定，不冒充确定值。
- [x] CHK007 Success criteria are measurable：SC-001—007规定代表链、目标一致、异常后动作数、可追溯性和必需验证完整性；未编造节拍/精度。
- [x] CHK008 Success criteria are technology-agnostic (no implementation details)：按运行、对象、动作、保存、可追查和错误拒绝结果判定。
- [x] CHK009 All acceptance scenarios are defined：AC-01—15覆盖保存/F唯一匹配/快照、OK/NG/Pending/姿态退出、更多面与可选额外E及主流程保护；现场未知输入未写成验收值。
- [x] CHK010 Edge cases are identified：只登记必要输入、姿态、保存、反馈、取消、关联及门禁失败，其余延期。
- [x] CHK011 Scope is clearly bounded：原Phase 1已完成，本轮为012最终接收/G-01/集成及tasks文档；不改代码或运行，后续1.3未交付消费增量不复制。
- [x] CHK012 Dependencies and assumptions identified：原U项保留解决记录；DEP-01—08区分延期通信/现场输入、已完成澄清合入、已接收合入012最终1.2设计与已接收1.3设计消费回执、待实际共同代码，不新增业务默认值。

## Feature Readiness

- [ ] CHK013 All functional requirements have clear acceptance criteria：确认业务FR有AC对应；DEP-02/05/06通信验收输入及共同代码/联合实施输入尚未齐备，1.3设计消费已接收，不能宣称全范围最终验收准备完成。
- [x] CHK014 User scenarios cover primary flows：首次3D/F、配置检测、异常退出、翻转放回与复查、E、分拣后下料及共同执行保护均有场景。
- [ ] CHK015 Feature meets measurable outcomes defined in Success Criteria：SC预期已按五项确认更新；已交付澄清和011共同设计完成不等于共同代码交付、延期输入和全部联合验收输入闭合。本项不是实现测试计分。
- [x] CHK016 No implementation details leak into specification：具体接口、报文布局、字段存储与执行器结构留给后续；禁止泄漏协议和测试知识是约束，不是新实现设计。

## 当前说明（tasks轮）

需求清单原14/16及CHK013/015未勾保持；012最终13份已接收/合入、原端口/身份/负责人修订已闭合，不能继续列为当前缺口。G-01生产端合同1.3 RC08已交，新设计消费回执及7份增量已接收合入；代码及现场/联合输入仍按具体范围保留。architecture对应Notes更新为15满足/0部分满足/0不满足，15框均未勾，历史评价保留。任务拆解是设计承接，不是实现或软件通过。详见[当前交接](../tasks-handoff-20261003.md)。

## 历史当前说明（前轮设计对齐；原plan勾选保留）

以下是前轮时点，当前接收状态以上文为准。

需求清单保持**14/16**，本轮不重新判定或修改勾选。012 Phase 1首版已经接收，具体差异与最终修订状态见[设计对齐记录](../design-alignment-20261003.md)；本轮辅助设计评价在[architecture](architecture.md)的Notes，新项全部未勾。88份澄清文件已实际合入主项目、四份混合合同已补齐，共同合同及Phase 1设计已交付；这关闭此前DEP-08澄清合入/物理文件归属部分。CHK013/015仍受正式通信输入及012最终修订/联合验收输入限制。设计可完成，不宣称实现、测试或生产通过。

当前共同定义/文件责任/最小M01—M11与删除义务见[plan](../plan.md)和[交接记录](../clarification-sync-20261003.md)。没有重复业务问题；本轮只读源码和文档核对，未运行构建、测试、设备、数据库或Git写。上一轮setup-plan仅在011副本显式选定功能；本轮checklist解析也显式指定该副本功能，主项目feature.json未切换。本轮before/after_checklist钩子为空，止于设计对齐/辅助审查，不生成tasks。

## 历史评审说明（2026-10-03 clarify）

以下保留上一轮当时的14/16、未接收及无plan状态，不当作本轮现状。

**11/16 → 14/16**。新增满足：CHK005、CHK006、CHK009；退回项：无；仍未勾选：CHK013、CHK015。五项业务问题已有答案，不代表设计、实现、测试、生产或跨会话同步通过。

| 尚未闭合 | 影响 |
| --- | --- |
| DEP-02、05、06：型号ASCII承载、全局速度参数、报警方向/类型 | 限制依赖字段的通信合同及互通验收；不限制共同业务模型、配置检测与无依赖正常链的plan |
| DEP-01、03、04：正式地址、恢复、安全控制 | 保持既有延期，不猜值，不恢复已关闭信号确认或增加前置 |
| DEP-07、08：012接收及主项目集成 | 本副本交接材料已列具体文件，尚无接收/合入证据；不能说联合保存/API/前端同步完成 |

011已确认范围具备进入plan的需求准备度，后续阶段仍由调度安排。本轮已定向更新维护中的需求、宪章及相关后端spec/contracts/已有plan/tasks；完整修改清单、同步落点及012待交接见[同步记录](../clarification-sync-20261003.md)。实际运行阶段及异常物理槽号由011输出，012只做既有界面必要绑定。

后续V01—V09及联合代表证据按受影响行为安排，承接真实保存、关联、有限等待、取消和架构负例。替代后清理义务见FR-023；不得删除失败证据、跳过失败或以无关联旧报告补齐。

本轮只作文件阅读与文档核对，未运行构建、测试、设备、数据库或Git写操作；无011 plan/tasks。显式功能路径已核对，feature.json不持久切换，before/after_clarify钩子为空。

## 历史评审原说明（本轮specify，2026-10-03）

以下保留上一轮11/16及当时U/DEP结论，描述当时授权与状态；不作为本轮现行规则。原未勾选为CHK005/006/009/013/015。原文：

本轮文档审阅结果：**11/16项满足，5/16项未完成**。未完成项不是测试失败统计；本轮未运行任何构建、测试、设备或数据库。

| 未完成项 | 规格中的具体依据或原文 | 影响与处理 |
| --- | --- | --- |
| CHK005/006/009/013 | U-01：“新工艺中OK和Pending分别是否自动分拣、适用对象及去向是什么？” | 影响对应处置、配方校验和验收；不得自动选用V3或旧例子的规则 |
| CHK005/006/009/013/015 | U-02：“是否需要固定额外第5个E扫码姿态”；U-03：“半成品是否扩展更多检测面” | 影响扩展面序/姿态、参数、预算与验收；已确认四面及扫码Z可独立审阅 |
| CHK006/013 | DEP-02：“承载未确认”；DEP-05：“类型、单位/倍率和作用范围缺失”；DEP-06：“方向/类型差异” | 只限制依赖它的通信定义与验证，不把建议、空值或表格初值定为最终规则 |
| CHK009/013/015 | 共享需求节：“本表是需求边界，不是已冻结的接口签名、存储结构或012保存规则”；DEP-07 | 011与012须经调度对齐同一合同后才能完整定义联合交接验收，本轮不写012文件 |
| 全局准备度 | “整体准备度未通过”“本会话完成specify后停止，等待安排” | 不宣称全部通过或可进入下一阶段；未决仅限制依赖部分 |

审阅还确认：

- CR D01—D16分别由FR-001—013及相应场景承接；来源文件哈希已核对。助手建议、样例和测试数据没有提升为生产准则。
- 旧规格冲突仅登记；CR、来源原件、其他规格、历史任务及原型保持原样。V3原文未在副本提供，仅记录用户明确指定的三项未决。
- 已关闭的额外握手、独立实际面号与旧信号逐项确认不重新设门槛；地址、恢复、安全控制等沿用延期状态。
- V01—V09是后续最小验证义务，优先复用现有测试及同次运行证据；不要求全量、配方穷举或009/010整套历史重验。
- 未用猜测消除未决项。当前失败项都关联具体未决输入，不通过改写“已确认”或重复空审查把它们勾选。
- 已读取副本 `.specify/extensions.yml`，`hooks: {}`，无before_specify/after_specify钩子可执行。
- 显式路径解析返回 `E:/dzk/gaode-1/workcopies/011-plc-interaction-update/specs/011-plc-interaction-update`；活动模板为副本 `.specify/templates/spec-template.md`。
- 用户本轮只授权生成两份产物，因此未持久切换 `.specify/feature.json`，未执行技能默认分支/Git动作或后续阶段。保留未决并停止也是用户本轮明确要求，不是技能要求额外审批。
- 后续文档更新与阶段安排由调度会话决定；本清单未完成项须在各自依赖工作前处理，不自动阻塞其他已确认工作。
