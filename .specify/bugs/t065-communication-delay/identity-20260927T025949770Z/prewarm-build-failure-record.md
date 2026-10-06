# 初始私有预热构建准备失败记录

本条从本轮exec/read工具实际返回的console记录补录，不冒称未保留的初始日志文件；该日志路径随后被显式restore构建输出复用。

实际首命令含 --no-restore 和全新 prewarm-build artifacts路径，返回exit1、NETSDK1004：找不到资产文件 prewarm-build/obj/Gaode.Host/project.assets.json，须运行NuGet还原；0警告1错误、1.06秒。未启动任何设备。去掉 --no-restore 后独立构建成功。最终build日志为成功那次，不能将它写成首命令成功。
