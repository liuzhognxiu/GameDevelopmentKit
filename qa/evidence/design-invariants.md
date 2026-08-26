# 不器新规则基线：设计不变量与证据

| 不变量 | 验证证据 |
| --- | --- |
| 棋盘固定 10 格、仓库固定 10 格 | 417 项 EditMode；`final-ten-slot-deploy.png` |
| 每日固定六时段：经营、经营、PVE、经营、经营、PVP | 流程集成测试；`final-operation-screen.png` 的阶段轨道 |
| 天数不设上限 | Run Core、存档迁移与流程集成测试 |
| 9 场 PVP 胜利后，下一次第六时段进入三阶段天劫 | `BuqiRunCoreTests`、`BuqiRunDayLoopIntegrationTests`、最终流程集成测试 |
| 生命池初始为 20；普通 PVP 失败按当日扣除；首次耗尽进入心魔试炼，再次失败结束 | Run Core、Settlement 与 Day Loop 集成测试 |
| 战斗奖励只结算一次，读档后不重复领取 | Reward、Settlement、最终流程集成测试 |
| 不兼容旧存档直接舍弃并建立新局 | Save Codec 与 Settlement 测试；实机从旧数据进入有效新基线 |
| 静态本地化不存在可见 `<NoKey>` 泄漏 | `StarForceUIForm` 仅翻译已存在的 Key；实机截图与 Console 复核 |
| Battle Core 暂留 v0.6，确定性哈希不漂移 | Headless verify 全部 approved hashes matched |

尚未证实的体验判断：首次玩家能否不经讲解理解六时段、生命池和天劫目标，需要独立可用性测试，不能由自动化代替。
