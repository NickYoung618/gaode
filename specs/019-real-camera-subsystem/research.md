# 调研决策

2026-10-07；只读本地SDK和原代码，未用网络替代安装证据。

- Decision：沿用.NET10/现有Host及每相机常驻进程；Rationale：厂家阻塞API与SDK生命周期隔离，正常连接复用；Alternatives：不采用每次进程/进程池/消息总线。
- Decision：持久命名管道，JSON只放有限元数据，raw binary另帧；Rationale：不放大图Base64，不以文件目录交付；Alternatives：拒绝旧调试工具按任务目录交付。
- Decision：索引用既有SQLite Runs/Writes/Media和MediaStore；Rationale：不造第二最终媒体数据库；Alternatives：不以sidecar目录扫描当成功索引。
- Decision：Galaxy及CameraPro SDK仅在worker编译/加载；CameraPro发现userIP如实校验；Rationale：历史直接helper与旧工具native DLL摘要相同，未证明版本差异是回环原因，不强填SDK地址。
- Decision：3D XYZ/depth/IR全部必需；Rationale：用户澄清。IR是完整remapTexture多图payload，深度尺寸取depthType，厂家FrameData元素数不能当字节数。
- Decision：部署优先使用本机安装依赖。Galaxy本地许可2.2支持大恒相机SDK/驱动发布并需保留声明；CameraPro许可尚未找到，依赖现场安装。详细研究代理报告/路径后续补入依赖清单。

不确定项仅当前硬件实采和CameraPro许可；不阻塞正式接口及实现，不虚构通过。

## 已核实SDK及运行依赖（最终）

Galaxy wrapper 1.0.2512.8261，native GxIAPI.dll 2.0.2603.8121；分别来自旧调试vendor及D:\GalaxySDK\APIDll\Win64，已用正式worker实采核实可用，不能仅由版本号推断兼容。实际native SHA/加载目录随帧metadata保存。CameraPro.dll无文件版本，SHA F1DAEB867D5C16F3C1E7CBCDA095833ECB0A698E817C97E502B48BA0127648A6，旧helper与现SDK同摘要，未证明DLL版本导致旧回环地址；本次正式发现有效绑定通过。

Galaxy许可来源 C:\Users\Administrator\Desktop\相机接入调试\commissioning-0.1.2-work-20261003-141852\app\third-party-notices\galaxy-10400682-license.rtf，第2.2条允许大恒相机免费使用和发布SDK/驱动；同时携带D:\GalaxySDK\License\galaxy_3rd_party_licenses.txt。CameraPro本地未找到明确再分发许可，包不携带native文件，仅现场安装运行；源码仅引用外部include，不将厂家include或DLL提交Git。

软件依赖精确版本见各工程packages.lock.json、global.json及发布manifest.json；本次锁文件仅增加win-x64目标，未升级NuGet版本。Windows已安装.NET10/ASP.NET Core10；包为framework-dependent，不包含厂家安装程序、网卡驱动或VC运行库安装程序。现场SDK目录现有依赖已实跑，迁机须先按厂家安装程序完整安装。

首次并行SDK全接口发现曾遗漏B/F；Galaxy当前API没有指定NIC的发现入口。最终Host仅首次准备顺序执行，避免广播并发，运行采集继续独立；最终七台首次启动一次成功，不使用自动发现重试。
