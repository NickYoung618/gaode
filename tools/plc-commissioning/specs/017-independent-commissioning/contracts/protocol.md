# 地址、编码与操作合同
PC块MB2000；PLC块MB6000；PDU=config块起点+(MB-块起点)//2。BOOL共享寄存器，EvenLow偶数MB取低字节；EvenHigh反之。INT按有符号寄存器值；REAL从原始4字节按Abcd/Badc/Cdab/Dcba分别解码，默认Abcd，不自动更换。
心跳PC MB2011，PLC MB6038；MB6037为PC报警；MB6039保留无信号。
轴：X 2024/2001/6040/6064；Y 2028/2002/6042/6076；检测Z 2032/2003/6044/6084；扫码Z 2036/2004/6046/6088；抓取Z 2040/2005/6048/6092；R 2044/2000/6060/6068。顺序目标/启动/到位/实际。
单点写只PC方向。RMW的FC03读取和FC06写入在同一操作锁内保留相邻BYTE；可选FC22掩码，不暗中回退。INT/REAL FC16。
POST /api/connect {host,port}；/api/disconnect；/api/settings高级配置；/api/heartbeat {enabled}；/api/write {id,value}；/api/axis {axis,target}；/api/clear {axis}；/api/note {text,operationId}；GET /api/state,/api/export。JSON请求须本机会话token且同源，全部关联operationId/actionId。
反馈到位码0运动中/1到位/2超时；旧到位1只有本次反馈0或实际位置变化证据之后再次到位才能判本次完成。无证据则待确认直至超时。容差未配置只报告PLC动作完成与位置差，不报告坐标验收。


## 2026-10-07 REAL 字序修正 1.0.1
实际 Y=11 的 FC16 TX 为 4130 0000，PLC 显示约2.33849e-41。所有PC方向REAL统一采用Cdab交换16位字；BOOL/INT不变。PC目标读回采用写字序，PLC反馈采用独立读字序，保留Abcd待非零现场校准。保留旧realOrder配置作为兼容回退。离线验证11.0、12.5、-3.25及全部REAL字段；发布与启动切换不发送PLC写请求，不自动连接或恢复心跳。来源表地址不变，新增补丁仅修改独立工程。

补充现场非零证据：X=FFFE 409F(CDAB约5)、Y=0006 4130(CDAB约11)、检测Z=0028 40A0(CDAB约5)、扫码Z=000E 40A0(CDAB约5)、抓取Z=FFF9 403F(CDAB约3)。REAL反馈也已修正Cdab，收发仍单独可调；实际物理位置、速度以及三组值验收由现场监视表确认。网络Meta路由超时复发，传输增加可选sourceAddress（默认空），本机保存192.168.0.88，界面高级设置可调。恢复连接只有FC03读取，心跳关闭，未写入真实PLC。


## 1.0.2 XYZ反馈和实体网卡持久化
所有PLC方向REAL含XYZ、三个Z、R和速度反馈按Cdab解码，发送同为Cdab，BOOL/INT不变。启动初始化从模板补全独立字序；缺少绑定时按同网段在线实体网卡自动识别，优先名为PLC，多个候选不猜测。Meta等HardwareInterface=false不参与候选。高级设置显式sourceAddress优先；保留本机192.168.0.88。每次连接实际socket源/目标地址写入NETWORK_PATH及页面；不改全局路由或代理。启动不连接或写PLC，部署后只读验证。


## 1.0.3 本次动作完成证据修正
已证实静止坐标抖动被误当运动并结合旧到位1提前清零。删除actual!=baselineActual完成路径；启动前读到的到位1、启动后观察到运动中0、其后到位1才确认本次完成并按协议清零。旧到位、坐标变化、已在目标都不能独立完成；漏采运动中时显示待确认/超时，明确需手动核对/清零，不增设虚构脉冲时长。未知反馈失败且不清零。界面显示复位1、PC就绪0、心跳未翻转、轴启动保持等真实状态为联锁核对提示，不冒称PLC阻挡原因，不自动操作这些信号。REAL CDAB及实体网卡绑定保留。部署不重试旧动作、不自动恢复心跳。
