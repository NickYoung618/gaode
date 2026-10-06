# 当前012导航交付（2026-10-06）

DUI02/03已确认并已按原预览实现/实页保存验收；准确证据、清理和复用范围见[navigation-implementation-20261006](navigation-implementation-20261006.md)。最终持续门禁与任务计数见artifacts/recipe-layout-012/navigation-20261006/final-index.json；不从授权/旧阶段记录推断通过。

---

## 此前阶段记录（下文保留原时点事实）

> 当前确认（2026-10-06）：DUI02/03已获明确批准，原预览只读；严格按navigation-approval-20261006.md推进剩余六项。下文此前“待审/未批”是当时记录，不再作为当前阻塞。实施/验收状态以本轮实际回执更新，批准不等于Passed。

# 012本增量当前交付（2026-10-06）

工作目录E:/dzk/gaode-1；显式014/012，feature.json未写入。E盘空间不足后，在C:/gd14v20261005建立了当前主项目的完整独立验证副本，保留源文件/程序集基线和迁移回执；产品修改仍在E，所属文件逐字节同步到C。未用旧012覆盖主项目，未修改015/016在制文件、013性能门槛、现场输入或其他会话进程。

统一证据位于C:/gd14v20261005/artifacts/014-012-joint/，主项目artifacts/014-special-part-rotation/continuation-20261006-index.json提供入口。run-ledger.json/ui-ledger.json以实际SQLite、API、Edge渲染及通知逐项对账；原始obligations/page-evidence仍保持当时“待对账”状态，不改写它们为通过。最终门禁和任务判定以索引的实际结果为准，不预填Passed。

验证进程显式使用DOTNET_ThreadPool_UseWindowsThreadPool=0及DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS=1；最终两链恢复原runtimeconfig ServerGC=true，没有GC环境覆盖。未更改产品或系统默认运行时配置，没有线程数、优先级、周期、重试或期限放宽。这是具名软件验证条件，不证明未采用该条件的环境稳定，也不把软件通过升级为真机/生产批准。曾比较的Windows native pool及workstation GC均未证明能解决全部迟延，未采纳为产品修改。

| 最终代表 | 实际结果 |
| --- | --- |
| special-continuation-20261006-14-first-cause | run668ce643-8550-4500-8d23-4e9c1a813111；真实公共3D/F、实际保存后绑定/全盘冻结、2个实际Worker OK；AB/AB每件4/共8独立captureId及参数/StageId；原槽r2:c4、r4:c6各放料/safe提交后再推进；最后一次整盘下料/Final/人工取盘门，正常重启读取。1发现/1执行/1通过，0Skip。 |
| ordinary-continuation-20261006-05-first-cause | runfebf58b9-72bd-4f25-9ca5-86e55264bf58；正常公共准备/F/冻结与检测、普通OK NoMoveRequired且0额外分拣动作；保持原批次节奏，盘下料/Final及正常重启。1发现/1执行/1通过，0Skip。 |
| 同run页面 | 特殊148个真实网络响应、296条通知、33个同run渲染快照、4次运行截图；普通的实际计数见ui-ledger。两链均0观察器错误，含实际配方GET及三步弹窗/100格/空位截图。观察器仅读取，运行前后配方编辑保存由实际HTTP驱动，独立“弹窗保存→API→SQLite”证据另列，不冒充同run点击保存。 |

冻结后修改实际使用的capture-s1-1-A（500→900）和分拣抓手（1→2），旧运行继续用500和抓手1；模态框读取最新保存版本而运行页保留被冻结版本。原槽put Z52与pick Z50分别配置，未自动复制。实际Media/AlgorithmCalls/StageEvents/WholeTrayCompletions、放料/safe和后件开始时序均在只读对账中核实；不是用质量OK或计划顺序替代物理完成。

当前未决范围：DUI02/03仍未收到明确确认，预览未落实产品，不算导航通过；012涉及导航的整任务保持未完成。正式硬件地址、3D机械映射、安全/固定角/容差及相机/光源实际Applied仍为DEP01—04局部现场限制。真实软件请求携带/虚拟适配消费不等于硬件应用。没有重新开展013性能测量，没有追认其旧目标达标。

并非所有历史迟延已得到Windows底层根因解释。普通03实际X采样间隔约1.70秒未见本次Moving；特殊13真实心跳采样空窗并触发原3秒保护，workstation GC对照没有解决。两次失败及全部旧失败保留。当前代表已在上述实际运行条件下通过，不将后续reconciliation、观察退出或关闭失败冒充最初故障，也不将CPU高或应用写出时点说成唯一根因/内核收发。

已确认核心：同一三步弹窗、实际100格/空位、区域独立编号及稳定关联、正式API/SQLite保存全读/编辑拒绝/重启、真正运行状态消费。runtime.js及实际后端投影在两链中对账；T046可按证据判定。T041/042/043的导航后批、T047导航验收、T048导航替代核查及T049全功能完成均仍未满足，不能记13/13。旧36项不改，清单复选框不改。


---

## 历史阶段回执（2026-10-05，以下原文仅表达当时时点）

# 012本次增量实施交付

2026-10-05；当前E:/dzk/gaode-1，显式specs/012-recipe-authoring；本会话统一014/012。旧T001—T036历史完成事实未改；本次13项的整项状态独立评价。DUI02/03未经确认，不实现预览导航，不称全部012完成。

三步弹窗Core、100格手选/原位空格/三区独立号、按CellId+Region映射/改区局部清、真实API→共同校验→SQLite→完整重读已实现。技术配置来源/Route/Pattern/Stage引用/摘要/准入等操作控件及旧数量克隆退出；有效隐藏数据保存，成员/部位既有能力保留。普通/特殊准确请求SourceRecipeId上下文，与Model/Scenario/UnitKind/InspectionKind/Route一致，不混同型号两类来源。

| 当前结果 | 真实位置 |
| --- | --- |
| 新建/编辑/保存/412/422/重启读取及源同库 | artifacts/recipe-layout-012/page-current-render-positions-20261005/save-read-ledger.json及new-page-proof.json；普通/特殊来源实际准备日志和recipes.db |
| 四次相机三参数不覆盖 | artifacts/recipe-layout-012/page-parameter-sqlite-proof.json；对真实表单输入、API全读及SQLite完整正文逐Stage/相机比对，不称硬件Applied |
| 正常停止/重启 | artifacts/recipe-layout-012/normal-read-02bc7ee239544b079de769ed4183e7d0/normal-restart-proof.json；两次正常Host退出及全读，未执行设备 |
| 当前三步实际截图/页头 | artifacts/recipe-layout-012/page-header-current-space-fixed-20261005；只读复用已验证库，没有重跑保存链，页头后端明确currentRun=null才显示未运行 |
| 原型/组件/构建 | prototype-header-current-fixed.json、frontend-status-binding-components.log 35/35、之前Core组件及frontend-header-types/build；精确差异、未授权区域仍保护 |
| 共同消费 | 014正文4/记录1.5/冻结3及StageId/Origin/safe状态；store/http唯一，前端不匹配/校验工艺/生成执行计划 |
| 同run联合证据 | 014代表链未通过，当前T047.RunEvidence未交付；保存/组件/截图不能替代实际原槽回放或Final |

012/T041 core已交，nav待批；T042/T043/T047对应导航义务未完成，相关整项不勾。T046状态消费代码及组件已交，同run最终显示事实未获证明。014的现实通信阻断只限制代表链及依赖运行事实，未阻设备离线保存。

清理核对、原型位置对应见同目录cleanup-implementation-receipt-20261005.md、prototype-implementation-map-20261005.md；全部新增/修改/删除/SHA与恢复依据见artifacts/recipe-layout-012/delivery-manifest.json及014共同清单。未改013成果，不把历史独立副本统一合入安排继续作为当前责任。最终门禁以当前lightweight-07-validation.json评价；L06已在其源码快照下73/73通过，最终交付文字同步后的当前复核不得预写通过。
