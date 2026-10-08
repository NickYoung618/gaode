# RC-020 阶段B：人工配方到实际执行

版本020-stage-b/1，2026-10-08。确定软件部分已实施并离线验证，现场完整链待T056；FR-001–004、010、017。B-DEC-01已按用户答复关闭：软件校验通过并保存后即可选择运行。

## RC-01 一条数据链

复用现`/api/v1/recipes`下editor-draft、editor-layout、validate、GET、PUT及既有ETag/权限语义，不新增整套参数导入或示教控制接口。新建先按Model/ScenarioId/UnitKind/InspectionKind/Route筛选，已有显式SourceRecipeId时再限定该来源并唯一匹配；没有明确来源才要求同键唯一。editor-layout继续核对已选来源版本，版本不符拒绝并要求重新读取；不得以其他型号源或默认样例补齐隐藏配置。

`RecipeDefinitionValidator.ValidateForSave`、`SqliteRecipeStore.SaveAsync`为共同校验/保存边界；保持服务器生成身份/版本/摘要、If-Match、真实事务和CommitUnknown语义。软件校验通过并保存成功后即可选择运行，无另行人工批准；启动设备状态/参数条件检查仍独立执行，不能把保存回执当作设备已就绪。完整新配方仍须满足Source、NG/Pending目标/格、抓手等既有条件，本轮实际OK不等于允许删掉其他分支参数。

## RC-02 参数责任与消费者

| spec矩阵 | 人工/配置归属 | 冻结及实际消费核查 |
| --- | --- | --- |
| P01/P02 | 现新增基础项、布局/成员/阶段；后台实体槽/能力来源 | F原码唯一匹配、选择意图、TraySlotMapping、BuildExecutable面序/对象 |
| P03 | 每对象/面/相机XY、Fixed.Z及来源 | CoordinateResolver→MotionCoordinator→PLC对应XY/检测Z；记录PointRef/StageId/Camera/单位 |
| P04 | 现翻面取放XY；后台Model+ProfileId/Version+PoseKey映射 | 翻面语义请求→通信层现场REAL型号/姿态；PLC内部Z不变成PC控轴 |
| P05/P06 | Source、目标区格与XYZ、原槽、抓手/R适用配置 | 完整验证/重读；实际有执行分支记录选择、取料提交及目标，未走分支标未执行 |
| P07 | 逐点曝光/增益/亮度；后台ROI/通道/等待/算法版本 | CaptureProfile→CP-020帧/灯证据→算法角色输入输出，不只比JSON |
| P08 | E点及扫码Z，F来自本盘有依据的3D结果 | F仅XY、E按配置XY+扫码Z；代表路线不额外移动E |
| P09 | 公共两点和设备安全/范围/速度配置 | 启动公共快照独立；公共Z保存/读取来源如实记录，执行仍按现XY |
| P10 | 服务端身份摘要、受控用途/来源/预算/能力 | 冻结版本/摘要/PlanRevision及实际调用，旧运行不受编辑影响 |

新增→校验→保存→重读→编辑→重新读取→冻结→实际执行均须逐项对账。布局扩展只使用当前明确来源支持的配置，不静默复制未确认物理坐标。不能把前端页面填写率当作参数齐备证明。

## RC-03 后台来源准备

复用DeploymentPrep现`--seed-authoring`，该入口仍准备空Approval的完整草稿来源，记录输入文件hash与共同保存返回的版本；它不是给操作员新增的导入功能。先核对当前目录是否已有适用来源，避免重复seed；同型号可以存在多个合法配方，不能为“同键唯一”禁止第二份配方。现页面会带已有configurationSourceId，沿既有来源ID/版本合同选择；没有明确来源且同键歧义时要求重新读取适用来源，不另建模板平台。示教参数可并行提供，字段缺失/来源不明指出对象/步骤/字段和受限范围。

## RC-04 软件校验后可选运行（B-DEC-01已确认）

用户答复：“填好后软件校验通过就能选择运行”。新建与编辑均按这一规则；不增加审核按钮、人工版本批准、维护侧授准命令或独立批准清单。

普通保存使用共同校验器和真实SQLite提交。可选状态由当前已保存版本的软件校验结果决定；启动对实际选用/F匹配的版本重新核对完整性、用途、物理槽/映射、坐标范围/单位、能力与必要配置，记录版本/摘要及校验原因，之后冻结。不能只依赖旧缓存的Valid，也不能以来源配方已通过替代当前配方校验。编辑后重新校验并保存，不借旧批准；旧运行保持原冻结值。

现RecipeAdmission对空Approval的阻断、Freeze对plan.Approval.Purpose的依赖及API可运行状态必须一起按本轮用途改造：读取当前运行用途/现场配置和共同校验结果，不再要求用户补批准字段；槽范围取实际布局/映射与适用配置校验。保留历史Approval数据及其他用途原行为，不制造虚假的批准人/依据，不把本轮改动扩为全系统权限放开。

可选运行仍需当前PLC就绪、安全反馈有效、参数齐备和真实运动输入有依据。软件拒绝时给具体字段/设备条件；这些是自动检查，不是另设人工审核。PLC安全映射未明及用户尚未交付的虚拟运动输入按原局部限制保留。


## RC-05 冻结、保存与失败

StartPublicPreparation已有公共冻结、F唯一匹配、实体槽校验、Admission、BuildExecutable、Freeze链，沿此扩展。新增配方完整重读后进入同一目录；不通过工具recipe或临时内存候选跳过SQLite。运行中编辑不会改变已冻结正文、采集参数、算法输入依据和处置。字段缺失/软件校验不通过/来源错配或设备未就绪在相关准入点报错；结果未知的提交/动作不自动重发。

共享影响：RecipeAdmission、FrozenExecutionInputs/Freeze、StartPublicPreparation及独立绑定路径、API可运行状态、共同目录/维护工具、序列化/历史读取与测试。共同校验/提交决定当前配方可选状态，启动设备条件决定能否开始；B tasks需覆盖空Approval新建成功选用、编辑后重新校验、无效值拒绝及旧运行快照隔离。

实施签名增量：BuildExecutable允许显式purpose参数，生产调用必须传冻结Public.Purpose；省略参数仅保持历史调用行为。Admission对新用途验证当前保存身份/摘要、实际槽和ExecutionProblem，不要求Approval；Freeze核cost.Purpose和plan匹配身份，其他用途仍核原Approval。catalog/validate响应取Host当前配置用途，不接受浏览器自报用途，不新增前端入口。
