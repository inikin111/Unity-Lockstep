using UnityEngine;

public class PlayerUnit : MonoBehaviour
{
    [InspectorName("Collider Settings")]
    [HideInInspector] public Vector3 colliderCenter = Vector3.zero;
    public Vector3 colliderSize = new Vector3(1, 1, 1);
    public float colliderRadius = 0.3f;

    uint clientId; // 暂时不知道干啥，留着吧
    public Transform unitTr => transform;

    bool isFirstFrame = true;
    public void UpdatePosition(Vector3i pos)
    {
        if (isFirstFrame)
        {
            unitTr.position = pos.ToVector3();
            isFirstFrame = false;
            return;
        }

        unitTr.position = unitTr.position.MoveTowards(pos.ToVector3(), Time.deltaTime * 200f);
    }

    public void SetClientId(uint clientId)
    {
        this.clientId = clientId;
    }

    public void UpdateColliderSize(Vector3i size)
    {
        colliderSize = size.ToVector3();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + colliderCenter, colliderSize);
        Gizmos.color = Color.antiqueWhite;
        Gizmos.DrawWireSphere(transform.position + colliderCenter, colliderRadius);
    }
}