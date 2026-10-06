# 012统一澄清、定向同步与交接记录（含阶段追溯）

> 主项目集成补记（011，tasks轮）：本轮13份最终增量已实际合入，详见[接收记录](../011-plc-interaction-update/tasks-handoff-20261003.md)。下文012交付时“待合入”保留来源时点，不代表当前集成状态。

> 历史主项目接收说明原文：主项目接收说明（011集成，2026-10-03）：下文保留012澄清交付时状态；本清单15份已交付文档已逐文件接收。实际目标和结果见[011集成记录](../011-plc-interaction-update/clarification-sync-20261003.md)。012后续plan产物未在此交付，未复制也未标记完成。

日期：2026-10-03（Asia/Shanghai）。执行身份：012。以下澄清记录保留当时结论与未交接事实。

**当前阶段更新（checklist）**：已接收主项目recipe-contract/1.2、station01-execution/1.0、011-verification/1.1及四份混合API，83份共享依据已刷新核摘要；IC-01—06逐项处理，G-02/03已随1.2关闭，具体剩余G-01见[接入合同](contracts/shared-integration.md)。此前本文件列出的15份澄清文档已由011合入主项目（011报告B1），原“未合入/未接收”保留为当时历史。本轮定向修订独立SQLite/共同保存/字段与状态映射，生成architecture并辅助评价，全部24框未勾选；当前23满足/1部分满足，保留首次1.1时21/3评价；011已接收Phase 1首版并合入历史basis-receipt；当前13份最终修订增量仍待011接收/统一合入。两个输入适配文件归012唯一编辑。完整当前状态见[plan-handoff](plan-handoff.md)及[basis-receipt](basis-receipt.md)。下方“本轮”均指前轮clarify时点，不倒改历史事实。

前轮依据：用户最终统一澄清与同步指令及speckit-clarify。

实际根目录：`E:/dzk/gaode-012-recipe-authoring`。显式 `SPECIFY_FEATURE_DIRECTORY=specs/012-recipe-authoring`；唯一一次check-prerequisites使用 `-Json -PathsOnly`，已读脚本确认其调用NoPersist，返回的FEATURE_DIR/FEATURE_SPEC均位于本副本；未写feature.json。

本轮新增提问0个，落实用户直接给出的5项答案。只修改当前副本的012责任文档；部分006已有文档/前端说明从主项目只读复制后修订，不创建012 plan/tasks。未进入implement，未运行构建、测试、设备、数据库或Git写操作。主项目及011副本无写入。

## 修改文件与确认位置

以下是本轮内容变更清单；006现存plan/tasks仅定向修订，T编号与勾选原样。表中的“完成”仅指012副本文档，不指主项目集成或功能实现。

| 文件（相对012副本根） | 实际修订内容 |
| --- | --- |
| [specs/012-recipe-authoring/spec.md](spec.md) | Clarifications五答案；US1-B、US2-B、US3-A/C/D/E/F；FR-003/004/008/011/013及FR-017—020；实体、快照、SC-003/007/008、V03、OPEN关闭、SH/SYNC与原型适用范围 |
| [specs/012-recipe-authoring/checklists/requirements.md](checklists/requirements.md) | 11/16→16/16，5个勾选变化；当前依据、历史11/16结论与设计/交接/软件未验证限制 |
| [specs/006-frontend-station01-console/spec.md](../006-frontend-station01-console/spec.md) | FR-001/008/010及新增FR-012；SC-001/003及成功条件；授权弹窗、真实保存/F、运行阶段、更多面/E与三区域/异常显示 |
| [specs/006-frontend-station01-console/contracts/api.md](../006-frontend-station01-console/contracts/api.md) | 目录/选用表及业务能力表；保存生效、完整读取/写入、料盘号唯一与身份区分、模型/校验唯一、实际状态/处置；新签名留plan |
| [specs/006-frontend-station01-console/contracts/prototype-mapping.md](../006-frontend-station01-console/contracts/prototype-mapping.md) | 当前说明、禁止项、弹窗映射行、原型差异保护及现有区域的更多面/E/三区域/异常槽号映射；不照搬V3 |
| [specs/006-frontend-station01-console/contracts/gaps.md](../006-frontend-station01-console/contracts/gaps.md) | FE-C07登记目录/读写实际接入缺口、012/011分工及禁止伪实现；修正后段显示顺序 |
| [specs/006-frontend-station01-console/contracts/host.md](../006-frontend-station01-console/contracts/host.md) | 历史009纯绑定限制与当前012授权弹窗范围分清；宿主/归档/无关页面保护保留，不新增宿主能力 |
| [specs/006-frontend-station01-console/data-model.md](../006-frontend-station01-console/data-model.md) | 区分前端编辑值/保存内容/运行只读投影；共同配置不变成第二模型；真实阶段、面/姿态、处置及物理槽号 |
| [specs/006-frontend-station01-console/plan.md](../006-frontend-station01-console/plan.md) | 摘要/页面职责/P03/P11/P12/阶段顺序/验证目标；012读写消费及删除失效逻辑义务，不代替012新plan |
| [specs/006-frontend-station01-console/tasks.md](../006-frontend-station01-console/tasks.md) | T048/T049对应消费及清理义务、原型门禁、阶段显示、现行验证范围；T040全量命令明确为历史，勾选和编号保持；历史证据不回写 |
| [specs/006-frontend-station01-console/research.md](../006-frontend-station01-console/research.md) | 原型差异/只读归档、基本读写与唯一业务路径、最小验证；原型摘要按006已核验值补齐遗漏末位0，不改归档 |
| [specs/006-frontend-station01-console/quickstart.md](../006-frontend-station01-console/quickstart.md) | 固定全量命令改为后续plan选择受影响入口，真实保存与联合代表及授权原型差异；摘要同上；无命令在本轮执行 |
| [frontend/README.md](../../frontend/README.md) | 006/012职责、弹窗与真实保存/F、原型保护、最小验证和清理义务 |
| [frontend/tests/README.md](../../frontend/tests/README.md) | 替身仅组件、正式联合证据、必要失败/架构/原型门禁、禁止零发现/Skip假通过及错误测试豁免 |
| 本文件 | 同步位置、另一负责人交接、旧逻辑核查、分类覆盖及完成边界 |

从主项目补入但未改内容的 `006/diagnostic-validation.md`仅为只读历史引用，未作为本次运行证据。012原有AGENTS、模板/脚本、宪章/对齐记录及011 change-request副本未修改；后两类仅来源快照，不宣称已收到011最新全量同步。

## 五项确认的正文落点

| 确认 | 012落点 | 006/前端消费落点 |
| --- | --- | --- |
| 保存成功后后续F用新内容、已冻结运行用旧内容 | US2-B、US3-B/C；FR-006/009/011；SC-002—004；OPEN-01关闭 | spec FR-010/012、api配方读写/目录表、data-model编辑/保存/冻结分离、plan/tasks T048/T049 |
| F扫码=料盘编号且不同配方不可共码 | US1-B、US3-A/D；FR-003/008；SC-003；OPEN-02关闭 | api唯一性/身份行、gaps FE-C07、prototype弹窗身份、目录/读写消费及失败显示 |
| 三区域、OK不搬、NG/Pending目标及异常原槽退出 | US3-F；FR-004/017；SC-008；OPEN-03分拣子项关闭 | spec FR-012、api真实处置、prototype/data-model现有结果与异常物理槽号；plan/tasks当前分拣后下料显示 |
| 四面后可选额外E姿态 | US3-E；FR-013/018；SC-007；OPEN-03 E子项关闭 | 输入与目录完整读取、面/姿态区分、现有状态媒体事实及联合代表，不能写PLC原始编码 |
| 更多检测面仍AB/CD、四面3CD＋1AB保持 | US3-E；FR-013/019；SC-007；OPEN-03更多面子项关闭 | 配方输入与读取/目录、已有媒体及运行绑定、plan/tasks/quickstart最小代表，不穷举组合 |

已消除012责任正文中的“保存生效/扫码规则待确认”“V3三项工艺未批准”“无编辑保存/仅选用”“先下料后分拣”“授权变化须重写原型归档”和“本次固定全量测试”冲突。历史原型、历史发布包和原始失败证据未改；当前引用已说明仅证明当时范围。

## 前轮clarify跨负责人交接状态（历史，当前见plan-handoff）

只读已看到 `E:/dzk/gaode-1/workcopies/011-plc-interaction-update/specs/011-plc-interaction-update/spec.md`同样落实五项统一决定。该观察不是011全部文件交付/验收，也不是共享合同已冻结；012没有复制其变更覆盖自己的副本，不能声称全项目同步完成。以下向调度提交具体交接需求，未收到逐文件接收记录的保持“待交接”。

| 编号 | 011负责的具体文件/位置（相对其副本） | 012需要承接的变更与当前状态 |
| --- | --- | --- |
| H01 | `软件需求规格说明书/软件需求规格说明书_V1.1_开发范围版.md`相机/点位、§11及配方/分拣条款；`.specify/memory/constitution.md` P03/P08/P12/P13；`constitution-alignment.md`当前摘要 | 同步更多面、四面3CD＋1AB、可选E、三区域/姿态异常、保存/F/快照和最小验证；012不编辑这些文件，旧副本仅来源快照；待接收011成套变更 |
| H02 | `specs/002-plc-xyz-recipes/spec.md` FR11/目录补充；`contracts/recipe-execution.md`目录/冻结及USR-E准入 | 主目录现有“无活动运行重新装载”“禁止静默选最新版”、两类四面及固定F条款须与新规则对齐；012目录/保存供新内容，011冻结在途内容。该文件混合模型/执行义务由011独改，012提供消费需求 |
| H03 | `specs/008-recipe-driven-inspection/contracts/api-results.md`目录/expectedRecipeRef/结果；`spec.md`、`data-model.md`、`contracts/execution.md`及相关已有plan/tasks | 移除活动“本轮不提供编辑保存API”限制，标清012负责读写；已有选用意图不能强制后续F用过期内容；完整读取不等于仅摘要。承接更多面/E、OK留原槽、NG/Pending及异常槽号；混合文件由011负责，未向其文件写入 |
| H04 | `specs/001-station01-public-preparation/spec.md`及`contracts/configuration-time.md`；`specs/003-plc-latest-protocol/contracts/whole-tray-workflow.md`及状态/API合同 | 首次3D提供F的XY，未匹配仅阻断产品动作；分拣后下料、真实阶段/异常槽号输出；既有F/型号码语义不混用。012已修正前端消费目标，生产者接口/新阶段名待011交接，不自造字段/枚举 |
| H05 | `specs/007-station01-integrated-loop`当前spec/plan/tasks与运行说明；`specs/010-recipe-execution-isolation/contracts/input-boundaries.md`、`common-execution.md`、`verification.md`及受影响009门禁合同 | 沿共同执行/通信隔离，正式保存配方供联合代表，不用固定测试编号补缺；维护必要失败/保存/取消/期限保护，共用更多面/E/三区处置证据，不全量重验 |
| H06 | `README.md`活动协议/配方接入说明、`高德_文档/通信协议使用说明.md`及后端当前配方示例/验收引用 | 主目录仍有旧协议唯一依据、File路径/旧范围示例；由011定向修订活动引用，历史发布包/原始来源不改。012已更新frontend开发/测试说明，不另改共享根README |
| H07 | `backend/src/Gaode.Host/Api/RecipeEndpoints.cs`等同时含目录/plan/bind的共享文件（后续代码阶段） | 只读已见同文件含catalog及绑定执行；012负责目录/读写、011负责绑定语义。plan需经调度确定该物理文件唯一编辑者，由另一方提供变更需求；未定前不并行覆盖，本轮不改代码 |
| H08 | 共同配方/校验/匹配/运行快照合同与联合证据清单 | SH-01—06业务要求已确定；字段结构、接口签名、存储技术及普通实现由双方plan衔接。需交付可供012读写/目录使用的同一合同，未交付前不另造模型/校验/执行器 |

`packaging/windows-local-20260927`及旧Q运行清单、006 evidence和历史tasks仅证明其发布/测试时范围，未重写旧包或历史事实；不能用其中旧3＋1集合、预置目录或固定Test入口替代新保存/更多面/E联合验证。当前前端说明已取消把全量历史命令设为本次门槛。

## 后续必须核查并实际清理的旧逻辑

这里只读定位候选，不判定所有旧代码均无用，也未执行删除。FR-020、006已有plan/tasks及frontend说明已登记义务；真正被替代且无有效用途者，在替代实现完成并承接有效责任后必须实际删除。

| 位置（主项目只读观察） | 已见事实与后续处理 | 需保留/核对的责任 |
| --- | --- | --- |
| `frontend/src/runtime.js` loadRecipeCatalog、sameRecipeRef、legalPreparedRequest、recipeChooseButton | 当前主要目录摘要/选用；存在S1/P01旧prepared入口与对所选引用的固定核对。按新保存/绑定需求核实际入口及宿主/脚本消费者，删除被替代且无用途的旧限制或旁路 | 后端授权、请求身份、F真实绑定、冻结运行显示及合法历史读取；不能为新保存放宽正确准入 |
| `frontend/src/runtime.js`约230行阶段显示 | Sorting待处理依赖UnloadPreparationCompleted，UnloadPreparation待处理依赖DetectionCompleted，反映旧顺序；替换后删除错误推断 | 以011实际阶段/事实为准，不根据本地排序或质量猜动作完成 |
| `frontend/src/pages/a.html` recipeModal及原型演示脚本 | 已有弹窗是原编辑展示/预置选用形态；核并清除被正式制作替代的演示字段值/无用脚本与预置成功，不复制V3流程生成 | 实现副本可按授权差异修改，归档与V3源文件只读；基本弹窗交互/无关页面保护保留 |
| `backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs`、`RecipeCatalogFactory.cs`、`SemanticRecipeInputProvider.cs`及装配/配置消费者 | 当前含构造时文件快照、Review/File选择、Test语义输入及码映射/测量偏移输入。012核新保存目录生产者，011核旧业务含义；替代后无用途的固定映射/测高依赖、测试特权和孤立配置应实际删除 | 不能先定存储技术或无差别删除文件；共同校验、唯一性、生产限制、有效叶适配与历史读取要承接；同文件单一编辑者 |
| `backend/src/Gaode.Host/Api/RecipeEndpoints.cs`与目录/绑定调用方 | 当前无新建/完整读取/编辑保存端点，catalog与plan/bind同文件；后续不得保留假保存API或另一套执行路径 | 保留正式绑定/预算/取消及必要保存门；文件责任按H07交接 |
| `frontend/scripts/verify-prototype.ps1`与`frontend/tests/us1/prototype-console.test.ts`、`prototype-all-pages.test.ts` | 前者逐页源副本字节比对会拒绝授权弹窗差异；后两者主要只核存在/特定标志，不能证明完整新授权范围。定向承接新差异和未授权区域检查，替代无效断言/孤立测试 | 归档哈希、三页清单、无关页面保护和正确架构拒绝能力继续；不能关闭检查或因失败删测试 |
| 011模型/校验/规划/分拣/设备协议文件 | 旧面数硬限制、旧E/OK搬运/先下料逻辑及协议实现按011真实消费者审计；012只提供配置/显示需求 | 011唯一负责，012本轮不修改；保留有效通信隔离、共同执行、身份/保存/期限/取消 |

特别区分：runtime.js的localStorage目前保存“不可信run引用，随后重新GET事实”，不是正式配方保存。本轮没有把它误判为应无条件删除的假配方库；是否受替代影响须核消费者。禁止用localStorage保存正式配方的要求仍成立。

历史失败日志、旧TRX/报告/媒体/原始数据不得删除或改成通过。以注释、永久关闭开关、备用实现或新兼容层保存错误逻辑不算清理完成。

## 质量变化与覆盖扫描

质量清单：11/16→16/16；新增通过“无澄清标记、需求可测且无歧义、场景完整、全部FR有验收依据、成功标准覆盖充分”5项，无回退/剩余未勾选。前轮11/16结论保留，不倒改历史。软件结果仍全部未在本轮执行。

| 澄清分类 | 状态 | 依据或剩余边界 |
| --- | --- | --- |
| Functional Scope & Behavior | Resolved | 基本弹窗/保存与新增配方能力明确，五决定落实；范围无页面扩张 |
| Domain & Data Model | Resolved | 料盘编号唯一、配方身份/型号区分、生效/冻结、面/姿态/区域明确；结构属于plan |
| Interaction & UX Flow | Clear | 同一弹窗三部分、真实状态/失败、已有身份权限和UI风格边界保持 |
| Non-Functional Quality Attributes | Clear | 必要持久诊断、授权、有限等待/取消与保存保护保持；不增加未经要求的性能/容量/恢复目标 |
| Integration & External Dependencies | Deferred | 011共同接口、单文件责任及文档交付由后续plan/调度衔接，业务含义已确定 |
| Edge Cases & Failure Handling | Resolved | 重复码、未匹配、必要读写失败、运行隔离及姿态异常明确；完整边界矩阵延期 |
| Constraints & Tradeoffs | Deferred | 字段结构、签名、存储等普通实现选择留plan，不作为用户业务未决 |
| Terminology & Consistency | Resolved | 检测面不等于E姿态，OK不搬与检测期必要动作分开，三种身份/三区域用途明确 |
| Completion Signals | Resolved | US/FR/SC/V完整承接，真实联合代表与清理义务可核验 |
| Misc / Placeholders | Clear | 当前spec无澄清标记；OPEN为已关闭追溯，不残留待批准工艺 |

需求准备度：可以进入speckit-plan，由调度安排下一次调用。Deferred是设计/交接工作，不要求再跑业务clarify；未接收共同合同会限制依赖其字段的实现及联合验证。正式地址/恢复/安全控制沿011原延期，仅限制实际依赖动作，不阻断无依赖工作。

文档核对结果：5条澄清答案，当前spec无NEEDS CLARIFICATION标记；质量清单16项已勾选且条目文字/顺序保留；006的50项任务编号和原勾选一致。012的plan/tasks及feature.json均不存在；本次只读核对的13份主项目来源文件、18份受保护副本文件及V3源文件摘要未变化。未运行软件验证。此副本是经授权建立的最小文档副本，003/008/010等跨功能相对引用尚未完整复制，在主项目来源位置可只读解析；这不代表已接收011新版合同。

本轮止于澄清与文档同步。下一命令建议为调度授权后的 `$speckit-plan`，本轮未执行。
