using UnityEngine;
using Lockstep.Packets;

public class Client : MonoSingleton<Client>
{
    InputManager inputManager => GetComponent<InputManager>();
    // TickScheduler tickScheduler => GetComponent<TickScheduler>();
    Vector3 Pos => gameObject.transform.position;
    const int FixedPointMultiplier = 1000;
    int GetFixedX() => Mathf.RoundToInt(Pos.x * FixedPointMultiplier);
    int GetFixedY() => Mathf.RoundToInt(Pos.y * FixedPointMultiplier);
    int GetFixedZ() => Mathf.RoundToInt(Pos.z * FixedPointMultiplier);
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;
    uint currentInputTick = 0;
    uint clientId = 0;
    bool isConnected = false;

    public FramePacket latestFramePacket;
    
    void Update()
    {
        accumulatedTime += Time.deltaTime;
        if (!isConnected && accumulatedTime >= 3.0)
        {
            accumulatedTime -= 3.0;
            if (!clientNetwork.Initialize(OnResponseReceived, new Position { x = GetFixedX(), y = GetFixedY(), z = GetFixedZ() }))
            {
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
            simulator.StartSimulates(latestFramePacket);

            accumulatedTime -= FixedTimeStepSeconds;
        }
    }

    InputPacket CreateInputPacket()
    {
        if (!inputManager.ReadInput(out Position inputPosition))
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
        simulator.SetPlayerState(clientId:          clientId,
                                 commandType:       CommandType.None,
                                 targetPosition:    default,
                                 localPosition:     new Position { x = GetFixedX(),
                                                                   y = GetFixedY(),
                                                                   z = GetFixedZ() }
        );
        simulator.SetGameState(positions);
    }
}