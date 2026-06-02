using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Lockstep.Packets;

public static class Program
{
    public record PendingClient(uint ClientId, Vector3i Position);

    public static void Main()
    {
        using UdpClient server = new UdpClient(5478);
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        uint connectedClientCount = 0;
        const uint maxClients = 1;
        const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
        uint currentTick = 0;
        
        Dictionary<IPEndPoint, PendingClient> pendingConnections = new();
        Dictionary<uint, IPEndPoint> clients = new Dictionary<uint, IPEndPoint>();
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick = new Dictionary<uint, Dictionary<uint, InputPacket>>();
        const uint maxBufferedFrames = 64;
        FramePacket[] framePackets = new FramePacket[maxBufferedFrames]; 

        // Stage 1: Wait for clients to connect and assign client IDs
        while (pendingConnections.Count < maxClients)
        {
            Console.WriteLine("Waiting for clients to connect...");
            if (server.Available <= 0)
            {
                Thread.Sleep(30);
                continue;
            }
            byte[] data = server.Receive(ref remote);
            if (data.Length > 0)
            {
                ACKPacket packet = PacketCodec.BytesToACKPacket(data);

                connectedClientCount++;
                uint clientId = connectedClientCount;
                Console.WriteLine($"Assigning clientId={clientId} to {remote.Address}:{remote.Port}");
                pendingConnections[remote] = new PendingClient(clientId, packet.clientPos[0].position);

                Console.WriteLine($"Received connection request from {remote.Address}:{remote.Port}");
            }
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
            server.Send(responseData, responseData.Length, connection.Key);
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

            ReceiveInputPacket(server, ref remote, clients, inputsByTick);

            if (accumulatedTime >= fixedTimeStepSeconds)
            {
                accumulatedTime -= fixedTimeStepSeconds;

                FramePacket framePacket = GetFramePacket(currentTick, clients, inputsByTick, framePackets);
                SendFramePacket(server, clients, framePacket);

                inputsByTick.Remove(currentTick);
                currentTick++;
            }

            Thread.Sleep(1);
        }
    }

    static void ReceiveInputPacket(
        UdpClient server,
        ref IPEndPoint remote,
        Dictionary<uint, IPEndPoint> clients,
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick) 
    {
        while (server.Available > 0)
        {
            byte[] data = server.Receive(ref remote);

            InputPacket packet;
            try
            {
                packet = PacketCodec.BytesToInputPacket(data);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Invalid packet from {remote.Address}:{remote.Port}: {exception.Message}");
                continue;
            }

            Console.WriteLine($"Receive input from {remote.Address}:{remote.Port}, clientId={packet.clientId}, tick={packet.tick}, input={packet.inputPos}");

            if (!clients.ContainsKey(packet.clientId))
            {
                clients[packet.clientId] = new IPEndPoint(remote.Address, remote.Port);
            }
            CacheInput(inputsByTick, packet);
        }
    }

    static void SendFramePacket(UdpClient server, Dictionary<uint, IPEndPoint> clients, FramePacket framePacket)
    {
        if (clients.Count > 0)
        {
            foreach (IPEndPoint client in clients.Values)
            {
                byte[] data = PacketCodec.FramePacketToBytes(framePacket);
                server.Send(data, data.Length, client);
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
