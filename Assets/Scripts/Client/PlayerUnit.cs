using UnityEngine;

public class PlayerUnit : MonoBehaviour
{
    [InspectorName("Collider Settings")]
    [HideInInspector] public Vector3 colliderCenter = Vector3.zero;
    public Vector3 colliderSize = new Vector3(1, 1, 1);

    uint clientId;
    public Transform unitTr => transform;

    public void UpdatePosition(Vector3i pos)
    {
        // unitTr.position = pos.ToVector3();
        unitTr.position = unitTr.position.MoveTowards(pos.ToVector3(), Time.deltaTime * 200f);
    }

    public void SetClientId(uint clientId)
    {
        this.clientId = clientId;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + colliderCenter, colliderSize);
    }
}