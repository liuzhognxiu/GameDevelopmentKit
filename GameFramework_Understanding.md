# Game Framework (GF) Understanding

This document outlines my current understanding of the Game Framework (GF) architecture, as it pertains to this project.

## Core Philosophy

The core philosophy of GF is **data-driven** and **component-based**.

-   **Data-Driven:** Game configuration and data (e.g., entity properties, UI text, level data) are stored in external data tables (like the Excel files in `Design/Excel`). The game loads this data at runtime, which allows for easy modification of game content without changing the code.
-   **Component-Based:** Game objects (Entities) are built by composing smaller, reusable components (`EntityLogic`). Each component is responsible for a specific aspect of the entity's behavior (e.g., movement, combat, UI). This promotes modularity and reusability.

## Key Modules

GF is composed of several key modules that handle different aspects of the game:

### 1. Entity Manager
-   The heart of the gameplay system. It manages the lifecycle of all entities in the game.
-   **Entity:** A container for components. It has a unique ID.
-   **EntityLogic:** The base class for all components that can be attached to an entity. This is where the actual game logic is implemented. (e.g., `Bullet.cs` is an `EntityLogic`).

### 2. Data Table Manager
-   Responsible for loading and managing the data from the Excel tables.
-   It parses the tables and provides an API to access the data in a structured way.
-   The `gen all` script likely uses this module (or a related tool) to generate C# classes from the tables.

### 3. Procedure Manager
-   A finite state machine that manages the overall game flow.
-   The game is always in a specific "procedure" (e.g., `ProcedureMenu`, `ProcedureMain`, `ProcedureGameOver`).
-   Each procedure is responsible for the logic of that specific game state.

### 4. Event Manager
-   A decoupled event system that allows different parts of the game to communicate without having direct references to each other.
-   This is useful for things like notifying the UI of a change in the game state, or for triggering sound effects.

### 5. Resource Manager
-   Manages the loading and unloading of all game assets (prefabs, textures, sounds, etc.).
-   It provides a unified interface for loading resources, whether they are in the local project, in an asset bundle, or on a remote server.

### 6. UI Manager
-   Manages the game's user interface.
-   It handles the opening, closing, and stacking of UI forms (`UIForm`).

## Hot-Update
-   The project has a `Hot` directory, which indicates that it uses a hot-update solution.
-   This means that a significant portion of the game logic (especially the gameplay logic in the `Hot` directory) can be updated without requiring a full rebuild and re-installation of the game client.

## Typical Workflow
1.  **Data Tables:** Define game data in Excel.
2.  **Code Generation:** Run `gen all` to generate C# classes from the tables.
3.  **Entity Definition:** Define entities in the `Entity.xlsx` table, specifying their components and assets.
4.  **Entity Logic:** Write the logic for each component in C# scripts that inherit from `EntityLogic`.
5.  **Procedures:** Implement the game flow by creating different procedures.
6.  **UI:** Create UI forms and the logic to interact with them.
7.  **Events:** Use the event system to communicate between different modules.

This understanding will serve as the foundation for our future development work.
