using System.Collections.Generic;
using Lockstep.Packets;
using UnityEngine;

public struct PlayerState
{
    public CommandType commandType;
    public Position targetPosition;
    public Position localPosition;
}

public class Simulator
{    
    Dictionary<uint, PlayerState> playerStates = new Dictionary<uint, PlayerState>();
    public int moveSpeed = 5;

    public void StartSimulates(FramePacket framePacket)
    {
        foreach (InputPacket input in framePacket.inputs)
        {
            if (!playerStates.TryGetValue(input.clientId, out PlayerState playerState))
            {
                playerState = new PlayerState();
            }
            playerState.commandType = input.commandType;

            switch (input.commandType)
            {
                case CommandType.Move:
                    playerState.targetPosition = input.inputPos;
                    break;
                case CommandType.None:
                    break;
            }
            playerStates[input.clientId] = playerState;
        }
    }

    public void SetPlayerState(uint clientId, CommandType commandType, Position targetPosition, Position localPosition)
    {
        PlayerState state = new()
        {
            commandType = commandType,
            targetPosition = targetPosition,
            localPosition = localPosition
        };

        playerStates[clientId] = state;
    }

}