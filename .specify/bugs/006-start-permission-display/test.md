# Bug Verification: 启动权限拒绝显示丢失

- Slug: 006-start-permission-display（沿评估/修复上下文）
- Verified: 2026-09-27
- Assessment: ./assessment.md
- Fix: ./fix.md
- Status: verified

原始r20 job000 Auth401 exit1/cleanup=true，9项中仅页面权限状态false，保留原包。修复后job001 Auth401及job002 Auth403均exit0/cleanup=true，各9/9。真实页面选择和一次Start→Host实际401/403→StartFailed同状态→现有区域稳定权限受限；SQLite Runs/Commands/Writes/Operations/AlgorithmCalls/StageEvents各0、PLC action0，没有运行/Final。

frontend-build.log exit0；frontend-after.json四份HTML及客户ZIP全部不变，实际runtime SHA 3E6BCE15A6A560761CC055FD6E8009BEC8E6A2E64E792E5D830837CA372BBB29。两个interactive-desktop.json均记录本SHA。Host/PLC使用原r15/原PLC，同default GC及原业务期限；显式Test I/O设置范围保持。

证据：artifacts/recipe-execution-008/r20-auth-0927/queue及runs/job-00{0,1,2}-Q01/Q01。负向身份仅覆盖启动请求，不替代生产登录或全面权限验收；正常分支未变，正常路线沿既有适用包复用，不重跑矩阵。
