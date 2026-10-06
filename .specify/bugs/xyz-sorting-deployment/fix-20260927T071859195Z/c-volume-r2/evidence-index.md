# 最终ZIP解压验收证据索引

- [固定ZIP验证证明](verification-proof.json)：精确ZIP SHA、4例版本/构建/检查及清理，范围明确为Test虚拟设备。
- [逐例原始结果](package-validation.json)：实际解压目录、runId、运行参数、入口/开始/结束及退出结果。
- [证据原件/副本与SHA](evidence-index.json)：C盘原件保留，四例页面、动作/报文、持久读回、实际DOM、截图副本在evidence/<Case>/。
- [专用任务调度](dispatch.json)、[实际固定接线](runner-used.ps1)、[任务收尾](task-cleanup.json)：最新请求消费、管理员Session2、原接线恢复、无旧请求重放。
- [最终解压入口](extracted-entries.json)：30个现有合法入口及依赖路径；其余入口仅依赖核对。
- [本轮显示单测](../final-monitor-unit-test.log)：5/5。
- [名称/方向/状态核查](../protocol-display-review.json)：只读协议及48个既有映射；XY名称保留，不新造信号。
- [实际依赖核验](../extracted-dependency-review-r2.json)：冻结配方摘要、F码和图片依赖。
- [源码变化/原件保留](../source-change-review.json)：原assessment及协议摘要未变，业务接口及任务勾选未改。

保留的失败不计通过：[279字符图片路径](../long-path-proof.json)、[首次候选失败](../package-validation-first-failure.json)、[短E盘失败](../short-path-r2/package-validation.json)、[真实晚提交窗口](../short-path-r2/save-blocker.json)、[存储探针限制](../storage-probe.json)。原失败ZIP和原run均保留。

本轮修复、范围和局限见缺陷根目录fix.md/test.md；不能以本索引关闭008或用当前代表覆盖其他验收条件。
