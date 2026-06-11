using UnityEngine;
using Lockstep.Packets;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;

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
    static readonly bool EnableGameStateFileLog = false;
    double accumulatedTime = 0.0;
    uint currentFrame = 0;
    uint clientId = 0;
    bool isConnected = false;
    bool isInitialized = false;
    string gameStateLogPath;

    SortedDictionary<uint, FramePacket> pendingFrames = new SortedDictionary<uint, FramePacket>();
    SortedDictionary<uint, List<PlayerJoinPacket>> pendingJoins = new SortedDictionary<uint, List<PlayerJoinPacket>>();

    void Awake()
    {
        inputManager = gameObject.GetOrAdd<InputManager>();
        gameRenderer = gameObject.GetOrAdd<GameRenderer>();
        unit = gameObject.GetOrAdd<PlayerUnit>();
    }

    public void Initialize(EntityData entityData)
    {
        clientNetwork.Initialize(
            OnResponseReceived,
            OnFramePacketReceived,
            OnStateSyncPacketReceived,
            OnPlayerJoinPacketReceived,
            Pos.ToVector3i());
        this.entityData = entityData;
        if (EnableGameStateFileLog)
        {
            gameStateLogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "client-gamestate.log"));
            GameStateFileLogger.Reset(gameStateLogPath);
            Debug.Log($"[Client] GameState log path: {gameStateLogPath}");
        }
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

        accumulatedTime = accumulatedTime + Time.deltaTime;
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
        ApplyPlayerJoins(currentFrame);
        clientNetwork.SendPacket(PacketType.Input, PacketCodec.InputPacketToBytes(CreateInputPacket()));

        // 这里要注意下，一开始我直接跳过这个条件里的操作，后来经过Codex的探讨想到可能出现玩家重叠的情况，会导致服务端客户端差异
        if (currentFrame < InputDelay)
        {
            simulator.SimulateFrame(new FramePacket
            {
                tick = currentFrame,
                inputs = Array.Empty<InputPacket>()
            });
            LogGameState("simulate");
            currentFrame++;
            return false;
        }

        // 获取当前帧的FramePacket，没有维护可靠性机制
        if (!TryGetFramePacket(currentFrame, out FramePacket framePacket))
        {
            return false;
        }

        simulator.SimulateFrame(framePacket);
        LogGameState("simulate");
        Debug.Log($"[Client] Simulated frame tick={currentFrame}, Checksum={simulator.GetGameStateChecksum(currentFrame)}.");

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
        ACKPacket packet = PacketCodec.ReadACKPacketBody(data);
        clientId = packet.clientId;

        UIManager.Instance.SetClientId(clientId);
        gameRenderer.AddLocalPlayerUnit(packet.clientId, this.gameObject);

        if (packet.clientPos.Length == 0)
        {
            return;
        }

        simulator.SetPlayerState(CreatePlayerState(packet.clientPos));
        simulator.SetEntityState(entityData.states);
        simulator.SetEntityMotionConfigs(entityData.motionConfigs);
        simulator.CaptureGameState(currentFrame);
        LogGameState("initial");

        gameRenderer.RenderFrame(simulator.gameStateHistory[currentFrame]);

        isConnected = true;
    }

    PlayerState[] CreatePlayerState(ClientPos[] clientPos)
    {
        PlayerState[] players = new PlayerState[clientPos.Length];
        int index = 0;
        foreach (ClientPos client in clientPos)
        {
            players[index++] = CreatePlayerState(client.id, client.position);
        }
        return players;
    }

    void OnFramePacketReceived(byte[] framePacket)
    {
        FramePacket packet = PacketCodec.ReadFramePacketBody(framePacket);
        Debug.Log($"[Client] Received frame packet for tick={packet.tick}.");
        if (packet.tick < currentFrame)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[Client] Dropping stale frame tick={packet.tick}; currentFrame={currentFrame}.");
#endif
            return;
        }

        pendingFrames[packet.tick] = packet;
    }

    void OnStateSyncPacketReceived(byte[] data)
    {
        StateSyncPacket packet = PacketCodec.ReadStateSyncPacketBody(data);
        simulator.SetEntityMotionConfigs(entityData.motionConfigs);
        simulator.gameStateHistory[packet.tick] = packet.gameState;
        simulator.LoadGameState(packet.tick);
        currentFrame = packet.tick + 1;

        List<uint> staleTicks = pendingFrames.Keys
            .Where(tick => tick < currentFrame)
            .ToList();
        foreach (uint staleTick in staleTicks)
        {
            pendingFrames.Remove(staleTick);
        }

        gameRenderer.RenderFrame(simulator.gameStateHistory[packet.tick]);
        isConnected = true;
        Debug.Log($"[Client] State sync loaded tick={packet.tick}; nextFrame={currentFrame}.");
    }

    void OnPlayerJoinPacketReceived(byte[] data)
    {
        PlayerJoinPacket packet = PacketCodec.ReadPlayerJoinPacketBody(data);
        if (!pendingJoins.TryGetValue(packet.joinTick, out List<PlayerJoinPacket> joins))
        {
            joins = new List<PlayerJoinPacket>();
            pendingJoins[packet.joinTick] = joins;
        }

        joins.RemoveAll(join => join.clientId == packet.clientId);
        joins.Add(packet);
        Debug.Log($"[Client] Queued player join clientId={packet.clientId}, joinTick={packet.joinTick}.");
    }

    void ApplyPlayerJoins(uint tick)
    {
        if (!pendingJoins.TryGetValue(tick, out List<PlayerJoinPacket> joins))
        {
            return;
        }

        foreach (PlayerJoinPacket join in joins)
        {
            simulator.SetPlayerState(new[] { CreatePlayerState(join.clientId, join.spawnPosition) });
            Debug.Log($"[Client] Applied player join clientId={join.clientId}, tick={tick}.");
        }

        pendingJoins.Remove(tick);
    }

    PlayerState CreatePlayerState(uint id, Vector3i position)
    {
        return new PlayerState
        {
            clientId = id,
            commandType = CommandType.None,
            targetPosition = Vector3i.Zero,
            frameVelocity = Vector3i.Zero,
            body = new CollisionBodyState
            {
                position = position,
                colliderSize = Vector3i.One,
                colliderRadius = 0.5f.ToFixedInt(),
                colliderType = ColliderType.Sphere
            }
        };
    }

    // 感谢伟大的QHW / inikin111 / 卧龙锅巴 给我指出了这里的操作
    void LogGameState(string phase)
    {
        if (!EnableGameStateFileLog)
        {
            return;
        }

        GameStateFileLogger.Append(
            gameStateLogPath,
            "Client",
            phase,
            currentFrame,
            simulator.gameStateHistory[currentFrame],
            simulator.GetGameStateChecksum(currentFrame));
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
