using UnityEngine;
using System.Net;
using System.Net.Sockets;
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
    
    void Start()
    {
        while (!clientNetwork.Initialize((uint id) => {assignedClientId = id;})) {}
    }

    void Update()
    {
        accumulatedTime += Time.deltaTime;
        while (accumulatedTime >= fixedTimeStepSeconds)
        {
            clientNetwork.SendInputPacket(CreateInputPacket());
            latestFramePacket = clientNetwork.ReceiveFramePacket();
            simulator.StartSimulates(latestFramePacket);

            accumulatedTime -= fixedTimeStepSeconds;
        }
    }

    InputPacket CreateInputPacket()
    {
        InputPosition inputPosition = inputManager.ReadInput();
        return new InputPacket
        {
            isValid = true,
            clientId = assignedClientId,
            tick = currentInputTick++,
            inputPos = inputPosition
        };
    }
}