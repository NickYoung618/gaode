# 采集与管道合同 v1

## 业务采集

CaptureRequest 保留既有信封/captureId/intentWriteId、Role、CameraBindingId、point/scope/version、MaxBytes；LightBindingId 允许 null。真实适配器拒绝 DetectionSettings 套用并保持当前设备参数，报告外部光源 NotApplied；不会调用光源。ICapturePort.GetConnectionEpoch(binding) / GetMaxCaptureBytes(binding, fallback) 提供按设备 epoch 与实际预约上限，默认沿用旧实现以保持模拟消费者。ConnectionEpoch 旧成员只作旧路径默认，真实多设备必须按 binding 获取。

CaptureFrameMetadata：schema/role/serial/expected及实际NIC MAC/cameraIP/hostIP、workerSessionId、frameId/deviceTimestamp、triggerSequence、triggeredUtc/receivedUtc、width/height/pixelFormat/payloadBytes、actualParameters（string map）、payloads（name/elementCount/elementBytes/byteLength/sha256）、SDK版本与触发前后读回。CorrelatedCaptureFact 添加 FrameMetadata。CameraFrame 同样附 metadata，并原样保留 Format/ContentType。

CameraAcquisitionService.ReceiveAsync(request, ct) 经 ICapturePort 和 CaptureEvidenceGate 接管 Ended+MediaTaken，同次 Fact/session/epoch/identity 校验后返回 bytes/format/fact；不保存算法结果或发 PLC。CaptureAsync(request,ct) 用于独立采集，经 ReserveCapture、Receive、MediaStore staged保存及 ICameraCaptureJournal.CommitAsync、MarkCommitted 后返回。调用前 ICameraCaptureJournal.RecordIntentAsync 持久保存意图；应用服务不能制造意图成功。

IMediaStore.SaveCaptureAsync 与原 SaveAsync 参数相同并添加 CorrelatedCaptureFact；MarkCommittedAsync(MediaRef,ct) 发布可读；旧实现可默认委托，正式 MediaStore 实现真实附加元数据保存及发布。所有实际业务保存 Media/CaptureFact 后调用 MarkCommitted。ICameraCaptureJournal RecordIntent/Commit/ListCommitted/GetMetadata/RecordFailure 留基础设施实现；独立采集复用现有 Runs/Writes/Media 表，不允许 orphan file 被猜为成功。

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

复用现有本地 Bearer 权限（Read/Start/Recovery.Check/Media.Read），token配置来自部署环境，不硬编码令牌。绑定冲突/未就绪409，采集未知504/明确失败502，保存失败500，不返回成功媒体。诊断带captureId/runId/session/阶段。接口仅纯采集，不表示整机生产就绪。
