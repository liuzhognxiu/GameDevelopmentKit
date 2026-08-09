# 《不器》AI 图像提示词全集
## 水墨机关修仙 · v0.1

> 依据：《首阶段完整单局视觉故事板设计》（`docs/superpowers/specs/2026-08-04-buqi-first-run-visual-storyboard-design.md`）、《不器》主线故事基线 v0.1（`buqi-main-story.md`）
> 日期：2026-08-08 ｜ 目标画幅：PC 横屏 16:9 ｜ 生成模型：gpt-image-2（默认，可换 Midjourney / SDXL / SD3.5）
> 目录：`output/imagegen/` ｜ 建议保存每张图到 `output/imagegen/buqi-first-run/NN-*.png`

---

## 0. 使用说明

- **一致性机制（最重要）**：每张图提示词都以 `STYLE_ANCHOR`（第 1 节）开头。先决定全局风格，再决定镜头，顺序不可颠倒。任何一张图丢掉锚定块，画面就会漂回"通用中国风"或"仙侠卡牌"。
- **生成顺序**：先出 4 张视觉锚点 —— 01（主菜单）、10（周天盘整备）、13（连锁高潮）、17（胜利结局）。这四张锁定城市轮廓、器物签、周天盘、功能色四大语法后，再批量生成其余镜头。
- **文字处理**：AI 生成简体中文小字容易乱码。提示词只保留"必要的中文标题/短标签"，凡出现文字的镜头，验收时若文字有误，用真实字重排覆盖，不重跑整张图。
- **负面提示词**：统一使用第 2 节 Negative Block，所有图追加。
- **参数建议**：锚点图 quality=high，其余 medium；尺寸 1536x1024（3:2）；PNG。主菜单可用 16:9。

---

## 1. 全局风格锚定块 STYLE_ANCHOR

以下两段效果等价，**英文段用于直接粘贴**，中文段用于自己调参/写变体时参考。

```
STYLE_ANCHOR = "2.5D hand-painted game scene in a Chinese xianxia cultivation world, art direction 'ink-wash mechanical xianxia'. Distant scenery and spiritual energy rendered with soft ink-wash gradients and mist; interactive objects, equipment and state feedback in dense mineral-pigment colors (cinnabar red, jade green, mineral gold, indigo, amber). Materials: warm celadon jade, aged bronze, black lacquered wood, rice paper, cinnabar seal marks, worn silk. No plastic look, no glass sci-fi panels. Market scenes lit by warm lantern light, wood and rice paper; trial scenes lit by cold moonlight, jade and bronze. Game UI clean, restrained and readable, an eight-slot circular 'Zhoutian board' as the main focal point; UI is a real playable interface, not a poster, not a full-screen concept illustration."
```

中文锚定块（等价说明，不用于粘贴）：

> 2.5D 手绘游戏场景，中国修仙世界观，视觉方向「水墨机关修仙」。远景与灵气用柔和水墨晕染与雾；交互物、器物与状态反馈用浓重矿物重彩（朱砂红、青玉绿、矿物金、靛青、琥珀）。材质：温润青玉、旧青铜、黑漆木、宣纸、朱砂印痕、磨损丝绸；避免塑料感和科幻玻璃面板。坊市场景暖灯、木与宣纸；问道场景冷月光、青玉与青铜。UI 清晰、克制、可读，八格周天盘为第一焦点；是真实可玩的界面，不是海报也不是全屏概念插画。

---

## 2. 统一负面提示词与参数

```
NEGATIVE_BLOCK = "modern office objects, office software UI, cyberpunk neon, western magic academy, european castle, traditional playing-card hand zone, manual skill bar during combat, active card-drawing hand, realistic photo, 3D render look, plastic material, glass sci-fi panels, watermark, signature, logo, garbled text, english long text, heavy text overlay, full-screen explosion covering the board, heroic power pose, movie poster composition"
```

| 项目 | 建议值 |
|---|---|
| 模型 | gpt-image-2（默认）；Midjourney v6.1+（改 `--ar 3:2 --style raw`）；SDXL/SD3.5（用同一锚定块，CFG 6-7） |
| 尺寸 | 1536x1024（3:2）；主菜单 1792x1024（16:9） |
| 质量 | 锚点图 01/10/13/17 = high；其余 = medium |
| 输出 | PNG，`output/imagegen/buqi-first-run/NN-<slug>.png` |
| 变体 | 每张先出 2-3 变体选 1，确认后再微调，不直接改关键锚点 |

---

## 3. A 组：首局全流程 18 张效果图

> 每张 = `STYLE_ANCHOR` + 下列镜头块。中文要点用于验收与微调。

### 01 标题与主菜单（Title & Main Menu）⭐锚点
```
SCENE_01 = "A vast moving cultivation city 'Buqi City' seen from a distance in a sea of clouds, city silhouette built from an old ferry-boat keel, layered market stalls, glowing spirit-meridian streets and a visible trial platform ('Wendao Platform') in the city center, hinting at the core gameplay. Composition: the city occupies the upper two-thirds, dark ink-wash mountains and cloud sea below. Overlay: a stylized game title in ink calligraphy and bronze engraving style, two menu buttons with minimal Simplified Chinese labels (e.g. '进入众妙集', '设置'). Cold moonlight and faint warm lantern dots inside the city. Cinematic wide shot, atmospheric, restrained UI."
```

中文要点：第一视口即不器城轮廓（渡舟船骨+坊市+灵脉）；标题为墨字+青铜刻痕；露出问道台预告玩法；主命令「进入众妙集」。

### 02 开场：破烂也能成道（Opening: Junk Becomes a Working Zhoutian）
```
SCENE_02 = "Close-up of a market stall scene in the bustling 'Zhongmiao Ji' market. On a worn wooden table, a leaking alchemy furnace, an old sword and a heart-protecting bell unexpectedly form a small self-sustaining circuit: cinnabar-red formation lines connect them into a loop on the tabletop, faint warm glow. A young itinerant cultivator (simple robes, no armor, no halo) watches with surprised curiosity, NOT a heroic pose. Behind the stall, the trial-platform keeper in plain bronze-trimmed robes hands over a plain low-grade trial token. Warm lantern light, wooden stalls, hanging paper lanterns, other shoppers blurred in background."
```

中文要点：漏火丹炉+旧剑+护心铃连成小周天，朱砂阵纹在桌面成回路；主角惊讶但非摆拍；看守递最低级问道令。

### 03 起始周天选择（Starting Zhoutian Choice）
```
SCENE_03 = "Three identical-budget starting loadout options displayed side by side as three equal panels on a stone trial-platform UI: (left) fast-attack set with small light flying swords and gold forward-flow lines, (center) shield-counter set with jade disc talismans and concentric circle motifs, (right) chain-reaction set with array diagrams and amber charging nodes. Each panel shows three artifact slips, a recommended eight-slot Zhoutian board layout, and one clear chain arrow linking them. Panels differ by silhouette and rhythm, readable at a glance. Top status bar with spirit stones, dao seals. Cold moonlight, jade and bronze palette."
```

中文要点：三套投入相同的起始方案并列；每套三件器物签+推荐八格布局+联动箭头；仅凭轮廓与节奏可辨差异（快攻=金线、护体反击=同心圆、连锁=琥珀节点）。

### 04 轮次简报（Round Briefing）
```
SCENE_04 = "Game UI screen: background is a miniature ink-wash block plan of Buqi City with glowing spirit-meridian streets. Foreground panel shows the current round's Dao-Shadow opponent summary (translucent ink silhouette and small Zhoutian board), current dao seals and dao foundation pips, and two preparation opportunities. Two selectable entrances look like street-gate formations, clearly labeled market / refinement / upgrade / event. Warm lantern tones, wood and rice paper UI, no branching map, no scroll."
```

中文要点：缩略街区背景+道影摘要+道印/道基+两次准备机会；入口像街巷阵门，不做多路线地图。

### 05 综合坊市（General Market）
```
SCENE_05 = "Market shop UI in the Zhongmiao Ji: a lively stall row with four artifact-slip merchandise cards displayed horizontally across the middle, each card an object-shaped artifact slip (weapon, talisman, array diagram, spirit creature) embedded in a bronze base showing size and build tag; each card shows spirit-stone price, refresh, lock and sell affordances. Two of the four items carry a visible build-tag (fast / shield / chain). Product silhouettes and size info more prominent than the merchant decoration. Warm lanterns, hanging scrolls, worn wood counter."
```

中文要点：四件商品横向陈列、覆盖至少两种构筑标签；灵石/价格/刷新/锁定/出售；商品轮廓与尺寸比商人装饰醒目。

### 06 效率专场（Efficiency Special）
```
SCENE_06 = "A fast-moving market stall special: lightweight flying swords, talismans and small array devices hanging on a rapidly rotating conveyor-rack mechanism, mostly small and medium artifact slips. UI structure identical to the general market (same top bar and card row), but the stall machinery, golden forward-flow lines and product mix express high-frequency triggers and chain combos. Gold mineral accents, warm lanterns, slight motion blur on the conveyor, readable prices."
```

中文要点：轻巧飞剑/符箓/小型阵器挂快速传送架；小/中型法门为主；用摊位机关与金色流线表达"高频与连锁"。

### 07 风险专场（Risk Special）
```
SCENE_07 = "A dangerous underground furnace-stall special: old cracked alchemy furnaces, cracked artifacts and sealed spirit creatures displayed at low prices. Purple-black imbalance patterns and orange warning marks are visibly engraved on the goods and stall banners, explicitly communicating 'cheap but carries imbalance risk'. Dim red furnace glow, dust, worn iron and stone, scattered cinnabar warning seals. Same card-row UI as the general market, but with hazard framing."
```

中文要点：地下炉坊式危险摊位；旧丹炉/裂纹法器/封印灵物低价；紫黑失衡纹+橙色警示必须可见；"便宜但带失衡"。

### 08 奇遇选择（Event Choice）
```
SCENE_08 = "Event interface anchored in the world, not a detached popup: a cinnabar-red invitation scroll ('Bailian Invitation') floats at the center of the screen, above a market background that stays visible. Below it, three option cards list fully visible consequences; the first option visually reads the player's current eight-slot Zhoutian board (mini board shown on the card). One option highlighted with warm gold 'confirm' frame. Ink-wash mist edges around the scroll, jade-and-bronze accents."
```

中文要点：朱砂请帖悬于画面中心；三个后果完整的选项分列下方；其一读取当前八格周天；背景保留众妙集环境，不做脱离世界的纯弹窗。

### 09 升级与淬炼（Upgrade & Refinement）
```
SCENE_09 = "Refinement workbench UI: left panel lists held artifact slips; center shows the same artifact at three quality stages (plain / improved / finalized) with visible material and glow changes; right panel shows six refinement seal types with their costs (spirit stones and risk marks). A merging interaction between two already-refined artifacts is highlighted with a prominent confirm frame. Warm furnace light, bronze anvil, rice-paper recipe scrolls, amber and cinnabar accents."
```

中文要点：炼器台界面；左=持有法门，中=普通/改良/定型品质变化，右=六种淬炼印记及代价；合并已淬炼法门时"保留选择"必须醒目。

### 10 周天盘整备（Zhoutian Board Preparation）⭐锚点
```
SCENE_10 = "THE most important persistent UI screen, full view. A complete 8-slot circular Zhoutian board dominates the lower-center, made of bronze rings and jade cell slots; a 5-slot storage rack sits beside it. The player is currently dragging an array-diagram artifact slip that occupies 2 adjacent slots, half-placed, with a ghost outline showing the target slots. Top status bar: spirit stones, dao seals, dao foundation pips, current round. Right side: artifact detail panel with cooldown estimate and a '确认问道' confirm command. Spatial occupancy and adjacency instantly readable, clean layout, no decorative clutter over the board."
```

中文要点：首阶段最重要常驻界面。底部/中央完整 8 格周天盘+5 格仓位；正在拖动占 2 格的阵图（带半透明放置预览）；顶部资源条；右侧详情+预计冷却+确认问道；占用与相邻一眼可读。

### 11 道影预览（Dao Shadow Preview）
```
SCENE_11 = "Pre-battle screen on the Wendao Platform: a translucent ink-wash Dao-Shadow cultivator silhouette stands in the far background with its own small Zhoutian board projected beside it. Foreground panel lists only: build direction, three key artifact slips, main threat, board occupancy, known refinements. The player's own Zhoutian board remains visible at bottom for last adjustments. Cold moonlight, jade and bronze, ghostly ink effects on the Dao Shadow, readable information hierarchy."
```

中文要点：战前界面；墨色道影在远端+其小周天；只展示方向/三件关键法门/主要威胁/占用/已知淬炼；玩家周天保持可见。

### 12 战斗开场（Battle Opening）
```
SCENE_12 = "Battle opening frame: two 8-slot Zhoutian boards face each other, one near-bottom and one far-top (or left-right), projected onto the Wendao Platform's bronze formation ring. Health, shield, imbalance and continuous cooldown rings are all in initial state, clearly visible. Camera focused on the boards, NO characters fighting each other. The two loadouts are readable as artifact circles; one side has distinct silhouettes, the other side differs in color and layout. Cold moonlight, bronze ring, faint ink mist."
```

中文要点：双方 8 格周天盘对置（远近或上下）；气血/护体/失衡/连续冷却全部初始状态；镜头聚焦棋盘不角色对砍；青铜阵环投影器物周天。

### 13 连锁运转高潮（Chain Cascade Climax）⭐锚点
```
SCENE_13 = "Mid-battle automatic-combat keyframe: the source artifact slip, its target, left-right adjacent chain transfers and a charging chain are highlighted simultaneously but with clear layer hierarchy. Golden chain energy flows along the bronze bases of the artifact slips; a jade shield disc absorbs a cinnabar sword-slash; continuous cooldown rings are clearly visible on each slip. Imbalance bar partially filled. The Zhoutian board stays fully readable, no explosion covers the board. Dynamic but readable, cold moonlight with gold and cinnabar accents."
```

中文要点：中期关键帧；来源/目标/左右相邻传递/蓄力链同时高亮但层级清楚；金色连锁沿青铜底座流动；护体玉璧吸收朱砂剑痕；连续冷却环清晰可见。

### 14 加班与走火（Overtime & Mishap）
```
SCENE_14 = "Dangerous state after the 45-second overtime mark: the outer ring of the Wendao Platform formation turns purple-black, both sides take increasing direct damage shown as readable number ticks. One side reaches the imbalance threshold and its furnace bursts into a controlled flame-mishap ('zouhuo') — cinnabar-red fire and offset formation lines, but the affected Zhoutian board remains visible and readable. Purple-black haze, orange warning edges, numbers and causality readable, no full-screen explosion."
```

中文要点：45 秒后危险状态；问道台外圈转紫黑+递增直接伤害；一侧失衡达阈值触发炉火走火；保持数值与因果可读，不用全屏爆炸遮住周天盘。

### 15 胜利复盘与轮次结算（Victory Review & Settlement）
```
SCENE_15 = "Post-victory review screen: three stat rows show top damage dealers, two rows show effective shield absorption, a compact timeline strip shows the last five seconds of key trigger chain, and one fact-based one-line summary in Simplified Chinese. A new dao seal glows into place and victory spirit stones are counted. Background: the city streets briefly mirror the player's final Zhoutian layout as faint glowing lines — the city is learning. Warm celebratory lantern light, gold accents, calm composition."
```

中文要点：伤害前三、有效护体前二、最后五秒关键触发链、一句基于日志的事实摘要；新增道印+胜利灵石；背景城街短暂映出玩家周天布局（不器城在学习）。

### 16 战败复盘与调整提示（Defeat Review & Adjustment Hints）
```
SCENE_16 = "Post-defeat review screen, NOT punishing: highlights the most-delayed core artifact slip (with a heavy indigo drag-trail mark), a chain that never started (grey unlit links), and the imbalance-loss damage (orange marks). One dao foundation pip is lost, and a small public defeat consolation of spirit stones is granted. The primary command button is '调整周天' — clearly showing what can be changed next round. Softer warm light, no red-black doom styling, constructive tone."
```

中文要点：战败但不羞辱；突出被延迟最久的核心法门（靛青拖尾）、未启动连锁（灰暗）、走火损失（橙）；失去一点道基+失败灵石补偿；主命令「调整周天」。

### 17 单局胜利结局（Run Victory Ending）⭐锚点
```
SCENE_17 = "Ending frame: five dao seals form a closed ring around the Wendao Platform, all glowing. Buqi City's bell is ringing (abstract bell wave lines in the ink-wash sky). Street spirit-meridian lines briefly re-trace the player's final Zhoutian layout across the city. The player stands as an ordinary cultivator, no hero transformation — the emphasis is 'this path works'. Two commands visible: continue reviewing the battle report, return to Zhongmiao Ji. Celebration via light, rings and resonance, not fireworks."
```

中文要点：五枚道印在问道台闭合成环；钟声（墨天中的抽象钟波纹）；街道灵脉复现玩家最终周天；主角仍是普通散修，重点是"这条路运转起来了"。

### 18 单局失败结局（Run Defeat Ending）
```
SCENE_18 = "Ending frame: all three dao foundation pips depleted, the trial token shows a crack but is NOT destroyed. The player packs up the incomplete artifacts and walks back into the warmly lit Zhongmiao Ji in the foreground; the Wendao Platform still stands open in the distance. A summary panel lists the key issues of the run, with two commands: '重新问道' and '返回菜单'. Warm, gentle, constructive mood — failure reads as a readable experiment, not a dead end. Soft lantern light, ink-wash dusk."
```

中文要点：三点道基耗尽；问道令裂纹但不销毁；收起残缺法门回到灯火温暖的众妙集，远处问道台仍开放；摘要+重新问道+返回菜单；失败是可读的试错。

---

## 4. B 组：核心 UI 组件资产提示词

> 用于生成独立组件图（纯白/纯黑底，方便抠图或直接当 UI 素材参考），统一追加 `STYLE_ANCHOR` 与 `NEGATIVE_BLOCK`。

### B1 器物签 · 法器（Artifact Slip – Weapon）
```
UI_ASSET_WEAPON = "A single artifact slip UI component: a weapon (chipped old flying sword) embedded in a shared aged-bronze base frame shaped like an elongated talisman plate, bronze base carries size marks (2 cells), cooldown ring around the base edge, quality glow in faint jade green, small cinnabar seal at the corner. Object-shaped silhouette, NOT a rectangular poker card. Isolated on a plain rice-paper background, centered, no other elements, top-down UI view."
```

### B2 器物签 · 符箓（Artifact Slip – Talisman）
```
UI_ASSET_TALISMAN = "A single artifact slip UI component: a folded paper talisman with cinnabar script embedded in a shared aged-bronze base frame, size mark 1 cell, cooldown ring, amber charging node dots lighting along the edge, small cinnabar seal at the corner. Object-shaped silhouette, not a rectangle. Isolated on plain rice-paper background, centered, top-down UI view."
```

### B3 器物签 · 阵图（Artifact Slip – Array Diagram）
```
UI_ASSET_ARRAY = "A single artifact slip UI component: a folded array diagram scroll with jade-green formation lines embedded in a shared aged-bronze base frame, size mark 3 cells, cooldown ring, faint rotating inner ring, small cinnabar seal at the corner. Object-shaped silhouette, not a rectangle. Isolated on plain rice-paper background, centered, top-down UI view."
```

### B4 器物签 · 灵物（Artifact Slip – Spirit Creature）
```
UI_ASSET_SPIRIT = "A single artifact slip UI component: a small spirit creature (ink-haze beast, warm amber eyes) resting inside a shared aged-bronze base frame, size mark 2 cells, cooldown ring, faint breath-glow, small cinnabar seal at the corner. Object-shaped silhouette, not a rectangle. Isolated on plain rice-paper background, centered, top-down UI view."
```

### B5 周天盘底座（Zhoutian Board Base）
```
UI_ASSET_BOARD = "Top-down view of an empty eight-slot circular Zhoutian board: eight equal cell slots arranged in a ring, made of aged bronze ring segments with celadon-jade inlaid cells, subtle engraved formation lines connecting the slots, two adjacent-slot and one three-slot capacity visually suggested by slot grouping. Central void with faint ink-wash mist. Isolated on plain rice-paper background, centered, no artifact slips, top-down UI view."
```

### B6 资源图标组（Resource Icon Set）
```
UI_ASSET_ICONS = "A set of three small game resource icons in a row, same jade-and-bronze style: (1) spirit stones — small pale jade pebbles with inner glow; (2) dao seal — a square bronze seal with cinnabar top; (3) dao foundation pip — a small ring of light with three dots. Flat UI icons, clean silhouettes, subtle ink outline, consistent size, isolated on plain rice-paper background."
```

### B7 道影（Dao Shadow）
```
UI_ASSET_SHADOW = "A translucent ink-wash Dao-Shadow: a monk-like cultivator silhouette in flowing dark ink robes, semi-transparent with visible brush-stroke texture, no facial detail, holding a faint small Zhoutian board projection beside it. Cold moonlight rim, ghostly, dignified not menacing. Isolated on a misty dark background, full-body view, game UI reference."
```

### B8 顶部状态栏（Top Status Bar）
```
UI_ASSET_STATUSBAR = "A horizontal game UI top status bar: four compact readouts in one row — round number, spirit stones, dao seals (5 slots), dao foundation pips (3 slots), each as a small jade-and-bronze capsule with clear icons and minimal labels. Clean, restrained, no decorative overload. Isolated on plain rice-paper background, top-down UI view."
```

---

## 5. C 组：六类战斗效果反馈提示词

> 生成 6 张"效果示意"图（可拼一张战斗界面或独立测试），用于统一功能色与形状语言。

### C1 伤害（Damage）— 朱砂红 / 断裂剑痕 / 锐角
```
EFFECT_DAMAGE = "Close-up of a combat effect on an artifact slip: a short cinnabar-red slash impact, broken sword-mark fracture lines and sharp angles striking a jade cell on the Zhoutian board, red engraved marks briefly glowing, small readable damage number in red. Ink-burst edges, mineral-pigment red, readable, not covering the board."
```

### C2 护体（Shield）— 青玉绿 / 同心圆 / 完整环
```
EFFECT_SHIELD = "Close-up of a shield effect: a translucent jade-green disc with concentric complete rings expanding softly around an artifact slip, gentle radial diffusion like a jade bi-disc, absorbed damage shown as a red slash being stopped at the ring edge. Soft green glow, calm, readable numbers."
```

### C3 加速（Haste）— 明金 / 前倾流线 / 连续短划
```
EFFECT_HASTE = "Close-up of a haste effect: mineral-gold forward-leaning streamline strokes and short continuous dashes flowing along the cooldown ring of an artifact slip, the ring visibly spinning faster, slight motion lines. Warm gold glow, energetic but clean."
```

### C4 延迟（Delay）— 靛青 / 向后拖尾 / 滞涩波纹
```
EFFECT_DELAY = "Close-up of a delay effect: heavy indigo-blue drag-trail lines pulling backward on an artifact slip's cooldown ring, sluggish ripples like thick ink, the ring nearly frozen, subtle blue haze. Heavy, viscous feel, readable state."
```

### C5 蓄力（Charge）— 琥珀 / 珠点 / 刻度
```
EFFECT_CHARGE = "Close-up of a charge-up effect: amber bead-like nodes lighting up one by one along tick marks on an artifact slip edge, a small progress scale, the last node flaring brighter. Warm amber glow, precise, readable progress."
```

### C6 失衡/走火（Imbalance / Mishap）— 紫黑 + 警示橙 / 裂纹 / 偏心环
```
EFFECT_MISHAP = "Close-up of an imbalance state: purple-black crack lines and an off-center wobbling ring around a furnace-like artifact slip, with orange warning marks at the rim; small furnace fire spurting out and formation lines misaligned. Dangerous but contained, readable, not a full-screen explosion."
```

---

## 6. D 组：世界场景与角色提示词

> 用于世界氛围图、载入图、角色参考图。均追加 `STYLE_ANCHOR`（或按场景取后半段）+ `NEGATIVE_BLOCK`。

### D1 不器城全景（Buqi City Panorama）
```
WORLD_CITY = "Panorama of the moving city Buqi: an enormous city built on and around an ancient ferry-boat hull, growing from layered market stalls, abandoned formation disks and repaired hulls made of broken flying swords, streets glowing as spirit meridians, a visible central Wendao Platform, drifting in a sea of clouds under a vast ink-wash sky with three faint bell-wave rings. Warm lantern dots vs cold moonlight, migration mood, misty edges."
```

### D2 众妙集（Zhongmiao Ji Market）
```
WORLD_MARKET = "Interior street of the Zhongmiao Ji market: crowded stalls of alchemists, itinerant daoists and sect outcasts trading weapons, talismans, array diagrams and spirit creatures, worn wood counters, hanging paper lanterns, rice-paper banners, one stall showing a small self-running Zhoutian circuit of junk items, warm busy atmosphere, slightly cluttered but readable."
```

### D3 问道台（Wendao Platform）
```
WORLD_PLATFORM = "The central Wendao Platform of Buqi City: a raised circular bronze-and-jade platform whose ring can project two facing Zhoutian boards, surrounded by stone steps and ink-wash mist, cold moonlight, jade pillars, bronze formation lines, quiet and solemn, a few cultivators waiting at the edges."
```

### D4 云海/荒漠/群山/秘境（Four Travel Landscapes）
```
WORLD_LANDSCAPES = "Four small landscape vignettes in a row showing where Buqi City might rest: (1) sea of clouds peaks, (2) desert dunes with ancient formation ruins half-buried, (3) layered mountain ranges with flying-sword trails, (4) misty secret realm with glowing spirit plants. Same ink-wash mineral style, each with a tiny distant ferry-city silhouette, consistent palette."
```

### D5 主角 · 年轻散修（Young Itinerant Cultivator）
```
CHARACTER_HERO = "Character reference: a young itinerant cultivator, plain worn robes in ink-gray and dark blue with a few jade and bronze trinkets, no armor, no halo, practical posture, carrying a bundle of odd junk artifacts (small furnace, old sword, talisman), friendly and curious expression, full-body turn-around style reference, ink-wash shading with mineral accents, game character sheet."
```

### D6 众妙集商人（Market Merchant）
```
CHARACTER_MERCHANT = "Character reference: a middle-aged merchant of the Zhongmiao Ji, round face, worn but clean clothes in warm browns, brass scales and a ledger on the belt, one sleeve showing a faint spirit-meridian tattoo, shrewd but kind expression, full-body reference, ink-wash with warm lantern palette."
```

### D7 问道台看守（Trial Keeper）
```
CHARACTER_KEEPER = "Character reference: the Wendao Platform keeper, plain bronze-trimmed dark robes, calm weathered face, holding a low-grade trial token, slight old-scholar air, neutral friendly expression, full-body reference, jade-and-bronze palette."
```

### D8 天衡院修士（Tianheng Academy Cultivator）
```
CHARACTER_ACADEMY = "Character reference: a cultivator official from the Tianheng Academy, immaculate uniform-like robes in pale jade and white with precise bronze seal insignia, straight posture, composed and reasonable expression (not villainous), carrying a regulation array-measure device, full-body reference, cooler and more disciplined palette than the market folk."
```

---

## 7. 拼接示例与批量生成

**最终 Prompt = `STYLE_ANCHOR` + `SCENE_xx` + `NEGATIVE_BLOCK`**

示例（01 主菜单完整版）：

```
2.5D hand-painted game scene in a Chinese xianxia cultivation world, art direction 'ink-wash mechanical xianxia'. Distant scenery and spiritual energy rendered with soft ink-wash gradients and mist; interactive objects, equipment and state feedback in dense mineral-pigment colors (cinnabar red, jade green, mineral gold, indigo, amber). Materials: warm celadon jade, aged bronze, black lacquered wood, rice paper, cinnabar seal marks, worn silk. No plastic look, no glass sci-fi panels. Market scenes lit by warm lantern light, wood and rice paper; trial scenes lit by cold moonlight, jade and bronze. Game UI clean, restrained and readable, an eight-slot circular 'Zhoutian board' as the main focal point; UI is a real playable interface, not a poster, not a full-screen concept illustration. A vast moving cultivation city 'Buqi City' seen from a distance in a sea of clouds, city silhouette built from an old ferry-boat keel, layered market stalls, glowing spirit-meridian streets and a visible trial platform in the city center. Composition: the city occupies the upper two-thirds, dark ink-wash mountains and cloud sea below. Overlay: a stylized game title in ink calligraphy and bronze engraving style, two menu buttons with minimal Simplified Chinese labels. Cold moonlight and faint warm lantern dots inside the city. Cinematic wide shot, atmospheric, restrained UI. -- modern office objects, office software UI, cyberpunk neon, western magic academy, traditional playing-card hand zone, manual skill bar, realistic photo, 3D render look, plastic material, watermark, signature, logo, garbled text, heavy text overlay, movie poster composition
```

**生成顺序建议**（一致性优先）：
1. 锚点：01 → 10 → 13 → 17（锁定城市、器物签、周天盘、功能色）
2. 世界与角色：D1-D8（锁定资产词汇，之后所有镜头复用同一批描述词）
3. UI 组件：B1-B8（锁定器物签/图标/道影/状态栏）
4. 效果：C1-C6（锁定六色反馈）
5. 流程其余镜头：02-09、11-12、14-16、18

**一致性纪律**：
- 角色名/器物名/城市名在所有 prompt 中拼写完全一致（Buqi City / Zhongmiao Ji / Wendao Platform / Dao Shadow / Zhoutian board / artifact slip）。
- 同一功能色在任何镜头中不变：伤害=朱砂红、护体=青玉绿、加速=明金、延迟=靛青、蓄力=琥珀、失衡=紫黑+橙。
- 道影永远是"半透明墨色轮廓"，不升级成完整立绘。
- 棋盘永远 8 格，器物签永远"嵌入青铜底座的物件轮廓"，不是矩形扑克牌。

---

## 8. 验收清单（对照故事板）

生成完成后逐项打勾：

- [ ] 16:9 / 3:2 构图，主要界面完整
- [ ] 八格周天盘在所有相关图中结构一致
- [ ] 器物签为物件轮廓+青铜底座，非矩形卡牌
- [ ] 功能色六色一致（朱砂/青玉/明金/靛青/琥珀/紫黑橙）
- [ ] 坊市=暖灯木宣纸，问道=冷月青玉青铜
- [ ] 文字不遮挡关键信息；小字乱码可接受（真实字重排）
- [ ] 无现代职场物件、办公软件 UI、赛博朋克霓虹、欧美魔法学院
- [ ] 无手牌区、无战斗中手动技能栏
- [ ] 无全屏爆炸遮住周天盘（14 号图尤其注意）
- [ ] 胜败分支完整（15/16 与 17/18 气质区分：暖金庆祝 vs 温暖但不暗淡）
- [ ] 无角色对砍（12 号图镜头聚焦棋盘）
- [ ] 无水印、无签名、无无关文字

---

*本套提示词基于《不器》既定视觉方向「水墨机关修仙」编制，生成效果如出现材质漂移，优先检查是否丢失 STYLE_ANCHOR，其次检查是否引入 NEGATIVE_BLOCK 中排除的元素。*
