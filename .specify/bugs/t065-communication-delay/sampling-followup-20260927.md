# 管理员采样启动反馈（2026-09-27）

截图异常对应已有WPR录制保护，不是字符串识别错误。

- `sampling-20260927T012026523Z/wpr-before.txt`明确为`WPR recording is in progress...`，持续75秒，File模式，Dropped event=0；同目录ETW列表包括两个WPR collector。此数值只描述该检查时刻，不证明完整采样无丢失。
- 更早的`sampling-20260927T011909797Z/wpr-before.txt`显示未录制；该目录launcher.err.log记录找不到dotnet。未找到该轮result.json/最终清理记录时，不宣称采样完成、自动清理成功或通信未复现。
- 后续普通账户WPR状态为未录制，但不能用不同执行上下文的结果保证管理员录制归属及状态。未执行任何stop/cancel或设备动作。

修正仅涉及缺陷目录采样工具：为所属启动器子进程PATH加入固定dotnet、Python312、pwsh目录，预先核验工具存在及摘要；启动器失败立即写诊断标记，使观察器结束并进入已有清理。父进程/系统PATH不修改，业务代码与原参数不改。原脚本及冻结清单保存为`*-v1`，旧证据不覆写；当前清单已同步启动器摘要及工具摘要。

验证见`evidence-20260927/script-validation-v2.json`：AST与ReviewOnly通过，管理员端到端尚未实跑。本修正解决已证实的工具路径缺口，不是T065业务补丁，不关闭T065/T055/T070。

下一步仍在管理员PowerShell执行同一脚本命令一次。若仍报告已有录制，停止该次尝试，保留记录，不盲目取消或连续重跑；先确认原录制的归属与结束情况。无需重新开发已验收能力。

## 并发修改核验（优先于上文工具修正描述）

本轮收尾发现`observe-default-20260927.ps1`已被其他执行方替换：当前增加PreflightOnly，读取`observation-freeze-20260927.json`，不是本轮修改的sampling-inputs启动方案。因此停止继续覆盖该脚本。script-validation-v2仅证明检查时脚本AST通过，ReviewOnly返回当前另一套冻结清单；**不能据此宣称本轮子进程PATH修正在当前入口生效**。本轮保留的启动器/输入清单及v1副本供当前执行方审阅，不能让用户基于尚未核验的并发版本连续重跑。
