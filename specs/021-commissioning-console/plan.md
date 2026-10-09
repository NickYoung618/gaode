# 技术方案：正式桌面与页面的受控设备联调入口

## 2026-10-09 当前增量：按新读Ready=1确认复位（SC-021-PLC-R5）

采用spec及start-and-completion最新R5，替代本文历史Ready下降/上升判据。先同步spec/contracts/plan/tasks，再改实现。

1. LatestProtocolPlcDevice.SiteOperations保留请求前置读回、写请求后时间边界和WaitGroupAsync同代次新鲜读取，移除sawNotReady作为完成门槛；是否见过0仅作低频诊断事实。
2. 发令后新读Ready=1立即清SystemResetCmd并记录写应答，然后执行原安全/当前Ready/XYZ检查和旧请求清理。ResetCompleted只在适配器核验通过后记录；后续完整初始状态及SQLite持久收尾保持现有真实路径。
3. Reset-CommissioningConsole仅修正文案，区分PLC完成反馈已收到、复位请求已清但后续检查未通过；不加Modbus写点或重试逻辑。同步configuration/commissioning/site-operations-confirmed-20261008.json的来源说明，记录R5替代旧下降沿规则；配置字段及机械参数不变。
4. 使用独立loopback PLC：请求前Ready=1、收到请求时立即变0以验证旧缓存不能完成；收到请求时已完成并保持1以验证不必看到0；另验证持续0超时、完成后XYZ不通过仍已清请求且不启动。原恢复/SQLite/资源/保存失败回归继续执行。
5. 本轮只做离线验证，不连接真实设备、不制作或应用部署包；待交付记录说明旧高请求仍不自动清除。PLC源程序与实际动作完成后置1由现场实现并联调。

**功能标识**：021-commissioning-console  
**日期**：2026-10-08  
**规格**：[spec.md](spec.md)  
**宪章版本**：9.0.0  
**状态**：软件实施及部分真实桌面离线验证已完成，部署包/独立安装和原联调配方录入有实证；完整故事验收尚未全部完成，现场完整流程未验证。最新证据见validation.md。  
**范围**：现有桌面及三页原型的身份、配方、启动、状态/媒体、人工取盘和终态绑定，以及主流程必需的最小后端配套。

实际Git分支保持020-real-device-commissioning，feature.json指向本目录。setup-plan返回的021逻辑标签不是切换分支的证据。保留工作区、标签及020历史记录，不初始化或升级Spec Kit。

## 方案摘要

在现WPF/WebView2承载RealDeviceCommissioning用途。维护人员预配置单身份，后台核定Operator或ProcessEngineer权限；页面输入和角色按钮不能授权。沿既有配方弹窗及正式Start链运行，显示实际阶段、媒体和人工取盘后的持久终态。

源码发现两个直接影响主流程的缺口：启动回执丢失后缺按原requestId查询的入口；正常Final保存后未释放CommandRegistry占用，会阻止下一轮。设计主体隔离的只读请求查询及Final保存后的普通完成释放，不重建调度或故障恢复。

| P13阶段边界 | 当前方案 |
| --- | --- |
| 起点与终点 | 受控身份及准备资料，从既有登录进入；配方保存可选，启动受理/拒绝可辨，人工确认后重读Final或显示具体阻断 |
| 必须参与的组件与接口 | 正式桌面、login/a/data-view实际页面、身份API、配方服务、Start/查询/通知、人工确认、SQLite/媒体；设备沿020端口 |
| 必要验证 | 身份、页面配方读存与编辑隔离、结果未知只查询、新用途零相关运动、离线正常Final与下一轮、媒体关联 |
| 完成证据 | 实际操作、主体/请求/Run/Tray、版本和实际Move/Capture参数、SQLite重读、媒体、写入审计及持久日志；注明环境 |
| 延期项 | 账号体系、报表平台、无关UI、全异常矩阵、真实算法/灯、发布；T055/T056保持现场阻断 |

## 技术上下文（Technical Context）

| 事项 | 当前选用方案 | 决策来源与状态 | 尚缺证据/OPEN |
| --- | --- | --- | --- |
| 后端运行时 | 现.NET 10/ASP.NET Core单Host | 已有源码，不升级 | 新路径待实施验证 |
| 算法运行 | 020现业务端口及虚拟实现 | 页面只读来源，不设置算法安全输出 | 用户现场安全值待提供 |
| 数据 | 现SQLite配方/Commands/Run/StageEvents及媒体 | 沿现Store/TraceQuery，无计划迁移 | Final及关联须实际落库验证 |
| 前端与宿主 | 现JS构建，WPF/WebView2 1.0.2903.40 | 021批准；实际入口src/pages | Windows会话及Runtime待验 |
| 身份与连接 | 单预配身份，后台独立Commissioning认证，固定https://appassets.local来源 | 用户确认及IH设计 | 真实秘密值不入仓库，测试用离线专用凭据 |
| 设备与采集 | 020正式链；本轮验证只用显式离线夹具 | 不改协议、安全或工艺 | DEP-021-01/02限制现场链 |
| 性能与容量 | 复用现请求/业务预算及单控制占用 | 无新增性能承诺 | 不编造生产时限 |
| 软件验证 | Node实际测试、离线Host、真实WebView2探针 | quickstart V01–V06 | 全部待执行，不把计划当通过 |

决策及替代方案见[research.md](research.md)。无未裁决技术选型；现场输入作为局部依赖保留。

## 宪章检查（Constitution Check）

设计前按规格和源码检查，设计后按本计划/契约复核。“符合”表示设计符合，不表示实现通过。

| 原则 | 本功能检查点 | 设计前 | 设计后 | 证据/受限范围与可继续部分 |
| --- | --- | --- | --- | --- |
| P01 | 最新决定及追溯 | 符合 | 符合 | spec来源表、七格确认、共享变更登记 |
| P02 | 后端设备/数据边界 | 符合 | 符合 | IH/RM，页面宿主只经API |
| P03 | 配方及工艺来源 | 符合 | 符合 | RM及020合同，人工配方、F绑定、离线标识 |
| P04 | 安全及有限状态 | 待补充，仅限制现场运动 | 待补充，仅限制现场运动 | DEP-021-01；V04验证拒绝，身份不代表运动许可 |
| P05 | 分层及单控制权 | 符合 | 符合 | SC；端口/存储隔离，共同完成服务释放owner |
| P06 | 资源和媒体所有权 | 符合 | 符合 | RM当前Run媒体，切Run释放对象URL |
| P07 | 身份、去重、未知 | 符合 | 符合 | SC/IH，原主体原请求查询，404仍未知 |
| P08 | 保存、快照及恢复 | 符合 | 符合 | data-model/SC，Final先保存后释放，A/B隔离 |
| P09 | 正式逻辑和诊断 | 符合 | 符合 | quickstart真实API/SQLite/持久日志，夹具来源明确 |
| P10 | OPEN局部限制 | 待补充，仅限制现场证据 | 待补充，仅限制现场证据 | DEP-021-01/02；独立软件继续，T055/T056不关闭 |
| P11 | 配置与能力区分 | 符合 | 符合 | IH版本化模板、RM字段归属，不创建工艺引擎 |
| P12 | 原型保护及API边界 | 符合 | 符合 | spec归档摘要、RM、精确授权差异 |
| P13 | 起终点与完成证据 | 符合 | 符合 | 本边界表/V01–V06；执行证据待取得 |

无复杂度豁免；已发现实现缺口进入后续任务。

## 结构与职责（Project Structure）

| 模块/端口 | 状态或资源所有者 | 依赖方向 | 外部对接边界 |
| --- | --- | --- | --- |
| Station01Authorization、Program及拟增CommissioningAuthenticationHandler/IdentityEndpoints | 后台身份/权限 | Host配置→认证→业务 | 无管理员或Test回退 |
| CommandRegistry、RunEndpoints、QueryEndpoints及共同完成服务 | 请求关联及单owner | 业务→Application端口→存储 | 新查询只读，Final后释放，保持故障恢复 |
| ITraceQuery/TraceQuery | 持久命令/Run查询 | Application端口←Infrastructure | 使用现Commands字段，同步实现/测试替身 |
| desktop/HostConfiguration.cs、HostRuntime.cs、DesktopRuntimeLog.cs | 连接、选定身份内存及日志 | 宿主→后台/受控页面 | 不开放联调调试端口，凭据脱敏 |
| frontend/src/pages三页及runtime.js/recipe-authoring.js/public-tray-flow.js | 展示、用户意图、待核查关联 | 页面→API/通知 | 不造完成、不使用Test随机媒体映射 |
| frontend/scripts/build.mjs、verify-prototype.ps1及授权差异/测试入口 | 构建和原型保护 | src/pages→产物及别名 | 不修改原件，不放宽校验 |
| 拟建Gaode.CommissioningUiFixture、Gaode.Desktop.CommissioningProbe | 隔离数据/离线Host及真实WebView2 | 测试→现公开入口 | STA持有实例ExecuteScriptAsync，不新增产品控制桥 |

实际触及路径由tasks细化；先按[共享变更登记](contracts/shared-change-register.md)同步现006/012有效合同再改共享代码。不补造缺失旧根部spec/plan/tasks。

## 数据、契约与状态

- [data-model.md](data-model.md)：预配身份、版本化模板、PendingOperationReference、运行/Final事实。
- [identity-host.md](contracts/identity-host.md)：IH-01–05，身份GET、宿主配置及秘密内存/来源边界。
- [start-and-completion.md](contracts/start-and-completion.md)：SC-01–04，现Start正文、原请求查询、Final释放及只读准入。
- [recipe-media-ui.md](contracts/recipe-media-ui.md)：RM-01–04，P01–P10归属、七格、数据页及原型。

配置expectedRole/Subject只作一致性检查，后台才是权限来源。启动模板只含用途、业务身份及版本引用，无任意坐标/安全值。POST前冻结准备资料并记录原requestId，202仅代表受理；技术、质量、物理处置和Final分别呈现。

GET start-requests按认证主体及原requestId查登记/SQLite Commands，404不能证明未执行。start-admission只观察，不释放owner；Available仅指软件占用，不代表PLC安全。普通释放先验证同Run持久Final、人工事件和终态，再锁内比较owner；旧Run重放不得释放下一Run，不改故障恢复。

## 配方共用逻辑与动作隔离（适用时）

FR-005–008/SC-002–003复用012读存校验及020执行链。工艺工程师编辑、操作员运行，不暗带管理员。软件校验保存后可选择，不增加逐版本批准。

公共配置启动冻结，产品配方经实际F唯一绑定后冻结；选择意图与执行版本分别显示。A在途编辑保存B不改A实际坐标/单张曝光，后续合法绑定才用B。显示PlcRecipeId不是现场REAL型号，后者沿现PoseProgram映射。公共Z及公共位置/示教限制不扩展。

### 配置与策略扩展设计（P11）

身份及模板版本化，只引用已注册能力。参数沿共同模型，不新增型号分支、策略引擎或整套导入。配置缺项定位字段/引用，不补默认值、不污染快照。算法未就绪与安全输入缺失分别依020处理；页面不计算动作或安全结果。

## 并发、资源与异常出口

| 路径 | 所有者/容量来源 | 等待期限来源 | 失败终态及后续步骤 | 资源释放/保留 |
| --- | --- | --- | --- | --- |
| 启动/人工确认 | 后台单owner，页面显式提交 | 现HTTP/业务预算 | 响应丢失为未知，只查原关联 | 未查明不重发 |
| 状态恢复 | 现SignalR LongPolling/只读API | 现连接/查询预算 | 断线过期如实显示，重连不赋许可 | 仅恢复观察 |
| 媒体 | 当前Run已提交资源及对象URL | 现读取机制 | 未采集/失败不回填旧图 | 切Run释放旧URL，持久事实保留 |
| 设备/采集/算法/保存 | 020后端调度 | 020受控预算 | 清零/未知/安全由后台决定 | UI不释放轴/owner，心跳停止不依赖UI |

不新增批次缓存、无限轮询或硬编码运动等待；七格显示不创建E采集步骤或改变参与条件。

## 保存与恢复

配方正文/版本/审计用现Store，重开实际读。PendingOperationReference只存非秘密关联及摘要，不存凭据或整套启动资料，不作为授权。换身份不能把旧请求归新主体；未知只能核对原请求/Run。

人工确认事务先保存事件、Final矩阵和Run终态，共同业务层再释放同Run普通owner。IWholeTrayCompletionStore.GetByRunAsync返回ReadyForRemoval，不能当Final证明；结合StageEvents/Run持久查询。保存失败/未知、取消或一般Blocked不自动释放，不改变故障恢复。无计划数据库迁移，发现必要结构变更须先更新契约/任务并受控维护。

## 软件验证与证据计划

见[quickstart.md](quickstart.md)，全部待实施执行。

| 需求/原则 | 正常/失败场景 | 方法与测试输入来源 | 预期可观察结果 | 证据产物 |
| --- | --- | --- | --- | --- |
| FR-001–004/015；SC-001/006；P05/P12 | V01身份、缺失错配/越权 | 真实Windows桌面→后台，离线身份 | 后台核权，零越权，无秘密泄漏 | 021 evidence身份/桌面/原型 |
| FR-005–008；SC-002/003；P03/P08/P11 | V02人工读存、字段错误/冲突、编辑隔离 | 真页面、同一真实目录/SQLite，正式执行器离线A/B | 参数归属完整，A旧值B新值 | 字段对账、读存、实际Move/Capture |
| FR-009/011/014；SC-004；P07 | V03受理后回执丢失/重开 | Host响应丢失夹具及持久Commands | 原主体/请求关联，404仍未知，无自动重复POST | DOM、请求及重开查询 |
| FR-009/010/014/016；SC-004；P04/P10 | V04新用途缺输入/PLC语义未明 | 正式组合根/页面，仅loopback | 零相关运动，可定位持久原因；心跳单列 | Run/写入审计/日志 |
| FR-010/011/013/014/016；SC-005；P07/P08/P13 | V05人工Final/下一轮/旧确认重放 | 声明Legacy Test离线布局，正式Host/SQLite | 保存先于释放，下一轮可启，旧Run不释放新owner；定向保持故障链 | 操作、Final重读及两轮记录 |
| FR-012/015/016；SC-005/006；P06/P12 | V06七格/多次采集/数据页 | 专属Commissioning离线Run，经实际采集/提交、SQLite/媒体、正式受认证GET及新用途页面；不复用V05 Test Run | C/D/A/B/E/3D/F，无旧帧/mock/本地质量判定 | 关联及三页对齐 |

新用途参数到正式执行器组件验证、旧Test正常整链、新用途失败整链分别记范围，不能拼成正常新用途真机通过。020未受影响12/55/58项证据按原用途版本复用；只对共享释放、查询消费者、实际配方编辑定向回归，不跑全异常组合。修正现前端test入口过滤，零匹配不算通过。缺Windows会话/Runtime明确Blocked，浏览器不能冒充桌面证据。

证据入021新目录，记录源码摘要、命令/非零计数、操作/存储及组件来源，不覆盖020证据；历史未验证和T055/T056保持。

2026-10-08 C1修正：后台必要身份/权限验证及对应接口实现是软件工作的前置，完整V01桌面验收独立，不作为配方/查询/组件验证的全局门。缺Runtime/交互会话时，只阻断实际依赖的桌面子项；已具备条件的源码、构建、API/组件和真实页面验证继续。按子项记录Passed/Failed/Blocked，整体任务/故事无完整证据保持未完成，浏览器证据不能替代桌面通过。T029/T030按对应组件就绪执行，最终汇总允许记录阻断，不代表验收通过。

2026-10-08 I1修正：V05仅旧Test正常Final/两轮；V06独立以Commissioning Run验证七格。离线夹具在应用层正式准备/持久路径建立专属Run，实际调用隔离采集端口、共同媒体保存/提交及正式查询，经新用途页面展示；保留实际Run/Capture/Media/提交身份与夹具来源。不得直接改库造成功、把旧Run换用途或绕过设备/Start安全门，不新增生产采集旁路。其为媒体组件到页面的离线证据，不声明正常整机或现场拍照通过；旧Test Final只作相应用途数据页补充证据。

## OPEN、外部依赖与决策记录

| 依赖 | 当前状态与范围 | 补充时机及可继续工作 |
| --- | --- | --- |
| DEP-021-01 | PLC-Q3/Q4及用户安全值未齐，阻断依赖真机运动 | 对应动作前补；独立软件/零派发继续 |
| DEP-021-02 | T055/T056现场未验 | 后续硬件授权；软件不关闭现场门 |
| DEP-021-03 | 旧资产缺失不补造，原件/现合同/源码用于设计 | 先同步合同；若必要新增控件，再具体澄清 |
| DEP-021-04 | IH完成技术方案，真实秘密不在计划提供 | tasks实现绑定，离线凭据验证，维护人员后配置 |

七格映射已确认，不再列未决。无新增业务/协议裁决。下一步tasks包含共享文档同步及V01–V06，再analyze后implement。本轮不生成tasks、不改产品、不构建测试、不连硬件、不打包部署或提交推送。

## 客户确认原型检查（P12）

只读原件E:/dzk/gaode/原型.zip，SHA-256：3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0；HTML为a.html、data-view.html、login.html。依据021及现006/012/016授权，保持布局、文字、控件和导航。构建实际读取src/pages，prototype.html是别名；旧副本/未使用TS helper不是验收对象。

用户确认环面1=C、环面2=D、孔底1=A、孔底2=B、来料2D=E、来料3D=3D、读码参数=F。同角色多张按当前Run提交身份呈现，未参与不填旧图。data-view绑定已有Run/结果/媒体，未具备历史筛选/复检/报表能力明确受限，不扩平台。

沿现精确授权差异校验，为021逐项登记绑定差异及需求，核源文件、产物、别名和资源摘要；不关闭保护。设计审阅不替代逐页运行证据。


## 用户增量：可配置联调配方与光源选择（2026-10-08）

按[CC合同](contracts/configurable-commissioning.md)执行FR-017/018、SC-007/008。先T033核定共同正文/冻结/请求的版本化字段及历史读取映射、虚拟算法多配方输入集合与公共采集作用范围，更新共享合同后实施T034/T035；T036定向离线验证，T037同步证据。原T012–T015新增配方与参数链工作继续；其原验收不包含新增模式通过结论。T018启动接线和T025来源消费必须结合本增量，不锁死Host唯一翻面配方。

沿现RecipeDefinition/Planner/Executor/Store和算法接口，扩展受控输入的配方匹配，不另建执行引擎。产品配方与前置公共3D/F分别冻结光源模式；Simulated在采集适配层跳过光源必填、控制和等待，保留真实相机设置及采集；Real只接受真实装配。更新CameraCaptureAdapter、CaptureEvidenceGate、摘要/来源及消费者，不能只在页面禁用亮度。

真实算法与真实光源硬件适配仍延期；本轮新增真实光源选择与缺装配阻断不是硬件接入。原型仅授权复选框及关联状态，归档只读，精确差异核验照常。旧证据保留，T055/T056不变。

## T033共享字段定版及交付授权（2026-10-08）

采用版本化可选扩展lightExecution={schemaVersion:"light-execution/1",mode:"Simulated"|"Real"}，置于共同RecipeDefinition、RecipeRunPlan、PublicConfiguration、CaptureRequest及CorrelatedCaptureFact；写入设置摘要，JSON null字段不输出。未含此扩展的历史正文仍按原schema及原灯控制/证据语义读取，不静默迁移。新建配方显式Simulated；编辑复选框保存显式选择，未编辑历史记录保持原字段。光源相关DetectionCaptureSettings的lightChannel/brightnessPercent/settleMs允许null，仅显式Simulated免校验；曝光/增益/ROI不变。公共CaptureParameters.lightLevel同理。此为现正文中的light-execution/1扩展，不强制重写历史recipe-definition/5或SQLite表。

CommissioningConfiguration保留已有主输入记录，增加recipeInputs[]，每项含id/version/source、expectedRecipe、slots、fLocation、mappingSourceReference、rawCodes、results及可选entityCodes；共享能力/公共配置/预算仍由外层版本约束。启动FreezeRun增加可选ExpectedRecipeRef选择意图参数，从主记录及集合唯一匹配RecipeId/Version及scenario，不填默认值；无选择仅允许单一记录的旧声明调用。每Run保存选择后的独立输入并在F绑定后再核DefinitionDigest/Model/FCode，不使用全局当前配方。EDecode采用EntityCode/decoded-code/1、按ReadECode实际步骤作用域的显式entityCodes记录，缺项局部阻断，不自动生成码。

用户已明确解除本轮打包和本机部署限制；原文“不打包/不部署”为历史授权边界。本轮可构建并制作包，检查本机安装/启动条件，保留已有安装及配置；无现场安全输入不得启动依赖真实运动。此授权不含Git提交/推送，不覆盖现场安全值确认与020 T055/T056证据门。

## 用户确认增量：配方中的虚拟F定位（2026-10-08）

用户确认示教坐标人工填配方，本次单品翻面样件的虚拟质量结果为OK；虚拟算法必须实际消费本Run媒体后记录调用成功，不绕过采集/保存或设备动作。用户将提供F读码XY，并要求加到虚拟算法联调用的配方设置。新增FR-019/SC-009：共同配方可选commissioningFPosition={schemaVersion:"commissioning-f-position/1",x,y}，页面在既有配方弹窗基础信息增加“虚拟算法 F读码X (mm)”及Y，由人员手填，不补默认坐标。后端校验有限值，启动再按公共运动配置核单位/坐标系/行程；所选配方版本与目录一致后从实际保存正文读取，构成带配方版本来源的FLocation，冻结给本Run虚拟3D的首次定位结果，实际F读码后仍再次核绑定。真实算法不消费该字段。旧记录保留原声明的受控输入策略；新建联调配方必须显式填写F位置才能通过相关运动准入，不能偷偷沿用旧示例F位置。

准确字段及端口：RecipeDefinition.CommissioningFPosition随正文/摘要保存；ICommissioningRunInputs.FreezeRun(..., selection=null, fLocation=null)增加可选冻结定位输入。StartPublicPreparation从本次expectedRecipeRef匹配的真实目录取得该位置，校验引用后转换FLocation，不从前端POST直接接收运动值。运行开始后修改配方不改变已冻结公共/虚拟输入。已有公共F固定位置及真实3D定位职责不改；新增位置仅适用本联调虚拟算法。FR-009/原“不在页面设置算法坐标”对此用户明确授权的F XY作唯一例外，其余安全值仍不允许默认补齐。

普通完成释放由IWholeTrayCompletionStore.ReconcileFinalAsync(runId,trayId,ct)共同路径核同盘Final事件、人工事件、FinalSourceMatrix及Run终态后执行，Confirm提交/重放和API确认重放均调用；GET不调用。CommandRegistry仅按同owner释放且不清故障owner。ITraceQuery.GetStartReceiptAsync(subject,requestId,ct)只读既有Commands。


## 2026-10-08 部署与现有成功配方录入授权

用户目标：完成部署包，再录入本机虚拟上位机成功联调配方；缺失项采用不参与当前流程的明确占位/跳过规则，不让必需字段缺失。来源以当前启动入口选择的Gaode-PlcCommissioning-1.1.6-win-x64/config/recipe.local.json为准，保留SHA256及逐字段映射，原件只读。正式共同Validator/SqliteRecipeStore保存并由新实例重读及BuildExecutable核验，不直接写SQL或给旧Test改用途。

原文件为单品两面AB→翻面/放回/3D复查→CD、全部OK；F65/50、检测Z5、取件/放回10/10、型号666有来源。外部光源显式Simulated，E扫码关闭，额外旋转不启用。缺相机曝光/增益采用明确联调初始设置并标待调优，不能声称历史真实拍照参数；曝光不改变运动目标。分拣夹爪缺值，可按用户授权给1作为未启用占位，当前固定OK路线不派发分拣，分拣安全配置保持缺失，正式适配器必须在派发前拒绝不具备配置的分拣，不借占位放行NG/Pending。FlipPick/FlipPutBack以及3D/F/Unload仅XY的Z正文占位为0（现场适配器不派发该Z）；检测Z严格使用来源5。不可用占位伪造PLC安全语义、机械行程、完成反馈或首次/恢复准入。

部署包含正式Host、Desktop、CameraWorker、前端、维护准备工具、SQLite配方、来源映射、版本/工作区源码摘要、验证/已知缺口及回退说明；新版本独立目录，启动入口默认仅检查，不连接设备。现场配置未知时包保持待配置/禁止运动，不冒称现场通过。用户F来源已找到，不能继续泛称F位置未提供；后续仍可通过页面编辑，实际安全以用户现场核定为准。

新增维护CLI --inspect-recipes <recipeRoot> <output>：只读正式配方库，经共同准入/规划并输出版本、正文、实际计划（不执行设备）；--seed-authoring保持原保存规则和拒绝覆盖。此接口仅部署工具，不新增产品前端或业务API。

确定缺陷：真实PLC阶段适配器仅允许TargetPurpose=Production，错误拒绝已授权RealDeviceCommissioning卸料阶段。修正为与设备实际ConfigurationPurpose严格匹配，保留身份、采样、位置、安全、阶段与证据门，不将Test放行到Real。


2026-10-08 两轮验证发现确定软件缺陷：离线SimulatedCapture的F防重拍计数误跨Run，导致普通第二轮在F采集失败。T043定向修正为按Run限制，保持同Run拒绝，再经相同Host完整两轮/人工确认/SQLite重读验证；不变更真实设备接口，现场未决保持。


T044：第二轮继续验证发现ThreeStageWorkflowExecutor阶段字面键在SQLite全局幂等索引冲突，属于正常连续运行缺陷。在事件生产端加入稳定Run/Tray命名空间，不改持久端口或唯一约束；定向两轮验证同时核原运行Final仍可读。旧库和在途事实保留，不以版本切换自动重发。


T045配置准备：补本机两角色受控身份、非秘密桌面profile和业务start模板，沿现IH配置和环境提供器，无认证/API改动。凭据仅安装data/private-identity当前用户ACL范围，不分发、不打印；脚本只准备当前会话。运动配置与首次/恢复准入仍等待现场答复。

### 2026-10-08 现场范围确认增量

单位及Z轴范围已获用户确认：X/Y各0–100毫米，检测/扫码/翻面抓取Z均0–10毫米；final-2配方重读29个XYZ数值分量均在范围内，记录见validation.md及field-limits-user-r2.json。无需再询问这两项。PLC首次/恢复准入、启动/复位时序及断线/软停语义仍阻断相关真实运动配置及放行，不影响其余独立软件工作；不据此关闭020 T055/T056。

### 2026-10-08 PLC“就绪”含义确认增量

用户针对MB6015就绪=1答“是”，确认它保证复位完成、各轴停稳、允许PC开始流程。记录见evidence/deployment-final-2-20261008/plc-ready-user-r1.json。本答复关闭PLC-Q4中这一项含义澄清，未关闭2007/2009请求置位/清零时序及断线/2008软停行为。不得继续把MB6015这三项含义列为未确认；也不得把此次确认扩大为全部现场恢复语义或真机验收通过。准入仍须本连接新鲜可靠反馈、既有安全检查、相关旧反馈清零和有效配方/配置；未知动作不自动重发。

源码核查：backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Semantics.cs中现场布局仍显式输出SafetyUnconfirmed/ManualAreaUnconfirmed。此次只记录确认，不改产品代码或以配置绕过门禁；后续实际映射须先同步对应规格、契约、计划和任务并定向验证。原冻结包不覆盖，020 T055/T056保持现场阻断，requirements只读。

### 2026-10-08 复位及启动边沿答复增量

用户确认：PLC只读PC系统复位请求MB2009的上升沿；收到请求后将MB6015从1置0，复位完成后由0置1，PC观察本次0→1后清MB2009。等待期间须确实观察到本次0，再观察本次1，不能只等任意新读1；超时、断线或反馈过期不得视为完成或自动重发。MB2007启动也只需上升沿，不要求持续保持；清零时点用户要求先解释后再决定，尚未确认。用户说“设备就停住就行”“安全位置位于0”；停住是否同时适用软停/断线及其与原表自动回位的裁决待明确；安全位置按协议所述X/Y/检测Z零位理解，不推导自动回零命令或编造容差。详细答复及范围见evidence/deployment-final-2-20261008/plc-sequence-user-r1.json。

源码核查发现LatestProtocolPlcDevice.ResetAsync当前复位请求置1后立即清0，未等待Ready本次0→1；AdvanceStart只写PcSystemReady并等待PlcReady，未派发MB2007。此为待实现缺口，不能因旧启动方法存在称新握手已完成。本轮仅记录答复/缺口，未改产品代码或重制包；实际共享变更实施前须完善对应契约/任务。原包、历史记录及requirements保持，020 T055/T056仍现场阻断。

## 2026-10-08 已确认启动/复位/停住规则（SC-021-PLC-R2）

用户确认采用所建议MB2007清零时点，并确认MB2008软停及心跳断线均停在当前位置、不自动回位；此规则取代旧说明的软停自动回位。MB2009复位按PC低态确认→置1→观察本连接本次MB6015先0再1→检查新鲜XYZ零位→清MB2009；不得立即脉冲清零、只读旧1或失败重发。MB2007仅在现场布局启动时确认原值0后置1，第一组实际下发运动完成、坐标验证及双方清零后清0；纯同坐标沿用不触发清零。未知/失败不得发下一轮启动；不在失败清理时伪造首次动作完成。现场报警及独立安全点均沿既有只读检查，未知报警非零阻断；安全门/光栅恢复不得自动重发未知动作。

新增通信配置plc-site-operations/1，带非空确认来源，经plc-mechanics/1的siteOperations读取，只适用于现场已确认布局。无此配置继续Unconfirmed，不凭单一布尔标志放行；确认配置采用本次用户约定的Ready语义、XYZ零位、停住规则及首次动作后清启动。零位使用已配置PositionTolerance核对，不填任意容差，不发自动回零运动。完整恢复持料/夹爪核定保持人工门，ReadInitialState不能因上述规则自动声明全部恢复成功。

新增SignalId.PcStartCmd仅绑定现场PC.xls MB2007（BoolByte，PC写/清），不添加旧Test地址、不改变前端API。受影响消费者：协议定义/现场映射、PLC机械配置、Sample安全解释、ResetAsync、AdvanceStart、轴完成清零；旧Test流程和清零/同坐标/未知不重发不变。补丁按final-2清单验证基包，生成独立版本目录并带SHA256、源码增量及验证记录；不覆盖旧包/安装，不连接硬件、不提交推送，T055/T056不关闭。

T047定向回归发现ObserveAxisClosures对所有现场布局无条件清除已闭环资格，导致相同坐标重新派发。SC-021-PLC-R2同步：已有siteOperations确认时，按自动/就绪、报警、独立安全点和光栅检查决定保留资格，不再仅因现场布局即清除；无确认配置仍清除并阻断。仍仅使用本连接真实完成及清零记录，启动前零位不授予同坐标资格。失败证据plc-sequence-r2/r3保留；回归修正后再记录。

2026-10-08 T050：按已确认来源在final-3 data/config/runtime-r1装配公共/预算/机械/现场地址/Host配置，并用已安装程序集注册检查。公共schema采用021既有lightExecution合同的定向覆盖，原001及不可变包保留；无Host/worker启动，不产生现场通过结论。

2026-10-08 T051：用户实际启动暴露桌面先于后台监听而一次性身份请求失败。启动脚本须等待本机身份接口返回并匹配当前profile后启动桌面；身份检查不代表PLC/相机准备完成。日志另证Meta路由接管PLC，使用仅192.168.0.10/32、PLC网卡、ActiveStore的可逆直连，不改代理全局设置或其他设备路由；不因网络修正自动重发动作。


2026-10-08 R3修正：先实施T054通信复位前置和定向loopback回归，再T055现有故障区/复位脚本提示及发布补丁。保持pcReady内部业务门为false直到复位核验通过；PC线路就绪仅用于PLC接受复位。T056独立处理Commissioning重启旧Run恢复服务、身份授权、持久核验和新轮关联；依赖现场实物复位结果确认，不以PLC复位或取消软停替代。


用户补充：没有下发过软停也必须能够复位；MB2008已为0时保持0即可，禁止为复位先置1。T054正常闭环分别验证初始软停为0和1，均只产生一次复位上升沿、零启动/轴运动。当前软停触发点核对：启动准备失败、动作/采集/阶段结果未知及显式RequestStop会请求软停；普通连接、正常闭环完成和显式复位不会主动先置软停1。保留未知动作的停止保护，不因本次修正取消。现场报警消失不自动复位或续跑。


## 2026-10-08 全部复位后的旧任务结束与新轮（SC-021-PLC-R4）

用户确认PLC系统复位完成后会全部恢复：零件放回、夹爪松开、翻转机构恢复初始状态。siteOperations新增可选restoresWorkpieceAndMechanisms=true及确认来源，默认false；只有本次已观察Ready 0→1、当前安全和XYZ零位满足、PC请求及相关PLC旧反馈清零时才能形成恢复核验。无此确认不把Ready单点扩大成恢复通过；旧Test语义不改变。

Commissioning正式POST /reset在原Run.Start权限下串行执行维护：阻断新启动，要求旧任务处于Blocked/RecoveryRequired/Restricted/Cancelled且执行退出、相机/算法/媒体资源释放；先持久保存复位意图，再完成PLC复位和新鲜初始状态核验，最后使用原Writer条件版本将旧Run保存为Cancelled并记录CommissioningRecoveryClosed（resetId、用户、真实观察、来源、旧Run身份）。这不生成检测成功或Final完成事实。必要保存未确认时保持占用；已提交后释放对应Motion/Command占用，返回recoveryClosed=true、manualStartRequired=true。无旧任务也支持复位，不人为要求先软停。旧MB2009=1仍拒绝重发未知复位，不把旧Ready=1认作本次复位完成。

GET Run新增commissioningRecovery投影，仅依据已提交取消终态和恢复审计；页面沿既有故障/人工操作区域提供“复位并结束旧任务”，调用同一正式/reset入口。成功后明确旧任务已结束，启动控件只在GET确认恢复证明及Available时可创建新的requestId/Run；不自动启动、不续接旧步骤，不复用旧冻结配置。StartPublicRequest可选commissioningRestartFrom={runId,recoveryWriteId}，后端验证所引用取消及恢复提交事实，并保存在新Run原始启动上下文中；普通启动和旧Test故障restartFrom合同不变。Host重启读取Cancelled持久终态，不再次恢复或自动重发旧Run；未完成恢复仍Held。

用户要求更新现有final-4目录及同名ZIP，不创建新部署包编号。更新前备份原manifest/ZIP摘要及改动文件，保留历史验证与回退；重新发布受影响Host/Prep/Worker等消费程序集，重新冻结源码和清单并校验同名ZIP，不触发真实复位/运动或替换运行中安装。

## 2026-10-09 诊断最小修正

先核附件校验值、SQLite和原始帧，再修改Start-CommissioningConsole.ps1的启动前日志保全、CommissioningRecoveryService的取消/阶段诊断、LatestProtocolPlcDevice.SiteOperations的关键事实日志。恢复门禁和设备写入时序不变，不以追加日志宣称解决旧请求恢复。验证采用隔离目录的真实启动脚本（进程/身份等待替身，无设备访问）、原PLC握手/恢复loopback测试及请求取消/预算到期定向用例。新任务独立编号，历史勾选不变；现场未知请求的释放规则单独待确认。


## 2026-10-09 已确认现场复位保留到位规则（SC-021-PLC-R6）

用户确认系统复位后轴到位反馈允许保持1；需要运动时PLC先反馈运动中再到位；相同坐标时保持1。MB6052放回反馈必须清0，PLC侧已修改，现场修改尚未由本轮验证。复位恢复允许手动模式，自动模式仅用于新任务/运动准入。复位仍须本次新鲜完成反馈、PC请求清除、安全互锁、全部直线轴安全零位、非轴闭环反馈清零及SQLite旧Run持久取消。

已核验本连接系统复位可形成受限轴初始证明：新鲜请求为0、反馈为0或已到位、实际位置在安全零位容差内。仅凭开机/重连坐标或旧缓存不成立。该证明允许首轮从保留到位发令，但本次必须观察发令后的运动中再到位；同坐标在新读坐标/请求/反馈一致时不发运动。轴被触发、位置变化、故障或连接代次失效即撤销。普通动作完成后双方清零机制不变，放回/翻转不套用轴到位例外。旋转保留到位只允许本次复位后的首次请求，仍检查本次运动中/到位及实际角度，普通闭环仍清零。

软件补丁不自动启动设备或清理旧未知请求；现场T055/T056验收继续保留。


## 2026-10-09 相机故障与复位资源检查分离（SC-021-CAMERA-R7）

本次诊断确认3D初始化在SDK Open之前因userIP=127.0.0.1被拒绝，worker退出及管道释放后仍显示Faulted，旧资源谓词只允许Ready/Stopped而阻止PLC复位。保留故障状态和新任务/真实采集七台Ready门禁，恢复门禁改用真实资源证据：无在途操作，Ready空闲或进程/管道退出且参数无需恢复/正常恢复已确认；其他未知故障继续阻断。

CameraWireMessage v2增加可选deviceOpenAttempted事实，缺失视为未知。正式driver在首次SDK Open调用前置true，初始化前失败才可报告false；Host仅在同session/request的初始化错误中采信，并在实际子进程/管道退出后形成无占用证明，不能仅凭Faulted放行。共享PersistentCameraGateway增加只读恢复资源投影；恢复日志记录算法、媒体计数和各相机证据，不改PLC握手。

CameraPro发现回环userIP时，允许从现有ExpectedNicMac唯一匹配Up物理网卡、同网段IPv4及Windows到相机的最佳路由接口，形成明确派生的本机地址。三者必须唯一且一致，SDK正常返回的地址不自动替换；记录原始发现、推导依据及SDK打开使用值，不硬编码IP、不修改现场网卡/相机地址。使用厂商现有CameraInfo.userIP setter填入核定本机地址，再执行原序列号/物理网卡校验与真实SDK Open；任一依据缺失即拒绝。没有伪造Ready或帧。

验证仅离线：已知Open前失败且资源退出可恢复、未知初始化/采集故障不放行、在途采集不放行；回环地址的唯一绑定解析和错网卡/错路由/重复候选拒绝。保留020 T055/T056现场阻断。本轮不自动启动设备、复位或拍照。


## SC-021-UI-R8 复位后新轮状态绑定修正（2026-10-09）

现场证据：旧Run持久Cancelled且恢复证明ClosedAfterVerifiedReset、启动准入Available，运行查询同时返回数字state=24及规范字符串executionState=Cancelled；前端只比较state字符串，误挡显式启动，未发送启动请求。
前端恢复提示和恢复后新轮判断使用既有executionState；旧字符串state仅沿用现有页面的备用读取方式，缺少可靠状态不得放行。仍必须核对恢复写入证明及后端Available，重新制作独立requestId/trayId并携带commissioningRestartFrom，不复用旧动作、不自动启动、不清库。API、PLC信号和页面原型不变。
定向验证使用现场查询同形JSON（数字state及字符串executionState），覆盖成功收尾、新轮准备和缺少证明/准入阻断；检查同一helper中的恢复提示状态读取。原地更新受影响前端产物并核对构建摘要，保留数据库/身份/配方/设备配置。真机完整流程继续待现场验收。


## SC-021-PLC-R9 受控复位前撤销PC残留动作请求（2026-10-09）

用户授权修正PC残留动作清除。旧流程/采集/算法/媒体退出与恢复权限检查保持不变；仍先检查旧MB2009，结果未知请求为1时不清除、不重发，也不清其他请求。旧MB2009为0才进入本次受控复位：撤销PC旋转MB2000、X/Y/检测Z/扫码Z/抓取Z请求MB2001–2005、启动MB2007、翻转/放回命令MB2014、分拣命令MB2016；各类型依现有效XLS映射使用BoolByte/Int16，清除值均为已采用的空闲0。不清目标坐标/配方/PLC反馈/报警/夹爪状态/心跳。

撤销发生在置PC就绪、取消软停以及发送系统复位MB2009之前；读回本次新鲜请求值全0且连接代次有效后才继续。写返回不算读回确认，任一失败/读回非0/断线/取消/超时即阻断，不发MB2009，不自动重试或启动。日志记录清理前、逐项清理、读回值/时间证据、失败阶段及耗时。原系统复位完成、安全零位、残留反馈、SQLite旧任务收尾门禁不变。普通动作清零规则不变，不用本补丁绕过此前MB6040未清问题。

最小回归：残留PC请求在复位沿前均清零并新读确认；PLC确认写入但保留请求时零复位派发；已有未知MB2009时不写清任何请求；复位后真实SQLite旧任务收尾与普通轴清零/同坐标回归。测试只用隔离离线TCP夹具，不连接硬件。发布各受影响程序集副本与PDB按四入口正式发布比对原地更新，现场资料保留，T055/T056仍待现场。


## SC-021-PLC-R10 轴到位状态保留与PC请求撤销（2026-10-09）

用户纠正并授权修改：现场轴反馈0=运动中、1=已到位、2=超时；不能要求普通动作完成后将到位1清0。此规则取代R6/R9及此前所有“现场轴正常闭环双方归零”的表述，仅针对现场五个直线轴及同含义的旋转轴，旧Test协议和翻转/放回/分拣独立握手不因此改变。
现场轴闭环：本次请求后已观察运动中→到位，且新鲜实际位置满足目标容差，再撤销PC请求并以写后新鲜读回确认请求0；PLC到位1保留。最终新读状态/坐标/请求须一致，不把运动中0或旧缓存当作正常到位。后续异坐标请求需本连接已验证复位/上次动作的轴位置证明，再重新观察本次0→1；同坐标沿用本连接证明与新鲜请求/到位/坐标核验，不发多余运动。重连、故障、坐标变化和异常反馈使证明失效，不自动重发未知动作。
旋转轴同样撤销PC请求、保留到位1并核对角度；只有本连接已验证复位或已完成旋转证明才能沿用保留的到位基线。复位前R9撤销PC残留动作请求仍有效，不清PLC反馈。
定向验证：首次XY到位保留、连续两轮异坐标、Y/检测Z同坐标、同目标全复用；反馈未观察本次运动中时不假完成；PC请求不清、过期/断线反馈仍阻断。有限回归旧Test闭环、复位/SQLite恢复及旋转。现场完整流程不以离线夹具代替。
同时审计当前安装与R9发布基准、有效源码/脚本，保留并同步已授权本地R6–R10改动；现场配置、身份、数据库、配方仅保留，不当作通用默认值拷回仓库。无提交推送。


## SC-021-PLC-R11 用户明确轴语义收敛（2026-10-09）
本节取代R6/R10冲突规则：直线轴及R轴反馈0运动中/1到位/2超时。请求释放确认仅检查PC请求写后新读0；完整周期空闲确认仍检查翻面/放回/分拣请求及其反馈0。普通轴最终坐标、到位1、连接和安全均须核验。运动前新读请求0、到位1与实际坐标；0不可解释为停稳。异坐标发送后必须观察本次0→1和坐标，不用旧1完成。
同坐标只允许本连接正常轴动作完整完成并请求释放的有效记录，加本次新读坐标/到位1/安全。系统复位清除旧记录，不建立动作完成或同坐标沿用记录。无记录同坐标阻断，不伪造运动变化。R轴只修复反馈语义和角度核验，不提供同角度沿用。
系统复位MB2009写应答后新读MB6015=1即清MB2009，不要求先0；后续安全、初始位置、轴到位1、MB6052=0和旧任务收尾仍有效；不强清未知复位请求。虚拟下位机轴完成/复位保留到位1，撤请求不改反馈，同坐标不伪造0→1；相应旧Test轴测试假设同步纠正，翻面/分拣完整周期零空闲协议不变。
实施范围：Axes/Handshakes/Stages/主设备/Semantics/SiteOperations/VirtualPlcEngine.Axes及直接相关测试；区分AxisMotionCompleted、PC请求释放与后续核验的持久关联日志。保留已部署R6-R10其他必要修复、现场数据和协议来源；按正式发布构建原地校验更新，提交到现场origin的新同步分支，不强推。


## SC-021-PLC-R12 复位/启动零位容差独立配置（2026-10-09）
用户确认复位/启动零位允许±0.2毫米，正常运动到位精度保持不变。现场证据：MB6015完成新读1后Y=0.15085936，原零位容差0.1拒绝；随后只读Y已为0。此修改不移除零位门禁、不固定延时、不自动重发复位。
在既有通信机械配置SiteOperations中增加可选safeZeroToleranceMm，必须有限且>0，来源由原sourceReference记录；旧配置未提供时沿用原PositionTolerance。用于RequireSafeZero（复位完成后及启动前XYZ）和ReadInitialState的全部直线轴安全零位检查。普通配方运动坐标到位、同坐标沿用/失效、采集窗口坐标核验仍使用原PositionTolerance；R角度容差不变。现场配置最小合并safeZeroToleranceMm=0.2，不更改原PositionTolerance=0.1，不覆盖其他机械数据。
定向验证0.15086及边界0.2通过，超0.2阻断、普通到位容差不变；回归既有复位握手/恢复检查。保持PC请求读回、轴到位1、MB6052=0及未知复位禁止强清规则。正式构建后原地更新、摘要/备份/回退；不发真实复位/运动，不提交或推送（按用户最新要求先部署通知）。


## SC-021-PLC-R13 复位完成与初始检查失败区分（2026-10-09）
现场：2026-10-09 07:19:26发送复位，07:19:28新读MB6015=1并撤MB2009，安全/XYZ±0.2通过；后续初始检查503轮均AxisResetFeedbackValid及AllLinearAxesAtSafeZero失败，最后07:19:56归为RecoveryResetDeadlineExceeded。扫码Z读值32；07:20:49只读核对MB6040/6042/6046为0，MB6044/6048/6060为1，PC请求均0。
确定软件修复：将轴反馈检查失败及零位检查失败从通用DeviceWorkNotReleased中分离，返回明确ResetAxisFeedbackUnconfirmed、ResetSafeZeroUnconfirmed，复位握手完成后遇这些条件不反复等待而报InitialStateIncomplete，记录本次新读每轴请求/反馈/实际位置、预期零位和容差，保留真实复位完成事实。不把此类状态误显示为PLC复位超时，不扩大超时、不自动重发、不清未知请求或释放旧任务。
用户更正：扫码Z实际32毫米意味着未回到复位位置，并非允许的复位安全位置。撤销尚未部署的scanZResetPositionMm配置及相关实现，仍按零位±0.2毫米核验，不用修改允许位置掩盖未复位事实。待用户决定：MB6015=1且PC请求0时X/Y/扫码Z反馈0是否属于正常复位状态。当前不据此放宽轴反馈门禁，其他确定软件修复继续。R12零位±0.2、正常运动精度及R11完整动作证明保持；若需额外现场语义，先补规格契约再实施。
