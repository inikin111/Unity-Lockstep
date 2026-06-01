using UnityEngine;

public class PlayerUnit : MonoBehaviour
{
    public uint clientId;
    public Transform unitTr => transform;

    public void UpdatePosition(Vector3i pos)
    {
        unitTr.position = pos.ToVector3();
    }
}