using Lockstep.Packets;
using System.Net;
using System.Net.Sockets;
using System;

public class ClientNetwork 
{
    const string ServerIP = "127.0.0.1";
    const int ServerPort = 5478;
    UdpClient server;
    IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);
    Action<uint> onClientIdAssigned;

    public bool Initialize(Action<uint> onClientIdAssigned)
    {
        this.onClientIdAssigned = onClientIdAssigned;
        ConnectToServer();
        SendConnectionRequest();
        ReceiveConnectionResponse();

        return true;
    }

    void ConnectToServer()
    {
        server = new UdpClient();
        server.Connect(ServerIP, ServerPort);
    }

    void SendConnectionRequest()
    {
        ACKPacket packet = new ACKPacket
        {
            clientId = 0 // Client ID will be assigned by server
        };
        byte[] data = PacketCodec.ACKPacketToBytes(packet);
        server.Send(data, data.Length);
    }

    void ReceiveConnectionResponse()
    {
        IPEndPoint remote = receiveEndPoint;
        byte[] data = server.Receive(ref remote);
        receiveEndPoint = remote;
        if (data.Length > 0)
        {
            ACKPacket responsePacket = PacketCodec.BytesToACKPacket(data);
            uint assignedClientId = responsePacket.clientId;
            onClientIdAssigned?.Invoke(assignedClientId);
        }
    }

    public FramePacket ReceiveFramePacket()
    {
        IPEndPoint remote = receiveEndPoint;
        byte[] data = server.Receive(ref remote);
        receiveEndPoint = remote;
        if (data.Length > 0)
        {
            FramePacket framePacket = PacketCodec.BytesToFramePacket(data);
            return framePacket;
        }
        return default;
    }

    public void SendLocalInput(InputPacket input)
    {
        byte[] data = PacketCodec.InputPacketToBytes(input);
        server.Send(data, data.Length);
    }
}