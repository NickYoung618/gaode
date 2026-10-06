> 当前受控升级参数顺序已按实际Program/StoreMaintenance核对：`--upgrade-public-tray-test <allowed-root> <owned-store-root>`。本轮真实已验收014测试库的DB/WAL/SHM稳定快照与SQLite备份在E:/dzk-delivery/016-integration-20261006/schema-upgrade-test；合并后使用主项目当前StorePrep构建，allowed-root指该目录，owned-store-root指其isolated-store子目录。下方旧轮次store-root/archive-root写法只保留历史，不作为执行指令。默认开发库/未知用途库/生产库不升级。

> 2026-10-06当前续修记录：以下旧轮次结论保留历史属性。当前副本验收唯一入口为artifacts/016-public-tray-flow/integrated-copy-acceptance-2/result.json，主项目集成唯一入口为E:/dzk/gaode-1/artifacts/016-public-tray-flow/integrated-main-acceptance-1/result.json。文件尚未产生或结果Rejected均不得称通过。验收Passed后按本轮用户授权自动E盘外部备份、逐文件三方合并、主项目重新构建和隔离验证；不在每阶段等待提示。最终交付状态/完整性及证据见delivery-package/integrated-20261006，旧Rejected保持。
>
> 已接收012最新导航与014完成派发/首故障/稳定事件键/harness首失败保护，接收快照及SHA见artifacts/016-public-tray-flow/reception-20261006/manifest.json。必要集合固定46+补充53=99个C#、18个前端组件，含四条016正式软件链、七条当前自动多组/程序集义务行、一个014真实双特殊件链；所有数据行独立登记且原始失败不删。迁移审计与FixtureOnly判定器自检独立，原初始登记及历史义务不改写。运行条件NativeThreadPool=0、SocketsInline=1、原ServerGC；未改生产默认或产品期限。
>
> 本轮产品修复读取已持久StageId并统一面身份，避免真实多组历史查询重复；测试代理透传实际SSE，浏览器刷新不再等待整条流结束。新增夹具仅合法当前配方/TrayPose准备，实读PNG、真实异步SQLite及2000ms保护保持。原d1仅Started且无数据库行，具体迟延阶段仍未知。副本与主项目Passed分别核验，不宣称正式PLC或生产验收。现有开发库不自动升级；/2→/3只在有来源的专属隔离备份执行，Host不自动DDL。

# 新016合并交接

本交付是独立副本的可审阅增量，不自动合并E:/dzk/gaode-1。[delivery-manifest.json](delivery-manifest.json)引用delivery-package/file-manifest.json、delta-files.zip、original-recovery.zip、source-current-comparison.json和seal.json。增量ZIP为当前准确字节；恢复ZIP只包含本次修改的原有文件原始字节。review.diff为去除换行格式噪声的审阅视图，不作为字节恢复依据。完整515MB基线、运行库、缓存、令牌及旧大型artifacts不打包。

合并顺序：

1. 核验最终result=Passed、phase=all及seal的源码/实际构建/ZIP摘要。保留原型zip和来源文档只读。
2. 对file-manifest每条比较原始baseline SHA、本次copy SHA和合并时当前主目录SHA。当前仍等于baseline才可直接接受增量；已等于copy为已存在；双方均变必须用恢复原件/当前主目录/交付文件三方合并。新路径已有不同内容同样三方处理。不得把本副本整目录覆盖回主目录。
3. 共享活动spec/contracts/data-model/plan与增量tasks先核对，再合代码。document-sync-changes.json逐块给旧规则/新规则与行号；旧任务ID/勾选、历史报告和013输入保留。其他同号016不重编号、不改内容。
4. 边界登记按真实职责逐条合并009-boundary-inventory.json及009-public-shapes.json，保留他人新增职责和有限形状，不整体替换。新字段/成员及现有磁盘维护工具的分类不是豁免；维护脚本源文件未改，本流程不执行它。
5. 存储安装必须先于新Host。新测试库由既有StorePrep按s01-store/3准备；已有已知/2测试库关闭Host、取得专属维护guard和真实备份后运行：`dotnet backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll --upgrade-public-tray-test <store-root> <archive-root>`。原/1先走已有/1→/2受控入口，再/2→/3。按实际部署路径调用，不对来源目录执行。保留备份/intent/receipt，核对StoreId、精确schema、业务与媒体摘要后再启动；不由Host自动DDL、不回填检测/分拣事件、不重写旧JSON。维护中断按既有guard/intent处理；本次未承诺通用灾备平台。
6. 新保存/执行配方使用recipe-definition/5，旧/2,/3,/4按原版本原值读取。散件组成员须填实际唯一格位/物理槽/坐标并经过共同校验、正式保存和冻结；历史缺项不按组父槽或编号自动补。003/011/012当前合同随增量一起接受。
7. 合并后在主项目新的专属测试库/端口上重跑具名最小回归、原型校验及当前009/010门禁，记录主项目当前身份；不复用本副本成功凭据冒充主项目通过。

`.specify/feature.json`、本副本Git和隔离元数据不合并。需要继续Spec Kit时由合并者选择新016目录。现场Real轴语义、正式3D身份与完整覆盖来源须由真实供应方落实；现有拒绝保持，不填假值。新SDK设备、额外边界加固和013性能不在本次软件接受范围。
