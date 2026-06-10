using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Lockstep.Packets;
namespace ServerHost;

public static class Program
{
    // 服务端要处理的：
    // 服务端处理客户端的重连/中途加入
    // 服务端处理客户端的重传请求，重传超时则放弃
    public record PendingClient(uint ClientId, Vector3i Position);

    static Network? network;
    static uint connectedClientCount = 0;
    const uint maxClients = 1;
    const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    const uint inputDelay = 2;
    static readonly bool enableGameStateFileLog = false;
    static uint currentTick = 0;

    static Simulator simulator = new Simulator();
    
    static Dictionary<IPEndPoint, PendingClient> pendingConnections = new();
    static Dictionary<uint, IPEndPoint> clients = new Dictionary<uint, IPEndPoint>();
    static Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick = new Dictionary<uint, Dictionary<uint, InputPacket>>();
    const uint maxBufferedFrames = 64;
    static FramePacket[] framePackets = new FramePacket[maxBufferedFrames];
    static readonly string gameStateLogPath = Path.Combine(AppContext.BaseDirectory, "server-gamestate.log");

    public static void Main()
    {
        network = new Network();
        framePackets = new FramePacket[maxBufferedFrames];
        if (enableGameStateFileLog)
        {
            GameStateFileLogger.Reset(gameStateLogPath);
            Console.WriteLine($"[Server] GameState log path: {gameStateLogPath}");
        }

        network.Initialize(
            OnConnectionRequest,
            OnInputPacketReceived
        );

        // Stage 1: Wait for clients to connect and assign client IDs
        while (pendingConnections.Count < maxClients)
        {
            while (network.TryReceivePacket()) { }
            Thread.Sleep(30);
        }

        ClientPos[] positions = pendingConnections.Values.Select(connection => new ClientPos
        {
            id = connection.ClientId,
            position = connection.Position
        }).ToArray();

        foreach (var connection in pendingConnections)
        {
            ACKPacket responsePacket = new ACKPacket
            {
                clientId = connection.Value.ClientId,
                clientPos = positions
            };
            byte[] responseData = PacketCodec.ACKPacketToBytes(responsePacket);
            network.SendPacket(PacketType.ACK, responseData, connection.Key);
            clients[connection.Value.ClientId] = new IPEndPoint(connection.Key.Address, connection.Key.Port);
            Console.WriteLine($"Sent connection response to {connection.Key.Address}:{connection.Key.Port}, assigned clientId={connection.Value.ClientId}");
        }

        EntityData entityData = LoadEntityData();
        simulator.SetPlayerState(CreatePlayerStates(positions));
        simulator.SetEntityState(entityData.states);
        simulator.SetEntityMotionConfigs(entityData.motionConfigs);
        simulator.CaptureGameState(currentTick);
        LogGameState("initial", currentTick);

        Console.WriteLine("Server started on udp://127.0.0.1:5478");

        Stopwatch stopwatch = Stopwatch.StartNew();
        double accumulatedTime = 0;
        long lastTime = stopwatch.ElapsedTicks;

        uint lastTick = 33;
        while (true)
        {
            long currentTime = stopwatch.ElapsedTicks;
            double deltaTime = (double)(currentTime - lastTime) / Stopwatch.Frequency;
            lastTime = currentTime;
            accumulatedTime += deltaTime;
            while (network.TryReceivePacket()) { }
            if (currentTick != lastTick)
            {
                Console.WriteLine($"[Server] Tick = {currentTick}");
                lastTick = currentTick;
            }
            // Client's latest GameState is at least (server)currentTick - inputDelay
            if (accumulatedTime >= fixedTimeStepSeconds && CanAdvanceTick(currentTick, clients, inputsByTick))
            {
                accumulatedTime -= fixedTimeStepSeconds;

                FramePacket framePacket = GetFramePacket(currentTick, clients, inputsByTick, framePackets);
                SendFramePacket(network, clients, framePacket);
                
                simulator.SimulateFrame(framePacket);
                LogGameState("simulate", currentTick);

                if (currentTick >= 4)
                {
                    Console.WriteLine($"Checksum of tick {currentTick - 4}: {simulator.GetGameStateChecksum(currentTick - 4)}");
                    Console.WriteLine($"Checksum of tick {currentTick - 3}: {simulator.GetGameStateChecksum(currentTick - 3)}");
                    Console.WriteLine($"Checksum of tick {currentTick - 2}: {simulator.GetGameStateChecksum(currentTick - 2)}");
                    Console.WriteLine($"Checksum of tick {currentTick - 1}: {simulator.GetGameStateChecksum(currentTick - 1)}");
                }
                inputsByTick.Remove(currentTick);
                currentTick++;
            }

            Thread.Sleep(1);
        }
    }

    static bool CanAdvanceTick(
        uint tick,
        Dictionary<uint, IPEndPoint> clients,
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick)
    {
        if (clients.Count == 0)
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

        foreach (uint clientId in clients.Keys)
        {
            if (!tickInputs.ContainsKey(clientId))
            {
                return false;
            }
        }

        return true;
    }

    static void OnConnectionRequest(byte[] data, IPEndPoint remote)
    {
        // 检查是否已经处理过这个客户端的连接请求
        if (pendingConnections.ContainsKey(remote))
        {
            return;
        }

        // 使用 ReadACKPacketBody 跳过包头解析数据
        ACKPacket packet = PacketCodec.ReadACKPacketBody(data);

        connectedClientCount++;
        uint clientId = connectedClientCount;
        pendingConnections[remote] = new PendingClient(clientId, packet.clientPos[0].position);
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
            clients[packet.clientId] = new IPEndPoint(remote.Address, remote.Port);
        }
        CacheInput(inputsByTick, packet);
    }

    static void SendFramePacket(Network network, Dictionary<uint, IPEndPoint> clients, FramePacket framePacket)
    {
        if (clients.Count > 0)
        {
            foreach (IPEndPoint client in clients.Values)
            {
                network.SendPacket(PacketType.Frame, PacketCodec.FramePacketToBytes(framePacket), client);
            }
        }
    }

    static void CacheInput(Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick, InputPacket packet)
    {
        if (!inputsByTick.TryGetValue(packet.tick, out Dictionary<uint, InputPacket>? tickInputs))
        {
            tickInputs = new Dictionary<uint, InputPacket>();
            inputsByTick[packet.tick] = tickInputs;
        }

        tickInputs[packet.clientId] = packet;
    }

    static FramePacket GetFramePacket(
        uint currentTick,
        Dictionary<uint, IPEndPoint> clients,
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick,
        FramePacket[] framePackets)
    {
        if (!inputsByTick.TryGetValue(currentTick, out Dictionary<uint, InputPacket>? tickInputs))
        {
            tickInputs = new Dictionary<uint, InputPacket>();
        }

        List<InputPacket> inputs = new List<InputPacket>();
        foreach (var client in clients.OrderBy(c => c.Key))
        {
            if (tickInputs.TryGetValue(client.Key, out InputPacket input))
            {
                inputs.Add(input);
            }
            else
            {
                inputs.Add(new InputPacket
                {
                    clientId = client.Key,
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
            players[index++] = new PlayerState
            {
                clientId = client.id,
                commandType = CommandType.None,
                targetPosition = Vector3i.Zero,
                frameVelocity = Vector3i.Zero,
                body = new CollisionBodyState
                {
                    position = client.position,
                    colliderSize = Vector3i.One,
                    colliderRadius = 0.5f.ToFixedInt(),
                    colliderType = ColliderType.Sphere
                }
            };
        }
        return players;
    }
}
