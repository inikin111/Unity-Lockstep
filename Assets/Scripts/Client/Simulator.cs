using System.Collections.Generic;
using Lockstep.Packets;

public struct LocalPosition
{
    public LocalPosition(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public int x;
    public int y;
    public int z;
}

public struct PlayerState
{
    public uint clientId;
    public LocalPosition position;
}

public class Simulator
{    
    Dictionary<uint, PlayerState> playerStates = new Dictionary<uint, PlayerState>();

    public void StartSimulates(FramePacket framePacket)
    {
        foreach (InputPacket input in framePacket.inputs)
        {
            playerStates[input.clientId] = new PlayerState
            {
                clientId = input.clientId,
                position = new LocalPosition(input.inputPos.x, input.inputPos.y, input.inputPos.z)
            };
        }
    }
}