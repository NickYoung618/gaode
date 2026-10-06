# 第一工位开发环境证据

2026-09-21，只读检查：`dotnet --info` 为 SDK 10.0.401、运行时 10.0.12，Windows win-x64；`dotnet nuget list source` 显示 nuget.org；本地缓存可见 EF Core/SQLite/Mvc.Testing 10.0.12、xUnit 2.9.3、runner 3.1.5、Test SDK 18.10.1。`Microsoft.Extensions.TimeProvider.Testing` 未见本地缓存，须以实际 restore 确认。

上述是首次检查时取得的开发工具证据，不是生产环境兼容性结论；OPEN-22未关闭。设计编制时点（2026-09-20）恢复、构建和测试尚待执行，这一历史NotRun事实保留。后续M1验证及独立审查已进行；定向修正见[修复证据](../../artifacts/station01/m1-review-fixes-20260921-02/repair-report.md)，当前审查和实际命令结果见[第三次独立审查证据](../../artifacts/station01/m1-independent-review-20260921-03/independent-review.md)。这些证据不将M1写成完整第一工位或真机验证，也不能从包缓存或历史通过推定生产兼容性。

历史证据说明：修复阶段机器摘要曾记录163个文件，最终完整性检查当前清单为164个文件；旧摘要和测试结果均保留，以最终源码清单及逐文件SHA-256一致为准。该差异不影响M1验证结论。

## 2026-09-21外部资料状态

只读核对了 `E:\dzk\gaode\原型.zip` 和 `E:\dzk\gaode\软件开发SDK.zip` 的归档目录与SHA-256，未解压、未安装、未执行文件。完整清单见 `artifacts/station01/m1-continuation-20260921/external-source-inventory.md`。

- 前端原型：资料已提供、目录已核对；交互及API一致性未审阅。它只供前端联调前参考，本期不新增前端任务。
- 设备SDK：资料已提供、目录已核对；接口内容未审阅、适配未实现、真机未验证。详细接口核验放在真实设备适配前。

该记录纠正“厂商SDK资料全部缺失”的笼统表述，但不证明生产OS、驱动、位数、协议、SDK行为或真实硬件兼容，OPEN-22保持开放。
