# 021软件实施记录

## 2026-10-09 最新增量：SC-021-PLC-R5复位确认

用户确认发出MB2009后，由PLC完成动作并置MB6015=1，PC新读1后清MB2009。本轮按此取代历史先0再1要求，并将清MB2009前移至后续安全/位置核验之前。未新增信号或延时；现有旧高请求拒绝、真实安全/位置/反馈核验及旧Run持久取消条件仍有效。

实际验证：`dotnet test backend/tests/Gaode.Communication.Tests/Gaode.Communication.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~CommissioningRecoveryTests|FullyQualifiedName~SiteOperationHandshakeTests'`通过23/23、零跳过，测试耗时42秒。脚本PowerShell语法解析、配置JSON解析及`git -c core.whitespace=cr-at-eol diff --check`通过。新用例实证：零次未就绪读取、一次完成读取即可清请求；请求前的缓存1不替代发令后的真实0读取；持续0超时保持请求且不重发；收到1后已清请求，XYZ仍为1.25时阻断启动。SQLite及Host三次构建实例重启恢复、保存失败、残留翻转反馈、请求取消/预算到期等回归通过。

可审阅证据：[汇总与源码摘要](evidence/reset-ready-r5-20261009/summary.json)、[关键运行事件](evidence/reset-ready-r5-20261009/runtime-events.json)、[测试结果](evidence/reset-ready-r5-20261009/reset-ready-r5.trx)。构建基线6ec4696加本次工作区差异；完整本机证据在artifacts/reset-ready-r5-20261009/run-626ac4ef6c9644b3a27b1c6d7ac64616。仅使用127.0.0.1模拟PLC及真实SQLite，未连接设备、未更新中控机安装，不代表现场PLC动作及完整恢复验收通过。

2026-10-08；实施开始，尚无本功能运行验收结论。分支020、feature指向021。T001已核基线，原型摘要一致，既有修改及五个回退标签保留。PLC/相机连通为用户报告，不代替流程证据。T055/T056现场Blocked。

本轮仅软件/离线验证，不连接硬件、不打包部署或提交推送。requirements只读。


## 2026-10-08 实施增量一：验证基础及身份

- T004：离线夹具支持明确commissioning/legacy-test两种用途，使用正式Host API、共享SqliteRecipeStore及loopback PLC，七真实SDK worker不启动。Commissioning媒体组件经RunExecution创建、隔离ICapturePort响应、CameraAcquisitionService及共同媒体保存/提交，真实SQLite查询两个Run（8张/1张）。仅声明组件事实，不造检测结果、运动、Final或生产成功。基础自检见evidence/fixture-commissioning.json.check.json及fixture-legacy-test.json.check.json。
- T005：真实STA WebView2探针引用公开HostRuntime，驱动持有实例DOM，已构建；无远程调试、产品测试桥或浏览器替代桌面。真实身份操作证据另见V01。
- T006：Node入口修正为现有.mjs；3项测试通过，typecheck通过；原型精确重建门禁通过，见prototype-foundation.json和prototype-identity.json。原型归档与三页源码未改。
- 既有授权资源清单过时：recipe-authoring.js的预期摘要对应b0e0098，当前源码为0f91f95成员抓手修改。保留源码，登记5个精确资源差异，门禁反向还原并核旧摘要；没有只刷新总摘要。
- T007/T008：身份组件2项前端用例、后台8项用例通过；后台覆盖两角色权限/缺失无效/重复配置/Test用途与凭据隔离/query凭据拒绝/固定CORS。后台结果见evidence/test-results/identity-expanded.trx，原identity.trx保留。
- T009/T010：桌面新增显式非秘密profile及所选环境凭据；限制顶层受控来源注入、日志脱敏，页面独立查询身份并绑定现登录/角色/导航。尚待完整V01证据后判断验收，不凭构建通过代替实测。

保留的失败与修复记录：媒体夹具首次用Audit创建Run遭RevisionOrTerminalMismatch拒绝，已改走正式RunCreated；随后缺设置应用事实被CaptureSettingsEvidenceMissingOrMismatched拒绝，已补齐显式OFFLINE夹具参数消费事实，未改产品门禁。原型检查先发现build资源登记缺项，后发现上述既有成员抓手摘要过时，均经具名精确登记修正。Identity GET首次误用Host不可见RuntimeDiagnostics导致CS0122，改用现ILogger持久提供者后8项测试通过。桌面启动脚本首次读仍在写的重定向文件遇共享锁，改为观测完成停止夹具后读，原实际桌面通过记录保留于v01-identity-attempt.json；后续完整V01另记。

以上均为OFFLINE软件范围；T055/T056及现场安全输入阻断保持，不声明新用途正常整机通过。


## 2026-10-08 用户新增要求及实际缺口（未实现/未验证）

用户要求正式可配置多配方、虚拟算法，以及配方界面“虚拟光源”勾选后跳过光源参数/控制。源码核查：CommissioningConfiguration目前持有单项ExpectedRecipe，CommissioningAlgorithm绑定核定该项且ReadECode明确拒绝；正式配方能力与当前虚拟输入覆盖范围不同。CameraCaptureAdapter当前configured采集始终要求灯通道并调用亮度/开关，SimulatedLightGateway只消费虚拟状态，没有跳过配置能力；共同Validator仍要求光源参数。因此不可声称本次要求已实现。

已追加CC合同并同步spec/plan/data-model/quickstart/任务和012共享合同边界，T033–T037未勾选。本增量没有修改产品代码、执行新测试或连接硬件；已有V01/V02证据不改标签、不补记V07/V08通过。requirements及020历史资产保持只读，T055/T056继续现场Blocked。


## 2026-10-08 F读码位置录入与当前软件验证增量

用户明确要求F读码XY加入虚拟算法联调配方设置，现场值由用户示教后填写。已同步FR-019/SC-009、CC合同、012共享边界及T040，再实施共同正文、页面、校验、保存重读、启动引用和每Run冻结；无默认现场坐标。历史“新增要求未实现”小节保留为当时核查记录，当前状态以本节及逐项证据为准。

- 正式桌面HostRuntime/WebView2的实际页面，工程师勾选虚拟光源、填写明确OFFLINE的F XY、经正式API保存并重读成功；相机曝光仍可编辑、虚拟光源亮度禁用。证据：evidence/console-20261007-221044/v08-page-light-F.json；另一次独立通过见console-20261007-221212同名文件。测试值只在临时离线SQLite中，不是现场配置。
- 配方选择事件存在读取值晚于编辑器重绘的确定缺陷，已改在捕获阶段读取选项。首次修复后操作员实际选择/启动、丢失回执查询、刷新后关联原Run通过，见console-20261007-221044/v03-page-query-operator.json；后续重复尝试在目录加载等待超时，见console-20261007-221212同名文件，不以首轮通过掩盖尚未定位的问题。
- F-position.trx：1/1通过，实际MediaStore写入/提交并由虚拟算法读取本Run媒体；两Run分别生成旧/新FLocation，再读旧Run仍为旧值；超行程FreezeRun被拒绝。证据：evidence/F-position-regression-20261008/F-position-run-isolation.json。此为冻结/算法输入证据，不冒称PLC实际执行已验证。
- 本轮前序定向后端整合回归40/40通过、0跳过，见evidence/test-results/commissioning-regression.trx；增量光源/查询6项见light-and-query-increment.trx。范围包括身份、光源模式、请求查询、闭环清零、同坐标、现场条件缺失阻断及原用途离线正式整链，不等同新用途真机流程。
- 前端7/7通过、typecheck通过；原型具名精确差异反向重建通过，见evidence/prototype-F-position-20261008.json，原型ZIP只读。
- 七图离线桌面尝试已显示7张当前图，但映射断言失败，见console-20261007-221044/v06-page-media-data.json；不能声称V06完成。后续重试在操作员目录等待被阻止，未覆盖媒体步骤。失败原件均保留，探针现在记录失败断言实际结果，便于后续定位。

T040保持未勾选：保存重读、Run输入隔离和行程阻断已有证据，仍需正式启动链的实际运动请求核对。T011及后续故事完整验收、V07/V08余项、T038/T039包和本机安装未全部完成。未连接硬件、未制作部署包、未提交或推送；现场F坐标尚未填写，020 T055/T056继续现场Blocked。requirements保持只读。


## 2026-10-08 部署、原配方录入与定向回归增量

本节更新先前“未打包/未安装”的当时状态，不覆盖失败记录和历史范围。用户已授权制作部署包、独立本机安装及录入已有联调成功配方；本次没有连接PLC/相机，没有提交推送。

- 真实WebView2桌面三项离线路线全部通过：evidence/console-20261007-222550中的v08-page-light-F.json、v03-page-query-operator.json、v06-page-media-data.json。探针等待完整页面及带data-media-id的真实查询图片，修正把原型初始图片当作媒体就绪的验证错误。覆盖工程师光源/F保存重读、操作员原请求恢复、当前Run七格与数据页；不代表V01–V08全部验收。之前超时和失败原件保留。
- 原本机工具启动器选中的1.1.6配置为配方来源；原件SHA256 A898BC7001FEBF8E0D626F0542BACDAC7AE7A8C493C80B3DD4D7202C81FF9A75。成功联调为用户报告，本轮未重测硬件。转换源/逐字段占位说明见configuration/commissioning及evidence/local-recipe-conversion-20261008。F65/50、检测Z5、翻面取放XY10/10均有来源，无任意运动目标。
- 正式RecipeDetectionExecutor验证：deployment-final.trx为2/2通过、0跳过。ImportedCommissioningRecipeTests经正式SQLite保存、新实例重读与正式执行器，核对四机实际请求、翻面/放回/复查、SQLite及媒体；evidence/deployment-regression-20261008-final/imported-recipe-execution.json。HandshakeClosureTests新增占位夹爪保护验证：缺分拣安全配置时，在夹爪选择、轴请求及SortingCmd写入前拒绝，并有通信诊断。全部仅loopback/隔离离线端口。
- 先前deployment-regression.trx为23通过/1失败，失败为测试夹具硬编码test-frame与来源配方坐标系不匹配；只修正夹具从已保存正文取得frame/slot，不放松产品门禁。imported-recipe-r2.trx和上述最终2项通过保留对照。23项含清零/同坐标阶段回归；没有扩展全量异常矩阵。
- 阶段适配器曾误把RealDeviceCommissioning拒成非Production，现严格匹配设备实际配置用途。Release发布通过，未绕过SafetyUnconfirmed；现场正常派发仍未验证。
- Host、Desktop、CameraWorker、DeploymentPrep四项Release发布完成。DeploymentPrep锁文件先因旧RID形状报NU1004，旧原件保存在evidence/deployment-prep-lock-before-20261008.json；仅对齐无RID发布，实际各非RID包解析版本比对不变。失败包目录保留，未升级依赖。
- 最终交付目录约定D:/gaode/artifacts/gaode-commissioning-console-021-final-1，独立安装D:/Gaode-Station01/commissioning-021-final-1；最终安装/文件校验实测记录另存evidence/deployment-final-20261008，不把约定路径当通过证据。旧0645/0700安装和旧联调入口保持原样。包绑定Git基点253324492a3ffe9ea5a62b605b5dd812d04b267c与脏工作区实际源码快照/文件SHA256，不伪称已提交版本。

当前未验/阻断：完整V02页面从零制作及坐标/单张曝光在途隔离、V05真实桌面正常两轮、V07多配方完整路线仍保留，不能由导入测试替代。正式现场完整流程未测，020 T055/T056保持Blocked。公共/机械行程与首次/恢复安全语义、身份与桌面运行资料尚缺完整核定配置；默认入口返回FieldRuntimeProfileMissing，不启动设备。包可安装/配方已录入与现场可以运行是两种不同结论。requirements只读。

最终安装实测：manifest795文件核对通过，安装到上述新目录，RecipeId=5e8ab0f51041427f8946108b84b46b81，Version=ac5b30ee8d264939bb53be38042862ef，DefinitionDigest=38CA3A3094E63EE963BE10575373C7190D02946FCB25202CDDA4DB986ACEF665；正式虚拟输入加载valid=true/resultScopes=6，默认启动检查ConfigurationRequired/FieldRuntimeProfileMissing、networkAccess=false、deviceDispatches=0。安装数据及最终包文件校验汇总另存evidence/deployment-final-20261008。T038/T039/T041/T042仅按各任务限定范围勾选，不关闭其他完整故事或现场阻断。


## 2026-10-08 连续两轮确定缺陷与修复（T043/T044）

同一个正式离线Host连续两轮验证首次失败于SimulatedCapture：F单拍计数误按进程生命周期限制。修复为按RunId拒绝同轮再次触发，累计计数只记受理。第二次继续执行在ThreeStageWorkflowExecutor遇T050StageEventIdempotencyConflict：阶段字面键缺Run/Tray命名空间，第二轮撞第一轮SQLite全局索引。已在生产端加入稳定run:{runId}:tray:{trayId}:前缀，不修改索引/表或StageEventStore冲突门禁。共享语义先记SC合同/plan/tasks，历史库不迁移，不借版本切换重发旧动作。

失败保留two-round-final.trx、two-round-final-r2.trx与对应evidence/two-round-final-20261008及-r2日志；修复后-r3为1项通过。最终two-round-final-r4.trx为3/3通过、0跳过，范围为完整后台两轮与既有查询/owner隔离两项。evidence/two-round-final-20261008-r4/two-round-final-completion.json证明：同Host、同设备适配器、不同显式请求/Run/Tray，两轮各7条媒体、人工确认API与SQLite最终完成，新读者重读；两轮之后准入Available，未手工释放注册表或重建Host；同Run F重拍拒绝且累计计数不变；真实已保存阶段键重放返回Replay，改变摘要返回Conflict，事件数量不增加；旧Run Final仍可读。

此为旧Test布局/离线正式后台的正常两轮，不是桌面DOM人工确认两轮或现场新用途通过，因此T027/V05完整桌面验收不勾。020 T055/T056保持Blocked。现场答复已给X/Y=0–100、Z=0–10，10处配方坐标数值均在范围；单位/三根Z适用性及开机恢复语义尚未获完整答复，资料见evidence/deployment-final-20261008/field-limits-user-r1.json及runtime-preparation.json。原成功local.json实际REAL读写均Cdab，旧realOrder=Abcd不作生效值。

两项修复进入后续final-2部署包；final-1及所有旧安装、证据、标签保持，最终包/安装摘要另存evidence/deployment-final-2-20261008。requirements只读；无设备连接、无提交推送。

final-2实际交付增量：四项Release重新发布成功；新独立安装D:/Gaode-Station01/commissioning-021-final-2已准备真实SQLite，原成功配方再次经正式保存/重读，RecipeId=e40ca547e7e34242a69cfa08554c218c，Version=08193b7a4c874157992bee90897f3c49；虚拟结果6项绑定该版本并经正式加载valid=true。首次包/安装796项文件校验通过，最终冻结及ZIP校验以deployment-final-2-20261008记录为准。默认启动检查仍为ConfigurationRequired/FieldRuntimeProfileMissing，未连接设备。final-1/旧安装不覆盖。新包范围记录保存用户给出的数值，但单位与三根Z适用性尚待答复；缺PLC首次恢复语义的相关动作仍阻断。


## 2026-10-08 真实桌面配方编辑及执行隔离（V02子项）

RunRecipeProbe.ps1改为每次独立证据目录、不覆盖旧记录；等待A实际冻结标记再编辑，并等待完整页面加载，页面和后台执行结果任一失败即报错。夹具/依赖构建0警告0错误；实际WebView2工程师页面选择既有配方、改一处X与单张曝光、正式API保存/重读、关闭重开后值保留，全部DOM断言通过。evidence/recipe-20261007-230250记录：A冻结版本098de66e3a3a4c58b8065fb6869da681，B保存版本3c16c240acf74c5dad2a8676419be44d。正式执行器A实际X10/四张曝光10，B实际X11/曝光110、10、10、10，其余三张未被修改；证据v02-run-a/b.json及v02-edit-isolation-audit.json。均为OFFLINE验证值，声明端口不代表真实机械/SDK应用。

另外用只读SQLite连接重新检查两Run各5条媒体与原文件长度，备份实际SQLite到durable-run-a/b/run.db并复制已提交媒体，共2份数据库和10个文件；便携证据不依赖临时目录仍存在，不写伪造成功记录。受影响四个产品源码消费者与final-2源码快照一致，本轮只改探针执行脚本/验证文档，未改产品代码；正式包无需为此重新编译。证据补充包单独关联final-2及其SHA256，不覆盖旧ZIP。

T015仍未整体勾选：本轮证明既有配方编辑/保存/重读/选择与在途旧值/后续新值，不是从零新增配方全过程。完整V05桌面两轮、V07多配方路线等未验子项保持真实状态。用户轴范围记录及待确认单位/三根Z、PLC首次/恢复准入语义仍只阻断依赖现场任务；020 T055/T056保持Blocked，requirements只读。


## 2026-10-08 本机受控身份与桌面模板（T045）

沿用户已确认预配置身份策略，在独立安装final-2的data/private-identity生成两项不同随机凭据，仅当前维护用户ACL可访问。凭据不复制进包、源码或证据；无默认管理员、无旧Test凭据复用。非秘密desktop-operator/engineer.json及console-template.json位于data/config，业务场景/槽/版本引用来自本安装正式保存配方与虚拟输入。内部station01/line01为软件关联标识，不作为PLC地址/安全信号来源。维护脚本Set-CommissioningIdentityEnvironment只加载当前会话，不启动Host/桌面或网络设备。

证据evidence/deployment-final-2-20261008/local-identity-check.json：直接加载安装内正式程序集，CommissioningIdentityRegistry分别核定两角色及权限，HostConfiguration.FromEnvironment/Validate通过，两角色与desktop profile匹配；StartRunContextParser对模板补本次逻辑TrayId和实际expectedRecipeRef后解析通过，未派发Start。ACL实际AreAccessRulesProtected且仅当前用户规则。上述均是正式配置读取类实测，不是界面/真机运行通过。脚本和非秘密检查可作为补充资料分发，私有凭据只留本机。

尚待现场答复：用户给出数值XY0–100、Z0–10；单位和三根Z适用性尚未确认，开机/复位后的允许开始条件、2007/2009时序及断线/软停区别仍未明确，不能生成已核定运动配置或编造PLC安全映射。正式安全采样仍为Unconfirmed而非凭配置标志放行；运行profile/公共和预算/机械配置完整装配还未完成，默认启动检查继续拒绝。020 T055/T056保持Blocked，目标未标整体完成，requirements只读。

## 2026-10-08 轴范围与单位答复补充

用户最新答复“是”确认单位为毫米，检测Z、扫码Z、翻面抓取Z均为0–10，X、Y各为0–100。此前单位及三根Z适用性待确认的记录保留为历史状态，本补充取代其当前待确认结论。新记录evidence/deployment-final-2-20261008/field-limits-user-r2.json关联final-2实际保存RecipeId=e40ca547e7e34242a69cfa08554c218c、Version=08193b7a4c874157992bee90897f3c49，并从正式重读文件重新遍历检查29个已存XYZ数值分量，全部在确认范围内；副本保存在独立安装data/config。此为离线数值检查，不证明设备原点、当前轴状态或运动安全，未改产品代码、未连接设备、未覆盖包及历史证据。

剩余现场依赖仍包括PLC首次/复位后放行条件、2007/2009置位清零时序及断线/软停行为；完整运行配置及安全映射不能凭范围确认自动放行。旧goal-blocking-audit.json为答复前快照，其fieldUnitsConfirmed/zRoleRangesConfirmed=false现已由本补充关闭；initialRecoveryPlcSemanticsConfirmed仍为false。020 T055/T056保持现场阻断，requirements只读。

### 2026-10-08 PLC“就绪”含义确认增量

用户针对MB6015就绪=1答“是”，确认它保证复位完成、各轴停稳、允许PC开始流程。记录见evidence/deployment-final-2-20261008/plc-ready-user-r1.json。本答复关闭PLC-Q4中这一项含义澄清，未关闭2007/2009请求置位/清零时序及断线/2008软停行为。不得继续把MB6015这三项含义列为未确认；也不得把此次确认扩大为全部现场恢复语义或真机验收通过。准入仍须本连接新鲜可靠反馈、既有安全检查、相关旧反馈清零和有效配方/配置；未知动作不自动重发。

源码核查：backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Semantics.cs中现场布局仍显式输出SafetyUnconfirmed/ManualAreaUnconfirmed。此次只记录确认，不改产品代码或以配置绕过门禁；后续实际映射须先同步对应规格、契约、计划和任务并定向验证。原冻结包不覆盖，020 T055/T056保持现场阻断，requirements只读。

### 2026-10-08 复位及启动边沿答复增量

用户确认：PLC只读PC系统复位请求MB2009的上升沿；收到请求后将MB6015从1置0，复位完成后由0置1，PC观察本次0→1后清MB2009。等待期间须确实观察到本次0，再观察本次1，不能只等任意新读1；超时、断线或反馈过期不得视为完成或自动重发。MB2007启动也只需上升沿，不要求持续保持；清零时点用户要求先解释后再决定，尚未确认。用户说“设备就停住就行”“安全位置位于0”；停住是否同时适用软停/断线及其与原表自动回位的裁决待明确；安全位置按协议所述X/Y/检测Z零位理解，不推导自动回零命令或编造容差。详细答复及范围见evidence/deployment-final-2-20261008/plc-sequence-user-r1.json。

源码核查发现LatestProtocolPlcDevice.ResetAsync当前复位请求置1后立即清0，未等待Ready本次0→1；AdvanceStart只写PcSystemReady并等待PlcReady，未派发MB2007。此为待实现缺口，不能因旧启动方法存在称新握手已完成。本轮仅记录答复/缺口，未改产品代码或重制包；实际共享变更实施前须完善对应契约/任务。原包、历史记录及requirements保持，020 T055/T056仍现场阻断。

## 2026-10-08 PLC确认规则源码落实与补丁验证

本轮用户确认MB2007在第一条实际运动完整闭环后清0；MB2008软停及心跳断线都停住、不自动回位。复位按MB2009上升沿、本次MB6015先0后1、检查新鲜XYZ零位后清请求。协议/地址来源原件不修改；新约定在SC-021-PLC-R2明确裁决旧软停自动回位说明。PcStartCmd新增为现场MB2007 BOOL高字节，旧Test地址表不加点。正式机械配置增加siteOperations（plc-site-operations/1及确认来源）；未提供时仍安全Unconfirmed。已提供时按已确认点表读取报警/独立安全点/光栅，不凭就绪覆盖报警，不写PLC报警。

实现修改包括：现场复位不再立即清请求，等待本次Ready下降及上升；启动前读新鲜XYZ与零位比较，不自动回零；启动发MB2007上升沿，首个实际Move的XY及适用Z全部完成、位置复核与双方清零后清启动。纯同坐标复用不算首次实际运动。另纠正旧ObserveAxisClosures对现场布局无条件撤销资格及引用缺失ManualZoneOccupied的问题；按已确认现场安全点检查保留闭环资格，旧Test逻辑保留。软停仅一次派发MB2008，不发自动回位/复位，之后PhysicalStopUnconfirmed并阻断自动续发；不把软件发出命令当作物理停稳证明。完整恢复的持料/夹爪核定未自动放行。

测试：plc-sequence-final.trx为35/35通过、0跳过（8项新现场握手/零位/软停/报警验证，3项既有现场协议验证，24项清零/同坐标回归）。最终现场原始通信审计、运行日志及实际SQLite位于evidence/plc-sequence-final-20261008；旧Test回归实证位于plc-sequence-legacy-final-20261008。r2/r3/r4失败与r5定向通过保留，前两次发现现场资格无条件撤销，随后暴露缺失旧ManualZoneOccupied依赖并修复；未隐藏失败。所有测试仅loopback，不连接设备，不能当作T055/T056现场通过。

T046/T047完成。下一步制作源版本关联补丁并实际应用/安装校验；原final-2包/安装不覆盖。当前完整运行profile尚未装配，这属于配置工作，不再把本次已确认的PLC含义/轴范围重复列为业务待澄清。现场流程、实际SDK动作及完整桌面正常两轮仍按历史未验证范围保留；requirements只读，无Git提交推送。

## 2026-10-08 final-2→final-3补丁交付实证（T048/T049）

四项Release发布完成（Host/Desktop/CameraWorker/DeploymentPrep），无硬件连接。final-2原包796项清单通过，新包final-3为834项；补丁80项变更、原始payload 6,884,990字节。实际Apply-CommissioningPatch从精确基包校验后生成artifacts/gaode-commissioning-console-021-final-3-patched，834项全部一致；随后在D:/Gaode-Station01/commissioning-021-final-3全新目录安装，正式维护入口准备真实SQLite，保存/重读配方RecipeId=1eb168da6ae44f1f983c17c48d21a59f，Version=3e477719e9454072a999a8b7acdde9d9；虚拟输入六项作用域绑定该版本、摘要校验有效。本机操作员/工程师身份新建且正式Registry/Desktop配置/业务上下文解析通过，秘密仅在新安装data/private-identity，不入包/源码/证据。旧final-2安装再核796项通过。

source-match.json核九个受影响产品源码与包内快照一致；构建后收尾任务/验证文档增量另在补丁delivery-evidence保存，冻结包的tasks是构建当时状态，最终进度以本补充及当前tasks为准。补丁含源文件增量、二进制增量、确认配置来源、35项回归结果、日志与SQLite；基包/目标清单和ZIP SHA256关联，不重复制完整350MB归档。

软件交付任务T046–T049完成，仅限定本次规则实现/定向回归/补丁与独立安装。完整现场公共配置、预算、PLC机械/地址及Host运行profile尚未装配并启动核验，CheckOnly仍为ConfigurationRequired/FieldRuntimeProfileMissing、派发0；不得称真机可运行或完整021验收通过。已确认的PLC就绪、复位/启动清零、停住及轴范围不再当待澄清阻塞；旧完整恢复持料/夹爪核定仍不自动放行，T055/T056保持现场未验。此前工具成功为用户报告，仓库工具本轮未重制：其通用手工MB2007/MB2009写点入口不等同正式程序新托管握手，不能以旧工具记录证明新握手现场通过。未连接设备、未执行-Run、未提交推送，requirements保持只读。

## 2026-10-08 T050 本机运行配置装配
final-3安装的data/config/runtime-r1完成公共/预算/机械/现场地址/Host装配。使用已安装程序集执行正式schema加载、AddStation01注册、PLC构造/协议定义检查、七相机绑定检查、SDK依赖存在检查、正式SQLite配方重读/运行库用途检查、虚拟算法FreezeRun/BindRecipe及正式能力/执行成本绑定，全部通过；Real光源缺参数仍被正式校验阻断。发现旧001 schema缺已有公共lightExecution合同，021补public-config.runtime.schema.json并在独立schema目录覆盖，不改原件/不可变包。检查结果evidence/runtime-config-20261008/preflight-result.json，0协议问题、0算法配置问题。相机状态Stopped，未调用Host/worker启动、无网络/设备派发。当前入口检查从ConfigurationRequired变为ConfiguredNotFieldValidated、reasons空；只证明配置装配通过，不等于实际进程/硬件启动成功。WebView2已安装，检查时5190无监听。新入口Start-Station01.ps1默认只检查；启动联调.cmd显式-Run并先载入操作员受控身份。秘密未复制到证据，原ACL复查通过。T050完成；020 T055/T056、完整桌面/现场未验项保持。原ZIP/补丁不重写，本机配置作为附加配置层；Git推送暂停。

## 2026-10-08 T051 实际启动失败及修正
用户启动日志显示桌面05:03:20.446Z发生IdentityRejected/TypeError，后台在七相机Ready后才监听5190，随后05:03:44Z开始PLC连接、45Z发生首帧FC3读取超时并退出。直接原因是桌面未等待后台就绪；后续独立故障是PLC经Meta/198.18.0.1而非专用PLC网卡。已保留原失败日志于evidence/login-startup-20261008。源码及当前安装启动脚本增加正式身份接口就绪和profile匹配等待，再打开桌面；后台退出/超时/401/403/身份不符则阻断，不改鉴权或前端原型。定向Mock检查5项通过：延迟就绪、身份不符、后台退出、超时、启动顺序；无真实连接/Host/worker启动。Publish脚本同步携带辅助脚本。当前系统仅新增ActiveStore路由192.168.0.10/32→PLC/0.0.0.0，Find-NetRoute核对源地址192.168.0.88及PLC接口；不更改Meta全局配置、不发PLC请求。该路由重启系统后需重新核对；回退只删除本次PLC接口ActiveStore的/32条目。当前残留旧桌面不能自动恢复身份，需用户关闭旧窗口再从启动联调.cmd重开；本轮未替用户重启，修复后真实登录/PLC交互待验证，不称现场通过。

2026-10-08 T052：源码scripts/Stop-CommissioningConsole.ps1及.cmd已复制至final-3安装scripts及根目录“一键关闭.cmd”。按三类准确可执行路径识别进程，执行前复核PID/路径，先请求桌面窗口退出再结束残留，后台/worker结束并检查退出；不按全局进程名批量结束、不发送PLC复位/运动，结果未知动作不续发。WhatIf实际列出本安装桌面1、Host1、worker7，未关闭任何程序；不是实机停止验证。实际使用会结束后台及相机采集进程，关闭记录在data/close-history.jsonl；仅用于停机后关闭软件，不代替现场急停。Publish脚本包含新脚本及根目录cmd，原不可变包不覆盖。

2026-10-08 T053：Reset-CommissioningConsole.ps1及根目录“一键复位.cmd”已安装final-3，源码Publish同步。脚本从受限本机身份加载凭据但不打印，先读取status，仅在Idle/Blocked/RecoveryRequired/Failed/Completed/Cancelled/Stopped调用已有POST /api/v1/station01/reset一次，35秒HTTP上限；失败/未知不重试。既有正式ResetAsync执行MB2009与Ready本次下降/上升、零位/安全确认，并释放既有启动/软停请求；脚本不增加直接清轴/清反馈语义。随后查询start-admission，Held时明确原任务仍阻断，不伪造恢复或下一轮许可。仅执行CheckOnly和PowerShell语法检查，未调用实际复位、未修改身份权限、未解除当前故障Run、未发启动。用户保证仅停止或报错时点击，不等于允许绕过结果未知处置。


## 2026-10-08 软停/复位顺序R3实证

依据用户明确确认“PC先就绪并取消软停，PLC才能复位”，已修正通信适配层：MB2009原请求为0→MB2006=1、MB2008=0→新鲜读回确认→MB2009=1→本次MB6015先0再1→安全及XYZ零位核验→清复位/旧启动。没有软停也走同一路线，不先置软停1。内部pcReady仍保持false直至完整核验完成，取消软停不发启动或轴运动。

离线：Release定向32/32通过，TRX=evidence/test-results/reset-order-r3-final.trx；各测试原始TCP写审计、SQLite通信证据及持久日志在evidence/reset-order-20261008-final。旧测试与现场loopback范围分开，非真机通过。初次3失败/28通过记录保留在reset-order-r3.trx；修正读取计划后31/31，再加入无软停正常完成后32/32。

前端：既有故障通知区明确旧任务恢复入口不可用与重启无效；没有增加成功按钮或放行。start-recovery组件2/2、frontend构建及原型精确替换校验通过（evidence/reset-order-20261008/prototype-r3.json）。这不是实际桌面交互验收。

补丁：artifacts/commissioning-021-reset-r3-hotfix，7项payload基线/目标SHA校验通过；安装目录updates/reset-r3及根目录“应用复位修正.cmd”已准备。应用脚本在独立离线目录实际完成备份/替换/逐项哈希复核；针对实际仍运行的安装，实际拒绝覆盖，未停进程/未连接设备/未应用。原包及运行库不改。

未解决：Commissioning未接入重启旧Run完整受控恢复，原Run 3d55a6d8-37b6-4988-8c6f-085b327ad61d保持Held。上次复位请求MB2009若仍为1，继续拒绝重发，不能声称本补丁会自动处理该未知请求。需要依据实际复位记录及现场持料/归位/翻转说明补齐T056；不能删除库、假定已取盘或无条件释放。


2026-10-08 final-4整包增量：按用户要求将R3补丁合入新的完整部署包，原final-3完整清单834项核对后复制，合入6项可分发payload（第7项为当前安装专用data/config运行配置，不放入通用包），更新启动等待身份/关闭/复位脚本、安装根CMD复制范围及021运行schema，最新源码/变更和32项离线回归证据随包。新目录886项校验通过。ZIP哈希及逐项核对见evidence/deployment-final-4-20261008/package-audit.json。没有连接设备、安装或替换当前运行文件；包不含本机身份凭据/运行数据库/私有profile，现场配置和旧Run恢复限制继续如实保留。

## 2026-10-08 R4：完整复位恢复流程（替代上述R3当前缺口说明）
用户已明确确认PLC系统复位会全部恢复：零件放回、夹爪松开、翻转机构回到初始状态。已新增来源绑定的restoresWorkpieceAndMechanisms配置，未加载该确认的配置仍不放行。正式/reset要求旧执行退出、采集/算法/媒体资源释放，PC先就绪并取消软停、新鲜读回后发复位上升沿，观察本次Ready先0再1，再核验安全、五根直线轴零位及相关请求/反馈清零。SQLite提交旧Run取消及恢复证据成功后才释放所有权；保留旧历史，不伪造检测完成，不续跑旧动作。
原故障通知处提供“复位并结束旧任务”；一键复位也走同一后端入口。成功后刷新页面、核对配方，由人员手动启动完整新一轮，新请求关联旧Run及恢复提交ID。Host重启后从真实SQLite恢复旧任务占用；已核验取消的任务不会再次恢复为在途任务。
软件验证：recovery-r4-verified.trx 40/40通过，包含8项恢复验证（正式Host三实例启动/SQLite重读、旧任务取消及新轮引用、资源未退出零复位派发、取消提交冲突不释放、旧翻转反馈不清零阻断、无来源配置拒绝）和32项现场协议/清零回归；此前final回归40通过/1失败如实保留。失败定位为清零等待连续强制新读造成8192条通信证据环覆盖；修复为首次立即读、后续按既有启用组周期采样，新鲜读回及原动作期限不变，再回归40/40。旧Test完整流程在recovery-r4-final.trx中单独通过，实证在recovery-r4-20261008-legacy。前端start-recovery组件3/3、构建和客户原型精确差异校验通过；未宣称本轮实际桌面交互或真机流程通过。
交付方式：更新原artifacts/gaode-commissioning-console-021-final-4目录和同名ZIP，不新增编号；旧ZIP/清单/摘要及R3待应用补丁备份到artifacts/rollback-final4-r3-before-recovery-r4。同步原安装updates/reset-r3待应用补丁，根目录“应用复位修正.cmd”沿用。当前运行安装尚未覆盖：先一键关闭，再应用复位修正，再启动软件；应用更新本身不复位PLC、不发运动。完整包不含本机秘密或运行库，本安装机械配置随定向补丁更新，避免覆盖其他站点配置。
限制：上次MB2009若仍为1，仍拒绝重新发上升沿，须核定旧复位结果；不能自动清0重试。现场完整流程及020 T055/T056继续未验证，不以离线通过替代。requirements只读、无硬件连接/复位/运动、无Git提交推送。最终文件校验及ZIP摘要见evidence/deployment-final-4-20261008/package-audit-r4.json。

R4交付核对：T056/T058/T059软件任务完成，原final-4完整目录和同名ZIP已实际逐项校验；36文件补丁已在隔离目录完成备份/应用/哈希复核，并同步原安装待应用目录。现场运行文件未覆盖，T055安装实际应用/桌面现场验证及020 T055/T056保留未验证。最终摘要见package-audit-r4.json。

2026-10-08 R4安装及GitHub交付状态更新：已按用户授权把R4完整包解压更新至原D:/Gaode-Station01/commissioning-021-final-3，36文件定向补丁亦已应用；964项安装文件校验通过，原配方库及运行库哈希保持不变，账号配置保留，备份位于data/deployment-backups/before-r4-20261008-160556及data/hotfix-backups。此前“补丁待应用/未覆盖本安装”属于该时刻历史记录，当前已由本段替代。T055的软件分发/安装子项已完成，仅实际桌面恢复交互与真机流程未验证，整体不借此勾选。用户后续启动后17:07只读状态为PlcHeartbeatLost、旧Run仍RecoveryRequired/PhysicalRunHeld；本次交付不证明已复位恢复或运行放行。部署包产品源码348文件与待推送源码完全一致，无新增产品改动，不需要再打二进制补丁。版本对应通过ZIP旁source-version.json及安装data/git-source-version.json记录；保留原封包sourceHead/dirtySource构建事实，不倒改历史清单。详见github-delivery-20261008.md。

## 2026-10-09 SC-021-PLC-R6 安装更新

已按用户确认修正手动复位恢复与自动运动准入、复位轴到位保留及MB6052清零门禁。55/55相关回归及最终28/28复位恢复回归通过；最终发布基准245运行文件摘要一致，13项原地替换，45项现场数据/配置保护检查通过，安装CheckOnly零设备派发。源码基准1eccc589加未提交R6差异，精确证据在evidence/reset-retained-arrival-r6-20261009，安装/备份/回退详见D:/gaode/artifacts/installed-reset-r6-20261009/更新与回退说明.txt。用户确认设备停稳允许维护后关闭软件更新，未执行复位或运动、未强制结束旧Run。PLC侧MB6052修改为用户报告，真实复位恢复和完整配方仍未验证，020 T055/T056保持现场阻断。


## 2026-10-09 SC-021-CAMERA-R7

47/47相机/协议/地址核验/PLC复位/SQLite收尾定向测试通过；原地替换11项，245运行文件与正式发布基准摘要匹配，47项现场资料保留，CheckOnly零派发。真实3D SDK打开、现场复位及完整流程未测；T055/T056保留。报告与原始成功/失败证据：artifacts/installed-camera-recovery-r7-20261009/修复及更新报告.md。


## 2026-10-09 SC-021-UI-R8

复位后新轮/恢复提示改用executionState。13/13前端定向测试（含实际click处理代码离线边界执行）、原型精确差异核对及类型检查通过；原地替换1个前端脚本，245项运行摘要核对通过，47项现场资料保留，CheckOnly零派发。真实新轮/完整流程仍待现场，T055/T056保留。见artifacts/installed-start-recovery-r8-20261009/修复及更新报告.txt。


## 2026-10-09 SC-021-PLC-R9

复位前清理9个PC动作请求并直接读回；未知旧MB2009仍阻断。32/32定向复位/恢复回归、最终4/4关键用例及脚本CheckOnly通过；四入口正式发布原地替换11项，245运行文件摘要匹配，47项现场资料不变。未执行现场复位/运动；普通动作MB6040清零及T055/T056现场项保留。报告：artifacts/installed-reset-clear-r9-20261009/修复及更新报告.txt。


## 2026-10-09 SC-021-PLC-R10

依据用户确认的现场轴语义0运动中/1到位，直线五轴和旋转轴完成后只清PC请求并新鲜读回0，保留PLC到位1；不同目标仍需本次Moving→Arrived及实际位置，同坐标使用本连接已核验证明和新读位置。翻转/放回/分拣保持独立握手，Legacy Test保持原清零协议。先前R9所列MB6040普通动作清零问题的软件修复已完成，真实设备执行仍待验证。

最终61/61定向回归通过（SiteOperationHandshakeTests、CommissioningRecoveryTests、SamePositionTests、HandshakeClosureTests）。初次新增检查失败源于夹具把持续请求再次视为运动中，修正夹具为上升沿；一次重跑失败源于证据SQLite文件重复，使用独立目录重跑全部通过，原失败记录保留。

四入口正式发布成功，原地替换9项、245项运行文件SHA256与本次发布基准匹配、47项现场配置/数据不变。启动/复位CheckOnly通过且零设备派发。同步5项源码/测试到D:\gaode，之前R6-R9产品改动保留；隔离副本与主工程的非发布辅助脚本/桌面测试差异保留，未整分支覆盖。未连接设备、未复位或运动、未解除旧任务、未提交推送。T055/T056保持现场阻断。

证据：artifacts/installed-axis-arrival-r10-20261009/修复及更新报告.txt。


## 2026-10-09 SC-021-PLC-R11 软件验证

61/61直接相关离线TCP回归通过，包含轴到位保持1、后续异坐标、新鲜同坐标证明、初始/复位/漂移后不冒充完成、请求释放失败与断线不自动重发、复位MB6015新读1与MB6052非0阻断、完整翻面放回及分拣周期清零。前端既有恢复启动8/8通过；前端按锁定依赖构建成功，没有新增前端改动。R轴在请求前启用采样，用写应答后的运动中证明避免旧到位冒充本次完成；未新增同角度沿用。

初次隔离副本测试夹具与现场基线不一致，已改用从当前现场源码HEAD建立的独立工作树；最初回归暴露旧测试依赖强造同坐标运动及R轴过早降采样，已纠正并完整重跑。原失败记录保留于本机artifacts/installed-axis-arrival-r11-20261009，不伪造通过。

软件证据：evidence/axis-arrival-r11-20261009/software-regression.trx及software-validation.json。现场部署及提交对应以本次独立安装记录为准；不据离线结果声明真机验收通过。


## 2026-10-09 SC-021-PLC-R12

用户确认safeZeroToleranceMm=0.2，正常PositionTolerance保持现场0.1。新增独立可选配置，复位/启动XYZ与恢复全部直线轴零位检查采用同一零位容差；旧配置省略则保持原精度，不影响配方运动/同坐标/采集窗口和R角度。PLC坐标为Float32，阈值按同一Float32精度比较，正负0.2边界均验证。

41/41定向复位及恢复回归通过；初次负边界暴露Float32/Double表示差异，已修复并在独立证据目录重跑，失败记录保留。正式四入口发布成功，原地替换11项（含机械配置最小合并），243项运行文件摘要匹配，48项保护文件不变（46项现场资料+2历史gitignore元文件），机械配置除safeZeroToleranceMm和来源追加外语义不变。启动/复位CheckOnly通过，实际配置读回零位0.2/运动0.1，未连接设备或复位/运动，软件保持关闭。源码同修复未提交，按用户最新指令不推送。T055/T056现场验证保留，T080推送仍等待用户后续安排。

报告及备份/回退：artifacts/installed-zero-tolerance-r12-20261009/修复及更新报告.txt。


### R13复位后初始检查诊断（2026-10-09，部分完成，未部署）
用户随后更正：扫码Z32毫米表示未复位。撤销尚未部署的32毫米允许位置配置和实现；保留具体初始阻断原因与日志修复。中间版本45/45（r13-b.trx）仅作历史证据，不作为最终修订通过证据；撤回后重新验证。首轮2个测试断言错误记录保留。不放宽未确认的复位后轴反馈0门禁，未更新安装、未发真实复位/运动。T082保持未完成，待反馈语义确认及构建部署核验。

最终撤回32毫米允许位置后的源码：SiteOperationHandshakeTests及CommissioningRecoveryTests 43/43通过，见r13-c.trx。扫码Z32仍被初始位置检查阻断，具体报ResetSafeZeroUnconfirmed；错误轴反馈另报ResetAxisFeedbackUnconfirmed。未更新部署，未操作真实设备。


2026-10-09用户重新授权源码提交推送。当前同步范围及已部署R12/未部署R13差异见source-sync-20261009-r13.md；此前“暂不推送”保留为历史记录。T080既有部署步骤已完成，本次补源码交付说明；T082部署及现场依赖仍未完成。
