# Bug Assessment: 同值解锁显示及测试者监控布局
- Slug: monitor-xyz-history（沿用会话已确认目录，新增时间戳报告，历史不覆盖）
- Created: 2026-09-29
- Verdict: valid
- Severity: medium
- Source: 本轮用户授权、上传Q01流程记录与解锁诊断、artifacts/upload-review-Q01-20260929/diagnosis.md。

## Report / Reproduction
公共解锁路径实际写Pallet_Lock_Cmd=0，原寄存器已为0；write155/transaction6001存在，PLC随后反馈1→0。页面只把Move/Sort坐标审计合入history，PalletLock写入未显示。页面历史位于全量点位表后方且高度小，人工核查要反复滚动。

## Root Cause / Confidence
高：PlcDataStore变化流同值过滤合理，audit/2.0已保留真实写入。缺陷为app.js消费遗漏及布局空间分配；不改锁盘、解锁或业务语义。当前源码无git仓库，使用逐文件before副本和摘要记录本轮边界。

## Proposed Remediation
1. 同步003现有FR14、诊断合同、plan及T062增量，再改源码；独立页面布局设计见monitor-test-workspace.md，006客户原型只读。
2. Pallet_Lock_Cmd唯一从audit实际写入展示（不造前值）；0明确解锁指令，拒绝写标明拒绝。以设备会话/审计序号去重，关联同连接写引用的PalletLock动作受理/完成，完成状态仍来自实际changes，不把受理当解锁。保留心跳过滤、其他状态/清零、完整XYZ及错误分类。
3. 首屏大历史区域、紧凑逐信号行、准确本地时间及UTC提示、搜索/方向筛选、暂停查看但继续接收、滚动历史时冻结画面、恢复最新、隐藏点位侧栏。保留有限缓存/缺口，清空不回灌。只读GET，不加控制功能。
4. imagegen仅提供布局概念，生成图存在示例地址/数值与协议不符，禁止照搬；实现使用真实协议映射及审计。
5. Files: VirtualPlc/wwwroot/{app.js,index.html,styles.css}; scripts/tests/virtual-plc-monitor.test.cjs; packaging/windows-local-20260927/{build.py,Start.ps1,README.md}; 003 spec/contracts/plan/tasks及独立监控页面设计；003/007/008证据入口。

## Validation
上传原始write155离线重放、没有前值时不造0→0、重复轮询去重、已变值命令不双记、拒绝不假成功、动作关联及状态反馈、暂停/搜索/方向/恢复/清空、现有XYZ/错误分类。真实Edge在1600x1000及1366x768查看布局与交互截图。当前VirtualPlc构建；新包解压资源一致性、包内页面重放；其他业务文件与r8逐文件比对复用。

## Limits
不跑配方矩阵、不新增PLC点位或共享API、不修改业务期限。原业务通过及原失败保留；未确认七格/真实设备待办不因本次UI完成而关闭。仅主张本次触及范围一致，不替代全项目验收。
