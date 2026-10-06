# Bug Verification: monitor-xyz-history
- Slug: monitor-xyz-history（explicit）
- Assessment: ./assessment.md
- Fix: ./fix.md
- Result: verified（本轮监控显示与协议名称范围）

## Summary
原列表完整XYZ发送/实际反馈，同值及0保留；独立栏目删除；XY协议名称恢复。本轮实际完成源码、设计、构建、离线重放及真实浏览器验证。未运行新Host/配方业务，不把工具或重放通过等同008整体验收。

## Checks Performed
|检查|命令/动作|结果|范围|
|---|---|---|---|
|源码必要回归|node --test scripts/tests/virtual-plc-monitor.test.cjs|13/13|同Y、同Z、全同连续两次、0；Move扫码/检测/下料及Sort取/放；真实反馈非目标；缺证据/旧命令/跨连接；去重/清空/缺口/有界；特殊HTTP；错误分类|
|当前源码编译|dotnet build VirtualPlc/VirtualPlc.csproj -c Debug --artifacts-path artifacts/monitor-xyz-history-r8-build|pass|0警告0错误；未改业务实现|
|实际编译程序|python -X utf8 本目录/verify-compiled.py|pass|新DLL独立HTTP/Modbus临时端口，只GET地址表及app.js，无Modbus写入/设备运动；PID和端口已释放|
|原始证据重放|python -X utf8 本目录/verify-browser.py|7/7|Q01、Q01-NG、Q02-PENDING-P03、Q03四份原始API/audit，加受控HTTP失败/心跳超时/渲染失败；原文件摘要不变|
|真实浏览器截图|python -X utf8 本目录/verify-browser-package.py 本目录/extracted/Gaode-008-Windows/VirtualPlc/wwwroot|pass|实际Edge/CDP，解压包原列表发送/反馈截图；仅滚动界面，不改显示数据|
|最终包|archive.py候选归档+全部载荷CRC/SHA256+解压资源比对|277项pass|7项r7载荷改变，270项不变；修改VirtualPlc重新编译，未变Host/worker/配方等逐文件复用|
|解压资源回归|GAODE_MONITOR_SCRIPT=解压app.js；node --test scripts/tests/virtual-plc-monitor.test.cjs|13/13|最终包使用当前消费者|
|只读一致性|consistency-verification.json|pass|当前合同/源码XY、独立DOM已删除、按地址采证保留、Word摘要不变、既有任务勾选不变|

## 实际截图
- [发送XYZ逐条记录](browser-package-focused-0-Q01-NG-healthy-send.png)：Camera_Target_X/Y及Grab_Target_Z，动作0:13、连接/事务与真实写入时间。
- [反馈XYZ逐条记录](browser-package-focused-0-Q01-NG-healthy.png)：Machine_Current_Pos_X/Y/Z，动作0:13与本次设备动作采样时间。

## 证据解释
四份原始业务run来自旧r3/r4，29动作/87轴范围保留；本轮显示共34/52/66/54条轴记录，含每次发送、动作阶段反馈及ZReset实际Z，不是新增业务运行次数。历史XYZ名称在原始文件中不改，仅当前页面对稳定4x0001/0002地址使用协议XY名。发送记录单值显示实际写入值；没有可靠前值时不伪造100→100。实际反馈为虚拟设备本次动作actual，不能冒称真实PLC或Host每次TCP读回。

## 保留的验证工具问题
首次CLI截图未准确滚动，另一次截图为空白；其DOM断言通过但截图不作最终显示证据。保留原文件，通过CDP实际滚动和等待浏览器绘制取得有效截图。未通过修改页面数据/图片生成截图。

## Residual Risks / Recommendation
关闭本缺陷限定的监控需求纠正。保留原xyz-sorting-deployment历史报告及partial范围：用户本机原RunningF仍缺同run日志，七格正式映射仍未确认。本轮未做真机/现场标定、没有新配方完整运行，不改变003 T065、008 T055/T070既有验收状态或勾选。
