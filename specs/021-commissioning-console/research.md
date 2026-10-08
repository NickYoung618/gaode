# 021技术决策与现状核查

2026-10-08；输入为spec、宪章9.0.0、AGENTS、020交接和实际源码。本轮只做设计；下述新增接口/修复均未实施。按plan技能分派身份和入口/验证两项只读研究，汇总如下。

## R01：单桌面进程绑定一个受控身份

**决定**：维护配置选择Operator或ProcessEngineer的一份联调身份，凭据从该进程环境变量读取，配置文件只记录变量名称。后台使用独立联调身份记录核定主体/角色/用途，页面只拿当前身份的凭据在内存使用；不加载一组可随意切换的凭据。换身份关闭当前桌面，由维护人员用另一配置重开；后端可保持运行，页面关闭不等于取消业务。

**依据**：用户确认预配置身份；Station01Authorization/TestAuthenticationHandler当前只有角色令牌且主体为test:<role>，HostRuntime仅注入Test令牌，登录页仅跳转。现有角色权限已足够本轮运行/编辑。

**替代**：直接扩展Test令牌会混淆真实联调来源；默认管理员绕过权限；新账号库/密码认证/热切换不属于本轮。均不采用。

## R02：身份查询、固定来源和既有长轮询

**决定**：新增只读identity接口，后台返回确认的主体/角色/权限/用途；运行页每次导航重新核验。联调只允许固定appassets.local桌面来源；保持现有SignalR LongPolling+Authorization头，拒绝query-token认证。凭据不进URL、持久存储、日志或证据。

**依据**：Program当前只为VirtualPlcIntegration注册页面CORS，新用途未贯通；runtime.js已显式LongPolling；DesktopRuntimeLog只遮蔽Test令牌，需承接选定联调凭据。这里只复用已有通信形式，不升级依赖或引入认证平台。

**替代**：放开任意跨源、采用URL凭据、靠登录角色chips鉴权均不满足FR-002/004。Cookie/设备桥接不必要。

## R03：启动模板与每次显式请求分离

**决定**：联调宿主读取有版本/来源且匹配当前用途的启动模板。模板提供station/line/scenario、配置引用及已确认槽映射；requestId和逻辑trayId在一次新的显式点击时产生，选择引用取当时真实目录。发送前冻结本次请求，持久保存非敏感关联标识；未知时不再生成新ID。正常下一轮必须先取得后台普通下一轮准入事实，再显式点击。

**依据**：StartPublicPreparation区分公共配置启动冻结与F绑定后配方冻结；逻辑trayId是运行关联，不等于硬件料盘码，F码仍实际绑定。runtime.js当前固定prepared.requestId、runId/startSubmitted锁不能支持普通第二轮。

**替代**：每次刷新自动生成并提交新请求、复制上一运行的运动/安全状态、把模板当PLC已就绪都不采用。模板过期或配置/槽映射变化须由维护准备新版本；不能在页面补现场值。

## R04：丢失回执的只读查询

**决定**：新增按后台认证主体+requestId查询启动回执的接口，读取活跃CommandRegistry与实际SQLite Commands；不要求客户端重发完整请求。结果不存在仅表示没有可证明的受理记录，保持未知，不作为重发许可。

**依据**：现有commands/{commandId}只在已知commandId时可用；runtime.js不能凭status.currentRun证明原请求归属。CommandRegistry已有(Subject,RequestId)字典，Commands已有SubjectId/RequestId，无需增加恢复数据库或新表。

**替代**：重发POST探测受理、把任何当前运行接给原请求、404后自动新建请求，均违反FR-011。不扩展通用恢复平台。

## R05：最终保存后释放普通下一轮占用

**决定**：在业务层核实同Run/Tray的最终人工确认及FinalUnloadCompleted已持久提交后，幂等释放该Run的普通physical owner；保留故障恢复owner。向页面提供软件准入状态Available/Held/Unknown，明确Available仅表示无软件占用，下一轮仍经过原配置、PLC和输入检查。

**依据**：CommandRegistry只在Register占用、ReleaseForFaultRestart释放，没有正常终态释放；RunEndpoints人工确认后只改Completed，单纯清前端锁仍会PhysicalRunHeld。这是SC-003后续运行必需的最小修复。

**限制**：检测完成、等待人工、失败/未知、页面关闭均不释放。旧运行确认的重复查询/回执不能释放后来运行的owner；不把任意Terminal状态当准入。原故障reset/check/restartFrom不变，不增加PLC复位写入。

## R06：真实构建入口与原型保护

**决定**：实际修改范围以frontend/src/pages三页、runtime.js、recipe-authoring.js、public-tray-flow.js和desktop为准；辅助TS模型按实际消费者同步。复用build.mjs的prototype.html别名，不另建页面。原型核验保留精确差异门，新增021业务绑定列具名授权资源/差异。

**依据**：build.mjs复制pages而非src根部同名原型副本；TS辅助文件未进入运行资源。verify-prototype.ps1和recipe-authoring-012-differences.json已精确核查归档、允许替换、构建页及资源摘要。旧012原型附件缺失，不能补造已核验的附件。

**替代**：仅改TS辅助层、另造页面、关闭原型检查或把原型中演示状态当运行事实，均不采用。

## R07：七格显示映射与媒体身份

**决定**：用户本轮确认C/D/A/B/E/3D/F顺序。按本次Run、实际BusinessCamera及CaptureId关联已提交媒体；同角色多帧沿现有切换交互，默认选最新已提交可读帧。未参与角色不补图。

**依据**：006 prototype-mapping此前明确映射未确认；runtime.js非Test直接返回空媒体候选，Test用运行散列分配临时格位。协议§3.1规定AB检孔/CD检环/F料盘码/E零件码，但显示顺序来自用户此次答复，不由协议推断。

**替代**：把Test散列搬到新用途、按字母猜顺序、拿独立采集/旧盘图填满七格均不采用。

## R08：字段归属及最小离线验收

**决定**：012已有三步/10×10/坐标/曝光/抓手等控件继续用共同正文及服务。P01–P10逐项对账，现场ModelNumber由机械姿态程序按Model+Profile+Pose匹配，不把PlcRecipeId显示编号当REAL码；新建PlcRecipeId=null不是现场型号缺省。缺完整来源/采集profile给明确字段错误，不静默丢编辑。

**依据**：RecipeEndpoints.Authoring新建强制PlcRecipeId=null，PlcPoseProgram.ModelNumber单独声明现场REAL值；共同Planner仅保留显示编号。需要核查实际MotionProfile与机械映射，而不是给页面新增PLC原码输入。

**验证选择**：受控联调身份/页面/API保存、正式执行器A/B实际参数请求、新用途真实装配失败闭环、旧Test布局正常Host人工确认/两轮、真实WPF/WebView2入口烟测分别留证。旧Test正常链不冒称新用途正常现场链；不为通过构造SafetyClear。已有020未受影响证据复用范围；不重跑全部历史。

**工具选择**：已有Node测试与Playwright范式用于页面；一个Windows STA探针引用现公开HostRuntime.InitializeAsync和WebView2实例，用ExecuteScriptAsync驱动真实页面。无需给联调用途打开远程调试，也不新增产品测试旁路。测试夹具只使用loopback和隔离SQLite/媒体，不提供现场可用配置。

所有本轮技术问题已明确决策。现场DEP-021-01/02及旧资产限制不变；尚未得到运行证据，设计符合不能计为验收通过。
