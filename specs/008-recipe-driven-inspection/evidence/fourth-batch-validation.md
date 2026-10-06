# 第四批单面组件执行证据（2026-09-25）

范围：显式标记`Test/InjectedXYZ`的独立组件输入，正式Host使用的同一`IntegratedDetectionPort`、`MotionCoordinator`、`LatestProtocolPlcDevice`，经真实Modbus TCP到VirtualPlc；FileBackedCapture实际读取固定PNG，独立Python worker实际读取媒体并等待规定模拟时长，TraceWriter/StageEventStore提交到独立SQLite，媒体保存到独立目录。**不属于Q01/Q02正式前端完整验收，不批准其物理点位或公共3D高度换算。**

| 用例 | 当前成功包 | 已核验事实 |
| --- | --- | --- |
| AB/P01 | [component-result.json](../../../artifacts/recipe-execution-008/fourth-batch/q01-1ae7c02aae5a40b5af2c70ca13cf960c/component-result.json)、同目录`station01.db`、`worker-protocol.jsonl`及`media/` | 命令2/到位/状态1→2/复位2→清0各2轮；A后B，媒体2、单图worker2、双输入融合worker1，对象结果1；SQLite媒体、算法、步骤及融合事实读回，媒体文件存在 |
| CD/P01、P03 | [component-result.json](../../../artifacts/recipe-execution-008/fourth-batch/q02-8c92202e306141ab966843c012e27fd2/component-result.json)、同目录`station01.db`、`worker-protocol.jsonl`及`media/` | 显式Test协议槽1/3；C:P01、C:P03、D:P01、D:P03各轮命令2/到位/1→2/复位2→0；媒体4、单图worker4、双输入融合worker2，对象结果2；SQLite及媒体读回，融合事实分别关联两个对象 |

测试中逐轮断言：本轮命令2先于状态1，单图必要事实先于状态2，本轮复位反馈2先于清0，清0先于下一定位；按SQLite已提交采集事实还原并断言A全批/B全批、C全批/D全批及非连续物理槽1/3。worker协议文件含每次双输入`Fused`结果、两个mediaId及分别释放记录；SQLite实际保存相机媒体、算法调用、复位和融合事实，且双输入调用数与对象数一致。每个包独立Test根，后续重复运行产生新目录，旧证据不覆盖。

| 失败注入 | 当前包 | 结果 |
| --- | --- | --- |
| 到位观察坐标不符 | [component-failure.json](../../../artifacts/recipe-execution-008/fourth-batch/q01-6b4da14275f34ee8a0a5b412f981f9e3/component-failure.json) | `DetectionCoordinatesMismatch`，0次采集，只有第1次命令2，动作占用Unknown |
| 必要媒体元数据保存失败 | [component-failure.json](../../../artifacts/recipe-execution-008/fourth-batch/q01-163d4d6fc8f44c9581e3d107a6c0a018/component-failure.json) | `DetectionTraceSaveFailed:Failed`，首图已采但无下一定位/算法完成，动作占用Unknown |
| PLC复位失败3 | [component-failure.json](../../../artifacts/recipe-execution-008/fourth-batch/q01-3e5e7b81a9f04853bfb1ab06be5afd0b/component-failure.json) | `ZResetFailed`，首图及单图worker完成后停止，无下一定位，动作占用Unknown |

最终验证命令：`dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --no-restore --filter 'FullyQualifiedName~SingleFaceDetectionIntegrationTests|FullyQualifiedName~RecipeExecutionCoordinatorTests|FullyQualifiedName~PublicPreparationTargetResolutionTests' -v:q`，9/9通过（含AB/CD及三失败）；现有Detection重试/Pending和F后产品握手定向回归6/6通过；`dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --no-restore -v:q`成功，0警告/0错误。公共移交组件测试验证带批准来源的显式固定目标可解析、缺目标有具体拒绝；其构造输入仍是组件样本，不是Q01正式数据。

目前仍未验证正式目录可用、公共3D高度到检测Z映射、正式页面选择/F绑定到本单元的连续运行，也未运行下料/解锁/取盘/Final。008 T052—T054、003 T070及001 T090保留未勾。
