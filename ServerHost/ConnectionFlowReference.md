# Connection Flow Reference

This is a reference-only sketch for reviewing connection handling. It is not compiled.

Key idea:

- `pendingConnections` is only a temporary request buffer.
- Opening players can be built from `pendingConnections`.
- Running joins should be scheduled to a future `joinTick`.
- State sync should send an already completed historical `GameState`.
- All clients should add the new player on the same `joinTick`.

```csharp
public record PendingClient(uint ClientId, Vector3i Position);
public record PendingJoin(uint ClientId, IPEndPoint Remote, Vector3i SpawnPosition, uint JoinTick);
public record StateSyncRequest(uint ClientId, uint SyncTick);

static Dictionary<IPEndPoint, PendingClient> pendingConnections = new();
static Dictionary<uint, IPEndPoint> clients = new();
static Queue<PendingJoin> pendingJoins = new();
static Queue<StateSyncRequest> stateSyncRequests = new();

static void ProcessConnectionRequests()
{
    if (pendingConnections.Count == 0)
    {
        return;
    }

    if (serverState == ServerState.WaitingForPlayers)
    {
        ProcessOpeningConnections();
        return;
    }

    ProcessRunningConnections();
}

static void ProcessOpeningConnections()
{
    if (pendingConnections.Count < minClients)
    {
        return;
    }

    ClientPos[] initialPositions = BuildInitialClientPositions();

    foreach (var connection in pendingConnections)
    {
        AcceptClient(connection.Value.ClientId, connection.Key);
        SendConnectionAck(connection.Value.ClientId, initialPositions, connection.Key);
    }

    // Opening initialization is the only place where pendingConnections should
    // be used to create the initial player set.
    simulator.SetPlayerState(CreatePlayerStates(initialPositions));

    pendingConnections.Clear();
    serverState = ServerState.Running;
}

static void ProcessRunningConnections()
{
    foreach (var connection in pendingConnections)
    {
        uint clientId = connection.Value.ClientId;

        if (clients.ContainsKey(clientId))
        {
            AcceptReconnect(clientId, connection.Key);
            continue;
        }

        AcceptRunningJoin(clientId, connection.Value.Position, connection.Key);
    }

    pendingConnections.Clear();
}

static void AcceptReconnect(uint clientId, IPEndPoint remote)
{
    AcceptClient(clientId, remote);

    uint syncTick = GetStateSyncTick();
    stateSyncRequests.Enqueue(new StateSyncRequest(clientId, syncTick));

    Console.WriteLine($"Client {clientId} reconnected from {remote.Address}:{remote.Port}; syncTick={syncTick}.");
}

static void AcceptRunningJoin(uint clientId, Vector3i spawnPosition, IPEndPoint remote)
{
    AcceptClient(clientId, remote);

    uint syncTick = GetStateSyncTick();
    uint joinTick = currentTick + inputDelay;

    // ACK only assigns identity. It should not pretend to contain the current
    // full game state for a running join.
    SendConnectionAck(clientId, Array.Empty<ClientPos>(), remote);

    stateSyncRequests.Enqueue(new StateSyncRequest(clientId, syncTick));
    pendingJoins.Enqueue(new PendingJoin(clientId, remote, spawnPosition, joinTick));

    // This should become a reliable broadcast packet or part of a future frame
    // command stream, so old clients and the joining client apply it together.
    BroadcastPlayerJoin(clientId, spawnPosition, joinTick);

    Console.WriteLine($"Client {clientId} joined from {remote.Address}:{remote.Port}; syncTick={syncTick}, joinTick={joinTick}.");
}

static void ProcessStateSyncRequests()
{
    while (stateSyncRequests.TryDequeue(out StateSyncRequest request))
    {
        if (!clients.TryGetValue(request.ClientId, out IPEndPoint? remote))
        {
            continue;
        }

        StateSyncPacket packet = new StateSyncPacket
        {
            tick = request.SyncTick,
            gameState = simulator.gameStateHistory[request.SyncTick]
        };

        network.SendPacket(PacketType.StateSync, PacketCodec.StateSyncPacketToBytes(packet), remote);

        // Optional but useful: also resend historical frames after syncTick so
        // the client can catch up to currentTick in order.
        ResendFrames(request.ClientId, request.SyncTick + 1, currentTick - 1);
    }
}

static void ProcessPendingJoinsForTick(uint tick)
{
    int count = pendingJoins.Count;
    for (int i = 0; i < count; i++)
    {
        PendingJoin join = pendingJoins.Dequeue();
        if (join.JoinTick != tick)
        {
            pendingJoins.Enqueue(join);
            continue;
        }

        // This must happen on the same tick on server and clients.
        simulator.SetPlayerState(new[]
        {
            CreatePlayerState(join.ClientId, join.SpawnPosition)
        });
    }
}

static uint GetStateSyncTick()
{
    if (currentTick == 0)
    {
        return 0;
    }

    uint completedTick = currentTick - 1;
    if (completedTick > inputDelay)
    {
        return completedTick - inputDelay;
    }

    return completedTick;
}

static ClientPos[] BuildInitialClientPositions()
{
    return pendingConnections.Values
        .OrderBy(connection => connection.ClientId)
        .Select(connection => new ClientPos
        {
            id = connection.ClientId,
            position = connection.Position
        })
        .ToArray();
}

static void AcceptClient(uint clientId, IPEndPoint remote)
{
    clients[clientId] = new IPEndPoint(remote.Address, remote.Port);
}

static void SendConnectionAck(uint clientId, ClientPos[] positions, IPEndPoint remote)
{
    ACKPacket responsePacket = new ACKPacket
    {
        clientId = clientId,
        clientPos = positions
    };

    network.SendPacket(PacketType.ACK, PacketCodec.ACKPacketToBytes(responsePacket), remote);
}

static void OnConnectionRequest(byte[] data, IPEndPoint remote)
{
    ACKPacket packet = PacketCodec.ReadACKPacketBody(data);
    Vector3i requestedPosition = packet.clientPos.Length > 0
        ? packet.clientPos[0].position
        : Vector3i.Zero;

    if (packet.clientId != 0 && clients.ContainsKey(packet.clientId))
    {
        pendingConnections[remote] = new PendingClient(packet.clientId, requestedPosition);
        return;
    }

    if (pendingConnections.ContainsKey(remote))
    {
        return;
    }

    connectedClientCount++;
    pendingConnections[remote] = new PendingClient(connectedClientCount, requestedPosition);
}

static PlayerState CreatePlayerState(uint clientId, Vector3i position)
{
    return new PlayerState
    {
        clientId = clientId,
        commandType = CommandType.None,
        targetPosition = Vector3i.Zero,
        frameVelocity = Vector3i.Zero,
        body = new CollisionBodyState
        {
            position = position,
            colliderSize = Vector3i.One,
            colliderRadius = 0.5f.ToFixedInt(),
            colliderType = ColliderType.Sphere
        }
    };
}

static void BroadcastPlayerJoin(uint clientId, Vector3i spawnPosition, uint joinTick)
{
    // Reference placeholder.
}

static void ResendFrames(uint clientId, uint fromTick, uint toTick)
{
    // Reference placeholder.
}
```
