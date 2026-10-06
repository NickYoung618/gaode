# 配置设计资产

日期2026-09-20，全部version=1.0.0、purpose=Test。文件是未来实现的方案输入示例，当前不能直接启动尚不存在的Host。所有坐标、范围、相机参数、高度和耗时为团队定义的合成测试数据，没有现场授权；不能把purpose改为Production后用于真机。

| 文件 | schema / 用途 |
| --- | --- |
| [public.test.json](public.test.json) | [公共schema](../contracts/public-config.schema.json)：3D/F本次3D定位XY、整盘范围、设备/能力、F单拍及测试解析器 |
| [budgets.test.json](budgets.test.json) | [预算schema](../contracts/budget.schema.json)：独立业务预算、队列/媒体限额 |
| [simulation.normal.json](simulation.normal.json) | [模拟schema](../contracts/simulation.schema.json)：实际时间正常延迟 |
| [simulation.controlled-normal.json](simulation.controlled-normal.json) | 同一正常响应，可控时钟 |
| [simulation.algorithm-timeouts.json](simulation.algorithm-timeouts.json) | 高度和F读码分别2000ms返回，超过1000/700ms预算；保留迟到反馈 |
| [simulation.motion-timeout.json](simulation.motion-timeout.json) | XY执行3000ms，含受理的总预算1500ms，首个3D移动即受限，不能进入F |
| [simulation.no-response.json](simulation.no-response.json) | 高度/F算法NoResponse，依实际业务期限有限结束 |
| [simulation.duplicate-late.json](simulation.duplicate-late.json) | 受理重复、两种算法超期重复返回；不重复物理动作或改终态 |

六份模拟profile是完整独立配置，通过publicConfigRef/budgetRef引用ID和版本，不使用隐式覆盖继承。新增变体须另给ID/版本并记录最终展开快照。具体耗时起点和预算见[时间合同](../contracts/configuration-time.md)。

测试驱动器必须在软件启动及安全条件后明确提供模拟实体按钮输入，配置不会自动代替按钮。deviceInitial只是模拟设备自己的初态，不证明实际装载或安全。

扩展用例：对七环节逐一建立strategy=Fail/outcome=Failure/failureCode=SimulatedFailure的版本，或NoResponse/outcome=Hold版本；设备NoResponse/outcome=Success表示内部完成但回执丢失，仍需核对。delay设为D-1/D/D+1验证边界（XY应计入plcAcceptance，不能误把执行延迟当总期限）；duplicateOffsetsMs相对于原响应时刻，有限次数且复用原身份。仅算法ignoreCancel=true允许在取消窗口后仍投递迟到测试响应；业务窗口仍拒绝它。

正常示例包含两项高度，sourceElementId不代表槽/件/面；不假定全盘同高。F输出TEST-TRAY-0001仅供注册的测试格式解析器产生trayIdentifier，不解析型号或配方。

Schema仅验证结构，语义校验必须执行：引用存在及版本一致、角色/能力兼容、min/max与点位/范围合法、用途/Provider、delay/strategy/outcome一致、参数上限及固定F/终点规则。算法配置问题单列，不能一概阻止启动。测试量未确认生产精度、覆盖、节拍或设备安全，OPEN保持未决。
