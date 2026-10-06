# Bug Assessment: 启动权限拒绝显示丢失

- Slug: 006-start-permission-display
- Created: 2026-09-27
- Source: 008 T055/T070及006 T048既有权限拒绝验收
- Verdict: valid
- Severity: medium

## Report / Symptom

正式WPF r20-auth-0927/job000真实POST返回401，StartFailed.httpStatus=401；render将启动catch中的Unknown覆写为尚无结果，错误区域受理状态未知也不准确表达已知权限拒绝。清理后SQLite六表零行、PLC零业务动作；九项审计仅permission_state_retained失败。原包exit1/cleanup=true保留。

## Reproduction

Q01合法Test fixture，真实页面登录/选用后仅在CDP请求阶段去掉该POST Authorization；实际Host鉴别，不替换响应。完整页面及后台/设备事实在artifacts/recipe-execution-008/r20-auth-0927/runs/job-000-Q01/Q01。

## Proposed Remediation

在runtime.js保留最后一次启动401/403状态，catch复用现有查询拒绝文案，render无当前run时显示既有权限受限；成功受理清除该状态。其他启动失败、正常结果、恢复与操作不改。

## Files likely to change

frontend/src/runtime.js及build.mjs正常复制的frontend/dist/runtime.js。现有006 spec/contracts/api/plan/tasks和008相应文档已先登记接口与采证增量；不改HTML、原型ZIP、页面结构、控件或交互。

## Tests to add or update

既有build/typecheck与原型摘要核对；新独立真实WPF Auth401/Auth403各一条，保留实际StartFailed和稳定页面状态，清理后SQLite与PLC零执行。不添加镜像实现的单元测试，不重跑未变的正常业务路线。

## Risks & Considerations

只绑定已知权限拒绝，不将409/超时当权限错误，也不凭UI推断数据库未创建运行。负向Test身份覆盖仅针对一次启动请求，不宣称生产登录和完整权限系统验收。
