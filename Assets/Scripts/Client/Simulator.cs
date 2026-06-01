using System.Collections.Generic;
using System.Linq;
using Lockstep.Packets;

public struct PlayerState
{
    public bool moveFlag;
    public CommandType commandType;
    public Vector3i targetPosition;
    public Vector3i localPosition;
}

public class Simulator
{    
    Dictionary<uint, PlayerState> playerStates = new Dictionary<uint, PlayerState>();
    public const int FixedDeltaTimeMs = 33;
    public int moveSpeed = 50;

    public void SimulateTick(FramePacket framePacket)
    {
        // UnityEngine.Debug.Log($"[Simulator] Tick={framePacket.tick}, inputCount={(framePacket.inputs == null ? 0 : framePacket.inputs.Length)}, playerCount={playerStates.Count}");

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
                    playerState.moveFlag = true;
                    break;
                case CommandType.None:
                    break;
            }
            playerStates[input.clientId] = playerState;

            UnityEngine.Debug.Log($"[Simulator] Input clientId={input.clientId}, commandType={input.commandType}, target={playerState.targetPosition}");
        }

        foreach (var clientId in playerStates.Keys.ToArray())
        {
            var state = playerStates[clientId];

            if (state.moveFlag == true)
            {
                Vector3i delta = state.targetPosition - state.localPosition;
                int distanceToTarget = Vector3i.Distance(state.localPosition, state.targetPosition);

                UnityEngine.Debug.Log($"[Simulator] Move clientId={clientId}, from={state.localPosition}, target={state.targetPosition}, delta={delta}, dist={distanceToTarget}, speed={moveSpeed}");

                if (distanceToTarget <= moveSpeed)
                {
                    state.localPosition = state.targetPosition; // 到达目标位置
                    state.moveFlag = false; // 停止移动
                    UnityEngine.Debug.Log($"[Simulator] Arrived clientId={clientId}, position={state.localPosition}");
                }
                else
                {
                    Vector3i step = delta * moveSpeed / distanceToTarget;
                    state.localPosition += step;
                    UnityEngine.Debug.Log($"[Simulator] Step clientId={clientId}, step={step}, newPosition={state.localPosition}");
                }

                playerStates[clientId] = state; // 写回
            }
        }
    }

    public void SetPlayerState(uint clientId, CommandType commandType, Vector3i targetPosition, Vector3i localPosition)
    {
        PlayerState state = new()
        {
            moveFlag = commandType == CommandType.Move,
            commandType = commandType,
            targetPosition = targetPosition,
            localPosition = localPosition
        };

        playerStates[clientId] = state;
    }

    public void SetGameState(ClientPos[] clientPositions)
    {
        foreach (ClientPos clientPos in clientPositions)
        {
            if (!playerStates.TryGetValue(clientPos.clientId, out PlayerState playerState))
            {
                playerState = new PlayerState()
                {
                    moveFlag = false,
                    commandType = CommandType.None,
                    targetPosition = default,
                    localPosition = clientPos.position
                };
            }
            else
            {
                playerState.localPosition = clientPos.position;
            }

            playerStates[clientPos.clientId] = playerState;
            UnityEngine.Debug.Log($"[Simulator] Sync clientId={clientPos.clientId}, position={clientPos.position}, commandType={playerState.commandType}");
        }
    }
}