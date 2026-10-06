# 012手动布局与014配套增量设计

2026-10-05，根E:/dzk/gaode-1，显式012。本会话统一维护014共同后端和012配套；旧独立根/011合入责任不再是当前安排。仅定向设计，当前代码1.4/正文3已集成；共同1.5/正文4/冻结3未实现，旧任务完成不能覆盖本次。

## LD01 界面范围与状态

保留recipeModal的基础信息→坐标配置→检查保存及深色样式。第一步底部唯一10×10矩阵用区域画笔、点击取消/改区，三区数只统计。第二步使用同一selectedCells和同一100坐标，未选格保持不可编辑，详情标题为区域/区域号/行列，当前格高亮；成员/部位/面切换只改变本格的适用输入，区域号只投影不索引数据。

编辑状态以`CellId + Region + 现SlotId + 实体/成员 + StageId + LocalFace或ScanPoseId + PointRef + Camera`寻址，技术键不显示控件。新Region与原Region不同或格删除时，只清本格旧业务配置及无人引用局部参数；其他格的隐藏元数据、坐标和拍照参数都保留。共用工位/两Stage配置不因删一格清掉。提交前由共同校验核实际内容；填写状态不是后端校验、提交、质量或物理到位。

普通/成组/半成品用已有类型、成员/面、AB/CD、适用E输入；旋转专属工位、两组角及上料抓手只特殊出现。所有特殊OK目的固定原CellId；没有任意OK目的格或处理开关。原槽放料参数只用已确认HandlingPoint来源独立填写/承接，不复制Source高度/补偿。

## LD02 共同字段→输入→保存→完整重读

唯一字段/类型在011共同RC10，以下仅消费映射，后台技术内容不成为UI授权。

| 共同内容 | 弹窗位置/输入 | API及保存正文 | 完整读取/运行 |
| --- | --- | --- | --- |
| TrayLayout.Cells(Row/Column/Region)、Position.CellId | 第一页底部区域画笔100格；第二页原位选择 | editor-layout传实际选格，服务端生成/核CellId；新正文4完整trayLayout+position关联 | 完整GET按实际格恢复空位和区域，不用counts生成；运行冻结布局 |
| 区域号、Capacity/Ng/PendingCapacity | 概览/格号只读统计 | 同一共同校验派生/核一致，无count独立手输或分配策略 | 区域独立row-major号；旧运行固定原号，缺料跳过不重号 |
| SlotId/UnitPattern/MemberPattern及Material | 只呈业务成员/部位，不技术身份编辑 | 程序保留已读有效身份/关联，格位更改只重建该格；不按显示序号迁移 | 完整GET保每实体关系；F/冻结取原值 |
| TraySlotMapping/PhysicalSlotIndex | 操作者不编辑映射/来源/3D号 | 后台已有正式配置绑定、无依据保持null，无0/行列公式；保存合法结构不要求现场在线 | 历史缺映射真实显示限制；相关产品动作准入拒绝，不卡离线保存 |
| InspectionKind/UnitKind/ScenarioId | 既有场景/类型选择；不新增场景 | 程序明确映射正式类型，specialRotation仅S1独立件 | 普通/成组/整体保持各流程，无产品名/test分支 |
| RotationLoadingGripperId/SortingGripperId | 基础信息特殊两用途1/2，普通仅适用分拣；未选占位 | 新保存显式合法值，同源共同校验，无默认 | 完整读取null仍未配置；冻结保持各用途，通信归014 |
| RotationWorkstation.Place/Pick，Stages两AngleDeg/CameraPair | 特殊共享工位取放、第一组/第二组绝对角与AB/CD | 工位只一份；角只Stages，组重复保持不同StageId | 完整重读共享值，各件仍四采集，UI不生计划 |
| ObjectExecutionInputs.Source/OriginPutBack | 特殊当前OK原槽的取料/原槽放料卡片 | 固定CellId，只存各用途HandlingPoint，无OK任意目标/复制Source | 原槽放料与取料参数可不同；Frozen origin不串件 |
| Coordinates / CaptureProfiles | 按格/成员或部位/面/实际拍照A-B或C-D，XYZ和三参数；E仅实际扫码项 | 点/Camera/StageId/局部profile精确绑定，AB/CD分别引用；其余有效后台配置保留 | 完整GET不丢隐藏参数；CaptureRequest消费对应局部值，真机Applied另证 |
| Flip/ExtraPose/PurposePoints | 原合法翻面取放、可选E卡片，不R或硬件高级输入 | 后台姿态/Z/注册引用保留，缺真实来源拒绝依赖变更，不填演示值 | member独立机械输入与assembled整体取放分开，E不当缺陷检测面 |
| SortingTargets NG/Pending + SortingCellIds | 点击对应区域实际格配置现用途点 | 目标CellId→HandlingPoint唯一存于SortingTargets，对象只存既有分配引用；无双重坐标源/OK目标selector | 同号不同区不混，目标不等实际动作完成 |
| 身份/Version/Digest/Approval/algorithm/ROI/light channel/config refs | 不展示技术编辑器/说明或折叠设置 | 完整正文保有效值；服务器唯一身份/校验/摘要/ExpectedVersion | 保存不批准生产，旧运行不从新目录补值 |

## LD03 HTTP与后台映射

具体信封见API-L00/L01/L01a，EditorDraftRequest明确(Model,ScenarioId,UnitKind,InspectionKind,可选SourceRecipeId)；新EditorLayoutRequest以JsonElement Definition、来源RecipeId/Version程序上下文、TrayLayout、程序StageId和原已适用成员/面/E变更为薄输入，移除Count/NgCount/PendingCount生成路径。mapper只编辑数据关联和隐藏引用，工艺/完整性由唯一Validator；不能生成计划或让它成为第二业务校验器。新格没有复制坐标/参数，仍需操作者填；所需后台采集/算法/姿态配置由正式程序提供，不操作者拼引用。

SQLite同实例Store/Catalog，完整正文保存/重读、F当前Head、一个全盘Frozen关联；ETag/If-Match→ExpectedVersion、真COMMIT/Saved/Unknown和取消/迟到保护不变。历史2/3缺布局真实读取；不得自动10×10摆放或默补抓手。编辑保存正文4必须明确布局/合法抓手与可证明身份关联；未关联旧数据不按旧序号移到新格。完整合法无设备保存可做；正式运动准入另核映射/安全。

## LD04 导航与实际状态

DUI02方案为当前格详情标题下成员按钮，切独立成员及其面/相机/扫码；DUI03同位置检测部位按钮，仅切部位拍照/扫码，整体取放独立一处。两项在2026-10-06获用户明确批准，严格按原预览进入导航实施；详见navigation-approval-20261006.md，不新增选项。

现运行页只经API/通知取真实cell/原号/entity/stage/origin/return/safe/结果。普通NoMoveRequired与特殊实际ReturnToOrigin分开；scope完成不显示整盘结束，质量OK不显示回放完成。无事实null/Unavailable，不从编辑器、序号或活动目录推正常；现8卡/明细/诊断，不新页面。

## LD05 当前宪章复核

| 原则 | 当前设计结论 | 证据/限制 |
| --- | --- | --- |
| P01 | 符合 | 最新10×10稿与原槽规则优先，原始来源/历史不改 |
| P02/P05 | 符合 | API→同共同validator/store；本会话唯一分工，无第二执行器 |
| P03/P11 | 符合 | RC10稳定格/成员/部位/配置，特殊字段条件化 |
| P04 | 待补充，仅实际运动 | 014DEP地址/安全/容差，保存与编辑设计可继续 |
| P06 | 符合 | 原有界保存/控制路径及013单源不变 |
| P07/P08 | 符合 | 相机StageId与Origin、完整往返/深冻结/并发和Unknown，不能质量代处置 |
| P09 | 符合 | 结构化诊断/真实证据，替身来源明确 |
| P10 | 符合 | 缺映射/历史不造默认；局部限制明确 |
| P12 | 待补充，仅DUI02/03 | 两预览于2026-10-06已批准；软件满足需本次实际导航验收，历史清单不改 |
| P13 | 符合 | 必要最小集合V14-01—07，仅计划，本轮执行0 |

无豁免；旧architecture勾选保持历史，不重新生成checklist。012当前需求清单14/16未改变。

## LD06 清理及验证承接

对象/直接消费者/保留保护见014 cleanup-and-consumers，最小集合见014 verification/quickstart。替代后删除Count克隆、旧序号身份/任意OK目标、旧列表/监听/无消费者测试；合法成员/部位、权限/取消/ETag/提交未知/冻结/历史保护继续。执行后以真实render+API/SQLite及一个特殊/一个普通代表证明，不以本预览或旧25/25当通过。本轮不任务生成、不代码/构建/测试。


## LD07 特殊首次新建及类型切换

请求、合法类型→Route关系、SourceRecipeId相容性/歧义拒绝、空待填共同JSON候选、后续新格所需同源authoringContext及真实来源准备责任按API-L00/L01a。基本信息场景1类型选择必须传普通或特殊；场景2/3固定适用ordinary，不多一个工艺开关。切换类型清不相容来源和旧候选关联，不让同型号来源混用、迟到响应覆盖。014负责真实共同能力配置，012沿StorePrep/共同保存实际准备同SQLite来源，不以Unavailable代替已承诺的新建主流程，不加模板选择UI。
