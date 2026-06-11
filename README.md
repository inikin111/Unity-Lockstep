# Unity Lockstep

一个用来学习和验证帧同步思路的 Unity Demo。项目的目标不是直接做成完整游戏，而是先把 rollback / 重连 / 状态同步这些多人同步能力最依赖的底层约束搭稳：

- 固定 tick 推进
- 确定性模拟
- 输入驱动而不是状态驱动
- 客户端与服务端共用同一套模拟逻辑
- 为后续 checksum、追帧、回滚和断线重连预留清晰入口

当前 Demo 主要关注二维平面上的移动与碰撞，不处理完整 3D 垂直运动。

## 流程图导航

- [服务端连接流程](./ServerHost/ConnectionFlow.md)
- [客户端运行流程](./ClientFlow.md)

## 项目概览

- Unity 版本：`6000.3.1f1`
- 服务端框架：`.NET 8`
- 网络协议：`UDP`
- 模拟频率：`30 ticks/s`
- 默认输入延迟：`2 ticks`
- 默认服务端地址：`127.0.0.1:5478`

项目现在已经打通了从客户端输入采集、服务端按 tick 汇总输入、广播帧包，到客户端/服务端共同推进模拟的主链路。

## 这个项目已经做到什么

- 客户端只发送输入，不直接发送位置状态。
- 服务端按 tick 收集所有客户端输入并广播 `FramePacket`。
- 客户端和服务端都运行同一套 `Simulator`，推进相同的 `GameState`。
- 模拟层统一使用 `Vector3i` 定点整数，避免浮点误差带来的分歧。
- 已支持玩家移动、实体运动、球体碰撞、盒体与球体碰撞、玩家与实体碰撞、实体间碰撞。
- 已保存历史 `GameState`，并提供历史状态恢复入口。
- 已提供状态 checksum，方便继续做客户端/服务端分歧检测。
- 服务端保留历史帧包，并已具备给重连客户端下发历史状态与补帧的基础能力。
- 已支持运行中的新玩家加入和旧玩家重连的基本流程骨架。

## 目录结构

```text
Assets/
  Scenes/
    SampleScene.unity
  Scripts/
    Client/
      Client.cs
      ClientNetwork.cs
      GameRenderer.cs
      InputManager.cs
      UIManager.cs
      ...
    Shared/
      Codec.cs
      Packets.cs
      Simulator.cs
      Vector3i.cs
      EntityData.cs
      entityData.json
      ...

ServerHost/
  Program.cs
  Network.cs
  ServerHost.csproj
  ConnectionFlow.md

ClientFlow.md

Packages/
  manifest.json
ProjectSettings/
  ProjectVersion.txt
```

## 核心模块

### Client

[`Assets/Scripts/Client/Client.cs`](./Assets/Scripts/Client/Client.cs) 是 Unity 客户端主入口，负责：

- 初始化网络连接和本地渲染组件
- 采集本地输入并发送 `InputPacket`
- 缓存服务端下发的 `FramePacket`
- 以固定 tick 驱动模拟
- 将指定 tick 的 `GameState` 渲染到场景中
- 在收到状态同步包后恢复到历史 tick 并继续追帧

### Simulator

[`Assets/Scripts/Shared/Simulator.cs`](./Assets/Scripts/Shared/Simulator.cs) 是整个项目的核心。客户端和服务端都依赖它做确定性模拟，它负责：

- 根据输入推进玩家状态
- 更新实体位置与速度
- 处理碰撞
- 保存每一帧的 `GameState`
- 从历史帧恢复状态
- 计算指定 tick 的 checksum

如果后面要继续做 rollback、重放、断线恢复，这里会是最重要的基础层。

### Shared Packets / Codec

[`Assets/Scripts/Shared/Packets.cs`](./Assets/Scripts/Shared/Packets.cs) 和 [`Assets/Scripts/Shared/Codec.cs`](./Assets/Scripts/Shared/Codec.cs) 定义并编码网络消息，当前涉及：

- `ACKPacket`：连接握手与客户端身份分配
- `InputPacket`：客户端上传输入
- `FramePacket`：服务端广播某一 tick 的完整输入集合
- `StateSyncPacket`：状态同步
- `PlayerJoinPacket`：运行中新玩家加入的同步通知
- `ResendFramePacket`：为可靠性补强预留的数据结构基础

### ServerHost

[`ServerHost/Program.cs`](./ServerHost/Program.cs) 是一个轻量 UDP 服务端，当前负责：

- 监听客户端连接请求
- 分配 `clientId`
- 管理初次连接、运行中加入、断线重连
- 按 tick 缓存输入
- 生成并广播 `FramePacket`
- 同步推进服务端权威模拟
- 为状态同步与补发历史帧提供基础支持

[`ServerHost/ConnectionFlow.md`](./ServerHost/ConnectionFlow.md) 用 Mermaid 流程图整理了首连、运行中加入、断线重连和状态同步的主流程，更适合快速阅读和展示。[`ClientFlow.md`](./ClientFlow.md) 则补充了客户端从连接、收包、推进模拟到状态同步追帧的完整路径。

## 运行方式

### 1. 启动服务端

在仓库根目录执行：

```powershell
dotnet run --project .\ServerHost\ServerHost.csproj
```

### 2. 启动 Unity 客户端

- 用 Unity `6000.3.1f1` 打开项目
- 打开 `Assets/Scenes/SampleScene.unity`
- 进入 Play Mode

### 3. 默认联机配置

- 服务端地址：`127.0.0.1:5478`
- 服务端默认最少玩家数：`1`
- 当前更偏向本地实验 / 单机多开验证

如果要进一步验证多客户端，需要按现有实现继续扩展配置与可靠性逻辑。

## 帧同步主流程

```text
Client reads input
        |
        v
InputPacket(tick = currentTick + inputDelay)
        |
        v
Server buffers inputs by tick
        |
        v
Server broadcasts FramePacket(tick, all inputs)
        |
        v
Client and Server both run Simulator.SimulateFrame(...)
        |
        v
GameState is stored / rendered / checksummed
```

这套设计的关键点是：网络里传的是“输入”，不是“最终位置”；真正的状态由同一套确定性模拟算出来。

## 状态同步与重连基础

这个项目最有价值的一点，不只是“能跑”，而是已经把后续做完整同步系统需要的几个拼图先搭好了：

- 服务端保存历史 `GameState`
- 服务端保存最近一段 `FramePacket`
- 客户端支持加载 `StateSyncPacket`
- 客户端加载历史状态后会清理旧帧并从同步 tick 之后继续追帧
- 服务端已经区分“初次连接”“运行中加入”“断线重连”三类场景

也就是说，后续如果要继续把断线恢复做完整，主要工作已经从“推翻重做”变成“把现有能力串起来”。

## 当前限制

- 这是一个实验性质 Demo，还不是完整的联机框架。
- UDP 丢包、乱序、重传、超时恢复还没有完全做稳。
- `ResendFramePacket` 相关可靠性流程仍未完整闭环。
- `StateSyncPacket` 虽然已接入主流程，但完整的状态同步策略仍可继续收敛。
- 默认逻辑更偏单客户端 / 本地验证，多客户端边界还需要继续打磨。
- 碰撞和运动目前主要围绕平面移动验证，不追求完整 3D 物理。
- Unity 客户端和服务端共享同一份 `Shared` 源码，后续可以进一步拆成独立 shared assembly 或 package。

## 适合继续扩展的方向

- 完成丢帧检测、超时检测和重传机制
- 把 checksum 接入自动分歧检测
- 完整串起断线重连状态机
- 增加追帧加速与回滚/重放能力
- 抽离共享逻辑为独立模块
- 为 `PacketCodec`、`Simulator`、连接流程增加自动化测试
- 增强多客户端调试体验和可观测性

## 说明

这个项目更像是一块“同步系统试验田”，重点在把多人同步底层问题拆开、验证、积累，而不是快速堆出完整玩法。对想自己实现联机同步底层的人来说，它的价值在于结构清晰、演进方向明确，而且已经把最难回头补的那部分基础设施先放进来了。
