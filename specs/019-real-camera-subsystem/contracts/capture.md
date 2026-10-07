# 采集与管道合同 v1

## 业务采集

CaptureRequest 保留既有信封/captureId/intentWriteId、Role、CameraBindingId、point/scope/version、MaxBytes；LightBindingId 允许 null。真实适配器不应用 DetectionSettings并保持当前设备参数，报告外部光源 NotApplied；不会调用光源。ICapturePort.GetConnectionEpoch(binding) / GetMaxCaptureBytes(binding, fallback) 提供按设备 epoch 与实际预约上限，默认沿用旧实现以保持模拟消费者。ConnectionEpoch 旧成员只作旧路径默认，真实多设备必须按 binding 获取。

CaptureFrameMetadata：schema/role/serial/实际NIC MAC（预期值见配置及status）/cameraIP/hostIP、workerSessionId、frameId/deviceTimestamp、triggerSequence、triggeredUtc/receivedUtc、width/height/pixelFormat/payloadBytes、actualParameters（string map）、payloads（name/elementCount/elementBytes/byteLength/sha256）、SDK版本（actualParameters）；触发备份/读回保存于会话诊断。CorrelatedCaptureFact 添加 FrameMetadata。CameraFrame 同样附 metadata，并原样保留 Format/ContentType。

CameraAcquisitionService.ReceiveAsync(request, ct) 经 ICapturePort 和 CaptureEvidenceGate 接管 Ended+MediaTaken，同次 Fact/session/epoch/identity 校验后返回 bytes/format/fact；不保存算法结果或发 PLC。CaptureAsync(request,journal,ct) 用于独立采集，经 ReserveCapture、Receive、MediaStore staged保存及 ICameraCaptureJournal.CommitAsync、MarkCommitted 后返回。CaptureAsync 内首先调用 ICameraCaptureJournal.RecordIntentAsync 持久保存意图；应用服务不能制造意图成功。

IMediaStore.SaveCaptureAsync 与原 SaveAsync 参数相同并添加 CorrelatedCaptureFact；MarkCommittedAsync(MediaRef,ct) 发布可读；旧实现可默认委托，正式 MediaStore 实现真实附加元数据保存及发布。所有实际业务保存 Media/CaptureFact 后调用 MarkCommitted。ICameraCaptureJournal 定义 RecordIntent/Commit/RecordFailure；ListCommitted/GetMetadata 为基础设施 journal 查询；独立采集复用现有 Runs/Writes/Media 表，不允许 orphan file 被猜为成功。

## 控制及二进制 framing

每个报文：4-byte little-endian signed JSON byte count（1..1MiB）+该长度 UTF8 JSON，随后8-byte little-endian signed binary byte count（0..768MiB）+该长度原始字节。JSON 不含图像数组。Binary 仅 frame 响应非零。ReadExactly，长度超界/截断立即失败并废弃会话，不继续解析后续帧。

共享 WireMessage：Version=1、Kind(init/ready/capture/frame/close/closed/error)、SessionId、RequestId、Binding(CameraBinding)/Metadata/MaxBytes/Format/ContentType/Error 可选。init/capture/close 请求与对应响应同 SessionId/RequestId；每次进程新 SessionId。Host 创建唯一随机 pipe 名、CurrentUserOnly，启动固定 worker executable 通过 ArgumentList 传 pipe/session/sdk/state-root，worker连接后 init一次；ready含绑定/实际参数/容量。正常连续capture不重复init。错误包无二进制，触发未知拒绝下一次采集直到受控recover。

worker 的参数备份和诊断目录用于恢复，不作为数据交付。完整数据只通过管道，只有 Host 管理最终保存路径。正常close返回恢复读回结果，失败不能返回正常closed成功。

## 后端入口

正式 Host `Gaode:CaptureOnly=true` 且 `Gaode:Mode=Production`；显式真实配置 site 路径、worker exe、SDK路径、storage root。本入口不注册 PLC。

- GET /api/v1/cameras：逐相机 PID/session/epoch/state/binding/实际参数/大小/诊断。
- POST /api/v1/cameras/{role}/captures：服务创建 run/operation/capture/intent，成功仅在全部提交后 200；返回媒体引用及 metadata。Role 为 A/B/C/D/E/F/3D。后续程序也调用相同服务。
- POST /api/v1/cameras/{role}/recover：当前请求完成/隔离后重建该相机，禁止重放原请求。
- GET /api/v1/camera-media/{id}：提交后的引用/元数据；GET /api/v1/camera-media/{id}/content：读取原始数据。

复用现有本地 Bearer 权限（Read/Start/Recovery.Check/Media.Read），token配置来自部署环境，不硬编码令牌。绑定冲突/未就绪409，调用取消504；采集/保存失败或未知500，未就绪/无效操作409，不返回成功媒体。诊断带captureId/runId/session/阶段。接口仅纯采集，不表示整机生产就绪。

纯采集Host新增 POST /api/v1/cameras/shutdown（Recovery.Check）：受理关闭后经Host正常退出各worker并记录参数恢复；202仅受理不代表恢复成功。纯采集介质根为CameraStoreRoot/media，沿用MediaStore相对media/...路径。

## 审查修复合同增补

RecoverAsync仅Faulted允许，立即尝试设备锁，忙态明确拒绝而非等待后重建健康连接；CameraRecoveryRejectedException映射409，CameraRecoveryFailedException映射503，携带状态。返回200前校验新session及Ready。关闭准入先于等待设备锁，服务关闭不可复活worker。当前进程退出事件关联Process实例+session，经设备状态锁和操作锁处理；Snapshot亦不得把已退出进程报告Ready。

MediaCapacity.RestoreFilesUsed恢复启动存量一次，不创建内存预约；MediaStore构造时枚举media子树普通载荷文件，排除*.metadata.json及*.metadata.json.partial；拒绝链接目录。索引恢复仅发布已提交完整文件，不负责存量累计。保存结束或失败都根据实际留存载荷提交预约；sidecar失败仍计载荷，索引失败保持占用，不重复累计MarkCommitted/Restore。配额不是整个磁盘空间保证，sidecar/DB/日志须由部署磁盘余量保障。

独立验收固定预期points.xyz.f32(float32,irWidth*irHeight*3)、depth.f32(float32,depthType1=textureWidth*textureHeight/2=irWidth*irHeight)、ir.bytes(uint8/uint16,irWidth*irHeight*2planes*cameraGroups，reconstructionType0=2组/2=1组)、metadata.json。实际元素数乘elementBytes必须等于通道字节长度，核对内外manifest身份/帧号/会话/触发关联。2D已知像素格式按其明确容器布局校验；其他格式以SDK PayloadSize/Width/Height/PixelFormat一致性及显式布局限制核查，不武断猜解码公式。

独立验收脚本要求SitePath作为独立设备身份来源；未定义几何解码规则的像素格式明确拒绝给出验收通过，保留SDK原始载荷不等于已解释其布局。

存量配额细化：runFileLimit与dataLimit沿用旧实现，均对同一MediaStore的FilesUsed总量门禁，不按业务Run或重启清零。FilesUsed包含库存载荷和在途磁盘预约；元数据/数据库/日志排除此载荷额度，应另外保证磁盘余量。此修复不引入清理器。
