# 《不器》无限修行基线 QA 报告

结论：工程与规则回归通过；正式首次上手体验评估未执行，因此状态记为 `PASS_WITH_NOT_RUN`，不把自动化通过等同于“已经好玩或已经易懂”。

已验证：

- Unity EditMode 417/417 通过；完整结果位于 `evidence/buqi-infinite-baseline/editmode-417.xml`。
- 两个 .NET 目标均为 0 error；仅保留 `TutorialForm.m_SkipButton` 的既有未使用字段警告。
- 无头规则、确定性与批准哈希全部通过。
- 实机可进入运行流程，显示六时段、10 格棋盘和 10 格仓库；PVE 奖励能够领取并进入第四时段。
- 最终运行时 Console error 为 0；可见 `<NoKey>` 已消除。
- 用户原存档已按原 SHA256 恢复，QA 临时备份随后删除。

已知边界：

- 完整 9 胜到三重天劫由集成测试覆盖，本轮未手动重复游玩全部循环。
- 尚未安排独立首次玩家验证引导、术语理解和操作节奏。
- Unity 自动序列化 Prefab 与 Luban 生成代码仍包含生成器尾随空格，`git diff --check` 会报告这些生成行；未人工清洗，以避免序列化漂移。
