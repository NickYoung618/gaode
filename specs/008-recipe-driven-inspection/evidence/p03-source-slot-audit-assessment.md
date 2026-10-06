# Bug Assessment: P03源槽位检查错误要求两次写入

- Slug: 008-p03-source-slot-audit
- Status: valid，验证工具问题，非协议或业务接口变更

r8 job001已退出且cleanupVerified=true；实际run ab6b70d9-c28a-47b8-b403-1e718d170b09完成P03→P15、P01保持OK，无Face物理状态，真实页面Final/刷新/重开与持久处置正确。整包exit1保留，唯一失败检查要求至少两次SortPartIndex写入。

LatestProtocolStageActionAdapter.WaitSortingAsync只在放置前写一次源槽位；取料由真实源XYZ定位。真实write sequence233、documentNumber32、PDU31、rawWords[3]，requestHex=responseHex=28F9000000060106001F0003，并被completed place动作writeSequenceRefs关联。不得为了工具检查新增协议动作。
