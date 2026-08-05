# 《不器》Unity Demo 分步工作计划 v0.4

> 目标：在当前 GameDevelopmentKit 工程中，按可运行、可验收、可暂停的顺序完成首阶段 Demo。
>
> 玩法基线：`buqi-gameplay-spec.md` v0.4；模拟基线：`buqi-battle-contract.md` v0.4.1。

## 1. 工程决策

### 1.1 客户端模式

采用 **GameHot（纯 GF）**，不采用 ET Client：

- 单机离线影子即可验证核心玩法，不需要 ET 房间、服务端或 MongoDB。
- 现有 `Game.Hot.Code` 已覆盖 Procedure、Luban、UGF UI、UniTask 和 HybridCLR。
- 业务代码放入热更 Code，稳定 Loader 不引用《不器》业务。

模式必须通过 Unity 菜单 `Game/Define Symbol/Add UNITY_GAMEHOT` 切换。不要手工编辑宏或 `luban.conf`，因为菜单还会同步资源规则、`link.xml` 和 HybridCLR 配置。

### 1.2 当前环境基线

- 工程实际 Unity 版本：`6000.3.21f1`，以 `Unity/ProjectSettings/ProjectVersion.txt` 为准。
- 当前宏和 Luban 状态仍是 ET：`UNITY_ET` 已启用，ET `active=true`，GameHot `active=false`。
- 当前已有 GameHot 飞行 Demo。保留旧实现作为框架样例，不直接改写 `SurvivalGame`。
- 当前项目业务目录内没有自有 Unity Test asmdef，需要新建 GameHot EditMode 测试程序集。

### 1.3 代码边界

```text
Unity/Assets/Scripts/Game/Hot/Code/Buqi/
├── Battle/          # 纯 C# 确定性模拟，不引用 UnityEngine
├── Config/          # Luban 到运行时定义的适配与校验
├── Run/             # 单局状态、经济、准备选择、结算
├── Board/           # 8 格占位、仓位、构筑编辑命令
├── Echo/            # 离线道影、筛选与版本校验
├── Replay/          # BattleLog 到表现时间线的适配
├── UI/              # GameHot UIForm/Widget 业务逻辑
├── Procedure/       # 《不器》顶层阶段切换
└── Debug/           # 构筑/单战/批量模拟工具的运行时入口

Unity/Assets/Tests/GameHot/Buqi/EditMode/
└── Game.Hot.Buqi.Tests.asmdef  # 独立测试程序集，不进入热更 DLL

Share/Buqi.Simulation.Headless/
└── Buqi.Simulation.Headless.csproj  # 链接同一模拟源码的无头验证器
```

约束：

- `Battle/`、`Run/`、`Board/`、`Echo/` 的核心模型不得调用 `Time.deltaTime`、Unity Random、场景对象或 UI。
- `Generate/` 下 Luban、UGF ID 和 CodeBind 文件禁止手改。
- UI prefab 通过 Unity 编辑器和 Agent Bridge 修改，禁止直接改 YAML。
- 首个 Demo 不新增 Loader 组件；确有跨整个 GameHot 生命周期的服务时，再评审 `HotComponent`。

## 2. 总体路线

| 步骤 | 可运行里程碑 | 核心产物 | 完成后可看到什么 |
|---|---|---|---|
| 0 | GameHot 基线可运行 | 模式、工具链、干净启动证据 | Launcher 能进入现有 GameHot 菜单 |
| 1 | 规则内核测试通过 | DTO、棋盘校验、固定 tick 模拟 | 在测试窗口跑出确定性结果 |
| 2 | 九法门战斗沙盒 | 日志、调试入口、9 法门/3 淬炼 | 不做正式 UI 也能验证三方向和 S/M/L 取舍 |
| 3 | 最小配置驱动 | Luban 战斗表与 9 法门/3 淬炼/6 道影 | 改 Excel 后导表即可改变验证内容 |
| 4 | 战斗可视化 | BattleForm、卡牌 Widget、回放 | 双方 8 格棋盘连续冷却自动战斗 |
| 5 | 构筑准备 | BoardForm、仓位、拖放/选择、单局随机状态 | 能购买、上阵、换位、合并和淬炼 |
| 6 | 三轮迷你局 | Run 状态、12 法门、3 商店、3 事件、9 道影 | 从开局玩到三场结算并继续调整 |
| 7 | 完整首阶段 Demo | 18 法门、6 事件、12 道影、完整胜负循环 | 8-12 分钟完成 5 道印/3 道基局 |
| 8 | 试玩与稳定化 | 数据、修复、构建验证 | 可交给他人试玩的 Windows Demo |

原则：**一次只执行一个步骤；该步骤验收通过并记录结果后，才进入下一步。**

## 3. Step 0：工程基线与模式切换

### 目标

让 GameHot 在当前 Unity 版本和工具链下稳定启动，建立后续所有工作的可信基线。

### 操作

1. 使用 Unity Hub `6000.3.21f1` 打开 `Unity/`，等待导入完成。
2. 在仓库根执行 `dotnet build Kit.sln`。
3. Unity 菜单选择 `Game/Define Symbol/Add UNITY_GAMEHOT`，等待重新编译。
4. 执行 `Game/Define Symbol/Refresh`，确认：
   - `UNITY_GAMEHOT` 存在，`UNITY_ET` 不存在。
   - GameHot `luban.conf` 为 `active=true`，ET 为 `false`。
   - HybridCLR、资源规则和 `link.xml` 已同步。
5. 执行 `Game/Tool/ExcelExporter`。
6. 点击工具栏 `Launcher`，确认进入 GameHot 现有菜单。
7. 暂时关闭 `UNITY_HOTFIX` 或关闭 Editor CodeBytes，先用 Unity 直接编译程序集开发；Step 7 再验证热更 DLL。

### 产物

- 一份基线记录：Unity 版本、当前宏、导表结果、Console 错误数、启动路径。
- 不新增业务代码。

### 验收

- `Kit.sln` 编译成功。
- GameHot/ET 只有一个 active。
- Launcher 依次完成 `Game.Hot.Code Start`、配置加载、进入菜单。
- 连续停止/播放两次，无重复 HotComponent 或残留事件。

### 不通过时停止

只修复模式、导表、编译或启动问题，不进入 Step 1。

## 4. Step 1：确定性规则内核

### 目标

不依赖场景和 UI，完成可自动测试的最小战斗核心。

### 新建目录

```text
Game/Hot/Code/Buqi/Battle/Model
Game/Hot/Code/Buqi/Battle/Rules
Game/Hot/Code/Buqi/Battle/Simulation
Game/Hot/Code/Buqi/Battle/Logging
Game/Hot/Code/Buqi/Board
Unity/Assets/Tests/GameHot/Buqi/EditMode
Share/Buqi.Simulation.Headless
```

### 实现顺序

1. DTO：`BattleRequest`、`BuildSnapshot`、`ItemInstance`、`BattleResult`、`BattleEvent`。
2. 版本与枚举：尺寸、品质、触发、效果、目标、结束原因。
3. `BoardValidator`：8 格，S/M/L 占 1/2/3 格，越界/重叠/实例 ID 校验。
4. `SnapshotCanonicalizer`：固定排序、稳定序列化、SHA-256 hash。
5. `BattleSimulator`：0.1 秒 tick、冷却、Declare/Resolve/Chain/Aggregate/PostTick。
6. 六类效果：伤害、护体、加速、延迟、蓄力、失衡。
7. 胜负：45 秒劫火、60 秒硬上限和平局。
8. 日志：sequence、tick、phase、chainId、来源、目标、amount、reason。
9. 新建 GameHot EditMode Test asmdef，只引用 `Game.Hot.Code` 与 Unity Test Framework。
10. 新建 `Buqi.Simulation.Headless` .NET 8 控制台项目，通过 MSBuild 链接 `Buqi/Battle/**/*.cs`，不引用 Unity、UGF 或 ET，并加入 `Kit.sln`。
11. Unity 测试与无头验证器读取同一份版本化 JSON 测试向量并输出结果 hash。

### 测试清单

- 同输入 100 次 hash 一致。
- 左右镜像结果对称。
- 1/2/3 格占位、空格阻断相邻。
- 同 tick 新护体、普通伤害、直接伤害顺序。
- 加速/延迟叠加和 +/-50% 上限。
- 蓄力声明时消费一次，上限 9。
- 失衡跨 10/20 阈值。
- 连锁达到 64 事件安全截断。
- 45 秒劫火和 60 秒比较。
- 非法快照全部拒绝。
- Windows Editor 与无头 .NET 验证器对固定向量输出相同 hash。

### 验收

- 测试全部通过。
- `Battle/` 无 `UnityEngine` 引用。
- 用固定生成器构造 10,000 份不同合法构筑进行压力测试；不得通过重复同一确定性对局充数。
- 尚不创建正式 UI、场景或全部配置。

## 5. Step 2：九法门战斗沙盒

### 目标

在 Unity 内用最小占位界面运行一场战斗，先验证规则和日志，不追求正式表现。

### 内容

验证法门：

- 快速方向：加急通知（S）、冲刺看板（M）、截止日（L）。
- 护体反制：临时缓冲（S）、风险清单（S）、灾备中心（L）。
- 周天连锁：交接单（S）、联签流程（S）、流程节点（M）。

验证淬炼：加急、可靠、复写。名称会在修仙包装统一时替换，规则保持不变。

### 实现

1. 在 `Buqi/Debug` 创建 `BuqiBattleSandbox`。
2. 使用代码内的测试定义构造双方快照，仅用于 Step 2，明确标记后续由 Luban 替换。
3. 提供双方 8 格文本布局、固定 seed、运行按钮和结果区域。
4. 日志支持按 tick、chainId、来源和 reasonCode 过滤。
5. 提供“重复 100 次”按钮，显示 hash 是否一致。
6. 沙盒通过 EditorWindow 或 Runtime Debugger 入口打开，不加入正式玩家流程。

### 验收

- 三种方向各有一组真实联动可运行；护体方向必须实际完成一次“获得护体 -> 护体受损 -> 转化反击”。
- 同一沙盒能验证 S/M/L 占位，并出现一次大型核心挤压辅助法门的取舍。
- 能解释每次伤害、护体、延迟、蓄力和失衡来源。
- 连续运行、关闭、重开不残留状态。
- Step 2 结束前不增加第七种效果。

## 6. Step 3：Luban 配置化

### 目标

把九法门验证内容从测试定义迁移到 GameHot Luban，运行时不依赖硬编码法门 ID 分支。本步骤只验证配置链路，不提前录入完整首阶段内容。

### Excel 规划

在 `Design/Excel/GameHot/Datas/Buqi/` 新增：

| 表 | 用途 |
|---|---|
| `BuqiGlobal.xlsx` | 气血、护体、失衡、tick、劫火和基础规则 |
| `BuqiItem.xlsx` | 9 个验证法门、尺寸、价格、品质、冷却、标签和效果参数 |
| `BuqiRefinement.xlsx` | 3 种验证淬炼 |
| `BuqiEcho.xlsx` | 6 份教学/前期道影及完整构筑快照 |

在 `__enums__.xlsx` 声明尺寸、品质、构筑方向、效果、触发和目标枚举；在 `__beans__.xlsx` 声明效果列表、格位实例和构筑快照 bean；在 `__tables__.xlsx` 注册本步骤四张表。商店、事件和起始方案表延后到 Step 6。

### 代码

1. `BuqiDefinitionProvider` 将 Luban 行转换为不可变战斗定义。
2. `BuqiConfigValidator` 检查 ID、尺寸、触发/效果组合、目标、价格和引用。
3. 模拟核心只读取定义接口，不直接依赖 Luban 生成类。
4. Preload 后运行配置校验；开发环境遇到错误阻止进入《不器》菜单。
5. 删除 Step 2 测试定义，测试改用固定 fixture 或构造器，不依赖 Unity 资源加载。

### 导表验收

- 从根目录先构建 `Kit.sln`。
- GameHot 是唯一 active 工程。
- 运行 ExcelExporter，检查只改预期生成目录。
- `HotEntry.Tables.LoadAllAsync()` 可访问所有新表。
- 9 法门、3 淬炼、6 道影数量校验通过；完整内容数量不属于本步骤验收。
- 生成目录无手工代码。

## 7. Step 4：正式战斗界面与日志回放

### 目标

玩家能看懂双方自动战斗，暂停、变速并查看因果。

### UI 资源

```text
Unity/Assets/Res/UI/UIForm/Hot/Buqi/BattleForm.prefab
Unity/Assets/Res/UI/UIPrefab/Buqi/ItemCardWidget.prefab
Unity/Assets/Res/UI/UIPrefab/Buqi/BattleLogWidget.prefab
```

业务代码：

```text
Game/Hot/Code/Buqi/UI/BattleForm.cs
Game/Hot/Code/Buqi/UI/ItemCardWidget.cs
Game/Hot/Code/Buqi/Replay/BattleReplayController.cs
```

### 实现

1. 在 GameHot `UI.xlsx` 增加 `BattleForm`，使用生成的 `UIFormId`。
2. prefab 根逻辑继承 `StarForceUIForm`；复用 CommonButton、ProgressBar、ItemSlot、Badge。
3. 用 CodeBind 生成引用，业务只写非生成 partial。
4. `BattleReplayController` 读取完整 BattleLog，表现层不重新计算规则。
5. 显示双方 8 格、尺寸、气血、护体、失衡、蓄力和连续冷却。
6. 提供暂停、1x/2x/4x、跳过、重播和日志过滤。
7. 高亮当前来源、目标、相邻连锁和延迟。
8. 战后展示伤害前 3、有效护体前 2、关键 5 秒和事实摘要。

### 生命周期

- `OnInit`：只缓存固定引用。
- `OnOpen`：校验 BattleResult/Log，重置 UI，打开静态 Widget，绑定本次回调。
- `OnClose`：停止回放并取消异步表现；容器释放事件/资源/Widget。
- 连续打开/关闭两次，不能使用上一次战斗状态。

### 验收

- 冷却视觉连续，但暂停/变速不改变 BattleResult。
- 跳过动画后最终状态与完整播放相同。
- 1280x720、1920x1080、2560x1440 下无重叠，长卡名不截断关键数值。
- 未参与开发者看完战斗能指出主要输赢原因。

## 8. Step 5：构筑准备界面

### 目标

完成“购买 -> 上阵 -> 排列 -> 升级 -> 淬炼 -> 开战”的玩家操作闭环，并建立可保存、可复现的单局随机状态。

### 系统

- `RunState`：灵石、道印、道基、轮次、持有法门、临时效果和 `RunRandomState`。
- `RunRandomState`：保存 `RunSeed` 及准备入口、商店、事件奖励、道影选择四条命名随机流的消费游标。
- `BoardState`：8 格棋盘和 5 仓位。
- `BoardCommandService`：放置、交换、收回、出售、购买、合并、淬炼。
- 随机结果在生成时写入 `RunState`；界面预览、关闭重开和重复读取不得继续消费随机流。
- 所有修改通过命令返回成功/失败原因，UI 不直接改列表。

### UI

- `BuqiBoardForm`：棋盘、仓位、灵石、道印、道基和“确认开战”。
- `BuqiItemDetailForm`：完整法门文本、品质、尺寸、淬炼和预估冷却。
- `BuqiShopForm`：4 个商品、刷新、锁定、出售。
- 首阶段拖放可以先用“选中来源 -> 点击目标格”替代，验证闭环后再加真正拖拽。

### 验收

- S/M/L 合法放置，错误位置给出明确原因。
- 满棋盘/满仓位只能购买可立即合并的卡。
- 合并两个带淬炼法门时选择保留一个。
- 灵石变化、出售返还、刷新费用和锁定符合规格。
- 相同 `RunSeed` 和操作序列产生相同货架；只增加界面打开/关闭操作不得改变后续结果。
- 序列化并恢复 `RunState` 后，当前货架、锁定项、待选事件和四条随机流游标完全一致。
- 关闭/重开 UI 后 RunState 不丢失，Widget 回调不重复。

## 9. Step 6：三轮迷你局

### 目标

先做一个可从开局到结算的三战版本，验证玩家是否根据败因调整。

### Procedure 建议

不要为整备内部每个小面板建立 GF 顶层 Procedure。采用：

```text
ProcedureBuqi
  -> BuqiRunController
      -> StarterSelection
      -> PreparationA
      -> PreparationB
      -> BoardReview
      -> Battle
      -> Summary
      -> RoundSettlement
```

`ProcedureBuqi` 负责进入/退出《不器》、持有本局 Controller 和返回菜单；局内阶段由纯 C# `RunController` 状态枚举管理，UI 作为当前阶段的表面。

### 接入

1. 不把《不器》加入当前 `GameMode -> ProcedureMain -> GameBase` 字典；该链路带有飞机、背景和生存玩法假设。
2. 新建独立 `ProcedureBuqi`，持有 `BuqiRunController`，负责本局初始化、退出与返回菜单。
3. 修改 `ProcedureChangeScene` 的目的流程选择：菜单场景进入 `ProcedureMenu`，《不器》场景进入 `ProcedureBuqi`，旧主场景仍进入 `ProcedureMain`。选择依据使用显式 FSM 数据或 SceneId，不能继续用“非菜单一律 Main”。
4. MenuForm 增加“不器 Demo”入口，将 NextSceneId 指向新场景并写入目标 Procedure 标识。
5. 新建最小 `Buqi.unity`，只保留相机、EventSystem、简单背景和必要挂点。
6. 在 Scene.xlsx 注册新 SceneId，通过 `ProcedureChangeScene` 加载。
7. 在 Step 3 配置基础上增加起始方案、商店和事件表；扩至 12 个法门、全部 6 种淬炼、3 类商店、3 个事件和 9 份道影。
8. 完成三次有胜负的战斗后显示迷你局结果并返回菜单。

### 验收

- 从 Launcher -> Menu -> Buqi -> 三轮结算 -> Menu 完整闭环。
- 每轮两次准备选择。
- 战败后下一轮资金补偿正确。
- 关闭/重进不保留上局静态状态或事件订阅。
- 5 名测试者中，70% 战败后会针对主因调整一次构筑。

## 10. Step 7：完整首阶段 Demo

### 目标

扩为批准范围内的完整 8-12 分钟单局。

### 补齐

1. 全部 18 个法门和 6 种淬炼。
2. 全部 3 类商店、6 个事件、12 份道影。
3. 5 枚道印胜利、3 点道基失败、最多 7 场有胜负战斗。
4. 道影筛选：道印 -> 占位差 -> 构筑投入 -> 确定性选择。
5. 战前预览：方向、三个关键法门、主要威胁、占用和淬炼。
6. 平局重赛、连续平局补偿、道影版本拒绝和兜底。
7. 本地存档：只保存当前单局和设置；先不把玩家构筑上传为真实影子。
8. 开启 `UNITY_HOTFIX`，编译 GameHot DLL，验证 Editor CodeBytes 和 Player 加载。
9. Windows Player 运行与 Editor、无头 .NET 相同的固定测试向量并导出 hash，三端逐项一致。

### 验收

- 所有功能与玩法规格一致。
- 中位单局 8-12 分钟；任何战斗不超过 60 秒。
- 三种方向代表样本胜率 40%-60%。
- 玩法不依赖网络断连状态。
- 热更开/关两种 Editor 路径均能完成一局。

## 11. Step 8：试玩、修复与 Windows 构建

### 目标

把工程内可玩版本变成可交付试玩包。

### 测试

1. 至少 5 名玩家，每人 3 局。
2. 记录首玩完成率、败因识别、下一轮调整、战斗时长、单局时长。
3. 从 12 份基础道影按固定规则生成布局、品质和淬炼变体；按相同投入档执行完整交叉对战并交换左右阵营，每个唯一向量只计一次。
4. 按投入档和方向等权汇总胜率；确定性重复运行只用于 hash 验证，不计入平衡样本量。
5. 另用固定生成器创建 10,000 份不同合法构筑，检查异常、循环上限和性能，不混入胜率。
6. 检查采用率 >75% 的法门和贡献提升 >50% 且代价未发生的淬炼。
7. 连续完成 10 局，检查内存、事件、UI 池、资源和静态状态不增长。
8. 关闭回放 UI、切场景、退出本局等竞态路径重复验证。

### 构建

1. 执行 HybridCLR `Do All`。
2. 使用 `Game/Build Tool Editor` 构建 Windows 试玩包。
3. 在无 Unity 编辑器环境运行：启动、完整一局、返回菜单、退出。
4. 保存规则版本、内容版本、构建号和批准的确定性测试向量 hash。

### Demo 完成门槛

- 80% 战斗后玩家能指出日志支持的主要原因。
- 70% 战败后下一轮主动调整。
- 80% 战斗在 30-45 秒形成主要结论。
- 三种构筑没有一种整体胜率超过 60%。
- Windows 包无阻塞错误，可离线完成完整单局。

## 12. 每一步统一完成清单

每一步结束都执行：

1. 编译当前 GameHot 模式，无新增 Console Error。
2. 运行本步自动测试；保存失败用例和修复结果。
3. 从 Launcher 走一遍受影响的真实路径，不只调用孤立场景。
4. 连续执行两次进入/退出，检查事件、异步、静态状态和 UI 池。
5. 若改 Excel，重新导表并确认生成目录稳定。
6. 若改 prefab，使用 Unity 编辑器/Agent Bridge 和 CodeBind，不改 YAML；连续生成检查稳定。
7. 记录：完成内容、修改文件、测试证据、未完成项、下一步入口。
8. 不把未实际运行的 Unity 验收写成“已通过”。

## 13. 当前立即执行项

现在只开始 **Step 0：工程基线与模式切换**。

Step 0 验收通过后，下一条指令应是：

> 开始 Step 1，建立《不器》确定性规则内核、EditMode 测试和无头 .NET 验证器。

在 Step 1 通过前，不创建正式战斗 prefab，不录入完整 18 张卡，也不改现有 `SurvivalGame`。
