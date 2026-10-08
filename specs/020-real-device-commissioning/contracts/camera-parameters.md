# CP-020 阶段B：配方参数应用及帧对应

版本020-stage-b/1，2026-10-08。软件v2已实施，离线验证见validation-stage-b.md，实际SDK效果待T055；FR-002/012/013。019历史v1是保持成像参数的纯采集，不能据其成功证明本合同已实现。

## CP-01 请求与原子范围

沿CaptureRequest/DetectionCaptureSettings及请求摘要，传逐次冻结ExposureUs/Gain、适用ROI、灯通道/亮度/等待及ProfileId/版本。公共3D/F设置来自冻结公共/设备配置，检测/适用E来自配方，不能拿最后一次页面值临时覆盖运行。

在现PersistentCameraGateway每相机gate内完成：参数能力/范围校验→保存原值（会话第一次改动前）→设置→SDK实际读回核对→触发→取得本次新帧/元数据。采用gateway内一次带设置的采集方法，不能在两个独立上锁的Configure/Trigger间让另一请求插入。

公共3D/F现只有CaptureParameters(ExposureUs, LightLevel)。CaptureRequest拟增加可选PublicSettings，按角色与DetectionSettings互斥；ThreeDStep、FScanStep及RecipeDetectionExecutor.Observation把冻结公共参数显式传入。不得补造公共Gain、ROI、通道或SettleMs；未请求的参数保留并标明实际设备读值。光源绑定所需通道来自有依据的设备配置，缺必需配置报错。无设置的CaptureOnly允许两载荷均空，配方/公共正式链不得以空载荷绕过适用参数。

等待唯一归属为采集适配层：灯配置消费成功后、触发前，执行配置SettleMs一次。RecipeDetectionExecutor现有请求前Task.Delay须移除/收敛到该边界，不双等，不在灯设置前提前等待；设置、等待、触发、返回都消费原采集及动作剩余期限，禁止重新开满额窗口。

具体SDK单位/步长/可写性由现SDK节点能力核实，不猜曝光/增益容差。无法精确表达的请求给出不支持原因，不静默截断。2D适用曝光/增益必须真正写入并读回；3D按设备实际曝光通道/模式能力，不把2D Gain套到3D。3D请求未包含的参数保持原值并标明；请求了不支持参数就拒绝该采集。

ROI先明确验证现全幅语义；非全幅不能静默忽略或擅自选软件裁剪。若本轮配方确需非全幅，补技术/业务用途确认后再设计该范围。像素格式、尺寸、自动曝光/增益如因参数应用必须改变，仅改必需项且备份/恢复；不得顺手修改其他设备参数。

## CP-02 worker协议及消费者

新增wire v2设置采集请求，包含请求设置及摘要，响应包含实际读回/应用事实并关联同SessionId/RequestId/TriggerSequence/FrameId。Host与worker同版本发布，版本不匹配拒绝，沿原framing/容量限制。不建自动降级至“未应用但成功”。

ICameraSdkGateway增加明确带设置的原子采集能力；所有实现/fixture须显式处理，不提供忽略设置的默认实现。CameraDriver增加等价设置采集边界；GalaxyDriver/CameraProDriver各实现实际支持参数。旧无设置Trigger用于CaptureOnly，保留“不应用配方参数”的明确语义，更新同包v2但不冒充配方Applied。

共享消费者：ThreeDStep、FScanStep、RecipeDetectionExecutor及其Observation分支、CameraCaptureAdapter、PersistentCameraGateway、CameraWorkerProtocol、Gaode.CameraWorker/Program/CameraDriver/两SDK驱动、CameraWorkerFixture、相机协议/生命周期/业务测试、CaptureEvidenceGate及媒体sidecar/结果查询。若扩CaptureFrameMetadata/CorrelatedCaptureFact，旧历史缺字段按Unknown/NotApplied读取，不补造Applied。

## CP-03 逐组件事实

请求摘要、实际设置、相机来源Real、灯来源Simulated、各自应用状态分别保存。优先复用现ActualSettings/FrameMetadata；如单个ApplicationState不能区分，追加可选逐组件事实，保留旧字段历史含义。

相机只有设置成功、读回匹配并关联新帧才能声明Camera Applied。虚拟灯经ILightGateway实际消费参数/等待/开关并记录成功，但PhysicalLightApplied始终为false，不把虚拟灯亮度写成真实相机参数。一次整体Applied不得掩盖某组件未应用。

每帧保留RunId/CaptureId/OperationId/IntentWriteId、配方/点/设置摘要、相机role/serial/NIC、session/epoch/trigger/frame、实际参数及媒体引用。设置失败不触发，不出现成功媒体；触发未知不重拍。原资源限额、必要意图与SQLite媒体提交、MarkCommitted顺序不变。

## CP-04 恢复与验证边界

正常关闭恢复本会话实际改过的参数并读回，恢复失败如实记录；异常退出不承诺已恢复。沿019原trigger/workmode备份机制扩成像参数，不覆盖已有封存恢复证据。SDK超时/物理断线的现场未验限制继续保留。

最小验证：同相机两次不同冻结设置，各自读回/帧匹配；不支持或读回失败无假Applied；参数应用后正常恢复；模拟灯实际调用且来源准确；实际SQLite/媒体重读。离线fixture只证明协议/保存，不替代真实SDK参数应用验收。

日志任务绑定：T050负责设置/灯消费/触发及失败的持久结构化诊断；T051负责参数证据/必要保存失败及依赖阻断。沿RuntimeDiagnostics及现日志设施关联RunId、CaptureId、OperationId、绑定/会话、设置摘要、来源、实际原因和处置，未知身份不补造。T058复用现必要失败用例核查读回失败及保存失败可定位，无假Applied、自动重拍或后继动作；不引入新日志平台。
