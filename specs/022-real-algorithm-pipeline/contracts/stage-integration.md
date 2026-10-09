# C022-STAGE：阶段联调与真实包接入
2026-10-10；FR-025–033；现行增量优先于本功能历史冲突描述。以下是后续实现约定，不是运行证据。

## 策略与动作依据
Run冻结ExecutionPolicy=StageFixedRoute或ResultDriven、PolicyVersion/Source、已保存RecipeRef/PlanRevision、作用域（点/相机/对象/面/轮）、确认路线和目标引用。只允许已授权RealDeviceCommissioning，Production拒绝；不新增用途或页面开关，不按测试名/GUID/环境变量分支。
当前策略从已确认配置取得零件/参与对象、点位、面序、抓手及目标、必要公共准备来源；质量和实际目标独立。不能把NG改写OK骗过现mapper，不全局写死G。正常配方加载/冻结、目标预约/容量、机械顺序、唯一动作执行者保持。
3D/F的Unknown/Uncalibrated原样记录；配置有独立已确认定位/对象/配方选择依据才能沿对应路线继续。F原始码与已选配方来源分开，不把期望配方伪造成扫码绑定。缺独立依据或所需设备观察未知则阻断，不合成Normal/Present/TrayObservation。最终恢复按真实3D/F/E和质量完整性决策。
本阶段保原同步等待，不提前运动、不省配方调用、不跨件推进；必要资源可靠结束后才依原序继续。PLC完整周期、轴到位/同坐标、停止/复位、互锁和人工Final保持。

| 情况 | 如实记录 | 当前处置 |
| --- | --- | --- |
| 合法OK/NG/Pending | 原始质量/完整性及技术Success | 不改冻结目标；必要提交/释放/设备条件成立后继续 |
| 缺陷识别或调用失败、无结果、超时 | error、Failed/TimedOut及Unavailable；业务Pending须保技术失败原因 | 仅不依赖识别的确认路线可继续，先保存问题并确认输入/执行结束；不自动重发 |
| F/E/3D缺步骤必需码/绑定/定位/身份 | 原始回复和缺项 | 没有独立确认依据则阻断；结果不决定路线不豁免动作前置 |
| 保存失败/CommitUnknown | 原ID对账、保存受限 | 阻断依赖动作和完成 |
| 输入/执行/派发/取消未知 | Unknown、原释放起点/截止、占用 | 阻断冲突动作及Final，保监管，不重开期限/重发/写假释放 |
| PLC安全/互锁/位置不成立、停止 | 实际设备原因 | 服从原保护，固定路线不放行 |

技术Success仅真实调用成功返回合法关联结果；Pending可技术Success但不Quality.OK。流程Completed只表示所选作用域及机械/保存/释放/人工条件完成。首切片不提交整盘Final。

## 静态核对的交付事实
接口接入只读。README为V0.3.1，原生API gaode-algorithm/0.3、algorithmVersion=gaode-interface/0.3.1，可选外壳station01-worker/2.0。
Windows x64 CPython3.10/3.11为交付目标；requirements版本区间、torch/torchvision、CUDA和驱动组合须实际核验并保存锁定清单。生产样例依赖CUDA；CPU样例不代表全模块Ready。D盘inputRoots/mediaRoot是样例，项目侧配置绑定本Run实际媒体根，不改包或复制现场配置。
communicationReady与algorithmReadiness.modules逐模块ready分开；all ready不能阻无关首切片模块，也不能把ready等同qualityDecisionReady/fusionReady/fLocationReady/physicalSlotMappingReady。

| 模块 | modelId / modelVersion / parametersVersion | 输入及当前限制 |
| --- | --- | --- |
| TrayPose | traypose-ply / traypose-whole-tray-v03.1 / traypose-params-v03.1 | pointCloud PLY、同Capture可选image；实际imageSize须确认，默认1280×1024不可盲用；近正视5×5观察非10×10物理布局；长度/pose/F定位未标定、物理号默认null |
| FDecode | f-qrcode / builtin-qrcode-v03 / fdecode-params-v03 | image、camera=F、8位PNG；rawCodes不等唯一配方绑定 |
| EDecode | e-ocr / dbnet-parseq-official-v1 / edecode-params-v03 | image、camera=E；DBNet/PARSeq真实权重及GPU依赖 |
| DefectSingle | defect-maskdino / maskdino-all5-8class-v1 / defect-params-v03 | image、camera=A/B/C/D；原图几何/置信度，Pending；未标定毫米null |
| DefectFusion | 同defect-maskdino | image1/image2为同对象/阶段/面/轮的A+B或C+D；imageResults独立检测、NotConfigured，总实例数未去重 |

native请求requestId/module/inputs/context/parameters；回复requestId/success/algorithmVersion/modelVersion/parametersVersion/parametersSha256/elapsedMs/data/error；早退未必含全部实际版本/hash，不能补已应用。
每输入绝对path在inputRoots内，明确name/format/camera及本次Media/Capture/长度/sha256/对象/面/阶段/轮；模型和参数版本匹配启动配置，values实际应用摘要保存。TrayPose context为InitialPreparation/round1/transition null或PostPlacementCheck/round>1/本动作；E和Defect必须objectId/stageId/localFace/coordinateEpoch。
PNG仅8位Gray/RGB，无16位/alpha静默降级；PLY支持ascii1.0/binary_little_endian1.0，保点序/无效点，不删点/猜单位/造RGB；点数=实际宽×高。复用raw→已保存派生文件/sidecar/转换来源和索引提交，校验身份并持有租约后派发。

## 项目側最小桥接（新约定，未实现）
交付Worker实际有Hello/Ready、Execute/Accepted、Result、逐输入InputReleased、Cancel，没有本Call独立ExecutionEnded。Cancel尽力、不能中断模型前向；stdin EOF会等待executor，不等OS已退出。SERVICE_BUSY未开始路径只回Result，不是可假回收的通用失败。现C# Test Worker2.0没有native参数/模块Ready，Real配置IsReady仍false，不能换标签当集成。
选择包外Infrastructure常驻Python桥，导入交付AlgorithmService并调用initialize/infer；只做IPC、输入/身份、结果保存及资源事件，不改识别逻辑，不建C队列/插件平台。单次在途串行，复用可核对的进程监督和既有受管边界。
项目自有gaode-real-bridge/1明确是新增协议：Ready（逐模块加载）、Accepted、Result（完整native回复文件引用）、逐输入InputReleased、CallEnded；原Test协议不放宽。
只有native计算返回、输入/执行资源确已结束且桥的本Call清理完成后才能发CallEnded；必须通过真实模块核验，不把Result/finally/取消回执视为结束。身份匹配的CallEnded完成AlgorithmDispatch.Exited，全输入可靠释放后才聚合InputReleased。OS退出只终结该进程会话登记调用，stdout EOF不证明死亡；异常保Unknown，T045门和原释放窗保持。
每Call完整native回复写受管结果根，stdout发小型描述relativeKey/length/sha256及Call/Run/Session/Attempt/逐输入关联，避免交付64KiB行限截断缺陷；这是项目侧协议，不改算法内部。适配器校验根/身份/摘要并存事实，结果文件也计持有/磁盘，不覆盖输入。文件或事实保存失败不能报告已保存成功。

## 补充可执行字段约定
桥Ready携workerSessionId/bridgeVersion/packageAlgorithmVersion及逐模块ready、modelVersion和限制；Call事件携RunId/CallId/WorkerSessionId/Attempt/Role和输入MediaId/CaptureId/LeaseId列表，InputReleased含inputIndex及已核摘要，CallEnded不代替逐输入确认。接受事件先按注册请求全键核对，不由包缺少的RunId自行猜来源；项目桥显式回传项目注入身份。
native输入name/path/format/camera与对象上下文从当前保存媒体/实际执行对象及冻结配置构造；coordinateEpoch等原请求缺字段时须列出真实来源，不能将HeightRound默认当它或默填0。缺必要输入/模块Ready仍阻对应调用主链，不得跳过真实调用仅走路线；已发生的技术失败只按上表有条件继续。
stage-result/1查询增量包含Run/Call/输入与rawResultRef、technicalOutcome、qualityDisposition（含Unavailable）、completeness、flowOutcome、executionPolicy/PolicyVersion、configuredTargetRef/actualDispositionRef及limitations。由已有Run/evidence API消费者承接，不新增页面编辑流程。
