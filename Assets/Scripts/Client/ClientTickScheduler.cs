using UnityEngine;

public class ClientTickScheduler : MonoBehaviour
{
    Client client => GetComponent<Client>();
    const double fixedTimeStepSeconds = 1.0 / 30.0; // 30 ticks per second
    double accumulatedTime = 0.0;

    public void Tick()
    {
        accumulatedTime += Time.deltaTime;
        while (accumulatedTime >= fixedTimeStepSeconds)
        {
            accumulatedTime -= fixedTimeStepSeconds;
            client.SendInputPacket();
            client.ReceiveFramePacket();
        }
    }
}