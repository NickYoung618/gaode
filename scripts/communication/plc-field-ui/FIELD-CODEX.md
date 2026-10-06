# 中控机 Codex 交接：0.4 确定地址基线

先读README.md、INTEGRATION.md、confirmed-points.json和specs/017-confirmed-plc-addresses。只使用最新PC.xls及PLC(2).xls；旧0.3模板不能写新版PLC。最新心跳MB6038，MB6037是PC_Alarm。

只读连接已经提供可修改默认参数，不以mapping.confirmed拦截；初值未经实机校准。检查数据时同时看原始寄存器与解释值。不要把软件默认PDU/字序当成用户确认。

根据同目录“中控机联调包开发提示词.txt”，在独立工作目录完成用户要求的简化操作和通信/动作界面，保留这份地址基线。与另一台主工程并行开发，回传基线SHA、源码和Spec Kit增量，不整目录覆盖。

优先验证：85项只读→心跳变化与同值应答→单点读写→X/R及各Z单轴本次运动与反馈→需要的翻面/取放。坐标、单位、真实运行条件不能造值；未填可选项不得挡住信号查看。真实动作由用户明确操作，不自动试写。

用同一套写协调保留共享字节；避免另一上位机同时写同一块。日志区分写请求应答、读回一致、PLC本次动作完成，并保留TX/RX。

独立源码位于source，页面wwwroot。完整发布包带Float32Codec.cs、BoolByteCodec.cs；源码构建使用.NET SDK10。原工程模板生成脚本依赖主工程configuration目录，独立包已经携带生成结果。
