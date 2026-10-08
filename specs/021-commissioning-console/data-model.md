# 021数据与状态模型

2026-10-08；设计合同，无新增业务数据库表/迁移。原RecipeDefinition、StartPublicRequest、Run/Command/媒体实体沿用既有模型。

## 身份及宿主资料

**CommissioningIdentityRecord（后台维护配置）**：profileId、subjectId、displayName、role、purpose、credential。role仅Operator或ProcessEngineer，purpose必须RealDeviceCommissioning；各subject/profile及非空credential唯一，非法或重复配置拒绝联调认证装配。credential来自后台本地维护配置/环境来源，不生成仓库默认值。permission由既有角色权限表生成，客户端不可提交权限。

**DesktopCommissioningProfile（非秘密启动配置）**：schemaVersion、profileId、expectedSubjectId、expectedRole、mode、credentialEnvironmentVariable、preparedTemplatePath、logRoot。mode为RealDeviceCommissioning；环境变量指向本进程一份凭据，不能指向Test默认值。expected字段只做一致性核验，不是授权来源。

**ConfirmedIdentity（只读响应）**：schemaVersion、profileId、subjectId、displayName、role、permissions[]、mode、purpose、authenticationSource。purpose为Commissioning运行用途，mode为RealDeviceCommissioning；两者分别对应现合同，不混写。响应不含credential，Cache-Control=no-store。

身份状态：Unconfirmed → Checking → Confirmed或Rejected/Unavailable。导航/重开重新核验；失败清内存权限、旧响应缓存及媒体URL。没有持久“已认证”标志，不以页面用户/角色恢复权限。

## 启动模板与意图

**CommissioningStartTemplate**：schemaVersion=commissioning-console-template/1、id、version、sourceReference、mode、contextTemplate、publicConfigRef、budgetRef、simulationRef。contextTemplate含stationId、lineId、scenarioId、occupiedSlots及context schemaVersion；purpose固定Commissioning，所有引用须显式存在。已有simulationRef仅匹配Host固定联调引用，不代表加载Test SimulationProfile。

模板不带设备地址、原始协议值、安全许可、算法运动坐标或凭据。外部安全配置由正式Host负责。本次显式新启动从模板产生requestId及逻辑trayId（均新GUID），加当时真实选择配方expectedRecipeRef，生成现有StartPublicRequest。逻辑trayId不替代实际F料盘码；模板不声明物理已装料。请求正文发送后不可变，后台仍核输入和机械条件。

**PendingOperationReference（不可信本地关联）**：schemaVersion、operationKind、apiOrigin/mode/stationScope、subjectId、requestId、trayId、commandId?、runId?、requestDigest、state。operationKind为Start或ManualRemoval。只保存必要标识和摘要，不存完整context、凭据、权限、图像或现场参数。按站点/模式隔离；同站身份交接保留原主体标识，不把其请求转成新主体请求。

启动状态：Idle → Submitting → Accepted / RejectedNoRun / AcceptanceUnknown。Accepted经GET重读到运行；AcceptanceUnknown只查原请求。404维持未知；不能生成新ID规避未知。只有后台确认原请求明确未创建运行，或正常最终保存且nextRunAdmission为Available，下一次显式点击才可形成新意图。恢复/故障仍走旧合同，不纳入普通下一轮。

## 普通下一轮准入

**NextRunAdmission**：state=Available/Held/Unknown、ownerRunId?、completedRunId?、reasonCodes[]。由后台CommandRegistry与同Run最终持久事实产生。Available表示软件资源未被当前轮占用，不表示PLC安全/已装料/相机就绪。

正常释放证明使用同Run/Tray的FinalUnloadCompleted、人工确认、FinalSourceMatrix及Run终态已提交事实；不采用页面传入true或单独Completed字样。释放操作在现registry锁内比较owner身份：本Run占用才清，已清则幂等，别的Run占用保持；不清faultRestartOwner。读接口不执行释放或任何设备动作。

## 配方、媒体与终态

选择引用是RecipeId/Version/CatalogDigest意图；执行引用取冻结recipeExecution/PlanRevision及definitionDigest。作者会话保存不改变操作员在途运行，角色切换不改变冻结事实。

媒体主键沿用RunId/MediaId/CaptureId；关联role、businessCamera、object/member/slot、stage、localFace、step/round及committedRevision/readiness/source。业务相机到七格仅按用户确认映射，不能按显示序号生成设备地址。已提交但尚不可读显示读取受限，不放演示图。

取盘状态沿既有后台：未许可 → AwaitingManualRemoval → 显式提交 → 确认未知/失败或最终已保存。人工确认重复查询保持原requestId，HTTP受理不等于Final保存。SQLite最终重读是页面终态依据，不存新的客户端业务终态。


## 待实施增量：配方光源模式及多配方虚拟输入

FR-017/018以[CC合同](contracts/configurable-commissioning.md)为准。共同配方增加显式光源模式Simulated/Real，保存重读、目录及冻结/CaptureRequest带同一模式和来源；公共配置独立带公共采集模式，不能从未绑定配方推导。Simulated不要求外部光源亮度/通道/等待，但曝光/增益/适用ROI仍校验；Real保留完整要求及真实装配检查。历史正文按原版本读取，准确新版本号及历史映射经T033定向核定，不默认改变旧记录。此处变更共同正文/序列化，不新增配方业务表或独立目录。

虚拟输入集合包含各配方作用范围、id/version/source及既有受控结果；每Run匹配启动意图与F绑定后的正式配方版本/摘要并冻结一组。启动前公共输入与绑定后产品输入分阶段核对，缺项拒绝依赖动作。不得从新增配方自动生成现场安全值。

## T033共享字段定版及交付授权（2026-10-08）

采用版本化可选扩展lightExecution={schemaVersion:"light-execution/1",mode:"Simulated"|"Real"}，置于共同RecipeDefinition、RecipeRunPlan、PublicConfiguration、CaptureRequest及CorrelatedCaptureFact；写入设置摘要，JSON null字段不输出。未含此扩展的历史正文仍按原schema及原灯控制/证据语义读取，不静默迁移。新建配方显式Simulated；编辑复选框保存显式选择，未编辑历史记录保持原字段。光源相关DetectionCaptureSettings的lightChannel/brightnessPercent/settleMs允许null，仅显式Simulated免校验；曝光/增益/ROI不变。公共CaptureParameters.lightLevel同理。此为现正文中的light-execution/1扩展，不强制重写历史recipe-definition/5或SQLite表。

CommissioningConfiguration保留已有主输入记录，增加recipeInputs[]，每项含id/version/source、expectedRecipe、slots、fLocation、mappingSourceReference、rawCodes、results及可选entityCodes；共享能力/公共配置/预算仍由外层版本约束。启动FreezeRun增加可选ExpectedRecipeRef选择意图参数，从主记录及集合唯一匹配RecipeId/Version及scenario，不填默认值；无选择仅允许单一记录的旧声明调用。每Run保存选择后的独立输入并在F绑定后再核DefinitionDigest/Model/FCode，不使用全局当前配方。EDecode采用EntityCode/decoded-code/1、按ReadECode实际步骤作用域的显式entityCodes记录，缺项局部阻断，不自动生成码。

用户已明确解除本轮打包和本机部署限制；原文“不打包/不部署”为历史授权边界。本轮可构建并制作包，检查本机安装/启动条件，保留已有安装及配置；无现场安全输入不得启动依赖真实运动。此授权不含Git提交/推送，不覆盖现场安全值确认与020 T055/T056证据门。

## 用户确认增量：配方中的虚拟F定位（2026-10-08）

用户确认示教坐标人工填配方，本次单品翻面样件的虚拟质量结果为OK；虚拟算法必须实际消费本Run媒体后记录调用成功，不绕过采集/保存或设备动作。用户将提供F读码XY，并要求加到虚拟算法联调用的配方设置。新增FR-019/SC-009：共同配方可选commissioningFPosition={schemaVersion:"commissioning-f-position/1",x,y}，页面在既有配方弹窗基础信息增加“虚拟算法 F读码X (mm)”及Y，由人员手填，不补默认坐标。后端校验有限值，启动再按公共运动配置核单位/坐标系/行程；所选配方版本与目录一致后从实际保存正文读取，构成带配方版本来源的FLocation，冻结给本Run虚拟3D的首次定位结果，实际F读码后仍再次核绑定。真实算法不消费该字段。旧记录保留原声明的受控输入策略；新建联调配方必须显式填写F位置才能通过相关运动准入，不能偷偷沿用旧示例F位置。

准确字段及端口：RecipeDefinition.CommissioningFPosition随正文/摘要保存；ICommissioningRunInputs.FreezeRun(..., selection=null, fLocation=null)增加可选冻结定位输入。StartPublicPreparation从本次expectedRecipeRef匹配的真实目录取得该位置，校验引用后转换FLocation，不从前端POST直接接收运动值。运行开始后修改配方不改变已冻结公共/虚拟输入。已有公共F固定位置及真实3D定位职责不改；新增位置仅适用本联调虚拟算法。FR-009/原“不在页面设置算法坐标”对此用户明确授权的F XY作唯一例外，其余安全值仍不允许默认补齐。
