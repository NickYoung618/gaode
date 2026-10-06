# 第八批：008 自动多面主链阶段证据

日期：2026-09-25。当前代码与 Test 虚拟环境；Q03 正式 WPF **NotRun/Restricted**，没有翻面或 Final 事实。本报告是第七批之后的当前判断；第七批报告保留当时的历史证据。Q01/Q02/Q01-PARAM 已通过的正式页面 Test 虚拟证据不变。

版本核对：当前测试程序集 SHA256 `80821E7834304BCF49DDF5B801097053B5B0314F4EC010C7BBB4326CF03E1A60`；Q03版本化配方文件 SHA256 `FC629D78F6D87FB24AEF347B27A8D5AAD33C05DE118A1D4EFA07BD0CCD67A99F`；前端运行脚本 SHA256 `FC8301F3E07FDC66F351B2C8687C65455934E6D2E845E222D84B43E46CCC7CAE`。当前前端`node --check`及`npm --prefix frontend run build`通过，生成`frontend/dist`；构建不抵正式WPF操作。

## 阶段检查点与任务进度

| 阶段 | 本批交付与验证 | 涉及任务 | 状态及下一步 |
| --- | --- | --- | --- |
| 配方与预算 | 复用 R008-Q03/1.0.0-test、P01、面1/轮1和面2/轮2 Test 映射；冻结计划为 AB→Flip→Rescan→AB，4图、2融合、6次检测 worker 调用及1次整盘重扫。PrepareOnly 已生成独立 Test 夹具，未启动业务。 | 008 T050/T051、002 T11 | Q03 子范围已准备；目录仍 Restricted，整项保持未勾。后续无E自动路线待按同一数据模型增量配置，不能把可表达当成实跑。 |
| 目录准入 | Q03 受限原因细化为 `FlipPickPlaceTransmissionUnconfigured`；未以 Test 坐标放行未定义的翻面通信。目录及映射回归3/3通过。 | 002 T11、008 T050 | 当前准确拒绝，正式派发前仍需取放两组坐标提交合同。 |
| 公共移交 | 首轮只解析首面目标；未来轮在新3D结果提交后按面/轮/对象/槽位解析。旧首轮 callId、错面或错对象拒绝。规则测试3/3通过。 | 001 T090、008 T060 | 子范围已接线；正式多面移交仍等实际翻面。 |
| 多面执行 | 同一 `IntegratedDetectionPort` 遍历原冻结计划：首面AB后在Flip处写受限事实并停止；显式 `Test/PostFlipStageContext` 只用于组件后半段，实际执行第二轮3D媒体采集、独立Height worker、必要保存和复位，再解析第二面目标，用原AB单元逐图运动、采集、分析、保存、复位及融合。Host实现翻面反馈关联判别，当前取放通信未配置，所以没有实际Flip命令/面号派发。 | 008 T060、003 T071自动子范围、001 T090 | 组件能力交付；不能据此勾T060/T071或标Q03通过。补齐取放提交字段/顺序后，才可接正式Flip派发与后继。 |
| 页面与查询 | 媒体查询从已提交事实返回对象/面/轮次；现有媒体格支持同角色多份媒体轮选并显示面/轮/Test来源。`RunMediaCatalog`定向测试1/1、前端语法与构建通过。 | 006 T048/T049、003 T068查询子范围、007 T033 | 页面代码子范围可用，Q03仍无合法启动条件，未做WPF Q03操作；采证工具沿用，未伪造页面包。原整项未勾。 |
| 组件验证 | 当前代码单独运行后半段用例1/1通过；首面受限1/1、必要媒体保存失败1/1、错对象高度1/1、反馈关联3/3、公共移交3/3、目录3/3。见下列TRX及SQLite/媒体读回。 | 008 T060、003 T071 | 本阶段验证通过，仅覆盖组件及既有虚拟接口；下一检查点是正式翻面接口与WPF Q03整链。 |
| 正式WPF | 目录Restricted，故未从页面启动Q03，也未执行实际翻面、下料、取盘或Final。 | 008 T062、007 T033、006 T048/T049 | NotRun。Q03通过后才继续Q04以后两面/四面自动无E路线；本批没有后续Q的运行证据。 |

## 实际调用与读回

- 后半段定向TRX：`artifacts/recipe-execution-008/eighth-batch-testresults/q03-eighth-postflip-current.trx`，当前代码1/1通过。运行目录`artifacts/recipe-execution-008/eighth-batch/q03-c23e400154a84fbdbd3b5d05799877b6/`，读回`q03-postflip-readback.json`：run `78a74014-7c13-43cc-8f25-4b5308e1d9d2`，轮2/sample-a高度10.33 mm，Test偏置102 mm，派发第二面检测Z=112.33 mm；3份媒体、2次逐图检测和1次双输入融合，SQLite/媒体可读回。`source=Test/PostFlipStageContext`及`noPlcFlipEvidence=true`明确标记组件前置，不代表PLC已翻面。
- 首面AB后受限：`q03-eighth-first-face-gate.trx`及`artifacts/recipe-execution-008/eighth-batch/q03-2f4e4f2d59f240b9ac638a25d7af2c7c/q03-first-face-restricted.json`，没有写Flip目标面。必要媒体保存失败不产生后继产品运动：`q03-eighth-save-failure-frozen-plan.trx`。错对象高度无依赖运动：`q03-eighth-height-mismatch-frozen-plan.trx`。旧轮次/错面由`q03-eighth-handoff-height-gates-final.trx`覆盖。
- Host翻面反馈关联规则：`q03-eighth-flip-feedback.trx`，3/3；它校验当前连接代次、发令前状态与发令后新观测、状态2/目标面双重完成，不把旧完成值直接当本次完成。规则测试不是机械翻面证据。
- 目录`q03-eighth-catalog-regression.trx`3/3、媒体投影`q03-eighth-media-api.trx`1/1。早期组合运行与重构前失败TRX仍保留在同目录，不用后来的单测抹掉历史失败。当前成功版本的后半段测试沿原冻结计划/原步骤ID，不创建第二套检测计划。
- 本批复用第五/六批Q01/Q02/Q01-PARAM WPF、第四批单面AB/CD单元及第七批Q03受限数据/预算证据。没有重复启动既有页面或设备实例；本批组件使用隔离Test虚拟PLC、模拟媒体、独立Python worker、SQLite和媒体存储。

## 通信边界与未完成项

指定PLC协议§2.3/§3.1.5已定义：命令3到翻转工位，到位后写目标面；PLC内部完成整体取放翻转；上位机核验`Flip_Status=2`且`Flip_Current_Face=目标面`，人工区占用禁动。Host内部可记录动作、连接代次、发令前后状态并判别历史完成，无需新增序号寄存器或规定固定0→1→2序列。第七批把正常触发和全部反馈关联也列为外部缺口，现已纠正。

总时序图还要求取料点和放料点两组坐标；协议寄存器表及§3.1.5没有说明两组坐标具体通过哪些现有字段及何时提交。不能把通用`Camera_Target_X/Y/Z`或`Grab_Target_Z`未经依据当成两个翻面点，也不能只凭命令3及面号声称PLC收到完整取放参数。此单一通信映射缺口使正式Flip动作与后续第二轮3D/第二面检测/Final保持受限。重复翻面如果发令前已是完成2，还须观察本轮发令后的新状态变化；现有Host规则已处理此关联，不要求额外PLC清零协议。

剩余代码工作：来源明确后在003唯一自动翻面适配器接入取放提交、命令3到位、目标面、双反馈及必要意图/反馈持久化；008同一执行入口随后接正式Rescan和第二面。剩余配置工作：Q03取放点的版本化Test数值及后续无E自动两面/四面配方，均须建立在已定义字段语义上。剩余页面证据：Q03正式WPF选用/启动、同run第一面、PLC翻面、轮2 3D/第二面、保存、下料、解锁、取盘、Final及失败定位；Q03通过后逐条运行后续自动无E路线。现阶段不勾008 T050/T051/T060/T062、002 T11、001 T090、003 T071、006 T048/T049或007 T033的整项完成。
