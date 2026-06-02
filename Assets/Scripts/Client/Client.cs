using UnityEngine;
using Lockstep.Packets;
using System.Collections.Generic;

public class Client : MonoSingleton<Client>
{
    InputManager inputManager;
    GameRenderer gameRenderer;
    Vector3 Pos => gameObject.transform.position;
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    public const int Scale = 1000;
    public const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;
    uint currentInputTick = 0;
    uint clientId = 0;
    bool isConnected = false;
    int retryCount = 0;

    Queue<InputPacket> pendingInputs = new Queue<InputPacket>();
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
            if (!clientNetwork.Initialize(OnResponseReceived, OnFramePacketReceived, Pos.ToVector3i()))
            {
                retryCount++;
                if (retryCount == 3) EndEditor();
                return;
            }
            isConnected = true;
            accumulatedTime = 0.0;
        }

        if (!isConnected)
        {
            return;
        }

        while (accumulatedTime >= FixedTimeStepSeconds)
        {
            if (!Tick())
            {
                break;
            }
            accumulatedTime -= FixedTimeStepSeconds;
        }
    }

    bool Tick()
    {
        clientNetwork.SendBytes(PacketCodec.InputPacketToBytes(CreateInputPacket()));

        clientNetwork.TryReceiveFramePacket();
        if (!TryGetLatestFramePacket(out FramePacket framePacket))
        {
            return false;
        }

        simulator.SimulateFrame(framePacket);
        gameRenderer.RenderFrame(simulator.playerStates);
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
                tick = currentInputTick++,
                inputPos = inputPosition,
                commandType = CommandType.None
            };
        }

        return new InputPacket
        {
            clientId = clientId,
            tick = currentInputTick++,
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

    void OnResponseReceived(uint uid, ClientPos[] positions)
    {
        clientId = uid;

        foreach (var pos in positions)
        {
            Debug.Log($"Received client position from server. clientId={pos.clientId}, position=({pos.X}, {pos.Y}, {pos.Z})");
        }
        simulator.SetGameState(positions);
        gameRenderer.AddLocalPlayerUnit(uid, this.gameObject);
        gameRenderer.RenderFrame(simulator.playerStates);
    }

    void OnFramePacketReceived(byte[] framePacket)
    {
        pendingFrames.Enqueue(PacketCodec.BytesToFramePacket(framePacket));
    }

    void EndEditor()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
