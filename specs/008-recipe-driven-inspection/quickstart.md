> 2026-09-26实施前置：下方命令为后续运行指南，本轮未执行。先按[新版实施清单](implementation-checklist-20260926.md)同步代码/配置/生成器，再运行新版Q03；当前磁盘fixture仍旧版，禁止直接用其宣称新协议通过。

# 后续验证操作指南（目标，全部未运行）

日期2026-09-24。本轮只做设计，未构建、启动服务、执行业务测试或迁移。
前提：对应阶段代码与任务完成；首条路线需实现并验证已定义的B01-F合同、适用B04及B09-H/U，后续按实际分支补依赖。必要同页控件已获修改授权但未实现；缺项记录Blocked，不能绕过F或页面操作。

## 1. 先明确现有入口和待扩展参数

现有scripts/start-station01-virtual-loop.ps1启动Host、独立VirtualPlc及WPF，Host管理独立worker；虚拟相机是正式采集适配，不需要另建相机服务。
当前脚本固定007配置和媒体/worker清单，TestRoot只允许artifacts/station01-007；simulate-station01-load.ps1只接受S1/P01。原样运行只能说明旧样本，不验证008。

007后续最小脚本合同：
- start-station01-virtual-loop.ps1增加-FixtureManifest绝对路径，加载该用例的配置根、公共/预算/模拟引用、Review配方目录、媒体/worker清单以及purpose=Test；008运行根在artifacts/recipe-execution-008下，仍检查目录边界和独立端口。
- 目录使用现有Recipes:Provider=Review及Recipes:CatalogPath；保留模拟用途，不能改成生产File绕过校验。
- simulate-station01-load.ps1在-PrepareOnly下按同一-FixtureManifest准备合法料盘/占用/媒体输入；解除固定S1/P01限制，仍不启动业务、不制造F或完成反馈。
- 前端从正式catalog API加载并由操作员选配方，PrepareOnly数据不能取代此操作。008不启用外部watch脚本代办需要前端确认的步骤。
- manifest由后续实现生成，至少含caseId、purpose、recipeRef、scenario/occupiedSlots、public/budget/simulation引用、目录/媒体/worker文件及摘要。不存在该文件时不得继续声称可运行。

上述参数是**待实现合同**，当前不能直接执行成功；不新建另一套测试平台。

## 2. 已存在的构建入口（本轮未执行）

在仓库根操作；只构建/测试当前改动涉及的部分，不要求每次全回归。

~~~powershell
Set-Location 'E:/dzk/gaode-1'
dotnet build 'backend/src/Gaode.Host/Gaode.Host.csproj'
dotnet build 'VirtualPlc/VirtualPlc.csproj'
npm --prefix 'frontend' run build
dotnet build 'desktop/Gaode.Station01.Desktop.csproj'
~~~

规则、合同或集成测试使用已有backend/tests对应工程的定向筛选；测试名称在后续tasks/实现中落实，不在设计阶段编造已存在的新测试。
前端真实构建使用src/runtime.js，修改辅助TS模块后必须核实已接入运行入口。

## 3. 参数扩展实现后的启动示例（当前不可作为运行指令）

以下采用未来生成的绝对manifest路径；确认文件/参数实际存在后才执行。令牌通过已有受控环境配置提供，不写入证据。

~~~powershell
pwsh -NoProfile -File 'E:/dzk/gaode-1/scripts/start-station01-virtual-loop.ps1' -FixtureManifest 'E:/dzk/gaode-1/artifacts/recipe-execution-008/prepared/Q01/fixture.json' -TestRoot 'E:/dzk/gaode-1/artifacts/recipe-execution-008/Q01/runtime-01' -ApiBase 'http://127.0.0.1:5101' -PlcApiBase 'http://127.0.0.1:5180' -PlcPort 1512
pwsh -NoProfile -File 'E:/dzk/gaode-1/scripts/simulate-station01-load.ps1' -PrepareOnly -FixtureManifest 'E:/dzk/gaode-1/artifacts/recipe-execution-008/prepared/Q01/fixture.json' -OutputDirectory 'E:/dzk/gaode-1/artifacts/recipe-execution-008/Q01/runtime-01/prepared'
~~~

端口是隔离Test示例，执行前确认空闲；不终止其他会话进程。新Test库使用现有StorePrep及维护迁移，正常Host不改schema；不复用活动运行库。
脚本必须把所有用例配置实际传给Host/worker并记录摘要，不能只把caseId写到日志。

## 4. 前端实际操作与核对

1. 打开现有WPF页面并核对Host/设备来源与可靠就绪；无可靠心跳即记录受阻，不靠反复启动碰成功。
2. 在正式页面配方入口加载后端条目，选择R008-Q01对应版本；核对只读ID/顶部版本。必要的选择、确认和结果位置按006已授权的最小原型增量实现后使用。
3. 从原型正式启动控件提交一次请求，保存页面/请求/202及runId；202只显示已受理。
4. 观察公共3D/F、唯一配方绑定与前端选择一致，实际定位/AB批采/单图分析/同面融合/保存/复位。
5. 观察该路线适用处置及下料、解锁、取盘确认、Final。所有需要人工确认的步骤经批准前端入口；若只有旧外部Test确认方式，记录该页面覆盖受阻。
6. 从页面核对进度、结果和来源；通过正式GET与只读SQLite/媒体进一步核对同run事实，生成contracts/evidence.md约定的最小包。
7. 更换配方执行Q02及后续序列；多面必须实际换面及ACK清零、逐面目标续接。若合法差异用例已覆盖某Q，在矩阵登记该完整run，不重复跑无风险相同覆盖。
8. C08以同程序摘要使用新配方/版本运行，记录变化作用于实际执行及旧快照不变；不要求前端编辑。
9. 只补覆盖矩阵列出的必要F失败及直接改动风险；适用人工恢复与未知动作不自动重放。

## 5. 完成与退出

逐项填Q/C/F及实际配方、对象、run、提交、页面操作和证据路径。008 SC-001当前选定路线、C01—C08实质差异和必要失败均有证据才可作本期整体结论。
配方加载、规划展开、组件测试或后台辅助样本均不抵扣前端完整通过。
结束时只清理本次manifest登记且身份匹配的进程；保留数据库、媒体、原始日志及失败包，退出后核验可定位性。
虚拟通过只标SoftwareLoopOnly，不宣称真机、精度或生产节拍通过。

实施前按[派生时序](sequences.md)逐步核对Q03及分拣子时序；[本轮来源与保护检查](sequence-alignment-20260926.md)仅为文档验证，不是运行通过。执行步骤、实际能力和必要证据仍按本文与实施清单要求。

## USR-20260926-D最少验证指南（设计待实施，今日不执行）

既有普通Q及Test启动方式仅用于原已实现路线，不能验证新恢复。后续先按[计划交接](plan-restart-alignment-20260926.md)调整现有任务，完成003/001/008/006消费者再运行。使用独立新证据根，不覆盖旧包，同机PLC/worker/页面串行。

1. 准备既有版本化Test普通OK配置、媒体及独立worker，记录本次Host/PLC/页面构建与配置摘要；从正式页面实际选配方/启动。
2. 由VirtualPlc受控故障注入形成旧轮真实故障；保存旧run、媒体与持久结果。先验证初始不足时新启动拒绝且无后继动作。
3. 在既有页面区域授权双端复位与初始核对，再通过既有启动控件显式POST /runs（新requestId、restartFrom，设计新增字段），实际完成新公共3D/F、绑定、全部检测、保存、解锁、页面取盘及Final。
4. 按[003 C07/F5合同](../003-plc-latest-protocol/contracts/recovery-test-execution.md#5-最少必要c07f5验证待实施)读回旧新身份、初始门禁、隔离及历史可查；对照暂停/manual继续不重采公共3D。旧单指令测试不抵新通过。
5. 只回归受实际改动影响的普通/多面及特殊代表；旧Q有效包按构建配置比较后复用，任何未执行项保持NotRun。

新恢复尚无已实现可执行脚本，本指南不把旧SingleCommandRecoveryIntegrationTests命令包装为新恢复验收命令。

## USR-E后续验证准备（未运行）

先按[交接](plan-six-issues-alignment-20260926.md)核对进程/构建/配置/协议，升级当前集合生成器后才生成新Test包。先扫码/AB-CD第二点/下料/取放/同值证据，再按影响验证四面代表及必要失败，后接USR-D正式页面新轮Final。设备采证串行，不放宽1秒I/O和3秒心跳、不自动重跑全部Q。
