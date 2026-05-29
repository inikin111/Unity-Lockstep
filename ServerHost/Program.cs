using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Lockstep.Packets;

public static class Program
{
    public static void Main()
    {
        using UdpClient server = new UdpClient(5478);
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        uint connectedClientCount = 0;
        const uint maxClients = 1;

        // Stage 1: Wait for clients to connect and assign client IDs
        while (connectedClientCount < maxClients)
        {
            ReceiveConnectionRequest(server, ref remote);
            uint clientId = connectedClientCount + 1;

            SendConnectionResponse(server, remote, clientId);

            Console.WriteLine($"Connection established from {remote.Address}:{remote.Port}, assigned clientId={clientId}");

            connectedClientCount++;
        }
        uint currentTick = 0;

        Dictionary<uint, IPEndPoint> clients = new Dictionary<uint, IPEndPoint>();
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick = new Dictionary<uint, Dictionary<uint, InputPacket>>();
        const uint maxBufferedFrames = 64;
        FramePacket[] framePackets = new FramePacket[maxBufferedFrames]; 

        Console.WriteLine("Server started on udp://127.0.0.1:5478");

        while (true)
        {
            // Stage 2: Receive input packets
            ReceiveInputPacket(server, ref remote, clients, inputsByTick);

            // Stage 3: Complete frame packets
            if (!TryGetCompleteFramePacket(currentTick, clients, inputsByTick, framePackets, out FramePacket framePacket))
            {
                Thread.Sleep(1);
                continue;
            }

            // Stage 4: Broadcast frame packets
            SendFramePacket(server, clients, framePacket);

            inputsByTick.Remove(currentTick);
            currentTick++;
        }
    }

    static void ReceiveConnectionRequest(UdpClient server, ref IPEndPoint remote)
    {
        while (true)
        {
            byte[] data = server.Receive(ref remote);
            if (data.Length > 0)
            {
                return;
            }
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

            clients[packet.clientId] = new IPEndPoint(remote.Address, remote.Port);
            CacheInput(inputsByTick, packet);
        }
    }

    static void SendConnectionResponse(UdpClient server, IPEndPoint remote, uint clientId)
    {
        ACKPacket responsePacket = new ACKPacket
        {
            clientId = clientId
        };

        byte[] responseData = PacketCodec.ACKPacketToBytes(responsePacket);
        server.Send(responseData, responseData.Length, remote);
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

    static bool TryGetCompleteFramePacket(
        uint currentTick,
        Dictionary<uint, IPEndPoint> clients,
        Dictionary<uint, Dictionary<uint, InputPacket>> inputsByTick,
        FramePacket[] framePackets,
        out FramePacket framePacket)
    {
        framePacket = default;

        if (clients.Count == 0)
        {
            return false;
        }

        if (!inputsByTick.TryGetValue(currentTick, out Dictionary<uint, InputPacket>? tickInputs))
        {
            return false;
        }

        if (tickInputs.Count < clients.Count)
        {
            return false;
        }

        framePacket = new FramePacket
        {
            tick = currentTick,
            inputs = tickInputs.Values.OrderBy(input => input.clientId).ToArray()
        };

        framePackets[currentTick % framePackets.Length] = framePacket;
        return true;
    }
}
