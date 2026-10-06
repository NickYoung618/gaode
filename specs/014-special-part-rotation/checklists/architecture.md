# Architecture Checklist: 014特殊旋转与012共同设计增量

**Purpose**：标准深度需求/设计质量审查，面向设计审阅者，任务拆解前使用；不评定软件实现通过。  
**Created**：2026-10-05  
**Feature**：[spec.md](../spec.md) / [plan.md](../plan.md)  
**实际目录**：E:/dzk/gaode-1/specs/014-special-part-rotation

**Review Ownership**：清单归审阅者所有；[x]仅表示需求质量获审阅认可，非实现完成。本轮新条目全部[ ]；用户授权辅助评价写入Notes，$speckit-implement只读取，不修改标记。

聚焦本次三项优先问题、唯一共同路径/原槽/身份、必要保护、原型授权边界与最小验收输入；不扩恢复/全量/013性能。用户已明确深度和边界，未重复澄清。

## Requirement Completeness

- [ ] CHK001 普通/特殊新建的业务选择、来源相容关系及完整可编辑候选要求是否明确且能贯通后续保存？ [Completeness, Spec §FR-001/015；012 API §API-L00/L01a；共同RC10.5]

- [ ] CHK002 首次特殊新建所需真实配置来源、准备责任及缺失时的局部限制是否已定义，且没有以不可用结论代替交付目标？ [Completeness, Spec §US1/DEP-014；012 API §API-L00第5项]

- [ ] CHK003 稳定格位、区域归属、区内显示号及检测顺序的含义和相互关联是否完整定义？ [Completeness, Spec §FR-005/015；共同RC10.1/2]

- [ ] CHK004 实体、成组独立成员、半成品检测部位及3D物理号的区别是否明确，不依赖未经确认的转换？ [Clarity, Spec §FR-012；共同RC10.1/2；Data-model §搬运与检测实体]

- [ ] CHK005 两用途抓手、共享工位、每件独立拍照及固定原槽放料关联的适用范围是否完整？ [Completeness, Spec §FR-002/003；共同RC10.1]

## Requirement Clarity

- [ ] CHK006 已填写候选、可保存正文、保存成功和运行准入的定义是否互相区分，未填项是否保持真实缺项？ [Clarity, Spec §US1；012 API §API-L00/L01a；共同RC10.2]

- [ ] CHK007 显示号、原槽身份、步骤及StageId的职责是否明确，足以排除重编号后关联迁移或重复组覆盖？ [Clarity, Spec §FR-005/007/015；共同RC10.1/4；012 API §API-L01a]

## Requirement Consistency

- [ ] CHK008 一个全盘冻结内容与特殊逐件范围完成的要求是否一致，件级完成是否明确不能代替盘终态？ [Consistency, Spec §FR-006/015；EX14-01/02/05]

- [ ] CHK009 普通OK无需额外搬运与特殊OK必须实际返回本件原始槽的要求是否无冲突且各有完成语义？ [Consistency, Spec §FR-008/US2-D/F/US4；EX14-02/03]

- [ ] CHK010 特殊下一件准入是否明确依赖前件必要保存、实际放料和安全位事实，回放失败的完成定义是否一致？ [Consistency, Spec §FR-006/013/US2-F；EX14-02/03]

- [ ] CHK011 当前新写正文4与历史2/3、旧冻结按原版本读取的要求是否在活动合同及消费文档中统一？ [Consistency, Spec §FR-015；共同RC10.3；012 API §RC08字段与正文透传/Data-model]

- [ ] CHK012 重复相机组的组身份是否贯通拍照、算法、必要保存与查询要求，不将坐标轮次当组号？ [Consistency, Spec §FR-007/SC-003；共同RC10.4；EX14-05]

## Acceptance Criteria Quality

- [ ] CHK013 至少两件实际OK分别返回自身原槽及安全位后才推进的验收输入与可关联事实是否明确？ [Measurability, Spec §SC-002/005；Verification §V14-03/V14-INPUT]

- [ ] CHK014 至少一个AB/AB或CD/CD实例的四次独立采集与参数不覆盖是否为必需、可衡量标准？ [Measurability, Spec §SC-003；Verification §V14-03/06/V14-INPUT]

## Scenario Coverage

- [ ] CHK015 有效同号复用、换号/失效重建、错误反馈阻断及翻面无选择的需求是否覆盖且一致？ [Coverage, Spec §US3/FR-009/010；EX14-04；Verification §V14-05]

- [ ] CHK016 普通/成组/半成品的既有面序、成员和批次处置要求是否明确保留，未被特殊逐件节奏替代？ [Coverage, Spec §FR-012/US4；Data-model；Verification §V14-04]

## Edge Case Coverage

- [ ] CHK017 必要取料保存失败、原槽放料/安全位失败及取消/期限/连接代次失效的局部阻断要求是否完整？ [Coverage, Spec §US2-E/F/US3-C/FR-013；EX14-03；Verification §V14-05]

## Non-Functional Requirements

- [ ] CHK018 通信隔离、单源采集及现期限/资源要求是否在新能力设计中保持，且未新增高频重复来源或性能承诺？ [Consistency, Spec §FR-013/014；EX14-04；009维护合同；013-acceptance/2]

- [ ] CHK019 最小验证的执行完整性、错误架构拒绝及旧证据适用范围是否定义，足以排除漏跑、Skip或无关联报告？ [Measurability, Spec §SC-006；Verification §V14-07/V14-INPUT]

## Dependencies & Assumptions

- [ ] CHK020 替代删除义务是否对应直接消费者与仍有效的历史读取/保护和契约迁移要求，而不是以失败为删除依据？ [Completeness, Spec §FR-016；Cleanup §本轮直接消费者与契约测试迁移义务]

- [ ] CHK021 缺正式地址、3D关联、安全/容差及硬件证据的具体影响是否局部化，未成为无设备保存的全局前置？ [Dependency, Spec §DEP-014-01—04；Plan §OPEN；共同RC10.2]

## Ambiguities & Conflicts

- [ ] CHK022 成组成员与半成品部位导航是否已有获确认的精确原型对应，且待审表达没有被当作已批准要求？ [Ambiguity, 012 Spec §US8/FR-025—031；Navigation-preview §DUI02/03]

## Notes

当前辅助评价：**21项满足、1项部分满足、0项不满足**；正式新勾选0/22，软件执行数0。三项优先问题与两个直接关联缺口的修订/关闭依据见[定向审查](../design-review-20261005.md)。部分满足只为DUI02/03精确导航待用户确认。满足仅指文档要求清晰完整，不是软件或硬件通过。

| 条目 | 修订后辅助评价 | 证据与必要限制 |
| --- | --- | --- |

| CHK001 | 满足 | API-L00准确请求/合法类型表/来源相容、完整候选和sourceContext；I01/I04修订。 |

| CHK002 | 满足 | API-L00第5项明确014配置/012同SQLite准备义务，无第二模板库；真实值是后续准备前置，未宣称已提供。 |

| CHK003 | 满足 | RC10 CellId/Region/派生号与OK排序，NG/Pending不参与。 |

| CHK004 | 满足 | ForObject成员独立/整体关系、DEP02正式映射，不把显示号或组锚点推成员机械位置。 |

| CHK005 | 满足 | RC10工位一份、Stages两角、每件Source/OriginPutBack及两抓手显式选择。 |

| CHK006 | 满足 | API-L01a JsonElement未填中间态，正式严格正文4；准入/COMMIT分开。 |

| CHK007 | 满足 | CellId稳定、StageId组关联；API-L01a对同面两组精确定位。 |

| CHK008 | 满足 | EX14全盘Frozen不重算身份；所有scope及排除事实齐才下料/Final。 |

| CHK009 | 满足 | EX14普通NoMoveRequired/特殊ReturnToOrigin分开，无任意OK目的。 |

| CHK010 | 满足 | EX14 picked/真实receipt/placed/safe门；质量OK不能代回放。 |

| CHK011 | 满足 | I02已修活动API及直接data-model/RC版本表；1.4写3仅历史，旧冻结不升级。 |

| CHK012 | 满足 | RC10.4与EX14-05共同StageId覆盖fusion/worker/workload/保存/投影消费者。 |

| CHK013 | 满足 | I03 V14-INPUT至少两实际OK各原槽/safe，下一件及下料时间关联明确。 |

| CHK014 | 满足 | V14-INPUT优先AB/AB同链，每件4capture/两件至少8；CD/CD必要组件替代也必做非可选。 |

| CHK015 | 满足 | EX14-04与US3四类需求及组件范围，未编地址/反馈。 |

| CHK016 | 满足 | 普通/组/整体原节奏，不盘末重复特殊分拣；受影响普通代表。 |

| CHK017 | 满足 | 保存/UnknownHeld、placement/safe failure、原deadline/cancel/epoch保护；仅必要失败组件。 |

| CHK018 | 满足 | EX14同Pump/WaitGroup及通信mask迁移，013既有成果和偏差保持，无性能重测。 |

| CHK019 | 满足 | V14-07双向必需发现/执行清单及同run事实，旧证据按源码输入范围复用。 |

| CHK020 | 满足 | Cleanup定位当前consumer、历史reader保护及正文4/StageId/来源测试迁移义务；未实际删代码。 |

| CHK021 | 满足 | DEP仅所依赖的定位/派发/硬件Applied，设计/保存可继续。 |

| CHK022 | 部分满足 | 两个业务导航方案可审且无技术扩展；精确导航未获用户确认，继续待审，不自行关闭DUI02/03。 |

延期恢复/未来边界不扩展；现场输入只限制其依赖动作。新tasks和共享代码须在后续获授权阶段承接，当前未生成任务或执行测试。
