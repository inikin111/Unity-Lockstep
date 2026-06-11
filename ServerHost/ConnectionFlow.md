# Connection Flow

这份文档用流程图概括 `ServerHost` 当前的连接处理主流程，方便快速理解服务端如何区分首连、运行中加入和断线重连。

## Server Connection Flow

```mermaid
flowchart TD
    A["Client sends ACKPacket as connection request"] --> B["Server parses clientId and requested spawn position"]
    B --> C{"clientId exists in clients?"}
    C -- Yes --> D["Treat as reconnect request"]
    C -- No --> E{"Server state is WaitingForPlayers?"}
    E -- Yes --> F["Add to pendingConnections"]
    E -- No --> G["Assign new clientId and treat as running join"]

    F --> H{"pendingConnections >= minClients?"}
    H -- No --> I["Keep waiting"]
    H -- Yes --> J["Build initial ClientPos list"]
    J --> K["Send ACKPacket with initial player positions"]
    K --> L["Initialize Simulator with opening players"]
    L --> M["Enter Running state"]

    D --> N["Update client endpoint"]
    N --> O["Send ACKPacket with empty clientPos"]
    O --> P["Queue StateSyncRequest(syncTick)"]

    G --> Q["Update client endpoint"]
    Q --> R["Send ACKPacket with empty clientPos"]
    R --> S["Choose syncTick"]
    S --> T["Choose joinTick = currentTick + inputDelay + 1"]
    T --> U["Queue PendingJoin(clientId, spawnPosition, joinTick)"]
    U --> V["Queue StateSyncRequest(syncTick)"]
    V --> W["Broadcast PlayerJoinPacket(joinTick)"]
```

## State Sync And Catch-Up

```mermaid
flowchart TD
    A["ProcessSyncRequests"] --> B{"Request count exceeds per-tick limit?"}
    B -- Yes --> C["Handle remaining requests next loop"]
    B -- No --> D["Load historical GameState by syncTick"]
    D --> E["Send StateSyncPacket(syncTick, gameState)"]
    E --> F["Resend buffered FramePacket from syncTick + 1 to currentTick - 1"]
    F --> G["Client loads state and continues catching up"]
```

## Join Application Timing

```mermaid
flowchart TD
    A["PendingJoin enters queue"] --> B["Server tick loop reaches joinTick"]
    B --> C["ProcessPendingJoinsForTick(joinTick)"]
    C --> D["Simulator adds new PlayerState"]
    D --> E["All clients also apply PlayerJoin on the same joinTick"]
```

## Key Points

- 首次开局时，`pendingConnections` 只用于凑齐开局玩家并生成初始玩家列表。
- 运行中的新玩家加入不会直接插入当前帧，而是延后到未来的 `joinTick` 同步生效。
- 断线重连和运行中加入都会先收到 `ACKPacket`，再通过 `StateSyncPacket` 恢复到历史状态。
- 状态同步之后，客户端依靠补发的历史 `FramePacket` 追到服务端当前 tick。
