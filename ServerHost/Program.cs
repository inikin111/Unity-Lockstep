using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Lockstep.Packets;
namespace ServerHost;

public static partial class Program
{
    static uint connectedClientCount = 0;
    const int MaxRequestHandlePerTick = 10;
    const uint minClients = 1;
    const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    const uint inputDelay = 2;
    static readonly bool enableGameStateFileLog = false;
    static uint currentTick = 0;
    static Network network = new Network();
    static Simulator simulator = new Simulator();
    public record PendingClient(uint ClientId, Vector3i Position);
    public record PendingJoin(uint ClientId, Vector3i SpawnPosition, uint JoinTick);
    public record StateSyncRequest(uint ClientId, uint SyncTick);
    static Dictionary<IPEndPoint, PendingClient> pendingConnections = new();
    static Dictionary<uint, IPEndPoint> clients = new Dictionary<uint, IPEndPoint>();
    // TODO:改成RingBuffer
    static Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick = new Dictionary<uint, Dictionary<uint, InputPacket>>();
    static Queue<PendingJoin> pendingJoins = new Queue<PendingJoin>();
    static Queue<StateSyncRequest> syncRequests = new Queue<StateSyncRequest>();
    // 重连要解决的问题：1.其他客户端添加重连客户端 2.重连客户端状态同步 3.不能写死ACKPacket发送，新加入的玩家需要赋予uid，其余信息直接状态同步
    const uint maxBufferedFrames = 64;
    static FramePacket[] framePackets = new FramePacket[maxBufferedFrames];
    static readonly string gameStateLogPath = Path.Combine(AppContext.BaseDirectory, "server-gamestate.log");
    static ServerState serverState = ServerState.WaitingForPlayers;

    // Main -> IninNetwork -> Pump -> ProcessRequests (Connection, Sync) -> SendFrame -> Simulate -> next loop
    public static void Main()
    {
        if (enableGameStateFileLog)
        {
            GameStateFileLogger.Reset(gameStateLogPath);
            Console.WriteLine($"[Server] GameState log path: {gameStateLogPath}");
        }
        network.Initialize(
            OnConnectionRequest,
            OnInputPacketReceived
        );

        Stopwatch stopwatch = Stopwatch.StartNew();
        double accumulatedTime = 0;
        long lastTime = stopwatch.ElapsedTicks;
        
        while (true)
        {
            // network.ReceivePacket();
            while (network.TryReceivePacket()) {}
            ProcessConnectionRequests();
            ProcessSyncRequests();

            long currentTime = stopwatch.ElapsedTicks;
            double deltaTime = (double)(currentTime - lastTime) / Stopwatch.Frequency;
            lastTime = currentTime;
            accumulatedTime += deltaTime;

            while (serverState == ServerState.Running && accumulatedTime >= fixedTimeStepSeconds)
            {
                accumulatedTime -= fixedTimeStepSeconds;
                Tick();
            }

            Thread.Sleep(1);
        }
    }

    static void InitSimulator(ClientPos[] initialPositions)
    {
        EntityData entityData = LoadEntityData();
        simulator.SetPlayerState(CreatePlayerStates(initialPositions));
        simulator.SetEntityState(entityData.states);
        simulator.SetEntityMotionConfigs(entityData.motionConfigs);
        simulator.CaptureGameState(currentTick);
        LogGameState("initial", currentTick);
    }

    static void Tick()
    {
        ProcessPendingJoinsForTick(currentTick);

        if (!CanAdvanceTick(currentTick, simulator.playerStates.Keys, inputsByTick))
        {
            return;
        }

        FramePacket framePacket = GetFramePacket(currentTick, simulator.playerStates.Keys, inputsByTick, framePackets);

        foreach (IPEndPoint client in clients.Values)
        {
            network.SendPacket(PacketType.Frame, PacketCodec.FramePacketToBytes(framePacket), client);
        }

        simulator.SimulateFrame(framePacket);
        LogGameState("simulate", currentTick);

        inputsByTick.Remove(currentTick);
        currentTick++;
    }

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

        ClientPos[] initialPositions = BuildClientPositions();
        foreach (var connection in pendingConnections)
        {
            SaveClientIPEP(connection.Value.ClientId, connection.Key);
            SendConnectionAck(connection.Value.ClientId, initialPositions, connection.Key);
        }

        InitSimulator(initialPositions);
        pendingConnections.Clear();
        serverState = ServerState.Running;
    }

    static void ProcessRunningConnections()
    {
        foreach (var connection in pendingConnections)
        {
            uint clientId = connection.Value.ClientId;
            if (clients.ContainsKey(clientId) && simulator.playerStates.ContainsKey(clientId))
            {
                AcceptReconnect(clientId, connection.Key);
                continue;
            }

            AcceptRunningJoin(clientId, connection.Value.Position, connection.Key);
        }

        pendingConnections.Clear();
    }

    static void ProcessSyncRequests()
    {
        int handledRequests = 0;
        while (syncRequests.Count > 0)
        {
            if (handledRequests >= MaxRequestHandlePerTick)
            {
                Console.WriteLine($"[Server] Reached max sync requests per tick, remaining requests will be handled in next ticks.");
                break;
            }

            StateSyncRequest request = syncRequests.Dequeue();
            if (!clients.TryGetValue(request.ClientId, out IPEndPoint? clientEndpoint))
            {
                continue;
            }

            GameState gameState = simulator.gameStateHistory[request.SyncTick];
            if (gameState.playerStates == null || gameState.entityStates == null)
            {
                continue;
            }

            StateSyncPacket packet = new StateSyncPacket
            {
                tick = request.SyncTick,
                playerCount = gameState.playerStates?.Length ?? 0,
                entityCount = gameState.entityStates?.Length ?? 0,
                gameState = gameState
            };

            network.SendPacket(PacketType.StateSync, PacketCodec.StateSyncPacketToBytes(packet), clientEndpoint);
            ResendBufferedFrames(clientEndpoint, request.SyncTick + 1, currentTick == 0 ? 0 : currentTick - 1);
            handledRequests++;
        }
    }

    static void ProcessInputPackets()
    {
        // 转换输入包到FramePacket
    }

    static ClientPos[] BuildClientPositions()
    {
        return pendingConnections.Values.OrderBy(connection => connection.ClientId).Select(connection => new ClientPos
        {
            id = connection.ClientId,
            position = connection.Position
        }).ToArray();
    }

    static void AcceptReconnect(uint clientId, IPEndPoint remote)
    {
        SaveClientIPEP(clientId, remote);
        SendConnectionAck(clientId, Array.Empty<ClientPos>(), remote);
        AddSyncRequest(clientId, GetStateSyncTick());
        Console.WriteLine($"[Server] Client {clientId} reconnected from {remote.Address}:{remote.Port}.");
    }

    static void AcceptRunningJoin(uint clientId, Vector3i spawnPosition, IPEndPoint remote)
    {
        SaveClientIPEP(clientId, remote);
        SendConnectionAck(clientId, Array.Empty<ClientPos>(), remote);

        uint syncTick = GetStateSyncTick();
        uint joinTick = currentTick + inputDelay + 1;
        pendingJoins.Enqueue(new PendingJoin(clientId, spawnPosition, joinTick));
        AddSyncRequest(clientId, syncTick);
        BroadcastPlayerJoin(clientId, spawnPosition, joinTick);

        Console.WriteLine($"[Server] Client {clientId} joined from {remote.Address}:{remote.Port}; syncTick={syncTick}, joinTick={joinTick}.");
    }

    static void SaveClientIPEP(uint clientId, IPEndPoint remote)
    {
        clients[clientId] = new IPEndPoint(remote.Address, remote.Port);
    }

    static void SendConnectionAck(uint clientId, ClientPos[] positions, IPEndPoint remote)
    {
        ACKPacket packet = new ACKPacket
        {
            clientId = clientId,
            clientPos = positions
        };
        network.SendPacket(PacketType.ACK, PacketCodec.ACKPacketToBytes(packet), remote);
    }

    static void AddSyncRequest(uint id, uint syncTick)
    {
        syncRequests.Enqueue(new StateSyncRequest(id, syncTick));
    }

    static void BroadcastPlayerJoin(uint clientId, Vector3i spawnPosition, uint joinTick)
    {
        PlayerJoinPacket packet = new PlayerJoinPacket
        {
            clientId = clientId,
            joinTick = joinTick,
            spawnPosition = spawnPosition
        };
        byte[] payload = PacketCodec.PlayerJoinPacketToBytes(packet);

        foreach (IPEndPoint client in clients.Values)
        {
            network.SendPacket(PacketType.PlayerJoin, payload, client);
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

            simulator.SetPlayerState([CreatePlayerState(join.ClientId, join.SpawnPosition)]);
            simulator.CaptureGameState(tick);
            Console.WriteLine($"[Server] Applied player join clientId={join.ClientId}, tick={tick}.");
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

    // 快速补齐，客户端处要做加速
    static void ResendBufferedFrames(IPEndPoint remote, uint fromTick, uint toTick)
    {
        if (fromTick > toTick)
        {
            return;
        }

        for (uint tick = fromTick; tick <= toTick; tick++)
        {
            FramePacket framePacket = framePackets[tick % framePackets.Length];
            if (framePacket.inputs == null || framePacket.tick != tick)
            {
                continue;
            }

            network.SendPacket(PacketType.Frame, PacketCodec.FramePacketToBytes(framePacket), remote);
        }
    }

    static bool CanAdvanceTick(
        uint tick,
        IEnumerable<uint> activeClientIds,
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick)
    {
        uint[] clientIds = activeClientIds.ToArray();
        if (clientIds.Length == 0)
        {
            return false;
        }

        if (tick < inputDelay)
        {
            return true;
        }

        if (!inputsByTick.TryGetValue(tick, out Dictionary<uint, InputPacket>? tickInputs))
        {
            return false;
        }

        foreach (uint clientId in clientIds)
        {
            if (!tickInputs.ContainsKey(clientId))
            {
                return false;
            }
        }

        return true;
    }

    // 维护一个需要同步状态的客户端列表，收到连接请求时加入，把所有需要处理的包体都放到不同的列表里处理
    static void OnConnectionRequest(byte[] data, IPEndPoint remote)
    {
        ACKPacket packet = PacketCodec.ReadACKPacketBody(data);
        Vector3i playerPosition = packet.clientPos.Length > 0
            ? packet.clientPos[0].position
            : Vector3i.Zero;

        // 重连
        if (packet.clientId != 0 && clients.ContainsKey(packet.clientId))
        {
            pendingConnections[remote] = new PendingClient(packet.clientId, playerPosition);
            return;
        }
        // 多发
        if (pendingConnections.ContainsKey(remote)) {
            return;
        }
        // 新连接
        connectedClientCount++;
        uint clientId = connectedClientCount;
        pendingConnections[remote] = new PendingClient(clientId, playerPosition);
    }

    static void OnInputPacketReceived(byte[] data, IPEndPoint remote)
    {
        InputPacket packet;
        try
        {
            packet = PacketCodec.ReadInputPacketBody(data);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Invalid packet from {remote.Address}:{remote.Port}: {exception.Message}");
            return;
        }

        if (!clients.ContainsKey(packet.clientId))
        {
            return;
        }

        CacheInput(inputsByTick, packet);
    }

    static void CacheInput(Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick, InputPacket packet)
    {
        if (!inputsByTick.TryGetValue(packet.tick, out Dictionary<uint, InputPacket>? tickInputs))
        {
            tickInputs = new Dictionary<uint, InputPacket>();
            inputsByTick[packet.tick] = tickInputs;
        }

        if (packet.commandType == CommandType.None &&
            tickInputs.TryGetValue(packet.clientId, out InputPacket cachedInput) &&
            cachedInput.commandType != CommandType.None)
        {
            return;
        }

        tickInputs[packet.clientId] = packet;
    }

    static FramePacket GetFramePacket(
        uint currentTick,
        IEnumerable<uint> activeClientIds,
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick,
        FramePacket[] framePackets)
    {
        if (!inputsByTick.TryGetValue(currentTick, out Dictionary<uint, InputPacket>? tickInputs))
        {
            tickInputs = new Dictionary<uint, InputPacket>();
        }

        List<InputPacket> inputs = new List<InputPacket>();
        foreach (uint clientId in activeClientIds.OrderBy(id => id))
        {
            if (tickInputs.TryGetValue(clientId, out InputPacket input))
            {
                inputs.Add(input);
            }
            else
            {
                inputs.Add(new InputPacket
                {
                    clientId = clientId,
                    tick = currentTick,
                    inputPos = new Vector3i { x = 0, y = 0, z = 0 },
                    commandType = CommandType.None
                });
            }
        }

        FramePacket framePacket = new FramePacket
        {
            tick = currentTick,
            inputs = inputs.ToArray()
        };

        framePackets[currentTick % framePackets.Length] = framePacket;
        return framePacket;
    }

    static void LogGameState(string phase, uint tick)
    {
        if (!enableGameStateFileLog)
        {
            return;
        }

        GameStateFileLogger.Append(
            gameStateLogPath,
            "Server",
            phase,
            tick,
            simulator.gameStateHistory[tick],
            simulator.GetGameStateChecksum(tick));
    }

    static EntityData LoadEntityData()
    {
        string path = Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\Assets\Scripts\Shared\entityData.json");

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EntityData>(
            json,
            new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNameCaseInsensitive = true
            }
        );
    }

    static PlayerState[] CreatePlayerStates(ClientPos[] clientPos)
    {
        PlayerState[] players = new PlayerState[clientPos.Length];
        int index = 0;
        foreach (ClientPos client in clientPos)
        {
            players[index++] = CreatePlayerState(client.id, client.position);
        }
        return players;
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
}
