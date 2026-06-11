namespace ServerHost;

using System.Net;
using System.Net.Sockets;
using Lockstep.Packets;

public enum ServerState
{
    WaitingForPlayers,
    Running
}

public class Network
{
    const int ServerPort = 5478;
    UdpClient? server;
    IPEndPoint? remoteEndPoint;
    Action<byte[], IPEndPoint>? onConnectionRequest;
    Action<byte[], IPEndPoint>? onInputPacketReceived;

    public bool Initialize(Action<byte[], IPEndPoint> onConnectionRequest, Action<byte[], IPEndPoint> onInputPacketReceived)
    {
        this.onConnectionRequest = onConnectionRequest;
        this.onInputPacketReceived = onInputPacketReceived;
        
        server = new UdpClient(ServerPort);
        remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        Console.WriteLine($"Server listening on udp://127.0.0.1:{ServerPort}");
        return true;
    }

    public void ReceivePacket()
    {
        if (server == null || server.Available <= 0)
        {
            return;
        }

        byte[] data = server.Receive(ref remoteEndPoint);

        if (data.Length == 0)
        {
            return;
        }
        
        PacketHeader header = PacketCodec.ReadPacketHeaderFromBytes(data);

        switch (header.packetType)
        {
            case PacketType.ACK:
                ReceiveConnectionRequest(data, remoteEndPoint);
                break;
            case PacketType.Input:
                ReceiveInputPacket(data, remoteEndPoint);
                break;
            default:
                Console.WriteLine($"Received packet with unknown type: {header.packetType} from {remoteEndPoint.Address}:{remoteEndPoint.Port}");
                return;
        }
    }

    public bool TryReceivePacket()
    {
        if (server == null || server.Available <= 0)
        {
            return false;
        }

        if (remoteEndPoint == null)
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        }

        byte[] data = server.Receive(ref remoteEndPoint);
        
        if (data.Length == 0)
        {
            return false;
        }

        PacketHeader header = PacketCodec.ReadPacketHeaderFromBytes(data);

        switch (header.packetType)
        {
            case PacketType.ACK:
                ReceiveConnectionRequest(data, remoteEndPoint);
                break;
            case PacketType.Input:
                ReceiveInputPacket(data, remoteEndPoint);
                break;
            default:
                Console.WriteLine($"Received packet with unknown type: {header.packetType} from {remoteEndPoint.Address}:{remoteEndPoint.Port}");
                return false;
        }

        return true;
    }

    void ReceiveConnectionRequest(byte[] data, IPEndPoint remote)
    {
        Console.WriteLine($"Received connection request from {remote.Address}:{remote.Port}");
        onConnectionRequest?.Invoke(data, remote);
    }

    void ReceiveInputPacket(byte[] data, IPEndPoint remote)
    {
        onInputPacketReceived?.Invoke(data, remote);
    }

    public void SendPacket(PacketType packetType, byte[] payload, IPEndPoint remote)
    {
        byte[] header = PacketCodec.PacketHeaderToBytes(new PacketHeader { packetType = packetType });
        byte[] data = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, data, 0, header.Length);
        Buffer.BlockCopy(payload, 0, data, header.Length, payload.Length);
        SendBytes(data, remote);
    }

    public void SendBytes(byte[] data, IPEndPoint remote)
    {
        if (server == null)
        {
            throw new InvalidOperationException("Network has not been initialized.");
        }

        server.Send(data, data.Length, remote);
    }
}
