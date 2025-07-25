# CodeBind for AI Agents: Interaction Guide (v2)

This document outlines the rules and patterns for interacting with the CodeBind system. It's designed to be understood by an AI agent to automate component binding in Unity.

## 1. Core Concepts

CodeBind is a tool that automates the process of linking scene GameObjects and Components to C# scripts. It saves developers from writing repetitive `GetComponent` or `Find` calls. It uses two main approaches: one for standard C# classes (`ICSCodeBind`) and one for `MonoBehaviour` scripts (`MonoCodeBind`).

## 2. `MonoCodeBind` for `MonoBehaviour` Scripts

This is the most common and powerful way to use CodeBind for scripts attached to GameObjects.

### 2.1. How it Works

1.  **Add the Attribute:** Add the `[MonoCodeBind('_')]` attribute to a `partial class` that inherits from `MonoBehaviour`. The character passed to the attribute is the separator used in the naming convention.
2.  **Naming Convention:** In the Unity scene, name the child GameObjects of the `MonoBehaviour`'s GameObject according to a specific convention.
3.  **Code Generation:** The CodeBind tool automatically generates a `.Bind.cs` partial class. This file contains `[SerializeField]` private fields to hold the references and public properties to access them.

### 2.2. Detailed Naming Rules

- **Structure:** `VariableName_ComponentType1_ComponentType2_...`

- **`VariableName`:** The part of the name **before** the first separator. This determines the base name of the generated C# properties.

- **`ComponentType`:** The parts of the name **after** the first separator. Each part specifies a component type to bind.
    - If this part is omitted, it defaults to binding the `GameObject` itself.
    - You can specify multiple component types, separated by the same separator.

- **Wildcard (`*`)**: If the component type section is a single `*`, CodeBind will generate properties for the `GameObject` and **all** components attached to it.

- **Arrays**: To create an array of bindings, create multiple child GameObjects with the exact same `VariableName_ComponentType` prefix. Unity's automatic renaming (e.g., adding ` (1)`, ` (2)`) is recognized by the tool.

### 2.3. Naming Convention Examples

Assume a `PlayerController` script with `[MonoCodeBind('_')]` has a child GameObject with the following names:

- `Head_Transform`: Creates a `public Transform HeadTransform { get; }` property.
- `Gun_SpriteRenderer_AudioSource`: Creates two properties: `public SpriteRenderer GunSpriteRenderer { get; }` and `public AudioSource GunAudioSource { get; }`.
- `StatsPanel`: Creates a `public GameObject StatsPanel { get; }` property.
- `Enemy_*`: Creates properties for the `GameObject` and every component on the `Enemy` object (e.g., `EnemyGameObject`, `EnemyTransform`, `EnemyRigidbody`, `EnemyEnemyAI`).
- `Bullets_Transform`, `Bullets_Transform (1)`, `Bullets_Transform (2)`: Creates a `public Transform[] BulletsTransformArray { get; }` property containing the `Transform` of all three objects.

### 2.4. AI Interaction Rules

- **To create a binding:**
    1.  Use `manage_gameobject` to create a child GameObject under the target `MonoBehaviour`'s object.
    2.  Name the child GameObject using the `VariableName_ComponentType1_ComponentType2` format.
- **To access a binding:**
    1.  The AI can assume that a public property exists on the `MonoBehaviour` script. The name will be the `VariableName` (PascalCase) followed by the `ComponentType` (if specified).
    2.  For example, to access the `AudioSource` from the `Gun_SpriteRenderer_AudioSource` object, the AI can use `playerController.GunAudioSource`.

## 3. `ICSCodeBind` for Plain C# Classes

This method is used when you have a C# class that isn't a `MonoBehaviour` but still needs references to scene objects.

### 3.1. How it Works

1.  **Implement the Interface:** The C# class must be `partial` and implement the `CodeBind.ICSCodeBind` interface.
2.  **Add the `CSCodeBindMono` Component:** In the scene, add the `CSCodeBindMono` component to a GameObject.
3.  **Drag and Drop:** Drag the GameObjects/Components you want to bind into the `BindComponents` list on the `CSCodeBindMono` component in the Inspector.
4.  **Code Generation:** A `.Bind.cs` partial class is generated. It implements the `InitBind` method, which populates properties by casting objects from the `BindComponents` list based on their order.

### 3.2. AI Interaction Rules

- **To create a binding:**
    1.  Use `manage_gameobject` to find the GameObject with the `CSCodeBindMono` component.
    2.  Use `manage_gameobject` with the `set_component_property` action to modify the `BindComponents` array on the `CSCodeBindMono` component.
- **To access a binding:**
    1.  The AI can assume that public properties exist on the C# class. The names of these properties are determined by the names of the GameObjects dragged into the `CSCodeBindMono` component's list.
    2.  The order is critical.

## 4. General AI Strategy

- **Prefer `MonoCodeBind`:** For any script that is a `MonoBehaviour`, the `MonoCodeBind` attribute method is strongly preferred.
- **Triggering Code Generation:** After creating or renaming GameObjects for `MonoCodeBind`, the AI may need to trigger a recompilation in Unity for the `.Bind.cs` files to be generated or updated. This can often be done by creating or modifying a dummy script file.