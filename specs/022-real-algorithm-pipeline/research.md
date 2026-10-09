# 022研究与决策记录

日期：2026-10-09。依据[规格](spec.md)、Q1=A/Q2=B/Q3=A、现有源码及宪章9.0.0。只读研究，未执行程序、算法或硬件；所有生产交付缺项是具名依赖，不以推测填补。研究任务分为流水线/保存和运行时/装配两组，均已完成源码核对。

| 决策 | 依据及理由 | 已考虑而不采用 |
| --- | --- | --- |
| 扩展现有AlgorithmRuntime及IsolatedAlgorithmCall受管边界 | Runtime当前只覆盖公共TrayPose/FDecode，产品检测/E/融合/复查直接调端口；统一关联、期限、取消和回收职责 | 另建通用任务平台；只删除await；每相机一个进程 |
| Detection生产阶段与对象结果屏障分开 | RecipeDetectionExecutor.ExecuteAsync及RecipeSortingMapper.MapAsync当前依赖整个Completed；普通全采后原序分拣与特殊逐件不同 | 部分Objects冒充Detection.Completed；按结果到达顺序跳拣 |
| 采集证据替换算法结果作为窗口结束前置 | IAcquisitionCyclePort.CaptureWorkCommit只有WriteIds/MediaReleased，适配器只验非空；窗口OperationId与CaptureRequest.OperationId可不同 | 任意保存ID放行；把文件租约释放误作相机缓冲释放 |
| 使用现文件/SQLite及必要事件投影增量 | MediaStore、TraceWriter、AlgorithmCalls、Writes、StageEvents已有正式保存链 | 新数据库、持久消息总线、自动重放未知任务 |
| 每Run短提交协调段 | SaveTraceAsync先读revision，TraceWriter单读队列仍无法解决并发预读CAS；StageEventStore也有阶段序号竞争 | CAS失败自动换ID重写；锁内等设备或算法 |
| 执行/队列与待配图文件保留额度分开 | CaptureStage按A批后B批顺序；首图持有全部容量会挡住配图 | 改机械次序交错A/B；融合占执行槽等另一图 |
| 正式提供者独立于测试Worker装配 | Production现为NotIntegratedAlgorithm；PythonWorkerAdapter为Test；现Worker选项仅VirtualPlcIntegration可用 | 修改Origin标签/测试白名单；静默虚拟回退 |
| 正式入口按已批准用途衔接，Production继续拒绝 | G2复核：存储Profile只支持Test/RealDeviceCommissioning，Production执行成本亦未批准；原“补齐生产入口”措辞不能视为生产授权。真实算法联调用途由本次G2-MODE确认，激活仍待实际交付/就绪 | 新生产Profile；放宽Test根；改现有联调模拟矩阵冒充已批准真实用途 |
| 生产wire在交付后定稿 | Codec.Execute未发送内部参数/能力版本；当前station01-worker/2.0不证明生产模型已应用 | 将V0.2样例当生产协议；预设Python/SDK/文件转换 |
| 可靠退出独立于结果/释放 | IsolatedAlgorithmCall分别登记业务结束、派发返回、执行结束；Test adapter以全部InputReleased完成Exited；Supervisor输出EOF不能证明OS进程退出 | 取消回执/超时/EOF当退出；Result后立即回收全部资源 |
| 保留全盘分拣配置和容量安全前置 | ThreeStageWorkflowExecutor先冻结全部目标/抓手，SortingTargetAllocator检查占用/重复目标/NG及Pending容量 | 删除全量准入，逐对象搬完后才发现原批次本不应开始 |

最后一项采用[分拣契约](contracts/sorting-barriers.md)的提前配置冻结与安全证据；只有真正共享容量依赖需要额外结果，无共享依赖的对象不得等待无关计算。无法证明安全等价的配置路线保留局部设计/准入阻断，不能由实现自行降低条件。

技术事实：global.json固定.NET SDK10.0.401、禁止rollForward；net10.0；EF Core SQLite10.0.12；xunit2.9.3、runner3.1.5、TestSDK18.10.1。均为仓库文件事实，不涉及升级或外部推荐。

外部依赖沿规格DEP-ALG-01至DEP-DOC-08登记；程序/布局/标定/规则/wire/性能/现场/旧规格维护位置未因此获得证据。Q1–Q3保持；前轮G2新增用途问题G2-MODE，本次已获用户确认；真实传输/未确认格式仍待交付，已知Mono8/XYZ转换可独立实施。

2026-10-09 analyze后定向复核：StageEventProjection.Apply原尾段无条件替换动作身份，RecoverAsync原按全事件Operation分组；资源事件须显式分流而非只保Status。Host当前GetUnfinishedRunsAsync只查Terminal=None，须独立资源查询覆盖异常终态。MediaCapacity.Reserve同时计内存/文件，Committed后Dispose不扣实际文件；批次工作保留不可直接占整批内存。既有WorkerReleaseGrace/InputReleaseWaitMs可用于每Call一次释放观察，Host退出CancellationToken限制阻塞等待而非证明资源已释放。选择见DATA I1/G1、PIPE I2/U1/U2；这些是本次文档修正依据，不是测试证据。


## 本次目标与V0.3定向研究结论

Decision：先A原同步链真实基本接入，B按实际交付逐项验证，C后续重叠。Rationale：当前目标验证真实算法参与后的既定流程，不把性能优化当启动条件。Alternatives considered：先启用T017或改Production均不符合最新授权；不删C历史设计/任务。

Decision：独立严格Host描述文件走Program现Gaode读取→Options→Registration真实消费者，字段/拒绝/冻结见C022-ALG R2。Rationale：现Program手动构造Options，旧CommissioningAlgorithm及其FreezeRun要求模拟Slots/RawCodes/Results，Freezer和公共Validator亦有直接依赖，单加adapter不贯通。Alternatives considered：扩公共业务schema/新模式/静默沿用模拟结果均不采用；原用途/Store及显式光源保留。G2-MODE由本次用户已明确，无需再次确认。

Decision：PNG/PLY原始媒体转换由采集/媒体适配层提供版本化来源/持有证据，core和IPC分开。Rationale：V0.3要求算法只读PNG/PLY，离线Mono8/XYZ证据支持软件转换但不证明算法接受编码、标定或正式集成。Alternatives considered：core解析raw、伪造RGB、缩放裁剪/删点及预设worker2.0兼容均拒绝。DEP-FMT/CAL/ALG等仍按模块局部阻断，真实协议待交付。

Decision：R1用实际Host InitializePersistenceAsync/停止通知/StopAsync验收，R3在plan/PIPE/tasks保持T010→T017且整体入口延期。Rationale：扫描器单测不能证明Host消费终态资源，后续集成不能替代启用前仲裁/冻结/监管。Alternatives considered：仅靠新增文字/独立manager通过宣布闭合不接受。
