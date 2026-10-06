# 008 首批 F 扫码握手（2026-09-25）

状态：**003 T067 的限定组件交付完成**；不把正式 F 协议等同 E 或产品运动规则，不代表 Q01 完整路线通过。

- 正式 Host `LatestProtocolPlcDevice` 对 F 在本轮公共 XY、Scan Z 运动反馈及实际 XYZ/连接代次匹配后写 `Inspection_Status=3`，清移动命令；保持既有命令清除观察间隔后写 4，等待扫码 Z 复位 2 才清 0。移动后的复位反馈须先为 0，防止前轮 2 被消费。完成等待有明确超时，失败保留 4 和原始原因。定向测试曾暴露快速 3→4 使虚拟 PLC 未观察到 3，此间隔修正后 `f-handshake-hold.trx` 1/1。
- `VirtualPlcEngine` 仅在已受理命令 5、本轮 3、移动命令已清时受理 4 并复位；下一有效运动受理才清扫码位反馈。`PlcDataStore` 拒绝从 3 清 0，即使已有旧复位 2；复位失败 3 不允许从 4 清 0。
- `FScanStep` 只有在采集/算法事实已成功持久提交后才进入完成握手；解码无合法码可保存失败事实，但不会假绑定。采集或保存失败不写 4。另一 Test 模拟 PLC 适配也按 F 角色写 3/4，以维持相同应用合同。

证据：`backend/tests/Gaode.Contracts.Tests/TestResults/f-boundaries-final.trx` 5/5，含正式 Modbus Host/VirtualPlc 正常与注入扫码 Z 复位失败、旧复位 2/状态 3 不许清 0、Test 模拟采集与必要算法事实保存失败不写 4；正式独立 worker 返回无匹配 F 的受控 API/SQLite 运行 `backend/tests/Gaode.Integration.Tests/TestResults/f-decode-failure-008.trx` 1/1，不产生旧配方计划或分拣。与心跳/诊断一起的当前合同组 `heartbeat-f-hold-current.trx` 6/6。此前 `008-foundation-contracts.trx` 9/10 的心跳失败继续保留。以上均不是完整 Q01。

独立风险：003 T065 的当前构建心跳稳定性仍未按其单独条件验收；产品运动及 Q01 整链分别归 003 T070、008 T055。本任务完成只覆盖 F 合同组件。
