# 独立PLC联调工具源码 1.1.6

用于现场信号联调及成组成员夹爪选择核心验证。正式业务应用在仓库backend/frontend/desktop。工具当前14步配方仍是一盘一件；新增成组入口只确认夹爪选择，不执行轴运动/取放料/检测。

运行可用系统Python 3.12执行`python src/app.py --no-browser`，或从1.1.6完整交付包复制runtime目录后使用Start-PLC.ps1。构建ZIP需内置Windows x64 runtime；发布、日志及现场local配置不提交Git。Rollback脚本使用本电脑交付目录。

核心验证：
```powershell
python -m unittest discover -s tests -p test_member_gripper.py -v
python -m unittest discover -s tests -p test_same_position.py -v
node --check src/web/recipe.js
```

源协议PC.xls/PLC.xls保持原字节，协议地址未修改。分层、变更、验证及回退见[交付说明](../../specs/member-gripper-selection/delivery.md)。本次只执行上述核心验证，历史测试与交付脚本包含其原版本依赖，不作为本次全量验收结论。
