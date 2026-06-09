using Lockstep.Packets;
using System.Net;
using System.Net.Sockets;
using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

public class ClientNetwork 
{
    const string ServerIP = "127.0.0.1";
    const int ServerPort = 5478;
    const int HandlePacketLimitPerFrame = 5;
    UdpClient server;
    IPEndPoint receiveEndPoint = new IPEndPoint(IPAddress.Parse(ServerIP), ServerPort);
    Action<byte[]> onClientIdAssigned;
    Action<byte[]> onFramePacketReceived;
    CancellationTokenSource receiveCts;
    Task receiveTask;
    public ConcurrentQueue<byte[]> pendingPackets = new ConcurrentQueue<byte[]>();

    public bool Initialize(Action<byte[]> onClientIdAssigned, Action<byte[]> onFramePacketReceived, Vector3i pos)
    {
        this.onClientIdAssigned = onClientIdAssigned;
        this.onFramePacketReceived = onFramePacketReceived;
        ConnectToServer();
        SendConnectionRequest(pos);

        return true;
    }

    void ConnectToServer()
    {
#if UNITY_EDITOR
        Debug.Log($"Connecting to server at udp://{ServerIP}:{ServerPort}...");
#endif
        server = new UdpClient();
        server.Connect(ServerIP, ServerPort);
        StartReceivingPacket();
    }

    void SendConnectionRequest(Vector3i pos)
    {
#if UNITY_EDITOR
        Debug.Log($"Sending connection request with position {pos}...");
#endif
        ClientPos[] position = new ClientPos[1];
        position[0] = new ClientPos { id = 0, position = pos };

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

    void StartReceivingPacket()
    {
        receiveCts = new CancellationTokenSource();
        receiveTask = ReceivePacket(receiveCts);
    }

    public void StopReceivingPacket()
    {
        receiveCts.Cancel();
        receiveCts.Dispose();
        receiveCts = null;
    }

    async Task ReceivePacket(CancellationTokenSource cts)
    {
        while (!cts.Token.IsCancellationRequested)
        {
            UdpReceiveResult result = await server.ReceiveAsync().ConfigureAwait(false);
            byte[] data = result.Buffer;

            if (data == null || data.Length == 0)
            {
                continue;
            }

            pendingPackets.Enqueue(data);
        }
    }

    public void PumpReceivedPackets()
    {
        int handledCount = 0;
        while (pendingPackets.TryDequeue(out byte[] data))
        {
            HandleReceivedPacket(data);
            handledCount++;
            if (handledCount >= HandlePacketLimitPerFrame)
            {
                break;
            }
        }
    }

    void HandleReceivedPacket(byte[] data)
    {
        PacketHeader header = PacketCodec.ReadPacketHeaderFromBytes(data);
#if UNITY_EDITOR
        Debug.Log($"Received packet of type {header.packetType} from server.");
#endif
        switch (header.packetType)
        {
            case PacketType.ACK:
                ReceiveConnectionResponse(data);
                break;
            case PacketType.Frame:
                ReceiveFramePacket(data);
                break;
            default:
#if UNITY_EDITOR
                Debug.LogWarning($"Received packet with unknown type: {header.packetType}");
#endif
                break;
        }
    }

    void ReceiveConnectionResponse(byte[] data)
    {
        // connectionResponseQueue.Enqueue(data);
        onClientIdAssigned?.Invoke(data);
    }

    void ReceiveFramePacket(byte[] data)
    {
        // framePacketQueue.Enqueue(data);
        onFramePacketReceived?.Invoke(data);
    }
}