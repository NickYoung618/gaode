# 09:31 Administrator预检补充

实际Administrator/Session2状态：WPR recording in progress，File，记录时长12:09，Dropped event0；与Codex普通账号看不到录制的结果不同。不能把普通账号清单当系统全部会话。当前脚本拒绝启动正确，不修改状态判断或跳过冲突门禁。

找到sampling-20260927T011909797Z旧包：启动前无WPR，tcp-timeline 01:19:11.634Z—01:19:29.614Z；launcher.err.log明确dotnet不在PATH。没有result/cleanup/stop/ETL证据；估计录制起点01:19:12Z与该包吻合，但没有据此自行取得停止其他会话的授权。已询问用户确认本次录制归属。

原失败和全部文件保留；当前新版私有launcher已明确dotnet绝对目录PATH。save-previous-recording-20260927.ps1准备在用户确认后：再次检查录制起点匹配09:19±20秒，保存ETL后结束当前WPR；精确匹配旧launcher脚本/唯一runtime/创建时间且无后代时清理该进程；不停止桌面worker。可用-ResumeObservation串行启动一次新有界观察，无盲重跑。仅AST检查通过，尚未执行收尾。
