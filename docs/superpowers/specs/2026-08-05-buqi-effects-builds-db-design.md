# Buqi Effects And Builds DB Expansion Design

Date: 2026-08-05  
Status: Draft for review  
Scope: approach B, expand effect vocabulary and build coverage while keeping the current Buqi automatic battle shape.

## Context

The current Buqi content chain has a small Step 3 slice: 9 enabled items, 3 refinements, 3 build enum values (`fast`, `buffer`, `chain`), and 6 echo snapshots. The battle core supports 6 effects: `Damage`, `Buffer`, `Haste`, `Delay`, `Charge`, and `Noise`.

The requested expansion is not only a packaging rename. `攻击` and `护盾` can safely replace public-facing `Damage` and `Buffer`, but `治疗`, `生命恢复`, `中毒`, `灼烧`, and `冰冻` require new battle semantics, new validation rules, new log reasons, and new content budgets.

Reference principles from The Bazaar: async board-vs-board combat, item sizes, tags/types, cooldown-based item actions, and status families such as Damage, Shield, Heal, Poison, Burn, Freeze, Haste, Slow, and Charge. This design uses those principles as genre references, not as copied content.

## Goals

1. Replace abstract public terms with readable battle words.
2. Expand from 3 builds to 8 build families.
3. Add enough DB shape for designers to author items, builds, decks, and echoes without hard-coded count gates.
4. Preserve deterministic battle simulation, board size, item size tradeoffs, and post-battle explainability.
5. Keep the first implementation slice small enough to validate before recording a full large card pool.

## Non-Goals

No real-time PvP, no server ghost upload, no hidden win-rate tuning, no random battle targeting, no traditional hand/deck draw system, no critical hit, ammo, destroy, summon, steal, or hero-specific card pools in this expansion.

## Player-Facing Term Migration

| Current/internal concept | New player-facing term | Migration rule |
|---|---|---|
| Execution | 生命 | Rename in UI and battle summary. Keep numeric win condition. |
| Damage | 攻击 | Public text says attack. Engine may keep `Damage` as compatibility alias. |
| Buffer | 护盾 | Public text says shield. Engine may keep `Buffer` as compatibility alias. |
| Haste | 加速 | Keep as support term. |
| Delay | 减速 | Public text says slow. Engine may keep `Delay` as compatibility alias. |
| Charge | 充能 | Keep as build resource. |
| Noise | 过载 | Rename risk meter; old noise accident becomes overdrive accident. |
| BufferLost | 护盾破裂 | Rename condition and log summary. |
| OnUse | 自动触发 | Card text should say “触发时”. |
| OnBattleStart | 开战时 | Card text should say “开战时”. |
| OnAdjacentUse | 相邻法门触发时 | Preserve positional clarity. |

Recommendation: keep existing enum identifiers as migration aliases for one content version, but generate all visible card text from display terms. Designers may author new content with aliases such as `Attack`, `Shield`, `Slow`, and `Overload` only after the adapter explicitly supports them.

## Expanded Effects

| Effect | Public text | Target | Timing | Core rule | Read point |
|---|---|---|---|---|---|
| Attack | 攻击 N | Enemy side | Aggregate | Shield absorbs first; leftover reduces life. | Damage source and shield absorb log. |
| Shield | 获得 N 护盾 | Self side | Aggregate before attack | Adds shield up to cap. | Attack, burn, shield break. |
| Heal | 治疗 N 生命 | Self side | Aggregate after attack, before DOT | Restores life up to max life. | Survival, healing contribution. |
| Regen | 获得 N 生命恢复，持续 T 秒 | Self side | Status tick every 1s | Heals N each status tick. | Healing-over-time contribution. |
| Poison | 施加 N 中毒，持续 T 秒 | Enemy side | Status tick every 1s | Direct life loss; shield does not absorb. | Anti-shield attrition. |
| Burn | 施加 N 灼烧，持续 T 秒 | Enemy side | Status tick every 1s | Normal damage; shield can absorb. | Shield pressure and damage contribution. |
| Freeze | 冰冻目标 T 秒 | Enemy item | PreTick cooldown gate | Frozen item does not advance cooldown. | Interrupted core item and control duration. |
| Haste | 加速目标 X%，持续 T 秒 | Item | PreTick modifier | Adds cooldown progress within cap. | Faster trigger count. |
| Slow | 减速目标 X%，持续 T 秒 | Enemy item | PreTick modifier | Reduces cooldown progress within cap. | Delayed trigger count. |
| Charge | 充能 N | Item | Declare | Adds item charge, read/consume rules unchanged. | Charge generated/consumed. |
| Overload | 过载 N | Self side | Aggregate | Cross threshold causes direct self-damage. | Risk build accident log. |

Settlement order:

1. Expire timed statuses and item modifiers.
2. Apply cooldown progress; frozen items receive zero progress.
3. Declare ready triggers.
4. Resolve charge reads/consumes.
5. Aggregate new shield.
6. Apply attack and burn damage through shield.
7. Apply heal and regen.
8. Apply poison direct life loss.
9. Apply overload accidents.
10. Check win/loss and hard cap.

This order makes each state readable: shield answers attack and burn, healing answers normal attrition, poison pressures life directly, and freeze answers cooldown-based engines.

## Status Budgets

| State | Starting value | Cap | Typical single effect | Minimum useful actions | Design reason |
|---|---:|---:|---:|---:|---|
| Life | 100 | 100 in first slice | Attack 4-16 | 8-14 hits | Keeps 30-45s fight length. |
| Shield | 0 | 60 | 6-14 | 1 | Old buffer budget remains readable. |
| Heal | none | life cap | 6-12 | 1-3 | Strong at low life but wasted at full life. |
| Regen | 0 | 20 per tick | 2-4/s for 4-6s | 2 | Slow sustain, vulnerable to burst/freeze. |
| Poison | 0 | 30 per tick | 2-5/s for 4-8s | 3 | Bypasses shield, pressures heal. |
| Burn | 0 | 30 per tick | 3-6/s for 3-6s | 2 | Pressures shield and rewards fast stacking. |
| Freeze | 0 | 2s per source | 0.5-1.5s | 1 | Control must be visible and short. |
| Overload | 0 | threshold 10 | +1 to +3 | 3-5 | Risk build keeps old noise tradeoff. |

## Build Families

| Build id | Public name | Primary effects | Core promise | Weakness |
|---|---|---|---|---|
| attack | 攻击快攻 | Attack, Haste | Kill before the opponent engine stabilizes. | Shield and freeze blunt tempo. |
| shield | 护盾反击 | Shield, Attack, ShieldBreak | Convert defense into counter pressure. | Poison bypasses shield. |
| heal | 治疗续航 | Heal, Regen, Shield | Outlast burst and win long fights. | Freeze can stop heal engines; poison pressures cap. |
| poison | 中毒消耗 | Poison, Slow | Ignore shield and force healing checks. | Fast attack can end fight before stacks mature. |
| burn | 灼烧压迫 | Burn, Attack, Haste | Stack shield-taxing DOT while attacking. | Big shield and heal can stabilize. |
| freeze | 冰冻控制 | Freeze, Slow, Attack | Stop the opponent core trigger window. | Wide small-item boards dilute targeting. |
| chain | 充能连锁 | Charge, Haste, Attack | Use adjacency to multiply item value. | Freeze or slow on the hub collapses timing. |
| overload | 过载爆发 | Overload, Attack, Shield | Accept self-risk for explosive payoff. | Long fights and poison punish self-damage. |

Build IDs should move from a closed enum-only gate to table-driven rows. The enum can remain for generated code, but validators should read enabled build ids from DB rather than hard-coded count arrays.

## Sample Decks

Each deck is a legal 8-slot board target, not a balance promise.

| Build | Deck name | Cards by role | Slot budget | Play pattern |
|---|---|---|---:|---|
| attack | 三连快攻 | S opener, S haste helper, M attack engine, M finisher, S overload spark | 7 | Many small hits plus one medium payoff. |
| shield | 玄盾返照 | S shield, S shield-break trigger, M counterattack, L shield core, S slow answer | 8 | Build shield, punish first break, survive burn. |
| heal | 回春长线 | S instant heal, S regen seed, M heal amplifier, L life furnace, S shield patch | 8 | Stabilize low life and win after overtime starts. |
| poison | 蚀脉拖局 | S poison opener, S slow, M poison jar, L plague core, S shield-bypass finisher | 8 | Stack direct loss while slowing the enemy answer. |
| burn | 燎原压迫 | S ember, M oil lamp, M burn attacker, L wildfire core | 8 | Force shield spending, then convert to attack pressure. |
| freeze | 寒镜控核 | S frost charm, M slow bell, M ice mirror, L freeze core | 8 | Identify and pause the enemy highest-value item. |
| chain | 周天传功 | S charge sender, S charge reader, M relay node, L circuit map, S backup hitter | 8 | Position matters; wrong adjacency lowers output sharply. |
| overload | 乱流爆发 | S overload spark, S safety shield, S vent valve, M burst engine, L unstable core | 8 | Spend life/risk for a short lethal window. |

## DB Shape

### Effect/Status DB

Add a status rule table with:

| Field | Purpose |
|---|---|
| EffectId | Stable id for Attack, Shield, Heal, Regen, Poison, Burn, Freeze, Haste, Slow, Charge, Overload. |
| DisplayName | Player-facing Chinese word. |
| InternalAlias | Backward-compatible engine term when needed. |
| TargetPolicy | Side, self item, adjacent item, enemy item. |
| StackPolicy | Add, refresh higher, refresh same source, cap. |
| TickInterval | Status tick cadence. |
| Cap | Max value or max duration. |
| SettlementPhase | The phase in deterministic order. |
| LogReason | Machine-readable reason family. |

### Build DB

Add or expand build rows with:

| Field | Purpose |
|---|---|
| BuildId | `attack`, `shield`, `heal`, `poison`, `burn`, `freeze`, `chain`, `overload`. |
| DisplayName | Chinese build name. |
| PrimaryEffects | 2-4 effect ids used for filtering and UI summary. |
| CounteredBy | 1-2 build ids that expose a weakness. |
| Counters | 1-2 build ids this family pressures. |
| StageGate | Exploration, Growth, Mature. |
| PublicThreatTemplate | Short pre-battle summary text key. |

### Item DB

Keep current item fields and add:

| Field | Purpose |
|---|---|
| PublicSummary | Generated or authored card summary using new terms. |
| BuildRoles | One or more build ids; an item can bridge builds. |
| StatusBudgetClass | Burst, sustain, control, risk, support. |
| StageGate | Controls when the card can appear. |
| DesignNotes | Designer-only note, not shipped text. |

First expanded content budget:

| Stage | Items | Builds | Refinements | Echo decks |
|---|---:|---:|---:|---:|
| Migration slice | 9 | 3 | 3 | 6 |
| New-effect sandbox | 18 | 6 | 6 | 12 |
| Full expanded set | 48 | 8 | 10 | 24 |

### Deck/Echo DB

Separate reusable deck recipes from opponent echoes:

| Table | Purpose |
|---|---|
| BuildDeck | Reusable sample board, starter deck, or benchmark deck. |
| Echo | Opponent wrapper that references or embeds a legal deck snapshot. |
| EchoIntel | Public preview text and key item disclosure. |

Echo difficulty should come from legal item quality, refinements, and build maturity, not hidden multipliers.

## Engineering Impact

Required implementation changes when this spec is approved for code:

1. Add new effect/status definitions to generated config schema.
2. Add runtime side statuses for regen, poison, burn, and item freeze.
3. Add max life to battle side state if healing is allowed.
4. Extend simulator settlement order and battle log reason families.
5. Replace validator hard-coded counts with stage-gated DB validation.
6. Add migration aliases for `Damage` to attack, `Buffer` to shield, `Delay` to slow, and `Noise` to overload.
7. Update headless contract checks with deterministic vectors for heal, regen, poison, burn, freeze, and old-content compatibility.
8. Regenerate Luban code and rerun headless validation.

No Excel data should be bulk-entered before the new status rules have passing deterministic tests.

## Decision Depth Table

| Visible state | Attack | Shield | Heal/Regen | Poison | Freeze |
|---|---:|---:|---:|---:|---:|
| Enemy shield 0, enemy core late, own life 80 | Best | Low | Low | Medium | Medium |
| Enemy shield 40, own life 80 | Low | Medium | Low | Best | Medium |
| Own life 25, poisoned 6/s | Medium | Low | Best | Low | Medium |
| Enemy L core triggers in 1s | Medium | Low | Low | Medium | Best |
| Enemy burn 8/s, own shield 10 | Medium | Best | Medium | Medium | Low |

The best action flips across common states, so the new effect set should not collapse into “pick largest attack”.

## Validation Plan

Functional validation:

1. Old 9-item content still produces identical results under compatibility aliases.
2. Heal never exceeds max life.
3. Regen ticks at deterministic intervals and logs each effective heal.
4. Poison bypasses shield and can be traced separately from attack.
5. Burn is absorbed by shield and can trigger shield-break conditions.
6. Frozen items do not advance cooldown and resume after expiry.
7. Haste/slow/freeze ordering is deterministic for same-tick events.
8. Stage gates reject items, builds, and echoes outside the enabled content set.
9. Echo snapshots remain legal 8-slot boards and stable hash inputs.

Playtest validation:

1. New players describe losses using public terms, not internal terms.
2. Players make at least one post-battle change that targets the shown cause.
3. Each of the first 6 builds can beat at least one representative echo and lose to at least one representative echo.
4. No build wins over 60% of the representative matrix before quality/refinement tuning.

## Risks

Healing can prolong fights; poison can make shield feel useless; freeze can feel unfair if durations are too long; build count can outrun readable UI. The mitigation is strict caps, short freeze windows, visible status tick logs, and staged content gates.

## Approval Question

Approve this design if the next step should be an implementation plan for the DB/schema/code changes. Request changes if the effect semantics or build list should be adjusted before planning.
