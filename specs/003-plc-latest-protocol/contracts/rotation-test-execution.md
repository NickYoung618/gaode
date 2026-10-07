# 特殊旋转虚拟设备请求/结果合同（2026-09-26）

依据原特殊零件闭环图、008 sequences §6及U03。仅Test/Virtual启用，本接口不定义生产寄存器，不借用Flip/Sorting ACK。Host仍持有唯一运动租约并核对连接代次、安全、对象、原槽、配置来源及必要保存。

VirtualPlc进程提供`POST /api/simulator/special-actions`受理和`GET /api/simulator/special-actions/{actionId}`结果。请求含runId/operationId/actionId/entityId/stepSequence/attempt、kind=Enter/Rotate/Exit、poseId、sourcePointRef、originalSlotIndex、target(X/Y/Z/unit/frame/version)。来源和点位仅版本化虚拟配置；angle/direction不由Host请求传入。虚拟设备内部采用既有Review旋转姿态对应的模拟角度和运动时长，不冒充真机标定。

结果关联同一请求，status=Executing/Completed/Failed/UnknownHeld，报告实际XYZ、poseId、occupiedEntityId及Virtual来源。Enter完成后该实体占用工位；Rotate仅接受当前占用实体，姿态结果匹配并保存后才采集；Exit按可靠质量选择原槽/本盘NG/Pending目标，实际位置及空闲反馈匹配并保存后才算处置完成。未知结果保留占用，不自动重发。

每次HTTP I/O仍最多1秒，Host阶段绝对期限不重置；虚拟动作耗时与已有普通动作相同。初始3D/F及实际逐图worker、媒体/SQLite保存保持，翻转放回后统一复查3D姿态，F不重绑。当前实体全部面完成后立即按质量退出，再处理下一实体；外围盘末Sorting读取已提交特殊处置依据，不再重复搬运。

生产特殊动作映射、实际旋转机械参数及标定继续现场确认；不限制本合同下合法虚拟路线。必须以真实VirtualPlc请求/结果及端口运行验证，Host不得直接生成完成结果。

冻结模型保留`specialType1WholeAssembly`，单件使用同能力的`specialType1Part`（independentPart）；每位置`resolvedRotation`显式提供entry/poses/exit源目标。结果`SpecialHandlingCompleted/HandlingEvidenceReference`只在Exit反馈及必要保存齐备后设置；外围映射须读回同run/plan/entity的SpecialExitCompleted事件，不能仅凭布尔值跳过分拣。

## USR-20260926-D复位与特殊Test初始观察（待实现）

不新增生产旋转寄存器。现有System_Reset在VirtualPlc内部同时使旧specialTask取消/收敛；复位generation屏蔽旧异步回写XYZ/occupiedEntityId，确认实际活动结束和占用释放，保留specialResults历史。仅Test sidechannel增加GET /api/simulator/special-actions/state，返回generation、lastResetId或本次复位观察关联、activeActionId、occupiedEntityId、status及observedAt/source；由真实VirtualPlc状态产生，不由Host写成功。Host仅在本次复位关联、无活动/占用且普通协议初始状态也成立时允许旋转新轮。生产无法自动判断的夹具状态遵守§4.3人工物理核对，缺可靠依据仍局部Blocked。

### 010实施定向对齐 A04（2026-10-02）

本节落实010已审查设计，优先于此前冲突的测试执行结构；历史记录和任务勾选保持原义。只调整以下共享接口及消费者，不宣称实现/运行通过。

- **A04**：AuxiliaryHandlingRequest用CoordinateEvidenceReference替代TestSourceReference/固定来源白名单。文件解码只转换格式，保人工占用观察/授权确认/清零、共享实体一次动作、E缺码错误处置、旋转姿态/出口。适配用途准入可识别Test但不能推进业务；009地址/原始码/ACK/协议槽知识仍只在通信层。
  生产/消费与010实施承接：typed依据→LatestProtocolPlcDevice.Acquisition/辅助适配→Wire/动作证据/查询；T008/T012/T018—T020/T030。

完整字段和判据见[IB](../../010-recipe-execution-isolation/contracts/input-boundaries.md)、[CE](../../010-recipe-execution-isolation/contracts/common-execution.md)、[VG](../../010-recipe-execution-isolation/contracts/verification.md)。原反馈、真实保存、取消、期限、未知占用、来源真实性及生产局部限制保持。不新增页面/真实SDK/工艺/历史数据库升级。
