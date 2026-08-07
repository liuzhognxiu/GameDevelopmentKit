# 《不器》首阶段系统设计包 v0.1 交付概览

## 完成内容

- 建立 1 份系统总纲与 9 份实现级规格：单局循环、棋盘/装备/改造、战斗/回放/复盘、经济/商店/事件、对手快照/公平性、跨端 UX/首玩、内容/叙事/长期精通、P-1/埋点/调参、集中 Tuning Register。
- 每个系统明确 Purpose、玩家决策、Inputs、Outputs、Owner、主流程、Edge Cases、Failure States、停止线和验收方法。
- 总纲提供共享状态模型、顶层状态机、系统交互矩阵与当前决策门。
- 集中 Tuning Register 为单局、构筑、战斗、经济、对手快照与内容变量记录当前值、状态、rationale、初始试验带、主指标和停止线。

## 关键设计裁决

1. Fun Hypothesis 保持为“预测自动构筑，观察结果，归因并付代价重构”；自动战斗是答案检查器，不是核心决策本身。
2. 当前 24 装备、6 改造、16 对手快照属于内部配置/模拟验证库，不等于玩家内容启用，也不等于 P-1 体验通过。
3. P-1 仍使用固定 9 装备 / 3 改造题目；目前仅完成自动构筑玩家 3/3 轮，自走棋与新手均为 0/3。
4. P-1 的 9 轮只判断三类玩家能否形成具体预测、证据归因和针对性改动；正式 `80%` 归因、`70%` 战败后重构只能用于后续较大样本，当前继续标 `[PLACEHOLDER]`。
5. `[CONTRACT]`、`[SCOPE]`、`[PLACEHOLDER]`、`[DERIVED]`、`[QUALITY]` 分离：工程正确性、制作范围和体验假设不得互相冒充。

## 当前边界

- 未修改 `buqi-battle-contract.md` v0.4.1、战斗代码、approved hash 或 Unity 资源。
- 设计包不授权 Step 4 正式战斗 UI、完整商店/事件经济、在线异步对手池或内部验证库整体玩家启用。
- 下一门禁仍是自走棋玩家与新手玩家各完成 3 轮 P-1 认知走查。

## 文档入口

主入口：`docs/game-concepts/systems/README.md`  
调参入口：`docs/game-concepts/systems/09-tuning-register.md`  
项目总索引：`docs/game-concepts/buqi-core.md`
