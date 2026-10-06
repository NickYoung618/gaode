# Bug Fix: XYZ监控显示与分拣测试交付

- Slug: xyz-sorting-deployment（用户显式指定）
- Fixed: 2026-09-27
- Assessment: ./assessment.md；增量依据 ./assessment-supplement-20260927.md
- Status: applied

## Summary

复用已有PLC动作审计与原始Modbus写入收据，在虚拟PLC监控分别展示完整发送XYZ和实际反馈XYZ，同值不省略，并修正数字方向枚举的显示。新部署包补已有普通NG/Pending、成组、整体和特殊取放入口及其合法依赖。业务适配器、下料/分拣/整盘门禁未修改。

## Changes

| 文件 | 修改 | 说明 |
|---|---|---|
| VirtualPlc/wwwroot/app.js | 修正并补动作显示 | 发送坐标从本动作写入引用、同连接命令前实际RawWords解码；反馈使用动作审计实际值。缺字段不以目标或快照填补。特殊HTTP Test路径明确来源与动作ID。 |
| VirtualPlc/wwwroot/index.html、styles.css | 增加完整动作记录区域 | 与数值变化列表分开；保留协议英文XY名称；客户确认WPF原型不变。 |
| scripts/tests/virtual-plc-monitor.test.cjs | 必要显示回归 | 数字0/1方向、未知方向、同值XYZ、缺收据与跨连接不拼接；原刷新/错误恢复检查。 |
| packaging/windows-local-20260927/build.py | 补现有输入和依赖 | Q01-NG、Q02-PENDING-P03及成组/整体/特殊入口；按实际imageManifest所在目录解析图片相对路径，保留配方摘要。 |
| packaging/windows-local-20260927/archive.py | 支持先固定ZIP再验收 | 明确candidate仅表示校验交付文件，不写运行通过；最终运行结果绑定不变ZIP摘要，独立侧文件保存。 |
| packaging/windows-local-20260927/README.md | 增加操作说明 | 普通OK留原位、命令4下料、NG/Pending顺序和特殊OK回原位含义。生成Start-NG.cmd/Start-Pending.cmd。 |

## Tests Added or Updated

`node --test scripts/tests/virtual-plc-monitor.test.cjs`：5/5。新增同值发送/反馈分离、连接归属及缺失不补造检查。
最终ZIP解压后实际WPF与监控浏览器、原始报文及SQLite读回验证由本目录verify-package.ps1、verify-monitor.cjs、verify-sorting.py完成；尚在接续bug-test，不以此文件预先声明正式包运行通过。

## Local Verification

局部显示测试已通过。48个现有映射信号已与只读协议核查：46个地址表条目方向一致；Alarm_Bits/Alarm_Severity对应报警章节，未自行新造地址或状态。检查详情protocol-point-review.json及protocol-tables.json。状态文字修正包括XY_Pos_Confirmed=0运动中、取料2与放料3区别、Inspection/Z_Reset握手。

## Deviations from Assessment

用户追加明确保留UnloadPreparation和特殊OK搬运，因此增加已有ROT-PART-OK必要代表。共享接口无需修改，沿用现有GET audit/special-actions。部署输入的图片路径应相对实际mediaManifest而非fixture目录，打包时修正该必要工具路径解析。交付验证使用新ZIP实际解压文件，不借用开发机历史Passed作为本轮结果。

## Follow-ups

接续`speckit-bug-test slug=xyz-sorting-deployment`。本轮时间戳目录fix-20260927T071859195Z保存修改前副本及所有新结果。最终状态以test.md和固定ZIP验证侧文件为准；用户原本地run尚未取得，不回写历史失败或任务勾选。

## 最终包验收增量
首次最终ZIP解压Q01在RunningF被阻断；同run Height worker InputMediaUnavailable。long-path-proof.json确认普通路径279字符is_file=false，扩展路径文件及SHA正确。这不计通过。保留ZIP、页面、SQLite、worker及任务失败，下一候选仅核准协议Retry/Alarm显示文字和说明，改用短且带空格的独立解压目录验收；业务二进制和期限不变。

2026-09-27 15:54增量：最终r2 ZIP固定后，实际短C盘解压验收Q01、Q01-NG、Q02-PENDING-P03、ROT-PART-OK全部通过显示、正式页面/媒体/SQLite读回、取放及ACK顺序和清理。实跑后载荷及ZIP摘要一致。短E盘独立保存晚提交失败仍保留，不以C盘通过认定存储根因解除。详情见test.md及fix-20260927T071859195Z/c-volume-r2/verification-proof.json；交付文件为Gaode-008-Windows-x64-20260927-xyz-sorting-r2.zip。原assessment、旧ZIP/失败、业务源码和任务勾选保持。
