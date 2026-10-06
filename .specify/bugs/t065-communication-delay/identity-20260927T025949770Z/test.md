# speckit-bug-test：诊断范围实际验证

结论：必要身份诊断能力PassedWithLimits；业务缺陷NotResolved，003 T065、008 T055/T070未完成。

1. 同一原ETL重读取得QPC并证实缺Threading事件，非猜测解析失败；新故障359个非零NativeOverlapped均唯一IODequeue匹配。故障op86包含完整回调/Read续体TID核验。
2. 初版队列诊断捕获启动故障，却在关键Read取消后才Started：覆盖失败如实保留；保存成功、首读disk-full中断；同ETL恢复解析errors/lost=0，未重新启动设备处理解析失败。
3. 独立预热诊断构建Host7684/PLC5920，实际有效8.3968700秒；观察器准备27.2977530秒，不用launcher寿命代替。双端已连接、Host Ready、currentRun null；未运行页面/配方/运动/复位命令。
4. 3248操作，1074非零快照、1072唯一IODequeue映射；6有效批队列快照证明字段具有实际判别能力。两个末尾Read无64、自然ProbeSummary未发生、快照开销/间隙未知均保留，不伪造覆盖。warmup id0全部排除。
5. 新ETL 484442112字节；WPR start进入运行、stop=0；事件2879641，选1038173，errors=0、lost=0；实际含TCP/Thread/CSwitch/Ready及CLR64/65和本诊断provider。源ETL及raw gzip保留。
6. 最慢Host socket26.1994ms，无超期；PLC101.0058ms是含等新请求的Read，不是处理故障。此次正常不能证明业务根因修复，也不是旧冻结构建严格前后对照。
7. 管理员最终Preflight WPR未录制；PID/创建身份及端点核验全部所属退出，仅TIME_WAIT0。旧raw result曾CleanupIncompleteRequiresReview不覆盖，最终核验补证。
8. 正式入口ReviewOnly核验原冻结业务输入及新工具清单；独立诊断任务固定SHA/路径、无任意参数。工具祖先proof及核心WPT实际字节保持；没有取消或停止未知会话。

必要主流程复验：NotRun。由于业务补丁未实施且T065原机制/安全对照条件未满足，本轮不启动Q01/全矩阵，不复开发已完成Test能力，不改勾选。真实运动锁验证须与有证据业务补丁同条件执行，当前取消结果不能充当安全锁验收。

唯一关键未决证据：同一真实超期NativeOverlapped的IOCP/批队列入队边界。后续禁止无新增判别能力的重复观察。r22 HTTP独立。

补核：旧故障ETL实际含栈，独立reader-stacks已离线导出故障窗口并核对原始帧；原reader未导出不能写成未采集。尚无可信原生/托管符号及入队操作边界，未据此选业务补丁。两次离线解析器编译错误日志保留，修正API命名后同ETL解析0错误0丢失。
