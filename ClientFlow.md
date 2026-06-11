# 客户端运行流程

这份文档概括 Unity 客户端从启动连接、接收服务端数据，到按固定 tick 推进模拟和处理状态同步的主要流程。

## 客户端主流程

```mermaid
flowchart TD
    A["Client.Initialize(entityData)"] --> B["ClientNetwork.Initialize(...)"]
    B --> C["连接 UDP 服务端"]
    C --> D["发送 ACKPacket 作为连接请求"]
    D --> E["启动异步收包任务"]
    E --> F["Update 中持续执行 PumpReceivedPackets"]
    F --> G["收到 ACK 后完成身份确认与初始状态设置"]
    G --> H["累积 deltaTime，按固定 tick 驱动 Tick()"]
    H --> I["发送 InputPacket(currentFrame + inputDelay)"]
    I --> J["等待并读取当前帧对应的 FramePacket"]
    J --> K["调用 Simulator.SimulateFrame(framePacket)"]
    K --> L["RenderFrame + UpdateFrame + currentFrame++"]
```

## 收包分发流程

```mermaid
flowchart TD
    A["后台 ReceiveAsync 收到 UDP 数据"] --> B["写入 pendingPackets 队列"]
    B --> C["主线程调用 PumpReceivedPackets"]
    C --> D["读取 PacketHeader"]
    D --> E["分发到 ACK / Frame / StateSync / PlayerJoin 处理函数"]
    E --> F["更新连接状态、帧缓存或同步状态"]
```

## 首次连接完成时

```mermaid
flowchart TD
    A["收到 ACKPacket"] --> B["记录 clientId"]
    B --> C["初始化本地玩家显示对象"]
    C --> D["设置初始 PlayerState / EntityState / MotionConfig"]
    D --> E["CaptureGameState(currentFrame)"]
    E --> F["渲染初始画面并标记 isConnected = true"]
```

## 状态同步与运行中加入

```mermaid
flowchart TD
    A["收到 StateSyncPacket"] --> B["写入 simulator.gameStateHistory[syncTick]"]
    B --> C["LoadGameState(syncTick)"]
    C --> D["currentFrame = syncTick + 1"]
    D --> E["清理早于 currentFrame 的旧 FramePacket"]
    E --> F["重新渲染同步后的状态"]
    F --> G["继续等待后续 FramePacket 追到最新 tick"]

    H["收到 PlayerJoinPacket"] --> I["按 joinTick 缓存到 pendingJoins"]
    I --> J["Tick() 开头调用 ApplyPlayerJoins(currentFrame)"]
    J --> K["在指定 joinTick 把新玩家写入 Simulator"]
```

## 关键点

- 客户端发给服务端的是输入，而不是最终坐标；真正的位置结果由 `Simulator` 计算得出。
- 客户端只会在拿到对应 tick 的 `FramePacket` 后推进正式模拟，因此服务端广播顺序直接决定追帧节奏。
- `InputDelay` 让输入先发往未来帧，减少网络往返对当前帧推进的阻塞。
- `StateSyncPacket` 与历史 `FramePacket` 组合起来，构成断线重连和运行中加入后的追帧基础。
