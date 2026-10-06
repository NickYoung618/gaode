# Bug Verification：XYZ、分拣及部署复测

- Slug：xyz-sorting-deployment（承接原缺陷，不新建任务）
- Tested：2026-09-27T09:50:13.240466+00:00
- Assessment：[本轮补充](assessment-supplement.md)；Fix：[本轮增量](fix.md)
- Result：**partial**。已确认的软件修复与下列Test包范围验证通过；用户原本机运行尚缺日志，七格正式映射尚缺业务确认，不将008整体改写为已收口。原assessment/fix/test、所有失败及中间包均保留。

## 最终交付与构建边界

最终包：`E:\dzk\gaode-1\artifacts\Gaode-008-Windows-x64-20260927-xyz-sorting-r5.zip`，345364219字节。SHA256：`70C5D53EA4E5CC19070516943A736085903439A8996AD2CD6CD5BA62A83CDF2E`。ZIP CRC及所有载荷摘要通过。最终r5实际解压后完成Host/VirtualPlc/独立算法Ready→Stop，未启动配方，所有本次进程与监听释放。

r4完成NG/Pending/翻面实际WPF主流程；r5只改Stop.ps1及README，343文件完全同摘要（包括Host、PLC、前端、算法及输入）。因此复用其业务证据，r5只复验变更的停止链，不把它描述为重新跑了全部配方。普通OK采用本轮r3真实解压运行，无普通OK盘末取放；r3至r4差异仅两个runtime副本及README。

## 实际运行

| 入口 | 实跑包 | RunId | 完整XYZ动作数 | 结果与限定 |
|---|---|---|---:|---|
| Q01 | r3 | `0b877606-15de-4688-aedb-61747485266c` | 5 | 主流程/XYZ/协议通过；本轮真实解压包：普通OK不分拣及主流程；前端后续修正不改变该业务证据 |
| Q01-NG | r4 | `f51c4aa4-a5e3-4e4e-bc76-ed013d89daff` | 7 | 主流程/XYZ/协议通过；最终业务文件同r5；显示检查器误选节点失败保留，实际截图及既有记录只读复核 |
| Q02-PENDING-P03 | r4 | `c1049872-0aa7-417b-8ca3-fd738f2c9d46` | 9 | 主流程/XYZ/协议通过；最终业务文件同r5；真实DOM/监控通过，原清理拒绝保留，按原身份补清理 |
| Q03 | r4 | `17d03621-1923-422c-9b67-98c73308dc6a` | 8 | 主流程/XYZ/协议通过；最终业务文件同r5；真实DOM/监控通过，原清理拒绝保留，按原身份补清理 |

每个动作逐一核对原始Modbus写入：连接内、命令前、本动作新写的X/Y/对应Z；FC16合并X/Y时按寄存器偏移解码，不把整包误当一个Float32。实际XYZ反馈关联相同generation/actionSequence，与本次写入在Float32位模式上完全一致（29个动作、87个轴值；xyz-float32-bit-review.json），业务坐标容差未改。即使Y相同仍有本次真实写入，未从上次动作补值。

NG取料→成功2→清命令→真实槽位/放料XYZ→放料→成功3→清命令/Sorting_OK=1→状态0→Sorting_OK=0已核对；Pending实际槽位3送P15，仅P03处置，P01保持原位；NG送P14。卸料定位命令4和门禁保留。Q03面号2反馈、Flip_Status=2、Flip_OK=1→状态0→Flip_OK=0→目标面清0已核对。3.1.7复位成功保留到下一运动受理才清，未提前清零以推进。

## 必要检查

| 检查 | 命令/动作 | 结果 |
|---|---|---|
| 旧HTML+新JS原故障 | mixed-html-js-reproduction.json；7项monitor测试 | 原空节点异常可复现；修正后一次版本重载/显示错误与连接区分，浏览器持续连接、no-store及新命名核验通过 |
| 前端结果及监控回归 | node --test frontend/tests/us1/runtime-007.test.ts frontend/tests/us2/runtime-media-007.test.ts scripts/tests/virtual-plc-monitor.test.cjs | 30/30通过；列、数值/单位、缺陷、当前对象、编号/4指标、权限拒绝及无假结果 |
| 实际长路径worker | python verify-long-path.py | 旧版274字符InputMediaUnavailable；新版273字符实际图片字节/SHA通过，原10秒计算后Result/InputReleased；两进程退出 |
| PLC编译 | dotnet build VirtualPlc/VirtualPlc.csproj --artifacts-path artifacts/xyz-r3-build --ignore-failed-sources -v quiet | 0警告0错误；首次--no-restore因无assets失败保留，不计为产品故障 |
| 真实包显示 | 固定verify-package.ps1内实际WPF/WebView2及Edge；离线同run/API/SQLite/媒体对账 | r4 Pending/Q03 DOM和监控通过；r4 NG截图及原始事实复核通过，测试器误报另列 |
| 最终停止修复 | r5 Start.ps1 -Case Q01-NG -Headless -ApiPort 27201 -PlcApiPort 27280 -PlcPort 27202；Stop.ps1 | 无配方启动；三进程身份/句柄核验均真，全部退出，端口清空 |
| 只读采证入口 | 解压包Collect-Diagnostics.ps1 | 最近3次日志和摘要39文件成功打包；已停止HTTP不可用如实记录；0运动/复位/停止命令 |

## 失败记录与复核

1. r3现场截图发现编号/统计错位及配方号冒充批次，继续修正为r4；未将r3页面宣称完整修复。
2. r3 Pending显示检查错误比较默认P03与已切换P01；r4 NG检查器CSS选择器把verdictBig计入4指标，实际Edge离线DOM证实旧选择器5个、正确容器4个。保留两个失败任务及日志，修正检查器，不重跑r4 NG。
3. r4 Pending与Q03 Stop在终止Host后再查worker时拒绝身份，业务Final已完成。原异常未记录具体不符字段，不能断言PID重用或某字段为空。只按创建时间/路径核实残留PLC后清理，未知进程停止数0，之后修正为先核验并持有句柄。r5实际Stop验证通过。
4. 长路径首次准备为250字符，未触及失败条件，两个旧/新worker均成功；保留该准备记录，改为明确超过260的路径后才进行有效对照。

## 收尾与待确认

专用管理员任务已空闲、当前请求已消费，受限runner恢复原摘要`D3A8D92A27C821630E85381C8EBCE27719AE2939ECD0C23959AEFCB19AF31643`。全部本轮监听已释放，未停止9428等其他worker；未运行WPR命令。没有改Host运动/分拣业务逻辑、1秒I/O/3秒心跳/50ms轮询、任务勾选、来源Word或客户原型。特殊搬运/回原位保持原实现和既有r2证据，本轮未重复该矩阵。

当前算法没有提供的置信度/缺陷数/测量值仍显示未提供，不伪造结果；七格正式对应未获确认，保留标明来源的Test临时格位。本机原两次RunningF不能由本轮通过推定同因：请将只读采证工具放到原失败包目录执行并提供生成ZIP，以核对原run的worker结果、配方匹配和真实包摘要。

建议：交付r5供本地复测；原本机缺陷保持待证据闭环，不宣称008整体收口。
