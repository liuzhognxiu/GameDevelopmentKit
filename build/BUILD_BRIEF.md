# Buqi Demo Final Build Brief

## Product Goal

Deliver a playable Unity Editor demo of Buqi for PC landscape input. The run lasts nine days. Every day advances through Morning Operation, Noon Operation, Dusk PVE, and Night local-preset asynchronous PVP. After Day 9 Night, the player chooses one of three Tribulation routes, completes three Tribulation stages, and reaches one run ending.

## Approved Sources

- `Design/GAME_DESIGN.md`
- `docs/superpowers/specs/2026-08-07-buqi-day-run-demo-design.md`
- Final 7/7 interaction approval delegated from task `019fd773-fa40-7332-af76-648a75e58381`

The final approval overrides earlier six-day or three-encounter loop descriptions.

## Required Experience

- Operation choice is a standalone three-choice screen with the current cycle board always visible.
- Item details open only from pointer hover or mobile long press.
- Bazaar products use hover/long-press details. There is no shelf lock or sell button. Dragging an owned board item over the upper stall previews its refund; dropping sells atomically.
- PVE preparation hides the phase rail and storage, shows exactly Initial/Advanced/Dangerous choices with threat and reward, and starts battle immediately on selection. The board is read-only.
- Replay has no center event report and no pause. It exposes only 1x, 2x, and Skip to Result. Attack, guard, heal, and damage feedback floats over the responsible board item. Builds cannot be edited during replay.
- Day record is an optional prompt opened from a button, with a first-time automatic display allowed. It is not a required daily settlement page.
- Tribulation route choice appears only after Day 9 Night: Embrace Thunder, Shatter Artifacts, or Borrow the Tribulation through Heart. The third route spends Dao seals to adjust the current omen. There is no three-day echo/history mechanic.
- PVP uses randomized local preset players. Network matchmaking and upload are excluded.

## Behavioral Invariants

- Replay close/confirmation is the only event that advances battle settlement presentation.
- Save restore is fail-closed and settlement is exactly once.
- Reloading after a settled PVE battle returns to the PVE battle summary before PVP and does not resimulate or duplicate rewards.
- Invalid commands do not advance phase, RNG, revision, currency, rewards, or settlement IDs.
- Player-facing strings remain compatible with the existing localization pipeline; this build does not replace localization work owned by task `019fdfbe-80c3-7b43-942d-d35a381657bb`.

## Scope Exclusions

- Online PVP, matchmaking, uploads, shelf locking, click-open item details, replay pause, replay-time deployment, six-day flow, three-day echo/history, and mandatory daily settlement pages.

## Toolchain

- Target platform/runtime: Unity Editor playable demo on Windows PC.
- Tested runtime: Unity `6000.3.21f1` through Unity AgentBridge.
- Runtime: .NET 8 and Unity Mono/HybridCLR project assemblies.
- Install: existing repository and Unity package cache; no new package installation planned.
- Build/compile: Unity AgentBridge `recompile` plus `get_compile_result`.
- Tests: Unity EditMode `Game.Hot.Buqi.Tests`, focused feature tests, and headless simulation stress.
- Evidence: `qa/evidence/verify.log`, `qa/verification.json`, and JPEG screenshots under `qa/evidence/`.

## Required Verification Suites

- `buqi-focused-editmode`: new nine-day, economy interaction, PVE difficulty, replay, builder, save/reload, and idempotency tests.
- `buqi-full-editmode`: all `Game.Hot.Buqi.Tests` EditMode tests.
- `buqi-headless-stress`: 200 deterministic simulation runs.
- `buqi-real-run`: main menu to one full day, Day 9 Night to route choice, three Tribulation stages to ending, restart/reload, and settled-PVE reload.
- `buqi-localization-visual`: no `<NoKey>` in required screens and no new Unity Console errors.

## Completion Evidence

The authoritative verification command/output and checkpoints will be recorded in `qa/verification.json` and `qa/evidence/verify.log`. Each real UI checkpoint will include both an observable state description and a JPEG path. Any target behavior not executed in Unity will be recorded as `NOT_RUN` rather than inferred from tests.
