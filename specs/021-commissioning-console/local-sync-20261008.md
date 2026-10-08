# 本地服务器同步中控机进度

开始日期：2026-10-08；核验收口日期：2026-10-09（Asia/Shanghai）。用户授权将中控机最新代码、通信协议、需求和验证记录合入本地 `E:/dzk/gaode-1`，并推送到原仓库的新同步分支。

## 来源及合入方式

- 中控机仓库：`https://github.com/yyh0513/gaode`。
- 来源分支：`020-real-device-commissioning`。
- 来源提交：`56141cbfdbeb6c0b33addc583ba2c102d8d556cd`。
- 本地合入前提交：`ab13a5fe3adbc8f79d34daacd97f6f538ed09ec9`，原 `main` 及 `migration-20261006-r1` 保留。
- 目标分支：`sync/020-real-device-commissioning-20261008`；目标仓库：`NickYoung618/gaode`。
- 两仓库历史独立；中控机 `source-baseline.json` 记录来源为上述本地提交。采用保留双亲历史的合并，来源仓库提供的文件以 `56141cb` 为准，本地独有内容保留。
- 合并初检：来源1,758个文件全部与暂存区Git对象一致；本地独有1,739个文件全部保留且Git对象未变。README另加本地入口，本记录和本地验证属于同步增量，不修改上游历史验证结论。
- 原型归档、来源文档、26张既有JPEG/PNG、本地现场数据及忽略文件不改写。中控机省略这些图片用于源码交付，不解释为删除本地资源。原仓库已经跟踪的图片保持原对象，没有新增或重新生成图片。
- 最终复核：来源文件中1,750项完全一致，8项差异仅为README及7项通信测试装配文件；本地独有1,739项全部保留，其中9项测试按新接口调整。95项受保护资料/原图片的Git对象均与本地基线一致。产品源码、配置、维护脚本、独立联调工具与中控机提交逐字节一致，详见[一致性记录](evidence/local-sync-20261008/source-parity.json)。

## 当前需求、协议与实现

| 变化 | 当前来源及本地实现 |
| --- | --- |
| 真实相机采集与媒体持久化 | 019规格/采集合同；Galaxy与CameraPro独立相机进程、采集关联、媒体保存及生命周期恢复源码完整同步 |
| 现场PLC地址、类型、编码及报警解释 | 020 `site-plc-adaptation.md`、017地址合同；共享85点来源保留，现场模型REAL、报警MB6056/6058只读及布局适配代码同步 |
| 动作完整闭环后双方清零 | 020 `closed-loop-handshake.md`；PC只清自身请求，必须读取PLC新鲜全0才释放相关资源；同坐标沿用依赖本连接真实闭环记录 |
| 启动、复位与停住 | 021 `start-and-completion.md` 的R2/R3增量；MB2007在首组实际运动完成并清零后清除；复位前PC就绪MB2006=1、软停MB2008=0并新鲜读回，再发MB2009；本次MB6015须0→1且XYZ零位有效 |
| R4全部复位后的旧任务处置 | 同一合同R4增量及 `site-operations-confirmed-20261008.json`；有确认来源才核验恢复，旧执行/资源退出后真实SQLite保存Cancelled和恢复事实，再释放对应占用，人员显式启动完整新轮 |
| 软停及心跳断线 | 按最新确认停在当前位置，不自动回位、复位或续接未知动作；原报警/安全准入继续生效 |
| 多配方联调及前端 | 020/021规格、共享合同和012接口增量；真实PLC/相机、虚拟算法/光源、配方F定位、运行冻结、七格当前Run媒体、受控身份和原请求只读查询同步 |
| 两轮运行与成组成员 | 跨Run F采集隔离、阶段幂等键命名空间、正常完成释放、按成员选择分拣夹爪及相关验证同步 |
| 独立联调工具 | `tools/plc-commissioning`源码、合同和测试同步；工具通用手工写点不能当成正式Host的启动/复位握手 |

旧来源原件、历史规格与失败证据保留。020/021文档内部按日期追加的确认覆盖对应旧待确认项；本地不得继续仅按旧011/017摘要推断现场行为。未独立修改业务合同、PLC点位、原任务勾选或中控机产品实现。

### 完整工程的测试合并

中控机精简快照把PLC测试夹具放在项目根目录，本地完整工程在 `Devices/ProtocolTcpFixture.cs` 和 `.Formal.cs` 已有同名部分类型。直接合并产生重复类型/成员编译错误；只将引入的夹具命名为 `CommissioningProtocolTcpFixture`，同步6个引入测试文件的类型引用，沿用本地已有 `CommunicationTcp` 串行集合。本地历史测试及其夹具不删除，新测试的步骤和断言不改变。

中控机测试项目还省略了完整工程需要的 `Shared/ApprovedTestRoot.cs` 编译输入和 `ProtocolOracle` 资源复制。本次恢复这两项，使保留的本地测试仍可编译。上述测试装配与README是相对上游的明确增量；产品源码、配置定义、协议和上游规格保持原字节。

完整解决方案构建还识别到本地旧测试替身未实现新增的启动回执查询、最终完成核对、原子相机采集及光源来源接口，以及Test配置对现可空Simulation的旧引用。只同步9个本地测试/支持文件：无启动回执/持久终态模型的替身明确拒绝不支持的调用；Test必须实际提供Simulation；相机替身按新的配置后触发接口及声明帧元数据验证顺序，虚拟光源不能报告真实应用。没有给产品接口增加默认成功或兼容实现，没有删除或跳过历史测试。构建中出现的重名局部变量已修正，最终完整后端构建0警告0错误。

## 本地环境与验证

本次只进行源码构建及隔离软件验证；测试PLC为loopback夹具，不连接现场设备。SDK采用本地既有交付文件，通过构建参数提供路径，不把中控机绝对路径替换成另一套硬编码，也不上传SDK。

| 本地检查 | 结果及范围 |
| --- | --- |
| 完整后端 `dotnet build backend/Gaode.slnx --no-restore` | 通过，0警告0错误；包含本地保留的合同、集成、通信及架构测试项目编译 |
| 桌面程序构建 | 通过，0警告0错误；首次 `--no-restore` 因本机无assets失败，正常还原后构建通过 |
| 相机进程及部署准备工具 | 均构建通过，0警告0错误；仅编译，无设备发现/触发 |
| 前端构建、类型检查 | 均通过 |
| 前端既有核心/联调组件 | 9/9通过，0跳过，包含恢复3项 |
| 客户原型精确校验 | 通过：3页、70项资源；原归档SHA256保持 `3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0` |
| PLC现场布局、双方清零、启动/复位及R4恢复 | 40/40通过，0跳过；真实loopback TCP和SQLite，见本轮 `plc-recovery.trx` |
| 相机生命周期/媒体、配方、身份及流程组件 | 54/57通过，3失败，0跳过；失败3项单独复测仍未通过，详见下文 |
| 本地原有合同测试的受影响集合 | 26/35通过，9失败，0跳过；旧整盘流程夹具8项未取得ReadyForRemoval及依赖事实，旧未知媒体测试1项在3D观察被拒绝，保留结果待后续按当前合同核对 |
| 本地相机适配顺序测试 | 1/1通过；验证新原子配置采集接口及虚拟光源来源 |
| 本地原有架构门禁 | 73/82通过，9项因旧扫描器无法展开相机项目的 `$(CameraProIncludeRoot)/*.cs` 中断；不能宣称门禁全部通过 |
| 独立联调工具定向测试 | 16项中13通过，3项在测试退出清理临时日志目录时发生WinError 145；不计作全部通过 |

原中控机验证是来源证据，本地结果另行记录，不能混为本机或真机实测。定向结果文件、摘要和文件一致性核对见 `evidence/local-sync-20261008/`；完整本机日志、SQLite及初次失败产物在 `artifacts/sync-020-20261008/`。

相机编译使用 `-p:GalaxyWrapperPath=E:/dzk/device-commissioning-20261002/src/vendor/GxIAPINET.dll` 与 `-p:CameraProIncludeRoot=E:/dzk/device-commissioning-20261002/vendor-extracted/threeD/app/CamSDK/CamSDK_CSharp/include`。Galaxy封装SHA256为 `EC679FB2D2335208B140E4B25AF45640D6BA5978143E425C16831FF7F54B021C`。这些是本机现有SDK位置，不代表中控机部署路径，也未写入产品默认配置。

### 本轮尚未通过及待办

- `CommissioningIdentityTests.BackendConfirmsIdentityAndEnforcesPermissions` 两个角色均在最后一条带 `access_token` 查询参数的拒绝验证中收到 `HttpIOException: ResponseEnded`，尚未确定原因。前面的身份、角色权限和CORS断言已执行到该位置，整个用例仍按失败保留；未放宽认证断言。
- `CommissioningWorkflowTests.LegacyTestHostRunsPublicPreparationBindingFlipAndDurableWholeTrayWorkflow` 两次运行都因XY动作未知/超时进入RecoveryRequired，未完成本地正常两轮。首轮日志定位到F定位等待；未提高动作期限、跳过握手或把中控机历史通过当作本地通过。
- 工具的 `test_initial_same_coordinate_requires_normal_motion`、`test_manual_coordinate_mismatch_clears_without_position_eligibility`、`test_recipe_all_axes_reused_sends_no_axis_writes` 在 `asyncTearDown -> TemporaryDirectory.cleanup` 出现目录非空错误；保留 `tool-r2/tests.txt` 与对应通信证据。未修改工具产品源码或吞掉清理错误；作为非阻塞的本机测试清理问题记录。
- 保留的本地历史合同测试有9项未通过：`WholeTrayWorkflowOrchestratorTests` 的8项及 `CaptureCoordinationTests.ActualUnknownMediaSourceIsNotFilledFromSimulationFixture`。本次只补齐接口编译和Test输入非空要求，没有改断言、补假完成事实或重写旧流程测试；后续应按当前019/020/021合同核对测试输入与真实缺口，不能直接认定全部是环境问题或全部是产品缺陷。
- 旧架构扫描器把SDK属性及通配符Compile条目当作字面路径，导致9项门禁中断；新019/020/021模块及合同也需在后续定向维护既有边界清单。未通过排除相机项目、关闭规则或自动批准新公开形状来制造通过结果。
- 首次自定义工具测试选择器误选了被设置为None的继承测试入口，47项/32错误结果保存在 `tool/result.json`，不能作为有效16项集合的结果；随后改用标准unittest加载器取得上表16项结果。所有实际失败记录均保留。
- 相机组件测试有两项向其固定历史日志路径追加输出，已先保存到本轮 `components/source-path-logs`，再恢复源提交对应的历史证据字节；最终一致性检查涵盖这些历史文件。

本地Spec Kit功能选择已从 `specs/016-plc-field-ui` 更新到 `specs/021-commissioning-console`，`check-prerequisites.ps1 -Json -PathsOnly` 已验证解析到021；原选择备份在 `.git/sync-020-20261008/feature-before.json`，保留该文件原有只读属性。该选择为本机状态，不提交身份凭据或运行配置。

## 保留的限制与回退

- 中控机R4已部署的说明按上游 `github-delivery-20261008.md` 保留；本次不重打或替换中控机部署包。
- 020 T055/T056、实际桌面恢复交互及真机完整流程仍按来源记录未验证；本地软件通过不关闭这些条目。真实算法/外部光源不由虚拟联调实现替代。
- 本地缺少中控机私有身份、现场运行库和完整安装配置时，不把源码同步视为可直接启动真机。
- 回退源码可切回原 `main` / `migration-20261006-r1`；切换前先保存新增修改。当前同步分支保留，历史证据不删除。本次未迁移或修改运行数据库，不自动恢复任何旧任务。
