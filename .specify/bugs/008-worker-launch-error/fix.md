# Bug Fix: 启动错误保留与资源未知暂停

- **Slug**: 008-worker-launch-error（从当前用户窗口与失败证据解析）
- **Fixed**: 2026-09-27
- **Assessment**: ./assessment.md
- **Status**: applied

## Summary / Changes

wait-008-page-batch.ps1 的 catch 改写独立 worker-error.json，避免重写 Start-Process 已占用的 stderr。launchAttempted 且无 runner 身份时，记录清理未知、exit1及暂停；只有根本未尝试启动才允许无进程清理成功。保留正常作业与既有 reloadWorkerRoot 行为。

原 worker 已实际退出，不能执行自动接班。新增 recover-008-desktop-worker.ps1 固定恢复入口：当前 Administrator 桌面查询真实命令行/创建时间及 ready，优先复用有效 worker，遇到未知 worker、旧批进程、端口或重定向日志占用拒绝冲突启动；新 root 必须准备并暂停，摘要/PID/账号/会话/ready 均一致才记录恢复。不会停止任何现有 worker 或未知进程。

按用户“如何给权限”的问题准备 grant-008-desktop-recovery.ps1/.cmd：Administrator 创建固定无触发器 InteractiveToken 任务，只给 CodexSandboxOnline SID 查询/执行，保留 SYSTEM/Administrator 权限，执行期限0、禁止硬终止，避免调度期限杀掉工作进程。授权脚本不启动验收，不配置登录启动。当前待 Administrator 执行授权；当前账号先前创建被拒绝0x80070005。

## Tests / Verification

desktop-worker-control/test-launch-error.ps1 执行真实 worker catch/finally AST：受控独占锁复现原错误，并验证独立异常保存、锁下存活、不覆盖原日志、部分启动清理未知并暂停、未启动可清理。7/7通过。三个PS脚本语法检查无错误。未冒充桌面恢复或正式WPF验证。

新暂停 root page-next-closure-20260926-night-r5-resume-1 保存原剩余job009—020及新job021独立E错误复验；原失败队列/文件未改。629冻结文件再次对账，只有授权 worker 修复文件改变，全部业务构建/fixture不变，见 frozen-reuse-validation.json。

## Deviations / Follow-ups

恢复任务权限配置是用户进一步询问后补的最小直接依赖；不新增调度平台或登录触发器。原始启动异常因旧 catch 失败丢失，尚不能判定；原job008 cleanuptrue只说明旧脚本分支，不足以证明未部分启动。恢复必须独立核查，原result不能改写。授权后从Session0实际Run任务，核查恢复与正式代表，再验证自动reload接班；登录启动仍待当前批次结束。

2026-09-27 恢复验证增量：第一次授权查询不存在任务的COM异常被PowerShell包装，未创建任务；去除错误的typed catch并解包原HResult，只处理真实0x80070002。实际Scheduler＋实际AST缺任务探针通过。Administrator第二次授权成功，实际任务无触发器、Codex仅0x1200a9查询/执行。Codex从Session0实际Run成功，旧资源独立核查、worker10536/Administrator/Session2新ready一致；再次Run得到ReusedLiveWorker同PID和真实创建时间，未重启。续跑job009已真实创建runner7708及正式WPF页面。恢复权限不等于后继reload已验证，后者待本批结束。

