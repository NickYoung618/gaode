# 正式链验证启动说明（实现前定义）

先按 scripts/Publish-CameraSubsystem.ps1 构建 Host/worker 到独立版本目录。配置沿用现场 site.json，明确 worker/SDK/storage root；不得把真实采集配置用于 FullSimulation。首次新存储根显式执行 Host --prepare-camera-store；现有根禁止重建。

启动同一 Gaode.Host，Gaode:Mode=Production、Gaode:CaptureOnly=true；绑定本机HTTP端口及本地Bearer令牌。GET cameras确认角色/序列号/NIC/PID/session。POST单台captures至少3次，下载原始内容，对照metadata和摘要；确认PID/session/open次数不变。

顺序：A一台→3D→配置七台逐台。3D包必须points.xyz.f32/depth.f32/ir.bytes及metadata.json齐备；不检查几何精度。停止后检查恢复读回和worker退出；重启GET camera-media/{id}及content与原摘要一致。

故障检查仅必要错误绑定、超时/断线、旧会话、文件/索引保存失败；失败不得返回Ready，不自动重拍。既有6个通信测试及新增协议/媒体测试应通过。具体可执行命令/配置参数在交付时补齐，本文不代表已执行。
