# Bug Verification: XYZ监控与分拣部署入口

- Slug: xyz-sorting-deployment（用户显式指定）
- Verified: 2026-09-27
- Fix: [fix.md](fix.md)
- Result: **verified，限本轮显示及Test分拣交付范围**

## Summary

最终ZIP先固定再解压，在带空格的短C盘路径实际运行包内WPF/WebView2、Host、VirtualPlc、Python算法、SQLite与媒体。四个必要代表全部完成页面启动、实际设备动作、Final、刷新/重开、数据库读回及资源清理；监控从本次Modbus写入收据和实际动作反馈分别展示完整XYZ，同值仍显示。没有修改业务运动、质量规则、期限、接口或任务勾选。

## Checks Performed

| 检查 | 实际命令/操作 | 结果 | 范围 |
|---|---|---|---|
| 显示必要回归 | `E:/nodejs/node.exe --test scripts/tests/virtual-plc-monitor.test.cjs` | 5/5 | 数字方向、同值XYZ、发送/反馈分离、不同连接不混配、缺失不补造、原轮询/错误可见 |
| 最终包 | `python packaging/windows-local-20260927/archive.py artifacts/windows-package-20260927T073439Z Gaode-008-Windows-x64-20260927-xyz-sorting-r2.zip --candidate` | pass | CRC、全载荷摘要；压缩包建立在实跑之前，候选不预写业务Passed |
| 解压包正式流程 | 已授权固定任务执行本目录 `verify-package.ps1`；每例 `& <解压根>/Start.ps1 -Case <下表入口> -Headless -ApiPort 27101 -PlcApiPort 27180 -PlcPort 27102` | 4/4 | 再启动包内真实WPF，由包内 `capture-station01-webview2-normal.cjs` 点击页面；未用API假启动替代页面 |
| 实际监控显示 | `node <本缺陷>/verify-monitor.cjs 27104 <解压根> <同run目录> http://127.0.0.1:27180 <入口>` | 4/4 | 独立实际Edge读取服务页面，核对实际DOM全部目标动作及发送/反馈/Z用途；服务JS与解压文件字节一致 |
| 正式主流程回归 | `python <解压根>/scripts/validate-008-operation-evidence.py <同run目录> <本次prepared fixture>` | 4/4 | 冻结配方、相机/算法输入及融合、媒体、运动XYZ、整盘尾链、SQLite、页面刷新/重开 |
| 取放协议顺序 | `python <本缺陷>/verify-sorting.py <同run目录> <入口>` | 4/4 | 下料命令4；取料2后清命令、提交真实槽位/XYZ、放料3、ACK置1、PLC状态清0、ACK清0 |
| 依赖交付 | 最终解压文件、cases.json、冻结摘要及实际输入清单核对 | pass | 30个已有合法入口；其余26个仅依赖核对，未冒称实跑 |
| 打包一致性与清理 | 同ZIP SHA及实跑后全载荷摘要复核；各例Stop及PID/监听核对 | pass | 所属PID/五个独立监听端口均释放；专用任务空闲，临时固定接线已恢复 |
| 其他矩阵/真实硬件 | 未运行 | not-run | 不扩大本次范围；不以Test虚拟设备替代现场硬件验收 |

## 逐例入口、构建与结果

| Case入口 | 冻结配方版本 | 本次runId | 结果 |
|---|---|---|---|
| Q01 / Start.cmd | R008-Q01 / 1.1.1-test | 2b915fb7-62be-4fd8-854f-ba8336e9b4cb | 完整XYZ与同值Y/Z可见；命令4、整盘完成/解锁/取盘确认/Final；普通OK无盘末取放 |
| Q01-NG / Start-NG.cmd | R008-Q01 / 1.1.2-test-night | fa9f0a1e-d115-46a8-b216-b7eb983c4196 | P01→配置NG区P14；实际取放及清零顺序、GrabZ/完整XYZ、同run持久及页面读回通过 |
| Q02-PENDING-P03 / Start-Pending.cmd | R008-Q02 / 1.1.4-test-p03 | ac05cd68-5bf2-4180-9b85-e25ce81661aa | 真实协议槽号3；P03→配置Pending区P15；P01 OK留原位；取放及ACK门禁通过 |
| ROT-PART-OK / Start.ps1 -Case ROT-PART-OK | R008-ROT-PART / 1.0.0-test | 60e57790-c6ac-4172-a891-2f71dbfa3284 | 检测所需搬运/旋转/Exit回原位及实际XYZ仍保留，随后下料及Final；无额外普通OK盘末取放 |

解压根为 `C:/Gaode-xyz-20260927/extracted package/Gaode-008-Windows`。实际管理员任务请求 `57eb7bfd3a4e412f8a418393da6664d1`，Administrator / Session2。四例均使用ZIP内同一Host/PLC/WPF，具体PID、程序摘要、页面资源摘要、冻结输入及运行参数逐例保存在process.json/interactive-desktop.json和verification-proof.json。Host SHA `D7DF49ECB4686AEB08293681AECF62DAB6EE668A7F706A7D9D6CB1C9229D9678`；PLC SHA `32E141BA4367137C5512282B2C1A632D5AF7BA26BEA9F8832C0C844D3E73EE6D`。双端inline=0、原正式Windows Native ThreadPool接线、1秒I/O、3秒心跳、50ms轮询及原GC/优先级/业务成功条件未改变。

## Output Excerpts

最终固定ZIP `artifacts/Gaode-008-Windows-x64-20260927-xyz-sorting-r2.zip`，345357666字节，SHA256 `6EA47D6DC783ECCAB811CEFF57ED12A6999EB8712BAD84ADD670FDAAB0942BC2`。配套 `.zip.validation.json` 绑定这一摘要，不重写实跑后的ZIP。

四例均 `routeExit=0, sortingExit=0, cleanupVerified=true, ownedRemaining=[], listenersRemaining=[]`；实际监控检查passed。NG/Pending的 `clear_pick_before_real_slot_and_place`、`all_place_xyz_after_pick_success_before_place_command`、`protocol_status_pick_place_ack_set_status_zero_ack_zero` 全部true。协议48个现有信号的名称、方向和显示含义核对见protocol-display-review.json；XY英文名称保留。

## Residual Risks / 保留的失败

1. 第一次候选解压路径产生279字符图片路径，Python普通路径is_file=false，扩展路径存在且SHA与Host派发一致；实际Height失败，Q01被阻断。未计Passed，原ZIP/页面/SQLite/日志和任务b4e364d73b3e484b8e293f62e918d282保留。短路径解决的是本次可执行部署条件；未建设通用长路径兼容。
2. 第二次短E盘目录Q01真实SQLite提交2576.2291ms，超过原2000ms保存门槛，先CommitUnknown后晚提交；流程阻断，没有盲重发。任务0baf53b9592542ffa8281f13f5d5a196原样保留。小型存储探针未复现，不能认定磁盘根因或认定迁至C盘修复了保存问题；同ZIP短C盘位置对照通过，不覆盖原失败。这项独立保存延迟原因仍待后续定位。
3. 原用户本机run尚未取得，因此不确认原本机没有真实漏轴；本次修复及验证针对已证实的显示/交付问题和明确授权的实际代表。特殊搬运为现有Test专用HTTP设备接口，监控明确标注非Modbus寄存器，不冒称真实PLC信号。r22初始HTTP超期未合并根因。
4. 不宣称008全部收口，不改T065/T055/T070勾选，不用四个代表代替其他原验收门槛。

## Recommendation

本轮显示和分拣Test交付范围已验证，可用最终r2包在短路径本地验收。先Stop旧包，再解压新包；NG用Start-NG.cmd，Pending用Start-Pending.cmd，结束用Stop.cmd。完整XYZ查看“动作发送与反馈·完整XYZ”，下方“最近数值变化”继续只列变化。全部证据索引见[本轮索引](fix-20260927T071859195Z/c-volume-r2/evidence-index.md)。
