# 验证记录（2026-10-07）

## 结论与范围

正式源码/正式Gaode.Host接口已完成 A–F Galaxy、CameraPro 3D 的真实绑定、程序触发新帧、原始数据与元数据保存、SQLite索引提交及重启读取。无算法、PLC运动、外部光源或ROI裁剪；仿真仍使用原FileBackedCapture，完整生产仍受NotIntegratedAlgorithm/外部能力阻断。

## 实测顺序和结果

1. 一台A先连续3帧，PID14840/session85905341…不变，原始每帧26,214,400字节，正式保存/下载摘要一致。
2. 3D连续3帧，PID25000/sessionf98a2d…不变，帧号0/1/2；XYZ/depth/IR和metadata.json四条目齐全，各通道元素数/字节数/SHA一致，ZIP约12.6MB；重启仍可读。
3. 首次七台并行发现时B/F未发现，状态Faulted；保留initial-parallel-startup-failure.json。仅显式recover后准备成功，无自动重拍。其后七台各3帧均成功，A/C额外同时采集的触发至收到时间区间重叠（parallel-check.json）。这是采集并行性证据，不是吞吐量承诺。
4. SDK不提供按NIC定向发现，最终Host只在首次准备逐台等待。最终部署包首次启动七台全部Ready，无recover；七台再各3帧，PID/session/openCount均不变，openCount=1。最终证据final-seven-initial/final/checks.json。
5. 同一最终包正常退出、重启，21份最终数据全部通过原mediaId查询、身份/帧号/原session及内容SHA256一致；final-restart-checks.json。
6. 正常关闭七台均有Stopped_ParametersRestored，restoration.json的restored=true、imagingUnchanged=true，无errors；再次重启关闭亦成功。CameraPro尺寸为SDK缓存信息，已明确标注，不能当作独立设备尺寸读回。Host和worker均已退出。

| 设备 | 最终帧号 | 数据长度 | 格式/结构 |
| --- | --- | --- | --- |
| A/B/C/D | 1、2、3 | 每帧26,214,400 | 5120×5120 Mono8，GalaxyRaw |
| E/F | 1、2、3 | 每帧5,013,504 | 实际尺寸/像素类型见metadata，GalaxyRaw |
| 3D | 0、1、2 | ZIP随内容变化，约12.6MB | XYZ-f32、depth-f32、完整多平面IR、metadata.json |

绑定均核对序列号、SDK实际主机IP到Windows物理MAC；A/B Windows网口名称曾互换，正式驱动未按名称选择。3D本次SDK返回有效主机地址，严格校验通过，无强填地址或绕过绑定。

## 核心回归及故障证据

14项测试全部通过（原6项+协议3项+存储3项+仿真2项）：持久流连续5MiB二进制帧；旧session/旧request拒绝；超界头/截断数据拒绝；SQLite提交前不可读、提交后重启可读、同长度篡改被拒绝；注入SQLite索引INSERT失败事务回滚且文件不发布；文件路径写失败；真实FileBackedCapture经共用服务/真实SQLite和文件保存并重启，保留Test/FixedImage来源；100ms取消3s模拟采集，触发计数1且不自动重拍、不保存。

必要超时取消/断流/保存失败已做离线主流程验证。实机SDK阻塞超时与物理断线/拔网线没有执行，不能据此宣称这两类硬件故障验收通过。真实设备极短超时注入命令被自动审批拒绝，返回仅为blocked by policy，未执行该命令；未通过改写命令规避。离线测试不替代硬件故障实测。

## 源码与规范收敛

20个FR、8个SC、3个用户故事及其验收场景、plan的生命周期/协议/所有权/参数/提交/恢复/部署/回退决策、宪章P01–P13已核对。共享接口修改前spec/contracts/plan/tasks已提交；宪章9.0.0保持。Spec Kit1.0.5.dev0从只读历史按摘要恢复，未升级。requirements检查已完成；camera-contracts保持reviewer-owned未勾选，不能冒充人工签字。

SC-005硬件故障实测仍有上述限制，当前只有离线证据；其余核心采集验收实测通过。全检测配方/PLC运动/算法/客户页面不在本次验证范围，不能扩大结论。

## 证据位置

紧凑JSON、两次最终会话的binding/discovery/backup/imaging/restoration、测试结果和运行文件摘要清单在evidence/。大体积原始内容及实际SQLite位于D:\gaode\artifacts\camera-runtime与D:\gaode\artifacts\camera-final-validation，未推入源码Git。令牌不归档进证据或发布ZIP。runtime-inventory.json列数据/文件SHA256，用于后续核对。

部署与回退见quickstart.md。CameraPro再分发许可未证实，交付依赖现场安装native SDK，不宣称可向其他客户再分发厂家文件。

最终交付二进制由正式提交7f1b1ac重新发布；重新执行七台各3帧和同包重启读取21份数据，全部通过。对应release-seven-*.json/release-restart-checks.json；发布与重启两次共14份关闭记录restored/imagingUnchanged均true（release-restoration-checks.json）。包仅随后补入文档及清理运行配置，运行二进制不变。全部Host/worker正常退出。
