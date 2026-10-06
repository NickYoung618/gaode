# 012增量共同数据消费合同

状态：1.4/正文3已实际集成；下文为该时点设计消费，2026-10-05新增布局/旋转沿共同RC10/1.5、正文4。业务唯一定义在[011共同合同RC09](../../011-plc-interaction-update/contracts/recipe-contract.md#rc09-逐次拍照与配方级夹爪14已集成历史设计)，下文仅012消费说明；唯一类型/校验/序列化仍在Application/Recipes。不是012私有模型。

- CoordinateDefinition新增可空CaptureProfile引用，精确关联已有slot/member+StageId+LocalFace+Camera+PointRef实际拍照项；RecipePurposePoint新增可空CaptureProfile，仅EScan使用。引用既有CaptureProfiles和DetectionCaptureSettings，不重复建立采集参数类型。新保存每个实际拍照点必须存在有效独立引用，不允许缺项转旧全局参数。
- 唯一planner按实际坐标或所属对象EScan点解析引用，步骤原CaptureProfile携入原执行器和CaptureRequest.DetectionSettings；每次相机/光源配置读取此次请求。不能改成第二执行路径。
- 新正文新增可空SortingGripperId；新保存校验严格1/2，缺项拒绝，UI占位未选择。RecipeRunPlan同字段携入冻结，只表达业务语义，当前Sorting/PLC命令不增字段编码或动作。
- 新字段在null时不序列化，保留旧recipe-definition/2与旧计划摘要字节。旧SQLite行recipe-contract/1.3正文2只读可消费；历史夹爪null、局部引用null保持事实，既有历史运行仍原参数。历史1.4编辑迁移到正文3曾要求显式夹爪/逐次参数；本次编辑新写正文4必须明确实际布局/关联、适用两抓手及逐次参数，不能静默迁移或补零。1.4新保存只接受正文3；本次设计只写正文4，SQLite整包JSON无需新增业务表；本次新行合同1.5，历史1.3/1.4原样保留，新冻结3，旧冻结按原版本/摘要读取。
- 曝光为正整数µs，增益有限正数，亮度整数0—100%。继承有效软件合同而非设备物理极限。ROI、光源通道、等待、算法/运动配置及点位依据保留已有有效后台配置，不开放UI输入、不制造生产批准。
- 新建所需后台来源由正式程序解析同SQLite已配置资料，不从测试配置/演示目录取。若目标场景/型号所需有效配置缺失或有歧义，只限制该新建配置，不用假值；既有配方读取编辑继续。选择场景/型号是原型基础操作，不能暴露来源引用给操作者。
- API仍完整RecipeDefinition、ETag/If-Match→ExpectedVersion，唯一校验与IRecipeStore。完整读取包含未展示字段；局部编辑不能裁掉其他字段。对实际配置的输入映射可做结构组织，前端不得检查工艺组合或生成设备计划。

## 设备证据边界

当前CameraCaptureAdapter检测设置明确NotIntegrated；SDK/光源网关尚无参数配置入口或具体正式实现。最小补齐网关参数设置能力，使适配器在Trigger前向相机/光源传递本请求参数，取消/关闭和有限等待沿既有路径。通过声明组件证明调用与实参，原真实Host未集成保护保留，ConfiguredOnly/Unknown不升级Applied。无真实硬件及物理映射证据不称硬件应用已通。夹爪硬件接入全部留后续明确PLC交付。
