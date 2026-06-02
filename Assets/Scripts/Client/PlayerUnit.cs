using UnityEngine;

public class PlayerUnit : MonoBehaviour
{
    public uint clientId;
    public Transform unitTr => transform;

    public void UpdatePosition(Vector3i pos)
    {
        // unitTr.position = pos.ToVector3();
        unitTr.position = unitTr.position.MoveTowards(pos.ToVector3(), Time.deltaTime * 200f);
    }
}