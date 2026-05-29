using UnityEngine;
using Lockstep.Packets;

public class Client : MonoSingleton<Client>
{
    InputManager inputManager => GetComponent<InputManager>();
    // TickScheduler tickScheduler => GetComponent<TickScheduler>();

    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;
    uint currentInputTick = 0;
    uint assignedClientId = 0;

    public FramePacket latestFramePacket;
    
    void Update()
    {
        if (!clientNetwork.Initialize((uint id) => {assignedClientId = id;})) { return; }

        accumulatedTime += Time.deltaTime;
        while (accumulatedTime >= fixedTimeStepSeconds)
        {
            clientNetwork.SendLocalInput(CreateInputPacket());
            latestFramePacket = clientNetwork.ReceiveFramePacket();
            simulator.StartSimulates(latestFramePacket);

            accumulatedTime -= fixedTimeStepSeconds;
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
}