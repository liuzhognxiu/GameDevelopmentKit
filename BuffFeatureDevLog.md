# Buff Feature Development Log

This document tracks the development process for the "Seeking Bullet Buff" feature.

## Feature Request

The user requested to implement a feature where a plane, upon colliding with a specific prefab, gains a "seeking bullet" buff for 10 seconds.

## Development Steps Taken

### 1. Resource Identification
- The user created a new prefab for the buff.
- The prefab is located at: `g:\GameDevelopmentKitAI\Unity\Assets\Res\Entity\Buff.prefab`.

### 2. Table Configuration

#### Entity Table (`Entity.xlsx`)
- Added a new entity for the buff prefab with the following details:
  - **ID:** 80000
  - **CSName:** SeekingBulletBuff
  - **Desc:** 追踪子弹Buff
  - **AssetName:** Buff
  - **EntityGroupName:** Buff
  - **Priority:** 50

#### Enum Table (`__enums__.xlsx`)
- Created a new enum `BulletBuffType` with the following member:
  - **Name:** Seeking
  - **Alias:** 追踪
  - **Value:** 0
  - **Comment:** Grants seeking bullets
  - **Unique:** TRUE

### 3. Established Development Workflow

A new standard development workflow was established and agreed upon:

1.  **Resources:** Prepare prefabs and other assets.
2.  **Entity Table:** Add new entries to `Entity.xlsx`.
3.  **Enum Table:** Add new enums or members to `__enums__.xlsx`.
4.  **Data Generation:** The user is notified to run `gen all`.
5.  **Compilation:** The user compiles the project.
6.  **Scripting:** The AI (Gemini) writes the C# logic only after the above steps are complete.

### Update (2025-08-14)

- **Error during `gen all`:** The user reported an error: "the `flags` field cannot be empty".
- **Correction:** Added `flags=FALSE` to the `BulletBuffType` enum definition in `__enums__.xlsx`. The `flags` field indicates whether an enum can be used as a bitmask.

## Implementation

Implemented the buff system using a component-based design aligned with Game Framework.

- **`Buff.cs`:** Created an abstract base class for all buffs.
- **`SeekingBulletBuff.cs`:** Created the specific logic for the seeking bullet buff. It overrides the weapon's bullet type.
- **`BuffComponent.cs`:** Created an `EntityLogic` component to manage buffs on an entity.
- **`BuffPickupLogic.cs`:** Created an `EntityLogic` for the buff prefab to handle collision and apply the buff.
- **`Aircraft.cs`:** Exposed the `Weapons` list to allow buffs to access it.
- **`MyAircraft.cs`:** Attached the `BuffComponent` to the player's aircraft.
- **`Weapon.cs`:** Refactored to allow the bullet type to be overridden by buffs.

## Bug Fixes (2025-08-14)

- Fixed a series of compiler errors after the initial implementation.
- **`Entity` vs `EntityLogic`:** Corrected the usage of `Game.Hot.Entity` and `UnityGameFramework.Runtime.Entity` in the buff scripts. `Game.Hot.Entity` is the `EntityLogic` in the hot-fix layer.
- **`WeaponData.BulletType`:** The `BulletType` property was not being generated in `WeaponData.cs`. The user was notified to fix the `gen all` process. After the user confirmed the fix, the `WeaponData.cs` was updated to use the new property.
- **`MovementStrategyType` vs `BulletType`:** Corrected the code to use the `BulletType` enum instead of `MovementStrategyType` when dealing with bullet data.

## Buff Spawning

- **Requirement:** The user requested that the buff pickup should be spawned every time an enemy is destroyed.
- **Implementation:** Modified the `OnDead` method in `Aircraft.cs`. It now checks if the dying aircraft is not a `MyAircraft` (i.e., an enemy) and, if so, spawns the buff pickup entity at the enemy's position.

### Update (2025-08-14) - Part 2

- **New Rule:** The user clarified that entity `Data` classes (e.g., `BuffData.cs`) are not auto-generated and must be created manually. This rule has been saved to permanent memory.
- **Implementation:**
    - Created `BuffData.cs` manually.
    - Corrected the `OnDead` method in `Aircraft.cs` to use the new `BuffData` class to spawn the buff pickup.

### Update (2025-08-14) - Part 3

- **Created `Buff.xlsx`:** Created a new data table for buffs with the following columns: `Id`, `Duration`, `BuffType`.
- **Added Buff Data:** Added the "Seeking Bullet Buff" to the `Buff.xlsx` table with ID `80000`, duration `10`, and `BuffType` `Seeking`.

### Update (2025-08-14) - Part 4

- **Corrected `Buff.xlsx`:** Deleted the default "Sheet1" as it's not needed for data generation. This rule has been saved to permanent memory.

### Update (2025-08-14) - Part 5

- **New Rule:** After adding a new table, it must be registered in `__tables__.xlsx`. This rule has been saved to permanent memory.
- **Implementation:** Registered `Buff.xlsx` in `__tables__.xlsx`.

### Update (2025-08-14) - Part 6

- **`Buff.xlsx` First Column Issue:** Encountered a tool limitation where `apply_formula` could not write literal strings starting with `#`. The user manually corrected the file.
- **Finalized `BuffData.cs`:** After the user ran `gen all` and the `DRBuff` class was generated, `BuffData.cs` was updated to read data from the new data table class, following the project's data-driven design pattern.

## Final Steps

- All coding and bug fixing is complete. The feature is ready for testing.
