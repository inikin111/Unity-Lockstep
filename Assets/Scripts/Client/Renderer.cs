using System.Collections.Generic;
using UnityEngine;

public class Renderer : MonoSingleton<Renderer>
{
    public GameObject playerPrefab;
    public Dictionary<uint, PlayerUnit> playerUnits = new Dictionary<uint, PlayerUnit>();
    public void WorldRendering(Dictionary<uint, PlayerState> playerStates)
    {
        foreach (var IdAndState in playerStates)
        {
            uint clientId = IdAndState.Key;
            PlayerState playerState = IdAndState.Value;

            if (!playerUnits.TryGetValue(clientId, out PlayerUnit playerUnit))
            {
                playerUnit = CreatePlayerUnit(clientId);
                playerUnits[clientId] = playerUnit;
            }

            playerUnit.UpdatePosition(playerState.localPosition);
        }
    }

    PlayerUnit CreatePlayerUnit(uint clientId)
    {
        GameObject playerUnitObj = Instantiate(playerPrefab);
        PlayerUnit playerUnit = playerUnitObj.GetComponent<PlayerUnit>();
        playerUnit.clientId = clientId;
        return playerUnit;
    }
}