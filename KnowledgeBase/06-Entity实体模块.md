# 模块 06：UGF Entity、GameHot Entity 与 ETEntity

> Catalog ID: `UNITY-06`、`ET-04`  
> 状态：`verified`  
> 最后核验：`2026-08-05`  
> 适用模式：GameHot Client / ET Client / Shared Unity

## 模块定位

项目以 UGF EntityComponent 统一负责 prefab 加载、实例池、显示/隐藏、更新和父子挂接。共享层提供按 Luban Entity 表打开资源的 API；GameHot 使用 `EntityData + EntityLogic` 编写表现业务；ETEntity 把一个 ET `UGFEntity` Entity 绑定到内部 UGF `ETMonoUGFEntity`，并把 UGF 生命周期转发为 ET System。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| 共享 Entity 基类 | `Unity/Assets/Scripts/Game/Entity` | AEntityData、AEntity、AExEntity、扩展 API |
| prefab | `Unity/Assets/Res/Entity` | 普通 Entity 资源 |
| 配置源 | `Design/Excel/GameHot/Datas/Game/Entity.xlsx`、`Design/Excel/ET/Datas/Game/Entity.xlsx` | 两种模式 Entity 表 |
| 运行配置 | `Unity/Assets/Res/Editor/Luban/dtentity.json` | 导出结果；禁止手改 |
| GameHot 业务 | `Unity/Assets/Scripts/Game/Hot/Code/Entity` | 飞机、装备、子弹、陨石、特效 |
| GameHot Loader 基类 | `Unity/Assets/Scripts/Game/Hot/Loader/Entity/AHotEntity.cs` | 热更 Entity 稳定基类 |
| ETEntity Loader | `Unity/Assets/Scripts/Game/ET/Loader/UGF/Entity` | Entity/Mono 桥与生命周期接口 |
| ETEntity 生命周期闸门 | `Unity/Assets/Scripts/Game/ET/Loader/UGF/UGFSystemSingleton.cs` | 扫描并分派 `[UGFEntitySystem]` |
| ETEntity 创建入口 | `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Module/GFEntity` | GFEntityComponent 扩展 |
| ETEntity System | `Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client` | `[UGFEntitySystem]` 业务逻辑 |
| 生成 ID | `Game/Hot/Code/Generate/UGF/EntityId.cs`、`Game/ET/Code/ModelView/Client/Generate/UGF/UGFEntityId.cs` | 禁止手改 |

省略的 `Game/...` 路径均以 `Unity/Assets/Scripts/` 为根。

## 依赖关系

```text
Entity.xlsx -> DREntity/DTEntity + EntityId
  -> Game.EntityExtension
  -> UGF EntityComponent
     -> GameHot EntityLogic
     -> ETMonoUGFEntity <-> ET UGFEntity<TView> <-> UGFEntity System
```

`AExEntity` 复用 EventContainer、EntityContainer、ResourceContainer。GameHot Code 引用 Loader；ET `UGFEntity` 和桥接 Mono 位于 Loader，具体 Entity 定义在 ModelView、System 在 HotfixView。

## 入口与调用链

### 共享/UGF 打开

```text
GameEntry.Entity.ShowEntity<T>(entityTypeId, userData)
  -> DTEntity.GetOrDefault(entityTypeId)
  -> GenerateSerialId()
  -> AssetUtility.GetEntityAsset(AssetName)
  -> UGF ShowEntity(id, logicType, asset, group, priority, userData)
  -> EntityLogic.OnInit（池实例首次）
  -> EntityLogic.OnShow（每次显示）
```

项目约定：`0` 无效；服务端同步实体使用正 ID；客户端本地临时实体使用负 ID。共享 `GenerateSerialId` 从 0 递减。

### GameHot

`SurvivalGame` 每秒创建 `AsteroidData` 并调用 `ShowAsteroid`。GameHot `EntityExtension` 用 `data.TypeId` 查 DTEntity，但保留 `data.Id` 作为 UGF Entity ID。`Entity.OnShow` 写入位置、旋转和单位缩放；Aircraft 再显示并挂接 Weapon、Armor、Thruster。

### ETEntity

```text
GFEntityComponent.AddGFEntityChildAsync<T>(UGFEntityId.X)
  -> 创建 ET UGFEntity
  -> UGFEntity.ShowEntityAsync
  -> GameEntry.Entity.ShowEntityAsync<ETMonoUGFEntity>(..., ETMonoUGFEntityData)
  -> ETMonoUGFEntity.OnShow 消费 pooled data
  -> 绑定 CachedTransform、UGFMono、强类型 View
  -> UGFSystemSingleton.UGFEntityOnShow
  -> [UGFEntitySystem] 方法
```

Attach/Detach、Update、Hide、Recycle 同样由 `ETMonoUGFEntity` 转发。`UGFEntity<TView>` 从 wrapper GameObject 上 `GetComponent<TView>()` 获取业务 Mono View。

这条 ETEntity 链路目前只核验到静态 API 与测试示例：`GFEntityComponentSystem` 提供 `AddGFEntityChildAsync` / `AddGFEntityComponentAsync`，`UGFEntityTest` 与 `UGFEntityTestSystem` 提供一个 `[UGFEntitySystem]` OnShow 示例；但 Demo/LockStep 的 Scene AddComponent 代码中 `scene.AddComponent<UGFEntityComponent>()` 仍是注释，当前未找到业务侧调用 `AddGFEntityChildAsync` 或 `AddGFEntityComponentAsync`。因此 `ET-04` 不能宣称已有业务 run loop 通过，只能记录“创建入口、桥接和分派机制已静态核验，运行闭环待接入/验证”。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/约束 |
| --- | --- | --- |
| `AEntityData` | 通用 Id、TypeId、Position、Rotation | 普通可序列化类，**不是 IReference** |
| `AEntity` | UGF EntityLogic 基类 | 缓存原父节点；Hide 时恢复父节点和编辑器名称 |
| `AExEntity` | 带事件/子 Entity/资源容器 | Hide 自动清理受管对象 |
| `EntityExtension.ShowEntity<T>` | 按 DREntity 同步显示 | 配置缺失返回 null |
| `EntityExtension.ShowEntityAsync<T>` | 可取消异步显示 | 配置缺失返回已完成的 null UniTask |
| `Game.Hot.EntityData` | GameHot Id/TypeId/Transform 数据 | 普通类，不走 ReferencePool |
| `Game.Hot.Entity` | GameHot EntityLogic 基类 | OnShow 强制 userData 为 EntityData |
| `UGFEntity<TView>` | ET Entity 与强类型 Mono View | View/Transform 标记 BsonIgnore |
| `ETMonoUGFEntity` | 内部 UGF EntityLogic 桥 | 不放业务逻辑，只转发生命周期 |
| `GFEntityComponentSystem` | 创建/移除 ETEntity | Child 支持多实例，Component 适合唯一类型 |
| `UGFSystemSingleton` | ETEntity 生命周期分派闸门 | Awake 收集 `[UGFEntitySystem]`；运行时先检查 `IUGFEntityOn*` 接口再 Run |

## 数据与生命周期

UGF 实例池生命周期为 OnInit -> OnShow -> OnUpdate -> OnHide -> 以后可再次 OnShow；实例真正回收时 OnRecycle。业务每次显示的数据必须在 OnShow 重置，不能只在 OnInit 初始化。

`AEntity.OnHide` 会把因 Attach 改变的 Transform 父节点恢复到 EntityGroup 实例根。`AExEntity.OnHide` 在此之前隐藏自己的子 Entity、退订事件、卸载资源；shutdown 时只清本地所有权，避免调用已关闭模块。

ET `UGFEntity.Dispose` 会取消尚未完成的 Show 请求，并在实体仍 Available 时调用 UGF Hide。Show 使用池化 `CancellationTokenSourcePlus`。UGF Hide 不等于 ET Entity 自动 Dispose；推荐由 ET 父 Child/Component 所有权驱动销毁，保持两侧一致。

父子挂接通过 UGF EntityComponent 完成，ET 桥会分别转发 Parent 的 OnAttached/OnDetached 和 Child 的 OnAttachTo/OnDetachFrom。

生命周期分派由 `UGFSystemSingleton` 统一控制：Awake 时扫描带 `UGFEntitySystemAttribute` 的 System 并登记到 `TypeSystems`；运行时每个 `UGFEntityOn*` 方法都会先判断 Entity 是否实现对应 `IUGFEntityOn*` 接口，再查找系统并捕获异常。声明接口但没有对应 `[UGFEntitySystem]` 方法时不会产生业务行为；没有声明接口时即使存在同名方法也不会被该闸门分派。

## 开发扩展步骤

### GameHot Entity

1. 在 GameHot `Entity.xlsx` 增加 Id、CSName、AssetName、EntityGroupName、Priority 并导出。
2. 在 `Game/Hot/Code/Entity/EntityData` 创建数据类，继承 GameHot `EntityData`。
3. 在 `EntityLogic` 创建表现类，继承 GameHot `Entity` 或其子类。
4. OnShow 中校验并缓存具体 Data；OnHide 中解除业务引用和事件。
5. 在 GameHot EntityExtension 增加语义化 Show 方法，使用生成的 EntityId/表 TypeId。
6. 需要附件时确保 prefab 存在准确的挂点路径，再调用 AttachEntity。

### ETEntity

1. 在 ET `Entity.xlsx` 配置并导出 `UGFEntityId`。
2. prefab 上添加继承 `AETMonoUGFEntity` 的 View 组件。
3. ModelView 创建 `UGFEntity<TView>` Entity，声明 `IAwake` 及需要的 `IUGFEntityOn*` 接口。
4. HotfixView 创建 `[EntitySystemOf]` partial System，生命周期方法标记 `[UGFEntitySystem]`。
5. 在目标 Scene 明确添加 `GFEntityComponent`，并从业务流程调用 Child/Component Async API；移除 Child/Component 以关闭并 Dispose。当前 Demo/LockStep 示例里相关 `AddComponent` 仍是注释，接入前不能假设运行时已有组件。

## 约束与常见错误

- Entity 表 TypeId、生成 ID、业务 Data.TypeId 和 prefab 路径必须一致。
- 共享 `ShowEntity(entityTypeId, ...)` 会生成新的负 ID；GameHot 专用 Show 使用 `data.Id`。不要混淆 TypeId 与运行实例 Id。
- 旧草稿曾把 `AEntityData` 描述成 ReferencePool 对象，当前源码不是；不要对它调用 `ReferencePool.Release`。
- `Game.Hot.Entity.OnShow` 收到非 GameHot EntityData 时只记录 Error 后返回，池中旧字段可能仍在；调用方必须传正确类型。
- Attach Point 字符串必须在父 prefab 中存在，否则装备层级和回调行为不正确。
- ET prefab 必须同时有内部 wrapper 所需的业务 `AETMonoUGFEntity` View；缺失时强类型 View 为 null。
- `UGFEntity.ShowEntityAsync` 不允许同一 ET Entity 重复 Show。
- ET Entity 声明生命周期接口后必须有对应 `[UGFEntitySystem]`；未声明接口则转发器不会调用该阶段。
- 当前未找到业务侧 `AddGFEntityChildAsync` / `AddGFEntityComponentAsync` 调用；新增 ETEntity 前必须先补齐 Scene 上的 `GFEntityComponent` 与真实调用点，否则只有 API 和测试 System，无法形成运行闭环。
- 场景切换通常会批量隐藏 UGF Entity；ET 侧也必须同步移除拥有它们的 Entity，避免只剩逻辑对象。

## 验证方法

1. 对一个新 Entity 验证首次 OnInit、每次 OnShow/OnHide、实例池复用和最终 OnRecycle。
2. 异步加载中取消/切场景，确认不会留下加载实例或未释放 ET Entity。
3. GameHot 运行 Survival，验证 Aircraft 附件、Asteroid、Bullet、Effect 的创建和回收。
4. 验证本地生成 ID 为负、服务器实体正 ID 不与本地临时实体冲突。
5. ET 模式先静态确认目标 Scene 实际添加 `GFEntityComponent`，并存在非注释的 `AddGFEntityChildAsync` 或 `AddGFEntityComponentAsync` 业务调用；没有这两项时只能验证 API，不能记录为 run loop 通过。
6. ET 模式创建一个 `UGFEntity<TView>`，检查 View/Transform 绑定、System 回调和 Dispose Hide；同时覆盖 prefab 缺少业务 `AETMonoUGFEntity` View、重复 Show、配置缺失导致返回 null/异常的失败边界。
7. 验证 Attach/Detach 的父子双方回调与 Transform 恢复。

本次已完成共享 Entity API、GameHot 实际玩法调用、ET UGFEntity/Mono 桥、GFEntityComponent 创建入口和 `[UGFEntitySystem]` 测试示例的静态源码核验；未发现业务侧 ETEntity run loop 调用。运行、实例池、Attach/Detach 和 ETEntity 接入行为仍需在 Unity 中回归，不能把本轮静态核对写成运行通过。

## 源码证据

- `Unity/Assets/Scripts/Game/Entity/EntityExtension.cs`：表驱动打开、负 ID 生成和 UIEntity 分支。
- `Unity/Assets/Scripts/Game/Entity/EntityLogic/AEntity.cs`：父节点恢复与基础生命周期。
- `Unity/Assets/Scripts/Game/Entity/EntityLogic/AExEntity.cs`：容器所有权清理。
- `Unity/Assets/Scripts/Game/Hot/Code/Entity/EntityExtension.cs`：GameHot Data.TypeId 到资源的映射。
- `Unity/Assets/Scripts/Game/Hot/Code/Entity/EntityLogic/Entity.cs`：GameHot Data 应用。
- `Unity/Assets/Scripts/Game/Hot/Code/Game/SurvivalGame.cs`：Asteroid 真实创建调用。
- `Unity/Assets/Scripts/Game/ET/Loader/UGF/Entity/UGFEntity.cs`：ET Entity 打开、取消、Attach 和 Dispose。
- `Unity/Assets/Scripts/Game/ET/Loader/UGF/Entity/ETMonoUGFEntity.cs`：UGF 到 ET 生命周期转发。
- `Unity/Assets/Scripts/Game/ET/Loader/UGF/UGFSystemSingleton.cs`：UGF Entity System 注册、接口检查和分派闸门。
- `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Module/GFEntity/GFEntityComponentSystem.cs`：Child/Component 创建和批量关闭。
- `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Game/Test/UGFEntityTest.cs`：声明 `IUGFEntityOnShow` 的 ETEntity 测试类型。
- `Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client/Game/Test/UGFEntityTestSystem.cs`：实际 `[UGFEntitySystem]` 示例。
- `Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client/Demo/Scene/AfterCreateCurrentScene_AddComponent.cs`、`Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client/Demo/Scene/AfterCreateClientScene_AddComponent.cs`、`Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client/LockStep/Scene/AfterCreateClientScene_LSAddComponent.cs`：当前 `UGFEntityComponent` 接入为注释状态。

## 关联知识

- 上游：`UNITY-03` GameHot 玩法、`ET-03` ET 程序集。
- 依赖：`DATA-01` Luban、`UNITY-05` 容器。
- 下游：`UNITY-08` 场景、`ET-07` 网络与锁步表现。
