# 数据与状态

复用已有 Runs/Writes/Media 表。独立采集 Run.ContextJson 标识 CameraAcquisition，不携带虚构PLC/配方；意图 Write.Kind=CaptureIntent，保存完整请求。成功事务存 Media引用、CaptureFact及完成Run；失败保留意图与Failure/Unknown事实，不自动重新触发。已存在业务运行仍沿用其正式run/intent事实。

CaptureFrameMetadata 与 FramePayload 见 contracts/capture.md；实际设备参数是 string map，保留读取失败状态，不补虚拟数值。2D原始Galaxy payload不压成JPEG；3D为 CameraProFrameZipV1（points.xyz.f32/depth.f32/ir.bytes/metadata.json），小端，元素数不是字节数，IR保留SDK多图原始布局。每条payload含摘要和长度，外层元数据与请求/帧匹配。2D Format=GalaxyRaw、ContentType=application/octet-stream；3D ContentType=application/zip。

每worker/设备会话一epoch；生命周期Ready才能触发，每角色独立。触发序号递增，旧请求/旧会话不接受。可靠Ended与安全MediaTaken组合才可保存，文件完成与索引提交分离，_ready只由commit/索引恢复填充。
