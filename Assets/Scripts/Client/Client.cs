using UnityEngine;
using Lockstep.Packets;
using System.Collections.Generic;

public class Client : MonoSingleton<Client>
{
    // 客户端向服务端重连/中途加入
    // 客户端未收到ACKPacket -> 客户端重试连接，超过次数则放弃
    // 客户端未收到FramePacket -> 等待超时后重连转 case 1
    // 客户端收到乱序FramePacket -> 发起重传请求，重传超时放弃
    public EntityUnit[] entities;
    InputManager inputManager;
    GameRenderer gameRenderer;
    PlayerUnit unit;
    Vector3 Pos => gameObject.transform.position;
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    const uint InputDelay = 2;
    const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;
    uint currentFrame = 0;
    uint clientId = 0;
    bool isConnected = false;
    bool isInitialized = false;
    int retryCount = 0;
    const int maxRetryCount = 3;

    SortedDictionary<uint, FramePacket> pendingFrames = new SortedDictionary<uint, FramePacket>();

    void Awake()
    {
        inputManager = gameObject.GetOrAdd<InputManager>();
        gameRenderer = gameObject.GetOrAdd<GameRenderer>();
        unit= gameObject.GetOrAdd<PlayerUnit>();
    }
    
    void Update()
    {
        if (!isInitialized)
        {
            // 根据当前状态判断一下是不是需要连接回复，如果中途加入直接同步状态了
            clientNetwork.Initialize(OnResponseReceived, OnFramePacketReceived, Pos.ToVector3i());
            isInitialized = true;
        }

        clientNetwork.PumpReceivedPackets();
        if (!isConnected) return;

        accumulatedTime += Time.deltaTime;
        while (accumulatedTime >= FixedTimeStepSeconds)
        {
            accumulatedTime -= FixedTimeStepSeconds;
            if (Tick())
            {
                gameRenderer.RenderFrame(simulator.gameStateHistory[currentFrame]);
                UIManager.Instance.UpdateFrame(currentFrame);
                currentFrame++;
            }
        }
    }

    bool Tick()
    {
        // 需要校验framePacket是否连续
        clientNetwork.SendPacket(PacketType.Input, PacketCodec.InputPacketToBytes(CreateInputPacket()));
#if UNITY_EDITOR
        Debug.Log($"[Client] Sent input for tick={currentFrame} to server.");
#endif
        if (currentFrame < InputDelay)
        {
            simulator.CaptureGameState(currentFrame);
            currentFrame++;
            return false;
        }
        retryCount = 0;
        // 改成获取当前帧的FramePacket，ai说：如果没有则继续等待（当前帧丢包了，等下一帧的FramePacket过来时再丢弃掉），如果等了很久都没有收到当前帧的FramePacket，则发起重传请求
        if (!TryGetFramePacket(currentFrame, out FramePacket framePacket))
        {
            return false;
        }

        simulator.SimulateFrame(framePacket);
#if UNITY_EDITOR
        Debug.Log($"[Client] Tick={framePacket.tick} simulated and rendered.");
        Debug.Log($"[Client] {pendingFrames.Count} pending frames remain.");
#endif
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
#if UNITY_EDITOR
        Debug.Log("Received connection response from server.");
#endif
        ACKPacket packet = PacketCodec.ReadACKPacketBody(data);
        clientId = packet.clientId;
#if UNITY_EDITOR
        Debug.Log($"Assigned clientId={clientId} by server.");
#endif
        UIManager.Instance.SetClientId(clientId);

        foreach (var pos in packet.clientPos)
        {
#if UNITY_EDITOR
            Debug.Log($"Received client position from server. clientId={pos.id}, position=({pos.X}, {pos.Y}, {pos.Z})");
#endif
        }
        simulator.SetPlayerState(CreatePlayerState(packet.clientPos));
        simulator.SetEntityState(CreateEntityStates());
        simulator.SetEntityMotionConfigs(CreateEntityMotionConfigs());
        simulator.CaptureGameState(currentFrame);
        gameRenderer.AddLocalPlayerUnit(packet.clientId, this.gameObject);
        gameRenderer.AddLocalEntityUnits(entities);
        gameRenderer.RenderFrame(simulator.gameStateHistory[currentFrame]);

        isConnected = true;
    }

    EntityState[] CreateEntityStates()
    {
        if (entities.Length == 0)
        {
            return new EntityState[0];
        }
        EntityState[] states = new EntityState[entities.Length];  
        int index = 0;
        foreach (var entity in entities)
        {
            states[index++] = new EntityState
            {
                entityId = entity.entityId,
                body = new CollisionBodyState
                {
                    position = entity.unitTr.position.ToVector3i() + entity.colliderCenter.ToVector3i(),
                    colliderSize = entity.colliderSize.ToVector3i(),
                    colliderRadius = entity.colliderRadius.ToFixedInt(),
                    colliderType = entity.colliderType
                }
            };
        }
        return states;
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

    EntityMotionConfig[] CreateEntityMotionConfigs()
    {
        if (entities.Length == 0)
        {
            return new EntityMotionConfig[0];
        }

        EntityMotionConfig[] configs = new EntityMotionConfig[entities.Length];
        int index = 0;
        foreach (var entity in entities)
        {
            configs[index++] = entity.SourceMotionConfig();
        }
        return configs;
    }

    void OnFramePacketReceived(byte[] framePacket)
    {
        FramePacket packet = PacketCodec.ReadFramePacketBody(framePacket);
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
