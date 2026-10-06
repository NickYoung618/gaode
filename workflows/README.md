# 高德后端开发Workflow

**版本**：1.2.0　**更新日期**：2026-09-21

使用现有Spec Kit 1.0.5.dev0的Workflow引擎，Codex CLI 0.155.1执行prompt步骤，
PowerShell 7及Spec Kit自带Python环境执行固定脚本。不安装工具，不覆盖现有宪章/
模板/spec/plan/tasks，不注册全局技能，不新增自动聊天触发器。

## 当前项目怎样接续

当前M1接续运行曾在命令执行器启动时失败，基础设施修复记录见下方。
**先确认其他对话在检查点停止写入，再启动这里的Workflow。**入口无法接管活跃聊天线程。
-WorkspaceIdle是操作者对此的明确确认；项目锁只互斥本入口，不能检测或停止其他对话。

## 多人/多需求并行

同一个物理工作区仍然禁止并行写入。`runner.lock`、Spec Kit运行状态、源码、`bin/obj`
和测试根目录都是工作区级资源；删除锁或抢锁会造成阶段报告、源码基线和测试数据串线。
使用`dev-parallel.ps1`为每个需求建立独立副本后再运行。它不会覆盖已有目录，复制时排除旧的
Workflow运行状态、artifacts、构建输出和`.git`，每个副本拥有自己的锁、请求、日志、构建和测试目录。

当前目录不是Git工作树，因此先使用完整副本隔离；以后接入Git后可将同一入口替换为Git worktree。
并行模式必须显式给出唯一Feature，避免不同副本自动分配相同Spec编号：

~~~powershell
# 两个用户/会话可以同时执行，目录和状态互不共享
.\dev-parallel.ps1 -WorkspaceName alice-003 -Feature 003-camera-reconnect `
  -Requirement "增加相机断线自动重连" -WorkspaceParent E:\dzk\gaode-1-workspaces -SourceIdle
.\dev-parallel.ps1 -WorkspaceName bob-004 -Feature 004-result-export `
  -Requirement "增加检测结果导出" -WorkspaceParent E:\dzk\gaode-1-workspaces -SourceIdle

# 先验证副本配置；不会调用模型
.\dev-parallel.ps1 -WorkspaceName alice-003 -Reuse -Check

# 真实隔离链路探针；会调用Codex，但不进入业务开发阶段
.\dev-parallel.ps1 -WorkspaceName alice-003 -Reuse -Doctor
~~~

并行不等于无限制地同时调用模型。服务器容量有限时，建议同时运行不超过1～2个Codex阶段；
模型容量、429和网络问题仍由各自副本独立记录和恢复。最后合并必须串行，合并后的主工作区要再次运行Verify。

并行入口的Verify席位是服务器级单席位：不同工作区的AI分析/实现可以并行，但完整的Restore、Build和
Integration测试默认排队，避免多个测试主机争用机器级计时器、文件观察器和CPU。默认最多等待600秒，
可用`-WaitSeconds 0`显式改为立即失败。测试失败不自动重跑、不自动篡改结果；失败证据交给Workflow的
Fix/Review流程处理，避免把不确定的时序问题伪装成通过。

~~~powershell
# 配置检查，无模型调用、构建、产品测试或数据库操作
.\dev.ps1 -Check

# 实际执行链路探针：调用Codex，只读写隔离诊断文件，不进入业务阶段
.\dev.ps1 -Doctor

# 当前第一工位接续M1，跳过Specify/Plan/Tasks
.\dev.ps1 -Mode Continue -Milestone M1 -WorkspaceIdle

# M1完成并停止后接续M2；M1未全勾选会拒绝进入
.\dev.ps1 -Mode Continue -Milestone M2 -WorkspaceIdle

# 未来新需求，显式给目录名便于识别；省略Feature会分配新编号
.\dev.ps1 "增加历史检测结果导出功能，仅后端" -Feature 002-history-export -WorkspaceIdle

# 查询/恢复官方引擎运行；run_id来自运行输出/Status
.\dev.ps1 -Status
.\dev.ps1 -Resume <run_id> -WorkspaceIdle
~~~

以上start/resume命令是真正开发入口，之后会调用Codex并运行开发测试；本次排障没有执行。
不需要逐条输入斜杠命令。Codex登录状态和模型沿用本机配置；实际模型及执行/报告链路已由隔离探针验证。
执行设置workspace-write，不自动扩大权限；无Git用skip-git-repo-check，不初始化Git。

## SSH / Session 0执行修复

本机Windows Server 2019中，SSH Session 0启动的Codex 0.155.1命令helper在USER32初始化时
退出0xC0000142，随后外层报runner pipe-in超时。独立Codex和SSH直接调用均可复现。
同一用户已登录的Session 2中，同一CLI与workspace-write/elevated设置可执行同一命令。

prompt-stage.ps1检测Session 0时，通过内置Task Scheduler临时任务在**同一用户**的已登录会话中
启动阶段Worker。使用InteractiveToken、LUA、隐藏窗口，不存储密码、不提升权限、不开放现有桌面ACL，
不修改Codex全局配置。普通用户会话直接执行。临时任务在结果确认后清理；不会启动第二个业务Workflow。

**环境依赖：同一用户必须保持Windows登录，会话可以断开，但不能注销。**
SSH入口本身不能创建这样的交互登录。无已登录会话、Task Scheduler不可用或Worker未启动时，
明确失败，不回退为全权限执行，不自动重开业务运行。未来重启/注销后须先恢复该登录条件，
本次没有配置自动登录或修改系统服务。使用-Doctor验证当前条件。

阶段Worker回执带nonce；排队期间没有进程不表示完成。编码在启动Python前固定为UTF-8。
`GAODE_WORKFLOW_REQUEST`始终指向`request.json`文件；桥接从其父目录读取`control.json`，
清单以`request_file`原样传给Worker，不把文件路径改成目录。Worker回执与Task Scheduler的
最终退出状态最多等待10秒收敛，不能凭回执文件提前判定进程结束。
Codex通过JSON Schema输出报告，外层先检查身份、状态及证据，再原子发布；不覆盖原报告。
执行器实际故障会提前失败；读取历史故障日志不会误判为当前故障。6900秒阶段上限保留，
隔离探针180秒；每次Codex由独立kill-on-close Job约束，异常时关闭本次子进程，不操作其他会话。

1.0.1的Doctor只覆盖ProbeFolder分支，曾遗漏真实请求路径，导致业务运行a71b9236在analyze启动前失败。
1.0.2的Doctor先在隔离目录中验证真实dispatch/阶段桥接/Worker请求文件传递，再运行模型探针。
前一部分仅将阶段接收器替换为诊断程序，不执行真实业务analyze；日志分别标识两种验证的范围。

## 流程及完成含义

新需求：Specify → Plan → Tasks → Analyze → Implement → Verify → 收敛/Review。
已有功能：读取已有基线 → Analyze → Implement → Verify → 收敛/Review。
验证或审查发现可修问题：Fix → Verify → 收敛/Review；**最多两轮Fix、三轮总验证**。
需要业务澄清、权限或外部输入时停止，不假定同意，不恢复范围外业务。
只有实际验证通过、审查pass、所选任务全完成且构建后源码未变才生成completion.json。

M1只包含34项最小正常闭环任务；M2才覆盖全部67项的软件义务。
OPEN、真实设备/算法/光学/节拍证据另列，不能用Done宣称真机可用。
最后的代码审查由顺序执行的Codex步骤进行，仍建议人工查看报告和修改。

## 引擎与集成方式

auto-dev.yml使用官方shell、if、do-while步骤。
阶段shell包装调用原生Codex exe，读取stages.md和当前项目模板；执行日志留在请求目录的
execution-logs/，结构化最终回复经身份、nonce、证据路径校验后由外层保存到reports/。
命令工具连接故障、Codex退出异常或报告缺失均使阶段失败；CLI退出0不表示阶段通过。
**没有安装/调用官方speckit命令技能，不能把这些阶段当作已注册斜杠命令。**
这使已有定制文档可以直接使用，无需在正在开发的目录执行specify init --force。
当前不支持Feature/Bug/Fast自动分类、GitHub Issue触发、提交/推送或PR发布。

### 本地资料读取策略

项目资料不预先拼接进模型提示词。阶段只接收当前`request.json`、`control.json`和必要路径，Codex在
workspace中通过命令工具按需查找：先用`rg`/`Select-String`定位相关章节、符号或需求ID，再读取命中行附近。
阶段规则禁止对大型规格、源码树或日志执行无筛选的`Get-Content -Raw`、全目录输出或宽范围`rg`；完整证据留在本地
请求日志/项目文件中，阶段报告只引用路径。这样可保持本地文件完整性，同时避免无关资料占用上下文和拖慢最终收敛。

统一入口使用本机已安装specify-cli环境，定位方式为uv tool dir。
未固定个人绝对路径；工具未找到会明确停止，不自动下载。
模型不写死、不修改用户Codex配置；开发步骤读取保存的CLI认证。
需求通过JSON文件传递，不拼接进入shell命令；YAML中的shell命令全部为固定字符串。

## 保存与恢复

- .specify/workflows/requests/<request_id>/request.json：本次需求、功能及里程碑。
- 同目录control.json：阶段nonce、所选任务、基线摘要、验证及修复次数。
- 同目录reports/：每阶段独立报告；过期nonce或假证据路径会拒绝。
- .specify/workflows/runs/<run_id>/：官方引擎状态、输入及日志。
- artifacts/workflow/<request_id>/：实际构建/测试日志、TRX和明确Test根。

恢复必须用dev.ps1 -Resume，以恢复同一请求上下文、权限与项目锁。
直接运行specify workflow run/resume缺少本项目上下文，步骤会拒绝。
本机引擎的嵌套分支/循环恢复可能重新进入父步骤，不保证停在同一子步骤；
阶段应检查已有产物而非盲重做，累计修复次数不会因resume清零。
超时/中断后先确认旧Codex、构建/测试进程已退出，再声明WorkspaceIdle。
状态仍running时入口拒绝resume；不自动改写官方状态或终止其他进程。
dc8137de、2cc35d91、a71b9236保持原failed状态和原配置摘要；本次执行包装已变化，不能将其冒充为旧配置恢复。
后续获授权时使用修复后配置新建Continue请求并关联旧证据，不修改旧运行来消除摘要冲突。

遇关键澄清停止时，可由用户在原需求对话确认，再把确认结果写入请求目录answers.md，
之后恢复。若必须改已冻结spec/plan/任务定义，应先显式修订并复核，
再以Continue发起新请求；旧请求不会偷偷接受新基线。
耗尽两轮仍失败时查看报告后决定，不自动重开无限修复。

## Verify具体做什么

固定运行backend/Gaode.slnx的restore/build及Rules、Contracts、Integration三个测试工程。
每套必须有新TRX，测试总数>0且全部执行/通过；NoTests、跳过、失败、命令超时都不是通过。
源码摘要覆盖backend源码/项目配置（排除bin/obj），最终审查后须保持同一构建依据。
每轮创建新的artifacts/workflow专用Test目录并传入GAODE_TEST_ROOT；
**这不证明产品夹具已支持该环境变量**，实现与审查步骤须核对真实使用路径。
首次使用前须确保原测试夹具确实按隔离Test根准备SQLite/媒体；
Workflow不执行SQL、不替Host建库、不删除或覆盖任何已有库。
新需求若需要其他测试工程或命令，应单独明确修改Verify策略，不接受模型输出任意shell命令。

也可以不启动Spec Kit，直接运行统一入口：

~~~powershell
pwsh -NoProfile -File .\scripts\verify.ps1
~~~

它只返回两个结果：0 / `PASS`，或1 / `FAIL`。结果和每条命令日志保存在
`artifacts/workflow/standalone-<id>/`；这是与业务Workflow相同的验证引擎，不会修改
第一工位规格、任务或源代码。Workflow内部在验证失败时读取同一份结果，再进入最多两轮Fix，
而不是因测试非零退出直接把调度器误判为崩溃。

## 报告与临时服务故障策略

阶段报告现在要求固定字段：`request_id`、`phase`、`nonce`、非空`summary`、真实文件路径
`evidence`和`issues`；未知字段、说明句、绝对路径、不存在路径和假证据都会拒绝发布。
Doctor探针的evidence由Schema限制为实际的`tool-proof.json`路径。

服务容量、429和网络瞬断与业务Fix分开计数。首次调用后同一阶段默认最多重试三次（总计最多四次），退避为30/60/120秒并带
20%抖动，且受阶段总时限约束。尚未启动工具时重试原调用；工具已经启动时不重放原提示，而使用已记录的
Codex session id执行`codex exec resume`，从上一个工具边界继续。没有session、未完成工具调用或事件状态未知则失败并保留
证据，避免重复写规格或代码。额度耗尽不自动重试，记录`paused_retryable`和可用时间，人工确认服务恢复后再Resume。
重试耗尽同样暂停；`dev.ps1 -Status`显示分类、session和下一步。不会自动换模型、扩大权限或无限循环。每次执行的日志有单文件、单请求和磁盘余量上限，
超限先停止本次子进程并保留限额内证据。若模型只输出了可识别的报告说明句，外层可进行一次确定性的“报告校正”，
只移除说明句并再次验证文件、身份、状态和问题；不调用第二个模型、不调用工具、不修改业务文件。
原始报告、校正结果和日志均保留。

如服务器容量恢复较慢，可在启动前调整退避基准（例如改为45/90/180秒）和抖动比例；它仍受阶段总时限和
300秒单次上限约束：

~~~powershell
$env:GAODE_WORKFLOW_CAPACITY_BACKOFF_SECONDS = '45'
$env:GAODE_WORKFLOW_CAPACITY_JITTER = '0.20'
~~~

日志保护也可按服务器容量调整（默认单文件64 MiB、单请求512 MiB、至少保留256 MiB磁盘空间）：

~~~powershell
$env:GAODE_WORKFLOW_MAX_LOG_BYTES = '67108864'
$env:GAODE_WORKFLOW_MAX_REQUEST_LOG_BYTES = '536870912'
$env:GAODE_WORKFLOW_MIN_FREE_BYTES = '268435456'
~~~

阶段和Verify都会每约20秒写进度到stderr；服务器上通过SSH运行时请保持同一用户的Windows
交互会话登录，断开SSH可以，注销不可以。Doctor会留下顶层`result.json`，包括
`failure_kind`、各次执行结果和证据目录，容量错误、报告错误、执行器故障、超时、测试失败
和权限/会话故障可分别定位。

## 当前验证边界

- 配置/脚本语法及Workflow控制逻辑自检：见setup-check.md。
- 隔离真实引擎/CLI/命令/文件写入/结构化报告/accept：通过，见setup-check.md。
- 工作流控制测试使用假的步骤结果；执行包装测试包含真实的受控诊断子进程。
- 真实Verify已经执行restore/build/test并通过131项；它只证明当前源码的软件测试通过，
  不覆盖SQLite/业务媒体的全部现场语义、设备、算法精度、节拍或M1任务审查。
- 以上检查不构成M1完成、真实设备适配或生产验收结论。

## 依据

- [Spec Kit Workflows](https://github.github.com/spec-kit/reference/workflows.html)
- [OpenAI Docs：Codex非交互模式](https://developers.openai.com/codex/noninteractive)
- 本机specify_cli/workflows、integrations/codex实际源码与CLI --help；
  本机行为优先于不能确认适用版本的示例。
