# 020阶段B实施验证记录

2026-10-08；源码HEAD `253324492a3ffe9ea5a62b605b5dd812d04b267c`，含已有工作区修改。阶段B实施中，非发布版本。

基线见[evidence/stage-b/baseline-stage-b.json](evidence/stage-b/baseline-stage-b.json)，记录现有修改、五个标签和源码哈希。全部测试使用临时隔离根/loopback；不连接PLC或相机，现场配置和数据不变。requirements为26/31，用户在了解局部未决后已明确“开始执行”，按局部范围推进，清单只读。

T029：分析及4项整改已核对，见[analysis-stage-b.md](analysis-stage-b.md)。T030：基线已记录；忽略规则覆盖bin/obj/日志/DB/本地现场配置，无需更改。

## 阶段B首次implement收口结论（历史时点）

确定的软件任务已实施并定向验证；阶段B尚未全部完成。正式程序55项通过，工具58项通过；Host、CameraWorker、DeploymentPrep构建均0警告/0错误。没有连接硬件或运行现场Host，没有打包、部署、提交、推送。最终工作区版本清单见[evidence/stage-b/software/source-state-final.json](evidence/stage-b/software/source-state-final.json)，它是HEAD加未提交文件哈希，不能作为发布提交号。

| 分类 | 状态与依据 |
| --- | --- |
| 已验证的软件 | T029–T054、T057–T060；范围与下表一致。T035复用共同Validator/SqliteRecipeStore，经正式API验证，无需为了任务改动已正确的代码 |
| 已修改、物理效果未验证 | T049 SDK节点设置/读回/正常恢复已编译，离线worker契约已验；真实Galaxy/CameraPro节点与七机效果待T055 |
| 尚未执行的现场义务 | T055七真实相机、T056正式新用途连续两轮真机；Blocked，未勾选 |
| 尚未实现或决定的依赖 | PLC-Q3剩余位/等级/清除、PLC-Q4首次/复位/软停恢复映射；不得用假安全值代替。公共Z运动用途未扩张 |

## 软件验证与复现

从`D:\gaode`执行。正式命令（55项、0失败、0跳过，44秒）：

```powershell
dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj --no-restore --filter "FullyQualifiedName~ControlledCommissioningTests|FullyQualifiedName~RecipeCommissioningChainTests|FullyQualifiedName~CommissioningWorkflowTests|FullyQualifiedName~MixedRuntimeCommissioningTests|FullyQualifiedName~CameraProtocolTests|FullyQualifiedName~CameraWorkerLifecycleTests|FullyQualifiedName~CameraBusinessRegressionTests|FullyQualifiedName~HandshakeClosureTests|FullyQualifiedName~SamePositionTests|FullyQualifiedName~SiteProtocolAdaptationTests|FullyQualifiedName~MemberGripperTests" --logger "trx;LogFileName=stage-b-final.trx" --results-directory specs/020-real-device-commissioning/evidence/stage-b/software
```

该次阶段B最终结果：[stage-b-final.trx](evidence/stage-b/software/stage-b-final.trx)。早期plc-regression.trx、recipe-camera-mixed.trx为增量验证，不累加成最终计数。补日志断言后的第一次编译因测试属性误写Frame失败，改为CoordinateFrame；随后一次测试因测试读取活跃worker日志的共享方式引发进程中止，不能算通过。改用现有FileShare.ReadWrite读取辅助方法后完整重跑55项通过，最终TRX覆盖中止结果。

工具从`D:\gaode\tools\plc-commissioning`执行：

```powershell
python -m unittest tests.test_commissioning tests.test_same_position tests.test_recipe tests.test_member_gripper -v
```

58项、0失败、71.424秒；[完整输出](evidence/stage-b/software/tool-regression.txt)。测试仅loopback，不启动GUI或连接现场。

构建分别执行`dotnet build <项目路径> --no-restore`：

| 项目路径 | 结果与日志 |
| --- | --- |
| backend/src/Gaode.Host/Gaode.Host.csproj | Passed；[build-host.txt](evidence/stage-b/software/build-host.txt) |
| backend/src/Gaode.CameraWorker/Gaode.CameraWorker.csproj | Passed；[build-camera-worker.txt](evidence/stage-b/software/build-camera-worker.txt) |
| backend/tools/Gaode.DeploymentPrep/Gaode.DeploymentPrep.csproj | Passed；[build-deployment-prep.txt](evidence/stage-b/software/build-deployment-prep.txt)；仅构建维护工具，未操作现场库或生成部署包 |

## 验证范围与参数证据

| 场景 | 软件证据及限制 |
| --- | --- |
| B-V01：人工制作、保存、编辑、重读 | RecipeCommissioningChainTests通过真实Kestrel API与隔离SQLite；来源ID/版本/多配方、空Approval新用途准入、ETag、字段拒绝和重开读取；旧Test/Production审批规则未放宽 |
| B-V02：冻结与实际消费 | API保存的双面配方进入正式RecipeRunPlanner/RecipeDetectionExecutor；[P01–P10对账](evidence/stage-b/recipe-field-trace.json)记录移动请求、翻面/放回、冻结设置、采集和SQLite/媒体。声明端口来源为Test，不能当实际PLC运动；P05/P06保存校验冻结但未在该检测组件执行，E不适用、F由下一行独立证据覆盖；公共Z不宣称已运动 |
| B-V02：正式Host正常链 | CommissioningWorkflowTests沿旧Test布局TCP和合法Test配方，运行公共准备→3D/F绑定→翻面/放回→采集/算法→卸料准备/分拣→WholeTray提交→AwaitingManualTrayRemoval；实际保存7份媒体。见[workflow](evidence/stage-b/software/legacy-test-workflow.json)、[持久日志](evidence/stage-b/software/legacy-workflow.log)。这是检测和落地完成、等待人工取料的边界，人工取走后的最终出料未测；不是新用途完整现场链 |
| B-V03：混合装配和受控算法 | ControlledCommissioningTests/MixedRuntimeCommissioningTests：Real PLC端口唯一、七角色worker未启动、虚拟灯/算法明确来源、真实时钟、用途与文件摘要/引用校验、六项有来源预算及新Profile隔离；实际读取当前文件媒体并释放租约，3D/扫码/单图/双图按配方范围消费；缺安全输入在运动前拒绝。所有OFFLINE值仅夹具，未生成现场输入 |
| B-V04：相机参数和帧 | CameraProtocol/CameraWorkerLifecycle/CameraBusinessRegression：wire v2、原子参数采集、两组读回/两新帧、fixture正常恢复、不支持及读回不符零触发、旧记录Unknown；虚拟灯实际消费但PhysicalLightApplied=false；sidecar及索引完整保存。真实SDK效果未验 |
| B-V05：现场协议已知部分 | 独立SiteProtocolTcpFixture真实TCP实现FC03/06/16，literal CDAB REAL型号、INT姿态、地址/只读方向、F XY/E扫码Z/检测Z轴用途、报警Bit0光栅/Bit2安全门及等级0；独立故障不被0覆盖。显式PositionBasis只解释新读坐标，不制造许可；SafetyUnconfirmed时零运动派发 |
| B-V06：清零整合回归 | HandshakeClosure/SamePosition/MemberGripper沿原Test布局，正常连续两轮、延迟/不清零、过期/断线/未知、父动作及取料保存门、Y与检测Z同坐标历史资格；无未知自动重发。现场安全门未明，所以未声称在现场布局跑过完整运动握手 |
| B-V07：现场代表链 | Blocked，T055/T056未执行；七台真实相机新设置、PLC连续两轮运动和现场参数对账均没有本轮证据 |
| B-V08：诊断与同步 | 已持久核对[受控算法输入失败](evidence/stage-b/software/algorithm-diagnostics.log)、[相机读回失败](evidence/stage-b/software/settings-readback-fail.log)、[不支持设置](evidence/stage-b/software/settings-unsupported.log)、[SQLite提交失败](evidence/stage-b/software/save-failure.log)：Error、组件/阶段、Run/Capture/Operation、错误原因及NoReplay；相机零触发/零发布，保存失败保留占用但不发布媒体，算法失败无Result/释放租约。正式与工具差异见[document-sync](document-sync.md) |

P01–P10完整现场实际消费尚不能宣称通过。未执行的E/R/NG等现场工艺和历史019 SDK阻塞/物理断线验证继续保留，不因软件用例通过消失。临时数据库由测试真实创建并清理，证据中的路径为运行时路径，不是需交付的现场库。

## 局部阻断与下一阶段

- T055：需要七机角色/序列号/NIC/SDK与配置核对，以及新的现场执行授权；本轮明确禁止连接硬件。
- T056：需要适用PLC-Q3/Q4答复、有来源且经用户确认的虚拟安全值、完整现场配方/型号/姿态/格位/机械范围/坐标依据/预算/码规则，以及唯一PLC写控制端和现场授权。
- OPEN-020-06公共Z：保留原字段读存及公共XY职责；未批准运动用途变更。
- 发布：仍需要最终源码提交号、真实配置及其摘要、适用现场验证、数据/配置兼容与回退步骤、包清单和GitHub变更说明；本次不制作或推送。

具备进入`speckit-converge`核对软件范围并按规则追加缺口任务的条件；不具备宣布020整体完成、真机验收或部署放行的条件。requirements保持26/31且只读，局部未决不重问已确认事项。after_implement hooks为空，无需派发扩展。

## Phase 8增量：当前结论（2026-10-08）

本次T061–T065按授权完成：T061只交接独立前端规格缺口，页面/桌面仍待后续；T062验证坐标及单张曝光A/B编辑隔离并核实际请求；T063将上文旧Test Host路线扩至人工确认、最终Completed及SQLite重读；T064经新用途正式DI/Start验证缺安全输入和PLC-Q3/Q4未明时零相关运动；T065更新过时进度。最终最小回归12/12、0失败/跳过，Host构建0警告/错误，详见[validation-phase8](validation-phase8.md)及[新源码/证据清单](evidence/phase8/source-state-final.json)。

上文55项及58项、等待人工边界和原source-state-final均保留为历史证据；本轮新证据单列，不覆盖、不累加计数。T063人工来源Test，不能声称真实料盘取走；T064是新用途失败闭环，不是完整新用途正常现场链。T055/T056与历史SDK/机械未验保留。可再次speckit-converge，不具备整体完成或部署放行条件。
