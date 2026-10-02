using UnityEngine;

/// <summary>One authored S02 room root. No navigation, scene loading or narrative language.</summary>
public sealed class TowerRoom : MonoBehaviour
{
    public enum RoomKind { Passage, Gun, Blade, DevelopmentFinal, DevelopmentDisposition }
    [SerializeField] private string stageId;
    [SerializeField] private RoomKind kind;
    [SerializeField] private Bounds localBounds = new Bounds(new Vector3(0, 3.25f, 0), new Vector3(30, 11.5f, 1));
    [SerializeField] private ChandelierEncounter prop;
    [SerializeField] private GameObject exitBarrier;
    [SerializeField] private string propEventInstanceId;
    public string StageId => stageId;
    public RoomKind Kind => kind;
    public ChandelierEncounter Prop => prop;
    public string PropEventInstanceId => propEventInstanceId;
    public Vector2 Offset => transform.position;
    public Vector3 BossSpawn => transform.position + new Vector3(9, -1.71f, 0);
    public Bounds PlayableBounds => new Bounds(transform.position + localBounds.center, localBounds.size);
    public bool ExitOpen { get; private set; }
    public GameSessionFlow Flow { get; private set; }

    public void Bind(GameSessionFlow flow) => Flow = flow;
    public void SetExitOpen(bool open)
    {
        ExitOpen = open;
        if (exitBarrier != null) exitBarrier.SetActive(!open);
    }

    // The existing encounter's approved text is exercised on a distinct DEV instance at 50F.
    // No turret/NPC/threat result is represented by that test opportunity.
    public bool DevelopmentOpportunity => stageId == "floor50";
}
