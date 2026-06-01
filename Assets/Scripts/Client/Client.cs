using UnityEngine;
using Lockstep.Packets;

public class Client : MonoSingleton<Client>
{
    InputManager inputManager => GetComponent<InputManager>();
    // TickScheduler tickScheduler => GetComponent<TickScheduler>();
    Vector3 Pos => gameObject.transform.position;
    int GetIntX() => Mathf.RoundToInt(Pos.x * Scale);
    int GetIntY() => Mathf.RoundToInt(Pos.y * Scale);
    int GetIntZ() => Mathf.RoundToInt(Pos.z * Scale);
    readonly ClientNetwork clientNetwork = new ClientNetwork();
    readonly Simulator simulator = new Simulator();
    public const int Scale = 1000;
    public const double FixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
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
            if (!clientNetwork.Initialize(OnResponseReceived, Vector3i.FromVector3(Pos)))
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
            simulator.SimulateTick(latestFramePacket);

            accumulatedTime -= FixedTimeStepSeconds;
        }
    }

    InputPacket CreateInputPacket()
    {
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
        simulator.SetPlayerState(clientId: clientId,
                                 commandType: CommandType.None,
                                 targetPosition:    default,
                                 localPosition:     new Vector3i(GetIntX(), GetIntY(), GetIntZ())
        );
        simulator.SetGameState(positions);
    }
}