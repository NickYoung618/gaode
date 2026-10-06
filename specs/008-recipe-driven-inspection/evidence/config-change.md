# 第六批：同程序配置变化验收

日期：2026-09-25。结论：C08和008 T058的**实际配置变化验收子项**在Test虚拟环境通过；T058整项仍按原前置与C01空槽要求审核，不把本报告等同生产标定。

| 项目 | Q01基版 | Q01-PARAM |
| --- | --- | --- |
| 正式页面运行 | `bed5e53a-cc74-410d-93d7-cc0a9d99b278` | `b6232908-8d41-4a47-8e1a-ae25bc2e1ebd` |
| 配方 | R008-Q01 / 1.1.1-test | R008-Q01 / 1.2.0-test |
| 目录摘要 | `2391FD64ECBBE7558CAFAB9A30909DC2448EA29705B48676DB3A975A9B30417D` | `6D055C2C9232638D7F309B1A55C8178820AD0544B43CB9C3125D480F684A1EB9` |
| 实际产品顺序 | A:P01→B:P01 | A:P01→A:P03→B:P01→B:P03 |
| 实际采集请求 | 10000 µs、亮度60、ROI1024²、稳定100 ms | 12000 µs、亮度75、ROI960²、稳定150 ms |
| 算法配置 | SIM_ALGORITHM、Test seed 7001 | SIM_ALGORITHM_PARAM、Test seed 7031 |
| 页面最终状态 | Final | Final |

两次使用相同Host DLL摘要`258591629D801B5F4129E199C201ACE1186A8B27E1754F1A1E6BD0D9258AC4D5`、VirtualPlc摘要`B9619E74B940DC67ED9AB254F005F28BB46889718093AE317B16F878E2EBC3D9`、前端runtime摘要`5FEDD162AF5682DC13F494081C6824D8FA593253AB65DC2DA7123490958DC74E`。新目录在无活动运行的独立Test根装载；基版SQLite冻结目录摘要保留原值。新run的页面选择、F实际解码、冻结版本/摘要、计划和保存记录一致。

Q01-PARAM经本轮3D实际采集与独立worker得到sample-a 10.19、sample-b 11.253；显式Test映射分别派发P01 Z110.19、P03 Z111.253。四次逐图分析/复位、两次同面融合、六份媒体和worker结果均可读回；下料、解锁、页面点击取盘及Final同run可核对。该素材、映射和亮度/曝光请求均为Test虚拟来源；文件模拟相机不会证明真实SDK/光源已设置，也不证明现场高度标定或识别精度。

证据：[基版第五批报告](fifth-batch-q01-q02.md)；[新run运行包](../../../artifacts/recipe-execution-008/sixth-batch/q01-param-20260925-052041/Q01-PARAM/)的上级目录`validation-result.json`及包内`case-result.json`、`verified-facts.json`、`config-change-verified.json`、页面截图、Host/PLC/worker日志、SQLite及媒体读回。生成源为`fixtures/generate-q01-param-test.py`，验证工具为`scripts/summarize-q01-q02-evidence.py`。
