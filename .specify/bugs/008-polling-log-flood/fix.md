# Bug Fix: 查询框架重复日志

- Slug: 008-polling-log-flood
- Status: applied，当前正式日志读回待验证
- Assessment: ./assessment.md

Gaode.Host Program以Microsoft.AspNetCore category Warning阈值控制框架轮询Info，Gaode.Runtime/业务类别未改变。命令POST/DELETE仍由原RuntimeHttp记录受理和结果，阶段、设备、持久、阻断、超时失败仍由原结构化通道记录。不改业务期限或返回成功。独立log-control-build Host0警告/0错误；旧冻结程序、9417条日志的原失败包不改。

本修正针对已确定的高频日志控制缺口，不宣称是间歇通信根因的修复。另有既有PLC256有界内存窗口补业务响应，用于新诊断读回，无逐交易控制台日志。临时采样接线已撤回，旧trace及部分解析记录保留，正常新作业不采样。
