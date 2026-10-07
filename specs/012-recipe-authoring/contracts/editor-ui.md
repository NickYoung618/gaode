# 配方弹窗与现有运行界面合同

当前增量：2026-10-05用户确认10×10手动布局，见[原型基线](../prototype-baseline-20261005.md)。旧V3纠偏/三参数/夹爪的数据保护保留；布局与数量交互由新稿替代。共同字段不是UI授权依据。此前G-01消费与历史实施事实保留在历史交接，旧技术编辑映射被本轮替代。

## 同一弹窗与完整正文

保持btnRecipe/recipeModal，不开页面。三步：基础信息→坐标配置→检查保存；基础信息/概览及底部固定10×10勾选；第二步完整同位矩阵含空位，区域/成员/面和直接卡片，检查摘要/填写状态/定位返回；旋转字段仅场景1特殊类型。

| 共同数据 | 表单输入 | 保存正文及完整重读 |
| --- | --- | --- |
| FCode/Model/UnitKind/Capacity及成员/面 | 料盘编号、型号、检测场景；槽数量由矩阵勾选统计，适用成员/面数/AB-CD保留 | 保持原共同字段；真实API映射结构，不从演示默认值造数 |
| CoordinateDefinition/Source/Flip/PurposePoints/Sorting | 按实际格位和区域选表单，填写拍照/扫码/分拣XYZ、翻面取放XY；特殊共用工位取放/两组绝对角，逐槽本件原始OK槽的放料关联 | 翻面Z与姿态由已有后台配置承接；共同归属/PointRef/StageId/来源/单位完整保留，缺真实配置拒绝，不人工拼技术引用 |
| CaptureProfiles/局部CaptureProfile | 每个实际相机拍照或E扫码卡片仅曝光µs、增益、亮度% | 逐槽/成员/面或扫码姿态/PointRef/Camera关联；无全局参数编辑，无AB/CD共用修改 |
| SortingGripperId | 基础信息一次选择夹爪1/2，未选择占位 | 分拣抓手显式1/2，特殊另独立选旋转上料抓手；历史缺项null不补；共同保存/冻结携入，选择通信由014承接，不在UI映射PLC |
| ECode/ExtraPose | 原型扫码项和已确认可选额外E姿态 | E不当检测面；原规则/算法/来源由后台保留，无原型“所有四面强制第5面” |
| 身份/版本/摘要/批准/算法/ROI/光源映射/等待/路线/Pattern/配置键/阶段 | 不展示编辑控件或技术说明，不折叠保留 | 完整正文保留所有有效隐藏字段；唯一服务器身份、校验、序列化及准入保持 |

## 请求与结果

读取必须完整GET，不能用目录摘要重建。检查/保存调用共同后端，字段错误定位业务对象/步骤；前端只做输入映射与填写状态，不复制工艺校验。关闭取消在途，迟到响应不覆盖新选择。If-Match沿共同ExpectedVersion；提交未知不自动重发，保存成立与重读失败区分。新建不默认夹爪或参数，不提升准入；已有run仍允许编辑保存，运行引用不变。
## 真实运行绑定

实施消费状态001：现有8个状态卡绑定运行状态、实际阶段、当前动作、观察覆盖、异常物理槽、绑定状态、分拣状态及整盘状态。值只取已交付投影；动作关联和逐槽presence/pose/participation写入现有诊断区，处置及movement写入现有结果明细，不增加页面/卡片。冻结页头仅取recipeExecution自身Model/身份，历史缺项不查活动目录填补。最终保存仍核真实Final结果及来源，不由某阶段Completed推导。状态001未交的轴/媒体关联保持原限制。

沿现有运行页头、阶段列表、结果/诊断区域消费011查询/通知。通知用于触发真实重读，保存后无需另建推送平台。

| 现有区域 | 共同公开字段及语义 | 显示边界 |
| --- | --- | --- |
| 选用/页头 | recipeSelection的RecipeId、ObservedVersion、ObservedCatalogDigest；recipeExecution的recipeId/recipeVersion/definitionDigest/catalogDigest/planRevision/fCode/model/snapshotRef | 选择意图不等于绑定；运行只显示冻结引用，不拿弹窗或新目录替换 |
| 阶段列表 | recipeExecution.stage、executionPhase.kind/state/stepSequence及适用transition/entity/slot/face/scanPose/observation关联 | 绑定前公共状态提供executionPhase，绑定后recipeExecution提供，同一投影器；绑定前不造recipeExecution。显示实际阶段，不本地推断下料/分拣顺序或完成 |
| 槽位/诊断 | slotStates[].physicalSlotIndex/presence/poseState/participation/observationRef/entityRefs/reasonCodes | Absent、PoseExcluded、Unknown按事实分开；姿态异常不变成NG/Pending，不重新编号 |
| 异常提示 | abnormalPhysicalSlotIndices、observationCoverage及其最后有效observationRef | 未可靠观察时null，不显示空数组冒充正常；Partial保留覆盖限制，Complete不等于质量或最终通过 |
| 结果/处置 | results的quality/completeness/technicalState/save/source及pose/participation；movements的实体/物理槽/目标/操作/提交/physicalState | 普通OK只有实际NoMoveRequired事实才显示不搬；特殊OK需实际原槽回放与safe事实；NG/Pending目标不等于实际到位；技术状态、质量、姿态和物理处置不合并 |
| 操作/错误 | restriction、allowedActions及真实Final/保存事实 | 不从本地按钮、质量或通知顺序推可操作性；取消不宣称物理动作已停止 |

原字段准确定义见station01-execution/1.0 EX04/05及已交付四份混合API。s01/notification/2.0的changedFields触发同一运行查询，不另建运行副本；结果revision/ETag随实际投影变化。历史记录缺新增事实时保留null/Unavailable。axisObservations只在已有确有用途区域消费实际值/可靠性，不将目标当反馈，也不新增轴监控页面。

更多面媒体仍按run/对象/面/相机查询，独立E沿现有媒体/诊断关联scanPoseId，不硬塞固定七格。login/data-view等结构不改，既有结果查询可消费兼容事实，不扩大UI。IC-05已实际接收状态001—007，运行绑定已实施且有33项runtime组件证据；更多面/E差异引用011具名共同动作组件。multi03真实链页面已采证，原历史查询Incomplete保留；状态007修后同run只读补证20项满足，011已实际接收T022005/006，独立9项与实际GET13项也满足。当前孤立换面分支删除未改变阶段/冻结/结果/分拣/终态映射，精确差异及当前组件/原型门禁确认原实页证据复用范围；不宣称旧截图采了新字节。

## 原型保护的定向承接

基线仍为客户ZIP及a.html、data-view.html、login.html；归档摘要沿006记录
3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0。
实施已运行原型正负例，结果按各源码批记录于plan-handoff；来源归档及历史报告保持只读，不把旧摘要当新代码验证结果。

实施时在frontend/scripts下维护012授权差异清单：每项含需求ID、文件、归档原文/摘要、精确替换内容/摘要及实际作用区域。对只读归档应用已审查的006既有绑定和012新增差异，生成内存中的预期实现，再全文件比较实际实现。差异不重叠，原文不能匹配则失败，未列差异失败；不得忽略整个modal/script/style/a.html。

012可变区域为弹窗DOM、其专用样式/事件、必要真实运行数据绑定；无关导航和页面布局仍受保护。独立runtime.js/构建注入资源按实际交付脚本及边界断言核对，不能把任意注入都视为已授权。归档哈希、三页清单、离线资源及无前端直控等门禁保持。

现有verify-prototype.ps1将实现页与归档逐字相同作为要求，须改为上述精确比较；不能直接关闭检查。prototype-console.test.ts的“unchanged”标题与浅标志检查、prototype-all-pages.test.ts的仅存在检查不再冒称完整一致性。定向替代无效断言，保留有效检查和失败证据；归档/实现摘要分别记录，不输出错误identical=true。

## 实施清理定位

2026-10-04消费者核查增量：011明确当前配置翻面服务已无ManualFlipInteraction生产调用。T023删除runtime的canConfirmFlip、人工换面提示及manual-flip-confirmations分支，原按钮/DOM和有效取盘/恢复许可保持；历史WaitingManualFlip不授权或提交。共同业务字段/序列化不变；只补当前runtime组件/构建及精确原型正负例，不重跑同义联合链。

runtime.js:223—235旧阶段推断须替代；:96—115旧expectedRecipeRef/专用S1/P01限制按真实消费者清理；:484—498旧选用交互按编辑/使用职责拆分。sameRecipeRef原用于冻结页头比较；状态001接入后页头直接消费冻结Model/身份，且启动只传观察引用，不再与活动目录比较。核实已无调用后删除该函数；冻结页头/版本保护由实际投影及组件回归承接。a.html旧演示PLC地址、固定翻面角度、MES/正则等无用途表单及flipToggle/flipParams监听在替代后删除。

clearPrototypeDemo仍保护其他演示区域；localStorage只存不可信run引用并重新GET，两者核对用途后保留有效部分，不因名字或旧实现身份直接删除。正式配方不进入浏览器持久存储。


## 2026-10-05稳定布局消费

区内左→右/上→下各自从1编号，实际格位+区域绑定点位/成员/每次拍照，显示号不作唯一身份；取消/改区只清该格，重编号不迁移其他参数。OK号是检测顺序，缺料/异常跳过保号；特殊逐件分拣，普通整盘统一分拣。填写完整不是保存/质量/到位。成组成员与半成品部位的具体导航见navigation-preview的DUI-02/03最小方案，2026-10-06已获用户确认，只按原预览实施，不准新增技术选项。

原始料盘、OK区域、实际物理格位及实体身份在取料前明确并冻结，后续旋转/采集/判定/回放沿用同一关联；区域号只用于展示/检测顺序，不能代替原始槽身份。特殊OK原槽回放使用分拣抓手，不切成上料抓手或NoMoveRequired；实际取料及必要保存、转运、放料和安全位确认后才推进下一件，失败不记录完成。

操作者不得为OK选择其他目标槽，不增加“是否回原槽/OK处理方式”开关；原型任意OK目标配置含义退出。若已有明确必要的原槽放料参数，归该原槽取放配置；同槽不推导全部取放坐标、高度/抓手补偿相同，不自动复制全部取料值、不编造新参数。

历史读取保持原记录事实，不把旧任意OK目标/旧完成标记重解释成已按本次原槽规则执行。当前显示与执行必须区分普通无需搬运事实和特殊实际原槽回放事实；缺实际保存/动作证据时不补造完成。准确共同字段/版本/历史策略见RC10；当前012完整映射见layout-design与API；DUI02/03见已确认navigation-preview及navigation-approval-20261006，设计不代表产品验证。


## 本次Phase 1配套结果

共同字段→表单→正文→完整重读准确消费见[layout-design LD02](../layout-design-20261005.md#ld02-共同字段输入保存完整重读)。原型对应及DUI02/03预览见[navigation-preview](../navigation-preview-20261005.md)，两项于2026-10-06明确批准，只按原预览修改对应导航；软件满足需当前实页/保存证据。


## 本次新建与采集组输入映射

基础信息已有场景/类型选择→API-L00的ScenarioId/UnitKind/InspectionKind，Route由程序按共同关系确定，不操作者编辑；型号相同的普通/特殊来源精确隔离。SourceRecipeId是程序上下文，切换清不相容来源，无技术selector。第一组/第二组卡片将各自StageId随布局映射传递，不新增阶段技术操作；未填candidate保JSON null，严格保存不放宽。来源准备属真实后台主流程义务，DUI02/03导航仍待用户确认。
