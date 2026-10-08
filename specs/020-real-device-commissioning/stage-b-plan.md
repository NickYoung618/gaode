# 技术方案：020阶段B——人工配方贯通与真实设备混合联调

**日期**：2026-10-08。**分支**：020-real-device-commissioning。**宪章**：9.0.0。
**规格**：[spec](spec.md)，FR-001–004、009–010、012–017；FR-005–008继承阶段A；FR-018留交付阶段。
**状态**：确定软件部分已实施，离线证据与限制见[validation-stage-b](validation-stage-b.md)。配方校验保存后可选；已知现场协议及显式混合装配已接入。PLC-Q3剩余解释、PLC-Q4首次/恢复和现场配置仍限制相关真实运动；T055/T056未执行。不连接硬件、不打包部署或提交推送。

## 方案摘要与阶段边界

操作员把设备示教得到的参数填入既有新增配方界面，经共同校验、SQLite保存和完整重读后，正式运行先冻结公共配置，经3D/F步骤绑定唯一配方，再冻结配方及执行输入。真实PLC执行、真实相机采集；虚拟算法和虚拟外部光源各自实际调用、标记来源，图像、参数和结果沿正式存储链落地。

| P13阶段边界 | 本阶段方案 |
| --- | --- |
| 起点 | 后台受控来源已准备，操作员从既有新增入口制作完整配方；缺参数给字段级原因 |
| 终点 | 同一配方版本经重读/冻结，沿单品翻面代表路线完成适用动作与采集、必要提交和终态；证据逐字段可追溯 |
| 参与组件 | 原页面/API、共同配方模型/校验/目录/规划、Host、PLC适配器、七相机worker、算法端口、光源端口、SQLite/媒体 |
| 必要验证 | 人工配方全链、快照不受编辑影响、真实参数消费、来源分离、未知不重发、A机制受影响最小回归 |
| 完成证据 | 配方版本/摘要、字段对账、TCP动作及清零、相机参数读回/帧、算法输入输出、SQLite/媒体提交；分别注明离线和现场 |
| 延期 | 包制作/安装/回退实操/GitHub；新页面控件及非代表工艺现场验收；历史019未验证项不消失 |

阶段A原计划、28项已完成任务及验证原样保留。[tasks](tasks.md)按阶段B实际实施/验证更新；B的分析前置已完成，不能拿A勾选代表B或真机通过。

## 技术上下文

| 事项 | 选用方案与实际依据 | 限制 |
| --- | --- | --- |
| 运行时 | 现.NET10、Python3.12、Windows SDK、SQLite；不升级 | 必要软件构建及受影响A定向回归，见B验证 |
| 配方 | 现RecipeDefinition/SqliteRecipeStore/RecipeAdmission/RecipeRunPlanner | 新建Approval为空，编辑保留旧Approval；B-DEC-01要求软件校验后可选运行，不再另行人工批准 |
| 混合装配 | 明确新增Mode/Purpose `RealDeviceCommissioning`，见MC-020 | 技术命名/用途隔离已设计；不把Test或Production配置改名即放行 |
| 相机 | 现一机一worker、单机gate，v2一次设置+读回+触发原子请求已实施 | 离线进程夹具已验；物理SDK效果待T055，019历史结果不扩大 |
| 算法/光源 | 经IAlgorithmPort、ILightGateway实际消费本轮配置，模拟来源可追溯 | SimulatedAlgorithm目前仅FDecode，不能当全角色实现；值由用户稍后提供 |
| PLC | 同一通信适配器内按已知现场布局/能力构建采样与命令 | 教学字段退出；安全语义未明保持Unconfirmed；不借缺字段返回false来放行 |
| 页面/宿主 | 既有012合同与客户原型；本轮未发现代表路径必须新增控件的证据 | 若后续需改页面，先独立前端规格；020不生成页面实现授权 |
| 容量与预算 | 实际配置来源、设备能力和既有有界资源 | 现场值待交；不复制Test秒数/坐标，A清零继续消费原动作预算 |

## 宪章检查（设计前/设计后）

| 原则 | 设计前 | 设计后 | 证据及局部范围 |
| --- | --- | --- | --- |
| P01 | 待补充，仅限制现场语义 | 待补充，仅限制PLC-Q3剩余项及PLC-Q4 | [现场合同](contracts/site-plc-adaptation.md)，用户答复前不填安全映射 |
| P02 | 待补充，缺混合装配 | 符合设计 | MC-020同一正式链，独立采集不冒充整线 |
| P03 | 符合 | 符合设计 | RC-020完整配方、既有面序；代表OK路线不删NG/Pending校验 |
| P04 | 待补充，现场安全不足 | 待补充，仅限制依赖动作 | 有限终态；模拟安全值有依据才可用于运动；不自动重拍/重发 |
| P05 | 符合 | 符合设计 | 唯一Host/业务端口，地址编码仅通信层，前端经API |
| P06 | 符合 | 符合设计 | 单相机gate、原媒体租约/容量/控制路径，不新建调度框架 |
| P07 | 待补充，参数Applied缺证据 | 符合设计 | CP-020请求/读回/帧及逐组件来源，旧事实不改写 |
| P08 | 待补充，新配方空Approval阻断 | 符合设计 | B-DEC-01已明确软件校验/保存后可选运行；每次编辑重新校验、启动复核、在途快照不变 |
| P09 | 待补充，算法角色缺实现 | 符合设计 | 明确虚拟提供者、真实媒体输入、中文持久日志及最小验证 |
| P10 | 符合 | 符合设计 | 下表B1/B2/现场门分别阻断，不让未知项挡独立设计 |
| P11 | 符合 | 符合设计 | 产品参数由受控来源和版本配置；能力仅按本轮真实消费者增加 |
| P12 | 符合 | 符合设计 | 保留原型；页面修改需独立规格，本轮只设计后端契约 |
| P13 | 待补充，端到端未证明 | 软件证据按范围分列，现场未通过 | B-V01–08见validation-stage-b；新用途完整真机链Blocked |

无复杂度豁免，无宪章修改。设计符合不代表软件/真机通过；未决依赖保留明确失败门。

## 结构与职责

| 模块/文件（相对仓库根） | 本阶段职责/消费者 |
| --- | --- |
| backend/src/Gaode.Host/Api/RecipeEndpoints.Authoring.cs；Infrastructure/Recipes/SqliteRecipeStore.cs | 复用editor-draft/layout/validate/GET/PUT、显式来源ID/版本选择及真实保存；必要错误引用返回既有字段模型 |
| backend/tools/Gaode.DeploymentPrep/Program.cs | 沿现--seed-authoring准备完整无批准来源；不得把此命令变成默认批准；不新增授准入口；按B-DEC-01由共同校验和运行检查承接可选状态 |
| Application/Recipes及Station01/StartPublicPreparation.cs | F匹配/选择意图/物理槽核对、共同校验、冻结和消费；不新造工具配方引擎 |
| Host/Composition/Station01Registration.cs、Station01RuntimeOptions.cs、CapabilityRegistration.cs | MC-020明确组合、用途、来源、配置校验与能力注册 |
| Application/Configuration/PublicConfigurationValidator.cs；Infrastructure/Configuration/ApprovedExecutionCostProvider.cs | 本轮用途和有来源预算；保留旧Test/Production隔离与旧未知阻断 |
| Infrastructure/Devices/Cameras及Gaode.CameraWorker | CP-020原子设置采集、读回/来源、正常恢复；沿原单worker门禁 |
| Application/Ports/CaptureAlgorithmMessages.cs；Acquisition/CaptureEvidenceGate.cs | 必要逐组件设置事实与事实校验；更新媒体/投影/模拟和fixture消费者 |
| Infrastructure算法适配、现IAlgorithmPort与能力注册 | 明确角色的虚拟实现读取实际媒体和受控结果；不依赖类型判断PythonWorkerAdapter才注册能力 |
| Infrastructure/Devices/Cameras/ILightGateway.cs及实现 | 显式模拟亮度/通道/开关/等待消费与记录，物理光源未应用 |
| Gaode.Plc.Protocol、LatestProtocolPlcDevice及PreparedPlcReadPlans | 型号REAL、F仅XY、现场能力集合、可信安全语义；保持A状态机/采样/未知门 |

共享变更必须先经本spec/合同/plan及后续tasks；表中不是现在改产品的指令。文件完整定位及证据见research阶段B节。

## 数据、契约和配置扩展

- [RC-020](contracts/recipe-chain.md)：P01–P10贯通、来源准备、冻结；B-DEC-01已确定无额外人工审核，新增/编辑校验保存后可选运行。
- [MC-020](contracts/mixed-runtime.md)：明确联调用途、逐组件来源、模拟结果适用范围及配置消费者。
- [CP-020](contracts/camera-parameters.md)：设置应用与帧关联，worker v2、旧纯采集兼容边界。
- [SP-020](contracts/site-plc-adaptation.md)：现场地址/型号/轴集合与待确认安全门。
- [data-model](data-model.md)阶段B节列版本和身份关系；不写运行默认值，不建立审批平台或通用插件框架。

## 并发、保存与异常出口

每物理相机复用原gate，参数设置至对应触发/新帧同一排他区。设置失败不触发；触发结果未知不重拍，原session故障隔离。不同相机沿现容量并发，不因混合模式提高并发或队列上限。算法读取已提交媒体或原合法租约，沿既有deadline和输入释放规则；算法失败给有限任务结果，只阻断缺安全输入的具体动作。心跳/停止不等待算法或SQLite。

运行启动冻结公共配置/预算及联调配置身份；F匹配后冻结具体配方及其适用模拟输入。未绑定配方前需要的3D/F输入也必须在启动时有明确场景/料盘与预期配方范围，不能先凭任意位置运动，等F之后再补合法性。F实际绑定与预期不符时阻断后继。

保留意图→实际采集/动作→媒体及SQLite提交→发布/消费。取料事实提交、安全位及A清零义务不变。重启只读历史不恢复动作许可。数据库准备和备份沿维护工具且与Host互斥，不直接编辑现场库、不后台迁移。

## 软件验证与证据计划

| ID | 范围 | 最小方法/预期 |
| --- | --- | --- |
| B-V01 | FR-001/002，P01–P10 | 正式API从完整受控来源新增，填写→共同校验→SQLite保存→GET/重启重读→编辑；逐字段一致，必要隐藏字段不丢 |
| B-V02 | FR-003/004 | StartPublicPreparation→F唯一匹配→实体槽核对→冻结→实际消费；运行中编辑不改旧快照；公共XYZ如实保存但只声明已执行XY |
| B-V03 | FR-012/014 | Host组合解析及用途错配拒绝，实际IAlgorithmPort读取真实保存媒体（离线用明确fixture），受控结果关联身份；缺安全值不发依赖动作 |
| B-V04 | FR-013 | 同相机连续两次不同参数→读回→各自新帧；不支持/读回错/超时无假Applied、无自动重拍；虚拟灯调用有日志 |
| B-V05 | FR-009/010 | XLS85点核对、现场已知能力定义/编码与F轴集合的loopback审计；补MB6058=0无报警解释及其不覆盖非零报警位/独立故障的验证；安全未明Unconfirmed，不因去Teach而删门 |
| B-V06 | FR-005–008/015 | 只复跑受现场语义/采样改动影响的A两轮、同坐标、父动作/取料保存门；沿原夹具 |
| B-V07 | FR-012/017 | 现场前置满足后正式人工配方单品翻面闭环；逐台角色/采集/媒体关联，不增加额外E/R动作来凑七台 |
| B-V08 | FR-010/016/017 | 逐文件文档/消费者核对、真实失败诊断和源码/配置关联；历史未验项保留 |

命令与实施前置见[quickstart](quickstart.md)阶段B节。没有测试文件的用例先由tasks安排，过滤器零匹配不算通过。

## OPEN与可独立推进部分

| 分组/门 | 状态与阻断范围 | 可继续 |
| --- | --- | --- |
| B1确定软件设计 | 无新增业务决定：参数链对账/共同保存、混合用途、相机参数原子调用、显式虚拟实现和已确认F用途 | 可生成离线任务；涉及共享消费者先完成tasks/analyze |
| B-DEC-01（已关闭） | 用户选择软件校验通过后即可选运行；取消逐版本人工确认/维护授准提案。当前空Approval阻断须改，启动自动检查仍保留 | 可拆共同校验、保存、运行准入/Freeze/API消费者及最小验证任务 |
| PLC-Q3–Q4局部依赖 | MB6056对应和MB6058无报警＝0已明确；光栅/安全门报警仅由PC读取，PLC-Q2已关闭；仅剩余位/等级/清除及首次准入/恢复解释按依赖阻断，不猜定 | 已确认地址/型号编码技术设计、缺失状态传播及离线链 |
| OPEN-020-05 | 现场型号/姿态、格位、完整取放与NG/Pending目标、机械范围/预算等待交 | 定义校验和来源；不造666/Z=2/任意坐标 |
| OPEN-020-06 | 公共Z用途待确认，仅挡改变公共控轴/删字段/宣称Z执行 | 原公共手填/实测候选/保存、XY动作保留 |
| 用户安全虚拟值 | 提供方式已确定，具体值待交；仅挡依赖它的真实动作 | 显式Test夹具、契约、参数适用范围验证 |
| 前端与ROI | 无代表路线新增控件授权；非全幅ROI若必要，须先明确定义，不静默裁切 | 现页面不改；已支持全幅和现SDK参数能力内设计/验证 |
| 019等历史验收 | SDK真实阻塞/物理断线、机械抓手/REAL独立核验仍待验证 | 复用合法历史正常证据，新增参数应用另验 |

## 客户原型与文档同步

原型历史SHA-256、a.html/data-view.html/login.html及既有012批准范围见spec；本轮未重新检查原ZIP，不声明逐页验收。页面仅经后端API。代表路线先沿已有字段与交互完成；发生真实页面缺口再建立独立前端规格，不重建020、不补造012历史任务。

[document-sync](document-sync.md)阶段B列精确同步对象、旧条文和新合同。当前只追加设计衔接，不把未来参数应用写成019历史已完成，不修改A运行证据。

## 分析后修正（2026-10-08，文档）

C1：FR-016实现明确分配T050/T051/T052，T058复用必要失败用例核对持久、分级分类、关联及限频日志。I1：T039建立独立现场地址TCP夹具；T043/T054按旧Test正常链与现场已知协议/安全未明阻断分别留证。新用途完整Host/现场链未通过不得宣称通过，等待对应现场门。I2：单张检测与融合都是AlgorithmRole.Detection，AlgorithmPurpose.SingleDetection/FaceFusion及输入数区分。I3：测试环境隔离与业务用途分开；T033显式覆盖RealDeviceCommissioning，并回归旧Test/Production规则，不复制Test批准或改变旧用途准入。

B-V02/03/05/06的软件证据按服务/消费者、旧Test正常链、现场布局阻断分列；需要未决安全结论的新用途完整正常链继续Blocked，不要求离线夹具编造现场许可。T056负责补齐实际新用途代表链证据，最终成功条件保持。详见tasks对应任务及analysis-stage-b.md。

2026-10-08分析整改复核完成：C1/I1–I3文档层已解决，记录见[analysis-stage-b.md](analysis-stage-b.md)。分析前置已满足，确定软件任务现已实施，实际证据见validation-stage-b.md；现场依赖和对应NotRun状态保留。

实施技术调整（2026-10-08）：现场SiteProtocolTcpFixture复用现Modbus客户端及MBAP约定，独立最小FC03/06/16服务器；旧PlcDataStore硬编码Test地址，不能直接作为现场寄存器存储。具体原因及边界见SP-020，不改A夹具语义。

实施用途核查：T045/T053同步PlcRuntimeOptions、PlcMechanicalConfiguration及Axes/Stages机械依据消费者，由Host显式下传联调用途，各机械依据与业务用途一致；旧Provider默认用途保留。七机装配同时核对公共3D/F绑定角色。具体边界见MC-020。

T036/T037同步PublicPreparationHandoffV2、CommittedRecipePlanReader、IndependentRecipeApplication：联调身份使用现RunPurpose.Commissioning，冻结用途来源为CostProfile，重读联调快照而非空Simulation；保持旧用途审批约束。

T045/T041核查Real实际位置缺坐标系/单位来源的问题：PlcMechanicalConfiguration新增显式PositionBasis，仅将有来源配置附于实测坐标，不构造安全或完成反馈；联调用途缺依据拒绝装配，具体现场值仍待交。T044/T039覆盖配置用途和观察事实。

## Phase 8当前进度（2026-10-08）

T062–T064补充的编辑隔离、人工取盘终态重读、新用途正式启动阻断已取得12/12最小回归证据，见[validation-phase8](validation-phase8.md)。T061仅完成[独立前端规格交接](frontend-handoff.md)，不表示页面/桌面入口已实现；T065已同步进度及清单。未变更共享接口或产品代码；T055/T056仍Blocked，原现场及历史未验不变。下一步再次speckit-converge。
