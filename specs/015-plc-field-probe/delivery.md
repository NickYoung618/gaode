# 015 交付记录

日期：2026-10-05。版本0.2.0。开发软件范围完成，现场运行待参数及点表。

- 包：`packaging/plc-field-probe/Gaode-PlcProbe-0.2.0-20261005.zip`
- 大小：104655字节；75个文件，含脚本、配置模板、现场Codex说明、规格和开发验证证据。
- SHA256：`c2e3ff1947629ecddde5d9abad0ec70d6990f7809f7b20c1959600219333c774`
- 已读取ZIP逐项核对package-manifest大小/哈希和压缩完整性；真实PLC未测试。
- 源码入口：`scripts/communication/plc-field-probe/Probe.ps1`。
- 现场Codex入口：包内`FIELD-CODEX.md`；可编辑配置`site.json`和记录`field-notes.json`由模板创建，不预填现场参数。
- 回传：`Export.cmd`或`Export.ps1`生成PLC-return-*.zip，包含成功/失败记录，不自动上传。
- 本次Spec Kit目录已记录到`.specify/feature.json`；spec/plan/contracts/tasks/checklist/validation均在015独立目录。其他规格/任务、共享业务代码、来源及原型保持原状。

开发机已验证只读、心跳、单轴X、必要阻断/通信异常和回传；具体来源及复用证据见validation.md。配置支持其他已定义轴，不等于已经逐轴验证真实机构。初始化/复位、组合动作与正式软件代表链按现场回传推进。
