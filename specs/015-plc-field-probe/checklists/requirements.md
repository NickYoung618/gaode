# Specification Quality Checklist: 现场 PLC 通信探针

Created: 2026-10-05。Feature: [spec.md](../spec.md)。

- [x] 需求聚焦现场通信与可观察结果，技术设计独立在plan/contracts。
- [x] 模板适用章节完成；产品前端/配方不适用并说明。
- [x] 需求明确且可测；成功条件覆盖只读、心跳、失败诊断和结果边界。
- [x] 正常与必要失败场景、依赖、范围假设明确。
- [x] 无未决需求标记；现场参数作为运行必填输入，不猜测。
- [x] 区分虚拟软件验证、现场通信测试与整机生产完成。
- [x] 用户追加的人工单轴、可配置现场输入、Codex执行及证据回传已纳入US3/FR-007—009/SC-005—006；真机未测范围保留。

模板采用.specify/templates/spec-template.md；extensions.yml hooks为空，无前后钩子需执行。规格可进入实施；真实点表缺失只限制现场运行。
