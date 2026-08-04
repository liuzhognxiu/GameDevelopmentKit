# Session State: 星期八

## Current Task

Core concept recorded. CCGS-style feasibility discussion completed through three focused role lenses. Phase-one scope approved on 2026-08-04.

## Locked Core

`星期八` is an asynchronous PvP auto-building roguelite.

Plain-language core:

> The player does not manually play cards during combat.
> The player buys, upgrades, modifies, and arranges a set of components before combat.
> When combat starts, those components run automatically.
> The result depends on what the player built, how the pieces trigger together, and which side effects the player accepted.

Shortest pitch:

> Build an automatic card machine, then let it clash with another player's saved machine.

## Design Boundary

- Core gameplay comes first; modern workplace / `星期八` lore is presentation and meaning.
- Avoid traditional hand-management deckbuilder combat as the primary loop.
- Borrow the broad structure of `The Bazaar`: run-based shop/event growth, automatic combat, asynchronous opponent snapshots.
- Differentiate through cards/components that can be modified by stamps/annotations and through side-effect debt/noise.

## Locked Phase One

- Battle presentation: continuous real-time cooldowns; no combat input.
- Simulation requirement: deterministic and replayable despite continuous presentation.
- Board: 8 cells; small items use 1, medium items use 2, and large items use 3.
- Run target: 8-12 minutes; gain 5 seat stamps before losing 3 authority points.
- Content: 18 items, 6 stamps/annotations, 3 build directions, 3 shop types, 6 events, and 12 offline ghost snapshots.
- Build directions: fast execution, buffer retaliation, and chained operation.
- Battle target: 30-45 seconds; overtime begins at 45 seconds; hard cap at 60 seconds.
- Basic battle effects: damage, buffer, haste, delay, charge, and noise.
- Online PvP, multiple characters, separate skills, rankings, seasons, and heavy art remain out of scope.

Approved scope document:

- `docs/superpowers/specs/2026-08-04-week-eight-phase-one-design.md`

Decision precedence:

- This approved scope overrides conflicting counts or boundaries in earlier unapproved v0.2 gameplay, battle-contract, and prototype drafts.
- Compatible deterministic rules, debug tooling, and staged validation ideas from those drafts may be reused only after they are checked against the approved scope.

## Current Working Terms

| Generic Concept | 星期八 Skin |
|---|---|
| Component / item / card | 事项卡 |
| Upgrade / modifier | 批注 / 盖章 |
| Side effect / curse | 噪音 / 债务 |
| Opponent snapshot | 影子档案 |
| Win token | 席位章 |
| Life / elimination resource | 权限值 |

## Next Step

Integrated role verdict:

1. Game Designer: CONCERNS. Core stands, but fun depends on readable automatic combat and meaningful build changes.
2. Technical Director: GO. Implement in Unity, with a pure C# deterministic simulation core and offline ghost snapshots first.
3. Producer: CONCERNS. Solo-feasible only if scope stays tight: no real server, no real-time PvP, no heavy art, no large narrative in MVP.

Recommended next deep-dive:

- Define the battle rules contract: time, trigger priority, targeting, overtime, ties, and randomness.
- Then define the approved first content set: 18 items, 6 modifiers, and 3 baseline builds.
- After the design is locked, define DTOs, deterministic logs, local ghost snapshot format, and the minimal Unity integration path.
