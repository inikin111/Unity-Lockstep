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
    Action<uint, ClientPos[]> onClientIdAssigned;

    public bool Initialize(Action<uint, ClientPos[]> onClientIdAssigned, Vector3i pos)
    {
        this.onClientIdAssigned = onClientIdAssigned;
        ConnectToServer();
        SendConnectionRequest(pos);
        ReceiveConnectionResponse();

        return true;
    }

    void ConnectToServer()
    {
        server = new UdpClient();
        server.Connect(ServerIP, ServerPort);
    }

    void SendConnectionRequest(Vector3i pos)
    {
        ClientPos[] position = new ClientPos[1];
        position[0] = new ClientPos { clientId = 0, position = pos };

        ACKPacket packet = new ACKPacket
        {
            clientId = 0,
            clientPos = position
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
            ClientPos[] assignedPositions = responsePacket.clientPos;
            onClientIdAssigned?.Invoke(assignedClientId, assignedPositions);
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
        if (server == null)
        {
            throw new InvalidOperationException("ClientNetwork has not been initialized.");
        }

        byte[] data = PacketCodec.InputPacketToBytes(input);
        server.Send(data, data.Length);
    }
}