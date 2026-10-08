# T061：新联调用途的独立前端规格交接

2026-10-08；本次仅完成缺口交接。020不新增页面实施任务，不修改frontend/desktop源码；独立前端spec/contracts/plan/tasks及页面验收尚待后续承接。本说明不是这些产物的替代品。

## 已核实的缺口及影响

| 位置 | 当前行为 | 独立规格需承接的边界 |
| --- | --- | --- |
| desktop/HostConfiguration.cs:16 | 只接受Test/Simulation/Production | 显式识别RealDeviceCommissioning及配置来源，不能把真机联调包装为Test |
| desktop/HostRuntime.cs:96–104 | 仅Test注入准备请求及testToken | 明确新用途的身份、权限、配置引用和准备请求交付；不复用Test令牌作为生产身份方案，不在页面暴露维护凭据 |
| frontend/src/runtime.js:5–6、95–104、597 | 仅Test取准备请求，legalPreparedRequest仅接受context.purpose=Test | 与后端RunPurpose.Commissioning一致；经后端API启动，展示准入拒绝和可关联Run状态；不删除后端用途门 |
| 既有配方选择与编辑入口 | 后端已支持校验保存、目录可用性、ETag编辑及冻结；本轮只取得API/执行器证据 | 对照原型核对必要参数在新增/编辑/重读/选择链中的实际绑定，不把API成功当页面完成；软件校验保存后可选择运行，不新增逐版本人工批准 |

正式后端模式/配置用途为RealDeviceCommissioning，运行上下文purpose为Commissioning。二者各有现有合同，不靠页面字符串替换绕过权限和准入。前端只经API和状态通知访问业务，不接触PLC地址、编码或握手。

## 后续独立规格的输入及验收要求

引用020的[spec](spec.md)、[RC配方合同](contracts/recipe-chain.md)、[MC混合运行合同](contracts/mixed-runtime.md)、[SP现场协议合同](contracts/site-plc-adaptation.md)，以及[012配方API](../012-recipe-authoring/contracts/recipe-authoring-api.md)、[012共享集成](../012-recipe-authoring/contracts/shared-integration.md)。先核查已有前端规格是否可承接；需要独立功能时再按项目流程选择目录，不在本轮初始化、升级或生成重复020。

已存在006前端合同资产：[宿主](../006-frontend-station01-console/contracts/host.md)、[原型映射](../006-frontend-station01-console/contracts/prototype-mapping.md)、[API](../006-frontend-station01-console/contracts/api.md)。宿主合同现模式同样仅列Test/Simulation/Production，后续必须与独立规格一起同步；当前006根部spec/plan/tasks不存在，不补造其历史。原型映射记录了012既有配方弹窗授权，后续应核对应基线，而非凭020另增控件。

独立spec/contracts/plan/tasks应先确定宿主配置与身份来源、API请求合同、错误/状态呈现和原型字段覆盖，再实施页面与桌面代码。若现有身份方式不足，先记录具体决定及阻断，不能自行指定现场密钥或新增未确认交互。

最小验收应覆盖：合法新用途经宿主/页面到正式Start；缺安全输入或PLC安全语义未明时呈现明确阻断并保持零相关运动；配方保存后可选、编辑不污染在途运行、下轮使用新值；最终人工确认的权限、终态/SQLite重读及状态通知一致。接口及页面离线证据与T055/T056现场证据分开，不把旧Test路线当新用途真机通过。

客户只读原型为`E:\dzk\gaode\原型.zip`（a.html、data-view.html、login.html）；SHA256：`3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0`。保持结构、布局、字段、文字、控件和流程；仅按独立规格绑定后端数据/权限/状态/错误。原型及全部frontend/desktop本轮未改，哈希保护见[Phase 8清单](evidence/phase8/source-state-final.json)。

T061完成只代表本交接已写入并可审阅；桌面/页面的新用途入口仍未实现，因此尚不能宣称正式桌面联调流程可交付。其余确定软件验证不依赖本交接的后续实施。
