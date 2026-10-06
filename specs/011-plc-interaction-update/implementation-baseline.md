# 011实施源码基线

2026-10-03，按本轮实施授权执行T001。来源E:/dzk/gaode-1；唯一开发/构建/运行根E:/dzk/gaode-1/workcopies/011-plc-interaction-update。绝对路径已核，未写主项目源码或012工作副本。

从主项目backend、VirtualPlc、scripts、frontend、desktop、workflows及global.json/dev入口补入691份文件，2份现有文档保留；[逐文件基线](implementation-source-baseline.json)记录693份来源/目标摘要。保留011当前spec/plan/contracts/tasks及交接文档；源码仅来自主项目基线，不复制012在制品。

排除嵌套workcopies、Git元数据、bin/obj、node_modules、TestResults、artifacts、dist/build、Python缓存/虚拟环境、运行实例/日志/证据、数据库、密钥/证书/环境秘密及压缩归档。前端现有源码资源保持原样。backend/VirtualPlc项目引用均存在；未复制运行库。

SDK 10.0.401已存在，符合global.json。主项目与副本均无Git仓库，未初始化Git/分支。未发现Docker、ESLint/Prettier、Terraform/Helm配置；frontend为private包，无新增ignore文件的适用触发。

清单只读：architecture 0/15勾选，requirements 14/16勾选；依用户本轮明确授权继续已确认范围，不改任何清单标记。扩展hooks为空。

此记录仅证明源码准备与路径/引用核对。后续共同字段变更须先迁移所选项目全部直接消费者，012所属文件仍由012迁移；源码已复制不证明Host可构建或新功能已通过。尚未运行构建、软件测试、设备或数据库。
