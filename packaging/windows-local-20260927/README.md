# 高德 008 Windows 本地测试包（2026-09-27）

完整解压到可写新目录，然后双击 Start-Test.cmd 选择测试入口；Start.cmd仍默认Q01。Q01—Q22适用范围及30个既有入口见测试配方清单.md。不要在压缩包内直接运行。
已包含桌面页面、Host、虚拟 PLC、独立 Python 算法、虚拟相机图片及真实 SQLite/媒体存储。
这是 Test 虚拟联调包，不连接现场 PLC，不代表真实设备和现场标定验收。

## 环境与操作

使用本机现有 Windows x64 环境：PowerShell 7.4+（pwsh）、.NET 10 x64 的 ASP.NET Core 和桌面运行时、Python 3（python）、WebView2。
不需要 SDK、源码编译或管理员权限。正式通信验证所用 .NET 版本为 10.0.12；其他补丁版本不冒称相同验证环境。
默认端口 5001、5080、1502。启动失败时保留窗口错误及 artifacts 下日志。

1. 双击 Start.cmd，等待桌面和 PLC 监控页面打开。
2. 点击“进入系统”，选择 R008-Q01 / 1.1.1-test，点击“启动”。
3. 等待流程执行；页面允许取盘后点击“取盘确认”，检查结束状态、结果和图片。
4. 测试完双击 Stop.cmd；仅关闭桌面窗口不会关闭后台服务。

切换配方前先 Stop，然后在本目录终端运行：

```powershell
pwsh -NoProfile -File .\Start.ps1 -Case Q02
```

当前正常路线：Q01、Q02、Q03、Q04、Q05、Q06、Q08、Q09、Q11、Q14、Q15、Q18、Q20、Q21；另有 GROUP-A-E、ASSEMBLY-A-E-NOCODE 分组/无码输入。各次只加载所选配方目录；不是要求全部重新验收。启动窗口显示本次准确配方和版本。
如端口已占用，可用 `-ApiPort 5101 -PlcApiPort 5180 -PlcPort 1602`。所有接口使用本机回环地址。

## 分拣测试入口

先双击 Stop.cmd 停止上次会话，然后选择：

- **Start-NG.cmd**：Q01-NG，R008-Q01 / 1.1.2-test-night。不合格件从合法源槽位搬至同盘NG区。
- **Start-Pending.cmd**：Q02-PENDING-P03，R008-Q02 / 1.1.4-test-p03。非连续槽位中P03待判定，按合法配置搬至同盘待判定区，普通OK留原位。
- **Start.cmd**：普通Q01合格件留原位，执行下料定位、解锁和取盘确认。

页面仍通过“进入系统”→选择本次配方→“启动”操作；窗口打印准确版本，不要求在PLC网页手动修改信号。
PLC监控原“最近数值变化”逐条显示完整XYZ及实际解锁写入，同值也记录；无独立完整XYZ栏目。英文信号名沿协议，XY_Move_Cmd / XY_Pos_Confirmed不改名。
普通流程保留下料命令4及三轴到位核验，随后执行适用分拣，再收敛解锁和取盘门禁。
取料成功状态2后，才提交真实槽位及放料坐标并发送放料命令2；放料状态3之后完成Sorting_OK确认和清零。

其他已有分拣场景通过 `pwsh -NoProfile -File .\Start.ps1 -Case <场景>` 启动：Q03-NG、Q03-Pending、GROUP-F-MIXED、GROUP-F-PENDING、ASSEMBLY-A-E-NG、ASSEMBLY-A-E-PENDING，以及ROT-PART/ASSEMBLY的OK、NG、PENDING。
ROT的OK仍要完成检测所需搬运和回原位；“普通OK不盘末分拣”不会取消这些必要动作。每次只运行一个场景。

## 数据和版本

每次启动创建独立 artifacts/recipe-execution-008/local-* 目录，保存 process.json、SQLite、媒体和 logs。测试问题请保留整个对应目录及 package-manifest.json。
本包复用已正式验证的Host等未改业务组件，VirtualPlc使用本次源码构建并核对静态资源；启动时对两个所属进程启用 Windows Native ThreadPool、inline=0，保持 1秒 I/O、3秒心跳、50ms 轮询和原 Test 调度参数。未包含机制诊断注入构建。
package-manifest.json 保存交付文件摘要；同ZIP的验证侧文件及缺陷test报告记录最终打包/解压后的实际验收范围。未实跑入口只确认依赖完整，不冒称全部通过。你本机人工结果独立保留。
建议解压到较短路径，例如 E:\Gaode Test；请勿放入多层长目录。当前 Test Python 图片路径受 Windows 普通路径长度限制，过长会明确阻断，不能计为测试通过。

## 既有业务测试入口（沿用r5之后的修正）
请解压到一个新目录，不覆盖正在运行的旧目录。启动前先通过旧包的 Stop.cmd 结束自己的旧会话。

- 普通 OK：Start.cmd。NG：Start-NG.cmd。Pending：Start-Pending.cmd。不同入口使用各自合法配方及区域配置，不能用普通 OK 入口测试盘末分拣。
- 原“最近数值变化”逐条显示每次XYZ实际发送和对应反馈，同值坐标也保留；Z按本动作协议用途展示。缺少本次记录明确标注；无独立完整坐标栏目。公开信号名XY_Move_Cmd / XY_Pos_Confirmed与来源Word一致。
- 检测项、参数、缺陷按原有列显示后端已有事实；没有结果或算法未提供的字段保留“未提供”。七格仍标明 Test 临时格位，尚无正式相机业务映射。
- 若扫码后阻断，先保留现场，双击 Collect-Diagnostics.cmd，提供 diagnostics 下生成的 ZIP。它只读最近三次本包日志及本地监控，不触发运动、复位或停止。也可将两个 Collect-Diagnostics 文件复制到旧测试包根目录，对原失败运行采证。

编号与四个统计格分别绑定；配方ID不冒充批次号，对象所属关系不冒充MES订单。当前Test算法只提供实际分类，未提供的置信度、尺寸等不能编造。

Stop.cmd 会先核验本次各进程身份并持有对应句柄，再依次停止。Host关闭导致算法子进程自行退出时不会误停其他进程；记录保存在本次运行目录的 stop-diagnostic.json。

## r6同步修复范围
监控读取、心跳超时事实与页面错误分列，HTTP成功不代表PLC TCP连接。r5包及业务证据保留；本轮仅验证显示与资源接线，不重跑配方。旧r5未携带summarize-q01-q02-evidence.py；该离线脚本修复属于仓库工具。缺陷仍partial，本机RunningF日志及七格正式映射待补。


## r9 公共解锁显示与人工测试布局（2026-09-29）
最近数值变化位于首屏主区域，右侧实时点位可收起。搜索支持信号/地址/含义/关联文本，方向选择只改变显示。向下滚动或点击“暂停查看”可固定当前记录，后台仍继续接收并提示新增数量；“恢复最新”回到实时记录。“专注记录”扩展历史宽度；“清空记录”只清页面，不改设备也不重放旧记录。
Pallet_Lock_Cmd显示每次真实写入（含0），没有可靠前值时显示“写入0”，不伪造0→0。写入受理、PLC动作记录和Pallet_Lock_Status反馈分开；展开关联详情查看写入序号、连接、事务和UTC。本次仅监控显示、原始证据重放及包资源验证；不冒称新包已经重跑所有业务配方。历史r8包和原失败保留。


### 本轮构建来源
使用本机.NET10环境，未变Host/配方/算法/正式前端及30个选择入口从显式r8基线按摘要复用；仅更新VirtualPlc构建和监控资源。项目打包命令：`python packaging/windows-local-20260927/build.py --plc-dir <本次VirtualPlc构建目录> --base-package artifacts/Gaode-008-Windows-x64-20260927-monitor-history-r8.zip`，再对输出work运行archive.py。build输出为覆盖暂存，不是完整运行目录；ZIP由archive合成全部基线载荷并核验，解压ZIP后运行。不把独立覆盖暂存交给测试者。
