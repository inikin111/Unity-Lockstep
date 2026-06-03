using UnityEngine;
using Lockstep.Packets;
using System.Collections.Generic;

public class Client : MonoSingleton<Client>
{
    // 客户端向服务端重连/中途加入
    // 客户端未收到ACKPacket -> 客户端重试连接，超过次数则放弃
    // 客户端未收到FramePacket -> 等待超时后重连转 case 1
    // 客户端收到乱序FramePacket -> 发起重传请求，重传超时放弃
    InputManager inputManager;
    GameRenderer gameRenderer;
    Vector3 Pos => gameObject.transform.position;
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    const uint InputDelay = 2;
    const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;
    uint currentFrame = 0;
    uint clientId = 0;
    bool isConnected = false;
    int retryCount = 0;
    const int maxRetryCount = 3;

    Queue<FramePacket> pendingFrames = new Queue<FramePacket>();

    void Awake()
    {
        inputManager = GetComponent<InputManager>();
        gameRenderer = GetComponent<GameRenderer>();
    }
    
    void Update()
    {
        accumulatedTime += Time.deltaTime;

        if (!isConnected && accumulatedTime >= 3.0)
        {
            accumulatedTime -= 3.0;
            // 根据当前状态判断一下是不是需要连接回复，如果中途加入直接同步状态了
            if (!clientNetwork.Initialize(OnResponseReceived, OnFramePacketReceived, Pos.ToVector3i()))
            {
                retryCount++;
                if (retryCount == maxRetryCount) EndGame();
                return;
            }
            isConnected = true;
            retryCount = 0;
            accumulatedTime = 0.0;
        }

        if (!isConnected)
        {
            return;
        }

        while (accumulatedTime >= FixedTimeStepSeconds)
        {
            accumulatedTime -= FixedTimeStepSeconds;
            if (Tick())
            {
                UIManager.Instance.UpdateFrame(currentFrame);
                currentFrame++;
            }
            gameRenderer.RenderFrame(simulator.playerStates);
        }
    }

    bool Tick()
    {
        // 需要校验framePacket是否连续
        clientNetwork.SendPacket(PacketType.Input, PacketCodec.InputPacketToBytes(CreateInputPacket()));

        Debug.Log($"[Client] Sent input for tick={currentFrame} to server.");
        if (currentFrame < InputDelay)
        {
            currentFrame++;
            return false;
        }
        if (!clientNetwork.TryReceivePacket())
        {
            retryCount++;
            // if (retryCount == maxRetryCount) EndGame(); 断开连接
            return false;
        }
        retryCount = 0;
        // 改成获取当前帧的FramePacket，ai说：如果没有则继续等待（当前帧丢包了，等下一帧的FramePacket过来时再丢弃掉），如果等了很久都没有收到当前帧的FramePacket，则发起重传请求
        if (!TryGetLatestFramePacket(out FramePacket framePacket))
        {
            return false;
        }

        simulator.SimulateFrame(framePacket);
        
        Debug.Log($"[Client] Tick={framePacket.tick} simulated and rendered.");
        return true;
    }

    InputPacket CreateInputPacket()
    {
        Debug.Log($"ClientID: {clientId}");
        if (!inputManager.ReadInput(out Vector3i inputPosition))
        {
            return new InputPacket
            {
                clientId = clientId,
                tick = currentFrame + InputDelay,
                inputPos = inputPosition,
                commandType = CommandType.None
            };
        }

        return new InputPacket
        {
            clientId = clientId,
            tick = currentFrame + InputDelay,
            inputPos = inputPosition,
            commandType = CommandType.Move
        };
    }

    bool TryGetLatestFramePacket(out FramePacket framePacket)
    {
        if (pendingFrames.Count <= 0)
        {
            framePacket = default;
            return false;
        }

        framePacket = pendingFrames.Dequeue();
        return true;
    }

    void OnResponseReceived(byte[] data)
    {
        Debug.Log("Received connection response from server.");
        ACKPacket packet = PacketCodec.ReadACKPacketBody(data);
        clientId = packet.clientId;
        Debug.Log($"Assigned clientId={clientId} by server.");
        UIManager.Instance.SetClientId(clientId);

        foreach (var pos in packet.clientPos)
        {
            Debug.Log($"Received client position from server. clientId={pos.clientId}, position=({pos.X}, {pos.Y}, {pos.Z})");
        }
        simulator.SetGameState(packet.clientPos);
        gameRenderer.AddLocalPlayerUnit(packet.clientId, this.gameObject);
        gameRenderer.RenderFrame(simulator.playerStates);
    }

    void OnFramePacketReceived(byte[] framePacket)
    {
        pendingFrames.Enqueue(PacketCodec.ReadFramePacketBody(framePacket));
    }

    void EndGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
