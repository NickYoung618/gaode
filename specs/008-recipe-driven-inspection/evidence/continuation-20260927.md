# 008 中断后接续记录（进行中）

2026-09-27，用户明确要求接续“008收尾工作”。本记录按原 T049—T070 的条件推进，任务编号、原勾选、来源资料、原型及历史失败不改写。

## 中断后已核实的完成范围

- r8 job013，Q02-PENDING-P03，run `dd8fdac0-d7cd-475e-9a82-450be56cabc9`：exitCode=0、cleanupVerified=true。正式页面、场景审计、冻结预算/真实调用审计及取放提交审计全部通过。P03/协议槽3 Pending→P15，P01 OK明确无需搬运；取料后可靠在途提交早于实际放料写入，放料及ACK清零后才提交占用。刷新/重开状态保留。此前job001工具误判失败保持。
- r8 job002，Q04-MANUAL，run `7033ab59-030b-4386-b21c-d29de0629916`：exitCode=0、cleanupVerified=true。实际页面人工确认、占用及完成握手、全部面结果、刷新/重开、预算/调用审计通过；初始Height/FDecode各一次，无翻后重扫、无自动Flip替代人工确认。
- 同上构建中，Host/PLC/Application摘要沿r8 build-freeze.json；上述通过范围不替代整体人工、旋转、成组或恢复，也不直接关闭父任务。

## 整体人工原失败定位

r8 job003，ASSEMBLY-A-E-MANUAL，run `1f2ced8d-fa6c-483c-ab1d-11c5989a7473`，exitCode=1、cleanupVerified=true。公共3D媒体实际保存，Height worker在受理前报InputMediaUnavailable，随后Host记录WorkerResultBeforeAccepted/Height超时，F未派发并阻断；页面最后为DeadlineExceeded。该包不计人工换面验收。

实际图片普通Windows路径长度260；同Python进程下普通Path.is_file()为false，扩展路径is_file()为true且SHA256为 `3E53F61CBF7B61B09F34FF2EF32C7181220517C40430D83E4E7839AA863BAEEB`，与Host派发摘要完全一致。当前解除方式是缩短新的独立采证目录，不改程序/算法/配方、业务期限或系统长路径设置。长路径支持及受理前worker错误的更准确呈现记录为后续待办；主流程先沿短目录跑通。

## 当前接续批次

`artifacts/recipe-execution-008/r9-0927` 使用与r8完全相同的冻结Host/PLC/Application及原fixture。原r8未启动job004—012移到同批held-jobs-after-interruption保留，新批selected-jobs.json保存一一映射。r8 job014通过原reloadWorkerRoot交班：Administrator/Session2后继worker6548先于旧worker4992退出就绪，后继初始保持暂停；随后准备原剩余10条并启动原串行复核控制器。

顺序：整体人工、旋转Pending、Q04/Q05/Q06、GROUP-A-E、RECOVERY-F、整体Pending、E错误、旋转OK。每条退出清理并通过场景、预算及取放适用审计才放行后继，失败暂停。当前job000整体人工已启动，尚未计Passed。

原requirements清单为15/16，唯一未勾是规格含实现细节的质量项；此前授权和本次继续覆盖既有实施，不修改该只读清单。共享通信间歇超期的历史根因尚未确认；当前路径错误的确定原因不替代该问题。

## 通信诊断接续

r9 job000退出/清理true，run `35b4ac99-2449-4f6c-b1f0-1d6f2ac9dc9d`，页面StoppedOrUnknown。Host心跳tx462于19:05:57.211934Z写出；PLC于19:05:59.878784Z才读到首部，随后处理0.0665ms。PLC累计GC暂停2726.525ms。累计值与间隔只是候选关联，尚未证明因果，完整两端窗口提取为该job目录communication-window-readback.json。

r10仅复制r8冻结程序并把副本两个runtimeconfig.json的System.GC.Server改为false，其余DLL摘要/fixture/期限相同。该对照job000在正式页面前启动失败，退出/清理true。Host/PLC累计GC暂停均为0，PLC心跳tx16于19:08:43.2456835Z发回，Host首部直到19:08:44.3139438Z才读完、正文因原1秒deadline拒绝；匹配实际同连接端口64709。此结果否定“切GC模式即可解决启动超期”，不采用其为修复，也不计正式业务Passed。原比较和诊断窗口保留在r10。

r11恢复原r8程序及runtime配置，仅单个Q01短预检启用12秒JIT/GC/线程EventPipe采证，暂时工具接线只作用于请求文件明确指定的TestRoot。collector停止并刷新后才清理该次Host，防止再次截断trace；不延长通信期限，不将带跟踪短预检计为正式页面业务通过。后继worker由已有reload接班，正式剩余路线保持暂停。

## 新构建启动子交付与必要回归

r11 trace完整解析67337事件/11101 JittingStarted，工具刷新后清理，有限trace结束。临时接线快照保留在r11/start-script-with-finite-trace.ps1，正常启动脚本已完整撤回到冻结摘要。新增评估与修复报告位于`.specify/bugs/008-startup-persistence-order/`；代码确定事实是Station01HostedService恢复查询原先与先启动的PLC轮询重叠，间歇延迟因果贡献仍待实跑。

Program在app.Run前等待原真实持久化恢复的同一个初始化Task，hosted StartAsync复用，原未知不自动动作及Stop次序不变。plan/tasks先记录T054/T069子交付。独立r12 locked restore及7项生命周期测试通过，未新增镜像测试。r11→r12先新就绪后旧退出，worker10396/Administrator/Session2。r12 job000整体人工实际run53762f43-c488-4153-a8b8-82eb2d629903进行中，初始化日志已证明完成先于PLC启动，算法worker/HTTP就绪。新Host及原PLC/default server GC冻结在r12/build-freeze.json，不据短预检或生命周期测试关闭正式主流程验收。

## r12整体人工正式结果（2026-09-27 03:28）

job000/run53762f43-c488-4153-a8b8-82eb2d629903 exit0/cleanupVerified=true。场景12/12，预算/真实调用12/12，取放适用3/3通过。全部3面保留、整体动作及人工页面/Complete握手，无自动Flip替代；初始Height/F各1，Detection9、EDecode1，输入释放齐，各实际阶段提交在原冻结期限内，刷新/重开及物理处置读回一致。OK无需取放，不把零次搬运写为分拣能力已验。控制器先因漏复制预计算清单暂停，原错误保留；新清单由实际冻结Application与原fixtures独立生成，不读取运行事实，随后同一已退出包读回通过，不重复业务。

启动顺序已真实验证；bug-test结论partial只因间歇通信根因尚未完全确认。当前剩余job001—009已采用同一r12冻结Host/原PLC/default server GC，由新串行复核控制器逐条放行，旋转Pending启动中。原任务未勾。

## 后续代表：Q04启动超期再次出现

r12 job002 exit1/cleanupVerified=true，正式页面未启动，无业务run。Host心跳同连接53940 tx2：PLC于19:36:56.0944677Z发回，Host直到19:36:57.192502Z读完首部；原1秒期限在正文读取处触发。Host累计GC暂停33.7ms，PLC处理18.4948ms，完整7个诊断窗口解析无JSON截断。该证据不支持通信已修复；上文启动顺序子交付与整体人工Passed保留，通信诊断仍未收口。

r12 job001旋转Pending exit0/cleanupVerified=true，场景、预算调用及取放适用读回通过。原selected剩余未启动作业继续暂停。下一步仅独立r13 ReadyToRun编译比较以减少已观测JIT冷启动工作，源码/GC/协议/期限/fixture不改；不是部署、不宣称根因确定，也不以比较准备替代正式验收。

## 预编译启动比较（r13，进行中）

r12整体人工和旋转Pending通过，r12 Q04再次在页面前启动超期并清理可靠，原Failed保留。r13以同一源码Debug/win-x64/framework-dependent PublishReadyToRun=true独立生成Host，Host/Infrastructure/Application/Domain的PE managed native header均实际存在；无源项目/来源资料/原型改动，依赖restore锁文件只在artifact目录，源packages.lock时间仍2026-09-22。保持原PLC和server GC及1秒I/O/3秒心跳/所有业务期限。

r12未启动job003—009移到同批held-jobs保留；已有reload接班到r13 worker2504/Administrator/Session2。新独立预算清单从该预编译Application和原fixture计算，未读取实际run。r13 job000仅重跑Q04代表，正式结果待读回；余七条映射已准备但未投队列。编译比较不等于根因已确认或正式验收Passed。

## r13已通过与当前队列（2026-09-27 03:50）

- job000 Q04，run9f867c78-739b-4a8a-b33e-a5273fca1081，exit0/cleanupVerified=true，场景/预算调用/取放适用审计通过；不是r12同名启动失败的覆盖。
- job001 Q05，runea67f898-9568-446c-a7ae-a72687f5a277，exit0/cleanupVerified=true，同上审计通过。
- job002 Q06启动中，后继顺序为job003 GROUP-A-E、job004 RECOVERY-F、job005 ASSEMBLY-A-E-PENDING、job006 ASSEMBLY-A-E-ERROR、job007 ROT-PART-OK。每条审计后才放行，当前无Failed。

冻结Host1CEE88711814687F8966681781F5AA05B83B294EB9981960E0A09F88B07C005B，默认server GC，原PLC及所有原期限。deps.json package-identity-comparison.json记录新旧依赖包版本一致，实际PE预编译头已核。应用计数和绝对预算来自独立新构建规划，不用业务事实反推。

固定Gaode008DesktopRecovery任务继续沿既授权读/执行，原恢复请求快照保留。请求更新originalRoot=r13、preparedRoot=r14-recovery-0927空暂停目录；再次触发实际复用PID2504/Administrator/Session2（attempt-20260926T194713271-7576/result.json），未新起worker、未修改任务权限或登录触发器。后继自动接班可继续，登录启动仍待本批完成后处理。

r13 job000 Q04、job001 Q05正式Passed，r13 job002 Q06启动心跳tx5读延迟再次Failed/cleanuptrue。PLC同端点56526于19:50:30.4639422Z发回，处理0.0537ms，Host直到19:50:31.8794103Z读完首部，原deadline取消正文；GC累计31.19ms。预编译不足以解除当前通信阻断，不认定根因解决。完整7个窗口解析无截断，无业务run或正式页面通过。

下一有限比较root r14-io-0927，原r13 DLL/PLC/GC/fixture/期限全部相同，仅指定该次自身Host进程DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1。已核实实际安装.NET10.0.12的PortableThreadPool.cs代码：Windows默认从IO poller派发线程池，开关改为内联回调；仅诊断候选，不据一次结果认定线程池根因。临时启动hook只匹配明确expectedTestRoot，Start-Process -Environment仅该Host，PLC/父进程/系统未设该变量。正常启动脚本完整副本保存，结束后撤回。原未启动job移held-jobs保留，新worker先就绪再旧退出，剩余正式代表仍暂停。

## r14比较Failed与r15启动顺序子交付

r14 Q06 runb590291d-f687-4ba0-8522-009955c535f6，exit1/cleanupVerified=true，运行期通信失败、页面未Final。失败期间与独立r15构建资源重叠，明确记录比较干扰，不能以此单包认定内联回调因果；不采用该开关为正常修复。临时hook快照保留，原脚本已撤回，摘要19C2251C791D9EA03E75FFE0B9FAC654A2B5797B3F89759CC8C0A5A914E2A15D；request移同包。

确认r13失败前PLC心跳已开始、worker与HTTP仍在初始化。按原T054/T069先更新plan/tasks/assessment，PlcConnectionHostedService改为BackgroundService等待ApplicationStarted再真实StartAsync设备；真实状态仍唯一准入，HTTP可达不代表设备Ready，原Stop顺序/释放及失败停Host语义保留。不改API/协议/期限，不增加动作重试。源码停服务实现保证先取消/等后台启动、最终释放设备。

r15独立locked restore/普通Debug构建，7项HostLifecycleTests全部通过；构建已退出。后继ready再旧退出，当前r15 worker沿同Administrator/Session2。采用普通构建/default server GC/default socket scheduling/原PLC和fixture，单条Q06正式复验，其他5条差异仍暂停。不将启动次序修正或测试Passed宣称通信根因解决；后续正式运行与构建/测试串行。

## r15默认调度失败与无构建干扰比较

r15 Q06 exit1/cleanupVerified=true，在启动门禁期间失败，尚无正式业务run。真实日志完整证明持久化完成→worker Ready→HTTP ApplicationStarted→PLC连接，启动次序子交付成立，但不足以解除全部通信阻断。Host心跳tx78同端点58574，PLC20:00:14.9550229Z响应、处理0.0244ms；Host20:00:16.4335785Z读完首部，GC累计34.747ms。原期限拒绝正文，7个窗口无截断。

r16-io-0927保持r15源码/DLL/原PLC/default server GC及全部期限，重新作一次明确自身Host内联回调比较。所有独立构建/测试已退出，临时request精确匹配单Q06 TestRoot，不修改父进程/PLC/系统设置。结果待读回，原失败及诊断干扰记录保持；启动顺序及通信根因结论分开。

## r16正式Q06通过与显式Test设置接线

r16 Q06/rund4fc1789-b50b-487f-953c-9acbb14410eb，exit0/cleanupVerified=true，场景/预算调用/取放适用审计通过。普通r15 Host/default server GC/原PLC/原期限，明确仅自身Host内联回调；6检测、Height/F各1、阶段提交与Final成立，构建/测试已结束。历史默认调度失败均保留，不据单条定义全局根因。

为后续原剩余主流程使用显式Test CLI开关，spec/contracts/plan/tasks先记录后修改wait→verify→start链。开关默认关闭且拒绝无合法Test fixture；只Start-Process -Environment设置所属Host，PLC/父/全局不改。临时request/hook撤回归档，未留下隐式请求接线。3脚本语法检查零错误，实际无fixture开关调用在任何store/device/process启动前拒绝，测试报告在r16。

r16旧worker基于原reload分支读当前新脚本摘要，成功先启动新r17 worker就绪再退出；r17新worker摘要7244578F9B5112C97E4BEF65877DFE905E0DF7C9C57686C69524D4E20141614B，Host5A335D720B4B76290979EA7586026451DBA1FFCEB34E4C834A164C1B64F3B531（同r15）。当前5条原代表：GROUP-A-E、RECOVERY-F、整体Pending、E错误、旋转OK，均显式hostSocketInlineCompletions=true、default server GC/原PLC/所有原期限。控制器逐条完整读回后放行，当前GROUP启动中，尚未计通过。

T054/T070证据元数据修正：Station01Registration已按public purpose=Test使用50ms轮询，当前及历史实际PLC starting日志同为50；start脚本process.json却写25。仅将之后作业记录改成实际50，设备/期限/配方/预算未改，不重跑业务；原process.json保持，读回以实际日志为准。r17当前GROUP已加载旧脚本、后继使用元数据修正版，source-freeze-amendment-poll-metadata.json记录两份摘要，不把原冻结源码重写为新版本。

## 2026-09-27 虚拟PLC I/O有限比较（T054/T069/T070）

r17 GROUP-A-E/run2431de00-3dda-4a03-adcb-40f9dce48944在P03 BASE E的InspectionBegin受阻，实际XYZ已匹配。Host20:15:49.799033Z写出tx9478，PLC20:15:54.5024904Z才读头、处理0.0133ms；当时PLC累计GC37.593ms，不据此认定GC根因。正式失败保留，不计通过，后继暂停。

增加默认关闭的PlcSocketInlineCompletions开关及queue字段plcSocketInlineCompletions。沿wait→verify→start传递，必须合法Test fixture；仅所属VirtualPlc Start-Process -Environment设置DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1，process.configuration.plcSocketInlineCompletions登记实际启用。Host开关原含义保持，父进程/系统环境不修改，不改变DLL、GC、协议、期限或成功条件。先对同成组路线作一次独立有限比较，真实页面、数据库、设备及算法全链仍必须完成。

采证器对非预期恢复场景应识别现有页面阶段的“阻断”状态并保存StoppedOrUnknown，避免只识别中文fault而等待全预算。恢复注入仍沿既有waitingRecovery条件操作，不把阻断视为Final。

## 2026-09-27 04:27接续检查点

r17成组正式Failed/cleanupVerified=true，后继四项移held-jobs保留；Host单侧Test调度设置不足以完成该路线。r18-io-0927同DLL/default GC/原期限，加所属虚拟PLC显式Test调度设置，仅GROUP-A-E有限复验。worker11392/Administrator/Session2，源码摘要45FC46C888A4AC243EB82E0A63C8CFCADB9A7475EA078A606D91CA7A76AC4295；原worker接班后退出，未重复启动或修改固定恢复任务。预算独立规划42次Detection、2次E、6次翻面、8普通分拣单位；尚未计通过。三脚本AST零错误、Node语法通过、无fixture PLC开关真实拒绝；恢复请求仅更新当前root/摘要，不增加登录触发器。

r18 job000 Failed/cleanup=true，ReadyHandshakeFailed(5秒)发生于PLC连接前，无业务run，不能评价PLC调度比较。原源码实际worker在Codex账户独立握手157ms/Ready，仅诊断，不冒充Administrator/Host验证。Test worker补Started/HelloReceived两条关联生命周期audit，已有Ready事件不改；不变算法、协议、5秒期限。r18 job001独立正式尝试按相同配置记录补充摘要及原失败，不自动重放旧run；独立预算新文件保留原规划。

r17实际页面StateObserved=20且raw errorCode非空，但模块阶段仍进行中；只补中文阶段阻断不足。后续collector按同run实际前端StateObserved=20停止非预期恢复作业为StoppedOrUnknown，不视为Final；既有waitingRecovery保持。本次旧失败报告83MB/2200网络条目，增加完全相同GET响应体SHA及原requestId引用，保留所有请求/状态码及每次变化的实际body，减少重复高频诊断；POST/错误结果与最终验收事实不替代。r18 job001已加载之前collector，原证据不重写，新源摘要单独登记；Node语法通过。

r18后继4项已在pending-jobs准备，独立预算另存selected-route-budgets-remaining.json。控制器只在成组job001成功退出/资源清理及scene/budget/sorting三份passed后才移入queue，逐条审计再放行；失败保留暂停。后继使用当前collector，job001保留实际已加载旧版本。无构建/测试与正式运行重叠。

## r18恢复采证时序缺陷（T069）

job002旧run f86b2a63-030b-4792-ad8b-2986f6ada2c8、新run8aaf32b5-c77f-4342-a4a5-d2089db03bec已真正完成双端复位/初始核验/显式新轮及Final，exit1/cleanup=true。23项读回中仅old_actual_media_visible_through_fault_and_reset=false：beforeFault三维图片已在API Ready，却尚未画到页面（页面Idle）；atFault/afterReset实际显示true，旧SQLite/文件/API摘要一致。原包保留Failed，不能以新Final抵整项。

采证修复仅在既有oldMediaProof内有限等待同旧run实际Ready三维媒体的img data-media-id且complete/naturalWidth>0，再保存截图及注入F故障；不改页面、算法、PLC或业务期限。不写DOM/不伪造displayed。后续新作业重新验证全条件，不覆写本包。

r18 GROUP-A-E/job001/rund85fefeb-c937-4764-9b71-4537c5e0750b正式Passed/cleanup=true：2组8成员14面、C28/F14即42 Detection、Height/F各1、E2、翻面6、翻后重扫0；全成员OK，实际普通取放0且8个实体NoMoveRequired均有已提交依据，组/面不复制物理处置。scene10/10、budget12/12、sorting适用3/3全部通过。页面刷新/重开/对象切换、E内部对象与外部码关联成立。正常Debug r15 Host和原PLC同冻结摘要、default GC/原1秒I/O与3秒心跳，所属Host及PLC明确Test I/O设置；不宣称默认模式或生产根因已解决。

r18 job002恢复Final仍整体Failed，23读回22通过，beforeFault显示时序缺口不放宽。修collector等待真实图片后job006独立验证，同冻结配方/程序/期限；剩余整体Pending/E错误/旋转OK保持暂存，仅job006整包验证及scene Passed后放行。新控制器不把job002改成Passed。

r18 job006 RECOVERY-F/run800275b5-ffe5-47d1-adfa-758b8b76e48f正式Passed/cleanup=true，23/23 operation-route及scene适用检查通过。旧runaaff6662-b267-4a72-8eb6-dbcab4b0e2df保留FaultRequiresNewRun/finalOutcome0；reset70211adc-439f-4dcb-9e81-7139d2a3a0b2、initialCheck78ce0708-04fc-4ba8-8b32-f6048ce73d75和newRun双向关联已保存。实际旧三维媒体336ea3cf-8988-4747-9af5-b5059c400a32，三次页面displayed均true且SHA/字节长度/SQLite/文件/API相同，新轮初始Height/F重新调用、3 Detection及全部盘末Final成立，不复用旧结果。原job002 beforeFault显示false失败不改；修复仅collector实际图片加载等待，业务/页面及期限不改。恢复验证主包为本job006，旧首图前恢复只作其原子范围，不再替代已有图片条件。

登录启动后续准备：固定Gaode008DesktopRecovery实际仍为0触发器/单一既有恢复action/Administrator InteractiveToken，Codex授权ACL仅FRFX读执行，未修改。已准备scripts/enable-008-desktop-recovery-at-logon.ps1给Administrator交互桌面执行：验证该固定action/同账户，保留现有ACL和action，仅增加该账户登录触发器；保存前后XML，不自动派发job。脚本AST零错误、只读核实Principal实际解析SID-500。尚未执行/未配置登录，不把草稿当环境验证；待正式批次完成后交用户审核执行，现有worker先持续复用。

## 2026-09-27 权限拒绝页面补验（008 T055/T070、003 T068、006 T048/T049）

现有权限鉴别与查询受限绑定已实现，历史006记录仍缺401/403正式WPF拒绝证据。源码启动catch始终Unknown，finally/render又按无结果覆写，需要以真实拒绝作业核实，不能仅引用查询catch或组件测试关闭父任务。

仅补Test采证开关AuthorizationMode=Auth401/Auth403，限Q01合法purpose=Test fixture。沿queue.authorizationMode→wait→verify→collector显式传递；真实页面选用后只对POST /api/v1/station01/runs在CDP Request阶段去掉Authorization(401)或替换为该作业有效EquipmentEngineer令牌(403，无Run.Start)。实际Host鉴别并返回错误，不拦截/伪造响应，不改业务授权。403凭据随机生成、仅所属Test Host配置/collector内存使用，不记令牌、头或命令行；普通模式默认不启用。

每次实际页面StartFailed及故障/状态区域、请求状态、清理后真实SQLite零Runs/控制命令、虚拟PLC无启动/产品/分拣动作分列核对；错误回执不可Final。权限工具等待30秒、外层240秒仅用于预期无业务run的拒绝测试，不改变业务期限或当作普通路线Passed。

若实际页面误报Unknown/尚无结果，006仅将已知401/403绑定到既有“权限受限”和已存在的拒绝文案，在render中保持该状态；不改客户ZIP、HTML结构/文字/控件或交互，其他结果绑定不改。旧失败与真实新验证分开记录。

r18 job004 ASSEMBLY-A-E-ERROR/runabf49a3a-6cc8-4a92-b92f-37cc385b3184 Passed/cleanup=true：Height/F/E各1、Detection9、3面；E异常真实持久化后整体OK继续，普通取放0；scene/budget/sorting适用检查全通过。job005 ROT-PART-OK/run43962e3e-7eef-4242-912a-20fd47f747ef Passed/cleanup=true：Height/F各1、Detection6、2面，SpecialExit实际OK且释放，不执行普通Sorting；页面刷新/重开和实体处置一致，三项审计全通过。r18七次正式尝试五次全条件通过、两次Failed（Ready、恢复首图时序）原包均保留。

本轮正式批次完成后才修改共享采证脚本，新增显式Auth401/Auth403 Test入口；普通默认关闭。Node语法、三PowerShell AST均通过。后继r20-auth-0927仅独立负向权限作业，复用同r15 Host/原PLC/default GC/原业务期限；不计普通路线Final验收。通过原worker接班协议加载新worker摘要，先暂停准备，不借恢复任务执行其他管理命令。


## 2026-09-27 最终本轮交付（本节优先于全部历史检查点）

当前Test选定主流程和必要验收已完成；008整体仍未完成。原22任务本轮完整审计后20项勾选，T058保持原X；T055因明文003 T065共享心跳延迟机制验收未全齐、T070因原T055依赖保留未勾。没有将未勾的父任务改名成“已完成子范围”，也不修改001/003/006父任务勾选；具体全条件、直接依赖及复用边界见[逐任务审计](task-audit-night-20260927.md)和[当前矩阵](../coverage-matrix.md)。质量清单15/16不动，不把文档或预检计为业务测试。

实现/修复包括：可靠取放预留/在途/占用及预算、真实物理处置投影、普通暂停安全边界与同run继续、实际旧媒体/恢复采证、命令/设备/保存持久结构化日志与重复查询受控、持久初始化先于PLC启动、有限预检/清理/接班、显式所属Test Host/PLC I/O配置。本次新修复为复位等待实际缓存PlcReady、旧Final门禁测试采用当前合法fixture、真实401/403权限状态保持；均有原失败、先合同后代码、独立构建及必要验证。源码/脚本/前端产物SHA与实际ResourcesResolved沿r20/r21/r22冻结包，普通默认配置和原业务/I/O/心跳期限保持。

当前独立必要验证35个不重复用例最终全Passed：r19当前合同18，SQLite/恢复/Final七项首次4Passed/3Failed原TRX保留，r21只复验三失败用例全部Passed，当前目录退出八项+容量/目标冲突两项10Passed。原投影15+生产者15、生命周期7及其余必要旧TRX仅按各未改源分支复用，不相加成当前全套测试。frontend build及PowerShell AST/Node采证语法通过，HTML四份与客户原型ZIP不变；新实际WPF runtime SHA3E6BCE15A6A560761CC055FD6E8009BEC8E6A2E64E792E5D830837CA372BBB29。当前正常Debug r21 Host实际Q18和恢复均通过，原PLC/默认server GC，只有所属Test进程明确I/O开关。

本接续正式路线尝试共48，整包Passed 24、Failed 24。其中exit0为25，但r5 job006整体Pending的场景Failed，故不能把exit0都算整包通过。权限拒绝页面3次：2Passed/1Failed，另外预检13次11成功/2失败，worker自动接班16次均成功，分别统计、不计普通路线。原page-ed-res 13次10Passed/3Failed是独立历史批次，不冒充本新构建。所有失败包位置见r22/failed-attempts-final.json，逐尝试定义/清理/各审计及源路径见r22/attempt-inventory-final.json；历史清理失败也保留原状态，不声称所有历史cleanuptrue。

最新Q18 run e4260221-41e2-4574-b705-33b6e8cebed6实际CD→AB→CD→CD，4面8采集4融合12Detection、初始Height/F各一次、3Flip/ACK、翻后Height0，普通OK无搬运但已提交NoMoveRequired，整盘Final/刷新/重开/实际字段一致。旧Q18 CD→CD→AB→CD合法历史不得改写成当前指定顺序。完整恢复主包见failures.md最新节和r22/current-primary-summary.json。

两轮converge：第一轮发现已有物理投影/人工/特殊/组/恢复及通信验证缺口，全部继续原T049—T070，追加0；第二轮在上述代码/必要测试/正式WPF后复核FR18/SC6/故事场景19/计划决策8/宪章原则13，无新增代码任务，追加0，检查期间tasks SHA3C0A4FF32655B98147F55FD878603AFE8CE572151BF627361B7B43A85F0A3918字节不变。既有T055/T070依赖条件保留，随后独立实施/证据阶段才更新19个勾选。最终analyze只读，没有Critical/High新增需求冲突，历史带日期状态及要求中既有实现备注为Low且当前证据入口明确；独立静态记录不算运行测试。

真正剩余原条件：003 T065的异常延迟机制及修复前后对照还不足，现有Test I/O缓解与多个完整成功代表只证明当前选定路线；r22 job001机构初始状态HTTP1秒超期也保留，不能宣称默认模式/VM调度根因已解决。过去缺失的3516ms窗口日志不能补造，后续需可控主机与有权限的系统级调度/I/O采样做定向前后对照；不重复同条件盲跑。生产局部原B限制包括真实点位/高度标定、取放目标阶段可靠采样、布局/容量及特殊机构生产映射，真实相机SDK/算法精度和现场节拍未验，不能通过Test反推参数。以上不是新增全Test门槛。

非阻塞待办：允许但未选位置变体、完整异常组合、性能长稳、示教/前端编辑保存、历史文档清理，以及Windows登录自动启动。固定恢复任务已有真实查询/执行授权、重复复用和16次自动接班；登录触发器仍0。脚本scripts/enable-008-desktop-recovery-at-logon.ps1已准备并AST检查，未执行，不宣称配置或登录实测成功；Codex仅获固定任务读/执行权限，不能改触发器。用户若需要自动登录接班，在Administrator交互PowerShell执行 `& "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -File "E:\dzk\gaode-1\scripts\enable-008-desktop-recovery-at-logon.ps1"`。该用户动作仅管理登录触发器，不影响本轮正式验收。

现场保留：r22 worker9428/Administrator Session2等待且queue/batch-paused.signal保留；新调度禁止自动下一业务job，所属Host/PLC/WPF/worker子资源按两个成功包与失败包各自cleanupVerified释放。桌面worker不提前关闭。若会话丢失，可按已有固定Gaode008DesktopRecovery任务执行恢复脚本，该任务仅复用有效worker或启动独立暂停root，不自动重跑业务。当前恢复请求root r22及worker加载SHA96787623D2CE21C9ED6C53FAF53FDD84D5001636B706401336D281341259DF85保留，生产/真实设备无操作，无提交/推送/发布。

范围澄清：早期Codex账户157ms ready探测使用错误worker/1.0 echo，只作该探测事实，不代表Administrator当前Host/2.0协议或正式页面验证。r12 source-differences比较37个源/脚本、7改动，不能称629项源相同；缺失baseline必须NotComparable，不造同构建证明。r22 build-freeze继承previousRoot=r17的元数据错误由source-freeze-amendment-metadata独立说明，实际worker前继r20、Host构建r21，原文件不覆写。


## 2026-09-27 最终当前恢复主包（优先于历史检查点）

唯一当前主验收为 `artifacts/recipe-execution-008/r22-page-0927/runs/job-002-RECOVERY-F/RECOVERY-F`。旧run `18de5e0b-c5c0-4237-9ada-805ceed87316`，新run `f8f791a2-015b-4502-b8ab-fb318905c776`；reset `90678c2b-b4ac-4aed-a3fa-dda048e1667e`、initialCheck `ee49a4ba-9650-422f-9822-9adfa56d5f5f`。正式页面复位→初始核验→显式新启动→完整公共准备/F/AB单图及融合→下料/可靠解锁/取盘→Final，23/23操作、5/5场景与外层exit0/cleanup=true。旧run保留FaultRequiresNewRun/finalOutcome0；双向链接及新身份持久可查。

旧实际三维媒体 `03fdcbf8-a07f-4eeb-8479-3e14a44e8e42` 在故障前、故障时、复位后三次实际DOM加载显示均true，SQLite/文件/API的SHA及99字节相同；新轮重新Height/F各一次、Detection三次，不复用旧完成标记。原r18主包保留其r15构建范围，现由本新包承接T069唯一当前验收；早期首图前恢复不抵旧图条件。

当前正常Debug Host来自r21，SHA `90BDA14E495B482F98FD841189314499B829DB8508177C1EB88D7C8B8D295409`，Application `60A045DEA40FA2825CC87FEBDD76A497F19C377C9069D766428C41F1131AEAFA`，PLC仍原 `66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF`。默认server GC、1秒I/O/3秒心跳/原业务期限，明确所属Host与PLC Test内联I/O，生产和默认模式根因不作通过声明。

r22 job001真实旧run3955ced1-bb60-4c67-a3c3-e8886d58122a，在复位后读取虚拟机构初始状态的HTTP超过1秒而Failed，cleanup=true；原错误、日志和页面保留，未启动新轮。它不再出现r19缓存PlcReady不同步的错误，属于独立运行时超期。定位到ReadInitialStateAsync原HTTP读取及虚拟状态快照路径后，仅一次独立job002定向复验，程序/配置/期限未变；通过不证明该超期机制已修复。不得重复盲跑或放宽期限。
