using UnityEngine;
using Lockstep.Packets;
using System.Collections.Generic;

public class Client : MonoSingleton<Client>
{
    // 客户端向服务端重连/中途加入
    // 客户端未收到ACKPacket -> 客户端重试连接，超过次数则放弃
    // 客户端未收到FramePacket -> 等待超时后重连转 case 1
    // 客户端收到乱序FramePacket -> 发起重传请求，重传超时放弃
    EntityData entityData;
    InputManager inputManager;
    GameRenderer gameRenderer;
    PlayerUnit unit;
    Vector3 Pos => gameObject.transform.position;
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    const uint InputDelay = 2;
    const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    const double MaxAccumulatedTimeSeconds = 0.25;
    const int MaxTicksPerUpdate = 4;
    double accumulatedTime = 0.0;
    uint currentFrame = 0;
    uint clientId = 0;
    bool isConnected = false;
    bool isInitialized = false;

    SortedDictionary<uint, FramePacket> pendingFrames = new SortedDictionary<uint, FramePacket>();

    void Awake()
    {
        inputManager = gameObject.GetOrAdd<InputManager>();
        gameRenderer = gameObject.GetOrAdd<GameRenderer>();
        unit = gameObject.GetOrAdd<PlayerUnit>();
    }

    public void Initialize(EntityData entityData)
    {
        clientNetwork.Initialize(OnResponseReceived, OnFramePacketReceived, Pos.ToVector3i());
        this.entityData = entityData;
        isInitialized = true;
    }
    
    void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        clientNetwork.PumpReceivedPackets();
        if (!isConnected) return;

        accumulatedTime = System.Math.Min(accumulatedTime + Time.deltaTime, MaxAccumulatedTimeSeconds);
        int simulatedTicks = 0;
        while (accumulatedTime >= FixedTimeStepSeconds && simulatedTicks < MaxTicksPerUpdate)
        {
            accumulatedTime -= FixedTimeStepSeconds;
            uint frameBeforeTick = currentFrame;
            if (Tick())
            {
                gameRenderer.RenderFrame(simulator.gameStateHistory[currentFrame]);
                UIManager.Instance.UpdateFrame(currentFrame);
                currentFrame++;
            }

            simulatedTicks++;
            if (currentFrame == frameBeforeTick)
            {
                break;
            }
        }
    }

    void OnDestroy()
    {
        clientNetwork.StopReceivingPacket();
    }

    bool Tick()
    {
        // 需要校验framePacket是否连续
        clientNetwork.SendPacket(PacketType.Input, Codec.InputPacketToBytes(CreateInputPacket()));

        if (currentFrame < InputDelay)
        {
            simulator.CaptureGameState(currentFrame);
            currentFrame++;
            return false;
        }
        // 改成获取当前帧的FramePacket，ai说：如果没有则继续等待（当前帧丢包了，等下一帧的FramePacket过来时再丢弃掉），如果等了很久都没有收到当前帧的FramePacket，则发起重传请求
        if (!TryGetFramePacket(currentFrame, out FramePacket framePacket))
        {
            return false;
        }

        simulator.SimulateFrame(framePacket);

        return true;
    }

    InputPacket CreateInputPacket()
    {
#if UNITY_EDITOR
        Debug.Log($"[Client] Creating input packet for tick={currentFrame}...");
#endif
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

    bool TryGetFramePacket(uint tick, out FramePacket framePacket)
    {
        if (!pendingFrames.TryGetValue(tick, out framePacket))
        {
            if (pendingFrames.Count > 0)
            {
                foreach (uint pendingTick in pendingFrames.Keys)
                {
#if UNITY_EDITOR
                    Debug.Log($"[Client] Waiting for frame tick={tick}; earliest pending tick={pendingTick}.");
#endif
                    break;
                }
            }
            return false;
        }

        pendingFrames.Remove(tick);
        return true;
    }

    void OnResponseReceived(byte[] data)
    {
        ACKPacket packet = Codec.ReadACKPacketBody(data);
        clientId = packet.clientId;

        UIManager.Instance.SetClientId(clientId);

        simulator.SetPlayerState(CreatePlayerState(packet.clientPos));
        simulator.SetEntityState(entityData.states);
        simulator.SetEntityMotionConfigs(entityData.motionConfigs);
        simulator.CaptureGameState(currentFrame);
        gameRenderer.AddLocalPlayerUnit(packet.clientId, this.gameObject);
        gameRenderer.RenderFrame(simulator.gameStateHistory[currentFrame]);

        isConnected = true;
    }

    PlayerState[] CreatePlayerState(ClientPos[] clientPos)
    {
        PlayerState[] players = new PlayerState[clientPos.Length];
        int index = 0;
        foreach (ClientPos client in clientPos)
        {
            players[index++] = new PlayerState
            {
                clientId = client.id,
                commandType = CommandType.None,
                targetPosition = Vector3i.Zero,
                frameVelocity = Vector3i.Zero,
                body = new CollisionBodyState
                {
                    position = client.position,
                    colliderSize = new Vector3i(300, 300, 300),
                    colliderRadius = unit.colliderRadius.ToFixedInt(),
                    colliderType = ColliderType.Sphere
                }
            };
        }
        return players;
    }

    void OnFramePacketReceived(byte[] framePacket)
    {
        FramePacket packet = Codec.ReadFramePacketBody(framePacket);
        if (packet.tick < currentFrame)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[Client] Dropping stale frame tick={packet.tick}; currentFrame={currentFrame}.");
#endif
            return;
        }

        pendingFrames[packet.tick] = packet;
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
