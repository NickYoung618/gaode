# 2026-09-29 打包输入核验补充
比较通用build.py产物与用户实际r8：通用入口缺失Start-Test.cmd、Select-Test.ps1、测试配方清单.md，带入额外重复frontend资源及Node采证依赖，与本轮只更新监控的约束不符。不能将该候选视为最终包。生成ZIP时磁盘不足，失败文件已保留在本会话C盘workspace的r9-failed-general-package-20260929.partial.zip；不是可运行交付。

修正依据：用户授权项目整体对齐并保留已验收能力。沿现有build.py→archive.py入口，显式指定已验证r8 ZIP作为未变组件基线；仅生成已修改监控/VirtualPlc/说明/Start文件的小型覆盖暂存，再从基线流式归档完整ZIP、逐文件摘要/CRC核验。暂存不是完整可运行包，最终ZIP才是交付。不修改既有r8、源码业务、配方或前端。恢复三份原有选择入口到打包源目录作为可审阅来源，保持字节相同。新build参数须显式--base-package并记录基线摘要；不重复物化大资源。

新增变更：packaging/windows-local-20260927/archive.py、Select-Test.ps1、Start-Test.cmd、测试配方清单.md；与原评估已有build.py/README/Start同步。验证最终ZIP与r8只有本轮预期差异；实际解压VirtualPlc及资源运行、真实浏览器、16项监控回归；其余全部载荷流式CRC/摘要验证，未重跑业务主流程。
