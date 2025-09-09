# AutoUI JSON选择规则说明

## 概述

AutoUI系统现在支持三种JSON文件选择方式，提供了更灵活的工作流程。

## 三种选择方法

### 方法1：从文件夹选择JSON文件（原有方式改进）

**菜单路径**: `Tools/AutoUI`

**功能描述**:
- 选择包含JSON文件的文件夹
- 自动检测文件夹中的所有JSON文件
- 如果只有一个JSON文件，自动选择
- 如果有多个JSON文件，提供选择对话框

**使用场景**:
- 项目中有多个UI设计文件
- 需要从特定文件夹中选择JSON文件
- 保持原有的工作流程

**代码实现**:
```csharp
// 选择文件夹
string folderPath = AutoUIFile.SelectFolderPath();
// 从文件夹中选择JSON文件
string jsonPath = AutoUIFile.SelectJsonFileFromFolder(folderPath);
```

### 方法2：从Design/UIJson目录选择JSON文件（新增）

**菜单路径**: `Tools/AutoUI - 从Design/UIJson选择JSON`

**功能描述**:
- 直接扫描Design/UIJson目录
- 查找所有JSON文件
- 提供选择界面或自动选择

**使用场景**:
- JSON文件统一放在Design/UIJson目录
- 快速访问设计文件
- 不需要选择文件夹的简化流程

**代码实现**:
```csharp
// 从Design/UIJson目录选择JSON文件
string jsonPath = AutoUIFile.SelectJsonFileFromRoot();
```

### 方法3：手动选择JSON文件（新增）

**菜单路径**: `Tools/AutoUI - 选择JSON文件`

**功能描述**:
- 使用Unity的文件选择对话框
- 可以浏览到任意位置选择JSON文件
- 最灵活的选择方式

**使用场景**:
- 需要从任意位置选择JSON文件
- 临时使用特定的JSON文件
- 调试和测试时使用

**代码实现**:
```csharp
// 手动选择JSON文件
string jsonPath = EditorUtility.OpenFilePanel("选择JSON文件", "", "json");
```

## 配置系统

### AutoUIConfig.json配置

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

### 配置参数说明

- **name**: 默认JSON文件名（用于向后兼容）
- **selectionMode**: 选择模式（"folder" 或 "root"）
- **autoSelect**: 是否自动选择唯一文件
- **uiJsonPath**: Design/UIJson目录的相对路径

## 智能选择逻辑

### 自动选择规则

1. **单个文件**: 如果只找到一个JSON文件，自动选择
2. **多个文件**: 显示选择对话框，提供以下选项：
   - 取消
   - 使用第一个
   - 手动选择（打开详细选择窗口）

### 选择对话框

当有多个JSON文件时，系统会显示一个包含以下选项的对话框：
- **取消**: 终止操作
- **使用第一个**: 自动选择第一个文件
- **手动选择**: 打开详细的选择窗口

### 详细选择窗口

手动选择模式会打开一个专门的窗口，显示：
- 所有可用JSON文件的列表
- 单选按钮界面
- 确定/取消按钮

## 使用建议

### 开发阶段
- 使用**方法1**（文件夹选择）进行常规开发
- 使用**方法3**（手动选择）进行调试和测试

### 生产环境
- 使用**方法2**（Design/UIJson目录选择）进行快速部署
- 配置`autoSelect: true`实现自动化

### 团队协作
- 统一使用**方法1**保持工作流程一致性
- 在配置文件中设置合适的默认值

## 错误处理

系统包含完善的错误处理机制：

1. **文件不存在**: 显示错误信息并终止操作
2. **路径无效**: 提示用户重新选择
3. **JSON格式错误**: 在解析阶段进行验证
4. **权限问题**: 检查文件访问权限

## 扩展性

系统设计具有良好的扩展性：

1. **新增选择模式**: 在`SelectJsonFileByMode`方法中添加新的case
2. **自定义选择逻辑**: 继承`AutoUIFile`类并重写选择方法
3. **配置扩展**: 在`AutoUIConfigData`类中添加新的配置项

## 总结

新的JSON选择规则提供了三种灵活的选择方式，满足不同场景的需求：

- **方法1**: 保持原有工作流程，适合常规开发
- **方法2**: 从Design/UIJson目录选择，适合设计文件管理
- **方法3**: 提供最大灵活性，适合调试和测试

所有方法都包含智能选择逻辑和错误处理，确保系统的稳定性和易用性。
