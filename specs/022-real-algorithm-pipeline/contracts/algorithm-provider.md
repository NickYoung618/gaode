# C022-ALG：正式配置、真实算法提供者与适配边界

这是本项目对真实提供者的能力/证据要求，V0.2为历史来源，最新V0.3_PNG_PLY仍为对接稿；生产传输、消息字段、输出格式及SDK由交付后确认，不宣称station01-worker/2.0已满足。

## A/B/C的调用顺序

A在既有正式同步链内：冻结配置→采集/可靠结束→保存原媒体/事实→转换并保存PNG/PLY与输入来源→原受管AlgorithmIntent/实际请求→结果/保存及可靠输入结束→按C022-ACQ结束原窗口/完整周期→原后续动作。转换/原文件持有须在派发前可靠，窗口释放不伪造AlgorithmFact；A保持原等待位置，不承诺提前释放窗口。C才按C022-ACQ采集证据先结束窗口并推进许可动作/后台计算。下节早释放/后台队列全路径为C设计保留，不作为A前置。

## 配置到结果保存

正式运行入口 → Station01RuntimeOptions/Station01Registration分模式读取 → ConfigurationLoader及共同校验/能力注册 → 公共算法/预算快照 → 真实3D/F输入、算法结果与SQLite保存 → F唯一匹配已保存配方 → 冻结产品RecipeRunPlan及算法模型参数 → 采集预约、正式相机采集 → MediaStore原文件/sidecar → Media/CaptureFact提交可读 → 正式窗口结束与AcquisitionReleased保存 → AlgorithmIntent/输入接管/有限队列 → 真实提供者以冻结版本派发 → 身份/输出/期限仲裁 → AlgorithmFact落库 → 同键融合/物理对象判定保存 → 原序分拣 → 可靠资源结束与原Final链。

Host拟新增明确RealAlgorithm提供者配置，与现WorkerExecutablePath/Script/Manifest测试配置分开，字段只含需要的Provider/实现身份/能力角色/版本、受验证的本机启动引用、模型参数/标定/转换引用、资源池及预算引用；路径/摘要/用途/能力匹配校验，不新增任意脚本插件入口。正式后台路径指共同业务执行与真实证据路径，不表示Production已获放行。既有用途矩阵保持；真实缺项用NotIntegrated/明确局部受限，不回退虚拟成功。真实算法模式不得消费ImageManifest或固定测试输入作为真实算法/设备依据。

### G2：用途、存储及准入范围

| 既有Mode/用途 | 022保留的范围 | 存储身份与限制 |
| --- | --- | --- |
| FullSimulation / Test | 原显式模拟提供者及共同Test校验 | Manifest Profile=Test，独立Test根 |
| VirtualPlcIntegration / Test | 原虚拟PLC、测试相机/算法及测试Worker限制 | Manifest Profile=Test，独立Test根；Test通过只证明软件 |
| RealDeviceCommissioning / 同名用途 | 保真实PLC/相机及显式模拟光源；本次已授权显式选择真实算法，原模拟算法配置另行保留 | Manifest Profile=RealDeviceCommissioning、RunPurpose.Commissioning，隔离新数据根、有来源预算；不等Production质量验收 |
| Production | 保持未支持/未批准拒绝，不把已有代码分支当用途授权 | 无获支持Production Profile；不映射为Test读写，不新增生产Profile/自动升级用途；原未批准生产预算和执行成本门禁保留 |

G2-MODE已由本次用户确认：优先在既有RealDeviceCommissioning用途显式接入真实算法，不新增模式/Profile、不扩Production，不授权设备连接或部署。源码当前固定CommissioningAlgorithm且校验仅允许模拟算法，是本功能必要直接依赖修改，不是无法复用现用途的依据；逐项衔接见下文。真实激活仍需实际交付、能力/就绪/预算及A必要验收。

存储沿用StoreAccessGuard的绝对隔离根、链接/祖先链接检查、单Owner锁和维护闩，StoreCompatibilityProbe/StoreSchemaInspection核实际Manifest Profile、schema和用途；旧参数TestRoot和数据库名station01.test.db只是历史命名，不决定用途。RealDeviceCommissioning使用已批准的隔离新根和同名Profile，禁止现场数据库充当Test、禁止将旧Test库改Manifest冒充联调、禁止放宽AllowedTestRoot或自动搬迁/迁移现场数据。需要新增用途、Profile或根保护规则须另行确认，仅阻断该新范围。

准入顺序：先识别已批准Mode/Purpose和不可变运行身份；再核根/存储Profile、公共配置/预算/联调配置引用与摘要、ApprovedExecutionCostProvider；再核Provider角色、真实Origin/程序身份、Capability合同、输入格式及冻结模型版本；实际Ready/已应用证据在对应算法派发及依赖动作前核验。CapabilityRegistration注册声明不等引擎已加载；已知Origin也不等Real。未知/未交付/不兼容/用途错配如实拒绝相应路径，不以注册成功造就绪，不改变既有StartupReadiness“算法未就绪不统一阻断公共启动”的规则，不为无关算法扩大阻断。真实3D/F仍无有效结果不派依赖运动。

直接消费者（T009/T010定向覆盖）：Station01RuntimeOptions/Station01Registration、RuntimePurposes及RunPurpose、ConfigurationLoader/PublicConfigurationValidator、CapabilityRegistration/StartupReadiness、ApprovedExecutionCostProvider、StoreAccessGuard/StoreCompatibilityProbe/StoreSchemaInspection、MediaStore/SQLite装配、ConfigurationFreezer、CommittedRecipePlanReader/RecipeAdmission，以及关闭/恢复入口。不通过修改Production StoreProfile返回值“修通”未经授权的生产路径。

最小校验：原两种Test模式与原显式模拟联调矩阵保持；错Purpose/Profile/根或维护占用拒绝；Real提供者未知/未就绪保局部受限；Production保拒绝且不打开Test存储；真实用途正向验收须实际交付/就绪证据及A必要验收。分别记录“模式/存储/拒绝规则通过”“真实提供者通过”，不用Test正向贯通替代后者。只在隔离临时根做解析/检查或不触设备的装配校验，本轮不执行。

公共与产品各自既有冻结点保留。参数版本、能力版本、程序/模型/标定/转换身份保存进任务快照及派发依据；提供者必须给可核的实际加载/应用证据，启动文件摘要或内部ParametersVersion本身不证明模型应用。旧运行需保留原实例/绑定至释放；旧版本不可服务时明确受限，不能换用新模型或重发未知调用。

真实程序、模型及必要配置未交付时，可以完成配置校验结构、未集成拒绝、业务端口、受管管线和测试范围离线验证；不能安装一个假提供者来宣称Real。PythonWorkerAdapter/WorkerImplementation现Test语义保留，新真实适配器只在真实协议/实现已交付时编写；共享进程监督基础可复用经核对的部分，不扩展测试白名单冒充真实。

## 提供者必须承诺的事实

| 对接项 | 正式要求 | 未交付影响 |
| --- | --- | --- |
| 启动/就绪 | 实际程序身份、依赖、模型已加载及支持角色；常驻适用调用复用，实例数按实测 | 不能证明真实常驻及实际能力 |
| 输入 | 原Media/Capture只读引用、长度/摘要/逐输入身份；算法仅接PNG/PLY；采集/媒体适配核GalaxyRaw布局和CameraPro元数据；3D单位/IR/编码/RGB约定仍局部待确认 | 相应真实输入或转换Blocked |
| 版本 | 能力合同与模型/参数/标定/转换绑定及实际应用核验方式 | 生产版本与真实快照验收Blocked |
| 输出 | 角色合法结果、缺陷/融合规则及原业务映射；不能固定成功或默认定位/码 | 相应真实结果接受Blocked |
| 关联 | 本调用Call/Run/会话及逐输入映射可唯一验证 | 不可接受不明来源结果 |
| 释放 | 每输入释放或严格全部输入聚合释放的可靠依据 | 相应文件持有不可归零 |
| 执行结束 | 本调用实际执行结束，独立于Result；实际OS进程退出只终结同进程会话调用 | 不能回收执行占用或Final |
| 取消 | 尽力取消/未派发的保证边界；未知不重发 | 保受管未知，不误称释放 |
| 资源/期限 | 冷启动/稳态/最长耗时及CPU/GPU/内存/媒体实测 | 生产C/Q与预算不能定数 |

保留IAlgorithmPort.RequestAsync/AlgorithmDispatch.Exited业务边界；如真实协议逐输入释放，适配器验证逐项后在现聚合InputReleased语义发一次全释放，不把一项释放当整个Call。需要逐项诊断时扩展带MediaId的证据而不改变聚合事件含义。Exited只在已证明本调用执行结束且持有解除后成功完成；失败的Exited保未知，不用stdout EOF作为实际进程死亡证明。

算法JSON/IPC、SDK留Infrastructure/Algorithms；原始媒体转换留Infrastructure/Media及采集适配层；Application只消费已保存输入与来源事实，不解析GalaxyRaw/CameraProFrameZipV1或wire/PLC原码。必要转换文件继承原采集和转换版本、单独消费者持有；原图不覆盖，转换失败为真实技术失败。

## Host生命周期

按关闭准入 → 请求取消/保存本地未派发任务 → 监督已进入调用及按原政策停止真实Worker → 收集可靠释放/OS退出 → 必要事实提交排空 → 资源收尾的顺序协调。退出等待有限，但超期不写假释放；保存未完成/资源未知快照，禁止退出后自动重发。监督与持久服务在最后事实写完前存活，不依赖HostedService注册反序恰好正确。恢复/复位资源检查消费统一任务/输入/执行登记，移除只针对PythonWorkerAdapter的假设；不新增设备复位或自动恢复动作。


## R2：独立Host配置实际入口（拟实现、非已交付协议）

依据实际Program.cs从builder.Configuration.GetSection("Gaode")手动构造Options并调用AddStation01，选择此现有入口添加两项宿主键，不另建运行入口：

| 来源/字段 | 类型与校验 | 消费者/责任 |
| --- | --- | --- |
| 现有appsettings/命令行/环境配置中的Gaode:RealAlgorithmConfigPath | string，绝对本机JSON路径；与Sha256成对、不可为空；不使用WorkerScriptPath/ImageManifestPath | Program读取→Station01RuntimeOptions.RealAlgorithmConfigPath→Station01Registration调用严格RealAlgorithmConfigurationLoader（Infrastructure/Configuration拟新增） |
| Gaode:RealAlgorithmConfigSha256 | string，64位十六进制；对原UTF8文件字节SHA256校验、比较忽略大小写 | 同链传递；加载保存SourceFile/原JSON/Digest；文件变化不能偷换在途绑定 |
| 两键均缺失 | 只保旧明确模拟联调选择；公共/产品Algorithm Binding均必须与此选择一致 | Real绑定无描述文件拒绝，不回退模拟；任一键存在均表示显式真实选择，仅允许RealDeviceCommissioning，Test/Production拒绝选择 |

描述文件schemaVersion固定为real-algorithm-host/1（本项目拟定内部schema，不是Worker wire版本）。单独严格JSON schema放022/contracts/real-algorithm-host.schema.json（实现阶段生成），加载器拒绝未知字段、重复属性、非法类型、数字字符串、未识别枚举和无效引用；不修改001公共schema或放宽ConfigurationLoader未知字段校验。

| 描述文件字段 | 类型/来源和要求 |
| --- | --- |
| schemaVersion、id、version、purpose、source | string必填；purpose=RealDeviceCommissioning；版本/资料来源明确，不接受SAMPLE_ONLY等占位作为实际证据 |
| commissioningRef、publicRef、budgetRef | object {id:string,version:string}；commissioningRef沿原CommissioningId/Version身份，public/budget匹配正式保存引用和用途；真实选择用此描述提供联调来源元信息，不要求旧CommissioningAlgorithmInputs的固定结果/码文/槽位。旧CommissioningPath/Sha256仅旧模拟选择必需，真实选择不读取其算法结果、不自动改旧文件 |
| codeRule | object {id:string,version:string,source:string}；沿现decoded-content-exact/1.0有来源解析规则，实际F rawCodes唯一匹配已保存配方，不从期望配方生成码 |
| provider | object {implementationId:string,algorithmVersion:string,deliveryKind:string,artifact:FileRef,entryPoint:string,arguments:array<string>,dependencies:array<FileRef>}；deliveryKind=PythonPackage/Exe/Dll，入口签名/启动及通信方式待DEP-ALG-01定稿；不实现任意动态插件，未支持交付形式明确NotIntegrated |
| modules | array<ModuleRef>；module=TrayPose/FDecode/EDecode/DefectSingle/DefectFusion，与现AlgorithmPurpose对应；每项bindingId/capabilityId/capabilityVersion/resultContract为非空string，inputCount为正integer（当前PLY单文件3D/F/E/单图1、融合2；3D若确认需要同capture PNG，InputCount必须匹配实际输入集合，届时定向同步TrayPose能力/端口验证消费者，不把两文件假报一输入）。无重复module/binding，按公共准备及实际配方所需角色/用途检查 |
| ModuleRef模型参数 | modelId/modelVersion/parametersVersion:string；modelFiles:array<FileRef>、parametersFile:FileRef；文件摘要/原值参数以算法方提供类型/单位/范围校验，不填未知阈值；modelVersion/parametersVersion分别匹配正式公共或产品绑定版本及实际加载证据 |
| inputs | object {pngBitDepths:array<integer>,pngColorTypes:array<string>,plyEncoding:string|null,plyRgbRequired:boolean|null,trayPosePng:object}；png为实际声明8/16位及Gray/Rgb；plyEncoding=ascii/binary_little_endian。trayPosePng={required:boolean|null,plane:string|null,bitDepth:integer|null}。null仅表示此3D交付选择尚未确认（不是可执行值），Loader可以保存明确的局部缺项，但TrayPose激活必须字段齐备；未知选择不填false或默认编码；TrayPose模块不完整仅该模块拒绝，已明确2D转换/其他模块继续 |
| layoutFile、calibrationFile | FileRef|null；TrayPose必需有实际布局/标定来源和版本，映射槽位/region/cell、F绝对mm/已确认设备frame。文件未提供或单位/无效点规则未知时3D结果用于动作的接收阻断；不填测试位置 |
| budgetRef及资源上限 | 复用同purpose正式预算Limits.WorkerPerRole/AlgorithmQueuePerRole与对应RecipeExecutionCostProfile/公共BusinessMs。共享真实实例计数聚合不能多role分别突破实际池上限；无实测适用预算则角色激活受限，不由描述文件新增未经批准数字。C批次队列选型延期 |
| FileRef | object {path:string,version:string,sha256:string}；相对路径只对描述文件目录解析后固定绝对路径，长度/摘要核对、只读使用；禁止输入/输出覆盖源或指向现场/Test共享库。缺失文件可报告ComponentMissing，不视为成功加载 |

加载分两步：严格结构/用途/引用校验可在无真实组件时完成；逐文件/运行环境/已支持入口/角色加载及实际模型应用核验决定真实激活。错误分别保ModeOrStoreInvalid、AlgorithmConfigurationInvalid、CapabilityMismatch、ModelNotReady、ComponentMissing/NotIntegrated诊断（拟内部分类，沿真实原因，不将示例当已有wire错误码）。结构错误拒绝装配，不打开未经许可存储；未交付允许明确局部未就绪状态，不注册假Real成功。

完整消费链：Program配置键→Options→Registration/严格独立Loader→用途/Store保护及PublicConfigurationValidator校验（既有algorithm Binding允许显式Real，但PLC/camera仍Real、light仍Simulated）→真实IAlgorithmPort与IAlgorithmCapabilityProvider注册→CapabilityRegistration按codeRule/实际能力声明注册→StartupReadiness及对应角色Ready检查→ConfigurationFreezer在原公共冻结点保存描述原JSON/Digest、文件/model/parameter/layout/calibration引用和公共绑定→RecipeAdmission/CommittedRecipePlanReader在原F绑定冻结点核产品所需模块/参数→Run/Call快照→Runtime/原执行消费者→结果仲裁/实际版本应用核验→AlgorithmFact/对象判定SQLite落库重读→原分拣/Final。

ConfigurationFreezer在真实选择分支核独立描述的commissioningRef/publicRef/budgetRef/codeRule及来源，而非强制旧模拟CommissioningJson；旧模拟分支校验保持。快照身份材料须包含独立描述Digest和所选文件版本/摘要，实际加载/应用证据另存，不仅复制请求版本。真实版本替换只能在原冻结点形成后续Run新绑定，旧Run实例/文件版本留至可靠结束；不引入热加载平台。

StartPublicPreparation现ICommissioningRunInputs仅服务显式模拟输入冻结；真实选择不装配CommissioningAlgorithm作该消费者。真实分支沿正式ConfigurationFreezer/已保存ExecutionInputs执行来源、布局/能力与版本冻结，StartPublicPreparation的FreezeRun/BindRecipe/ReleaseRun调用按实际所选提供者用途分开；不为真路径增加无操作ICommissioningRunInputs或重复业务冻结接口。真路径不读取配方CommissioningFPosition覆盖真实3D fLocation、不要求预填Slots/RawCodes/Results；保正式已保存配方选择/来源/场景校验，依本次真实3D观察/经标定F定位及真实F码绑定。原模拟分支保持原有效校验，真实缺配置/定位拒绝对应动作。

IndependentRecipeApplication读取FrozenPublicConfiguration并重算SnapshotId时，必须一并恢复该Run已持久保存的真实算法原JSON/摘要/冻结文件引用，不读取当前Host描述代替旧版本；StartPublicPreparation原冻结Audit保存此材料。缺历史字段不得补当前真实身份，沿历史用途拒绝或原规则读取。Station01Registration中CommissioningRecoveryService的resourcesReleased predicate必须消费独立未回收资源登记（含终态Run的Unknown Call/输入），不能仅凭AlgorithmRuntime.ActiveExecutions=0许可原恢复；不新增复位/动作或自动重发。

T010依T009和T031-A后，通过Program共享的真实读取方法/Options/AddStation01实际链做隔离解析/装配与消费者断言，不能只new DTO调用独立Validate。覆盖两键缺一/摘要错/字段非法/未知属性及Real选择缺组件、能力不匹配、未Ready局部拒绝；原Test/模拟联调与Production拒绝保持，Production存储打开次数0。真实联调正向装配/加载在T012/T013依真实交付验证；Test不伪装Real就绪。仅DI/持久入口，不启动真实设备HostedService。

## PNG/PLY媒体适配、核心映射与所有权

MediaStore保存原始媒体及真实采集元数据后，媒体适配器（拟Infrastructure/Media/AlgorithmInputConverter）读取有租约的原引用：2D支持已知Mono8→原尺寸Gray8 PNG；其他实际位深/行步长逐项按真实元数据验证，不将未知格式猜成Mono8。3D现已知XYZ Float32→binary little-endian XYZ保字节路径可独立实现/离线验证；该产物未获算法方接受时不得派发真实TrayPose。只按确认编码补ASCII或实际RGB/同capture伴图，不预实现全部PLY变体、不造RGB/删零点/更改单位坐标。深度显示图不是浮点深度或标定依据。

转换步骤读取raw租约、逐文件取得必要写入/工作额度，原子发布完整产物及sidecar/媒体索引并提交转换来源事实后才可派发；失败/CommitUnknown不登记可用输入，不覆盖原图。产物保存CaptureId、RawMediaId、DerivedMediaId、原/产物长度和SHA256、源格式/实际尺寸或XYZ布局、ConverterId/Version、已选择编码/平面、转换参数摘要、原采集元数据/事实引用。转换事实是媒体来源事实，不借AlgorithmFact/假算法结果保存。CaptureFact仍指可靠原采集，AlgorithmIntent/Fact逐输入同时存主Capture及实际派发DerivedMedia引用。

最小共享边界沿既有IMediaStore/CaptureAlgorithmMessages定义“取得本次可用算法输入”能力：输入为已提交RawMediaRef、冻结输入格式/转换引用及原有限保存期限；输出为逐输入AlgorithmMediaInput及上述DerivedAlgorithmInput来源/Committed引用，尚未保存或格式不支持返回明确受限/失败。实现由Host装配的采集/媒体适配器负责，业务执行消费者只等待并核关联/回执，不解析raw或编码PNG/PLY。复用MediaStore文件/索引/租约能力，不能把转换产物伪装成原相机帧送旧SaveAsync元数据校验；不新增第二套业务执行/算法调用端口。原始保存入口含义不变，转换后的业务算法请求以实际文件引用派发。

派发到核心只含已保存PNG/PLY完整路径及V0.3对应module/context/parameters；外层适配保存Run/Call/Operation/WorkerSession/逐输入映射，requestId明确映射CallId，输出检查requestId、实际程序/模型/参数版本及合法data。缺陷结果沿原NG/Pending汇总；无码/多码沿原业务规则；3D映射靠冻结布局/标定。V0.3未包含的IPC受理、取消、输入释放、执行结束须交付后定义外层桥接，不把success或核心return直接冒充InputReleased/Exited；若交付明确证实返回前已关闭文件并结束本次执行，适配器才可据此发相应可靠证据。现Worker/2.0参数缺失不可假称已兼容。

原文件、转换读取、派发产物、待融合各有消费者；转换完成仅结束转换读取，不等推理/融合结束。多文件取得租约失败回滚仅本次未移交持有，成功移交不中断所有权；同3D伴图严格同Capture。原与派发产物保关联及持久磁盘计数，算法不删输入，最后工作消费者结束只释放工作保留额度。真实文件/SQLite重读验证源和派发产物，不只assert后缀或样例JSON能解析。

### A媒体增量签名（实现前确认）
MediaRef新增可空AlgorithmInputProvenance：SchemaVersion=algorithm-input/1、RawMediaId、RawRelativeKey、RawFormat、RawSha256、InputSha256、ConverterId、ConverterVersion、Width、Height、PixelFormat、PointCount及保留的PointUnitSource/CoordinateSource。它是媒体来源事实，不是AlgorithmFact；来源未知以null记录，不猜设备坐标。IMediaStore.PrepareAlgorithmInputAsync(raw, token)仅处理已提交、元数据摘要可核的GalaxyRaw Mono8和CameraPro XYZ；未知格式拒绝，现有模拟媒体不自动转换或伪称真实。转换生成仍须提交既有Media事实后MarkCommitted；恢复通过原RawMediaId查同采集事实及原文件/摘要，不能为产物伪造CaptureFact。PLY仅提供已验证binary little-endian XYZ无RGB候选，配置未确认不得用于真实派发。
采集Reservation只预约实际单次内存及待写载荷磁盘；元数据和转换文件独立预约磁盘，保留失败部分亦计实际占用。MediaLease另占工作保留额度，消费者Dispose只解除工作保留，不删除文件或减持久占用。三类额度分别可观察，沿既有有限memory/run-file/data限额，C批次机制不启用。
A冻结消费者增量：DetectionRequest带原FrozenConfiguration的RealAlgorithmConfiguration（不可变描述副本）；AlgorithmRequest保存可空FrozenModule及AlgorithmConfigurationDigest，资源事实保存原Request以完整关联Role/对象/面/阶段/轮次/逐输入/冻结版本。真实选择不接受非Real算法Origin，PNG 8 Gray/PLY binary little-endian无RGB且3D伴图required=false须明确确认；未知选择局部拒绝，不猜值。转换输入由媒体适配产生，再沿各消费者原Media保存入口提交后派发。
A稳定输入声明：沿现IAlgorithmPort添加InputRepresentation(role)声明NativeMedia/Png/Ply，不创建第二业务接口；旧显式虚拟适配默认NativeMedia。新真实选择仍须独立配置、真实来源/就绪/冻结模块及已确认格式，不能靠声明升格Ready；明确Test提供者可声明Png验证同一软件转换消费者，来源仍Test。业务只按声明请求IMediaStore，不解释SDK媒体。

A真实选择准入增量：独立真实描述选中时，实际能力未注册/不匹配或算法未就绪属于RealAlgorithmNotReady控制阻断，公共Start保存ConfigurationBlocked且不派发采集/机械动作；原显式虚拟分支原有算法问题分类保持。配置读入不等真实就绪。

T026/T038-A关联修正：已绑定WorkerSession的Call拒绝缺失或不同会话事件，匿名InputReleased/WorkerExited不能解除原占用；本调用的成功Exited任务仍是可靠执行结束证据。结果必须保持原FrozenModule模型/参数文件版本及摘要身份，不只匹配配置摘要。

## 2026-10-10交付状态及现行合同
DEP-ALG-01更新为V0.3.1已交付、逐模块待验证；旧“未交付”表保留历史范围。仅核说明/代码，真实加载、推理和释放仍NotExecuted，不提升当前IsReady或注册Test为真实。
现行细节见[阶段合同](stage-integration.md)。DEP-CAL-03/RULE-04只阻依赖标定/质量的路径与最终验收，不阻DefectSingle真实调用/Pending保存。最终结果驱动不依赖C。
