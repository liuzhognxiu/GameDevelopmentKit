# 01｜单局循环与状态系统

## 1. 系统卡

| 字段 | 定义 |
|---|---|
| Purpose | 连续制造“读取题目—押注—验证—订正”，直到取得足够道印或失去全部道基 |
| Player experience goal | 每轮都能回答“我为什么这样改，以及下一战预期哪里不同” |
| Owner | `BuqiRunController` + 纯 C# `RunState` |
| Inputs | 起始方案、准备候选、玩家命令、道影摘要、BattleResult |
| Outputs | 下一阶段、状态差量、奖励/损失、最终 RunResult |
| 核心边界 | 顶层 Procedure 只管进入/退出；局内阶段不拆成多个 GF Procedure |

## 2. 单局结构

### 2.1 开局

1. 创建版本化 `RunState`。
2. 写入 `[PLACEHOLDER]` 初始灵石 6、`[PLACEHOLDER]` 道基 3、道印 0。
3. 固化三套起始方案候选；玩家只能选择一套。
4. 生成第 1 轮道影与准备入口，不因查看详情重新生成。

**Rationale**：三套起始方案用于教学三种关系，不是流派承诺。初始资源必须让玩家在第一轮做一次购买或保留的真实取舍；具体 6 灵石待 P-1/迷你局验证。

### 2.2 每轮

```text
RoundStart
-> 固化道影
-> 展示公开情报
-> 准备 A
-> 准备 B
-> 棋盘复核
-> 记录战前预测
-> 战斗
-> 复盘归因
-> 轮次结算
```

每个阶段只接受白名单命令。任何界面操作、动画回调或重复读取都不能自行推进状态。

## 3. 阶段规格

| Phase | 玩家决策 | 输入 | 输出 | 禁止 |
|---|---|---|---|---|
| StarterSelection | 选择想学习的关系 | 三套固定方案 | 初始持有物与布局 | 以“推荐强度”暗示答案 |
| EchoIntel | 判断主要威胁 | 已固化道影摘要 | 战前意图草稿 | 查看后更换对手 |
| PreparationA/B | 投资、转向或保留 | 固化入口与资源 | StateDelta | UI 重开重抽 |
| BoardReview | 位置、尺寸、仓位取舍 | 持有法门 | 合法快照 | 自动替玩家挤位置 |
| PreBattlePrediction | 明确一个可证伪预测 | 双方公开信息 | PredictionRecord | 用系统自动生成替代玩家判断 |
| Battle | 观察，无战斗输入 | 两份快照 | BattleResult/Log | 暂停或倍速改变结果 |
| Summary | 指认主因和下一步 | 日志与预测 | CauseRecord/ChangeIntent | 先给策略建议再让玩家回答 |
| RoundSettlement | 接受道印/道基与奖励 | 结果、未发放标记 | 下一轮或终局 | 重复结算 |

## 4. 进度与终局

- `[PLACEHOLDER]` 获得 5 枚道印：RunVictory。
- `[PLACEHOLDER]` 道基从 3 降至 0：RunDefeat。
- 每场有胜负战斗：胜利 +1 道印；失败 -1 道基。
- `[PLACEHOLDER]` 最多 7 场有胜负战斗，是 5 胜/3 败组合的数学上界，不含平局。
- 平局处理沿用 gameplay spec，但体验上必须记录是否让单局显著超时。

**验证路径**：记录有效闭环数量、中位局长、P90 局长、平局重赛耗时。若移动端中位局长高于 PC 的差值主要来自阅读/布局，则改信息与交互，不先砍战斗内容。

## 5. 命令模型

```text
RunCommand
- SelectStarter(starterId)
- EnterPreparation(offerId)
- Purchase/Refresh/Lock/Sell/Upgrade/Refine
- Place/Swap/ReturnToStorage
- ConfirmBoard()
- SubmitPrediction(subject, expectedEvent, expectedOutcome)
- ConfirmBattleResult(resultHash)
- SubmitCause(causeCode, sourceItemId, evidenceEventIds)
- SubmitChangeIntent(commandType, targetId, expectedEffect)
- ConfirmSettlement(settlementId)
```

每个命令返回：

```text
CommandResult {
  success;
  failureReason;
  stateRevisionBefore;
  stateRevisionAfter;
  stateDelta;
}
```

UI 只根据 `CommandResult` 更新，不先播放“购买成功”再等模型追账。

## 6. 保存与恢复

### 保存点

- 起始方案确认后。
- 每个准备命令成功后。
- 棋盘确认后，战斗前。
- BattleResult 生成后，结算前。
- 结算完成后。

### 幂等要求

- `settlementId` 已应用则重复确认不再发奖励。
- 战斗请求 hash 已有结果时恢复该结果，不重新选择道影。
- 未确认的商店、事件候选属于 RunState。
- 版本不兼容时显式拒绝恢复；不能悄悄换卡或重抽。

## 7. Edge Cases

| 情况 | 处理 |
|---|---|
| 两次准备机会都无法消费 | 允许跳过并保留资源；记录原因 |
| 棋盘无合法法门 | 禁止确认，定位具体非法状态 |
| 战斗后崩溃 | 从已保存 BattleResult 恢复，结算一次 |
| 平局后换道影 | 不重新准备；固化新道影并写入状态 |
| 连续第二次平局 | 按规格进入下一轮；补偿标 `[PLACEHOLDER]` |
| 玩家主动退出 | 保留当前单局；明确“继续/放弃” |
| 内容版本变化 | 有迁移规则才恢复，否则归档旧局并说明原因 |

## 8. Failure States

- 每轮流程完整，但玩家只是追最高品质。
- 玩家同时改动 3 个以上变量，无法判断哪项有效。
- 失败补偿使故意失败优于争胜。
- 两次准备都只是“进商店买更强”，没有入口级决策。
- 战报阶段过长，打断 8–12 分钟节奏。

## 9. Tuning Levers

| Lever | 当前状态 | Rationale / 验证 |
|---|---|---|
| 每轮准备次数 | `[PLACEHOLDER]` 2 | 需要足够修正空间，但不能一轮重做整套构筑；测每轮实质变更数 |
| 道印/道基 | `[PLACEHOLDER]` 5/3 | 目标约 4–7 次闭环；测局长与逆转率 |
| 最大有胜负战斗 | `[DERIVED]` 7 | 5 胜与 3 败的终局边界 |
| 战前预测字段数 | `[PLACEHOLDER]` 1 个主预测 | 降低填写负担；测跳过率和具体度 |
| 战后主因数 | `[PLACEHOLDER]` 1 主 + 可选 1 次因 | 防止复盘变成报表阅读考试 |

## 10. Acceptance

### 功能

- 相同 RunSeed 与操作序列产生相同候选、道影和结果引用。
- UI 重开不改变状态与 RNG 游标。
- 所有结算幂等。
- 非法阶段命令返回明确原因，不静默执行。

### 体验

- 玩家能连续完成“预测—结果—归因—针对性重构”。
- 多数轮次只改变 1–2 个可解释变量。
- 玩家即使跳过详细日志，也能在摘要层找到主因证据。
