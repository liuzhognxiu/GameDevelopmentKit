# 《不器》完整 Demo UI 系统设计

- 日期：2026-08-06
- 状态：用户已授权自主决策，可进入实施计划
- 目标分辨率：1920x1080 横屏
- 战斗子规格：`2026-08-06-buqi-battle-replay-ui-demo-design.md`
- 统一术语：`docs/game-concepts/gameplay-terminology.md`

## 1. 目标

在不等待 Step 5/6 全部玩法系统完成的前提下，制作一套真实 Unity prefab、可交互、可逐页验收的完整 Demo UI。界面必须覆盖首阶段玩家流程，但只有战斗回放连接真实模拟结果；购买、事件、改造、棋盘编辑、预测和结算先连接确定性的 `BuqiUIDemoState`，不得声称已经完成正式经济、保存、随机流或命令契约。

本设计解决两个问题：

1. 用户可以在 Unity 中查看并操作全部主要界面，不需要等待完整玩法闭环。
2. 后续正式系统只替换 ViewModel 与命令适配层，不推翻 prefab、信息层级和输入布局。

## 2. 入口与模式

现有 GameHot 主菜单增加两个清晰入口：

- **开始战斗**：按已批准战斗子规格，直接打开真实 `BattleForm`。
- **界面预览**：打开 `BuqiRunShellForm` 的 Demo Gallery 模式，从起始方案开始，可顺序浏览全部阶段。

设置、关于和退出保留现有行为。暂不接入 `ProcedureBuqi`、新场景或正式 RunState；这些属于 Step 5/6 玩法接入，不属于本轮 UI 制作。

Editor 额外提供 `Game/Buqi/Open Full UI Demo` 与 `Game/Buqi/Rebuild Full UI Demo`，用于自动化验收和 prefab 重建。玩家路径不依赖 Editor 菜单。

## 3. 顶层窗体

| UIForm | UIGroup | 职责 |
|---|---|---|
| `BattleForm` | Default | 真实 BattleResult/BattleLog 回放 |
| `BuqiRunShellForm` | Default | Demo 阶段导航、状态栏与当前主 Widget |
| `BuqiItemDetailForm` | Pop | 装备、改造和完整效果详情 |
| `BuqiConfirmForm` | Pop | 出售、刷新、跳过预测和离开预览确认 |
| `BuqiMessageForm` | Message | 非阻塞成功、失败和状态提示 |

`SettingForm`、`AboutForm` 与现有 `DialogForm` 继续复用，不复制新版本。

## 4. Run Shell 布局

`BuqiRunShellForm` 延续战斗界面的视觉语法：深色机关台、清晰类型色、固定尺寸控制，不使用渐变和精美插画。

1920x1080 稳定区域：

| 区域 | 尺寸 | 内容 |
|---|---:|---|
| 外边距 | 32 px | 四边安全留白 |
| 顶部状态栏 | 1856x72 | 金币、胜场、单局生命、轮次、当前阶段 |
| 垂直间距 | 1856x16 | 状态栏、主体和命令栏之间各一处 |
| 左侧阶段轨 | 208x824 | 已到达阶段和当前阶段，不允许任意跳过 |
| 中央工作区 | 1112x824 | 当前阶段 Widget |
| 右侧上下文栏 | 488x824 | 详情、对手公开情报、合法性或事实证据 |
| 区域间距 | 24 px | 阶段轨、工作区和上下文分隔 |
| 底部命令栏 | 1856x88 | 返回、次要命令、主要确认、错误原因 |

同一时刻只显示一个主阶段 Widget。Pop 窗体位于 Run Shell 之上，不把详情面板嵌套成卡片套卡片。

## 5. 阶段 Widget

### 5.1 起始方案 `StarterSelectionWidget`

- 三个方案横向排列，每个显示方向、3 件核心装备、空间占用和节奏。
- 不显示战力、胜率或“推荐”。
- 选择后右栏显示完整关系；底部“确认方案”进入对手情报。

### 5.2 对手快照 `OpponentIntelWidget`

- 显示对手方向、连续 8 格、3 件关键装备、主要威胁和一个已知风险。
- 隐藏内部评分、随机种子和未公开改造。
- “继续准备”进入准备选择。

### 5.3 准备选择 `PreparationChoiceWidget`

- 同一版式承载商店、事件或改造服务入口。
- 三张选择卡只显示可见成本、收益类别和代价。
- Demo Gallery 使用固定候选；打开、关闭和返回不会改变候选。

### 5.4 商店 `ShopWidget`

- 4 个商品、金币、刷新费用、锁定状态和出售入口。
- 商品卡显示装备名称、尺寸、主要效果、价格和是否可购买。
- 点击商品只更新 DemoState；金币不足、棋盘/仓库满和可合并使用不同失败原因。

### 5.5 事件 `EventWidget`

- 标题、两段以内事件文本、2-3 个互斥选项和可见代价。
- 不使用装饰性大段叙事遮住决策结果。
- 选择后立即显示 Demo 结果摘要，再进入下一阶段。

### 5.6 改造 `ModificationWidget`

- 左侧选择一件装备，中央显示 2-3 个改造，右侧对比改造前后。
- 使用统一名：高速、强效、复制、抗减速、稳定、超载。
- 收益与代价同时显示；不隐藏冷却、护盾、伤害或过载变化。

### 5.7 棋盘编辑 `BoardEditorWidget`

- 中央保留连续 8 格棋盘，下方 5 格仓库。
- 本轮使用“选中来源 -> 点击目标格 -> 确认”而非拖拽。
- 合法位置使用绿色范围，非法位置使用红色范围并显示具体原因。
- 支持放置、交换、收回、出售、查看详情和确认构筑的 Demo 操作。

### 5.8 战前预测 `PredictionWidget`

- 固定三项：对象、窗口、预期。
- 允许跳过，但必须经过确认。
- 提交后在 DemoState 中锁定，返回页面仍不可修改。

### 5.9 战斗回放

使用独立 `BattleForm`，完全遵守战斗子规格。Run Shell 在 Demo Gallery 中显示入口页和“打开战斗回放”命令，不复制第二套回放布局。

### 5.10 战后总结 `BattleSummaryWidget`

- Layer 1：胜负、结束 tick、双方剩余生命值/护盾、预测符合程度。
- Layer 2：最大有效贡献、关键连锁/中断、最大风险账单。
- 每条事实可打开 `BattleForm` 并跳到对应 tick，或打开日志页。
- 主因由玩家选择；界面不自动推荐下一件装备。

### 5.11 轮次结算 `RoundSettlementWidget`

- 显示胜场、单局生命、金币变化、战斗结果和下一轮状态。
- Demo 只演示视觉状态，不执行正式奖励账本。
- “下一轮”进入新的对手快照；“查看复盘”返回总结。

### 5.12 单局终局 `RunTerminalWidget`

- 胜利与失败共用同一结构，仅替换结果、统计和主命令。
- 显示总战斗数、胜负、最常用构筑方向、一次关键调整和总时长。
- 提供返回主菜单与重新预览，不提供联网、排行或永久成长。

## 6. 领域预制体

```text
Unity/Assets/Res/UI/UIPrefab/Buqi/
├── ItemCardWidget.prefab
├── BattleLogWidget.prefab
├── BoardSlotWidget.prefab
├── ResourceChipWidget.prefab
├── PhaseStepWidget.prefab
├── ChoiceCardWidget.prefab
├── OfferCardWidget.prefab
├── OpponentSnapshotWidget.prefab
└── FactRowWidget.prefab
```

阶段预制体：

```text
Unity/Assets/Res/UI/UIPrefab/Buqi/Stages/
├── StarterSelectionWidget.prefab
├── OpponentIntelWidget.prefab
├── PreparationChoiceWidget.prefab
├── ShopWidget.prefab
├── EventWidget.prefab
├── ModificationWidget.prefab
├── BoardEditorWidget.prefab
├── PredictionWidget.prefab
├── BattleSummaryWidget.prefab
├── RoundSettlementWidget.prefab
└── RunTerminalWidget.prefab
```

所有 prefab 由 `BuqiFullUIBuilder` 创建或重建，不手写 YAML。公共按钮、进度条、ItemSlot、Badge、Toggle 与 Loading 继续复用现有组件库。

## 7. Demo 数据与命令边界

```text
BuqiUIDemoCatalog
-> BuqiUIDemoState
-> BuqiUIDemoController
-> immutable phase ViewModel
-> RunShell / Stage Widget
-> BuqiUIDemoCommand
-> validation + new DemoState
```

- `BuqiUIDemoCatalog` 从当前 Luban 配置读取名称、尺寸、效果、改造和对手快照，并提供固定商店/事件样本。
- `BuqiUIDemoState` 只存在于界面预览会话，关闭 Run Shell 后丢弃。
- `BuqiUIDemoController` 是纯 C#，用于演示合法/非法状态、阶段转换和页面回开，不写正式 RunState。
- UI 不直接修改 DemoState；每次操作提交 `BuqiUIDemoCommand` 并渲染结果。
- 同一初始状态与命令序列必须得到相同 ViewModel。
- Demo Controller 不消费 Unity RNG、战斗 RNG 或正式命名随机流。

## 8. 统一视觉与交互

- 延续 Battle UI 色板；装备类型使用颜色 + 中文效果标记双通道。
- 所有圆角不超过 8 px；页面区域不做漂浮卡片。
- 工具命令使用图标按钮；明确提交命令使用图标 + 文本。
- 主按钮固定在底部右侧，不因错误文本改变位置。
- Hover 信息都有点击路径；右侧详情栏承担触摸/焦点替代。
- 长名称最多两行，关键数值不省略；reasonCode 只在高级详情或日志显示。
- 空态、加载、失败、锁定、选中、非法和已提交均有独立状态。
- 本轮不制作新位图插画；只使用现有公共 Sprite、TMP 文本、轮廓和纯色块。

## 9. 生命周期

- `BuqiRunShellForm.OnOpen` 创建新的 Demo Controller、绑定静态阶段 Widget 并进入起始方案。
- 阶段 Widget 每次展示都接收完整不可变 ViewModel，不依赖上次残留。
- `BuqiItemDetailForm`、`BuqiConfirmForm` 和 `BuqiMessageForm` 每次 OnOpen 重置标题、内容与回调。
- `OnClose` 解除全部按钮、Toggle 和异步回调；关闭 Run Shell 会清空 DemoState。
- 连续打开/关闭两次，第二次从相同初始状态开始，不能保留选择、金币、预测或阶段。

## 10. UI Gallery 验收路径

Demo Gallery 左侧阶段轨只显示已访问阶段，底部提供“上一步”和当前合法主命令。Editor 验收菜单可直接指定阶段，但玩家入口不能越过阶段状态机。

自动截图集合：

```text
01-main-menu
02-starter-selection
03-opponent-intel
04-preparation-choice
05-shop
06-event
07-modification
08-board-editor
09-prediction
10-battle-replay
11-battle-summary
12-round-settlement
13-run-terminal
14-item-detail
15-confirm
16-error-and-loading
```

每张截图均使用 1920x1080 Game View，检查非空、无重叠、关键文本完整、焦点清晰和同一色义一致。

## 11. 实施顺序

1. 战斗回放基础与 BattleForm。
2. DemoState、Run Shell、状态栏、阶段轨和详情/确认/消息窗体。
3. 起始方案、对手情报、准备选择、商店、事件和改造。
4. 棋盘、预测、总结、结算和终局。
5. 主菜单双入口、UI Gallery、全部 prefab、CodeBind 与 UI.xlsx。
6. Unity 编译、EditMode、真实交互、16 张截图和独立 Review。

## 12. 完成条件

- 上述 5 个 UIForm、11 个阶段 Widget 和 9 个领域 Widget 均有真实脚本与 prefab。
- 主菜单“开始战斗”和“界面预览”都可用。
- Gallery 可从起始方案顺序走到终局，并覆盖合法、非法、加载和错误状态。
- BattleForm 使用真实日志；其他阶段明确使用确定性 DemoState。
- 全部玩家可见文案遵守通用词汇表。
- 1920x1080 的 16 张验收截图通过检查。
- 新增纯 C# 与 prefab EditMode 测试通过，Unity Console error 为 0。
- 不覆盖并发会话的玩法文档、Excel 临时文件或未提交改动。
