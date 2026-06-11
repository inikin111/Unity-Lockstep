using System.Collections.Generic;
using UnityEngine;

public class GlobalManager : MonoSingleton<GlobalManager>
{
    public List<EntityUnit> entities;
    EntityData entityData;

    void Awake()
    {
        entityData = GenerateEntityData();
        SerializeEntityData();
    }

    void Start()
    {
        Client.Instance.Initialize(entityData);
        GameRenderer.Instance.AddEntityUnits(entities.ToArray());
    }

    EntityData GenerateEntityData()
    {
        var entityStates = new EntityState[entities.Count];
        var motionConfigs = new EntityMotionConfig[entities.Count];
        int index = 0;
        foreach (var entity in entities)
        {
            entityStates[index] = new EntityState
            {
                entityId = entity.SetEntityId((uint)index),
                body = new CollisionBodyState
                {
                    position = entity.unitTr.position.ToVector3i() + entity.colliderCenter.ToVector3i(),
                    colliderSize = entity.colliderSize.ToVector3i(),
                    colliderRadius = entity.colliderRadius.ToFixedInt(),
                    colliderType = entity.colliderType
                }
            };
            motionConfigs[index] = entity.SourceMotionConfig();
            motionConfigs[index].entityId = (uint)index;
            index++;
        }
        return new EntityData
        {
            states = entityStates,
            motionConfigs = motionConfigs
        };
    }

    void SerializeEntityData()
    {
#if UNITY_EDITOR
        string json = JsonUtility.ToJson(entityData, true);
        string path = Application.dataPath + "/Scripts/Shared/entityData.json";
        System.IO.File.WriteAllText(path, json);
#endif
    }
}
