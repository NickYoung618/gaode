# 本轮证据索引

| 文件/目录 | 作用与限制 |
|---|---|
| initial-hashes.json、observer-before.ps1、task-runner-before.ps1、freeze-before.json、request-before.json | 接管前版本及原Observe请求归档，防止两套启动链混用 |
| current-freeze-validation.json、tool-verification.json、protected-validation.json、manifest.json | 原37冻结条目匹配、工具必要验证、原assessment/宪章/任务保护及本轮最终摘要；静态验证不算业务Passed |
| application-windows.json | 014401旧样本完整窗口派生；另有截断窗口，不是全量证据 |
| new-application-readback.json | 015550两完整窗口及一parseError，如实保留；原logs为准 |
| task-final.json、inspection-request.json、new-observation-request.json、completed-observation-request.json | 任务状态、唯一新观察请求及已完成原请求链 |
| ../task-aa081bbf9493447e8eaec700777d108e/ | 原管理员Preflight |
| ../task-2c19da537c2948b182a26bd2746333be/ | 原Observe已执行；result只说返回待审，不是成功 |
| ../task-f1eb991789e34c5296466fa884410f56/ | 本轮第一次管理员只读核验：旧实例/默认状态、旧进程身份，不停止进程、不采样 |
| ../task-754b6885aa1842efb024b4165a552237/ | 本轮唯一补充观察管理员执行上下文/日志/返回 |
| ../task-eed0b1ff9aa84005a20c1311c7e37dac/ | 最终管理员核验：全部11身份不存在、默认WPR未录制、无监听、无时间窗内WPR临时ETL候选 |
| ../sampling-20260927T011909797Z/及recovery-20260927T013620973Z/ | dotnet缺失启动失败；后续保存旧ETL（约3.4GB）和管理员结束证据。无双端运行，不用于通信根因 |
| ../sampling-20260927T012026523Z/ | 前置冲突拒绝，未启动设备 |
| artifacts/communication-delay/t065-communication-delay/observe-20260927T014401343Z/ | 原实际双端观察、误匹配、0x80010106及原不完整cleanup |
| artifacts/communication-delay/t065-communication-delay/observe-20260927T015550635Z/ | 本轮116.81秒实际观察、冻结摘要、窗口、保存失败、清理瞬间结果；没有输出ETL |
| artifacts/recipe-execution-008/t065-observation-observe-20260927T015550635Z/ | 实际SQLite/process.json/Host与PLC日志，不启动配方；无超期检测不能推定根因解决 |
| ../observation-freeze-20260927-reconciled.json、../wpr-save-blocker-20260927.json | 当前唯一入口引用的新冻结清单及故障工具阻断；另一sampling-inputs/launcher未消费 |

引用的路径以`E:/dzk/gaode-1`为根。原assessment、supplemental-assessment、supplement-20260927T092247和sampling-followup均保留；本报告澄清其后产生的管理员结果，不覆盖历史。
