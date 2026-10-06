# 第一工位流程冲突修复任务历史归档

- **归档日期**：2026-09-22
- **归档阶段**：Phase 11：第一工位流程冲突修复；US1：按方案 B 完成第一工位启动、夹紧观察和完成解锁
- **归档原因**：T011-T022 与后续修正后的 T023-T034 存在重复执行范围。为保留审计追溯和原始勾选状态，将旧任务块完整归档；当前执行只使用 T023-T034。
- **原任务状态**：T011-T022 原文中的任务均为未勾选 `[ ]`，归档不将任何旧任务伪造为完成。
- **原任务块 UTF-8 内容 SHA-256**：406f38ceb7e0e4065bb921c6b4dc1030833edb78c0ee38528166e4938a2615f5

## 原任务勾选状态

| 原任务 | 原始状态 |
| --- | --- |
| T011 | `[ ]` |
| T012 | `[ ]` |
| T013 | `[ ]` |
| T014 | `[ ]` |
| T015 | `[ ]` |
| T016 | `[ ]` |
| T017 | `[ ]` |
| T018 | `[ ]` |
| T019 | `[ ]` |
| T020 | `[ ]` |
| T021 | `[ ]` |
| T022 | `[ ]` |

## 原任务块（原文完整保留）

## Phase 11: 第一工位流程冲突修复

**依据**：`spec.md` 方案 B、`plan.md` 第一工位信号顺序、`contracts.md` 方案 B 料盘夹紧合同、`data-model.md` 和 `quickstart.md`。

### US1：按方案 B 完成第一工位启动、夹紧观察和完成解锁

**目标**：人工放盘并满足实体启动条件后由 PLC 内部夹紧；Host 只读取 `Pallet_Lock_Status`，启动阶段不写 `Pallet_Lock_Cmd=1`，公共流程完成时才写 `Pallet_Lock_Cmd=0` 解锁。

**独立完成条件**：正常流程、夹紧失败、夹紧观察超时、断联、禁止启动锁紧写入和完成解锁均有可复核的软件证据；未知状态不会推进到区域握手、3D 或 F。

### 问题复现与合同准备

- [ ] T011 [US1] 在 `backend/tests/Gaode.Contracts.Tests/Station01/StartClampStepTests.cs` 和 `backend/tests/Gaode.Contracts.Tests/Simulation/SimulatedPlcTests.cs` 增加当前错误行为的回归基线：证明旧实现把独立 `ClampCompleted` 事件和启动阶段夹紧完成等待作为流程准入，并记录实际失败或错误推进证据。
- [ ] T012 [US1] 在 `backend/src/Gaode.Infrastructure/Devices/Plc/ProtocolLatestMap.cs`、`specs/003-plc-latest-protocol/contracts.md` 和 `backend/tests/Gaode.Contracts.Tests/Devices/LatestPlcProtocolTests.cs` 中固定方案 B 点位边界：`Pallet_Lock_Status(4x0024)` 只读，启动阶段禁止写 `Pallet_Lock_Cmd(4x0023)=1`，完成解锁只允许写 `Pallet_Lock_Cmd=0`；完成条件是读写方向和禁止写入断言通过。

### 后端流程与适配器实现

- [ ] T013 [US1] 修改 `backend/src/Gaode.Application/Station01/Steps/StartClampStep.cs`、`backend/src/Gaode.Application/Station01/StartPublicPreparation.cs` 和相关状态模型，将夹紧完成从独立 Host 事件改为实体启动后的 `Pallet_Lock_Status=1` 观察；`=2` 进入锁停，`0` 在业务期限内保持时进入 `Unknown/Held`，未满足状态不得进入区域握手或 3D。
- [ ] T014 [US1] 在 `backend/src/Gaode.Application/Ports/IPlcStatePort.cs`、`backend/src/Gaode.Application/Ports/IPlcActionPort.cs`、`backend/src/Gaode.Application/Ports/DeviceMessages.cs` 和相关 Application 代码中分离启动受理、实体启动、夹紧状态事实和完成解锁结果；增加受控的流程完成解锁端口或等价动作合同，禁止以 `ClampStarted`/预期值代替 PLC 状态事实。
- [ ] T015 [P] [US1] 修改 `backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.cs`：保持 `4x0024` 轮询和状态新鲜度校验，启动阶段不写 `4x0023=1`；在公共流程完成且允许取盘的受控路径写 `4x0023=0`，分别处理写入失败、断联、状态仍为 1 和连接代次变化。
- [ ] T016 [P] [US1] 修改 `backend/src/Gaode.Infrastructure/Simulation/SimulatedPlc.cs`、`backend/src/Gaode.Infrastructure/Simulation/SimulatedDeviceState.cs` 和模拟配置，使实体启动后由模拟设备自行产生 `Pallet_Lock_Status`，不要求 Host 写 `Pallet_Lock_Cmd=1`；支持状态成功、失败、保持 0 超时、迟到反馈和断联场景。
- [ ] T017 [P] [US1] 修改 `VirtualPlc/VirtualPlcEngine.cs`、`VirtualPlc/PlcAddressMap.cs` 及必要的 `VirtualPlc/SimulationModels.cs` 配置，使 VirtualPlc 按方案 B 内部执行夹紧，保留 `4x0023/4x0024` 协议映射，仅接受完成阶段的 `Pallet_Lock_Cmd=0` 解锁，并记录解锁失败状态。

### 合同、规则与集成验证

- [ ] T018 [P] [US1] 在 `backend/tests/Gaode.Contracts.Tests/Station01/StartClampStepTests.cs`、`backend/tests/Gaode.Contracts.Tests/Simulation/SimulatedPlcTests.cs` 和必要的 Port 测试中验证正常方案 B：实体启动后状态由 `0→1`，Host 未写 `Pallet_Lock_Cmd=1`，读到 `1` 后才允许区域握手/第一条移动，完成时写 `0` 解锁。
- [ ] T019 [P] [US1] 在 `backend/tests/Gaode.Contracts.Tests/Station01/StartClampStepTests.cs` 和 `backend/tests/Gaode.Contracts.Tests/Simulation/SimulatedPlcTests.cs` 中验证失败和超时：状态 `2` 锁停、状态 `0` 超时为 `Unknown/Held`、心跳/连接代次变化不自动重发、不推进 3D/F，解锁写入失败不释放设备占用。
- [ ] T020 [US1] 在 `backend/tests/Gaode.Integration.Tests/Station01/NormalPublicPreparationTests.cs`、`backend/tests/Gaode.Integration.Tests/Support/Station01HostFixture.cs` 和现有独立进程验证入口中覆盖 Host↔VirtualPlc 的启动、内部夹紧、区域握手、3D/F 前置条件和完成解锁顺序；证据必须记录实际点位写入和读取顺序。
- [ ] T021 [US1] 更新 `specs/003-plc-latest-protocol/validation.md` 和 `specs/003-plc-latest-protocol/quickstart.md` 的验证证据模板，运行受影响的 Rules、Contracts、Integration 测试并记录结果路径；不得把模拟通过写成真实 PLC 或现场验收通过。

### 依赖与执行顺序

```text
T011 → T012 → T013/T014 → T015/T016/T017 → T018/T019 → T020 → T021
```

T015、T016、T017 分别修改真实 PLC 适配、进程内模拟和 VirtualPlc，只有在 T013/T014 的 Application 合同完成后才能并行；T018 与 T019 可在对应实现完成后并行。其余任务按依赖顺序执行。

### 关键规则覆盖

| 规则 | 任务 | 完成证据 |
| --- | --- | --- |
| PLC 内部夹紧、Host 只读锁紧状态 | T013、T015、T016、T017 | 启动路径无 `Pallet_Lock_Cmd=1` 写入，状态 `1` 才准入 |
| 锁紧失败和观察超时不得推进 | T013、T018、T019 | `2` 锁停，`0` 超时为 `Unknown/Held` |
| 命令受理、实体启动、物理夹紧事实分离 | T012、T014、T018 | 独立事件/状态和关联证据 |
| 完成阶段只写 `Pallet_Lock_Cmd=0` 解锁 | T014、T015、T017、T020 | 写入顺序、结果和失败处置记录 |
| 断联、迟到、重复和旧状态不自动续跑 | T016、T018、T019、T020 | 连接代次、状态新鲜度和无重复动作证据 |

### 收尾检查

- [ ] T022 [US1] 核对 `spec.md`、`plan.md`、`contracts.md`、`tasks.md`、`quickstart.md` 和 `validation.md` 的方案 B 术语与点位一致，并保存最终测试证据索引。

## 新旧任务对应关系

| 原任务 | 新任务 | 对应说明 |
| --- | --- | --- |
| T011 | T023 | 旧流程问题复现与错误推进基线 |
| T012 | T025、T026、T030 | 状态只读、命令方向、禁止启动写入和合同测试 |
| T013 | T024、T031 | StartClampStep 状态准入、失败和超时 |
| T014 | T025、T027 | 应用端口边界、完成解锁与结果观察 |
| T015 | T026、T027 | 真实 PLC 适配器读状态、写 0 和解锁失败 |
| T016 | T029、T032 | 进程内模拟状态、超时和断联 |
| T017 | T028、T032 | VirtualPlc 内部夹紧、解锁和断联模拟 |
| T018 | T030、T033 | 正常启动准入与整盘完成解锁验证 |
| T019 | T031、T032、T033 | 失败、超时、断联和解锁失败验证 |
| T020 | T032、T033 | Host↔VirtualPlc 集成时序和点位证据 |
| T021 | T034 | 回归测试、验证记录和证据路径 |
| T022 | T034 | 规格、计划、合同、任务和验证的一致性收尾检查 |
