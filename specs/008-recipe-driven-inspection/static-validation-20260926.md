# 静态一致性与保护范围核查

日期：2026-09-26。仅文档、链接、Office字段、任务及文件保护检查；未运行build/test、生成器、业务启动、数据库迁移或采证。analyze只读检查，发现问题后退出分析在授权文档修订阶段修正，再只读复核；本记录在文档交付阶段保存，不覆盖历史analysis.md。

## 结论与准确边界

既有008及直接关联文档已对齐，代码尚待实施。最终复核未发现阻止按清单启动普通Q03实施的现行文档冲突；未将源码旧规则或历史证据当作现行要求。新协议Q=0/22、C=0/8，必要F尚待按影响验证；旧Q01/Q02/PARAM通过和Q03仅组件事实分别保留。

保护核验有明确例外：9个运行目录`last-watch-error.json`在两次哈希采样之间变化，本轮未执行写入这些文件的操作，写入方未确认；未回写。它们是连接失败观测文件，不能据此证明历史运行包全部字节不变。协议原件/备份、外部原型ZIP、历史报告/证据正文/analysis/tasks-history、源码/测试/配置/fixture/生成器/脚本在所建基线内未检测到其他变化。

## 已发现并修正的文档问题

| ID | 类别/原严重度 | 位置 | 修正和复核 |
| --- | --- | --- | --- |
| C1 | 宪章冲突/CRITICAL | constitution P03及008数据/执行契约 | 删除面间强制高度刷新；合法逐面目标与初始真实测量分离，宪章6.0.0 |
| I1 | 时序冲突/HIGH | 003/007/008当前合同和派生需求 | Flip_OK/Sorting_OK完整握手、状态2仅取料、000B下料和普通盘末先下料后分拣 |
| I2 | 当前/历史混用/HIGH | 008 spec/plan/tasks/索引 | 旧Q03第二轮3D与受限判断归历史；新版优先Q03完整链，无翻后重扫 |
| I3 | 页面确认冲突/HIGH | 006 spec/API/原型映射、007 spec | 旧客户端自动确认归历史；当前已有正式页面控件调用确认，原型只读 |
| I4 | 预算冲突/HIGH | 003/007指南、008预算 | 当前3秒心跳；按冻结动作/ACK推导期限，不沿用共享120秒 |
| I5 | Office不同步/MEDIUM | SRS Word及跟踪表 | 受影响ID/OPEN/点位同步；补Word修订行/SHA和真实新协议超链接；恢复POS-001/003未受影响说明 |
| I6 | 引用/路径/MEDIUM | 003 tasks、002 tasks及跨功能引用 | 修正eighth-batch相对链接；RecipeRunPlan实际定义在RecipeContracts；现行跨功能引用采用目录+ID |

## 008覆盖分析

spec包含18个FR和6个SC；tasks保留22项T049—T070，均未勾选。24项均有显式任务追溯，未发现008未映射需求或无归属任务；这只是文档覆盖，不是24项已实现。US1—US6保留，Q01—Q22/C01—C08/必要F范围不缩减。

| 需求/成功条件 | 任务/所有者 | 覆盖用途 |
| --- | --- | --- |
| FR-001 | specs/002-plc-xyz-recipes T11、specs/003-plc-latest-protocol T067/T068、specs/001-station01-public-preparation T090；specs/008-recipe-driven-inspection T055/T069 | Q全体，F1；S0/S1 |
| FR-002 | specs/003-plc-latest-protocol T070；specs/008-recipe-driven-inspection T052—T055/T069 | Q全体，F2/F4；S1起 |
| FR-003 | specs/007-station01-integrated-loop T032；specs/008-recipe-driven-inspection T053/T056/T059 | Q01/Q02及后续，F3；S1/S2 |
| FR-004 | specs/002-plc-xyz-recipes T11；specs/008-recipe-driven-inspection T050/T052/T062 | Q01—Q22；S0—S3 |
| FR-005 | specs/003-plc-latest-protocol T071/T072、specs/006-frontend-station01-console T050；specs/008-recipe-driven-inspection T060/T062 | Q03—Q22/C02/F2；S3 |
| FR-006 | specs/003-plc-latest-protocol T067、specs/001-station01-public-preparation T090；specs/008-recipe-driven-inspection T061/T062 | 公共F全Q、C02的E；S1/S3 |
| FR-007 | specs/008-recipe-driven-inspection T063/T066/T069 | C03/F6；S4 |
| FR-008 | specs/008-recipe-driven-inspection T064—T067 | C04/C05；S4 |
| FR-009 | specs/003-plc-latest-protocol T071；specs/008-recipe-driven-inspection T065/T067 | C05/F2/F5；S4 |
| FR-010 | specs/003-plc-latest-protocol T071；specs/008-recipe-driven-inspection T057/T059/T064—T067 | C06/C03—05/F5/F6；S2/S4 |
| FR-011 | specs/003-plc-latest-protocol T069、specs/006-frontend-station01-console T049；specs/008-recipe-driven-inspection T055及后续全部完整运行 | C06/Q全体/F4/F6；S1起 |
| FR-012 | specs/003-plc-latest-protocol T072、specs/006-frontend-station01-console T050/T051；specs/008-recipe-driven-inspection T060/T068/T069 | C02/C07/F5；S3/S5 |
| FR-013 | specs/008-recipe-driven-inspection T051/T055/T059/T062 | 全Q调用量/实际耗时；S0起 |
| FR-014 | 各实现内日志；specs/008-recipe-driven-inspection T054/T069 | F1—F6；随阶段 |
| FR-015 | specs/007-station01-integrated-loop T033；specs/008-recipe-driven-inspection T055/T058/T059/T062/T066/T067/T069/T070 | Q22/C8/F必要集；S1—S5 |
| FR-016 | specs/003-plc-latest-protocol T068/T072、specs/006-frontend-station01-console T048—T051；上述完整运行 | 全Q及C02/C07；S0起 |
| FR-017 | specs/002-plc-xyz-recipes T11；specs/008-recipe-driven-inspection T050/T058 | C08/Q01-PARAM；S2 |
| FR-018 | specs/002-plc-xyz-recipes T11、specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T068；specs/008-recipe-driven-inspection T049—T051/T058/T069 | 全Q/F1；S0起 |
| SC-001 | specs/008-recipe-driven-inspection T055/T059/T062/T070 | 22/22逐Q正式完整证据 |
| SC-002 | specs/008-recipe-driven-inspection T058/T070 | 同程序新/变配方实际执行变化 |
| SC-003 | specs/008-recipe-driven-inspection T054及所有运行/T070 | 真实PLC/采集/worker/DB/媒体 |
| SC-004 | specs/008-recipe-driven-inspection T069/T070及阶段失败证据 | F1—F6，必要子项不能漏 |
| SC-005 | specs/001-station01-public-preparation T090、specs/003-plc-latest-protocol T068、specs/006-frontend-station01-console T048—T051；specs/008-recipe-driven-inspection T055/T070 | 选择/绑定/冻结/保存一致，人工确认 |
| SC-006 | specs/008-recipe-driven-inspection T058/T059/T062/T066/T067/T069/T070 | C01—C08 |

实现顺序为共享协议/自动翻面/下料→合法逐面目标/数据/预算→页面Q03完整Final→其余适用自动两/四面；实际前置按子能力交付，不等待无关父任务整项验收。普通OK合法不需搬运仍记录判定依据。E/特殊旋转/组与混合处置/生产标定/实际人工恢复按B表限制对应分支，未被删除或虚构解决。

## 静态检查结果

- prerequisites解析到既有`specs/008-recipe-driven-inspection`，spec/plan/tasks齐全；extensions无hooks，没有创建替代功能或覆盖模板。
- 当前修改文档及新交付的相对Markdown链接进行平衡括号解析，带括号的来源XLSX按实际路径核验；文档交付完成后复核无缺失链接。任务正文中的未来evidence路径是待交付产物，不宣称文件已存在。
- 现行完整功能目录+任务ID引用均能在对应tasks中找到；各唯一实现所有者及子范围前置见tasks增量表。历史引用按原上下文理解，不跨功能混号。
- execution/3.0、recipe-api/3.0、review-recipe-catalog/0.5均为目标文档合同，现行程序/配置未升级；旧0.4单面数据与旧运行证据保留边界，不伪称程序已消费新schema。
- Word 52张表、XLSX 13张工作表结构保留。Word受影响需求/OPEN/M/D等可按稳定首列匹配的109行与MD读回一致；协议超链接已指分区版，新增修订行含原文SHA。
- XLSX仅修改sheet1/2/3/7/11/12和workbook定义范围，其他ZIP成员保持原字节；sheet2全部I列开发状态未改变。Word仅document.xml及对应rels修改，图片等其他成员保持原字节。
- 所备份6份tasks的274个编号/勾选序列全部一致；未新勾业务实现或验收任务。67个既有文档修改、新增5个交付文件；逐文件见影响清单。
- 基线22277文件全部仍存在；排除node_modules/bin/obj/.git的范围已明确。协议SHA与用户给定值一致，关键文件前后哈希和9个例外逐项记录见保护JSON。全量哈希临时基线与文档修改前副本仍保留本机，不初始化Git。

## 剩余项与后续动作

未关闭的设备/业务输入是局部实施限制，不是用文档推测的答案；详见对齐记录B01—B09/OPEN/D表。保护例外使“所有运行归档完全未变”不能作为本轮结论；未发现本轮越权写入，且不覆盖其他写入方的数据。

后续按[实施清单](implementation-checklist-20260926.md)及其提示词另行实施。本轮到文档交付和只读复核结束，未进入implement。
