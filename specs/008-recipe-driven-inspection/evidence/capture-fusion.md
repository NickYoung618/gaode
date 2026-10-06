# 第三批采集融合代码（008 T053部分）

`FileBackedCapture`按CaptureRequest的相机身份从Q01 Test清单读取A/B两张不同PNG，仍经历真实4秒采集延迟；`Q01CameraMediaTests`1/1通过并核对文件摘要。`FaceResultAggregator`只配同对象/面/高度轮次A+B或C+D，拒绝重复/混对；合同测试1/1通过。`IntegratedDetectionPort`在单图媒体/算法事实提交后配对，再以两份媒体和各自InputIdentities发送独立worker双输入融合，等待两份输入释放并保存融合事实；输出对象结论取融合结果，缺配对拒绝完成。

本次重新运行`WorkerTwoInputTests.IndependentWorkerReadsEveryInputAndReleasesEveryLease` 1/2输入两种2/2通过，双输入请求现在逐媒体传A、B身份；真实独立Python进程读取两份受控媒体、各等待10秒并分别释放租约。该组件测试仍不是正式Q01检测链。这些代码目前被产品定位安全门拦住；没有用Q01正式入口触发实际双输入调用，也没有本次版本的融合SQLite读回或复位时序证据。T053未勾。

## 第四批更新

上述“被产品定位安全门拦住”仅指第三批时点。显式Test目标组件运行现已实际按配置传曝光、增益、ROI、光源通道和稳定等待参数给采集请求；FileBackedCapture按A/B或C/D读取受控PNG，真实独立worker逐图处理并在第二输入齐备且复位后做双输入融合。AB单槽2图/1融合、CD非连续两槽4图/2融合的SQLite及媒体读回、worker释放和PLC复位顺序见[第四批记录](fourth-batch-validation.md)。固定图源只验证Test参数传递、光源绑定与等待；真实SDK适配当前不能设置检测曝光/ROI，现显式拒绝带检测参数的请求，避免静默忽略，不证明真机或缺陷精度。T053完整验收仍依正式Q01链。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
