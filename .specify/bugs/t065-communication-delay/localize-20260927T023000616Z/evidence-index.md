# 本轮证据索引

时间：2026-09-27T02:56Z。所有以下路径均相对仓库根目录；原始证据不覆写。结论见同目录 assessment-supplement.md。

## 原始执行证据

|证据|路径及用途|
|---|---|
|前置实际管理员检查|`.specify/bugs/t065-communication-delay/task-283f1a3440f047ff9e8def000d48fbdd/`：identity、WPR/ETW、冻结预检、任务结果|
|唯一Observe请求/消费|本目录 observe-request.json、requests.json；`.specify/bugs/t065-communication-delay/task-7d76cee1ddfe4509a7b99bbc5b563d17/`：started/result/observation-output|
|系统原始采集|`artifacts/communication-delay/t065-communication-delay/observe-20260927T023052877Z/`：tcp-scheduling.etl、events.xml、trace-stats.txt、parse-summary.txt、parse-result.json、wpr-stop-result.json|
|采集身份/配置/窗口/清理|同上目录 identity.txt、freeze.json、private-launch.ps1、network-profile.txt、observation-window.json、owned-identities.json、owned-cleanup.json、connections-before-cleanup.txt、etw-before/after.txt、result.json|
|双端日志、实际PID、数据库|`artifacts/recipe-execution-008/t065-observation-observe-20260927T023052877Z/`：logs/、process.json、station01.test.db及原WAL；不能仅复制db单文件重新计数|
|后置实际管理员检查|`.specify/bugs/t065-communication-delay/task-a844dd7dd419492d9633539b036d8991/`：wpr-status.txt、etw-sessions.txt（若空输出无文件，参照已完成查询及result）、formal-preflight-output.txt、result.json|

本轮真实调用固定任务，未接受任意外部命令参数：

```powershell
$svc = New-Object -ComObject Schedule.Service
$svc.Connect()
$svc.GetFolder('\').GetTask('GaodeT065CommunicationDiagnostic').Run($null)
```

Observe只在新请求7d76…被消费一次。此命令是执行记录，**不要在旧Observe请求仍在文件时重复运行**。任务调用固定入口observe-default-20260927.ps1，由resolve-wpt-toolchain-20260927.ps1 -RequireValidation返回显式WPT路径：

`E:\dzk\gaode-1\.specify\bugs\t065-communication-delay\tool-fix-20260927T020325729Z\portable\Windows Kits\10\Windows Performance Toolkit\wpr.exe` 与同目录xperf.exe，版本20348.3694；tracerpt `C:\Windows\System32\tracerpt.exe`，摘要见工具清单和freeze-review.txt。

真实命令参数：`wpr -start Network -filemode -instancename GaodeT065-4acf614b002340b8a8c1098aa1d61488`；`wpr -stop <本轮tcp-scheduling.etl> -instancename <同一实例>`；`xperf -i <同一ETL> -o <trace-stats.txt> -a tracestats`；tracerpt读取同一ETL输出XML及summary。详细调用路径/退出码见wpr-stop-result.json、parse-result.json和固定脚本。保存已完成，无需再调用stop。

## 本目录新增只读派生证据

- `initial-hashes.json`、`final-hashes.json`：原assessment、fix/test、规范入口/启动器/工具清单及任务文件等前后摘要；均未改变。`freeze-review.txt`记录实际当前冻结链，不依赖旧AST结论。
- `application-windows.json`、`partial-window-salvage.json`、`application-records.json`：完整结构化窗口及截断行可恢复的完整记录前缀。截断窗口不伪装为完整；`failure-timings.json`记录两条原始失败日志及QPC。
- `matched-transactions.json`：连接内66个完整成功匹配；`application-summary.txt`保留慢成功39、失败306/112、有效echo及锁存上下文。
- `database-readback.json`：只读SQLite计数；`runtime-readonly-checks.jsonl`：运行状态；`historical-load-comparison.json`：r15/r17输入/build/实际负载差异。r22 HTTP不合并。
- `extract-target-events.ps1`：对原events.xml流式提取，无设备启动。执行：`& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File '<本目录>\extract-target-events.ps1' -XmlPath '<本轮采集目录>\events.xml'`。这是重解析命令，不是重采集命令。
- `system-events.jsonl`及`.summary.json`：231557条选中系统事件、4376518总事件、解析错误及UTC/FILETIME锚点。Thread v3恢复沿用工具证明和归档微软PerfView源布局。
- `extractor-before-duplicate-field.ps1`、`system-events-first-partial.jsonl`、`extraction-first-error.json`：首次派生提取遇重复Guid字段的失败；GroupBy保留全部同名字段后对**同一XML**重解析成功。没有改采样程序。
- `analyze-system-events.py`、`scheduling-analysis.json`：目标线程、Ready等待与CPU候选。`target-tcp.json`/`core-tcp-timeline.txt`/`failure-system-details.txt`为初筛，不能单独作完整四TCB或跨源事务证明。
- `validate-correlation.py`、`correlation-validation.json`、`target-tcp-bounded.json`：限制在实际连接生命周期内的四TCB候选、时钟缓存残差、102326次切换连续性验证。执行：`& 'C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe' '<本目录>\validate-correlation.py'`。
- `failure-all-tcp.json`：故障区段全部已选TCP provider事件，包含无关连接，不能把其中所有记录归属本业务。
- `dotnet-io-gc.json`为空；已捕获的.NET主要Loader/JIT，不代表运行时没有IO/GC。应以实际profile及事件覆盖解释此缺口。
- `process-readback.json`、`final-state.json`：所有已识别所属进程后来退出，任务最终空闲且最新Preflight已消费。原CleanupIncomplete结果不改写。
- `evidence-hashes.json`：原ETL和关键派生结果的SHA256与字节数。

## 审核判定

一次真正通信观察已完成并捕获超期，系统采集和离线解析有有效证据。**事务到socket完成/续体线程的身份桥接不完整，业务补丁不能确定。**零丢事件不消除非目标解析错误、provider覆盖和时钟缓存限制。下一步提示词及关闭条件见补充评估最后两节；无需重做工具下载或无设备验证。
