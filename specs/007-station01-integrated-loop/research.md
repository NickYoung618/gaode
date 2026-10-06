# 007 方案研究与决定

**日期**：2026-09-23。依据 [007规格](spec.md)、宪章3.0.0及仓库当前文档和代码只读核对。本文件记录设计决定，不表示下述缺口已实现。

## R1：选择实际虚拟组合

**决定**：使用已有唯一 Host 的 `VirtualPlcIntegration` 测试模式和正式 Modbus TCP 端口，保留独立 VirtualPlc；在该模式明确替换当前进程内合成采集、进程内算法和直接返回结果的 Detection 适配器。相机作为 Host 进程内正式采集端口的固定图片适配，不另起相机服务；算法为 Host 外独立虚拟进程，实际接收执行请求与受控媒体，返回结果。SQLite/媒体仍走现有写入与查询通道。

**依据**：`Station01Registration.cs` 在 `VirtualPlcIntegration` 下仍通过 `AddSimulationAdapters` 绑定 `SimulatedCapture`、`SimulatedAlgorithm`，`AddFirstStationStageAdapters` 绑定 `SimulatedDetectionPort`；前者生成 SyntheticMediaFixture，后者一次返回全部 ExpectedObjects 的 OK，没有执行计划中的检测采集和算法。当前 `PythonWorkerAdapter` 直接抛 `AlgorithmNotDispatchedException`。003合同要求 Detection 经正式端口产出可追溯输入。仅复用现状无法满足007 FR-003–006/009。

**备选及取舍**：延续进程内模拟只能复用003局部证据；把虚拟相机另做HTTP服务增加无需求的进程和接口。选最小必要适配与独立算法进程，复用既有 `WorkerProcessSupervisor`、NDJSON消息编解码和媒体租约；如果现有消息不足以表达结果，先同步003共享算法合同再最小扩展，不新建通用消息平台。

## R2：固定图片与随机结果的可信边界

**决定**：007测试配置指定单个固定图片根和用途清单，启动前检查文件存在、格式/大小与摘要并冻结版本；每次正式采集按当前 role/plan step 读取对应固定图片，经过真实墙钟3–5秒模拟拍照与读取，产生本次唯一 captureId/mediaId，再由现有 MediaStore 持久化。3D角色需提供既有合同所需的可验证模拟输入语义，普通二维图片不冒充真实点云。独立虚拟算法按 Height、FDecode、Detection 用途处理合法输入，运行10秒模拟计算后返回对应结构；随机种子、范围、结果、worker会话和媒体引用可复核。F 正常样本必须返回当前合法唯一配方码，随机性限于仍能合法绑定的范围；不随机化身份、PLC反馈或保存结果。

**依据**：007 FR-003–006；001 `persistence-handoff.md` 的媒体提交先于算法意图；003 `detection-port.md` 要求正式采集/算法引用。输入图片相同不等于一次采集事实可被多个 run 冒用。

**备选及取舍**：用文件路径或预制 URI 直接当媒体、或随机生成全部结果，无法证明正式调用及存储，弃用。具体目录名、文件名和随机值域由007版本化测试清单在实施时固定，并在证据中记录；它们不是生产参数。

## R3：预算兼容性核算

**决定**：为007建立新版本 `purpose=Test` 的业务预算和模拟配置，复用001 schema、配置加载及快照，不原地改写已用的 `s01-budget-virtual-plc` v1.0.0。保持每项等待有限，正常采集窗口必须大于5秒加媒体/调度开销，Height和FDecode算法窗口必须大于10秒加保存意图/IPC/排队开销；检测阶段仍按003从StageStartedAt共享按冻结RecipeExecutionBudget确定的，不重置，不暗改重试次数。固定一个可被正式加载的最小完整测试样本，计划生成后计算真实调用数再锁定。

**已核对现值**：`specs/001-station01-public-preparation/examples/budgets.virtual-plc.json` v1.0.0 的 `capture3d=1500ms`、`heightAlgorithm=1000ms`、`captureF=1000ms`、`fDecode=700ms`，均小于007的正常延迟，直接复用会确定性超时。001模拟配置还把各阶段延迟设为150–600ms，必须为007新版本重设；时钟采用 `RealElapsed` 证明实际3–5/10秒，不能用受控时钟加速样本充当证据。

**检测计算**：本阶段选每个检测采集步骤对应一次独立算法请求，避免把多项计划采集折叠为一次无法逐步追溯的计算。设一次检测内实际顺序采集数为 C、独立算法调用数为 A，正常配置预期 A=C；实际PLC握手、媒体保存、队列、存储及调度为 T，重试和退避为 R，则正常通过须满足 `5C+10A+T+R < 按冻结RecipeExecutionBudget确定的` 的保守上界，并在运行证据中测量实际值。公共3D/F若各采集一次且各算法调用一次，仅模拟延迟为 `2×(3–5)+2×10=26–30秒`，另加运动/握手/保存；其各自已有业务窗口必须同步调整。算法超时至多3次总尝试、退避2+5秒；临时通信至多4次、退避1+2+4秒；全部受当前阶段同一按冻结RecipeExecutionBudget确定的约束。

**候选完整样本**：现有 `recipe-catalog-review.json` 的 `R-S1-A-CAP` v`0.4.0-review` 标记 `simulationOnly`，合法F测试码为 `RC:R-S1-A-CAP:0.4.0-review`；若当前正式启动上下文选择仅P01占位，计划预期一阶段AB双相机、2个检测采集及2个算法请求，仅模拟延迟上界30秒。必须以运行时实际 `RecipeRunPlan` 及算法调用数校核。现有默认BASE加P01–P15占位预计四个计划阶段合计120个检测采集，每阶段约30个；单阶段仅采集的3–5秒上界已达150秒，逐采集调用10秒算法则更久。整个计划的120个采集仅模拟延迟就需360–600秒；每采集各有一次算法请求时需1560–1800秒。不能以默认15槽样本宣称兼容，亦不能跳过当前选定样本必检步骤。若CAP/P01运行时实际调用数或其他耗时仍使期限不足，先同步受影响003合同及007规格/计划/任务，明确版本化测试阶段预算或合法计划方案，再实现；不得截短延迟、跳步骤或默认Pending来制造正常通过。

**备选及取舍**：直接把阶段按冻结RecipeExecutionBudget确定的无限提高、每次重试重置、只返回OK或大规模并发化，都改变003明确约束并增加风险。先选合法有限样本并实测；确需改预算时走共享文档同步。

## R4：最小启动和“模拟上料”

**决定**：开发环境可复用 `dotnet run --project VirtualPlc/VirtualPlc.csproj`、`dotnet run --project backend/src/Gaode.Host/Gaode.Host.csproj`、构建后 `dotnet run --project desktop/Gaode.Station01.Desktop.csproj`。桌面先用 `npm run build --prefix frontend` 构建静态资源；WebView2从本地资源加载，无单独前端Web服务。Host配置绝对测试根、版本引用、`VirtualPlcIntegration` 和虚拟PLC；桌面显式设置与Host一致的 `GAODE_API_BASE_URL`、`GAODE_SIGNALR_URL`、`GAODE_MODE=Test`。007实现后提供独立worker及PowerShell编排的确定命令；当前仓库没有可执行worker或模拟上料联调入口，不能把拟定命令写作已运行。

“模拟上料”只准备测试托盘身份、合法占位/配置和VirtualPlc空闲状态，再由前端或辅助脚本调用同一授权Host启动API，PLC按其既有 `PC_Start_Cmd` 流程内部夹紧。VirtualPlc没有上料REST接口；脚本不得直写Modbus完成位、Host状态或SQLite。脚本启动样本另记channel，不能代替实际前端启动样本。自动模拟取盘观察同一runId的已提交WholeTrayCompletion与ObservedUnlocked，使用受控测试身份/稳定requestId调用确认API；仅启动监视功能不意味着启动时立即完成。

**备选及取舍**：发布包 `artifacts/windows-framework-publish-20260922/start-local.ps1` 只启VirtualPlc/Host；它不足以覆盖算法worker、桌面和取盘确认。无需新增VirtualPlc上料API或第二套业务启动入口。

## R5：003/006共享合同和当前实现的差异

**决定及当前状态**：003/006受影响的共享spec、contracts、plan、tasks现已最小同步。003已统一唯一正式路由`/manual-removal-confirmations`、请求体`requestId/expectedRevision/reason`，其余同run整盘/解锁引用由Host从已提交事实取得；来源矩阵要求007自动模拟确认记录Test/Simulated ManualActor和受控身份。T013已将VirtualPlcIntegration的Host确认来源改为Test，辅助API样本已核验；006实际页面联调仍待T015。

006 `frontend/src/runtime.js` 的现有按钮调用 `station01.start({})`，不满足Host所需 requestId、contextJson和三项配置引用；页面请求也未带Bearer token，当前Host将返回401。006需负责前端身份/请求体、API与状态绑定、宿主凭据边界及跨源连通性，保持原型只读。Host `/status` 当前在VirtualPlcIntegration模式仍报告相机/算法NotIntegrated，需以实际适配器事实投影；前端不得补假状态。006若无确认控件，自动模拟确认由受控联调客户端执行，报告显示它未由前端完成。

**其他候选**：让007脚本替前端发起必跑正常样本、把Test actor写成AuthenticatedHuman、或把页面演示数据当状态，均与007完成口径冲突。

## 设计门禁

上述事项没有悬而未决的产品决定；路径/字段统一、合法测试样本的实际计划数和预算余量属于实现前可核对的有界设计门禁。门禁未关闭时可继续独立的合同与适配设计，但不得宣布完整软件闭环已通过。非阻塞优化、全配方生命周期、生产鉴权与长稳压测留待项目各主流程跑通后。
