# SC-021：启动关联、未知查询及最终完成合同

2026-10-08；追溯FR-007–011/013/014/016，SC-003–005。新增读接口与正常释放属于本前端主流程必需的后端配套，不修改PLC协议、安全门或旧故障恢复合同。

## SC-01 准备与提交

宿主提供有版本/来源的CommissioningStartTemplate；字段见[data-model](../data-model.md)。模板仅为业务准备资料，不是安全许可。真实设备端点、算法安全值、型号REAL/机械映射等仍由020正式Host配置消费；页面不填充。

每次合法显式新启动生成新requestId及逻辑trayId，冻结模板和当前选择的expectedRecipeRef，提交现有`POST /api/v1/station01/runs`，请求形状仍为requestId/contextJson/publicConfigRef/budgetRef/simulationRef。context沿既有2.0及purpose=Commissioning；F码与预期配方错配仍由后端拒绝。Test分支保留Test语义，不删除用途校验。

发送前保存PendingOperationReference。202只表示已受理，保存其commandId/runId并GET状态；有明确runCreated=false的拒绝记RejectedNoRun。传输失败、响应丢失/无法确认均AcceptanceUnknown，不自动重发POST，不以新requestId绕过旧未知状态。通知/页面重开只查询。

## SC-02 原请求只读查询

新增`GET /api/v1/station01/start-requests/{requestId}`，要求Run.Read。使用后台确认的SubjectId与requestId查询现CommandRegistry及SQLite Commands（Kind=Start、Scope=Station01），不能由query指定别的SubjectId。返回schemaVersion=`station01-start-request/1`、requestId、commandId、runId、receiptDurability、statusUrl、accepted；durability来自实际Pending/Committed事实，不用日志推断。

活跃注册表可返回Pending回执，重启后已提交命令由现持久读取恢复/查询。没有可证明记录返回404 StartRequestNotObserved，语义为“当前无法确认”，不构成未执行或重发许可。数据库不可用返回可定位的查询受限，不落到404。命中后页查对应Run；不得仅从status.currentRun捡一个运行冒作原请求。

接口不调用Replay或Start，不需要请求正文，不引发动作或释放。若维护切换身份，原Subject的未知关联保持受限；新身份不能把原请求接成自身新请求。已知Run可按现权限查询其事实，原请求查询由原身份或已存在授权核查途径完成，不新增跨主体恢复API。

内部最小扩展：CommandRegistry增加只读按主体/请求取回执能力；ITraceQuery/TraceQuery增加按主体/请求查询已保存Start命令，沿原查询预算及唯一约束核验；无新表/迁移，发现多条不一致记录明确报错，不能任取一条。

## SC-03 人工确认和普通下一轮

继续现`POST /runs/{runId}/manual-removal-confirmations`的requestId/expectedRevision/reason与Run.Start权限。仅后台允许的人工取盘准入成立时提交；页面一次显式确认冻结ID，未知/重开后不换ID再次确认，先查询原Run最终事实。未提交成功不显示已完成。

业务层在同Run/Tray的人工确认及FinalUnloadCompleted/最终来源矩阵真实提交后，调用正常完成准入释放；验证依据为IWholeTrayCompletionStore/StageEventStore及保存的Run终态，不能接收页面“完成=true”。释放在CommandRegistry原锁内只清该Run的_physicalOwner，重复处理幂等，若已为另一Run则保留，不清_faultRestartOwner。

新增只读`GET /api/v1/station01/start-admission`（Run.Read），返回schemaVersion=`station01-start-admission/1`及NextRunAdmission。Available仅为普通软件占用可接下一请求，POST仍重核全部条件；这不是运动授权，GET也不执行释放。Held/Unknown附当前占用及原因。

完成正常释放放在共同业务完成路径，不能只靠页面刷新；人工确认返回已提交Final的重放路径也可经同一幂等业务核对，使“Final已保存但响应/内存更新中断”得到一致状态。启动恢复沿现unfinished/Command恢复核对持久终态；缺Final证明不增加恢复许可。

页面只在GET确认Final事实与Available后解除普通本轮启动锁，保留历史Run用于查询；下次仍需人员显式点击才生成新意图。检测完成、AwaitingManualRemoval、Cancelled/Blocked/RecoveryRequired、页面关闭/重连都不适用普通释放。故障原reset/check/restartFrom链保留，不增加自动复位或续发。

## SC-04 验证边界

正常两轮使用已获批旧Test布局的离线正式Host链，核最终保存先于释放及下一新请求，不能把它标成现场布局通过。新用途真实装配以缺输入/安全未明失败闭环验证；正式A/B参数消费用保存后的实际正文及正式执行器声明端口留证。现场正常两轮依旧由020 T056承接。V06七格映射另用专属Commissioning离线媒体组件路线，遵循RM-02；不得复用旧Test Run或修改用途充作新映射证据。后台身份/接口前置与V01实际桌面验收分开，桌面缺环境不阻断独立软件工作，完整验收要求不降。

## 用户确认增量：配方中的虚拟F定位（2026-10-08）

用户确认示教坐标人工填配方，本次单品翻面样件的虚拟质量结果为OK；虚拟算法必须实际消费本Run媒体后记录调用成功，不绕过采集/保存或设备动作。用户将提供F读码XY，并要求加到虚拟算法联调用的配方设置。新增FR-019/SC-009：共同配方可选commissioningFPosition={schemaVersion:"commissioning-f-position/1",x,y}，页面在既有配方弹窗基础信息增加“虚拟算法 F读码X (mm)”及Y，由人员手填，不补默认坐标。后端校验有限值，启动再按公共运动配置核单位/坐标系/行程；所选配方版本与目录一致后从实际保存正文读取，构成带配方版本来源的FLocation，冻结给本Run虚拟3D的首次定位结果，实际F读码后仍再次核绑定。真实算法不消费该字段。旧记录保留原声明的受控输入策略；新建联调配方必须显式填写F位置才能通过相关运动准入，不能偷偷沿用旧示例F位置。

准确字段及端口：RecipeDefinition.CommissioningFPosition随正文/摘要保存；ICommissioningRunInputs.FreezeRun(..., selection=null, fLocation=null)增加可选冻结定位输入。StartPublicPreparation从本次expectedRecipeRef匹配的真实目录取得该位置，校验引用后转换FLocation，不从前端POST直接接收运动值。运行开始后修改配方不改变已冻结公共/虚拟输入。已有公共F固定位置及真实3D定位职责不改；新增位置仅适用本联调虚拟算法。FR-009/原“不在页面设置算法坐标”对此用户明确授权的F XY作唯一例外，其余安全值仍不允许默认补齐。

普通完成释放由IWholeTrayCompletionStore.ReconcileFinalAsync(runId,trayId,ct)共同路径核同盘Final事件、人工事件、FinalSourceMatrix及Run终态后执行，Confirm提交/重放和API确认重放均调用；GET不调用。CommandRegistry仅按同owner释放且不清故障owner。ITraceQuery.GetStartReceiptAsync(subject,requestId,ct)只读既有Commands。


2026-10-08 定向修复：旧Test正常两轮要求沿SC-04执行。SimulatedCapture的F单拍限制必须按Envelope.RunId区分，不是进程生命周期共一次；同Run再次申请仍拒绝，无自动重拍。TriggerCount仍为累计已受理触发数。此变更只修正离线采集实现，不改变真实相机、业务端口、协议或现场安全准入。


2026-10-08 两轮持久事件缺陷修复：ThreeStageWorkflowExecutor生成的StageEventAppendRequest.IdempotencyKey须有稳定runId/trayId命名空间，再保留阶段/尝试/对象原键。同一运行的相同键仍按现StageEventStore回放/冲突语义处理，不同Run不能共享detection:1:intent等字面键。不改表/端口形状、不迁移历史记录。新部署使用独立运行库，不用新版本重放旧版本在途或结果未知的动作。

## 2026-10-08 已确认启动/复位/停住规则（SC-021-PLC-R2）

用户确认采用所建议MB2007清零时点，并确认MB2008软停及心跳断线均停在当前位置、不自动回位；此规则取代旧说明的软停自动回位。MB2009复位按PC低态确认→置1→观察本连接本次MB6015先0再1→检查新鲜XYZ零位→清MB2009；不得立即脉冲清零、只读旧1或失败重发。MB2007仅在现场布局启动时确认原值0后置1，第一组实际下发运动完成、坐标验证及双方清零后清0；纯同坐标沿用不触发清零。未知/失败不得发下一轮启动；不在失败清理时伪造首次动作完成。现场报警及独立安全点均沿既有只读检查，未知报警非零阻断；安全门/光栅恢复不得自动重发未知动作。

新增通信配置plc-site-operations/1，带非空确认来源，经plc-mechanics/1的siteOperations读取，只适用于现场已确认布局。无此配置继续Unconfirmed，不凭单一布尔标志放行；确认配置采用本次用户约定的Ready语义、XYZ零位、停住规则及首次动作后清启动。零位使用已配置PositionTolerance核对，不填任意容差，不发自动回零运动。完整恢复持料/夹爪核定保持人工门，ReadInitialState不能因上述规则自动声明全部恢复成功。

新增SignalId.PcStartCmd仅绑定现场PC.xls MB2007（BoolByte，PC写/清），不添加旧Test地址、不改变前端API。受影响消费者：协议定义/现场映射、PLC机械配置、Sample安全解释、ResetAsync、AdvanceStart、轴完成清零；旧Test流程和清零/同坐标/未知不重发不变。补丁按final-2清单验证基包，生成独立版本目录并带SHA256、源码增量及验证记录；不覆盖旧包/安装，不连接硬件、不提交推送，T055/T056不关闭。

### 配置接入方式

将config/site-operations-confirmed-20261008.json正文作为正式plc-mechanics/1中的siteOperations字段，与原posePrograms、positionBasis等并列。该文件是已确认语义来源，不是完整机械配置；不要把它直接当作PlcMechanicsPath文件。config/field-limits-confirmed-20261008.json是已确认范围来源，装配公共配置时采用它；零位检查使用正式已配置容差，不新增猜测值。旧field-limits-source-pending-review.json仅为历史来源，不代表最新确认仍未完成。缺完整运行配置时默认CheckOnly仍拒绝，不启设备。补丁不含任何本机私有身份凭据、旧运行库或自动恢复动作。


## 2026-10-08 现场软停恢复顺序修正（SC-021-PLC-R3）

依据用户现场确认：软停使PLC进入类似急停状态；必须先由PC就绪并取消软停，PLC才能执行复位。显式复位先确认MB2009原值为0，再写MB2006=1、MB2008=0，并用新读取值确认两项均成立，之后才产生MB2009上升沿。写成功或固定延时不能替代读回。内部业务仍处于Resetting，不能因此下发启动/轴运动或续接旧动作。本次MB6015先0再1、安全和XYZ零位核验及最终清请求要求继续保留。旧MB2009仍为1时拒绝再次产生复位，不擅自清除后重发。

PLC复位成功不等于旧Run恢复成功。当前Commissioning尚无覆盖重启恢复Run的完整恢复服务；ReadInitialState中的恢复协议/人工区核验不得改成假通过。旧任务、持久记录及Held保持。页面在既有故障通知区域明确显示“旧任务待恢复核验，PLC复位不等于放行，重启或重复启动无效”，并区分可用的既有恢复操作与尚缺恢复入口。恢复服务/权限/持久核验/新轮关联另列T056，不沿用旧Test内存恢复对象冒充重启恢复。需要明确复位后夹爪是否持料、零件归位及翻转状态，未确定前仅阻断相应恢复放行，不阻断本次复位修正。


用户补充：没有下发过软停也必须能够复位；MB2008已为0时保持0即可，禁止为复位先置1。T054正常闭环分别验证初始软停为0和1，均只产生一次复位上升沿、零启动/轴运动。当前软停触发点核对：启动准备失败、动作/采集/阶段结果未知及显式RequestStop会请求软停；普通连接、正常闭环完成和显式复位不会主动先置软停1。保留未知动作的停止保护，不因本次修正取消。现场报警消失不自动复位或续跑。


## 2026-10-08 全部复位后的旧任务结束与新轮（SC-021-PLC-R4）

用户确认PLC系统复位完成后会全部恢复：零件放回、夹爪松开、翻转机构恢复初始状态。siteOperations新增可选restoresWorkpieceAndMechanisms=true及确认来源，默认false；只有本次已观察Ready 0→1、当前安全和XYZ零位满足、PC请求及相关PLC旧反馈清零时才能形成恢复核验。无此确认不把Ready单点扩大成恢复通过；旧Test语义不改变。

Commissioning正式POST /reset在原Run.Start权限下串行执行维护：阻断新启动，要求旧任务处于Blocked/RecoveryRequired/Restricted/Cancelled且执行退出、相机/算法/媒体资源释放；先持久保存复位意图，再完成PLC复位和新鲜初始状态核验，最后使用原Writer条件版本将旧Run保存为Cancelled并记录CommissioningRecoveryClosed（resetId、用户、真实观察、来源、旧Run身份）。这不生成检测成功或Final完成事实。必要保存未确认时保持占用；已提交后释放对应Motion/Command占用，返回recoveryClosed=true、manualStartRequired=true。无旧任务也支持复位，不人为要求先软停。旧MB2009=1仍拒绝重发未知复位，不把旧Ready=1认作本次复位完成。

GET Run新增commissioningRecovery投影，仅依据已提交取消终态和恢复审计；页面沿既有故障/人工操作区域提供“复位并结束旧任务”，调用同一正式/reset入口。成功后明确旧任务已结束，启动控件只在GET确认恢复证明及Available时可创建新的requestId/Run；不自动启动、不续接旧步骤，不复用旧冻结配置。StartPublicRequest可选commissioningRestartFrom={runId,recoveryWriteId}，后端验证所引用取消及恢复提交事实，并保存在新Run原始启动上下文中；普通启动和旧Test故障restartFrom合同不变。Host重启读取Cancelled持久终态，不再次恢复或自动重发旧Run；未完成恢复仍Held。

用户要求更新现有final-4目录及同名ZIP，不创建新部署包编号。更新前备份原manifest/ZIP摘要及改动文件，保留历史验证与回退；重新发布受影响Host/Prep/Worker等消费程序集，重新冻结源码和清单并校验同名ZIP，不触发真实复位/运动或替换运行中安装。

R4定向回归修复：清零确认首次可请求立即新读；未清零后沿用已启用反馈组的正式采样周期，始终核对本次清请求之后的新鲜读数及原动作期限。不连续强制即时读造成通信证据环覆盖，不把采样间隔当作清零确认。

## 2026-10-09 复位诊断增量

不改变/reset请求/响应、授权、PLC端口、清零顺序及持久取消条件。CommissioningRecovery的Blocked日志增加phase、elapsedMs、requestCancellationObserved、resetBudgetExpired及cancellationObservation；预算到期与外部请求取消用独立CancellationTokenSource观察，两者同时发生则同时记录，均未观察到时为Unclassified。仍使用原30秒配置预算起点和原调用取消传播，不转为后台自动恢复。

PlcSiteHandshake记录ResetRequestObserved、ResetReadyObserved、ResetVerificationObserved、ResetRequestClearWriteResponded等低频事实；handshakeDiagnostic用于失败窗口保留最后阶段。异常前未读取的数据不填默认成功，清写响应不冒充读回清零证明。resetId与PLC事件仍通过现有运行/epoch/时间核对，本次不声称端到端唯一ID已贯通。


## 2026-10-09 已确认现场复位保留到位规则（SC-021-PLC-R6）

用户确认系统复位后轴到位反馈允许保持1；需要运动时PLC先反馈运动中再到位；相同坐标时保持1。MB6052放回反馈必须清0，PLC侧已修改，现场修改尚未由本轮验证。复位恢复允许手动模式，自动模式仅用于新任务/运动准入。复位仍须本次新鲜完成反馈、PC请求清除、安全互锁、全部直线轴安全零位、非轴闭环反馈清零及SQLite旧Run持久取消。

已核验本连接系统复位可形成受限轴初始证明：新鲜请求为0、反馈为0或已到位、实际位置在安全零位容差内。仅凭开机/重连坐标或旧缓存不成立。该证明允许首轮从保留到位发令，但本次必须观察发令后的运动中再到位；同坐标在新读坐标/请求/反馈一致时不发运动。轴被触发、位置变化、故障或连接代次失效即撤销。普通动作完成后双方清零机制不变，放回/翻转不套用轴到位例外。旋转保留到位只允许本次复位后的首次请求，仍检查本次运动中/到位及实际角度，普通闭环仍清零。

软件补丁不自动启动设备或清理旧未知请求；现场T055/T056验收继续保留。


## 2026-10-09 相机故障与复位资源检查分离（SC-021-CAMERA-R7）

本次诊断确认3D初始化在SDK Open之前因userIP=127.0.0.1被拒绝，worker退出及管道释放后仍显示Faulted，旧资源谓词只允许Ready/Stopped而阻止PLC复位。保留故障状态和新任务/真实采集七台Ready门禁，恢复门禁改用真实资源证据：无在途操作，Ready空闲或进程/管道退出且参数无需恢复/正常恢复已确认；其他未知故障继续阻断。

CameraWireMessage v2增加可选deviceOpenAttempted事实，缺失视为未知。正式driver在首次SDK Open调用前置true，初始化前失败才可报告false；Host仅在同session/request的初始化错误中采信，并在实际子进程/管道退出后形成无占用证明，不能仅凭Faulted放行。共享PersistentCameraGateway增加只读恢复资源投影；恢复日志记录算法、媒体计数和各相机证据，不改PLC握手。

CameraPro发现回环userIP时，允许从现有ExpectedNicMac唯一匹配Up物理网卡、同网段IPv4及Windows到相机的最佳路由接口，形成明确派生的本机地址。三者必须唯一且一致，SDK正常返回的地址不自动替换；记录原始发现、推导依据及SDK打开使用值，不硬编码IP、不修改现场网卡/相机地址。使用厂商现有CameraInfo.userIP setter填入核定本机地址，再执行原序列号/物理网卡校验与真实SDK Open；任一依据缺失即拒绝。没有伪造Ready或帧。

验证仅离线：已知Open前失败且资源退出可恢复、未知初始化/采集故障不放行、在途采集不放行；回环地址的唯一绑定解析和错网卡/错路由/重复候选拒绝。保留020 T055/T056现场阻断。本轮不自动启动设备、复位或拍照。


## SC-021-UI-R8 复位后新轮状态绑定修正（2026-10-09）

现场证据：旧Run持久Cancelled且恢复证明ClosedAfterVerifiedReset、启动准入Available，运行查询同时返回数字state=24及规范字符串executionState=Cancelled；前端只比较state字符串，误挡显式启动，未发送启动请求。
前端恢复提示和恢复后新轮判断使用既有executionState；旧字符串state仅沿用现有页面的备用读取方式，缺少可靠状态不得放行。仍必须核对恢复写入证明及后端Available，重新制作独立requestId/trayId并携带commissioningRestartFrom，不复用旧动作、不自动启动、不清库。API、PLC信号和页面原型不变。
定向验证使用现场查询同形JSON（数字state及字符串executionState），覆盖成功收尾、新轮准备和缺少证明/准入阻断；检查同一helper中的恢复提示状态读取。原地更新受影响前端产物并核对构建摘要，保留数据库/身份/配方/设备配置。真机完整流程继续待现场验收。


## SC-021-PLC-R9 受控复位前撤销PC残留动作请求（2026-10-09）

用户授权修正PC残留动作清除。旧流程/采集/算法/媒体退出与恢复权限检查保持不变；仍先检查旧MB2009，结果未知请求为1时不清除、不重发，也不清其他请求。旧MB2009为0才进入本次受控复位：撤销PC旋转MB2000、X/Y/检测Z/扫码Z/抓取Z请求MB2001–2005、启动MB2007、翻转/放回命令MB2014、分拣命令MB2016；各类型依现有效XLS映射使用BoolByte/Int16，清除值均为已采用的空闲0。不清目标坐标/配方/PLC反馈/报警/夹爪状态/心跳。

撤销发生在置PC就绪、取消软停以及发送系统复位MB2009之前；读回本次新鲜请求值全0且连接代次有效后才继续。写返回不算读回确认，任一失败/读回非0/断线/取消/超时即阻断，不发MB2009，不自动重试或启动。日志记录清理前、逐项清理、读回值/时间证据、失败阶段及耗时。原系统复位完成、安全零位、残留反馈、SQLite旧任务收尾门禁不变。普通动作清零规则不变，不用本补丁绕过此前MB6040未清问题。

最小回归：残留PC请求在复位沿前均清零并新读确认；PLC确认写入但保留请求时零复位派发；已有未知MB2009时不写清任何请求；复位后真实SQLite旧任务收尾与普通轴清零/同坐标回归。测试只用隔离离线TCP夹具，不连接硬件。发布各受影响程序集副本与PDB按四入口正式发布比对原地更新，现场资料保留，T055/T056仍待现场。


## SC-021-PLC-R10 轴到位状态保留与PC请求撤销（2026-10-09）

用户纠正并授权修改：现场轴反馈0=运动中、1=已到位、2=超时；不能要求普通动作完成后将到位1清0。此规则取代R6/R9及此前所有“现场轴正常闭环双方归零”的表述，仅针对现场五个直线轴及同含义的旋转轴，旧Test协议和翻转/放回/分拣独立握手不因此改变。
现场轴闭环：本次请求后已观察运动中→到位，且新鲜实际位置满足目标容差，再撤销PC请求并以写后新鲜读回确认请求0；PLC到位1保留。最终新读状态/坐标/请求须一致，不把运动中0或旧缓存当作正常到位。后续异坐标请求需本连接已验证复位/上次动作的轴位置证明，再重新观察本次0→1；同坐标沿用本连接证明与新鲜请求/到位/坐标核验，不发多余运动。重连、故障、坐标变化和异常反馈使证明失效，不自动重发未知动作。
旋转轴同样撤销PC请求、保留到位1并核对角度；只有本连接已验证复位或已完成旋转证明才能沿用保留的到位基线。复位前R9撤销PC残留动作请求仍有效，不清PLC反馈。
定向验证：首次XY到位保留、连续两轮异坐标、Y/检测Z同坐标、同目标全复用；反馈未观察本次运动中时不假完成；PC请求不清、过期/断线反馈仍阻断。有限回归旧Test闭环、复位/SQLite恢复及旋转。现场完整流程不以离线夹具代替。
同时审计当前安装与R9发布基准、有效源码/脚本，保留并同步已授权本地R6–R10改动；现场配置、身份、数据库、配方仅保留，不当作通用默认值拷回仓库。无提交推送。


## SC-021-PLC-R11 用户明确轴语义收敛（2026-10-09）
本节取代R6/R10冲突规则：直线轴及R轴反馈0运动中/1到位/2超时。请求释放确认仅检查PC请求写后新读0；完整周期空闲确认仍检查翻面/放回/分拣请求及其反馈0。普通轴最终坐标、到位1、连接和安全均须核验。运动前新读请求0、到位1与实际坐标；0不可解释为停稳。异坐标发送后必须观察本次0→1和坐标，不用旧1完成。
同坐标只允许本连接正常轴动作完整完成并请求释放的有效记录，加本次新读坐标/到位1/安全。系统复位清除旧记录，不建立动作完成或同坐标沿用记录。无记录同坐标阻断，不伪造运动变化。R轴只修复反馈语义和角度核验，不提供同角度沿用。
系统复位MB2009写应答后新读MB6015=1即清MB2009，不要求先0；后续安全、初始位置、轴到位1、MB6052=0和旧任务收尾仍有效；不强清未知复位请求。虚拟下位机轴完成/复位保留到位1，撤请求不改反馈，同坐标不伪造0→1；相应旧Test轴测试假设同步纠正，翻面/分拣完整周期零空闲协议不变。
实施范围：Axes/Handshakes/Stages/主设备/Semantics/SiteOperations/VirtualPlcEngine.Axes及直接相关测试；区分AxisMotionCompleted、PC请求释放与后续核验的持久关联日志。保留已部署R6-R10其他必要修复、现场数据和协议来源；按正式发布构建原地校验更新，提交到现场origin的新同步分支，不强推。


## SC-021-PLC-R12 复位/启动零位容差独立配置（2026-10-09）
用户确认复位/启动零位允许±0.2毫米，正常运动到位精度保持不变。现场证据：MB6015完成新读1后Y=0.15085936，原零位容差0.1拒绝；随后只读Y已为0。此修改不移除零位门禁、不固定延时、不自动重发复位。
在既有通信机械配置SiteOperations中增加可选safeZeroToleranceMm，必须有限且>0，来源由原sourceReference记录；旧配置未提供时沿用原PositionTolerance。用于RequireSafeZero（复位完成后及启动前XYZ）和ReadInitialState的全部直线轴安全零位检查。普通配方运动坐标到位、同坐标沿用/失效、采集窗口坐标核验仍使用原PositionTolerance；R角度容差不变。现场配置最小合并safeZeroToleranceMm=0.2，不更改原PositionTolerance=0.1，不覆盖其他机械数据。
定向验证0.15086及边界0.2通过，超0.2阻断、普通到位容差不变；回归既有复位握手/恢复检查。保持PC请求读回、轴到位1、MB6052=0及未知复位禁止强清规则。正式构建后原地更新、摘要/备份/回退；不发真实复位/运动，不提交或推送（按用户最新要求先部署通知）。


## SC-021-PLC-R13 复位完成与初始检查失败区分（2026-10-09）
现场：2026-10-09 07:19:26发送复位，07:19:28新读MB6015=1并撤MB2009，安全/XYZ±0.2通过；后续初始检查503轮均AxisResetFeedbackValid及AllLinearAxesAtSafeZero失败，最后07:19:56归为RecoveryResetDeadlineExceeded。扫码Z读值32；07:20:49只读核对MB6040/6042/6046为0，MB6044/6048/6060为1，PC请求均0。
确定软件修复：将轴反馈检查失败及零位检查失败从通用DeviceWorkNotReleased中分离，返回明确ResetAxisFeedbackUnconfirmed、ResetSafeZeroUnconfirmed，复位握手完成后遇这些条件不反复等待而报InitialStateIncomplete，记录本次新读每轴请求/反馈/实际位置、预期零位和容差，保留真实复位完成事实。不把此类状态误显示为PLC复位超时，不扩大超时、不自动重发、不清未知请求或释放旧任务。
用户更正：扫码Z实际32毫米意味着未回到复位位置，并非允许的复位安全位置。撤销尚未部署的scanZResetPositionMm配置及相关实现，仍按零位±0.2毫米核验，不用修改允许位置掩盖未复位事实。待用户决定：MB6015=1且PC请求0时X/Y/扫码Z反馈0是否属于正常复位状态。当前不据此放宽轴反馈门禁，其他确定软件修复继续。R12零位±0.2、正常运动精度及R11完整动作证明保持；若需额外现场语义，先补规格契约再实施。
