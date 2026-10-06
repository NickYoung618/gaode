# 第三批线性执行入口（008 T052部分）

`RecipeExecutionCoordinator.ValidateDetectionPlan`逐项检查冻结步骤序号、阶段所有者、Position/Capture同对象/面/轮次/相机配对及未支持步骤；Sort/Unload只作为外围移交，未知Detection步骤不得被捕获筛选静默跳过。008的2.0选配方运行显式置`StrictRecipeExecution`；`IntegratedDetectionPort`在相机请求前执行严格检查，并因产品运动/复位尚未接线明确拒绝，不再把008的Capture-only路径报告Completed。公共移交在缺已批准XYZ解析时拒绝构造0,0对象位置。历史007 Test入口按原合同保留，不能当作008验证。

合同测试`RecipeExecutionCoordinatorTests`4/4通过，包括Q01缺点位、错配相机、未知Rotate、Q01冻结期限及严格请求拒绝旧占位位置。该测试仅检验静态门禁；真实定位、Z绑定、到位后采集、复位及完整步骤消费尚缺。Q01正式入口仍Restricted，008 T052未勾。

## 第四批更新

上述“尚缺真实定位/复位”是第三批时点记录。现在严格请求逐Position校验显式目标身份、来源及坐标系；同一`IntegratedDetectionPort`保存运动意图，经MotionCoordinator下发Detection命令2，核对本轮反馈/XYZ并保存事实，单图必要保存后执行1/2及Z复位，复位事实提交才释放动作占用。第二输入复位后融合，再进行下一定位；DecideUnit提交对象结论。AB/CD和失败实际组件证据见[第四批记录](fourth-batch-validation.md)。正式Q01仍Restricted，T052整项未勾。


## 2026-09-27 当前已退出子范围

选定Test代表已完整退出并经同run原操作/场景/适用预算/实际动作/持久与页面读回验证。当前索引见[原条件审计](task-audit-night-20260927.md)、[范围矩阵](../coverage-matrix.md)及[收口报告](completion-review.md)最新节，历史待验证描述不覆盖本节。r18 GROUP-A-E两组8成员14面/42Detection，普通整体Pending/EError及旋转PartOK整包通过；r12普通整体人工与旋转Pending、r8非连续P03与普通人工、r13 Q04/Q05和r16 Q06按各真实构建复用。当前r22 Q18 CDABCDCD通过，当前恢复唯一主包r22 job002通过。未变分支复用不代表旧DLL等同r21，不回填旧字段，生产限制不变。
