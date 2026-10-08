# 006公共位置与异常选择增量 / frontend-public-tray/1

2026-10-06用户明确授权；新016共同后端合同为唯一业务来源。本文件是006独立前端规格/合同增量，由006 spec/plan/tasks引用，不将页面任务放入仅后端规格。

只读原型ZIP SHA256：3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0；页面a.html/data-view.html/login.html，归档不改。例外仅生产页面顶部右上独立“示教／公共位置”按钮与弹窗，异常弹窗及必要穴位/结束原因。沿当前样式与配置权限，不改012导航/配方弹窗范围。

公共弹窗只含人工上/下料位、3D位的当前合同必要坐标、读取当前位置、保存、取消及真实反馈；F及其他有效配置隐藏保持，配方不出现公共坐标。当前可靠实测不可用显示后端原因，不以目标值或旧缓存示教。

异常弹窗列本次行列、异常类型/原因，只有“人工介入”“继续”。倒计时由服务给定原始deadline显示，本地计时不提交默认选择或推进业务；刷新重取同DecisionId与deadline。已选择/关闭后停止计时；API拒绝或保存失败显示真实错误。

运行/历史显示占用/姿态/参与与物理Pending进度；异常不伪造检测质量结果。明确结束原因及InspectionCompleted；空盘/介入结束显示本盘已结束，不显示全部检测完成。首次介入/空盘没有配方、扫码/检测事实时保持空；人工取盘沿已有合法允许和确认API。

必要验收：实际浏览器顶部入口/两个独立弹窗、行列/原截止及选择、真实配置API保存/重启读取，运行及历史实际状态；精确原型差异按授权补丁登记，不能整页豁免。
异常决策在已查询到run后立即显示，不等待证据和图片下载；刷新复用后端原decisionId/deadlineUtc。页面不新建或延长决策窗口。


## 021受控联调用途增量（2026-10-08）

本次仅定向更新021消费者合同，旧用途/历史证据保持原范围。独立规格与设计见[021规格](../../021-commissioning-console/spec.md)、[计划](../../021-commissioning-console/plan.md)、[任务](../../021-commissioning-console/tasks.md)。新用途为RealDeviceCommissioning，运行purpose为Commissioning。

预配置单身份由后台核权，Operator运行/ProcessEngineer编辑，不用Test令牌或客户端角色授权；identity GET、固定appassets.local来源、头认证和宿主内存凭据按[IH合同](../../021-commissioning-console/contracts/identity-host.md)。按主体/requestId只读启动查询、Final持久提交后同Run普通释放及只读start-admission按[SC合同](../../021-commissioning-console/contracts/start-and-completion.md)；未知不重发，保留原故障恢复。

七格显示已由用户确认C/D/A/B/E/3D/F；本新用途的对应问题关闭，旧Test临时映射证据不改。当前Run已提交图像/结果、未参与不补图和三页既有承载按[RM合同](../../021-commissioning-console/contracts/recipe-media-ui.md)，不改变采集工艺/硬件绑定或原型布局。
