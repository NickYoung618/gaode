# Phase 8实施与最小回归记录

2026-10-08；HEAD `253324492a3ffe9ea5a62b605b5dd812d04b267c`，分支020-real-device-commissioning，包含既有未提交修改。feature.json仍指向本功能。使用仓库speckit-implement技能，沿用户对局部未决项的实施授权；requirements只读。

## 本轮结果

T062/T063/T064已补软件验证，T061完成独立前端规格的缺口交接，T065同步当前进度及证据。没有发现需改产品代码的确定缺陷；本轮源码变更仅4个测试文件，没有变更共享接口、通信协议、现场值、前端或桌面。

最终最小回归12执行、12通过、0失败、0跳过，约9秒：[phase8-final.trx](evidence/phase8/phase8-final.trx)。Host构建0警告、0错误：[build-host.txt](evidence/phase8/build-host.txt)。本次12项不是原阶段B的55项加总，也没有重跑全库或工具58项；原工具/握手及SDK代码未改，原证据保留其原范围。

| 任务 | 实际验证/交付 | 证据及限制 |
| --- | --- | --- |
| T062 | 正式API保存双面配方A，正式执行器冻结后用ETag保存B；修改一处X及一个单张专用曝光配置。A后续Detection MoveRequest/CaptureRequest仍等于A，重读冻结的B实际请求等于B；版本/定义摘要不同，SQLite和媒体沿真实保存链 | [A字段/请求](evidence/phase8/edit-isolation-run-a.json)、[B字段/请求](evidence/phase8/edit-isolation-run-b.json)、[原参数对账](evidence/phase8/recipe-field-trace.json)。两次独立运行，动作/图像端口为显式OFFLINE夹具，非物理运动/真实SDK；P05/P06等原限制不扩大 |
| T063 | 旧Test布局正式Host正常链到AwaitingManualRemoval，经已鉴权人工取盘确认HTTP API接受，再核Completed/FinalUnloadCompletion；新TraceQuery/StageEventStore上下文从SQLite读到Completed终态和同Run/Tray人工/最终事实，查询API返回成功；前序真实保存7份媒体 | [最终确认及重读](evidence/phase8/manual-final-completion.json)、[等待人工边界](evidence/phase8/legacy-test-workflow.json)、[持久日志](evidence/phase8/legacy-workflow.log)。人工来源Test、最终事实HostDerived；未假称真实料盘已取走或新用途现场正常链完成 |
| T064 | RealDeviceCommissioning正式DI + 合法Commissioning上下文经StartPublicPreparation：缺F定位安全输入在依赖运动前拒绝；结构齐备但PLC-Q3/Q4未明时StartupNotReady。两例持久审计无ActionIntent，loopback写入仅允许PC心跳offset1005；无准备/启动/轴/目标派发、无未知重发，七worker未启动、TrayPose调用为0 | [缺输入状态/审计/写入](evidence/phase8/start-missing-input.json)、[日志](evidence/phase8/start-missing-input.log)；[安全未明状态/审计/写入](evidence/phase8/start-site-safety-unconfirmed.json)、[日志](evidence/phase8/start-site-safety-unconfirmed.log)。日志按Run定位原因；BlockedNoDeviceAction/noStartOrMotionRequest在SQLite审计。测试正式业务启动入口，未证明desktop/frontend入口可用 |
| T061 | 独立前端规格缺口及后续合同/计划/验收交接 | [frontend-handoff](frontend-handoff.md)；只完成交接说明，实际独立规格、页面/桌面实施及验收仍待后续 |
| T065 | 修正plan阶段B“全部未执行/下一步analyze”的过时当前说明，同步spec、stage-b-plan、tasks、quickstart、document-sync、阶段B验证与本清单 | 新[源码/证据清单](evidence/phase8/source-state-final.json)相对[本轮基线](evidence/phase8/baseline.json)，保留原阶段B清单及历史运行文件 |

T062的运行A在冻结之后、首个检测执行请求之前保存B，随后观察实际请求，不只是比较冻结对象。修改单张曝光通过新专用CaptureProfile隔离，其他采集配置保留。T063的持久读取不把原AwaitingManualRemoval内存状态误作数据库状态：先重读Run.State/Terminal，再用保存事实重建终态投影。临时SQLite/媒体确实创建、读取并清理；证据内临时路径不是可交付现场数据。

## 命令与增量失败记录

从`D:\gaode`执行：

```powershell
dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj --no-restore --filter "FullyQualifiedName~RecipeCommissioningChainTests|FullyQualifiedName~ControlledCommissioningTests|FullyQualifiedName~CommissioningWorkflowTests|FullyQualifiedName~CameraBusinessRegressionTests" --logger "trx;LogFileName=phase8-final.trx" --results-directory specs/020-real-device-commissioning/evidence/phase8
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --no-restore
```

覆盖3项配方、5项受控联调、1项完整Test流程、3项相机业务回归；共用执行夹具变更由这3项相机回归覆盖，不扩展异常组合。

增量T062先3/3通过（t062.trx）。T063首次编译缺少StageEventType命名空间，补using后首次运行1失败（t063.trx）：测试把旧内存状态直接传给只补充事实的投影，误期待投影自动更新State；修正为重读SQLite Run，再投影。T064首次1通过/1失败（t064.trx）：测试误要求持久日志含数据库审计字段BlockedNoDeviceAction；实际日志记录StartupReadiness/PLC-Q3/Q4，审计含阻断处置。修正为分别核日志及SQLite。两者均为测试断言问题，未修改产品行为或放宽运动门禁。修正后3/3通过（t063-t064-corrected.trx），随后12/12通过；复核又补每张采集请求的ProfileId与配方目标对应断言，防止用错曝光配置仍通过，保存此前结果为phase8-before-profile-assertion.trx，再次上述12/12最终回归。保留失败TRX，不计为通过；部分诊断日志包含多次执行，以最终JSON中的Run匹配。

## 剩余限制

T055七台真实相机新参数/SDK效果、T056正式新用途真机连续两轮仍Blocked且未勾选；等待既有现场资料、安全虚拟输入、适用PLC-Q3/Q4答复和当次硬件授权。本轮未填写任何现场安全值，没有连接硬件、打包、部署、提交或推送。公共Z用途、019历史SDK阻塞/物理断线及机械精度等原未验保持。前端后续义务见交接，发布/回退仍待后续交付阶段。

可再次执行speckit-converge核对软件范围及剩余义务；不能宣称020整体完成、桌面入口完成或真机/部署放行。after_implement hooks为空。
