# 007 第一工位完整虚拟联调指南

**日期**：2026-09-23。007后端/虚拟组件及PowerShell入口已接线，仍须以运行证据判定闭环；006实际页面启动和完整状态展示尚未交付，不能记录前端闭环通过。

## 前置条件与时间核算

1. Windows、.NET10 SDK、Node/npm、Python运行环境（供规划的独立虚拟worker）及WebView2 Runtime满足006宿主要求；客户ZIP只读哈希为`3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0`。
2. 003/006相关共享文档已最小同步；003确认API唯一正式路由为`/manual-removal-confirmations`，请求体为`requestId/expectedRevision/reason`。T013已使VirtualPlcIntegration记录Test模拟确认来源，辅助API样本已验证。006仍负责页面请求、受控Test凭据与已有位置展示；007 Host侧Test来源API/通知跨源和授权已单侧验证，双方实际页面连通待T015。
3. 使用`purpose=Test`、`RealElapsed`的新版本007预算与模拟清单，固定单一图片目录及SHA-256。候选S1 `R-S1-A-CAP` v`0.4.0-review`、占位仅P01、F合法唯一码`RC:R-S1-A-CAP:0.4.0-review`。正式计划预期2次检测采集及逐采集对应的2次算法请求，实际步骤/调用数必须从冻结计划和证据核对。
4. 旧`s01-budget-virtual-plc` v1.0.0的3D/F采集仅1–1.5秒、算法仅0.7–1秒，会使007正常延迟确定性超时；不得用它作通过样本。使用`s01-budget-virtual-loop` v1.1.0：采集8秒窗口、算法15秒窗口、3秒PLC心跳失联门限；上述数值仅为旧虚拟配置事实；新版按008冻结实际动作、采集、算法、保存及ACK预算推导阶段绝对期限，不沿用共享120秒，不放宽3秒心跳。
5. 建立新的空Test数据根，记录SQLite真实路径、媒体根、端口、配置摘要及进程版本。不重复使用旧活动运行的数据库来规避恢复/占用门禁。

## 已有命令：构建与现有进程

从项目根目录 `E:\dzk\gaode-1` 执行。下面命令对应现有工程；它们启动后仍只是旧适配组合，不能单独证明007。

```powershell
dotnet build VirtualPlc/VirtualPlc.csproj
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj
npm ci --prefix frontend
npm run build --prefix frontend
dotnet build desktop/Gaode.Station01.Desktop.csproj -c Release
```

VirtualPlc独立进程现成启动命令；默认Modbus `127.0.0.1:1502`、UnitId 1，HTTP状态 `127.0.0.1:5080`，实际值写入manifest：

```powershell
dotnet run --project VirtualPlc/VirtualPlc.csproj
```

Host由下方007平台脚本使用已构建`Gaode.Host.dll`启动，设置绝对TestRoot/AllowedTestRoot、007 `examples`配置根、001只读schema根、`s01-public-virtual-loop` v1.2.0、`s01-budget-virtual-loop` v1.1.0、`s01-sim-virtual-loop` v1.2.0、`Gaode__ImageManifestPath`、`Gaode__WorkerExecutablePath`、`Gaode__WorkerScriptPath`、`Gaode__WorkerManifestPath`及`Gaode__TestAllowedOrigin=https://appassets.local`。Host通过WorkerProcessSupervisor管理唯一worker子进程；平台核对PID和Host状态。单独调试Host时应沿用平台脚本设置的这些环境值，不能用旧预算。v1.2.0的Unload XYZ=(300,100,150) mm仅供Test/VirtualPlc，取值在虚拟设备有效范围0–500内，Z=虚拟复位安全位置150；冻结来源、版本及原有0.001 mm位置容差均须可查，不能移作真机默认值：

```powershell
pwsh -NoProfile -File scripts/start-station01-virtual-loop.ps1 -SkipDesktop
```

桌面宿主现成构建/启动命令。需显式配置与Host一致的地址；006须完成页面请求、Bearer凭据受控传递及已有位置的完整状态展示，007须完成Host侧限定Test来源的API/通知跨源配置、后端授权验证及实际连通性。双方交付前，页面启动仍可能401/网络失败：

```powershell
$env:GAODE_API_BASE_URL = 'http://127.0.0.1:5001'
$env:GAODE_SIGNALR_URL = 'http://127.0.0.1:5001/hubs/station01'
$env:GAODE_MODE = 'Test'
dotnet run --project desktop/Gaode.Station01.Desktop.csproj
```

前端静态资源内嵌WPF/WebView2，不运行额外前端Web服务。上述命令在不同终端或受控启动脚本中执行，记录PID、端点、工作目录、日志和退出方式；终止只针对本次启动且由manifest记录的进程。

## 007平台和辅助入口

以下命令从项目根目录执行，受控Test令牌只经进程环境传递，不写入证据。`-SkipDesktop`只用于007后端组件调试，完整联调必须由006实际桌面页面启动；脚本辅助启动记录为另一样本。

```powershell
$env:GAODE_TEST_OPERATOR_TOKEN = '<受控本地Test token>'
dotnet build VirtualPlc/VirtualPlc.csproj
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj
dotnet build desktop/Gaode.Station01.Desktop.csproj -c Release
pwsh -NoProfile -File scripts/start-station01-virtual-loop.ps1
pwsh -NoProfile -File scripts/simulate-station01-load.ps1 -PrepareOnly -Scenario S1 -OccupiedSlots P01
# 仅辅助API样本；不能代替006页面启动：
pwsh -NoProfile -File scripts/simulate-station01-load.ps1 -StartRun -Scenario S1 -OccupiedSlots P01
```

Host以当前配置从`GAODE__WorkerExecutablePath`启动唯一`python scripts/virtual-station01-algorithm.py <绝对算法清单路径>`子进程，工作目录为本次Test根的`media-root`；单独Python调用只用于协议调试，不与平台并列启动。`process.json`登记Host/VirtualPlc/worker/监视/桌面PID，`media-root/worker-protocol.jsonl`和`logs/`保存进程证据。退出时只停止本次`process.json`登记的进程。模拟上料脚本`-PrepareOnly`供前端实际启动；`-StartRun`仅为辅助API样本。虚拟相机是Host采集适配，无独立服务。VirtualPlc没有上料REST，不用`/api/simulator/reset`冒充上料。旧两进程`start-local.ps1`不是007全组合入口。

本次联调结束后，以平台输出的绝对`process.json`路径代入下列命令，仅停止其中仍匹配的本次进程；若存在活动运行，先查询终态和SQLite事实：

```powershell
$record = Get-Content -Raw '<本次绝对TestRoot>/process.json' | ConvertFrom-Json
foreach ($ownedId in @($record.desktopPid, $record.watchPid, $record.hostPid, $record.workerPid, $record.plcPid)) {
    if ($ownedId -and (Get-Process -Id $ownedId -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $ownedId
    }
}
```

## 必须执行：实际前端启动到FinalUnloadCompletion

1. 启动全部指定进程并登记清单，核对Host管理的唯一worker子进程PID/健康/日志、VirtualPlc`/health`、Host`/api/v1/station01/status`的真实可用状态和已选Test来源；确保Host不再把实际文件采集/独立worker错误报告为NotIntegrated。
2. T002的006页面请求/受控Test凭据/已有位置状态展示和T010的007 Host侧限定Test来源跨源/授权配置均就绪后，验证WebView2页面到API及通知的实际连通。`-PrepareOnly`准备模拟托盘、图片清单和合法CAP/P01上下文；在006实际桌面已有启动入口发起请求。前端必须构造唯一requestId、JSON contextJson及public/budget/simulation版本引用，并经后端授权。保存页面操作、真实HTTP请求/202回执、commandId/runId及通知/GET对照；页面202只显示受理。
3. 用原命令查询和`GET /api/v1/station01/runs/{runId}`观察公共准备、PLC内部夹紧反馈、3D/F采集和worker调用、F合法码、配方冻结及handoff提交；不得第二次启动或客户端手工造计划。
4. 观察003正式Detection按冻结计划执行全部必检采集/算法。核对每个capture的输入文件摘要、新媒体内容和3–5秒墙钟；每个worker call真实收发、受控媒体关联、10秒模拟计算和合法随机结果；计划和结果按runId/对象/阶段归档。
5. 核对Sorting及UnloadPreparation的PLC动作与当前代次反馈、SQLite短事务和完整状态；三阶段提交后才有WholeTrayCompletion及ReadyForUnlockSourceMatrix，随后才发解锁并提交ObservedUnlocked。
6. 启动时已启用的客户端监视到已提交ObservedUnlocked后自动模拟取盘并向统一后的后端确认API提交。服务端检查身份/阶段/revision后原子保存确认与FinalUnloadCompletion。查询最终矩阵必须把ManualActor标为Test/Simulated受控客户端，而非真实人工；页面已有位置呈现后端最终状态，明确取盘确认并非由前端控件完成。
7. 对照实际SQLite事件/投影、媒体文件内容、API查询和完整源矩阵。所有必需组件与调用都在同一runId，最终证据为`SoftwareLoopOnly`且`productionClaimAllowed=false`。将结果记Passed的前提是下面必要失败验证或有效证据复用同样完成。

PowerShell辅助启动样本可额外执行`-StartRun`验证简易平台，但不可替代第2步实际前端启动样本。真实前端入站请求当前存在空body/缺Bearer/跨源差异；006页面交付和007 Host侧跨源/授权连通共同构成本步骤门禁，不限制独立后端/虚拟组件实现。

## 必要失败路径与证据复用

| 场景 | 最小操作/可复用范围 | 通过条件 |
| --- | --- | --- |
| 文件/媒体/worker真实性 | 对新增文件输入缺失、媒体保存失败、worker不派发/无响应或错误身份做局部验证 | 无假媒体或默认OK；有限终态及SQLite原始失败事实可查；不能以旧合成媒体测试复用 |
| F/3D安全与合法配方 | F返回无效/歧义码或3D安全Z缺失；未改的PLC安全逻辑可引用003证据 | 不借旧值、无提前绑定/运动；依据当前003有效工艺停止依赖动作 |
| Detection有限失败/映射 | 新worker超时或失败；003未受影响的Pending分拣/MappingFailed证据可复用，改动处局部重验 | 原错误、尝试及总期限保留；合法映射走Pending正式分拣，歧义暂停；不得硬编码OK |
| PLC未知动作/解锁 | 复用003相同实现与合同证据；实际接线涉及该路径时做单点验证 | 当前代次不匹配或可能已派发的动作进入UnknownHeld，不盲重发、不提前解锁 |
| 关键持久化/完成门禁 | 新媒体保存和最终确认提交故障，提前/无授权/错误revision自动确认 | 未提交不得报告完成；409/401/403/503及提交未知如实体现；无重复Final记录 |

旧证据复用记录原路径、对应断言、代码/配置/合同版本及本次未受影响理由。003旧五场景及006旧前端测试不自动全量重复；新增独立worker、固定图片、实际前端组合、Test来源的模拟确认及新预算必须有本次证据。

## 证据目录与结束判定

建议 `artifacts/station01-007/<evidence-id>/` 下至少有：

- `manifest.json`：配置/代码/合同/原型哈希、runId/trayId、样本占位、配方/预算、C/A和来源索引；
- `process.json`与各进程日志：VirtualPlc、worker、Host、WPF PID、命令、版本、地址及退出；
- `frontend-operation.json`、`api-transcript.json`及必要画面对照：实际前端启动、状态、模拟取盘客户端渠道；
- `input-images.json`、`media-index.json`、`algorithm-calls.json`：输入摘要、实际读取/保存、采集与计算时长、进程收发与随机配置；
- `modbus-audit.json`、`sqlite-events.json`、`sqlite-projections.json`、真实SQLite/媒体摘要及`source-matrix.json`；
- `final-result.json`：每项Passed/Failed/Blocked/NotRun、有效旧证据引用和最终SoftwareLoopOnly结论。

无实际前端启动、任一必需进程未参与、媒体/算法无真实收发、关键SQLite事实缺失、模拟取盘写成AuthenticatedHuman或未到FinalUnloadCompletion时，整体结果不得为Passed。联调通过只证明007选择的第一工位虚拟软件闭环，不表示真机、算法精度、现场节拍、生产验收或整个项目完成。
