# 008 FixtureManifest 工具（2026-09-25）

状态：**部分实现，007 T031 保持未完成**。没有执行前端启动或业务运行。

- `start-station01-virtual-loop.ps1` 增加绝对 `FixtureManifest`、独立 `artifacts/recipe-execution-008` Test 根、端口/数据库、配置根/目录/媒体/worker 引用及组件摘要；此入口禁用旧 Test 自动取盘监视器。旧 007 入口仍按旧 Test 用途运行。
- `simulate-station01-load.ps1 -PrepareOnly -FixtureManifest` 从清单生成 `contextJson/2.0`、`expectedRecipeRef` 及公共配置引用，不发 POST，不产生 F 或 Final。`-StartRun` 与清单组合被拒绝。
- PowerShell 解析通过。最小清单工具检查见 `artifacts/recipe-execution-008/fixture-tooling-smoke/manifest.json` 和 `prepared/load-*.json`：`caseId=TOOLING-ONLY`、`channel=PrepareOnlyFor006`、`receipt` 不存在。该清单故意只是接口样本，不是 008 T050 的 Q01 可执行配方或媒体夹具。
- 另以该 `TOOLING-ONLY` 清单在独立 Test 根 `artifacts/recipe-execution-008/fixture-tooling-smoke/runtime-current/` 启动当前 Debug Host、VirtualPlc 和 Host 所属 Python worker，端口 25112/25113/25114；`process.json` 记录三个组件 PID/构建 SHA、独立 SQLite、目录/清单摘要，初次状态 `connected=true/safetyClear=true`、相机与算法 `Ready`。未启动桌面、未发业务 POST、未模拟取盘。15 秒后 `heartbeat-observation.json` 中 VirtualPlc `communicationTimedOut=false`，此单次观察不作为 T065 修复验收。结束后仅按该 `process.json` 的 PID 与命令行匹配停止本次 Host/VirtualPlc，worker 随 Host 退出；没有触及其他服务。

剩余：008 T050 提供带合法点位/素材摘要的真实 Q01 FixtureManifest 后，在独立端口/数据库根做一次当前构建清单装载和版本/路径拒绝验证，记录供正式页面使用的准备结果；随后才能勾选 T031。准备成功不等于完整 Q01。

## 第二批 Q01 Test 夹具（2026-09-25）

`specs/008-recipe-driven-inspection/fixtures/fixture.json` 绑定 Q01 Test 目录、公共/预算/模拟版本、独立 worker 和三种实际可读 PNG；A/B 暂引用同一明确标记的 `Detection` 模拟素材，不宣称相机精度。启动工具新增目录摘要、Q01 F/场景/版本一致、worker F 一致、每个媒体实际文件与 SHA-256 校验。PrepareOnly 生成 `artifacts/recipe-execution-008/Q01-prepared-second-batch/load-*.json`，无 POST/receipt。错误摘要在 `artifacts/recipe-execution-008/q01-bad-digest-rejection.json` 中两工具均退出 1。

当前 Host/VirtualPlc/worker 在独立根 `artifacts/recipe-execution-008/Q01-fixture-second-batch-runtime/` 和端口 25122/25123/25124 启动；`process.json`、`catalog-and-process.json` 证明装载单条 Restricted 配方及独立 SQLite。随后 API 错误合同修正，在另一个独立根 `artifacts/recipe-execution-008/Q01-api-rejection-v2/` 核对 409 `RecipeRestricted/ProductPointMappingMissing`、`runCreated=false`，见其 `restricted-start-rejection.json`；早先 400 通用错误包原样保留。两次均使用 `-SkipDesktop`，未启动业务也未证明正式前端。结束后只核对并停止本次两个 Host/VirtualPlc 进程组；端口无遗留监听。Q01 点位/高度仍未确认，正式页面也未就绪，故 **007 T031 保持部分完成**，不将受限准备结果称为可执行 Q01。

第三批将Q01 Test媒体清单改为按A/B相机选择两张不同且可读的模拟PNG，清单SHA-256 `125FBC678EE1363271710B31755E8998A00F9B8B99CC99C7A13AE0D5A74FD570`；公共3D/F仍按原角色文件。`Q01CameraMediaTests`实际触发两次4秒文件采集并核对各自摘要，1/1通过。`simulate-station01-load.ps1 -PrepareOnly`按原FixtureManifest在`artifacts/recipe-execution-008/third-batch-prepared/load-s01-007-3b4624f53363464598793132837cc00a.json`生成2.0请求，未发POST、无receipt。Q01仍Restricted，T031不勾。

## 第五批当前状态

用户授权的版本化Test映射使Q01/Q02目录在完整配置与虚拟设备环境中实际Available；两份FixtureManifest、素材/摘要、独立根与端口经`scripts/verify-q01-q02-test-page.ps1`准备并供正式WPF加载。`PrepareOnly`仍只生成请求，正式启动由页面点击；脚本关闭外部自动取盘，仅采证器模拟人类鼠标/键盘操作。正常运行包和历史拒绝/失败包见[008第五批证据](../../008-recipe-driven-inspection/evidence/fifth-batch-q01-q02.md)。T031的清单、隔离、拒绝和页面可用准备交付齐全，现勾选；这不把PrepareOnly算成业务完成。以上旧段是当时状态，保留追溯。
