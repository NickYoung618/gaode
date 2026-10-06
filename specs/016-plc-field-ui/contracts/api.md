# 本地联调API v1

仅监听127.0.0.1，浏览器同源；POST必须application/json且拒绝外部Origin。

- GET /api/config：schema2配置（无site.json时返回现场模板）。
- POST /api/config：完整配置保存，仅断开时；不自动连接或写入。
- GET /api/state：版本、连接状态、是否启用写/心跳、最近样本、事件、报文、当前动作及会话路径。界面必须标记停止/失败旧样本。
- POST /api/connect：默认只读开始一轮；POST /api/disconnect：停止采样，保留未完成动作；关闭并非急停。
- POST /api/writes {enabled}；POST /api/heartbeat {enabled}：显式开关，按015准入。
- POST /api/write {signal,value}：单点人工写入；结果仅为应答和读回。关联operationId。
- POST /api/move {axis,target}：确认配置的单轴动作，关联actionId，后台观察0→1等配置阶段，不阻塞心跳。
- GET/POST /api/notes：现场自由文本和设备/程序资料JSON。
- POST /api/export：断开后生成ZIP并返回filename；GET /api/download/{filename}下载该导出文件。

拒绝操作返回400和error；实际通信异常同时终止会话、保存失败。导出可包含没有summary的强制中断轮次，不能当通过。
