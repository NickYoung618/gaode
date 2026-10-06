# 008 独立 worker 单/双输入（2026-09-25）

状态：**007 T032 组件交付完成**；008 T053 的同面检测接线及 Q01 正式前端验收尚未执行。

- `station01-worker/2.0` 在 Host/独立 Python 两端显式传 1 或 2 个媒体身份、租约、相对路径和 SHA-256。Host 在受控媒体根求摘要；worker 实际读文件、校验长度/摘要，使用全部输入摘要计算结果，按输入索引逐一释放。Host 校验会话、调用、尝试、角色、租约和每个释放索引/摘要；缺媒体在派发前失败，不能产生算法成功。
- 两次实际独立进程调用：`backend/tests/Gaode.Integration.Tests/TestResults/worker-two-input-graceful.trx` 2/2；各自 10 秒 Test 模拟延迟，单输入 `Detected`、双输入 `Fused`，结果包含对应媒体 ID，审计含各输入释放。缺字节用例 `worker-missing-final.trx` 1/1，错调用 ID/错捕获/无 worker 用例 `worker-failures-008.trx` 3/3。协议编解码在 `008-foundation-contracts.trx` 对应 3 项通过。原始 Test/Simulated 审计与结果在 `artifacts/recipe-execution-008/worker-contract/` 各独立目录。
- 本轮排查保留失败 TRX：首次测试因 Windows 审计文件占用失败；随后发现 `RequestStopAsync` 写 Shutdown 后立即 kill 可截断末尾审计，现改为最多等待 1 秒自然退出再兜底终止。`worker-two-input-graceful.trx` 是修正后结果，不覆盖旧失败。

该证据只证明组件输入/输出和来源，未证明 A/B 实际采集、逐图保存、同面融合接入业务链或算法精度。
