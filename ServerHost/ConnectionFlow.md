# 服务端连接流程

这份文档用流程图概括 `ServerHost` 当前的连接处理方式，重点说明服务端如何区分首连、运行中加入、断线重连，以及状态同步如何接回主循环。

## 总览

```mermaid
flowchart TD
    A["客户端发送连接请求"] --> B["服务端解析 clientId 与出生点"]
    B --> C["服务端归类为首连、运行中加入或断线重连"]
    C --> D["分配或确认 clientId，并更新远端地址"]
    D --> E["发送 ACKPacket 完成身份确认"]
    E --> F["首连：生成开局玩家列表并初始化 Simulator"]
    E --> G["运行中加入：计算 syncTick 与 joinTick"]
    E --> H["断线重连：准备状态同步"]
    F --> I["服务端进入 Running 状态"]
    G --> J["加入 PendingJoin 队列并广播 PlayerJoinPacket"]
    H --> K["加入 StateSyncRequest 队列"]
    J --> K
```

## 状态同步与补帧

```mermaid
flowchart TD
    A["主循环处理 StateSyncRequest 队列"] --> B["按 syncTick 读取历史 GameState"]
    B --> C["发送 StateSyncPacket(syncTick, gameState)"]
    C --> D["补发 syncTick 之后到 currentTick 之前的历史 FramePacket"]
    D --> E["客户端加载历史状态并继续追帧"]
```

## 运行中加入何时真正生效

```mermaid
flowchart TD
    A["新玩家加入请求进入 PendingJoin 队列"] --> B["服务端 tick 循环推进到 joinTick"]
    B --> C["ProcessPendingJoinsForTick 应用加入事件"]
    C --> D["Simulator 增加新的 PlayerState"]
    D --> E["所有客户端在同一个 joinTick 应用 PlayerJoin"]
```

## 关键点

- `pendingConnections` 只用于开局阶段聚合首批玩家，不参与运行期的正式玩家状态维护。
- 运行中的新玩家不会立刻插入当前帧，而是延后到未来的 `joinTick` 统一生效。
- 断线重连与运行中加入都会先拿到 `ACKPacket`，再通过 `StateSyncPacket` 恢复到历史状态。
- 状态同步完成后，客户端还需要依赖补发的历史 `FramePacket` 追到服务端当前 tick。
