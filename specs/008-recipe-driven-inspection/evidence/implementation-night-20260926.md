# 008 夜间实施与验收（2026-09-26，进行中）

仅版本化 Test 虚拟链路；不代表生产设备、现场坐标/高度标定或算法精度验收。沿用 Spec Kit implement，正式页面验收后执行 converge；质量清单仍为 15/16，当前仅 T058 保留已完成状态。

## 基线及本轮修复

前批基线见 `implementation-ed-res-20260926.md` 与 `artifacts/recipe-execution-008/page-ed-res-20260926/page-batch-verification.json`：13 次正式 WPF 尝试、10 次通过、3 次失败保留，9 条最终选定路线通过。该恢复故障发生在首图前，不能证明已有旧图片留存。

本轮先修复实际无人值守阻塞：采集工具的 HTTP/CDP 连接、请求、断开增加有限期限；每个 job 使用冻结配方的真实计划与现有业务预算推导独立外层期限，业务期限不变；记录所属进程的父子关系、创建时间及路径，失败后核对资源释放，未知则暂停。StorePrep 使用既有编译产物，正式 job 不重新编译共享资源。

GROUP-A-E 合法路线预算为 1,842,000ms；旧采集统一 1,000,000ms 会误杀合法长路线。新采集外层为路线预算加公共准备与采证开销；实际配置见各 job 的 `started.json` 与 `page-case.json`。不放宽 I/O 1 秒、心跳 3 秒及合同阶段期限。

新增两份独立 Pending Test fixture/worker manifest：GROUP-F-PENDING 仅指定问题成员返回 Pending，其余 OK；ASSEMBLY-A-E-PENDING 指定 BASE 面返回 Pending，由后端汇总整体。旧 fixture 未覆盖，结论来自独立算法进程的版本化清单。

## 预检尝试与失败保留

夜间后续真实阻塞：r4 job-004 ASSEMBLY-A-E-NG内层页面/同run读回退出0，但外层清理因PID身份变化失败而暂停，原job退出1、cleanupVerified=false保持。追踪器仅看ParentProcessId，PID复用使更早的Administrator shell PID4340（2026-09-24创建）及其console PID4808误入新WebView2子树。PID4340仍存活；PID4808已不存在，原脚本未记录每次停止尝试，不能判断该旧console退出原因或宣称未受影响。后续不停止这些无关身份。`job-004-cleanup-identity-reconciliation.json`核对本job实际新建身份全部不存在、相关端口空闲，允许独立修复/构建，原清理失败不改判。

修复追踪须子进程创建时间不早于本次父身份，并匹配本会话；以PID+创建时间分别记录身份，清理遇不同身份不停止。补每job之间的暂停/finish检查。工具层受控检查`tool-fixes-probe-result.json`使用实际脚本AST抽取的代码，证明旧父边排除、同PID新身份分别保留，以及完整游标分页冻结2406而不追随新增心跳、空页无进展有限失败。该检查不是正式WPF预检；新r5初始暂停，等待实际交互worker加载已冻结脚本后继续。

新worker增加仅用于已授权构建重载的同会话后继启动：新root须暂停、无job/run，校验源摘要、PID/账号/会话及ready后原worker才退出；重载记录明确非正式WPF尝试。不另建调度平台。当前Session0账户尝试Administrator InteractiveToken探针被Windows拒绝，见`interactive-task-probe-dispatch.json`，未启动作业；已向用户给出准确r5重启命令，等待期间继续后端工作。

- `page-next-closure-20260926-night`：工具层 HTTP 停滞、CDP 请求停滞及断开三项探针全部有限退出，证据 `preflight-collector-r2/result.json`。最初探针依赖导出错误保留，修复后新目录复验。
- `...night-r2/queue/job-000*`：实际 WPF 登录、连接和正常采集退出通过；发现 PowerShell 单元素父进程数组被转换为标量，所属树记录不完整，因此不宣称无人值守清理通过。修复后受控五节点树全部记录，无关节点排除，见 `ownership-tracking-probe.json`。
- `...night-r3/queue/job-000*`：实际 WPF 预检退出 0，记录完整 17 个所属子进程；清理原结果失败，原因是 WebView2 子进程在查询与停止之间自行退出。原结果与日志保留。随后核对全部已记录 PID 均不存在、相关端口均释放，见 `preflight-cleanup-reconciliation.json`。仅在停止失败后复查 PID 已不存在才接受；其他错误仍暂停。真实子进程退出探针通过，见 `cleanup-exit-race-probe.json`。
- 当前 `...night-r4`：重新冻结并等待修正版 worker 加载，尚未据旧 ready 派发正式路线。

## 必要测试

`...night-r3/tests/necessary-guards.trx`：16 项，15 通过、1 失败，原失败保留。失败是未知步骤测试仍将 Rotate 当作不支持步骤，而批准的特殊旋转已实现。只将测试哨兵替换为未定义枚举值 999，不改生产逻辑或放宽拒绝条件。重新编译测试项目并运行受影响类，`coordinator-corrected.trx` 6/6 通过。Host 与 Application 二进制摘要未变，与前批最终页面构建相同。

## 后续验收与任务判定

本轮只读索引既有必要门禁，不再次执行全套：r4 `necessary-evidence-reuse.json`保存7份选定TRX的119/119实际结果、逐项名称、原文件摘要及程序集比对。覆盖协议/配置、取放反馈、SQLite保存/API、恢复关联/幂等和组结果等既有门禁。业务程序集及PLC/前端资源与前批最终记录相同，Host查询投影摘要不同；原批`buildApplicability`亦明确部分测试早于最终查询投影。因此仅沿已检查的未变实现复用，不能称119项都在当前Host运行；新预算、预留、暂停或前端修复须作对应必要检查。

T070收口入口已建立[completion-review.md](completion-review.md)，目前明确为进行中，未替代任何正式验收或converge。

按成组、整体、特殊旋转、人工换面推进；继续审计 T049—T070 原始条件，已有证据只按构建与未变实现范围复用。页面 Final 必须与已退出作业的 API、SQLite、媒体和虚拟 PLC 对账；完整父任务未满足时不勾选。当前正式路线、旧媒体恢复、其余普通面及失败证据映射尚未收口，008 尚未完成。

`...night-r4/queue/job-000.result.json`：修正版 worker Administrator / Session 2 / PID 10944，SHA256 `407FBD68A40E6644D7D892FB4FFB9DEE5FBD2ACACCF331258FE3E2CA2E2961D2`；实际 WPF 短预检及完整所属进程清理通过，exitCode=0、cleanupVerified=true。随后自动串行派发代表路线；worker 保持等待，不提前发送 finish。

### 已结束正式路线

r4 job-003 GROUP-A-E实际runId `253e1ff9-3284-4a13-8d6a-db3f2104afdb`已真实页面Final并完成两组/八成员/十四面、完整采集/融合/E绑定；原验证16项true而翻面ACK采证false，退出1，cleanupVerified=true。根因为导出只读取1024项第一页，真实latestSequence2406尚未分页，不是ACK已证明缺失；也不能据Final将失败改Passed。原`operation-route-validation.json`及`scene-acceptance-audit.json`均保留失败。修复合同见`.specify/bugs/008-page-plc-pagination/assessment.md`，冻结队列结束后修工具并用新job/new run复验。

| Job/路线 | runId | 实际通过范围 | 同run证据 |
| --- | --- | --- | --- |
| r4 job-001 GROUP-F-MIXED | 4993a0b8-096a-4037-bb7e-79bf7a919ab1 | 两组、四成员、六面全部；NG优先保留Pending面；仅一个问题成员取放，三个OK不搬；页面刷新/重开和对象切换；Final及清理通过 | `runs/job-001-GROUP-F-MIXED/GROUP-F-MIXED/operation-route-validation.json`、`scene-acceptance-audit.json`、页面/API/SQLite/媒体及PLC事实 |
| r4 job-002 GROUP-F-PENDING | 22eca098-2b64-47ad-baf9-44fca682248d | 两组、四成员、六面全部；仅P01/M01为Pending并取放一次，其他三成员OK留原位；判定刷新/重开、Final及清理通过 | `runs/job-002-GROUP-F-PENDING/GROUP-F-PENDING/operation-route-validation.json`、`scene-acceptance-audit.json`及原始包 |

Host SHA256 `760393F33F5C3213630AF8CB3DC6AFBDD3BA0559D4C2681EAAEE7BA7CE734875`，业务程序集与前批最终构建一致；各job实际装载摘要见包内 manifest/interactive-desktop 与 r4 frozen-files。上述实际取放通过不抵扣未实现的容量/预留原条件。

实际截图复核另发现重开后顶部当前配方标题未从后端恢复，虽同run判定、F/冻结/保存及Final正确。缺陷记录`.specify/bugs/008-recipe-header-reopen/assessment.md`；当前Passed仅指上述明确子范围，不能宣称全部页面刷新状态已正确。后续最小绑定修复与必要复验由006现有结果/配方任务和008 T070承接。

当前3.1分拣公式还有合法上界计数缺口：每应搬实体只计一段XyCompletion与一份保存，未完整计入取放两段/ACK/在途提交。当前包的实际耗时及未超期事实成立，不能据此关闭T051全条件。后续按原合同实际步骤版本化推导，不放宽配置或追改原deadline，见plan夜间增量。

### 本轮开始时的完整任务条件对照

以下为审计检查点，不将代码存在等同验收完成。前批有效证据统一指 `implementation-ed-res-20260926.md` 所引批次，历史失败仍保留。

| 任务 | 已实现子能力/有效证据 | 当前剩余条件 | 直接依赖/处理 |
| --- | --- | --- | --- |
| T049 | Q01/Q02 Test 映射已确认；当前多面/多对象合法 Test 输入可加载 | 全部实际采用路线及生产局部限制的输入来源登记 | 003动作合同、Test映射，更新原 input-readiness |
| T050 | 当前生成器显式合法3＋1集合；旧包保留；F唯一绑定和非法准入定向测试 | 新 Pending 清单/素材/worker 与实际选定包核对 | 002目录合同、007清单 |
| T051 | 当前公式3.1、单面/多槽/Q09/Q18实际冻结；本轮合法外层预算 | 成组/整体/旋转/人工实际计数及耗时对照 | T050、现有业务预算，外层不得替代业务期限 |
| T052 | 唯一计划入口、完整XYZ/轴校验；未知步骤哨兵已更正验证 | 复用必要到位/缺输入/保存负例的构建范围核实 | T054/003动作 |
| T053 | AB/CD独立采集、worker/融合、参数与媒体身份，前批同run通过 | 组/整体/E媒体与全部成员、租约及有限缺输入证据映射 | T050/052/054 |
| T054 | SQLite事务/回执/查询；结果关联及恢复关联保存测试通过 | 全部新包退出后读回、requestId诊断、原保存门禁范围核对 | 001持久基础/003查询 |
| T055 | 最终Host Q01页面正常Final及XYZ已通过 | F不匹配、产品到位/复位/必要保存失败的适用证据审定 | 已有负例需按变更选最少复验 |
| T056 | Q02 P01/P03非连续槽CD整链通过，空槽未采集可由冻结序列核对 | 多槽失败身份及第二点Y证据映射 | 与T059共享Q02包 |
| T057 | 实际源槽/协议槽、NG/Pending、取料2→放料3→ACK | **实现缺口：目标格位预留/容量冲突及可靠占用提交未接入**；未知保留与满位不派发必要验证 | 原任务覆盖，继续 implement；不追加容量扩展 |
| T058 | 同最终程序 Q01/PARAM 实际参数/槽位/调用/结果变化通过 | 已完成，保留原勾选及证据 | 不重复运行 |
| T059 | 当前Q02多对象及Q03 NG/Pending页面通过 | 原指定Q01-NG、Q02-PENDING正式页面仍需补；缺结果/满位门禁映射 | 与T057/056同链，不以Q03替代原指定 |
| T060 | 自动不重扫、逐面定位/Flip/ACK；人工子能力后端通过 | 当前人工真实页面确认、安全解除/Complete；错面/缺目标/ACK阻断证据 | 003 T072-M、006 T050 |
| T061 | E真实捕获/worker/绑定/保存/复位；缺码/worker错误有限继续 | 当前E正式页面与机械/复位/保存阻断适用证据 | 不阻无E路线 |
| T062 | 当前Q03/Q09/Q18正式页面及两类3＋1通过 | Q04/Q05/Q06当前完整代表及必要失败门禁 | 仅实际差异选定，不重跑14/22组合 |
| T063 | 成组完整成员/NG优先/问题成员集合与真实分拣已实现 | 当前成组F混合、纯Pending、A四面/E页面对账 | T066分别验组与整体 |
| T064 | 部位独立/整体共享翻面和一次处置已实现 | 当前BASE/PIN/E正常、NG/Pending完整证据 | 不增加旋转前置 |
| T065 | Test Enter/Rotate/Exit、占用和姿态、特殊出口去重已实现 | 当前三出口及整体差异、未知/错姿态门禁证据范围 | 复用003特殊合同 |
| T066 | 组/整体后端已有各自子证据 | 本轮独立正式页面、成员完整/媒体/实际处置 | 两分支独立，不以一分支勾父任务 |
| T067 | 旋转后端三出口/整体OK及错姿态已有历史证据 | 当前正式页面三出口与整体；旧构建失效部分最少复验 | 特殊实际链，不能用普通分拣替代 |
| T068 | 故障双端reset/check→新run实际完整；关联失败/CommitUnknown不启动 | **正常PauseRequested无Paused转换/继续路径**；异步隔离/资源/幂等证据审计 | `.specify/bugs/008-pause-continuation/assessment.md`；现有任务覆盖 |
| T069 | 当前首图前故障正式页面新runFinal，旧故障可查 | 有实际旧媒体后的最少恢复页面；正常暂停/人工/故障对照及权限/日志 | 不把工具保存样本冒充原检测媒体 |
| T070 | 前批局部结果及参数准确汇总 | 全部任务原条件、代码/构建/证据/剩余映射；converge循环和最终覆盖收口 | 当前仍有可解决工作，不能提前结束 |

## 2026-09-27 r5 实现、构建与验收检查点

复用 Administrator / Session 2 的现有交互 worker PID 9476，脚本 SHA256 `9B29E1EB6FE14F3CD2F601FC186CC55463E78F031D0F29B174D254EA73939F1F`。批次为 `artifacts/recipe-execution-008/page-next-closure-20260926-night-r5`。629 个冻结文件逐项摘要核验通过；前端源、dist、WPF `frontend/dist/runtime.js` 三份摘要一致。第一次准备误用 WPF 副本路径，失败元数据保留为 `build-freeze-preparation-failed.json`，未派发业务作业；修正后才派发 job-000 短预检。正式路线此检查点仍为 NotRun。

共享合同先更新，再实施 T057 目标容量与预留、取料在途提交、放料 ACK 后占用提交；实际关键保存/读取上限 2 秒。普通取/放分别限制 XY 8 秒、ACK 2 秒。预算版本 3.2-test 按实际动作计入两段运动、ACK、I/O、持久读写；Q01 Detection 108 秒、Unload 41 秒、单普通分拣实体 56.1 秒。没有修改现有业务配置、心跳或历史冻结期限。

正常暂停接入公共准备、检测与阶段边界；已派发动作完成实际反馈、保存及复位后进入 Paused，检查与 Continue 继续同一 run。取消、安全、断连与故障新轮规则保留。顶部配方标题从实际后端绑定恢复。PLC 采证按冻结序号分页，不将第一页作为完整证据。

必要验证包均位于 r5/tests，以下计数存在重叠，不可相加：`sorting-budget-affected.trx` 41/41；`pause-affected.trx` 13/13；`pause-sorting-final.trx` 43/43；`sorting-motion-timeout.trx` 1/1；最后源码守卫 `final-source-guards.trx` 5/5。前端 runtime 必要测试 21/21。真实 Q01 API 链 `pause-q01-real-chain-r2.trx` 1/1：VirtualModbus、独立 Python 算法、真实 SQLite，3D 后暂停/检查/同 run 继续至 Final，3D/F 各一次；这是 BackendApiNotWpf，不能抵正式页面验收。

失败 TRX 保留：旧四面全 AB 的 smoke 在 Continue 后因当前业务范围明确拒绝；首次新 Q01 暂停试验发现无暂停时额外准入检查提前阻断，修复后 r2 通过。未将失败包改成通过。默认 Host 和 Release WPF 构建均 0 警告、0 错误；摘要见 r5/build-freeze.json。独立 source-build 的测试输出和默认正式构建分开登记。

当前实现已消除先前表中容量/预留和普通暂停的代码缺口；父任务保持原勾选，等待正式页面、持久读回及完整原条件对账。后续需要接班时使用已有 reloadWorkerRoot：后继同账号/会话/脚本摘要就绪并存活后旧 worker 才退出；不先停止有效 worker。登录自动启动配置安排在当前批次验收结束后。

### r5 job-002 Q01-NG 正式通过（2026-09-27）

runId `4208c38d-d5db-4345-9571-46b3fcd97821`，exitCode0、cleanupVerified=true，页面NG保持至Final/刷新/重开；独立worker实际产生结论，原操作验证及场景审计通过。新实现持久读回 `sorting-commit-audit.json`：P01/protocolSlot1→P14，预留持久时间16:05:49.815690Z先于下料；取料status2后InTransit持久16:05:59.133172Z，实际PlaceWrite16:05:59.238729Z；放料status3/ACK清零后Occupied持久16:06:05.745645Z。6项直接顺序/身份检查全部true。不是状态2完成冒充整体处置，也不是日志替代实际反馈。

同一桌面worker自动接续job-003 Q02-PENDING；批次调度端每条开始后保留暂停信号，结束复核exit/cleanup及场景读回后才放行下一条。调度自身UTC转换准备失败保留为dispatcher-preparation-failure.json，修正版deadline检查真实UTC通过，未中断当前worker或作业。

### r5 job-003 Q02-PENDING 正式通过（2026-09-27）

runId `e3072005-0e53-4160-b36f-e5a9614e769f`，两非连续槽P01/P03，实际worker使P01为Pending、P03为OK，只有P01/protocolSlot1搬至P15。exitCode0、cleanupVerified=true，完整操作验证、场景读回及sorting-commit-audit七项通过。Pending维持至Final及刷新/重开，没有用“完成”代替质量或将其他正常实体搬走。原取料2→在途可靠提交→实际放料写入→放料3/ACK清零→占用提交顺序成立。当前同一worker自动进入job-004 GROUP-F-MIXED。

### r5 job-004 GROUP-F-MIXED 已结束子范围通过（2026-09-27）

runId `1b6869ad-de49-4e66-a419-4ca2425553a2`，两组/四成员/六面，八项场景审计true、operation-route-validation通过、exitCode0及cleanupVerified=true。NG优先且Pending面明细保留，仅P01/M01实际取放一次，其余三OK保留；新分拣提交审计七项true。实际旧媒体/结果刷新、登录重开与顶部配方恢复均通过。处置查询字段缺口已登记008-disposition-projection，不能由本子范围通过推称全部结果投影齐备；旧包不补写新字段。当前同一worker自动接续整体NG。

### r5 整体NG与Pending已退出事实、审计失败及恢复（2026-09-27）

job-005 ASSEMBLY-A-E-NG runId `07bce235-af0a-4251-9c62-9b750150a1e7`：三面、独立BASE/PIN结果及媒体，实际E绑定主对象，整体只取放一次；八项场景、七项分拣提交及十二项预算/调用审计通过，exit0/cleanuptrue。旧r4同类清理失败包保留，不覆盖。

job-006 ASSEMBLY-A-E-PENDING runId `4788a57c-9d3f-4d4e-94cc-4e4e7cb5690b`：业务operation验证通过、exit0/cleanuptrue，但场景审计Failed，调度已暂停。真实Assembly及BASE面2为Pending，其余面/PIN为OK，整体一次P01→P15；分拣提交七项、预算/调用十二项均true，仅作为明确子范围，不将整个包改Passed。根因是审计以endswith("NG")判断，PENDING也匹配，错误期望NG。assessment/fix见008-assembly-pending-audit；修正版实际AST四项通过，原失败保持。新job-020独立new run复验已安排，先恢复其他不受影响路线。

worker9476/Session2仍有效、脚本未改；原629冻结文件再次全部一致，场景Python审计工具单独加载，其原/新摘要由source-freeze-amendment-001.json补充登记，冻结业务构建与配置不变。复核调度r3接续job007—019，随后job020；无人工桌面重启。当前job007 E缺码路线进行中。

### 2026-09-27 00:42 交互worker意外退出及权限恢复准备

job007 E缺码run `7a5da230-3c1f-4124-8d8f-be8e64fed5c4` 正式场景7/7、预算调用12/12、零取放3/3通过，exit0/cleanuptrue。job008启动失败、未取得runnerPid，原stderr空；用户窗口确认catch重写stderr被占用，引发第二次异常，worker9476已退出。未发送finish、未停止旧worker，原始启动异常丢失、不得推测。

最小工具修复及实际AST锁复验7/7见 `.specify/bugs/008-worker-launch-error`。启动异常独立保存，部分启动身份未知必须暂停，不能沿用旧cleanuptrue结论。629冻结文件新对账只有worker修复文件变化；业务源码/二进制/fixture保持相同。新暂停root `page-next-closure-20260926-night-r5-resume-1` 承接原job009—020及新job021 E错误复验，不覆盖r5失败或队列。

旧worker已退出使reloadWorkerRoot无执行主体。当前Session0无法创建Administrator InteractiveToken任务（先前0x80070005），用户询问如何授予权限后已准备固定按需恢复任务的Admin授权入口，只授Codex查询/执行、无登录触发器，不关闭任何现存worker。恢复脚本先核查真实进程、端口、原stderr锁及账号会话，再启动新暂停worker并记录ready。当前授权及恢复未执行，不计正式验收；后续继续当前批次，批次结束后实际reload接班，登录启动仍延后。

### 恢复权限已验证、续跑与旋转采证修复

Administrator授予固定Gaode008DesktopRecovery无触发器任务的查询/执行权限；Codex从Session0实际Run并核查旧进程/端口/日志锁，恢复Administrator/Session2 worker10536。再次Run实际ReusedLiveWorker同PID与真实创建时间，未停止或重启有效worker。首次授权准备仅COM“任务不存在”异常处理错误，未创建任务；实际解包探针通过后授权成功。完整taskSddl、运行身份及独立恢复结果见desktop-worker-control，bug008-worker-launch-error验证通过。

续跑job009 ROT-PART-OK run3981e5e8-36c3-4eee-a0c8-63e63e9921be页面Final但采证数组额外包装，Python验证报list没有items；outer1/cleanuptrue，原失败保留。仅修verify子脚本单行，实际PS数组返回＋实际赋值AST四项通过，新job022独立复验。当前worker10536保持本体不变，后续runner自动加载修正子脚本；629冻结文件二次对账只有两项必要工具修复，业务源码/程序/fixture不变。

job010 ROT-PART-NG run08eec80b-4b87-41cf-bb08-fb25e6e934e2已正式通过17项操作、5项场景、12项预算/调用及3项零外层取放审计；outer0/cleanuptrue。真实四个Enter/Rotate/Rotate/Exit回执，NG出口可靠释放，2面独立媒体与结果保留，Height/FDecode各1、Detection6，阶段提交均在冻结绝对期限内。Pending出口及其余选定代表继续；当前仍待处置查询补缺、完整任务条件、converge和实际reload验证，登录自动启动尚未配置。

job012特殊整体OK run `e2531231-93e6-424e-8701-6b27c4c5ecd0`，18项操作、8项场景、12项预算调用、3项零普通取放审计通过，outer0/cleanuptrue。独立BASE/PIN三面、E一次、可靠旋转出口及释放均有实际同run事实，适用范围见rotation-runs。

job013普通人工换面 run `0cff6304-2358-4889-b1ba-a72070759503`，outer1/cleanuptrue，页面开始后StartupNotReady，原因PlcCommunicationUnknown/epoch2；实际人工占用注入和页面确认尚未发生，不计人工换面通过。Host心跳ReadBody记录3742.5521ms（原1000ms期限），阻断禁止设备动作；OS调度原因尚未证明。原console两段诊断JSON不完整，提取报告明确登记未解析段，不伪造完整诊断。保留原失败，worker10536存活并自动接续job014及其他独立路线；未手动重启，未放宽业务期限。

job014整体人工路线在平台启动时再次I/O超时，未打开正式页面/无业务run，outer1/cleanuptrue。停止派发共享启动链的后续路线。短预检resume-1 job000采用受控串行PLC启动，工具页面登录/退出/清理通过，但实际PLC仍锁未知；此调整没有解决问题，已撤回且启动脚本SHA恢复原冻结值 `86A26B5006B455791B046B9156AC201F564ACE846ADDD07B0852B88FF1237F64`。job001在无并发Codex命令的观察窗口仍于Connect超1000ms并退出Host，outer1/cleanuptrue，无正式页面，不归因于资料读取。两份观察不是业务路线通过，原日志均保留。

在无活动job、所有已启动job均已退出并确认清理的暂停窗口，实际使用reloadWorkerRoot：resume-1 job008的后继worker9936/Administrator/Session2于17:16:23.7109199Z就绪，旧10536于17:16:23.8067816Z写结束并退出；后继先于旧结束95.8617ms。exit0/cleanuptrue，非WPF尝试。新暂停root resume-2承接尚未执行的job015—022，同业务构建/fixture；旧队列和失败保持。未手动重启或提前杀旧worker。固定按需恢复请求同步到当前后继；登录触发器仍未配置，通信阻断未据接班成功宣告修复。

### 独立处置构建及r6正式代表（最新01:54检查点）

共享通信阻断期间，独立输出实施已确认处置投影：同run/tray/plan的预留、Pick可靠提交、Place3/ACK0占用和特殊出口可靠释放事实生成物理状态/实际movement；ordinaryOk明确无需搬运，Group/Part/Face不复制物理状态。补原事件生产者的ordinaryOk及特殊出口实际sourceSlotId/targetPointRef，结果版本含处置事实。不补写历史库、不加动作或期限。15项投影/原结果＋15项阶段生产者必要测试通过；真实旧已退出Q02 SQLite在独立新Host回放正确，不算新WPF。

原worker9936通过reloadWorkerRoot自动交接5040/Administrator/Session2于17:33:09.6514138Z就绪，之后9936退出。r6 Host `5CB0250D243A330D796896F775C5F02ACD2D9650F581A56C94B986644BF62852`、PLC `7AFDE51A1D7D48BEAFDC625F8E62372E48F6DD687804FACD642CA750C36DD326`、Application `FD4EE7C7338217E3B7C62C3D34F1C7D84926B8C5B50602A75A00A10A1E442797`，新构建预检job000真实页面登录/退出/清理通过。默认旧程序、WPF/runtime及原型保持。

P03新代表job001实际run `cba580f7-64b0-4be0-b3c3-7abda4f91647`在分拣前缺目标配置，SortingTargetUnconfigured，无分拣派发。页面DeadlineExceeded，outer1/cleanuptrue17:46:09Z。独立1.1.4给P03绑定原同盘P15 Test目的点，保留自身源点/协议槽3，旧1.1.3保留。job002尚未WPF即Host Connect超过1000ms，outer1/cleanuptrue17:47:07Z。

有限所属PLC健康门禁先于Host，通信期限不改。job014短WPF预检outer0/cleanuptrue17:49:02Z且无通信失败，不能抵业务验收；随后job015正式代表再次在平台启动WriteRequest/ReadBody超期，尚未WPF/无业务run，outer1/cleanuptrue17:50:09Z。原始五个诊断窗口完整解析，PLC首个心跳响应17:50:02.2134893Z、Host首部读完17:50:03.2899122Z约1076ms空档；Host CPU2156ms/存活约2847ms、GC7.504ms，具体根因未定。停止派发，不盲重跑003—013。

继续已有T054/T069必要定位，仅增加await进入及Connect完成tick；独立Host诊断构建 `9DD787CD1D0E1E2A3F0B4C6181D9BE3D2084A33FE348BFE6580352EF911D0B51` 构建0警告/0错误，初次新目录no-restore缺assets为准备失败而非业务尝试。定向日志验证及新暂停r7准备中，后续仍自动接班；登录启动配置延后。第一轮converge见[记录](converge-night-20260927.md)，追加0任务，未关闭任何未齐原条件。

## 2026-09-27 02:31当前检查点（优先于历史状态）

当前root为page-next-closure-20260927-log-control-r8，worker4992/Administrator/Session2持续复用；r7→r8自动接班先新ready再旧退出，累计四次真实reloadWorkerRoot接班。无登录触发器配置，待本批完成后处理。

当前冻结Host A2AD71BB9A3ABEB8A264BDC4968AC106FA11103F553EA3F0CFFCFBE818D1B653、PLC 66FDD5F61421C644C3CAE0B182DBD73B808E0E18728057B87EE2E7D312E9F4AF（实际完整SHA以build-freeze.json为准）、Application 7E086195C239F4D0B9511998A874220A5FB7BE62306BE98AE276443EA8733634。框架重复查询日志已由实际包验证受控；现有业务超时未放宽，此前通信超期根因仍未知。r7临时进程内采样已移除，部分截断trace只作诊断，不计业务Passed。

r8 job001真实run ab6b70d9-c28a-47b8-b403-1e718d170b09：P03 Pending→P15可靠提交Completed，P01 OK NoMoveRequired，页面刷新/重开处置正确；24检查23通过，唯一源槽位工具错误使整包Failed，原包保留。真实协议只要求放置前一次slot3写入，已将工具改为精确回显/偏移/动作关联。三项必要工具读回通过，新job013在同worker自动执行；其他job002—012按原冻结顺序等待。不能以已通过子范围代整包或任务完成。T058及质量15/16不变。
