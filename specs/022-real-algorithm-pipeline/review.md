# 022 A阶段实施审查与基线

## 实施前核对（T001–T003）

2026-10-09，分支022-real-algorithm-pipeline，HEAD eb85aa4b2985e61171b9d1d749af346207282d9f，feature.json指向本功能；已跟踪源码无差异，现有022文档全部保留。实施前完整文档、源码摘要及差异清单：C:/Temp/gaode-022-A-before-f541ae18dee444e08eb646b5ff6fe697。源码原内容可从该HEAD逐文件读取到另一个目录比较，不reset/clean/覆盖工作区；未提交改动以差异及新增文件清单保全。旧回退标签不冒称本次基线。

读取AGENTS、宪章9.0.0及本功能spec/plan/research/data-model/quickstart、五契约与40任务。最近两轮分析为会话报告，原目录没有review/validation运行证据；不得补造历史测试通过。最近结论为I1/G1/G2/U2设计待实现、I2/C入口延期、U1分A/C，R1实际Host、R2实际配置/冻结重读、R3 T010前置。requirements 34项已勾选，仅历史需求质量，无未勾选；保持原摘要/字节，本轮按实际A范围验收。

消费者审查：StageEventProjection.Apply/Initial、StageEventStore.Append/Recover/Read与TraceQuery；Runtime/Isolated/LeaseSupervisor；所有FinishCaptureWindow与RequestAsync调用；MediaStore/Capacity/Registry/CameraCaptureJournal；Program/Options/Registration、公共校验/能力、Freezer/StartPublicPreparation/IndependentRecipeApplication、实际Host持久与停止、CommissioningRecovery资源判定；原检测/复查/融合/分拣/Final。PLC协议不进入业务，格式转换在媒体适配，测试替身仅Test。

008–011当前工作树仅有契约，原规格维护来源仍为spec记载的E:/dzk/gaode-1只读位置。此次用户授权先同步“本功能”共享契约；022作为直接增量维护位置，旧来源和其他功能勾选不改。对应接口的必要行为已登记于022五合同；实施发现与旧合同实质冲突时只阻断对应文件并报告，不静默改旧规格。不把来源缺失解释为缺算法，更不伪称已回写其他功能。

唯一编辑者为本轮主代理；无独立代码编辑代理。原同步机械顺序保留，T017不启用，真实激活待B交付，现场部署/程序/硬件均不触及。

## A基础最小持久实现决策（先契约后代码）

生命周期沿StageEvents pipeline/1权威追加，不另建事件平台；资源查询投影从该权威流分页重建，不另迁移现场库。只含资源的阶段不创建含虚构业务身份的StageProjections行；业务DTO的LastEventId允许null，真正业务投影行仍保持既有非空字段。事件序号以真实StageEvents序列为准，资源追加可推进已有业务行的Revision/保留期限而保持身份。普通业务恢复在Operation分组前过滤资源事件。此选择避免为了未启用C句柄扩大数据库结构；独立资源查询不依赖Runs.Terminal。

每Run短提交协调只覆盖revision/提交及阶段追加，不持锁等待算法、动作或配图；生命周期资源保存可在业务异常终态后继续。实现/验证未完成前不勾任务。

## 单独只读实现审查（T037-A，验证后）

按实际diff及消费者逐项阅读，审查阶段未修改代码。发现S1：IsolatedAlgorithmCall.Observe已绑定WorkerSession后仍允许null Session的释放/退出事件，可能错误解除旧输入。严重性High，I1/G1/T026直接相关；最小修正只拒绝缺/错原会话事件，并以原代表失败用例验证匿名退出不能回收，可靠原会话退出仍可回收。发现S2：算法结果关联未核FrozenModule，可能同Call被不同模型引用冒用；严重性High，最小补冻结模块身份比较，原关联用例复验。机械顺序/PLC地址解释未变；真实组件缺失不伪Ready；未启用T017。以下修复另作为T038阶段，不在只读审查中边读边改。


T037-A补充只读发现S3（Medium）：StageEventStore.GetUnreclaimedResourcesAsync排序仅RunId/OperationId，同Operation多个Call缺少稳定唯一分页键。G1分页消费可能受等键顺序影响；最小修正为既有ORDER BY补CallId，不改筛选/资源事实/恢复用途；沿原同Operation用例增加两页独立Call断言。以下属T038修复/定向复验，不在只读审查阶段修改代码。


## T038定向修复与最终独立只读复查

S1在IsolatedAlgorithmCall.Observe拒绝绑定会话后的null/不同WorkerSession，原Exited成功仍有效；S2在AcquisitionContract.Matches加入FrozenModule及完整原请求身份。identity-final 4/4及review-fixes 11/11验证。S3 ORDER BY加入json CallId，stable-pages-final 4/4包含同Operation跨两页无重漏及实际Host消费者。三项已修复且直接复验，不是新增任务文本即关闭。

修复之后再次单独只读核对最终diff/新文件与Media转换、Runtime、Supervisor、实际Program/Registration/Start/恢复及分拣/Final消费者。没有发现本轮剩余确定的阻断性软件缺陷。转换仅媒体适配；PLC协议留通信层；无生产按Test名称/GUID/fixture/本轮证据环境变量分支，无假Ready/固定Real成功。原同步顺序/显式虚拟保留，未启用T017/C。真实提供者尚不存在，NotIntegrated保持；没有部署/硬件审查结论。

T039消费者核对：公共3D/F、产品单图、同步E、融合和复查均经受管Runtime；正式算法port RequestAsync由Isolated边界调用，原错误4guid窗口回执构造已移除。同步ExecuteAsync、正式虚拟提供者和Worker/2.0仍各有原用途，不因C延期删除。不另造配方/存储模式或DB迁移。源码行结束恢复到原对应行，消除无业务意义的整文件diff。

最终范围/来源/失败复验及任务子范围见validation.md；38项必要用例由分批成功证据证明，不冒称单次全通过。Test完整Host两种机械代表与两Run均真实SQLite重读；PLY仅离线真实媒体转换，不声称正式真3D调用通过。现A可以范围限定收敛审查，B/C及真实输入/模型/标定依赖未关闭。


## 2026-10-09 T041–T044独立只读实现审查

审查阶段只读当前本轮差异、契约及实际调用消费者，旧S1/S2/S3和converge F1–F4均保留。范围仅四项A修复，未扩算法桥/模型验收/C并发或全故障组合。

发现R4：产品单图/E/融合仍在受管Result外用独立WaitAsync结果计时，复查又设算法CancelAfter，可能使消费者与生命周期各自裁决。依据C022-PIPE唯一终态要求，另行修复阶段删除重复结果计时，保控制Token、原due及独立释放/阶段截止；公共生命周期结果投影也承接原Ingress裁决，不让迟到Result抢占超时。之后重新只读核查并运行最终12组件+7 Host，全部通过。源码与验证索引见validation新节及证据根。

最终核查：公共可靠输入/执行结束后才返回成功，超期不派发F或Final且迟到释放不续算；Final/已提交Final释放核本Run持久最新资源，不按业务终态清空。公共实际进入许可查Host关闭准入，注册成功不等派发许可；未知不得回收。受管唯一终态覆盖Result/Failed/Timeout/Cancel/派发错误，身份/会话/冻结模型版本仍核对；迟到诊断只供原FinitePending，取消不可转成功。实际Host按各Call原资源窗口等待，不把媒体等待或Host5s重开为Call窗口；超期Unknown持久可查，Input/Execution/BusinessEnded分别保持，可靠迟到证据才回收。

无新增PLC地址/反馈解释进入业务层，无SDK/格式解析挪到配方，无按测试名/GUID/fixture/环境变量变更产品行为，无测试端口注册假Real Ready；所有控制探针/固定结果仅在测试。原同步await、机械/分拣/人工Final顺序、模式及存储保护保持；T017及C入口没有启用。原媒体、内存/工作/磁盘额度逻辑未改，Unknown租约保占用，取消媒体“等待”不释放其租约/文件。四项范围内未发现剩余确定软件缺陷；实际算法桥/接口联调后续单独授权，不把精度/模型验收当本轮前置。可再次converge，不表示真实算法接入或现场通过。

证据补强（仅测试导出，无产品变更）：host-resource-evidence/host-resources-*.json保存实际Host每次SQLite新读取的Unknown清单、原截止及关闭快照，对这两个已有初始化/停止用例复验2/2，不计新增覆盖。late-recipe保存对应已通过迟到用例的隔离Test SQLite/媒体副本；late-recipe-readback.json再次内存读取数据库/WAL完整性ok、7个调用全部Reclaimed、唯一TimedOut及9份媒体，绝非现场库。

## 2026-10-09 T045范围内实现审查

基线7cbba95，隔离工作树gaode-022-t045，Spec Kit实际FEATURE_DIR为本022目录；requirements 34/34只读且SHA256保持2D80B4CEE4D4DD4F684AA6C456B795B8488B5AB616907DCD895466019298F3F7，扩展hooks为空。先更新spec/contracts/plan/tasks，再修改四个受管算法文件。

TryEnter与BeginShutdown共用admissionGate，门内仅读关闭标记和MarkEntered内存状态；日志、观察/数据库保存、RequestAsync及取消均在门外。登记首次保存的完成任务先入表，门外启动数据库写入，后续修订等待首次保存，避免关闭保存倒序。公共3D/F和产品同步均传同一Supervisor；成功许可后不重新读关闭标记否定派发。取消/唯一结果裁决/未知资源管理和T041–T044逻辑未撤回；释放起点/截止由既有Start维护不重开。生产Host装配Supervisor，无Test端口注册真实Ready。

六项可控屏障验证先关闭端口0、先许可端口1并保未知租约，可靠退出才回收；重读SQLite UTC/tick截止不变。原组件12项与Host7项分批通过，包括普通/特殊两轮、同步释放、取消/超时唯一裁决、Host退出；无新增大规模异常矩阵。仅HTTP测试日志/loopback代理装配另作环境隔离，持续文件写入保原日志/预算/断言。失败尝试详见证据索引，不隐藏或追溯改写。

本范围未发现剩余确定软件缺口，可交下一轮独立只读复核/converge；不自行宣称整022或真实算法完成。B交付待核，C/T017未启用；PLC/虚拟PLC/前端/现场配置、数据库及部署包未修改；上传目录不纳入提交。详见t045-handoff-20261009.md和evidence/t045-20261009。

## 2026-10-09 T045合入主目录后的定向复核

主目录E:/dzk/gaode-1，分支sync/022-real-algorithm-pipeline-20261009；从009e084c合入远端已核对一致的38918e9，合并提交3281227，无冲突。保留已迁移CommissioningProtocolTcpFixture四处引用、所有本地历史资料和PLC修复；没有删除已有跟踪文件。算法产品源码与T045来源一致，PLC/Host配置/前端未改；两个上传目录保留且未跟踪，不提交。Spec Kit实际FEATURE_DIR为本022目录（使用既有feature.json），未改任务状态，除合入已有T045完成标记。扩展/算法包/权重/部署包/现场数据库和设备均未操作。

复核共同admissionGate中的TryEnter/BeginShutdown排序、门内仅内存Entered、门外算法/日志/保存和两入口传参；已进入调用保资源归属，Start不重置已有释放UTC/tick起点与截止。新六项可控交接均通过，实际SQLite重新读取、先关闭0次调用、先许可1次调用和可靠退出前不释放断言均保留。

主目录Release完整后端构建通过，0警告0错误。定向测试10项，9通过/1失败：六项T045交接与activeExpired=false通过，普通/特殊Host主链各两轮通过；activeExpired=true仍失败于RealAlgorithmPipelineLifecycleTests.cs:94的DispatchSynchronousAsync，AlgorithmNotDispatchedException: OriginalWindowClosedBeforeEntry。原100ms请求窗口包含资源登记和入场交接，在进入适配器之前已耗尽，未触达目标“已进入后超时退出”情境。未采样各项I/O/调度耗时，不将该失败推断为PLC/算法包或Host关闭故障。没有改预算、断言或产品/测试实现来通过；旧sync-validation.json字节不变，旧失败和本次失败均保留。

证据见evidence/t045-local-merge-20261009/merge-validation.json和两份TRX，原日志/测试导出保留artifacts/t045-local-merge-20261009。此前隔离工作树25个不同用例的证据保持，不能替代本机这次未通过的生命周期用例。可以进入第二步“更新阶段联调规格和任务”，但须显式携带生命周期验证缺口；真实算法接入/退出验收仍需后续核对，不能宣称全绿或真实接入完成。T017/C保持禁用，本轮未提前实施固定路线/算法仅记录阶段。

## 2026-10-10阶段设计复核（仅文档）
基线519cc8f；现行用户决定为真调用/真保存/现页显示后按确认路线，最终结果驱动独立保留。spec/阶段contract先更新，setup-plan保既有plan并实际定位022，再定向研究/模型/计划/quickstart，setup-tasks定位022后保旧任务、追加T046–T050；021仅必要独立页面规格/契约/计划及T083。扩展hooks为空，未执行implement。
实际差异：Real配置IsReady恒false且无真端口；包有原生0.3和可选Worker2.0，但没有本Call结束消息；现结果payload丢几何/完整native错误/双图明细；现mapper按判定分目标。最小桥、原始结果增量和显式配置路线分别归T012/T047/T048，原T045/释放/PLC保护保持。Pending/未融合/3D映射与F定位缺项分别阻相应能力，不统一封锁独立DefectSingle真实接入。
首切片任务为T011/T012/T016/T031/T047/T013的S1，不提前阶段路线/最终规则/页面实施。T046保生命周期缺口，T050保最终条件；C/T017未启用。当前只是设计，不声称Bridge协议已实现、模块已Ready或生命周期已全绿。
来源/包/原型/两个上传目录只读；项目AGENTS、宪章、022历史证据/旧勾选未改。原型ZIP三页及SHA与021独立规格匹配。只核静态文档与关联路径，不安装/启动算法或设备/改库/跑产品测试。
