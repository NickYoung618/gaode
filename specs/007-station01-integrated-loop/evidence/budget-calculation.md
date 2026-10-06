# 007 Test 时间预算核算（2026-09-23）

配置：`examples/budget.virtual-loop.json`，`s01-budget-virtual-loop` v1.1.0，`purpose=Test`；旧 `s01-budget-virtual-plc` v1.0.0 未改。公共 3D/F 正常采集分别采用 8,000 ms 窗口，涵盖固定 4,000 ms 墙钟延迟及 4,000 ms 文件读取/调度余量；Height/FDecode 各 15,000 ms，涵盖 worker 固定 10,000 ms 计算及 5,000 ms IPC/调度余量。PLC I/O 单次期限 1,000 ms，心跳失联 8,000 ms；均为有限Test门限，超时仍阻断后续动作。配置由现有 `budget.schema.json` 加载，并由 Host 启动时冻结引用。

CAP/P01 的冻结配方 `R-S1-A-CAP` v`0.4.0-review` 预期 Detection Capture 步骤 C=2，逐项 worker 算法 A=2。按指定上界计算：`5C + 10A = 30 秒`；留给正式阶段事件/媒体保存/IPC/PLC外的步骤 `T=20 秒`，原重试预留 `R=14 秒`，则 `30+20+14=64<120 秒`，阶段余量至少 56 秒。正常固定延迟为 `4C+10A=28 秒`。T/R 是本次受控样本的预算假设，最终仍须按实际计划与时间戳核对；若实际计划或耗时超过上限，不删步骤、不缩短指定延迟，也不把 Pending 当成正常完成。
