# Bug Fix: 第一工位下料协议对齐

- **Slug**: `station01-unload-protocol-alignment`（用户显式指定）
- **Fixed**: 2026-09-24（Asia/Shanghai）
- **Assessment**: [assessment.md](./assessment.md)（原文保留）
- **Status**: applied（软件修复及隔离Test/VirtualPlc局部验证；独立bug-test、原WPF入口与真机验收未完成）

## Summary

移除把上一轮 `XY_Pos_Confirmed=1` 当成下料前错误状态的门禁，并补齐冻结下料XYZ、复位/安全Z核验、先写目标后发命令4及本轮可靠反馈判定。旧1、写入回执、同目标歧义或失联都不能单独形成完成事实；清命令不再等待协议未保证的到位0。未放宽心跳、状态过期、设备安全门禁或超时，也不自动重发。

## Changes

| File | Change | Notes |
| --- | --- | --- |
| `specs/003-plc-latest-protocol/spec.md`, `contracts/plc-stage-action-port.md`, `contracts/virtual-plc-boundary.md`, `plan.md`, `tasks.md` | modified | FR16–FR18及T064；分清Word点位/复位原文与2026-09-24用户确认的命令4项目约定。 |
| `specs/007-station01-integrated-loop/spec.md`, `contracts/virtual-integration.md`, `plan.md`, `tasks.md`, `quickstart.md`, `examples/public.virtual-loop.json`, `examples/simulation.virtual-loop.json` | modified | FR-020/SC-010/T028；Test/VirtualPlc独立下料目标 `(300,100,150)` mm、`SIM_MACHINE`、虚拟安全Z=150及原0.001 mm容差；版本1.2.0，非生产值。 |
| `specs/001-station01-public-preparation/contracts/public-config.schema.json`, `spec.md`, `plan.md`, `tasks.md` | modified | 共享公共配置允许可选独立Unload点；新增T088仍待专门配置覆盖验证，历史T087不变。 |
| `backend/src/Gaode.Domain/Configuration/PublicConfiguration.cs` | modified | 固定点增加可选Unload及其校验，旧3D/F配置保持可读取。 |
| `backend/src/Gaode.Application/Ports/StagePortContracts.cs`, `Workflow/ThreeStageWorkflowExecutor.cs`, `Station01/StartPublicPreparation.cs` | modified | 冻结目标、容差、用途、配置快照传至下料动作；意图摘要/事件保留关联。 |
| `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolStageActionAdapter.cs`, `LatestProtocolPlcDevice.cs` | modified | 复位和安全Z预检、4x0003/5/7 Float32写入先于4x0001=4、只读4x000D/F/11；旧1允许前态，但同目标无可辨新事实进入UnknownHeld；业务Failed/未知分支记录目标、run/operation/action/epoch和处置。 |
| `VirtualPlc/VirtualPlcEngine.cs`, `SimulationModels.cs`, `appsettings.json` | modified | Test复位安全Z=150；命令4须有复位反馈，只更新XY，不产生新的Z运动或伪造Z到位。 |
| `backend/tests/Gaode.Contracts.Tests/Ports/PlcStageActionPortContractTests.cs`, `Devices/VirtualPlcLatestProtocolTests.cs` | modified | 移除旧`[0,0,1,0]`错误预期；覆盖旧1、新XYZ写序、同目标未知、Z门禁、缺目标、失联/代次变化及正式Modbus虚拟时序。 |
| `scripts/start-station01-virtual-loop.ps1`, `scripts/simulate-station01-load.ps1` | modified | Test配置引用1.2.0；启动脚本可选择隔离构建DLL，避免改动共享构建或其他测试进程。 |
| `scripts/collect-station01-diagnostics.ps1` | modified | 退出后索引同时哈希SQLite主库、WAL和SHM，避免仅凭主库文件误认已覆盖本次阶段事实。 |

未改变公开错误/状态字段，因此未改006消费者规格或公开错误合同；未改宪章、Word协议来源、客户原型、`assessment.md`及既有诊断任务历史证据。

## Tests Added or Updated

- `PlcStageActionPortContractTests.UnloadAcceptsOldArrivalAndWritesFreshTargetsBeforeCommand`：旧1不拒绝，目标写入先于命令4，清命令后不等0。
- `SameTargetWithoutDistinguishableFeedbackIsUnknownHeld`、`DisconnectAfterCommandWriteIsUnknownHeldAndCommandIsNeverReplayed`及代次变化用例：不把旧反馈/写回执当完成，不盲重发。
- 缺Z复位、实际Z不在安全位置、缺冻结目标用例：发命令前拒绝且无目标/命令写入。
- `VirtualPlcLatestProtocolTests.TcpProtocolExposesHeartbeatClampMoveSortUnloadUnlockAndFailureFacts`：正式Modbus下命令4前复位，实际Z仍为150，命令4后不出现新Z到位2。

## Local Verification

- `dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --artifacts-path artifacts/unload-fix-build-20260924`：成功，0 warnings/errors。定向`dotnet test`最终31/31通过；机器可复查结果为 `artifacts/station01-007/unload-fix-20260924-b/unload-fix-contracts.trx`，SHA-256 `CF5DD96E0E6EDF28A53474C46980A618F07D8AD7A01CA178CBD29C8182D7337A`。一次测试构建因本轮运行的隔离VirtualPlc DLL被占用而失败；核对PID后仅关闭本轮实例，再重跑通过。后续测试代码两处符号/读权限编译错误已修正，不隐去这些中途失败。
- 独立Test实例A `artifacts/station01-007/unload-fix-20260924-a/`：辅助API启动，正式Host/VirtualPlc经Modbus到`FinalUnloadCompletion`；保存GET、PLC审计/变化、日志、SQLite和退出后索引。它使用VirtualPlc门禁最后加严之前的构建，仅作为额外样本。
- 独立Test实例B `artifacts/station01-007/unload-fix-20260924-b/`：**最新构建**，独立端口25091/25092/25112和SQLite；辅助API请求`s01-007-59008516a2f5459a8257113207026ea6`→命令`ded7cf0f-4a01-4b0c-8cae-4dc8161896d3`→运行`33ce1827-c597-4da0-88e5-85ad553bf251`→下料动作`15ca556e-72f5-46a1-aed9-2ed7453aa69b`、设备代次1。PLC变化序列146/153/154为`Inspection_Status=2`、`Z_Reset_Status=2`、`Inspection_Status=0`；212/213/216/223/224为目标X新值、命令4、旧1→0、实际X到300、本轮到位1。审计在命令4前记录4x0003/0005/0007三目标写入；Host日志行932/958记录目标与实际`(300,100,150)`、基线`(200,100,150)`及判据。GET/SQLite确认`WholeTrayCompletion`、解锁读回后的`ObservedUnlocked`及受控Test人工确认后的`FinalUnloadCompletion`；不是仅命令写入成功。
- B的`process.json`保存Host/PLC/Worker版本、配置哈希、协议和端口；`diagnostic-index.json`在仅结束本轮已验证身份的进程后收集20项文件路径、请求关联、SQLite主库/WAL/SHM索引及SHA-256。原始A/B包未覆盖或替换`manual-20260924-033644-b7b9f088`失败样本。
- 证据等级：上述均为`Test/VirtualPlc`、`SoftwareLoopOnly`。辅助API和合同测试不是WPF/WebView2实际原型启动点击，也不是真实设备验收。

## Deviations from Assessment

- 评估时下料目标写入映射、Z用途和同目标边界尚未确认；本轮用户已确认4x0003/5/7目标、4x000D/F/11反馈、命令4不作新Z运动但须核验复位安全Z，故按此实施而非继续澄清。协议Word提供点位方向/类型与复位时序；命令4映射及不动Z为用户确认的项目约定，不伪称Word逐字规定。
- 实际公共配置由001共享schema所有，新增可选Unload点必须同步001合同/说明/T088；这是评估文件清单外的必要最小扩围。生产下料数值仍未批准，真实设备缺目标由正式适配器在派发前阻断；Test值不作为生产默认。
- VirtualPlc不是原始前置拒绝的直接原因，但修复命令4语义需要其复位门禁与不新增Z运动的可信对照，因此最小修改该引擎；未修改心跳逻辑或扩大超时。

## Follow-ups

- 执行独立`$speckit-bug-test slug=station01-unload-protocol-alignment`：用实际WPF/WebView2原型既有单次启动入口，按原始失败场景复现/验收，保存点击、请求/回执/GET、下料点位审计、SQLite、最终页面状态；旧1不拒绝、同目标/失联未知不解锁及正常解锁终态分别核对。原始人工故障不能因本轮软件模拟正常闭环宣称已独立修复。
- 001 T088的专门配置验证、真实设备下料坐标/安全Z/容差批准及真机协议验收另行完成。此前间歇性心跳故障独立保留，不能凭本轮结果宣称解决。
