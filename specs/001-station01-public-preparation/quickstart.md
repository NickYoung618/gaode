# 实现后的独立模拟运行与验证

**版本**：1.2.0　**日期**：2026-09-22　**状态**：操作设计；本轮公开接口补齐、受控媒体fixture和前端联调验证仍未完成，命令未执行。不可据此声称已经启动Host或通过测试。

M1的34项任务完成后仅可验证最小正常闭环及已有查询/基础控制信号；下列完整pause/cancel、取消竞争和恢复步骤须在T044/T048/T053/T054等M2依赖实现后执行，不能当作T031/T032或M1的前置通过条件。

本指南描述后端合同完成后的验证顺序。当前代码已有启动、查询、handoff、status和受控媒体读取，但暂停/取消/恢复核对/继续/配置校验、统一错误、版本化通知及媒体持久索引仍需先按[plan.md](plan.md)和[contracts/api.md](contracts/api.md)实现；不能用当前缺口直接开始正式前端联调。

## 1. 前置条件

以[plan.md](plan.md)及[research.md](research.md)实现四个后端工程与相应测试后，确认显式.NET SDK、锁定依赖及本地Test认证映射。全模拟不需要真实设备、厂商SDK、Python Worker、前端或虚拟PLC服务；算法端口可以模拟成功/失败或NotIntegrated。

选择项目内专用测试数据根，例如未来的artifacts/station01/dev-store；不得指向生产路径、其他项目或已有数据库。配置根选择本功能examples，加载public.test.json、budgets.test.json及一种simulation profile。确认各用途为Test、所有设备Simulated、模拟实体输入来自驱动器。

## 2. 受控准备开发测试库

1. 实现后的StorePreparation测试入口按[persistence-handoff合同§4](contracts/persistence-handoff.md)检查目标、Test标记及维护锁；正式Host必须尚未持有目标库。
2. 只在明确空测试根应用同一Infrastructure初始EF迁移、创建s01-store/1并输出StoreManifest/准备结果。已存在库先Inspect，不覆盖或重建；失败保留现场。
3. 校验版本、关键结构与媒体根后再启动Host。缺失/不兼容库的负面测试应确认Host不建库/改表。上述步骤是未来隔离测试准备，本次没有执行任何数据库操作。
4. 不通过Host启动的EnsureCreated/Migrate偷渡准备，不安装或实现完整维护平台。

## 3. 未来工程验证命令示意

下列路径对应plan的拟定工程，当前不存在；包restore需要后续开发环境实际满足，本次未安装：

```powershell
dotnet restore backend/src/Gaode.Host/Gaode.Host.csproj
dotnet build backend/src/Gaode.Host/Gaode.Host.csproj --no-restore
dotnet test backend/tests/Gaode.Rules.Tests/Gaode.Rules.Tests.csproj
dotnet test backend/tests/Gaode.Contracts.Tests/Gaode.Contracts.Tests.csproj
```

SQLite/媒体测试必须按上节由专用准备夹具建立测试库再运行。未来Integration.Tests用例按[verification.md](verification.md)分类；不把普通test命令隐式作用于生产数据。具体准备入口在实现时作为测试夹具，不是第二个正式Host。

## 4. 实际时间观察

1. 选择simulation.normal/1.0.0（RealElapsed）。通过未来Host.Composition显式配置FullSimulation、Test配置根/数据根、loopback监听和Test认证，不打开任何真实设备连接。
2. 先查询/api/v1/station01/status，查看存储/维护、模拟设备就绪及来源；算法状态单独列出，不是启动门槛。
3. 通过[API合同](contracts/api.md)POST runs，使用唯一requestId和测试context（工单TEST-WO、批次TEST-BATCH、scenario=Incoming）。配置引用为s01-public-dev/1.0.0及s01-budget-dev/1.0.0、s01-sim-normal/1.0.0。不要传产品配方或伪造Part/Face。
4. 返回CommandReceipt后查询run，观察配置保存及WaitingPhysicalStart；使用同进程开发集成驱动器显式输入模拟按钮。不得将POST自动当实体启动。
5. 在受理/夹紧/XY/采集/算法各延迟期间查询中间态，验证后继调用未提前发生。正常结束只读取handoff，看到来源Test/Simulated、配方Unmatched、质量NotEvaluated、分拣NotStarted。
6. 设备仍有物理占用，不以完成标记自动开始第二盘。各用例使用独立测试夹具/设备初态和独立数据根；重置模拟装载只在夹具停机且无在途引用后进行，不提供生产卸料或绕过重入的API。

## 4A. 前端联调最小验收

1. 使用四类显式Test令牌分别验证允许和拒绝的请求；不在请求体或Header中伪造角色。未授权请求应返回统一`ErrorContract`，并确认没有设备动作。
2. 先GET `status`和运行快照，再订阅SignalR；断开或跳过通知后用revision/ETag重查，确认通知不是动作确认，且任一公开状态变化都会更新ETag。
3. 在Test模式选择受控本地fixture或合成媒体，启动后只通过后端返回的`mediaId`读取媒体。结果必须标记`source=Simulated`、`purpose=Test`及contentType，不允许前端传本地路径或直接读取数据库/文件夹。
4. 对未接入真实相机/算法的环境，状态显示`NotIntegrated`/`NotReady`/`Unknown`/`DependencyFailed`等受限状态；不得显示虚假的真实成功。Production配置若无真实适配器必须拒绝或明确受限，不能静默回退模拟。

## 5. 可控时钟规则测试

绑定simulation.controlled-normal/1.0.0及同一FakeTimeProvider实例给期限调度和模拟端口，AutoAdvance=0。先受理请求、Drain已就绪事件，查询状态；不推进时间也应能受理pause/cancel并读取当前快照。

通过夹具推进到下一个定时时刻并Drain短事件链，逐阶段记录状态。所有响应仍来自端口，业务超时必须由DeadlineScheduler产生。对D-1、D、D+1的边界，恰好到期必须超时；不要一次跳过多个时刻导致响应的接收时间被错误归并。纯规则/端口保存替身可同步报告完成；真实媒体/数据库I/O用独立集成测试，不能由假时钟声称已完成。

## 6. 故障与恢复操作顺序

| 场景 | 选择与观察 |
| --- | --- |
| 算法超时 | algorithm-timeouts，先观察高度超时后安全F，再观察F超时带异常移交；推进/等待迟到窗口后终态与动作计数不变 |
| 不响应 | no-response，两个算法在真实期限结束；查询/心跳/取消不等待响应 |
| 运动超时 | motion-timeout，首个XY到期进入Unknown/Held，F未开始；取消受理不是停机；可靠停止及人工核对前不能continue |
| 重复/迟到 | duplicate-late，重复受理/算法结果仅留痕，不重拍/重动作/进下一阶段 |
| 配置错误 | 独立版本移除F必需点/参数，确认PLC启动及全部物理动作调用为0，原运行保留错误 |
| 保存失败 | 对快照、动作/Call意图、媒体/结果及移交注入失败；Call意图Failed/CommitUnknown不得Execute，保存中算法到期晚提交也不派发，不重置期限 |
| 取消与移交竞争 | M2按verification §6用真实SQLite提交/回执屏障覆盖五窗口；先关准入，未决applied=null，按WriteId/条件事务核对唯一Run/Handoff终态 |
| 中断恢复 | 停在可控故障点，保留Test根，重启进入RecoveryRequired；授权核对同盘/装载/原快照/设备安全，再显式continue，只复用有完整保存证据的步骤；Call派发未知不重算，完成/取消已提交只读，未决取消按WriteId核对不自动继续 |

按verification生成事件、状态、动作调用计数、文件/提交记录、配置版本和限制说明；未执行场景标NotRun，不能写通过。真实PLC/相机/光学/算法精度、生产环境和节拍仍需相应OPEN补齐后的单独验证。

## 7. 当前明确限制

- 本轮不实现前端页面、桌面壳或WebView2；前端功能由006独立规格按客户确认原型开发。
- 本轮不接入真实相机、真实算法Worker、正式账户体系或生产部署；模拟媒体不能作为真机效果证据。
- `/reset`、配方目录/计划/绑定和最新PLC协议继续由002/003管理，不能从001前端合同重复定义。


修订记录：2026-09-20，1.0.1，同步H01/H02验证步骤及M01里程碑前置；2026-09-22，1.2.0，增加公开接口缺口门禁、前端联调最小验收、受控测试媒体和真实适配限制。未执行上述命令或测试。
