# 《不器》策划案总览与评审记录

> 建立日期：2026-08-23
> 用途：集中登记项目全部策划/设计文档，按编号逐份评审并记录结论。
> 真源原则：本文档只做登记与索引，**不复制正文**；各文档正文以原路径为唯一真源（复制会造成双份漂移，是本项目已存在的治理问题，不再新增）。

---

## 一、策划案清单

状态标记：✅ 有效 · ⚠️ 部分过期/与实现冲突 · ❌ 已废止（superseded） · 📦 在库但需核实
评审状态：⏳ 待评审 · ✅ 已评审通过 · 🔧 已评审待修改 · ⏭ 已跳过

### A 组｜核心概念与规格（`docs/game-concepts/`）

| 编号 | 文档 | 版本/日期 | 状态 | 评审 |
|---|---|---|---|---|
| A01 | `buqi-core.md` 核心概念 | 8/6 | ⚠️ 引用旧数字（18 件/v0.4） | 🔧 已改待确认 |
| A02 | `buqi-gameplay-spec.md` 玩法规格（总览） | v0.5（8/23 重写） | ✅ 顶层概览+索引，取代 v0.4 历史基线；待迁移 5 块已列清单 | ✅ 已评审 |
| A03 | `buqi-battle-contract.md` 战斗契约 | v0.7.0（8/23） | ✅ 删过载/怒气、灼烧减伤50%、A-06→A-04、10 格棋盘 | ✅ 已通过 |
| A04 | `gameplay-terminology.md` 玩法通用词汇 | v1.3（8/23） | ✅ 双层术语；删过载/怒气、加价值、改造 4、方向 7 | ✅ 已评审 |
| A05 | `buqi-feasibility.md` 可行性说明 | 8/6 | ⚠️ 旧基线（5 胜/18 件/60s 上限） | ⏳ |
| A06 | `buqi-prototype-plan.md` 原型计划 | v0.4（8/6） | ❌ 已 superseded（B 方案取代） | ⏳ |
| A07 | `buqi-unity-demo-work-plan.md` Unity Demo 分步计划 | 8/6 | ⚠️ Step 0-8 仍在用，门禁已被绕过 | ⏳ |
| A08 | `buqi-combat-system-s01.md` S01 战斗系统（迁回副本） | 8/23 迁回 | ✅ 仓库内真源；附 S01↔契约映射表 | ✅ 已迁回 |
| A09 | `buqi-run-loop-spec.md` 单局循环规格 | v0.5（8/23） | ✅ 实现级真源：无限天/双进度/单局生命/渡劫/英雄 + 代码改动清单（游戏术语版） | 🔧 待确认 |

### B 组｜系统设计包（`docs/game-concepts/systems/`）

| 编号 | 文档 | 状态 | 评审 |
|---|---|---|---|
| B01 | `systems/README.md` 设计包总纲 v0.1 | ⚠️ 基线 v0.4.1/5 胜/3 方向 | ⏳ |
| B02 | `systems/01-run-loop.md` 单局循环 | ❌ 被 A09 v0.5 取代（命令模型/保存恢复/验收已并入） | ✅ 已评审 |
| B03 | `systems/02-board-items-refinement.md` 棋盘/装备/改造 | v0.5（8/23） | ✅ 10 格线性/仓库 10/品质单独配置/改造 4/方向 7/Hero 字段 | 🔧 待确认 |
| B04 | `systems/03-combat-replay.md` 战斗回放 | ⚠️ 45s/60s 与 0.6.0 冲突 | ⏳ |
| B05 | `systems/04-economy-shops-events.md` 经济商店事件 | ⚠️ 3 商店/6 事件，无商人训练 | ⏳ |
| B06 | `systems/05-echo-opponents.md` 对手快照 | ⚠️ 无环形棋盘/无 300 件 | ⏳ |
| B07 | `systems/06-ux-onboarding-crossplatform.md` UX 引导 | ⚠️ 移动分辨率未锁定 | ⏳ |
| B08 | `systems/07-content-narrative-mastery.md` 内容叙事精通 | ⚠️ 门禁表 18/6/3/6/12 | ⏳ |
| B09 | `systems/08-validation-telemetry-tuning.md` P-1/埋点/平衡 | ⚠️ P-1 3/9 轮未通过 | ⏳ |
| B10 | `systems/09-tuning-register.md` 调优登记 | ⚠️ 缺战斗数值登记 | ⏳ |

### C 组｜B 方案（`Design/`）

| 编号 | 文档 | 状态 | 评审 |
|---|---|---|---|
| C01 | `Design/GAME_DESIGN.md` 效果词条与 Build 扩展设计 | ❌ 被 C03 v0.5 取代（8 构筑方向/体验支柱已并入） | ✅ 已评审 |
| C02 | `Design/Buqi_EFFECTS_BUILDS_DB.md` 词条与构筑 DB | ❌ 被 C03 v0.5 取代（24 件为历史快照，词条已修正） | ✅ 已评审 |
| C03 | `Design/Buqi_BUILD_SYSTEM_v0.5.md` 构筑系统与词条 DB | v0.6（8/23） | ✅ 15 词条（删过载/怒气、加价值）/7 方向/300 件归属 + 沙暴 | 🔧 待确认 |

### D 组｜设计规格（`docs/superpowers/specs/`）

| 编号 | 文档 | 状态 | 评审 |
|---|---|---|---|
| D01 | `2026-08-04-buqi-first-run-visual-storyboard-design.md` 首局视觉分镜 | ⚠️ 5 胜/18 件旧口径 | ⏳ |
| D02 | `2026-08-04-week-eight-phase-one-design.md` 星期八阶段一 | ❌ 历史包装 | ⏳ |
| D03 | `2026-08-05-buqi-effects-builds-db-design.md` 效果构筑库设计 | ⚠️ 24/6/16 基线 | ⏳ |
| D04 | `2026-08-05-buqi-ui-interaction-design.md` UI 交互方案 | ⚠️ 认知链阶段已被九日规格删除 | ⏳ |
| D05 | `2026-08-06-buqi-battle-replay-ui-demo-design.md` 战斗回放 UI | ⏳ |
| D06 | `2026-08-06-buqi-drag-deploy-ui-design.md` 拖拽布阵 UI | ⏳ |
| D07 | `2026-08-06-buqi-full-demo-ui-system-design.md` 完整 Demo UI 系统 | ⏳ |
| D08 | `2026-08-07-buqi-bazaar-v6-research-prototype-design.md` 大巴扎研究原型 | ⏳ |
| D09 | `2026-08-07-buqi-day-run-demo-design.md` 九日 Demo 设计 | ❌ 被 D11 无限天新玩法基线取代 | ✅ 已归档 |
| D10 | `2026-08-09-buqi-save-recovery-design.md` 存档恢复设计 | ✅ 与实现一致 | ⏳ |
| D11 | `2026-08-24-buqi-new-gameplay-baseline-design.md` 新玩法基线 | ✅ 无限天/六时段/10 格/20 单局生命/九胜后渡劫；当前实施真源 | ✅ 已批准 |

### E 组｜剧情、研究与输出物

| 编号 | 文档 | 状态 | 评审 |
|---|---|---|---|
| E01 | `docs/game-concepts/buqi-main-story.md` 主线故事基线 | v0.3（8/23 英雄制重写） | ✅ 已按用户方向重写（英雄选人/十胜问道/境界/道印/天劫成道） | 🔧 待确认 |
| E02 | `docs/game-concepts/buqi-story-outline.md` 剧情大纲 | v0.4（8/23 英雄制重写） | ✅ 已按用户方向重写（双进度/渡劫/天道三形态/四英雄） | 🔧 待确认 |
| E03 | `docs/game-concepts/buqi-heroes.md` 英雄系统设定 | v0.1（8/23 新建） | ✅ 四英雄/专属卡池/本命法宝/道途特质，对标大巴扎密度 | 🔧 待确认 |
| E04 | `KnowledgeBase/34-The_Bazaar_深度研究报告_2026-08-07.md` | ✅ 高质量研究；面向在线产品 | ⏳ |
| E05 | `output/buqi-core-loop-review-v0.1.md` 核心循环评审 | ✅ 历史评审记录 | ⏳ |
| E06 | `output/buqi-system-design-package-overview-v0.1.md` 系统设计包总览 | ✅ 与 B 组同源 | ⏳ |
| E07 | `output/imagegen/buqi-image-prompts.md` 美术提示词全集 | ⏳ |
| E08 | `output/imagegen/buqi-disc-style-prompts.md` 虹彩阵盘提示词 | ⏳ |
| E09 | `docs/策划案/调研/大巴扎三英雄构筑与卡片总结.md` 大巴扎三英雄权威口径 | ✅ 用户提供；校准 E03 对标 | ⏳ |
| E10 | `docs/策划案/调研/凡人修仙传_修仙体系与流派调研.md` 境界与流派来源 | ✅ 供境界命名对照 | ⏳ |

### 附录（非策划案，评审时作参考）

- **F｜开发计划**：`docs/superpowers/plans/` ×14（run-core/economy/battles/settlement/encounters、drag-deploy、battle-replay、run-shell、stage-gallery、day-run-demo-integration、save-recovery、parallel-integration 等）
- **G｜外部设计真源（仓库外）**：`C:\Users\123\WorkBuddy\大巴扎\outputs\系统策划案\`《星墟商栈》00 总纲 + S01~S12。**S01 战斗系统已迁回仓库（A08）**；其余 S02~S12 仍在仓库外，评审涉及到的系统需另行迁回。
- **H｜内容数据**：`Design/Excel/GameHot/Datas/Buqi/` 9 张表 + `Design/Excel/GameHot/Tests/Test-BuqiItemCatalog.ps1`（300 件门禁）

---

## 二、评审进度

- 完成：6 / 43（A02/A03/A04 通过；B02 被 A09 取代；C01/C02 被 C03 取代；A09 待确认；C03 待确认；A01 待确认；E01-E03 故事暂停）
- 进行中：玩法评审（下一步 B05 经济商店 / B06 对手快照 / 旧文档过载清理）
- 评审记录（每份确认后在此追加）：

| 编号 | 结论 | 用户意见 | 日期 |
|---|---|---|---|
| A01 | 🔧 需修改（已完成 5 项：版本声明/数字口径/定位分层/术语 0.6.0 对齐/补九日形态） | 4 项勾选 + "补充核心概念" | 2026-08-23 |
| E01/E02 | 🔧 按用户方向整体重写：大巴扎流程（10 胜/无限天/道基 Prestige 化）+ 境界=修为等级 + 道印=十胜 + 天劫=第 10 胜成道 | 多轮讨论后确认双进度方案 | 2026-08-23 |
| E03 | 🔧 新建英雄制设定：首阶段 4 英雄（阿瓷/闻辛/裴照川/荀照），按大巴扎"角色+专属卡片"密度包装，现有卡片可扩展归属 | 用户确认 4 角色起步；荀照旁白承接记为 TODO | 2026-08-23 |
| A03 | ✅ 通过。锁定 v0.6.0 为当前战斗真源；道途特质 v0.7.0 延后、沙暴无上限不动；S01 真源迁回仓库（A08） | 用户：1 不加 / 2 迁回 / 3 正确 | 2026-08-23 |
| C01/C02 | ✅ 已评审。合并更新为 C03 v0.5（词条对齐 v0.6.0、非目标更新、300 件归属、沙暴 30s 无上限） | 用户：现在做 v0.5 合并更新 | 2026-08-23 |
| A04 | ✅ 已评审。升级 v1.1：器物/道印/道基/道影 转正，装备/胜场/单局生命/对手快照 停用；补 暴击/多重/弹药/飞行/怒气 | 用户：修仙词为正式词 | 2026-08-23 |
| A04/B03 | 🔧 修正。术语改**双层**（游戏术语+包装术语并存，v1.2）；B03 → v0.5：棋盘 10 格线性、仓库 10、品质单独配置、8 方向、Hero 字段 | 用户：棋盘 10 格/仓库 10/术语双层/品质每品质单独配置 | 2026-08-23 |
| A03/A04/B03/C03 | 🔧 词条对齐大巴扎：删过载/怒气、加价值、灼烧=护盾减伤50%、改造 6→4、方向 8→7 | 用户逐条评审词条 + 过载连锁一并处理 | 2026-08-23 |
| A02 | ✅ 已评审。重写为 v0.5 总览（顶层概览+索引），取代 v0.4 历史基线；5 块待迁移内容已列清单 | 用户：B（重写总览） | 2026-08-23 |

---

## 三、评审顺序说明

按"核心 → 规格 → 系统 → B 方案 → 设计规格 → 剧情/研究"逐层评审，与文档依赖顺序一致；每份确认后再进入下一份。
