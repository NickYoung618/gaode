# 阶段A最小验证指南

日期：2026-10-08。**状态：阶段A已实现及离线验证通过；结果见[validation](validation.md)**。本指南用于软件离线复核；没有连接硬件、打包或部署指令。以[plan](plan.md)、[HC](contracts/closed-loop-handshake.md)、[TC](contracts/commissioning-tool.md)为预期，不把本指南当已通过报告。

## 前提与顺序

1. 当前020的[tasks](tasks.md)已生成，完成只读交叉分析并处理当前阶段关键问题后实施；保留既有修改和回退标签。核对`.specify/feature.json`仍指本目录。
2. 复用.NET 10、现项目依赖与Python 3.12；仅使用loopback和明确Test配置。测试程序不得加载真实PLC host或实际设备账号/配置。测试坐标不得输出为现场默认值。
3. 先补VirtualPlc/工具模拟器独立清反馈，再实现适配及消费者；测试断言独立依据HC，不直接复用被测清零判定。
4. 既有SamePosition测试须先执行一轮实际模拟动作并清零，不能用“直接写坐标+反馈1”造复用资格。新故障注入明确Test来源，所有等待有界。

## 最小场景

| ID | 准备与操作 | 必须核对的证据 |
| --- | --- | --- |
| V01 | 各轴（含R）及翻面/放回、取放连续两轮 | 实际完成→清写应答→新读全0→下次相关请求，六轴映射正确；正式/工具分别证明 |
| V02 | 保持旧反馈，稍后独立归0 | 期间请求仅撤一次、后继未派发，归0新样本后才继续，心跳可运行 |
| V03 | 永不清反馈或清写结果未知 | 原预算到期，明确错误/持有未知，无成功、无自动重发；迟到全0不自动恢复 |
| V04 | 注入清写前已开始的旧读、过期缓存、断线/重连、字段混合代次 | 即使值全0也不生成有效清零证明，关联资源保持阻断 |
| V05 | 首轮闭环后全轴相同、X变化而Y/检测Z相同；再漂移/复位/重连 | 前两类正确复用且无多余启动/Moving等待；失效类不复用；首次正常移动不被强制要求历史记录 |
| V06 | 翻面持件→放回；Pick→提交→转运→Place→Safe | 中途父命令未清；提交失败不搬运；最终安全轴Closed之后父清零；抓手选择保持 |
| V07 | manual/recipe/raw/clear都尝试绕过未清周期 | 后端/工具入口拒绝后继相关请求；人工清0不制造成功或复用；锁不死锁、不递归重复派发 |
| V08 | 完成后反馈已0，保存并消费证据；覆盖全复用和Acquisition保存失败 | 实际运动保留到位身份，复用以本次复核关联当前Action且可用于后续采集；清零诊断独立，必要保存前不释放所有权，失败不丢物理事实 |
| V09 | 严格运动、工具手工坐标报告和配方旧放宽路径、轴反馈0后的分拣 | 严格路径需派发后Moving；手工未验收坐标不直接授复用；配方新鲜到位+坐标仍正确；VirtualPlc基于内部停稳事实接受合法Sort，不删除位置门 |
| V10 | 核对受影响文档与源码/配置、历史证据 | document-sync逐项落地，新验证单独记录；未跑硬件/后续配方链不计通过 |

先正常主链，再覆盖直接影响安全/完成真实性的上述失败路径；不扩大为全量历史测试。R和NG/取放此处是离线协议覆盖，不代表真机代表路线新增动作。

## 后续执行命令

以下从`D:\gaode`执行。`HandshakeClosureTests`已实现并纳入26项正式定向测试；不能以过滤器零匹配当通过。依赖未还原时先沿仓库既有还原流程，不安装/升级Spec Kit。

```powershell
dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj --filter "FullyQualifiedName~HandshakeClosureTests|FullyQualifiedName~SamePositionTests|FullyQualifiedName~MemberGripperTests"
```

正式夹具`ProtocolTcpFixture`使用真实TCP和进程内实际VirtualPlc引擎及SQLite证据，不等于独立进程/真机验收。测试须检查非零用例数量、请求审计与持久化内容，不能只看返回成功。

工具在`D:\gaode\tools\plc-commissioning`执行：

```powershell
python -m unittest discover -s tests -p test_commissioning.py -v
python -m unittest discover -s tests -p test_same_position.py -v
python -m unittest discover -s tests -p test_recipe.py -v
python -m unittest discover -s tests -p test_member_gripper.py -v
```

这些是已存在且直接受影响的文件；新用例优先落其中，不新建大测试平台。若旧失败来自被替代的首次静态复用假设，按HC重写前置及断言，不删除测试绕过失败。页面没有改动不要求无关前端全量回归。

## 证据与判定

已建立本020的[validation.md](validation.md)与`evidence/`，复核时另记新的运行记录，记源码提交/工作区差异、实际命令、通过/失败/跳过数量、Test配置摘要、请求审计及关键日志路径。每场景关联HC/TC与原FR/SC；列正式与工具实际差异。日志须能复核清写水位、各读发起、反馈值和下一请求顺序。

阶段A软件完成需V01–V10适用项目成立，必要构建/测试通过、文档同步及剩余限制有据；不从旧1.1.6成功盘推断新清零已验。真机时序、预算适用性、REAL独立监视表、019硬件故障与整体配方数据贯通仍按原OPEN保留。后续真实部署前另核用户安全值、协议安全语义及正式组件链，不在本指南自动运行设备。


## 阶段B验证指南（软件已验证，现场待执行）

当前[stage-b-plan](stage-b-plan.md)及RC/MC/CP/SP合同的确定软件部分已实施，实际命令、结果和边界见[validation-stage-b](validation-stage-b.md)。分析前置已完成；软件校验保存后可选运行，MB6056对应及MB6058无报警＝0已确认。PLC-Q3剩余等级/清除及PLC-Q4仍限制相关真实运动，不能带未知映射启动设备。

1. 准备明确Test/loopback的完整配方来源和隔离数据根，通过既有DeploymentPrep来源准备入口及正式API新增、校验、保存、GET、编辑、重启GET。不要对现场库运行示例种子。
2. 以同一正文逐字段记录P01–P10输入/校验/SQLite重读/编辑版本/冻结/实际消费者；经StartPublicPreparation→F匹配→计划→执行，不用测试直接造一个已批准Plan替代新增链。
3. 相机fixture证明两次不同设置与两帧、读回失败、来源及真实SQLite/文件保存；真实SDK参数应用另在现场阶段留证。
4. 新混合装配在离线fixture环境核注册/用途/缺依据阻断，不允许fixture的Test值进入Real配置。使用现ICapturePort/IAlgorithmPort及正式保存消费者。
5. PLC用已知现场布局的loopback核REAL型号/F轴集合和安全未知阻断；安全语义待答部分写Blocked。只回归实际受影响的A动作/保存/同坐标用例。

现有相机测试入口（实施后按改动选择，不自动全库扩测；从D:\gaode执行）：

```powershell
dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj --filter "FullyQualifiedName~CameraProtocolTests|FullyQualifiedName~CameraBusinessRegressionTests|FullyQualifiedName~CameraServiceSimulationTests|FullyQualifiedName~CameraWorkerLifecycleTests" --logger "trx;LogFileName=stage-b-camera.trx"
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj
```

B新增RecipeCommissioningChainTests、SiteProtocolAdaptationTests/SiteProtocolTcpFixture、MixedRuntimeCommissioningTests、ControlledCommissioningTests、CommissioningWorkflowTests和既有相机夹具已实际运行。最终55项正式及58项工具结果、筛选命令和三个构建日志单列于`validation-stage-b.md`/`evidence/stage-b/`；A证据不覆盖。P01–P10对账明确未执行分支，不能将其升级为现场全链证据。

现场顺序：先核软件自动配方校验/保存/启动检查与PLC安全答复、用户安全虚拟值、完整配方/型号/机械/预算及唯一PLC写控制端；再七相机角色核对及适用参数验证；最后正式入口单品翻面闭环。未参与该路线的相机独立真实采集单独标记，不添加E/R运动。现指南不给硬件自动启动命令；实际联调执行另按已确认资料和当次授权。

包/安装/回退/GitHub留交付阶段；包须引用最终源码提交及新的现场验证，不能仅复用A离线通过结论。

分析后验证口径补充：T033在隔离环境实际覆盖RealDeviceCommissioning用途的空Approval准入/Freeze，另验旧Test/Production；不能只跑Test用途称新模式已验。T039的SiteProtocolTcpFixture承担现场地址/已知反馈/报警只读与未明安全阻断，ProtocolTcpFixture承担旧Test正常链；T054分别报告两者，缺现场依据的新用途完整正常链仍为Blocked。组件字段/冻结/实际消费结果不能拼成完整Host或现场通过。算法检测/融合均为AlgorithmRole.Detection，依能力和输入数区别。T058同时复用必要失败用例验证T050–T052持久诊断可定位。

2026-10-08分析整改复核完成后已实施确定软件任务；现场依赖和NotRun/Blocked状态保留。下一步可用speckit-converge审查软件证据和剩余任务，不执行硬件或发布。

新联调用途配置入口（不是启动设备命令）：`Gaode:Mode=RealDeviceCommissioning`，显式PublicId/Version、BudgetId/Version、CommissioningId/Version、CommissioningPath及CommissioningSha256；配置用途同名。必须提供真实PLC端点/UnitId/容差/IO期限/Provider=Real、PlcFieldProfilePath、PlcMechanicsPath（包含有来源且用途匹配的PositionBasis、适用型号/姿态/安全位）。Cameras:Enabled及SitePath/WorkerPath/GalaxySdkPath/CameraProSdkPath/StateRoot必须显式配置并涵盖A/B/C/D/E/F/3D，公共3D/F绑定匹配角色或唯一序列号。新独立数据根沿既有TestRoot/AllowedTestRoot字段传入，StoreManifest.Profile=RealDeviceCommissioning，名称不表示Test值可用于真机；RecipeStore独立库仍按现互斥/路径规则。受控算法配置按MC-020及CommissioningConfiguration提供身份/来源/作用范围、公共及预算引用、预期配方摘要、码规则/灯通道、能力和受控结果，预算提供六项执行allowance。缺现场值拒绝，不提供含任意坐标的现场样例，不加载Test SimulationProfile；现Start请求SimulationRef只匹配Host固定联调引用。

## Phase 8复现入口

当前T061–T065完成范围及12项最小回归命令见[validation-phase8](validation-phase8.md)，新证据存evidence/phase8，不覆盖原阶段A/B结果。T061只交接独立前端规格，实际桌面/页面新用途入口尚不可宣称完成；后续范围见[frontend-handoff](frontend-handoff.md)。下一步再次speckit-converge，T055/T056及发布限制保持。
