# 《不器》确定性战斗契约 v0.4.1

> 状态：0.4.1 定型蓄力声明时读取/消费契约；旧六效果向量继续由该规则版本和 approved hash 保护。
>
> 内容扩展：`ContentVersion=buqi-effects-cv1` 在同一确定性内核上增加 Heal、Regen、Poison、Burn、Freeze。双方快照必须使用相同且由 definition provider 认可的内容版本。未知内容版本或旧消费者不能静默接受扩展枚举。

## 1. Contract Identity

- `GameId`: `buqi`
- `RuleVersion`: `0.4.1`
- `SimulationVersion`: `battle-core-0.4.1`
- 1 tick = 100 ms；正常阶段 450 tick，硬上限 600 tick。
- 模拟层只使用整数；倍率使用万分比 `basis points`。
- 视觉层可连续插值冷却条，但不得参与结果计算。

## 2. Request DTO

```text
BattleRequest {
    string ruleVersion;
    ulong battleSeed;
    int roundIndex;
    BuildSnapshot left;
    BuildSnapshot right;
}

BuildSnapshot {
    string snapshotId;
    string contentVersion;
    string archetypeId;
    int initialExecution = 100;
    int initialBuffer = 0;
    int initialNoiseDebt = 0;
    ItemInstance[] items;
}

ItemInstance {
    string instanceId;
    string definitionId;       // W8-001 ... W8-018
    int quality;               // 1普通, 2改良, 3定型
    int anchorSlot;            // 0..7，始终为最左占位
    string annotationId;       // 空字符串或 A-01 ... A-06
    TemporaryModifier[] temporaryModifiers;
}
```

### 2.1 规范化与校验

1. `items` 按 `anchorSlot`、`instanceId` 序数升序。
2. 配置定义、品质、批注、临时效果和内容版本必须有效。
3. S/M/L 分别覆盖 1/2/3 格；不得越界、重叠或重复实例 ID。
4. 棋盘必须至少有 1 张事项卡。
5. 输入不合法时返回 `InvalidBuild`，不尝试静默修复。
6. 规范化快照使用固定字段顺序序列化，并计算 SHA-256 `SnapshotHash`。

## 3. Runtime State

```text
SideState {
    int execution;
    int maxExecution;
    int buffer;
    int noise;                 // 0..9
    TimedStatus[] statuses;    // Regen / Poison / Burn
    ItemState[] items;
}

ItemState {
    string instanceId;
    int cooldownProgress;      // 剩余进度，单位为 1/10000 tick
    int charge;                // 0..9
    int frozenTicks;
    int ownUseCount;
    int adjacentUseCount;
    bool firstConditionUsed;
    bool firstInterferenceUsed;
    TimedModifier[] modifiers;
}
```

执行值、缓冲、噪音属于阵营；充能和触发计数属于实例。战斗结束后全部丢弃。

## 4. Result and Log

```text
BattleResult {
    BattleOutcome outcome;     // LeftWin, RightWin, Draw, InvalidBuild, Aborted
    int durationTicks;
    int leftExecution;
    int rightExecution;
    int leftBuffer;
    int rightBuffer;
    int leftNoise;
    int rightNoise;
    string terminationReason;
    string battleLogHash;
    string leftSnapshotHash;
    string rightSnapshotHash;
}

BattleEvent {
    int sequence;
    int tick;
    EventPhase phase;
    int chainDepth;
    string chainId;
    string actorInstanceId;
    string sourceInstanceId;
    string targetInstanceId;
    EventType type;
    int amount;
    string effectId;
    string reasonCode;
}
```

日志必须区分“声明”“生效”和“空转/截断”，使 UI 能准确展示连锁和中断原因。

## 5. Tick Phases

每个 tick 严格按以下阶段执行：

1. `PreTick`：减少临时效果持续 tick，推进事项卡冷却，声明本秒加班伤害。
2. `Declare`：收集冷却到期的主动使用、首次条件和受干扰响应。
3. `Resolve`：确定目标，将声明展开为当前内容版本允许的效果和计数事件。
4. `Chain`：处理相邻组件使用、累计次数和复写追加；重复 Resolve，直到队列为空或达到上限。
5. `Aggregate`：汇总同 tick 双方普通伤害、缓冲和直接伤害；按稳定顺序处理加速、延迟、充能和噪音。
6. `PostTick`：应用汇总值，写日志，检查胜负或硬上限。

同阶段固定排序键：

`(tick, phaseOrdinal, chainDepth, sourceAnchorSlot, sourceInstanceId, eventTypeOrdinal, sequence)`

容器迭代顺序、帧率、动画时长和平台区域设置均不能影响排序。

## 6. Cooldown

战斗开始：

`cooldownProgress = baseCooldownTicks * 10000`

每个 PreTick：

`cooldownProgress -= Clamp(10000 + HasteBps - DelayBps, 5000, 15000)`

当进度 `<= 0`：

1. 声明一次主动使用。
2. 进度加回 `baseCooldownTicks * 10000`，保留负溢出，避免高频卡累计漂移。
3. 若仍 `<= 0`，也不能在同一 tick 再次主动使用；等待下一 tick。

品质默认不修改冷却。A-01 加急和 A-02 延期在初始化时修改基础冷却，最终至少 10 tick。

## 7. Trigger Contract

首阶段触发类型：

```text
OnUse
OnBattleStart
OnAdjacentUse
OnFirstConditionMet
OnUseCountReached
OnFirstInterfered
```

规则：

- `OnBattleStart` 在 tick 0、首次冷却推进前声明。
- `OnAdjacentUse` 只响应主动 `OnUse`，不响应复写、计数触发或其他响应。
- `OnFirstConditionMet` 每实例每场最多一次，在条件首次由 false 变 true 时声明。
- `OnUseCountReached` 达阈值即声明并按配置清零或扣除计数。
- `OnFirstInterfered` 只响应敌方施加的首个有效延迟；延迟被 A-04 可靠免疫时不算有效。
- A-03 复写只复制首次主动使用的直接效果，倍率 50%，不产生 `OnAdjacentUse`，不能再次被复写。

上限：单实例每 tick 最多 4 次主动/响应事件；全场每 tick 最多 64 个事件。超限时丢弃当前 chain 后续事件，记录 `LoopCapReached`，继续战斗。

## 8. Targeting

合法选择器：

```text
EnemyExecution
Self
LeftAdjacentItem
RightAdjacentItem
AllAdjacentItems
ShortestCooldownEnemyItem
LongestCooldownEnemyItem
LeftmostEnemyItem
RightmostEnemyItem
```

- 相邻由 8 格占位重建；空格阻断。
- 冷却比较使用当前 `cooldownProgress`；相同按锚点、实例 ID。
- 没有合法目标时记录 `NoValidTarget`，该分支不生效。
- 战斗目标不使用随机。`battleSeed` 仅保留给未来规则版本，本版本不能消费 RNG。

## 9. Effect Types

9.1-9.5 是 v0.4.1 基础效果；9.6 是 `buqi-effects-cv1` 内容扩展。扩展不得改变旧内容向量的结果和 hash。

### 9.1 Damage

普通伤害同 tick 汇总后应用：

```text
absorbed = Min(buffer, normalDamage)
buffer -= absorbed
execution -= normalDamage - absorbed
execution -= directDamage
```

直接伤害只来自噪音事故和加班。

### 9.2 Buffer

同 tick 新获得缓冲在普通伤害之前加入缓冲池：

`buffer = Min(60, buffer + grantedBuffer)`

该顺序让双方同 tick 防御声明均有效，且不依赖左右先后。战报分别记录“生成缓冲”和“实际吸收”。

### 9.3 Haste and Delay

- 同来源重复施加同效果时刷新持续时间并取较高幅度，不叠加。
- 不同来源可以叠加，最终分别求和，再将推进倍率限制为 50%-150%。
- A-04 可靠：忽略敌方延迟，且忽略友方加速；仍记录 `Immune`。
- 到期在该 tick 冷却推进完成后移除，使显示和模拟一致。

### 9.4 Charge

`charge = Clamp(charge + delta, 0, 9)`

蓄力增减、读取和消费都在稳定的 `Declare` 顺序中立即落地，事件 `amount` 记录有符号资源变化：获得为正、消费为负。这样先声明的蓄力来源可被同 tick 后续声明读取，同时避免同一蓄力被多个消费型声明重复使用。每条效果配置 `chargeReadLimit`、`amountPerCharge` 与 `chargeConsume`：先读取 `Min(currentCharge, chargeReadLimit)`，将 `amount + readCharge * amountPerCharge` 作为该声明的基础效果量；`chargeConsume=true` 时立即扣除本次读取量，后续声明只能读取剩余蓄力。只读效果不扣除，可在同 tick 再次读取同一当前值。A-03 复写复用原主动声明的读取快照并应用 50% 倍率，不再次消费。没有合法目标时不读取也不消费。只允许卡牌文本给自身或相邻卡增加蓄力。

### 9.5 Noise

噪音事件按稳定事件顺序逐个处理：

```text
noise += delta
while noise >= 10:
    noise -= 10
    directDamage += 8
noise = Max(noise, 0)
```

已经跨阈值声明的事故不被同 tick 后续降噪撤销。A-05 静音先修改来源事件的噪音增量，最低为 0；A-06 超额在 tick 0 产生 3 噪音。

### 9.6 Heal, Regen, Poison, Burn and Freeze

- `Heal`：在普通伤害与灼烧之后、Poison 之前立即恢复执行值，最高不超过 `MaxExecution`；溢出记录 `HealOverflow`。
- `Regen`：按来源实例与效果类型维护持续状态，每 10 tick 产生一次 Heal。相同来源刷新时取较高强度与较长剩余时间，并保留当前 tick 进度。
- `Poison`：按来源实例维护持续状态，每 10 tick 直接扣执行值，不经过缓冲。
- `Burn`：按来源实例维护持续状态，每 10 tick 产生可被缓冲吸收的伤害，并参与缓冲清空条件。
- `Freeze`：作用于确定性选中的敌方事项卡；冻结剩余 tick 大于 0 时，该卡本 tick 不推进冷却并消耗 1 tick。相同目标刷新时取较长剩余时间。
- 新施加的持续状态和冰冻在本 tick Aggregate 写入，从下一 tick 开始产生持续效果或阻断冷却；不会追溯取消本 tick 已声明事件。

## 10. Simultaneous Resolution

同 tick 的双方声明都必须进入 Aggregate 后才改变执行值。固定应用顺序：

1. 新增缓冲。
2. 普通伤害由缓冲吸收，剩余扣执行值。
3. 灼烧伤害由缓冲吸收，剩余扣执行值。
4. 治疗与生命恢复恢复执行值，不超过最大值。
5. 中毒直接扣执行值。
6. 噪音事故与加班直接扣执行值。
7. 写入加速、延迟、持续状态和冰冻；蓄力已按稳定 `Declare` 顺序即时更新。
8. PostTick 检查胜负。

因此，一方在本 tick 被击至 0 也不会取消其已经声明的效果。双方均 <=0 判平局。

## 11. Overtime and End Conditions

- tick 0-449 为正常阶段。
- 从 tick 450 后的第一个完整秒开始，每 10 tick 声明一次加班直接伤害。
- 伤害公式：`2 + Floor(completedOvertimeSeconds / 5)`。
- 任一 PostTick 结束时一方执行值 <=0，按 10 节判胜负。
- tick 600 PostTick 后仍存活，依次比较执行值、缓冲、噪音：前两项高者优先，噪音低者优先；仍相同则 Draw。

## 12. Content Semantics

v0.4 基础 18 张事项卡只由以下原语组合：

- 造成普通伤害。
- 获得缓冲。
- 添加/读取/消耗充能。
- 给自身或合法目标添加加速/延迟。
- 增减己方噪音。
- 读取尺寸、标签、相邻、使用次数、缓冲损失和首次干扰。

`buqi-effects-cv1` 已通过通用枚举、状态模型、配置校验和确定性测试加入 Heal/Regen/Poison/Burn/Freeze，不按法门 ID 写特例。取消触发、暴击、弹药、随机目标、召唤或其它新语义仍属于规则扩容，必须先增加独立契约、内容版本与跨端测试。

## 13. Determinism Tests

1. 相同输入重复 100 次，`BattleResult` 和 `BattleLogHash` 完全一致。
2. 固定测试向量在 Windows 编辑器、无头 .NET 测试和目标平台得到相同 hash。
3. 左右镜像输入产生镜像胜负、相同持续 tick 和绝对资源变化。
4. 打乱内部事件插入顺序不改变 Aggregate 结果。
5. 8 格 S/M/L 占位与相邻重建正确。
6. 同 tick 新缓冲、普通伤害和直接伤害按 10 节顺序处理。
7. 加速与延迟多来源叠加、刷新、上限和 A-04 免疫正确。
8. 蓄力在同 tick 声明时按稳定事件顺序消费；同一份蓄力不能被多个消费型声明重复读取，A-03 复写不二次消费，且蓄力不超过 9。
9. 噪音跨越 10 和 20 时事故次数、余数和直接伤害正确。
10. A-03 复写不触发相邻响应，也不能复制自身。
11. 连锁达到 64 事件后安全截断并记录原因。
12. 45 秒加班和 60 秒比较顺序正确。
13. 重叠、越界、未知定义、未知批注和版本不匹配全部拒绝。
14. Heal 不超过最大执行值，溢出量可追溯。
15. Regen、Poison、Burn 按固定 10 tick 周期结算，同来源刷新不重置 tick 进度。
16. Poison 绕过缓冲，Burn 经过缓冲并可触发缓冲清空条件。
17. Freeze 阻断后续 tick 冷却推进，到期后恢复；同 tick 目标选择稳定。
18. 旧六效果向量在 `buqi-effects-cv1` 实现加入后仍保持 approved hash。

### 13.1 三端验证方式

运行时模拟源码保留在 `Game.Hot.Code`，同时由 `Share/Buqi.Simulation.Headless` 的 .NET 8 控制台项目通过 MSBuild 链接编译。该验证项目不得引用 Unity、UGF、ET、资源系统或热更生命周期；只要模拟源码引入这些依赖，无头项目就必须编译失败。

Unity EditMode 测试、无头 .NET 验证器和 Windows Player 使用同一组版本化 JSON 测试向量。三端分别输出规范化 `BattleResult` 与 `BattleLogHash`，逐项比较，不允许在某一端维护独立期望值。

“相同输入重复 100 次”只验证确定性，不计入平衡样本量。平衡测试必须使用不同的合法构筑向量，并交换左右阵营；重复同一确定性对局不能改变胜率统计权重。

## 14. Battle Log Requirements

日志必须支持：

- 从初始快照重建执行值、缓冲、噪音、充能、冷却和临时状态。
- 按 `chainId` 定位连锁来源、目标和响应卡。
- 区分实际伤害、缓冲吸收、溢出缓冲、免疫、无目标和上限截断。
- 统计伤害贡献、有效缓冲、延迟覆盖时长、充能生成/消耗、噪音事故和空转。
- 保存规则版本、内容版本、双方快照 hash、种子和结束原因。

固定序列化后的日志计算 SHA-256。字段顺序、枚举序号和空值表示法必须版本化。

## 15. Unity Integration Boundary

纯模拟核心只依赖 .NET 基础库和项目 DTO，不引用：

- `UnityEngine`
- UGF 或 ET 生命周期
- `Time.deltaTime`
- Unity Random
- 场景对象、动画、UI、资源加载或网络

推荐目录：

```text
Unity/Assets/Scripts/Game/Hot/Code/Buqi/Battle/
    Model/
    Rules/
    Simulation/
    Logging/
    Tests/
```

Unity 表现层读取日志，以连续插值显示冷却、暂停、1x/2x/4x 和跳过；动画落后时可追赶或省略，不能反向修改模拟。

## 16. Compatibility

- `contentVersion` 不同先走显式迁移；没有规则则拒绝档案。
- 改变事件顺序、目标、胜负或原语语义时递增规则主版本，旧日志只读。
- 新增不改变旧语义的可选字段可递增次版本，规范化必须写入确定缺省值。
- BattleLog 永久保存版本与双方 hash，不能只保存结果。
