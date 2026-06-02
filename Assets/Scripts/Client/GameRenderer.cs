using System.Collections.Generic;
using UnityEngine;

public class GameRenderer : MonoSingleton<GameRenderer>
{
    public GameObject playerPrefab;
    public Dictionary<uint, PlayerUnit> playerUnits = new Dictionary<uint, PlayerUnit>();
    public void RenderFrame(Dictionary<uint, PlayerState> playerStates)
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
        Debug.Log($"Creating player unit for clientId={clientId}");
        GameObject playerUnitObj = Instantiate(playerPrefab);
        PlayerUnit playerUnit = playerUnitObj.AddComponent<PlayerUnit>();
        playerUnit.clientId = clientId;
        return playerUnit;
    }

    public void AddLocalPlayerUnit(uint clientId, GameObject playerObject)
    {
        if (playerUnits.ContainsKey(clientId))
        {
            Debug.LogWarning($"Player unit with clientId={clientId} already exists.");
            return;
        }

        if (!playerObject.TryGetComponent(out PlayerUnit playerUnit))
        {
            playerUnit = playerObject.AddComponent<PlayerUnit>();
        }

        playerUnit.clientId = clientId;
        playerUnits[clientId] = playerUnit;
    }
}