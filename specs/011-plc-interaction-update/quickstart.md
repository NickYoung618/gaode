# 011 后续最小验证指南

日期：2026-10-03。**本轮只交付指南，没有执行下列命令。** 当前011仍是文档副本；代码、输入和新增能力未实现，不能据此直接构建或宣称链通过。

## 前提与停止边界

1. 调度安排后续tasks/实施，按plan准备完整独立源码副本；不得在主项目临时改代码运行，也不递归复制workcopies。
2. 012最终13份设计已接收并合入主项目；共同合同为[recipe-contract/1.3](contracts/recipe-contract.md)，G-01生产端已补。012新增字段设计消费回执已接收；实施接线前仍需正式代码交付，见tasks-handoff-20261003.md。保存与F读取同一独立SQLite来源，不能回退文件；当前任务见tasks.md，尚未实施。
3. 独立保存的首次构建先完成共同类型的双方直接消费者迁移，详见下节；不等待完整设备/联合链。联合运行前才要求其所用新PLC通信、观察算法、翻转放回及组件就绪。Word/表定义的正式地址、型号承载等未齐时，仅验证有依据的部分；不能为跑完整链编地址/ASCII、反馈或默认姿态。
4. 采用独立Test数据/媒体/数据库与当前版本化配置，已有生产准入保持。配方由012真实保存并重读，不把测试脚本生成的内存对象当保存成功。
5. 按[验证合同M01—M11](contracts/verification.md)在运行前固定真实方法/dataRow及必要项。旧名字/预期需迁移的先承接义务；下列已存在方法仅是可复用入口，不替代新增更多面/E/姿态/保存用例。

## 受影响构建与必要组件

D011-common-code-1.3源码可先交012，Application直接消费者由011基础批迁移；012两适配、目录及Host调用必要早批在收到源码后进行，不等T017或最终联合证据。Infrastructure依赖Application，Host/StorePrep再依赖Infrastructure；所选测试工程全部直接消费者同样须先迁移。首次构建前核项目对应双方迁移已齐，分别记录源码/可构建范围/验证结果。不能排除源码、保旧当前字段或伪造返回消除编译缺口。迁移路径见[tasks-handoff](tasks-handoff-20261003.md)，012本轮4份最终修订已接收合入；实际代码仍待交付。

在已准备完整源码且完成上述迁移的011副本运行；以下是联合验证的受影响构建示例，不是独立保存必须等待VirtualPlc或设备能力的要求。当前缺源码而停止是正确行为。所有命令非零或缺产物如实记录，不继续用旧构建补齐。

```powershell
$work011 = 'E:/dzk/gaode-1/workcopies/011-plc-interaction-update'
Set-Location -LiteralPath $work011
if (-not (Test-Path -LiteralPath "$work011/backend/src/Gaode.Host/Gaode.Host.csproj")) {
    throw '011尚不是完整源码副本'
}
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj
if ($LASTEXITCODE -ne 0) { throw 'Host构建失败' }
dotnet build VirtualPlc/VirtualPlc.csproj
if ($LASTEXITCODE -ne 0) { throw 'VirtualPlc构建失败' }
```

后续只构建实际选用的Contracts/Communication/Rules/Integration测试工程，联合链准备工具沿backend/tools/Gaode.StorePrep/Gaode.StorePrep.csproj；使用当前构建或有当前摘要可核对的未变工具，不重新验证历史迁移矩阵。

已存在且义务仍有效的组件入口示例：

```powershell
dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --filter 'FullyQualifiedName~RecipeRunPlannerTests.SameFrozenInputsCreateStableOrderedPlan|FullyQualifiedName~RecipeExecutionCoordinatorTests.CaptureMustFollowMatchingPositionAndUnknownStepsCannotBeSkipped|FullyQualifiedName~RecipeSortingMapperTests.OkIsNotDispatchedWhileNgAndPendingKeepDistinctFormalActions|FullyQualifiedName~FScanStepTests.FailedAlgorithmFactSaveDoesNotSubmitFCompletion' --logger 'trx;LogFileName=011-components.trx'
if ($LASTEXITCODE -ne 0) { throw '必要组件失败；保留原报告' }
```

这四方法只证明各自义务，不能代替M02—M05/M08/M09新增或迁移项。更多面、E开/关、首次/复查异常、真实保存/唯一匹配/冻结隔离与新软件绑定回执用例需在实施时落到真实方法并加入事先固定过滤清单；本轮不编造尚不存在的方法名。每项实际发现和执行数量与固定清单对账，零发现或Skip不得通过。

架构使用既有检查器和同入口正负例；小规模架构测试可按以下三个受影响类运行，并核当前正式源码扫描结果，不执行009/010全部历史治理专项：

```powershell
dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj --filter 'FullyQualifiedName~ProtocolRepositoryBoundaryTests|FullyQualifiedName~ProtocolBoundaryTests|FullyQualifiedName~RecipeExecutionBoundaryTests' --logger 'trx;LogFileName=011-boundaries.trx'
if ($LASTEXITCODE -ne 0) { throw '架构门禁失败；不得扩大豁免' }
```

通信用例按M02/M09的受影响方法/dataRow单独选择，独立预期对照当前来源；业务用例不读取原码。“没有正式映射”是相关互通未验证，不能因组件替身返回成功勾Passed。

## 联合代表链操作

T025只准备唯一驱动与输入，T026及双方必要清理不等待联合成功。T027启动前核D011-common-code-1.3、D011-runtime-binding-1.3、D011-runtime-state-1.0及012 T017真实存储、T019适用迁移、T020运行接线、T021页面/API代码和实际输入齐备。由011统一启动下面单面/多面各一条，012 T022同期采证；D012-ui-joint-evidence运行后产生，双方再据此核完成，不将其列为启动前置。

使用实际012入口/API保存合法完整配方A，读取其RecipeId/Version/DefinitionDigest/FCode；明确F输入就等于该料盘编号。输入、配置预算和可用动作映射都有实际来源后：

1. 单面链经正式启动，首次3D实际提供F XY；扫码唯一匹配实际保存内容；软件冻结/绑定提交成功后检测。验证检测XYZ取配置，适用分拣后下料、允许取盘、实际人工确认及Final保存。
2. 保存修改A为新内容B。已经冻结A的运行继续A；下一次F读取B。保两份真实读取/保存回执和运行快照，不在内存更换快照模拟结果；选择展示版本旧不应强制F用A。
3. 多面链复用同一正式驱动，使用合法更多动作代表；每轮相关对象完成真实翻转放回后统一3D复查，F不重绑。代表中合并无料/异常槽及实际需搬运对象，或用必要组件补足容量无法同盘覆盖的差异。
4. 需要的四面/E配置差异及更多面代表用必要组件补齐；实际执行必须触及动作/采集/保存，不能仅打印计划。OK不分拣搬运、NG/Pending到本盘各自目标，异常留槽且后续对象动作数为零。
5. 查看后端同run状态/媒体/保存与012已有界面。实际阶段、异常物理槽、面/PoseId、质量/技术/物理状态及Final一致；未执行页面只报告后端范围。

现有正式驱动是RecipeExecution010RunHarness，已实际具备独立Host/VirtualPlc/Worker、StorePrep、HTTP启动/人工结束能力。复用它时定向移除旧配方ACK/码映射/TestSpecialActions配置消费者，并按新合同更新输入及期望；不启动整个010 profile，也不写业务状态/完成信号。

现存完整链入口在迁移后可继续使用下面命令。环境变量名和既有允许根只是驱动接线，不构成010重验；本次运行使用新目录，不能覆盖旧包。joint-single.json是实施阶段从实际保存结果和已核配置生成的本次驱动输入，本轮没有创建它。

```powershell
$fixture011 = "$work011/artifacts/plc-interaction-011/inputs/joint-single.json"
if (-not (Test-Path -LiteralPath $fixture011)) { throw '缺本次已核联合输入' }
if (-not $env:GAODE_010_PYTHON) { throw '必须指定已核对的实际Python解释器' }
$env:GAODE_010_FIXTURE = $fixture011
$env:GAODE_010_FULLRUN_ROOT = "$work011/artifacts/recipe-execution-010/011-single-$([guid]::NewGuid().ToString('N'))"
dotnet test backend/tests/Gaode.Integration.Tests/Gaode.Integration.Tests.csproj --filter 'FullyQualifiedName=Gaode.Integration.Tests.Station01.ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite' --logger 'trx;LogFileName=011-single-chain.trx'
if ($LASTEXITCODE -ne 0) { throw '代表链失败；保留全部当前证据' }
```

现010Expectations写有单面Q02特定预期，不能仅换fixture就声称多面可运行。多面代表须在同一驱动上增加本次独立预期与正式调用行，依M07固定方法/dataRow后执行一次；不重建整段驱动或重复单面相同义务。型号承载尚未具备时多面动作标Blocked，不以旧HTTP旁路代替。

## 失败、清理与收口

必要失败复用当前保存回执、取消、期限和未知反馈用例，按M09迁移旧绑定容量写注入；实际取料保存失败不得放料。保存失败不能只模拟HTTP错误响应，必须在真实持久路径证明未返回成功及实际后继约束。缺陷Pending与姿态未知分别处理，失败日志定位到具体Run/动作/调用/保存。

实施替代后核plan删除表的真实调用/装配/配置/脚本消费者，实际删除无用途旧分支；已由010删除者只核实。保留历史reader必要字段与原失败证据，不用永久关停、兼容层或删除正确断言消除失败。

最终记录M01—M11的Passed/Failed/Blocked/NotRun/Skipped实际状态、命令、发现/执行数量及原始证据引用；同一run证据供011/012共用。仅受影响项需要本次新证据，未改历史事实保持原范围。无全量测试、面数/配方穷举或009/010全专项门槛。

本轮未运行以上任何验证；设计完成后停止，等待调度。


## 本轮接收后的证据对齐（仅指南）

M08与012 QV-01/02/03共用同批保存、完整重读、正常重启重读及联合run记录；增加服务器RecipeId/Version、011唯一DefinitionDigest与读取ETag→ExpectedVersion映射、过期更新拒绝的必要证明。内部SaveId只关联存储，不替代这些字段。M10接012 QV-04的新保存可达路径/架构正负例，M11接QV-05消费者清理；不新增第三条完整链。012配方库准备工具若受影响则共用其构建证据，不重做009运行库升级验收。所有本轮软件项仍NotRun。

当前实施承接（非历史阶段描述）：T002最小集合/具名待交和局部来源已登记，逐项class/method/dataRow及数量见implementation-verification-map当前表；T003—T005源码/直接消费者已随共同001—004交付，唯一序列化/校验/匹配/深冻结11项与实际存储API9项、冻结reader6项及当前完整Contracts构建有据，具备各自完成判据。后续新增机械配置/Host入口依赖归T025/T026，不反向把基础交付说成运行能力。当前联合输入/驱动部分准备，T025/T027仍未勾；旧时点NotRun、仅设计或未授权说明按其历史范围读取。
