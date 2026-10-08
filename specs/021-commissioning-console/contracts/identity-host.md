# IH-021：预配置身份与桌面承载合同

2026-10-08；拟实施合同，追溯FR-001–004/009/014/015、US1。实现前须完成tasks及共享同步；旧模式的已获批语义保持。

## IH-01 模式及配置

桌面新增明确mode=RealDeviceCommissioning，当前后端模式同名，运行context.purpose=Commissioning。联调只读取显式`GAODE_COMMISSIONING_PROFILE_PATH`对应的DesktopCommissioningProfile（字段见[data-model](../data-model.md)），不复用GAODE_TEST_*配置。profile从绝对本地路径读取，必须完整，无缺省凭据或默认管理员。维护人员给当前进程配置所选credentialEnvironmentVariable，profile文件不含秘密值。

一个桌面进程只绑定一个profile。工艺工程师编辑后关闭其桌面，再由维护人员以操作员profile启动；Host不因此停止或重发业务。无需新的角色切换按钮、账号库或配置管理器。

## IH-02 后台认证及公开身份

新增Commissioning认证分支，仅在Host的RealDeviceCommissioning模式读取`Gaode:CommissioningIdentities`记录；字段及唯一性见数据模型。用Authorization: Bearer验证当前选定credential，按后台记录形成非test主体、角色与permission，客户端不能声明或覆盖主体/权限/用途。联调分支不接受Gaode:Tokens旧Test值，旧用途不得接受新联调身份。既有非联调认证规则不在本轮扩张。

| 角色 | 本轮权限 |
| --- | --- |
| Operator | Run.Read、Run.Start、Run.Pause、Run.Cancel、Media.Read |
| ProcessEngineer | Run.Read、Config.Validate、Recipe.Write、Media.Read |

所有业务路由继续用既有后台策略授权；本功能不把L2解释为EquipmentEngineer，也不授Config.Write/Recovery等额外权限。新分支没有默认管理员分支。

新增`GET /api/v1/station01/identity`，要求Authenticated，返回ConfirmedIdentity。schemaVersion=`station01-identity/1`；联调authenticationSource=`PreconfiguredCommissioning`，purpose=`Commissioning`。无效/缺凭据401，身份有效但业务操作无权403；响应Cache-Control=no-store。GET不得创建业务运行、打开相机或下发任何PLC请求。

## IH-03 页面身份绑定

宿主仅对受控`https://appassets.local`页面注入选定凭据和非秘密配置到内存，页面立即移除全局配置引用。不得给外部导航/子帧提供凭据；既有外部导航/资源限制继续生效。前端闭包使用凭据发后台请求，不写storage、URL、console或文件。

login.html现有用户栏绑定后台displayName，L1对应Operator、L2对应ProcessEngineer；初始化可绑定后台已确认角色为选中状态。用户名/密码不发送作账户验证，不因非空授予权限；对用户输入/所选角色与确认身份不符，沿现有校验反馈机制拒绝进入。L3不能取得本功能权限。导航仍用现prototype.html别名，URL不携凭据、用户名或自授角色；运行页独立再查identity，不信任跳转参数。

不新增登录组件、隐藏后台管理员、密码管理或设备桥接。原型表单布局/文字保持；权限绑定允许控制既有控件可用性，不把原型演示值当真账号。

## IH-04 后台连接与通知

新用途显式允许固定来源`https://appassets.local`，CORS仅现业务所需GET/POST/PUT/DELETE及Authorization/Content-Type/If-Match/If-None-Match/X-Requested-With/X-SignalR-User-Agent，暴露ETag/Location；不启用任意来源或带凭据Cookie。Test来源设置只留旧Test路径。

沿现SignalR LongPolling与Authorization头；不改为query-token，服务端拒绝query凭据认证。通知仅触发正式GET重读，不成为完成事实。后台连接无效时不退回Test或离线演示模式。

## IH-05 诊断

沿DesktopRuntimeLog与后台RuntimeDiagnostics持久记录IdentityChecked/IdentityRejected/TemplateRejected/StartPosting/StartAccepted/AcceptanceUnknown/RunObserved/ManualConfirmation等事件，字段仅时间、模式、非秘密profile/主体、请求/Run/Tray/trace、结果/原因。不打印host config、headers、上下文全文、图像、环境变量值或原始query；遮蔽当前选定联调凭据，不能只遮GAODE_TEST_OPERATOR_TOKEN。DevTools/远程调试保持现模式限制，不为测试打开联调产品调试端口。

实施核对修正：操作员须能经现配方目录读取并选择已保存配方，以完成正式启动；可打开既有弹窗只读选择，不能新增/编辑/校验保存。工艺工程师仍有编辑权限。禁用整个配方按钮会同时阻断操作员选用，因此改为限制编辑控件；后台权限不扩大，GET仍Run.Read。V01旧“操作员按钮禁用”证据仅属旧范围，新只读选择另验。


2026-10-08 本机配置准备：沿已确认预配置身份，在独立安装data/private-identity生成本机随机凭据和两角色记录，目录ACL仅当前维护用户继承访问；秘密不进入包/源码/证据。非秘密桌面profile及版本化start模板放data/config，绑定安装中实际配方场景/槽和已有公共/预算/虚拟输入引用。维护脚本只将所选桌面凭据和后台身份记录装入当前进程环境，不启动Host/桌面、不连接设备；缺现场依据不生成已核定运动配置，不设默认管理员。
