# Unity-Lockstep

一个用于学习和验证帧同步思路的 Unity demo。项目用 UDP 做客户端和服务器之间的输入/帧包通信，客户端用固定 tick 和整数定点数推进模拟，再把模拟结果渲染到 Unity 场景中。

当前 demo 主要关注二维平面上的移动、碰撞和帧同步流程，不考虑垂直运动。

## 项目特点

- 固定 30 ticks/s 的模拟步进。
- 客户端只上传输入，服务器按 tick 汇总输入并广播 `FramePacket`。
- 客户端根据服务器下发的帧包执行确定性模拟。
- 使用 `Vector3i` 表示定点数，默认 `1000 = 1 Unity unit`，减少浮点误差对同步的影响。
- 支持球形碰撞、盒体与球体碰撞、玩家与实体碰撞、实体间碰撞。
- 客户端保留历史 `GameState`，为后续回滚、重传和状态同步扩展留出位置。

## 目录结构

```text
Assets/
  Scenes/
    SampleScene.unity        Unity 示例场景
  Scripts/
    Client/                  客户端逻辑、模拟、渲染和输入
    Shared/                  客户端/服务器共享的数据结构和编解码
    Utils/                   Unity 与定点数相关工具扩展

ServerHost/
  Program.cs                 UDP 服务器主循环
  Network.cs                 服务器网络收发封装

Packages/
  manifest.json              Unity 包依赖
```

## 核心模块

### Client

`Assets/Scripts/Client/Client.cs` 是客户端主入口。它负责：

- 初始化网络连接。
- 采集本地输入并发送 `InputPacket`。
- 缓存服务器下发的 `FramePacket`。
- 按固定 tick 驱动 `Simulator`。
- 将模拟结果交给 `GameRenderer` 渲染。

### Simulator

`Assets/Scripts/Client/Simulator.cs` 是确定性模拟核心。它负责：

- 根据帧包里的输入移动玩家。
- 更新动态实体速度和位置。
- 计算玩家、实体、墙体之间的碰撞。
- 保存每一帧的 `GameState`。

模拟中位置、速度、半径、碰撞尺寸都使用 `Vector3i` 定点整数表示。

### Shared Packets

`Assets/Scripts/Shared/Packets.cs` 和 `PacketCodec.cs` 定义并编码网络数据：

- `ACKPacket`：连接握手与初始客户端信息。
- `InputPacket`：客户端发送的输入。
- `FramePacket`：服务器广播的某一 tick 的完整输入集合。
- `ResendFramePacket`：预留的重传数据结构。

### ServerHost

`ServerHost/Program.cs` 是一个简单 UDP 服务器。默认逻辑是：

- 监听 `udp://127.0.0.1:5478`。
- 等待 2 个客户端连接。
- 为客户端分配 `clientId`。
- 每 tick 收集所有客户端输入。
- 当某个 tick 的输入齐全后，广播对应的 `FramePacket`。

## 运行方式

### 1. 打开 Unity 项目

使用 Unity `6000.3.1f1` 打开项目根目录。

打开场景：

```text
Assets/Scenes/SampleScene.unity
```

### 2. 启动服务器

在项目根目录运行服务器入口。

如果你的 IDE 已经把 `ServerHost` 作为可运行项目识别出来，可以直接运行 `ServerHost.Program.Main()`。

服务器启动后会监听：

```text
udp://127.0.0.1:5478
```

注意：当前服务器默认等待 2 个客户端连接后才会开始下发连接响应和帧包。

### 3. 启动客户端

在 Unity Editor 中运行场景。为了让默认服务器开始游戏，需要启动两个客户端实例，例如：

- 一个 Unity Editor 实例。
- 一个打包出的 Player。

客户端连接成功后，会收到服务器分配的 `clientId`，然后进入固定 tick 模拟。

### 4. 操作方式

在场景中点击鼠标左键，客户端会把点击位置转换为移动目标，并通过 `InputPacket` 发送给服务器。服务器在对应 tick 广播输入集合后，客户端执行移动和碰撞模拟。

## 帧同步流程

```text
Client click/input
        |
        v
InputPacket(tick = currentFrame + inputDelay)
        |
        v
Server caches inputs by tick
        |
        v
Server broadcasts FramePacket(tick, all client inputs)
        |
        v
Client Simulator.SimulateFrame(framePacket)
        |
        v
GameRenderer renders GameState
```

当前输入延迟为 `2` 帧，客户端和服务器都按 `30 ticks/s` 推进。

## 当前实现范围

- 已实现基础连接握手。
- 已实现输入上传和帧包广播。
- 已实现本地确定性模拟。
- 已实现玩家移动和若干碰撞处理。
- 已预留重传和状态同步相关数据结构。

## 已知限制

- 服务器默认等待 2 个客户端，不适合单客户端直接开跑。
- 重传、断线重连、中途加入、状态同步还没有完整实现。
- 碰撞主要按水平面 demo 处理，不考虑完整 3D 垂直运动。
- UDP 丢包、乱序和超时恢复逻辑仍处于实验阶段。
- 服务器和 Unity 客户端共享代码目前依赖同一套 C# 源文件组织，后续可以拆成独立 shared assembly 或 package。

## 适合继续扩展的方向

- 增加单客户端调试模式。
- 完成 `ResendFramePacket` 重传流程。
- 增加状态 hash，用于检测不同客户端的模拟分歧。
- 加入回滚和重放逻辑。
- 把服务器工程独立成标准 `.csproj`。
- 为 `PacketCodec` 和 `Simulator` 增加单元测试。
