# 故障双端复位与完整新轮合同（USR-20260926-D）



**状态**：2026-09-26增量设计，尚未实施或运行验证。取代现行U05同run、同operationId、attempt=2单指令重发。旧实现及证据适用于USR-20260926-C，原运行包和事实不可删除，不抵C07/F5新规则。唯一恢复业务协调归008 T068，设备与API归003 T072；持久控制基础归001 T078。



## 1. 三种操作与真实接口



| 操作 | 入口、身份与准入 | 执行与禁止事项 |

| --- | --- | --- |

| 正常暂停继续 | 既有pause/recovery-checks/continue；Run.Continue，expectedRevision、checkId；只允许真实Paused、未发生设备/保存故障且原物料/快照仍适用 | 同run继续未开展步骤，保留已保存事实和原期限；未确认停止不称Paused。不重新启动或重采公共3D/F |

| 历史人工换面 | 原批准/操作/面来源记录保留历史适用范围；当前无此待确认生产者，孤立manual-flip接口/服务/许可生成删除 | 当前配置翻转/放回后统一真实3D复查，不按人工确认命令面冒称观察；人工区安全仍阻断，无新机械或恢复信号 |

| 故障完整新轮 | 既有recovery-reset及recovery-checks负责双端复位与初始检查；Recovery.Check；随后显式POST /runs，Run.Start，新requestId、新commandId、新runId | 旧故障轮结束业务执行，保留历史；新轮从StartClamp、公共3D/F、唯一F绑定及全部适用检测/处置/保存/盘末开始。故障continue返回409 FaultRequiresNewRun，不能重命名attempt=2为重启 |



当前ControlEndpoints/FixedMoveRecoveryInteraction仍实现旧同轮重发；上述为目标合同。既有全局reset不能旁路本合同，重复旧启动requestId仍返回旧run，不创建新轮。无故障的内容异常/E缺码/算法Pending，在可靠机械、输入释放及保存成立时按原合同有限继续，不一概触发双端复位。进程重启/重连只重建历史查询和受限状态，不自动运动或自动新轮。



## 2. 复位顺序和真实初始判据



1. 受理授权复位时记录resetId、旧run/revision、故障/动作引用、actor/reason/evidenceRefs，关闭旧轮派发。读取最后可靠位置和持件/占用，不把取消Task解释为设备停止。

2. Host停止旧业务调度；取消并实际收敛旧采集/worker任务，收回已实际释放的输入/媒体租约，保存已发生事实及故障。未知物理资源继续UnknownHeld；必须保持PLC心跳、轮询、停止/复位控制可运行。Host清理的是当前阶段游标、待执行队列、临时面/绑定缓存、在途关联、旧预算计时及控制等待者；已持久事实只关闭旧执行资格，不删除。

3. 经唯一设备适配器请求既有System_Reset_Cmd，按当前协议映射完成请求清零和实际读取；机械复位归PLC（§4.3②③：断伺服、松夹爪、机构回零；§5.2初始化/回原点由触摸屏负责）。Host不得补写未知轴位置或PLC反馈。

4. 新复位generation/连接epoch下读取下面的InitialStateCheck。generation/epoch仅作关联隔离，绝非物理复位证据。状态不足或期限到达就保存Blocked原因，不后台重复复位/启动。观察期限采用现有版本化控制预算，1秒I/O、3秒心跳和动作期限不放宽。



| 判据与证据 | 来源和边界 |

| --- | --- |

| 本次复位后真实Connected、有效心跳、Auto，无SystemFault/安全报警，无人工占用/示教/软停 | 现有协议状态映射及互锁；用当前观察，缓存和人工true不能代替机械反馈 |

| PLC_Ready_State=1，复位请求已清零 | §4.3/5.2；Ready是必要项，不能推导全部机构释放 |

| 实际XYZ有限且位于当前合法版本化安全初始范围 | §1.5、§4.3②、§2.2；Test引用既有SIM坐标系、运动范围及虚拟回零，生产范围无依据时局部受限，不以XYZ=0硬写成功 |

| 无普通在途动作，PC启动/移动/检测/扫码/Flip/Sorting命令与ACK处于可启动初始态并实际回读 | §1.5、§3.1.5—3.1.7及现行方向映射；只读已定义反馈，不新造总复位完成位。检测1/2、扫码3/4与对应Z复位不得串用；旧完成反馈须先清零，动作关联进入新generation |

| Pallet_Lock_Status=0；新轮再实际Start并等待1 | §4.3松开与既有锁状态；只证明料盘锁，不能推导抓爪或特殊夹具释放 |

| 特殊机构活动结束且占用实际释放；不能自动判断的状态有人工实物核对依据 | §4.3⑤及旋转Test合同；生产未定义旋转占用反馈时只限制旋转分支，不发明寄存器。Test状态须来自真实VirtualPlc复位generation与占用观察 |

| 旧Host执行任务已停止；必要旧故障/媒体/已发生事实已提交，CommitUnknown已按原WriteId核实；逻辑owner可安全移交 | USR-D、P06/P08；PLC物理初始与逻辑释放分别确认，未保存不清历史、不启动新轮 |



成功检查保存initialCheckId/resetId、实际观察时间/epoch/generation、配置版本、安全范围版本、逐项Passed/Blocked/Unknown及来源、人工核对范围。未知项不能整体折算Passed。检查后条件变化使启动资格失效。现有ResetAsync仅Ready+Auto+无Fault，StartupReadiness缺完整初始检查，PlcObservation尚缺Sorting/PC命令清零等投影；实现时补既有映射读取，不声称已经具备。



## 3. 旧轮结束、持久关联与新启动



旧故障轮保留Faulted/RecoveryRequired及原故障结果，追加executionClosed=true、resetDisposition及持久reset/check事实；物理未定时仍UnknownHeld。业务结束不生成WholeTrayCompletion或Final，也不把故障改OK/Cancelled。历史查询继续返回原run、动作attempt、媒体、日志和异常；原WriteId提交核对不等于业务续跑。完成、取消或已由其他新轮消费的旧轮不可被continue复活。



复用POST /runs；设计在既有StartPublicRequest增加可空restartFrom={faultRunId,resetId,initialCheckId,expectedFaultRevision}，无此字段的普通启动结构不变。仅服务端验证来源，不接受客户端声称initial=true。对尚占物理owner的故障轮，普通start不可绕过restartFrom；选择新配置可沿既有加载/校验入口，但必须与当前初始安全范围兼容。



新启动先核验Run.Start权限、已持久检查/旧轮关闭、动态安全及当前epoch；生成独立WorkflowIdentity，在现有单写短事务中提交新run身份、旧故障关联、检查消费事实及命令幂等关系，然后才准入PLC启动。复用既有CommandRegistry键规则：相同新requestId相同内容返回同一新run；不同内容冲突；同一reset/check只允许一个新run消费。关联提交失败/CommitUnknown不得派发，先核对同WriteId；重复POST不能产生第二个物理新轮。必要结构扩展通过StorePrep及受控迁移实施，Host不静默改库。



查询最少返回faultRestart={faultRunId,resetId,initialCheckId,status,blockedReasons,newRunId,committedRevision}；旧轮可查newRunId，新轮可查faultRunId。状态为ResetRequested/Resetting/InitialBlocked/InitialReady/NewRunLinked，均为软件投影，不是生产信号。allowedActions只在后端核验后返回RecoveryReset、RecoveryCheck、RestartFullRun；不存在故障RecoveryCheckAndContinue。新启动202仅受理，不是完成。



新轮重新冻结公共配置/预算/模拟来源和配方意图，公共3D、F采集与解码及唯一绑定均重做；相同合法配置版本可选，但生成本轮新快照/测量/绑定事实。新run下所有operation/action/capture/call/media/step身份重新创建，attempt从本动作1开始；不是原operation attempt2。预算自新轮对应阶段实际开始冻结，不修改旧deadline。新轮只使用新轮媒体、worker结果及保存引用。



## 4. 新旧隔离与特殊机构



旧反馈经旧run/action/epoch隔离，仅写旧故障诊断。实际协议无run序号，必须依单在途、命令/ACK真实清零、复位代次与新动作受理闭环；迟到旧完成位不能推进新动作。迟到算法结果按旧call身份留痕/拒采用，不覆盖新结果；媒体仍归旧capture，可靠释放后才能移交共享资源。新轮获取新的Motion租约；现行ReconcileVerifiedReset保留旧owner仅供旧重发，不能直接用于新轮。



VirtualPlc SystemReset须使TestSpecialActions实际取消/结束旧specialTask、清当前specialActive/occupant并记录本次复位结果；异步回调检查generation，不得在复位后写旧XYZ/占用。保留specialResults历史。现行代码尚未做到，Ready不包含special占用。特殊Test状态读取扩展见[rotation-test-execution.md](rotation-test-execution.md)，真机缺乏可靠特殊占用证明时保留人工核对与局部Blocked。



## 5. 最少必要C07/F5验证（待实施）



| 场景 | 必须观察的结果/证据 |

| --- | --- |

| 初始状态不足代表 | 实际Ready不足/XYZ不安全/占用未释放至少按实际改动选必要注入；新start被拒，无PC_Start/新轮动作，逐项检查和原因持久可查 |

| 公共3D或检测阶段故障→复位→正式页面新启动 | 旧轮故障和媒体不变；双端清理/实际PLC复位/初始检查后，新request/command/run真实再执行公共3D/F绑定、全部必检面、必要保存及盘末到Final；不是后台代启动/代取盘 |

| 隔离与保存门禁 | 旧worker迟到或旧反馈不推进新轮；旧预算不延长，新轮独立预算；关联保存失败不运动，重复新启动仍单run |

| 暂停与历史人工记录 | 正常pause/continue保持有效权限/保存/取消；当前对象翻转放回后统一复查3D，F不重绑。历史人工确认记录不能授当前动作；故障继续/新轮仍按当前受限恢复合同 |



每包提供构建/配置摘要、旧新runId、reset/check/actor与请求记录、Host/PLC/worker实际轨迹、SQLite及媒体读回、Final页面和取盘事实。旧单指令恢复包仅历史，不能抵本验证；普通Q01—Q22未受影响部分按构建影响复用，不全量重跑或扩完整恢复矩阵。



实施细化：新轮Created及启动命令受理记录可先作为未获派发权的占位保存。现有单Writer在一个SQLite短事务中校验新run/command/request身份与两轮revision，提交旧轮RecoveryNewRunLinked、检查消费及新轮RecoveryFromFaultRun；任一条件或保存未确认，整项回滚/保持未知，不派发。新轮Created不代表恢复授权。复用现有Runs/Commands/Writes，不新增表或Host迁移；运行内存revision仅在同WriteId提交确认后同步。

## 2026-09-27 复位观察同步必要修复（原003 T072-A、008 T068/T069/T070）

必要顺序用例在Reset 202后立即Check实际返回RecoveryResetNotObserved，两次均未到链接保存注入，原r19 TRX保留。复位直接Modbus Ready已成立但缓存PlcReady仍旧false；原MotionCoordinator必要门禁不放宽。仅ResetAsync在原轮询/原期限内同时等待缓存实际PlcReady、Connected/SafetyClear，使用同一次observed快照；不改变接口、信号、初始判据或生产机械未知边界。评估见.specify/bugs/008-reset-ready-observation/assessment.md。源码当前尚未修改，待当前测试结束；现有两个保存门禁及正式旧图/完整新轮独立新包复验，已有任务承接不追加重复任务。

## 009 / AL03 当前共享接口（2026-10-01）

本节为2026-10-01已授权009共享接口定向对齐，规范性优先于本文件此前冲突的接口表达；历史记录/任务勾选仍只证明原范围。线缆地址、原值及ACK条款保留给通信实现和通信测试，不能再成为Application/Domain、业务端口、业务断言或API控制字段。业务含义、真实动作、安全、必要保存节点和原期限保持；实现/运行验收另按009任务，文档修改不代表通过。

维持USR-D：故障旧轮关闭，双端复位及实际初始状态核查后显式新run，从公共3D/F及绑定重新开始。原状态/握手由通信内部解释为复位/初始可靠结果；业务不用raw判断清零。旧事务提交核查只是事实核查，不续跑、不重发未知物理动作；已观察取料但raw失败按已存意图/预留保持占用，数据库全不可用不保证新事实持久。正常暂停及人工换面仍按原合同，人工命令默认面来源不得冒充PLC实测。


### 009 T033/T039 后段退出后的故障保存版本交接（2026-10-02）

integration238的实际三阶段UnknownHeld案例已提交检测事实至Run revision43，公共准备RunExecution仍持旧revision，故障收尾保存被CAS拒绝。仅在已等待后段执行返回UnknownHeld/解锁失败、当前执行停止后，故障协调器通过既有ITraceQuery有界读取同run的已提交状态，核对runId/requestId/subjectId/context、无终态、版本不回退且不存在活动配方应用保存窗口，再把现有运行保存游标交接到真实已提交revision。随后原CriticalSave与CAS不变；不循环重试冲突，不重放物理动作，不把只读核查或迟到记录当绑定、取放或后继动作成功许可。

原始故障ErrorCode及UnknownHeld事实保留，FaultRequiresNewRun仍作为恢复规则/事件和旧continue拒绝码，不覆盖已记录的设备故障原因。查询/保存失败仍无成功保存声明，不调整预算或重启规则。实施归009 T033/T039，验证保留原ThreeStageMainFlowIntegrationTests未知保持与零WholeTray断言，并补游标交接拒绝条件；当前修复未运行验证，不改其他功能历史任务状态。
