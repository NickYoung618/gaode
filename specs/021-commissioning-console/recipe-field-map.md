# 021配方字段归属与执行路径核对

2026-10-08；核对实际构建源frontend/src/recipe-authoring.js、RecipeEndpoints.Authoring、共同RecipeDefinition及020有效合同。仅软件字段归属，不确认现场数值或机械含义。

| 020项 | 现有输入/后台归属 | 保存、冻结和实际消费者 | 限制及证据 |
| --- | --- | --- | --- |
| P01/P02 产品、槽、成员、面/相机 | 基础信息、手动10×10、当前槽/成员/面卡片；后台editor-draft/layout建立正式实体 | 完整GET/validate/POST或原ETag PUT→SqliteRecipeStore→F绑定→RecipeRunPlanner | 不用显示号作物理槽；V02需实际页面/正文重读 |
| P03 检测XY/Z | 逐点卡片X/Y/检测Z；stage/face/camera及点来源保留 | executionPositions.coordinates→冻结plan→RecipeDetectionExecutor Move/Capture | 不凭计划列表证明执行；A/B实际请求另记 |
| P04 翻面取放XY、Z/姿态 | 既有取放卡片XY；隐藏固定Z、profile/version/pose为后台源 | handling.purposePoints/flip.stages→冻结→Flip/PutBack适配 | 显示PlcRecipeId不是机械ModelNumber；后台机械映射不在页面制造 |
| P05 分拣 | OK原始槽、NG/Pending区域及抓取/放置点；显式抓手，成员抓手保留既有实现 | sortingTargets及handling→冻结→实际取/放/回安全位 | 本代表OK路线不删除NG/Pending正文要求 |
| P06 特殊工位、角度、抓手 | 既有工位取放、角度及抓手输入；公共/机械来源仍后台归属 | rotationWorkstation及阶段配置→正式执行器/设备端口 | 特殊路线只复用原证据范围，不借本单品代表宣称全覆盖 |
| P07 每张曝光、增益、亮度 | 每张相机卡片独立profile；ROI、光源通道、settleMs、算法引用保留后台来源 | captureProfiles→坐标独立captureProfile→冻结→CaptureRequest.DetectionSettings→CameraCaptureAdapter | editCapture局部克隆；缺基础profile明确修改未应用，禁止假成功；虚拟灯只消费参数，不声称物理应用 |
| P08 E及F | E扫码坐标/适用额外姿态沿现输入；F XY由受控3D结果，公共F采集配置由后台提供 | eCode/extraPose及公共配置→正式采集/算法/绑定 | 不新增E采集或固定F坐标；用户安全算法值未交付阻断相关真机动作 |
| P09 公共位置/示教 | 现独立公共位置功能及公共配置，产品配方卡片不改其职责 | public配置冻结→公共运动/采集 | 公共Z历史限制保留，不删除/扩展示教 |
| P10 预算、能力、用途、来源 | 后台配置/正式Host；页面仅完整保留正文和公开状态 | 共同Validator/Admission/ConfigurationFreezer/RecipeAdmission.Freeze | 页面不批准安全、不填机械/PLC地址、不新增技术编辑器 |

全体配方采用同一模型、业务校验、SQLite保存与目录；新用途校验保存后可选，旧Test/Production准入保持。完整隐藏字段与原ETag由作者会话维护，CommitUnknown/冲突不自动重存。在途快照与后续版本的实际参数消费须由V02证明，字段归属核对本身不代替验收。


## 用户新决定对P07的修订（待实施）

P07旧行描述的是目前代码，虚拟灯仍消费参数；不能当新需求已满足。FR-018/CC-02要求配方级虚拟光源勾选后跳过亮度/通道/等待及全部灯控制，曝光/增益不跳过。共同校验、读存、冻结、请求、采集门禁及来源须一并修改，任务T033–T037；前置公共3D/F独立配置。


2026-10-08当前证据更新：P03/P07编辑隔离已在真实桌面页面保存后，由正式执行器核实际Move/Capture消费（X10→11，单张曝光10→110，其他三张保持10），见validation.md的V02子项；不把字段表或旧计划当实证。虚拟光源跳过已按CC实现并有模式组件/桌面保存证据，前面“待实施”段为历史状态。虚拟F XY已依用户定向授权加入配方，保存版本冻结到Run；成功来源F65/50已有，不再泛称未提供。真实算法F定位及公共位置职责保持原合同。
