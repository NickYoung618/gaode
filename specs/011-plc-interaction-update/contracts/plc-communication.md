# PLC与VirtualPlc通信合同 plc-interaction/1.0



启动实施落位（W §1.2/§3.1、CR D16及关闭旧信号确认）：人工上料后的正式启动请求承接已提交StartIntent和运动租约；StartPreparationStep只确认同连接代次的实际设备就绪/自动/安全观察，保原PlcAcceptance窗口、取消和真实ActionFact保存。PC_System_Ready/PLC_Ready_State用于就绪握手，不再发送旧PC_Start_Cmd触发夹紧，不要求或伪造Clamp/PhysicalButton/ZoneConfigACK。新StartPreparationEvidence(Accepted,DeviceReady,DeviceEpoch,Observation及Operation/Action/Intent身份)替代当前StartClampEvidence；V1历史StageHandoff字段仍只读。共同移动/独立绑定/当前V2移交消费实际Ready和有效租约，不再以旧Clamp作为准入。无需新增页面或第二个人工按钮前置；新源Start_Button作为真实外部输入未被观察时不能伪称已按。012不需改共同类型，消费新运行批后仅接既有真实阶段。旧StartClampStep、ClampObservationPolicy及无用区域准备端口/当前装配删除；有效保存、动作互斥、有限等待和暂停/取消/未知保持。



日期：2026-10-03。状态：已分批实施；Test组件和当前架构证据见交接，完整联合及正式互通尚未完成。唯一所有者011。本文是通信边界文件，内部信号/原码不得复制到Application、Domain、业务测试或公开业务DTO。



## PC01 来源与定义归属



当前来源为只读`高德_文档/new-1/PLC与上位机通信接口协议.docx` §1.7、§2.2—2.4、§3.2、§4.2及同目录`上下位机通讯交互信号表.xlsx`；旧new目录协议是历史基线；用户已确认的新决定优先。Word中的旧示例地址不覆盖“正式地址后补”的状态，Excel空地址不填0。PC为Modbus TCP主站，PLC为从站。



复用Gaode.Plc.Protocol的定义/编码/访问计划校验、Infrastructure/Devices/Plc的正式设备与StageAction适配，以及VirtualPlc独立服务。不引入双协议常驻兼容分支。更换定义及所有真实消费者后，删除无用途旧实现；原历史reader和证据继续只读。



每个字段定义保留方向、类型/宽度、字序/位布局、来源与适用环境；正式未定字段明确Incomplete。准备校验只对本次依赖动作判断可用，不制造“全部字段齐备”去阻断无依赖设计，也不让Incomplete字段参与实际读写。已有越界/冲突/派生读访问计划保护继续使用，不能把缺定义变成默认成功。



新F匹配/冻结与业务绑定无需旧PLC配方ACK：型号只在相关机械动作内部实际下发。IPlcRecipePort旧绑定实现及固定DeviceApplied投影按消费者清理表替代；不增加新握手，不把TCP写入应答当PLC已执行型号程序。实际动作反馈保护保持。



## PC02 分轴映射（语义已确认，正式地址未交付）



| 业务用途 | 目标/触发信号 | 实际位置/到位信号 | 来源 |

| --- | --- | --- | --- |

| X | Camera_Target_X / X_Move_Start | Machine_Current_Pos_X / X_Pos_Confirmed | Word §2.2；表3—7、59及65 |

| Y | Camera_Target_Y / Y_Move_Start | Machine_Current_Pos_Y / Y_Pos_Confirmed | Word §2.2；表8—12、60及66 |

| 检测Z | Camera_Target_Z / Z_Camera_Move_Start | Machine_Current_Pos_Z / Z_Camera_Pos_Confirmed | Word §2.2；表13—16、61及67 |

| 扫码Z（F/E） | Scan_Target_Z / Z_Scan_Move_Start | Scan_Current_Pos_Z / Z_Scan_Pos_Confirmed | Word §2.2；表17—20、62及68 |

| 抓取/翻转分拣Z | Grab_Target_Z / Z_Grab_Move_Start | Flip_Grap_Current_Pos_Z / Z_Grap_Pos_Confirmed | Word §2.2；表21—24、29、63及69 |



沿来源拼写保留Grap与Grab在各字段的名字，不能为了统一名字错接反馈。到位语义按来源0运动中、1到位、2超时未到位仅在此边界解码。目标/实际/完成各自独立；不能把某Z的完成或实测值代替其他Z。位置比较还需当前动作、连接代次、用途、有效容差/单位，旧完成值不能自动归为本次完成。



011实施Test映射增量明确为测试约定：五个Bool触发放在coil 0x0022—0x0026；X/Y/CameraZ/ScanZ/GrabZ五个到位字放在holding 0x0080—0x0084；扫码Z及抓取Z实际Float32在0x0085/0x0087各占两字。测试holding容量256；不推断现场区/地址或未定承载。已有Test取值只用于当前独立VirtualPlc。ProtocolDefinition携Purpose与SourceReference，Real适配不得接受Test定义。新独立预期confirmed-011.json人工按此明确测试约定和Word语义填写，不从实现导出。



上位机只写PC→PLC目标/触发/已确认控制；到位与实时位置由PLC产生。目标先有效写入再触发；对应当前完成成立后按来源复位触发。不得让业务执行器负责这些内部步骤。F XY取本次3D观察，ABCD检测XYZ取冻结配置，E使用扫码Z；各用途的安全位置/容差从有效配置读取，本合同不编造数值。



当前动作轴选择明确消费业务用途：3D/FlipPick/FlipPutBack/Unload只请求XY；Detection请求XY后检测Z；F/E请求XY后扫码Z；分拣依PC04分别驱动抓取Z。反馈必须先观察本次各轴运动，再观察对应到位及实际值，按源清该轴触发。公开PositionObservation的AxisPurpose标XY时ActualZ为空，不用另一个Z充数；DeviceObservation独立轴集合分别保留五轴实测来源。



翻转取件MoveRequest增加可空FlipPreparation语义值，只有FlipPick必须提供TransitionId、Model、TargetPose、PhysicalEntityId、PhysicalSlotIndex。适配器在该XY派发前映射并下发型号/目标姿态；后续FlipRequest须与已保存位置及同一准备相符，不能到取件XY之后才下发程序。此值没有原码/ASCII字段，不改变recipe-contract/1.3。



## PC03 翻转与放回



Word §2.3已确认：

1. 写取件XY、目标面与产品型号并按独立轴触发；取得可靠X/Y到位后清对应触发。

2. Flip_Sorting=1执行翻转；Flip_Status=2才表示翻转完成。0空闲、1执行中、3失败按来源处理。

3. 单独写放回XY并触发，当前到位后清触发；Flip_Sorting=2执行放料。

4. Flip_Unload_Status=2表示放料完成且Z回安全位置，再清Flip_Sorting=0；不能仅见Flip_Status=2就报放回完成。

5. 业务收到各实体真实放回事实后安排统一3D复查；通信不按姿态质量决定下一配方步骤。



本次只读复核Word信号行另直接确认Flip_Unload_Status的0空闲、1执行中、3失败；新适配承接这些实际码，先前仅使用已确认完成2的源码批不能视为完整放回实现。原Word来源摘要B0C30492E31A5C285A7E7397B69F0EBEFF39CC088F14174A38C638C9436D0E51保持。



不再要求新协议提供独立实际面号或旧Flip_OK ACK。回执关联仍由适配器维护真实派发、连接代次、观察与内部动作状态；没有可证明的本次反馈进入UnknownHeld，不新造PLC序号、假应答或重发。



Model为大小写敏感文本语义，通信编码仍受DEP-02限制：Word写Float32、表写Real不能自行解释为ASCII数值装载。TargetPose/检测面号必须有有效程序映射；额外E的“第5姿态”不是原码5约定。业务可保存/规划已确认含义，但相关PLC实际编码互通尚不能验证。



实施配置边界：PlcPoseProgram位于Infrastructure/Devices/Plc，仅保存经来源标识约束的Model/ProfileId/ProfileVersion/PoseKey到目标面字及型号载荷字的映射，型号载荷使用明确配置的寄存器地址及原始words，不能由业务文字猜ASCII编码。PlcRuntimeOptions.PosePrograms默认空；缺映射在派发前报PlcPoseProgramMappingMissing。若仅有Test映射，其来源/范围明确标Test且Real适配不可使用；它不能证明正式承载。新FlipSorting/FlipUnloadStatus通信字段的正式地址仍缺，测试地址另外标记，不转为生产缺省。



## PC04 分拣



依据Word §2.4，源XY与抓取Z定位→Sorting_Cmd=1取料→上提安全位→目标XY与放料Z定位→Sorting_Cmd=2放料→上提安全位。Sorting_Exec_Status含义是0空闲、1取料成功、2放料完成、3抓取空/失败；不得使用旧Executing=1、Picked=2、Placed=3、失败4/5解释。



新动作适配复用IPickCommitPort：可靠取料事实交业务保存，当前有效真实提交回执是授权放料的必要条件；若保存/取消/期限不成立，不能发后继放料。放料完成与最终安全位置及必要占用提交分别记录，业务只消费语义事实，不维护原码/ACK。



不要从本段失败码决定自动重试、跳过或恢复；源文“锁停”不等于已明确所有恢复动作。当前动作未知保持原保护，新的恢复/安全处置仍按DEP-03/04。



分拣实施落位：通信配置PlcRuntimeOptions.SortingSafePosition使用PlcGrabSafetyPosition(GrabZ,Unit,Frame,Purpose,SourceReference)，没有默认高度；只有来源/用途/单位/坐标系匹配目标才允许依赖分拣。Test组件显式提供自己的值，现场仍待有效配置。主机按PC04逐轴驱动；Sorting_Exec_Status按源0空闲/1取料完成/2放料完成/3失败，不保旧Executing/4失败/5满位或Sorting_OK。取料确认及IPickCommitPort真实提交先于抬升和搬运；放料完成后再抬升，最终清命令不以清零充动作反馈。下一次取料核非持件及新位置/当前指令关联，不能把上次取料完成当本次反馈。Unload仅XY，不写GrabZ/旧XY命令。普通槽身份只在业务事实关联，新源不下发旧Sorting_Part_Index。未知或取消不派发后续动作，未确认输入不补安全高度。



## PC05 VirtualPlc与诊断



VirtualPlc经正式Modbus访问相同通信定义，保持目标寄存器与实际位置分开；收到合法命令后由自身状态推进、生成中间和终态反馈。不得从Host直接设置完成或把目标写入回显当测量。测试用途映射/运动数值须单独显式来源，不能用于生产批准。



现VirtualPlcEngine/DeviceActionAudit已接独立轴与新动作；其剩余无生产者旧单Z分支按下方清理义务删除，监控仅展示真实观察。VirtualPlc不是业务引擎，不根据测试编号、图片、对象质量或配方名称决定下一动作。



原特殊机械Test HTTP入口及当时消费者已随稳定011实际删除，历史证据保留。新路线依正式共同动作与通信能力；在对应机械含义未获有效映射前保持该局部受限，不增加HTTP兜底或备用整段流程。



## PC06 未决范围与最小通信证明



| 依赖 | 仍不能证明 | 可先完成 |

| --- | --- | --- |

| DEP-01正式地址/映射 | 正式读写/现场互通 | 语义接口、访问计划设计、显式测试地址下组件验证 |

| DEP-02 ASCII承载 | 型号实际编码及相关翻转/E端到端互通 | 大小写身份、配方保存/规划/关联 |

| DEP-05速度类型/单位/倍率/范围 | 该参数设置及依赖动作 | 无此参数依赖的确认部分 |

| DEP-06报警方向/类型/等级 | 对应编解码及处置证明 | 已确认故障语义、真实日志及其他信号 |

| DEP-03/04恢复、安全控制 | 新恢复/自动回位/安全现场验收 | 正常已确认动作设计、既有取消/期限/保存保护 |



后续使用既有独立ProtocolOracle及必要通信组件：分轴目标/反馈不串、E扫码Z、翻转与放回两事实、新分拣及取料保存失败不放料、当前动作关联/有限窗口/取消。独立预期来自来源文档/经确认测试映射，不从被测定义生成oracle；业务测试只断言语义。无需重跑009全映射突变、旧协议全部握手或历史专项。



T026绑定清理落位：RecipeBindingSaveProtectionTests的5项真实SQLite保护已通过（binding-protection-02，01中断保留）。据此删除当前IPlcRecipePort、真实/模拟BindRecipeAsync、容量ACK轮询/占用、UnavailablePlcRecipePort及BA03/BA05专用故障和释放HTTP入口；当前软件绑定不派PLC配方ID/容量。历史RecipeApplication*原payload类型移至HistoricalRecipeApplicationEvidence.cs仅用于原记录读取，当前Receipt仍RecipeBindingReceipt。旧通信容量握手7项及Host健康ACK等待2项失去动作对象而删除，有效期限/保存/取消义务由上述5项、现行通信取消/未知及取料实存门承接；原报告/数据库证据不删。集成夹具的绑定计数改读本独立运行库实际RecipePlanBound写入，不以模拟设备计数冒充软件绑定。012指定源文件不修改。



### Test机械旁路删除实施承接（T026）



010/009已交真实软件绑定、普通配置执行/Flip＋PutBack、三区分拣及未知/保存保护后，删除当前AuxiliaryHandling端口、TestSpecial HTTP DTO/实现/VirtualPlc路由与监控专用消费者；旧JSON历史投影保留。VirtualPlc监控文件VirtualPlc/wwwroot/app.js由011维护，仅删除失效数据源。旧HTTP证据类型仍供通信历史读取，当前TCP动作不伪造HTTP交换。



Station01RuntimeOptions和PlcRuntimeOptions删除TestSpecialActionsBaseUri。Host Program.cs实参由012唯一移除，011已提前在tasks-handoff交准确要求；未接收前不构建Host/Integration，不保无用途参数或排除源码绕过依赖。ReadInitialStateAsync不再请求Test HTTP；正式恢复机械/安全输入仍未配置时返回Blocked并明确RecoveryProtocolNotConfigured，不推断Ready，不清除未知占用。旧Reset/人工翻面协议的剩余迁移另由T009/T026核有效消费者，本批不声明已通过。



旧AuxiliaryEvidenceTests的HTTP动作正例及VirtualPlcMonitor的TestSpecial reset用例已无当前对象：实际TCP翻放关联/未知保护由ActionHandshakeTests.Flip现行6项承接；真实原始交换保存继续通信组件，新增1项恢复缺输入拒绝；旧报告不删除。旧Rotation Test机械链及其驱动在普通更多面/E组件承接后删除，缺正式旋转映射仍明确拒绝，不能换成备用HTTP。



人工翻面死分支核查（T026）：全源码/装配/脚本无BeginManualFlipAsync、CompleteManualFlipAsync及WaitingForManualOccupancy消费者。删除这两个私有旧机械方法、其永不进入的hold标志/采样等待/安全豁免分支；实际ManualZoneOccupied、故障、自动模式和取消/未知准入继续阻断，不把现场安全信号改成默认安全。历史ManualHandling枚举及原记录继续可读。仅复核受影响TCP翻放/未知/启动组件，不重跑历史专项。



T025配置入口落位：Station01RuntimeOptions末尾追加可空PlcMechanicsPath，仅是通信配置文件路径。012在唯一Program入口用具名实参PlcMechanicsPath: section["PlcMechanicsPath"]传入；不把型号载荷或原码放入业务DTO。011 Station01Registration仅经设备构造转交路径；LatestProtocolPlcDevice在通信层调用PlcMechanicalConfiguration.Apply读取一次plc-mechanics/1（purpose/sourceReference/posePrograms/sortingSafePosition）；同连接生命周期固定，不动态切配置。不提供时保空映射/空安全位，对应动作按现有明确缺输入拒绝，不回退第二文件。显式Test配置只供Virtual；Production仍须已确认来源及正式协议定义，不由本文件批准现场值。源码/普通文件解析组件可先交；012 T020001入口已接收，代表链验证仍待必要清理及012页面就绪。



T026低层死分支定向清理：VirtualPlc原合轴Move、采集ZReset已无PendingAction生产调用，删除其完成/失败/重试/审计分支和无调用MoveZSignal、ResetMoveFeedback；保实际独立Axes、Flip/PutBack/Sort、安全/心跳与实际复位代次。旧StartTrace只服务已替代的PC_Start/夹紧断言，一并删除该虚拟追踪及失效消费者；有效连接/旧反馈/保存/取消保护由现行ProtocolStartup、IndependentAxis、Flip关联及Pick保存组件承接。此批不声称所有旧控制/地址已清完，区域/夹紧/旧复位握手实际消费者继续逐项迁移，不猜新恢复。



T009/M02独立预期定向迁移：旧AllConfirmedFields全旧表/旧分拣码断言由CurrentAxesAndMechanicalFeedbackMatchIndependentLiteralOracle替代，按既有人工confirmed-011验证五轴方向/类型/地址/码及翻放/分拣码；20260925原件保历史。Float32四字序和访问间隔/非法长度保护保留，间隔夹具改200/210、Test容量256以避新模型载荷而非放宽访问门。当前仅这3方法加受影响IndependentAxis(2)/ProtocolStartup(2)/Flip(6)及有效连接/心跳/陈旧安全(3)，预计16项；不运行旧009全专项。结果待实际记录。



当前M02失败与修复承接：current-protocol-01实际发现16、15通过、1失败、0Skip；FreshFlipAndPutBack在放回XY收到AxisActualPositionMismatch。定位DriveAxes原ReadAsync按地址拆请求，低址实际位置先于高址到位读取，可拼成跨完成时刻样本。改为当前动作已见Moving→Arrived后另读实际位置，再核原期限/连接/安全和原容差，仍错位立即失败，不放宽断言或重试掩盖。复核此翻放、独立轴及取料保存相关组件；历史分拣瞬时失败不据此直接宣称同根因已证。



T015/T026现行运动状态消费者修正：通信Sample仍从旧单Z状态推导MotionStatus，虽业务动作由pending/auxiliary占用，但独立PLC轴活动可能被查询为Available。迁移为五轴实际触发＋各自反馈解码；当前移动输出InUse，未知码/超时保持HeldUnknown，未触发初始0不伪称运动。保连接/安全/当前动作保护；只改通信投影不改DTO、原码不泄漏。扩展既有IndependentAxis两项中的真实TCP观察断言，不新跑完整链或追加组合。





多面01实证修正（T010/T019/T024）：第二面受理2秒期限先到，后台整表轮询尚未让出动作推进；旧MoveAndBegin失败后只标租约Unknown，未取消设备请求，导致期限后仍下发轴触发。保持原期限，运动请求在准入后由同一串行动作任务及时推进，不等待下一整表轮询；仍等上一动作结束且核当前身份/代次/安全，不新增并行动作或回执假成功。每次业务移动持独立链接取消令牌，超时/取消立即取消未完成请求并保持Unknown。共同DetectionResultKind增UnknownHeld（当前执行合同修订，recipe-contract/1.3不变），失去运动确认以此返回并持久化UnknownHeld，不得显示可重试或转Pending分拣；未派发/纯算法失败按原语义。



T010启动受理实证修正：multi02在012真实ready后已创建run=f235db29-9c67-495f-a740-f033df123fa3，StartAcceptanceUnknownHeld，无F/Final，Host正常退出0。启动旧AdvanceStart仍等两次整表轮询，未沿023同串行任务即时推进。启动也在同一actionAdvance内发送就绪并等同代次、新采样起点的实际Ready/安全观察及证据真实提交；保持2秒受理/取消门。常规轮询只读Sample/AxisMotion实际消费的观察信号，不再无意义轮询PC配置/载荷/命令全表；不减少安全/心跳/当前反馈或扩大读取范围。必要ProtocolStartup原2项将实际请求窗口限定2秒；不重跑整套协议。下一链预留multi03，先代码/组件稳定及012新准备再启动。


T009/T026失效ACK/区域字清理：当前翻转/放回和分拣已有各自动作事实及提交门，旧Sorting_OK/Flip_OK/Retry_Cmd、区域数量/Ready/Ack及PLC整数Recipe_ID不再消费，删除其活动Test映射、码表和无用复位/UI描述。SignalId保其余成员既有数值，避免改变保留成员身份。ReadInitialState仍因RecoveryProtocolNotConfigured明确阻断，不把去掉旧ACK检查当已实现恢复。共同RecipeId/PLC型号/实际配方容量不受此通信字段删除影响；正式地址/ASCII仍待输入。旧全表规格断言已由confirmed-011独立预期替代，只保其中有效宽度/所有权和访问计划保护，不重跑009全套。

T009/T026采集复位失效消费者定向迁移：新协议采集结束不发送Inspection_Status/Z_Reset ACK。旧5行Capture正负例迁移为实际独立轴/用途、错误动作身份拒绝、未提交或未释放输入拒绝、真实TCP断开后HeldUnknown；有限窗口和SQLite分段通信证据/轮询覆盖仍保留。删除VirtualPlc无生产者的ZResetFailure/ZResetFeedbackHold故障；现有DetectionCommunicationFixture的释放故障在真实关闭时断开其自有TCP服务，不伪造释放结果。旧009脚本F04-reset在任何进程启动前明确拒绝已替代场景，原历史证据及清单不改。仍只运行受影响采集组件，不重跑009历史流程。

T009/T026当前监视字段最终迁移：旧PC_Start、Manual_Flip_Complete、合轴命令/反馈、单Z状态、Flip_Current_Face、Sorting_Part_Index、Pallet锁命令/状态及采集/复位ACK已无当前动作生产者，活动Test定义、码表、轮询私有状态、虚拟PLC处理/审计/UI中实际删除。有效独立五轴、Flip/PutBack/Sort、真实就绪/安全/心跳不变；恢复检查继续明确RecoveryProtocolNotConfigured，不补机械/安全事实。历史载荷和原证据不改。两种有限TCP响应故障改对实际X轴触发，不能再识别已删除的“Unload原码”；旧无生产者的UnloadMissing/Previous注入和无效原码断言删前由现行反馈/UnknownHeld/无重放保护承接。必要旧测试按新语义迁移，首次构建全部消费者齐备；不以保错字段、排除编译或历史全量复跑解决。

## 013实施前定向同步（2026-10-04）

SY-05/06：FR-001/013/016—023、PC02—05及执行状态沿013 A01—08/D02—07：两连接、单源用途采集；H300、B200/500、动作200、位置500/1000ms，首Moving/Executing局部50ms齐备后恢复200；原期限/取消/未知及中间态→完成→到位后实坐标因果保持。各块真实身份，基础与位置独立可靠性；最终位置发布后才返回完成，关键准入/采集释放即时核查。Host位置增加SampleStartedUtc、SampleEndedUtc、ConnectionEpoch、Reliability并升live/1.2，Domain和持久原格式不变。预算schema2.0/代表预算2/模拟2（模拟schema1.0）及Start/Host/fixture引用先迁移。实际取料、原始证据与有效在途提交后才Place不弱化。正常HeldFlip原5秒失败保存不强求未达1024的段；自动阈值真提交接续另用通信证据组件，原缺口拒绝独立保持。013每侧一次同run-2+空闲及必要组件/完整L/受影响009；真实只读API观察器只替013后端负载前置，011/012页面证据义务与历史报告不改。原T009/T010正式PLC缺口仍局部限制，历史任务勾选不变。

本节为本轮授权的现行条款对齐，软件完成由[013任务](../../013-plc-polling-optimization/tasks.md)及实际证据判定；不改历史完成/失败记录，不表示已运行通过。

## PC06 014抓手与R轴（Phase 1语义已设计，正式地址未交）

Grab_ID/Grab_Active_ID只区分1/2，不绑定用途；上料和分拣各闭环开始消费各自配方选择，有效同号复用不重复发送/等待新确认。换号/首次/初始化/PLC重启/失效须本次有效匹配确认，历史同号不授权；闭环中不重发或核ID，正常结束保有效选择。初始化/系统复位清零选择，清零不松手，持料状态分开；翻面/翻面放料不引入抓手选择握手。Rotate_Target_R为绝对目标，使用本次R_Pos_Confirmed和Machine_Current_Pos_R核本次到位及实际角度，不新增完成信号。公共Sorting_Cmd/Sorting_Exec_Status用于上料/分拣闭环，取料真实保存门仍先于后续搬运/放料。

上述原始名字仅在通信合同，不进入业务模型/公开DTO。地址空项/布局/编码未确认不填0；准确映射/访问计划和真实消费者须后续设计。复用013现有单源/固定计划/按需采样，不能另起抓手/R高频读；预算/期限/取消/代次不放宽。软件模拟和正式互通分列。旧旋转MappingUnavailable和删除的HTTP专用链仅为基线事实，不能作为新闭环实现或恢复旁路。

本次特殊OK原槽回放仍属检测后分拣闭环，消费分拣抓手配置而非上料配置。目标为原槽不跳过有效同号复用/换号或失效重建规则，不另设“回原槽”通信旁路，实际取料提交门/转运/放料/安全位反馈保持。


## 2026-10-05当前Phase 1消费

共同字段/序列化唯一定义见011 recipe-contract RC10（设计1.5、正文4/冻结3；实际代码仍1.4）。执行增量见014 contracts/execution.md EX14-01—05，012界面/HTTP见layout-design与recipe-authoring-api；均为本会话统一设计，无第二模型/校验/身份/执行器。本轮不代码/构建/测试、不新增tasks；后续代码前须准确任务/消费者/注册扫描承接，不能称待同步已完成。旧source、任务勾选、历史验证和013单源降频/性能偏差保持。


### PC14-C 现通信适配与采样接入

014 EX14-03/04给业务端口变化；原件字段类型仅本通信边界承接：抓手选择/有效反馈Int16，R目标/实际Float32，旋转启动Bool，本次R到位Int16，精确字段名/值按new-1原件，正式地址均未交。复用LatestProtocolPlcDevice当前epoch的有效选择缓存与实际反馈；循环内不重选，safe/pick证据来自当前动作而非目标回显。

新字段只进入同SignalAccessor/ProtocolDefinition与Pump有限订阅/WaitGroup，沿现013 policy，不加第二读源。PreparedPlcReadPlans现键0..63不足，本次设计将内部FieldMask/Key/Dictionary准确扩UInt128，保原ID/RequireOwned和有限订阅，直接消费者纳既有009扫描/负例；不能溢出、扩大豁免或把缺地址作0。选择失效与持料状态分开，翻面不携抓手选择。正式安全/取料角/容差缺项只阻对应动作，不能以软件模拟批准生产。
