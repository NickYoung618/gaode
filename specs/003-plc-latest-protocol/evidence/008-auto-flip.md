# 008自动翻面：003 T071当前接口边界

2026-09-25第八批。指定PLC协议§2.3、§3.1.5已定义`XY_Move_Cmd=3`到翻转工位、到位后写`Flip_Target_Face`、PLC内部整体翻面、`Flip_Status=2`及`Flip_Current_Face=目标面`双重确认，人工区占用禁动。总时序图要求整体翻面指令包含取料和放料两组坐标；需规FLP-003要求来自固定点位表，CTL-008禁止旧完成状态抵本次。

Host已通过`FlipFeedbackCorrelation`实现连接代次、发令前后状态和新反馈关联判别，定向规则`artifacts/recipe-execution-008/eighth-batch-testresults/q03-eighth-flip-feedback.trx`为3/3。设备观察已读取协议定义的翻面状态、实际面号和人工区占用。正常触发无需新定义；Host动作ID、写入成功本身不作为PLC完成证据。

仍缺具体设备提交语义：取料/放料两组坐标分别对应现有哪个字段或寄存器，以及相对命令3、到位、目标面写入的提交顺序。现有通用相机目标字段和抓取Z不能无来源地解释为两个完整翻面点。该缺口使自动翻面适配器保持未配置，当前正式路线没有写命令3、目标面，也没有实际PLC翻面反馈或Q03页面Final。003 T071只登记已定义字段观察及Host内部判别子范围，整项未完成。完整阶段证据见[008第八批](../../008-recipe-driven-inspection/evidence/eighth-batch-auto-multiface.md)。
