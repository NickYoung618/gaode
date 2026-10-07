# 环境与源码核查

日期：2026-10-07（Asia/Shanghai）。范围：第一阶段，只读检查和治理/新规格恢复，未修改正式功能源码，未启动真实相机或 PLC 通信。

## 仓库、工具与回退

- 正式仓库 D:\gaode，开始时 main 与 origin/main 一致，工作区干净；HEAD `0f91f95dadc03751b275c20bb2b954fcf70ccefe`。
- 建立标签 `camera-baseline-20261007-0f91f95`；新分支 `019-real-camera-subsystem`。只在本地操作，未推送。
- 完整历史备份 `D:\gaode\artifacts\camera-baseline-20261007\source.bundle`，`git bundle verify` 通过；摘要见 evidence/input-hashes.json。artifacts 被现有 .gitignore 排除。
- 编号检查：正式 specs 既有编号最高 017；tools/plc-commissioning/specs 有 018-flip-recipe；只读历史规格到 017。选择 019 避开全部已发现编号，不覆盖旧规格。
- `.specify`、`.agents/skills` 原缺失；指定历史归档可读。恢复 38 个文件并逐文件 SHA-256 比对一致，见 evidence/speckit-restoration.json。保留版本 `1.0.5.dev0`、宪章 `9.0.0`、bug 扩展 `1.0.0`。未安装或升级 CLI，环境中 specify/uv 未找到。
- 恢复 memory/scripts/templates/integrations、核心配置和全部既有 skills；未搬历史 bugs、浏览器/运行快照、缓存目录和旧 feature.json。新 feature.json 仅指向本功能，按恢复的规则不入 Git。
- extensions.yml 为 `hooks: {}`，specify/clarify 无需执行 hooks。使用恢复的 create-new-feature.ps1 生成规格，check-prerequisites.ps1 -Json -PathsOnly 正确解析本功能目录。没有调用 plan/setup-plan。
- `global.json` 固定 .NET SDK `10.0.401`、rollForward disable；本机对应版本存在。Host 原始源码 `dotnet build ... --no-restore -v minimal` 成功，0 警告/错误。
- 原有 Gaode.Communication.Tests 基线 6/6 通过，日志见 evidence/baseline-tests.txt。只代表既有通信相关测试，不代表相机功能或完整虚拟流程回归。
- 仓库 `.gitattributes` 使用 `* -text` 保留字节身份；恢复文件保持归档 CRLF 和 Markdown 换行空格。默认 diff --check 将其报告为尾随空白；未为消除报告改写历史工具。按 `core.whitespace=cr-at-eol,-blank-at-eol` 检查无其他空白错误，38 项恢复哈希均再次通过。

## 宪章适用性与旧规格状态

阅读正式 AGENTS.md、归档宪章及 constitution-alignment、007 spec 和 001 采集合同相关章节。P01/P02/P04–P11/P13 适用；P03 只保留既有工艺语义，本次不执行运动/配方检测；P12 保护原型，本次不增前端。不需要重写宪章。用户本次采集范围不包含算法/外部光源，不能套用旧完整检测终点作为本次成功条件。

正式 specs 下许多历史编号目录只有部分附属文件，正文 spec/plan/tasks 未随核心源码迁入；归档正文可只读参考。未用历史正文覆盖正式目录或宣称历史任务当前通过。plan 阶段需逐一列出共享接口消费者和本次影响，不擅改旧任务状态。

## 用户线索逐项核实

| 线索 | 结论与证据 | 本次含义 |
| --- | --- | --- |
| Production 注册 NotIntegratedCapture | 已确认：Station01Registration.cs:102 | 真实模式需正式接线，不能用仿真兜底 |
| SDK 驱动/绑定未接入 | 已确认当前基础设施相机目录只有 ICameraSdkGateway、ILightGateway 和 CameraCaptureAdapter；Production 无真实 gateway 注册 | 旧调试驱动不算正式接入 |
| CaptureRequest/适配器强依赖光源 | 部分需区分：请求构造参数包含非空 LightBindingId；适配器显式校验非空且必调 Set/SetBrightness；消息类型本身未做校验 | 独立采集契约要明确光源不参与，不能塞空光源假成功 |
| 每次采集打开设备 | 已确认 RequestCaptureAsync 内调用 OpenAsync；接口实现是否幂等不能由此推断，正式仓库缺真实实现 | 常驻准备和采集分开，正常采集不调用打开 |
| 打开前获取连接会话 | 已确认 epoch 在 OpenAsync 前读取 | 是否每次必错取决于 gateway；存在错误关联风险，不能直接声称已复现硬件故障 |
| F 限制绑定实例 | 已确认 _f 在适配器实例累计，第二次 F 抛异常，无 runId 范围 | 常驻服务跨运行不可用；业务单次约束不能删掉 |
| 真实格式覆盖 | 已确认帧 Format 未用，按 Role 输出 bin/img | GalaxyRaw/CameraProFrameZipV1 会丢失格式语义 |
| 检测固定 4 MB | 已确认 RecipeDetectionExecutor.cs:233/240 固定预约及请求上限 | 与真实帧大小是否冲突需实际元数据确认，尚未实采 |
| 来源/参数/帧元数据缺失 | 已确认适配器 MediaSource/CameraOrigin/LightOrigin 为 Unknown，Fact ActualSettings=null；CameraFrame 仅字节/格式/类型/epoch | 需从实际驱动传事实；原有关联 Fact/Gate 可复用 |
| 索引重启及提交 | 已确认 MediaStore._ready 只在当前 SaveCoreAsync 填充，无启动恢复；TraceWriter WriteKind.Media 已写持久 MediaEntity；RunMediaCatalog 仍查询 _ready 判可用 | 不是“完全没有数据库索引”。需复用已有持久索引，修复重启可读和跨阶段提交一致性 |
| 文件/索引失败 | MediaStore 写 partial、flush、move 后立刻 FileCompleted/_ready；业务随后另调 WriteKind.Media/CaptureFact。跨阶段失败可能留可读文件和未完成业务索引 | 计划须明确提交点、失败状态及孤立数据处理，不能虚构共同事务 |
| 保存后继续算法 | 已确认 RecipeDetectionExecutor 在检测链里继续算法；AcquisitionCoordinator 已有可复用的采集/保存能力，但依赖 Station01Run 及运行意图/配置 | 应抽出正式独立采集能力并供业务共用，不复制一套测试驱动 |

## SDK 与设备资料

- site.json 可读、7 台角色/序列号/ExpectedNicMac 齐备；输入摘要已保存，没有改现场文件。
- GalaxySDK 与 CameraPro C#/C++ SDK、示例、文档存在；旧调试源码引用 GxIAPINET，CameraPro 使用厂家 C# 包装与 native DLL，目标 x64/.NET 10 Windows。
- 旧 Galaxy 调试代码会 FlushQueue → 软件触发 → GetImage，校验成功状态/尺寸/长度/帧号，复制原始数据，销毁 SDK 图像；这可作参考，仍需本次正式驱动和实采验证。
- CameraPro FrameData.cs 注释明确 point3DSize/depthmapSize 为元素数，不能当字节数；深度维度取决于 depthType，IR 可能包含左右/多张图。正式包需完整保存原始结构和实际尺寸/类型，而不是简单假定一张 IR。
- 旧 ThreeDGateway 请求 XYZ/深度/IR，关闭 RGB 等可选通道，但成功检查仅 payloadCount>0。这是 CL-001 的来源；不能因有任意数据即断言完整 3D 包成功。
- 旧调试源码路径目录名为 0.1.2，csproj 内版本为 0.1.3；只按实际文件内容和摘要追溯，不根据目录名判断版本。
- 当前仅完成许可相关文件名查找，尚未找到明确 Galaxy/CameraPro 再分发授权。未完成许可证全文/安装条款核验；不宣称禁止或允许再分发。只限制将厂家二进制并入交付包，不限制使用本机已安装 SDK 继续实现。

## 当前网络快照与设备验证边界

Windows 网卡快照见 evidence/host-network.json。七个预期 MAC 对应网卡均 Up，IPv4 与历史检查一致；A 名称对应 B 的物理 MAC，B 名称对应 A 的物理 MAC。C 网卡与 PLC 网卡同在 192.168.0.0/24，后续必须按 SDK 返回的 NIC 身份/地址精确验证，不可只靠“同子网”判定绑定。

当前有既有联调包的 python 进程 PID 8816；未停止或修改它。进程名不能证明它占用了相机/发现端口，后续发生占用再定位，不能预先当作阻塞。

本阶段未发现/打开/触发相机，没有修改相机参数、网卡/IP/路由，没有启动 PLC 会话或发送运动命令。历史 2026-10-05 七台打开/读参结果只说明当时连接；CameraPro 旧回环地址失败必须保留并在正式发现时严格校验，不能强填 userIP。

## 阻塞及下一阶段入口

1. **当前待澄清 CL-001**：3D 必需数据通道影响成功判定/合同/测试。已发出一题，未收到答案前不把默认建议写成确认。
2. **硬件未验证**：不是已确认离线或 SDK 故障；当前没有相机不可用的实证。正式驱动接入后分阶段验证。
3. **SDK 再分发许可未确认**：只限制带厂家文件的打包方式。可以按现场安装依赖部署，不能擅自把大型 DLL 推入 Git。
4. **旧规格正文缺失**：只读归档可追溯；不覆盖历史状态，不据此阻断独立新功能。

待本次澄清完成并报告上述事实后，进入 plan，明确状态/进程/管道协议/所有权/参数/新帧成功条件/存储提交/超时及受控恢复/部署及回退；完成 contracts、checklist、tasks、analyze 后才能改共享接口和实现。没有已证实、必须由用户修复的环境故障。

## 回退说明（当前阶段）

正式功能源码未改；可继续运行原提交的程序。需要独立取回基线时，在另一个新目录从 source.bundle 克隆并检出 `camera-baseline-20261007-0f91f95`，避免覆盖当前工作。后续部署替换前必须补齐包/配置/存储备份及正常关闭设备参数恢复说明；不能用 Git 回退声称恢复了硬件参数。
