# Bug Fix / Verification: P03源槽位验收

修正scripts/validate-008-operation-evidence.py：要求恰好一次放置与一次源槽位写入，检查值3、PDU31、原样回显、放置动作writeSequenceRefs关联。维持源槽位与动作序号隔离要求，不改变业务协议、预算或程序。

r8/source-slot-validator-readback.json为已退出原包的工具级读回：实际关联通过；替换为动作序号1拒绝；缺少放置关联拒绝，三项通过。原job001的operation-route-validation.json及失败结果不改写。独立job013已在worker4992/Administrator/Session2开始实际新WPF，整包结果待定。source-freeze-amendment-002.json记录工具摘要与复用worker；原预算计划保留，r2计划及dispatcher只引用新job013后续原job002—012。
