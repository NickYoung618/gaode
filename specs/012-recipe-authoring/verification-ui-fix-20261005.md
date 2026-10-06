# 012增量软件验证（2026-10-05）

实际根：`E:/dzk/gaode-012-ui-fix`；显式功能目录 `specs/012-recipe-authoring`。本轮顺序完成定向specify、plan、checklist、tasks、只读analyze和实施；未重建旧012、未改主项目/013、未执行Git写入或合入。原25项只证明旧范围；本轮为T026—036。

## 当前结论与证据边界

本增量软件实现和必要验证完成，等待逐文件合入。实页为编译后的原弹窗，通过真实Host API、唯一共同校验和独立SQLite保存/重读；相机和光源端口的组件替身仅用于参数调用证明。没有正式PLC互通或真机参数生效证据，分拣夹爪未增加PLC信号。

基线记录见 `artifacts/recipe-ui-fix-012/baseline-manifest.json`（1409文件，包含一份单独补齐并核摘要的既有注册脚本）。构建和运行通过 `scripts/verify-012-ui-fix.ps1` 在启动前及运行中只读核013测量锁与所有者；仅管理本副本子进程。独立端口16531—16533和数据库在本副本。未把进程存在当作测量窗口，未终止013进程。

## 实际执行与有限复用

下列路径均相对 `artifacts/recipe-ui-fix-012`；数量按各证据范围列示，重复补跑不累加成覆盖数量。日志和失败记录保留在 `verification/`，TRX在 `backend-tests/`。

| 范围 | 当前证据 | 结果及适用范围 |
| --- | --- | --- |
| 受影响后端 | verification/backend-build-10；contracts-build-01；rules-build-01 | Integration/Host/StorePrep、Contracts、Rules实际构建成功，0警告/错误；未变Contracts/Rules源使用相应构建 |
| 前端 | frontend-build-07、frontend-typecheck-01 | 最新三步弹窗源构建和类型检查通过 |
| 实际存储、保护、适配组件 | backend-components-01 | 13通过、0失败/Skip；真实SQLite保存/完整读取/重开、版本/夹爪/关联拒绝、F精确匹配/冻结、相机光源参数调用。不变存储/适配/匹配实现复用；后来调整的有辨识参数样例由api03两项PerCapture再次通过承接，新建映射按下面最新证据，不声称整机或硬件通过 |
| 新建后台映射/成员与面关联 | editor-api-02、editor-api-03、group-editor-04、editor-projection-05 | api02四项通过；api03六项通过、一项测试JsonNode所有权错误；修后group04一项通过，最新projection05两项通过。覆盖正式HTTP新建草稿、真实保存的两成员两面八项参数规划及翻面隐藏Z保真；失败未删除 |
| 共同采集入口 | capture-entry-01 | 12通过、0失败/Skip，包括实际既有executor九个A/B/C/D/E请求参数逐项核对、共同基础和序列化。设备接口为明确组件替身，非真机 |
| 当前弹窗/原型拒绝 | frontend-final-02 | 14通过、0失败/Skip：12 authoring组件、原型正例及负例检查；五个未经授权页面变更拒绝。包括不同对象/成员/面/拍照项/相机及共享引用拆分，不松绑断言 |
| 既有运行页绑定 | frontend-components-01及frontend-reference-03 | 初轮45项44通过、一项既有脚本版本引用失败。两个启动脚本引用改为基线有效3.0.0后，仅补该一项通过。运行33项未变范围复用，authoring以最新final02为准 |
| 实际渲染和真实保存 | rendered-page-09、rendered/page-09/page-evidence.json | 三步实页与只读V3三步同次截图；实际编辑/新建保存、完整读回、过期If-Match 412、正常Host重启后完整定义一致。DOM注入额外技术字段和嵌套编辑器均拒绝；无假API/localStorage保存 |
| C#009/010受影响门禁 | boundaries-03及boundaries-01未变负例范围 | 最新4个仓库检查＋5个现行业务规则共9通过、0Skip；初轮另13个负例已通过，规则/负例输入未变，仅复用该范围。初轮B01无类型HTTP字段失败已由正式EditorLayoutRequest修复，不改扫描规则 |
| 脚本009/010 | script-boundaries-04.json及verification/script-boundaries-04 | 172文件实际解析、6正负例、0错误/违反，Passed；不是零发现放行。PowerShell UTF-8输出修复及遗漏注册脚本补齐后重新核当前源码 |

不执行全量测试、面数组合穷举、009/010全历史、整机或另一同义完整链。完整运行链旧证据仅沿旧范围，不用于证明本次新参数的真机生效。本次参数用实际共同planner/executor入口和适配器组件证明。

## 两项新增的数据路径

逐次参数：具体槽位/成员的 `CoordinateDefinition.CaptureProfile` 或E `PurposePoint.CaptureProfile` →已有 `CaptureProfiles` 三参数 →完整RecipeDefinition API →共同 `ValidateForSave` →SqliteRecipeStore正文3/合同1.4 →完整GET →共同精确Matcher/深冻结 →RecipeRunPlanner既有步骤 →既有CaptureRequest →CameraCaptureAdapter的ConfigureAsync/SetBrightnessAsync →原触发入口。

单相机修改按对象、成员、面/扫码姿态、实际项及相机拆分引用；共享后台参数的有效ROI/光源通道/等待仍保留，不展示。A/B、C/D和E值独立；E仅扫码。新正文不得回退公共参数，历史正文2沿合法既有规则读取。

夹爪：基础一次显式选择 →共同 `SortingGripperId` →单一校验1/2 →真实SQLite/完整GET →统一DefinitionDigest和冻结RecipeRunPlan。历史null不补1、不写出新增null改变旧身份；新写缺项/0/3拒绝。原运行冻结值保持，后续F读取新提交内容；生产准入限制未提升。测试中临时批准仅组件规划所需，未写入真实保存记录、不能代表正式批准。

## 实页对应与清理

实现前对应表和授权差异见 [ui-fix-20261004.md](ui-fix-20261004.md)。实际对照截图为 `rendered/page-09/01-basic.png`、`02-coordinates.png`、`03-check-save.png`；只读原型为同目录 `v3-reference-*.png`。基本双列与概览、区域/对象/面三栏、拍照/扫码/取放卡片及检查摘要实际渲染；翻面取放只显示XY，原后台Z保真。批准差异限HMI弹窗适配、已确认业务修正、三参数、夹爪和正式保存反馈，不以自身摘要相同替代原型对照。

核消费者后实际删除：通用递归模型表单；技术配置来源/阶段身份的控件、状态和监听；无消费者的POST editor-stage-identities及请求类型；多余“选用已保存配方”按钮。目录选择承接合法读取意图，GET阶段元数据、完整隐藏正文、版本冲突、取消/期限、提交未知、权限和历史读取保护保留。修改错误测试断言以消费授权界面，补技术字段/嵌套编辑器/关联错误负例；没有按失败删证据或豁免文件。

## 失败保留与明确限制

早期构建xUnit规则/类型条件错误、迟到读取竞态、初次New未回基础页、测试JsonNode父级错误、启动版本引用、B01无类型字段、PowerShell编码、遗漏注册源脚本、浏览器定位/挂载等待/自有进程清理及标签采集错误均保留原日志。修复后只对受影响范围补证；最新实页09和final02为当前UI证据。没有Skip或未跑结果冒称通过。

尚未验证：真实相机SDK参数应用/回读、实际硬件范围、物理光源映射/共享约束及夹爪PLC选择。本次增加最小参数端口，不猜设备值/地址/握手。正式Host的NotIntegratedCapture保持真实限制。

新建程序自动消费当前正式保存记录的同型号/场景配置上下文；没有正式来源、配置有歧义或新增面/成员缺实际姿态配置时，仅该映射拒绝。算法/翻面姿态等不由操作者拼装，不用测试模板兜底；已有保存记录的读取、编辑和无设备保存可继续。上述是部署配置依赖，不增加高级编辑或未授权能力。
