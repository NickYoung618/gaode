# 最终r4包验证接续（不覆盖失败）

- r4 NG实际run f51c4aa4-a5e3-4e4e-bc76-ed013d89daff在WPF完成Final并形成原始SQLite/媒体/PLC证据。
- 新检查器在统计格断言失败，原task 4ac96391d6bd40d1878d6bc93d17f138及Q01-NG-result.json保持失败。准确归因不是业务失败：选择器 `.grid .num-font` 在限定节点内查询时仍可使用外层aside.grid祖先，因而包括verdictBig，返回5个节点。原runtime过滤verdictBig后赋4格，截图显示正确。
- selector-proof.html来自最终包原HTML树，移除业务脚本/外部资源后在实际Edge只读验证：旧选择器5个，精确统计容器4个。selector-proof.json及selector-dom.html保留；这里的原型演示数字仅用于DOM结构检查，不作为业务结果证据。
- ng-existing-evidence-review.json三项只读复核全部退出0：实际分拣握手、每动作原始XYZ、SQLite/媒体/Final链。r4 NG截图人工复核编号正确、四指标为后端实际缺失、批次/MES未冒用其他字段。
- 未修改最终r4 ZIP、运行时或原失败报告；未重跑NG。verification-resume使用新请求，仅执行尚缺Pending/Q03，检查器改为精确统计格，并在断言前保存原始DOM供失败分析。
- r3→r4仅两个runtime副本及README不同（342文件完全同摘要），见package-difference.json。普通OK无盘末取放复用本轮r3的已完成真实包证据；不是开发机源码单测代替包验收。
