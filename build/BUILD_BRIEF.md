# Buqi Demo Infinite-Run Baseline Build Brief

## Product Goal

Deliver a playable Unity Editor demo of Buqi for Windows PC landscape pointer input. The run has no fixed day limit. Each day advances through six periods: Hour 1 Operation, Hour 2 Operation, Hour 3 PVE, Hour 4 Operation, Hour 5 Operation, and Hour 6 local-snapshot asynchronous PVP. After nine PVP wins, the next Hour 6 becomes a three-route, three-stage Tribulation; clearing all stages grants the tenth win and victory.

## Approved Sources

- `docs/superpowers/specs/2026-08-24-buqi-new-gameplay-baseline-design.md`
- `docs/game-concepts/buqi-run-loop-spec.md`
- `docs/game-concepts/buqi-gameplay-spec.md`
- `docs/superpowers/specs/2026-08-21-buqi-bazaar-reference-ui-interaction-flow-design.md`
- `Design/GAME_DESIGN.md` for the currently implemented build-content fixtures only; its eight-slot and older run-loop statements are superseded.

The August 24 baseline supersedes the fixed-nine-day, eight-slot Demo flow. The current implementation deliberately retains deterministic battle contract v0.6 and its existing six refinements until the separate battle-v0.7 migration is approved for implementation.

## Required Experience

- The board is a ten-slot linear space and storage has ten slots. Small, medium, and large items occupy one, two, or three contiguous slots.
- The run starts with 20 run-life. A normal PVP loss removes life equal to the current day. First depletion opens one heart trial; losing again ends the run.
- Cultivation drives nine realms. Real run days continue without a cap; authored merchant/content schedules clamp only their lookup day.
- Four operation periods offer the Bazaar-style merchant, event, training, upgrade, and refinement decisions already present in the Demo.
- Item details use hover tips. A shop may sell multiple different offers in one visit, visibly shows the remaining balance, and supports direct drag-to-sell without a separate sell button.
- The board and storage can be arranged outside battle. Dropping onto an occupied compatible target swaps the two items. Battle and preparation views remain read-only where specified.
- PVE preparation shows three difficulty choices and starts battle immediately after selection.
- Replay has no pause and no center event report. It exposes 1x, 2x, and Skip to Result; attack, shield, healing, and damage feedback appears at the responsible item.
- Battle summary confirmation opens a four-candidate reward selection. Claimed rewards, settlement, save restore, and stale-controller replay are idempotent.
- PVP opponents come from randomized local presets and player-saved local opponent data. Network matchmaking and upload are excluded.
- At nine PVP wins, the following Hour 6 shows the three Tribulation routes. Three successful stages grant win ten and a terminal victory state.
- Player-facing first-release UI is Chinese. Difficult proprietary terms use common game wording in functional UI; story packaging remains a later pass.

## Behavioral Invariants

- Replay close/confirmation is the only action that advances battle settlement presentation.
- Save restore is fail-closed and settlement is exactly once. Save schema v5 is the only accepted run schema; incompatible older saves are explicitly discarded and replaced by a clean run.
- Loading a settled battle returns to its blocking summary or reward screen without resimulation, duplicate rewards, or extra random consumption.
- Invalid commands do not advance phase, RNG, revision, currency, rewards, settlement IDs, or merchant supply state.
- The same rule version, content version, seed, snapshots, and commands produce the same battle result and log hash.
- The ten-slot shape is preserved through purchase, deployment, swap, save/restore, battle request, echo, and local opponent snapshots.

## Scope Exclusions

- Online PVP, matchmaking, uploads, real-time combat input, replay pause, replay-time deployment, shelf locking, and mandatory daily settlement pages.
- Keyboard shortcuts, controller input, and the former R-key save reset; these are deferred to the next input phase.
- Hero-selection UI, hero-specific pool filtering and combat traits, battle-contract v0.7 migration, background story, hero-specific performance, and final ending presentation.

## Toolchain and Authoritative Verification

toolchain:

- targetPlatform: Windows PC Unity project
- targetRuntime: Unity Editor playable Demo
- testedRuntime: Unity Editor 6000.3.21f1 on Windows through Unity AgentBridge
- engine: Unity
- engineVersion: 6000.3.21f1
- runtime: .NET 8 build tooling plus Unity Mono/HybridCLR project assemblies
- packageManager: Unity Package Manager / existing repository package cache
- browser: N/A

commands:

- install: NONE
- buildOrExport: `dotnet build Unity/Game.Hot.Buqi.Tests.csproj --no-restore` and `dotnet build Unity/Game.Hot.Editor.csproj --no-restore`
- start: Unity AgentBridge `play_scene`
- verify: Unity AgentBridge full `Game.Hot.Buqi.Tests` EditMode run plus `dotnet run --project Share/Buqi.Simulation.Headless/Buqi.Simulation.Headless.csproj --no-restore -- verify`

required suites:

- `buqi-focused-editmode`: infinite run, ten-slot deployment, save v5, merchant supply, reward recovery, and Tribulation contracts.
- `buqi-full-editmode`: all `Game.Hot.Buqi.Tests` EditMode tests.
- `buqi-headless-stress`: deterministic simulation verification.
- `buqi-real-run`: clean start through a visible operation/PVE/battle/reward cycle, state change, reload/restart evidence, and Console inspection.
- `buqi-localization-visual`: required screens contain no `<NoKey>` and no new Unity Console errors.

## Completion Evidence

The authoritative commands, source state, suite results, and complete-run checkpoints are recorded in `qa/verification.json` and `qa/evidence/verify.log`. Runtime screenshots are JPEG files under `qa/evidence/buqi-infinite-baseline/`. Automated state coverage may prove the full nine-win/Tribulation terminal path; real UI evidence must separately prove rendering, pointer interaction, visible state change, and restart. Any unexecuted target behavior is recorded as `NOT_RUN` rather than inferred.

## Final Scope Comparison

As of 2026-08-26, the implementation target is the approved unlimited-day, six-period, ten-slot, ten-win baseline. It keeps local PVP and the existing battle v0.6 engine, intentionally defers hero filtering and narrative packaging, and replaces all fixed-nine-day termination and eight-slot runtime assumptions in the active Demo path.
