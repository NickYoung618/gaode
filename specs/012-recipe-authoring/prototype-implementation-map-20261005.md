# 已确认原型到本轮实现及真实页面对应

基线只读：C:/Users/codexsandboxonline.10_3_0_13/.codex/visualizations/2026/10/05/01a109ca-eecf-74c2-9bfc-0335d8594c5f/recipe-layout-10x10-review.html。源ZIP/a/data-view/login只读；具体SHA见既有prototype-baseline及本轮delivery manifest。以下差异均来自用户已确认内容，不以自身摘要批准新差异。

| 原型位置 | 实现位置 | 操作保持/准确授权差异 |
| --- | --- | --- |
| 基础信息→坐标配置→检查保存 | 原a.html配方弹窗；recipe-authoring.js render | 原三步骤、深色UI、无新页；正式新建/全读/编辑/保存反馈 |
| 首步底部料盘矩阵 | renderMatrix(editable) | 10×10手选NG/OK/Pending；同区取消/换区只清本格，数量统计；不按数量生成矩形 |
| 第二步料盘总览/当前格 | renderMatrix(false)/selectedCell | 同100位置和空位，三区独立row-major从1，未选格不可填；高亮与区域/号/行列联动 |
| 点位及拍照卡 | displayedCards/editCard/editCapture | 直接XYZ；逐对象/Stage/实际相机曝光、增益、亮度，隐藏共同字段完整保留；不加ROI/模板/高级设置 |
| 原型OK放置任意目标含义 | 原槽放料卡与OriginPutBack | 已确认差异：特殊固定回自己的原始OK格，取放Z不擅自复制；普通OK不额外搬运，无选择开关 |
| 特殊旋转类型 | 场景1条件字段 | 两用途明确夹爪、共同工位/两组角；普通无旋转专属配置，重复组仍独立 |
| 概览/缺项定位/返回编辑 | summary/locate/review | 填写标记不代表保存/检测/设备到位；实际后端检查/save/GET |
| 原页头设备状态 | 既有行stationOperationalStatus/dot | 006已授权的API/通知数据绑定，删除固定检测中，布局不变；不从质量或计划推断阶段 |
| 成组成员/半成品部位 | 当前格详情的成员/检测部位→面→实际相机卡 | DUI02/03于2026-10-06批准；按原预览实施与当前实页验收，半成品整体取放隐藏部位导航 |

真实页面：page-current-render-positions-20261005的三步/新建操作及全保存；page-header-current-space-fixed-20261005的当前三步/后端无run页头。actual render-review包含完整100位置/空位/顺序及禁止技术编辑器检查，组件负例拒错序/重复/可填空位/技术字段/嵌套编辑器。同run代表链已按continuation-20261006-index完成；新导航证据仍须本次实页与保存重读，不借旧图通过。


## 2026-10-06获批导航的实际对照

DUI02预览detail标题→“成员”choice rail→face rail→实际相机卡，对应recipe-object-rail/data-object-material及data-local-face，料盘栏不再放成员按钮。DUI03同位置“检测部位”，photo/E使用部位，flip使用整体并隐藏两层选择。两预览左用途栏由实际适用配置显示拍照/翻面/适用E/NG-Pending，不复制示例名称、数量或AB。无新增按钮类别/字段/步骤。

实页与逐控件/100格DOM对照：navigation-approved-20261006-03两类型photo/face2/flip/reopened及三步截图；navigation-cells-20261006-04跨真实格/区域返回。已实际查看成组photo与半成品整体flip图片；完整参数与SQLite对账见navigation-implementation-20261006。原代表链运行页面账按原范围复用；新导航不是旧截图代替。
