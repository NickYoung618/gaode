# 008及直接关联功能协议对齐记录

日期：2026-09-26。范围为既有001、002、003-plc-latest-protocol、006、007、008及直接受影响的治理/派生需求/接入说明；不新建功能替代008。**文档已对齐、代码尚待实施。**

## 唯一依据及保护范围

完整读取[协议正文及全部表格](../../高德_文档/PLC与上位机通信接口协议_最新版_上下位机信号分区版.docx)，实测SHA256等于`405AC9EE2AE2D765951D9F523DC7195CC77F6CD1F38DBC0AD144A0013586C519`。说明文件/20260925核查只是索引，未代替原文。协议原件/备份、客户资料/流程图、原型ZIP、历史报告/analysis/tasks-history、运行包/SQLite/媒体/TRX、源码/测试/实际配置/fixture/生成器/脚本保持只读；AGENTS.md与技能/工具配置不改。无Git，未初始化或提交。

修改前已建立文件哈希清单和全部既有tasks任务状态快照，并独立备份可写文档以便差异核验；最终摘要见[静态验证](static-validation-20260926.md)，逐文件分类见[影响清单](file-impact-20260926.md)。历史证据只引用，不重写当时结论。packaging/q01-q02-local/README.md是打包源说明，仅标明旧协议范围；既有发布包不改。

## Spec Kit依赖与脚本副作用

已先读AGENTS.md及constitution、specify、plan、tasks、analyze五项SKILL.md。按constitution→specify→plan→tasks推进必要增量，不重建模板或新功能。

- constitution：通过resolve-template.ps1读取有效模板；P03的强制面间高度刷新与新协议冲突，改成逐面目标/来源核验，5.0.0→6.0.0为不兼容原则变更；其他P01—P13内容/编号保留。constitution-alignment当前摘要修正，旧裁决正文保留历史。
- specify：读取有效spec-template，仅更新既有008及直接关联规格。跳过mkdir/复制spec模板/feature.json写入；现有feature.json已指008。
- plan：检查setup-plan.ps1及common.ps1后执行；既有plan分支只跳过模板复制。按技能Phase0调用两个只读研究代理核查实际消费者，主代理编辑文档。无技术选型扩张；未知设备/业务输入局部登记。
- tasks：setup-tasks.ps1只读现有spec/plan和解析模板，未重生tasks。保留编号/勾选，修改未完成项目标，已完成旧任务（如003 T067、specs/007-station01-integrated-loop T031）增加明确20260925协议NotRun子范围。specs/003-plc-latest-protocol T064旧下料增量由同目录T069承接；不误改003-ingress-argument-validation。
- analyze：检查check-prerequisites.ps1及common.ps1，当前未设置异目录覆盖，读取既有feature.json不会持久改写；最终只读检查spec/plan/tasks、依赖/链接/保护范围。发现授权内文档问题退出只读分析阶段修正文档后，再复核；不进入implement。

extensions.yml为hooks:{}，无前后钩子。解析出的旧模板含部分过时策略接口建议；模板只读，不复制这些建议覆盖当前宪章/有效文件。frontend build.mjs会删dist；fixture生成器会覆写JSON和摘要；启动/采证脚本会启动业务，均只读未执行。

## 实际核查范围和事实区分

文件枚举/关键词扫描覆盖当前backend/src、backend/tests、VirtualPlc、frontend、scripts、specs、治理及项目说明；排除bin/obj/node_modules及运行归档作为当前实现依据。人工语义核对聚焦匹配到的协议映射、动作状态机、调用/消费、计划/目录/目标/预算/完成/媒体/页面及相关文档上下文，具体文件与改动见影响清单和[实施清单](implementation-checklist-20260926.md)。**不声称对全仓每行审查。**

Office：解析需求Word正文与52张表；读取跟踪表13张工作表及源单元格，对受影响ID/OPEN/点位/来源逐项对照；保留未变表、样式和开发状态。11张SVG均提取可读文本，核对普通翻面、分拣、下料与特殊旋转实际流程；未凭关键词推断不可读图像细节。

| 类别 | 当前事实 |
| --- | --- |
| 有效要求 | 新版零件翻面/Flip_OK、同盘两段分拣/Sorting_OK、命令4抓取Z、翻后无3D和新普通盘末顺序 |
| 当前实现 | 映射缺ACK、Flip正式受限、目录/规划强制Rescan、面与测量轮耦合、状态2分拣完成、0007及XY-only下料、Sorting先于Unload；详见实施清单 |
| 已有可复用能力 | 公共3D/F、AB/CD定位采集/单图分析/复位/双图融合、独立worker/媒体/SQLite/宿主及页面采证；不重建 |
| 目标设计 | execution/3.0、recipe-api/3.0、review-recipe-catalog/0.5及独立阶段/测量/配置/连接身份；尚未被当前程序消费 |
| 旧验证 | Q01/Q02旧协议正式WPF正常路线2/22、Q01-PARAM/C08历史通过；Q03只有第二轮3D/第二面组件，没有实际Flip或页面Final |
| 新版验证 | Q0/22、C0/8，必要F待按影响验证；本轮文档检查不增加业务通过数 |
| 未变范围 | Q01—Q22、C01—C08覆盖目标、固定XY/固定对焦/3D仅Z、后台业务边界、必要真实交互/保存及3秒心跳 |

## 条款→需求→合同→任务→消费者→验证

任务引用完整目录见下表，子编号同属该目录；不把不同功能同号任务混用。

| 协议/来源 | 需求ID及008 FR | spec/contract | 唯一任务 | 当前代码/配置消费者 | 必要验证 |
| --- | --- | --- | --- | --- | --- |
| §2.3/2.4/2.8 | CTL-009、HMI-006；FR-014/018 | 003 spec及plc-stage-action-port；008 execution E04 | specs/003-plc-latest-protocol T071；来源矩阵T069；specs/007-station01-integrated-loop T031/T033 | ProtocolLatestMap、PlcAddressMap、DeviceMessages、适配器、VirtualPlc app.js、启动manifest/来源矩阵 | 0054/0055方向Int16、预留0056、版本/SHA一致，真机校准不外推 |
| §3.1.5①—⑥ | FLP-002/003；FR-005/012 | 008 spec US2、execution E04；003设备合同 | specs/003-plc-latest-protocol T071；specs/008-recipe-driven-inspection T050/T060/T062 | PLC适配器/模拟引擎、IntegratedDetectionPort、flipPosition | 连续不同实体同面完整定位/双反馈/ACK；错面/ACK失败不继续 |
| §3.1.5⑦ | ID-006/007、FLP-004、POS-002；FR-002/004/005/018 | 008 data-model/目标映射；001 persistence-handoff；002 recipe-execution | specs/001-station01-public-preparation T090；specs/002-plc-xyz-recipes T11；specs/008-recipe-driven-inspection T050/T052/T053/T054/T060 | Planner、Catalog、ResolveTargetsForRound、Coordinator、DetectionStepTarget、实际目录/fixture | 无Rescan；初始测量真实、面2合法独立目标；错来源不运动，不填0/复用上一面XYZ |
| §3.1.5⑧ | FLP-005、SAF-009；FR-005/012/016 | 008 execution/API；006独立接口/原型映射 | specs/003-plc-latest-protocol T072；specs/006-frontend-station01-console T050/T051；specs/008-recipe-driven-inspection T060/T068/T069 | Manual反馈、控制API、runtime.js | 占用1禁动、确认1→占用0→清确认；人工面/持件恢复按局部决定 |
| §3.1.6③—⑧ | SRT-001/003/004/005/006/008；FR-010 | 008 execution/data-model；003 stage action | specs/003-plc-latest-protocol T071；specs/008-recipe-driven-inspection T057/T059 | StageActionAdapter、VirtualPlc、SortingMapper/Allocator、ThreeStage执行器 | 真实非连续源槽、同盘目标、状态2仅在途、状态3及ACK后提交；失败4/满盘5不假完成 |
| §3.1.6②/⑨ | SRT-009、TASK-008；FR-011/016 | 003 FR16—18/whole-tray；008 API；006绑定 | specs/003-plc-latest-protocol T069/T068；specs/006-frontend-station01-console T049；specs/008-recipe-driven-inspection T054/T062 | cmd4/000B、ThreeStage、WholeTrayOrchestrator/Store、allowedActions/runtime | 先下料再分拣；到位不准取盘；未保存/未解锁不Final，旧来源不混入 |
| §3.1.7 | ID-001、CTL-010；FR-001/002/006 | 001 device/handoff、003 detection、008 E02/E04 | specs/003-plc-latest-protocol T067/T070；specs/001-station01-public-preparation T090 | FScanStep、LatestProtocolPlcDevice、VirtualPlc、绑定/保存 | F3/4与检测1/2对应轴复位/清零；解码/绑定/保存失败不放行；E不外推 |
| §1.3及实际步序 | CTL-002/007、DAT-008；FR-013/014 | 008 E06/plan、007 virtual integration | specs/008-recipe-driven-inspection T051/T069；specs/007-station01-integrated-loop T033 | Workload/Budget、阶段期限、结构化日志、采证工具 | 无翻后重扫耗时，实际Flip/取放/ACK预算；3秒心跳；必要失败持久定位 |
| §3.1.5⑦及实际采集 | HMI-002/003/006、DAT-001/003；FR-014/016 | 008 API/data-model、006 API/model | specs/008-recipe-driven-inspection T053/T054；specs/003-plc-latest-protocol T068；specs/006-frontend-station01-console T048/T049 | FaceResultAggregator、媒体事件/RunMediaCatalog、runtime.js/build.mjs | 面/阶段/测量来源分开，跨面不混图，不生成第二次3D或假标签 |

## 剩余局部输入和协议差异

| 登记 | 分类与明确部分 | 剩余限制/任务 |
| --- | --- | --- |
| B01-F/OPEN-29/D04 | F已定义且已有旧版组件实现 | 新协议来源与整链验证；specs/003-plc-latest-protocol T067；E另B01-E/OPEN-03，仅阻塞E |
| B02/OPEN-06/24/D05 | 普通翻面及同盘分拣已定义，旧两组翻面字段外部阻塞撤销 | 代码/数值待齐；特殊进出旋转站/恢复仍局部待输入；specs/003-plc-latest-protocol T071、specs/008-recipe-driven-inspection T057/T060/T065 |
| B03/OPEN-07/D06 | 特殊类型1流程图给进出站、姿态和三出口 | 角度/方向/占用/反馈未定义，不能用Flip_OK推定；仅008 T065/T067相关动作 |
| B04/OPEN-08/26/27/D01/D03 | 固定XY及3D仅Z决定保持；逐面Test映射可明确配置 | 生产逐面Z基准/轴/占用、地址/字节序和标定待实机；缺哪面只限制哪面，不假测量 |
| B05/OPEN-30/D09 | 场景/成员/面数已有来源 | 生产逐面相机映射/组处置/布局待相应输入；specs/008-recipe-driven-inspection T063/T066，不全局阻塞普通Q03 |
| B06/OPEN-03/16/D10 | 内部身份不由E替代，单一NG/Pending可先验证 | E必扫/放行及混合NG+Pending决定仍缺；只限制依赖分支 |
| B07/OPEN-10/11/12/24/D07/D11 | 人工占用禁动和完成确认/清零已明确 | 真机启动/夹紧、实际人工操作/面确认/持件/恢复仍缺具体子决定，T072/T050/T051/T068按子范围 |
| B08/OPEN-05/14/D08 | PC管理配方/点位，Recipe_ID只显示 | 协议§1.6/5.1与§2.5内部表/Zone_Config_Ack范围存在差异；缓存/容量不从虚拟15/15外推 |
| B09 | 旧页面成功事实可复用；新身份/阶段/取盘绑定待实施 | 原型只读，仅现有控件；实际人工控件不足局部登记，不重建页面；新版负载心跳保持3秒 |

具体冲突：§2.2/§3.1.2/§3.1.3写3D求XY，与既有固定XY决定不同，D01保留固定XY，本轮不新增算法；§2.5示教运行示例写Camera_Target_Z，与§3.1.5/3.1.6专用抓取Z不同，普通翻面/分拣/下料按专门时序000B，示教不在本轮实现；§4.1重试/跳过与§3.1.6失败锁停、§4.3人工恢复的具体持件条件仍局部未定，不自动重放未知机械动作。旧SVG分拣后下料与新§3.1.6普通盘末不同，普通链按新文；特殊旋转按自身来源不套用。

## 当前进度与后续停止点

本次文档交付后停止。后续先新版普通OK Q03完整链，检查点为真实Flip/ACK、一次初始3D、两面合法目标/检测、必要保存、新下料顺序、解锁及页面取盘Final；未满足不得记Passed。通过后再其余适用两/四面，保留NG/Pending/E/组/整体/旋转/恢复最终范围。完整可执行顺序及下一条提示词见[实施清单](implementation-checklist-20260926.md)。
