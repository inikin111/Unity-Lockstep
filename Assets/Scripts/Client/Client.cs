using UnityEngine;
using Lockstep.Packets;

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

    public FramePacket latestFramePacket;

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
            if (!clientNetwork.Initialize(OnResponseReceived, Vector3i.FromVector3(Pos)))
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

        accumulatedTime += Time.deltaTime;
        while (accumulatedTime >= FixedTimeStepSeconds)
        {
            clientNetwork.SendLocalInput(CreateInputPacket());
            latestFramePacket = clientNetwork.ReceiveFramePacket();
            simulator.SimulateFrame(latestFramePacket);
            gameRenderer.WorldRendering(simulator.playerStates);

            accumulatedTime -= FixedTimeStepSeconds;
        }
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

    void OnResponseReceived(uint uid, ClientPos[] positions)
    {
        clientId = uid;
        // 罪魁祸首是Position
        foreach (var pos in positions)
        {
            Debug.Log($"Received client position from server. clientId={pos.clientId}, position=({pos.X}, {pos.Y}, {pos.Z})");
        }
        simulator.SetGameState(positions); // 罪魁祸首
        gameRenderer.AddLocalPlayerUnit(uid, this.gameObject);
        // Debug.Log($"simulator player states count: {simulator.playerStates.Count}");
        // Debug.Log($"Simulator player clientId: {simulator.playerStates[0]}, position: {simulator.playerStates[0].localPosition}");
        gameRenderer.WorldRendering(simulator.playerStates);
    }

    void EndEditor()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}