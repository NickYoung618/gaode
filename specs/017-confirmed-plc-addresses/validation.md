# 017 验证记录（2026-10-06）

范围：确定地址合入、BYTE访问、显式Profile入口及0.4联调基线。hardwareTested=false；未连接现场PLC/相机，未验收正式整机或中控机正在开发的简化界面。

## 已通过

- XLS原件SHA及85项逐行对照，共享表和联调模板一致：artifacts/017-confirmed-plc-addresses/source-comparison.json。
- 正式Host构建成功，0警告0错误：`dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --no-restore`。
- 通信定向测试21/21：ConfirmedMemoryLayoutTests、ProductionDefinitionTests、ProtocolDefinitionAdmissionTests。最终记录artifacts/017-confirmed-plc-addresses/tests/confirmed-addresses-final-3.trx。包括最新地址、可配置PDU、BOOL高低字节、两连接竞争读改写保护、旧Test图准入，以及Real Profile缺项在网络I/O前拒绝。
- 009实际源码边界/依赖检查6/6：ProtocolRepositoryBoundaryTests、DependencyRulesTests。最终记录tests/address-boundary-final-3.trx，完整扫描在artifacts/recipe-execution-008/009-isolation/017-confirmed-addresses-final-3-20261006/csharp-boundary.json。
- 工具6组验证通过：field_layout、primary、codecs、failures、motion_failure、browser。实际执行发布EXE，使用独立TCP服务及Microsoft Edge；85项读取，X=12.5、R=-30.25目标及本次运动/到位/实际值/清零，BOOL邻字节保留、REAL四字序、超时/异常包记录、旧到位拒绝、导出SHA及页面操作。最终目录artifacts/017-confirmed-plc-addresses/tool-tests/20261006-162213，包含results.json、源/程序SHA和截图。
- 发布目录启动检查通过：删除测试进程PATH中的dotnet入口，并把DOTNET_ROOT指向不存在位置后，实际自包含EXE可启动；读取85项配置及6轴。记录artifacts/017-confirmed-plc-addresses/packaged-entry-check.json。空IP仍明确报错；端口/UnitId/PDU/字序已有初值，未校准不挡只读。

## 过程中发现和修正

1. 工具旧动作入口仅接受Int16启动，导致第一次新版X动作被拒绝；已按新表改为BoolByte，最终X/R闭环与浏览器验证均通过。
2. 第一次架构运行把证据目录放到检查器不允许的路径，保留失败TRX，后续使用既定009受控目录，没有放宽检查器。
3. Host直接引用ProtocolDefinition/字序及新增原始配置属性触发架构边界；已把Profile加载、字序选择、准入日志移入Infrastructure，Host只传路径字符串。最终6项检查通过，没有更改检查规则。
4. 故意让两条测试连接让出线程的并发用例，在多组工具同时运行时触发5秒测试看门狗。将该类纳入既有CommunicationTcp测试集合，保留用例内部两连接并发，使用20秒测试看门狗及3轮置位/清零。最终21项通过。未修改任何生产心跳/动作期限。
5. 所有此前失败结果均保留在artifacts/017-confirmed-plc-addresses/tests及tool-tests；不合并成“从未失败”。

## 工作流及剩余工作

.specify/feature.json为只读；标准RequireTasks命令因其写回副作用失败。已使用显式SPECIFY_FEATURE_DIRECTORY和PathsOnly解析新017，并实际读取spec/contract/plan/tasks；未修改只读标志、旧功能状态或用户规则。017无checklists目录；extensions.yml的hooks为空，前后置hook均无须执行。

正式执行图还缺10项语义定义；只投影40项同名已知信号，没有用旧虚拟地址补齐。其余45项保存在完整共享表及工具，不伪装成业务已消费。实际Modbus映射、型号编码、启动/报警合同对齐、机械参数、真实相机/光源/算法仍按用户决定在现场解决。

正式迁移时以当前源码及本轮change-manifest.json为准；旧迁移审计是历史检查点。用户要求的简化联调界面在中控机独立目录继续开发，详见交接提示词，回传后按基线合并。
