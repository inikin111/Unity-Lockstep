namespace ServerHost;

using System.Net;
using System.Net.Sockets;
using Lockstep.Packets;
public static class Network
{
    static UdpClient server = new UdpClient(5478);

    public static void SendPacket(PacketType packetType, byte[] payload, IPEndPoint remote)
    {
        byte[] header = PacketCodec.PacketHeaderToBytes(new PacketHeader { packetType = packetType });
        byte[] data = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, data, 0, header.Length);
        Buffer.BlockCopy(payload, 0, data, header.Length, payload.Length);
        SendBytes(data, remote);
    }

    public static void SendBytes(byte[] data, IPEndPoint remote)
    {
        if (server == null)
        {
            throw new InvalidOperationException("Network has not been initialized.");
        }

        server.Send(data, data.Length, remote);
    }
}