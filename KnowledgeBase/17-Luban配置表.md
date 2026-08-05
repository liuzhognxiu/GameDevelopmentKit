# 模块 17：Luban 配置生产链与客户端 Tables

> Catalog ID: `UNITY-15`、`DATA-01`  
> 状态：`verified`  
> 最后核验：`2026-08-05`  
> 适用模式：GameHot / ET Client / ET Server / Editor / Tooling

## 模块定位

本模块覆盖从 ET/GameHot Excel 工程，经 Share.Tool 并行调用 Luban，到 Unity、服务端 Config 和生成 C# 的完整生产链；同时说明公共 GF Tables 与 GameHot 热更 Tables 的运行时装载差异。本地化后处理详见 UNITY-10。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| 数据源 | `Design/Excel/ET`、`Design/Excel/GameHot` | schema、业务 Excel、luban.conf |
| 工具入口 | `Share/Tool/Loader/Init.cs`、`Share/Tool/Loader/Define.cs` | `AppType.ExcelExporter` 分派与 `../Bin` 工作目录 |
| 导出器 | `Share/Tool/ExcelExporter/ExcelExporter.Luban.cs` | 发现、命令展开、并行执行、复制 |
| 二次生成 | `Share/Tool/ExcelExporter/Generate` | UGF UI/Entity/Scene/Sound ID |
| Luban | `Tools/Luban` | DLL、自定义模板和官方模板 |
| 公共运行时 | `Unity/Assets/Scripts/Game/Tables/TablesComponent.Load.cs` | GF 公共表加载类型与生命周期 |
| 公共生成物 | `Unity/Assets/Scripts/Game/Generate/Luban`、`Unity/Assets/Res/Luban` | 禁止手改 |
| GameHot 运行时 | `Unity/Assets/Scripts/Game/Hot/Code/Tables` | 热更表加载扩展 |
| GameHot 生成物 | `Unity/Assets/Scripts/Game/Hot/Code/Generate/Luban`、`Unity/Assets/Res/Hot/Luban` | 禁止手改 |
| Buqi 当前扩展表 | `Design/Excel/GameHot/Datas/Buqi`、`Unity/Assets/Scripts/Game/Hot/Code/Buqi/Config` | 历史 Step 3 9/3/6 已完成；当前 `buqi-effects-cv1` 为 24 法门、6 淬炼、16 道影，详见 `BUQI-01` |
| 服务端数据 | `Config/Luban` | ET clientserver 数据副本，禁止人工混放文件 |

## 依赖关系

- 导出工具属于 Share.Tool，要求先构建 `Kit.sln`，并从 `Bin` 工作目录运行。
- `Share/Tool/Loader/Init.Main` 解析命令行后按 `Options.Instance.AppType` 分派；`AppType.ExcelExporter` 会设置 `Console=1` 并调用 `ExcelExporter.ExportAll()`。`Share/Tool/Loader/Define.WorkDir` 固定为 `Path.GetFullPath("../Bin")`，`%UNITY_ASSETS%` 与 `%ROOT%` 都从该工作目录推导。
- 工具依赖 .NET 8、Luban DLL、ExcelDataReader、两个 `luban.conf` 和 `Localization.xlsx`。
- `UNITY_ET` / `UNITY_GAMEHOT` 模式工具会切换对应 conf 的 active；当前仓库状态是 ET=false、GameHot=true。
- 公共 Tables 依赖 UGF Resource Awaitable、Luban ByteBuf/SimpleJSON 和 `TablesMemory`。
- ET 独立服务端从 `Config/Luban` 加载，客户端公共表和 ET/GameHot 私有表是不同生成目标。

## 入口与调用链

导出：在 `Bin` 工作目录运行 `Tool.exe --AppType=ExcelExporter` -> `Init.Main` 校验 `Define.WorkDir` 不含空格并进入 `AppType.ExcelExporter` -> `ExcelExporter.ExportAll` -> 扫描 `Design/Excel` 直接子目录 -> 读取 active conf -> 展开路径/格式选项 -> `Parallel.ForEachAsync` 调 `dotnet Luban.dll` -> 复制逗号分隔的次级输出 -> 生成 UGF ID -> `ExcelExporter.ExportAll` 继续导出 Localization。

公共运行时：`ProcedurePreload` -> `GameEntry.Tables.LoadAllAsync` -> 反射查找生成 partial 的 `LoadAsync` -> 根据 Loader 返回值选择 ByteBuf 或 JSON -> 并行加载各表并解析引用。若不存在 `LoadAsync`，只设置 `LoadType=Code`。

GameHot：`HotEntry` 的 TablesComponent -> 热更 `LoadAllAsync` -> 反射生成 Loader -> 从 `Assets/Res/Hot/Luban/{file}.bytes/json` 读取 -> 解析后立即 Unload TextAsset。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/线程约束 |
| --- | --- | --- |
| `ExcelExporter_Luban.DoExport` | 整个 Luban 导出协调器 | 外部进程可并行，输出目录不得冲突 |
| `GenConfig` | active/customTemplate/cmds | GDK 扩展，不是 Luban 标准 schema |
| `TablesComponent.LoadAllAsync` | 装载公共表 | Unity 主线程发起，资源加载异步 |
| `TablesLoadType` | Undefined/Bytes/Json/Code | 仅公共 GF Tables 暴露 |
| 生成 `LoadAsync(loader)` | 创建表、并发 Load、ResolveRef | 生成物，签名随导出格式变化 |
| `TablesMemory` | 记录/清理 Luban 解析内存 | 公共组件 Awake/OnDestroy 清理 |
| `DT*.GetOrDefault(id)` | 强类型按键查询 | 表加载完成后使用 |

## 数据与生命周期

- ET conf 生成公共 GF、ET Client、ClientServer、Editor 五个目标；ClientServer 数据从 Unity 输出复制到 `Config/Luban`。当前 ET conf 为 inactive，普通导出不会刷新服务端 `Config/Luban`。
- GameHot conf 生成公共 GF、热更 Client 和 Editor 四个目标。当前 GameHot conf 为 active，是本基线的默认导出目标。
- 公共 GF 目标在两套 conf 中都写入 `Unity/Assets/Scripts/Game/Generate/Luban` 与 `Unity/Assets/Res/Luban`；`AssetUtility.GetLubanAsset(file, fromJson)` 和公共 `TablesComponent.LoadAllAsync` 只从 `Assets/Res/Luban/{file}.bytes/json` 取数。
- GameHot 私有目标只由 GameHot conf 的 `client` 生成，代码在 `Unity/Assets/Scripts/Game/Hot/Code/Generate/Luban`，数据在 `Unity/Assets/Res/Hot/Luban`；热更 Tables 通过 `AssetUtility.GetGameHotAsset("Luban/...")` 读取并在解析后卸载 TextAsset。
- Buqi 历史 Step 3 已在 GameHot `__tables__.xlsx` 注册 `DTBuqiGlobal`、`DTBuqiItem`、`DTBuqiRefinement`、`DTBuqiEcho`，并在 `__beans__.xlsx`/`__enums__.xlsx` 注册效果、快照、尺寸、品质、构筑方向、触发、目标和条件 schema。该门禁只证明 9 法门、3 淬炼、6 道影的最小配置链路能导出到 Hot generated C#、Hot bytes 与 Editor JSON。
- Buqi 当前扩展基线为 `buqi-effects-cv1`：GameHot 源 Excel 记录 24 个法门、6 个淬炼、16 个道影和 1 条全局配置，新增 Heal/Regen/Poison/Burn/Freeze 等效果枚举与字段，导出结果继续落到 Hot generated C#、Hot bytes 与 Editor JSON；热更 `TablesComponent.BuqiConfig.cs` 在生成表存在时反射读取、校验并暴露 `BuqiConfig`/`BuqiItemDefinitions`。
- ET 私有目标由 ET conf 的 `client` / `clientserver` 生成到 `Unity/Assets/Scripts/Game/ET/Code/Model/Generate/*/Luban` 与 `Unity/Assets/Res/ET/*/Luban`，其中 `clientserver` 的数据副本复制到 `Config/Luban` 供服务端读取。
- `Json` 将 `cs-bin/bin` 替换成 `cs-simple-json/json`；`Check` 移除输出参数并附加 `-f`，且跳过 Localization。
- 多目标目录以第一个为源，导出后清空并复制到其余目录；目标目录不能放人工文件。
- 公共 Tables 的 Byte/JSON Loader 当前没有调用 `GameEntry.Resource.UnloadAsset(textAsset)`；资源由 Resource 系统继续持有。GameHot Loader 解析后会卸载 TextAsset。
- 公共组件 Awake 与 OnDestroy 会 `TablesMemory.Clear`；GameHot 热更 partial 没有对应清理代码。

## 开发扩展步骤

1. 在当前 active 工程的 `Datas` 增加/修改 Excel，并在 `__tables__.xlsx`、`__beans__.xlsx` 或 `__enums__.xlsx` 声明 schema。
2. 先从仓库根执行 `dotnet build Kit.sln`，再从 Unity 菜单 `Game/Tool/ExcelExporter`，或切换到 `Design/Excel` 目录运行 `gen all bin.bat`。该 bat 未用 `%~dp0` 固定自身目录。
3. 检查公共、当前业务模式、Editor JSON 和服务端目标是否按 conf 更新。
4. 使用生成 Tables 属性和 ID 常量；任何字段/常量缺失都回到 Excel/schema/生成器修改。
5. 新增导出后处理时修改 `Share/Tool/ExcelExporter/Generate`，不要在生成目录补手写 partial 伪装结果。

## 约束与常见错误

- 仓库根路径不能含空格；Share.Tool 会拒绝该工作目录。
- 两个 active 工程会并行写公共目录，可能互相覆盖。正常模式只应启用一个。
- `IsEnableET/IsEnableGameHot` 当前第一轮扫描仅按目录名设置，没有检查 active，因此两个目录都存在时两个标志都会为 true；不要用它们判断当前运行模式。
- GameHot Loader 假定反射得到 `LoadAsync`；缺生成 partial 时会空引用，不会回退 Code 模式。
- `Check` 不生成数据、代码、ID 或 Localization，不能用于“刷新配置”。
- 表名查询使用生成类名而非 Excel 文件名；生成目录和 Config/Luban 禁止手改。
- 并行日志顺序不代表配置顺序；外部进程退出码当前没有显式检查，主要通过 stderr 和包含 `|ERROR|` 的输出判断失败。

## 验证方法

1. 在仓库根构建 `Kit.sln`，确认 `Bin/Tool.exe` 或 Tool.dll 存在。
2. 从 `Bin` 运行 `Tool.exe --AppType=ExcelExporter --Console=1 --Customs=Check,ShowCmd`，确认所有 active 命令通过。
3. 正式 bin 导出后执行 `git diff`，确认只变化预期生成目录和资源。
4. Unity 启动到 ProcedurePreload，确认 `LoadType` 与生成格式一致且关键表可 `GetOrDefault`。
5. ET 服务端从 Bin 启动，确认 `Config/Luban` 可读；运行时验证需要 .NET 8 和 Unity 环境。

Buqi 局部配置链路还应执行 `Game.Hot.Buqi.Tests` 中的配置适配测试，确认当前 24/6/16 计数、ID 范围、触发/效果/目标组合、引用、道影棋盘和真实 generated bytes 适配合法；历史 9/3/6 只作为 Step 3 门禁记录保留。这只证明 `BUQI-01` 的局部门禁，不替代本知识库五项通用运行验收。

## 源码证据

- `Share/Tool/Loader/Init.cs`、`Share/Tool/Loader/Define.cs`：`AppType.ExcelExporter` 入口、`../Bin` 工作目录和空格路径失败边界。
- `Share/Tool/ExcelExporter/ExcelExporter.Luban.cs`：发现、选项替换、并行、复制和二次生成。
- `Design/Excel/ET/luban.conf`：当前 inactive 状态与五类真实输出。
- `Design/Excel/GameHot/luban.conf`：GameHot 输出及当前 active 状态。
- `Unity/Assets/Scripts/Game/Utility/AssetUtility.cs`：公共 `Assets/Res/Luban`、ET `Assets/Res/ET` 和 GameHot `Assets/Res/Hot` 资源路径拼接。
- `Unity/Assets/Scripts/Game/Tables/TablesComponent.Load.cs`：公共类型判断、Code 回退和内存清理。
- `Unity/Assets/Scripts/Game/Hot/Code/Tables/TablesComponent.Load.cs`：热更路径与 TextAsset 卸载。
- `Unity/Assets/Scripts/Game/Generate/Luban/TablesComponent.cs`：生成 Loader 的并行加载和引用解析。
- `Design/Excel/GameHot/Datas/Buqi/*.xlsx`、`Design/Excel/GameHot/Datas/__tables__.xlsx`、`__beans__.xlsx`、`__enums__.xlsx`：Buqi 历史 Step 3 与当前 24/6/16 扩展基线的源表和 schema 注册。
- `Unity/Assets/Scripts/Game/Hot/Code/Tables/TablesComponent.BuqiConfig.cs`、`Unity/Assets/Scripts/Game/Hot/Code/Buqi/Config/*.cs`：生成表到战斗定义的适配、校验和 provider。
- `Unity/Assets/Scripts/Game/Hot/Code/Generate/Luban/DTBuqi*.cs`、`Unity/Assets/Res/Hot/Luban/dtbuqi*.bytes`、`Unity/Assets/Res/Editor/Hot/Luban/dtbuqi*.json`：Buqi 四表的生成代码与导出数据。

## 关联知识

- 上游：`ARCH-02`、`TOOLS-01`、`LIB-05`
- 下游：`UNITY-04`、`UNITY-06`、`UNITY-08`、`UNITY-09`、`UNITY-10`、`SERVER-02`、`BUQI-01`
