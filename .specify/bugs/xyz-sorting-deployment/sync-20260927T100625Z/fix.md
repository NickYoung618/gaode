# Bug Fix: I1—I4合同、采证与监控同步

- Slug: xyz-sorting-deployment（explicit）
- Assessment: 原assessment + 本目录assessment-supplement.md + 本次只读analyze I1—I4
- Status: applied（同步修复范围）；缺陷整体partial

## 修改
I1：003/007/008当前合同统一公开XYZ名称与最新用户依据；旧名称/只动XY/首ushort/待实施快照标明历史及接续规则。来源Word不变，地址/类型/方向/握手不变。
I2：汇总器两个运动检查按既有document address 4x0001筛选；不增偏移规则，不依赖显示名，不把Sorting或不同命令混入。
I3：先补003当前诊断合同和T062现有要求；读取/渲染/变化/审计错误分别处理。当前仅communicationTimedOut，无TCP Connected字段：报告心跳事实及未提供的TCP状态。保留旧快照/显示时间，失败不假恢复。监控版本接线r6。
I4：补003动作诊断入口及006/008三个入口，准确引用旧运行、29动作87轴、版本差异和失败；不改任务勾选。

## 验证及交付
验证见test.md及verification-proof.json。r5保持，新r6沿现有build.py显式PLC构建及archive.py候选不可覆盖路径生成，解压后验证资源和显示。

## 与原评估的增量
本轮依据用户授权仅I1—I4，原缺陷业务实现与分拣测试入口不重开发。新增必要变更为index.html、Start.ps1版本查询、build.py版本、README及现有测试；不改Host/PLC业务DLL。TCP连接状态字段不存在的事实已如实记录，不扩接口补造。

## 仍待
原本机RunningF同run日志/包摘要；七格正式映射确认。缺陷整体partial，原报告和失败不覆盖。

Fixed: 2026-09-27T10:14:55.100539+00:00

## 实际文件清单

|文件|对应范围|
|---|---|
|`specs/003-plc-latest-protocol/contracts/virtual-plc-boundary.md`|I1/I3|
|`specs/007-station01-integrated-loop/contracts/virtual-integration.md`|I1|
|`specs/008-recipe-driven-inspection/contracts/execution.md`|I1|
|`scripts/summarize-q01-q02-evidence.py`|I2|
|`VirtualPlc/wwwroot/app.js`|I3/版本交付|
|`scripts/tests/virtual-plc-monitor.test.cjs`|I3/版本交付|
|`specs/003-plc-latest-protocol/tasks.md`|I3/版本交付|
|`specs/006-frontend-station01-console/evidence/008-first-route-ui.md`|I4|
|`specs/008-recipe-driven-inspection/evidence/index.md`|I4|
|`specs/008-recipe-driven-inspection/evidence/completion-review.md`|I4|
|`VirtualPlc/wwwroot/index.html`|I3/版本交付|
|`packaging/windows-local-20260927/Start.ps1`|I3/版本交付|
|`packaging/windows-local-20260927/build.py`|I3/版本交付|
|`packaging/windows-local-20260927/README.md`|I3/版本交付|
|`specs/003-plc-latest-protocol/evidence/008-action-diagnostics.md`|I4|
