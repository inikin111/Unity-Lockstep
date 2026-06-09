using UnityEngine;

[System.Serializable]
public class EntityMotionProfile
{
    [InspectorName("Dynamic motion")]
    public bool isDynamic = true;

    [Range(-10, 1000)]
    public int dragPermille = 180;

    public int maxSpeedPerTick = 120;
    public int pushImpulsePerCollision = 90;

    [Range(0, 1000)]
    public int bouncinessPermille = 120;
}

[ExecuteAlways]
public class EntityUnit : MonoBehaviour
{
    [InspectorName("Collider Settings")]
    [HideInInspector] public ColliderType colliderType = ColliderType.Sphere;
    [HideInInspector] public Vector3 colliderCenter = Vector3.zero;
    public Vector3 colliderSize = new Vector3(1, 1, 1);
    public float colliderRadius = 0.5f;

    [InspectorName("Experience Motion")]
    public EntityMotionProfile motion = new EntityMotionProfile();
    
    [HideInInspector] public uint entityId;
    public Transform unitTr => transform;

    void Reset()
    {
        SyncColliderSettingsFromComponent();
    }

    void OnValidate()
    {
        SyncColliderSettingsInEditor();
    }

    void Update()
    {
        SyncColliderSettingsInEditor();
    }

    public void UpdatePosition(Vector3i pos)
    {
        // unitTr.position = unitTr.position.MoveTowards(pos.ToVector3(), Time.deltaTime * 200f);
        unitTr.position = pos.ToVector3();
    }

    public uint SetEntityId(uint entityId)
    {
        this.entityId = entityId;
        return entityId;
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
            isDynamic = motion.isDynamic, // 先全部当做静态的，后面再根据需要改成动态的
            dragPermille = motion.dragPermille,
            maxSpeedPerTick = motion.maxSpeedPerTick,
            pushImpulsePerCollision = motion.pushImpulsePerCollision,
            bouncinessPermille = motion.bouncinessPermille
        };
    }

    void SyncColliderSettingsFromComponent()
    {
        Vector3 scale = GetAbsoluteLossyScale();

        if (TryGetComponent(out SphereCollider sphereCollider))
        {
            colliderType = ColliderType.Sphere;
            colliderCenter = Vector3.Scale(sphereCollider.center, scale);
            colliderRadius = sphereCollider.radius * Mathf.Max(scale.x, scale.y, scale.z);
            float diameter = colliderRadius * 2f;
            colliderSize = new Vector3(diameter, diameter, diameter);
            return;
        }

        if (TryGetComponent(out BoxCollider boxCollider))
        {
            colliderType = ColliderType.Box;
            colliderCenter = Vector3.Scale(boxCollider.center, scale);
            colliderSize = Vector3.Scale(boxCollider.size, scale);
            colliderRadius = Mathf.Max(boxCollider.size.x, boxCollider.size.y, boxCollider.size.z) * 0.5f;
            colliderRadius *= Mathf.Max(scale.x, scale.y, scale.z);
            return;
        }
    }

    void SyncColliderSettingsInEditor()
    {
        if (Application.isPlaying)
        {
            return;
        }

        SyncColliderSettingsFromComponent();
    }

    Vector3 GetAbsoluteLossyScale()
    {
        Vector3 scale = transform.lossyScale;
        return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    void OnDrawGizmos()
    {
        // 用不同颜色显示物体碰撞体
        Gizmos.color = Color.yellow;
        Vector3 worldCenter = transform.position + colliderCenter;
        if (colliderType == ColliderType.Sphere)
        {
            Gizmos.DrawWireSphere(worldCenter, colliderRadius);
        }
        else if (colliderType == ColliderType.Box)
        {
            Gizmos.DrawWireCube(worldCenter, colliderSize);
        }
        else
        {
            Gizmos.DrawWireCube(worldCenter, colliderSize);
        }
        
        // 显示物体ID
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"Entity: {entityId}");
        #endif
    }
}
