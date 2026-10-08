# CC-021：可配置联调配方与虚拟光源选择

2026-10-08；依据用户最新决定；FR-017/018、SC-007/008。此合同先于共享接口及页面改动生效，尚无本增量实现或验证通过结论。

## CC-01 正式配方决定流程

联调与最终软件复用共同RecipeDefinition、唯一校验、SQLite目录、F码绑定、冻结、规划及实际执行器；不能把单品两面翻转样例变成联调软件唯一流程。不新增独立联调配方库、流程引擎、产品名称分支或测试号分支。沿项目已确认工艺能力配置槽、成员、面数、面序、AB/CD、坐标和适用取放/分拣参数；不把配置能力等同于任意新增工艺。

虚拟算法沿现业务接口实际消费本Run真实采集并保存的媒体。受控输入集合支持多个配方的独立输入记录，每项带id/version/source及明确适用配方RecipeId/Version/DefinitionDigest、场景、槽/对象/阶段/面/相机。启动选择意图与本次F码绑定一致后，冻结相匹配的输入记录；缺项、歧义或版本不匹配只阻断依赖动作，不自动借上一配方输入或用统一OK补齐。公共3D/F准备输入在绑定前需有对应本次启动选择及公共配置的明确作用范围，实际F绑定后再次核对，不提前伪造配方已绑定。

坐标、姿态、料盘码和分拣结果如影响真实运动，必须使用用户确认安全且可追溯的值。改变配方不产生新的安全值；测试样例只能标OFFLINE。额外E扫码等现虚拟实现不支持的已确认工艺，应按相同结果合同补适用的受控输入及消费，不删步骤、不宣称全部可用。现场缺值与确定的软件不支持分别报告。

## CC-02 配方级虚拟光源

在既有配方配置弹窗内增加一项“虚拟光源”复选框，作用于该配方的全部产品采集步骤，不增加每相机/每张混合模式编辑器。用户本次明确授权该控件及光源字段启用状态变化；其他结构、导航、文字及原型归档不变，精确差异纳入现原型检查。

勾选时：保存显式Simulated模式，光源亮度输入禁用并不要求填写，通道与稳定等待也不再是本配方执行的必填条件。不派发SetBrightness/开灯/关灯，不等待光源稳定，不用默认亮度/通道/等待值补齐；相机曝光、增益、适用ROI、真实触发及媒体/SQLite保存继续正常校验和执行。已有真实光源参数如保留，属于未启用内容，不能据其声称本轮实际应用。

取消勾选时：保存显式Real模式，按正式光源配置校验亮度、通道及等待，依次设置亮度、开灯、稳定、真实采集、关灯。真实适配器未装配或必要配置缺失时，在依赖采集派发前明确拒绝，不回退虚拟、不返回物理成功。本轮完成选择及准入规则，不编造真实控制器协议，也不把真实光源硬件实现纳入本轮交付。

曝光和增益属于相机参数，两种模式都不能跳过。虚拟算法与光源模式独立：取消虚拟光源不切换算法，也不改变PLC清零、安全门禁或运动规则。

## CC-03 公共采集与快照

3D/F公共采集发生在产品配方绑定之前，不能在此时偷用尚未绑定配方的标志。本轮公共采集由版本化公共运行配置显式标记为虚拟光源，同样跳过外部光源参数/控制，保留相机参数与真实采集。后续接真实公共光源时，由公共配置改为Real并检查真实装配，不将公共参数偷偷搬入产品配方。

共同正文/序列化、完整GET、校验、POST/原ETag PUT、SQLite重读、目录、冻结计划、CaptureRequest、设置摘要、采集证据门禁及来源投影都必须传递模式；记录显式跳过及模式来源，PhysicalLightApplied=false，不将跳过伪装为控制器应用成功。历史正文继续按原版本读取；新模式需要版本化，禁止用缺字段默认Real/Simulated改变历史执行语义。准确版本号和迁移映射在T033登记后才能改产品模型。

在途Run使用旧冻结模式与参数，保存新选择仅影响后续合法运行；切换不得修改正在拍照的运行。真实适配器的错误关灯处理属于同一采集生命周期，不引入重复拍照或未知自动重发。

## CC-04 最小验证与现有证据边界

- V07：两套具有实际面序/步骤差异的已支持正式配方，经页面保存重读及选择、正式规划/执行端口和各自版本化虚拟输入，核对实际Move/Capture/算法请求差异及实际媒体/SQLite。用最小代表证明配置生效，不扩全组合，不只改名字，不拼成真机整链通过。
- V08：实际页面勾选、保存重读及编辑模式；Simulated缺光源参数仍可采集且光源派发数为0；相机参数仍应用。Real缺适配器明确阻断，不能借模拟实现通过。用声明离线的真实光源端口替身验证有参数时的控制顺序，标OFFLINE，不声称真实灯已验。补一项模式编辑的在途/后续冻结隔离。

既有V01–V06、020代表单品翻面和旧Test证据保留原环境/版本，不改标签，不作为V07/V08已通过。020 T055/T056及未提供现场安全值继续局部阻断。

## T033共享字段定版及交付授权（2026-10-08）

采用版本化可选扩展lightExecution={schemaVersion:"light-execution/1",mode:"Simulated"|"Real"}，置于共同RecipeDefinition、RecipeRunPlan、PublicConfiguration、CaptureRequest及CorrelatedCaptureFact；写入设置摘要，JSON null字段不输出。未含此扩展的历史正文仍按原schema及原灯控制/证据语义读取，不静默迁移。新建配方显式Simulated；编辑复选框保存显式选择，未编辑历史记录保持原字段。光源相关DetectionCaptureSettings的lightChannel/brightnessPercent/settleMs允许null，仅显式Simulated免校验；曝光/增益/ROI不变。公共CaptureParameters.lightLevel同理。此为现正文中的light-execution/1扩展，不强制重写历史recipe-definition/5或SQLite表。

CommissioningConfiguration保留已有主输入记录，增加recipeInputs[]，每项含id/version/source、expectedRecipe、slots、fLocation、mappingSourceReference、rawCodes、results及可选entityCodes；共享能力/公共配置/预算仍由外层版本约束。启动FreezeRun增加可选ExpectedRecipeRef选择意图参数，从主记录及集合唯一匹配RecipeId/Version及scenario，不填默认值；无选择仅允许单一记录的旧声明调用。每Run保存选择后的独立输入并在F绑定后再核DefinitionDigest/Model/FCode，不使用全局当前配方。EDecode采用EntityCode/decoded-code/1、按ReadECode实际步骤作用域的显式entityCodes记录，缺项局部阻断，不自动生成码。

用户已明确解除本轮打包和本机部署限制；原文“不打包/不部署”为历史授权边界。本轮可构建并制作包，检查本机安装/启动条件，保留已有安装及配置；无现场安全输入不得启动依赖真实运动。此授权不含Git提交/推送，不覆盖现场安全值确认与020 T055/T056证据门。

## 用户确认增量：配方中的虚拟F定位（2026-10-08）

用户确认示教坐标人工填配方，本次单品翻面样件的虚拟质量结果为OK；虚拟算法必须实际消费本Run媒体后记录调用成功，不绕过采集/保存或设备动作。用户将提供F读码XY，并要求加到虚拟算法联调用的配方设置。新增FR-019/SC-009：共同配方可选commissioningFPosition={schemaVersion:"commissioning-f-position/1",x,y}，页面在既有配方弹窗基础信息增加“虚拟算法 F读码X (mm)”及Y，由人员手填，不补默认坐标。后端校验有限值，启动再按公共运动配置核单位/坐标系/行程；所选配方版本与目录一致后从实际保存正文读取，构成带配方版本来源的FLocation，冻结给本Run虚拟3D的首次定位结果，实际F读码后仍再次核绑定。真实算法不消费该字段。旧记录保留原声明的受控输入策略；新建联调配方必须显式填写F位置才能通过相关运动准入，不能偷偷沿用旧示例F位置。

准确字段及端口：RecipeDefinition.CommissioningFPosition随正文/摘要保存；ICommissioningRunInputs.FreezeRun(..., selection=null, fLocation=null)增加可选冻结定位输入。StartPublicPreparation从本次expectedRecipeRef匹配的真实目录取得该位置，校验引用后转换FLocation，不从前端POST直接接收运动值。运行开始后修改配方不改变已冻结公共/虚拟输入。已有公共F固定位置及真实3D定位职责不改；新增位置仅适用本联调虚拟算法。FR-009/原“不在页面设置算法坐标”对此用户明确授权的F XY作唯一例外，其余安全值仍不允许默认补齐。


## 2026-10-08 部署与现有成功配方录入授权

用户目标：完成部署包，再录入本机虚拟上位机成功联调配方；缺失项采用不参与当前流程的明确占位/跳过规则，不让必需字段缺失。来源以当前启动入口选择的Gaode-PlcCommissioning-1.1.6-win-x64/config/recipe.local.json为准，保留SHA256及逐字段映射，原件只读。正式共同Validator/SqliteRecipeStore保存并由新实例重读及BuildExecutable核验，不直接写SQL或给旧Test改用途。

原文件为单品两面AB→翻面/放回/3D复查→CD、全部OK；F65/50、检测Z5、取件/放回10/10、型号666有来源。外部光源显式Simulated，E扫码关闭，额外旋转不启用。缺相机曝光/增益采用明确联调初始设置并标待调优，不能声称历史真实拍照参数；曝光不改变运动目标。分拣夹爪缺值，可按用户授权给1作为未启用占位，当前固定OK路线不派发分拣，分拣安全配置保持缺失，正式适配器必须在派发前拒绝不具备配置的分拣，不借占位放行NG/Pending。FlipPick/FlipPutBack以及3D/F/Unload仅XY的Z正文占位为0（现场适配器不派发该Z）；检测Z严格使用来源5。不可用占位伪造PLC安全语义、机械行程、完成反馈或首次/恢复准入。

部署包含正式Host、Desktop、CameraWorker、前端、维护准备工具、SQLite配方、来源映射、版本/工作区源码摘要、验证/已知缺口及回退说明；新版本独立目录，启动入口默认仅检查，不连接设备。现场配置未知时包保持待配置/禁止运动，不冒称现场通过。用户F来源已找到，不能继续泛称F位置未提供；后续仍可通过页面编辑，实际安全以用户现场核定为准。

新增维护CLI --inspect-recipes <recipeRoot> <output>：只读正式配方库，经共同准入/规划并输出版本、正文、实际计划（不执行设备）；--seed-authoring保持原保存规则和拒绝覆盖。此接口仅部署工具，不新增产品前端或业务API。

确定缺陷：真实PLC阶段适配器仅允许TargetPurpose=Production，错误拒绝已授权RealDeviceCommissioning卸料阶段。修正为与设备实际ConfigurationPurpose严格匹配，保留身份、采样、位置、安全、阶段与证据门，不将Test放行到Real。

录入后由Prepare-CommissioningInputs.ps1读取正式重读结果，生成带准确RecipeId/Version/DefinitionDigest的本机虚拟输入：当前单品Present/Normal、F来自保存正文、四张单图及两组融合OK。按实际Capture步骤的slot/material/stage/face/coordinateEpoch/camera确定作用域，不生成通用强制成功。维护CLI --inspect-commissioning <path> <sha256>复用正式CommissioningAlgorithmInputs.Load检查来源、版本及作用域结构；不连接设备。生成配置不代替真实媒体消费，也不绕过PLC安全门禁。

2026-10-08 T050配置实查：旧001 public-config.schema.json未收录本合同已有lightExecution可选扩展，且将lightLevel强制必填，正式加载会拒绝本轮公共虚拟光源。021维护public-config.runtime.schema.json作为本用途schema覆盖；部署配置在独立schema目录采用它（文件名public-config.schema.json），其余schema原样复制。仅增加light-execution/1结构并允许省略lightLevel；非虚拟模式仍由正式PublicConfigurationValidator强制要求光源参数。原001合同、旧包和历史配置不改；字段语义不新增。
