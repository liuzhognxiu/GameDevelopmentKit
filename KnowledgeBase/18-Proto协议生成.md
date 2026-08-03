# 模块 18：Proto 协议生成链

> Catalog ID: `DATA-02`  
> 状态：`verified`  
> 最后核验：`2026-08-04`  
> 适用模式：GameHot / ET Client / ET Server / Admin / Tooling

## 模块定位

Proto2CS 是项目自带的行解析代码生成器，不调用 protoc。它从多个 `proto.conf` 生成 ET MemoryPack Message 或 GameHot Protobuf Packet、Opcode 常量和 UGF Handler partial。它只支持项目约定的 Proto 子集。

## 源码边界

| 类型 | 仓库相对路径 | 说明 |
| --- | --- | --- |
| 输入 | `Design/Proto` | 四组 proto.conf 与协议源 |
| 协调器 | `Share/Tool/Proto2CS/Proto2CS.cs` | 配置发现、分派、路径和清理 |
| ET 生成器 | `Share/Tool/Proto2CS/Proto2CS.ET.cs` | MemoryPack Message 与 Opcode |
| UGF 生成器 | `Share/Tool/Proto2CS/Proto2CS.UGF.cs` | Protobuf Packet、ID 和 Handler |
| GameHot 输出 | `Unity/Assets/Scripts/Game/Hot/Code/Generate/Message` | 禁止手改 |
| ET 输出 | `Unity/Assets/Scripts/Game/ET/Code/Model/Generate/*/Message` | 禁止手改 |
| Admin 输出 | `DotNet/Model/Generate/Message` | 禁止手改 |
| 命令 | `Design/Proto/proto2cs.bat` | 从 Bin 启动 Tool.exe |

## 依赖关系

- 入口属于 Share.Tool 的 `AppType.Proto2CS`，要求先构建 `Kit.sln`。
- ET 输出依赖 ET MessageObject、MemoryPack、对象池和消息特性。
- UGF 输出依赖 Protobuf Unity、GameHot `CSPacketBase/SCPacketBase` 和 PacketHandler。
- `proto.conf` 独立分配 startOpcode，工具不做跨配置区间冲突检测。
- 生成代码被 ET 网络与 GameHot Network Loader 消费，协议源本身不实现业务 Handler。

## 入口与调用链

`Tool.exe --AppType=Proto2CS` -> 扫描 `Design/Proto` 直接子目录的 active conf -> 展开输出路径 -> 按 codeType 选择 ET/UGF -> 递归获取 `.proto` 并按完整路径排序 -> 按文件和消息出现顺序从 `startOpcode+1` 递增 -> 写各输出目录 -> 清理没有对应文件的空目录和 `.meta`。

当前配置：ET-Client 10000，输出到 Client 与 ClientServer；ET-ClientServer 20000；GameHot UGF 30000；ET-Admin 也从 30000 开始并输出 DotNet Model。

## 核心类型与 API

| 类型/API | 职责 | 生命周期/线程约束 |
| --- | --- | --- |
| `Proto2CS.Export` | 发现配置并生成全部 active 组 | 必须以 Bin 为工作目录运行 |
| `proto.conf` | active/startOpcode/codeName/codeType/namespace/outputs | 每组独立，无全局冲突验证 |
| `Proto2CS_ET` | 生成 MessageObject、MemoryPack、Create/Dispose | 读取项目自定义行注释 |
| `Proto2CS_UGF` | 生成 CS/SC Packet、ProtoMember 和 Handler partial | 仅 CS/SC 前缀分配 Packet Opcode |
| `<codeName>_Id.cs` | ET Opcode 容器 | 生成物 |
| `<codeName>Id.cs` | UGF Packet ID | 生成物 |
| `<codeName>_PacketHandler.cs` | SC Handler partial 壳 | 业务实现放在非生成 partial 文件 |

## 数据与生命周期

- Opcode 由排序后的文件顺序和文件内 message 顺序决定；在中间插入消息会改变其后编号。
- ET `// ResponseType X` 与消息尾部 `// IRequest`、`// IActorRequest` 等影响生成特性/接口；结束行 `// no dispose` 抑制 Dispose。
- UGF `CS*` 继承 CSPacketBase，`SC*` 继承 SCPacketBase；其他 message 作为 IReference 数据对象，无 Packet ID。
- 当前实际输出名是 `GameHotMessage.cs`、`GameHotMessageId.cs`、`GameHotMessage_PacketHandler.cs`，以及 ET 的 `Message_ET_*` 主文件和 `_Id.cs`。
- 工具仅在内容变化时重写主输出，并保留仍对应文件/目录的 Unity meta。

## 开发扩展步骤

1. 在正确协议组修改 `.proto`，保持一行一个声明和项目支持的格式。
2. 上线协议只在组末尾追加 message，避免已有 Opcode 漂移；预先检查所有 conf 区间。
3. 在 `Design/Proto` 目录运行 `proto2cs.bat`，或使用 Unity 菜单 `Game/Tool/Proto2CS`。该 bat 未用 `%~dp0` 固定自身目录，从仓库根直接调用会让相对路径指向错误位置。
4. 检查所有共享输出目录内容一致，并编译 Unity 与 DotNet 消费端。
5. UGF SC 消息在非生成 partial 中实现 OnHandle；ET 消息按生成的请求/响应类型注册 Handler。

## 约束与常见错误

- 解析器不支持完整 Proto 语法；不要直接使用 package、import、oneof、optional 或复杂 option。
- startOpcode 不是第一条最终值，第一条是 `startOpcode + 1`；硬上限为 60000。
- 工具不检测不同 proto.conf 的 Opcode 重叠。GameHot 与 ET-Admin 当前都从 30000 开始，因运行栈不同暂可并存，但任何合并注册表的改动都必须先消除冲突。
- ET-Client 多输出是直接分别写相同内容；不要在任一生成副本手改。
- 修改文件路径或在前面插入消息会重排 Opcode，即使消息文本本身没变。
- Proto2CS 的相对输入根是 `../Design/Proto`，从错误工作目录直接调用会找不到配置。

## 验证方法

1. 构建 Kit.sln 后从 Bin 运行 `Tool.exe --AppType=Proto2CS --Console=1`。
2. 执行 `git diff`，确认已有上线 Opcode 未变化、共享 ET 输出一致且只有预期生成物变化。
3. 先用项目指定 Unity 版本打开工程、等待导入并生成/刷新被忽略的 `Unity/Unity.sln`，再编译 `Unity/Unity.sln` 与 `DotNet/DotNet.sln`，覆盖 GameHot、ET ClientServer 和 Admin 输出；干净检出不存在 Unity solution 时不能直接执行该步骤。
4. 对一对请求/响应做编码、发送、Handler 分派和解码的端到端测试。
5. 当前环境尚未执行生成与编译；需要 .NET 8/Unity 依赖后做最终运行验证。

## 源码证据

- `Share/Tool/Proto2CS/Proto2CS.cs`：发现、排序、分派、路径和清理规则。
- `Share/Tool/Proto2CS/Proto2CS.ET.cs`：ET 注释语义、MemoryPack 与对象池生成。
- `Share/Tool/Proto2CS/Proto2CS.UGF.cs`：CS/SC 识别、Protobuf 与 Handler 生成。
- `Design/Proto/ET-Client/proto.conf`：10000 段与双输出。
- `Design/Proto/GameHot/proto.conf`、`Design/Proto/ET-Admin/proto.conf`：当前 30000 起始值重叠。
- `Unity/Assets/Scripts/Game/Hot/Code/Generate/Message/GameHotMessage_PacketHandler.cs`：UGF 实际 Handler 生成物。

## 关联知识

- 上游：`TOOLS-01`、`PACKAGE-01`
- 下游：`ET-03`、`ET-07`、`SERVER-02`、`OPS-01`
