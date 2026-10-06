# 011/012主项目软件集成收口（2026-10-04）

011/012已实现、已验证的软件成果已实际集成 `E:/dzk/gaode-1`，本轮必要集成验证通过。该结论仅覆盖软件/Test范围，不代表正式互通、现场批准或生产部署。

## 最终交接与任务

012固定005 manifest `e637bc1844782fa41fd5b97e750a68703039d61dc21cb5bd9209cbef6c7925d4` 的4文档、5证据已逐项核验；共同006源码身份、实际接收和004此前合入事实一致，005没有产品代码变化。四文档为012 plan-handoff.md、tasks.md、quickstart.md、contracts/shared-integration.md，已按差异合主项目，保留历史及原编号/勾选。T028原其他完成条件已具备，仅按实际共同006回执勾选，01126/28；T009/T010未完成。01225/25是其软件任务范围。未制造005代码变化或新的往返交接要求。

architecture仍0/15，requirements仍14/16；清单文本与勾选未改。已有实施授权允许推进，清单评价不替代软件验证。

## 实际集成与恢复

唯一产品来源：`E:/dzk/gaode-1/workcopies/011-plc-interaction-update`；目标：`E:/dzk/gaode-1`。新增68、修改194、删除20，产品/测试/脚本/必要静态资源计282项，文档同步及构建输出另计。准确逐文件清单与前后SHA：[main-integration-20261004/applied-mutations.json](E:/dzk/gaode-1/workcopies/011-plc-interaction-update/artifacts/011-plc-interaction-update/main-integration-20261004/applied-mutations.json)；恢复与原文件备份：[main-integration-20261004/recovery-manifest.json](E:/dzk/gaode-1/workcopies/011-plc-interaction-update/artifacts/011-plc-interaction-update/main-integration-20261004/recovery-manifest.json)。

依据原693文件基线逐文件三方比较，原已有文件主项目内容仍等于基线，没有需要合并的实质并发冲突；不整目录覆盖。012来源证明不足的4项已按固定批接收记录补核，未复制012在制源码。额外静态范围为18项Test联合输入与2项schema。schema不在原693文件基线中，已逐节点合并确认的新观察角色/算法与动作预算差异，其他当前节点语义保持；未虚构其原基线。全部变更执行前记录范围并保存可恢复原文件，执行前再次核主项目摘要，执行后全部目标摘要与最终来源一致。

必要配置只移除失效Review来源和旧动作键；保留的全部字段值与原主项目一致。joint目录是新增Test输入，未覆盖现场实例。未复制bin/obj、node_modules、运行库、日志、密钥或工作副本。主项目feature.json、实例/运行库、来源、客户原型和历史失败证据不在变更清单中；验证只创建下述独立库，未初始化或迁移已有运行库。

20项删除含共同审计19项及012 T019/004明确交付的失效Review产品资源1项。删除前核对应路径/内容及原消费者审计，集成后全部路径实际不存在；当前源码/脚本无旧IPlcRecipePort、ManualFlipInteraction、TestSpecialMessages、VirtualSafetyGate或review资源引用，受影响正式工程完整构建及当前闭包扫描通过。原文件在恢复目录保留历史字节，未作为活动备用实现。

- `VirtualPlc/RecipeApplicationTestFault.cs`
- `VirtualPlc/TestSpecialActions.cs`
- `backend/src/Gaode.Application/Ports/IPlcRecipePort.cs`
- `backend/src/Gaode.Application/Station01/Steps/PalletUnlockStep.cs`
- `backend/src/Gaode.Application/Station01/Steps/StartClampStep.cs`
- `backend/src/Gaode.Application/Workflow/ManualFlipInteraction.cs`
- `backend/src/Gaode.Host/Composition/UnavailablePlcRecipePort.cs`
- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Binding.cs`
- `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.HttpEvidence.cs`
- `backend/src/Gaode.Infrastructure/Devices/Plc/TestSpecialMessages.cs`
- `backend/tests/Gaode.Communication.Tests/Devices/AuxiliaryEvidenceTests.cs`
- `backend/tests/Gaode.Communication.Tests/Devices/RecipeApplicationDeadlineTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Station01/PalletUnlockStepTests.cs`
- `backend/tests/Gaode.Contracts.Tests/Station01/StartClampStepTests.cs`
- `backend/tests/Gaode.Integration.Tests/CommunicationFixtures/HealthyBindingCommunicationTests.cs`
- `backend/tests/Gaode.Integration.Tests/CommunicationFixtures/RecipeRotationIntegrationTests.Wire.cs`
- `backend/tests/Gaode.Integration.Tests/CommunicationFixtures/VirtualPlcSafetyGateTests.Wire.cs`
- `backend/tests/Gaode.Integration.Tests/Station01/RecipeRotationIntegrationTests.cs`
- `backend/tests/Gaode.Integration.Tests/Station01/VirtualPlcSafetyGateTests.cs`
- `backend/src/Gaode.Infrastructure/Recipes/catalogs/recipe-catalog-review.json`

## 本轮主项目验证

| 验证 | 实际结果及范围 |
| --- | --- |
| 构建 | 从主项目构建Integration（包含Host、StorePrep、VirtualPlc、Domain、Application、Infrastructure、Plc.Protocol）、Contracts、Communication、Rules，全部0警告/0错误；共用依赖增量构建，没有排除源码 |
| 前端 | 主项目 `frontend` 执行现有build，生成资源成功；先核dist绝对路径并备份原生成文件。a.html/runtime.js/recipe-authoring.js与已验证副本构建产物逐字节一致 |
| 当前架构 | 主项目Rules实际发现/执行13，通过13，失败0、Skip0（9当前检查＋4保存/序列化负例）。009扫描482文件，010扫描437文件、447闭包项，0违规。新扫描文件摘要再次与主项目实际源码核对；本轮证据 `E:/dzk/gaode-1/artifacts/recipe-execution-010/011-main-integration-20261004`，未用副本旧明细替代 |
| 保存→完整重读 | 主项目实际Host、SQLite、正式HTTP路由，POST201/GET200，服务端身份/版本/摘要非空，完整definition与ETag一致，目录同源读取；新配方库RecipeHead及RecipeSavedContent各1真实行 |
| 页面/资源 | 独立本机静态服务读取主项目frontend/dist，真实Edge headless加载login/a/data-view三页，ready=complete，runtime/authoring实际加载，资源与页面错误列表为空，保留a页截图。仅页面加载冒烟，未重跑页面联合链或宣称新增WPF验收 |
| 生命周期/隔离 | 新事实库Runs=0，本机独立VirtualPlc actions=0；有真实依赖握手/心跳，不声称零I/O。Host正常退出0。端口自动选取，凭据仅进程环境；仅结束本轮拥有的进程，没有连接现场设备或终止012进程 |

构建和目标核验：`E:/dzk/gaode-1/workcopies/011-plc-interaction-update/artifacts/011-plc-interaction-update/main-integration-20261004`；API/页面/SQLite及生命周期实证：`E:/dzk/gaode-1/artifacts/011-plc-interaction-update/main-integration-20261004-smoke`。未新增全量测试、配方穷举或009/010历史专项。

## 原证据复用与限制

复用011 `implementation-20261003/joint-single-05`（run17602489-a2f9-4352-bd1c-c9384637f1ed）及 `joint-multi-03`（runcc7f1d75-10f0-4ad1-ba46-1bd44feeed50），各1/1、0Skip；012同期及同run终态T022005/006、状态007后的真实GET/只读终态对账、既有必要组件和不变架构正负例，仅沿原验证报告列明范围。主项目交付源码/静态输入摘要与组成副本一致，前端关键输出一致；目录迁移未改变业务，当前根架构和保存/页面冒烟另有本轮证据。未把原截图重标为本轮，未再启动单面/多面两条完整链。

T009/T010依赖的正式地址、ASCII/型号承载及真实外部新3D/现场机械安全输入仍未齐，实际正式互通/生产验收未验证，不编造值或设备反馈；仅限制依赖部分。少数迟回执、瞬时阻断等稳定性原因仍是待办，原失败与Incomplete证据完整保留，成功重试不表示根因消失。当前没有尚未接收的005软件交接增量；未来外部输入不是已完成的软件交付。没有启动新功能或生产部署。
