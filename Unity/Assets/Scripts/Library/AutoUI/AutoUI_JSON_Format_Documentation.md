### AutoUI 导出的JSON文件格式详解

JSON文件的根对象是一个代表PSD画布或主图层的 `Layer` 对象。整个结构是递归的，一个 `Layer` 对象内部可以包含一个 `layers` 数组，形成层级树。

#### 根对象 (Root Layer)

JSON的顶层就是一个 `Layer` 对象，通常是 `canvas` 类型。

```json
{
  "name": "YourPSDFilename",
  "layerKind": "canvas",
  "visible": true,
  "opacity": 1.0,
  "rectTransform": { ... },
  "canvasLayerData": {
    "renderMode": "overlay", // overlay = RenderMode.ScreenSpaceOverlay; camera = RenderMode.ScreenSpaceCamera; worldSpace = RenderMode.WorldSpace
    "width": 1920,
    "height": 1080
  },
  "layers": [
    ... // 子图层
  ]
}
```

#### `Layer` 对象 (核心结构)

每个图层，无论类型，都由一个 `Layer` 对象表示。

| 字段名 | 类型 | 描述 |
| :--- | :--- | :--- |
| `name` | string | 图层在PS中的名称。**非常重要**，用于资源匹配和组件识别。 |
| `layerKind` | string | 图层类型。可为: `"group"`, `"smartObject"`, `"pixel"`, `"text"`, `"canvas"`。 |
| `visible` | boolean | 图层的可见性。 |
| `opacity` | number | 图层的不透明度 (0.0 - 1.0)。 |
| `rectTransform` | object | 包含位置、尺寸、锚点等布局信息。见下文。 |
| `layers` | array | **仅当 `layerKind` 为 "group" 或 "canvas" 时存在**。包含所有子图层的 `Layer` 对象数组。 |
| `pixelLayerData` | object | **仅当 `layerKind` 为 "pixel" 时存在**。图片图层的特定数据。 |
| `textLayerData` | object | **仅当 `layerKind` 为 "text" 时存在**。文本图层的特定数据。 |
| `smartObjectLayerData` | object | **仅当 `layerKind` 为 "smartObject" 时存在**。智能对象图层的特定数据。 |
| `canvasLayerData` | object | **仅当 `layerKind` 为 "canvas" 时存在**。画布/根图层的特定数据。 |
| `components` | array | **可选**。一个组件列表，用于为该图层添加额外功能（如按钮、布局等）。见下文。 |

---

#### 1. `rectTransform` 对象 (布局)

所有可见图层都包含此对象，定义了其在UI中的布局。

| 字段名 | 类型 | 描述 |
| :--- | :--- | :--- |
| `anchor` | array | 锚点。一个包含两个 `NormalizedPoint` 对象的数组，分别代表 `anchorMin` 和 `anchorMax`。 |
| `pivot` | object | 轴心点。一个 `NormalizedPoint` 对象。 |
| `anchoredPosition` | object | 锚定位置 (x, y)。一个 `NormalizedPoint` 对象。 |
| `sizeDelta` | object | 尺寸增量 (width, height)。一个 `NormalizedPoint` 对象。 |

**`NormalizedPoint` 对象格式:**
```json
{ "x": 0.5, "y": 0.5 }
```

---

#### 2. `layerKind` 详解

##### 2.1. `pixel` 和 `smartObject` (图片)
这两种类型都被处理为图片。Unity会使用图层的 `name` 字段去项目中查找同名的图片资源，并创建一个 `Image` 组件。

```json
{
  "name": "Icon_Settings",
  "layerKind": "pixel",
  "rectTransform": { ... },
  "pixelLayerData": {
    "kind": "pixel" // 内部类型，固定值
  }
}
```

##### 2.2. `text` (文本)
处理为 `TextMeshProUGUI` 组件。

```json
{
  "name": "Label_PlayerName",
  "layerKind": "text",
  "rectTransform": { ... },
  "textLayerData": {
    "kind": "text", // 固定值
    "text": "玩家名称",
    "fontSize": 24,
    "color": { "r": 255, "g": 255, "b": 255 },
    "textAlign": "left", // "left", "center", "right"
    "haveShadow": false, // 是否有描边/阴影
    "warp": true, // 是否自动换行
    "rotation": 0.0 // 旋转角度
  }
}
```

##### 2.3. `group` (容器/组)
用于组织图层。它本身不直接显示，但可以附加功能性组件。

```json
{
  "name": "Panel_Options",
  "layerKind": "group",
  "rectTransform": { ... },
  "layers": [ ... ], // 包含子图层
  "components": [ ... ] // 可以附加组件
}
```

---

#### 3. `components` 数组 (核心功能)

这是此工具最强大的部分。通过在PS中对图层进行特定命名（脚本会将其解析为 `components` 数组），可以为生成的对象添加Unity组件。

`components` 是一个对象数组，每个对象代表一个要添加的组件。

```json
"components": [
  {
    "name": "button",
    "parameters": {}
  }
]
```

| `name` 值 | 对应Unity组件/功能 | `parameters` (参数) | 示例 |
| :--- | :--- | :--- | :--- |
| `button` | `UnityEngine.UI.Button` | (空) | `{ "name": "button", "parameters": {} }` |
| `grid` | `GridLayoutGroup` | 自动推断，无需参数 | `{ "name": "grid", "parameters": {} }` |
| `horizontalLayout` | `HorizontalLayoutGroup` | 自动推断，无需参数 | `{ "name": "horizontalLayout", "parameters": {} }` |
| `verticalLayout` | `VerticalLayoutGroup` | 自动推断，无需参数 | `{ "name": "verticalLayout", "parameters": {} }` |
| `title` | (文本样式) | (空) | `{ "name": "title", "parameters": {} }` |
| `prefab` | 实例化另一个Prefab | `{"name": "PrefabNameToLoad"}` | `{ "name": "prefab", "parameters": {"name": "CommonItem"} }` |

