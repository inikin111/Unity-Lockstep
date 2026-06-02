using Lockstep.Packets;
using System.Net;
using System.Net.Sockets;
using System;
using UnityEngine;
using System.Threading.Tasks;

public class ClientNetwork 
{
    const string ServerIP = "127.0.0.1";
    const int ServerPort = 5478;
    UdpClient server;
    IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Any, 0);
    Action<uint, ClientPos[]> onClientIdAssigned;
    Action<byte[]> onFramePacketReceived;

    public bool Initialize(Action<uint, ClientPos[]> onClientIdAssigned, Action<byte[]> onFramePacketReceived, Vector3i pos)
    {
        this.onClientIdAssigned = onClientIdAssigned;
        this.onFramePacketReceived = onFramePacketReceived;
        ConnectToServer();
        SendConnectionRequest(pos);
        if (!TryReceiveConnectionResponse()) return false;

        return true;
    }

    void ConnectToServer()
    {
        Debug.Log($"Connecting to server at udp://{ServerIP}:{ServerPort}...");
        server = new UdpClient();
        server.Connect(ServerIP, ServerPort);
    }

    void SendConnectionRequest(Vector3i pos)
    {
        Debug.Log($"Sending connection request with position {pos}...");
        ClientPos[] position = new ClientPos[1];
        position[0] = new ClientPos { clientId = 0, position = pos };

        ACKPacket packet = new ACKPacket
        {
            clientId = 0,
            clientPos = position
        };
        
        byte[] data = PacketCodec.ACKPacketToBytes(packet);

        SendBytes(data);
    }

    public void SendBytes(byte[] data)
    {
        if (server == null)
        {
            throw new InvalidOperationException("ClientNetwork has not been initialized.");
        }

        server.Send(data, data.Length);
    }

    bool TryReceiveConnectionResponse()
    {
        Debug.Log("Trying to receive connection response...");
        while (server.Available >= 0)
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
                return true;
            }
        }

        return false;
    }

    async Task ReceiveFramePacketsAsync()
    {
        while (true)
        {
            if (server.Available > 0)
            {
                IPEndPoint remote = receiveEndPoint;
                byte[] data = server.Receive(ref remote);
                receiveEndPoint = remote;
                if (data.Length > 0)
                {
                    onFramePacketReceived?.Invoke(data);
                }
            }
            await Task.Yield();
        }
    }

    public bool TryReceiveFramePacket()
    {
        if (server.Available <= 0)
        {
            return false;
        }

        IPEndPoint remote = receiveEndPoint;
        byte[] data = server.Receive(ref remote);
        receiveEndPoint = remote;

        if (data.Length <= 0)
        {
            return false;
        }
        onFramePacketReceived?.Invoke(data);

        return true;
    }
}