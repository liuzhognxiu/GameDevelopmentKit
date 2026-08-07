# 《不器》首阶段系统设计包 v0.1

> 状态：设计展开稿；用于实现、配置、UI 与 Playtest 对齐。  
> 规则基线：`../buqi-gameplay-spec.md` v0.4、`../buqi-battle-contract.md` v0.4.1。  
> 当前工程：Step 1/2/3、Charge v0.4.1 与内部效果验证库均已验收；当前验证库为 8 构筑方向、24 装备、6 改造、16 对手快照。P-1 已完成第 1 名自动构筑玩家的 3/3 轮，自走棋玩家与新手玩家尚未开始，体验门禁仍未通过。
> 冲突优先级：战斗语义以 battle contract 为准；首阶段范围以 gameplay spec 为准；本文只补系统责任、体验目标和验证方法。

## 1. Fun Hypothesis

> 玩家用有限格位与带代价的装备组装一套自己能预测的自动构筑，对抗公开但不完整的对手快照；结果证明或推翻判断后，玩家通过一次有代价的重构，让下一战明显更接近自己的预期。

### 可证伪条件

玩家能复述战报，却不能提出一个具体改动及预期结果，核心乐趣仍判失败。自动战斗只是答案检查器；没有订正，就只有自动播放。

## 2. Design Pillars

| 支柱 | 机制过审问题 | 否决条件 |
|---|---|---|
| 先预测，再验证 | 开战前信息能否支持一个具体预测？ | 只能事后合理化 |
| 每格都代表取舍 | 放入该装备时放弃了什么？ | 大型装备只是更大数字 |
| 强度必须带账单 | 收益对应哪个成本、风险或适配损失？ | 所有构筑都无脑选 |
| 败因能转成行动 | 主因是否对应至少两种成本不同的处理？ | 战报只负责宣布死因 |
| 对手快照是题目，不是倍率墙 | 玩家是在识别结构还是追战力数字？ | 隐藏倍率决定难度 |

## 3. 数值状态标签

| 标签 | 含义 | 使用规则 |
|---|---|---|
| `[CONTRACT]` | 已进入 v0.4.1 实现或确定性契约 | 修改必须更新规则版本、测试向量与 hash |
| `[SCOPE]` | 已批准的首阶段制作边界 | 不用 tuning 擅自扩容 |
| `[PLACEHOLDER]` | 未经目标玩家 Playtest 的体验数值 | 必须附假设、验证指标和可重画范围 |
| `[DERIVED]` | 由其他已标记变量推导出的结果 | 不独立调参；上游变量变化时重新计算 |
| `[QUALITY]` | 工程质量或异常率门槛 | 用自动化验证，不拿来替代体验结论 |
| `[TO VALIDATE]` | 方向合理但尚无样本支持 | 不进入对外承诺或完成验收 |

## 4. 系统文档目录

| 文档 | 系统责任 | 主要消费方 |
|---|---|---|
| [01-run-loop.md](01-run-loop.md) | 单局状态机、轮次、胜负与保存 | Run、Procedure、UI |
| [02-board-items-refinement.md](02-board-items-refinement.md) | 棋盘、仓位、装备、品质、改造 | Board、Config、UI |
| [03-combat-replay.md](03-combat-replay.md) | 确定性战斗、表现、战报与归因 | Battle、Replay、UI |
| [04-economy-shops-events.md](04-economy-shops-events.md) | 金币 Sources/Sinks、商店、事件与补偿 | Run、Config、UI |
| [05-echo-opponents.md](05-echo-opponents.md) | 对手快照制作、情报、筛选与公平性 | Echo、Run、UI |
| [06-ux-onboarding-crossplatform.md](06-ux-onboarding-crossplatform.md) | 首玩教学、PC/移动交互与信息层级 | UI、UX、QA |
| [07-content-narrative-mastery.md](07-content-narrative-mastery.md) | 内容架构、叙事映射、长期精通 | Design、Narrative、Config |
| [08-validation-telemetry-tuning.md](08-validation-telemetry-tuning.md) | P-1、埋点、平衡、经济模拟和停止线 | Design、QA、Data |
| [09-tuning-register.md](09-tuning-register.md) | 可调变量、rationale、试验带、主指标和停止线 | Design、Config、QA、Data |

## 5. 共享状态模型

```text
RunState
├── identity: runId, contentVersion, randomVersion, runSeed
├── progress: phase, roundIndex, daoSeal, daoFoundation
├── economy: spiritStone, pendingRewards, temporaryDebts
├── collection: board, storage, itemInstances
├── choices: currentPreparationOffers, shopState, eventState
├── opponent: selectedEchoId, disclosedIntel
├── cognition: preBattlePrediction, postBattleCause, intendedChange
└── random: preparation/shop/eventReward/echo cursors

BattleRequest = normalized player BuildSnapshot + echo BuildSnapshot + ruleVersion
BattleResult = outcome + final state + BattleLog + hashes
```

UI 不直接修改 `RunState`；所有改变通过命令服务返回 `Success/FailureReason/StateDelta`。表现层不能重新计算战斗结果。

## 6. 顶层状态机

```text
StarterSelection
  -> RoundStart
  -> EchoIntel
  -> PreparationA
  -> PreparationB
  -> BoardReview
  -> PreBattlePrediction
  -> Battle
  -> Summary
  -> RoundSettlement
  -> (RoundStart | RunVictory | RunDefeat)
```

### 全局不变量

1. 战斗开始后玩家不能改变战斗输入。
2. 每轮只能从当前固化候选中消费准备机会；打开/关闭 UI 不消费 RNG。
3. `BoardReview` 结束时必须产生合法、规范化的 `BuildSnapshot`。
4. 战斗结果只由 v0.4.1 模拟核产生。
5. 战后结算前必须保存结果；异常退出恢复后不得重复发放奖励。
6. 每场战斗保留“预测—结果—主因—预期改动”认知链，用于 P-1 与后续体验验证。

## 7. 系统交互矩阵

`I` = intended，`A` = acceptable，`B` = bug。表中描述以前者影响后者。

| Source → Target | 战斗 | 棋盘/装备 | 经济 | 商店/事件 | 对手快照 | UI/复盘 | 叙事/长期 |
|---|---|---|---|---|---|---|---|
| 战斗 | I：触发链互相作用 | B：战中改构筑 | B：战中直接改金币 | A：只消费已固化临时效果 | B：按结果改对手快照 | I：输出日志 | A：输出事实标签 |
| 棋盘/装备 | I：生成快照 | I：尺寸/相邻/品质 | I：价格与出售价值 | I：读取标签/持有状态 | I：形成构筑特征 | I：提供可视结构 | I：表达玩家道路 |
| 经济 | B：提供隐藏战斗倍率 | I：限制购买与升级 | I：Sources/Sinks | I：支付成本 | A：只参与投入筛选 | I：展示变化原因 | A：影响短期选择 |
| 商店/事件 | A：只通过显式临时修正 | I：增删改实例 | I：产生/消耗金币 | I：固化候选 | B：战后篡改已选对手 | I：完整预览结果 | I：承载坊市故事 |
| 对手快照 | I：提供对方快照 | B：改变玩家棋盘 | B：按玩家存量动态加价 | B：控制玩家货架来克制 | I：版本与公平性 | I：输出公开情报 | I：承载他人道路 |
| UI/复盘 | B：改变模拟 | I：提交命令 | B：本地先扣后回滚 | B：重开重抽 | B：泄露未公开字段 | I：解释事实 | A：展示叙事文本 |
| 叙事/长期 | B：对白赋予隐藏伤害 | B：永久数值强化 | B：首阶段发永久货币 | A：改变内容呈现，不改既定规则 | A：附加人格标签 | I：增强意义 | I：知识成长 |

## 8. 当前决策门

原评审提出在 Step 1 前做 P-1；工程实际已完成 Step 1/2/3，因此门禁按当前事实调整为：

> **利用现有 Battle Sandbox 与固定题目执行 P-1 认知走查；P-1 体验门禁通过前，不进入 Step 4 正式战斗 UI、完整经济内容，也不把内部效果验证库作为玩家内容整体启用。**

P-1 不推翻已验收的确定性内核、配置链路或内部效果验证库，只决定卡面信息、对手快照公开量、三类准备选项和战报层级是否值得进入正式玩家流程。`24/6/16` 是工程覆盖面，不是体验门禁通过的替代证据。

## 9. Definition of Done

一份系统只有同时满足以下条件才算可交付：

- Purpose 与玩家决策明确。
- Inputs、Outputs、Owner 和依赖明确。
- 主流程、非法流程和中断恢复明确。
- Edge cases、Failure states 和停止线明确。
- 所有 cost/reward/duration/cooldown 有 rationale 或标 `[PLACEHOLDER]`。
- 至少一个功能测试和一个体验验证方法。
- 不通过跨系统暗改制造“暂时能跑”的特例。
