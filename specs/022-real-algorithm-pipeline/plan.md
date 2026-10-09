# 技术方案：真实算法接入与采集检测流水线

现行依据为末尾2026-10-10定向增量；前部2026-10-09的范围、前端不涉及及未交付表述为原计划历史快照，冲突时适用新阶段合同。Git实际分支为sync/022-real-algorithm-pipeline-20261009，逻辑功能仍022。


**功能标识**：022-real-algorithm-pipeline  
**日期**：2026-10-09（Asia/Shanghai）  
**分支/基准**：022-real-algorithm-pipeline / eb85aa4b2985e61171b9d1d749af346207282d9f  
**规格**：[spec.md](spec.md)，Q1=A、Q2=B、Q3=A  
**宪章版本**：9.0.0  
**范围**：plan阶段生成研究、计划、模型、必要契约和验证指南；后续用户已授权生成[tasks.md](tasks.md)，本轮仍不实现或执行任务；不安装/升级、不改安装目录/制包/部署/连接硬件/发送设备命令/改页面/提交推送。

## 方案摘要

现行目标先在既有RealDeviceCommissioning正式配方/同步执行链接入真实3D/F/E/单图及必要融合，输入由采集/媒体适配层转换为PNG/PLY；保持原动作和等待屏障。A先完成配置读取、版本冻结、受管调用及可靠资源/Host监管；B依交付完成真实验收；C再拆开采集生产与缺陷等待，保留原设计但不启用T017。替身仅Test，Production不放行。

| P13阶段边界 | 当前方案 |
| --- | --- |
| 起点与终点 | 正式配置受理、公共准备/F绑定冻结至对象处置、相关输入/执行结束及原Final |
| 必须参与组件 | 配方校验/冻结、采集/MediaStore/PNG-PLY转换、TraceWriter/Query/StageEventStore、算法受管运行时、原运动/检测/分拣/整盘编排、实际后端Host消费者 |
| 必要验证 | A定向屏障/关联/同步容量/代表超时/快照/原链回归；B真实专项待交付；SC-001重叠及批次专项延期C |
| 完成证据 | 偏序、文件摘要和SQLite重读、必检集合、唯一终态及释放；Test/虚拟/Real分栏 |
| 延期项 | C重叠优化保设计；真实交付专项局部Blocked；生产/部署/现场不在本轮，非必要异常组合与平台不开展 |

## 2026-10-09阶段决定（历史，现行增量见2026-10-10）

本节将先前并发优先目标调整为先接入真实算法；原Q1=A/Q2=B/Q3=A、FR/SC编号、历史审查和回退证据均保留。历史记录中的G2-MODE“待确认”已由本次用户明确授权沿现有RealDeviceCommissioning接入真实算法解决；不代表算法已交付或入口已就绪。

| 阶段 | 目标及边界 | 完成判据 |
| --- | --- | --- |
| A：基本接入软件 | 正式配置读取、原始媒体到PNG/PLY、真实算法端口适配的可独立部分；按原同步调用链覆盖首次/复查3D、F、同步E、单图和必要融合；不移除算法await，不启用生产/完成句柄 | 配置/配方冻结→采集原文件/转换文件→受管派发→结果/SQLite重读→原分拣及Final；Test证据仅证明软件。基本关联、预算、可靠释放、停止/关闭及Host资源扫描不能延期 |
| B：交付后真实验收 | 在同一RealDeviceCommissioning用途核实际程序、模型参数、标定、样本和就绪，取得真实结果与释放证据 | 对实际交付模块分项验收；资料缺失只阻断对应适配/激活/实测预算/正向验收；设备现场验证仍须另行授权 |
| C：后续重叠优化 | 原已确认允许的采集/运动与缺陷计算重叠、受管句柄及对象级等待/预约、批次额度 | 原设计与任务保留未执行。T017本轮不得启用，不能把延期标为修复或完成；后续仍须完整前置证据 |

现行用途只为RealDeviceCommissioning，保真实PLC、真实相机及当前显式虚拟光源；原显式模拟算法配置可继续使用，真实选择必须显式且来源为Real。Mode、RunPurpose.Commissioning、Manifest Profile及隔离根保护不变；Production继续拒绝，不新建用途，不迁移现场配置/数据库，不用现场库做Test。

A保普通整盘原检测完成后原序分拣、特殊本件两组识别/完整判定后立即处置/OK回原槽及安全位后下一件；3D/F必须等待有效结果及保存，同步E必须在原步骤处理/追溯/释放/保存后继续；最终完成必须等相关必要事实及实际输入/执行结束。仅C解除无结果依赖步骤上的缺陷等待，不能以A接入授权改变运动次序或提前窗口/分拣。

FR-005/006、FR-010的“不等无关计算”、FR-013/014的批次后台调度及SC-001对应C，不是A基本接入前置；FR-001/002/003/004/007/008/009/011/015–024的本阶段部分仍适用。FR-012的有限实际执行/准入及容量也适用A，批次队列优化另属C。SC-002/003/004/005/006按A必要部分定向验证，SC-007属B；不以A通过声称整项SC或022完成。

本轮只修文档，随后单独只读分析。40项任务均未执行；requirements保持只读。禁止触及D:/Gaode-Station01/commissioning-021-final-3、关闭现场程序、连接硬件、打包部署、提交推送；D:/Data只读。后续Test独立端口/库/媒体/证据，020 T055/T056仍现场未验证。

## 技术上下文（Technical Context）

| 事项 | 当前方案与来源 | 尚缺证据 |
| --- | --- | --- |
| 后端 | global.json固定SDK10.0.401，Directory.Build.props=net10.0，现分层 | 本轮未运行SDK，不升级 |
| 算法 | IAlgorithmPort、Runtime/IsolatedAlgorithmCall扩展；Production现NotIntegrated，PythonWorkerAdapter为Test | DEP-ALG-01/VER-05，不预选生产wire/SDK |
| 数据 | 实际EF Core SQLite10.0.12、原媒体格式、版本化事实必要增量 | 后续隔离副本迁移验证 |
| 设备/采集 | 现正式语义端口及020/021最新完整周期 | DEP-FMT-02/CAL-03/SITE-07 |
| 容量/预算 | 有限C/Q/输入额度/准入及绝对截止 | DEP-CAP-06，生产不猜数值 |
| 验证 | Gaode.Communication.Tests；xunit2.9.3、TestSDK18.10.1、runner3.1.5 | Test替身，本轮未执行 |
| 前端/桌面 | 不涉及，原API/通知边界保持 | 不新增UI/原型义务 |

[research.md](research.md)记录选型及拒绝替代。未知生产资料具名列为依赖，不以测试反推；已确认Q1–Q3不重问。G2-MODE已确认既有联调用途，Host激活仍受实际交付/Ready及A必要证据限制，独立软件工作继续。

## 宪章检查（Constitution Check）

设计前根据spec/源码，设计后根据模型/合同复核；符合仅指设计，不表示实现或测试通过。

| 原则 | 检查点 | 设计前 | 设计后 | 依据/限制 |
| --- | --- | --- | --- | --- |
| P01 | 最新规则/追溯 | 符合 | 符合 | Q1/2/3、021最新规则 |
| P02 | 正式模块参与 | 待补充，仅限制真实算法 | 待补充，仅限制真实算法激活/验收 | C022-ALG、DEP-ALG-01/G2-MODE；离线继续 |
| P03 | 配方运动顺序 | 符合 | 符合 | C022-PIPE/SRT，不改动作次序 |
| P04 | 屏障/有限等待 | 符合 | 符合 | C022-ACQ/PIPE |
| P05 | 唯一控制/分层 | 符合 | 符合 | 下方职责表，业务不解IPC/PLC |
| P06 | 有界资源/配图 | 只读审查指出G1/U1细化缺口 | 文档已细化，未实现/验证 | DATA独立资源扫描、PIPE三类额度，未知占用计入C |
| P07 | 身份/终态分离 | 只读审查指出I1缺口 | 文档已细化，未实现/验证 | DATA业务身份/资源事件隔离，data-model完整键 |
| P08 | 真实保存/快照 | 只读审查指出I2/G2缺口 | 文档已细化，真实用途已批准、激活仍待交付/就绪 | DATA短提交段/历史读取、启用前冻结传递及ALG用途矩阵 |
| P09 | 日志/必要验证 | 符合 | 符合 | quickstart偏序/重读/资源证据 |
| P10 | 局部阻断 | 符合 | 符合 | DEP表，不造格式/释放 |
| P11 | 配置/能力准入 | 只读审查指出G2范围问题 | 原用途/生产拒绝保持；增量已批准、实现待验证 | DEP-CAP-06/G2-MODE，未批准生产门禁及存储保护保持 |
| P12 | 页面/原型 | 不适用并说明 | 不适用并说明 | 用户范围仅后端 |
| P13 | 阶段完成/实证 | 符合 | 符合 | 本轮文档、所有SC未运行、T055/056未验 |

无需复杂度豁免；六项审查问题按下文定向修订，规则关闭只表示文档明确，不表示实现通过。外部及用途缺项仅限制所列范围。

## 结构与职责（Project Structure）

不新增工程、服务、通用平台或无使用方框架。新增类型仅限现项目需要，具体文件在tasks列定。

| 模块/端口 | 唯一所有者与必要变化 | 保持边界 |
| --- | --- | --- |
| Domain 配方/质量 | 冻结必检集合及原质量模型 | NG优先/Pending明细/成员整体规则 |
| Application Algorithms | Run管线登记；Runtime实际执行；LeaseSupervisor输入 | 扩展产品/E/融合/复查，同一隔离/期限/回收 |
| RecipeDetectionExecutor | 唯一动作生产者及受管句柄 | 去无依赖缺陷等待，3D/F/E/动作条件保持 |
| Acquisition/Motion/Ports | 当前窗口、可靠采集证据 | 类型化证据替换AlgorithmFact依赖，不解释地址 |
| ThreeStage/WholeTray | 原序作用域/阶段/收尾 | ProductionEnded/对象屏障/Completion分开 |
| SortingMapper/Allocator | 当前对象映射与唯一占位 | 保全盘配置/容量准入、取料提交后放料 |
| Infrastructure Algorithms | 真提供者/进程会话/IPC/SDK | 真资料交付后实现，不改Test标签 |
| PLC/相机适配 | 底层握手/连接/帧/元数据 | 新语义衔接，原复位/轴/完整周期保持 |
| Persistence/MediaStore | 原始/PNG/PLY文件及转换、SQLite事实/投影、短提交协调 | 覆盖原动作与新后台，增量迁移/历史读取 |
| Host Composition/Lifecycle | 分模式准入、装配、统一停止/资源核对 | Production不加载Test模拟输入，无虚拟回退 |

共享消费者：ThreeDStep/FScanStep、Detection.Observation/融合、CaptureEvidenceGate/AcquisitionCoordinator、MotionCoordinator/IAcquisitionCyclePort、真实和显式虚拟PLC、Host能力注册/RecipeAdmission/恢复/关闭、TraceQuery及测试夹具。各合同列直接影响；不能只改新调用者。

## 数据、契约与状态

[data-model.md](data-model.md)定义身份、快照、任务两轨状态、消费者及判定集合。

| 合同 | 改变的直接依赖 | 保留行为 |
| --- | --- | --- |
| [C022-ACQ](contracts/capture-completion.md) | 窗口结束从AlgorithmFact改采集/媒体/提交/交接证据 | 原完整握手/未知持有、3D/F条件 |
| [C022-PIPE](contracts/pipeline-execution.md) | 生产/结果/回收分离，有限受管产品任务 | 运动顺序、E同步、融合键、原期限/Pending |
| [C022-SRT](contracts/sorting-barriers.md) | 新对象判定/映射入口 | 普通全采后原序、特殊逐件、占位/容量/取放提交 |
| [C022-ALG](contracts/algorithm-provider.md) | 正式配置/真实提供者/版本结束证据 | 显式虚拟/Test、不静默成功 |
| [C022-DATA](contracts/persistence-lifecycle.md) | 生命周期登记/短提交协调/关机资源投影 | 实际SQLite、关键失败、旧记录、原Final |

本轮只在022形成对008/009/010/011/019/020/021的直接衔接合同，未改其他功能或任务勾选。共享代码改动前须生成022 tasks，完成相关spec/contracts/plan/tasks定向同步及全消费者核对；008–011维护位置沿DEP-DOC-08处理，不声称旧spec已同步，不修改来源原件。

关键契约检查结论：采集保存证据不依赖AlgorithmFact；输入与窗口OperationId分别表达；生产结束不冒充Detection.Completed；对象映射不省全盘目标/容量安全条件；业务终态不等资源结束；异常Run终态后的释放事实可继续追加独立生命周期事件且不改变阶段状态。这些规则在实现前固定，不能由代码自行选择。

## 配方共用逻辑与动作隔离

保正式共同加载/校验/权限审计/保存后生效，F唯一配方绑定及原公共/产品冻结点。后台按实际面/相机/成员/整体/scope计划执行，不重排运动。测试配置/媒体明确Test，共同正式业务链验证；真实准入拒绝模拟位置/反馈/固定判定。无新增配方页、模型管理平台或审批。

### 配置与策略扩展设计（P11）

C/Q、排队/执行/释放预算、输入额度和provider绑定进入共同校验/快照，不用测试变量分支。按C022-ALG G2保FullSimulation/VirtualPlcIntegration的Test规则与既有RealDeviceCommissioning显式模拟算法规则。Production保持未支持/未批准拒绝，不在Test根落库；不新增用途/Profile、不放宽存储保护、不自动迁移现场配置/数据。真实算法绑定既有联调用途本次已确认，激活仍受真实交付/就绪限制，能力声明/注册与实际角色Ready/模型应用分开。

原RecipeApplicationProductionBudgetUnapproved、ApprovedExecutionCostProvider的生产拒绝及StoreSchemaInspection允许Profile集合均保留；交付性能资料本身不授权生产支持。正式后台≠Production放行，真实算法Host正向准入另受批准用途和实证约束。直接消费者及最小模式校验见C022-ALG，不以Test成功代替真实用途成功。

能力兼容与运行就绪分别表达；实际模型应用证据不同于参数字段。旧轮保原模型实例/绑定至可靠结束，无法服务旧版本则局部受限，不偷换模型。常驻实例数依实测，不预设每相机一进程/全算法并行。

## 并发、资源与异常出口

| 路径 | 所有者/有限容量 | 期限 | 出口及资源 |
| --- | --- | --- | --- |
| 采集准入 | 生产者有限预约及批次媒体保留额度 | 原采集/阶段窗口 | 满载不采、不造Pending |
| 排队/执行 | Runtime共享真实池C/Q | 意图前首次登记总截止及分段上限 | 超时业务终态，未知执行仍占C |
| 配图/融合 | 完整键与独立文件消费者 | 原阶段/调用预算 | 未齐不占推理位，批采前证可完成配图 |
| 保存 | 每Run短串行提交段 | 原关键保存期限 | 未知对账，不换ID重写/重发动作 |
| 分拣 | 对象判定、原序及安全预约 | 原Sorting/任务剩余期限 | 不跳拣、不绕容量/取放门禁 |
| 正常暂停 | Run控制/后台管理 | 原截止不延长 | 已提交任务继续，不采不动 |
| 取消/故障/关闭 | Host/Run统一登记 | 独立有限释放观察 | 关新派发，未知保监管/持有 |

不持提交锁等推理/动作/配图；心跳和停止独立。A整批后B整批媒体保留额度独立于执行C/Q，不只“留一个配图槽”。预算唯一登记起点在意图保存前；对象屏障不重新计时；实际执行未知不让新推理超C。原算法整段重试不能用于已提交后台任务，保原明确安全通信重试边界。

## 保存与恢复

按C022-DATA，窗口、结果、判定、动作、资源事实分别保存；权威意图先于派发。每Run提交协调覆盖读取revision到有效receipt及阶段序号追加，CommitUnknown按原ID对账。生命周期追加不覆盖业务终态；异常终态后资源证据仍须可持久保存。必要增量迁移经现有受控入口，不在Host自动改表。

重启将业务恢复与资源核对分开：资源查询不受Run业务终态/旧Run链接过滤，Cancelled/Failed仍扫描未回收Call/消费者/未知执行，但不把其改回可运行业务。生命周期事件不替换Status/CurrentOperationId/ConnectionEpoch/LastEventId等业务字段；只推进事件流游标与资源投影，业务Operation分组恢复先排除资源事件。具体规则及两项定向用例见C022-DATA I1/G1。旧未知保文件及原登记，不凭旧PID消失整Run归零。Final前必要事实与相关执行/输入排空，原下料/允许取盘/人工确认保持。

三类额度分别为逐帧采集内存、跨排队/配图的工作文件保留和实际持久磁盘占用；最后消费者结束只释放工作额度，文件仍在就仍计磁盘。批次工作预约不占整批采集内存，磁盘不足不靠推理结束或删除必要图恢复。复用现MediaCapacity/LeaseRegistry/MediaStore最小增量，见C022-PIPE U1。

单Call释放观察以首个匹配结果/失败、派发普通异常、原业务超时或取消/故障/Host关闭受理事件起算一次；公共复用WorkerReleaseGrace，产品复用InputReleaseWaitMs，Start/Due及事件身份落生命周期事实。后到事件不重开，Expired仍Unknown且受管；Host仅按自己原退出期限截短等待，不改Call截止。起点不改变技术终态/Pending/动作许可，数值保持既定用途及DEP-CAP-06限制，见C022-PIPE U2。

## 分阶段实施与回退

下表供后续tasks拆解，本轮不实施。没有commit授权，故不创建阶段提交/标签；后续如授权才逐批记录具体提交及副本摘要。此前回退使用完整隔离目录备份及差异，不冒充已有标签。

| 阶段/批次 | 最小工作与依赖 | 验收与回退边界 |
| --- | --- | --- |
| A0（原S0/S1） | T001–T008契约/类型/提交/持久登记；不含C新句柄启用 | T007验证I1真实SQLite重读/重放/Operation恢复；保文档/差异和匹配旧库副本，无覆盖回退 |
| A1（原S2基本部分） | T009与T031-A后T010独立Host配置实际读取及拒绝；T015/T016基本受管调用/PNG/PLY转换及连续所有权；T026仲裁、T027实际Host监管、T031冻结及最低证据 | 入口仍未激活；配置严格、文件原值、终态后资源可查、原释放截止不重开。回匹配版本/离线副本，未知输入不删 |
| A2（原S3非重叠部分） | T023迁入原同步执行消费者，T024/T025原屏障；T028/T029/T030基本集成；T032–T040原链贯通/最小回归/审查收敛 | 实际配置/采集/转换/调用/SQLite/原分拣/Final贯通；Test仅软件证据；回退保原同步链，不删除尚有用途await |
| B（原S4真实分支） | T011按实际交付闭合；T012真实桥接/Host激活，T013/T014真实验收；依A所需T010/T026/T027/T031及原链证据 | 缺真实程序/模型/格式/标定/规则/释放/实测预算局部Blocked，不猜数值。可靠结束后回原显式模拟联调配置，保不同Origin，未知不得换版重跑 |
| C（后续原S2/S3重叠部分） | T015/T016批次调度部分→T017/T018/T019→T020/T021/T022；后续集成验证按对应子项补齐 | T017本轮禁止启用；保T006/T010/T015/T016/T026/T027/T031全部相关前置证据，及SRT-SAFETY对应证明。可靠排空后才回基本同步链；不能重置物理状态/删未释放文件 |

基准eb85aa4工作树可只读比较；原现场回退方法沿spec。禁止reset --hard/clean或覆盖现场。代码回退不能重置物理状态或自动重采/重发；未证所有权解除不得删除输入。数据库新增结构旧二进制不可读时使用匹配旧副本，不做破坏性降级。

## 软件验证与证据计划（A/B/C按最新阶段适用性分栏）

| 需求/成功条件 | 必要场景及方法 | 可观察证据 |
| --- | --- | --- |
| FR-004至006/SC-001（C延期） | Test控制首结果，正式保存链+原序采集/翻放/旋转 | 下一步骤早于前Result，文件摘要/SQLite重读 |
| FR-007/010/011/SC-002 | 3D/F/E、普通/特殊对象判定屏障 | 缺依据动作0，判定/分拣/下一件偏序 |
| FR-008/009/SC-003 | 连续两Run、同面两对象双图乱序 | 全键、唯一融合/判定、重读不串轮 |
| FR-012至018/SC-004 | Test小C/Q、配图/满载、一代表超时及暂停 | 峰值/原截止、终态唯一、迟到释放、不动设备 |
| FR-019/SC-005 | 旧轮在途保存新版本后新轮 | 两轮所有调用冻结版本一致 |
| FR-020/022/SC-006 | 受影响采集释放/完整周期/质量/容量/收尾回归 | 原021条件和必要保存/释放，无早Final |
| FR-001/002/SC-007 | 交付后真实输入/模型应用/常驻专项 | 实际程序/版本/结果/释放/冷稳态实测 |

详见[quickstart.md](quickstart.md)。本轮没有软件测试/Host/设备运行证据。

## OPEN、外部依赖与决策记录

| 依赖 | 影响/补充时机 | 独立可继续 |
| --- | --- | --- |
| DEP-ALG-01 | 实际程序/模型/能力/IPC/取消释放，B真实适配及激活前 | A独立配置/转换/关联/监管/冻结/拒绝与Test同步链 |
| DEP-FMT-02 | PNG算法位深支持、PLY编码/RGB/3D伴图选择，局部真实激活前 | 已知Mono8 PNG/XYZ PLY转换、来源/所有权/文件与SQLite校验 |
| DEP-CAL-03 | 标定/坐标/物理槽，真定位前 | 观察合同及缺证阻断 |
| DEP-RULE-04 | 码/缺陷融合输出，真实接收前 | 原质量/追溯/Test调度 |
| DEP-VER-05 | 真wire/模型版本应用，真实版本验收前 | 快照冻结/错绑拒绝 |
| DEP-CAP-06 | 真耗时/资源/正式预算成本，生产准入前 | C/Q结构、Test预算 |
| DEP-SITE-07 | 现场复位/轴/完整周期，另行现场授权后 | 本轮及离线，T055/056未验 |
| DEP-DOC-08 | 008–011维护位置/资料可移交，共享代码前 | 022合同/消费者登记 |
| SRT-SAFETY | 特定共享目标/不足容量的等价证明，对象映射改造前 | 安全独立路线与采集计算重叠 |
| G2-MODE | 本次用户已确认在既有RealDeviceCommissioning接入真实算法；无需再问用途 | 同用途配置/装配增量可设计实现；真实激活仍依实际交付/Ready/预算与最低A证据，Production拒绝 |

SRT-SAFETY是直接安全衔接核查项，不新增机械业务权限；必要共享容量依赖不等于无关对象计算。不能证明安全等价的具体配置保局部限制，不删原准入。研究决策见research；plan阶段结束于Phase1，后续授权的任务拆解见[tasks.md](tasks.md)；不自动实现。

## 客户确认原型检查（P12）

不适用：无页面/桌面/原型修改，Host仅后端组合根。来源原件、其他功能任务、安装目录保持原样。

2026-10-09 plan阶段历史文档核查：共有11份本功能文档（含原spec/requirements），其中新增plan/research/data-model/quickstart及5项contracts；相对引用全部可解析，无模板占位；3项确认、24项FR和7项SC保留，requirements仍34/34，仅表示需求质量。原工作区12修改及133未跟踪文件共145项SHA256与保全快照一致，原feature.json字节未变；隔离HEAD仍eb85aa4，已跟踪文件无差异，本功能目录未跟踪。前后plan hooks均为空。未生成tasks、未实现代码、未执行软件/算法测试、Host/数据库迁移或硬件动作，未提交推送。


2026-10-09后续tasks授权：已依据本计划生成[tasks.md](tasks.md)，40项均未执行，按契约/架构审查、最小实现、定向验证、实现审查及收敛组织；真实交付分支独立Blocked。原有plan阶段检查记录保留，不能据任务生成声称实现或验证完成。

## 2026-10-09 analyze后定向修订账

本轮按用户明确授权只修本文档及直接关联的contracts/tasks/data-model/research/quickstart；spec已确认行为无矛盾不改，requirements只读。原40项任务ID/勾选、历史证据和eb85aa4回退基线保留。修改前本功能完整文档副本：`C:\Temp\gaode-022-plan-review-before-98076f5721d84ce08309b7c8544c84c1\022-real-algorithm-pipeline`；同目录before.json含分支、HEAD、feature原字节及逐文件SHA256，只用于文档差异核对，不是业务验证。回退本轮文档须逐项比较该副本并保之后现场修改，不整体覆盖工作区、reset或clean。

| 审查项 | 修订位置与依据 | 文档状态/仍受限范围 |
| --- | --- | --- |
| I1 | C022-DATA I1、data-model资源投影、T004/T005/T007；现Apply无条件改动作字段，Recover按Operation分组 | 规则已明确；后续实现在投影/重放/恢复共同验收前不放行 |
| G1 | C022-DATA G1、T005/T027/T029；现GetUnfinishedRuns只查Terminal=None | 独立资源查询/Host核对规则已明确，未验证；不续算不改业务终态 |
| I2 | C022-PIPE I2及S2/S3、tasks正文/依赖图 | 启用前置依赖已同步；仲裁/控制/冻结最小证据缺一则T017不得启用 |
| G2 | C022-ALG G2、配置段、T009/T010/T012及research | 原用途/存储/生产拒绝规则已修正；真实算法联调Host增量仍受G2-MODE确认及实际交付限制 |
| U1 | C022-PIPE U1、data-model及T016/T029；现Reserve混计，已提交文件Dispose不减磁盘 | 三类额度及取得/转移/释放责任已明确，未实现；不引入新清理政策 |
| U2 | C022-PIPE U2、data-model及T015/T027/T029；复用既有释放时限与Host退出控制 | 一次起点/截止/合并语义已明确，未验证；生产实测值仍DEP-CAP-06 |

不将以上状态用于勾选实现或真实验收；020 T055/T056仍现场未验证。用途确认只影响G2-MODE列出的范围，不阻断其余独立软件任务。无初始化/升级、现场配置/库迁移、代码/测试修改、Host或设备执行、安装/部署/提交推送。

本轮文档核对：修改8份本功能文档，spec及requirements与修改前SHA256一致；40项任务连续唯一、全未勾选，正文依赖无环，全部本功能相对文档链接可解析。分支/feature仍022，HEAD仍eb85aa4，已跟踪文件无差异；原工作区145项摘要及原feature字节不变。现setup-plan已执行并跳过模板复制，前后plan hooks为空；这些是文档/保全检查，不是产品或测试运行证据。


## 本次R1/R2/R3及旧问题状态

R1由C022-DATA及T027规定实际InitializePersistenceAsync/停止通知/StopAsync消费者验收，独立扫描器通过不算Host通过。R2采用C022-ALG“独立Host配置实际入口”具体字段和消费者，不扩严格公共配置schema。R3统一T010为T017前置，A基本路径不依T017。

I1（T004/005/007）、G1（T005/027，含R1）、G2（T009/010/012/031，含R2）、U2（T005/015/027）设计明确待实现；I2入口前置设计保留而C不触达延期（含R3），基本仲裁/监管/冻结仍属A；U1基本逐帧/持久磁盘及转换工作保留属A，批次后台预约优化属C延期。延期不代表缺口实现修复，所有任务均未执行。

本轮修改前完整022文档副本：C:/Temp/gaode-022-target-before-9a8a199496e44dbc850dc5982ccc60a9，before.json保存原摘要。仅逐项比较文档差异并保留后来修改；HEAD/原feature/历史回退基线不变。requirements原SHA256=2D80B4CEE4D4DD4F684AA6C456B795B8488B5AB616907DCD895466019298F3F7，只读；原质量清单勾选不能证明新阶段/格式适配已经验收。旧任务/plan生成记录中的历史数量和用途待确认保留，不作为当前状态。

A基础共享实现衔接：生命周期沿StageEvents权威流独立分页查询/重建资源投影；只含资源时不建立虚假业务投影行，业务DTO LastEventId可缺项，既有业务行schema保持；RunFactCommitCoordinator覆盖短提交。见contracts/persistence-lifecycle.md的A阶段增量及review.md，不启用C句柄，不自动迁移现场库。
A实现补充：IMediaStore.PrepareAlgorithmInputAsync由Infrastructure媒体适配实现，非算法核心；无新增数据库表，Media权威payload扩展可空来源字段，旧模拟读取不变。
T015-A/T023-A采用AlgorithmRuntime.DispatchSynchronousAsync/ManagedAlgorithmCall复用IsolatedAlgorithmCall；公共3D/F与产品E/单图/融合/复查均入同一资源监管，原同步消费者仍等待原结果及释放。
T004/T006-A签名：CaptureAsync返回PersistedCapture；四处窗口消费者改为Completion.ForWindow(session,trayId)；真实与模拟PLC均校验IsFor当前窗口，转换输入与此原采集保存证据独立。
T023-A同步Real冻结描述→媒体转换保存→ManagedCall；保原已知Pending规则，缺NoWorkStarted保证的超时/断线禁止整段重发。
T015-A/T016-A/T023-A复用IAlgorithmPort的每Role输入声明，以明确Test格式提供者贯通正式配方同步链；声明不改变用途/源/模型就绪或存储门禁。

A资源恢复增量：实际Host恢复未确认输入时，对已Ready的原输入重新取得工作保留租约（不增加/减少实际文件磁盘）；缺少输入仍明确Unknown并报告。未回收资源存在时Start公共准入在任何新采集/动作前拒绝AlgorithmResourcesUnconfirmed；不由重启或Test计数清空。资源核对不恢复业务执行。

A真实选择准入增量：独立真实描述选中时，实际能力未注册/不匹配或算法未就绪属于RealAlgorithmNotReady控制阻断，公共Start保存ConfigurationBlocked且不派发采集/机械动作；原显式虚拟分支原有算法问题分类保持。配置读入不等真实就绪。

T026/T038-A关联修正：已绑定WorkerSession的Call拒绝缺失或不同会话事件，匿名InputReleased/WorkerExited不能解除原占用；本调用的成功Exited任务仍是可靠执行结束证据。结果必须保持原FrozenModule模型/参数文件版本及摘要身份，不只匹配配置摘要。

## T041–T044最小消费者修复

沿现AlgorithmRuntime/IsolatedAlgorithmCall/Supervisor修复公共释放与实际进入准入；ManagedAlgorithmCall增加Result和LateResult两个只读任务：原结果/超时/取消共用单锁裁决，消费者使用Result，LateResult仅服务原有限Pending诊断。RecipeDetectionExecutor单图/E/融合及Observation同步消费该裁决；不引入新业务端口。WholeTrayCompletionStore在Final提交及Reconcile释放前查询本Run最新生命周期，存在未回收时拒绝；原机械/人工条件仍全部成立。Host关闭调用Runtime按各Call原观察剩余时间等待，Host/外部Token截短，各Call到期仍Unknown，事实保存另用Host剩余窗口。定向复验T041–T044并独立只读审查，旧失败证据不替换。

T043审查修正：移除产品/E/融合的重复结果WaitAsync时限及复查的重复算法CancelAfter，只保受管Result原due和业务控制取消，避免受管终态与消费者独立时钟竞态。定向复验原同步消费者，不更改阶段期限或释放预算。
## T045最小实施决定（2026-10-09，先契约后代码）

沿现Supervisor.admissionGate新增内部TryEnter(IsolatedAlgorithmCall, tick)，和BeginShutdown统一排序；门内仅调用内存MarkEntered，不调用外部算法、日志/保存或取消。IsolatedAlgorithmCall.Start接收现有Supervisor，两个Runtime入口均传入；移除其资格lambda中不能解决竞态的AdmissionClosed判定，原控制/截止判定保持。无Supervisor的既有明确测试装配仍按本地受管进入，不新增调度器或真实Ready。

RegisterAsync在门内预置首次保存完成任务并登记，门外启动/等待实际AppendResourceAsync；后续SaveAfterAsync依原修订序列等待该任务。沿现RuntimeDiagnostics记录资格、许可、拒绝、进入和逐Call关闭，全部在门外。

仅追加一个可控交接理论用例，覆盖公共3D/F与产品同步以及关闭先/许可先。使用实际Station01HostedService.NotifyStopping/StopAsync和隔离SQLite、媒体、明确Test端口，诊断订阅控制交接；保既有登记阶段CloseBeforeEntry互补用例。复验直接受影响受管/Host和普通特殊主链，不执行真实设备、算法包或C优化。
T045本机离线验证隔离补充：既有实际HTTP Host夹具不能依赖执行账号的Windows EventLog写权限或机器代理；测试显式仅注册原持久FileProvider，并让HttpClient直连127.0.0.1。此项只改测试装配，不改产品Host、预算、API或部署配置。已出现的EventLog权限异常/响应中断和初始就绪证据超时保留原日志及TRX，按原预算复验。
T045验证环境补充：既有Host夹具每条日志重复打开同一文件，两个日志入口各自加锁，原700/1000ms算法预算下出现入场前I/O超时。仅该夹具共用一个持续打开、按行刷新的日志写入器；保留全部日志和原预算，不改产品日志/业务流程。

## 2026-10-10 speckit-plan定向增量（现行）
实际Git分支sync/022-real-algorithm-pipeline-20261009、基线519cc8f；setup-plan解析FEATURE_DIR为022并保留既有plan。以下优先于历史“未交付/结果决定路线/禁止推送”范围；本轮只文档。完整wire、配置和处置唯一来源为[阶段合同](contracts/stage-integration.md)。

| 技术决定 | 最小落点及理由 |
| --- | --- |
| 运行环境/逐模块Ready | T011核CPython3.10/3.11、依赖/GPU和实际模型路径/摘要；独立环境与项目侧启动配置，不改包。首切片仅检查其所需DefectSingle模块，仍如实保其他模块NotReady |
| 真适配 | T012新增RealAlgorithmAdapter和包外gaode_real_bridge.py，真实导入AlgorithmService；保IAlgorithmPort与T045，不扩大Test白名单。一次在途串行/常驻复用；仅复用能核对的进程监督代码，不建并发平台 |
| 媒体/冻结 | 复用T016的已提交Mono8 PNG、Float32 XYZ PLY、元数据/sidecar/索引；T031冻结本Run模型/参数/转换/策略版本。无新采集SDK或二次采集路径 |
| 结果保存 | T047在现AlgorithmEvent最小增量RawPayloadJson及实际版本/参数摘要，通过现AlgorithmFact/StageEvents保存完整native成功或错误；结果文件引用与源媒体关联，不另建表平台或舍弃大结果 |
| 业务投影/查询 | 技术状态、质量/Unavailable、完整性、流程和实际路线分轨，Run/evidence既有后端出口；字段契约为stage-result/1增量，旧记录缺项不反补。Result格式/版本解析留Infrastructure，业务不解wire |
| 既有页面 | 021 RM-STAGE和T083消费后端查询，在既有区域绑定；022只定义查询事实，不安排页面DOM/布局实现 |
| 阶段路线 | T048在现配置校验/冻结/StartPublicPreparation及RecipeSortingMapper直接消费者增加显式策略分支；不建新执行引擎。StageFixedRoute从确认配置选源/目标，绝不通过改质量复用mapper；先保存原始事实，再按原序等待必要资源/设备条件继续 |
| 结束与异常 | 项目桥显式CallEnded才兑现Exited；逐输入释放可信聚合。保未知占用/原期限/Host门；T046补真正入场后过期核验，不把100ms入场前拒绝算退出证据 |
| 最终切换 | T050完善质量/标定/缺码/完整性/真正融合及ResultDriven验收，策略冻结只作用新Run，不依赖T017/C |

最小顺序：T011-S1环境/包核验 → T012-S1真实端口与所需Host配置（配合T016-S1、T031-S1） → T047-S1完整结果保存 → T013-S1真实首切片核验 → 021:T083现页绑定 → T048阶段路线 → T049必要主流程。T046可与首切片代码工作分开，但对应实际设备Host激活及宣布生命周期全绿前必须闭合；T050最终目标保留未执行，C继续延期。
首切片选择已保存配方中的首个已确认实际检测位/相机和DefectSingle，不写死坐标、相机A或槽号；范围是组件证据，不冒称完整Detection/Final。扩展由实际配方的1/2/4/更多面和普通/特殊/组/整体运动顺序决定，不新增排列组合矩阵。
共享payload、Host配置和策略签名均先按本轮spec/contracts/plan/tasks实施；实际字段schema和直接消费者同批修改，禁止兼容层/测试生产特权/新页面参数。
宪章复核：P01承接最新决定；P02真实核心接入；P03/P04配置来源和原设备屏障；P05前后端/适配边界；P06/T045真实释放；P07/P08完整原始事实和SQLite；P09关联日志；P10范围依赖；P11最小直接消费者改动；P12原型保护；P13分阶段完成。无修改宪章或来源文档，无质量精度或C并发作为首切片前置。

包外桥拟新增路径：backend/src/Gaode.Infrastructure/Algorithms/gaode_real_bridge.py；RealAlgorithmAdapter.cs和必要真实协议解析位于同目录。所有包导入路径由严格Host描述文件提供，不能用当前上传临时路径硬编码为部署路径。已确认路径/权重/参数配置记录而不拷贝其内容进源码。
