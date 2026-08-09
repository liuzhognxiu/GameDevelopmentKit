# 《不器》「虹彩悬浮阵盘」风格提示词集
## 唯美厚涂仙侠 · 圆盘元素 · v0.1

> 依据：用户选定 `output/imagegen/style-compare-v2/Breathtaking_beautiful_Chinese_2026-08-08T10-06-29.png` 中"中间圆盘位置风格"，沉淀其视觉语言为可复用提示词。
> 元素别名：虹彩悬浮阵盘 / Iridescent Levitating Disc / Crystal Platform。
> 用途：周天盘、战斗阵盘、问道台焦点、器物签底座等所有"圆形悬浮平台"类资产。

---

## 1. 元素视觉语言（供调参与微调参考）

| 维度 | 描述 |
|---|---|
| 结构 | 多层同心环（通常 3-4 层），上下叠置形成"层叠光环"，中央略凹可承托物体 |
| 材质 | 半透明玻璃 + 玉质温润感，表面类似抛光水晶或琉璃，内部隐约可见光纹 |
| 表面纹路 | 彩虹折射的虹彩纹路（青绿、粉橙、淡蓝、淡紫渐变），像油膜或贝母光晕；细密的同心纹、涡卷纹、符箓纹 |
| 边缘 | 金色高光勾边（暖金），整体泛着柔和金色辉光 |
| 悬浮方式 | 完全悬浮在空中（levitating），不接触下方实体；底层基座为青铜+汉白玉阶梯，阵盘浮于基座之上 |
| 光效 | 自发光（暖白偏金的内部光），向四周散射柔和光晕；下方地面有彩色光带辐射出去 |
| 配色 | 主色 = 暖金（边缘光） + 玉白（主材质）+ 虹彩（流纹）；不抢戏，不抢原色 |
| 气质 | 仙气 + 科技感 + 水晶感的混合；克制华丽，不喧宾夺主 |
| 与场景关系 | 是"焦点"而非"主体"——永远在画面中心偏上或中央，周围是飞檐仙山云海等实体场景 |

**关键词**：iridescent / levitating disc / crystal jade / concentric rings / golden rim glow / glass translucent / oil-slick iridescence / mother-of-pearl

---

## 2. 全局画风锚点（与阵盘配套使用）

```
STYLE_ANCHOR_DISC = "Breathtaking beautiful Chinese xianxia fantasy concept art, painterly digital oil style: a vast celestial cultivation city seen from a distance in a sea of clouds at dawn, ornate traditional pavilions with sweeping upturned eaves, vermilion pillars, white marble terraces, glowing spirit-meridian streets, cranes circling, god rays through clouds, rich warm golden-hour lighting, ultra-detailed, ethereal mist, cinematic composition, 16:9, no text, no watermark"
```

> 中文等价说明：唯美仙侠厚涂/次世代国风，温暖金光氛围（破晓/黄昏），精美飞檐仙山，仙鹤流云，超细节油画风。

---

## 3. 核心资产提示词

### D1 阵盘本体（独立 asset，纯净白底/米色底）
```
PROMPT_DISC_ASSET = "[STYLE_ANCHOR_DISC] Single central prop, a levitating iridescent crystal disc platform: 3-4 concentric translucent rings stacked vertically with a slight gap between layers, the material is warm jade-white polished crystal with internal glow, surface covered in soft mother-of-pearl iridescence (pale teal, peach, lavender, sky blue), each ring rimmed with a thin warm-gold edge highlight, fine concentric engraved talisman-like patterns on the surface, the disc floats above a small aged-bronze and white-marble pedestal with stone steps, soft warm light radiates outward and downward as gentle rainbow-colored light bands on the pedestal floor. Isolated on a plain warm-cream background, three-quarter view from slightly above, centered, prop asset, game UI reference, no text, no watermark"
```

### D2 阵盘正面俯视（用于周天盘 UI）
```
PROMPT_DISC_TOP = "[STYLE_ANCHOR_DISC] Top-down view of an iridescent levitating crystal disc: 3 concentric translucent jade-white rings with golden rims, the surface inscribed with fine jade-green concentric formation lines and eight evenly distributed slot marks around the inner ring, iridescent mother-of-pearl shimmer (pale teal, peach, lavender), soft golden glow inside the rim, the whole disc slightly hovers above an aged-bronze frame visible at the edges, clean and readable as a game UI board, isolated on warm-cream background, centered, no text, no watermark"
```

### D3 阵盘战斗态（带光剑流光+能量波）
```
PROMPT_DISC_BATTLE = "[STYLE_ANCHOR_DISC] Iridescent levitating crystal disc in an active combat state: 3-4 concentric rings spinning slowly at different speeds, bright golden light streams along the rim and arcs between rings like chains of energy, jade-green and cinnabar-red damage sparks flashing on the surface, a translucent jade shield dome shimmering above the disc, iridescent oil-slick patterns more vivid and dynamic, warm gold volumetric rays radiating outward, the bronze-and-marble pedestal below catches reflected colored light, single prop centerpiece, dramatic lighting, three-quarter view, no text, no watermark"
```

### D4 阵盘+器物签承载（物件置于阵盘之上）
```
PROMPT_DISC_WITH_SLIP = "[STYLE_ANCHOR_DISC] Iridescent levitating crystal disc platform, on top of which sits a single artifact slip (an object-shaped item, e.g. a chipped old flying sword) embedded in an aged-bronze base, the disc's iridescent rings softly encircle the slip, golden rim glow highlights the slip's silhouette, faint warm light pooling under the slip, the bronze-and-marble pedestal beneath the disc, three-quarter view, single composition, prop asset, no text, no watermark"
```

### D5 阵盘嵌入城市广场（问道台焦点）
```
PROMPT_DISC_IN_CITY = "[STYLE_ANCHOR_DISC] An iridescent levitating crystal disc at the center of a celestial city square: the disc floats above a raised white-marble and bronze trial platform, surrounded by traditional upturned-eave pavilions and lanterns, spirit-meridian streets of iridescent light connecting to the disc like rivers, dawn god rays piercing through sea of clouds, cranes circling overhead, the disc is the visual focal point but does not block the architecture behind, panoramic wide shot, 16:9, no text, no watermark"
```

---

## 4. 应用变体（场景化复用）

### 4.1 主菜单 · 不器城（升级版）
```
SCENE_01_V2 = "[STYLE_ANCHOR_DISC] The moving cultivation city Buqi: city built on an ancient ferry-boat keel growing into layered pavilions, glowing spirit-meridian streets, cranes, sea of clouds at dawn, an iridescent levitating crystal disc floats above the central trial platform, the disc is the visual focal point catching god rays, minimal UI overlay with a stylized Chinese title 'Buqi' in ink and bronze engraving style, cinematic wide shot, atmospheric, restrained UI, 16:9, no text, no watermark"
```

### 4.2 周天盘整备（核心 UI · 阵盘作主棋盘）
```
SCENE_10_V2 = "[STYLE_ANCHOR_DISC] Persistent game UI screen centered on an iridescent levitating crystal disc functioning as the eight-slot Zhoutian board: 8 cell slots visible as iridescent jade cells arranged around the disc's inner ring, sized for S/M/L artifact slips, an artifact slip (object-shaped, in bronze base) half-placed across two slots with a translucent ghost outline, top status bar with spirit stones / dao seals / dao foundation pips, right side detail panel with cooldown ring, the disc's golden rim glow softly frames all UI elements without clutter, clean, readable, 16:9, no text, no watermark"
```

### 4.3 战斗开场（双方阵盘对置）
```
SCENE_12_V2 = "[STYLE_ANCHOR_DISC] Battle opening frame: two iridescent crystal discs face each other (one near-bottom, one far-top), each floating above its own bronze-marble pedestal, health / shield / imbalance / continuous cooldown rings all in initial state, golden rim glow on both discs, faint warm god rays from above, distant traditional pavilions and cranes in background, camera focused on the two discs (not on characters), 16:9, no text, no watermark"
```

### 4.4 战斗连锁高潮
```
SCENE_13_V2 = "[STYLE_ANCHOR_DISC] Mid-battle chain-combo climax: two iridescent discs face each other, bright golden chain-energy streams arc between artifact slips across the discs, jade shield disc shimmering over one side, cinnabar damage sparks flashing on the other, both discs' iridescent rings spinning faster and brighter, golden rim glow intensified, formation lines glowing under the discs, drama without obscuring the boards, 16:9, no text, no watermark"
```

### 4.5 问道台特写（用于炼器台/事件 UI 背景）
```
SCENE_BACKDROP_DISC = "[STYLE_ANCHOR_DISC] A close-up cinematic backdrop of an iridescent levitating crystal disc hovering above an aged-bronze and white-marble trial pedestal, golden rim glow soft against dawn clouds, faint iridescent light bands radiating outward, designed as a UI background plate, slightly blurred atmosphere in distance, calm and divine, 16:9, no text, no watermark"
```

---

## 5. 负面提示词（与本风格配套）

```
NEGATIVE_DISC = "modern office objects, office software UI, cyberpunk neon, western magic academy, traditional playing-card hand zone, manual skill bar during combat, sci-fi circuit board, holographic UI, dark gothic, gritty, dirty, low-poly, pixel art, plastic, watermark, signature, logo, garbled text, heavy text overlay, full-screen explosion covering the disc, watermark 'AI generated' in corner"
```

---

## 6. 关键纪律（保持一致性）

1. **阵盘永远是焦点而非主体**：在场景里，它的位置/光效突出，但体量克制，永远不抢建筑、角色或 UI 信息的戏。
2. **三层不混用**：氛围层（破晓金光）= 厚涂写实；交互层（虹彩阵盘）= 水晶/玉质；信息层（数字/UI）= 硬边清晰。禁止把阵盘的虹彩纹用在 UI 数字上。
3. **虹彩是配角不是主色**：虹彩光只能出现在阵盘表面、灵脉流光、护体玉璧三处。一旦虹彩泛滥，整个画面就退化为"80 年代酒店大堂"。
4. **金边厚度统一**：所有阵盘边缘的金色高光粗细/亮度必须一致，靠 STYLE_ANCHOR_DISC 中的 "thin warm-gold edge highlight" 锁定。
5. **悬浮高度统一**：阵盘距下方青铜底座约等于"一层环厚"——既明确"浮空"又不让它变成"空中飞碟"。
6. **背光不打散**：阵盘只在破晓/黄昏的金光下最美；正午顶光下会失去虹彩柔光，禁用。

---

## 7. 与既有提示词文档的关系

- 本文档为 `output/imagegen/buqi-image-prompts.md`（v0.1，水墨机关修仙）的**风格替换附件**。
- 若用户最终选择此方向，应将主文档的 STYLE_ANCHOR 整体替换为本文档的 `STYLE_ANCHOR_DISC`。
- A 组 18 张效果图、B 组 UI 资产、C 组战斗效果、D 组场景角色等镜头都应按本文档第 4 节变体重做。
- 建议下一步：用本文档提示词重出 4 张关键场景（01 主菜单 / 10 周天盘 / 12 战斗 / 13 连锁高潮）做完整闭环验证，确认阵盘风格在游戏画面里真的立得住。

---

*本提示词集基于用户选定的具体视觉元素提炼，生成效果如出现"虹彩滥用""金边过粗""悬浮失重"等飘移，优先回查第 6 节纪律与第 5 节负面词。*