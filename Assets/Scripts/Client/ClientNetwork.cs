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
    Action<byte[]> onClientIdAssigned;
    Action<byte[]> onFramePacketReceived;

    public bool Initialize(Action<byte[]> onClientIdAssigned, Action<byte[]> onFramePacketReceived, Vector3i pos)
    {
        this.onClientIdAssigned = onClientIdAssigned;
        this.onFramePacketReceived = onFramePacketReceived;
        ConnectToServer();
        SendConnectionRequest(pos);
        
        // 持续尝试接收响应，直到收到为止
        // 服务端需要等待所有客户端连接完成后才会发送响应
        while (!TryReceivePacket())
        {
            System.Threading.Thread.Sleep(100);
        }

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

        SendPacket(PacketType.ACK, PacketCodec.ACKPacketToBytes(packet));
    }

    public void SendPacket(PacketType packetType, byte[] payload)
    {
        byte[] header = PacketCodec.PacketHeaderToBytes(new PacketHeader { packetType = packetType });
        byte[] data = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(header, 0, data, 0, header.Length);
        Buffer.BlockCopy(payload, 0, data, header.Length, payload.Length);
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

    public bool TryReceivePacket()
    {
        if (server.Available <= 0)
        {
            return false;
        }

        byte[] data = server.Receive(ref receiveEndPoint);

        PacketHeader header = PacketCodec.ReadPacketHeaderFromBytes(data);

        switch (header.packetType)
        {
            case PacketType.ACK:
                ReceiveConnectionResponse(data);
                break;
            case PacketType.Frame:
                ReceiveFramePacket(data);
                break;
            default:
                Debug.LogWarning($"Received packet with unknown type: {header.packetType}");
                return false;
        }

        return true;
    }

    void ReceiveConnectionResponse(byte[] data)
    {
        onClientIdAssigned?.Invoke(data);
    }

    void ReceiveFramePacket(byte[] data)
    {
        onFramePacketReceived?.Invoke(data);
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
                onClientIdAssigned?.Invoke(data);
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