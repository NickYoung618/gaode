# 正式相机子系统部署与回退

这是正式 Gaode.Host 的 CaptureOnly 组合，复用正式采集服务、MediaStore、SQLite 索引。只采集保存，不构造 PLC/算法/外部光源；productionReady=false，不代表整机生产就绪。

## 依赖及许可

Windows x64；.NET / ASP.NET Core Runtime 10.0（本机 SDK 10.0.401）；现场已安装 Galaxy SDK/网卡驱动和 CameraPro SDK。构建不升级锁定依赖。

- Galaxy native：D:\GalaxySDK\APIDll\Win64，GxIAPI.dll 2.0.2603.8121。
- Galaxy managed：GxIAPINET.dll 1.0.2512.8261，SHA256 EC679FB2D2335208B140E4B25AF45640D6BA5978143E425C16831FF7F54B021C。
- CameraPro native：D:\软件开发sdk\3DCameraViewer\CamSDK\CamSDK_CSharp\bin\CameraPro.dll，SHA256 F1DAEB867D5C16F3C1E7CBCDA095833ECB0A698E817C97E502B48BA0127648A6，无文件版本；C# include 构建时编入 worker。
- Galaxy 本地许可2.2允许大恒相机使用/发布SDK及驱动，包保留许可和第三方声明。CameraPro 未找到明确再分发许可，包不附其native DLL，仅使用现场已有安装。厂家大文件不入Git。

## 构建与启动

仓库根 PowerShell 7 执行，OutputPath 必须为新目录：

```powershell
./scripts/Publish-CameraSubsystem.ps1 -OutputPath D:\gaode\artifacts\camera-release-019
```

可指定 GalaxyWrapperPath、CameraProIncludeRoot 构建依赖。包为 framework-dependent win-x64，含host/worker、脚本、说明、许可、文件SHA256 manifest。解压至独立版本目录，不与另一个相机Host同时运行。在包目录执行：

```powershell
./Start-CameraSubsystem.ps1 -SitePath D:\图片采集\_配置\site.json -DataRoot D:\gaode\camera-data -PrepareStore
```

首次显式创建新库；重启去掉 PrepareStore。已有库不重建、不静默迁移。脚本绑定127.0.0.1:5189，生成独立Operator/SystemAdministrator token，仅受控本机账户可读；不要提交/归档token。DataRoot/camera.settings.json 为配置，operator.headers.json/admin.headers.json 为鉴权头，host.pid/stdout/stderr为运行记录。

配置 Gaode:Mode=Production、CaptureOnly=true、CameraStoreRoot，以及 Cameras 的 SitePath/WorkerPath/GalaxySdkPath/CameraProSdkPath/StateRoot。默认启动30s/采集30s/关闭15s，API总预算60s；容量默认2GiB内存/100GiB磁盘，按现场实际调整。存储位于 DataRoot/store/camera.db 及 media。改版本时脚本更新WorkerPath；其他已有配置保持，变更SDK或端口需审查配置。

site.json 的 Cameras 数组项为 Role(A–F/3D)、Kind(2D/3D)、Serial、ExpectedNicMac。序列号和MAC必须唯一；不能以Windows网口名称/枚举顺序/强填IP替代。可用只含A或3D的真实身份子集分阶段验证。首次准备顺序执行避免SDK发现广播冲突；正常采集各设备独立并发。

## 正式接口与验证

```powershell
$h = Get-Content D:\gaode\camera-data\operator.headers.json | ConvertFrom-Json -AsHashtable
Invoke-RestMethod http://127.0.0.1:5189/api/v1/cameras -Headers $h
./Invoke-CameraAcceptance.ps1 -HeadersPath D:\gaode\camera-data\operator.headers.json -SitePath D:\图片采集\_配置\site.json -EvidencePath D:\gaode\camera-evidence-001
```

验收脚本逐台POST /api/v1/cameras/{role}/captures三次，下载并检查session/帧号/长度/摘要、3D通道及metadata.json，最后检查PID/session/openCount不变。单台可用 Roles A 或 Roles 3D。失败不自动恢复或重拍。

GET /api/v1/camera-media 列已提交索引，/{id} 查数据/元数据，/{id}/content 下载。2D为GalaxyRaw；3D为CameraProFrameZipV1，包含points.xyz.f32、depth.f32、ir.bytes、metadata.json。metadata含实际参数、原始布局、绑定/IP/会话/帧号/时间和payload摘要；几何精度不在验收范围。

正常停止并重启后，使用原mediaId重新GET元数据/content，比对身份/长度/SHA256。只恢复已完整提交并校验通过的索引；孤立文件不猜测为成功。停止写入后将store内SQLite和media一起备份。

```powershell
$admin = Get-Content D:\gaode\camera-data\admin.headers.json | ConvertFrom-Json -AsHashtable
Invoke-RestMethod -Method Post http://127.0.0.1:5189/api/v1/cameras/shutdown -Headers $admin
Wait-Process -Id ([int](Get-Content D:\gaode\camera-data\host.pid)) -Timeout 30
```

202仅表示开始停止，必须检查state/{role}.host.jsonl最后Stopped_ParametersRestored、会话restoration.json读回、进程退出。正常关闭恢复临时触发参数；成像参数只读比较。强制结束/掉电不能宣称恢复。管理员POST /api/v1/cameras/{role}/recover只重建故障设备新会话，原未知请求不重拍。

## 回退

停止Host并核对七台恢复读回及worker退出，再切回旧包/旧配置。纯采集独立根不替换既有生产库。源码标签camera-baseline-20261007-0f91f95，Git bundle D:\gaode\artifacts\camera-baseline-20261007\source.bundle；先在独立目录clone bundle审查，保留当前源码和数据，不用reset --hard或删除数据回退。Git回退不能代替设备参数恢复。

验证结果及未实测项见validation.md；依赖/许可见research.md。前端和历史归档只读。

## 67e4a57审查修复部署增补

独立验收现在必须传SitePath，用现场配置独立核对身份；3D按固定四条目及厂家元素/尺寸规则校验，未知2D像素布局不会猜测通过。磁盘限额仍是media子树载荷字节（含保留失败载荷和partial），排除sidecar/DB/日志；启动恢复存量，不清理历史文件。Ready或忙态recover返回409，故障恢复失败503；仅Faulted允许重建，服务关闭后不可重启。新修复回退标签camera-review-baseline-67e4a57和bundle artifacts/camera-review-baseline-67e4a57/source.bundle。必须先正常停止并读回恢复，再在独立目录取回旧源码/包；保留新旧数据根，Git回退不代替硬件参数恢复。
