# Bug Verification: B02 环境提前返回漏检

- **Slug**: 010-b02-early-return（显式用户参数，连续执行授权）
- **Tested**: 2026-10-03T01:27:39.290646+00:00
- **Assessment**: ./assessment.md
- **Fix**: ./fix.md
- **Result**: verified

## Summary

010 B02门禁缺口修复并通过专项验证。修复前实际复现两条有效语法/语义样本均漏报B02；修复后直接条件及bool helper/别名条件均由同一正式检查器报告B02并核精确位置。合法明确拒绝、来源记录和正常业务return通过。本次不宣称当前010完整动态聚合重新通过。

## Checks Performed

| 检查 | 实际动作/证据 | 结果 | 范围与判据 |
| --- | --- | --- | --- |
| 修复前复现 | pre-fix/pre-fix.trx，pre-fix.log，original-checker.cs.txt，pre-fix-reproduction.json | 漏检成立 | 实际2失败；compilationErrors=0，ExecuteAsync/helper绑定成功，rules为空，失败为Expected B02 |
| 修复后新增正负例 | post-fix-dev-01/post-fix-dev-01.trx；当前L/boundaries.trx中的六条新sample | pass | 开发6/6；当前最终版本再由L实际执行；两负例核B02+环境条件路径/行/列；四合法正例无违规 |
| 当前正式完整L | run-current-l.py → 原run_lightweight及final_gate；E:/dzk/gaode-1/artifacts/recipe-execution-010/bug-010-b02-early-return-76d2915a436f47c7846b7e0b1ad4898a/L/ledger.json | pass | required/discovered/executed/passed=70/70/70/70；0Skip/0缺失/0解析错误；dotnet27、script7、G/C36 |
| 直接受影响回归/构建 | L/build.log、boundaries.discovery.log、boundaries.trx | pass | 构建0错误/0警告；保留原5当前规则、9违规、3合法及009四项适用静态保护，加新六行；没有运行Rules全量 |
| 新必需行缺失 | verify-current-evidence.py调用原final_gate，final-gate-negative/missing/rejection.json | 拒绝符合预期 | N02-return-direct未在TRX执行；MissingOrDuplicateExecution:N02-return-direct；固定清单完整、raw摘要重绑定，无单纯hash错误掩盖漏跑 |
| 新必需行未执行 | 同脚本/判定函数，final-gate-negative/not-executed/rejection.json | 拒绝符合预期 | NotExecuted真实解析为未执行；NotDiscoveredExecutedPassed:N02-return-direct及TrxNonPassing:notExecuted |
| 伪造汇总不能抵消 | 两隔离故障副本均保留passed=true/result=Passed，传入own_passed=True | 拒绝符合预期 | 既有最终判定重读原始TRX与固定L，不信汇总；副本明确标NegativeFinalGateInput，不冒充运行证据 |
| 当前凭证再核/未重复运行 | bug-test阶段final_gate重读当前源/构建/清单及原始报告 | pass | 重用同一有效执行证据；未再启动dotnet test或完整L，未在bug-test修改被测源码 |
| 产品/历史/保护核对 | verification.json、baseline.json，旧freeze.sourceFiles与当前枚举比较 | pass | 228产品源码文件不变、1182历史证据文件不变、AGENTS/宪章/spec/requirements/architecture不变；独立预期/业务helpers等冻结源变化0，只有允许的五个检查器/测试/清单/文档源变化 |

## Output Excerpts

- 修复前：`sample=N02-return-direct/helper-alias; compilationErrors=0; boundCalls=RecipeDetectionExecutor.RequiresApproval(string),IDetectionPort.ExecuteAsync(); rules=`，2失败/0通过/0Skip。
- 当前L：`requiredCount=70, observedCount=70, errors=[]`；TRX `total=27, executed=27, passed=27, failed=0, notExecuted=0`。
- 最终负例：`MissingOrDuplicateExecution:N02-return-direct`；`NotDiscoveredExecutedPassed:N02-return-direct`。两次最终passed=false。

## Current Execution Identity

- parentAttemptId: bug-010-b02-early-return-76d2915a436f47c7846b7e0b1ad4898a
- verificationAttemptId: 05b32b1cdcb244d98b43886ccbaff99c
- profile: RecipeExecution010；scope: B02LightweightOnly（未调用动态run_profile）。
- sourceDigest: 497fa6f3a4cf2d29d607dc95c70a295696760fe2c4e25c2d4c924291e40c790c
- buildDigest: d587afb78c6ac80c74bd4f02604f0b060180fe42ed1095a3c22cba16c3712a4d
- manifestDigest: ad2c2ee472b4f36054b1d331ae8cc9e31b875f8ddd7f16d80e01d0802cc6895a
- checkerDigest: 0e4dc4d8610b93d698977fddb4a961c80b7f6bf4234a9a0cf7d27708d8c5ddb5
- inputDigest: be5e666b8c53ac8d0c3fd557a05a5415d3297ac589948d71e3354ebe4b7e8351
- 原始凭证：E:/dzk/gaode-1/artifacts/recipe-execution-010/bug-010-b02-early-return-76d2915a436f47c7846b7e0b1ad4898a/L/context.json、bundle.json、ledger.json、boundaries.trx、boundaries.discovery.log、native/recipe-boundary.json、scripts.json、selfcheck.json。统计按现manifest真实case/dataRow核验。

## Residual Risks

- 本修复限定相关方法中环境if及现bool helper/标量别名的有限控制流，不是任意C#语法通用分析平台；没有扩大语法/异常矩阵。
- 旧业务运行证据对应`standalone-54fa1c4e72904772bd9c91cb27bc7d47-verify-1`及2026-10-02T13:40:11.084839+00:00冻结：当时L64/B178/E1/S1/T180通过。B RunId=c9bf58a3-2a15-417b-bb0c-ad0a1ba6bcac，E RunId=67f75399-0c6d-4b31-a04f-ae6daa8e5c08。旧原始报告/冻结/DB/TRX完整只读保留；本次产品源码未变化。
- 当前检查器/测试/清单/verification/quickstart改变后，旧全局冻结摘要不匹配当前源，不能重盖章或抵充新动态结论。**旧动态证据保留；修改后全套聚合未重新执行。**
- 本轮明确未执行Host、PLC、Worker、数据库、B/E/S/T、全量测试、整机回归、生产接入或真实设备。原失败、生产局部限制和历史未验证事项保留，不自动重开009验收。
- 未触碰被自动审批拒绝删除的旧临时目录；无Git写操作或后续自动阶段。

## Recommendation

关闭本Bug为verified，追加修补任务T049可据上述真实证据完成。结论仅为“010 B02门禁缺口修复并通过专项验证”。本轮停止，不启动全量或后续功能。
