# Bug Verification: 第一工位下料协议对齐

- **Slug**: `station01-unload-protocol-alignment`（用户显式指定）
- **Tested**: 2026-09-24（Asia/Shanghai）
- **Assessment**: [assessment.md](./assessment.md)
- **Fix**: [fix.md](./fix.md)
- **Result**: partial

## Summary

修复版本的下料相关合同测试和相邻流程回归通过；本轮新建的独立Test/VirtualPlc页面样本在**任何页面点击或运行创建之前**发生独立的`HeartbeatStoppedChanging`，Host以`PlcHeartbeatLost`阻断，故原始WPF/WebView2单次启动到下料的复现验收**未执行**。此心跳前置故障不等于原始`UnloadPreparation / StagePreconditionStatus:1`复现，也不能据本轮测试宣称下料缺陷已独立验收或真机通过。失败样本已保留、未自动重试；仅关闭本轮核对身份的Host/VirtualPlc及其自退出worker。

## Checks Performed

| Check | Command / Action | Result | Notes |
| --- | --- | --- | --- |
| 测试对象和脚本加载核对 | 只读哈希`start-station01-virtual-loop.ps1`加载的默认DLL、修复隔离DLL、WPF EXE及前端runtime，并对照上轮B包`process.json` | pass | 默认Host `F1D124...`、PLC `6CF84B...`均非修复构建；不得用一键脚本的`-SkipBuild`验收。修复Host `7797E502DBCC479CC62AFA7E4D3526216879F9D5E664A69AB9E53030E9F69336`、PLC `96A4FABD003D94DF9AE3E22C30FEC5C80AB8A6E13ED2C4853B55D59576A36818`。前端runtime和WPF内嵌runtime同为`3CDD927C72B1B400D023015FBABDA1B53ED17AE341BCFD6D2EEE1E8C8E27D59C`，WPF EXE为`A72420FB3BA356BD02B34ADCEC7739C583A34E694D8FB72D3F1BBC5EA766893C`。WPF未实际启动，故这些是磁盘文件而非运行PID证明。 |
| 新/更新下料、VirtualPlc、配置测试 | `dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj --artifacts-path artifacts/unload-fix-build-20260924 --no-build --no-restore --filter 'FullyQualifiedName~PlcStageActionPortContractTests\|FullyQualifiedName~VirtualPlcLatestProtocolTests\|FullyQualifiedName~Configuration' --logger trx --results-directory .specify/bugs/station01-unload-protocol-alignment` | pass | 31/31；`bug-test-contracts.trx`，SHA-256 `31E4AE20ED00393DD5AB363495B5B38543CEBA0145C7E2E319D337C1B7C4B9BF`。包括旧1允许、三目标先写、同目标歧义UnknownHeld、缺目标/Z复位或错误Z拒绝、命令后代次变化、正式Modbus VirtualPlc命令4无新Z运动。失联不重发用例属于合同测试，不是本轮新页面故障注入。 |
| 相邻编排回归 | 同一测试DLL `--filter 'FullyQualifiedName~WholeTrayWorkflowOrchestratorTests\|FullyQualifiedName~ThreeStageWorkflowExecutorTests\|FullyQualifiedName~LatestPlcProtocolTests'`，TRX写入本缺陷目录 | pass | 21/21；`bug-test-workflow-regression.trx`，SHA-256 `7ED40C9BA03999F6FDFF43B44492F956A88012D50651B83DACF6F6E58FA3EC5E`。仅为自动化局部回归。 |
| 独立页面实例准备与对象核验 | `Gaode.StorePrep.dll <本缺陷目录> <本缺陷目录>/page-run-20260924-0520`，用本缺陷目录的`start-page-test.ps1`启动修复Host/PLC，端口25121/25122/25123、独立SQLite | fail（前置就绪） | `page-run-20260924-0520/process.json`保存实际修复DLL、配置`public/simulation 1.2.0`及哈希；Host日志记录`heartbeat first failure`后`HeartbeatStoppedChanging`，代次1→2，GET为`PlcHeartbeatLost`。脚本在未就绪处停止；不改阈值、不自动重试。 |
| 原始WPF/WebView2一次页面启动与正常下料、解锁、最终显示 | 未点击；检查当前命令进程Session 0、交互RDP Session 2及页面启动工具的Session 0拒绝规则 | not-run | 本轮WPF进程未启动、无前后截图、无实际页面POST/202或GET链。该前置故障样本SQLite `Runs=0`、`StageEvents=0`、PLC审计命令4写入0；不能用上轮辅助API完整闭环抵扣。 |
| 001 T088 | 核对现有配置测试、公共schema/运行冻结链及Test配置；未补写测试或勾选任务 | partial | 旧3D/F配置加载及一般冻结/用途校验由现有测试覆盖；上轮B包可见Test Unload版本和冻结目标。但缺**Unload专属**旧配置兼容、冻结后变更不影响本run、Test目标不能用于Real动作、缺生产批准Unload目标在正式Real路径派发前拒绝的直接测试/证据。`001/tasks.md`的T088仍为`[ ]`。 |

## Output Excerpts and Preserved Evidence

- 自动化：`已通过! - 失败: 0，通过: 31`；编排回归`已通过! - 失败: 0，通过: 21`。
- 本次首次心跳故障：`page-run-20260924-0520/logs/host.out.log`含`PLC heartbeat first failure: reads=28, edges=1, lastEdgeAgeMs=3015, thresholdMs=3000`，随后`reason=HeartbeatStoppedChanging, epoch=1, runId=(null), actionId=(null)`；`preclick-host-status.json`为`connected=false / diagnosticCode=PlcHeartbeatLost / connectionEpoch=2`。这不是下料动作失败。
- 本次样本根：`page-run-20260924-0520/`，含`process.json`、原始Host/PLC日志、`preclick-host-status.json`、`preclick-plc-state.json`、`preclick-plc-audit.json`及独立SQLite主库/WAL/SHM。Host日志SHA-256 `EC6C2406DB9138A4214E655EEB703862C12C7D32D22665123AB67A130B10006E`，状态快照SHA-256 `03A8FD5AD29DE2BFBC99D2F99B5CCBD17F16468B6525DE3D2839F20841FD9CF7`。无运行/阶段事件、无命令4；未覆盖原始`artifacts/station01-007/manual-20260924-033644-b7b9f088`证据。
- 上轮`fix.md`的`artifacts/station01-007/unload-fix-20260924-b/`辅助API正常闭环仅作为已保存修复阶段证据复核，不计本轮独立WPF页面通过。

## Residual Risks

- **下料缺陷独立验收：未完成。** 原始样本直接根因为Host错误要求派发前`XY_Pos_Confirmed=0`；当前合同测试显示修复语义成立，但本轮没有新的实际页面运行到`UnloadPreparation`来证明原始人工路径症状不再出现。
- **原始WPF页面场景：未通过验收／未执行。** 交互桌面位于Session 2，而本轮命令位于Session 0；更直接的阻碍是隔离Host在点击前已因独立心跳故障阻断。没有页面截图、脱敏请求/202、同run后端和显示对照。不能把预启动阻断写成下料失败或修复失败。
- **T088仍有证据缺口**，见上表；本轮没有修改001任务状态或任何产品/测试源码。
- **第一工位整体闭环仍未完成**：007 T015实际WPF全链、006 T044页面连通及T045媒体格位绑定仍为未勾选；本轮未验证真实人工取盘。上轮自动模拟取盘只可标`Test/Simulated`。生产下料坐标/容差与真实设备验收另待批准，间歇性心跳问题独立处理；本轮发生一次心跳失效，并未解决其根因。

## Recommendation

**Hold，保持`partial`，不要关闭缺陷或宣称真机／整站通过。** 先独立诊断本次预点击`HeartbeatStoppedChanging`（保留本包，不放宽阈值），再经用户确认安排**一个新隔离Test实例**，不能复用本次受阻数据库或自动重试。需要用户在Windows交互Session 2启动实际WPF/WebView2并在原型页面点“进入系统”→已有“启动”按钮**一次**；随后必须取得页面前后截图、真实POST/202、requestId/runId/operationId、同次协议审计/SQLite/GET及Test模拟取盘来源。既有`start-station01-manual-test.ps1 -SkipBuild`会加载旧DLL，不能使用；不带该参数则会构建并写入`artifacts/`而非本缺陷目录，须在下一次执行前选定符合本轮输出约束的隔离启动方式。无此页面证据不得改为`verified`。
