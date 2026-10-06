# Bug Verification: 查询框架日志重复输出

- Slug: 008-polling-log-flood
- Result: verified，限日志控制子范围
- Build: r8/build-freeze.json，Host A2AD71BB9A3ABEB8A264BDC4968AC106FA11103F553EA3F0CFFCFBE818D1B653

实际r8 job000短WPF预检退出/清理成功：框架Info 0，RuntimeFlow 2。实际业务job001退出且资源清理true：框架Info 0，RuntimeFlow 504，RuntimeHttp 16。阶段、设备与命令日志保留，重复查询框架Info停止。

业务job001整体Failed原因是独立源槽位验收工具错误，未改写。上述验证只关闭日志重复缺陷，不关闭P03整包或父任务，也不声称已定位/修复此前间歇Modbus交换超期根因。
