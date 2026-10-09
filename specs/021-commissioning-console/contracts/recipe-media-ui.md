# RM-021：配方、媒体与原型绑定合同

2026-10-08；FR-005–008/012–016；用户本次七格答复为显示映射依据，不改变采集工艺或物理相机绑定。

## RM-01 必要参数归属

复用012 editor-draft/editor-layout/validate/完整GET/POST/带If-Match PUT及实际SqliteRecipeStore；采用统一RecipeDefinitionSerialization和共同校验，不另建页面正文/工艺模型。

| 020参数 | 页面/后台归属及实施核对 |
| --- | --- |
| P01/P02 | 既有基础字段、10×10区域/物理槽、成员/面/相机组。后台唯一来源与layout提供关联；不用空稿或默认数量造实体 |
| P03 | 每实际点/阶段/相机卡片的XY及检测Z；PointRef/Stage/来源/单位完整保留，保存重读并核执行请求 |
| P04 | 翻面取放XY由既有卡片；隐藏Z及姿态Profile/Version/Pose由后台来源。现场REAL型号取020机械程序ModelNumber，与显示PlcRecipeId分离 |
| P05/P06 | 分拣源/NG/Pending/原槽、适用特殊工位与角度、显式抓手。沿共同校验，OK代表路线不能删除未走分支要求 |
| P07 | 每张曝光/增益/亮度独立局部profile；缺基础profile不能出现输入已改但正文未改的假成功。ROI/通道/等待/算法引用保持后台归属 |
| P08 | E扫码坐标沿既有字段，F定位依受控3D结果，页面不新增固定F坐标或强制E动作 |
| P09/P10 | 公共位置和后台机械/能力/预算/用途/来源各自归属；公共Z保持020限制，不新增技术控件/配方批准 |

新用途保存成功即可选；首次GET取得的原ETag用于本次编辑，冲突不得偷取最新ETag覆盖。未编辑隐藏字段完整保留。编辑/保存失败或CommitUnknown不得宣称成功；通知只触发重读。旧运行页头使用冻结引用，不使用正在编辑版本。

## RM-02 七格与媒体

| 原型格位（从1编号） | 保持的原型名称 | 业务相机 | 当前媒体判据 |
| --- | --- | --- | --- |
| 1 | 密封面环面检测1 | C | role=Detection、businessCamera=C |
| 2 | 密封面环面检测2 | D | role=Detection、businessCamera=D |
| 3 | 密封面孔底检测1 | A | role=Detection、businessCamera=A |
| 4 | 密封面孔底检测2 | B | role=Detection、businessCamera=B |
| 5 | 来料状态2D检测 | E | role=E、businessCamera=E |
| 6 | 来料状态3D检测 | 3D | role=ThreeD、businessCamera=ThreeD（按现公开序列化） |
| 7 | 读码参数 | F | role=F、businessCamera=F |

从现`GET /api/v1/station01/runs/{runId}/media`读取同Run已提交目录，再用现受认证media/{mediaId}读取文件。以上相机关系是显示配置，不是设备地址或采集就绪来源；相机硬件/序列号仍由019/020绑定。

候选必须匹配本Run/角色/相机及有效MediaId/CaptureId/committedRevision；保留对象/槽/成员/阶段/面及采集轮次。不跨Run借图。沿既有点击切换交互展示同角色多次采集，默认最新已提交可读项，切换到固定媒体后不让迟到刷新替换所选项；换Run清选择/旧URL。真实Camera、模拟Algorithm/Light及未知来源分别显示在既有来源区域，不增加标签控件。

没有目录项显示未参与/未采集；提交后不可读显示读取受限。主路线没有E就不填第五格，七机独立现场验收不能伪装本Run采集。Test可保留原显式临时映射证据，联调采用本表而非Test散列。

### I1：映射验收用途隔离

V05旧Test Run及其临时映射只能证明原声明范围。V06由离线夹具单独建立Commissioning Run，经实际隔离采集、共同媒体保存/提交、正式受认证目录/文件/Run GET及新用途页面，验证本表。至少核各角色对应、同相机多帧、跨Run隔离及未参与留空；来源明确OFFLINE，不能直接写假目录/响应、重新标记旧Test Run或借历史图。此为媒体组件贯通，不造物理动作、质量/Final成功，不新增生产采集旁路，不改变E是否参与工艺。场景/Runtime限制按实际子项保留，不把组件证据当桌面/整机通过。

## RM-03 运行、数据页和取盘

运行阶段、结果、来源矩阵及最终事实取现runs/{runId}和/evidence；通知仅触发重读。现data-view.html包含mock产品、趋势及随机报告编号，联调绑定时必须清除其演示数据路径：仅把有权查询的已知Run及其已提交结果/媒体呈现到现有表/详情；缺指标/规则/历史范围显示未提供/查询受限，不在页面重算质量结论或生成随机数据。

本轮不新增全库历史搜索、趋势平台、复检接口或报表导出。既有查询/复检控件无对应有效后台能力时保持明确受限，不执行原型mock处理器。已知Run引用可跨页传递非秘密ID，不能传token、权限或完整结果作为事实；每页重新鉴权、查询。人工确认按[启动/终态合同](start-and-completion.md)，等待人工、卸料准备、质量OK、Final保存分别呈现。

## RM-04 原型授权与精确核验

归档只读`E:\dzk\gaode\原型.zip`，SHA256=`3dc791c1f8ab5eedfa037f5dbae450b2d20522fed654f86ea700c0284945e1e0`，三HTML：login.html/a.html/data-view.html。保持已授权012/016的结构/布局/样式/文字/控件；仅修改必要脚本的数据、身份、状态和错误绑定。

实际资源源为frontend/src/pages，build.mjs生成prototype.html作为a.html的既有别名。根部旧副本不作为新运行入口，不能只改TS辅助文件。现recipe-authoring-012-differences.json继续保留原授权条目；实施时增加有FR和本合同引用的021具名脚本/资源差异及新摘要，并由verify-prototype.ps1验证精确源/构建/别名和资源。不得关闭门禁、重做归档或只更新总摘要掩盖未列出的页面变化。


## RM-05 用户明确授权增量：可配置配方与虚拟光源

本次用户决定优先于RM-01“亮度始终必填”及RM-04“只改脚本不增控件”的旧范围；只对既有配方弹窗增加“虚拟光源”复选框及亮度字段状态作定向例外，不修改归档。光源模式随正式配方读存与冻结，虚拟跳过光源部分、相机曝光增益继续。多配方虚拟输入、前置公共3D/F及实际证据要求见[CC-021](configurable-commissioning.md)。准确共享版本在T033同步后改代码，页面不能自行造成功。

实施核对修正：操作员须能经现配方目录读取并选择已保存配方，以完成正式启动；可打开既有弹窗只读选择，不能新增/编辑/校验保存。工艺工程师仍有编辑权限。禁用整个配方按钮会同时阻断操作员选用，因此改为限制编辑控件；后台权限不扩大，GET仍Run.Read。V01旧“操作员按钮禁用”证据仅属旧范围，新只读选择另验。

## RM-STAGE（2026-10-10）
消费022的stage-result/1查询投影，字段定义见022/data-model.md与C022-STAGE；沿现Run/evidence/media出口，不新增页面接口旁路。technicalOutcome、qualityDisposition、completeness、flowOutcome、executionPolicy及actualDisposition分别映射现有运行/数据/详情区域，不由分拣目标重算质量。
原始回复和输入通过后端受权引用，不能把包本地路径暴露为可直接读取的URL；缺字段为未提供/历史缺项。Pending/Uncalibrated/NotConfigured与技术错误原样绑定。七格和同Run多次采集仍用本合同RM-02；没有控件承载的复杂几何仅保持后台可查/局部显示待办，不增加布局或mock成功。
