# 021最小验证与部署指南

2026-10-08；夹具、真实桌面探针和部分定向验证已经完成，最新结果见validation.md。下列路线仍是验收要求，未执行项不能因包已制作而视为通过。本轮已获本机打包、独立安装及离线检查授权；不连接硬件，不提交推送。部署使用deployment-readme.md，默认入口仅本地配置检查。

## 前置及资料

feature.json指向021；spec、plan、research、data-model及三项接口/页面合同可读，后续tasks与analyze完成。固定归档摘要/原型精确差异核验；桌面验证需Windows交互会话及现WebView2 Runtime，缺Runtime明确Blocked，不改用浏览器证据冒充桌面通过。后台身份/接口必要验证通过后，不依赖桌面的配方、查询、采集/保存组件、构建及可用真实页面验证继续；分别记录页面与桌面子项，受阻任务整体不勾完成。

离线身份凭据由夹具随机生成到受控进程环境，联调身份和旧Test身份分别声明，不能写进日志/仓库。数据根为临时隔离SQLite/媒体；PLC只监听loopback，相机只用未启动/显式worker夹具，无真实SDK/设备。安全输入数值全部标OFFLINE，永不生成现场可用配置。

计划复用020的Inputs、SiteProtocolTcpFixture、ProtocolTcpFixture和真实SQLite/MediaStore组织方式，但真实页面的保存操作必须落到同一实际配方目录；不能用固定Catalog替代页面新保存内容。

## 必要构建与规则检查

后续实现完成后按实际改动执行，输出存021新的evidence目录：

```powershell
npm --prefix frontend run build
npm --prefix frontend run typecheck
node --test frontend/tests/core/*.test.mjs frontend/tests/commissioning-console/*.test.mjs
pwsh -File frontend/scripts/verify-prototype.ps1 -Output specs/021-commissioning-console/evidence/prototype.json
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --no-restore
dotnet build desktop/Gaode.Station01.Desktop.csproj --no-restore
```

package.json已修正为实际.mjs测试入口；已有前端7项及定向后端结果，完整范围见validation.md。member-gripper.test.mjs仍是受影响配方编辑的必要回归。原型证据输出不得覆盖历史文件。

## 最小代表路线

| ID | 操作及真实消费者 | 必须核对的事实 |
| --- | --- | --- |
| V01 身份/权限/桌面 | Windows STA探针引用公开HostRuntime.InitializeAsync创建真实WebView2，驱动既有login→prototype导航；分别用Operator/ProcessEngineer身份 | 身份GET与模式匹配；角色点击不提权；缺/无效凭据拒绝；无URL/持久存储/日志凭据，CORS只放固定来源；不打开联调调试端口 |
| V02 人工配方与编辑 | 真构建页面新增→检查保存→关闭重开→目录选择；在正式执行器A冻结后由真实页面改一处X和单张曝光B | 共同API与真实SQLite完整正文；A实际Move/Capture旧值、后续B新值；版本/摘要/媒体身份；仅声明端口/离线来源，无真机成功声明 |
| V03 受理未知 | 夹具让一次启动POST已受理但页面取不到回执，再重开/恢复 | 页面保留原requestId，GET/start-requests命中同Subject/Run；404仍未知，不自动新ID或重复POST；数据库重开命中已提交命令 |
| V04 新用途失败闭环 | 正式RealDeviceCommissioning DI及Start，经新页面先缺受控输入，再齐结构但PLC-Q3/Q4未明 | 相同Run持久原因，零相关运动；心跳允许，七worker未启动；不伪造SafetyClear，不把旧Test正常链算现场链 |
| V05 正常人工终点/下一轮 | 明确Legacy Test布局、有效Test配方及正式Host流程到等待人工，真实页面确认后重读Final；再显式新启动 | 同Run/Tray真实SQLite Final/来源，保存先于owner释放；新请求新关联，旧确认重放不能释放新owner，保存失败/未知不释放；原故障恢复路径定向回归 |
| V06 图像与数据页 | 专属Commissioning离线Run，实际隔离采集→共同媒体保存/提交→正式受认证目录/媒体/Run查询→新用途真实构建页面；独立于V05旧Test Run | 用户确认七格C/D/A/B/E/3D/F；未参与不补图，数据页无mock/随机ID/本地质量判定；重开读实际来源/媒体/结果 |

V06最小输入由T004夹具建立，保持context.purpose=Commissioning，运行及媒体关联沿正式应用/持久入口；隔离采集端口实际返回图像，共同服务保存文件与提交记录，页面消费实际GET而非伪造响应。证据记录Run/Capture/Media/提交版本、role/businessCamera及明确OFFLINE来源。用具备相应角色的组件采集记录核七格对应、一次同相机多帧、第二Run防串图及一次未参与留空；这是媒体组件验证，不更改正常工艺来强制E采集，不绕过运动门或新增产品采集API，不伪造Run完成/质量结果。旧Test Final重读可作数据页补充，不能作为新用途映射证明。组件缺少实际事实时保留未验，不用临时图补齐。

正常旧Test链用已声明/有效的Test来源，不能为方便把联调新配方改用途或伪批准。V02可用新用途保存/Freeze/正式执行器组件路线取得真实参数消费；V04证明新用途入口失败闭环。二者不拼接成正常新用途整机通过。

后续拟建Gaode.CommissioningUiFixture与Gaode.Desktop.CommissioningProbe分别负责离线Host/数据和真实Windows宿主观测；探针只操作持有的WebView2实例，用现ExecuteScriptAsync及DOM事件，不新增产品设备控制桥接。优先复用仓库现Playwright范式进行页面驱动，不新增生产依赖。具体启动/过滤命令随tasks所列实际夹具入口确定，当前不给未实现的硬件启动命令。

## 证据及退出条件

021 evidence应记录命令/非零匹配计数、构建结果、实际DOM操作、身份/用途、模板及资源摘要、请求/Run/Tray、配方A/B、实际动作/采集请求、Final与SQLite重读、持久日志和源码文件摘要。不得留凭据或以临时目录路径当交付数据。失败记录保留，不能覆盖020历史证据。

V01–V06按子项实际执行且范围注明后才可收口对应软件任务；Runtime/会话缺失只阻断对应桌面子项，其余确定工作按tasks修正后的依赖继续，整体缺证据不得计通过；硬件/Runtime未验据实保留。020 T055/T056、公共Z限制及历史SDK/机械未验不因本功能软件通过关闭。下一步tasks，随后analyze，再implement；计划本身不授予打包/现场运行权限。


## V07/V08：用户新增配置要求（待实施，不能计Passed）

依据[CC合同](contracts/configurable-commissioning.md)，T033–T037完成共享设计后：

- V07：两套实际步骤不同、来自正式模型的配方，经实际页面新增/编辑、校验保存重读选择，用对应版本受控OFFLINE虚拟输入执行，核Move/Capture/算法请求和真实SQLite/媒体；不只更改名称、不使用生产设备或未确认现场值。
- V08：实际页面勾选“虚拟光源”，不填光源参数仍通过相机校验及采集，记录灯调用为0和跳过来源；曝光/增益仍实际应用。取消勾选而缺真实装配/配置明确阻断；声明OFFLINE的端口替身仅核真实模式调用顺序。编辑保存模式后，旧Run保持旧冻结，新Run使用新选择。公共3D/F独立声明虚拟模式，不依赖尚未绑定的配方。

沿既有离线夹具及WebView2探针扩展，不扩全组合；证据分别输出evidence/v07-configurable-recipes.json、v08-light-mode.json。旧V01–V06不覆盖本增量。

## 2026-10-08 PLC确认规则源码落实与补丁验证

本轮用户确认MB2007在第一条实际运动完整闭环后清0；MB2008软停及心跳断线都停住、不自动回位。复位按MB2009上升沿、本次MB6015先0后1、检查新鲜XYZ零位后清请求。协议/地址来源原件不修改；新约定在SC-021-PLC-R2明确裁决旧软停自动回位说明。PcStartCmd新增为现场MB2007 BOOL高字节，旧Test地址表不加点。正式机械配置增加siteOperations（plc-site-operations/1及确认来源）；未提供时仍安全Unconfirmed。已提供时按已确认点表读取报警/独立安全点/光栅，不凭就绪覆盖报警，不写PLC报警。

实现修改包括：现场复位不再立即清请求，等待本次Ready下降及上升；启动前读新鲜XYZ与零位比较，不自动回零；启动发MB2007上升沿，首个实际Move的XY及适用Z全部完成、位置复核与双方清零后清启动。纯同坐标复用不算首次实际运动。另纠正旧ObserveAxisClosures对现场布局无条件撤销资格及引用缺失ManualZoneOccupied的问题；按已确认现场安全点检查保留闭环资格，旧Test逻辑保留。软停仅一次派发MB2008，不发自动回位/复位，之后PhysicalStopUnconfirmed并阻断自动续发；不把软件发出命令当作物理停稳证明。完整恢复的持料/夹爪核定未自动放行。

测试：plc-sequence-final.trx为35/35通过、0跳过（8项新现场握手/零位/软停/报警验证，3项既有现场协议验证，24项清零/同坐标回归）。最终现场原始通信审计、运行日志及实际SQLite位于evidence/plc-sequence-final-20261008；旧Test回归实证位于plc-sequence-legacy-final-20261008。r2/r3/r4失败与r5定向通过保留，前两次发现现场资格无条件撤销，随后暴露缺失旧ManualZoneOccupied依赖并修复；未隐藏失败。所有测试仅loopback，不连接设备，不能当作T055/T056现场通过。

T046/T047完成。下一步制作源版本关联补丁并实际应用/安装校验；原final-2包/安装不覆盖。当前完整运行profile尚未装配，这属于配置工作，不再把本次已确认的PLC含义/轴范围重复列为业务待澄清。现场流程、实际SDK动作及完整桌面正常两轮仍按历史未验证范围保留；requirements只读，无Git提交推送。

2026-10-08 本机final-3已完成runtime-r1装配，入口检查ConfiguredNotFieldValidated。安装目录D:/Gaode-Station01/commissioning-021-final-3；Start-Station01.ps1不带参数只检查，启动联调.cmd才实际启动并连接设备（本轮未执行）。入口先载入本机操作员秘密，不手填令牌；配置在data/config/runtime-r1，配方仍从data/recipes/recipes.db读取。详细参数来源与实际验证见validation.md T050及evidence/runtime-config-20261008。原包/补丁没有被重制，现场完整流程未通过。


2026-10-08 R3：安装D:/Gaode-Station01/commissioning-021-final-3的更新待应用。先使用“一键关闭.cmd”，再“应用复位修正.cmd”；脚本有运行进程门和基线SHA核对，备份data/hotfix-backups，不启动软件或PLC。软件复位新顺序为PC就绪/取消软停及读回→PLC复位握手；有无软停均支持。PLC复位成功仍须旧任务受控恢复核验，当前此入口缺口为T056；不要直接重复启动或删除运行库。

## 2026-10-08 R4：完整复位恢复流程（替代上述R3当前缺口说明）
用户已明确确认PLC系统复位会全部恢复：零件放回、夹爪松开、翻转机构回到初始状态。已新增来源绑定的restoresWorkpieceAndMechanisms配置，未加载该确认的配置仍不放行。正式/reset要求旧执行退出、采集/算法/媒体资源释放，PC先就绪并取消软停、新鲜读回后发复位上升沿，观察本次Ready先0再1，再核验安全、五根直线轴零位及相关请求/反馈清零。SQLite提交旧Run取消及恢复证据成功后才释放所有权；保留旧历史，不伪造检测完成，不续跑旧动作。
原故障通知处提供“复位并结束旧任务”；一键复位也走同一后端入口。成功后刷新页面、核对配方，由人员手动启动完整新一轮，新请求关联旧Run及恢复提交ID。Host重启后从真实SQLite恢复旧任务占用；已核验取消的任务不会再次恢复为在途任务。
软件验证：recovery-r4-verified.trx 40/40通过，包含8项恢复验证（正式Host三实例启动/SQLite重读、旧任务取消及新轮引用、资源未退出零复位派发、取消提交冲突不释放、旧翻转反馈不清零阻断、无来源配置拒绝）和32项现场协议/清零回归；此前final回归40通过/1失败如实保留。失败定位为清零等待连续强制新读造成8192条通信证据环覆盖；修复为首次立即读、后续按既有启用组周期采样，新鲜读回及原动作期限不变，再回归40/40。旧Test完整流程在recovery-r4-final.trx中单独通过，实证在recovery-r4-20261008-legacy。前端start-recovery组件3/3、构建和客户原型精确差异校验通过；未宣称本轮实际桌面交互或真机流程通过。
交付方式：更新原artifacts/gaode-commissioning-console-021-final-4目录和同名ZIP，不新增编号；旧ZIP/清单/摘要及R3待应用补丁备份到artifacts/rollback-final4-r3-before-recovery-r4。同步原安装updates/reset-r3待应用补丁，根目录“应用复位修正.cmd”沿用。当前运行安装尚未覆盖：先一键关闭，再应用复位修正，再启动软件；应用更新本身不复位PLC、不发运动。完整包不含本机秘密或运行库，本安装机械配置随定向补丁更新，避免覆盖其他站点配置。
限制：上次MB2009若仍为1，仍拒绝重新发上升沿，须核定旧复位结果；不能自动清0重试。现场完整流程及020 T055/T056继续未验证，不以离线通过替代。requirements只读、无硬件连接/复位/运动、无Git提交推送。最终文件校验及ZIP摘要见evidence/deployment-final-4-20261008/package-audit-r4.json。

R4交付核对：T056/T058/T059软件任务完成，原final-4完整目录和同名ZIP已实际逐项校验；36文件补丁已在隔离目录完成备份/应用/哈希复核，并同步原安装待应用目录。现场运行文件未覆盖，T055安装实际应用/桌面现场验证及020 T055/T056保留未验证。最终摘要见package-audit-r4.json。

2026-10-08 R4安装及GitHub交付状态更新：已按用户授权把R4完整包解压更新至原D:/Gaode-Station01/commissioning-021-final-3，36文件定向补丁亦已应用；964项安装文件校验通过，原配方库及运行库哈希保持不变，账号配置保留，备份位于data/deployment-backups/before-r4-20261008-160556及data/hotfix-backups。此前“补丁待应用/未覆盖本安装”属于该时刻历史记录，当前已由本段替代。T055的软件分发/安装子项已完成，仅实际桌面恢复交互与真机流程未验证，整体不借此勾选。用户后续启动后17:07只读状态为PlcHeartbeatLost、旧Run仍RecoveryRequired/PhysicalRunHeld；本次交付不证明已复位恢复或运行放行。部署包产品源码348文件与待推送源码完全一致，无新增产品改动，不需要再打二进制补丁。版本对应通过ZIP旁source-version.json及安装data/git-source-version.json记录；保留原封包sourceHead/dirtySource构建事实，不倒改历史清单。详见github-delivery-20261008.md。
