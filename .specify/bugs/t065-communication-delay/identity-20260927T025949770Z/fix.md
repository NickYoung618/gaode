# speckit-bug-fix：必要诊断修复（非通信业务修复）

Slug: t065-communication-delay。依据本目录assessment-supplement；用户本轮明确授权。

已修改仅私有源码副本中的ModbusTcpClient/ModbusTcpServer及其csproj，新增SocketIdentity.cs、CLR Threading/诊断profile、固定ObserveIdentity接线。记录连接/事务/实际字节/NativeOverlapped/QPC及回调线程，后加只读队列快照；实际验证证明初版探针过晚，改constructor诊断预热及Started门禁。原源码、旧冻结DLL、I/O期限、重试、成功、安全条件不变。

观察器修正空PLC日志NullString准备失败、PID/创建时间所属清理、有效连接计时；解析器修正原QPC与CLR/Kernel注册，后独立流式gzip构建消除已观测磁盘阻断。最后修正InspectWpt分支的数组比较为单一固定动作判断；ObserveIdentity固定入口先分支不受影响，未增加任意命令权限。

构建实际命令（工作目录仓库根；输出路径均本目录，旧构建保留）：
```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build '.specify/bugs/t065-communication-delay/identity-20260927T025949770Z/source/backend/src/Gaode.Host/Gaode.Host.csproj' --artifacts-path 'E:/dzk/gaode-1/.specify/bugs/t065-communication-delay/identity-20260927T025949770Z/prewarm-build' -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' build '.specify/bugs/t065-communication-delay/identity-20260927T025949770Z/source/VirtualPlc/VirtualPlc.csproj' --artifacts-path 'E:/dzk/gaode-1/.specify/bugs/t065-communication-delay/identity-20260927T025949770Z/prewarm-build' -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' build '.specify/bugs/t065-communication-delay/identity-20260927T025949770Z/reader-compressed/reader.csproj' -p:UseSharedCompilation=false
```

Host/PLC均0警告0错误。reader仅原QPC API的Discouraged警告，0错误；必须保留原QPC，不能用错误UTC替代。首个prewarm Host --no-restore因新artifacts路径没有assets失败，之后显式还原成功；console NETSDK1004记录见单独文件，不是业务运行失败。

真实采样通过已有Administrator固定任务Run(null)，全新ObserveIdentity请求；实际请求、命令与入口快照见每个task/root及证据索引。禁止重放这些请求。正式默认入口保留，只新增受限独立诊断入口，使用明确WPT依赖与构建摘要。

通信业务补丁未实施：根因A/C分解证据不足。共享业务接口没有变化，未更新spec/contracts/plan/tasks或任务勾选。诊断构建只用于本缺陷，退出方式为清理其所属进程/90秒探针上限；不可部署为正式程序。
