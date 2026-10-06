# 后续最小验证指南

> 当前验收解释：2026-10-05需求方批准 **013-acceptance/2**，详见[验证合同V06.2](contracts/verification.md#acceptance-v2)。普通25ms等新增工程目标改为非阻断观察；原期限/保护、局部首态75ms、确认周期、单源/计划、预算/净收益及证据完整性仍硬。原数字和历史失败保留。下文历史“全部V06成立/不得倒改门槛”以本次显式批准范围解释，不能据此修改硬条件。


**013 / Phase 1设计 / 2026-10-04**。本轮未执行本指南。以下步骤只供后续设计审查、实施及验证获准后使用；当前没有可直接宣称可用的013验证profile，不应现在运行它。完整冻结标准见[验证合同](contracts/verification.md)。

## 1. 先满足运行前提

先完成[plan.md](plan.md)中实际受影响的SY-01—07及DQ-02命中的001预算合同/准入消费者文档对齐，再修改共享代码；不改历史任务完成事实。保留未经产品改动的主项目源码基线/摘要，分别构建基线和改后所需项目并记录产物身份；不能从旧workcopies拿产品覆盖主项目。

后续实现须先提供以下最小接线，才可以使用本指南命令：
- 明确PlcPolling013 runner分支和对应必需manifest；不能落回默认009。
- 保持完整010 L外层包装，复用发现/执行/原报告核对，支持引用同attempt已执行结果以避免重复。
- 相同中性计量补丁、真正连接后端的2秒API观察器与代表链驱动，两侧完全一致；不能生成假page-ready文件。013后端观察不能冒充011/012页面验收。
- 在产品改动前取得可重现的基线源目录；保存独立完整预期、依赖输入、原期限、模拟持续时间及原始输出，包含StorePrep完整源码/项目/实际锁文件和backend/Directory.Packages.props等实际构建导入。首次修改前核项目引用/链接/复制资源/运行工具闭包；按清单排除产物及R自身，B从自身源码构建且使用自己的StorePrep/Host/VirtualPlc/测试DLL。
- 现有.NET/Python、VirtualPlc、采集/算法进程及隔离测试存储已具备。仅用新建隔离验证根，不操作现场或正在运行的业务数据库。

有缺失时将对应运行标NotRun/受限并说明，不能绕过保存、设备/算法或Final。预算和时效在首次对比前冻结，不根据测量重新调门槛。

## 2. 冻结输入与清单

采用主项目现specs/011-plc-interaction-update/examples/joint/run-2.json及其真实引用，清单列出解析后的配方catalog、机械配置、算法/采集输入、所有摘要。Motion500/Flip3000/PutBack150/Sorting3000ms、scan10ms、心跳1000ms、jitter0，两侧一致。

两侧配置和引用必须按V02.1装配，不能拿同一主目录schema同时加载两个产品：

| 项 | before | after |
| --- | --- | --- |
| fixture / configRoot | R/inputs/before/run-2.json；相对config | R/inputs/after/run-2.json；相对config |
| 实际ConfigRoot / SchemaRoot | R/inputs/before/config；B/specs/001-station01-public-preparation/contracts | R/inputs/after/config；A/specs/001-station01-public-preparation/contracts |
| budget.json / budget schema | s01-budget-011-joint/1；schema1.1；plcPoll=50 | s01-budget-011-joint/**2**；schema**2.0**；删除plcPoll |
| simulation.json / budgetRef | s01-sim-011-joint/1；budgetRef /1 | s01-sim-011-joint/**2**；budgetRef /**2**；simulation schema仍1.0 |
| run-2引用及Host/Start请求 | budgetRef /1，simulationRef /1 | budgetRef /**2**，simulationRef /**2** |
| public配置/引用 | s01-public-011-joint/1，schema1.0 | 同值同摘要；public/simulation schema内容不变 |
| 通信采集装配 | 原产品与Test50设置，保持未优化 | 通信内部plc-acquisition/013-1不可变策略，由Station01Registration装配；记录冻结摘要，业务预算不承载周期 |

B/A/R分别为下述三个GAODE_013_*根，实际绝对值在执行前冻结。先由B中原011 joint依赖树复制两套隔离输入；after仅按V02.1改预算schemaVersion/version/plcPoll、模拟version/budgetRef及fixture两引用，禁止修改before适配新schema。所有媒体/机械/配置相对引用须在fixture自己的目录树，worker按各侧源码根且摘要相同；各侧用自己的测试DLL和产品DLL，不能只换cwd。旧input-manifest仅作来源，重新记录两侧原文件摘要、预算/模拟规范化摘要、SnapshotId及批准字段差异；这些摘要允许不同，原配方/机械/算法/媒体、模拟时序、业务预算/资源限额及线程池/GC/优先级/2秒观察负载保持语义一致。中性计量补丁摘要单列，不包含013策略或配置迁移。

冻结必需项：
- 基线与改后各5秒预热、一次60秒空闲、一次相同完整run-2；
- V07保护组件和N1—N3数据行；I-FU保持正常降频HeldFlip原期限/故障下真实失败保存，未达1024不要求段；I-SEG独立真实TCP1023/1024自动阈值组件走生产记录/SQLite提交和后续失败接续，I-GAP原始缺口拒绝独立保留，不增加完整链；
- 完整010 L（现70行）、受影响009边界/映射；
- V05静态21准入入口、21轴批/33轴启动、108核心业务写及完整业务预期；
- V04/V05公式与预算、V06时效/4950ms回归容许量。
零发现/漏执行不得缩小清单后通过。

拟议013 runner的测试接线变量（后续才实现，**不是当前产品配置**）：
GAODE_013_BASELINE_ROOT为冻结基线根；GAODE_013_AFTER_ROOT为当前优化源码根；GAODE_013_ATTEMPT_ROOT为新建验证产物根。具体Windows绝对路径在运行前写入manifest。原harness使用的GAODE_011_FIXTURE、GAODE_011_FULLRUN_ROOT、GAODE_011_PYTHON继续由013 runner按每侧隔离根设置；旧GAODE_011_PAGE_EVIDENCE_ROOT不允许指向缺失观察器后假装已满足。

## 3. 后续运行入口

在上述分支、清单及中性观察接线实现后，从主项目运行一次统一入口；runner负责使用冻结基线和改后、必要构建、两侧计量、必要组件及完整L。以下只是该**拟议入口**的调用方式，当前未实现/未运行：

```powershell
Set-Location 'E:/dzk/gaode-1'
$env:SPECIFY_FEATURE_DIRECTORY = 'specs/013-plc-polling-optimization'
# 先按已冻结manifest设置三个GAODE_013_*测试路径。
pwsh -NoProfile -File 'E:/dzk/gaode-1/scripts/verify.ps1' -Profile PlcPolling013 -WaitSeconds 600
```

若入口返回运行中，沿既有attempt轮询/收集机制等待其结果；600秒是命令等待参数，不是任何设备/业务期限配置。不能因命令返回就把未结束用例算通过。若profile未识别或仍选到009，立即报告接线未完成，不能把009结果当013。

代表链仍使用既有测试方法：
Gaode.Integration.Tests.Station01.ThreeStageMainFlowIntegrationTests.CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite。
runner以该单方法/既有harness驱动两侧各一次；不要同时再手工跑一次完整链。仅定位已证实失败需要时补跑受影响项，保留原失败产物。

## 4. 对照验收

核对artifact根下两侧源码/构建/输入原始摘要、configuration-map、计量补丁摘要、实际请求/完整阶段时间、原日志/TRX及真实持久引用。采用与V01.1相同的适用矩阵：

| 适用类别 | 判据 | 实际引用 |
| --- | --- | --- |
| A 两侧共同资格 | 相同run-2路线/完成数，真实VirtualPlc、采集/算法、必要保存和Final；原期限、取消/未知及保存保护；输入语义可比、计量有效、原始证据完整 | before、after各自一次完整空闲和代表链。21轴批/33轴启动/108核心写只审计这条冻结路线/协议布局，不成为业务常量 |
| B 仅after | 新周期和V06.2硬时效（局部首态75ms/原保护）；普通工程目标单列观察、完整60秒空闲≤1110、活动周期/按需预算、单源、无重复缓存等待、循环零准备、新观察语义及013新增门禁 | before只报告实际数值，允许存在原高频/重复源；不让旧产品先达到013要求 |
| C 跨侧比较 | 空闲率下降、同一完整流程总事务严格下降；按种类报告每完成单位事务及新增必要即时读；SC-006四项直接负担/证据量变化；T_after≤T_before+4950ms | A合格且B满足才宣布013成功；固定必要读写无需每项下降，未完成动作不能算收益 |

下列工程目标仅观察after固定正常Test负载（依V06.2，超限单独不否决）：单PDU发送→响应≤25ms，周期到期→首发送≤25ms，完整首发送→发布B150/P50/F-U-H25/T75ms，必须计入块间排队和发布；它们是独立联合目标，不是块数乘25的保证。首态激活→首次可靠发布≤75ms、随后“下一发布−上一采样开始”≤75ms，含竞争/交换/发布（不要求首读已是Moving/Executing）；设备中间态仍须在原截止内真实观察。必要写资格→发送≤50ms，即时单块/P/B资格→可消费≤75/100/200ms。其余状态年龄、两类心跳延迟及最旧依赖口径逐项采用V06，原安全新鲜度/I/O/3秒/动作/保存期限两侧均不放宽。before按相同边界计量，不强套这些新增门槛；故意慢响应组件仅检原保护。

T024在执行L前实际登记009-boundary-inventory.json中013新增合同、C#、Python及相关直接调用/helper，按真实职责和扫描要求分类；不得关闭CHECKER-INVENTORY/A10、扩大排除或新增豁免。完整010 L、受影响009及V07/三负例在**最终after源码+补丁+对应输入/构建**身份下执行，按V09.1发现→执行→原报告核对；同源码同attempt已完成的同一门禁只引用一次。before一窗一链是该attempt的基线测量子项，不再复制完整L，也不能用旧基线或旧attempt的L代替after。N1实际重复PDU、N2旧缓存后继拒绝、N3删必需结果均须被原门禁拒绝；不得只信自报统计。

结果与V10一致，至少分别列出：

- **业务/原保护失败**：任一侧原截止、必要反馈/坐标、取消/未知或真实保存门失败，漏步骤/Final；保留原失败，不算成功分母。
- **优化目标不满足**：after确认策略/硬时效/预算、单源等不满足，或净收益/4950ms失败；普通工程数字超限另记性能观察，不纳入此硬失败；不能因请求少而通过。
- **环境/输入不具可比性**：指出实际输入、身份、外部负载或计量差异证据；不能只因25/75超限就归此类，SC不得宣布通过。
- **NotRun**：能力未执行或具体前提使该项未能开始；不能把已执行失败改成NotRun或免除必需项。
- **正式设备信息不足，NotMeasurable**：缺正式地址、心跳周期、中间态最短保持/锁存或设备时基，只限制对应真机预算/捕获/端到端结论。

保留原整窗/整链，只在已定位原因确有必要时补相应侧/项；不循环跑链择优、不挑片段、不改模拟时序/门槛/期限。不可比不自动引出线程池/GC/优先级或长期压力治理。软件对照成功也不等于正式PLC、整机或生产验收。

## 本轮停止状态

技术研究和Phase 1文档已交付；需求质量CHK015保持NotRun。本轮不执行上述命令，不生成tasks，不实施或构建，不启动/连接设备，不操作运行数据库。等待设计审查。

2026-10-04分析11补验顺序：固定前置包含原I-DIAG-01/02/03和新增I-TIME-02/03（原I/O绝对截止），须传既有GAODE_013_MEASUREMENT_ROOT；同一冻结context原报告只引用一次。随后当前完整L70及其余48行，合格53行后N3，再关闭额外内核/运行时采集执行原60秒空闲+run-2一次。前置结果不可代替完整L，旧08/11未修复身份不能冒充修复后通过。
