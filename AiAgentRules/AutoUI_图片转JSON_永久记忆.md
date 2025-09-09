# AutoUI 图片转JSON 永久记忆

## 功能概述

当用户发送UI图片时，AI助手需要按照AutoUI系统的JSON格式规则分析图片并生成data.json文件，用于创建Unity UI预制体。

## 核心规则

### 1. 分析流程
1. **图片分析**：识别UI图片中的各种元素（按钮、文本、图片、容器等）
2. **层次结构**：分析元素的父子关系和层级结构
3. **布局计算**：估算元素的位置、尺寸、锚点等布局信息
4. **组件识别**：识别需要添加的Unity组件类型
5. **JSON生成**：按照AutoUI格式生成完整的JSON文件
6. **生成预制体** 通过UnityMCP调用AutoUIMainFromRoot方法
### 2. JSON结构规则

#### 根对象结构
读取项目中Assets\Scripts\Library\AutoUI\AutoUI_JSON_Format_Documentation.md

### 3. 元素类型识别规则

#### 3.1 图片元素 (pixel/smartObject)
- **识别特征**：纯图片、图标、背景图
- **layerKind**：`"pixel"` 或 `"smartObject"`
- **命名规则**：使用描述性名称，如 `"Icon_Settings"`, `"Background_Main"`
- **数据对象**：`pixelLayerData`

#### 3.2 文本元素 (text)
- **识别特征**：包含文字内容的区域
- **layerKind**：`"text"`
- **命名规则**：如 `"Label_PlayerName"`, `"Text_Score"`
- **数据对象**：`textLayerData`
- **必需字段**：text, fontSize, color, textAlign

#### 3.3 容器元素 (group)
- **识别特征**：包含其他元素的容器
- **layerKind**：`"group"`
- **命名规则**：如 `"Panel_Options"`, `"Container_Items"`
- **数据对象**：无特定数据对象
- **子元素**：包含layers数组

### 4. 布局计算规则

#### 4.1 位置计算
- 基于图片像素坐标计算相对位置
- 转换为Unity的锚点系统
- 考虑不同分辨率适配

#### 4.2 尺寸计算
- 基于图片中的实际尺寸
- 转换为Unity的sizeDelta
- 保持宽高比

#### 4.3 锚点设置
- 根据元素在界面中的位置关系设置锚点
- 左对齐：anchorMin.x = 0, anchorMax.x = 0
- 右对齐：anchorMin.x = 1, anchorMax.x = 1
- 居中：anchorMin.x = 0.5, anchorMax.x = 0.5

### 5. 组件识别规则

#### 5.1 按钮组件
- **识别特征**：可点击的UI元素
- **组件名称**：`"button"`
- **命名规则**：如 `"Button_Close"`, `"Button_Confirm"`

#### 5.2 布局组件
- **水平布局**：`"horizontalLayout"`
- **垂直布局**：`"verticalLayout"`
- **网格布局**：`"grid"`

#### 5.3 文本样式
- **标题样式**：`"title"`

### 6. 命名规范

#### 6.1 元素命名
- 使用描述性名称
- 采用 `类型_功能` 格式
- 避免特殊字符和空格
- 使用下划线分隔单词

#### 6.2 常见命名模式
- 按钮：`Button_功能名`
- 文本：`Label_内容描述` 或 `Text_内容描述`
- 图片：`Icon_功能名` 或 `Image_内容描述`
- 容器：`Panel_功能名` 或 `Container_内容描述`

### 7. 文件保存规则

#### 7.1 保存位置
- 文件路径：`Design/UIJson/data.json`
- 使用相对路径：`../../Design/UIJson/data.json`

#### 7.2 文件格式
- 使用UTF-8编码
- 格式化JSON（缩进2个空格）
- 确保JSON语法正确

### 8. 特殊处理规则

#### 8.1 复杂布局
- 识别嵌套容器结构
- 正确处理父子关系
- 保持层级结构清晰

#### 8.2 响应式设计
- 考虑不同屏幕尺寸
- 使用合适的锚点设置
- 避免固定像素值

#### 8.3 性能优化
- 避免过深的嵌套层级
- 合理使用组件
- 优化资源引用

### 9. 质量检查

#### 9.1 JSON验证
- 确保JSON格式正确
- 验证必需字段存在
- 检查数据类型正确

#### 9.2 结构验证
- 确保层级结构合理
- 验证组件配置正确
- 检查命名规范符合

### 10. 使用示例

当用户发送UI图片时，AI助手应该：

1. **分析图片**：识别所有UI元素和布局
2. **生成JSON**：按照上述规则生成完整的JSON结构
3. **保存文件**：将JSON保存到指定位置
4. **验证结果**：确保生成的JSON可以正确解析

## 注意事项

1. **准确性**：确保分析结果准确反映图片内容
2. **完整性**：不遗漏重要的UI元素
3. **规范性**：严格遵循命名和格式规范
4. **可维护性**：生成的JSON结构清晰易读
5. **兼容性**：确保与AutoUI系统完全兼容

