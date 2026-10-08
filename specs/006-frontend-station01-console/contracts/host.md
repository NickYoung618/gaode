# WPF/WebView2 宿主合同

**012澄清交付时状态（历史）**：2026-10-03，012副本；已按统一确认定向修订本文件有效条款，历史证据及任务勾选保持原范围。仅文档同步，尚未合入主项目或证明实现/验证通过。

## 启动

1. 启动 WPF 进程。
2. 检查系统 WebView2 Runtime；缺失/版本不满足时显示宿主受限提示并停止页面启动。
3. 创建 `CoreWebView2Environment`。
4. 将安装目录下的静态资源根目录映射到固定虚拟主机 `https://appassets.local/`。
5. 导航至 `https://appassets.local/login.html`。

宿主不得导航到原型 ZIP、任意本地文件、外部网页或 CDN。资源加载失败必须显示宿主错误，不显示业务成功。

## 配置注入

宿主只注入：

- 后端 API 基地址；
- SignalR Hub 地址；
- 当前运行模式（Test/Simulation/Production 显示用途）；
- 资源版本和原型哈希。

宿主不得注入 PLC 地址、相机 SDK、算法端口、数据库连接串、设备命令或后端内部类型。

## 生命周期与安全边界

- 页面加载完成、导航失败、WebView2 崩溃、窗口关闭和宿主退出均可记录诊断。
- WebView2 页面只能通过 HTTP/SignalR 客户端访问业务；不提供设备控制 JS bridge、任意文件读取或数据库桥接。
- 生产包不允许外部页面导航和 CDN 资源；开发/测试若允许调试工具，必须与生产配置分离。
- Runtime 缺失、页面资源缺失和后端不可用是三个不同状态，分别显示宿主/资源/连接错误。

## 009 / AL04 当前共享接口（2026-10-01）

本节优先于此前冲突的公开字段、职责和当前完成声明；历史证据只适用于原构建，不改原任务勾选。具体实现及运行待009任务，不能用文档对齐代替交付。

009历史增量沿既有runtime.js/notification-reducer及正式动态绑定保留有效义务；当前另承接012授权配方弹窗及011真实阶段/异常槽号，代码未因文档同步而完成。原型ZIP SHA256=3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0，原只读核对覆盖当时页面；当前实现副本的012配方弹窗及新016顶部公共位置/异常弹窗按已授权映射调整，归档、无关结构/布局/文字/控件/导航保持保护，不新增页面。现有来源区域显示真实executionOrigin，不能硬写Test/Simulated；未知保留未知。新后端元数据不自动获得新页面区域。实现归006范围，由009 T046执行并留接口验收证据，006历史勾选不变；T046未实交前009 T061不得签完整基线。

宿主只注入既有后端地址/认证/运行显示/资源版本，不注入raw解释能力或数据库桥接。


## 021受控联调用途增量（2026-10-08）

本次仅定向更新021消费者合同，旧用途/历史证据保持原范围。独立规格与设计见[021规格](../../021-commissioning-console/spec.md)、[计划](../../021-commissioning-console/plan.md)、[任务](../../021-commissioning-console/tasks.md)。新用途为RealDeviceCommissioning，运行purpose为Commissioning。

预配置单身份由后台核权，Operator运行/ProcessEngineer编辑，不用Test令牌或客户端角色授权；identity GET、固定appassets.local来源、头认证和宿主内存凭据按[IH合同](../../021-commissioning-console/contracts/identity-host.md)。按主体/requestId只读启动查询、Final持久提交后同Run普通释放及只读start-admission按[SC合同](../../021-commissioning-console/contracts/start-and-completion.md)；未知不重发，保留原故障恢复。

七格显示已由用户确认C/D/A/B/E/3D/F；本新用途的对应问题关闭，旧Test临时映射证据不改。当前Run已提交图像/结果、未参与不补图和三页既有承载按[RM合同](../../021-commissioning-console/contracts/recipe-media-ui.md)，不改变采集工艺/硬件绑定或原型布局。
