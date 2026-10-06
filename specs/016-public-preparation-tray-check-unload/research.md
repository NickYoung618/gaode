# 016定向研究

2026-10-06，speckit-plan研究代理只读核对当前副本，无构建或编辑。

| 决策 | 理由 | 排除方案 |
| --- | --- | --- |
| 公共设置沿完整PublicConfiguration原文件保存，同reference/digest修订 | Host/simulation/start引用固定；已有Freeze深拷贝隔离运行 | 每配方位置、额外活动坐标源/指针 |
| 启动准入与夹紧反馈分开 | 当前MotionAdmission.CheckStart不要求Ready，适配器发送PcReady后等待本次反馈；保持并验证 | 启动前要求后续反馈 |
| 观察显式位置/覆盖 | 当前仅返回槽号，无法判缺槽/空盘或F前行列；不能用配方推导 | 默认公式、旧盘配方 |
| 姿态处置独立于检测结果 | 当前mapper只收Participating，异常无动作；不能造DetectionObjectResult | 模拟算法Pending/OK |
| 同ThreeStage公共下料输入，同完成store扩展 | 原下料依赖plan，store强制三个Completed，projection绑定Inputs；必须解除提前结束假事实依赖 | 空RecipePlan、第二执行器/store |
| 组员显式物理位置 | 当前planner给全部member同unit号；validator也强制同号 | 组内共享物理穴、按显示号生成 |
| 现有真实提交门不变 | CommitPick及两次MayAuthorizePlace是放料必要条件 | 软件成功替代提交/反馈 |

可靠位置边界：Real当前PositionObservation缺用途/frame/unit映射，不能示教；Test/Virtual可用具名完整SIM_MACHINE输入但须显示来源。正常/提前准备矩阵仍用已发生本次3D camera/light/algorithm与Unload PLC事实，不放松全局来源要求。10秒只有后端一次选择，阶段/动作期限不因刷新/复查重开。
