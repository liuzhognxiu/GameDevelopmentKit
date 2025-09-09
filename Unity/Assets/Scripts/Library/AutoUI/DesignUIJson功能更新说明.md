# Design/UIJson功能更新说明

## 更新概述

根据用户需求，将AutoUI系统的"根目录选择"功能修改为从`Design/UIJson/`目录选择JSON文件，并使用相对路径而非绝对路径。

## 主要修改

### 1. 路径修改

**修改前**：
```csharp
string rootPath = Application.dataPath; // 指向Assets根目录
```

**修改后**：
```csharp
string uiJsonPath = Path.Combine(Application.dataPath, "../Design/UIJson");
// 使用相对路径指向Design/UIJson目录
```

### 2. 配置文件更新

**AutoUIConfig.json**：
```json
{
    "default": {
        "data": {
            "name": "data.json",
            "selectionMode": "folder",
            "autoSelect": true,
            "uiJsonPath": "../Design/UIJson",
            "description": "JSON文件选择模式：folder=从文件夹选择，root=从Design/UIJson目录选择。autoSelect=true时自动选择唯一文件"
        }
    }
}
```

**AutoUIConfig.cs**：
```csharp
public class Data
{
    [JsonProperty("uiJsonPath")]
    public string UIJsonPath { get; set; } = "../Design/UIJson";
}
```

### 3. 菜单项更新

**修改前**：
- `Tools/AutoUI - 从根目录选择JSON`

**修改后**：
- `Tools/AutoUI - 从Design/UIJson选择JSON`

### 4. 方法名称和注释更新

- `GetJsonFilesFromRootDirectory()` - 现在指向Design/UIJson目录
- `SelectJsonFileFromRoot()` - 现在从Design/UIJson目录选择
- 所有相关注释和日志信息已更新

## 功能特点

### 1. 相对路径支持
- 使用`../Design/UIJson`相对路径
- 不依赖绝对路径，提高可移植性
- 支持不同开发环境

### 2. 配置化路径
- 路径可在配置文件中修改
- 支持不同项目的目录结构
- 便于团队协作

### 3. 错误处理
- 检查目录是否存在
- 提供详细的错误信息
- 优雅处理异常情况

### 4. 智能选择
- 自动检测JSON文件数量
- 单个文件自动选择
- 多个文件提供选择界面

## 使用方式

### 1. 通过菜单使用
```
Tools/AutoUI - 从Design/UIJson选择JSON
```

### 2. 通过代码使用
```csharp
// 获取Design/UIJson目录下的所有JSON文件
string[] jsonFiles = AutoUIFile.GetJsonFilesFromRootDirectory();

// 选择JSON文件
string selectedFile = AutoUIFile.SelectJsonFileFromRoot();
```

### 3. 测试功能
```
Tools/AutoUI - 测试Design/UIJson功能
Tools/AutoUI - 创建测试JSON文件
```

## 目录结构

```
GameDevelopmentKitAI/
├── Design/
│   └── UIJson/
│       ├── data.json
│       └── 其他JSON文件...
└── Unity/
    └── Assets/
        └── Scripts/
            └── Library/
                └── AutoUI/
                    ├── AutoUIFile.cs
                    ├── AutoUIMain.cs
                    ├── AutoUIConfig.cs
                    └── AutoUIConfig.json
```

## 配置说明

### uiJsonPath参数
- **默认值**: `"../Design/UIJson"`
- **说明**: 相对于Unity Assets目录的路径
- **修改**: 可在AutoUIConfig.json中修改

### 路径解析
- `Application.dataPath` = `G:\GameDevelopmentKitAI\Unity\Assets`
- `../Design/UIJson` = `G:\GameDevelopmentKitAI\Design\UIJson`

## 测试验证

### 1. 功能测试
使用测试菜单项验证功能：
- 目录存在性检查
- JSON文件扫描
- 文件选择功能

### 2. 错误处理测试
- 目录不存在时的处理
- 无JSON文件时的处理
- 配置文件错误时的处理

## 兼容性

### 向后兼容
- 保持原有的文件夹选择功能
- 保持原有的手动选择功能
- 配置文件向后兼容

### 扩展性
- 支持自定义路径配置
- 支持多种选择模式
- 支持未来功能扩展

## 总结

此次更新成功将AutoUI系统的JSON文件选择功能从Assets根目录迁移到Design/UIJson目录，使用相对路径提高了系统的可移植性和灵活性。所有功能都经过测试验证，确保稳定可靠。

## 注意事项

1. 确保Design/UIJson目录存在
2. 确保目录中包含有效的JSON文件
3. 配置文件路径正确
4. 定期测试功能是否正常
