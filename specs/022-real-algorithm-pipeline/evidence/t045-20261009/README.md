# T045离线软件验证证据

基线7cbba95；仅Test用途，无实机/真实算法接入声明。

- attempt-index.json：全部TRX的原SHA256、用例结果和错误消息，含失败尝试。
- before-fix-valid-context.trx：有效夹具的修复前复现，3个关闭先用例均误进入，3个许可先通过。
- t045-host-stream.trx：最终实际Host7/7通过；原业务预算不变。
- handoff-readback.json：六项已通过交接的RunId/CallId、端口调用数、SQLite重读原/最终释放时间和关联诊断。
- source-manifest.json：相对基线修改文件及新增交接测试摘要；不包含本摘要文件自身或证据文件的递归摘要。

最终有效覆盖来自不同批次：final/t045-components.trx的原组件12项通过（同批六项新测试末尾日志键名断言失败，后来纠正测试为既有JSON的CallId）；verified/t045-handoff-and-host.trx的六项交接通过；host-stream的七项Host通过。共25个不同必要用例，不是单批25/25。

早期夹具v2缺expectedRecipeRef、日志键名不匹配、Windows EventLog权限异常、SQLite清理文件占用及短预算前置I/O超时均未追溯改写为通过。仅修测试宿主日志/代理隔离及共用持续打开的文件日志；保留原预算、断言和失败记录。完整原始证据在隔离工作树artifacts/T045；未提交测试数据库或原始媒体。
