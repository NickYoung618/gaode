# gaode-1：当前迁移与开发入口（2026-10-06）

GitHub 主仓库：https://github.com/NickYoung618/gaode 。首个迁移基线标签为 `migration-20261006`，此前没有可恢复的 Git 提交历史。完整迁移、现场接续开发与版本回退见[迁移与版本回退](迁移与版本回退.txt)。SDK、原型原始 ZIP、现场数据及详细运行证据随迁移包交付，不存入源码 Git。

当前主目录已包含 011/012/013/014 及 `016-public-preparation-tray-check-unload` 的集成，以及 [017 确定地址合入](specs/017-confirmed-plc-addresses/validation.md)。当前地址唯一来源为 `configuration/plc/confirmed-20261006/sources/PC.xls` 和 `PLC(2).xls`，共 85 项；PLC 心跳在 MB6038。完整地址目录已接入联调工具，正式业务合同仍有缺项，真实相机/光源/算法接入仍在现场完成，不能把迁移基线当作整机实测通过。

以下保留 2026-10-03 的 011 设计交接记录，仅描述当时状态；判断当前实现须结合对应规格、验证记录与上述迁移说明。

# 011 历史设计入口（2026-10-03）

当前011依据[20261001接口原件](高德_文档/new/PLC与上位机通信接口协议__20261001-最终版.docx)、[交互信号表](高德_文档/new/上下位机通讯交互信号表.xlsx)和[2026-10-03统一澄清](specs/011-plc-interaction-update/spec.md)。原件只读；旧20260925分区版及旧发布/运行包保留历史，不作为新协议已实现或通过的证明。

主项目位于`E:/dzk/gaode-1`；011文档工作副本位于`E:/dzk/gaode-1/workcopies/011-plc-interaction-update`，后者尚未复制完整源码/构建环境。Phase 1设计已完成，本次仅作设计定向对齐、architecture辅助评审与已完成文档集成，不执行历史运行命令。逐文件主项目合入与跨会话交接见[同步记录](specs/011-plc-interaction-update/clarification-sync-20261003.md)，文档交付不代表功能已实现或验证通过。

011负责共同模型、唯一业务校验、F料盘编号唯一绑定、运行冻结、通信与执行；012负责编辑保存/目录/API/前端。真实保存新内容供后续F使用，冻结运行不变；不新增发布审批或另一套执行。

首次3D提供有无/姿态/F XY，检测XYZ来自配置；翻转放回后复查姿态、F不重绑。支持更多检测面（AB/CD，四面3CD＋1AB）及四面后的可选独立E姿态。同盘OK原槽、NG/Pending各区、姿态异常跳过后续检测，最后从原槽实际分拣到Pending并输出物理槽号；适用分拣及保存后下料。

Host与独立VirtualPlc经真实通信接入；Test和真实设备共用业务，地址/原码/ASCII/内部握手留通信。原1502/5080等端口及File目录示例仅是旧运行配置，不批准新现场参数；共同字段与接口见[recipe-contract/1.3](specs/011-plc-interaction-update/contracts/recipe-contract.md)，Phase 1设计见[011计划](specs/011-plc-interaction-update/plan.md)；012最终13份设计已接收合入，原1.2消费差异已关闭；1.3 RC08补G-01生产端，D012-G01-receipt-1.3及7份新增量已接收合入，共同代码仍待实际交付。任务及逐文件结果见[当前交接](specs/011-plc-interaction-update/tasks-handoff-20261003.md)。F绑定保存软件事实，型号在实际翻转动作中下发，不增加旧配方ACK。正式地址、恢复、安全控制等延期见011 DEP。

后续只做受影响构建、组件回归、架构门禁和011/012联合代表链；旧`verify-latest-plc.py`及[003历史验证记录](specs/003-plc-latest-protocol/validation.md)不等于新流程验证，也不要求全量/面数穷举/009或010全历史重跑。实施替代后实际清理无用途旧协议、业务旁路、测试特权/配置/测试与孤立代码，保留有效保存、关联、取消、期限和历史证据。
