# 《不器》战斗回放 UI Demo 设计

- 日期：2026-08-06
- 状态：已批准，作为完整 Demo UI 系统的战斗子规格进入实施计划
- 目标分辨率：1920x1080 横屏
- 替代文档：`2026-08-05-buqi-ui-interaction-design.md` 的正式 UI 与 Run Shell 部分

## 1. 决策摘要

本子规格只约束可从现有 GameHot 主菜单进入的战斗回放 Demo，不建设完整 Run Shell。Run Shell 与其他阶段界面由 `2026-08-06-buqi-full-demo-ui-system-design.md` 统一约束。

- 现有“开始游戏”按钮改为直接打开 `BattleForm`，暂时绕过 `ProcedureMain/SurvivalGame`。
- 采用“上下双轨战场 + 右侧固定证据栏 + 底部回放控制”的 1920x1080 布局。
- 双方连续 8 格必须同时可见；战斗中无玩家战斗指令。
- 回放表现只消费已经生成的 `BattleResult` 与完整 `BattleLog`，不重新执行战斗规则。
- Demo 卡面不制作精细插画，只用名称、尺寸、效果符号、类型色和状态轮廓区分内容。
- UI 资源通过 Editor Builder 和 CodeBind 生成，不手写 prefab YAML。
- 本轮不做移动端、商店、构筑编辑、预测录入、粒子、音频、首玩引导或完整经济循环。

## 2. 玩家流程

```text
Launcher
-> GameHot 主菜单
-> 点击“开始战斗”
-> 生成固定演示 BattleResult + BattleLog
-> 打开 BattleForm 并自动播放
-> 暂停 / 1x / 2x / 4x / 跳过 / 重播 / 查看日志
-> 查看三条战后事实
-> 返回主菜单
```

关闭 `BattleForm` 后回到仍被覆盖的主菜单。Demo 不写入单局存档、不结算奖励、不改变 RNG 游标。

## 3. 1920x1080 布局

Canvas 使用 `CanvasScaler.ScaleWithScreenSize`，参考分辨率 `1920x1080`，`Match Width Or Height = 0.5`。本轮只对该分辨率声明视觉验收。

### 3.1 稳定区域

| 区域 | 尺寸 | 内容 |
|---|---:|---|
| 外边距 | 32 px | 四边固定安全留白 |
| 顶部状态栏 | 1856x72 | 场次、双方名称、当前 tick、规则/内容版本 |
| 主战场 | 1376x824 | 上方对手快照轨、中央关键事件、下方玩家轨 |
| 主区间距 | 24 px | 战场与证据栏分隔 |
| 证据栏 | 456x824 | 当前事件、关键战报、全部日志与筛选 |
| 底部控制栏 | 1856x88 | 返回、播放控制、时间线、跳过、重播 |

垂直排列为：顶部状态栏 72 px、间距 16 px、主体 824 px、间距 16 px、底部控制栏 88 px。

### 3.2 双方 8 格

每一方使用相同结构：

- 左侧 216 px 显示名称、生命值、护盾、过载和当前关键状态。
- 右侧为 8 格轨道；单格宽 134 px，格间距 8 px。
- 轨道底层始终显示 8 个固定槽位。
- 装备只在 `AnchorSlot` 创建一个 `ItemCardWidget`；宽度为 `Size * 134 + (Size - 1) * 8`。
- 被多格装备覆盖的槽位显示统一占用底纹，不重复名称或数值。
- 空格明确显示“空位”，视为构筑取舍，不显示错误态。

上方为对手快照，下方为玩家。双方顺序不会因胜负、播放速度或重播改变。

### 3.3 中央关键事件

中央区域只显示当前最高优先级事件：

1. 致胜或致败。
2. 首次核心发动、未发动或无合法目标。
3. 连锁中断、免疫、截断。
4. 过载临界与过载伤害。
5. 普通效果与重复触发。

显示内容为 `tick + 来源 -> 目标 + 效果 + 数值`。连锁最多同时标出三层，使用 `1/2/3` 编号；更深层显示 `+N 次响应`。

## 4. 视觉规则

界面使用深色机关台与高对比信息色，不追求精美卡图，也不使用渐变背景。

| 语义 | 色值 | 同时使用的非颜色标记 |
|---|---|---|
| 背景 | `#111416` | 无 |
| 抬升面板 | `#202629` | 1 px 边框 |
| 主文本 | `#EDF0ED` | 正文 |
| 次文本 | `#AEB8B3` | 较小字号 |
| 伤害/灼烧 | `#C65F55` | “伤”标记 |
| 护盾/冻结 | `#5D94AD` | “护”或“冻”标记 |
| 治疗/恢复 | `#5B9C73` | “愈”标记 |
| 充能 | `#C19B52` | 层数数字 |
| 延迟/毒 | `#7A668C` | “迟”或“毒”标记 |
| 连锁 | `#5F9A78` | 链深编号 |
| 当前来源 | `#E0B75E` | 顶边框 |
| 当前目标 | `#EDF0ED` | 完整外框 |

卡面第一层固定显示：名称、尺寸、主要效果、冷却进度和充能层数。定义 ID、effect ID、reasonCode 和完整数值进入右侧日志，不挤进卡面。

现有 `Assets/Res/UI/UISprite/Common/` 与公共组件库提供面板、按钮、进度条和选择轮廓。本轮不新增位图插画。

## 5. 组件与所有权

### 5.1 运行时代码

```text
Unity/Assets/Scripts/Game/Hot/Code/Buqi/Demo/BuqiBattleDemoFactory.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/Replay/BattleReplayData.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/Replay/BattleReplayFrame.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/Replay/BattleReplayFacts.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/Replay/BattleReplayController.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleForm.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/ItemCardWidget.cs
Unity/Assets/Scripts/Game/Hot/Code/Buqi/UI/BattleLogWidget.cs
```

职责：

- `BuqiBattleDemoFactory`：从当前 Luban 配置中选择固定双方构筑，运行一次模拟器并生成完整回放输入。它是 Demo 数据入口，不属于表现层。
- `BattleReplayData`：持有场景标题、双方 BuildSnapshot、显示元数据、`BattleResult`、完整 `BattleLog` 与定义查询接口。
- `BattleReplayFrame`：当前 tick 下不可变的双方资源、卡面状态、关键事件和终局状态。
- `BattleReplayFacts`：从日志聚合最大有效贡献、关键连锁/中断和最大风险账单，不产生策略建议。
- `BattleReplayController`：纯 C# 状态机，拥有播放位置、速度、暂停、跳过、重播、筛选和日志分页；不得引用 Unity UI 或调用 `BuqiBattleSimulator.Simulate`。
- `BattleForm`：`StarForceUIForm` 子类，是控制器、静态卡位、日志条目和 UI 回调的唯一 Owner。
- `ItemCardWidget`：无业务决策，只渲染一件装备或空位的当前状态。
- `BattleLogWidget`：无业务决策，只渲染一条格式化日志或战后事实。

### 5.2 Editor 代码

```text
Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleUIBuilder.cs
```

菜单 `Game/Buqi/Rebuild Battle UI Demo` 负责创建或重建以下资源：

```text
Unity/Assets/Res/UI/UIForm/Hot/Buqi/BattleForm.prefab
Unity/Assets/Res/UI/UIPrefab/Buqi/ItemCardWidget.prefab
Unity/Assets/Res/UI/UIPrefab/Buqi/BattleLogWidget.prefab
```

Builder 只创建层级、布局、公共组件实例和初始序列化值。CodeBind 生成引用 partial；业务逻辑只写在非生成 partial 中。

## 6. 数据流与回放语义

```text
HotEntry.Tables.BuqiConfig + HotEntry.Tables.BuqiItemDefinitions
-> BuqiBattleDemoFactory
-> BuqiBattleSimulator.Simulate（仅一次，打开 UI 前）
-> BattleReplayData(Result + Log + Builds + Definitions)
-> BattleReplayController
-> BattleReplayFrame / BattleReplayFacts / paged logs
-> BattleForm / ItemCardWidget / BattleLogWidget
```

### 6.1 时间

- 战斗规则为每 tick 0.1 秒。
- `Advance(realSeconds)` 只计算表现目标 tick，不执行规则。
- 速度只允许 `1x`、`2x`、`4x`。
- 暂停时 `Advance` 不改变 tick。
- 跳过会从初始状态顺序投影全部日志，而不是直接写一份手工终态。
- 重播会清空投影状态并从同一 `BattleReplayData` 的 tick 0 开始。

### 6.2 连续冷却

控制器预索引每个实例的 `Declare` 事件。卡面冷却进度在相邻两次真实声明 tick 之间线性插值；首次声明以前从 tick 0 插值，最后一次声明以后保持结束态。该进度只表达“距下一次真实发动的表现进度”，不重新计算内部冷却规则。

### 6.3 资源投影

控制器从 BuildSnapshot 初始资源开始，按 `Sequence` 顺序应用 `BattleEvent`。效果类型通过当前定义表和 `EffectId` 查询，不从 reasonCode 猜测规则。

播放到结尾时，投影得到的生命值、护盾和过载必须与 `BattleResult` 一致；不一致时进入数据错误状态，不用 `BattleResult` 覆盖错误投影。

未知但不影响资源的日志仍可作为原始日志展示。无法识别的资源变化、hash 不一致、缺失实例或乱序事件会终止播放并显示数据错误。

## 7. 右侧证据栏

证据栏有两个页签：

1. **关键事件**：当前关键事件、最近事件和战后固定三条事实。
2. **全部日志**：按每页 12 条显示完整筛选结果。

筛选条件：关键事件、来源、目标、chainId、reasonCode。筛选只改变日志结果，不改变播放位置、当前帧或事实摘要。

战后固定三条事实：

- 最大有效贡献：伤害、护盾或治疗中绝对有效量最高的一项。
- 关键连锁或中断：优先取致胜链、截断、无目标或免疫；没有时取最深链。
- 最大风险账单：过载伤害、持续伤害或空转中总损失最高的一项。

每条事实保留来源 event IDs，点击后跳到对应 tick 并选中日志页；事实不输出购买、改造、摆位或强弱建议。

## 8. 控件与生命周期

底部控制栏使用固定尺寸控件：

- 返回。
- 播放/暂停图标按钮。
- `1x / 2x / 4x` 分段速度控件。
- 只读时间线与当前 tick。
- 跳过。
- 重播。

`BattleForm` 生命周期：

- `OnInit`：缓存固定引用，不读取战斗数据。
- `OnOpen`：校验 `BattleReplayData`、创建新控制器、重置所有 Widget、绑定本次回调并自动播放。
- `OnUpdate`：把 unscaled delta time 交给控制器并渲染发生变化的帧。
- `OnClose`：停止控制器、解除回调、清空日志页和卡面状态。
- 连续打开两次必须创建两个不同控制器实例，第二次不得读取第一次的播放位置、速度或筛选。

## 9. 错误状态

以下情况在战场中央显示“回放数据不可用”，右侧列出可诊断原因，底部只保留返回和重试：

- `BattleReplayData` 为空。
- BattleLog hash 与 BattleResult 不一致。
- 规则、模拟或内容版本缺失。
- BuildSnapshot 中实例重叠、越界或定义缺失。
- 日志 Sequence 乱序或引用不存在的实例。
- 日志资源投影与 BattleResult 终态不一致。

重试重新使用同一输入，不重新模拟。固定 Demo 数据创建失败时，主菜单记录结构化错误并保持可操作，不打开空白 UI。

## 10. UI 配置与主菜单

在 `Design/Excel/GameHot/Datas/Game/UI.xlsx` 增加 `BattleForm`：

- `AssetName = Hot/Buqi/BattleForm`
- `UIGroupName = Default`
- 单实例
- 覆盖时暂停被覆盖 UI

运行 GameHot ExcelExporter，使用生成的 `UIFormId.BattleForm`，禁止手改生成文件。

`MenuForm.OnStartButtonClick` 改为：创建固定演示数据并打开 `BattleForm`。现有 `ProcedureMenu.StartGame()` 保留但不再由该按钮调用，方便后续恢复原流程。

主菜单按钮显示文本改为“开始战斗”，明确当前 Demo 行为。

## 11. 测试策略

### 11.1 纯 C# EditMode

- 初始帧来自 BuildSnapshot，不提前应用日志。
- 暂停后多次 Advance 不推进。
- `1x/2x/4x` 对相同 realSeconds 产生正确表现 tick，且不改 BattleResult/hash。
- 同 tick 事件按 Sequence 应用。
- 冷却进度只在真实 Declare tick 间插值。
- 跳过、完整播放和重播得到相同终态与三条事实。
- 筛选和翻页不改变帧状态。
- 未知资源事件、乱序、缺实例和 hash 漂移进入数据错误。
- Controller 源码/依赖图不存在到 `BuqiBattleSimulator.Simulate` 的调用边。

### 11.2 Factory

- 固定配置产生合法双方 8 格构筑。
- 连续创建两次的 Result、Log hash 和显示元数据一致。
- 缺少固定配置 ID 时返回明确失败，不回退到随机构筑。

### 11.3 Prefab 与生命周期

- 三个 prefab 均存在且根组件正确。
- `BattleForm` 包含双方 8 格槽位、16 个卡面实例位、证据栏、12 个日志实例位和全部控制。
- CodeBind 字段均非空。
- 连续打开/关闭两次没有回调、播放状态或日志残留。

### 11.4 真实 Unity 验证

- Unity 重编译 0 error。
- `Game.Hot.Buqi.Tests` 全量 EditMode 通过。
- 从 Launcher 进入主菜单，点击“开始战斗”打开真实 `BattleForm`。
- 1920x1080 Game View 截图中无空白、遮挡、越界或关键文本截断。
- 暂停、三档速度、跳过、重播、日志页和返回均实际可操作。
- 完整播放与跳过的最终 UI 数值、结果文本和 hash 一致。
- Console error 为 0。

## 12. 验收边界

本轮完成条件：

1. 主菜单“开始战斗”能稳定打开正式 GameHot `BattleForm`。
2. 双方 8 格、卡面尺寸、生命值、护盾、过载、充能和连续冷却均可见。
3. 暂停、1x/2x/4x、跳过、重播和日志筛选可用。
4. 当前来源、目标、连锁和延迟可辨认。
5. 战后显示三条有 event IDs 的事实，且不包含策略建议。
6. 回放控制器不调用模拟器；速度与跳过不改变 Result/hash。
7. 1920x1080 下布局通过真实截图检查。
8. 所有新增测试、Unity 编译和 Console 检查通过。

精美卡图、移动端和完整 Run Shell 不属于本战斗子规格的完成条件。
