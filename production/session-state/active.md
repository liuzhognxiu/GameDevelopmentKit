# 当前项目状态：《不器》

## 当前任务

首阶段核心范围、玩法规格 v0.4、战斗契约 v0.4.1、原型计划 v0.4 和 Unity Demo 分步计划 v0.4 已完成结构对齐。**Step 0、Step 1、Step 2 均已完成验收。** 当前正在完成进入 Step 3 前的 Charge 0.4.1 契约门禁；尚未进入 Luban Schema。

## Step 0 验收结论（2026-08-04）

- 工具链：新增 `global.json` 锁 .NET 8.0.423；`DotNet.ThirdParty.csproj` 排除 `Forwarders.cs` 并纳入 ReactiveBinding 源码；Kit.sln 用独立 `obj.wb` 目录 + `NuGetAudit=false` 绕过环境锁，可编译。
- 模式切换：Unity Define Symbols 由 `UNITY_ET` 切换为 `UNITY_GAMEHOT`；`Design/Excel/GameHot/luban.conf` active=true、`ET` false；`link.xml` 注释 ET 段、`HybridCLRSettings` 唯一热更程序集指向 Game.Hot.Code。
- Luban 导表：通过 AgentBridge `invoke_menu "Game/Tool/ExcelExporter"` 成功，`Generate/Luban` 与 `Res/Hot/Luban` 已落地；Unity 重新编译 0 错误 0 警告。
- 启动基线：通过 AgentBridge `play_scene` 连续两轮进入/退出 Play Mode，GameHot 正常启动（日志见 `开始 GameHot！`/`Game.Hot.Code Start!`），**两轮均无重复 HotComponent/静态状态错误，退出无 OnDestroy 异常**。
- 场景缺口已修复：`Assets/Res/Scene/Menu.unity`（连同 `Assets/Launcher.unity`）已加入 `ProjectSettings/EditorBuildSettings.asset` 的 `m_Scenes`。复测 `play_scene` 进入后 `Load scene '...Menu.unity' OK`，`search_logs type=error` 为 0，GameHot 进入菜单无错误。Step 0 验收完全通过。

其他 Agent 生成的工作计划已经完成首轮审阅，其中前五项问题已于 2026-08-04 修正：

1. 平衡测试改为不同构筑样本的交叉对战，不再重复相同确定性对局制造样本量。
2. 内容顺序改为 9 法门战斗切片、12 法门迷你局、18 法门完整局。
3. 增加链接同一模拟源码的无头 .NET 验证器，不新增运行时或 HybridCLR 程序集。
4. 增加版本化 `RunRandomState` 和四条隔离的单局随机流。
5. 六卡验证集改为九法门验证集，覆盖三种真实联动和 S/M/L 空间取舍。

审阅中的第 6-9 项尚未修正：事件/道影配置原语、菜单与 Procedure 职责、EditorWindow/测试 asmdef 约束、当前终端的 `dotnet` PATH。

## 已锁定核心

《不器》是一款异步对战自动构筑肉鸽。

通俗核心：

> 玩家在战斗中不手动出牌。
> 玩家在战前购买、升级、改造和排列组件，组成一套自动运行的卡牌机器。
> 战斗开始后，双方组件按照冷却和触发关系自行运转。
> 玩家看懂胜负原因，再在下一轮调整构筑。

最短介绍：

> 组装一套自动卡牌机器，再让它与另一名玩家保存下来的机器交战。

## 世界与主线

- 游戏暂定名：《不器》。
- 核心舞台：不断迁徙并暗中修炼自身的不器城。
- 玩家身份：无门无派、善于把残缺法门拼成周天的年轻散修。
- 日常舞台：众妙集；单局竞赛：百炼问道；异步对手：道影。
- 主线命题：一条尚未完成的道路，有没有资格被称为大道？
- 不器城会模仿获胜构筑，却必须学习取舍，最终以城池身份渡劫。
- 天衡院是立场合理的对手，代表稳定、纯粹和可控制的道路，不是邪恶宗门。
- 故事采用轻快热闹的修仙游历气质，不再直接映射现代职场。

主线基线见 `docs/game-concepts/buqi-main-story.md`。

## 首阶段范围

- 战斗表现：连续实时冷却；战斗中无玩家输入。
- 模拟要求：固定步长、确定性、可回放。
- 棋盘：8 格；小/中/大型法门占 1/2/3 格。
- 单局：8-12 分钟；获得 5 枚道印胜利，道基累计损失 3 点失败。
- 内容：18 个法门、6 种淬炼、3 个构筑方向、3 类商店、6 个事件、12 份离线道影。
- 构筑方向：快速执行、护体反制、周天连锁。
- 战斗目标：30-45 秒；45 秒进入劫火，60 秒硬上限。
- 基础效果：伤害、护体、加速、延迟、蓄力、失衡。
- 不做：在线对战、多角色、独立技能、排行、赛季、重美术和完整剧情演出。

## 渐进内容门槛

| 阶段 | 法门 | 淬炼 | 商店 | 事件 | 道影 |
|---|---:|---:|---:|---:|---:|
| 战斗沙盒 | 9 | 3 | 0 | 0 | 代码内测试快照 |
| 最小配置链路 | 9 | 3 | 0 | 0 | 6 |
| 三轮迷你局 | 12 | 6 | 3 | 3 | 9 |
| 完整首阶段 | 18 | 6 | 3 | 6 | 12 |

## 当前文档

- `docs/game-concepts/buqi-core.md`：通俗核心和统一术语。
- `docs/game-concepts/buqi-main-story.md`：已暂定的主线故事基线。
- `docs/game-concepts/buqi-story-outline.md`：剧情大纲 v0.2，包含序章、五章连续因果、主要角色弧、关键分支、三种结局、三层 Lore 与首阶段叙事切片。
- `docs/game-concepts/buqi-gameplay-spec.md`：玩法规格 v0.4。
- `docs/game-concepts/buqi-battle-contract.md`：确定性战斗契约 v0.4.1。
- `docs/game-concepts/buqi-prototype-plan.md`：P0-P3 原型计划 v0.4。
- `docs/game-concepts/buqi-unity-demo-work-plan.md`：GameHot Step 0-8 工作计划 v0.4。
- `docs/superpowers/specs/2026-08-04-buqi-first-run-visual-storyboard-design.md`：首阶段完整单局 18 张视觉故事板。
- `output/imagegen/buqi-first-run/`：已按 01-18 生成并整理首局全流程效果图，统一采用“水墨机关修仙”方向。

## 核心循环评审（2026-08-04）

- 已确认产品边界：PC/移动双端；目标用户偏《The Bazaar（大巴扎）》的自动构筑玩家；单局仍为 8-12 分钟。
- Fun Hypothesis：玩家用有限格位与带代价的法门组装可预测的自动周天，对抗公开但不完整的道影；结果证明或推翻判断后，通过一次有代价的重构让下一战更接近预期。
- 设计支柱：先预测再验证、每格代表取舍、强度必须带账单、败因能转成行动、异步对手是题目而非倍率墙。
- 流程裁决：现有 P0 能证明战斗算得对，但核心乐趣到 P2 才验证，顺序偏晚。Step 1 前新增 P-1 Fun Gate，用 9 个既有法门做纸面原型，验证“预测 -> 归因 -> 针对性重构”，不新增内容。
- 评审报告：`output/buqi-core-loop-review-v0.1.md`。

## Step 1 当前结论（2026-08-04）

- 已完成纯 C# DTO、枚举、定义访问、8 格棋盘校验、确定性目标选择、快照/日志规范化、战斗模拟器、测试夹具、无头验证器和 Unity EditMode 测试接入。
- 已按知识库完成命名空间、公开 API、私有字段、程序集边界和跨端字符串格式化整改；共享战斗源码不引用 Unity、UGF、ET 生命周期、场景、资源或随机 API。
- 三重评审结论：第一重规范与架构通过；第二重战斗契约主体通过；第三重测试与工程风险评审通过。
- Unity 最终 EditMode 回归：`TestResults.xml` 显示 `total=2`、`passed=2`、`failed=0`、`inconclusive=0`、`skipped=0`，`result=Passed`；测试程序集为 `Game.Hot.Buqi.Tests.dll`，平台为 EditMode。
- 无头 Release 编译：0 warning / 0 error。
- 无头 `verify`：行为契约全部通过，15 个 approved hash 全部匹配，未自动修改基线。
- 无头 `stress 10000`：`builds=10000`、`distinct=10000`、`invalid=0`、`hung=0`、`elapsedMs=5879`。
- 已补齐战斗模型、规则、模拟器、测试夹具、无头验证器和 Unity 测试的中文注释，明确 tick 0、同 tick 聚合、A-01..A-06、相邻空格阻断、劫火、硬上限和双层事件上限。
- 剩余风险：蓄力当前在 Resolve 内即时更新但日志阶段标为 Aggregate；Step 1 尚未覆盖复杂蓄力消费链。该风险不影响当前已批准向量，但进入后续内容扩展前需单独裁决。

## Step 2 当前结论（2026-08-04）

- 已新增 `Unity/Assets/Scripts/Game/Hot/Code/Buqi/Debug/BuqiBattleSandbox.cs`：Editor-only 沙盒模型，固定 seed `2026080402`，9 个临时法门、3 个方向、3 个固定场景、8 格文本棋盘、单场运行、100 次复跑、日志按 tick/chainId/来源/reasonCode 过滤。
- 已新增 `Unity/Assets/Scripts/Game/Hot/Code/Editor/Buqi/BuqiBattleSandboxWindow.cs`：EditorWindow 菜单 `Game/Buqi/Battle Sandbox`，只负责调试输入和展示，不进入正式玩家流程、不创建正式 UI/prefab。
- 已新增 `Unity/Assets/Tests/GameHot/Buqi/EditMode/BuqiBattleSandboxTests.cs`：覆盖九法门、三方向、S/M/L、护体获得/吸收/清空反击、相邻连锁、A-01/A-03/A-04、四维日志过滤、100 次 hash 一致和关闭重开状态隔离。
- Unity 刷新与编译真实通过：`generation=24`、`errorCount=0`、`warningCount=0`。
- Unity EditMode 真实终态通过：`Game.Hot.Buqi.Tests`，`total=9`、`completed=9`、`passed=9`、`failed=0`、`skipped=0`、`inconclusive=0`、`success=true`，耗时约 12.184 秒。首次测试发现并修正了 Step 2 对 A-01 日志原因码的过宽假设，未修改战斗契约或 approved hash。
- 菜单入口真实验证通过：`invoke_menu("Game/Buqi/Battle Sandbox")` 返回 `executed=true`。
- 无头回归真实通过：行为契约全部通过，15 个 approved hash 全部匹配；为避免历史 `obj`/`obj.wb` 被 SDK 默认扫描，`Buqi.Simulation.Headless.csproj` 增加 `DefaultItemExcludes` 排除生成目录。
- 已知风险保持不变：Charge 当前在 Resolve 内即时更新但日志阶段标记 Aggregate；沙盒只验证蓄力生成、条件阈值和联动，不硬编码法门 ID 实现动态消费。
- 首次批处理测试因 Unity 工程已被另一实例占用而停止，后续改用当前实例 Bridge 完成真实验收；没有删除锁文件或启动第二实例。
- 独立子 Agent 调用曾因服务端 403 无法使用，未伪装结果；本阶段以本地检查、跨端编译、Unity Bridge 和真实 EditMode 测试为准。

## Charge 0.4.1 门禁状态（2026-08-05）

- 已将蓄力读取/消费定型为通用配置字段 `ChargeReadLimit`、`AmountPerCharge`、`ChargeConsume`，不增加第七类基础效果，也不按法门 ID 写特例。
- 独立审计发现并修复同来源同触发声明的排序缺陷：同一 source/anchor 下现在按本批 `Declare` 插入序列作为最终 tie-breaker，确保先声明的蓄力可被同 tick 后续声明读取。
- 行为断言已覆盖：稳定 `Declare` 顺序即时获得、读取与消费；消费日志为负值且处于 `Declare` 阶段；无合法目标不消费；只读可重复读取；A-03 复写复用原声明快照且不二次消费；双方同 tick 消费后仍在 Aggregate/PostTick 同时判定。
- 在行为契约通过后，已显式运行无头 Release `update-hashes` 更新 approved hash；随后只读 `verify` 通过：15 个向量全部匹配。
- 无头 Release `all 10000` 真实通过：`builds=10000`、`distinct=10000`、`invalid=0`、`hung=0`、`elapsedMs=5844`。
- 独立门禁未启动第二个 Unity Editor；协调器整合后通过主工作区 Unity Agent Bridge 复验：重编译 `generation=26`、`errorCount=0`、`warningCount=1`（既有 `TutorialForm.m_SkipButton` 未使用警告）；EditMode `Game.Hot.Buqi.Tests` 为 `total=9`、`passed=9`、`failed=0`、`success=true`（runId `test-run-0f3cb05e70654dcaa2398b7efaac6495`）；`Game/Buqi/Battle Sandbox` 菜单返回 `executed=true`。

## 下一步

Charge 0.4.1 门禁已经完成，下一节点进入 Step 3 Luban Schema。Step 3 只建立 9 法门、3 淬炼与 6 道影的最小配置链路，仍不得直接录入完整 18 个法门或创建正式玩家 UI。

## Step 3 当前进展（2026-08-05）

- 已在 `Design/Excel/GameHot/Datas/Buqi/` 新增 `BuqiGlobal.xlsx`、`BuqiItem.xlsx`、`BuqiRefinement.xlsx`、`BuqiEcho.xlsx`，仅录入最小配置链路范围：9 个验证法门、3 个淬炼、6 份道影。
- 已在 GameHot 的 `__enums__.xlsx`、`__beans__.xlsx`、`__tables__.xlsx` 注册尺寸、品质、构筑方向、效果、触发、目标、条件、效果配置、格位实例与构筑快照；补齐 `__beans__.xlsx` 中 Luban 当前运行时需要的 `alias` 与 `variants` 字段子列。
- 已新增 `Game.Hot.Buqi.Config` 适配层：`BuqiDefinitionProvider` 深拷贝配置为战斗定义，`BuqiConfigValidator` 校验 Step 3 计数、ID、尺寸/价格、触发/效果/目标组合、引用和道影棋盘；`TablesComponent` 在生成表存在时通过反射读取并校验，不让战斗核心直接依赖 Luban 生成类。
- 命令行 `ExcelExporter` 已实际导出 GameHot Luban 代码与 bin/json 数据；协调器整合后又通过主 Unity 执行 `Game/Tool/ExcelExporter`，生成结果与提交一致，没有产生额外 diff。
- Step 3 主工作区验收完成：Unity 重编译 `generation=29`、`errorCount=0`、`warningCount=1`（既有 `TutorialForm.m_SkipButton` 未使用警告）；EditMode `Game.Hot.Buqi.Tests` 为 `total=14`、`passed=14`、`failed=0`、`success=true`（runId `test-run-a28a8b146218445bbbc47b511a884138`）；`Game/Buqi/Battle Sandbox` 菜单返回 `executed=true`。Luban Check/Export、无头战斗 `verify`、配置适配静态编译和生成 JSON 数量检查也均通过。

## 下一步

Step 3 已完成。下一门禁是利用现有 Battle Sandbox 执行 P-1 认知走查，验证“预测 -> 归因 -> 针对性重构”是否成立；P-1 通过前不进入正式玩家 UI、完整经济内容或 18 法门批量录入。

## P-1 认知走查准备（2026-08-05）

- 已在 Editor-only `BuqiBattleSandbox` 增加 `BuqiSandboxWalkthroughRecord`：战前必须记录参与者、场景和具体预测；战斗结果绑定真实 `BattleLogHash` 与 `Outcome`；战后才允许填写主因和下一轮改动意图。
- 已增加 `Purchase`、`Refinement`、`Position` 三类改动记录；记录模型不参与战斗输入、随机状态或结果计算。
- `BuqiBattleSandboxWindow` 已提供 P-1 记录入口，并在场景切换/清空结果时清理走查状态；菜单真实执行成功。
- Unity 真实编译：`generation=32`、`errorCount=0`、`warningCount=1`；唯一 warning 为既有 `TutorialForm.m_SkipButton` 未使用警告。
- Unity EditMode 真实终态：`Game.Hot.Buqi.Tests`，`total=17`、`passed=17`、`failed=0`、`skipped=0`、`success=true`。
- 独立 Review 会话仍被服务端 `403 This API can only be used with the WorkBuddy client` 拒绝，未伪装为 Review 通过；本轮改动因此暂不提交。
- P-1 体验结论尚未形成：仍需至少 3 类目标玩家、每人连续完成 3 轮“预测—结果—归因—针对性重构”，再依据真实记录判断是否进入 Step 4。自动测试不能替代玩家样本。
- 另有未提交的 effects/builds 扩展流修改共享 `BuqiTestSuite.cs`、`BuqiContractChecks.cs`，新增 Heal/Regen/Poison/Burn/Freeze 红测，但对应战斗枚举和模拟器语义尚未落地；该流与 P-1 共用战斗测试边界，暂不纳入 P-1 提交，也不能视为已通过。

## 下一步

先完成 P-1 独立 Review 门禁并根据 Review 修复；随后安排真实参与者走查并记录结果。只有 Review、代码回归和体验门禁均通过后，才提交 P-1 节点并进入 Step 4 战斗回放界面。
