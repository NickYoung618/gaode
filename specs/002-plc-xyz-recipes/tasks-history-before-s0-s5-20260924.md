# 开发任务

- [x] T01 保留并记录原测试基线。
- [x] T02 独立VirtualPlc、按钮夹紧、信号所有权。
- [x] T03 Gaode原生协议、连接泵、心跳、启动、XYZ、清零、区域和配方ID。
- [x] T04 XYZ配置合并冻结、容差/限位与模式准入。
- [x] T05 MotionCoordinator统一运动期限。
- [x] T06 F后独立配方绑定、快照、数字ID及区域覆盖。
- [x] T07 Host配置、运行脚本、独立存储准备。
- [x] T08 原测试回归、跨进程第一工位和F后配方联调、故障验证。
- [x] T09 记录证据、文件清单、真实接入边界。

## 2026-09-24 最新需求与008完整执行对齐

以下是新要求的未完成关联任务，执行工作由008对应任务主责；同步回写实际证据后才分别判定，不要求重复实现。历史任务状态保持不变。

- [ ] T10 落实008 T002—004关联的配方场景/路线解耦及槽位/身份/参数校验，产物backend/src/Gaode.Application/Recipes/RecipeContracts.cs、RecipeRunPlanner.cs及backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs；依赖008目标合同，完成需22表达检查及两配方差异生效，证据归008，不改T01—T09历史勾选。 追溯：FR11、宪章3.2.0 P03/P07/P08/P09/P11/P13（006另P12）。

