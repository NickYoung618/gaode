# 调研决策

2026-10-07；只读本地SDK和原代码，未用网络替代安装证据。

- Decision：沿用.NET10/现有Host及每相机常驻进程；Rationale：厂家阻塞API与SDK生命周期隔离，正常连接复用；Alternatives：不采用每次进程/进程池/消息总线。
- Decision：持久命名管道，JSON只放有限元数据，raw binary另帧；Rationale：不放大图Base64，不以文件目录交付；Alternatives：拒绝旧调试工具按任务目录交付。
- Decision：索引用既有SQLite Runs/Writes/Media和MediaStore；Rationale：不造第二最终媒体数据库；Alternatives：不以sidecar目录扫描当成功索引。
- Decision：Galaxy及CameraPro SDK仅在worker编译/加载；CameraPro发现userIP如实校验；Rationale：历史直接helper与旧工具native DLL摘要相同，未证明版本差异是回环原因，不强填SDK地址。
- Decision：3D XYZ/depth/IR全部必需；Rationale：用户澄清。IR是完整remapTexture多图payload，深度尺寸取depthType，厂家FrameData元素数不能当字节数。
- Decision：部署优先使用本机安装依赖。Galaxy本地许可2.2支持大恒相机SDK/驱动发布并需保留声明；CameraPro许可尚未找到，依赖现场安装。详细研究代理报告/路径后续补入依赖清单。

不确定项仅当前硬件实采和CameraPro许可；不阻塞正式接口及实现，不虚构通过。
