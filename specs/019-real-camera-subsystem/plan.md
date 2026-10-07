# 技术方案：正式真实相机采集子系统

功能 019；2026-10-07；[spec](spec.md)；宪章 9.0.0。CL-001 已确认 XYZ/深度/IR 同次齐全。

## 方案摘要与阶段边界

正式 Gaode.Host 增加 CaptureOnly 启动选项：这是同一正式 Host 的纯采集组合根，不启 PLC/算法/光源，不创建独立测试产品。Production 配置真实相机时注册同一 CameraCaptureAdapter 和正式 CameraAcquisitionService；完整检测保留算法/光源等准入。FullSimulation/VirtualPlcIntegration 不注册真实 worker。最小验收先启动配置的一台 2D，之后一台 3D，最后七台；每次启动只初始化配置角色。

## 技术上下文

.NET 10.0.401/Windows x64；复用 Application/Infrastructure/Host 分层。新增 Gaode.CameraWorker 可执行工程；一个角色一个进程，引用基础设施中共享管道编解码/帧模型，厂家 wrapper 仅编译在 worker。Galaxy wrapper 通过 SDK 绝对路径编译引用；CameraPro 厂家 C# wrapper 通过本地 include 编译，不把厂家文件纳入源码。CameraPro native DLL 用 SDK 安装目录解析，Galaxy 使用安装运行库，不联网升级依赖。索引复用 Station01DbContext 的 Runs/Writes/Media，无新表/迁移。纯采集专用存储根以显式 prepare-camera-store 命令建立现有 schema，普通启动不自动改表；既有 station store 不重新建库。

## 宪章检查（设计前与后）

P01/P02：符合，用户明确纯采集终点优先，spec 清晰；P03：不适用运动/新配方；P04：符合，超时 Unknown 不重拍；P05：符合，SDK/IPC 在基础设施，业务仅端口；P06：符合，每相机一道闸和有界大小，无全局采集锁；P07：符合，角色/序列号/NIC/会话/请求/触发/帧匹配；P08：符合，意图先保存，短事务提交索引，文件不等同事务；P09：符合，持久诊断关联请求和生命周期；P10：许可/硬件仅限制实际验证和打包；P11：沿用 site 身份配置、不造插件系统；P12：不改前端；P13：按 spec 七台验收、未验证如实保留。设计后无须宪章变更。自定义清单留 reviewer 标记，本轮用户已授权完整实现，不重复申请实施授权。

## 结构与职责

- Application/Ports：CaptureFrameMetadata、FramePayload；ICapturePort 增加按 binding 获取会话/容量的默认成员，不改变模拟实现。
- Application/Acquisition：CameraAcquisitionService 接受已保存意图的 CaptureRequest，经既有 CaptureEvidenceGate 获取证据；独立入口及 AcquisitionCoordinator/RecipeDetectionExecutor 共用此服务的数据接管逻辑，不调用算法。
- Infrastructure/Devices/Cameras：CameraWorkerProtocol（有界二进制管道）；PersistentCameraGateway（每设备进程/管道/串行锁）；CameraCaptureAdapter（正式采集事件转译）。
- CameraWorker：GalaxyDriver/CameraProDriver，发现/绑定/备份/触发/原始数据接管/恢复；正常只初始化一次。不包含媒体最终索引。
- Infrastructure/Media：MediaStore 阶段文件/元数据，提交前不进入可读 _ready；从现有持久索引恢复。
- Infrastructure/Persistence：CameraCaptureJournal 复用 Runs/Writes/Media 保存独立采集意图/事实/媒体，并提供索引恢复，使用现有 schema。
- Host：真实相机 hosted service、独立采集后端入口/既有鉴权、配置/生命周期与日志；CaptureOnly 不构造 PLC 服务。

## 状态、协议与所有权

见 contracts/capture.md、data-model.md。启动 Disabled→Starting→Binding→Opening→Ready；请求 Ready→Capturing→Ready；任何未知触发/传输/超时→Faulted；显式 recover 在单相机锁内正常关闭原 worker（超时才杀进程）、换 session/epoch，重新绑定打开。未知原请求从不重放。首次启动准备按配置逐台等待，避免 Galaxy 全网卡发现广播相互干扰；单台失败记录后继续下一台，无自动重试。不同相机正常采集/显式恢复不共用锁，3D 固定发现端口由仅一台 3D 使用。

同一持久 NamedPipeServerStream（CurrentUserOnly）与一个 worker 连接，byte 模式。控制/元数据为长度前缀 UTF8 JSON，帧字节单独 length-prefix raw binary；无 Base64、无临时交付目录。每消息 protocol=1/session/requestId，响应 identity 必须匹配；ready 包含实际绑定/参数/所需最大数据量。数据使用 long 长度上限后分块读，当前 byte[] 实现限定 <=768 MiB。收到完整帧后 SDK 缓冲释放；Host 持有复制数据直至持久保存，无算法租约。

## 参数策略与成功条件

设备当前曝光/增益/分辨率/PixelFormat/自动参数均保持，不套 DetectionSettings。只临时改软件触发必需字段，持久备份先于第一次设置，设置读回，正常关闭按厂家约束恢复并读回；不写 user set 或网络配置。3D 设置 host 输出 XYZ/depth/remapTexture，不要求 RGB；内置投射随 Capture 正常运行。

Galaxy：打开前匹配唯一序列号及 SDK NIC MAC/IP→Windows 物理 MAC；软件触发前 FlushQueue，发 TriggerSoftware 后 GetImage，成功帧、非空缓冲/合法尺寸/原始 payload、帧号变化。CameraPro：Discover 返回 userIP 必须是非回环有效 IPv4、唯一映射预期 MAC，设备与主机在实际前缀内；不覆盖 SDK 地址。Capture 同步返回、frameIndex/timestamp 与前帧有变化；XYZ/depth/IR 都非空且元素/尺寸符合厂家结构。无有效几何点不冒充算法结论，但不属于本次图像/包采集失败条件。

## 并发、容量、超时与受控恢复

每相机一个 SemaphoreSlim 覆盖完整请求/回复；等待串行也受请求取消限制。设备启动默认 30s，单次采集 30s，正常关闭 15s，纯采集 API 保存总预算 60s；均为本次工程初始等待预算，可配置，不宣称生产节拍。SDK 自带阻塞最多在隔离进程内；Host 到期断开会话，Faulted，原请求 Unknown，不自动恢复或重拍。显式恢复允许对无在途会话做重建。最大内存/磁盘按 ready 实际 PayloadSize 与 3D 有界原始通道总量（含包开销）预约；纯采集配置总预算覆盖同时设备，不能固定 4 MB。停止先关闭采集准入，再正常关闭 worker/读回恢复结果，有限超时记录恢复未知。

## 保存与恢复

1. 正式独立请求创建真实采集用途 Run 和 CaptureIntent，事务提交后才能触发；已有业务沿用 run.SaveAsync 意图。
2. 新帧数据通过 MediaStore 写 .partial→flush disk→原子 rename，实际元数据同样落盘；返回 FileCompleted 引用但不发布可读。
3. 独立请求由 CameraCaptureJournal 一个 SQLite 事务写 Media/CaptureFact 并结束该采集记录；业务沿用既有 Media/CaptureFact 提交。
4. 提交后调用 MediaStore.MarkCommitted；此前查询不能 Ready。索引提交失败保留未发布文件用于诊断，不自动删除或重新拍照。记录结果未知时不假定未提交。
5. 启动扫描已提交 Media 写记录（真实来源要求匹配 CaptureFact），核对身份/相对路径/长度/摘要/元数据；加载 _ready。不扫描磁盘孤立文件来猜测成功。老模拟数据按其原格式读取。

不更改既有数据库表或静默升级。必要失败验证覆盖文件写失败、索引提交失败、旧会话及断线/超时，不扩展完整故障矩阵。

## 共享消费者影响与回退

ICapturePort 的模拟/FileBacked/NotIntegrated 实现保留默认成员；AcquisitionCoordinator/RecipeDetectionExecutor 改用按相机 epoch/容量与共用接管服务，保存后发布；既有 AlgorithmRuntime/MediaEndpoints 消费已提交媒体；Station01HostedService 增启动索引恢复。CaptureRequest 光源允许 null；模拟路径原 light binding 和参数行为保持。F 适配器不再 lifetime 限制，既有业务运行单次语义不改。生产未配置 real-camera 时仍 NotIntegrated。

回退点标签和 bundle 已有；部署包放 artifacts，独立版本目录；停止 Host 确认参数恢复后切回旧包和旧配置。纯采集单独根不修改既有生产数据；新相机元数据为附加文件/写记录，既有 schema 不变。Git 回退不能替代设备参数恢复。Galaxy 许可按本地第2.2条及声明携带；CameraPro 未明确许可则使用现场安装依赖，包不含该二进制。

## 验证与证据

最少正式后端实跑：A 连续3帧→3D 连续3帧→七台各3帧；绑定/原始结构/进程复用/元数据/SQLite索引/重启下载摘要。必要失败：超时/断管、错误绑定/旧session、保存失败及退出恢复。离线测试验证协议帧/关联和媒体提交/重启，既有6个通信测试回归。所有未执行硬件项写未验证；前端不在范围。证据与部署命令见 quickstart.md。

## 实施核实补充（2026-10-07）

首次七台并行初始化时 B/F 未被 SDK 发现，显式恢复后成功。SDK 无按 NIC 定向发现接口，采用启动准备顺序执行这一最小修正；不串行化采集、不重放原请求。补充 discovery.json、imaging-before.json、restoration.json 作为真实绑定和只读成像参数比较证据。Observation 消费者同样改用共用 ReceiveAsync/按 binding epoch/实际容量/提交后发布。实际状态使用 Starting/Opening/Ready/Capturing/Faulted/Stopping/Stopped；绑定校验发生 Opening 内，Recover 在设备锁内重建新会话。纯采集存储为 CameraStoreRoot/camera.db、CameraStoreRoot/media 下的相对媒体路径。

## 审查修复实施方案

沿用现有职责。gateway增加同步状态锁保护Snapshot/状态/Process关联，原每设备SemaphoreSlim继续覆盖异步操作；Exited回调只处理当前Process+session，正常关闭和迟到事件忽略；快照/采集最终准入同步检查HasExited。服务停止标志先置位，禁止后续启动/恢复。recover使用非等待准入，Faulted检查在设备锁内，启动后显式验证结果并由正式入口映射错误。

配额保持原载荷口径，不把元数据改算进4字节测试。构造MediaStore在开始预约前恢复文件库存一次；成功/失败写后按实际full或partial载荷结算，重复索引恢复只校验发布不累计。保留旧索引读取/摘要/提交门禁，不引入清理器或新schema。

新增最小离线可控worker fixture（仅测试工程，无SDK/无HostHTTP），经真实PersistentCameraGateway/管道/CameraCaptureAdapter/CameraAcquisitionService/SQLite验证退出与恢复、缺通道/错映射/错尺寸及失败不发布；不作为产品驱动或兼容层。独立验收脚本按现场site身份及厂家尺寸规则做独立断言；Host基础设施也验证真实帧结构。针对AcquisitionCoordinator、RecipeDetectionExecutor与Observation按实际API选择必要虚拟业务执行验证，不仅直接调用Receive辅助服务。

证据：旧runtime-inventory保留并记录差异；新版本单独数据根和包，实机正常链至少各相机新帧并验证复用/保存/重启；冻结时Host/worker退出，将配置摘要（不含token明文）、SDK、DB、媒体、二进制和包关联。日志/DB可能变化的旧清单明确是历史运行快照。不得启动已被拒的模拟worker Host HTTP复验；其HTTP故障验证标未验证。

## 2026-10-07剩余故障验收授权

用户明确重新授权隔离模拟worker＋正式Host的127.0.0.1 HTTP复验，使用全新独立目录/SQLite，不连接真实设备；先前APR-002保留为历史拒绝，不再将其解释为本轮用户禁止。执行脚本scripts/Invoke-CameraHttpFaultAcceptance.ps1，固定测试fixture且SDK路径指向空目录，核实监听地址、无SDK模块、Ready后Exit(17)记录、HTTP409拒绝、显式恢复失败503及新会话Ready，最后正常关闭。发生新的系统拒绝则原样保存并停止，不改写命令。实机SDK超时/物理断线仅形成设备/步骤/参数备份与恢复方案，待用户另行确认；T031仍按实际证据分项关闭，不以HTTP离线结果替代实机。
