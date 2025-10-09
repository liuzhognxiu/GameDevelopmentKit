### 快速提示词（用于LLM生成AutoUI JSON，短版）

- 任务：根据PSD层级/命名生成符合 AutoUI 的 JSON。输出为单一根 `Layer`（通常为 `canvas`），内含递归 `layers` 树。
- 严格输出结构：仅包含所需字段，布尔/数值/字符串类型正确；坐标用 `NormalizedPoint`（`{"x":num,"y":num}`）。

- 根对象（通常 `canvas`）：
  - 必含：`name`/`layerKind":"canvas"/`visible`/`opacity`/`rectTransform`/`canvasLayerData`（`renderMode":"overlay|camera|worldSpace"，width，height`）/`layers`。

- Layer 通用字段：
  - `name`（用于资源匹配，务必与 Sprite/Prefab 同名）
  - `layerKind`: `group|smartObject|pixel|text|canvas`
  - `visible`（bool），`opacity`（0-1）
  - `rectTransform`: `anchor`([min,max])、`pivot`、`anchoredPosition`、`sizeDelta`（必要时可含 `offsetMin/offsetMax`）

- 各 layerKind 规则：
  - `pixel`/`smartObject` ⇒ 生成 `Image`，需要 `pixelLayerData:{"kind":"pixel"}` 或 `smartObjectLayerData`。
  - `text` ⇒ 生成 `TextMeshProUGUI`，`textLayerData` 包含：`text`、`fontSize`、`color{r,g,b}`、`textAlign":"left|center|right"、`haveShadow`、`warp`、`rotation`。
  - `group` ⇒ 容器，可携带 `components`；`layers` 为子项。
  - `canvas` ⇒ 根容器，含 `canvasLayerData`。

- components（放在任意需要的 `Layer.components`，数组，每项 `{name, parameters}`）：
  - `button` ⇒ `UnityEngine.UI.Button`
  - `grid` ⇒ `GridLayoutGroup`（自动推断）
  - `horizontalLayout` ⇒ `HorizontalLayoutGroup`（自动推断）
  - `verticalLayout` ⇒ `VerticalLayoutGroup`（自动推断）
  - `title` ⇒ 文本样式标记
  - `prefab` ⇒ `{parameters:{"name":"PrefabName"}}`
  - `slider`/`progress`（基于 `UnityEngine.UI.Slider`）：`parameters` 支持
    - `min`/`max`/`value`（默认 0/1/0），`wholeNumbers`（默认 false）
    - `direction":"LeftToRight|RightToLeft|BottomToTop|TopToBottom`（大小写不敏感；默认 LeftToRight）
    - `interactable`（`slider` 默认 true；`progress` 默认 false）
    - `background`/`fill`/`handle`（用子节点名绑定；若缺省按关键词：background/bg，fill/bar/progress，handle/thumb）
    - 注：`fill` 绑定是必须项，缺失需给出警告；目标节点缺 `Image` 将自动补挂空 `Image`。
  - `toggle`（`UnityEngine.UI.Toggle`）：`isOn`（默认 false）、`interactable`（默认 true）、`background`、`checkmark`（同样支持关键词匹配）
  - `scroll`（`ScrollRect`）：可选 `direction":"vertical|horizontal`（默认 vertical）、`movementType":"Clamped|Elastic|Unrestricted`、`inertia`、`decelerationRate`、`elasticity`、`viewport`、`content`；
    - 若未提供 `viewport/content`，自动创建并为 `Content` 添加合适的 `LayoutGroup + ContentSizeFitter`（垂直/水平随方向）

- 资源与命名最佳实践：
  - 图层 `name` 与工程资源名一致；找不到 Sprite 也会为像素类节点补挂空 `Image`，以保证后续绑定不失败。
  - 初始 `value` 将被钳制到 `[min,max]`。

- 最小模板示例（裁剪至关键字段）：
```json
{
  "name": "UI_Demo",
  "layerKind": "canvas",
  "visible": true,
  "opacity": 1.0,
  "rectTransform": {
    "anchor": [{"x":0, "y":0}, {"x":1, "y":1}],
    "pivot": {"x":0.5, "y":0.5},
    "anchoredPosition": {"x":0, "y":0},
    "sizeDelta": {"x":0, "y":0}
  },
  "canvasLayerData": {"renderMode": "overlay", "width": 1080, "height": 1920},
  "layers": [
    {
      "name": "Panel_Main",
      "layerKind": "group",
      "rectTransform": {
        "anchor": [{"x":0.5, "y":0.5}, {"x":0.5, "y":0.5}],
        "pivot": {"x":0.5, "y":0.5},
        "anchoredPosition": {"x":0, "y":0},
        "sizeDelta": {"x":800, "y":600}
      },
      "components": [
        {"name": "verticalLayout", "parameters": {}},
        {"name": "toggle", "parameters": {"isOn": true, "background": "Box", "checkmark": "Tick"}}
      ],
      "layers": [
        {"name": "Box", "layerKind": "pixel", "rectTransform": {"anchor":[{"x":0, "y":0}, {"x":1, "y":1}], "pivot":{"x":0.5, "y":0.5}, "anchoredPosition": {"x":0, "y":0}, "sizeDelta": {"x":0, "y":0}}, "pixelLayerData": {"kind":"pixel"}},
        {"name": "Tick", "layerKind": "pixel", "rectTransform": {"anchor":[{"x":0.5, "y":0.5}, {"x":0.5, "y":0.5}], "pivot":{"x":0.5, "y":0.5}, "anchoredPosition": {"x":0, "y":0}, "sizeDelta": {"x":40, "y":40}}, "pixelLayerData": {"kind":"pixel"}},
        {"name": "Label_Title", "layerKind": "text", "rectTransform": {"anchor":[{"x":0, "y":1}, {"x":1, "y":1}], "pivot":{"x":0.5, "y":1}, "anchoredPosition": {"x":0, "y":0}, "sizeDelta": {"x":0, "y":80}}, "textLayerData": {"kind":"text", "text":"标题", "fontSize":32, "color":{"r":255, "g":255, "b":255}, "textAlign":"center", "haveShadow":false, "warp":true, "rotation":0}}
      ]
    }
  ]
}
```

---

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
| `slider` | `UnityEngine.UI.Slider` | `min`/`max`/`value`/`wholeNumbers`/`direction`/`background`/`fill`/`handle`/`interactable` | `{ "name": "slider", "parameters": { "min":0, "max":100, "value":50, "fill":"Fill" } }` |
| `progress` | `UnityEngine.UI.Slider`（不可交互） | 同 `slider`，`interactable` 默认为 `false` | `{ "name": "progress", "parameters": { "min":0, "max":100, "value":35, "fill":"Fill" } }` |
| `toggle` | `UnityEngine.UI.Toggle` | `isOn`/`interactable`/`background`/`checkmark` | `{ "name": "toggle", "parameters": { "isOn": true, "background": "Box", "checkmark": "Tick" } }` |

## 处理流程

### 1. JSON解析阶段
- 使用`LayerJsonParser.ParseFromJson()`解析JSON
- 自动转换`layerKind`字符串为`ELayerKind`枚举
- 递归验证图层结构

### 2. 资源加载阶段
- 根据图层名称匹配Sprite资源
- 支持多种资源路径查找策略

### 3. 预制体生成阶段
- 创建Canvas根对象
- 递归处理所有图层
- 应用组件和布局

### 4. 组件处理阶段
- 根据`components`数组添加Unity组件
- 自动推导布局参数
- 应用文本样式和字体

### 5. 进度条（Slider/Progress）组件说明

不支持在 `group` 图层上添加 `slider` 或 `progress` 组件：

- `slider`: 生成可交互的 `UnityEngine.UI.Slider`
- `progress`: 生成不可交互的 `UnityEngine.UI.Slider`（`interactable=false`），用于展示型进度
- 如果有进度条，创建一个父物体，将相关组件加到这个父物体上

可用参数（放在 `parameters` 下）：

- `min`/`max`/`value`: 数值范围与初始值（默认 0/1/0）
- `wholeNumbers`: 是否只允许整数（默认 false）
- `direction`: 方向（可选值：LeftToRight, RightToLeft, BottomToTop, TopToBottom；默认 LeftToRight）
- `interactable`: 是否可交互；`progress` 若未显式设置，默认为 false
- `background`/`fill`/`handle`: 指定子结点名称以绑定 `targetGraphic`/`fillRect`/`handleRect`

若未显式指定节点名，将按名称关键词尝试匹配：背景（background/bg）、填充（fill/bar/progress）、手柄（handle/thumb）。其中 `fill` 最为关键，找不到会给出警告。

示例：

```json
{
  "name": "HP_Bar",
  "layerKind": "group",
  "rectTransform": { ... },
  "components": [
    {
      "name": "progress",
      "parameters": {
        "min": 0,
        "max": 100,
        "value": 35,
        "wholeNumbers": true,
        "direction": "LeftToRight",
        "background": "BG",
        "fill": "Fill",
        "handle": "Handle",
        "interactable": false
      }
    }
  ],
  "layers": [
    { "name": "BG", "layerKind": "pixel", "rectTransform": { ... }, "pixelLayerData": {"kind": "pixel"} },
    { "name": "Fill", "layerKind": "pixel", "rectTransform": { ... }, "pixelLayerData": {"kind": "pixel"} },
    { "name": "Handle", "layerKind": "pixel", "rectTransform": { ... }, "pixelLayerData": {"kind": "pixel"} }
  ]
}
```

### 6. 开关（Toggle）组件说明

支持在 `group` 图层上添加 `toggle` 组件，生成 `UnityEngine.UI.Toggle`。

可用参数：

- `isOn`: 初始是否选中（默认 false）
- `interactable`: 是否可交互（默认 true）
- `background`: 背景节点名称，若缺省按关键词（background/bg/box）匹配
- `checkmark`: 选中图标节点名称，若缺省按关键词（check/checkmark/tick）匹配

示例：

```json
{
  "name": "AcceptPolicy",
  "layerKind": "group",
  "rectTransform": { ... },
  "components": [
    {
      "name": "toggle",
      "parameters": {
        "isOn": true,
        "background": "Box",
        "checkmark": "Tick"
      }
    }
  ],
  "layers": [
    { "name": "Box",  "layerKind": "pixel", "rectTransform": { ... }, "pixelLayerData": {"kind": "pixel"} },
    { "name": "Tick", "layerKind": "pixel", "rectTransform": { ... }, "pixelLayerData": {"kind": "pixel"} }
  ]
}
```
### 7. 滚动容器与循环列表（ScrollRect + Prefab 模板）

支持在 `group` 图层上添加 `scroll` 组件，自动装配 `ScrollRect → Viewport(Image+Mask) → Content(Layout+Fitter)`，并绑定引用。适用于长列表、循环列表等场景。

- 组件：
  - `name`: `scroll`
  - `parameters`（可选）：
    - `direction`: `vertical` | `horizontal`（默认 `vertical`）
    - `movementType`: `Clamped` | `Elastic` | `Unrestricted`（默认 `Clamped`）
    - `inertia`: `true/false`（默认 `true`）
    - `decelerationRate`: `float`（默认 `0.135`）
    - `elasticity`: `float`（默认 `0.1`）
    - `viewport`: 视口节点名（未找到将自动创建 `Viewport`）
    - `content`: 内容节点名（未找到将自动创建 `Content`）

- 行为：
  - 当 `direction=vertical` 时，为 `Content` 自动挂 `VerticalLayoutGroup + ContentSizeFitter(vertical=PreferredSize)`；
  - 当 `direction=horizontal` 时，为 `Content` 自动挂 `HorizontalLayoutGroup + ContentSizeFitter(horizontal=PreferredSize)`；
  - 若未指定 `viewport/content`，将自动创建并按最佳实践设置锚点与遮罩。

- 循环列表（模板）示例：使用 `prefab` 将条目作为可复用模板（运行期可通过代码复用/填充数据）。

```json
{
  "name": "LoopListDemo",
  "layerKind": "canvas",
  "visible": true,
  "opacity": 1.0,
  "rectTransform": {
    "anchor": [{ "x": 0, "y": 0 }, { "x": 1, "y": 1 }],
    "pivot": { "x": 0.5, "y": 0.5 },
    "anchoredPosition": { "x": 0, "y": 0 },
    "sizeDelta": { "x": 0, "y": 0 },
    "offsetMin": { "x": 0, "y": 0 },
    "offsetMax": { "x": 0, "y": 0 }
  },
  "canvasLayerData": { "renderMode": "overlay", "width": 1080, "height": 1920 },
  "layers": [
    {
      "name": "List_Loop",
      "layerKind": "group",
      "visible": true,
      "opacity": 1.0,
      "rectTransform": {
        "anchor": [{ "x": 0.5, "y": 0.5 }, { "x": 0.5, "y": 0.5 }],
        "pivot": { "x": 0.5, "y": 0.5 },
        "anchoredPosition": { "x": 0, "y": 0 },
        "sizeDelta": { "x": 800, "y": 1000 },
        "offsetMin": { "x": 0, "y": 0 },
        "offsetMax": { "x": 0, "y": 0 }
      },
      "components": [
        {
          "name": "scroll",
          "parameters": {
            "direction": "vertical",
            "movementType": "Clamped",
            "inertia": true,
            "decelerationRate": 0.135,
            "elasticity": 0.1,
            "viewport": "Viewport",
            "content": "Content"
          }
        }
      ],
      "layers": [
        {
          "name": "Item_Template",
          "layerKind": "group",
          "visible": true,
          "opacity": 1.0,
          "rectTransform": {
            "anchor": [{ "x": 0, "y": 1 }, { "x": 1, "y": 1 }],
            "pivot": { "x": 0.5, "y": 1 },
            "anchoredPosition": { "x": 0, "y": 0 },
            "sizeDelta": { "x": 0, "y": 120 },
            "offsetMin": { "x": 0, "y": 0 },
            "offsetMax": { "x": 0, "y": 0 }
          },
          "components": [
            { "name": "prefab", "parameters": { "name": "LoopItem" } }
          ],
          "layers": [
            {
              "name": "Item_BG",
              "layerKind": "pixel",
              "visible": true,
              "opacity": 1.0,
              "rectTransform": {
                "anchor": [{ "x": 0, "y": 0 }, { "x": 1, "y": 1 }],
                "pivot": { "x": 0.5, "y": 0.5 },
                "anchoredPosition": { "x": 0, "y": 0 },
                "sizeDelta": { "x": 0, "y": 0 },
                "offsetMin": { "x": 0, "y": 0 },
                "offsetMax": { "x": 0, "y": 0 }
              },
              "pixelLayerData": { "kind": "pixel" }
            },
            {
              "name": "Label_Item",
              "layerKind": "text",
              "visible": true,
              "opacity": 1.0,
              "rectTransform": {
                "anchor": [{ "x": 0, "y": 0.5 }, { "x": 0, "y": 0.5 }],
                "pivot": { "x": 0, "y": 0.5 },
                "anchoredPosition": { "x": 24, "y": 0 },
                "sizeDelta": { "x": 680, "y": 60 },
                "offsetMin": { "x": 0, "y": 0 },
                "offsetMax": { "x": 0, "y": 0 }
              },
              "textLayerData": {
                "kind": "text",
                "text": "循环项",
                "fontSize": 28,
                "color": { "r": 255, "g": 255, "b": 255 },
                "textAlign": "left",
                "haveShadow": false,
                "warp": true,
                "rotation": 0.0
              }
            }
          ]
        }
      ]
    }
  ]
}
```

提示：循环列表通常在运行时由脚本驱动（对象池/复用、数据绑定）。上面的 `Item_Template` 使用 `prefab` 标记，会在首次构建时保存为独立的 `LoopItem.prefab`，便于运行期复用与实例化。

注意事项：

- 缺少 Image 时自动补挂：绑定到 `background`/`fill`/`handle` 的目标节点若无 `Image`，会自动添加一个空 `Image` 组件，确保可见与可绑定。
- Sprite 兜底：当资源库中找不到同名 Sprite 时，会依然为像素/智能对象图层挂载空 `Image` 作为兜底，避免后续绑定失败。
- 关键词匹配：未显式提供名称时，按关键词匹配节点——背景（background/bg）、填充（fill/bar/progress）、手柄（handle/thumb）。
- 填充节点必需：`fill` 是进度条的关键节点，若找不到会输出警告并跳过绑定。
- 方向与大小写：`direction` 字符串大小写不敏感，支持 LeftToRight / RightToLeft / BottomToTop / TopToBottom。
- 数值范围：初始 `value` 会被钳制到 `[min, max]`。
- 交互默认：`slider` 默认 `interactable=true`；`progress` 若未显式设置，默认 `interactable=false`。

## 配置系统
通过`AutoUIConfig.json`配置：
- 字体资源路径
- 布局参数
- 按钮点击效果
- 本地化设置

## 最佳实践

### 命名规范
- 图层名称应与资源文件名匹配
- 使用描述性的组件名称
- 遵循Unity命名约定

### 布局建议
- 合理使用锚点系统
- 利用自动布局减少手动调整
- 考虑不同分辨率适配

### 性能优化
- 避免过深的嵌套层级
- 合理使用预制体复用
- 优化图片资源大小




