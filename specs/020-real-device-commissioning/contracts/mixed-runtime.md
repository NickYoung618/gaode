# MC-020 阶段B：显式真实设备混合联调

版本020-stage-b/1，2026-10-08。确定软件部分已实施，验证与现场限制见validation-stage-b.md；FR-010/012/014/015/017。

## MC-01 装配和用途

技术方案新增明确`Mode=RealDeviceCommissioning`，用途同名。它表示有真实设备的受控联调，不表示Production质量验收。Test配置不得原样改Purpose冒充现场依据。Production继续拒绝隐式模拟。

| 组件 | 本轮提供者 | 必须留证 |
| --- | --- | --- |
| PLC | Real，经LatestProtocolPlcDevice | 现场布局/配置、动作与清零/安全来源 |
| A/B/C/D/E/F/3D相机 | Real，经原persistent worker | 角色/序列号/NIC、SDK、session/epoch、实际帧 |
| 算法各使用角色 | 明确Simulated实现，经IAlgorithmPort | 提供者版本、参数/受控结果摘要、实际媒体引用、输出与适用范围 |
| 外部光源 | 明确Simulated实现，经ILightGateway | 配置通道/亮度/开关/等待实际消费；不报物理灯Applied |
| 配方/数据库/媒体 | 正式模型/SQLite/MediaStore | 真实意图/提交/快照和文件引用 |

使用同一Station01业务协调器，不调用AddSimulationAdapters整套覆盖真实组件，不用CaptureOnly或Python工具代替正式链。正式Host与联调工具不得同时写同一PLC；部署前关闭另一控制端并记录控制权。

## MC-02 全部用途消费者

新增用途必须贯通：Station01RuntimeOptions/配置绑定、Station01Registration/RealCameraRegistration、公共及预算schema、ConfigurationLoader/PublicConfigurationValidator、Station01Policies/CapabilityRegistry/CapabilityRegistration、RecipeAdmission/Freeze、ApprovedExecutionCostProvider、StoreManifest/维护准备和读取校验、结果/状态投影。现公共/预算/模拟一律Test与Production预算/Real阻断须改为明确分支，不能只删除检查。旧模式行为保留，禁止以“未知模式兜底模拟”。

Parser须明确登记适用现场码规则/版本/来源；现code.test-tray-format只准Test，不能直接扩大测试规则授权，可在确认同规则适用后复用解码实现。F原码匹配规则仍沿正式目录。ApprovedExecutionCostProvider现有5000/10000/5000及CaptureWait8000/AlgorithmWait15000属于Test常量，新用途须从有来源的预算配置读取对应项，不能只放宽Purpose检查。

本轮运行须真实时钟、有来源预算、隔离的新数据根和唯一Host。数据库已有schema能表达Profile时只用现字段，不为新用途自动迁移；任何实际schema缺口先追加契约/tasks及受控迁移设计。TestRoot旧参数名不作为Test数据可用于真机的授权。

联调用途只描述组件组合；按RC-020已确认规则，当前配方软件校验保存后可选择运行，启动自动核对参数和设备条件，不再等待人工版本批准。

新联调配置在启动前核对用途、引用版本/摘要、PLC地址/机械与安全说明、七相机绑定、算法/灯提供者、预算和配方准入。缺项输出按依赖分类的错误；相机可读/独立采集就绪不等于PLC可运动。

## MC-03 虚拟算法

现SimulatedAlgorithm仅支持FDecode，须补本轮实际消费的AlgorithmRole.TrayPose、FDecode和Detection；SingleDetection/FaceFusion属于AlgorithmPurpose能力，分别使用1/2个输入，两者均沿AlgorithmRole.Detection请求和现结果合同，保持既有结果合同和有限终态。不要为了不使用的E/R分支注册假成功能力。能力绑定按已登记提供者/合同/输入数/用途，不再依赖“是PythonWorkerAdapter”这个具体类型作为唯一能力来源。

受控输入至少含id/version/hash、提供依据、用途/场景/料盘/预期配方范围、槽/成员/阶段/相机/算法角色、已确认位置/姿态/有无及结果。未填项不是默认OK/有料/零坐标。数值有限、单位/坐标系/槽范围和现场限制均须核对。现场参数由用户后供，本合同不填具体值。

算法收到正式关联请求后，读取本次合法媒体引用，核对CaptureId/RunId/角色、实际载荷身份，再按该角色受控输入产生显式模拟结果并经原保存链提交。不能只看配置就跳过图像、不能用历史帧配当前结果。启动冻结3D/F前置依据；F绑定后核对预期配方与实际摘要并冻结配方阶段依据。错配只阻断依赖动作，不发纠正运动。

算法技术成功不等于真实检测质量已验证；所有结果保留Simulated来源。算法失败沿已有有限结果/处置，缺安全定位依据不得继续依赖运动。不得强制OK以绕开未配置的分拣目标，完整配方仍按RC-020校验。

## MC-04 七相机与代表路线

七角色必须唯一配置并正常接入。代表单品翻面路线只调用其实际需要的相机；未参与的角色可在独立真实采集验证中留下新帧和媒体，分别标注，不能说每台都参与本盘，也不能增加E扫码/R/取放动作凑数量。019已有正常七相机证据可作基线，新参数应用与当前组合必须另留证据。真实SDK阻塞/物理断线未验项仍保留。

补充离线用途验证：Test/loopback是测试执行环境标记，不代替业务Purpose。T033显式验证RealDeviceCommissioning准入/Freeze，并回归旧Test/Production规则；fixture来源必须如实标识，不伪装Real或修改正式启动检查。正常已确认Test链与新用途配置/准入/参数消费者验证分别留证；现场安全未明时，新用途完整Host/真机链仍为Blocked，不能凭组件测试宣布完成。

新增算法日志由T052实施，采集/灯及参数/保存阻断日志由T050/T051实施；沿现持久诊断，记录受理、阶段、实际结果/失败及必要释放，关联运行/采集/调用/配置身份及来源，限制重复。T058在已有必要失败用例中验证可定位性。

实施接口落位：CommissioningConfiguration记录身份/来源、公共与预算引用、预期配方身份及摘要、码规则来源、公共灯通道、算法能力声明和按槽/成员/阶段/面/相机限定的受控结果。配置由Host显式路径和文件SHA-256加载；不生成现场示例值。ICommissioningRunInputs分别在首次运动前冻结运行适用范围、在F绑定后核对并冻结配方输入。AlgorithmRole保持既有枚举；能力由IAlgorithmCapabilityProvider显式声明，Python和联调实现各自提供实际能力。联调使用独立CommissioningAlgorithm，经同一IAlgorithmPort；旧SimulatedAlgorithm及Test SimulationProfile保持原职责，不把Test配置改名使用。

该模式不加载Test SimulationProfile。冻结配置中的历史Simulation字段可空，另保存CommissioningJson/Digest及来源；旧启动请求SimulationRef字段保留，仅核对Host固定引用，不作为真实设备或模拟输入授权，新的联调配置由Host加载并核对公共/预算引用。历史快照、旧模式及页面结构不变。

实施用途对齐：Host显式传递PlcRuntimeOptions.Purpose；联调机械配置、姿态映射、安全位和适用R依据均要求RealDeviceCommissioning，与运行用途一致，不复用Test或Production用途配置。旧调用未指定Purpose时保留既有Provider对应用途；显式用途错配拒绝。现场地址布局仍按独立FieldAddressProfile核对。公共3D/F绑定须对应七机配置中的3D/F角色或其唯一序列号，不能等到运动后才发现相机绑定不存在。

既有运行身份RunPurpose.Commissioning对应本轮配置用途RealDeviceCommissioning；不新增枚举或改变旧Test/Production解释。正式handoff检测请求、CommittedRecipePlanReader及IndependentRecipeApplication以已冻结CostProfile用途核对联调配置，不从空Approval推导用途；联调快照重读须核CommissioningJson/Digest，不能加载空SimulationJson。T036/T037覆盖这些消费者。

实际位置依据：旧Real适配器不声明坐标系和单位来源。联调机械配置必须显式提供PositionBasis(Frame,Unit=mm,Purpose,SourceReference)，不填现场默认；适配器将该依据附于实际新读坐标，沿原PositionForPurpose区分XY/检测Z/扫码Z/抓手Z。该配置只解释读数，不制造到位或安全事实，仍须当前采样和既有清零记录。缺依据拒绝联调装配，旧用途保留历史限制。T045/T041同步此消费者，T044/T039验证元数据与无运动许可。
