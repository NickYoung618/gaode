# Bug Fix: 启动权限拒绝显示丢失

- Slug: 006-start-permission-display（沿当前评估上下文）
- Fixed: 2026-09-27
- Assessment: ./assessment.md
- Status: applied

## Summary / Changes

frontend/src/runtime.js增加lastStartHttpStatus，401/403使用已有后端拒绝访问文案，render在无run时保持权限受限，成功启动清除。正常失败、质量、Final及恢复分支未改变。build.mjs已将实际运行入口复制至dist/runtime.js。

## Tests Added or Updated / Local Verification

node scripts/build.mjs在frontend执行exit0；四份dist HTML摘要与构建前相同，客户原型ZIP摘要相同。r20 job000原始失败保留，job001实际Auth401和后续Auth403独立验证。未添加镜像实现单元测试或扩大正常配方矩阵。

## Deviations from Assessment

无。采证入口先于复现依已登记Test合同实现；只改变请求身份，实际Host响应及数据库/设备事实不替代。

## Remaining

两条新的真实权限拒绝整包尚待完整读回，不凭源码/编译声明已验证。
