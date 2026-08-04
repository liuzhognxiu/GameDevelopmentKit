# 模块 07：AssetSet 资源设置

> Catalog ID: `UNITY-07`  
> 状态：`verified`  
> 最后核验：`2026-08-04`  
> 适用模式：GameHot / ET Client / Unity Runtime

## 模块定位

AssetSet 把 Sprite、Texture 等 Unity 资源异步或同步地设置到 UI 目标，并跟踪“目标是否仍在使用该资源”。它在 UGF Resource、项目私有文件系统和 WebRequest 之上提供请求合并、对象池复用、远程图片落盘和延迟释放。

它不负责 AssetBundle 构建、资源更新清单或通用资源依赖管理；这些仍由 UGF Resource 模块负责。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| 公开入口 | `Unity/Assets/Scripts/Game/AssetSet/SetSpriteExtension.cs` | `Image`、`UXImage`、`RawImage` 扩展方法 |
| 核心 | `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/AssetSet` | 接口、组件、三种来源、对象池和内置设置项 |
| 项目扩展 | `Unity/Assets/Scripts/Game/AssetSet` | UXTool 目标和 UniTask 可等待封装 |
| 注册 | `Unity/Assets/Scripts/Game/Base/GameEntry.Extension.cs` | 暴露 `GameEntry.AssetSet` |
| 序列化配置 | `Unity/Assets/Res/GameEntry.prefab` | 回收间隔、池容量、文件系统容量 |

## 依赖关系

- 上游依赖：UGF `ResourceComponent`、`FileSystemComponent`、`WebRequestComponent`、`ObjectPoolComponent`、`EventComponent`，以及 GF `ReferencePool`。
- 异步封装依赖 UniTask 的 `AutoResetUniTaskCompletionSource`。
- 下游调用者通过 `Image.SetSprite*`、`UXImage.SetSprite*`、`RawImage.SetTextureBy*` 使用；ET Client 和 GameHot 共用同一个 Unity 组件。
- `GameEntry.Start` 先缓存组件引用；`AssetSetComponent.Start` 再创建对象池和内部集合，因此不要在其 `Start` 之前调用。

## 入口与调用链

以 `image.SetSpriteAsync(path)` 为例：

1. 扩展方法从引用池取得 `WaitableImageSet`，记录 `Target`、`AssetPath` 和完成源。
2. `SetByResource` 先取消同一 Target 尚未完成的旧请求。
3. 若对象池已有 `(path, Sprite)`，直接 Spawn、设置目标并创建 `LoadedAssetSet`。
4. 否则加入等待列表；`m_LoadingAssets` 保证同一“路径+类型”只调用一次 UGF `LoadAsset`。
5. 成功回调注册 `AssetSetObject`，为每个等待者各 Spawn 一次并执行 `SetAsset`。
6. `Update` 每 30 秒调用 `ReleaseUnused`；目标销毁或资源被替换后，回收 Spawn，最终由池释放资源。

Web 路径优先复用对象池；`NeedSave` 为真且私有文件系统已有同名文件时改走本地读取，否则发 WebRequest，反序列化后最多保存一次响应字节。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/线程约束 |
| --- | --- | --- |
| `IAssetSet` / `AssetSet<T>` | 描述资源路径、目标、类型、赋值和可释放条件 | 实现 `IReference`，必须通过 `ReferencePool` 获取/释放 |
| `SetByResource<T>` | 从 UGF Resource 设置资源 | Resource 回调驱动；按路径+类型合并加载 |
| `SetByFileSystem<T>` | 从私有 GF 文件系统读取并反序列化 | 当前实现同步读取；要求 `ISerializeAssetSet` |
| `SetByWebRequest<T>` | 下载、反序列化，并可写入私有文件系统 | 按路径合并 Web 请求；要求序列化与保存接口 |
| `ImageSet` / `UXImageSet` | 将 Sprite 设置到 UI 控件 | 控件销毁或 Sprite 被替换后可释放 |
| `RawImageSet` | 设置 Texture2D，并实现字节反序列化 | Web/文件资源最终由 Unity `Destroy` |
| `Waitable*Set` | 在 `SetAsset` 时完成 UniTask | 被替换或加载失败并释放时，任务被取消 |
| `RemoveLoadingAssetSet` | 取消指定等待项 | 只移除本地等待，不会取消底层共享加载/请求 |

## 数据与生命周期

- `AssetSetObject` 是多引用对象池对象；每个活跃目标占一次 Spawn。
- UGF Resource 来源保存 `ResourceComponent`，最终释放走 `UnloadAsset`；文件和 Web 创建的 Unity 对象没有 ResourceComponent，最终走 `UnityEngine.Object.Destroy`。
- `LoadedAssetSet` 持有设置项和实际资源。`IsCanRelease` 为真后回收 Spawn，并把设置项和记录释放到引用池。
- GameEntry prefab 当前配置：检查间隔 30 秒、对象池自动释放间隔 60 秒、容量 16、过期时间 60 秒、初始读缓冲 65536 字节。
- WebRequest 来源在 `InitializeWeb` 中订阅全局 WebRequest 成功/失败事件；当前源码没有对应 `OnDestroy`/`OnDisable` 退订实现，依赖 GameEntry 常驻组件生命周期。
- 私有文件系统使用持久化目录中的 `AssetSetFileSystem_1.dat` / `_2.dat`；文件数达到上限时迁移到另一个文件并扩容。

## 开发扩展步骤

1. 为目标资源类型实现 `AssetSet<T>`，在 `Create` 中从 `ReferencePool` 获取实例并完整设置 `AssetPath` 与 `Target`。
2. 在 `SetAsset` 中只在目标仍有效时赋值，并记录本次设置的资源。
3. 在 `IsCanRelease` 中判断目标销毁或目标已不再引用本资源；在 `Clear` 中清空全部引用和完成源。
4. 若来源是字节流，实现 `ISerializeAssetSet`；需要 Web 落盘时再实现 `ISaveAbleAssetSet`。
5. 提供面向业务控件的扩展方法，参照 `SetSpriteExtension.cs` 选择 Resource、FileSystem 或 WebRequest。

```csharp
await icon.SetSpriteAsync(AssetUtility.GetUISpriteAsset("Icon/world"));
await avatar.SetTextureByWebRequestAsync(avatarUrl);
```

## 约束与常见错误

- `SetTextureByFileSystemAsync` 名称虽带 Async，但底层文件读取和 `Texture2D.LoadImage` 当前同步执行；不要在主线程加载大文件。
- WebRequest 失败不会把错误对象传给等待者；等待项被释放后 UniTask 表现为取消，详细原因只写日志。
- 同一 Target 的新“等待请求”会取消旧等待项；已经加载完成的旧记录要等目标换图或销毁后由定时检查回收。
- Web 请求按路径合并，而 Resource 按路径+类型合并。不要让同一 URL 同时代表不兼容的资源类型。
- `RemoveLoadingAssetSet` 不能取消已经发出的共享底层请求。
- 如果将 `AssetSetComponent` 改成可动态卸载或替换的组件，必须补上 WebRequest 事件退订，否则旧实例仍可能收到全局事件。
- 所有 Unity 对象赋值、反序列化和销毁均发生在 Unity 主线程语境，不要从工作线程直接调用。

## 验证方法

1. 在 Unity 打开 `GameEntry.prefab`，确认存在 `AssetSetComponent` 及上述池配置。
2. 对两个 Image 同时请求同一路径，观察只产生一次 Resource 加载且两者都显示；换图后等待一次回收检查。
3. 对 RawImage 请求远程图片，确认首次下载后 `HasFile(path)` 为真，第二次从文件系统加载。
4. 在请求完成前销毁目标或再次设置，确认等待任务取消且无悬挂引用。
5. 运行 `powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1` 做知识库静态校验。

## 源码证据

- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/AssetSet/AssetSetComponent.Resource.cs`：Resource 请求合并、成功分发与失败清理。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/AssetSet/AssetSetComponent.WebRequest.cs`：Web、本地缓存、保存链路及全局事件订阅。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/AssetSet/AssetSetComponent.FileSystem.cs`：双文件系统、同步读取和迁移扩容。
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework.Extension/Runtime/AssetSet/AssetSetComponent.AssetSetObject.cs`：`UnloadAsset` 与 `Destroy` 的所有权边界。
- `Unity/Assets/Scripts/Game/AssetSet/SetSpriteExtension.cs`：项目公开调用 API。

## 关联知识

- 上游：`LIB-01`、`LIB-02`、`LIB-03`、`LIB-04`
- 下游：`UNITY-04`、`UNITY-05`、`ET-05`
