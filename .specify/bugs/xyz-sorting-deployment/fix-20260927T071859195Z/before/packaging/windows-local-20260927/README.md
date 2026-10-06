# 高德 008 Windows 本地测试包（2026-09-27）

完整解压到可写目录，然后双击 Start.cmd，默认启动 Q01。不要在压缩包内直接运行。
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

## 数据和版本

每次启动创建独立 artifacts/recipe-execution-008/local-* 目录，保存 process.json、SQLite、媒体和 logs。测试问题请保留整个对应目录及 package-manifest.json。
本包复用已正式验证的 Host/VirtualPlc 二进制；启动时对两个所属进程启用 Windows Native ThreadPool、inline=0，保持 1秒 I/O、3秒心跳、50ms 轮询和原 Test 调度参数。未包含机制诊断注入构建。
package-manifest.json 保存所有交付文件摘要；package-validation.json 记录本次部署验证范围。已有 Q01 页面验收不等于在你本机完成验收。
