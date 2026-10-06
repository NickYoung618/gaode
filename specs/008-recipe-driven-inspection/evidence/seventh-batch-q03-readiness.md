# 第七批Q03：自动换面路线与当前受限边界

日期：2026-09-25。Q03 **未从正式页面启动，也未到Final**；当前目录为Restricted。Q01、Q02、Q01-PARAM的第五、六批Test虚拟正常运行证据保持有效，本批未重跑它们。

## 已核实的自动换面合同

- 指定PLC协议`高德_文档/PLC与上位机通信接口协议_最新版.docx` §2.3：`Flip_Target_Face`写目标面，PLC反馈`Flip_Status`（0空闲、1执行、2完成、3失败）及`Flip_Current_Face`；`Manual_Zone_Occupied=1`禁止运动。§3.1.5：`XY_Move_Cmd=3`去翻转工位，到位后写目标面2，状态2且实际面2才可继续整盘3D重扫，旧坐标严禁沿用。
- `高德_文档/上下位机对接/总时序图.svg`翻面段明确上位机下发含取料/放料坐标的整体翻面指令，PLC内部完成取料、翻转和放料，再反馈完成。无需上位机分步控制夹爪。
- 需规V1.1 FLP-002/003和CTL-005/008要求双重面号核验、固定点位表、PLC每次动作独立安全校验及旧完成状态不得抵本次；OPEN-06/24仍留接口细节。

选择**自动**路线，因为这些来源已给动作顺序、PLC内整体闭环和结果核验；本批不实现人工换面API/页面。两项准确缺口仍未在协议寄存器表、§3.1.5、总时序图、需规或现有Host代码找到：

1. 固定取料点和放料点分别通过哪些字段/寄存器提交，何时相对`XY_Move_Cmd=3`与`Flip_Target_Face`提交？协议只有公共`Camera_Target_X/Y/Z`、`Grab_Target_Z`及面号，不能证明它们就是翻面源、目标字段。
2. 新翻面怎样触发；旧`Flip_Status=2`由PLC还是上位机、在何时清零；上位机须观察哪一次空闲/执行/完成边沿以证明状态2属于本轮？现有VirtualPlc内部以`Flip_Target_Face=0`重新武装只是模拟实现，来源协议未把它定义为现场握手。

因此未派发命令3或目标面，也未把虚拟面号反馈冒称已批准动作。无需再询问已明确的双重核验、人工区禁止运动或PLC内部夹爪职责。待两项接口有来源并同步003合同时，才接003 T071的自动动作适配和008 T060的换面执行；缺口只阻塞这条换面后的动作，不撤销Q01/Q02的单面证据。

## 本批交付与证据

- `fixtures/recipes-q03.json`、`fixture-q03.json`、`worker-manifest-q03.json`、`media-manifest-q03.json`由`generate-q03-test.py`可重复生成。R008-Q03/1.0.0-test，F Test码`TEST-TRAY-0103`，P01同一对象，两面A/B，`1:1`和`2:2`分别保存Test目标/高度绑定，当前目录摘要`FC629D78F6D87FB24AEF347B27A8D5AAD33C05DE118A1D4EFA07BD0CCD67A99F`。坐标为SIM_MACHINE/mm、SIM_REFERENCE高度加显式Test偏置，生产坐标和标定未获批准。
- 002目录核验新增第二面映射/错轮次原因检查；完整Q03数据仍返回`FlipAndHeightContractUnconfirmed`。冻结计划有第一面AB、Flip、RescanWholeTray、第二面AB；008面/轮次顺序检查拒绝缺重扫或复用旧轮次。严格Detection继续拒绝未实现的`FlipMember`，不会静默跳过而完成。
- 预算从冻结计划算得4图、2融合、6次检测worker调用、1次整盘重扫；这只是绝对期限的输入，不证明第二轮实际3D已执行。
- 最终代码及Fixture定向验证`artifacts/recipe-execution-008/seventh-batch-testresults/q03-final-code-and-single-face-regression.trx`为4/4通过，含Q03映射/预算/无静默Flip与Q02目录回归；另有Q01/Q02目录回归`q01-q02-catalog-regression.trx`2/2通过。最终Fixture的PrepareOnly输出在`artifacts/recipe-execution-008/seventh-batch-prepared-q03-v2/`，无业务启动；首版`seventh-batch-prepared-q03/`保留历史，仅其Q01来源引用在最终Fixture中改为Q03并更新摘要。没有实际第二轮3D、第二面检测、同run媒体/SQLite或Final证据。

桌面核对时Administrator Session 2为Active；未发现本批隔离端口25152—25155监听，未启动或占用其他服务。由于正式目录受限，本批未打开WPF执行Q03，也未用辅助API代替页面。

## 第六批F失败定向复核

原始TRX：`artifacts/recipe-execution-008/sixth-batch-testresults/`下`final-gate.trx`、`sixth-batch-focused-gates.trx`、`sixth-batch-f-mismatch-isolated.trx`。合并测试的F不匹配run在TRX Host日志最终确实保存`InvalidOperationException:ExpectedRecipeFMismatch`，但测试在`Blocked`投影先出现、错误码仍空时断言失败；测试现改为等同run错误码提交后再断言，`artifacts/recipe-execution-008/seventh-batch-testresults/q03-f-mismatch-projection.trx`为1/1通过。这是取证时机修正，不调整PLC/心跳期限或重发运动。

旧`FinalUnloadCompletionIntegrationTests`的TRX则显示3D已完成，F固定点运动意图提交，F受理在原2秒期限内无匹配应答而超时。该旧夹具使用`s01-public-virtual/1.0.0`与100ms虚拟心跳；当前Q01/Q02使用`s01-public-virtual-loop/1.2.0`与不同隔离配置。现有旧TRX缺同事务PLC寄存器审计，尚不能唯一证明无应答的设备原因，失败仍保留。它不推翻当前构建三条WPF正常F链；Q03真正运行时仍须保留同事务Host/VirtualPlc日志并按原期限定位任何新异常。

## 后继实施与任务状态

008 T050/T051的Q03数据/预算子范围和T060的面轮次静态门禁已交付；实际Flip、第二轮3D、第二面动态目标解析及同面融合/Final仍未接线。003 T071未派发，003 T072/006 T050未做（本批选择自动），007 T033无需在无合法Q03启动时增加页面脚本。008 T060/T062及上述整项任务均保持未勾；T058保持原状态。待两个接口缺口解除后，先同步003合同双方，再实现单一自动翻面、第二轮实际3D和目标解析，最后由正式WPF运行Q03至Final并验证错面/旧高阻断。
