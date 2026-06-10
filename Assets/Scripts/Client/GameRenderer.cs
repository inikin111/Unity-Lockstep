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
#if UNITY_EDITOR
                Debug.Log("Create new Player Unit!");
#endif
                playerUnit = CreatePlayerUnit(clientId);
                playerUnits[clientId] = playerUnit;
            }

            playerUnit.UpdatePosition(playerState.position);
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
#if UNITY_EDITOR
                Debug.Log("Create new Entity Unit!");
#endif
                entityUnit = CreateEntityUnit(entityId);
                entityUnits[entityId] = entityUnit;
            }
            
            entityUnit.UpdatePosition(entityState.position);
        }
    }
    

    PlayerUnit CreatePlayerUnit(uint clientId)
    {
#if UNITY_EDITOR
        Debug.Log($"Creating player unit for clientId={clientId}");
#endif
        GameObject playerUnitObj = Instantiate(playerPrefab);
        PlayerUnit playerUnit = playerUnitObj.GetOrAdd<PlayerUnit>();
        playerUnit.SetClientId(clientId);
        activePlayerIds.Add(clientId);
        return playerUnit;
    }
    
    EntityUnit CreateEntityUnit(uint entityId)
    {
#if UNITY_EDITOR
        Debug.Log($"Creating entity unit for entityId={entityId}");
#endif
        GameObject entityUnitObj = Instantiate(entityPrefab);
        EntityUnit entityUnit = entityUnitObj.GetOrAdd<EntityUnit>();
        entityUnit.SetEntityId(entityId);
        activeEntityIds.Add(entityId);
        return entityUnit;
    }

    void CleanupInactive()
    {
        uint[] ids = playerUnits.Keys.ToArray();
        foreach (uint id in ids)
        {
            if (!activePlayerIds.Contains(id))
            {
                GameObject.Destroy(playerUnits[id].gameObject);
                playerUnits.Remove(id);
            }
        }
        ids = entityUnits.Keys.ToArray();
        foreach (uint id in ids)
        {
            if (!activeEntityIds.Contains(id))
            {
                GameObject.Destroy(entityUnits[id].gameObject);
                entityUnits.Remove(id);
            }
        }
    }

    public void AddLocalPlayerUnit(uint clientId, GameObject playerObject)
    {
#if UNITY_EDITOR
        Debug.Log($"Adding local player unit for clientId={clientId}");
#endif
        if (playerUnits.ContainsKey(clientId))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"Player unit with clientId={clientId} already exists.");
#endif
            return;
        }

        PlayerUnit playerUnit = playerObject.GetOrAdd<PlayerUnit>();

        playerUnit.SetClientId(clientId);
        playerUnits[clientId] = playerUnit;
    }

    public void AddEntityUnits(EntityUnit[] entities)
    {
        foreach (var entity in entities)
        {
            uint entityId = entity.entityId;
            if (entityUnits.ContainsKey(entityId))
            {
                continue;
            }
            entityUnits[entityId] = entity;
        }
    }
}