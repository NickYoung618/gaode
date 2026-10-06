# 0.3 交付说明

Windows x64自包含包：`packaging/plc-field-probe/Gaode-PlcProbe-0.3.0-20261005.zip`，旁边的sha256.txt为最终包哈希；开发构建与封包备份在C盘`.codex/artifacts/plc-field-probe/delivery`。

解压后双击Start.cmd；浏览器主入口填写X/Y/检测Z/扫码Z/抓取Z/R目标，按现场确认参数执行单轴闭环。真实连接参数未预填，默认只读；未确认反馈不判为通过。

内含40点现场模板、原始来源文件及哈希、README、FIELD-CODEX、INTEGRATION、实际源码与程序、015/016规格/合同/验证、TCP及浏览器证据。无需现场安装SDK/Python；现场参数可由中控机Codex填写并生成回传ZIP。

软件验证范围见validation.md；R实际反馈、型号编码、抓手反馈等未决项未编造。正式软件尚未完整接入本次现场协议，须回传后按INTEGRATION处理。
