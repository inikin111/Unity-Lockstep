using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
    const uint maxClients = 2;
    const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    static uint currentTick = 0;
    
    static Dictionary<IPEndPoint, PendingClient> pendingConnections = new();
    static Dictionary<uint, IPEndPoint> clients = new Dictionary<uint, IPEndPoint>();
    static Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick = new Dictionary<uint, Dictionary<uint, InputPacket>>();
    const uint maxBufferedFrames = 64;
    static FramePacket[] framePackets = new FramePacket[maxBufferedFrames];

    public static void Main()
    {
        network = new Network();
        framePackets = new FramePacket[maxBufferedFrames];

        network.Initialize(
            OnConnectionRequest,
            OnInputPacketReceived
        );

        // Stage 1: Wait for clients to connect and assign client IDs
        while (pendingConnections.Count < maxClients)
        {
            Console.WriteLine("Waiting for clients to connect...");
            network.TryReceivePacket();
            Thread.Sleep(30);
        }

        ClientPos[] positions = pendingConnections.Values.Select(connection => new ClientPos
        {
            clientId = connection.ClientId,
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
            Console.WriteLine($"Sent connection response to {connection.Key.Address}:{connection.Key.Port}, assigned clientId={connection.Value.ClientId}");
        }

        Console.WriteLine("Server started on udp://127.0.0.1:5478");

        Stopwatch stopwatch = Stopwatch.StartNew();
        double accumulatedTime = 0;
        long lastTime = stopwatch.ElapsedTicks;

        while (true)
        {
            long currentTime = stopwatch.ElapsedTicks;
            double deltaTime = (double)(currentTime - lastTime) / Stopwatch.Frequency;
            lastTime = currentTime;
            accumulatedTime += deltaTime;

            network.TryReceivePacket();

            if (accumulatedTime >= fixedTimeStepSeconds)
            {
                accumulatedTime -= fixedTimeStepSeconds;

                FramePacket framePacket = GetFramePacket(currentTick, clients, inputsByTick, framePackets);
                SendFramePacket(network, clients, framePacket);

                inputsByTick.Remove(currentTick);
                currentTick++;
            }

            Thread.Sleep(1);
        }
    }

    static void OnConnectionRequest(byte[] data, IPEndPoint remote)
    {
        // 检查是否已经处理过这个客户端的连接请求
        if (pendingConnections.ContainsKey(remote))
        {
            Console.WriteLine($"Ignoring duplicate connection request from {remote.Address}:{remote.Port}");
            return;
        }

        // 使用 ReadACKPacketBody 跳过包头解析数据
        ACKPacket packet = PacketCodec.ReadACKPacketBody(data);

        connectedClientCount++;
        uint clientId = connectedClientCount;
        Console.WriteLine($"Assigning clientId={clientId} to {remote.Address}:{remote.Port}");
        pendingConnections[remote] = new PendingClient(clientId, packet.clientPos[0].position);

        Console.WriteLine($"Received connection request from {remote.Address}:{remote.Port}");
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

        Console.WriteLine($"Receive input from {remote.Address}:{remote.Port}, clientId={packet.clientId}, tick={packet.tick}, input={packet.inputPos}");

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

            Console.WriteLine($"Broadcast frame tick={framePacket.tick}, inputCount={framePacket.inputs.Length}");
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
}
