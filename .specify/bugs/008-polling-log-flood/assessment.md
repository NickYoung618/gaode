# Bug Assessment: 查询框架日志重复输出

- Slug: 008-polling-log-flood
- Status: valid，既有T054/T069日志控制范围
- Evidence: r6/runs/job-001-Q02-PENDING-P03/Q02-PENDING-P03/logs/host.out.log

已退出失败包中9417条Microsoft.AspNetCore日志，3487条GET/OPTIONS请求开始/结束，实际RuntimeFlow478条。Program已有中间件意图排除查询，但框架Info默认仍持续输出。高频重复日志未受控属于当前项目明确要求，不能用增加永久全量日志替代。

修正：Microsoft.AspNetCore框架category只输出Warning及以上，保留Gaode自己的命令受理/阶段/设备/持久/超时失败分级结构化日志。不是已确定的Modbus超期根因，不声称过滤日志可解决所有通信。原失败包不改；当前独立构建和下一条实际包验证框架查询重复日志停止、业务日志仍可关联。
