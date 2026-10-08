# 高德检测设备主项目核心源码

正式主工程核心源码快照，来源为迁移工程 `gaode-1`。来源提交及文件 SHA256 见 `source-baseline.json`。

- `backend/src/`：领域、业务、基础设施、PLC 协议与 Host 服务。
- `frontend/`：正式前端代码、页面和构建配置。
- `desktop/`：Windows WPF / WebView2 桌面程序。
- `configuration/`：PLC 地址来源、协议和配方模板。
- `specs/*/contracts/`：接口合同和配置 schema。

正式应用源码位于backend、frontend及desktop；核心验证位于backend/tests。按本轮授权，独立联调工具源码同步在tools/plc-commissioning，部署runtime、现场数据及发布ZIP不进入Git。`frontend/src/assets/` 已补入既有原图片，不重新生成或修改客户原型。

当前020/021源码改动、35项定向回归、final-3补丁关联及尚未完成的现场条件见[2026-10-08源码交付说明](specs/021-commissioning-console/source-delivery-20261008.md)。当前为待完整现场运行配置的联调软件，不代表真机完整流程或生产验收通过。

环境：.NET SDK 10.0.401（见 `global.json`）、Node.js。

```powershell
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj
cd frontend
npm ci
npm run build
```

前端构建完成后，可执行 `dotnet build desktop/Gaode.Station01.Desktop.csproj`。硬件 SDK 和现场配置需另行准备。

上传前完整工作副本已通过后端及桌面构建、前端构建、TypeScript 类型检查；不代表现场硬件验证通过。

2026-10-07：按成组成员选择分拣夹爪，正式源码及联调1.1.6同步；[改动、验证和回退说明](specs/member-gripper-selection/delivery.md)。
