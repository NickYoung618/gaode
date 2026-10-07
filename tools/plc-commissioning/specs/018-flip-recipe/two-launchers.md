# 当前工作目录双启动入口
配方启动.cmd → Start-PLC.ps1 -View recipe → /recipe。
虚拟上位机联调.cmd及三个原联调入口 → -View manual → /。
独立包Start-PLC.ps1新增ValidateSet参数View与仅验证用NoBrowser，已运行复用服务；无自动PLC连接/写入或配方开始，不改变在途流程与心跳。共享工作目录启动脚本已备份，正式主工程未修改。
