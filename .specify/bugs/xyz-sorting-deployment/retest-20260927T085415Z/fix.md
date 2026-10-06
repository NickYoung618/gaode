# Bug Fix 增量记录：XYZ/分拣部署本机复测

- Slug: xyz-sorting-deployment（沿用已有缺陷）
- 依据：本目录 assessment-supplement.md、用户本轮七张截图及明确XYZ命名确认。
- 历史：原 assessment/fix/test、r2 压缩包及所有失败运行保留。

## 实际修改

1. VirtualPlc 静态资源返回 `Cache-Control: no-store`；index CSS/JS及部署浏览器入口使用本次版本。旧HTML缺完整XYZ节点时一次版本跳转，不在旧DOM上轮询报错；动作审计请求错误与PLC状态连接分开显示，避免500ms/100ms分支红绿交替。
2. 地址表和监控显示公开名称改为 `XYZ_Move_Cmd` / `XYZ_Pos_Confirmed`，数值地址、命令意义及清零没有改变。先同步003 spec/contracts/plan/tasks，来源Word不改。已有同动作写入审计和实际XYZ反馈保持原始来源，相同值在完整运动区域仍显示，变化日志不伪造值变化。
3. 仅Test Python worker输入读取使用Windows扩展路径；保留媒体根目录约束、真实字节长度/SHA校验、原10秒算法及成功条件。274/273字符两条等效长路径对照：旧版InputMediaUnavailable，新版Accepted→真实Result→InputReleased，均退出，无设备/算法业务期限修改。
4. frontend/runtime恢复原型已有检测项及缺陷四列、参数格，以实际投影填入名称、规则、实测/单位、判定、参数/来源、缺陷类型/位置/尺寸；缺失仍缺失，文本转义。保留对象/阶段/完整性/保存状态，并在已有故障区域区分F扫码结束但未取得唯一有效F码。先同步006设计文档，不改客户原型/HTML布局/API。
5. 增加限定本包最近三次运行的只读Collect-Diagnostics入口，读取日志、摘要、环回PLC状态/审计，不发送业务命令、不读令牌配置、不终止进程。打包必须显式指定本轮VirtualPlc构建，避免复制旧DLL。

## 未修改

不改变Host业务算法、XYZ运动/卸料/分拣/翻面握手、不放宽期限、不取消UnloadPreparation。普通OK不盘末取放；特殊搬运回原位保持已有实现。r2独立已验证特殊场景证据保留。没有更新任务勾选。

## 验证计划及界限

按speckit-bug-test继续：精确旧HTML/新JS复现回归、实际长路径worker、必要前端/监控检查；最终ZIP解压后的Q01、Q01-NG、Q02-PENDING-P03、Q03（覆盖翻面），真实WPF操作、Host/VirtualPlc/相机/算法/SQLite/媒体及Final，检查同run DOM、协议顺序及资源释放。

用户本机NG/Pending故障日志尚未取得，不能把本轮环境通过认定为其原失败已确定同因。七格正式业务映射仍未确认，不编造；Test临时格位不宣称正式验收。最终结论见本目录test.md。

## 解压包截图发现的结果字段追加修复

r3的实际WPF截图确认：原代码使用整个判定panel的num-font序列（包含编号）填4个统计值，造成编号/统计错位；基础信息把配方号写入批次、parentId写入MES亦无合同依据。继续bug-fix修正为编号单独绑定id、四统计格精准定位；没有名称/规格/批次/MES字段时保持未提供，所属关系仅保留在对象明细。判定颜色绑定实际质量。未改API和原型HTML，保留r3及其截图，不将r3整体标为已交付修复。

30项必要检查包含新编号/四指标分别绑定及不冒用批次/MES的断言，见ui-followup/component-tests.txt。最终r4将重新打包解压，实际复验NG和Pending两条用户报告入口；普通OK/翻面复用同一Host、VirtualPlc、worker及合法输入的本轮r3解压执行证据，并逐文件说明仅前端绑定/说明修订差异，不能冒称r4运行了这两例。

## 必要收尾修复（r5）

r4 Pending与Q03均完成业务Final及真实DOM/监控检查，但旧Stop先终止Host，再按PID查询它所属的worker时均触发身份校验拒绝；随后worker已消失，剩余PLC须按原身份清理。原始检查未保存不符的具体字段，不能编造是PID复用或哪个字段瞬态为空。已证实的设计缺陷是“中止父进程后再检查子进程身份”导致当前停止流程中断。

最小修正Stop.ps1：在任何停止前核验所有本次进程的记录创建时间、实际创建时间和命令路径，打开并持有对应进程句柄；然后依次终止这些已核验的句柄。Host带动worker退出时通过已持有句柄判断退出，不根据旧PID再选择进程。身份不符仍拒绝停止，不终止未知进程；有界等待5秒，日志stop-diagnostic.json保留每步事实。未修改业务期限或系统设置。

最终r5与r4仅Stop.ps1及README不同，其余343个文件同摘要。用最终r5解压包做一次真实启动/停止验证，保留r4全部真实业务运行结果；不重复配方检测矩阵。
