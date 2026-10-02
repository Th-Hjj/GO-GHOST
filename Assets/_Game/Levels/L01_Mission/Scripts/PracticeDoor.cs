using UnityEngine;

/// <summary>Greybox practice-room door: a trigger the officer walks through. GameSessionFlow owns the transition.</summary>
public sealed class PracticeDoor : MonoBehaviour
{
    [SerializeField] private GameSessionFlow flow;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (flow != null && other.GetComponentInParent<PlayerHealth>() != null) flow.RequestEnterBossRoom();
    }
}
