# Unity MCP for AI Agents: Interaction Guide

This document outlines the rules and patterns for interacting with the Unity MCP (Model-View-Controller-Presenter) system. It's designed to be understood by an AI agent to control the Unity Editor programmatically.

## 1. Core Concepts

### 1.1. Command Structure

All interactions are done by sending JSON commands over a TCP socket. The core MCP bridge listens for these commands and dispatches them to the appropriate tool.

The general command structure is:

```json
{
  "type": "tool_name",
  "params": {
    "action": "action_to_perform",
    ...other parameters
  }
}
```

- `type`: The name of the tool to use (e.g., `manage_gameobject`, `manage_asset`).
- `params`: A dictionary of parameters for the tool.
- `action`: The specific operation to perform within the tool.

### 1.2. Available Tools

The following tools are available:

- `manage_gameobject`: Create, find, modify, and delete GameObjects and their components in the current scene.
- `manage_asset`: Create, find, modify, and delete assets (Prefabs, Materials, etc.) in the project.
- `manage_scene`: Load, save, and get information about scenes.
- `manage_editor`: Control the editor state (play, pause, etc.).
- `manage_script`: Create and modify C# scripts.
- `manage_shader`: Create and modify shader files.
- `read_console`: Read log messages from the Unity console.
- `execute_menu_item`: Execute a Unity menu item by its path.

## 2. The `manage_gameobject` Tool

This is the most powerful and complex tool for scene manipulation.

### 2.1. Actions

- `create`: Create a new GameObject.
- `find`: Find one or more GameObjects in the scene.
- `modify`: Modify properties of an existing GameObject.
- `delete`: Delete a GameObject.
- `add_component`: Add a component to a GameObject.
- `remove_component`: Remove a component from a GameObject.
- `set_component_property`: Set specific properties on a component.
- `get_components`: Get a list of all components on a GameObject.

### 2.2. Targeting GameObjects

To modify, delete, or interact with a GameObject, you must first target it. This is done using the `target` and `searchMethod` parameters.

- `target`: The identifier for the object. This can be its name (string), instance ID (int), or scene path (string).
- `searchMethod`: How to use the `target` identifier.

**Available `searchMethod` values:**

- `by_name`: (Default) Find objects by their exact name.
- `by_id`: Find an object by its unique `instanceID`.
- `by_path`: Find an object by its path in the scene hierarchy (e.g., "Parent/Child/Grandchild").
- `by_tag`: Find objects with a specific tag.
- `by_layer`: Find objects on a specific layer.
- `by_component`: Find objects that have a specific component attached.
- `selected`: Get the currently selected GameObject(s) in the editor.
- `by_id_or_name_or_path`: A flexible internal method that tries to find by ID, then path, then name.

**Example: Finding a GameObject**

```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "find",
    "target": "Player",
    "searchMethod": "by_name",
    "findAll": true
  }
}
```

### 2.3. Component Manipulation

#### Component Names

When adding, removing, or modifying components, you need to provide the component's type name. You can usually use the short name (e.g., `BoxCollider`, `Rigidbody`). If there's an ambiguity, use the full name (e.g., `UnityEngine.BoxCollider`).

#### Setting Properties (`set_component_property` and `modify`)

You can set component properties when creating or modifying a GameObject. The `componentProperties` parameter is a dictionary where keys are component names and values are dictionaries of property-value pairs.

**Example: Creating a Cube with a modified Rigidbody**

```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "create",
    "name": "FallingCube",
    "primitiveType": "Cube",
    "componentsToAdd": [
      {
        "typeName": "Rigidbody",
        "properties": {
          "mass": 25,
          "useGravity": true,
          "drag": 0.5
        }
      }
    ]
  }
}
```

**Example: Modifying an existing object's properties**

```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "modify",
    "target": "Player",
    "componentProperties": {
      "Light": {
        "intensity": 2.5,
        "color": [1, 0.8, 0.5, 1]
      }
    }
  }
}
```

### 2.4. Advanced Property Setting

The system uses reflection and can handle complex scenarios.

#### Nested Properties

You can set properties on nested objects (like a material) using dot notation.

**Example: Changing the color of a material on a MeshRenderer**

```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "set_component_property",
    "target": "MyObject",
    "componentName": "MeshRenderer",
    "componentProperties": {
      "sharedMaterial.color": [1, 0, 0, 1]
    }
  }
}
```

This also works for arrays, like the `materials` array on a renderer.

**Example: Changing the color of the second material**

```json
{
  "type": "manage_gameobject",
  "params": {
    "action": "set_component_property",
    "target": "MyComplexObject",
    "componentName": "MeshRenderer",
    "componentProperties": {
      "materials[1].color": [0, 1, 0, 1]
    }
  }
}
```

#### Object References

To assign a reference to another object (e.g., setting a `target` field on a script, or assigning a material), you can use two methods:

1.  **By Asset Path (for assets like Materials, Prefabs):** Provide the path to the asset in the project.

    **Example: Assigning a material to a Renderer**

    ```json
    {
      "type": "manage_gameobject",
      "params": {
        "action": "set_component_property",
        "target": "MyObject",
        "componentName": "MeshRenderer",
        "componentProperties": {
          "sharedMaterial": "Assets/Materials/MyShinyMaterial.mat"
        }
      }
    }
    ```

2.  **By Scene Search (for scene objects/components):** Provide a dictionary with `find`, `method`, and optionally `component` keys.

    **Example: Assigning another GameObject to a script's `target` field**

    ```json
    {
      "type": "manage_gameobject",
      "params": {
        "action": "set_component_property",
        "target": "EnemyAI",
        "componentName": "EnemyFollowScript",
        "componentProperties": {
          "playerTarget": {
            "find": "PlayerShip",
            "method": "by_name"
          }
        }
      }
    }
    ```

    **Example: Assigning a component reference (like a Rigidbody) to a script**

    ```json
    {
      "type": "manage_gameobject",
      "params": {
        "action": "set_component_property",
        "target": "CollisionHandler",
        "componentName": "MyCollisionScript",
        "componentProperties": {
          "playerRigidbody": {
            "find": "PlayerShip",
            "method": "by_name",
            "component": "Rigidbody"
          }
        }
      }
    }
    ```

By following these rules, an AI agent can effectively and precisely control the Unity Editor through the MCP bridge.
