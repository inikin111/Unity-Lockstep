using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameRenderer : MonoSingleton<GameRenderer>
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] GameObject entityPrefab;
    Dictionary<uint, PlayerUnit> playerUnits = new Dictionary<uint, PlayerUnit>();
    Dictionary<uint, EntityUnit> entityUnits = new Dictionary<uint, EntityUnit>();
    HashSet<uint> activePlayerIds = new HashSet<uint>();
    HashSet<uint> activeEntityIds = new HashSet<uint>();
    
    public void RenderFrame(GameState gameState)
    {
        RenderPlayers(gameState.playerStates);
        RenderEntities(gameState.entityStates);
        CleanupInactive();
    }
    
    private void RenderPlayers(PlayerState[] playerStates)
    {
        if (playerStates == null) return;
        
        foreach (PlayerState playerState in playerStates)
        {
            uint clientId = playerState.clientId;
            
            activePlayerIds.Add(clientId);
            
            if (!playerUnits.TryGetValue(clientId, out PlayerUnit playerUnit))
            {
                playerUnit = CreatePlayerUnit(clientId);
                playerUnits[clientId] = playerUnit;
            }

            playerUnit.UpdatePosition(playerState.localPosition);
            
            playerUnit.UpdateColliderSize(playerState.colliderSizes);
        }
    }
    
    private void RenderEntities(EntityState[] entityStates)
    {
        if (entityStates == null) return;
        
        foreach (EntityState entityState in entityStates)
        {
            uint entityId = entityState.entityId;
            activeEntityIds.Add(entityId);
            
            if (!entityUnits.TryGetValue(entityId, out EntityUnit entityUnit))
            {
                entityUnit = CreateEntityUnit(entityId);
                entityUnits[entityId] = entityUnit;
            }
            
            entityUnit.UpdatePosition(entityState.position);
            entityUnit.UpdateColliderSize(entityState.colliderSize);
        }
    }
    

    PlayerUnit CreatePlayerUnit(uint clientId)
    {
        Debug.Log($"Creating player unit for clientId={clientId}");
        GameObject playerUnitObj = Instantiate(playerPrefab);
        PlayerUnit playerUnit = playerUnitObj.GetOrAdd<PlayerUnit>();
        playerUnit.SetClientId(clientId);
        return playerUnit;
    }
    
    EntityUnit CreateEntityUnit(uint entityId)
    {
        Debug.Log($"Creating entity unit for entityId={entityId}");
        GameObject entityUnitObj = Instantiate(entityPrefab);
        EntityUnit entityUnit = entityUnitObj.GetOrAdd<EntityUnit>();
        entityUnit.SetEntityId(entityId);
        return entityUnit;
    }

    void CleanupInactive()
    {
        uint[] ids = playerUnits.Keys.ToArray();
        foreach (uint id in ids)
        {
            if (!activePlayerIds.Contains(id))
            {
                playerUnits.Remove(id);
            }
        }
        ids = entityUnits.Keys.ToArray();
        foreach (uint id in ids)
        {
            if (!activeEntityIds.Contains(id))
            {
                entityUnits.Remove(id);
            }
        }
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

        playerUnit.SetClientId(clientId);
        playerUnits[clientId] = playerUnit;
    }
}