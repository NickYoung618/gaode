# 022最小验证指南

状态：后续实现后使用，本轮未运行。仅隔离工作树和新建临时媒体/SQLite副本；不启动Host、不连接PLC/相机或发送命令。真算法需资料交付及相应执行授权，离线不替代现场。

## 最新A/B/C验证顺序

A先核实际Host算法配置读取链/非法字段及缺组件拒绝（T010），PNG像素/PLY XYZ及原-产物来源SQLite重读（T016），I1阶段终态后释放（T007），实际Host终态Run资源扫描/停止/原释放截止不重开（T027），原同步链两Run/融合/冻结/3D-F-E-分拣/Final屏障及直接受影响回归（T024/T028/T029/T032/T033/T035）。复用各前置断言，不重复异常矩阵。

B交付后才核同RealDeviceCommissioning用途真实程序/模型/标定/预期样本与实际Ready及结果落库/释放（T011–T014）；Test不能代替。C的AV-01重叠、小队列/批采配对及对象级增量分拣保留延期，T017不启用，不作为A通行证。R1可在Test DI中调用实际持久/停止入口，但不Start设备HostedService或运行硬件Host。当前本轮不执行任何验证程序。

## 前置与入口

使用仓库SDK10.0.401及锁定依赖，不安装升级替代工具。先完成[plan](plan.md)A必要子项与契约，C重叠S3不作基本前置；测试装配只用明确Test端口/算法替身，经正式业务链保存真实临时文件和SQLite，不使用安装目录/原业务库，不配置真实设备端点。生产不能按测试名/GUID/fixture/env改变行为。

以下是未来验证命令示例，本轮未执行。过滤器使用实际既有受影响类；新增022测试全名在tasks/实现后明确，不能把尚不存在的测试写作已可运行证据。

~~~powershell
Set-Location 'D:\gaode-022-real-algorithm-pipeline'
dotnet --version
dotnet restore backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj --locked-mode
dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj --no-restore --filter 'FullyQualifiedName~RecipeCommissioningChainTests|FullyQualifiedName~AcquisitionBusinessRegressionTests' --logger 'trx;LogFileName=022-directed.trx' --results-directory 'C:\Temp\gaode-022-validation'
~~~

现测试入口不能证明新增管线覆盖；后续在同项目补必要022场景，按实际类名定向运行。SDK/锁文件失败保诊断，不临时换版本迎合命令。

## 最小场景

| 集合 | 操作与断言 | 证据 |
| --- | --- | --- |
| AV-01/U2-1/3 | 控制首缺陷结果未返回；下一采集/原序翻放/特殊本件旋转确派发；保3D复查/E/安全 | 保存及窗口结束<后续步骤<前Result；文件摘要/SQLite重读 |
| AV-02/U3-1/2/4 | 缺3D/F不派动作；E绑定保存前不继续；本件缺判定不分拣、齐备不等无关计算；特殊分拣安全后下一件 | 必检集合、判定/预约/取放偏序及调用记录 |
| AV-03/U2-2/U4-1 | 两Run同名对象，同面两对象AB/CD乱序，旧/重复/迟到 | 完整Capture/Media/Call/面键，两轮重读，二次融合/动作0 |
| AV-04/U4-2/3/4 | Test小C/Q/输入额度，A批后B配图；一代表超时随后Result/释放；正常暂停 | 实际峰值、无自锁、终态不覆、未知不早回收；暂停不采不动/期限不重置 |
| AV-05/U5-1 | 在途旧轮保存新配方/参数，新轮启动 | 旧轮全调用/融合旧绑定，新轮新绑定 |
| AV-06/U5-2/U3-3 | 定向采集释放、完整周期、质量/分拣安全及收尾 | 原复位/轴/请求释放/同坐标，资源未完无Final，未知保存不派发 |

只核受影响原测试及必要新增断言，不扩大完整异常矩阵。共享目标/容量确受影响时补一项准入证明验证。Test替身不证明真算法/标定/生产预算/硬件事实。

2026-10-09审查修正对应的最小增量（仍未执行）：T007保存业务阶段终态后追加释放，重读/重放及Operation恢复保持业务身份；T027在同一异常/关闭夹具调用实际隔离Test的InitializePersistenceAsync、停止通知及StopAsync消费者，Cancelled/Failed且资源Unknown仍可查，不启动设备Host、不续算；同一批采/超时用例记录三类额度及释放Start/Due不重置。T026/T027/T031各自先在未启用Test受管组件完成身份/控制/冻结最小前置验收，T017启用前检查这些证据，后续完整链场景保持。

T010直接核原两种Test模式、原模拟联调用途与Production拒绝、用途/Profile/根不一致拒绝以及未知Real局部受限。真实算法在联调用途正向装配用途已由用户确认，仍须C022-ALG实际交付/Ready及A必要实证；Production不列成功场景，不操作现场库、不自动迁移。规则校验与真实算法专项分开记录。

## 真算法专项

SC-007当前Blocked。DEP-ALG/FMT/CAL/RULE/VER/CAP齐备后再定实际提供者命令和合法真实输入；此处不编虚构真算法脚本。记录程序/模型/参数/标定身份、实际输入/结果落库、可靠释放/执行结束、连续调用常驻及冷/稳态资源。020 T055/T056仍现场未验证。

## 记录与回退

后续每轮记录源码提交或未提交差异摘要、冻结配置/配方、来源Test/虚拟/Real、路线顺序、C/Q/媒体/预算、文件/SQLite副本摘要、偏序/日志/TRX及诊断；在本功能validation.md索引实际记录，本轮不创建假通过报告。

未知资源保文件/登记和副本，按plan分阶段回匹配源码/配置/库，不清现场/降级新库/自动重发。必要结果、释放及保存可靠后结束运行；未执行/失败/Blocked分别记录，旧局部证据不冒充当前完整通过。



## A已执行证据与安全复验边界

实际命令/38个必要用例分批结果与失败修正见validation.md；最终Host三情境仅Test/环回端口/独立存储。复验时显式将GAODE_022_EVIDENCE_ROOT、GAODE_COMMISSIONING_EVIDENCE_ROOT、GAODE_STAGE_A_EVIDENCE指向新的022证据目录（只由测试导出代码读取），选本次直接相关过滤器并保TRX，不沿用旧SQLite。不得执行真实设备HostedService Start或Production，不连接现场，不将真实选中NotIntegrated改为Ready。T017不启用。

## 2026-10-10下一轮最小接入与验收顺序
以下仅后续指南，本轮没有运行这些动作。当前包已交付，但无已验证Ready；旧指南“尚未交付”是历史。任何现场Host/PLC/相机或动作仍需对应执行授权；本轮文档不提供该授权。
1. T011-S1：核独立CPython环境/依赖与GPU、真实模块/权重/入口、项目侧媒体根及模型参数配置；先不连接设备，逐模块Ready记录，不强求无关模块或精度就绪。
2. T012-S1/T016-S1/T031-S1/T047-S1：真实桥和端口/所需装配、使用同Run已提交实际采集文件及派生PNG、版本/身份冻结、完整native回复及失败保存；不借固定样例或注册Test为真Ready。首切片为已确认配方检测点的一台对应相机→DefectSingle。
3. T013-S1：实际调用交付模型，核参数应用/输入摘要、SQLite新读取、原始Pending/错误及可靠CallEnded/输入释放；组件通过不提交整盘Final。T046另核原100ms及可控真正入场后超时，保留原失败，不放宽预算。
4. 021:T083绑定既有页面；T048启用经共同校验和冻结的阶段路线，不把NG改OK。需要3D/F依据的动作没有独立确认配置就阻断。
5. T049按实际配方运动顺序验证普通/特殊等必要主链和原Final；单点证据不得替代全部流程。T050另行完成最终ResultDriven规则/标定/切换，T017/C不作为前置。

验收只选必要正常主链和直接关系到事实/释放/安全的代表失败；未知资源/保存未知/PLC互锁必须阻断，真实Pending照存，不用“忽略所有错误”。记录Run/Call/输入/原释放UTC与tick期限及来源。当前activeExpired=true缺口见evidence/t045-local-merge-20261009/merge-validation.json，不覆盖或删除。

当前交付缺陷仅Pending，不能为证明NG路线制造真实NG。阶段软件可用明确Test合同结果验证NG不改目标（不注册真Ready、不计真实算法验收）；实际包验收以本次真实Pending/错误为证据，待真包及确认规则能够产出NG后再补该真实分支。页面通知只触发正式重读，不新增浏览器回执作为运动门禁。
