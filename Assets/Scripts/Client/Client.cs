using UnityEngine;
using Lockstep.Packets;

public class Client : MonoSingleton<Client>
{
    InputManager inputManager => GetComponent<InputManager>();
    // TickScheduler tickScheduler => GetComponent<TickScheduler>();
    Vector3 Pos => gameObject.transform.position;
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;
    uint currentInputTick = 0;
    uint assignedClientId = 0;

    public FramePacket latestFramePacket;
    
    void Update()
    {
        if (!clientNetwork.Initialize(OnResponseReceived)) { return; }

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
        if (!inputManager.ReadInput(out Lockstep.Packets.Position inputPosition))
        {
            return new InputPacket
            {
                clientId = assignedClientId,
                tick = currentInputTick++,
                inputPos = inputPosition,
                commandType = CommandType.None
            };
        }

        return new InputPacket
        {
            clientId = assignedClientId,
            tick = currentInputTick++,
            inputPos = inputPosition,
            commandType = CommandType.Move
        };
    }

    void OnResponseReceived(uint uid)
    {
        assignedClientId = uid;
        simulator.SetPlayerState(clientId:          assignedClientId,
                                 commandType:       CommandType.None,
                                 targetPosition:    default,
                                 localPosition:     new Position { x = Mathf.RoundToInt(Pos.x * 1000),
                                                                   y = Mathf.RoundToInt(Pos.y * 1000),
                                                                   z = Mathf.RoundToInt(Pos.z * 1000)}
        );
    }
}