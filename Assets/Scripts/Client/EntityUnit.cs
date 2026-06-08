using UnityEngine;

[System.Serializable]
public class EntityMotionProfile
{
    [InspectorName("Dynamic motion")]
    public bool isDynamic = true;

    [Range(0, 1000)]
    public int dragPermille = 180;

    public int maxSpeedPerTick = 120;
    public int pushImpulsePerCollision = 90;

    [Range(0, 1000)]
    public int bouncinessPermille = 120;
}

public class EntityUnit : MonoBehaviour
{
    [InspectorName("Collider Settings")]
    [HideInInspector] public Vector3 colliderCenter = Vector3.zero;
    public Vector3 colliderSize = new Vector3(1, 1, 1);
    public float colliderRadius = 0.5f;

    [InspectorName("Experience Motion")]
    public EntityMotionProfile motion = new EntityMotionProfile();
    
    public uint entityId;
    public Transform unitTr => transform;

    public void UpdatePosition(Vector3i pos)
    {
        unitTr.position = unitTr.position.MoveTowards(pos.ToVector3(), Time.deltaTime * 200f);
    }

    public void SetEntityId(uint entityId)
    {
        this.entityId = entityId;
    }

    public void UpdateColliderSize(Vector3i size)
    {
        colliderSize = size.ToVector3();
    }

    public EntityMotionConfig SourceMotionConfig()
    {
        return new EntityMotionConfig
        {
            entityId = entityId,
            isDynamic = motion.isDynamic,
            dragPermille = motion.dragPermille,
            maxSpeedPerTick = motion.maxSpeedPerTick,
            pushImpulsePerCollision = motion.pushImpulsePerCollision,
            bouncinessPermille = motion.bouncinessPermille
        };
    }

    void OnDrawGizmos()
    {
        // 用不同颜色显示物体碰撞体
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + colliderCenter, colliderSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + colliderCenter, colliderRadius);
        
        // 显示物体ID
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"Entity: {entityId}");
        #endif
    }
}
