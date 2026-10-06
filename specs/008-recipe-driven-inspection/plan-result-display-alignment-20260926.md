
# RES-01—RES-04结果展示设计交接

2026-09-26；仅speckit-plan，功能008-recipe-driven-inspection。依据HMI-003、DAT-004、008 FR-016及006 FR-010，不重写规格、不新增业务。setup-plan确认既有008并跳过模板覆盖，feature.json沿既有配置解析；before/after_plan无hook。技术栈沿现有.NET Host/SQLite事件、独立worker、runtime.js和WPF/WebView2，不引入平台。

## 研究决定与实际依据

- 决定：扩展同一GET投影，复用现有事件/调用/媒体；不新增API/结果库。理由：QueryEndpoints已有results，TraceWriter保存WriteBatch payload，IntegratedDetectionPort保存单图及融合事实。排除仅前端改标签，因为项目/面关联和空值语义未齐。
- 决定：disposition用于业务质量，quality/source分别用于事实属性/来源；判定已提交即可展示，不能等Final。理由：RunResultProjection现有实际字段如此定义；对象/面/项目汇总不由前端推断。
- 当前worker适配只取disposition，没有置信度/缺陷坐标；单图AlgorithmFact保留DetectionDisposition/RawCodes/ErrorCode，融合保留输入及面。选择明确“未提供”，不造新算法字段值或演示数据。业务项目名称不足时展示真实能力身份和项目未提供。
- QueryEndpoints现只从Detection Completed取对象结果，部分层级限定Group/Assembly，completeness有默认Complete，coordinator无内存run会404。设计分别要求已提交子结果即时投影、完整性有依据、持久历史只读查询；不恢复执行。结果新增提交必须影响ETag。

历史核验：run `0551aa03-dd36-4cc9-bcf7-cbab99b728a8`的[后端记录](../../artifacts/recipe-execution-008/page-q03-sorting-interactive-20260926-v2/Q03-Pending/page-api-device-facts.json)返回Pending/Simulated/Derived；[页面记录](../../artifacts/recipe-execution-008/page-q03-sorting-interactive-20260926-v2/Q03-Pending/recipe-webview2-page-evidence.json)末尾verdict为“完成”，items含Pending；[原判定](../../artifacts/recipe-execution-008/page-q03-sorting-interactive-20260926-v2/Q03-Pending/case-result.json)为FinalPageDisplayed。保留其到Final的原范围，不能抵本次质量区域验收，也不是用户最新包复现。

## 设计输出与归属

| 问题 | 唯一设计/实施落点 | 下一轮任务建议（本轮只读） |
| --- | --- | --- |
| RES-01 判定错绑 | 006 runtime.js真实入口及既有DOM值；流程、质量、完整性、处置、Final分离 | T049加入当前对象质量及缺失事实绑定，不新建任务 |
| RES-02 明细契约 | [008字段表](contracts/api-results.md#res结果展示增量2026-09-26目标尚未实现)、[持久模型](data-model.md#res结果展示的持久事实边界2026-09-26)、[003公开查询](../003-plc-latest-protocol/contracts/station01-main-flow-api.md#res已提交结果查询增量2026-09-26目标)、[006逐区域映射](../006-frontend-station01-console/contracts/prototype-mapping.md#res真实结果字段映射2026-09-26设计未实施) | 008 T054保存及关联，003 T068投影/ETag/历史读取，006 T049消费；无前端直读 |
| RES-03 验收 | 下节最少代表及同run持久/API/页面对账 | T049为页面绑定主归属；008 T055/T059等对应OK/NG/Pending原路线复用，T069新恢复复用，不另跑重复整链 |
| RES-04 实际资源 | build.mjs复制runtime.js；HostRuntime资源解析与ResourcesResolved | 007 T033既有manifest能力复用，006 T049附实际加载摘要；本轮不改007文档/任务 |

实施依赖：冻结契约→下一轮细化既有任务→008 T054必要事实子能力→003 T068投影→006 T049→既有适用路线验收。003可先交查询结构/读取基础，不要求保存任务等待页面；008 T054与003 T068按子交付衔接不整任务互等。USR-E协议/目录工作保持原归属，本增量并入其页面验收；恢复仍003 T072-A＋001 T078→001 T052→008 T068→003 T072-B→006 T051→008 T069→008 T070，所用T049子能力先交，不等全部Q/C/F。

## 最少必要验证设计

1. 沿既有合法Test选一个OK、一个NG、一个Pending代表，不重跑22条；每个记录正式页面选用/启动/必要确认、run/对象及适用面/项目、API结果、持久引用和实际资源摘要。组/整体如有新增映射差异，用现有事实/接口对照核身份，只有实质运行差异才增补，不扩矩阵。
2. 判定区必须等于同对象已提交disposition；NG/Pending到Final后仍保持质量，流程Final在原状态位置。已提交面/项目在Detection未结束时可显示，未产生对象汇总不得补成OK/Pending。
3. 对适用检测项目、原因、已提供参数/缺陷/媒体逐项对账；当前worker未提供置信度/缺陷坐标时验明确缺失和无演示值。必要保存失败或未知提交只显示受限，不凭内存填已提交结果。
4. 通知/轮询、页面刷新/重开、既有对象/媒体切换后同一结果保持一致；旧run回包、其他对象面/项目不得覆盖新焦点。API持久结果版本变化不得被304遮蔽；Host重开历史查询只读，不触发设备或回填历史。
5. 保留取盘allowedActions、解锁、保存和Final门禁；必要回归结合原路线，不增独立第二套页面整链。旧FinalPageDisplayed不抵本次质量展示验收。失败保留新证据包并精确定位算法/保存/投影/绑定/资源层。

## 实际运行资源核验方法

后续实施时复用现有采证脚本，先记录进程PID/启动命令、WPF EXE与Host DLL绝对路径/版本/SHA256、Provider及实际配置/配方摘要（脱敏），再读取本次桌面ResourcesResolved.resourceRoot/runtimeSha256及WebView版本。资源优先级：GAODE_FRONTEND_DIST→EXE旁frontend/dist→开发回退目录。对该目录runtime.js、a.html/data-view.html及实际脚本引用取摘要，与本次build.mjs产物和src比对；不能只看仓库dist。

build.mjs当前直接复制src/runtime.js到dist/runtime.js，再由桌面项目打包复制；所以同次该脚本产物应一致，但不同摘要只能说明当前文件不相同，不能独自证明某个进程加载旧包。历史上述run的ResourcesResolved摘要为ED3067768291E73DE78FB9F0AA10FE23791BB9610ACF5957BD0682A13DA37B04，路径为项目frontend/dist；当前静态src为C98AB5506E6A33548BFD5A45FE8CE4B2A4E4D1890E1E1797DFD3998D25EECFC3，dist为2072C88FEE8C72F5F9E55775BC69DC21AEF27B5951AA525545211DECAE87A166。它们是各时点事实，不等于用户最新运行已复现。页面截图/DOM、API响应、持久结果引用须与该次加载记录属于同一个run证据包。

## 保护、限制和停止点

仅修改003/006/008直接contracts、plan、008必要data-model及本交接；未改spec业务、tasks/勾选、代码/dist、配置、数据库、需规、来源和历史证据。项目外临时目录保存修改前文档、SHA256、差异和核验记录；写入前逐文件校验并发变化。既有文档正文保留，字段歧义在原A03处直接澄清并链接增量。无新增或修改Mermaid图，无需渲染。

没有需要用户重做业务确认的设计阻塞。仍需实施核验的项：用户最新包实际加载资源、持久字段完整性、当前worker可提供的明细、历史缺字段范围；缺事实按合同表达，不造值。生产原局部限制、PARAM Failed/尚未复验、质量清单15/16保留。USR-E六项及3＋1、USR-D和RST关闭结论不变，VirtualPlc延迟模式不推进。

本轮只补设计；RES-01代码未修复，RES-03页面未验收，RES-04最新运行未核验。不执行tasks/analyze/implement或构建/业务测试。
