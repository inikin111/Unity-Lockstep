using UnityEngine;

public struct EntityPhysics
{
    public Vector3i velocity;
    public Vector3i acceleration;
    public int mass;
    public int friction;
    public bool isStatic;
}

public class EntityUnit : MonoBehaviour
{
    [InspectorName("Physics settings")]
    public int mass = 10;
    public int friciton = 5;
    public bool isStatic = false;

    [InspectorName("Collider Settings")]
    [HideInInspector] public Vector3 colliderCenter = Vector3.zero;
    public Vector3 colliderSize = new Vector3(1, 1, 1);
    
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

    public EntityPhysics SourcePhysics()
    {
        return new EntityPhysics
        {
            velocity = Vector3i.Zero,
            acceleration = Vector3i.Zero,
            mass = mass,
            friction = friciton,
            isStatic = isStatic
        };
    }

    void OnDrawGizmos()
    {
        // 用不同颜色显示物体碰撞体
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + colliderCenter, colliderSize);
        
        // 显示物体ID
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"Entity: {entityId}");
        #endif
    }
}