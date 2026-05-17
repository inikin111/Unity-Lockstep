using UnityEngine;
using System.Net;
using System.Net.Sockets;
using Lockstep.Network;

public class Client : MonoBehaviour
{
    InputManager inputManager => GetComponent<InputManager>();
    ClientTickScheduler tickScheduler => GetComponent<ClientTickScheduler>();
    uint currentInputTick = 0;
    uint assignedClientId = 0;
    bool isConnected = false;
    UdpClient client;
    
    void Start()
    {
        client = new UdpClient();
        client.Connect("127.0.0.1", 5478);
        Debug.Log("Client UDP socket initialized, target=127.0.0.1:5478");
        SendConnectionPacket();
    }

    void Update()
    {
        if (!isConnected)
        {
            ReceiveConnectionPacket();
            return;
        }

        tickScheduler.Tick();
    }

    public void SendInputPacket()
    {
        InputPacket packet = new InputPacket
        {
            clientId = assignedClientId,
            tick = currentInputTick,
            input = inputManager.ReadInput()
        };
        byte[] data = PacketCodec.InputPacketToBytes(packet);
        client.Send(data, data.Length);
    
        currentInputTick++;

        // Debug.Log($"Send input tick={packet.tick}, input={packet.input}");
    }

    public void SendConnectionPacket()
    {
        ConnectionPacket packet = new ConnectionPacket
        {
            clientId = 0 // Client ID will be assigned by server
        };
        byte[] data = PacketCodec.ConnectionPacketToBytes(packet);
        client.Send(data, data.Length);
        Debug.Log("Send connection packet");
    }

    void ReceiveConnectionPacket()
    {
        if (isConnected) 
            return;
        int availableBytes = client.Available;
        while (availableBytes > 0)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.Receive(ref remote);
            availableBytes -= data.Length;

            if (data.Length != sizeof(uint))
            {
                continue;
            }

            ConnectionPacket packet = PacketCodec.BytesToConnectionPacket(data);
            assignedClientId = packet.clientId;
            isConnected = true;

            Debug.Log($"Receive connection packet, assignedClientId={assignedClientId}");
        }
    }

    public void ReceiveFramePacket()
    {
        int availableBytes = client.Available;
        while (availableBytes > 0)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = client.Receive(ref remote);
            availableBytes -= data.Length;
            FramePacket framePacket = PacketCodec.BytesToFramePacket(data);
            Debug.Log($"Receive frame tick={framePacket.tick}, inputCount={framePacket.inputs.Length}");
        }
    }
}