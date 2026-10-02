using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class TowerDoor : MonoBehaviour
{
    private TowerRoom room;
    private void Awake() => room = GetComponentInParent<TowerRoom>();
    // Stay also admits an officer already against the gate when the encounter resolves.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (room != null && room.ExitOpen && other.GetComponentInParent<PlayerHealth>() != null)
            room.Flow?.RequestTowerExit(room);
    }
}
