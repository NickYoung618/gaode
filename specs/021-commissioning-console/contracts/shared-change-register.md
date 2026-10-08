# 021共享变更登记与前置顺序

## 2026-10-09 SC-021-PLC-R6

用户明确确认移动/同坐标/复位三类反馈核验。当前合同优先于历史全轴清零与到位1禁止沿用的条款；保留MB6052=0及其他已确认门槛。先更新021 spec/contracts/plan/tasks，再改共享适配器。无新增PLC地址、前端控件或业务API；原始来源、其他功能历史规格和旧任务勾选不改。


## 2026-10-09 SC-021-PLC-R5

用户已明确确认PC发复位、PLC动作完成后置MB6015=1、PC读1后清MB2009。先更新021 spec、start-and-completion、plan与tasks，再修改现场PLC适配器和脚本文案。不新增地址或接口字段；现场恢复消费者仍需真实安全/位置/其他反馈及持久收尾通过。

该确认替代本登记及020/021历史约定中对本次Ready 0→1的观察要求。历史来源文档和其他功能规格不改写；当前生效条款见SC-021-PLC-R5。旧MB2009=1处置、未知失败不重发、旧Run未核验不释放维持原约束。

2026-10-08；已按T002/T003在006的host/api/prototype-mapping/public-tray-flow-016以及012的editor-ui/recipe-authoring-api/shared-integration追加021合同引用；原有历史内容保留。后台身份有部分实现和测试，其他共享实现及最终验证尚未完成。继续遵守共享文档先于代码，不初始化/升级或补造缺失旧功能的spec/plan/tasks。

| 需求及本合同 | 拟改共享接口/代码 | 必须先同步的有效资产 | 实施边界与消费者 |
| --- | --- | --- | --- |
| FR-001–004；IH | Host身份配置、认证分支、identity GET | 006 contracts/host.md、api.md：新用途/身份；021spec/plan/tasks/IH | Station01Authorization/新增CommissioningAuthenticationHandler和IdentityEndpoints、Program；Test旧规则不扩大，联调不叫test主体 |
| FR-001/009/014；IH | HostConfiguration、HostRuntime、DesktopRuntimeLog；运行资源配置 | 006 host/prototype-mapping与021 IH/tasks | frontend/runtime.js、实际pages/login、runtime-config.ts及构建消费者；固定来源/一份内存凭据/日志遮蔽 |
| FR-011；SC | 主体+requestId只读查询、原命令关联 | 006 api及021 SC/data-model/tasks | CommandRegistry、ITraceQuery/TraceQuery、QueryEndpoints；扫描端口实现/测试替身，不造默认成功实现 |
| SC-003/FR-008/011/013；SC | 最终保存后普通owner释放、start-admission GET、前端下一轮状态 | 006 api/public-tray-flow-016及021 SC/plan/tasks | WholeTrayWorkflowOrchestrator或现共同完成服务、RunEndpoints、CommandRegistry与QueryEndpoints；核同Run Final事实，保持故障链，读接口无副作用 |
| FR-005–008；RM | 012表单及完整正文绑定/字段错误 | 012 editor-ui/recipe-authoring-api/shared-integration仅新增021消费引用及实际必要差异 | RecipeEndpoints既有正确部分不强改；API模型/共同Validator/Store不复制；显示编号不当现场REAL值 |
| FR-012/015；RM | 七格真实目录与数据页已保存投影 | 006 prototype-mapping/api：登记用户七格答复，关闭本显示映射未决；021 RM/tasks | runtime.js及pages/data-view；已确认映射不修改设备角色/工艺；不覆盖历史未关联通过范围 |
| FR-014–016；IH/RM | 精确原型差异及测试/持久诊断消费者 | 021验证计划与后续tasks | build/verify-prototype/差异清单、Node入口、Windows探针；不修改020历史TRX、requirements或T055/T056勾选 |

现有020合同继续作为设备、配方、采集和安全语义的依据；本功能不修改其协议或现场许可。发现实际共享语义必须改时，先在对应本规格/合同/plan/tasks中列清变更再实施，不能靠“前端修复”绕过业务门禁。

2026-10-08按用户授权定向修正C1/I1：实现前置与桌面验收分开，V06采用专属Commissioning离线采集/提交/正式查询媒体路线，V05仍旧Test。修正涉及021 plan/tasks/quickstart、RM及SC合同；只明确软件依赖和证据范围，不改变业务/PLC/页面结构，不改006/012/020资产。逐项记录见[修正记录](../remediation-20261008.md)。

完成以上同步不等于共享接口已实现或验证通过。后续实施收口应逐项登记实际文件、已执行测试、未验与必要差异；旧证据保持原环境/版本。


## 2026-10-08 用户增量登记：FR-017/018

新增[CC-021](configurable-commissioning.md)，对应T033–T037；已同步021spec/plan/data-model/quickstart/tasks及012合同消费边界。必要共享改动：RecipeDefinition及Serializer/Validator/Identity/Store正文消费、冻结模型/Planner、CaptureRequest和设置摘要、CameraCaptureAdapter/CaptureEvidenceGate/来源、CommissioningConfiguration/AlgorithmInputs/Algorithm及Host装配、012编辑器及精确原型差异。T033先定版确切字段/版本及扫描消费者，随后代码实施。

不修改020历史合同、任务及证据；旧单品翻面验证是代表范围，不能当当前新增要求通过。当前新需求只有规格/契约/计划/任务登记，尚未实现可选跳过光源或多配方输入装配，也未执行V07/V08。真实光源控制器接口未知只阻断真实接入，不阻断虚拟模式和拒绝路径。

## T033共享字段定版及交付授权（2026-10-08）

采用版本化可选扩展lightExecution={schemaVersion:"light-execution/1",mode:"Simulated"|"Real"}，置于共同RecipeDefinition、RecipeRunPlan、PublicConfiguration、CaptureRequest及CorrelatedCaptureFact；写入设置摘要，JSON null字段不输出。未含此扩展的历史正文仍按原schema及原灯控制/证据语义读取，不静默迁移。新建配方显式Simulated；编辑复选框保存显式选择，未编辑历史记录保持原字段。光源相关DetectionCaptureSettings的lightChannel/brightnessPercent/settleMs允许null，仅显式Simulated免校验；曝光/增益/ROI不变。公共CaptureParameters.lightLevel同理。此为现正文中的light-execution/1扩展，不强制重写历史recipe-definition/5或SQLite表。

CommissioningConfiguration保留已有主输入记录，增加recipeInputs[]，每项含id/version/source、expectedRecipe、slots、fLocation、mappingSourceReference、rawCodes、results及可选entityCodes；共享能力/公共配置/预算仍由外层版本约束。启动FreezeRun增加可选ExpectedRecipeRef选择意图参数，从主记录及集合唯一匹配RecipeId/Version及scenario，不填默认值；无选择仅允许单一记录的旧声明调用。每Run保存选择后的独立输入并在F绑定后再核DefinitionDigest/Model/FCode，不使用全局当前配方。EDecode采用EntityCode/decoded-code/1、按ReadECode实际步骤作用域的显式entityCodes记录，缺项局部阻断，不自动生成码。

用户已明确解除本轮打包和本机部署限制；原文“不打包/不部署”为历史授权边界。本轮可构建并制作包，检查本机安装/启动条件，保留已有安装及配置；无现场安全输入不得启动依赖真实运动。此授权不含Git提交/推送，不覆盖现场安全值确认与020 T055/T056证据门。

## 用户确认增量：配方中的虚拟F定位（2026-10-08）

用户确认示教坐标人工填配方，本次单品翻面样件的虚拟质量结果为OK；虚拟算法必须实际消费本Run媒体后记录调用成功，不绕过采集/保存或设备动作。用户将提供F读码XY，并要求加到虚拟算法联调用的配方设置。新增FR-019/SC-009：共同配方可选commissioningFPosition={schemaVersion:"commissioning-f-position/1",x,y}，页面在既有配方弹窗基础信息增加“虚拟算法 F读码X (mm)”及Y，由人员手填，不补默认坐标。后端校验有限值，启动再按公共运动配置核单位/坐标系/行程；所选配方版本与目录一致后从实际保存正文读取，构成带配方版本来源的FLocation，冻结给本Run虚拟3D的首次定位结果，实际F读码后仍再次核绑定。真实算法不消费该字段。旧记录保留原声明的受控输入策略；新建联调配方必须显式填写F位置才能通过相关运动准入，不能偷偷沿用旧示例F位置。

准确字段及端口：RecipeDefinition.CommissioningFPosition随正文/摘要保存；ICommissioningRunInputs.FreezeRun(..., selection=null, fLocation=null)增加可选冻结定位输入。StartPublicPreparation从本次expectedRecipeRef匹配的真实目录取得该位置，校验引用后转换FLocation，不从前端POST直接接收运动值。运行开始后修改配方不改变已冻结公共/虚拟输入。已有公共F固定位置及真实3D定位职责不改；新增位置仅适用本联调虚拟算法。FR-009/原“不在页面设置算法坐标”对此用户明确授权的F XY作唯一例外，其余安全值仍不允许默认补齐。


## 2026-10-08 部署维护入口增量

维护工具增加--inspect-recipes和--inspect-commissioning只读检查：前者通过正式SQLite配方目录、准入与规划器重读，后者通过正式虚拟输入加载器核摘要/作用域；不启动设备。安装在全新目录先校验manifest的文件SHA256，再准备数据库、正式保存配方、重读、生成绑定该安装版本的输入；默认启动检查只读取本地配置。安装/校验脚本不增加产品API或前端入口，运行配置缺失保持ConfigurationRequired。契约细节及占位边界见configurable-commissioning.md与deployment-readme.md。


T044持久事件生产键增加Run/Tray命名空间，见SC-021增量；未变更端口DTO、SQLite结构、唯一约束或现回放/冲突语义。新包只安装独立运行库；原记录/在途动作不迁移自动重发。T043仅修离线SimulatedCapture的F防重拍范围，真实相机端口不改。

### 2026-10-08 PLC“就绪”含义确认增量

用户针对MB6015就绪=1答“是”，确认它保证复位完成、各轴停稳、允许PC开始流程。记录见evidence/deployment-final-2-20261008/plc-ready-user-r1.json。本答复关闭PLC-Q4中这一项含义澄清，未关闭2007/2009请求置位/清零时序及断线/2008软停行为。不得继续把MB6015这三项含义列为未确认；也不得把此次确认扩大为全部现场恢复语义或真机验收通过。准入仍须本连接新鲜可靠反馈、既有安全检查、相关旧反馈清零和有效配方/配置；未知动作不自动重发。

源码核查：backend/src/Gaode.Infrastructure/Devices/Plc/LatestProtocolPlcDevice.Semantics.cs中现场布局仍显式输出SafetyUnconfirmed/ManualAreaUnconfirmed。此次只记录确认，不改产品代码或以配置绕过门禁；后续实际映射须先同步对应规格、契约、计划和任务并定向验证。原冻结包不覆盖，020 T055/T056保持现场阻断，requirements只读。

### 2026-10-08 复位及启动边沿答复增量

用户确认：PLC只读PC系统复位请求MB2009的上升沿；收到请求后将MB6015从1置0，复位完成后由0置1，PC观察本次0→1后清MB2009。等待期间须确实观察到本次0，再观察本次1，不能只等任意新读1；超时、断线或反馈过期不得视为完成或自动重发。MB2007启动也只需上升沿，不要求持续保持；清零时点用户要求先解释后再决定，尚未确认。用户说“设备就停住就行”“安全位置位于0”；停住是否同时适用软停/断线及其与原表自动回位的裁决待明确；安全位置按协议所述X/Y/检测Z零位理解，不推导自动回零命令或编造容差。详细答复及范围见evidence/deployment-final-2-20261008/plc-sequence-user-r1.json。

源码核查发现LatestProtocolPlcDevice.ResetAsync当前复位请求置1后立即清0，未等待Ready本次0→1；AdvanceStart只写PcSystemReady并等待PlcReady，未派发MB2007。此为待实现缺口，不能因旧启动方法存在称新握手已完成。本轮仅记录答复/缺口，未改产品代码或重制包；实际共享变更实施前须完善对应契约/任务。原包、历史记录及requirements保持，020 T055/T056仍现场阻断。

## 2026-10-08 已确认启动/复位/停住规则（SC-021-PLC-R2）

用户确认采用所建议MB2007清零时点，并确认MB2008软停及心跳断线均停在当前位置、不自动回位；此规则取代旧说明的软停自动回位。MB2009复位按PC低态确认→置1→观察本连接本次MB6015先0再1→检查新鲜XYZ零位→清MB2009；不得立即脉冲清零、只读旧1或失败重发。MB2007仅在现场布局启动时确认原值0后置1，第一组实际下发运动完成、坐标验证及双方清零后清0；纯同坐标沿用不触发清零。未知/失败不得发下一轮启动；不在失败清理时伪造首次动作完成。现场报警及独立安全点均沿既有只读检查，未知报警非零阻断；安全门/光栅恢复不得自动重发未知动作。

新增通信配置plc-site-operations/1，带非空确认来源，经plc-mechanics/1的siteOperations读取，只适用于现场已确认布局。无此配置继续Unconfirmed，不凭单一布尔标志放行；确认配置采用本次用户约定的Ready语义、XYZ零位、停住规则及首次动作后清启动。零位使用已配置PositionTolerance核对，不填任意容差，不发自动回零运动。完整恢复持料/夹爪核定保持人工门，ReadInitialState不能因上述规则自动声明全部恢复成功。

新增SignalId.PcStartCmd仅绑定现场PC.xls MB2007（BoolByte，PC写/清），不添加旧Test地址、不改变前端API。受影响消费者：协议定义/现场映射、PLC机械配置、Sample安全解释、ResetAsync、AdvanceStart、轴完成清零；旧Test流程和清零/同坐标/未知不重发不变。补丁按final-2清单验证基包，生成独立版本目录并带SHA256、源码增量及验证记录；不覆盖旧包/安装，不连接硬件、不提交推送，T055/T056不关闭。

T047定向回归发现ObserveAxisClosures对所有现场布局无条件清除已闭环资格，导致相同坐标重新派发。SC-021-PLC-R2同步：已有siteOperations确认时，按自动/就绪、报警、独立安全点和光栅检查决定保留资格，不再仅因现场布局即清除；无确认配置仍清除并阻断。仍仅使用本连接真实完成及清零记录，启动前零位不授予同坐标资格。失败证据plc-sequence-r2/r3保留；回归修正后再记录。


## 2026-10-08 现场软停恢复顺序修正（SC-021-PLC-R3）

依据用户现场确认：软停使PLC进入类似急停状态；必须先由PC就绪并取消软停，PLC才能执行复位。显式复位先确认MB2009原值为0，再写MB2006=1、MB2008=0，并用新读取值确认两项均成立，之后才产生MB2009上升沿。写成功或固定延时不能替代读回。内部业务仍处于Resetting，不能因此下发启动/轴运动或续接旧动作。本次MB6015先0再1、安全和XYZ零位核验及最终清请求要求继续保留。旧MB2009仍为1时拒绝再次产生复位，不擅自清除后重发。

PLC复位成功不等于旧Run恢复成功。当前Commissioning尚无覆盖重启恢复Run的完整恢复服务；ReadInitialState中的恢复协议/人工区核验不得改成假通过。旧任务、持久记录及Held保持。页面在既有故障通知区域明确显示“旧任务待恢复核验，PLC复位不等于放行，重启或重复启动无效”，并区分可用的既有恢复操作与尚缺恢复入口。恢复服务/权限/持久核验/新轮关联另列T056，不沿用旧Test内存恢复对象冒充重启恢复。需要明确复位后夹爪是否持料、零件归位及翻转状态，未确定前仅阻断相应恢复放行，不阻断本次复位修正。


## 2026-10-08 全部复位后的旧任务结束与新轮（SC-021-PLC-R4）

用户确认PLC系统复位完成后会全部恢复：零件放回、夹爪松开、翻转机构恢复初始状态。siteOperations新增可选restoresWorkpieceAndMechanisms=true及确认来源，默认false；只有本次已观察Ready 0→1、当前安全和XYZ零位满足、PC请求及相关PLC旧反馈清零时才能形成恢复核验。无此确认不把Ready单点扩大成恢复通过；旧Test语义不改变。

Commissioning正式POST /reset在原Run.Start权限下串行执行维护：阻断新启动，要求旧任务处于Blocked/RecoveryRequired/Restricted/Cancelled且执行退出、相机/算法/媒体资源释放；先持久保存复位意图，再完成PLC复位和新鲜初始状态核验，最后使用原Writer条件版本将旧Run保存为Cancelled并记录CommissioningRecoveryClosed（resetId、用户、真实观察、来源、旧Run身份）。这不生成检测成功或Final完成事实。必要保存未确认时保持占用；已提交后释放对应Motion/Command占用，返回recoveryClosed=true、manualStartRequired=true。无旧任务也支持复位，不人为要求先软停。旧MB2009=1仍拒绝重发未知复位，不把旧Ready=1认作本次复位完成。

GET Run新增commissioningRecovery投影，仅依据已提交取消终态和恢复审计；页面沿既有故障/人工操作区域提供“复位并结束旧任务”，调用同一正式/reset入口。成功后明确旧任务已结束，启动控件只在GET确认恢复证明及Available时可创建新的requestId/Run；不自动启动、不续接旧步骤，不复用旧冻结配置。StartPublicRequest可选commissioningRestartFrom={runId,recoveryWriteId}，后端验证所引用取消及恢复提交事实，并保存在新Run原始启动上下文中；普通启动和旧Test故障restartFrom合同不变。Host重启读取Cancelled持久终态，不再次恢复或自动重发旧Run；未完成恢复仍Held。

用户要求更新现有final-4目录及同名ZIP，不创建新部署包编号。更新前备份原manifest/ZIP摘要及改动文件，保留历史验证与回退；重新发布受影响Host/Prep/Worker等消费程序集，重新冻结源码和清单并校验同名ZIP，不触发真实复位/运动或替换运行中安装。
